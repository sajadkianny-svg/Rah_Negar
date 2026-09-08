using Rah_Negar.Core;
using Rah_Negar.Properties;
using Rah_Negar.Utils;
using Rah_Negar.Services.UI;

namespace Rah_Negar.UI.Forms.Base;

public class BaseForm : Form
{
    public const int MaximumSupportedDpi = 144;

    public BaseForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        if (!DesignMode)
            Icon = Resources.AppIcon;
    }

    protected override void OnLoad(EventArgs e)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        base.OnLoad(e);

        if (DeviceDpi > MaximumSupportedDpi)
        {
            UiMessageService.ShowMessageBox(this,
                "این مقیاس نمایش پشتیبانی نمی‌شود. لطفاً مقیاس ویندوز را حداکثر روی ۱۵۰٪ تنظیم کنید.",
                "مقیاس نمایش پشتیبانی نمی‌شود", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            Close();
            return;
        }

        UiStyleService.ApplyFormConventions(this, AppThemeManager.CurrentPalette);
    }

}
