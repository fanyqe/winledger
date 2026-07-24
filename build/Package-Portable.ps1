param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$OutputRoot = "artifacts\release",
    [string]$DotNetPath = "dotnet",
    [switch]$FrameworkDependent,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputRootPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$packageName = "WinLedger-$Version-$Runtime-portable"
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $outputRootPath $packageName))
$appStage = Join-Path $stageRoot "app"
$cliStage = Join-Path $stageRoot "cli"
$helperStage = Join-Path $stageRoot "helper"
$zipPath = Join-Path $outputRootPath "$packageName.zip"
$selfContainedValue = if ($FrameworkDependent.IsPresent) { "false" } else { "true" }

$directorySeparators = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$outputRootWithSeparator = $outputRootPath.TrimEnd($directorySeparators) + [System.IO.Path]::DirectorySeparatorChar
if (-not $stageRoot.StartsWith($outputRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Package staging path must stay inside the output root."
}

function Resolve-DotNetPath {
    param(
        [string]$RequestedPath,
        [string]$RepositoryRoot
    )

    if ($RequestedPath -ne "dotnet") {
        return $RequestedPath
    }

    $globalJsonPath = Join-Path $RepositoryRoot "global.json"
    $requestedSdkVersion = $null
    if (Test-Path $globalJsonPath) {
        $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
        $requestedSdkVersion = $globalJson.sdk.version
    }

    $candidatePaths = @()
    if ($env:USERPROFILE) {
        $candidatePaths += Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    }

    $candidatePaths += "dotnet"

    foreach ($candidatePath in $candidatePaths) {
        try {
            $sdkList = & $candidatePath --list-sdks 2>$null
            if ($LASTEXITCODE -ne 0) {
                continue
            }

            if (-not $requestedSdkVersion -or ($sdkList | Where-Object { $_.StartsWith($requestedSdkVersion, [System.StringComparison]::Ordinal) })) {
                return $candidatePath
            }
        }
        catch {
            continue
        }
    }

    return $RequestedPath
}

$resolvedDotNetPath = Resolve-DotNetPath -RequestedPath $DotNetPath -RepositoryRoot $repoRoot

if (-not $NoRestore.IsPresent) {
    & $resolvedDotNetPath restore (Join-Path $repoRoot "WinLedger.sln") "-r" $Runtime
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }
}

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $appStage -Force | Out-Null
New-Item -ItemType Directory -Path $cliStage -Force | Out-Null
New-Item -ItemType Directory -Path $helperStage -Force | Out-Null

$publishArgs = @(
    "publish",
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContainedValue,
    "-p:PublishSingleFile=false",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

if ($NoRestore.IsPresent) {
    $publishArgs += "--no-restore"
}

& $resolvedDotNetPath @publishArgs (Join-Path $repoRoot "src\WinLedger.App\WinLedger.App.csproj") "-o" $appStage
if ($LASTEXITCODE -ne 0) {
    throw "WinLedger.App publish failed."
}

& $resolvedDotNetPath @publishArgs (Join-Path $repoRoot "src\WinLedger.Cli\WinLedger.Cli.csproj") "-o" $cliStage
if ($LASTEXITCODE -ne 0) {
    throw "WinLedger.Cli publish failed."
}

& $resolvedDotNetPath @publishArgs (Join-Path $repoRoot "src\WinLedger.ElevatedHelper\WinLedger.ElevatedHelper.csproj") "-o" $helperStage
if ($LASTEXITCODE -ne 0) {
    throw "WinLedger.ElevatedHelper publish failed."
}

Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $stageRoot "LICENSE.txt")
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $stageRoot "README.md")
Copy-Item -LiteralPath (Join-Path $repoRoot "SECURITY.md") -Destination (Join-Path $stageRoot "SECURITY.md")
Copy-Item -LiteralPath (Join-Path $repoRoot "assets") -Destination (Join-Path $stageRoot "assets") -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot "docs") -Destination (Join-Path $stageRoot "docs") -Recurse

$readmeText = @"
WinLedger Portable Release

Version: $Version
Runtime: $Runtime
Self-contained: $selfContainedValue

Start the desktop app:
  app\WinLedger.App.exe

Start the command line preview:
  cli\WinLedger.Cli.exe --help

Start the elevated rollback helper through the CLI:
  cli\WinLedger.Cli.exe elevated-rollback-apply <subsystem> <report-json> <operation-id|all> helper\WinLedger.ElevatedHelper.exe

WinLedger stores data locally under the current user's profile unless a CLI command is given an explicit database path.
Review exported reports before sharing them because they may contain local paths, installed software inventory, firewall rules, registry values, environment variables, and backed-up file bytes.
"@

Set-Content -LiteralPath (Join-Path $stageRoot "README.txt") -Value $readmeText -Encoding UTF8

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$manifest = [ordered]@{
    name = $packageName
    version = $Version
    runtime = $Runtime
    selfContained = -not $FrameworkDependent.IsPresent
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    archive = (Split-Path $zipPath -Leaf)
    sha256 = $hash.Hash
    appEntry = "app\WinLedger.App.exe"
    cliEntry = "cli\WinLedger.Cli.exe"
    helperEntry = "helper\WinLedger.ElevatedHelper.exe"
}

$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRootPath "$packageName.json") -Encoding UTF8

$sbomPath = Join-Path $outputRootPath "$packageName.sbom.json"
& (Join-Path $repoRoot "build\Generate-Sbom.ps1") `
    -SolutionPath (Join-Path $repoRoot "WinLedger.sln") `
    -OutputPath $sbomPath `
    -DotNetPath $resolvedDotNetPath `
    -PackageRoot $outputRootPath
if ($LASTEXITCODE -ne 0) {
    throw "SBOM generation failed."
}

Write-Output "Package: $zipPath"
Write-Output "Manifest: $(Join-Path $outputRootPath "$packageName.json")"
Write-Output "SBOM: $sbomPath"
Write-Output "SHA256: $($hash.Hash)"
