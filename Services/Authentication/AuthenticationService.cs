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

        public bool HasAuthorizedOtherProfile => _authorizationSessionStore.HasActiveSessionForAnotherUser(
            AppEnvironment.GetActiveUserId());

        public bool HasAnyRegisteredProfileWithActiveSession => _registeredDeviceService
            .ReadRegistrations()
            .Any(registration => _authorizationSessionStore.GetStates(registration.UserKey).Count > 0);

        public bool IsLoginNavigationDisabled => HasAuthorizedOtherProfile;

        public bool CanLoginForActiveNamespace => !IsAuthorizedForActiveNamespace && !HasAuthorizedOtherProfile;

        public bool IsAuthorizedForActiveNamespace
        {
            get
            {
                try
                {
                    AuthorizationContext activeNamespaceContext = CreateActiveNamespaceAuthorizationContext();
                    return _authorizationSessionStore.HasActiveSessionForNamespace(
                        activeNamespaceContext.UserId,
                        activeNamespaceContext.NamespaceId);
                }
                catch
                {
                    return false;
                }
            }
        }

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
                    $"[AUTH] Active namespace credential check failed. accountId='{activeNamespaceContext.AccountId}', desktopDeviceId='{activeNamespaceContext.DesktopDeviceId}'.");
                NotifyAuthorizationStatus("The selected namespace does not have an active backend credential for this user.", ToolTipIcon.Warning);
            }

            return hasCredential;
        }

        public async Task<bool> EnsureAuthorizedAsync(bool allowInteractiveLogin = true)
        {
            IReadOnlyList<AuthorizationContext> knownContexts = CreateAuthorizationContexts();

            if (await TryReuseAnyActiveAuthorizationAsync(knownContexts, notifyOnExpiredSession: true).ConfigureAwait(false))
            {
                return true;
            }

            if (await TryFindAnyActiveAuthorizationAsync(knownContexts).ConfigureAwait(false))
            {
                _log?.Invoke("[AUTH] Found an active attestation in a known namespace context.");
                return true;
            }

            if (!allowInteractiveLogin)
            {
                return false;
            }

            await _authenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                knownContexts = CreateAuthorizationContexts();

                if (await TryReuseAnyActiveAuthorizationAsync(knownContexts, notifyOnExpiredSession: false).ConfigureAwait(false))
                {
                    _log?.Invoke("[AUTH] Reusing active attestation after lock.");
                    return true;
                }

                if (await TryFindAnyActiveAuthorizationAsync(knownContexts).ConfigureAwait(false))
                {
                    _log?.Invoke("[AUTH] Found an active attestation in a known namespace context after lock.");
                    return true;
                }

                AuthorizationContext context = CreateAuthorizationContext();

                bool authorized = await RequestAuthorizationAsync(context).ConfigureAwait(false);
                if (!authorized)
                {
                    RevokeAuthorization(context);
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

                RevokeAuthorization(context);
                _log?.Invoke("[AUTH] Authorization succeeded but no active attestation was found.");
                NotifyAuthorizationStatus("Login completed but no active desktop attestation was found.", ToolTipIcon.Error);
                return false;
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        public async Task<AuthorizationHealthResult> CheckAuthorizationStateAsync()
        {
            AuthorizationContext activeNamespaceContext = CreateActiveNamespaceAuthorizationContext();
            IReadOnlyList<AuthorizationContext> knownContexts = CreateAuthorizationContexts();
            bool anyActiveSession = false;
            AuthorizationHealthResult? activeNamespaceHealth = null;
            string? transientReason = null;

            foreach (AuthorizationContext context in knownContexts)
            {
                bool isActiveNamespaceContext = ContextsMatch(context, activeNamespaceContext);
                string inactiveReason = isActiveNamespaceContext
                    ? "active_namespace_credential_missing"
                    : "authorization_revoked";

                AuthorizationHealthResult health = await CheckAuthorizationContextHealthAsync(
                    context,
                    persistSession: true,
                    inactiveReason: inactiveReason).ConfigureAwait(false);

                if (health.Authorized)
                {
                    anyActiveSession = true;
                }
                else if (health.ExplicitlyInvalid)
                {
                    RevokeAuthorization(context);
                }
                else if (health.TransientFailure && string.IsNullOrWhiteSpace(transientReason))
                {
                    transientReason = health.Reason;
                }

                if (isActiveNamespaceContext)
                {
                    activeNamespaceHealth = health;
                }
            }

            if (activeNamespaceHealth == null)
            {
                activeNamespaceHealth = await CheckAuthorizationContextHealthAsync(
                    activeNamespaceContext,
                    persistSession: true,
                    inactiveReason: "active_namespace_credential_missing").ConfigureAwait(false);

                if (activeNamespaceHealth.Authorized)
                {
                    anyActiveSession = true;
                }
                else if (activeNamespaceHealth.ExplicitlyInvalid)
                {
                    RevokeAuthorization(activeNamespaceContext);
                }
                else if (activeNamespaceHealth.TransientFailure && string.IsNullOrWhiteSpace(transientReason))
                {
                    transientReason = activeNamespaceHealth.Reason;
                }
            }

            if (anyActiveSession)
            {
                return AuthorizationHealthResult.Active();
            }

            if (activeNamespaceHealth.TransientFailure)
            {
                return activeNamespaceHealth;
            }

            if (!string.IsNullOrWhiteSpace(transientReason))
            {
                return AuthorizationHealthResult.Transient(transientReason);
            }

            if (activeNamespaceHealth.ExplicitlyInvalid)
            {
                return activeNamespaceHealth;
            }

            return AuthorizationHealthResult.Invalid("authorization_missing");
        }

        public void RevokeAuthorization()
        {
            _authorizationSessionStore.Revoke();
        }

        public void RevokeAuthorizationForActiveNamespace()
        {
            AuthorizationContext context = CreateActiveNamespaceAuthorizationContext();
            RevokeAuthorization(context);
        }

        public async Task<bool> LogoutAsync()
        {
            bool remoteSuccess = true;

            try
            {
                IReadOnlyList<AuthorizationContext> knownContexts = CreateAuthorizationContexts();

                foreach (AuthorizationContext context in knownContexts)
                {
                    bool contextSuccess = await RevokeActiveAttestationAsync(context).ConfigureAwait(false);
                    remoteSuccess &= contextSuccess;
                }
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

        public async Task<bool> LogoutActiveNamespaceAsync()
        {
            AuthorizationContext context = CreateActiveNamespaceAuthorizationContext();
            bool remoteSuccess = true;

            try
            {
                remoteSuccess = await RevokeActiveAttestationAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                remoteSuccess = false;
                _log?.Invoke($"[AUTH] Selected namespace logout revoke failed: {ex.Message}");
            }
            finally
            {
                RevokeAuthorization(context);
            }

            return remoteSuccess;
        }

        public async Task<bool> LogoutNamespaceAsync(string userId, string namespaceId)
        {
            AuthorizationContext context = FindAuthorizationContext(userId, namespaceId);
            bool remoteSuccess = true;

            try
            {
                remoteSuccess = await RevokeActiveAttestationAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                remoteSuccess = false;
                _log?.Invoke($"[AUTH] Namespace logout revoke failed. namespaceId='{namespaceId}': {ex.Message}");
            }
            finally
            {
                RevokeAuthorization(context);
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
                    $"[AUTH] Request start. Endpoint='{authEndpoint}', activeNamespaceId='{activeNamespaceId}', accountId='{context.AccountId}', desktopDeviceId='{context.DesktopDeviceId}'.");

                var authenticationPayload = new AuthenticationRequestDto
                {
                    NamespaceId = context.NamespaceId,
                    AccountId = context.AccountId,
                    DesktopDeviceId = context.DesktopDeviceId,
                    Nonce = nonce
                };

                string payloadJson = JsonSerializer.Serialize(authenticationPayload);

                IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                    context.DesktopDeviceId,
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

            if (IsAuthorizationRejected(response))
            {
                _log?.Invoke($"[AUTH] Authorization rejected. Response: {DescribeResponse(response)}");
                NotifyAuthorizationStatus(BuildAuthorizationFailureMessage(response), ToolTipIcon.Error);
                return false;
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
            using var streamCancellation = new CancellationTokenSource();
            var authorizationResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var statusPayload = new
            {
                namespaceId = context.NamespaceId,
                accountId = context.AccountId,
                desktopDeviceId = context.DesktopDeviceId,
                notificationId,
                nonce = NonceGenerator.Create()
            };

            var serializedStatusPayload = new AuthenticationStatusRequestDto
            {
                NamespaceId = context.NamespaceId,
                AccountId = context.AccountId,
                DesktopDeviceId = context.DesktopDeviceId,
                NotificationId = notificationId,
                Nonce = statusPayload.nonce
            };

            string payloadJson = JsonSerializer.Serialize(serializedStatusPayload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DesktopDeviceId,
                payloadJson,
                "authentication status request");

            string statusEndpoint = AppEnvironment.BuildApiUrl(
                context.BaseUrl,
                ApiRoutes.AuthenticationStatusNotification(context.AccountId, context.DesktopDeviceId, notificationId, statusPayload.nonce, context.NamespaceId));
            _log?.Invoke($"[AUTH] Listening for status updates. Endpoint='{statusEndpoint}'.");

            Task streamTask = _httpApiClient.ConnectToSseEndpoint(
                statusEndpoint,
                headers,
                data => HandleAuthenticationStatusMessage(data, authorizationResult, streamCancellation),
                streamCancellation.Token);
            Task timeoutTask = Task.Delay(timeout);

            try
            {
                Task completedTask = await Task.WhenAny(authorizationResult.Task, streamTask, timeoutTask)
                    .ConfigureAwait(false);

                if (completedTask == authorizationResult.Task)
                {
                    streamCancellation.Cancel();
                    return await authorizationResult.Task.ConfigureAwait(false);
                }

                if (completedTask == timeoutTask)
                {
                    streamCancellation.Cancel();
                    _log?.Invoke("[AUTH] Status stream timed out.");
                    NotifyAuthorizationStatus("Login timed out while waiting for mobile approval.", ToolTipIcon.Warning);
                    return false;
                }

                await streamTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (authorizationResult.Task.IsCompleted)
            {
                _log?.Invoke("[AUTH] Status stream completed after result was determined.");
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

            if (IsAuthorizationRejected(statusResponse))
            {
                _log?.Invoke($"[AUTH] SSE status rejected authorization. Response: {DescribeResponse(statusResponse)}");
                NotifyAuthorizationStatus(BuildAuthorizationFailureMessage(statusResponse), ToolTipIcon.Error);
                authorizationResult.TrySetResult(false);
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
            string payloadJson = BuildAuthenticationStatePayloadJson(context, nonce);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DesktopDeviceId,
                payloadJson,
                "authentication state request");

            string endpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AuthenticationState(context.AccountId, context.DesktopDeviceId, nonce, context.NamespaceId));

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
                        ? context.DesktopDeviceId
                        : attestation.DesktopId!;

                    _authorizationSessionStore.SaveActive(
                        context.UserId,
                        context.NamespaceId,
                        context.BaseUrl,
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

        public async Task<bool> EnsureAuthorizedForActiveNamespaceAsync(bool allowInteractiveLogin = true)
        {
            AuthorizationContext activeNamespaceContext = CreateActiveNamespaceAuthorizationContext();

            if (await TryEnsureAuthorizationContextAsync(
                    activeNamespaceContext,
                    allowInteractiveLogin: false,
                    notifyOnExpiredSession: true,
                    logReuseMessage: "[AUTH] Reusing active attestation for the selected namespace.",
                    logFoundMessage: "[AUTH] Found an active attestation for the selected namespace.")
                .ConfigureAwait(false))
            {
                return true;
            }

            if (!allowInteractiveLogin)
            {
                return false;
            }

            await _authenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                activeNamespaceContext = CreateActiveNamespaceAuthorizationContext();

                if (await TryEnsureAuthorizationContextAsync(
                        activeNamespaceContext,
                        allowInteractiveLogin: false,
                        notifyOnExpiredSession: false,
                        logReuseMessage: "[AUTH] Reusing selected namespace attestation after lock.",
                        logFoundMessage: "[AUTH] Found an active attestation for the selected namespace after lock.")
                    .ConfigureAwait(false))
                {
                    return true;
                }

                string? authorizedProfileName = await FindOtherAuthorizedProfileAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(authorizedProfileName))
                {
                    NotifyAuthorizationStatus(
                        $"Login blocked: {authorizedProfileName} already has an active desktop session. Log out before switching profiles.",
                        ToolTipIcon.Warning);
                    return false;
                }

                return await TryEnsureAuthorizationContextAsync(
                        activeNamespaceContext,
                        allowInteractiveLogin,
                        notifyOnExpiredSession: false,
                        logReuseMessage: "[AUTH] Reusing selected namespace attestation after lock.",
                        logFoundMessage: "[AUTH] Found an active attestation for the selected namespace after lock.")
                    .ConfigureAwait(false);
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        private async Task<string?> FindOtherAuthorizedProfileAsync()
        {
            string? activeUserId = AppEnvironment.GetActiveUserId();
            if (string.IsNullOrWhiteSpace(activeUserId))
            {
                return null;
            }

            foreach (DesktopRegistration registration in _registeredDeviceService.ReadRegistrations())
            {
                if (string.Equals(registration.UserKey, activeUserId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
                {
                    if (string.IsNullOrWhiteSpace(namespaceItem.Url))
                    {
                        continue;
                    }

                    AuthorizationContext context = CreateAuthorizationContext(registration, namespaceItem, string.Empty);
                    if (!context.IsValid)
                    {
                        continue;
                    }
                    AuthorizationHealthResult health = await CheckAuthorizationContextHealthAsync(
                        context,
                        persistSession: true,
                        inactiveReason: "authorization_revoked").ConfigureAwait(false);

                    if (health.Authorized)
                    {
                        return registration.DisplayUserName;
                    }

                    if (health.ExplicitlyInvalid)
                    {
                        RevokeAuthorization(context);
                    }
                }
            }

            return null;
        }

        private async Task<AuthorizationHealthResult> CheckAuthorizationContextHealthAsync(
            AuthorizationContext context,
            bool persistSession,
            string inactiveReason)
        {
            string nonce = NonceGenerator.Create();
            string payloadJson = BuildAuthenticationStatePayloadJson(context, nonce);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DesktopDeviceId,
                payloadJson,
                "authentication state request");

            string endpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AuthenticationState(context.AccountId, context.DesktopDeviceId, nonce, context.NamespaceId));

            try
            {
                AuthenticationStateResponseDto? response = await _httpApiClient
                    .GetJsonAsync<AuthenticationStateResponseDto>(endpoint, headers)
                    .ConfigureAwait(false);

                if (response?.Active != true)
                {
                    return AuthorizationHealthResult.Invalid(inactiveReason);
                }

                AuthenticationAttestationDto? attestation = response.Attestation;
                if (persistSession)
                {
                    string boundAccountId = string.IsNullOrWhiteSpace(attestation?.AccountId)
                        ? context.AccountId
                        : attestation.AccountId!;
                    string boundDesktopId = string.IsNullOrWhiteSpace(attestation?.DesktopId)
                        ? context.DesktopDeviceId
                        : attestation.DesktopId!;

                    _authorizationSessionStore.SaveActive(
                        context.UserId,
                        context.NamespaceId,
                        context.BaseUrl,
                        boundAccountId,
                        boundDesktopId,
                        attestation?.ExpiresAt,
                        attestation?.AttestationId);
                }

                return AuthorizationHealthResult.Active();
            }
            catch (ApiRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return AuthorizationHealthResult.Invalid(inactiveReason);
            }
            catch (Exception ex)
            {
                return AuthorizationHealthResult.Transient(ex.Message);
            }
        }

        private async Task<bool> RevokeActiveAttestationAsync(AuthorizationContext context)
        {
            string nonce = NonceGenerator.Create();
            var payload = new AuthenticationRevokeRequestDto
            {
                AccountId = context.AccountId,
                DesktopDeviceId = context.DesktopDeviceId,
                Nonce = nonce,
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                context.DesktopDeviceId,
                payloadJson,
                "authentication revoke request");

            string endpoint = AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AuthenticationRevoke(context.AccountId, context.DesktopDeviceId, nonce, context.NamespaceId));

            AuthenticationRevokeResponseDto? response = await _httpApiClient
                .DeleteJsonAsync<object, AuthenticationRevokeResponseDto>(endpoint, payload, headers)
                .ConfigureAwait(false);

            if (response != null && !response.Success)
            {
                _log?.Invoke("[AUTH] Backend reported revoke success=false. Completing local logout anyway.");
                return false;
            }

            return true;
        }

        private static string BuildAuthenticationStatePayloadJson(AuthorizationContext context, string nonce)
        {
            var payload = new
            {
                accountId = context.AccountId,
                desktopDeviceId = context.DesktopDeviceId,
                namespaceId = context.NamespaceId,
                nonce,
            };

            return JsonSerializer.Serialize(payload);
        }

        private async Task<bool> TryEnsureAuthorizationContextAsync(
            AuthorizationContext context,
            bool allowInteractiveLogin,
            bool notifyOnExpiredSession,
            string logReuseMessage,
            string logFoundMessage)
        {
            if (_authorizationSessionStore.Matches(context.UserId, context.AccountId, context.DesktopDeviceId, context.BaseUrl))
            {
                AuthorizationHealthResult storedHealth = await CheckAuthorizationContextHealthAsync(
                    context,
                    persistSession: true,
                    inactiveReason: "authorization_revoked").ConfigureAwait(false);

                if (storedHealth.Authorized)
                {
                    _log?.Invoke(logReuseMessage);
                    return true;
                }

                RevokeAuthorization(context);
                _log?.Invoke("[AUTH] Cached selected namespace attestation was no longer active.");

                if (notifyOnExpiredSession)
                {
                    NotifyAuthorizationStatus("Desktop attestation expired. Log in again.", ToolTipIcon.Warning);
                }
            }

            AuthorizationHealthResult activeHealth = await CheckAuthorizationContextHealthAsync(
                context,
                persistSession: true,
                inactiveReason: "active_namespace_credential_missing").ConfigureAwait(false);

            if (activeHealth.Authorized)
            {
                _log?.Invoke(logFoundMessage);
                return true;
            }

            if (!allowInteractiveLogin)
            {
                return false;
            }

            bool authorized = await RequestAuthorizationAsync(context).ConfigureAwait(false);
            if (!authorized)
            {
                RevokeAuthorization(context);
                _log?.Invoke("[AUTH] Selected namespace authorization failed. Session revoked.");
                NotifyAuthorizationStatus("Login failed. Mobile approval was declined or the session no longer exists.", ToolTipIcon.Error);
                return false;
            }

            bool attestationActive = await CheckActiveAttestationAsync(context, persistSession: true).ConfigureAwait(false);
            if (attestationActive)
            {
                _log?.Invoke("[AUTH] Selected namespace authorization succeeded. Active attestation confirmed.");
                return true;
            }

            RevokeAuthorization(context);
            _log?.Invoke("[AUTH] Selected namespace authorization succeeded but no active attestation was found.");
            NotifyAuthorizationStatus("Login completed but no active desktop attestation was found.", ToolTipIcon.Error);
            return false;
        }

        private AuthorizationContext CreateAuthorizationContext()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string userId = registration.UserKey;
            string namespaceId = registration.Namespaces.FirstOrDefault()?.NamespaceId ?? string.Empty;
            string accountId = AppEnvironment.GetPrimaryNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetPrimaryNamespaceDesktopDeviceId();
            string baseUrl = AppEnvironment.GetPrimaryNamespaceUrl();

            return new AuthorizationContext(userId, namespaceId, accountId, desktopDeviceId, baseUrl);
        }

        private IReadOnlyList<AuthorizationContext> CreateAuthorizationContexts()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            var contexts = new List<AuthorizationContext>
            {
                CreateAuthorizationContext()
            };

            foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
            {
                contexts.Add(CreateAuthorizationContext(registration, namespaceItem, AppEnvironment.BackendBaseUrl));
            }

            return contexts
                .GroupBy(context => string.IsNullOrWhiteSpace(context.NamespaceId)
                    ? $"{context.AccountId}|{context.DesktopDeviceId}|{context.BaseUrl}"
                    : context.NamespaceId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static AuthorizationContext CreateAuthorizationContext(
            DesktopRegistration registration,
            NamespaceItemDto namespaceItem,
            string fallbackBaseUrl)
        {
            string accountId = string.IsNullOrWhiteSpace(namespaceItem.AccountId)
                ? registration.AccountId
                : namespaceItem.AccountId;
            string desktopDeviceId = string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId)
                ? registration.DesktopDeviceId
                : namespaceItem.DesktopDeviceId;
            string baseUrl = string.IsNullOrWhiteSpace(namespaceItem.Url)
                ? fallbackBaseUrl
                : namespaceItem.Url.TrimEnd('/');

            return new AuthorizationContext(
                registration.UserKey,
                namespaceItem.NamespaceId,
                accountId,
                desktopDeviceId,
                baseUrl);
        }

        private AuthorizationContext CreateActiveNamespaceAuthorizationContext()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string userId = registration.UserKey;
            string namespaceId = AppEnvironment.GetActiveNamespaceId() ?? string.Empty;
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();
            string baseUrl = AppEnvironment.GetActiveNamespaceUrl();

            return new AuthorizationContext(userId, namespaceId, accountId, desktopDeviceId, baseUrl);
        }

        private AuthorizationContext FindAuthorizationContext(string userId, string namespaceId)
        {
            AuthorizationContext? context = CreateAuthorizationContexts().FirstOrDefault(item =>
                string.Equals(item.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(item.NamespaceId, namespaceId, StringComparison.OrdinalIgnoreCase));

            if (context == null)
            {
                throw new InvalidOperationException($"Authorization context not found for namespace '{namespaceId}'.");
            }

            return context;
        }

        private static bool IsAuthorizationAccepted(AuthenticationResponseDto response)
        {
            return response.Authorized == true ||
                   response.Accepted == true ||
                   response.Authenticated == true ||
                   string.Equals(response.Status, "authorized", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAuthorizationRejected(AuthenticationResponseDto response)
        {
            return response.Declined == true ||
                   string.Equals(response.Status, "declined", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(response.Status, "denied", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(response.Status, "rejected", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(response.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(response.Status, "expired", StringComparison.OrdinalIgnoreCase);
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
                   string.Equals(left.DesktopDeviceId, right.DesktopDeviceId, StringComparison.Ordinal) &&
                   string.Equals(left.BaseUrl, right.BaseUrl, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> TryReuseAnyActiveAuthorizationAsync(
            IReadOnlyList<AuthorizationContext> contexts,
            bool notifyOnExpiredSession)
        {
            AuthorizationContext? storedContext = FindStoredAuthorizationContext(contexts);
            if (storedContext == null)
            {
                return false;
            }

            AuthorizationHealthResult health = await CheckAuthorizationContextHealthAsync(
                storedContext,
                persistSession: true,
                inactiveReason: "authorization_revoked").ConfigureAwait(false);

            if (health.Authorized)
            {
                _log?.Invoke("[AUTH] Reusing active attestation.");
                return true;
            }

            RevokeAuthorization(storedContext);
            _log?.Invoke("[AUTH] Cached attestation was no longer active.");

            if (notifyOnExpiredSession)
            {
                NotifyAuthorizationStatus("Desktop attestation expired. Log in again.", ToolTipIcon.Warning);
            }

            return false;
        }

        private async Task<bool> TryFindAnyActiveAuthorizationAsync(IReadOnlyList<AuthorizationContext> contexts)
        {
            foreach (AuthorizationContext context in contexts)
            {
                AuthorizationHealthResult health = await CheckAuthorizationContextHealthAsync(
                    context,
                    persistSession: true,
                    inactiveReason: "authorization_revoked").ConfigureAwait(false);

                if (health.Authorized)
                {
                    return true;
                }
            }

            return false;
        }

        private AuthorizationContext? FindStoredAuthorizationContext(IReadOnlyList<AuthorizationContext> contexts)
        {
            return contexts.FirstOrDefault(context =>
                _authorizationSessionStore.Matches(context.UserId, context.AccountId, context.DesktopDeviceId, context.BaseUrl));
        }

        private void RevokeAuthorization(AuthorizationContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.NamespaceId))
            {
                _authorizationSessionStore.Revoke(context.UserId, context.NamespaceId);
                return;
            }

            _authorizationSessionStore.Revoke(context.UserId, context.AccountId, context.DesktopDeviceId, context.BaseUrl);
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

        private sealed record AuthorizationContext(string UserId, string NamespaceId, string AccountId, string DesktopDeviceId, string BaseUrl)
        {
            public bool IsValid => !string.IsNullOrWhiteSpace(AccountId) &&
                !string.IsNullOrWhiteSpace(DesktopDeviceId) &&
                !string.IsNullOrWhiteSpace(BaseUrl);
        }
    }
}
