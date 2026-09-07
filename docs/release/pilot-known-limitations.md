# Rah_Negar Pilot RC1 — Known Limitations

1. This is a Pilot / Pre-Production package only. It is not authorization for
   Production Activation or Production Cutover, and it contains no real
   Production installation evidence.
2. Independent Human Review is **NOT PERFORMED / UNAVAILABLE**. Automated
   invariant evidence does not replace that review.
3. MQ-07 is **BLOCKED — MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH
   AUTOMATED INVARIANT EVIDENCE RETAINED**.
4. The build retains six known NU1701 warnings because OpenTK 3.1.0,
   OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 resolve
   through older .NET Framework compatibility assets in the app and test
   dependency graphs. They were not silently upgraded for this package.
5. The package is self-contained for Windows x64, but is not an MSI installer
   and does not register shortcuts or services.
6. Backup/restore is local and encrypted by the current application
   implementation. Operators must protect backup files and retain a tested
   copy; organizational custody and independent verification are not claimed.
7. UI qualification evidence covers 100%, 125%, and 150% DPI. Values above
   150% remain blocked by the current UI safety boundary.
8. The package ships no database. First-run setup is required before login and
   before entering operational data.

