"""Локальные transport/gates HIF. Без сторонних пакетов и собственных model loops."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import threading
import urllib.request


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def save(path, value):
    path = Path(path)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def codex_rpc(method, params):
    bins = list((Path(os.environ["LOCALAPPDATA"]) / "OpenAI/Codex/bin").rglob("codex.exe"))
    if not bins:
        raise RuntimeError("Bundled Codex CLI not found")
    exe = max(bins, key=lambda p: p.stat().st_mtime)
    env = dict(os.environ, CODEX_HOME="D:\\Codex")
    proc = subprocess.Popen([str(exe), "app-server"], stdin=subprocess.PIPE,
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                            text=True, encoding="utf-8", env=env)
    incoming = queue.Queue()

    def reader():
        for line in proc.stdout:
            try:
                incoming.put(json.loads(line))
            except json.JSONDecodeError:
                pass
        incoming.put({"error": "app-server closed"})

    threading.Thread(target=reader, daemon=True).start()

    def send(value):
        proc.stdin.write(json.dumps(value) + "\n")
        proc.stdin.flush()

    def receive(ident):
        deadline = dt.datetime.now() + dt.timedelta(seconds=40)
        while True:
            remaining = (deadline - dt.datetime.now()).total_seconds()
            if remaining <= 0:
                raise TimeoutError("Codex RPC timeout")
            value = incoming.get(timeout=remaining)
            if value.get("id") == ident:
                if "error" in value:
                    raise RuntimeError(str(value["error"]))
                return value["result"]
            if value.get("error"):
                raise RuntimeError(str(value["error"]))
            # Reject unexpected server requests rather than silently approving them.
            if "id" in value and "method" in value:
                send({"id": value["id"], "error": {"code": -32601, "message": "No approvals in maintenance RPC"}})

    try:
        send({"id": 0, "method": "initialize", "params": {
            "clientInfo": {"name": "hif_control", "version": "1.0"},
            "capabilities": {"experimentalApi": True}}})
        receive(0)
        send({"method": "initialized", "params": {}})
        send({"id": 1, "method": method, "params": params})
        return receive(1)
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()


def quota_result(raw):
    # Account-wide Codex limits, not an unrelated model-specific bucket.
    bucket = raw.get("rateLimitsByLimitId", {}).get("codex") or raw.get("rateLimits", {})
    windows = [bucket.get("primary"), bucket.get("secondary")]
    if not all(isinstance(w, dict) and isinstance(w.get("usedPercent"), (int, float)) for w in windows):
        raise ValueError("Incomplete Codex quota windows")
    remaining = [max(0, 100 - w["usedPercent"]) for w in windows]
    return {"updated_at": now(), "source": "account/rateLimits/read", "mode":
            "QUOTA_SAVE" if min(remaining) <= 25 else "NORMAL",
            "threshold_remaining_percent": 25, "primary_remaining_percent": remaining[0],
            "weekly_remaining_percent": remaining[1], "windows": windows}


def high_risk(paths):
    paths = [p.replace("\\", "/") for p in paths]
    return any(re.search(r"\.(cs|unity|prefab|uxml|uss|shader|shadergraph)$", p, re.I) or
               p.startswith(("Packages/", "ProjectSettings/", "tools/", ".github/"))
               for p in paths)


def validate_worker_report(report):
    expected = {"status", "base_sha", "summary", "validation_complete", "changed_files", "validation", "not_run", "risks"}
    if not isinstance(report, dict) or set(report) != expected:
        raise ValueError("Worker report keys/schema mismatch")
    if report["status"] not in ("REVIEW_CANDIDATE", "NO_PRODUCTION_CHANGE", "BLOCKED"):
        raise ValueError("Invalid worker status")
    if type(report["validation_complete"]) is not bool:
        raise ValueError("validation_complete must be boolean")
    for key in ("base_sha", "summary"):
        if not isinstance(report[key], str):
            raise ValueError("Invalid string field " + key)
    for key in ("changed_files", "validation", "not_run", "risks"):
        if not isinstance(report[key], list) or not all(isinstance(v, str) for v in report[key]):
            raise ValueError("Invalid string-array field " + key)
    if not re.fullmatch(r"[a-f0-9]{40}", report["base_sha"]):
        raise ValueError("Invalid worker base SHA")


def review_ok(review, task, strong=False):
    keys = {"verdict", "risk", "escalate", "summary", "findings", "validation_gaps", "visual_proof_verified",
            "task_id", "base_sha", "head_sha", "reviewer_model", "reviewer_reasoning", "reviewed_at"}
    if not isinstance(review, dict) or set(review) != keys:
        raise ValueError("Independent review keys/schema mismatch")
    if review["risk"] not in ("low", "medium", "high") or type(review["escalate"]) is not bool or type(review["visual_proof_verified"]) is not bool:
        raise ValueError("Independent review enum/boolean types invalid")
    if not isinstance(review["findings"], list) or not isinstance(review["validation_gaps"], list):
        raise ValueError("Independent review findings/gaps must be arrays")
    if not all(isinstance(gap, str) for gap in review["validation_gaps"]):
        raise ValueError("Invalid validation gap type")
    for key in ("summary", "task_id", "base_sha", "head_sha", "reviewer_model", "reviewer_reasoning", "reviewed_at"):
        if not isinstance(review[key], str):
            raise ValueError("Invalid review string field " + key)
    expected = (task["id"], task["base_sha"], task["head_sha"])
    actual = tuple(review.get(k) for k in ("task_id", "base_sha", "head_sha"))
    if actual != expected or review.get("verdict") != "CLEAN":
        raise ValueError("Missing, stale or non-clean independent review")
    if review.get("validation_gaps") or review.get("findings"):
        raise ValueError("Unresolved review findings/validation gaps")
    if strong and (review.get("reviewer_model") != "gpt-6.1-sol" or review.get("reviewer_reasoning") != "high"):
        raise ValueError("Sol High strong review required")


def needs_strong(task, changed, cheap=None):
    if high_risk(changed) or task.get("risk") == "high" or task.get("player_facing"):
        return True
    bound = cheap and all(cheap.get(k) == task.get(t) for k, t in
                         (("task_id", "id"), ("base_sha", "base_sha"), ("head_sha", "head_sha")))
    return bool(bound and (cheap.get("risk") == "high" or cheap.get("escalate")))


def ci_latest(checks, head):
    names = {"CI Gate", "CI change classification", "Unity Test Framework", "Unity smoke tests"}
    relevant = [run for run in checks.get("check_runs", [])
                if run.get("name") in names and run.get("head_sha") == head
                and run.get("app", {}).get("slug") == "github-actions"]
    # A new suite without its own Gate must never inherit the previous suite's Gate.
    if relevant:
        suite = max(relevant, key=lambda run: run["id"]).get("check_suite", {}).get("id")
        relevant = [run for run in relevant if run.get("check_suite", {}).get("id") == suite]
    latest = {}
    for run in relevant:
        if run["name"] not in latest or run["id"] > latest[run["name"]]["id"]:
            latest[run["name"]] = run
    return latest


def ci_pending(checks, head):
    workflow = checks.get("workflow_run", {})
    return (workflow.get("head_sha") != head or workflow.get("status") != "completed"
            or any(run.get("status") != "completed" for run in ci_latest(checks, head).values()))


def gate(task, cheap, strong, pr, checks, changed):
    strong_required = needs_strong(task, changed, cheap)
    final_review = strong if strong_required else cheap
    review_ok(final_review or {}, task, strong=strong_required)
    if pr.get("state") != "open" or pr.get("draft") or pr.get("head", {}).get("sha") != task["head_sha"]:
        raise ValueError("PR is not an open exact-head candidate")
    if pr.get("base", {}).get("ref") != "master" or pr.get("mergeable") is not True:
        raise ValueError("PR mergeability/base not verified")
    if pr.get("base", {}).get("sha") != task["base_sha"]:
        raise ValueError("Master moved: update candidate, validate and re-review")
    if ci_pending(checks, task["head_sha"]):
        raise ValueError("Exact-head CI jobs are still running; old success cannot authorize merge")
    if checks["workflow_run"].get("conclusion") != "success" or any(run.get("conclusion") not in ("success", "skipped") for run in ci_latest(checks, task["head_sha"]).values()):
        raise ValueError("A latest exact-head CI job failed; old success cannot authorize merge")
    latest = ci_latest(checks, task["head_sha"]).get("CI Gate")
    if not latest:
        raise ValueError("Exact-head CI Gate missing")
    if latest.get("status") != "completed" or latest.get("conclusion") != "success":
        raise ValueError("Latest exact-head CI Gate is not GREEN")
    if task.get("player_facing") and not final_review.get("visual_proof_verified"):
        raise ValueError("Reviewer-visible graphical proof not verified")
    return {"status": "MERGE_ALLOWED", "task_id": task["id"], "head_sha": task["head_sha"],
            "checked_at": now(), "ci_check_id": latest["id"], "pr_number": pr["number"],
            "ci_run_id": checks["workflow_run"]["id"], "ci_run_attempt": checks["workflow_run"]["run_attempt"]}


def ci_status(checks, head):
    if ci_pending(checks, head):
        return "WAIT_CI"
    if checks["workflow_run"].get("conclusion") != "success" or any(run.get("conclusion") not in ("success", "skipped") for run in ci_latest(checks, head).values()):
        return "FAILED"
    latest = ci_latest(checks, head).get("CI Gate")
    if not latest or latest.get("status") != "completed":
        return "WAIT_CI"
    return "GREEN" if latest.get("conclusion") == "success" else "FAILED"


def github(path):
    req = urllib.request.Request("https://api.github.com/repos/Bladgrif/How_I_Fall/" + path,
                                 headers={"Accept": "application/vnd.github+json", "User-Agent": "HIF-gate"})
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.load(response)


def ci_checks(head):
    # Fetch jobs from ONE exact workflow attempt, not a mixed commit check collection.
    runs = github("actions/runs?head_sha=" + head + "&event=pull_request&per_page=100")["workflow_runs"]
    runs = [run for run in runs if run.get("head_sha") == head
            and run.get("path") == ".github/workflows/unity-ci.yml"]
    if not runs:
        return {"check_runs": []}
    run_id = max(runs, key=lambda run: run["id"])["id"]
    before = github("actions/runs/" + str(run_id))
    jobs = github("actions/runs/" + str(run_id) + "/attempts/" + str(before["run_attempt"]) + "/jobs?per_page=100")
    after = github("actions/runs/" + str(run_id))
    if jobs.get("total_count", 0) > 100:
        raise ValueError("CI jobs exceed bounded page; cannot verify Gate")
    if before["run_attempt"] != after["run_attempt"] or before["status"] != after["status"]:
        return {"workflow_run": dict(after, status="in_progress"), "check_runs": []}
    return {"workflow_run": after, "check_runs": [dict(job, head_sha=head,
            app={"slug": "github-actions"}, check_suite={"id": after["check_suite_id"]})
            for job in jobs["jobs"]]}


class FatalPersistence(Exception):
    """Control state persistence failed; the scheduler must stop with exit 78."""


# Native post-merge transport: fixed target, fixed origin, never develop,
# no caller-supplied repo argument. Invoked ONLY by the outer scheduler.
MASTER_CHECKOUT = "D:/How_I_Fall/master"
MASTER_ORIGIN = "https://github.com/Bladgrif/How_I_Fall.git"


def task_changed_files(task):
    repo = task["writer_path"]
    return subprocess.check_output(["git", "-c", "safe.directory=" + repo, "-C", repo,
                                    "diff", "--name-only", "-z", task["base_sha"] + "..." + task["head_sha"]],
                                   encoding="utf-8").rstrip("\0").split("\0")


def merged_review_ok(control, task):
    control = Path(control)
    cheap = load(control / "review-latest.json") if (control / "review-latest.json").exists() else None
    strong = load(control / "strong-review-latest.json") if (control / "strong-review-latest.json").exists() else None
    strong_required = needs_strong(task, task_changed_files(task), cheap)
    review_ok(strong if strong_required else cheap or {}, task, strong=strong_required)


def git_output(repo, *args):
    return subprocess.check_output(["git", "-c", "safe.directory=" + str(repo), "-C", str(repo), *args],
                                   encoding="utf-8").strip()


def sync_master(control):
    control = Path(control)
    controller = load(control / "controller.json")
    if controller.get("enabled") is not True or (control / "STOP").exists() or (control / "MAINTENANCE").exists():
        raise ValueError("Master sync disabled or in maintenance")
    if controller.get("native_master_sync_enabled") is not True:
        raise ValueError("Native master sync requires controller.native_master_sync_enabled=true")
    state, queue = load(control / "state.json"), load(control / "queue.json")
    if state.get("status") != "SYNC_MASTER_PENDING":
        raise ValueError("Master sync requires durable SYNC_MASTER_PENDING state")
    matches = [t for t in queue["tasks"] if t.get("id") == state.get("active_task_id")]
    if len(matches) != 1:
        raise ValueError("Master sync requires exactly one active task identity")
    task = matches[0]
    if task.get("status") != "SYNC_MASTER_PENDING" or any(
            t.get("id") != task["id"] and t.get("status") not in ("DONE", "WAIT_USER", "READY", "CANCELLED")
            for t in queue["tasks"]):
        raise ValueError("Master sync requires one pending task and no other active task")
    for key in ("base_sha", "head_sha", "merge_sha"):
        if not isinstance(task.get(key), str) or not re.fullmatch(r"[a-f0-9]{40}", task[key]):
            raise ValueError("Invalid exact 40-hex " + key)
    if any(state.get(key) != task[key] for key in ("base_sha", "head_sha")):
        raise ValueError("State/queue base or head identity mismatch")
    if str(task.get("writer_path", "")).replace("\\", "/") not in ("D:/How_I_Fall/agent", "D:/How_I_Fall/zagent"):
        raise ValueError("Unexpected writer checkout for master sync")
    if type(task.get("pr_number")) is not int or task["pr_number"] <= 0:
        raise ValueError("Integer PR id required for master sync")
    pr = github("pulls/" + str(task["pr_number"]))
    if pr.get("state") != "closed" or pr.get("merged") is not True or pr.get("base", {}).get("ref") != "master":
        raise ValueError("GitHub PR is not a merged master candidate")
    if pr.get("merge_commit_sha") != task["merge_sha"] or pr.get("head", {}).get("sha") != task["head_sha"]:
        raise ValueError("Merged PR head/merge SHA mismatch")
    if github("branches/master").get("commit", {}).get("sha") != task["merge_sha"]:
        raise ValueError("Fresh remote master does not match expected merge SHA")
    if ci_status(ci_checks(task["head_sha"]), task["head_sha"]) != "GREEN":
        raise ValueError("Exact-head CI is not GREEN for master sync")
    merged_review_ok(control, task)

    repo = Path(MASTER_CHECKOUT)
    if Path(git_output(repo, "rev-parse", "--show-toplevel")).resolve() != repo.resolve():
        raise ValueError("Fixed master path is not the repository root")
    if git_output(repo, "branch", "--show-current") != "master":
        raise ValueError("Fixed master checkout is not on branch master")
    if git_output(repo, "status", "--porcelain"):
        raise ValueError("master checkout is dirty; user diff preserved, sync refused")
    if git_output(repo, "remote", "get-url", "origin") != MASTER_ORIGIN:
        raise ValueError("master checkout origin mismatch; sync refused")
    head = git_output(repo, "rev-parse", "HEAD")
    if head == task["merge_sha"]:
        # Already-synced expected master: idempotent success, no fetch/merge.
        already = True
    else:
        already = False
        if head != task["base_sha"]:
            raise ValueError("master HEAD is neither task base nor expected merge; sync refused")
        git_output(repo, "fetch", "origin", "master")
        if git_output(repo, "branch", "--show-current") != "master" or git_output(repo, "remote", "get-url", "origin") != MASTER_ORIGIN:
            raise ValueError("master branch/origin changed during fetch; sync refused")
        if git_output(repo, "status", "--porcelain"):
            raise ValueError("master checkout became dirty during fetch; sync refused")
        if git_output(repo, "rev-parse", "HEAD") != task["base_sha"]:
            raise ValueError("master HEAD changed during fetch; sync refused")
        if git_output(repo, "rev-parse", "FETCH_HEAD") != task["merge_sha"]:
            raise ValueError("Fetched master does not match expected merge SHA")
        git_output(repo, "merge", "--ff-only", task["merge_sha"])
        if git_output(repo, "rev-parse", "HEAD") != task["merge_sha"] or git_output(repo, "status", "--porcelain"):
            raise ValueError("Post-merge master verification failed")
    task["status"] = "MERGED_LOCAL_SYNC_DONE"
    task["local_sync_done"] = True
    state["status"] = "ROADMAP_SYNC_READY"
    try:
        save(control / "queue.json", queue)
        save(control / "state.json", state)
    except Exception as error:
        try:
            (control / "STOP").write_text("Master sync persistence failed: " + str(error), encoding="utf-8")
        except OSError:
            pass
        raise FatalPersistence(str(error)) from error
    return {"status": "MASTER_SYNCED", "task_id": task["id"], "merge_sha": task["merge_sha"],
            "already_synced": already, "checked_at": now()}


def zcode_run(repo, prompt, output, events):
    config = load(Path.home() / ".zcode/cli/config.json")
    if config.get("model") != "account:zai-individual-coding-plan/GLM-5.3-Flash" or config.get("thoughtLevel") != "max":
        raise ValueError("Z-Code model drift: expected Flash/Max")
    cli = "D:/Users/roman/AppData/Local/Programs/ZCode/resources/glm/zcode.cjs"
    result = subprocess.run([str(Path.home() / ".cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe"), cli, "--cwd", repo, "--mode", "edit", "--disallowed-tools", "Bash,Agent,Task", "--surface", "terminal",
                             "--prompt", Path(prompt).read_text(encoding="utf-8-sig"), "--no-color", "--json"],
                            capture_output=True, encoding="utf-8", errors="replace",
                            env=dict(os.environ, NODE_USE_SYSTEM_CA="1"))
    Path(events).write_text(result.stdout + "\nSTDERR:\n" + result.stderr, encoding="utf-8")
    if result.returncode:
        raise RuntimeError("Z-Code failed; preserved partial diff, see events")
    # Z-Code may prepend diagnostics to its JSON envelope.
    decoder = json.JSONDecoder()
    for match in re.finditer(r"\{", result.stdout):
        try:
            envelope, _ = decoder.raw_decode(result.stdout[match.start():])
            if isinstance(envelope, dict) and isinstance(envelope.get("response"), str):
                response = envelope["response"].strip()
                if response.startswith("```"):
                    response = re.sub(r"^```(?:json)?\s*|\s*```$", "", response)
                report = json.loads(response)
                validate_worker_report(report)
                save(output, report)
                return
        except json.JSONDecodeError:
            continue
    raise ValueError("Z-Code did not return a machine-readable final report")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["rpc", "quota", "zcode-run", "merge-gate", "validate-report", "ci-status", "review-tier", "sync-master"])
    parser.add_argument("--control", default="D:/How_I_Fall/agent-control")
    parser.add_argument("--method")
    parser.add_argument("--params", default="{}")
    parser.add_argument("--params-file")
    parser.add_argument("--repo")
    parser.add_argument("--prompt")
    parser.add_argument("--output")
    parser.add_argument("--events")
    parser.add_argument("--head")
    args = parser.parse_args()
    c = Path(args.control)
    if args.command == "rpc":
        print(json.dumps(codex_rpc(args.method, load(args.params_file) if args.params_file else json.loads(args.params)), ensure_ascii=False))
    elif args.command == "quota":
        try:
            value = quota_result(codex_rpc("account/rateLimits/read", {}))
        except Exception as error:
            value = {"updated_at": now(), "mode": "UNKNOWN", "error": str(error), "threshold_remaining_percent": 25}
        save(c / "codex-quota.json", value)
        print(json.dumps(value))
    elif args.command == "zcode-run":
        zcode_run(args.repo, args.prompt, args.output, args.events)
    elif args.command == "validate-report":
        validate_worker_report(load(args.output))
    elif args.command == "ci-status":
        if not re.fullmatch(r"[a-f0-9]{40}", args.head or ""):
            raise ValueError("Exact CI head SHA required")
        print(ci_status(ci_checks(args.head), args.head))
    elif args.command == "sync-master":
        try:
            print(json.dumps(sync_master(c), ensure_ascii=False))
        except FatalPersistence:
            sys.exit(78)
    else:
        state, tasks = load(c / "state.json"), load(c / "queue.json")["tasks"]
        task = next(t for t in tasks if t["id"] == state["active_task_id"])
        changed = task_changed_files(task)
        cheap = load(c / "review-latest.json") if (c / "review-latest.json").exists() else None
        if args.command == "review-tier":
            print("strong" if needs_strong(task, changed, cheap) else "cheap")
            return
        pr = github("pulls/" + str(task["pr_number"]))
        checks = ci_checks(task["head_sha"])
        receipt = gate(task, cheap, load(c / "strong-review-latest.json")
                       if (c / "strong-review-latest.json").exists() else None, pr, checks, changed)
        save(c / "merge-gate.json", receipt)
        print(json.dumps(receipt))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
