using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using TrayApp.Services;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class Pa55wordBrand : UserControl
    {
        private readonly PictureBox _wordmark = new();

        public Pa55wordBrand()
            : this(startInfo => Process.Start(startInfo))
        {
        }

        internal Pa55wordBrand(Action<ProcessStartInfo> openBrowser)
        {
            Size = new Size(140, 34);
            BackColor = Color.Transparent;
            TabStop = false;
            AccessibleName = "pa55word";
            AccessibleDescription = "Open https://pa55word.com in the default browser";
            Cursor = Cursors.Hand;

            using Stream stream = typeof(Pa55wordBrand).Assembly.GetManifestResourceStream(
                "TrayApp.Assets.pa55word-logo.png")
                ?? throw new InvalidOperationException("The pa55word logo resource is missing.");
            using var logo = new Bitmap(stream);
            _wordmark.Image = new Bitmap(logo);
            _wordmark.Location = Point.Empty;
            _wordmark.Dock = DockStyle.Fill;
            _wordmark.SizeMode = PictureBoxSizeMode.Zoom;
            _wordmark.TabStop = false;
            _wordmark.AccessibleName = "pa55word";
            _wordmark.Cursor = Cursors.Hand;
            _wordmark.Click += (_, _) => OpenWebsite(openBrowser);
            Click += (_, _) => OpenWebsite(openBrowser);

            Controls.Add(_wordmark);
        }

        private void OpenWebsite(Action<ProcessStartInfo> openBrowser)
        {
            try
            {
                openBrowser(new ProcessStartInfo("https://pa55word.com")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                DiagnosticLog.Write($"Unable to open pa55word website: {exception}");
                MessageBox.Show(this, $"Unable to open the website: {exception.Message}",
                    "pa55word", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _wordmark.Image?.Dispose();
                _wordmark.Image = null;
            }

            base.Dispose(disposing);
        }
    }
}
