# WinLedger

WinLedger records Windows system changes before and after you run an installer, script, tweak tool, driver package, or manual configuration change. It helps you answer a simple question: what changed on this machine, and which parts can be safely rolled back?

Think of it as change tracking for Windows system modifications. It is built for people who want a clear before-and-after record instead of guessing what an installer or tweak changed.

## What WinLedger Does

- Creates local tracking sessions.
- Captures before-and-after snapshots of selected Windows areas.
- Compares snapshots and groups changes by subsystem.
- Explains technical changes in readable terms.
- Exports reports as JSON, HTML, plain text, registry `.reg` files, and registry PowerShell rollback scripts.
- Builds conservative rollback plans where the current machine state can be validated first.
- Stores data locally in SQLite.
- Provides both a WPF desktop app and a CLI preview.

## What It Is Not

WinLedger is not a virtual machine, antivirus, registry cleaner, optimizer, sandbox, or perfect System Restore replacement. Rollback is best-effort, subsystem-dependent, and intentionally conservative.

Some changes can be reversed automatically after validation. Some require manual review. Some cannot be safely reversed by WinLedger, especially when they depend on vendor uninstallers, shared files, drivers, credentials, or external state.

## Current Alpha Scope

The current alpha includes working slices for:

- Windows Registry values and selected keys
- Windows Services
- Scheduled Tasks
- Startup entries
- User and machine environment variables
- Hosts file changes
- Windows Firewall rules
- Installed application and AppX/MSIX package registrations
- Selected-root file-system tracking
- JSON, HTML, plain-text, `.reg`, and registry `.ps1` exports
- Conservative rollback planning and execution for supported operations
- Restricted elevated rollback helper
- Portable win-x64 package output

File-system tracking currently uses selected-root scanning with exclusions. NTFS USN Journal support is planned for a later phase.

## Requirements

- Windows 11 or another supported Windows desktop release
- .NET 10 LTS SDK

This repository pins SDK `10.0.302` through `global.json`.

## Build And Test

```powershell
dotnet restore
dotnet build WinLedger.sln
dotnet test WinLedger.sln
dotnet format WinLedger.sln --verify-no-changes --no-restore
dotnet list WinLedger.sln package --vulnerable --include-transitive
```

If the system `dotnet` command resolves to an older SDK, use the pinned SDK path explicitly:

```powershell
C:\Users\cekir\.dotnet\dotnet.exe build WinLedger.sln
```

## Portable Package

```powershell
.\build\Package-Portable.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0-alpha
```

The package is written to `artifacts\release` and contains:

- `app\WinLedger.App.exe`
- `cli\WinLedger.Cli.exe`
- `helper\WinLedger.ElevatedHelper.exe`
- license, security notes, README, and docs

The default package is self-contained. Use `-FrameworkDependent` only when the target machine already has the required .NET runtime installed.

The packaging script uses the SDK pinned by `global.json`. On developer machines with multiple SDKs, pass `-DotNetPath C:\Users\cekir\.dotnet\dotnet.exe` if automatic SDK resolution cannot find the pinned SDK.

See [docs/release-process.md](docs/release-process.md) for the release checklist.

## CLI Preview

Show available commands:

```powershell
dotnet run --project src\WinLedger.Cli -- --help
```

Create, list, and reopen local sessions:

```powershell
dotnet run --project src\WinLedger.Cli -- session create .\winledger.db "Installing ExampleApp"
dotnet run --project src\WinLedger.Cli -- session list .\winledger.db
dotnet run --project src\WinLedger.Cli -- session show .\winledger.db <session-id>
```

The single-token aliases `session-create`, `session-list`, and `session-show` are also available for scripts.

Minimal registry flow:

```powershell
dotnet run --project src\WinLedger.Cli -- session create .\winledger.db "Installing ExampleApp"
dotnet run --project src\WinLedger.Cli -- registry-capture .\winledger.db <session-id> Baseline HKCU\Software\WinLedger\TestSandbox
dotnet run --project src\WinLedger.Cli -- registry-capture .\winledger.db <session-id> Comparison HKCU\Software\WinLedger\TestSandbox
dotnet run --project src\WinLedger.Cli -- registry-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\registry-report.json
dotnet run --project src\WinLedger.Cli -- registry-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\registry-report.html
dotnet run --project src\WinLedger.Cli -- registry-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\registry-report.txt
dotnet run --project src\WinLedger.Cli -- registry-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\registry-rollback.reg
dotnet run --project src\WinLedger.Cli -- registry-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\registry-rollback.ps1
dotnet run --project src\WinLedger.Cli -- registry-rollback-apply .\registry-report.json <operation-id>
```

Other subsystem commands follow the same pattern:

```powershell
dotnet run --project src\WinLedger.Cli -- service-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- service-compare .\winledger.db <baseline-snapshot-id> <comparison-snapshot-id> .\services-report.json
dotnet run --project src\WinLedger.Cli -- task-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- startup-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- environment-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- hosts-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- firewall-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- applications-capture .\winledger.db <session-id> Baseline
dotnet run --project src\WinLedger.Cli -- files-capture .\winledger.db <session-id> Baseline .\Sandbox --hash --backup-small-files 262144
```

Use `--no-elevation` only for local smoke tests against non-privileged sandbox paths.

## Safety Notes

WinLedger validates the expected current state before applying supported rollback operations. If the machine has changed again since the comparison snapshot, rollback can stop or mark the operation as conflicted instead of blindly mutating the system.

Rollback support is intentionally limited in the alpha:

- Registry rollback covers value-level operations.
- Service rollback covers start mode and delayed automatic start.
- Scheduled task rollback covers newly created tasks and enabled-state changes.
- Startup rollback covers newly created Startup folder entries.
- Environment rollback restores tracked variable values after validation.
- Hosts file rollback restores tracked file bytes after validation.
- Firewall rollback covers newly created rules and enabled-state changes.
- Installed application rollback is manual-review only.
- File-system rollback covers newly created entries and backed-up deleted or modified files.

WinLedger stores data locally and does not add telemetry, cloud accounts, advertisements, hidden network calls, or remote analysis services.

## Documentation

- [Architecture](docs/architecture.md)
- [Data format](docs/data-format.md)
- [Rollback limitations](docs/rollback-limitations.md)
- [Threat model](docs/threat-model.md)
- [Release process](docs/release-process.md)
- [Security policy](SECURITY.md)
- [Roadmap](ROADMAP.md)

## License

WinLedger is released under the MIT License.
