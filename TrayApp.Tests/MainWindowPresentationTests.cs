using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class MainWindowPresentationTests
{
    [Fact]
    public void Recovery_ContentRemainsInsideSettingsContainerAfterNavigationAndResize()
    {
        RunOnStaThread(() =>
        {
            var form = (Form1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            using var detail = new Panel { Size = new Size(643, 600) };
            using var settings = new Panel { Location = new Point(32, 152), Size = new Size(578, 390) };
            using var navigation = new FlowLayoutPanel { Height = 20 };
            using var settingsDescription = new RichTextBox { Top = 38 };
            using var recovery = new Panel();
            using var profileLabel = new Label();
            using var profiles = new TrayDropdown();
            using var description = new RichTextBox();
            using var status = new Label();
            using var open = new Button();
            var controller = new TrayMenuController((_, _) => { }, () => { });
            void Set(string field, object value) => typeof(Form1)
                .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(form, value);
            void Invoke(string method) => typeof(Form1)
                .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, null);

            Set("_quickPanelDetailPanel", detail);
            Set("_quickPanelSettingsOverviewPanel", settings);
            Set("_quickPanelSettingsNavigationPanel", navigation);
            Set("_quickPanelSettingsDescriptionBox", settingsDescription);
            Set("_quickPanelRecoveryPanel", recovery);
            Set("_quickPanelRecoveryProfileLabel", profileLabel);
            Set("_quickPanelRecoveryProfileComboBox", profiles);
            Set("_quickPanelRecoveryDescriptionBox", description);
            Set("_quickPanelRecoveryStatusLabel", status);
            Set("_quickPanelRecoveryOpenButton", open);
            Set("_selectedRecoveryProfileUserId", string.Empty);
            Set("_trayMenuController", controller);
            detail.Controls.Add(settings);
            settings.Controls.Add(navigation);
            settings.Controls.Add(settingsDescription);
            settings.Controls.Add(recovery);
            Invoke("InitializeRecoveryPanel");

            controller.SetActiveSection(TrayMenuSection.SettingsUsers);
            Invoke("LayoutSettingsOverview");
            Assert.Equal(navigation.Height, settings.Height);

            controller.SetActiveSection(TrayMenuSection.Recovery);
            recovery.Visible = true;
            Invoke("LayoutSettingsOverview");
            Assert.True(profiles.Visible);
            Assert.False(description.Visible);
            Assert.True(settings.ClientRectangle.Contains(recovery.Bounds));
            Assert.True(recovery.ClientRectangle.Contains(profiles.Bounds));

            Type optionType = typeof(Form1).GetNestedType("SelectionOption",
                System.Reflection.BindingFlags.NonPublic)!;
            profiles.Items.Add(Activator.CreateInstance(optionType, "Alice", "alice")!);
            profiles.SelectedIndex = 0;
            Assert.True(description.Visible);
            Assert.True(open.Visible);
            Assert.Equal("Open recovery", open.Text);
            Assert.Contains("damaged or lost", description.Text);
            Assert.Contains("email address", description.Text);
            Assert.Contains("new mobile phone", description.Text);

            foreach (int height in new[] { 720, 440 })
            {
                detail.Height = height;
                Invoke("LayoutSettingsOverview");
                Assert.Equal(height - settings.Top - 15, settings.Height);
                Assert.True(settings.ClientRectangle.Contains(recovery.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(profiles.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(open.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(description.Bounds));
                Assert.Equal(recovery.Height, description.Bottom);
            }

            using var card = new RecoveryControl(new TrayApp.Models.DesktopRegistration());
            typeof(Form1).GetMethod("ShowRecoveryCard",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, new object[] { card });
            Assert.Same(recovery, card.Parent);
            Assert.Equal(DockStyle.Fill, card.Dock);
            Assert.IsNotAssignableFrom<Form>(card);
            Assert.False(profiles.Enabled);
            Assert.False(open.Enabled);
            foreach (int height in new[] { 720, 440 })
            {
                detail.Height = height;
                Invoke("LayoutSettingsOverview");
                recovery.PerformLayout();
                Assert.Equal(recovery.ClientRectangle, card.Bounds);
            }
            var back = card.Controls.Find("RecoveryBack", true).Single();
            typeof(Control).GetMethod("OnClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(back, new object[] { EventArgs.Empty });
            Assert.True(card.IsDisposed);
            Assert.DoesNotContain(card, recovery.Controls.Cast<Control>());
            Assert.True(open.Visible);
            Assert.True(profiles.Enabled);
            Assert.True(open.Enabled);
        });
    }

    [Fact]
    public void Brand_ClickOpensWebsiteUsingDefaultBrowser()
    {
        RunOnStaThread(() =>
        {
            var requests = new List<System.Diagnostics.ProcessStartInfo>();
            using var brand = new Pa55wordBrand(requests.Add);
            var wordmark = Assert.Single(brand.Controls.OfType<PictureBox>());
            var onClick = typeof(Control).GetMethod("OnClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

            onClick.Invoke(wordmark, new object[] { EventArgs.Empty });
            onClick.Invoke(brand, new object[] { EventArgs.Empty });

            Assert.Equal(2, requests.Count);
            Assert.All(requests, request =>
            {
                Assert.Equal("https://pa55word.com", request.FileName);
                Assert.True(request.UseShellExecute);
            });
            Assert.Equal(Cursors.Hand, wordmark.Cursor);
        });
    }

    [Fact]
    public void Brand_LoadsEmbeddedWordmarkWithoutTagline()
    {
        RunOnStaThread(() =>
        {
            using var brand = new Pa55wordBrand();
            var wordmark = Assert.Single(brand.Controls.OfType<PictureBox>());
            Assert.Empty(brand.Controls.OfType<Label>());

            Assert.NotNull(wordmark.Image);
            Assert.Equal(new Size(1000, 240), wordmark.Image.Size);
            Assert.Equal(PictureBoxSizeMode.Zoom, wordmark.SizeMode);
            Assert.Equal(new Size(140, 34), brand.Size);
            Assert.Equal(DockStyle.Fill, wordmark.Dock);
            Assert.False(brand.TabStop);
            using var bitmap = new Bitmap(wordmark.Image);
            Assert.Equal(Color.FromArgb(120, 205, 0).ToArgb(), bitmap.GetPixel(405, 10).ToArgb());
            Assert.Equal(0, bitmap.GetPixel(405, 25).A);
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        });
    }

    [Theory]
    [InlineData(false, (int)TrayMenuSection.Login)]
    [InlineData(true, (int)TrayMenuSection.Credentials)]
    public void Startup_SelectsSectionForActiveNamespace(bool authorized, int expected)
    {
        var controller = new TrayMenuController((_, _) => { }, () => { });
        controller.SetActiveSection(TrayMenuSection.Settings);

        controller.SetInitialSection(authorized);

        Assert.Equal((TrayMenuSection)expected, controller.ActiveSection);
    }

    [Fact]
    public void CredentialList_ShowsAllGroupsAndSelectsEntriesByIdWithoutFeedback()
    {
        RunOnStaThread(() =>
        {
            using var panel = new CredentialListPanel();
            var entries = new[]
            {
                new CredentialEntryListItem(1, "Same label", "group-a", "Group A", "", "", ""),
                new CredentialEntryListItem(2, "Same label", "group-b", "Group B", "", "", ""),
                new CredentialEntryListItem(3, "Ungrouped entry", "", "", "", "", "")
            };
            ListBox list = Assert.Single(panel.Controls.OfType<ListBox>());
            var selections = new List<CredentialEntryListItem>();
            panel.EntrySelected += selections.Add;

            panel.UpdateEntries(entries, "2", false, "");

            Assert.Equal(3, list.Items.Count);
            Assert.Same(entries[1], list.SelectedItem);
            Assert.Empty(selections);

            list.SelectedIndex = 2;

            Assert.Same(entries[2], Assert.Single(selections));
            panel.UpdateEntries(entries, "1", false, "");
            Assert.Equal(3, list.Items.Count);
            Assert.Same(entries[0], list.SelectedItem);
            Assert.Single(selections);

            var updatedEntry = entries[1] with { Label = "Renamed credential" };
            panel.UpdateEntries(new[] { updatedEntry }, "2", false, "");
            Assert.Same(updatedEntry, Assert.Single(list.Items.Cast<CredentialEntryListItem>()));
            Assert.Same(updatedEntry, list.SelectedItem);
            Assert.Single(selections);

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "");
            Assert.Empty(list.Items.Cast<CredentialEntryListItem>());
            Assert.Equal(-1, list.SelectedIndex);
        });
    }

    [Fact]
    public void CredentialList_DisablesSelectionWhileLoadingOrFailed()
    {
        RunOnStaThread(() =>
        {
            using var panel = new CredentialListPanel();
            ListBox list = Assert.Single(panel.Controls.OfType<ListBox>());

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", true, "");
            Assert.False(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "Loading credentials...");

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "Server unavailable");
            Assert.False(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "Server unavailable");

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "");
            Assert.True(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "No credentials available.");
        });
    }

    [Fact]
    public void CredentialList_MatchesDetailPanelHeightAndKeepsEditorWidth()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();
            using var navigation = new Panel { Dock = DockStyle.Left, Width = 177 };
            using var details = new Panel { Dock = DockStyle.Fill };
            using var list = new CredentialListPanel();
            content.Controls.Add(details);
            content.Controls.Add(list);
            content.Controls.Add(navigation);
            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window, showCredentialList: true);
            window.Show();
            window.PerformLayout();
            content.PerformLayout();

            Assert.Equal(details.Height, list.Height);
            Assert.Equal(list.Right, details.Left);
            Assert.True(details.Width >= 643);

            window.ClientSize = new Size(window.ClientSize.Width, 900);
            window.PerformLayout();
            content.PerformLayout();
            Assert.Equal(900 - WindowHeader.HeaderHeight, list.Height);
            Assert.Equal(details.Height, list.Height);

            list.Visible = false;
            MainWindowPresentation.Configure(window);
            window.ClientSize = MainWindowPresentation.MinimumClientSize;
            content.PerformLayout();
            Assert.Equal(643, details.Width);
        });
    }

    [Fact]
    public void Content_IsHostedOnFormAndFollowsClientSize()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();

            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window);
            window.Show();
            window.PerformLayout();

            Assert.Same(window, content.Parent);
            Assert.Equal(DockStyle.Fill, content.Dock);
            Assert.Equal(new Size(window.ClientSize.Width,
                window.ClientSize.Height - WindowHeader.HeaderHeight), content.Size);
            Assert.Equal(WindowHeader.HeaderHeight, content.Top);
            var header = Assert.Single(window.Controls.OfType<WindowHeader>());
            var headerLogo = Assert.Single(header.Controls.OfType<Pa55wordBrand>());
            Assert.Equal(new Size(140, 34), headerLogo.Size);
            Assert.Equal(Color.FromArgb(31, 33, 57), header.BackColor);
            var headerImage = Assert.Single(headerLogo.Controls.OfType<PictureBox>()).Image;
            Assert.NotNull(headerImage);
            using var headerBitmap = new Bitmap(headerImage);
            Assert.Equal(Color.FromArgb(247, 248, 250).ToArgb(), headerBitmap.GetPixel(40, 110).ToArgb());
            Assert.Equal(Color.FromArgb(120, 205, 0).ToArgb(),
                headerBitmap.GetPixel(405, 10).ToArgb());
            Assert.Equal(0, headerBitmap.GetPixel(405, 25).A);
            Assert.DoesNotContain(header.Controls.OfType<Button>(), button => button.Text == "+");
            Assert.Equal(new[] { "Minimize", "Close" },
                header.Controls.OfType<Button>().Select(button => button.AccessibleName));
            Assert.Contains(header.Controls.OfType<Label>(),
                label => label.Text == "It just feels right | Effortless by Design" &&
                    label.ForeColor == Color.FromArgb(247, 248, 250));
            var separator = Assert.Single(header.Controls.OfType<Panel>());
            Assert.Equal(Color.White, separator.BackColor);
            Assert.Equal(1, separator.Height);
            Assert.Equal(FormBorderStyle.Sizable, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.True(window.MinimizeBox);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal("pa55word It just feels right | Effortless by Design", window.Text);
            Assert.NotNull(window.Icon);
            Assert.Equal(window.Size.Width, window.MaximumSize.Width);
            Assert.Equal(int.MaxValue, window.MaximumSize.Height);
            Assert.True(window.ClientSize.Width >= MainWindowPresentation.MinimumClientSize.Width);
            Assert.True(window.ClientSize.Height >= MainWindowPresentation.MinimumClientSize.Height);

            int fixedWidth = window.ClientSize.Width;
            window.ClientSize = new Size(fixedWidth, 800);
            window.PerformLayout();

            Assert.Equal(new Size(fixedWidth, 800 - WindowHeader.HeaderHeight), content.Size);
            MainWindowPresentation.Configure(window);
            Assert.Equal(new Size(fixedWidth, 800), window.ClientSize);
        });
    }

    [Fact]
    public void HideAndReopen_PreservesContentAndWindowSize()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();
            using var input = new TextBox { Text = "unsaved value" };
            content.Controls.Add(input);
            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window);
            int fixedWidth = window.ClientSize.Width;
            window.ClientSize = new Size(fixedWidth, 800);
            MainWindowPresentation.Show(window);

            MainWindowPresentation.Hide(window);

            Assert.False(window.Visible);
            Assert.False(window.ShowInTaskbar);
            Assert.False(window.IsDisposed);

            MainWindowPresentation.Show(window);

            Assert.True(window.Visible);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal(new Size(fixedWidth, 800), window.ClientSize);
            Assert.Equal("unsaved value", input.Text);
        });
    }

    [Fact]
    public void RegistrationTransition_RestoresNormalWindowAndCanReturnToResizableLayout()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window);
            MainWindowPresentation.Show(window);
            window.WindowState = FormWindowState.Maximized;

            MainWindowPresentation.ConfigureRegistration(window);

            Assert.Equal(FormWindowState.Normal, window.WindowState);
            Assert.Equal(new Size(800, 450), window.ClientSize);
            Assert.Equal(FormBorderStyle.FixedSingle, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.Equal(Size.Empty, window.MaximumSize);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal("pa55word It just feels right | Effortless by Design Registration", window.Text);
            Assert.NotNull(window.Icon);

            MainWindowPresentation.Configure(window);

            Assert.Equal(FormBorderStyle.Sizable, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.True(window.ClientSize.Width >= MainWindowPresentation.MinimumClientSize.Width);
            Assert.True(window.ClientSize.Height >= MainWindowPresentation.MinimumClientSize.Height);
        });
    }

    [Fact]
    public void Reopen_RestoresMinimizedWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window);
            MainWindowPresentation.Show(window);
            window.WindowState = FormWindowState.Minimized;

            Assert.True(window.ShowInTaskbar);
            MainWindowPresentation.Show(window);

            Assert.True(window.Visible);
            Assert.Equal(FormWindowState.Normal, window.WindowState);
        });
    }

    private static void RunOnStaThread(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The window test did not finish in time.");
        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
