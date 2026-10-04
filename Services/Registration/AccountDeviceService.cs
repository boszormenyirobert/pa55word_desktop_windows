using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AccountDeviceService
    {
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;

        public AccountDeviceService(
            HttpApiClient httpApiClient,
            AuthenticationService authenticationService)
        {
            _httpApiClient = httpApiClient;
            _authenticationService = authenticationService;
        }

        public async Task<IReadOnlyList<AccountDeviceDto>> ReadDevicesAsync()
        {
            AccountDevicesRequestDto request = CreateRequest();
            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                request.DesktopDeviceId,
                payloadJson,
                "account device list request");

            AccountDevicesResponseDto? response = await SendAsync(request.DesktopDeviceId, () => _httpApiClient
                .PostJsonAsync<AccountDevicesRequestDto, AccountDevicesResponseDto>(
                    AppEnvironment.BuildApiUrl(ApiRoutes.AccountDevices),
                    request,
                    headers))
                .ConfigureAwait(false);

            EnsureSuccess(response?.Success ?? false, response?.Error, "The account device list could not be loaded.");
            return response!.Devices;
        }

        public async Task DeleteDeviceAsync(AccountDeviceDto device)
        {
            ArgumentNullException.ThrowIfNull(device);

            AccountDeviceDeleteRequestDto request = CreateDeleteRequest(device);
            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                request.DesktopDeviceId,
                payloadJson,
                "account device deletion request");

            AccountDeviceDeleteResponseDto? response = await SendAsync(request.DesktopDeviceId, () => _httpApiClient
                .DeleteJsonAsync<AccountDeviceDeleteRequestDto, AccountDeviceDeleteResponseDto>(
                    AppEnvironment.BuildApiUrl(ApiRoutes.AccountDevices),
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

        private AccountDevicesRequestDto CreateRequest()
        {
            EnsureAuthorized();
            return new AccountDevicesRequestDto
            {
                AccountId = AppEnvironment.GetActiveNamespaceAccountId(),
                DesktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId(),
                Nonce = NonceGenerator.Create()
            };
        }

        private AccountDeviceDeleteRequestDto CreateDeleteRequest(AccountDeviceDto device)
        {
            AccountDevicesRequestDto activeDevice = CreateRequest();
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

        private void EnsureAuthorized()
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
