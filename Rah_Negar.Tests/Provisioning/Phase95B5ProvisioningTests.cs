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

public sealed class Phase95B5ProvisioningTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TargetStationCode.Rasht, 3)]
    [InlineData(TargetStationCode.Ramsar, 4)]
    public void Manifest_covers_the_station_shape_without_exposing_sensitive_material(
        TargetStationCode station, int expectedUnits)
    {
        TargetStationProvisioningPackage package = Package(station, expectedUnits);
        TargetProvisioningValidationResult result = TargetStationProvisioningManifestBuilder.Validate(package);

        Assert.True(result.IsValid, string.Join(",", result.Issues));
        TargetStationProvisioningManifest manifest = result.Manifest!;
        Assert.Equal(expectedUnits, manifest.ExpectedUnitCount);
        Assert.Equal(expectedUnits, manifest.EntityCounts["Units"]);
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
        TargetStationProvisioningPackage package = Package(TargetStationCode.Rasht, 3);
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
    [InlineData(TargetStationCode.Rasht, 3)]
    [InlineData(TargetStationCode.Ramsar, 4)]
    public async Task Provisioning_is_transactional_idempotent_and_preserves_finalized_snapshot_and_lock(
        TargetStationCode station, int expectedUnits)
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ApplyTargetSchemaAsync(db);
        var boundary = new SQLiteTargetStationProvisioningBoundary(db.Factory);
        TargetStationProvisioningPackage package = Package(station, expectedUnits);
        string stationId = package.StationId;
        string snapshotId = "snapshot-" + station.ToString().ToLowerInvariant();

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
        TargetStationProvisioningPackage package = Package(TargetStationCode.Rasht, 3);
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

    private static TargetStationProvisioningPackage Package(TargetStationCode station, int unitCount)
    {
        string suffix = station.ToString().ToLowerInvariant();
        string stationId = $"station-{suffix}";
        var units = Enumerable.Range(1, unitCount)
            .Select(i => new TargetUnitProvisioningRecord(stationId, $"{stationId}-unit-{i}", i, $"Unit {i}", true, 1))
            .ToArray();
        var profiles = new List<TargetShiftProfileProvisioningRecord>();
        for (int i = 1; i <= 2; i++)
        {
            string profileId = $"{suffix}-shift-{i}";
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
        TargetFinalizedSnapshotProvisioningRecord snapshot = new("snapshot-" + suffix, "report-" + suffix,
            stationId, 1, 2, "Monthly", 1, null, 1, "{\"synthetic\":true}", "SHA256", "v1",
            "snapshot-checksum", 18, "source-revision", Now);
        TargetFinalizedLockProvisioningRecord reportLock = new(stationId, 1, 2, "Monthly",
            snapshot.SnapshotId, 1, "finalization-" + suffix, Now, profiles[0].Profile.ShiftProfileId);
        return new("manifest-" + suffix, "corr-" + suffix, station, stationId, station + " Station", Now,
            units, profiles, management, new("device-" + suffix + "-000001", Now, 1),
            new("vendor-key-" + suffix, [1, 2, 3, 4], "ECDSA-P256-SHA256", Now, null, Now, 1),
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
}
