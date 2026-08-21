using Rah_Negar.Services;
using Rah_Negar.Utils;

namespace Rah_Negar.Services.Records;

/// <summary>
/// نتیجه اعتبارسنجی ترتیب ثبت یا ویرایش داده‌های روزانه
/// </summary>
public sealed class DailySaveSequenceResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public static DailySaveSequenceResult Success()
    {
        return new DailySaveSequenceResult
        {
            IsValid = true
        };
    }

    public static DailySaveSequenceResult Fail(string message)
    {
        return new DailySaveSequenceResult
        {
            IsValid = false,
            Message = message
        };
    }
}

/// <summary>
/// کنترل‌کننده ترتیب ثبت داده‌های روزانه
/// این سرویس اجازه نمی‌دهد ثبت داده‌ها با پرش تاریخی یا جایگزینی اشتباه انجام شود
/// </summary>
public static class DailySaveSequenceService
{
    /// <summary>
    /// اعتبارسنجی ثبت جدید
    /// ثبت جدید فقط برای تاریخ مجاز بعدی انجام می‌شود
    /// </summary>
    public static DailySaveSequenceResult ValidateNewSave(long selectedDate)
    {
        if (CommonRecordQueryService.ExistsForDate(selectedDate))
        {
            return DailySaveSequenceResult.Fail(
                "برای تاریخ انتخاب‌شده قبلاً اطلاعات ثبت شده است" +
                Environment.NewLine +
                "تاریخ انتخاب‌شده" +
                Environment.NewLine +
                DateFormatHelper.FormatDateRep(selectedDate) +
                Environment.NewLine +
                "برای تغییر اطلاعات باید ابتدا داده همان تاریخ را بارگذاری کرده و وارد حالت ویرایش شوید");
        }

        long dataStartDate = AppSettingsService.GetDataStartDate();

        if (dataStartDate <= 0)
        {
            return DailySaveSequenceResult.Fail(
                "تاریخ مبنای شروع داده‌ها معتبر نیست");
        }

        long? lastSavedDate = CommonRecordQueryService.GetLastSavedDate();

        if (lastSavedDate == null)
        {
            if (selectedDate != dataStartDate)
            {
                return DailySaveSequenceResult.Fail(
                    "اولین ثبت داده باید از تاریخ مبنای شروع داده‌ها انجام شود" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "تاریخ انتخاب‌شده" +
                    Environment.NewLine +
                    DateFormatHelper.FormatDateRep(selectedDate) +
                    Environment.NewLine +
                    Environment.NewLine +
                    "تاریخ مجاز" +
                    Environment.NewLine +
                    DateFormatHelper.FormatDateRep(dataStartDate) +
                    Environment.NewLine +
                    Environment.NewLine +
                    "اگر تاریخ مبنای شروع اشتباه تنظیم شده است، قبل از اولین ثبت داده می‌توانید آن را از تنظیمات اصلاح کنید");
            }

            return DailySaveSequenceResult.Success();
        }

        long expectedDate = PersianDateHelper.AddDays(lastSavedDate.Value, 1);

        if (selectedDate != expectedDate)
        {
            return DailySaveSequenceResult.Fail(
                "ثبت داده‌ها باید به ترتیب تاریخ انجام شود" +
                Environment.NewLine +
                Environment.NewLine +
                "تاریخ انتخاب‌شده" +
                Environment.NewLine +
                DateFormatHelper.FormatDateRep(selectedDate) +
                Environment.NewLine +
                Environment.NewLine +
                "تاریخ مجاز" +
                Environment.NewLine +
                DateFormatHelper.FormatDateRep(expectedDate));
        }

        return DailySaveSequenceResult.Success();
    }

    /// <summary>
    /// اعتبارسنجی ویرایش
    /// ویرایش فقط برای تاریخی مجاز است که قبلاً ثبت شده باشد
    /// </summary>
    public static DailySaveSequenceResult ValidateEdit(long selectedDate)
    {
        if (!CommonRecordQueryService.ExistsForDate(selectedDate))
        {
            return DailySaveSequenceResult.Fail(
                "برای این تاریخ داده‌ای ثبت نشده است" +
                Environment.NewLine +
                "امکان ویرایش وجود ندارد");
        }

        return DailySaveSequenceResult.Success();
    }

    }
