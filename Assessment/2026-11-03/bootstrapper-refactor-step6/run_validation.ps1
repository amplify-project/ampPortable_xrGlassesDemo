param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('PlayMode', 'Build')]
    [string]$Stage,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9-]+$')]
    [string]$Attempt,
    [string]$TestFilter,
    [switch]$UseWindowsTrustStore
)

$ErrorActionPreference = 'Stop'
$cleanupRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$cleanupRun = Join-Path $cleanupRoot 'Temp/BootstrapperRefactorStep6-20261103'
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
if ($UseWindowsTrustStore) {
    # Reuse the public-certificate store prepared for the baseline; TLS validation stays enabled.
    $cleanupTrustStore = Join-Path $env:TEMP 'rnc1030/windows-roots.p12'
    if (-not (Test-Path -LiteralPath $cleanupTrustStore)) { throw 'Baseline public-certificate store is unavailable.' }
    $env:JDK_JAVA_OPTIONS = ($env:JDK_JAVA_OPTIONS + ' -Djavax.net.ssl.trustStore="' + $cleanupTrustStore + '" -Djavax.net.ssl.trustStoreType=PKCS12 -Djavax.net.ssl.trustStorePassword=changeit -Dorg.gradle.daemon=false').Trim()
    $env:GRADLE_OPTS = ($env:GRADLE_OPTS + ' -Dorg.gradle.daemon=false').Trim()
}

$cleanupArgs = @('-batchmode', '-projectPath', ('"' + $cleanupProject + '"'),
    '-buildTarget', 'Android', '-logFile', ('"' + $cleanupLog + '"'), '-forgetProjectPath')
if ($Stage -eq 'PlayMode') {
    $cleanupArgs += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + (Join-Path $PSScriptRoot "$Attempt.xml") + '"'))
    if ($TestFilter) { $cleanupArgs += @('-testFilter', ('"' + $TestFilter + '"')) }
} else {
    $cleanupBuildOutput = Join-Path $cleanupRun $Attempt
    if (Test-Path -LiteralPath $cleanupBuildOutput) { throw 'Build output already exists.' }
    New-Item -ItemType Directory -Path $cleanupBuildOutput | Out-Null
    $env:AMP_ASSESSMENT_OUTPUT = $cleanupBuildOutput
    $cleanupArgs += @('-executeMethod', 'PreMergeValidation.InspectAndBuildAndroid', '-quit')
}

$cleanupStarted = [DateTime]::UtcNow
$cleanupProcess = Start-Process -FilePath $cleanupEditor -ArgumentList $cleanupArgs -WindowStyle Hidden -PassThru -Wait
if ($Stage -eq 'Build') {
    foreach ($cleanupArtifact in @('scene-inspection.json', 'build-summary.json')) {
        $cleanupArtifactPath = Join-Path $cleanupBuildOutput $cleanupArtifact
        if (Test-Path -LiteralPath $cleanupArtifactPath) {
            Copy-Item -LiteralPath $cleanupArtifactPath -Destination (Join-Path $PSScriptRoot "$Attempt-$cleanupArtifact")
        }
    }
}
[ordered]@{
    stage = $Stage
    attempt = $Attempt
    started_utc = $cleanupStarted.ToString('o')
    finished_utc = [DateTime]::UtcNow.ToString('o')
    exit_code = $cleanupProcess.ExitCode
    editor = $cleanupEditor
    arguments = $cleanupArgs
    process_git_longpaths = $true
    process_java_windows_trust = [bool]$UseWindowsTrustStore
    log = $cleanupLog
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $cleanupRecord -Encoding UTF8
Write-Output "Unity $Stage exit code: $($cleanupProcess.ExitCode)"
exit $cleanupProcess.ExitCode
