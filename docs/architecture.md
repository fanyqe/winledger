# Architecture

WinLedger is a Windows-only .NET 10 desktop application with shared application logic for UI and CLI entry points.

## Projects

- `WinLedger.App`: WPF desktop shell.
- `WinLedger.Cli`: command-line preview for automation.
- `WinLedger.ElevatedHelper`: restricted helper process for rollback operations that may require administrator rights.
- `WinLedger.Domain`: immutable session, registry, service, scheduled task, startup, environment, hosts file, firewall, installed application, file-system, change, and rollback records.
- `WinLedger.Core`: shared interfaces and export services.
- `WinLedger.Collectors`: default collector target definitions.
- `WinLedger.Comparison`: deterministic diff and explanation rules.
- `WinLedger.Rollback`: conservative rollback planning and execution.
- `WinLedger.Storage`: SQLite repositories and migrations.
- `WinLedger.Windows`: Windows API adapters.
- `build`: portable release packaging scripts.
- `.github/workflows`: CI validation and artifact publishing.

## Dependency Rule

Domain does not depend on Windows APIs, SQLite, WPF, or CLI code. UI and CLI depend on the shared services instead of duplicating workflow logic.

## Implemented Slices

The first slice tracks selected registry keys, compares snapshots, classifies changes, exports JSON/HTML, and creates value-level rollback operations.

The services slice captures Service Control Manager state together with persistent configuration from `HKLM\SYSTEM\CurrentControlSet\Services`. It compares service creation, removal, start mode, executable path, display name, account, runtime state, delayed automatic start, and dependencies. Rollback planning is limited to start mode and delayed automatic start because those can be validated against the tracked post-change state without stopping, starting, creating, or deleting services.

The scheduled tasks slice captures Task Scheduler definitions through the Windows Task Scheduler COM service. It compares task creation, removal, enabled state, actions, triggers, run-as user, privilege level, and normalized definition XML. Rollback planning is limited to deleting newly created tasks and restoring enabled state after validating that the current definition still matches the tracked post-change snapshot.

The startup slice groups startup-related persistence from registry Run and RunOnce keys, current-user and common Startup folders, logon or boot scheduled tasks, and automatic, boot, or system Windows services. It compares created, removed, command, enabled-state, and metadata changes in a dedicated view. Rollback planning is limited to deleting newly created Startup folder entries after validating file path, size, timestamp, and entry metadata against the tracked post-change snapshot.

The environment variables slice captures current-user and machine environment variables from their Windows registry-backed locations. It compares created, removed, and modified variables and expands PATH changes into entry-level additions, removals, and relative reordering. Rollback planning remains variable-level: it restores or deletes the full variable only after validating that the current value still matches the tracked post-change snapshot.

The hosts file slice captures `%SystemRoot%\System32\drivers\etc\hosts` through the Windows file system adapter. It stores exact file bytes for rollback, decoded text for line-level display, a content hash, length, timestamp, and warnings when the file is missing or inaccessible. Comparison reports file creation, file removal, line additions, line removals, and byte-level content changes that do not produce visible line additions or removals. Rollback restores the previous full file bytes or deletes a newly created hosts file only after validating that the current file still matches the tracked post-change snapshot.

The firewall slice captures Windows Firewall rules through the `HNetCfg.FwPolicy2` COM policy. It compares rule creation, removal, enabled state, action, direction, application path, service name, protocol, ports, profiles, address scope, interface types, edge traversal, description, and grouping. Rollback planning is limited to deleting newly created rules and restoring enabled state after validating that the current rule still matches the tracked post-change snapshot.

The installed applications slice captures current-user and machine uninstall registry registrations across 32-bit and 64-bit registry views, plus registry-backed AppX/MSIX package registrations from the Windows package repository. It compares registration creation, registration removal, display name, version, publisher, install location, install source, install date, uninstall commands, quiet uninstall commands, modify commands, estimated size, MSI marker, system component marker, release type, comments, information URL, package full name, package family name, package publisher identity, package resource identity, manifest path, and inbox package marker changes. Rollback planning intentionally emits manual-review warnings only, because safe application or package removal depends on vendor uninstallers, package ownership, shared files, services, drivers, deployment state, and current machine state.

The file-system slice captures selected roots with recursive metadata scanning, default high-noise exclusions, optional SHA-256 hashing, and optional small-file backup content. It compares created, deleted, modified, and hash-backed renamed files. Rollback planning is limited to deleting newly created entries and restoring backed-up deleted or modified files after validating that the current entry still matches the tracked post-change snapshot. Directory deletion refuses non-empty directories, and mutation is restricted to the monitored root.

The elevated helper slice adds a separate executable for rollback operations that may need administrator rights. The CLI can launch the helper with a one-time authenticated request file. The helper only accepts known rollback report schemas for registry, services, scheduled tasks, startup entries, environment variables, hosts file, firewall, and file-system changes. It reuses the existing validation-first rollback executors and writes a local audit log for accepted and completed requests.
