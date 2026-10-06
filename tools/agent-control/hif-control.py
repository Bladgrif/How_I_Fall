"""Локальные transport/gates HIF. Без сторонних пакетов и собственных model loops."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import struct
import sys
import threading
import time
import urllib.request
import xml.etree.ElementTree as ET


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


# Native QA extends the existing worker, not model-provided shell commands.
# A PlayMode test override AFTER SaveManager.Awake is not startup isolation.
# Keep runtime/graphical selections closed until isolation before ANY runtime
# access and bounded affected-state screenshot coverage are proven separately.
NATIVE_FILTERS = {
    "EditMode": ("InteractiveHotspotEditModeTests", "SavePaginationEditModeTests"),
}
NATIVE_GRAPHICAL = {}
UNITY_WRITE_ROOTS = ("Assets", "Packages", "ProjectSettings", "Library", "Temp",
                     "Logs", "UserSettings", "obj", ".vs", "QAArtifacts")


def native_plan(task):
    if not isinstance(task, dict) or not isinstance(task.get("writer_path"), str):
        raise ValueError("Malformed native task/path")
    profile = task.get("native_validation_profile")
    if profile not in ("agent-control", "hif-runtime"):
        raise ValueError("Unknown/missing native QA profile")
    repo = task.get("writer_path", "").replace("\\", "/")
    expected = {"Codex": "D:/How_I_Fall/agent", "ZCode": "D:/How_I_Fall/zagent"}
    if repo != expected.get(task.get("writer_engine")):
        raise ValueError("Native QA requires bound agent/zagent writer checkout and engine")
    if not re.fullmatch(r"[a-f0-9]{40}", task.get("base_sha", "")):
        raise ValueError("Native QA requires exact base")
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,79}", task.get("id", "")):
        raise ValueError("Unsafe native task ID")
    if profile == "agent-control":
        if task.get("native_qa") is not None:
            raise ValueError("agent-control has no caller-supplied QA arguments")
        return []
    qa = task.get("native_qa")
    if not isinstance(qa, dict) or set(qa) != {"unity", "graphical"}:
        raise ValueError("Native QA accepts only unity/graphical selection, not commands")
    unity, graphical = qa["unity"], qa["graphical"]
    if not isinstance(unity, list) or not 1 <= len(unity) <= 6 or not isinstance(graphical, list) or len(graphical) > 3:
        raise ValueError("Native QA check selection must be bounded and nonempty")
    if type(task.get("player_facing")) is not bool or (task["player_facing"] and not graphical):
        raise ValueError("Player-facing native QA requires graphical selection")
    plan = []
    for check in unity:
        if not isinstance(check, dict) or set(check) != {"mode", "filter"}:
            raise ValueError("Invalid native Unity arguments")
        mode, test_filter = check["mode"], check["filter"]
        if not isinstance(mode, str) or not isinstance(test_filter, str) or test_filter not in NATIVE_FILTERS.get(mode, ()):
            raise ValueError("Unknown or unproven save isolation for Mode/TestFilter")
        plan.append(("tools/run-unity-tests.ps1", ["-Mode", mode, "-TestFilter", test_filter]))
    for scenario in graphical:
        if not isinstance(scenario, str) or scenario not in NATIVE_GRAPHICAL:
            raise ValueError("REVIEWER VISUAL PROOF NOT AVAILABLE: unknown/unproven startup isolation or bounded affected-state coverage for Scenario")
        plan.append(("tools/run-graphical-e2e.ps1", ["-Scenario", scenario]))
    if len({tuple(args) for _, args in plan}) != len(plan):
        raise ValueError("Duplicate native QA selection")
    return plan


class NativeQaError(ValueError):
    def __init__(self, message, retryable=False):
        super().__init__(message)
        self.retryable = retryable


def bounded_path(root, relative):
    root = Path(root)
    path = root / relative
    if not path.resolve().is_relative_to(root.resolve()):
        raise NativeQaError("QA path escapes bound checkout")
    # Reject junctions/reparse points too, even when they resolve inside root.
    for part in (path, *path.parents):
        if os.path.lexists(part) and (part.is_symlink() or getattr(part.lstat(), "st_file_attributes", 0) & 0x400):
            raise NativeQaError("QA path contains reparse point")
    return path


def graphical_names(repo, script, scenario):
    source = bounded_path(repo, script).read_text(encoding="utf-8-sig")
    line = next((line for line in source.splitlines() if line.strip().startswith(scenario + " = @{")), "")
    names = re.findall(r"'([^'/\\]+\.png)'", line)
    # No arbitrary three-frame sample of a broad launcher: archive ALL required
    # originals, or refuse before launch. Current production scenarios are closed.
    if not 1 <= len(names) <= 3 or len(set(names)) != len(names):
        raise NativeQaError("REVIEWER VISUAL PROOF NOT AVAILABLE: launcher coverage is not bounded to three originals")
    return names


def preflight_native_paths(repo, plan):
    """Inspect every existing Unity write subtree and every selected output BEFORE launch."""
    # Unity can regenerate root-level IDE/project files too. Reject root/ancestor
    # or immediate-child links before traversing any write subtree.
    bounded_path(repo, ".")
    for child in Path(repo).iterdir():
        bounded_path(repo, child.name)
    for relative in UNITY_WRITE_ROOTS:
        root = bounded_path(repo, relative)
        if root.exists() and not root.is_dir():
            raise NativeQaError("Unity write root is not a directory: " + relative)
        pending = [root] if root.exists() else []
        while pending:
            for child in pending.pop().iterdir():
                path = bounded_path(repo, child.relative_to(repo))
                if path.is_dir():
                    pending.append(path)
    outputs = ["Temp/CodexTests"]
    for script, args in plan:
        if args[0] == "-Mode":
            stem = "Temp/CodexTests/" + args[1] + "_" + args[3]
            outputs.extend((stem + ".log", stem + "_results.xml"))
        else:
            _, sentinel, directory = NATIVE_GRAPHICAL[args[1]]
            outputs.extend(("Temp/CodexTests/graphical_preflight.log",
                            "Temp/CodexTests/graphical_" + args[1] + ".log",
                            sentinel, "QAArtifacts/GraphicalE2E/" + directory))
            if bounded_path(repo, sentinel).exists():
                raise NativeQaError("Existing sentinel would be overwritten; preserve and block")
            outputs.extend("QAArtifacts/GraphicalE2E/" + directory + "/" + name
                           for name in graphical_names(repo, script, args[1]))
    for relative in outputs:
        bounded_path(repo, relative)


def native_selection(plan):
    return [dict(launcher=script, arguments=args) for script, args in plan]


def native_selection_ok(proof, task):
    selection = native_selection(native_plan(task))
    invocations = proof.get("invocations")
    if (proof.get("selection") != selection or not isinstance(invocations, list)
            or len(invocations) != len(selection) or not selection):
        raise ValueError("Native proof selection differs from current approved plan")
    for expected, actual in zip(selection, invocations):
        if (not isinstance(actual, dict) or set(actual) != {"launcher", "arguments", "exit_code"}
                or actual["launcher"] != expected["launcher"] or actual["arguments"] != expected["arguments"]
                or type(actual["exit_code"]) is not int or actual["exit_code"] != 0):
            raise ValueError("Native proof lacks exact successful launcher invocation")


def native_originals_ok(proof, task, evidence):
    files = proof.get("files")
    if not isinstance(files, list) or not files:
        raise ValueError("Missing native originals")
    names = [entry["filename"] for entry in files]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate native originals")
    for entry in files:
        original = bounded_path(evidence, entry["filename"])
        if original.stat().st_size != entry["bytes"] or sha256(original) != entry["sha256"]:
            raise ValueError("Native original hash/size mismatch")
    for index, (script, args) in enumerate(native_plan(task)):
        label = str(index) + "-" + args[1]
        required = [label + "-transport.log", label + ".log"]
        if args[0] == "-Mode":
            required.append(label + ".xml")
            unity_xml_ok(bounded_path(evidence, label + ".xml"))
        else:
            required.extend((label + "-preflight.log", label + "-result.txt"))
            required.extend(label + "-" + name for name in graphical_names(task["writer_path"], script, args[1]))
        if not set(required).issubset(names):
            raise ValueError("Missing selected invocation originals")


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def source_fingerprint(repo, base):
    names = subprocess.check_output(["git", "-c", "safe.directory=" + str(repo), "-C", str(repo),
                                     "diff", "--name-only", "-z", base], stderr=subprocess.PIPE).decode("utf-8").split("\0")
    names += subprocess.check_output(["git", "-c", "safe.directory=" + str(repo), "-C", str(repo),
                                      "ls-files", "--others", "--exclude-standard", "-z"], stderr=subprocess.PIPE).decode("utf-8").split("\0")
    values = [(name, sha256(bounded_path(repo, name)) if bounded_path(repo, name).is_file() else "DELETED")
              for name in sorted(set(names) - {""})]
    return hashlib.sha256(json.dumps(values, ensure_ascii=False).encode("utf-8")).hexdigest()


def unchanged_harness(repo, base, relative):
    path = bounded_path(repo, relative)
    approved = subprocess.check_output(["git", "-c", "safe.directory=" + str(repo), "-C", str(repo), "show", base + ":" + relative])
    # Git working-tree CRLF conversion is not a harness change.
    if path.read_bytes().replace(b"\r\n", b"\n") != approved.replace(b"\r\n", b"\n"):
        raise NativeQaError("QA harness/isolation changed since approved base: " + relative)
    return path


def fresh_artifact(repo, relative, started):
    path = bounded_path(repo, relative)
    if not path.is_file() or not path.stat().st_size or path.stat().st_mtime_ns < started:
        raise NativeQaError("MISSING/empty/stale native proof: " + relative)
    return path


def unity_xml_ok(path):
    root = ET.parse(path).getroot()
    cases = list(root.iter("test-case"))
    if root.tag != "test-run" or int(root.get("total", "0")) <= 0 or not cases:
        raise NativeQaError("Zero/malformed Unity XML totals")
    if int(root.get("failed", "0")) or int(root.get("errors", "0")) or root.get("result") != "Passed" or any(case.get("result") == "Failed" for case in cases):
        raise NativeQaError("Unity test failure", retryable=True)
    if (int(root.get("passed", "0")) <= 0 or int(root.get("passed", "0")) != sum(case.get("result") == "Passed" for case in cases)
            or int(root.get("total", "0")) != len(cases)):
        raise NativeQaError("Zero/inconsistent executed Unity XML totals")


def native_qa(task, control):
    plan = native_plan(task)  # Before ANY subprocess, fail closed on queue input.
    if task["native_validation_profile"] != "hif-runtime":
        raise ValueError("agent-control retains existing PowerShell fixture gate")
    repo = Path(task["writer_path"])
    head = task["resume_head_sha"]
    if not re.fullmatch(r"[a-f0-9]{40}", head):
        raise ValueError("Native QA missing resume HEAD")
    if git_output(repo, "rev-parse", "HEAD") != head or git_output(repo, "branch", "--show-current") != task["branch"]:
        raise ValueError("Native QA branch/HEAD mismatch")
    if Path(git_output(repo, "rev-parse", "--show-toplevel")).resolve() != repo.resolve():
        raise ValueError("Native QA writer is not repository root")
    evidence = bounded_path(control, "evidence/" + task["id"] + "-" + str(time.time_ns()))
    evidence.mkdir(parents=True)
    manifest = dict(task_id=task["id"], base_sha=task["base_sha"], source_head_sha=head,
                    writer_engine=task["writer_engine"], writer_path=str(repo), started_at=now(),
                    source_fingerprint=source_fingerprint(repo, task["base_sha"]),
                    selection=native_selection(plan),
                    status="BLOCKED", validation_complete=False, invocations=[], files=[],
                    visual_inspection="NOT VERIFIED: independent Sol High owns inspection", remote_proof="NOT VERIFIED")
    manifest_path = evidence / "manifest.json"

    def archive(path, name):
        dest = evidence / name
        dest.write_bytes(path.read_bytes())
        manifest["files"].append(dict(filename=name, sha256=sha256(dest), bytes=dest.stat().st_size))

    try:
        # Validate every harness before the first launch. No execution of modified
        # scripts or isolation harnesses supplied by the writer.
        # This input controls the existing launcher's installed Unity executable.
        unchanged_harness(repo, task["base_sha"], "ProjectSettings/ProjectVersion.txt")
        for script, args in plan:
            unchanged_harness(repo, task["base_sha"], script)
            if args[0] == "-Mode":
                unchanged_harness(repo, task["base_sha"], "Assets/HowIFall/Tests/" + args[1] + "/" + args[3] + ".cs")
            else:
                runner = NATIVE_GRAPHICAL[args[1]][0]
                unchanged_harness(repo, task["base_sha"], "Assets/HowIFall/Editor/" + runner + ".cs")
        preflight_native_paths(repo, plan)
        for index, (script, args) in enumerate(plan):
            # Recheck the complete selection, not just this invocation's files.
            preflight_native_paths(repo, plan)
            label = str(index) + "-" + args[1]
            started = time.time_ns()
            exe = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe")
            argv = [exe, "-NoProfile", "-NonInteractive", "-File", str(bounded_path(repo, script)), *args]
            # Existing launchers resolve the installed Editor; no model executable.
            env = {k: v for k, v in os.environ.items() if k.upper() != "UNITY_EDITOR_PATH"}
            run = subprocess.run(argv, cwd=repo, env=env, capture_output=True, timeout=1800)
            output = evidence / (label + "-transport.log")
            output.write_bytes(run.stdout + b"\nSTDERR:\n" + run.stderr)
            manifest["invocations"].append(dict(launcher=script, arguments=args, exit_code=run.returncode))
            manifest["files"].append(dict(filename=output.name, sha256=sha256(output), bytes=output.stat().st_size))
            if re.search(rb"(?i)access (?:is )?denied|unauthorized|authentication failed", run.stdout + run.stderr):
                raise PermissionError("Native launcher permission/auth failure; see transport log")
            if args[0] == "-Mode":
                stem = args[1] + "_" + args[3]
                log = fresh_artifact(repo, "Temp/CodexTests/" + stem + ".log", started)
                archive(log, label + ".log")
                if re.search(r"error CS\d+:", log.read_text(encoding="utf-8-sig", errors="replace")):
                    raise NativeQaError("Unity implementation compilation failure", retryable=True)
                xml = fresh_artifact(repo, "Temp/CodexTests/" + stem + "_results.xml", started)
                archive(xml, label + ".xml")
                unity_xml_ok(xml)
            else:
                scenario = args[1]
                _, sentinel_name, directory = NATIVE_GRAPHICAL[scenario]
                preflight = fresh_artifact(repo, "Temp/CodexTests/graphical_preflight.log", started)
                archive(preflight, label + "-preflight.log")
                if re.search(r"error CS\d+:", preflight.read_text(encoding="utf-8-sig", errors="replace")):
                    raise NativeQaError("Unity graphical preflight compilation failure", retryable=True)
                log = fresh_artifact(repo, "Temp/CodexTests/graphical_" + scenario + ".log", started)
                sentinel = fresh_artifact(repo, sentinel_name, started)
                archive(log, label + ".log")
                archive(sentinel, label + "-result.txt")
                value = sentinel.read_text(encoding="utf-8-sig")
                # Existing PlayerUi/Hotspot sentinels are not all gitignored.
                # Remove ONLY the fresh generated sentinel after archiving its
                # original; never let it become task diff or source fingerprint.
                sentinel.unlink()
                if not re.search(r"(?m)^status=PASS\s*$", value):
                    raise NativeQaError("Graphical sentinel failed", retryable=True)
                if scenario in ("PlayerUi", "GameMenu") and "playerPrefsRestored=true" not in value:
                    raise NativeQaError("PlayerPrefs restoration proof missing")
                # Use ALL required names from the unchanged existing launcher,
                # not a model claim or a universal screenshot proof platform.
                names = graphical_names(repo, script, scenario)
                for name in names:
                    png = fresh_artifact(repo, "QAArtifacts/GraphicalE2E/" + directory + "/" + name, started)
                    data = png.read_bytes()
                    size = (1920, 1080) if "1920x1080" in name else (1280, 720)
                    if data[:8] != b"\x89PNG\r\n\x1a\n" or len(data) < 24 or struct.unpack(">II", data[16:24]) != size:
                        raise NativeQaError("Malformed/wrong-size graphical original: " + name)
                    archive(png, label + "-" + name)
            if run.returncode:
                raise NativeQaError("Native launcher exit failure", retryable=True)
        if (git_output(repo, "rev-parse", "HEAD") != head or git_output(repo, "branch", "--show-current") != task["branch"]
                or source_fingerprint(repo, task["base_sha"]) != manifest["source_fingerprint"]):
            raise NativeQaError("HEAD/branch drift during QA")
        manifest.update(status="PASS", validation_complete=True)
    except PermissionError as error:
        manifest.update(status="WAIT_AUTH", error=str(error))
    except NativeQaError as error:
        manifest.update(status="PARTIAL_RETRY" if error.retryable else "BLOCKED", error=str(error))
    except (OSError, ValueError, ET.ParseError, subprocess.SubprocessError) as error:
        manifest.update(status="BLOCKED", error=str(error))
    manifest["finished_at"] = now()
    save(manifest_path, manifest)
    return dict(manifest, manifest_path=str(manifest_path))


def native_retry(task, outcome):
    """Only implementation failures get two corrections, retaining exact identity."""
    count = task.get("native_correction_attempts", 0)
    if type(count) is not int or count < 0 or count > 2:
        raise ValueError("Invalid native correction counter")
    if outcome["status"] == "PARTIAL_RETRY" and count < 2:
        task["native_correction_attempts"] = count + 1
        task["correction"] = "Исправь implementation failure в SAME checkout/branch/engine/head; не меняй QA harness/selection. " + outcome["error"] + "; manifest: " + outcome["manifest_path"]
        return "PARTIAL_RETRY"
    return "WAIT_AUTH" if outcome["status"] == "WAIT_AUTH" else "BLOCKED"


def native_proof_ok(task, require_remote=True, control="D:/How_I_Fall/agent-control"):
    if task.get("writer_engine") != "ZCode" or task.get("native_validation_profile") != "hif-runtime":
        return
    native_plan(task)
    path = Path(task["native_validation_manifest"])
    relative = path.resolve().relative_to(Path(control).resolve()).as_posix()
    if not re.fullmatch(r"evidence/" + re.escape(task["id"]) + r"-[0-9]+/manifest\.json", relative):
        raise ValueError("Native manifest path is not bound task evidence")
    path = bounded_path(control, relative)
    proof = load(path)
    if (proof.get("status") != "PASS" or proof.get("validation_complete") is not True
            or proof.get("task_id") != task["id"] or proof.get("base_sha") != task["base_sha"]
            or proof.get("source_head_sha") != task["head_sha"] or proof.get("writer_engine") != "ZCode"
            or Path(proof.get("writer_path", "")).resolve() != Path(task["writer_path"]).resolve()
            or proof.get("source_fingerprint") != source_fingerprint(task["writer_path"], task["base_sha"])):
        raise ValueError("Missing/stale/incomplete native QA manifest")
    native_selection_ok(proof, task)
    native_originals_ok(proof, task, path.parent)
    if task["native_qa"]["graphical"] and require_remote:
        receipt = task.get("native_remote_proof", {})
        if (receipt.get("head_sha") != task["head_sha"] or receipt.get("manifest_sha256") != sha256(path)
                or receipt.get("route") not in ("visual-review", "drive", "evidence-branch")):
            raise ValueError("REVIEWER VISUAL PROOF NOT AVAILABLE: publication receipt missing/stale")
        url = receipt.get("url", "")
        patterns = {
            "visual-review": r"https://github\.com/Bladgrif/How_I_Fall/actions/runs/[0-9]+/artifacts/[0-9]+",
            "drive": r"https://drive\.google\.com/(?:file/d/|drive/folders/)[A-Za-z0-9_-]+(?:/view)?",
            "evidence-branch": r"https://github\.com/Bladgrif/How_I_Fall/tree/evidence/[a-z0-9-]+",
        }
        if not isinstance(url, str) or not re.fullmatch(patterns[receipt["route"]], url):
            raise ValueError("Unpermitted remote proof route")


def native_proof_status(task, control="D:/How_I_Fall/agent-control"):
    try:
        native_proof_ok(task, require_remote=False, control=control)
    except (ValueError, OSError, KeyError, ET.ParseError) as error:
        return dict(status="BLOCKED", error=str(error))
    try:
        native_proof_ok(task, control=control)
    except (ValueError, OSError, KeyError, ET.ParseError) as error:
        return dict(status="WAIT_VISUAL_PROOF", error=str(error))
    return dict(status="READY")


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
    if high_risk(changed) or task.get("risk") == "high" or task.get("player_facing") or (task.get("native_qa") or {}).get("graphical"):
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


def gate(task, cheap, strong, pr, checks, changed, control="D:/How_I_Fall/agent-control"):
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
    if (task.get("native_qa") or {}).get("graphical") and not final_review.get("visual_proof_verified"):
        raise ValueError("GLM graphical proof requires independent Sol High inspection")
    native_proof_ok(task, control=control)
    return {"status": "MERGE_ALLOWED", "task_id": task["id"], "head_sha": task["head_sha"],
            "expected_head_sha": task["head_sha"],
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
    if task.get("player_facing") and not (strong if strong_required else cheap or {}).get("visual_proof_verified"):
        raise ValueError("Merged candidate visual proof not verified")
    native_proof_ok(task, control=control)


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
    parser.add_argument("command", choices=["rpc", "quota", "zcode-run", "merge-gate", "validate-report", "ci-status", "review-tier", "sync-master", "native-plan", "native-qa", "bind-native-proof", "native-proof-status"])
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
    elif args.command == "native-plan":
        native_plan(load(args.output))
    elif args.command in ("native-qa", "bind-native-proof", "native-proof-status"):
        state, queue = load(c / "state.json"), load(c / "queue.json")
        matches = [t for t in queue["tasks"] if t["id"] == state["active_task_id"]]
        if len(matches) != 1:
            raise ValueError("Native QA active task missing/ambiguous")
        task = matches[0]
        native_plan(task)
        if args.command == "native-proof-status":
            print(json.dumps(native_proof_status(task, control=c)))
            return
        if args.command == "bind-native-proof":
            path = Path(task["native_validation_manifest"])
            relative = path.resolve().relative_to(c.resolve()).as_posix()
            if not re.fullmatch(r"evidence/" + re.escape(task["id"]) + r"-[0-9]+/manifest\.json", relative):
                raise ValueError("Native manifest write path escapes bound task evidence")
            path = bounded_path(c, relative)
            proof = load(path)
            head = git_output(task["writer_path"], "rev-parse", "HEAD")
            native_selection_ok(proof, task)
            native_originals_ok(proof, task, path.parent)
            if (proof.get("status") != "PASS" or proof.get("validation_complete") is not True
                    or proof.get("task_id") != task["id"] or proof.get("base_sha") != task["base_sha"]
                    or proof.get("source_head_sha") != task["resume_head_sha"]
                    or proof.get("writer_engine") != task["writer_engine"]
                    or Path(proof.get("writer_path", "")).resolve() != Path(task["writer_path"]).resolve()
                    or source_fingerprint(task["writer_path"], task["base_sha"]) != proof.get("source_fingerprint")):
                raise ValueError("Cannot bind incomplete/drifted native QA to committed head")
            proof["source_head_sha"] = head
            save(path, proof)
        else:
            outcome = native_qa(task, c)
            task["native_validation_manifest"] = outcome["manifest_path"]
            if outcome["status"] != "PASS":
                task["status"] = state["status"] = native_retry(task, outcome)
                state["last_error"] = outcome["error"]
            try:
                save(c / "queue.json", queue)
                save(c / "state.json", state)
                save(args.output, outcome)
            except OSError:
                sys.exit(78)
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
                       if (c / "strong-review-latest.json").exists() else None, pr, checks, changed, control=c)
        save(c / "merge-gate.json", receipt)
        print(json.dumps(receipt))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
