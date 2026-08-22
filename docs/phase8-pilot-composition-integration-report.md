# Phase 8.7 - Controlled Pilot Composition and Read-Only Workflow Integration Foundation

Status: **Implemented as an inactive, explicitly approved, read-only composition boundary; production remains legacy-authoritative**

Date: 2026-08-22

## 1. Outcome and phase boundary

Phase 8.7 creates the first composition layer between an immutable pilot dashboard-state provider and the Phase 8.4/8.5 presentation consumer boundary. It supplies one immutable state to one explicitly selected pilot surface after validation and then stops.

```text
explicit approved PilotCompositionContext
        + immutable surface/source/capability descriptors
        + IPilotDashboardStateProvider
        + IPilotWinFormsStateConsumer
        -> PilotSurfaceComposer.Create
        -> inactive PilotSurfaceBinding
        -> explicit AttachAsync
        -> one immutable PilotDashboardState
        -> read-only pilot surface
        -> stop
```

The composition layer does not execute a pilot host or workflow. It has no startup registration, service locator, database, migration, ESD, feature router, authority switch, production fallback, timer, polling loop, automatic refresh, production form, or navigation integration.

Implementation is isolated in `Application/Pilot/Composition`:

- `PilotCompositionContracts.cs` contains immutable approval, capability, surface, state-source, result, lifecycle, and provider contracts;
- `ImmutablePilotDashboardStateProvider.cs` provides an explicit in-memory source for an already-created immutable dashboard state;
- `PilotSurfaceComposer.cs` contains the fail-closed composer and explicit binding lifecycle;
- `PilotCompositionIntegrationTests.cs` validates composition and production boundaries.

No Phase 8.7 type is referenced by `Program.cs`, startup, or an existing production form.

## 2. Immutable composition contracts

`PilotCompositionContext` binds one composition ID, pilot ID, correlation ID, surface ID, state-source ID, explicit approval flag, UTC approval window, UTC evaluation time, and `PilotCapabilityEvidence`. All properties are get-only. The context always reports that it does not automatically activate, allow execution, allow authority switching, or fall back to production.

The composer requires explicit approval and verifies that evaluation occurs within the supplied UTC approval window. IDs must pass a restricted 128-character identifier allow-list. The context must match the selected surface, provider source, pilot state, correlation, and capability evidence. There is no ambient clock, environment read, configuration lookup, assembly scan, or default context.

`PilotSurfaceDescriptor` identifies the intended surface and declares its kind, read-only status, automatic-opening status, and command support. A valid descriptor must be read-only, not automatically open, expose no commands, and match the consumer's `PilotUiSurfaceKind`. Its fixed legacy-protection properties state that it cannot replace the shell, login, settings, reporting authority, or Runtime/Event authority.

`PilotStateSourceDescriptor` captures a safe source ID and name, availability, read-only status, workflow-execution status, UTC observation timestamp, and a defensive read-only metadata dictionary. Unsafe names use fixed fallback text. Unsafe metadata keys or values—including paths, SQL-like terms, exception terms, and credential-like terms—are withheld.

`PilotCompositionResult` contains only a fixed status, fixed reason code, and an optional binding. Blocked or failed composition returns no binding. It always reports no production activation and no authority switch. No raw exception text enters a result.

## 3. Read-only state provider boundary

`IPilotDashboardStateProvider` exposes:

- one immutable `PilotStateSourceDescriptor`;
- `GetDashboardStateAsync`, which accepts the explicit composition context and cancellation token and returns an immutable `PilotDashboardState`.

The boundary has no command, mutation, save, refresh, execute, repository, connection, transaction, migration, production-form, or UI-control member. Availability and safe metadata are exposed through the descriptor. The composition contract requires providers to be read-only and to declare that they do not execute workflows.

`ImmutablePilotDashboardStateProvider` is the only concrete Phase 8.7 provider. It retains an explicitly supplied immutable state and descriptor and returns that state once when the binding asks. It performs no refresh, discovery, persistence, mutation, command execution, UI access, database access, or host invocation. Composition cannot discover or construct it automatically. This prevents hidden production access and keeps state acquisition an explicit dependency.

## 4. Composer validation and fail-closed behavior

