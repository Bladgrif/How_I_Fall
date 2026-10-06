"""Regression tests: no models, filesystem cleanup, GitHub writes or production work."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("control", Path(__file__).with_name("hif-control.py"))
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)


class ControlTests(unittest.TestCase):
    def setUp(self):
        self.task = {"id": "test", "base_sha": "a"*40, "head_sha": "b"*40}
        self.review = dict(task_id="test", base_sha="a"*40, head_sha="b"*40,
                           verdict="CLEAN", findings=[], validation_gaps=[], escalate=False,
                           reviewer_model="gpt-6-luna", reviewer_reasoning="low", risk="low",
                           summary="clean", visual_proof_verified=False, reviewed_at="2026-10-05T00:00:00Z")
        self.strong = dict(self.review, reviewer_model="gpt-6.1-sol", reviewer_reasoning="high")
        self.pr = dict(number=1, state="open", draft=False, mergeable=True,
                       head={"sha": "b"*40}, base={"ref": "master", "sha": "a"*40})
        self.checks = {"workflow_run": dict(id=100, run_attempt=1, head_sha="b"*40, status="completed", conclusion="success"), "check_runs": [dict(id=1, name="CI Gate", head_sha="b"*40,
                                         app={"slug": "github-actions"}, status="completed", conclusion="success")]}

    def gate(self, changed=()):
        return c.gate(self.task, self.review, self.strong, self.pr, self.checks, changed)

    def test_normal_and_threshold_either_window(self):
        for primary, weekly, expected in [(74,74,"NORMAL"),(75,0,"QUOTA_SAVE"),(0,75,"QUOTA_SAVE"),(100,0,"QUOTA_SAVE")]:
            result = c.quota_result({"rateLimits": {"primary": {"usedPercent": primary}, "secondary": {"usedPercent": weekly}}})
            self.assertEqual(expected, result["mode"])

    def test_missing_window_fail_closed(self):
        with self.assertRaises(ValueError):
            c.quota_result({"rateLimits": {"primary": {"usedPercent": 0}}})

    def test_lowrisk_clean_allows(self):
        self.assertEqual("MERGE_ALLOWED", self.gate(["docs/file.md"])["status"])

    def test_runtime_and_automation_require_strong(self):
        for path in ["Assets/X.cs", "Assets/X.prefab", "Packages/manifest.json", "tools/agent-control/hif-worker.ps1", ".github/workflows/unity-ci.yml"]:
            self.strong = None
            with self.assertRaises(ValueError):
                self.gate([path])

    def test_existing_qa_automation_launchers_go_directly_to_strong(self):
        for path in ["tools/run-unity-tests.ps1", "tools/run-graphical-e2e.ps1"]:
            self.assertTrue(c.needs_strong(self.task, [path]))
            with self.assertRaises(ValueError):
                c.gate(self.task, self.review, None, self.pr, self.checks, [path])

    def test_strong_wrong_model_rejected(self):
        self.strong["reviewer_model"] = "GLM-5.3-Flash"
        with self.assertRaises(ValueError):
            self.gate(["Assets/X.cs"])

    def test_stale_review_rejected(self):
        self.review["head_sha"] = "c"*40
        with self.assertRaises(ValueError):
            self.gate()

    def test_malformed_review_cannot_bypass_gate(self):
        for key, value in [("findings", None), ("findings", 0), ("validation_gaps", {}),
                           ("validation_gaps", ""), ("visual_proof_verified", "true"), ("escalate", None)]:
            with self.assertRaises(ValueError):
                c.gate(self.task, dict(self.review, **{key:value}), self.strong, self.pr, self.checks, [])
        with self.assertRaises(ValueError):
            c.gate(self.task, dict(self.review, extra="not allowed"), self.strong, self.pr, self.checks, [])

    def test_risk_paths_normalized_with_component_boundaries(self):
        self.assertTrue(c.high_risk(["ProjectSettings\\file.asset"]))
        self.assertFalse(c.high_risk(["PackagesEvil/file.asset"]))
        self.assertTrue(c.high_risk(["tools/agent-control-evil/file.txt"])) # All repository tooling is conservative strong scope.

    def test_findings_and_validation_gaps_rejected(self):
        for key in ["findings", "validation_gaps"]:
            review = copy.deepcopy(self.review)
            review[key] = ["missing proof"]
            with self.assertRaises(ValueError):
                c.gate(self.task, review, self.strong, self.pr, self.checks, [])

    def test_new_ci_rerun_invalidates_old_success(self):
        self.checks["check_runs"].append(dict(self.checks["check_runs"][0], id=2, status="in_progress", conclusion=None))
        with self.assertRaises(ValueError):
            self.gate()

    def test_ci_wrong_head_or_missing_rejected(self):
        self.checks["check_runs"][0]["head_sha"] = "c"*40
        with self.assertRaises(ValueError):
            self.gate()

    def test_new_suite_without_gate_cannot_inherit_old_gate(self):
        self.checks["check_runs"][0]["check_suite"] = {"id": 10}
        for name in ["CI change classification", "Unity Test Framework", "Unity smoke tests"]:
            self.checks["check_runs"].append(dict(self.checks["check_runs"][0], id=len(self.checks["check_runs"])+10,
                                                  name=name, check_suite={"id": 20}))
        self.assertEqual("WAIT_CI", c.ci_status(self.checks, "b"*40))
        with self.assertRaises(ValueError):
            self.gate()

    def test_active_workflow_with_completed_old_jobs_waits(self):
        for status in ["queued", "in_progress"]:
            self.checks["workflow_run"].update(status=status, run_attempt=2)
            self.assertEqual("WAIT_CI", c.ci_status(self.checks, "b"*40))
            with self.assertRaises(ValueError):
                self.gate()

    def test_ci_transport_binds_exact_attempt_and_detects_rerun_during_read(self):
        run = dict(id=100, run_attempt=2, head_sha="b"*40, status="completed", conclusion="success",
                   path=".github/workflows/unity-ci.yml", check_suite_id=20)
        jobs = {"total_count": 1, "jobs": [dict(id=10, name="CI Gate", status="completed", conclusion="success")]}
        for after, expected in [(run, "GREEN"), (dict(run, run_attempt=3), "WAIT_CI")]:
            with patch.object(c, "github", side_effect=[{"workflow_runs": [run]}, run, jobs, after]) as fetch:
                self.assertEqual(expected, c.ci_status(c.ci_checks("b"*40), "b"*40))
                self.assertIn("/attempts/2/jobs", fetch.call_args_list[2].args[0])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell UTF-8 fixture")
    def test_worker_reads_russian_queue_and_state_without_bom(self):
        with tempfile.TemporaryDirectory(prefix="hif-utf8-fixture-") as directory:
            root = Path(directory)
            value = {"prompt": "Завершить проверку и сохранить задачу"}
            for name in ["queue.json", "state.json"]:
                (root/name).write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
            worker = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
            reads = [line.strip() for line in worker.splitlines() if "= Get-Content $QueuePath" in line or "= Get-Content $StatePath" in line]
            self.assertEqual(4, len(reads))
            script = "$ErrorActionPreference='Stop'\n$QueuePath='"+str(root/"queue.json")+"'\n$StatePath='"+str(root/"state.json")+"'\n"
            for line in reads:
                var = "$q" if "$QueuePath" in line else "$s"
                script += line + "\nif("+var+".prompt -cne '"+value["prompt"]+"'){throw 'Russian text corrupted'}\n"
            (root/"read.ps1").write_text(script, encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root/"read.ps1")], capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell native stdin fixture")
    def test_all_model_helpers_preserve_russian_native_stdin(self):
        with tempfile.TemporaryDirectory(prefix="hif-stdin-fixture-") as directory:
            root = Path(directory)
            value = "Завершить проверку и сохранить задачу"
            for name in ["hif-worker.ps1", "hif-reviewer.ps1", "wake-supervisor.ps1"]:
                source = Path(__file__).with_name(name).read_text(encoding="utf-8-sig")
                setup = [line for line in source.splitlines() if line.startswith("$OutputEncoding =")
                         or line.startswith("[Console]::OutputEncoding =")]
                self.assertEqual(2, len(setup), name)
                script = "$ErrorActionPreference='Stop'\n" + "\n".join(setup)
                script += "\n'"+value+"' | & '"+sys.executable+"' -c 'import sys; sys.stdout.buffer.write(sys.stdin.buffer.read())'\n"
                (root/"stdin.ps1").write_text(script, encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root/"stdin.ps1")], capture_output=True, timeout=15)
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
                self.assertEqual(value, result.stdout.decode("utf-8").strip(), name)

    def test_ci_poll_quiet_while_pending_and_uses_latest_head_gate(self):
        self.assertEqual("WAIT_CI", c.ci_status({"check_runs":[]}, "b"*40))
        self.assertEqual("WAIT_CI", c.ci_status(self.checks, "c"*40))
        self.assertEqual("GREEN", c.ci_status(self.checks, "b"*40))
        self.checks["check_runs"].append(dict(self.checks["check_runs"][0], id=2, status="in_progress", conclusion=None))
        self.assertEqual("WAIT_CI", c.ci_status(self.checks, "b"*40))
        self.checks["check_runs"][-1].update(status="completed", conclusion="failure")
        self.assertEqual("FAILED", c.ci_status(self.checks, "b"*40))

    def test_master_head_draft_mergeability_changes_rejected(self):
        for key, value in [("base", {"ref":"master", "sha":"c"*40}), ("head", {"sha":"c"*40}), ("draft",True), ("mergeable",None)]:
            pr = dict(self.pr, **{key:value})
            with self.assertRaises(ValueError):
                c.gate(self.task, self.review, self.strong, pr, self.checks, [])

    def test_player_facing_requires_inspected_proof(self):
        self.task["player_facing"] = True
        with self.assertRaises(ValueError):
            self.gate(["Assets/X.cs"])
        self.strong["visual_proof_verified"] = True
        self.assertEqual("MERGE_ALLOWED", self.gate(["Assets/X.cs"])["status"])

    def test_high_risk_uses_one_fresh_strong_review(self):
        self.task["risk"] = "high"
        self.assertEqual("MERGE_ALLOWED", c.gate(self.task, None, self.strong, self.pr, self.checks, ["Assets/X.cs"])["status"])
        stale_cheap = dict(self.review, head_sha="c"*40, verdict="CORRECTION_NEEDED")
        self.assertEqual("MERGE_ALLOWED", c.gate(self.task, stale_cheap, self.strong, self.pr, self.checks, ["Assets/X.cs"])["status"])
        with self.assertRaises(ValueError):
            c.gate(self.task, self.review, None, self.pr, self.checks, ["Assets/X.cs"])

    def test_old_success_does_not_cover_active_classification_or_unity(self):
        for name in ["CI change classification", "Unity Test Framework", "Unity smoke tests"]:
            checks = copy.deepcopy(self.checks)
            checks["check_runs"].append(dict(self.checks["check_runs"][0], id=10, name=name, status="in_progress", conclusion=None))
            self.assertEqual("WAIT_CI", c.ci_status(checks, "b"*40))
            with self.assertRaises(ValueError):
                c.gate(self.task, self.review, self.strong, self.pr, checks, [])
            checks["check_runs"][-1].update(status="completed", conclusion="failure")
            self.assertEqual("FAILED", c.ci_status(checks, "b"*40))
            with self.assertRaises(ValueError):
                c.gate(self.task, self.review, self.strong, self.pr, checks, [])

    def test_stale_escalation_does_not_affect_other_task(self):
        stale = dict(self.review, task_id="old", risk="high", escalate=True)
        self.assertFalse(c.needs_strong(self.task, ["docs/x.md"], stale))
        self.assertTrue(c.needs_strong(self.task, ["Assets/X.cs"], stale))

    def test_ui_and_reviewer_risk_require_strong(self):
        self.strong = None
        for path in ["Assets/UI/X.uxml", "Assets/UI/X.uss"]:
            with self.assertRaises(ValueError):
                self.gate([path])
        self.review["risk"] = "high"
        with self.assertRaises(ValueError):
            self.gate(["docs/x.md"])
        self.review["risk"] = "low"
        self.task["player_facing"] = True
        self.review["visual_proof_verified"] = True
        with self.assertRaises(ValueError):
            self.gate(["Assets/UI/X.asset"])

    def test_worker_report_strict_types(self):
        report = dict(status="REVIEW_CANDIDATE", base_sha="a"*40, summary="ok",
                      validation_complete=True, changed_files=["docs/x.md"],
                      validation=["diff check: PASS"], not_run=[], risks=[])
        c.validate_worker_report(report)
        for key, value in [("validation_complete", "true"), ("validation", "NOT RUN"),
                           ("changed_files", "docs/x.md"), ("status", "DONE"), ("base_sha", "bad")]:
            with self.assertRaises(ValueError):
                c.validate_worker_report(dict(report, **{key: value}))

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_worker_preflight_and_idle_preserve_active_candidate(self):
        with tempfile.TemporaryDirectory(prefix="hif-guard-fixture-") as directory:
            root = Path(directory)
            text = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
            text = text.replace("$ControlDir = 'D:\\How_I_Fall\\agent-control'", "$ControlDir = '" + directory + "'")
            script = root / "worker.ps1"
            script.write_text(text.replace("Local\\HowIFallWriter", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
            state = {"status": "REVIEW_CANDIDATE", "active_task_id": "old", "head_sha": "b"*40}
            (root/"state.json").write_text(json.dumps(state), encoding="utf-8")
            for tasks, expected_code in [([dict(id="old",status="REVIEW_CANDIDATE")], 0),
                                         ([dict(id="old",status="REVIEW_CANDIDATE"),dict(id="new",status="READY")], 1)]:
                (root/"queue.json").write_text(json.dumps({"tasks":tasks}), encoding="utf-8")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)], capture_output=True)
                self.assertEqual(expected_code, result.returncode, result.stderr.decode(errors="replace"))
                self.assertEqual(state, c.load(root/"state.json"))

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_scheduler_stops_after_three_transport_failures(self):
        with tempfile.TemporaryDirectory(prefix="hif-loop-fixture-") as directory:
            root = Path(directory)
            text = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig")
            text = text.replace("$C='D:\\How_I_Fall\\agent-control'", "$C='" + directory + "'")
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
            (root/"wake-supervisor.ps1").write_text("exit 1", encoding="utf-8-sig")
            (root/"state.json").write_text(json.dumps({"status":"IDLE", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"controller.json").write_text(json.dumps({"enabled":True}), encoding="utf-8")
            for _ in range(4):
                result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root/"loop.ps1"), "-Once"], capture_output=True)
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            state = c.load(root/"state.json")
            self.assertEqual("BLOCKED", state["status"])
            self.assertEqual(3, state["transport_failures"])
            self.assertEqual("preserve", state["active_task_id"])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_scheduler_recovers_pre_dispatch_exception(self):
        with tempfile.TemporaryDirectory(prefix="hif-dispatch-fixture-") as directory:
            root = Path(directory)
            text = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig").replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
            (root/"state.json").write_text(json.dumps({"status":"READY", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"queue.json").write_text(json.dumps({"tasks":[{"id":"preserve","status":"READY"}]}), encoding="utf-8")
            (root/"controller.json").write_text(json.dumps({"enabled":True}), encoding="utf-8")
            # Missing fixture hif-control.py causes a local pre-dispatch failure, NO RPC.
            for _ in range(4):
                result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1"),"-Once"], capture_output=True)
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            state=c.load(root/"state.json")
            self.assertEqual("BLOCKED",state["status"])
            self.assertEqual("preserve",state["active_task_id"])
            self.assertEqual(3,state["transport_failures"])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_scheduler_quota_retry_is_not_transport_blocker(self):
        with tempfile.TemporaryDirectory(prefix="hif-quota-fixture-") as directory:
            root=Path(directory)
            text=Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig").replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name),encoding="utf-8-sig")
            (root/"hif-worker.ps1").write_text('Write-Error usage_limit_exceeded; exit 75',encoding="utf-8-sig")
            (root/"state.json").write_text(json.dumps({"status":"PARTIAL_RETRY","active_task_id":"preserve"}),encoding="utf-8")
            (root/"queue.json").write_text(json.dumps({"tasks":[{"id":"preserve","status":"PARTIAL_RETRY","writer_engine":"Codex"}]}),encoding="utf-8")
            (root/"controller.json").write_text(json.dumps({"enabled":True}),encoding="utf-8")
            for _ in range(4):
                result=subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1"),"-Once"],capture_output=True)
                self.assertEqual(0,result.returncode,result.stderr.decode(errors="replace"))
            state=c.load(root/"state.json")
            self.assertEqual("PARTIAL_RETRY",state["status"])
            self.assertEqual(0,state["transport_failures"])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_fatal_persistence_stops_process_without_writable_marker(self):
        with tempfile.TemporaryDirectory(prefix="hif-fatal-fixture-") as directory:
            root = Path(directory)
            text = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig").replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
            text = text.replace("$ErrorActionPreference='Stop'", "$ErrorActionPreference='Stop'\nfunction Set-Content { throw 'Simulated control and STOP write failure' }", 1)
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
            (root/"hif-worker.ps1").write_text("Add-Content -LiteralPath '"+str(root/"dispatches.txt")+"' 'dispatch'; Write-Error persistence_failed; exit 78", encoding="utf-8-sig")
            (root/"controller.json").write_text(json.dumps({"enabled": True}), encoding="utf-8")
            (root/"state.json").write_text(json.dumps({"status":"PARTIAL_RETRY", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"queue.json").write_text(json.dumps({"tasks":[{"id":"preserve","status":"PARTIAL_RETRY","writer_engine":"Codex"}]}), encoding="utf-8")
            result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1")], capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual(1, len((root/"dispatches.txt").read_text().splitlines()))
            self.assertFalse((root/"STOP").exists())
            self.assertEqual("preserve", c.load(root/"state.json")["active_task_id"])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_actual_worker_save_failure_returns_fatal_without_resume(self):
        with tempfile.TemporaryDirectory(prefix="hif-save-fixture-") as directory:
            root = Path(directory)
            worker = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
            functions = worker[worker.index("function NowIso"):worker.index("function RunGit")]
            text = "$ErrorActionPreference='Stop'\n$ControlDir='"+directory+"'\n"+functions
            text += "\nfunction WriteJson { throw 'Simulated durable write failure' }\nfunction Set-Content { throw 'Simulated STOP write failure' }\n"
            text += "try { SaveControl ([pscustomobject]@{}) ([pscustomobject]@{}); Add-Content '"+str(root/"unsafe-resume.txt")+"' 'bad' } finally { Add-Content '"+str(root/"cleanup.txt")+"' 'finally' }"
            (root/"save.ps1").write_text(text, encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"save.ps1")], capture_output=True, timeout=15)
            self.assertEqual(78, result.returncode, result.stderr.decode(errors="replace"))
            self.assertFalse((root/"unsafe-resume.txt").exists())
            self.assertTrue((root/"cleanup.txt").exists())

    def test_merge_approval_override_is_supervisor_tool_scoped(self):
        expected = 'apps.connector_76869538009648d5b282a4bb21c3d157.tools.merge_pull_request.approval_mode="approve"'
        wake = Path(__file__).with_name("wake-supervisor.ps1").read_text(encoding="utf-8-sig")
        self.assertEqual(1, wake.count(expected))
        self.assertNotIn("default_tools_approval_mode", wake)
        for name in ["hif-worker.ps1", "hif-reviewer.ps1", "supervisor-loop.ps1"]:
            self.assertNotIn("approval_mode=", Path(__file__).with_name(name).read_text(encoding="utf-8-sig"))

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell auth-wait fixture")
    def test_auth_wait_is_quiet_without_model_dispatch(self):
        with tempfile.TemporaryDirectory(prefix="hif-auth-fixture-") as directory:
            root = Path(directory)
            source = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig")
            source = source.replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
            (root/"loop.ps1").write_text(source.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
            (root/"controller.json").write_text(json.dumps({"enabled":True}), encoding="utf-8")
            (root/"state.json").write_text(json.dumps({"status":"WAIT_AUTH", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"wake-supervisor.ps1").write_text("throw 'Unexpected auth-wait wake'", encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1"),"-Once"], capture_output=True, timeout=15)
            self.assertEqual(0,result.returncode,result.stderr.decode(errors="replace"))
            self.assertFalse((root/"supervisor-loop.log").exists())
            self.assertEqual("WAIT_AUTH",c.load(root/"state.json")["status"])


    # --- Native post-merge master sync (sync-master) ---

    def sync_fixture(self, head, *, status="SYNC_MASTER_PENDING", enabled=True, dirty="",
                     branch="master", origin=None, fetch_head="match", post_head=None,
                     post_dirty=None, task=None, queue=None, state=None, review=True,
                     pr=None, remote_master=None, checks=None, top_level=None):
        directory = tempfile.TemporaryDirectory(prefix="hif-sync-fixture-")
        self.addCleanup(directory.cleanup)
        root = Path(directory.name)
        base = {"id": "sync", "base_sha": "a"*40, "head_sha": "b"*40, "merge_sha": "c"*40,
                "status": "SYNC_MASTER_PENDING", "pr_number": 7, "writer_path": "D:/How_I_Fall/agent", "risk": "low"}
        task = dict(base, **(task or {}))
        tasks = queue if queue is not None else [task]
        state = dict({"status": status, "active_task_id": "sync", "base_sha": task["base_sha"], "head_sha": task["head_sha"]}, **(state or {}))
        (root/"controller.json").write_text(json.dumps({"enabled": True, "native_master_sync_enabled": enabled}), encoding="utf-8")
        (root/"queue.json").write_text(json.dumps({"tasks": tasks}), encoding="utf-8")
        (root/"state.json").write_text(json.dumps(state), encoding="utf-8")
        if review:
            (root/"review-latest.json").write_text(json.dumps(dict(self.review, task_id=task["id"])), encoding="utf-8")
        pr = pr if pr is not None else dict(number=7, state="closed", merged=True,
                                            merge_commit_sha="c"*40, head={"sha": "b"*40},
                                            base={"ref": "master", "sha": "a"*40})
        fetched = [False]
        merged = [False]
        writes = []
        def git(argv):
            cmd = argv[5:]
            if cmd[0] in ("fetch", "merge"):
                writes.append(cmd)
                fetched[0] = True
                if cmd[0] == "merge":
                    merged[0] = True
                return ""
            if cmd[0] == "diff":
                return "docs/x.md\0"
            if cmd == ["rev-parse", "--show-toplevel"]:
                return c.MASTER_CHECKOUT if top_level is None else top_level
            if cmd[:2] == ["branch", "--show-current"]:
                return branch
            if cmd[0] == "status":
                return dirty if (post_dirty is None or not fetched[0]) else post_dirty
            if cmd[:2] == ["remote", "get-url"]:
                return c.MASTER_ORIGIN if origin is None else origin
            if cmd[:2] == ["rev-parse", "HEAD"]:
                if merged[0]:
                    return task["merge_sha"]
                return head if (post_head is None or not fetched[0]) else post_head
            if cmd[:2] == ["rev-parse", "FETCH_HEAD"]:
                return task["merge_sha"] if fetch_head == "match" else "d"*40
            raise AssertionError("unexpected git command: " + " ".join(cmd))
        return dict(root=root, task=task, pr=pr, git=git, writes=writes,
                    remote=remote_master if remote_master is not None else "c"*40,
                    checks=checks if checks is not None else self.checks)

    def run_sync(self, fx):
        with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: fx["git"](argv)), \
             patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": fx["remote"]}}]), \
             patch.object(c, "ci_checks", return_value=fx["checks"]):
            return c.sync_master(fx["root"])

    def test_sync_disabled_fails_closed_without_probes(self):
        fx = self.sync_fixture("a"*40, enabled=False)
        with patch.object(c, "github") as api, patch.object(c.subprocess, "check_output") as shell:
            with self.assertRaises(ValueError):
                c.sync_master(fx["root"])
            api.assert_not_called()
            shell.assert_not_called()

    def test_sync_requires_pending_state_single_identity_and_exact_fields(self):
        for kwargs in [dict(status="REVIEW_CANDIDATE"),
                       dict(task={"status": "DONE"}),
                       dict(state={"head_sha": "d"*40}),
                       dict(task={"writer_path": "D:/How_I_Fall/develop"}),
                       dict(state={"status": "SYNC_MASTER_PENDING", "active_task_id": "other"}),
                       dict(queue=[{"id": "sync", "base_sha": "a"*40}, {"id": "sync", "base_sha": "a"*40}]),
                       dict(task={"merge_sha": "short"}),
                       dict(task={"pr_number": True}),
                       dict(task={"pr_number": None})]:
            fx = self.sync_fixture("a"*40, **kwargs)
            with patch.object(c, "github") as api, patch.object(c.subprocess, "check_output") as shell:
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
                api.assert_not_called()
                shell.assert_not_called()

    def test_sync_disabled_controller_or_markers_never_probe(self):
        for marker in ("STOP", "MAINTENANCE", "disabled"):
            fx = self.sync_fixture("a"*40)
            if marker == "disabled":
                c.save(fx["root"] / "controller.json", {"enabled": False, "native_master_sync_enabled": True})
            else:
                (fx["root"] / marker).touch()
            with patch.object(c, "github") as api, patch.object(c.subprocess, "check_output") as shell:
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
                api.assert_not_called()
                shell.assert_not_called()

    def test_sync_rejects_unmerged_or_identity_mismatched_pr(self):
        base_pr = dict(number=7, state="closed", merged=True, merge_commit_sha="c"*40,
                       head={"sha": "b"*40}, base={"ref": "master", "sha": "a"*40})
        for override in [dict(state="open"), dict(merged=False), dict(merge_commit_sha="d"*40),
                         dict(head={"sha": "d"*40}), dict(base={"ref": "develop", "sha": "a"*40})]:
            fx = self.sync_fixture("a"*40, pr=dict(base_pr, **override))
            with patch.object(c.subprocess, "check_output") as shell, \
                 patch.object(c, "github", side_effect=[fx["pr"]]) as api:
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
                self.assertEqual(1, api.call_count)
                shell.assert_not_called()

    def test_sync_rejects_fresh_remote_master_mismatch(self):
        fx = self.sync_fixture("a"*40, remote_master="e"*40)
        with patch.object(c.subprocess, "check_output") as shell, \
             patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "e"*40}}]):
            with self.assertRaises(ValueError):
                c.sync_master(fx["root"])
            shell.assert_not_called()

    def test_sync_rejects_non_green_exact_head_ci(self):
        wait = {"workflow_run": dict(id=100, run_attempt=1, head_sha="b"*40, status="in_progress", conclusion=None), "check_runs": []}
        failed = {"workflow_run": dict(id=100, run_attempt=1, head_sha="b"*40, status="completed", conclusion="failure"), "check_runs": []}
        for checks in [wait, failed]:
            fx = self.sync_fixture("a"*40, checks=checks)
            with patch.object(c.subprocess, "check_output") as shell, \
                 patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "c"*40}}]), \
                 patch.object(c, "ci_checks", return_value=checks):
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
                shell.assert_not_called()

    def test_sync_rejects_missing_stale_or_wrong_tier_review(self):
        head = "c"*40
        checks = {"workflow_run": dict(id=100, run_attempt=1, head_sha=head, status="completed", conclusion="success"),
                  "check_runs": [dict(id=1, name="CI Gate", head_sha=head, app={"slug": "github-actions"}, status="completed", conclusion="success")]}
        pr = dict(number=7, state="closed", merged=True, merge_commit_sha="d"*40,
                  head={"sha": head}, base={"ref": "master", "sha": "a"*40})
        missing = self.sync_fixture("a"*40, review=False)
        with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: missing["git"](argv)), \
             patch.object(c, "github", side_effect=[missing["pr"], {"commit": {"sha": "c"*40}}]), \
             patch.object(c, "ci_checks", return_value=missing["checks"]):
            with self.assertRaises(ValueError):
                c.sync_master(missing["root"])
        stale = self.sync_fixture("a"*40, task={"head_sha": head, "merge_sha": "d"*40}, pr=pr, checks=checks)
        with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: stale["git"](argv)), \
             patch.object(c, "github", side_effect=[stale["pr"], {"commit": {"sha": "d"*40}}]), \
             patch.object(c, "ci_checks", return_value=checks):
            with self.assertRaises(ValueError):
                c.sync_master(stale["root"])
        strong_required = self.sync_fixture("a"*40, task={"risk": "high"})
        with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: strong_required["git"](argv)), \
             patch.object(c, "github", side_effect=[strong_required["pr"], {"commit": {"sha": "c"*40}}]), \
             patch.object(c, "ci_checks", return_value=strong_required["checks"]):
            with self.assertRaises(ValueError):
                c.sync_master(strong_required["root"])

    def test_sync_rejects_dirty_origin_branch_or_unexpected_head_before_git_writes(self):
        for kwargs in [dict(dirty="M docs/notes.md"),
                       dict(origin="https://github.com/evil/mirror.git"),
                       dict(branch="develop"),
                       dict(top_level="D:/How_I_Fall"),
                       dict(head="f"*40)]:
            fx = self.sync_fixture(kwargs.pop("head", "a"*40), **kwargs)
            with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs2: fx["git"](argv)), \
                 patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "c"*40}}]), \
                 patch.object(c, "ci_checks", return_value=fx["checks"]):
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
            self.assertEqual([], fx["writes"])

    def test_sync_happy_path_fetches_rechecks_and_ff_only_exact_sha(self):
        fx = self.sync_fixture("a"*40)
        result = self.run_sync(fx)
        self.assertEqual("MASTER_SYNCED", result["status"])
        self.assertEqual("c"*40, result["merge_sha"])
        self.assertFalse(result["already_synced"])
        self.assertEqual([["fetch", "origin", "master"], ["merge", "--ff-only", "c"*40]], fx["writes"])
        persisted = c.load(fx["root"] / "queue.json")["tasks"][0]
        state = c.load(fx["root"] / "state.json")
        self.assertEqual("MERGED_LOCAL_SYNC_DONE", persisted["status"])
        self.assertTrue(persisted["local_sync_done"])
        self.assertEqual("ROADMAP_SYNC_READY", state["status"])

    def test_sync_idempotent_when_master_already_at_merge(self):
        fx = self.sync_fixture("c"*40)
        result = self.run_sync(fx)
        self.assertTrue(result["already_synced"])
        self.assertEqual([], fx["writes"])
        self.assertEqual("MERGED_LOCAL_SYNC_DONE", c.load(fx["root"] / "queue.json")["tasks"][0]["status"])
        self.assertEqual("ROADMAP_SYNC_READY", c.load(fx["root"] / "state.json")["status"])

    def test_sync_refuses_merge_on_post_fetch_drift(self):
        for kwargs in [dict(post_head="e"*40), dict(fetch_head="other"), dict(post_dirty="?? stray.md")]:
            fx = self.sync_fixture("a"*40, **kwargs)
            with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs2: fx["git"](argv)), \
                 patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "c"*40}}]), \
                 patch.object(c, "ci_checks", return_value=fx["checks"]):
                with self.assertRaises(ValueError):
                    c.sync_master(fx["root"])
            self.assertEqual([["fetch", "origin", "master"]], fx["writes"])

    def test_sync_persistence_failure_is_fatal_and_preserves_identity(self):
        fx = self.sync_fixture("c"*40)
        before_queue = (fx["root"]/"queue.json").read_text(encoding="utf-8")
        before_state = (fx["root"]/"state.json").read_text(encoding="utf-8")
        with patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: fx["git"](argv)), \
             patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "c"*40}}]), \
             patch.object(c, "ci_checks", return_value=fx["checks"]), \
             patch.object(c, "save", side_effect=OSError("disk full")):
            with self.assertRaises(c.FatalPersistence):
                c.sync_master(fx["root"])
        self.assertTrue((fx["root"]/"STOP").exists())
        self.assertEqual(before_queue, (fx["root"]/"queue.json").read_text(encoding="utf-8"))
        self.assertEqual(before_state, (fx["root"]/"state.json").read_text(encoding="utf-8"))

    def test_sync_persistence_failure_exits_78_from_main(self):
        fx = self.sync_fixture("c"*40)
        with patch.object(sys, "argv", ["hif-control.py", "sync-master", "--control", str(fx["root"])]), \
             patch.object(c.subprocess, "check_output", side_effect=lambda argv, **kwargs: fx["git"](argv)), \
             patch.object(c, "github", side_effect=[fx["pr"], {"commit": {"sha": "c"*40}}]), \
             patch.object(c, "ci_checks", return_value=fx["checks"]), \
             patch.object(c, "save", side_effect=OSError("disk full")):
            with self.assertRaises(SystemExit) as caught:
                c.main()
            self.assertEqual(78, caught.exception.code)

    def test_zcode_transport_is_edit_mode_without_shell_tools(self):
        source = Path(__file__).with_name("hif-control.py").read_text(encoding="utf-8-sig")
        self.assertIn('"--mode", "edit"', source)
        self.assertIn('"--disallowed-tools", "Bash,Agent,Task"', source)
        self.assertNotIn('"--mode", "build"', source)

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell fixture")
    def test_native_profile_restricted_scope_and_blocks_commit(self):
        with tempfile.TemporaryDirectory(prefix="hif-scope-fixture-") as directory:
            root = Path(directory)
            worker = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
            # The fixed native validation gate runs before any commit/push transport.
            self.assertLess(worker.index("native_validation_profile -eq 'agent-control'"),
                            worker.index("'git commit failed'"))
            self.assertLess(worker.index("'Native infrastructure fixtures failed; diff preserved'"),
                            worker.index("'git commit failed'"))
            scope = next(line.strip() for line in worker.splitlines() if "Native profile is restricted" in line)
            for paths, expected_code in [(["Assets/Scripts/X.cs"], 1),
                                         (["AGENTS.md", "docs/product/plan.md", "tools/agent-control/test_control.py"], 0)]:
                body = "$ErrorActionPreference='Stop'\n"
                body += "$task=[pscustomobject]@{allowed_paths=@(" + ",".join("'" + p + "'" for p in paths) + ")}\n"
                body += scope + "\n'INFRA_SCOPE_OK'\n"
                script = root / "scope.ps1"
                script.write_text(body, encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)], capture_output=True, timeout=15)
                self.assertEqual(expected_code, result.returncode, result.stderr.decode(errors="replace"))
                if expected_code == 0:
                    self.assertIn("INFRA_SCOPE_OK", result.stdout.decode("utf-8"))

    def scheduler_sync_fixture(self, directory):
        root = Path(directory)
        text = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig").replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
        (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name), encoding="utf-8-sig")
        (root/"wake-supervisor.ps1").write_text("Add-Content '"+str(root/"wake-log.txt")+"' 'wake'", encoding="utf-8-sig")
        stub = ("import sys, json\nfrom pathlib import Path\n"
                "c = Path(sys.argv[sys.argv.index('--control') + 1])\n"
                "open(str(c / 'sync-log.txt'), 'a').write('sync\\n')\n"
                "exit_code = c / 'sync-exit.txt'\n"
                "if exit_code.exists():\n    print('actual native stderr', file=sys.stderr)\n    sys.exit(int(exit_code.read_text().strip()))\n"
                "s = json.loads((c / 'state.json').read_text(encoding='utf-8'))\n"
                "s['status'] = 'ROADMAP_SYNC_READY'\n"
                "(c / 'state.json').write_text(json.dumps(s), encoding='utf-8')\n")
        (root/"hif-control.py").write_text(stub, encoding="utf-8")
        (root/"controller.json").write_text(json.dumps({"enabled": True, "native_master_sync_enabled": True}), encoding="utf-8")
        (root/"state.json").write_text(json.dumps({"status": "SYNC_MASTER_PENDING", "active_task_id": "sync"}), encoding="utf-8")
        (root/"queue.json").write_text(json.dumps({"tasks": [{"id": "sync", "status": "MERGED_LOCAL_SYNC"}]}), encoding="utf-8")
        return root

    @unittest.skipUnless(os.name == "nt", "Windows reviewer quota fixture")
    def test_actual_strong_reviewer_usage_limit_preserves_candidate_and_waits(self):
        source = Path(__file__).with_name("hif-reviewer.ps1").read_text(encoding="utf-8-sig")
        guard = source[source.index("    if($code){"):source.index("    if(@(git", source.index("    if($code){"))]
        with tempfile.TemporaryDirectory(prefix="hif-review-quota-fixture-") as directory:
            root = Path(directory)
            state = {"status": "REVIEW_CANDIDATE", "active_task_id": "same", "head_sha": "b"*40}
            c.save(root / "state.json", state)
            (root / "events.jsonl").write_text('{"error":"You have hit your usage limit"}', encoding="utf-8")
            (root / "review.json").write_text('{"verdict":"CLEAN"}', encoding="utf-8")
            prelude = "$ErrorActionPreference='Stop'; $Strong=$true; $code=1; $C='" + directory + "'\n"
            prelude += "$Events=Join-Path $C 'events.jsonl';$Out=Join-Path $C 'review.json';$s=Get-Content (Join-Path $C 'state.json') -Raw -Encoding UTF8|ConvertFrom-Json\n"
            (root / "quota.ps1").write_text(prelude + guard, encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root / "quota.ps1")], capture_output=True, timeout=15)
            self.assertEqual(75, result.returncode, result.stderr.decode(errors="replace"))
            persisted = c.load(root / "state.json")
            self.assertEqual("WAIT_STRONG_REVIEW", persisted["status"])
            self.assertEqual(state["active_task_id"], persisted["active_task_id"])
            self.assertEqual(state["head_sha"], persisted["head_sha"])
            self.assertFalse((root / "review.json").exists())

    @unittest.skipUnless(os.name == "nt", "Windows native validation fixture")
    def test_actual_native_validation_gate_requires_nonzero_pass_and_preserves_failed_report(self):
        worker = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        gate = worker[worker.index("    if($Engine -eq 'ZCode' -and $task.native_validation_profile"):
                      worker.index("    if ($result.status -notin")]
        with tempfile.TemporaryDirectory(prefix="hif-validation-fixture-") as directory:
            root = Path(directory)
            scripts = root / "tools/agent-control"
            scripts.mkdir(parents=True)
            (scripts / "fixture.ps1").write_text("'ok'", encoding="utf-8-sig")
            prelude = "$ErrorActionPreference='Stop'\n"
            prelude += "$Engine='ZCode'; $Repo='" + directory + "'; $LogDir=$Repo; $stamp='fixture'; $id='gate'; $base='" + "a"*40 + "'\n"
            prelude += "$Python='" + sys.executable + "'; $reportPath=Join-Path $Repo 'report.json'\n"
            prelude += "$task=[pscustomobject]@{native_validation_profile='agent-control'; allowed_paths=@('tools/agent-control/test_control.py')}\n"
            prelude += "$result=[pscustomobject]@{status='REVIEW_CANDIDATE';base_sha=$base; validation_complete=$false;validation=@();not_run=@()}\n"
            prelude += "function ChangedFiles { 'tools/agent-control/test_control.py' }; function Allowed { $true }\n"
            prelude += "function WriteJson($v,$p) { $v | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $p -Encoding UTF8 }\n"
            prelude += "function SetProp($o,$n,$v) {$o | Add-Member -NotePropertyName $n -NotePropertyValue $v -Force}\n"
            (root / "gate.ps1").write_text(prelude + gate, encoding="utf-8-sig")
            for output, code, expected in [("Ran 1 test in 0.01s\n\nOK", 0, 0),
                                            ("Ran 1 test in 0.01s\n\nFAILED", 1, 1),
                                            ("Ran 0 tests in 0.01s\n\nOK", 0, 1)]:
                (root / "report.json").write_text('{"validation_complete": false}', encoding="utf-8")
                (scripts / "test_control.py").write_text("import sys\nprint(" + repr(output) + ",file=sys.stderr)\nsys.exit(" + str(code) + ")", encoding="utf-8")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root / "gate.ps1")], capture_output=True, timeout=30)
                self.assertEqual(expected, result.returncode, result.stderr.decode(errors="replace"))
                self.assertIs(c.load(root / "report.json")["validation_complete"], expected == 0)

    def run_loop_once(self, root):
        return subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root/"loop.ps1"), "-Once"], capture_output=True, timeout=30)

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell native sync fixture")
    def test_scheduler_dispatches_native_sync_before_model_wake(self):
        with tempfile.TemporaryDirectory(prefix="hif-syncloop-fixture-") as directory:
            root = self.scheduler_sync_fixture(directory)
            result = self.run_loop_once(root)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual(1, len((root/"sync-log.txt").read_text().splitlines()))
            self.assertFalse((root/"wake-log.txt").exists())
            self.assertEqual("ROADMAP_SYNC_READY", c.load(root/"state.json")["status"])
            # Local sync finished: only now may the scheduler wake the model for roadmap sync.
            result = self.run_loop_once(root)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual(1, len((root/"wake-log.txt").read_text().splitlines()))
            self.assertEqual(1, len((root/"sync-log.txt").read_text().splitlines()))

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell native sync fixture")
    def test_scheduler_sync_failure_blocks_without_model_wake_or_repeat(self):
        with tempfile.TemporaryDirectory(prefix="hif-syncfail-fixture-") as directory:
            root = self.scheduler_sync_fixture(directory)
            (root/"sync-exit.txt").write_text("1", encoding="utf-8")
            result = self.run_loop_once(root)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual(1, len((root/"sync-log.txt").read_text().splitlines()))
            self.assertFalse((root/"wake-log.txt").exists())
            state = c.load(root/"state.json")
            self.assertEqual("BLOCKED", state["status"])
            self.assertIn("Native master sync failed exit 1", state["last_error"])
            self.assertEqual("sync", state["active_task_id"])
            (root/"sync-exit.txt").unlink()
            result = self.run_loop_once(root)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual(1, len((root/"sync-log.txt").read_text().splitlines()))
            self.assertFalse((root/"wake-log.txt").exists())
            self.assertEqual("BLOCKED", c.load(root/"state.json")["status"])


if __name__ == "__main__":
    unittest.main()
