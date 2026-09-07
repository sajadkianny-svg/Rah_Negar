namespace Rah_Negar.Services.Reports;

/// <summary>
/// Formats the service-unit combination summary for the operator report surface.
/// Keeping this calculation outside the form makes empty and tie cases directly testable.
/// </summary>
public static class ServiceCombinationSummaryService
{
    public static string Format(IReadOnlyDictionary<string, int> combinationFrequency)
    {
        ArgumentNullException.ThrowIfNull(combinationFrequency);

        if (combinationFrequency.Count == 0)
            return "پرتکرارترین ترکیب واحدها: داده‌ای ثبت نشده است";

        int maxCount = combinationFrequency.Values.Max();
        string combinations = string.Join(
            " | ",
            combinationFrequency
                .Where(pair => pair.Value == maxCount)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key));

        string dayLabel = maxCount == 1 ? "روز" : "روز";
        return $"پرتکرارترین ترکیب واحدها: {combinations} ({maxCount} {dayLabel})";
    }
}
