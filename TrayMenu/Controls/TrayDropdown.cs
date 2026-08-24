using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class TrayDropdown : ComboBox
    {
        private const int WmPaint = 0x000F;
        private const int HorizontalTextPadding = 10;
        private static readonly Color BorderColor = Color.FromArgb(59, 61, 88);
        private static readonly Color DisabledTextColor = Color.FromArgb(171, 174, 211);
        private static readonly Color ArrowColor = Color.FromArgb(120, 205, 0);
        private bool _suppressSelectedIndexChanged;

        public TrayDropdown()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            IntegralHeight = false;
            ItemHeight = 22;
            BackColor = Color.FromArgb(31, 33, 57);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

        public new int DropDownHeight
        {
            get => base.DropDownHeight;
            set => base.DropDownHeight = Math.Max(44, value);
        }

        public void SetSelectedIndexSilently(int index)
        {
            if (SelectedIndex == index)
            {
                Invalidate();
                return;
            }

            _suppressSelectedIndexChanged = true;

            try
            {
                SelectedIndex = index;
            }
            finally
            {
                _suppressSelectedIndexChanged = false;
            }

            Invalidate();
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if (_suppressSelectedIndexChanged)
            {
                return;
            }

            base.OnSelectedIndexChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowTheme(Handle, string.Empty, string.Empty);
            Invalidate();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0)
            {
                string selectedText = Text;
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    using var selectedTextBrush = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(171, 174, 211));
                    Font selectedDrawFont = e.Font ?? Font;
                    e.Graphics.DrawString(selectedText, selectedDrawFont, selectedTextBrush, e.Bounds.Left + HorizontalTextPadding, e.Bounds.Top + 3);
                }

                return;
            }

            bool activeItem = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color backColor = Color.FromArgb(31, 33, 57);
            Color foreColor = activeItem ? ArrowColor : Color.White;

            using var backgroundBrush = new SolidBrush(backColor);
            using var textBrush = new SolidBrush(foreColor);
            Font drawFont = e.Font ?? Font;

            e.Graphics.FillRectangle(backgroundBrush, e.Bounds);

            if (e.Index >= 0 && e.Index < Items.Count)
            {
                e.Graphics.DrawString(Items[e.Index]?.ToString() ?? string.Empty, drawFont, textBrush, e.Bounds.Left + HorizontalTextPadding, e.Bounds.Top + 3);
            }

        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmPaint)
            {
                PaintClosedState();
            }
        }

        private void PaintClosedState()
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using Graphics graphics = Graphics.FromHwnd(Handle);
            using var backgroundBrush = new SolidBrush(Enabled ? BackColor : Color.FromArgb(49, 51, 74));
            using var borderPen = new Pen(BorderColor);
            using var textBrush = new SolidBrush(Enabled ? ForeColor : DisabledTextColor);

            graphics.FillRectangle(backgroundBrush, bounds);

            Rectangle arrowBounds = new Rectangle(Math.Max(0, bounds.Width - 28), 1, 27, Math.Max(0, bounds.Height - 2));
            graphics.FillRectangle(backgroundBrush, arrowBounds);

            graphics.DrawRectangle(borderPen, 0, 0, bounds.Width - 1, bounds.Height - 1);

            Rectangle textBounds = new Rectangle(HorizontalTextPadding, 0, Math.Max(0, bounds.Width - (HorizontalTextPadding + 26)), bounds.Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                textBrush.Color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.SingleLine);

            DrawArrow(graphics, bounds);
        }

        private static void DrawArrow(Graphics graphics, Rectangle bounds)
        {
            int arrowCenterX = bounds.Right - 15;
            int arrowCenterY = bounds.Top + (bounds.Height / 2) + 1;

            Point[] arrowPoints =
            {
                new Point(arrowCenterX - 4, arrowCenterY - 2),
                new Point(arrowCenterX, arrowCenterY + 2),
                new Point(arrowCenterX + 4, arrowCenterY - 2)
            };

            using var arrowPen = new Pen(ArrowColor, 2f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };

            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawLines(arrowPen, arrowPoints);
        }
    }
}