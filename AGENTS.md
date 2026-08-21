# Rah_Negar Project Instructions

## Project
Rah_Negar is a C# WinForms desktop application using SQLite.

The current production scope is limited to the Rasht and Ramsar stations.
Do not redesign this project into the future universal platform unless explicitly requested.

## Technology
- C#
- WinForms
- SQLite
- .NET 8 / net8.0-windows unless project files prove otherwise
- Fully offline
- No cloud services
- No external AI dependency inside the application

## Safety Rules
- Preserve existing business behavior unless a bug is confirmed.
- Do not delete or reset user data.
- Do not modify the SQLite schema destructively without explicit approval.
- Do not make broad architectural rewrites unless explicitly requested.
- Prefer small, reviewable, testable changes.
- Do not silently upgrade major frameworks or packages.
- Do not modify files during the initial audit phase.

## Important Business Rules
- Data entry is valid only from data_start_date onward.
- Each valid day contains 12 main records at odd hours:
  01, 03, 05, 07, 09, 11, 13, 15, 17, 19, 21, 23.
- Daily unique data is mandatory where required by the current workflow.
- Events are optional.
- Finalized months must remain protected against unintended editing.
- Persian date handling must preserve existing application conventions.
- Rasht and Ramsar station-specific logic must remain isolated.
- Reporting must preserve the intended calculation rules:
  min/max/average for main data where applicable,
  and sum for daily unique values where applicable.

## Architecture Review
Inspect all:
- Forms
- Services
- Models
- Helpers / Utils
- Core
- Data access
- SQLite queries
- Reporting logic
- Station profile logic
- Date/time logic
- DataGrid logic

## Audit Requirements
During the initial audit:

1. Read the entire solution before proposing changes.
2. Build the whole solution.
3. Record all build errors and warnings.
4. Inspect all NuGet packages for:
   - vulnerabilities
   - deprecation
   - incompatibility
   - redundant dependencies
5. Trace major workflows end-to-end.
6. Verify findings from code or reproduction.
7. Do not classify speculative concerns as confirmed bugs.

Check specifically for:
- runtime exceptions
- null/reference bugs
- incorrect conditions
- boundary and off-by-one errors
- Persian date mistakes
- incorrect time handling
- database consistency problems
- unsafe transactions
- duplicate records
- invalid INSERT/UPDATE/DELETE behavior
- monthly-lock bypasses
- reporting/calculation errors
- Rasht/Ramsar logic leakage
- duplicate/conflicting implementations
- dead or unreachable code
- incomplete features
- resource leaks
- UI state bugs
- DataGrid recreation/performance issues
- DPI/layout issues
- slow startup
- deprecated or vulnerable packages

## Finding Classification
Classify findings as:
- CRITICAL
- HIGH
- MEDIUM
- LOW
- CODE QUALITY

For each confirmed finding report:
- file path
- class/method
- evidence
- failure scenario
- severity
- recommended fix

## Initial Audit Output
Produce:
A. Architecture map
B. Build status
C. Dependency/package health
D. Confirmed bugs
E. Potential bugs requiring validation
F. Incomplete functionality
G. Database/schema risks
H. Performance problems
I. UI/DPI problems
J. Duplication/technical debt
K. Prioritized remediation plan

## Validation After Future Changes
After every modification batch:
- build the entire solution
- run available tests
- test the affected workflow
- inspect git diff
- report exactly what changed
- report what remains unresolved
