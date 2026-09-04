using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Operational;

public interface IControlledPilotFingerprintSpecification
{
    PilotValidationWorkflow Workflow { get; }
    string Version { get; }
    string Algorithm { get; }
}

public interface IControlledPilotFingerprintSpecification<in TObservation> :
    IControlledPilotFingerprintSpecification
{
    string CreateFingerprint(TObservation observation);
}

public enum OperationalObservationBoundary
{
    LegacyAuthoritative,
    TargetReadOnly
}

public interface IControlledPilotBoundaryObservation
{
    OperationalObservationBoundary Boundary { get; }
    bool IsValid { get; }
}

public sealed class AuthenticationOperationalObservation : IControlledPilotBoundaryObservation
{
    public AuthenticationOperationalObservation(
        string stationScopeId,
        bool capabilityAvailable,
        bool identifiesShiftProfile,
        bool acceptsPersonnelNumber,
        bool enforcesStationScope,
        IEnumerable<string> capabilityCodes,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        ArgumentNullException.ThrowIfNull(capabilityCodes);
        StationScopeId = OperationalText.SafeIdentifier(stationScopeId,
            "station-scope-unavailable");
        CapabilityAvailable = capabilityAvailable;
        IdentifiesShiftProfile = identifiesShiftProfile;
        AcceptsPersonnelNumber = acceptsPersonnelNumber;
        EnforcesStationScope = enforcesStationScope;
        CapabilityCodes = OperationalCollections.SafeSortedIdentifiers(capabilityCodes);
        Boundary = boundary;
    }

    public string StationScopeId { get; }
    public bool CapabilityAvailable { get; }
    public bool IdentifiesShiftProfile { get; }
    public bool AcceptsPersonnelNumber { get; }
    public bool EnforcesStationScope { get; }
    public IReadOnlyList<string> CapabilityCodes { get; }
    public OperationalObservationBoundary Boundary { get; }
    public bool AcceptsPassword => false;
    public bool ContainsCredentialHash => false;
    public bool CreatesSession => false;
    public bool ImplementsRoles => false;
    public bool IsValid => Enum.IsDefined(Boundary) &&
        OperationalText.IsUsableIdentifier(StationScopeId) && CapabilityCodes.Count > 0 &&
        CapabilityCodes.All(OperationalText.IsUsableIdentifier);
}

public sealed record ReportingSummaryObservation(
    string ParameterId,
    string AggregationCode,
    decimal Value,
    int ContributingCount);

public sealed record ReportingChartPointObservation(
    string SeriesId,
    string PointIdentity,
    decimal Value);

public sealed record ReportingDailyStatusObservation(
    string DateIdentity,
    string StatusCode,
    int ExpectedRecordCount,
    int ActualRecordCount);

