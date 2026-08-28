[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('EditMode', 'PlayMode', 'Smoke')]
    [string]$Mode,
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) { throw "ProjectVersion.txt was not found: $versionFile" }
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
if ($versionLine -notmatch '^m_EditorVersion:\s*(.+)$') { throw "Could not read Unity version from $versionFile" }
$unityVersion = $Matches[1].Trim()
$unityPath = if ($env:UNITY_EDITOR_PATH) { $env:UNITY_EDITOR_PATH } else { Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe" }
if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) { throw "Unity $unityVersion was not found. Set UNITY_EDITOR_PATH or install it through Unity Hub. Checked: $unityPath" }

$outputDirectory = Join-Path $projectRoot 'Temp/CodexTests'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$filterSuffix = if ([string]::IsNullOrWhiteSpace($TestFilter)) { 'all' } else { ($TestFilter -replace '[^A-Za-z0-9_.-]', '_') }
$resultPath = Join-Path $outputDirectory "${Mode}_${filterSuffix}_results.xml"
$logPath = Join-Path $outputDirectory "${Mode}_${filterSuffix}.log"
Remove-Item -LiteralPath $resultPath,$logPath -Force -ErrorAction SilentlyContinue

if ($Mode -eq 'Smoke') {
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) { throw 'TestFilter is not supported with Smoke mode.' }
    # The smoke runner controls EditorApplication.Exit after reporting every check.
    $arguments = @('-batchmode', '-projectPath', $projectRoot, '-executeMethod', 'HowIFallCiSmokeTests.RunAll', '-logFile', $logPath)
} else {
    $arguments = @('-batchmode', '-projectPath', $projectRoot, '-runTests', '-testPlatform', $Mode, '-testResults', $resultPath, '-logFile', $logPath)
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) { $arguments += @('-testFilter', $TestFilter) }
}
$argumentLine = ($arguments | ForEach-Object {
    if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
}) -join ' '
$process = Start-Process -FilePath $unityPath -ArgumentList $argumentLine -Wait -PassThru
exit $process.ExitCode
