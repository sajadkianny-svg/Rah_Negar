using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Foundation.Application.Startup;

public interface IStartupStep
{
    string Name { get; }
    int Order { get; }
    Task<Result<StartupStepOutcome>> ExecuteAsync(
        CancellationToken cancellationToken = default);
}

public sealed record StartupStepOutcome(string StepName, bool IsCompleted);
