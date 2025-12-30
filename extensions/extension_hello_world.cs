using System;
using System.Collections.Generic;
using System.Windows.Forms;
//using CSharpCompilerWatcher;
using Microsoft.Extensions.Logging;
using Reminder.Compiler;

namespace test
{
    public class HelloWorldScript : IExtension
    {
        public string Name => "Hello World Extension example";
        public string Version => "1.0.0"; 
        public string Description => "Simple example showing a message box with a greeting";
        
        private string _message;
        
        public IEnumerable<ExtensionMenuItem>? GetMenuItems() => null; // No menu items
        
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
