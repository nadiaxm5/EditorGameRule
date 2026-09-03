param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.1f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ResultsDirectory = Join-Path $PSScriptRoot "Results"
$LogPath = Join-Path $ResultsDirectory "unity-equivalence.log"

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null

if (-not (Test-Path $UnityPath)) {
    throw "Unity executable not found at: $UnityPath"
}

& $UnityPath `
    -batchmode `
    -projectPath $ProjectRoot `
    -executeMethod GameRuleValidation.EquivalenceValidationRunner.RunFromCommandLine `
    -logFile $LogPath

exit $LASTEXITCODE
