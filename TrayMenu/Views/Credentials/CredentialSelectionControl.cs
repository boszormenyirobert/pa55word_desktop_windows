using System.Drawing;
using System.Windows.Forms;

namespace TrayApp.TrayMenu.Controls
{
    internal sealed class CredentialSelectionControl : UserControl
    {
        private readonly Label _groupLabel = new();
        private readonly Label _entryLabel = new();
        internal TrayDropdown GroupSelector { get; } = new();
        internal TrayDropdown EntrySelector { get; } = new();
        internal Button ManageGroupsButton { get; } = new();
        internal Button CreateButton { get; } = new();
        internal event EventHandler? GroupChanged;
        internal event EventHandler? EntryChanged;
        internal event EventHandler? ManageGroupsRequested;
        internal event EventHandler? CreateRequested;

        public CredentialSelectionControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(41, 43, 71);
            Bounds = new Rectangle(32, 142, 578, 74);
            Visible = false;
            TrayControlStyle.ConfigureLabel(_groupLabel, "Group", new Point(16, 14), fontSize: 9F);
            TrayControlStyle.ConfigureLabel(_entryLabel, "Entries", new Point(308, 14), fontSize: 9F);
            TrayControlStyle.ConfigureComboBox(GroupSelector, 10F, Color.FromArgb(31, 33, 57));
            TrayControlStyle.ConfigureComboBox(EntrySelector, 10F, Color.FromArgb(31, 33, 57));
            ConfigureAction(ManageGroupsButton, "Manage groups", new Size(130, 24));
            ConfigureAction(CreateButton, "+ Create new entry", new Size(150, 24));
            GroupSelector.SelectedIndexChanged += (_, _) => GroupChanged?.Invoke(GroupSelector, EventArgs.Empty);
            EntrySelector.SelectedIndexChanged += (_, _) => EntryChanged?.Invoke(EntrySelector, EventArgs.Empty);
            ManageGroupsButton.Click += (_, _) => ManageGroupsRequested?.Invoke(this, EventArgs.Empty);
            CreateButton.Click += (_, _) => CreateRequested?.Invoke(this, EventArgs.Empty);
            Controls.AddRange(new Control[] { _groupLabel, GroupSelector, _entryLabel, EntrySelector, ManageGroupsButton, CreateButton });
            LayoutSelectors();
        }

        private static void ConfigureAction(Button button, string text, Size size)
        {
            button.Size = size;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(46, 48, 77);
            button.ForeColor = Color.FromArgb(120, 205, 0);
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
        }

        internal void LayoutSelectors()
        {
            Bounds = new Rectangle(32, 142, 578, 74);
            GroupSelector.Bounds = new Rectangle(16, 36, 244, 25);
            EntrySelector.Bounds = new Rectangle(308, 36, 216, 25);
            ManageGroupsButton.Location = new Point(130, 10);
            CreateButton.Location = new Point(374, 10);
        }

        internal void ApplyMode(bool groupManager, bool createGroup, bool createEntry, bool loading, Control groupEditor)
        {
            Visible = !groupManager && !createEntry;
            Control target = groupManager ? groupEditor : this;
            if (CreateButton.Parent != target)
            {
                target.Controls.Add(CreateButton);
            }
            LayoutSelectors();
            ManageGroupsButton.Visible = !groupManager && !createEntry;
            CreateButton.Visible = groupManager ? !createGroup : !createEntry;
            CreateButton.Enabled = !groupManager || !loading && !createGroup;
            CreateButton.Text = groupManager ? "+ Create new group" : "+ Create new entry";
            if (groupManager)
            {
                CreateButton.Location = new Point(372, 0);
            }
        }
    }
}