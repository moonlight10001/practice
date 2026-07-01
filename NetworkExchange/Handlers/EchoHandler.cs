using NetworkCommunication.Commands;
using NetworkCommunication.Connections;

namespace NetworkCommunication.Handlers
{
    public class EchoHandler : IHandler<EchoCommand>
    {
        private readonly IConnection _connection;

        public EchoHandler(IConnection connection)
        {
            _connection = connection;
        }

        public void Received(EchoCommand command)
        {
            Console.WriteLine($"Echo message received: {command.Message}");

            var responseCommand = new EchoCommand(command.Message)
            {
                CorrelationId = command.CorrelationId
            };

            Task.Run(() => _connection.Send(responseCommand));
        }
    }
}