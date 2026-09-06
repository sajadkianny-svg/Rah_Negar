# Phase 9.8 - Operator Runbook Approval Record

Status: **AWAITING OPERATOR/RUNBOOK APPROVAL**

Reference: [`docs/phase9.6g-operator-cutover-runbook.md`](phase9.6g-operator-cutover-runbook.md)

This record prepares the approval evidence for the existing future controlled
runbook. It does not approve or execute Production Activation or Cutover.

## Approval fields

| Review or acknowledgement | Reviewer/name | UTC date/time | Signature/reference | Status |
|---|---|---|---|---|
| Operator review | Not recorded | Not recorded | Not recorded | AWAITING |
| Operational supervisor review | Not recorded | Not recorded | Not recorded | AWAITING |
| Project Owner acknowledgement | Not recorded in this Phase 9.8 record | Not recorded | Not recorded | AWAITING |

## Required operator understanding

The reviewer must explicitly confirm each item against the referenced runbook.

- [ ] STOP conditions understood.
- [ ] Abort procedure understood.
- [ ] Rollback decision tree understood.
- [ ] `RECOVERY_REQUIRED` procedure understood.
- [ ] No manual database flag editing.
- [ ] No bypass, hidden credential, or generic override.
- [ ] Routing ordering understood: Target routing cannot precede committed and verified Target authority.
- [ ] Authority handoff ordering understood: canonical authority commit precedes route enablement and Legacy de-authority verification.
- [ ] Evidence capture, retention, and handoff responsibilities understood.
- [ ] Current Legacy-authoritative, Target-non-authoritative, Target-routing-disabled state understood.

## Evidence limitation

The Phase 9.6G runbook is prepared and Phase 9.7 documents its technical
compatibility. The repository contains no new operator review, supervisor
review, training confirmation, Project Owner acknowledgement, signature, or
approval timestamp for Phase 9.8. A prepared runbook is not an approved
runbook.

**Final status: AWAITING OPERATOR/RUNBOOK APPROVAL.**

