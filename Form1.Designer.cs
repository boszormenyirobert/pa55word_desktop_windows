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
            this.pictureBoxQr = new PictureBox();
            this.device_registration = new Label();
            this.baseUrlLabel = new Label();
            this.baseUrlTextBox = new TextBox();
            this.continueRegistrationButton = new Button();
            this.restartRegistrationButton = new Button();
            this.namespaceNameLabel = new Label();
            this.namespaceNameTextBox = new TextBox();
            ((System.ComponentModel.ISupportInitialize)this.pictureBoxQr).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            this.notifyIcon1.Text = "notifyIcon1";
            this.notifyIcon1.Visible = true;
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
            // restartRegistrationButton
            //
            this.restartRegistrationButton.Location = new Point(590, 274);
            this.restartRegistrationButton.Name = "restartRegistrationButton";
            this.restartRegistrationButton.Size = new Size(130, 32);
            this.restartRegistrationButton.TabIndex = 8;
            this.restartRegistrationButton.Text = "Restart";
            this.restartRegistrationButton.UseVisualStyleBackColor = true;
            this.restartRegistrationButton.Visible = false;
            this.restartRegistrationButton.Click += this.restartRegistrationButton_Click;
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
            this.Controls.Add(this.restartRegistrationButton);
            this.Controls.Add(this.continueRegistrationButton);
            this.Controls.Add(this.baseUrlTextBox);
            this.Controls.Add(this.baseUrlLabel);
            this.Controls.Add(this.device_registration);
            this.Controls.Add(this.pictureBoxQr);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)this.pictureBoxQr).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private NotifyIcon notifyIcon1;
        private PictureBox pictureBoxQr;
        private Label device_registration;
        private Label baseUrlLabel;
        private TextBox baseUrlTextBox;
        private Button continueRegistrationButton;
        private Button restartRegistrationButton;
        private Label namespaceNameLabel;
        private TextBox namespaceNameTextBox;
    }
}
