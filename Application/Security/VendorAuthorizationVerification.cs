using System.Security.Cryptography;

namespace Rah_Negar.Foundation.Application.Security;

public enum VendorPublicKeyState { Active, Retired, NotYetActive }

public sealed class TrustedVendorPublicKey
{
    private readonly byte[] _subjectPublicKeyInfo;

    public TrustedVendorPublicKey(string keyId, ReadOnlySpan<byte> subjectPublicKeyInfo,
        DateTimeOffset activatedAtUtc, DateTimeOffset? retiredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(keyId)) throw new ArgumentException("Key identity is required.", nameof(keyId));
        if (subjectPublicKeyInfo.IsEmpty) throw new ArgumentException("Public verification material is required.", nameof(subjectPublicKeyInfo));
        if (activatedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Key activation time must be UTC.", nameof(activatedAtUtc));
        if (retiredAtUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Key retirement time must be UTC.", nameof(retiredAtUtc));
        if (retiredAtUtc is not null && retiredAtUtc <= activatedAtUtc)
            throw new ArgumentException("Key retirement must follow activation.", nameof(retiredAtUtc));
        KeyId = keyId;
        _subjectPublicKeyInfo = subjectPublicKeyInfo.ToArray();
        ActivatedAtUtc = activatedAtUtc;
        RetiredAtUtc = retiredAtUtc;
    }

    public string KeyId { get; }
    public ReadOnlyMemory<byte> SubjectPublicKeyInfo => _subjectPublicKeyInfo.ToArray();
    public DateTimeOffset ActivatedAtUtc { get; }
    public DateTimeOffset? RetiredAtUtc { get; }

    public VendorPublicKeyState GetState(DateTimeOffset now) => now < ActivatedAtUtc
        ? VendorPublicKeyState.NotYetActive
        : RetiredAtUtc is not null && now >= RetiredAtUtc.Value
            ? VendorPublicKeyState.Retired
            : VendorPublicKeyState.Active;
}

public interface ITrustedVendorPublicKeyProvider
{
    Task<TrustedVendorPublicKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default);
}

public enum VendorAuthorizationVerificationFailure
{
    None,
    MalformedEnvelope,
    MalformedPayload,
    UnsupportedEnvelopeVersion,
    UnsupportedPayloadVersion,
    UnknownKeyId,
    KeyNotActive,
    InvalidSignature,
    WrongDevice,
    WrongRequest,
    WrongAction,
    WrongProposedValue,
    WrongIssuedAt,
    WrongExpiry,
    Expired,
    IssuedInFuture,
    VerificationUnavailable
}

public sealed record VendorAuthorizationVerificationResult(
    bool IsValid,
    VendorAuthorizationVerificationFailure Failure,
    string? RequestId,
    string? KeyId,
    DateTimeOffset VerifiedAtUtc);

