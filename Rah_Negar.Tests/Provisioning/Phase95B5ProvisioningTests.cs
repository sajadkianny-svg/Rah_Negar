using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Provisioning;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Provisioning;
using Rah_Negar.Tests.Database;

namespace Rah_Negar.Tests.Provisioning;

[Trait("Qualification", "MQ-03")]
public sealed class Phase95B5ProvisioningTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("rasht", "Rasht Station", 3)]
    [InlineData("ramsar", "Ramsar Station", 4)]
    public void Manifest_covers_the_station_shape_without_exposing_sensitive_material(
        string stationKey, string stationName, int expectedUnits)
    {
        TargetStationProvisioningPackage package = Package(stationKey, stationName, expectedUnits);
        TargetProvisioningValidationResult result = TargetStationProvisioningManifestBuilder.Validate(package);

        Assert.True(result.IsValid, string.Join(",", result.Issues));
        TargetStationProvisioningManifest manifest = result.Manifest!;
        Assert.Equal($"profile-{stationKey}", manifest.StationProfileId);
        Assert.Equal(expectedUnits, manifest.ProfileUnitCount);
        Assert.Equal(expectedUnits, manifest.EntityCounts["Units"]);
        Assert.Equal(1, manifest.EntityCounts["StationProfiles"]);
        Assert.DoesNotContain("1001", JsonSerializer.Serialize(manifest), StringComparison.Ordinal);
        Assert.DoesNotContain("management-secret", JsonSerializer.Serialize(manifest), StringComparison.Ordinal);
        Assert.DoesNotContain("private-key", JsonSerializer.Serialize(manifest), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(manifest.Entities, x => x.EntityType == "RuntimeBaseline");
        Assert.Contains(manifest.Entities, x => x.EntityType == "FinalizedSnapshot");
        Assert.Contains(manifest.Entities, x => x.EntityType == "FinalizedLock");
    }

    [Fact]
    public void Manifest_rejects_cross_station_unit_mapping()
    {
        TargetStationProvisioningPackage package = Package("rasht", "Rasht Station", 3);
        TargetStationProvisioningPackage invalid = package with
        {
            Units = package.Units.Select((x, i) => i == 0 ? x with { StationId = "station-ramsar" } : x).ToArray()
        };

        TargetProvisioningValidationResult result = TargetStationProvisioningManifestBuilder.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains("unit-station-mismatch", result.Issues);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Target_route_catalog_is_complete_but_explicitly_inactive()
    {
        IReadOnlyList<TargetOperationalRouteDescriptor> routes = TargetOperationalRouteCatalog.Create();

        Assert.Equal(Enum.GetValues<TargetOperationalRouteArea>().Length, routes
            .Select(x => x.Area).Distinct().Count());
        Assert.Contains(routes, x => x.Access == TargetOperationalRouteAccess.Write);
        Assert.Contains(routes, x => x.Access == TargetOperationalRouteAccess.ProtectedWrite);
        Assert.All(routes, route =>
        {
            Assert.True(route.IsComposed);
            Assert.False(route.IsEnabled);
            Assert.False(route.ProductionMutationAllowed);
            Assert.False(string.IsNullOrWhiteSpace(route.LegacyOwner));
            Assert.False(string.IsNullOrWhiteSpace(route.TargetOwner));
        });
    }

    [Theory]
    [InlineData("rasht", "Rasht Station", 3)]
    [InlineData("ramsar", "Ramsar Station", 4)]
    public async Task Provisioning_is_transactional_idempotent_and_preserves_finalized_snapshot_and_lock(
        string stationKey, string stationName, int expectedUnits)
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package(stationKey, stationName, expectedUnits);
        string stationId = package.StationId;
        string snapshotId = "snapshot-" + stationKey;

        TargetProvisioningResult first = await boundary.ProvisionAsync(package);
        Assert.True(first.Outcome == TargetProvisioningOutcome.Provisioned, string.Join(",", first.Issues));
        string snapshotBefore = await ScalarAsync(db, $"SELECT CanonicalJson FROM ReportSnapshots WHERE SnapshotId='{snapshotId}';");
        string lockBefore = await ScalarAsync(db, "SELECT EffectiveSnapshotId||':'||Revision FROM ReportPeriodLocks;");

        TargetProvisioningResult second = await boundary.ProvisionAsync(package);

        Assert.Equal(TargetProvisioningOutcome.AlreadyProvisioned, second.Outcome);
        Assert.Equal(snapshotBefore, await ScalarAsync(db, $"SELECT CanonicalJson FROM ReportSnapshots WHERE SnapshotId='{snapshotId}';"));
        Assert.Equal(lockBefore, await ScalarAsync(db, "SELECT EffectiveSnapshotId||':'||Revision FROM ReportPeriodLocks;"));
        Assert.Equal(expectedUnits, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Units WHERE StationId='{stationId}';"));
        Assert.Equal(2L, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Events WHERE StationId='{stationId}';"));
        Assert.Equal(1L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM SecurityManagementCredentials WHERE IsCurrent=1;"));
    }

    [Fact]
    public async Task Conflicting_esd_value_is_rejected_without_mutating_the_prepared_database()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package("rasht", "Rasht Station", 3);
        TargetProvisioningResult initial = await boundary.ProvisionAsync(package);
        Assert.True(initial.Succeeded, string.Join(",", initial.Issues));

        TargetProvisioningResult conflict = await boundary.ProvisionAsync(package with
        {
            CorrelationId = "corr-rasht-conflict",
            EsdAdjustmentCanonical = "9"
        });

        Assert.Equal(TargetProvisioningOutcome.Rejected, conflict.Outcome);
        Assert.Equal(TargetProvisioningFailure.Conflict, conflict.Failure);
        Assert.Contains("esd-mapping-conflict", conflict.Issues);
        Assert.Equal("2.5", await ScalarAsync(db,
            "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings WHERE SingletonId=1;"));
        Assert.Equal(3L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM Units WHERE StationId='station-rasht';"));
    }

    [Theory]
    [InlineData("delta-minimum", "Synthetic Minimum Station", 3)]
    [InlineData("delta-maximum", "Synthetic Maximum Station", 5)]
    public async Task Arbitrary_station_uses_the_supplied_profile_within_the_product_unit_boundary(
        string stationKey, string stationName, int unitCount)
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package(stationKey, stationName, unitCount);

        TargetProvisioningValidationResult validation = TargetStationProvisioningManifestBuilder.Validate(package);
        TargetProvisioningResult result = await boundary.ProvisionAsync(package);

        Assert.True(validation.IsValid, string.Join(",", validation.Issues));
        Assert.Equal($"profile-{stationKey}", validation.Manifest!.StationProfileId);
        Assert.Equal(unitCount, validation.Manifest.ProfileUnitCount);
        Assert.Equal(TargetProvisioningOutcome.Provisioned, result.Outcome);
        Assert.Equal(unitCount, await ScalarLongAsync(db,
            $"SELECT COUNT(*) FROM Units WHERE StationId='station-{stationKey}';"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(35)]
    public async Task Arbitrary_station_outside_the_product_unit_boundary_is_rejected_without_partial_mutation(
        int unitCount)
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package(
            $"boundary-{unitCount}", $"Synthetic Boundary {unitCount} Station", unitCount);

        TargetProvisioningValidationResult validation = TargetStationProvisioningManifestBuilder.Validate(package);
        TargetProvisioningResult result = await boundary.ProvisionAsync(package);

        Assert.False(validation.IsValid);
        Assert.Contains("profile-unit-count-out-of-range", validation.Issues);
        Assert.Null(validation.Manifest);
        Assert.Equal(TargetProvisioningOutcome.Rejected, result.Outcome);
        Assert.Equal(TargetProvisioningFailure.InvalidManifest, result.Failure);
        Assert.Contains("profile-unit-count-out-of-range", result.Issues);
        Assert.Equal(0L, await ProvisionedRowCountAsync(db));
    }

    [Fact]
    public async Task Unit_count_mismatch_and_persisted_conflict_are_rejected_without_partial_mutation()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package("delta", "Synthetic Delta Station", 5);
        TargetStationProvisioningPackage conflict = package with
        {
            StationProfile = package.StationProfile with { UnitCount = 4 }
        };

        TargetProvisioningResult mismatch = await boundary.ProvisionAsync(conflict);

        Assert.Equal(TargetProvisioningOutcome.Rejected, mismatch.Outcome);
        Assert.Equal(TargetProvisioningFailure.InvalidManifest, mismatch.Failure);
        Assert.Contains("unit-count-mismatch", mismatch.Issues);
        Assert.Equal(0L, await ProvisionedRowCountAsync(db));

        Assert.True((await boundary.ProvisionAsync(package)).Succeeded);
        TargetEventProvisioningRecord additional = AdditionalEvent(package);
        TargetProvisioningResult persistedConflict = await boundary.ProvisionAsync(package with
        {
            CorrelationId = "corr-delta-unit-conflict",
            StationProfile = package.StationProfile with { UnitCount = 4 },
            Units = package.Units.Take(4).ToArray(),
            RuntimeBaselines = package.RuntimeBaselines.Take(4).ToArray(),
            Events = [.. package.Events, additional]
        });

        Assert.Equal(TargetProvisioningOutcome.Rejected, persistedConflict.Outcome);
        Assert.Equal(TargetProvisioningFailure.Conflict, persistedConflict.Failure);
        Assert.Contains("unit-mapping-conflict", persistedConflict.Issues);
        Assert.Equal(5L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM Units;"));
        Assert.Equal(0L, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Events WHERE EventId='{additional.EventId}';"));
    }

    [Fact]
    public async Task Event_record_conflict_rolls_back_new_records_and_preserves_existing_event()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package("delta", "Synthetic Delta Station", 5);
        Assert.True((await boundary.ProvisionAsync(package)).Succeeded);
        TargetEventProvisioningRecord additional = AdditionalEvent(package);
        TargetEventProvisioningRecord conflicting = package.Events[0] with { Remark = "conflicting-event" };

        TargetProvisioningResult result = await boundary.ProvisionAsync(package with
        {
            CorrelationId = "corr-delta-event-conflict",
            Events = [conflicting, .. package.Events.Skip(1), additional]
        });

        Assert.Equal(TargetProvisioningOutcome.Rejected, result.Outcome);
        Assert.Equal(TargetProvisioningFailure.Conflict, result.Failure);
        Assert.Contains("event-record-conflict", result.Issues);
        Assert.Equal(2L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM Events;"));
        Assert.Equal(0L, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Events WHERE EventId='{additional.EventId}';"));
        Assert.Equal("synthetic", await ScalarAsync(db,
            $"SELECT Remark FROM Events WHERE EventId='{package.Events[0].EventId}';"));
    }

    [Fact]
    public async Task Finalized_snapshot_conflict_rolls_back_new_records_and_preserves_immutable_snapshot()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package("delta", "Synthetic Delta Station", 5);
        Assert.True((await boundary.ProvisionAsync(package)).Succeeded);
        TargetEventProvisioningRecord additional = AdditionalEvent(package);
        TargetFinalizedSnapshotProvisioningRecord conflicting = package.FinalizedSnapshots[0] with
        {
            CanonicalJson = "{\"synthetic\":false}",
            ChecksumValue = "conflicting-snapshot-checksum"
        };

        TargetProvisioningResult result = await boundary.ProvisionAsync(package with
        {
            CorrelationId = "corr-delta-snapshot-conflict",
            Events = [.. package.Events, additional],
            FinalizedSnapshots = [conflicting]
        });

        Assert.Equal(TargetProvisioningOutcome.Rejected, result.Outcome);
        Assert.Equal(TargetProvisioningFailure.Conflict, result.Failure);
        Assert.Contains("snapshot-record-conflict", result.Issues);
        Assert.Equal(1L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM ReportSnapshots;"));
        Assert.Equal("{\"synthetic\":true}", await ScalarAsync(db,
            $"SELECT CanonicalJson FROM ReportSnapshots WHERE SnapshotId='{package.FinalizedSnapshots[0].SnapshotId}';"));
        Assert.Equal(0L, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Events WHERE EventId='{additional.EventId}';"));
    }

    [Fact]
    public async Task Finalized_lock_conflict_rolls_back_new_records_and_preserves_immutable_lock()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package("delta", "Synthetic Delta Station", 5);
        Assert.True((await boundary.ProvisionAsync(package)).Succeeded);
        TargetEventProvisioningRecord additional = AdditionalEvent(package);
        TargetFinalizedLockProvisioningRecord conflicting = package.FinalizedLocks[0] with
        {
            ActorIdentity = package.ShiftProfiles[1].Profile.ShiftProfileId
        };

        TargetProvisioningResult result = await boundary.ProvisionAsync(package with
        {
            CorrelationId = "corr-delta-lock-conflict",
            Events = [.. package.Events, additional],
            FinalizedLocks = [conflicting]
        });

        Assert.Equal(TargetProvisioningOutcome.Rejected, result.Outcome);
        Assert.Equal(TargetProvisioningFailure.Conflict, result.Failure);
        Assert.Contains("lock-record-conflict", result.Issues);
        Assert.Equal(1L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM ReportPeriodLocks;"));
        Assert.Equal(package.FinalizedLocks[0].ActorIdentity, await ScalarAsync(db,
            "SELECT ActorIdentity FROM ReportPeriodLocks;"));
        Assert.Equal(0L, await ScalarLongAsync(db, $"SELECT COUNT(*) FROM Events WHERE EventId='{additional.EventId}';"));
    }

    private static TargetEventProvisioningRecord AdditionalEvent(TargetStationProvisioningPackage package) =>
        new("01ARZ3NDEKTSV4RRFFQ69G5FAX", package.StationId, package.Units[0].UnitId,
            "OH", 14050101, 3, 3, "transaction-probe", Now,
            package.ShiftProfiles[0].Profile.ShiftProfileId);

    private static TargetStationProvisioningPackage Package(string stationKey, string stationName, int unitCount)
    {
        string stationId = $"station-{stationKey}";
        var units = Enumerable.Range(1, unitCount)
            .Select(i => new TargetUnitProvisioningRecord(stationId, $"{stationId}-unit-{i}", i, $"Unit {i}", true, 1))
            .ToArray();
        var profiles = new List<TargetShiftProfileProvisioningRecord>();
        for (int i = 1; i <= 2; i++)
        {
            string profileId = $"{stationKey}-shift-{i}";
            ShiftProfile profile = new(profileId, stationId, i, $"Shift {i}", "First", "Supervisor",
                $"{1000 + i}", true, Now, Now, 1);
            byte[] salt = [(byte)i, 2, 3, 4];
            profiles.Add(new(profile, new(profileId, 1, "PBKDF2-SHA256", "iterations=100000;length=32",
                salt, Pbkdf2TargetPasswordVerifier.CreateVerifier($"shift-secret-{i}", salt), true, Now, null)));
        }
        byte[] managementSalt = [9, 8, 7, 6];
        ManagementCredentialRecord management = new(1, "PBKDF2-SHA256", "iterations=100000;length=32",
            managementSalt, Pbkdf2TargetPasswordVerifier.CreateVerifier("management-secret", managementSalt),
            true, true, Now, Now, null);
        var events = units.Take(2).Select((unit, i) => new TargetEventProvisioningRecord(
            i == 0 ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : "01ARZ3NDEKTSV4RRFFQ69G5FAW",
            stationId, unit.UnitId, i == 0 ? "START" : "NSD", 14050101, i + 1, i + 1,
            "synthetic", Now, profiles[0].Profile.ShiftProfileId)).ToArray();
        TargetFinalizedSnapshotProvisioningRecord snapshot = new("snapshot-" + stationKey, "report-" + stationKey,
            stationId, 1, 2, "Monthly", 1, null, 1, "{\"synthetic\":true}", "SHA256", "v1",
            "snapshot-checksum", 18, "source-revision", Now);
        TargetFinalizedLockProvisioningRecord reportLock = new(stationId, 1, 2, "Monthly",
            snapshot.SnapshotId, 1, "finalization-" + stationKey, Now, profiles[0].Profile.ShiftProfileId);
        return new("manifest-" + stationKey, "corr-" + stationKey,
            new("profile-" + stationKey, unitCount), stationId, stationName, Now,
            units, profiles, management, new("device-" + stationKey + "-000001", Now, 1),
            new("vendor-key-" + stationKey, [1, 2, 3, 4], "ECDSA-P256-SHA256", Now, null, Now, 1),
            units.Select(x => new TargetRuntimeBaselineProvisioningRecord(x.UnitId, "Stopped", 0,
                "baseline-v1", "synthetic-qualification")).ToArray(), events, "2.5", [snapshot], [reportLock],
            "approval-management", "owner-data", "review-security");
    }

    private static async Task ApplyTargetSchemaAsync(TemporarySqliteDatabase db)
    {
        var checksums = new Sha256ChecksumService();
        var runner = new MigrationRunner(new SqliteTransactionManager(db.Factory),
            new MigrationChecksumValidator(checksums));
        await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksums));
    }

    private static async Task<string> ScalarAsync(TemporarySqliteDatabase db, string sql)
    {
        await using SqliteConnection connection = await db.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarLongAsync(TemporarySqliteDatabase db, string sql) =>
        Convert.ToInt64(await ScalarAsync(db, sql));

    private static Task<long> ProvisionedRowCountAsync(TemporarySqliteDatabase db) => ScalarLongAsync(db, """
        SELECT
            (SELECT COUNT(*) FROM Stations) +
            (SELECT COUNT(*) FROM Units) +
            (SELECT COUNT(*) FROM SecurityShiftProfiles) +
            (SELECT COUNT(*) FROM SecurityShiftProfileCredentials) +
            (SELECT COUNT(*) FROM SecurityManagementCredentials) +
            (SELECT COUNT(*) FROM SecurityDeviceIdentity) +
            (SELECT COUNT(*) FROM SecurityTrustedVendorPublicKeys) +
            (SELECT COUNT(*) FROM SecurityDeploymentSettings) +
            (SELECT COUNT(*) FROM Events) +
            (SELECT COUNT(*) FROM ReportSnapshots) +
            (SELECT COUNT(*) FROM ReportPeriodLocks);
        """);
}
