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
                    string? requestJson = await NativePipe.ReadFramedMessageAsync(stdin, cancellationToken).ConfigureAwait(false);
                    if (requestJson == null)
                    {
                        _log?.Invoke("[NATIVE-HOST] Standard input closed. Exiting host loop.");
                        return 0;
                    }

                    _log?.Invoke($"[NATIVE-HOST] Received framed request. bytes={Encoding.UTF8.GetByteCount(requestJson)}");
                    string responseJson = await _messageProcessor.ProcessRequestAsync(requestJson).ConfigureAwait(false);
                    _log?.Invoke($"[NATIVE-HOST] Writing framed response. chars={responseJson.Length}");

                    await NativePipe.WriteFramedMessageAsync(stdout, responseJson, cancellationToken).ConfigureAwait(false);
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

    }
}
