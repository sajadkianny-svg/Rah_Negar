using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rah_Negar.Models;

namespace Rah_Negar.Services;

public static class MissingDaysTextService
{
    public static string BuildMonthMessage(MissingDaysResultModel result, Func<int, string> monthNameResolver)
    {
        if (result.Months.Count != 1)
            throw new InvalidOperationException("این متد فقط برای یک ماه است.");

        int month = result.Months[0];
        string monthName = monthNameResolver(month);

        if (!result.HasMissingDays)
            return $"هیچ روز ناقصی برای ماه {monthName} سال {result.Year} وجود ندارد.";

        List<string> dayNumbers = result.MissingDates
            .Select(x => x.Split('/')[2])
            .ToList();

        return $"{monthName}-{result.Year}:{Environment.NewLine}{string.Join(" , ", dayNumbers)}";
    }

    public static string BuildFullDatesMessage(MissingDaysResultModel result)
    {
        if (!result.HasMissingDays)
            return "هیچ روز ناقصی در بازه انتخاب‌شده وجود ندارد.";

        return string.Join(Environment.NewLine, result.MissingDates);
    }
}