`PilotSurfaceComposer` has a parameterless constructor and no fields, service provider, configuration, or registration behavior. `Create` receives every dependency explicitly and validates before producing a binding.

Composition is blocked for missing dependencies, missing explicit approval, unsafe identifiers, invalid UTC approval windows, absent or mismatched capability evidence, surface/source ID mismatch, unsafe surface behavior, unavailable source, writable or workflow-executing provider, invalid timestamp, surface-kind mismatch, or bidirectional provider/consumer implementation.

The direction check rejects a state provider that also implements the UI consumer and rejects a consumer that also implements the state-provider interface. This prevents a hidden callback or command channel. Provider descriptor access and consumer surface-kind access occur inside the composer's exception boundary. A throwing dependency produces the fixed `composition-validation-failed` result; the exception is discarded.

The composer permanently advertises no automatic registration, automatic attachment, workflow execution, production fallback, or feature activation. It never substitutes a legacy or production screen when validation fails.

## 5. Explicit binding lifecycle

`PilotSurfaceBinding` supports four explicit lifecycle operations:

1. `Create`: `PilotSurfaceComposer.Create` returns a binding in `Created` state without reading the provider or rendering the consumer.
2. `Attach`: one caller invokes `AttachAsync`; the binding obtains one immutable state, validates pilot/correlation/authority invariants, and passes it to the consumer once.
3. `Detach`: the binding cancels an in-flight update and becomes permanently detached. It does not clear, close, replace, or navigate a UI.
4. `Dispose`: the binding cancels its lifetime, becomes permanently disposed, and releases cancellation resources.

Attachment is single-use. A second attach, attach after detach, or attach after failure is blocked. Attach after disposal returns a fixed disposed result. There is no public refresh method and no reattachment loop. State is requested only by an explicit attach call.

The binding stores only its immutable context, surface descriptor, provider interface, consumer interface, lifecycle lock, and cancellation source. It has no `Task`, timer, thread, scheduler, database, workflow, route, or navigation field.

## 6. One-way UI binding

The one permitted data movement is:

```text
IPilotDashboardStateProvider.GetDashboardStateAsync
        -> PilotDashboardState validation
        -> IPilotWinFormsStateConsumer.RenderAsync
```

The returned state must match the approved pilot ID and correlation ID. An active pilot ID, when present, must also match. The state and nested feature state must continue to prohibit execution, routing, activation, and authority switching. Invalid or null state fails before the consumer is called.

The UI consumer receives no provider, binding, composer, callback, delegate, host, repository, authorization executor, credential service, or command object. The provider receives the immutable context, not the consumer. Tests connect a provider both to a recording consumer and to the real hardened `PilotDashboardControl`; each receives exactly one state and executes no command.

Detach deliberately does not call a clear or legacy-navigation method because `IPilotWinFormsStateConsumer` exposes presentation only. Navigation remains outside Phase 8.7.

## 7. Capability evidence boundary

`PilotCapabilityEvidence` carries only pilot ID, correlation ID, UTC observation time, and a defensive sorted list of safe capability identifiers. The valid composition set is exactly:

- `pilot.view`;
- `comparison.view`;
- `evidence.view`.

The evidence declares that it is read-only, does not implement RBAC, and does not create permissions. The composer requires the evidence to match the approved pilot/correlation and contain exactly the supported set. Unknown or incomplete capability evidence is blocked.

This metadata does not authenticate an operator, assign a role, create a permission table, replace login, or grant domain authority. It is evidence that the proposed read-only surface capabilities were explicitly included in the composition approval.

## 8. Failure and disposal isolation

Provider descriptor failure is caught during creation. Provider state failure is caught during attach and does not call the consumer. Consumer failure is caught after one provider read and triggers no retry or production fallback. Null, mismatched, or authority-capable state fails before UI consumption.

Cancellation and disposal are linked to an attachment lifetime. Disposal during an awaited provider call cancels the update, skips the consumer, and returns a fixed disposed result. Cancellation callbacks and cancellation-source disposal are independently protected so dependency behavior cannot leak an exception through `Detach` or `Dispose`.

