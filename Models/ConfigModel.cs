namespace TrayApp.Models
{
    public class ConfigModel
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string DesktopDeviceId { get; set; } = string.Empty;
        public string DeviceId
        {
            get => DesktopDeviceId;
            set => DesktopDeviceId = value;
        }
        public string AccountId { get; set; } = string.Empty;
        public string KeyContainer { get; set; } = string.Empty;
        public string PublicKeyDER { get; set; } = string.Empty;
        public string EncDek { get; set; } = string.Empty;
    }
}
