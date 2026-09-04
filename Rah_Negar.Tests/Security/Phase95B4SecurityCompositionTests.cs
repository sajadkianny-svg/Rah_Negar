using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Security;
using Rah_Negar.Tests.Database;

namespace Rah_Negar.Tests.Security;

public sealed class Phase95B4SecurityCompositionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShiftProfile_authentication_creates_only_an_active_station_bound_session()
    {
        var fixture = Fixture.Create();

        TargetAuthenticationResult success = await fixture.Authentication.AuthenticateAsync(
            "Rasht", "1001", "ShiftPass1!".AsMemory(), "corr-login-1");

        Assert.True(success.Succeeded);
        Assert.Equal("shift-1", success.Session!.ShiftProfileId);
        Assert.Equal("Rasht", success.Session.StationId);
        Assert.Equal(3, success.Session.CredentialVersion);
        Assert.True(TargetShiftProfileAuthenticationService.IsSessionActive(success.Session, Now));

        TargetAuthenticationResult invalid = await fixture.Authentication.AuthenticateAsync(
            "Rasht", "1001", "wrong".AsMemory(), "corr-login-2");
        Assert.False(invalid.Succeeded);
        Assert.Equal(TargetAuthenticationFailure.InvalidCredential, invalid.Failure);
        Assert.Null(invalid.Session);
        Assert.Contains(fixture.Audit.Events, x => !x.Succeeded && x.AuthorizationType ==
            SecurityAuthorizationType.OperationalShiftProfile);
    }

    [Fact]
    public async Task Every_protected_action_requires_the_singleton_management_credential_and_exact_binding()
    {
        var fixture = Fixture.Create();
        TargetShiftProfileSession session = Session();

        foreach (ProtectedAction action in ProtectedActionInventory.All)
        {
            TargetManagementAuthorizationResult result = await fixture.Management.AuthorizeAsync(
                session, "Rasht", action, $"Rasht|{action}", $"corr-{action}", "ManagePass1!".AsMemory());
            Assert.True(result.Succeeded, action.ToString());
            Assert.Equal(action, result.Proof!.Action);
            Assert.Equal($"Rasht|{action}", result.Proof.ActionScope);
            Assert.Equal(7, result.Proof.CredentialVersion);
        }

        TargetManagementAuthorizationResult wrongScope = await fixture.Management.AuthorizeAsync(
            session, "Ramsar", ProtectedAction.Restore, "Ramsar|backup-1", "corr-wrong", "ManagePass1!".AsMemory());
        Assert.False(wrongScope.Succeeded);
        Assert.Equal(TargetManagementAuthorizationFailure.StationScopeMismatch, wrongScope.Failure);

        TargetManagementAuthorizationResult wrongCredential = await fixture.Management.AuthorizeAsync(
            session, "Rasht", ProtectedAction.Restore, "Rasht|backup-1", "corr-wrong-secret", "wrong".AsMemory());
        Assert.False(wrongCredential.Succeeded);
        Assert.Equal(TargetManagementAuthorizationFailure.InvalidCredential, wrongCredential.Failure);
    }

    [Fact]
    public async Task Management_authorization_fails_closed_when_audit_is_unavailable()
    {
        var fixture = Fixture.Create();
        fixture.Audit.Throw = true;

        TargetManagementAuthorizationResult result = await fixture.Management.AuthorizeAsync(
            Session(), "Rasht", ProtectedAction.Restore, "Rasht|backup-1", "corr-audit-failure",
            "ManagePass1!".AsMemory());

        Assert.False(result.Succeeded);
        Assert.Equal(TargetManagementAuthorizationFailure.AuditUnavailable, result.Failure);
        Assert.Null(result.Proof);
    }

    [Fact]
    public async Task Recovery_rotates_the_singleton_revision_without_creating_an_identity_or_secret_record()
    {
        var fixture = Fixture.Create();
        ManagementRecoveryRequest request = new("shift-1", "Rasht", "corr-recovery-1", "approved rotation",
            "management-approval-1", "security-review-1", Now);

        ManagementRecoveryResult result = await fixture.Recovery.RotateAsync(
            Session(), request, "NewManagePass1!".AsMemory());

        Assert.True(result.Succeeded);
        Assert.Equal(8, result.NewCredentialVersion);
        Assert.NotNull(fixture.RecoveryBoundary.Replacement);
        Assert.Equal(8, fixture.RecoveryBoundary.Replacement!.CredentialVersion);
        Assert.Equal(ProtectedAction.EmergencyRecovery, fixture.RecoveryBoundary.Audit!.Action);
        Assert.DoesNotContain("NewManagePass1!", fixture.RecoveryBoundary.Audit.NonSecretValueMetadata.Values);
        Assert.True(Pbkdf2TargetPasswordVerifier.Instance.Verify("NewManagePass1!",
            fixture.RecoveryBoundary.Replacement));

        ManagementRecoveryResult invalidApprover = await fixture.Recovery.RotateAsync(
            Session(), request with { ManagementApproverReference = "secret approval" }, "NewManagePass1!".AsMemory());
        Assert.False(invalidApprover.Succeeded);
        Assert.Equal(ManagementRecoveryFailure.ApprovalReferenceInvalid, invalidApprover.Failure);
    }

    [Fact]
    public async Task Recovery_boundary_rejection_does_not_report_a_new_revision()
    {
        var fixture = Fixture.Create();
        fixture.RecoveryBoundary.Accept = false;
        ManagementRecoveryResult result = await fixture.Recovery.RotateAsync(Session(),
            new("shift-1", "Rasht", "corr-recovery-2", "approved rotation", "approval-1", "review-1", Now),
            "NewManagePass1!".AsMemory());

        Assert.False(result.Succeeded);
        Assert.Equal(ManagementRecoveryFailure.RotationRejected, result.Failure);
        Assert.Null(result.NewCredentialVersion);
    }

    [Fact]
    public async Task SQLite_recovery_boundary_commits_new_credential_and_audit_as_one_transaction()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        var checksums = new Sha256ChecksumService();
        var runner = new MigrationRunner(new SqliteTransactionManager(db.Factory),
            new MigrationChecksumValidator(checksums));
        await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksums));
        await using (SqliteConnection connection = await db.Factory.OpenConnectionAsync())
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO SecurityManagementCredentials
                  (SingletonId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
                   IsCurrent,IsActive,CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc)
                VALUES (1,7,'PBKDF2-SHA256','iterations=100000;length=32',X'01',X'02',1,1,
                   '2026-09-04T08:00:00.0000000+00:00','2026-09-04T08:00:00.0000000+00:00',NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var boundary = new SQLiteManagementCredentialRecoveryBoundary(db.Factory);
        ManagementCredentialRecord replacement = new(8, Pbkdf2TargetPasswordVerifier.Algorithm,
            Pbkdf2TargetPasswordVerifier.Parameters, [3], [4], true, true,
            Now.AddMinutes(1), Now.AddMinutes(1), null);
        SecurityAuditEvent audit = new("shift-1", ProtectedAction.EmergencyRecovery, "Rasht",
            SecurityAuthorizationType.ManagementCredential, true, Now.AddMinutes(1), "corr-sqlite-recovery",
            SecurityAuditMetadataBuilder.Create([
                new("AuthorizationStage", "ManagementRecovery"),
                new("ResultCategory", "CredentialRotated"),
                new("CorrelationId", "corr-sqlite-recovery")]));

        Assert.True(await boundary.TryRotateAsync(replacement, 7, audit));
        Assert.Equal(1L, await ScalarAsync(db, "SELECT COUNT(*) FROM SecurityManagementCredentials WHERE IsCurrent=1 AND CredentialVersion=8;"));
        Assert.Equal(1L, await ScalarAsync(db, "SELECT COUNT(*) FROM SecurityManagementCredentials WHERE IsCurrent=0 AND CredentialVersion=7;"));
        Assert.Equal(1L, await ScalarAsync(db, "SELECT COUNT(*) FROM SecurityAuditEntries WHERE Action='EmergencyRecovery' AND CorrelationId='corr-sqlite-recovery';"));
    }

    [Fact]
    public void Target_composition_is_explicitly_inactive_and_has_no_legacy_recovery_entry_point()
    {
        TargetSecurityCompositionDescriptor descriptor = TargetSecurityCompositionDescriptor.Inactive;

        Assert.Equal(TargetSecurityCompositionState.Inactive, descriptor.State);
        Assert.False(descriptor.TargetRoutesEnabled);
        Assert.True(descriptor.LegacyRemainsAuthoritative);
        Assert.False(descriptor.LegacyRecoveryReachable);
        Assert.True(descriptor.UsesShiftProfileAuthentication);
        Assert.True(descriptor.UsesSingletonManagementCredential);
        Assert.True(descriptor.UsesOfflineEcdsaP256VendorAuthorization);
        Assert.Equal(Enum.GetValues<ProtectedAction>().Length, ProtectedActionInventory.All.Count);
        Assert.DoesNotContain(typeof(InactiveTargetSecurityComposition).GetMethods(),
            method => method.Name.Contains("Activate", StringComparison.OrdinalIgnoreCase));
    }

    private static TargetShiftProfileSession Session() => new("shift-1", "Rasht", 3, Now.AddMinutes(-1), Now.AddMinutes(20));

    private static async Task<long> ScalarAsync(TemporarySqliteDatabase db, string sql)
    {
        await using SqliteConnection connection = await db.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed class Fixture
    {
        private Fixture()
        {
            Audit = new MemoryAudit();
            Profiles = new MemoryProfiles();
            ShiftCredentials = new MemoryShiftCredentials();
            ManagementCredentials = new MemoryManagementCredentials();
            RecoveryBoundary = new MemoryRecoveryBoundary();
            IClock clock = new FixedClock(Now);
            Authentication = new TargetShiftProfileAuthenticationService(Profiles, ShiftCredentials, Audit, clock,
                TimeSpan.FromMinutes(15));
            Management = new TargetManagementAuthorizationService(ManagementCredentials, Audit, clock,
                TimeSpan.FromMinutes(3));
            Recovery = new TargetManagementRecoveryService(ManagementCredentials, RecoveryBoundary, clock);
        }

        public static Fixture Create() => new();
        public MemoryAudit Audit { get; }
        public MemoryProfiles Profiles { get; }
        public MemoryShiftCredentials ShiftCredentials { get; }
        public MemoryManagementCredentials ManagementCredentials { get; }
        public MemoryRecoveryBoundary RecoveryBoundary { get; }
        public TargetShiftProfileAuthenticationService Authentication { get; }
        public TargetManagementAuthorizationService Management { get; }
        public TargetManagementRecoveryService Recovery { get; }
    }

    private sealed class MemoryProfiles : IShiftProfileRepository
    {
        private static readonly ShiftProfile Profile = new("shift-1", "Rasht", 1, "Shift 1", "First", "Last",
            "1001", true, Now.AddDays(-1), Now.AddDays(-1), 1);

        public Task<IReadOnlyList<ShiftProfile>> ReadActiveAsync(string stationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShiftProfile>>([Profile]);
        public Task<ShiftProfile?> FindByPersonnelNoAsync(string stationId, string personnelNo, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShiftProfile?>(stationId == Profile.StationId && personnelNo == Profile.PersonnelNo ? Profile : null);
        public Task CreateAsync(ShiftProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> UpdateAsync(ShiftProfile profile, long expectedRevision, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class MemoryShiftCredentials : IShiftProfileCredentialRepository
    {
        private readonly ShiftProfileCredentialRecord _record = CreateShiftCredential();
        public Task<ShiftProfileCredentialRecord?> LoadCurrentAsync(string shiftProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShiftProfileCredentialRecord?>(shiftProfileId == "shift-1" ? _record : null);
        public Task<bool> ReplaceAsync(ShiftProfileCredentialRecord replacement, int? expectedCurrentVersion, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class MemoryManagementCredentials : IManagementCredentialRepository
    {
        private ManagementCredentialRecord _record = CreateManagementCredential();
        public Task<ManagementCredentialRecord?> LoadCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<ManagementCredentialRecord?>(_record);
        public Task<bool> ReplaceAsync(ManagementCredentialRecord replacement, int? expectedCurrentVersion, CancellationToken cancellationToken = default)
        { _record = replacement; return Task.FromResult(true); }
    }

    private sealed class MemoryRecoveryBoundary : IManagementCredentialRecoveryBoundary
    {
        public bool Accept { get; set; } = true;
        public ManagementCredentialRecord? Replacement { get; private set; }
        public SecurityAuditEvent? Audit { get; private set; }
        public Task<bool> TryRotateAsync(ManagementCredentialRecord replacement, int expectedCurrentVersion,
            SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            if (Accept)
            {
                Replacement = replacement with
                {
                    Salt = replacement.Salt.ToArray(),
                    PasswordVerifier = replacement.PasswordVerifier.ToArray()
                };
                Audit = auditEvent;
            }
            return Task.FromResult(Accept);
        }
    }

    private sealed class MemoryAudit : ISecurityAuditSink
    {
        public List<SecurityAuditEvent> Events { get; } = [];
        public bool Throw { get; set; }
        public Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException();
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset LocalNow => now;
    }

    private static ShiftProfileCredentialRecord CreateShiftCredential()
    {
        byte[] salt = [1, 2, 3, 4, 5, 6, 7, 8];
        return new("shift-1", 3, Pbkdf2TargetPasswordVerifier.Algorithm,
            Pbkdf2TargetPasswordVerifier.Parameters, salt,
            Pbkdf2TargetPasswordVerifier.CreateVerifier("ShiftPass1!", salt), true, Now, null);
    }

    private static ManagementCredentialRecord CreateManagementCredential()
    {
        byte[] salt = [8, 7, 6, 5, 4, 3, 2, 1];
        return new(7, Pbkdf2TargetPasswordVerifier.Algorithm, Pbkdf2TargetPasswordVerifier.Parameters,
            salt, Pbkdf2TargetPasswordVerifier.CreateVerifier("ManagePass1!", salt), true, true, Now, Now, null);
    }
}
