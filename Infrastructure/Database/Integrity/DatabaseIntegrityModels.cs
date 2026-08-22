using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database.Integrity;

public sealed record ForeignKeyViolation(
    string Table,
    long RowId,
    string ParentTable,
    int ConstraintIndex);

public sealed record DatabaseIntegrityResult(
    bool IsIntegrityValid,
    IReadOnlyList<string> IntegrityMessages,
    IReadOnlyList<ForeignKeyViolation> ForeignKeyViolations,
    IReadOnlyList<string> SchemaValidationErrors)
{
    public bool IsValid =>
        IsIntegrityValid && ForeignKeyViolations.Count == 0 && SchemaValidationErrors.Count == 0;
}

public interface IDatabaseSchemaValidationHook
{
    Task<IReadOnlyList<string>> ValidateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default);
}
