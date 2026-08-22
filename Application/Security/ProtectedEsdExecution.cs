using System.Collections.ObjectModel;
using System.Globalization;

namespace Rah_Negar.Foundation.Application.Security;

public sealed record VendorAuthorizationConsumption(
    string RequestId,
    string CorrelationId,
    DateTimeOffset ConsumedAtUtc,
    string? ExecutionReceiptId,
    string DeviceId = "",
    VendorSupportAction Action = VendorSupportAction.ChangeEsdAdjustment,
    string KeyId = "",
    string InitiatingShiftProfileId = "",
    decimal ProposedEsdAdjustment = 0m);

public interface IConsumedVendorAuthorizationStore
{
    Task<bool> IsConsumedAsync(string requestId, string correlationId, CancellationToken cancellationToken = default);
    Task<bool> TryConsumeAsync(VendorAuthorizationConsumption consumption, CancellationToken cancellationToken = default);
}

public enum AtomicEsdExecutionStatus { Executed, AlreadyConsumed, StoreFailed, MutationFailed }

public sealed record AtomicEsdExecutionResult(AtomicEsdExecutionStatus Status, string? ReceiptId);

/// <summary>Future SQLite adapter must atomically persist consumption and apply the ESD mutation.</summary>
public interface IAtomicEsdAdjustmentExecutionBoundary
{
    Task<AtomicEsdExecutionResult> ExecuteOnceAsync(VendorAuthorizationConsumption consumption,
        decimal proposedEsdAdjustment, Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken = default);
}

public interface IEsdAdjustmentDomainValidator
{
    Task<bool> IsValidAsync(decimal proposedEsdAdjustment, CancellationToken cancellationToken = default);
}

public interface ISecurityAuditSink
{
    Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public static class SecurityAuditMetadataBuilder
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "DeviceId", "RequestId", "ProposedEsdAdjustment", "AuthorizationStage",
        "ResultCategory", "KeyId", "CorrelationId"
    };

    public static IReadOnlyDictionary<string, string> Create(IEnumerable<KeyValuePair<string, string>> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var safe = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in metadata)
        {
            if (!Allowed.Contains(key)) throw new ArgumentException("Audit metadata key is not approved.", nameof(metadata));
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Audit metadata value is required.", nameof(metadata));
            safe.Add(key, value);
        }
        return new ReadOnlyDictionary<string, string>(safe);
    }
}

public enum ProtectedEsdExecutionFailure
{
    None, InactiveShiftProfile, ShiftProfileScopeMismatch, InvalidManagementProof, VendorAuthorizationRejected,
    DomainValidationRejected, AuditFailed, ReplayRejected, ReplayStoreFailed, MutationFailed
}

public sealed record ProtectedEsdExecutionResult(bool Succeeded, ProtectedEsdExecutionFailure Failure,
    string? ReceiptId, string CorrelationId);

public sealed class ProtectedEsdAdjustmentExecutionService
{
    private readonly IVendorAuthorizationVerifier _vendorVerifier;
    private readonly IEsdAdjustmentDomainValidator _domain;
    private readonly ISecurityAuditSink _audit;
    private readonly IAtomicEsdAdjustmentExecutionBoundary _atomicExecution;

    public ProtectedEsdAdjustmentExecutionService(IVendorAuthorizationVerifier vendorVerifier,
        IEsdAdjustmentDomainValidator domain, ISecurityAuditSink audit,
        IAtomicEsdAdjustmentExecutionBoundary atomicExecution)
    {
        _vendorVerifier = vendorVerifier ?? throw new ArgumentNullException(nameof(vendorVerifier));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _atomicExecution = atomicExecution ?? throw new ArgumentNullException(nameof(atomicExecution));
    }

