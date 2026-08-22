using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Security;

public sealed class SQLiteLegacyEsdValueReader(ISqliteConnectionFactory connections,
    IEsdAdjustmentReconciliationPolicy policy) : ILegacyEsdValueReader
{
    public async Task<LegacyEsdValueResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand exists = connection.CreateCommand();
        exists.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='app_settings');";
        if (Convert.ToInt32(await exists.ExecuteScalarAsync(cancellationToken)) == 0)
            return new(EsdReconciliationState.LegacyValueMissing, null, null, null, 0);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(esd_extra_runtime_hours AS TEXT) FROM app_settings ORDER BY id;";
        var raw = new List<string?>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) raw.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        if (raw.Count == 0) return new(EsdReconciliationState.LegacyValueMissing, null, null, null, 0);
        if (raw.Count != 1) return new(EsdReconciliationState.LegacyValueInvalid, null, null, null, raw.Count);

        string? value = raw[0];
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (string.IsNullOrWhiteSpace(value) || !decimal.TryParse(value, styles, CultureInfo.InvariantCulture,
                out decimal exact) || !policy.IsAllowed(exact))
            return new(EsdReconciliationState.LegacyValueInvalid, value, null, null, 1);
        return new(EsdReconciliationState.LegacyValueFound, value, exact,
            exact.ToString("G29", CultureInfo.InvariantCulture), 1);
    }
}

public sealed class SQLiteTargetEsdProvisioningStore(ISqliteConnectionFactory connections) : ITargetEsdProvisioningStore
{
    public async Task<TargetEsdValue?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EsdAdjustmentCanonical,Revision FROM SecurityDeploymentSettings WHERE SingletonId=1;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        string canonical = reader.GetString(0);
        return decimal.TryParse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal exact) ? new(exact, canonical, reader.GetInt64(1))
            : throw new InvalidOperationException("Target ESD canonical value is invalid.");
    }

    public async Task<bool> TryProvisionAsync(decimal exactValue, string canonicalValue,
        DateTimeOffset provisionedAtUtc, CancellationToken cancellationToken = default)
    {
        if (!StringComparer.Ordinal.Equals(exactValue.ToString("G29", CultureInfo.InvariantCulture), canonicalValue))
            return false;
        try
        {
            await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SecurityDeploymentSettings
                 (SingletonId,EsdAdjustmentCanonical,Revision,UpdatedAtUtc,UpdatedByShiftProfileId)
                VALUES (1,$value,1,$at,NULL);
                """;
            command.Parameters.AddWithValue("$value", canonicalValue);
            command.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(provisionedAtUtc));
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return false; }
    }
}

public sealed class LegacyEsdReconciliationService
{
    private readonly ILegacyEsdValueReader _legacy;
    private readonly ITargetEsdProvisioningStore _target;
    private readonly IEsdAuthorityStateProvider _authority;

    public LegacyEsdReconciliationService(ILegacyEsdValueReader legacy,
        ITargetEsdProvisioningStore target, IEsdAuthorityStateProvider authority)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    public async Task<EsdReconciliationResult> InspectAsync(string correlationId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        EsdAuthorityState authority = await _authority.GetAsync(cancellationToken);
        LegacyEsdValueResult legacy = await _legacy.ReadAsync(cancellationToken);
        TargetEsdValue? target = await _target.ReadAsync(cancellationToken);
        if (legacy.State != EsdReconciliationState.LegacyValueFound)
            return Result(legacy.State, authority.Mode, correlationId, nowUtc, legacy.CanonicalValue,
                target?.CanonicalValue, legacy.State.ToString());
        if (target is null)
            return Result(EsdReconciliationState.ReadyToProvision, authority.Mode, correlationId, nowUtc,
                legacy.CanonicalValue, null, EsdReconciliationState.TargetNotProvisioned.ToString());
        if (target.ExactValue == legacy.ExactValue)
            return Result(EsdReconciliationState.TargetAlreadyProvisionedSameValue, authority.Mode,
                correlationId, nowUtc, legacy.CanonicalValue, target.CanonicalValue, "SameValue");
        return Result(EsdReconciliationState.TargetAlreadyProvisionedDifferentValue, authority.Mode,
            correlationId, nowUtc, legacy.CanonicalValue, target.CanonicalValue, EsdReconciliationState.Conflict.ToString());
    }

    public async Task<EsdReconciliationResult> ProvisionAsync(string correlationId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        EsdReconciliationResult inspection = await InspectAsync(correlationId, nowUtc, cancellationToken);
        if (inspection.State != EsdReconciliationState.ReadyToProvision) return inspection;
        LegacyEsdValueResult legacy = await _legacy.ReadAsync(cancellationToken);
        if (legacy.ExactValue is null || legacy.CanonicalValue is null)
            return Result(EsdReconciliationState.Failed, inspection.AuthorityMode, correlationId, nowUtc,
                null, null, "LegacyChangedDuringProvisioning");
        bool inserted = await _target.TryProvisionAsync(legacy.ExactValue.Value, legacy.CanonicalValue,
            nowUtc, cancellationToken);
        if (!inserted)
        {
            EsdReconciliationResult raced = await InspectAsync(correlationId, nowUtc, cancellationToken);
            return raced.State == EsdReconciliationState.TargetAlreadyProvisionedDifferentValue
                ? raced with { State = EsdReconciliationState.Conflict, ResultCategory = "ConcurrentConflict" }
                : raced;
        }
        return Result(EsdReconciliationState.Provisioned, inspection.AuthorityMode, correlationId, nowUtc,
            legacy.CanonicalValue, legacy.CanonicalValue, "ProvisionedWithoutCutover");
    }

    private static EsdReconciliationResult Result(EsdReconciliationState state, EsdAuthorityMode authority,
        string correlationId, DateTimeOffset at, string? legacy, string? target, string category) =>
        new(state, authority, correlationId, at, legacy, target, category);
}
