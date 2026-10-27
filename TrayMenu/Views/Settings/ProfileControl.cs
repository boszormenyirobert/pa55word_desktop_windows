using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class ProfileControl : UserControl
    {
        internal Panel ActionPanel { get; } = new();
        internal Label ActionLabel { get; } = new();
        internal TrayDropdown ActionComboBox { get; } = new();
        internal Panel RenamePanel { get; } = new();
        internal TextBox NameTextBox { get; } = new();
        internal string ProfileName
        {
            get => NameTextBox.Text;
            set => NameTextBox.Text = value;
        }

        internal void FocusProfileName()
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }
        internal Button RenameSubmitButton { get; } = new();
        internal Panel RegistrationPanel { get; } = new();
        internal Label RegistrationUrlLabel { get; } = new();
        internal TextBox RegistrationUrlTextBox { get; } = new();
        internal Label RegistrationNameLabel { get; } = new();
        internal TextBox RegistrationNameTextBox { get; } = new();
        internal Button RegistrationBackButton { get; } = new();
        internal Button RegistrationSubmitButton { get; } = new();
        internal Label RegistrationStatusLabel { get; } = new();
        internal PictureBox RegistrationQrPictureBox { get; } = new();
        internal Panel DeletionPanel { get; } = new();
        internal Label DeletionMessageLabel { get; } = new();
        internal Label DeletionProfileLabel { get; } = new();
        internal Button DeletionCancelButton { get; } = new();
        internal Button DeletionConfirmButton { get; } = new();

        internal event EventHandler? ActionChanged;
        internal event EventHandler? RenameRequested;
        internal event EventHandler? RegistrationBackRequested;
        internal event EventHandler? RegistrationSubmitRequested;
        internal event EventHandler? DeletionCancelRequested;
        internal event EventHandler? DeletionConfirmRequested;

        public ProfileControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Transparent;
            Location = new Point(32, 152);
            Size = new Size(578, 493);
            Visible = false;
            SuspendLayout();
            ActionPanel.BackColor = Color.FromArgb(41, 43, 71);
            ActionPanel.Location = new Point(0, 53);
            ActionPanel.Size = new Size(578, 74);
            ActionLabel.AutoSize = true;
            ActionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            ActionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            ActionLabel.Location = new Point(16, 14);
            ActionLabel.Text = "Account action";
            TrayControlStyle.ConfigureComboBox(ActionComboBox, 10F, Color.FromArgb(31, 33, 57));
            ActionComboBox.Location = new Point(16, 36);
            ActionComboBox.Size = new Size(544, 25);
            ActionComboBox.SelectedIndexChanged += (_, _) => ActionChanged?.Invoke(this, EventArgs.Empty);
            ActionPanel.Controls.Add(ActionLabel);
            ActionPanel.Controls.Add(ActionComboBox);
            Controls.Add(ActionPanel);
            RenamePanel.BackColor = Color.FromArgb(41, 43, 71);
            RenamePanel.Location = new Point(0, 143);
            RenamePanel.Size = new Size(578, 140);
            RenamePanel.Visible = false;
            var nameLabel = new Label();
            TrayControlStyle.ConfigureLabel(nameLabel, "Profile name", new Point(16, 16));
            TrayControlStyle.ConfigureTextBox(
                NameTextBox, new Point(16, 40), new Size(544, 25), multiline: false);
            TrayControlStyle.ConfigureButton(
                RenameSubmitButton, "Save name", new Point(16, 90), (_, _) => RenameRequested?.Invoke(this, EventArgs.Empty));
            RenameSubmitButton.Size = new Size(120, 30);
            RenamePanel.Controls.Add(nameLabel);
            RenamePanel.Controls.Add(NameTextBox);
            RenamePanel.Controls.Add(RenameSubmitButton);

            RegistrationPanel.BackColor = Color.FromArgb(41, 43, 71);
            RegistrationPanel.Location = Point.Empty;
            RegistrationPanel.Size = new Size(578, 388);
            RegistrationPanel.Visible = false;

            TrayControlStyle.ConfigureLabel(RegistrationUrlLabel, "Base url", new Point(16, 18));
            TrayControlStyle.ConfigureTextBox(RegistrationUrlTextBox, new Point(16, 38), new Size(544, 25), multiline: false);
            TrayControlStyle.ConfigureLabel(RegistrationNameLabel, "Profile name", new Point(16, 82));
            TrayControlStyle.ConfigureTextBox(RegistrationNameTextBox, new Point(16, 102), new Size(544, 25), multiline: false);

            TrayControlStyle.ConfigureButton(RegistrationBackButton, "Back", new Point(16, 148), (_, _) => RegistrationBackRequested?.Invoke(this, EventArgs.Empty));
            RegistrationBackButton.Size = new Size(90, 30);
            TrayControlStyle.ConfigureButton(
                RegistrationSubmitButton,
                "Continue",
                new Point(RegistrationBackButton.Right + 5, RegistrationBackButton.Top),
                (_, _) => RegistrationSubmitRequested?.Invoke(this, EventArgs.Empty));
            RegistrationSubmitButton.Size = new Size(120, 30);

            RegistrationStatusLabel.AutoSize = false;
            RegistrationStatusLabel.Font = new Font("Segoe UI", 8.75F);
            RegistrationStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            RegistrationStatusLabel.Location = new Point(16, 194);
            RegistrationStatusLabel.Size = new Size(316, 32);

            RegistrationQrPictureBox.BackColor = Color.FromArgb(31, 33, 57);
            RegistrationQrPictureBox.BorderStyle = BorderStyle.FixedSingle;
            RegistrationQrPictureBox.Location = new Point(348, 148);
            RegistrationQrPictureBox.Size = new Size(212, 212);
            RegistrationQrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            RegistrationQrPictureBox.Visible = false;

            RegistrationPanel.Controls.Add(RegistrationUrlLabel);
            RegistrationPanel.Controls.Add(RegistrationUrlTextBox);
            RegistrationPanel.Controls.Add(RegistrationNameLabel);
            RegistrationPanel.Controls.Add(RegistrationNameTextBox);
            RegistrationPanel.Controls.Add(RegistrationSubmitButton);
            RegistrationPanel.Controls.Add(RegistrationBackButton);
            RegistrationPanel.Controls.Add(RegistrationStatusLabel);
            RegistrationPanel.Controls.Add(RegistrationQrPictureBox);

            DeletionPanel.BackColor = Color.FromArgb(41, 43, 71);
            DeletionPanel.Location = Point.Empty;
            DeletionPanel.Size = new Size(578, 220);
            DeletionPanel.Visible = false;

            DeletionMessageLabel.AutoSize = false;
            DeletionMessageLabel.Font = new Font("Segoe UI", 10F);
            DeletionMessageLabel.ForeColor = Color.FromArgb(171, 174, 211);
            DeletionMessageLabel.Location = new Point(16, 20);
            DeletionMessageLabel.Size = new Size(544, 104);
            DeletionMessageLabel.Text = "Deleting your profile deregisters this desktop from every server in the profile. It does not delete accounts or credentials. Sign in with this profile to every server first. If a deletion fails, the remaining server registrations are kept so you can retry.";

            DeletionProfileLabel.AutoSize = false;
            DeletionProfileLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            DeletionProfileLabel.ForeColor = Color.White;
            DeletionProfileLabel.Location = new Point(16, 130);
            DeletionProfileLabel.Size = new Size(544, 20);

            TrayControlStyle.ConfigureButton(DeletionCancelButton, "Cancel", new Point(16, 162), (_, _) => DeletionCancelRequested?.Invoke(this, EventArgs.Empty));
            DeletionCancelButton.Size = new Size(90, 30);
            TrayControlStyle.ConfigureButton(DeletionConfirmButton, "Delete profile", new Point(112, 162), (_, _) => DeletionConfirmRequested?.Invoke(this, EventArgs.Empty));
            DeletionConfirmButton.Size = new Size(120, 30);
            DeletionConfirmButton.BackColor = Color.FromArgb(214, 88, 88);
            DeletionConfirmButton.ForeColor = Color.White;
            DeletionConfirmButton.FlatAppearance.BorderColor = Color.FromArgb(214, 88, 88);

            DeletionPanel.Controls.Add(DeletionMessageLabel);
            DeletionPanel.Controls.Add(DeletionProfileLabel);
            DeletionPanel.Controls.Add(DeletionCancelButton);
            DeletionPanel.Controls.Add(DeletionConfirmButton);
            Controls.Add(RenamePanel);
            Controls.Add(RegistrationPanel);
            Controls.Add(DeletionPanel);
            ResumeLayout(false);
        }

        internal void ShowSection(bool active, bool rename, bool registration, bool deletion, bool canRename)
        {
            Visible = active;
            ActionPanel.Visible = active && !registration && !deletion;
            ActionLabel.Visible = true;
            ActionComboBox.Visible = true;
            RenamePanel.Visible = active && rename;
            RegistrationPanel.Visible = active && registration;
            DeletionPanel.Visible = active && deletion;
            RenameSubmitButton.Enabled = canRename;
            if (registration)
            {
                RegistrationPanel.BringToFront();
            }
            else if (deletion)
            {
                DeletionPanel.BringToFront();
            }
        }
    }
}
