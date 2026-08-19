using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AuthenticationService
    {
        private static readonly TimeSpan MaxAuthorizationWait = TimeSpan.FromMinutes(1);
        private static readonly JsonSerializerOptions AuthenticationResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthorizationSessionStore _authorizationSessionStore;
        private readonly Action<string?, ToolTipIcon>? _authorizationNotificationChanged;
        private readonly Action<string>? _log;
        private readonly SemaphoreSlim _authenticationLock = new(1, 1);

        public AuthenticationService(
            RegisteredDeviceService registeredDeviceService,
            HttpApiClient httpApiClient,
            AuthorizationSessionStore authorizationSessionStore,
            Action<string?, ToolTipIcon>? authorizationNotificationChanged = null,
            Action<string>? log = null)
        {
            _registeredDeviceService = registeredDeviceService;
            _httpApiClient = httpApiClient;
            _authorizationSessionStore = authorizationSessionStore;
            _authorizationNotificationChanged = authorizationNotificationChanged;
            _log = log;
        }

        public bool IsAuthorized => _authorizationSessionStore.IsAuthorized;

        public async Task<bool> HasValidCredentialForActiveNamespaceAsync()
        {
            AuthorizationContext loginContext = CreateAuthorizationContext();
            AuthorizationContext activeNamespaceContext = CreateActiveNamespaceAuthorizationContext();

            if (ContextsMatch(loginContext, activeNamespaceContext))
            {
                return true;
            }

            bool hasCredential = await CheckActiveAttestationAsync(activeNamespaceContext, persistSession: false).ConfigureAwait(false);
            if (!hasCredential)
            {
                _log?.Invoke(
                    $"[AUTH] Active namespace credential check failed. accountId='{activeNamespaceContext.AccountId}', deviceId='{activeNamespaceContext.DeviceId}'.");
                NotifyAuthorizationStatus("The selected namespace does not have an active backend credential for this user.", ToolTipIcon.Warning);
            }

            return hasCredential;
        }

        public async Task<bool> EnsureAuthorizedAsync(bool allowInteractiveLogin = true)
        {
            AuthorizationContext context = CreateAuthorizationContext();

            if (_authorizationSessionStore.Matches(context.UserId, context.AccountId, context.DeviceId))
            {
                bool stillActive = await CheckActiveAttestationAsync(context, persistSession: true).ConfigureAwait(false);
                if (stillActive)
                {
                    _log?.Invoke("[AUTH] Reusing active attestation.");
                    return true;
                }

                _authorizationSessionStore.Revoke();
                _log?.Invoke("[AUTH] Cached attestation was no longer active.");
                NotifyAuthorizationStatus("Desktop attestation expired. Log in again.", ToolTipIcon.Warning);

                if (!allowInteractiveLogin)
                {
                    return false;
                }
            }

            if (!allowInteractiveLogin)
            {
                return false;
            }

            await _authenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                context = CreateAuthorizationContext();

                if (_authorizationSessionStore.Matches(context.UserId, context.AccountId, context.DeviceId))
                {
                    bool stillActive = await CheckActiveAttestationAsync(context, persistSession: true).ConfigureAwait(false);
                    if (stillActive)
                    {
                        _log?.Invoke("[AUTH] Reusing active attestation after lock.");
                        return true;
                    }

                    _authorizationSessionStore.Revoke();
                }

                bool authorized = await RequestAuthorizationAsync(context).ConfigureAwait(false);
                if (!authorized)
                {
                    _authorizationSessionStore.Revoke();
                    _log?.Invoke("[AUTH] Authorization failed. Session revoked.");
                    NotifyAuthorizationStatus("Login failed. Mobile approval was declined or the session no longer exists.", ToolTipIcon.Error);
                    return false;
                }

                bool attestationActive = await CheckActiveAttestationAsync(context, persistSession: true).ConfigureAwait(false);
                if (attestationActive)
                {
                    _log?.Invoke("[AUTH] Authorization succeeded. Active attestation confirmed.");
                    return true;
                }

                _authorizationSessionStore.Revoke();
                _log?.Invoke("[AUTH] Authorization succeeded but no active attestation was found.");
                NotifyAuthorizationStatus("Login completed but no active desktop attestation was found.", ToolTipIcon.Error);
                return false;
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

        public async Task<bool> LogoutAsync()
        {
            bool remoteSuccess = true;

            try
            {
                AuthorizationContext context = CreateAuthorizationContext();
                remoteSuccess = await RevokeActiveAttestationAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                remoteSuccess = false;
                _log?.Invoke($"[AUTH] Logout revoke failed: {ex.Message}");
            }
            finally
            {
                _authorizationSessionStore.Revoke();
            }

            return remoteSuccess;
        }

        private async Task<bool> RequestAuthorizationAsync(AuthorizationContext context)
        {
            try
            {
                string activeNamespaceId = AppEnvironment.GetActiveNamespaceId() ?? "<null>";
                string authEndpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.Authentication);
                string nonce = NonceGenerator.Create();

                _log?.Invoke(
                    $"[AUTH] Request start. Endpoint='{authEndpoint}', activeNamespaceId='{activeNamespaceId}', accountId='{context.AccountId}', deviceId='{context.DeviceId}'.");

                var authenticationPayload = new AuthenticationRequestDto
                {
                    AccountId = context.AccountId,
                    DeviceId = context.DeviceId,
                    Nonce = nonce
                };

                string payloadJson = JsonSerializer.Serialize(authenticationPayload);

                IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                    context.DeviceId,
                    payloadJson,
                    "authentication request");

                AuthenticationResponseDto? response = await _httpApiClient
                    .PostJsonAsync<AuthenticationRequestDto, AuthenticationResponseDto>(authEndpoint, authenticationPayload, headers)
                    .ConfigureAwait(false);

                _log?.Invoke($"[AUTH] Initial response: {DescribeResponse(response)}");

                return await ResolveAuthorizationResponseAsync(response, context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[AUTH] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                _authorizationNotificationChanged?.Invoke(null, ToolTipIcon.None);
            }
        }

        private async Task<bool> ResolveAuthorizationResponseAsync(AuthenticationResponseDto? response, AuthorizationContext context)
        {
            if (response == null)
            {
                _log?.Invoke("[AUTH] Response was null.");
                NotifyAuthorizationStatus("Login failed: backend returned an empty response.", ToolTipIcon.Error);
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
                NotifyAuthorizationStatus(BuildAuthorizationFailureMessage(response), ToolTipIcon.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.NotificationId))
            {
                _log?.Invoke("[AUTH] Pending response without notificationId.");
                NotifyAuthorizationStatus("Login failed: backend did not return a notification id.", ToolTipIcon.Error);
                return false;
            }

            TimeSpan backendTimeout = TimeSpan.FromSeconds(response.TimeoutSeconds.GetValueOrDefault(90));
            TimeSpan timeout = backendTimeout <= TimeSpan.Zero
                ? MaxAuthorizationWait
                : backendTimeout < MaxAuthorizationWait
                    ? backendTimeout
                    : MaxAuthorizationWait;

            NotifyAuthorizationStatus("Waiting for confirmation on your mobile device", ToolTipIcon.Info);
            _log?.Invoke($"[AUTH] Pending confirmation. notificationId='{response.NotificationId}', timeout={timeout.TotalSeconds:0}s.");

            return await WaitForAuthorizationStatusAsync(response.NotificationId, context, timeout).ConfigureAwait(false);
        }

        private async Task<bool> WaitForAuthorizationStatusAsync(
            string notificationId,
            AuthorizationContext context,
            TimeSpan timeout)
        {
            using var timeoutCancellation = new CancellationTokenSource(timeout);
            using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token);
            var authorizationResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var statusPayload = new
            {
                accountId = context.AccountId,
                deviceId = context.DeviceId,
                notificationId,
                nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(statusPayload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DeviceId,
                payloadJson,
                "authentication status request");

            string statusEndpoint = AppEnvironment.BuildApiUrl(
                context.BaseUrl,
                ApiRoutes.AuthenticationStatusNotification(context.AccountId, context.DeviceId, notificationId, statusPayload.nonce));
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
                NotifyAuthorizationStatus("Login timed out while waiting for mobile approval.", ToolTipIcon.Warning);
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
                NotifyAuthorizationStatus("Login failed: invalid status response from backend.", ToolTipIcon.Error);
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
                return;
            }

            if (statusResponse == null)
            {
                _log?.Invoke("[AUTH] SSE status response was null.");
                NotifyAuthorizationStatus("Login failed: empty status response from backend.", ToolTipIcon.Error);
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
                NotifyAuthorizationStatus(BuildAuthorizationFailureMessage(statusResponse), ToolTipIcon.Error);
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
            }
        }

        private async Task<bool> CheckActiveAttestationAsync(AuthorizationContext context, bool persistSession)
        {
            string nonce = NonceGenerator.Create();
            var payload = new AuthenticationRequestDto
            {
                AccountId = context.AccountId,
                DeviceId = context.DeviceId,
                Nonce = nonce
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DeviceId,
                payloadJson,
                "authentication state request");

            string endpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AuthenticationState(context.AccountId, context.DeviceId, nonce));

            try
            {
                AuthenticationStateResponseDto? response = await _httpApiClient
                    .GetJsonAsync<AuthenticationStateResponseDto>(endpoint, headers)
                    .ConfigureAwait(false);

                if (response?.Active != true)
                {
                    return false;
                }

                AuthenticationAttestationDto? attestation = response.Attestation;
                if (persistSession)
                {
                    string boundAccountId = string.IsNullOrWhiteSpace(attestation?.AccountId)
                        ? context.AccountId
                        : attestation.AccountId!;
                    string boundDesktopId = string.IsNullOrWhiteSpace(attestation?.DesktopId)
                        ? context.DeviceId
                        : attestation.DesktopId!;

                    _authorizationSessionStore.SaveActive(
                        context.UserId,
                        boundAccountId,
                        boundDesktopId,
                        attestation?.ExpiresAt,
                        attestation?.AttestationId);
                }

                return true;
            }
            catch (ApiRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _log?.Invoke($"[AUTH] Active attestation check failed with {(int)ex.StatusCode}. Treating session as inactive.");
                NotifyAuthorizationStatus("Desktop attestation is no longer active. Log in again.", ToolTipIcon.Warning);
                return false;
            }
        }

        private async Task<bool> RevokeActiveAttestationAsync(AuthorizationContext context)
        {
            string nonce = NonceGenerator.Create();
            var payload = new AuthenticationRequestDto
            {
                AccountId = context.AccountId,
                DeviceId = context.DeviceId,
                Nonce = nonce
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DeviceId,
                payloadJson,
                "authentication revoke request");

            string endpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AuthenticationRevoke(context.AccountId, context.DeviceId, nonce));

            AuthenticationRevokeResponseDto? response = await _httpApiClient
                .DeleteJsonAsync<AuthenticationRequestDto, AuthenticationRevokeResponseDto>(endpoint, payload, headers)
                .ConfigureAwait(false);

            if (response != null && !response.Success)
            {
                _log?.Invoke("[AUTH] Backend reported revoke success=false. Completing local logout anyway.");
                return false;
            }

            return true;
        }

        private AuthorizationContext CreateAuthorizationContext()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string userId = registration.UserKey;
            string accountId = AppEnvironment.GetPrimaryNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetPrimaryNamespaceDeviceIdOrDefault(registration.DeviceId);
            string baseUrl = AppEnvironment.GetPrimaryNamespaceUrlOrDefault(AppEnvironment.BackendBaseUrl);

            return new AuthorizationContext(userId, accountId, deviceId, baseUrl);
        }

        private AuthorizationContext CreateActiveNamespaceAuthorizationContext()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string userId = registration.UserKey;
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);
            string baseUrl = AppEnvironment.GetActiveNamespaceUrlOrDefault(AppEnvironment.BackendBaseUrl);

            return new AuthorizationContext(userId, accountId, deviceId, baseUrl);
        }

        private static bool IsAuthorizationAccepted(AuthenticationResponseDto response)
        {
            return response.Authorized == true ||
                   response.Accepted == true ||
                   response.Authenticated == true ||
                   string.Equals(response.Status, "authorized", StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeResponse(AuthenticationResponseDto? response)
        {
            if (response == null)
            {
                return "<null>";
            }

            return $"status='{response.Status}', accepted={response.Accepted}, authorized={response.Authorized}, authenticated={response.Authenticated}, declined={response.Declined}, notificationId='{response.NotificationId}', timeoutSeconds={response.TimeoutSeconds}, role='{response.Role}'";
        }

        private static bool ContextsMatch(AuthorizationContext left, AuthorizationContext right)
        {
            return string.Equals(left.UserId, right.UserId, StringComparison.Ordinal) &&
                   string.Equals(left.AccountId, right.AccountId, StringComparison.Ordinal) &&
                   string.Equals(left.DeviceId, right.DeviceId, StringComparison.Ordinal) &&
                   string.Equals(left.BaseUrl, right.BaseUrl, StringComparison.OrdinalIgnoreCase);
        }

        private void NotifyAuthorizationStatus(string? message, ToolTipIcon icon)
        {
            _authorizationNotificationChanged?.Invoke(message, icon);
        }

        private static string BuildAuthorizationFailureMessage(AuthenticationResponseDto response)
        {
            if (string.Equals(response.Status, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return "Login failed: authentication session was not found on the backend.";
            }

            if (response.Declined == true)
            {
                return "Login was declined on the mobile device.";
            }

            if (!string.IsNullOrWhiteSpace(response.Status))
            {
                return $"Login failed with status '{response.Status}'.";
            }

            return "Login failed. Mobile approval did not complete.";
        }

        private sealed record AuthorizationContext(string UserId, string AccountId, string DeviceId, string BaseUrl);
    }
}
