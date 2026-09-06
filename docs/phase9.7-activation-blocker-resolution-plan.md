# Phase 9.7 — Activation Blocker Resolution Plan

## Boundary

This plan resolves technically implementable Phase 9.6 gaps for a future controlled decision. It does not authorize or execute Production Activation or Production Cutover. The current state remains: Legacy authoritative, Target non-authoritative, Target routing disabled, Production Activation unauthorized, and Production Cutover unauthorized.

## Authoritative inherited inventory

| ID | Inherited blocker/gap and origin | Severity | Category | Resolvable in 9.7 | Dependency | Implementation / qualification target | Status |
|---|---|---|---|---|---|---|---|
| B-01 | No future decision artifact bound to installation, scope, version, expiry, and owner; 9.6H PR-01/PR-20 | HIGH | GOVERNANCE | No | Project Owner/governance | Typed contract and rejection tests; decision itself remains future | GOVERNANCE OPEN |
| B-02 | No governed Production execution boundary; 9.6F PR-06/PR-17 | HIGH | TECHNICAL_IMPLEMENTATION | Yes | B-01 for real execution | `ProductionExecutionContext`, staged executor, default-deny verifier | RESOLVED FOR FUTURE QUALIFICATION |
| B-03 | No physical/single-writer fence and handoff boundary; 9.6D GD-05/GD-16/GD-19 | HIGH | TECHNICAL_IMPLEMENTATION | Yes | B-02 | Named mutex + exclusive lock + generation lease; write gate/drain tests | RESOLVED FOR FUTURE QUALIFICATION |
| B-04 | Authority commit/routing ordering was rehearsal-only; 9.6C/9.6D GD-02/GD-04 | HIGH | TECHNICAL_IMPLEMENTATION | Yes | B-02/B-03 | Canonical store adapter, atomic qualification state, route-after-authority tests | RESOLVED FOR FUTURE QUALIFICATION |
| B-05 | Governed physical Production rollback/custody absent; 9.6F PR-07/PR-17 | HIGH | TECHNICAL_IMPLEMENTATION | Yes for architecture; no real execution | B-01/B-02 and backup custody | Eligibility states, fenced rollback executor, recovery-required path | RESOLVED FOR FUTURE QUALIFICATION; REAL CUSTODY OPEN |
| B-06 | Final Production-like Target/handoff evidence absent; 9.6F PR-08 | HIGH | TECHNICAL_QUALIFICATION | Partly | Real installation and controlled handoff | Handoff evidence package and disposable harness; real Production evidence remains future | OPEN — FUTURE INSTALLATION EVIDENCE |
| B-07 | Durable immutable/tamper-evident retention/readback unproven; 9.6F PR-18/GD-19 | HIGH | TECHNICAL_IMPLEMENTATION | Yes for local chain | Retention custody policy | Append-only JSONL sequence/hash chain and integrity failure fail-closed | RESOLVED FOR IMPLEMENTED SCOPE |
| B-08 | Versioned runbook/package not finally approved; 9.6F PR-16/9.6G | HIGH | OPERATIONAL | No | Owner/operator approval | Compatibility documented in handoff; approval remains open | GOVERNANCE/OPERATIONAL OPEN |
| B-09 | Independent Human Review unavailable; 9.5/9.6H | HIGH | INDEPENDENT_REVIEW | No | Independent reviewer availability | Preserve exact limitation; no coding substitute | NOT PERFORMED / UNAVAILABLE |
| B-10 | MQ-07 manual observation not practically exercisable; 9.6B2 | MEDIUM | RESIDUAL_LIMITATION | No | Existing owner acceptance | Preserve exact accepted residual treatment and automated evidence | BLOCKED — EXISTING ACCEPTED LIMITATION |
| B-11 | Generic activation-scope reconciliation was incomplete; 9.6H | MEDIUM | FUTURE_PRODUCTION_EXECUTION | No scope-specific branch added | Generic future deployment composition | Context binds generic station/profile scope without Rasht/Ramsar Production branching | OPEN — FUTURE COMPOSITION |
| B-12 | Final governance decision and explicit activation approval absent; 9.6H | HIGH | GOVERNANCE | No | B-01, B-06, B-08, B-09 | Readiness reassessment and exact next decision; no decision recorded here | OPEN — OWNER DECISION REQUIRED |

## Required future commit order

