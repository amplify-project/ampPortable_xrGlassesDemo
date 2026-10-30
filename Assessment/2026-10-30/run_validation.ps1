param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('PlayMode', 'Build', 'Probe')]
    [string]$Stage,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9-]+$')]
    [string]$Attempt,
    [switch]$EnableGitLongPaths,
    [switch]$UseWindowsTrustStore
)

$ErrorActionPreference = 'Stop'
$assessmentRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assessmentRun = Join-Path $assessmentRoot 'Temp/PreMergeAssessment-20261030'
$assessmentProject = Join-Path $assessmentRun 'project'
$assessmentShortRecord = Join-Path $PSScriptRoot 'short-path-project.json'
if (Test-Path -LiteralPath $assessmentShortRecord) {
    $assessmentProject = (Get-Content -LiteralPath $assessmentShortRecord -Raw | ConvertFrom-Json).project
}
$assessmentEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.36f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath (Join-Path $assessmentProject 'Assets'))) {
    throw 'Capture the isolated project first.'
}

# This process and its children only; no global Git/Windows settings are changed.
if ($EnableGitLongPaths) {
    if ($env:GIT_CONFIG_COUNT) { throw 'Existing process Git config requires explicit handling.' }
    $env:GIT_CONFIG_COUNT = '1'
    $env:GIT_CONFIG_KEY_0 = 'core.longpaths'
    $env:GIT_CONFIG_VALUE_0 = 'true'
}
if ($UseWindowsTrustStore) {
    # Certificate-only temporary copy of existing Windows roots. No global trust edits.
    # Gradle 7.2 needs a file store; Unity replaces JAVA_TOOL_OPTIONS for Gradle.
    $assessmentTrustStore = Join-Path $env:TEMP 'rnc1030/windows-roots.p12'
    if (-not (Test-Path -LiteralPath $assessmentTrustStore)) {
        throw 'Prepare the temporary Windows public-certificate trust store first.'
    }
    $env:JDK_JAVA_OPTIONS = ($env:JDK_JAVA_OPTIONS + ' -Djavax.net.ssl.trustStore="' + $assessmentTrustStore + '" -Djavax.net.ssl.trustStoreType=PKCS12 -Djavax.net.ssl.trustStorePassword=changeit -Dorg.gradle.daemon=false').Trim()
    # Do not reuse a daemon launched before the process-local trust configuration.
    $env:GRADLE_OPTS = ($env:GRADLE_OPTS + ' -Dorg.gradle.daemon=false').Trim()
}

$assessmentLog = Join-Path $assessmentRun "$Attempt.log"
if ((Test-Path -LiteralPath $assessmentLog) -or
    (Test-Path -LiteralPath (Join-Path $PSScriptRoot "$Attempt-run.json"))) {
    throw 'Attempt already exists; choose a new name to preserve previous evidence.'
}
$assessmentArgs = @('-batchmode', '-projectPath', ('"' + $assessmentProject + '"'),
    '-buildTarget', 'Android', '-logFile', ('"' + $assessmentLog + '"'), '-forgetProjectPath')
if ($Stage -eq 'PlayMode') {
    $assessmentXml = Join-Path $assessmentRun "$Attempt.xml"
    $assessmentArgs += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + $assessmentXml + '"'))
} elseif ($Stage -eq 'Build') {
    $assessmentBuildOutput = Join-Path $assessmentRun $Attempt
    if (Test-Path -LiteralPath $assessmentBuildOutput) { throw 'Build output directory already exists.' }
    New-Item -ItemType Directory -Path $assessmentBuildOutput | Out-Null
    $env:AMP_ASSESSMENT_OUTPUT = $assessmentBuildOutput
    $assessmentHelper = Join-Path $assessmentProject 'Assets/Editor/PreMergeValidation.cs'
    New-Item -ItemType Directory -Path (Split-Path -Parent $assessmentHelper) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PreMergeValidation.cs') -Destination $assessmentHelper
    $assessmentArgs += @('-executeMethod', 'PreMergeValidation.InspectAndBuildAndroid', '-quit')
} else {
    $assessmentProbe = Join-Path $assessmentProject 'Assets/Editor/MonoScriptProbe.cs'
    New-Item -ItemType Directory -Path (Split-Path -Parent $assessmentProbe) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MonoScriptProbe.cs') -Destination $assessmentProbe
    $assessmentArgs += @('-executeMethod', 'MonoScriptProbe.Inspect', '-quit')
}

$assessmentStarted = [DateTime]::UtcNow
$assessmentProcess = Start-Process -FilePath $assessmentEditor -ArgumentList $assessmentArgs -WindowStyle Hidden -PassThru -Wait
if ($Stage -eq 'Build') {
    foreach ($assessmentArtifact in @('scene-inspection.json', 'build-summary.json', 'RayNeo-assessment.apk')) {
        $assessmentArtifactPath = Join-Path $assessmentBuildOutput $assessmentArtifact
        if ((Test-Path -LiteralPath $assessmentArtifactPath) -and
            ([System.IO.Path]::GetFullPath($assessmentArtifactPath) -ne
             [System.IO.Path]::GetFullPath((Join-Path $assessmentRun $assessmentArtifact)))) {
            Copy-Item -LiteralPath $assessmentArtifactPath -Destination (Join-Path $assessmentRun $assessmentArtifact)
        }
    }
} elseif ($Stage -eq 'Probe') {
    $assessmentProbeResult = Join-Path (Split-Path -Parent $assessmentProject) 'mono-script-probe.json'
    if (Test-Path -LiteralPath $assessmentProbeResult) {
        Copy-Item -LiteralPath $assessmentProbeResult -Destination (Join-Path $assessmentRun 'mono-script-probe.json')
    }
}
$assessmentResult = [ordered]@{
    stage = $Stage
    attempt = $Attempt
    started_utc = $assessmentStarted.ToString('o')
    finished_utc = [DateTime]::UtcNow.ToString('o')
    exit_code = $assessmentProcess.ExitCode
    editor = $assessmentEditor
    arguments = $assessmentArgs
    process_git_longpaths = [bool]$EnableGitLongPaths
    process_java_windows_trust = [bool]$UseWindowsTrustStore
    java_trust_store_path = $assessmentTrustStore
    log = $assessmentLog
}
$assessmentResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot "$Attempt-run.json") -Encoding UTF8
Write-Output "Unity $Stage exit code: $($assessmentProcess.ExitCode)"
exit $assessmentProcess.ExitCode
