using Microsoft.Win32;
using Reminder.Compiler;

namespace Reminder;

public class SystrayApplication : Form
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private IHost host;
    private bool serviceRunning = false;

    public SystrayApplication()
    {
        InitializeComponent();
        InitializeBackgroundService();
        SetStartWithWindows(true);
    }

    private void InitializeComponent()
    {
        // Create context menu
        trayMenu = new ContextMenuStrip();

        // Menu items will be added dynamically from extensions
        // Bottom items: Exit
        trayMenu.Items.Add("Exit", null, OnExit);

        // Extensions will be added dynamically when menu opens
        trayMenu.Opening += TrayMenu_Opening;

        // Create tray icon
        trayIcon = new NotifyIcon
        {
            Text = "Reminder App - to remind you best practices",
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "reminder3.ico")),
            ContextMenuStrip = trayMenu,
            Visible = true
        };

        // Configure the form
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Size = new Size(1, 1);
        this.Load += (s, e) => this.Hide();
    }

    private void InitializeBackgroundService()
    {
        // Configure the host
        host = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                // Only register the compiler service - extensions provide functionality
                services.AddHostedService<CompilerBackgroundService>();
            })
            .Build();
        OnStartService(this, EventArgs.Empty);
    }

    private async void OnStartService(object sender, EventArgs e)
    {
        if (!serviceRunning)
        {
            await host.StartAsync();
            serviceRunning = true;
            trayIcon.ShowBalloonTip(3000, "Service Started", "The background service is now running", ToolTipIcon.Info);
        }
    }

    private async void OnStopService(object sender, EventArgs e)
    {
        if (serviceRunning)
        {
            await host.StopAsync();
            serviceRunning = false;
            trayIcon.ShowBalloonTip(3000, "Service Stopped", "The background service has been stopped", ToolTipIcon.Info);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Clean up resources before closing
        if (trayIcon != null)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        // Ensure the service is stopped
        if (serviceRunning)
        {
            host.StopAsync().GetAwaiter().GetResult();
        }

        base.OnFormClosing(e);
    }

    private void OnExit(object sender, EventArgs e)
    {
        Application.Exit();
    }

    private void TrayMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Remove old dynamic items
        var itemsToRemove = trayMenu.Items.Cast<ToolStripItem>()
            .Where(item => item.Tag?.ToString() == "Dynamic")
            .ToList();

        foreach (var item in itemsToRemove)
        {
            trayMenu.Items.Remove(item);
        }

        var extensions = CompilerBackgroundService.Extensions;

        // Build the menu structure from top to bottom:
        // 1. Extension menu items
        // 2. Separator
        // 3. Loaded Extensions submenu
        // 4. Exit (already there)

        int insertIndex = 0;

        // Add extension menu items at the top
        bool hasExtensionMenuItems = false;
        foreach (var ext in extensions.Where(e => e.IsLoaded))
        {
            var menuItems = ext.MenuItems;
            if (menuItems != null)
            {
                foreach (var menuItem in menuItems)
                {
                    hasExtensionMenuItems = true;
                    if (menuItem.IsSeparator)
                    {
                        var separator = new ToolStripSeparator { Tag = "Dynamic" };
                        trayMenu.Items.Insert(insertIndex++, separator);
                    }
                    else
                    {
                        var item = new ToolStripMenuItem(menuItem.Text)
                        {
                            Enabled = menuItem.Enabled,
                            Tag = "Dynamic"
                        };
                        if (menuItem.OnClick != null)
                        {
                            item.Click += (s, args) => menuItem.OnClick();
                        }
                        trayMenu.Items.Insert(insertIndex++, item);
                    }
                }
            }
        }

        // Add separator before Loaded Extensions if we have extension menu items
        if (hasExtensionMenuItems || extensions.Count > 0)
        {
            var separator = new ToolStripSeparator { Tag = "Dynamic" };
            trayMenu.Items.Insert(insertIndex++, separator);
        }

        // Add "Loaded Extensions" submenu
        if (extensions.Count > 0)
        {
            var extensionsMenu = new ToolStripMenuItem("Loaded Extensions") { Tag = "Dynamic" };

            foreach (var ext in extensions)
            {
                var extMenuItem = new ToolStripMenuItem(ext.Name);

                // Add submenu items
                if (ext.IsLoaded)
                {
                    extMenuItem.DropDownItems.Add(new ToolStripMenuItem($"✓ Status: Loaded") { Enabled = false });
                    extMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Version: {ext.Version}") { Enabled = false });
                    extMenuItem.DropDownItems.Add(new ToolStripSeparator());
                    extMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Description: {ext.Description}")
                    {
                        Enabled = false,
                        AutoSize = false,
                        Width = 400
                    });
                }
                else
                {
                    extMenuItem.ForeColor = Color.Red;
                    extMenuItem.DropDownItems.Add(new ToolStripMenuItem($"✗ Status: Compilation Failed")
                    {
                        Enabled = false,
                        ForeColor = Color.Red
                    });
                    extMenuItem.DropDownItems.Add(new ToolStripSeparator());

                    // Add "View Error" button
                    var viewErrorItem = new ToolStripMenuItem("View Error Details");
                    viewErrorItem.Click += (s, args) =>
                    {
                        var form = new Compiler.CompilationErrorForm(ext.FileName, ext.ErrorMessage ?? "Unknown error");
                        form.Show();
                    };
                    extMenuItem.DropDownItems.Add(viewErrorItem);
                }

                extensionsMenu.DropDownItems.Add(extMenuItem);
            }

            trayMenu.Items.Insert(insertIndex++, extensionsMenu);
        }
    }

    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartWithWindows(bool enable)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        if (enable)
        {
            key.SetValue("ReminderApp", Application.ExecutablePath);
        }
        else
        {
            key.DeleteValue("ReminderApp", false);
        }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SystrayApplication());
    }
}