
namespace Reminder
{
    public class ReminderWindowsService : BackgroundService
    {
        private readonly ILogger<ReminderWindowsService> _logger;
        //private ServiceConfig _config = new();
        private ReminderService _reminderService;
        private FileSystemWatcher? _fileWatcher;
        private DateTime _lastConfigLoad = DateTime.MinValue;

        public ReminderWindowsService(
            ReminderService reminderService,
            ILogger<ReminderWindowsService> logger)
        {
            _reminderService = reminderService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial configuration loading and configuration file watcher setup
            _reminderService.Prepare();

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                _reminderService.Execute();
                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
