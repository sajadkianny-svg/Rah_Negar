using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Persistence;

namespace Rah_Negar.Infrastructure.Reporting.Snapshot;

public sealed class CanonicalJsonReportSnapshotSerializer : IReportSnapshotSerializer
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Default,
        Converters = { new JsonStringEnumConverter() }
    };

    public SerializedReportSnapshot Serialize(FinalizedReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var payload = Payload.From(snapshot);
        string json = JsonSerializer.Serialize(payload, Options);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var checksum = new SnapshotChecksum("SHA-256", snapshot.Evidence.SnapshotIntegrityVersion,
            SnapshotChecksumState.Calculated, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
        return new(CurrentSchemaVersion, json, checksum);
    }

    public FinalizedReportSnapshot Deserialize(SerializedReportSnapshot serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        if (serialized.SchemaVersion != CurrentSchemaVersion)
            throw new NotSupportedException($"Snapshot payload schema {serialized.SchemaVersion} is not supported.");
        if (serialized.Checksum.State != SnapshotChecksumState.Calculated ||
            !StringComparer.Ordinal.Equals(serialized.Checksum.Algorithm, "SHA-256"))
            throw new InvalidDataException("A calculated SHA-256 checksum is required.");

        byte[] bytes = Encoding.UTF8.GetBytes(serialized.CanonicalJson);
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (serialized.Checksum.CanonicalPayloadLength != bytes.LongLength ||
            !string.Equals(serialized.Checksum.Value, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Snapshot checksum validation failed.");

        Payload payload = JsonSerializer.Deserialize<Payload>(serialized.CanonicalJson, Options)
            ?? throw new InvalidDataException("Snapshot payload is empty.");
        FinalizedReportSnapshot snapshot = payload.ToDomain(serialized.Checksum);
        if (!StringComparer.Ordinal.Equals(Serialize(snapshot).CanonicalJson, serialized.CanonicalJson))
            throw new InvalidDataException("Snapshot payload does not round-trip canonically.");
        return snapshot;
    }

    private sealed record Payload(
        SnapshotIdentityPayload Identity,
        ReportIdentityPayload ReportIdentity,
        CompletenessDimensionPayload[] Completeness,
        EvidencePayload Evidence,
        VersionsPayload Versions,
        OperationalSummary[] OperationalSummaries,
        DailySummary[] DailySummaries,
        RuntimeSummary[] RuntimeSummaries,
        EventSummary[] EventSummaries,
        ReportEvent[] EventLog,
        ServiceSummary[] ServiceSummaries,
        ExtremeDateSummary[] ExtremeDateSummaries,
        string[] Warnings)
    {
        public static Payload From(FinalizedReportSnapshot value) => new(
            SnapshotIdentityPayload.From(value.Identity),
            ReportIdentityPayload.From(value.ReportIdentity),
            value.Completeness.Dimensions.OrderBy(x => x.Dimension).Select(CompletenessDimensionPayload.From).ToArray(),
            EvidencePayload.From(value.Evidence), VersionsPayload.From(value.Versions),
            value.OperationalSummaries.ToArray(),
            value.DailySummaries.Select(x => x with { MissingDates = Array.AsReadOnly(x.MissingDates.ToArray()) }).ToArray(),
            value.RuntimeSummaries.ToArray(), value.EventSummaries.ToArray(), value.EventLog.ToArray(),
            value.ServiceSummaries.ToArray(),
            value.ExtremeDateSummaries.Select(x => x with
            {
                MinimumDates = Array.AsReadOnly(x.MinimumDates.ToArray()),
                MaximumDates = Array.AsReadOnly(x.MaximumDates.ToArray())
            }).ToArray(), value.Warnings.ToArray());

        public FinalizedReportSnapshot ToDomain(SnapshotChecksum checksum)
        {
            ReportSnapshotIdentity identity = Identity.ToDomain();
            ReportIdentity reportIdentity = ReportIdentity.ToDomain();
            var completeness = new ReportCompletenessResult(Completeness.Select(x => x.ToDomain()));
            ReportSnapshotEvidence evidence = Evidence.ToDomain();
            ReportVersionSet versions = Versions.ToDomain();
            return new(identity, reportIdentity, completeness, evidence, versions, checksum,
                OperationalSummaries, DailySummaries, RuntimeSummaries, EventSummaries, EventLog,
                ServiceSummaries, ExtremeDateSummaries, Warnings);
        }
    }

    private sealed record SnapshotIdentityPayload(string SnapshotId, string ReportId, string StationId,
        long PeriodStartMinute, long PeriodEndMinute, ReportPeriodKind PeriodKind, string[] UnitIds,
        int SnapshotSequence, string? SupersedesSnapshotId)
    {
        public static SnapshotIdentityPayload From(ReportSnapshotIdentity x) => new(x.SnapshotId, x.ReportId,
            x.StationId, x.PeriodStartMinute, x.PeriodEndMinute, x.PeriodKind, x.UnitIds.ToArray(),
            x.SnapshotSequence, x.SupersedesSnapshotId);
        public ReportSnapshotIdentity ToDomain() => new(SnapshotId, ReportId, StationId, PeriodStartMinute,
            PeriodEndMinute, PeriodKind, UnitIds, SnapshotSequence, SupersedesSnapshotId);
    }

    private sealed record ReportIdentityPayload(string ReportId, string StationId, string StationName,
        long PeriodStartMinute, long PeriodEndMinute, string PersianPeriodLabel, ReportPeriodKind PeriodKind,
        string[] UnitIds, ReportSourceMode SourceMode)
    {
        public static ReportIdentityPayload From(ReportIdentity x) => new(x.ReportId, x.StationId, x.StationName,
            x.PeriodStartMinute, x.PeriodEndMinute, x.PersianPeriodLabel, x.PeriodKind, x.UnitIds.ToArray(), x.SourceMode);
        public ReportIdentity ToDomain() => new(ReportId, StationId, StationName, PeriodStartMinute,
            PeriodEndMinute, PersianPeriodLabel, PeriodKind, UnitIds, SourceMode);
    }

    private sealed record CompletenessDimensionPayload(CompletenessDimension Dimension, CompletenessState State,
        CompletenessIssue[] Issues)
    {
        public static CompletenessDimensionPayload From(CompletenessDimensionResult x) =>
            new(x.Dimension, x.State, x.Issues.ToArray());
        public CompletenessDimensionResult ToDomain() => new(Dimension, State, Issues);
    }

    private sealed record EvidencePayload(string SourceRevision, string HourlyRevision, int HourlyRecordCount,
        string DailyRevision, int DailyRecordCount, string StationProfileIdentity, long DataStartMinute,
        string CalendarIdentity, string OrderingConvention, string VerifiedSourceRevision,
        string FinalizationId, string ActorIdentity, DateTimeOffset ProjectionCalculatedAt,
        DateTimeOffset FinalizedAt, string FinalizationPolicyVersion, string SnapshotIntegrityVersion)
    {
        public static EvidencePayload From(ReportSnapshotEvidence x) => new(x.SourceEvidence.SourceRevision,
            x.SourceEvidence.HourlyRevision, x.SourceEvidence.HourlyRecordCount, x.SourceEvidence.DailyRevision,
            x.SourceEvidence.DailyRecordCount, x.SourceEvidence.StationProfileIdentity,
            x.SourceEvidence.DataStartMinute, x.SourceEvidence.CalendarIdentity,
            x.SourceEvidence.OrderingConvention, x.VerifiedSourceRevision, x.FinalizationId,
            x.ActorIdentity, x.ProjectionCalculatedAt, x.FinalizedAt, x.FinalizationPolicyVersion,
            x.SnapshotIntegrityVersion);
        public ReportSnapshotEvidence ToDomain() => new(new ReportEvidence(SourceRevision, HourlyRevision,
            HourlyRecordCount, DailyRevision, DailyRecordCount, StationProfileIdentity, DataStartMinute,
            CalendarIdentity, OrderingConvention), VerifiedSourceRevision, FinalizationId, ActorIdentity,
            ProjectionCalculatedAt, FinalizedAt, FinalizationPolicyVersion, SnapshotIntegrityVersion);
    }

    private sealed record VersionsPayload(string ReportCalculationVersion, string ReportPolicyVersion,
        string ReportProfileVersion, string SnapshotFormatVersion, string EventPolicyVersion,
        string RuntimeCalculationVersion, string RuntimePolicyVersion, string CalendarPolicyVersion,
        SortedDictionary<string, string> EventChainVersions,
        SortedDictionary<string, string> RuntimeBaselineVersions,
        SortedDictionary<string, string> RuntimeConfigurationVersions)
    {
        public static VersionsPayload From(ReportVersionSet x) => new(x.ReportCalculationVersion,
            x.ReportPolicyVersion, x.ReportProfileVersion, x.SnapshotFormatVersion, x.EventPolicyVersion,
            x.RuntimeCalculationVersion, x.RuntimePolicyVersion, x.CalendarPolicyVersion,
            Sorted(x.EventChainVersions), Sorted(x.RuntimeBaselineVersions),
            Sorted(x.RuntimeConfigurationVersions));
        public ReportVersionSet ToDomain() => new(ReportCalculationVersion, ReportPolicyVersion,
            ReportProfileVersion, SnapshotFormatVersion, EventPolicyVersion, RuntimeCalculationVersion,
            RuntimePolicyVersion, CalendarPolicyVersion, EventChainVersions, RuntimeBaselineVersions,
            RuntimeConfigurationVersions);

        private static SortedDictionary<string, string> Sorted(IReadOnlyDictionary<string, string> source)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach ((string key, string value) in source) result.Add(key, value);
            return result;
        }
    }
}
