namespace Reminder;

public class WebOpenerService : BackgroundService
{
    private readonly ILogger<WebOpenerService> _logger;
    private ServiceConfig _config = new();
    private FileSystemWatcher? _fileWatcher;
    private DateTime _lastConfigLoad = DateTime.MinValue;

    public WebOpenerService(ILogger<WebOpenerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial configuration loading
        LoadConfig();

        // Setup FileSystemWatcher to monitor file changes
        SetupFileWatcher();

        // Main service loop
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var page in _config.Pages)
            {
                if (ShouldOpenPageNow(page))
                {
                    await OpenWebPageAsync(page);
                    page.NextOpening = NextOpeningDateAndTime(page);
                    SaveConfig();
                }
            }

            // Check every minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = Path.GetFullPath(_config.ConfigFilePath);

            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Configuration file not found. Creating a default one.");
                _config.Pages = new List<WebPageConfig>
                {
                    new WebPageConfig { Url = "https://www.example.com", Frequency = "daily" }
                };
                SaveConfig();
                return;
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            string yamlContent = File.ReadAllText(configPath);
            _config = deserializer.Deserialize<ServiceConfig>(yamlContent) ?? new ServiceConfig();
            _lastConfigLoad = DateTime.Now;

            _logger.LogInformation("Configuration loaded successfully with {Count} pages", _config.Pages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
        }
    }

    private void SaveConfig()
    {
        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string yaml = serializer.Serialize(_config);
            File.WriteAllText(_config.ConfigFilePath, yaml);

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
        }
    }

    private void SetupFileWatcher()
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(_config.ConfigFilePath)) ?? ".";
        string filename = Path.GetFileName(_config.ConfigFilePath);

        _fileWatcher = new FileSystemWatcher(directory, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnConfigFileChanged;
        _fileWatcher.Created += OnConfigFileChanged;

        _logger.LogInformation("File watcher setup for {Path}", _config.ConfigFilePath);
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Avoid multiple reloads in case of rapid changes
        if ((DateTime.Now - _lastConfigLoad).TotalSeconds < 5)
            return;

        _logger.LogInformation("Configuration file changed, reloading...");

        // Small delay to ensure the file is completely written
        Thread.Sleep(500);
        LoadConfig();
    }

    private bool ShouldOpenPageNow(WebPageConfig page)
    {

        // Always open if it's the first time or the next opening is not set
        if (page.NextOpening == null)
            return true;

        // Check if we have reached the date & time
        if (DateTime.Now >= page.NextOpening.Value)
            return true;

        return false;
    }
    private DateTime NextOpeningDateAndTime(WebPageConfig page)
    {
        return page.Frequency.ToLower() switch
        {
            "daily" => DateTime.Now.AddDays(1),
            "weekly" => DateTime.Now.AddDays(7),
            "monthly" => DateTime.Now.AddMonths(1),
            "quarterly" => DateTime.Now.AddMonths(3),
            _ => DateTime.Now.AddDays(1)
        };
    }

    private async Task OpenWebPageAsync(WebPageConfig page)
    {
        try
        {
            _logger.LogInformation("Opening web page: {Url}", page.Url);

            // Opens the web page in the default browser
            ProcessStartInfo psi = new()
            {
                FileName = page.Url,
                UseShellExecute = true
            };

            Process.Start(psi);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open web page: {Url}", page.Url);
        }
    }
}
