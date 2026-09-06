# Phase 9.6E — Production-like Rehearsal Results

Status: **COMPLETED — ISOLATED REHEARSAL ONLY**  
Rehearsal UTC: 2026-09-06 (execution timestamp captured in `Qualification/qualification-run/phase9.6e-result.json`)  
Branch: `phase9.6-activation-readiness`  
Base/current implementation commit: `ae2c07b703a3150144d29a709a67d6e0c9840f1` (pre-change base)

## Boundary and environment

- Environment: Windows, .NET 8 / `net8.0-windows`, offline local qualification.
- Rehearsal path: `Qualification/qualification-run/`.
- Disposable databases: `Qualification/qualification-run/databases/Rasht/db.sys` and `Ramsar/db.sys`, created by `QualificationTool`.
- Authority, transition, and audit state used by the focused tests is created under disposable temporary directories; no Production state is used.
- No secrets are stored in the evidence package.

Production isolation pre-state: `Data/db.sys` was absent. It was not created for qualification. The script records absent pre/post state and fails on any unexpected Production DB change.

## Baseline

Qualification authority is initialized/persisted as `LegacyAuthoritative`, with `LegacyAuthoritative=true`, `TargetAuthoritative=false`, and `TargetRoutingEnabled=false`. The qualification scope is `qualification/all`; the first transition generation is `1`. Transition metadata is absent before a scenario, and audit evidence is written to an isolated sink/file. Backup evidence is represented by a verified, scope- and correlation-bound receipt contract; invalid, unavailable, or mismatched evidence is rejected.

## Rehearsal results

| Area | Result | Evidence |
|---|---|---|
| Happy path | PASS | `Isolated_happy_path...`; commits `TargetAuthoritative`, keeps routing disabled, and restart reconstructs `CommitPersisted` deterministically. |
| Routing order | PASS | Target routing readiness is true only after committed Target authority; route remains disabled in the committed state. |
| Abort | PASS | D3 tests cover prepared abort, idempotent retry, wrong correlation, stale generation, persistence/audit failure, and restart. |
| Safe rollback | PASS | D3 rollback service accepts only isolated context plus complete safety evidence and restores Legacy. |
| Unsafe rollback | PASS | Target writes, divergence, post-handoff events/reports, incomplete audit, missing receipt, or restore readiness produce `ROLLBACK_NOT_SAFE`. |
| Recovery-required | PASS | Authority/transition/audit/abort/rollback failures and malformed metadata fail closed with both routes disabled. |
| Reconciliation | PASS | `MATCHED`, `MISMATCHED`, `BLOCKED`, `NOT_EVALUATED`; evaluator is read-only. |
| Divergence | PASS | `SYNCHRONIZED`, `DELTA_PENDING`, `DIVERGED`, `BLOCKED`; evaluator is read-only. |
| Fencing/concurrency | PASS | Mutex, scope/correlation/generation checks, replay rejection, stale writer rejection, and deterministic retry behavior are covered by D3/E tests. |
| Failure injection | PASS | Authority, transition, audit, backup/evidence, reconciliation, abort, rollback, malformed/corrupt metadata, and restart fail-closed contracts are covered by focused/D3 tests. No artificial timing delay is used. |
| Startup/recovery | PASS | Valid Legacy/Target rehearsal state persists; incomplete, contradictory, malformed, or corrupt state blocks normal routing and uses the Persian recovery message contract. |
| Backup evidence | PASS | Existing managed backup receipt is consumed by `VerifiedBackupEvidenceFactory`; scope/correlation/integrity requirements are enforced. |

## Validation evidence

- Focused Phase 9.6E qualification: **PASS — 5/5 tests**; TRX: `Qualification/qualification-run/phase9.6e.trx`.
- Qualification script: **PASS** — `Qualification/run-phase9.6e-rehearsal.ps1`; result JSON: `Qualification/qualification-run/phase9.6e-result.json`.
- Full automated suite: **PASS — 734 tests**.
- Normal solution build: **PASS**, with the known 12 NU1701 warnings and 0 errors.
- `git diff --check`: PASS.

## Exact safety state after rehearsal

Production was never transitioned. The application production path remains:

- Legacy = **AUTHORITATIVE**
- Target = **NON-AUTHORITATIVE**
- Target Routing = **DISABLED**
- Production Activation = **UNAUTHORIZED**
- Production Cutover = **UNAUTHORIZED**

No Production cutover UI, unrestricted executor, override/waiver, RBAC, Support identity, master credential, 35-unit support, Rasht/Ramsar Production branching, generic STOP, or artificial Production delay was introduced. MQ-07 remains exactly: **BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**.

## Remaining gaps

Production authority history/executor, physical Production swap, governed Production rollback, write drain/fence/final sync, governance/runbook repair tooling, immutable audit retention/readback, and activation qualification remain open. The isolated service is not registered by `Program.cs` and rejects `RehearsalContext.Production` before state access. These are Phase 9.6F/G or later boundaries and are not authorized by this rehearsal.

READY TO BEGIN PHASE 9.6F

## Execution addendum — 2026-09-06T21:10:21.7460018Z

The repeatable run completed with 12/12 Phase 9.6E tests and 20/20 total focused tests when the 8 D3 authority tests were included. The launcher now refreshes only the disposable rehearsal directory and records `production-pre-state.json` and `production-post-state.json`. Both show `Data/db.sys` absent and both production authority metadata files absent; the result JSON records `productionIsolationUnchanged=true`, `productionMetadataUnchanged=true`, `disposableDatabaseInputsOnly=true`, and `productionDatabaseUsedAsWritableInput=false`.

This addendum supersedes the earlier 5/5 focused-test count in the historical validation bullet.

Final post-change validation supersedes the historical suite count as follows: full automated suite **PASS — 741/741**, solution build **PASS — 0 errors** (the existing NU1701 compatibility warnings remain), and `git diff --check` **PASS**.
