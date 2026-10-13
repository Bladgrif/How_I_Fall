"""Regression tests: no models, filesystem cleanup, GitHub writes or production work."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import struct
import sys
import tempfile
import time
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
            self.assertEqual(6, len(reads))
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
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name), encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name), encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name),encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name), encoding="utf-8-sig")
            (root/"hif-worker.ps1").write_text("Add-Content -LiteralPath '"+str(root/"dispatches.txt")+"' 'dispatch'; Write-Error persistence_failed; exit 78", encoding="utf-8-sig")
            (root/"controller.json").write_text(json.dumps({"enabled": True}), encoding="utf-8")
            (root/"state.json").write_text(json.dumps({"status":"PARTIAL_RETRY", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"queue.json").write_text(json.dumps({"tasks":[{"id":"preserve","status":"PARTIAL_RETRY","writer_engine":"Codex"}]}), encoding="utf-8")
            result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1")], capture_output=True, timeout=15)
            self.assertEqual(78, result.returncode, result.stderr.decode(errors="replace"))
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

    def test_supervisor_reads_independent_review_not_stale_writer_stage(self):
        source = Path(__file__).with_name("wake-supervisor.ps1").read_text(encoding="utf-8-sig")
        self.assertIn("strong-review-latest.json", source)
        self.assertIn("verify task_id/base_sha/head_sha", source)
        self.assertIn("must NOT be rerun", source)
        self.assertIn("set WAIT_CI, not REVIEW_CANDIDATE", source)

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell auth-wait fixture")
    def test_auth_wait_is_quiet_without_model_dispatch(self):
        with tempfile.TemporaryDirectory(prefix="hif-auth-fixture-") as directory:
            root = Path(directory)
            source = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig")
            source = source.replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
            (root/"loop.ps1").write_text(source.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name), encoding="utf-8-sig")
            (root/"controller.json").write_text(json.dumps({"enabled":True}), encoding="utf-8")
            (root/"state.json").write_text(json.dumps({"status":"WAIT_AUTH", "active_task_id":"preserve"}), encoding="utf-8")
            c.save(root/"queue.json", {"tasks": []})
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
             patch.object(c, "save", side_effect=OSError("disk full")), patch.object(c, "control_paths"), patch.object(c, "writer_lock", return_value=__import__("contextlib").nullcontext()):
            with self.assertRaises(c.FatalPersistence):
                c.main()

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
                            worker.index("RunGit @('commit'"))
            self.assertLess(worker.index("'Native infrastructure fixtures failed; diff preserved'"),
                            worker.index("RunGit @('commit'"))
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
        (root/"loop.ps1").write_text(text.replace("Local\\HowIFallSupervisorLoop", "Local\\HowIFallFixture-" + root.name).replace("Local\\HowIFallWriter", "Local\\HowIFallFixtureWriter-" + root.name), encoding="utf-8-sig")
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
            prelude += "function SaveReviewerState($value) {$value|ConvertTo-Json -Depth 30|Set-Content (Join-Path $C 'state.json') -Encoding UTF8}\n"
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
            for name in ("hif-worker.ps1", "hif-reviewer.ps1", "supervisor-loop.ps1", "wake-supervisor.ps1"):
                (scripts / name).write_text("'ok'", encoding="utf-8-sig")
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
                (root / "hif-control.py").write_text("import sys\nprint(" + repr(output) + ",file=sys.stderr)\nsys.exit(" + str(code) + ")", encoding="utf-8")
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

    # --- Fixed native runtime QA: all invocations are mocked, no Unity/LocalLow. ---
    def native_task(self, **overrides):
        return dict(dict(id="native", base_sha="a"*40, resume_head_sha="a"*40,
                         head_sha="b"*40, branch="codex/native", writer_engine="ZCode",
                         writer_path="D:/How_I_Fall/zagent", native_validation_profile="hif-runtime",
                         allowed_paths=["tools/agent-control/test_control.py"],
                         player_facing=False, native_qa={"unity": [{"mode": "EditMode", "filter": "InteractiveHotspotEditModeTests"}], "graphical": []}), **overrides)

    def test_native_known_profiles_and_fixed_selection(self):
        self.assertEqual([], c.native_plan(self.native_task(native_validation_profile="agent-control", native_qa=None)))
        for mode, filters in c.NATIVE_FILTERS.items():
            for name in filters:
                task = self.native_task(native_qa={"unity": [{"mode": mode, "filter": name}], "graphical": []})
                self.assertEqual([("tools/run-unity-tests.ps1", ["-Mode", mode, "-TestFilter", name])], c.native_plan(task))
        for scenario in c.NATIVE_GRAPHICAL:
            task = self.native_task(player_facing=True)
            task["native_qa"]["graphical"] = [scenario]
            self.assertEqual(("tools/run-graphical-e2e.ps1", ["-Scenario", scenario]), c.native_plan(task)[-1])

    def assert_native_candidate_rejected(self, task, control="unused"):
        review = dict(self.strong, task_id=task["id"])
        self.assertEqual("BLOCKED", c.native_proof_status(task, control=control)["status"])
        with self.assertRaises(ValueError):
            c.gate(task, review, review, self.pr, self.checks,
                   ["tools/agent-control/test_control.py"], control=control)

    def test_native_candidate_missing_profile_rejects_ready_and_merge(self):
        task = self.native_task(native_qa=None)
        del task["native_validation_profile"]
        with patch.object(c.subprocess, "check_output") as git, patch.object(c.subprocess, "run") as process:
            self.assert_native_candidate_rejected(task)
            git.assert_not_called()
            process.assert_not_called()

    def test_native_candidate_unknown_profile_rejects_ready_and_merge(self):
        for profile in (None, "", "unknown", [], {}):
            with self.subTest(profile=profile), patch.object(c.subprocess, "check_output") as git:
                self.assert_native_candidate_rejected(self.native_task(native_validation_profile=profile, native_qa=None))
                git.assert_not_called()

    def test_native_candidate_profile_downgrade_with_clean_review_and_retained_proof_rejected(self):
        task, _, run, control = self.native_fixture()
        proof = run()
        task["writer_path"] = "D:/How_I_Fall/zagent"
        task["native_validation_manifest"] = proof["manifest_path"]
        proof.update(source_head_sha=task["head_sha"], writer_path=task["writer_path"])
        c.save(proof["manifest_path"], proof)
        review = dict(self.strong, task_id=task["id"])
        with patch.object(c, "source_fingerprint", return_value="fingerprint"):
            self.assertEqual("READY", c.native_proof_status(task, control=control)["status"])
            self.assertEqual("MERGE_ALLOWED", c.gate(task, review, review, self.pr, self.checks,
                             ["tools/agent-control/test_control.py"], control=control)["status"])
            # Exact-head canonical CLEAN is unchanged; only queue selection drifts.
            task.update(native_validation_profile="agent-control", native_qa=None)
            with patch.object(c.subprocess, "check_output") as git:
                self.assert_native_candidate_rejected(task, control)
                git.assert_not_called()
            del task["native_validation_manifest"]
            task["native_remote_proof"] = {"head_sha": task["head_sha"]}
            self.assert_native_candidate_rejected(task, control)

    def test_native_agent_control_scope_checked_at_plan_review_and_merge_boundaries(self):
        valid = self.native_task(native_validation_profile="agent-control", native_qa=None)
        for allowed in (None, [], "tools/agent-control/test_control.py",
                        ["Assets/X.cs"], ["docs/../Assets/X.md"], ["tools/agent-control/../X.py"],
                        ["docs/"], [None]):
            with self.subTest(allowed=allowed), patch.object(c.subprocess, "check_output") as git:
                task = dict(valid, allowed_paths=allowed)
                with self.assertRaises(ValueError):
                    c.native_plan(task)
                self.assert_native_candidate_rejected(task)
                git.assert_not_called()
        for changed in (["Assets/X.cs"], ["docs/unapproved.md"], ["tools/agent-control/../X.py"]):
            with self.subTest(changed=changed), patch.object(c, "task_changed_files", return_value=changed):
                self.assert_native_candidate_rejected(valid)

    def test_native_agent_control_valid_scope_preserves_ready_and_merge(self):
        task = self.native_task(native_validation_profile="agent-control", native_qa=None)
        review = dict(self.strong, task_id=task["id"])
        with patch.object(c, "task_changed_files", return_value=task["allowed_paths"]) as changed:
            self.assertEqual("READY", c.native_proof_status(task)["status"])
            self.assertEqual("MERGE_ALLOWED", c.gate(task, review, review, self.pr, self.checks,
                             task["allowed_paths"])["status"])
            self.assertGreaterEqual(changed.call_count, 3)

    def test_native_agent_control_scope_includes_renamed_production_source(self):
        task = self.native_task(native_validation_profile="agent-control", native_qa=None)
        def renamed_diff(argv, **kwargs):
            # --name-only with rename detection normally exposes only destination.
            destination = "tools/agent-control/test_control.py\0"
            return "Assets/Old.cs\0"+destination if "--no-renames" in argv else destination
        with patch.object(c.subprocess, "check_output", side_effect=renamed_diff) as git:
            self.assert_native_candidate_rejected(task)
            for call in git.call_args_list:
                self.assertIn("--no-renames", call.args[0])
                self.assertIn(task["base_sha"]+"..."+task["head_sha"], call.args[0])

    @unittest.skipUnless(os.name == "nt", "Windows PowerShell reviewer profile fixture")
    def test_actual_reviewer_blocks_all_invalid_zcode_profiles_before_dispatch(self):
        source = Path(__file__).with_name("hif-reviewer.ps1").read_text(encoding="utf-8-sig")
        valid = self.native_task(native_validation_profile="agent-control", native_qa=None,
                                 writer_path="D:\\How_I_Fall\\zagent")
        missing = dict(valid)
        del missing["native_validation_profile"]
        invalid = [missing, dict(valid, native_validation_profile="unknown"),
                   dict(valid, allowed_paths=["Assets/X.cs"]),
                   dict(valid, native_validation_manifest="retained-runtime-manifest.json")]
        for task in invalid:
            with self.subTest(task=task), tempfile.TemporaryDirectory(prefix="hif-review-profile-fixture-") as directory:
                root = Path(directory)
                script = source.replace("$C='D:\\How_I_Fall\\agent-control'", "$C='"+directory+"'")
                script = script.replace("Local\\HowIFallWriter", "Local\\HowIFallFixture-"+root.name)
                script = script.replace("C:\\Users\\roman\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe", sys.executable)
                # No real Git checkout reads/writes, quota RPC or model dispatch.
                script = script.replace("$mutex=New-Object Threading.Mutex", "function git { if($args -contains 'status'){return}; if($args -contains 'rev-parse'){'"+task["head_sha"]+"';return}; throw 'Unexpected Git probe' }\n$mutex=New-Object Threading.Mutex", 1)
                script = script.replace("    $suffix=", "    Set-Content (Join-Path $C 'unexpected-dispatch.txt') 'DISPATCH'; exit 99\n    $suffix=", 1)
                (root/"reviewer.ps1").write_text(script, encoding="utf-8-sig")
                control_source = Path(__file__).with_name("hif-control.py").read_text(encoding="utf-8-sig")
                control_source = control_source.replace('CONTROL_ROOT = "D:/How_I_Fall/agent-control"', 'CONTROL_ROOT = ' + repr(str(root)))
                control_source = control_source.replace('print(json.dumps(boundary_scope(c, args.phase)))', 'print("SCOPE_VERIFIED") # scope mocked in old profile fixture')
                (root/"hif-control.py").write_text(control_source, encoding="utf-8")
                (root/"hif-batch.py").write_bytes(Path(__file__).with_name("hif-batch.py").read_bytes())
                c.save(root/"queue.json", {"tasks": [task]})
                c.save(root/"state.json", {"status": "REVIEW_CANDIDATE", "active_task_id": task["id"],
                                           "head_sha": task["head_sha"]})
                review_path = root/"strong-review-latest.json"
                c.save(review_path, dict(self.strong, task_id=task["id"]))
                before = review_path.read_bytes()
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root/"reviewer.ps1"), "-Strong"],
                                        capture_output=True, timeout=15)
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
                self.assertEqual("BLOCKED", c.load(root/"state.json")["status"])
                self.assertEqual(task["head_sha"], c.load(root/"state.json")["head_sha"])
                self.assertFalse((root/"unexpected-dispatch.txt").exists())
                self.assertEqual(before, review_path.read_bytes())

    def test_native_unknown_malformed_or_command_arguments_fail_before_process(self):
        bad = [self.native_task(native_validation_profile=p) for p in (None, "", "shell", {}, [])]
        for qa in ({}, {"unity": [], "graphical": []}, {"executable": "cmd.exe"},
                   {"unity": [{"mode":"EditMode", "filter":"InteractiveHotspotEditModeTests", "args":["-nographics"]}], "graphical":[]},
                   {"unity": [{"mode":"EditMode", "filter":"X; Start-Process cmd"}], "graphical":[]},
                   {"unity": [{"mode":"Smoke", "filter":""}], "graphical":[]},
                   {"unity": [{"mode":"PlayMode", "filter":"../MainMenu"}], "graphical":[]},
                   {"unity": "-Mode EditMode", "graphical":[]},
                   {"unity": [{"mode":{}, "filter":[]}], "graphical":[]}):
            bad.append(self.native_task(native_qa=qa))
        for task in bad:
            with patch.object(c.subprocess, "run") as process, patch.object(c.subprocess, "check_output") as git:
                with self.assertRaises((ValueError, TypeError)):
                    c.native_qa(task, "unused")
                process.assert_not_called()
                git.assert_not_called()

    def test_native_protected_path_engine_and_unproven_isolation_rejected(self):
        for repo in ("D:/How_I_Fall/develop", "D:/How_I_Fall/master", "D:/How_I_Fall/zagent/../develop", "D:/How_I_Fall/zagent-extra", "D:/How_I_Fall/agent"):
            with self.assertRaises(ValueError):
                c.native_plan(self.native_task(writer_path=repo))
        for scenario in ("PlayerUi", "SaveBackendV2", "GameMenu", "ManualSave", "Hotspot", "Unknown", "PlayerUi -nographics", "../PlayerUi"):
            task = self.native_task()
            task["native_qa"]["graphical"] = [scenario]
            with self.assertRaises(ValueError):
                c.native_plan(task)
        with self.assertRaises(ValueError):
            c.native_plan(self.native_task(player_facing=True))

    def test_native_xml_failure_zero_missing_cases_and_all_skipped(self):
        with tempfile.TemporaryDirectory(prefix="hif-xml-fixture-") as directory:
            path = Path(directory) / "result.xml"
            for total, passed, failed, result, cases in [(0,0,0,"Passed",""), (1,0,0,"Passed",'<test-case result="Skipped"/>'),
                                                        (1,1,0,"Passed",""), (1,0,1,"Failed",'<test-case result="Failed"/>')]:
                path.write_text(f'<test-run total="{total}" passed="{passed}" failed="{failed}" result="{result}">{cases}</test-run>')
                with self.assertRaises(c.NativeQaError) as caught:
                    c.unity_xml_ok(path)
                if failed:
                    self.assertTrue(caught.exception.retryable)

    def test_native_late_save_overrides_fail_closed_before_any_process(self):
        self.assertEqual({}, c.NATIVE_GRAPHICAL)
        tasks = [self.native_task(native_qa={"unity": [{"mode": "PlayMode", "filter": "SaveLoadFocusOwnershipPlayModeTests"}], "graphical": []})]
        for scenario in ("PlayerUi", "SaveBackendV2", "GameMenu"):
            task = self.native_task(player_facing=True)
            task["native_qa"]["graphical"] = [scenario]
            tasks.append(task)
        for task in tasks:
            with patch.object(c.subprocess, "run") as process, patch.object(c.subprocess, "check_output") as git:
                with self.assertRaises(ValueError):
                    c.native_qa(task, "unused")
                process.assert_not_called()
                git.assert_not_called()

    def native_fixture(self, *, graphical=False, fault=None, repeat_graphical=False):
        directory = tempfile.TemporaryDirectory(prefix="hif-native-fixture-")
        self.addCleanup(directory.cleanup)
        root = Path(directory.name)
        repo = root / "writer"
        repo.mkdir()
        task = self.native_task(player_facing=graphical)
        if graphical:
            task["native_qa"]["graphical"] = ["GameMenu"]
        graphical_fixture = {"GameMenu": ("PlayerUiGraphicalE2ERunner", "player_ui_graphical_result.txt", "PlayerUi")}
        with patch.object(c, "NATIVE_GRAPHICAL", graphical_fixture):
            plan = c.native_plan(task)
        if repeat_graphical:
            # Fixture-only replay simulates later launcher cleanup of shared output.
            # Duplicate selection is still prohibited by the real native_plan.
            plan.append(plan[-1])
        task["writer_path"] = str(repo)  # Fixture-only path injection AFTER real policy check.
        (repo / "tools").mkdir()
        # Fixture-only bounded list, NEVER a production startup isolation claim.
        mock_source = "GameMenu = @{ ProofFiles = @('game_menu_root_1920x1080.png', 'game_menu_pointer_hover_quit_1920x1080.png', 'game_menu_keyboard_focus_return_1920x1080.png') }"
        (repo / "tools/run-graphical-e2e.ps1").write_text(mock_source)
        calls = []

        def process(argv, **kwargs):
            calls.append((argv, kwargs))
            self.assertEqual(repo, kwargs["cwd"])
            self.assertNotIn("UNITY_EDITOR_PATH", kwargs["env"])
            self.assertNotIn("-nographics", argv)
            def write(relative, data):
                path = repo / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                if fault == "stale":
                    os.utime(path, (1, 1))
                return path
            if "-Mode" in argv:
                stem = "Temp/CodexTests/EditMode_InteractiveHotspotEditModeTests"
                write(stem + ".log", b"error CS1234: broken" if fault == "compile" else b"Unity test log")
                if fault not in ("missing-xml", "compile"):
                    total = "0" if fault == "zero" else "1"
                    failed = "1" if fault == "failed" else "0"
                    result = "Failed" if failed == "1" else "Passed"
                    write(stem + "_results.xml", f'<test-run total="{total}" passed="1" failed="{failed}" result="{result}"><test-case result="{result}"/></test-run>'.encode())
            else:
                proof_dir = repo / "QAArtifacts/GraphicalE2E/PlayerUi"
                if proof_dir.exists():
                    for previous in proof_dir.iterdir():
                        previous.unlink()  # Only generated fixture originals.
                write("Temp/CodexTests/graphical_GameMenu.log", b"Graphical log")
                write("Temp/CodexTests/graphical_preflight.log", b"Preflight")
                if fault != "missing-sentinel":
                    write("player_ui_graphical_result.txt", b"status=PASS\nplayerPrefsRestored=true\n")
                import re
                line = next(line for line in mock_source.splitlines() if line.strip().startswith("GameMenu = @{"))
                for i, name in enumerate(re.findall(r"'([^'/\\]+\.png)'", line)):
                    if fault == "missing-png" and i == 0:
                        continue
                    size = (1920, 1080) if "1920x1080" in name else (1280, 720)
                    if fault == "wrong-size":
                        size = (2, 2)
                    write("QAArtifacts/GraphicalE2E/PlayerUi/" + name, b"\x89PNG\r\n\x1a\n" + b"\0"*8 + struct.pack(">II", *size) + str(len(calls)).encode())
            return subprocess.CompletedProcess(argv, 1 if fault == "exit" else 0, b"native fixture", b"")

        def git(_, *args):
            return {("rev-parse", "HEAD"):"a"*40, ("branch", "--show-current"):"codex/native",
                    ("rev-parse", "--show-toplevel"):str(repo)}[args]

        def harness(_, base, relative):
            if fault == "version" and relative == "ProjectSettings/ProjectVersion.txt":
                raise c.NativeQaError("Unapproved Unity executable version input")

        def run():
            with patch.object(c, "NATIVE_GRAPHICAL", graphical_fixture), patch.object(c, "native_plan", return_value=plan), patch.object(c, "git_output", side_effect=git), \
                 patch.object(c, "source_fingerprint", return_value="fingerprint"), \
                 patch.object(c, "unchanged_harness", side_effect=harness), patch.object(c.subprocess, "run", side_effect=process), \
                 patch.dict(os.environ, SystemRoot="C:/Windows", UNITY_EDITOR_PATH="evil.exe"):
                return c.native_qa(task, root)
        return task, calls, run, root

    def test_native_safe_invocations_fresh_xml_and_head_bound_archive(self):
        task, calls, run, _ = self.native_fixture()
        outcome = run()
        self.assertEqual("PASS", outcome["status"])
        self.assertIs(True, outcome["validation_complete"])
        self.assertEqual(task["resume_head_sha"], outcome["source_head_sha"])
        self.assertEqual("ZCode", outcome["writer_engine"])
        self.assertEqual(1, len(calls))
        self.assertEqual(["-Mode", "EditMode", "-TestFilter", "InteractiveHotspotEditModeTests"], calls[0][0][-4:])
        for entry in outcome["files"]:
            self.assertEqual(entry["sha256"], c.sha256(Path(outcome["manifest_path"]).parent / entry["filename"]))

    def test_native_failed_zero_stale_or_missing_xml_never_complete(self):
        for fault in ("failed", "zero", "stale", "missing-xml", "exit", "compile"):
            _, _, run, _ = self.native_fixture(fault=fault)
            outcome = run()
            self.assertIs(False, outcome["validation_complete"])
            self.assertEqual("PARTIAL_RETRY" if fault in ("failed", "exit", "compile") else "BLOCKED", outcome["status"], fault)
            self.assertTrue(Path(outcome["manifest_path"]).is_file())

    def test_native_graphical_originals_logs_sentinel_and_curated_manifest(self):
        _, calls, run, _ = self.native_fixture(graphical=True)
        outcome = run()
        self.assertEqual("PASS", outcome["status"])
        self.assertEqual(2, len(calls))
        self.assertEqual(["-Scenario", "GameMenu"], calls[-1][0][-2:])
        self.assertEqual(3, sum(f["filename"].endswith(".png") for f in outcome["files"]))
        self.assertIn("NOT VERIFIED", outcome["visual_inspection"])
        self.assertEqual("NOT VERIFIED", outcome["remote_proof"])

    def test_native_missing_fresh_graphical_proof_blocks(self):
        for fault in ("missing-sentinel", "missing-png", "wrong-size"):
            _, _, run, _ = self.native_fixture(graphical=True, fault=fault)
            outcome = run()
            self.assertEqual("BLOCKED", outcome["status"], fault)
            self.assertIs(False, outcome["validation_complete"])

    def test_native_originals_archived_before_later_shared_launcher_cleanup(self):
        task, calls, run, _ = self.native_fixture(graphical=True, repeat_graphical=True)
        outcome = run()
        self.assertEqual("PASS", outcome["status"])
        self.assertEqual(3, len(calls))
        evidence = Path(outcome["manifest_path"]).parent
        originals = [entry for entry in outcome["files"] if entry["filename"].endswith(".png")]
        self.assertEqual(6, len(originals))
        for entry in originals:
            expected_run = b"2" if entry["filename"].startswith("1-") else b"3"
            self.assertTrue((evidence / entry["filename"]).read_bytes().endswith(expected_run))
            self.assertEqual(entry["sha256"], c.sha256(evidence / entry["filename"]))
        live = Path(task["writer_path"]) / "QAArtifacts/GraphicalE2E/PlayerUi/game_menu_root_1920x1080.png"
        self.assertTrue(live.read_bytes().endswith(b"3"))

    def test_native_changed_harness_rejected_and_path_escape_rejected(self):
        with tempfile.TemporaryDirectory(prefix="hif-harness-fixture-") as directory:
            root = Path(directory)
            (root/"launcher.ps1").write_bytes(b"evil")
            with patch.object(c.subprocess, "check_output", return_value=b"approved"):
                with self.assertRaises(c.NativeQaError):
                    c.unchanged_harness(root, "a"*40, "launcher.ps1")
            with self.assertRaises(c.NativeQaError):
                c.bounded_path(root, "../escape")

    @unittest.skipUnless(os.name == "nt", "Windows junction escape preflight")
    def test_native_all_write_roots_and_later_graphical_outputs_preflight_zero_launches(self):
        paths = list(c.UNITY_WRITE_ROOTS) + ["Library/nested", "Temp/CodexTests", "QAArtifacts/GraphicalE2E/PlayerUi"]
        for relative in paths:
            task, calls, run, control = self.native_fixture(graphical=True)
            repo = Path(task["writer_path"])
            link = repo / relative
            target = control / "outside-writer"
            target.mkdir()
            link.parent.mkdir(parents=True, exist_ok=True)
            result = subprocess.run(["cmd.exe", "/c", "mklink", "/J", str(link), str(target)], capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            try:
                outcome = run()
                self.assertEqual("BLOCKED", outcome["status"], relative)
                self.assertFalse(outcome["validation_complete"])
                self.assertEqual([], calls, relative)
                self.assertEqual([], list(target.iterdir()))
            finally:
                link.rmdir()  # Remove the fixture junction itself, NEVER its target.

    def test_native_reparse_output_files_and_sentinel_preflight_zero_launches(self):
        from types import SimpleNamespace
        for relative in ("Temp/CodexTests/EditMode_InteractiveHotspotEditModeTests_results.xml",
                         "Temp/CodexTests/EditMode_InteractiveHotspotEditModeTests.log",
                         "Temp/CodexTests/graphical_preflight.log", "player_ui_graphical_result.txt", "Assembly-CSharp.csproj"):
            task, calls, run, _ = self.native_fixture(graphical=True)
            file = Path(task["writer_path"]) / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b"preserved fixture")
            original_lstat = Path.lstat
            def lstat(path):
                result = original_lstat(path)
                return SimpleNamespace(st_file_attributes=0x400, st_mode=result.st_mode) if path == file else result
            with patch.object(Path, "lstat", lstat):
                outcome = run()
            self.assertEqual("BLOCKED", outcome["status"], relative)
            self.assertEqual([], calls)
            self.assertEqual(b"preserved fixture", file.read_bytes())

    def test_native_version_executable_input_must_match_approved_base_before_launch(self):
        _, calls, run, _ = self.native_fixture(fault="version")
        outcome = run()
        self.assertEqual("BLOCKED", outcome["status"])
        self.assertFalse(outcome["validation_complete"])
        self.assertEqual([], calls)

    def test_native_existing_sentinel_and_unbounded_graphical_coverage_zero_launches(self):
        for fault in ("existing-sentinel", "broad-coverage"):
            task, calls, run, _ = self.native_fixture(graphical=True)
            repo = Path(task["writer_path"])
            if fault == "existing-sentinel":
                (repo / "player_ui_graphical_result.txt").write_bytes(b"do not overwrite")
            else:
                # Actual existing broad launcher cannot be sampled arbitrarily.
                (repo / "tools/run-graphical-e2e.ps1").write_bytes((Path(__file__).parents[1] / "run-graphical-e2e.ps1").read_bytes())
            outcome = run()
            self.assertEqual("BLOCKED", outcome["status"])
            self.assertEqual([], calls)
            self.assertFalse(outcome["validation_complete"])
            if fault == "broad-coverage":
                self.assertIn("REVIEWER VISUAL PROOF NOT AVAILABLE", outcome["error"])

    def test_native_stale_pass_rejected_when_current_selection_or_invocation_changes(self):
        task, _, run, control = self.native_fixture()
        outcome = run()
        path = Path(outcome["manifest_path"])
        outcome["source_head_sha"] = task["head_sha"]
        task["native_validation_manifest"] = str(path)
        original_plan = [(entry["launcher"], entry["arguments"]) for entry in outcome["selection"]]
        with patch.object(c, "native_plan", return_value=original_plan) as planner, \
             patch.object(c, "source_fingerprint", return_value="fingerprint"):
            c.save(path, outcome)
            c.native_proof_ok(task, control=control)
            changed_plan = [("tools/run-unity-tests.ps1", ["-Mode", "EditMode", "-TestFilter", "SavePaginationEditModeTests"])]
            for plan in (changed_plan, original_plan + changed_plan, []):
                planner.return_value = plan
                self.assertEqual("BLOCKED", c.native_proof_status(task, control=control)["status"])
            planner.return_value = original_plan
            for invocations in ([], [dict(outcome["invocations"][0], exit_code=1)],
                                [dict(outcome["invocations"][0], exit_code=False)],
                                [dict(outcome["invocations"][0], arguments=["-Mode", "Smoke"])],
                                outcome["invocations"] * 2):
                c.save(path, dict(outcome, invocations=invocations))
                self.assertEqual("BLOCKED", c.native_proof_status(task, control=control)["status"])
            c.save(path, dict(outcome, selection=[]))
            self.assertEqual("BLOCKED", c.native_proof_status(task, control=control)["status"])

    def test_native_bind_rejects_changed_plan_failure_or_missing_originals_before_write(self):
        task, _, run, control = self.native_fixture()
        outcome = run()
        path = Path(outcome["manifest_path"])
        task["native_validation_manifest"] = str(path)
        c.save(control/"queue.json", {"tasks": [task]})
        c.save(control/"state.json", {"active_task_id": task["id"]})
        plan = [(entry["launcher"], entry["arguments"]) for entry in outcome["selection"]]
        with patch.object(sys, "argv", ["hif-control.py", "bind-native-proof", "--control", str(control)]), \
             patch.object(c, "native_plan", return_value=plan), \
             patch.object(c, "source_fingerprint", return_value="fingerprint"), \
             patch.object(c, "git_output", return_value=task["head_sha"]):
            for change in ({"selection": []}, {"validation_complete": False},
                           {"invocations": [dict(outcome["invocations"][0], exit_code=1)]},
                           {"files": outcome["files"][:1]}):
                c.save(path, dict(outcome, **change))
                before = path.read_bytes()
                with self.assertRaises((ValueError, OSError)):
                    c.main()
                self.assertEqual(before, path.read_bytes())
            c.save(path, outcome)
            c.main()
            self.assertEqual(task["head_sha"], c.load(path)["source_head_sha"])

    def test_native_two_corrections_then_block_exact_identity_retained(self):
        task = self.native_task()
        identity = {k:task[k] for k in ("writer_engine", "writer_path", "base_sha", "branch", "resume_head_sha")}
        outcome = dict(status="PARTIAL_RETRY", error="implementation test failed", manifest_path="fixture/manifest.json")
        self.assertEqual("PARTIAL_RETRY", c.native_retry(task, outcome))
        self.assertEqual("PARTIAL_RETRY", c.native_retry(task, outcome))
        self.assertEqual("BLOCKED", c.native_retry(task, outcome))
        self.assertEqual(2, task["native_correction_attempts"])
        self.assertEqual(identity, {k:task[k] for k in identity})
        self.assertIn("SAME checkout/branch/engine/head", task["correction"])
        self.assertEqual("WAIT_AUTH", c.native_retry(task, dict(outcome, status="WAIT_AUTH")))
        self.assertEqual("BLOCKED", c.native_retry(task, dict(outcome, status="BLOCKED")))

    def test_native_cli_persists_correction_limit_and_never_transports_git(self):
        with tempfile.TemporaryDirectory(prefix="hif-native-cli-fixture-") as directory:
            root = Path(directory)
            task = self.native_task(status="RUNNING")
            c.save(root/"queue.json", {"tasks":[task]})
            c.save(root/"state.json", {"active_task_id":"native", "status":"RUNNING"})
            outcome = dict(status="PARTIAL_RETRY", error="test failure", validation_complete=False,
                           manifest_path=str(root/"evidence/native-1/manifest.json"))
            for expected in ("PARTIAL_RETRY", "PARTIAL_RETRY", "BLOCKED"):
                with patch.object(sys, "argv", ["hif-control.py", "native-qa", "--control", directory, "--output", str(root/"outcome.json")]), \
                     patch.object(c, "native_qa", return_value=outcome), patch.object(c.subprocess, "check_output") as git:
                    c.main()
                    git.assert_not_called()
                persisted = c.load(root/"queue.json")["tasks"][0]
                self.assertEqual(expected, persisted["status"])
                self.assertEqual(expected, c.load(root/"state.json")["status"])
                self.assertIs(False, c.load(root/"outcome.json")["validation_complete"])
                for key in ("writer_engine", "writer_path", "base_sha", "branch", "resume_head_sha"):
                    self.assertEqual(task[key], persisted[key])

    def test_native_proof_wait_or_block_is_not_clean_visual_acceptance(self):
        task, _, run, control = self.native_fixture(graphical=True)
        outcome = run()
        path = Path(outcome["manifest_path"])
        outcome["source_head_sha"] = task["head_sha"]
        c.save(path, outcome)
        task["native_validation_manifest"] = str(path)
        plan = [(entry["launcher"], entry["arguments"]) for entry in outcome["selection"]]
        with patch.object(c, "native_plan", return_value=plan), patch.object(c, "source_fingerprint", return_value="fingerprint"):
            self.assertEqual("WAIT_VISUAL_PROOF", c.native_proof_status(task, control=control)["status"])
            outcome["validation_complete"] = False
            c.save(path, outcome)
            self.assertEqual("BLOCKED", c.native_proof_status(task, control=control)["status"])
            with self.assertRaises(ValueError):
                c.native_proof_ok(task, control=control.parent)

    @unittest.skipUnless(os.name == "nt", "Windows 5.1 infrastructure correction fixture")
    def test_native_infrastructure_corrections_persist_twice_then_block(self):
        source = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        start = source.index("function RequestNativeCorrection(")
        body = source[start:source.index("$mutex =", start)]
        with tempfile.TemporaryDirectory(prefix="hif-native-infra-retry-fixture-") as directory:
            root = Path(directory)
            c.save(root/"task.json", self.native_task())
            for expected, count in [("PARTIAL_RETRY", 1), ("PARTIAL_RETRY", 2), ("BLOCKED", 2)]:
                prelude = "$ErrorActionPreference='Stop';$C='"+directory+"';$task=Get-Content (Join-Path $C 'task.json') -Raw|ConvertFrom-Json;$s=[pscustomobject]@{};$q=[pscustomobject]@{}\n"
                prelude += "$testLog='fixture.log';$reportPath=Join-Path $C 'report.json';$result=[pscustomobject]@{status='REVIEW_CANDIDATE';validation_complete=$true}\n"
                prelude += "function SetProp($o,$n,$v){$o|Add-Member -NotePropertyName $n -NotePropertyValue $v -Force};function WriteJson($v,$p){$v|ConvertTo-Json -Depth 10|Set-Content $p -Encoding UTF8}\n"
                prelude += "function SaveControl($q,$s){WriteJson $task (Join-Path $C 'task.json');WriteJson $s (Join-Path $C 'state.json')}\n"
                (root/"retry.ps1").write_text(prelude+body+"\nRequestNativeCorrection 'implementation failure'\nthrow 'Commit reached'", encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root/"retry.ps1")], capture_output=True, timeout=15)
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
                persisted = c.load(root/"task.json")
                self.assertEqual(expected, persisted["status"])
                self.assertEqual(count, persisted["native_correction_attempts"])
                self.assertEqual("ZCode", persisted["writer_engine"])
                self.assertIs(False, c.load(root/"report.json")["validation_complete"])

    @unittest.skipUnless(os.name == "nt", "Windows 5.1 native stderr Git transport fixture")
    def test_native_git_warning_is_not_failure_but_nonzero_exit_is(self):
        source = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        start = source.index("function RunGit(")
        body = source[start:source.index("function ChangedFiles", start)]
        with tempfile.TemporaryDirectory(prefix="hif-native-stderr-fixture-") as directory:
            root = Path(directory)
            for exit_code in (0, 1):
                (root/"native.py").write_text("import sys\nprint('warning: fixture CRLF',file=sys.stderr)\nprint('actual output')\nsys.exit("+str(exit_code)+")\n")
                script = "$ErrorActionPreference='Stop';$Repo='fixture'\n"
                script += "function git { & '"+sys.executable+"' '"+str(root/"native.py")+"' }\n"
                script += body + "\nRunGit @('diff','--check')\n"
                (root/"transport.ps1").write_text(script, encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root/"transport.ps1")], capture_output=True, timeout=15)
                self.assertEqual(exit_code, result.returncode, result.stderr.decode(errors="replace"))
                if exit_code == 0:
                    self.assertIn("actual output", result.stdout.decode("utf-8"))

    def test_native_remote_publication_required_and_tampering_rejected(self):
        task, _, run, control = self.native_fixture(graphical=True)
        outcome = run()
        manifest = Path(outcome["manifest_path"])
        outcome["source_head_sha"] = task["head_sha"]
        c.save(manifest, outcome)
        task["native_validation_manifest"] = str(manifest)
        plan = [(entry["launcher"], entry["arguments"]) for entry in outcome["selection"]]
        with patch.object(c, "native_plan", return_value=plan), patch.object(c, "source_fingerprint", return_value="fingerprint"):
            with self.assertRaises(ValueError):
                c.native_proof_ok(task, control=control)
            for route, url in [("drive", "https://drive.google.com/file/d/fixture/view"),
                               ("evidence-branch", "https://github.com/Bladgrif/How_I_Fall/tree/evidence/native"),
                               ("visual-review", "https://github.com/Bladgrif/How_I_Fall/actions/runs/1/artifacts/2")]:
                task["native_remote_proof"] = dict(route=route, url=url, head_sha=task["head_sha"], manifest_sha256=c.sha256(manifest))
                c.native_proof_ok(task, control=control)
            task["native_remote_proof"]["head_sha"] = "c"*40
            with self.assertRaises(ValueError):
                c.native_proof_ok(task, control=control)
            task["native_remote_proof"]["head_sha"] = task["head_sha"]
            task["native_remote_proof"]["url"] = "file:///C:/local.png"
            with self.assertRaises(ValueError):
                c.native_proof_ok(task, control=control)
            original = manifest.parent / outcome["files"][0]["filename"]
            original.write_bytes(b"tampered")
            with self.assertRaises(ValueError):
                c.native_proof_ok(task, control=control)

    @unittest.skipUnless(os.name == "nt", "Windows 5.1 actual native worker gate fixture")
    def test_native_worker_incomplete_qa_cannot_reach_commit(self):
        source = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        start = source.index("    if($Engine -eq 'ZCode' -and $task.native_validation_profile -eq 'hif-runtime' -and $result.status")
        gate = source[start:source.index("    if($Engine -eq 'ZCode' -and $task.native_validation_profile -eq 'agent-control'", start)]
        with tempfile.TemporaryDirectory(prefix="hif-native-worker-fixture-") as directory:
            root = Path(directory)
            task = self.native_task(status="RUNNING", allowed_paths=["docs/test.md"])
            outcome = dict(status="PARTIAL_RETRY", error="fixture failure")
            c.save(root/"queue.json", {"tasks":[dict(task, status="PARTIAL_RETRY")]})
            c.save(root/"state.json", {"status":"PARTIAL_RETRY"})
            c.save(root/"fixture-native-native-outcome.json", outcome)
            stub = root/"hif-control.py"
            stub.write_text("# native gate fixture: no real QA\n")
            prelude = "$ErrorActionPreference='Stop'; $Engine='ZCode';$id='native';$stamp='fixture';$base='" + "a"*40 + "';$branch='codex/native'\n"
            prelude += "$ControlDir='"+directory+"';$LogDir=$ControlDir;$QueuePath=Join-Path $ControlDir 'queue.json';$StatePath=Join-Path $ControlDir 'state.json';$reportPath=Join-Path $ControlDir 'report.json'\n"
            prelude += "$Python='"+sys.executable+"'; $task=(Get-Content $QueuePath -Raw|ConvertFrom-Json).tasks[0]\n"
            prelude += "$result=[pscustomobject]@{status='REVIEW_CANDIDATE';base_sha=$base;validation_complete=$true;risks=@()}\n"
            prelude += "function ChangedFiles {'docs/test.md'};function Allowed {$true};function WriteJson($v,$p){$v|ConvertTo-Json -Depth 10|Set-Content $p -Encoding UTF8}\n"
            prelude += "function RunGit($a){if($a[0] -eq 'branch'){'codex/native'}elseif($a[0] -eq 'rev-parse'){'"+"a"*40+"'}}\n"
            (root/"worker-gate.ps1").write_text(prelude + gate + "\nthrow 'UNSAFE: commit reached'\n", encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root/"worker-gate.ps1")], capture_output=True, timeout=20)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertIs(False, c.load(root/"report.json")["validation_complete"])
            self.assertEqual("PARTIAL_RETRY", c.load(root/"state.json")["status"])

    @unittest.skipUnless(os.name == "nt", "Windows 5.1 actual retry identity guard fixture")
    def test_native_partial_retry_never_switches_or_transfers_head(self):
        source = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        start = source.index("    elseif ($mode -eq 'PARTIAL_RETRY') {")
        body = source[start:source.index("    & $Python (Join-Path $PSScriptRoot 'hif-control.py') scope-check", start)].replace("elseif", "if", 1)
        with tempfile.TemporaryDirectory(prefix="hif-native-identity-fixture-") as directory:
            root = Path(directory)
            for branch, head, expected in [("codex/native", "a"*40, 0), ("other", "a"*40, 1), ("codex/native", "b"*40, 1)]:
                prelude = "$ErrorActionPreference='Stop';$mode='PARTIAL_RETRY';$branch='codex/native';$Repo='fixture';$task=[pscustomobject]@{resume_head_sha='"+"a"*40+"'}\n"
                prelude += "function git {if($args -contains 'branch'){'"+branch+"'}else{'"+head+"'}};function RunGit {throw 'No switch permitted'}\n"
                (root/"identity.ps1").write_text(prelude + body, encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root/"identity.ps1")], capture_output=True, timeout=15)
                self.assertEqual(expected, result.returncode, result.stderr.decode(errors="replace"))



class MasterRecoveryTests(unittest.TestCase):
    """All Git writes are confined to temporary repositories, GitHub is a fixture."""
    def setUp(self):
        ControlTests.setUp(self)
        directory = tempfile.TemporaryDirectory(prefix="hif-recovery-fixture-")
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.control = self.root / "control"; self.control.mkdir()
        self.remote = self.root / "origin.git"
        self.writer = self.root / "writer"; self.writer.mkdir()
        self.master = self.root / "master"
        self.writes = []
        def run(repo, *args):
            return subprocess.check_output(["git", "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                                           "-C", str(repo), *args], encoding="utf-8", stderr=subprocess.DEVNULL).strip()
        self.git = run
        run(self.root, "init", "--bare", str(self.remote))
        run(self.writer, "init", "-b", "master")
        (self.writer / "docs").mkdir()
        (self.writer / "docs/technical_plan.md").write_text("base", encoding="utf-8")
        run(self.writer, "add", "docs/technical_plan.md"); run(self.writer, "commit", "-m", "base")
        self.base = run(self.writer, "rev-parse", "HEAD")
        run(self.writer, "remote", "add", "origin", str(self.remote))
        run(self.writer, "push", "origin", "master")
        run(self.root, "clone", "--branch", "master", str(self.remote), str(self.master))
        self.chain = []; self.api = {}; previous = self.base
        for number in (71, 72):
            run(self.writer, "switch", "-c", "pr" + str(number))
            name = "docs/pr" + str(number) + ".md"
            (self.writer / name).write_text(str(number), encoding="utf-8")
            run(self.writer, "add", name); run(self.writer, "commit", "-m", "candidate " + str(number))
            head = run(self.writer, "rev-parse", "HEAD")
            # PR #71/#72 used squash: one parent, a distinct commit, exact head tree.
            run(self.writer, "switch", "master"); run(self.writer, "merge", "--squash", "pr" + str(number))
            run(self.writer, "commit", "-m", "squash " + str(number))
            merge = run(self.writer, "rev-parse", "HEAD")
            self.assertNotEqual(head, merge)
            self.assertEqual(previous, run(self.writer, "show", "-s", "--format=%P", merge))
            self.assertEqual(run(self.writer, "rev-parse", head + "^{tree}"), run(self.writer, "rev-parse", merge + "^{tree}"))
            review = dict(self.strong, task_id="manual-pr-" + str(number), base_sha=previous, head_sha=head)
            evidence = self.control / "evidence" / ("pr" + str(number)); evidence.mkdir(parents=True)
            c.save(evidence / "strong-review-latest.json", review)
            body = {k:v for k,v in review.items() if k not in {"task_id", "base_sha", "head_sha", "reviewer_model", "reviewer_reasoning", "reviewed_at"}}
            thread = "00000000-0000-0000-0000-" + str(number).zfill(12)
            events = [dict(type="thread.started", thread_id=thread), dict(type="item.completed", item=dict(type="agent_message", text=json.dumps(body))), dict(type="turn.completed")]
            (evidence / "strong-review-latest-events.jsonl").write_text("\n".join(json.dumps(e) for e in events), encoding="utf-8")
            entry = dict(pr_number=number, base_sha=previous, head_sha=head, merge_sha=merge, player_facing=False,
                         review=dict(path=(evidence / "strong-review-latest.json").relative_to(self.control).as_posix(),
                                     events_path=(evidence / "strong-review-latest-events.jsonl").relative_to(self.control).as_posix(),
                                     sha256=c.hashlib.sha256((evidence / "strong-review-latest.json").read_bytes()).hexdigest(),
                                     events_sha256=c.hashlib.sha256((evidence / "strong-review-latest-events.jsonl").read_bytes()).hexdigest(), thread_id=thread))
            self.chain.append(entry)
            self.api["pulls/" + str(number)] = dict(number=number, state="closed", merged=True, merge_commit_sha=merge,
                                                    base=dict(ref="master", sha=previous, repo=dict(full_name="Bladgrif/How_I_Fall")), head=dict(sha=head))
            for sha in (merge, head):
                self.api["commits/" + sha] = dict(sha=sha, parents=[dict(sha=previous)],
                                                commit=dict(tree=dict(sha=run(self.writer, "rev-parse", sha + "^{tree}"))))
            for target in (previous, head):
                self.api["compare/" + previous + "..." + target] = dict(status="identical" if target == previous else "ahead",
                                                                     merge_base_commit=dict(sha=previous), files=[dict(filename=name, status="added")])
            previous = merge
        run(self.writer, "push", "origin", "master")
        self.request = dict(version=1, local_sha=self.base, target_sha=previous, chain=self.chain)
        self.api["branches/master"] = dict(commit=dict(sha=previous))
        c.save(self.control / "controller.json", dict(enabled=True, native_master_sync_enabled=True))
        c.save(self.control / "queue.json", dict(tasks=[]))
        c.save(self.control / "state.json", dict(status="WAIT_USER", active_task_id=None))
        # Fake seals are fixture-only; assertions validate payload equality, not DPAPI.
        self.seal = lambda p: json.dumps(p, sort_keys=True)
        def verify(encoded, payload):
            if encoded != self.seal(payload):
                raise ValueError("Fixture receipt mismatch")
        self.verify = verify
        def tracked(repo, *args):
            self.assertEqual(self.master.resolve(), Path(repo).resolve())
            if args[0] in ("fetch", "merge"):
                self.writes.append(args)
            return run(repo, *args)
        for name, kwargs in (("MASTER_CHECKOUT", dict(new=str(self.master))), ("MASTER_ORIGIN", dict(new=str(self.remote))),
                             ("github", dict(side_effect=lambda path: copy.deepcopy(self.api[path]))),
                             ("ci_checks", dict(side_effect=lambda head: dict(workflow_run=dict(id=1, run_attempt=1, head_sha=head, status="completed", conclusion="success"),
                                  check_runs=[dict(id=1, name="CI Gate", head_sha=head, app=dict(slug="github-actions"), status="completed", conclusion="success")]))),
                             ("git_output", dict(side_effect=tracked)), ("receipt_seal", dict(side_effect=self.seal)),
                             ("verify_receipt", dict(side_effect=verify)), ("recovery_scheduler_origin", dict(return_value=None))):
            patcher = patch.object(c, name, **kwargs); patcher.start(); self.addCleanup(patcher.stop)

    def approve(self, request=None):
        result = c.propose_master_recovery(self.control, request or self.request)
        with patch.object(c, "operator_origin"), patch.object(sys.stdin, "isatty", return_value=True), patch.object(sys.stdout, "isatty", return_value=True), patch("builtins.print"):
            c.approve_master_recovery(self.control, result["task_id"], result["digest"], "operator-console:fixture", input_fn=lambda prompt: ("ATTEST NATIVE REVIEWS " if "ATTEST NATIVE" in prompt else "APPROVE RECOVERY ") + result["digest"])
        return result

    def test_original_manual_merge_gap_still_refused_by_normal_sync(self):
        # Exact confirmed source case: no automated execution identity existed.
        fx = ControlTests.sync_fixture(self, "0a4dad45a73177ac74d9740647ec890e5313c935",
                status="WAIT_USER", queue=[], state={"active_task_id": None},
                remote_master="7f79c708a47419c08a90e4f8f9d78db7760ce419")
        with patch.object(c, "github", return_value={"commit": {"sha": fx["remote"]}}) as api, \
             patch.object(c, "git_output", return_value="0a4dad45a73177ac74d9740647ec890e5313c935") as git:
            with self.assertRaisesRegex(ValueError, "SYNC_MASTER_PENDING"):
                c.sync_master(fx["root"])
            api.assert_not_called(); git.assert_not_called()

    def test_successful_real_ff_chain_and_idempotent_repeat_no_done(self):
        result = self.approve()
        original = copy.deepcopy(self.request)
        synced = c.sync_master(self.control)
        self.assertFalse(synced["already_synced"])
        self.assertEqual([("fetch", "origin", "master"), ("merge", "--ff-only", self.request["target_sha"])], self.writes)
        self.assertEqual(self.request["target_sha"], self.git(self.master, "rev-parse", "HEAD"))
        q = c.load(self.control / "queue.json"); state = c.load(self.control / "state.json")
        task = q["tasks"][0]
        self.assertEqual(original, task["recovery_request"])
        self.assertEqual(c.RECOVERY_KIND, task["origin"])
        self.assertEqual([None, None], [r["registered_task_id"] for r in task["reconciliations"]])
        self.assertNotIn("dispatch_identity", task)
        self.assertEqual("MERGED_LOCAL_SYNC_DONE", task["status"])
        self.assertEqual("ROADMAP_SYNC_READY", state["status"])
        self.assertEqual("ACTIVE", c.batch.advance(q, state, {}, None, None, c.dt.datetime.now(c.dt.timezone.utc), self.verify, self.seal)[2])
        self.assertTrue(c.sync_master(self.control)["already_synced"])
        self.assertEqual(2, len(self.writes))

    def test_squash_requires_exact_existing_commit_and_tree_identities(self):
        entry = self.chain[0]
        for mode in ("different_tree", "missing_merge_tree", "missing_head_tree", "invalid_tree", "wrong_head_commit", "wrong_merge_commit"):
            api = copy.deepcopy(self.api)
            merge = api["commits/" + entry["merge_sha"]]
            head = api["commits/" + entry["head_sha"]]
            if mode == "different_tree":
                # Use an actual different Git tree, not just a fabricated digest.
                merge["commit"]["tree"]["sha"] = self.git(self.writer, "rev-parse", self.base + "^{tree}")
            elif mode == "missing_merge_tree": merge["commit"].pop("tree")
            elif mode == "missing_head_tree": head.pop("commit")
            elif mode == "invalid_tree": merge["commit"]["tree"]["sha"] = head["commit"]["tree"]["sha"] = "not-a-tree"
            elif mode == "wrong_head_commit": head["sha"] = self.base
            elif mode == "wrong_merge_commit": merge["sha"] = self.base
            with self.subTest(mode=mode), patch.object(c, "github", side_effect=lambda path: copy.deepcopy(api[path])):
                with self.assertRaises(ValueError): c.propose_master_recovery(self.control, self.request)
                self.assertEqual([], self.writes)

    def test_direct_commit_without_matching_merged_pr_is_not_squash(self):
        # Same tree/sole parent alone must not substitute for the exact merged PR.
        entry = self.chain[0]
        self.git(self.writer, "switch", "-c", "direct", self.base)
        self.git(self.writer, "merge", "--squash", "pr71")
        self.git(self.writer, "commit", "-m", "unregistered direct commit")
        direct = self.git(self.writer, "rev-parse", "HEAD")
        self.assertEqual(self.base, self.git(self.writer, "show", "-s", "--format=%P", direct))
        self.assertEqual(self.git(self.writer, "rev-parse", entry["head_sha"] + "^{tree}"), self.git(self.writer, "rev-parse", direct + "^{tree}"))
        request = copy.deepcopy(self.request)
        request["chain"] = [dict(request["chain"][0], merge_sha=direct)]
        request["target_sha"] = direct
        with self.assertRaisesRegex(ValueError, "PR identity mismatch"):
            c.propose_master_recovery(self.control, request)
        self.assertEqual([], self.writes)

    def test_original_two_parent_merge_strategy_remains_supported(self):
        self.git(self.writer, "switch", "-c", "merge-strategy", self.base)
        self.git(self.writer, "merge", "--no-ff", "pr71", "-m", "two-parent merge")
        merge = self.git(self.writer, "rev-parse", "HEAD")
        entry = dict(copy.deepcopy(self.chain[0]), merge_sha=merge)
        parents = self.git(self.writer, "show", "-s", "--format=%P", merge).split()
        self.assertEqual([self.base, entry["head_sha"]], parents)
        self.api["pulls/71"]["merge_commit_sha"] = merge
        self.api["commits/" + merge] = dict(sha=merge, parents=[dict(sha=p) for p in parents])
        self.api["branches/master"] = dict(commit=dict(sha=merge))
        request = dict(version=1, local_sha=self.base, target_sha=merge, chain=[entry])
        records = c.recovery_chain_ok(self.control, request, dict(tasks=[]))
        self.assertEqual(merge, records[0]["merge_sha"])
        # Existing merge strategy still rejects the wrong second parent.
        self.api["commits/" + merge]["parents"][1]["sha"] = self.base
        with self.assertRaisesRegex(ValueError, "first-parent chain"):
            c.recovery_chain_ok(self.control, request, dict(tasks=[]))
        self.assertEqual([], self.writes)

    def test_dirty_diverged_wrong_branch_origin_block_before_writes(self):
        self.approve()
        for mode in ("dirty", "diverged", "branch", "origin"):
            with self.subTest(mode=mode):
                if mode == "dirty": (self.master / "stray.md").write_text("user change", encoding="utf-8")
                if mode == "diverged":
                    self.git(self.master, "commit", "--allow-empty", "-m", "local divergence")
                if mode == "branch": self.git(self.master, "switch", "-c", "protected")
                if mode == "origin": self.git(self.master, "remote", "set-url", "origin", "https://invalid.example/mirror.git")
                with self.assertRaises(ValueError): c.sync_master(self.control)
                self.assertEqual([], self.writes)
                if mode == "dirty": (self.master / "stray.md").unlink()  # fixture-owned only
                if mode == "diverged": self.git(self.master, "switch", "--detach", self.base); self.git(self.master, "branch", "-f", "master", self.base); self.git(self.master, "switch", "master")
                if mode == "branch": self.git(self.master, "switch", "master")
                if mode == "origin": self.git(self.master, "remote", "set-url", "origin", str(self.remote))

    def test_wrong_sha_base_head_merge_skip_stale_remote_fail_closed(self):
        for key in ("base_sha", "head_sha", "merge_sha", "pr_number", "skip", "remote", "parent", "unmerged", "repo", "base_ancestor"):
            request = copy.deepcopy(self.request); api = copy.deepcopy(self.api)
            if key in ("base_sha", "head_sha", "merge_sha"): request["chain"][0][key] = "f"*40
            elif key == "pr_number": request["chain"][0][key] = True
            elif key == "skip": request["chain"] = request["chain"][1:]
            elif key == "remote": api["branches/master"]["commit"]["sha"] = "f"*40
            elif key == "parent": api["commits/" + self.chain[1]["merge_sha"]]["parents"][0]["sha"] = self.base
            elif key == "unmerged": api["pulls/71"]["merged"] = False
            elif key == "repo": api["pulls/71"]["base"]["repo"]["full_name"] = "evil/mirror"
            elif key == "base_ancestor": api["compare/" + self.base + "..." + self.chain[0]["head_sha"]]["status"] = "diverged"
            with self.subTest(key=key), patch.object(c, "github", side_effect=lambda path: copy.deepcopy(api[path])):
                with self.assertRaises(ValueError): c.propose_master_recovery(self.control, request)
                self.assertEqual([], self.writes)

    def test_review_visual_ci_and_native_provenance_failures(self):
        for mode in ("missing", "comment", "wrong_head", "visual", "ci", "production_downgrade", "renamed", "truncated", "events", "thread"):
            request = copy.deepcopy(self.request); entry = request["chain"][0]
            path = self.control / entry["review"]["path"]; original = path.read_bytes()
            review = json.loads(original)
            if mode == "missing": path.unlink()
            elif mode == "comment": entry["review"]["path"] = "https://github.com/Bladgrif/How_I_Fall/issues/71#issuecomment-1"
            elif mode == "wrong_head": review["head_sha"] = "f"*40
            elif mode == "visual": entry["player_facing"] = True
            elif mode == "events": entry["review"]["events_sha256"] = "f"*64
            elif mode == "thread": entry["review"]["thread_id"] = "ffffffff-ffff-ffff-ffff-ffffffffffff"
            if mode == "wrong_head":
                c.save(path, review); entry["review"]["sha256"] = c.hashlib.sha256(path.read_bytes()).hexdigest()
            api = copy.deepcopy(self.api)
            compare = "compare/" + self.base + "..." + self.chain[0]["head_sha"]
            if mode == "production_downgrade": api[compare]["files"] = [dict(filename="Assets/UI.cs", status="modified")]
            if mode == "renamed": api[compare]["files"] = [dict(filename="docs/moved.cs", status="renamed", previous_filename="Assets/UI.cs")]
            if mode == "truncated": api[compare]["files"] = [dict(filename="docs/x", status="modified")] * 300
            ci = c.ci_checks(self.chain[0]["head_sha"])
            if mode == "ci": ci["check_runs"][0]["conclusion"] = "failure"
            with self.subTest(mode=mode), patch.object(c, "github", side_effect=lambda path: copy.deepcopy(api[path])), patch.object(c, "ci_checks", return_value=ci) if mode == "ci" else __import__("contextlib").nullcontext():
                with self.assertRaises((ValueError, FileNotFoundError)): c.propose_master_recovery(self.control, request)
                self.assertEqual([], self.writes)
            path.write_bytes(original)

    def test_approval_required_sealed_exact_request_and_fresh(self):
        self.approve()
        original = c.load(self.control / "queue.json")
        for mode in ("missing", "fake", "general", "sha", "order", "target", "digest", "expired", "future", "source"):
            q = copy.deepcopy(original); task = q["tasks"][0]
            if mode == "missing": task["recovery_approval"] = None
            elif mode == "fake": task["recovery_approval"]["seal"] = "fake"
            elif mode == "general": task["recovery_approval"] = dict(enabled=True, approved_source="coding-pass")
            elif mode == "sha": task["recovery_request"]["local_sha"] = "f"*40
            elif mode == "order": task["recovery_request"]["chain"].reverse()
            elif mode == "target": task["recovery_request"]["target_sha"] = "f"*40
            elif mode == "digest": task["recovery_digest"] = "f"*64
            else:
                approval = task["recovery_approval"]
                if mode == "expired": approval["approved_at"] = "2020-01-01T00:00:00+00:00"
                if mode == "future": approval["approved_at"] = "2099-01-01T00:00:00+00:00"
                if mode == "source": approval["source"] = "model"
                approval["seal"] = self.seal({k:v for k,v in approval.items() if k != "seal"})
            c.save(self.control / "queue.json", q)
            with self.subTest(mode=mode), self.assertRaises(ValueError): c.sync_master(self.control)
            self.assertEqual([], self.writes)
        c.save(self.control / "queue.json", original)

    def test_operator_approval_requires_native_origin_tty_both_exact_decisions(self):
        result = c.propose_master_recovery(self.control, self.request)
        with patch.object(c, "operator_origin", side_effect=ValueError("model")), self.assertRaises(ValueError):
            c.approve_master_recovery(self.control, result["task_id"], result["digest"], "operator-console:fixture")
        for tty, answer in ((False, ""), (True, "APPROVE " + result["digest"]), (True, "ATTEST NATIVE REVIEWS " + result["digest"])):
            with patch.object(c, "operator_origin"), patch.object(sys.stdin, "isatty", return_value=tty), patch.object(sys.stdout, "isatty", return_value=tty), patch("builtins.print"), self.assertRaises(ValueError):
                c.approve_master_recovery(self.control, result["task_id"], result["digest"], "operator-console:fixture", input_fn=lambda _: answer)
        self.assertEqual("WAIT_USER", c.load(self.control / "state.json")["status"])

    def test_stop_maintenance_active_conflict_stale_remote_and_state_block(self):
        self.approve()
        state = c.load(self.control / "state.json"); q = c.load(self.control / "queue.json")
        for mode in ("STOP", "MAINTENANCE", "PAUSED", "BLOCKED", "active", "competing", "remote"):
            if mode in ("STOP", "MAINTENANCE"): (self.control / mode).touch()
            elif mode in ("PAUSED", "BLOCKED"): c.save(self.control / "state.json", dict(state, status=mode))
            elif mode == "active": c.save(self.control / "state.json", dict(state, active_task_id="other"))
            elif mode == "competing": c.save(self.control / "queue.json", dict(tasks=q["tasks"] + [dict(id="other", status="READY")]))
            elif mode == "remote": self.api["branches/master"]["commit"]["sha"] = "f"*40
            with self.subTest(mode=mode), self.assertRaises(ValueError): c.sync_master(self.control)
            self.assertEqual([], self.writes)
            if mode in ("STOP", "MAINTENANCE"): (self.control / mode).unlink()
            c.save(self.control / "state.json", state); c.save(self.control / "queue.json", q)
            self.api["branches/master"]["commit"]["sha"] = self.request["target_sha"]

    def test_registered_identity_preserved_not_rewritten(self):
        entry = self.chain[0]
        registered = dict(id="manual-pr-71", status="DONE", **{k:entry[k] for k in ("pr_number", "base_sha", "head_sha", "merge_sha")})
        c.save(self.control / "queue.json", dict(tasks=[registered]))
        self.approve(); c.sync_master(self.control)
        q = c.load(self.control / "queue.json")
        self.assertEqual(registered, q["tasks"][0])
        self.assertEqual(registered["id"], q["tasks"][1]["reconciliations"][0]["registered_task_id"])

    def test_persistence_failure_after_git_sync_stops_and_safe_retry(self):
        self.approve(); original_save = c.save
        def partial(path, value):
            if Path(path).name == "state.json": raise OSError("fixture disk full")
            original_save(path, value)
        with patch.object(c, "save", side_effect=partial), self.assertRaises(c.FatalPersistence): c.sync_master(self.control)
        self.assertEqual(self.request["target_sha"], self.git(self.master, "rev-parse", "HEAD"))
        self.assertTrue((self.control / "STOP").exists())
        self.assertEqual("MASTER_RECOVERY_PENDING", c.load(self.control / "state.json")["status"])
        self.assertEqual("MERGED_LOCAL_SYNC_DONE", c.load(self.control / "queue.json")["tasks"][0]["status"])
        with self.assertRaises(ValueError): c.sync_master(self.control)
        (self.control / "STOP").unlink()  # Explicit fixture operator inspection/recovery.
        self.assertTrue(c.sync_master(self.control)["already_synced"])
        self.assertEqual(2, len(self.writes))

    def test_receipt_failure_after_git_sync_is_fatal(self):
        self.approve()
        with patch.object(c, "receipt_seal", side_effect=OSError("DPAPI unavailable")), self.assertRaises(c.FatalPersistence): c.sync_master(self.control)
        self.assertTrue((self.control / "STOP").exists())
        self.assertEqual("MASTER_RECOVERY_PENDING", c.load(self.control / "state.json")["status"])

    def test_post_fetch_drift_rechecked_before_merge(self):
        self.approve(); native = c.git_output.side_effect
        def drift(repo, *args):
            result = native(repo, *args)
            if args[0] == "fetch": (self.master / "stray.md").write_text("new user diff", encoding="utf-8")
            return result
        with patch.object(c, "git_output", side_effect=drift), self.assertRaises(ValueError): c.sync_master(self.control)
        self.assertEqual([("fetch", "origin", "master")], self.writes)
        self.assertEqual(self.base, self.git(self.master, "rev-parse", "HEAD"))

    def test_control_change_before_git_write_blocks(self):
        self.approve(); native = c.github.side_effect
        def drift(path):
            result = native(path)
            if path == "branches/master": c.save(self.control / "state.json", dict(status="STOP", active_task_id=None))
            return result
        with patch.object(c, "github", side_effect=drift), self.assertRaises(ValueError): c.sync_master(self.control)
        self.assertEqual([], self.writes)

    def test_mixed_native_utf16_diagnostics_preserve_authentic_events(self):
        for entry in self.chain:
            path = self.control / entry["review"]["events_path"]
            lines = path.read_text(encoding="utf-8")
            path.write_text("codex.exe : 2026-10-05 WARN codex_skills missing path\nAt D:\\How_I_Fall\\agent-control\\hif-reviewer.ps1:89 char:1\n+ CategoryInfo : NotSpecified\n" + lines, encoding="utf-16")
            entry["review"]["events_sha256"] = c.hashlib.sha256(path.read_bytes()).hexdigest()
        self.approve(); self.assertEqual("MASTER_SYNCED", c.sync_master(self.control)["status"])

    def test_prefetch_remote_lookup_stop_or_control_drift_blocks_zero_writes(self):
        self.approve(); state = c.load(self.control / "state.json"); native = c.github.side_effect
        for mode in ("STOP", "state"):
            count = [0]
            def drift(path):
                result = native(path)
                if path == "branches/master":
                    count[0] += 1
                    if count[0] == 2:
                        if mode == "STOP": (self.control / "STOP").touch()
                        else: c.save(self.control / "state.json", dict(state, status="STOP"))
                return result
            with self.subTest(mode=mode), patch.object(c, "github", side_effect=drift), self.assertRaises(ValueError): c.sync_master(self.control)
            self.assertEqual([], self.writes)
            if mode == "STOP": (self.control / "STOP").unlink()
            c.save(self.control / "state.json", state)

    def test_final_operator_revalidation_stop_cannot_record_approval(self):
        result = c.propose_master_recovery(self.control, self.request); native = c.github.side_effect; count = [0]
        def drift(path):
            output = native(path)
            if path == "branches/master":
                count[0] += 1
                if count[0] == 2: (self.control / "STOP").touch()
            return output
        with patch.object(c, "github", side_effect=drift), patch.object(c, "operator_origin"), patch.object(sys.stdin, "isatty", return_value=True), patch.object(sys.stdout, "isatty", return_value=True), patch("builtins.print"), self.assertRaises(ValueError):
            c.approve_master_recovery(self.control, result["task_id"], result["digest"], "operator-console:fixture", input_fn=lambda prompt: ("ATTEST NATIVE REVIEWS " if "ATTEST NATIVE" in prompt else "APPROVE RECOVERY ") + result["digest"])
        self.assertIsNone(c.load(self.control / "queue.json")["tasks"][0]["recovery_approval"])

    def test_approval_expires_during_network_before_fetch(self):
        self.approve(); native = c.github.side_effect
        q = c.load(self.control / "queue.json"); approval = q["tasks"][0]["recovery_approval"]
        initial = c.dt.datetime.now(c.dt.timezone.utc)
        approval["approved_at"] = (initial - c.dt.timedelta(seconds=86399)).isoformat()
        approval["seal"] = self.seal({k:v for k,v in approval.items() if k != "seal"}); c.save(self.control / "queue.json", q)
        class Clock(c.dt.datetime):
            value = initial
            @classmethod
            def now(cls, tz=None): return cls.value
        def drift(path):
            result = native(path)
            if path == "branches/master": Clock.value = initial + c.dt.timedelta(seconds=2)
            return result
        with patch.object(c, "github", side_effect=drift), patch.object(c.dt, "datetime", Clock), self.assertRaisesRegex(ValueError, "expired during"): c.sync_master(self.control)
        self.assertEqual([], self.writes)

    def test_done_without_sync_receipt_and_all_roadmap_entries_cannot_dispatch(self):
        self.approve(); c.sync_master(self.control)
        original = c.load(self.control / "queue.json")
        c.save(self.control / "state.json", dict(c.load(self.control / "state.json"), status="DONE"))
        content = self.git(self.master, "show", self.request["target_sha"] + ":docs/technical_plan.md")
        roadmap = dict(revision=self.request["target_sha"], sha256=c.hashlib.sha256(content.encode()).hexdigest(),
                       entries=[dict(pr_number=e["pr_number"], base_sha=e["base_sha"], head_sha=e["head_sha"], merge_sha=e["merge_sha"], status="RECONCILED") for e in self.chain])
        for mode in ("not_synced", "receipt", "request", "roadmap", "skipped_pr", "roadmap_hash"):
            q = copy.deepcopy(original); task = q["tasks"][0]; task.update(status="DONE", recovery_roadmap=copy.deepcopy(roadmap))
            if mode == "not_synced": task["local_sync_done"] = False
            if mode == "receipt": task["local_sync_receipt"] = "counterfeit"
            if mode == "request": task["recovery_request"]["local_sha"] = "f"*40
            if mode == "roadmap": task.pop("recovery_roadmap")
            if mode == "skipped_pr": task["recovery_roadmap"]["entries"].pop()
            if mode == "roadmap_hash": task["recovery_roadmap"]["sha256"] = "f"*64
            c.save(self.control / "queue.json", q)
            with self.subTest(mode=mode), patch.object(c, "fresh_quota") as quota, self.assertRaises(ValueError): c.batch_tick(self.control)
            quota.assert_not_called()
        q = copy.deepcopy(original); q["tasks"][0].update(status="DONE", recovery_roadmap=roadmap)
        c.save(self.control / "queue.json", q)
        self.assertEqual("PLANNING_ONCE", c.batch_tick(self.control)["status"])

    def test_inactive_recovery_labels_cannot_discard_sync_and_roadmap_obligations(self):
        import test_batch
        self.approve(); c.sync_master(self.control)
        original = c.batch.propose(c.load(self.control / "queue.json"), test_batch.packet(), self.verify)
        record = original["batches"][0]
        approval = dict(digest=record["digest"], operator="fixture", source="operator-console:fixture", approved_at=c.now())
        record.update(status="APPROVED", approval=dict(approval, seal=self.seal(approval)))
        content = self.git(self.master, "show", self.request["target_sha"] + ":docs/technical_plan.md")
        roadmap = dict(revision=self.request["target_sha"], sha256=c.hashlib.sha256(content.encode()).hexdigest(),
                       entries=[dict(pr_number=e["pr_number"], base_sha=e["base_sha"], head_sha=e["head_sha"], merge_sha=e["merge_sha"], status="RECONCILED") for e in self.chain])
        state = dict(status="DONE", active_task_id=None)
        limits = dict(updated_at=c.now(), source="account/rateLimits/read", mode="NORMAL", primary_remaining_percent=90, weekly_remaining_percent=90)
        c.save(self.control / "state.json", state)
        for status in ("CANCELLED", "WAIT_USER", "LIST_APPROVED_NOT_DISPATCHED"):
            for missing in ("roadmap", "receipt", "sync"):
                q = copy.deepcopy(original); task = q["tasks"][0]
                task.update(status=status, recovery_roadmap=copy.deepcopy(roadmap))
                if missing == "roadmap": task.pop("recovery_roadmap")
                elif missing == "receipt": task.pop("local_sync_receipt")
                else: task.pop("local_sync_done")
                c.save(self.control / "queue.json", q)
                with self.subTest(status=status, missing=missing), patch.object(c, "fresh_quota", return_value=limits) as quota, self.assertRaises(ValueError):
                    c.batch_tick(self.control)
                quota.assert_not_called()
        q = copy.deepcopy(original); q["tasks"][0].update(status="DONE", recovery_roadmap=roadmap)
        c.save(self.control / "queue.json", q)
        with patch.object(c, "fresh_quota", return_value=limits):
            self.assertEqual("READY", c.batch_tick(self.control)["status"])
        self.assertEqual("first", c.load(self.control / "state.json")["active_task_id"])

    def test_partial_recovery_marker_removal_cannot_hide_imported_history(self):
        self.approve(); c.sync_master(self.control)
        original = c.load(self.control / "queue.json")
        c.save(self.control / "state.json", dict(status="DONE", active_task_id=None))
        for fields in (("origin",), ("recovery_request",), ("origin", "recovery_request"),
                       ("origin", "recovery_request", "recovery_digest", "recovery_approval", "reconciliations")):
            q = copy.deepcopy(original); task = q["tasks"][0]; task["status"] = "CANCELLED"
            for field in fields: task.pop(field)
            c.save(self.control / "queue.json", q)
            with self.subTest(fields=fields), patch.object(c, "fresh_quota") as quota, self.assertRaises(ValueError):
                c.batch_tick(self.control)
            quota.assert_not_called()

    def test_proposed_and_pending_recovery_keep_existing_wait_and_serial_workflow(self):
        c.propose_master_recovery(self.control, self.request)
        self.assertEqual("WAIT", c.batch_tick(self.control)["status"])
        # Use the existing proposal for a native fixture approval, not a new proposal.
        q = c.load(self.control / "queue.json"); task = q["tasks"][0]
        with patch.object(c, "operator_origin"), patch.object(sys.stdin, "isatty", return_value=True), patch.object(sys.stdout, "isatty", return_value=True), patch("builtins.print"):
            c.approve_master_recovery(self.control, task["id"], task["recovery_digest"], "operator-console:fixture", input_fn=lambda prompt: ("ATTEST NATIVE REVIEWS " if "ATTEST NATIVE" in prompt else "APPROVE RECOVERY ") + task["recovery_digest"])
        self.assertEqual("ACTIVE", c.batch_tick(self.control)["status"])
        c.sync_master(self.control)
        self.assertEqual("ACTIVE", c.batch_tick(self.control)["status"])

    def test_recovery_file_coverage_rejects_malformed_paths_and_empty_diff(self):
        compare = "compare/" + self.base + "..." + self.chain[0]["head_sha"]
        cases = [[], [{}], [dict(filename="")], [None], [dict(filename=42, status="modified")],
                 [dict(filename="docs/x", status="unknown")], [dict(filename="docs/x")],
                 [dict(filename="docs/x", status="renamed")],
                 [dict(filename="docs/x", status="copied")],
                 [dict(filename="docs/x", status="renamed", previous_filename="")],
                 [dict(filename="docs/x", status="renamed", previous_filename=[])]]
        for path in ("/Assets/x", "../Assets/x", "docs/../Assets/x", "docs//x", "./Assets/x", "C:/Assets/x", "Assets\\x", "docs/x\n"):
            cases.extend(([dict(filename=path, status="modified")], [dict(filename="docs/x", status="renamed", previous_filename=path)]))
        for files in cases:
            api = copy.deepcopy(self.api); api[compare]["files"] = files
            with self.subTest(files=files), patch.object(c, "github", side_effect=lambda path: copy.deepcopy(api[path])):
                with self.assertRaises(ValueError): c.recovery_chain_ok(self.control, self.request, dict(tasks=[]))
                self.assertEqual([], self.writes)
        for files in ([dict(filename="docs/genuine.md", status="modified")],
                      [dict(filename="docs/new name.md", status="renamed", previous_filename="docs/old name.md")],
                      [dict(filename="docs/copied.md", status="copied", previous_filename="docs/original.md")]):
            api = copy.deepcopy(self.api); api[compare]["files"] = files
            with patch.object(c, "github", side_effect=lambda path: copy.deepcopy(api[path])):
                self.assertEqual(2, len(c.recovery_chain_ok(self.control, self.request, dict(tasks=[]))))

    def test_done_cannot_use_unmerged_roadmap_revision(self):
        self.approve(); c.sync_master(self.control)
        self.git(self.master, "switch", "-c", "unmerged-roadmap")
        (self.master / "docs/technical_plan.md").write_text("unmerged roadmap", encoding="utf-8")
        self.git(self.master, "add", "docs/technical_plan.md"); self.git(self.master, "commit", "-m", "unmerged roadmap")
        revision = self.git(self.master, "rev-parse", "HEAD"); self.git(self.master, "switch", "master")
        q = c.load(self.control / "queue.json"); task = q["tasks"][0]; task["status"] = "DONE"
        task["recovery_roadmap"] = dict(revision=revision, sha256=c.hashlib.sha256(b"unmerged roadmap").hexdigest(),
            entries=[dict(pr_number=e["pr_number"], base_sha=e["base_sha"], head_sha=e["head_sha"], merge_sha=e["merge_sha"], status="RECONCILED") for e in self.chain])
        c.save(self.control / "queue.json", q); c.save(self.control / "state.json", dict(c.load(self.control / "state.json"), status="DONE"))
        with self.assertRaisesRegex(ValueError, "not reachable"): c.batch_tick(self.control)

    def test_persistence_marker_failure_still_fatal_after_actual_sync(self):
        self.approve(); native_write = Path.write_text
        def refuse_marker(path, *args, **kwargs):
            if path.name == "STOP": raise OSError("fixture marker unavailable")
            return native_write(path, *args, **kwargs)
        with patch.object(c, "save", side_effect=OSError("fixture disk unavailable")), patch.object(Path, "write_text", refuse_marker), self.assertRaises(c.FatalPersistence): c.sync_master(self.control)
        self.assertEqual(self.request["target_sha"], self.git(self.master, "rev-parse", "HEAD"))
        self.assertEqual("MASTER_RECOVERY_PENDING", c.load(self.control / "state.json")["status"])


    def test_after_fetch_branch_origin_head_remote_drift_block_ff_only(self):
        self.approve(); native = c.git_output.side_effect
        for cmd, value in ((('branch', '--show-current'), 'other'), (('remote', 'get-url', 'origin'), 'evil'),
                           (('rev-parse', 'HEAD'), 'f'*40), (('rev-parse', 'FETCH_HEAD'), 'f'*40)):
            fetched = [False]
            def drift(repo, *args):
                if fetched[0] and args == cmd: return value
                result = native(repo, *args)
                if args[0] == "fetch": fetched[0] = True
                return result
            with self.subTest(cmd=cmd), patch.object(c, "git_output", side_effect=drift), self.assertRaises(ValueError): c.sync_master(self.control)
            self.assertEqual(('fetch', 'origin', 'master'), self.writes[-1])
            self.assertFalse(any(w[0] == 'merge' for w in self.writes))


    def test_scheduler_ancestry_model_and_manual_console_blocked(self):
        # Use actual guard rather than the transport fixture override.
        source = Path(__file__).with_name("hif-control.py").read_text(encoding="utf-8")
        namespace = dict(c.__dict__); exec(source[source.index("def recovery_scheduler_origin():"):source.index("def sync_master_recovery(control):")], namespace)
        guard = namespace["recovery_scheduler_origin"]
        with patch.dict(os.environ, {"CODEX_THREAD_ID": "fixture"}), self.assertRaises(ValueError): guard()
        for parent in ("powershell.exe console", "codex.exe -File D:\\How_I_Fall\\agent-control\\supervisor-loop.ps1",
                       "powershell.exe -File D:\\Temp\\supervisor-loop.ps1", "powershell.exe -File D:\\How_I_Fall\\agent-control\\not-supervisor-loop.ps1",
                       'powershell.exe -Command "-File D:\\How_I_Fall\\agent-control\\supervisor-loop.ps1"',
                       "powershell.exe powershell.exe -File D:\\How_I_Fall\\agent-control\\supervisor-loop.ps1"):
            result = type("Result", (), dict(returncode=0, stdout=json.dumps(["python fixture", parent])))()
            with patch.dict(os.environ, {}, clear=True), patch.object(c.subprocess, "run", return_value=result):
                if parent.startswith("powershell.exe powershell.exe -File"): guard()
                else:
                    with self.assertRaises(ValueError): guard()


if __name__ == "__main__":
    unittest.main()
