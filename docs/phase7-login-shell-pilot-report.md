# Phase 7.1 Login and Shell Pilot Report

## Status

Phase 7.1 adds isolated pilot adapters for Phase 6 login, shell context, feature-gated login selection, and protected navigation feedback. It does not modify any existing form, register the pilot in `Program.cs`, activate Phase 6 authentication, or replace the legacy workflow.

## Login adapter

`Phase6LoginAdapter` composes `LoginViewModel`, `LoginApplicationService`, and an isolated `InMemorySessionManager` around an injected Phase 6 `ILoginService`. It exposes authentication, logout, and session boundaries without referencing `FrmLogin` or legacy `AppSession`. The view model continues to clear its password property after submission.

## Feature-gated selection and rollback

`PilotLoginSelector` uses `IFeatureActivationProvider` and supports:

- **Legacy** — only the injected legacy adapter runs and remains authoritative;
- **NewWorkflow** — only the isolated Phase 6 adapter determines the pilot result;
- **MixedValidation** — legacy login remains authoritative while Phase 6 runs for comparison.

Mixed validation clears any Phase 6 session immediately after successful validation, preventing a non-authoritative validation from creating lasting authenticated state. Returning the feature key to legacy mode is the immediate rollback path. No concrete legacy adapter or production feature configuration is supplied.

## Shell pilot

`IUiWorkflowHost` and `UiWorkflowHost` load the authenticated shell context, expose active/anonymous/expired session state, clear the isolated Phase 6 session during logout, and delegate navigation decisions to the Phase 7.0 interaction boundary.

Standard navigation is allowed for an active session. Protected and support-only denials produce an additional-authorization prompt, while management denials produce a management-authorization prompt. The host returns decisions and presentation state only; it does not open or replace a WinForms screen.

## Tests and isolation

Tests cover Phase 6 login success and failure, session creation, all feature-gate modes, mixed-validation authority and cleanup, shell context loading, logout, expiration, and management-navigation prompting.

`FrmLogin`, all other existing forms, legacy login implementation, `Program.cs`, production composition, and database behavior remain unchanged. The legacy adapter contract and legacy feature mode preserve the rollback path.
