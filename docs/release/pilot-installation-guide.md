# Rah_Negar Pilot RC1 — Offline Installation Guide

## Before installation

Use a Windows x64 workstation in the isolated Pilot environment. Keep the
package on a local, writable disk. Do not place it over a live Production
installation and do not copy a Production database into the package during
this Pilot release.

The package is self-contained and includes the .NET 8 Windows runtime. No
separate .NET installation or network connection is required.

## Install

1. Extract `Rah_Negar-Pilot-RC1-win-x64.zip` to a dedicated folder, for example
   `C:\RahNegar\Pilot-RC1\`.
2. Keep the extracted folder structure intact, especially the `App` folder.
3. Start `App\Rah_Negar.exe`.

## First launch

The package intentionally contains no database. On first launch the application
creates `App\Data\db.sys` and opens the setup screen. Select Rasht or Ramsar,
enter the station setup values, choose the Persian data start year and month,
set the unit runtime baselines/statuses, and create the local reset/login
password. Complete setup once; subsequent launches open the login screen.

The current product scope is Rasht and Ramsar. Rasht uses three units and
Ramsar uses four. The generic supported boundary is 3–5 units inclusive;
35 units is rejected.

## Normal use and backups

The database remains at `App\Data\db.sys`. Use the in-app Settings → Backup
action to create an encrypted `.rngbak` file outside the application folder,
preferably on a controlled removable or network-isolated custody medium. Test
restore using a disposable Pilot copy before relying on a backup.

## DPI requirement

Use 100%, 125%, or 150% Windows display scaling. Scaling above 150% is blocked
by the current safety boundary and is not a supported Pilot configuration.

## Updating while preserving data

1. Exit Rah_Negar completely.
2. Copy the whole existing `App\Data` folder, including `db.sys` and any SQLite
   sidecar files, to a separate backup location.
3. Extract the new package into a new versioned folder.
4. Copy the saved `Data` folder into the new `App` folder only after verifying
   the backup and package checksums.
5. Start the new executable and verify the station/profile before continuing.

Never overwrite or delete the old application folder until the new version has
opened successfully and the backup has been checked.

## Rollback and uninstall

To roll back, exit the application and start the prior version using its prior
App folder and Data folder. To uninstall the Pilot, exit the application and
remove the extracted package folder. Preserve `App\Data` and all `.rngbak`
files separately if the data may be needed later. Windows registry, services,
and cloud accounts are not created by this package.

