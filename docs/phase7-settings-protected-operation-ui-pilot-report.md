# Phase 7.4 Settings and Protected Operations UI Pilot Report

> **Reconciled by Phase 7.5:** “Support authorization” in this pilot now means external vendor authorization only. It is an additional mandatory stage after management authorization for post-Wizard ESD changes and never a local login/profile. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 7.4 is implemented as an isolated, UI-neutral pilot. It adds a protected-settings presentation boundary, protected-operation coordinators, authorization prompt states, secure result mapping, tests, and this report. It does not replace or edit any settings form, call a legacy settings service, modify production composition, activate a feature, or open a database.

The checkout supplied for this phase contains the Phase 6 and earlier Phase 7 reports but not the security or UI-foundation source contracts described by those reports. The pilot is therefore self-contained under `Rah_Negar.Foundation.Application.UI.Settings` and exposes dependency-injection boundaries that a future approved composition can adapt to the missing foundations. No attempt was made to reconstruct or activate production security infrastructure.

## Architecture

The implementation is in three new application files:

- `Application/UI/Settings/ProtectedSettingsContracts.cs` defines operation requests, result enums, session and settings projections, audit evidence, feature-mode contracts, and injected reader, legacy, authorization, audit, and presenter boundaries.
- `Application/UI/Settings/ProtectedSettingsViewState.cs` defines immutable, presentation-safe UI state.
- `Application/UI/Settings/ProtectedSettingsUiCoordinator.cs` implements viewing, feature fallback, authorization prompts, protected execution, audit ordering, and result mapping.

`IProtectedSettingsPresenter` receives complete immutable state and has no WinForms dependency. `ProtectedSettingsViewState` can represent loading, ready, management prompt, support request information, one-time-code prompt visibility, completion, denial, session expiration, invalid authorization, execution failure, and legacy fallback. `ProtectedSettingsSnapshot` carries one Station/deployment ESD value and display-only settings; it has no Unit key.

The coordinator depends only on interfaces. It cannot directly read SQLite, mutate settings, authenticate a credential, generate a support code, write a concrete audit store, or open a form. Execution is supplied as a delegate by a future adapter and is reachable only after a successful decision.

## Protected workflows

The coordinator supports three explicit request types:

1. `EsdAdjustmentChangeRequest` for a shared Station/deployment ESD Adjustment.
2. `SecuritySettingChangeRequest` for a named protected security-setting action.
3. `CredentialManagementRequest` for a sanitized action and target identity.

Requests contain correlation metadata, caller-supplied time, safe operation metadata, and non-secret business values. Credential and support secrets are accepted only as transient `ReadOnlyMemory<char>` arguments on specialized authorization submission methods and are forwarded directly to the injected authorization gateway. They are not copied to pending operation metadata, presenter state, results, feedback, or audit entries.

## State transitions and execution ordering

Viewing in Pilot mode follows:

`session validation -> Loading -> reader -> Ready`

An absent or expired session maps directly to `SessionExpired`; the protected reader is not called. In Legacy mode, the coordinator presents `LegacyFallback` and invokes only the injected legacy workflow.

A protected change follows this enforced sequence:

`session/identity validation -> authorization -> audit decision -> execute only if Authorized -> presentation result`

Authorization and execution are separate dependencies. Every authorization decision reached with an active identity is awaited by the audit writer before execution is considered. Denied, prompt-required, invalid, and failed authorization results return without invoking the protected delegate. An authorized delegate is invoked once. Delegate exceptions are caught and mapped to generic `ExecutionFailed` feedback without exposing exception text.

Prompt-required decisions retain only the sanitized request, validated session identity, and delegate under the correlation ID. Management or support submission resumes that exact pending operation. A missing, expired, replayed, or already-completed correlation fails closed. A changed or expired session removes the pending operation and produces session-expired feedback.

## ESD Adjustment workflow

The pilot models ESD Adjustment as one value for the Station/deployment. There is deliberately no Unit identifier in `EsdAdjustmentChangeRequest` or `ProtectedSettingsSnapshot`.

Zero is accepted. An unchanged zero/default request is authorized by the coordinator without an additional post-Wizard prompt. A request explicitly marked `IsWizardInitialValue` represents the initial value established by the Wizard and likewise does not require post-Wizard authorization. Negative values are denied and audited without execution.

