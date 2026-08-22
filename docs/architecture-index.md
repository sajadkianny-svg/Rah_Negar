# RahNegar Modernization Architecture Index

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Document type:** Architecture navigation and authority map  
**Scope:** Approved modernization documentation for the Rasht and Ramsar production application  
**Status:** Documentation only

## 1. Purpose

This index provides one entry point to the RahNegar modernization documents. It identifies what each document governs, how documents depend on one another, which implementation phase consumes each decision, and which source is authoritative when statements appear to conflict.

Architects, developers, reviewers, testers, release owners, and operators should begin here before changing a production-facing workflow. Locate the proposed change in the decision ownership map, read the authoritative specification and its dependencies, then use the applicable roadmap for sequence, gates, migration, and rollback. Legacy audits remain essential evidence about the current application and confirmed defects, but they are not target designs.

Approved documentation is the source of truth before implementation. Code must implement a traceable approved decision; existing code does not silently redefine the target architecture. Where implementation and approved documentation differ, treat the difference as an unresolved discrepancy until either the code is corrected or the architecture change is explicitly approved and documented first.

## 2. Architecture hierarchy

### Level 1 — System-wide architecture

Defines the complete modernization sequence, cross-domain dependencies, production gates, deployment, rollback, and decision precedence. The principal Level 1 documents are this index and `master-implementation-roadmap.md`.

### Level 2 — Foundation services

Defines capabilities shared by every subsystem: ShiftProfile authentication, independent ManagementCredential authorization, audit, logging, settings, database integrity, backup/Restore, import/export, startup lifecycle, migration governance, and SQLite limitations. The authoritative Level 2 document is `system-foundation-architecture-specification.md`.

### Level 3 — Domain subsystems

Defines bounded business architecture and detailed contracts. Event documents govern Event state, validation, persistence, services, transactions, and audit. Reporting documents govern report sources, calculations, completeness, projections, runtime integration, snapshots, finalization, Reopen, and export. Legacy audits provide verified current-state evidence feeding these specifications.

### Level 4 — Implementation roadmaps

Defines implementation order, phase gates, coexistence, validation, migration, rollback, and cutover. Roadmaps translate approved architecture into controlled work packages; they do not override an architecture specification.

## 3. Document catalog

| File name | Purpose | Scope | Authority level | Depends on | Used by implementation phase |
|---|---|---|---|---|---|
| `architecture-index.md` | Navigation, ownership, precedence, and traceability map | Entire documentation set | Level 1 governance index | All listed documents | All phases |
| `master-implementation-roadmap.md` | Master phased delivery, gates, dependencies, rollback, and deployment plan | Complete modernization | Level 1 implementation authority | Foundation, Event, reporting specifications and both audits | Phase 0 through Phase 9, especially integration and cutover |
| `system-foundation-architecture-specification.md` | Defines shared security and operational foundations | Authentication, Management authorization, audit/logging, settings, SQLite security, backup/Restore, import/export, startup, Migration | Level 2 authoritative specification | Approved product scope and business rules | Foundation, Database, Migration, Cutover, Maintenance |
| `legacy-event-subsystem-audit.md` | Verified current Event behavior, defects, strengths, risks, and required regression cases | Existing Event entry, persistence, validation, runtime interaction, UI | Level 3 current-state evidence | Existing legacy solution | Phase 0, Event, Runtime, UI, Migration |
| `event-subsystem-architecture-specification.md` | Defines target Event domain behavior and invariants | Event model, state machine, commands, validation, runtime rules, UI principles, transactions | Level 3 authoritative domain specification | Legacy Event audit; system foundation | Event, Runtime, UI, Migration |
| `event-database-schema-specification.md` | Defines canonical target Event persistence | Events/EventAudit fields, keys, dates/times, constraints, indexes, deletion, migration mapping | Level 3 authoritative persistence specification | Event architecture; system foundation; legacy Event evidence | Database, Event, Migration |
| `event-service-layer-specification.md` | Defines application/domain/infrastructure boundaries and workflows | Command handlers, repositories, validation, state machine, runtime projection, transactions, errors | Level 3 authoritative service specification | Event architecture and Event schema; system foundation | Event, Runtime, UI |
| `event-implementation-roadmap.md` | Sequences safe Event subsystem replacement | Domain, persistence, commands, runtime, UI, coexistence, migration, cutover | Level 4 domain roadmap | All Event specifications and legacy Event audit | Phase 0, Database, Event, Runtime, UI, Migration, Cutover |
| `legacy-report-subsystem-audit.md` | Verified current report behavior, defects, risks, and reusable strengths | Legacy calculations, queries, finalization, locks, snapshots, UI, exports | Level 3 current-state evidence | Existing legacy solution and Event/runtime behavior | Phase 0, Runtime, Reporting, UI, Migration |
| `reporting-architecture-specification.md` | Defines target reporting pipeline and evidence model | Source repositories, calculations, projections, completeness, snapshots, finalization/Reopen, exports, UI | Level 3 authoritative domain specification | Legacy report audit; Event/runtime architecture; system foundation | Runtime, Reporting, UI, Migration, Cutover |

