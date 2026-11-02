param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9-]+$')]
    [string]$Attempt,
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$refactorRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$refactorRun = Join-Path $refactorRoot 'Temp/BootstrapperRefactorStep3-20261102'
$refactorProject = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'validation-snapshot.json') -Raw | ConvertFrom-Json).project
$refactorEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.36f1\Editor\Unity.exe'
$refactorLog = Join-Path $refactorRun "$Attempt.log"
$refactorRecord = Join-Path $PSScriptRoot "$Attempt-run.json"
$refactorXml = Join-Path $PSScriptRoot "$Attempt.xml"
if (-not (Test-Path -LiteralPath $refactorEditor)) { throw 'Required Unity Editor is unavailable.' }
if ((Test-Path -LiteralPath $refactorLog) -or (Test-Path -LiteralPath $refactorRecord) -or (Test-Path -LiteralPath $refactorXml)) {
    throw 'Attempt already exists; use a new name to preserve earlier results.'
}

# Append process-local Git configuration without changing inherited or global settings.
$refactorGitConfigCount = 0
if ($env:GIT_CONFIG_COUNT) {
    if (-not [int]::TryParse($env:GIT_CONFIG_COUNT, [ref]$refactorGitConfigCount) -or $refactorGitConfigCount -lt 0) {
        throw 'Invalid inherited Git configuration count.'
    }
}
[Environment]::SetEnvironmentVariable("GIT_CONFIG_KEY_$refactorGitConfigCount", 'core.longpaths', 'Process')
[Environment]::SetEnvironmentVariable("GIT_CONFIG_VALUE_$refactorGitConfigCount", 'true', 'Process')
$env:GIT_CONFIG_COUNT = [string]($refactorGitConfigCount + 1)

$refactorArgs = @('-batchmode', '-projectPath', ('"' + $refactorProject + '"'),
    '-buildTarget', 'Android', '-logFile', ('"' + $refactorLog + '"'), '-forgetProjectPath',
    '-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + $refactorXml + '"'))
if ($TestFilter) { $refactorArgs += @('-testFilter', ('"' + $TestFilter + '"')) }
$refactorStarted = [DateTime]::UtcNow
$refactorProcess = Start-Process -FilePath $refactorEditor -ArgumentList $refactorArgs -WindowStyle Hidden -PassThru -Wait
[ordered]@{
    attempt = $Attempt
    started_utc = $refactorStarted.ToString('o')
    finished_utc = [DateTime]::UtcNow.ToString('o')
    exit_code = $refactorProcess.ExitCode
    editor = $refactorEditor
    arguments = $refactorArgs
    process_git_longpaths = $true
    log = $refactorLog
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $refactorRecord -Encoding UTF8
Write-Output "Unity PlayMode exit code: $($refactorProcess.ExitCode)"
exit $refactorProcess.ExitCode
