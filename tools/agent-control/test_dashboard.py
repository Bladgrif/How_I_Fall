"""Regression tests: только stdlib, моки и временные каталоги.

Никакие реальные CLI/провайдеры/PowerShell/runtime-контур/Unity/каталоги
пользователя не вызываются: файлы читаются из temp, процессы и квота —
внедряемые фейки, HTTP поднимается на изолированном ephemeral localhost.
Полный прогон и логи выполняет root host; сам writer тесты НЕ запускает.
"""
import importlib.util
import json
import os
import subprocess
from pathlib import Path
import threading
import time
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request

spec = importlib.util.spec_from_file_location("dashboard", Path(__file__).with_name("hif-dashboard.py"))
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)

BASE_SHA = "a" * 40
HEAD_SHA = "b" * 40


def write_json(path, value, bom=False):
    text = json.dumps(value, ensure_ascii=False)
    Path(path).write_text(("\ufeff" if bom else "") + text, encoding="utf-8")


def quota_value(mode="NORMAL"):
    return {"updated_at": "2026-10-06T12:00:00+00:00", "source": "account/rateLimits/read",
            "mode": mode, "threshold_remaining_percent": 25,
            "primary_remaining_percent": 80, "weekly_remaining_percent": 90,
            "windows": [{"usedPercent": 20, "windowDurationMins": 300, "resetsAt": 1791296196},
                        {"usedPercent": 10, "windowDurationMins": 10080, "resetsAt": 1791882996}]}


def prow(pid, role, engine=None, created_at="2026-10-06T12:00:00+03:00"):
    return {"pid": pid, "role": role, "engine": engine, "created_at": created_at}


def default_processes():
    return [prow(50, "scheduler"), prow(100, "writer_codex", "Codex"),
            prow(200, "glm_support", "GLM", "2026-10-06T15:29:57+03:00"), prow(300, "reviewer_strong", "Codex")]


def build_control_dir(base, *, with_state=True, with_receipt=True):
    control = Path(base) / "agent-control"
    (control / "logs").mkdir(parents=True)
    (control / "evidence").mkdir(parents=True)
    write_json(control / "controller.json",
               {"version": 2, "repo": "Bladgrif/How_I_Fall", "enabled": True,
                "scheduler_mode": "one-outer-dispatch-15-minute", "native_master_sync_enabled": True})
    events = control / "logs" / "run-events.jsonl"
    events.write_text(
        '{"type":"thread.started","thread_id":"t"}\n'
        '{"type":"item.started","item":{"id":"i0","type":"command_execution",'
        '"command":"SECRET-COMMAND","status":"in_progress"}}\n'
        '{"type":"item.completed","item":{"id":"i0","type":"command_execution",'
        '"command":"SECRET-COMMAND","aggregated_output":"SECRET-OUTPUT","status":"completed"}}\n'
        '{"type":"item.completed","item":{"id":"i1","type":"file_change",'
        '"changes":[{"path":"D:/proj/Assets/Alpha.cs"}]}}\n'
        '{"type":"turn.completed","usage":{"input_tokens":10,"output_tokens":2}}\n',
        encoding="utf-8")
    write_json(control / "codex-quota.json", quota_value())
    queue = {"version": 2, "updated_at": "2026-10-06T12:00:00+03:00", "tasks": [
        {"id": "task-1", "title": "Активная задача", "status": "REVIEW_CANDIDATE",
         "writer_engine": "Codex", "model": "gpt-6.1-sol", "branch": "codex/task-1",
         "pr_number": 67, "head_sha": HEAD_SHA, "base_sha": BASE_SHA, "risk": "high",
         "player_facing": False, "prompt": "SECRET-PROMPT", "approved_source": "SECRET-SOURCE",
         "validation": "SECRET-VALIDATION", "correction": "SECRET-CORRECTION"},
        {"id": "task-2", "title": "Готовая задача", "status": "DONE", "writer_engine": "Codex",
         "pr_number": 66, "head_sha": "c" * 40, "base_sha": BASE_SHA, "risk": "low",
         "player_facing": False},
        {"id": "task-3", "title": "Ожидает пользователя", "status": "WAIT_USER",
         "writer_engine": "ZCode", "model": "GLM-5.3-Flash", "branch": "codex/task-3",
         "head_sha": "d" * 40, "base_sha": BASE_SHA, "risk": "low", "player_facing": False},
    ]}
    write_json(control / "queue.json", queue, bom=True)
    if with_state:
        state = {"base_sha": BASE_SHA, "status": "REVIEW_CANDIDATE", "active_task_id": "task-1",
                 "next_action": "Ждёт независимого ревью exact-head.", "version": 2,
                 "branch": "codex/task-1", "events_path": str(events),
                 "report_path": str(control / "logs" / "run-report.json"),
                 "last_error": None, "head_sha": HEAD_SHA,
                 "updated_at": "2026-10-06T12:00:00+03:00", "transport_failures": 0}
        write_json(control / "state.json", state, bom=True)
    if with_receipt:
        receipt_events = control / "evidence" / "dashboard-glm-events.jsonl"
        receipt_events.write_text("diag line\n" + json.dumps({
            "sessionId": "sess", "response": "SECRET-RESPONSE",
            "usage": {"source": "provider", "modelRequestCount": 2, "inputTokens": 100,
                      "outputTokens": 20, "totalTokens": 120, "cacheReadTokens": 50}},
            ensure_ascii=False), encoding="utf-8")
        write_json(control / "evidence" / "dashboard-glm-run.json", {
            "task_id": "support-1", "title": "Локальный экран поддержки", "engine": "ZCode",
            "model": "GLM-5.3-Flash", "status": "SUPPORT_RUNNING",
            "started_at": "2026-10-06T15:29:56+03:00", "base_sha": BASE_SHA,
            "branch": "codex/support", "writer_path": "D:\\How_I_Fall\\zagent",
            "events_path": str(receipt_events),
            "report_path": str(control / "evidence" / "dashboard-glm-report.json"),
            "pid": 200, "note": "support, not a second queue"})
    return control


class QuotaRpc:
    def __init__(self, gate=None, error=None):
        self.gate = gate
        self.error = error
        self.calls = 0
        self.lock = threading.Lock()

    def __call__(self):
        with self.lock:
            self.calls += 1
        if self.gate is not None:
            if not self.gate.wait(timeout=5):
                raise RuntimeError("test gate timeout")
        if self.error is not None:
            raise self.error
        return quota_value()


def make_dashboard(control, *, process_probe=None, quota_rpc=None, now_fn=time.time,
                   inline_probes=True, html_path=None):
    return d.Dashboard(control_dir=control, process_probe=process_probe or default_processes,
                       quota_rpc=quota_rpc or (lambda: quota_value()), now_fn=now_fn,
                       inline_probes=inline_probes, html_path=html_path or Path(control) / "unused.html")


class BaseControlTest(unittest.TestCase):
    def setUp(self):
        import tempfile
        self._tmp = tempfile.TemporaryDirectory()
        self.base = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)


