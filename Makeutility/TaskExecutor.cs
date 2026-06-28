using System.Collections.Generic;

namespace MakeUtility
{
    class TaskExecutor
    {
        private readonly Dictionary<string, Task> _tasks;
        private readonly ILogger _logger;
        private readonly ITaskRunner _runner;

        public TaskExecutor(Dictionary<string, Task> tasks, ILogger logger, ITaskRunner runner)
        {
            _tasks = tasks;
            _logger = logger;
            _runner = runner;
        }

        public bool TryExecute(string targetName)
        {
            var visited = new HashSet<string>();
            var inProgress = new HashSet<string>();

            return Visit(targetName, visited, inProgress);
        }

        private bool Visit(string name, HashSet<string> visited, HashSet<string> inProgress)
        {
            var stack = new Stack<(string name, bool expanding)>();
            stack.Push((name, false));

            while (stack.Count > 0)
            {
                var (current, expanding) = stack.Pop();

                if (expanding)
                {
                    inProgress.Remove(current);
                    visited.Add(current);

                    var task = _tasks[current];
                    _runner.RunTask(task);
                    continue;
                }

                if (visited.Contains(current))
                    continue;

                if (inProgress.Contains(current))
                {
                    _logger.Log($"Error: circular dependency detected at target '{current}'");
                    return false;
                }

                inProgress.Add(current);
                stack.Push((current, true));

                var deps = _tasks[current].Dependencies;
                for (int i = deps.Count - 1; i >= 0; i--)
                    stack.Push((deps[i], false));
            }

            return true;
        }
    }
}