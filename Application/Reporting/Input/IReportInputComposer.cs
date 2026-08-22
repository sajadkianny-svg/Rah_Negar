using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Foundation.Application.Reporting.Input;

public sealed record ReportInputCompositionRequest(
    ReportIdentity Identity,
    DateTimeOffset CalculationTimestamp);

public sealed class ReportInputCompositionResult
{
    private ReportInputCompositionResult(NormalizedReportInput? input, IReadOnlyList<ReportingInputFailure> failures)
    {
        Input = input;
        Failures = failures;
    }

    public bool IsSuccess => Input is not null && Failures.Count == 0;
    public NormalizedReportInput? Input { get; }
    public IReadOnlyList<ReportingInputFailure> Failures { get; }

    public static ReportInputCompositionResult Success(NormalizedReportInput input) =>
        new(input ?? throw new ArgumentNullException(nameof(input)), Array.Empty<ReportingInputFailure>());

    public static ReportInputCompositionResult Failed(IEnumerable<ReportingInputFailure> failures)
    {
        ReportingInputFailure[] ordered = failures.OrderBy(x => x.Source, StringComparer.Ordinal)
            .ThenBy(x => x.UnitId, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("A failed composition requires a failure.", nameof(failures));
        return new(null, Array.AsReadOnly(ordered));
    }
}

public interface IReportInputComposer
{
    Task<ReportInputCompositionResult> ComposeAsync(
        ReportInputCompositionRequest request, CancellationToken cancellationToken = default);
}
