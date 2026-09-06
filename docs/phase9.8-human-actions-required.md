# Phase 9.8 - Human Actions Required

This checklist contains only actions not completed by the Phase 9.8 technical
evidence collection. Completing an action requires the named person to provide
the stated evidence; no field is pre-completed here.

| Action ID | Person required | Document to open | Exact fields/decision to complete | Evidence/signature required | Blocks activation-decision eligibility? |
|---|---|---|---|---|---|
| H-01 | Deployment/operations administrator with access to the actual installation | `phase9.8-installation-bound-evidence-package.md` | Record actual deployment/station, installed build/version, schema/migration, profile, Unit count, Production DB path/hash/size/last-write UTC, Target identity/equivalence, authority/transition/audit paths and readbacks, and live fence/write-drain evidence | Installation command/readback receipts bound to the real deployment, plus named acknowledgement and UTC timestamp | YES |
| H-02 | Restore operator and physical/records custodian | `phase9.8-physical-restore-custody-record.md` | Identify the retained backup artifact and storage location; complete operator, custodian, accessibility, recovery-time, and physical retention fields | Human restore observation, custody acknowledgement, signature/auditable reference, and UTC timestamp | YES |
| H-03 | Named ShiftProfile operator | `phase9.8-operator-runbook-approval.md` | Complete operator identity/scope and every runbook acknowledgement checkbox, including STOP, ABORT, rollback, `RECOVERY_REQUIRED`, no bypass, routing ordering, and handoff | Operator signature or auditable approval reference and UTC timestamp | YES |
| H-04 | Operational Supervisor | `phase9.8-operator-runbook-approval.md` | Confirm runbook version, operator readiness/training, writer fence/drain responsibility, restore escalation, evidence custody, and all required acknowledgements | Supervisor signature or auditable approval reference and UTC timestamp | YES |
| H-05 | Audit/evidence retention custodian or governance records owner | `phase9.8-installation-bound-evidence-package.md` and `phase9.8-physical-restore-custody-record.md` | Confirm actual audit retention location, immutable/readback controls, access policy, retention period/manifest, and custody responsibility; distinguish technical hash-chain evidence from organizational custody | Retention manifest, access/custody acknowledgement, named owner, signature/reference, and UTC timestamp | YES |
| H-06 | Organizationally independent human reviewer | `phase9.8-independent-review-final-package.md` | Confirm independence, review the exact scope/checklist/evidence, record findings, and select exactly one: PASS, PASS WITH LIMITATIONS, or FAIL | Reviewer identity, independence basis, findings, signature/auditable review reference, and UTC timestamp | YES |
| H-07 | Project Owner / governance authority | `phase9.8-project-owner-decision.md` | Review the completed evidence and human approvals; complete exactly one permitted decision choice and all acknowledgements. Do not treat READY as activation/cutover authorization | Explicit decision, owner comment, signature/reference, and UTC timestamp; no decision is recorded by this package | YES |

## Persistent boundary while actions are pending

Legacy remains **AUTHORITATIVE**. Target remains **NON-AUTHORITATIVE**. Target
routing remains **DISABLED**. Production Activation and Production Cutover
remain **UNAUTHORIZED**. MQ-07 remains **BLOCKED  MANUAL OBSERVATION NOT
PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**.

**Current aggregate: NOT_ELIGIBLE_FOR_ACTIVATION_DECISION.**
