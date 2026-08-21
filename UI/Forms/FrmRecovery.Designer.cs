
namespace Rah_Negar.UI.Forms
{
    partial class FrmRecovery
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmRecovery));
            txtRecoveryCode = new TextBox();
            btnVerify = new Button();
            txtRequestId = new TextBox();
            lblRequestId = new Label();
            btnGenerateRequest = new Button();
            pnlHeader = new Panel();
            lblHeaderText = new Label();
            pictureBox1 = new PictureBox();
            pnlBody = new Panel();
            lblRecoveryCode = new Label();
            pnlHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            pnlBody.SuspendLayout();
            SuspendLayout();
            // 
            // txtRecoveryCode
            // 
            txtRecoveryCode.BorderStyle = BorderStyle.FixedSingle;
            txtRecoveryCode.Location = new Point(42, 75);
            txtRecoveryCode.Name = "txtRecoveryCode";
            txtRecoveryCode.Size = new Size(223, 22);
            txtRecoveryCode.TabIndex = 1;
            // 
            // btnVerify
            // 
            btnVerify.BackColor = Color.SteelBlue;
            btnVerify.FlatAppearance.BorderColor = Color.Silver;
            btnVerify.FlatStyle = FlatStyle.Flat;
            btnVerify.Font = new Font("Tahoma", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnVerify.ForeColor = Color.White;
            btnVerify.Location = new Point(42, 123);
            btnVerify.Name = "btnVerify";
            btnVerify.Size = new Size(90, 24);
            btnVerify.TabIndex = 2;
            btnVerify.Text = "بررسی کد بازیابی";
            btnVerify.UseVisualStyleBackColor = false;
            btnVerify.Click += btnVerify_Click;
            // 
            // txtRequestId
            // 
            txtRequestId.BackColor = Color.White;
            txtRequestId.BorderStyle = BorderStyle.FixedSingle;
            txtRequestId.Enabled = false;
            txtRequestId.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            txtRequestId.Location = new Point(140, 39);
            txtRequestId.Name = "txtRequestId";
            txtRequestId.ReadOnly = true;
            txtRequestId.Size = new Size(125, 22);
            txtRequestId.TabIndex = 3;
            txtRequestId.TextAlign = HorizontalAlignment.Center;
            // 
            // lblRequestId
            // 
            lblRequestId.AutoSize = true;
            lblRequestId.Font = new Font("Tahoma", 8F);
            lblRequestId.ForeColor = Color.Black;
            lblRequestId.Location = new Point(272, 43);
            lblRequestId.Name = "lblRequestId";
            lblRequestId.RightToLeft = RightToLeft.Yes;
            lblRequestId.Size = new Size(97, 13);
            lblRequestId.TabIndex = 4;
            lblRequestId.Text = "شناسه درخواست :";
            // 
            // btnGenerateRequest
            // 
            btnGenerateRequest.BackColor = Color.SlateGray;
            btnGenerateRequest.FlatAppearance.BorderColor = Color.Black;
            btnGenerateRequest.FlatStyle = FlatStyle.Flat;
            btnGenerateRequest.Font = new Font("Tahoma", 8F);
            btnGenerateRequest.ForeColor = Color.White;
            btnGenerateRequest.Location = new Point(42, 39);
            btnGenerateRequest.Name = "btnGenerateRequest";
            btnGenerateRequest.Size = new Size(100, 23);
            btnGenerateRequest.TabIndex = 7;
            btnGenerateRequest.Text = "تولید شناسه";
            btnGenerateRequest.UseVisualStyleBackColor = false;
            btnGenerateRequest.Click += btnGenerate_Click;
            // 
            // pnlHeader
            // 
            pnlHeader.Controls.Add(lblHeaderText);
            pnlHeader.Controls.Add(pictureBox1);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(404, 47);
            pnlHeader.TabIndex = 10;
            // 
            // lblHeaderText
            // 
            lblHeaderText.AutoSize = true;
            lblHeaderText.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblHeaderText.Location = new Point(219, 20);
            lblHeaderText.Name = "lblHeaderText";
            lblHeaderText.RightToLeft = RightToLeft.Yes;
            lblHeaderText.Size = new Size(93, 13);
            lblHeaderText.TabIndex = 8;
            lblHeaderText.Text = "بازیابی رمز عبور ";
            lblHeaderText.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.InitialImage = null;
            pictureBox1.Location = new Point(296, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(100, 42);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 9;
            pictureBox1.TabStop = false;
            // 
            // pnlBody
            // 
            pnlBody.Controls.Add(lblRecoveryCode);
            pnlBody.Controls.Add(txtRequestId);
            pnlBody.Controls.Add(txtRecoveryCode);
            pnlBody.Controls.Add(lblRequestId);
            pnlBody.Controls.Add(btnGenerateRequest);
            pnlBody.Controls.Add(btnVerify);
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.Location = new Point(0, 47);
            pnlBody.Name = "pnlBody";
            pnlBody.Size = new Size(404, 164);
            pnlBody.TabIndex = 11;
            // 
            // lblRecoveryCode
            // 
            lblRecoveryCode.AutoSize = true;
            lblRecoveryCode.Font = new Font("Tahoma", 8F);
            lblRecoveryCode.ForeColor = Color.Black;
            lblRecoveryCode.Location = new Point(272, 79);
            lblRecoveryCode.Name = "lblRecoveryCode";
            lblRecoveryCode.RightToLeft = RightToLeft.Yes;
            lblRecoveryCode.Size = new Size(58, 13);
            lblRecoveryCode.TabIndex = 8;
            lblRecoveryCode.Text = "کد بازیابی :";
            // 
            // FrmRecovery
            // 
            AutoScaleDimensions = new SizeF(7F, 14F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Gainsboro;
            ClientSize = new Size(404, 211);
            Controls.Add(pnlBody);
            Controls.Add(pnlHeader);
            Font = new Font("Tahoma", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmRecovery";
            StartPosition = FormStartPosition.CenterScreen;
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            pnlBody.ResumeLayout(false);
            pnlBody.PerformLayout();
            ResumeLayout(false);
        }

        private void btnVerify_Click_1(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
        private TextBox txtRecoveryCode;
        private Button btnVerify;
        private TextBox txtRequestId;
        private Label lblRequestId;
        private Button btnGenerateRequest;
        private Panel pnlHeader;
        private Label lblHeaderText;
        private PictureBox pictureBox1;
        private Panel pnlBody;
        private Label lblRecoveryCode;
    }
}