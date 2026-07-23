# Roadmap

## Phase 1: Foundation and Registry Slice

- Session creation
- Registry snapshot collection
- SQLite persistence
- Registry comparison
- JSON, HTML, plain-text, registry `.reg`, and registry PowerShell export
- Conservative registry rollback plan
- WPF shell
- CLI preview with session create, list, and show commands
- Unit tests

## Phase 3: Windows Configuration

- Services snapshot, comparison, JSON export, WPF tab, CLI commands, and conservative rollback planning
- Scheduled tasks snapshot, comparison, JSON export, WPF tab, CLI commands, and conservative rollback planning
- Startup grouping, comparison, JSON export, WPF tab, CLI commands, and conservative Startup folder rollback planning
- Environment variables snapshot, comparison, PATH entry reporting, JSON export, WPF tab, CLI commands, and conservative variable-level rollback planning
- Hosts file snapshot, line comparison, exact-byte JSON export, WPF tab, CLI commands, and conservative full-file rollback planning

## Phase 4: Firewall and Packages

- Firewall rules snapshot, comparison, JSON export, WPF tab, CLI commands, and conservative rule deletion/enabled-state rollback planning
- Installed applications and AppX/MSIX package registration snapshot, comparison, JSON/HTML export, WPF tab, CLI commands, and manual-review rollback planning for registration changes

## Phase 5: File-System Tracking

- Selected-root file-system snapshot, comparison, JSON export, WPF tab, CLI commands, and conservative rollback planning for created and backed-up file changes

## Phase 6: UX and Release

- Restricted elevated rollback helper process
- Portable win-x64 release package script and GitHub Actions artifact workflow

## Later Phases

- Windows package deployment API integration beyond registry-backed AppX/MSIX package registration tracking
- NTFS USN Journal file tracking beyond the selected-root file-system scanner
- Full desktop elevation prompts and helper status UI
- Installer package beyond the portable zip
