namespace Rah_Negar.UI.Startup
{
    partial class FrmStartup
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
            rbRasht = new RadioButton();
            grpStation = new GroupBox();
            lblCustom = new Label();
            txtCustom = new TextBox();
            rbOther = new RadioButton();
            rbRamsar = new RadioButton();
            grpSecurity = new GroupBox();
            btnTogglePassword = new Button();
            txtConfirm = new TextBox();
            lblConfirm = new Label();
            txtPass = new TextBox();
            lblPass = new Label();
            grpRuntime = new GroupBox();
            lblDate = new Label();
            cmbStU4 = new ComboBox();
            cmbStU3 = new ComboBox();
            cmbStU1 = new ComboBox();
            cmbStU2 = new ComboBox();
            label2 = new Label();
            lblOH = new Label();
            lblRun = new Label();
            lblU1 = new Label();
            lblU4 = new Label();
            lblU3 = new Label();
            lblU2 = new Label();
            lblInitialBaseDateInfo = new Label();
            txtEsdExtraHours = new TextBox();
            chAddHoursAfterEsd = new CheckBox();
            txtU4OH = new TextBox();
            txtU4Run = new TextBox();
            txtU3OH = new TextBox();
            txtU2OH = new TextBox();
            txtU1OH = new TextBox();
            txtU3Run = new TextBox();
            txtU2Run = new TextBox();
            txtU1Run = new TextBox();
            btnCancel = new Button();
            btnSave = new Button();
            cmbDataStartYear = new ComboBox();
            cmbDataStartMonth = new ComboBox();
            groupBox1 = new GroupBox();
            grpStation.SuspendLayout();
            grpSecurity.SuspendLayout();
            grpRuntime.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // rbRasht
            // 
            rbRasht.AutoSize = true;
            rbRasht.Font = new Font("Segoe UI", 8.5F);
            rbRasht.Location = new Point(21, 29);
            rbRasht.Margin = new Padding(4, 3, 4, 3);
            rbRasht.Name = "rbRasht";
            rbRasht.Size = new Size(54, 19);
            rbRasht.TabIndex = 2;
            rbRasht.TabStop = true;
            rbRasht.Text = "Rasht";
            rbRasht.UseVisualStyleBackColor = true;
            // 
            // grpStation
            // 
            grpStation.Controls.Add(lblCustom);
            grpStation.Controls.Add(txtCustom);
            grpStation.Controls.Add(rbOther);
            grpStation.Controls.Add(rbRamsar);
            grpStation.Controls.Add(rbRasht);
            grpStation.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grpStation.ForeColor = Color.DimGray;
            grpStation.Location = new Point(13, 17);
            grpStation.Margin = new Padding(4, 3, 4, 3);
            grpStation.Name = "grpStation";
            grpStation.Padding = new Padding(4, 3, 4, 3);
            grpStation.Size = new Size(638, 65);
            grpStation.TabIndex = 1;
            grpStation.TabStop = false;
            grpStation.Text = "Station";
            // 
            // lblCustom
            // 
            lblCustom.AutoSize = true;
            lblCustom.Font = new Font("Segoe UI", 8.5F);
            lblCustom.Location = new Point(265, 31);
            lblCustom.Margin = new Padding(4, 0, 4, 0);
            lblCustom.Name = "lblCustom";
            lblCustom.Size = new Size(87, 15);
            lblCustom.TabIndex = 4;
            lblCustom.Text = "Custom Name:";
            // 
            // txtCustom
            // 
            txtCustom.BorderStyle = BorderStyle.FixedSingle;
            txtCustom.Font = new Font("Segoe UI", 8.5F);
            txtCustom.Location = new Point(375, 27);
            txtCustom.Margin = new Padding(4, 3, 4, 3);
            txtCustom.MaxLength = 10;
            txtCustom.Name = "txtCustom";
            txtCustom.Size = new Size(90, 23);
            txtCustom.TabIndex = 5;
            txtCustom.TextAlign = HorizontalAlignment.Center;
            txtCustom.Visible = false;
            // 
            // rbOther
            // 
            rbOther.AutoSize = true;
            rbOther.Font = new Font("Segoe UI", 8.5F);
            rbOther.Location = new Point(187, 29);
            rbOther.Margin = new Padding(4, 3, 4, 3);
            rbOther.Name = "rbOther";
            rbOther.Size = new Size(55, 19);
            rbOther.TabIndex = 4;
            rbOther.TabStop = true;
            rbOther.Text = "Other";
            rbOther.UseVisualStyleBackColor = true;
            // 
            // rbRamsar
            // 
            rbRamsar.AutoSize = true;
            rbRamsar.Font = new Font("Segoe UI", 8.5F);
            rbRamsar.Location = new Point(99, 29);
            rbRamsar.Margin = new Padding(4, 3, 4, 3);
            rbRamsar.Name = "rbRamsar";
            rbRamsar.Size = new Size(64, 19);
            rbRamsar.TabIndex = 3;
            rbRamsar.TabStop = true;
            rbRamsar.Text = "Ramsar";
            rbRamsar.UseVisualStyleBackColor = true;
            // 
            // grpSecurity
            // 
            grpSecurity.Controls.Add(btnTogglePassword);
            grpSecurity.Controls.Add(txtConfirm);
            grpSecurity.Controls.Add(lblConfirm);
            grpSecurity.Controls.Add(txtPass);
            grpSecurity.Controls.Add(lblPass);
            grpSecurity.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grpSecurity.ForeColor = Color.DimGray;
            grpSecurity.Location = new Point(262, 89);
            grpSecurity.Margin = new Padding(4, 3, 4, 3);
            grpSecurity.Name = "grpSecurity";
            grpSecurity.Padding = new Padding(4, 3, 4, 3);
            grpSecurity.Size = new Size(389, 65);
            grpSecurity.TabIndex = 9;
            grpSecurity.TabStop = false;
            grpSecurity.Text = "Security";
            // 
            // btnTogglePassword
            // 
            btnTogglePassword.FlatAppearance.BorderSize = 0;
            btnTogglePassword.FlatStyle = FlatStyle.Flat;
            btnTogglePassword.Location = new Point(355, 26);
            btnTogglePassword.Margin = new Padding(4, 3, 4, 3);
            btnTogglePassword.Name = "btnTogglePassword";
            btnTogglePassword.Size = new Size(26, 24);
            btnTogglePassword.TabIndex = 12;
            btnTogglePassword.UseVisualStyleBackColor = true;
            // 
            // txtConfirm
            // 
            txtConfirm.BorderStyle = BorderStyle.FixedSingle;
            txtConfirm.Font = new Font("Segoe UI", 8F);
            txtConfirm.Location = new Point(265, 27);
            txtConfirm.Margin = new Padding(4, 3, 4, 3);
            txtConfirm.MaxLength = 10;
            txtConfirm.Name = "txtConfirm";
            txtConfirm.Size = new Size(90, 22);
            txtConfirm.TabIndex = 11;
            txtConfirm.TextAlign = HorizontalAlignment.Center;
            txtConfirm.UseSystemPasswordChar = true;
            txtConfirm.WordWrap = false;
            // 
            // lblConfirm
            // 
            lblConfirm.AutoSize = true;
            lblConfirm.Font = new Font("Segoe UI", 9F);
            lblConfirm.Location = new Point(208, 31);
            lblConfirm.Margin = new Padding(4, 0, 4, 0);
            lblConfirm.Name = "lblConfirm";
            lblConfirm.Size = new Size(54, 15);
            lblConfirm.TabIndex = 2;
            lblConfirm.Text = "Confirm:";
            // 
            // txtPass
            // 
            txtPass.BorderStyle = BorderStyle.FixedSingle;
            txtPass.Font = new Font("Segoe UI", 8F);
            txtPass.Location = new Point(79, 27);
            txtPass.Margin = new Padding(4, 3, 4, 3);
            txtPass.MaxLength = 10;
            txtPass.Name = "txtPass";
            txtPass.Size = new Size(90, 22);
            txtPass.TabIndex = 10;
            txtPass.TextAlign = HorizontalAlignment.Center;
            txtPass.UseSystemPasswordChar = true;
            txtPass.WordWrap = false;
            // 
            // lblPass
            // 
            lblPass.AutoSize = true;
            lblPass.Font = new Font("Segoe UI", 9F);
            lblPass.Location = new Point(17, 31);
            lblPass.Margin = new Padding(4, 0, 4, 0);
            lblPass.Name = "lblPass";
            lblPass.Size = new Size(60, 15);
            lblPass.TabIndex = 0;
            lblPass.Text = "Password:";
            // 
            // grpRuntime
            // 
            grpRuntime.Controls.Add(lblDate);
            grpRuntime.Controls.Add(cmbStU4);
            grpRuntime.Controls.Add(cmbStU3);
            grpRuntime.Controls.Add(cmbStU1);
            grpRuntime.Controls.Add(cmbStU2);
            grpRuntime.Controls.Add(label2);
            grpRuntime.Controls.Add(lblOH);
            grpRuntime.Controls.Add(lblRun);
            grpRuntime.Controls.Add(lblU1);
            grpRuntime.Controls.Add(lblU4);
            grpRuntime.Controls.Add(lblU3);
            grpRuntime.Controls.Add(lblU2);
            grpRuntime.Controls.Add(lblInitialBaseDateInfo);
            grpRuntime.Controls.Add(txtEsdExtraHours);
            grpRuntime.Controls.Add(chAddHoursAfterEsd);
            grpRuntime.Controls.Add(txtU4OH);
            grpRuntime.Controls.Add(txtU4Run);
            grpRuntime.Controls.Add(txtU3OH);
            grpRuntime.Controls.Add(txtU2OH);
            grpRuntime.Controls.Add(txtU1OH);
            grpRuntime.Controls.Add(txtU3Run);
            grpRuntime.Controls.Add(txtU2Run);
            grpRuntime.Controls.Add(txtU1Run);
            grpRuntime.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grpRuntime.ForeColor = Color.DimGray;
            grpRuntime.Location = new Point(13, 161);
            grpRuntime.Margin = new Padding(4, 3, 4, 3);
            grpRuntime.Name = "grpRuntime";
            grpRuntime.Padding = new Padding(4, 3, 4, 3);
            grpRuntime.Size = new Size(638, 193);
            grpRuntime.TabIndex = 13;
            grpRuntime.TabStop = false;
            grpRuntime.Text = "Runtime";
            // 
            // lblDate
            // 
            lblDate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblDate.AutoSize = true;
            lblDate.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblDate.ForeColor = Color.Peru;
            lblDate.Location = new Point(163, 27);
            lblDate.Margin = new Padding(4, 0, 4, 0);
            lblDate.Name = "lblDate";
            lblDate.Size = new Size(75, 13);
            lblDate.TabIndex = 29;
            lblDate.Text = "1404/12/12";
            lblDate.TextAlign = ContentAlignment.MiddleRight;
            lblDate.Visible = false;
            // 
            // cmbStU4
            // 
            cmbStU4.BackColor = Color.White;
            cmbStU4.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStU4.Font = new Font("Segoe UI", 9F);
            cmbStU4.FormattingEnabled = true;
            cmbStU4.ItemHeight = 15;
            cmbStU4.Items.AddRange(new object[] { "ON", "OFF" });
            cmbStU4.Location = new Point(250, 154);
            cmbStU4.Name = "cmbStU4";
            cmbStU4.Size = new Size(83, 23);
            cmbStU4.TabIndex = 24;
            // 
            // cmbStU3
            // 
            cmbStU3.BackColor = Color.White;
            cmbStU3.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStU3.Font = new Font("Segoe UI", 9F);
            cmbStU3.FormattingEnabled = true;
            cmbStU3.ItemHeight = 15;
            cmbStU3.Items.AddRange(new object[] { "ON", "OFF" });
            cmbStU3.Location = new Point(250, 128);
            cmbStU3.Name = "cmbStU3";
            cmbStU3.Size = new Size(83, 23);
            cmbStU3.TabIndex = 21;
            // 
            // cmbStU1
            // 
            cmbStU1.BackColor = Color.White;
            cmbStU1.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStU1.Font = new Font("Segoe UI", 9F);
            cmbStU1.FormattingEnabled = true;
            cmbStU1.ItemHeight = 15;
            cmbStU1.Items.AddRange(new object[] { "ON", "OFF" });
            cmbStU1.Location = new Point(250, 75);
            cmbStU1.Name = "cmbStU1";
            cmbStU1.Size = new Size(83, 23);
            cmbStU1.TabIndex = 15;
            // 
            // cmbStU2
            // 
            cmbStU2.BackColor = Color.White;
            cmbStU2.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStU2.Font = new Font("Segoe UI", 9F);
            cmbStU2.FormattingEnabled = true;
            cmbStU2.ItemHeight = 15;
            cmbStU2.Items.AddRange(new object[] { "ON ", "OFF" });
            cmbStU2.Location = new Point(250, 101);
            cmbStU2.Name = "cmbStU2";
            cmbStU2.Size = new Size(83, 23);
            cmbStU2.TabIndex = 18;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 9F);
            label2.Location = new Point(272, 58);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(39, 15);
            label2.TabIndex = 19;
            label2.Text = "Status";
            // 
            // lblOH
            // 
            lblOH.AutoSize = true;
            lblOH.Font = new Font("Segoe UI", 9F);
            lblOH.Location = new Point(175, 58);
            lblOH.Margin = new Padding(4, 0, 4, 0);
            lblOH.Name = "lblOH";
            lblOH.Size = new Size(54, 15);
            lblOH.TabIndex = 2;
            lblOH.Text = "After OH";
            // 
            // lblRun
            // 
            lblRun.AutoSize = true;
            lblRun.Font = new Font("Segoe UI", 9F);
            lblRun.Location = new Point(93, 58);
            lblRun.Margin = new Padding(4, 0, 4, 0);
            lblRun.Name = "lblRun";
            lblRun.Size = new Size(33, 15);
            lblRun.TabIndex = 1;
            lblRun.Text = "Total";
            // 
            // lblU1
            // 
            lblU1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblU1.AutoSize = true;
            lblU1.Font = new Font("Segoe UI", 9F);
            lblU1.Location = new Point(19, 79);
            lblU1.Margin = new Padding(4, 0, 4, 0);
            lblU1.Name = "lblU1";
            lblU1.Size = new Size(38, 15);
            lblU1.TabIndex = 3;
            lblU1.Text = "Unit 1";
            lblU1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblU4
            // 
            lblU4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblU4.AutoSize = true;
            lblU4.Font = new Font("Segoe UI", 9F);
            lblU4.Location = new Point(19, 158);
            lblU4.Margin = new Padding(4, 0, 4, 0);
            lblU4.Name = "lblU4";
            lblU4.Size = new Size(38, 15);
            lblU4.TabIndex = 25;
            lblU4.Text = "Unit 4";
            lblU4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblU3
            // 
            lblU3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblU3.AutoSize = true;
            lblU3.Font = new Font("Segoe UI", 9F);
            lblU3.Location = new Point(19, 132);
            lblU3.Margin = new Padding(4, 0, 4, 0);
            lblU3.Name = "lblU3";
            lblU3.Size = new Size(38, 15);
            lblU3.TabIndex = 23;
            lblU3.Text = "Unit 3";
            lblU3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblU2
            // 
            lblU2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblU2.AutoSize = true;
            lblU2.Font = new Font("Segoe UI", 9F);
            lblU2.Location = new Point(19, 105);
            lblU2.Margin = new Padding(4, 0, 4, 0);
            lblU2.Name = "lblU2";
            lblU2.Size = new Size(38, 15);
            lblU2.TabIndex = 24;
            lblU2.Text = "Unit 2";
            lblU2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInitialBaseDateInfo
            // 
            lblInitialBaseDateInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblInitialBaseDateInfo.AutoSize = true;
            lblInitialBaseDateInfo.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblInitialBaseDateInfo.ForeColor = Color.Peru;
            lblInitialBaseDateInfo.Location = new Point(246, 27);
            lblInitialBaseDateInfo.Margin = new Padding(4, 0, 4, 0);
            lblInitialBaseDateInfo.Name = "lblInitialBaseDateInfo";
            lblInitialBaseDateInfo.Size = new Size(373, 13);
            lblInitialBaseDateInfo.TabIndex = 20;
            lblInitialBaseDateInfo.Text = ":مقادیر ساعت کارکرد و وضعیت اولیه واحدها از این تاریخ اعمال می‌شود";
            lblInitialBaseDateInfo.TextAlign = ContentAlignment.MiddleRight;
            lblInitialBaseDateInfo.Visible = false;
            // 
            // txtEsdExtraHours
            // 
            txtEsdExtraHours.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtEsdExtraHours.BorderStyle = BorderStyle.FixedSingle;
            txtEsdExtraHours.Enabled = false;
            txtEsdExtraHours.Font = new Font("Tahoma", 9F);
            txtEsdExtraHours.Location = new Point(481, 97);
            txtEsdExtraHours.Margin = new Padding(4, 3, 4, 3);
            txtEsdExtraHours.Name = "txtEsdExtraHours";
            txtEsdExtraHours.Size = new Size(51, 22);
            txtEsdExtraHours.TabIndex = 26;
            txtEsdExtraHours.Text = "100";
            txtEsdExtraHours.TextAlign = HorizontalAlignment.Center;
            // 
            // chAddHoursAfterEsd
            // 
            chAddHoursAfterEsd.AutoSize = true;
            chAddHoursAfterEsd.Checked = true;
            chAddHoursAfterEsd.CheckState = CheckState.Checked;
            chAddHoursAfterEsd.Font = new Font("Segoe UI", 8.5F);
            chAddHoursAfterEsd.Location = new Point(463, 77);
            chAddHoursAfterEsd.Margin = new Padding(4, 3, 4, 3);
            chAddHoursAfterEsd.Name = "chAddHoursAfterEsd";
            chAddHoursAfterEsd.Size = new Size(146, 19);
            chAddHoursAfterEsd.TabIndex = 25;
            chAddHoursAfterEsd.Text = "Extra runtime after ESD";
            chAddHoursAfterEsd.UseVisualStyleBackColor = true;
            // 
            // txtU4OH
            // 
            txtU4OH.BorderStyle = BorderStyle.FixedSingle;
            txtU4OH.Font = new Font("Segoe UI", 9F);
            txtU4OH.Location = new Point(157, 154);
            txtU4OH.Margin = new Padding(4, 3, 4, 3);
            txtU4OH.MaxLength = 10;
            txtU4OH.Name = "txtU4OH";
            txtU4OH.Size = new Size(90, 23);
            txtU4OH.TabIndex = 23;
            txtU4OH.TextAlign = HorizontalAlignment.Center;
            txtU4OH.WordWrap = false;
            // 
            // txtU4Run
            // 
            txtU4Run.BorderStyle = BorderStyle.FixedSingle;
            txtU4Run.Font = new Font("Segoe UI", 9F);
            txtU4Run.Location = new Point(64, 154);
            txtU4Run.Margin = new Padding(4, 3, 4, 3);
            txtU4Run.MaxLength = 10;
            txtU4Run.Name = "txtU4Run";
            txtU4Run.Size = new Size(90, 23);
            txtU4Run.TabIndex = 22;
            txtU4Run.TextAlign = HorizontalAlignment.Center;
            txtU4Run.WordWrap = false;
            // 
            // txtU3OH
            // 
            txtU3OH.BorderStyle = BorderStyle.FixedSingle;
            txtU3OH.Font = new Font("Segoe UI", 9F);
            txtU3OH.Location = new Point(157, 128);
            txtU3OH.Margin = new Padding(4, 3, 4, 3);
            txtU3OH.MaxLength = 10;
            txtU3OH.Name = "txtU3OH";
            txtU3OH.Size = new Size(90, 23);
            txtU3OH.TabIndex = 20;
            txtU3OH.TextAlign = HorizontalAlignment.Center;
            txtU3OH.WordWrap = false;
            // 
            // txtU2OH
            // 
            txtU2OH.BorderStyle = BorderStyle.FixedSingle;
            txtU2OH.Font = new Font("Segoe UI", 9F);
            txtU2OH.Location = new Point(157, 101);
            txtU2OH.Margin = new Padding(4, 3, 4, 3);
            txtU2OH.MaxLength = 10;
            txtU2OH.Name = "txtU2OH";
            txtU2OH.Size = new Size(90, 23);
            txtU2OH.TabIndex = 17;
            txtU2OH.TextAlign = HorizontalAlignment.Center;
            txtU2OH.WordWrap = false;
            // 
            // txtU1OH
            // 
            txtU1OH.BorderStyle = BorderStyle.FixedSingle;
            txtU1OH.Font = new Font("Segoe UI", 9F);
            txtU1OH.Location = new Point(157, 75);
            txtU1OH.Margin = new Padding(4, 3, 4, 3);
            txtU1OH.MaxLength = 10;
            txtU1OH.Name = "txtU1OH";
            txtU1OH.Size = new Size(90, 23);
            txtU1OH.TabIndex = 14;
            txtU1OH.TextAlign = HorizontalAlignment.Center;
            txtU1OH.WordWrap = false;
            // 
            // txtU3Run
            // 
            txtU3Run.BorderStyle = BorderStyle.FixedSingle;
            txtU3Run.Font = new Font("Segoe UI", 9F);
            txtU3Run.Location = new Point(64, 128);
            txtU3Run.Margin = new Padding(4, 3, 4, 3);
            txtU3Run.MaxLength = 10;
            txtU3Run.Name = "txtU3Run";
            txtU3Run.Size = new Size(90, 23);
            txtU3Run.TabIndex = 19;
            txtU3Run.TextAlign = HorizontalAlignment.Center;
            txtU3Run.WordWrap = false;
            // 
            // txtU2Run
            // 
            txtU2Run.BorderStyle = BorderStyle.FixedSingle;
            txtU2Run.Font = new Font("Segoe UI", 9F);
            txtU2Run.Location = new Point(64, 101);
            txtU2Run.Margin = new Padding(4, 3, 4, 3);
            txtU2Run.MaxLength = 10;
            txtU2Run.Name = "txtU2Run";
            txtU2Run.Size = new Size(90, 23);
            txtU2Run.TabIndex = 16;
            txtU2Run.TextAlign = HorizontalAlignment.Center;
            txtU2Run.WordWrap = false;
            // 
            // txtU1Run
            // 
            txtU1Run.BorderStyle = BorderStyle.FixedSingle;
            txtU1Run.Font = new Font("Segoe UI", 9F);
            txtU1Run.Location = new Point(64, 75);
            txtU1Run.Margin = new Padding(4, 3, 4, 3);
            txtU1Run.MaxLength = 10;
            txtU1Run.Name = "txtU1Run";
            txtU1Run.Size = new Size(90, 23);
            txtU1Run.TabIndex = 13;
            txtU1Run.TextAlign = HorizontalAlignment.Center;
            txtU1Run.WordWrap = false;
            // 
            // btnCancel
            // 
            btnCancel.BackColor = Color.Silver;
            btnCancel.FlatAppearance.BorderColor = Color.Firebrick;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.MouseDownBackColor = Color.DimGray;
            btnCancel.FlatAppearance.MouseOverBackColor = Color.Gainsboro;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Font = new Font("Segoe UI", 8.5F);
            btnCancel.ForeColor = Color.Black;
            btnCancel.Location = new Point(365, 362);
            btnCancel.Margin = new Padding(4, 3, 4, 3);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(110, 28);
            btnCancel.TabIndex = 28;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = false;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.SteelBlue;
            btnSave.FlatAppearance.BorderColor = Color.SeaGreen;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseDownBackColor = Color.DarkSlateGray;
            btnSave.FlatAppearance.MouseOverBackColor = Color.SkyBlue;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Font = new Font("Segoe UI", 8.5F);
            btnSave.ForeColor = Color.White;
            btnSave.Location = new Point(481, 362);
            btnSave.Margin = new Padding(4, 3, 4, 3);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(170, 28);
            btnSave.TabIndex = 27;
            btnSave.Text = "Create Profile";
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += btnSave_Click;
            // 
            // cmbDataStartYear
            // 
            cmbDataStartYear.BackColor = Color.White;
            cmbDataStartYear.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDataStartYear.Font = new Font("Segoe UI", 9F);
            cmbDataStartYear.FormattingEnabled = true;
            cmbDataStartYear.ItemHeight = 15;
            cmbDataStartYear.Location = new Point(21, 27);
            cmbDataStartYear.Name = "cmbDataStartYear";
            cmbDataStartYear.Size = new Size(80, 23);
            cmbDataStartYear.TabIndex = 7;
            // 
            // cmbDataStartMonth
            // 
            cmbDataStartMonth.BackColor = Color.White;
            cmbDataStartMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDataStartMonth.Font = new Font("Segoe UI", 9F);
            cmbDataStartMonth.FormattingEnabled = true;
            cmbDataStartMonth.ItemHeight = 15;
            cmbDataStartMonth.Location = new Point(115, 27);
            cmbDataStartMonth.Name = "cmbDataStartMonth";
            cmbDataStartMonth.Size = new Size(108, 23);
            cmbDataStartMonth.TabIndex = 8;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(cmbDataStartMonth);
            groupBox1.Controls.Add(cmbDataStartYear);
            groupBox1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            groupBox1.ForeColor = Color.DimGray;
            groupBox1.Location = new Point(13, 89);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(242, 65);
            groupBox1.TabIndex = 6;
            groupBox1.TabStop = false;
            groupBox1.Text = "Data Baseline";
            // 
            // FrmStartup
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(664, 398);
            Controls.Add(grpStation);
            Controls.Add(groupBox1);
            Controls.Add(grpSecurity);
            Controls.Add(grpRuntime);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Location = new Point(20, 360);
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            Name = "FrmStartup";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Rah_Negar Startup Wizard";
            Load += FrmStartup_Load;
            grpStation.ResumeLayout(false);
            grpStation.PerformLayout();
            grpSecurity.ResumeLayout(false);
            grpSecurity.PerformLayout();
            grpRuntime.ResumeLayout(false);
            grpRuntime.PerformLayout();
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private RadioButton rbRasht;
        private GroupBox grpStation;
        private RadioButton rbOther;
        private RadioButton rbRamsar;
        private GroupBox grpSecurity;
        private TextBox txtPass;
        private Label lblPass;
        private TextBox txtConfirm;
        private Label lblConfirm;
        private GroupBox grpRuntime;
        private Label lblOH;
        private Label lblRun;
        private Label lblU1;
        private TextBox txtU3OH;
        private TextBox txtU2OH;
        private TextBox txtU1OH;
        private TextBox txtU3Run;
        private TextBox txtU2Run;
        private TextBox txtU1Run;
        private TextBox txtU4OH;
        private TextBox txtU4Run;
        private Button btnCancel;
        private TextBox txtCustom;
        private Label lblCustom;
        private Button btnSave;
        private Button btnTogglePassword;
        private TextBox txtEsdExtraHours;
        private CheckBox chAddHoursAfterEsd;
        private Label label2;
        private Label lblU4;
        private Label lblU2;
        private Label lblU3;
        private ComboBox cmbDataStartYear;
        private GroupBox groupBox1;
        private ComboBox cmbDataStartMonth;
        private ComboBox cmbStU1;
        private ComboBox cmbStU4;
        private ComboBox cmbStU3;
        private ComboBox cmbStU2;
        private Label lblInitialBaseDateInfo;
        private Label lblDate;
    }
}