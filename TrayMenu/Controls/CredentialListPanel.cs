using System.Drawing;
using System.Windows.Forms;
using TrayApp.Services;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class CredentialListPanel : Panel
    {
        internal const int PanelWidth = 240;
        private readonly ListBox _entriesList = new();
        private readonly Label _statusLabel = new();
        private IReadOnlyList<CredentialEntryListItem> _entries = Array.Empty<CredentialEntryListItem>();
        private bool _updating;
        private int _hoveredIndex = -1;

        public event Action<CredentialEntryListItem>? EntrySelected;

        public CredentialListPanel()
        {
            Width = PanelWidth;
            Dock = DockStyle.Left;
            Padding = new Padding(12, 32, 12, 15);
            BackColor = Color.FromArgb(41, 43, 71);
            ForeColor = Color.White;

            var titleLabel = new Label
            {
                Text = "All credentials",
                Dock = DockStyle.Top,
                Height = 32,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
            };
            _statusLabel.Dock = DockStyle.Bottom;
            _statusLabel.Height = 64;
            _statusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _statusLabel.Font = new Font("Segoe UI", 9F);
            _entriesList.Dock = DockStyle.Fill;
            _entriesList.IntegralHeight = false;
            _entriesList.HorizontalScrollbar = true;
            _entriesList.BorderStyle = BorderStyle.None;
            _entriesList.DrawMode = DrawMode.OwnerDrawFixed;
            _entriesList.ItemHeight = 34;
            _entriesList.BackColor = BackColor;
            _entriesList.ForeColor = Color.White;
            _entriesList.Font = new Font("Segoe UI", 10F);
            _entriesList.DisplayMember = nameof(CredentialEntryListItem.Label);
            _entriesList.DrawItem += DrawCredentialEntry;
            _entriesList.MouseMove += UpdateHoveredEntry;
            _entriesList.MouseLeave += ClearHoveredEntry;
            _entriesList.SelectedIndexChanged += (_, _) =>
            {
                if (!_updating && _entriesList.SelectedItem is CredentialEntryListItem entry)
                {
                    EntrySelected?.Invoke(entry);
                }
            };
            Controls.Add(_entriesList);
            Controls.Add(_statusLabel);
            Controls.Add(titleLabel);
        }

        private void DrawCredentialEntry(object? sender, DrawItemEventArgs e)
        {
            using var listBackground = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(listBackground, e.Bounds);
            if (e.Index < 0 || e.Index >= _entriesList.Items.Count)
            {
                return;
            }

            TextRenderer.DrawText(
                e.Graphics,
                ((CredentialEntryListItem)_entriesList.Items[e.Index]!).Label,
                _entriesList.Font,
                Rectangle.Inflate(e.Bounds, -8, 0),
                (e.State & DrawItemState.Selected) != 0
                    ? Color.FromArgb(120, 205, 0)
                    : e.Index == _hoveredIndex
                        ? Color.FromArgb(190, 245, 122)
                        : Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (e.Index < _entriesList.Items.Count - 1)
            {
                using var divider = new Pen(Color.FromArgb(78, 81, 112));
                e.Graphics.DrawLine(divider, e.Bounds.Left + 8, e.Bounds.Bottom - 1, e.Bounds.Right - 8, e.Bounds.Bottom - 1);
            }

            if ((e.State & DrawItemState.Focus) != 0)
            {
                e.DrawFocusRectangle();
            }
        }

        private void UpdateHoveredEntry(object? sender, MouseEventArgs e)
        {
            int hoveredIndex = _entriesList.IndexFromPoint(e.Location);
            if (hoveredIndex == _hoveredIndex)
            {
                return;
            }

            int previousIndex = _hoveredIndex;
            _hoveredIndex = hoveredIndex;
            InvalidateEntry(previousIndex);
            InvalidateEntry(_hoveredIndex);
        }

        private void ClearHoveredEntry(object? sender, EventArgs e)
        {
            int previousIndex = _hoveredIndex;
            _hoveredIndex = -1;
            InvalidateEntry(previousIndex);
        }

        private void InvalidateEntry(int index)
        {
            if (index >= 0 && index < _entriesList.Items.Count)
            {
                _entriesList.Invalidate(_entriesList.GetItemRectangle(index));
            }
        }

        public void UpdateEntries(
            IReadOnlyList<CredentialEntryListItem> entries,
            string selectedEntryId,
            bool loading,
            string errorMessage)
        {
            _updating = true;
            _entriesList.BeginUpdate();
            try
            {
                if (!_entries.SequenceEqual(entries))
                {
                    _entriesList.Items.Clear();
                    foreach (CredentialEntryListItem entry in entries)
                    {
                        _entriesList.Items.Add(entry);
                    }
                    _entries = entries.ToArray();
                }

                _entriesList.SelectedIndex = _entriesList.Items
                    .Cast<CredentialEntryListItem>()
                    .Select((entry, index) => (entry, index))
                    .Where(item => string.Equals(item.entry.Id.ToString(), selectedEntryId, StringComparison.Ordinal))
                    .Select(item => item.index)
                    .DefaultIfEmpty(-1)
                    .First();
                _entriesList.Enabled = !loading && string.IsNullOrWhiteSpace(errorMessage);
                _statusLabel.Text = loading
                    ? "Loading credentials..."
                    : !string.IsNullOrWhiteSpace(errorMessage)
                        ? errorMessage
                        : entries.Count == 0 ? "No credentials available." : string.Empty;
                _statusLabel.Visible = !string.IsNullOrWhiteSpace(_statusLabel.Text);
            }
            finally
            {
                _entriesList.EndUpdate();
                _updating = false;
            }
        }
    }
}
