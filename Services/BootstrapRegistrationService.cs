using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    public class BootstrapRegistrationService
    {
        private readonly RegistrationConfigStore _configStore = new();
        private readonly CancellationTokenSource _registrationCts = new();

        public event Action? RegistrationCompleted;
        public event Action<string>? RegistrationFailed;

        public Task<bool> LoadOrCreateConfig()
        {
            if (_configStore.TryReadRegistration(out _))
            {
                return Task.FromResult(true);
            }

            ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();

            if (!_configStore.FileExists)
            {
                _configStore.SaveBootstrapConfig(bootstrapConfig);
            }

            return Task.FromResult(false);
        }

        public void StartRegistrationPolling()
        {
            _ = Task.Run(() => PollRegistrationApi());
        }

        public string GetBootstrapBaseUrl()
        {
            return _configStore.LoadBootstrapConfig().BaseUrl;
        }

        public async Task PrepareRegistrationQrAsync(string baseUrl)
        {
            ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
            bootstrapConfig.BaseUrl = NormalizeBaseUrl(baseUrl);
            await PrepareBootstrapConfigAndQrAsync(bootstrapConfig).ConfigureAwait(false);
        }

        private async Task PrepareBootstrapConfigAndQrAsync(ConfigModel bootstrapConfig)
        {
            if (string.IsNullOrWhiteSpace(bootstrapConfig.DeviceId))
            {
                bootstrapConfig.DeviceId = GenerateId();
            }

            if (string.IsNullOrWhiteSpace(bootstrapConfig.KeyContainer))
            {
                bootstrapConfig.KeyContainer = "KeyContainer_" + bootstrapConfig.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(bootstrapConfig.PublicKeyDER))
            {
                await Task.Run(() =>
                {
                    new Pki().CreatePrivateKey(bootstrapConfig.KeyContainer);
                    bootstrapConfig.PublicKeyDER =
                        new Pki().GetPublicKeyDerBase64(bootstrapConfig.KeyContainer);
                }).ConfigureAwait(false);
            }

            _configStore.SaveBootstrapConfig(bootstrapConfig);
            AppServices.Qr.GenerateQrCode(CreateJsonToQrCode(bootstrapConfig));
        }

        private async Task<DesktopRegistration?> PollRegistrationApi()
        {
            var httpApiClient = new HttpApiClient();
            ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
            string deviceId = bootstrapConfig.DeviceId;
            DesktopRegistration? registration = null;
            string registrationPayload = JsonSerializer.Serialize(new
            {
                deviceId
            });
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                deviceId,
                registrationPayload,
                "registration polling request");

            while (!_registrationCts.IsCancellationRequested)
            {
                try
                {
                    await httpApiClient.ConnectToSseEndpoint(
                        AppEnvironment.BuildApiUrl(ApiRoutes.RootDevice(deviceId)),
                        headers,
                        message =>
                        {
                            var parsed = Transformer.ToRegistrationResponse(message);

                            bool isValidRegistration =
                                parsed != null &&
                                parsed.Id > 0 &&
                                !string.IsNullOrWhiteSpace(parsed.AccountId) &&
                                !string.IsNullOrWhiteSpace(parsed.DeviceId) &&
                                !string.IsNullOrWhiteSpace(parsed.EncDek);

                            if (!isValidRegistration)
                            {
                                Debug.WriteLine($"SSE ignored: {message}");
                                return;
                            }

                            registration = parsed;
                            _configStore.SaveRegistration(parsed!);
                            RegistrationCompleted?.Invoke();
                            _registrationCts.Cancel();
                        },
                        _registrationCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("SSE polling cancelled.");
                    break;
                }
                catch (HttpRequestException ex) when (IsRetryableRegistrationFailure(ex))
                {
                    Debug.WriteLine($"Registration polling retry: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), _registrationCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Registration polling failed: {ex}");
                    RegistrationFailed?.Invoke(ex.Message);
                    break;
                }
            }

            return registration;
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Base URL must not be empty.");
            }

            return baseUrl.Trim().TrimEnd('/');
        }

        private static bool IsRetryableRegistrationFailure(HttpRequestException exception)
        {
            return exception.Message.Contains("Device not found for signature verification", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("e_40101_2", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateJsonToQrCode(ConfigModel bootstrapConfig)
        {
            var data = new
            {
                deviceId = bootstrapConfig.DeviceId,
                publicKey = bootstrapConfig.PublicKeyDER
            };

            return JsonSerializer.Serialize(data);
        }

        private static string GenerateId(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(
                Enumerable.Range(0, length)
                    .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
                    .ToArray());
        }
    }
}