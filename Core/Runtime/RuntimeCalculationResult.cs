using Rah_Negar.Core.Event.Comparison;

namespace Rah_Negar.Core.Runtime;

public sealed record ValidatedEventChain(
    string StationId,
    string UnitId,
    IReadOnlyList<NormalizedEvent> Events,
    UnitOperationalState InitialState,
    UnitOperationalState ResultingState,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors)
{
    public static ValidatedEventChain Valid(
        string stationId, string unitId, IReadOnlyList<NormalizedEvent> events,
        UnitOperationalState initialState, UnitOperationalState resultingState) =>
        new(stationId, unitId, events, initialState, resultingState, true, Array.Empty<string>());

    public static ValidatedEventChain Invalid(
        string stationId, string unitId, IReadOnlyList<NormalizedEvent> events,
        UnitOperationalState initialState, params string[] errors)
    {
        if (errors.Length == 0) throw new ArgumentException("An invalid chain requires an error.", nameof(errors));
        return new(stationId, unitId, events, initialState, initialState, false, errors);
    }
}

public sealed record RuntimeCalculationRequest(
    ValidatedEventChain EventChain,
    RuntimeBaseline Baseline,
    RuntimeCalculationPolicy Policy,
    long PeriodStartMinute,
    long PeriodEndMinute);

public sealed record RuntimeCalculationError(string Code, string Message);

public sealed class RuntimeCalculationResult
{
    private RuntimeCalculationResult(RuntimeProjection? projection, IReadOnlyList<RuntimeCalculationError> errors)
    {
        Projection = projection;
        Errors = errors;
    }

    public bool IsSuccess => Projection is not null && Errors.Count == 0;
    public RuntimeProjection? Projection { get; }
    public IReadOnlyList<RuntimeCalculationError> Errors { get; }

    public static RuntimeCalculationResult Success(RuntimeProjection projection) =>
        new(projection ?? throw new ArgumentNullException(nameof(projection)), Array.Empty<RuntimeCalculationError>());

    public static RuntimeCalculationResult Failure(params RuntimeCalculationError[] errors)
    {
        if (errors.Length == 0) throw new ArgumentException("A failure requires an error.", nameof(errors));
        return new(null, errors);
    }
}
