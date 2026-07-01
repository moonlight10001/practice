using System.Collections.Concurrent;
using NetworkCommunication.Commands;
using NetworkCommunication.Connections;

namespace NetworkCommunication.Handlers
{
	public class RequestResponseHandler : IHandler<RequestResponseCommand>
	{
		private readonly IConnection _connection;
		private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _pendingRequests;

		public RequestResponseHandler(IConnection connection)
		{
			_connection = connection;
			_pendingRequests = new ConcurrentDictionary<Guid, TaskCompletionSource<string>>();
		}

		public void Received(RequestResponseCommand command)
		{
			Console.WriteLine($"Request received: {command.RequestData}");

			if (_pendingRequests.TryRemove(command.CorrelationId, out var tcs))
			{
				tcs.SetResult(command.RequestData);
			}
			else
			{
				var responseCommand = new RequestResponseCommand($"Response to: {command.RequestData}")
				{
					CorrelationId = command.CorrelationId
				};

				Task.Run(() => _connection.Send(responseCommand));
			}
		}

		public async Task<string> SendRequestAsync(string requestData)
		{
			var command = new RequestResponseCommand(requestData);
			var tcs = new TaskCompletionSource<string>();
			_pendingRequests.TryAdd(command.CorrelationId, tcs);

			_connection.Send(command);

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try
			{
				return await tcs.Task.WaitAsync(cts.Token);
			}
			catch (TimeoutException)
			{
				_pendingRequests.TryRemove(command.CorrelationId, out _);
				throw new Exceptions.ReceiveTimeoutException("Response timeout");
			}
		}
	}
}