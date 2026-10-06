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
            script.write_text(text, encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text, encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text, encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text,encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(text, encoding="utf-8-sig")
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
            (root/"loop.ps1").write_text(source, encoding="utf-8-sig")
            (root/"controller.json").write_text(json.dumps({"enabled":True}), encoding="utf-8")
            (root/"state.json").write_text(json.dumps({"status":"WAIT_AUTH", "active_task_id":"preserve"}), encoding="utf-8")
            (root/"wake-supervisor.ps1").write_text("throw 'Unexpected auth-wait wake'", encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",str(root/"loop.ps1"),"-Once"], capture_output=True, timeout=15)
            self.assertEqual(0,result.returncode,result.stderr.decode(errors="replace"))
            self.assertFalse((root/"supervisor-loop.log").exists())
            self.assertEqual("WAIT_AUTH",c.load(root/"state.json")["status"])


if __name__ == "__main__":
    unittest.main()
