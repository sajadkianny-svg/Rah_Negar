# RahNegar Master Implementation Roadmap

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Scope:** Complete modernization of the offline Rasht and Ramsar application  
**Status:** Implementation roadmap; documentation only

## 1. Executive overview

RahNegar will move from the verified legacy implementation to the approved foundation, Event, runtime, reporting, persistence, and UI architecture through controlled increments. This is not a rewrite-in-place. Legacy production behavior and evidence remain available until each replacement has passed automated comparison, operational acceptance, migration rehearsal, and an explicit activation decision.

The critical dependency chain is foundation → database infrastructure → Event domain/application authority → runtime projection → reporting projections/snapshots → UI migration → data reconciliation → cutover. Defects identified by the legacy Event and report audits—validation bypasses, destructive Event identity, nondeterministic ordering, incomplete runtime history, invalid OH/ESD behavior, stale finalization, mixed live/snapshot finalized reports, duplicate observations, and silent time coercion—must be addressed by the responsible layer and proven with regression fixtures.

Only Rasht and Ramsar are production scope. Station-specific rules remain isolated. Authentication uses only ShiftProfile for normal work and the independent ManagementCredential for protected operations.

## 2. Implementation principles

1. **No big-bang rewrite.** Introduce domain, persistence, services, projections, and screens behind controlled seams; retain the legacy path until acceptance.
2. **Reversible phases.** Every activation has a switch-back path, known database compatibility, preserved artifacts, and a verified backup point.
3. **Production protection.** Never experiment on the only production database, overwrite legacy evidence, weaken finalized locks, or expose test seeding in production.
4. **Documentation before code.** Specifications, mappings, invariants, acceptance cases, and rollback procedures are approved before their implementation batch.
5. **Tests before migration.** Migration code is written only after target constraints and reconciliation tests exist; production migration follows repeated representative rehearsals.
6. **Preserve legacy evidence.** Original rows, finalized artifacts, audit context, database copies, source versions, and reconciliation results remain attributable and recoverable. Legacy finalized reports are not silently recalculated.
7. **One authority per rule.** UI guides users; application/domain services enforce business rules; repositories enforce persistence contracts; SQLite constraints provide structural defense.
8. **Small reviewable changes.** After every batch build the solution, run applicable tests, exercise the affected workflow, inspect the diff, and record unresolved items.

## 3. Complete phase plan

### Phase 0 — Baseline and safety

**Objective:** Establish a reproducible, protected baseline before behavior or schema changes.

**Dependencies:** Approved architecture/audits; access to representative anonymized Rasht/Ramsar databases and finalized reports.

**Deliverables:** Clean build/warning/dependency record; source/database inventory; golden legacy fixtures; known-defect register; production data custody rules; backup/restore drill; performance/UI/DPI baselines; feature-switch and rollback conventions; traceability matrix from audit findings to tests/phases.

**Production risk:** Low if read-only; highest risks are accidental writes, incomplete fixture coverage, or treating legacy defects as desired behavior.

**Required tests:** Whole-solution build; existing tests; checksum/integrity/foreign-key checks on copies; backup restoration; baseline scenarios for Event chains, Persian boundaries, monthly completeness/finalization, runtime, reports, export, keyboard flow, DPI, and startup.

**Completion criteria:** Baseline artifacts are versioned, backups independently restore, representative datasets are approved, confirmed defects are separated from compatibility requirements, and no production data was modified.

### Phase 1 — Foundation implementation

**Objective:** Implement shared identity, authorization, audit, settings, logging, lifecycle, and transaction abstractions without switching domain behavior.

**Dependencies:** Phase 0; approved system foundation specification.

**Deliverables:** ShiftProfile authentication; ManagementCredential and offline recovery; Wizard/profile maintenance; credential hashing/lockout/versioning; trusted session/action-bound authorization; System/Login audit; redacted logging; typed settings/configuration versions; central clock, identity, connection/transaction contracts; startup coordinator and single-instance policy.

