using QRCoder;
using System.Drawing;

namespace TrayApp.Services
{
    internal class QrService
    {
        private Bitmap? _qrCode;

        public void GenerateQrCode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.");

            _qrCode?.Dispose();

            using var qrGenerator = new QRCodeGenerator();

            using var qrData = qrGenerator.CreateQrCode(
                json,
                QRCodeGenerator.ECCLevel.Q
            );

            using var qrCode = new QRCode(qrData);

            _qrCode = qrCode.GetGraphic(10);
        }


        public Bitmap? GetQrCode()
        {
            return _qrCode;
        }
    }
}