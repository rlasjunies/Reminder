// Dependencies:
//   System.Text.Json
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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// Bookmarks Extension - Displays Edge browser bookmarks in a web interface
    /// </summary>
    public class BookmarksExtension : IExtension
    {
        public string Name => "Browser Bookmarks Viewer";
        public string Version => "1.0.0";
        public string Description => "Web interface to view and search Edge browser bookmarks on port 5005";

        private WebApplication? _host;
        private readonly int _port = 5005;
        private ILogger? _logger;

        public IEnumerable<ExtensionMenuItem>? GetMenuItems()
        {
            yield return new ExtensionMenuItem
            {
                Text = "Show Bookmarks",
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
                        _logger?.LogError(ex, "Failed to open bookmarks viewer");
                    }
                }
            };
        }

        public void Prepare(ILogger logger)
        {
            try
            {
                _logger = logger;
                logger.LogInformation("Preparing Bookmarks Extension");

                var builder = WebApplication.CreateBuilder();
                builder.Services.AddLogging();
                builder.WebHost.UseUrls($"http://localhost:{_port}");

                var app = builder.Build();

                // Main page
                app.MapGet("/", () => Results.Content(GetMainPageHtml(), "text/html"));

                // API endpoint
                app.MapGet("/api/bookmarks", GetBookmarks);

                _host = app;
                logger.LogInformation($"Bookmarks web server configured on port {_port}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to prepare Bookmarks Extension");
                throw;
            }
        }

        public void Execute(ILogger logger)
        {
            try
            {
                logger.LogInformation($"Starting Bookmarks web server on http://localhost:{_port}");
                _host?.RunAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start Bookmarks web server");
                throw;
            }
        }

        private IResult GetBookmarks()
        {
            try
            {
                var bookmarksPath = GetBookmarksPath();
                if (!File.Exists(bookmarksPath))
                    return Results.NotFound("Bookmarks file not found at: " + bookmarksPath);

                var json = File.ReadAllText(bookmarksPath);
                var bookmarks = JsonSerializer.Deserialize<BookmarksRoot>(json);
                return Results.Ok(bookmarks);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading bookmarks");
                return Results.Problem("Error reading bookmarks: " + ex.Message);
            }
        }

        private string GetBookmarksPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, @"AppData\Local\Microsoft\Edge\User Data\Default\Bookmarks");
        }

        private string GetMainPageHtml() => $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Edge Bookmarks Manager</title>
    <link rel=""icon"" href=""data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>📚</text></svg>"">
    <style>
        {GetThemeStyles()}
        {GetBaseStyles()}
    </style>
</head>
<body class=""theme-default"">
    <div class=""container"">
        <div class=""header"">
            <h1>📚 Edge Bookmarks Manager</h1>
            <div class=""controls"">
                <div class=""search-container"">
                    <input type=""text"" id=""searchInput"" placeholder=""Search bookmarks..."" autocomplete=""off"">
                    <span class=""search-icon"">🔍</span>
                </div>
                <select id=""themeSelector"">
                    {GetThemeOptions()}
                </select>
            </div>
        </div>
        <div class=""content"">
            <div class=""bookmarks-container"">
                <div id=""bookmarks"">Loading...</div>
            </div>
        </div>
        <div class=""footer"">
            <div class=""stats"" id=""stats"">Loading bookmarks...</div>
        </div>
    </div>
    <script>
        {GetClientScript()}
    </script>
</body>
</html>";

        private string GetThemeStyles() => @"
:root {
    --primary-color: #0078d4;
    --secondary-color: #106ebe;
    --background-color: #ffffff;
    --surface-color: #f5f5f5;
    --text-color: #323130;
    --text-secondary: #605e5c;
    --border-color: #d1d1d1;
    --hover-color: #f3f2f1;
    --shadow: 0 2px 4px rgba(0,0,0,0.1);
}
.theme-dark {
    --primary-color: #4fc3f7;
    --secondary-color: #29b6f6;
    --background-color: #121212;
    --surface-color: #1e1e1e;
    --text-color: #ffffff;
    --text-secondary: #b0b0b0;
    --border-color: #333333;
    --hover-color: #2a2a2a;
    --shadow: 0 2px 4px rgba(0,0,0,0.3);
}
.theme-blue {
    --primary-color: #2196f3;
    --secondary-color: #1976d2;
    --background-color: #e3f2fd;
    --surface-color: #bbdefb;
    --text-color: #0d47a1;
    --text-secondary: #1565c0;
    --border-color: #90caf9;
    --hover-color: #e1f5fe;
}
.theme-green {
    --primary-color: #4caf50;
    --secondary-color: #388e3c;
    --background-color: #e8f5e8;
    --surface-color: #c8e6c9;
    --text-color: #1b5e20;
    --text-secondary: #2e7d32;
    --border-color: #a5d6a7;
    --hover-color: #f1f8e9;
}";

        private string GetThemeOptions() => @"
