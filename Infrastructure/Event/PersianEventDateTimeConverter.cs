using System.Globalization;
using Rah_Negar.Foundation.Application.Event.Policies;

namespace Rah_Negar.Infrastructure.Event;

public sealed class PersianEventDateTimeConverter : IEventDateTimeConverter
{
    public long ToChronologicalMinute(int persianDate, int minuteOfDay)
    {
        if (minuteOfDay is < 0 or > 1439)
            throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
        int year = persianDate / 10000;
        int month = persianDate / 100 % 100;
        int day = persianDate % 100;
        DateTime gregorian = new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
        return gregorian.Ticks / TimeSpan.TicksPerMinute + minuteOfDay;
    }
}
