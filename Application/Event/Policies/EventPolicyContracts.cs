using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Foundation.Application.Event.Policies;

public interface IEventOwnershipPolicy
{
    Task<bool> IsUnitOwnedByStationAsync(
        ITransactionContext context, string stationId, string unitId, int eventDate,
        CancellationToken cancellationToken = default);
}

public interface IFinalizedPeriodPolicy
{
    Task<bool> IsLockedAsync(
        ITransactionContext context, string stationId, int eventDate,
        CancellationToken cancellationToken = default);
}

public interface IOperatingDayPolicy
{
    Task<bool> IsEligibleAsync(
        ITransactionContext context, string stationId, int eventDate,
        CancellationToken cancellationToken = default);
}

public interface IEventBaselineStateProvider
{
    Task<EventBaseline?> GetBaselineAsync(
        ITransactionContext context, string stationId, string unitId,
        CancellationToken cancellationToken = default);
}

public sealed record EventBaseline(
    EventOperationalState InitialState,
    long EffectiveFromEventDateTime,
    long Version);

public interface IEventIdGenerator
{
    string NewId();
}

public interface IEventDateTimeConverter
{
    long ToChronologicalMinute(int persianDate, int minuteOfDay);
}
