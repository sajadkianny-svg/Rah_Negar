using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Finalized;

namespace Rah_Negar.Foundation.Application.Reporting.Export;

public enum ReportExportFormat { Pdf, Excel }

public enum ReportExportStatus
{
    Exported,
    NotFound,
    NotFinalized,
    IntegrityInvalid,
    IntegrityUnsupported,
    LockSnapshotMismatch,
    ValidationRejected,
    RendererFailed,
    InfrastructureFailed
}

public sealed record ReportExportGenerationMetadata(string GeneratorVersion,
    DateTimeOffset GeneratedAt, string RequestedBy);

public sealed record FinalizedReportExportRequest(FinalizedReportQuery Query,
    ReportExportFormat Format, ReportExportGenerationMetadata GenerationMetadata);

public sealed record ReportExportError(string Code, string Message);

public sealed class RenderedReport
{
    public RenderedReport(string mediaType, string fileExtension, IEnumerable<byte> content,
        string? suggestedFileName = null)
    {
        MediaType = string.IsNullOrWhiteSpace(mediaType) ?
            throw new ArgumentException("Media type is required.", nameof(mediaType)) : mediaType.Trim();
        FileExtension = string.IsNullOrWhiteSpace(fileExtension) ?
            throw new ArgumentException("File extension is required.", nameof(fileExtension)) : fileExtension.Trim();
        Content = Array.AsReadOnly((content ?? throw new ArgumentNullException(nameof(content))).ToArray());
        SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? null : suggestedFileName.Trim();
    }

    public string MediaType { get; }
    public string FileExtension { get; }
    public IReadOnlyList<byte> Content { get; }
    public string? SuggestedFileName { get; }
}

public sealed class FinalizedReportExportModel
{
    private FinalizedReportExportModel(FinalizedReportSnapshot snapshot,
        ReportExportGenerationMetadata generation)
    {
        SnapshotId = snapshot.Identity.SnapshotId;
        ReportId = snapshot.Identity.ReportId;
        StationId = snapshot.Identity.StationId;
        StationName = snapshot.ReportIdentity.StationName;
        PeriodStartMinute = snapshot.Identity.PeriodStartMinute;
        PeriodEndMinute = snapshot.Identity.PeriodEndMinute;
        PersianPeriodLabel = snapshot.ReportIdentity.PersianPeriodLabel;
        PeriodKind = snapshot.Identity.PeriodKind;
        UnitIds = ReadOnly(snapshot.Identity.UnitIds.OrderBy(x => x, StringComparer.Ordinal));
        ReportVersion = snapshot.Versions.ReportCalculationVersion;
        SchemaVersion = snapshot.Versions.SnapshotFormatVersion;
        IntegrityVersion = snapshot.Checksum.IntegrityFormatVersion;
        Checksum = snapshot.Checksum.Value!;
        GenerationMetadata = generation;
        OperationalSummaries = ReadOnly(snapshot.OperationalSummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal));
        DailySummaries = ReadOnly(snapshot.DailySummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal));
        RuntimeSummaries = ReadOnly(snapshot.RuntimeSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        EventSummaries = ReadOnly(snapshot.EventSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        EventLog = ReadOnly(snapshot.EventLog.OrderBy(x => x.EventMinute).ThenBy(x => x.UnitId, StringComparer.Ordinal)
            .ThenBy(x => x.SourceOrdinal).ThenBy(x => x.EventId, StringComparer.Ordinal));
        ServiceSummaries = ReadOnly(snapshot.ServiceSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        ExtremeDateSummaries = ReadOnly(snapshot.ExtremeDateSummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal));
        Warnings = ReadOnly(snapshot.Warnings.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }

    public string SnapshotId { get; }
    public string ReportId { get; }
    public string StationId { get; }
    public string StationName { get; }
    public long PeriodStartMinute { get; }
    public long PeriodEndMinute { get; }
    public string PersianPeriodLabel { get; }
    public ReportPeriodKind PeriodKind { get; }
    public IReadOnlyList<string> UnitIds { get; }
    public string ReportVersion { get; }
    public string SchemaVersion { get; }
    public string IntegrityVersion { get; }
    public string Checksum { get; }
    public ReportExportGenerationMetadata GenerationMetadata { get; }
    public IReadOnlyList<OperationalSummary> OperationalSummaries { get; }
    public IReadOnlyList<DailySummary> DailySummaries { get; }
    public IReadOnlyList<RuntimeSummary> RuntimeSummaries { get; }
    public IReadOnlyList<EventSummary> EventSummaries { get; }
    public IReadOnlyList<ReportEvent> EventLog { get; }
    public IReadOnlyList<ServiceSummary> ServiceSummaries { get; }
    public IReadOnlyList<ExtremeDateSummary> ExtremeDateSummaries { get; }
    public IReadOnlyList<string> Warnings { get; }

    public static FinalizedReportExportModel FromSnapshot(FinalizedReportSnapshot snapshot,
        ReportExportGenerationMetadata generation) => new(snapshot, generation);

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

public sealed class ReportExportResult
{
    private ReportExportResult(ReportExportStatus status, RenderedReport? artifact,
        IEnumerable<ReportExportError> errors)
    {
        Status = status;
        Artifact = artifact;
        Errors = Array.AsReadOnly(errors.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    public bool IsSuccess => Status == ReportExportStatus.Exported && Artifact is not null;
    public ReportExportStatus Status { get; }
    public RenderedReport? Artifact { get; }
    public IReadOnlyList<ReportExportError> Errors { get; }

    public static ReportExportResult Success(RenderedReport artifact) =>
        new(ReportExportStatus.Exported, artifact ?? throw new ArgumentNullException(nameof(artifact)), []);
    public static ReportExportResult Failure(ReportExportStatus status, string code, string message) =>
        status == ReportExportStatus.Exported
            ? throw new ArgumentException("A failure cannot have Exported status.", nameof(status))
            : new(status, null, [new(code, message)]);
}

public interface IReportExporter
{
    Task<ReportExportResult> ExportAsync(FinalizedReportExportRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPdfReportRenderer
{
    Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
        CancellationToken cancellationToken = default);
}

public interface IExcelReportRenderer
{
    Task<RenderedReport> RenderAsync(FinalizedReportExportModel model,
        CancellationToken cancellationToken = default);
}

public interface IReportFileNamePolicy
{
    string Create(FinalizedReportExportModel model, ReportExportFormat format);
}

public sealed record ReportExportValidationResult(bool IsValid, ReportExportStatus Status,
    IReadOnlyList<ReportExportError> Errors);

public interface IReportExportValidator
{
    ReportExportValidationResult Validate(FinalizedReportSnapshot snapshot);
}
