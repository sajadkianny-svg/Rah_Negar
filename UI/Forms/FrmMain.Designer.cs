namespace Rah_Negar.UI.Forms
{
    partial class FrmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMain));
            pnlBody = new Panel();
            pnlDivider = new Panel();
            cardRecords = new Panel();
            lblRecordsTitle = new Label();
            cardReports = new Panel();
            lblReportsTitle = new Label();
            cardSettings = new Panel();
            lblSettingsTitle = new Label();
            picLogo = new PictureBox();
            pnlFooter = new Panel();
            lblStatus = new Label();
            pnlBody.SuspendLayout();
            cardRecords.SuspendLayout();
            cardReports.SuspendLayout();
            cardSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picLogo).BeginInit();
            pnlFooter.SuspendLayout();
            SuspendLayout();
            // 
            // pnlBody
            // 
            pnlBody.Controls.Add(pnlDivider);
            pnlBody.Controls.Add(cardRecords);
            pnlBody.Controls.Add(cardReports);
            pnlBody.Controls.Add(cardSettings);
            pnlBody.Controls.Add(picLogo);
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.Location = new Point(0, 0);
            pnlBody.Name = "pnlBody";
            pnlBody.Padding = new Padding(18, 16, 18, 10);
            pnlBody.Size = new Size(634, 331);
            pnlBody.TabIndex = 1;
            // 
            // pnlDivider
            // 
            pnlDivider.BackColor = Color.Black;
            pnlDivider.Location = new Point(0, 309);
            pnlDivider.Name = "pnlDivider";
            pnlDivider.Size = new Size(645, 1);
            pnlDivider.TabIndex = 14;
            // 
            // cardRecords
            // 
            cardRecords.Controls.Add(lblRecordsTitle);
            cardRecords.Location = new Point(0, 269);
            cardRecords.Name = "cardRecords";
            cardRecords.Size = new Size(211, 40);
            cardRecords.TabIndex = 13;
            // 
            // lblRecordsTitle
            // 
            lblRecordsTitle.AutoSize = true;
            lblRecordsTitle.Dock = DockStyle.Fill;
            lblRecordsTitle.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblRecordsTitle.Location = new Point(0, 0);
            lblRecordsTitle.Name = "lblRecordsTitle";
            lblRecordsTitle.Size = new Size(67, 13);
            lblRecordsTitle.TabIndex = 2;
            lblRecordsTitle.Text = "Data Entry";
            lblRecordsTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // cardReports
            // 
            cardReports.Controls.Add(lblReportsTitle);
            cardReports.Location = new Point(210, 269);
            cardReports.Name = "cardReports";
            cardReports.Size = new Size(213, 40);
            cardReports.TabIndex = 12;
            // 
            // lblReportsTitle
            // 
            lblReportsTitle.AutoSize = true;
            lblReportsTitle.Dock = DockStyle.Fill;
            lblReportsTitle.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblReportsTitle.Location = new Point(0, 0);
            lblReportsTitle.Name = "lblReportsTitle";
            lblReportsTitle.Size = new Size(87, 13);
            lblReportsTitle.TabIndex = 1;
            lblReportsTitle.Text = "Report Center";
            lblReportsTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // cardSettings
            // 
            cardSettings.Controls.Add(lblSettingsTitle);
            cardSettings.Location = new Point(423, 269);
            cardSettings.Name = "cardSettings";
            cardSettings.Size = new Size(212, 40);
            cardSettings.TabIndex = 11;
            // 
            // lblSettingsTitle
            // 
            lblSettingsTitle.AutoSize = true;
            lblSettingsTitle.Dock = DockStyle.Fill;
            lblSettingsTitle.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblSettingsTitle.Location = new Point(0, 0);
            lblSettingsTitle.Name = "lblSettingsTitle";
            lblSettingsTitle.Size = new Size(54, 13);
            lblSettingsTitle.TabIndex = 0;
            lblSettingsTitle.Text = "Settings";
            lblSettingsTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // picLogo
            // 
            picLogo.Anchor = AnchorStyles.Top;
            picLogo.Image = (Image)resources.GetObject("picLogo.Image");
            picLogo.Location = new Point(220, 51);
            picLogo.Name = "picLogo";
            picLogo.Size = new Size(196, 195);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.TabIndex = 9;
            picLogo.TabStop = false;
            // 
            // pnlFooter
            // 
            pnlFooter.Controls.Add(lblStatus);
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Location = new Point(0, 310);
            pnlFooter.Name = "pnlFooter";
            pnlFooter.Size = new Size(634, 21);
            pnlFooter.TabIndex = 3;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Tahoma", 8F);
            lblStatus.Location = new Point(628, 2);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(41, 13);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "وضعیت";
            // 
            // FrmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.WhiteSmoke;
            ClientSize = new Size(634, 331);
            Controls.Add(pnlFooter);
            Controls.Add(pnlBody);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "FrmMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Rah_Negar";
            pnlBody.ResumeLayout(false);
            cardRecords.ResumeLayout(false);
            cardRecords.PerformLayout();
            cardReports.ResumeLayout(false);
            cardReports.PerformLayout();
            cardSettings.ResumeLayout(false);
            cardSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picLogo).EndInit();
            pnlFooter.ResumeLayout(false);
            pnlFooter.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private Panel pnlBody;
        private Panel pnlFooter;
        private PictureBox picLogo;
        private Panel cardReports;
        private Panel cardSettings;
        private Panel cardRecords;
        private Label lblSettingsTitle;
        private Label lblReportsTitle;
        private Label lblRecordsTitle;
        private Label lblStatus;
        private Panel pnlDivider;
    }
}