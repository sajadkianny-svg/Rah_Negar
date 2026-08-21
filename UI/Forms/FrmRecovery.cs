using Rah_Negar.Core;
using Rah_Negar.Services;
using Rah_Negar.Services.UI;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Utils;

namespace Rah_Negar.UI.Forms;

public partial class FrmRecovery : BaseForm
{
    /// <summary>
    /// نام ایستگاه فعلی برای ساخت و اعتبارسنجی کد بازیابی
    /// </summary>
    private readonly string _stationName;

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

        ApplyTheme();

        _stationName = stationName;

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
        try
        {
            AppSettingsModel? settings = AppSettingsService.GetSettings();
            string stationName = settings?.StationName ?? "Unknown";
            if (string.IsNullOrWhiteSpace(stationName))
            {
                UiMessageService.ShowError("نام ایستگاه مشخص نیستی", "خطا");
                return;
            }

            string requestId = RecoveryService.CreateRecoveryRequest(stationName);

            txtRequestId.Text = requestId;
            UiMessageService.ShowInfo(
                UiMessageService.Paragraphs(
                    "شناسه بازیابی ایجاد شد",
                     "این کد را به ادمین اعلام کنید"
                    ),
                 "بازیابی رمز عبور");

            txtRecoveryCode.Clear();
            txtRecoveryCode.Focus();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmRecovery.btnGenerate_Click");
            UiMessageService.ShowError("خطا در ایجاد شناسه بازیابی", ex, "خطا");
        }
    }

    /// <summary>
    /// بررسی کد بازیابی واردشده توسط کاربر
    /// </summary>
    private void btnVerify_Click(object sender, EventArgs e)
    {
        AppSettingsModel? settings = AppSettingsService.GetSettings();
        string stationName = settings?.StationName ?? "Unknown";
        try
        {
            string requestId = txtRequestId.Text.Trim();
            string code = txtRecoveryCode.Text.Trim();

            if (string.IsNullOrWhiteSpace(requestId))
            {
                UiMessageService.ShowWarning("ابتدا شناسه بازیابی را تولید کنید", "اعتبارسنجی");
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                UiMessageService.ShowWarning("لطفاً کد بازیابی را وارد کنید", "اعتبارسنجی");
                txtRecoveryCode.Focus();
                return;
            }

            bool isValid = RecoveryService.ValidateRecoveryCode(
                stationName,
                requestId,
                code);

            if (!isValid)
            {
                UiMessageService.ShowWarning("کد بازیابی نامعتبر است", "اعتبارسنجی");
                txtRecoveryCode.SelectAll();
                txtRecoveryCode.Focus();
                return;
            }

            using (FrmChangePassword frm = new FrmChangePassword(ChangePasswordMode.Recovery))
            {
                DialogResult result = frm.ShowDialog();

                if (result == DialogResult.OK)
                {
                    this.DialogResult = DialogResult.OK;
                }
            }

            Close();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "FrmRecovery.btnVerify_Click");
            UiMessageService.ShowError("خطا در بررسی کد بازیابی", ex, "خطا");
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