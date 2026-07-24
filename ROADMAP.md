# Roadmap

## Current Release

WinLedger 0.1.0 is focused on local Windows change tracking with conservative rollback support.

- Unified tracking sessions across the supported subsystems.
- WPF session history with saved session reopening.
- Baseline and comparison orchestration for registry, services, scheduled tasks, startup entries, environment variables, hosts file, firewall rules, installed applications, and selected file-system roots.
- Progress reporting, cancellation, and background execution for unified WPF capture.
- SQLite persistence with a migration table and `PRAGMA foreign_keys = ON`.
- DPAPI protection for stored snapshot payloads on Windows.
- Hash-based file validation enabled by default.
- Session retention and cleanup controls.
- Multi-root registry tracking profiles.
- `REG_NONE` registry value preservation.
- NTFS change journal state capture and comparison continuity warnings where Windows exposes the journal.
- WPF rollback execution through the elevated helper for supported rollback operations.
- Helper executable hash verification, report-hash binding, restrictive ACLs, and cleanup for helper request files.
- Atomic hosts file restore and atomic backed-up file restore.
- Atomic unified capture commits for snapshot rows and session status.
- JSON, HTML, plain-text, registry `.reg`, and registry PowerShell exports.
- Portable Windows x64 package generation with manifest and SBOM.
- CI build, test, format verification, coverage gate, CodeQL, dependency review, Dependabot, and issue templates.
- Focused test coverage for comparison, storage, rollback, helper request validation, Windows registry integration, package scripts, and critical WPF binding regressions.

## Remaining Hardening

- Add broader real Windows integration tests for services, scheduled tasks, startup entries, hosts file, firewall, and file-system rollback flows.
- Expand UI-level tests around the main WPF workflows.
- Move more repeated subsystem capture, compare, export, and rollback flows into shared services.
- Continue splitting the WPF main view model into focused view models and services.
- Add issue triage labels and contribution templates.
- Add optional signed release artifacts when a signing certificate is available.
- Evaluate authenticated named pipes or another handle-based protocol for helper IPC.
- Evaluate full USN delta enumeration for faster large-tree file tracking.

## Later

- Windows package deployment API integration beyond registry-backed AppX/MSIX package registration tracking.
- Installer package beyond the portable zip.
- Additional report formats or integrations if real users ask for them.
