using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Reminder.Reminder;
using System.IO.Packaging;
using System.Reflection;
using System.Text;

namespace Reminder.Compiler;

public class CompilerBackgroundService(
    //CompilerService compilerService,
    ILogger<CompilerBackgroundService> logger) : BackgroundService
{

    private static readonly Dictionary<string, DateTime> _lastModifiedTimes = [];
    private static string _watchFolder;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //// Initial configuration loading and configuration file watcher setup
        //compilerService.Prepare();

        //// Main service loop
        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    compilerService.Execute();
        //    // Check every minute
        //    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        //}

        // Set the directory to watch - change this to your target directory
        _watchFolder = Path.Combine(Environment.CurrentDirectory, "extensions");

        // Ensure the directory exists
        if (!Directory.Exists(_watchFolder))
        {
            Directory.CreateDirectory(_watchFolder);
            logger.LogInformation("Created directory: {}", _watchFolder);
        }

        // Initialize the file watcher
        FileSystemWatcher watcher = new()
        {
            Path = _watchFolder,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.cs",
            EnableRaisingEvents = true
        };

        // Add event handlers
        watcher.Changed += (sender, e) => OnFileChanged(e, logger); ;
        watcher.Created += (sender, e) => OnFileChanged(e, logger); ;

        // Compile any existing files
        CompileAndRunAllExistingFiles(logger);

        logger.LogInformation("Watching for changes in: {}", _watchFolder);
    }

    private static void CompileAndRunAllExistingFiles(ILogger<CompilerBackgroundService> logger)
    {
        string[] files = Directory.GetFiles(_watchFolder, "*.cs");
        foreach (string file in files)
        {
            _lastModifiedTimes[file] = File.GetLastWriteTime(file);
            CompileAndRunFile(file, logger);
        }
    }

    private static void OnFileChanged(FileSystemEventArgs e, ILogger<CompilerBackgroundService> logger)
    {
        try
        {
            // Add a small delay to ensure the file is fully written
            Thread.Sleep(500);

            DateTime lastModified = File.GetLastWriteTime(e.FullPath);

            if (!_lastModifiedTimes.TryGetValue(e.FullPath, out DateTime value) || lastModified > value)
            {
                value = lastModified;
                _lastModifiedTimes[e.FullPath] = value;
                CompileAndRunFile(e.FullPath, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing file change");
        }
    }

    private static void CompileAndRunFile(string filePath, ILogger<CompilerBackgroundService> logger)
    {
        Console.WriteLine($"Compiling: {filePath}");

        try
        {
            string code = File.ReadAllText(filePath);

            // References needed for compilation
            var references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Windows.Forms.MessageBox).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
                };

            // Get System.Runtime
            string runtimePath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll");
            if (File.Exists(runtimePath))
            {
                references.Add(MetadataReference.CreateFromFile(runtimePath));
            }
            //runtimePath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "Microsoft.Extensions.Logging.Abstractions");
            //if (File.Exists(runtimePath))
            //{
            //    references.Add(MetadataReference.CreateFromFile(runtimePath));
            //}

            // Add Microsoft.Extensions.Logging.Abstractions for ILogger
            try
            {
                // Try to find the assembly in the current application
                var loggerAssembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions");
                references.Add(MetadataReference.CreateFromFile(loggerAssembly.Location));
                Console.WriteLine($"Added reference to Logging.Abstractions from: {loggerAssembly.Location}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load Microsoft.Extensions.Logging.Abstractions: {ex.Message}");
                Console.WriteLine("Scripts that use ILogger may not compile correctly.");

                // You might want to provide a default path to the DLL or prompt user for path
                // Example of specifying an explicit path:
                string nugetPackagesFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages");

                // This path will need to be adjusted based on installed version
                string[] possibleVersions = { "6.0.0", "5.0.0", "7.0.0", "3.1.0" };
                string[] possibleFrameworks = { "net6.0", "net5.0", "netstandard2.1", "netstandard2.0" };

                bool found = false;
                foreach (var version in possibleVersions)
                {
                    foreach (var framework in possibleFrameworks)
                    {
                        string loggingPath = Path.Combine(
                            nugetPackagesFolder,
                            "microsoft.extensions.logging.abstractions",
                            version,
                            "lib", framework,
                            "Microsoft.Extensions.Logging.Abstractions.dll");

                        if (File.Exists(loggingPath))
                        {
                            references.Add(MetadataReference.CreateFromFile(loggingPath));
                            logger.LogInformation($"Added reference to Logging.Abstractions from NuGet cache: {loggingPath}");
                            found = true;
                            break;
                        }
                    }
                    if (found) 
                        break;
                }

                if (!found)
                {
                    logger.LogWarning("Could not find Microsoft.Extensions.Logging.Abstractions.dll. Scripts using ILogger may not compile.");
                }

                //if (File.Exists(loggingPath))
                //{
                //    references.Add(MetadataReference.CreateFromFile(loggingPath));
                //    Console.WriteLine($"Added reference to Logging.Abstractions from NuGet cache");
                //}
            }


            // Create compilation
            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(filePath),
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code) },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)
            );

            // Emit the compiled code
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // Handle compilation errors
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Compilation Errors in {Path.GetFileName(filePath)}:");

                    foreach (Diagnostic diagnostic in result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error))
                    {
                        sb.AppendLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                    }
                    logger.LogInformation(sb.ToString());
                    Console.WriteLine(sb.ToString());
                }
                else
                {
                    // Reset the position to the beginning of the memory stream
                    ms.Seek(0, SeekOrigin.Begin);

                    // Load the assembly
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // Run the compiled assembly in a separate thread
                    Thread thread = new Thread(() =>
                    {
                        try
                        {
                            // Find all types that implement IScript
                            foreach (Type type in assembly.GetTypes())
                            {
                                if (typeof(IExtension).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                                {
                                    Console.WriteLine($"Found script implementation: {type.Name}");

                                    // Create an instance of the script
                                    IExtension script = (IExtension)Activator.CreateInstance(type);

                                    // Call Prepare method first
                                    logger.LogInformation("Calling Prepare() on {}", type.Name);
                                    script.Prepare(logger);

                                    // Then call Execute method
                                    Console.WriteLine($"Calling Execute() on {type.Name}");
                                    logger.LogInformation("Calling Execute() on {}", type.Name);
                                    script.Execute(logger);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Runtime Error: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                            }
                        }
                    });

                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
}
