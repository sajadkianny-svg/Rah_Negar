# Phase 9.8 - Installation-Bound Evidence Package

Status: **AWAITING INSTALLATION EVIDENCE**

Purpose: define the evidence that must be captured from the actual intended
deployment environment before activation-decision readiness can be assessed.
This document is a collection template, not a Production authorization and
not an instruction to activate or cut over.

## Current control boundary

Legacy is **AUTHORITATIVE**. Target is **NON-AUTHORITATIVE**. Target Routing is
**DISABLED**. Production Activation and Production Cutover are
**UNAUTHORIZED**. No installation-bound values are present in this package.

## Required installation receipt

| Field | Required value/evidence | Installation value | Status |
|---|---|---|---|
| Deployment/station identifier | Exact deployment identity and supported station (`Rasht` or `Ramsar`) | Not captured | AWAITING INSTALLATION EVIDENCE |
| Application version | Exact candidate binary/version identity | Not captured | AWAITING INSTALLATION EVIDENCE |
| Schema version | Exact Production schema/migration version | Not captured | AWAITING INSTALLATION EVIDENCE |
| Profile identity/version | Exact installed profile identity and profile version | Not captured | AWAITING INSTALLATION EVIDENCE |
| Unit count | Actual installed unit count; supported boundary is 3-5 inclusive | Not captured | AWAITING INSTALLATION EVIDENCE |
| Production DB path | Exact normalized live `db.sys` path | Not captured; qualification recorded `Data/db.sys` absent | AWAITING INSTALLATION EVIDENCE |
| Production DB SHA-256 | Hash of the quiesced, identified Production DB | Not captured | AWAITING INSTALLATION EVIDENCE |
| Production DB size | Size in bytes of the identified Production DB | Not captured | AWAITING INSTALLATION EVIDENCE |
| Last-write timestamp | UTC last-write timestamp captured from the identified DB | Not captured | AWAITING INSTALLATION EVIDENCE |
| Backup receipt | Receipt bound to source path, DB identity, correlation, schema, hash, size, integrity, FK, and WAL/SHM state | Not captured for an actual installation | AWAITING INSTALLATION EVIDENCE |
| Restore verification receipt | Receipt for an isolated restore of the selected artifact, including post-restore checks | Not captured for an actual installation | AWAITING INSTALLATION EVIDENCE |
| Authority metadata state | Exact authority-state and transition metadata paths, hashes, and readback | Not captured from an installation; qualification metadata was absent pre/post | AWAITING INSTALLATION EVIDENCE |
| Transition metadata state | Exact transition intent/generation/epoch state and receipt | Not captured | AWAITING INSTALLATION EVIDENCE |
| Audit store state | Audit path, sequence/hash-chain verification, retention location, and readback result | Not captured from an installation; qualification audit metadata was absent pre/post | AWAITING INSTALLATION EVIDENCE |
| Fencing mechanism state | Named mutex, exclusive lock, generation lease, ownership, and stale/abandoned handling evidence | Not captured from an installation | AWAITING INSTALLATION EVIDENCE |
| Write-drain readiness | Writer inventory, quiescence confirmation, drain result, and abort/reopen readiness | Not captured | AWAITING INSTALLATION EVIDENCE |
| Routing state | Installation readback proving Target route is disabled | Not captured from an installation | AWAITING INSTALLATION EVIDENCE |
| Legacy authority confirmation | Installation readback proving Legacy is authoritative | Not captured from an installation | AWAITING INSTALLATION EVIDENCE |
| Target non-authority confirmation | Installation readback proving Target is non-authoritative | Not captured from an installation | AWAITING INSTALLATION EVIDENCE |
| Target routing disabled confirmation | Separate explicit confirmation that Target routing is disabled | Not captured from an installation | AWAITING INSTALLATION EVIDENCE |

## Evidence capture requirements

The future receipt must be UTC-timestamped, correlation-bound, scope-bound,
version-bound, and retained with the source artifacts. It must identify the
actual installation and distinguish live Production artifacts from disposable
qualification fixtures. A hash, size, or timestamp from a qualification
database is not a Production installation value.

The capture must include the backup and restore receipts, SQLite integrity and
foreign-key results, WAL/SHM handling, final authority/routing readbacks, and
the evidence references needed for the operator and governance review. Any
missing, stale, contradictory, or unavailable field is a no-go condition.

## Current evidence limitation

Phase 9.7 recorded a successful disposable qualification and verified that the
Production path remained isolated. That proves safety of the qualification
boundary only. It does not provide any of the installation values above and
does not close B-06/G-97-10 or B-11/G-97-16.

**Final package status: AWAITING INSTALLATION EVIDENCE.**

