namespace NetworkCommunication.Transport
{
    public interface ITransport
    {
        void Send(byte[] data);
        event EventHandler<byte[]> Received;
    }
}