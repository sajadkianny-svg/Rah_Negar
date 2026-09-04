# Phase 9.5B4 - Blocker Closure Report

Status: **PHASE 9.5B4 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**
Date: 2026-09-04
Branch: `phase9-operational-readiness`
Starting commit: `bd4ce86ff87829bcc4466f192a8209748fa30131`
Scope: Phase 9.5B4 only

## 1. Authoritative B4 scope

The Phase 9.5B1 closure plan assigns Phase 9.5B4 the following narrow work:

> Compose ShiftProfile-only target login/session behavior, singleton ManagementCredential proofs for the complete protected-action inventory, bounded target management recovery, vendor-signed ESD execution, durable audit, and explicit removal/isolation of legacy recovery bypass from target authority. Keep all target routes disabled in normal production startup.

The B4 primary gates are `SEC-01`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, and `SEC-08`. B4 also supplies security prerequisites for `BR-03`, `MIG-02`, `MIG-03`, `MIG-04`, `AUTH-03`, and `AUTH-04`; it does not close those supporting gates.

No closure-plan reorder or redesign was made. No B4, B5, B6, B7, or B8 work was started.

## 2. Scope and dependency record

| Item | B4 determination |
|---|---|
| Gate IDs assigned to B4 | `SEC-01`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, `SEC-08` |
| Initial states | All six were `BLOCKED` in the B1 plan and remained `BLOCKED` after B2/B3 pending implementation and evidence. |
| B2 dependencies satisfied | Frozen authority/routing rules; ShiftProfile-only identity rule; singleton ManagementCredential proof contract; complete protected-action inventory; bounded recovery policy; vendor P-256 custody rules; audit allow-list/retention rules; legacy bypass isolation requirement. |
| B3 dependencies satisfied | Existing ManagementCredential-bound backup/restore boundary, exact proof binding foundation, durable audit sink, SQLite target persistence schema, atomic ESD execution boundary, and isolated disposable-database test pattern are available for reuse. No B3 backup/restore boundary was duplicated or bypassed. |
| Unresolved prerequisites | B2 stakeholder approval record; B3 manual isolated qualification and later production binding; approved production provisioning of ShiftProfiles, ManagementCredential, device/public key, and audit custody; human security review; exact production binary/installation evidence. |
| Expected evidence | Positive/negative authentication and station-scope tests; credential version/disable/session behavior; complete action-inventory proof binding; recovery rehearsal; ECDSA P-256 device/key/value/time/replay/exactly-once tests; append-only non-secret audit and failure-atomicity evidence; binary/composition review; isolated manual qualification. |
| Production code permitted | Yes, only target security/composition and directly supporting persistence/audit code behind the inactive boundary. |
| Test code required | Yes, focused regression tests are required for the B4 implementation. |
| Qualification tooling required | No new qualification tooling was necessary. Existing isolated SQLite/test infrastructure was reused. |
| Human/manual qualification required | Yes, isolated target authentication, session/disable behavior, protected-action allow/deny, recovery, ESD success/failure, audit visibility, and security review remain required. |
| Exact completion criteria | The six B4 capabilities exist behind an explicitly inactive target composition; all protected actions use exact singleton ManagementCredential proof; recovery is bounded and atomic with audit; ESD remains signed offline ECDSA P-256 and replay-safe; audit is durable/append-only/non-secret; no legacy recovery bypass is reachable through target composition; Legacy remains authoritative and startup routing is unchanged. |

## 3. Implementation and evidence changes

### Target authentication and session boundary

Added target-only ShiftProfile authentication using the existing `IShiftProfileRepository` and `IShiftProfileCredentialRepository`. Authentication requires an active station-scoped profile and a current one-to-one credential. Password verification is an explicit PBKDF2-SHA256 target verifier with bounded parameter parsing and constant-time comparison. The service creates only a `TargetShiftProfileSession` containing the ShiftProfile ID, station, credential version, and expiry. No user, role, Support identity, or alternate login identity is created.

Authentication and denied attempts are sent to the existing security audit sink using only allow-listed non-secret metadata. Credential-verifier and repository failures fail closed. An expired or absent session cannot be treated as authenticated.

### Protected-action composition

Added `ProtectedActionInventory`, which exposes the existing enum values as the complete inventory without adding or renaming an action. `TargetManagementAuthorizationService` requires an active target session, matching station scope, an action from that inventory, a current active singleton ManagementCredential, and an exact action/scope/correlation/version/time-bound proof. A proof is returned only after the authorization audit entry succeeds. Invalid credentials, invalid scope, missing/expired sessions, and audit failures do not produce a proof.