public sealed class ReportingOperationalObservation : IControlledPilotBoundaryObservation
{
    public ReportingOperationalObservation(
        string stationId,
        string periodIdentity,
        long periodStartMinute,
        long periodEndMinute,
        IEnumerable<ReportingSummaryObservation> summaries,
        IEnumerable<ReportingChartPointObservation> chartPoints,
        IEnumerable<ReportingDailyStatusObservation> dailyStatuses,
        IEnumerable<string> warningCodes,
        string? finalizedSnapshotId,
        string? finalizedSnapshotChecksum,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        ArgumentNullException.ThrowIfNull(chartPoints);
        ArgumentNullException.ThrowIfNull(dailyStatuses);
        ArgumentNullException.ThrowIfNull(warningCodes);
        if (periodEndMinute <= periodStartMinute)
            throw new ArgumentException("Reporting observation period must be non-empty.");
        StationId = OperationalText.SafeIdentifier(stationId, "station-unavailable");
        PeriodIdentity = OperationalText.SafeIdentifier(periodIdentity, "period-unavailable");
        PeriodStartMinute = periodStartMinute;
        PeriodEndMinute = periodEndMinute;
        Summaries = OperationalCollections.ReadOnly(summaries
            .Select(x => new ReportingSummaryObservation(
                OperationalText.SafeIdentifier(x.ParameterId, "parameter-unavailable"),
                OperationalText.SafeIdentifier(x.AggregationCode, "aggregation-unavailable"),
                x.Value, x.ContributingCount))
            .OrderBy(x => x.ParameterId, StringComparer.Ordinal)
            .ThenBy(x => x.AggregationCode, StringComparer.Ordinal));
        ChartPoints = OperationalCollections.ReadOnly(chartPoints
            .Select(x => new ReportingChartPointObservation(
                OperationalText.SafeIdentifier(x.SeriesId, "series-unavailable"),
                OperationalText.SafeIdentifier(x.PointIdentity, "point-unavailable"), x.Value))
            .OrderBy(x => x.SeriesId, StringComparer.Ordinal)
            .ThenBy(x => x.PointIdentity, StringComparer.Ordinal));
        DailyStatuses = OperationalCollections.ReadOnly(dailyStatuses
            .Select(x => new ReportingDailyStatusObservation(
                OperationalText.SafeIdentifier(x.DateIdentity, "date-unavailable"),
                OperationalText.SafeIdentifier(x.StatusCode, "status-unavailable"),
                x.ExpectedRecordCount, x.ActualRecordCount))
            .OrderBy(x => x.DateIdentity, StringComparer.Ordinal));
        WarningCodes = OperationalCollections.SafeSortedIdentifiers(warningCodes);
        FinalizedSnapshotId = string.IsNullOrWhiteSpace(finalizedSnapshotId) ? null :
            OperationalText.SafeIdentifier(finalizedSnapshotId, "snapshot-unavailable");
        FinalizedSnapshotChecksum = finalizedSnapshotChecksum is null
            ? null
            : FingerprintSafety.SafeSha256(finalizedSnapshotChecksum);
        Boundary = boundary;
    }

    public string StationId { get; }
    public string PeriodIdentity { get; }
    public long PeriodStartMinute { get; }
    public long PeriodEndMinute { get; }
    public IReadOnlyList<ReportingSummaryObservation> Summaries { get; }
    public IReadOnlyList<ReportingChartPointObservation> ChartPoints { get; }
    public IReadOnlyList<ReportingDailyStatusObservation> DailyStatuses { get; }
    public IReadOnlyList<string> WarningCodes { get; }
    public string? FinalizedSnapshotId { get; }
    public string? FinalizedSnapshotChecksum { get; }
    public OperationalObservationBoundary Boundary { get; }
    public bool RecalculatesFinalizedSnapshot => false;
    public bool FinalizesSnapshot => false;
    public bool IsValid => Enum.IsDefined(Boundary) &&
        OperationalText.IsUsableIdentifier(StationId) &&
        OperationalText.IsUsableIdentifier(PeriodIdentity) &&
        Summaries.Count > 0 && Summaries.All(item =>
            OperationalText.IsUsableIdentifier(item.ParameterId) &&
            OperationalText.IsUsableIdentifier(item.AggregationCode) &&
            item.ContributingCount >= 0) && ChartPoints.All(item =>
            OperationalText.IsUsableIdentifier(item.SeriesId) &&
            OperationalText.IsUsableIdentifier(item.PointIdentity)) &&
        DailyStatuses.All(item => OperationalText.IsUsableIdentifier(item.DateIdentity) &&
            OperationalText.IsUsableIdentifier(item.StatusCode) &&
            item.ExpectedRecordCount >= 0 && item.ActualRecordCount >= 0) &&
        WarningCodes.All(OperationalText.IsUsableIdentifier) &&
        (FinalizedSnapshotId is null ||
            OperationalText.IsUsableIdentifier(FinalizedSnapshotId)) &&
        (FinalizedSnapshotChecksum is null ||
            FinalizedSnapshotChecksum.Any(character => character != '0'));
}

public sealed record RuntimeEventItemObservation(
    string EventId,
    EventType EventType,
    long EventMinute,
    int Sequence);

