# Rah_Negar final product completion plan

This is the shortest practical implementation sequence. It is intentionally grouped by outcome, not by governance phase.

## Batch 1 — remove unsafe active paths and establish one authority boundary

Close C-01 through C-04 and H-01/H-02/H-06/H-07 together where possible:

- remove the Records seed buttons and all test seeding from the product build;
- make the validated complete Event chain/runtime/report path authoritative;
- replace embedded recovery/backup secrets with approved key custody and authenticated, versioned backup format;
- make restore staged, safety-backed, atomic, sidecar-aware, post-validated, and rollback-capable;
- compose ManagementCredential/fail-closed authorization through backup/import/repair/reset and all other protected actions;
- preserve the explicit Production routing/authority block while testing generic 3/4/5-unit profiles on disposable qualification databases.

Exit gate: no unresolved CRITICAL/HIGH security, data-integrity, runtime, or authorization defects; legacy-path integration tests pass.

Batch 1 implementation status (2026-09-07): C-01 through C-04 and H-01, H-02, and H-06 are resolved with focused regression coverage. Production activation and cutover remain unauthorized.

## Batch 2 — installability and persistence lifecycle

Close H-03, H-04, H-07, and M-08:

- choose one offline x64 MSI or setup EXE technology;
- move DB/WAL/SHM/settings/logs to a documented persistent data root and migrate existing users safely;
- provide publisher/version/icon metadata, Start Menu shortcut, optional Desktop shortcut, uninstall/repair, and upgrade/reinstall preservation;
- verify standard-user operation after elevated installation and interrupted install/upgrade rollback.
- keep Target composition explicitly inactive with a below-UI write boundary and regression tests.

Exit gate: fresh, upgrade, reinstall, uninstall, and rollback tests pass without user-data loss.

Batch 2 implementation status (2026-09-07): H-03 and H-04 are resolved; H-07 is resolved as inactive composition. The installer lifecycle and clean qualification wrapper passed. H-05 remains open because the native desktop visual acceptance surface was unavailable.

## Batch 3 — operator UI/UX and performance polish

Close H-05, M-01 through M-07, and L-01/L-02:

- run the actual form inventory for generic 3/4/5-unit profiles at 1920×1080 and 100/125/150%;
- standardize Persian text, RTL, typography, spacing, buttons, focus, tab order, grids, empty/loading/error states, and destructive confirmations;
- remove default/debug labels and complete or retire summary/chart features;
- cache/reuse grid definitions, then measure startup, navigation, report/PDF, scrolling, memory, and disposal behavior.

Exit gate: independent visual/operator acceptance and benchmark evidence pass; no visible CRITICAL/HIGH UI issues.

## Batch 4 — dependency and exhaustive release validation

Batch 3 implementation status (2026-09-07): M-01 through M-07 and L-01/L-02 are technically closed. The source-level UI policy/copy cleanup, report summary, cache reuse, malformed-settings handling, bounded redacted logging, and qualification performance evidence are implemented and covered by focused tests. L-03 is also closed by removing the unused ScottPlot rendering reference; the Release solution build now has zero warnings. H-05 remains open only for human/native visual observation using `Qualification/run-final-ui-acceptance.ps1` and `docs/final/final-native-ui-acceptance-checklist.md`.

Re-evaluate dependency health only if new packages are introduced, then run Release build, all tests, legacy integration/failure-injection suites, installer lifecycle, UI/DPI/RTL acceptance, backup/restore/recovery drills, relevant qualification suites, `git diff --check`, and clean ZIP/package checks only when a separately authorized release task requests packaging.

The next task before final offline installer delivery is the human/native H-05 acceptance run, followed by the already-defined full release validation. Do not activate or cut over Production; Legacy remains authoritative and Target routing remains disabled.
