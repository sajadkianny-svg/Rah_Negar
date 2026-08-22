using Rah_Negar.Core.Event.Comparison;

namespace Rah_Negar.Foundation.Application.Event.Comparison;

public interface ILegacyEventReader
{
    Task<EventSequenceSnapshot> ReadSnapshotAsync(
        LegacyEventReadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record LegacyEventReadRequest(
    string StationId,
    string UnitId,
    int? FromPersianDate = null,
    int? ToPersianDate = null);
