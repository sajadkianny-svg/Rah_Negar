using System.Collections.ObjectModel;

namespace Rah_Negar.Foundation.Application.Integration;

public enum ShadowDifferenceSeverity
{
    None,
    Informational,
    Warning,
    Critical,
    Failed
}

public sealed record ShadowComparisonDifference(
    string Code,
    string SafeDescription);

public sealed record ShadowComparisonAssessment(
    string ComparisonFingerprint,
    ShadowDifferenceSeverity Severity,
    IReadOnlyList<ShadowComparisonDifference> Differences);

public sealed record ShadowComparisonEvidenceMetadata(
    string EvidenceId,
    string CorrelationId,
    ControlledIntegrationFeature Feature,
    string TargetScope,
    DateTimeOffset ObservedAtUtc,
    string LegacyVersion,
    string TargetVersion);

public interface ILegacyShadowResultReader<in TRequest, TResult>
{
    Task<TResult> ReadAuthoritativeAsync(TRequest request, CancellationToken cancellationToken = default);
}

public interface IReadOnlyTargetShadowEvaluator<in TRequest, TResult>
{
    Task<TResult> EvaluateReadOnlyAsync(TRequest request, CancellationToken cancellationToken = default);
}

public interface IShadowResultComparer<in TLegacy, in TTarget>
{
    ShadowComparisonAssessment Compare(TLegacy legacyResult, TTarget targetResult);
}

public sealed class GeneralizedShadowComparisonResult<TLegacy, TTarget>
{
    public GeneralizedShadowComparisonResult(
        bool succeeded,
        TLegacy? legacyResult,
        TTarget? targetResult,
        ShadowComparisonAssessment assessment,
        ShadowComparisonEvidenceMetadata evidence,
        string resultCategory)
    {
        Succeeded = succeeded;
        LegacyResult = legacyResult;
        TargetResult = targetResult;
        Assessment = new(assessment.ComparisonFingerprint, assessment.Severity,
            new ReadOnlyCollection<ShadowComparisonDifference>(assessment.Differences.ToArray()));
        Evidence = evidence;
        ResultCategory = resultCategory;
    }

    public bool Succeeded { get; }
    public bool LegacyRemainsAuthoritative => true;
    public bool TargetProductionMutationAllowed => false;
    public TLegacy? LegacyResult { get; }
    public TTarget? TargetResult { get; }
    public ShadowComparisonAssessment Assessment { get; }
    public ShadowComparisonEvidenceMetadata Evidence { get; }
    public string ResultCategory { get; }
}

public sealed class GeneralizedShadowComparisonCoordinator<TRequest, TLegacy, TTarget>
{
    private readonly ILegacyShadowResultReader<TRequest, TLegacy> _legacy;
    private readonly IReadOnlyTargetShadowEvaluator<TRequest, TTarget> _target;
    private readonly IShadowResultComparer<TLegacy, TTarget> _comparer;

    public GeneralizedShadowComparisonCoordinator(
        ILegacyShadowResultReader<TRequest, TLegacy> legacy,
        IReadOnlyTargetShadowEvaluator<TRequest, TTarget> target,
        IShadowResultComparer<TLegacy, TTarget> comparer)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    public async Task<GeneralizedShadowComparisonResult<TLegacy, TTarget>> CompareAsync(
        TRequest request,
        ShadowComparisonEvidenceMetadata evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEvidence(evidence);
        TLegacy? legacy = default;
        TTarget? target = default;
        try
        {
            legacy = await _legacy.ReadAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            target = await _target.EvaluateReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            ShadowComparisonAssessment assessment = _comparer.Compare(legacy, target);
            ValidateAssessment(assessment);
            return new(true, legacy, target, assessment, evidence,
                assessment.Severity == ShadowDifferenceSeverity.None ? "ShadowMatch" : "ShadowDifference");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(false, legacy, target,
                new("comparison-unavailable", ShadowDifferenceSeverity.Failed,
                    [new("shadow-comparison-failed", "Shadow comparison did not complete.")]),
                evidence, "ShadowComparisonFailed");
        }
    }

    private static void ValidateEvidence(ShadowComparisonEvidenceMetadata evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.EvidenceId) ||
            string.IsNullOrWhiteSpace(evidence.CorrelationId) ||
            string.IsNullOrWhiteSpace(evidence.TargetScope) ||
            string.IsNullOrWhiteSpace(evidence.LegacyVersion) ||
            string.IsNullOrWhiteSpace(evidence.TargetVersion) ||
            evidence.ObservedAtUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(evidence.Feature))
            throw new ArgumentException("Shadow evidence is incomplete.", nameof(evidence));
    }

    private static void ValidateAssessment(ShadowComparisonAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (string.IsNullOrWhiteSpace(assessment.ComparisonFingerprint) || assessment.Differences is null ||
            !Enum.IsDefined(assessment.Severity))
            throw new InvalidOperationException("Shadow comparison assessment is incomplete.");
    }
}
