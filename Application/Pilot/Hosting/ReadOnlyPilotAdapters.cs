using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Core;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Foundation.Application.Reporting.Finalized;
using Rah_Negar.Foundation.Application.Runtime.Shadow;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Application.UI.Settings;

namespace Rah_Negar.Foundation.Application.Pilot.Hosting;

public interface ILegacyAuthenticationStateReader
{
    bool IsAuthenticated { get; }
    string SourceVersion { get; }
}

/// <summary>Reads the existing session flag; it never calls Login or Logout.</summary>
public sealed class AppSessionAuthenticationStateReader : ILegacyAuthenticationStateReader
{
    public bool IsAuthenticated => AppSession.IsLoggedIn;
    public string SourceVersion => "legacy-app-session-v1";
}

public sealed class LegacyAuthenticationObservationAdapter :
    ILegacyAuthenticationPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly ILegacyAuthenticationStateReader _reader;

    public LegacyAuthenticationObservationAdapter(ILegacyAuthenticationStateReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Descriptor = PilotAdapterDescriptors.Create("legacy-authentication-observer",
            "phase8.3-v1", reader.SourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public Task<LegacyAuthenticationPilotObservation> ObserveAuthoritativeAsync(
        AuthenticationPilotRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool authenticated = _reader.IsAuthenticated;
        return Task.FromResult(new LegacyAuthenticationPilotObservation(authenticated,
            PilotSafeFingerprint.Create("legacy-authentication", authenticated ? "accepted" : "rejected"),
            authenticated ? "LegacySessionAuthenticated" : "LegacySessionNotAuthenticated"));
    }
}

public sealed record ShiftProfileAuthenticationReadModelResult(
    bool Succeeded,
    string ShiftProfileId,
    string StationId,
    int CredentialVersion,
    string SafeCategory,
    string SourceVersion);

/// <summary>
/// Safe authentication-result read model. It deliberately accepts no password, verifier, salt, or session writer.
/// </summary>
public interface IShiftProfileAuthenticationReadModel
{
    Task<ShiftProfileAuthenticationReadModelResult> ObserveAsync(
        string stationId,
        string shiftProfileId,
        CancellationToken cancellationToken = default);
}

public sealed class ShiftProfileAuthenticationObservationAdapter :
    IShiftProfileAuthenticationPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly IShiftProfileAuthenticationReadModel _source;

    public ShiftProfileAuthenticationObservationAdapter(
        IShiftProfileAuthenticationReadModel source,
        string sourceVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Descriptor = PilotAdapterDescriptors.Create("target-shift-profile-authentication-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<ShiftProfileAuthenticationPilotObservation> ObserveReadOnlyAsync(
        AuthenticationPilotRequest request, CancellationToken cancellationToken = default)
    {
        ShiftProfileAuthenticationReadModelResult result = await _source.ObserveAsync(
            request.Context.StationId, request.ShiftProfileId, cancellationToken).ConfigureAwait(false);
        string fingerprint = PilotSafeFingerprint.Create("target-authentication", result.ShiftProfileId,
            result.StationId, result.CredentialVersion.ToString(CultureInfo.InvariantCulture),
            result.Succeeded ? "accepted" : "rejected", result.SourceVersion);
        return new(result.Succeeded, result.ShiftProfileId, result.StationId,
            result.CredentialVersion, fingerprint, SafeCategory(result.SafeCategory,
                result.Succeeded ? "TargetAuthenticationAccepted" : "TargetAuthenticationRejected"));
    }

    private static string SafeCategory(string category, string fallback) =>
        string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
}

public sealed record LegacyReportReadModelResult(
    bool Readable,
    IReadOnlyDictionary<string, string> SectionFingerprints,
    string SourceVersion,
    string SafeCategory);

/// <summary>
/// Explicit read-side bridge for an existing legacy report result. Implementations must be supplied by
/// a future isolated host; this contract performs no connection discovery.
/// </summary>
public interface ILegacyReportReadModel
{
    Task<LegacyReportReadModelResult> ReadAsync(
        string stationId,
        string reportScope,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyReportObservationAdapter :
    ILegacyReportPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly ILegacyReportReadModel _source;

    public LegacyReportObservationAdapter(ILegacyReportReadModel source, string sourceVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Descriptor = PilotAdapterDescriptors.Create("legacy-report-observer", "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<LegacyReportPilotObservation> ObserveAuthoritativeAsync(
        ReportingPilotRequest request, CancellationToken cancellationToken = default)
    {
        LegacyReportReadModelResult result = await _source.ReadAsync(request.Context.StationId,
            request.ReportScope, cancellationToken).ConfigureAwait(false);
        string fingerprint = PilotSafeFingerprint.CreateSections(result.SectionFingerprints);
        return new(result.Readable, fingerprint,
            string.IsNullOrWhiteSpace(result.SafeCategory) ? "LegacyReportObserved" : result.SafeCategory.Trim());
    }
}

/// <summary>Uses the Phase 5 finalized-snapshot reader and pure export validator only.</summary>
public sealed class SnapshotReportObservationAdapter :
    ITargetSnapshotPilotObserver, IExportArtifactPilotValidator, IPilotAdapterDescriptorProvider
{
    private readonly IFinalizedReportReader _reader;
    private readonly IReportExportValidator _exportValidator;

    public SnapshotReportObservationAdapter(IFinalizedReportReader reader,
        IReportExportValidator exportValidator, string sourceVersion)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _exportValidator = exportValidator ?? throw new ArgumentNullException(nameof(exportValidator));
        Descriptor = PilotAdapterDescriptors.Create("target-finalized-snapshot-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<TargetSnapshotPilotObservation> ObserveReadOnlyAsync(
        ReportingPilotRequest request, CancellationToken cancellationToken = default)
    {
        FinalizedReportReadResult read = await _reader.GetBySnapshotIdAsync(
            request.SnapshotId, cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess)
            return new(false, request.SnapshotId,
                PilotSafeFingerprint.Create("snapshot-read", read.Status.ToString()), true,
                false, false, $"Snapshot{read.Status}");
        FinalizedReportSnapshot snapshot = read.Snapshot!;
        bool identityMatches = StringComparer.Ordinal.Equals(snapshot.Identity.SnapshotId, request.SnapshotId) &&
            StringComparer.Ordinal.Equals(snapshot.Identity.StationId, request.Context.StationId);
        return new(identityMatches, snapshot.Identity.SnapshotId,
            SnapshotSections.CreateFingerprint(snapshot), true, false, false,
            identityMatches ? "FinalizedSnapshotObserved" : "SnapshotIdentityMismatch");
    }

    public async Task<ExportArtifactPilotObservation> ValidateReadOnlyAsync(
        ReportingPilotRequest request, CancellationToken cancellationToken = default)
    {
        FinalizedReportReadResult read = await _reader.GetBySnapshotIdAsync(
            request.SnapshotId, cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess)
            return new(false, PilotSafeFingerprint.Create("snapshot-export", read.Status.ToString()),
                false, $"SnapshotExport{read.Status}");
        FinalizedReportSnapshot snapshot = read.Snapshot!;
        ReportExportValidationResult validation = _exportValidator.Validate(snapshot);
        return new(validation.IsValid,
            PilotSafeFingerprint.Create("snapshot-export", SnapshotSections.CreateFingerprint(snapshot),
                validation.Status.ToString()), false,
            validation.IsValid ? "SnapshotExportValidated" : $"SnapshotExport{validation.Status}");
    }
}

public sealed record LegacyRuntimeEventReadModelResult(
    string RuntimeFingerprint,
    string EventFingerprint,
    string SourceVersion,
    string SafeCategory);

public interface ILegacyRuntimeEventReadModel
{
    Task<LegacyRuntimeEventReadModelResult> ReadAsync(
        string stationId,
        string projectionScope,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyRuntimeEventObservationAdapter :
    ILegacyRuntimeEventPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly ILegacyRuntimeEventReadModel _source;

    public LegacyRuntimeEventObservationAdapter(ILegacyRuntimeEventReadModel source, string sourceVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Descriptor = PilotAdapterDescriptors.Create("legacy-runtime-event-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<LegacyRuntimeEventPilotObservation> ObserveAuthoritativeAsync(
        RuntimeEventPilotRequest request, CancellationToken cancellationToken = default)
    {
        LegacyRuntimeEventReadModelResult result = await _source.ReadAsync(
            request.Context.StationId, request.ProjectionScope, cancellationToken).ConfigureAwait(false);
        return new(PilotSafeFingerprint.Create("legacy-runtime", result.RuntimeFingerprint,
                result.SourceVersion),
            PilotSafeFingerprint.Create("legacy-event", result.EventFingerprint, result.SourceVersion),
            string.IsNullOrWhiteSpace(result.SafeCategory) ? "LegacyRuntimeEventObserved" :
                result.SafeCategory.Trim());
    }
}

public sealed record RuntimeEventTargetReadModelResult(
    RuntimeShadowExecutionResult RuntimeResult,
    EventComparisonResult EventResult,
    string SourceVersion);

/// <summary>Supplies existing Phase 4 shadow/comparison results from an approved read-only source.</summary>
public interface IRuntimeEventTargetReadModel
{
    Task<RuntimeEventTargetReadModelResult> ObserveAsync(
        string stationId,
        string projectionScope,
        CancellationToken cancellationToken = default);
}

public sealed class TargetRuntimeEventObservationAdapter :
    ITargetRuntimeEventPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly IRuntimeEventTargetReadModel _source;

    public TargetRuntimeEventObservationAdapter(IRuntimeEventTargetReadModel source, string sourceVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Descriptor = PilotAdapterDescriptors.Create("target-runtime-event-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<TargetRuntimeEventPilotObservation> ObserveReadOnlyAsync(
        RuntimeEventPilotRequest request, CancellationToken cancellationToken = default)
    {
        RuntimeEventTargetReadModelResult result = await _source.ObserveAsync(
            request.Context.StationId, request.ProjectionScope, cancellationToken).ConfigureAwait(false);
        RuntimeShadowExecutionResult runtime = result.RuntimeResult;
        EventComparisonResult events = result.EventResult;
        bool usable = runtime.Status is RuntimeShadowExecutionStatus.Match or
            RuntimeShadowExecutionStatus.DifferenceDetected && runtime.Evidence is not null;
        string runtimeFingerprint = PilotSafeFingerprint.Create("target-runtime",
            runtime.StationId, runtime.UnitId,
            runtime.PeriodStartMinute.ToString(CultureInfo.InvariantCulture),
            runtime.PeriodEndMinute.ToString(CultureInfo.InvariantCulture), runtime.Status.ToString(),
            runtime.Evidence?.CalculationVersion ?? "unavailable", result.SourceVersion);
        string eventFingerprint = PilotSafeFingerprint.Create("target-event",
            events.Category.ToString(), events.LegacyEventCount.ToString(CultureInfo.InvariantCulture),
            events.TargetEventCount.ToString(CultureInfo.InvariantCulture),
            events.LegacyFinalState.ToString(), events.TargetFinalState.ToString(),
            string.Join(',', events.Differences.Order(StringComparer.Ordinal)), result.SourceVersion);
        return new(runtimeFingerprint, eventFingerprint, usable,
            false, false, false, false, false,
            usable ? "TargetRuntimeEventObserved" : $"TargetRuntime{runtime.Status}");
    }
}

/// <summary>Wraps the existing protected-settings read contract; values are never returned as raw rows.</summary>
public sealed class LegacyProtectedSettingsObservationAdapter :
    ILegacySettingsPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly IProtectedSettingsReader _reader;

    public LegacyProtectedSettingsObservationAdapter(IProtectedSettingsReader reader, string sourceVersion)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Descriptor = PilotAdapterDescriptors.Create("legacy-protected-settings-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<LegacySettingsPilotObservation> ObserveAuthoritativeAsync(
        ProtectedSettingsPilotRequest request, CancellationToken cancellationToken = default)
    {
        ProtectedSettingsSnapshot snapshot = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        bool matchesStation = StringComparer.Ordinal.Equals(snapshot.StationId, request.Context.StationId);
        string fingerprint = PilotSafeFingerprint.Create("legacy-settings", snapshot.StationId,
            snapshot.EsdAdjustmentEnabled ? "enabled" : "disabled",
            snapshot.EsdAdjustmentHours.ToString("G29", CultureInfo.InvariantCulture),
            string.Join(',', snapshot.DisplaySettings.Keys.Order(StringComparer.Ordinal)));
        return new(matchesStation, fingerprint,
            matchesStation ? "LegacySettingsObserved" : "LegacySettingsStationMismatch");
    }
}

/// <summary>Evaluates the Phase 8.1 legacy-authoritative policy without invoking authorization or mutation.</summary>
public sealed class TargetProtectedSettingsDecisionAdapter :
    ITargetProtectedSettingsPilotObserver, IPilotAdapterDescriptorProvider
{
    public TargetProtectedSettingsDecisionAdapter(string sourceVersion) =>
        Descriptor = PilotAdapterDescriptors.Create("target-protected-settings-decision-observer",
            "phase8.3-v1", sourceVersion);

    public PilotAdapterDescriptor Descriptor { get; }

    public Task<TargetProtectedSettingsPilotObservation> EvaluateDecisionReadOnlyAsync(
        ProtectedSettingsPilotRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var routing = new IntegrationAuthorityRoutingDecision(IntegrationControlDecision.Allowed,
            IntegrationAuthorityMode.ShadowValidation, IntegrationAuthorityMode.ShadowValidation,
            request.Context.StationId, request.Context.EvidencePackageId, request.Context.CorrelationId,
            true, true, false, Array.Empty<string>());
        var evidence = new ProtectedSettingsIntegrationEvidence(EsdAuthorityMode.LegacyAuthoritative,
            true, request.TargetProvisioningRequested, request.EsdCutoverRequested,
            request.SettingsMutationRequested, request.Context.EvidencePackageId);
        ProtectedSettingsIntegrationDecision decision = ProtectedSettingsIntegrationPolicy.Evaluate(
            new(ProtectedSettingsIntegrationMode.ProtectedSettingsShadow, routing, evidence));
        string fingerprint = PilotSafeFingerprint.Create("target-settings-decision",
            request.SettingsScope, decision.Decision.ToString(), decision.ResultCategory,
            decision.LegacySettingsAuthoritative ? "legacy-authoritative" : "invalid-authority");
        return Task.FromResult(new TargetProtectedSettingsPilotObservation(fingerprint,
            request.SettingsMutationRequested, request.TargetProvisioningRequested,
            request.EsdCutoverRequested, false, false, decision.ResultCategory));
    }
}

public sealed record LegacyExportReadModelResult(
    bool Readable,
    string ArtifactFingerprint,
    string SourceVersion,
    string SafeCategory);

public interface ILegacyExportReadModel
{
    Task<LegacyExportReadModelResult> ReadAsync(
        string stationId,
        string snapshotId,
        string exportFormat,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyExportObservationAdapter :
    ILegacyExportPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly ILegacyExportReadModel _source;

    public LegacyExportObservationAdapter(ILegacyExportReadModel source, string sourceVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Descriptor = PilotAdapterDescriptors.Create("legacy-export-observer", "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<LegacyExportPilotObservation> ObserveAuthoritativeAsync(
        ExportPilotRequest request, CancellationToken cancellationToken = default)
    {
        LegacyExportReadModelResult result = await _source.ReadAsync(request.Context.StationId,
            request.SnapshotId, request.ExportFormat, cancellationToken).ConfigureAwait(false);
        return new(result.Readable, PilotSafeFingerprint.Create("legacy-export",
                result.ArtifactFingerprint, result.SourceVersion),
            string.IsNullOrWhiteSpace(result.SafeCategory) ? "LegacyExportObserved" : result.SafeCategory.Trim());
    }
}

/// <summary>Validates a finalized snapshot for export without rendering or writing an artifact.</summary>
public sealed class SnapshotExportObservationAdapter :
    ITargetExportPilotObserver, IPilotAdapterDescriptorProvider
{
    private readonly IFinalizedReportReader _reader;
    private readonly IReportExportValidator _validator;

    public SnapshotExportObservationAdapter(IFinalizedReportReader reader,
        IReportExportValidator validator, string sourceVersion)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        Descriptor = PilotAdapterDescriptors.Create("target-snapshot-export-observer",
            "phase8.3-v1", sourceVersion);
    }

    public PilotAdapterDescriptor Descriptor { get; }

    public async Task<TargetExportPilotObservation> ValidateReadOnlyAsync(
        ExportPilotRequest request, CancellationToken cancellationToken = default)
    {
        FinalizedReportReadResult read = await _reader.GetBySnapshotIdAsync(
            request.SnapshotId, cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess)
            return new(false, PilotSafeFingerprint.Create("target-export", read.Status.ToString()),
                true, true, false, $"TargetExport{read.Status}");
        FinalizedReportSnapshot snapshot = read.Snapshot!;
        ReportExportValidationResult validation = _validator.Validate(snapshot);
        bool identityMatches = StringComparer.Ordinal.Equals(snapshot.Identity.SnapshotId,
            request.SnapshotId) && StringComparer.Ordinal.Equals(snapshot.Identity.StationId,
            request.Context.StationId);
        return new(validation.IsValid && identityMatches,
            PilotSafeFingerprint.Create("target-export", SnapshotSections.CreateFingerprint(snapshot),
                request.ExportFormat.Trim().ToUpperInvariant(), validation.Status.ToString()),
            true, true, false,
            validation.IsValid && identityMatches ? "TargetExportValidated" : "TargetExportRejected");
    }
}

internal static class PilotAdapterDescriptors
{
    public static PilotAdapterDescriptor Create(string id, string version, string sourceVersion)
    {
        if (string.IsNullOrWhiteSpace(sourceVersion))
            throw new ArgumentException("Source version is required.", nameof(sourceVersion));
        return new(id, version, sourceVersion.Trim(), true, true);
    }
}

internal static class SnapshotSections
{
    public static string CreateFingerprint(FinalizedReportSnapshot snapshot)
    {
        IReadOnlyDictionary<string, string> sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["identity"] = PilotSafeFingerprint.Create(snapshot.Identity.SnapshotId,
                snapshot.Identity.StationId, snapshot.Identity.PeriodStartMinute.ToString(CultureInfo.InvariantCulture),
                snapshot.Identity.PeriodEndMinute.ToString(CultureInfo.InvariantCulture)),
            ["operational"] = PilotSafeFingerprint.Create(snapshot.OperationalSummaries.Count.ToString(
                CultureInfo.InvariantCulture)),
            ["daily"] = PilotSafeFingerprint.Create(snapshot.DailySummaries.Count.ToString(CultureInfo.InvariantCulture)),
            ["runtime"] = PilotSafeFingerprint.Create(snapshot.RuntimeSummaries.Count.ToString(CultureInfo.InvariantCulture)),
            ["events"] = PilotSafeFingerprint.Create(snapshot.EventSummaries.Count.ToString(CultureInfo.InvariantCulture),
                snapshot.EventLog.Count.ToString(CultureInfo.InvariantCulture)),
            ["service"] = PilotSafeFingerprint.Create(snapshot.ServiceSummaries.Count.ToString(CultureInfo.InvariantCulture)),
            ["extremes"] = PilotSafeFingerprint.Create(snapshot.ExtremeDateSummaries.Count.ToString(CultureInfo.InvariantCulture)),
            ["integrity"] = snapshot.Checksum.Value ?? "checksum-unavailable"
        };
        return PilotSafeFingerprint.CreateSections(sections);
    }
}

internal static class PilotSafeFingerprint
{
    public static string Create(params string[] values)
    {
        using var stream = new MemoryStream();
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] length = Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture));
            stream.Write(length);
            stream.WriteByte((byte)':');
            stream.Write(bytes);
            stream.WriteByte((byte)'|');
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static string CreateSections(IReadOnlyDictionary<string, string> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return Create(sections.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .SelectMany(pair => new[] { pair.Key, pair.Value }).ToArray());
    }
}
