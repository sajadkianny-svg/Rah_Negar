namespace Rah_Negar.UI.Forms
{
    partial class FrmChangePassword
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmChangePassword));
            txtCurrent = new TextBox();
            btnSave = new Button();
            txtNew = new TextBox();
            txtConfirm = new TextBox();
            pnlHeader = new Panel();
            lblHeaderText = new Label();
            pictureBox1 = new PictureBox();
            pnlBody = new Panel();
            lblConfirmPassword = new Label();
            lblNewPassword = new Label();
            lblOldPassword = new Label();
            pnlHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            pnlBody.SuspendLayout();
            SuspendLayout();
            // 
            // txtCurrent
            // 
            txtCurrent.BorderStyle = BorderStyle.FixedSingle;
            txtCurrent.Location = new Point(49, 27);
            txtCurrent.Name = "txtCurrent";
            txtCurrent.Size = new Size(200, 22);
            txtCurrent.TabIndex = 1;
            txtCurrent.TextAlign = HorizontalAlignment.Center;
            txtCurrent.UseSystemPasswordChar = true;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.SteelBlue;
            btnSave.FlatAppearance.BorderColor = Color.Silver;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Font = new Font("Tahoma", 9F);
            btnSave.ForeColor = Color.White;
            btnSave.Location = new Point(49, 123);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(90, 24);
            btnSave.TabIndex = 2;
            btnSave.Text = "تایید";
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += btnSave_Click;
            // 
            // txtNew
            // 
            txtNew.BorderStyle = BorderStyle.FixedSingle;
            txtNew.Location = new Point(49, 54);
            txtNew.Name = "txtNew";
            txtNew.Size = new Size(200, 22);
            txtNew.TabIndex = 4;
            txtNew.TextAlign = HorizontalAlignment.Center;
            txtNew.UseSystemPasswordChar = true;
            // 
            // txtConfirm
            // 
            txtConfirm.BorderStyle = BorderStyle.FixedSingle;
            txtConfirm.Location = new Point(49, 92);
            txtConfirm.Name = "txtConfirm";
            txtConfirm.Size = new Size(200, 22);
            txtConfirm.TabIndex = 6;
            txtConfirm.TextAlign = HorizontalAlignment.Center;
            txtConfirm.UseSystemPasswordChar = true;
            // 
            // pnlHeader
            // 
            pnlHeader.Controls.Add(lblHeaderText);
            pnlHeader.Controls.Add(pictureBox1);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(404, 50);
            pnlHeader.TabIndex = 9;
            // 
            // lblHeaderText
            // 
            lblHeaderText.AutoSize = true;
            lblHeaderText.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblHeaderText.Location = new Point(232, 21);
            lblHeaderText.Name = "lblHeaderText";
            lblHeaderText.RightToLeft = RightToLeft.Yes;
            lblHeaderText.Size = new Size(80, 13);
            lblHeaderText.TabIndex = 8;
            lblHeaderText.Text = "تغییر رمز عبور ";
            lblHeaderText.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.InitialImage = null;
            pictureBox1.Location = new Point(296, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(100, 45);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 9;
            pictureBox1.TabStop = false;
            // 
            // pnlBody
            // 
            pnlBody.Controls.Add(lblConfirmPassword);
            pnlBody.Controls.Add(lblNewPassword);
            pnlBody.Controls.Add(lblOldPassword);
            pnlBody.Controls.Add(txtCurrent);
            pnlBody.Controls.Add(btnSave);
            pnlBody.Controls.Add(txtConfirm);
            pnlBody.Controls.Add(txtNew);
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.Font = new Font("Tahoma", 9F);
            pnlBody.Location = new Point(0, 50);
            pnlBody.Name = "pnlBody";
            pnlBody.Size = new Size(404, 161);
            pnlBody.TabIndex = 10;
            // 
            // lblConfirmPassword
            // 
            lblConfirmPassword.AutoSize = true;
            lblConfirmPassword.Font = new Font("Tahoma", 8F);
            lblConfirmPassword.Location = new Point(254, 97);
            lblConfirmPassword.Name = "lblConfirmPassword";
            lblConfirmPassword.RightToLeft = RightToLeft.Yes;
            lblConfirmPassword.Size = new Size(104, 13);
            lblConfirmPassword.TabIndex = 9;
            lblConfirmPassword.Text = " تکرار رمز عبور جدید :";
            lblConfirmPassword.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblNewPassword
            // 
            lblNewPassword.AutoSize = true;
            lblNewPassword.Font = new Font("Tahoma", 8F);
            lblNewPassword.Location = new Point(279, 59);
            lblNewPassword.Name = "lblNewPassword";
            lblNewPassword.RightToLeft = RightToLeft.Yes;
            lblNewPassword.Size = new Size(80, 13);
            lblNewPassword.TabIndex = 8;
            lblNewPassword.Text = " رمز عبور جدید :";
            lblNewPassword.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblOldPassword
            // 
            lblOldPassword.AutoSize = true;
            lblOldPassword.Font = new Font("Tahoma", 8F);
            lblOldPassword.Location = new Point(279, 32);
            lblOldPassword.Name = "lblOldPassword";
            lblOldPassword.RightToLeft = RightToLeft.Yes;
            lblOldPassword.Size = new Size(81, 13);
            lblOldPassword.TabIndex = 7;
            lblOldPassword.Text = "رمز عبور فعلی :";
            lblOldPassword.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FrmChangePassword
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.WhiteSmoke;
            ClientSize = new Size(404, 211);
            Controls.Add(pnlBody);
            Controls.Add(pnlHeader);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmChangePassword";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            pnlBody.ResumeLayout(false);
            pnlBody.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TextBox txtCurrent;
        private Button btnSave;
        private TextBox txtNew;
        private TextBox txtConfirm;
        private Panel pnlHeader;
        private Panel pnlBody;
        private Label lblOldPassword;
        private Label lblHeaderText;
        private Label lblConfirmPassword;
        private Label lblNewPassword;
        private PictureBox pictureBox1;
    }
}