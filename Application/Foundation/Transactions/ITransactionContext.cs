using System.Data.Common;

namespace Rah_Negar.Foundation.Application.Transactions;

public interface ITransactionContext
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
}