The existing B3 backup/restore boundary remains the implementation for backup and restore operations. This B4 layer does not implement another restore path and does not directly overwrite a database.

### Bounded management recovery

Added `TargetManagementRecoveryService` and `IManagementCredentialRecoveryBoundary`. Recovery requires the active initiating ShiftProfile session, matching station, correlation and reason, safe human approval/reviewer references, and a one-time secret that is never written to audit metadata. A new singleton credential revision is generated using a fresh salt and PBKDF2-SHA256. The previous revision is not retired by the application service directly.

Added `SQLiteManagementCredentialRecoveryBoundary`, which retires the old revision, writes the new singleton revision, writes the non-secret recovery audit receipt and metadata, and commits them in one SQLite transaction. If the replacement or audit cannot be committed, the transaction rolls back and no new revision is reported. Transient verifier/salt buffers are cleared after persistence handoff. No recovery code, application-derived secret, master password, or new principal is created.

### Vendor ESD authorization and audit composition

The composition accepts the existing `ProtectedEsdAdjustmentExecutionService`, `EcdsaP256VendorAuthorizationVerifier`, and `IAtomicEsdAdjustmentExecutionBoundary`. The existing vendor path remains signed offline ECDSA P-256, public-key-only on the customer side, bound to device/request/action/value/time, replay-safe, exactly-once, and atomic with the ESD mutation. B4 adds no Support login or Support identity and does not change event types.

The target composition descriptor is explicitly `Inactive`: target routes are disabled, Legacy remains authoritative, and legacy recovery is not reachable through the target composition. No production startup, `Program.cs`, existing WinForms login, existing recovery form, or legacy settings route was changed.

## 4. Files changed

| File | Change |
|---|---|
| `Application/Security/TargetSecurityComposition.cs` | Inactive target security composition descriptor; ShiftProfile authentication/session service; PBKDF2 target verifier; complete ManagementCredential protected-action authorization service; bounded recovery contracts/service. |
| `Infrastructure/Security/SQLiteManagementCredentialRecoveryBoundary.cs` | Atomic SQLite ManagementCredential replacement plus append-only recovery audit persistence. |
| `Rah_Negar.Tests/Security/Phase95B4SecurityCompositionTests.cs` | Seven focused B4 tests covering authentication/session, all protected actions and binding, audit fail-closed behavior, recovery, inactive composition, and SQLite recovery persistence. |
| `docs/phase9.5b4-blocker-closure-report.md` | This B4 closure report. |

No SQLite schema, migration registration, production database, production path resolution, normal startup behavior, existing legacy form, or production authority state was modified.

## 5. Focused tests

The focused B4 test class contains 7 tests:

1. Active ShiftProfile authentication creates only a station-bound, expiring session and rejects a wrong password.
2. Every existing `ProtectedAction` requires the singleton ManagementCredential and exact station/action-scope binding.
3. Management authorization fails closed when audit persistence is unavailable.
4. Recovery creates a new singleton revision without recording the secret and rejects unsafe approval references.
5. A rejected recovery boundary does not report a new revision.
6. The SQLite recovery boundary commits credential replacement and its audit receipt in one transaction.
7. The target composition is explicitly inactive and has no activation method or target-reachable legacy recovery entry point.

The tests use in-memory fakes or temporary SQLite databases outside the application `Data` directory. No production database, credential, private key, or authority transition was used.

## 6. Manual qualification required

Manual qualification remains required before any B4 gate can be treated as production-ready. The minimum isolated checklist is:

1. Use a newly generated Rasht or Ramsar qualification fixture outside `Data`; record only safe fixture and station identifiers.
2. Confirm writers are stopped, the target composition remains inactive, and Legacy remains the displayed/authoritative workflow.
3. Qualify active/inactive ShiftProfile login, wrong station, wrong credential, expired session, credential-version change, disable behavior, logout, and restart/session expiry.
4. For every `ProtectedAction`, qualify allow with the exact current ManagementCredential proof and deny for wrong actor, action, scope, correlation, credential version, expiry, disabled credential, and missing session.
5. Qualify recovery with approved human references, verify old revision retirement/new revision availability, verify audit visibility, and inject a persistence/audit failure to confirm no partial rotation.
6. Qualify vendor ESD success, wrong device/request/action/value/time, unknown/retired key, malformed envelope, replay, audit failure, replay-store failure, mutation failure, and exactly-once behavior.
7. Review the final target composition/binary for no RBAC catalog, no Administrator/Engineer/Operator/Viewer/Support identity, no customer private key, no master secret, and no reachable legacy deterministic recovery bypass.
8. Record operator, reviewer, correlation, safe artifact references, timestamps, outcomes, stop conditions, and approvals. Keep secrets, raw signed envelopes, verifier material, and raw database contents out of ordinary evidence.

