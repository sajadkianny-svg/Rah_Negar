# Phase 9.6B — Activation Prerequisite Contract

Status: **CONTRACT DEFINED — ACTIVATION NOT AUTHORIZED**

This is a documentation-only contract. It does not implement, invoke, or
authorize Production Activation, Production Cutover, Target routing,
authority transition, database mutation, startup registration, or qualification
behavior. It applies to any later package that may ask whether a future
Production Activation decision can be considered.

## 1. Frozen authority boundary

The Phase 9.6 authority boundary is unchanged:

- **Legacy = AUTHORITATIVE**.
- **Target = NON-AUTHORITATIVE**.
- **Target Routing = DISABLED**.
- **Production Activation = UNAUTHORIZED**.
- **Production Cutover = UNAUTHORIZED**.

Qualification, Pilot, migration rehearsal, readiness receipts, owner
acceptance, and an eligibility result are evidence only. None grants authority
or permits a production write. Phase 9.5 used Rasht and Ramsar as legacy or
disposable qualification fixtures; it did not authorize a Target production
route.

The supported generic Target profile boundary remains **3–5 units inclusive**.
Rasht/Ramsar-specific implementation or fixture behavior must not be treated
as proof of generic production readiness. Existing Persian-date conventions,
data-start-date rules, 12 odd-hour observations, daily-unique requirements,
optional events, reporting aggregation rules, and finalized-month protection
remain in force.

## 2. Evaluation vocabulary and aggregation

Every prerequisite evaluation has exactly one outcome:

| Outcome | Meaning |
|---|---|
| `SATISFIED` | The mandatory evidence is complete, current, authentic, in scope, and its evaluation rule passed. |
| `NOT_SATISFIED` | Evidence exists but a required assertion failed, is stale, mismatched, unsafe, or contradicted by the repository. |
| `NOT_EVALUATED` | A required evaluation was not performed, cannot be independently established, or its governance interpretation is still pending. It is not a pass. |
| `BLOCKED` | Evaluation cannot safely proceed because a required capability, control, procedure, access boundary, or evidence source is unavailable or explicitly blocked. |

The only aggregate result is one of:

- `ELIGIBLE_FOR_ACTIVATION_DECISION`
- `NOT_ELIGIBLE_FOR_ACTIVATION_DECISION`

Aggregation is fail-closed. `ELIGIBLE_FOR_ACTIVATION_DECISION` is permitted
only when **every mandatory prerequisite is `SATISFIED`** and the evidence
package is internally consistent and within its validity window. Any mandatory
`NOT_SATISFIED`, `NOT_EVALUATED`, or `BLOCKED` result produces
`NOT_ELIGIBLE_FOR_ACTIVATION_DECISION`. Optional evidence may improve context,
but can never repair a mandatory result or change the aggregate.

Evaluation is read-only with respect to authority, routing, production
activation state, and Legacy/Target data authority. An evaluator may inspect
copies, receipts, hashes, configuration, source, and isolated rehearsal
artifacts; it may not edit an authority flag, enable a route, migrate a
production database, or alter Legacy/Target ownership.

## 3. Evidence record contract

Each evaluation record must contain, at minimum: stable prerequisite ID;
evaluation run ID; correlation ID; evaluated scope and database identity where
applicable; UTC timestamp; evaluator identity and source; software/build and
OS/version context where relevant; evidence references; result; and safe
reason/details. Evidence files, receipts, screenshots, logs, and reports must
be immutable or SHA-256 hashed when appropriate. Hashes must be calculated over
the exact artifact reviewed, with artifact identity, size, and capture time
retained. Secrets, private keys, raw passwords, master credentials, and
production data must not be copied into evidence.

Evidence must be traceable across prerequisite records by correlation ID and
must bind approvals, database fingerprints, backup fingerprints, build hash,
scope, and validity/expiry where those concepts apply. A missing, conflicting,
unverifiable, or unbound artifact is not evidence of satisfaction.

## 4. Mandatory prerequisite register

All entries below are mandatory. “Automatic” means a deterministic tool or
contract check may produce the result; “manual” means an authorized human must
inspect, observe, approve, or attest; “both” requires both forms where stated.

### PR-01 — Governance and authorization

- **Purpose:** Establish that the requested future decision is within an
  approved Phase 9.6 scope and has a named authority, scope, expiry, and
  accountable decision owner.
- **Status:** Mandatory.
- **Required evidence:** Later governance package; approved scope; named
  accountable owner; authorization ID; approval ID; UTC issue/expiry; target
  database identity; evidence-package ID; correlation ID; and recorded
  limitation dispositions.
