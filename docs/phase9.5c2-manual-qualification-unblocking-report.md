# Phase 9.5C2 Manual Qualification Unblocking and Execution Enablement

## 1. Objective

Resolve the 12 Phase 9.5C manual-qualification blockers without production
cutover, authority change, production data access, or fabricated manual PASS.

## 2. Initial 12-item BLOCKED inventory

| ID | Gates | Initial reason | Classification |
|---|---|---|---|
| MQ-01 | DB-03, BR-02, BR-03, BR-05, BR-06 | Manual evidence review unavailable | D — missing evidence capture mechanism |
| MQ-02 | SEC-01..05, SEC-08 | Manual evidence review unavailable | D |
| MQ-03 | MIG-03, MIG-04, RT-01 | Manual manifest review unavailable | D |
| MQ-04 | MIG-02, MIG-05 | Manual receipt review unavailable | D |
| MQ-05 | AUTH-03, AUTH-04, MIG-06, SEC-05 | Manual JSONL review unavailable | D |
| MQ-06 | UI-02, UI-06 | Native desktop surface unavailable | D/E — capture and execution instructions |
| MQ-07 | UI-03, UI-06 | Native desktop surface unavailable | D/E |
| MQ-08 | UI-04, UI-06 | Native desktop surface unavailable | D/E |
| MQ-09 | UI-05, UI-06 | Native desktop surface unavailable | D/E |
| MQ-10 | UI-05, UI-06 | Native desktop surface unavailable | D/E |
| MQ-11 | UI-05, UI-06 | Native desktop surface unavailable | D/E |
| MQ-12 | UI-06 | Native desktop surface unavailable | D/E |

No item was classified as a production-code defect, missing UI entry point,
missing qualification data, missing launcher, or production-only requirement.
The prior desktop limitation was an environment/evidence limitation, not a
confirmed application defect. Human action not yet performed is not a defect.

## 3. True unblocking decision

All 12 items move to READY TO EXECUTE NOW. MQ-01 through MQ-05 have a
consolidated command and sanitized evidence manifest. MQ-06 through MQ-12 have
the existing station launcher and deterministic runbook steps. None is marked
manual PASS or FAIL.

## 4. Tooling identified and changes made

Added `Qualification/run-readiness-qualification.ps1`. It validates isolated
paths, prepares both synthetic station databases, runs the five existing
focused suites, and writes `qualification-evidence/MQ-01.trx` through
`MQ-05.trx` plus `readiness-manifest.json`. It records no passwords, hashes,
private keys, raw SQL, stack traces, or production values. A PowerShell overload
issue found during smoke testing was fixed in the path guard.

Updated `Qualification/README.md` and the runbook/results documentation with
the consolidated operator path, evidence rules, cleanup, and C2 status.

## 5. Qualification commands

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\run-readiness-qualification.ps1
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Rasht
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar
```

The first command produced 3, 7, 7, 4, and 10 passing support tests for
MQ-01..MQ-05 respectively. UI launch must be performed by the human operator
on a desktop session.

## 6. Evidence and operator execution contract

For MQ-01..MQ-05, inspect the corresponding sanitized TRX and the receipts or
descriptors required by the runbook; record station, UTC time, reviewer,
outcome, and evidence reference. Screenshot is not required for these service
reviews; hashes/receipts are required where the runbook says so.

For MQ-06..MQ-12, launch each station from the command above, use only the
synthetic qualification profile, follow the exact runbook state sequence, and
capture the required state screenshots and before/after disposable-database
hashes. DPI items must be run independently at 100%, 125%, and 150%, restoring
the prior scale afterward. Stop, cancel, close, and confirmation-No outcomes
must be observed, not inferred from tests. Cleanup is limited to generated
`Qualification/qualification-data`, `qualification-run`, and
`qualification-evidence` directories.

## 7. Status counts

| Status | Count |
|---|---:|
| READY TO EXECUTE NOW | 12 |
| EXECUTED PASS | 0 |
| EXECUTED FAIL | 0 |
| BLOCKED | 0 |

The five automated support suites executed successfully, but remain pending
manual review. No item is PRODUCTION-ONLY. Production-only pre-cutover items
remain explicitly outside C2 and untouched.

## 8. Validation

- Production code changed: **NO**.
- Test code changed: **NO**.
- Qualification tooling changed: **YES** — one consolidated PowerShell harness and README.
- Focused qualification result: **PASS**, 31 support tests passed, 0 failed.
- Full build/test: **NOT RUN after documentation/script-only changes**.
- `git diff --check`: **PASS**.
- Known package warnings remain the existing NU1701 compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp.Views.WindowsForms; no dependency changes were made.

## 9. Human observation still required

Human desktop observation and screenshots remain required for MQ-06 through
MQ-12. A reviewer must inspect and sign off MQ-01 through MQ-05. Automated tests
must not be converted to manual PASS without that evidence.

## 10. Exact next manual operator actions

1. Run the consolidated harness and inspect `readiness-manifest.json` and all five TRX files.
2. For each station, run the launcher and complete MQ-06, MQ-07, MQ-08, and MQ-12.
3. Repeat the lifecycle independently for MQ-09 at 100%, MQ-10 at 125%, and MQ-11 at 150% DPI.
4. Capture the runbook evidence, obtain independent reviewer sign-off, and update the result rows to PASS or FAIL only after observation.
5. Clean up generated qualification directories; do not proceed to production evidence or cutover.

## 11. Authority state and final status

Legacy remains authoritative. Target is non-authoritative, inactive, and
routing-disabled. No production database, production path, real credential,
migration, restore, activation, commit, or push was used.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C2 UNBLOCKING COMPLETE  READY FOR HUMAN MANUAL QUALIFICATION**
