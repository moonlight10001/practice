namespace MakeUtility
{
    public interface ITaskRunner
    {
        void RunTask(Task task);
    }

    public class LoggingTaskRunner : ITaskRunner
    {
        private readonly ILogger _logger;

        public LoggingTaskRunner(ILogger logger)
        {
            _logger = logger;
        }

        public void RunTask(Task task)
        {
            _logger.Log($"Executing task: {task.Name}");
            foreach (var action in task.Actions)
                _logger.Log($"  Action: {action}");
        }
    }
}