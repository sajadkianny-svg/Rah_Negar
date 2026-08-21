using Rah_Negar.Services.UI;
using System.Text;
using System.Windows.Forms;

namespace Rah_Negar.Services.UI;

/// <summary>
/// سرویس مرکزی نمایش پیام‌های رابط کاربری.
/// تمام پیام‌های برنامه باید از این سرویس عبور کنند
/// تا عنوان، آیکون، راست‌چین بودن، دکمه پیش‌فرض و ساختار متن‌ها یکدست بماند.
/// </summary>
public static class UiMessageService
{
    // ================= Basic Messages =================

    /// <summary>
    /// نمایش پیام موفقیت‌آمیز.
    /// برای عملیات‌هایی مثل ذخیره موفق، ثبت موفق یا تکمیل عملیات استفاده می‌شود.
    /// </summary>
    public static void ShowSuccess(string message, string title = "Success")
    {
        ShowOk(message, title, MessageBoxIcon.Information);
    }

    /// <summary>
    /// نمایش پیام اطلاع‌رسانی عمومی.
    /// برای پیام‌های خنثی و غیرخطا استفاده می‌شود.
    /// </summary>
    public static void ShowInfo(string message, string title = "Information")
    {
        ShowOk(message, title, MessageBoxIcon.Information);
    }

    /// <summary>
    /// نمایش پیام هشدار.
    /// برای ورودی نامعتبر، داده ناقص یا شرایط قابل اصلاح توسط کاربر استفاده می‌شود.
    /// </summary>
    public static void ShowWarning(string message, string title = "Warning")
    {
        ShowOk(message, title, MessageBoxIcon.Warning);
    }

    /// <summary>
    /// نمایش پیام هشدار به همراه جزئیات خطا.
    /// </summary>
    public static void ShowWarning(string message, Exception ex, string title = "Warning")
    {
        ShowOk(BuildExceptionMessage(message, ex), title, MessageBoxIcon.Warning);
    }

    /// <summary>
    /// نمایش پیام خطا.
    /// برای خطاهای جدی، شکست عملیات یا Exceptionهای کنترل‌شده استفاده می‌شود.
    /// </summary>
    public static void ShowError(string message, string title = "Error")
    {
        ShowOk(message, title, MessageBoxIcon.Error);
    }

    /// <summary>
    /// نمایش پیام خطا به همراه جزئیات Exception.
    /// </summary>
    public static void ShowError(string message, Exception ex, string title = "Error")
    {
        ShowOk(BuildExceptionMessage(message, ex), title, MessageBoxIcon.Error);
    }

    /// <summary>
    /// نمایش پیام توقف یا خطر جدی.
    /// برای عملیات‌های بسیار حساس مثل Factory Reset استفاده می‌شود.
    /// </summary>
    public static void ShowStop(string message, string title = "Stop")
    {
        ShowOk(message, title, MessageBoxIcon.Stop);
    }

    /// <summary>
    /// نمایش پیام توقف یا خطر جدی به همراه جزئیات خطا.
    /// </summary>
    public static void ShowStop(string message, Exception ex, string title = "Stop")
    {
        ShowOk(BuildExceptionMessage(message, ex), title, MessageBoxIcon.Stop);
    }

    // ================= Confirmation Messages =================

