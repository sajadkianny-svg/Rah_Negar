# Phase 7.3 Runtime and Event UI Pilot Report

## Status

Phase 7.3 adds isolated, UI-neutral Runtime and Event projection adapters. It does not modify or replace existing Runtime/Event screens, change legacy calculations, register pilot services, or activate a production feature.

## Presentation boundaries

`IRuntimeViewPresenter`, `RuntimeViewState`, and `RuntimeUiWorkflowCoordinator` define Runtime loading, legacy, projection, shadow, validation-failure, and unauthorized states. `IEventViewPresenter`, `EventViewState`, and `EventUiWorkflowCoordinator` provide the equivalent Event boundary.

Read-only projection contexts expose display values and explicit evidence metadata. Runtime evidence can preserve Event-chain, Baseline, policy, calculation version, and calculation timestamp. Event evidence can preserve chain, policy, and source revisions. The UI models provide no mutation methods or repository access.

## Feature modes and authority

Each workflow has an independent feature key supporting:

- **Legacy Mode** — calls only the injected legacy adapter;
- **New Projection Mode** — calls only the new read-only projection source;
- **Shadow Comparison Mode** — loads both paths, compares caller-supplied deterministic fingerprints, and keeps the legacy result authoritative.

Unconfigured features default to Legacy Mode. Switching a feature back to legacy is the immediate rollback path.

## Validation and authorization

Invalid projection results map to stable validation feedback without partial projection display. Shadow mode marks an unavailable projection without affecting the authoritative legacy context.

Access requires an active authenticated shell session and the explicit `runtime.view` or `events.view` capability. Unauthorized requests do not call legacy or projection sources.

## Tests and isolation

Tests cover Runtime and Event projection loading, evidence propagation, shadow comparison, validation failure, unauthorized Runtime/Event access, source suppression after denial, and default legacy fallback.

No existing Runtime/Event form, legacy calculation service, database, `Program.cs`, or production feature configuration was changed. Legacy Runtime and Event calculations remain authoritative.
