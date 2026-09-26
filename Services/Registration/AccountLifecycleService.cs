using System.Net;
using System.Text.Json;
using System.Diagnostics;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class AccountLifecycleService
    {
        private readonly RegistrationConfigStore _configStore = new();
        private readonly HttpApiClient _httpApiClient;

        public AccountLifecycleService(HttpApiClient httpApiClient)
        {
            _httpApiClient = httpApiClient;
        }

        public async Task RemoveDeletedAccountsAsync()
        {
            if (!_configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) || registrations == null)
            {
                return;
            }

            foreach (DesktopRegistration registration in registrations)
            {
                foreach (NamespaceItemDto namespaceItem in registration.Namespaces.ToArray())
                {
                    if (await IsAccountDeletedAsync(namespaceItem).ConfigureAwait(false))
                    {
                        _configStore.RemoveNamespace(registration.UserKey, namespaceItem.NamespaceId);
                    }
                }
            }
        }

        public async Task DeleteAccountAsync(string userId, NamespaceItemDto namespaceItem)
        {
            AccountLifecycleRequestDto request = CreateRequest(namespaceItem);
            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                namespaceItem.DesktopDeviceId,
                payloadJson,
                "account deletion");

            await _httpApiClient.DeleteJsonAsync(
                AppEnvironment.BuildApiUrl(namespaceItem.Url, ApiRoutes.Account),
                request,
                headers).ConfigureAwait(false);

            _configStore.RemoveNamespace(userId, namespaceItem.NamespaceId);
        }

        private async Task<bool> IsAccountDeletedAsync(NamespaceItemDto namespaceItem)
        {
            try
            {
                AccountLifecycleRequestDto request = CreateRequest(namespaceItem);
                string payloadJson = JsonSerializer.Serialize(request);
                IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                    namespaceItem.DesktopDeviceId,
                    payloadJson,
                    "account status check");

                AccountStatusResponseDto? response = await _httpApiClient.PostJsonAsync<AccountLifecycleRequestDto, AccountStatusResponseDto>(
                    AppEnvironment.BuildApiUrl(namespaceItem.Url, ApiRoutes.AccountStatus),
                    request,
                    headers).ConfigureAwait(false);

                return string.Equals(response?.Status, "deleted", StringComparison.OrdinalIgnoreCase);
            }
            catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Gone)
            {
                return true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Account status check failed for '{namespaceItem.Url}': {exception.Message}");
                return false;
            }
        }

        private static AccountLifecycleRequestDto CreateRequest(NamespaceItemDto namespaceItem)
        {
            if (string.IsNullOrWhiteSpace(namespaceItem.AccountId) ||
                string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId) ||
                string.IsNullOrWhiteSpace(namespaceItem.Url))
            {
                throw new InvalidDataException("Namespace does not contain valid account lifecycle credentials.");
            }

            return new AccountLifecycleRequestDto
            {
                AccountId = namespaceItem.AccountId,
                DesktopDeviceId = namespaceItem.DesktopDeviceId,
                Nonce = NonceGenerator.Create()
            };
        }
    }
}