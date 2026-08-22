using Rah_Negar.Core.Runtime.Comparison;

namespace Rah_Negar.Foundation.Application.Runtime.Comparison;

/// <summary>
/// Read-only boundary for obtaining a normalized legacy Runtime snapshot.
/// Phase 4.3 deliberately provides no production implementation.
/// </summary>
public interface ILegacyRuntimeReader
{
    RuntimeSnapshot Read(
        string stationId,
        string unitId,
        long periodStartMinute,
        long periodEndMinute,
        string eventBoundaryVersion);
}