**Production risk:** Medium: authentication lockout, secret leakage, startup regressions, or accidental authorization bypass.

**Required tests:** Credential/KDF/unique-salt tests; PersonnelNo uniqueness; inactive/failed login; reset/recovery/one-time material; action proof binding/expiry; secret-redaction; audit atomicity; startup failure modes; Shift operations and management gates; Rasht/Ramsar isolation.

**Completion criteria:** Foundation APIs are stable; no RBAC remains; normal operations require ShiftProfile; protected operations require ManagementCredential; domain validations cannot be bypassed; recovery and startup acceptance pass.

### Phase 2 — Database infrastructure

**Objective:** Provide safe target persistence and operational database services before domain cutover.

**Dependencies:** Phases 0–1; approved Event schema and foundation database rules.

**Deliverables:** Central SQLite connection factory; foreign-key/journal/busy policy; schema/migration ledger; additive target Event/EventAudit/baseline/report snapshot structures and constraints; repositories/transactions; backup catalogue/package verification; Restore maintenance workflow; import/export staging; integrity and SourceRevision services; deterministic identifiers/indexes/concurrency tokens.

**Production risk:** High: schema incompatibility, locks, WAL mishandling, constraint rejection, or data loss.

**Required tests:** Empty/legacy/current database startup; FK/constraint/index tests; transaction rollback and concurrency; online backup under load; corrupt/wrong-identity Restore rejection; atomic restore recovery; interrupted migration markers; path traversal; migration idempotency on disposable copies.

**Completion criteria:** Target structures can be created additively on copies; legacy tables remain untouched; backup/Restore drills pass; all connections enforce policy; migration is never silent.

### Phase 3 — Event subsystem replacement

**Objective:** Establish the new Event domain and command layer as the sole validated Event mutation path, initially shadowed.

**Dependencies:** Phases 1–2; approved Event architecture, schema, and service-layer contracts.

**Deliverables:** Event/EventAudit domain models; canonical EventType/time semantics; deterministic EventStateMachine; EventValidationService; Add/Edit/Delete handlers; repositories and same-transaction audit; finalized-period and Station policy ports; structured errors; optimistic concurrency; removal/isolation of public persistence bypasses and production test seeding.

**Production risk:** High: changed Event chains, historical identity loss, lock bypass, invalid transitions, or duplicate/same-time ordering.

**Required tests:** Full transition matrix; add/edit/delete chain reconstruction; duplicate timestamps; deterministic tie-breaking; Running+OH rejection; stopped-state ESD rules; optional Event independence from daily observations; Persian midnight/date boundaries; finalized lock; rollback and direct-handler bypass resistance.

**Completion criteria:** Commands pass unit/integration/scenario suites; every mutation and audit is atomic; known Event audit defects have evidence-backed closure; shadow comparison has no unexplained differences.

### Phase 4 — Runtime engine

**Objective:** Replace legacy runtime calculations with one deterministic, typed projection authority.

**Dependencies:** Phase 3 Event semantics and ordered history; trusted runtime baseline infrastructure from Phase 2.

**Deliverables:** RuntimeProjectionService; trusted baseline repository/workflow; operating-window calculation; OH and RuntimeAfterOH separation; valid stopped-state ESD adjustment; pre-range history loading; midnight/Persian boundary handling; calculation version/provenance; legacy comparison harness.

**Production risk:** High because incorrect hours propagate to reports and finalized evidence.

**Required tests:** Start/stop/OH/ESD matrix; overlapping/range-boundary histories; no pre-range Event; baseline-present/missing/invalid; same-time deterministic order; multi-day/month/Persian transitions; edit/delete replay; legacy comparison with expected differences explicitly classified.

**Completion criteria:** Physical and business-rule scenarios are approved; no invalid Event alters runtime; all projections expose provenance/version; representative Rasht/Ramsar results reconcile or have approved defect corrections.

### Phase 5 — Reporting engine

