using System.Text;

namespace TrayApp.Services
{
    internal sealed class NativeMessagingHostRunner
    {
        private readonly INativeMessageProcessor _messageProcessor;
        private readonly Action<string>? _log;

        public NativeMessagingHostRunner(INativeMessageProcessor messageProcessor, Action<string>? log = null)
        {
            _messageProcessor = messageProcessor;
            _log = log;
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using Stream stdin = Console.OpenStandardInput();
                using Stream stdout = Console.OpenStandardOutput();

                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[]? requestBytes = await ReadFramedMessageAsync(stdin, cancellationToken).ConfigureAwait(false);
                    if (requestBytes == null)
                    {
                        _log?.Invoke("[NATIVE-HOST] Standard input closed. Exiting host loop.");
                        return 0;
                    }

                    string requestJson = Encoding.UTF8.GetString(requestBytes);
                    _log?.Invoke($"[NATIVE-HOST] Received framed request. bytes={requestBytes.Length}");
                    string responseJson = await _messageProcessor.ProcessRequestAsync(requestJson).ConfigureAwait(false);
                    _log?.Invoke($"[NATIVE-HOST] Writing framed response. chars={responseJson.Length}");

                    await WriteFramedMessageAsync(stdout, responseJson, cancellationToken).ConfigureAwait(false);
                    await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                return 0;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[NATIVE-HOST] Fatal error: {ex.Message}");
                DiagnosticLog.Write($"[NATIVE-HOST] Fatal exception: {ex}");
                return 1;
            }
        }

        private static async Task<byte[]?> ReadFramedMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] lengthBuffer = new byte[4];
            int lengthBytesRead = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);

            if (lengthBytesRead == 0)
            {
                return null;
            }

            if (lengthBytesRead != 4)
            {
                throw new InvalidDataException("Invalid native message header length.");
            }

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength <= 0)
            {
                throw new InvalidDataException("Invalid native message payload length.");
            }

            byte[] payload = new byte[payloadLength];
            int payloadBytesRead = await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);

            if (payloadBytesRead != payloadLength)
            {
                throw new EndOfStreamException("Native message payload ended unexpectedly.");
            }

            return payload;
        }

        private static async Task WriteFramedMessageAsync(Stream stream, string json, CancellationToken cancellationToken)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] lengthBuffer = BitConverter.GetBytes(payload.Length);

            await stream.WriteAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
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
