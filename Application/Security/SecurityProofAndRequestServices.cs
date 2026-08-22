using System.Security.Cryptography;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Security;

public interface IDeviceIdentityProvider
{
    Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default);
}

public sealed record VendorAuthorizationRequestContext(
    string InitiatingShiftProfileId,
    string CorrelationId,
    VendorAuthorizationPayload Payload);

public sealed class VendorAuthorizationRequestFactory
{
    private readonly IDeviceIdentityProvider _devices;
    private readonly IClock _clock;
    private readonly TimeSpan _lifetime;

    public VendorAuthorizationRequestFactory(IDeviceIdentityProvider devices, IClock clock, TimeSpan lifetime)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
        _lifetime = lifetime;
    }

    public async Task<VendorAuthorizationRequestContext> CreateEsdAdjustmentRequestAsync(
        string initiatingShiftProfileId,
        string correlationId,
        decimal proposedEsdAdjustment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatingShiftProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        string deviceId = await _devices.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(deviceId)) throw new InvalidOperationException("Device identity is unavailable.");
        DateTimeOffset issued = _clock.UtcNow.ToUniversalTime();
        string requestId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var payload = new VendorAuthorizationPayload(deviceId, requestId,
            VendorSupportAction.ChangeEsdAdjustment, proposedEsdAdjustment, issued, issued.Add(_lifetime));
        return new(initiatingShiftProfileId, correlationId, payload);
    }
}

public sealed record ValidatedManagementCredentialEvidence(int ManagementCredentialVersion);

public sealed class ManagementAuthorizationProofIssuer
{
    private readonly IClock _clock;
    private readonly TimeSpan _lifetime;

    public ManagementAuthorizationProofIssuer(IClock clock, TimeSpan lifetime)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
        _lifetime = lifetime;
    }

    public ManagementAuthorizationProof Issue(string initiatingShiftProfileId, ProtectedAction action,
        string actionScope, string correlationId, ValidatedManagementCredentialEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatingShiftProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (evidence.ManagementCredentialVersion <= 0) throw new ArgumentOutOfRangeException(nameof(evidence));
        DateTimeOffset issued = _clock.UtcNow.ToUniversalTime();
        return new(initiatingShiftProfileId, action, actionScope, evidence.ManagementCredentialVersion,
            issued, issued.Add(_lifetime), correlationId);
    }
}

public enum ManagementProofFailure
{
    None,
    WrongActor,
    WrongAction,
    WrongScope,
    WrongCorrelation,
    CredentialVersionMismatch,
    NotYetValid,
    Expired
}

public sealed record ManagementProofValidationResult(bool IsValid, ManagementProofFailure Failure);

public static class ManagementAuthorizationProofValidator
{
    public static ManagementProofValidationResult Validate(ManagementAuthorizationProof proof,
        string expectedShiftProfileId, ProtectedAction expectedAction, string expectedScope,
        string expectedCorrelationId, int currentCredentialVersion, DateTimeOffset nowUtc)
    {
        if (!StringComparer.Ordinal.Equals(proof.InitiatingShiftProfileId, expectedShiftProfileId)) return Fail(ManagementProofFailure.WrongActor);
        if (proof.Action != expectedAction) return Fail(ManagementProofFailure.WrongAction);
        if (!StringComparer.Ordinal.Equals(proof.ActionScope, expectedScope)) return Fail(ManagementProofFailure.WrongScope);
        if (!StringComparer.Ordinal.Equals(proof.CorrelationId, expectedCorrelationId)) return Fail(ManagementProofFailure.WrongCorrelation);
        if (proof.CredentialVersion != currentCredentialVersion) return Fail(ManagementProofFailure.CredentialVersionMismatch);
        if (proof.IssuedAt > nowUtc) return Fail(ManagementProofFailure.NotYetValid);
        if (nowUtc >= proof.ExpiresAt) return Fail(ManagementProofFailure.Expired);
        return new(true, ManagementProofFailure.None);
    }

    private static ManagementProofValidationResult Fail(ManagementProofFailure failure) => new(false, failure);
}
