param([switch]$Once)
$ErrorActionPreference='Stop'
$C='D:\How_I_Fall\agent-control'

# Fixed runtime only; refuse junction/symlink ancestors before reading or writing control.
function AssertControlPaths([string]$root) {
    foreach($relative in @('', 'queue.json', 'queue.json.tmp', 'state.json', 'state.json.tmp', 'controller.json', 'codex-quota.json', 'STOP', 'MAINTENANCE', 'logs', 'evidence')) {
        $candidate=if($relative){Join-Path $root $relative}else{$root}
        while($candidate){
            if(Test-Path -LiteralPath $candidate){
                $item=Get-Item -LiteralPath $candidate -Force
                if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Runtime reparse/ancestor escape refused'}
            }
            $parent=Split-Path -Parent $candidate
            if($parent -eq $candidate){break}; $candidate=$parent
        }
    }
}

AssertControlPaths $C
function SaveSchedulerState($value, [string]$expected) {
    $writeLock=New-Object Threading.Mutex($false,'Local\HowIFallWriter')
    $held=$false
    try {
        $held=$writeLock.WaitOne(0)
        if(-not $held){return} # Normal operator/reviewer contention, not partial persistence.
        AssertControlPaths $C
        $current=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        if($expected -and ($current | ConvertTo-Json -Depth 30 -Compress) -ne $expected){return} # Native operator won the race; never overwrite consent.
        $tmp=Join-Path $C 'state.json.tmp'
        $value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $tmp -Encoding UTF8
        Move-Item -LiteralPath $tmp -Destination (Join-Path $C 'state.json') -Force
    } catch {
        try {Set-Content -LiteralPath (Join-Path $C 'STOP') 'Fatal scheduler control write; preserve partial diff' -Encoding UTF8} catch {}
        exit 78 # Never swallow partial persistence and dispatch again.
    } finally {if($held){try{$writeLock.ReleaseMutex()}catch{}}; $writeLock.Dispose()}
}
$env:CODEX_HOME='D:\Codex'
$mutex=New-Object Threading.Mutex($false,'Local\HowIFallSupervisorLoop')
if(-not $mutex.WaitOne(0)){exit 0}
try {
    do {
      try {
        if(-not (Test-Path (Join-Path $C 'MAINTENANCE')) -and -not (Test-Path (Join-Path $C 'STOP'))){
            $cfg=Get-Content (Join-Path $C 'controller.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                $observed=$s | ConvertTo-Json -Depth 30 -Compress
            $q=$null
            if($cfg.enabled -and $s.status -notin @('WAIT_USER','WAIT_AUTH','BLOCKED','MAINTENANCE','PAUSED','STOP')){
                $q=Get-Content (Join-Path $C 'queue.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            }
            if($cfg.enabled -and ($q.batches -or (Test-Path (Join-Path $PSScriptRoot 'hif-batch.py'))) -and $s.status -notin @('WAIT_USER','WAIT_AUTH','BLOCKED','MAINTENANCE','PAUSED','STOP')){
                $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                $selection=& $Python (Join-Path $PSScriptRoot 'hif-control.py') batch-tick --control $C
                if($LASTEXITCODE -eq 78){exit 78}
                if($LASTEXITCODE){throw 'Native batch selection/scope failed'}
                $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                $observed=$s | ConvertTo-Json -Depth 30 -Compress
            }
            # Quiet while waiting on the user/auth/blocker. No repeated idle model bill.
            if($cfg.enabled -and $s.status -notin @('WAIT_USER','WAIT_AUTH','BLOCKED','MAINTENANCE','WAIT_QUOTA','PAUSED','STOP')){
                # Dispatch models outside the Supervisor tool sandbox; never nested exec.
                $helper='wake-supervisor.ps1'; $helperArgs=@(); $runHelper=$true; $nativeSync=$false
                if($s.status -eq 'WAIT_CI'){
                    $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                    $ci=& $Python (Join-Path $PSScriptRoot 'hif-control.py') ci-status --head $s.head_sha
                    if($LASTEXITCODE){throw 'CI poll transport failed'}
                    if($ci -eq 'WAIT_CI'){$runHelper=$false} # No model wake on unchanged CI.
                }
                if($s.status -in @('READY','CORRECTION_READY','PARTIAL_RETRY')){
                    $q=Get-Content (Join-Path $C 'queue.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                    $task=@($q.tasks | Where-Object {$_.status -in @('READY','CORRECTION_READY','PARTIAL_RETRY')} | Select-Object -First 1)
                    if(-not $task.Count){throw 'State/queue mismatch: no eligible task'}
                    $helper='hif-worker.ps1'
                    $engine=[string]$task[0].writer_engine
                    if($task[0].status -eq 'READY'){
                        $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                        $quotaJson=& $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $C
                        if($LASTEXITCODE){throw 'Quota transport failed'}
                        $quota=($quotaJson -join "`n") | ConvertFrom-Json
                        if($quota.mode -eq 'UNKNOWN'){throw 'Quota UNKNOWN: dispatch blocked'}
                        $engine=if($task[0].batch_id){[string]$task[0].writer_engine}elseif($quota.mode -eq 'QUOTA_SAVE'){'ZCode'}else{'Codex'}
                    }
                    if($engine -notin @('Codex','ZCode')){throw 'Missing original retry engine'}
                    $helperArgs=@('-Engine',$engine)
                } elseif($s.status -in @('REVIEW_CANDIDATE','REVIEW_CANDIDATE_NO_CHANGE')){
                    $helper='hif-reviewer.ps1'
                    $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                    $tier=& $Python (Join-Path $PSScriptRoot 'hif-control.py') review-tier --control $C
                    if($LASTEXITCODE -or $tier -notin @('cheap','strong')){throw 'Review routing failed'}
                    if($tier -eq 'strong'){$helperArgs=@('-Strong')}
                } elseif($s.status -eq 'WAIT_STRONG_REVIEW'){
                    $helper='hif-reviewer.ps1'; $helperArgs=@('-Strong')
                } elseif($s.status -eq 'SYNC_MASTER_PENDING'){
                    # Native post-merge git transport runs BEFORE any model wake; no model sandbox git writes.
                    $runHelper=$false; $nativeSync=$true
                }
                if($s.status -eq 'PLANNING_ONCE'){
                    & $Python (Join-Path $PSScriptRoot 'hif-control.py') batch-plan-claim --control $C | Out-Null
                    if($LASTEXITCODE -eq 78){exit 78}
                    if($LASTEXITCODE){throw 'Planning claim failed; no repeated wake'}
                }
                $code=0
                if($nativeSync){
                    $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                    $savedPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'
                    try {
                        & $Python (Join-Path $PSScriptRoot 'hif-control.py') sync-master --control $C 2>&1 | ForEach-Object {Add-Content (Join-Path $C 'supervisor-loop.log') $_ -Encoding UTF8}
                        $code=$LASTEXITCODE
                    } finally {$ErrorActionPreference=$savedPreference}
                } elseif($runHelper){
                    $savedPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'
                    try {
                        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $helper) @helperArgs 2>&1 | ForEach-Object {Add-Content (Join-Path $C 'supervisor-loop.log') $_ -Encoding UTF8}
                        $code=$LASTEXITCODE
                    } finally {$ErrorActionPreference=$savedPreference}
                }
                if($code -eq 78){
                    # No further dispatch in this process, independent of disk/marker availability.
                    try {
                        $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                        $observed=$s | ConvertTo-Json -Depth 30 -Compress
                        $s.status='BLOCKED'
                        SaveSchedulerState $s $observed
                        Set-Content (Join-Path $C 'STOP') 'Fatal control persistence failure; recover before resuming' -Encoding UTF8
                    } catch {}
                    break
                }
                $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                $observed=$s | ConvertTo-Json -Depth 30 -Compress
                if($s.status -eq 'PLANNING_ONCE'){$s.status='WAIT_USER'}
                $failures=if($code -and $code -ne 75){[int]$s.transport_failures+1}else{0}
                $s | Add-Member -NotePropertyName transport_failures -NotePropertyValue $failures -Force
                if($failures -ge 3){
                    $s.status='BLOCKED'
                    $s | Add-Member -NotePropertyName last_error -NotePropertyValue 'Supervisor transport failed three times; manual recovery needed' -Force
                }
                if($nativeSync -and $code -and $code -ne 78){
                    # A failed native sync blocks durably instead of spinning model wakes or retries.
                    $s.status='BLOCKED'
                    $s | Add-Member -NotePropertyName last_error -NotePropertyValue ('Native master sync failed exit '+$code) -Force
                }
                SaveSchedulerState $s $observed
            }
        }
      } catch {
        # A transient pre-dispatch failure must not kill the only scheduler.
        Add-Content (Join-Path $C 'supervisor-loop.log') ((Get-Date).ToString('o')+' '+$_.Exception.Message) -Encoding UTF8
        try {
            $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $observed=$s | ConvertTo-Json -Depth 30 -Compress
            $failures=[int]$s.transport_failures+1
            $s | Add-Member -NotePropertyName transport_failures -NotePropertyValue $failures -Force
            $s | Add-Member -NotePropertyName last_error -NotePropertyValue $_.Exception.Message -Force
            if($failures -ge 3){$s.status='BLOCKED'}
            SaveSchedulerState $s $observed
        } catch {
            # Malformed state is preserved for recovery, never replaced with empty IDLE.
            try {Set-Content (Join-Path $C 'STOP') 'Unreadable control state; recover from archive before resuming' -Encoding UTF8} catch {}
            exit 78
        }
      }
        if(-not $Once){Start-Sleep -Seconds 900}
    } while(-not $Once)
} finally {try{$mutex.ReleaseMutex()}catch{}; $mutex.Dispose()}
