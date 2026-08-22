namespace Rah_Negar.Core.Event.Comparison;

public sealed record EventComparisonResult(
    DifferenceCategory Category,
    IReadOnlyList<string> Differences,
    int LegacyEventCount,
    int TargetEventCount,
    bool LegacyChainIsValid,
    bool TargetChainIsValid,
    EventOperationalState LegacyFinalState,
    EventOperationalState TargetFinalState)
{
    public bool IsEquivalent => Category == DifferenceCategory.Equivalent;
}
