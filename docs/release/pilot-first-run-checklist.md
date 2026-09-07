# Rah_Negar Pilot RC1 — First-Run Checklist

Use this checklist on a clean extracted copy of the package. Record the date,
operator, workstation, and package ZIP SHA-256 with the Pilot evidence.

- [ ] Extracted only the release ZIP; no repository or Git metadata is present.
- [ ] `App\Rah_Negar.exe` starts without a crash dialog.
- [ ] The first-run setup screen is displayed because no `App\Data\db.sys`
      exists.
- [ ] `App\Data\db.sys` is created after startup initialization checks.
- [ ] No qualification/test database, credentials, private key, activation
      file, Target-authority file, or enabled routing file is present.
- [ ] Station selection is limited to the supported current scope: Rasht or
      Ramsar.
- [ ] The Unit boundary is understood: 3–5 inclusive; 35 rejected.
- [ ] Setup can be completed with a locally chosen password; no synthetic
      credential is shipped in the package.
- [ ] A subsequent launch reaches the login screen after setup.
- [ ] Backup location is selected outside the application folder and a backup
      recommendation has been recorded.
- [ ] Windows scaling is 100%, 125%, or 150%; above 150% is not accepted.
- [ ] Legacy is AUTHORITATIVE, Target is NON-AUTHORITATIVE, and Target Routing
      is DISABLED.
- [ ] Production Activation and Production Cutover remain UNAUTHORIZED.
- [ ] Independent Human Review is recorded as NOT PERFORMED / UNAVAILABLE.
- [ ] MQ-07 is recorded as BLOCKED with automated invariant evidence retained.

