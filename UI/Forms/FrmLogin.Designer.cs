namespace Rah_Negar.UI.Forms;

partial class FrmLogin
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmLogin));
        txtPass = new TextBox();
        btnLogin = new Button();
        lblPassword = new Label();
        lblUserValue = new Label();
        lblLoginError = new Label();
        lnkForgot = new LinkLabel();
        lblTitr = new Label();
        lblSubTitr = new Label();
        lnkChangePass = new LinkLabel();
        lblDivider = new Label();
        pnlBack = new Panel();
        pnlLoginArea = new Panel();
        pnlLoginCard = new Panel();
        pnlTextBox = new Panel();
        pnlBrand = new Panel();
        picLogo = new PictureBox();
        stationNameTip = new ToolTip(components);
        pnlBack.SuspendLayout();
        pnlLoginArea.SuspendLayout();
        pnlLoginCard.SuspendLayout();
        pnlTextBox.SuspendLayout();
        pnlBrand.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picLogo).BeginInit();
        SuspendLayout();
        //
        // txtPass
        //
        txtPass.BackColor = Color.White;
        txtPass.BorderStyle = BorderStyle.FixedSingle;
        txtPass.Font = new Font("Tahoma", 10F);
        txtPass.Location = new Point(0, 0);
        txtPass.Name = "txtPass";
        txtPass.Size = new Size(250, 25);
        txtPass.TabIndex = 0;
        txtPass.TextAlign = HorizontalAlignment.Center;
        txtPass.UseSystemPasswordChar = true;
        txtPass.WordWrap = false;
        //
        // btnLogin
        //
        btnLogin.BackColor = Color.FromArgb(88, 126, 160);
        btnLogin.FlatAppearance.BorderColor = Color.Black;
        btnLogin.FlatAppearance.BorderSize = 0;
        btnLogin.FlatStyle = FlatStyle.Flat;
        btnLogin.Font = new Font("Tahoma", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        btnLogin.ForeColor = Color.White;
        btnLogin.Location = new Point(248, 0);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(90, 25);
        btnLogin.TabIndex = 1;
        btnLogin.Text = "ورود";
        btnLogin.UseVisualStyleBackColor = false;
        btnLogin.Click += btnLogin_Click;
        //
        // lblPassword
        //
        lblPassword.BackColor = Color.Transparent;
        lblPassword.Font = new Font("Tahoma", 9F);
        lblPassword.ForeColor = Color.FromArgb(92, 105, 118);
        lblPassword.Location = new Point(192, 154);
        lblPassword.Name = "lblPassword";
        lblPassword.RightToLeft = RightToLeft.Yes;
        lblPassword.Size = new Size(64, 24);
        lblPassword.TabIndex = 11;
        lblPassword.Text = "کلمه عبور";
        lblPassword.TextAlign = ContentAlignment.MiddleCenter;
        //
        // lblUserValue
        //
        lblUserValue.AutoEllipsis = true;
        lblUserValue.BackColor = Color.Transparent;
        lblUserValue.Font = new Font("Tahoma", 10F, FontStyle.Bold);
        lblUserValue.ForeColor = Color.FromArgb(35, 43, 52);
        lblUserValue.Location = new Point(16, 100);
        lblUserValue.Name = "lblUserValue";
        lblUserValue.RightToLeft = RightToLeft.Yes;
        lblUserValue.Size = new Size(416, 44);
        lblUserValue.TabIndex = 8;
        lblUserValue.Text = "ایستگاه عملیاتی";
        lblUserValue.TextAlign = ContentAlignment.MiddleCenter;
        lblUserValue.UseMnemonic = false;
        //
        // lblLoginError
        //
        lblLoginError.BackColor = Color.Transparent;
        lblLoginError.Font = new Font("Tahoma", 8.5F);
        lblLoginError.ForeColor = Color.FromArgb(166, 55, 55);
        lblLoginError.Location = new Point(16, 232);
        lblLoginError.Name = "lblLoginError";
        lblLoginError.RightToLeft = RightToLeft.Yes;
        lblLoginError.Size = new Size(416, 28);
        lblLoginError.TabIndex = 12;
        lblLoginError.TextAlign = ContentAlignment.MiddleCenter;
        lblLoginError.Visible = false;
        //
        // lnkForgot
        //
        lnkForgot.ActiveLinkColor = Color.FromArgb(104, 142, 176);
        lnkForgot.BackColor = Color.Transparent;
        lnkForgot.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lnkForgot.LinkBehavior = LinkBehavior.HoverUnderline;
        lnkForgot.LinkColor = Color.FromArgb(88, 126, 160);
        lnkForgot.Location = new Point(16, 313);
        lnkForgot.Name = "lnkForgot";
        lnkForgot.RightToLeft = RightToLeft.Yes;
        lnkForgot.Size = new Size(416, 24);
        lnkForgot.TabIndex = 3;
        lnkForgot.TabStop = true;
        lnkForgot.Text = "کلمه عبور را فراموش کردم";
        lnkForgot.TextAlign = ContentAlignment.MiddleCenter;
        lnkForgot.VisitedLinkColor = Color.FromArgb(67, 98, 128);
        lnkForgot.LinkClicked += lnkForgot_LinkClicked;
        //
        // lblTitr
        //
        lblTitr.BackColor = Color.Transparent;
        lblTitr.Font = new Font("Tahoma", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lblTitr.ForeColor = Color.FromArgb(35, 43, 52);
        lblTitr.Location = new Point(16, 20);
        lblTitr.Name = "lblTitr";
        lblTitr.RightToLeft = RightToLeft.Yes;
        lblTitr.Size = new Size(416, 42);
        lblTitr.TabIndex = 0;
        lblTitr.Text = "ره‌نگار";
        lblTitr.TextAlign = ContentAlignment.MiddleCenter;
        //
        // lblSubTitr
        //
        lblSubTitr.BackColor = Color.Transparent;
        lblSubTitr.Font = new Font("Tahoma", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblSubTitr.ForeColor = Color.FromArgb(92, 105, 118);
        lblSubTitr.Location = new Point(16, 64);
        lblSubTitr.Name = "lblSubTitr";
        lblSubTitr.RightToLeft = RightToLeft.Yes;
        lblSubTitr.Size = new Size(416, 28);
        lblSubTitr.TabIndex = 13;
        lblSubTitr.Text = "سامانه پایش و تحلیل داده‌های عملیاتی";
        lblSubTitr.TextAlign = ContentAlignment.MiddleCenter;
        //
        // lnkChangePass
        //
        lnkChangePass.ActiveLinkColor = Color.FromArgb(104, 142, 176);
        lnkChangePass.BackColor = Color.Transparent;
        lnkChangePass.Font = new Font("Tahoma", 9F);
        lnkChangePass.LinkBehavior = LinkBehavior.HoverUnderline;
        lnkChangePass.LinkColor = Color.FromArgb(88, 126, 160);
        lnkChangePass.Location = new Point(16, 281);
        lnkChangePass.Name = "lnkChangePass";
        lnkChangePass.RightToLeft = RightToLeft.Yes;
        lnkChangePass.Size = new Size(416, 24);
        lnkChangePass.TabIndex = 2;
        lnkChangePass.TabStop = true;
        lnkChangePass.Text = "تغییر کلمه عبور";
        lnkChangePass.TextAlign = ContentAlignment.MiddleCenter;
        lnkChangePass.VisitedLinkColor = Color.FromArgb(67, 98, 128);
        lnkChangePass.LinkClicked += lnkChangePass_LinkClicked;
        //
        // lblDivider
        //
        lblDivider.BackColor = Color.FromArgb(215, 222, 230);
        lblDivider.Location = new Point(16, 268);
        lblDivider.Name = "lblDivider";
        lblDivider.Size = new Size(416, 1);
        lblDivider.TabIndex = 14;
        //
        // pnlBack
        //
        pnlBack.BackColor = Color.FromArgb(244, 247, 250);
        pnlBack.Controls.Add(pnlLoginArea);
        pnlBack.Controls.Add(pnlBrand);
        pnlBack.Dock = DockStyle.Fill;
        pnlBack.Location = new Point(0, 0);
        pnlBack.Name = "pnlBack";
        pnlBack.RightToLeft = RightToLeft.No;
        pnlBack.Size = new Size(634, 361);
        pnlBack.TabIndex = 0;
        //
        // pnlLoginArea
        //
        pnlLoginArea.BackColor = Color.FromArgb(244, 247, 250);
        pnlLoginArea.Controls.Add(pnlLoginCard);
        pnlLoginArea.Dock = DockStyle.Fill;
        pnlLoginArea.Location = new Point(167, 0);
        pnlLoginArea.Name = "pnlLoginArea";
        pnlLoginArea.RightToLeft = RightToLeft.No;
        pnlLoginArea.Size = new Size(467, 361);
        pnlLoginArea.TabIndex = 0;
        //
        // pnlLoginCard
        //
        pnlLoginCard.Anchor = AnchorStyles.None;
        pnlLoginCard.BackColor = Color.FromArgb(252, 253, 254);
        pnlLoginCard.BorderStyle = BorderStyle.FixedSingle;
        pnlLoginCard.Controls.Add(lblDivider);
        pnlLoginCard.Controls.Add(lnkForgot);
        pnlLoginCard.Controls.Add(lnkChangePass);
        pnlLoginCard.Controls.Add(lblLoginError);
        pnlLoginCard.Controls.Add(pnlTextBox);
        pnlLoginCard.Controls.Add(lblPassword);
        pnlLoginCard.Controls.Add(lblUserValue);
        pnlLoginCard.Controls.Add(lblSubTitr);
        pnlLoginCard.Controls.Add(lblTitr);
        pnlLoginCard.Location = new Point(9, 3);
        pnlLoginCard.Name = "pnlLoginCard";
        pnlLoginCard.RightToLeft = RightToLeft.No;
        pnlLoginCard.Size = new Size(449, 355);
        pnlLoginCard.TabIndex = 0;
        //
        // pnlTextBox
        //
        pnlTextBox.BackColor = Color.Transparent;
        pnlTextBox.Controls.Add(btnLogin);
        pnlTextBox.Controls.Add(txtPass);
        pnlTextBox.Location = new Point(54, 193);
        pnlTextBox.Name = "pnlTextBox";
        pnlTextBox.RightToLeft = RightToLeft.No;
        pnlTextBox.Size = new Size(340, 27);
        pnlTextBox.TabIndex = 0;
        //
        // pnlBrand
        //
        pnlBrand.BackColor = Color.FromArgb(79, 105, 132);
        pnlBrand.Controls.Add(picLogo);
        pnlBrand.Dock = DockStyle.Left;
        pnlBrand.Location = new Point(0, 0);
        pnlBrand.Name = "pnlBrand";
        pnlBrand.RightToLeft = RightToLeft.No;
        pnlBrand.Size = new Size(167, 361);
        pnlBrand.TabIndex = 1;
        //
        // picLogo
        //
        picLogo.BackColor = Color.Transparent;
        picLogo.Image = (Image)resources.GetObject("picLogo.Image");
        picLogo.InitialImage = (Image)resources.GetObject("picLogo.InitialImage");
        picLogo.Location = new Point(11, 74);
        picLogo.Name = "picLogo";
        picLogo.Size = new Size(150, 150);
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.TabIndex = 0;
        picLogo.TabStop = false;
        //
        // FrmLogin
        //
        AcceptButton = btnLogin;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(244, 247, 250);
        ClientSize = new Size(634, 361);
        Controls.Add(pnlBack);
        Font = new Font("Tahoma", 9F);
        ForeColor = Color.FromArgb(35, 43, 52);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(600, 400);
        Name = "FrmLogin";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ورود به ره‌نگار";
        Load += FrmLogin_Load;
        pnlBack.ResumeLayout(false);
        pnlLoginArea.ResumeLayout(false);
        pnlLoginCard.ResumeLayout(false);
        pnlTextBox.ResumeLayout(false);
        pnlTextBox.PerformLayout();
        pnlBrand.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)picLogo).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private TextBox txtPass;
    private Button btnLogin;
    private Label lblPassword;
    private Label lblUserValue;
    private Label lblLoginError;
    private LinkLabel lnkForgot;
    private Label lblTitr;
    private Label lblSubTitr;
    private LinkLabel lnkChangePass;
    private Label lblDivider;
    private Panel pnlBack;
    private Panel pnlLoginArea;
    private Panel pnlLoginCard;
    private Panel pnlTextBox;
    private Panel pnlBrand;
    private PictureBox picLogo;
    private ToolTip stationNameTip;
}
