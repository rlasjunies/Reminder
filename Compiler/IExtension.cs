
namespace Reminder.Compiler
{
    public interface IExtension
    {
        void Execute(ILogger logger);
        void Prepare(ILogger logger);
    }
}
