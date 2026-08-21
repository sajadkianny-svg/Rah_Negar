namespace Rah_Negar.Utils;

/// <summary>
/// سرویس مرکزی مدیریت DPI و مقیاس‌بندی UI.
/// تمام فرم‌ها و کنترل‌های سفارشی باید از این سرویس استفاده کنند.
/// </summary>
public static class UiScaleService
{
    private const float BaseDpi = 96f;

    public static float GetScale(Control control)
    {
        if (control == null)
            return 1f;

        float scale = control.DeviceDpi / BaseDpi;

        // برای جلوگیری از بزرگ‌نمایی افراطی در DPIهای خیلی بالا
        return Math.Clamp(scale, 1f, 1.5f);
    }

    public static int Scale(Control control, int value)
    {
        return (int)Math.Round(value * GetScale(control));
    }

    public static Size Scale(Control control, Size size)
    {
        return new Size(
            Scale(control, size.Width),
            Scale(control, size.Height));
    }

    public static Font GetDefaultFont(Control control, float baseSize = 9f)
    {
        return new Font("Segoe UI", baseSize, FontStyle.Regular);
    }

    public static Font GetBoldFont(Control control, float baseSize = 9f)
    {
        return new Font("Segoe UI", baseSize, FontStyle.Bold);
    }
}