class LoadJsonTests(BaseControlTest):
    def test_bom_and_plain_ok(self):
        plain = self.base / "plain.json"
        bom = self.base / "bom.json"
        write_json(plain, {"x": 1})
        write_json(bom, {"x": 2}, bom=True)
        value, error = d.load_json_file(plain)
        self.assertIsNone(error)
        self.assertEqual({"x": 1}, value)
        value, error = d.load_json_file(bom)
        self.assertIsNone(error)
        self.assertEqual({"x": 2}, value)

    def test_missing_explicit_error(self):
        value, error = d.load_json_file(self.base / "absent.json")
        self.assertIsNone(value)
        self.assertIn("отсутствует", error)

    def test_malformed_explicit_error(self):
        path = self.base / "broken.json"
        path.write_text('{"a": 1,,}', encoding="utf-8")
        value, error = d.load_json_file(path)
        self.assertIsNone(value)
        self.assertIn("некорректный JSON", error)

    def test_non_object_rejected(self):
        path = self.base / "array.json"
        path.write_text("[1, 2]", encoding="utf-8")
        value, error = d.load_json_file(path)
        self.assertIsNone(value)
        self.assertIn("не является объектом", error)

    def test_directory_read_error_not_exception(self):
        value, error = d.load_json_file(self.base)
        self.assertIsNone(value)
        self.assertIsNotNone(error)


