using System.Security.Cryptography;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Pilot;
using Rah_Negar.Qualification;

namespace Rah_Negar.Tests.Pilot;

public sealed class QualificationReportingPilotRegressionTests
{
    private static readonly DateTimeOffset At =
        ControlledPilotOperationalFixture.WindowStart.AddMinutes(10);

    [Theory]
    [InlineData("Rasht", "station-rasht")]
    [InlineData("Ramsar", "station-ramsar")]
    public async Task Qualification_reporting_observation_is_valid_deterministic_safe_and_non_mutating(
        string station, string stationId)
    {
        string root = TemporaryRoot();
        try
        {
            QualificationEnvironment.Prepare(root);
            string path = Path.Combine(root, station, "db.sys");
            string before = await FileDigestAsync(path);
            (LiveSqlitePilotReadModels models, LivePilotReadOnlyPreflightResult preflight) =
                await CreateModelsAsync(path);
            var observer = new LiveReportingPilotObserver(models);
            LivePilotObservationPair<ReportingOperationalObservation> source =
                await ((ILiveReportingPilotReadModel)models).ReadAsync();

            ControlledPilotOperationalWorkflowResult? first = await observer.ObserveAsync(
                QualificationFixture(station).Context(), At);
            ControlledPilotOperationalWorkflowResult? second = await observer.ObserveAsync(
                QualificationFixture(station).Context(), At);

            Assert.True(preflight.IsReady);
            Assert.Equal(stationId, preflight.Scope!.StationId);
            Assert.True(source.Legacy.IsValid);
            Assert.True(source.Target.IsValid);
            Assert.Null(source.Legacy.FinalizedSnapshotId);
            Assert.Null(source.Legacy.FinalizedSnapshotChecksum);
            Assert.Null(source.Target.FinalizedSnapshotId);
            Assert.Null(source.Target.FinalizedSnapshotChecksum);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(OperationalWorkflowComparisonStatus.Failed, first.Status);
            Assert.Equal(first.Status, second.Status);
            Assert.Equal(first.LegacyFingerprint, second.LegacyFingerprint);
            Assert.Equal(first.TargetFingerprint, second.TargetFingerprint);
            Assert.Equal(first.EvidenceReference, second.EvidenceReference);
            Assert.Equal("reporting-fingerprint-v1", first.FingerprintSpecificationVersion);
            Assert.Equal("live-reporting-evidence", first.EvidenceReference);
            Assert.Equal(64, first.LegacyFingerprint.Length);
            Assert.Equal(64, first.TargetFingerprint.Length);
            Assert.All(first.LegacyFingerprint, character => Assert.True(Uri.IsHexDigit(character)));
            Assert.All(first.TargetFingerprint, character => Assert.True(Uri.IsHexDigit(character)));
            Assert.True(first.LegacyRemainsAuthoritative);
            Assert.False(first.MutatedProduction);
            Assert.False(first.ContainsRawRows);
            Assert.False(first.ContainsSql);
            string exposedEvidence = string.Join('|', first.EvidenceReference,
                first.FingerprintSpecificationVersion, first.LegacyFingerprint,
                first.TargetFingerprint);
            foreach (string forbidden in new[]
                     {
                         "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "PRAGMA ",
                         "tbl_data", "tbl_unique", "connection-string", "password",
                         QualificationEnvironment.LoginPassword, "qualification"
                     })
                Assert.DoesNotContain(forbidden, exposedEvidence,
                    StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await FileDigestAsync(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reporting_observation_does_not_abort_valid_Rasht_qualification_run()
    {
        string root = TemporaryRoot();
        try
        {
            QualificationEnvironment.Prepare(root);
            string path = Path.Combine(root, "Rasht", "db.sys");
            string before = await FileDigestAsync(path);
            (LiveSqlitePilotReadModels models, LivePilotReadOnlyPreflightResult preflight) =
                await CreateModelsAsync(path);
            ControlledPilotOperationalFixture fixture =
                ControlledPilotOperationalFixture.Rasht();
            IControlledPilotOperationalWorkflowObserver[] observers = fixture.Observers()
                .Select(observer => observer.Workflow switch
                {
                    PilotValidationWorkflow.Authentication =>
                        (IControlledPilotOperationalWorkflowObserver)
                            new LiveAuthenticationPilotObserver(models),
                    PilotValidationWorkflow.Reporting =>
                        new LiveReportingPilotObserver(models),
                    _ => observer
                })
                .ToArray();
            using var coordinator = fixture.Coordinator(
                allowedDifferences: observers.Length, observers: observers);
            using var session = new LivePilotOperatorSession(coordinator, preflight,
                new ReportingQualificationTimeProvider(At));

            LivePilotDashboardView view = await session.StartObservationAsync();

            Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired,
                session.Lifecycle);
            Assert.False(session.IsTerminal);
            Assert.Equal(5, view.Workflows.Count);
            Assert.Equal("reporting-fingerprint-v1", view.Workflows.Single(item =>
                item.Workflow == PilotValidationWorkflow.Reporting)
                .FingerprintSpecificationVersion);
            Assert.Equal(before, await FileDigestAsync(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Reporting_observation_still_rejects_malformed_finalized_checksum()
    {
        var observation = new ReportingOperationalObservation("station-rasht", "1405-01",
            1, 2, [new ReportingSummaryObservation("in_p", "average", 10m, 1)],
            [], [], [], "snapshot-1", "not-a-checksum");

        Assert.False(observation.IsValid);
        Assert.NotNull(observation.FinalizedSnapshotChecksum);
        Assert.All(observation.FinalizedSnapshotChecksum, character => Assert.Equal('0', character));
    }

    private static async Task<(LiveSqlitePilotReadModels Models,
        LivePilotReadOnlyPreflightResult Preflight)> CreateModelsAsync(string path)
    {
        var connections = new PilotReadOnlySqliteConnectionFactory(path);
        LivePilotReadOnlyPreflightResult preflight =
            await new LivePilotReadOnlyPreflight(connections).EvaluateAsync(At);
        Assert.True(preflight.IsReady);
        var models = new LiveSqlitePilotReadModels(connections, preflight.Scope!,
            new RuntimeCalculator(), new ReportCalculator(),
            new DeterministicReportFileNamePolicy(),
            new ReportingQualificationTimeProvider(At));
        return (models, preflight);
    }

    private static ControlledPilotOperationalFixture QualificationFixture(string station) =>
        station == "Rasht" ? ControlledPilotOperationalFixture.Rasht() :
            ControlledPilotOperationalFixture.Ramsar();

    private static string TemporaryRoot() => Path.Combine(Path.GetTempPath(),
        "rah-negar-reporting-qualification", Guid.NewGuid().ToString("N"));

    private static async Task<string> FileDigestAsync(string path) =>
        Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
}

file sealed class ReportingQualificationTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
