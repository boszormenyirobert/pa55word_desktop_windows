using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AuthenticationService
    {
        private static string AuthenticationEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.Authentication);
        private static readonly TimeSpan AuthorizationDuration = TimeSpan.FromHours(3);
        private static readonly JsonSerializerOptions AuthenticationResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthorizationSessionStore _authorizationSessionStore;
        private readonly Action<string?>? _authorizationNotificationChanged;
        private readonly Action<string>? _log;
        private readonly SemaphoreSlim _authenticationLock = new(1, 1);

        public AuthenticationService(
            RegisteredDeviceService registeredDeviceService,
            HttpApiClient httpApiClient,
            AuthorizationSessionStore authorizationSessionStore,
            Action<string?>? authorizationNotificationChanged = null,
            Action<string>? log = null)
        {
            _registeredDeviceService = registeredDeviceService;
            _httpApiClient = httpApiClient;
            _authorizationSessionStore = authorizationSessionStore;
            _authorizationNotificationChanged = authorizationNotificationChanged;
            _log = log;
        }

        public async Task<bool> EnsureAuthorizedAsync()
        {
            if (_authorizationSessionStore.IsAuthorized)
            {
                _log?.Invoke("[AUTH] Reusing existing authorized session.");
                return true;
            }

            await _authenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_authorizationSessionStore.IsAuthorized)
                {
                    _log?.Invoke("[AUTH] Reusing existing authorized session after lock.");
                    return true;
                }

                bool authorized = await RequestAuthorizationAsync().ConfigureAwait(false);
                if (authorized)
                {
                    _authorizationSessionStore.AuthorizeFor(AuthorizationDuration);
                    _log?.Invoke("[AUTH] Authorization succeeded. Session marked as authorized.");
                }
                else
                {
                    _authorizationSessionStore.Revoke();
                    _log?.Invoke("[AUTH] Authorization failed. Session revoked.");
                }

                return authorized;
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        public void RevokeAuthorization()
        {
            _authorizationSessionStore.Revoke();
        }

        private async Task<bool> RequestAuthorizationAsync()
        {
            try
            {
                var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
                string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
                string activeDeviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);
                string activeNamespaceId = AppEnvironment.GetActiveNamespaceId() ?? "<null>";
                string authEndpoint = AppEnvironment.BuildApiUrl(ApiRoutes.Authentication);

                _log?.Invoke(
                    $"[AUTH] Request start. Endpoint='{authEndpoint}', activeNamespaceId='{activeNamespaceId}', accountId='{accountId}', deviceId='{activeDeviceId}'.");

                var authenticationPayload = new AuthenticationRequestDto
                {
                    AccountId = accountId,
                    DeviceId = activeDeviceId
                };

                IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                    activeDeviceId,
                    JsonSerializer.Serialize(authenticationPayload),
                    "authentication request");

                AuthenticationResponseDto? response = await _httpApiClient
                    .PostJsonAsync<AuthenticationRequestDto, AuthenticationResponseDto>(AuthenticationEndpoint, authenticationPayload, headers)
                    .ConfigureAwait(false);

                _log?.Invoke($"[AUTH] Initial response: {DescribeResponse(response)}");

                bool authorized = await ResolveAuthorizationResponseAsync(
                    response,
                    activeDeviceId,
                    accountId).ConfigureAwait(false);

                return authorized;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[AUTH] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                _authorizationNotificationChanged?.Invoke(null);
            }
        }

        private async Task<bool> ResolveAuthorizationResponseAsync(AuthenticationResponseDto? response, string deviceId, string accountId)
        {
            if (response == null)
            {
                _log?.Invoke("[AUTH] Response was null.");
                return false;
            }

            if (IsAuthorizationAccepted(response))
            {
                _log?.Invoke("[AUTH] Authorization accepted immediately.");
                return true;
            }

            if (!string.Equals(response.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                _log?.Invoke($"[AUTH] Authorization rejected with non-pending status. Response: {DescribeResponse(response)}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.NotificationId))
            {
                _log?.Invoke("[AUTH] Pending response without notificationId.");
                return false;
            }

            TimeSpan timeout = TimeSpan.FromSeconds(response.TimeoutSeconds.GetValueOrDefault(90));

            _authorizationNotificationChanged?.Invoke("Waiting for confirmation on your mobile device");
            _log?.Invoke($"[AUTH] Pending confirmation. notificationId='{response.NotificationId}', timeout={timeout.TotalSeconds:0}s.");

            return await WaitForAuthorizationStatusAsync(response.NotificationId, deviceId, accountId, timeout).ConfigureAwait(false);
        }

        private async Task<bool> WaitForAuthorizationStatusAsync(
            string notificationId,
            string deviceId,
            string accountId,
            TimeSpan timeout)
        {
            using var timeoutCancellation = new CancellationTokenSource(timeout);
            using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token);
            var authorizationResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var statusPayload = new
            {
                accountId,
                deviceId,
                notificationId
            };

            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                deviceId,
                JsonSerializer.Serialize(statusPayload),
                "authentication status request");

            string statusEndpoint = AppEnvironment.BuildApiUrl(ApiRoutes.AuthenticationStatusNotification(accountId, deviceId, notificationId));
            _log?.Invoke($"[AUTH] Listening for status updates. Endpoint='{statusEndpoint}'.");

            try
            {
                await _httpApiClient.ConnectToSseEndpoint(
                    statusEndpoint,
                    headers,
                    data => HandleAuthenticationStatusMessage(data, authorizationResult, streamCancellation),
                    streamCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (authorizationResult.Task.IsCompleted)
            {
                _log?.Invoke("[AUTH] Status stream completed after result was determined.");
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                _log?.Invoke("[AUTH] Status stream timed out.");
                return false;
            }

            return authorizationResult.Task.IsCompleted && await authorizationResult.Task.ConfigureAwait(false);
        }

        private void HandleAuthenticationStatusMessage(
            string data,
            TaskCompletionSource<bool> authorizationResult,
            CancellationTokenSource streamCancellation)
        {
            _log?.Invoke($"[AUTH] SSE message: {data}");

            AuthenticationResponseDto? statusResponse;

            try
            {
                statusResponse = JsonSerializer.Deserialize<AuthenticationResponseDto>(
                    data,
                    AuthenticationResponseJsonOptions);
            }
            catch (JsonException)
            {
                _log?.Invoke("[AUTH] Failed to parse SSE status response JSON.");
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
                return;
            }

            if (statusResponse == null)
            {
                _log?.Invoke("[AUTH] SSE status response was null.");
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
                return;
            }

            if (IsAuthorizationAccepted(statusResponse))
            {
                _log?.Invoke("[AUTH] SSE status accepted authorization.");
                authorizationResult.TrySetResult(true);
                streamCancellation.Cancel();
                return;
            }

            if (!string.Equals(statusResponse.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                _log?.Invoke($"[AUTH] SSE status rejected authorization. Response: {DescribeResponse(statusResponse)}");
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
            }
        }
        private static bool IsAuthorizationAccepted(AuthenticationResponseDto response)
        {
            return response.Authorized == true ||
                   response.Accepted == true ||
                   response.Authenticated == true ||
                   string.Equals(response.Status, "authorized", StringComparison.OrdinalIgnoreCase) ||
                   !string.IsNullOrWhiteSpace(response.Role);
        }

        private static string DescribeResponse(AuthenticationResponseDto? response)
        {
            if (response == null)
            {
                return "<null>";
            }

            return $"status='{response.Status}', accepted={response.Accepted}, authorized={response.Authorized}, authenticated={response.Authenticated}, declined={response.Declined}, notificationId='{response.NotificationId}', timeoutSeconds={response.TimeoutSeconds}, role='{response.Role}'";
        }

    }
}
