using System.Security.Cryptography;
using System.Text.Json;
using Rah_Negar.Foundation.Application.Authority;

namespace Rah_Negar.Tests.Authority;

public sealed class AuthorityFoundationTests
{
    [Fact]
    public async Task Missing_store_initializes_legacy_and_target_is_not_authoritative()
    {
        using TempDirectory temp = new();
        var result = await new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json")).LoadAsync();
        Assert.Equal(AuthorityLoadStatus.Initialized, result.Status);
        Assert.Equal(AuthorityState.LegacyAuthoritative, result.Record.State);
        Assert.True(result.Record.LegacyAuthoritative);
        Assert.False(result.Record.TargetAuthoritative);
        Assert.False(result.Record.TargetRoutingEnabled);
        Assert.True(AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(result.Record));
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(result.Record));
    }

    [Fact]
    public async Task Valid_legacy_state_survives_restart_deterministically()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "authority.json");
        var store = new FileAuthorityStateStore(path);
        AuthorityStateRecord expected = AuthorityStateRecord.Legacy("rehearsal", "all");
        await store.SaveAsync(expected);
        AuthorityLoadResult actual = await new FileAuthorityStateStore(path).LoadAsync();
        Assert.Equal(AuthorityLoadStatus.Loaded, actual.Status);
        Assert.Equal(expected with { RecordedAtUtc = actual.Record.RecordedAtUtc }, actual.Record);
    }

    [Fact]
    public async Task Malformed_unknown_and_contradictory_metadata_fail_closed_to_recovery()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "authority.json");
        await File.WriteAllTextAsync(path, "{not-json");
        Assert.Equal(AuthorityLoadStatus.RecoveryRequired,
            (await new FileAuthorityStateStore(path).LoadAsync()).Status);

        string payload = "{\"SchemaVersion\":1,\"Revision\":1,\"State\":\"NoSuchState\",\"LegacyAuthoritative\":true,\"TargetAuthoritative\":false,\"TargetRoutingEnabled\":false,\"DeploymentScope\":\"production\",\"StationScope\":\"all\",\"CorrelationId\":null,\"Reason\":null,\"RecordedAtUtc\":\"2026-09-06T00:00:00+00:00\"}";
        string envelope = JsonSerializer.Serialize(new { Payload = payload, IntegritySha256 = "00" });
        await File.WriteAllTextAsync(path, envelope);
        Assert.Equal(AuthorityLoadStatus.RecoveryRequired,
            (await new FileAuthorityStateStore(path).LoadAsync()).Status);

        AuthorityStateRecord contradictory = AuthorityStateRecord.Legacy() with { TargetAuthoritative = true };
        Assert.False(AuthorityStateValidator.Validate(contradictory).IsValid);
    }

    [Fact]
    public async Task Recovery_required_is_first_class_and_audit_failure_is_fail_closed()
    {
        AuthorityStateRecord recovery = AuthorityStateRecord.Recovery("commit-outcome-unknown", "corr-recovery");
        Assert.Equal(AuthorityState.RecoveryRequired, recovery.State);
        Assert.False(AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(recovery));
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(recovery));
        var audit = new FailureInjectingAuthorityAuditSink(new InMemoryAuthorityAuditSink()) { FailWrites = true };
        await Assert.ThrowsAsync<IOException>(() => audit.WriteAsync(new(
            "corr-recovery", DateTimeOffset.UtcNow, "production", "all", "9.6D1",
            AuthorityState.LegacyAuthoritative, AuthorityState.RecoveryRequired,
            AuthorityAuditAction.RecoveryRequiredEntered, "Blocked", "commit-outcome-unknown")));
    }

    [Fact]
    public void Impossible_combinations_are_rejected_and_target_route_never_appears_by_default()
    {
        AuthorityStateRecord both = AuthorityStateRecord.Legacy() with { TargetAuthoritative = true };
        AuthorityStateRecord routeOnly = AuthorityStateRecord.Legacy() with { TargetRoutingEnabled = true };
        Assert.False(AuthorityStateValidator.Validate(both).IsValid);
        Assert.False(AuthorityStateValidator.Validate(routeOnly).IsValid);
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(AuthorityStateRecord.Legacy()));
    }

    [Fact]
    public async Task Intent_is_scope_and_source_bound_does_not_change_authority_or_routing()
    {
        AuthorityStateRecord current = AuthorityStateRecord.Legacy("rehearsal", "all");
        var service = new TransitionIntentService(new InMemoryTransitionIntentStore());
        TransitionIntent intent = Intent(current, "corr-1");
        var created = await service.CreateAsync(intent, current);
        Assert.NotNull(created.Intent);
        Assert.False(created.Intent!.ChangesAuthority);
        Assert.False(created.Intent.EnablesRouting);
        Assert.Equal(AuthorityState.LegacyAuthoritative, current.State);
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(current));
    }

    [Fact]
    public async Task Intent_rejects_replay_wrong_scope_wrong_source_and_unsupported_target()
    {
        AuthorityStateRecord current = AuthorityStateRecord.Legacy("rehearsal", "all");
        var service = new TransitionIntentService(new InMemoryTransitionIntentStore());
        Assert.True((await service.CreateAsync(Intent(current, "corr-1"), current)).Validation.IsValid);
        Assert.False((await service.CreateAsync(Intent(current, "corr-1"), current)).Validation.IsValid);
        Assert.Contains("intent-scope-mismatch", (await service.CreateAsync(Intent(current, "corr-2") with { StationScope = "station-ramsar" }, current)).Validation.Issues);
        Assert.Contains("intent-source-state-mismatch", (await service.CreateAsync(Intent(current, "corr-3") with { SourceState = AuthorityState.TransitionInProgress }, current)).Validation.Issues);
        Assert.Contains("intent-target-transition-not-supported", (await service.CreateAsync(Intent(current, "corr-4") with { RequestedTargetState = AuthorityState.TargetAuthoritative }, current)).Validation.Issues);
        Assert.Contains("intent-version-invalid", (await service.CreateAsync(Intent(current, "corr-5") with { ApplicationVersion = "9.6 D1" }, current)).Validation.Issues);
    }

    [Fact]
    public void Reconciliation_reports_all_required_outcomes_and_is_read_only()
    {
        var evaluator = new ReadOnlyReconciliationEvaluator();
        ReconciliationSnapshot matching = new(true, "ABC", 10, null);
        Assert.Equal(ReconciliationStatus.Matched, evaluator.Evaluate(matching, matching).Status);
        Assert.Equal(ReconciliationStatus.Mismatched, evaluator.Evaluate(matching, matching with { Fingerprint = "DEF" }).Status);
        Assert.Equal(ReconciliationStatus.Blocked, evaluator.Evaluate(matching, matching with { Readable = false }).Status);
        Assert.Equal(ReconciliationStatus.NotEvaluated, evaluator.Evaluate(matching, matching with { Fingerprint = null }).Status);
        ReconciliationResult result = evaluator.Evaluate(matching, matching);
        Assert.False(result.MutatedAuthority || result.MutatedRouting || result.MutatedData);
    }

    [Fact]
    public async Task Failure_injection_covers_authority_write_and_qualification_does_not_mutate_production_db()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "authority.json");
        var store = new FileAuthorityStateStore(path, () => true);
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(AuthorityStateRecord.Legacy()));
        string production = Path.Combine(RepositoryRoot(), "Data", "db.sys");
        if (!File.Exists(production)) return;
        string before = Hash(production);
        await new FileAuthorityStateStore(Path.Combine(temp.Path, "qualification-authority.json")).SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        Assert.Equal(before, Hash(production));
    }

    [Fact]
    public void Existing_unit_boundary_and_forbidden_security_concepts_remain_unchanged()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "Qualification", "QualificationEnvironment.cs"));
        Assert.DoesNotContain("35", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RBAC", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportIdentity", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("master password", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Startup_resolves_transition_before_routing_and_fails_closed_on_mismatch()
    {
        using TempDirectory temp = new();
        string authorityPath = Path.Combine(temp.Path, "authority.json");
        string transitionPath = Path.Combine(temp.Path, "transition.json");
        var authority = new FileAuthorityStateStore(authorityPath);
        var transitions = new FileTransitionStateStore(transitionPath);
        AuthorityLoadResult loaded = await authority.LoadAsync();
        await transitions.SaveAsync(AuthorityTransitionRecord.PreparedFrom(loaded.Record, "t-1", 1));
        AuthorityStartupResult result = await new AuthorityStartupResolver(authority).ResolveCanonicalAsync(transitions);
        Assert.True(result.RoutingBlocked);
        Assert.Equal("PreparedNotCommitted", result.Classification);
        Assert.True(result.EffectiveAuthority.LegacyAuthoritative);
        Assert.False(result.EffectiveAuthority.TargetRoutingEnabled);

        await authority.SaveAsync(loaded.Record with { DeploymentScope = "other" });
        result = await new AuthorityStartupResolver(authority).ResolveCanonicalAsync(transitions);
        Assert.True(result.RoutingBlocked);
        Assert.Equal("AuthorityTransitionMismatch", result.Classification);
    }

    [Fact]
    public async Task Commit_is_idempotent_and_stale_generation_is_fenced()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var service = new AuthorityCommitService(authority, transitions);
        AuthorityTransitionRecord prepared = await service.PrepareAsync("t-1");
        Assert.True(await service.CommitAsync(prepared.TransitionId, prepared.Generation));
        Assert.True(await service.CommitAsync(prepared.TransitionId, prepared.Generation));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitAsync(prepared.TransitionId, prepared.Generation - 1));
        AuthorityLoadResult current = await authority.LoadAsync();
        Assert.Equal(AuthorityState.LegacyAuthoritative, current.Record.State);
        Assert.False(current.Record.TargetAuthoritative || current.Record.TargetRoutingEnabled);
    }

    [Fact]
    public async Task Restart_after_commit_marker_has_deterministic_outcome()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var service = new AuthorityCommitService(authority, transitions);
        AuthorityTransitionRecord prepared = await service.PrepareAsync("t-restart");
        await service.CommitAsync(prepared.TransitionId, prepared.Generation);
        AuthorityStartupResult first = await new AuthorityStartupResolver(authority).ResolveCanonicalAsync(transitions);
        AuthorityStartupResult second = await new AuthorityStartupResolver(new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json")))
            .ResolveCanonicalAsync(new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json")));
        Assert.Equal(first.Classification, second.Classification);
        Assert.Equal("CommitPersisted", second.Classification);
        Assert.True(second.RoutingBlocked);
    }

    [Fact]
    public async Task Retry_after_crash_between_authority_and_commit_marker_is_safe()
    {
        using TempDirectory temp = new();
        string authorityPath = Path.Combine(temp.Path, "authority.json");
        string transitionPath = Path.Combine(temp.Path, "transition.json");
        var authority = new FileAuthorityStateStore(authorityPath);
        var transitions = new FileTransitionStateStore(transitionPath);
        AuthorityLoadResult initial = await authority.LoadAsync();
        AuthorityTransitionRecord prepared = AuthorityTransitionRecord.PreparedFrom(initial.Record, "t-crash", 1);
        await transitions.SaveAsync(prepared with { Lifecycle = TransitionLifecycle.Committing });
        await authority.SaveAsync(initial.Record with { AuthorityEpoch = prepared.Generation, Revision = 1 });

        AuthorityStartupResult recovery = await new AuthorityStartupResolver(authority).ResolveCanonicalAsync(transitions);
        Assert.Equal("CommitInProgress", recovery.Classification);
        Assert.True(recovery.RoutingBlocked);
        Assert.True(await new AuthorityCommitService(authority, transitions).CommitAsync("t-crash", 1));
        Assert.Equal(TransitionLifecycle.Committed, (await transitions.LoadAsync()).Record!.Lifecycle);
        Assert.False((await authority.LoadAsync()).Record.TargetAuthoritative);
    }

    private static TransitionIntent Intent(AuthorityStateRecord current, string correlation) => new(
        correlation, current.DeploymentScope, current.StationScope, "9.6D1.1",
        current.State, AuthorityState.ActivationPreparedNotExecuted, DateTimeOffset.UtcNow,
        TransitionIntentStatus.Created, "qualification-evidence", null);

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rah-authority-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
