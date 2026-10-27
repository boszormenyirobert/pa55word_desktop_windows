using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal static class TrayControlStyle
    {
        internal static void DrawDeleteIcon(object? sender, PaintEventArgs eventArgs)
        {
            if (sender is not Button button)
            {
                return;
            }

            using var pen = new Pen(button.ForeColor, 2F);
            Rectangle bounds = button.ClientRectangle;
            int centerX = bounds.Width / 2;
            eventArgs.Graphics.DrawRectangle(pen, centerX - 6, 13, 12, 13);
            eventArgs.Graphics.DrawLine(pen, centerX - 8, 10, centerX + 8, 10);
            eventArgs.Graphics.DrawLine(pen, centerX - 2, 7, centerX + 2, 7);
            eventArgs.Graphics.DrawLine(pen, centerX - 2, 16, centerX - 2, 23);
            eventArgs.Graphics.DrawLine(pen, centerX + 2, 16, centerX + 2, 23);
        }

        private const int TextBoxHorizontalPadding = 6;
        private const int EmSetMargins = 0x00D3;
        private const int EcLeftMargin = 0x0001;
        private const int EcRightMargin = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        internal static void ConfigureTextBox(TextBox textBox, Point location, Size size, bool multiline)
        {
            textBox.Location = location;
            textBox.Size = size;
            textBox.Multiline = multiline;
            textBox.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
            textBox.Font = new Font("Segoe UI", 9.5F);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.FromArgb(41, 43, 71);
            textBox.ForeColor = Color.White;
            ApplyTextBoxHorizontalPadding(textBox, TextBoxHorizontalPadding);
        }

        internal static void ConfigureLabel(Label label, string text, Point location, float fontSize = 8.5F)
        {
            label.AutoSize = true;
            label.Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(171, 174, 211);
            label.Location = location;
            label.Text = text;
        }

        internal static void ApplyTextBoxHorizontalPadding(TextBox textBox, int padding)
        {
            void ApplyMargins()
            {
                int margins = padding | (padding << 16);
                SendMessage(textBox.Handle, EmSetMargins, (IntPtr)(EcLeftMargin | EcRightMargin), (IntPtr)margins);
            }

            if (textBox.IsHandleCreated)
            {
                ApplyMargins();
            }

            textBox.HandleCreated += (_, _) => ApplyMargins();
        }

        internal static void ConfigureEditorComboBox(TrayDropdown comboBox, Point location, Size size)
        {
            ConfigureComboBox(comboBox, 9.5F, Color.FromArgb(41, 43, 71));
            comboBox.Location = location;
            comboBox.Size = size;
        }

        internal static void ConfigureComboBox(TrayDropdown comboBox, float fontSize, Color backColor)
        {
            comboBox.Font = new Font("Segoe UI", fontSize);
            comboBox.BackColor = backColor;
            comboBox.ForeColor = Color.White;
            comboBox.DropDownHeight = 220;
        }

        internal static void ConfigureSurfaceButton(Button button, Color backColor, Color foreColor)
        {
            button.Size = new Size(140, 30);
            button.Margin = new Padding(0, 0, 0, 8);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 12, 0);
            button.UseVisualStyleBackColor = false;
        }

        internal static void ConfigureButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            button.Size = new Size(104, 28);
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(101, 106, 146);
            button.BackColor = Color.FromArgb(67, 72, 106);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += clickHandler;
        }

        internal static void ConfigureInlineActionButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            ConfigureButton(button, text, location, clickHandler);
            button.Size = new Size(152, 28);
        }

        internal static void ConfigureIconButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            button.Size = new Size(22, 25);
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(46, 48, 77);
            button.ForeColor = Color.FromArgb(120, 205, 0);
            button.Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += clickHandler;
        }
    }
}
