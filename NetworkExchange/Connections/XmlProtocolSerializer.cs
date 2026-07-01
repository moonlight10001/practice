using System.Xml;
using System.Xml.Serialization;
using NetworkCommunication.Commands;

namespace NetworkCommunication.Connections
{
    public class XmlProtocolSerializer : IProtocolSerializer
    {
        public byte[] Serialize(ICommand command)
        {
            var serializer = new XmlSerializer(command.GetType());
            using var ms = new MemoryStream();
            serializer.Serialize(ms, command);
            return ms.ToArray();
        }

        public ICommand Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(ms);

            string typeName = xmlDoc.DocumentElement?.Name ?? string.Empty;
            var commandType = typeof(EchoCommand).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName && typeof(ICommand).IsAssignableFrom(t));

            if (commandType == null)
                throw new InvalidOperationException($"Unknown command type: {typeName}");

            ms.Position = 0;
            var serializer = new XmlSerializer(commandType);
            var command = serializer.Deserialize(ms) as ICommand;

            if (command == null)
                throw new InvalidOperationException("Failed to deserialize command");

            return command;
        }
    }
}