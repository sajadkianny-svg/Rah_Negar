namespace Rah_Negar.Foundation.Application.Security;

public enum EsdReconciliationState
{
    LegacyValueFound,
    LegacyValueMissing,
    LegacyValueInvalid,
    TargetNotProvisioned,
    TargetAlreadyProvisionedSameValue,
    TargetAlreadyProvisionedDifferentValue,
    ReadyToProvision,
    Provisioned,
    Conflict,
    Failed
}

public enum EsdAuthorityMode { LegacyAuthoritative, TargetAuthoritative }

public sealed record EsdAuthorityState(EsdAuthorityMode Mode, DateTimeOffset? CutoverAtUtc,
    string? ApprovedCorrelationId)
{
    public static EsdAuthorityState PreCutover { get; } = new(EsdAuthorityMode.LegacyAuthoritative, null, null);
}

public interface IEsdAuthorityStateProvider
{
    Task<EsdAuthorityState> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Inactive default contract: target authority requires a future explicit approved adapter.</summary>
public sealed class InactivePreCutoverEsdAuthorityProvider : IEsdAuthorityStateProvider
{
    public Task<EsdAuthorityState> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(EsdAuthorityState.PreCutover);
}

public sealed record LegacyEsdValueResult(EsdReconciliationState State, string? RawValue,
    decimal? ExactValue, string? CanonicalValue, int RowCount);

public sealed record TargetEsdValue(decimal ExactValue, string CanonicalValue, long Revision);

public sealed record EsdReconciliationResult(EsdReconciliationState State,
    EsdAuthorityMode AuthorityMode, string CorrelationId, DateTimeOffset TimestampUtc,
    string? LegacyCanonicalValue, string? TargetCanonicalValue, string ResultCategory);

public interface ILegacyEsdValueReader
{
    Task<LegacyEsdValueResult> ReadAsync(CancellationToken cancellationToken = default);
}

public interface ITargetEsdProvisioningStore
{
    Task<TargetEsdValue?> ReadAsync(CancellationToken cancellationToken = default);
    Task<bool> TryProvisionAsync(decimal exactValue, string canonicalValue, DateTimeOffset provisionedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IEsdAdjustmentReconciliationPolicy
{
    bool IsAllowed(decimal value);
}

public sealed class BoundedEsdAdjustmentReconciliationPolicy(decimal maximum) : IEsdAdjustmentReconciliationPolicy
{
    public decimal Maximum { get; } = maximum >= 0 ? maximum : throw new ArgumentOutOfRangeException(nameof(maximum));
    public bool IsAllowed(decimal value) => value >= 0 && value <= Maximum;
}
