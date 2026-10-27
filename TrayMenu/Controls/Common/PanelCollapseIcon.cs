using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class PanelCollapseIcon : Label
    {
        private bool _collapsed = true;
        private bool _hovered;

        internal bool Collapsed
        {
            get => _collapsed;
            set
            {
                _collapsed = value;
                AccessibleName = value ? "Show credential list" : "Hide credential list";
                Invalidate();
            }
        }

        public PanelCollapseIcon()
        {
            Size = new Size(28, 28);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Collapsed = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color color = _hovered ? Color.FromArgb(190, 245, 122) : Color.FromArgb(120, 205, 0);
            using var pen = new Pen(color, 1.5F);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLine(pen, 0, 5, 0, 23);
            Point[] chevron = Collapsed
                ? new[] { new Point(6, 7), new Point(12, 14), new Point(6, 21) }
                : new[] { new Point(12, 7), new Point(6, 14), new Point(12, 21) };
            e.Graphics.DrawLines(pen, chevron);
        }
    }
}