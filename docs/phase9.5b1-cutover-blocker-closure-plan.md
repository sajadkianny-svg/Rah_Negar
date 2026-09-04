# Phase 9.5B1 — Cutover Blocker Extraction and Closure Plan

Status: **PHASE 9.5B1 CLOSURE PLAN READY**

Date: 2026-09-04

Branch: `phase9-operational-readiness`

Baseline commit: `6b7047e5c71c2ee6cb3830c04d5edfb2c8dcd383`

> This document is a blocker-extraction and closure plan only. It does not authorize or execute production cutover, production authority transition, production migration, production-data mutation, schema change, production startup change, commit, or push. Legacy remains the sole production authority.

## 1. Objective and review boundary

This plan converts the Phase 9.5A `BLOCKED` decision into a small, ordered set of closure tasks. It extracts every mandatory gate whose Phase 9.5A state is `BLOCKED` or `CONDITIONAL`, identifies the exact missing capability or evidence, distinguishes work that can be completed locally from evidence that can exist only against the real production installation, and defines the earliest honest entry point for a future pre-cutover verification.

The review used only these sources:

1. `docs/phase9.5a-cutover-readiness-gate.md` — primary and authoritative gate source.
2. `docs/phase9.4-final-qualification-report.md` — final Phase 9.4 disposition.
3. `docs/phase9.4b-manual-pilot-qualification-results.md` — authoritative manual checklist record.
4. `docs/phase9-controlled-live-pilot-integration-report.md` — Phase 9.3 implementation and automated Pilot evidence.

This was not a repository audit. READY gates were not re-audited except where their stated invariant is a dependency of an unresolved gate. No production database was accessed. No production code, test code, schema, data, startup behavior, authority state, or package declaration was changed.

## 2. Extracted decision

Phase 9.5A contains 56 mandatory gates:

- 22 READY;
- 17 CONDITIONAL;
- 17 BLOCKED;
- 0 NOT APPLICABLE.

All 34 unresolved gates are mandatory before production cutover. There are no optional unresolved gates. A mandatory `CONDITIONAL` gate prevents GO just as a `BLOCKED` gate does, but the reason differs:

- `BLOCKED` identifies an absent required production capability/control/procedure or a confirmed unsafe/conflicting operational path.
- `CONDITIONAL` identifies credible foundations for which current installation-specific, manual, approval, or time-sensitive evidence is missing.

The software-side and locally executable blockers must be closed before production-bound verification begins. Production-only evidence must not be relabeled as a software defect or manufactured from qualification fixtures. Some gates, especially DB-05, can receive an approved procedure before cutover but can obtain their final observation only at a future authorized cutover hold point before target authority is accepted.

## 3. Closure-route definitions

The `Closure route` field below uses the requested classification:

- **A — now using isolated/local evidence:** existing capability can be evaluated or documented without production access or a code change.
- **B — small implementation/test task:** a bounded production-code and focused-test change is required. A gate may also require later production binding after its software blocker is removed.
- **C — real pre-cutover production verification:** final evidence must be captured against the exact production installation, database identity/backup, final binary, station, approvals, and maintenance window. This route does not itself authorize cutover.
- **D — human/manual qualification:** a person must execute and record the defined interaction, visual check, approval, or procedural qualification. This may be performed in an isolated environment unless the gate explicitly requires the production installation.

Where two routes are shown, they are sequential requirements rather than alternatives. `B → C`, for example, means the missing capability must first be implemented and qualified locally, and the resulting final gate evidence must later be bound to production.

## 4. Exact BLOCKED gate inventory

All gates in this table are mandatory before cutover.

