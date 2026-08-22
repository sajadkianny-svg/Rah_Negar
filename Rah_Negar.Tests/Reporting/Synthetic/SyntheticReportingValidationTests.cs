using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Foundation.Application.Reporting.Input;

namespace Rah_Negar.Tests.Reporting.Synthetic;

public sealed class SyntheticReportingValidationTests
{
    private readonly SyntheticReportingFixture _fixture = new();

    [Fact]
    public async Task ScenarioA_FullyValidReport_ProducesCompleteProjection()
    {
        SyntheticPipelineResult result = await _fixture.RunAsync(SyntheticReportingScenario.Complete);

        Assert.True(result.Composition.IsSuccess);
        Assert.NotNull(result.Projection);
        Assert.Equal(ReportProjectionStatus.Complete, result.Projection.Status);
        Assert.Equal(CompletenessState.Complete, result.Projection.Completeness.State);
        Assert.Equal(105.5m, Assert.Single(result.Projection.OperationalSummaries).Value);
        Assert.Equal(10m, Assert.Single(result.Projection.DailySummaries).Sum);
        Assert.Equal(2, result.Projection.RuntimeSummaries.Count);
        Assert.Equal(4, result.Projection.EventLog.Count);
    }

    [Fact]
    public async Task ScenarioB_MissingHourlyData_ProducesIncompleteProjection()
    {
        SyntheticPipelineResult result = await _fixture.RunAsync(SyntheticReportingScenario.MissingHourlyData);

        Assert.True(result.Composition.IsSuccess);
        Assert.Equal(ReportProjectionStatus.Incomplete, result.Projection!.Status);
        Assert.False(result.Projection.Completeness.IsFinalizationEligible);
        Assert.Contains("hourly.slot.missing", result.Projection.Warnings);
    }

    [Fact]
    public async Task ScenarioC_VersionMismatch_ProducesRejectedProjection()
    {
        SyntheticPipelineResult result = await _fixture.RunAsync(SyntheticReportingScenario.VersionMismatch);

        Assert.True(result.Composition.IsSuccess);
        Assert.Equal(ReportProjectionStatus.Rejected, result.Projection!.Status);
        Assert.Contains("version.runtime-baseline.missing:unit-1", result.Projection.BlockingReasons);
    }

    [Fact]
    public async Task ScenarioD_InvalidUnitEventAlignment_FailsComposition()
    {
        SyntheticPipelineResult result = await _fixture.RunAsync(SyntheticReportingScenario.InvalidEventUnitAlignment);

        Assert.False(result.Composition.IsSuccess);
        Assert.Null(result.Projection);
        Assert.Contains(result.Composition.Failures, x => x.Kind == ReportingInputFailureKind.MissingUnit &&
            x.Source == "event" && x.UnitId == "unit-1");
        Assert.Contains(result.Composition.Failures, x => x.Code == "reporting.input.unit.unexpected" &&
            x.UnitId == "unit-x");
    }

    [Fact]
    public async Task Evidence_IsPreservedAcrossCompleteSyntheticPipeline()
    {
        SyntheticPipelineResult result = await _fixture.RunAsync(SyntheticReportingScenario.Complete);
        ReportProjection projection = result.Projection!;

        Assert.Equal("synthetic-read-revision-v1", projection.Evidence.SourceRevision);
        Assert.Equal("synthetic-read-revision-v1", projection.Evidence.HourlyRevision);
        Assert.Equal("synthetic-read-revision-v1", projection.Evidence.DailyRevision);
        Assert.Equal("synthetic-profile", projection.Evidence.StationProfileIdentity);
        Assert.Equal(12, projection.Evidence.HourlyRecordCount);
        Assert.Equal(2, projection.Evidence.DailyRecordCount);
        Assert.Equal("event-chain-v1", projection.Versions.EventChainVersions["unit-2"]);
        Assert.Equal("runtime-config-v1", projection.Versions.RuntimeConfigurationVersions["unit-1"]);
    }

    [Fact]
    public async Task ScenarioE_RepeatedCalculation_ProducesDeterministicallyIdenticalResult()
    {
        SyntheticPipelineResult first = await _fixture.RunAsync(SyntheticReportingScenario.Complete);
        SyntheticPipelineResult second = await _fixture.RunAsync(SyntheticReportingScenario.Complete);

        Assert.Equal(SyntheticReportingFixture.Fingerprint(first.Projection!),
            SyntheticReportingFixture.Fingerprint(second.Projection!));
        Assert.Equal(first.Projection!.EventLog, second.Projection!.EventLog);
        Assert.Equal(first.Projection.RuntimeSummaries, second.Projection.RuntimeSummaries);
    }
}
