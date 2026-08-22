using Rah_Negar.Core.Event;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Foundation.Application.Reporting.Finalization;

namespace Rah_Negar.Tests.Reporting;

public sealed class ReportSnapshotDomainContractsTests
{
    private readonly IReportFinalizationValidator _validator = new ReportFinalizationValidator();
    private readonly IReportSnapshotFactory _factory = new ReportSnapshotFactory();

    [Fact]
    public void ValidProjection_ConstructsSnapshotAndCapturesIdentity()
    {
        ReportFinalizationRequest request = Request(Projection());
        FinalizationValidationResult validation = _validator.Validate(request);
        ReportFinalizationResult result = _factory.Create(request, validation);

        Assert.True(validation.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal("snapshot-1", result.Snapshot!.Identity.SnapshotId);
        Assert.Equal("report-1", result.Snapshot.Identity.ReportId);
        Assert.Equal(["unit-1", "unit-2"], result.Snapshot.Identity.UnitIds);
        Assert.Equal(1, result.Snapshot.Identity.SnapshotSequence);
        Assert.Equal(SnapshotChecksumState.Pending, result.Snapshot.Checksum.State);
        Assert.Equal("SHA-256", result.Snapshot.Checksum.Algorithm);
    }

    [Fact]
    public void SnapshotCollections_AreImmutableAndDetached()
    {
        ReportProjection projection = Projection();
        FinalizedReportSnapshot snapshot = Create(projection);

        Assert.Throws<NotSupportedException>(() => ((IList<ReportEvent>)snapshot.EventLog)
            .Add(new("new", "unit-1", EventType.Start, 150, 1)));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)snapshot.Versions.EventChainVersions)
            .Add("unit-3", "chain-v1"));
        Assert.NotSame(projection.Identity, snapshot.ReportIdentity);
    }

    [Fact]
    public void Snapshot_PreservesEvidenceAndVersionsExactly()
    {
        FinalizedReportSnapshot snapshot = Create(Projection());

        Assert.Equal("source-v1", snapshot.Evidence.SourceEvidence.SourceRevision);
        Assert.Equal("source-v1", snapshot.Evidence.VerifiedSourceRevision);
        Assert.Equal("hourly-v1", snapshot.Evidence.SourceEvidence.HourlyRevision);
        Assert.Equal(2, snapshot.Evidence.SourceEvidence.HourlyRecordCount);
        Assert.Equal("actor-1", snapshot.Evidence.ActorIdentity);
        Assert.Equal("finalization-policy-v1", snapshot.Evidence.FinalizationPolicyVersion);
        Assert.Equal("event-chain-v1", snapshot.Versions.EventChainVersions["unit-2"]);
        Assert.Equal("baseline-v1", snapshot.Versions.RuntimeBaselineVersions["unit-1"]);
    }

    [Fact]
    public void Snapshot_UsesDeterministicOrdering()
    {
        FinalizedReportSnapshot first = Create(Projection(reverseInputs: true));
        FinalizedReportSnapshot second = Create(Projection(reverseInputs: false));

        Assert.Equal(["unit-1", "unit-2"], first.RuntimeSummaries.Select(x => x.UnitId));
        Assert.Equal(["event-1", "event-2"], first.EventLog.Select(x => x.EventId));
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.RuntimeSummaries, second.RuntimeSummaries);
        Assert.Equal(first.OperationalSummaries, second.OperationalSummaries);
    }

    [Fact]
    public void MissingEvidence_IsRejected()
    {
        ReportProjection projection = Projection(evidence: Evidence(hourlyRevision: ""));
        FinalizationValidationResult result = _validator.Validate(Request(projection));

        Assert.False(result.IsValid);
        Assert.Equal(ReportFinalizationOutcome.ValidationRejected, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "evidence.hourly-revision.missing");
    }

    [Fact]
    public void MissingVersion_IsRejectedAsVersionFailure()
    {
        ReportProjection projection = Projection(versions: Versions(runtimePolicy: ""));
        FinalizationValidationResult result = _validator.Validate(Request(projection));

        Assert.False(result.IsValid);
        Assert.Equal(ReportFinalizationOutcome.VersionRejected, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "version.runtime-policy.missing");
    }

    [Fact]
    public void InvalidCompleteness_IsRejectedWithoutSnapshot()
    {
        ReportProjection projection = Projection(completeness: Completeness(CompletenessState.Incomplete));
        ReportFinalizationRequest request = Request(projection);
        FinalizationValidationResult validation = _validator.Validate(request);
        ReportFinalizationResult result = _factory.Create(request, validation);

        Assert.Equal(ReportFinalizationOutcome.IncompleteRejected, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void IdentityMismatch_IsRejected()
    {
        FinalizationValidationResult result = _validator.Validate(Request(Projection(), stationId: "ramsar"));

        Assert.Equal(ReportFinalizationOutcome.ValidationRejected, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "identity.station.mismatch");
    }

    [Fact]
    public void ChangedSourceRevision_IsRejected()
    {
        FinalizationValidationResult result = _validator.Validate(Request(Projection(), verifiedRevision: "source-v2"));

        Assert.Equal(ReportFinalizationOutcome.SourceChangedRejected, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "source.changed");
    }

    private FinalizedReportSnapshot Create(ReportProjection projection)
    {
        ReportFinalizationRequest request = Request(projection);
        return _factory.Create(request, _validator.Validate(request)).Snapshot!;
    }

    private static ReportFinalizationRequest Request(ReportProjection projection,
        string stationId = "rasht", string verifiedRevision = "source-v1") => new(
        "finalization-1", "snapshot-1", projection, stationId, 100, 200,
        ["unit-2", "unit-1"], "source-v1", verifiedRevision, 1, null, "actor-1",
        new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
        "finalization-policy-v1", "snapshot-integrity-v1");

    private static ReportProjection Projection(bool reverseInputs = true,
        ReportCompletenessResult? completeness = null, ReportEvidence? evidence = null,
        ReportVersionSet? versions = null)
    {
        var identity = new ReportIdentity("report-1", "rasht", "Rasht", 100, 200,
            "1405/05", ReportPeriodKind.Monthly, ["unit-2", "unit-1"], ReportSourceMode.OpenProjection);
        ReportEvent[] reportEvents =
        [
            new("event-2", "unit-2", EventType.Nsd, 160, 2),
            new("event-1", "unit-1", EventType.Start, 110, 1)
        ];
        AuthoritativeEventInput[] events =
        [
            EventInput("unit-2", [reportEvents[0]]),
            EventInput("unit-1", [reportEvents[1]])
        ];
        AuthoritativeRuntimeInput[] runtimes =
        [
            RuntimeInput("unit-2", 60),
            RuntimeInput("unit-1", 50)
        ];
        if (!reverseInputs)
        {
            Array.Reverse(events);
            Array.Reverse(runtimes);
        }
        var input = new NormalizedReportInput(identity,
            new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.FromHours(3.5)),
            [new("zeta", "Zeta", "u", ReportAggregationType.Maximum, 2),
             new("alpha", "Alpha", "u", ReportAggregationType.Average, 1)],
            [new("h-2", "zeta", "1405/05/02", 120, 20),
             new("h-1", "alpha", "1405/05/01", 110, 10)],
            [], events, runtimes, completeness ?? Completeness(CompletenessState.Complete),
            evidence ?? Evidence(), versions ?? Versions());
        return new ReportCalculator().Calculate(input);
    }

    private static AuthoritativeEventInput EventInput(string unit, IReadOnlyList<ReportEvent> events) =>
        new($"chain-{unit}", "event-chain-v1", "rasht", unit, 100, 200, true, events);
    private static AuthoritativeRuntimeInput RuntimeInput(string unit, long physical) =>
        new($"runtime-{unit}", "rasht", unit, 100, 200, physical, 5, physical + 5,
            20, 30, 1, UnitOperationalState.Stopped, "runtime-calc-v1", "runtime-policy-v1",
            "baseline-v1", "config-v1");
    private static ReportEvidence Evidence(string hourlyRevision = "hourly-v1") =>
        new("source-v1", hourlyRevision, 2, "daily-v1", 0, "profile-v1", 0,
            "calendar-v1", "ordering-v1");
    private static ReportCompletenessResult Completeness(CompletenessState state) => new(
        Enum.GetValues<CompletenessDimension>().Select((dimension, index) =>
            new CompletenessDimensionResult(dimension, index == 0 ? state : CompletenessState.Complete,
                index == 0 && state != CompletenessState.Complete
                    ? [new CompletenessIssue("hourly.missing", "Missing hourly data.")] : [])));
    private static ReportVersionSet Versions(string runtimePolicy = "runtime-policy-v1") => new(
        "report-calc-v1", "report-policy-v1", "profile-v1", "snapshot-v1", "event-policy-v1",
        "runtime-calc-v1", runtimePolicy, "calendar-v1",
        new Dictionary<string, string> { ["unit-2"] = "event-chain-v1", ["unit-1"] = "event-chain-v1" },
        new Dictionary<string, string> { ["unit-2"] = "baseline-v1", ["unit-1"] = "baseline-v1" },
        new Dictionary<string, string> { ["unit-2"] = "config-v1", ["unit-1"] = "config-v1" });
}