    /// <summary>
    /// نمایش پیام تأیید عمومی با دکمه‌های Yes / No.
    /// خروجی true یعنی کاربر Yes را انتخاب کرده است.
    /// </summary>
    public static bool Confirm(
        string message,
        string title = "Confirmation")
    {
        return ShowYesNo(
            message,
            title,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
    }

    /// <summary>
    /// نمایش پیام تأیید هشدارآمیز با دکمه‌های Yes / No.
    /// دکمه پیش‌فرض No است.
    /// </summary>
    public static bool ConfirmWarning(
        string message,
        string title = "Warning")
    {
        return ShowYesNo(
            message,
            title,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
    }

    /// <summary>
    /// نمایش پیام تأیید عملیات خطرناک با دکمه‌های Yes / No.
    /// برای حذف اطلاعات، Factory Reset، Import جایگزین‌کننده و عملیات غیرقابل برگشت استفاده می‌شود.
    /// دکمه پیش‌فرض No است.
    /// </summary>
    public static bool ConfirmDanger(
        string message,
        string title = "Danger")
    {
        return ShowYesNo(
            message,
            title,
            MessageBoxIcon.Stop,
            MessageBoxDefaultButton.Button2);
    }

    /// <summary>
    /// نمایش پیام تأیید معمولی با پیش‌فرض Yes.
    /// فقط برای عملیات‌های کم‌ریسک استفاده شود.
    /// </summary>
    public static bool ConfirmSafe(
        string message,
        string title = "Confirmation")
    {
        return ShowYesNo(
            message,
            title,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
    }

    /// <summary>
    /// نمایش پیام تأیید با گزینه‌های OK / Cancel.
    /// خروجی true یعنی کاربر OK را انتخاب کرده است.
    /// </summary>
    public static bool ConfirmOkCancel(
        string message,
        string title = "Confirmation",
        MessageBoxIcon icon = MessageBoxIcon.Question)
    {
        DialogResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OKCancel,
            icon,
            MessageBoxDefaultButton.Button2,
            GetDefaultOptions());

        return result == DialogResult.OK;
    }

    // ================= Special Purpose Messages =================

    /// <summary>
    /// نمایش پیام خطای اعتبارسنجی ورودی.
    /// </summary>
    public static void ShowValidationError(string message)
    {
        ShowWarning(message, "Validation");
    }

    /// <summary>
    /// نمایش پیام داده ناقص.
    /// </summary>
    public static void ShowIncompleteData(string message)
    {
        ShowWarning(message, "Incomplete Data");
    }

    /// <summary>
    /// نمایش پیام عدم دسترسی یا رمز اشتباه.
    /// </summary>
    public static void ShowAccessDenied(string message = "رمز واردشده صحیح نیست")
    {
        ShowWarning(message, "Access Denied");
    }

    /// <summary>
    /// نمایش پیام عملیات غیرمجاز.
    /// </summary>
    public static void ShowOperationNotAllowed(string message)
    {
        ShowWarning(message, "Operation Not Allowed");
    }

    /// <summary>
    /// نمایش پیام موفقیت ذخیره‌سازی.
    /// </summary>
    public static void ShowSaved(string message = "اطلاعات با موفقیت ذخیره شد")
    {
        ShowSuccess(message, "Saved");
    }

    /// <summary>
    /// نمایش پیام موفقیت حذف.
    /// </summary>
    public static void ShowDeleted(string message = "اطلاعات با موفقیت حذف شد")
    {
        ShowSuccess(message, "Deleted");
    }

    /// <summary>
    /// نمایش پیام موفقیت Export.
    /// </summary>
    public static void ShowExported(string message = "خروجی با موفقیت ایجاد شد")
    {
        ShowSuccess(message, "Export");
    }

    /// <summary>
    /// نمایش پیام موفقیت Import.
    /// </summary>
    public static void ShowImported(string message = "بازیابی اطلاعات با موفقیت انجام شد")
    {
        ShowSuccess(message, "Import");
    }

    // ================= Text Builder Helpers =================

    /// <summary>
    /// چند خط متن را با فاصله خطی استاندارد به یک پیام تبدیل می‌کند.
    /// برای ساخت پیام‌های چندخطی خوانا استفاده شود.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// چند پاراگراف را با یک خط خالی بین هر بخش به یک پیام تبدیل می‌کند.
    /// برای پیام‌های طولانی‌تر و هشدارهای مهم استفاده شود.
    /// </summary>
    public static string Paragraphs(params string[] paragraphs)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            paragraphs);
    }

    /// <summary>
    /// پیام دارای عنوان داخلی و جزئیات را می‌سازد.
    /// </summary>
    public static string WithDetails(
        string message,
        string detailsTitle,
        string details)
    {
        return Paragraphs(
            message,
            detailsTitle + Environment.NewLine + details);
    }

    /// <summary>
    /// پیام دارای لیست آیتم‌ها را می‌سازد.
    /// </summary>
    public static string WithList(
        string message,
        IEnumerable<string> items)
    {
        StringBuilder sb = new();

        sb.AppendLine(message);
        sb.AppendLine();

        foreach (string item in items)
            sb.AppendLine("• " + item);

        return sb.ToString().TrimEnd();
    }

    // ================= Internal Core Methods =================

    /// <summary>
    /// نمایش پیام OK محور.
    /// </summary>
    private static void ShowOk(
        string message,
        string title,
        MessageBoxIcon icon)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            icon,
            MessageBoxDefaultButton.Button1,
            GetDefaultOptions());
    }

    /// <summary>
    /// نمایش پیام Yes / No و برگرداندن نتیجه Boolean.
    /// </summary>
    private static bool ShowYesNo(
        string message,
        string title,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton)
    {
        DialogResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButtons.YesNo,
            icon,
            defaultButton,
            GetDefaultOptions());

        return result == DialogResult.Yes;
    }

    /// <summary>
    /// ساخت متن استاندارد خطا به همراه پیام Exception.
    /// </summary>
    private static string BuildExceptionMessage(string message, Exception ex)
    {
        if (ex == null)
            return message;

        return Paragraphs(
            message,
            ex.Message);
    }

    /// <summary>
    /// تنظیمات پیش‌فرض نمایش پیام‌ها.
    /// </summary>
    private static MessageBoxOptions GetDefaultOptions()
    {
        return MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading;
    }
}
