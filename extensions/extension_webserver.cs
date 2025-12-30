// Dependencies:
//   System.Text.Json
//   Microsoft.Net.Http.Headers
//   Microsoft.AspNetCore.Http.Results
//   System.Runtime
//   System.IO
//   System.Threading.Tasks
//   System.Threading
//   System.Private.CoreLib
//   System.Collections.Generic
//   System.Diagnostics.Process
//   System.ComponentModel.Primitives
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reminder.Compiler;

namespace ReminderExtensions
{
    /// <summary>
    /// Web Server Extension - Demonstrates ASP.NET Core Minimal API
    /// This extension creates a lightweight web server that serves HTML pages
    /// </summary>
    public class WebServerExtension : IExtension
    {
        public string Name => "Web Server Extension example";
        public string Version => "1.0.0";
        public string Description => "ASP.NET Core Minimal API web server on port 5001 with HTML interface and REST endpoints";
        private WebApplication _host;
        private int _port = 5001;
        private ILogger _logger;

        public IEnumerable<ExtensionMenuItem>? GetMenuItems()
        {
            yield return new ExtensionMenuItem
            {
                Text = "Show Portal",
                Enabled = true,
                OnClick = () =>
                {
                    try
                    {
                        var url = $"http://localhost:{_port}";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to open portal");
                    }
                }
            };
        }

        public void Prepare(ILogger logger)
        {
            try
            {
                _logger = logger;
                logger.LogInformation("Preparing Web Server Extension");

                // Build the web application
                var builder = WebApplication.CreateBuilder();

                // Configure services
                builder.Services.AddLogging();

                // Configure Kestrel to listen on specific port
                builder.WebHost.UseUrls($"http://localhost:{_port}");

                var app = builder.Build();

                // Root endpoint - serves a nice HTML page
                app.MapGet("/", () => Results.Content(HomePageHtml, "text/html"));

                // About page
                app.MapGet("/about", () => Results.Content(AboutPageHtml, "text/html"));

                // API endpoint - returns JSON status
                app.MapGet("/api/status", () => Results.Json(new
                {
                    status = "running",
                    timestamp = DateTime.Now,
                    uptime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    message = "Web server extension is running successfully!",
                    endpoints = new[]
                    {
                    "/",
                    "/about",
                    "/api/status",
                    "/api/echo"
                }
                }));

                // API endpoint - echo back posted message
                app.MapPost("/api/echo", async (HttpContext context) =>
                {
                    try
                    {
                        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                        return Results.Json(new
                        {
                            received = body,
                            timestamp = DateTime.Now,
                            echo = $"You sent: {body}"
                        });
                    }
                    catch
                    {
                        return Results.BadRequest(new { error = "Invalid request body" });
                    }
                });

                _host = app;

                logger.LogInformation($"Web server prepared on http://localhost:{_port}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to prepare web server: {Message}", ex.Message);
                throw;
            }
        }

        public void Execute(ILogger logger)
        {
            try
            {
                logger.LogInformation("Starting web server...");

                // Start the web server asynchronously
                var startTask = Task.Run(async () =>
                {
                    try
                    {
                        await _host.StartAsync();

                        // Keep running
                        await Task.Delay(Timeout.Infinite);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Web server stopped with error");
                    }
                });

                // Wait a bit to see if startup fails immediately
                Task.Delay(1000).Wait();

                if (startTask.IsFaulted)
                {
                    logger.LogError("Web server failed to start!");
                    if (startTask.Exception != null)
                    {
                        logger.LogError("Startup exception: {Exception}", startTask.Exception);
                    }
                    throw new Exception("Web server startup failed", startTask.Exception);
                }

                logger.LogInformation("Web server running at http://localhost:{Port}", _port);
                logger.LogInformation("Endpoints: GET /, /about, /api/status | POST /api/echo");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start web server");
                throw;
            }
        }

        #region HTML Content

