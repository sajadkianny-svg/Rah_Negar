# Phase 6.4 Protected Settings and Support Authorization Report

> **Superseded security model:** Local support ShiftProfiles are prohibited. Post-Wizard ESD changes require management authorization plus injected external vendor challenge-response verification. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 6.4 adds isolated security-workflow boundaries for protected settings and support authorization. It does not modify the settings UI, replace the legacy settings service, register authorization in production, or change existing application behavior.

## Protected settings authorization

`IProtectedSettingAuthorizationService` accepts an explicit access classification:

- normal access requires a valid active user credential bound to the requested ShiftProfile;
- protected access additionally requires either a support ShiftProfile or a valid management credential;
- management-required access requires a separately validated management credential.

Results distinguish invalid user credentials, inactive or mismatched profiles, missing protected authority, and missing management authority. `ProtectedOperationType` identifies ESD adjustment changes, security-setting changes, credential management, and report-finalization overrides without calling any legacy setting operation.

## Support authorization

`ISupportAuthorizationService` requires a valid user credential, active matching support ShiftProfile, independently validated management credential, and valid one-time support code. Identity and management checks occur before the code provider is called.

`ISupportCodeProvider` is contract-only. There is no code generator, hardcoded code, SMS integration, network provider, or external dependency.

## Audit integration

Every protected-setting and support-authorization decision creates a `ProtectedAuthorizationAuditEntry`, including failed and successful decisions. Entries contain correlation, timestamp, operation, subject, profile/station, validated management identity, decision, and reasons. Passwords, supplied support codes, hashes, salts, and secrets are excluded.

## Tests and isolation

Tests cover protected-setting denial, management authorization, normal access, successful support authorization, invalid support code, non-support profile rejection, audit creation, and secret exclusion.

`FrmSettings`, `AppSettingsService`, `Program.cs`, production authentication, existing UI behavior, and database behavior remain unchanged.
