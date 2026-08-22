using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Core.Runtime.Comparison;
using Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;

namespace Rah_Negar.Foundation.Application.Runtime.Shadow;

public sealed class RuntimeShadowRunner
{
    private readonly ILegacyRuntimeAdapter _legacyAdapter;
    private readonly RuntimeCalculator _runtimeCalculator;
    private readonly RuntimeComparisonService _comparisonService;
    private readonly TimeProvider _timeProvider;

    public RuntimeShadowRunner(
        ILegacyRuntimeAdapter legacyAdapter,
        RuntimeCalculator runtimeCalculator,
        RuntimeComparisonService comparisonService,
        TimeProvider? timeProvider = null)
    {
        _legacyAdapter = legacyAdapter ?? throw new ArgumentNullException(nameof(legacyAdapter));
        _runtimeCalculator = runtimeCalculator ?? throw new ArgumentNullException(nameof(runtimeCalculator));
        _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<RuntimeShadowExecutionResult> Execute(RuntimeShadowExecutionRequest request)
    {
        ValidateRequest(request);
        RuntimeDatabaseCopyIdentity copy = request.InputSource.Identity;
        ValidateCopyBoundary(copy);

        string[] orderedUnits = request.UnitIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return Array.AsReadOnly(orderedUnits.Select(unit => ExecuteUnit(request, copy, unit)).ToArray());
    }

    private RuntimeShadowExecutionResult ExecuteUnit(
        RuntimeShadowExecutionRequest request,
        RuntimeDatabaseCopyIdentity copy,
        string unitId)
    {
        RuntimeCalculationContext context;
        try
        {
            context = request.InputSource.LoadContext(
                request.StationId, unitId, request.PeriodStartMinute, request.PeriodEndMinute);
            ValidateLoadedContext(request, unitId, context);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Failure(request, unitId, RuntimeShadowExecutionStatus.InputUnavailable,
                "runtime.shadow.input-unavailable", error.Message);
        }

        RuntimeShadowEvidenceMetadata evidence = CreateEvidence(request, copy, context);

        LegacyRuntimeSnapshot rawLegacy;
        RuntimeSnapshot legacy;
        try
        {
            rawLegacy = _legacyAdapter.Read(
                request.StationId,
                unitId,
                request.PeriodStartMinute,
                request.PeriodEndMinute,
                context.EventChainVersion);
            legacy = LegacyRuntimeSnapshotNormalizer.Normalize(
                rawLegacy,
                request.StationId,
                unitId,
                request.PeriodStartMinute,
                request.PeriodEndMinute,
                context.EventChainVersion);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Failure(request, unitId, RuntimeShadowExecutionStatus.LegacyUnavailable,
                "runtime.shadow.legacy-unavailable", error.Message, evidence);
        }

        Rah_Negar.Core.Runtime.Calculation.RuntimeCalculationResult calculated =
            _runtimeCalculator.Calculate(context);
        if (!calculated.IsSuccess)
        {
            string message = string.Join("; ", calculated.Errors.Select(x => $"{x.Code}: {x.Message}"));
            return Failure(request, unitId, RuntimeShadowExecutionStatus.NewEngineFailure,
                "runtime.shadow.new-engine-failed", message, evidence, legacy);
        }

        RuntimeProjection projection = calculated.Projection!;
        RuntimeSnapshot target = RuntimeSnapshotNormalizer.FromProjection(
            projection, "runtime-projection-engine", context.EventChainVersion);

        try
        {
            RuntimeComparisonResult comparison = _comparisonService.Compare(legacy, target);
            RuntimeShadowExecutionStatus status = comparison.IsMatch
                ? RuntimeShadowExecutionStatus.Match
                : RuntimeShadowExecutionStatus.DifferenceDetected;
            return new RuntimeShadowExecutionResult(
                request.StationId,
                unitId,
                request.PeriodStartMinute,
                request.PeriodEndMinute,
                status,
                legacy,
                projection,
                comparison,
                evidence,
                null,
                null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Failure(request, unitId, RuntimeShadowExecutionStatus.ComparisonFailure,
                "runtime.shadow.comparison-failed", error.Message, evidence, legacy, projection);
        }
    }

    private RuntimeShadowEvidenceMetadata CreateEvidence(
        RuntimeShadowExecutionRequest request,
        RuntimeDatabaseCopyIdentity copy,
        RuntimeCalculationContext context) =>
        new(
            request.ExecutionId,
            copy.CopyId,
            copy.SourceFingerprint,
            copy.CapturedAt,
            context.EventChainVersion,
            context.BaselineVersion,
            context.PolicyVersion,
            context.CalculationVersion,
            _timeProvider.GetUtcNow());

    private static RuntimeShadowExecutionResult Failure(
        RuntimeShadowExecutionRequest request,
        string unitId,
        RuntimeShadowExecutionStatus status,
        string code,
        string message,
        RuntimeShadowEvidenceMetadata? evidence = null,
        RuntimeSnapshot? legacy = null,
        RuntimeProjection? projection = null) =>
        new(
            request.StationId,
            unitId,
            request.PeriodStartMinute,
            request.PeriodEndMinute,
            status,
            legacy,
            projection,
            null,
            evidence,
            code,
            message);

    private static void ValidateRequest(RuntimeShadowExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.InputSource);
        if (string.IsNullOrWhiteSpace(request.StationId))
            throw new ArgumentException("Shadow StationId is required.", nameof(request));
        if (request.UnitIds is null || request.UnitIds.Count == 0 || request.UnitIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty shadow UnitId is required.", nameof(request));
        if (request.PeriodEndMinute <= request.PeriodStartMinute)
            throw new ArgumentException("Shadow period end must be after its start.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ExecutionId))
            throw new ArgumentException("Shadow ExecutionId is required.", nameof(request));
    }

    private static void ValidateCopyBoundary(RuntimeDatabaseCopyIdentity copy)
    {
        ArgumentNullException.ThrowIfNull(copy);
        if (copy.IsProductionSource)
            throw new InvalidOperationException("Runtime shadow execution rejects production database sources.");
        if (!copy.IsReadOnly)
            throw new InvalidOperationException("Runtime shadow execution requires a read-only database copy source.");
        if (string.IsNullOrWhiteSpace(copy.CopyId) || string.IsNullOrWhiteSpace(copy.SourceFingerprint))
            throw new InvalidOperationException("Runtime shadow database copy identity is incomplete.");
    }

    private static void ValidateLoadedContext(
        RuntimeShadowExecutionRequest request,
        string unitId,
        RuntimeCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!StringComparer.Ordinal.Equals(context.EventChain.StationId, request.StationId) ||
            !StringComparer.Ordinal.Equals(context.EventChain.UnitId, unitId))
            throw new InvalidOperationException("Loaded Runtime context identity does not match the shadow request.");
        if (context.PeriodStartMinute != request.PeriodStartMinute ||
            context.PeriodEndMinute != request.PeriodEndMinute)
            throw new InvalidOperationException("Loaded Runtime context period does not match the shadow request.");
    }
}