public sealed class RuntimeUnitOperationalObservation
{
    public RuntimeUnitOperationalObservation(
        string unitId,
        IEnumerable<RuntimeEventItemObservation> authoritativeEvents,
        long physicalRuntimeMinutes,
        long esdAdjustmentMinutes,
        long adjustedRuntimeMinutes,
        long runtimeAfterOhMinutes,
        UnitOperationalState state,
        int serviceDayCount,
        long longestRunMinutes,
        long cumulativeRuntimeMinutes,
        string trustedBaselineReference)
    {
        ArgumentNullException.ThrowIfNull(authoritativeEvents);
        UnitId = OperationalText.SafeIdentifier(unitId, "unit-unavailable");
        AuthoritativeEvents = OperationalCollections.ReadOnly(authoritativeEvents
            .Select(x => new RuntimeEventItemObservation(
                OperationalText.SafeIdentifier(x.EventId, "event-unavailable"),
                x.EventType, x.EventMinute, x.Sequence))
            .OrderBy(x => x.EventMinute).ThenBy(x => x.Sequence)
            .ThenBy(x => x.EventId, StringComparer.Ordinal));
        PhysicalRuntimeMinutes = physicalRuntimeMinutes;
        EsdAdjustmentMinutes = esdAdjustmentMinutes;
        AdjustedRuntimeMinutes = adjustedRuntimeMinutes;
        RuntimeAfterOhMinutes = runtimeAfterOhMinutes;
        State = state;
        ServiceDayCount = serviceDayCount;
        LongestRunMinutes = longestRunMinutes;
        CumulativeRuntimeMinutes = cumulativeRuntimeMinutes;
        TrustedBaselineReference = OperationalText.SafeIdentifier(trustedBaselineReference,
            "baseline-unavailable");
    }

    public string UnitId { get; }
    public IReadOnlyList<RuntimeEventItemObservation> AuthoritativeEvents { get; }
    public long PhysicalRuntimeMinutes { get; }
    public long EsdAdjustmentMinutes { get; }
    public long AdjustedRuntimeMinutes { get; }
    public long RuntimeAfterOhMinutes { get; }
    public UnitOperationalState State { get; }
    public int ServiceDayCount { get; }
    public long LongestRunMinutes { get; }
    public long CumulativeRuntimeMinutes { get; }
    public string TrustedBaselineReference { get; }
}

public sealed class RuntimeEventOperationalObservation : IControlledPilotBoundaryObservation
{
    public RuntimeEventOperationalObservation(
        string stationId,
        long periodStartMinute,
        long periodEndMinute,
        IEnumerable<RuntimeUnitOperationalObservation> units,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (periodEndMinute <= periodStartMinute)
            throw new ArgumentException("Runtime observation period must be non-empty.");
        StationId = OperationalText.SafeIdentifier(stationId, "station-unavailable");
        PeriodStartMinute = periodStartMinute;
        PeriodEndMinute = periodEndMinute;
        Units = OperationalCollections.ReadOnly(units.OrderBy(x => x.UnitId,
            StringComparer.Ordinal));
        Boundary = boundary;
    }

    public string StationId { get; }
    public long PeriodStartMinute { get; }
    public long PeriodEndMinute { get; }
    public IReadOnlyList<RuntimeUnitOperationalObservation> Units { get; }
    public OperationalObservationBoundary Boundary { get; }
    public bool MutatesEvents => false;
    public bool AppliesEsdMutation => false;
    public bool IsValid => Enum.IsDefined(Boundary) &&
        OperationalText.IsUsableIdentifier(StationId) && Units.Count > 0 &&
        Units.All(unit => OperationalText.IsUsableIdentifier(unit.UnitId) &&
            OperationalText.IsUsableIdentifier(unit.TrustedBaselineReference) &&
            unit.PhysicalRuntimeMinutes >= 0 && unit.EsdAdjustmentMinutes >= 0 &&
            unit.AdjustedRuntimeMinutes == unit.PhysicalRuntimeMinutes +
                unit.EsdAdjustmentMinutes && unit.RuntimeAfterOhMinutes >= 0 &&
            unit.ServiceDayCount >= 0 && unit.LongestRunMinutes >= 0 &&
            unit.LongestRunMinutes <= unit.PhysicalRuntimeMinutes &&
            unit.CumulativeRuntimeMinutes >= 0 && Enum.IsDefined(unit.State) &&
            unit.AuthoritativeEvents.All(item =>
                OperationalText.IsUsableIdentifier(item.EventId) &&
                Enum.IsDefined(item.EventType) && item.Sequence >= 0));
}

