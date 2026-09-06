# Phase 9.6A — Baseline & Scope Freeze

Status: **BASELINE AND SCOPE FROZEN**  
Decision: **READY TO BEGIN PHASE 9.6B**

This record freezes the Phase 9.6 starting point. It is documentation-only. No
production code, tests, database schema, startup behavior, routing, authority
state, activation state, or qualification behavior was changed by this phase.

## 1. Baseline identity

| Item | Frozen value |
|---|---|
| Branch | `phase9.6-activation-readiness` |
| Source tag | `phase9.5-governance-closed` |
| Source commit | `e8ec4ecc2fc72c3ce3749d98dd52a17f8717c176` |
| HEAD verification | HEAD resolves to the source commit; the source tag resolves to the same commit |
| Worktree at inspection | Clean before this documentation change |

Phase 9.5 closure artifacts inspected included the project-owner acceptance,
alternative-governance review, independent-review package/checklist,
manual-qualification results/runbook, the C5–C10 remediation and closure
records, and the Phase 9.5A readiness gate.

## 2. Frozen authority boundaries

The following are the only current authority meanings for this baseline:

- **Legacy = AUTHORITATIVE.**
- **Target = NON-AUTHORITATIVE.**
- **Target Routing = DISABLED.**
- **Production Activation = UNAUTHORIZED.**
- **Production Cutover = UNAUTHORIZED.**

Qualification and Pilot evidence are observation/qualification evidence only.
An eligibility receipt, approval state, migration rehearsal, or readiness
artifact must not be interpreted as an authority transition or activation.

## 3. Product and operating constraints

These constraints are frozen for Phase 9.6 and may not be relaxed implicitly:

- Production architecture remains generic, dynamic, and profile-driven. Station
  names must not be production behavior selectors.
- The supported unit boundary is **3–5 units inclusive**. Values outside that
  boundary, including 35, are rejected by the current generic provisioning
  policy.
- Rasht and Ramsar are legacy, qualification, migration, and test fixtures
  only; no Rasht/Ramsar-specific production branching is introduced.
- `ShiftProfile` is the only normal operational identity.
- `ManagementCredential` is a singleton privileged proof for protected actions;
  it is not an RBAC role or a second normal operator identity.
- No RBAC, Support identity, backdoor, universal secret, master credential, or
  hidden recovery identity may be added.
- The event model is strict and limited to `START`, `NSD`, `ESD`, and `OH`.
- Finalized reports remain immutable through canonical JSON plus checksum, with
  finalized-period locks and snapshots protected.
- Qualification remains isolated from the Production DB and must use
  disposable/isolated qualification data.
- The qualification DPI boundary is 100%, 125%, and 150%; qualification above
  150% is blocked and must not be represented as supported evidence.
- Existing Persian-date conventions, data-start-date rules, 12 odd-hour main
  observations, daily-unique requirements, optional events, reporting
  aggregations, and finalized-month protection remain in force.

## 4. Phase 9.5 limitations carried forward exactly

- **MQ-07 remains BLOCKED.** Its manual observation was not practically
  exercisable; automated invariant evidence is retained but does not convert it
  to PASS.
- **Independent Human Review was NOT PERFORMED / UNAVAILABLE.**
- **AI-assisted review is not organizationally independent.** The Phase 9.5
  alternative governance path and project-owner acceptance do not change that
  limitation or authorize activation.

These limitations are governance/evidence facts, not silently closed by this
baseline. Any Phase 9.6 prerequisite contract must state their treatment and
required approval explicitly.

## 5. Phase 9.6 definition and work packages

Phase 9.6 is **Production Activation Readiness**, not Production Activation.
Its purpose is to define, implement only where separately authorized, and
qualify the evidence and operating controls needed for a future activation
decision.

Proposed work packages:

1. **9.6A Baseline & Scope Freeze** — this record; freeze identity, boundaries,
   constraints, limitations, conflicts, and entry decision.
2. **9.6B Activation Prerequisite Contract** — define mandatory evidence,
   approvals, identities, database checks, integrity, rollback, audit, and
   fail-closed prerequisites without executing them.
3. **9.6C Authority Transition Design** — design the explicit, approved,
   auditable transition boundary and terminal states without making Target
   authoritative.
