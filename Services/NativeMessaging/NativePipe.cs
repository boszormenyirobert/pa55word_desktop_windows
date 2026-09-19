using System.IO.Pipes;
using System.Text;

namespace TrayApp.Services
{
    internal static class NativePipe
    {
        public const string PipeName = "ZeroIntrusion.TrayApp.NativeMessaging";
        public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(250);

        public static async Task<string?> ReadFramedMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] lengthBuffer = new byte[4];
            int lengthBytesRead = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);

            if (lengthBytesRead == 0)
            {
                return null;
            }

            if (lengthBytesRead != 4)
            {
                throw new InvalidDataException("Invalid message header length.");
            }

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength <= 0)
            {
                throw new InvalidDataException("Invalid message payload length.");
            }

            byte[] payload = new byte[payloadLength];
            int payloadBytesRead = await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);

            if (payloadBytesRead != payloadLength)
            {
                throw new EndOfStreamException("Message payload ended unexpectedly.");
            }

            return Encoding.UTF8.GetString(payload);
        }

        public static async Task WriteFramedMessageAsync(Stream stream, string json, CancellationToken cancellationToken)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] lengthBuffer = BitConverter.GetBytes(payload.Length);

            await stream.WriteAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}
