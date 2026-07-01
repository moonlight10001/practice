using System.Text;
using System.Text.Json;
using NetworkCommunication.Commands;

namespace NetworkCommunication.Connections
{
    public class JsonProtocolSerializer : IProtocolSerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonProtocolSerializer()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }

        public byte[] Serialize(ICommand command)
        {
            var wrapper = new CommandWrapper
            {
                CommandType = command.GetType().AssemblyQualifiedName!,
                CommandData = JsonSerializer.Serialize(command, command.GetType(), _options)
            };

            string json = JsonSerializer.Serialize(wrapper, _options);
            return Encoding.UTF8.GetBytes(json);
        }

        public ICommand Deserialize(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            var wrapper = JsonSerializer.Deserialize<CommandWrapper>(json, _options);

            if (wrapper == null || string.IsNullOrEmpty(wrapper.CommandType) || string.IsNullOrEmpty(wrapper.CommandData))
                throw new InvalidOperationException("Invalid data format");

            var commandType = Type.GetType(wrapper.CommandType);
            if (commandType == null)
                throw new InvalidOperationException($"Unknown command type: {wrapper.CommandType}");

            var command = JsonSerializer.Deserialize(wrapper.CommandData, commandType, _options) as ICommand;
            if (command == null)
                throw new InvalidOperationException("Failed to deserialize command");

            return command;
        }

        private class CommandWrapper
        {
            public string CommandType { get; set; } = string.Empty;
            public string CommandData { get; set; } = string.Empty;
        }
    }
}