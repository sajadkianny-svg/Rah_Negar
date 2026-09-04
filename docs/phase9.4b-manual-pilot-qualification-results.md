# Phase 9.4B — Actual Manual Pilot Qualification Results

## Current status

**QUALIFIED WITH LIMITATIONS**

This is the authoritative record of the newest human manual qualification run in
the isolated Phase 9.4C environment. The functional Rasht and Ramsar Pilot
lifecycle observations succeeded, but several checklist paths were not manually
exercised. This status does not authorize production authority cutover, authority
transition, schema change, commit, or push.

## Scope and evidence basis

The results below use only the human observations supplied for this closure. An
automated test is recorded as automated evidence, never as a manual PASS. The two
station scenarios were run separately in the disposable qualification environment:
Rasht with 3 units and Ramsar with 4 units. Both used the explicit login, main-window
Pilot entry, confirmation, Start Observation, Complete Pilot, and Return to current
application flow.

## Historical blocked attempts and defect discoveries

Earlier attempts are retained as history only; their superseded BLOCKED/READY
conclusions are not the current status.

1. The first environment attempt was blocked by missing initialized disposable
   databases, operator setup, station fixtures, and a controlled launch path.
   Phase 9.4C supplied the isolated generator and launcher.
2. A qualification launcher wildcard-copy defect was corrected in the launcher
   before the successful human run. It was not a production-code change.
3. Authentication initially failed because the safe evidence identifier contained
   a prohibited `password` token (`legacy-password-capability`). The identifier was
   corrected to a safe value; authentication differences now complete as observable
   comparison outcomes.
4. Reporting initially failed because a nullable finalized-snapshot checksum was
   converted to an all-zero sentinel before validation. The constructor was corrected
   to preserve `null` while still rejecting malformed supplied checksums.
5. Workflow-row selection initially did not refresh the top detail panel. The live
   view binding was corrected so selection now displays the selected workflow’s
   categorical status, comparison, safe evidence identifier, and UTC timestamp.

The authentication and reporting corrections, including the row-detail correction,
were already present on the branch before this final manual run. No production code
was changed for this closure recording.

## Final Rasht manual qualification — 3 units

Qualification database preparation, login, explicit Pilot entry and confirmation,
read-only preflight, and session creation succeeded. The station displayed as Rasht.
Legacy was visibly identified as the current operational authority, the read-only
banner remained visible, five workflows and all fingerprint versions were visible,
Start Observation was enabled, and Complete/Stop were initially disabled.

After Start Observation, all five workflows executed without failure:

| Workflow | Execution | Comparison | Fingerprint |
|---|---|---|---|
| Authentication | Completed | Difference observed | `auth-fingerprint-v1` |
| Reporting | Completed | Match | `reporting-fingerprint-v1` |
| Runtime/Event | Completed | Match | `runtime-event-fingerprint-v1` |
| Protected Settings | Completed | Difference observed | `protected-settings-fingerprint-v1` |
| Export | Completed | Difference observed | `export-fingerprint-v1` |

The session reached operator review; Complete Pilot and Stop Pilot became enabled,
with no workflow failed or pending. Reporting row selection displayed Reporting,
Match, Completed, Severity None, `live-reporting-evidence`, and a timestamp without
raw exception, SQL, or sensitive detail. Complete Pilot changed the session and
completion statuses to completed and disabled Start/Complete/Stop while retaining
the results. Return to current application returned successfully to FrmMain.

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

Complete Pilot changed the session and completion statuses to completed, preserved
the workflow evidence, and left Legacy authoritative. Return to current application
returned successfully to FrmMain.

## Interpretation of workflow outcomes

Execution success and comparison outcome are separate facts. Authentication
`Difference observed` is an expected, observable Pilot comparison result, not an
execution failure. Protected Settings and Export `Difference observed` results are
also comparison outcomes, not workflow execution failures. Reporting and
Runtime/Event produced `Match` in both Rasht and Ramsar. No workflow was observed to
fail, remain pending, or abort either final lifecycle.

## Safety conclusions

- Pilot remained read-only throughout the observed runs.
- Legacy remained authoritative; no authority cutover occurred.
- No production database or schema mutation occurred.
- The qualification environment remained isolated from the production database.
- No sensitive credential/hash/recovery/connection-string or raw exception/SQL detail
  was displayed in the Pilot UI.

## Checklist reconciliation

Statuses are exactly `PASS`, `FAIL`, or `NOT EXECUTABLE / NOT MANUALLY VERIFIED`.
Where the checklist item is a compound requirement and one required part was not
observed, the item is not marked PASS. The same status applies to both station runs
unless a station-specific note is shown.

