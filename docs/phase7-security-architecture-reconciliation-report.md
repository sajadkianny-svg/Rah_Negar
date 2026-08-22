# Phase 7.5 Security Architecture Reconciliation Before Production UI Integration

## Status and scope

Status: **implemented and verified as an inactive architecture/application layer**.

This phase reconciles the Phase 6 security proposals and the Phase 7 UI-neutral pilot with the approved product security model. It does not activate authentication or protected operations in production. `Program.cs`, production WinForms forms, production feature configuration, and production databases were not changed. No migration was run and no database connection was needed for this work.

The implementation is station-neutral. A deployment supplies a Station identity, configurable shifts, and any configured Unit count/names through other product boundaries. The security contracts contain no Rasht, Ramsar, fixed Unit count, or Unit-specific ESD assumption.

## Audit method and baseline

The initial phase was read-only. The solution inventory, project files, active source/test contracts, Phase 6/7 reports, reporting finalization path, settings pilot, production entry point, and production UI boundary were inspected before modification. The complete solution was built before changes. Major paths traced were normal login/session descriptions, protected-settings request/authorization/audit/execution, report finalization authorization/validation/atomic persistence, and legacy production composition isolation.

Baseline build: succeeded with zero compiler errors and six repeated NU1701 compatibility warnings. The warnings arise because transitive `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0` assets resolve from .NET Framework targets rather than `net8.0-windows7.0`; each warning appears for the application and test project. This phase did not change packages.

NuGet health was queried against the official NuGet feed on 2026-08-24. No known vulnerable package was reported in either project. No deprecated application package was reported. Test dependency `xunit 2.9.3` was reported deprecated/legacy with `xunit.v3` as the alternative. Compatibility warnings and dependency redundancy remain production-readiness follow-ups; they were not silently upgraded here.

The repository baseline was already substantially dirty/untracked before Phase 7.5, including the Phase 1–7 foundation tree, tests, and documentation, plus modified solution/project files. Those pre-existing changes were preserved. The active source tree did not contain the Phase 6 identity/persistence classes described by historical reports; only their reports remained. The active security-related pilot was Phase 7.4 under `Application/UI/Settings`. Consequently, Phase 7.5 establishes corrected active contracts without reviving the discarded role/support-profile proposal.

## Architecture map

- `Application/Security/SecurityArchitectureContracts.cs`: sole operational identity, internal credential interpretation, operational and protected action catalogs, management proof, safe audit event, and support-contact presentation provider.
- `Application/Security/ExternalVendorSupportAuthorization.cs`: external challenge data, injected verifier, replay-consumption boundary, post-Wizard ESD authorization service, and exactly-once execution gate.
- `Application/Reporting/Finalization/ProductReportAuthorization.cs`: ShiftProfile authorizer for ordinary finalization and management-proof policy for reopening.
- `Application/UI/Settings/ProtectedSettingsContracts.cs`: UI-neutral protected-settings session and audit contracts identify the actor as `ShiftProfileId`.
- `Application/UI/Settings/ProtectedSettingsUiCoordinator.cs`: enforced ESD sequence: normal session, management prompt, external vendor-support prompt, then one authorized execution.
- `Rah_Negar.Tests/Security/SecurityArchitectureReconciliationTests.cs`: product-model, authorization-binding, replay, secret-exclusion, neutrality, and execution invariants.
- `Rah_Negar.Tests/UI/ProtectedSettingsUiCoordinatorTests.cs`: reconciled pilot stage and bypass tests.
- Phase 6 historical security reports and the Phase 7.4 pilot report: explicit supersession notices prevent obsolete support-profile rules from being treated as target architecture.

## Confirmed contradictions and corrections

### HIGH — local Support identity in Phase 6 reports

Evidence: `docs/phase6-user-authorization-foundation-report.md`, `docs/phase6-protected-settings-support-authorization-report.md`, and `docs/phase6-secure-workflow-integration-report.md` described a support-enabled ShiftProfile, support category, or active matching support profile.