**Objective:** Build typed Station-scoped report projections and immutable finalization using authoritative data/runtime sources.

**Dependencies:** Phases 2–4; approved reporting architecture; confirmed report-audit findings.

**Deliverables:** source repositories; calculation services for main-data min/max/average and daily-unique sums; ReportRequest/Projection/Section; completeness service; SourceRevision/CalculationVersion/ConfigurationVersion; ReportSnapshot/Version; Finalize/Reopen/supersession commands; finalized reads exclusively from snapshot; PDF and normal export contracts.

**Production risk:** Critical: stale or mixed finalized output, incorrect aggregation/completeness, or historical alteration.

**Required tests:** All report types; duplicate observations; missing/invalid inputs; Persian ranges; runtime integration; completeness; source-revision race; finalization transaction; snapshot-only reads; immutable checksums; Management-authorized Reopen; export/print/PDF; Rasht/Ramsar isolation.

**Completion criteria:** All report audit defects are resolved or explicitly deferred; finalized output is complete and immutable; live/finalized comparisons pass; business owners approve calculations and samples.

### Phase 6 — UI modernization

**Objective:** Move workflows to new services without placing business authority in WinForms.

**Dependencies:** Phases 1, 3, 4, and 5 stable application APIs.

**Deliverables:** Shift login/Wizard/management screens; Event workflow; runtime views; Report Center; finalization/Reopen/export UI; structured Persian corrections; scrollable grids; keyboard flow; progress/cancellation outside transactions; DPI/accessibility fixes; legacy/new feature switches.

**Production risk:** Medium–high: operator error, hidden state, focus/keyboard regressions, or UI calling legacy/direct repositories.

**Required tests:** Presenter/view-model unit tests; UI integration; keyboard/mouse; RTL/Persian; DPI/resolution; grid scrolling; accessibility; cancellation/error/focus recovery; locked/finalized states; direct-call denial; performance with large histories.

**Completion criteria:** UI is a thin client of approved services, preserves accepted workflow strengths, exposes actionable errors, and passes operator acceptance at both Stations.

### Phase 7 — Migration and reconciliation

**Objective:** Convert legacy data into target structures on copies, prove completeness, and prepare an executable production migration package.

**Dependencies:** Phases 0–6; target constraints/tests; approved mappings.

**Deliverables:** immutable source snapshot; preflight/anomaly report; canonical mapping; deterministic IDs/order; quarantine policy; dry-run migration; row/control totals/checksums; Event-chain/runtime/report reconciliation; historical audit/snapshot provenance; runbook, timing, backup, rollback, and approval evidence.

**Production risk:** Critical: silent coercion, dropped/duplicated rows, wrong identity/time/Station mapping, or rewritten finalized evidence.

**Required tests:** Every supported legacy version; malformed/null/duplicate/same-time Events; U4 normalization; invalid times (never silently midnight); Persian dates; finalized months; repeatability/idempotency; interruption; disk failure; rollback; before/after control totals and sample reports.

**Completion criteria:** Rehearsals succeed repeatedly within window; every discrepancy is resolved/quarantined/approved; no legacy source is overwritten; rollback is timed and proven; release authority signs migration evidence.

### Phase 8 — Production cutover

**Objective:** Activate the modern architecture with controlled downtime, verification, monitoring, and rapid rollback.

**Dependencies:** All prior gates; trained users; release approval.

**Deliverables:** signed release/package checksums; verified pre-cutover backup; maintenance window; migration/cutover execution; smoke/control-total/report checks; user acceptance; monitoring log; retained prior application/database; incident and rollback decision tree.

**Production risk:** Critical: outage, lost writes, authentication failure, inconsistent migration, or report divergence.

**Required tests:** Dress rehearsal; install/upgrade; recovery material custody; migration verification; Shift login; Event/runtime/report/finalize/export; backup/Restore; performance; crash/restart; rollback rehearsal.

