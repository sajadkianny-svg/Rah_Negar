# Phase 8.0 — Controlled Production Activation Planning Foundation

Status: **Implemented as inactive planning and control contracts; production activation remains blocked**

Date: 2026-08-22

## 1. Executive conclusion

Phase 8.0 adds the control-plane foundation that must sit between Phase 7.9 technical readiness and any future production activation. It models explicit state transitions, a safe evidence package, operator approval, a fail-closed activation guard, audit evidence, rollback readiness, the cutover checklist, disabled feature boundaries, and the future production migration executor contract.

This phase performs no activation. It does not register a migration, alter startup, enable authentication, change WinForms, choose or open a deployment database, run a production migration, provision or cut over ESD authority, or change a feature flag. There is no production implementation of the migration or feature-activation executor interfaces.

The controlled boundary is:

```text
Phase 7.9 technical readiness
        -> immutable activation evidence package
        -> explicit operational approval
        -> fail-closed activation guard
        -> explicit migration authorization
        -> future executor contract
        -> stop
```

Repository-level foundation status is complete. Installation activation status remains `NotPrepared` because no real database, evidence package, operator approval, maintenance window, or migration authorization was supplied.

## 2. Architecture

The implementation is isolated under `Foundation.Application.Activation`:

- `ProductionActivationContracts.cs` defines immutable state, evidence, approval, guard, migration-command, audit, and executor contracts.
- `ProductionActivationPolicies.cs` defines pure transition, evidence, approval, guard, audit, and approved-context validation policies.
- `ProductionCutoverPlanning.cs` defines rollback readiness, checklist, disabled feature boundaries, and legacy/target comparison.
- `ControlledProductionActivationPlanningTests.cs` contains pure tests and the only production-migration executor test double.

The namespace depends on Phase 7.9 application evidence contracts and the Phase 7 security reconciliation enums. It has no Microsoft.Data.Sqlite, WinForms, migration-runner, startup, filesystem-discovery, repository, or infrastructure dependency. The only filesystem operation in the policy layer is `Path.IsPathFullyQualified`, used to reject an implicit future executor target; it does not inspect or open the path.

No Phase 8.0 type is composed in `Program.cs` or an existing startup coordinator. All policies require an explicit caller invocation and explicit evidence arguments.

## 3. Production activation state machine

`ProductionActivationState` provides the required states:

- `NotPrepared`
- `AssessmentReady`
- `BackupVerified`
- `RehearsalVerified`
- `ApprovalPending`
- `ApprovedForActivation`
- `ActivationInProgress`
- `Activated`
- `ActivationBlocked`
- `ActivationRolledBack`

`ProductionActivationStateTransitionPolicy` evaluates an explicit `ActivationStateTransitionRequest`. Every request names the from-state, to-state, transition ID, correlation ID, actor reference, and UTC request time. Evaluation returns an immutable accepted/rejected result; it does not store state or invoke a downstream action.

The normal forward path is:

```text
NotPrepared
  -> AssessmentReady
  -> BackupVerified
  -> RehearsalVerified
  -> ApprovalPending
  -> ApprovedForActivation
  -> ActivationInProgress
  -> Activated
```

Explicit blocking is permitted from every pre-activation/in-progress stage. `ActivationInProgress` can explicitly move to `ActivationRolledBack`; an `Activated` installation can also record a later explicit rollback. `ActivationBlocked` and `ActivationRolledBack` can only return to `NotPrepared`, forcing reassessment from the beginning. Same-state requests, skipped stages, backwards promotion, and missing transition evidence are rejected. No timer, constructor, evidence validator, approval result, or guard result automatically advances state.

## 4. Activation evidence package

`ActivationEvidencePackage` is an immutable aggregate assembled before approval. It contains:

- evidence package ID and correlation ID;
- target database identity fingerprint;
- read-only preflight summary and inspection time;
- migration classification, supported target version, chain support, and checksum validation state;
- backup receipt ID, source/backup identity fingerprints, verification/integrity state, size, and creation time;
- rehearsal receipt ID, result, idempotency, original-backup preservation, final version, ESD reconciliation state, authority mode, and completion time;
- preflight, foreign-key, backup, and rehearsal integrity results;
- finalized snapshot, report lock, legacy evidence, and ESD preservation results;
- explicit no-RBAC and no-Support-identity evidence;
- operator approval metadata boundary;
- package assembly time.

The package deliberately uses safe summaries rather than carrying raw database data, SQLite rows, raw signed vendor authorizations, credential records, audit dictionaries, or exception text. It has no password, password hash, salt, credential verifier, private key, recovery code, support authorization secret, or raw authorization field. Database and backup identity fingerprints are non-secret correlation evidence; they are not credential hashes.