Failure scenario: implementing those documents would create a second privileged local identity/capability and allow vendor support to be modeled as a normal application login.

Correction: those descriptions are marked superseded. No active contract defines a support profile, support login, support role, support permission profile, or role catalog. External vendor authorization is a verification boundary, not an actor.

### HIGH — ManagementCredential could authorize ESD by itself

Evidence: the Phase 6.5 report stated that a non-default ESD change could use management authorization, while the Phase 7.4 coordinator accepted a generic `Authorized` result and executed immediately.

Failure scenario: after Wizard completion, a gateway could return `Authorized` after local management authentication and bypass vendor authorization.

Correction: `EsdAdjustmentAuthorizationService` requires both an applicable management proof and a successful external vendor verification. The Phase 7.4 coordinator rejects invalid stage transitions: initial post-Wizard ESD must request management, management completion must request external vendor support, and only successful external vendor submission may execute. Management-only and direct-authorized paths do not execute.

### HIGH — support request lacked an explicit cryptographic binding contract

Evidence: Phase 7.4 exposed only `DeviceId` and an opaque request-information string. Historical Phase 6 documents described a code provider but not an authoritative signed request model.

Failure scenario: implementations could accidentally verify a reusable code not bound to the device, request, action, proposed value, or expiry.

Correction: `VendorSupportAuthorizationRequest` explicitly carries `DeviceId`, cryptographic nonce/`RequestId`, `ChangeEsdAdjustment`, proposed decimal value, issue time, and expiry. `IExternalVendorSupportAuthorizationVerifier` receives that expected request plus transient signed authorization. The private key remains vendor-side. There is no generator, master password, universal code, hardcoded bypass, or private key contract in the customer application.

### HIGH — replay and exactly-once behavior were presentation-only

Evidence: Phase 7.4 could display a replay failure but had no application replay-consumption boundary.

Failure scenario: the same valid signed authorization could be applied repeatedly or concurrent submissions could execute more than once.

Correction: `IConsumedVendorSupportRequestStore` provides durable implementation points for consumed-request lookup and atomic `TryConsume`. Consumption occurs after successful signature verification and before execution. A failed consume is a replay. `EsdAdjustmentChangeExecutor` calls the mutation only after successful consume, and tests prove a repeated request executes once.

### MEDIUM — normal actor was called SubjectId

Evidence: `ProtectedSettingsSession.SubjectId` and `ProtectedOperationAuditDecision.SubjectId` obscured the approved operational identity.

Failure scenario: later UI/application integration could treat a credential identifier or another subject type as the operational actor.

Correction: both contracts now use `ShiftProfileId`/`InitiatingShiftProfileId`. Presentation and audit contracts identify the authenticated actor as ShiftProfile.

### MEDIUM — ordinary Finalize authorization was ambiguous

Evidence: the generic `IReportFinalizationAuthorizer` did not state whether management was required, while Phase 6 documentation mentioned report-finalization overrides among protected operations.

Failure scenario: UI integration could treat creation of a finalized snapshot as a privileged management override.

Correction: `OperationalAction.FinalizeReport` is available to every active ShiftProfile. `ShiftProfileReportFinalizationAuthorizer` verifies an active actor and matching Station, without accepting or requiring ManagementCredential. `ProtectedAction.ReopenFinalizedReport` is separate and `ReportReopenAuthorizationPolicy` requires a matching short-lived management proof.

### MEDIUM — UserCredential could be read as a second identity

Evidence: Phase 6 reports used phrases such as user identity and credential identity alongside ShiftProfile.

Failure scenario: persistence/UI work could create independent users, usernames, or roles detached from configured shifts.

Correction: active `UserCredential` is internal and 1:1 by `ShiftProfileId`. It stores password verifier metadata, version, state, and update time. It has no independent username, display identity, role, or permission. PersonnelNo on ShiftProfile is the normal login name. The type is intentionally not public/presentation-facing.