class SelectProcessesTests(unittest.TestCase):
    def test_script_roles(self):
        rows = [{"pid": 1, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\supervisor-loop.ps1"},
                {"pid": 2, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\wake-supervisor.ps1"},
                {"pid": 3, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-worker.ps1 -Engine Codex"},
                {"pid": 4, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-worker.ps1 -Engine ZCode"},
                {"pid": 5, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-reviewer.ps1"},
                {"pid": 6, "ppid": 0, "name": "powershell.exe", "created": "o", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-reviewer.ps1 -Strong"}]
        selected = {item["pid"]: item["role"] for item in d.select_hif_processes(rows)}
        self.assertEqual({1: "scheduler", 2: "wake", 3: "writer_codex", 4: "writer_glm",
                          5: "reviewer", 6: "reviewer_strong"}, selected)

    def test_codex_ancestry_and_zcode_rules(self):
        rows = [{"pid": 10, "ppid": 0, "name": "powershell.exe", "created": None,
                 "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-worker.ps1 -Engine Codex"},
                {"pid": 11, "ppid": 10, "name": "codex.exe", "created": "o", "cmd": None},
                {"pid": 20, "ppid": 0, "name": "codex.exe", "created": "o", "cmd": None},
                {"pid": 30, "ppid": 0, "name": "node.exe", "created": "o",
                 "cmd": "node zcode.cjs --cwd D:\\How_I_Fall\\agent --mode edit"},
                {"pid": 31, "ppid": 0, "name": "node.exe", "created": "o",
                 "cmd": "node zcode.cjs --cwd D:/How_I_Fall/zagent --mode edit"},
                {"pid": 32, "ppid": 0, "name": "node.exe", "created": "o",
                 "cmd": "node zcode.cjs --cwd D:\\How_I_Fall\\agent-control"},
                {"pid": 33, "ppid": 0, "name": "node.exe", "created": "o",
                 "cmd": "node server.cjs --cwd D:\\How_I_Fall\\zagent"}]
        selected = {item["pid"]: item["role"] for item in d.select_hif_processes(rows)}
        self.assertEqual({10: "writer_codex", 11: "writer_codex", 30: "writer_glm",
                          31: "glm_support"}, selected)

    def test_safe_fields_only(self):
        rows = [{"pid": 10, "ppid": 0, "name": "powershell.exe", "created": "o",
                 "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\hif-worker.ps1 -Engine Codex -TopSecretFlag"}]
        for item in d.select_hif_processes(rows):
            self.assertEqual(set(item), {"pid", "role", "engine", "created_at"})
            self.assertNotIn("TopSecretFlag", json.dumps(item))

    def test_unrelated_script_names_are_not_hif_agents(self):
        rows = [{"pid": 1, "name": "powershell.exe", "cmd": "powershell -File D:\\other\\hif-worker.ps1"},
                {"pid": 2, "name": "powershell.exe", "cmd": "powershell -Command Write-Output '-File D:\\How_I_Fall\\agent-control\\hif-worker.ps1'"},
                {"pid": 3, "name": "node.exe", "cmd": "node hif-worker.ps1"}]
        self.assertEqual([], d.select_hif_processes(rows))

    def test_supervisor_codex_child_is_not_sol_writer(self):
        rows = [{"pid": 1, "ppid": 0, "name": "powershell.exe", "cmd": "powershell -File D:\\How_I_Fall\\agent-control\\wake-supervisor.ps1"},
                {"pid": 2, "ppid": 1, "name": "codex.exe", "cmd": "codex exec"}]
        roles = {row["pid"]: row["role"] for row in d.select_hif_processes(rows)}
        self.assertEqual({1: "wake", 2: "wake"}, roles)

    def test_none_passthrough(self):
        self.assertIsNone(d.select_hif_processes(None))


class AgentActivityRegressionTests(BaseControlTest):
    def test_production_zcode_is_not_idle(self):
        control = build_control_dir(self.base)
        dash = make_dashboard(control)
        tasks = [{"id": "z", "is_active": True, "group": "running", "engine": "ZCode"}]
        info = dash.glm_idle_reasons(tasks, {}, False, True)
        self.assertTrue(info["production_running"])
        self.assertEqual([], info["reasons"])
        waiting = dash.glm_idle_reasons(tasks, {}, False, False)
        self.assertIn("назначена", waiting["reasons"][0])
        self.assertNotIn("не назначена", waiting["reasons"][0])

    def test_supervisor_activity_differs_from_timer_only(self):
        control = build_control_dir(self.base)
        dash = make_dashboard(control)
        support = {"status": "absent"}
        idle = dash.agents_snapshot({"value": [prow(1, "scheduler")], "transport": "ok"}, support)
        active = dash.agents_snapshot({"value": [prow(1, "scheduler"), prow(2, "wake")], "transport": "ok"}, support)
        a = {c["id"]: c for c in idle["cards"]}
        b = {c["id"]: c for c in active["cards"]}
        self.assertEqual("stopped", a["supervisor"]["status"])
        self.assertEqual("running", b["supervisor"]["status"])
        self.assertEqual(a["scheduler"]["status"], b["scheduler"]["status"])

class TaskGroupTests(unittest.TestCase):
    def test_group_mapping(self):
        expected = {"DONE": "done", "MERGED_LOCAL_SYNC_DONE": "done", "ROADMAP_SYNC_READY": "done",
                    "REVIEW_CANDIDATE": "review", "WAIT_STRONG_REVIEW": "review", "WAIT_CI": "review",
                    "NEEDS_CORRECTION": "error", "BLOCKED": "error",
                    "SYNC_MASTER_PENDING": "running", "RUNNING": "running",
                    "WAIT_USER": "waiting", "WAIT_AUTH": "waiting", None: "unknown"}
        for status, group in expected.items():
            self.assertEqual(group, d.task_group(status), status)


class SnapshotTests(BaseControlTest):
    def snapshot(self, **kwargs):
        control = build_control_dir(self.base, **kwargs.pop("build", {}))
        return make_dashboard(control, **kwargs).snapshot(), control

    def test_full_snapshot_and_sanitization(self):
        snap, control = self.snapshot()
        dump = json.dumps(snap, ensure_ascii=False)
        for banned in ("SECRET-PROMPT", "SECRET-SOURCE", "SECRET-VALIDATION",
                       "SECRET-CORRECTION", "SECRET-COMMAND", "SECRET-OUTPUT",
                       "SECRET-RESPONSE", '"prompt"', "approved_source",
                       "integration_instruction", "CommandLine", '"cmd"',
                       "authorization", "sessionId", "traceId"):
            self.assertNotIn(banned, dump)
        self.assertEqual(3, snap["queue"]["task_count"])
        self.assertEqual({"running": 0, "review": 1, "waiting": 1, "error": 0, "done": 1, "unknown": 0},
                         snap["groups"])
        active = snap["tasks"][0]
        self.assertEqual("task-1", active["id"])
        self.assertTrue(active["is_active"])
        self.assertEqual("Ждёт независимого ревью exact-head.", active["blocker"])
        self.assertEqual(10, len(active["head_sha"]))
        self.assertEqual("running", snap["support"]["status"])
        self.assertTrue(snap["support"]["pid_alive"])
        self.assertEqual("ok", snap["agents"]["transport"])
        cards = {card["id"]: card for card in snap["agents"]["cards"]}
        self.assertEqual("running", cards["sol_writer"]["status"])
        self.assertEqual("running", cards["glm"]["status"])
        self.assertEqual(120, snap["activity"]["glm_usage"]["usage"]["totalTokens"])
        self.assertEqual("Шаг завершён", snap["activity"]["task_events"]["last_step"]["label"])
        self.assertEqual([], snap["glm_idle"]["reasons"])
        self.assertEqual("live", snap["quota_gpt"]["source"])
        self.assertEqual("ok", snap["quota_gpt"]["level"])
        self.assertFalse(snap["quota_glm"]["available"])
        self.assertNotIn("SECRET", json.dumps(snap["quota_gpt"], ensure_ascii=False))

    def test_malformed_queue_explicit_not_blank(self):
        snap, control = self.snapshot(build={"with_state": False})
        (control / "queue.json").write_text("{broken", encoding="utf-8")
        dashboard = make_dashboard(control)
        snap = dashboard.snapshot()
        self.assertEqual([], snap["tasks"])
        self.assertIn("некорректный JSON", snap["queue"]["error"])

    def test_no_state_still_infers_running_writer(self):
        snap, control = self.snapshot(build={"with_state": False})
        self.assertIsNone(snap["state"]["status"])
        self.assertIn("отсутствует", snap["state"]["error"])
        cards = {card["id"]: card for card in snap["agents"]["cards"]}
        self.assertEqual("running", cards["sol_writer"]["status"])

    def test_unknown_process_transport(self):
        snap, control = self.snapshot(process_probe=lambda: None)
        self.assertEqual("error", snap["agents"]["transport"])
        for card in snap["agents"]["cards"]:
            self.assertEqual("unknown", card["status"])
        self.assertEqual("unknown", snap["support"]["status"])

    def test_idle_reasons(self):
        snap, control = self.snapshot(build={"with_receipt": False})
        reasons = " ".join(snap["glm_idle"]["reasons"])
        self.assertIn("последовательна", reasons)
        (control / "state.json").write_text(
            "\ufeff" + json.dumps({"status": "WAIT_USER", "active_task_id": None},
                                  ensure_ascii=False), encoding="utf-8")
        snap = make_dashboard(control).snapshot()
        reasons = " ".join(snap["glm_idle"]["reasons"])
        self.assertIn("пользователя", reasons)
        (control / "queue.json").write_text(
            "\ufeff" + json.dumps({"version": 2, "tasks": [
                {"id": "done-1", "title": "x", "status": "DONE"}]}, ensure_ascii=False),
            encoding="utf-8")
        snap = make_dashboard(control).snapshot()
        self.assertTrue(any("Нет готовой утверждённой работы" in reason
                            for reason in snap["glm_idle"]["reasons"]))


class SupportReceiptTests(BaseControlTest):
    def support(self, processes):
        control = build_control_dir(self.base)
        return make_dashboard(control, process_probe=lambda: processes).support_snapshot(
            {"value": processes, "transport": "ok"})

    def test_running_with_relevant_alive_pid(self):
        support = self.support(default_processes())
        self.assertEqual("running", support["status"])
        self.assertTrue(support["pid_alive"])

    def test_stale_running_with_dead_pid_is_interrupted(self):
        support = self.support([prow(50, "scheduler"), prow(100, "writer_codex", "Codex")])
        self.assertEqual("interrupted", support["status"])
        self.assertIn("прервана", support["status_label"])

    def test_alive_but_irrelevant_pid_not_working(self):
        support = self.support([prow(50, "scheduler"), prow(200, "scheduler")])
        self.assertEqual("unknown", support["status"])
        self.assertNotEqual("running", support["status"])

    def test_reused_pid_is_not_old_support_run(self):
        support = self.support([prow(200, "glm_support", "GLM", "2026-10-07T15:29:57+03:00")])
        self.assertEqual("interrupted", support["status"])

    def test_missing_creation_time_is_unknown(self):
        support = self.support([prow(200, "glm_support", "GLM", None)])
        self.assertEqual("unknown", support["status"])

    def test_missing_receipt_absent(self):
        control = build_control_dir(self.base, with_receipt=False)
        support = make_dashboard(control).support_snapshot({"value": default_processes(), "transport": "ok"})
        self.assertEqual("absent", support["status"])
        self.assertFalse(support["receipt_present"])


class QuotaTests(BaseControlTest):
    def quota_dashboard(self, rpc, now_fn=time.time, inline=True):
        control = build_control_dir(self.base)
        return make_dashboard(control, quota_rpc=rpc, now_fn=now_fn, inline_probes=inline)

    def test_live_success_never_unknown(self):
        snap = self.quota_dashboard(QuotaRpc()).quota_snapshot()
        self.assertEqual("live", snap["source"])
        self.assertEqual("ok", snap["level"])
        self.assertTrue(snap["available"])
        labels = [window["label"] for window in snap["windows"]]
        self.assertEqual(["5-часовое окно", "Недельное окно"], labels)
        self.assertEqual(80.0, snap["windows"][0]["remaining_percent"])

    def test_failure_without_fallback_is_error_not_green(self):
        control = build_control_dir(self.base)
        (control / "codex-quota.json").unlink()
        snap = make_dashboard(control, quota_rpc=QuotaRpc(error=RuntimeError("Codex RPC timeout"))).quota_snapshot()
        self.assertEqual("error", snap["level"])
        self.assertFalse(snap["available"])
        self.assertIn("timeout", snap["error"])
        self.assertEqual([], snap["windows"])
        self.assertIn("fallback", snap["detail"])

    def test_fallback_stale_with_failed_fresh_read(self):
        control = build_control_dir(self.base)
        quota_path = control / "codex-quota.json"
        os.utime(quota_path, (time.time() - 600, time.time() - 600))
        snap = make_dashboard(control, quota_rpc=QuotaRpc(error=RuntimeError("rpc down"))).quota_snapshot()
        self.assertEqual("fallback", snap["source"])
        self.assertEqual("stale", snap["level"])
        self.assertNotEqual("ok", snap["level"])
        self.assertTrue(snap["available"])
        self.assertIn("rpc down", snap["error"])
        self.assertIsNotNone(snap["age_seconds"])

    def test_fallback_unknown_mode_is_not_green(self):
        control = build_control_dir(self.base)
        write_json(control / "codex-quota.json",
                   {"updated_at": "2026-10-06T12:24:07+00:00", "mode": "UNKNOWN",
                    "error": "Bundled Codex CLI not found", "threshold_remaining_percent": 25})
        snap = make_dashboard(control, quota_rpc=QuotaRpc(error=RuntimeError("rpc down"))).quota_snapshot()
        self.assertEqual("error", snap["level"])
        self.assertFalse(snap["available"])
        self.assertIn("CLI not found", snap["detail"])

    def test_quota_save_warns(self):
        control = build_control_dir(self.base)
        dash = make_dashboard(control, quota_rpc=lambda: quota_value(mode="QUOTA_SAVE"))
        snap = dash.quota_snapshot()
        self.assertEqual("warn", snap["level"])
        self.assertEqual("QUOTA_SAVE", snap["mode"])

    def test_throttle_min_interval(self):
        clock = {"t": 1000.0}
        rpc = QuotaRpc()
        dash = self.quota_dashboard(rpc, now_fn=lambda: clock["t"])
        snap = dash.quota_snapshot()
        self.assertEqual(1, rpc.calls)
        self.assertEqual("live", snap["source"])
        clock["t"] += 30
        snap = dash.quota_snapshot()
        self.assertEqual(1, rpc.calls)
        clock["t"] += 31
        snap = dash.quota_snapshot()
        self.assertEqual(2, rpc.calls)
        self.assertEqual("live", snap["source"])

    def test_cached_value_survives_failed_refresh(self):
        clock = {"t": 1000.0}
        calls = {"n": 0}

        def rpc():
            calls["n"] += 1
            if calls["n"] == 1:
                return quota_value()
            raise RuntimeError("rpc down")

        dash = self.quota_dashboard(rpc, now_fn=lambda: clock["t"])
        first = dash.quota_snapshot()
        self.assertEqual("ok", first["level"])
        clock["t"] += 120
        second = dash.quota_snapshot()
        self.assertEqual(2, calls["n"])
        self.assertTrue(second["available"])
        self.assertEqual("cache", second["source"])
        self.assertEqual("stale", second["level"])
        self.assertIn("rpc down", second["error"])
        self.assertGreater(second["age_seconds"], 60)

    def test_single_flight_no_parallel_refresh(self):
        clock = {"t": 1000.0}
        gate = threading.Event()
        rpc = QuotaRpc(gate=gate)
        dash = self.quota_dashboard(rpc, now_fn=lambda: clock["t"], inline=False)
        try:
            snap = dash.quota_snapshot()
            self.assertTrue(snap["refreshing"])
            self.assertEqual("fallback", snap["source"])
            clock["t"] += 61
            dash.quota_snapshot()
            self.assertEqual(1, rpc.calls)
            clock["t"] = 1000.5
            gate.set()
            deadline = time.time() + 5
            while time.time() < deadline:
                snap = dash.quota_snapshot()
                if snap["source"] in ("live", "cache"):
                    break
                time.sleep(0.05)
            self.assertIn(snap["source"], ("live", "cache"))
            self.assertEqual(1, rpc.calls)
        finally:
            gate.set()


def glm_quota_file_value(mode="NORMAL", used=(15.0, 5.0), resets_offset=(4000, 700000),
                         updated_age_seconds=1.0, updated_at=None):
    now = time.time()
    if updated_at is None:
        updated_at = d.dt.datetime.fromtimestamp(now - updated_age_seconds, d.dt.timezone.utc).isoformat()
    snapshot = {"version": 1, "updated_at": updated_at,
                "source": "https://api.z.ai/api/monitor/usage/quota/limit",
                "mode": mode, "threshold_remaining_percent": 25, "plan_level": "lite",
                "windows": [], "error": None}
    if mode != "UNKNOWN":
        snapshot["windows"] = [
            {"usedPercent": used[0], "windowDurationMins": 300,
             "resetsAt": now + resets_offset[0], "remainingCount": 1686},
            {"usedPercent": used[1], "windowDurationMins": 10080,
             "resetsAt": now + resets_offset[1], "remainingCount": 9486}]
    else:
        snapshot["error"] = "API квоты вернуло бизнес-ошибку"
    return snapshot


class GlmQuotaTests(BaseControlTest):
    def test_unknown_without_snapshot(self):
        control = build_control_dir(self.base)
        value = d.glm_quota_snapshot(control)
        self.assertFalse(value["available"])
        self.assertEqual("unknown", value["level"])
        self.assertEqual([], value["windows"])
        self.assertIn("неизвестен", value["detail"])
        self.assertIn("hif-glm-quota.py", value["detail"])
        self.assertIn("GLM-5.3-Flash", value["provider"])
        self.assertTrue(value["limitation"])

    def test_unknown_with_snapshot_error_is_error_not_green(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value(mode="UNKNOWN"))
        value = d.glm_quota_snapshot(control)
        self.assertFalse(value["available"])
        self.assertEqual("error", value["level"])
        self.assertIn("хелпера", value["detail"])

    def test_request_usage_is_not_subscription_balance(self):
        value = d.glm_quota_snapshot(self.base)
        self.assertIn("НЕ остаток", value["request_usage_note"])
        names = " ".join(source["name"] for source in value["checked_sources"])
        self.assertIn("usage/stats", names)
        self.assertIn("hif-glm-quota.py", names)

    def test_no_secret_material_in_block(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value())
        dump = json.dumps(d.glm_quota_snapshot(control), ensure_ascii=False).lower()
        for banned in ("enc:v1", "bearer ", "access_token", "secretkey", "credentials.json"):
            self.assertNotIn(banned, dump)

    def test_snapshot_with_fresh_file_shows_windows(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value())
        value = d.glm_quota_snapshot(control)
        self.assertTrue(value["available"])
        self.assertEqual("ok", value["level"])
        self.assertEqual("helper-file", value["source"])
        self.assertEqual("lite", value["plan_level"])
        self.assertEqual(["5-часовое окно", "Недельное окно"], [w["label"] for w in value["windows"]])
        self.assertEqual(85.0, value["windows"][0]["remaining_percent"])
        self.assertEqual(95.0, value["windows"][1]["remaining_percent"])
        self.assertIsNotNone(value["age_seconds"])

    def test_quota_save_warns_when_fresh(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value(mode="QUOTA_SAVE", used=(80.0, 5.0)))
        value = d.glm_quota_snapshot(control)
        self.assertEqual("warn", value["level"])
        self.assertEqual("QUOTA_SAVE", value["mode"])
        self.assertIn("QUOTA_SAVE", value["detail"])

    def test_old_updated_at_is_stale_even_with_fresh_mtime(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value(updated_age_seconds=1200))
        os.utime(control / "glm-quota.json", (time.time(), time.time()))
        value = d.glm_quota_snapshot(control)
        self.assertEqual("stale", value["level"])
        self.assertNotEqual("ok", value["level"])
        self.assertIn("исторические", value["detail"])
        self.assertIsNotNone(value["age_seconds"])
        self.assertGreater(value["age_seconds"], 900)

    def test_expired_quota_save_is_stale_not_warn(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json",
                   glm_quota_file_value(mode="QUOTA_SAVE", used=(80.0, 5.0), updated_age_seconds=1200))
        os.utime(control / "glm-quota.json", (time.time(), time.time()))
        value = d.glm_quota_snapshot(control)
        self.assertEqual("stale", value["level"])
        self.assertNotEqual("warn", value["level"])
        self.assertEqual("QUOTA_SAVE", value["mode"])
        self.assertIn("исторические", value["detail"])

    def test_missing_invalid_and_future_updated_at_are_not_ok(self):
        control = build_control_dir(self.base)
        for label, updated_at in (("missing", None), ("invalid", "не-время"),
                                  ("naive", "2026-10-07T13:13:34"), ("future", "2099-01-01T00:00:00+00:00")):
            with self.subTest(label=label):
                value = glm_quota_file_value()
                if updated_at is None:
                    value.pop("updated_at")
                else:
                    value["updated_at"] = updated_at
                write_json(control / "glm-quota.json", value)
                snapshot = d.glm_quota_snapshot(control)
                self.assertEqual("stale", snapshot["level"], label)
                self.assertIn("не актуален", snapshot["detail"], label)

    def test_rolled_reset_window_is_not_ok(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value(resets_offset=(-100, 700000)))
        value = d.glm_quota_snapshot(control)
        self.assertEqual("stale", value["level"])
        self.assertIn("сбросилось", value["detail"])

    def test_unreadable_snapshot_is_error(self):
        control = build_control_dir(self.base)
        (control / "glm-quota.json").write_text("{broken", encoding="utf-8")
        value = d.glm_quota_snapshot(control)
        self.assertFalse(value["available"])
        self.assertEqual("error", value["level"])
        self.assertIn("не читается", value["detail"])

    def test_snapshot_integration_and_usage_separate(self):
        control = build_control_dir(self.base)
        write_json(control / "glm-quota.json", glm_quota_file_value())
        # Freeze the clock: compare snapshots, not two different wall-clock ages.
        value = json.loads((control / "glm-quota.json").read_text(encoding="utf-8"))
        now = d._parse_snapshot_time(value["updated_at"]).timestamp() + 1.0
        clock = lambda: now
        snap = make_dashboard(control, now_fn=clock).snapshot()
        self.assertEqual(d.glm_quota_snapshot(control, clock), snap["quota_glm"])
        self.assertTrue(snap["quota_glm"]["available"])
        # Расход прошлого запуска живёт в activity и не подменяет остаток.
        self.assertEqual(120, snap["activity"]["glm_usage"]["usage"]["totalTokens"])

    def test_html_renders_windows_and_usage_disclaimer(self):
        html = Path(__file__).with_name("hif-dashboard.html").read_text(encoding="utf-8")
        self.assertIn("request_usage_note", html)
        self.assertIn("остаток неизвестен", html)
        self.assertIn("renderQuotaWindows(box, q.windows)", html)
        self.assertIn('"quotaGlmPanel", snapshot.quota_glm', html)


class GlmQuotaHelperTests(BaseControlTest):
    @classmethod
    def setUpClass(cls):
        helper_spec = importlib.util.spec_from_file_location(
            "hif_glm_quota", Path(__file__).with_name("hif-glm-quota.py"))
        cls.helper = importlib.util.module_from_spec(helper_spec)
        helper_spec.loader.exec_module(cls.helper)

    def test_describe_limit_known_plan_windows(self):
        self.assertEqual((300, "5-часовое окно"), self.helper.describe_limit({"number": 5, "unit": 3}))
        self.assertEqual((10080, "Недельное окно"), self.helper.describe_limit({"number": 1, "unit": 6}))
        minutes, label = self.helper.describe_limit({"number": 2, "unit": 99})
        self.assertIsNone(minutes)
        self.assertIn("unit=99", label)

    def test_quota_result_normal_and_sorted(self):
        body = {"code": 200, "data": {"level": "lite", "limits": [
            {"type": "CREDIT_LIMIT", "unit": 3, "number": 5, "usage": 2000, "currentValue": 295,
             "remaining": 1704, "percentage": 14, "nextResetTime": 1791394086904},
            {"type": "CREDIT_LIMIT", "unit": 6, "number": 1, "usage": 10000, "currentValue": 495,
             "remaining": 9504, "percentage": 4, "nextResetTime": 1791878591985}]}}
        value = self.helper.quota_result(body)
        self.assertEqual("NORMAL", value["mode"])
        self.assertEqual("lite", value["plan_level"])
        self.assertEqual([300, 10080], [w["windowDurationMins"] for w in value["windows"]])
        self.assertEqual(14.0, value["windows"][0]["usedPercent"])
        self.assertEqual(1704, value["windows"][0]["remainingCount"])
        self.assertEqual(1791394086.904, value["windows"][0]["resetsAt"])

    def test_quota_save_and_unknown(self):
        body = {"code": 0, "data": {"limits": [
            {"type": "CREDIT_LIMIT", "unit": 3, "number": 5, "percentage": 90,
             "nextResetTime": 1791394086904}]}}
        self.assertEqual("QUOTA_SAVE", self.helper.quota_result(body)["mode"])
        self.assertEqual("UNKNOWN", self.helper.quota_result({"code": 0, "data": {"limits": []}})["mode"])
        with self.assertRaises(self.helper.QuotaHelperError):
            self.helper.quota_result({"code": 401, "msg": "unauthorized"})

    def test_credential_blob_parsing_rejects_bad_shapes(self):
        for blob in ("plain", "enc:v1:a.b", "enc:v1:" + "AAAA." * 3, "enc:v1:AAAAAAAA.AAAA.AAAA"):
            with self.assertRaises(ValueError, msg=blob):
                self.helper.parse_credential_blob(blob)

    def test_credential_secret_prefers_env_and_fallback_shape(self):
        self.assertEqual("from-env", self.helper.credential_secret({"ZCODE_CREDENTIAL_SECRET": " from-env "}))
        fallback = self.helper.credential_secret({})
        self.assertTrue(fallback.startswith("zcode-credential-fallback:"))
        self.assertIn(str(Path.home()), fallback)

    def test_decrypt_failure_has_no_secret_in_message(self):
        node = self.helper.resolve_node()
        if node is None:
            self.fail("node unavailable in test environment")
        with self.assertRaises(ValueError) as caught:
            self.helper.decrypt_credential("enc:v1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.AAAA", node)
        self.assertNotIn("SECRET", str(caught.exception))

    def test_main_writes_snapshot_without_secret_output(self):
        import contextlib
        import io

        control = self.base / "control"
        control.mkdir()
        stdout = io.StringIO()

        def fake_load(path, node, env=None):
            return "SECRET-KEY"

        def fake_fetch(key, timeout=20.0):
            return {"code": 200, "data": {"level": "lite", "limits": [
                {"type": "CREDIT_LIMIT", "unit": 3, "number": 5, "percentage": 15,
                 "remaining": 1686, "nextResetTime": 1791394086904}]}}

        with patch.object(self.helper, "load_coding_plan_api_key", fake_load), \
                patch.object(self.helper, "fetch_quota", fake_fetch), \
                contextlib.redirect_stdout(stdout):
            code = self.helper.main(["--control-dir", str(control)])
        self.assertEqual(0, code)
        snapshot = json.loads((control / "glm-quota.json").read_text(encoding="utf-8"))
        self.assertEqual("NORMAL", snapshot["mode"])
        self.assertEqual(1, len(snapshot["windows"]))
        self.assertNotIn("SECRET-KEY", json.dumps(snapshot))
        self.assertNotIn("SECRET-KEY", stdout.getvalue())

    def test_fetch_quota_forbids_cross_host_redirect(self):
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

        seen = {"first": [], "second": []}

        def make_handler(record, redirect_to):
            class Handler(BaseHTTPRequestHandler):
                def do_GET(self):
                    record.append(self.headers.get("authorization"))
                    if redirect_to:
                        self.send_response(302)
                        self.send_header("Location", redirect_to)
                        self.send_header("Content-Length", "0")
                        self.end_headers()
                    else:
                        self.send_response(200)
                        self.send_header("Content-Length", "2")
                        self.end_headers()
                        self.wfile.write(b"{}")

                def log_message(self, *args):
                    pass
            return Handler

        second = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(seen["second"], None))
        first = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(
            seen["first"], "http://127.0.0.1:%d/hook" % second.server_address[1]))
        for server in (second, first):
            threading.Thread(target=server.serve_forever, daemon=True).start()

        def stop_servers():
            first.shutdown()
            second.shutdown()
            first.server_close()
            second.server_close()
        self.addCleanup(stop_servers)
        with patch.object(self.helper, "QUOTA_URL",
                          "http://127.0.0.1:%d/limits" % first.server_address[1]):
            with self.assertRaises(self.helper.QuotaHelperError) as caught:
                self.helper.fetch_quota("SECRET-KEY")
        self.assertEqual(302, caught.exception.code)
        self.assertIn("redirect", str(caught.exception))
        self.assertNotIn("SECRET-KEY", str(caught.exception))
        # Перенаправленный запрос не отправляется: второй host не получил ничего.
        self.assertEqual([], seen["second"])
        self.assertEqual(1, len(seen["first"]))

    def test_no_redirect_handler_never_follows_including_https_to_http(self):
        handler = self.helper.NoRedirectHandler()
        request = urllib.request.Request("https://api.z.ai/api/monitor/usage/quota/limit")
        for code in self.helper.REDIRECT_CODES:
            self.assertIsNone(handler.redirect_request(request, None, code, "x", {}, "http://127.0.0.1:9/hook"), code)

    def test_http_error_reason_is_not_passed_through(self):
        class FakeOpener:
            def open(self, request, timeout=20.0):
                raise urllib.error.HTTPError(request.full_url, 500, "SECRET-IN-REASON", {}, None)

        with patch.object(self.helper.urllib.request, "build_opener", return_value=FakeOpener()):
            with self.assertRaises(self.helper.QuotaHelperError) as caught:
                self.helper.fetch_quota("SECRET-KEY")
        self.assertEqual(500, caught.exception.code)
        self.assertNotIn("SECRET-IN-REASON", str(caught.exception))
        self.assertNotIn("SECRET-KEY", str(caught.exception))

    def test_business_error_message_is_sanitized(self):
        body = {"code": 403, "msg": "BAD key SECRET-KEY-VALUE rejected", "data": None}
        with self.assertRaises(self.helper.QuotaHelperError) as caught:
            self.helper.quota_result(body)
        self.assertEqual(403, caught.exception.code)
        self.assertNotIn("SECRET-KEY-VALUE", str(caught.exception))
        self.assertNotIn("rejected", str(caught.exception))

    def test_main_never_outputs_reflected_secret(self):
        import contextlib
        import io

        control = self.base / "control-secret"
        control.mkdir()
        stdout, stderr = io.StringIO(), io.StringIO()
        leaking = {"code": 403, "msg": "BAD key SECRET-KEY-VALUE", "data": None}
        with patch.object(self.helper, "load_coding_plan_api_key", lambda *a, **k: "SECRET-KEY-VALUE"), \
                patch.object(self.helper, "fetch_quota", lambda *a, **k: leaking), \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = self.helper.main(["--control-dir", str(control)])
        self.assertEqual(3, code)
        self.assertNotIn("SECRET-KEY-VALUE", stdout.getvalue())
        self.assertNotIn("SECRET-KEY-VALUE", stderr.getvalue())
        self.assertFalse((control / "glm-quota.json").exists())

    def test_decrypt_error_does_not_include_subprocess_stderr(self):
        class FakeResult:
            returncode = 1
            stdout = ""
            stderr = "node failure SECRET-STDERR"

        blob = "enc:v1:" + "A" * 16 + "." + "A" * 22 + "." + "A" * 43
        with patch.object(self.helper.subprocess, "run", return_value=FakeResult()):
            with self.assertRaises(ValueError) as caught:
                self.helper.decrypt_credential(blob, "node")
        self.assertNotIn("SECRET-STDERR", str(caught.exception))

    def test_expired_api_error_exit_code(self):
        control = self.base / "control-empty"
        control.mkdir()
        with patch.object(self.helper, "load_coding_plan_api_key", lambda *a, **k: "SECRET-KEY"), \
                patch.object(self.helper, "fetch_quota", lambda *a, **k: (_ for _ in ()).throw(OSError("network down"))):
            code = self.helper.main(["--control-dir", str(control)])
        self.assertEqual(3, code)
        self.assertFalse((control / "glm-quota.json").exists())


class TailTests(BaseControlTest):
    def dashboard(self):
        control = build_control_dir(self.base)
        return make_dashboard(control), control

    def test_codex_events_classified_without_commands(self):
        dash, control = self.dashboard()
        meta = dash.read_event_tail(str(control / "logs" / "run-events.jsonl"))
        self.assertTrue(meta["present"])
        self.assertEqual("Шаг завершён", meta["last_step"]["label"])
        self.assertNotIn("SECRET", json.dumps(meta["last_step"], ensure_ascii=False))

    def test_file_change_basename_only(self):
        text = ('{"type":"item.completed","item":{"id":"i","type":"file_change",'
                '"changes":[{"path":"C:/Users/roman/secret/Alpha.cs"}]}}\n')
        signature = d.classify_tail(text)
        self.assertEqual("Изменение файлов", signature["label"])
        self.assertEqual("Alpha.cs", signature["detail"])
        self.assertNotIn("secret", json.dumps(signature))

    def test_zcode_envelope_usage_extracted(self):
        dash, control = self.dashboard()
        meta = dash.read_event_tail(str(control / "evidence" / "dashboard-glm-events.jsonl"))
        self.assertEqual(120, meta["usage"]["totalTokens"])
        self.assertNotIn("SECRET", json.dumps(meta, ensure_ascii=False))
        self.assertNotIn("sess", json.dumps(meta, ensure_ascii=False))

    def test_outside_root_rejected(self):
        dash, control = self.dashboard()
        outside = self.base / "elsewhere" / "secret.jsonl"
        outside.parent.mkdir()
        outside.write_text("{}", encoding="utf-8")
        meta = dash.read_event_tail(str(outside))
        self.assertFalse(meta["present"])
        self.assertIn("вне разрешённых", meta["error"])

    def test_missing_and_unset_paths(self):
        dash, control = self.dashboard()
        meta = dash.read_event_tail(str(control / "logs" / "absent.jsonl"))
        self.assertFalse(meta["present"])
        self.assertIn("недоступен", meta["error"])
        meta = dash.read_event_tail(None)
        self.assertIn("не задан", meta["error"])

    def test_symlink_escape_rejected(self):
        dash, control = self.dashboard()
        outside = self.base / "elsewhere" / "secret.jsonl"
        outside.parent.mkdir()
        outside.write_text("{}", encoding="utf-8")
        link = control / "logs" / "link.jsonl"
        try:
            os.symlink(outside, link)
        except (OSError, NotImplementedError):
            # Windows file symlink may require elevation. A temporary directory
            # junction exercises the SAME ancestor escape without those privileges.
            if os.name != "nt":
                raise
            directory_link = control / "logs" / "outside-link"
            result = subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command",
                                     "New-Item -ItemType Junction -Path $env:HIF_TEST_LINK -Target $env:HIF_TEST_TARGET | Out-Null"],
                                    env=dict(os.environ, HIF_TEST_LINK=str(directory_link), HIF_TEST_TARGET=str(outside.parent)),
                                    capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            link = directory_link / outside.name
        meta = dash.read_event_tail(str(link))
        self.assertFalse(meta["present"])
        self.assertIn("вне разрешённых", meta["error"])

    def test_tail_bounded_but_last_step_found(self):
        control = build_control_dir(self.base)
        dash = make_dashboard(control)
        big = control / "logs" / "big.jsonl"
        line = '{"type":"turn.completed"}\n'
        big.write_text("junk line without json\n" * 20000 + line, encoding="utf-8")
        meta = dash.read_event_tail(str(big))
        self.assertGreater(meta["size_bytes"], 300000)
        self.assertEqual("Шаг завершён", meta["last_step"]["label"])

    def test_event_tail_encodings_keep_unicode_and_usage(self):
        dash, control = self.dashboard()
        event = {"type": "item.completed", "item": {"type": "file_change",
                 "changes": [{"path": "SECRET/Проверка.cs"}]},
                 "usage": {"totalTokens": 9}}
        text = json.dumps(event, ensure_ascii=False) + "\r\n"
        for encoding, bom in (("utf-8", b""), ("utf-8", b"\xef\xbb\xbf"),
                              ("utf-16-le", b"\xff\xfe"), ("utf-16-le", b""),
                              ("utf-16-be", b"\xfe\xff")):
            with self.subTest(encoding=encoding, bom=bom):
                path = control / "logs" / "encoded.jsonl"
                path.write_bytes(bom + text.encode(encoding))
                meta = dash.read_event_tail(str(path))
                self.assertTrue(meta["present"])
                self.assertEqual("Проверка.cs", meta["last_step"]["detail"])
                self.assertEqual(9, meta["usage"]["totalTokens"])
                self.assertNotIn("SECRET", json.dumps(meta, ensure_ascii=False))

    def test_utf16_large_failed_command_tail_is_aligned_and_bounded(self):
        from io import BytesIO

        class TrackedReader(BytesIO):
            def __init__(self, data):
                super().__init__(data)
                self.requests = []

            def read(self, size=-1):
                self.requests.append(size)
                return super().read(size)

        dash, control = self.dashboard()
        path = control / "logs" / "large-utf16.jsonl"
        event = {"type": "item.completed", "item": {"type": "command_execution",
                 "command": "python test_batch.py SECRET", "exit_code": 1,
                 "aggregated_output": "SECRET-OUTPUT"}}
        text = "diagnostic line\r\n" * 20000 + '{"type":"turn.started"}\r\n'
        text += json.dumps(event) + "\r\n"
        payload = b"\xff\xfe" + text.encode("utf-16-le")
        for cap, suffix in ((d.EVENT_TAIL_MAX_BYTES, b""),
                            (d.EVENT_TAIL_MAX_BYTES - 1, b""),
                            (d.EVENT_TAIL_MAX_BYTES, b"{")):
            with self.subTest(cap=cap, partial_code_unit=bool(suffix)):
                data = payload + suffix
                path.write_bytes(data)
                reader = TrackedReader(data)
                with patch.object(d, "EVENT_TAIL_MAX_BYTES", cap), \
                        patch.object(d, "open", return_value=reader, create=True):
                    meta = dash.read_event_tail(str(path))
                self.assertGreater(meta["size_bytes"], cap)
                self.assertEqual("Проверки завершены с ошибкой", meta["last_step"]["label"])
                self.assertEqual("error", meta["last_step"]["level"])
                self.assertNotIn("SECRET", json.dumps(meta, ensure_ascii=False))
                self.assertTrue(all(0 <= size <= cap for size in reader.requests))
                self.assertLessEqual(sum(reader.requests), cap + 4)

    def test_extract_envelope_usage_last_wins(self):
        text = json.dumps({"usage": {"totalTokens": 5}}) + "noise\n" + json.dumps(
            {"usage": {"totalTokens": 9, "inputTokens": 7}})
        self.assertEqual(9, d.extract_envelope_usage(text)["totalTokens"])
        self.assertIsNone(d.extract_envelope_usage('{"usage": {"input_tokens": 3}}'))


class NormalizeWindowsTests(unittest.TestCase):
    def test_labels_and_values(self):
        windows = d.normalize_windows(quota_value()["windows"])
        self.assertEqual("5-часовое окно", windows[0]["label"])
        self.assertEqual("Недельное окно", windows[1]["label"])
        self.assertEqual(1791296196, windows[0]["resets_at_epoch"])
        self.assertEqual(20.0, windows[0]["used_percent"])
        self.assertEqual(80.0, windows[0]["remaining_percent"])
        self.assertEqual([], d.normalize_windows(None))
        self.assertEqual([], d.normalize_windows([{"usedPercent": "x"}]))


class HttpTests(BaseControlTest):
    def setUp(self):
        super().setUp()
        self.control = build_control_dir(self.base)
        html = self.base / "page.html"
        html.write_text("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">"
                        "<title>fixture</title></head><body>fixture</body></html>", encoding="utf-8")
        self.dashboard = make_dashboard(self.control, html_path=html)
        self.server = d.make_server(self.dashboard, port=0)
        self.port = self.server.server_address[1]
        self.base_url = f"http://127.0.0.1:{self.port}"
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        # LIFO: shutdown -> server_close -> join (иначе join ждёт вечный serve_forever).
        self.addCleanup(self.thread.join)
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)

    def request(self, path, method="GET", headers=None):
        req = urllib.request.Request(self.base_url + path, method=method, headers=headers or {})
        try:
            with urllib.request.urlopen(req, timeout=5) as response:
                return response.status, dict(response.headers), response.read()
        except urllib.error.HTTPError as error:
            return error.code, dict(error.headers), error.read()

    def test_html_with_csp(self):
        status, headers, body = self.request("/")
        self.assertEqual(200, status)
        self.assertTrue(headers.get("Content-Type", "").startswith("text/html"))
        self.assertIn("Content-Security-Policy", headers)
        self.assertIn(b"fixture", body)

    def test_api_status_json(self):
        status, headers, body = self.request("/api/status")
        self.assertEqual(200, status)
        payload = json.loads(body.decode("utf-8"))
        self.assertIn("tasks", payload)
        self.assertIn("quota_gpt", payload)
        self.assertTrue(headers.get("Content-Type", "").startswith("application/json"))

    def test_health_and_favicon(self):
        status, _, body = self.request("/health")
        self.assertEqual(200, status)
        self.assertTrue(json.loads(body)["ok"])
        status, headers, _ = self.request("/favicon.ico")
        self.assertEqual(200, status)
        self.assertIn("image/svg", headers.get("Content-Type", ""))

    def test_unknown_path_404(self):
        status, _, _ = self.request("/etc/passwd")
        self.assertEqual(404, status)
        status, _, _ = self.request("/api/status/extra")
        self.assertEqual(404, status)

    def test_post_and_other_methods_405(self):
        for method in ("POST", "PUT", "DELETE", "OPTIONS"):
            status, headers, _ = self.request("/api/status", method=method,
                                              headers={"Content-Type": "application/json"})
            self.assertEqual(405, status, method)
            self.assertEqual("GET", headers.get("Allow"))

    def test_bad_host_403(self):
        status, _, _ = self.request("/api/status", headers={"Host": "evil.example:1234"})
        self.assertEqual(403, status)

    def test_cross_origin_403_and_same_origin_ok(self):
        status, _, _ = self.request("/api/status", headers={"Origin": "http://evil.example:8080"})
        self.assertEqual(403, status)
        status, _, _ = self.request("/", headers={"Origin": f"http://127.0.0.1:{self.port}"})
        self.assertEqual(200, status)
        status, _, _ = self.request("/api/status", headers={"Referer": "http://evil.example/x"})
        self.assertEqual(403, status)

    def test_malformed_and_non_http_origins_are_refused(self):
        for origin in ("http://127.0.0.1:bad", "http://127.0.0.1", "https://127.0.0.1:%d" % self.port,
                       "http://user:password@127.0.0.1:%d" % self.port, "null", "http://[broken"):
            status, _, _ = self.request("/api/status", headers={"Origin": origin})
            self.assertEqual(403, status, origin)

    def test_malformed_control_files_still_200_explicit(self):
        (self.control / "queue.json").write_text("{broken", encoding="utf-8")
        (self.control / "state.json").unlink()
        status, _, body = self.request("/api/status")
        self.assertEqual(200, status)
        payload = json.loads(body.decode("utf-8"))
        self.assertIn("некорректный JSON", payload["queue"]["error"])
        self.assertIn("отсутствует", payload["state"]["error"])
        self.assertEqual([], payload["tasks"])

    def test_control_files_untouched_by_serving(self):
        before = {name: (self.control / name).read_bytes()
                  for name in ("queue.json", "state.json", "controller.json", "codex-quota.json")}
        self.request("/")
        self.request("/api/status")
        self.request("/health")
        after = {name: (self.control / name).read_bytes()
                 for name in ("queue.json", "state.json", "controller.json", "codex-quota.json")}
        self.assertEqual(before, after)
        self.assertEqual({"dashboard-glm-events.jsonl", "dashboard-glm-run.json"},
                         {path.name for path in (self.control / "evidence").iterdir()})


class HtmlContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.html = Path(__file__).with_name("hif-dashboard.html").read_text(encoding="utf-8")

    def test_no_html_injection_or_external_content(self):
        for banned in ("innerHTML", "outerHTML", "insertAdjacentHTML", "document.write",
                       "<script src", "src=\"http", "href=\"http", "url(http", "@import"):
            self.assertNotIn(banned, self.html)

    def test_textcontent_and_autorefresh_present(self):
        self.assertIn("textContent", self.html)
        self.assertIn("replaceChildren", self.html)
        self.assertIn("REFRESH_MS = 5000", self.html)
        self.assertIn("setInterval(fetchData, REFRESH_MS)", self.html)
        self.assertIn('charset="utf-8"', self.html)
        self.assertIn("Работа агентов", self.html)

    def test_production_glm_is_distinguished_in_ui(self):
        self.assertIn("glmIdle.production_running", self.html)
        self.assertIn("GLM выполняет производственную задачу.", self.html)

    def test_error_banner_marks_not_live(self):
        self.assertIn("НЕ LIVE", self.html)
        self.assertIn("НЕ актуальны", self.html)
        self.assertIn("markConnectionError", self.html)

    def test_batch_and_stale_timer_explicit_not_live(self):
        self.assertIn('id="batchBody"', self.html)
        self.assertIn("Date.now() - lastRenderedAt > 15000", self.html)
        self.assertIn("Снимок устарел", self.html)
        self.assertIn("Native approval/dispatch guard", self.html)


class BatchDashboardTests(unittest.TestCase):
    def test_compact_progress_digest_and_no_approval_actions(self):
        record = dict(id="morning", source=dict(path="docs/technical_plan.md", revision="a"*40),
                      tasks=[dict(id="one", dependencies=[]), dict(id="two", dependencies=["one"])])
        digest = d.hashlib.sha256(json.dumps(record, sort_keys=True, ensure_ascii=False,
                                            separators=(",", ":")).encode("utf-8")).hexdigest()
        record.update(status="APPROVED", digest=digest, approval=dict(digest=digest, seal="fixture-recorded"))
        queue = dict(batches=[record], tasks=[dict(id="one", status="DONE")])
        value = d.batch_snapshot(queue, dict(next_action="ждём exact remote base"))
        self.assertEqual(1, value["packets"][0]["done"])
        self.assertEqual(1, value["packets"][0]["pending"])
        self.assertEqual("two", value["next_task"])
        self.assertIs(False, value["approval_verified"])
        record["tasks"][1]["dependencies"] = []
        self.assertEqual("WAIT_APPROVAL", d.batch_snapshot(queue, {})["packets"][0]["status"])
        self.assertIsNone(d.batch_snapshot(queue, {})["next_task"])

    def test_malformed_and_legacy_batches_do_not_claim_progress(self):
        self.assertEqual([], d.batch_snapshot({"tasks": []}, {})["packets"])
        self.assertIn("error", d.batch_snapshot({"batches": "bad", "tasks": []}, {}))
        self.assertIn("error", d.batch_snapshot({"batches": [{"tasks": [None]}], "tasks": []}, {}))


class TaskProgressTests(unittest.TestCase):
    def setUp(self):
        self.state = {"active_task_id": "demo", "status": "RUNNING"}
        self.tasks = [{"id": "demo", "title": "Тестовая задача"}]
        self.start = d.dt.datetime(2026, 10, 6, 12, 0, 0).timestamp()
        self.events = {"name": "20261006-120000-demo-events.jsonl", "age_seconds": 7,
                       "last_step": {"label": "Проверки выполняются"}}

    def result(self, **state):
        return d.progress_snapshot(dict(self.state, **state), self.tasks, self.events, self.start+1800)

    def test_running_is_stage_scale_not_code_percentage_or_eta(self):
        result = self.result()
        self.assertEqual("Реализация", result["phase"])
        self.assertEqual(14, result["percent"])
        self.assertEqual(1800, result["elapsed_seconds"])
        self.assertIsNone(result["eta_seconds"])
        self.assertIn("НЕ процент", result["disclaimer"])

    def test_waiting_review_does_not_claim_done(self):
        result = self.result(status="REVIEW_CANDIDATE")
        self.assertEqual("Ожидание независимого ревью", result["phase"])
        self.assertEqual(43, result["percent"])
        self.assertIn("PR и CI", result["remaining_steps"])

    def test_ci_merge_and_roadmap_are_distinct(self):
        for status, percent in (("WAIT_CI", 57), ("SYNC_MASTER_PENDING", 71), ("ROADMAP_SYNC_READY", 86), ("DONE", 100)):
            with self.subTest(status=status):
                self.assertEqual(percent, self.result(status=status)["percent"])

    def test_unknown_auth_blocker_has_no_fake_progress(self):
        for status in ("WAIT_AUTH", "BLOCKED", "MALFORMED"):
            self.assertIsNone(self.result(status=status)["percent"])

    def test_correction_can_move_back(self):
        self.assertLess(self.result(status="CORRECTION_READY")["percent"], self.result(status="WAIT_CI")["percent"])

    def test_no_active_or_ambiguous_task_no_percentage(self):
        self.assertFalse(self.result(active_task_id=None)["active"])
        self.tasks *= 2
        self.assertIsNone(self.result()["percent"])

    def test_missing_wrong_and_future_log_start_is_unknown(self):
        for name in (None, "20261006-120000-other-events.jsonl", "20261306-120000-demo-events.jsonl", "20261007-120000-demo-events.jsonl"):
            self.events["name"] = name
            self.assertIsNone(self.result()["elapsed_seconds"])

    def test_activity_is_redacted_label_not_raw_output(self):
        signature = d.classify_event({"type": "item.completed", "item": {"type": "command_execution",
            "command": "python test_control.py SECRET", "aggregated_output": "SECRET-OUTPUT", "exit_code": 1}})
        self.assertEqual("error", signature["level"])
        self.assertIn("с ошибкой", signature["label"])
        self.assertNotIn("SECRET", json.dumps(signature))

    def test_completed_unknown_command_is_not_a_passed_test(self):
        signature = d.classify_event({"type": "item.completed", "item": {"type": "command_execution", "status": "completed"}})
        self.assertEqual("info", signature["level"])
        self.assertNotIn("Проверки", signature["label"])

    def test_html_has_safe_progress_and_unknown_eta(self):
        html = Path(__file__).with_name("hif-dashboard.html").read_text(encoding="utf-8")
        self.assertIn('id="progressBody"', html)
        self.assertIn("НЕ процент готовности кода", html)
        self.assertIn("Прогноз времени неизвестен", html)


if __name__ == "__main__":
    unittest.main()