`ActivationEvidencePackageValidator` fails closed unless every required element is present and internally consistent. It requires successful read-only preflight, database and foreign-key integrity, a supported clean migration classification, verified backup, successful idempotent rehearsal, unchanged original backup, legacy-authoritative non-conflicting ESD inspection, complete preservation evidence, matching database identities, UTC timestamps, sensible chronology, and an explicit approval boundary bound to the package and database.

An evidence package is not approval. The `OperatorApprovalMetadataBoundary` states that explicit approval is required and names the required scope, database identity, and package identity. Actual approval is a separate contract created only after evidence assembly.

## 5. Approval boundary

`ProductionActivationApproval` is UI-neutral and storage-neutral. It explicitly records:

- approval ID;
- approved-by actor reference;
- approval timestamp in UTC;
- approved scope;
- target database identity fingerprint;
- evidence package ID;
- correlation ID;
- optional expiration in UTC.

The actor reference is an opaque operator boundary, not a password, credential record, or new application identity. Phase 8.0 does not define an approval screen, user workflow, login, persistence repository, or production approver source.

`ProductionActivationApprovalValidator` requires a structurally valid approval, a non-future approval timestamp, a valid optional lifetime, exact requested scope, exact database identity, exact evidence package identity, and exact correlation ID. It produces safe categories for malformed, not-yet-valid, expired, wrong-scope, wrong-database, wrong-package, and wrong-correlation evidence. Expiry is exclusive: an approval is invalid at or after its expiry.

Approval cannot repair or override failed technical evidence. It is a necessary input to the guard, not a master bypass.

## 6. Activation guard

`ProductionActivationGuard` is an inactive application service with an injected clock. It evaluates the Phase 7.9 maintenance readiness result and the underlying preflight, migration classification, backup, rehearsal, evidence package, approval, and required scope.

It returns one of:

- `Allowed`
- `Blocked`
- `RequiresManualReview`

`Allowed` requires all of the following:

1. Phase 7.9 status is `ReadyForFutureMigrationApproval` with no blocker.
2. Preflight, SQLite integrity, foreign-key integrity, and enforced read-only inspection passed.
3. Classification is exactly `CleanLegacyBaseline` or `CleanUnifiedTarget`.
4. Backup is verified, integrity passed, and has no backup failure.
5. Rehearsal passed, was idempotent, left the original backup unchanged, and passed preservation verification.
6. ESD remains `LegacyAuthoritative` and has no conflict/invalid/failure state.
7. The activation evidence package is complete.
8. The evidence database identity matches preflight and backup source identities.
9. The evidence backup identity, migration classification, rehearsal version, ESD state, and approval scope match their source readiness results.
10. A current approval matches scope, database, evidence package, and correlation.

Recognized historical drafts and adoption-required histories return `RequiresManualReview`. They are never silently promoted or converted to an allowed result. All other missing, unsafe, corrupt, unknown, checksum-failed, expired, mismatched, or incomplete conditions return `Blocked`. The guard catches no exception to manufacture success and has no bypass flag.

The decision still does not execute anything or transition state. A caller must explicitly request the `ApprovalPending -> ApprovedForActivation` transition after retaining the guard evidence.

## 7. Future production migration command boundary

`IProductionMigrationExecutor` is the future execution boundary. Its input, `ApprovedProductionMigrationContext`, requires:

- a fully qualified database path explicitly supplied by a future caller;
- a complete evidence package;
- a valid activation approval;
- a separate `ExplicitProductionMigrationAuthorization`;
- an `Allowed` guard result.

The explicit migration authorization binds authorization ID, authorizing actor reference, approval ID, evidence package ID, target database identity, correlation ID, issue time, and expiry. `ApprovedProductionMigrationContextValidator` checks every binding and lifetime without touching the path.

Phase 8.0 provides no application or infrastructure class implementing `IProductionMigrationExecutor`. The only implementation is a private test double in the test assembly; it validates the context and returns a synthetic result without opening a database. The same rule applies to `IFutureFeatureActivationExecutor`.

Consequently, reaching `ApprovedForActivation` in planning data cannot cause migration execution. A future separately approved phase must implement and compose the executor.

## 8. Activation audit model

`ActivationAuditEntry` is typed and contains only:

- audit entry ID;
- action;
- from/to activation state;
- correlation ID;
- database identity fingerprint;
- evidence package ID;
- opaque actor reference;
- UTC timestamp;
- safe result enum.

Actions distinguish transition request/rejection, evidence assembly, approval recording, guard evaluation, activation request, and rollback decision recording. Results distinguish success, rejection, blocking, and manual review.