“Authority level” expresses architectural function, not an unlimited right to override a more specific approved decision. Conflict rules in section 5 always apply.

## 4. Decision ownership map

| Decision | Authoritative document | Supporting context |
|---|---|---|
| Authentication model | `system-foundation-architecture-specification.md` | Master roadmap consumes it; no domain document may reintroduce RBAC |
| ShiftProfile identity, login, lifecycle, and normal access | `system-foundation-architecture-specification.md` | Audit actor fields in Event/report specifications must conform |
| ManagementCredential, sensitive-action authorization, and recovery | `system-foundation-architecture-specification.md` | Roadmaps define delivery order and gates only |
| Shared database security, connection policy, integrity, schema-version governance | `system-foundation-architecture-specification.md` | Domain schemas add bounded table-level requirements |
| Event database design | `event-database-schema-specification.md` | Foundation controls connections/Migration; Event architecture controls meaning |
| Event rules and state transitions | `event-subsystem-architecture-specification.md` | Legacy Event audit supplies evidence and regression cases |
| Event command validation and transaction workflow | `event-service-layer-specification.md` | Event architecture defines invariants; schema defines structural defense |
| Runtime calculation rules and projection contract | `event-subsystem-architecture-specification.md` and `event-service-layer-specification.md` | Reporting specification defines how reports consume the authoritative projection; audit identifies legacy defects |
| Report generation, calculations, completeness, and projections | `reporting-architecture-specification.md` | Legacy report audit supplies baseline evidence and required cases |
| Snapshot, finalization, finalized reads, Reopen, and supersession | `reporting-architecture-specification.md` | Foundation specifies Management authorization and audit protection |
| Migration governance and operational safeguards | `system-foundation-architecture-specification.md` | Event schema owns field mapping; reporting specification owns report/snapshot migration; master roadmap owns phase/cutover sequence |
| Backup and Restore | `system-foundation-architecture-specification.md` | Master roadmap defines required phase backup points and rollback use |
| Import and export security/governance | `system-foundation-architecture-specification.md` | Reporting specification governs report projection/snapshot export content |
| Event UI principles | `event-subsystem-architecture-specification.md` | Event service specification supplies UI-facing commands/errors; legacy audit preserves proven UX strengths |
| Reporting UI principles | `reporting-architecture-specification.md` | Legacy report audit supplies current-state evidence |
| Cross-subsystem implementation order, gates, deployment, and cutover | `master-implementation-roadmap.md` | Event roadmap supplies more detailed Event work packages |

Where two documents jointly own a decision, ownership is divided by concern rather than duplicated. For example, the Event specification owns runtime business semantics, the service specification owns the callable projection contract and transaction boundary, and the reporting specification owns consumption and presentation of that projection.

## 5. Conflict resolution rules

When documents conflict, first confirm that the texts address the same scope, product version, Station, and decision. Apparent conflicts may be a legitimate division between domain semantics, persistence constraints, service workflow, and implementation sequencing.

Apply this priority:

1. **Latest explicitly approved architecture specification.** A newer approved decision supersedes an older target decision in the same scope. Approval status and date/version must be evidenced, not inferred from file timestamp alone.
2. **Most specific applicable domain specification.** Within a system-wide constraint, the Event schema/service/domain or reporting specification governs its bounded concern. A domain decision cannot weaken foundation security, audit, backup, Migration, Station isolation, or finalized-history protection.
3. **Implementation roadmap.** Roadmaps govern order, gates, coexistence, rollback, and deployment. They must be corrected if they contradict an authoritative architecture specification.
4. **Legacy audit documents.** Audits describe observed current behavior, confirmed defects, evidence, and strengths to preserve. They never override approved target architecture. A legacy behavior continues only when the target specification intentionally preserves it.

Additional rules:

