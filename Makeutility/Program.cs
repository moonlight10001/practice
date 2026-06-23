using System;
using System.IO;

namespace MakeUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: make.exe <target>");
                Environment.Exit(1);
            }

            string targetName = args[0];

            if (!File.Exists("makefile"))
            {
                Console.Error.WriteLine("Error: makefile not found");
                Environment.Exit(1);
            }

            string[] lines = File.ReadAllLines("makefile");

            var parser = new MakefileParser();
            var tasks = parser.Parse(lines);

            if (!tasks.ContainsKey(targetName))
            {
                Console.Error.WriteLine($"Error: target '{targetName}' not found");
                Environment.Exit(1);
            }

            var executor = new TaskExecutor(tasks);
            bool success = executor.Execute(targetName);

            if (!success)
            {
                Console.Error.WriteLine("Error: circular dependency detected");
                Environment.Exit(1);
            }
        }
    }
}
