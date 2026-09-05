# Phase 9.5C9 - MQ-11 150% DPI Pilot Dashboard Remediation

Status: **CODE REMEDIATION COMPLETE - PENDING MANUAL REQUALIFICATION**

## Finding

MQ-11 required this remediation after manual qualification observation at
1920x1080 with Windows scaling at 150%;
the Pilot Dashboard was fully usable and correctly rendered when maximized, but
its normal window size did not expose the complete dashboard vertically.

## Remediation

The exact Pilot observation dashboard form is
`Rah_Negar.UI.Forms.Pilot.FrmLivePilot` in
`UI/Composition/Pilot/FrmLivePilot.cs`. Only this form now sets
`WindowState = FormWindowState.Maximized` in its constructor. Main Form remains
fixed and non-maximizable. No layout redesign, production activation/cutover,
authority switch, data mutation, or change to RTL, DPI, read-only Pilot, or
qualification isolation behavior was introduced.

## Automated evidence

The focused UI/Pilot tests passed 75/75 with 0 failures and 0 skips. The added
regression test verifies the Pilot default state, Main Form constraints, and
the existing no-activation/no-authority-switch boundaries.

MQ-11 is **PENDING MANUAL REQUALIFICATION**. Automated tests must not be
promoted to manual PASS.

## Next manual action

Using the Release build, repeat MQ-11 at 1920x1080 / Windows scaling 150%.
Confirm the maximized Pilot Dashboard exposes the complete vertical dashboard,
the fixed-grid core remains usable without forbidden horizontal scrolling or
header wrapping, and RTL/read-only behavior remains correct. Capture the
required screenshots and reviewer evidence, then record the manual result.

**PRODUCTION CUTOVER REMAINS NOT AUTHORIZED.**
