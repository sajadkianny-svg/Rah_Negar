namespace Rah_Negar.Foundation.Application.Transactions;

public interface ITransactionManager
{
    Task<T> ExecuteAsync<T>(
        Func<ITransactionContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
