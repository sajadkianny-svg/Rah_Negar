# Phase 9.6B1 MQ 07 Activation Eligibility Analysis

Status: **ANALYSIS COMPLETE - GOVERNANCE DECISION REQUIRED**  
Scope: Technical and governance analysis only. No production code, tests,
database schema, qualification behavior, timing behavior, authority, routing,
or activation state was changed.

## 1. Decision question and preserved qualification status

This analysis determines whether the unresolved MQ-07 manual observation must
remain a hard blocker for any future activation eligibility decision or may be
treated as an explicitly accepted residual limitation. It does not close MQ-07,
change the Phase 9.6B prerequisite contract, or authorize activation.

The current MQ-07 status is preserved exactly:

**BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**

MQ-07 is not PASS. The Phase 9.5 Project Owner Acceptance concerns Phase 9.5
qualification/governance only and does not constitute a Phase 9.6 acceptance of
the residual limitation. Independent Human Review remains NOT PERFORMED /
UNAVAILABLE, and the AI-assisted review is not organizationally independent.

## 2. Evidence reviewed

The analysis reviewed the Phase 9.6A frozen baseline, the Phase 9.6B
activation-prerequisite contract, the consolidated Phase 9.5 manual results and
runbook, the C5-C10 reconciliation/closure records, the Phase 9.5 alternative
governance and independent-review records, and the related implementation and
tests.

The relevant source evidence is:

| Evidence | Relevant fact |
|---|---|
| `docs/phase9.5c-manual-qualification-results.md:62-75, 284-303` | MQ-07 is the only blocked item; all five workflows completed before Stop could be invoked; no manual PASS is claimed; no artificial production delay or qualification-only timing control was added. |
| `docs/phase9.5-manual-qualification-runbook.md:162-169, 299-303, 326-332` | Intended test is active-session cancellation before review, with responsiveness, no false completion, no unhandled exception, safe evidence, unchanged data/authority, and Legacy return. |
| `docs/phase9.5c5-mq08-closure-and-next-manual-batch.md:239-246` | C5 records the MQ-07 disposition and explicitly does not authorize cutover or change authority. |
| `docs/phase9.5c10-dpi-and-mq12-manual-closure.md:18-30` | Later manual reconciliation retains MQ-07 blocked and does not convert it to PASS. |
| `docs/phase9.6a-baseline-and-scope-freeze.md:64-77, 187-193` | The exact limitation is frozen into Phase 9.6; no later package may silently close it. |
| `docs/phase9.6b-activation-prerequisite-contract.md:121-179, 431-474` | MQ-07 qualification remains blocked; activation treatment is `NOT_EVALUATED` until an explicit later governance decision. Aggregation is fail-closed. |
| `UI/Composition/Pilot/FrmLivePilot.cs:72-145, 160-224` | The UI has explicit Start, Stop, Complete, Return, close-guard, cancellation, and safe-failure paths. It does not expose an automatic start or authority switch. |
| `Application/Pilot/Live/LivePilotOperatorSession.cs:37-94` | The operator session declares no automatic retry, polling, timer, background work, production mutation, or authority change. |
| `Application/Pilot/Operational/ControlledPilotOperationalRehearsalCoordinator.cs:227-320` | Observation checks cancellation between workflows and on observer cancellation, creates a terminal stop result, persists evidence, and does not run production migration or change authority. |
| `Rah_Negar.Tests/Pilot/ControlledPilotOperationalReadinessTests.cs:276-295` | Automated cancellation test verifies stopped lifecycle, `Cancellation` stop reason, valid immutable evidence, and inability to restart. |
| `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs:165-182, 322-395` | Session cancellation/shutdown is safe; close and confirmed-close tests verify stopped state, no completion, no authority change, no Target activation, and profile independence. |

## 3. What MQ-07 was intended to prove

MQ-07 maps to UI-03, active-session cancellation. Its intended proof was not
merely that a cancellation token exists. It was an end-to-end human observation
that an operator can cancel an actually active Pilot observation before the
workflow reaches review. The expected result was a responsive cancellation,
without false review or completion, unhandled exception, unsafe evidence, data
mutation, authority change, or loss of usable return to the Legacy application.

The intended operational property is therefore operator control over an
in-progress qualification/rehearsal session. The runbook distinguishes this
from MQ-06, where the operator stops after successful active observation, and
from MQ-08, where the operator closes an incomplete Pilot form.

## 4. Why manual observation was not practically exercisable

The isolated qualification launcher could start the Pilot surface, but all five
selected workflows completed before a human could click Stop/cancel during the
active observation window. The available computer-use environment also lacked a
native desktop app surface for the intended manual observation and screenshots.
The result is a qualification-method limitation, not evidence of a confirmed
production coding defect.

This is a real observation gap: the repository does not contain a human,
in-progress, end-to-end MQ-07 PASS. It is not appropriate to infer one from
completion, from MQ-06, from MQ-08, or from automated tests.

