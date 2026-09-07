# Rah_Negar final product completion plan

This is the shortest practical implementation sequence. It is intentionally grouped by outcome, not by governance phase.

## Batch 1 — remove unsafe active paths and establish one authority boundary

Close C-01 through C-04 and H-01/H-02/H-06/H-07 together where possible:

- remove the Records seed buttons and all test seeding from the product build;
- make the validated complete Event chain/runtime/report path authoritative;
- replace embedded recovery/backup secrets with approved key custody and authenticated, versioned backup format;
- make restore staged, safety-backed, atomic, sidecar-aware, post-validated, and rollback-capable;
- compose ManagementCredential/fail-closed authorization through backup/import/repair/reset and all other protected actions;
- preserve the explicit Production routing/authority block while testing the new path on disposable Rasht/Ramsar databases.

Exit gate: no unresolved CRITICAL/HIGH security, data-integrity, runtime, or authorization defects; legacy-path integration tests pass.

## Batch 2 — installability and persistence lifecycle

Close H-03 and M-08:

- choose one offline x64 MSI or setup EXE technology;
- move DB/WAL/SHM/settings/logs to a documented persistent data root and migrate existing users safely;
- provide publisher/version/icon metadata, Start Menu shortcut, optional Desktop shortcut, uninstall/repair, and upgrade/reinstall preservation;
- verify standard-user operation after elevated installation and interrupted install/upgrade rollback.

Exit gate: fresh, upgrade, reinstall, uninstall, and rollback tests pass without user-data loss.

## Batch 3 — operator UI/UX and performance polish

Close H-05, M-01 through M-07, and L-01/L-02:

- run the actual form inventory at 1920×1080 and 100/125/150%;
- standardize Persian text, RTL, typography, spacing, buttons, focus, tab order, grids, empty/loading/error states, and destructive confirmations;
- remove default/debug labels and complete or retire summary/chart features;
- cache/reuse grid definitions, then measure startup, navigation, report/PDF, scrolling, memory, and disposal behavior.

Exit gate: independent visual/operator acceptance and benchmark evidence pass; no visible CRITICAL/HIGH UI issues.

## Batch 4 — dependency and exhaustive release validation

Re-evaluate the six NU1701 instances through the rendering stack; retain them only with a reviewed compatibility rationale or upgrade in a separate tested change. Then run Release build, all tests, legacy integration/failure-injection suites, installer lifecycle, UI/DPI/RTL acceptance, backup/restore/recovery drills, relevant qualification suites, `git diff --check`, and clean ZIP/package checks only when a separately authorized release task requests packaging.

The next single implementation task should be Batch 1, starting with removal of the exposed test seeding and replacement of the embedded-key legacy backup/recovery path. Do not activate or cut over Production while this work is in progress.
