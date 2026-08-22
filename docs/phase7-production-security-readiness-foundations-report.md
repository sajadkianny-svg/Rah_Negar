# Phase 7.6 Production Security Readiness Foundations

## Status and non-activation boundary

Status: **implemented and verified as inactive, adapter-oriented foundations**.

Phase 7.6 implements the remaining application contracts required before production authentication or protected-operation integration. Phase 7.5 remains the authoritative identity and authorization baseline: ShiftProfile is the sole normal operational identity, every active ShiftProfile has equivalent normal permissions, ManagementCredential is the deployment-wide privileged credential, ordinary report Finalize is a ShiftProfile workflow, Reopen is management-protected, and vendor support is external to local identities.

Nothing in this phase is registered in `Program.cs`. Production authentication is not enabled, no feature mode is changed, no WinForms file is modified or replaced, and no database is opened, created, migrated, or changed. All new implementations live in the application foundation and tests. There is no production persistence adapter in this phase.

## Implemented files

- `Application/Security/VendorAuthorizationPayload.cs`: versioned canonical payload, deterministic UTF-8 serializer/parser, and signed-envelope codec.
- `Application/Security/VendorAuthorizationVerification.cs`: trusted public-key lifecycle contract and concrete ECDSA P-256 verifier.
- `Application/Security/SecurityProofAndRequestServices.cs`: DeviceId provider, cryptographic request factory, management-proof issuer, and proof validator.
- `Application/Security/ProtectedEsdExecution.cs`: replay contracts, audit allow-list, domain/audit gates, safe results, and atomic consumption/mutation adapter boundary.
- `Application/Security/ExternalVendorSupportAuthorization.cs`: retains the Phase 7.5 boundary and now defines an explicit non-authorizing `Unspecified` action for fail-closed mismatch validation.
- `Rah_Negar.Tests/Security/ProductionSecurityReadinessFoundationTests.cs`: ephemeral test-only signing keys and comprehensive readiness tests.

## Initial audit record

### A. Architecture map

The production entry point remains `Program.cs`, which directly opens the legacy startup or login form. No Phase 7 security service is composed there. Legacy settings, authentication, recovery, record entry, monthly locking, finalization, and reporting remain under `UI`, `Services`, `Data`, and the station-specific `Core` profiles. The inactive target architecture is isolated under `Application`, `Core/Foundation`, and `Infrastructure`; Phase 7.6 changes are limited to `Application/Security`, its test fixture, and this report. Phase 7.5's identity decisions remain authoritative.

The protected ESD foundation has four separable adapter boundaries: trusted-key lookup, device identity, security audit, and atomic replay-consumption/mutation. Cryptographic payload handling and proof/request policy are application services. No WinForms type, database connection, or production composition dependency enters these services.

### B. Build status

The first read-only `dotnet build --no-restore` attempt failed before compilation because stale generated NuGet assets referenced a package cache and Visual Studio fallback folder from another machine/path. An isolated restore using the repository-local package cache regenerated build assets without opening the application or any database. The complete solution then built successfully with zero compiler errors and six NU1701 warnings. The warnings are the same application/test duplicates for transitive `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0`, which restore .NET Framework assets for `net8.0-windows7.0`.

### C. Dependency/package health

The official NuGet feed was queried on 2026-08-22 for direct and transitive vulnerabilities, deprecations, and available updates. Neither project reports a known vulnerable package. The application has no package marked deprecated. Test package `xunit 2.9.3` and its v2 components are marked legacy, with xUnit v3 identified as the alternative. Multiple newer package versions exist, but this phase intentionally performs no package upgrade.

Compatibility and redundancy need separate review: the application references `Microsoft.Data.Sqlite` both as a package and a direct DLL, and also carries `SourceGear.sqlite3`, `SQLitePCLRaw.bundle_e_sqlite3 2.1.11`, `SQLitePCLRaw.core 3.0.3`, and `SQLitePCLRaw.lib.e_sqlite3 2.1.13`. The mixed SQLite ownership/version set and the OpenTK/SkiaSharp compatibility warnings should be resolved only after runtime/native-loading tests; they were not silently changed here.

