# Phase 9.4 Final Manual Pilot Qualification and Closure Report

## Final decision

**QUALIFIED WITH LIMITATIONS**

Phase 9.4’s final functional manual Pilot lifecycles succeeded for both supported
station scenarios in the isolated qualification environment. The decision remains
limited because Stop, active cancellation, active application shutdown, complete
manual database before/after evidence, and independent 100%/125%/150% DPI visual
qualification were not all performed. The Phase 9.4A checklist therefore does not
permit an unqualified `QUALIFIED` decision.

This report is the authoritative Phase 9.4 closure report. It does not authorize
production authority cutover, authority transition, migration, schema change,
production activation, commit, or push. Readiness for Phase 9.5 is recorded only as
a gated follow-up decision below; Phase 9.5 is not started by this report.

## Objective

To manually qualify the explicitly invoked, read-only Pilot lifecycle against the
real local Windows desktop application for the current production scope: Rasht with
3 units and Ramsar with 4 units. The qualification covered login, explicit entry and
confirmation, preflight, Start Observation, five workflow observations, operator
review, Complete Pilot, and return to the current Legacy application.

## Qualification environment

Phase 9.4C supplied disposable, local-only Rasht and Ramsar SQLite qualification
databases and isolated copied Release application launch paths. The environments
were kept separate from the production executable/data path. The qualification
database preparation and both isolated launches completed successfully. The human
tester used the qualification-only login and the prepared 3-unit Rasht / 4-unit
Ramsar fixtures. No external service or production cutover tooling was used.

## First blocked attempts

The first Phase 9.4 attempt lacked initialized disposable databases, operator setup,
station fixtures, and a controlled launch path. Phase 9.4C addressed those preparation
blockers. A separate launcher wildcard-copy issue was also corrected before the final
manual run. These historical blocked conclusions are superseded by the final manual
observations and are not the current status.

## Defects discovered and corrected

Three qualification defects were recorded and corrected in commits already present on
the branch:

1. Authentication safe evidence identifier defect: an evidence identifier containing
   `password` was rejected by the safety filter, causing an otherwise valid
   authentication observation to be treated as invalid evidence. The identifier was
   changed to a safe non-sensitive identifier. Authentication `Difference observed`
   now completes as a comparison result, not a workflow failure.
2. Reporting nullable finalized-snapshot checksum defect: an absent finalized
   snapshot checksum was converted to an all-zero sentinel before validation,
   invalidating both live reporting observations. The constructor now preserves
   `null` and continues to reject malformed supplied checksums.
3. Workflow-row detail binding defect: selecting a workflow row did not refresh the
   top detail panel. The binding was corrected so the selected workflow’s label,
   execution/comparison status, severity, safe evidence reference, and UTC timestamp
   are displayed.

No production-code change was made for this closure task, and no schema or database
change was made.

## Final Rasht manual qualification — 3 units

Preparation, qualification login, FrmMain, explicit `Pilot / فقط خواندنی` entry,
confirmation, session creation, and read-only preflight succeeded. Rasht was shown as
the station. Legacy was clearly shown as the current operational authority. The
read-only banner, five workflows, fingerprint versions, and initial control states
were visible. Start Observation was enabled; Complete and Stop were initially
disabled.

| Workflow | Execution | Comparison | Fingerprint |
|---|---|---|---|
| Authentication | Completed | Difference observed | `auth-fingerprint-v1` |
| Reporting | Completed | Match | `reporting-fingerprint-v1` |
| Runtime/Event | Completed | Match | `runtime-event-fingerprint-v1` |
| Protected Settings | Completed | Difference observed | `protected-settings-fingerprint-v1` |
| Export | Completed | Difference observed | `export-fingerprint-v1` |

All five workflows completed; none failed or remained pending. The session reached
operator review, and Complete Pilot and Stop Pilot became enabled. Selecting
Reporting displayed Reporting, Match, Completed, Severity None,
`live-reporting-evidence`, and a timestamp. No raw exception, SQL, or sensitive detail
was displayed. Complete Pilot produced completed session and completion statuses,
retained the results, and disabled Start/Complete/Stop. Return to current application
returned successfully to FrmMain.

