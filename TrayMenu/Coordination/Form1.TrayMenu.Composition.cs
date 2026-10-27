using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;

namespace TrayApp
{
    public partial class Form1
    {
        private void BuildQuickPanel()
        {
            SuspendLayout();

            _quickPanelContainer.Padding = new Padding(0);
            _quickPanelContainer.Margin = Padding.Empty;
            _quickPanelContainer.Location = Point.Empty;
            _quickPanelContainer.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelContainer.Visible = false;
            _quickPanelContainer.Size = new Size(820, InitialMainWindowHeight);

            _quickPanelDividerPanel.BackColor = Color.FromArgb(59, 61, 88);
            _quickPanelDividerPanel.Location = new Point(176, 0);
            _quickPanelDividerPanel.Size = new Size(1, InitialMainWindowHeight);
            _quickPanelDividerPanel.Dock = DockStyle.Left;

            _quickPanelDetailPanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelDetailPanel.Location = new Point(177, 0);
            _quickPanelDetailPanel.Size = new Size(643, InitialMainWindowHeight);
            _quickPanelDetailPanel.Dock = DockStyle.Fill;

            _sidebar.SettingsRequested += settingsNavButton_Click;
            _sidebar.PrimaryRequested += quickPanelPrimaryActionButton_Click;
            _sidebar.RefreshRequested += quickPanelRefreshButton_Click;
            _sidebar.ServerSelected += async (_, request) => await SelectActiveServerAsync(request.NamespaceId);
            _trayMenuController = new TrayMenuController(SidebarControl.SetNavigationState, HandleTrayMenuSectionChanged);
            _trayMenuController.Register(TrayMenuSection.Login, _sidebar.LoginNavButton);
            _trayMenuController.Register(TrayMenuSection.Credentials, _sidebar.CredentialsNavButton);
            _trayMenuController.Register(TrayMenuSection.Faq, _sidebar.FaqNavButton);

            _quickPanelDetailEyebrowLabel.AutoSize = true;
            _quickPanelDetailEyebrowLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelDetailEyebrowLabel.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelDetailEyebrowLabel.Location = new Point(34, 22);
            _quickPanelDetailEyebrowLabel.Text = "SYSTEM STATUS";

            _quickPanelDetailTitleLabel.AutoSize = true;
            _quickPanelDetailTitleLabel.Font = _quickPanelDetailDefaultTitleFont;
            _quickPanelDetailTitleLabel.ForeColor = Color.White;
            _quickPanelDetailTitleLabel.Location = new Point(32, 52);
            _quickPanelDetailTitleLabel.Text = "Tray overview";

            _quickPanelDetailDescriptionLabel.AutoSize = false;
            _quickPanelDetailDescriptionLabel.Font = new Font("Segoe UI", 10F);
            _quickPanelDetailDescriptionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelDetailDescriptionLabel.Location = new Point(36, 98);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);

            _quickPanelSelectionCard.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelSelectionCard.BorderStyle = BorderStyle.None;
            _quickPanelSelectionCard.Location = new Point(32, 152);
            _quickPanelSelectionCard.Size = new Size(578, 126);

            _quickPanelSelectionCard.Controls.Add(_quickPanelRegisterNewServerInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerRegistrationInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerStatusValueLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerLoginInlineButton);

