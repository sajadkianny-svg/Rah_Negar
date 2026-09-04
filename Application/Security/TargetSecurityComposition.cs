using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Security;

public enum TargetSecurityCompositionState
{
    Inactive
}

/// <summary>
/// Explicit description of the target security boundary. There is deliberately no activation
/// method: target routing remains disabled until a later, separately scoped authority phase.
/// </summary>
public sealed record TargetSecurityCompositionDescriptor(
    TargetSecurityCompositionState State,
    bool TargetRoutesEnabled,
    bool LegacyRemainsAuthoritative,
    bool LegacyRecoveryReachable,
    bool UsesShiftProfileAuthentication,
    bool UsesSingletonManagementCredential,
    bool UsesOfflineEcdsaP256VendorAuthorization)
{
    public static TargetSecurityCompositionDescriptor Inactive { get; } = new(
        TargetSecurityCompositionState.Inactive,
        TargetRoutesEnabled: false,
        LegacyRemainsAuthoritative: true,
        LegacyRecoveryReachable: false,
        UsesShiftProfileAuthentication: true,
        UsesSingletonManagementCredential: true,
        UsesOfflineEcdsaP256VendorAuthorization: true);
}

/// <summary>Target security services are composed for qualification only while this boundary is inactive.</summary>
public sealed class InactiveTargetSecurityComposition
{
    public InactiveTargetSecurityComposition(
        TargetShiftProfileAuthenticationService authentication,
        TargetManagementAuthorizationService managementAuthorization,
        TargetManagementRecoveryService managementRecovery,
        ProtectedEsdAdjustmentExecutionService esdExecution,
        ISecurityAuditSink audit)
    {
        Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        ManagementAuthorization = managementAuthorization ?? throw new ArgumentNullException(nameof(managementAuthorization));
        ManagementRecovery = managementRecovery ?? throw new ArgumentNullException(nameof(managementRecovery));
        EsdExecution = esdExecution ?? throw new ArgumentNullException(nameof(esdExecution));
        Audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public TargetSecurityCompositionDescriptor Descriptor => TargetSecurityCompositionDescriptor.Inactive;
    public TargetShiftProfileAuthenticationService Authentication { get; }
    public TargetManagementAuthorizationService ManagementAuthorization { get; }
    public TargetManagementRecoveryService ManagementRecovery { get; }
    public ProtectedEsdAdjustmentExecutionService EsdExecution { get; }
    public ISecurityAuditSink Audit { get; }
}

public sealed record TargetShiftProfileSession(
    string ShiftProfileId,
    string StationId,
    int CredentialVersion,
    DateTimeOffset SignedInAtUtc,
    DateTimeOffset ExpiresAtUtc);

public enum TargetAuthenticationFailure
{
    None,
    InvalidRequest,
    ProfileNotFound,
    CredentialUnavailable,
    InvalidCredential,
    AuthenticationUnavailable,
    AuditUnavailable
}

public sealed record TargetAuthenticationResult(
    bool Succeeded,
    TargetAuthenticationFailure Failure,
    TargetShiftProfileSession? Session);

public sealed class TargetShiftProfileAuthenticationService
{
    private const string AuthenticationScope = "target-authentication";
    private readonly IShiftProfileRepository _profiles;
    private readonly IShiftProfileCredentialRepository _credentials;
    private readonly ISecurityAuditSink _audit;
    private readonly IClock _clock;
    private readonly TimeSpan _sessionLifetime;
    private readonly ITargetPasswordVerifier _passwords;

    public TargetShiftProfileAuthenticationService(
        IShiftProfileRepository profiles,
        IShiftProfileCredentialRepository credentials,
        ISecurityAuditSink audit,
        IClock clock,
        TimeSpan sessionLifetime,
        ITargetPasswordVerifier? passwords = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (sessionLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(sessionLifetime));
        _sessionLifetime = sessionLifetime;
        _passwords = passwords ?? Pbkdf2TargetPasswordVerifier.Instance;
    }

