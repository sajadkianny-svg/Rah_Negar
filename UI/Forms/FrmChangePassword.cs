using Rah_Negar.Core;
using Rah_Negar.Services;
using Rah_Negar.Utils;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Services.UI;

namespace Rah_Negar.UI.Forms;

public partial class FrmChangePassword : BaseForm
{
    /// <summary>
    /// مشخص می‌کند فرم در حالت عادی باز شده یا از مسیر بازیابی رمز
    /// </summary>
    private readonly ChangePasswordMode _mode;

    /// <summary>
    /// سازنده پیش‌فرض برای Designer
    /// </summary>
    public FrmChangePassword() : this(ChangePasswordMode.Normal)
    {
    }

    /// <summary>
    /// سازنده اصلی فرم تغییر رمز
    /// </summary>
    public FrmChangePassword(ChangePasswordMode mode)
    {
        InitializeComponent();
        _mode = mode;

        ApplyTheme();
        WireButtonHover();

        InitializeChangePasswordForm();

        AcceptButton = btnSave;
    }

    /// <summary>
    /// مقداردهی اولیه فرم و تنظیم ظاهر آن بر اساس حالت جاری
    /// </summary>
    private void InitializeChangePasswordForm()
    {
        try
        {
            ApplyModeUi();

            txtCurrent.Clear();
            txtNew.Clear();
            txtConfirm.Clear();

            if (_mode == ChangePasswordMode.Normal)
                txtCurrent.Focus();
            else
                txtNew.Focus();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmChangePassword.InitializeChangePasswordForm");
            UiMessageService.ShowError("خطا در آماده‌سازی فرم تغییر رمز", ex, "خطا");
        }
    }

    /// <summary>
    /// اعمال تنظیمات ظاهری فرم بر اساس حالت Normal یا Recovery
    /// </summary>
    private void ApplyModeUi()
    {
        bool isNormalMode = _mode == ChangePasswordMode.Normal;

        lblOldPassword.Visible = isNormalMode;
        txtCurrent.Visible = isNormalMode;

    }

    /// <summary>
    /// اعتبارسنجی کامل ورودی‌های فرم
    /// </summary>
    private bool ValidateInputs()
    {
        string currentPassword = txtCurrent.Text.Trim();
        string newPassword = txtNew.Text.Trim();
        string confirmPassword = txtConfirm.Text.Trim();

        // در حالت عادی، وارد کردن و صحت رمز فعلی الزامی است
        if (_mode == ChangePasswordMode.Normal)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                UiMessageService.ShowWarning("لطفاً رمز فعلی را وارد کنید", "اعتبارسنجی");

                txtCurrent.Focus();
                return false;
            }

            if (!PasswordManagementService.VerifyCurrentPassword(currentPassword))
            {
                UiMessageService.ShowWarning("رمز فعلی اشتباه است", "اعتبارسنجی");

                txtCurrent.SelectAll();
                txtCurrent.Focus();
                return false;
            }
        }

        // رمز جدید خالی نباشد
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            UiMessageService.ShowWarning("لطفاً رمز جدید را وارد کنید", "اعتبارسنجی");

            txtNew.Focus();
            return false;
        }

        // تکرار رمز جدید خالی نباشد
        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            UiMessageService.ShowWarning("لطفاً تکرار رمز جدید را وارد کنید", "اعتبارسنجی");

            txtConfirm.Focus();
            return false;
        }

        // اعتبارسنجی امنیتی رمز جدید
        if (!PasswordManagementService.ValidateNewPassword(newPassword, out string errorMessage))
        {
            UiMessageService.ShowWarning(errorMessage, "اعتبارسنجی");

            txtNew.SelectAll();
            txtNew.Focus();
            return false;
        }

        // تطابق رمز جدید با تکرار آن
        if (newPassword != confirmPassword)
        {
            UiMessageService.ShowWarning("رمز جدید با تکرار آن مطابقت ندارد", "اعتبارسنجی");

            txtConfirm.SelectAll();
            txtConfirm.Focus();
            return false;
        }

        return true;
    }

    /// <summary>
    /// ذخیره رمز جدید در دیتابیس
    /// </summary>
    private void btnSave_Click(object sender, EventArgs e)
    {
        try
        {
            if (!ValidateInputs())
                return;

            string newPassword = txtNew.Text.Trim();

            PasswordManagementService.UpdatePassword(newPassword);
            UiMessageService.ShowSuccess("رمز عبور با موفقیت تغییر یافت", "موفق");

            txtCurrent.Clear();
            txtNew.Clear();
            txtConfirm.Clear();

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmChangePassword.btnSave_Click");
            UiMessageService.ShowError("خطا در تغییر رمز عبور", ex, "خطا");
        }
    }

    private void ApplyTheme()
    {
        var palette = AppThemeManager.CurrentPalette;

        BackColor = palette.FormBackColor;

        pnlHeader.BackColor = palette.HeaderBackColor;
        pnlBody.BackColor = palette.ContentBackColor;

        // Header
        lblHeaderText.ForeColor = palette.TextOnAccentColor;
        lblHeaderText.Font = new Font("tahoma", 9F, FontStyle.Bold);

        // Labels
        ApplyLabelStyle(lblOldPassword);
        ApplyLabelStyle(lblNewPassword);
        ApplyLabelStyle(lblConfirmPassword);

        // TextBox ها
        ApplyTextBoxStyle(txtCurrent);
        ApplyTextBoxStyle(txtNew);
        ApplyTextBoxStyle(txtConfirm);

        // دکمه اصلی
        ApplyPrimaryButton(btnSave);
    }


    private static void ApplyLabelStyle(Label lbl)
    {
        var palette = AppThemeManager.CurrentPalette;

        lbl.ForeColor = palette.TextPrimaryColor;
        lbl.BackColor = Color.Transparent;
        lbl.Font = new Font("tahoma", 9F);
        lbl.TextAlign = ContentAlignment.MiddleRight;
    }

    private static void ApplyTextBoxStyle(TextBox txt)
    {
        var palette = AppThemeManager.CurrentPalette;

        txt.BorderStyle = BorderStyle.FixedSingle;
        txt.Font = new Font("tahoma", 9F);

        // مهم: برای همه تم‌ها خوب جواب میده
        txt.BackColor = Color.White;
        txt.ForeColor = Color.FromArgb(30, 30, 30);

        txt.UseSystemPasswordChar = true;
    }

    private static void ApplyPrimaryButton(Button btn)
    {
        var palette = AppThemeManager.CurrentPalette;

        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;

        btn.BackColor = palette.PrimaryButtonBackColor;
        btn.ForeColor = palette.TextOnAccentColor;

        btn.FlatAppearance.MouseOverBackColor = palette.PrimaryButtonHoverColor;
        btn.FlatAppearance.MouseDownBackColor = palette.PrimaryButtonDownColor;

        btn.Font = new Font("tahoma", 9.5F, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
    }

    private void WireButtonHover()
    {
        btnSave.MouseEnter += (_, _) =>
        {
            btnSave.Top -= 1;
        };

        btnSave.MouseLeave += (_, _) =>
        {
            btnSave.Top += 1;
        };
    }

}