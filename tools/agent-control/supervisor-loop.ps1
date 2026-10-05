param([switch]$Once)
$ErrorActionPreference='Stop'
$C='D:\How_I_Fall\agent-control'
$env:CODEX_HOME='D:\Codex'
$mutex=New-Object Threading.Mutex($false,'Local\HowIFallSupervisorLoop')
if(-not $mutex.WaitOne(0)){exit 0}
try {
    do {
      try {
        if(-not (Test-Path (Join-Path $C 'MAINTENANCE')) -and -not (Test-Path (Join-Path $C 'STOP'))){
            $cfg=Get-Content (Join-Path $C 'controller.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            # Quiet while waiting on the user/auth/blocker. No repeated idle model bill.
            if($cfg.enabled -and $s.status -notin @('WAIT_USER','BLOCKED','MAINTENANCE')){
                # Dispatch models outside the Supervisor tool sandbox; never nested exec.
                $helper='wake-supervisor.ps1'; $helperArgs=@()
                if($s.status -in @('READY','CORRECTION_READY','PARTIAL_RETRY')){
                    $q=Get-Content (Join-Path $C 'queue.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                    $task=@($q.tasks | Where-Object {$_.status -in @('READY','CORRECTION_READY','PARTIAL_RETRY')} | Select-Object -First 1)
                    if(-not $task.Count){throw 'State/queue mismatch: no eligible task'}
                    $helper='hif-worker.ps1'
                    $engine=[string]$task[0].writer_engine
                    if($task[0].status -eq 'READY'){
                        $Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
                        & $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $C | Out-Null
                        if($LASTEXITCODE){throw 'Quota transport failed'}
                        $quota=Get-Content (Join-Path $C 'codex-quota.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                        if($quota.mode -eq 'UNKNOWN'){throw 'Quota UNKNOWN: dispatch blocked'}
                        $engine=if($quota.mode -eq 'QUOTA_SAVE'){'ZCode'}else{'Codex'}
                    }
                    if($engine -notin @('Codex','ZCode')){throw 'Missing original retry engine'}
                    $helperArgs=@('-Engine',$engine)
                } elseif($s.status -in @('REVIEW_CANDIDATE','REVIEW_CANDIDATE_NO_CHANGE')){
                    $helper='hif-reviewer.ps1'
                } elseif($s.status -eq 'WAIT_STRONG_REVIEW'){
                    $helper='hif-reviewer.ps1'; $helperArgs=@('-Strong')
                }
                & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $helper) @helperArgs >> (Join-Path $C 'supervisor-loop.log') 2>&1
                $code=$LASTEXITCODE
                $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                $failures=if($code -and $s.status -ne 'PARTIAL_RETRY'){[int]$s.transport_failures+1}else{0}
                $s | Add-Member -NotePropertyName transport_failures -NotePropertyValue $failures -Force
                if($failures -ge 3){
                    $s.status='BLOCKED'
                    $s | Add-Member -NotePropertyName last_error -NotePropertyValue 'Supervisor transport failed three times; manual recovery needed' -Force
                }
                $s | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $C 'state.json') -Encoding UTF8
            }
        }
      } catch {
        # A transient pre-dispatch failure must not kill the only scheduler.
        Add-Content (Join-Path $C 'supervisor-loop.log') ((Get-Date).ToString('o')+' '+$_.Exception.Message) -Encoding UTF8
        try {
            $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $failures=[int]$s.transport_failures+1
            $s | Add-Member -NotePropertyName transport_failures -NotePropertyValue $failures -Force
            $s | Add-Member -NotePropertyName last_error -NotePropertyValue $_.Exception.Message -Force
            if($failures -ge 3){$s.status='BLOCKED'}
            $s | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $C 'state.json') -Encoding UTF8
        } catch {
            # Malformed state is preserved for recovery, never replaced with empty IDLE.
            Set-Content (Join-Path $C 'STOP') 'Unreadable control state; recover from archive before resuming' -Encoding UTF8
        }
      }
        if(-not $Once){Start-Sleep -Seconds 900}
    } while(-not $Once)
} finally {try{$mutex.ReleaseMutex()}catch{}; $mutex.Dispose()}
