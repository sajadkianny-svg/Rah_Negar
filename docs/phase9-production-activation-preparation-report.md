# Phase 9.0 - Controlled Production Activation Preparation

Status: **Implemented as an inactive, evidence-only activation-preparation layer; production remains legacy-authoritative**

Date: 2026-08-22

## 1. Outcome and scope

Phase 9.0 creates the final governance boundary before any future request to activate production behavior. It evaluates immutable preparation context, backup and restore-test evidence, rollback evidence, six activation gates, and six explicit stop conditions. It produces an in-memory cutover evidence package and one of three decisions: `ApprovedForPreparation`, `Blocked`, or `RequiresReview`.

```text
explicit ProductionActivationPreparationContext
        + backup and restore-test evidence
        + rollback verification evidence
        + six immutable activation gates
        + six inactive stop conditions
        -> ProductionActivationReadinessCoordinator.Evaluate
        -> ProductionCutoverEvidencePackage
        -> ApprovedForPreparation / Blocked / RequiresReview
        -> stop
```

`ApprovedForPreparation` is deliberately narrower than approval for activation. It confirms only that supplied evidence satisfied the Phase 9.0 preparation rules. It grants no permission, changes no activation state, enables no feature, registers no route, deploys no artifact, runs no migration, reads or modifies no database, performs no ESD cutover, and switches no authority.

Implementation is isolated in `Application/Activation/Preparation`:

- `ProductionActivationPreparationContracts.cs` defines immutable context, gate, backup, rollback, stop-condition, validation-summary, package, and result contracts;
- `ProductionActivationReadinessCoordinator.cs` implements deterministic fail-closed evaluation;
- `ProductionActivationPreparationTests.cs` proves behavior and protected production boundaries.

The namespace consumes only the existing activation-scope enum and the immutable Phase 8.8 validation and Phase 8.9 deployment-readiness statuses. It does not compose the Phase 8.0 activation guard, state-transition policy, migration executor, feature executor, database-readiness services, pilot host, production UI, or startup.

## 2. Preparation context

`ProductionActivationPreparationContext` is explicitly constructed with preparation ID, release identifier, target activation scope, legacy-authority state, pilot-validation status, deployment-readiness status, rollback reference, approval references, UTC timestamp, and explicit-request flag. Approval references are defensively copied, deduplicated, sorted, and exposed through a read-only collection.

The coordinator accepts the context only when the request is explicit; all identifiers are safe; scope and status values are defined; there are exactly three approval references; time is UTC; and authority remains `LegacyAuthoritative`. `TargetAuthorityRequested`, `Unknown`, undefined values, implicit requests, and unsafe identifiers fail before evidence evaluation and produce no cutover package.

The context permanently reports that it does not discover an environment, access production, grant activation permission, or fall back to production. It contains no database path, machine identifier, connection string, user object, service provider, command, delegate, or executor.

Dynamic identifiers use a restricted 128-character allow-list. Paths, control characters, SQL-like content, exception text, credentials, passwords, secrets, private-key terms, connection strings, access tokens, authorization tokens, and permission-escalation terms cannot enter valid evidence. Failed input is represented by fixed reason codes and is never echoed.

## 3. Activation gates

Phase 9.0 requires exactly one immutable gate of each type:

1. security review;
2. operations readiness;
3. data-owner approval;
4. rollback readiness;
5. validation completion;
6. deployment readiness.

Each `ProductionActivationGate` contains type, `Satisfied`/`Missing`/`Failed`/`RequiresReview` status, safe evidence reference, safe reviewer reference, and UTC review timestamp. Missing, duplicate, undefined, unsafe, or future-dated gate evidence blocks preparation. A missing or failed gate blocks. A review gate cannot produce an approved preparation result.

Security, operations, and data-owner gate evidence references must exactly equal the three approval references in the context. These gates are evidence only: they do not establish the reviewer’s organizational authority, grant permission, create a permission, implement RBAC, or change an application identity. Phase 9.0 has no approval database, approval UI, signature verifier, credential flow, or permission engine.

The remaining gates must agree with their source evidence. Validation must be satisfied only when Phase 8.8 completed; a recorded difference requires review. Deployment must be satisfied only when Phase 8.9 is ready; review remains review. The rollback gate must match both rollback status and rollback evidence reference. A gate cannot claim success over a blocked, incomplete, unavailable, or mismatched source.

## 4. Backup and restore-test verification

`BackupVerificationResult` contains an opaque backup reference, verification status, restore-test status, and UTC verification time. A valid preparation requires `Verified` backup evidence and a `Passed` restore test. Unavailable, failed, or not-performed evidence blocks. Review status yields `RequiresReview` only when no blocker exists.

This boundary performs no backup. It does not access files, inspect a directory, open a database, create a copy, validate SQLite, or execute restore. The backup reference is an evidence identifier, not a path or payload. Actual backup and restore qualification remain responsibilities of separately approved operational tooling and procedure.

The coordinator rejects missing, unsafe, non-UTC, future-dated, or undefined backup evidence. It never attempts to repair or regenerate evidence and has no fallback to a production source.

## 5. Rollback verification

`RollbackVerificationResult` contains rollback-plan reference, `Verified`/`Unavailable`/`Failed`/`RequiresReview` status, owner reference, and evidence reference. The plan reference must exactly match the preparation context. Owner and evidence references must be safe and explicit.

A verified rollback result plus matching satisfied gate is required for approved preparation. Unavailable or failed rollback evidence blocks. Review evidence requires review and must be paired with a review gate. Mismatched plan, status, or gate evidence blocks as an evidence-integrity failure.