- A specific approved rule takes precedence over a general statement within the same authority tier.
- The current production scope is Rasht and Ramsar; future-platform assumptions do not override it.
- Security, data integrity, immutable finalized evidence, and no-destructive-migration rules cannot be relaxed by roadmap convenience.
- If conflict remains unresolved, stop implementation of the affected decision, record the conflict, assign an architecture owner, update/approve the governing document, then update dependent roadmaps and tests. Do not choose silently in code.
- Superseded text must be clearly marked or removed so only one active target decision remains.

## 6. Implementation phase mapping

| Phase | Primary documents | Required use |
|---|---|---|
| Phase 0 — Baseline and safety | Both legacy audits; master roadmap; Event roadmap | Establish verified behavior, defects, fixtures, build/dependency status, backup and rollback baseline |
| Phase 1 — Foundation | System foundation; master roadmap | Implement ShiftProfile, ManagementCredential, audit/logging/settings/startup and shared contracts |
| Phase 2 — Database | System foundation; Event database schema; Event roadmap; master roadmap | Implement connection/integrity/backup/Migration infrastructure and additive target structures |
| Phase 3 — Event | Event architecture, schema, service specification, legacy Event audit, both roadmaps | Implement canonical Event authority, commands, persistence, validation, audit, and defect regressions |
| Phase 4 — Runtime | Event architecture/service specification; both legacy audits; reporting architecture; roadmaps | Implement authoritative runtime projection and reconcile physical/business results |
| Phase 5 — Reporting | Reporting architecture; legacy report audit; foundation; master roadmap | Implement projections, completeness, immutable snapshots, finalization/Reopen, and export |
| Phase 6 — UI | Event/reporting architecture; service specification; both audits; roadmaps | Move WinForms to application services while preserving approved workflow strengths and improving DPI/UX |
| Phase 7 — Migration | Foundation; Event schema; Event/reporting architecture; both audits; roadmaps | Map, rehearse, reconcile, preserve evidence, and prove rollback on copies |
| Phase 8 — Cutover | Master roadmap; Event roadmap; foundation; all applicable specifications | Execute approved runbook, verification, monitoring, fallback, and release acceptance |
| Phase 9 — Maintenance | Master roadmap; foundation; domain specifications | Maintain regression, backup/Restore/recovery drills, audit, compatibility, and governed evolution |

## 7. Change management

1. Raise an architecture change record describing problem, scope, affected Stations/workflows, alternatives, security/data/reporting impact, migration/rollback implications, and decision owner.
2. Update the authoritative specification before production implementation. If a decision spans documents, update the highest applicable authority and all dependent domain contracts.
3. Assign an explicit document version or approved revision identifier. Implementation work items, commits, reviews, test evidence, migration packages, and release notes reference that approved version.
4. Update this index's catalog, ownership, dependencies, and phase mapping when authority or scope changes.
5. Update roadmaps, schema mappings, acceptance cases, and rollback procedures affected by the decision.
6. Mark outdated decisions as superseded with replacement reference, or remove contradictory target text while retaining required historical decision records. Do not leave two apparently active architectures.
7. Code review verifies conformance and detects architecture drift. Test review proves the new decision, including legacy/regression and failure cases.
8. No silent architecture drift is accepted. An implementation discrepancy is either corrected or processed as an architecture change before merge/release.

Emergency containment may temporarily disable a dangerous path without redesigning architecture. Any lasting behavioral change still requires the documentation-first process and retrospective evidence.

## 8. Final architecture map

```text
                         System Foundation
             authentication • authorization • audit
        database • backup/Restore • Migration • lifecycle
                                |
             +------------------+------------------+
             |                  |                  |
             v                  v                  v
        Event Domain ----> Runtime Engine ----> Reporting Engine
             |                  |                  |
             +------------------+------------------+
                                |
                                v
                               UI
                                |
                                v
                    Migration / Operations
               reconciliation • cutover • recovery
```

The dependency direction is downward/rightward: UI and operations consume approved services; they do not redefine domain rules. Reporting consumes authoritative Event/runtime projections. All components depend on foundation identity, transactions, audit, integrity, backup, and lifecycle controls.

## 9. Index acceptance checklist

- [ ] Every approved source document is cataloged.
- [ ] Every major decision has an authoritative owner.
- [ ] Foundation and domain ownership boundaries are explicit.
- [ ] Legacy audits are treated as evidence, not target authority.
- [ ] All master implementation phases map to governing documents.
- [ ] Conflict and supersession procedures prevent multiple active decisions.
- [ ] Implementation references an approved document revision.
- [ ] Architecture changes precede code and migration changes.
- [ ] Rasht/Ramsar scope and ShiftProfile/ManagementCredential model remain consistent.

