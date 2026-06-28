using System.IO;

namespace MakeUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new ConsoleLogger();

            if (args.Length != 1)
            {
                logger.Log("Usage: make.exe <target>");
                return;
            }

            string targetName = args[0];

            if (!File.Exists("makefile"))
            {
                logger.Log("Error: makefile not found");
                return;
            }

            string[] lines = File.ReadAllLines("makefile");

            var parser = new MakefileParser(logger);
            if (!parser.TryParse(lines, out var tasks))
                return;

            if (!tasks.ContainsKey(targetName))
            {
                logger.Log($"Error: target '{targetName}' not found");
                return;
            }

            var runner = new LoggingTaskRunner(logger);
            var executor = new TaskExecutor(tasks, logger, runner);

            executor.TryExecute(targetName);
        }
    }
}