    public async Task<ProtectedEsdExecutionResult> ExecuteAsync(ShiftProfile shiftProfile, string actionScope,
        int currentManagementCredentialVersion, ManagementAuthorizationProof managementProof,
        VendorAuthorizationRequestContext request, ReadOnlyMemory<char> signedEnvelope,
        DateTimeOffset nowUtc, Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shiftProfile);
        ArgumentNullException.ThrowIfNull(managementProof);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mutation);
        if (!shiftProfile.IsActive || !StringComparer.Ordinal.Equals(shiftProfile.ShiftProfileId, request.InitiatingShiftProfileId))
            return Fail(ProtectedEsdExecutionFailure.InactiveShiftProfile, request.CorrelationId);
        if (!StringComparer.Ordinal.Equals(shiftProfile.StationId, actionScope))
            return Fail(ProtectedEsdExecutionFailure.ShiftProfileScopeMismatch, request.CorrelationId);

        ManagementProofValidationResult management = ManagementAuthorizationProofValidator.Validate(managementProof,
            shiftProfile.ShiftProfileId, ProtectedAction.ChangeEsdAdjustment, actionScope,
            request.CorrelationId, currentManagementCredentialVersion, nowUtc);
        if (!management.IsValid) return Fail(ProtectedEsdExecutionFailure.InvalidManagementProof, request.CorrelationId);

        VendorAuthorizationVerificationResult vendor;
        try
        {
            vendor = await _vendorVerifier.VerifyAsync(request.Payload,
                signedEnvelope, nowUtc, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail(ProtectedEsdExecutionFailure.VendorAuthorizationRejected, request.CorrelationId);
        }
        if (!vendor.IsValid) return Fail(ProtectedEsdExecutionFailure.VendorAuthorizationRejected, request.CorrelationId);
        try
        {
            if (!await _domain.IsValidAsync(request.Payload.ProposedEsdAdjustment, cancellationToken).ConfigureAwait(false))
                return Fail(ProtectedEsdExecutionFailure.DomainValidationRejected, request.CorrelationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail(ProtectedEsdExecutionFailure.DomainValidationRejected, request.CorrelationId);
        }

        try
        {
            IReadOnlyDictionary<string, string> metadata = SecurityAuditMetadataBuilder.Create([
                new("DeviceId", request.Payload.DeviceId),
                new("RequestId", request.Payload.RequestId),
                new("ProposedEsdAdjustment", request.Payload.ProposedEsdAdjustment.ToString("G29", CultureInfo.InvariantCulture)),
                new("AuthorizationStage", "PreExecution"), new("ResultCategory", "Authorized"),
                new("KeyId", vendor.KeyId!), new("CorrelationId", request.CorrelationId)]);
            await _audit.WriteAsync(new(shiftProfile.ShiftProfileId, ProtectedAction.ChangeEsdAdjustment,
                actionScope, SecurityAuthorizationType.ExternalVendorSupport, true, nowUtc,
                request.CorrelationId, metadata), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return Fail(ProtectedEsdExecutionFailure.AuditFailed, request.CorrelationId);
        }

        string receiptId = Guid.NewGuid().ToString("N");
        AtomicEsdExecutionResult atomic;
        try
        {
            atomic = await _atomicExecution.ExecuteOnceAsync(new(request.Payload.RequestId,
                request.CorrelationId, nowUtc, receiptId, request.Payload.DeviceId,
                request.Payload.Action, vendor.KeyId!, shiftProfile.ShiftProfileId,
                request.Payload.ProposedEsdAdjustment), request.Payload.ProposedEsdAdjustment,
                mutation, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return Fail(ProtectedEsdExecutionFailure.ReplayStoreFailed, request.CorrelationId);
        }

        return atomic.Status switch
        {
            AtomicEsdExecutionStatus.Executed => new(true, ProtectedEsdExecutionFailure.None, atomic.ReceiptId, request.CorrelationId),
            AtomicEsdExecutionStatus.AlreadyConsumed => Fail(ProtectedEsdExecutionFailure.ReplayRejected, request.CorrelationId),
            AtomicEsdExecutionStatus.StoreFailed => Fail(ProtectedEsdExecutionFailure.ReplayStoreFailed, request.CorrelationId),
            _ => Fail(ProtectedEsdExecutionFailure.MutationFailed, request.CorrelationId)
        };
    }

    private static ProtectedEsdExecutionResult Fail(ProtectedEsdExecutionFailure failure, string correlationId) =>
        new(false, failure, null, correlationId);
}
