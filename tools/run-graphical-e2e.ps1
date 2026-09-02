[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('ManualSave', 'SaveBackendV2', 'PlayerUi')]
    [string]$Scenario
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

$entryPoints = @{
    ManualSave = @{ Method = 'ManualSavePlayModeE2ERunner.StartAutomatedPlayMode'; Sentinel = 'manual_save_playmode_result.txt'; ProofFiles = @('manual_save_1920x1080.png', 'gameplay_load_confirmation_1920x1080.png', 'gameplay_invalid_save_slot_1920x1080.png', 'main_menu_load_1920x1080.png') }
    SaveBackendV2 = @{ Method = 'SaveBackendV2PlayModeE2ERunner.StartAutomatedPlayMode'; Sentinel = 'save_backend_v2_playmode_result.txt'; ProofFiles = @('save_load_manual_1920x1080.png', 'save_load_auto_1920x1080.png', 'save_load_quick_1920x1080.png') }
    PlayerUi = @{ Method = 'PlayerUiGraphicalE2ERunner.StartAutomatedPlayMode'; Sentinel = 'player_ui_graphical_result.txt'; RequirePlayerPrefsRestore = $true; ProofFiles = @('main_menu_1920x1080.png', 'main_menu_settings_focus_1920x1080.png', 'main_menu_preferences_1920x1080.png', 'main_menu_preferences_screen_mode_open_1920x1080.png', 'main_menu_preferences_screen_mode_selected_1920x1080.png', 'main_menu_preferences_resolution_open_1920x1080.png', 'main_menu_preferences_resolution_selected_1920x1080.png', 'main_menu_preferences_slider_focus_1920x1080.png', 'main_menu_preferences_text_speed_max_1920x1080.png', 'main_menu_preferences_1280x720.png', 'main_menu_preferences_return_hover_1920x1080.png', 'main_menu_load_1920x1080.png', 'main_menu_quit_confirmation_1920x1080.png', 'main_menu_quit_confirmation_yes_focus_1920x1080.png', 'main_menu_1280x720.png', 'gameplay_dialogue_standard_1920x1080.png', 'gameplay_quick_save_feedback_1920x1080.png', 'gameplay_dialogue_long_125pct_1920x1080.png', 'gameplay_choice_two_1920x1080.png', 'gameplay_choice_four_long_1920x1080.png', 'gameplay_choice_four_long_1280x720.png', 'gameplay_choice_hover_1920x1080.png', 'gameplay_relationship_cue_positive_1920x1080.png', 'gameplay_relationship_cue_negative_1920x1080.png', 'gameplay_relationship_cue_mixed_1920x1080.png', 'gameplay_reading_after_relationship_cue_1920x1080.png', 'gameplay_backlog_1920x1080.png', 'gameplay_backlog_long_scroll_1920x1080.png', 'gameplay_backlog_1280x720.png', 'gameplay_auto_active_1920x1080.png', 'gameplay_skip_active_1920x1080.png', 'gameplay_hide_ui_1920x1080.png', 'game_menu_root_1920x1080.png', 'game_menu_alternate_focus_1920x1080.png', 'gameplay_preferences_1920x1080.png', 'gameplay_preferences_1280x720.png') }
}
$entryPoint = $entryPoints[$Scenario]
# Compile/import preflight: fail before any GUI launch can open Unity Safe Mode.
$preflightLogPath = Join-Path $projectRoot "Temp\CodexTests\graphical_preflight.log"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $preflightLogPath) | Out-Null
Remove-Item -LiteralPath $preflightLogPath -Force -ErrorAction SilentlyContinue
$preflightArguments = @('-batchmode', '-quit', '-projectPath', $projectRoot, '-logFile', $preflightLogPath)
$preflightArgumentLine = ($preflightArguments | ForEach-Object {
    if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
}) -join ' '
$preflight = Start-Process -FilePath $unityPath -ArgumentList $preflightArgumentLine -Wait -PassThru
if ($preflight.ExitCode -ne 0) { throw "Unity compile/import preflight failed (exit $($preflight.ExitCode)). See: $preflightLogPath" }
if ((Test-Path -LiteralPath $preflightLogPath) -and (Select-String -LiteralPath $preflightLogPath -Pattern 'error CS\d+:' -Quiet)) { throw "Unity compile/import preflight reported compilation errors. See: $preflightLogPath" }
$outputDirectory = Join-Path $projectRoot 'Temp/CodexTests'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$logPath = Join-Path $outputDirectory "graphical_${Scenario}.log"
$sentinelPath = Join-Path $projectRoot $entryPoint.Sentinel
$proofDirectory = Join-Path $projectRoot "QAArtifacts\GraphicalE2E\$Scenario"
Remove-Item -LiteralPath $logPath,$sentinelPath,$proofDirectory -Recurse -Force -ErrorAction SilentlyContinue
$runStartedUtc = [DateTime]::UtcNow

# No -nographics and no -quit: the asynchronous runner owns EditorApplication.Exit.
$arguments = @('-projectPath', $projectRoot, '-executeMethod', $entryPoint.Method, '-logFile', $logPath)
$argumentLine = ($arguments | ForEach-Object {
    if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
}) -join ' '
$process = Start-Process -FilePath $unityPath -ArgumentList $argumentLine -Wait -PassThru
$unityExitCode = $process.ExitCode
if ($unityExitCode -ne 0) { exit $unityExitCode }
if (-not (Test-Path -LiteralPath $sentinelPath)) { Write-Error "Graphical E2E exited without result sentinel: $sentinelPath"; exit 1 }
$sentinel = Get-Content -Raw -LiteralPath $sentinelPath
if ($sentinel -notmatch '(?m)^status=PASS\s*$') { Write-Error "Graphical E2E reported failure:`n$sentinel"; exit 1 }
if ($entryPoint.RequirePlayerPrefsRestore -and $sentinel -notmatch '(?m)playerPrefsRestored=true') {
    Write-Error "PlayerUi graphical E2E did not prove PlayerPrefs restoration:`n$sentinel"
    exit 1
}
foreach ($proofFile in $entryPoint.ProofFiles) {
    $proofPath = Join-Path $proofDirectory $proofFile
    if (-not (Test-Path -LiteralPath $proofPath -PathType Leaf)) { Write-Error "Graphical E2E passed without current screenshot proof: $proofPath"; exit 1 }
    $proof = Get-Item -LiteralPath $proofPath
    if ($proof.LastWriteTimeUtc -lt $runStartedUtc) { Write-Error "Screenshot proof predates this run: $proofPath"; exit 1 }
    if ($proof.Length -le 0) { Write-Error "Screenshot proof is empty: $proofPath"; exit 1 }
}
exit 0

