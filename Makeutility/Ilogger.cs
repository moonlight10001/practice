namespace MakeUtility
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            System.Console.WriteLine($"[LOG] {message}");
        }
    }
}