The executor validates prerequisites, contract, management proof, fence, drain, Legacy state, Target non-authority, disabled Target route, reconciliation, divergence, backup, and durable audit preparation before canonical authority commit. It then verifies Target authority, enables routing, verifies Legacy de-authorization, finalizes audit, and releases the fence. Any ambiguous post-commit or rollback outcome enters `RECOVERY_REQUIRED`; no authority is guessed.

## Non-negotiable safety state

MQ-07 remains `BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED`. Independent Human Review remains `NOT PERFORMED / UNAVAILABLE`; AI-assisted review is not organizationally independent. No RBAC, Support identity, master credential, hidden bypass, generic override, station-specific Production branch, 35-unit support, or artificial Production delay is introduced.

## Initial audit output A–K

### A. Architecture map

The live application path remains `Program.cs` → legacy WinForms startup/login → `Data/SqliteDatabaseHelper` → `Data/db.sys` → legacy `Services`, `UI`, and station profiles. The inactive target path is separated under `Application`, `Core`, and `Infrastructure`; its existing migration, provisioning, reporting, security, authority, reconciliation, and pilot layers are not registered by production startup. Phase 9.7 adds the future authority boundary in `Application/Authority/Phase97ProductionExecution.cs`, with `FileProductionAuthorityStore` and `TamperEvidentAuthorityAuditSink` available for future composition, and the disposable path in `Qualification/`.

### B. Build status

Final Release solution build: **PASS**, 0 errors, 6 NU1701 warnings. Final full suite: **759/759 PASS**, 0 failed, 0 skipped. Final Phase 9.7 harness: **PASS**; focused harness steps were 38/38 and 104/104.

### C. Dependency/package health

The solution and transitive package inventory reported no vulnerable packages from the configured NuGet sources. No dependency was changed. Six known NU1701 compatibility warnings remain for OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 on net8.0-windows.

### D. Confirmed findings

The confirmed inherited findings were the missing governed Production execution boundary, physical writer handoff, governed rollback custody, durable retention/readback, installation-bound handoff evidence, and open governance/review requirements. They are tracked in B-01 through B-12 and G-97-10 through G-97-17. No new regression or confirmed business-rule defect was found in the unchanged Legacy-authoritative workflow during this phase.

### E. Potential findings requiring validation

Future deployment must validate composition of every real writer with `ProductionWriteGate`, physical lock placement/permissions, machine-restart and abandoned-mutex behavior, real Target data equivalence, backup custody, operator training, and manual DPI evidence. These are not claimed as current bugs or as satisfied by disposable tests.

### F. Incomplete functionality

The installation-bound approval, final Target handoff manifest, physical restore operator/custody, approved runbook/training, independent human review, and explicit owner decision remain incomplete. The current repository intentionally has no Production cutover UI or startup route.

### G. Database/schema risks

The current Production database `Data/db.sys` is absent and remains untouched. Authority and transition metadata are separate integrity-checked files; the Phase 9.7 file adapter serializes each canonical state update and enters recovery on ambiguous failure, but a future deployment must validate installation-level custody and restart behavior. No destructive schema change was made; SQLite WAL sidecars remain part of the existing backup/restore evidence contract.

### H. Performance

No Phase 9.7 performance regression was observed. The write gate is event/barrier based and adds no artificial sleep or Production timing delay. The audit sink serializes local lifecycle appends intentionally; existing UI/DataGrid and startup performance concerns remain historical review areas, not newly confirmed Phase 9.7 blockers.

### I. UI/DPI

No Production cutover or recovery override UI was added. Existing historical 100/125/150 DPI evidence remains historical, and MQ-07 manual active-session observation remains blocked under the accepted limitation. No new UI/DPI claim is made.

### J. Duplication/technical debt

Legacy static persistence services and inactive target services remain separate by design. The new boundary is additive and avoids refactoring those services; before any real cutover, all authority-sensitive writers must be composed through the gate and the installation-bound authority store. Existing phase-specific contracts and duplicate preparation/readiness models remain documented technical debt.

### K. Prioritized remediation plan

1. Obtain independent human review and a Project Owner decision bound to the actual installation, scope, version, schema, evidence, correlation, and expiry.
2. Produce and approve the final Production-like Target/handoff manifest, backup/restore custody, operator training, and evidence-retention custody.
3. Compose and rehearse every real writer with the fence/drain boundary on disposable copies, including machine restart and physical restore verification.
4. Only after the above, hold a separate explicit governance decision about whether a controlled Production cutover may be authorized. Phase 9.7 performs none of these authorization or cutover actions.
