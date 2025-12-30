// Dependencies:
//   YamlDotNet
//   System.IO
//   System.IO.FileSystem.Watcher
//   System.Diagnostics.Process
//   System.ComponentModel.Primitives
//   System.Windows.Forms
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reminder.Compiler;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReminderExtensions
{
    public class ReminderExtension : IExtension
    {
        public string Name => "Reminder - Core Functionality";
        public string Version => "1.0.0";
        public string Description => "Reads config.yaml and opens URLs at scheduled times";

        private static readonly string CONFIG_FILE = "config.yaml";
        private ServiceConfig _config = new();
        private FileSystemWatcher? _fileWatcher;
        private DateTime _lastConfigLoad = DateTime.MinValue;
        private ILogger? _logger;
        private Timer? _executionTimer;

        public void Prepare(ILogger logger)
        {
            _logger = logger;
            logger.LogInformation("Preparing Reminder Extension");
            LoadConfig();
            SetupFileWatcher();
        }

        public void Execute(ILogger logger)
        {
            logger.LogInformation("Starting Reminder Extension - checking every minute");

            // Execute immediately first time
            CheckAndOpenPages(logger);

            // Then schedule to run every minute
            _executionTimer = new Timer(_ => CheckAndOpenPages(logger), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public IEnumerable<ExtensionMenuItem> GetMenuItems()
        {
            return new[]
            {
                new ExtensionMenuItem
                {
                    Text = "Show Config File",
                    OnClick = () => OpenFile(GetConfigFilePath())
                },
                new ExtensionMenuItem
                {
                    Text = "Show Readme File",
                    OnClick = () => OpenFile(Path.Combine(AppContext.BaseDirectory, "readme.md"))
                }
            };
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, CONFIG_FILE);
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(GetConfigFilePath()))
                {
                    _logger?.LogWarning("Configuration file not found. Creating a default one.");
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

                string yamlContent = File.ReadAllText(GetConfigFilePath());
                _config = deserializer.Deserialize<ServiceConfig>(yamlContent) ?? new ServiceConfig();
                _lastConfigLoad = DateTime.Now;

                _logger?.LogInformation("Configuration loaded successfully with {Count} pages", _config.Pages.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load configuration");
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
                File.WriteAllText(GetConfigFilePath(), yaml);
                _logger?.LogInformation("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save configuration");
            }
        }

        private void SetupFileWatcher()
        {
            try
            {
                _fileWatcher = new FileSystemWatcher
                {
                    Path = AppContext.BaseDirectory,
                    Filter = CONFIG_FILE,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += (sender, e) =>
                {
                    // Debounce: only reload if it's been at least 1 second since last load
                    if ((DateTime.Now - _lastConfigLoad).TotalSeconds > 1)
                    {
                        Thread.Sleep(100); // Small delay to ensure file is fully written
                        _logger?.LogInformation("Configuration file changed, reloading...");
                        LoadConfig();
                    }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to setup file watcher");
            }
        }

        private void CheckAndOpenPages(ILogger logger)
        {
            try
            {
                DateTime now = DateTime.Now;
                bool configChanged = false;

                foreach (var page in _config.Pages)
                {
                    if (page.NextOpening == null || page.NextOpening <= now)
                    {
                        // Time to open this page
                        logger.LogInformation("Opening web page: {Url}", page.Url);
                        OpenUrl(page.Url);

                        // Calculate next opening time
                        page.NextOpening = CalculateNextOpening(now, page.Frequency);
                        configChanged = true;

                        logger.LogInformation("Next opening for {Url} scheduled at {NextOpening}",
                            page.Url, page.NextOpening);
                    }
                }

                if (configChanged)
                {
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking pages");
            }
        }

        private DateTime CalculateNextOpening(DateTime from, string frequency)
        {
            return frequency.ToLower() switch
            {
                "daily" => from.AddDays(1),
                "weekly" => from.AddDays(7),
                "monthly" => from.AddMonths(1),
                "quarterly" => from.AddMonths(3),
                "yearly" => from.AddYears(1),
                _ => from.AddDays(1) // Default to daily
            };
        }

        private void OpenUrl(string url)
        {
            try
            {
                _logger?.LogInformation("Opening web page: {Url}", url);
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open URL: {Url}", url);
            }
        }

        private void OpenFile(string filePath)
        {
            try
            {
                _logger?.LogInformation("Attempting to open file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger?.LogWarning("File does not exist: {FilePath}", filePath);
                    System.Windows.Forms.MessageBox.Show($"File not found: {filePath}", "Error",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                _logger?.LogInformation("File exists, launching with explorer...");

                // Use explorer to open the file (more reliable for file associations)
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false
                };
                Process.Start(psi);

                _logger?.LogInformation("File opened successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open file: {FilePath}", filePath);
                System.Windows.Forms.MessageBox.Show($"Error opening file: {ex.Message}", "Error",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }

    // Configuration classes
    public class ServiceConfig
    {
        public List<WebPageConfig> Pages { get; set; } = new();
    }

    public class WebPageConfig
    {
        public string Url { get; set; } = "";
        public string Frequency { get; set; } = "daily";
        public DateTime? NextOpening { get; set; }
    }
}
