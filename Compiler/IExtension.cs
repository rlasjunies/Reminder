
namespace Reminder.Compiler
{
    public class ExtensionMenuItem
    {
        public string Text { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public Action? OnClick { get; set; }
        public bool IsSeparator { get; set; } = false;

        public static ExtensionMenuItem Separator() => new ExtensionMenuItem { IsSeparator = true };
    }

    public interface IExtension
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        void Execute(ILogger logger);
        void Prepare(ILogger logger);
        
        /// <summary>
        /// Optional: Return menu items to be added to the systray context menu
        /// </summary>
        IEnumerable<ExtensionMenuItem>? GetMenuItems() => null;
    }
}