There is no arbitrary metadata dictionary or raw payload field. `ActivationAuditEntryValidator` requires all correlation fields, actor reference, and UTC time. `IActivationAuditSink` is only a persistence boundary; Phase 8.0 supplies no production sink and writes no audit record to a real database.

## 9. Rollback readiness

`RollbackReadinessEvidence` represents:

- backup availability;
- backup verification;
- restore validation;
- assigned rollback-owner actor reference;
- explicit rollback decision boundary.

The decision boundary can be `NotEstablished`, `ManualDecisionRequired`, or `ExplicitRestoreAuthorizationRequired`. `RollbackReadinessEvaluator` returns `Ready` only when backup, verification, restore validation, owner, and a non-default manual decision boundary are all present. Readiness never initiates restore or rollback.

This model complements, rather than replaces, Phase 7.9 operational rollback states. It establishes who decides and what evidence must exist. It does not copy, replace, delete, rename, or roll back a database.

## 10. Production cutover checklist

The structured checklist contains all required items and categories.

Technical:

- build verified;
- tests passed;
- migration rehearsal passed;
- backup verified;
- restore validated;
- disk capacity checked;
- lock policy checked.

Operational:

- maintenance window approved;
- operator assigned;
- rollback owner assigned;
- support contact available;
- monitoring plan available.

Security:

- ShiftProfile model confirmed;
- ManagementCredential model confirmed;
- vendor authorization boundary confirmed;
- no Support identity;
- no RBAC introduced.

`ProductionCutoverChecklistEvaluator` rejects missing, duplicate, or wrongly categorized items. A list is complete only when every required item appears exactly once in the correct category. It is confirmed only when every item has `Confirmed` status and a nonblank evidence reference. The default planning checklist includes every item as `Pending`, so it is structurally complete but never cutover-ready.

“Support contact available” is operational contact information, consistent with Phase 7.5. It is not a local Support role, profile, login, authorization source, or normal application identity.

## 11. Feature activation boundaries

The future controlled feature catalog covers:

- new authentication workflow;
- snapshot reporting workflow;
- protected settings workflow;
- migration tooling.

`FeatureActivationBoundarySnapshot.Inactive` contains every feature with state `Disabled` and category `Phase8PlanningOnly`. The contracts can describe a future planned or enabled state, and `FutureFeatureActivationRequest` binds a feature to evidence, approval, and correlation IDs, but Phase 8.0 supplies no executor implementation.

No production configuration, flag provider, startup step, form, or feature-routing branch is changed. Merely constructing a future request or state value has no production effect.

## 12. Current legacy versus future target authority

`ProductionReadinessComparison.CreateCurrent()` exposes six remaining activation gaps:

| Dimension | Current legacy authority | Future target authority | Remaining gap |
|---|---|---|---|
| Authentication | Existing production composition and legacy login behavior | ShiftProfile authentication backed by internal credential records | UI/composition, credential provisioning, recovery, and approval remain absent. |
| Reporting | Legacy live reporting and ordinary ShiftProfile Finalize | Snapshot-backed finalized reporting while retaining ordinary Finalize | Production read routing and UI adoption are disabled. |
| Snapshots | Legacy finalized-month protection/evidence | Immutable `ReportSnapshots` and `ReportPeriodLocks` | Installation preservation proof and controlled read cutover are required. |
| ESD settings | `app_settings.esd_extra_runtime_hours` | `SecurityDeploymentSettings` with protected exactly-once changes | Conflict resolution, provisioning, and explicit ESD authority cutover remain future work. |
| Security persistence | Legacy production behavior without target composition | ShiftProfile credentials, singleton ManagementCredential, trusted public keys, audit, and replay receipts | Provisioning, operational procedures, and composition remain inactive. |
| Migration state | No unified chain registered at startup | Explicit checksummed unified chain through version 4 | Installation assessment, authorization, maintenance execution, and validation remain future work. |

Every target is marked inactive or future-activation-required. None is represented as current production authority.

## 13. Tests

`ControlledProductionActivationPlanningTests` covers:

- every normal activation transition;
- invalid skip, reversal, same-state, and missing-evidence rejection;
- complete and incomplete evidence-package validation;
- absence of secret-bearing evidence/audit fields;
- valid approval;
- expired approval;
- wrong database identity rejection;
- guard allowance for a complete, mutually bound package only;
- guard blocking for missing approval and failed backup;
- manual-review routing for recognized draft adoption;
- rollback readiness and missing-owner/decision failures;
- checklist structure, confirmation, and missing-item failure;
- every controlled feature remaining disabled;
- absence of production migration and feature executor implementations;
- approved-context validation by a test-only executor double;
- six-dimension legacy/target comparison completeness;
- absence of startup, WinForms, SQLite, and migration-runner dependencies in the activation namespace.

