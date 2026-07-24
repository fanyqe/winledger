# Contributing

WinLedger favors deterministic, auditable behavior over heuristics. Contributions should keep local Windows change tracking predictable, reviewable, and privacy-preserving.

## Before Opening a Pull Request

Use the .NET 10 SDK pinned in `global.json`. On Windows:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" restore WinLedger.sln
& "$env:USERPROFILE\.dotnet\dotnet.exe" build WinLedger.sln --configuration Release --no-restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" test WinLedger.sln --configuration Release --no-build
& "$env:USERPROFILE\.dotnet\dotnet.exe" format WinLedger.sln --verify-no-changes --no-restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" list WinLedger.sln package --vulnerable --include-transitive
```

## Contribution Guidelines

- Keep domain models independent from Windows APIs.
- Write tests for diff, rollback, export, storage, and migration behavior when those areas change.
- Keep rollback conservative and validation-first; document limits when a rollback path is partial or manual-review only.
- Avoid telemetry, hidden network calls, cloud-only features, and background analysis services.
- Redact secrets and sensitive machine data from logs, screenshots, reports, and issue comments.
- Update README, docs, changelog, or roadmap entries when behavior, release scope, or user-facing support changes.
- Keep commit messages, pull request titles, and public project text in English.
- Do not add dependencies with licenses that conflict with MIT.

## Pull Request Checklist

- Fill in the summary, verification, rollback notes, and security/privacy impact sections from the pull request template.
- Mention any Windows behavior that could not be fully automated in tests.
- Link related issues when applicable.
