using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class LoginControl : UserControl
    {
        private readonly Label _profileLabel = new();
        private readonly Label _serverLabel = new();
        internal TrayDropdown ProfileSelector { get; } = new();
        internal TrayDropdown ServerSelector { get; } = new();
        internal Button LoginButton { get; } = new();
        internal event EventHandler? ProfileChanged;
        internal event EventHandler? ServerChanged;
        internal event EventHandler? LoginRequested;

        public LoginControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(41, 43, 71);
            Location = new Point(32, 152);
            Size = new Size(578, 166);
            Visible = false;
            TrayControlStyle.ConfigureLabel(_profileLabel, "Active profile", new Point(16, 14), fontSize: 9F);
            TrayControlStyle.ConfigureLabel(_serverLabel, "Active server", new Point(16, 72), fontSize: 9F);
            TrayControlStyle.ConfigureComboBox(ProfileSelector, 10F, Color.FromArgb(31, 33, 57));
            TrayControlStyle.ConfigureComboBox(ServerSelector, 10F, Color.FromArgb(31, 33, 57));
            ProfileSelector.Bounds = new Rectangle(16, 36, 544, 25);
            ServerSelector.Bounds = new Rectangle(16, 92, 544, 25);
            ProfileSelector.SelectedIndexChanged += (_, _) => ProfileChanged?.Invoke(this, EventArgs.Empty);
            ServerSelector.SelectedIndexChanged += (_, _) => ServerChanged?.Invoke(this, EventArgs.Empty);
            TrayControlStyle.ConfigureInlineActionButton(LoginButton, "Login", new Point(438, 132),
                (_, _) => LoginRequested?.Invoke(this, EventArgs.Empty));
            LoginButton.Size = new Size(122, 28);
            Controls.AddRange(new Control[] { _profileLabel, ProfileSelector, _serverLabel, ServerSelector, LoginButton });
        }

        internal void ApplyState(LoginViewState state)
        {
            _profileLabel.Visible = state.ShowProfileSelector;
            ProfileSelector.Visible = state.ShowProfileSelector;
            _serverLabel.Location = new Point(16, state.ShowProfileSelector ? 72 : 14);
            ServerSelector.Location = new Point(16, state.ShowProfileSelector ? 92 : 36);
            LoginButton.Location = new Point(438, state.ShowProfileSelector ? 132 : 72);
            Height = state.ShowProfileSelector ? 166 : 108;
            ProfileSelector.Enabled = state.CanSelectProfile;
            ProfileSelector.Cursor = state.CanSelectProfile ? Cursors.Hand : Cursors.Default;
            LoginButton.Enabled = state.CanLogin || state.CanLogout;
            LoginButton.Text = state.Busy
                ? state.Authorized ? "Logging out..." : "Logging in..."
                : state.Authorized ? "Logout" : "Login";
            Color back = state.Busy ? Color.FromArgb(46, 48, 77)
                : state.CanLogin ? Color.FromArgb(120, 205, 0)
                : state.CanLogout ? Color.FromArgb(214, 88, 88)
                : Color.FromArgb(46, 48, 77);
            LoginButton.BackColor = back;
            LoginButton.ForeColor = !state.Busy && state.CanLogin ? Color.FromArgb(24, 30, 24) : Color.White;
            LoginButton.FlatAppearance.BorderColor = back;
        }
    }

    internal sealed record LoginViewState(
        bool ShowProfileSelector, bool CanSelectProfile, bool Authorized,
        bool Busy, bool CanLogin, bool CanLogout);
}