| Gate ID | Requirement | Current state | Mandatory | Exact missing evidence or capability | Why unresolved | Closure route | Dependencies | Recommended order |
|---|---|---|---|---|---|---|---|---|
| AUTH-03 | Authority transition is explicit, approved, installation-bound, audited, and executable only at the authorized decision point. | BLOCKED | Yes | Production activation boundary; durable exact authority-state persistence; approval/context binding; audit emission; fail-closed transition procedure; production implementations behind `IFutureFeatureActivationExecutor` and the authority adapter. | Contracts and policy exist, but the production executor/adapter intentionally do not. Planning or successful migration cannot establish authority. | B → C/D | SEC-01–SEC-05 and SEC-08; MIG-02–MIG-04; DB-03 and BR-02–BR-06; approved authority policy. | 6 |
| AUTH-04 | Rollback authority behavior defines trigger, owner, routing to Legacy, data boundary, audit, and terminal authority state. | BLOCKED | Yes | Station-specific rollback policy/runbook; implementation that restores Legacy routing and persists/audits `ActivationRolledBack`; explicit treatment of writes made during any target-authoritative interval; tested decision ownership. | The evaluator and state value exist, but no production rollback/authority adapter or complete data/authority procedure exists. Restoring an old file alone is insufficient. | B + D → C | DB-03; BR-02–BR-06; AUTH-03; MIG-06; rollback/data owners. | 6 |
| DB-03 | Restore is proven, authorized, integrity-checked, and avoids unsafe overwrite. | BLOCKED | Yes | ManagementCredential-authorized, staged/crash-safe restore execution or controlled implementation; exact artifact/destination binding; isolated restoration rehearsal; integrity/foreign-key/migration checks before and after replacement. | `RestoreValidationService` validates but does not restore. The current Import path directly overwrites `Data/db.sys` and bypasses the newer validation. | B → C/D | SEC-02; BR-02, BR-03, BR-05, BR-06; approved restore policy. | 2 |
| SEC-01 | Normal target authentication uses active ShiftProfile only, with no separate user/role identity. | BLOCKED | Yes | Production ShiftProfile authentication composition and UI; station-scoped provisioning; credential change/disable behavior; session identity propagation; recovery behavior; focused and manual qualification. | Domain/persistence tests exist, but production UI/composition, provisioning, recovery, routing, and activation are absent. | B + D → C | Security policy decisions; MIG-03 and MIG-04; SEC-03 and SEC-05. | 3 |
| SEC-02 | Protected actions require action/scope/correlation-bound singleton ManagementCredential proof. | BLOCKED | Yes | Production composition for every `ProtectedAction`; action/scope/correlation/version/expiry binding; removal of legacy login-password confirmation from the target-authority authorization route; focused negative and atomicity tests. | Current Backup/Import/Repair/Factory Reset settings paths use `ConfirmLoginPassword` and legacy login-password verification. Target proof foundations are not production composed. | B + D → C | SEC-01; SEC-03; SEC-05; protected-action inventory; DB-03 and BR-03. | 3 |
| SEC-03 | Management recovery is documented, auditable, bounded, and creates no alternate identity or universal secret. | BLOCKED | Yes | Approved recovery design; recovery authorization and audit; bounded recovery implementation; rehearsal; proof that recovery neither creates a principal nor exposes a universal secret. | No target ManagementCredential recovery exists. The reachable legacy deterministic, application-secret-derived recovery model is incompatible with target authority. | B + D → C | Approved recovery policy; SEC-01, SEC-02, SEC-05, SEC-08; MIG-03. | 3 |
| SEC-04 | Post-wizard ESD changes use ShiftProfile, ManagementCredential proof, approved vendor P-256 signature, exact binding, and replay protection. | BLOCKED | Yes | Production protected-executor composition; approved device/public-key/management provisioning; routing behind disabled activation boundary; success and fail-closed rehearsal with durable receipt. | Cryptographic and exactly-once foundations are extensively tested, but production provisioning/composition and authority routing are absent. | B → C/D | SEC-01, SEC-02, SEC-05; MIG-03, MIG-04; reconciled production ESD value; separate authority approval. | 3 |
| SEC-05 | Security and activation audit trails are durable, append-only, non-secret, and complete. | BLOCKED | Yes | End-to-end production audit wiring for authentication, protected actions, approvals, transition, failures, and rollback; retention procedure; atomic proof that failed actions cannot mutate without the required receipt. | Target security persistence and activation audit contracts exist, but production activation/protected-action emission and retention wiring do not. | B + D → C | SEC-01–SEC-04; AUTH-03/AUTH-04; MIG-02/MIG-06; retention owner. | 3 |
| SEC-08 | No hidden backdoor, master password, private signing key, universal code, or bypass is reachable in target authority. | BLOCKED | Yes | Removal or hard isolation of legacy recovery/bypass reachability under target routing; independent final-binary security review; approved recovery runbook; negative tests. | The target contracts contain no forbidden secret, but the existing legacy recovery path contains an embedded secret and deterministic code; no decommission/transition proof exists. | B + D → C | SEC-03; MIG-03; AUTH-03; independent security reviewer. | 3 |
| BR-02 | Backup integrity is cryptographically and structurally verified before acceptance. | BLOCKED | Yes | Production-wired verified receipt or approved controlled procedure binding the retained artifact/ciphertext identity to a verified SQLite-consistent copy; hash, full integrity, foreign-key, source-stability, custody, and retention evidence. | The explicit backup service provides the checks locally but is not production wired. Legacy encrypted export emits no integrity receipt and has no authentication tag. | B/D → C | DB-01, DB-02; SEC-02; BR-03; storage/custody policy. | 2 |
| BR-03 | Restore requires explicit ManagementCredential authorization bound to exact backup and destination. | BLOCKED | Yes | Restore `ProtectedAction` integration; backup/destination identity, initiating ShiftProfile, action/scope/correlation/version/expiry binding; durable allow/deny audit. | Target policy classifies Restore as protected, but current Import uses only legacy login password and confirmation. | B + D → C | SEC-01, SEC-02, SEC-05; DB-03; BR-02. | 2 |
| BR-05 | Verified rollback copy exists before live replacement with recorded identity/location. | BLOCKED | Yes | Implementation or enforceable procedure that creates an immutable verified rollback copy outside the live path before replacement; identity/hash/location/custodian/owner receipt; recovery rehearsal. | `DatabaseMaintenanceService.ImportDatabase` declares `safetyBackupPath` but never creates or uses the copy before overwrite. | B + D → C | BR-02, BR-03, BR-06; DB-01; rollback owner and retention location. | 2 |
| BR-06 | Restore/replacement failure is crash-safe and leaves no ambiguous live database. | BLOCKED | Yes | Staged replace/rename design; WAL/journal/sidecar handling; pre-swap and post-swap validation; failure cleanup and deterministic recovery; fault-injection tests; manual rollback steps. | Current Import decrypts and performs direct overwrite with no atomic staging, post-copy integrity check, or interrupted-copy recovery. | B + D → C | DB-03; BR-02, BR-03, BR-05; quiescence policy; authority/rollback policy. | 2 |
| MIG-02 | Production migration has an approved executable boundary that validates exact context and fails safely. | BLOCKED | Yes | Production `IProductionMigrationExecutor`; exact approved-context validation; transaction/failure/cancellation behavior; immutable receipts; post-validation; abort/rollback behavior; end-to-end tests. | Only the fail-closed validator and a test double exist. No production executor exists. | B → C/D | DB/restore foundation; SEC-02 and SEC-05; MIG-03/MIG-04; final migration chain. | 5 |
| MIG-03 | Target production composition/routing exists but remains disabled until explicit activation. | BLOCKED | Yes | Complete target read/write/security/UI composition behind an explicit inactive feature boundary; proof normal startup and Legacy routing remain unchanged before authorization. | The inactive snapshot exists, but current production composition lacks target routing, security composition, and UI adoption. | B + D → C | SEC-01–SEC-05 and SEC-08; approved route inventory; AUTH-03. | 4 |
| MIG-04 | Production mapping/provisioning covers ShiftProfiles, credentials, ManagementCredential, device/key, Events/baselines, snapshots/locks, and ESD. | BLOCKED | Yes | Approved repeatable station-specific mapping/provisioning implementation and manifest; validation for every entity; reconciliation and no-RBAC/no-Support checks; exact production mappings later. | The unified chain creates non-destructive target schema but does not provide complete Rasht/Ramsar production adoption/provisioning for all target authorities. | B → C/D | SEC-01–SEC-04; RT-01; MIG-03; data/security owners; exact station source inventory. | 4 |
| MIG-06 | Authority and rollback transitions are coupled to validation and never inferred from migration completion. | BLOCKED | Yes | Explicit approved decision points; durable authority state; post-validation acceptance; rollback transition; audit; tests proving migration completion alone cannot activate. | State-machine planning exists, but no production authority adapter/executor couples the required controls. Rehearsal deliberately remains Legacy-authoritative. | B + D → C | AUTH-03/AUTH-04; MIG-02–MIG-04; SEC-05; DB-05; final decision authority. | 6 |

