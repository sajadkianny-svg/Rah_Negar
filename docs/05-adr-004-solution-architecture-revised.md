# ADR-004 — Logical Solution Architecture and Physical Packaging

**Status:** Revised — Proposed for approval

## Context

RahNegar requires clear separation among domain rules, use-case orchestration, runtime calculation, reporting, configuration parsing, persistence, backup, migration, and desktop UI. The earlier ADR described each logical boundary as a separate named project. That wording risked making one `.csproj` per boundary an immediate requirement even when a smaller physical solution could preserve the same architecture during early development.

Logical architecture and physical packaging solve different problems:

- A **logical module boundary** defines responsibility, public contracts, allowed dependencies, ownership of models, and prohibited knowledge.
- A **physical `.csproj`/assembly boundary** uses compiler and build-system enforcement to isolate one or more logical modules.

The platform requires the former from the first change. It does not require a one-to-one mapping to the latter from the first milestone.

## Decision

RahNegar Version 1 is a modular monolith with mandatory logical boundaries and mandatory dependency direction. Initial implementation may combine compatible logical modules into fewer physical projects, provided the internal structure and references preserve those boundaries and allow later extraction without redesigning domain concepts or use-case contracts.

### Mandatory logical modules

- Domain
- Application
- Runtime
- Reporting
- Configuration
- SQLite Infrastructure
- Backup Infrastructure
- Legacy Migration
- Desktop UI
- Bootstrap/Composition Root

### Mandatory dependency direction

```text
Desktop UI ───────────► Application
Bootstrap ────────────► composition targets

Application ──────────► Domain
Application ──────────► Runtime contracts/services
Application ──────────► Reporting contracts/services

Runtime ──────────────► Domain
Reporting ────────────► Domain
Reporting ────────────► Runtime result contracts where required

Configuration ────────► Domain construction/validation contracts

SQLite Infrastructure ► Application-owned persistence contracts
SQLite Infrastructure ► Domain types needed by those contracts

Backup Infrastructure ► Application-owned backup contracts

Legacy Migration ─────► Application import contracts
Legacy Migration ─────► Domain
```

Circular logical or physical dependencies are prohibited.

## Domain independence

The Domain logical module must not depend on:

- Desktop UI frameworks.
- SQLite or SQL libraries.
- File-system paths.
- JSON/YAML parsers or configuration document shapes.
- Backup/encryption implementation.
- Legacy schemas or Rasht/Ramsar source-table names.
- Dependency-injection containers.
- Logging implementations.

Domain types may express configuration meaning after validation, but cannot parse configuration documents.

## Permitted initial physical packaging

An initial solution may use, for example:

```text
RahNegar.Core
  ├── Domain logical module
  ├── Runtime logical module
  └── Reporting domain/calculation logical module

RahNegar.Application
  ├── Application logical module
  └── contracts implemented by infrastructure

RahNegar.Infrastructure
  ├── Configuration parsing
  ├── SQLite infrastructure
  ├── Backup infrastructure
  └── Legacy migration, if dependency isolation remains enforceable

RahNegar.Desktop
  ├── Desktop UI
  └── Bootstrap composition root
```

This is an allowed example, not a mandated project list. High-risk modules such as Runtime or Legacy Migration may be separate assemblies immediately if compiler isolation materially reduces risk.

## Boundary enforcement without an assembly

When logical modules share a project, they must still have:

- Dedicated namespaces and directories.
- Explicit internal/public APIs.
- No access to another module's internal implementation.
- Architecture tests enforcing namespace dependency rules.
- Module-specific unit tests.
- Composition only through the Bootstrap module.
- No service-locator access from Domain code.
- No database or UI types crossing Domain contracts.

`internal` visibility, `InternalsVisibleTo` usage, and shared utility folders must not become shortcuts that erase boundaries.

## Extraction requirement

Every logical module must remain extractable into a separate assembly without changing:

- Domain terminology or entity identities.
- Use-case semantics.
- Persistence contract meaning.
- Runtime input/output meaning.
- Final snapshot domain schema.
- Station-configuration semantics.

Extraction may require moving files, adjusting visibility, and adding references, but must not require redesigning the domain.

## Criteria for creating a physical boundary

A logical module should receive its own `.csproj` when one or more apply:

- Compiler enforcement materially reduces dependency risk.
- It has an independent release/versioned contract.
- It requires dependencies inappropriate for its neighbors.
- It needs isolated performance or security review.
- It is reused by migration, tests, CLI tooling, or another host.
- Team ownership or build time benefits justify separation.

Runtime and Domain are strong candidates for early physical separation, but this is not mandatory before the first vertical slice.

## Consequences

### Positive

- Architecture remains disciplined without project proliferation.
- Early vertical slices can be built with lower setup overhead.
- Compiler boundaries can be introduced where they provide value.
- Future extraction remains possible.
- Domain independence and dependency direction remain non-negotiable.

### Negative

- Shared assemblies provide weaker compiler enforcement.
- Architecture tests and review discipline become mandatory.
- Developers must understand logical boundaries rather than equating folders with architecture.

## Compliance tests

Before implementation milestones are accepted:

- Architecture tests must reject prohibited namespace dependencies.
- Domain tests must run without initializing SQLite, WinForms, or configuration parsers.
- Runtime tests must run without a database or UI.
- Infrastructure types must not appear in Domain public APIs.
- Legacy migration types must not appear in normal production use cases.

## Superseded wording

Any earlier wording implying that each logical boundary must immediately be a separate `.csproj` is superseded. Mandatory logical separation does not mean mandatory one-to-one physical assembly separation.
