using System.Drawing;
using System.Windows.Forms;
using TrayApp.Models;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class AccountsControl : UserControl
    {
        private readonly RichTextBox _guidanceBox = new();
        private readonly ToolTip _toolTip = new();
        internal Panel GuidancePanel { get; } = new();
        internal FlowLayoutPanel ListPanel { get; } = new();
        internal event EventHandler<AccountRemovalRequest>? RemoveRequested;

        public AccountsControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Transparent;
            Bounds = new Rectangle(32, 190, 578, 440);
            Visible = false;
            ConfigureAccountsGuidanceBox();
            ListPanel.FlowDirection = FlowDirection.TopDown;
            ListPanel.WrapContents = false;
            ListPanel.AutoScroll = true;
            ListPanel.Bounds = new Rectangle(0, 194, 578, 246);
            Controls.Add(GuidancePanel);
            Controls.Add(ListPanel);
        }

        internal void LayoutContent(int availableHeight)
        {
            Height = Math.Max(0, availableHeight - Top - 15);
            ListPanel.Height = Math.Max(0, Height - ListPanel.Top);
        }

        internal void Render(string userId, IReadOnlyList<NamespaceItemDto> namespaces)
        {
            ListPanel.SuspendLayout();
            try
            {
                while (ListPanel.Controls.Count > 0)
                {
                    ListPanel.Controls[0].Dispose();
                }
                foreach (NamespaceItemDto namespaceItem in namespaces)
                {
                    ListPanel.Controls.Add(CreateAccountRow(userId, namespaceItem));
                }
            }
            finally
            {
                ListPanel.ResumeLayout();
            }
        }

        private void ConfigureAccountsGuidanceBox()
        {
            GuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            GuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            GuidancePanel.Location = Point.Empty;
            GuidancePanel.Size = new Size(578, 174);
            GuidancePanel.Visible = true;

            _guidanceBox.BackColor = GuidancePanel.BackColor;
            _guidanceBox.BorderStyle = BorderStyle.None;
            _guidanceBox.Cursor = Cursors.Default;
            _guidanceBox.DetectUrls = false;
            _guidanceBox.Font = new Font("Segoe UI", 8.5F);
            _guidanceBox.ForeColor = Color.FromArgb(220, 222, 241);
            _guidanceBox.Location = new Point(8, 8);
            _guidanceBox.ReadOnly = true;
            _guidanceBox.ScrollBars = RichTextBoxScrollBars.None;
            _guidanceBox.ShortcutsEnabled = false;
            _guidanceBox.Size = new Size(560, 156);
            _guidanceBox.TabStop = false;
            _guidanceBox.Text = "Deleting an account completely deregisters all registered devices, credentials, and recovery data, including email addresses and phone numbers. All user-related information is permanently deleted and cannot be recovered.";
            GuidancePanel.Controls.Add(_guidanceBox);
        }

        private Control CreateAccountRow(string userId, NamespaceItemDto namespaceItem)
        {
            Panel row = new()
            {
                BackColor = Color.FromArgb(46, 48, 77),
                Margin = new Padding(0, 0, 0, 8),
                Size = new Size(550, 64)
            };
            Label urlLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 10),
                Size = new Size(455, 18),
                Text = namespaceItem.Url
            };
            Label accountLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 34),
                Size = new Size(455, 18),
                Text = namespaceItem.AccountId
            };
            Button deleteButton = new()
            {
                AccessibleName = "Delete account",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(214, 88, 88),
                Location = new Point(486, 14),
                Size = new Size(42, 36),
                UseVisualStyleBackColor = false
            };
            deleteButton.FlatAppearance.BorderSize = 0;
            deleteButton.Paint += TrayControlStyle.DrawDeleteIcon;
            deleteButton.Click += (_, _) => RemoveRequested?.Invoke(this, new AccountRemovalRequest(userId, namespaceItem));
            row.Controls.Add(urlLabel);
            row.Controls.Add(accountLabel);
            row.Controls.Add(deleteButton);
            return row;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class AccountRemovalRequest(string userId, NamespaceItemDto namespaceItem) : EventArgs
    {
        internal string UserId { get; } = userId;
        internal NamespaceItemDto NamespaceItem { get; } = namespaceItem;
    }
}
