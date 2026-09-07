# Rah_Negar Pilot RC1 — Release Notes

Release name: **Rah_Negar Pilot RC1**  
Release version: **9.9.0-rc1**  
Release class: **Pilot / Pre-Production release candidate**  
Owner: **Sajad Kiyani**

## Scope

This package is approved for bounded Pilot / Pre-Production use. The current
production scope remains the Rasht and Ramsar stations. Station profiles remain
isolated: Rasht is three units and Ramsar is four units. The supported generic
Unit boundary is **3–5 inclusive**; 2, 6, and 35 are rejected.

## Major capabilities

- Local-first WinForms desktop operation backed by SQLite; no cloud service and
  no external AI dependency.
- Initial setup for station profile, data start date, unit runtime baselines,
  and a local login password.
- Main daily observations at the established odd-hour schedule:
  01, 03, 05, 07, 09, 11, 13, 15, 17, 19, 21, and 23.
- Daily unique values, event entry, monthly reporting, finalization, and
  protection of finalized months according to the current application rules.
- Report calculations preserve min/max/average for applicable main data and
  sum for applicable daily unique values.
- Encrypted local backup/export and restore/import with profile compatibility
  checks.
- Authority metadata validation, fail-closed startup behavior, write fencing,
  and tamper-evident audit-chain support.
- DPI-aware UI coverage at 100%, 125%, and 150%.

## Authentication and data location

The first-run setup password is stored locally as a PBKDF2-SHA256 derived hash
with a random salt. Application data is created beneath the deployed App folder
at `Data\db.sys`; the package ships without operational data or credentials.

## Authority safety model

Legacy remains **AUTHORITATIVE**. Target remains **NON-AUTHORITATIVE**. Target
Routing is **DISABLED**. Production Activation and Production Cutover are both
**UNAUTHORIZED**. The package does not expose an activation authorization
artifact or enable Target routing.

## Qualification status

- Pilot / Pre-Production release: **APPROVED**.
- Production Activation: **NOT_ELIGIBLE_FOR_ACTIVATION_DECISION**.
- Current validation baseline: **759/759 tests passed**, normal Release build
  **0 errors**, and **6 known NU1701 warnings**.
- MQ-07 remains **BLOCKED — MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE,
  WITH AUTOMATED INVARIANT EVIDENCE RETAINED**.
- Independent Human Review remains **NOT PERFORMED / UNAVAILABLE**.

## Pilot-only boundary

This release candidate must not be used as evidence of Production Activation,
Production Cutover, Target authority, or live Production installation. Legacy
is the operational reference for the Pilot.

