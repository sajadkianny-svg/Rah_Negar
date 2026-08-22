using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Pilot.Hosting;

/// <summary>
/// Explicitly invoked pilot host. It owns no startup registration, database locator, feature flag,
/// production route, or mutation service.
/// </summary>
public sealed class PilotExecutionCoordinator : IPilotHost
{
    private readonly IClock _clock;
    private readonly IReadOnlyDictionary<PilotFeature, IPilotWorkflowExecutor> _executors;

    public PilotExecutionCoordinator(IClock clock, IEnumerable<IPilotWorkflowExecutor> executors)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentNullException.ThrowIfNull(executors);
        IPilotWorkflowExecutor[] supplied = executors.ToArray();
        if (supplied.Any(executor => executor is null))
            throw new ArgumentException("Pilot executors cannot contain null.", nameof(executors));
        if (supplied.Any(executor => !Enum.IsDefined(executor.Feature)))
            throw new ArgumentException("Pilot executors must declare a known feature.", nameof(executors));
        if (supplied.GroupBy(executor => executor.Feature).Any(group => group.Count() != 1))
            throw new ArgumentException("Each pilot feature may have only one executor.", nameof(executors));
        _executors = new ReadOnlyDictionary<PilotFeature, IPilotWorkflowExecutor>(
            supplied.ToDictionary(executor => executor.Feature));
    }

    public bool AutomaticallyRuns => false;
    public bool RegisteredInProductionStartup => false;
    public bool SelectsDatabase => false;
    public bool ActivatesFeatures => false;

    public async Task<PilotExecutionResult> ExecuteAsync(
        PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset started = _clock.UtcNow.ToUniversalTime();
        if (request is null)
            return Blocked(string.Empty, default, string.Empty, started, ["pilot-host-request-required"]);
        PilotExecutionContext? context = request.Context;
        if (context is null)
            return Blocked(string.Empty, request.Feature, string.Empty, started, ["pilot-context-required"]);

        var reasons = new List<string>();
        PilotContextValidationResult contextResult = PilotExecutionContextValidator.Validate(context, started);
        reasons.AddRange(contextResult.Issues);
        if (!Enum.IsDefined(request.Feature)) reasons.Add("unknown-pilot-feature");
        if (request.Input is null) reasons.Add("pilot-workflow-input-required");
        if (!_executors.TryGetValue(request.Feature, out IPilotWorkflowExecutor? executor))
            reasons.Add("pilot-workflow-not-configured");
        else if (request.Input is not null && executor.InputType != request.Input.GetType())
            reasons.Add("pilot-workflow-input-type-mismatch");
        reasons.AddRange(PilotPermitValidator.Validate(
            request.Permit, context, request.Feature, started));
        if (reasons.Count > 0)
            return Blocked(context.PilotId, request.Feature, context.CorrelationId,
                started, reasons);

        try
        {
            PilotWorkflowAdapterExecution execution = await executor!.ExecuteAsync(
                request, cancellationToken).ConfigureAwait(false);
            DateTimeOffset completed = _clock.UtcNow.ToUniversalTime();
            IReadOnlyList<string> executionIssues = ValidateExecution(context, request.Feature, execution);
            if (executionIssues.Count > 0)
                return new(context.PilotId, request.Feature, PilotExecutionStatus.Blocked,
                    IsSafeObservation(execution.Legacy) ? execution.Legacy : null, null,
                    new(false, ShadowDifferenceSeverity.Failed,
                        "Pilot adapter result was rejected.", executionIssues), null,
                    context.CorrelationId, started, completed, executionIssues);
            PilotComparisonResult comparison = BuildComparison(execution);
            PilotExecutionStatus status = execution.Decision != IntegrationControlDecision.Allowed
                ? execution.TargetFailed ? PilotExecutionStatus.TargetFailed : PilotExecutionStatus.Blocked
                : comparison.IsMatch ? PilotExecutionStatus.Completed : PilotExecutionStatus.CompletedWithDifference;
            return new(context.PilotId, request.Feature, status, execution.Legacy,
                execution.Target, comparison, execution.Evidence?.EvidenceId, context.CorrelationId,
                started, completed, execution.Reasons);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(context.PilotId, request.Feature, PilotExecutionStatus.Failed,
                null, null, FailedComparison("Pilot workflow did not complete."), null,
                context.CorrelationId, started, _clock.UtcNow.ToUniversalTime(),
                ["pilot-workflow-failed"]);
        }
    }

    private static PilotComparisonResult BuildComparison(PilotWorkflowAdapterExecution execution)
    {
        if (execution.Evidence is { } evidence)
        {
            bool match = evidence.ComparisonSeverity == ShadowDifferenceSeverity.None;
            return new(match, evidence.ComparisonSeverity,
                evidence.OperatorVisibleSafeMessage,
                match ? Array.Empty<string>() : ["legacy-target-difference"]);
        }
        if (execution.TargetFailed)
            return FailedComparison("Target observation failed; legacy authority was preserved.");
        return new(false, ShadowDifferenceSeverity.Failed,
            "Pilot execution was blocked.", execution.Reasons);
    }

    private static PilotComparisonResult FailedComparison(string message) =>
        new(false, ShadowDifferenceSeverity.Failed, message, ["comparison-unavailable"]);

    private static IReadOnlyList<string> ValidateExecution(PilotExecutionContext context,
        PilotFeature feature, PilotWorkflowAdapterExecution execution)
    {
        var issues = new List<string>();
        if (!Enum.IsDefined(execution.Decision)) issues.Add("unknown-workflow-decision");
        if (execution.Legacy is not null && !IsSafeObservation(execution.Legacy))
            issues.Add("legacy-observation-metadata-invalid");
        if (execution.Target is not null && !IsSafeObservation(execution.Target))
            issues.Add("target-observation-metadata-invalid");
        if (execution.Evidence is not null &&
            PilotEvidenceValidator.Validate(execution.Evidence, context) != PilotEvidenceState.Complete)
            issues.Add("pilot-evidence-invalid");
        if (execution.Decision == IntegrationControlDecision.Allowed &&
            (execution.Legacy is null || execution.Target is null || execution.Evidence is null))
            issues.Add("completed-workflow-evidence-incomplete");
        if (execution.TargetFailed && (execution.Legacy is null || execution.Target is not null ||
            execution.Decision == IntegrationControlDecision.Allowed))
            issues.Add("target-failure-evidence-invalid");
        if (execution.Evidence is not null && execution.Evidence.Feature != feature)
            issues.Add("workflow-evidence-feature-mismatch");
        return issues.AsReadOnly();
    }

    private static bool IsSafeObservation(PilotObservationResult? observation) => observation is not null &&
        observation.ResultFingerprint.Length == 64 &&
        observation.ResultFingerprint.All(Uri.IsHexDigit) &&
        IsSafeCategory(observation.SafeStatus) &&
        !string.IsNullOrWhiteSpace(observation.Metadata.AdapterId) &&
        !string.IsNullOrWhiteSpace(observation.Metadata.AdapterVersion) &&
        !string.IsNullOrWhiteSpace(observation.Metadata.SourceVersion) &&
        observation.Metadata.ObservedAtUtc.Offset == TimeSpan.Zero &&
        observation.Metadata.ReadOnly && observation.Metadata.PreservesLegacyAuthority;

    private static bool IsSafeCategory(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 && value.All(character => char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-');

    private PilotExecutionResult Blocked(
        string pilotId,
        PilotFeature feature,
        string correlationId,
        DateTimeOffset started,
        IEnumerable<string> reasons) =>
        new(pilotId, feature, PilotExecutionStatus.Blocked, null, null,
            new(false, ShadowDifferenceSeverity.Failed, "Pilot execution was blocked.", reasons),
            null, correlationId, started, _clock.UtcNow.ToUniversalTime(), reasons);
}