Recommended-order numbers refer to the closure waves in Section 10, not to authority to implement them under Phase 9.5B1.

## 5. Exact CONDITIONAL gate inventory

All gates in this table are mandatory before cutover. None is a confirmed software defect merely because production or manual evidence is missing.

| Gate ID | Requirement | Current state | Mandatory | Exact missing evidence or capability | Why unresolved | Closure route | Dependencies | Recommended order |
|---|---|---|---|---|---|---|---|---|
| DB-01 | Exact production database identity and canonical path are known and unambiguous. | CONDITIONAL | Yes | Quiesced canonical full path; station identity; file size/time; SQLite header; journal/WAL state; hash or approved logical fingerprint; later receipt binding. | Phase 9.5A intentionally inspected no production file. Executable-local `Data/db.sys` convention is not installation evidence. | C | Final evidence protocol; named installation/operator; quiescence; supported station scope. | 8 |
| DB-02 | Current SQLite-consistent verified backup is bound to DB-01. | CONDITIONAL | Yes | Fresh approved backup; source-stability receipt; hash/size; integrity/foreign-key results; schema/migration classification; location/custodian/retention; DB-01 binding. | Backup implementation is tested, but no real production backup was created in Phase 9.5A. | C + D | DB-01; BR-02; approved custody and backup operator. | 8 |
| DB-04 | Pre-cutover database integrity and foreign-key integrity pass read-only checks. | CONDITIONAL | Yes | Full `integrity_check`, `foreign_key_check`, header/read-only enforcement, categorical results on the exact quiesced DB-01 identity. | Analyzer and tests exist; the production database was not inspected. | C | DB-01; quiescence; approved read-only command/operator sequence. | 8 |
| DB-05 | Post-migration/post-cutover integrity, ledger, identity, counts, and fingerprints pass before target authority acceptance. | CONDITIONAL | Yes | Exact command/operator sequence; approved tolerances; post-migration integrity/FK/ledger/station/count/fingerprint results; failure routing and rollback trigger. | No cutover occurred, so the final post-migration observation cannot exist. Phase 9.5B may prepare and rehearse the procedure but cannot honestly close the observation early. | A/D for procedure, then C at the future cutover hold point | MIG-02–MIG-06; DB-01/DB-02/DB-04; rollback readiness; authority acceptance withheld. | 9 |
| DB-09 | Production station identity remains Rasht or Ramsar with correct unit scope. | CONDITIONAL | Yes | Exact production station ID/name/type; expected Rasht 3-unit or Ramsar 4-unit count; per-unit mapping against the selected backup. | Qualification fixtures proved both supported shapes, but no production identity was captured. | C + D | DB-01/DB-02; station owner; MIG-04. | 8 |
| RT-01 | Every production unit has a trusted Runtime Baseline with complete identity/version/state/totals. | CONDITIONAL | Yes | Per-unit authoritative baseline, responsibility-boundary minute, station/unit, initial state, cumulative total, RuntimeAfterOH, version, Legacy reconciliation, and approval. | Validation rejects invalid baselines, but production baseline provisioning and evidence are neither composed nor captured. | B through MIG-04, then C + D | DB-09; MIG-04; data owner; production Legacy records; ESD authority/value. | 8 |
| RT-08 | Runtime/Event target results match production source data. | CONDITIONAL | Yes | Read-only production shadow reconciliation for every in-scope unit/period; chain versions/fingerprints; invariant results; disposition of every difference. | Automated fixtures and both manual qualification stations matched, but those were disposable datasets. | C + D | DB-01/DB-02/DB-09; RT-01; MIG-04; final binary; data owner. | 8 |
| REP-01 | Target projection preserves Legacy min/max/average and daily-unique sums. | CONDITIONAL | Yes | Production reconciliation for representative and boundary periods, including data-start boundary, incomplete/open periods, finalized months, and all required aggregations. | Calculator/tests and both fixture Pilots matched, but evidence is qualification-only. | C + D | DB-01/DB-02; MIG-04; final target route; data owner. | 8 |
| REP-05 | Source/target report evidence is complete and within approved tolerance. | CONDITIONAL | Yes | Exact per-metric production inputs/results; versions; evidence references; zero tolerance by default or named owner approval of a metric-specific non-zero tolerance; all differences resolved. | No production reconciliation or approved non-zero tolerance exists. Aggregate-only agreement is insufficient. | C + D | REP-01; DB-01/DB-02; data owner; approved tolerance policy. | 8 |
| BR-04 | Selected production backup can be restored and opened in isolation. | CONDITIONAL | Yes | Exact backup restored to an isolated destination; final binary start; checksum/integrity/FK/station/authentication/Runtime/Event/report/snapshot checks; elapsed time; failure recovery evidence. | Existing validation and rehearsal use generated fixtures, not the selected production backup. | C + D | DB-01/DB-02; DB-03; BR-02/BR-03/BR-05/BR-06; final binary. | 8 |
| MIG-05 | Exact production migration classification and rehearsal are current and clean. | CONDITIONAL | Yes | DB-01 classification; explicit adoption decision if needed; two-pass migration of the exact backup; final version; idempotency; preservation receipts; unchanged original backup. | Classifier/rehearsal are tested, but the production database was not inspected or rehearsed. | C + D | DB-01/DB-02/DB-04/BR-04; MIG-02–MIG-04; data/security owners. | 8 |
| UI-02 | Stop after successful active observation is manually qualified. | CONDITIONAL | Yes | Both station scenarios: execute Stop after successful observation; record stopped status/reason, retained safe evidence, safe close/return, zero writes, and unchanged authority. | Stop became enabled and automated lifecycle coverage exists, but the action was not manually executed. | D | Final isolated build; Rasht 3-unit and Ramsar 4-unit fixtures; traceable evidence protocol. | 7 |
| UI-03 | Active-session cancellation is manually qualified. | CONDITIONAL | Yes | Both stations: cancel during active observation; prove responsiveness, no false review/completion, no unhandled exception, no mutation, and usable Legacy return. | Focused automated cancellation tests exist, but no human in-progress cancellation was observed. | D | Final isolated build; controllable active observation; database before/after evidence. | 7 |
| UI-04 | Application shutdown during active Pilot is manually qualified. | CONDITIONAL | Yes | Both stations: normal shutdown/close during active work; prove cancellation/disposal, process exit, database safety, unchanged authority, and safe restart/Legacy availability. | Automated shutdown/disposal coverage exists, but process/UI/database interaction was not manually observed. | D | Final isolated build; database before/after evidence; restart protocol. | 7 |
| UI-05 | Independent 100%, 125%, and 150% DPI visual qualification is complete. | CONDITIONAL | Yes | At every scale and both stations: RTL readability; focus order; grid; identity/status/monitoring/rollback fields; dialogs; Stop/Complete/Return; no clipping/overlap; sanitized visual evidence. | DPI-aware implementation/source checks exist, but no independent complete lifecycle at all three scales was recorded. | D | Final isolated build; Windows DPI environments; UI-02–UI-04 scenarios; evidence capture protocol. | 7 |
| UI-06 | Confirmation cancel, keyboard/RTL, monitoring/rollback fields, DB before/after evidence, and traceable sanitized run log are complete. | CONDITIONAL | Yes | Close or strictly supersede Phase 9.4A rows P9.4A-05, -08, -09, -20, -25, -29, and -35; retain row-level safe evidence for both station scenarios. | The Phase 9.4 record has 16 `NOT EXECUTABLE / NOT MANUALLY VERIFIED` rows, including the named compound elements. Automated coverage cannot turn them into manual PASS. | D | Final isolated build; approved acceptance checklist; UI-02–UI-05; evidence custodian. | 7 |
| OPS-01 | Named operator, approvers/owners, window, monitoring plan, and local support contact are recorded. | CONDITIONAL | Yes | Current operator/ShiftProfile, ManagementCredential authorization, management approver, data owner, security reviewer, rollback owner, maintenance-window and monitoring owners, local contact, scope, timestamps, expiry, escalation, and exact evidence bindings. | No real production approvals were captured by design. Identities and approvals are human, time-sensitive, installation-specific evidence. | C + D | Final binaries/protocol; DB-01/DB-02; runbooks; reachable decision owners; approved window. | 8 |