- **Evaluation rule:** Verify signatures/identity, scope, dates, database and
  evidence binding, and that the approval authorizes consideration only—not
  activation or cutover by implication.
- **Failure result:** Invalid, missing, expired, ambiguous, or unbound approval
  = `NOT_SATISFIED`; unavailable governance evidence = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-02 — Phase 9.5 qualification evidence integrity

- **Purpose:** Confirm that Phase 9.5 evidence is authentic, complete,
  correctly scoped, internally consistent, and not presented as production
  evidence.
- **Status:** Mandatory.
- **Required evidence:** Current qualification package, runbook, result
  tables, TRX files and SHA-256 hashes where used, sanitized receipts,
  fixture/database identity, build/version context, and reconciliation of
  historical C5–C10 records.
- **Evaluation rule:** Match every claim to its exact artifact and scope;
  reject stale, altered, contradictory, fixture-only, or unsupported claims.
  Preserve the distinction between technical/operator evidence and governance
  sign-off.
- **Failure result:** Integrity or traceability failure = `NOT_SATISFIED`;
  incomplete or unavailable review package = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-03 — MQ status and disposition

- **Purpose:** Establish the authoritative disposition of MQ-01 through MQ-12
  and determine whether that disposition is acceptable for a later activation
  decision.
- **Status:** Mandatory.
- **Required evidence:** Phase 9.5 consolidated results, manual runbook,
  independent-review checklist/package, alternative-governance review, and
  owner acceptance, all with hashes and correlation to the qualification
  package.
- **Evaluation rule:** Preserve **MQ-07 exactly as `BLOCKED — MANUAL
  OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE
  RETAINED`**. Do not convert it to PASS. Analyze whether a blocked manual
  item may be a formally accepted residual limitation for future activation,
  or whether it must be closed. The current repository supplies no independent
  human determination resolving that governance question. Therefore the
  activation treatment of MQ-07 is `NOT_EVALUATED`, pending a later Phase 9.6
  governance package that explicitly accepts or rejects the residual risk.
- **Failure result:** MQ-07 remains `BLOCKED` as a qualification fact; its
  unresolved activation treatment yields `NOT_EVALUATED` for this prerequisite.
  Any failed MQ or unapproved residual limitation yields `NOT_SATISFIED`.
- **Evaluation mode:** Both automatic and manual.

Independent Human Review remains **NOT PERFORMED / UNAVAILABLE**. AI-assisted
review is not organizationally independent and must not be described as such.
Project-owner acceptance does not silently substitute for independent review
or invent acceptance of MQ-07.

### PR-04 — MQ status and Phase 9.5 evidence integrity

- **Purpose:** Confirm that each MQ result is supported by the right evidence
  class and that automated invariant evidence is not misrepresented as manual
  observation or independent review.
- **Status:** Mandatory.
- **Required evidence:** Per-MQ evidence matrix, reviewer identity, test/build
  context, manual observation records, blocked/unavailable reasons, and exact
  mapping to qualification gates.
- **Evaluation rule:** Reconcile counts, statuses, scope, and evidence type;
  reject any PASS that depends on an unavailable observation or independent
  sign-off.
- **Failure result:** Contradiction or misclassification = `NOT_SATISFIED`;
  unperformed reconciliation = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-05 — MQ status (reserved for future governance disposition)

- **Purpose:** Preserve a separate auditable decision point for whether the
  MQ-07 residual limitation is acceptable, without changing its qualification
  status.
- **Status:** Mandatory.
- **Required evidence:** A later Phase 9.6 governance record naming the
  decision owner, rationale, risk acceptance or closure requirement, scope,
  expiry, compensating controls, and explicit statement that MQ-07 remains
  BLOCKED.
- **Evaluation rule:** Accept only an explicit, auditable governance decision;
  infer nothing from owner acceptance, technical review, or readiness artifacts.
- **Failure result:** No such decision = `NOT_EVALUATED`; rejection or expired
  acceptance = `NOT_SATISFIED`.
- **Evaluation mode:** Manual.

### PR-06 — Migration readiness

- **Purpose:** Prove that the exact target migration chain, checksums,
  idempotency, rollback semantics, and preservation checks are ready for a
  separately authorized implementation.
- **Status:** Mandatory.
- **Required evidence:** Isolated production-like rehearsal receipts, migration
  ledger, checksum inventory, initial/final versions, rollback result, backup
  binding, preservation comparison, and failure-case evidence.
