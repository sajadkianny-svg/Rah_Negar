using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Foundation.Application.Event;

public interface IEventRepository
{
    Task<Core.Event.Event?> GetByIdAsync(
        ITransactionContext transactionContext,
        string eventId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Core.Event.Event>> LoadUnitChainAsync(
        ITransactionContext transactionContext,
        string stationId,
        string unitId,
        long baselineBoundaryEventDateTime,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ITransactionContext transactionContext,
        Core.Event.Event value,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        ITransactionContext transactionContext,
        Core.Event.Event value,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task<bool> TombstoneAsync(
        ITransactionContext transactionContext,
        string eventId,
        long expectedRowVersion,
        DateTimeOffset deletedAtUtc,
        Guid deletedByShiftProfileId,
        CancellationToken cancellationToken = default);
}