## 6. Blocker classification

The groups below are not mutually exclusive: a gate can contain both a software capability gap and a required human policy decision. The primary purpose is to prevent a production-evidence gap from being mislabeled as a defect.

### A. REAL SOFTWARE DEFECT

Confirmed unsafe behavior in an existing relevant operational path:

- **DB-03 / BR-05 / BR-06:** the current Import implementation directly overwrites the live database, does not invoke the newer restore validation, declares but does not create/use its `safetyBackupPath`, has no staged atomic replacement, performs no post-copy integrity check, and has no tested interrupted-copy recovery. This path is not cutover-safe.

Confirmed absent software capabilities that block the proposed target-authority scope, but are not claimed as regressions in the current Legacy-authoritative scope:

- **AUTH-03, AUTH-04, MIG-06:** production authority and rollback adapters/executors are absent.
- **SEC-01–SEC-05 and SEC-08:** target authentication, protected-action, recovery, vendor-authorization composition, audit wiring, and bypass decommission proof are absent/incomplete.
- **BR-02 and BR-03:** verified receipt integration and ManagementCredential-bound restore authorization are absent.
- **MIG-02–MIG-04:** production migration executor, disabled target composition, and complete provisioning/mapping are absent.

These are confirmed capability blockers because Phase 9.5A identified the missing production boundaries directly. Calling them confirmed does not authorize implementation in Phase 9.5B1 and does not imply that current Legacy operation must be changed before a separately approved implementation plan exists.

### B. MISSING AUTOMATED EVIDENCE

No unresolved gate is blocked solely because an existing isolated behavior lacks one more unit test. Phase 9.5A reports substantial focused automated coverage for the foundations. New focused and end-to-end tests are nevertheless mandatory evidence for every newly implemented boundary in A:

- authority transitions cannot be inferred from migration completion;
- backup/restore/replacement failure must be fault-tested;
- all protected actions must test exact proof binding, denial, expiry, revision, replay, audit, and atomic rollback;
- disabled target routing must prove normal startup remains Legacy-authoritative;
- migration/provisioning must test station isolation, preservation, idempotency, and failure recovery;
- the final target binary must prove forbidden identities and bypasses are unreachable.

Tests may close the automated-evidence portion of a gate, but cannot substitute for production identities, human approvals, or the manual/UI observations listed below.

### C. MISSING MANUAL QUALIFICATION