<option value=""theme-default"">Default</option>
<option value=""theme-dark"">Dark Mode</option>
<option value=""theme-blue"">Ocean Blue</option>
<option value=""theme-green"">Nature Green</option>";

        private string GetBaseStyles() => @"
* { box-sizing: border-box; }
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    margin: 0;
    padding: 0;
    background-color: var(--background-color);
    color: var(--text-color);
    line-height: 1.5;
    height: 100vh;
    overflow: hidden;
}
.container {
    max-width: 1200px;
    margin: 0 auto;
    height: 100vh;
    display: flex;
    flex-direction: column;
}
.header {
    position: sticky;
    top: 0;
    z-index: 100;
    background-color: var(--background-color);
    border-bottom: 2px solid var(--border-color);
    padding: 20px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 20px;
    box-shadow: var(--shadow);
}
.content {
    flex: 1;
    overflow-y: auto;
    padding: 20px 20px 0 20px;
}
.footer {
    position: sticky;
    bottom: 0;
    z-index: 100;
    background-color: var(--background-color);
    border-top: 2px solid var(--border-color);
    padding: 15px 20px;
    box-shadow: 0 -2px 4px rgba(0,0,0,0.1);
}
h1 {
    margin: 0;
    color: var(--primary-color);
    font-size: 2rem;
    font-weight: 600;
}
.controls {
    display: flex;
    gap: 15px;
    align-items: center;
    flex-wrap: wrap;
}
.search-container {
    position: relative;
    min-width: 300px;
}
#searchInput {
    width: 100%;
    padding: 12px 40px 12px 16px;
    border: 2px solid var(--border-color);
    border-radius: 8px;
    font-size: 16px;
    background-color: var(--background-color);
    color: var(--text-color);
    transition: border-color 0.2s, box-shadow 0.2s;
}
#searchInput:focus {
    outline: none;
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px rgba(0, 120, 212, 0.1);
}
.search-icon {
    position: absolute;
    right: 12px;
    top: 50%;
    transform: translateY(-50%);
    color: var(--text-secondary);
}
#themeSelector {
    padding: 10px 16px;
    border: 2px solid var(--border-color);
    border-radius: 8px;
    background-color: var(--background-color);
    color: var(--text-color);
    font-size: 14px;
    cursor: pointer;
}
.stats {
    color: var(--text-secondary);
    font-size: 14px;
}
.bookmarks-container {
    background: var(--surface-color);
    border-radius: 12px;
    padding: 20px;
    box-shadow: var(--shadow);
    margin-bottom: 20px;
}
.folder {
    font-weight: 600;
    color: var(--primary-color);
    margin: 20px 0 10px 0;
    padding: 10px;
    background: linear-gradient(90deg, var(--primary-color)10, transparent);
    border-radius: 6px;
    border-left: 4px solid var(--primary-color);
    font-size: 1.1em;
}
.bookmark {
    margin: 8px 0;
    padding: 12px;
    background: var(--background-color);
    border: 1px solid var(--border-color);
    border-radius: 8px;
    transition: all 0.2s;
    display: flex;
    align-items: center;
    gap: 12px;
    cursor: pointer;
}
.bookmark:hover {
    background: var(--hover-color);
    border-color: var(--primary-color);
    transform: translateY(-1px);
    box-shadow: var(--shadow);
}
.bookmark-icon {
    width: 16px;
    height: 16px;
    background: var(--primary-color);
    border-radius: 3px;
    flex-shrink: 0;
}
.url {
    color: var(--text-color);
    text-decoration: none;
    font-weight: 500;
    flex: 1;
    word-break: break-word;
}
.bookmark-url {
    font-size: 12px;
    color: var(--text-secondary);
    margin-top: 4px;
    word-break: break-all;
}
.no-results {
    text-align: center;
    padding: 40px;
    color: var(--text-secondary);
    font-style: italic;
}";

        private string GetClientScript() => @"
