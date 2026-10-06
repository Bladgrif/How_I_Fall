param()
$ErrorActionPreference='Stop'
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $OutputEncoding
$C='D:\How_I_Fall\agent-control'
$env:CODEX_HOME='D:\Codex'
$Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if((Test-Path (Join-Path $C 'MAINTENANCE')) -or (Test-Path (Join-Path $C 'STOP'))){exit 0}
$mutex=New-Object Threading.Mutex($false,'Local\HowIFallSupervisorWake')
if(-not $mutex.WaitOne(0)){exit 0}
try {
    $cfg=Get-Content (Join-Path $C 'controller.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $cfg.enabled){exit 0}
    & $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $C | Out-Null
    if($LASTEXITCODE){throw 'Quota reader failed'}
    $quota=Get-Content (Join-Path $C 'codex-quota.json') -Raw -Encoding UTF8
    $prompt=@"
HIF SUPERVISOR WAKE. Continue from durable state, do not restart completed work.
Read D:\How_I_Fall\master\AGENTS.md and docs/product/agent_orchestration.md.
Repository docs/technical_plan.md is the approved roadmap; compare optional Drive mirror when tools are available. Missing mirror access never blocks selection.
Preserve protected D:\How_I_Fall\develop: NO edits, reset, clean, stash, tests or import there.
Use ONE queue.json and state.json in D:\How_I_Fall\agent-control.
Do NOT invoke writer/reviewer helpers, git transport or model processes from your sandbox.
The ONE outer scheduler dispatches them after this turn ends. Your job is durable
selection/routing and connected PR/CI/merge/roadmap tools, not nested exec.
Helpers (same directory):
  hif-worker.ps1 -Engine Codex|ZCode (one serial writer, exact approved bounded task)
  hif-reviewer.ps1 (fresh independent Luna Low); -Strong (fresh Sol High)
  & "$Python" "$PSScriptRoot\hif-control.py" merge-gate (read-only exact-head PR/CI/review gate)
Quota snapshot (fresh): $quota
NORMAL: Sol6.1 High writer. QUOTA_SAVE <=25% either window: Flash Max writer,
including runtime/UI as explicitly authorized by user. UNKNOWN: wait, never guess.
High-risk candidates always need CLEAN Sol High independent review. If unavailable,
WAIT_STRONG_REVIEW; no merge, no substitution of GLM/Luna for strong review.
Never transfer a partial diff to another engine silently. Preserve original retry branch.
Do not invoke yourself recursively or run additional worker/supervisor loops.
Before writer, set id, approved_source (repository decision/approved roadmap),
exact base_sha from current remote master, bounded prompt, allowed_paths,
validation/acceptance, risk and player_facing in queue.json. Production tasks must
be supported by approved source. Set state.status=READY, then end this turn so the
scheduler invokes the writer. At most ONE active task across both engines.
Scheduler sends high-risk candidate DIRECTLY to fresh Sol High review; low-risk to Luna.
No mandatory cheap first pass for significant work. Read REVIEW_READY result:
correction -> task AND state CORRECTION_READY on same checkout; high risk or
escalation -> state WAIT_STRONG_REVIEW, end turn; STRONG_REVIEW_READY must be CLEAN.
Clean accepted candidate -> create GitHub PR using plugin, record task.pr_number.
Do not merge until & "$Python" "$PSScriptRoot\hif-control.py" merge-gate succeeds NOW, then plugin merge
MUST specify expected_head_sha from receipt (receipt <=5 minutes old). Any head or
master change invalidates gate. CI cannot be replaced with local tests/docs-only proof. Set state.status=WAIT_CI
and preserve exact head_sha while current CI jobs are pending; never treat an old
failure/success as current if a rerun is in progress.
After merge verify GitHub master and merge SHA via plugin, record task.merge_sha,
set task AND state SYNC_MASTER_PENDING, then end this turn: the outer scheduler
runs native hif-control.py sync-master outside your sandbox (fixed clean master
checkout, controller.native_master_sync_enabled). Never run git writes yourself.
In ROADMAP_SYNC_READY keep the repository roadmap current - docs/technical_plan.md
is the single approved-task entrypoint (SourceRepoPlan), repository roadmap updates
belong to the merged candidate diff. Drive roadmap is an optional research/history
mirror: sync it when Drive write works; if unavailable explicitly mark Drive stale
and set task DONE with a durable next action. Missing/stale Drive sync never blocks
DONE after the other gates. Never silently claim a synced Drive.
If queue ends: check approved roadmap/confirmed defects once. If exhausted, propose
at most 3 new product tasks with acceptance in proposals.md, set WAIT_USER, stop.
Do not execute unapproved product proposals or invent features/canon to burn tokens.
User approval arrives in Codex: user never relays prompts between apps.
For real auth/CI/quota/blocker wait durably. Retry failed transport at most 3 times,
then BLOCKED. Stay quiet on unchanged waits; notify only completion/failure/user decision.
Use GitHub and Google Drive plugins directly. Browser ChatGPT is not required.
Finish with concise real progress and durable next action. Never report unrun PASS.
"@
    $codex=(Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin" -Recurse -Filter codex.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    if(-not $codex){throw 'Bundled Codex CLI missing'}
    $old=$ErrorActionPreference; $ErrorActionPreference='Continue'
    # User explicitly authorized autonomous HIF merge. Override only this tool,
    # only in this Supervisor process; no global app/default permission change.
    $prompt | & $codex -C 'D:\How_I_Fall\master' -a never -s workspace-write -c 'sandbox_workspace_write.network_access=true' -c 'apps.connector_76869538009648d5b282a4bb21c3d157.tools.merge_pull_request.approval_mode="approve"' --add-dir $C --add-dir 'D:\How_I_Fall\agent' --add-dir 'D:\How_I_Fall\zagent' -m 'gpt-6-luna' -c 'model_reasoning_effort="low"' exec resume $cfg.supervisor_thread_id - 2>&1 | Tee-Object -FilePath (Join-Path $C 'supervisor-wake.log') -Append
    $code=$LASTEXITCODE; $ErrorActionPreference=$old
    if($code){throw ('Supervisor transport failed: '+$code)}
} finally {try{$mutex.ReleaseMutex()}catch{}; $mutex.Dispose()}