The focused suite contains 27 passing cases after xUnit theory expansion. Full solution results are recorded in the verification section.

## 14. Current activation assessment

| Control | Current Phase 8.0 state |
|---|---|
| Technical readiness | Foundation available; no installation evidence assembled. |
| Activation evidence | No production package exists. |
| Operational approval | Not requested or stored. |
| Backup confirmation | No production backup selected or confirmed. |
| Rehearsal confirmation | No production installation rehearsal performed. |
| Migration authorization | Not issued. |
| Rollback readiness | Not assessed for an installation. |
| Cutover checklist | Planning template is complete but all items default to Pending. |
| Feature activation | All four controlled features are Disabled. |
| ESD authority | Legacy remains authoritative; no cutover mechanism is composed. |
| Final activation state | `NotPrepared` / `ActivationBlocked` for any attempted execution. |

## 15. Known limitations

- No real operator identity, approval UI, approval storage, signature, separation-of-duties policy, or revocation source is implemented.
- Evidence IDs and actor references are application boundaries; future persistence must enforce uniqueness, immutability, append-only audit, and retention.
- The approval validator proves binding and time validity, not that the approver had organizational authority. A future adapter must supply that trust decision.
- No production executor exists, so transaction, maintenance lock, crash recovery, and post-commit validation are intentionally unimplemented here.
- No automatic rollback exists. Actual restore requires a separately approved runbook and explicit decision.
- Monitoring criteria, observation duration, escalation contacts, and stop conditions are checklist evidence boundaries, not monitoring implementations.
- Feature contracts do not alter current flags and do not supply runtime routing.
- The six pre-existing NU1701 package compatibility warnings remain unresolved and require separate production qualification.

## 16. Remaining steps before actual activation

1. Select a real installation database explicitly in an approved maintenance-assessment process.
2. Complete Phase 7.9 read-only preflight, classification, verified backup, restore validation, isolated rehearsal, preservation validation, disk assessment, and lock policy evidence.
3. Resolve every manual-adoption, checksum, integrity, ESD, snapshot, or history issue without rewriting migration history.
4. Define operator and approver authority, separation of duties, approval persistence, revocation, expiry, audit retention, and emergency procedures.
5. Assign the maintenance operator and rollback owner; approve the window, monitoring plan, support contact, and stop conditions.
6. Complete every cutover checklist item with independently reviewable evidence.
7. Create an immutable activation evidence package and validate all source-result bindings.
8. Obtain approval scoped to the exact database identity, evidence package, and correlation.
9. Obtain separate explicit migration authorization with a short lifetime.
10. Implement the production executor only in a future reviewed phase, applying Phase 7.9 busy/lock, backup, transaction, validation, audit, and recovery policies.
11. Keep authentication, snapshot routing, protected settings, and migration tooling independently disabled until each receives its own controlled activation approval.
12. Design ESD provisioning and authority cutover as a separately approved action; production activation must not imply ESD cutover.

## 17. Verification record

Required final verification:

- build the complete solution;
- run the complete test suite;
- run `git diff --check`;
- confirm the pre-phase `Program.cs` SHA-256 and Git diff are unchanged;
- confirm no production WinForms or startup diff;
- confirm no Phase 8.0 reference from `Program.cs`, startup, or UI;
- confirm no production database path discovery or access;
- confirm no migration runner/executor implementation or invocation;
- confirm no ESD provisioning/cutover or target-authority activation;
- confirm no authentication composition or feature enablement;
- confirm no RBAC or local Support identity.

Verification completed with these results:

- Debug solution build: succeeded with zero errors and six pre-existing NU1701 warnings.
- Release solution build: succeeded with zero errors and the same six warnings.
- Complete Debug and Release test suites: 335 passed, zero failed, zero skipped in each configuration.
- Focused Phase 8.0 suite: 27 passed.
- `git diff --check`: passed; it emitted only pre-existing line-ending notices for unrelated working-tree files.
- `Program.cs`: unchanged from the pre-phase SHA-256 `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76` and has no Git diff.
- Production WinForms and application startup foundation: no Git diff and no Phase 8.0 reference.
- Activation namespace: no SQLite, WinForms, migration-runner, ESD provisioning, target-authority, environment-path, or database-discovery dependency.
- Executor boundaries: interfaces only in the production assembly; the migration executor test double is private to tests.
- Features: all controlled feature entries remain `Disabled`; no production flag or authentication composition changed.
- Security model: ShiftProfile and ManagementCredential boundaries remain unchanged; no RBAC or Support role/profile/login was introduced.
- Production effects: no production database was selected or opened, no migration executed, and no ESD authority cutover occurred.

Regardless of foundation verification, actual production activation remains outside Phase 8.0.