- **UI-02:** Stop after successful active observation.
- **UI-03:** active-session cancellation.
- **UI-04:** shutdown during an active Pilot.
- **UI-05:** separate 100%, 125%, and 150% DPI qualification.
- **UI-06:** confirmation No/cancel, keyboard/RTL, all required identity/monitoring/rollback/status fields, active Return path, database before/after evidence, and a traceable sanitized evidence package.
- Manual qualification portions of **SEC-01–SEC-05, SEC-08, DB-03, BR-02–BR-06, AUTH-03/AUTH-04, MIG-02–MIG-06** after their implementations exist.

The five UI gates can realistically be closed before production deployment using the final isolated build and Rasht/Ramsar qualification fixtures. A failed manual observation may reveal a new software defect, but missing manual evidence by itself is not a defect.

### D. PRODUCTION-ONLY PRE-CUTOVER EVIDENCE

- **DB-01, DB-02, DB-04, DB-09:** exact production identity, backup, integrity, and station/unit evidence.
- **RT-01, RT-08:** approved per-unit production baselines and all-unit Runtime/Event reconciliation.
- **REP-01, REP-05:** representative/boundary production reporting and approved-tolerance reconciliation.
- **BR-04:** restoration and final-binary exercise using the exact selected production backup in isolation.
- **MIG-05:** exact production classification and two-pass rehearsal.
- Production-binding portions of **AUTH-03/AUTH-04, SEC-01–SEC-05, SEC-08, BR-02/BR-03/BR-05/BR-06, MIG-02–MIG-04/MIG-06**.
- **DB-05:** the procedure can be approved and rehearsed pre-cutover, but final post-migration results exist only at a future authorized cutover hold point before target authority acceptance.

None of these items can be honestly closed with generated qualification data. Their absence is expected before real production-bound verification and must not be described as a software defect.

### E. POLICY / AUTHORIZATION / OPERATOR REQUIREMENT

- **AUTH-03/AUTH-04/MIG-06:** authority decision point, rollback trigger, data boundary, owners, two-person/management approval if adopted, and terminal authority state.
- **SEC-02/SEC-03/SEC-04/SEC-05/SEC-08:** protected-action inventory, management recovery authorization, vendor key/device custody, audit retention, independent security review, and forbidden-bypass policy.
- **DB-03/BR-02–BR-06:** backup custody/retention, restore authorization, quiescence, sidecar handling, rollback-copy custody, replacement procedure, and recovery owner.
- **MIG-04:** data/security-owner approval for station-specific mapping and provisioning.
- **REP-05:** named data-owner approval for any non-zero tolerance; otherwise tolerance remains exact equality.
- **OPS-01:** current named operators, approvers, owners, contacts, monitoring thresholds, window, expiry, and escalation.

Policy text alone does not close a gate whose required executor is absent. Conversely, a passing executor test does not manufacture the required human approval.

### F. NON-BLOCKING FOLLOW-UP

- The known NU1701 compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms assets remain visible release risk/technical debt. Phase 9.5A did not make them an unresolved gate; VAL-01 was READY with 0 errors, 12 existing warnings, and 652 passing tests. They require explicit visibility/risk acceptance in the final validation package but are not one of the 34 extracted unresolved gates.
- Broader universal-platform work, RBAC, a Support identity, cloud services, and unrelated dependency modernization are outside the Rasht/Ramsar cutover scope and must not be pulled into blocker closure.

There is no basis in Phase 9.5A for demoting any of the 34 unresolved mandatory gates, or any of the four named Phase 9.4 residual limitations, to this non-blocking group.

## 7. Phase 9.4 residual limitation disposition

| Phase 9.4 residual limitation | Phase 9.5A gate | Disposition | Defect status | Required closure |
|---|---|---|---|---|
| Stop Pilot after successful active observation | UI-02 | **Mandatory blocker to GO; PRE-CUTOVER REQUIRED.** | Missing manual qualification, not a confirmed defect. | Human execution for Rasht and Ramsar on the final isolated/pre-cutover build with stopped status/reason, retained safe evidence, non-mutation, authority, close, and return checks. |
| Active-session cancellation | UI-03 | **Mandatory blocker to GO; PRE-CUTOVER REQUIRED.** | Missing manual qualification, not a confirmed defect. | Human cancellation during active work for both stations with responsiveness, terminal-state, exception, mutation, and Legacy-availability evidence. |
| Application shutdown during active Pilot | UI-04 | **Mandatory blocker to GO; PRE-CUTOVER REQUIRED.** | Missing manual qualification, not a confirmed defect. | Human normal shutdown/close during active work for both stations with disposal, process exit, database, authority, restart, and Legacy evidence. |
| Separate 100%, 125%, and 150% DPI visual qualification | UI-05 | **Mandatory blocker to GO; PRE-CUTOVER REQUIRED.** | Missing manual qualification, not a confirmed defect. | Human visual/interaction qualification at all three scales for both stations, including RTL/focus/status/actions/dialogs and sanitized evidence. |

All four are pre-cutover required and prevent GO while conditional. None is a non-blocking follow-up. “Mandatory blocker” here means a mandatory unresolved gate blocking GO; it does not change the gate’s Phase 9.5A state from `CONDITIONAL` to `BLOCKED` and does not assert a software failure before the manual test is run.

The other Phase 9.4 residuals remain grouped under UI-06 and must also be closed: confirmation No/cancel; independent keyboard/RTL; complete identity, monitoring, rollback, stop-reason, and completion fields; active-session Return/confirmation; per-lifecycle database before/after evidence; and complete sanitized checklist-traceable run logs/screenshots.

## 8. Local versus production-only closure

### Can realistically be completed before any real production deployment

Subject to separate authorization for production-code work, the following can be designed, implemented, and qualified without touching production data or changing production authority:

1. Authority-state and rollback policy, transition adapter, executor contracts/implementation, audit, and fail-closed tests for AUTH-03, AUTH-04, and MIG-06.
2. Crash-safe backup/restore/rollback implementation, protected authorization, artifact receipts, isolated failure injection, and runbook rehearsal for DB-03 and BR-02/BR-03/BR-05/BR-06.
3. Target ShiftProfile, ManagementCredential, recovery, vendor ESD, audit, and forbidden-bypass production composition for SEC-01–SEC-05 and SEC-08.
4. Disabled target composition, station-specific mapping/provisioning mechanism, migration executor, receipts, post-validation hooks, and synthetic/qualification-fixture tests for MIG-02–MIG-04.
5. Complete manual Rasht/Ramsar Pilot qualification for UI-02–UI-06 on the final isolated build.
6. The exact evidence schemas, operator commands, stop conditions, runbooks, and acceptance templates needed later for DB-01/DB-02/DB-04/DB-05/DB-09, RT-01/RT-08, REP-01/REP-05, BR-04, MIG-05, and OPS-01.

These activities can remove the `BLOCKED` reasons and close local/manual gates. They cannot make production identity/reconciliation gates READY until real production-bound evidence is captured.

### Cannot honestly be completed before real production-bound verification

The final evidence for DB-01, DB-02, DB-04, DB-09, RT-01, RT-08, REP-01, REP-05, BR-04, MIG-05, and OPS-01 requires the exact installation, database/backup, station, final binaries, current owners/approvals, and window. Production access should remain read-only or operate only on the SQLite-consistent backup and isolated restored/rehearsal copy until a separately authorized cutover action.

DB-05 is later still: its procedure and dry-run evidence can be ready before cutover, but its final post-migration checks must be performed at the cutover hold point while Legacy remains authoritative and before target authority can be accepted. Failure must force rollback/NO-GO.

## 9. Dependency and order map

```text
9.5B2 policy and safety decisions
  ├──> 9.5B3 safe backup / restore / rollback-copy implementation
  └──> 9.5B4 target security composition and recovery
             └──────────────┐
9.5B3 ──────────────────────┼──> 9.5B5 disabled target composition and provisioning
                            │          └──> 9.5B6 migration executor and validation receipts
                            └──────────────────┘
9.5B3 + 9.5B4 + 9.5B5 + 9.5B6
  └──> 9.5B7 explicit authority / rollback transition boundary
          └──> 9.5B8 final isolated integrated and manual qualification
                  └──> future PRE-CUTOVER VERIFICATION on exact production identity
                          └──> future cutover-window DB-05 hold point
                                  └──> possible authority decision only under separate authorization
```

Key dependency rules:

- Migration success never activates target authority.
- Target composition must be complete but disabled before migration/activation qualification.
- Restore and rollback must be executable before any migration or authority exercise is considered ready.
- Security identities, proofs, recovery, audit, and provisioning must exist before target routing or protected operational flows are qualified.
- The final isolated qualification must use the same candidate code/binaries intended for production-bound verification; later changes invalidate it and require proportionate rerun.
- Production-only evidence capture begins only after all implementation and isolated manual blockers are closed.
- DB-05 remains an explicit hold point; target authority cannot be accepted before its final results pass.

## 10. Proposed small closure tasks

No task below is authorized by this Phase 9.5B1 document. Each requires its own scope/approval. “Production code may change” describes what that future task would need, not a change made now.

### Phase 9.5B2 — Authority, recovery, restore, and rollback decision contracts

**Narrow scope:** Approve and freeze the operational decisions needed before implementation: authority states and decision owners; target-to-Legacy routing; target-authoritative write boundary; rollback triggers and maximum decision time; backup/restore custody; quiescence and SQLite sidecar handling; protected-action inventory; ManagementCredential recovery policy; vendor public-key/device custody; audit retention; station-specific provisioning ownership. Produce numbered runbook specifications and testable acceptance criteria only.

**Primary gates advanced:** AUTH-03, AUTH-04, DB-03, SEC-02–SEC-05, SEC-08, BR-02–BR-06, MIG-04, MIG-06, OPS-01 template.

**Expected evidence:** Approved decision record; threat/safety review; action and route inventory; state/rollback diagrams; exact owner matrix; no-secret evidence rules; acceptance-test matrix; explicit statement that Legacy remains authoritative.

**Production code may change:** **No.** Documentation/specification only.

**Human manual testing required:** **No application testing**, but **yes** for management, data-owner, security, rollback-owner, and operator review/approval.

**Expected risk:** **Medium** procedural risk; no runtime/data risk. Incorrect decisions would propagate into critical implementation, so unresolved decisions stop the sequence.

### Phase 9.5B3 — Crash-safe verified backup, restore, and rollback-copy boundary

**Narrow scope:** Implement one ManagementCredential-bound, receipt-producing path for SQLite-consistent backup acceptance and staged restore/replacement. Create and verify an immutable rollback copy before replacement; handle WAL/journal sidecars; validate before/after swap; recover deterministically from injected failures. Do not migrate production data or change authority.

**Primary gates targeted for closure:** DB-03, BR-02, BR-03, BR-05, BR-06. Supports AUTH-04 and future BR-04.

**Expected evidence:** Focused authorization/binding tests; checksum/integrity/FK tests; WAL-aware backup receipt; destination/same-path rejection; staged-swap and interruption fault injection; rollback-copy identity; post-restore checks; isolated disposable rehearsal; code review and diff record.

**Production code may change:** **Yes**, only backup/restore/protected-action composition and directly supporting code. No schema change.

**Human manual testing required:** **Yes**, isolated operator rehearsal of backup, denied restore, allowed restore, failure recovery, and rollback-copy recovery. No production database.

**Expected risk:** **High** because file replacement and authorization are destructive-capability boundaries; testing must use disposable isolated copies.

### Phase 9.5B4 — Target security composition, recovery, ESD authorization, and audit

