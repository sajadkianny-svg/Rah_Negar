# Phase 9.6F — Activation Readiness Evidence Index

Generated for branch `phase9.6-activation-readiness`, base `1ce7cf0`. Qualification evidence is isolated and non-authoritative; hashes are recorded in the generated result/TRX artifacts when available.

| Evidence ID | Source file | Purpose | Status | Phase | Limitation |
|---|---|---|---|---|---|
| F-01 | `docs/phase9.6b-activation-prerequisite-contract.md` | Mandatory prerequisite definitions and sole MQ-07 exception | CURRENT | 9.6B/F | No generic waiver. |
| F-02 | `docs/phase9.6b2-mq07-project-owner-governance-decision.md` | Owner acceptance of residual MQ-07 treatment | CURRENT | 9.6B2/F | MQ-07 remains BLOCKED; not authorization. |
| F-03 | `docs/phase9.6d1-pre-rehearsal-foundation-results.md` | Authority/recovery foundation | RETAINED | 9.6D1 | Production executor absent. |
| F-04 | `docs/phase9.6d2-startup-commit-fencing-results.md` | Startup, lifecycle, epoch fencing | RETAINED | 9.6D2 | Production drain/fence absent. |
| F-05 | `docs/phase9.6d3-reconciliation-recovery-rollback-results.md` | Read-only reconciliation, recovery, isolated rollback | RETAINED | 9.6D3 | Production rollback absent. |
| F-06 | `docs/phase9.6e-production-like-rehearsal-results.md` and `Qualification/qualification-run/phase9.6f/phase9.6e/phase9.6e-result.json` | Isolated end-to-end rehearsal | PASS; JSON SHA-256 `E995057F5C60E763EB62F8D44C53CF0DFD93AB0A9CFBF6C3F0651EA698EB6687` | 9.6E | Not Production evidence. |
| F-07 | `Qualification/run-phase9.6f-activation-qualification.ps1` | Repeatable isolation and qualification runner | IMPLEMENTED | 9.6F | Does not authorize transition. |
| F-08 | `Qualification/qualification-run/phase9.6f/phase9.6f-result.json` | Harness result, tests, build, diff, pre/post Production state | PASS; SHA-256 `8B8032160C5DF0E5846132A804D5B9B23D5896FD8364506213EDD1CE757BAB2A` | 9.6F | Generated artifact; no Production DB present. |
| F-09 | `Qualification/qualification-run/phase9.6f/phase9.6f-focused.trx` | Focused automated evidence | PASS; SHA-256 `C904B8AA23E2F102DD5A9EFE1986254672AA292FEC15582E170DB9A4D6CB90CF` | 9.6F | Automated evidence is not manual review. |
| F-10 | `docs/phase9.5-ai-assisted-technical-review.md` | MQ-01..12 reconciliation and independence boundary | RETAINED | 9.5/F | AI-assisted review is not independent. |
| F-11 | `Qualification/qualification-evidence/MQ-01.trx`..`MQ-05.trx` | Prior automated MQ evidence | PASS | 9.5/F | Historical evidence; hashes are in F-10. |
| F-12 | `docs/phase9.5-manual-qualification-runbook.md` | Historical manual MQ/DPI evidence and limitations | RETAINED | 9.5/F | No new 9.6F manual observation. |
| F-13 | `Rah_Negar.Tests/Authority/Phase96ERehearsalTests.cs` | Rehearsal lifecycle/failure contracts | PASS | 9.6E/F | Qualification-only. |
| F-14 | `Rah_Negar.Tests/Authority/AuthorityD3Tests.cs` | Reconciliation, abort, rollback, recovery | PASS | 9.6D3/F | Qualification-only. |
| F-15 | `Application/Activation/ProductionActivationBoundary.cs` | Non-authoritative eligibility receipt boundary | REVIEWED | 9.6F | Production activation remains unauthorized. |

MQ-07 exact status: **BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**. Independent Human Review: **NOT PERFORMED / UNAVAILABLE**.