public sealed class ProtectedSettingsOperationalObservation : IControlledPilotBoundaryObservation
{
    public ProtectedSettingsOperationalObservation(
        string stationId,
        string settingStateCode,
        decimal esdAdjustmentValue,
        string effectiveEvidenceReference,
        bool managementProtectionRequired,
        bool externalVendorAuthorizationRequired,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        StationId = OperationalText.SafeIdentifier(stationId, "station-unavailable");
        SettingStateCode = OperationalText.SafeIdentifier(settingStateCode,
            "setting-state-unavailable");
        EsdAdjustmentValue = esdAdjustmentValue;
        EffectiveEvidenceReference = OperationalText.SafeIdentifier(
            effectiveEvidenceReference, "effective-evidence-unavailable");
        ManagementProtectionRequired = managementProtectionRequired;
        ExternalVendorAuthorizationRequired = externalVendorAuthorizationRequired;
        Boundary = boundary;
    }

    public string StationId { get; }
    public string SettingStateCode { get; }
    public decimal EsdAdjustmentValue { get; }
    public string EffectiveEvidenceReference { get; }
    public bool ManagementProtectionRequired { get; }
    public bool ExternalVendorAuthorizationRequired { get; }
    public OperationalObservationBoundary Boundary { get; }
    public bool VerifiesManagementCredential => false;
    public bool ExecutesVendorAuthorization => false;
    public bool MutatesEsdAdjustment => false;
    public bool RecoversOrProvisions => false;
    public bool IsValid => Enum.IsDefined(Boundary) &&
        OperationalText.IsUsableIdentifier(StationId) &&
        OperationalText.IsUsableIdentifier(SettingStateCode) &&
        EsdAdjustmentValue >= 0 &&
        OperationalText.IsUsableIdentifier(EffectiveEvidenceReference);
}

public sealed class ExportOperationalObservation : IControlledPilotBoundaryObservation
{
    public ExportOperationalObservation(
        string snapshotId,
        string intendedRenderer,
        string deterministicFileName,
        string sourceChecksum,
        string artifactFormat,
        string artifactMetadataFingerprint,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        SnapshotId = OperationalText.SafeIdentifier(snapshotId, "snapshot-unavailable");
        IntendedRenderer = OperationalText.SafeIdentifier(intendedRenderer,
            "renderer-unavailable");
        DeterministicFileName = FingerprintSafety.SafeFileName(deterministicFileName);
        SourceChecksum = FingerprintSafety.SafeSha256(sourceChecksum);
        ArtifactFormat = OperationalText.SafeIdentifier(artifactFormat, "format-unavailable");
        ArtifactMetadataFingerprint = FingerprintSafety.SafeSha256(
            artifactMetadataFingerprint);
        Boundary = boundary;
    }

    public string SnapshotId { get; }
    public string IntendedRenderer { get; }
    public string DeterministicFileName { get; }
    public string SourceChecksum { get; }
    public string ArtifactFormat { get; }
    public string ArtifactMetadataFingerprint { get; }
    public OperationalObservationBoundary Boundary { get; }
    public bool GeneratesArtifact => false;
    public bool OverwritesArtifact => false;
    public bool MutatesAuthoritativeReport => false;
    public bool IsValid => Enum.IsDefined(Boundary) &&
        OperationalText.IsUsableIdentifier(SnapshotId) &&
        OperationalText.IsUsableIdentifier(IntendedRenderer) &&
        DeterministicFileName != "artifact-unavailable" &&
        SourceChecksum.Any(character => character != '0') &&
        OperationalText.IsUsableIdentifier(ArtifactFormat) &&
        ArtifactMetadataFingerprint.Any(character => character != '0');
}