public interface IVendorAuthorizationVerifier
{
    Task<VendorAuthorizationVerificationResult> VerifyAsync(
        VendorAuthorizationPayload expected,
        ReadOnlyMemory<char> signedEnvelope,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class EcdsaP256VendorAuthorizationVerifier : IVendorAuthorizationVerifier
{
    private readonly ITrustedVendorPublicKeyProvider _keys;
    private readonly IVendorAuthorizationPayloadSerializer _serializer;

    public EcdsaP256VendorAuthorizationVerifier(
        ITrustedVendorPublicKeyProvider keys,
        IVendorAuthorizationPayloadSerializer serializer)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task<VendorAuthorizationVerificationResult> VerifyAsync(
        VendorAuthorizationPayload expected,
        ReadOnlyMemory<char> signedEnvelope,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        try
        {
            if (!VendorSignedAuthorizationEnvelopeCodec.TryDecode(signedEnvelope, out VendorSignedAuthorizationEnvelope? envelope))
                return Fail(VendorAuthorizationVerificationFailure.MalformedEnvelope, null, null, nowUtc);
            if (!StringComparer.Ordinal.Equals(envelope!.EnvelopeVersion, VendorSignedAuthorizationEnvelopeCodec.CurrentEnvelopeVersion))
                return Fail(VendorAuthorizationVerificationFailure.UnsupportedEnvelopeVersion, null, envelope.KeyId, nowUtc);
            if (!_serializer.TryDeserializeCanonical(envelope.PayloadUtf8, out VendorAuthorizationPayload? signed))
                return Fail(VendorAuthorizationVerificationFailure.MalformedPayload, null, envelope.KeyId, nowUtc);
            VendorAuthorizationPayload actual = signed!;
            if (!StringComparer.Ordinal.Equals(expected.PayloadVersion, VendorAuthorizationPayloadVersions.Version1) ||
                !StringComparer.Ordinal.Equals(actual.PayloadVersion, VendorAuthorizationPayloadVersions.Version1))
                return Fail(VendorAuthorizationVerificationFailure.UnsupportedPayloadVersion, actual.RequestId, envelope.KeyId, nowUtc);

            TrustedVendorPublicKey? key = await _keys.FindByKeyIdAsync(envelope.KeyId, cancellationToken).ConfigureAwait(false);
            if (key is null) return Fail(VendorAuthorizationVerificationFailure.UnknownKeyId, actual.RequestId, envelope.KeyId, nowUtc);
            if (key.GetState(nowUtc) != VendorPublicKeyState.Active)
                return Fail(VendorAuthorizationVerificationFailure.KeyNotActive, actual.RequestId, envelope.KeyId, nowUtc);

            ReadOnlyMemory<byte> publicKey = key.SubjectPublicKeyInfo;
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey.Span, out int bytesRead);
            if (bytesRead != publicKey.Length ||
                !verifier.VerifyData(envelope.PayloadUtf8, envelope.Signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return Fail(VendorAuthorizationVerificationFailure.InvalidSignature, actual.RequestId, envelope.KeyId, nowUtc);

            if (expected.Action != VendorSupportAction.ChangeEsdAdjustment ||
                actual.Action != VendorSupportAction.ChangeEsdAdjustment)
                return Fail(VendorAuthorizationVerificationFailure.WrongAction, actual.RequestId, envelope.KeyId, nowUtc);
            VendorAuthorizationVerificationFailure mismatch = Compare(expected, actual);
            if (mismatch != VendorAuthorizationVerificationFailure.None)
                return Fail(mismatch, actual.RequestId, envelope.KeyId, nowUtc);
            if (actual.IssuedAtUtc > nowUtc)
                return Fail(VendorAuthorizationVerificationFailure.IssuedInFuture, actual.RequestId, envelope.KeyId, nowUtc);
            if (nowUtc >= actual.ExpiresAtUtc)
                return Fail(VendorAuthorizationVerificationFailure.Expired, actual.RequestId, envelope.KeyId, nowUtc);
            return new(true, VendorAuthorizationVerificationFailure.None, actual.RequestId, envelope.KeyId, nowUtc);
        }
        catch (CryptographicException)
        {
            return Fail(VendorAuthorizationVerificationFailure.InvalidSignature, expected.RequestId, null, nowUtc);
        }
        catch
        {
            return Fail(VendorAuthorizationVerificationFailure.VerificationUnavailable, expected.RequestId, null, nowUtc);
        }
    }

    private static VendorAuthorizationVerificationFailure Compare(VendorAuthorizationPayload expected, VendorAuthorizationPayload actual)
    {
        if (!StringComparer.Ordinal.Equals(expected.DeviceId, actual.DeviceId)) return VendorAuthorizationVerificationFailure.WrongDevice;
        if (!StringComparer.Ordinal.Equals(expected.RequestId, actual.RequestId)) return VendorAuthorizationVerificationFailure.WrongRequest;
        if (expected.Action != actual.Action) return VendorAuthorizationVerificationFailure.WrongAction;
        if (expected.ProposedEsdAdjustment != actual.ProposedEsdAdjustment) return VendorAuthorizationVerificationFailure.WrongProposedValue;
        if (expected.IssuedAtUtc != actual.IssuedAtUtc) return VendorAuthorizationVerificationFailure.WrongIssuedAt;
        if (expected.ExpiresAtUtc != actual.ExpiresAtUtc) return VendorAuthorizationVerificationFailure.WrongExpiry;
        return VendorAuthorizationVerificationFailure.None;
    }

    private static VendorAuthorizationVerificationResult Fail(VendorAuthorizationVerificationFailure failure,
        string? requestId, string? keyId, DateTimeOffset now) => new(false, failure, requestId, keyId, now);
}
