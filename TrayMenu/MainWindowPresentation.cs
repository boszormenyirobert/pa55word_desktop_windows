using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TrayApp.Services;
using TrayApp.TrayMenu.Controls;

namespace TrayApp.TrayMenu
{
    internal static class MainWindowPresentation
    {
        internal static readonly Size MinimumClientSize = new(820, 660 + WindowHeader.HeaderHeight);
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;
        private const string WindowTitle = "pa55word INVICTUS MANEO I Remain Unconquered";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr window, int attribute, ref int value, int valueSize);

        private static readonly Lazy<Icon> BrandIcon = new(() =>
        {
            using Stream stream = typeof(MainWindowPresentation).Assembly.GetManifestResourceStream(
                "TrayApp.Assets.pa55word.ico")
                ?? throw new InvalidOperationException("The pa55word window icon resource is missing.");
            return new Icon(stream);
        });

        public static void ApplyBranding(Form window, bool registration = false)
        {
            window.Icon = BrandIcon.Value;
            window.Text = registration ? $"{WindowTitle} Registration" : WindowTitle;
            WindowHeader? header = window.Controls.OfType<WindowHeader>().FirstOrDefault();
            if (header == null)
            {
                header = new WindowHeader(window);
                window.Controls.Add(header);
            }

            header.Configure(registration);
            header.SendToBack();
            if (window.IsHandleCreated)
            {
                ApplyTitleBarTheme(window);
            }
        }

        public static void ApplyTitleBarTheme(Form window)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                return;
            }

            int darkModeAttribute = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
                ? DwmUseImmersiveDarkMode : 19;
            SetTitleBarAttribute(window, darkModeAttribute, 1);

            // Explicit caption colors are supported starting with Windows 11.
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                SetTitleBarAttribute(window, DwmCaptionColor,
                    ColorTranslator.ToWin32(Color.FromArgb(31, 33, 57)));
                SetTitleBarAttribute(window, DwmTextColor,
                    ColorTranslator.ToWin32(Color.FromArgb(247, 248, 250)));
            }
        }

        private static void SetTitleBarAttribute(Form window, int attribute, int value)
        {
            int result = DwmSetWindowAttribute(window.Handle, attribute, ref value, sizeof(int));
            if (result < 0)
            {
                DiagnosticLog.Write($"Unable to apply title bar attribute {attribute}: HRESULT 0x{result:X8}.");
            }
        }

        public static void AttachContent(Form window, Control content)
        {
            content.Dock = DockStyle.Fill;
            window.Controls.Add(content);
            content.BringToFront();
        }

        public static void Configure(Form window, bool showCredentialList = false)
        {
            int minimumWidth = MinimumClientSize.Width +
                (showCredentialList ? CredentialListPanel.PanelWidth : 0);
            Size minimumSize = new(
                minimumWidth + window.Width - window.ClientSize.Width,
                MinimumClientSize.Height + window.Height - window.ClientSize.Height);
            Size clientSize = new(
                Math.Max(window.ClientSize.Width, minimumWidth),
                Math.Max(window.ClientSize.Height, MinimumClientSize.Height));

            if (window.FormBorderStyle != FormBorderStyle.Sizable)
            {
                window.FormBorderStyle = FormBorderStyle.Sizable;
            }

            if (window.MaximizeBox)
            {
                window.MaximizeBox = false;
            }

            if (!window.MinimizeBox)
            {
                window.MinimizeBox = true;
            }

            if (window.MinimumSize != minimumSize)
            {
                window.MinimumSize = minimumSize;
            }

            if (window.ClientSize != clientSize)
            {
                window.ClientSize = clientSize;
            }

            if (!window.ShowInTaskbar)
            {
                window.ShowInTaskbar = true;
            }

            ApplyBranding(window);
        }

        public static void Show(Form window)
        {
            window.Opacity = 1;
            window.ShowInTaskbar = true;
            if (window.WindowState == FormWindowState.Minimized)
            {
                window.WindowState = FormWindowState.Normal;
            }

            window.Show();
            // Windows may apply the launcher's STARTUPINFO show state to the first ShowWindow call.
            if (window.WindowState == FormWindowState.Minimized)
            {
                window.WindowState = FormWindowState.Normal;
            }

            window.BringToFront();
            window.Activate();
        }

        public static void Hide(Form window)
        {
            window.Hide();
            window.ShowInTaskbar = false;
        }

        public static void ConfigureRegistration(Form window)
        {
            window.WindowState = FormWindowState.Normal;
            window.MinimumSize = Size.Empty;
            window.ClientSize = new Size(800, 450);
            window.FormBorderStyle = FormBorderStyle.FixedSingle;
            window.MaximizeBox = false;
            window.MinimizeBox = true;
            window.ShowInTaskbar = true;
            ApplyBranding(window, registration: true);
        }

    }
}
