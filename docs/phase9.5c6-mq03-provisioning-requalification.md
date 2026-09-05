# Phase 9.5C6 - MQ-03 Provisioning Requalification

Status: **SUPERSEDED BY C7 - C6 ARCHITECTURE FIX RETAINED, UNIT BOUNDARY CORRECTED**

Date: 2026-09-05
Branch: `phase9-operational-readiness`
Starting commit: `0123d1058fda6cb195ce0220a8ea020dfa4fbe3f`

C7 supersession: C6 correctly removed station-name branching and made the
supplied Station Profile authoritative for unit count. Renewed human review
then found an independent generic boundary defect: C6 allowed 1 through 35,
while the locked current product boundary is 3 through 5 inclusive. The C7
implementation and evidence are recorded in
`docs/phase9.5c7-mq03-unit-boundary-remediation.md`.

This phase does not authorize production cutover, migration, restore, Target
authority, or production-data mutation. Legacy remains authoritative and all
Target routes remain disabled.

## 1. Confirmed defect

Manual MQ-03 evidence review found a production application-layer architecture
defect in `Application/Provisioning/TargetProvisioningContracts.cs`:

- `TargetStationCode` enumerated Rasht and Ramsar as the support catalog;
- `TargetStationScopeRules.IsSupported` accepted only those named values; and
- `TargetStationScopeRules.ExpectedUnitCount` derived 3 units for Rasht and 4
  units for Ramsar.

This made a station name/code a behavioral selector for production support and
provisioning shape. It contradicted the locked architecture in which the Setup
Wizard supplies station identity, Station Profile, unit count, field
definitions, and the resulting dynamic operational structure. Rasht/3 and
Ramsar/4 are qualification and legacy fixture data only.

Severity: **HIGH**. Failure scenario: an otherwise valid arbitrary station or
profile could not be provisioned, while unit count was silently coupled to two
legacy names. Recommended and implemented fix: remove the station enum/support
catalog and derive validation exclusively from the supplied profile/package.

## 2. Narrow provisioning-path audit

The audit covered all references to `TargetStationCode`, `TargetStationScopeRules`,
`ExpectedUnitCount`, Rasht, and Ramsar in the Application and Infrastructure
provisioning path.

| File / member | Finding | Disposition |
|---|---|---|
| `Application/Provisioning/TargetProvisioningContracts.cs` / `TargetStationCode` | Named support catalog; confirmed behavioral selector | Removed |
| Same / `TargetStationScopeRules.IsSupported` | Rasht/Ramsar support predicate | Removed |
| Same / `TargetStationScopeRules.ExpectedUnitCount` | Rasht=3/Ramsar=4 unit-count switch | Removed |
| Same / package, manifest and validator | Carried the enum and consumed the switch result | Replaced with supplied profile identity/count |
| `Infrastructure/Database/Provisioning/SQLiteTargetStationProvisioningBoundary.cs` | No Rasht/Ramsar or station-code branch; SQL uses supplied station/unit/profile IDs and package collections | Retained unchanged |

Post-change search finds no `TargetStationCode`, `TargetStationScopeRules`, or
`ExpectedUnitCount` in Application or Infrastructure, and no Rasht/Ramsar text
in either provisioning directory. Broader legacy and qualification UI code was
not redesigned in this narrow provisioning correction.

## 3. Generic profile-driven replacement

The production contract now uses:

```text
TargetStationProfileProvisioningDefinition
  ProfileId
  UnitCount
```

`TargetStationProvisioningPackage` receives this definition. Manifest
validation requires a safe opaque profile ID and compares the package Unit
collection and unit numbers with `StationProfile.UnitCount`. C6 incorrectly
defined the independent generic product range as 1 through 35; C7 corrects the
named policy constants to 3 through 5. No station string or enum participates
in either decision.

The safe manifest now exposes `StationProfileId`, `ProfileUnitCount`, a
`StationProfiles=1` count, and a profile fingerprint. It continues to omit raw
passwords, salts, password verifiers, personnel numbers, private keys, and raw
public verification material. Unit/profile shape is part of the manifest
fingerprint, so a reviewer can bind the supplied profile to the dynamic
provisioning structure without disclosing sensitive material.

No database schema was changed. The target `Stations` and `Units` schema was
already generalized, and the explicit-path SQLite boundary was already
package-driven. Field-definition storage remains owned by the upstream Station
Profile/setup architecture; this correction neither invents a second field
registry nor changes the target schema.

## 4. Qualification fixture and synthetic proof

The existing Rasht 3-unit and Ramsar 4-unit tests remain, but their shapes are
now passed as fixture data to the same generic package builder. They are not
production support declarations.

C7 replaces the incorrect 35-unit success proof with arbitrary-name profiles at
the valid minimum and maximum: 3 and 5 units provision successfully. Separate
arbitrary-name profiles at 2, 6, and 35 units are rejected before provisioning,
with explicit proof that no target rows are created. Neither station nor profile
is named Rasht or Ramsar.

## 5. Negative transactional evidence

The earlier MQ-03 TRX had 7 passing tests but no explicit assertions for the
required conflicts. The remediated suite adds:

| Case | Required issue | Transaction/preservation proof |
|---|---|---|
| Supplied profile count differs from Unit collection, then a valid smaller profile conflicts with an existing larger Unit mapping | `unit-count-mismatch`, then `unit-mapping-conflict` | Invalid input leaves all 11 provisioned target tables empty; persisted conflict rolls back a probe Event and preserves all five existing Units |
| Existing Event ID has different immutable record content | `event-record-conflict` | A new valid probe Event is inserted in the same transaction; conflict causes rollback; probe is absent and original Event remark remains unchanged |
| Existing finalized snapshot ID has different canonical payload/checksum | `snapshot-record-conflict` | Probe Event is rolled back; snapshot count remains one and original canonical JSON is unchanged |
| Existing finalized lock identity has different actor content | `lock-record-conflict` | Probe Event is rolled back; lock count remains one and original actor identity is unchanged |

