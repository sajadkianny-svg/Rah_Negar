using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Foundation.Application.Runtime;

public interface IRuntimeProjectionRepository
{
    Task<RuntimeProjection?> GetAsync(
        string stationId, string unitId, long periodStartMinute, long periodEndMinute,
        CancellationToken cancellationToken = default);

    Task SaveAsync(RuntimeProjection projection, CancellationToken cancellationToken = default);
}