## Final Ramsar manual qualification — 4 units

The isolated Ramsar environment launched successfully. Qualification login, FrmMain,
explicit Pilot entry and confirmation succeeded. After Start Observation, all five
workflows completed without failure:

| Workflow | Execution | Comparison | Fingerprint |
|---|---|---|---|
| Authentication | Completed | Difference observed | `auth-fingerprint-v1` |
| Reporting | Completed | Match | `reporting-fingerprint-v1` |
| Runtime/Event | Completed | Match | `runtime-event-fingerprint-v1` |
| Protected Settings | Completed | Difference observed | `protected-settings-fingerprint-v1` |
| Export | Completed | Difference observed | `export-fingerprint-v1` |

Complete Pilot produced completed session and completion statuses and retained the
workflow evidence. Legacy remained authoritative. Return to current application
returned successfully to FrmMain.

## Five-workflow interpretation

Execution state and comparison result are distinct. Authentication differences are
expected, observable Pilot comparison results, not execution failures. Protected
Settings and Export differences are likewise comparison outcomes, not execution
failures. Reporting and Runtime/Event produced Match in both station scenarios. No
workflow failed in either final manual run.

## Workflow-row detail verification

In Rasht, selecting Reporting changed the top detail panel to Reporting and showed:
Comparison `Match`, Execution `Completed`, Severity `None`, evidence
`live-reporting-evidence`, and a timestamp. The UI did not expose raw exception/SQL or
sensitive detail. This verifies the corrected row-selection binding behavior.

## Complete and Return-to-Legacy verification

Complete Pilot was manually clicked in both station scenarios. Both sessions became
completed, completion became completed, workflow evidence remained visible, and the
action states became terminal. Return to current application was then clicked and
successfully restored FrmMain in both runs. Legacy remained available and authoritative.

## Production isolation, read-only, and authority evidence

- Pilot remained read-only.
- Legacy remained authoritative throughout.
- No authority cutover occurred.
- No production database or schema mutation occurred.
- The qualification environment remained isolated.
- No credential, password, hash, recovery secret, connection string, raw SQL, raw
  exception, or sensitive detail was displayed in the Pilot UI.

## Automated regression, build, and test evidence

The branch already contains focused regression coverage for the authentication safe
evidence identifier, nullable reporting checksum, live Rasht/Ramsar reporting paths,
session completion, and workflow-row detail binding. These tests are supporting
evidence and are not represented as manual checklist PASS results.

The final requested validation was run after saving both closure documents:

- `dotnet build Rah_Negar.sln -c Release`: PASS, 0 errors; existing NU1701 warnings
  were reported.
- `dotnet test Rah_Negar.sln -c Release`: PASS, **652 passed, 0 failed, 0 skipped;
  652 total executed tests**.
- `git diff --check`: PASS, no whitespace errors.

## Remaining manual limitations

The following were not fully manually exercised in this qualification session:

- Stop Pilot after an active successful observation;
- active-session cancellation;
- application shutdown while Pilot is active;
- independent visual DPI qualification at 100%, 125%, and 150%;
- a complete manually supplied before/after database comparison package for each
  lifecycle;
- a complete sanitized screenshot/run-log package traceable to every checklist row;
- the confirmation No/cancel path and independent keyboard/RTL qualification.

These limitations are recorded as `NOT EXECUTABLE / NOT MANUALLY VERIFIED` in the
reconciled 35-item record at
`docs/phase9.4b-manual-pilot-qualification-results.md`. No limitation is converted
to PASS from source inspection or automated testing.

## Phase 9.5 readiness decision

Phase 9.4 provides evidence that the scoped read-only Pilot lifecycle is functionally
usable for Rasht and Ramsar, but it does not provide complete manual acceptance under
the Phase 9.4A checklist. Phase 9.5 may be considered only as a separately authorized,
gated follow-up after the remaining manual limitations are explicitly dispositioned
and any required governance approval is obtained. This report does not begin Phase
9.5 and does not authorize production authority transition.

