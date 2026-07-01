namespace NetworkCommunication.Commands
{
    public class EchoCommand : ICommand
    {
        public string Message { get; set; }
        public Guid CorrelationId { get; set; }

        public EchoCommand(string message)
        {
            Message = message;
            CorrelationId = Guid.NewGuid();
        }
    }
}