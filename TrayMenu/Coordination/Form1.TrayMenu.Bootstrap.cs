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
        private void ApplyRegistrationLayout()
        {
            CloseQuickPanel();
            _quickPanelContainer.Visible = false;
            restartRegistrationButton.Visible = false;
            SetRegistrationControlsVisible(true);
            ApplyRegistrationTheme();
            MainWindowPresentation.ConfigureRegistration(this);
        }

        private void ApplyRegistrationTheme()
        {
            BackColor = Color.FromArgb(31, 33, 57);
            ForeColor = Color.White;

            device_registration.ForeColor = Color.White;
            device_registration.Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point, 0);

            baseUrlLabel.ForeColor = Color.FromArgb(171, 174, 211);
            baseUrlLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            namespaceNameLabel.ForeColor = Color.FromArgb(171, 174, 211);
            namespaceNameLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

            baseUrlTextBox.BorderStyle = BorderStyle.FixedSingle;
            baseUrlTextBox.BackColor = Color.FromArgb(41, 43, 71);
            baseUrlTextBox.ForeColor = Color.White;
            baseUrlTextBox.Font = new Font("Segoe UI", 10F);

            namespaceNameTextBox.BorderStyle = BorderStyle.FixedSingle;
            namespaceNameTextBox.BackColor = Color.FromArgb(41, 43, 71);
            namespaceNameTextBox.ForeColor = Color.White;
            namespaceNameTextBox.Font = new Font("Segoe UI", 10F);

            continueRegistrationButton.FlatStyle = FlatStyle.Flat;
            continueRegistrationButton.FlatAppearance.BorderSize = 0;
            continueRegistrationButton.BackColor = Color.FromArgb(120, 205, 0);
            continueRegistrationButton.ForeColor = Color.FromArgb(24, 30, 24);
            continueRegistrationButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            continueRegistrationButton.UseVisualStyleBackColor = false;

            restartRegistrationButton.FlatStyle = FlatStyle.Flat;
            restartRegistrationButton.FlatAppearance.BorderColor = Color.FromArgb(101, 106, 146);
            restartRegistrationButton.BackColor = Color.FromArgb(67, 72, 106);
            restartRegistrationButton.ForeColor = Color.White;
            restartRegistrationButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            restartRegistrationButton.UseVisualStyleBackColor = false;

            pictureBoxQr.BackColor = Color.FromArgb(41, 43, 71);
            pictureBoxQr.BorderStyle = BorderStyle.FixedSingle;
        }

        private void SetRegistrationControlsVisible(bool visible)
        {
            pictureBoxQr.Visible = visible && pictureBoxQr.Visible;
            device_registration.Visible = visible;
            baseUrlLabel.Visible = visible && baseUrlLabel.Visible;
            baseUrlTextBox.Visible = visible && baseUrlTextBox.Visible;
            continueRegistrationButton.Visible = visible && continueRegistrationButton.Visible;
            namespaceNameLabel.Visible = visible && namespaceNameLabel.Visible;
            namespaceNameTextBox.Visible = visible && namespaceNameTextBox.Visible;
            restartRegistrationButton.Visible = visible && restartRegistrationButton.Visible;
        }

        private void restartRegistrationButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            pictureBoxQr.Image = null;

            if (_registrationPromptMode == RegistrationPromptMode.ServerRegistration)
            {
                ShowNamespaceSyncPrompt();
            }
            else if (_registrationPromptMode == RegistrationPromptMode.NewServerRegistration)
            {
                ShowServerRegistrationPrompt();
            }
            else
            {
                ShowBaseUrlPrompt();
            }
        }
    }
}
