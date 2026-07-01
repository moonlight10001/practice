using NetworkCommunication.Commands;
using NetworkCommunication.Connections;
using NetworkCommunication.Handlers;
using NetworkCommunication.Transport;

namespace NetworkCommunication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Network Communication Demo");
            var transport = new TcpTransport(8888);
            var serializer = new JsonProtocolSerializer();
            using var connection = new NetworkConnection(transport, serializer);

            var echoHandler = new EchoHandler(connection);
            connection.RegisterHandler<EchoCommand>(echoHandler);

            var requestHandler = new RequestResponseHandler(connection);
            connection.RegisterHandler<RequestResponseCommand>(requestHandler);

            Console.WriteLine("Sending echo message...");
            connection.Send(new EchoCommand("Hello, World!"));

            await Task.Delay(500);

            Console.WriteLine("\nSending request...");
            connection.BlockSending();

            try
            {
                var response = await requestHandler.SendRequestAsync("Test request");
                Console.WriteLine($"Response received: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            connection.UnblockSending();

            Console.WriteLine("\nParallel message sending...");
            var tasks = new List<Task>();
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await connection.SendAsync(new EchoCommand($"Message {index}"));
                    Console.WriteLine($"Message {index} sent");
                }));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine("\nDemo completed.");
            Console.ReadLine();
        }
    }
}