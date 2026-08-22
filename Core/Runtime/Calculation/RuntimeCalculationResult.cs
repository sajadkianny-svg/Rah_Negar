namespace Rah_Negar.Core.Runtime.Calculation;

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

    public static RuntimeCalculationResult Failure(string code, string message) =>
        new(null, new[] { new RuntimeCalculationError(code, message) });
}
