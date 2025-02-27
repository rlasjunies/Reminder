using Microsoft.Win32;

namespace Reminder;

public class SystrayApplication : Form
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private IHost host;
    private bool serviceRunning = false;
    private ToolStripMenuItem serviceStatusMenuItem;

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
        
        // Shows the files
        trayMenu.Items.Add("Show Config File", null, OnShowConfig);
        trayMenu.Items.Add("Show readme File", null, OnShowReadme);

        // Add service control menu items
        serviceStatusMenuItem = new ToolStripMenuItem("Service Status: Stopped");
        serviceStatusMenuItem.Enabled = false;
        //trayMenu.Items.Add(serviceStatusMenuItem);
        //trayMenu.Items.Add("Start Service", null, OnStartService);
        //trayMenu.Items.Add("Stop Service", null, OnStopService);

        trayMenu.Items.Add("-"); // Separator
        trayMenu.Items.Add("Exit", null, OnExit);

        // Create tray icon
        trayIcon = new NotifyIcon();
        trayIcon.Text = "Reminder App - to remind you best practices";
        //trayIcon.Icon = SystemIcons.Application;
        trayIcon.Icon = new Icon(Path.Combine(AppContext.BaseDirectory,"Assets", "reminder3.ico"));
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;

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
                services.AddSingleton<ReminderService>();
                services.AddHostedService<ReminderWindowsService>();
            })
            .Build();
        //host.RunAsync();
        OnStartService(this, EventArgs.Empty);
    }

    private async void OnStartService(object sender, EventArgs e)
    {
        if (!serviceRunning)
        {
            await host.StartAsync();
            serviceRunning = true;
            UpdateServiceStatus();
            trayIcon.ShowBalloonTip(3000, "Service Started", "The background service is now running", ToolTipIcon.Info);
        }
    }

    private async void OnStopService(object sender, EventArgs e)
    {
        if (serviceRunning)
        {
            await host.StopAsync();
            serviceRunning = false;
            UpdateServiceStatus();
            trayIcon.ShowBalloonTip(3000, "Service Stopped", "The background service has been stopped", ToolTipIcon.Info);
        }
    }

    private void UpdateServiceStatus()
    {
        serviceStatusMenuItem.Text = serviceRunning ?
            "Service Status: Running" :
            "Service Status: Stopped";
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

    private void OnShowConfig(object sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReminderService.GetConfigFilePath(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening config file: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnShowReadme(object sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "readme.md"),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening readme file: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExit(object sender, EventArgs e)
    {
        Application.Exit();
    }

    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartWithWindows(bool enable)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
        {
            if (enable)
            {
                key.SetValue("MySystrayApp", Application.ExecutablePath);
            }
            else
            {
                key.DeleteValue("MySystrayApp", false);
            }
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