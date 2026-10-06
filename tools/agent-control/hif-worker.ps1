param([ValidateSet('Codex','ZCode')][string]$Engine='Codex')

$ErrorActionPreference = 'Stop'
$ControlDir = 'D:\How_I_Fall\agent-control'
if ((Test-Path (Join-Path $ControlDir 'MAINTENANCE')) -or (Test-Path (Join-Path $ControlDir 'STOP'))) { throw 'Maintenance: worker disabled' }
$env:CODEX_HOME='D:\Codex'
$Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$Repo = if ($Engine -eq 'ZCode') { 'D:\How_I_Fall\zagent' } else { 'D:\How_I_Fall\agent' }
$QueuePath = Join-Path $ControlDir 'queue.json'
$StatePath = Join-Path $ControlDir 'state.json'
$LogDir = Join-Path $ControlDir 'logs'
New-Item -ItemType Directory -Force $LogDir | Out-Null

function NowIso { (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK') }
function WriteJson($v, $p) { $tmp=$p+'.tmp'; $v | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $tmp -Encoding UTF8; Move-Item -LiteralPath $tmp -Destination $p -Force }
function SetProp($o, $n, $v) {
    if ($o.PSObject.Properties.Name -contains $n) { $o.$n = $v }
    else { $o | Add-Member -NotePropertyName $n -NotePropertyValue $v }
}
function SaveControl($q, $s) {
    SetProp $q 'updated_at' (NowIso)
    SetProp $s 'updated_at' (NowIso)
    try {
        WriteJson $q $QueuePath
        WriteJson $s $StatePath
    } catch {
        try { Set-Content -LiteralPath (Join-Path $ControlDir 'STOP') -Value 'Durable control write failed; preserve partial diff and recover before dispatch' -Encoding UTF8 } catch { Write-Error 'STOP could not be persisted; scheduler must terminate on control-write failure' -ErrorAction Continue }
        exit 78 # Fatal control persistence failure: caller must stop even if STOP cannot be written.
    }
}
function RunGit([string[]]$a) {
    & git -c ("safe.directory="+$Repo) -C $Repo @a
    if ($LASTEXITCODE -ne 0) { throw ('git failed: ' + ($a -join ' ')) }
}
function ChangedFiles {
    $a = @(& git -c ("safe.directory="+$Repo) -C $Repo diff --name-only)
    $b = @(& git -c ("safe.directory="+$Repo) -C $Repo ls-files --others --exclude-standard)
    @($a + $b | Where-Object { $_ } | Sort-Object -Unique)
}
function Allowed($p, $allowed) {
    foreach ($a in @($allowed)) {
        if ($a.EndsWith('/')) {
            if ($p.StartsWith($a, [StringComparison]::OrdinalIgnoreCase)) { return $true }
        } elseif ($p.Equals($a, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

$mutex = New-Object Threading.Mutex($false, 'Local\HowIFallWriter')
if (-not $mutex.WaitOne(0)) { throw 'Writer/reviewer busy' }

$claimed=$false
try {
    $q = Get-Content $QueuePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $s = Get-Content $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $eligible = @('READY','CORRECTION_READY','PARTIAL_RETRY')
    $task = @($q.tasks | Where-Object { $eligible -contains $_.status } | Select-Object -First 1)
    if ($task.Count -eq 0) { exit 0 } # Never erase a pending candidate.
    $task = $task[0]
    if (@($q.tasks | Where-Object { $_.id -ne $task.id -and $_.status -notin @('DONE','READY','WAIT_USER','CANCELLED') }).Count) { throw 'Another task is active; serial writer only' }
    $id = [string]$task.id
    if ($id -notmatch '^[a-z0-9][a-z0-9-]{0,79}$') { throw 'Unsafe task ID' }
    if (-not $task.approved_source -or -not $task.base_sha -or -not $task.allowed_paths -or -not $task.validation -or -not $task.prompt) { throw 'Incomplete approved bounded task' }
    if ([string]$task.base_sha -notmatch '^[a-f0-9]{40}$') { throw 'Exact 40-character base SHA required before claim' }
    if (@($task.allowed_paths | Where-Object { $_ -match '(^[/\\]|(^|/)\.\.(/|$)|:)' }).Count) { throw 'Unsafe allowed paths' }
    & $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $ControlDir | Out-Null
    if ($LASTEXITCODE) { throw 'Quota reader failed' }
    $quota=Get-Content (Join-Path $ControlDir 'codex-quota.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($quota.mode -eq 'UNKNOWN') { throw 'Quota UNKNOWN: no automatic implementation' }
    if ($task.status -eq 'READY') {
        $requiredEngine=if($quota.mode -eq 'QUOTA_SAVE'){'ZCode'}else{'Codex'}
        if($Engine -ne $requiredEngine){throw ('Routing requires '+$requiredEngine)}
    } elseif ($task.writer_engine -and $task.writer_engine -ne $Engine) { throw 'Retry must preserve the original checkout/engine; no silent partial-diff transfer' }
    if($Engine -eq 'Codex' -and ($quota.primary_remaining_percent -le 0 -or $quota.weekly_remaining_percent -le 0)){exit 0} # Expected quota wait: preserve original task/diff.
    SetProp $task 'writer_engine' $Engine
    SetProp $task 'writer_path' $Repo
    SetProp $task 'model' $(if($Engine -eq 'ZCode'){'GLM-5.3-Flash'}else{'gpt-6.1-sol'})
    SetProp $task 'reasoning' $(if($Engine -eq 'ZCode'){'max'}else{'high'})
    $branch = if ($task.branch) { [string]$task.branch } else { 'codex/' + $id }
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $promptPath = Join-Path $LogDir ($stamp + '-' + $id + '-prompt.txt')
    $eventsPath = Join-Path $LogDir ($stamp + '-' + $id + '-events.jsonl')
    $reportPath = Join-Path $LogDir ($stamp + '-' + $id + '-report.json')

    $claimed=$true
    SetProp $s 'status' 'PREPARING'
    SetProp $s 'active_task_id' $id
    SetProp $s 'branch' $branch
    SetProp $s 'events_path' $eventsPath
    SetProp $s 'report_path' $reportPath
    SetProp $s 'last_error' $null
    SaveControl $q $s

    $mode = [string]$task.status
    if ($mode -eq 'READY') {
        $dirty = @(& git -c ("safe.directory="+$Repo) -C $Repo status --porcelain)
        if ($dirty.Count -gt 0) { throw ('Worker repo dirty before new task: ' + ($dirty -join '; ')) }
        RunGit @('fetch','origin')

        $base = (& git -c ("safe.directory="+$Repo) -C $Repo rev-parse origin/master).Trim()
        if ($base -ne [string]$task.base_sha) { throw ('Expected base mismatch: actual '+$base) }
        if (((@(& git -c ("safe.directory="+$Repo) -C $Repo branch --list $branch)) -join '').Trim()) { throw ('Local task branch exists: ' + $branch) }
        if (((@(& git -c ("safe.directory="+$Repo) -C $Repo branch -r --list ('origin/' + $branch))) -join '').Trim()) { throw ('Remote task branch exists: ' + $branch) }
        RunGit @('switch','-c',$branch,'origin/master')
        SetProp $task 'base_sha' $base
        SetProp $task 'branch' $branch
    }
    elseif ($mode -eq 'CORRECTION_READY') {
        $dirty = @(& git -c ("safe.directory="+$Repo) -C $Repo status --porcelain)
        if ($dirty.Count -gt 0) { throw ('Worker repo dirty before correction: ' + ($dirty -join '; ')) }
        RunGit @('fetch','origin')
        if (((@(& git -c ("safe.directory="+$Repo) -C $Repo branch --list $branch)) -join '').Trim()) { RunGit @('switch',$branch) }
        elseif (((@(& git -c ("safe.directory="+$Repo) -C $Repo branch -r --list ('origin/' + $branch))) -join '').Trim()) { RunGit @('switch','-c',$branch,'--track',('origin/' + $branch)) }
        else { throw ('Correction branch missing: ' + $branch) }
    }
    elseif ($mode -eq 'PARTIAL_RETRY') {
        $current = (& git -c ("safe.directory="+$Repo) -C $Repo branch --show-current).Trim()
        if ($current -ne $branch) {
            $dirty = @(& git -c ("safe.directory="+$Repo) -C $Repo status --porcelain)
            if ($dirty.Count -gt 0) { throw ('Partial diff is on unexpected branch: ' + $current) }
            RunGit @('switch',$branch)
        }
    }

    SetProp $task 'resume_head_sha' ((& git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim())
    $base = [string]$task.base_sha

    $allowedLines = (@($task.allowed_paths) | ForEach-Object { '- ' + $_ }) -join [Environment]::NewLine
    $extra = ''
    if (($task.PSObject.Properties.Name -contains 'correction') -and $task.correction) {
        $extra = [Environment]::NewLine + 'REVIEWER CORRECTION:' + [Environment]::NewLine + [string]$task.correction
    }
    if ($mode -eq 'PARTIAL_RETRY') {
        $extra += [Environment]::NewLine + 'RESUME: inspect and continue the existing task-scoped working tree diff. Do not discard or broaden it.'
    }

    $prompt = @"
HOW I FALL - AUTONOMOUS BOUNDED PASS

Environment: $Engine
Model: $($task.model)
Reasoning: $($task.reasoning)
Session: new
Task ID: $id
Exact task base: $base
Working directory: $Repo
Branch: $branch

Goal:
$($task.prompt)

Allowed repository paths:
$allowedLines

Validation:
$($task.validation)
$extra

Mandatory constraints:
- Read AGENTS.md and only relevant docs/files before editing.
- Repository decisions are source of truth. Do not invent future canon.
- Minimal task-scoped diff. Preserve unrelated work.
- Do not use destructive reset/clean, broad stash, git add ., or dangerous bypass modes.
- Do not commit, push, create or merge PRs, or switch branches. Local supervisor owns Git transport.
- Do not stop at a plan without a real blocker.
- If already satisfied, make no production change and report NO PRODUCTION CHANGE with evidence.
- Never claim PASS for tests that were not run. Use NOT RUN or NOT VERIFIED accurately.

Return ONLY JSON with keys:
status (REVIEW_CANDIDATE, NO_PRODUCTION_CHANGE, BLOCKED), base_sha,
changed_files (array), summary, validation_complete (boolean),
validation (array of actually executed checks and results), not_run (array), risks (array).
validation_complete=true only when ALL implementation acceptance checks are satisfied.
Missing required graphical proof or a required test is BLOCKED, not a success.
"@
    Set-Content $promptPath $prompt -Encoding UTF8

    $codex = Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin" -Recurse -Filter codex.exe -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $codex) { throw 'Codex app CLI not found' }

    SetProp $task 'status' 'RUNNING'
    SetProp $task 'last_run_started_at' (NowIso)
    SetProp $task 'prompt_path' $promptPath
    SetProp $s 'status' 'RUNNING'
    SaveControl $q $s

    if ($Engine -eq 'ZCode') {
        & $Python (Join-Path $PSScriptRoot 'hif-control.py') zcode-run --repo $Repo --prompt $promptPath --output $reportPath --events $eventsPath
        $code=$LASTEXITCODE
    } else {
        $old = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $configArg = 'model_reasoning_effort="high"'
        Get-Content $promptPath -Raw -Encoding UTF8 |
            & $codex.FullName -a never -s workspace-write -c 'sandbox_workspace_write.network_access=true' exec -C $Repo -m 'gpt-6.1-sol' -c $configArg --output-schema (Join-Path $PSScriptRoot 'worker-schema.json') --json -o $reportPath - 2>&1 |
            Tee-Object -FilePath $eventsPath
        $code = $LASTEXITCODE
        $ErrorActionPreference = $old
    }

    SetProp $task 'last_run_finished_at' (NowIso)

    if ($code -ne 0) {
        $quota = $false
        foreach ($p in @($eventsPath,$reportPath)) {
            if ((Test-Path $p) -and (Select-String $p -Pattern 'usage_limit_exceeded|hit your usage limit|usage limit' -Quiet)) { $quota = $true }
        }
        if ($quota) {
            SetProp $task 'status' 'PARTIAL_RETRY'
            SetProp $s 'status' 'PARTIAL_RETRY'
            SetProp $s 'last_error' ('Codex quota hit; retry next worker run. Exit=' + $code)
        } else {
            SetProp $task 'status' 'BLOCKED'
            SetProp $s 'status' 'BLOCKED'
            SetProp $s 'last_error' ('Codex failed. Exit=' + $code + '. See ' + $eventsPath)
        }
        SaveControl $q $s
        if($quota){exit 75} # Expected quota wait, not a transport/persistence failure.
        exit $code
    }

    & $Python (Join-Path $PSScriptRoot 'hif-control.py') validate-report --output $reportPath
    if($LASTEXITCODE){throw 'Malformed implementation report: no transport'}
    $result=Get-Content $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($result.status -notin @('REVIEW_CANDIDATE','NO_PRODUCTION_CHANGE') -or $result.base_sha -ne $base -or $result.validation_complete -ne $true -or -not $result.validation) { throw 'Implementation BLOCKED or missing required validation; diff preserved, no commit/push' }
    if ((& git -c ("safe.directory="+$Repo) -C $Repo branch --show-current).Trim() -ne $branch) { throw 'Agent changed branch' }
    $expectedHead=if($mode -eq 'READY'){$base}else{[string]$task.resume_head_sha}
    if ((& git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim() -ne $expectedHead) { throw 'Agent changed HEAD; refusing transport' }
    SetProp $task 'report_path' $reportPath
    $stagedByAgent = @(& git -c ("safe.directory="+$Repo) -C $Repo diff --cached --name-only)
    if ($stagedByAgent.Count -gt 0) { throw ('Codex staged files: ' + ($stagedByAgent -join ', ')) }

    $changed = @(ChangedFiles)
    if ((@($changed | Sort-Object) -join "`n") -ne (@($result.changed_files | Sort-Object) -join "`n")) { throw 'Report changed_files differs from actual diff' }
    if ($changed.Count -eq 0) {
        if ($result.status -ne 'NO_PRODUCTION_CHANGE') { throw 'Empty diff is not verified no-change acceptance' }
        $head = (& git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim()
        $candidateDiff=@(& git -c ("safe.directory="+$Repo) -C $Repo diff --name-only ($base+'...'+$head))
        if($LASTEXITCODE){throw 'Cannot classify committed candidate diff'}
        $candidateStatus=if($candidateDiff.Count){'REVIEW_CANDIDATE'}else{'REVIEW_CANDIDATE_NO_CHANGE'}
        SetProp $task 'status' $candidateStatus
        SetProp $task 'head_sha' $head
        SetProp $s 'status' $candidateStatus
        SetProp $s 'head_sha' $head
        SaveControl $q $s
        exit 0
    }

    if ($result.status -ne 'REVIEW_CANDIDATE') { throw 'NO_PRODUCTION_CHANGE report with a diff' }
    $outside = @($changed | Where-Object { -not (Allowed $_ $task.allowed_paths) })
    if ($outside.Count -gt 0) { throw ('Out-of-scope changes: ' + ($outside -join ', ')) }

    & git -c ("safe.directory="+$Repo) -C $Repo diff --check
    if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed' }

    & git -c ("safe.directory="+$Repo) -C $Repo add -- $changed
    if ($LASTEXITCODE -ne 0) { throw 'Exact-file git add failed' }

    & git -c ("safe.directory="+$Repo) -C $Repo diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'git diff --cached --check failed' }

    $staged = @(& git -c ("safe.directory="+$Repo) -C $Repo diff --cached --name-only)
    $bad = @($staged | Where-Object { -not (Allowed $_ $task.allowed_paths) })
    if ($bad.Count -gt 0) { throw ('Unexpected staged files: ' + ($bad -join ', ')) }

    & git -c ("safe.directory="+$Repo) -C $Repo commit -m ('agent: ' + $id)
    if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }
    $head = (& git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim()

    & git -c ("safe.directory="+$Repo) -C $Repo push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw 'git push failed' }

    SetProp $task 'head_sha' $head
    SetProp $task 'status' 'REVIEW_CANDIDATE'
    SetProp $task 'report_path' $reportPath
    SetProp $task 'events_path' $eventsPath
    SetProp $s 'status' 'REVIEW_CANDIDATE'
    SetProp $s 'base_sha' $base
    SetProp $s 'head_sha' $head
    SetProp $s 'branch' $branch
    SetProp $s 'last_error' $null
    SaveControl $q $s
}
catch {
    if (-not $claimed) { throw } # Preflight must not corrupt another active task.
    try {
        $q = Get-Content $QueuePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $s = Get-Content $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $id = [string]$s.active_task_id
        if ($id) {
            $x = @($q.tasks | Where-Object { $_.id -eq $id } | Select-Object -First 1)
            if ($x.Count -gt 0) { SetProp $x[0] 'status' 'BLOCKED' }
        }
        SetProp $s 'status' 'BLOCKED'
        SetProp $s 'last_error' $_.Exception.Message
        SaveControl $q $s
    } catch {
        Set-Content -LiteralPath (Join-Path $ControlDir 'STOP') -Value ('Worker recovery persistence failed: '+$_.Exception.Message) -Encoding UTF8
        Write-Error 'Control persistence failed; STOP written, partial diff preserved' -ErrorAction Continue
    }
    throw
}
finally {
    try { $mutex.ReleaseMutex() } catch {}
    $mutex.Dispose()
}
