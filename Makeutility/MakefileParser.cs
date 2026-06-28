using System;
using System.Collections.Generic;

namespace MakeUtility
{
    class MakefileParser
    {
        private readonly ILogger _logger;

        public MakefileParser(ILogger logger)
        {
            _logger = logger;
        }

        public bool TryParse(string[] lines, out Dictionary<string, Task> tasks)
        {
            tasks = new Dictionary<string, Task>(StringComparer.Ordinal);
            Task currentTask = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                bool isAction = line[0] == ' ' || line[0] == '\t';

                if (isAction)
                {
                    if (currentTask == null)
                    {
                        _logger.Log($"Error: action without target at line {i + 1}");
                        return false;
                    }
                    currentTask.Actions.Add(line.Trim());
                }
                else
                {
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex < 0)
                    {
                        _logger.Log($"Error: invalid line format at line {i + 1}: '{line}'");
                        return false;
                    }

                    string taskName = line.Substring(0, colonIndex).Trim();
                    string depsString = line.Substring(colonIndex + 1).Trim();

                    if (string.IsNullOrEmpty(taskName))
                    {
                        _logger.Log($"Error: empty target name at line {i + 1}");
                        return false;
                    }

                    if (tasks.ContainsKey(taskName))
                    {
                        _logger.Log($"Error: duplicate target '{taskName}' at line {i + 1}");
                        return false;
                    }

                    currentTask = new Task { Name = taskName };

                    if (!string.IsNullOrWhiteSpace(depsString))
                    {
                        var deps = depsString.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var dep in deps)
                            currentTask.Dependencies.Add(dep);
                    }

                    tasks[taskName] = currentTask;
                    _logger.Log($"Parsed target: {taskName}");
                }
            }

            foreach (var task in tasks.Values)
            {
                foreach (var dep in task.Dependencies)
                {
                    if (!tasks.ContainsKey(dep))
                    {
                        _logger.Log($"Error: unknown dependency '{dep}' in target '{task.Name}'");
                        return false;
                    }
                }
            }

            return true;
        }
    }
}