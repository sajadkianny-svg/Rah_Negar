namespace Rah_Negar.Foundation.Application.Security;

public enum VendorSupportAction
{
    Unspecified = 0,
    ChangeEsdAdjustment = 1
}

public sealed record VendorSupportAuthorizationRequest(
    string DeviceId,
    string RequestId,
    VendorSupportAction Action,
    decimal ProposedEsdAdjustmentHours,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public enum VendorSupportVerificationFailure
{
    None,
    Invalid,
    Expired,
    Replayed,
    WrongDevice,
    WrongRequest,
    WrongAction,
    WrongProposedValue
}

public sealed record VendorSupportVerificationResult(
    bool IsValid,
    VendorSupportVerificationFailure Failure,
    string RequestId,
    DateTimeOffset VerifiedAt);

/// <summary>
/// Injected verifier implemented with the vendor public key. The customer application never
/// receives a private signing key, master password, universal code, or bypass secret.
/// </summary>
public interface IExternalVendorSupportAuthorizationVerifier
{
    Task<VendorSupportVerificationResult> VerifyAsync(
        VendorSupportAuthorizationRequest expectedRequest,
        ReadOnlyMemory<char> signedAuthorization,
        CancellationToken cancellationToken = default);
}

public interface IConsumedVendorSupportRequestStore
{
    Task<bool> IsConsumedAsync(string requestId, CancellationToken cancellationToken = default);
    Task<bool> TryConsumeAsync(string requestId, CancellationToken cancellationToken = default);
}

public sealed class EsdAdjustmentAuthorizationService
{
    private readonly IExternalVendorSupportAuthorizationVerifier _verifier;
    private readonly IConsumedVendorSupportRequestStore _consumedRequests;

    public EsdAdjustmentAuthorizationService(
        IExternalVendorSupportAuthorizationVerifier verifier,
        IConsumedVendorSupportRequestStore consumedRequests)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _consumedRequests = consumedRequests ?? throw new ArgumentNullException(nameof(consumedRequests));
    }

    public async Task<VendorSupportVerificationResult> AuthorizePostWizardChangeAsync(
        string initiatingShiftProfileId,
        string stationScope,
        decimal proposedHours,
        ManagementAuthorizationProof managementProof,
        VendorSupportAuthorizationRequest supportRequest,
        ReadOnlyMemory<char> signedAuthorization,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatingShiftProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stationScope);

        if (!managementProof.AppliesTo(initiatingShiftProfileId, ProtectedAction.ChangeEsdAdjustment, stationScope, now))
            return Invalid(VendorSupportVerificationFailure.Invalid, supportRequest.RequestId, now);
        if (supportRequest.ExpiresAt <= now || supportRequest.IssuedAt > now)
            return Invalid(VendorSupportVerificationFailure.Expired, supportRequest.RequestId, now);
        if (supportRequest.Action != VendorSupportAction.ChangeEsdAdjustment)
            return Invalid(VendorSupportVerificationFailure.WrongAction, supportRequest.RequestId, now);
        if (supportRequest.ProposedEsdAdjustmentHours != proposedHours)
            return Invalid(VendorSupportVerificationFailure.WrongProposedValue, supportRequest.RequestId, now);
        if (await _consumedRequests.IsConsumedAsync(supportRequest.RequestId, cancellationToken).ConfigureAwait(false))
            return Invalid(VendorSupportVerificationFailure.Replayed, supportRequest.RequestId, now);

        VendorSupportVerificationResult verified = await _verifier.VerifyAsync(
            supportRequest, signedAuthorization, cancellationToken).ConfigureAwait(false);
        if (!verified.IsValid)
            return verified;

        bool consumed = await _consumedRequests.TryConsumeAsync(supportRequest.RequestId, cancellationToken)
            .ConfigureAwait(false);
        return consumed
            ? new(true, VendorSupportVerificationFailure.None, supportRequest.RequestId, now)
            : Invalid(VendorSupportVerificationFailure.Replayed, supportRequest.RequestId, now);
    }

    private static VendorSupportVerificationResult Invalid(
        VendorSupportVerificationFailure failure, string requestId, DateTimeOffset now) =>
        new(false, failure, requestId, now);
}

public sealed class EsdAdjustmentChangeExecutor
{
    private readonly EsdAdjustmentAuthorizationService _authorization;

    public EsdAdjustmentChangeExecutor(EsdAdjustmentAuthorizationService authorization) =>
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));

    public async Task<bool> ExecutePostWizardAsync(
        string shiftProfileId,
        string stationScope,
        decimal proposedHours,
        ManagementAuthorizationProof managementProof,
        VendorSupportAuthorizationRequest supportRequest,
        ReadOnlyMemory<char> signedAuthorization,
        DateTimeOffset now,
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execute);
        VendorSupportVerificationResult result = await _authorization.AuthorizePostWizardChangeAsync(
            shiftProfileId, stationScope, proposedHours, managementProof, supportRequest,
            signedAuthorization, now, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid) return false;
        await execute(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
