# Rah_Negar native UI acceptance checklist

This is the human-observation record for H-05. The final product acceptance identity is a user-created generic/profile-driven station profile. Legacy station names are compatibility-fixture concerns only and are not part of this matrix.

Run from the repository root after a Release build. Each command creates a disposable profile through the same supported 3–5 unit profile boundary:

```powershell
foreach ($units in 3,4,5) {
    foreach ($dpi in 100,125,150) {
        powershell -ExecutionPolicy Bypass -File .\Qualification\run-final-ui-acceptance.ps1 -UnitCount $units -DpiPercent $dpi
    }
}
```

Before launching each session, set Windows Display scale to the requested percentage. The harness writes evidence under `Qualification/qualification-run/final-ui-acceptance/generic-<units>-units-<dpi>-<utc>/session-evidence.json` and records `productionDataTouched=false`. Invalid counts 2, 6, and 35 are rejected by the harness and by `TargetStationProfileRules`; do not bypass that boundary.

Qualification login, only for the harness: profile `Qualification Shift`; password `Qualification-9.4C!`. This credential is not part of the product assembly or normal application data. Production authority remains unchanged: Legacy is authoritative, Target is non-authoritative, routing is disabled, and activation/cutover is unauthorized.

For each profile/DPI session, inspect every surface and every dialog it opens. Mark PASS/FAIL only with an observer, date, screenshot or note path, and a short observation. `NOT OBSERVED` is not acceptance.

| Profile / DPI | clipping | overlap | readable text | RTL | controls visible | buttons reachable | grid readable | dynamic/unit layout | navigation |
|---|---|---|---|---|---|---|---|---|---|
| Generic 3 units / 100% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 3 units / 125% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 3 units / 150% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 4 units / 100% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 4 units / 125% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 4 units / 150% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 5 units / 100% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 5 units / 125% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| Generic 5 units / 150% | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |

Required inventory for every row:

- [ ] Startup Wizard / generic profile creation
- [ ] Login
- [ ] Main / Dashboard
- [ ] Operational / Live form
- [ ] FrmRecords
- [ ] Reports
- [ ] Settings
- [ ] Backup
- [ ] Restore
- [ ] Recovery
- [ ] Management authorization dialogs
- [ ] Important confirmation and error dialogs

Operator checks:

- [ ] Tab order follows visual order; Enter activates the intended default action and Escape cancels where appropriate.
- [ ] Dynamic grids show the correct 3, 4, or 5 unit layout without clipping or overlap; cached grid definitions remain stable across repeated visits.
- [ ] Invalid input, duplicate event, locked database, backup/restore rejection, missing configuration, unsupported unit count, read-only path, damaged metadata, and recovery-required states show a short Persian next action without raw exception text or stack trace.
- [ ] Empty, loading, incomplete-data, and finalized/read-only report states are understandable.
- [ ] No production DB, authority metadata, target route, activation artifact, or cutover state is touched.

## Human result

Observer: ____________________  Machine: ____________________  Date: ____________________

Overall result: `NOT EVALUATED` / `PASS` / `FAIL`

H-05 remains OPEN until all nine profile/DPI sessions and the complete inventory have been inspected and retained evidence has been reviewed by a human. This document intentionally contains no self-certified visual PASS.
