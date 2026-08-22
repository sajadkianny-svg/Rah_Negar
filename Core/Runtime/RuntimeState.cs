namespace Rah_Negar.Core.Runtime;

public sealed record RuntimeState(
    string StationId,
    string UnitId,
    long EffectiveAtMinute,
    UnitOperationalState OperationalState,
    TimeSpan CumulativePhysicalRuntime,
    TimeSpan CumulativeEsdAdjustment,
    TimeSpan RuntimeAfterOh,
    long? OpenRunStartedAtMinute = null)
{
    public TimeSpan CumulativeAdjustedRuntime => CumulativePhysicalRuntime + CumulativeEsdAdjustment;
}

public sealed record RuntimeBaseline(
    RuntimeState State,
    string BaselineVersion,
    string Provenance);
