using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;

namespace TrayApp.Services
{
    internal sealed class NativePipeMessageClient : INativeMessageProcessor
    {
        private readonly Action<string>? _log;
        private bool _trayAppStartRequested;

        public NativePipeMessageClient(Action<string>? log = null)
        {
            _log = log;
        }

        public async Task<string> ProcessRequestAsync(string requestJson)
        {
            try
            {
                _log?.Invoke($"[PIPE-CLIENT] Forwarding request to tray pipe. chars={requestJson.Length}");
                using NamedPipeClientStream client = await ConnectToTrayAppAsync().ConfigureAwait(false);
                await NativePipe.WriteFramedMessageAsync(client, requestJson, CancellationToken.None).ConfigureAwait(false);

                string? responseJson = await NativePipe.ReadFramedMessageAsync(client, CancellationToken.None).ConfigureAwait(false);
                if (responseJson == null)
                {
                    _log?.Invoke("[PIPE-CLIENT] Tray pipe closed without a response.");
                    return BrowserCommandHandler.CreateErrorResponseJson(
                        "tray_app_no_response",
                        "TrayApp closed the pipe connection without a response.");
                }

                _log?.Invoke($"[PIPE-CLIENT] Received response from tray pipe. chars={responseJson.Length}");

                return responseJson;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[PIPE-CLIENT] Forwarding failed: {ex}");
                return BrowserCommandHandler.CreateErrorResponseJson(
                    "tray_app_unavailable",
                    ex.Message);
            }
        }

        private async Task<NamedPipeClientStream> ConnectToTrayAppAsync()
        {
            EnsureTrayAppStartedIfNeeded();

            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(NativePipe.StartupTimeout);

            while (DateTimeOffset.UtcNow < deadline)
            {
                NamedPipeClientStream client = CreateClient();

                try
                {
                    using var connectCancellation = new CancellationTokenSource(NativePipe.ConnectRetryDelay);
                    await client.ConnectAsync(connectCancellation.Token).ConfigureAwait(false);
                    _log?.Invoke("[PIPE-CLIENT] Connected to running tray pipe instance.");
                    return client;
                }
                catch (OperationCanceledException)
                {
                    client.Dispose();
                    EnsureTrayAppStartedIfNeeded();
                }
                catch (IOException)
                {
                    client.Dispose();
                    EnsureTrayAppStartedIfNeeded();
                }
                catch (TimeoutException)
                {
                    client.Dispose();
                    EnsureTrayAppStartedIfNeeded();
                }

                await Task.Delay(NativePipe.ConnectRetryDelay).ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"TrayApp is not available. Could not connect to pipe '{NativePipe.PipeName}' within {NativePipe.StartupTimeout.TotalSeconds:0} seconds.");
        }

        private static NamedPipeClientStream CreateClient()
        {
            return new NamedPipeClientStream(
                ".",
                NativePipe.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
        }

        private void EnsureTrayAppStartedIfNeeded()
        {
            if (IsTrayAppRunning())
            {
                return;
            }

            StartTrayAppOnce();
        }

        private static bool IsTrayAppRunning()
        {
            try
            {
                using var singleInstanceMutex = Mutex.OpenExisting(Program.SingleInstanceMutexName);
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

        private void StartTrayAppOnce()
        {
            if (_trayAppStartRequested)
            {
                return;
            }

            _trayAppStartRequested = true;

            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                _log?.Invoke("[PIPE-CLIENT] Cannot start TrayApp because the executable path is unknown.");
                return;
            }

            try
            {
                string? workingDirectory = Path.GetDirectoryName(executablePath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppContext.BaseDirectory : workingDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[PIPE-CLIENT] Failed to start TrayApp: {ex.Message}");
            }
        }
    }
}
