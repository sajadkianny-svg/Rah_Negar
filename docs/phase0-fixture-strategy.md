# Phase 0 Legacy Fixture and Safety Strategy

## 1. Principles

Fixtures must be synthetic or derived from formally approved, anonymized copies. Never copy a production database into the repository, ordinary developer storage, test output, or CI. Remove names, personnel identifiers, paths, credentials, recovery values, and organizational secrets; replace deployment/database identity so a fixture cannot be restored as production accidentally.

Each fixture has immutable source checksum, generator/mapping version, Station, schema/application version, purpose, expected control totals, expected anomalies, and approved expected outputs. Store small non-sensitive fixtures in a future test-fixture area; keep restricted large fixtures in access-controlled offline custody with a manifest in the repository.

## 2. Fixture sets

**Event database copies:** Rasht and Ramsar variants covering empty history, valid start/stop chains, duplicate and same-time Events, invalid types/times, U4 normalization, edit/delete downstream effects, finalized-month conflicts, optional Events without daily observations, and legacy identity loss. Keep an immutable legacy source and create a fresh working copy per run.

**Report data:** complete/incomplete days and months; exactly 12 odd-hour observations; duplicate/missing hours; daily-unique present/missing; min/max/average and daily-unique sum controls; Events/service days/extreme dates; live and finalized legacy outputs; stale-finalization and mixed snapshot/live examples.

**Runtime comparison:** trusted baselines; Running/Stopped transitions; OH during valid stopped state and invalid Running+OH; RuntimeAfterOH; stopped-state ESD adjustment and invalid ESD; pre-range history; no prior Event; range crossing day/month/year; edit/delete replay; deterministic same-time ordering.

**Persian calendar boundaries:** Esfand 29/30 as applicable, Farvardin 1, leap/non-leap years, month/year transitions, Gregorian conversion round trips, midnight/23:59 boundaries, `data_start_date` before/on/after, and canonical integer date ordering.

**Finalized periods:** complete finalized month with immutable PDF/snapshot/checksum; incomplete finalization rejection; locked Event/data edit rejection; Reopen/supersession; legacy finalized reports whose values intentionally remain legacy evidence; affected adjacent/range reports.

## 3. Expected-result and comparison policy

Expected results come from approved rules, not automatic capture of buggy legacy output. Label each comparison as preserved behavior, confirmed defect correction, or unresolved discrepancy. Store calculation/source/configuration version, query scope, ordered inputs, output checksum, and tolerance only where numerical policy explicitly permits it. Never hide row loss or business-rule differences behind aggregate tolerances.

## 4. Safe working procedure

1. Obtain data-owner approval or generate synthetic data.
2. Copy to isolated staging; hash before transformation.
3. anonymize and scan for secrets/identifiers.
4. verify SQLite integrity/foreign keys and inventory schema/counts.
5. assign fixture identity and read-only golden checksum.
6. run tests only on disposable child copies in unique temporary directories.
7. compare rows, Event chains, runtime, reports, finalized artifacts, and audits.
8. destroy temporary copies under approved retention while retaining manifests/results.

No fixture tool may use `ReadWriteCreate` against an unresolved application output path. A guard must reject known production paths and require explicit fixture identity before writes.

## 5. Feature-switch fixtures and rollback

Create paired legacy/new expected outputs for shadow-read comparison. Do not dual-write until authority, failure atomicity, and reconciliation are specified. Each activation rehearsal starts from a verified fixture backup, records switch/configuration version, and proves switch-back with the prior application/schema pair. Post-switch writes are reconciled explicitly, never silently discarded.

