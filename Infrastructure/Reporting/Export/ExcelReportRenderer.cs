using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Rah_Negar.Foundation.Application.Reporting.Export;

namespace Rah_Negar.Infrastructure.Reporting.Export;

public sealed class ExcelReportRenderer : IExcelReportRenderer
{
    private static readonly DateTimeOffset ZipTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly IReportFileNamePolicy _fileNames;

    public ExcelReportRenderer(IReportFileNamePolicy fileNames) =>
        _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));

    public Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "Finalized Report";
        workbook.Properties.Subject = model.SnapshotId;
        workbook.Properties.Author = model.GenerationMetadata.RequestedBy;
        workbook.Properties.Created = model.GenerationMetadata.GeneratedAt.UtcDateTime;
        workbook.Properties.Modified = model.GenerationMetadata.GeneratedAt.UtcDateTime;
        AddSummary(workbook, model);
        AddRuntime(workbook, model);
        AddEvents(workbook, model);
        AddDaily(workbook, model);
        AddEvidence(workbook, model);

        using var raw = new MemoryStream();
        workbook.SaveAs(raw, validate: true, evaluateFormulae: false);
        byte[] content = NormalizePackage(raw.ToArray(), cancellationToken);
        return Task.FromResult(new RenderedReport(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx", content,
            _fileNames.Create(model, ReportExportFormat.Excel)));
    }

    private static void AddSummary(XLWorkbook workbook, FinalizedReportExportModel model)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Summary");
        Header(sheet, "Finalized Report Summary", model);
        WriteRow(sheet, 5, "Parameter", "Label", "Aggregation", "Value", "Unit", "Count");
        int row = 6;
        foreach (var item in model.OperationalSummaries)
            WriteRow(sheet, row++, item.ParameterId, item.Label, item.Aggregation.ToString(), item.Value,
                item.Unit, item.ContributingCount);
        Finish(sheet);
    }

    private static void AddRuntime(XLWorkbook workbook, FinalizedReportExportModel model)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Runtime");
        WriteRow(sheet, 1, "Unit", "PhysicalMinutes", "EsdAdjustmentMinutes", "AdjustedMinutes",
            "AfterOhMinutes", "LongestRunMinutes", "ServiceDays", "FinalState");
        int row = 2;
        foreach (var item in model.RuntimeSummaries)
            WriteRow(sheet, row++, item.UnitId, item.PhysicalRuntimeMinutes, item.EsdAdjustmentMinutes,
                item.AdjustedRuntimeMinutes, item.RuntimeAfterOhMinutes, item.LongestRunMinutes,
                item.ServiceDayCount, item.FinalState.ToString());
        Finish(sheet);
    }

    private static void AddEvents(XLWorkbook workbook, FinalizedReportExportModel model)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Events");
        WriteRow(sheet, 1, "EventId", "Unit", "Type", "EventMinute", "SourceOrdinal");
        int row = 2;
        foreach (var item in model.EventLog)
            WriteRow(sheet, row++, item.EventId, item.UnitId, item.EventType.ToString(),
                item.EventMinute, item.SourceOrdinal);
        Finish(sheet);
    }

    private static void AddDaily(XLWorkbook workbook, FinalizedReportExportModel model)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Daily");
        WriteRow(sheet, 1, "Parameter", "Label", "Sum", "Unit", "Count", "MissingDates");
        int row = 2;
        foreach (var item in model.DailySummaries)
            WriteRow(sheet, row++, item.ParameterId, item.Label, item.Sum, item.Unit,
                item.ContributingCount, string.Join(",", item.MissingDates.OrderBy(x => x, StringComparer.Ordinal)));
        Finish(sheet);
    }

    private static void AddEvidence(XLWorkbook workbook, FinalizedReportExportModel model)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Evidence");
        string[,] values =
        {
            { "SnapshotId", model.SnapshotId },
            { "ReportId", model.ReportId },
            { "ReportVersion", model.ReportVersion },
            { "SchemaVersion", model.SchemaVersion },
            { "IntegrityVersion", model.IntegrityVersion },
            { "Checksum", model.Checksum },
            { "GeneratorVersion", model.GenerationMetadata.GeneratorVersion },
            { "GeneratedAt", model.GenerationMetadata.GeneratedAt.ToString("O", CultureInfo.InvariantCulture) },
            { "RequestedBy", model.GenerationMetadata.RequestedBy }
        };
        for (int row = 0; row < values.GetLength(0); row++)
            WriteRow(sheet, row + 1, values[row, 0], values[row, 1]);
        Finish(sheet);
    }

    private static void Header(IXLWorksheet sheet, string title, FinalizedReportExportModel model)
    {
        WriteRow(sheet, 1, title);
        WriteRow(sheet, 2, "Station", model.StationName, "Period", model.PersianPeriodLabel);
        WriteRow(sheet, 3, "SnapshotId", model.SnapshotId, "SchemaVersion", model.SchemaVersion);
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params object[] values)
    {
        for (int index = 0; index < values.Length; index++)
            sheet.Cell(row, index + 1).Value = XLCellValue.FromObject(values[index], CultureInfo.InvariantCulture);
    }

    private static void Finish(IXLWorksheet sheet)
    {
        if (sheet.LastCellUsed() is null) return;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.ColumnsUsed().AdjustToContents();
        sheet.SheetView.FreezeRows(1);
    }

    private static byte[] NormalizePackage(byte[] source, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(source, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
        using var output = new MemoryStream();
        using (var normalized = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in archive.Entries.OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string targetName = CanonicalEntryName(entry.FullName);
                ZipArchiveEntry target = normalized.CreateEntry(targetName, CompressionLevel.Optimal);
                target.LastWriteTime = ZipTimestamp;
                using Stream from = entry.Open();
                using Stream to = target.Open();
                if (entry.FullName == "_rels/.rels")
                {
                    using var reader = new StreamReader(from, Encoding.UTF8, true, leaveOpen: true);
                    string relationships = reader.ReadToEnd();
                    relationships = Regex.Replace(relationships,
                        "package/services/metadata/core-properties/[0-9a-f]+\\.psmdcp",
                        "package/services/metadata/core-properties/core.psmdcp",
                        RegexOptions.CultureInvariant);
                    int relationshipOrdinal = 0;
                    relationships = Regex.Replace(relationships, "Id=\"R[0-9a-f]+\"",
                        _ => $"Id=\"rDeterministic{++relationshipOrdinal}\"",
                        RegexOptions.CultureInvariant);
                    byte[] bytes = Encoding.UTF8.GetBytes(relationships);
                    to.Write(bytes);
                }
                else from.CopyTo(to);
            }
        }
        return output.ToArray();
    }

    private static string CanonicalEntryName(string name) =>
        Regex.IsMatch(name, "^package/services/metadata/core-properties/[0-9a-f]+\\.psmdcp$",
            RegexOptions.CultureInvariant)
            ? "package/services/metadata/core-properties/core.psmdcp"
            : name;
}
