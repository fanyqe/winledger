# Release Process

WinLedger can produce a portable Windows release package without an installer.

## Local Package

Run the validation commands first:

```powershell
dotnet restore WinLedger.sln
dotnet build WinLedger.sln --configuration Release --no-restore
dotnet test WinLedger.sln --configuration Release --no-build
dotnet format WinLedger.sln --verify-no-changes --no-restore
dotnet list WinLedger.sln package --vulnerable --include-transitive
```

Create the default self-contained portable package:

```powershell
.\build\Package-Portable.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0
```

The packaging script resolves the SDK pinned by `global.json`. If a developer machine has an older `dotnet` first on `PATH`, pass `-DotNetPath C:\Users\cekir\.dotnet\dotnet.exe`.

Create a smaller framework-dependent portable package only when the target machine already has the required .NET runtime installed:

```powershell
.\build\Package-Portable.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0 -FrameworkDependent
```

The script writes:

- `artifacts\release\WinLedger-<version>-win-x64-portable.zip`;
- `artifacts\release\WinLedger-<version>-win-x64-portable.json`.

The zip contains:

- `app\WinLedger.App.exe`;
- `cli\WinLedger.Cli.exe`;
- `helper\WinLedger.ElevatedHelper.exe`;
- license, security notes, README, and docs.

## GitHub Actions

The `ci` workflow restores, builds, tests, checks formatting, checks vulnerable packages, creates the portable package, and uploads the zip plus manifest as an artifact.

## Release Checklist

- Confirm all validation commands pass on `master`.
- Confirm the portable artifact contains both the desktop app and CLI.
- Confirm the generated manifest SHA-256 matches the uploaded zip.
- Attach the zip and manifest to the GitHub release.
- Keep release notes focused on user-visible subsystem support and rollback limitations.
