using System.IO.Pipes;

namespace TrayApp.Services
{
    internal sealed class NativePipeMessageServer : IDisposable
    {
        private readonly INativeMessageProcessor _messageProcessor;
        private readonly Action<string>? _log;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _serverTask;

        public NativePipeMessageServer(INativeMessageProcessor messageProcessor, Action<string>? log = null)
        {
            _messageProcessor = messageProcessor;
            _log = log;
        }

        public void Start()
        {
            _serverTask ??= Task.Run(() => RunAsync(_cancellationTokenSource.Token));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        NativePipe.PipeName,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    string? requestJson = await NativePipe.ReadFramedMessageAsync(server, cancellationToken).ConfigureAwait(false);
                    if (requestJson == null)
                    {
                        _log?.Invoke("[PIPE-SERVER] Received empty pipe request.");
                        continue;
                    }

                    _log?.Invoke($"[PIPE-SERVER] Received request from native host. chars={requestJson.Length}");

                    string responseJson = await _messageProcessor.ProcessRequestAsync(requestJson).ConfigureAwait(false);
                    _log?.Invoke($"[PIPE-SERVER] Sending response to native host. chars={responseJson.Length}");
                    await NativePipe.WriteFramedMessageAsync(server, responseJson, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"[PIPE-SERVER] {ex.Message}");
                }
            }
        }
    }
}
