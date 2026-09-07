param(
    [Parameter(Mandatory=$true)][string]$Unity,
    [int]$StartupTimeoutSeconds = 300,
    [int]$SampleTimeoutSeconds = 660
)
$ErrorActionPreference = 'Stop'
$evaluationRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = (Resolve-Path (Join-Path $evaluationRoot '../..')).Path
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6)
$runRoot = Join-Path $evaluationRoot ('runs/' + $runId)
New-Item -ItemType Directory -Path $runRoot | Out-Null
$privateLogs = Join-Path $evaluationRoot '.work'
New-Item -ItemType Directory -Path $privateLogs -Force | Out-Null
$privateLog = Join-Path $privateLogs ($runId + '.log')
$oldRunId = $env:GR_EVAL_RUN_ID
$env:GR_EVAL_RUN_ID = $runId
$started = [DateTime]::UtcNow
$reason = 'process_exit'
$lastSample = ''
$sampleStart = $started
$progress = $null
try {
    # -batchmode/-nographics are deliberately absent: attached production EditorWindow layout is required.
    $arguments = @('-projectPath', ('"' + $projectRoot + '"'), '-executeMethod',
        'GameRuleEvaluation.RobustnessScaleEvaluation.RunCommandLine', '-logFile', ('"' + $privateLog + '"'))
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
    while (!$process.WaitForExit(1000)) {
        $progressPath = Join-Path $runRoot 'progress.json'
        if (Test-Path -LiteralPath $progressPath) {
            try { $progress = Get-Content -Raw -LiteralPath $progressPath | ConvertFrom-Json } catch { continue }
            $sample = "$($progress.sample.kind)-$($progress.sample.case_id)-$($progress.sample.repetition)"
            if ($sample -ne $lastSample) { $lastSample = $sample; $sampleStart = [DateTime]::UtcNow }
            if (([DateTime]::UtcNow - $sampleStart).TotalSeconds -gt $SampleTimeoutSeconds) {
                $reason = 'sample_timeout'; break
            }
        } elseif (([DateTime]::UtcNow - $started).TotalSeconds -gt $StartupTimeoutSeconds) {
            $reason = 'startup_timeout'; break
        }
    }
    if (!$process.HasExited) {
        # Kill only the exact process launched here. No recursive filesystem operations.
        $process.Kill()
        $process.WaitForExit()
    }
    $record = [ordered]@{
        run_id = $runId; started_utc = $started.ToString('O'); finished_utc = [DateTime]::UtcNow.ToString('O')
        outcome = $reason; exit_code = $process.ExitCode
        startup_timeout_seconds = $StartupTimeoutSeconds; sample_timeout_seconds = $SampleTimeoutSeconds
        last_stage = $progress.stage; last_sample = $progress.sample
        git_commit = (& git -C $projectRoot rev-parse HEAD)
        arguments = @('-projectPath','<PROJECT>','-executeMethod','GameRuleEvaluation.RobustnessScaleEvaluation.RunCommandLine','-logFile','<PRIVATE_LOG>')
    }
    $record | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $runRoot 'launcher.json') -Encoding utf8
    if (Test-Path -LiteralPath $privateLog) {
        # Only retain diagnostic/build lines. Raw Unity logs may contain hostnames, account/license IDs and paths.
        $evidence = Get-Content -LiteralPath $privateLog | Where-Object {
            $_ -match 'error CS[0-9]+|Compilation failed|Aborting batchmode|Scripts have compiler errors|Licensing is not yet initialized|The connection with the Unity Licensing Client has been lost|Successfully launched the LicensingClient|Built from |^OS: |^BatchMode: |^Date: |Application will terminate'
        } | ForEach-Object {
            $_.Replace($projectRoot,'<PROJECT>').Replace($projectRoot.Replace('\','/'),'<PROJECT>').Replace($env:USERPROFILE,'<USER>') -replace '\(PId: [0-9]+\)', '(PID redacted)'
        }
        $evidence | Set-Content -LiteralPath (Join-Path $runRoot 'unity-diagnostics.txt') -Encoding utf8
    }
    Write-Output ('Run: ' + $runId + '; outcome: ' + $reason + '; exit: ' + $process.ExitCode)
} finally {
    $env:GR_EVAL_RUN_ID = $oldRunId
}
