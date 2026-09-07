using Rah_Negar.Core;
using Rah_Negar.Services.UI;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Utils;

namespace Rah_Negar.UI.Forms;

public partial class FrmRecovery : BaseForm
{
    /// <summary>
    /// سازنده پیش‌فرض برای Designer
    /// </summary>
    public FrmRecovery() : this(string.Empty)
    {
    }

    /// <summary>
    /// سازنده اصلی فرم بازیابی
    /// </summary>
    public FrmRecovery(string stationName)
    {
        InitializeComponent();

        btnGenerateRequest.Enabled = false;
        btnVerify.Enabled = false;
        txtRecoveryCode.Enabled = false;
        lblHeaderText.Text = "بازیابی رمز عبور (مدیریت‌شده)";

        ApplyTheme();

        InitializeRecoveryForm();
        AcceptButton = btnVerify;
    }

    /// <summary>
    /// مقداردهی اولیه فرم
    /// </summary>
    private void InitializeRecoveryForm()
    {
        try
        {
            txtRequestId.Clear();
            txtRecoveryCode.Clear();

            txtRequestId.ReadOnly = true;
            txtRecoveryCode.Focus();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmRecovery.InitializeRecoveryForm");
            UiMessageService.ShowError("خطا در آماده‌سازی فرم بازیابی", "خطا");
        }
    }

    /// <summary>
    /// ساخت شناسه بازیابی و نمایش آن در فرم
    /// </summary>
    private void btnGenerate_Click(object sender, EventArgs e)
    {
        UiMessageService.ShowWarning(
            "بازیابی خودکار غیرفعال است. برای تغییر رمز، فرایند مدیریت‌شده و مجاز را اجرا کنید.",
            "بازیابی در دسترس نیست");
    }

    /// <summary>
    /// بررسی کد بازیابی واردشده توسط کاربر
    /// </summary>
    private void btnVerify_Click(object sender, EventArgs e)
    {
        UiMessageService.ShowWarning(
            "کد بازیابی محلی قابل قبول نیست. این عملیات بدون مدرک مدیریت‌شده انجام نمی‌شود.",
            "بازیابی رد شد");
    }

    private void ApplyTheme()
    {
        var palette = AppThemeManager.CurrentPalette;

        BackColor = palette.FormBackColor;

        pnlHeader.BackColor = palette.HeaderBackColor;
        pnlBody.BackColor = palette.ContentBackColor;

        // Header
        lblHeaderText.ForeColor = palette.TextOnAccentColor;
        lblHeaderText.Font = new Font("Tahoma", 8F, FontStyle.Bold);

        // Labels
        ApplyLabelStyle(lblRequestId);
        ApplyLabelStyle(lblRecoveryCode);

        // TextBox ها
        ApplyTextBoxStyle(txtRequestId, readOnly: true);
        ApplyTextBoxStyle(txtRecoveryCode, readOnly: false);

        // دکمه‌ها
        ApplyPrimaryButton(btnGenerateRequest);
        ApplyPrimaryButton(btnVerify);

    }

    private static void ApplyLabelStyle(Label lbl)
    {
        var palette = AppThemeManager.CurrentPalette;

        lbl.ForeColor = palette.TextPrimaryColor;
        lbl.BackColor = Color.Transparent;
        lbl.Font = new Font("Tahoma", 9F);
        lbl.TextAlign = ContentAlignment.MiddleRight;
    }

    private static void ApplyTextBoxStyle(TextBox txt, bool readOnly)
    {
        var palette = AppThemeManager.CurrentPalette;

        txt.BorderStyle = BorderStyle.FixedSingle;
        txt.Font = new Font("tahoma", 9F);

        if (readOnly)
        {
            // برای RequestId
            txt.BackColor = ControlPaint.Light(palette.ContentBackColor, 0.15f);
            txt.ForeColor = palette.TextSecondaryColor;
        }
        else
        {
            // برای RecoveryCode
            txt.BackColor = Color.White;
            txt.ForeColor = Color.FromArgb(30, 30, 30);
        }

        txt.ReadOnly = readOnly;
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

        btn.Font = new Font("Tahoma", 8F, FontStyle.Regular);
        btn.Cursor = Cursors.Hand;
    }

}
