using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Foundation.Application.Runtime;

public sealed class RuntimeCalculatorFoundation : IRuntimeCalculator
{
    public RuntimeCalculationResult Calculate(RuntimeCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.EventChain.IsValid)
            return RuntimeCalculationResult.Failure(new RuntimeCalculationError("runtime.event-chain.invalid", "Runtime requires a fully validated Event chain."));
        if (request.PeriodEndMinute <= request.PeriodStartMinute)
            return RuntimeCalculationResult.Failure(new RuntimeCalculationError("runtime.period.invalid", "Runtime period end must be after its start."));
        if (request.Baseline.State.StationId != request.EventChain.StationId ||
            request.Baseline.State.UnitId != request.EventChain.UnitId)
            return RuntimeCalculationResult.Failure(new RuntimeCalculationError("runtime.baseline.identity-mismatch", "Baseline and Event chain must identify the same Station and Unit."));
        if (request.Baseline.State.OperationalState != request.EventChain.InitialState)
            return RuntimeCalculationResult.Failure(new RuntimeCalculationError("runtime.baseline.state-mismatch", "Baseline state must equal the validated chain initial state."));
        if (request.EventChain.Events.Count != 0)
            return RuntimeCalculationResult.Failure(new RuntimeCalculationError("runtime.projection.nonempty-not-implemented", "Non-empty Event projection is intentionally deferred beyond the domain foundation."));

        var state = request.Baseline.State;
        return RuntimeCalculationResult.Success(new RuntimeProjection(
            state.StationId, state.UnitId, request.PeriodStartMinute, request.PeriodEndMinute,
            TimeSpan.Zero, TimeSpan.Zero, state.CumulativePhysicalRuntime, state.CumulativeEsdAdjustment,
            state.RuntimeAfterOh, 0, TimeSpan.Zero, state.OperationalState,
            request.Policy.CalculationPolicyVersion));
    }
}