The rollback model does not execute rollback or perform a destructive operation. It has no database restore, file replacement, transaction reversal, delete, rename, process-control, or authority-routing member. It records governance evidence for a future manual decision only.

## 6. Stop conditions

The coordinator requires all six explicit stop-condition records:

- validation incomplete;
- backup unavailable;
- rollback unavailable;
- approval missing;
- evidence mismatch;
- environment mismatch.

Each record contains only type, triggered state, and safe evidence reference. Missing, duplicate, undefined, or unsafe stop-condition definitions block preparation. Any triggered condition produces a fixed blocker. A stop condition records evidence only; it does not shut down the application, disable a route, alter a feature flag, cancel a workflow, roll back data, or perform another automatic action.

The explicit records supplement rather than replace independent evidence validation. For example, an untriggered backup condition cannot override a failed backup result, and an untriggered mismatch condition cannot override a gate/reference mismatch.

## 7. Decision and cutover evidence package

`ProductionActivationReadinessCoordinator` is a parameterless pure evaluator. Every input is passed explicitly to `Evaluate`. It has no injected service, ambient configuration, clock, environment discovery, filesystem, database, repository, WinForms type, startup hook, host, activation executor, migration executor, feature executor, timer, scheduler, or background task.

The result policy is deterministic:

- `Blocked` if context, gates, approval bindings, backup, restore test, rollback, stop conditions, validation, deployment readiness, or cross-evidence bindings fail;
- `RequiresReview` when there is no blocker but at least one source or gate requires review;
- `ApprovedForPreparation` only when all evidence is complete, mutually consistent, and legacy authority remains preserved.

Dependency and input-enumeration failures are caught and reduced to `activation-preparation-evaluation-failed`. Raw exceptions, stack traces, rejected input, and implementation details never enter a result. The coordinator exposes fixed negative security and execution declarations: no password handling, credential mutation, RBAC creation, Support identity, secret storage, permission escalation, activation, deployment, migration, database modification, ESD cutover, route registration, or authority switch.

For structurally complete backup and rollback input, evaluation assembles `ProductionCutoverEvidencePackage`. It includes package ID, preparation decision, supplied gates, validation summary, rollback status, backup status, normalized blockers, normalized review items, and UTC assembly time. Its lists are defensive immutable copies.

The package excludes secrets, credentials, database dumps, raw logs, private keys, SQL, connection strings, file payloads, passwords, permission grants, and callbacks. It is not stored or transmitted. Phase 9.0 supplies no evidence repository, file writer, database table, audit sink, or network transport.

## 8. Legacy authority and security preservation

Only `LegacyAuthoritative` is valid in an accepted context. Every result permanently reports legacy authority preserved and no authority switch. The result also records no login replacement, settings replacement, reporting replacement, or runtime/event replacement. The coordinator registers no route and enables no pilot or production feature.

The preparation namespace contains no password verifier, password input, credential repository, credential mutation service, secret store, role assignment, permission table, RBAC implementation, Support profile, Support login, Support identity, escalation mechanism, authentication composition, or security-persistence adapter. Reviewer references are opaque governance evidence and do not become application identities.

Phase 9.0 also does not invoke the existing Phase 8.0 future activation interfaces. No `IProductionMigrationExecutor` or feature-activation executor implementation or dependency was added. The Phase 8.0 activation guard and production state machine remain separate and inactive.

## 9. Tests and verification

The focused Phase 9.0 suite contains 34 passing cases after theory expansion. Coverage includes context immutability and defensive copying; approved-for-preparation semantics; all six required gates; all three approval-reference bindings; missing and review approvals; backup and restore-test evidence; rollback evidence and plan binding; all six stop conditions; validation, deployment, backup, and rollback review paths; gate/source mismatches; explicit request and legacy-authority enforcement; hostile identifier rejection; cutover-package safety; throwing-enumerable isolation; legacy workflow preservation; security/non-execution declarations; namespace dependency scans; and startup/UI boundary scans.

Boundary tests pin the established `Program.cs` SHA-256. They verify that `Program.cs`, startup, and production forms do not reference the preparation namespace or coordinator. Reflection and source scans prove no SQLite provider, repository, migration runner, production migration executor, WinForms type, production UI dependency, service locator, timer, background task, deployment/activation/migration/restore method, credential service, escalation method, or Support identity dependency.

The complete Release solution builds with zero errors and the same six pre-existing NU1701 compatibility warnings for OpenTK, OpenTK.GLControl, and SkiaSharp.Views.WindowsForms. The complete test suite passes. `git diff --check` passes. `Program.cs`, startup, production WinForms, and navigation are unchanged by Phase 9.0. No production database was accessed; no migration, deployment, activation, ESD cutover, or authority switch occurred; and no RBAC or Support identity was introduced.

## 10. Remaining requirements before activation

Phase 9.0 is preparation governance, not activation authorization. Before any production activation, a separately approved process must provide real installation-specific evidence, verify package provenance and integrity, qualify the target environment, validate backup restoration in an isolated environment, approve a rollback runbook, assign accountable operators and reviewers, define separation of duties, establish approval expiry and revocation, specify a maintenance window, define monitoring thresholds and observation duration, complete privacy and security review, and retain auditable evidence under an approved policy.

A future activation phase would still need an explicit short-lived authorization bound to the exact release, target, evidence package, database identity, and correlation; an independently reviewed executor; transaction and locking controls; post-action verification; incident response; and an explicit authority-cutover decision. Authentication, reporting, settings, runtime/events, export, migration, and ESD authority must remain independently gated.

No Phase 9.0 result may bypass the existing activation guard, transition production state, invoke an executor, or be interpreted as `ApprovedForActivation`. Legacy production workflows remain authoritative until a future authorized cutover is implemented and explicitly approved.