These tests exercise the real SQLite transaction boundary after the original
package has been provisioned. The probe makes rollback observable rather than
merely showing that `INSERT OR IGNORE` left the conflicting row unchanged.

Existing coverage remains for:

- Rasht 3-unit and Ramsar 4-unit fixtures;
- redacted manifests;
- profile and Unit counts;
- trusted runtime baseline manifest entries;
- transactional provisioning and `AlreadyProvisioned` idempotency;
- cross-station Unit mapping rejection;
- conflicting ESD rejection;
- immutable finalized snapshot and lock preservation;
- singleton current ManagementCredential; and
- device identity and trusted vendor public-key metadata.

Event validation remains restricted to `START`, `NSD`, `ESD`, and `OH`.

## 6. Qualification harness and generated TRX

`Phase95B5ProvisioningTests` now carries the trait
`Qualification=MQ-03`. `Qualification/run-readiness-qualification.ps1` selects
that trait for MQ-03 rather than relying only on a class-name substring. This
keeps the qualification evidence set explicit while preserving the existing
MQ-01, MQ-02, MQ-04, and MQ-05 filters.

Command executed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\run-readiness-qualification.ps1 -SkipPreparation
```

Generated evidence:
`Qualification/qualification-evidence/MQ-03.trx` (ignored local evidence; not a
production artifact).

Exact TRX counters:

| Total | Executed | Passed | Failed | Skipped/not executed | Error | Timeout | Aborted | Inconclusive |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 16 | 16 | 16 | 0 | 0 | 0 | 0 | 0 | 0 |

The current C7 TRX contains the two Rasht/Ramsar manifest cases, two
Rasht/Ramsar idempotent provisioning cases, arbitrary-name 3/5 success and
2/6/35 no-mutation rejection proofs, cross-station rejection, disabled-route
proof, ESD conflict, and the four required C6 explicit negative cases.

## 7. Validation record

| Validation | Result |
|---|---|
| Baseline Release build before editing | PASS - 0 errors, 12 NU1701 warnings |
| NuGet vulnerability audit | No known vulnerable packages from configured sources |
| NuGet deprecation audit | Existing `xunit` 2.9.3 reported Legacy; no package changed |
| Focused provisioning tests | PASS - 16 passed, 0 failed, 0 skipped |
| Readiness harness MQ-03 | PASS - 16 passed, 0 failed, 0 skipped |
| Other harness suites | MQ-01 3/3, MQ-02 7/7, MQ-04 4/4, MQ-05 10/10 passed |
| Final Release build | PASS - 0 errors, 6 pre-existing NU1701 warnings |
| Full solution tests | PASS - 706 passed, 0 failed, 0 skipped |
| Generated MQ-03 TRX inspection | PASS - exact 16/16 counters and expected test names confirmed |
| Production database access/mutation | None |
| Production authority or route change | None |

The NU1701 warnings concern transitive OpenTK 3.1.0, OpenTK.GLControl 3.1.0,
and SkiaSharp.Views.WindowsForms 3.119.0 compatibility with net8.0-windows.
They predate and are unrelated to this narrow remediation.

## 8. Preserved safety architecture

- Legacy remains the sole production authority.
- Target routes remain explicitly disabled and non-mutating.
- No production cutover, migration, restore, or database access occurred.
- ShiftProfile remains the sole normal operational identity.
- Singleton ManagementCredential remains privileged proof, not a login role.
- No RBAC roles, Support identity, backdoor, universal credential, or master
  password was introduced.
- ESD authorization and offline trusted vendor public-key boundaries were not
  weakened or bypassed.
- Finalized snapshots and locks remain immutable; provisioning uses insert,
  exact verification, and rollback, never update/delete.
- Event types remain exactly START, NSD, ESD, and OH.
- No startup registration, route activation, path discovery, or production
  mutation capability was added.

## 9. Files changed

| File | Change |
|---|---|
| `Application/Provisioning/TargetProvisioningContracts.cs` | Removed station-name/code behavior; added generic profile definition and profile manifest evidence; C7 corrects its named count policy to 3..5 |
| `Rah_Negar.Tests/Provisioning/Phase95B5ProvisioningTests.cs` | Retains all C6 evidence; C7 replaces 35-unit success with arbitrary 3/5 success and 2/6/35 no-mutation rejection cases |
| `Qualification/run-readiness-qualification.ps1` | Selects MQ-03 by explicit qualification trait |
| `docs/phase9.5-manual-qualification-runbook.md` | Superseding C6 MQ-03 review procedure and 12-test evidence requirements |
| `docs/phase9.5c-manual-qualification-results.md` | Records defect, remediation, exact validation, and pending human disposition |
| `docs/phase9.5c6-mq03-provisioning-requalification.md` | This remediation and requalification record |

## 10. Resulting MQ-03 status and next action

MQ-03 is **READY FOR RENEWED HUMAN/OPERATOR REVIEW**. Automated remediation
does not mark the manual item PASS.

Next action: an operator and independent reviewer should inspect the regenerated
16-test MQ-03 TRX, verify the profile-derived manifest and arbitrary-name 3/5
success plus 2/6/35 rejection proofs, confirm each transactional conflict/preservation assertion, record safe
references and UTC sign-off, and only then assign the manual MQ-03 result.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**
