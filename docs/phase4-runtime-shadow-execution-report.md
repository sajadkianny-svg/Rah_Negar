# Phase 4.5 — Runtime Shadow Execution Harness Report

**Project:** Rah_Negar  
**Status:** Isolated shadow-runner foundation complete  
**Production activation:** None  
**Database access/schema change:** None  
**Legacy Runtime modification:** None

## 1. Scope and outcome

Phase 4.5 adds an application-layer orchestration harness that joins the Phase 4.4 legacy adapter contract, the Phase 4.2 Runtime calculator, and the Phase 4.3 comparison service. It operates only on an injected read-only input-source abstraction representing an approved database copy. It contains no SQLite reference, connection string, SQL, file write, persistence, UI, Reporting, or production registration.

The runner rejects a source marked as production or writable before loading any Runtime context or invoking the legacy adapter. It executes each distinct requested Unit independently for one common half-open period and returns results in ordinal Unit-id order.

## 2. Architecture

```text
RuntimeShadowExecutionRequest
    - read-only input source
    - Station
    - multiple Units
    - one half-open period
    - execution identity
                |
                v
RuntimeShadowRunner safety gate
    - reject production source
    - reject writable source
    - validate copy identity
                |
                v
For each distinct Unit (ordinal order):
    IRuntimeShadowInputSource.LoadContext
                |
                +--> ILegacyRuntimeAdapter.Read
                |       -> LegacyRuntimeSnapshotNormalizer
                |       -> normalized legacy RuntimeSnapshot
                |
                +--> RuntimeCalculator.Calculate
                        -> RuntimeProjection
                        -> normalized target RuntimeSnapshot
                |
                v
        RuntimeComparisonService.Compare
                |
                v
RuntimeShadowExecutionResult
    - identities and period
    - legacy snapshot
    - new projection
    - comparison result
    - immutable evidence metadata
    - per-Unit status/error
```

## 3. Created files

### Shadow execution foundation

- `Application/Runtime/Shadow/RuntimeShadowSourceContracts.cs`
- `Application/Runtime/Shadow/RuntimeShadowExecutionRequest.cs`
- `Application/Runtime/Shadow/RuntimeShadowExecutionResult.cs`
- `Application/Runtime/Shadow/RuntimeShadowRunner.cs`

### Tests

- `Rah_Negar.Tests/Runtime/RuntimeShadowRunnerTests.cs`

### Documentation

- `docs/phase4-runtime-shadow-execution-report.md`

No existing legacy Runtime, Runtime calculation, comparison, Event, UI, Reporting, database/schema, or startup file was modified by Phase 4.5.

## 4. Boundary contracts

### 4.1 Read-only input source

`IRuntimeShadowInputSource` exposes only:

- immutable `RuntimeDatabaseCopyIdentity` metadata;
- `LoadContext` for one explicit Station, Unit, and period.

It does not expose a database connection, SQL command, mutable repository, transaction, save, update, or delete operation. Its output is an already reconstructed `RuntimeCalculationContext` containing a validated Event Chain and trusted calculation evidence.

### 4.2 Database copy identity

`RuntimeDatabaseCopyIdentity` records:

- CopyId;
- source fingerprint;
- capture timestamp;
- source label;
- whether the source is read-only;
- whether the source is production.

The runner requires nonempty CopyId/fingerprint, `IsReadOnly = true`, and `IsProductionSource = false`. Rejection occurs before `LoadContext` or legacy adapter invocation.

These flags are a foundation contract, not cryptographic proof. The future copy workflow must construct metadata from verified backup/copy custody and enforce filesystem-level read-only access independently.

## 5. Execution flow and result handling

1. Validate request, Station, Unit list, period, and ExecutionId.
2. Validate database-copy identity and reject production/writable sources.
3. Remove duplicate Unit ids and sort remaining ids with ordinal comparison.
4. Load one context per Unit and require exact Station, Unit, PeriodStart, and PeriodEnd agreement.
5. Invoke `ILegacyRuntimeAdapter` using the context EventChainVersion.
6. Normalize raw legacy hours to exact authoritative minutes and require matching identity, period, and Event boundary.
7. Execute the Phase 4.2 calculator without changing its behavior.
8. Normalize the new projection and compare all Phase 4.3 metrics/state.
9. Return a `Match` or `DifferenceDetected` result with both sides and evidence.
10. Isolate per-Unit source, legacy, new-engine, or comparison failures so one unavailable Unit does not corrupt another result.

