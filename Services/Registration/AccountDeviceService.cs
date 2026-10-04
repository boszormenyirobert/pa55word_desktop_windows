using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AccountDeviceService
    {
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;

        private readonly record struct AccountDeviceRequestContext(string BaseUrl, string AccountId, string DesktopDeviceId);

        public AccountDeviceService(
            HttpApiClient httpApiClient,
            AuthenticationService authenticationService)
        {
            _httpApiClient = httpApiClient;
            _authenticationService = authenticationService;
        }

        public async Task<IReadOnlyList<AccountDeviceDto>> ReadDevicesAsync()
        {
            EnsureAuthorizedForActiveNamespace();
            AccountDeviceRequestContext context = CreateActiveNamespaceRequestContext();
            return await ReadDevicesAsync(context).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<AccountDeviceDto>> ReadDevicesAsync(
            string baseUrl,
            string accountId,
            string desktopDeviceId)
        {
            AccountDeviceRequestContext context = CreateRequestContext(baseUrl, accountId, desktopDeviceId);
            return await ReadDevicesAsync(context).ConfigureAwait(false);
        }

        public async Task DeleteDeviceAsync(AccountDeviceDto device)
        {
            ArgumentNullException.ThrowIfNull(device);

            EnsureAuthorizedForActiveNamespace();
            AccountDeviceRequestContext context = CreateActiveNamespaceRequestContext();
            await DeleteDeviceAsync(context, device).ConfigureAwait(false);
        }

        public async Task DeleteDeviceAsync(
            AccountDeviceDto device,
            string baseUrl,
            string accountId,
            string desktopDeviceId)
        {
            ArgumentNullException.ThrowIfNull(device);

            AccountDeviceRequestContext context = CreateRequestContext(baseUrl, accountId, desktopDeviceId);
            await DeleteDeviceAsync(context, device).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<AccountDeviceDto>> ReadDevicesAsync(AccountDeviceRequestContext context)
        {
            AccountDevicesRequestDto request = CreateRequest(context);
            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                request.DesktopDeviceId,
                payloadJson,
                "account device list request");

            AccountDevicesResponseDto? response = await SendAsync(request.DesktopDeviceId, () => _httpApiClient
                .PostJsonAsync<AccountDevicesRequestDto, AccountDevicesResponseDto>(
                    AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AccountDevices),
                    request,
                    headers))
                .ConfigureAwait(false);

            EnsureSuccess(response?.Success ?? false, response?.Error, "The account device list could not be loaded.");
            return response!.Devices;
        }

        private async Task DeleteDeviceAsync(AccountDeviceRequestContext context, AccountDeviceDto device)
        {
            AccountDeviceDeleteRequestDto request = CreateDeleteRequest(context, device);
            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                request.DesktopDeviceId,
                payloadJson,
                "account device deletion request");

            AccountDeviceDeleteResponseDto? response = await SendAsync(request.DesktopDeviceId, () => _httpApiClient
                .DeleteJsonAsync<AccountDeviceDeleteRequestDto, AccountDeviceDeleteResponseDto>(
                    AppEnvironment.BuildApiUrl(context.BaseUrl, ApiRoutes.AccountDevices),
                    request,
                    headers))
                .ConfigureAwait(false);

            EnsureSuccess(response?.Success ?? false, response?.Error, "The account device could not be deleted.");
        }

        private async Task<T> SendAsync<T>(string desktopDeviceId, Func<Task<T>> send)
        {
            try
            {
                return await send().ConfigureAwait(false);
            }
            catch (ApiRequestException ex)
            {
                if (_authenticationService.TryHandleDeviceRemoved(ex, desktopDeviceId))
                {
                    throw new InvalidOperationException(AuthenticationService.DeviceRemovedMessage, ex);
                }

                throw;
            }
        }

        private static AccountDeviceRequestContext CreateRequestContext(
            string baseUrl,
            string accountId,
            string desktopDeviceId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base url must not be empty.", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id must not be empty.", nameof(accountId));
            }

            if (string.IsNullOrWhiteSpace(desktopDeviceId))
            {
                throw new ArgumentException("Desktop device id must not be empty.", nameof(desktopDeviceId));
            }

            return new AccountDeviceRequestContext(baseUrl.TrimEnd('/'), accountId, desktopDeviceId);
        }

        private static AccountDeviceRequestContext CreateActiveNamespaceRequestContext()
        {
            return CreateRequestContext(
                AppEnvironment.GetActiveNamespaceUrl(),
                AppEnvironment.GetActiveNamespaceAccountId(),
                AppEnvironment.GetActiveNamespaceDesktopDeviceId());
        }

        private static AccountDevicesRequestDto CreateRequest(AccountDeviceRequestContext context)
        {
            return new AccountDevicesRequestDto
            {
                AccountId = context.AccountId,
                DesktopDeviceId = context.DesktopDeviceId,
                Nonce = NonceGenerator.Create()
            };
        }

        private static AccountDeviceDeleteRequestDto CreateDeleteRequest(AccountDeviceRequestContext context, AccountDeviceDto device)
        {
            AccountDevicesRequestDto activeDevice = CreateRequest(context);
            string? targetDesktopDeviceId = string.IsNullOrWhiteSpace(device.DesktopDeviceId)
                ? null
                : device.DesktopDeviceId;
            string? targetMobileDeviceId = targetDesktopDeviceId == null && !string.IsNullOrWhiteSpace(device.MobileDeviceId)
                ? device.MobileDeviceId
                : null;

            if (targetDesktopDeviceId == null && targetMobileDeviceId == null)
            {
                throw new InvalidOperationException("The selected device does not contain a deletable identifier.");
            }

            return new AccountDeviceDeleteRequestDto
            {
                AccountId = activeDevice.AccountId,
                DesktopDeviceId = activeDevice.DesktopDeviceId,
                TargetMobileDeviceId = targetMobileDeviceId,
                TargetDesktopDeviceId = targetDesktopDeviceId,
                Nonce = activeDevice.Nonce
            };
        }

        private void EnsureAuthorizedForActiveNamespace()
        {
            if (!_authenticationService.IsAuthorizedForActiveNamespace)
            {
                throw new InvalidOperationException("Log in to the active server before managing devices.");
            }
        }

        private static void EnsureSuccess(bool success, string? error, string fallbackError)
        {
            if (!success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? fallbackError : error);
            }
        }
    }
}