| ID | Status | Manual evidence / limitation |
|---|---|---|
| P9.4A-01 | PASS | Rasht and Ramsar launched through ordinary login and FrmMain; Pilot did not open automatically. |
| P9.4A-02 | PASS | FrmMain visibly contained the explicit `Pilot / فقط خواندنی` entry in both scenarios. |
| P9.4A-03 | PASS | Clicking the entry displayed the explicit Pilot confirmation message. |
| P9.4A-04 | PASS | The confirmation explicitly identified read-only Pilot mode and preceded entry. |
| P9.4A-05 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | The No/cancel confirmation outcome was not manually exercised. |
| P9.4A-06 | PASS | Confirmation was accepted and read-only Pilot composition/preflight began without automatic workflow execution. |
| P9.4A-07 | PASS | Pilot/read-only safety labeling and Legacy-authority indication were visible. |
| P9.4A-08 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Independent keyboard-focus and RTL qualification was not fully manually exercised. |
| P9.4A-09 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | The supplied observations do not directly confirm every listed identity, monitoring, rollback, stop-reason, and completion field before Start. |
| P9.4A-10 | PASS | Rasht displayed Rasht with a created session; Ramsar launched its corresponding isolated scenario with a created session. |
| P9.4A-11 | PASS | Read-only preflight displayed ready / connection confirmed before observation. |
| P9.4A-12 | PASS | Legacy was clearly displayed as current operational authority and remained so. |
| P9.4A-13 | PASS | Start was enabled; Complete and Stop initially disabled; Return remained available. |
| P9.4A-14 | PASS | Start Observation was manually invoked and each final scenario completed one controlled observation run. |
| P9.4A-15 | PASS | Both sessions reached operator decision/review after observation. |
| P9.4A-16 | PASS | Exactly five workflows were displayed. |
| P9.4A-17 | PASS | All five rows were completed; none was failed or pending in either final run. |
| P9.4A-18 | PASS | Match/Difference values were visible row by row; differences remained review outcomes. |
| P9.4A-19 | PASS | All five expected fingerprint versions were visible in both scenarios. |
| P9.4A-20 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | The complete monitoring and rollback-field check was not directly documented. |
| P9.4A-21 | PASS | The observed Pilot surface remained read-only and no prohibited write or authority action was offered or executed. |
| P9.4A-22 | PASS | No sensitive credential/hash/recovery/connection-string or raw exception/SQL detail was displayed. |
| P9.4A-23 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Stop was enabled after review, but the Stop operation itself was not exercised. |
| P9.4A-24 | PASS | Complete Pilot was clicked in both scenarios; completed status and Legacy authority were preserved. |
| P9.4A-25 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Return from an active, not-yet-completed session and its confirmation were not exercised. |
| P9.4A-26 | PASS | Return from completed sessions successfully restored FrmMain in both scenarios. |
| P9.4A-27 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Active-session cancellation was not manually exercised. |
| P9.4A-28 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Application shutdown while Pilot was active was not manually exercised. |
| P9.4A-29 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | No manual before/after database comparison evidence was supplied for each lifecycle. |
| P9.4A-30 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Independent 100% DPI lifecycle qualification was not performed/documented. |
| P9.4A-31 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Independent 125% DPI lifecycle qualification was not performed/documented. |
| P9.4A-32 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Independent 150% DPI lifecycle qualification was not performed/documented. |
| P9.4A-33 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Rasht functional lifecycle succeeded, but the item also requires Stop and complete station-scenario checks; Stop was not exercised. |
| P9.4A-34 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | Ramsar functional lifecycle succeeded, but the item also requires Stop and complete station-scenario checks; Stop was not exercised. |
| P9.4A-35 | NOT EXECUTABLE / NOT MANUALLY VERIFIED | The supplied record does not establish a complete sanitized screenshot/run-log evidence package for every checklist row. |

Counts: **PASS 19; FAIL 0; NOT EXECUTABLE / NOT MANUALLY VERIFIED 16**.

## Automated and repository validation evidence

The correction commits already on the branch include focused regression coverage for
the authentication safe-evidence identifier, nullable reporting checksum, live
Rasht/Ramsar reporting observations, session completion, and workflow-row detail
binding. Automated coverage is supporting evidence only and does not change the
manual checklist statuses above. Final build/test counts for this closure are recorded
after the requested validation commands are run.

## Final decision

The functional final manual Pilot lifecycles for Rasht 3-unit and Ramsar 4-unit
scenarios are qualified with limitations. Phase 9.4 is **not fully qualified** under
the checklist’s every-applicable-row acceptance rule because Stop, active cancellation,
active shutdown, complete database before/after evidence, and independent 100%/125%/
150% DPI visual checks were not all manually performed. No production authority
transition is authorized.