        private const string HomePageHtml = @"""
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Reminder Extension - Web Server</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }
        .container {
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            padding: 40px;
            max-width: 800px;
            width: 100%;
        }
        h1 {
            color: #667eea;
            margin-bottom: 20px;
            font-size: 2.5em;
        }
        h2 {
            color: #764ba2;
            margin-top: 30px;
            margin-bottom: 15px;
        }
        p {
            line-height: 1.6;
            color: #333;
            margin-bottom: 15px;
        }
        .feature {
            background: #f7f7f7;
            padding: 15px;
            border-radius: 10px;
            margin-bottom: 10px;
            border-left: 4px solid #667eea;
        }
        .api-endpoint {
            font-family: 'Courier New', monospace;
            background: #2d2d2d;
            color: #f8f8f2;
            padding: 10px;
            border-radius: 5px;
            margin: 5px 0;
        }
        .method {
            color: #50fa7b;
            font-weight: bold;
        }
        .path {
            color: #8be9fd;
        }
        button {
            background: #667eea;
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 8px;
            cursor: pointer;
            font-size: 16px;
            margin: 10px 5px;
            transition: background 0.3s;
        }
        button:hover {
            background: #764ba2;
        }
        .status {
            margin-top: 20px;
            padding: 15px;
            border-radius: 8px;
            background: #e8f5e9;
            border: 1px solid #4caf50;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🚀 Reminder Extension Web Server</h1>
        <p>Welcome! This is a dynamically compiled ASP.NET Core Minimal API running as a Reminder extension.</p>
        
        <h2>✨ Features</h2>
        <div class='feature'>
            <strong>Dynamic Compilation:</strong> This extension was compiled at runtime from a .cs file
        </div>
        <div class='feature'>
            <strong>Minimal API:</strong> Lightweight and fast ASP.NET Core endpoint handling
        </div>
        <div class='feature'>
            <strong>Hot Reload:</strong> Modify the extension file and it will be recompiled automatically
        </div>
        
        <h2>🔗 Available API Endpoints</h2>
        <div class='api-endpoint'><span class='method'>GET</span> <span class='path'>/</span> - This page</div>
        <div class='api-endpoint'><span class='method'>GET</span> <span class='path'>/about</span> - About page</div>
        <div class='api-endpoint'><span class='method'>GET</span> <span class='path'>/api/status</span> - Server status JSON</div>
        <div class='api-endpoint'><span class='method'>POST</span> <span class='path'>/api/echo</span> - Echo back your message</div>
        
        <h2>🧪 Try It Out</h2>
        <button onclick='testStatus()'>Test Status API</button>
        <button onclick='testEcho()'>Test Echo API</button>
        <button onclick='window.location.href=""/about""'>View About Page</button>
        
        <div id='result'></div>
    </div>
    
    <script>
        async function testStatus() {
            try {
                const response = await fetch('/api/status');
                const data = await response.json();
                document.getElementById('result').innerHTML = 
                    '<div class=""status""><strong>Status Response:</strong><br>' + 
                    '<pre>' + JSON.stringify(data, null, 2) + '</pre></div>';
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
        
        async function testEcho() {
            const message = prompt('Enter a message to echo:');
            if (!message) return;
            
            try {
                const response = await fetch('/api/echo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message: message })
                });
                const data = await response.json();
                document.getElementById('result').innerHTML = 
                    '<div class=""status""><strong>Echo Response:</strong><br>' + 
                    '<pre>' + JSON.stringify(data, null, 2) + '</pre></div>';
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    </script>
</body>
</html>""";

        private const string AboutPageHtml = @"""
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>About - Reminder Web Server</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }
        .container {
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            padding: 40px;
            max-width: 800px;
            width: 100%;
        }
        h1 { color: #667eea; margin-bottom: 20px; }
        p { line-height: 1.8; color: #333; margin-bottom: 15px; }
        a {
            color: #667eea;
            text-decoration: none;
            font-weight: bold;
        }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>📖 About This Extension</h1>
        <p>
            This web server extension demonstrates the power of the Reminder application's 
            extension system. It showcases how you can create sophisticated functionality 
            by simply dropping a .cs file into the extensions folder.
        </p>
        <p>
            <strong>Technology Stack:</strong>
        </p>
        <ul style='margin-left: 20px; line-height: 2;'>
            <li>ASP.NET Core Minimal API</li>
            <li>Roslyn Dynamic Compilation</li>
            <li>Kestrel Web Server</li>
            <li>IExtension Interface</li>
        </ul>
        <p style='margin-top: 20px;'>
            <a href='/'>← Back to Home</a>
        </p>
    </div>
</body>
</html>""";

        #endregion
    }
}