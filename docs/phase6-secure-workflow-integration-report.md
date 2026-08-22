# Phase 6.5 Security Integration with Sensitive Workflows Report

> **Superseded security model:** Management authorization alone cannot authorize a post-Wizard ESD Adjustment change, and no local support profile is permitted. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 6.5 adds opt-in authorization adapters for sensitive workflows. It does not connect them to production composition, existing forms, legacy settings services, credential services, or report-finalization execution.

## Secure operation execution

`ISecureOperationExecutor` accepts sanitized operation metadata, a workflow authorization adapter, and an operation delegate. Authorization completes first, the decision is audited next, and only an allowed operation is executed. Denied operations never invoke their delegate. Failure results expose a generic message rather than exception details or secrets.

`SecureOperationRequest` contains correlation, operation, validated credential evidence, ShiftProfile, and timestamp. It contains no password, support code, hash, salt, or secret value. Secure-operation audit entries likewise exclude secrets.

## Workflow adapters

Adapters map sensitive workflows to the Phase 6.4 authorization boundary:

- ESD adjustment changes require protected access;
- security-setting changes require management authorization;
- credential creation, rotation, and disabling require management authorization;
- report-finalization overrides require management authorization.

The adapters verify that the requested operation matches their fixed operation type and fail closed on mismatch.

## ESD adjustment boundary

`IESDAdjustmentAuthorizationService` treats an unchanged zero/default adjustment as normal validated-user access. A non-default change requires management authorization, or the complete support path consisting of an active matching support profile, management credential, and valid one-time support code. The code remains confined to the support-code provider request and is excluded from results and audit records.

## Tests and isolation

Tests cover denied execution, management-authorized execution, authorization/audit/execution ordering, credential create/rotate/disable authorization, the zero/default ESD case, management ESD authorization, support ESD authorization, and secret exclusion.

No UI file, legacy service, `Program.cs`, production authentication registration, existing workflow, or database behavior was changed.
