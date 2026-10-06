param()
$ErrorActionPreference='Stop'
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $OutputEncoding
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
$env:CODEX_HOME='D:\Codex'
$Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if((Test-Path (Join-Path $C 'MAINTENANCE')) -or (Test-Path (Join-Path $C 'STOP'))){exit 0}
$mutex=New-Object Threading.Mutex($false,'Local\HowIFallSupervisorWake')
if(-not $mutex.WaitOne(0)){exit 0}
try {
    $cfg=Get-Content (Join-Path $C 'controller.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $cfg.enabled){exit 0}
    $quotaJson=& $Python (Join-Path $PSScriptRoot 'hif-control.py') quota --control $C
    if($LASTEXITCODE){throw 'Quota reader failed'}
    $quota=$quotaJson -join "`n"
    $prompt=@"
HIF SUPERVISOR WAKE. NEW bounded Luna Low session; ONE durable Supervisor role.
Read controller.json, queue.json, state.json, merge-gate.json and matching latest review receipts
from existing control. Historical supervisor_thread_id is NOT a session to resume.
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
Native scheduler alone materializes READY from an explicitly operator-approved queue.batches
packet. Never set READY from source-only/roadmap/model claim, never edit signed definitions,
approval receipts or dispatch identities. Preserve the three LIST_APPROVED_NOT_DISPATCHED
legacy game items and DONE history. Native scope guards run before writer/reviewer/merge.
If state PLANNING_ONCE: draft ONE packet of 1..15 bounded tasks from repository roadmap,
goal, scope, protected_contracts, acceptance, dependencies, risk, fixed QA selection.
Use native hif-control.py batch-propose --output <packet file inside control>; it stores proposal
in SAME queue.json, not proposals.md or a second execution tracker. No features/canon padding.
Subjective UI choices belong in morning definitions, objective QA is autonomous.
A proposal/approved_source is NOT user consent. Only interactive native batch-approve can
record an operator decision. Do not invoke approval, forge seals, self-approve or dispatch.
After planning end turn: native scheduler sets WAIT_USER and never endlessly repeats it.
Each approved Flash task MUST explicitly select native_validation_profile:
agent-control (existing fixed infrastructure fixtures) or hif-runtime with native_qa
{unity:[{mode,filter}],graphical:[Scenario]}. Use ONLY hif-control.py's fixed whitelist;
never commands/executables/extra arguments. Unproven save isolation blocks selection.
PARTIAL_RETRY after native implementation failure retains SAME writer_path/engine/
branch/resume_head_sha and existing diff; at most 2 native correction attempts.
Do not reroute a retry when fresh quota crosses 25%; fallback applies to NEXT task.
WAIT_VISUAL_PROOF: publish native manifest + at most 3 originals per Scenario via
existing visual-review artifact (only actually changed curated baselines), otherwise
Drive proof folder, otherwise small evidence/<task> branch. You own this transport,
not Flash. Record native_remote_proof={route,url,head_sha,manifest_sha256} in task;
verify upload/readback hashes and exact candidate head. Never stage QAArtifacts/build
payload in task PR. On success restore state/task REVIEW_CANDIDATE for Sol High;
if permitted routes fail after attempts: BLOCKED, REVIEWER VISUAL PROOF NOT AVAILABLE,
no merge. Missing local proof is BLOCKED, never bypass native QA via writer report.
Scheduler sends high-risk candidate DIRECTLY to fresh Sol High review; low-risk to Luna.
Read independent results from D:\How_I_Fall\agent-control\strong-review-latest.json
or review-latest.json and verify task_id/base_sha/head_sha. A matching CLEAN result
must NOT be rerun just because the older writer report says review was pending.
For a CLEAN reviewed candidate with pending CI, set WAIT_CI, not REVIEW_CANDIDATE.
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
If queue ends: only the native PLANNING_ONCE request permits a single proposal pass.
Otherwise WAIT_USER, no unapproved execution. Never ask approval for each safe step
inside an approved scope. CI failures/deterministic fixes use bounded corrections, same
identity/engine/checkout; true user decisions/auth blockers wait durably.
For real auth/CI/quota/blocker wait durably. Retry failed transport at most 3 times,
then BLOCKED. Stay quiet on unchanged waits; notify only completion/failure/user decision.
Use GitHub and Google Drive plugins directly. Browser ChatGPT is not required.
Finish with concise real progress and durable next action. Never report unrun PASS.
"@
    $codex=(Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin" -Recurse -Filter codex.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    if(-not $codex){throw 'Bundled Codex CLI missing'}
    $old=$ErrorActionPreference; $ErrorActionPreference='Continue'
    # User authorized scoped HIF PR creation AND merge. Override only these two tools,
    # only in this Supervisor process; no global app/default permission change.
    $prompt | & $codex -C 'D:\How_I_Fall\agent' -a never -s workspace-write -c 'sandbox_workspace_write.network_access=true' -c 'apps.connector_76869538009648d5b282a4bb21c3d157.tools.create_pull_request.approval_mode="approve"' -c 'apps.connector_76869538009648d5b282a4bb21c3d157.tools.merge_pull_request.approval_mode="approve"' --add-dir $C --add-dir 'D:\How_I_Fall\agent' --add-dir 'D:\How_I_Fall\zagent' -m 'gpt-6-luna' -c 'model_reasoning_effort="low"' exec - 2>&1 | Tee-Object -FilePath (Join-Path $C 'supervisor-wake.log') -Append
    $code=$LASTEXITCODE; $ErrorActionPreference=$old
    if($code){throw ('Supervisor transport failed: '+$code)}
} finally {try{$mutex.ReleaseMutex()}catch{}; $mutex.Dispose()}
