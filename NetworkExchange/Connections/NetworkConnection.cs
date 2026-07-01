using System.Collections.Concurrent;
using NetworkCommunication.Commands;
using NetworkCommunication.Handlers;
using NetworkCommunication.Transport;

namespace NetworkCommunication.Connections
{
    public class NetworkConnection : IConnection, IDisposable
    {
        private readonly ITransport _transport;
        private readonly IProtocolSerializer _serializer;
        private readonly ConcurrentDictionary<Type, object> _handlers;
        private readonly SemaphoreSlim _sendLock;
        private bool _isBlocked;

        public NetworkConnection(ITransport transport, IProtocolSerializer serializer)
        {
            _transport = transport;
            _serializer = serializer;
            _handlers = new ConcurrentDictionary<Type, object>();
            _sendLock = new SemaphoreSlim(1, 1);
            _isBlocked = false;

            _transport.Received += OnDataReceived;
        }

        private void OnDataReceived(object? sender, byte[] data)
        {
            try
            {
                var command = _serializer.Deserialize(data);
                var commandType = command.GetType();

                if (_handlers.TryGetValue(commandType, out var handler))
                {
                    var handlerType = typeof(IHandler<>).MakeGenericType(commandType);
                    var method = handlerType.GetMethod("Received");

                    if (method != null)
                    {
                        Task.Run(() => method.Invoke(handler, new object[] { command }));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Message processing error: {ex.Message}");
            }
        }

        public void RegisterHandler<T>(IHandler<T> handler) where T : ICommand
        {
            _handlers[typeof(T)] = handler;
        }

        public void Send(ICommand command)
        {
            _sendLock.Wait();

            try
            {
                if (_isBlocked)
                    throw new InvalidOperationException("Sending is blocked until response is received");

                byte[] data = _serializer.Serialize(command);
                _transport.Send(data);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task SendAsync(ICommand command)
        {
            await _sendLock.WaitAsync();

            try
            {
                if (_isBlocked)
                    throw new InvalidOperationException("Sending is blocked until response is received");

                byte[] data = _serializer.Serialize(command);
                await Task.Run(() => _transport.Send(data));
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void BlockSending()
        {
            _isBlocked = true;
        }

        public void UnblockSending()
        {
            _isBlocked = false;
        }

        public void Dispose()
        {
            _sendLock?.Dispose();
            if (_transport is IDisposable disposableTransport)
                disposableTransport.Dispose();
        }
    }
}