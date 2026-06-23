using System;
using System.Collections.Generic;

namespace MakeUtility
{
    class MakefileParser
    {
        public Dictionary<string, Task> Parse(string[] lines)
        {
            var tasks = new Dictionary<string, Task>(StringComparer.Ordinal);
            Task currentTask = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                bool isAction = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');

                if (isAction)
                {
                    if (currentTask == null)
                    {
                        Console.Error.WriteLine($"Error: action without target at line {i + 1}");
                        Environment.Exit(1);
                    }
                    currentTask.Actions.Add(line.Trim());
                }
                else
                {
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex < 0)
                    {
                        Console.Error.WriteLine($"Error: invalid line format at line {i + 1}: '{line}'");
                        Environment.Exit(1);
                    }

                    string taskName = line.Substring(0, colonIndex).Trim();
                    string depsString = line.Substring(colonIndex + 1).Trim();

                    if (string.IsNullOrEmpty(taskName))
                    {
                        Console.Error.WriteLine($"Error: empty target name at line {i + 1}");
                        Environment.Exit(1);
                    }

                    if (tasks.ContainsKey(taskName))
                    {
                        Console.Error.WriteLine($"Error: duplicate target '{taskName}' at line {i + 1}");
                        Environment.Exit(1);
                    }

                    currentTask = new Task { Name = taskName };

                    if (!string.IsNullOrWhiteSpace(depsString))
                    {
                        var deps = depsString.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var dep in deps)
                            currentTask.Dependencies.Add(dep);
                    }

                    tasks[taskName] = currentTask;
                }
            }

            foreach (var task in tasks.Values)
            {
                foreach (var dep in task.Dependencies)
                {
                    if (!tasks.ContainsKey(dep))
                    {
                        Console.Error.WriteLine($"Error: unknown dependency '{dep}' in target '{task.Name}'");
                        Environment.Exit(1);
                    }
                }
            }

            return tasks;
        }
    }
}
