namespace NetworkCommunication.Commands
{
    public class RequestResponseCommand : ICommand
    {
        public string RequestData { get; set; }
        public Guid CorrelationId { get; set; }

        public RequestResponseCommand(string requestData)
        {
            RequestData = requestData;
            CorrelationId = Guid.NewGuid();
        }
    }
}