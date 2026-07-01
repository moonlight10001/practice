using NetworkCommunication.Commands;

namespace NetworkCommunication.Connections
{
    public interface IProtocolSerializer
    {
        byte[] Serialize(ICommand command);
        ICommand Deserialize(byte[] data);
    }
}