## 5. Automated invariant evidence retained

The automated evidence independently exercises the underlying cancellation
invariant at the operational-coordinator and session boundaries:

1. `ObserveAsync` links the caller token with the session lifetime token,
   checks cancellation before each workflow, treats observer cancellation as a
   cancellation stop, and finishes with a terminal stopped result.
2. The coordinator's cancellation test verifies `Stopped`, the explicit
   `Cancellation` stop reason, a valid checksummed evidence bundle, and a
   blocked restart attempt.
3. Session tests verify cancellation/shutdown does not escape as an unsafe
   exception and that the session becomes terminal or remains safely before
   review.
4. Operator decision tests verify that stop decisions stop the rehearsal only,
   do not stop Production, do not execute rollback, and preserve the authority
   boundary.
5. UI close tests verify the related incomplete-session paths: repeated
   Cancel/No keeps the Pilot open; confirmed close stops on the same attempt;
   completion is not falsely recorded; Legacy remains authoritative; Target is
   not activated; and a new form does not inherit stale close state.
6. The session and UI declarations explicitly exclude automatic start/retry,
   timers, polling, background work, production mutation, route switching, and
   authority change.

These assertions are stronger than a timing-dependent manual observation for
the specific invariants they cover. They are deterministic and do not require
the workflow to remain active for an operator-sized interval. Accordingly,
automated evidence proves the cancellation and safety invariants independently
of workflow duration, within the tested implementation and isolated
qualification scope.

It does not prove that a human can always reach and operate the cancellation
control during a real active session, that the control remains responsive under
real desktop scheduling and rendering conditions, or that an operator sees and
understands the active/cancelled state in the exact production installation.

## 6. What remains unobserved manually and risk assessment

Unobserved behavior is the human-to-UI timing and control loop: an operator
initiates cancellation while work is visibly active, the control accepts the
input promptly, the UI presents the safe terminal state, and the operator can
return to Legacy. The remaining uncertainty is principally an operator-control
risk, not a demonstrated Production data or authority risk.

| Risk area | Assessment from evidence |
|---|---|
| Production safety | No credible direct Production mutation path is shown in the retained evidence. The Pilot is read-only/isolated and the coordinator declares no production mutation or migration. This is bounded evidence, not proof of all future production integration. |
| Data integrity | Automated cancellation stops the rehearsal and retains checksummed evidence; the tested path does not report false completion or restart. No MQ-07-specific manual before/after database observation exists. |
| Authority/routing | The coordinator and UI tests preserve Legacy authority, keep Target inactive, and report no authority change or Target activation. No credible MQ-07 path to an authority switch was found. |
| Recovery | Cancellation produces a terminal stopped state and prevents restart through the tested coordinator path. Production rollback is separately governed by Phase 9.6 prerequisites and is not supplied by MQ-07. |
| Operator control | A credible residual risk remains: the operator-facing active-session cancellation interaction was not manually observed. A delay, missed input, or unclear state could reduce control over an in-progress rehearsal. The available evidence bounds but does not eliminate this risk. |

The unobserved behavior therefore does not establish a credible Production
safety, integrity, authority, or recovery failure in the current isolated Pilot
boundary. It does establish a credible, limited operator-control uncertainty.
That uncertainty must not be hidden by relabeling MQ-07.

## 7. Governance option A - hard blocker

**Definition:** MQ-07 must be SATISFIED before activation eligibility can ever
become eligible.

**Technical rationale:** This is the strongest interpretation of the original
UI-03 gate and preserves a complete manual proof of operator control. It avoids
accepting any gap between deterministic cancellation semantics and the actual
desktop interaction.

**Operational risk:** It prevents any future activation-eligibility decision
until a controllable active observation and human evidence are available. The
risk is schedule and operational availability, not a direct safety hazard.

**Safety/integrity implications:** It maximizes evidence completeness and
avoids relying on automated evidence for a human-observation assertion. It does
not itself improve the implementation or make the UI more controllable.

**Evidence strength:** Strong for the completeness requirement; conservative
for the residual risk. It gives manual evidence the highest weight, while
underweighting the retained automated proof of the underlying cancellation and
authority invariants.

**Disadvantages:** The active window may be intrinsically too short for a
repeatable human test. Treating duration as a prerequisite could incentivize
changes solely to make qualification observable, even though the current
evidence does not show that Production needs a delay.

**Required compensating controls:** No waiver is available under Option A.
The project would need a valid manual observation method using the reviewed
workflow and final installation context, with no qualification-only behavior
altering the product semantics, plus the existing evidence, authority, and
production prerequisites.

**Effect on Phase 9.6 prerequisite aggregation:** PR-03/PR-05 remain
`NOT_EVALUATED` until MQ-07 is manually satisfied and the corresponding
governance record is complete. The aggregate remains
`NOT_ELIGIBLE_FOR_ACTIVATION_DECISION` while any mandatory prerequisite is not
`SATISFIED`.

