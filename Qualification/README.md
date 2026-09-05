# Phase 9.5C2 qualification environment

This directory contains only qualification tooling. Generated SQLite files belong
in disposable directories outside the application `Data` directory and are not
production data.

Run the consolidated local readiness harness from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\run-readiness-qualification.ps1
```

It prepares both station fixtures, runs MQ-01 through MQ-05 support suites, and
writes sanitized TRX files plus `qualification-evidence/readiness-manifest.json`.
The manifest marks service-level items READY TO EXECUTE NOW; it never promotes
automated results to manual PASS. For MQ-06 through MQ-12, use
`launch-qualification.ps1 -Station Rasht` or `-Station Ramsar`, then follow the
runbook's human-observation steps and capture screenshots in the evidence folder.
Never place output under the application's `Data` directory. Remove only the
generated `qualification-data`, `qualification-run`, and `qualification-evidence`
directories after review.
