param(
    [string]$CoveragePath = "artifacts\coverage",
    [double]$MinimumLineRate = 0.50,
    [double]$MinimumBranchRate = 0.40
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$resolvedPath = [System.IO.Path]::GetFullPath($CoveragePath)
if (Test-Path -LiteralPath $resolvedPath -PathType Leaf) {
    $coverageFiles = @(Get-Item -LiteralPath $resolvedPath)
}
else {
    $coverageFiles = @(Get-ChildItem -LiteralPath $resolvedPath -Recurse -Filter "coverage.cobertura.xml")
}

if ($coverageFiles.Count -eq 0) {
    throw "No Cobertura coverage files were found under $resolvedPath."
}

$lowestLineRate = 1.0
$lowestBranchRate = 1.0
foreach ($coverageFile in $coverageFiles) {
    [xml]$coverage = Get-Content -LiteralPath $coverageFile.FullName
    $lineRate = [double]$coverage.coverage.'line-rate'
    $branchRate = [double]$coverage.coverage.'branch-rate'
    $lowestLineRate = [Math]::Min($lowestLineRate, $lineRate)
    $lowestBranchRate = [Math]::Min($lowestBranchRate, $branchRate)

    Write-Output ("Coverage file: {0}" -f $coverageFile.FullName)
    Write-Output ("  Line coverage:   {0:P2}" -f $lineRate)
    Write-Output ("  Branch coverage: {0:P2}" -f $branchRate)
}

if ($lowestLineRate -lt $MinimumLineRate) {
    throw ("Line coverage {0:P2} is below the required {1:P2}." -f $lowestLineRate, $MinimumLineRate)
}

if ($lowestBranchRate -lt $MinimumBranchRate) {
    throw ("Branch coverage {0:P2} is below the required {1:P2}." -f $lowestBranchRate, $MinimumBranchRate)
}

Write-Output ("Coverage gate passed: line {0:P2}, branch {1:P2}." -f $lowestLineRate, $lowestBranchRate)
