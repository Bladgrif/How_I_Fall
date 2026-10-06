param([switch]$Strong)
$ErrorActionPreference='Stop'
$C='D:\How_I_Fall\agent-control'
$env:CODEX_HOME='D:\Codex'
$Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if((Test-Path (Join-Path $C 'MAINTENANCE')) -or (Test-Path (Join-Path $C 'STOP'))){throw 'Maintenance: reviewer disabled'}
$mutex=New-Object Threading.Mutex($false,'Local\HowIFallWriter')
if(-not $mutex.WaitOne(0)){throw 'Writer/reviewer already running'}
try {
    $s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $q=Get-Content (Join-Path $C 'queue.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($s.status -notin @('REVIEW_CANDIDATE','REVIEW_CANDIDATE_NO_CHANGE','REVIEW_READY','WAIT_STRONG_REVIEW')){throw 'State does not allow review'}
    $t=@($q.tasks | Where-Object {$_.id -eq $s.active_task_id})
    if($t.Count -ne 1){throw 'Active task missing/ambiguous'}
    $t=$t[0]
    $Repo=[string]$t.writer_path
    if($Repo -notin @('D:\How_I_Fall\agent','D:\How_I_Fall\zagent')){throw 'Unsafe reviewer checkout'}
    $base=[string]$t.base_sha; $head=[string]$t.head_sha
    if($base -notmatch '^[a-f0-9]{40}$' -or $head -notmatch '^[a-f0-9]{40}$'){throw 'Missing exact SHA'}
    if(@(git -c ("safe.directory="+$Repo) -C $Repo status --porcelain).Count){throw 'Dirty checkout before review'}
    if((git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim() -ne $head){throw 'Reviewer HEAD mismatch'}
    $suffix=if($Strong){'strong-review-latest'}else{'review-latest'}
    $model=if($Strong){'gpt-6.1-sol'}else{'gpt-6-luna'}
    $reasoning=if($Strong){'high'}else{'low'}
    $Out=Join-Path $C ($suffix+'.json')
    $Events=Join-Path $C ($suffix+'-events.jsonl')
    # Failed reruns cannot leave an old approval at the canonical output path.
    if(Test-Path $Out){Remove-Item -LiteralPath $Out}
    if($Strong){
        & $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $C | Out-Null
        if($LASTEXITCODE){throw 'Quota reader failed'}
        $quota=Get-Content (Join-Path $C 'codex-quota.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        if($quota.mode -eq 'UNKNOWN' -or $quota.primary_remaining_percent -le 0 -or $quota.weekly_remaining_percent -le 0){
            $s.status='WAIT_STRONG_REVIEW'
            $s | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $C 'state.json') -Encoding UTF8
            Write-Output 'Strong review waits for available Sol quota; merge prohibited'
            exit 0
        }
    }
    $brief=$t | ConvertTo-Json -Depth 20
    $report=Get-Content $t.report_path -Raw -Encoding UTF8
    $prompt=@"
HIF INDEPENDENT EXACT-DIFF REVIEW. Fresh session, $model / $reasoning, READ ONLY.
Read AGENTS.md and relevant repository contracts. Repository is source of truth.
Review exact base $base ... exact head $head. Confirm scope, correctness, regression
coverage, actually executed validation and acceptance. Writer report is NOT proof.
Do not edit, stage, commit, push, switch branch, create PR or merge.
If no-change, prove acceptance; an empty diff is not acceptance.
CI is a later Supervisor gate: missing future PR CI alone is not a correction.
Set escalate=true for runtime/C#/UI/lifecycle/Save/scenes/prefabs/Packages/
ProjectSettings/workflow or automation tooling. Strong=$Strong. In the strong pass, set escalate=false: this IS the strong review.
Set visual_proof_verified=true ONLY for relevant fresh remotely accessible screenshots
that YOU inspected, bound to this head; otherwise false. Every required missing
implementation-side check/proof is validation_gaps, never silently waive it.
validation_gaps contains ONLY unmet implementation-side acceptance requirements.
Accepted read-only rerun restrictions, later PR CI, and staged rollout NOT VERIFIED
belong in summary, not blockers, unless the brief actually requires them now.
Task brief:
$brief
Writer report (verify claims independently):
$report
Return only JSON matching output schema.
"@
    $codex=(Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin" -Recurse -Filter codex.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    if(-not $codex){throw 'Bundled Codex CLI missing'}
    $configArg='model_reasoning_effort="'+$reasoning+'"'
    $old=$ErrorActionPreference; $ErrorActionPreference='Continue'
    $prompt | & $codex -C $Repo -a never -s read-only -m $model -c $configArg exec --output-schema (Join-Path $PSScriptRoot 'review-schema.json') --json -o $Out - 2>&1 | Tee-Object -FilePath $Events
    $code=$LASTEXITCODE; $ErrorActionPreference=$old
    if($code){throw ('Reviewer failed: '+$code)}
    if(@(git -c ("safe.directory="+$Repo) -C $Repo status --porcelain).Count -or (git -c ("safe.directory="+$Repo) -C $Repo rev-parse HEAD).Trim() -ne $head){throw 'Read-only reviewer changed checkout'}
    $r=Get-Content $Out -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach($entry in @(@('task_id',$t.id),@('base_sha',$base),@('head_sha',$head),@('reviewer_model',$model),@('reviewer_reasoning',$reasoning),@('reviewed_at',(Get-Date).ToUniversalTime().ToString('o')))){
        $r | Add-Member -NotePropertyName $entry[0] -NotePropertyValue $entry[1] -Force
    }
    $r | ConvertTo-Json -Depth 30 | Set-Content $Out -Encoding UTF8
    $s.status=if($Strong){'STRONG_REVIEW_READY'}else{'REVIEW_READY'}
    $s | Add-Member -NotePropertyName updated_at -NotePropertyValue (Get-Date).ToUniversalTime().ToString('o') -Force
    $s | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $C 'state.json') -Encoding UTF8
    Write-Output ($s.status+' '+$Out)
} finally {
    try{$mutex.ReleaseMutex()}catch{}
    $mutex.Dispose()
}