No result is persisted. A detected difference defaults to the Phase 4.3 `NewEngineDefect` classification until separately supported by approved policy/legacy-defect evidence; the harness does not auto-classify differences.

### Result contents

Each `RuntimeShadowExecutionResult` contains:

- StationId and UnitId;
- half-open period start/end;
- execution status;
- normalized legacy snapshot when available;
- new Runtime projection when available;
- comparison result when completed;
- evidence metadata;
- stable failure code and diagnostic message when execution cannot complete.

Evidence includes ExecutionId, copy identity/fingerprint/capture time, EventChainVersion, BaselineVersion, PolicyVersion, CalculationVersion, and trusted execution timestamp.

## 6. Safety boundaries

- No production database access exists in code.
- Production-marked sources are rejected before any read.
- Writable copies are rejected before any read.
- No SQL or SQLite dependency exists in the Shadow folder.
- No source database write API exists.
- The legacy adapter remains an interface with no production implementation.
- Legacy Runtime is not modified or invoked directly by the harness.
- The Phase 4.2 calculator and Phase 4.3 comparison service are called without modification.
- No UI, Reporting, startup, dependency-injection, or persistence integration exists.
- Errors produce in-memory per-Unit results only.

## 7. Test coverage and results

The in-memory test suite covers:

- successful shadow execution with both snapshots, comparison, and deterministic evidence;
- invalid request rejection before source execution;
- production-source rejection before source or legacy read;
- writable-copy rejection before source or legacy read;
- unavailable legacy capture isolated as a Unit failure;
- numeric comparison difference reported without mutating either result;
- multiple Unit execution with duplicate removal and ordinal deterministic ordering.

All sources and adapters in tests are fakes. No database or filesystem source is opened.

Final verification:

- Whole-solution build: passed, 0 errors.
- Automated tests: 122 passed, 0 failed, 0 skipped.
- Existing warnings: `NU1701` compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms packages.
- Environment warning: `NU1900` because NuGet vulnerability metadata was unavailable in the restricted/offline environment.
- Production database access: none.

## 8. Limitations

1. There is no implementation of `IRuntimeShadowInputSource` or `ILegacyRuntimeAdapter`; real legacy execution remains unavailable.
2. Database copy creation, verification, mounting, access control, and disposal are not implemented.
3. `IsProductionSource` and `IsReadOnly` are trusted metadata at this layer; future infrastructure must enforce and attest them.
4. No batch persistence/export exists. Results live only in memory.
5. A result difference uses the safe default `NewEngineDefect`; evidence-backed reclassification is a separate review step.
6. Cancellation, progress, large-copy performance controls, parallelism, and resumability are deferred. Sequential execution is intentional for deterministic foundation behavior.
7. Unit failures do not stop the batch after the top-level safety gate. Future policy must decide whether critical input-custody failures should stop all remaining Units.
8. Real Rasht/Ramsar comparison remains blocked by the Phase 4.4 legacy mapping gaps, especially period Physical Runtime, period Adjusted Runtime, Final State, and Event boundary identity.

## 9. Future production-copy workflow

The future workflow must remain outside the production database and should proceed as follows:

1. Create a verified backup/copy through the approved backup custody process; never point the harness at the live path.
2. Record source database identity, copy checksum/fingerprint, capture time, application/schema version, Station identity, and responsible operator.
3. Verify the copy independently, then expose it through a sandboxed read-only infrastructure implementation.
4. Build a deterministic Event boundary marker and per-Unit validated Event Chains without changing the copy.
5. Supply trusted Baseline and current configuration evidence for the same source/capture boundary.
6. Capture complete legacy fields through an approved read-only adapter implementation without SQL inside the adapter.
7. Run the harness with an explicit ExecutionId and retained copy/evidence manifest.
8. Export results only through a separately approved non-authoritative evidence writer; never write into the source copy or production reporting tables.
9. Review every `InputMismatch`, unavailable result, or numeric difference and attach approved classification evidence.
10. Retain or dispose of copies according to approved data-custody policy; do not activate target Runtime from shadow results alone.

## 10. Verification conclusion

Phase 4.5 provides orchestration and safety contracts only. It proves deterministic in-memory execution using fakes, but it does not claim a real legacy comparison. Legacy Runtime and the production database remain unchanged, and no existing calculation path has been replaced.
