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

## Post-Alpha Priority Backlog

### P0: Product Usability

- Single unified tracking session across all supported subsystems.
- WPF session history with saved session reopening.
- One baseline and comparison orchestration flow for every subsystem.
- Progress reporting, cancellation, and non-blocking background execution.
- WPF rollback execution through the elevated helper.
- Helper executable signature or hash verification before privileged execution.
- Rollback requests bound to the source report hash.
- Restrictive ACLs and cleanup for helper request and response files.
- Multi-root registry tracking and ready-made tracking profiles.
- Real Windows integration tests for supported subsystem flows.
- Atomic hosts file and backed-up file restore where the platform allows it.

### P1: Reliability And Release Hardening

- Real SQLite migration pipeline instead of create-only schema setup.
- `PRAGMA foreign_keys = ON` for SQLite connections.
- DPAPI or per-user protection for sensitive snapshot data.
- Hash-based file validation by default for supported file rollback operations.
- NTFS USN Journal-backed file tracking.
- Local database retention and cleanup controls.
- Signed release artifacts.
- SBOM generation and CodeQL scanning.
- Public issue workflow and triage labels.
- Correct `REG_NONE` registry value handling.

### P2: Code Health

- Split the large WPF main view model into focused view models and services.
- Move repeated subsystem capture, compare, export, and rollback flows into shared services.
- Add a coverage gate for core comparison, storage, and rollback behavior.
- Add UI-level tests for critical WPF flows.
- Move helper communication to named pipes or another authenticated handle-based protocol.
- Coordinate snapshot consistency and transactions across subsystem captures.

## Later Phases

- Windows package deployment API integration beyond registry-backed AppX/MSIX package registration tracking
- NTFS USN Journal file tracking beyond the selected-root file-system scanner
- Full desktop elevation prompts and helper status UI
- Installer package beyond the portable zip