    public async Task<TargetAuthenticationResult> AuthenticateAsync(
        string stationId,
        string personnelNo,
        ReadOnlyMemory<char> password,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stationId) || string.IsNullOrWhiteSpace(personnelNo) ||
            password.IsEmpty || string.IsNullOrWhiteSpace(correlationId))
            return await FailAsync("unknown", TargetAuthenticationFailure.InvalidRequest, correlationId, cancellationToken)
                .ConfigureAwait(false);

        ShiftProfile? profile;
        try
        {
            profile = await _profiles.FindByPersonnelNoAsync(stationId, personnelNo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await FailAsync("unknown", TargetAuthenticationFailure.AuthenticationUnavailable,
                correlationId, cancellationToken).ConfigureAwait(false);
        }

        if (profile is null || !profile.IsActive)
            return await FailAsync("unknown", TargetAuthenticationFailure.ProfileNotFound, correlationId, cancellationToken)
                .ConfigureAwait(false);

        ShiftProfileCredentialRecord? credential;
        try
        {
            credential = await _credentials.LoadCurrentAsync(profile.ShiftProfileId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await FailAsync(profile.ShiftProfileId, TargetAuthenticationFailure.CredentialUnavailable,
                correlationId, cancellationToken).ConfigureAwait(false);
        }

        bool valid = false;
        if (credential is { IsCurrent: true })
        {
            try { valid = _passwords.Verify(password.ToString(), credential); }
            catch { valid = false; }
        }
        if (!valid)
            return await FailAsync(profile.ShiftProfileId, TargetAuthenticationFailure.InvalidCredential,
                correlationId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset signedIn = _clock.UtcNow.ToUniversalTime();
        TargetShiftProfileSession session = new(profile.ShiftProfileId, profile.StationId,
            credential!.CredentialVersion, signedIn, signedIn.Add(_sessionLifetime));
        if (!await TryAuditAsync(profile.ShiftProfileId, true, correlationId, "Authenticated",
                cancellationToken).ConfigureAwait(false))
            return new(false, TargetAuthenticationFailure.AuditUnavailable, null);
        return new(true, TargetAuthenticationFailure.None, session);
    }

    public static bool IsSessionActive(TargetShiftProfileSession? session, DateTimeOffset nowUtc) =>
        session is not null && nowUtc < session.ExpiresAtUtc;

    private async Task<TargetAuthenticationResult> FailAsync(string actor, TargetAuthenticationFailure failure,
        string correlationId, CancellationToken cancellationToken)
    {
        bool audited = await TryAuditAsync(string.IsNullOrWhiteSpace(actor) ? "unknown" : actor, false,
            string.IsNullOrWhiteSpace(correlationId) ? "invalid-request" : correlationId,
            failure.ToString(), cancellationToken).ConfigureAwait(false);
        return new(false, audited ? failure : TargetAuthenticationFailure.AuditUnavailable, null);
    }

    private async Task<bool> TryAuditAsync(string actor, bool succeeded, string correlationId,
        string resultCategory, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyDictionary<string, string> metadata = SecurityAuditMetadataBuilder.Create([
                new("AuthorizationStage", "ShiftProfileAuthentication"),
                new("ResultCategory", resultCategory),
                new("CorrelationId", correlationId)]);
            await _audit.WriteAsync(new(actor, ProtectedAction.SecurityConfiguration, AuthenticationScope,
                SecurityAuthorizationType.OperationalShiftProfile, succeeded, _clock.UtcNow.ToUniversalTime(),
                correlationId, metadata), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}

public interface ITargetPasswordVerifier
{
    bool Verify(string password, ShiftProfileCredentialRecord credential);
    bool Verify(string password, ManagementCredentialRecord credential);
}

public sealed class Pbkdf2TargetPasswordVerifier : ITargetPasswordVerifier
{
    public const string Algorithm = "PBKDF2-SHA256";
    public const string Parameters = "iterations=100000;length=32";
    public static Pbkdf2TargetPasswordVerifier Instance { get; } = new();

    private Pbkdf2TargetPasswordVerifier() { }

    public bool Verify(string password, ShiftProfileCredentialRecord credential) =>
        VerifyCore(password, credential.KdfAlgorithm, credential.KdfParameters,
            credential.Salt, credential.PasswordVerifier);

    public bool Verify(string password, ManagementCredentialRecord credential) =>
        VerifyCore(password, credential.KdfAlgorithm, credential.KdfParameters,
            credential.Salt, credential.PasswordVerifier);

    public static byte[] CreateVerifier(string password, ReadOnlySpan<byte> salt,
        string algorithm = Algorithm, string parameters = Parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (!TryReadParameters(algorithm, parameters, out int iterations, out int length))
            throw new ArgumentException("Unsupported password KDF.", nameof(parameters));
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
    }

    private static bool VerifyCore(string password, string algorithm, string parameters,
        byte[] salt, byte[] expected)
    {
        if (password is null || salt is null || expected is null ||
            !TryReadParameters(algorithm, parameters, out int iterations, out int length) ||
            expected.Length != length || salt.Length == 0)
            return false;
        byte[] actual;
        try { actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length); }
        catch (CryptographicException) { return false; }
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryReadParameters(string algorithm, string parameters, out int iterations, out int length)
    {
        iterations = 0;
        length = 0;
        if (!StringComparer.Ordinal.Equals(algorithm, Algorithm) || string.IsNullOrWhiteSpace(parameters))
            return false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string item in parameters.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = item.Split('=', 2, StringSplitOptions.None);
            if (pair.Length != 2 || !values.TryAdd(pair[0], pair[1])) return false;
        }
        return values.Count == 2 && values.TryGetValue("iterations", out string? iterationText) &&
               values.TryGetValue("length", out string? lengthText) &&
               int.TryParse(iterationText, out iterations) && int.TryParse(lengthText, out length) &&
               iterations >= 10_000 && iterations <= 2_000_000 &&
               length is >= 16 and <= 64;
    }
}

public enum TargetManagementAuthorizationFailure
{
    None,
    InvalidRequest,
    SessionMissingOrExpired,
    StationScopeMismatch,
    ActionNotInInventory,
    CredentialUnavailable,
    InvalidCredential,
    AuditUnavailable
}

public sealed record TargetManagementAuthorizationResult(
    bool Succeeded,
    TargetManagementAuthorizationFailure Failure,
    ManagementAuthorizationProof? Proof);

public static class ProtectedActionInventory
{
    public static IReadOnlyList<ProtectedAction> All { get; } =
        new ReadOnlyCollection<ProtectedAction>(Enum.GetValues<ProtectedAction>());
}

public sealed class TargetManagementAuthorizationService
{
    private const string AuthorizationScope = "target-protected-action";
    private readonly IManagementCredentialRepository _credentials;
    private readonly ITargetPasswordVerifier _passwords;
    private readonly ISecurityAuditSink _audit;
    private readonly IClock _clock;
    private readonly TimeSpan _proofLifetime;

    public TargetManagementAuthorizationService(
        IManagementCredentialRepository credentials,
        ISecurityAuditSink audit,
        IClock clock,
        TimeSpan proofLifetime,
        ITargetPasswordVerifier? passwords = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (proofLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(proofLifetime));
        _proofLifetime = proofLifetime;
        _passwords = passwords ?? Pbkdf2TargetPasswordVerifier.Instance;
    }

    public async Task<TargetManagementAuthorizationResult> AuthorizeAsync(
        TargetShiftProfileSession? session,
        string stationScope,
        ProtectedAction action,
        string actionScope,
        string correlationId,
        ReadOnlyMemory<char> managementCredential,
        CancellationToken cancellationToken = default)
    {
        if (!TargetShiftProfileAuthenticationService.IsSessionActive(session, _clock.UtcNow) ||
            string.IsNullOrWhiteSpace(session!.ShiftProfileId))
            return await DenyAsync("unknown", TargetManagementAuthorizationFailure.SessionMissingOrExpired,
                correlationId, cancellationToken).ConfigureAwait(false);
        if (!StringComparer.Ordinal.Equals(session.StationId, stationScope))
            return await DenyAsync(session.ShiftProfileId, TargetManagementAuthorizationFailure.StationScopeMismatch,
                correlationId, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(action) || !ProtectedActionInventory.All.Contains(action) ||
            string.IsNullOrWhiteSpace(actionScope) || string.IsNullOrWhiteSpace(correlationId) ||
            managementCredential.IsEmpty)
            return await DenyAsync(session.ShiftProfileId, TargetManagementAuthorizationFailure.InvalidRequest,
                correlationId, cancellationToken).ConfigureAwait(false);

        ManagementCredentialRecord? credential;
        try { credential = await _credentials.LoadCurrentAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return await DenyAsync(session.ShiftProfileId, TargetManagementAuthorizationFailure.CredentialUnavailable,
                correlationId, cancellationToken).ConfigureAwait(false);
        }

        bool validCredential = false;
        if (credential is { IsCurrent: true, IsActive: true })
        {
            try { validCredential = _passwords.Verify(managementCredential.ToString(), credential); }
            catch { validCredential = false; }
        }
        if (!validCredential)
            return await DenyAsync(session.ShiftProfileId, TargetManagementAuthorizationFailure.InvalidCredential,
                correlationId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        ManagementAuthorizationProof proof = new(session.ShiftProfileId, action, actionScope,
            credential!.CredentialVersion, now, now.Add(_proofLifetime), correlationId);
        if (!await TryAuditAsync(session.ShiftProfileId, action, actionScope, true, correlationId,
                "Authorized", cancellationToken).ConfigureAwait(false))
            return new(false, TargetManagementAuthorizationFailure.AuditUnavailable, null);
        return new(true, TargetManagementAuthorizationFailure.None, proof);
    }

    private async Task<TargetManagementAuthorizationResult> DenyAsync(string actor,
        TargetManagementAuthorizationFailure failure, string correlationId, CancellationToken cancellationToken)
    {
        bool audited = await TryAuditAsync(string.IsNullOrWhiteSpace(actor) ? "unknown" : actor,
            ProtectedAction.SecurityConfiguration, AuthorizationScope, false,
            string.IsNullOrWhiteSpace(correlationId) ? "invalid-request" : correlationId,
            failure.ToString(), cancellationToken).ConfigureAwait(false);
        return new(false, audited ? failure : TargetManagementAuthorizationFailure.AuditUnavailable, null);
    }

    private async Task<bool> TryAuditAsync(string actor, ProtectedAction action, string scope, bool succeeded,
        string correlationId, string resultCategory, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyDictionary<string, string> metadata = SecurityAuditMetadataBuilder.Create([
                new("AuthorizationStage", "ManagementCredential"),
                new("ResultCategory", resultCategory),
                new("CorrelationId", correlationId)]);
            await _audit.WriteAsync(new(actor, action, scope, SecurityAuthorizationType.ManagementCredential,
                succeeded, _clock.UtcNow.ToUniversalTime(), correlationId, metadata), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}

public sealed record ManagementRecoveryRequest(
    string InitiatingShiftProfileId,
    string StationScope,
    string CorrelationId,
    string Reason,
    string ManagementApproverReference,
    string SecurityReviewerReference,
    DateTimeOffset RequestedAtUtc);

public interface IManagementCredentialRecoveryBoundary
{
    Task<bool> TryRotateAsync(
        ManagementCredentialRecord replacement,
        int expectedCurrentVersion,
        SecurityAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

public enum ManagementRecoveryFailure
{
    None,
    InvalidRequest,
    SessionMissingOrExpired,
    ApprovalReferenceInvalid,
    SecretPolicyRejected,
    CredentialUnavailable,
    RotationRejected,
    RecoveryUnavailable
}

public sealed record ManagementRecoveryResult(bool Succeeded, ManagementRecoveryFailure Failure,
    int? NewCredentialVersion);

public sealed class TargetManagementRecoveryService
{
    private readonly IManagementCredentialRepository _credentials;
    private readonly IManagementCredentialRecoveryBoundary _boundary;
    private readonly IClock _clock;

    public TargetManagementRecoveryService(IManagementCredentialRepository credentials,
        IManagementCredentialRecoveryBoundary boundary, IClock clock)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ManagementRecoveryResult> RotateAsync(
        TargetShiftProfileSession? session,
        ManagementRecoveryRequest request,
        ReadOnlyMemory<char> oneTimeSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TargetShiftProfileAuthenticationService.IsSessionActive(session, _clock.UtcNow))
            return new(false, ManagementRecoveryFailure.SessionMissingOrExpired, null);
        if (!StringComparer.Ordinal.Equals(session!.StationId, request.StationScope) ||
            !StringComparer.Ordinal.Equals(session.ShiftProfileId, request.InitiatingShiftProfileId) ||
            string.IsNullOrWhiteSpace(request.CorrelationId) || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 200 || request.Reason.Any(char.IsControl))
            return new(false, ManagementRecoveryFailure.InvalidRequest, null);
        if (!IsSafeReference(request.ManagementApproverReference) ||
            !IsSafeReference(request.SecurityReviewerReference))
            return new(false, ManagementRecoveryFailure.ApprovalReferenceInvalid, null);
        if (!IsAcceptableSecret(oneTimeSecret))
            return new(false, ManagementRecoveryFailure.SecretPolicyRejected, null);

        ManagementCredentialRecord? current;
        try { current = await _credentials.LoadCurrentAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, ManagementRecoveryFailure.CredentialUnavailable, null); }
        if (current is not { IsCurrent: true })
            return new(false, ManagementRecoveryFailure.CredentialUnavailable, null);

        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] verifier = Pbkdf2TargetPasswordVerifier.CreateVerifier(oneTimeSecret.ToString(), salt);
        ManagementCredentialRecord replacement = new(checked(current.CredentialVersion + 1),
            Pbkdf2TargetPasswordVerifier.Algorithm, Pbkdf2TargetPasswordVerifier.Parameters,
            salt, verifier, true, true, now, now, null);
        IReadOnlyDictionary<string, string> metadata = SecurityAuditMetadataBuilder.Create([
            new("AuthorizationStage", "ManagementRecovery"),
            new("ResultCategory", "CredentialRotated"),
            new("CorrelationId", request.CorrelationId)]);
        SecurityAuditEvent audit = new(request.InitiatingShiftProfileId, ProtectedAction.EmergencyRecovery,
            request.StationScope, SecurityAuthorizationType.ManagementCredential, true, now,
            request.CorrelationId, metadata);
        try
        {
            bool rotated = await _boundary.TryRotateAsync(replacement, current.CredentialVersion,
                audit, cancellationToken).ConfigureAwait(false);
            return rotated
                ? new(true, ManagementRecoveryFailure.None, replacement.CredentialVersion)
                : new(false, ManagementRecoveryFailure.RotationRejected, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, ManagementRecoveryFailure.RecoveryUnavailable, null); }
        finally
        {
            CryptographicOperations.ZeroMemory(verifier);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    private static bool IsAcceptableSecret(ReadOnlyMemory<char> secret)
    {
        if (secret.Length < 12 || secret.Length > 256) return false;
        ReadOnlySpan<char> value = secret.Span;
        bool hasLetter = false;
        bool hasDigit = false;
        foreach (char character in value)
        {
            if (character is ' ' or '\t' || char.IsControl(character)) return false;
            hasLetter |= char.IsLetter(character);
            hasDigit |= char.IsDigit(character);
        }
        return hasLetter && hasDigit;
    }

    private static bool IsSafeReference(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
