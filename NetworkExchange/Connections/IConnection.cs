using NetworkCommunication.Commands;
using NetworkCommunication.Handlers;

namespace NetworkCommunication.Connections
{
    public interface IConnection
    {
        void RegisterHandler<T>(IHandler<T> handler) where T : ICommand;
        void Send(ICommand command);
    }
}