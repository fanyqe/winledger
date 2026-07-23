param(
    [string]$SolutionPath = "WinLedger.sln",
    [string]$OutputPath = "artifacts\release\winledger-sbom.json",
    [string]$DotNetPath = "dotnet",
    [string]$PackageRoot = "artifacts\release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Resolve-RepositoryPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$solutionFullPath = Resolve-RepositoryPath -Path $SolutionPath
$outputFullPath = Resolve-RepositoryPath -Path $OutputPath
$packageRootFullPath = Resolve-RepositoryPath -Path $PackageRoot

$packageReportText = & $DotNetPath list $solutionFullPath package --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    throw "dotnet package report failed."
}

$packageReport = $packageReportText | ConvertFrom-Json
$componentsByKey = [ordered]@{}
foreach ($project in $packageReport.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($packageSetName in @("topLevelPackages", "transitivePackages")) {
            if (-not ($framework.PSObject.Properties.Name -contains $packageSetName)) {
                continue
            }

            foreach ($package in $framework.$packageSetName) {
                $version = if ($package.PSObject.Properties.Name -contains "resolvedVersion") {
                    $package.resolvedVersion
                }
                else {
                    $package.requestedVersion
                }

                $key = "$($package.id)@$version"
                if (-not $componentsByKey.Contains($key)) {
                    $componentsByKey[$key] = [ordered]@{
                        type = "library"
                        name = $package.id
                        version = $version
                        packageUrl = "pkg:nuget/$($package.id)@$version"
                    }
                }
            }
        }
    }
}

$artifacts = @()
if (Test-Path -LiteralPath $packageRootFullPath) {
    $artifacts = Get-ChildItem -LiteralPath $packageRootFullPath -File |
        Where-Object { $_.Extension -in @(".zip", ".json") -and $_.FullName -ne $outputFullPath } |
        Sort-Object Name |
        ForEach-Object {
            $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
            [ordered]@{
                name = $_.Name
                sha256 = $hash.Hash
                sizeBytes = $_.Length
            }
        }
}

$sbom = [ordered]@{
    bomFormat = "WinLedger-SBOM"
    specVersion = "1.0"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    solution = (Split-Path $solutionFullPath -Leaf)
    components = @($componentsByKey.Values)
    artifacts = @($artifacts)
}

New-Item -ItemType Directory -Path (Split-Path $outputFullPath -Parent) -Force | Out-Null
$sbom | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFullPath -Encoding UTF8
Write-Output "SBOM: $outputFullPath"
