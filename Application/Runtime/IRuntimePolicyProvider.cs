using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Foundation.Application.Runtime;

public interface IRuntimePolicyProvider
{
    Task<RuntimeCalculationPolicy> GetPolicyAsync(
        string stationId, string unitId, long effectiveAtMinute,
        CancellationToken cancellationToken = default);
}
