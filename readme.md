# Reminder

The purpose of this tool is to remind some topics we feel important by poping up urls at a regular period.

You can use it to popup daily a energizing sentence, to remind you the goal of the month, to remind you good practice ...

## How to use?

Just start the application clicking on 'Reminder.exe', a new icon will appears in the systray. If you already configured some pages, they will be opened at the right time.

You can add, remove, update pages directly in the configuration file, which is accessible from the context menu in the systray app.
The configuration will be reloaded automatically as soon you save it.

The application execute the configuration every minute.

The application will automatically register itself to start at windows startup.

## 🔌 Extension System

Reminder includes a powerful extension system that allows you to extend the application's functionality by simply dropping C# files into the `extensions/` folder. These files are automatically compiled and executed at runtime using Roslyn.

### How Extensions Work

1. **Drop a .cs file** into the `extensions/` folder
2. **Automatic compilation** happens when the file is created or modified
3. **Instant execution** without rebuilding the entire application
4. **Hot reload** support - edit and save to see changes immediately

### Creating Your Own Extension

Extensions must implement the `IExtension` interface:

```csharp
// Dependencies:
//   Any.Required.Assembly
//   Another.Assembly
using Reminder.Compiler;
using Microsoft.Extensions.Logging;

namespace MyExtensions
{
    public class MyExtension : IExtension
    {
        public string Name => "My Custom Extension";
        public string Version => "1.0.0";
        public string Description => "Brief description of what this extension does";

        public void Prepare(ILogger logger)
        {
            // Initialize your extension here
            // Load configurations, setup resources, etc.
            logger.LogInformation("Preparing my extension");
        }

        public void Execute(ILogger logger)
        {
            // Main logic goes here
            // This is called after Prepare()
            logger.LogInformation("Executing my extension");
        }
    }
}
```

**Required Properties:**

- `Name` - Friendly name for your extension (shown in systray menu)
- `Version` - Version number (shown in systray menu)
- `Description` - Brief description of functionality (shown in systray menu)

**Required Methods:**

- `Prepare(ILogger)` - Initialization method called first
- `Execute(ILogger)` - Main execution method called after Prepare

### Declaring Dependencies

Extensions can declare their required assemblies using comments at the top of the file:

```csharp
// Dependencies:
//   System.Text.Json
//   Microsoft.AspNetCore.Http.Results
//   System.IO
//   System.Threading.Tasks
```

The compiler will automatically load these assemblies during compilation. Common dependencies include:

- `System.Text.Json` - JSON serialization
- `Microsoft.AspNetCore.*` - Web server functionality
- `System.IO` - File operations
- `System.Threading.Tasks` - Async operations
- `System.Runtime` - Core types (DateTime, Exception, etc.)

### Viewing Loaded Extensions

Open the systray menu to see the **"Loaded Extensions"** submenu. It displays:

- ✅ Successfully loaded extensions with name, version, and description
- ❌ Failed extensions with error details

Click on a failed extension to see compilation errors.

### Example Extensions

#### 1. Hello World Extension (`extension_hello_world.cs`)

Simple MessageBox demonstration:

```csharp
public class HelloWorldScript : IExtension
{
    public string Name => "Hello World Extension";
    public string Version => "1.0.0";
    public string Description => "Simple example showing a message box";
    
    private string _message;
    
    public void Prepare(ILogger logger)
    {
        _message = "Hello, World!";
    }

    public void Execute(ILogger logger)
    {
        MessageBox.Show(_message, "Message from Script", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
```

#### 2. Web Server Extension (`extension_webserver.cs`)

Full-featured ASP.NET Core web server with HTML interface and REST APIs:

```csharp
// Dependencies:
//   System.Text.Json
//   Microsoft.Net.Http.Headers
//   Microsoft.AspNetCore.Http.Results
//   System.Runtime
//   System.IO
//   System.Threading.Tasks
//   System.Threading
//   System.Private.CoreLib
```

**Features:**

- ASP.NET Core Minimal API
- Beautiful HTML/CSS/JavaScript interface
- Multiple endpoints (pages and APIs)
- Real-time status monitoring

**Endpoints:**

- `GET /` - Main web interface with interactive features
- `GET /about` - About page with extension information
- `GET /api/status` - JSON status endpoint
- `POST /api/echo` - Echo API for testing

Visit `http://localhost:5001` in your browser after starting the application.

### Technical Capabilities

Extensions have access to:

- ✅ .NET 9.0 Framework
- ✅ Windows Forms (MessageBox, Forms, Controls)
- ✅ ASP.NET Core (Minimal API, Kestrel server, middleware)
- ✅ Microsoft.Extensions.Logging for structured logging
- ✅ All standard .NET libraries (System.*, Microsoft.*)
- ✅ File system access
- ✅ Network access
- ✅ Async/await patterns

### Use Cases

- **Web Dashboards** - Create monitoring or admin interfaces
- **API Servers** - Build REST APIs, webhooks, or microservices
- **Automation Scripts** - Schedule tasks and workflows
- **Custom Notifications** - Display alerts, reminders, or status updates
- **Data Processing** - Background data transformation or analysis
- **Integration Services** - Connect to external systems or databases
- **Development Tools** - Create utilities for development workflows

### Error Handling

If an extension fails to compile:

1. A popup window displays the compilation errors
2. The extension appears in the systray menu marked with ❌
3. Click the failed extension to view errors again
4. Use the "Copy Error" button to paste errors for debugging

Common errors and solutions:

- **Missing types** - Add required dependencies in the `// Dependencies:` section
- **Missing using statements** - Add appropriate `using` directives
- **Syntax errors** - Check C# syntax (same as regular C# files)

## Example of configuration file

To help you, a schema is provided in the repository. You can use it in your editor to have a better experience.

You have to specify the url of the page you want to open, the frequency of the reminder. The next opening date will be automatically calculated after the 1st opening. Anyway, it could be manually update to fit your needs in term of date and time.

```yaml
# yaml-language-server: $schema=./configSchema.json
pages:
  - url: https://www.example.com
    frequency: daily
    nextOpening: 2025-02-26T15:12:18Z
    
  # - url: onenote:https://siemens-my.sharepoint.com/personal/richard_lasjunies_siemens_com/Documents/Notebooks/Journal2025/Feb-25.one#2025%2002%2026&section-id={0AB20093-17D3-431C-A997-A28DA6B647D0}&page-id={B241A038-1642-4224-9397-42D0578A9F6A}&end
  #   frequency: weekly
  #   nextOpening: 2025-02-26T15:21:00Z
   
  - url: https://onedrive.live.com/redir?resid=3981CA5E56F3AD91%21387606&page=Edit&wd=target%28Reminder%20-%20strategy%20box.one%7Cff8cfb6b-8db5-426c-a9a2-10ed6d2c88c3%2FMonthly%20goals%7C5a2559be-5300-4193-82a0-13fa873d91ae%2F%29&wdorigin=703
    frequency: weekly
    
  # - url: https://www.example4.com
  #   frequency: quarterly
```
