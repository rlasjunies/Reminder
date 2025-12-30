using Reminder.Compiler;
using System;
using System.Windows.Forms;
//using CSharpCompilerWatcher;
using Microsoft.Extensions.Logging;

namespace test
{
    public class HelloWorldScript : IExtension
    {
        //private ILogger _logger;

        private string _message;
        public void Prepare(ILogger logger)
        {
            // In a real application, you'd inject the logger
            // _logger = loggerFactory.CreateLogger<MyScript>();
            logger.LogInformation("Preparing script");
            Console.WriteLine("Preparing HelloWorldScript...");
            //logger.LogInformation("Preparing HelloWorldScript...");
            _message = "Hello, World!";
            // You could load configurations, initialize resources, etc. here
        }

        public void Execute(ILogger logger)
        {
            logger.LogInformation("Executing HelloWorldScript...");

            try
            {
                MessageBox.Show(_message, "Message from Script", MessageBoxButtons.OK, MessageBoxIcon.Information);
                logger.LogInformation("Message box displayed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to display message box");
                throw;
            }
        }
    }
}