**Completion criteria:** Acceptance checklist passes, both Stations reconcile, no critical/high unresolved cutover defect exists, backups and rollback artifacts are verified, and authorized owners approve activation.

### Phase 9 — Maintenance and future evolution

**Objective:** Sustain correctness without expanding beyond approved production scope unintentionally.

**Dependencies:** Stable Phase 8 operation.

**Deliverables:** monitoring/review cadence; restore drills; credential recovery custody checks; audit/retention; performance baselines; dependency/security review; defect/change process; compatibility fixtures; decommission plan for legacy path after retention approval.

**Production risk:** Medium: drift, untested dependency/schema change, lost recovery material, or premature legacy deletion.

**Required tests:** Scheduled regression; backup Restore drills; integrity/audit-chain checks; credential recovery exercise; upgrade/downgrade rehearsal; Station-specific regression; capacity/performance trends.

**Completion criteria:** Operational owners accept support procedures; recovery remains proven; changes continue through gates; legacy evidence is retained per policy; future scope requires separate architecture approval.

## 4. Dependency graph

```text
Phase 0 Baseline
  └─ Phase 1 Foundation
       └─ Phase 2 Database infrastructure
            └─ Phase 3 Event authority
                 └─ Phase 4 Runtime engine
                      └─ Phase 5 Reporting engine
                           └─ Phase 6 UI modernization
                                └─ Phase 7 Migration/reconciliation
                                     └─ Phase 8 Cutover
                                          └─ Phase 9 Maintenance
```

Before Event implementation: stable Shift/session identity, Management gates, clock/transaction/log/audit contracts, connection factory, Event schema/constraints/repositories, Station and finalized-lock policies.

Before Runtime implementation: canonical validated Event model, deterministic ordering/state machine, full history queries, trusted baseline persistence, Persian/canonical time rules, and replay tests.

Before Reporting implementation: authoritative source repositories, runtime projection, aggregation rules, SourceRevision/configuration/calculation versions, completeness contract, snapshot storage, and transaction/audit services.

Before UI migration: stable application commands/queries and structured errors, authentication/session flows, finalized/Reopen policies, performance envelope, feature switches, and automated non-UI tests. UI never becomes the first enforcement layer.

## 5. Exact coding order

1. Test harnesses, fixtures, database-copy tooling, and baseline characterizations.
2. Shared primitives: Result/error, IDs, clock, Station context, transaction abstraction.
3. Logging/redaction and System/Login audit contracts.
4. ShiftProfile authentication, ManagementCredential, recovery, Wizard, and action proofs.
5. Typed settings, startup coordinator, instance guard, and database metadata.
6. Central SQLite factory, migration ledger, backup/Restore/integrity services.
7. Additive Event/EventAudit/baseline schema and repositories.
8. Event domain values, state machine, validation, then Add/Edit/Delete handlers.
9. RuntimeProjectionService and comparison harness.
10. Report source repositories, calculations, completeness, projections.
11. Snapshot/finalize/read/Reopen/supersession and exports.
12. Authentication/settings UI, Event UI, runtime UI, then Report Center.
13. Migration/reconciliation tooling after target tests pass.
14. Feature activation, cutover packaging, monitoring, then separately approved legacy retirement.

## 6. Testing gate strategy

No phase completes on build success alone. **Unit tests** cover deterministic rules and error contracts. **Integration tests** cover SQLite constraints, repositories, transactions, audits, files, and service composition. **Scenario tests** replay end-to-end Rasht/Ramsar workflows and confirmed legacy defects. **Manual acceptance tests** cover operational meaning, Persian UX, keyboard/DPI, reports, recovery, performance, and runbooks.

Each gate requires: whole-solution build; all prior regression tests; new unit/integration/scenario results; applicable manual sign-off; affected workflow exercise; `git diff` review; no secret/data artifact leakage; risk/rollback review; and exact unresolved-item record. Critical/high failures block progression. Expected legacy differences require documented business approval, not tolerance-based hiding.

## 7. Rollback strategy

