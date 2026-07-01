namespace NetworkCommunication.Exceptions
{
    public class SendTimeoutException : Exception
    {
        public SendTimeoutException(string message) : base(message)
        {
        }
    }
}