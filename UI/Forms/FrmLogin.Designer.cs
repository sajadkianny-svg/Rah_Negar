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
            txtPass.BackColor = Color.WhiteSmoke;
            txtPass.BorderStyle = BorderStyle.None;
            txtPass.Font = new Font("Tahoma", 9F);
            txtPass.Location = new Point(3, 4);
            txtPass.Name = "txtPass";
            txtPass.Size = new Size(132, 15);
            txtPass.TabIndex = 4;
            txtPass.TextAlign = HorizontalAlignment.Center;
            txtPass.UseSystemPasswordChar = true;
            txtPass.WordWrap = false;
            // 
            // btnLogin
            // 
            btnLogin.BackColor = Color.SteelBlue;
            btnLogin.FlatAppearance.BorderColor = Color.SteelBlue;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.LightSkyBlue;
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.Font = new Font("Tahoma", 11F, FontStyle.Bold, GraphicsUnit.World);
            btnLogin.ForeColor = Color.WhiteSmoke;
            btnLogin.Location = new Point(135, 0);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(25, 22);
            btnLogin.TabIndex = 6;
            btnLogin.TabStop = false;
            btnLogin.Text = ">";
            btnLogin.UseVisualStyleBackColor = false;
            btnLogin.Click += btnLogin_Click;
            // 
            // lblUserValue
            // 
            lblUserValue.AutoSize = true;
            lblUserValue.BackColor = Color.Transparent;
            lblUserValue.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            lblUserValue.ForeColor = Color.WhiteSmoke;
            lblUserValue.Location = new Point(194, 161);
            lblUserValue.Name = "lblUserValue";
            lblUserValue.Size = new Size(197, 14);
            lblUserValue.TabIndex = 8;
            lblUserValue.Text = "تاسیسات تقویت فشار گاز رشــت";
            lblUserValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lnkForgot
            // 
            lnkForgot.ActiveLinkColor = Color.DodgerBlue;
            lnkForgot.AutoSize = true;
            lnkForgot.BackColor = Color.Transparent;
            lnkForgot.Font = new Font("Tahoma", 9F);
            lnkForgot.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkForgot.LinkColor = Color.WhiteSmoke;
            lnkForgot.Location = new Point(224, 270);
            lnkForgot.Name = "lnkForgot";
            lnkForgot.Size = new Size(137, 14);
            lnkForgot.TabIndex = 10;
            lnkForgot.TabStop = true;
            lnkForgot.Text = "کلمه عبور را فراموش کردم";
            lnkForgot.VisitedLinkColor = Color.SteelBlue;
            lnkForgot.LinkClicked += lnkForgot_LinkClicked;
            // 
            // lblTitr
            // 
            lblTitr.AutoSize = true;
            lblTitr.BackColor = Color.DarkGray;
            lblTitr.Font = new Font("Tahoma", 11F, FontStyle.Bold);
            lblTitr.ForeColor = Color.White;
            lblTitr.Location = new Point(252, 120);
            lblTitr.Name = "lblTitr";
            lblTitr.Size = new Size(81, 18);
            lblTitr.TabIndex = 0;
            lblTitr.Text = "ره نگـــــــار";
            // 
            // pnlBack
            // 
            pnlBack.BackColor = Color.SteelBlue;
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
            pnlBack.Size = new Size(584, 301);
            pnlBack.TabIndex = 0;
            pnlBack.Paint += pnlBack_Paint;
            // 
            // lblSubTitr
            // 
            lblSubTitr.AutoSize = true;
            lblSubTitr.BackColor = Color.Transparent;
            lblSubTitr.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            lblSubTitr.ForeColor = Color.WhiteSmoke;
            lblSubTitr.Location = new Point(173, 143);
            lblSubTitr.Name = "lblSubTitr";
            lblSubTitr.Size = new Size(238, 14);
            lblSubTitr.TabIndex = 13;
            lblSubTitr.Text = "سامانه پایش و تحلیل داده های عملیاتی";
            lblSubTitr.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lnkChangePass
            // 
            lnkChangePass.ActiveLinkColor = Color.DodgerBlue;
            lnkChangePass.AutoSize = true;
            lnkChangePass.BackColor = Color.Transparent;
            lnkChangePass.Font = new Font("Tahoma", 9F);
            lnkChangePass.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkChangePass.LinkColor = Color.WhiteSmoke;
            lnkChangePass.Location = new Point(250, 253);
            lnkChangePass.Name = "lnkChangePass";
            lnkChangePass.Size = new Size(84, 14);
            lnkChangePass.TabIndex = 12;
            lnkChangePass.TabStop = true;
            lnkChangePass.Text = "تغیـیر کلمه عبور";
            lnkChangePass.VisitedLinkColor = Color.SteelBlue;
            lnkChangePass.LinkClicked += lnkChangePass_LinkClicked;
            // 
            // pnlTextBox
            // 
            pnlTextBox.BackColor = Color.WhiteSmoke;
            pnlTextBox.Controls.Add(btnLogin);
            pnlTextBox.Controls.Add(txtPass);
            pnlTextBox.Location = new Point(212, 225);
            pnlTextBox.Name = "pnlTextBox";
            pnlTextBox.Size = new Size(160, 22);
            pnlTextBox.TabIndex = 11;
            // 
            // lblDownLine
            // 
            lblDownLine.AutoSize = true;
            lblDownLine.BackColor = Color.Transparent;
            lblDownLine.ForeColor = Color.DarkGray;
            lblDownLine.Location = new Point(163, 124);
            lblDownLine.Name = "lblDownLine";
            lblDownLine.Size = new Size(259, 14);
            lblDownLine.TabIndex = 14;
            lblDownLine.Text = "____________________________________";
            // 
            // FrmLogin
            // 
            AcceptButton = btnLogin;
            AutoScaleDimensions = new SizeF(7F, 14F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Gainsboro;
            ClientSize = new Size(584, 301);
            Controls.Add(pnlBack);
            Font = new Font("Tahoma", 9F);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmLogin";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Rah_Negar Login";
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