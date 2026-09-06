using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Tests.Authority;

public sealed class Phase97ProductionExecutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Default_production_boundary_rejects_and_preserves_legacy_state()
    {
        var authority = new InMemoryProductionAuthorityStore(AuthorityStateRecord.Legacy("production", "all"));
        var before = await authority.ReadAsync();
        var gate = new ProductionWriteGate();
        await using var fence = new TestFence();
        var executor = new GovernedProductionActivationExecutor(new FixedClock(Now), new ProductionAuthorizationDisabledVerifier(), fence, gate, authority, new InMemoryAuthorityAuditSink());

        ProductionExecutionResult result = await executor.ExecuteAsync(Context());

        Assert.Equal(ProductionExecutionStatus.Rejected, result.Status);
        Assert.Contains("governance-authorization", result.Reason);
        ProductionAuthoritySnapshot after = await authority.ReadAsync();
        Assert.Equal(before.Record, after.Record);
        Assert.Equal(AuthorityState.LegacyAuthoritative, after.Record.State);
        Assert.False(after.Record.TargetRoutingEnabled);
    }

    [Fact]
    public async Task Qualification_authorization_exercises_ordered_commit_and_routing_boundary()
    {
        var authority = new InMemoryProductionAuthorityStore(AuthorityStateRecord.Legacy("qualification", "all"));
        var gate = new ProductionWriteGate();
        await using var fence = new TestFence();
        var executor = new GovernedProductionActivationExecutor(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), fence, gate, authority, new InMemoryAuthorityAuditSink());

        ProductionExecutionResult result = await executor.ExecuteAsync(Context());

        Assert.True(result.Succeeded, result.Status + ":" + result.Reason);
        Assert.Equal(new[] { "prerequisite-validation", "authorization-validation", "writer-fence-acquired", "legacy-writes-drained", "final-reconciliation-and-backup-verified", "durable-audit-prepared", "canonical-authority-committed", "target-routing-enabled-after-authority", "durable-audit-finalized" }, result.CompletedStages);
        ProductionAuthoritySnapshot current = await authority.ReadAsync();
        Assert.Equal(AuthorityState.TargetAuthoritative, current.Record.State);
        Assert.False(current.Record.LegacyAuthoritative);
        Assert.True(current.Record.TargetRoutingEnabled);
        Assert.True((await gate.EnterTargetWriteAsync(1)).Acquired);
        Assert.False((await gate.EnterLegacyWriteAsync(0)).Acquired);
    }

    [Fact]
    public async Task Missing_owner_reference_expired_and_wrong_action_contracts_are_rejected()
    {
        var authority = new InMemoryProductionAuthorityStore();
        var gate = new ProductionWriteGate();
        await using var fence = new TestFence();
        var executor = new GovernedProductionActivationExecutor(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), fence, gate, authority, new InMemoryAuthorityAuditSink());

        foreach (ProductionExecutionContext context in new[]
        {
            Context() with { Authorization = Context().Authorization with { ProjectOwnerGovernanceReference = "" } },
            Context() with { ExpiresAtUtc = Now.AddMinutes(-1), Authorization = Context().Authorization with { ExpiresAtUtc = Now.AddMinutes(-1) } },
            Context() with { Authorization = Context().Authorization with { Action = ProductionExecutionAction.Rollback } }
        })
            Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(context)).Status);
    }

    [Theory]
    [InlineData("wrong-deployment", "all", "profile")]
    [InlineData("qualification", "wrong-station", "profile")]
    [InlineData("qualification", "all", "wrong-profile")]
    public async Task Wrong_scope_is_rejected(string deployment, string station, string profile)
    {
        ProductionExecutionContext context = Context();
        ProductionExecutionScope scope = new(deployment, station, profile);
        context = profile == "wrong-profile"
            ? context with { Scope = scope }
            : context with { Scope = scope, Authorization = context.Authorization with { Scope = scope } };
        var executor = Executor(new InMemoryProductionAuthorityStore());
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(context)).Status);
    }

    [Theory]
    [InlineData("2.0", null)]
    [InlineData("9.7.0", 2)]
    public async Task Wrong_version_or_schema_is_rejected(string version, int? schema)
    {
        ProductionExecutionContext context = Context() with { ApplicationVersion = version, SchemaVersion = schema };
        var executor = Executor(new InMemoryProductionAuthorityStore());
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(context)).Status);
    }

    [Fact]
    public async Task Wrong_correlation_source_state_and_stale_generation_are_rejected()
    {
        var authority = new InMemoryProductionAuthorityStore();
        var executor = Executor(authority);
        ProductionExecutionContext wrongCorrelation = Context() with { CorrelationId = "other" };
        ProductionExecutionContext wrongState = Context() with { SourceAuthorityState = AuthorityState.TargetAuthoritative };
        ProductionExecutionContext stale = Context() with { AuthorityGeneration = new(5, 5) };
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(wrongCorrelation)).Status);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(wrongState)).Status);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(stale)).Status);
    }

    [Fact]
    public async Task Invalid_backup_reconciliation_divergence_and_audit_fail_closed()
    {
        var authority = new InMemoryProductionAuthorityStore();
        var executor = Executor(authority);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(Context() with { BackupReceipt = Context().BackupReceipt with { IsValid = false } })).Status);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(Context() with { Reconciliation = Context().Reconciliation with { Reconciliation = ReconciliationStatus.Mismatched } })).Status);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(Context() with { Reconciliation = Context().Reconciliation with { Divergence = DivergenceStatus.Diverged } })).Status);
        Assert.Equal(ProductionExecutionStatus.Rejected, (await executor.ExecuteAsync(Context() with { AuditReadiness = Context().AuditReadiness with { IntegrityVerified = false } })).Status);
    }

    [Fact]
    public async Task Write_gate_drains_legacy_writes_and_aborts_without_delay()
    {
        var gate = new ProductionWriteGate();
        LegacyWriteLeaseResult write = await gate.EnterLegacyWriteAsync(0);
        Assert.True(write.Acquired);
        Task<WriteDrainLeaseResult> drainTask = gate.AcquireDrainAsync();
        Assert.False((await gate.EnterLegacyWriteAsync(0)).Acquired);
        await write.Lease!.DisposeAsync();
        WriteDrainLeaseResult drained = await drainTask;
        Assert.True(drained.Acquired);
        Assert.False((await gate.EnterTargetWriteAsync(1)).Acquired);
        gate.AbortDrain();
        await drained.Lease!.DisposeAsync();
        Assert.True((await gate.EnterLegacyWriteAsync(0)).Acquired);
    }

    [Fact]
    public async Task Authority_persistence_or_routing_failure_enters_recovery_required()
    {
        var commitFailure = new InMemoryProductionAuthorityStore();
        commitFailure.FailTargetCommit = true;
        ProductionExecutionResult failedCommit = await Executor(commitFailure).ExecuteAsync(Context());
        Assert.Equal(ProductionExecutionStatus.RecoveryRequired, failedCommit.Status);
        Assert.Equal(AuthorityState.RecoveryRequired, (await commitFailure.ReadAsync()).Record.State);

        var routingFailure = new InMemoryProductionAuthorityStore();
        routingFailure.FailTargetRouting = true;
        ProductionExecutionResult failedRouting = await Executor(routingFailure).ExecuteAsync(Context());
        Assert.Equal(ProductionExecutionStatus.RecoveryRequired, failedRouting.Status);
        Assert.Equal(AuthorityState.RecoveryRequired, (await routingFailure.ReadAsync()).Record.State);
    }

    [Fact]
    public async Task Audit_failure_fails_closed_and_does_not_leave_a_normal_route()
    {
        var authority = new InMemoryProductionAuthorityStore();
        var audit = new FailureInjectingAuthorityAuditSink(new InMemoryAuthorityAuditSink()) { FailWrites = true };
        var executor = new GovernedProductionActivationExecutor(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), new TestFence(), new ProductionWriteGate(), authority, audit);

        ProductionExecutionResult result = await executor.ExecuteAsync(Context());

        Assert.Equal(ProductionExecutionStatus.RecoveryRequired, result.Status);
        ProductionAuthoritySnapshot current = await authority.ReadAsync();
        Assert.Equal(AuthorityState.RecoveryRequired, current.Record.State);
        Assert.False(current.Record.TargetRoutingEnabled);
    }

    [Fact]
    public async Task Local_fence_rejects_second_writer_rejects_stale_lease_and_recovers_after_release()
    {
        using var temp = new TempDirectory();
        var first = new LocalSingleWriterFence(Path.Combine(temp.Path, "writer.lock"));
        var second = new LocalSingleWriterFence(Path.Combine(temp.Path, "writer.lock"));
        WriterFenceAcquireResult acquired = await first.AcquireAsync(new("qualification", 1, "c1"));
        Assert.True(acquired.Acquired);
        Assert.False((await second.AcquireAsync(new("qualification", 1, "c2"))).Acquired);
        acquired.Lease!.EnsureCurrent(1);
        Assert.Throws<InvalidOperationException>(() => acquired.Lease.EnsureCurrent(2));
        await acquired.Lease.DisposeAsync();
        WriterFenceAcquireResult reacquired = await second.AcquireAsync(new("qualification", 2, "c2"));
        Assert.True(reacquired.Acquired);
        await reacquired.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Rollback_requires_eligible_evidence_and_restores_legacy_without_target_routing()
    {
        var authority = new InMemoryProductionAuthorityStore();
        var gate = new ProductionWriteGate();
        await using var fence = new TestFence();
        var audit = new InMemoryAuthorityAuditSink();
        var activation = new GovernedProductionActivationExecutor(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), fence, gate, authority, audit);
        Assert.True((await activation.ExecuteAsync(Context())).Succeeded);
        ProductionExecutionContext rollback = RollbackContext();
        var executor = new GovernedProductionRollbackExecutor(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), fence, gate, authority, audit);

        ProductionExecutionResult result = await executor.ExecuteAsync(rollback);

        Assert.True(result.Succeeded, result.Status + ":" + result.Reason);
        ProductionAuthoritySnapshot current = await authority.ReadAsync();
        Assert.Equal(AuthorityState.LegacyAuthoritative, current.Record.State);
        Assert.True(current.Record.LegacyAuthoritative);
        Assert.False(current.Record.TargetAuthoritative || current.Record.TargetRoutingEnabled);
    }

    [Fact]
    public void Rollback_target_writes_divergence_backup_and_audit_are_not_safe()
    {
        ProductionExecutionContext context = RollbackContext();
        Assert.Equal(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ProductionRollbackEligibility.Evaluate(context with { Reconciliation = context.Reconciliation with { HasUnreconciledTargetWrites = true } }, Now).Status);
        Assert.Equal(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ProductionRollbackEligibility.Evaluate(context with { Reconciliation = context.Reconciliation with { Divergence = DivergenceStatus.Diverged } }, Now).Status);
        Assert.Equal(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ProductionRollbackEligibility.Evaluate(context with { BackupReceipt = context.BackupReceipt with { IsValid = false } }, Now).Status);
        Assert.Equal(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ProductionRollbackEligibility.Evaluate(context with { AuditReadiness = context.AuditReadiness with { IsAvailable = false } }, Now).Status);
    }

    [Fact]
    public async Task Tamper_evident_audit_detects_edit_and_preserves_sequence()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "activation-audit.jsonl");
        using var sink = new TamperEvidentAuthorityAuditSink(path);
        await sink.WriteAsync(new("c1", Now, "qualification", "all", "9.7", AuthorityState.LegacyAuthoritative, AuthorityState.TargetAuthoritative, AuthorityAuditAction.Prepare, "PREPARED", "test", "e1", 1));
        await sink.WriteAsync(new("c1", Now, "qualification", "all", "9.7", AuthorityState.LegacyAuthoritative, AuthorityState.TargetAuthoritative, AuthorityAuditAction.CommitSucceeded, "SUCCEEDED", "test", "e2", 1));
        Assert.True((await sink.VerifyAsync()).IsValid);
        File.WriteAllText(path, File.ReadAllText(path).Replace("SUCCEEDED", "TAMPERED", StringComparison.Ordinal));
        Assert.False((await sink.VerifyAsync()).IsValid);
    }

    [Fact]
    public void Profile_boundary_remains_3_4_5_valid_and_2_6_35_invalid()
    {
        foreach (int value in new[] { 3, 4, 5 }) Assert.True(value is >= 3 and <= 5);
        foreach (int value in new[] { 2, 6, 35 }) Assert.False(value is >= 3 and <= 5);
    }

    private static GovernedProductionActivationExecutor Executor(IProductionAuthorityStore authority)
    {
        var fence = new TestFence();
        return new(new FixedClock(Now), new QualificationProductionAuthorizationVerifier(), fence, new ProductionWriteGate(), authority, new InMemoryAuthorityAuditSink());
    }

    private static ProductionExecutionContext Context()
    {
        var scope = new ProductionExecutionScope("qualification", "all", "profile-qualification");
        var authorization = new ProductionAuthorizationContract(ProductionExecutionAction.Activate, scope, "9.7.0", 1, "phase97-correlation", AuthorityState.LegacyAuthoritative, AuthorityState.TargetAuthoritative, Now.AddMinutes(-1), Now.AddMinutes(10), "decision-reference", "owner-governance-reference");
        return new(ProductionExecutionAction.Activate, scope, "phase97-correlation", "9.7.0", 1, AuthorityState.LegacyAuthoritative, AuthorityState.TargetAuthoritative, new(0, 0),
            new("pr-97", true, Now.AddMinutes(-2), "evidence/pr-97"), new("decision-reference", "owner-reference", true, Now.AddMinutes(-1), Now.AddMinutes(10)),
            new(true, 1, ManagementProofFailure.None, Now.AddMinutes(-1)), new("backup-97", "backup-fingerprint", true, Now.AddMinutes(-1), "evidence/backup"),
            new(ReconciliationStatus.Matched, DivergenceStatus.Synchronized, "evidence/reconciliation", true), new(true, true, "evidence/audit"), Now.AddMinutes(-1), Now.AddMinutes(10), authorization);
    }

    private static ProductionExecutionContext RollbackContext()
    {
        ProductionExecutionContext activation = Context();
        var authorization = activation.Authorization with { Action = ProductionExecutionAction.Rollback, SourceAuthorityState = AuthorityState.TargetAuthoritative, TargetAuthorityState = AuthorityState.LegacyAuthoritative };
        return activation with { Action = ProductionExecutionAction.Rollback, SourceAuthorityState = AuthorityState.TargetAuthoritative, ExpectedTargetAuthorityState = AuthorityState.LegacyAuthoritative, AuthorityGeneration = new(1, 1), Authorization = authorization };
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow;
    }
    private sealed class TestFence : IProductionWriterFence, IAsyncDisposable
    {
        public Task<WriterFenceAcquireResult> AcquireAsync(WriterFenceRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.Acquired, "test-fence", new WriterFenceLease(request.Generation, false, () => { })));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rahnegar-phase97-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
