using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rah_Negar.Core.Reports;
using Rah_Negar.Data;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// تولید PDF رسمی گزارش نهایی ماهانه.
/// اعداد اصلی گزارش فقط از داده‌های نهایی ذخیره‌شده خوانده می‌شوند.
/// </summary>
public static class MonthlyFinalPdfService
{
    /// <summary>
    /// ساخت فایل PDF رسمی گزارش نهایی ماهانه.
    /// </summary>
    public static void GenerateMonthlyFinalPdf(
        int year,
        int month,
        string filePath,
        string stationName)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        ReportResult summary =
            MonthlyFinalReportReadService.LoadMonthlySummarySnapshot(conn, year, month);

        EventReportResult eventSummary =
            MonthlyFinalReportReadService.LoadMonthlyEventSummarySnapshot(conn, year, month);

        Dictionary<string, double> serviceSummary =
            MonthlyFinalReportReadService.LoadServiceDaysSummary(conn, year, month);

        int recycleChangeCount =
            MonthlyFinalReportReadService.LoadRecycleChangeCount(conn, year, month);

        IReadOnlyList<ReportParameterDefinition> parameters =
            ReportParameterRegistry.GetParameters(stationName);

        List<EventDatePdfRow> eventDates =
            LoadEventDates(conn, year, month);

        const float rowHeight = 12f;

        int actualRows = CountValidOperationalParams(parameters);
        int standardRowCount = GetStandardOperationalRowCount();
        int missingRows = Math.Max(0, standardRowCount - actualRows);
        float spacerHeight = missingRows * rowHeight;

        string reportId = $"MFR-{year}-{month:00}-{DateTime.Now:HHmm}";

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(60);

                page.DefaultTextStyle(x => x
                    .FontSize(7)
                    .FontFamily("Arial")
                    .FontColor(Colors.Grey.Darken4));

                page.Header().Element(e =>
                    BuildHeader(e, stationName, year, month, reportId));

                page.Content().Column(col =>
                {
                    col.Spacing(7);

                    col.Item().Row(row =>
                    {
                        row.Spacing(10);

                        row.RelativeItem(1.3f).Column(left =>
                        {
                            left.Spacing(6);

                            left.Item().Element(e =>
                                BuildOperationalSummaryTable(e, summary, parameters));

                            if (spacerHeight > 0)
                                left.Item().Height(spacerHeight);

                            left.Item().Element(e =>
                                BuildUnitEventSummaryTable(e, eventSummary));
                        });

                        row.RelativeItem(1f).Column(right =>
                        {
                            right.Spacing(6);

                            right.Item().Element(e =>
                                BuildFuelFlowTable(e, summary, recycleChangeCount));

                            right.Item().Element(e =>
                                BuildServiceDaysTable(e, serviceSummary));
                        });
                    });

                    col.Item().Element(e =>
                        BuildEventDatesTable(e, eventDates));
                });

