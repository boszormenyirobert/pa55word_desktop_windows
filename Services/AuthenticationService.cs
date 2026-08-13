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
        private readonly SemaphoreSlim _authenticationLock = new(1, 1);

        public AuthenticationService(
            RegisteredDeviceService registeredDeviceService,
            HttpApiClient httpApiClient,
            AuthorizationSessionStore authorizationSessionStore,
            Action<string?>? authorizationNotificationChanged = null)
        {
            _registeredDeviceService = registeredDeviceService;
            _httpApiClient = httpApiClient;
            _authorizationSessionStore = authorizationSessionStore;
            _authorizationNotificationChanged = authorizationNotificationChanged;
        }

        public async Task<bool> EnsureAuthorizedAsync()
        {
            if (_authorizationSessionStore.IsAuthorized)
            {
                return true;
            }

            await _authenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_authorizationSessionStore.IsAuthorized)
                {
                    return true;
                }

                bool authorized = await RequestAuthorizationAsync().ConfigureAwait(false);
                if (authorized)
                {
                    _authorizationSessionStore.AuthorizeFor(AuthorizationDuration);
                }
                else
                {
                    _authorizationSessionStore.Revoke();
                }

                return authorized;
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        private async Task<bool> RequestAuthorizationAsync()
        {
            try
            {
                var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();

                var authenticationPayload = new AuthenticationRequestDto
                {
                    AccountId = registration.AccountId,
                    DeviceId = registration.DeviceId
                };

                IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                    registration.DeviceId,
                    JsonSerializer.Serialize(authenticationPayload),
                    "authentication request");

                AuthenticationResponseDto? response = await _httpApiClient
                    .PostJsonAsync<AuthenticationRequestDto, AuthenticationResponseDto>(AuthenticationEndpoint, authenticationPayload, headers)
                    .ConfigureAwait(false);

                bool authorized = await ResolveAuthorizationResponseAsync(
                    response,
                    registration.DeviceId,
                    registration.AccountId).ConfigureAwait(false);

                if (authorized)
                {
                    await FetchAndStoreNamespacesAsync(
                        registration.AccountId,
                        registration.DeviceId).ConfigureAwait(false);
                }

                return authorized;
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
                return false;
            }

            if (IsAuthorizationAccepted(response))
            {
                return true;
            }

            if (!string.Equals(response.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.NotificationId))
            {
                return false;
            }

            TimeSpan timeout = TimeSpan.FromSeconds(response.TimeoutSeconds.GetValueOrDefault(90));

            _authorizationNotificationChanged?.Invoke("Waiting for confirmation on your mobile device");

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

            try
            {
                await _httpApiClient.ConnectToSseEndpoint(
                    AppEnvironment.BuildApiUrl(ApiRoutes.AuthenticationStatusNotification(accountId, deviceId, notificationId)),
                    headers,
                    data => HandleAuthenticationStatusMessage(data, authorizationResult, streamCancellation),
                    streamCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (authorizationResult.Task.IsCompleted)
            {
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                return false;
            }

            return authorizationResult.Task.IsCompleted && await authorizationResult.Task.ConfigureAwait(false);
        }

        private static void HandleAuthenticationStatusMessage(
            string data,
            TaskCompletionSource<bool> authorizationResult,
            CancellationTokenSource streamCancellation)
        {
            AuthenticationResponseDto? statusResponse;

            try
            {
                statusResponse = JsonSerializer.Deserialize<AuthenticationResponseDto>(
                    data,
                    AuthenticationResponseJsonOptions);
            }
            catch (JsonException)
            {
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
                return;
            }

            if (statusResponse == null)
            {
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
                return;
            }

            if (IsAuthorizationAccepted(statusResponse))
            {
                authorizationResult.TrySetResult(true);
                streamCancellation.Cancel();
                return;
            }

            if (!string.Equals(statusResponse.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                authorizationResult.TrySetResult(false);
                streamCancellation.Cancel();
            }
        }

        private async Task FetchAndStoreNamespacesAsync(string accountId, string deviceId)
        {
            var namespacesPayload = new AuthenticationRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId
            };

            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                deviceId,
                JsonSerializer.Serialize(namespacesPayload),
                "namespaces request");

            List<NamespaceItemDto>? namespaces = await _httpApiClient
                .GetJsonAsync<List<NamespaceItemDto>>(
                    AppEnvironment.BuildApiUrl(ApiRoutes.NamespacesForDevice(accountId, deviceId)),
                    headers)
                .ConfigureAwait(false);

            _registeredDeviceService.SaveNamespaces(accountId, deviceId, namespaces ?? new List<NamespaceItemDto>());
        }

        private static bool IsAuthorizationAccepted(AuthenticationResponseDto response)
        {
            return response.Authorized == true ||
                   response.Accepted == true ||
                   response.Authenticated == true ||
                   string.Equals(response.Status, "authorized", StringComparison.OrdinalIgnoreCase) ||
                   !string.IsNullOrWhiteSpace(response.Role);
        }
    }
}
