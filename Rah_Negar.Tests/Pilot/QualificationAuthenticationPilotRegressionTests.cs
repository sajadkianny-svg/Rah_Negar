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

public sealed class QualificationAuthenticationPilotRegressionTests
{
    private static readonly DateTimeOffset At =
        ControlledPilotOperationalFixture.WindowStart.AddMinutes(10);

    [Theory]
    [InlineData("Rasht", "station-rasht")]
    [InlineData("Ramsar", "station-ramsar")]
    public async Task Qualification_authentication_observation_is_safe_deterministic_and_non_mutating(
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
            var observer = new LiveAuthenticationPilotObserver(models);
            LivePilotObservationPair<AuthenticationOperationalObservation> source =
                await ((ILiveAuthenticationPilotReadModel)models).ReadAsync();

            ControlledPilotOperationalWorkflowResult? first = await observer.ObserveAsync(
                QualificationFixture(station).Context(), At);
            ControlledPilotOperationalWorkflowResult? second = await observer.ObserveAsync(
                QualificationFixture(station).Context(), At);

            Assert.True(preflight.IsReady);
            Assert.Equal(stationId, preflight.Scope!.StationId);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(OperationalWorkflowComparisonStatus.Failed, first.Status);
            Assert.Equal(first.Status, second.Status);
            Assert.Equal(first.LegacyFingerprint, second.LegacyFingerprint);
            Assert.Equal(first.TargetFingerprint, second.TargetFingerprint);
            Assert.Equal("auth-fingerprint-v1", first.FingerprintSpecificationVersion);
            Assert.Equal("live-authentication-evidence", first.EvidenceReference);
            Assert.True(first.LegacyRemainsAuthoritative);
            Assert.False(first.MutatedProduction);
            Assert.False(first.ContainsRawRows);
            Assert.False(first.ContainsSql);
            Assert.False(source.Legacy.AcceptsPassword);
            Assert.False(source.Target.AcceptsPassword);
            Assert.False(source.Legacy.ContainsCredentialHash);
            Assert.False(source.Target.ContainsCredentialHash);
            string exposedEvidence = string.Join('|', new[]
                {
                    first.EvidenceReference, first.FingerprintSpecificationVersion,
                    first.LegacyFingerprint, first.TargetFingerprint
                }
                .Concat(source.Legacy.CapabilityCodes)
                .Concat(source.Target.CapabilityCodes));
            foreach (string forbidden in new[]
                     {
                         "password", "credential", "hash", "recovery",
                         QualificationEnvironment.LoginPassword, "01020304", "05060708"
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
    public async Task Authentication_observation_does_not_abort_valid_Rasht_qualification_run()
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
                .Select(observer => observer.Workflow == PilotValidationWorkflow.Authentication
                    ? (IControlledPilotOperationalWorkflowObserver)
                        new LiveAuthenticationPilotObserver(models)
                    : observer)
                .ToArray();
            using var coordinator = fixture.Coordinator(
                allowedDifferences: observers.Length, observers: observers);
            using var session = new LivePilotOperatorSession(coordinator, preflight,
                new FixedQualificationTimeProvider(At));

            LivePilotDashboardView view = await session.StartObservationAsync();

            Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired,
                session.Lifecycle);
            Assert.False(session.IsTerminal);
            Assert.Equal(5, view.Workflows.Count);
            Assert.True(view.IsReadOnly);
            Assert.False(view.CanSwitchAuthority);
            Assert.Equal("auth-fingerprint-v1", view.Workflows.Single(item =>
                item.Workflow == PilotValidationWorkflow.Authentication)
                .FingerprintSpecificationVersion);
            Assert.Equal(before, await FileDigestAsync(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(LiveSqlitePilotReadModels Models,
        LivePilotReadOnlyPreflightResult Preflight)> CreateModelsAsync(string path)
    {
        var connections = new PilotReadOnlySqliteConnectionFactory(path);
        var preflightService = new LivePilotReadOnlyPreflight(connections);
        LivePilotReadOnlyPreflightResult preflight = await preflightService.EvaluateAsync(At);
        Assert.True(preflight.IsReady);
        var models = new LiveSqlitePilotReadModels(connections, preflight.Scope!,
            new RuntimeCalculator(), new ReportCalculator(),
            new DeterministicReportFileNamePolicy(),
            new FixedQualificationTimeProvider(At));
        return (models, preflight);
    }

    private static ControlledPilotOperationalFixture QualificationFixture(string station) =>
        station == "Rasht" ? ControlledPilotOperationalFixture.Rasht() :
            ControlledPilotOperationalFixture.Ramsar();

    private static string TemporaryRoot() => Path.Combine(Path.GetTempPath(),
        "rah-negar-authentication-qualification", Guid.NewGuid().ToString("N"));

    private static async Task<string> FileDigestAsync(string path) =>
        Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
}

file sealed class FixedQualificationTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
