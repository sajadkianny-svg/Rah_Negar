namespace Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;

/// <summary>
/// Read-only legacy Runtime capture boundary. Phase 4.4 provides no implementation,
/// database access, SQL, production registration, or legacy calculation invocation.
/// </summary>
public interface ILegacyRuntimeAdapter
{
    LegacyRuntimeSnapshot Read(
        string stationId,
        string unitId,
        long periodStartMinute,
        long periodEndMinute,
        string eventBoundaryVersion);
}