### D. Confirmed bugs corrected in this phase

#### HIGH — expected payload version/action were caller-shapeable

- File/class: `Application/Security/VendorAuthorizationVerification.cs`, `EcdsaP256VendorAuthorizationVerifier.VerifyAsync`.
- Evidence: the verifier checked only the signed payload version and compared actions only for equality. A directly constructed expected payload with an unsupported version or with both expected and signed action set to `Unspecified` could pass those checks.
- Failure scenario: an adapter bypassing the request factory could expand the verifier beyond the only approved V1 `ChangeEsdAdjustment` contract.
- Fix: require both expected and signed payload versions to be V1 and require both actions to be exactly `ChangeEsdAdjustment` after signature verification.

#### HIGH — management proof correlation was bound but not validated

- File/class: `Application/Security/SecurityProofAndRequestServices.cs`, `ManagementAuthorizationProofValidator`; `Application/Security/ProtectedEsdExecution.cs`, `ExecuteAsync`.
- Evidence: `CorrelationId` existed in the proof, but the validator accepted no expected correlation value.
- Failure scenario: a still-valid proof for the same actor/action/scope/version could be reused for a different ESD request correlation.
- Fix: add an explicit `WrongCorrelation` result and compare the proof correlation with the request context correlation before vendor verification.

#### HIGH — ESD scope was not anchored to the active ShiftProfile Station

- File/class: `Application/Security/ProtectedEsdExecution.cs`, `ProtectedEsdAdjustmentExecutionService.ExecuteAsync`.
- Evidence: the proof scope was compared with a caller-supplied scope, but that scope was not compared with the active profile's `StationId`.
- Failure scenario: a caller could present an otherwise valid proof/context combination for a different Station scope, weakening Rasht/Ramsar isolation at the application boundary.
- Fix: fail with `ShiftProfileScopeMismatch` before proof/vendor validation unless the active ShiftProfile Station exactly matches the requested action scope.

#### MEDIUM — public-key bytes were externally mutable

- File/class: `Application/Security/VendorAuthorizationVerification.cs`, `TrustedVendorPublicKey.SubjectPublicKeyInfo`.
- Evidence: `ReadOnlyMemory<byte>` exposed the private backing array, which advanced callers could recover and mutate.
- Failure scenario: trusted verification material could change after provider construction, producing denial or unintended key substitution within the process.
- Fix: copy on construction and export a defensive copy; also reject non-UTC lifecycle timestamps and retirement at/before activation.

#### MEDIUM — application service depended on a concrete verifier

- File/class: `Application/Security/ProtectedEsdExecution.cs`, constructor/field.
- Evidence: the protected execution service accepted only `EcdsaP256VendorAuthorizationVerifier` despite the requirement for a customer-side verification abstraction.
- Failure scenario: future approved verifiers, interoperability fixtures, or safe failure adapters could not be substituted without changing the execution service.
- Fix: introduce `IVendorAuthorizationVerifier`; the ECDSA verifier implements it and the execution service depends only on the interface.

#### MEDIUM — verifier/domain adapter faults escaped the safe result boundary

- File/class: `Application/Security/ProtectedEsdExecution.cs`, `ExecuteAsync`.
- Evidence: exceptions from an alternate verifier or the domain validator could propagate instead of returning a safe failure category.
- Failure scenario: UI/application callers could receive infrastructure exception details or inconsistent failure handling, although the mutation delegate was not yet called.
- Fix: preserve caller-requested cancellation but convert all other verifier/domain failures to closed, message-free result categories before audit or mutation.

#### CODE QUALITY — test replay store keyed request plus correlation

- File/class: `Rah_Negar.Tests/Security/ProductionSecurityReadinessFoundationTests.cs`, `MemoryConsumedStore`.
- Evidence: the store could accept the same RequestId again under another correlation.
- Failure scenario: the test double did not accurately model globally one-time request consumption.
- Fix: key consumption by RequestId and retain correlation as stored evidence.

#### CODE QUALITY — allow-listed audit metadata remained mutable

