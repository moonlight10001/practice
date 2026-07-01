using System.Net;
using System.Net.Sockets;

namespace NetworkCommunication.Transport
{
	public class UdpTransport : ITransport, IDisposable
	{
		private readonly UdpClient _udpClient;
		private readonly IPEndPoint _remoteEndPoint;
		private bool _isRunning;

		public event EventHandler<byte[]>? Received;

		public UdpTransport(int listenPort, string remoteHost, int remotePort)
		{
			_udpClient = new UdpClient(listenPort);
			_remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
			_isRunning = true;
			_ = Task.Run(ReceiveLoop);
		}

		private async Task ReceiveLoop()
		{
			while (_isRunning)
			{
				try
				{
					var result = await _udpClient.ReceiveAsync();
					Received?.Invoke(this, result.Buffer);
				}
				catch { break; }
			}
		}

		public void Send(byte[] data)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			try
			{
				_udpClient.SendAsync(data, data.Length, _remoteEndPoint).Wait(cts.Token);
			}
			catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
			{
				throw new Exceptions.SendTimeoutException("Send timeout");
			}
		}

		public void Dispose()
		{
			_isRunning = false;
			_udpClient?.Dispose();
		}
	}
}