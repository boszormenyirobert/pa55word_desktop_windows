using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrayApp
{
    public partial class Form1
    {
        private bool IsQuickPanelOpen()
        {
            return Visible && _quickPanelContainer.Visible && WindowState != FormWindowState.Minimized;
        }

        private void CloseQuickPanel()
        {
            _quickPanelContainer.Visible = false;
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override bool ProcessDialogKey(Keys keyData)
            {
                if ((keyData & Keys.KeyCode) == Keys.Tab)
                {
                    List<Control> inputControls = GetVisibleInputControls(this);
                    Control? focusedControl = inputControls.FirstOrDefault(control => control.Focused);

                    if (focusedControl != null && inputControls.Count > 1)
                    {
                        bool forward = (keyData & Keys.Shift) != Keys.Shift;
                        int currentIndex = inputControls.IndexOf(focusedControl);
                        int nextIndex = (currentIndex + (forward ? 1 : inputControls.Count - 1)) % inputControls.Count;
                        inputControls[nextIndex].Focus();
                        return true;
                    }
                }

                return base.ProcessDialogKey(keyData);
            }

            private static List<Control> GetVisibleInputControls(Control parent)
            {
                var inputControls = new List<Control>();
                AddVisibleInputControls(parent, inputControls);
                return inputControls
                    .OrderBy(control => control.PointToScreen(Point.Empty).Y)
                    .ThenBy(control => control.PointToScreen(Point.Empty).X)
                    .ToList();
            }

            private static void AddVisibleInputControls(Control parent, ICollection<Control> inputControls)
            {
                foreach (Control child in parent.Controls)
                {
                    if (child.Visible && child.Enabled && child.TabStop && (child is TextBoxBase || child is ComboBox))
                    {
                        inputControls.Add(child);
                    }

                    AddVisibleInputControls(child, inputControls);
                }
            }
        }

    }
}