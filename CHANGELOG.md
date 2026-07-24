# Changelog

## Unreleased

- No unreleased product changes.

## 0.1.0 - 2026-07-24

- Started the .NET 10 LTS WinLedger solution.
- Added the first registry snapshot, diff, JSON/HTML export, and rollback planning slice.
- Added Windows Services snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and conservative rollback planning.
- Added Scheduled Tasks snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and conservative rollback planning.
- Added Startup Entries grouping, comparison, JSON/HTML export, WPF tab, CLI commands, and conservative Startup folder rollback planning.
- Added Environment Variables snapshot, comparison, PATH entry reporting, JSON/HTML export, WPF tab, CLI commands, and conservative variable-level rollback planning.
- Added Hosts File snapshot, line comparison, exact-byte JSON/HTML export, WPF tab, CLI commands, and conservative full-file rollback planning.
- Added Windows Firewall rule snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and conservative rule deletion/enabled-state rollback planning.
- Added Installed Applications snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and manual-review rollback planning for registration changes.
- Added AppX/MSIX package registration metadata tracking to the Installed Applications slice.
- Added plain-text summary exports plus registry `.reg` and PowerShell rollback exports generated from structured report data.
- Added CLI commands to list saved sessions and reopen a session summary from local SQLite storage.
- Added selected-root File-System snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and conservative rollback planning for created and backed-up file changes.
- Added a portable Windows release package script and GitHub Actions artifact workflow.
- Added a restricted elevated rollback helper executable with authenticated request files and CLI launch support.
- Enabled hash-based file validation by default for file-system captures.
- Added session retention cleanup controls and a CLI cleanup command.
- Added CI build, test, format verification, coverage gate, CodeQL, dependency review, Dependabot, issue templates, and a pull request template.
- Added release metadata, branding assets, portable package manifest generation, and SBOM generation.