                page.Footer().Element(BuildFooter);
            });
        })
        .GeneratePdf(filePath);
    }

    /// <summary>
    /// ساخت سربرگ رسمی و مدیریتی گزارش.
    /// </summary>
    private static void BuildHeader(
        IContainer container,
        string stationName,
        int year,
        int month,
        string reportId)

    {
        string officialStation = GetOfficialStationTitle(stationName);

        container.Column(col =>
        {
            col.Item()
                .Border(0.8f)
                .PaddingVertical(7)
                .PaddingHorizontal(10)
                .Column(inner =>
                {
                    inner.Item().AlignCenter().Text("CONFIDENTIAL")
                        .FontSize(8)
                        .Bold()
                        .FontColor(Colors.Red.Darken2);

                    inner.Item().AlignCenter().Text("MONTHLY FINAL OPERATION REPORT")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Colors.Grey.Darken4);

                    inner.Item().AlignCenter().Text(officialStation)
                        .FontSize(8)
                        .SemiBold()
                        .FontColor(Colors.Grey.Darken2);

                    inner.Item().Row(row =>
                    {
                        // 👈 گوشه چپ
                        row.RelativeItem().AlignLeft().Column(left =>
                        {
                            left.Spacing(1);

                            left.Item().Text($"Report ID: {reportId}")
                                .FontSize(6.3f)
                                .SemiBold();

                            left.Item().Text($"Period: {year}/{month:00}")
                                .FontSize(6.3f)
                                .SemiBold();
                        });

                        // 👈 فضای خالی سمت راست برای بالانس
                        row.RelativeItem();
                    });
                });

            col.Item().PaddingBottom(7);
        });
    }

    /// <summary>
    /// ساخت جدول خلاصه عملیاتی شامل Min / Max / Avg پارامترهای اصلی.
    /// RPM و Status در این جدول نمایش داده نمی‌شوند.
    /// </summary>
    private static void BuildOperationalSummaryTable(
        IContainer container,
        ReportResult result,
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        List<ReportParameterDefinition> dataParameters = parameters
            .Where(p => p.DataColumnName != null)
            .Where(p => p.Category != ReportParameterCategory.RPM)
            .Where(p => p.Category != ReportParameterCategory.Status)
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Min))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Max))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Avg))
            .ToList();

        container.Column(col =>
        {
            col.Item().Element(e => BuildSectionTitle(e, PdfSectionTitles.Operational));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.7f);
                });

                AddHeaderCell(table, "Parameter");
                AddHeaderCell(table, "Min");
                AddHeaderCell(table, "Max");
                AddHeaderCell(table, "Avg");

                foreach (ReportParameterDefinition parameter in dataParameters)
                {
                    List<ReportSummaryItem> items = result.SummaryItems
                        .Where(x => x.ParameterKey == parameter.Key)
                        .ToList();

                    AddBodyCell(table, parameter.DisplayName);
                    AddBodyCell(table, GetSummaryValue(items, ReportAggregationType.Min));
                    AddBodyCell(table, GetSummaryValue(items, ReportAggregationType.Max));
                    AddBodyCell(table, GetSummaryValue(items, ReportAggregationType.Avg));
                }
            });
        });
    }

    /// <summary>
    /// ساخت جدول سوخت، فلو، و تعداد تغییرات Recycle.
    /// </summary>
    private static void BuildFuelFlowTable(
        IContainer container,
        ReportResult result,
        int recycleChangeCount)
    {
        double gasGeneratorFuel = GetSummaryValueNumber(result, "ir_f", ReportAggregationType.Sum);
        double turbineFuel = GetSummaryValueNumber(result, "turbine_fuel", ReportAggregationType.Sum);
        double turbineFlow = GetSummaryValueNumber(result, "turbine_flow", ReportAggregationType.Sum);
        double nonTurbineFlow = GetSummaryValueNumber(result, "non_turbine_flow", ReportAggregationType.Sum);
        double vent = GetSummaryValueNumber(result, "vent", ReportAggregationType.Sum);

        container.Column(col =>
        {
            col.Item().Element(e => BuildSectionTitle(e, PdfSectionTitles.FuelFlow));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn(0.8f);
                });

                AddHeaderCell(table, "Item");
                AddHeaderCell(table, "Value");

                AddBodyCell(table, "Gas Generator Fuel");
                AddBodyCell(table, gasGeneratorFuel.ToString("F1"));

                AddBodyCell(table, "Turbine Fuel");
                AddBodyCell(table, turbineFuel.ToString("F1"));

                AddBodyCell(table, "Total Fuel");
                AddBodyCell(table, (gasGeneratorFuel + turbineFuel).ToString("F1"));

                AddBodyCell(table, "Turbine Flow");
                AddBodyCell(table, turbineFlow.ToString("F1"));

                AddBodyCell(table, "Non-Turbine Flow");
                AddBodyCell(table, nonTurbineFlow.ToString("F1"));

                AddBodyCell(table, "Total Flow");
                AddBodyCell(table, (turbineFlow + nonTurbineFlow).ToString("F1"));

                AddBodyCell(table, "Vent");
                AddBodyCell(table, vent.ToString("F1"));

                AddBodyCell(table, "Recycle Change Count");
                AddBodyCell(table, recycleChangeCount.ToString());
            });
        });
    }

    /// <summary>
    /// ساخت جدول خلاصه رویدادها و Runtime واحدها.
    /// </summary>
    private static void BuildUnitEventSummaryTable(
        IContainer container,
        EventReportResult result)
    {
        container.Column(col =>
        {
            col.Item().Element(e => BuildSectionTitle(e, PdfSectionTitles.UnitEvent));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.6f);
                    columns.RelativeColumn(0.6f);
                    columns.RelativeColumn(0.6f);
                    columns.RelativeColumn(0.9f);
                });

                AddHeaderCell(table, "Unit");
                AddHeaderCell(table, "Runtime");
                AddHeaderCell(table, "After OH");
                AddHeaderCell(table, "Start");
                AddHeaderCell(table, "NSD");
                AddHeaderCell(table, "ESD");
                AddHeaderCell(table, "Longest");

                foreach (UnitEventSummary item in result.UnitSummaries.OrderBy(x => x.Unit))
                {
                    AddBodyCell(table, item.Unit);
                    AddBodyCell(table, item.RuntimeHours.ToString("F1"));
                    AddBodyCell(table, item.RuntimeAfterOH.ToString("F1"));
                    AddBodyCell(table, item.StartCount.ToString());
                    AddBodyCell(table, item.NSDCount.ToString());
                    AddBodyCell(table, item.ESDCount.ToString());
                    AddBodyCell(table, item.LongestRunHours.ToString("F1"));
                }
            });
        });
    }

    /// <summary>
    /// ساخت جدول روزهای سرویس واحدها و ترکیب تعداد واحدهای فعال.
    /// </summary>
    private static void BuildServiceDaysTable(
        IContainer container,
        Dictionary<string, double> map)
    {
        container.Column(col =>
        {
            col.Item().Element(e => BuildSectionTitle(e, PdfSectionTitles.ServiceDays));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(0.6f);
                });

                AddHeaderCell(table, "Item");
                AddHeaderCell(table, "Days");

                foreach (KeyValuePair<string, double> item in map
                    .Where(x => x.Key.StartsWith("unit_service_days_") ||
                                x.Key.StartsWith("combination_"))
                    .OrderBy(x => x.Key))
                {
                    AddBodyCell(table, FormatServiceKey(item.Key));
                    AddBodyCell(table, item.Value.ToString("F0"));
                }
            });
        });
    }

    /// <summary>
    /// ساخت جدول کامل تاریخ‌های Start / NSD / ESD.
    /// اگر تعداد رویدادها زیاد باشد، جدول به صفحه بعد ادامه پیدا می‌کند.
    /// </summary>
    private static void BuildEventDatesTable(
        IContainer container,
        List<EventDatePdfRow> rows)
    {
        container.Column(col =>
        {
            col.Item().Element(e => BuildSectionTitle(e, PdfSectionTitles.EventDates));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                });

                table.Header(header =>
                {
                    AddRepeatedHeaderCell(header, "START");
                    AddRepeatedHeaderCell(header, "NSD");
                    AddRepeatedHeaderCell(header, "ESD");
                });

                foreach (EventDatePdfRow unitRow in rows.OrderBy(x => x.Unit))
                {
                    table.Cell()
                        .ColumnSpan(3)
                        .Border(0.5f)
                        .Background(Colors.Grey.Lighten4)
                        .PaddingVertical(2)
                        .AlignCenter()
                        .Text(unitRow.Unit)
                        .FontSize(7)
                        .Bold();

                    int maxCount = Math.Max(
                        unitRow.StartDates.Count,
                        Math.Max(unitRow.NsdDates.Count, unitRow.EsdDates.Count));

                    int rowCount = (int)Math.Ceiling(maxCount / 3.0);

                    for (int i = 0; i < rowCount; i++)
                    {
                        AddEventDateCell(table, unitRow.StartDates.Skip(i * 3).Take(3).ToList());
                        AddEventDateCell(table, unitRow.NsdDates.Skip(i * 3).Take(3).ToList());
                        AddEventDateCell(table, unitRow.EsdDates.Skip(i * 3).Take(3).ToList());
                    }
                }
            });
        });
    }

    /// <summary>
    /// خواندن تاریخ‌های Start / NSD / ESD از جدول رویدادها برای ماه انتخاب‌شده.
    /// </summary>
    private static List<EventDatePdfRow> LoadEventDates(
        SqliteConnection conn,
        int year,
        int month)
    {
        int daysInMonth = new System.Globalization.PersianCalendar()
            .GetDaysInMonth(year, month);

        long fromDate = year * 10000L + month * 100L + 1;
        long toDate = year * 10000L + month * 100L + daysInMonth;

        const string sql = @"
SELECT unit, event_type, date_rep, event_time
FROM tbl_events
WHERE date_rep BETWEEN @fromDate AND @toDate
  AND UPPER(event_type) IN ('START', 'NSD', 'ESD')
ORDER BY unit, date_rep, event_time;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@fromDate", fromDate);
        cmd.Parameters.AddWithValue("@toDate", toDate);

        using SqliteDataReader reader = cmd.ExecuteReader();

        Dictionary<string, List<string>> startMap = [];
        Dictionary<string, List<string>> nsdMap = [];
        Dictionary<string, List<string>> esdMap = [];

        while (reader.Read())
        {
            string unit = reader["unit"].ToString() ?? "";
            string eventType = reader["event_type"].ToString()?.ToUpperInvariant() ?? "";
            long dateRep = Convert.ToInt64(reader["date_rep"]);
            string time = reader["event_time"].ToString() ?? "";

            string text = $"{FormatDateShort(dateRep)} {time}";

            Dictionary<string, List<string>> target = eventType switch
            {
                "START" => startMap,
                "NSD" => nsdMap,
                "ESD" => esdMap,
                _ => startMap
            };

            if (!target.TryGetValue(unit, out List<string>? list))
            {
                list = [];
                target[unit] = list;
            }

            list.Add(text);
        }

        List<string> units = startMap.Keys
            .Union(nsdMap.Keys)
            .Union(esdMap.Keys)
            .OrderBy(x => x)
            .ToList();

        return units.Select(unit => new EventDatePdfRow
        {
            Unit = unit,
            StartDates = startMap.TryGetValue(unit, out List<string>? starts) ? starts : [],
            NsdDates = nsdMap.TryGetValue(unit, out List<string>? nsds) ? nsds : [],
            EsdDates = esdMap.TryGetValue(unit, out List<string>? esds) ? esds : []
        }).ToList();
    }

    /// <summary>
    /// ساخت عنوان رسمی هر بخش در گزارش.
    /// </summary>
    private static void BuildSectionTitle(IContainer container, string title)
    {
        container
            .PaddingBottom(1) // 👈 چسبوندن به جدول
            .Background(Colors.Grey.Lighten3) // 👈 خیلی subtle
            .PaddingVertical(1.2f)
            .AlignCenter()
            .Text(title.ToUpperInvariant())
            .FontSize(6)
            .SemiBold()
            .FontColor(Colors.Grey.Darken3);
    }

    /// <summary>
    /// افزودن سلول عنوان جدول.
    /// </summary>
    private static void AddHeaderCell(TableDescriptor table, string text)
    {
        table.Cell()
            .Border(0.5f)
            .Background(Colors.Grey.Darken2)
            .PaddingVertical(2.5f)
            .PaddingHorizontal(3)
            .AlignCenter()
            .AlignMiddle()
            .Text(text.ToUpperInvariant())
            .FontSize(6.5f)
            .Bold()
            .FontColor(Colors.White);
    }

    /// <summary>
    /// افزودن سلول عنوان تکرارشونده جدول.
    /// </summary>
    private static void AddRepeatedHeaderCell(
        TableCellDescriptor header,
        string text)
    {
        header.Cell()
            .Border(0.5f)
            .Background(Colors.Grey.Darken2)
            .PaddingVertical(2.5f)
            .PaddingHorizontal(3)
            .AlignCenter()
            .AlignMiddle()
            .Text(text.ToUpperInvariant())
            .FontSize(6.5f)
            .Bold()
            .FontColor(Colors.White);
    }

    /// <summary>
    /// افزودن سلول عادی جدول.
    /// </summary>
    private static void AddBodyCell(TableDescriptor table, string text)
    {
        table.Cell()
            .Border(0.4f)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(2.2f)
            .PaddingHorizontal(3)
            .AlignCenter()
            .AlignMiddle()
            .Text(CleanPdfText(text))
            .FontSize(7)
            .FontColor(Colors.Grey.Darken4);
    }

    /// <summary>
    /// افزودن سلول مخصوص تاریخ‌های رویداد.
    /// هر مقدار date-time در یک خط جداگانه نمایش داده می‌شود.
    /// </summary>
    private static void AddEventDateCell(
        TableDescriptor table,
        List<string> values)
    {
        table.Cell()
            .Border(0.5f)
            .PaddingVertical(3)
            .PaddingHorizontal(4)
            .AlignCenter()
            .AlignMiddle()
            .Column(col =>
            {
                col.Spacing(2);

                if (values.Count == 0)
                {
                    col.Item().AlignCenter().Text("-").FontSize(7);
                    return;
                }

                foreach (string value in values)
                {
                    col.Item().AlignCenter().Text(t =>
                    {
                        string cleanValue = CleanPdfText(value);
                        string[] parts = cleanValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length >= 2)
                        {
                            t.Span(parts[0]).FontSize(7);
                            t.Span("    ").FontSize(7);
                            t.Span(parts[1]).FontSize(7);
                        }
                        else
                        {
                            t.Span(cleanValue).FontSize(7);
                        }
                    });
                }
            });
    }

    /// <summary>
    /// ساخت Footer رسمی سند.
    /// </summary>
    private static void BuildFooter(IContainer container)
    {
        container
            .BorderTop(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingTop(4)
            .AlignCenter()
            .Text(x =>
            {
                x.Span("Rah Negar System  |  Confidential Final Report  |  Page ")
                    .FontSize(6)
                    .FontColor(Colors.Grey.Darken2);

                x.CurrentPageNumber()
                    .FontSize(6)
                    .FontColor(Colors.Grey.Darken2);
            });
    }

    /// <summary>
    /// گرفتن مقدار متنی یک نوع محاسبه از لیست Summary.
    /// </summary>
    private static string GetSummaryValue(
        IEnumerable<ReportSummaryItem> items,
        ReportAggregationType type)
    {
        ReportSummaryItem? item = items.FirstOrDefault(x => x.AggregationType == type);

        return item?.Value?.ToString("F1") ?? "-";
    }

    /// <summary>
    /// گرفتن مقدار عددی از Summary.
    /// </summary>
    private static double GetSummaryValueNumber(
        ReportResult result,
        string key,
        ReportAggregationType type)
    {
        return result.SummaryItems
            .FirstOrDefault(x => x.ParameterKey == key &&
                                 x.AggregationType == type)
            ?.Value ?? 0;
    }

    /// <summary>
    /// شمارش پارامترهای معتبر Operational Summary.
    /// </summary>
    private static int CountValidOperationalParams(
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        return parameters
            .Where(p => p.DataColumnName != null)
            .Where(p => p.Category != ReportParameterCategory.RPM)
            .Where(p => p.Category != ReportParameterCategory.Status)
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Min))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Max))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Avg))
            .Count();
    }

    /// <summary>
    /// بیشترین تعداد سطر Operational Summary بین پروفایل‌های فعلی.
    /// برای ثابت نگه داشتن چیدمان بین ایستگاه‌ها استفاده می‌شود.
    /// </summary>
    private static int GetStandardOperationalRowCount()
    {
        IReadOnlyList<ReportParameterDefinition> rasht =
            ReportParameterRegistry.GetParameters("Rasht Station");

        IReadOnlyList<ReportParameterDefinition> ramsar =
            ReportParameterRegistry.GetParameters("Ramsar Station");

        return Math.Max(
            CountValidOperationalParams(rasht),
            CountValidOperationalParams(ramsar));
    }

    /// <summary>
    /// کوتاه‌سازی نام کلیدهای Service Summary برای نمایش در PDF.
    /// </summary>
    private static string FormatServiceKey(string key)
    {
        if (key.StartsWith("unit_service_days_"))
            return key.Replace("unit_service_days_", "Service Days ");

        if (key.StartsWith("combination_"))
            return key.Replace("combination_", "")
                .Replace("_units_days", " Unit(s) Active");

        return key;
    }

    /// <summary>
    /// تبدیل تاریخ عددی به فرمت کوتاه.
    /// </summary>
    private static string FormatDateShort(long dateRep)
    {
        string value = dateRep.ToString();

        if (value.Length != 8)
            return value;

        return $"{value[..4]}/{value.Substring(4, 2)}/{value.Substring(6, 2)}";
    }


    /// <summary>
    /// تبدیل نام ایستگاه به عنوان رسمی انگلیسی برای گزارش.
    /// </summary>
    private static string GetOfficialStationTitle(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => "RASHT GAS COMPRESSION STATION",
            "Ramsar Station" => "RAMSAR GAS COMPRESSION STATION",
            _ => stationName.ToUpperInvariant()
        };
    }

    /// <summary>
    /// حذف کاراکترهای نامعتبر برای جلوگیری از خطای Glyph در QuestPDF.
    /// </summary>
    private static string CleanPdfText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }

    /// <summary>
    /// مدل داخلی برای نمایش لیست کامل تاریخ‌های رویدادها در PDF.
    /// </summary>
    private sealed class EventDatePdfRow
    {
        public string Unit { get; init; } = "";
        public List<string> StartDates { get; init; } = [];
        public List<string> NsdDates { get; init; } = [];
        public List<string> EsdDates { get; init; } = [];
    }

    public static class PdfSectionTitles
    {
        public const string Operational = "OPERATIONAL SUMMARY";
        public const string UnitEvent = "UNIT PERFORMANCE";
        public const string ServiceDays = "SERVICE DISTRIBUTION";
        public const string FuelFlow = "FUEL & FLOW ANALYSIS";
        public const string EventDates = "EVENT TIMELINE";
    }
}