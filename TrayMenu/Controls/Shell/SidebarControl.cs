using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class SidebarControl : UserControl
    {
        private IReadOnlyList<TrayServerStatus> _renderedServerStatuses = Array.Empty<TrayServerStatus>();
        internal Label EyebrowLabel { get; } = new();
        internal Label ProfileLabel { get; } = new();
        internal Label StatusLabel { get; } = new();
        internal Label SummaryLabel { get; } = new();
        internal FlowLayoutPanel ServerStatusPanel { get; } = new();
        internal Label MenuSectionLabel { get; } = new();
        internal FlowLayoutPanel NavigationPanel { get; } = new();
        internal Button LoginNavButton { get; } = new();
        internal Button CredentialsNavButton { get; } = new();
        internal Button SettingsNavButton { get; } = new();
        internal Button FaqNavButton { get; } = new();
        internal Label ActionSectionLabel { get; } = new();
        internal FlowLayoutPanel ActionPanel { get; } = new();
        internal Button PrimaryActionButton { get; } = new();
        internal Button RefreshButton { get; } = new();
        internal event EventHandler? SettingsRequested;
        internal event EventHandler? PrimaryRequested;
        internal event EventHandler? RefreshRequested;
        internal event EventHandler<SidebarServerSelection>? ServerSelected;

        internal void ApplyIdentity(SidebarIdentityState state)
        {
            ProfileLabel.Text = $"Profile: {state.ProfileName}";
            StatusLabel.Text = state.Status;
            StatusLabel.BackColor = state.StatusBackColor;
            StatusLabel.ForeColor = state.StatusForeColor;
            SummaryLabel.Text = state.Summary;
        }

        internal void ApplyNavigation(SidebarNavigationState state)
        {
            CredentialsNavButton.Visible = state.Registered;
            SettingsNavButton.Visible = state.Registered;
            CredentialsNavButton.Enabled = state.CredentialsAvailable;
            CredentialsNavButton.Cursor = state.CredentialsAvailable ? Cursors.Hand : Cursors.Default;
            LoginNavButton.Enabled = true;
            LoginNavButton.Cursor = Cursors.Hand;
            SetNavigationState(LoginNavButton, state.ActiveSection == TrayMenuSection.Login);
            SetNavigationState(CredentialsNavButton, state.ActiveSection == TrayMenuSection.Credentials);
            SetNavigationState(SettingsNavButton, state.SettingsActive);
            SetNavigationState(FaqNavButton, state.ActiveSection == TrayMenuSection.Faq);
            LayoutContent();
        }

        internal void ApplyActions(SidebarActionState state)
        {
            bool canLogout = state.Registered && state.Authorized;
            PrimaryActionButton.Text = canLogout ? "Logout" : "Login";
            PrimaryActionButton.Visible = canLogout;
            PrimaryActionButton.Enabled = canLogout && !state.Authenticating;
            RefreshButton.Enabled = state.Registered && !state.Refreshing;
            LoginNavButton.Enabled = true;
            LoginNavButton.Cursor = Cursors.Hand;
            Color back = canLogout && !state.Authenticating
                ? Color.FromArgb(214, 88, 88) : Color.FromArgb(46, 48, 77);
            PrimaryActionButton.BackColor = back;
            PrimaryActionButton.ForeColor = Color.White;
            PrimaryActionButton.FlatAppearance.BorderColor = back;
        }

        public SidebarControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            this.BackColor = Color.FromArgb(36, 38, 63);
            this.Location = new Point(0, 0);
            this.Size = new Size(176, 760);
            this.Dock = DockStyle.Left;

            EyebrowLabel.AutoSize = true;
            EyebrowLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            EyebrowLabel.ForeColor = Color.FromArgb(171, 174, 211);
            EyebrowLabel.Location = new Point(18, 16);
            EyebrowLabel.Text = "MENU";

            ProfileLabel.AutoEllipsis = true;
            ProfileLabel.AutoSize = false;
            ProfileLabel.Font = new Font("Segoe UI", 8F);
            ProfileLabel.ForeColor = Color.FromArgb(171, 174, 211);
            ProfileLabel.Location = new Point(18, 36);
            ProfileLabel.Size = new Size(140, 18);
            ProfileLabel.Text = "Profile: Not registered";

            StatusLabel.AutoSize = false;
            StatusLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            StatusLabel.Location = new Point(18, 58);
            StatusLabel.Size = new Size(92, 24);
            StatusLabel.TextAlign = ContentAlignment.MiddleCenter;

            SummaryLabel.AutoSize = false;
            SummaryLabel.Font = new Font("Segoe UI", 8.5F);
            SummaryLabel.ForeColor = Color.FromArgb(171, 174, 211);
            SummaryLabel.Location = new Point(18, 92);
            SummaryLabel.Size = new Size(140, 56);

            ServerStatusPanel.FlowDirection = FlowDirection.TopDown;
            ServerStatusPanel.WrapContents = false;
            ServerStatusPanel.AutoSize = true;
            ServerStatusPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ServerStatusPanel.BackColor = Color.Transparent;
            ServerStatusPanel.Location = new Point(18, 58);
            ServerStatusPanel.Size = new Size(140, 88);
            ServerStatusPanel.MinimumSize = new Size(140, 0);
            ServerStatusPanel.MaximumSize = new Size(140, 1000);
            ServerStatusPanel.Margin = new Padding(0);

            StatusLabel.Visible = false;
            SummaryLabel.Visible = false;

            MenuSectionLabel.AutoSize = true;
            MenuSectionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            MenuSectionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            MenuSectionLabel.Location = new Point(18, 204);
            MenuSectionLabel.Text = "Views";

            NavigationPanel.FlowDirection = FlowDirection.TopDown;
            NavigationPanel.WrapContents = false;
            NavigationPanel.AutoSize = true;
            NavigationPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            NavigationPanel.BackColor = Color.Transparent;
            NavigationPanel.Location = new Point(18, 228);
            NavigationPanel.Size = new Size(140, 172);
            NavigationPanel.MinimumSize = new Size(140, 0);
            NavigationPanel.MaximumSize = new Size(140, 1000);
            NavigationPanel.Margin = new Padding(0);

            ConfigureQuickPanelNavigationButton(LoginNavButton, "Login/Logout");
            ConfigureQuickPanelNavigationButton(CredentialsNavButton, "Credentials");
            ConfigureQuickPanelNavigationButton(SettingsNavButton, "Settings");
            ConfigureQuickPanelNavigationButton(FaqNavButton, "FAQ");
            SettingsNavButton.Click += (_, args) => SettingsRequested?.Invoke(this, args);

            NavigationPanel.Controls.Add(LoginNavButton);
            NavigationPanel.Controls.Add(CredentialsNavButton);
            NavigationPanel.Controls.Add(SettingsNavButton);
            NavigationPanel.Controls.Add(FaqNavButton);


            ActionSectionLabel.AutoSize = true;
            ActionSectionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            ActionSectionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            ActionSectionLabel.Location = new Point(18, 492);
            ActionSectionLabel.Text = "Actions";

            ActionPanel.FlowDirection = FlowDirection.TopDown;
            ActionPanel.WrapContents = false;
            ActionPanel.AutoSize = true;
            ActionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ActionPanel.Location = new Point(18, 516);
            ActionPanel.Size = new Size(140, 132);
            ActionPanel.MinimumSize = new Size(140, 0);
            ActionPanel.MaximumSize = new Size(140, 1000);
            ActionPanel.BackColor = Color.Transparent;
            ActionPanel.Margin = new Padding(0);

            TrayControlStyle.ConfigureSurfaceButton(PrimaryActionButton, Color.FromArgb(20, 109, 214), Color.White);
            PrimaryActionButton.Click += (_, args) => PrimaryRequested?.Invoke(this, args);

            TrayControlStyle.ConfigureSurfaceButton(RefreshButton, Color.White, Color.FromArgb(24, 34, 46));
            RefreshButton.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 222);
            RefreshButton.Text = "Refresh";
            RefreshButton.Click += (_, args) => RefreshRequested?.Invoke(this, args);

            ActionPanel.Controls.Add(PrimaryActionButton);
            ActionPanel.Controls.Add(RefreshButton);

            this.Controls.Add(EyebrowLabel);
            this.Controls.Add(ProfileLabel);
            this.Controls.Add(StatusLabel);
            this.Controls.Add(SummaryLabel);
            this.Controls.Add(ServerStatusPanel);
            this.Controls.Add(MenuSectionLabel);
            this.Controls.Add(NavigationPanel);
            this.Controls.Add(ActionSectionLabel);
            this.Controls.Add(ActionPanel);


        }

        internal void LayoutContent()
        {
            const int sectionGap = 15;
            const int labelToPanelGap = 24;

            ApplyPreferredStackPanelHeight(ServerStatusPanel);
            ApplyPreferredStackPanelHeight(NavigationPanel);
            ApplyPreferredStackPanelHeight(ActionPanel);

            MenuSectionLabel.Location = new Point(18, ServerStatusPanel.Bottom + sectionGap);
            NavigationPanel.Location = new Point(18, MenuSectionLabel.Bottom + (labelToPanelGap - MenuSectionLabel.Height));
            ActionSectionLabel.Location = new Point(18, NavigationPanel.Bottom + sectionGap);
            ActionPanel.Location = new Point(18, ActionSectionLabel.Bottom + (labelToPanelGap - ActionSectionLabel.Height));
        }

        private void ConfigureQuickPanelNavigationButton(Button button, string text)
        {
            button.Size = new Size(140, 34);
            button.Margin = new Padding(0, 0, 0, 8);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            button.BackColor = Color.FromArgb(46, 48, 77);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 12, 0);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
        }

        internal static void SetNavigationState(Button button, bool isActive)
        {
            if (!button.Enabled)
            {
                button.BackColor = Color.FromArgb(61, 63, 89);
                button.ForeColor = Color.FromArgb(141, 144, 176);
                button.FlatAppearance.BorderColor = Color.FromArgb(61, 63, 89);
                return;
            }

            button.BackColor = isActive ? Color.FromArgb(120, 205, 0) : Color.FromArgb(46, 48, 77);
            button.ForeColor = isActive ? Color.Black : Color.White;
            button.FlatAppearance.BorderColor = isActive ? Color.FromArgb(120, 205, 0) : Color.FromArgb(46, 48, 77);
        }

        private static void ApplyPreferredStackPanelHeight(FlowLayoutPanel panel)
        {
            panel.PerformLayout();
            int preferredHeight = panel.GetPreferredSize(new Size(panel.Width, 0)).Height;
            panel.Size = new Size(panel.Width, Math.Max(0, preferredHeight));
        }

        private Control CreateQuickPanelServerStatusRow(string namespaceId, string serverName, bool isConnected, bool isActive)
        {
            Panel rowPanel = new()
            {
                Size = new Size(140, 24),
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };

            Panel statusIndicator = new()
            {
                Size = new Size(10, 10),
                Location = new Point(0, 7),
                BackColor = isConnected ? Color.FromArgb(120, 205, 0) : Color.FromArgb(239, 68, 68)
            };

            Label serverNameLabel = new()
            {
                AutoSize = false,
                Location = new Point(18, 2),
                Size = new Size(94, 18),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Text = serverName,
                AutoEllipsis = true
            };

            Label activeLabel = new()
            {
                AutoSize = false,
                Location = new Point(114, 2),
                Size = new Size(26, 18),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 205, 0),
                Text = isActive ? "ACT" : string.Empty,
                TextAlign = ContentAlignment.MiddleRight
            };

            rowPanel.Controls.Add(statusIndicator);
            rowPanel.Controls.Add(serverNameLabel);
            rowPanel.Controls.Add(activeLabel);

            if (!string.IsNullOrWhiteSpace(namespaceId))
            {
                EventHandler selectServer = (_, _) => ServerSelected?.Invoke(this, new SidebarServerSelection(namespaceId));
                rowPanel.Cursor = Cursors.Hand;
                statusIndicator.Cursor = Cursors.Hand;
                serverNameLabel.Cursor = Cursors.Hand;
                activeLabel.Cursor = Cursors.Hand;
                rowPanel.Click += selectServer;
                statusIndicator.Click += selectServer;
                serverNameLabel.Click += selectServer;
                activeLabel.Click += selectServer;
            }

            return rowPanel;
        }

        internal void RenderServers(IReadOnlyList<TrayServerStatus> serverStatuses)
        {
            if (_renderedServerStatuses.SequenceEqual(serverStatuses))
            {
                return;
            }

            ServerStatusPanel.SuspendLayout();

            try
            {
                while (ServerStatusPanel.Controls.Count > 0)
                {
                    ServerStatusPanel.Controls[0].Dispose();
                }

                foreach (TrayServerStatus serverStatus in serverStatuses)
                {
                    ServerStatusPanel.Controls.Add(CreateQuickPanelServerStatusRow(
                        serverStatus.NamespaceId,
                        serverStatus.Name,
                        serverStatus.IsConnected,
                        serverStatus.IsActive));
                }

                _renderedServerStatuses = serverStatuses;
            }
            finally
            {
                ServerStatusPanel.ResumeLayout(performLayout: true);
            }
        }
    }

    internal sealed record SidebarIdentityState(string ProfileName, string Status, Color StatusBackColor,
        Color StatusForeColor, string Summary);
    internal sealed record SidebarNavigationState(bool Registered, bool CredentialsAvailable,
        TrayMenuSection? ActiveSection, bool SettingsActive);
    internal sealed record SidebarActionState(bool Registered, bool Authorized, bool Authenticating, bool Refreshing);
    internal readonly record struct TrayServerStatus(string NamespaceId, string Name, bool IsConnected, bool IsActive);

    internal sealed class SidebarServerSelection(string namespaceId) : EventArgs
    {
        internal string NamespaceId { get; } = namespaceId;
    }
}
