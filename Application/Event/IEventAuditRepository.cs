using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Foundation.Application.Event;

public interface IEventAuditRepository
{
    Task AddAsync(
        ITransactionContext transactionContext,
        EventAudit audit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventAudit>> GetForEventAsync(
        ITransactionContext transactionContext,
        string eventId,
        CancellationToken cancellationToken = default);
}
