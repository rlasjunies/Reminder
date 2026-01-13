using System.IO.Packaging;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Reminder.Reminder;

namespace Reminder.Compiler;

public class ExtensionInfo
{
    public string FileName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsLoaded { get; set; }
    public string? ErrorMessage { get; set; }
    public IEnumerable<ExtensionMenuItem>? MenuItems { get; set; }
}

public class CompilerBackgroundService(
    //CompilerService compilerService,
    ILogger<CompilerBackgroundService> logger) : BackgroundService
{

    private static readonly Dictionary<string, DateTime> _lastModifiedTimes = [];
    private static string _watchFolder;
    public static List<ExtensionInfo> Extensions { get; } = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set the directory to watch for extension files
        _watchFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions");

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

    private static List<string> ParseExtensionDependencies(string code)
    {
        var dependencies = new List<string>();

        // Look for comments:
        // Dependencies: Assembly1, Assembly2
        // Dependencies: Assembly3
        // Or:
        // Dependencies:
        //   Assembly1
        //   Assembly2
        var lines = code.Split('\n');
        bool inDependenciesSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("// Dependencies:", StringComparison.OrdinalIgnoreCase))
            {
                inDependenciesSection = true;
                var depString = trimmed.Substring("// Dependencies:".Length).Trim();
                if (!string.IsNullOrEmpty(depString))
                {
                    // Parse dependencies on same line
                    var deps = depString.Split(',');
                    foreach (var dep in deps)
                    {
                        var cleanDep = dep.Trim();
                        if (!string.IsNullOrEmpty(cleanDep))
                        {
                            dependencies.Add(cleanDep);
                        }
                    }
                }
            }
            else if (inDependenciesSection && trimmed.StartsWith("//"))
            {
                // Parse continuation lines like "//   Assembly1"
                var depString = trimmed.Substring(2).Trim();
                if (!string.IsNullOrEmpty(depString))
                {
                    // Check if it contains commas (multiple deps on one line)
                    var deps = depString.Split(',');
                    foreach (var dep in deps)
                    {
                        var cleanDep = dep.Trim();
                        if (!string.IsNullOrEmpty(cleanDep))
                        {
                            dependencies.Add(cleanDep);
                        }
                    }
                }
            }
            else if (inDependenciesSection && !trimmed.StartsWith("//"))
            {
                // End of dependency comments section
                break;
            }
        }

        return dependencies;
    }

    private static void CompileAndRunFile(string filePath, ILogger<CompilerBackgroundService> logger)
    {
        Console.WriteLine($"Compiling: {filePath}");

        try
        {
            string code = File.ReadAllText(filePath);

            // Parse extension dependencies from comments
            var extensionDependencies = ParseExtensionDependencies(code);
            logger.LogInformation($"Found {extensionDependencies.Count} extension dependencies: {string.Join(", ", extensionDependencies)}");

            // References needed for compilation
            var references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Windows.Forms.MessageBox).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
                    MetadataReference.CreateFromFile(typeof(DateTime).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(StreamReader).Assembly.Location)
                };

            // Get System.Runtime
            string runtimePath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll");
            if (File.Exists(runtimePath))
            {
                references.Add(MetadataReference.CreateFromFile(runtimePath));
            }

            // Add Microsoft.Extensions.Logging for AddLogging
            try
            {
                var loggingAssembly = Assembly.Load("Microsoft.Extensions.Logging");
                references.Add(MetadataReference.CreateFromFile(loggingAssembly.Location));
                logger.LogInformation($"Added reference to Microsoft.Extensions.Logging");
            }
            catch
            {
                logger.LogWarning("Could not load Microsoft.Extensions.Logging");
            }

            // Add extension-specific dependencies
            foreach (var dep in extensionDependencies)
            {
                try
                {
                    var assembly = Assembly.Load(dep);
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    logger.LogInformation($"✓ Added extension dependency: {dep} from {assembly.Location}");
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"✗ Could not load extension dependency: {dep} - {ex.Message}");
                }
            }

            // Add System assemblies
            try
            {
                var systemAssemblies = new[]
                {
                    "System.Text.Json",
                    "System.IO",
                    "System.Threading.Tasks",
                    "System.Threading",
                    "System.Linq",
                    "System.Collections",
                    "netstandard"
                };

                foreach (var assemblyName in systemAssemblies)
                {
                    try
                    {
                        var assembly = Assembly.Load(assemblyName);
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                        logger.LogInformation($"Added reference to {assemblyName}");
                    }
                    catch
                    {
                        logger.LogWarning($"Could not load {assemblyName}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Warning loading System references: {ex.Message}");
            }

            // Add ASP.NET Core references for Minimal API support
            try
            {
                var aspNetCoreAssemblies = new[]
                {
                    "Microsoft.AspNetCore",
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Hosting.Abstractions",
                    "Microsoft.AspNetCore.Http",
                    "Microsoft.AspNetCore.Http.Abstractions",
                    "Microsoft.AspNetCore.Http.Results",
                    "Microsoft.AspNetCore.Routing",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Hosting.Abstractions",
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.DependencyInjection.Abstractions",
                    "Microsoft.Net.Http.Headers"
                };

                foreach (var assemblyName in aspNetCoreAssemblies)
                {
                    try
                    {
                        var assembly = Assembly.Load(assemblyName);
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                        logger.LogInformation($"Added reference to {assemblyName}");
                    }
                    catch
                    {
                        // Assembly not available, skip it
                        logger.LogWarning($"Could not load {assemblyName}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Warning loading ASP.NET Core references: {ex.Message}");
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
                logger.LogWarning("Could not load Microsoft.Extensions.Logging.Abstractions: {Message}", ex.Message);
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
                    string errorMessage = sb.ToString();
                    logger.LogError(errorMessage);
                    Console.WriteLine(errorMessage);

                    // Update extension info with error
                    lock (Extensions)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var existing = Extensions.FirstOrDefault(e => e.FileName == fileName);
                        if (existing != null)
                        {
                            existing.IsLoaded = false;
                            existing.ErrorMessage = errorMessage;
                        }
                        else
                        {
                            Extensions.Add(new ExtensionInfo
                            {
                                FileName = fileName,
                                Name = fileName,
                                IsLoaded = false,
                                ErrorMessage = errorMessage
                            });
                        }
                    }

                    // Show error popup
                    ShowCompilationErrorPopup(Path.GetFileName(filePath), errorMessage);
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

                                    // Update extension info
                                    lock (Extensions)
                                    {
                                        var fileName = Path.GetFileName(filePath);
                                        var existing = Extensions.FirstOrDefault(e => e.FileName == fileName);
                                        if (existing != null)
                                        {
                                            existing.Name = script.Name;
                                            existing.Version = script.Version;
                                            existing.Description = script.Description;
                                            existing.IsLoaded = true;
                                            existing.ErrorMessage = null;
                                            existing.MenuItems = script.GetMenuItems();
                                        }
                                        else
                                        {
                                            Extensions.Add(new ExtensionInfo
                                            {
                                                FileName = fileName,
                                                Name = script.Name,
                                                Version = script.Version,
                                                Description = script.Description,
                                                IsLoaded = true,
                                                ErrorMessage = null,
                                                MenuItems = script.GetMenuItems()
                                            });
                                        }
                                    }

                                    logger.LogInformation("Loaded extension: {} v{}", script.Name, script.Version);

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

    private static void ShowCompilationErrorPopup(string fileName, string errorMessage)
    {
        // Show popup in UI thread
        var thread = new Thread(() =>
        {
            var form = new CompilationErrorForm(fileName, errorMessage);
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}

public class CompilationErrorForm : Form
{
    private TextBox errorTextBox;
    private Button copyButton;

    public CompilationErrorForm(string fileName, string errorMessage)
    {
        InitializeComponents(fileName, errorMessage);
    }

    private void InitializeComponents(string fileName, string errorMessage)
    {
        this.Text = $"Compilation Error - {fileName}";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MinimizeBox = true;
        this.MaximizeBox = true;

        // Copy button at top
        copyButton = new Button
        {
            Text = "📋 Copy Error to Clipboard",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        copyButton.FlatAppearance.BorderSize = 0;
        copyButton.Click += CopyButton_Click;

        // Error text box
        errorTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            Text = errorMessage,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220)
        };

        this.Controls.Add(errorTextBox);
        this.Controls.Add(copyButton);
    }

    private void CopyButton_Click(object sender, EventArgs e)
    {
        try
        {
            Clipboard.SetText(errorTextBox.Text);
            copyButton.Text = "✓ Copied to Clipboard!";
            copyButton.BackColor = Color.FromArgb(16, 124, 16);

            // Reset button text after 2 seconds
            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, args) =>
            {
                copyButton.Text = "📋 Copy Error to Clipboard";
                copyButton.BackColor = Color.FromArgb(0, 120, 215);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
