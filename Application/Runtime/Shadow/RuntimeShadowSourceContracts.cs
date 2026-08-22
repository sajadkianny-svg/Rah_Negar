using Rah_Negar.Core.Runtime.Calculation;

namespace Rah_Negar.Foundation.Application.Runtime.Shadow;

public sealed record RuntimeDatabaseCopyIdentity(
    string CopyId,
    string SourceFingerprint,
    DateTimeOffset CapturedAt,
    string SourceLabel,
    bool IsReadOnly,
    bool IsProductionSource);

/// <summary>
/// Supplies already reconstructed calculation contexts from an approved read-only database copy.
/// It is not a database connection and exposes no query or write operation.
/// </summary>
public interface IRuntimeShadowInputSource
{
    RuntimeDatabaseCopyIdentity Identity { get; }

    RuntimeCalculationContext LoadContext(
        string stationId,
        string unitId,
        long periodStartMinute,
        long periodEndMinute);
}
