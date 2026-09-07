# Phase 9.8 - Installation-Bound Evidence Package

Status: **COMPLETE FOR PRODUCTION-LIKE QUALIFICATION**

REAL PRODUCTION INSTALLATION EVIDENCE: **NOT AVAILABLE**.

This package records a deliberately isolated, non-authoritative deployment
created for qualification and evidence. It is explicitly **PRODUCTION-LIKE
QUALIFICATION DEPLOYMENT - NOT PRODUCTION**. No real Production installation,
Production database, Production authority, routing, activation, or cutover was
created or changed.

## Control boundary

Legacy = **AUTHORITATIVE**; Target = **NON-AUTHORITATIVE**; Target Routing =
**DISABLED**; Production Activation = **UNAUTHORIZED**; Production Cutover =
**UNAUTHORIZED**. The qualification deployment does not alter that boundary.

## Production-Like Qualification Deployment

| Field | Proven value | Evidence |
|---|---|---|
| Classification | `PRODUCTION-LIKE QUALIFICATION DEPLOYMENT - NOT PRODUCTION` | `Qualification/qualification-run/phase9.8-production-like-deployment/Evidence/deployment-manifest.json` |
| Deployment root | `D:\Projects\RahNegar_SQLite\Rah_Negar\Qualification\qualification-run\phase9.8-production-like-deployment` | deployment manifest |
| Qualification identity | `phase9.8-qualification-generic-r3` | `App\DataFiles\deployment-profile.json` |
| Application compatibility fixture | Rasht-compatible 3-unit fixture required by the current supported application schema; not Production-specific architecture | deployment profile; initialization receipt |
| Application started/registered as Production | No | deployment manifest and run log |
| Real Production installation evidence | **NOT AVAILABLE** | boundary declaration |

### Application build identity

| Field | Proven value |
|---|---|
| Source branch | `phase9.7-activation-blocker-resolution` |
| Source HEAD | `dc41f12e4cabce2cbff67cefa661c96eb74d6fc9` |
| Target framework | `net8.0-windows` |
| Executable | `...\App\Rah_Negar.exe`; 151,552 bytes; SHA-256 `BAF571A8FEE9B4FE0CFC65C6FF4F043DB70667A640BA25C83CC223D4EE64EACB` |
| DLL | `...\App\Rah_Negar.dll`; assembly version `1.0.0.0`; 5,490,688 bytes; SHA-256 `22AFF332EE7FEF618EC2999B7480F7E84A37E184AD541E999851C76317287D9D` |
| Published build timestamp | `2026-09-06T23:51:20Z` (file last-write UTC) |
| Executable path | `D:\Projects\RahNegar_SQLite\Rah_Negar\Qualification\qualification-run\phase9.8-production-like-deployment\App\Rah_Negar.exe` |
| DLL path | `D:\Projects\RahNegar_SQLite\Rah_Negar\Qualification\qualification-run\phase9.8-production-like-deployment\App\Rah_Negar.dll` |

Evidence: `Evidence/application-evidence.json` and
`Evidence/deployment-manifest.json`.

### Database, schema, and units

| Field | Proven value |
|---|---|
| Package DB | `...\Data\db.sys`; 385,024 bytes; SHA-256 `6225EC8F99432FAF27BF503BD33C3DA9304270FBE28E9C272FD77496917E64D7` |
| Runtime DB used by the published app layout | `...\App\Data\db.sys`; 385,024 bytes; same SHA-256 as package DB |
| Last-write UTC | `2026-09-06T23:55:36.4436264Z` |
| Journal mode | `wal` |
| SQLite schema version | `69` |
| SQLite user version | `4` |
| Application migration ledger | `0 -> 4`, four migrations applied |
| Migration IDs | `target-database-foundation-v1`; `phase7.7-security-persistence-atomic-esd-v1`; `event-target-schema-v1-draft`; `report-snapshot-target-schema-v1-isolated` |
| Integrity check | `ok` |
| Foreign-key check | PASS; zero violations |
| Unit count | 3; within supported 3-5 boundary; not 2, 6, or 35 |
| Deployment/profile identity | `qualification-station` / `phase9.8-qualification-generic-r3` |

Evidence: `Evidence/deployment-initialization.json`,
`Evidence/phase9.8-restore-verification.json`, and
`Evidence/deployment-manifest.json`.

### Authority, transition, routing, and startup

The actual application-configured paths are under `App\DataFiles`:

| Field | Proven value |
|---|---|
| Authority state | `...\App\DataFiles\authority-state.json`; persisted Legacy authoritative record |
| Transition state | `...\App\DataFiles\authority-transition.json`; qualification-only transition lifecycle `Aborted` |
| Audit path | `...\App\DataFiles\authority-audit.jsonl`; actual configured path, sequence/hash-chain verified |
| Legacy | `AUTHORITATIVE` |
| Target | `NON-AUTHORITATIVE` |
| Target routing | `DISABLED`; target routing guard returned false |
| Production Activation | `UNAUTHORIZED` |
| Production Cutover | `UNAUTHORIZED` |
| Malformed metadata startup test | PASS; classification `InvalidOrCorrupt`, routing blocked, issue `authority-metadata-unreadable` |
| Activation authorization artifacts | None present in the configured metadata directory |

Evidence: `Evidence/authority-startup-readback.json`,
`Evidence/deployment-initialization.json`, and the persisted files in
`App\DataFiles`. The root `DataFiles` folder is only a qualification evidence
mirror; the application reads `App\DataFiles`.

### Fence and write-drain

The qualification-only fence receipt records one isolated writer before drain,
zero after drain, rejection of a new Legacy writer during the barrier,
rejection of Target write admission while routing is disabled, and successful
restart acquisition with classification `RESTART_READY_NO_ORPHANED_WRITER`.

Evidence: `Evidence/fence-drain-receipt.json`; lock path:
`Audit\writer-fence.lock`.

### Backup and disposable restore

The managed exercise used only the isolated runtime DB. Source hash before and
after was `6225EC8F99432FAF27BF503BD33C3DA9304270FBE28E9C272FD77496917E64D7`.
Backup hash was
`73FFEF3FE8826F02603D0709200F61060B509F1A18184C188479FD4339D22F0F`; restore
target hash matched the backup; the read-only rollback artifact is retained in
`Restore\disposable-restore-rollback.sqlite`. Integrity, FK, WAL/SHM handling,
and source isolation all passed. This is a disposable qualification result,
not a Production backup or custody receipt.

Evidence: `Evidence/phase9.8-restore-verification.json`,
`Backup\verified-backup.sqlite`, and `Restore\` artifacts.

### Audit retention and tamper evidence

The actual configured audit path appended from sequence 2 to 3, retained a
valid hash chain, detected a tampered copy with `audit-digest-invalid`, and
passed restart readback. Implementation retention is `KeepAll`; no
organizational custody or immutable-store claim is made.

Evidence: `Evidence/phase9.8-audit-verification.json` and
`Audit\authority-audit-tampered.jsonl`.

## Qualification conclusion

Installation-bound technical evidence is complete **for this isolated
production-like qualification deployment**. It is not Production evidence and
does not make the project eligible for a Production Activation decision.

**INSTALLATION EVIDENCE STATUS: COMPLETE FOR PRODUCTION-LIKE QUALIFICATION**

**REAL PRODUCTION INSTALLATION EVIDENCE: NOT AVAILABLE**