## 7. Production-only evidence still required

The following evidence cannot be produced honestly from local fixtures and remains unresolved:

- `SEC-01`: exact production ShiftProfile provisioning, installation/session behavior, credential disable/change evidence, and production-bound manual qualification.
- `SEC-02`: production composition for every protected route and exact production ManagementCredential proof evidence.
- `SEC-03`: approved production recovery owners/custody, recovery rehearsal record, and production audit binding.
- `SEC-04`: approved production device/public-key provisioning, reconciled ESD value, and production-bound signed-authorization rehearsal.
- `SEC-05`: production audit retention/custody operation and complete final-binary audit wiring review.
- `SEC-08`: independent final-binary security review proving the legacy deterministic recovery/bypass is unreachable through target authority.

The B3 production-only evidence for `DB-03`, `BR-02`, `BR-03`, `BR-05`, and `BR-06` also remains pending. B4 does not close those gates. No production data was accessed, migrated, restored, replaced, or mutated.

## 8. Gate disposition

### Gates closed

Fully closed for production readiness: **none**. Manual qualification, human approvals, independent review, and exact production binding remain mandatory for all six B4 gates.

B4 local implementation closure recorded for: `SEC-01`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, `SEC-08`.

### Gates remaining

`SEC-01`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, and `SEC-08` remain unresolved for cutover readiness pending the manual and production-only evidence above. Supporting gates `BR-03`, `MIG-02`, `MIG-03`, `MIG-04`, `AUTH-03`, and `AUTH-04` remain outside B4 and unresolved under the documented sequence.

## 9. Safety-boundary verification

- Legacy remains the sole production authority. Target routes remain explicitly inactive.
- No production cutover, authority transition, migration, restore, live replacement, or destructive production operation occurred.
- No automatic authority switch, startup migration, target fallback, or activation inference was introduced.
- Normal operational authentication remains ShiftProfile-only.
- Privileged proof remains the singleton ManagementCredential; it is not a normal login identity.
- No Administrator, Engineer, Operator, Viewer, Support, RBAC catalog, support login, hidden backdoor, universal credential, or master password was introduced.
- Vendor authorization remains offline signed ECDSA P-256 with public verification material only in the customer application.
- Event types remain exactly `START`, `NSD`, `ESD`, and `OH`.
- Finalized historical report snapshots and locks remain immutable.
- Rasht and Ramsar remain the only supported station scope; no station-specific identity logic was added to the shared security composition.
- Qualification data remains isolated from production data and outside the application `Data` directory.
- The B3 managed backup/restore boundary remains authoritative for backup/restore; no duplicate or bypass path was added.
- No commit or push was performed.

## 10. Validation record

| Validation | Result |
|---|---|
| Focused B4 tests | **PASS** - 7 passed, 0 failed, 0 skipped. |
| `dotnet build Rah_Negar.sln -c Release` | **PASS** - 0 errors, 6 existing NU1701 compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp.Views.WindowsForms. |
| `dotnet test Rah_Negar.sln -c Release` | **PASS** - 662 passed, 0 failed, 0 skipped. |
| `git diff --check` | **PASS**. |
| Production data access/mutation | **None**. |
| Production authority change | **None**. |
| Commit/push | **None**. |

## 11. Change classification and next-phase readiness

| Item | Result |
|---|---|
| Production code changed | **Yes** - target security composition and directly supporting recovery persistence only. |
| Test code changed | **Yes** - focused B4 regression tests only. |
| Qualification tooling changed | **No**. |
| Documentation changed | **Yes** - this report. |
| Database schema changed | **No**. |
| Production code wired into normal startup | **No**. |
| Target routes enabled | **No**. |
| Production authority changed | **No**. |

B4 local implementation is ready for its required isolated manual qualification and security review handoff. The next documented closure phase is not started by this report and must not be treated as authorized by B4. Production cutover remains blocked until all documented dependencies and production-only gates are separately closed.

## 12. Exact final status

**PHASE 9.5B4 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**
