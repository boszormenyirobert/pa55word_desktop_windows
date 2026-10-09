using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class WindowHeader : Panel
    {
        internal const int HeaderHeight = 44;
        private readonly Form _window;
        private readonly Label _description;
        private readonly Button _minimize;
        private readonly Button _close;

        private sealed class SingleLineLabel : Label
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        public WindowHeader(Form window)
        {
            _window = window;
            Dock = DockStyle.Top;
            Height = HeaderHeight;
            BackColor = Color.FromArgb(31, 33, 57);
            TabStop = false;

            var logo = new Pa55wordBrand
            {
                Size = new Size(140, 34),
                Location = new Point(12, (HeaderHeight - 1 - 34) / 2)
            };
            _description = new SingleLineLabel
            {
                AutoSize = false,
                Location = new Point(160, (HeaderHeight - 1 - 20) / 2),
                Height = 20,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(247, 248, 250),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _minimize = CreateButton("_", "Minimize", () => window.WindowState = FormWindowState.Minimized);
            _close = CreateButton("X", "Close", window.Close);

            Controls.Add(logo);
            Controls.Add(_description);
            Controls.Add(_minimize);
            Controls.Add(_close);
            Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.White,
                TabStop = false
            });

            MouseDown += DragWindow;
            _description.MouseDown += DragWindow;
            Resize += (_, _) => LayoutButtons();
            Configure(registration: false);
        }

        public void Configure(bool registration)
        {
            _description.Text = registration
                ? "INVICTUS MANEO I Remain Unconquered Registration"
                : "INVICTUS MANEO I Remain Unconquered";
            _minimize.Enabled = _window.MinimizeBox;
            LayoutButtons();
        }

        private Button CreateButton(string text, string accessibleName, Action action)
        {
            var button = new Button
            {
                Text = text,
                AccessibleName = accessibleName,
                Size = new Size(44, HeaderHeight - 1),
                FlatStyle = FlatStyle.Flat,
                BackColor = BackColor,
                ForeColor = Color.White,
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = text == "X"
                ? Color.FromArgb(196, 43, 28) : Color.FromArgb(59, 61, 88);
            button.Click += (_, _) => action();
            return button;
        }

        private void LayoutButtons()
        {
            int right = ClientSize.Width;
            _close.Location = new Point(right - _close.Width, 0);
            right = _close.Left;
            _minimize.Location = new Point(right - _minimize.Width, 0);
            _description.Width = Math.Max(0, _minimize.Left - _description.Left - 8);
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 1)
            {
                ReleaseCapture();
                SendMessage(_window.Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            }
        }
    }
}
