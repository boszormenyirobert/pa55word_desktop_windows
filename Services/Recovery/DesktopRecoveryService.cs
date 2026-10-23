using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class RecoveryClientException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public DateTimeOffset? RetryAt { get; }
        public RecoveryClientException(HttpStatusCode status, string message, DateTimeOffset? retryAt = null)
            : base(message) { StatusCode = status; RetryAt = retryAt; }
    }

    internal sealed class DesktopRecoveryService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
        private readonly HttpApiClient _api;
        private readonly HttpClient? _ownedClient;
        private readonly IRecoveryKeyService _keys;
        private readonly IRecoverySessionStore _store;
        private readonly SemaphoreSlim _gate = new(1, 1);
        public RecoveryTarget Target { get; }
        public RecoverySession Session { get; private set; }
        public DateTimeOffset? RetryAt => Session.RetryAt;
        public bool HasPendingRequest => Session.PendingJson != null;
        public bool IsTerminal => Session.Status is "completed" or "cancelled" or "expired" or "revoked" or "locked" or "delivery_failed";

        public DesktopRecoveryService(RecoveryTarget target, IRecoverySessionStore? store = null,
            IRecoveryKeyService? keys = null, HttpApiClient? api = null)
        {
            Target = target with { BaseUrl = RecoveryTarget.NormalizeOrigin(target.BaseUrl) };
            _store = store ?? new RecoverySessionStore();
            _keys = keys ?? new RecoveryKeyService();
            if (api == null)
            {
                _ownedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { Timeout = TimeSpan.FromSeconds(45) };
                _api = new HttpApiClient(_ownedClient);
            }
            else _api = api;
            Session = _store.Load(Target) ?? new RecoverySession { TargetKey = Target.StorageKey };
            ValidateSavedSession();
        }

        public Task StartAsync(CancellationToken token) => MutateAsync("start",
            () => new JsonObject { ["desktopDeviceId"] = Target.DesktopDeviceId }, token);
        public Task ConfirmAsync(string code, CancellationToken token) =>
            MutateAsync("confirmation", () => new JsonObject { ["code"] = ValidateCode(code) }, token);
        public Task ResendAsync(CancellationToken token) => MutateAsync("resend", () => new JsonObject(), token);
        public Task PairAsync(string code, CancellationToken token) =>
            MutateAsync("pairing", () => new JsonObject { ["pairingCode"] = ValidateCode(code) }, token);
        public Task CancelAsync(CancellationToken token) => MutateAsync("cancellation", () => new JsonObject(), token);
        public Task UploadDekAsync(CancellationToken token) => MutateAsync("dek", () =>
        {
            var selected = Session.Selected ?? throw new InvalidOperationException("Retry the pairing request before transferring the DEK.");
            return new JsonObject
            {
                ["candidateId"] = selected.CandidateId,
                ["mobileDeviceId"] = selected.MobileDeviceId,
                ["encDek"] = _keys.ReencryptDek(Target, selected.PublicKey)
            };
        }, token);

        private async Task MutateAsync(string operation, Func<JsonObject> fields, CancellationToken token)
        {
            await _gate.WaitAsync(token);
            try
            {
                EnsureRetryAllowed();
                if (HasPendingRequest) throw new InvalidOperationException("Retry the pending request first. Its outcome is not yet known.");
                RequireOperationState(operation);
                JsonObject body = new() { ["operation"] = operation, ["requestId"] = Guid.NewGuid().ToString() };
                foreach (var field in fields()) body[field.Key] = field.Value?.DeepClone();
                if (operation != "start") body["recoveryId"] = Session.RecoveryId;
                else Session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
                Session.PendingOperation = operation;
                Session.PendingJson = body.ToJsonString();
                Save();
                await ExecutePendingAsync(token);
            }
            finally { _gate.Release(); }
        }

        public async Task RetryAsync(CancellationToken token)
        {
            await _gate.WaitAsync(token);
            try
            {
                EnsureRetryAllowed();
                if (!HasPendingRequest) throw new InvalidOperationException("There is no pending request to retry.");
                await ExecutePendingAsync(token);
            }
            finally { _gate.Release(); }
        }

        public async Task RefreshAsync(CancellationToken token)
        {
            await _gate.WaitAsync(token);
            try
            {
                EnsureRetryAllowed();
                if (string.IsNullOrEmpty(Session.RecoveryId))
                    throw new InvalidOperationException("Retry the start request to retrieve its recovery identifier.");
                var body = new JsonObject { ["operation"] = "status", ["recoveryId"] = Session.RecoveryId };
                string snapshot = JsonSerializer.Serialize(Session);
                try
                {
                    var response = await SendAsync(ApiRoutes.RecoveryStatus, body, token);
                    ApplyState(Read<RecoveryStateDto>(response));
                    Save();
                }
                catch (ApiRequestException error) { throw HandleApiError(error, mutation: false); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
                {
                    Session = JsonSerializer.Deserialize<RecoverySession>(snapshot)!;
                    throw;
                }
            }
            finally { _gate.Release(); }
        }

        private async Task ExecutePendingAsync(CancellationToken token)
        {
            string operation = Session.PendingOperation ?? throw new InvalidDataException("Saved recovery operation is missing.");
            var body = JsonNode.Parse(Session.PendingJson!) as JsonObject
                ?? throw new InvalidDataException("Saved recovery request is invalid.");
            Save();
            string snapshot = JsonSerializer.Serialize(Session);
            try
            {
                JsonElement response = await SendAsync(Route(operation), body, token);
                if (operation == "start")
                {
                    var started = Read<RecoveryStartedDto>(response);
                    ValidateId(started.RecoveryId);
                    Session.RecoveryId = started.RecoveryId;
                    Session.Status = "email_pending";
                }
                else if (operation == "pairing")
                {
                    var selected = Read<RecoverySelectedDto>(response);
                    ValidateSelection(selected);
                    ApplyState(selected);
                    Session.Selected = selected;
                }
                else
                {
                    var state = Read<RecoveryStateDto>(response);
                    string expected = operation switch
                    {
                        "confirmation" => "email_confirmed",
                        "resend" => "email_pending",
                        "dek" => "dek_ready",
                        "cancellation" => "cancelled",
                        _ => throw new InvalidDataException("Unexpected recovery response.")
                    };
                    if (state.Status != expected)
                        throw new InvalidDataException("The recovery server returned an unexpected operation result.");
                    ApplyState(state);
                }
                if (operation is "start" or "resend") Session.EmailResendAt = DateTimeOffset.UtcNow.AddSeconds(60);
                ClearPending();
                Save();
            }
            catch (ApiRequestException error) { throw HandleApiError(error, mutation: true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            {
                Session = JsonSerializer.Deserialize<RecoverySession>(snapshot)!;
                throw;
            }
        }

        private async Task<JsonElement> SendAsync(string route, JsonObject body, CancellationToken token)
        {
            body["nonce"] = Guid.NewGuid().ToString();
            string json = body.ToJsonString();
            var headers = _keys.Sign(Target.DesktopDeviceId, json);
            return await _api.PostSensitiveJsonAsync<JsonElement>(
                AppEnvironment.BuildApiUrl(Target.BaseUrl, route), json, headers, token);
        }

        private RecoveryClientException HandleApiError(ApiRequestException error, bool mutation)
        {
            string message = "The recovery server rejected the request.";
            string internalCode = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(error.ResponseBody);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Invalid recovery error response.");
                if (document.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
                    message = value.GetString() ?? message;
                if (document.RootElement.TryGetProperty("internalCode", out var code) && code.ValueKind == JsonValueKind.String)
                    internalCode = code.GetString() ?? string.Empty;
            }
            catch (JsonException) { message = "The recovery server returned an invalid error response."; }
            var status = error.StatusCode ?? HttpStatusCode.BadGateway;
            if (status == HttpStatusCode.TooManyRequests)
                Session.RetryAt = DateTimeOffset.UtcNow + (error.RetryAfter is { } delay && delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(60));
            bool pending = status == HttpStatusCode.Conflict && message.Contains("outcome is pending", StringComparison.OrdinalIgnoreCase);
            bool deliveryFailed = status == HttpStatusCode.ServiceUnavailable && internalCode == "e_recovery_503";
            if (mutation && !pending && ((int)status < 500 || deliveryFailed))
            {
                if (deliveryFailed && Session.PendingOperation == "start") Session.Status = "delivery_failed";
                ClearPending();
            }
            if (status == HttpStatusCode.Gone && !message.StartsWith("Candidate ", StringComparison.Ordinal) &&
                message != "Email code expired or invalidated")
            { Session.Status = "expired"; Session.Selected = null; ClearPending(); }
            if (status == HttpStatusCode.Unauthorized) { Session.Status = "revoked"; Session.Selected = null; ClearPending(); }
            if (status == HttpStatusCode.TooManyRequests && message.Contains("attempt limit", StringComparison.OrdinalIgnoreCase))
            { Session.Status = "locked"; Session.Selected = null; ClearPending(); }
            Save();
            return new RecoveryClientException(status, message.Length > 250 ? message[..250] : message, RetryAt);
        }

        public void Reset()
        {
            if (HasPendingRequest || (!IsTerminal && Session.Status != "not_started"))
                throw new InvalidOperationException("Cancel the active recovery or resolve the pending request first.");
            _store.Delete(Target);
            Session = new RecoverySession { TargetKey = Target.StorageKey };
        }

        public string QrPayload()
        {
            if (Session.Status != "email_confirmed")
                throw new InvalidOperationException("The recovery QR is available only after email confirmation.");
            return JsonSerializer.Serialize(new
            {
                type = "recovery",
                version = 1,
                recoveryId = Session.RecoveryId,
                apiBaseUrl = Target.BaseUrl
            });
        }

        internal static string ValidateCode(string code)
        {
            if (code.Length != 12 || code.Any(character => character is < '0' or > '9'))
                throw new ArgumentException("Enter exactly 12 digits, including any leading zeroes.");
            return code;
        }

        private void RequireOperationState(string operation)
        {
            if (operation == "resend" && Session.EmailResendAt > DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Wait 60 seconds after the previous email before requesting a new code.");
            bool allowed = operation switch
            {
                "start" => Session.Status == "not_started",
                "confirmation" or "resend" => Session.Status == "email_pending",
                "pairing" => Session.Status == "email_confirmed",
                "dek" => Session.Status == "mobile_selected" && Session.Selected != null,
                "cancellation" => !IsTerminal && !string.IsNullOrEmpty(Session.RecoveryId),
                _ => false
            };
            if (!allowed) throw new InvalidOperationException("This action is not available in the current recovery state.");
        }

        private void ValidateSavedSession()
        {
            if (Session.TargetKey != Target.StorageKey || !KnownState(Session.Status) ||
                (Session.PendingJson == null) != (Session.PendingOperation == null))
                throw new InvalidDataException("Saved recovery state is invalid. Do not start another recovery until its outcome is resolved.");
            if (!string.IsNullOrEmpty(Session.RecoveryId)) ValidateId(Session.RecoveryId);
            if (Session.PendingOperation != null) _ = Route(Session.PendingOperation);
            if (Session.Selected != null) ValidateSelection(Session.Selected);
        }

        private static void ValidateId(string id)
        {
            if (!Guid.TryParseExact(id, "D", out var guid) || guid.ToString() != id)
                throw new InvalidDataException("The server returned an invalid recovery identifier.");
        }

        private void ValidateSelection(RecoverySelectedDto selected)
        {
            ValidateId(selected.CandidateId);
            if (selected.RecoveryId != Session.RecoveryId || selected.Status != "mobile_selected" ||
                string.IsNullOrWhiteSpace(selected.DeviceName) || selected.DeviceName.Length > 200 ||
                string.IsNullOrWhiteSpace(selected.MobileDeviceId) || selected.MobileDeviceId.Length is < 1 or > 64 ||
                selected.MobileDeviceId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
                throw new InvalidDataException("The server returned an invalid selected mobile.");
            using var key = RecoveryKeyService.ImportMobileKey(selected.PublicKey);
        }

        private void ApplyState(RecoveryStateDto state)
        {
            if (state.RecoveryId != Session.RecoveryId || state.Status == "not_started" || !KnownState(state.Status) ||
                state.ExpiresAt <= DateTimeOffset.UnixEpoch)
                throw new InvalidDataException("The recovery server returned an invalid state.");
            Session.Status = state.Status;
            Session.ExpiresAt = state.ExpiresAt;
            if (IsTerminal) { Session.Selected = null; ClearPending(); }
        }

        private static T Read<T>(JsonElement response) where T : class =>
            response.ValueKind == JsonValueKind.Object
                ? response.Deserialize<T>(JsonOptions) ?? throw new InvalidDataException("The recovery response is empty.")
                : throw new InvalidDataException("The recovery response must be a JSON object.");

        private static bool KnownState(string state) => state is "not_started" or "delivery_pending" or "email_pending"
            or "email_confirmed" or "mobile_selected" or "dek_ready" or "completed" or "delivery_failed"
            or "cancelled" or "revoked" or "expired" or "locked";
        private static string Route(string operation) => operation switch
        {
            "start" => ApiRoutes.Recovery,
            "confirmation" => ApiRoutes.RecoveryConfirmation,
            "resend" => ApiRoutes.RecoveryResend,
            "pairing" => ApiRoutes.RecoveryPairing,
            "dek" => ApiRoutes.RecoveryDek,
            "cancellation" => ApiRoutes.RecoveryCancellation,
            _ => throw new InvalidDataException("Unknown saved recovery operation.")
        };
        private void EnsureRetryAllowed()
        {
            if (RetryAt > DateTimeOffset.UtcNow)
                throw new InvalidOperationException($"The server requested a pause. Retry after {RetryAt:HH:mm:ss}.");
        }
        private void ClearPending() { Session.PendingJson = null; Session.PendingOperation = null; }
        private void Save() => _store.Save(Target, Session);
        public void Dispose() { _ownedClient?.Dispose(); _gate.Dispose(); }
    }
}