- File/class: `Application/Security/ProtectedEsdExecution.cs`, `SecurityAuditMetadataBuilder.Create`.
- Evidence: a mutable `SortedDictionary` was returned through an `IReadOnlyDictionary` interface and could be recovered by down-casting.
- Failure scenario: a later caller could append a non-allow-listed key after validation.
- Fix: return a read-only wrapper whose mutation methods reject changes.

### E. Potential bugs requiring validation

- Clock trust, tolerated skew, and recovery after major local clock correction have no approved product policy.
- Envelope/input size limits and repeated-attempt throttling are not defined.
- The exact crash relationship among durable audit, consumption, setting mutation, and receipt persistence requires SQLite fault-injection validation when production integration is authorized.
- Device replacement, backup/restore, and cloned-installation behavior require an approved DeviceId lifecycle decision.

### F. Incomplete functionality

Production key provisioning, DeviceId persistence, ManagementCredential validation/persistence, append-only audit persistence, and production composition remain intentionally inactive. No remote key retrieval, authentication activation, UI replacement, or feature enablement was added.

### G. Database/schema risks

Phase 7.6 opens and changes no database and adds no migration. Any later consumed-request schema must enforce global RequestId uniqueness, preserve correlation/receipt evidence, and participate in the same transaction as the authoritative ESD mutation. Existing inactive infrastructure from later foundation work is not registered by this phase and does not change that prerequisite.

### H. Performance problems

No performance regression was found in the Phase 7.6 path. Canonical payloads and envelopes are small, and verification performs one key lookup and one framework ECDSA operation. Production integration still needs envelope size limits before parsing.

### I. UI/DPI problems

No production UI or DPI behavior was changed. The existing WinForms surface remains outside this inactive application phase.

### J. Duplication/technical debt

The Phase 7.5 compatibility authorization service and the stronger Phase 7.6 verifier/execution service coexist. This is deliberate for non-activation, but production composition must select only the Phase 7.6 path. SQLite package/native ownership and legacy xUnit are separate technical-debt items.

### K. Prioritized remediation plan

1. Approve V1 interoperability vectors and vendor-side signing/key ceremonies.
2. Approve DeviceId provisioning, clone/restore, and recovery semantics.
3. Implement integrity-protected local public-key and DeviceId providers.
4. Implement non-destructive, globally unique replay/receipt persistence and transactional ESD mutation.
5. Implement append-only audit and singleton ManagementCredential validation/versioning.
6. Add size/throttling, clock, crash/concurrency, provisioning rollback, and external interoperability tests.
7. Resolve package/native ownership and NU1701 warnings in a separate reviewed batch.
8. Perform production composition only behind unchanged-by-default gates after a dedicated readiness review.

## Canonical signed payload V1

The signed bytes are a compact UTF-8 JSON object. V1 uses exactly seven properties in this exact order:

1. `payloadVersion`
2. `deviceId`
3. `requestId`
4. `action`
5. `proposedEsdAdjustment`
6. `issuedAtUtc`
7. `expiresAtUtc`

The current payload version is the string `1`. Action is exactly `ChangeEsdAdjustment`. The proposed decimal is encoded as a JSON string using invariant `G29` formatting. This preserves the exact decimal value while preventing current culture, localized separators/digits, or insignificant trailing zeroes from changing the signed representation. UTC timestamps use seven fractional digits and a literal `Z`: `yyyy-MM-ddTHH:mm:ss.fffffffZ`.

`CanonicalVendorAuthorizationPayloadSerializer` writes properties directly with `Utf8JsonWriter` in the defined order. It does not rely on reflection, dictionary enumeration, current culture, or platform newlines. Parsing is strict: the object must contain exactly the V1 field count; values must have the expected types; action must be a defined enum member; decimal/date formats must parse invariantly; and reserialization must byte-match the original. Reordered or otherwise noncanonical JSON is rejected as malformed even if semantically similar.

The payload contains no ShiftProfile name, PersonnelNo, password, credential verifier, authorization code, signing key, or other secret. Initiating ShiftProfileId and application CorrelationId are retained separately in `VendorAuthorizationRequestContext` as local evidence; they are not silently added to the vendor-signed payload defined by the approved minimum contract.

