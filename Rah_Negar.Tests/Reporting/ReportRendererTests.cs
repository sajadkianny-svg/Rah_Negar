using ClosedXML.Excel;
using System.IO.Compression;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Infrastructure.Reporting.Export;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Reporting;

public sealed class ReportRendererTests
{
    private readonly IReportFileNamePolicy _fileNames = new DeterministicReportFileNamePolicy();

    [Fact]
    public async Task PdfRenderer_GeneratesPdfWithDeterministicFilename()
    {
        FinalizedReportExportModel model = await ModelAsync();

        RenderedReport result = await new PdfReportRenderer(_fileNames).RenderAsync(model);

        Assert.Equal("application/pdf", result.MediaType);
        Assert.Equal(".pdf", result.FileExtension);
        Assert.Equal("rasht_1405-05_Monthly_snapshot-format-v1.pdf", result.SuggestedFileName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.Content.Take(4).ToArray()));
        Assert.True(result.Content.Count > 1_000);
    }

    [Fact]
    public async Task ExcelRenderer_GeneratesRequiredSheets()
    {
        FinalizedReportExportModel model = await ModelAsync();
        RenderedReport result = await new ExcelReportRenderer(_fileNames).RenderAsync(model);

        using var stream = new MemoryStream(result.Content.ToArray());
        using var workbook = new XLWorkbook(stream);

        Assert.Equal(["Summary", "Runtime", "Events", "Daily", "Evidence"],
            workbook.Worksheets.Select(x => x.Name));
        Assert.Equal("rasht_1405-05_Monthly_snapshot-format-v1.xlsx", result.SuggestedFileName);
        Assert.Equal("snapshot-render-1", workbook.Worksheet("Evidence").Cell("B1").GetString());
    }

    [Fact]
    public async Task Renderers_PreserveSnapshotAndGenerationMetadata()
    {
        FinalizedReportExportModel model = await ModelAsync();
        RenderedReport result = await new ExcelReportRenderer(_fileNames).RenderAsync(model);
        using var workbook = new XLWorkbook(new MemoryStream(result.Content.ToArray()));
        IXLWorksheet evidence = workbook.Worksheet("Evidence");

        Assert.Equal(model.ReportVersion, evidence.Cell("B3").GetString());
        Assert.Equal(model.SchemaVersion, evidence.Cell("B4").GetString());
        Assert.Equal(model.IntegrityVersion, evidence.Cell("B5").GetString());
        Assert.Equal(model.Checksum, evidence.Cell("B6").GetString());
        Assert.Equal(model.GenerationMetadata.GeneratorVersion, evidence.Cell("B7").GetString());
        Assert.Equal(model.GenerationMetadata.GeneratedAt.ToString("O"), evidence.Cell("B8").GetString());
        Assert.Equal(model.GenerationMetadata.RequestedBy, evidence.Cell("B9").GetString());
    }

    [Fact]
    public async Task RepeatedRendering_PreservesDeterministicOutputOrdering()
    {
        FinalizedReportExportModel model = await ModelAsync();
        var pdf = new PdfReportRenderer(_fileNames);
        var excel = new ExcelReportRenderer(_fileNames);

        RenderedReport firstPdf = await pdf.RenderAsync(model);
        RenderedReport secondPdf = await pdf.RenderAsync(model);
        RenderedReport firstExcel = await excel.RenderAsync(model);
        RenderedReport secondExcel = await excel.RenderAsync(model);

        Assert.Equal(firstPdf.SuggestedFileName, secondPdf.SuggestedFileName);
        Assert.Equal(firstPdf.Content.Count, secondPdf.Content.Count);
        Assert.True(firstExcel.Content.SequenceEqual(secondExcel.Content),
            "Different XLSX entries: " + string.Join(", ", DifferentEntries(firstExcel.Content, secondExcel.Content)));
        using var workbook = new XLWorkbook(new MemoryStream(firstExcel.Content.ToArray()));
        Assert.Equal(model.RuntimeSummaries.Select(x => x.UnitId),
            workbook.Worksheet("Runtime").RowsUsed().Skip(1).Select(x => x.Cell(1).GetString()));
        Assert.Equal(model.EventLog.Select(x => x.EventId),
            workbook.Worksheet("Events").RowsUsed().Skip(1).Select(x => x.Cell(1).GetString()));
    }

    private static IEnumerable<string> DifferentEntries(IReadOnlyList<byte> first, IReadOnlyList<byte> second)
    {
        using var firstArchive = new ZipArchive(new MemoryStream(first.ToArray()), ZipArchiveMode.Read);
        using var secondArchive = new ZipArchive(new MemoryStream(second.ToArray()), ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in firstArchive.Entries)
        {
            ZipArchiveEntry? other = secondArchive.GetEntry(entry.FullName);
            if (other is null)
            {
                yield return entry.FullName + " (missing)";
                continue;
            }
            using Stream left = entry.Open();
            using Stream right = other.Open();
            using var leftBytes = new MemoryStream();
            using var rightBytes = new MemoryStream();
            left.CopyTo(leftBytes);
            right.CopyTo(rightBytes);
            if (!leftBytes.ToArray().SequenceEqual(rightBytes.ToArray())) yield return entry.FullName;
        }
    }

    [Fact]
    public async Task Renderers_RequireOnlyExportModel_WhenOperationalSourcesAreUnavailable()
    {
        FinalizedReportExportModel model = await ModelAsync();

        RenderedReport pdf = await new PdfReportRenderer(_fileNames).RenderAsync(model);
        RenderedReport excel = await new ExcelReportRenderer(_fileNames).RenderAsync(model);

        Assert.NotEmpty(pdf.Content);
        Assert.NotEmpty(excel.Content);
    }

    private static async Task<FinalizedReportExportModel> ModelAsync()
    {
        SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(
            SyntheticReportingScenario.Complete);
        var request = new ReportFinalizationRequest("finalization-render-1", "snapshot-render-1",
            pipeline.Projection!, "rasht", 10_000, 53_200, ["unit-2", "unit-1"],
            "synthetic-read-revision-v1", "synthetic-read-revision-v1", 1, null, "renderer-test",
            new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
            "finalization-policy-v1", "snapshot-integrity-v1");
        IReportFinalizationValidator validator = new ReportFinalizationValidator();
        FinalizedReportSnapshot pending = new ReportSnapshotFactory().Create(request,
            validator.Validate(request)).Snapshot!;
        var snapshot = new FinalizedReportSnapshot(pending.Identity, pending.ReportIdentity,
            pending.Completeness, pending.Evidence, pending.Versions,
            new SnapshotChecksum("SHA-256", "snapshot-integrity-v1", SnapshotChecksumState.Calculated,
                new string('b', 64), 2048), pending.OperationalSummaries, pending.DailySummaries,
            pending.RuntimeSummaries, pending.EventSummaries, pending.EventLog,
            pending.ServiceSummaries, pending.ExtremeDateSummaries, pending.Warnings);
        return FinalizedReportExportModel.FromSnapshot(snapshot,
            new ReportExportGenerationMetadata("renderer-v1",
                new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.FromHours(3.5)), "export-actor"));
    }
}
