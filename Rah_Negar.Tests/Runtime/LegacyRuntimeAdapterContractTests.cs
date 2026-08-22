using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Comparison;
using Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;

namespace Rah_Negar.Tests.Runtime;

public sealed class LegacyRuntimeAdapterContractTests
{
    [Fact]
    public void SnapshotCreation_PreservesRawLegacyEvidence()
    {
        LegacyRuntimeSnapshot snapshot = ValidSnapshot();

        Assert.Equal("legacy-runtime", snapshot.SourceName);
        Assert.Equal(2.0, snapshot.PhysicalRuntimeHours);
        Assert.Equal(0.5, snapshot.EsdAdjustmentHours);
        Assert.Equal(UnitOperationalState.Stopped, snapshot.FinalState);
    }

    [Fact]
    public void Normalization_MapsExactHoursToAuthoritativeMinutes()
    {
        RuntimeSnapshot normalized = Normalize(ValidSnapshot());

        Assert.Equal(120, normalized.PhysicalRuntimeMinutes);
        Assert.Equal(30, normalized.EsdAdjustmentMinutes);
        Assert.Equal(150, normalized.AdjustedRuntimeMinutes);
        Assert.Equal(90, normalized.RuntimeAfterOhMinutes);
        Assert.Equal(120, normalized.LongestRunMinutes);
        Assert.Equal(2, normalized.ServiceDayCount);
    }

    [Fact]
    public void Normalization_MissingFieldFailsWithoutInference()
    {
        LegacyRuntimeSnapshot incomplete = ValidSnapshot() with { PhysicalRuntimeHours = null };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Normalize(incomplete));

        Assert.Contains("PhysicalRuntimeHours", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalization_IdentityMismatchIsRejected()
    {
        LegacyRuntimeSnapshot wrongUnit = ValidSnapshot() with { UnitId = "unit-2" };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Normalize(wrongUnit));

        Assert.Contains("UnitId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalization_PeriodMismatchIsRejected()
    {
        LegacyRuntimeSnapshot wrongPeriod = ValidSnapshot() with { PeriodEndMinute = 2_001 };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Normalize(wrongPeriod));

        Assert.Contains("period", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalization_RoundedDisplayHoursCannotBecomeMinuteAuthority()
    {
        LegacyRuntimeSnapshot displayOnly = ValidSnapshot() with
        {
            PhysicalRuntimeHours = 2.08,
            AdjustedRuntimeHours = 2.58
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Normalize(displayOnly));

        Assert.Contains("integral minutes", error.Message, StringComparison.Ordinal);
    }

    private static RuntimeSnapshot Normalize(LegacyRuntimeSnapshot snapshot) =>
        LegacyRuntimeSnapshotNormalizer.Normalize(
            snapshot,
            "station-rasht",
            "unit-1",
            1_000,
            2_000,
            "events-v1");

    private static LegacyRuntimeSnapshot ValidSnapshot() =>
        new(
            SourceName: "legacy-runtime",
            StationId: "station-rasht",
            UnitId: "unit-1",
            PeriodStartMinute: 1_000,
            PeriodEndMinute: 2_000,
            EventBoundaryVersion: "events-v1",
            PhysicalRuntimeHours: 2.0,
            EsdAdjustmentHours: 0.5,
            AdjustedRuntimeHours: 2.5,
            RuntimeAfterOhHours: 1.5,
            LongestRunHours: 2.0,
            ServiceDayCount: 2,
            FinalState: UnitOperationalState.Stopped,
            CalculationVersion: "legacy-audit-v1");
}
