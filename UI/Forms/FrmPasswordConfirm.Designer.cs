namespace Rah_Negar.UI.Forms
{
    partial class FrmPasswordConfirm
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
            txtPassword = new TextBox();
            lblPassword = new Label();
            btnOk = new Button();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(344, 52);
            pnlHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Tahoma", 8F);
            lblTitle.Location = new Point(93, 23);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(211, 13);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "برای ادامه عملیات لطفا رمز خود را وارد نمایید";
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(39, 86);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(210, 23);
            txtPassword.TabIndex = 2;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Font = new Font("Tahoma", 8F);
            lblPassword.Location = new Point(249, 91);
            lblPassword.Name = "lblPassword";
            lblPassword.RightToLeft = RightToLeft.Yes;
            lblPassword.Size = new Size(55, 13);
            lblPassword.TabIndex = 8;
            lblPassword.Text = "رمز عبور  :";
            lblPassword.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnOk
            // 
            btnOk.BackColor = Color.SteelBlue;
            btnOk.FlatAppearance.BorderColor = Color.Silver;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.Font = new Font("Tahoma", 9F);
            btnOk.ForeColor = Color.White;
            btnOk.Location = new Point(39, 135);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(90, 24);
            btnOk.TabIndex = 9;
            btnOk.Text = "تایید";
            btnOk.UseVisualStyleBackColor = false;
            btnOk.Click += btnOk_Click;
            // 
            // FrmPasswordConfirm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.WhiteSmoke;
            ClientSize = new Size(344, 171);
            Controls.Add(btnOk);
            Controls.Add(lblPassword);
            Controls.Add(txtPassword);
            Controls.Add(pnlHeader);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmPasswordConfirm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "ورود رمز عبور";
            Load += FrmPasswordConfirm_Load;
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel pnlHeader;
        private Label lblTitle;
        private TextBox txtPassword;
        private Label lblPassword;
        private Button btnOk;
    }
}