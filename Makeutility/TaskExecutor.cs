using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MakeUtility
{
    class TaskExecutor
    {
        private readonly Dictionary<string, Task> _tasks;

        public TaskExecutor(Dictionary<string, Task> tasks)
        {
            _tasks = tasks;
        }

        public bool Execute(string targetName)
        {
            var ordered = new List<Task>();
            var visited = new HashSet<string>();
            var inProgress = new HashSet<string>();

            bool ok = TopoSort(targetName, visited, inProgress, ordered);
            if (!ok) return false;

            foreach (var task in ordered)
            {
                Console.WriteLine(task.Name);
                foreach (var action in task.Actions)
                {
                    Console.WriteLine($" {action}");
                    RunAction(action);
                }
            }

            return true;
        }

        private bool TopoSort(string name, HashSet<string> visited, HashSet<string> inProgress, List<Task> ordered)
        {
            if (visited.Contains(name))
                return true;

            if (inProgress.Contains(name))
                return false;

            inProgress.Add(name);

            var task = _tasks[name];
            foreach (var dep in task.Dependencies)
            {
                bool ok = TopoSort(dep, visited, inProgress, ordered);
                if (!ok) return false;
            }

            inProgress.Remove(name);
            visited.Add(name);
            ordered.Add(task);

            return true;
        }

        private void RunAction(string action)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {action}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Console.Write(output);
                if (!string.IsNullOrEmpty(error))
                    Console.Error.Write(error);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error running action '{action}': {ex.Message}");
            }
        }
    }
}
