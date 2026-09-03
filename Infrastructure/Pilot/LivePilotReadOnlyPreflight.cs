using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Production;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Utils;

namespace Rah_Negar.Infrastructure.Pilot;

public sealed class LivePilotReadOnlyPreflight : ILivePilotReadOnlyPreflight
{
    private static readonly string[] RequiredTables =
    [
        "app_settings", "unit_runtime_base", "tbl_data", "tbl_unique", "tbl_events"
    ];

    private readonly IPilotReadOnlySqliteConnectionFactory _connections;

    public LivePilotReadOnlyPreflight(IPilotReadOnlySqliteConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async ValueTask<LivePilotReadOnlyPreflightResult> EvaluateAsync(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (evaluatedAtUtc.Offset != TimeSpan.Zero)
            return Blocked("live-preflight-time-invalid", evaluatedAtUtc);
        if (cancellationToken.IsCancellationRequested)
            return Canceled(evaluatedAtUtc);

        try
        {
            await using SqliteConnection connection =
                await _connections.OpenReadOnlyAsync(cancellationToken).ConfigureAwait(false);
            if (connection.State != System.Data.ConnectionState.Open ||
                connection.ConnectionString.IndexOf("Mode=ReadOnly",
                    StringComparison.OrdinalIgnoreCase) < 0)
                return Blocked("live-preflight-read-only-boundary-invalid", evaluatedAtUtc);

            if (!await HasRequiredTablesAsync(connection, cancellationToken).ConfigureAwait(false))
                return Blocked("live-preflight-schema-unavailable", evaluatedAtUtc);

            LivePilotReadScope? scope = await ReadScopeAsync(
                connection, cancellationToken).ConfigureAwait(false);
            return scope is null
                ? Blocked("live-preflight-station-scope-invalid", evaluatedAtUtc)
                : new(LivePilotReadOnlyPreflightStatus.Ready,
                    "live-preflight-ready", evaluatedAtUtc, scope);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Canceled(evaluatedAtUtc);
        }
        catch
        {
            return Blocked("live-preflight-read-failed", evaluatedAtUtc);
        }
    }

    public bool OpensReadOnly => true;
    public bool CreatesSchema => false;
    public bool RunsMigration => false;
    public bool OpensTransaction => false;
    public bool MutatesPragma => false;

    private static async Task<bool> HasRequiredTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));
        return RequiredTables.All(names.Contains);
    }

    private static async Task<LivePilotReadScope?> ReadScopeAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand settings = connection.CreateCommand();
        settings.CommandText = """
            SELECT is_initialized, station_type, station_name, data_start_date,
                   esd_extra_runtime_enabled, esd_extra_runtime_hours
            FROM app_settings ORDER BY id LIMIT 1;
            """;
        await using SqliteDataReader reader =
            await settings.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.GetInt64(0) != 1 ||
            !Enum.TryParse(reader.GetString(1), out StationType stationType) ||
            stationType is not (StationType.Rasht or StationType.Ramsar))
            return null;

        string stationName = reader.GetString(2);
        long dataStartDate = reader.GetInt64(3);
        bool esdEnabled = reader.GetInt64(4) == 1;
        decimal esdHours = Convert.ToDecimal(reader.GetDouble(5), CultureInfo.InvariantCulture);
        if (dataStartDate <= 0 || esdHours < 0)
            return null;
        reader.Close();

        long latestDate = await LatestDateAsync(connection, cancellationToken)
            .ConfigureAwait(false) ?? dataStartDate;
        if (latestDate < dataStartDate)
            latestDate = dataStartDate;
        int year = checked((int)(latestDate / 10_000));
        int month = checked((int)(latestDate / 100 % 100));
        _ = PersianDateHelper.GetDaysInMonth(year, month);
        long firstOfMonth = year * 10_000L + month * 100L + 1;
        long dateFrom = Math.Max(dataStartDate, firstOfMonth);
        long periodStart = ToAbsoluteMinute(dateFrom);
        long periodEnd = checked(ToAbsoluteMinute(latestDate) + 1_440);

        (string stationId, ControlledProductionPilotScope scope) = stationType switch
        {
            StationType.Rasht => ("station-rasht",
                ControlledProductionPilotScope.RashtReadOnlyObservation),
            StationType.Ramsar => ("station-ramsar",
                ControlledProductionPilotScope.RamsarReadOnlyObservation),
            _ => throw new InvalidOperationException()
        };

        return new LivePilotReadScope(stationId, stationName, scope, dataStartDate,
            dateFrom, latestDate, periodStart, periodEnd,
            $"{year:0000}-{month:00}", esdEnabled, esdHours);
    }

    private static async Task<long?> LatestDateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(date_rep) FROM (
                SELECT date_rep FROM tbl_data
                UNION ALL SELECT date_rep FROM tbl_unique
                UNION ALL SELECT date_rep FROM tbl_events
            );
            """;
        object? value = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    internal static long ToAbsoluteMinute(long persianDate)
    {
        int year = checked((int)(persianDate / 10_000));
        int month = checked((int)(persianDate / 100 % 100));
        int day = checked((int)(persianDate % 100));
        var calendar = new PersianCalendar();
        DateTime gregorian = calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        return checked(DateOnly.FromDateTime(gregorian).DayNumber * 1_440L);
    }

    private static LivePilotReadOnlyPreflightResult Blocked(
        string reasonCode,
        DateTimeOffset atUtc) => new(
            LivePilotReadOnlyPreflightStatus.Blocked, reasonCode, atUtc);

    private static LivePilotReadOnlyPreflightResult Canceled(DateTimeOffset atUtc) => new(
        LivePilotReadOnlyPreflightStatus.Canceled, "live-preflight-canceled", atUtc);
}