public abstract class Sha256FingerprintSpecification<TObservation> :
    IControlledPilotFingerprintSpecification<TObservation>
{
    public abstract PilotValidationWorkflow Workflow { get; }
    public abstract string Version { get; }
    public string Algorithm => "SHA-256";

    public string CreateFingerprint(TObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var canonical = new CanonicalFingerprintWriter();
        canonical.Add("spec", Version);
        Write(canonical, observation);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    protected abstract void Write(CanonicalFingerprintWriter writer, TObservation observation);
}

public sealed class AuthenticationFingerprintSpecification :
    Sha256FingerprintSpecification<AuthenticationOperationalObservation>
{
    public override PilotValidationWorkflow Workflow => PilotValidationWorkflow.Authentication;
    public override string Version => "auth-fingerprint-v1";

    protected override void Write(CanonicalFingerprintWriter writer,
        AuthenticationOperationalObservation value)
    {
        writer.Add("station", value.StationScopeId);
        writer.Add("available", value.CapabilityAvailable);
        writer.Add("shift-profile", value.IdentifiesShiftProfile);
        writer.Add("personnel-number", value.AcceptsPersonnelNumber);
        writer.Add("station-scope", value.EnforcesStationScope);
        writer.AddCollection("capability", value.CapabilityCodes);
    }
}

public sealed class ReportingFingerprintSpecification :
    Sha256FingerprintSpecification<ReportingOperationalObservation>
{
    public override PilotValidationWorkflow Workflow => PilotValidationWorkflow.Reporting;
    public override string Version => "reporting-fingerprint-v1";

    protected override void Write(CanonicalFingerprintWriter writer,
        ReportingOperationalObservation value)
    {
        writer.Add("station", value.StationId);
        writer.Add("period", value.PeriodIdentity);
        writer.Add("period-start", value.PeriodStartMinute);
        writer.Add("period-end", value.PeriodEndMinute);
        foreach (ReportingSummaryObservation item in value.Summaries)
        {
            writer.Add("summary-id", item.ParameterId);
            writer.Add("summary-aggregation", item.AggregationCode);
            writer.Add("summary-value", item.Value);
            writer.Add("summary-count", item.ContributingCount);
        }
        foreach (ReportingChartPointObservation item in value.ChartPoints)
        {
            writer.Add("chart-series", item.SeriesId);
            writer.Add("chart-point", item.PointIdentity);
            writer.Add("chart-value", item.Value);
        }
        foreach (ReportingDailyStatusObservation item in value.DailyStatuses)
        {
            writer.Add("daily-date", item.DateIdentity);
            writer.Add("daily-status", item.StatusCode);
            writer.Add("daily-expected", item.ExpectedRecordCount);
            writer.Add("daily-actual", item.ActualRecordCount);
        }
        writer.AddCollection("warning", value.WarningCodes);
        writer.Add("snapshot-id", value.FinalizedSnapshotId ?? "none");
        writer.Add("snapshot-checksum", value.FinalizedSnapshotChecksum ?? "none");
    }
}

public sealed class RuntimeEventFingerprintSpecification :
    Sha256FingerprintSpecification<RuntimeEventOperationalObservation>
{
    public override PilotValidationWorkflow Workflow => PilotValidationWorkflow.RuntimeEvent;
    public override string Version => "runtime-event-fingerprint-v1";

    protected override void Write(CanonicalFingerprintWriter writer,
        RuntimeEventOperationalObservation value)
    {
        writer.Add("station", value.StationId);
        writer.Add("period-start", value.PeriodStartMinute);
        writer.Add("period-end", value.PeriodEndMinute);
        foreach (RuntimeUnitOperationalObservation unit in value.Units)
        {
            writer.Add("unit", unit.UnitId);
            foreach (RuntimeEventItemObservation item in unit.AuthoritativeEvents)
            {
                writer.Add("event-id", item.EventId);
                writer.Add("event-type", item.EventType.ToCode());
                writer.Add("event-minute", item.EventMinute);
                writer.Add("event-sequence", item.Sequence);
            }
            writer.Add("physical-runtime", unit.PhysicalRuntimeMinutes);
            writer.Add("esd-adjustment", unit.EsdAdjustmentMinutes);
            writer.Add("adjusted-runtime", unit.AdjustedRuntimeMinutes);
            writer.Add("runtime-after-oh", unit.RuntimeAfterOhMinutes);
            writer.Add("state", unit.State.ToString());
            writer.Add("service-days", unit.ServiceDayCount);
            writer.Add("longest-run", unit.LongestRunMinutes);
            writer.Add("cumulative-runtime", unit.CumulativeRuntimeMinutes);
            writer.Add("trusted-baseline", unit.TrustedBaselineReference);
        }
    }
}

public sealed class ProtectedSettingsFingerprintSpecification :
    Sha256FingerprintSpecification<ProtectedSettingsOperationalObservation>
{
    public override PilotValidationWorkflow Workflow => PilotValidationWorkflow.ProtectedSettings;
    public override string Version => "protected-settings-fingerprint-v1";

    protected override void Write(CanonicalFingerprintWriter writer,
        ProtectedSettingsOperationalObservation value)
    {
        writer.Add("station", value.StationId);
        writer.Add("state", value.SettingStateCode);
        writer.Add("esd-adjustment", value.EsdAdjustmentValue);
        writer.Add("effective-evidence", value.EffectiveEvidenceReference);
        writer.Add("management-protection", value.ManagementProtectionRequired);
        writer.Add("vendor-authorization", value.ExternalVendorAuthorizationRequired);
    }
}

public sealed class ExportFingerprintSpecification :
    Sha256FingerprintSpecification<ExportOperationalObservation>
{
    public override PilotValidationWorkflow Workflow => PilotValidationWorkflow.Export;
    public override string Version => "export-fingerprint-v1";

    protected override void Write(CanonicalFingerprintWriter writer,
        ExportOperationalObservation value)
    {
        writer.Add("snapshot", value.SnapshotId);
        writer.Add("renderer", value.IntendedRenderer);
        writer.Add("filename", value.DeterministicFileName);
        writer.Add("source-checksum", value.SourceChecksum);
        writer.Add("format", value.ArtifactFormat);
        writer.Add("metadata-fingerprint", value.ArtifactMetadataFingerprint);
    }
}

public sealed class CanonicalFingerprintWriter
{
    private readonly StringBuilder _builder = new();

    public void Add(string key, string value)
    {
        string normalizedKey = key.Normalize(NormalizationForm.FormC);
        string normalizedValue = value.Normalize(NormalizationForm.FormC);
        _builder.Append(normalizedKey.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(normalizedKey).Append('=')
            .Append(normalizedValue.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(normalizedValue).Append(';');
    }

    public void Add(string key, bool value) => Add(key, value ? "1" : "0");
    public void Add(string key, int value) => Add(key, value.ToString(CultureInfo.InvariantCulture));
    public void Add(string key, long value) => Add(key, value.ToString(CultureInfo.InvariantCulture));
    public void Add(string key, decimal value) => Add(key, value.ToString("G29", CultureInfo.InvariantCulture));

    public void AddCollection(string key, IEnumerable<string> values)
    {
        foreach (string value in values)
            Add(key, value);
    }

    public override string ToString() => _builder.ToString();
}

internal static class OperationalCollections
{
    public static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());

    public static IReadOnlyList<string> SafeSortedIdentifiers(IEnumerable<string> values) =>
        ReadOnly(values.Select(value => OperationalText.SafeIdentifier(value,
                "value-unavailable"))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}

internal static class FingerprintSafety
{
    public static string SafeSha256(string? value) => value is not null && value.Length == 64 &&
        value.All(Uri.IsHexDigit) ? value.ToUpperInvariant() : new string('0', 64);

    public static string SafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains('/') || value.Contains('\\') || value is "." or "..")
            return "artifact-unavailable";
        return value.Normalize(NormalizationForm.FormC);
    }
}
