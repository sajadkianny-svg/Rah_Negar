using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Activation;

public enum ProductionActivationEligibilityDecision
{
    Blocked,
    EligibleButNotExecuted
}

/// <summary>
/// Describes one explicit eligibility evaluation. This boundary records a
/// decision only; it never changes routing or grants Target authority.
/// </summary>
public sealed record ProductionActivationEligibilityRequest(
    string RequestId,
    string StationScope,
    string InitiatingShiftProfileId,
    bool ExplicitOperatorIntent,
    ProductionActivationState CurrentState,
    ProductionActivationGuardRequest? GuardRequest,
    ProductionMigrationExecutionResult? MigrationExecution,
    RollbackReadinessEvidence? RollbackReadiness,
    ManagementAuthorizationProof? ManagementProof,
    int CurrentManagementCredentialVersion,
    DateTimeOffset RequestedAtUtc);

public sealed class ProductionActivationEligibilityReceipt
{
    public ProductionActivationEligibilityReceipt(
        string receiptId,
        string requestId,
        string correlationId,
        string databaseIdentityFingerprint,
        string evidencePackageId,
        ProductionActivationState authorityState,
        ProductionActivationEligibilityDecision decision,
        IEnumerable<string> reasons,
        bool legacyRemainsAuthoritative,
        bool targetAuthorityAccepted,
        bool activationExecuted,
        bool evidencePersisted,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseIdentityFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidencePackageId);
        ArgumentNullException.ThrowIfNull(reasons);
        if (recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Receipt time must be UTC.", nameof(recordedAtUtc));
        if (!legacyRemainsAuthoritative || targetAuthorityAccepted || activationExecuted)
            throw new ArgumentException("Eligibility receipts cannot accept or execute authority.", nameof(targetAuthorityAccepted));
        if (decision == ProductionActivationEligibilityDecision.EligibleButNotExecuted &&
            authorityState != ProductionActivationState.ApprovedForActivation)
            throw new ArgumentException("Eligible receipts must remain ApprovedForActivation.", nameof(authorityState));
        if (decision == ProductionActivationEligibilityDecision.Blocked &&
            authorityState != ProductionActivationState.ActivationBlocked)
            throw new ArgumentException("Blocked receipts must use ActivationBlocked.", nameof(authorityState));

        ReceiptId = receiptId;
        RequestId = requestId;
        CorrelationId = correlationId;
        DatabaseIdentityFingerprint = databaseIdentityFingerprint;
        EvidencePackageId = evidencePackageId;
        AuthorityState = authorityState;
        Decision = decision;
        Reasons = new ReadOnlyCollection<string>(reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        LegacyRemainsAuthoritative = legacyRemainsAuthoritative;
        TargetAuthorityAccepted = targetAuthorityAccepted;
        ActivationExecuted = activationExecuted;
        EvidencePersisted = evidencePersisted;
        RecordedAtUtc = recordedAtUtc;
    }

    public string ReceiptId { get; }
    public string RequestId { get; }
    public string CorrelationId { get; }
    public string DatabaseIdentityFingerprint { get; }
    public string EvidencePackageId { get; }
    public ProductionActivationState AuthorityState { get; }
    public ProductionActivationEligibilityDecision Decision { get; }
    public IReadOnlyList<string> Reasons { get; }
    public bool LegacyRemainsAuthoritative { get; }
    public bool TargetAuthorityAccepted { get; }
    public bool ActivationExecuted { get; }
    public bool EvidencePersisted { get; }
    public DateTimeOffset RecordedAtUtc { get; }
}

/// <summary>
/// Stores the eligibility receipt and its matching activation audit entry as
/// one evidence record. Implementations must not interpret the record as an
/// authority switch.
/// </summary>
public interface IActivationDecisionEvidenceStore
{
    Task<bool> TryAppendAsync(
        ProductionActivationEligibilityReceipt receipt,
        ActivationAuditEntry auditEntry,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicitly describes the authority meaning of the existing activation
/// state machine. It is a projection of ProductionActivationState, not a
/// competing authority model.
/// </summary>
public static class ProductionActivationAuthoritySafety
{
    public static bool LegacyRemainsAuthoritative(ProductionActivationState state) =>
        state != ProductionActivationState.Activated;

    public static bool TargetAuthorityNotAccepted(ProductionActivationState state) =>
        state != ProductionActivationState.Activated;

    public static bool TransitionNotStarted(ProductionActivationState state) =>
        state is ProductionActivationState.NotPrepared or
            ProductionActivationState.AssessmentReady or
            ProductionActivationState.BackupVerified or
            ProductionActivationState.RehearsalVerified or
            ProductionActivationState.ApprovalPending or
            ProductionActivationState.ApprovedForActivation;

    public static bool TransitionFailedWithoutAuthorityChange(ProductionActivationState state) =>
        state == ProductionActivationState.ActivationBlocked;

    public static bool TransitionEligibleButNotExecuted(ProductionActivationState state) =>
        state == ProductionActivationState.ApprovedForActivation;

    public static bool CompletedOnlyThroughExplicitAcceptance(ProductionActivationState state) =>
        state == ProductionActivationState.Activated;
}

public sealed class ProductionActivationEligibilityBoundary
{
    public const string ActivationActionScopePrefix = "production-activation";
    private static readonly TimeSpan DefaultReceiptLifetime = TimeSpan.FromHours(24);

    private readonly IClock _clock;
    private readonly ProductionActivationGuard _guard;
    private readonly IActivationDecisionEvidenceStore _evidenceStore;
    private readonly TimeSpan _receiptLifetime;

    public ProductionActivationEligibilityBoundary(
        IClock clock,
        IActivationDecisionEvidenceStore evidenceStore,
        TimeSpan? receiptLifetime = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _guard = new ProductionActivationGuard(_clock);
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _receiptLifetime = receiptLifetime ?? DefaultReceiptLifetime;
        if (_receiptLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(receiptLifetime));
    }

    public async Task<ProductionActivationEligibilityReceipt> EvaluateAsync(
        ProductionActivationEligibilityRequest? request,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        var reasons = new List<string>();
        string requestId = Safe(request?.RequestId, "activation-request-unavailable");
        string actor = Safe(request?.InitiatingShiftProfileId, "actor-unavailable");
        string station = Safe(request?.StationScope, "station-unavailable");
        string correlationId = Safe(request?.GuardRequest?.EvidencePackage?.CorrelationId,
            "activation-correlation-unavailable");
        string databaseIdentity = Safe(request?.GuardRequest?.EvidencePackage?.DatabaseIdentityFingerprint,
            "database-identity-unavailable");
        string evidencePackageId = Safe(request?.GuardRequest?.EvidencePackage?.EvidencePackageId,
            "evidence-package-unavailable");

        ValidateRequest(request, now, reasons);
        try
        {
            if (request?.GuardRequest is not null)
            {
                ProductionActivationGuardResult guard = _guard.Evaluate(request.GuardRequest);
                if (guard.Decision != ActivationGuardDecision.Allowed)
                    foreach (string reason in guard.Reasons)
                        reasons.Add($"guard-{reason}");
            }
            ValidateMigration(request, now, reasons);
            ValidateRollback(request, reasons);
            ValidateManagementProof(request, now, reasons);
        }
        catch
        {
            reasons.Add("activation-precondition-evaluation-failed");
        }

        ProductionActivationEligibilityDecision decision = reasons.Count == 0
            ? ProductionActivationEligibilityDecision.EligibleButNotExecuted
            : ProductionActivationEligibilityDecision.Blocked;
        ProductionActivationState resultingState = decision ==
            ProductionActivationEligibilityDecision.EligibleButNotExecuted
            ? ProductionActivationState.ApprovedForActivation
            : ProductionActivationState.ActivationBlocked;
        var receipt = new ProductionActivationEligibilityReceipt(
            CreateReceiptId(requestId, correlationId, now), requestId, correlationId,
            databaseIdentity, evidencePackageId, resultingState, decision, reasons,
            legacyRemainsAuthoritative: true, targetAuthorityAccepted: false,
            activationExecuted: false, evidencePersisted: false, now);
        ActivationAuditEntry audit = new(
            $"{receipt.ReceiptId}:audit",
            decision == ProductionActivationEligibilityDecision.EligibleButNotExecuted
                ? ActivationAuditAction.ActivationRequested
                : ActivationAuditAction.GuardEvaluated,
            request?.CurrentState ?? ProductionActivationState.NotPrepared,
            resultingState, correlationId, databaseIdentity, evidencePackageId, actor, now,
            decision == ProductionActivationEligibilityDecision.EligibleButNotExecuted
                ? ActivationAuditResult.ManualReviewRequired
                : ActivationAuditResult.Blocked);

        bool persisted;
        try
        {
            persisted = await _evidenceStore.TryAppendAsync(receipt, audit, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            persisted = false;
        }

        if (persisted)
            return new(receipt.ReceiptId, receipt.RequestId, receipt.CorrelationId,
                receipt.DatabaseIdentityFingerprint, receipt.EvidencePackageId,
                receipt.AuthorityState, receipt.Decision, receipt.Reasons,
                receipt.LegacyRemainsAuthoritative, receipt.TargetAuthorityAccepted,
                receipt.ActivationExecuted, true, receipt.RecordedAtUtc);

        return new(receipt.ReceiptId, receipt.RequestId, receipt.CorrelationId,
            receipt.DatabaseIdentityFingerprint, receipt.EvidencePackageId,
            ProductionActivationState.ActivationBlocked,
            ProductionActivationEligibilityDecision.Blocked,
            receipt.Reasons.Append("activation-evidence-persistence-failed"),
            legacyRemainsAuthoritative: true, targetAuthorityAccepted: false,
            activationExecuted: false, evidencePersisted: false, receipt.RecordedAtUtc);
    }

    private void ValidateRequest(ProductionActivationEligibilityRequest? request,
        DateTimeOffset now, ICollection<string> reasons)
    {
        if (request is null)
        {
            reasons.Add("activation-request-required");
            return;
        }
        if (!IsSafe(request.RequestId)) reasons.Add("activation-request-id-invalid");
        if (request.StationScope is not ("station-rasht" or "station-ramsar"))
            reasons.Add("station-scope-invalid");
        if (!IsSafe(request.InitiatingShiftProfileId)) reasons.Add("shift-profile-required");
        if (!request.ExplicitOperatorIntent) reasons.Add("explicit-operator-intent-required");
        if (request.CurrentState != ProductionActivationState.ApprovedForActivation)
            reasons.Add("activation-state-not-eligible");
        if (request.RequestedAtUtc.Offset != TimeSpan.Zero || request.RequestedAtUtc > now)
            reasons.Add("activation-request-time-invalid");
        if (request.GuardRequest is null) reasons.Add("activation-guard-request-required");
        else if (request.GuardRequest.RequiredScope != ProductionActivationScope.UnifiedMigrationActivation)
            reasons.Add("activation-scope-invalid");
        if (request.CurrentManagementCredentialVersion <= 0)
            reasons.Add("management-credential-version-invalid");
    }

    private void ValidateMigration(ProductionActivationEligibilityRequest? request,
        DateTimeOffset now, ICollection<string> reasons)
    {
        ProductionMigrationExecutionResult? execution = request?.MigrationExecution;
        ProductionMigrationValidationReceipt? receipt = execution?.Receipt;
        ActivationEvidencePackage? evidence = request?.GuardRequest?.EvidencePackage;
        if (execution?.Status != ProductionMigrationExecutionStatus.Succeeded || receipt is null)
        {
            reasons.Add("migration-success-receipt-required");
            return;
        }
        if (string.IsNullOrWhiteSpace(execution.SafeReceiptId) ||
            !StringComparer.Ordinal.Equals(execution.SafeReceiptId, receipt.ReceiptId) ||
            !StringComparer.Ordinal.Equals(receipt.CorrelationId, evidence?.CorrelationId) ||
            !StringComparer.Ordinal.Equals(receipt.DatabaseIdentityFingerprint,
                evidence?.DatabaseIdentityFingerprint) ||
            !StringComparer.Ordinal.Equals(receipt.BackupIdentityFingerprint,
                evidence?.BackupReceipt.BackupIdentityFingerprint) ||
            !receipt.LegacyRemainsAuthoritative || !receipt.TargetRoutingDisabled ||
            !receipt.PreflightIntegrityPassed || !receipt.PostValidationPassed ||
            receipt.RollbackState != OperationalRollbackState.ValidationPassed ||
            receipt.Preservation is not { Passed: true } || receipt.FinalVersion <= 0 ||
            receipt.CompletedAtUtc.Offset != TimeSpan.Zero || receipt.CompletedAtUtc > now ||
            now - receipt.CompletedAtUtc > _receiptLifetime)
            reasons.Add("migration-receipt-invalid-or-stale");
    }

    private static void ValidateRollback(ProductionActivationEligibilityRequest? request,
        ICollection<string> reasons)
    {
        if (request?.RollbackReadiness is null)
        {
            reasons.Add("rollback-readiness-required");
            return;
        }
        RollbackReadinessResult result = RollbackReadinessEvaluator.Evaluate(request.RollbackReadiness);
        if (result.Status != RollbackReadinessStatus.Ready)
            foreach (string blocker in result.Blockers)
                reasons.Add($"rollback-{blocker}");
    }

    private static void ValidateManagementProof(ProductionActivationEligibilityRequest? request,
        DateTimeOffset now, ICollection<string> reasons)
    {
        if (request?.GuardRequest?.EvidencePackage is null || request.ManagementProof is null)
        {
            reasons.Add("management-proof-required");
            return;
        }
        string expectedScope = ScopeFor(request.StationScope);
        ManagementProofValidationResult result = ManagementAuthorizationProofValidator.Validate(
            request.ManagementProof, request.InitiatingShiftProfileId, ProtectedAction.Migration,
            expectedScope, request.GuardRequest.EvidencePackage.CorrelationId,
            request.CurrentManagementCredentialVersion, now);
        if (!result.IsValid) reasons.Add($"management-proof-{result.Failure}");
    }

    private static string ScopeFor(string stationScope) =>
        $"{ActivationActionScopePrefix}:{stationScope}";

    private static bool IsSafe(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    private static string Safe(string? value, string fallback) => IsSafe(value) ? value! : fallback;

    private static string CreateReceiptId(string requestId, string correlationId, DateTimeOffset now) =>
        $"activation-eligibility:{requestId}:{now.UtcTicks:x}";
}