4. **9.6D Cutover / Abort / Rollback Design** — define trigger, owner, data
   boundary, abort conditions, restore handling, routing behavior, audit, and
   rollback evidence.
5. **9.6E Production-like Rehearsal** — rehearse against isolated,
   production-like copies only; preserve the Production DB and current
   authority.
6. **9.6F Activation Qualification** — qualify the prerequisites and evidence,
   including negative/fail-closed cases, without activation.
7. **9.6G Operator Cutover Runbook** — document the separately authorized
   operator sequence, receipts, stop points, abort path, and handoffs.
8. **9.6H Governance Closure** — record review, limitations, approvals, and a
   separate decision about whether any future activation phase may begin.

## 6. Explicit Phase 9.6 prohibitions

Phase 9.6 must not itself:

- execute Production Cutover;
- make Target authoritative;
- enable Target routing;
- remove Legacy authority;
- add RBAC;
- add a Support identity;
- change the 3–5 unit boundary;
- introduce Rasht/Ramsar-specific production branching.

No work package may infer authorization from readiness, rehearsal, a passing
test, an eligibility receipt, owner acceptance, or this entry decision.

## 7. Repository architecture and evidence map

The repository is a .NET 8 Windows Forms application with SQLite persistence,
an existing Legacy runtime/UI path, and separated Application, Core,
Infrastructure, and test/qualification surfaces. Target security,
provisioning, migration, Runtime/Event, reporting/finalization, and activation
readiness contracts exist as controlled foundations. Target operational routes
are composed as disabled descriptors; the inactive composition reports
`TargetRoutesEnabled = false`, `LegacyRemainsAuthoritative = true`, and no
production mutation.

The activation boundary currently evaluates eligibility and persists evidence;
its contracts require Legacy authority, disabled Target routing, and
`ActivationExecuted = false`. The Phase 9.5A gate still records missing or
blocked production executor, restore, audit, rollback, and production-data
evidence capabilities. Those are readiness gaps, not permission to implement
or invoke cutover in 9.6A.

## 8. Build and dependency baseline

The full solution Release build completed successfully: **0 errors, 12
warnings**. All warnings are NU1701 compatibility warnings for
`OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and
`SkiaSharp.Views.WindowsForms 3.119.0`, restored from .NET Framework assets for
the `net8.0-windows` solution. No package was changed in this phase. The
warnings remain a dependency-health item for later review; they do not alter
the frozen authority or activation boundary.

## 9. Conflicting repository evidence and disposition

The following evidence conflicts with, or requires reconciliation against,
this freeze. It is recorded without changing historical remediation evidence:

- `docs/phase9.5c6-mq03-provisioning-requalification.md` preserves the
  superseded C6 defect in which 1–35 units were allowed, and
  `docs/phase9.5c7-mq03-unit-boundary-remediation.md` records the correction to
  3–5 inclusive. The C6 record remains historical evidence; C7 is the current
  boundary.
- `Infrastructure/Pilot/LivePilotReadOnlyPreflight.cs` intentionally accepts
  only Rasht/Ramsar station types for the legacy read-only Pilot surface. This
  is compatible only as a fixture/qualification boundary, not as generic
  production architecture.
- `Application/Activation/ProductionActivationBoundary.cs` currently validates
  activation station scope as `station-rasht` or `station-ramsar`. This is
  repository evidence of station-specific activation-scope logic and must be
  resolved or explicitly bounded in 9.6B/9.6C before any future production
  activation design can claim full genericity.
- Older roadmap/audit documents describe Rasht/Ramsar as the current production
  scope and contain historical station-specific assumptions. They are not
  silently rewritten; the frozen Phase 9.6 interpretation is the constrained
  distinction between legacy/fixture scope and generic Target production
  architecture.

No speculative concern is classified as a confirmed production bug by this
document. No historical defect record is amended merely because it conflicts
with the current boundary.

## 10. Entry decision

**READY TO BEGIN PHASE 9.6B.**

This decision authorizes only prerequisite-contract work within the frozen
scope. It does not authorize Production Activation, Production Cutover,
authority transition, routing enablement, production database mutation, or
closure of MQ-07 or the independent-review limitations. The station-scope
conflict and all Phase 9.5 limitations are exact reasons that 9.6B must define
explicit gates and cannot be treated as implicit activation approval.
