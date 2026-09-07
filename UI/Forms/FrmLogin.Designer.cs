namespace Rah_Negar.UI.Forms
{
    partial class FrmLogin
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
            txtPass = new TextBox();
            btnLogin = new Button();
            lblUserValue = new Label();
            lnkForgot = new LinkLabel();
            lblTitr = new Label();
            pnlBack = new Panel();
            lblSubTitr = new Label();
            lnkChangePass = new LinkLabel();
            pnlTextBox = new Panel();
            lblDownLine = new Label();
            pnlBack.SuspendLayout();
            pnlTextBox.SuspendLayout();
            SuspendLayout();
            // 
            // txtPass
            // 
            txtPass.BackColor = Color.White;
            txtPass.BorderStyle = BorderStyle.FixedSingle;
            txtPass.Font = new Font("Segoe UI", 10F);
            txtPass.Location = new Point(0, 0);
            txtPass.Name = "txtPass";
            txtPass.Size = new Size(255, 32);
            txtPass.TabIndex = 0;
            txtPass.TextAlign = HorizontalAlignment.Left;
            txtPass.UseSystemPasswordChar = true;
            txtPass.WordWrap = false;
            // 
            // btnLogin
            // 
            btnLogin.BackColor = Color.FromArgb(40, 104, 148);
            btnLogin.FlatAppearance.BorderColor = Color.FromArgb(190, 220, 235);
            btnLogin.FlatAppearance.BorderSize = 1;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 126, 171);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnLogin.ForeColor = Color.White;
            btnLogin.Location = new Point(265, 0);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(95, 32);
            btnLogin.TabIndex = 1;
            btnLogin.TabStop = true;
            btnLogin.Text = "ورود";
            btnLogin.UseVisualStyleBackColor = false;
            btnLogin.Click += btnLogin_Click;
            // 
            // lblUserValue
            // 
            lblUserValue.AutoSize = true;
            lblUserValue.BackColor = Color.Transparent;
            lblUserValue.AutoSize = false;
            lblUserValue.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblUserValue.ForeColor = Color.WhiteSmoke;
            lblUserValue.Location = new Point(0, 156);
            lblUserValue.Name = "lblUserValue";
            lblUserValue.Size = new Size(500, 24);
            lblUserValue.TabIndex = 8;
            lblUserValue.Text = "تاسیسات تقویت فشار گاز رشــت";
            lblUserValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lnkForgot
            // 
            lnkForgot.ActiveLinkColor = Color.DodgerBlue;
            lnkForgot.AutoSize = true;
            lnkForgot.BackColor = Color.Transparent;
            lnkForgot.Font = new Font("Segoe UI", 9F);
            lnkForgot.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkForgot.LinkColor = Color.WhiteSmoke;
            lnkForgot.Location = new Point(0, 286);
            lnkForgot.Name = "lnkForgot";
            lnkForgot.Size = new Size(137, 14);
            lnkForgot.TabIndex = 2;
            lnkForgot.TabStop = true;
            lnkForgot.Text = "کلمه عبور را فراموش کردم";
            lnkForgot.VisitedLinkColor = Color.SteelBlue;
            lnkForgot.LinkClicked += lnkForgot_LinkClicked;
            // 
            // lblTitr
            // 
            lblTitr.AutoSize = false;
            lblTitr.BackColor = Color.DarkGray;
            lblTitr.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitr.ForeColor = Color.White;
            lblTitr.Location = new Point(0, 62);
            lblTitr.Name = "lblTitr";
            lblTitr.Size = new Size(500, 34);
            lblTitr.TabIndex = 0;
            lblTitr.Text = "ره‌نگار";
            lblTitr.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pnlBack
            // 
            pnlBack.BackColor = Color.FromArgb(32, 73, 105);
            pnlBack.Controls.Add(lblSubTitr);
            pnlBack.Controls.Add(lnkChangePass);
            pnlBack.Controls.Add(pnlTextBox);
            pnlBack.Controls.Add(lnkForgot);
            pnlBack.Controls.Add(lblUserValue);
            pnlBack.Controls.Add(lblTitr);
            pnlBack.Controls.Add(lblDownLine);
            pnlBack.Dock = DockStyle.Fill;
            pnlBack.Location = new Point(0, 0);
            pnlBack.Name = "pnlBack";
            pnlBack.Size = new Size(500, 360);
            pnlBack.TabIndex = 0;
            pnlBack.Paint += pnlBack_Paint;
            // 
            // lblSubTitr
            // 
            lblSubTitr.AutoSize = true;
            lblSubTitr.BackColor = Color.Transparent;
            lblSubTitr.AutoSize = false;
            lblSubTitr.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            lblSubTitr.ForeColor = Color.WhiteSmoke;
            lblSubTitr.Location = new Point(0, 104);
            lblSubTitr.Name = "lblSubTitr";
            lblSubTitr.Size = new Size(500, 24);
            lblSubTitr.TabIndex = 13;
            lblSubTitr.Text = "سامانه پایش و تحلیل داده های عملیاتی";
            lblSubTitr.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lnkChangePass
            // 
            lnkChangePass.ActiveLinkColor = Color.DodgerBlue;
            lnkChangePass.AutoSize = true;
            lnkChangePass.BackColor = Color.Transparent;
            lnkChangePass.Font = new Font("Segoe UI", 9F);
            lnkChangePass.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkChangePass.LinkColor = Color.WhiteSmoke;
            lnkChangePass.Location = new Point(0, 252);
            lnkChangePass.Name = "lnkChangePass";
            lnkChangePass.Size = new Size(84, 14);
            lnkChangePass.TabIndex = 1;
            lnkChangePass.TabStop = true;
            lnkChangePass.Text = "تغیـیر کلمه عبور";
            lnkChangePass.VisitedLinkColor = Color.SteelBlue;
            lnkChangePass.LinkClicked += lnkChangePass_LinkClicked;
            // 
            // pnlTextBox
            // 
            pnlTextBox.BackColor = Color.Transparent;
            pnlTextBox.Controls.Add(btnLogin);
            pnlTextBox.Controls.Add(txtPass);
            pnlTextBox.Location = new Point(65, 202);
            pnlTextBox.Name = "pnlTextBox";
            pnlTextBox.Size = new Size(360, 32);
            pnlTextBox.TabIndex = 0;
            // 
            // lblDownLine
            // 
            lblDownLine.AutoSize = true;
            lblDownLine.BackColor = Color.Transparent;
            lblDownLine.ForeColor = Color.Transparent;
            lblDownLine.Location = new Point(0, 0);
            lblDownLine.Name = "lblDownLine";
            lblDownLine.Size = new Size(0, 0);
            lblDownLine.TabIndex = 14;
            lblDownLine.Text = "";
            lblDownLine.Visible = false;
            // 
            // FrmLogin
            // 
            AcceptButton = btnLogin;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(32, 73, 105);
            ClientSize = new Size(500, 360);
            Controls.Add(pnlBack);
            Font = new Font("Tahoma", 9F);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmLogin";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ورود به ره‌نگار";
            Load += FrmLogin_Load;
            pnlBack.ResumeLayout(false);
            pnlBack.PerformLayout();
            pnlTextBox.ResumeLayout(false);
            pnlTextBox.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TextBox txtPass;
        private Button btnLogin;
        private Label lblUserValue;
        private LinkLabel lnkForgot;
        private Label lblTitr;
        private Panel pnlBack;
        private Panel pnlTextBox;
        private LinkLabel lnkChangePass;
        private Label lblSubTitr;
        private Label lblDownLine;
    }
}
