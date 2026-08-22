using System.Text;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Finalized;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Reporting;

public sealed class SnapshotReportExporterTests
{
    [Fact]
    public async Task ValidFinalizedSnapshot_IsExportedWithMetadata()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var renderer = new CapturingPdfRenderer();
        IReportExporter exporter = Exporter(FinalizedReportReadResult.Found(snapshot), renderer);

        ReportExportResult result = await exporter.ExportAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal("application/pdf", result.Artifact!.MediaType);
        Assert.Equal("snapshot-export-1", renderer.Model!.SnapshotId);
        Assert.Equal("report-calculation-v1", renderer.Model.ReportVersion);
        Assert.Equal("snapshot-format-v1", renderer.Model.SchemaVersion);
        Assert.Equal("export-architecture-v1", renderer.Model.GenerationMetadata.GeneratorVersion);
    }

    [Fact]
    public async Task InvalidIntegrityRead_IsRejectedBeforeRendering()
    {
        var renderer = new CapturingPdfRenderer();
        IReportExporter exporter = Exporter(FinalizedReportReadResult.Failure(
            FinalizedReportReadStatus.IntegrityInvalid, "checksum.invalid", "Invalid checksum."), renderer);

        ReportExportResult result = await exporter.ExportAsync(Request());

        Assert.Equal(ReportExportStatus.IntegrityInvalid, result.Status);
        Assert.Null(renderer.Model);
    }

    [Fact]
    public async Task UnsupportedVersion_IsRejectedBeforeRendering()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var renderer = new CapturingPdfRenderer();
        IReportExporter exporter = Exporter(FinalizedReportReadResult.Found(snapshot), renderer,
            supportedSnapshotVersions: ["future-version"]);

        ReportExportResult result = await exporter.ExportAsync(Request());

        Assert.Equal(ReportExportStatus.IntegrityUnsupported, result.Status);
        Assert.Null(renderer.Model);
    }

    [Fact]
    public async Task Export_SucceedsWithoutAnyOperationalSource()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var renderer = new CapturingPdfRenderer();
        var reader = new StubFinalizedReader(FinalizedReportReadResult.Found(snapshot));
        var exporter = new SnapshotReportExporter(reader,
            new ReportExportValidator(["snapshot-format-v1"], ["snapshot-integrity-v1"]),
            renderer, new StubExcelRenderer());

        ReportExportResult result = await exporter.ExportAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, reader.EffectiveReadCount);
    }

    [Fact]
    public async Task ExportModel_UsesDeterministicOutputOrdering()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var first = new CapturingPdfRenderer();
        var second = new CapturingPdfRenderer();

        ReportExportResult firstResult = await Exporter(FinalizedReportReadResult.Found(snapshot), first)
            .ExportAsync(Request());
        ReportExportResult secondResult = await Exporter(FinalizedReportReadResult.Found(snapshot), second)
            .ExportAsync(Request());

        Assert.Equal(firstResult.Artifact!.Content, secondResult.Artifact!.Content);
        Assert.Equal(first.Model!.UnitIds, second.Model!.UnitIds);
        Assert.Equal(first.Model.EventLog, second.Model.EventLog);
        Assert.Equal(first.Model.Warnings, second.Model.Warnings);
    }

    private static IReportExporter Exporter(FinalizedReportReadResult result,
        CapturingPdfRenderer renderer, IEnumerable<string>? supportedSnapshotVersions = null) =>
        new SnapshotReportExporter(new StubFinalizedReader(result),
            new ReportExportValidator(supportedSnapshotVersions ?? ["snapshot-format-v1"],
                ["snapshot-integrity-v1"]), renderer, new StubExcelRenderer());

    private static FinalizedReportExportRequest Request() => new(
        new("rasht", 10_000, 53_200, "Monthly"), ReportExportFormat.Pdf,
        new("export-architecture-v1",
            new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.FromHours(3.5)), "test-actor"));

    private static async Task<FinalizedReportSnapshot> SnapshotAsync()
    {
        SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(
            SyntheticReportingScenario.Complete);
        var request = new ReportFinalizationRequest("finalization-export-1", "snapshot-export-1",
            pipeline.Projection!, "rasht", 10_000, 53_200, ["unit-2", "unit-1"],
            "synthetic-read-revision-v1", "synthetic-read-revision-v1", 1, null, "test-actor",
            new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
            "finalization-policy-v1", "snapshot-integrity-v1");
        IReportFinalizationValidator validator = new ReportFinalizationValidator();
        FinalizedReportSnapshot pending = new ReportSnapshotFactory().Create(request,
            validator.Validate(request)).Snapshot!;
        return WithChecksum(pending);
    }

    private static FinalizedReportSnapshot WithChecksum(FinalizedReportSnapshot value) => new(
        value.Identity, value.ReportIdentity, value.Completeness, value.Evidence, value.Versions,
        new SnapshotChecksum("SHA-256", "snapshot-integrity-v1", SnapshotChecksumState.Calculated,
            new string('a', 64), 1024), value.OperationalSummaries, value.DailySummaries,
        value.RuntimeSummaries, value.EventSummaries, value.EventLog, value.ServiceSummaries,
        value.ExtremeDateSummaries, value.Warnings);

    private sealed class StubFinalizedReader(FinalizedReportReadResult result) : IFinalizedReportReader
    {
        public int EffectiveReadCount { get; private set; }
        public Task<FinalizedReportReadResult> GetBySnapshotIdAsync(string snapshotId,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<FinalizedReportReadResult> GetEffectiveAsync(FinalizedReportQuery query,
            CancellationToken cancellationToken = default)
        {
            EffectiveReadCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class CapturingPdfRenderer : IPdfReportRenderer
    {
        public FinalizedReportExportModel? Model { get; private set; }
        public Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
            CancellationToken cancellationToken = default)
        {
            Model = model;
            string deterministic = string.Join('|', model.UnitIds) + ";" +
                string.Join('|', model.EventLog.Select(x => x.EventId)) + ";" +
                string.Join('|', model.Warnings);
            return Task.FromResult(new RenderedReport("application/pdf", ".pdf",
                Array.AsReadOnly(Encoding.UTF8.GetBytes(deterministic))));
        }
    }

    private sealed class StubExcelRenderer : IExcelReportRenderer
    {
        public Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new RenderedReport("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".xlsx", Array.Empty<byte>()));
    }
}