## Signed authorization envelope

The offline envelope is JSON containing `envelopeVersion`, `keyId`, Base64 canonical `payload`, and Base64 `signature`. Envelope version is independently versioned as `1`. Decoding rejects missing/extra fields, invalid JSON, blank identifiers, invalid Base64, and empty payload/signature. Decode failures map to a safe category and never expose parser or cryptographic exception text.

The envelope contains a public identifier and signature evidence, not a private or shared secret. The raw signed envelope is transient input and is prohibited from audit metadata.

## Signature algorithm and rationale

The concrete verifier uses ECDSA over the NIST P-256 curve with SHA-256. Public keys are supplied in standard X.509 SubjectPublicKeyInfo encoding. Signatures use fixed-width IEEE P1363 `r || s` encoding to avoid DER representation variability.

Ed25519 was preferred, but this net8.0-windows target does not expose a clean built-in Ed25519 application API suitable for this repository without adding and governing a new cryptographic dependency. ECDSA P-256/SHA-256 is a modern asymmetric algorithm implemented by the .NET cryptography stack, requires no new package, and uses library verification semantics. Application code performs no hand-written elliptic-curve or timing-sensitive comparison.

Only public verification material crosses the production contract. No private signing key, master support password, universal code, shared vendor secret, or bypass exists in application source/configuration/contracts. Tests generate an ephemeral P-256 key at runtime, sign only inside the test assembly, and dispose the private key.

## Public-key lifecycle

`ITrustedVendorPublicKeyProvider` performs offline lookup by exact KeyId. `TrustedVendorPublicKey` carries KeyId, defensively copied SubjectPublicKeyInfo bytes, activation UTC, and optional retirement UTC. Lifecycle timestamps must be UTC and retirement must follow activation. At verification time, a key is `NotYetActive`, `Active`, or `Retired`. Only Active keys verify authorizations; unknown, future, and retired keys fail closed.

No remote download or internet dependency exists. A future production provider must read approved, integrity-protected local provisioning data and support controlled overlap/rotation. Key retirement policy currently rejects use at verification time. If the product later permits authorizations signed before retirement to remain valid afterward, that policy must be explicitly approved and separately encoded; it is not assumed here.

Public-key material is not secret, but its authenticity is security-critical. Production provisioning must protect replacement/rollback, validate format/curve, and audit key lifecycle changes under ManagementCredential protection.

## DeviceId architecture

`IDeviceIdentityProvider` supplies one opaque stable deployment/device installation identifier. The application foundation intentionally does not invent a hardware fingerprint: machine serials, network addresses, usernames, station names, and other raw hardware/environment identifiers may be unstable or unnecessarily identifying.

The recommended production implementation generates a high-entropy deployment identifier once during approved provisioning and persists it later in protected local infrastructure. It remains stable across ordinary restarts and upgrades, contains no secret, and is independent of Rasht, Ramsar, station display names, or Unit configuration. Backup/restore and device replacement semantics must be approved before implementing persistence. Deterministic providers are used in tests.

## Request generation

`VendorAuthorizationRequestFactory` accepts no caller-provided nonce. It obtains DeviceId from the injected provider, time from the injected `IClock`, and policy lifetime from constructor configuration. It generates 32 random bytes with `RandomNumberGenerator.GetBytes`, represented as a 64-character lowercase hexadecimal RequestId. This gives each request a fresh cryptographically strong 256-bit nonce and prevents an ordinary caller from reusing a chosen RequestId.

The factory binds DeviceId, action, exact proposed decimal, issue UTC, expiry UTC, and payload version. Initiating ShiftProfileId and correlation ID are stored beside the payload as application evidence. Blank actor/correlation/device values fail. A zero or negative lifetime is rejected; an approved lifetime must be positive.

## Verification state machine

`IVendorAuthorizationVerifier` is the customer-side application abstraction. `EcdsaP256VendorAuthorizationVerifier` is its production-suitable framework-backed implementation and processes input in this order:

