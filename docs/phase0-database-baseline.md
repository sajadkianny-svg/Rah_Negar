# Phase 0 Database Baseline

**Baseline ID:** `phase0_baseline_001`  
**Rule:** No production database was opened by the application, migrated, copied, or modified.

## 1. Location and connection assumptions

`SqliteDatabaseHelper.GetDatabasePath()` resolves `Data/db.sys` beneath `AppDomain.CurrentDomain.BaseDirectory`. It ensures the directory exists and opens with `ReadWriteCreate`, pooling enabled, default timeout 10 seconds. Consequently database location varies by executable output/install directory; launching from a new location can create an empty file.

One discovered database artifact was `bin/x64/Debug/net8.0-windows/Data/db.sys`, 131,072 bytes, last modified UTC `2026-08-21T17:06:49.9090955Z`. Baseline SHA-256 is `EB3ECA2C96092888912D23AAFD2B4DBBBC1F25CA13894EB2E39B67B5ED4D2F43`. It is a build-output artifact and is not asserted to be the production database. No `db.sys` was found under source `DataFiles`.

## 2. SQLite configuration

Every helper-created connection executes:

- `PRAGMA journal_mode=WAL`
- `PRAGMA synchronous=NORMAL`
- `PRAGMA foreign_keys=ON`
- `PRAGMA temp_store=MEMORY`

This is positive centralized intent, but future Phase 2 must verify all connections use the helper, foreign keys are active per connection, WAL/SHM are included in operational procedures, and busy/concurrency behavior is tested. Pooling must be closed for exclusive Restore/Migration.

## 3. Static schema discovery

The schema is assembled through startup services and SQL strings rather than a versioned migration package. Discovered tables are:

| Area | Tables |
|---|---|
| Station data | `tbl_data` (Rasht/Ramsar variants selected by station schema) |
| Common operations | `tbl_unique`, `tbl_events` |
| Configuration/runtime | `app_settings`, `unit_runtime_base`, `tbl_recovery` |
| Finalization | `tbl_monthly_lock`, `tbl_monthly_report_header`, `tbl_monthly_report_summary`, `tbl_monthly_report_unique_summary`, `tbl_monthly_report_event_summary`, `tbl_monthly_report_service_summary`, `tbl_monthly_report_unit_event_summary` |

Static Event indexes include unique daily-unique date and indexes over Event date, time, unit, and type. The current `tbl_events` has autoincrement ID and required date/unit/type/time but no approved target audit identity, canonical state constraints, or duplicate uniqueness constraint. Exact deployed columns, triggers, views, row counts, `user_version`, and schema drift require a read-only SQLite inventory tool on a verified copy; no SQLite CLI was installed, so they are not claimed here.

## 4. Migration state

No `PRAGMA user_version`, SchemaVersion entity, ordered migration ledger, migration-package checksum, or explicit application migration manager was found in static search. Startup uses `CREATE TABLE IF NOT EXISTS` and schema-builder services. This is schema initialization/evolution by current code, not the approved future migration mechanism.

Phase 2 must introduce migration governance additively only after target tests exist. Never infer deployed schema solely from source; inventory each approved copy, detect anomalies first, and retain legacy structures/evidence.

## 5. Backup and recovery baseline

Current code uses the SQLite backup API to create a temporary database, then legacy application encryption. Import/Restore decrypts to temporary storage and creates a safety copy. The embedded internal encryption key and exact integrity/atomic-swap guarantees require security validation; this report does not certify current backup as production-ready.

Before every future schema introduction, migration rehearsal, pilot, production migration, Restore, or cutover:

1. stop or coordinate writers and identify database/WAL/SHM;
2. use SQLite online backup or a documented consistent offline copy;
3. record database/deployment/Station identity, application/schema version, timestamp, size, source path category, and operator;
4. compute SHA-256 or stronger over the finalized package and store manifest separately;
5. restore into an isolated directory with the compatible application;
6. run `integrity_check`, `foreign_key_check`, schema inventory, row/control totals, Event/report/finalized samples, and startup smoke test;
7. retain the original read-only and record restore result/duration.

Test copies must come only from approved anonymized/synthetic sources, receive new fixture identity, and remain outside production paths. Copying production data into source control or developer workstations is prohibited.

## 6. Phase 0 conclusion

Database discovery is sufficient to plan safe tooling but not to certify a deployed schema. No database was migrated or intentionally modified. Before Phase 2, perform checksum-verified read-only inventory and restore rehearsal on organization-approved copies.