**Narrow scope:** Compose ShiftProfile-only target login/session behavior, singleton ManagementCredential proofs for the complete protected-action inventory, bounded target management recovery, vendor-signed ESD execution, durable audit, and explicit removal/isolation of legacy recovery bypass from target authority. Keep all target routes disabled in normal production startup.

**Primary gates targeted for closure:** SEC-01–SEC-05 and SEC-08. Also supplies security prerequisites for BR-03, MIG-02–MIG-04, AUTH-03/AUTH-04.

**Expected evidence:** Positive/negative authentication and station-scope tests; credential change/disable/session tests; action/scope/correlation/version/expiry proof tests; recovery rehearsal; ECDSA key/device/value/time/replay/exactly-once tests; append-only non-secret audit and atomic rollback tests; binary/composition review showing no RBAC, Support identity, customer private key, master secret, or reachable target bypass.

**Production code may change:** **Yes**, only target security/composition and directly supporting UI/audit paths behind the inactive boundary. No authority activation.

**Human manual testing required:** **Yes**, isolated target authentication, disable/session, protected-action denial/allow, recovery, ESD failure/success, and audit visibility workflows.

**Expected risk:** **High/Critical** due to authentication, recovery, protected actions, and ESD authority. Failure leaves these gates BLOCKED.

### Phase 9.5B5 — Disabled target composition and repeatable station provisioning

**Narrow scope:** Compose target read/write/security/report/runtime routes behind an explicit inactive activation boundary, with normal startup still Legacy-authoritative. Implement the repeatable Rasht/Ramsar mapping/provisioning manifest for ShiftProfiles, credentials, ManagementCredential, device/public key, trusted baselines, Events, ESD value, finalized snapshots, and locks. Use only synthetic/qualification data.

**Primary gates targeted for closure:** MIG-03 and the local capability portion of MIG-04. Supports RT-01, RT-08, REP-01, REP-05, SEC-01–SEC-04, and AUTH-03.

**Expected evidence:** Composition/route inventory; startup-inactive tests; station isolation and exact unit-scope tests; idempotent provisioning; complete manifest validation; preservation/no-RBAC/no-Support checks; baseline/snapshot/lock/ESD validation; disposable Rasht/Ramsar rehearsal.

**Production code may change:** **Yes**, only disabled target composition and provisioning/mapping capability. No default route switch and no authority change.

**Human manual testing required:** **Limited yes**, isolated inspection that Legacy startup is unchanged and disabled target preparation is not operator-reachable as authority. Full UI qualification occurs in 9.5B8.

**Expected risk:** **High** because hidden routing or station-scope leakage would be a stop condition.

### Phase 9.5B6 — Production migration executor and validation receipts

**Narrow scope:** Implement the production migration executor around the already-tested migration chain. Enforce exact approved context, verified backup prerequisite, cancellation/transaction semantics, immutable receipts, preservation, post-validation, idempotent rerun, and abort/rollback behavior. Migration completion must leave Legacy authoritative and target routing disabled.

**Primary gate targeted for closure:** MIG-02. Supports MIG-05 and AUTH-03/AUTH-04/MIG-06.

**Expected evidence:** End-to-end disposable-copy tests; hostile approval/context cases; transaction rollback and cancellation; capacity/lock handling; two-pass idempotency; original backup unchanged; finalized snapshot/lock/legacy evidence and ESD preservation; post-validation receipt; proof that executor cannot activate authority.

**Production code may change:** **Yes**, only the migration execution boundary and directly supporting validation/receipt wiring. No production execution and no schema redesign beyond the already-approved chain.

**Human manual testing required:** **Yes**, isolated operator rehearsal including stop/failure handling and receipt review.

**Expected risk:** **Critical** due to migration/destructive capability, even though qualification must remain isolated.

### Phase 9.5B7 — Explicit authority acceptance and rollback transition boundary

**Narrow scope:** Implement the explicit installation-bound authority transition and rollback adapter using the approved 9.5B2 decisions. Enforce preconditions, post-migration validation, durable audit/state, two-person/management decision boundary if approved, target routing acceptance, rollback triggers, Legacy restoration, and target-interval data handling. Normal startup remains unchanged unless an exact persisted authorized state says otherwise. Do not perform real cutover.

**Primary gates targeted for closure:** AUTH-03, AUTH-04, MIG-06 and activation-audit portions of SEC-05.

**Expected evidence:** Complete state-transition matrix; invalid/stale/mismatched approval tests; migration-does-not-activate tests; atomic state/audit tests; restart behavior; failed validation/ambiguous-state fail-closed tests; simulated target-write rollback treatment; disposable end-to-end rehearsal returning to Legacy; approved runbook.

**Production code may change:** **Yes**, narrowly within authority/activation/rollback boundaries. The production feature must remain inactive; no actual authority transition is authorized.

**Human manual testing required:** **Yes**, two-person/operator runbook rehearsal in a disposable isolated environment, including abort and rollback.

**Expected risk:** **Critical**, the highest-risk implementation task. Independent review is required before progression.

### Phase 9.5B8 — Final isolated integrated and manual qualification

**Narrow scope:** Freeze a candidate Release build, rerun the complete automated suite and integrated disposable Rasht/Ramsar rehearsals, and close UI-02–UI-06. Exercise Stop, active cancellation, active shutdown/restart, confirmation No, active Return, keyboard/RTL, monitoring/rollback/status fields, complete database before/after non-mutation, and separate 100%/125%/150% DPI lifecycles. Include security, restore, migration, authority-abort, and rollback rehearsals without production data or authority change.

**Primary gates targeted for closure:** UI-02–UI-06 and the local automated/manual evidence portions of every formerly BLOCKED gate.

**Expected evidence:** Exact commit/binary hashes; Release build errors/warnings; full test totals; focused test results; complete Phase 9.4A-equivalent row record for both stations; sanitized screenshots/logs; database hashes/fingerprints before/after; no-mutation/Legacy-authority receipts; all implementation-gate closure decisions; `git diff --check`.

