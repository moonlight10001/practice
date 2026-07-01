using NetworkCommunication.Commands;

namespace NetworkCommunication.Handlers
{
    public interface IHandler<TCommand> where TCommand : ICommand
    {
        void Received(TCommand command);
    }
}