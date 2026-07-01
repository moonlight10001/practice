namespace NetworkCommunication.Exceptions
{
    public class ReceiveTimeoutException : Exception
    {
        public ReceiveTimeoutException(string message) : base(message)
        {
        }
    }
}