let allBookmarks = [];
let filteredBookmarks = [];

fetch('/api/bookmarks')
    .then(r => r.json())
    .then(data => {
        allBookmarks = flattenBookmarks(data.roots.bookmark_bar.children);
        filteredBookmarks = [...allBookmarks];
        updateDisplay();
        document.getElementById('searchInput').focus();
    })
    .catch(error => {
        document.getElementById('bookmarks').innerHTML = '<div class=""no-results"">Error loading bookmarks: ' + error.message + '</div>';
    });

function flattenBookmarks(items, path = '') {
    let result = [];
    items.forEach(item => {
        if (item.type === 'folder') {
            const folderPath = path ? `${path} > ${item.name}` : item.name;
            if (item.children) {
                result.push(...flattenBookmarks(item.children, folderPath));
            }
        } else if (item.url) {
            result.push({
                name: item.name,
                url: item.url,
                path: path
            });
        }
    });
    return result;
}

function updateDisplay() {
    const container = document.getElementById('bookmarks');
    const stats = document.getElementById('stats');
    stats.textContent = `Showing ${filteredBookmarks.length} of ${allBookmarks.length} bookmarks`;

    if (filteredBookmarks.length === 0) {
        container.innerHTML = '<div class=""no-results"">No bookmarks found matching your search.</div>';
        return;
    }

    const grouped = {};
    filteredBookmarks.forEach(bookmark => {
        const folder = bookmark.path || 'Bookmarks Bar';
        if (!grouped[folder]) grouped[folder] = [];
        grouped[folder].push(bookmark);
    });

    let html = '';
    Object.keys(grouped).sort().forEach(folder => {
        html += `<div class=""folder"">📁 ${escapeHtml(folder)}</div>`;
        grouped[folder].forEach(bookmark => {
            const domain = extractDomain(bookmark.url);
            html += `
                <div class=""bookmark"" onclick=""window.open('${escapeHtml(bookmark.url)}', '_blank')"">
                    <div class=""bookmark-icon""></div>
                    <div style=""flex: 1;"">
                        <div class=""url"">${escapeHtml(bookmark.name)}</div>
                        <div class=""bookmark-url"">${escapeHtml(domain)}</div>
                    </div>
                </div>
            `;
        });
    });
    container.innerHTML = html;
}

function extractDomain(url) {
    try {
        return new URL(url).hostname;
    } catch {
        return url;
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

document.getElementById('searchInput').addEventListener('input', function(e) {
    const query = e.target.value.toLowerCase().trim();
    if (!query) {
        filteredBookmarks = [...allBookmarks];
    } else {
        const words = query.split(/\s+/).filter(word => word.length > 0);
        filteredBookmarks = allBookmarks.filter(bookmark => {
            const searchText = bookmark.name.toLowerCase();
            return words.every(word => searchText.includes(word));
        });
    }
    updateDisplay();
});

document.getElementById('searchInput').addEventListener('keydown', function(e) {
    if (e.key === 'Enter' && filteredBookmarks.length === 1) {
        e.preventDefault();
        window.open(filteredBookmarks[0].url, '_blank');
    }
});

document.getElementById('themeSelector').addEventListener('change', function(e) {
    document.body.className = e.target.value;
    localStorage.setItem('selectedTheme', e.target.value);
});

const savedTheme = localStorage.getItem('selectedTheme');
if (savedTheme) {
    document.body.className = savedTheme;
    document.getElementById('themeSelector').value = savedTheme;
}";
    }

    #region Models

    public class BookmarksRoot
    {
        [JsonPropertyName("roots")]
        public BookmarkRoots? Roots { get; set; }
    }

    public class BookmarkRoots
    {
        [JsonPropertyName("bookmark_bar")]
        public BookmarkFolder? BookmarkBar { get; set; }

        [JsonPropertyName("other")]
        public BookmarkFolder? Other { get; set; }
    }

    public class BookmarkFolder
    {
        [JsonPropertyName("children")]
        public List<BookmarkItem>? Children { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class BookmarkItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("children")]
        public List<BookmarkItem>? Children { get; set; }
    }

    #endregion
}