**Production code may change:** **No** within the qualification task. Any discovered defect must create a separate narrowly scoped correction batch, followed by proportionate rerun and a new candidate build identity.

**Human manual testing required:** **Yes**, comprehensive but isolated.

**Expected risk:** **Medium operational risk / no production-data risk**. A failure blocks pre-cutover verification; it is not waived.

### Future Phase 9.5B9 — Production-bound pre-cutover verification

**Narrow scope:** After the entry criteria in Section 11 are met and separate access/verification authority exists, identify and quiesce exactly one supported station installation; capture DB-01/DB-02/DB-04/DB-09; restore the exact backup in isolation; execute classification, two-pass rehearsal, provisioning, preservation, Runtime/Event, report, snapshot/lock, security, and final-binary checks; bind current operational approvals. Do not accept target authority and do not modify the live production database.

**Primary gates targeted for closure:** DB-01, DB-02, DB-04, DB-09, RT-01, RT-08, REP-01, REP-05, BR-04, MIG-05, OPS-01, plus final production bindings for the implemented gates. DB-05 procedure readiness is established, but its final observation remains a future cutover-window hold point.

**Expected evidence:** The Phase 9.5A evidence package items bound to one correlation ID, station, canonical database, verified backup, immutable rollback artifact, final binaries, owners, approvals, window, and complete reconciliation results; an updated gate table with no hidden/waived gaps.

**Production code may change:** **No.** Evidence capture and rehearsal only. Any defect creates a separate correction phase and invalidates affected candidate evidence.

**Human manual testing required:** **Yes**, controlled operator, data-owner, security, rollback-owner, and approver participation. Database work is read-only against live production and write-capable only against the approved isolated restored copy.

**Expected risk:** **High** due to production identity/access and operational coordination, even though cutover and live mutation remain forbidden.

No Phase 9.5B10 cutover task is proposed here. A cutover-window plan, including the DB-05 hold point, may be defined only after 9.5B9 succeeds and requires separate explicit authorization.

## 11. Earliest honest entry to PRE-CUTOVER VERIFICATION

A future PRE-CUTOVER VERIFICATION can honestly begin only after Phase 9.5B2 through Phase 9.5B8 are complete and all of the following are true:

1. Every Phase 9.5A `BLOCKED` gate has an implemented, reviewed, locally tested, and where applicable manually rehearsed capability; none remains blocked by absent executor, composition, recovery, audit, restore, rollback, provisioning, or authority behavior.
2. UI-02 through UI-06 are READY on the frozen candidate build for both Rasht and Ramsar, including all four named Phase 9.4 limitations and the remaining compound checklist evidence.
3. Safe backup/restore/rollback is executable in isolation, ManagementCredential-bound, receipt-producing, crash-safe, and recoverable under fault injection.
4. Target security and routing are complete but disabled; Legacy remains the default and sole authority; no automatic startup migration or activation exists.
5. Migration and provisioning are deterministic, fail-closed, idempotent, preservation-checked, and cannot change authority.
6. Authority acceptance and rollback are explicit, installation-bound, audited, restart-safe, and rehearsed without real authority change.
7. Runbooks, stop conditions, evidence schemas, zero/default tolerances, owner roles, and production read-only/backup-copy boundaries are approved.
8. The candidate source and binaries are frozen and identified; Release build, full automated suite, focused tests, manual qualification, and diff hygiene pass.
9. No unresolved local failure is deferred into production verification merely to obtain more evidence.
10. Separate permission exists to access the named production installation for read-only identity/preflight and to create/use an isolated SQLite-consistent backup. That permission is verification authority only, not cutover authority.

At this point, remaining unresolved gates should be only the explicitly production-bound/time-sensitive gates in Section 8 and the DB-05 cutover hold-point observation. The readiness label may move from **BLOCKED** to **PRE-CUTOVER VERIFICATION**; it must not move to GO, CUTOVER AUTHORIZED, or TARGET AUTHORITATIVE.

## 12. Criteria for leaving PRE-CUTOVER VERIFICATION

Pre-cutover verification can produce a candidate GO recommendation only when:

- DB-01, DB-02, DB-04, DB-09, RT-01, RT-08, REP-01, REP-05, BR-04, MIG-05, and OPS-01 are READY with exact, current, internally consistent production-bound evidence;
- every formerly BLOCKED gate retains READY implementation/manual/production bindings on the exact same candidate artifacts;
- every stop condition can be determined false;
- evidence is bound to one supported station, database, backup, binary set, correlation ID, and valid window;
- rollback copy, restore owner, monitoring owner, decision owners, and contacts are available;
- DB-05’s procedure and hold point are approved, and authority acceptance remains technically impossible until its future post-migration checks pass;
- no artifact, binary, database, approval, owner, or window changes after evidence capture without reevaluation.

Even then, the output is only a readiness recommendation. A separate authorized cutover decision and runbook are required. During any future authorized cutover, DB-05 must pass after migration and before target authority acceptance; failure produces NO-GO/rollback, never conditional acceptance.

## 13. Phase 9.5B1 change and validation record

- Production code changed: **NO**.
- Test code changed: **NO**.
- Database schema changed: **NO**.
- Production data accessed or changed: **NO**.
- Production authority changed: **NO**.
- Production cutover or migration performed: **NO**.
- Full repository audit performed: **NO**.
- Build/test suite run: **NO**, as required for this documentation-only task.
- Documentation created: **YES** — this file only.
- Commit or push performed: **NO**.
- Required validation: `git diff --check` — **PASS**; no whitespace errors.

## 14. Final statement

**PHASE 9.5B1 CLOSURE PLAN READY**

The closure plan is ready for review. Phase 9.5A remains NO-GO, Legacy remains authoritative, and no cutover is authorized. Do not begin Phase 9.5B2, production migration, production activation, or authority transition under Phase 9.5B1.
