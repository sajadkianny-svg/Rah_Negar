using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Foundation.Application.Startup;

public sealed class StartupCoordinator
{
    private readonly IReadOnlyList<IStartupStep> _steps;

    public StartupCoordinator(IEnumerable<IStartupStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = steps.OrderBy(step => step.Order).ToArray();
    }

    public async Task<Result<StartupSummary>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var completed = new List<StartupStepOutcome>();

        foreach (IStartupStep step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<StartupStepOutcome> result =
                await step.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
                return Result<StartupSummary>.Failure(result.Error!);

            completed.Add(result.Value);
        }

        return Result<StartupSummary>.Success(new StartupSummary(completed));
    }
}

public sealed record StartupSummary(IReadOnlyList<StartupStepOutcome> CompletedSteps);