            InitializeSettingsOverviewPanel();

            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeActiveUserLabel);
            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeActiveServerLabel);
            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeSessionLabel);

            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeActiveUserLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeActiveServerLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeSessionLabel);

            _quickPanelMetricsCard.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelMetricsCard.BorderStyle = BorderStyle.None;
            _quickPanelMetricsCard.Location = new Point(32, 294);
            _quickPanelMetricsCard.Size = new Size(578, 120);

            AddMetricRow(_quickPanelMetricsCard, 14, "Account", _quickPanelAccountValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 44, "Device", _quickPanelDeviceValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 74, "Connection", _quickPanelConnectionValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 104, "Updated", _quickPanelLastUpdatedValueLabel);

            _quickPanelInsightCard.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelInsightCard.BorderStyle = BorderStyle.None;
            _quickPanelInsightCard.Location = new Point(32, 424);
            _quickPanelInsightCard.Size = new Size(578, 110);

            _quickPanelInsightTitleLabel.AutoSize = true;
            _quickPanelInsightTitleLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _quickPanelInsightTitleLabel.ForeColor = Color.White;
            _quickPanelInsightTitleLabel.Location = new Point(16, 16);
            _quickPanelInsightTitleLabel.Text = "Overview";

            _quickPanelInsightBodyLabel.AutoSize = false;
            _quickPanelInsightBodyLabel.Font = new Font("Segoe UI", 9.5F);
            _quickPanelInsightBodyLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelInsightBodyLabel.Location = new Point(16, 42);
            _quickPanelInsightBodyLabel.Size = new Size(544, 56);

            InitializeDatabaseView();

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelRegisterNewServerInlineButton, "+ Add a database", new Point(16, 14), quickPanelRegisterNewServerInlineButton_Click);
            _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
            _quickPanelRegisterNewServerInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelServerRegistrationInlineButton, "+ Grant database access", new Point(180, 14), quickPanelServerRegistrationInlineButton_Click);
            _quickPanelServerRegistrationInlineButton.Size = new Size(190, 28);
            _quickPanelServerRegistrationInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            _quickPanelServerStatusValueLabel.AutoSize = false;
            _quickPanelServerStatusValueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelServerStatusValueLabel.Location = new Point(214, 16);
            _quickPanelServerStatusValueLabel.Size = new Size(108, 24);
            _quickPanelServerStatusValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            _quickPanelServerStatusValueLabel.Visible = false;

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelServerLoginInlineButton, "Login", new Point(438, 14), quickPanelServerLoginInlineButton_Click);
            _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
            _quickPanelServerLoginInlineButton.Visible = false;

            _quickPanelInsightCard.Controls.Add(_quickPanelInsightTitleLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelInsightBodyLabel);

            InitializeCredentialWorkspacePanel();

            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailEyebrowLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailTitleLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailDescriptionLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelSelectionCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelMetricsCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelInsightCard);
            _quickPanelDetailPanel.Controls.Add(_settingsView);
            _quickPanelDetailPanel.Controls.Add(_credentialWorkspace);
            _quickPanelDetailPanel.Controls.Add(_profileView);
            _quickPanelDetailPanel.Controls.Add(_databaseView);
            _quickPanelDetailPanel.Controls.Add(_loginView);
            _quickPanelDetailPanel.Controls.Add(_credentialSelection);
            _loginView.ProfileChanged += HandleLoginProfileChanged;
            _loginView.ServerChanged += HandleLoginServerChanged;
            _loginView.LoginRequested += quickPanelServerLoginInlineButton_Click;

            _quickPanelDetailPanel.Controls.Add(_accountsView);
            _accountsView.RemoveRequested += async (_, request) =>
                await DeleteAccountFromQuickPanelAsync(request.UserId, request.NamespaceItem);

            _devicesView.SourceChanged += quickPanelDevicesSourceComboBox_SelectedIndexChanged;
            _devicesView.DeletionConfirmationRequested += (_, item) =>
            {
                _devicePendingDeletionKey = item.IdentityKey;
                UpdateDevicesPresentation();
            };
            _devicesView.DeletionCancelled += (_, _) =>
            {
                _devicePendingDeletionKey = string.Empty;
                UpdateQuickPanelState();
            };
            _devicesView.DeleteRequested += async (_, item) => await DeleteDeviceFromQuickPanelAsync(item);

            _quickPanelDetailPanel.Controls.Add(_devicesView);

            _quickPanelDetailPanel.Controls.Add(_quickPanelFaqPanel);

            _quickPanelContainer.Controls.Add(_quickPanelDetailPanel);
            _quickPanelCredentialListCollapseButton.Click += quickPanelCredentialListCollapseButton_Click;
            _quickPanelCredentialListCollapseButton.Visible = false;
            _quickPanelCredentialListPanel.Visible = false;
            _quickPanelCredentialListPanel.EntrySelected += SelectCredentialFromList;
            _quickPanelCredentialListPanel.SizeChanged += (_, _) => LayoutCredentialListCollapseButton();
            _quickPanelContainer.SizeChanged += (_, _) => LayoutCredentialListCollapseButton();
            _quickPanelContainer.Controls.Add(_quickPanelCredentialListCollapseButton);
            _quickPanelContainer.Controls.Add(_quickPanelCredentialListPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDividerPanel);
            _quickPanelContainer.Controls.Add(_sidebar);

            ResumeLayout(performLayout: false);
        }

        private static void ConfigureQuickPanelSummaryValueLabel(Label label)
        {
            label.AutoSize = false;
            label.Font = new Font("Segoe UI", 10F);
            label.ForeColor = Color.White;
            label.Size = new Size(544, 22);
            label.Visible = false;
        }

        private static void AddMetricRow(Control parent, int y, string caption, Label valueLabel)
        {
            var captionLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(16, y),
                Text = caption
            };

            valueLabel.AutoSize = false;
            valueLabel.Font = new Font("Segoe UI", 9F);
            valueLabel.ForeColor = Color.White;
            valueLabel.Location = new Point(132, y - 1);
            valueLabel.Size = new Size(428, 18);
            valueLabel.TextAlign = ContentAlignment.MiddleRight;

            parent.Controls.Add(captionLabel);
            parent.Controls.Add(valueLabel);
        }
    }
}