Every post-Wizard non-default change is passed to the injected authorization gateway. It cannot execute before an `Authorized` result has been audited. This pilot changes no Runtime calculator or report data. Consequently, the existing approved policy remains a responsibility of domain and persistence adapters: open/unlocked Runtime recalculation uses the current Station value, while finalized/locked snapshots remain immutable after later changes.

## Authorization prompts and secure mapping

`SecureOperationPresentationResult` maps all required outcomes: `Authorized`, `Denied`, `SessionExpired`, `ManagementAuthorizationRequired`, `SupportAuthorizationRequired`, `InvalidAuthorization`, and `ExecutionFailed`.

Management-required results produce a management prompt. Support-required results can safely expose `DeviceId` and request information, plus a Boolean indicating that the one-time-code prompt is visible. No generator is implemented and no support or master code is embedded.

Support submission can distinguish invalid, expired, and replayed authorization through `AuthorizationFailureKind`. These outcomes map to stable generic feedback. Authorization-provider reasons and submitted values are not reflected back to the presenter. Device ID and request information are presentation metadata, not authorization secrets.

## Feature gate and rollback

The pilot feature key is `settings.protected.pilot`. `SettingsPilotFeatureMode.Legacy` is the rollback mode and causes only the injected legacy workflow to run. Pilot readers, authorization, audit, and protected delegates are not invoked in that path. No production provider or configuration has been added, so nothing is activated by this change.

Legacy settings remain authoritative. `FrmSettings`, its designer, existing settings forms, `AppSettingsService`, and other legacy services are unchanged. `Program.cs`, `Rah_Negar.csproj`, and `Rah_Negar.sln` were not modified by Phase 7.4. The project and solution already had working-tree differences before this phase; they were preserved.

## Tests

`Rah_Negar.Tests/UI/ProtectedSettingsUiCoordinatorTests.cs` adds coverage for:

- normal protected-settings viewing;
- denied changes and proof that denial never executes;
- valid zero/default ESD behavior;
- Wizard initial ESD behavior;
- authorized non-default post-Wizard ESD changes;
- management prompt and resume;
- Device ID/request-information support presentation and code-prompt state;
- invalid, expired, and replayed support authorization feedback;
- expired session suppression of authorization and execution;
- successful operation execution exactly once;
- audit metadata propagation and authorization/audit/execution ordering;
- generic execution-failure feedback;
- reflection-based secret-field exclusion across presentation, request, result, and audit contracts;
- legacy feature-mode fallback and pilot-source suppression.

Complete solution verification on 2026-08-24:

- `dotnet build Rah_Negar.sln --no-restore --nologo`: succeeded, zero errors, six NU1701 warnings.
- `dotnet test Rah_Negar.sln --no-build --no-restore --nologo`: 202 passed, zero failed, zero skipped.
- `git diff --check`: passed; Git emitted only existing LF-to-CRLF working-copy notices.
- NuGet vulnerability scan including transitive packages: no known vulnerable packages from the configured NuGet source.
- NuGet deprecation scan: application project has none; test project reports xUnit 2.9.3 as legacy with xUnit v3 as the suggested alternative.
- Compatibility diagnostics: OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 restore against .NET Framework assets rather than `net8.0-windows7.0`, producing NU1701 for both projects. Packages were not changed because major package changes are outside this pilot.

No test instantiated a production form or concrete legacy settings service. The new fakes are memory-only. No production SQLite connection factory or database service was called, and no production database was opened or modified.

## Limitations

- This is a presentation/application pilot, not production composition. A future approved phase must adapt the real session, security authorization, audit, and legacy workflow boundaries.
- The supplied checkout does not contain the Phase 6/7 source implementations described by the reference reports. Compatibility with those absent concrete types must be validated when they are restored.
- The coordinator does not generate, persist, transmit, or verify a support code; verification belongs behind `IProtectedSettingsAuthorizationGateway`.
- The pilot does not persist ESD Adjustment and does not alter Runtime recalculation or finalized snapshot behavior.
- Pending prompt state is in-memory and process-local. Production lifecycle, cancellation, timeout cleanup, and concurrent prompt policy remain future composition concerns.
- Session expiration uses UTC wall-clock time because the absent Phase 7 clock/session contracts could not be reused. A future adapter should supply the approved application clock semantics.
- Existing package compatibility warnings and xUnit deprecation are documented but intentionally not remediated in this phase.
