# Rah_Negar final UI/UX gap audit

Audit date: 2026-09-07. Batch 2 adds code-level UI policy hardening and automated DPI invariants, but this remains not a claim of full visual polish.

## Scope and validation limit

User-reachable forms include Startup, Login, Main, Records, Report Center, Settings, Change Password, Recovery, Password Confirmation, About, Runtime Settings, Live/Pilot, and the Shamsi calendar popup. `BaseForm` now applies common RTL/RTL-layout and baseline control styling on load. `UiMessageService` uses Persian default titles and logs technical exception details instead of presenting raw exception text.

The native WinForms surface was not available to the audit tool, so complete visual inspection at 1920x1080 for 100%, 125%, and 150% could not be completed. Automated tests are evidence of policy/invariants, not visual certification. Above 150% remains blocked by the supported-scale policy.

## Current findings

| ID | Severity | Status | Evidence / next action |
|---|---|---|---|
| UI-01 | CRITICAL | RESOLVED in Batch 1 | Production seed controls/handlers removed and covered by static regression tests. |
| UI-02 | HIGH | OPEN | Real form-by-form visual acceptance remains unavailable; retain screenshots and observations from a native desktop run. |
| UI-03 | HIGH | MITIGATED / VISUAL VALIDATION OPEN | `BaseForm` applies RTL and RTL layout to the user-facing form base; confirm rendering and keyboard order on desktop. |
| UI-04 | HIGH | OPEN | Mixed English/default production copy remains in legacy forms; replace with approved Persian text in a focused UI copy batch. |
| UI-05 | HIGH | OPEN | Legacy event keyboard/focus flow and silent validation paths still need native UI integration acceptance. |
| UI-06 | MEDIUM | OPEN | Fixed pixel regions and compact controls need visual checks, especially at 150%. |
| UI-07 | MEDIUM | OPEN | Grid definitions are still rebuilt in some legacy paths; preserve/cache architecture and benchmark separately. |
| UI-08 | MEDIUM | OPEN | Report-center frequent-combination summary remains unpresented. |
| UI-09 | MEDIUM | OPEN | Chart presentation scope remains unresolved in the legacy report UI. |
| UI-10 | MEDIUM | OPEN | Factory Reset remains a destructive maintenance surface requiring future UX review. |
| UI-11 | LOW | OPEN | Dead recovery handler remains for cleanup. |
| UI-12 | LOW | OPEN | Legacy debug `NULL` message remains for cleanup. |

## Acceptance conclusion

H-05/UI-02 is not closed. Manual observations still required are clipping/overlap, keyboard focus and tab order, font substitution, grid readability, RTL rendering, and dialog placement at 100/125/150% for every user-facing form. See `docs/final/batch2-ui-dpi-acceptance.md` for the retained evidence and exact limitation.
