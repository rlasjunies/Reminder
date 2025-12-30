namespace Reminder.Reminder;

// Class to store the configuration of the link to open
public class WebPageConfig
{
    public string Url { get; set; } = string.Empty;
    public string Frequency { get; set; } = "daily";
    public DateTime? NextOpening { get; set; }
}

// Class to store the configuration of the service
public class ServiceConfig
{
    public List<WebPageConfig> Pages { get; set; } = [];
}
