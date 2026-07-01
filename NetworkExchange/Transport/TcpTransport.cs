using System.Net;
using System.Net.Sockets;

namespace NetworkCommunication.Transport
{
    public class TcpTransport : ITransport, IDisposable
    {
        private readonly TcpListener _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isRunning;

        public event EventHandler<byte[]>? Received;

        public TcpTransport(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _isRunning = true;
            _ = Task.Run(AcceptClient);
        }

        private async Task AcceptClient()
        {
            try
            {
                _client = await _listener.AcceptTcpClientAsync();
                _stream = _client.GetStream();
                _ = Task.Run(ReceiveLoop);
            }
            catch { }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            while (_isRunning && _stream != null)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        byte[] data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        Received?.Invoke(this, data);
                    }
                }
                catch { break; }
            }
        }

        public void Send(byte[] data)
        {
            if (_stream == null || _client == null || !_client.Connected)
                throw new InvalidOperationException("No active connection");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                _stream.WriteAsync(data, 0, data.Length, cts.Token).Wait(cts.Token);
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                throw new Exceptions.SendTimeoutException("Send timeout");
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _stream?.Dispose();
            _client?.Dispose();
            _listener?.Stop();
        }
    }
}