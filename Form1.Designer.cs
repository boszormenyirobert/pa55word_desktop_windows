namespace TrayApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.notifyIcon1 = new NotifyIcon(this.components);
            this.contextMenuStrip1 = new ContextMenuStrip(this.components);
            this.loginToolStripMenuItem = new ToolStripMenuItem();
            this.logoutToolStripMenuItem = new ToolStripMenuItem();
            this.registerServerToolStripMenuItem = new ToolStripMenuItem();
            this.exitToolStripMenuItem = new ToolStripMenuItem();
            this.pictureBoxQr = new PictureBox();
            this.device_registration = new Label();
            this.baseUrlLabel = new Label();
            this.baseUrlTextBox = new TextBox();
            this.continueRegistrationButton = new Button();
            this.namespaceNameLabel = new Label();
            this.namespaceNameTextBox = new TextBox();
            this.contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.pictureBoxQr).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            this.notifyIcon1.Text = "notifyIcon1";
            this.notifyIcon1.Visible = true;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.AutoSize = false;
            this.contextMenuStrip1.BackColor = Color.LightSkyBlue;
            this.contextMenuStrip1.Font = new Font("Segoe UI", 10F);
            this.contextMenuStrip1.Items.AddRange(new ToolStripItem[] { this.loginToolStripMenuItem, this.logoutToolStripMenuItem, this.registerServerToolStripMenuItem, this.exitToolStripMenuItem });
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.RenderMode = ToolStripRenderMode.System;
            this.contextMenuStrip1.Size = new Size(500, 300);
            // 
            // loginToolStripMenuItem
            // 
            this.loginToolStripMenuItem.Name = "loginToolStripMenuItem";
            this.loginToolStripMenuItem.Size = new Size(180, 24);
            this.loginToolStripMenuItem.Text = "login";
            // 
            // logoutToolStripMenuItem
            // 
            this.logoutToolStripMenuItem.Name = "logoutToolStripMenuItem";
            this.logoutToolStripMenuItem.Size = new Size(180, 24);
            this.logoutToolStripMenuItem.Text = "logout";
            this.logoutToolStripMenuItem.Click += this.logoutToolStripMenuItem_Click;
            // 
            // registerServerToolStripMenuItem
            // 
            this.registerServerToolStripMenuItem.Name = "registerServerToolStripMenuItem";
            this.registerServerToolStripMenuItem.Size = new Size(180, 24);
            this.registerServerToolStripMenuItem.Text = "register server";
            this.registerServerToolStripMenuItem.Click += this.registerServerToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new Size(180, 24);
            this.exitToolStripMenuItem.Text = "exit";
            this.exitToolStripMenuItem.Click += this.exitToolStripMenuItem_Click;
            // 
            // pictureBoxQr
            // 
            this.pictureBoxQr.Location = new Point(231, 164);
            this.pictureBoxQr.Name = "pictureBoxQr";
            this.pictureBoxQr.Size = new Size(329, 259);
            this.pictureBoxQr.TabIndex = 1;
            this.pictureBoxQr.TabStop = false;
            // 
            // device_registration
            // 
            this.device_registration.AutoSize = true;
            this.device_registration.Font = new Font("Segoe UI", 27.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            this.device_registration.Location = new Point(231, 54);
            this.device_registration.Name = "device_registration";
            this.device_registration.Size = new Size(329, 50);
            this.device_registration.TabIndex = 2;
            this.device_registration.Text = "Device registration";
            // 
            // baseUrlLabel
            // 
            this.baseUrlLabel.AutoSize = true;
            this.baseUrlLabel.Location = new Point(231, 164);
            this.baseUrlLabel.Name = "baseUrlLabel";
            this.baseUrlLabel.Size = new Size(48, 15);
            this.baseUrlLabel.TabIndex = 3;
            this.baseUrlLabel.Text = "Base url";
            this.baseUrlLabel.Visible = false;
            // 
            // baseUrlTextBox
            // 
            this.baseUrlTextBox.Location = new Point(231, 190);
            this.baseUrlTextBox.Name = "baseUrlTextBox";
            this.baseUrlTextBox.Size = new Size(329, 23);
            this.baseUrlTextBox.TabIndex = 4;
            this.baseUrlTextBox.Visible = false;
            // 
            // continueRegistrationButton
            // 
            this.continueRegistrationButton.Location = new Point(231, 294);
            this.continueRegistrationButton.Name = "continueRegistrationButton";
            this.continueRegistrationButton.Size = new Size(329, 32);
            this.continueRegistrationButton.TabIndex = 5;
            this.continueRegistrationButton.Text = "Continue";
            this.continueRegistrationButton.UseVisualStyleBackColor = true;
            this.continueRegistrationButton.Visible = false;
            this.continueRegistrationButton.Click += this.continueRegistrationButton_Click;
            // 
            // namespaceNameLabel
            // 
            this.namespaceNameLabel.AutoSize = true;
            this.namespaceNameLabel.Location = new Point(231, 229);
            this.namespaceNameLabel.Name = "namespaceNameLabel";
            this.namespaceNameLabel.Size = new Size(102, 15);
            this.namespaceNameLabel.TabIndex = 6;
            this.namespaceNameLabel.Text = "Namespace name";
            this.namespaceNameLabel.Visible = false;
            // 
            // namespaceNameTextBox
            // 
            this.namespaceNameTextBox.Location = new Point(231, 255);
            this.namespaceNameTextBox.Name = "namespaceNameTextBox";
            this.namespaceNameTextBox.Size = new Size(329, 23);
            this.namespaceNameTextBox.TabIndex = 7;
            this.namespaceNameTextBox.Visible = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(800, 450);
            this.Controls.Add(this.namespaceNameTextBox);
            this.Controls.Add(this.namespaceNameLabel);
            this.Controls.Add(this.continueRegistrationButton);
            this.Controls.Add(this.baseUrlTextBox);
            this.Controls.Add(this.baseUrlLabel);
            this.Controls.Add(this.device_registration);
            this.Controls.Add(this.pictureBoxQr);
            this.Name = "Form1";
            this.Text = "Form1";
            this.contextMenuStrip1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)this.pictureBoxQr).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private NotifyIcon notifyIcon1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem loginToolStripMenuItem;
        private ToolStripMenuItem logoutToolStripMenuItem;
        private ToolStripMenuItem registerServerToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private PictureBox pictureBoxQr;
        private Label device_registration;
        private Label baseUrlLabel;
        private TextBox baseUrlTextBox;
        private Button continueRegistrationButton;
        private Label namespaceNameLabel;
        private TextBox namespaceNameTextBox;
    }
}