### LOW — support contact had no neutral future UI boundary

Correction: `ISupportContactInformationProvider` exposes an optional configured software-support mobile number. No phone number is hardcoded and no production About form is changed.

## Final identity and credential model

`ShiftProfile` is the only normal operational identity. Its stable ID survives supervisor/name/personnel metadata changes. Fields include Station, shift number/name, supervisor names, PersonnelNo, active state, timestamps, and revision. PersonnelNo is the username. All active profiles have the same normal operational authorization; there is no RBAC catalog and no profile kind.

`UserCredential`, if retained in later persistence work, is solely an internal credential record belonging 1:1 to ShiftProfile. A password is represented only by verifier/hash metadata. It cannot independently log in, acquire permissions, or identify an operational actor. Any historical schema draft must be reviewed before activation to enforce this interpretation and prevent duplicate current credentials.

`ManagementCredential` is an internal singleton deployment credential. It has no username, ShiftProfile, normal session, or operational access. It protects profile editing/replacement, protected settings, backup policy/path/deletion, restore, migration, finalized-report reopen, security configuration, integrity repair, sensitive raw import/export, and emergency/recovery operations. Manual Backup is an ordinary authenticated ShiftProfile action.

No new end-user role was introduced. Vendor/programmer support exists entirely outside normal application identities.

## Protected action proof

`ManagementAuthorizationProof` is short-lived and bound to initiating ShiftProfileId, protected action, action scope, management credential version, issue/expiry timestamps, and correlation ID. `AppliesTo` rejects actor, action, scope, or time mismatch. Credential version allows later invalidation when the singleton credential changes.

The proof is evidence of a successful protected authorization decision, not raw credential material. It contains no password or verifier. Production integration must issue it only after validating the current ManagementCredential and must set a conservative lifetime.

## ESD Adjustment flow

ESD Adjustment is one Station/deployment decimal value; no Unit ID appears in its security request. Zero is valid.

During initial Wizard setup, the value is domain-validated and saved without external vendor authorization. After Wizard completion, every actual value change follows:

1. require an active authenticated ShiftProfile;
2. validate an action/scope-bound ManagementAuthorizationProof where protected-settings policy requires management;
3. present DeviceId, fresh cryptographic RequestId/nonce, action, proposed value, and expiration data;
4. accept the signed authorization transiently and pass it to the injected public-key verifier;
5. reject wrong action/value, invalid timing, verifier failure, or previously consumed RequestId;
6. atomically consume the RequestId;
7. audit non-secret authorization evidence;
8. run domain validation and execute once.

The verifier interface is deliberately implementation-neutral so a later approved offline signature format and public-key algorithm can be selected without embedding vendor secrets. The production implementation must itself verify DeviceId, RequestId, action, proposed value, and expiry in the signed payload; the request contract makes all expected bindings explicit. The consumed-request store must be durable and transactionally safe in production.

## Audit model

`SecurityAuditEvent` records initiating ShiftProfileId, action, scope, authorization type, success/failure, timestamp, correlation/request ID, and a dictionary of non-secret value metadata. Appropriate ESD metadata may include a normalized proposed value and DeviceId when policy permits. The Phase 7.4 pilot audit record also distinguishes operational, management, and external-vendor stages.

Passwords, password verifiers/hashes, salts, signed one-time support authorizations/codes, private keys, recovery secrets, and raw credential material are prohibited from presentation and operational audit contracts. Verification results retain only status, safe failure category, RequestId, and verification time.

## Testing and verification results

Final full solution build: **succeeded**, zero errors and six NU1701 warnings described above.

Final full test run: **212 passed, 0 failed, 0 skipped**. Coverage includes:

- ShiftProfile as normal identity and PersonnelNo as username data;
- equivalent authorization across active ShiftProfiles and absence of a role catalog/support login type;
- ordinary Finalize as operational and Reopen as management-protected;
- zero-valid initial Wizard ESD behavior;
- rejection of management-only/direct-authorized post-Wizard ESD paths;
- explicit DeviceId/request/action/value/time binding contract;
- expiry, wrong value, wrong action, replay, and invalid signature outcomes;
- exactly-once successful execution;
- secret exclusion from presentation/audit contracts;
- absence of Rasht/Ramsar assumptions in security contracts;
- management-to-external-support stage enforcement in the Phase 7.4 coordinator.

`git diff --check` passed. `Program.cs` has no Phase 7.5 diff. No file under `UI/Forms` or `UI/Startup` has a Phase 7.5 diff. No production feature configuration was changed. No database file was opened, created, migrated, or modified. Source search found no active Support role/profile/login model. Ordinary report Finalize does not consume a management proof; post-Wizard ESD execution cannot succeed without external verification and one-time consumption.

## Remaining production-integration risks

### HIGH

- No production cryptographic verifier exists yet. Select an approved offline signature algorithm, canonical payload format, trusted public-key provisioning/rotation policy, secure DeviceId derivation, clock/expiry policy, and test vectors before activation.
- No durable atomic consumed-request store is implemented. An in-memory store is test-only. Production must make verification consumption and setting mutation resilient to crash/concurrency, ideally through one transaction or durable execution receipt/idempotency design.
- The historical Phase 6 SQLite security schema is absent from active source and must not be reconstructed verbatim. A future non-destructive migration needs explicit singleton ManagementCredential enforcement and a strict 1:1 ShiftProfile credential relationship.

### MEDIUM

- Production login/session composition is intentionally unchanged. A later integration must map successful PersonnelNo authentication directly to ShiftProfileId and must not expose internal credential records.
- Management proof issuance, lifetime, invalidation, failed-attempt throttling, rotation, backup/recovery, and secure memory handling need approved implementations and operational procedures.
- Audit persistence needs append-only behavior, safe metadata allow-listing, retention policy, integrity protection, and failure semantics. Audit failure must not silently permit protected execution.
- ESD domain validation and persistence remain outside this phase. Validate numeric range/precision and Wizard-completed state immediately before mutation, and bind execution to the same proposed value that was signed.
- Report reopen currently has a policy contract, not a production reopen workflow. Future implementation must preserve finalized evidence, create an auditable transition, and prevent edit-lock bypass.

### LOW / CODE QUALITY

- Resolve the NU1701 transitive compatibility warnings before production qualification and assess whether overlapping SQLite packages/native assets are redundant. Package changes require a separately reviewed batch.
- Plan migration from deprecated xUnit 2 to xUnit v3 separately; it is test infrastructure and not a Phase 7.5 security change.
- Historical reports remain as audit evidence with supersession banners. Future documentation navigation should identify Phase 7.5 as authoritative wherever security is summarized.

## Prioritized remediation plan

1. Approve cryptographic payload canonicalization, signature algorithm, public-key lifecycle, DeviceId definition, expiry/clock behavior, and vendor-side issuance procedure.
2. Design and review a non-destructive security migration with one ShiftProfile per configured shift, exactly one current internal credential per ShiftProfile, one deployment ManagementCredential, append-only audit, and consumed vendor requests/execution receipts.
3. Implement verifier, durable replay store, management proof issuer, and allow-listed audit writer with adversarial and crash/concurrency tests.
4. Validate ESD domain/persistence transaction boundaries and finalized-report reopen transition semantics.
5. Run a separate production-integration readiness review, then integrate behind unchanged-by-default feature gates in small WinForms batches with rollback.
6. Resolve dependency compatibility/deprecation findings without broad framework or package upgrades.

## Production non-activation confirmation

Phase 7.5 is corrective architecture only. It does not register services, change startup, replace forms, enable a feature flag, touch a production database, or change legacy behavior. The contracts and tests are ready for later reviewed adapters; they are not a claim that production security is active.
