param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('PlayMode', 'MigratePrefab')]
    [string]$Stage,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9-]+$')]
    [string]$Attempt,
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$cleanupRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$cleanupRun = Join-Path $cleanupRoot 'Temp/AudienceGraphPrefab-20261103'
$cleanupProject = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'validation-snapshot.json') -Raw | ConvertFrom-Json).project
$cleanupEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.36f1\Editor\Unity.exe'
$cleanupLog = Join-Path $cleanupRun "$Attempt.log"
$cleanupRecord = Join-Path $PSScriptRoot "$Attempt-run.json"
if ((Test-Path -LiteralPath $cleanupLog) -or (Test-Path -LiteralPath $cleanupRecord)) {
    throw 'Attempt already exists; preserve previous evidence with a new attempt name.'
}
if (-not (Test-Path -LiteralPath $cleanupEditor)) { throw 'Required Unity Editor is unavailable.' }

# Process-local Git configuration; leave global settings unchanged.
$cleanupGitConfigCount = 0
if ($env:GIT_CONFIG_COUNT) {
    if (-not [int]::TryParse($env:GIT_CONFIG_COUNT, [ref]$cleanupGitConfigCount) -or $cleanupGitConfigCount -lt 0) {
        throw 'Invalid inherited process Git configuration count.'
    }
}
[Environment]::SetEnvironmentVariable("GIT_CONFIG_KEY_$cleanupGitConfigCount", 'core.longpaths', 'Process')
[Environment]::SetEnvironmentVariable("GIT_CONFIG_VALUE_$cleanupGitConfigCount", 'true', 'Process')
$env:GIT_CONFIG_COUNT = [string]($cleanupGitConfigCount + 1)

$cleanupArgs = @('-batchmode', '-projectPath', ('"' + $cleanupProject + '"'),
    '-buildTarget', 'Android', '-logFile', ('"' + $cleanupLog + '"'), '-forgetProjectPath')
if ($Stage -eq 'PlayMode') {
    $cleanupArgs += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + (Join-Path $PSScriptRoot "$Attempt.xml") + '"'))
    if ($TestFilter) { $cleanupArgs += @('-testFilter', ('"' + $TestFilter + '"')) }
} else {
    $env:AMP_PREFAB_MIGRATION_RECORD = Join-Path $PSScriptRoot "$Attempt-migration.txt"
    $cleanupArgs += @('-executeMethod', 'AudienceGraphPrefabMigration.Apply', '-quit')
}

$cleanupStarted = [DateTime]::UtcNow
$cleanupProcess = Start-Process -FilePath $cleanupEditor -ArgumentList $cleanupArgs -WindowStyle Hidden -PassThru -Wait
[ordered]@{
    stage = $Stage
    attempt = $Attempt
    started_utc = $cleanupStarted.ToString('o')
    finished_utc = [DateTime]::UtcNow.ToString('o')
    exit_code = $cleanupProcess.ExitCode
    editor = $cleanupEditor
    arguments = $cleanupArgs
    process_git_longpaths = $true
    log = $cleanupLog
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $cleanupRecord -Encoding UTF8
Write-Output "Unity $Stage exit code: $($cleanupProcess.ExitCode)"
exit $cleanupProcess.ExitCode
