# Phase 9.5C7 - MQ-03 Generic Unit-Count Boundary Remediation

Status: **AUTOMATED REMEDIATION COMPLETE - READY FOR RENEWED HUMAN/OPERATOR REVIEW**

Date: 2026-09-05

**PRODUCTION CUTOVER IS NOT AUTHORIZED.** Legacy remains authoritative, all
Target routes remain disabled, and no production data or authority was used.

## Confirmed defect

Phase 9.5C6 correctly removed `TargetStationCode` and the Rasht/Ramsar
unit-count switch. The provisioning package became generic and profile-driven:
the supplied `StationProfile.UnitCount`, not a station name, determines the
station shape.

Renewed human review found an independent defect in that generic validation.
`TargetStationProfileRules` allowed 1 through 35 units, and MQ-03 treated a
synthetic 35-unit station as success. Generic/profile-driven does not mean an
unlimited unit count. The locked current product boundary is 3 through 5 units
inclusive.

Severity: **HIGH**. A supplied profile outside the supported product boundary
could pass validation and provision an unsupported target shape.

## Remediation

The existing generic named policy remains the single source of truth:

```text
TargetStationProfileRules.MinimumUnitCount = 3
TargetStationProfileRules.MaximumUnitCount = 5
```

`TargetStationProvisioningManifestBuilder.Validate` applies this policy to the
supplied profile and emits `profile-unit-count-out-of-range` when it fails. The
SQLite boundary returns `InvalidManifest` before opening a provisioning
transaction, so rejected profiles cannot partially mutate the target database.
No Rasht/Ramsar branch, SQLite schema change, route activation, or broad
architecture change was introduced.

## Qualification evidence

`Phase95B5ProvisioningTests` now proves:

- an arbitrary synthetic station with a supplied 3-unit profile succeeds;
- an arbitrary synthetic station with a supplied 5-unit profile succeeds;
- arbitrary synthetic stations with supplied 2-unit, 6-unit, and 35-unit
  profiles are rejected with `profile-unit-count-out-of-range`;
- each boundary rejection returns no manifest and leaves all provisioned target
  tables empty; and
- Rasht 3-unit and Ramsar 4-unit shapes remain fixture inputs to the same generic
  package builder.

The qualification class retains the C6 negative and safety evidence for
unit-count mismatch/persisted mapping conflict, Event conflict rollback,
finalized snapshot conflict rollback, finalized lock conflict rollback, ESD
conflict, cross-station mapping, idempotency, manifest redaction, and inactive
Target routes.

The regenerated ignored evidence file is
`Qualification/qualification-evidence/MQ-03.trx`.

| Counter | Result |
|---|---:|
| Total/executed/passed | 16 / 16 / 16 |
| Failed/skipped | 0 / 0 |
| Error/timeout/aborted/inconclusive/not executed | 0 |

## Validation record

| Validation | Result |
|---|---|
| Focused provisioning tests | PASS - 16 passed, 0 failed, 0 skipped |
| Readiness qualification harness MQ-03 | PASS - 16 passed, 0 failed, 0 skipped |
| Other harness suites | MQ-01 3/3, MQ-02 7/7, MQ-04 4/4, MQ-05 10/10 passed |
| Full Release solution build | PASS - 0 errors, 6 pre-existing NU1701 warnings |
| Full solution tests | PASS - 706 passed, 0 failed, 0 skipped |
| MQ-03 TRX inspection | PASS - exact 16/16 counters and expected boundary cases confirmed |

The existing NU1701 warnings concern OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and
SkiaSharp.Views.WindowsForms 3.119.0 compatibility. No package was changed.

## Disposition and next action

MQ-03 is **READY FOR RENEWED HUMAN/OPERATOR REVIEW**. Automated remediation does
not mark it manual PASS.

An operator and independent reviewer should inspect the regenerated 16-test
MQ-03 TRX, confirm the supplied-profile 3/5 success evidence, verify the 2/6/35
rejections and zero-mutation assertions, and recheck every preserved negative
case. Record safe evidence references, UTC review time, and the manual outcome.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**
