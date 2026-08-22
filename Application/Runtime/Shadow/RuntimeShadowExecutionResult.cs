using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Core.Runtime.Comparison;

namespace Rah_Negar.Foundation.Application.Runtime.Shadow;

public enum RuntimeShadowExecutionStatus
{
    Match,
    DifferenceDetected,
    InputUnavailable,
    LegacyUnavailable,
    NewEngineFailure,
    ComparisonFailure
}

public sealed record RuntimeShadowEvidenceMetadata(
    string ExecutionId,
    string DatabaseCopyId,
    string SourceFingerprint,
    DateTimeOffset CopyCapturedAt,
    string EventChainVersion,
    string BaselineVersion,
    string PolicyVersion,
    string CalculationVersion,
    DateTimeOffset ExecutionTimestamp);

public sealed record RuntimeShadowExecutionResult(
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    RuntimeShadowExecutionStatus Status,
    RuntimeSnapshot? LegacySnapshot,
    RuntimeProjection? NewProjection,
    RuntimeComparisonResult? ComparisonResult,
    RuntimeShadowEvidenceMetadata? Evidence,
    string? ErrorCode,
    string? ErrorMessage);
