using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;
using System.Collections.ObjectModel;

namespace Rah_Negar.Foundation.Application.Pilot;

public sealed record PilotGatewayRequest(
    PilotExecutionContext Context,
    PilotFeature Feature,
    ActivationEvidencePackage EvidencePackage,
    FeatureIntegrationApproval? Approval,
    RollbackReadinessResult RollbackReadiness);

public sealed class PilotExecutionPermit
{
    internal PilotExecutionPermit(
        string permitId,
        PilotExecutionContext context,
        PilotFeature feature,
        string approvalId,
        DateTimeOffset issuedAtUtc)
    {
        PermitId = permitId;
        PilotId = context.PilotId;
        StationId = context.StationId;
        SelectedShiftProfileIds = new ReadOnlyCollection<string>(
            context.SelectedShiftProfileIds.ToArray());
        Feature = feature;
        EvidencePackageId = context.EvidencePackageId;
        CorrelationId = context.CorrelationId;
        RollbackReference = context.RollbackReference;
        ApprovalId = approvalId;
        IssuedAtUtc = issuedAtUtc;
        ContextCreatedAtUtc = context.CreatedAtUtc;
        ExpiresAtUtc = context.ExpiresAtUtc;
    }

    public string PermitId { get; }
    public string PilotId { get; }
    public string StationId { get; }
    public IReadOnlyList<string> SelectedShiftProfileIds { get; }
    public PilotFeature Feature { get; }
    public string EvidencePackageId { get; }
    public string CorrelationId { get; }
    public string RollbackReference { get; }
    public string ApprovalId { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset ContextCreatedAtUtc { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public bool LegacyRemainsAuthoritative => true;
    public bool TargetReadOnly => true;
    public bool ProductionMutationAllowed => false;
    public bool EsdCutoverAllowed => false;
}

public sealed record PilotGatewayResult(
    IntegrationControlDecision Decision,
    PilotFeature Feature,
    string PilotId,
    string EvidencePackageId,
    string CorrelationId,
    PilotExecutionPermit? Permit,
    IReadOnlyList<string> Reasons);

public interface IPilotGateway
{
    PilotGatewayResult Evaluate(PilotGatewayRequest request);
}

/// <summary>Issues observation-only pilot permits. It never routes UI, changes flags, or executes a feature.</summary>
public sealed class PilotGateway : IPilotGateway
{
    private readonly IClock _clock;
    private readonly IPilotFeatureRegistry _registry;
    private readonly IFeatureIntegrationActivationCoordinator _activationCoordinator;

    public PilotGateway(
        IClock clock,
        IPilotFeatureRegistry registry,
        IFeatureIntegrationActivationCoordinator activationCoordinator)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _activationCoordinator = activationCoordinator ??
            throw new ArgumentNullException(nameof(activationCoordinator));
    }

    public PilotGatewayResult Evaluate(PilotGatewayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        var reasons = new List<string>();
        PilotExecutionContext? context = request.Context;
        if (context is null)
            return Blocked(request.Feature, string.Empty, string.Empty, string.Empty,
                ["pilot-context-required"]);

        PilotContextValidationResult contextValidation = PilotExecutionContextValidator.Validate(context, now);
        reasons.AddRange(contextValidation.Issues);
        if (!_registry.TryGet(request.Feature, out PilotFeatureDefinition? definition) || definition is null)
            reasons.Add("unknown-pilot-feature");
        else
        {
            if (!context.EnabledPilotFeatures.Contains(request.Feature))
                reasons.Add("pilot-feature-outside-context-scope");
            if (definition.EnabledByDefault) reasons.Add("default-pilot-activation-prohibited");
            if (!definition.RollbackRequired) reasons.Add("pilot-rollback-must-be-required");
        }

        if (request.EvidencePackage is null)
            reasons.Add("activation-evidence-required");
        else
        {
            if (!ActivationEvidencePackageValidator.Validate(request.EvidencePackage).IsComplete)
                reasons.Add("activation-evidence-incomplete");
            if (!StringComparer.Ordinal.Equals(context.EvidencePackageId,
                    request.EvidencePackage.EvidencePackageId))
                reasons.Add("pilot-evidence-binding-mismatch");
            if (!StringComparer.Ordinal.Equals(context.CorrelationId,
                    request.EvidencePackage.CorrelationId))
                reasons.Add("pilot-correlation-binding-mismatch");
        }
        if (request.RollbackReadiness is null ||
            request.RollbackReadiness.Status != RollbackReadinessStatus.Ready ||
            request.RollbackReadiness.Blockers.Count > 0)
            reasons.Add("pilot-rollback-not-ready");

        if (definition is not null)
        {
            var integrationBoundary = new PilotEnvironmentBoundary(
                context.PilotId, true, context.StationId, context.SelectedShiftProfileIds,
                [definition.IntegrationFeature], true, context.EvidencePackageId, context.CorrelationId);
            PilotBoundaryValidationResult pilotBoundary = PilotEnvironmentBoundaryValidator.Validate(
                integrationBoundary);
            if (!pilotBoundary.IsValid) reasons.AddRange(pilotBoundary.Issues);
            if (request.EvidencePackage is not null)
            {
                FeatureIntegrationActivationDecision activation = _activationCoordinator.Evaluate(new(
                    request.EvidencePackage, request.Approval, definition.IntegrationFeature,
                    context.StationId, context.CorrelationId));
                if (activation.Decision == IntegrationControlDecision.RequiresManualReview)
                    reasons.Add("pilot-activation-requires-manual-review");
                else if (activation.Decision != IntegrationControlDecision.Allowed)
                    reasons.AddRange(activation.Reasons);
            }
        }

        reasons = reasons.Distinct(StringComparer.Ordinal).ToList();
        if (reasons.Count > 0)
        {
            IntegrationControlDecision decision = reasons.Count == 1 &&
                reasons[0] == "pilot-activation-requires-manual-review"
                ? IntegrationControlDecision.RequiresManualReview
                : IntegrationControlDecision.Blocked;
            return new(decision, request.Feature, context.PilotId, context.EvidencePackageId,
                context.CorrelationId, null, reasons.AsReadOnly());
        }

        string approvalId = request.Approval!.ActivationApproval.ApprovalId;
        var permit = new PilotExecutionPermit(
            $"pilot-permit:{context.PilotId}:{definition!.FeatureId}:{context.CorrelationId}",
            context, request.Feature, approvalId, now);
        return new(IntegrationControlDecision.Allowed, request.Feature, context.PilotId,
            context.EvidencePackageId, context.CorrelationId, permit, Array.Empty<string>());
    }

    private static PilotGatewayResult Blocked(
        PilotFeature feature,
        string pilotId,
        string evidencePackageId,
        string correlationId,
        IReadOnlyList<string> reasons) => new(IntegrationControlDecision.Blocked, feature,
            pilotId, evidencePackageId, correlationId, null, reasons);
}

internal static class PilotPermitValidator
{
    public static IReadOnlyList<string> Validate(
        PilotExecutionPermit? permit,
        PilotExecutionContext context,
        PilotFeature requiredFeature,
        DateTimeOffset nowUtc)
    {
        var reasons = new List<string>();
        if (permit is null) return ["pilot-permit-required"];
        if (nowUtc.Offset != TimeSpan.Zero) reasons.Add("current-time-must-be-utc");
        if (!StringComparer.Ordinal.Equals(permit.PilotId, context.PilotId) ||
            !StringComparer.Ordinal.Equals(permit.StationId, context.StationId) ||
            !StringComparer.Ordinal.Equals(permit.EvidencePackageId, context.EvidencePackageId) ||
            !StringComparer.Ordinal.Equals(permit.CorrelationId, context.CorrelationId) ||
            !StringComparer.Ordinal.Equals(permit.RollbackReference, context.RollbackReference))
            reasons.Add("pilot-permit-binding-mismatch");
        if (!permit.SelectedShiftProfileIds.SequenceEqual(
                context.SelectedShiftProfileIds, StringComparer.Ordinal) ||
            permit.ContextCreatedAtUtc != context.CreatedAtUtc ||
            permit.ExpiresAtUtc != context.ExpiresAtUtc)
            reasons.Add("pilot-permit-scope-binding-mismatch");
        if (permit.Feature != requiredFeature) reasons.Add("pilot-permit-feature-mismatch");
        if (permit.IssuedAtUtc.Offset != TimeSpan.Zero || permit.IssuedAtUtc > nowUtc)
            reasons.Add("pilot-permit-not-yet-valid");
        if (permit.ExpiresAtUtc is { } expiry && nowUtc >= expiry)
            reasons.Add("pilot-permit-expired");
        if (!permit.LegacyRemainsAuthoritative || !permit.TargetReadOnly ||
            permit.ProductionMutationAllowed || permit.EsdCutoverAllowed)
            reasons.Add("pilot-permit-safety-invariant-failed");
        return reasons.AsReadOnly();
    }
}
