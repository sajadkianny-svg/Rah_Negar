namespace Rah_Negar.UI.Forms
{
    partial class FrmAbout
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
            label2 = new Label();
            lblTitle = new Label();
            label1 = new Label();
            lblSubtitle = new Label();
            lblAppName = new Label();
            btnClose = new Button();
            lblCopyright = new Label();
            lblDeveloper = new Label();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.BorderStyle = BorderStyle.FixedSingle;
            pnlHeader.Controls.Add(label2);
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(label1);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(lblAppName);
            pnlHeader.Controls.Add(btnClose);
            pnlHeader.Controls.Add(lblCopyright);
            pnlHeader.Controls.Add(lblDeveloper);
            pnlHeader.Dock = DockStyle.Fill;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(404, 211);
            pnlHeader.TabIndex = 0;
            pnlHeader.Paint += pnlHeader_Paint;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.BackColor = Color.Transparent;
            label2.Font = new Font("Tahoma", 8.25F, FontStyle.Bold);
            label2.Location = new Point(138, 156);
            label2.Name = "label2";
            label2.Size = new Size(132, 13);
            label2.TabIndex = 9;
            label2.Text = "تمام حقوق محفوظ است";
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Font = new Font("Tahoma", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTitle.ForeColor = Color.Black;
            lblTitle.Location = new Point(125, 78);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(159, 13);
            lblTitle.TabIndex = 8;
            lblTitle.Text = "ایستگاه های تقویت فشار گاز";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = Color.Transparent;
            label1.Font = new Font("Tahoma", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.Black;
            label1.Location = new Point(99, 88);
            label1.Name = "label1";
            label1.Size = new Size(0, 13);
            label1.TabIndex = 7;
            // 
            // lblSubtitle
            // 
            lblSubtitle.AutoSize = true;
            lblSubtitle.BackColor = Color.Transparent;
            lblSubtitle.Font = new Font("Tahoma", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblSubtitle.ForeColor = Color.Black;
            lblSubtitle.Location = new Point(102, 62);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(205, 13);
            lblSubtitle.TabIndex = 1;
            lblSubtitle.Text = "سامانه ثبت و تحلیل داده های عملیاتی";
            // 
            // lblAppName
            // 
            lblAppName.AutoSize = true;
            lblAppName.BackColor = Color.Transparent;
            lblAppName.Font = new Font("Tahoma", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblAppName.Location = new Point(170, 45);
            lblAppName.Name = "lblAppName";
            lblAppName.Size = new Size(69, 14);
            lblAppName.TabIndex = 0;
            lblAppName.Text = "ره نــــــــگار";
            // 
            // btnClose
            // 
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 64, 64);
            btnClose.FlatAppearance.MouseOverBackColor = Color.LightCoral;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("Tahoma", 10F, FontStyle.Bold);
            btnClose.Location = new Point(0, -5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(27, 27);
            btnClose.TabIndex = 6;
            btnClose.Text = "×";
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += btnClose_Click;
            // 
            // lblCopyright
            // 
            lblCopyright.AutoSize = true;
            lblCopyright.BackColor = Color.Transparent;
            lblCopyright.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblCopyright.Location = new Point(153, 174);
            lblCopyright.Name = "lblCopyright";
            lblCopyright.Size = new Size(103, 13);
            lblCopyright.TabIndex = 5;
            lblCopyright.Text = " 1405 | نسخه 1.0";
            // 
            // lblDeveloper
            // 
            lblDeveloper.AutoSize = true;
            lblDeveloper.BackColor = Color.Transparent;
            lblDeveloper.Font = new Font("Tahoma", 8.25F, FontStyle.Bold);
            lblDeveloper.Location = new Point(120, 140);
            lblDeveloper.Name = "lblDeveloper";
            lblDeveloper.Size = new Size(168, 13);
            lblDeveloper.TabIndex = 4;
            lblDeveloper.Text = "طراحی و توسعه :  سجاد کیانی";
            // 
            // FrmAbout
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(404, 211);
            Controls.Add(pnlHeader);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmAbout";
            RightToLeft = RightToLeft.No;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "About";
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblAppName;
        private Label lblSubtitle;
        private Label lblDeveloper;
        private Label lblCopyright;
        private Button btnClose;
        private Label label1;
        private Label lblTitle;
        private Label label2;
    }
}