//using Reminder.Reminder;

//namespace Reminder.Compiler;

//public class CompilerService(ILogger<CompilerService> logger)
//{
//    private static readonly string CONFIG_FILE = "config.yaml";
//    private ServiceConfig _config = new();
//    private FileSystemWatcher? _fileWatcher;
//    private DateTime _lastConfigLoad = DateTime.MinValue;

//    public static string GetConfigFilePath()
//    {
//        return Path.Combine(AppContext.BaseDirectory, CONFIG_FILE);
//    }
//    public void Prepare()
//    {
//        LoadConfig();
//        SetupFileWatcher();

//        void LoadConfig()
//        {
//            try
//            {

//                if (!File.Exists(GetConfigFilePath()))
//                {
//                    logger.LogWarning("Configuration file not found. Creating a default one.");
//                    _config.Pages =
//                        [
//                            new WebPageConfig { Url = "https://www.example.com", Frequency = "daily" }
//                        ];
//                    SaveConfig();
//                    return;
//                }

//                var deserializer = new DeserializerBuilder()
//                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
//                    .IgnoreUnmatchedProperties()
//                    .Build();

//                string yamlContent = File.ReadAllText(GetConfigFilePath());
//                _config = deserializer.Deserialize<ServiceConfig>(yamlContent) ?? new ServiceConfig();
//                _lastConfigLoad = DateTime.Now;

//                logger.LogInformation("Configuration loaded successfully with {Count} pages", _config.Pages.Count);
//            }
//            catch (Exception ex)
//            {
//                logger.LogError(ex, "Failed to load configuration");
//            }
//        }
//        void SetupFileWatcher()
//        {
//            string directory = AppContext.BaseDirectory;
//            string filename = CONFIG_FILE;

//            _fileWatcher = new FileSystemWatcher(directory, filename)
//            {
//                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
//                EnableRaisingEvents = true
//            };

//            _fileWatcher.Changed += OnConfigFileChanged;
//            _fileWatcher.Created += OnConfigFileChanged;

//            logger.LogInformation("File watcher setup for {Path}", CONFIG_FILE);


//            void OnConfigFileChanged(object sender, FileSystemEventArgs e)
//            {
//                // Avoid multiple reloads in case of rapid changes
//                if ((DateTime.Now - _lastConfigLoad).TotalSeconds < 5)
//                    return;

//                logger.LogInformation("Configuration file changed, reloading...");

//                // Small delay to ensure the file is completely written
//                Thread.Sleep(500);
//                LoadConfig();
//            }
//        }
//    }

//    private void SaveConfig()
//    {
//        try
//        {
//            var serializer = new SerializerBuilder()
//                .WithNamingConvention(CamelCaseNamingConvention.Instance)
//                .Build();

//            string yaml = serializer.Serialize(_config);
//            File.WriteAllText(GetConfigFilePath(), yaml);

//            logger.LogInformation("Configuration saved successfully");
//        }
//        catch (Exception ex)
//        {
//            logger.LogError(ex, "Failed to save configuration");
//        }
//    }

//    public void Execute()
//    {
//        var atLeastOpenOneUrl = false;
//        foreach (var page in _config.Pages)
//        {
//            if (ShouldOpenPageNow(page))
//            {
//                Task.Run(() => OpenUrlAsync(page));
//                CalculateNextOpeningDateAndTime(page);
//                atLeastOpenOneUrl = true;
//            }
//        }
//        if (atLeastOpenOneUrl) SaveConfig();

//        bool ShouldOpenPageNow(WebPageConfig page)
//        {

//            // Always open if it's the first time or the next opening is not set
//            if (page.NextOpening == null)
//                return true;

//            // Check if we have reached the date & time
//            if (DateTime.Now >= page.NextOpening.Value)
//                return true;

//            return false;
//        }
//        void CalculateNextOpeningDateAndTime(WebPageConfig page)
//        {
//            var existingPage = _config.Pages.FirstOrDefault(p => p.Url == page.Url && p.Frequency == page.Frequency);
//            if (existingPage != null)
//            {
//                var nextTimeFromFile = existingPage.NextOpening ?? DateTime.Now;

//                existingPage.NextOpening = page.Frequency.ToLower() switch
//                {
//                    "daily" => nextTimeFromFile.AddDays(1),
//                    "weekly" => nextTimeFromFile.AddDays(7),
//                    "monthly" => nextTimeFromFile.AddMonths(1),
//                    "quarterly" => nextTimeFromFile.AddMonths(3),
//                    _ => nextTimeFromFile.AddDays(1)
//                };
//            }
//        }
//        async Task OpenUrlAsync(WebPageConfig page)
//        {
//            try
//            {
//                logger.LogInformation("Opening web page: {Url}", page.Url);

//                // Opens the web page in the default browser
//                ProcessStartInfo psi = new()
//                {
//                    FileName = page.Url,
//                    UseShellExecute = true
//                };

//                Process.Start(psi);

//                await Task.CompletedTask;
//            }
//            catch (Exception ex)
//            {
//                logger.LogError(ex, "Failed to open web page: {Url}", page.Url);
//            }
//        }
//    }
//}
