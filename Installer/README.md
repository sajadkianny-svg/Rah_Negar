# RahNegar offline installer

`RahNegar.iss` is the version-controlled Inno Setup source for the x64 offline installer.
It consumes only the clean self-contained Release publish output produced by
`prepare-installer.ps1`. The application is installed under Program Files; mutable
operational state is created under `%ProgramData%\RahNegar` and is preserved on uninstall.

The installer must never contain `db.sys`, SQLite sidecars, qualification fixtures,
credentials, secrets, source files, tests, or PDB files. `validate-installer.ps1` enforces
that payload rule before compilation.

Inno Setup 6 is intentionally not vendored. On a build machine with `ISCC.exe` available,
run `powershell -ExecutionPolicy Bypass -File .\Installer\prepare-installer.ps1 -BuildInstaller`.