Create verified backup points before any schema introduction, migration rehearsal, pilot, production Migration, Restore, feature activation, and final cutover. Record checksums, versions, database identity, application package, and restore instructions outside the live database.

Phases 0–6 remain reversible through additive structures, disabled feature switches, and retained legacy reads/writes; never dual-write without a specified authority and reconciliation rule. Phase 7 operates on copies until production authorization. Migration rollback uses transaction rollback when possible; otherwise restore the verified pre-migration backup with the compatible prior application. Preserve failed artifacts for diagnosis.

Cutover rollback stops writes, records incident/correlation, validates the retained pre-cutover database, restores the prior application/database pair atomically, runs integrity/control-total/smoke checks, and communicates the authoritative data boundary. Post-cutover writes must never be silently discarded or merged; an approved reconciliation decision is required. Rollback criteria and decision owner are established before the window.

## 8. Production deployment strategy

**Development:** disposable databases, unit/integration tests, feature switches, reviewed small batches, and no production credentials/data. **Internal validation:** representative anonymized copies, cross-version migration, security/threat review, UI/DPI/performance, reconciliation, restore and cutover rehearsals. **Pilot deployment:** one explicitly selected controlled workstation/Station window, trained supervisors, heightened logging/audit review, daily reconciliation, verified fallback, and predetermined stop criteria. **Full deployment:** only after pilot stability and owner approval; stagger where feasible, repeat verified runbook, retain old package/database, and monitor authentication, integrity, Events, runtime, reports, backups, and performance.

## 9. Team workflow

Documentation approval precedes implementation and records architecture owner, domain owner, security/data owner, and version. Code review requires traceability to approved decision/test, small diff, dependency direction, transaction/security review, and no unrelated rewrite. Test approval is independent evidence that all four test levels and regression gates passed with discrepancies classified. Release approval requires migration/rollback rehearsal, verified backup/Restore, security review, operational training, package checksum, open-risk acceptance, and named cutover/rollback authority. Emergency fixes follow the same evidence trail after immediate containment.

## 10. Final checklist

- [ ] Architecture approved
- [ ] Foundation complete
- [ ] Database infrastructure and constraints validated
- [ ] Event complete
- [ ] Runtime validated
- [ ] Reports validated
- [ ] UI accepted at Rasht and Ramsar
- [ ] Migration approved and rehearsed
- [ ] Security verified
- [ ] Backup and Restore tested
- [ ] Reconciliation signed off
- [ ] Rollback timed and approved
- [ ] Release approved
- [ ] Recovery material custody confirmed
- [ ] Legacy evidence retained

## 11. Final decision table

| Phase | Main Deliverable | Depends On | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 0 | Protected reproducible baseline | Approved source documents | Medium | Builds, fixtures, backup/Restore, and evidence inventory approved |
| 1 | Authentication/foundation services | 0 | Medium | Shift/Management model, audit, security, startup gates pass |
| 2 | Safe SQLite infrastructure/target structures | 0–1 | High | Additive schema, transactions, integrity, backup/Restore proven |
| 3 | Authoritative Event commands | 1–2 | High | Transition/chain/lock/audit suites and shadow comparison pass |
| 4 | Deterministic runtime projection | 2–3 | High | OH/ESD/baseline/range results reconcile and are versioned |
| 5 | Typed reports and immutable snapshots | 2–4 | Critical | Calculations/completeness/finalization/Reopen/export approved |
| 6 | Service-driven modern UI | 1, 3–5 | High | DPI/keyboard/Persian/operator acceptance; no UI-only authority |
| 7 | Rehearsed migration/reconciliation | 0–6 | Critical | Repeatable migration, zero unexplained discrepancy, rollback proven |
| 8 | Controlled production activation | 0–7 | Critical | Cutover checks, Station sign-off, monitoring and fallback ready |
| 9 | Sustainable operations | 8 | Medium | Regression, restore/recovery drills, review cadence operational |