## 8. Governance option B - accepted residual limitation

**Definition:** MQ-07 remains BLOCKED as a qualification item, but future
activation eligibility may proceed only through explicit, auditable Project
Owner/Governance acceptance of the residual limitation.

**Technical rationale:** The gap is limited to human timing/desktop
observability. The core cancellation invariant, terminal state, evidence
integrity, no-false-completion behavior, Legacy authority, Target inactivity,
and non-production boundary are independently covered by code-level and
automated evidence. The automated proof does not depend on keeping the
workflow active for a human-sized duration.

**Operational risk:** The residual operator-control uncertainty remains. A
future operator could encounter a cancellation interaction that was not
manually observed in the reviewed environment. This risk is explicit and is
not converted into a PASS.

**Safety/integrity implications:** With the controls below, the evidence
supports bounded residual acceptance without weakening the fail-closed
activation boundary. This option must not waive unrelated production, recovery,
security, authority, routing, or independent-review prerequisites.

**Evidence strength:** Strong for the technical invariants and moderate for the
operator experience. The evidence is sufficient for a technical recommendation
only because the unobserved portion is identified precisely and is not treated
as proven.

**Disadvantages:** It accepts an incomplete manual qualification item and may
be unsuitable for a governance standard that requires direct human observation
for every operator-control gate. It also creates continuing evidence-retention
and expiry obligations.

**Required compensating controls:** A later governance record must name the
decision owner and approver, bind scope and evidence-package/database identity,
state the residual risk, define expiry/review conditions, preserve the exact
MQ-07 BLOCKED wording, and list the retained automated evidence. The package
must require the existing fail-closed aggregation, explicit operator intent,
current build/install identity, isolated or production-like evidence as
otherwise required, immutable audit/receipt binding, and a prohibition on
interpreting eligibility as activation. Any later evidence of cancellation
failure, data mutation, authority change, route enablement, false completion,
or unsafe recovery must invalidate the acceptance and block eligibility.

Making MQ-07 manually exercisable is not a compensating control by itself. The
qualification method must not add an artificial Production delay, alter
Production timing, introduce debug-only behavior into Production, or weaken
workflow semantics. The current automated evidence should be retained as the
duration-independent invariant proof; any future manual method must observe the
existing behavior rather than reshape it for test convenience.

**Effect on Phase 9.6 prerequisite aggregation:** PR-03 remains a record of
MQ-07 as BLOCKED. PR-05 can become `SATISFIED` only after explicit auditable
governance acceptance under this option. Until then, the current Phase 9.6B
result remains `NOT_EVALUATED` for MQ-07 treatment and the aggregate remains
`NOT_ELIGIBLE_FOR_ACTIVATION_DECISION`. Acceptance of this residual limitation
would not make MQ-07 PASS and would not by itself make the aggregate eligible.

## 9. Contradicting evidence search

No reviewed record contradicts the exact Phase 9.5 conclusion that MQ-07 is
BLOCKED and must not be converted to PASS. The C5, C10, Phase 9.5 technical
review, independent-review package, Phase 9.6A freeze, and Phase 9.6B contract
all preserve that status.

There is, however, evidence relevant to the governance choice that must not be
omitted:

- The Phase 9.5B1 plan describes UI-03 as **MANDATORY BLOCKER TO GO** and says
  focused automated cancellation tests existed but no human in-progress
  cancellation was observed (`docs/phase9.5b1-cutover-blocker-closure-plan.md:97,
  181`). This supports Option A as the original operational policy.
- The Phase 9.5 results later record the concrete blocker as qualification-method
  timing, retain automated invariants, and reject artificial production delay
  (`docs/phase9.5c-manual-qualification-results.md:299-303`). This supports
  considering Option B, but does not constitute governance acceptance.
- MQ-08's human close-path PASS and MQ-12's No/Cancel and keyboard/RTL PASS do
  not contradict MQ-07. They test adjacent paths, not cancellation during an
  active observation.
- Automated cancellation tests do not contradict the missing manual result.
  They prove a different evidence class and must remain labeled automated.

The records therefore narrow the question; they do not resolve it by
themselves. No owner or governance approval is invented by this analysis.

## 10. Technical recommendation

**RECOMMEND ACCEPTED RESIDUAL LIMITATION**

This is a technical recommendation, not governance approval. The recommendation
rests on the evidence that the underlying cancellation/safety invariant is
proven independently of workflow duration, while the unobserved behavior is a
specific operator-control observation gap with no confirmed Production safety,
integrity, authority, or recovery failure in the current boundary. The
recommendation is conditional on the explicit, auditable governance package and
compensating controls in Option B. MQ-07 must remain BLOCKED exactly as stated.

No Phase 9.6B prerequisite status is changed by this analysis.

READY FOR PROJECT OWNER GOVERNANCE DECISION
