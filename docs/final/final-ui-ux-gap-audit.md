# Rah_Negar final UI/UX gap audit

Audit date: 2026-09-07. Batch 2 adds code-level UI policy hardening and automated DPI invariants, but this remains not a claim of full visual polish.

## Scope and validation limit

User-reachable forms include Startup, Login, Main, Records, Report Center, Settings, Change Password, Recovery, Password Confirmation, About, Runtime Settings, Live/Pilot, and the Shamsi calendar popup. `BaseForm` now applies common RTL/RTL-layout and baseline control styling on load. `UiMessageService` uses Persian default titles and logs technical exception details instead of presenting raw exception text.

The native WinForms surface was not available to the audit tool, so complete visual inspection at 1920x1080 for 100%, 125%, and 150% could not be completed. Automated tests are evidence of policy/invariants, not visual certification. Above 150% remains blocked by the supported-scale policy.

## Current findings

| ID | Severity | Status | Evidence / next action |
|---|---|---|---|
| UI-01 | CRITICAL | RESOLVED in Batch 1 | Production seed controls/handlers removed and covered by static regression tests. |
| UI-02 | HIGH | MAPPED TO H-05 / HUMAN OPEN | Real form-by-form visual acceptance remains unavailable; retain screenshots and observations from a native desktop run. |
| UI-03 | HIGH | SOURCE COMPLETE / H-05 HUMAN OPEN | `BaseForm` applies RTL and RTL layout to the user-facing form base; native rendering and keyboard order remain H-05 observations. |
| UI-04 | HIGH | RESOLVED SOURCE-LEVEL | Reachable legacy captions/default labels were localized in Batch 3; native rendering remains covered by H-05. |
| UI-05 | HIGH | SOURCE COMPLETE / H-05 HUMAN OPEN | Event keyboard/focus and validation paths have source-level coverage; native integration acceptance remains H-05. |
| UI-06 | MEDIUM | RESOLVED SOURCE-LEVEL / H-05 HUMAN OPEN | BaseForm and the acceptance harness cover the supported 100/125/150% matrix; human observation remains H-05. |
| UI-07 | MEDIUM | RESOLVED SOURCE-LEVEL | Grid definition cache/reuse and focused performance evidence added. |
| UI-08 | MEDIUM | RESOLVED | Frequent-combination summary is now rendered in the Service Analysis page. |
| UI-09 | MEDIUM | RETIRED FROM ACTIVE UI | No chart control or chart button is user-reachable; the unused rendering package was removed. |
| UI-10 | MEDIUM | RESOLVED FOR CURRENT SCOPE | Factory Reset is protected in the service and visibly disabled until the managed recovery workflow exists. |
| UI-11 | LOW | RESOLVED | Dead recovery handler removed. |
| UI-12 | LOW | RESOLVED | Debug `NULL` message removed. |

## Batch 3 evidence update (2026-09-07)

Source-level UI hardening now uses Persian captions, a shared RTL/DPI BaseForm policy, explicit disabled states for unavailable reset/recovery operations, localized report summaries, and standardized error-path messaging. The H-05 harness now prepares the generic/profile-driven 3/4/5-unit matrix at 100%, 125%, and 150%; record the checklist without pre-marking any visual item. Legacy station fixtures remain compatibility-only.

## Acceptance conclusion

H-05/UI-02 is not closed. Manual observations still required are clipping/overlap, keyboard focus and tab order, font substitution, grid readability, RTL rendering, and dialog placement at 100/125/150% for every user-facing form. See `docs/final/batch2-ui-dpi-acceptance.md` for the retained evidence and exact limitation.
