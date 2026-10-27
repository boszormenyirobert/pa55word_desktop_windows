using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class ManageDatabasesControl : UserControl
    {
        private readonly Font _guidanceFont = new("Segoe UI", 8.5F);
        private readonly Font _guidanceBoldFont = new("Segoe UI", 8.5F, FontStyle.Bold);
        internal Panel ActionPanel { get; } = new();
        internal Panel RegistrationPanel { get; } = new();
        internal Label ActionLabel { get; } = new();
        internal TrayDropdown ActionComboBox { get; } = new();
        internal Label UrlLabel { get; } = new();
        internal TextBox UrlTextBox { get; } = new();
        internal Label NameLabel { get; } = new();
        internal TextBox NameTextBox { get; } = new();
        internal Button SubmitButton { get; } = new();
        internal Button BackButton { get; } = new();
        internal Button RestartButton { get; } = new();
        internal Label StatusLabel { get; } = new();
        internal PictureBox QrPictureBox { get; } = new();
        internal Panel GuidancePanel { get; } = new();
        internal RichTextBox GuidanceBox { get; } = new();
        internal event EventHandler? ActionChanged;
        internal event EventHandler? SubmitRequested;
        internal event EventHandler? BackRequested;
        internal event EventHandler? RestartRequested;

        public ManageDatabasesControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Transparent;
            Location = new Point(32, 152);
            Size = new Size(578, 493);
            Visible = false;
            SuspendLayout();
            ActionLabel.AutoSize = true;
            ActionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            ActionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            ActionLabel.Location = new Point(16, 14);
            ActionLabel.Text = "Database action";
            ActionLabel.Visible = false;

            TrayControlStyle.ConfigureComboBox(ActionComboBox, 10F, Color.FromArgb(31, 33, 57));
            ActionComboBox.Location = new Point(16, 36);
            ActionComboBox.Size = new Size(544, 25);
            ActionComboBox.Visible = false;
            ActionComboBox.SelectedIndexChanged += (_, _) => ActionChanged?.Invoke(this, EventArgs.Empty);

            ConfigureServerRegistrationGuidanceBox();
            TrayControlStyle.ConfigureLabel(UrlLabel, "Connection path", new Point(16, 50));
            TrayControlStyle.ConfigureTextBox(UrlTextBox, new Point(16, 70), new Size(544, 25), multiline: false);

            TrayControlStyle.ConfigureLabel(NameLabel, "Database name", new Point(300, 50));
            TrayControlStyle.ConfigureTextBox(NameTextBox, new Point(300, 70), new Size(260, 25), multiline: false);
            NameLabel.Visible = false;
            NameTextBox.Visible = false;

            TrayControlStyle.ConfigureInlineActionButton(SubmitButton, "Generate QR", new Point(16, 108), (_, _) => SubmitRequested?.Invoke(this, EventArgs.Empty));

            TrayControlStyle.ConfigureInlineActionButton(BackButton, "Back", new Point(16, 12), (_, _) => BackRequested?.Invoke(this, EventArgs.Empty));
            BackButton.Size = new Size(64, 28);
            BackButton.Visible = false;

            TrayControlStyle.ConfigureInlineActionButton(
                RestartButton,
                "Restart",
                new Point(16, 460),
                (_, _) => RestartRequested?.Invoke(this, EventArgs.Empty));
            RestartButton.Size = new Size(90, 28);
            RestartButton.Visible = false;

            StatusLabel.AutoSize = false;
            StatusLabel.Font = new Font("Segoe UI", 8.75F);
            StatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            StatusLabel.Location = new Point(176, 112);
            StatusLabel.Size = new Size(384, 32);

            QrPictureBox.BackColor = Color.White;
            QrPictureBox.Location = new Point(16, 148);
            QrPictureBox.Size = new Size(180, 180);
            QrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            QrPictureBox.Visible = false;


            ActionPanel.Location = new Point(0, 53);
            ActionPanel.Size = new Size(578, 74);
            ActionPanel.BackColor = Color.FromArgb(41, 43, 71);
            ActionPanel.Controls.Add(ActionLabel);
            ActionPanel.Controls.Add(ActionComboBox);
            RegistrationPanel.Size = new Size(578, 493);
            RegistrationPanel.BackColor = Color.FromArgb(31, 33, 57);
            RegistrationPanel.Controls.Add(UrlLabel);
            RegistrationPanel.Controls.Add(UrlTextBox);
            RegistrationPanel.Controls.Add(NameLabel);
            RegistrationPanel.Controls.Add(NameTextBox);
            RegistrationPanel.Controls.Add(SubmitButton);
            RegistrationPanel.Controls.Add(BackButton);
            RegistrationPanel.Controls.Add(RestartButton);
            RegistrationPanel.Controls.Add(StatusLabel);
            RegistrationPanel.Controls.Add(QrPictureBox);
            RegistrationPanel.Controls.Add(GuidancePanel);
            Controls.Add(ActionPanel);
            Controls.Add(RegistrationPanel);
            ResumeLayout(false);
        }

        internal void ShowSection(bool actionSelection, bool registration)
        {
            Visible = actionSelection || registration;
            ActionPanel.Visible = actionSelection;
            ActionLabel.Visible = actionSelection;
            ActionComboBox.Visible = actionSelection;
            RegistrationPanel.Visible = registration;
        }

        internal void ApplyRegistrationState(bool newServer, bool submitting, bool awaitingCompletion, string status)
        {
            UpdateServerRegistrationGuidance(newServer);
            GuidancePanel.Visible = true;
            UrlLabel.Location = new Point(16, 174);
            UrlTextBox.Location = new Point(16, 194);
            UrlTextBox.Size = newServer ? new Size(260, 25) : new Size(544, 25);
            NameLabel.Location = new Point(300, 174);
            NameTextBox.Location = new Point(300, 194);
            NameTextBox.Size = new Size(260, 25);
            SetServerRegistrationControlsVisible(true, newServer);
            BackButton.Location = new Point(16, 232);
            SubmitButton.Location = new Point(BackButton.Right + 5, BackButton.Top);
            StatusLabel.Location = new Point(176, 236);
            QrPictureBox.Location = new Point(16, 270);
            StatusLabel.Text = status;
            SubmitButton.Enabled = !submitting && !awaitingCompletion;
            SubmitButton.Text = submitting ? "Generating..." : awaitingCompletion ? "QR ready" : "Continue";
        }

        private void ConfigureServerRegistrationGuidanceBox()
        {
            GuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            GuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            GuidancePanel.Location = new Point(16, 50);
            GuidancePanel.Size = new Size(546, 110);
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
            GuidanceBox.Size = new Size(528, 92);
            GuidanceBox.TabStop = false;
            GuidancePanel.Controls.Add(GuidanceBox);

            UpdateServerRegistrationGuidance(isNewServerRegistration: true);
        }

        internal void UpdateServerRegistrationGuidance(bool isNewServerRegistration)
        {
            string guidance = isNewServerRegistration
                ? "Add a server:\n" +
                    "Enter the new server URL and name, then select Continue.\n\n" +
                    "Scan the QR code with a mobile device already registered to your account. The mobile app registers this desktop with the new server; no separate mobile registration is needed."
                : "Sync existing server:\n" +
                    "Use this when your mobile device is already registered on the target server.\n\n" +
                    "Select Continue, then scan the QR code in the mobile app to connect this desktop.";

            if (string.Equals(GuidanceBox.Text, guidance, StringComparison.Ordinal))
            {
                return;
            }

            GuidanceBox.Text = guidance;

            EmphasizeServerRegistrationGuidance(isNewServerRegistration ? "Add a server:" : "Sync existing server:");
            EmphasizeServerRegistrationGuidance(isNewServerRegistration
                ? "no separate mobile registration is needed."
                : "already registered on the target server.");
            EmphasizeServerRegistrationGuidance(isNewServerRegistration
                ? "Scan the QR code"
                : "scan the QR code");
            GuidanceBox.Select(0, 0);
        }

        private void EmphasizeServerRegistrationGuidance(string text)
        {
            int startIndex = GuidanceBox.Text.IndexOf(text, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return;
            }

            GuidanceBox.Select(startIndex, text.Length);
            GuidanceBox.SelectionFont = _guidanceBoldFont;
        }

        internal void SetServerRegistrationControlsVisible(bool visible, bool showNamespaceName = false)
        {
            UrlLabel.Visible = visible;
            UrlTextBox.Visible = visible;
            NameLabel.Visible = visible && showNamespaceName;
            NameTextBox.Visible = visible && showNamespaceName;
            SubmitButton.Visible = visible;
            BackButton.Visible = visible;
            StatusLabel.Visible = visible;
            QrPictureBox.Visible = visible && QrPictureBox.Image != null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _guidanceFont.Dispose();
                _guidanceBoldFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
