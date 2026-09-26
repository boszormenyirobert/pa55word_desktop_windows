using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrayApp
{
    public partial class Form1
    {
        private bool IsQuickPanelOpen()
        {
            return contextMenuStrip1.Visible && _showExpandedTrayMenu;
        }

        private void ShowExpandedTrayMenu()
        {
            if (_quickPanelHost == null)
            {
                return;
            }

            _showExpandedTrayMenu = true;
            ConfigureTrayMenuPresentation();

            if (!_quickPanelPositionPinned)
            {
                _lastTrayMenuScreenPoint = ResolveQuickPanelScreenPoint();
            }

            contextMenuStrip1.Show(_lastTrayMenuScreenPoint);
        }

        private void ShowCompactTrayMenu()
        {
            _showExpandedTrayMenu = false;
            _lastTrayMenuScreenPoint = ResolveCompactTrayMenuScreenPoint();
            contextMenuStrip1.Show(_lastTrayMenuScreenPoint);
        }

        private void CloseQuickPanel()
        {
            _showExpandedTrayMenu = false;

            if (contextMenuStrip1.Visible)
            {
                _explicitQuickPanelCloseRequested = true;
                contextMenuStrip1.Close();
            }
        }

        private Point ResolveQuickPanelScreenPoint()
        {
            return ResolveTrayAnchoredScreenPoint(_quickPanelContainer.Size);
        }

        private void ConfigureQuickPanelDragHandle(Control control)
        {
            control.Cursor = Cursors.Default;
            control.MouseDown += quickPanelContainer_MouseDown;
            control.MouseMove += quickPanelContainer_MouseMove;
            control.MouseUp += quickPanelContainer_MouseUp;
        }

        private void quickPanelContainer_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !IsQuickPanelOpen())
            {
                return;
            }

            _quickPanelDragging = true;
            Point screenPoint = Control.MousePosition;
            _quickPanelDragOffset = new Point(
                screenPoint.X - contextMenuStrip1.Location.X,
                screenPoint.Y - contextMenuStrip1.Location.Y);
        }

        private void quickPanelContainer_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_quickPanelDragging)
            {
                return;
            }

            Point screenPoint = Control.MousePosition;
            _lastTrayMenuScreenPoint = ClampQuickPanelScreenPoint(new Point(
                screenPoint.X - _quickPanelDragOffset.X,
                screenPoint.Y - _quickPanelDragOffset.Y));
            _quickPanelPositionPinned = true;
            contextMenuStrip1.Location = _lastTrayMenuScreenPoint;
        }

        private void quickPanelContainer_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _quickPanelDragging = false;
        }

        private Point ClampQuickPanelScreenPoint(Point screenPoint)
        {
            Rectangle workingArea = Screen.FromPoint(screenPoint).WorkingArea;
            int maximumX = Math.Max(workingArea.Left, workingArea.Right - _quickPanelContainer.Width);
            int maximumY = Math.Max(workingArea.Top, workingArea.Bottom - _quickPanelContainer.Height);

            return new Point(
                Math.Clamp(screenPoint.X, workingArea.Left, maximumX),
                Math.Clamp(screenPoint.Y, workingArea.Top, maximumY));
        }

        private Point ResolveTrayAnchoredScreenPoint(Size contentSize)
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle workingArea = screen.WorkingArea;
            const int margin = 8;

            int x = workingArea.Left + (workingArea.Width - contentSize.Width) / 2;
            int y = workingArea.Top + (workingArea.Height - contentSize.Height) / 2;

            int maximumX = Math.Max(workingArea.Left, workingArea.Right - contentSize.Width - margin);
            int maximumY = Math.Max(workingArea.Top, workingArea.Bottom - contentSize.Height - margin);
            x = Math.Clamp(x, workingArea.Left, maximumX);
            y = Math.Clamp(y, workingArea.Top, maximumY);
            return new Point(x, y);
        }

        private Point ResolveCompactTrayMenuScreenPoint()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle workingArea = screen.WorkingArea;
            const int trayOffset = 40;
            const int compactMenuWidth = 240;
            int x = workingArea.Right - compactMenuWidth - trayOffset;
            int y = workingArea.Bottom - trayOffset;
            return new Point(x, y);
        }

        private sealed class TrayPanelHost : ToolStripControlHost
        {
            private readonly Size _initialSize;

            public TrayPanelHost(Control content)
                : base(content)
            {
                _initialSize = content?.Size ?? new Size(962, 640);

                if (content != null)
                {
                    content.Margin = Padding.Empty;
                    content.Location = Point.Empty;
                }

                AutoSize = false;
                Margin = Padding.Empty;
                Padding = Padding.Empty;
            }

            protected override Size DefaultSize => Control?.Size ?? _initialSize;

            protected override Padding DefaultMargin => Padding.Empty;
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        private sealed class TrayMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder => Color.FromArgb(36, 58, 82);

            public override Color ToolStripDropDownBackground => Color.FromArgb(36, 38, 63);

            public override Color ImageMarginGradientBegin => Color.FromArgb(19, 33, 50);

            public override Color ImageMarginGradientMiddle => Color.FromArgb(19, 33, 50);

            public override Color ImageMarginGradientEnd => Color.FromArgb(19, 33, 50);

            public override Color MenuItemSelected => Color.FromArgb(40, 69, 98);

            public override Color MenuItemBorder => Color.FromArgb(40, 69, 98);

            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(40, 69, 98);

            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(40, 69, 98);

            public override Color MenuItemPressedGradientBegin => Color.FromArgb(53, 104, 147);

            public override Color MenuItemPressedGradientMiddle => Color.FromArgb(53, 104, 147);

            public override Color MenuItemPressedGradientEnd => Color.FromArgb(53, 104, 147);
        }

        private sealed class SettingsHoverMenuRenderer : ToolStripProfessionalRenderer
        {
            public SettingsHoverMenuRenderer(ProfessionalColorTable colorTable)
                : base(colorTable)
            {
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                Rectangle textBounds = e.TextRectangle;
                textBounds.Y += 8;

                TextRenderer.DrawText(
                    e.Graphics,
                    e.Text,
                    e.TextFont,
                    textBounds,
                    e.TextColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
        }

    }
}