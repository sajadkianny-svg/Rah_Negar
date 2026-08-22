using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Finalized;

namespace Rah_Negar.Foundation.Application.Reporting.Export;

/// <summary>Pure validation of an already integrity-checked finalized snapshot.</summary>
public sealed class ReportExportValidator : IReportExportValidator
{
    private readonly IReadOnlySet<string> _snapshotVersions;
    private readonly IReadOnlySet<string> _integrityVersions;

    public ReportExportValidator(IEnumerable<string> supportedSnapshotVersions,
        IEnumerable<string> supportedIntegrityVersions)
    {
        _snapshotVersions = new HashSet<string>(supportedSnapshotVersions ??
            throw new ArgumentNullException(nameof(supportedSnapshotVersions)), StringComparer.Ordinal);
        _integrityVersions = new HashSet<string>(supportedIntegrityVersions ??
            throw new ArgumentNullException(nameof(supportedIntegrityVersions)), StringComparer.Ordinal);
    }

    public ReportExportValidationResult Validate(FinalizedReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!_snapshotVersions.Contains(snapshot.Versions.SnapshotFormatVersion) ||
            !_integrityVersions.Contains(snapshot.Checksum.IntegrityFormatVersion))
            return Invalid(ReportExportStatus.IntegrityUnsupported, "report.export.version.unsupported",
                "Snapshot or integrity version is unsupported for export.");
        if (snapshot.Checksum.State != SnapshotChecksumState.Calculated ||
            string.IsNullOrWhiteSpace(snapshot.Checksum.Value) || snapshot.Checksum.CanonicalPayloadLength is null)
            return Invalid(ReportExportStatus.IntegrityInvalid, "report.export.integrity.invalid",
                "A calculated, verified snapshot checksum is required for export.");
        if (!snapshot.Completeness.IsFinalizationEligible ||
            snapshot.Versions.ValidateFor(snapshot.Identity.UnitIds).Count != 0)
            return Invalid(ReportExportStatus.ValidationRejected, "report.export.finalized-state.invalid",
                "Only complete finalized snapshots with full version evidence may be exported.");
        return new(true, ReportExportStatus.Exported, Array.Empty<ReportExportError>());
    }

    private static ReportExportValidationResult Invalid(ReportExportStatus status,
        string code, string message) => new(false, status,
            Array.AsReadOnly(new[] { new ReportExportError(code, message) }));
}

/// <summary>Exports only snapshots returned as valid by IFinalizedReportReader.</summary>
public sealed class SnapshotReportExporter : IReportExporter
{
    private readonly IFinalizedReportReader _reader;
    private readonly IReportExportValidator _validator;
    private readonly IPdfReportRenderer _pdf;
    private readonly IExcelReportRenderer _excel;

    public SnapshotReportExporter(IFinalizedReportReader reader, IReportExportValidator validator,
        IPdfReportRenderer pdf, IExcelReportRenderer excel)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _excel = excel ?? throw new ArgumentNullException(nameof(excel));
    }

    public async Task<ReportExportResult> ExportAsync(FinalizedReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            FinalizedReportReadResult read = await _reader.GetEffectiveAsync(request.Query, cancellationToken)
                .ConfigureAwait(false);
            if (!read.IsSuccess) return Map(read);

            ReportExportValidationResult validation = _validator.Validate(read.Snapshot!);
            if (!validation.IsValid)
            {
                ReportExportError error = validation.Errors[0];
                return ReportExportResult.Failure(validation.Status, error.Code, error.Message);
            }

            FinalizedReportExportModel model = FinalizedReportExportModel.FromSnapshot(
                read.Snapshot!, request.GenerationMetadata);
            RenderedReport artifact = request.Format == ReportExportFormat.Pdf
                ? await _pdf.RenderAsync(model, cancellationToken).ConfigureAwait(false)
                : await _excel.RenderAsync(model, cancellationToken).ConfigureAwait(false);
            return ReportExportResult.Success(artifact);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return ReportExportResult.Failure(ReportExportStatus.RendererFailed,
                "report.export.render.failed", "The finalized report could not be rendered safely.");
        }
    }

    private static ReportExportResult Map(FinalizedReportReadResult read)
    {
        ReportExportStatus status = read.Status switch
        {
            FinalizedReportReadStatus.NotFound => ReportExportStatus.NotFound,
            FinalizedReportReadStatus.NotFinalized => ReportExportStatus.NotFinalized,
            FinalizedReportReadStatus.IntegrityInvalid => ReportExportStatus.IntegrityInvalid,
            FinalizedReportReadStatus.IntegrityUnsupported => ReportExportStatus.IntegrityUnsupported,
            FinalizedReportReadStatus.LockSnapshotMismatch => ReportExportStatus.LockSnapshotMismatch,
            _ => ReportExportStatus.InfrastructureFailed
        };
        FinalizedReportReadError? error = read.Errors.FirstOrDefault();
        return ReportExportResult.Failure(status, error?.Code ?? "report.export.read.failed",
            error?.Message ?? "The finalized snapshot could not be read.");
    }
}
