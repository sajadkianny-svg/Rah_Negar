using Rah_Negar.Core;
using Rah_Negar.Models;
using Rah_Negar.Services;
using Rah_Negar.Services.UI;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Utils;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.UI.Forms;

public partial class FrmLogin : BaseForm
{
    private AppSettingsModel? _appSettings;
    private bool _isLoginInProgress;

    public FrmLogin() : this(initializeSettings: true)
    {
    }

    internal FrmLogin(bool initializeSettings)
    {
        InitializeComponent();

        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);

        LoadBrandImage();

        if (initializeSettings)
            InitializeLoginForm();

        LayoutLoginCard();
    }

    private void FrmLogin_Load(object sender, EventArgs e)
    {
        ApplyLoginPalette();
        LayoutLoginCard();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        txtPass.Focus();
    }

    private void InitializeLoginForm()
    {
        try
        {
            _appSettings = AppSettingsService.GetSettings();

            if (_appSettings == null || !_appSettings.IsInitialized)
            {
                UiMessageService.ShowError("برنامه هنوز راه‌اندازی اولیه نشده است", "خطا");
                Close();
                return;
            }

            SetConfiguredStationName(_appSettings.StationName);
            LayoutLoginCard();
            txtPass.Focus();
        }
        catch (Exception ex)
        {
            UiMessageService.ShowError("خطا در بارگذاری فرم ورود", ex, "خطا");
            Close();
        }
    }

    private void SetConfiguredStationName(string? configuredStationName)
    {
        string displayName = StationIdentityProvider.ResolveForLogin(configuredStationName);
        lblUserValue.Text = displayName;
        stationNameTip.SetToolTip(lblUserValue, displayName);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        LayoutLoginCard();
    }

    private void LayoutLoginCard()
    {
        if (pnlLoginArea == null || pnlLoginCard == null)
            return;

        int x = Math.Max(0, (pnlLoginArea.ClientSize.Width - pnlLoginCard.Width) / 2);
        int y = Math.Max(0, (pnlLoginArea.ClientSize.Height - pnlLoginCard.Height) / 2);
        Point location = new(x, y);

        // The shared Tahoma/button rule may increase the button height after
        // Designer scaling. Keep the input row a real RTL composition unit.
        pnlTextBox.Height = Math.Max(pnlTextBox.Height, btnLogin.Height);
        txtPass.Width = Math.Max(txtPass.Width, 251);

        if (pnlLoginCard.Location != location)
            pnlLoginCard.Location = location;
    }

    private void btnLogin_Click(object sender, EventArgs e)
    {
        if (_isLoginInProgress)
            return;

        _isLoginInProgress = true;
        btnLogin.Enabled = false;
        lblLoginError.Visible = false;

        try
        {
            if (_appSettings == null)
            {
                UiMessageService.ShowError("پیکره‌بندی به‌درستی صورت نگرفته است", "خطا");
                return;
            }

            string password = txtPass.Text.Trim();

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowLoginError("کلمه عبور را وارد کنید.");
                txtPass.Focus();
                return;
            }

            bool isValid = PasswordHelper.VerifyPassword(
                password,
                _appSettings.UserResetPasswordSalt,
                _appSettings.UserResetPasswordHash);

            if (!isValid)
            {
                ShowLoginError("کلمه عبور نادرست است.");
                txtPass.SelectAll();
                txtPass.Focus();
                return;
            }

            AppSession.Login();
            Hide();

            using FrmMain main = new();
            main.ShowDialog();
            Close();
        }
        catch (Exception ex)
        {
            UiMessageService.ShowError("خطا در هنگام ورود", ex, "خطا");
        }
        finally
        {
            _isLoginInProgress = false;
            if (!IsDisposed)
                btnLogin.Enabled = true;
        }
    }

    private void lnkForgot_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            if (_appSettings == null)
            {
                UiMessageService.ShowError("تنظیمات برنامه بارگذاری نشده است", "خطا");
                return;
            }

            using FrmRecovery frm = new(_appSettings.StationName);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                _appSettings = AppSettingsService.GetSettings();
                txtPass.Clear();
                lblLoginError.Visible = false;
                txtPass.Focus();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmLogin.lnkForgot_LinkClicked");
            UiMessageService.ShowError("خطا در باز کردن فرم بازیابی", ex, "خطا");
        }
    }

    private void lnkChangePass_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            using FrmChangePassword frm = new(ChangePasswordMode.Normal);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                _appSettings = AppSettingsService.GetSettings();
                txtPass.Clear();
                lblLoginError.Visible = false;
                txtPass.Focus();
            }
        }
        catch (Exception ex)
        {
            UiMessageService.ShowError("خطا در باز کردن فرم تغییر رمز", ex, "خطا");
        }
    }

    private void ApplyLoginPalette()
    {
        AppThemePalette palette = AppThemeManager.CurrentPalette;
        BackColor = palette.FormBackColor;
        pnlBack.BackColor = palette.FormBackColor;
        pnlLoginArea.BackColor = palette.FormBackColor;
        pnlLoginCard.BackColor = palette.CardBackColor;
        pnlLoginCard.ForeColor = palette.TextPrimaryColor;
        lblTitr.ForeColor = palette.TextPrimaryColor;
        lblSubTitr.ForeColor = palette.TextSecondaryColor;
        lblUserValue.ForeColor = palette.TextPrimaryColor;
        lblPassword.ForeColor = palette.TextSecondaryColor;
        lnkChangePass.LinkColor = palette.PrimaryButtonBackColor;
        lnkChangePass.ActiveLinkColor = palette.PrimaryButtonHoverColor;
        lnkChangePass.VisitedLinkColor = palette.PrimaryButtonDownColor;
        lnkForgot.LinkColor = palette.PrimaryButtonBackColor;
        lnkForgot.ActiveLinkColor = palette.PrimaryButtonHoverColor;
        lnkForgot.VisitedLinkColor = palette.PrimaryButtonDownColor;
        btnLogin.BackColor = palette.PrimaryButtonBackColor;
        btnLogin.FlatAppearance.MouseOverBackColor = palette.PrimaryButtonHoverColor;
        btnLogin.FlatAppearance.MouseDownBackColor = palette.PrimaryButtonDownColor;
        btnLogin.FlatAppearance.BorderColor = palette.PrimaryButtonBackColor;
        lblDivider.BackColor = palette.DividerBackColor;
        pnlBrand.BackColor = palette.HeaderBackColor;
    }

    private void LoadBrandImage()
    {
        string logoPath = Path.Combine(AppContext.BaseDirectory, "DataFiles", "LOGO.png");
        if (!File.Exists(logoPath))
            return;

        try
        {
            using Image source = Image.FromFile(logoPath);
            picLogo.Image = new Bitmap(source);
        }
        catch
        {
            picLogo.Image = null;
        }
    }

    private void ShowLoginError(string message)
    {
        lblLoginError.Text = message;
        lblLoginError.Visible = true;
    }
}
