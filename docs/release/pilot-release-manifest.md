# Rah_Negar Pilot RC1 — Release Manifest

| Field | Value |
|---|---|
| Release name | Rah_Negar Pilot RC1 |
| Version | 9.9.0-rc1 |
| Branch | phase9.7-activation-blocker-resolution |
| Commit SHA | ef287aa6a7c67b3745e70f4e0d21fd9de42c12b7 |
| Build configuration | Release / x64 |
| Target framework | net8.0-windows |
| Runtime | win-x64, self-contained |
| Publish mode | Folder publish; no installer; offline deployable |
| SQLite strategy | No DB shipped; initialized on first run |
| Application executable | `Qualification/release/pilot-rc1/App/Rah_Negar.exe` |
| Executable SHA-256 | `84D64D4ACB4DC3BBE8B37ACE598B7985BFBD06AB423E8F6B1917A4AA434FEB91` |
| ZIP SHA-256 | `491E926DF4BC4CE76263B4CB950D4BE57433671404BD3A5A2D549F8E9FB201BD` |
| ZIP size | 95,825,123 bytes |
| Full automated tests | 759 passed, 0 failed, 0 skipped |
| Relevant qualification | Phase 9.8 production-like harness passed; 41 focused tests passed |
| Known build warnings | 6 NU1701 compatibility warnings; see limitations |
| Project Owner approval reference | `docs/phase9.8-project-owner-decision.md`; Project Owner: Sajad Kiyani |
| Independent Human Review | NOT PERFORMED / UNAVAILABLE |
| Pilot authorization | APPROVED FOR PILOT / PRE-PRODUCTION RELEASE |
| Production Activation | UNAUTHORIZED; NOT_ELIGIBLE_FOR_ACTIVATION_DECISION |
| Production Cutover | UNAUTHORIZED |

## Authority boundary

Legacy = **AUTHORITATIVE**  
Target = **NON-AUTHORITATIVE**  
Target Routing = **DISABLED**  
Production Activation = **UNAUTHORIZED**  
Production Cutover = **UNAUTHORIZED**

The release package contains no Production database, real credentials, private
keys, activation authorization, Target-authority state, or enabled routing.