- **Evaluation rule:** Require a supported clean history, verified checksums,
  deterministic no-op rerun, unchanged source/backup, and no authority change.
  A test double or rehearsal alone is not a production executor.
- **Failure result:** Missing executor/procedure or failed preservation =
  `NOT_SATISFIED`; unavailable exact-scope rehearsal = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-07 — Backup, restore, and recovery readiness

- **Purpose:** Ensure recoverability before any future live replacement or
  authority decision.
- **Status:** Mandatory.
- **Required evidence:** Exact backup identity/hash, SQLite and foreign-key
  integrity, isolated restore rehearsal, immutable rollback copy, staged
  replacement procedure, crash/fault recovery result, owner, and audit receipt.
- **Evaluation rule:** Verify the selected backup restores to an isolated
  destination and that failure cannot leave an ambiguous live database.
- **Failure result:** Direct overwrite, missing rollback, failed restore, or
  missing authorization = `NOT_SATISFIED`; missing exact production-like
  evidence = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-08 — Target schema and data integrity

- **Purpose:** Prove target schema, mappings, constraints, identities, rows,
  events, baselines, snapshots, locks, credentials, and ESD values are
  complete and consistent without taking authority.
- **Status:** Mandatory.
- **Required evidence:** Schema/migration inventory, integrity and foreign-key
  checks, identity fingerprints, row/count reconciliation, station/unit
  mapping, ESD/event/baseline/snapshot/lock preservation, and negative-case
  receipts.
- **Evaluation rule:** Require exact-scope reconciliation and no partial
  writes; reject fixture-only proof or any mismatch.