1. decode the signed envelope;
2. validate envelope version;
3. parse and canonicalize the payload;
4. validate both expected and signed payload versions;
5. find KeyId and require an active trusted public key;
6. import public SubjectPublicKeyInfo and verify ECDSA-P256/SHA-256 signature over the exact canonical UTF-8 bytes;
7. require the expected and signed actions to be exactly `ChangeEsdAdjustment`, then compare signed and expected DeviceId, RequestId, action, proposed value, issue time, and expiry;
8. reject future-issued payloads and payloads whose expiry is reached;
9. return a presentation/audit-safe category, RequestId, KeyId, and verification timestamp.

Every mismatch fails closed. Result categories distinguish malformed envelope/payload, unsupported versions, unknown/inactive key, invalid signature, wrong bindings, expiry/future issue, and verifier unavailability. Cryptographic/import exceptions are converted to safe failures. Arbitrary exception messages never reach results, UI, or audit contracts.

## Management proof foundation

`ManagementAuthorizationProofIssuer` consumes only `ValidatedManagementCredentialEvidence`; actual credential validation/persistence/login remains deliberately absent. The issuer binds initiating ShiftProfileId, a defined protected action, exact scope, validated ManagementCredential version, issue UTC, expiry UTC, and correlation ID. Lifetimes must be positive.

`ManagementAuthorizationProofValidator` returns explicit safe failures for wrong actor, action, scope, correlation, credential version, not-yet-valid proof, and expired proof. Expiry uses a half-open interval: a proof is invalid when current time reaches `ExpiresAt`. Correlation validation prevents a proof issued for one request from being reused for another. A later production issuer must create evidence only after securely validating the singleton ManagementCredential and current version.

## Replay and exactly-once model

`IConsumedVendorAuthorizationStore` is durable-store-ready and records RequestId plus correlation identity, consumption UTC, and optional safe execution receipt ID. It exposes `IsConsumedAsync` and atomic-style `TryConsumeAsync`. Phase 7.6 supplies only a private in-memory test double and registers nothing. The repository also contains later inactive SQLite foundation work, but Phase 7.6 neither composes nor treats it as production-ready persistence.

The stronger execution path uses `IAtomicEsdAdjustmentExecutionBoundary`. A future SQLite adapter must combine request consumption and ESD mutation in one local transaction or an equivalently safe idempotent receipt protocol. The boundary returns `Executed`, `AlreadyConsumed`, `StoreFailed`, or `MutationFailed` plus a safe receipt. Phase 7.6 explicitly does **not** claim database-level atomicity.

`ProtectedEsdAdjustmentExecutionService` requires, in order: active matching ShiftProfile and Station scope, valid action/scope/correlation/version/time-bound management proof, valid vendor signature and payload bindings, successful domain validation, successful pre-execution audit, and successful atomic adapter execution. The mutation delegate is handed only to the atomic boundary after all earlier gates pass. Replay, store failure, audit failure, domain rejection, invalid management proof, actor/Station mismatch, inactive profile, or vendor failure suppresses the delegate.

## Security audit allow-list

`SecurityAuditMetadataBuilder` accepts only these exact keys: `DeviceId`, `RequestId`, `ProposedEsdAdjustment`, `AuthorizationStage`, `ResultCategory`, `KeyId`, and `CorrelationId`. Values must be nonblank and the returned dictionary is ordinally sorted for stable handling.

All other keys are rejected, which logically prohibits password, password hash, salt, raw signed authorization, private key, recovery code, credential secret, and verifier material. Validated metadata is exposed through a non-mutable dictionary wrapper so callers cannot append fields after validation. The execution service emits the invariant proposed value and non-secret identifiers only. Audit is fail-closed: if the required pre-execution audit write throws, atomic consumption and mutation are not invoked.

## Tests and results

The full suite now contains **290 tests**, all passing; **36** directly exercise the Phase 7.6 readiness foundation. Coverage verifies:

- stable canonical UTF-8 bytes, fixed property order, round-trip strictness, and culture-invariant decimals;
- rejection of noncanonical order, unsupported payload/envelope versions, malformed envelope/payload;
- valid ECDSA P-256 signature, altered signature, wrong public key, unknown KeyId, and retired key;
- verifier abstraction conformance, defensive public-key copying, and invalid key lifecycle rejection;
- wrong DeviceId, RequestId, action, proposed value, and expiry binding;
- expired and future-issued authorizations;
- 128 unique cryptographic RequestIds with no caller nonce input;
- complete management-proof issuance and actor/action/scope/version/time mismatch failures;
- management-proof correlation mismatch and expected-version/action fail-closed behavior;
- audit allow-list acceptance and secret-key rejection;
- exactly-once execution and replay rejection;
- concurrent replay suppression with exactly one mutation;
- correlation/time/receipt evidence in the test-only consumed-request store;
- suppression on inactive profile, management failure, vendor failure, domain failure, audit failure, and replay-store failure;
- suppression on ShiftProfile Station-scope mismatch and rejection of undefined protected actions;
- immutability of validated audit metadata;
- safe suppression when verifier or domain adapters throw, without propagating their exception text;
- absence of production private-key contracts, local Support identity/role, and station-specific security types.

Final non-incremental Debug build and Release build: succeeded with zero errors and the same six pre-existing NU1701 compatibility warnings for transitive OpenTK/SkiaSharp Windows Forms packages. A non-incremental Debug build was used because pre-existing build outputs carried future timestamps and could otherwise mask current test changes. No package was added or upgraded.

## Known limitations

- There is no registered/activated production public-key provider, DeviceId persistence provider, replay/atomic adapter, audit sink, ManagementCredential validator, or composition. Later inactive SQLite foundation files do not constitute Phase 7.6 production activation or proof that all deployment failure modes are resolved.
- Envelope size limits and input throttling are not yet enforced. Production adapters should reject unreasonably large input before decoding.
- Clock trust and tolerated skew are not defined. Current behavior rejects any future issue time and accepts no grace interval.
- Public-key provisioning integrity, rotation ceremony, emergency revocation, recovery, and rollback controls require operational approval.
- Audit-before-execution is enforced, but a durable audit sink and the exact transactional relationship between audit, consumption, and mutation remain to be designed.
- Atomicity is an interface guarantee required of the future adapter, not an implemented database guarantee.
- DeviceId backup/restore/reprovisioning semantics remain a product/security decision.
- The Phase 7.5 pilot and older compatibility service remain inactive; production integration must select the Phase 7.6 verification/execution path rather than composing an older test boundary.

## Production integration prerequisites

1. Approve canonical V1 and create independent vendor/customer interoperability test vectors.
2. Approve ECDSA P-256 key generation/storage on the vendor side and offline public-key provisioning/rotation/revocation procedures.
3. Define DeviceId provisioning, protected persistence, backup/restore, replacement, and recovery semantics.
4. Implement and security-review a local trusted-key provider with configuration integrity protection.
5. Implement a non-destructive SQLite migration for consumed requests/execution receipts and a transactionally atomic ESD consumption/mutation adapter.
6. Implement append-only safe audit persistence and define failure/recovery behavior.
7. Implement singleton ManagementCredential validation/versioning and issue proofs only from validated evidence.
8. Approve ESD range/precision/domain rules and ensure the exact signed decimal is the exact stored value.
9. Add abuse limits, envelope size limits, clock policy, crash/concurrency tests, backup/restore tests, and external cryptographic interoperability tests.
10. Perform a separate production integration phase behind unchanged-by-default gates, then validate rollback without modifying finalized evidence.

## Verification and isolation confirmation

The complete solution builds and all tests pass. `git diff --check` passes. `Program.cs` and all production files under `UI/Forms` and `UI/Startup` retain their pre-phase SHA-256 hashes. No production feature configuration changed. No database command or application was run against a production database. Source/configuration inspection finds no vendor private key, local Support role/profile/login, universal support secret, or master bypass.

ShiftProfile remains the only normal operational identity. `OperationalAction.FinalizeReport` remains normal ShiftProfile authorization; Reopen remains separately management-protected. Post-Wizard ESD mutation remains impossible through the Phase 7.6 service without active ShiftProfile validation, current management proof, valid external vendor signature, exact payload bindings, domain acceptance, successful audit, and one-time atomic execution acceptance.
