using System.Drawing;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Presenters;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrayApp
{
    public partial class Form1
    {
        private bool IsQuickPanelOpen()
        {
            return Visible && _quickPanelContainer.Visible && WindowState != FormWindowState.Minimized;
        }

        private void CloseQuickPanel()
        {
            _quickPanelContainer.Visible = false;
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override bool ProcessDialogKey(Keys keyData)
            {
                if ((keyData & Keys.KeyCode) == Keys.Tab)
                {
                    List<Control> inputControls = GetVisibleInputControls(this);
                    Control? focusedControl = inputControls.FirstOrDefault(control => control.Focused);

                    if (focusedControl != null && inputControls.Count > 1)
                    {
                        bool forward = (keyData & Keys.Shift) != Keys.Shift;
                        int currentIndex = inputControls.IndexOf(focusedControl);
                        int nextIndex = (currentIndex + (forward ? 1 : inputControls.Count - 1)) % inputControls.Count;
                        inputControls[nextIndex].Focus();
                        return true;
                    }
                }

                return base.ProcessDialogKey(keyData);
            }

            private static List<Control> GetVisibleInputControls(Control parent)
            {
                var inputControls = new List<Control>();
                AddVisibleInputControls(parent, inputControls);
                return inputControls
                    .OrderBy(control => control.PointToScreen(Point.Empty).Y)
                    .ThenBy(control => control.PointToScreen(Point.Empty).X)
                    .ToList();
            }

            private static void AddVisibleInputControls(Control parent, ICollection<Control> inputControls)
            {
                foreach (Control child in parent.Controls)
                {
                    if (child.Visible && child.Enabled && child.TabStop && (child is TextBoxBase || child is ComboBox))
                    {
                        inputControls.Add(child);
                    }

                    AddVisibleInputControls(child, inputControls);
                }
            }
        }

        private void UpdateSectionPresentation(TrayMenuViewState viewState)
        {
            bool loginSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Login;
            bool settingsUsersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers;
            bool settingsServersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsServers;
            bool settingsActionSectionActive = settingsUsersSectionActive || settingsServersSectionActive;
            bool serversSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Servers;
            bool credentialsSectionActive = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            bool showInlineRegistration = serversSectionActive && _showInlineServerRegistration;
            bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
            _databaseView.GuidancePanel.Visible = showInlineRegistration;
            bool credentialGroupManagerActive = credentialsSectionActive &&
                _credentialsPresenter.WorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup;
            bool isAuthorizedForActiveNamespace = _authenticationService.IsAuthorizedForActiveNamespace;
            bool canStartRegistration = _authenticationService.IsAuthorized && !_authenticationInProgress;
            bool canLoginToActiveNamespace = _authenticationService.CanLoginForActiveNamespace &&
                !_authenticationInProgress;
            bool canLogoutFromActiveNamespace = isAuthorizedForActiveNamespace && !_authenticationInProgress;

            bool showInlineUserRegistration = settingsUsersSectionActive && _showInlineUserRegistration;
            bool showInlineUserDeletion = settingsUsersSectionActive && _showInlineUserDeletion;
            if (showInlineUserDeletion)
            {
                _quickPanelDetailTitleLabel.Text = "Delete user profile";
                _quickPanelDetailDescriptionLabel.Text = string.Empty;
            }

            _quickPanelRegisterNewServerInlineButton.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelRegisterNewServerInlineButton.Enabled = canStartRegistration;
            _quickPanelServerRegistrationInlineButton.Visible = serversSectionActive;
            _quickPanelServerRegistrationInlineButton.Enabled = !_inlineServerRegistrationAwaitingCompletion && !_inlineServerRegistrationSubmitting && canStartRegistration;
            _quickPanelServerStatusValueLabel.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Enabled = canLoginToActiveNamespace || canLogoutFromActiveNamespace;
            _quickPanelServerLoginInlineButton.Text = _authenticationInProgress
                ? isAuthorizedForActiveNamespace ? "Logging out..." : "Logging in..."
                : isAuthorizedForActiveNamespace ? "Logout" : "Login";
            _quickPanelSelectionCard.Visible = !credentialGroupManagerActive && !credentialsSectionActive;
            _quickPanelMetricsCard.Visible = !credentialsSectionActive && !loginSectionActive && !settingsActionSectionActive;
            _quickPanelMetricsCard.Location = new Point(32, 294);
            _quickPanelInsightCard.Location = new Point(32, 424);

            if (_authenticationInProgress)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            }
            else if (canLoginToActiveNamespace)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(120, 205, 0);
                _quickPanelServerLoginInlineButton.ForeColor = Color.FromArgb(24, 30, 24);
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);
            }
            else if (canLogoutFromActiveNamespace)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(214, 88, 88);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(214, 88, 88);
            }
            else
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            }

            if (credentialsSectionActive)
            {
                _quickPanelRegisterNewServerInlineButton.Visible = false;
                _quickPanelServerRegistrationInlineButton.Visible = false;
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Visible = false;
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!serversSectionActive)
            {
                if (loginSectionActive)
                {
                    _quickPanelSelectionCard.Visible = false;
                    _quickPanelInsightCard.Visible = false;
                    _databaseView.SetServerRegistrationControlsVisible(false);
                    return;
                }

                if (settingsActionSectionActive)
                {
                    if (settingsUsersSectionActive && (showInlineUserRegistration || showInlineUserDeletion))
                    {
                        _quickPanelSelectionCard.Visible = false;
                    }

                    _quickPanelInsightCard.Visible = false;
                    _databaseView.SetServerRegistrationControlsVisible(false);
                    return;
                }

                _quickPanelInsightCard.Visible = true;
                _quickPanelInsightCard.Size = new Size(578, 110);
                _quickPanelInsightTitleLabel.Location = new Point(16, 16);
                _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
                _quickPanelInsightBodyLabel.Text = viewState.InsightBody;
                _quickPanelInsightBodyLabel.Size = new Size(544, 56);
                _quickPanelInsightBodyLabel.Visible = true;
                _quickPanelRegisterNewServerInlineButton.Visible = false;
                _quickPanelServerRegistrationInlineButton.Visible = false;
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Visible = false;
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!showInlineRegistration)
            {
                _quickPanelSelectionCard.Size = new Size(578, 108);
                _quickPanelInsightCard.Size = new Size(578, 110);
                _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
                _quickPanelInsightBodyLabel.Text = viewState.InsightBody;
                _quickPanelInsightBodyLabel.Size = new Size(544, 56);
                _quickPanelInsightBodyLabel.Visible = true;
                _quickPanelRegisterNewServerInlineButton.Text = "+ Add a server";
                _quickPanelRegisterNewServerInlineButton.Location = new Point(16, 14);
                _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
                _quickPanelServerRegistrationInlineButton.Text = "+ Sync existing server";
                _quickPanelServerRegistrationInlineButton.Location = new Point(180, 14);
                _quickPanelServerRegistrationInlineButton.Size = new Size(170, 28);
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Location = new Point(438, 14);
                _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelDetailTitleLabel.Text = isNewServerRegistration ? "Add a server" : "Sync existing server";
            _quickPanelInsightCard.Visible = false;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerRegistrationInlineButton.Visible = false;
            _databaseView.ApplyRegistrationState(isNewServerRegistration,
                _inlineServerRegistrationSubmitting, _inlineServerRegistrationAwaitingCompletion,
                _serverRegistrationStatusMessage);
        }
    }
}