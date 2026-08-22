using System.Reflection;
using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot.Composition;
using Rah_Negar.Foundation.Application.Pilot.Presentation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Pilot;

public sealed class PilotWorkflowValidationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 22, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validation_contracts_are_immutable_explicit_and_defensively_copied()
    {
        var subjects = new List<string> { "subject-b", "subject-a", "subject-a" };
        PilotValidationScope scope = Scope(PilotValidationWorkflow.Reporting, subjects);
        PilotWorkflowValidationContext context = Context(PilotValidationWorkflow.Reporting,
            scope: scope);
        subjects.Add("late-subject");

        Assert.Equal(["subject-a", "subject-b"], scope.SubjectIds);
        Assert.False(scope.AllowsAutomaticDiscovery);
        Assert.False(scope.AllowsProductionFallback);
        Assert.False(context.AutomaticallyDiscoversWorkflow);
        Assert.False(context.FallsBackToProduction);
        Assert.False(context.SwitchesAuthority);
        Assert.All(new[] { typeof(PilotValidationScope),
            typeof(PilotWorkflowValidationContext), typeof(PilotWorkflowObserverDescriptor),
            typeof(PilotWorkflowObservationResult), typeof(PilotWorkflowComparisonResult),
            typeof(PilotValidationEvidence), typeof(PilotWorkflowValidationResult) }, type =>
            Assert.DoesNotContain(type.GetProperties(), property => property.SetMethod is not null));
    }

    [Theory]
    [InlineData(PilotValidationWorkflow.Authentication)]
    [InlineData(PilotValidationWorkflow.Reporting)]
    [InlineData(PilotValidationWorkflow.RuntimeEvent)]
    [InlineData(PilotValidationWorkflow.ProtectedSettings)]
    [InlineData(PilotValidationWorkflow.Export)]
    public async Task Each_workflow_contract_produces_read_only_matching_evidence(
        PilotValidationWorkflow workflow)
    {
        using PilotWorkflowValidationCoordinator coordinator = Coordinator(workflow,
            "fingerprint-1", "fingerprint-1");

        PilotWorkflowValidationResult result = await coordinator.ValidateAsync(Context(workflow));

        Assert.Equal(PilotValidationResultStatus.Completed, result.Status);
        Assert.Equal("validation-completed", result.ReasonCode);
        Assert.Equal(PilotDifferenceClassification.Match, result.Comparison!.Classification);
        Assert.Equal(ShadowDifferenceSeverity.None, result.Comparison.Severity);
        Assert.True(result.Comparison.LegacyRemainsAuthoritative);
        Assert.False(result.Comparison.AutomaticallyCorrectsDifference);
        Assert.NotNull(result.Evidence);
        Assert.Equal("validation-1", result.Evidence!.ValidationId);
        Assert.Equal(workflow, result.Evidence.Workflow);
        Assert.False(result.MutatedState);
        Assert.False(result.ExecutedProductionWorkflow);
        Assert.False(result.SwitchedAuthority);
        Assert.Equal(PilotValidationLifecycleState.Completed, coordinator.Lifecycle);
    }

    [Fact]
    public async Task Difference_is_recorded_as_evidence_without_correction_or_authority_change()
    {
        using PilotWorkflowValidationCoordinator coordinator = Coordinator(
            PilotValidationWorkflow.Reporting, "legacy-view-1", "target-view-2");

        PilotWorkflowValidationResult result = await coordinator.ValidateAsync(
            Context(PilotValidationWorkflow.Reporting));

        Assert.Equal(PilotValidationResultStatus.DifferenceDetected, result.Status);
        Assert.Equal("validation-difference-recorded", result.ReasonCode);
        Assert.Equal(PilotDifferenceClassification.Difference,
            result.Evidence!.ComparisonStatus);
        Assert.Equal(ShadowDifferenceSeverity.Warning, result.Evidence.Severity);
        Assert.False(result.Evidence.GrantsAuthority);
        Assert.False(result.Comparison!.AutomaticallyCorrectsDifference);
        Assert.False(result.Comparison.SwitchesAuthority);
    }

    [Fact]
    public void Observer_safety_profile_rejects_every_prohibited_workflow_capability()
    {
        PilotObservationSafetyProfile safe = PilotObservationSafetyProfile.ReadOnlyObservation;

        Assert.True(safe.IsSafe);
        Assert.True(safe.ReadOnly);
        Assert.False(safe.ExecutesProductionWorkflow);
        Assert.False(safe.HandlesPasswords);
        Assert.False(safe.CreatesSession);
        Assert.False(safe.Recalculates);
        Assert.False(safe.MutatesEvents);
        Assert.False(safe.MutatesSettings);
        Assert.False(safe.PerformsProvisioning);
        Assert.False(safe.ExecutesCredentials);
        Assert.False(safe.MutatesArtifacts);
        Assert.False(safe.ChangesAuthority);
        Assert.False(safe.AccessesDatabase);
        Assert.False(safe.CreatesRbac);
        Assert.False(safe.UsesSupportIdentity);
        Assert.False(safe with { MutatesSettings = true } is { IsSafe: true });
    }

    [Theory]
    [InlineData("../pilot.db")]
    [InlineData("SELECT-password-FROM-users")]
    [InlineData("System.Exception: stack-trace")]
    [InlineData("credential-secret")]
    [InlineData("bad\u0001value")]
    public async Task Hostile_scope_input_fails_closed_without_exposing_content(string subject)
    {
        PilotWorkflowValidationContext context = Context(PilotValidationWorkflow.Authentication,
            scope: Scope(PilotValidationWorkflow.Authentication, [subject]));
        using PilotWorkflowValidationCoordinator coordinator = Coordinator(
            PilotValidationWorkflow.Authentication, "legacy-1", "target-1");

        PilotWorkflowValidationResult result = await coordinator.ValidateAsync(context);

        Assert.Equal(PilotValidationResultStatus.Failed, result.Status);
        Assert.Equal("validation-scope-invalid", result.ReasonCode);
        Assert.DoesNotContain(subject, result.ReasonCode, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Context_requires_approval_exact_capabilities_and_utc_timestamp()
    {
        PilotCapabilityEvidence incomplete = new("pilot-1", "correlation-1",
            [PilotUiCapabilities.PilotView], Now.AddMinutes(-1));
        using PilotWorkflowValidationCoordinator unapproved = Coordinator(
            PilotValidationWorkflow.Export, "artifact-1", "artifact-1");
        using PilotWorkflowValidationCoordinator badCapabilities = Coordinator(
            PilotValidationWorkflow.Export, "artifact-1", "artifact-1");

        PilotWorkflowValidationResult approvalResult = await unapproved.ValidateAsync(
            Context(PilotValidationWorkflow.Export, approved: false));
        PilotWorkflowValidationResult capabilityResult = await badCapabilities.ValidateAsync(
            Context(PilotValidationWorkflow.Export, evidence: incomplete));

        Assert.Equal("validation-approval-required", approvalResult.ReasonCode);
        Assert.Equal("validation-capability-evidence-invalid", capabilityResult.ReasonCode);
        Assert.Equal(PilotValidationLifecycleState.Failed, unapproved.Lifecycle);
        Assert.Equal(PilotValidationLifecycleState.Failed, badCapabilities.Lifecycle);
    }

    [Fact]
    public async Task Observer_comparison_and_evidence_failures_are_isolated_with_fixed_codes()
    {
        PilotWorkflowValidationContext context = Context(PilotValidationWorkflow.Authentication);
        using var observerFailure = new PilotWorkflowValidationCoordinator(
            [new ThrowingAuthenticationObserver(LegacyDescriptor()),
             TargetObserver(PilotValidationWorkflow.Authentication, "same")],
            new DeterministicPilotWorkflowObservationComparer(), new PilotValidationEvidenceFactory());
        using var comparisonFailure = new PilotWorkflowValidationCoordinator(
            Pair(PilotValidationWorkflow.Authentication, "same", "same"),
            new ThrowingComparer(), new PilotValidationEvidenceFactory());
        using var evidenceFailure = new PilotWorkflowValidationCoordinator(
            Pair(PilotValidationWorkflow.Authentication, "same", "same"),
            new DeterministicPilotWorkflowObservationComparer(), new ThrowingEvidenceFactory());

        PilotWorkflowValidationResult observer = await observerFailure.ValidateAsync(context);
        PilotWorkflowValidationResult comparison = await comparisonFailure.ValidateAsync(context);
        PilotWorkflowValidationResult evidence = await evidenceFailure.ValidateAsync(context);

        Assert.Equal("validation-observer-failed", observer.ReasonCode);
        Assert.Equal("validation-comparison-failed", comparison.ReasonCode);
        Assert.Equal("validation-evidence-creation-failed", evidence.ReasonCode);
        Assert.All(new[] { observer, comparison, evidence }, result =>
        {
            Assert.Equal(PilotValidationResultStatus.Failed, result.Status);
            Assert.Null(result.Evidence);
            Assert.DoesNotContain("exception", result.ReasonCode, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Invalid_observation_and_unsafe_descriptor_fail_closed()
    {
        PilotWorkflowValidationContext context = Context(PilotValidationWorkflow.Authentication);
        var invalidResult = Observation(PilotValidationWorkflow.Authentication,
            PilotObservationBoundary.LegacyAuthoritative, "SELECT-secret");
        using var invalid = new PilotWorkflowValidationCoordinator(
            [new AuthenticationPilotValidationObserver("legacy-observer", "Legacy observer",
                 PilotObservationBoundary.LegacyAuthoritative, invalidResult),
             TargetObserver(PilotValidationWorkflow.Authentication, "target-1")],
            new DeterministicPilotWorkflowObservationComparer(), new PilotValidationEvidenceFactory());
        using var unsafeCoordinator = new PilotWorkflowValidationCoordinator(
            [new UnsafeAuthenticationObserver(),
             TargetObserver(PilotValidationWorkflow.Authentication, "target-1")],
            new DeterministicPilotWorkflowObservationComparer(), new PilotValidationEvidenceFactory());

        Assert.Equal("validation-observation-invalid",
            (await invalid.ValidateAsync(context)).ReasonCode);
        Assert.Equal("validation-observer-unsafe",
            (await unsafeCoordinator.ValidateAsync(context)).ReasonCode);
    }

    [Fact]
    public async Task Lifecycle_is_single_attempt_disposable_and_never_retries()
    {
        var legacy = new CountingAuthenticationObserver(LegacyDescriptor(),
            Observation(PilotValidationWorkflow.Authentication,
                PilotObservationBoundary.LegacyAuthoritative, "same"));
        var target = new CountingAuthenticationObserver(TargetDescriptor(),
            Observation(PilotValidationWorkflow.Authentication,
                PilotObservationBoundary.TargetReadOnly, "same"));
        var coordinator = new PilotWorkflowValidationCoordinator([legacy, target],
            new DeterministicPilotWorkflowObservationComparer(), new PilotValidationEvidenceFactory());

        Assert.Equal(PilotValidationLifecycleState.Created, coordinator.Lifecycle);
        Assert.Equal(PilotValidationResultStatus.Completed,
            (await coordinator.ValidateAsync(Context(PilotValidationWorkflow.Authentication))).Status);
        Assert.Equal("validation-already-attempted",
            (await coordinator.ValidateAsync(Context(PilotValidationWorkflow.Authentication))).ReasonCode);
        Assert.Equal(1, legacy.Calls);
        Assert.Equal(1, target.Calls);
        coordinator.Dispose();
        coordinator.Dispose();
        Assert.Equal(PilotValidationLifecycleState.Disposed, coordinator.Lifecycle);
        Assert.Equal("validation-coordinator-disposed",
            (await coordinator.ValidateAsync(Context(PilotValidationWorkflow.Authentication))).ReasonCode);
    }

    [Fact]
    public async Task Cancellation_and_disposal_during_observation_do_not_escape()
    {
        var blocking = new BlockingAuthenticationObserver();
        var coordinator = new PilotWorkflowValidationCoordinator(
            [blocking, TargetObserver(PilotValidationWorkflow.Authentication, "same")],
            new DeterministicPilotWorkflowObservationComparer(), new PilotValidationEvidenceFactory());
        Task<PilotWorkflowValidationResult> validation = coordinator.ValidateAsync(
            Context(PilotValidationWorkflow.Authentication)).AsTask();
        await blocking.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        coordinator.Dispose();
        PilotWorkflowValidationResult result = await validation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PilotValidationResultStatus.Failed, result.Status);
        Assert.Equal("validation-coordinator-disposed", result.ReasonCode);
        Assert.Equal(PilotValidationLifecycleState.Disposed, coordinator.Lifecycle);
    }

    [Fact]
    public async Task Caller_cancellation_fails_safely_without_observation_or_evidence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using PilotWorkflowValidationCoordinator coordinator = Coordinator(
            PilotValidationWorkflow.Export, "artifact-1", "artifact-1");

        PilotWorkflowValidationResult result = await coordinator.ValidateAsync(
            Context(PilotValidationWorkflow.Export), cancellation.Token);

        Assert.Equal(PilotValidationResultStatus.Failed, result.Status);
        Assert.Equal("validation-canceled", result.ReasonCode);
        Assert.Null(result.Evidence);
        Assert.Equal(PilotValidationLifecycleState.Failed, coordinator.Lifecycle);
    }

    [Fact]
    public void Evidence_shape_contains_only_safe_identity_status_and_reference_fields()
    {
        string[] names = typeof(PilotValidationEvidence).GetProperties(
            BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name).ToArray();
        string joined = string.Join('|', names);

        Assert.Contains(nameof(PilotValidationEvidence.ValidationId), names);
        Assert.Contains(nameof(PilotValidationEvidence.CorrelationId), names);
        Assert.DoesNotContain("Password", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hash", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sql", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawDatabase", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Coordinator_has_no_automatic_execution_polling_scheduler_or_retry()
    {
        using PilotWorkflowValidationCoordinator coordinator = Coordinator(
            PilotValidationWorkflow.RuntimeEvent, "projection-1", "projection-1");

        Assert.False(coordinator.AutomaticallyRuns);
        Assert.False(coordinator.UsesScheduler);
        Assert.False(coordinator.UsesPolling);
        Assert.False(coordinator.Retries);
        Assert.False(coordinator.ExecutesProductionWorkflow);
        Assert.False(coordinator.MutatesState);
        Assert.False(coordinator.FallsBackToProduction);
        Assert.False(coordinator.SwitchesAuthority);
    }

    [Fact]
    public void Validation_namespace_has_no_database_migration_UI_host_or_execution_dependency()
    {
        Type[] types = typeof(PilotWorkflowValidationCoordinator).Assembly.GetTypes().Where(type =>
            type.Namespace == typeof(PilotWorkflowValidationCoordinator).Namespace).ToArray();
        string surface = string.Join('|', types.Select(type => type.FullName)
            .Concat(types.SelectMany(type => type.GetInterfaces()).Select(type => type.FullName))
            .Concat(types.SelectMany(type => type.GetFields(BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.NonPublic)).Select(field =>
                field.FieldType.FullName)));
        string methodNames = string.Join('|', types.SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.DeclaredOnly)).Where(method => !method.IsSpecialName)
            .Select(method => method.Name));

        Assert.DoesNotContain("Microsoft.Data.Sqlite", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repository", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Windows.Forms", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rah_Negar.UI", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PilotHost", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecutionCoordinator", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methodNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methodNames, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_startup_navigation_and_forms_do_not_reference_validation()
    {
        string root = RepositoryRoot();
        string programPath = Path.Combine(root, "Program.cs");
        string protectedSource = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));
        string validationSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Pilot", "Validation"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("PilotWorkflowValidationCoordinator", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Pilot.Validation", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", validationSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", validationSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", validationSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PilotWorkflowValidationCoordinator Coordinator(
        PilotValidationWorkflow workflow, string legacyFingerprint, string targetFingerprint) =>
        new(Pair(workflow, legacyFingerprint, targetFingerprint),
            new DeterministicPilotWorkflowObservationComparer(),
            new PilotValidationEvidenceFactory());

    private static IPilotWorkflowObserver[] Pair(PilotValidationWorkflow workflow,
        string legacyFingerprint, string targetFingerprint) =>
        [LegacyObserver(workflow, legacyFingerprint), TargetObserver(workflow, targetFingerprint)];

    private static IPilotWorkflowObserver LegacyObserver(PilotValidationWorkflow workflow,
        string fingerprint) => Observer(workflow, PilotObservationBoundary.LegacyAuthoritative,
            "legacy-observer", fingerprint);

    private static IPilotWorkflowObserver TargetObserver(PilotValidationWorkflow workflow,
        string fingerprint) => Observer(workflow, PilotObservationBoundary.TargetReadOnly,
            "target-observer", fingerprint);

    private static IPilotWorkflowObserver Observer(PilotValidationWorkflow workflow,
        PilotObservationBoundary boundary, string id, string fingerprint)
    {
        PilotWorkflowObservationResult result = Observation(workflow, boundary, fingerprint);
        return workflow switch
        {
            PilotValidationWorkflow.Authentication => new AuthenticationPilotValidationObserver(
                id, "Authentication observer", boundary, result),
            PilotValidationWorkflow.Reporting => new ReportingPilotValidationObserver(
                id, "Reporting observer", boundary, result),
            PilotValidationWorkflow.RuntimeEvent => new RuntimeEventPilotValidationObserver(
                id, "Runtime event observer", boundary, result),
            PilotValidationWorkflow.ProtectedSettings => new ProtectedSettingsPilotValidationObserver(
                id, "Protected settings observer", boundary, result),
            PilotValidationWorkflow.Export => new ExportPilotValidationObserver(
                id, "Export observer", boundary, result),
            _ => throw new ArgumentOutOfRangeException(nameof(workflow))
        };
    }

    private static PilotWorkflowObservationResult Observation(PilotValidationWorkflow workflow,
        PilotObservationBoundary boundary, string fingerprint) => new(workflow, boundary,
            PilotObservationStatus.Available, fingerprint,
            boundary == PilotObservationBoundary.LegacyAuthoritative
                ? "legacy-evidence-1" : "target-evidence-1", Now.AddMinutes(-2),
            [new("source-version", "version-1")]);

    private static PilotWorkflowValidationContext Context(PilotValidationWorkflow workflow,
        bool approved = true, PilotCapabilityEvidence? evidence = null,
        PilotValidationScope? scope = null) => new("validation-1", "pilot-1",
            "correlation-1", "composition-1", workflow, Now,
            evidence ?? new PilotCapabilityEvidence("pilot-1", "correlation-1",
                PilotUiCapabilities.All, Now.AddMinutes(-1)),
            scope ?? Scope(workflow, [Subject(workflow)]), approved);

    private static PilotValidationScope Scope(PilotValidationWorkflow workflow,
        IEnumerable<string> subjects) => new("scope-1", workflow, "legacy-observer",
            "target-observer", subjects, true, true, true);

    private static string Subject(PilotValidationWorkflow workflow) => workflow switch
    {
        PilotValidationWorkflow.Authentication => "shift-profile-1",
        PilotValidationWorkflow.Reporting => "finalized-snapshot-1",
        PilotValidationWorkflow.RuntimeEvent => "runtime-projection-1",
        PilotValidationWorkflow.ProtectedSettings => "protected-setting-1",
        PilotValidationWorkflow.Export => "artifact-metadata-1",
        _ => "subject-1"
    };

    private static PilotWorkflowObserverDescriptor LegacyDescriptor() => new(
        "legacy-observer", "Legacy observer", PilotValidationWorkflow.Authentication,
        PilotObservationBoundary.LegacyAuthoritative, PilotStateAvailability.Available,
        PilotObservationSafetyProfile.ReadOnlyObservation);

    private static PilotWorkflowObserverDescriptor TargetDescriptor() => new(
        "target-observer", "Target observer", PilotValidationWorkflow.Authentication,
        PilotObservationBoundary.TargetReadOnly, PilotStateAvailability.Available,
        PilotObservationSafetyProfile.ReadOnlyObservation);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "Rah_Negar.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class ThrowingAuthenticationObserver(PilotWorkflowObserverDescriptor descriptor) :
        IAuthenticationPilotValidationObserver
    {
        public PilotWorkflowObserverDescriptor Descriptor { get; } = descriptor;
        public ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
            PilotWorkflowValidationContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sensitive exception text");
    }

    private sealed class CountingAuthenticationObserver(
        PilotWorkflowObserverDescriptor descriptor, PilotWorkflowObservationResult result) :
        IAuthenticationPilotValidationObserver
    {
        public PilotWorkflowObserverDescriptor Descriptor { get; } = descriptor;
        public int Calls { get; private set; }
        public ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
            PilotWorkflowValidationContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult<PilotWorkflowObservationResult?>(result);
        }
    }

    private sealed class UnsafeAuthenticationObserver : IAuthenticationPilotValidationObserver
    {
        public PilotWorkflowObserverDescriptor Descriptor { get; } = new("legacy-observer",
            "Unsafe observer", PilotValidationWorkflow.Authentication,
            PilotObservationBoundary.LegacyAuthoritative, PilotStateAvailability.Available,
            PilotObservationSafetyProfile.ReadOnlyObservation with { AccessesDatabase = true });
        public ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
            PilotWorkflowValidationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PilotWorkflowObservationResult?>(null);
    }

    private sealed class BlockingAuthenticationObserver : IAuthenticationPilotValidationObserver
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public PilotWorkflowObserverDescriptor Descriptor => LegacyDescriptor();
        public async ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
            PilotWorkflowValidationContext context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class ThrowingComparer : IPilotWorkflowObservationComparer
    {
        public PilotWorkflowComparisonResult Compare(PilotWorkflowValidationContext context,
            PilotWorkflowObservationResult legacyObservation,
            PilotWorkflowObservationResult targetObservation) =>
            throw new InvalidOperationException("raw comparison exception");
    }

    private sealed class ThrowingEvidenceFactory : IPilotValidationEvidenceFactory
    {
        public PilotValidationEvidence Create(PilotWorkflowValidationContext context,
            PilotWorkflowComparisonResult comparison) =>
            throw new InvalidOperationException("raw evidence exception");
    }
}
