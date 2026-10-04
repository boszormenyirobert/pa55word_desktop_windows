using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AuthStateMonitorService : IDisposable
    {
        private static readonly bool BackgroundPollingEnabled = true;
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

        private readonly AuthenticationService _authenticationService;
        private readonly ExtensionSessionStateStore _extensionSessionStateStore;
        private readonly Action? _stateEvaluated;
        private readonly Action<string>? _log;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _monitorTask;

        public AuthStateMonitorService(
            AuthenticationService authenticationService,
            ExtensionSessionStateStore extensionSessionStateStore,
            Action? stateEvaluated = null,
            Action<string>? log = null)
        {
            _authenticationService = authenticationService;
            _extensionSessionStateStore = extensionSessionStateStore;
            _stateEvaluated = stateEvaluated;
            _log = log;
        }

        public void Start()
        {
            if (!BackgroundPollingEnabled)
            {
                _log?.Invoke("[AUTH-MONITOR] Background polling is disabled.");
                return;
            }

            _monitorTask ??= Task.Run(() => RunAsync(_cancellationTokenSource.Token));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            _log?.Invoke($"[AUTH-MONITOR] Background polling started ({PollInterval}).");

            try
            {
                using var timer = new PeriodicTimer(PollInterval);

                do
                {
                    try
                    {
                        await EvaluateAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        _log?.Invoke($"[AUTH-MONITOR] Poll iteration failed: {ex}");
                    }
                }
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _log?.Invoke("[AUTH-MONITOR] Background polling stopped.");
        }

        private async Task EvaluateAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!_authenticationService.HasActiveRegistration)
            {
                _log?.Invoke("[AUTH-MONITOR] Skipping poll: no active registration.");
                return;
            }

            _log?.Invoke("[AUTH-MONITOR] Checking active profile device registration.");

            try
            {
                AuthorizationHealthResult result = await _authenticationService.CheckAuthorizationStateAsync().ConfigureAwait(false);

                if (result.Authorized)
                {
                    _extensionSessionStateStore.MarkAuthorized();
                    return;
                }

                if (result.ExplicitlyInvalid)
                {
                    _authenticationService.RevokeAuthorization();
                    _extensionSessionStateStore.MarkInvalid(result.Reason ?? "authorization_revoked");
                    _log?.Invoke($"[AUTH-MONITOR] Authorization invalidated: {result.Reason}");
                    return;
                }

                if (result.TransientFailure)
                {
                    _log?.Invoke($"[AUTH-MONITOR] Transient auth check failure: {result.Reason}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[AUTH-MONITOR] Unexpected monitor failure: {ex.Message}");
            }
            finally
            {
                try
                {
                    _stateEvaluated?.Invoke();
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"[AUTH-MONITOR] State evaluated callback failed: {ex.Message}");
                }
            }
        }
    }
}