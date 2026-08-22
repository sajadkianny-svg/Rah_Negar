using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Reporting;

public sealed class ReportFinalizationApplicationServiceTests
{
    [Fact]
    public async Task SuccessfulFinalization_ReturnsCommittedSnapshotAndLockRevision()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.Committed, "snapshot-1", 1, []));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.Complete), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.Succeeded, result.Status);
        Assert.Equal("snapshot-1", result.SnapshotId);
        Assert.Equal(1, result.LockRevision);
        Assert.Equal(1, atomic.CallCount);
    }

    [Fact]
    public async Task IncompleteProjection_IsRejectedBeforeAtomicPersistence()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.Committed, "unexpected", 1, []));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.MissingHourlyData), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.IncompleteRejected, result.Status);
        Assert.Equal(0, atomic.CallCount);
        Assert.Contains(result.Errors, x => x.Code == "completeness.not-eligible");
    }

    [Fact]
    public async Task MissingVersion_IsMappedToVersionRejection()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.Committed, "unexpected", 1, []));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.VersionMismatch), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.VersionRejected, result.Status);
        Assert.Equal(0, atomic.CallCount);
        Assert.Contains(result.Errors, x => x.Code == "version.runtime-baseline.missing:unit-1");
    }

    [Fact]
    public async Task IdempotentRetry_IsReportedAsAlreadyFinalized()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.IdempotentReplay, "snapshot-1", 1, []));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.Complete), Context());

        Assert.True(result.IsSuccess);
        Assert.Equal(ReportFinalizationApplicationStatus.AlreadyFinalized, result.Status);
        Assert.Equal("snapshot-1", result.SnapshotId);
    }

    [Fact]
    public async Task AuthorizationRejection_StopsBeforeReportValidationAndPersistence()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.Committed, "unexpected", 1, []));
        IReportFinalizationService service = Service(new RejectAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.Complete), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.AuthorizationRejected, result.Status);
        Assert.Contains(result.Errors, x => x.Code == "authorization.denied");
        Assert.Equal(0, atomic.CallCount);
    }

    [Fact]
    public async Task AtomicInfrastructureFailure_IsPropagatedAsApplicationFailure()
    {
        var atomic = new FakeAtomic(new(AtomicFinalizationOutcome.InfrastructureFailed, null, null,
            ["report.finalization.infrastructure-failure"]));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.Complete), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.InfrastructureFailed, result.Status);
        Assert.Contains(result.Errors, x => x.Code == "report.finalization.infrastructure-failure");
    }

    [Theory]
    [InlineData(AtomicFinalizationOutcome.SnapshotConflict)]
    [InlineData(AtomicFinalizationOutcome.LockConflict)]
    [InlineData(AtomicFinalizationOutcome.ReceiptConflict)]
    public async Task AtomicConflicts_AreMappedWithoutClaimingSuccess(AtomicFinalizationOutcome outcome)
    {
        var atomic = new FakeAtomic(new(outcome, null, null, ["conflict"]));
        IReportFinalizationService service = Service(new AllowAuthorizer(), atomic);

        ReportFinalizationApplicationResult result = await service.FinalizeAsync(
            await RequestAsync(SyntheticReportingScenario.Complete), Context());

        Assert.Equal(ReportFinalizationApplicationStatus.Conflict, result.Status);
        Assert.False(result.IsSuccess);
    }

    private static IReportFinalizationService Service(IReportFinalizationAuthorizer authorizer,
        IAtomicReportFinalizationService atomic) => new ReportFinalizationApplicationService(
            authorizer, new ReportFinalizationValidator(), new ReportSnapshotFactory(), atomic);

    private static ReportFinalizationContext Context() =>
        new("correlation-1", "synthetic-actor", expectedLockRevision: 0);

    private static async Task<ReportFinalizationRequest> RequestAsync(SyntheticReportingScenario scenario)
    {
        SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(scenario);
        return new("finalization-1", "snapshot-1", pipeline.Projection!, "rasht", 10_000, 53_200,
            ["unit-2", "unit-1"], "synthetic-read-revision-v1", "synthetic-read-revision-v1",
            1, null, "synthetic-actor",
            new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
            "finalization-policy-v1", "snapshot-integrity-v1");
    }

    private sealed class AllowAuthorizer : IReportFinalizationAuthorizer
    {
        public Task<ReportFinalizationAuthorizationResult> AuthorizeAsync(ReportFinalizationRequest request,
            ReportFinalizationContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReportFinalizationAuthorizationResult.Authorized());
    }

    private sealed class RejectAuthorizer : IReportFinalizationAuthorizer
    {
        public Task<ReportFinalizationAuthorizationResult> AuthorizeAsync(ReportFinalizationRequest request,
            ReportFinalizationContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReportFinalizationAuthorizationResult.Rejected(
                new ReportFinalizationAuthorizationFailure(
                    "authorization.denied", "The actor is not authorized to finalize reports.")));
    }

    private sealed class FakeAtomic : IAtomicReportFinalizationService
    {
        private readonly AtomicFinalizationResult _result;
        public FakeAtomic(AtomicFinalizationResult result) => _result = result;
        public int CallCount { get; private set; }
        public Task<AtomicFinalizationResult> FinalizeAsync(ReportFinalizationRequest request,
            long expectedLockRevision, string? expectedEffectiveSnapshotId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
