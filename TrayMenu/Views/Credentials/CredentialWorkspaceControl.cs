using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class CredentialWorkspaceControl : UserControl
    {
        internal Button BackButton { get; } = new();
        internal Panel EntryEditorPanel { get; } = new();
        private Label EditorModeLabel { get; } = new();
        private Label LabelEditorLabel { get; } = new();
        internal TextBox LabelTextBox { get; } = new();
        private Label AssignedGroupLabel { get; } = new();
        internal TrayDropdown AssignedGroupComboBox { get; } = new();
        private Label DomainLabel { get; } = new();
        internal TextBox DomainTextBox { get; } = new();
        private Label UsernameLabel { get; } = new();
        internal TextBox UsernameTextBox { get; } = new();
        private Button UsernameCopyButton { get; } = new();
        private Label PasswordLabel { get; } = new();
        internal TextBox PasswordTextBox { get; } = new();
        private Button PasswordGenerateButton { get; } = new();
        private Button PasswordRevealButton { get; } = new();
        private Button PasswordCopyButton { get; } = new();
        private Label DescriptionLabel { get; } = new();
        internal TextBox DescriptionTextBox { get; } = new();
        private Button DescriptionCopyButton { get; } = new();
        internal Button DeleteButton { get; } = new();
        internal Button SaveButton { get; } = new();
        internal Label StatusLabel { get; } = new();
        internal Panel GroupEditorPanel { get; } = new();
        private Label ManagedGroupLabel { get; } = new();
        internal TrayDropdown ManagedGroupComboBox { get; } = new();
        private Label ManagedGroupNameLabel { get; } = new();
        internal TextBox ManagedGroupNameTextBox { get; } = new();
        private Label NewGroupNameLabel { get; } = new();
        internal TextBox NewGroupNameTextBox { get; } = new();
        internal Button GroupDeleteButton { get; } = new();
        internal Button GroupSaveButton { get; } = new();
        internal Button GroupCreateButton { get; } = new();
        internal Label GroupStatusLabel { get; } = new();
        internal Panel GroupDeletionInfoPanel { get; } = new();
        private Label GroupDeletionInfoLabel { get; } = new();

        internal event EventHandler? BackRequested;
        internal event EventHandler? UsernameCopyRequested;
        internal event EventHandler? PasswordGenerateRequested;
        internal event EventHandler? PasswordRevealRequested;
        internal event EventHandler? PasswordCopyRequested;
        internal event EventHandler? DescriptionCopyRequested;
        internal event EventHandler? DeleteRequested;
        internal event EventHandler? SaveRequested;
        internal event EventHandler? ManagedGroupChanged;
        internal event EventHandler? GroupDeleteRequested;
        internal event EventHandler? GroupSaveRequested;
        internal event EventHandler? GroupCreateRequested;
        internal event EventHandler? EntryInputChanged;
        internal event EventHandler? EntrySelectionChanged;
        internal event EventHandler? GroupInputChanged;

        public CredentialWorkspaceControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            SuspendLayout();
            this.Location = new Point(32, 231);
            this.Size = new Size(578, 414);
            this.BackColor = Color.FromArgb(41, 43, 71);
            this.AutoScroll = false;
            this.Visible = false;

            TrayControlStyle.ConfigureButton(BackButton, "Back", new Point(16, 12), (_, _) => BackRequested?.Invoke(this, EventArgs.Empty));
            BackButton.Visible = false;

            EntryEditorPanel.Location = new Point(16, 12);
            EntryEditorPanel.Size = new Size(547, 414);
            EntryEditorPanel.BackColor = Color.Transparent;

            TrayControlStyle.ConfigureLabel(EditorModeLabel, "Read / Edit / Delete mode", new Point(0, 0));
            EditorModeLabel.ForeColor = Color.FromArgb(120, 205, 0);
            EditorModeLabel.Visible = false;

            TrayControlStyle.ConfigureLabel(LabelEditorLabel, "Label", new Point(0, 20));
            TrayControlStyle.ConfigureTextBox(LabelTextBox, new Point(0, 40), new Size(257, 25), multiline: false);
            LabelTextBox.TextChanged += (_, _) => EntryInputChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureLabel(AssignedGroupLabel, "Assigned group", new Point(272, 20));
            TrayControlStyle.ConfigureEditorComboBox(AssignedGroupComboBox, new Point(272, 40), new Size(275, 25));
            AssignedGroupComboBox.SelectedIndexChanged += (_, _) => EntrySelectionChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureLabel(DomainLabel, "Domain", new Point(0, 78));
            TrayControlStyle.ConfigureTextBox(DomainTextBox, new Point(0, 98), new Size(166, 25), multiline: false);
            DomainTextBox.TextChanged += (_, _) => EntryInputChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureLabel(UsernameLabel, "Username", new Point(178, 78));
            TrayControlStyle.ConfigureTextBox(UsernameTextBox, new Point(178, 98), new Size(166, 25), multiline: false);
            UsernameTextBox.TextChanged += (_, _) => EntryInputChanged?.Invoke(this, EventArgs.Empty);
            TrayControlStyle.ConfigureIconButton(UsernameCopyButton, "\u29C9", new Point(250, 75), (_, _) => UsernameCopyRequested?.Invoke(this, EventArgs.Empty));

            TrayControlStyle.ConfigureLabel(PasswordLabel, "Password", new Point(356, 78));
            TrayControlStyle.ConfigureTextBox(PasswordTextBox, new Point(356, 98), new Size(191, 25), multiline: false);
            PasswordTextBox.UseSystemPasswordChar = true;
            PasswordTextBox.TextChanged += (_, _) => EntryInputChanged?.Invoke(this, EventArgs.Empty);
            TrayControlStyle.ConfigureIconButton(PasswordGenerateButton, "\u21BB", new Point(425, 75), (_, _) => PasswordGenerateRequested?.Invoke(this, EventArgs.Empty));
            TrayControlStyle.ConfigureIconButton(PasswordRevealButton, "\U0001F441", new Point(501, 75), (_, _) => PasswordRevealRequested?.Invoke(this, EventArgs.Empty));
            TrayControlStyle.ConfigureIconButton(PasswordCopyButton, "\u29C9", new Point(525, 75), (_, _) => PasswordCopyRequested?.Invoke(this, EventArgs.Empty));

            TrayControlStyle.ConfigureLabel(DescriptionLabel, "Description", new Point(0, 140));
            TrayControlStyle.ConfigureTextBox(DescriptionTextBox, new Point(0, 160), new Size(547, 135), multiline: true);
            DescriptionTextBox.TextChanged += (_, _) => EntryInputChanged?.Invoke(this, EventArgs.Empty);
            TrayControlStyle.ConfigureIconButton(DescriptionCopyButton, "\u29C9", new Point(88, 137), (_, _) => DescriptionCopyRequested?.Invoke(this, EventArgs.Empty));

            TrayControlStyle.ConfigureButton(DeleteButton, "Delete entry", new Point(0, 311), (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty));
            DeleteButton.BackColor = Color.FromArgb(214, 88, 88);
            DeleteButton.ForeColor = Color.White;

            TrayControlStyle.ConfigureButton(SaveButton, "Save changes", new Point(112, 311), (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty));
            SaveButton.BackColor = Color.FromArgb(120, 205, 0);
            SaveButton.ForeColor = Color.Black;
            SaveButton.FlatAppearance.BorderSize = 1;
            SaveButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);

            StatusLabel.AutoSize = false;
            StatusLabel.Font = new Font("Segoe UI", 8.75F);
            StatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            StatusLabel.Location = new Point(0, 351);
            StatusLabel.Size = new Size(547, 32);

            EntryEditorPanel.Controls.Add(EditorModeLabel);
            EntryEditorPanel.Controls.Add(LabelEditorLabel);
            EntryEditorPanel.Controls.Add(LabelTextBox);
            EntryEditorPanel.Controls.Add(AssignedGroupLabel);
            EntryEditorPanel.Controls.Add(AssignedGroupComboBox);
            EntryEditorPanel.Controls.Add(DomainLabel);
            EntryEditorPanel.Controls.Add(DomainTextBox);
            EntryEditorPanel.Controls.Add(UsernameLabel);
            EntryEditorPanel.Controls.Add(UsernameTextBox);
            EntryEditorPanel.Controls.Add(UsernameCopyButton);
            EntryEditorPanel.Controls.Add(PasswordLabel);
            EntryEditorPanel.Controls.Add(PasswordTextBox);
            EntryEditorPanel.Controls.Add(PasswordGenerateButton);
            EntryEditorPanel.Controls.Add(PasswordRevealButton);
            EntryEditorPanel.Controls.Add(PasswordCopyButton);
            EntryEditorPanel.Controls.Add(DescriptionLabel);
            EntryEditorPanel.Controls.Add(DescriptionTextBox);
            EntryEditorPanel.Controls.Add(DescriptionCopyButton);
            EntryEditorPanel.Controls.Add(DeleteButton);
            EntryEditorPanel.Controls.Add(SaveButton);
            EntryEditorPanel.Controls.Add(StatusLabel);

            GroupEditorPanel.Location = new Point(16, 12);
            GroupEditorPanel.Size = new Size(530, 280);
            GroupEditorPanel.BackColor = Color.Transparent;
            GroupEditorPanel.Visible = false;

            TrayControlStyle.ConfigureLabel(ManagedGroupLabel, "Groups", new Point(0, 0));
            TrayControlStyle.ConfigureEditorComboBox(ManagedGroupComboBox, new Point(0, 20), new Size(522, 25));
            ManagedGroupComboBox.SelectedIndexChanged += (_, _) => ManagedGroupChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureLabel(ManagedGroupNameLabel, "Group name", new Point(0, 58));
            TrayControlStyle.ConfigureTextBox(ManagedGroupNameTextBox, new Point(0, 78), new Size(522, 25), multiline: false);
            ManagedGroupNameTextBox.TextChanged += (_, _) => GroupInputChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureLabel(NewGroupNameLabel, "New group name", new Point(0, 58));
            TrayControlStyle.ConfigureTextBox(NewGroupNameTextBox, new Point(0, 78), new Size(522, 25), multiline: false);
            NewGroupNameTextBox.TextChanged += (_, _) => GroupInputChanged?.Invoke(this, EventArgs.Empty);

            TrayControlStyle.ConfigureButton(GroupDeleteButton, "Delete selected group", new Point(0, 122), (_, _) => GroupDeleteRequested?.Invoke(this, EventArgs.Empty));
            GroupDeleteButton.Size = new Size(150, 28);
            GroupDeleteButton.BackColor = Color.FromArgb(214, 88, 88);
            GroupDeleteButton.ForeColor = Color.White;
            TrayControlStyle.ConfigureButton(GroupSaveButton, "Save group", new Point(158, 122), (_, _) => GroupSaveRequested?.Invoke(this, EventArgs.Empty));
            TrayControlStyle.ConfigureButton(GroupCreateButton, "Create group", new Point(270, 122), (_, _) => GroupCreateRequested?.Invoke(this, EventArgs.Empty));

            GroupStatusLabel.AutoSize = false;
            GroupStatusLabel.Font = new Font("Segoe UI", 8.75F);
            GroupStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            GroupStatusLabel.Location = new Point(0, 162);
            GroupStatusLabel.Size = new Size(522, 40);

            GroupDeletionInfoPanel.BackColor = Color.FromArgb(46, 48, 77);
            GroupDeletionInfoPanel.BorderStyle = BorderStyle.FixedSingle;
            GroupDeletionInfoPanel.Location = new Point(0, 212);
            GroupDeletionInfoPanel.Size = new Size(522, 54);

            GroupDeletionInfoLabel.AutoSize = false;
            GroupDeletionInfoLabel.Font = new Font("Segoe UI", 8.5F);
            GroupDeletionInfoLabel.ForeColor = Color.FromArgb(220, 222, 241);
            GroupDeletionInfoLabel.Location = new Point(10, 7);
            GroupDeletionInfoLabel.Size = new Size(500, 38);
            GroupDeletionInfoLabel.Text = "Deleting a group only removes the selected group. Credentials assigned to it move to the Ungrouped category.";
            GroupDeletionInfoPanel.Controls.Add(GroupDeletionInfoLabel);

            GroupEditorPanel.Controls.Add(ManagedGroupLabel);
            GroupEditorPanel.Controls.Add(ManagedGroupComboBox);
            GroupEditorPanel.Controls.Add(ManagedGroupNameLabel);
            GroupEditorPanel.Controls.Add(ManagedGroupNameTextBox);
            GroupEditorPanel.Controls.Add(NewGroupNameLabel);
            GroupEditorPanel.Controls.Add(NewGroupNameTextBox);
            GroupEditorPanel.Controls.Add(GroupSaveButton);
            GroupEditorPanel.Controls.Add(GroupDeleteButton);
            GroupEditorPanel.Controls.Add(GroupCreateButton);
            GroupEditorPanel.Controls.Add(GroupStatusLabel);
            GroupEditorPanel.Controls.Add(GroupDeletionInfoPanel);

            this.Controls.Add(BackButton);
            this.Controls.Add(EntryEditorPanel);
            this.Controls.Add(GroupEditorPanel);
            ResumeLayout(false);
        }

        private IEnumerable<TextBox> EntryTextBoxes =>
            new[] { LabelTextBox, DomainTextBox, UsernameTextBox, PasswordTextBox, DescriptionTextBox };

        internal void ClearEntry()
        {
            foreach (TextBox textBox in EntryTextBoxes)
            {
                textBox.Clear();
            }
        }

        internal void SetEntryAvailability(bool enabled, bool assignmentEnabled, bool saveEnabled)
        {
            foreach (Control control in EntryTextBoxes.Cast<Control>().Concat(new Control[]
                { UsernameCopyButton, PasswordGenerateButton, PasswordRevealButton, PasswordCopyButton, DescriptionCopyButton }))
            {
                control.Enabled = enabled;
            }

            AssignedGroupComboBox.Enabled = assignmentEnabled;
            SaveButton.Enabled = saveEnabled;
        }

        internal void ApplyEntryState(bool creating, bool canDelete, bool confirmingDeletion, bool loading, string status)
        {
            SaveButton.Left = confirmingDeletion ? 0 : canDelete ? 112 : 0;
            DeleteButton.Left = confirmingDeletion ? 112 : 0;
            SaveButton.Text = confirmingDeletion ? "Cancel" : creating ? "Save new item" : "Save changes";
            StatusLabel.Text = status;
            DeleteButton.Visible = canDelete;
            DeleteButton.Enabled = canDelete && !loading;
            DeleteButton.Text = confirmingDeletion ? "Delete" : "Delete entry";
            EditorModeLabel.Text = creating ? "Create credential mode" : "Read / Edit / Delete mode";
            EditorModeLabel.Visible = creating || canDelete;
        }

        internal void ApplyGroupState(bool creating, bool canDelete, bool confirmingDeletion, bool loading, string status)
        {
            GroupSaveButton.Location = new Point(confirmingDeletion || creating ? 0 : 158, 122);
            GroupDeleteButton.Location = new Point(confirmingDeletion ? 112 : 0, 122);
            GroupStatusLabel.Text = status;
            ManagedGroupLabel.Visible = !creating;
            ManagedGroupComboBox.Visible = !creating;
            ManagedGroupNameLabel.Visible = !creating;
            ManagedGroupNameTextBox.Visible = !creating;
            GroupCreateButton.Visible = false;
            GroupDeleteButton.Visible = !creating;
            GroupDeleteButton.Enabled = canDelete && !loading;
            GroupDeleteButton.Text = confirmingDeletion ? "Delete" : "Delete selected group";
            GroupSaveButton.Text = confirmingDeletion ? "Cancel" : creating ? "Save group" : "Save changes";
            GroupDeletionInfoPanel.Visible = !creating && !confirmingDeletion;
            NewGroupNameLabel.Visible = creating;
            NewGroupNameTextBox.Visible = creating;
        }

        internal void ShowEditor(bool groupMode, bool showEntry, bool showBack, int availableHeight)
        {
            EntryEditorPanel.Visible = showEntry;
            GroupEditorPanel.Visible = groupMode;
            BackButton.Visible = showBack;
            LayoutEditor(availableHeight);
            Panel editor = groupMode ? GroupEditorPanel : EntryEditorPanel;
            Button save = groupMode ? GroupSaveButton : SaveButton;
            BackButton.Location = new Point(editor.Left + save.Right + 8, editor.Top + save.Top);
            editor.BringToFront();
            if (groupMode || showEntry)
            {
                BackButton.BringToFront();
            }
        }

        internal void LayoutEditor(int availableHeight)
        {
            const int bottomMargin = 15;
            const int actionToStatusGap = 12;
            const int descriptionToActionGap = 16;

            int workspaceHeight = Math.Max(
                414,
                availableHeight - this.Top - bottomMargin);
            this.Height = workspaceHeight;
            EntryEditorPanel.Height = workspaceHeight - EntryEditorPanel.Top;

            StatusLabel.Top = EntryEditorPanel.Height -
                StatusLabel.Height - bottomMargin;
            int actionTop = StatusLabel.Top -
                SaveButton.Height - actionToStatusGap;
            SaveButton.Top = actionTop;
            DeleteButton.Top = actionTop;
            if (EntryEditorPanel.Visible)
            {
                BackButton.Location = new Point(
                    EntryEditorPanel.Left + SaveButton.Right + 8,
                    EntryEditorPanel.Top + actionTop);
            }

            DescriptionTextBox.Height = Math.Max(
                80,
                actionTop - DescriptionTextBox.Top - descriptionToActionGap);
        }
    }
}
