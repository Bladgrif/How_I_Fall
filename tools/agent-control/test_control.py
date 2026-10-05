"""Regression tests: no models, filesystem cleanup, GitHub writes or production work."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("control", Path(__file__).with_name("hif-control.py"))
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)


class ControlTests(unittest.TestCase):
    def setUp(self):
        self.task = {"id": "test", "base_sha": "a"*40, "head_sha": "b"*40}
        self.review = dict(task_id="test", base_sha="a"*40, head_sha="b"*40,
                           verdict="CLEAN", findings=[], validation_gaps=[], escalate=False,
                           reviewer_model="gpt-6-luna", reviewer_reasoning="low")
        self.strong = dict(self.review, reviewer_model="gpt-6.1-sol", reviewer_reasoning="high")
        self.pr = dict(number=1, state="open", draft=False, mergeable=True,
                       head={"sha": "b"*40}, base={"ref": "master", "sha": "a"*40})
        self.checks = {"check_runs": [dict(id=1, name="CI Gate", head_sha="b"*40,
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

    def test_strong_wrong_model_rejected(self):
        self.strong["reviewer_model"] = "GLM-5.3-Flash"
        with self.assertRaises(ValueError):
            self.gate(["Assets/X.cs"])

    def test_stale_review_rejected(self):
        self.review["head_sha"] = "c"*40
        with self.assertRaises(ValueError):
            self.gate()

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

    def test_master_head_draft_mergeability_changes_rejected(self):
        for key, value in [("base", {"ref":"master", "sha":"c"*40}), ("head", {"sha":"c"*40}), ("draft",True), ("mergeable",None)]:
            pr = dict(self.pr, **{key:value})
            with self.assertRaises(ValueError):
                c.gate(self.task, self.review, self.strong, pr, self.checks, [])

    def test_player_facing_requires_inspected_proof(self):
        self.task["player_facing"] = True
        with self.assertRaises(ValueError):
            self.gate(["Assets/X.cs"])
        self.review["visual_proof_verified"] = True
        self.assertEqual("MERGE_ALLOWED", self.gate(["Assets/X.cs"])["status"])

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


if __name__ == "__main__":
    unittest.main()
