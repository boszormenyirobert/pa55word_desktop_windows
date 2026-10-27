using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class SettingsControl : UserControl
    {
        internal FlowLayoutPanel NavigationPanel { get; } = new();
        internal Label OverviewLink { get; } = new();
        internal Label ProfileLink { get; } = new();
        internal Label ServersLink { get; } = new();
        internal Label DevicesLink { get; } = new();
        internal Label RecoveryLink { get; } = new();
        internal RichTextBox DescriptionBox { get; } = new();
        internal Panel RecoveryPanel { get; } = new();
        internal Label RecoveryProfileLabel { get; } = new();
        internal TrayDropdown RecoveryProfileComboBox { get; } = new();
        internal Label RecoveryStatusLabel { get; } = new();
        internal Button RecoveryOpenButton { get; } = new();
        internal RichTextBox RecoveryDescriptionBox { get; } = new();
        internal event EventHandler<SettingsNavigationRequest>? NavigationRequested;
        internal event EventHandler? RecoveryProfileChanged;
        internal event EventHandler? RecoveryOpenRequested;

        internal void ApplyState(SettingsViewState state)
        {
            Visible = state.NavigationVisible;
            DescriptionBox.Visible = state.OverviewVisible;
            RecoveryPanel.Visible = state.RecoveryVisible;
            if (state.OverviewVisible)
            {
                DescriptionBox.Text =
                    (string.IsNullOrWhiteSpace(state.AccessMessage) ? string.Empty : state.AccessMessage + "\n\n") +
                    $"{state.Description}\n\n" +
                    "Overview provides a summary of the available settings.\n\n" +
                    "Profile: manage the active profile and related actions.\n\n" +
                    "Manage Databases: add, synchronize, and manage database connections.\n\n" +
                    "Devices: review and manage registered account devices.\n\n" +
                    "Recovery: learn how to restore credential access when you no longer have your registered mobile phone.";
            }
        }

        internal void SetLinkState(TrayMenuSection section, bool available, bool active)
        {
            Label label = section switch
            {
                TrayMenuSection.Settings => OverviewLink,
                TrayMenuSection.SettingsUsers => ProfileLink,
                TrayMenuSection.SettingsServers => ServersLink,
                TrayMenuSection.Devices => DevicesLink,
                TrayMenuSection.Recovery => RecoveryLink,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
            label.ForeColor = !available ? Color.FromArgb(119, 122, 151)
                : active ? Color.FromArgb(120, 205, 0) : Color.White;
            label.Cursor = available ? Cursors.Hand : Cursors.Default;
            label.Enabled = section != TrayMenuSection.Recovery || available;
        }

        public SettingsControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            SuspendLayout();

            RecoveryPanel.BackColor = Color.Transparent;
            RecoveryPanel.Location = new Point(0, 38);
            RecoveryPanel.Size = new Size(578, 352);
            RecoveryPanel.Visible = false;

            RecoveryProfileLabel.AutoSize = true;
            RecoveryProfileLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            RecoveryProfileLabel.ForeColor = Color.FromArgb(171, 174, 211);
            RecoveryProfileLabel.Location = new Point(0, 0);
            RecoveryProfileLabel.Text = "Select profile";

            TrayControlStyle.ConfigureComboBox(RecoveryProfileComboBox, 9.5F, Color.FromArgb(31, 33, 57));
            RecoveryProfileComboBox.Location = new Point(0, 24);
            RecoveryProfileComboBox.Size = new Size(544, 25);
            RecoveryProfileComboBox.SelectedIndexChanged += (_, args) => RecoveryProfileChanged?.Invoke(this, args);

            RecoveryStatusLabel.AutoSize = false;
            RecoveryStatusLabel.Font = new Font("Segoe UI", 9F);
            RecoveryStatusLabel.ForeColor = Color.FromArgb(220, 222, 241);
            RecoveryStatusLabel.Location = new Point(0, 58);
            RecoveryStatusLabel.Size = new Size(544, 36);
            RecoveryStatusLabel.Visible = false;

            TrayControlStyle.ConfigureSurfaceButton(RecoveryOpenButton, Color.FromArgb(46, 48, 77), Color.White);
            RecoveryOpenButton.Text = "Open recovery";
            RecoveryOpenButton.Location = new Point(0, 58);
            RecoveryOpenButton.Size = new Size(180, 32);
            RecoveryOpenButton.Visible = false;
            RecoveryOpenButton.Click += (_, args) => RecoveryOpenRequested?.Invoke(this, args);

            RecoveryDescriptionBox.BackColor = Color.FromArgb(31, 33, 57);
            RecoveryDescriptionBox.BorderStyle = BorderStyle.None;
            RecoveryDescriptionBox.Cursor = Cursors.Default;
            RecoveryDescriptionBox.Font = new Font("Segoe UI", 9.5F);
            RecoveryDescriptionBox.ForeColor = Color.FromArgb(220, 222, 241);
            RecoveryDescriptionBox.Location = new Point(0, 100);
            RecoveryDescriptionBox.ReadOnly = true;
            RecoveryDescriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            RecoveryDescriptionBox.Size = new Size(578, 252);
            RecoveryDescriptionBox.TabStop = false;
            RecoveryDescriptionBox.Visible = false;

            RecoveryPanel.Controls.Add(RecoveryProfileLabel);
            RecoveryPanel.Controls.Add(RecoveryProfileComboBox);
            RecoveryPanel.Controls.Add(RecoveryStatusLabel);
            RecoveryPanel.Controls.Add(RecoveryDescriptionBox);
            RecoveryPanel.Controls.Add(RecoveryOpenButton);
        
            this.BackColor = Color.Transparent;
            this.Location = new Point(32, 152);
            this.Size = new Size(578, 390);
            this.Visible = false;

            NavigationPanel.AutoSize = true;
            NavigationPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            NavigationPanel.BackColor = Color.Transparent;
            NavigationPanel.FlowDirection = FlowDirection.LeftToRight;
            NavigationPanel.Location = Point.Empty;
            NavigationPanel.Margin = Padding.Empty;
            NavigationPanel.Padding = Padding.Empty;
            NavigationPanel.WrapContents = false;

            ConfigureSettingsOverviewLink(OverviewLink, "Overview", TrayMenuSection.Settings);
            ConfigureSettingsOverviewLink(ProfileLink, "Profile", TrayMenuSection.SettingsUsers);
            ConfigureSettingsOverviewLink(ServersLink, "Manage Databases", TrayMenuSection.SettingsServers);
            ConfigureSettingsOverviewLink(DevicesLink, "Devices", TrayMenuSection.Devices);
            ConfigureSettingsOverviewLink(RecoveryLink, "Recovery", TrayMenuSection.Recovery);

            NavigationPanel.Controls.Add(OverviewLink);
            NavigationPanel.Controls.Add(ProfileLink);
            NavigationPanel.Controls.Add(ServersLink);
            NavigationPanel.Controls.Add(DevicesLink);
            NavigationPanel.Controls.Add(RecoveryLink);


            DescriptionBox.BackColor = Color.FromArgb(31, 33, 57);
            DescriptionBox.BorderStyle = BorderStyle.None;
            DescriptionBox.Cursor = Cursors.Default;
            DescriptionBox.Font = new Font("Segoe UI", 9.5F);
            DescriptionBox.ForeColor = Color.FromArgb(220, 222, 241);
            DescriptionBox.Location = new Point(0, 38);
            DescriptionBox.ReadOnly = true;
            DescriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            DescriptionBox.Size = new Size(578, 337);
            DescriptionBox.TabStop = false;

            this.Controls.Add(NavigationPanel);
            this.Controls.Add(DescriptionBox);
            this.Controls.Add(RecoveryPanel);
        
            ResumeLayout(false);
        }

        private void ConfigureSettingsOverviewLink(Label label, string text, TrayMenuSection section)
        {
            label.AutoSize = true;
            label.Cursor = Cursors.Hand;
            label.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            label.Margin = new Padding(0, 0, 20, 0);
            label.Text = text;
            label.Tag = section;
            label.Click += (_, _) => NavigationRequested?.Invoke(this, new SettingsNavigationRequest(section));
        }

        internal void LayoutContent(int availableHeight, bool fullHeight)
        {
            const int bottomMargin = 15;
            if (!fullHeight)
            {
                this.Height = NavigationPanel.Height;
                return;
            }

            int overviewHeight = Math.Max(
                220,
                availableHeight - this.Top - bottomMargin);

            this.Height = overviewHeight;
            DescriptionBox.Height = overviewHeight - DescriptionBox.Top;
            RecoveryPanel.Height = overviewHeight - RecoveryPanel.Top;
            RecoveryDescriptionBox.Height =
                RecoveryPanel.Height - RecoveryDescriptionBox.Top;
        }
    }

    internal sealed record SettingsViewState(bool NavigationVisible, bool OverviewVisible, bool RecoveryVisible,
        string Description, string AccessMessage);

    internal sealed class SettingsNavigationRequest(TrayMenuSection section) : EventArgs
    {
        internal TrayMenuSection Section { get; } = section;
    }
}
