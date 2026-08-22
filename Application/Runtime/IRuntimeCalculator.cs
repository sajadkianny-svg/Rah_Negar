using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Foundation.Application.Runtime;

public interface IRuntimeCalculator
{
    RuntimeCalculationResult Calculate(RuntimeCalculationRequest request);
}
