using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rah_Negar.Foundation.Application.Reporting.Export;

namespace Rah_Negar.Infrastructure.Reporting.Export;

public sealed class PdfReportRenderer : IPdfReportRenderer
{
    private readonly IReportFileNamePolicy _fileNames;

    public PdfReportRenderer(IReportFileNamePolicy fileNames)
    {
        _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] content = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));
            page.Header().Column(header =>
            {
                header.Item().Text("Finalized Report").Bold().FontSize(18);
                header.Item().Text($"{model.StationName} | {model.PersianPeriodLabel} | {model.PeriodKind}");
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                Section(column, "Summary", table =>
                {
                    Row(table, "Parameter", "Value", "Unit");
                    foreach (var item in model.OperationalSummaries)
                        Row(table, item.Label, Decimal(item.Value), item.Unit);
                });
                Section(column, "Runtime", table =>
                {
                    Row(table, "Unit", "Physical minutes", "Adjusted minutes");
                    foreach (var item in model.RuntimeSummaries)
                        Row(table, item.UnitId, item.PhysicalRuntimeMinutes.ToString(CultureInfo.InvariantCulture),
                            item.AdjustedRuntimeMinutes.ToString(CultureInfo.InvariantCulture));
                });
                Section(column, "Events", table =>
                {
                    Row(table, "Unit", "Start", "NSD / ESD / OH");
                    foreach (var item in model.EventSummaries)
                        Row(table, item.UnitId, item.StartCount.ToString(CultureInfo.InvariantCulture),
                            $"{item.NsdCount} / {item.EsdCount} / {item.OhCount}");
                });
                Section(column, "Evidence", table =>
                {
                    Row(table, "SnapshotId", model.SnapshotId, "");
                    Row(table, "Report version", model.ReportVersion, "");
                    Row(table, "Schema version", model.SchemaVersion, "");
                    Row(table, "Integrity version", model.IntegrityVersion, "");
                    Row(table, "Checksum", model.Checksum, "");
                    Row(table, "Generator", model.GenerationMetadata.GeneratorVersion, "");
                    Row(table, "Generated at", model.GenerationMetadata.GeneratedAt.ToString("O", CultureInfo.InvariantCulture), "");
                    Row(table, "Requested by", model.GenerationMetadata.RequestedBy, "");
                });
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Snapshot ");
                text.Span(model.SnapshotId);
                text.Span(" | Page ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RenderedReport("application/pdf", ".pdf", content,
            _fileNames.Create(model, ReportExportFormat.Pdf)));
    }

    private static void Section(ColumnDescriptor column, string title,
        Action<TableDescriptor> content)
    {
        column.Item().PaddingTop(8).Text(title).Bold().FontSize(12);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });
            content(table);
        });
    }

    private static void Row(TableDescriptor table, string first, string second, string third)
    {
        Cell(table, first);
        Cell(table, second);
        Cell(table, third);
    }

    private static void Cell(TableDescriptor table, string value) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value ?? "");

    private static string Decimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
