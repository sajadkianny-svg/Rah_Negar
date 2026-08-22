# Phase 7.2 Report UI Pilot Report

## Status

Phase 7.2 adds isolated, UI-neutral report pilot adapters. It does not modify or replace existing report forms, register a report workflow in production, change report calculations, or alter the authoritative legacy reporting path.

## Report presentation boundary

`IReportViewPresenter`, `ReportViewState`, and `ReportUiWorkflowCoordinator` provide loading, legacy-ready, snapshot-ready, shadow-ready, validation-failed, and unauthorized states. Snapshot success is mapped to a read-only report context containing identity, Station/period, checksum, and finalization evidence. The UI context does not expose mutable persistence objects or operational data services.

## Finalized snapshot reader

Snapshot and shadow modes use only `IFinalizedReportReader`. A snapshot is presented only when the reader returns `FoundValid`. Integrity-invalid, unsupported, not-finalized, missing, mismatch, and infrastructure outcomes map to stable feedback without exposing internal errors or partial snapshots.

## Feature modes and rollback

The report feature key maps the existing UI feature modes as follows:

- **Legacy** → Legacy Report Mode;
- **NewWorkflow** → Snapshot Report Mode;
- **MixedValidation** → Shadow Comparison Mode.

Unknown or unconfigured features default to Legacy Report Mode. Legacy mode never calls the snapshot reader. Shadow mode loads legacy output as authoritative and records whether its supplied comparison fingerprint matches the finalized snapshot checksum. Returning the feature key to legacy mode is the immediate rollback path.

## Navigation authorization

Report navigation requires an active authenticated shell context and the explicit `reports.view` capability. Requests marked as management-sensitive additionally require management authorization. Denied requests do not call either the legacy report adapter or finalized snapshot reader.

## Tests and isolation

Tests cover successful finalized snapshot loading, integrity-validation failure mapping, unauthenticated denial, default legacy fallback, matched shadow comparison, report permission enforcement, and management-sensitive denial.

No existing report form, legacy report service, `Program.cs`, production feature configuration, database, renderer, or export workflow was changed. Legacy reporting remains authoritative and active.
