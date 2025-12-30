using Reminder.Compiler;

namespace Reminder.Reminder;

public class ReminderBackgroundService(
    ReminderService reminderService,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial configuration loading and configuration file watcher setup
        reminderService.Prepare();

        // Main service loop
        while (!stoppingToken.IsCancellationRequested)
        {
            reminderService.Execute();
            // Check every minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
