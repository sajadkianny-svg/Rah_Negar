# Rah_Negar final installer gap audit

## Current state

No MSI/EXE installer project or installer toolchain was found in the solution/repository. The only publish configuration found is `Properties/PublishProfiles/FolderProfile.pubxml`; it is a folder publish configuration, not an install/uninstall/upgrade product. The current offline package is not an installer.

`Data/SqliteDatabaseHelper.cs:10-16` and `Utils/ErrorLogger.cs:18` place mutable data and logs under `AppDomain.CurrentDomain.BaseDirectory`. That is unsafe for a normal Program Files installation, where standard operators commonly cannot write to the application directory.

## Readiness matrix

| Capability | Status | Evidence / required work |
|---|---|---|
| Offline x64 application payload | COMPLETE for Pilot package | Existing self-contained folder package was previously launched offline; this audit did not create a new package. |
| `RahNegar-Setup-x64.exe` or MSI | MISSING | No installer project/toolchain found. |
| Program Files application install | MISSING | Requires installer layout, publisher/version metadata, elevation policy, and file ACL design. |
| Safe persistent mutable data location | MISSING | Move/migrate DB, WAL/SHM, settings, logs, and recovery artifacts to a documented managed data root. |
| Start Menu shortcut | MISSING | Installer feature. |
| Optional Desktop shortcut | MISSING | Installer feature with explicit default/opt-in decision. |
| Clean uninstall | MISSING | Remove binaries/shortcuts while preserving or explicitly offering user data; verify sidecars/logs behavior. |
| Upgrade without data loss | MISSING | Versioned migration, backup, rollback, and upgrade rehearsal required. |
| Reinstall without data loss | MISSING | Data root must be outside versioned binaries and tested with existing DB/WAL/SHM. |
| No source/test/qualification/PDB artifacts | PARTIALLY_COMPLETE | Folder package was audited previously; installer must enforce the same exclusion rules. |
| Version/publisher/icon metadata | PARTIALLY_COMPLETE | Application icon resource exists; installer identity and publisher metadata are absent. |
| No internet dependency | COMPLETE for current scope | Source/dependency scan and offline launch show no online service requirement. |

## Exact installer acceptance

Implement one reviewed installer with x64 architecture, version `9.9.0-rc1`, publisher identity, proper icon, offline payload, standard-user launch after elevated install, Start Menu shortcut, optional Desktop shortcut, repair/uninstall behavior, and explicit data preservation. Test fresh install, first run, upgrade, reinstall, uninstall, interrupted install, read-only/low-permission conditions, existing DB migration, and rollback. Do not build it as part of this audit because installer infrastructure does not already exist and a safe upgrade/data-path design must precede implementation.

Installer readiness estimate: 10%. The application payload is usable for Pilot handoff, but there is no product installer and the current data path is not suitable for Program Files.