Failure results use fixed reason codes only. Raw provider or consumer exception messages, database details, paths, SQL, credentials, and stack traces are never returned or displayed. No failure path invokes a production workflow, activates a feature, opens a legacy form, or switches authority.

## 9. Legacy and production protection

The composition namespace references only the immutable pilot presentation contracts and base framework types. It does not reference `System.Windows.Forms`, `Rah_Negar.UI.Forms`, `Microsoft.Data.Sqlite`, a repository, migration runner, pilot host, execution coordinator, settings writer, ESD implementation, RBAC type, Support identity, or login implementation.

`Program.cs` retains SHA-256 `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76`. Source scans confirm no composer/provider registration in `Program.cs`, `UI/Startup`, or `UI/Forms`. Existing shell, login, settings, reporting, Runtime/Event, menu, shortcut, and navigation behavior remain unchanged.

No production database was selected, opened, written, or migrated. No ESD cutover or production authority switch occurred.

## 10. Tests and verification

`PilotCompositionIntegrationTests` adds 16 focused passing tests covering:

- explicit creation and zero provider/UI activity before attach;
- immutable get-only contracts and defensive capability/metadata copies;
- safe source metadata filtering;
- provider availability and safety validation;
- context, dependency, approval-window, surface, source, capability, and direction validation;
- one-way immutable state transfer;
- binding to the real hardened pilot control;
- Create/Attach/Detach/Dispose lifecycle and single attachment;
- provider descriptor, provider update, and consumer failure isolation;
- invalid state rejection before UI;
- disposal during an in-flight provider update;
- exact read-only capability evidence and absence of RBAC/permissions;
- reflection-based absence of execution, database, migration, production UI, host, RBAC, and Support dependencies;
- source-based absence of polling, scheduler, timer, ESD, startup registration, and navigation integration.

The complete Release solution build succeeds with zero errors. Six existing NU1701 warnings remain for the repository's legacy `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms` dependency chain. Phase 8.7 changes no package or target framework.

The focused Phase 8.7 suite passes 16 of 16 tests. The complete suite passes 474 of 474 tests. `git diff --check` passes, and focused Phase 8.7 files contain no trailing whitespace.

## 11. Limitations and remaining activation requirements

Phase 8.7 does not provide production composition, startup registration, provider discovery, database-backed state, host execution, automatic refresh, monitoring, persistence, navigation, shell integration, authentication, RBAC, Support identity, ESD, migration, feature activation, target routing, or authority switching.

Before any real pilot observation is activated, a separately approved phase must provide:

1. a reviewed, explicitly constructed in-memory or otherwise proven read-only state provider;
2. an approved non-production composition root with no default route;
3. operator capability evidence acquisition without role or permissions-database expansion;
4. controlled navigation that can close the pilot surface and return to legacy without replacing the shell;
5. provider timeout, cancellation, shutdown, handle recreation, and long-duration validation;
6. monitoring and evidence-retention rules that do not expose raw or secret data;
7. deployment, DPI, accessibility, localization, and operator acceptance testing;
8. independent proof that no host execution, production database, migration, settings mutation, ESD, credential, RBAC, Support, activation, routing, or authority-switch dependency enters composition;
9. full build, tests, package review, diff inspection, and production-data safeguards immediately before any future registration.

## 12. Task-scoped audit record

The baseline Release build passed with zero errors and six existing compatibility warnings; 458 of 458 tests passed before Phase 8.7. Architecture tracing confirmed the Phase 8.3 host produces immutable results, Phase 8.4 presents immutable dashboard state, and Phase 8.5/8.6 consume state without execution. No existing composition implementation was present.

No production bug was confirmed. The principal risk addressed by this phase was accidental dependency inversion: allowing a view to receive a host/provider command channel or allowing a provider to know the UI. The separate interfaces, direction validation, single-read binding, immutable context, and absence of refresh close that foundation risk.

Potential items requiring future validation are provider provenance, timeout policy, real controlled-host shutdown behavior, capability evidence issuance, and operator/navigation integration. They are activation prerequisites, not confirmed defects. Phase 8.7 introduces no database/schema risk, startup cost, polling, DataGrid recreation, production UI change, station logic leakage, RBAC, or Support identity.
