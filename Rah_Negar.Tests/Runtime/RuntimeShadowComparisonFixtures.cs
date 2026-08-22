using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Core.Runtime.Comparison;

namespace Rah_Negar.Tests.Runtime;

internal static class RuntimeShadowComparisonFixtures
{
    private static readonly RuntimeCalculator Calculator = new();

    public static IEnumerable<object[]> MatchingScenarios()
    {
        yield return Scenario("normal-start-nsd", UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 300, 0, 0,
            EventAt("start", EventType.Start, 60), EventAt("nsd", EventType.Nsd, 180));
        yield return Scenario("start-esd", UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 300, 45, 0,
            EventAt("start", EventType.Start, 60), EventAt("esd", EventType.Esd, 180));
        yield return Scenario("oh", UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 600, 0, 500,
            EventAt("start-1", EventType.Start, 50), EventAt("nsd-1", EventType.Nsd, 100),
            EventAt("oh", EventType.Oh, 200), EventAt("start-2", EventType.Start, 300),
            EventAt("nsd-2", EventType.Nsd, 360));
        yield return Scenario("running-baseline", UnitOperationalState.Running, UnitOperationalState.Stopped, 30, 240, 0, 20,
            EventAt("nsd", EventType.Nsd, 120));
        yield return Scenario("cross-midnight", UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 2_000, 0, 0,
            EventAt("start", EventType.Start, 1_380), EventAt("nsd", EventType.Nsd, 1_500));
    }

    public static object[] IntentionalDifference()
    {
        object[] scenario = Scenario("intentional-esd-policy", UnitOperationalState.Stopped, UnitOperationalState.Stopped,
            0, 300, 120, 0, EventAt("start", EventType.Start, 60), EventAt("esd", EventType.Esd, 180));
        var target = (RuntimeSnapshot)scenario[2];
        RuntimeSnapshot legacy = target with
        {
            SourceName = "legacy-synthetic",
            EsdAdjustmentMinutes = 100,
            AdjustedRuntimeMinutes = target.PhysicalRuntimeMinutes + 100,
            RuntimeAfterOhMinutes = target.RuntimeAfterOhMinutes - 20,
            CalculationVersion = "legacy-fixture-v1"
        };
        return new object[] { (string)scenario[0], legacy, target };
    }

    private static object[] Scenario(
        string name,
        UnitOperationalState initial,
        UnitOperationalState resulting,
        long periodStart,
        long periodEnd,
        long esdAdjustment,
        long baselineAfterOh,
        params NormalizedEvent[] events)
    {
        var chain = ValidatedEventChain.Valid("station-rasht", "unit-1", events, initial, resulting);
        var context = new RuntimeCalculationContext(
            chain,
            BaselineMinute: 0,
            BaselineState: initial,
            BaselineTotalRuntimeMinutes: 1_000,
            BaselineRuntimeAfterOhMinutes: baselineAfterOh,
            PeriodStartMinute: periodStart,
            PeriodEndMinute: periodEnd,
            CurrentEsdAdjustmentMinutes: esdAdjustment,
            EventChainVersion: $"events-{name}-v1",
            BaselineVersion: "baseline-v1",
            PolicyVersion: "policy-v1",
            CalculationVersion: "runtime-4.2-v1",
            CalculationTimestamp: new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero));
        Rah_Negar.Core.Runtime.Calculation.RuntimeCalculationResult calculated = Calculator.Calculate(context);
        if (!calculated.IsSuccess)
            throw new InvalidOperationException(calculated.Errors[0].Message);

        RuntimeSnapshot target = RuntimeSnapshotNormalizer.FromProjection(
            calculated.Projection!, "new-engine-synthetic", context.EventChainVersion);
        RuntimeSnapshot legacy = target with
        {
            SourceName = "legacy-synthetic",
            CalculationVersion = "legacy-fixture-v1"
        };
        return new object[] { name, legacy, target };
    }

    private static NormalizedEvent EventAt(string id, EventType type, long minute) =>
        new(id, "station-rasht", "unit-1", type, 14050101,
            checked((int)(minute % 1_440)), minute, checked((int)minute), Array.Empty<string>());
}