- **Failure result:** Any integrity, mapping, preservation, or partial-write
  failure = `NOT_SATISFIED`; absent production-like data evidence =
  `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-09 — Profile compatibility and 35-unit boundary

- **Purpose:** Confirm generic profile-driven behavior and the frozen 3–5
  inclusive product boundary.
- **Status:** Mandatory.
- **Required evidence:** Profile manifests, arbitrary-name 3- and 5-unit
  success evidence, 2-, 6-, and **35-unit rejection** evidence, no-partial-row
  checks, station identity mapping, and final build evidence.
- **Evaluation rule:** Unit count comes from the supplied profile, not a station
  name; 35 is outside the product boundary and must be rejected. Rasht/Ramsar
  fixtures cannot prove generic production compatibility.
- **Failure result:** Any 35-unit acceptance, station-name selector, or partial
  provisioning = `NOT_SATISFIED`; missing boundary evidence = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-10 — Authentication and ManagementCredential integrity

- **Purpose:** Prove that normal identity is ShiftProfile and protected
  actions use the singleton, scoped, expiring ManagementCredential proof.
- **Status:** Mandatory.
- **Required evidence:** Authentication and session-boundary tests,
  ManagementCredential version/state records, protected-action matrix,
  recovery procedure, atomic audit receipts, expiry/scope/concurrency tests,
  and final-binary reachability review.
- **Evaluation rule:** Reject RBAC, Support identities, legacy-login
  substitution, embedded recovery bypasses, raw credentials, and unbounded
  recovery.
- **Failure result:** Any bypass or missing protected-action proof =
  `NOT_SATISFIED`; incomplete production composition/review = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-11 — Vendor-signed ESD authorization validation readiness

- **Purpose:** Ensure future ESD changes can be accepted only through valid,
  exact, replay-resistant vendor authorization.
- **Status:** Mandatory.
- **Required evidence:** Approved key IDs/lifecycle, canonical envelope and
  payload version, ECDSA P-256 verification receipts, exact device/request/
  action/value/time binding, expiry/replay tests, ManagementCredential proof,
  and non-secret audit records.
- **Evaluation rule:** Require active trusted key and valid signature; reject
  malformed, unknown, inactive, expired, future, mismatched, or unavailable
  verification.
- **Failure result:** Any verification weakness or unavailable validation path =
  `NOT_SATISFIED`.
- **Evaluation mode:** Both automatic and manual.

### PR-12 — Event state-machine integrity

- **Purpose:** Prove strict event types, valid transitions, temporal ordering,
  auditability, and safe handling of event/runtime relationships.
- **Status:** Mandatory.
- **Required evidence:** Event policy and transition tests, normalized chains,
  audit receipts, invalid-transition negatives, baseline/date/time checks, and
  production-like reconciliation for all in-scope units.
- **Evaluation rule:** Allow only START, NSD, ESD, and OH under current rules;
  reject invalid order, duplicate/conflicting records, pre-baseline events,
  and Persian/UTC conversion inconsistencies.
- **Failure result:** Any invariant failure = `NOT_SATISFIED`; absent exact-scope
  reconciliation = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-13 — Finalized-report immutability

- **Purpose:** Ensure finalized reports remain immutable and are read/exported
  from integrity-checked snapshots rather than mutable operational data.
- **Status:** Mandatory.
- **Required evidence:** Canonical JSON, checksum, snapshot/lock identities,
  finalization receipts, mutation rejection, finalized-period read/export tests,
  and preservation evidence through rehearsal.
- **Evaluation rule:** Any mutation, recalculation from mutable sources, lock
  bypass, checksum mismatch, or export-path substitution fails the check.
- **Failure result:** `NOT_SATISFIED`; missing production-like preservation
  evidence = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-14 — Production DB isolation during qualification/rehearsal

- **Purpose:** Ensure no qualification or rehearsal can identify, modify,
  replace, or become authoritative over the Production DB.
- **Status:** Mandatory.
- **Required evidence:** Canonical path and identity checks, isolated/disposable
  directory records, before/after production hash, launcher configuration,
  read-only guards, and negative path-rejection receipts.
- **Evaluation rule:** Verify separate file identity, no production path in
  fixtures/media, and no authority/routing/data mutation during the run.
- **Failure result:** Any production touch or ambiguous identity =
  `NOT_SATISFIED`; no independently verified isolation = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-15 — DPI/UI readiness

- **Purpose:** Ensure required operator surfaces are readable, usable, RTL-safe,
  keyboard-accessible, and stable at 100%, 125%, and 150% DPI.
- **Status:** Mandatory.
- **Required evidence:** Sanitized screenshots and operator records for both
  supported fixtures, all three DPI scales, focus order, dialogs, grids,
  status/identity fields, Stop/Complete/Return, and final build hash.
- **Evaluation rule:** Reject clipping, overlap, inaccessible controls, hidden
  state, or unsupported DPI claims. MQ-07’s blocked cancellation observation
  remains separate and unresolved under PR-03/PR-05.
- **Failure result:** Any required scale/workflow failure = `NOT_SATISFIED`;
  unavailable manual observation = `NOT_EVALUATED`.
- **Evaluation mode:** Manual, with automatic supporting checks.

### PR-16 — Operator runbook readiness

- **Purpose:** Provide a complete, versioned, role-assigned, reversible
  operator sequence for evidence collection and future decision handling.
- **Status:** Mandatory.
- **Required evidence:** Approved runbook, preconditions, stop points,
  credentials/secret-handling instructions, receipts, handoffs, abort triggers,
  escalation contacts, and operator training/acknowledgement.
- **Evaluation rule:** A new operator must be able to execute the evidence
  workflow without undocumented judgment or bypass.
- **Failure result:** Missing or ambiguous steps = `NOT_SATISFIED`; no trained
  evaluator/observation = `NOT_EVALUATED`.
- **Evaluation mode:** Manual.

### PR-17 — Cutover, abort, and rollback procedure readiness

- **Purpose:** Define a controlled future transaction with explicit trigger,
  owner, data boundary, validation, abort, restore, routing, authority, and
  terminal-state behavior.
- **Status:** Mandatory.
- **Required evidence:** Approved 9.6D/9.6G procedure, isolated rehearsal,
  abort receipts, rollback owner and decision boundary, staged restore proof,
  post-validation checklist, and audit sequence.
- **Evaluation rule:** No activation step may be coupled merely to migration
  completion; authority acceptance requires successful validation, and every
  failure path must preserve Legacy authority.
- **Failure result:** Missing executor, unsafe replacement, undefined rollback,
  or ambiguous authority state = `NOT_SATISFIED`.
- **Evaluation mode:** Both automatic and manual.

### PR-18 — Auditability and evidence retention

- **Purpose:** Make every evaluation, approval, failure, abort, and rollback
  reconstructable without exposing secrets or production data.
- **Status:** Mandatory.
- **Required evidence:** Append-only audit store, retention policy, artifact
  hashes, correlation IDs, timestamps, evaluator/source, result/reason,
  software context, access controls, export/readback test, and retention owner.
- **Evaluation rule:** Require durable writes for both success and failure;
  reject mutable, incomplete, secret-bearing, or uncorrelated evidence.
- **Failure result:** Missing audit or retention capability = `NOT_SATISFIED`;
  inability to inspect it safely = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-19 — Authority-transition safety

- **Purpose:** Ensure any future transition is explicit, atomic, authorized,
  auditable, installation-bound, and impossible to infer from readiness.
- **Status:** Mandatory.
- **Required evidence:** 9.6C design, durable authority-state model, guarded
  transaction specification, exact routing boundary, negative tests,
  transition/rollback receipts, and final-binary review.
- **Evaluation rule:** Legacy must remain authoritative until the separately
  authorized controlled transaction completes all checks. Readiness,
  rehearsal, approval, or an eligibility receipt alone must not transition
  state.
- **Failure result:** Missing implementation/design safety or any bypass path =
  `NOT_SATISFIED`; unperformed final review = `NOT_EVALUATED`.
- **Evaluation mode:** Both automatic and manual.

### PR-20 — Explicit owner/governance approval for a future activation decision

- **Purpose:** Establish a separate, explicit decision to consider activation,
  without confusing it with permission to execute activation or cutover.
- **Status:** Mandatory.
- **Required evidence:** Named accountable owner and governance approver,
  decision scope, evidence-package ID, database fingerprint, UTC timestamp,
  expiry, rationale, unresolved limitations, and approval audit entry.
- **Evaluation rule:** Approval must be affirmative, current, identity-bound,
  and conditional on all mandatory prerequisites being SATISFIED. It must not
  waive MQ-07, independent-review absence, or any other prerequisite unless a
  later governance package explicitly defines and audits that waiver policy.
- **Failure result:** Missing, ambiguous, expired, or improperly scoped approval
  = `NOT_SATISFIED`; unavailable governance review = `NOT_EVALUATED`.
- **Evaluation mode:** Manual, with automatic binding checks.

## 5. Anti-bypass rules

The following are prohibited and must be negative-tested or manually reviewed
where applicable:

- no hidden override;
- no Support identity;
- no master password;
- no universal activation code;
- no manual database flag editing;
- no direct Target routing enablement outside the future controlled activation
  transaction;
- no partial prerequisite waiver unless a later governance package explicitly
  defines the waiver authority, scope, compensating controls, expiry, and audit
  record.

No evaluator, operator, developer, project owner, or support contact may turn a
`NOT_SATISFIED`, `NOT_EVALUATED`, or `BLOCKED` prerequisite into
`SATISFIED` by relabeling evidence, changing a database value, rerunning a
different scope, or relying on an undocumented emergency path.

## 6. Current gaps deferred to later Phase 9.6 work packages

The repository evidence reviewed for this contract identifies, without fixing,
these later work items:

1. The production migration executor, production restore/replacement boundary,
   complete rollback implementation, end-to-end activation audit wiring, and
   production data evidence are absent or only represented by contracts,
   test doubles, or isolated rehearsal foundations.
2. The current activation boundary contains station-specific Rasht/Ramsar
   scope validation while the frozen Target architecture is generic and
   profile-driven. This must be resolved or explicitly bounded before generic
   activation readiness can be claimed.
3. Target production composition, authentication adoption, ManagementCredential
   recovery/composition, ESD provisioning, baseline adoption, and complete
   production data reconciliation are not established by qualification fixtures.
4. MQ-07’s blocked observation has no later independent governance disposition.
   Independent Human Review remains NOT PERFORMED / UNAVAILABLE, and the
   AI-assisted review is not organizationally independent.
5. A complete, approved cutover/abort/rollback transaction and operator
   runbook with exact production-like rehearsal evidence remain to be defined.
6. Final evidence-retention ownership, immutable artifact handling, and a
   complete prerequisite evaluator/aggregate are not yet implemented.

These are documentation and implementation inputs to later 9.6C–9.6H work.
No fix is authorized or made by Phase 9.6B.

## 7. What Phase 9.6B does not authorize

This contract does not authorize Production Activation, Production Cutover,
Target authority, Target routing, Legacy de-authoritization, production
database mutation, startup behavior changes, qualification changes, or any
authority-state transition. It authorizes only the definition of a
fail-closed prerequisite contract and the later evaluation of evidence under
that contract.

## Phase 9.6B decision

**BLOCKED** — MQ-07 remains BLOCKED and its acceptance as a future activation
residual is NOT_EVALUATED; Independent Human Review is NOT PERFORMED /
UNAVAILABLE and AI-assisted review is not organizationally independent; and
the repository still lacks the production executor/restore/rollback/audit
controls, complete production-like evidence, generic station-scope resolution,
and governance/runbook package required to satisfy all mandatory prerequisites.
