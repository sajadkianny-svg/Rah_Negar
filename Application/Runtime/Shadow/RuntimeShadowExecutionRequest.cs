namespace Rah_Negar.Foundation.Application.Runtime.Shadow;

public sealed record RuntimeShadowExecutionRequest(
    IRuntimeShadowInputSource InputSource,
    string StationId,
    IReadOnlyList<string> UnitIds,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string ExecutionId);
