namespace Rah_Negar.UI.Forms
{
    partial class FrmSettings
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
            pnlHeader = new Panel();
            lblTitle = new Label();
            lblSubTitle = new Label();
            rdoThemeClassicSoftAccent = new RadioButton();
            rdoThemeClassicNeutral = new RadioButton();
            pnlFooter = new Panel();
            btnClose = new Button();
            btnResetFactory = new Button();
            btnAbout = new Button();
            lblDatabaseDetails = new Label();
            pnlBody = new Panel();
            grpRuntimeSettings = new GroupBox();
            grbBaseLine = new GroupBox();
            label2 = new Label();
            label1 = new Label();
            txtDataStartDateInfo = new TextBox();
            btnUpdateDataStartDate = new Button();
            cmbDataStartMonth = new ComboBox();
            cmbDataStartYear = new ComboBox();
            btnSave = new Button();
            ChAddHoursAfterEsd = new CheckBox();
            txtEsdExtraHours = new TextBox();
            gpPassword = new GroupBox();
            lblPasswordDetails = new Label();
            btnResetPassword = new Button();
            btnChangeLoginPassword = new Button();
            gpDatabase = new GroupBox();
            panel1 = new Panel();
            btnRepairDatabase = new Button();
            btnImportDatabase = new Button();
            btnExportDatabase = new Button();
            gbTheme = new GroupBox();
            rdoIndustrialRed = new RadioButton();
            rdoIndigoViolet = new RadioButton();
            rdoThemeTerracottaStone = new RadioButton();
            rdoThemeOlive = new RadioButton();
            rdoThemeGraphite = new RadioButton();
            rdoThemeBlue = new RadioButton();
            pnlHeader.SuspendLayout();
            pnlFooter.SuspendLayout();
            pnlBody.SuspendLayout();
            grpRuntimeSettings.SuspendLayout();
            grbBaseLine.SuspendLayout();
            gpPassword.SuspendLayout();
            gpDatabase.SuspendLayout();
            panel1.SuspendLayout();
            gbTheme.SuspendLayout();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubTitle);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(704, 55);
            pnlHeader.TabIndex = 4;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            lblTitle.Location = new Point(25, 14);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(59, 14);
            lblTitle.TabIndex = 2;
            lblTitle.Text = "Settings";
            // 
            // lblSubTitle
            // 
            lblSubTitle.AutoSize = true;
            lblSubTitle.Font = new Font("Segoe UI", 8F);
            lblSubTitle.Location = new Point(25, 31);
            lblSubTitle.Name = "lblSubTitle";
            lblSubTitle.Size = new Size(149, 13);
            lblSubTitle.TabIndex = 1;
            lblSubTitle.Text = "System Configuration Panel";
            // 
            // rdoThemeClassicSoftAccent
            // 
            rdoThemeClassicSoftAccent.AutoSize = true;
            rdoThemeClassicSoftAccent.Location = new Point(428, 26);
            rdoThemeClassicSoftAccent.Name = "rdoThemeClassicSoftAccent";
            rdoThemeClassicSoftAccent.Size = new Size(84, 17);
            rdoThemeClassicSoftAccent.TabIndex = 6;
            rdoThemeClassicSoftAccent.TabStop = true;
            rdoThemeClassicSoftAccent.Text = "کلاسیک گرم";
            rdoThemeClassicSoftAccent.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeClassicSoftAccent.UseVisualStyleBackColor = true;
            rdoThemeClassicSoftAccent.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoThemeClassicNeutral
            // 
            rdoThemeClassicNeutral.AutoSize = true;
            rdoThemeClassicNeutral.Location = new Point(527, 26);
            rdoThemeClassicNeutral.Name = "rdoThemeClassicNeutral";
            rdoThemeClassicNeutral.Size = new Size(93, 17);
            rdoThemeClassicNeutral.TabIndex = 5;
            rdoThemeClassicNeutral.TabStop = true;
            rdoThemeClassicNeutral.Text = "کلاسیک خنثی";
            rdoThemeClassicNeutral.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeClassicNeutral.UseVisualStyleBackColor = true;
            rdoThemeClassicNeutral.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // pnlFooter
            // 
            pnlFooter.Controls.Add(btnClose);
            pnlFooter.Controls.Add(btnResetFactory);
            pnlFooter.Controls.Add(btnAbout);
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Location = new Point(0, 345);
            pnlFooter.Name = "pnlFooter";
            pnlFooter.Size = new Size(704, 44);
            pnlFooter.TabIndex = 7;
            // 
            // btnClose
            // 
            btnClose.BackColor = Color.Gainsboro;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("Tahoma", 8F);
            btnClose.Location = new Point(609, 9);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(80, 25);
            btnClose.TabIndex = 7;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += btnClose_Click;
            // 
            // btnResetFactory
            // 
            btnResetFactory.BackColor = Color.Gainsboro;
            btnResetFactory.FlatStyle = FlatStyle.Flat;
            btnResetFactory.Font = new Font("Tahoma", 8F);
            btnResetFactory.Location = new Point(488, 9);
            btnResetFactory.Name = "btnResetFactory";
            btnResetFactory.Size = new Size(115, 25);
            btnResetFactory.TabIndex = 6;
            btnResetFactory.Text = "Reset Factory";
            btnResetFactory.UseVisualStyleBackColor = false;
            btnResetFactory.Click += btnResetFactory_Click;
            // 
            // btnAbout
            // 
            btnAbout.BackColor = Color.Gainsboro;
            btnAbout.FlatStyle = FlatStyle.Flat;
            btnAbout.Font = new Font("Tahoma", 8F);
            btnAbout.Location = new Point(13, 9);
            btnAbout.Name = "btnAbout";
            btnAbout.Size = new Size(90, 25);
            btnAbout.TabIndex = 5;
            btnAbout.Text = "About";
            btnAbout.UseVisualStyleBackColor = false;
            btnAbout.Click += btnAbout_Click;
            // 
            // lblDatabaseDetails
            // 
            lblDatabaseDetails.AutoSize = true;
            lblDatabaseDetails.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblDatabaseDetails.ForeColor = Color.DimGray;
            lblDatabaseDetails.Location = new Point(9, 8);
            lblDatabaseDetails.Name = "lblDatabaseDetails";
            lblDatabaseDetails.Size = new Size(113, 13);
            lblDatabaseDetails.TabIndex = 8;
            lblDatabaseDetails.Text = "lblDatabaseDetails";
            // 
            // pnlBody
            // 
            pnlBody.BackColor = Color.White;
            pnlBody.Controls.Add(grpRuntimeSettings);
            pnlBody.Controls.Add(gpPassword);
            pnlBody.Controls.Add(gpDatabase);
            pnlBody.Controls.Add(gbTheme);
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.Location = new Point(0, 55);
            pnlBody.Name = "pnlBody";
            pnlBody.Size = new Size(704, 290);
            pnlBody.TabIndex = 8;
            // 
            // grpRuntimeSettings
            // 
            grpRuntimeSettings.Controls.Add(grbBaseLine);
            grpRuntimeSettings.Controls.Add(btnSave);
            grpRuntimeSettings.Controls.Add(ChAddHoursAfterEsd);
            grpRuntimeSettings.Controls.Add(txtEsdExtraHours);
            grpRuntimeSettings.Font = new Font("Tahoma", 8F);
            grpRuntimeSettings.Location = new Point(13, 201);
            grpRuntimeSettings.Name = "grpRuntimeSettings";
            grpRuntimeSettings.Size = new Size(676, 82);
            grpRuntimeSettings.TabIndex = 10;
            grpRuntimeSettings.TabStop = false;
            grpRuntimeSettings.Text = "Runtime";
            // 
            // grbBaseLine
            // 
            grbBaseLine.BackColor = Color.Transparent;
            grbBaseLine.Controls.Add(label2);
            grbBaseLine.Controls.Add(label1);
            grbBaseLine.Controls.Add(txtDataStartDateInfo);
            grbBaseLine.Controls.Add(btnUpdateDataStartDate);
            grbBaseLine.Controls.Add(cmbDataStartMonth);
            grbBaseLine.Controls.Add(cmbDataStartYear);
            grbBaseLine.FlatStyle = FlatStyle.Flat;
            grbBaseLine.Location = new Point(368, 7);
            grbBaseLine.Name = "grbBaseLine";
            grbBaseLine.Size = new Size(302, 69);
            grbBaseLine.TabIndex = 38;
            grbBaseLine.TabStop = false;
            grbBaseLine.Text = "Data Baseline";
            grbBaseLine.Visible = false;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.ForeColor = Color.Gray;
            label2.Location = new Point(16, 44);
            label2.Name = "label2";
            label2.Size = new Size(37, 13);
            label2.TabIndex = 30;
            label2.Text = "Month";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = Color.Gray;
            label1.Location = new Point(16, 23);
            label1.Name = "label1";
            label1.Size = new Size(29, 13);
            label1.TabIndex = 29;
            label1.Text = "Year";
            // 
            // txtDataStartDateInfo
            // 
            txtDataStartDateInfo.BorderStyle = BorderStyle.FixedSingle;
            txtDataStartDateInfo.Enabled = false;
            txtDataStartDateInfo.Font = new Font("Segoe UI", 8F);
            txtDataStartDateInfo.Location = new Point(173, 40);
            txtDataStartDateInfo.Name = "txtDataStartDateInfo";
            txtDataStartDateInfo.Size = new Size(65, 22);
            txtDataStartDateInfo.TabIndex = 28;
            txtDataStartDateInfo.Text = "1405/02/12";
            // 
            // btnUpdateDataStartDate
            // 
            btnUpdateDataStartDate.FlatAppearance.BorderColor = Color.Black;
            btnUpdateDataStartDate.FlatStyle = FlatStyle.Flat;
            btnUpdateDataStartDate.Location = new Point(173, 15);
            btnUpdateDataStartDate.Name = "btnUpdateDataStartDate";
            btnUpdateDataStartDate.Size = new Size(65, 23);
            btnUpdateDataStartDate.TabIndex = 27;
            btnUpdateDataStartDate.Text = "Update";
            btnUpdateDataStartDate.UseVisualStyleBackColor = true;
            btnUpdateDataStartDate.Click += btnUpdateDataStartDate_Click;
            // 
            // cmbDataStartMonth
            // 
            cmbDataStartMonth.BackColor = Color.White;
            cmbDataStartMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDataStartMonth.Font = new Font("Tahoma", 9F);
            cmbDataStartMonth.FormattingEnabled = true;
            cmbDataStartMonth.ItemHeight = 14;
            cmbDataStartMonth.Location = new Point(63, 40);
            cmbDataStartMonth.Name = "cmbDataStartMonth";
            cmbDataStartMonth.Size = new Size(100, 22);
            cmbDataStartMonth.TabIndex = 25;
            // 
            // cmbDataStartYear
            // 
            cmbDataStartYear.BackColor = Color.White;
            cmbDataStartYear.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDataStartYear.Font = new Font("Tahoma", 9F);
            cmbDataStartYear.FormattingEnabled = true;
            cmbDataStartYear.ItemHeight = 14;
            cmbDataStartYear.Location = new Point(63, 16);
            cmbDataStartYear.Name = "cmbDataStartYear";
            cmbDataStartYear.Size = new Size(100, 22);
            cmbDataStartYear.TabIndex = 24;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.Gainsboro;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Font = new Font("Tahoma", 8F);
            btnSave.Location = new Point(214, 32);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(70, 25);
            btnSave.TabIndex = 8;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += btnSave_Click;
            // 
            // ChAddHoursAfterEsd
            // 
            ChAddHoursAfterEsd.AutoSize = true;
            ChAddHoursAfterEsd.Location = new Point(17, 36);
            ChAddHoursAfterEsd.Name = "ChAddHoursAfterEsd";
            ChAddHoursAfterEsd.RightToLeft = RightToLeft.No;
            ChAddHoursAfterEsd.Size = new Size(128, 17);
            ChAddHoursAfterEsd.TabIndex = 0;
            ChAddHoursAfterEsd.Text = "Add Runtime per ESD";
            ChAddHoursAfterEsd.UseVisualStyleBackColor = true;
            ChAddHoursAfterEsd.CheckedChanged += chkAddHoursAfterNsd_CheckedChanged;
            // 
            // txtEsdExtraHours
            // 
            txtEsdExtraHours.BorderStyle = BorderStyle.FixedSingle;
            txtEsdExtraHours.Enabled = false;
            txtEsdExtraHours.Font = new Font("Tahoma", 10F);
            txtEsdExtraHours.Location = new Point(155, 32);
            txtEsdExtraHours.Name = "txtEsdExtraHours";
            txtEsdExtraHours.Size = new Size(55, 24);
            txtEsdExtraHours.TabIndex = 1;
            txtEsdExtraHours.TextAlign = HorizontalAlignment.Center;
            // 
            // gpPassword
            // 
            gpPassword.Controls.Add(lblPasswordDetails);
            gpPassword.Controls.Add(btnResetPassword);
            gpPassword.Controls.Add(btnChangeLoginPassword);
            gpPassword.Font = new Font("Tahoma", 8F);
            gpPassword.Location = new Point(381, 79);
            gpPassword.Name = "gpPassword";
            gpPassword.RightToLeft = RightToLeft.No;
            gpPassword.Size = new Size(308, 114);
            gpPassword.TabIndex = 9;
            gpPassword.TabStop = false;
            gpPassword.Text = "Password";
            // 
            // lblPasswordDetails
            // 
            lblPasswordDetails.AutoSize = true;
            lblPasswordDetails.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblPasswordDetails.ForeColor = Color.DimGray;
            lblPasswordDetails.Location = new Point(16, 24);
            lblPasswordDetails.Name = "lblPasswordDetails";
            lblPasswordDetails.Size = new Size(135, 13);
            lblPasswordDetails.TabIndex = 9;
            lblPasswordDetails.Text = "Last Password Update:";
            // 
            // btnResetPassword
            // 
            btnResetPassword.BackColor = Color.Gainsboro;
            btnResetPassword.FlatStyle = FlatStyle.Flat;
            btnResetPassword.Location = new Point(173, 52);
            btnResetPassword.Name = "btnResetPassword";
            btnResetPassword.Size = new Size(120, 25);
            btnResetPassword.TabIndex = 4;
            btnResetPassword.Text = "Recovery";
            btnResetPassword.UseVisualStyleBackColor = false;
            btnResetPassword.Click += btnResetPassword_Click;
            // 
            // btnChangeLoginPassword
            // 
            btnChangeLoginPassword.BackColor = Color.Gainsboro;
            btnChangeLoginPassword.FlatStyle = FlatStyle.Flat;
            btnChangeLoginPassword.Location = new Point(173, 80);
            btnChangeLoginPassword.Name = "btnChangeLoginPassword";
            btnChangeLoginPassword.Size = new Size(120, 25);
            btnChangeLoginPassword.TabIndex = 3;
            btnChangeLoginPassword.Text = "Change";
            btnChangeLoginPassword.UseVisualStyleBackColor = false;
            btnChangeLoginPassword.Click += btnChangeLoginPassword_Click;
            // 
            // gpDatabase
            // 
            gpDatabase.BackColor = Color.Transparent;
            gpDatabase.Controls.Add(panel1);
            gpDatabase.Controls.Add(btnRepairDatabase);
            gpDatabase.Controls.Add(btnImportDatabase);
            gpDatabase.Controls.Add(btnExportDatabase);
            gpDatabase.Font = new Font("Tahoma", 8F);
            gpDatabase.Location = new Point(13, 79);
            gpDatabase.Name = "gpDatabase";
            gpDatabase.Size = new Size(362, 114);
            gpDatabase.TabIndex = 8;
            gpDatabase.TabStop = false;
            gpDatabase.Text = "Database";
            // 
            // panel1
            // 
            panel1.BackColor = Color.WhiteSmoke;
            panel1.Controls.Add(lblDatabaseDetails);
            panel1.Location = new Point(176, 24);
            panel1.Name = "panel1";
            panel1.Size = new Size(177, 81);
            panel1.TabIndex = 9;
            // 
            // btnRepairDatabase
            // 
            btnRepairDatabase.BackColor = Color.Gainsboro;
            btnRepairDatabase.FlatStyle = FlatStyle.Flat;
            btnRepairDatabase.Location = new Point(23, 80);
            btnRepairDatabase.Name = "btnRepairDatabase";
            btnRepairDatabase.Size = new Size(120, 25);
            btnRepairDatabase.TabIndex = 2;
            btnRepairDatabase.Text = "Maintenance";
            btnRepairDatabase.UseVisualStyleBackColor = false;
            btnRepairDatabase.Click += btnRepairDatabase_Click;
            // 
            // btnImportDatabase
            // 
            btnImportDatabase.BackColor = Color.Gainsboro;
            btnImportDatabase.FlatStyle = FlatStyle.Flat;
            btnImportDatabase.Location = new Point(23, 52);
            btnImportDatabase.Name = "btnImportDatabase";
            btnImportDatabase.Size = new Size(120, 25);
            btnImportDatabase.TabIndex = 1;
            btnImportDatabase.Text = "Import";
            btnImportDatabase.UseVisualStyleBackColor = false;
            btnImportDatabase.Click += btnImportDatabase_Click;
            // 
            // btnExportDatabase
            // 
            btnExportDatabase.BackColor = Color.Gainsboro;
            btnExportDatabase.FlatStyle = FlatStyle.Flat;
            btnExportDatabase.Location = new Point(23, 24);
            btnExportDatabase.Name = "btnExportDatabase";
            btnExportDatabase.Size = new Size(120, 25);
            btnExportDatabase.TabIndex = 0;
            btnExportDatabase.Text = "Backup";
            btnExportDatabase.UseVisualStyleBackColor = false;
            btnExportDatabase.Click += btnExportDatabase_Click;
            // 
            // gbTheme
            // 
            gbTheme.Controls.Add(rdoThemeClassicSoftAccent);
            gbTheme.Controls.Add(rdoThemeClassicNeutral);
            gbTheme.Controls.Add(rdoIndustrialRed);
            gbTheme.Controls.Add(rdoIndigoViolet);
            gbTheme.Controls.Add(rdoThemeTerracottaStone);
            gbTheme.Controls.Add(rdoThemeOlive);
            gbTheme.Controls.Add(rdoThemeGraphite);
            gbTheme.Controls.Add(rdoThemeBlue);
            gbTheme.Font = new Font("Tahoma", 8F);
            gbTheme.Location = new Point(13, 11);
            gbTheme.Name = "gbTheme";
            gbTheme.Size = new Size(676, 60);
            gbTheme.TabIndex = 7;
            gbTheme.TabStop = false;
            gbTheme.Text = "Theme";
            // 
            // rdoIndustrialRed
            // 
            rdoIndustrialRed.AutoSize = true;
            rdoIndustrialRed.Location = new Point(232, 26);
            rdoIndustrialRed.Name = "rdoIndustrialRed";
            rdoIndustrialRed.Size = new Size(49, 17);
            rdoIndustrialRed.TabIndex = 6;
            rdoIndustrialRed.TabStop = true;
            rdoIndustrialRed.Text = "قرمز ";
            rdoIndustrialRed.TextAlign = ContentAlignment.MiddleRight;
            rdoIndustrialRed.UseVisualStyleBackColor = true;
            rdoIndustrialRed.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoIndigoViolet
            // 
            rdoIndigoViolet.AutoSize = true;
            rdoIndigoViolet.Location = new Point(162, 26);
            rdoIndigoViolet.Name = "rdoIndigoViolet";
            rdoIndigoViolet.Size = new Size(55, 17);
            rdoIndigoViolet.TabIndex = 5;
            rdoIndigoViolet.TabStop = true;
            rdoIndigoViolet.Text = "بنفش ";
            rdoIndigoViolet.TextAlign = ContentAlignment.MiddleRight;
            rdoIndigoViolet.UseVisualStyleBackColor = true;
            rdoIndigoViolet.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoThemeTerracottaStone
            // 
            rdoThemeTerracottaStone.AutoSize = true;
            rdoThemeTerracottaStone.Location = new Point(12, 26);
            rdoThemeTerracottaStone.Name = "rdoThemeTerracottaStone";
            rdoThemeTerracottaStone.Size = new Size(65, 17);
            rdoThemeTerracottaStone.TabIndex = 4;
            rdoThemeTerracottaStone.TabStop = true;
            rdoThemeTerracottaStone.Text = "قهوه ای ";
            rdoThemeTerracottaStone.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeTerracottaStone.UseVisualStyleBackColor = true;
            rdoThemeTerracottaStone.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoThemeOlive
            // 
            rdoThemeOlive.AutoSize = true;
            rdoThemeOlive.Location = new Point(92, 26);
            rdoThemeOlive.Name = "rdoThemeOlive";
            rdoThemeOlive.Size = new Size(55, 17);
            rdoThemeOlive.TabIndex = 3;
            rdoThemeOlive.TabStop = true;
            rdoThemeOlive.Text = "زیتونی";
            rdoThemeOlive.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeOlive.UseVisualStyleBackColor = true;
            rdoThemeOlive.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoThemeGraphite
            // 
            rdoThemeGraphite.AutoSize = true;
            rdoThemeGraphite.Location = new Point(296, 26);
            rdoThemeGraphite.Name = "rdoThemeGraphite";
            rdoThemeGraphite.Size = new Size(57, 17);
            rdoThemeGraphite.TabIndex = 2;
            rdoThemeGraphite.TabStop = true;
            rdoThemeGraphite.Text = "گرافیت";
            rdoThemeGraphite.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeGraphite.UseVisualStyleBackColor = true;
            rdoThemeGraphite.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // rdoThemeBlue
            // 
            rdoThemeBlue.AutoSize = true;
            rdoThemeBlue.Location = new Point(368, 26);
            rdoThemeBlue.Name = "rdoThemeBlue";
            rdoThemeBlue.Size = new Size(45, 17);
            rdoThemeBlue.TabIndex = 0;
            rdoThemeBlue.TabStop = true;
            rdoThemeBlue.Text = "آبی ";
            rdoThemeBlue.TextAlign = ContentAlignment.MiddleRight;
            rdoThemeBlue.UseVisualStyleBackColor = true;
            rdoThemeBlue.CheckedChanged += ThemeRadio_CheckedChanged;
            // 
            // FrmSettings
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(704, 389);
            Controls.Add(pnlBody);
            Controls.Add(pnlFooter);
            Controls.Add(pnlHeader);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            KeyPreview = true;
            MaximizeBox = false;
            Name = "FrmSettings";
            RightToLeft = RightToLeft.No;
            StartPosition = FormStartPosition.CenterScreen;
            Load += FrmSettings_Load;
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            pnlFooter.ResumeLayout(false);
            pnlBody.ResumeLayout(false);
            grpRuntimeSettings.ResumeLayout(false);
            grpRuntimeSettings.PerformLayout();
            grbBaseLine.ResumeLayout(false);
            grbBaseLine.PerformLayout();
            gpPassword.ResumeLayout(false);
            gpPassword.PerformLayout();
            gpDatabase.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            gbTheme.ResumeLayout(false);
            gbTheme.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private Panel pnlHeader;
        private Label lblSubTitle;
        private Panel pnlFooter;
        private Panel pnlBody;
        private GroupBox gpPassword;
        private Button btnResetPassword;
        private Button btnChangeLoginPassword;
        private GroupBox gpDatabase;
        private Button btnRepairDatabase;
        private Button btnImportDatabase;
        private Button btnExportDatabase;
        private GroupBox gbTheme;
        private RadioButton rdoThemeOlive;
        private RadioButton rdoThemeGraphite;
        private RadioButton rdoThemeBlue;
        private Button btnAbout;
        private Button btnClose;
        private Button btnResetFactory;
        private Label lblTitle;
        private RadioButton rdoIndigoViolet;
        private RadioButton rdoThemeTerracottaStone;
        private RadioButton rdoIndustrialRed;
        private GroupBox grpRuntimeSettings;
        private CheckBox ChAddHoursAfterEsd;
        private TextBox txtEsdExtraHours;
        private Button btnSave;
        private RadioButton rdoThemeClassicSoftAccent;
        private RadioButton rdoThemeClassicNeutral;
        private GroupBox grbBaseLine;
        private ComboBox cmbDataStartMonth;
        private ComboBox cmbDataStartYear;
        private Button btnUpdateDataStartDate;
        private TextBox txtDataStartDateInfo;
        private Label label2;
        private Label label1;
        private Label lblDatabaseDetails;
        private Panel panel1;
        private Label lblPasswordDetails;
    }
}