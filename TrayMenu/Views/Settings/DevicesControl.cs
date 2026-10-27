using System.Drawing;
using System.Windows.Forms;
using TrayApp.Models;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class DevicesControl : UserControl
    {
        private readonly ToolTip _toolTip = new();
        private readonly Font _guidanceFont = new("Segoe UI", 8.5F);
        private readonly Font _guidanceBoldFont = new("Segoe UI", 8.5F, FontStyle.Bold);
        private string _pendingDeletionKey = string.Empty;
        private bool _deleting;
        private DeviceRenderState? _renderedState;
        private IReadOnlyList<DeviceRowState> _renderedRows = Array.Empty<DeviceRowState>();
        private sealed record DeviceRenderState(bool Loading, bool Loaded, bool HasServers, string Error, string PendingDeletionKey, bool Deleting);
        private sealed record DeviceRowState(string IdentityKey, string SourceUrl, string? DesktopDeviceId,
            string? MobileDeviceId, string? Name, DateTimeOffset? LastUsed, string Namespaces,
            string NamespaceId, string ServerUrl, string AccountId, string SigningDeviceId);
        internal Font TitleFont { get; } = new("Segoe UI", 15F, FontStyle.Bold);
        internal FlowLayoutPanel ListPanel { get; } = new();
        internal Label SourceLabel { get; } = new();
        internal TrayDropdown SourceComboBox { get; } = new();
        internal Panel GuidancePanel { get; } = new();
        internal RichTextBox GuidanceBox { get; } = new();
        internal event EventHandler? SourceChanged;
        internal event EventHandler<DeviceListItem>? DeletionConfirmationRequested;
        internal event EventHandler? DeletionCancelled;
        internal event EventHandler<DeviceListItem>? DeleteRequested;

        public DevicesControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Transparent;
            Location = new Point(32, 190);
            Size = new Size(578, 440);
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Visible = false;
            ListPanel.FlowDirection = FlowDirection.TopDown;
            ListPanel.WrapContents = false;
            ListPanel.AutoScroll = true;
            ListPanel.Location = new Point(32, 152);
            ListPanel.Size = new Size(578, 390);
            ListPanel.Visible = false;
            SourceLabel.AutoSize = true;
            SourceLabel.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            SourceLabel.ForeColor = Color.FromArgb(171, 174, 211);
            SourceLabel.Text = "Server URL";
            SourceLabel.Visible = false;

            TrayControlStyle.ConfigureComboBox(SourceComboBox, 9F, Color.FromArgb(41, 43, 71));
            SourceComboBox.Visible = false;
            SourceComboBox.SelectedIndexChanged += (_, _) => SourceChanged?.Invoke(this, EventArgs.Empty);
        
            ListPanel.Visible = true;
            SourceLabel.Visible = true;
            SourceComboBox.Visible = true;
            ConfigureDevicesGuidanceBox();
            GuidancePanel.Visible = true;
            Controls.Add(ListPanel);
            Controls.Add(SourceLabel);
            Controls.Add(SourceComboBox);
            Controls.Add(GuidancePanel);
            SizeChanged += (_, _) => LayoutContent();
            LayoutContent();
        }

        internal void LayoutContent(int availableHeight)
        {
            Height = Math.Max(0, availableHeight - Top - 15);
            LayoutContent();
        }

        private void LayoutContent()
        {
            int labelTop = GuidancePanel.Bottom + 15;
            SourceLabel.Location = new Point(0, labelTop);
            SourceComboBox.Location = new Point(76, labelTop - 4);
            SourceComboBox.Size = new Size(502, 25);
            ListPanel.Location = new Point(0, SourceComboBox.Bottom + 15);
            ListPanel.Height = Math.Max(0, Height - ListPanel.Top);
        }

        internal void Render(bool loading, bool loaded, bool hasServers, string error,
            IReadOnlyList<DeviceListItem> devices, string pendingDeletionKey, bool deleting)
        {
            var state = new DeviceRenderState(loading, loaded, hasServers, error, pendingDeletionKey, deleting);
            DeviceRowState[] rows = devices.Select(item => new DeviceRowState(
                item.IdentityKey, item.SourceUrl, item.Device.DesktopDeviceId, item.Device.MobileDeviceId,
                item.Device.DeviceName, item.Device.LastTimeUsed,
                string.Join(", ", item.Device.Namespaces.Select(namespaceItem => namespaceItem.Namespace)),
                item.NamespaceItem.NamespaceId, item.NamespaceItem.Url,
                item.NamespaceItem.AccountId, item.NamespaceItem.DesktopDeviceId)).ToArray();
            if (state == _renderedState && _renderedRows.SequenceEqual(rows))
            {
                return;
            }

            Point scroll = ListPanel.AutoScrollPosition;
            ListPanel.SuspendLayout();
            try
            {
                RenderRows(loading, loaded, hasServers, error, devices, pendingDeletionKey, deleting);
                _renderedState = state;
                _renderedRows = rows;
            }
            finally
            {
                ListPanel.ResumeLayout(performLayout: true);
                ListPanel.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            }
        }

        private void RenderRows(bool loading, bool loaded, bool hasServers, string error,
            IReadOnlyList<DeviceListItem> devices, string pendingDeletionKey, bool deleting)
        {
            _pendingDeletionKey = pendingDeletionKey;
            _deleting = deleting;
            while (ListPanel.Controls.Count > 0)
            {
                ListPanel.Controls[0].Dispose();
            }

            if (loading)
            {
                ListPanel.Controls.Add(CreateDeviceListMessage("Loading registered devices..."));
                return;
            }

            if (!loaded)
            {
                ListPanel.Controls.Add(CreateDeviceListMessage("Select Devices to load registered devices from all logged-in servers."));
                return;
            }

            if (!hasServers)
            {
                ListPanel.Controls.Add(CreateDeviceListMessage("Log in to one or more servers to manage devices."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                ListPanel.Controls.Add(CreateDeviceListMessage(error));
            }

            if (devices.Count == 0)
            {
                ListPanel.Controls.Add(CreateDeviceListMessage("No registered devices were found for the selected server scope."));
                return;
            }

            foreach (DeviceListItem device in devices)
            {
                ListPanel.Controls.Add(CreateDeviceRow(device));
            }
        
        }

        private void ConfigureDevicesGuidanceBox()
        {
            const string guidance = "1. Replace your mobile device:\n" +
                "Install the mobile app on the new device, enter your server URL(s), and follow the instructions on the phone.\n\n" +
                "2. Delete a device:\n" +
                "You can delete any mobile or desktop device from any other registered device.\n\n" +
                "3. Keep at least one registered device:\n" +
                "Keep one mobile or desktop device registered to your servers so you can easily add devices to your account.\n\n" +
                "4. Recovery:\n" +
                "If all mobile and desktop devices are deleted from your account, follow the Recovery description.";

            GuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            GuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            GuidancePanel.Location = Point.Empty;
            GuidancePanel.Size = new Size(578, 174);
            GuidancePanel.Visible = false;

            GuidanceBox.BackColor = GuidancePanel.BackColor;
            GuidanceBox.BorderStyle = BorderStyle.None;
            GuidanceBox.Cursor = Cursors.Default;
            GuidanceBox.DetectUrls = false;
            GuidanceBox.Font = _guidanceFont;
            GuidanceBox.ForeColor = Color.FromArgb(220, 222, 241);
            GuidanceBox.Location = new Point(8, 8);
            GuidanceBox.ReadOnly = true;
            GuidanceBox.ScrollBars = RichTextBoxScrollBars.None;
            GuidanceBox.ShortcutsEnabled = false;
            GuidanceBox.Size = new Size(560, 156);
            GuidanceBox.TabStop = false;
            GuidanceBox.Text = guidance;
            GuidancePanel.Controls.Add(GuidanceBox);

            EmphasizeDevicesGuidance("1. Replace your mobile device:");
            EmphasizeDevicesGuidance("2. Delete a device:");
            EmphasizeDevicesGuidance("3. Keep at least one registered device:");
            EmphasizeDevicesGuidance("4. Recovery:");
            EmphasizeDevicesGuidance("Keep one mobile or desktop device registered");
            EmphasizeDevicesGuidance("Recovery description");
            GuidanceBox.Select(0, 0);
        }

        private void EmphasizeDevicesGuidance(string text)
        {
            int startIndex = GuidanceBox.Text.IndexOf(text, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return;
            }

            GuidanceBox.Select(startIndex, text.Length);
            GuidanceBox.SelectionFont = _guidanceBoldFont;
        }

        private static Label CreateDeviceListMessage(string message)
        {
            return new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Margin = new Padding(0),
                Size = new Size(550, 42),
                Text = message,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Control CreateDeviceRow(DeviceListItem item)
        {
            AccountDeviceDto device = item.Device;
            Panel row = new()
            {
                BackColor = Color.FromArgb(46, 48, 77),
                Margin = new Padding(0, 0, 0, 8),
                Size = new Size(550, 76)
            };
            string deviceIdentifier = !string.IsNullOrWhiteSpace(device.DesktopDeviceId)
                ? device.DesktopDeviceId
                : device.MobileDeviceId ?? "Unknown device";
            string deviceName = string.IsNullOrWhiteSpace(device.DeviceName)
                ? deviceIdentifier
                : device.DeviceName;
            string lastUsed = device.LastTimeUsed.HasValue
                ? device.LastTimeUsed.Value.LocalDateTime.ToString("g")
                : "Unknown";
            string namespaceSummary = device.Namespaces.Count == 0
                ? "No linked servers"
                : string.Join(", ", device.Namespaces.Select(item => item.Namespace));

            Label nameLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 10),
                Size = new Size(350, 18),
                Text = deviceName
            };
            Label detailLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 32),
                Size = new Size(350, 18),
                Text = $"{deviceIdentifier} | Last used: {lastUsed}"
            };
            Label namespaceLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 52),
                Size = new Size(350, 18),
                Text = $"{namespaceSummary} | Source: {item.SourceUrl}"
            };
            Panel actionPanel = new()
            {
                BackColor = row.BackColor,
                Location = new Point(374, 18),
                Size = new Size(154, 40)
            };
            string tooltip = $"{deviceIdentifier}{Environment.NewLine}{namespaceSummary}{Environment.NewLine}Source: {item.SourceUrl}";
            _toolTip.SetToolTip(row, tooltip);
            _toolTip.SetToolTip(nameLabel, tooltip);
            _toolTip.SetToolTip(detailLabel, tooltip);
            _toolTip.SetToolTip(namespaceLabel, tooltip);

            row.Controls.Add(nameLabel);
            row.Controls.Add(detailLabel);
            row.Controls.Add(namespaceLabel);
            row.Controls.Add(actionPanel);

            if (string.Equals(_pendingDeletionKey, item.IdentityKey, StringComparison.Ordinal))
            {
                AddDeviceDeletionConfirmation(actionPanel, item);
            }
            else
            {
                Button deleteButton = new()
                {
                    AccessibleName = "Delete device",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(214, 88, 88),
                    Location = new Point(108, 0),
                    Size = new Size(46, 40),
                    UseVisualStyleBackColor = false
                };
                deleteButton.FlatAppearance.BorderSize = 0;
                deleteButton.Paint += TrayControlStyle.DrawDeleteIcon;
                deleteButton.Click += (_, _) =>
                {
                    DeletionConfirmationRequested?.Invoke(this, item);
                };
                actionPanel.Controls.Add(deleteButton);
            }

            return row;
        }

        private void AddDeviceDeletionConfirmation(Panel actionPanel, DeviceListItem item)
        {
            Button cancelButton = new()
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(0, 5),
                Size = new Size(68, 30),
                Text = "Cancel",
                UseVisualStyleBackColor = false
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 104, 143);
            cancelButton.Click += (_, _) =>
            {
                DeletionCancelled?.Invoke(this, EventArgs.Empty);
            };

            Button confirmButton = new()
            {
                Enabled = !_deleting,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(214, 88, 88),
                ForeColor = Color.White,
                Location = new Point(76, 5),
                Size = new Size(76, 30),
                Text = _deleting ? "Deleting" : "Delete",
                UseVisualStyleBackColor = false
            };
            confirmButton.FlatAppearance.BorderSize = 0;
            confirmButton.Click += (_, _) => DeleteRequested?.Invoke(this, item);

            actionPanel.Controls.Add(cancelButton);
            actionPanel.Controls.Add(confirmButton);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
                TitleFont.Dispose();
                _guidanceFont.Dispose();
                _guidanceBoldFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DeviceListItem
    {
        public AccountDeviceDto Device { get; init; } = new();

        public NamespaceItemDto NamespaceItem { get; init; } = new();

        public string SourceUrl { get; init; } = string.Empty;

        public string IdentityKey { get; init; } = string.Empty;
    }
}
