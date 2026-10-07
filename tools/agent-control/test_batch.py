"""Утренние пакеты: temporary fixtures, fake seals/remote/quota; никаких live writes."""
import copy
import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("control", Path(__file__).with_name("hif-control.py"))
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)
b = c.batch
TIME = dt.datetime.now(dt.timezone.utc)


def seal(payload):
    return "native-fixture:" + b.digest(payload)


def verify(value, payload):
    if value != seal(payload):
        raise ValueError("Fake/stale receipt")


def definition(ident, dependencies=()):
    return dict(id=ident, title="Fixture " + ident, goal="Fixture, не новая game feature.",
                allowed_paths=["tools/agent-control/test_batch.py"], protected_contracts=["develop protected"],
                acceptance=["Temporary fixture coverage"], dependencies=list(dependencies), risk="high",
                player_facing=False, validation="Fixed agent-control", native_validation_profile="agent-control", native_qa=None)


def packet():
    return dict(id="morning", source=dict(path=b.SOURCE, revision="a"*40),
                tasks=[definition("first"), definition("second", ["first"])])


def approved():
    q = b.propose({"tasks": []}, packet(), verify)
    r = q["batches"][0]
    payload = dict(digest=r["digest"], operator="fixture-user", source="operator-console:explicit-user-decision",
                   approved_at=TIME.isoformat())
    r.update(status="APPROVED", approval=dict(payload, seal=seal(payload)))
    return q


def quota(value=90):
    return dict(updated_at=TIME.isoformat(), source="account/rateLimits/read", mode="NORMAL",
                primary_remaining_percent=value, weekly_remaining_percent=90)


class BatchTests(unittest.TestCase):
    def advance(self, q=None, s=None, base="a"*40, limits=None):
        return b.advance(q or approved(), s or dict(status="IDLE"), limits or quota(), base, base, TIME, verify, seal)

    def finish(self, q, s):
        t = q["tasks"][-1]
        t.update(status="DONE", head_sha="b"*40, merge_sha="c"*40, local_sync_done=True)
        t["local_sync_receipt"] = seal({k: t[k] for k in ("id", "base_sha", "head_sha", "merge_sha", "local_sync_done")})
        s["status"] = "DONE"

    def test_two_serial_tasks_new_base_after_native_sync_done(self):
        q, s, result = self.advance()
        self.assertEqual("READY", result)
        self.assertEqual(["first"], [t["id"] for t in q["tasks"]])
        self.finish(q, s)
        q, s, result = self.advance(q, s, "c"*40)
        self.assertEqual("READY", result)
        self.assertEqual("second", s["active_task_id"])
        self.assertEqual("c"*40, q["tasks"][-1]["base_sha"])

    def test_source_only_proposal_never_dispatches(self):
        q = b.propose({"tasks": []}, packet(), verify)
        q, s, outcome = self.advance(q)
        self.assertEqual("WAIT_USER", outcome)
        self.assertEqual([], q["tasks"])

    def test_partial_and_candidate_protection_idempotency(self):
        for status in ("READY", "RUNNING", "REVIEW_CANDIDATE", "WAIT_CI", "WAIT_AUTH", "WAIT_STRONG_REVIEW", "PARTIAL_RETRY", "CORRECTION_READY"):
            q, s, _ = self.advance()
            q["tasks"][0].update(status=status, resume_head_sha="d"*40)
            s["status"] = status
            original = copy.deepcopy(q)
            q2, s2, outcome = self.advance(q, s, "c"*40, quota(1))
            self.assertEqual("ACTIVE", outcome)
            self.assertEqual(original, q2)
            self.assertEqual(s, s2)

    def test_approval_digest_or_signed_payload_drift_fail_closed(self):
        for mutate in (lambda q: q["batches"][0]["tasks"][0].update(goal="broadened"),
                       lambda q: q["batches"][0]["approval"].update(digest="0"*64),
                       lambda q: q["batches"][0]["approval"].update(operator="model"),
                       lambda q: q["batches"][0].update(approval={"approved_source": "roadmap"}),
                       lambda q: q["batches"][0]["approval"].update(source="model:approved")):
            q = approved(); mutate(q)
            with self.assertRaises(ValueError):
                self.advance(q)

    def test_recomputed_digest_still_cannot_reuse_old_receipt(self):
        q = approved(); r = q["batches"][0]
        r["tasks"][0]["allowed_paths"] += ["Assets/"]
        r["digest"] = b.digest(b.packet_of(r)); r["approval"]["digest"] = r["digest"]
        with self.assertRaises(ValueError):
            self.advance(q)

    def test_malformed_bounds_duplicates_cycles_deps(self):
        cases = [dict(packet(), tasks=[]), dict(packet(), tasks=[definition("t" + str(i)) for i in range(16)]),
                 dict(packet(), tasks=[definition("same"), definition("same")]),
                 dict(packet(), tasks=[definition("self", ["self"])]),
                 dict(packet(), tasks=[definition("one", ["two"]), definition("two", ["one"])]),
                 dict(packet(), tasks=[definition("one", ["missing"])]), dict(packet(), executable="cmd.exe")]
        for value in cases:
            with self.assertRaises(ValueError):
                b.validate_packet(value)

    def test_unknown_selection_and_commands_rejected_before_approval(self):
        for native in (dict(native_validation_profile="shell"), dict(native_qa={"command": "cmd.exe"}),
                       dict(native_validation_profile="hif-runtime", native_qa={"unity": [{"mode": "PlayMode", "filter": "All"}], "graphical": []})):
            value = packet(); value["tasks"][0].update(native)
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory); c.save(root / "state.json", {"status": "IDLE"}); c.save(root / "queue.json", {"tasks": []})
                with self.assertRaises(ValueError), patch.object(c, "git_output") as git:
                    c.batch_propose(root, value)
                git.assert_not_called()

    def test_duplicate_across_batches_or_legacy_collision_rejected(self):
        q = approved(); other = packet(); other["id"] = "other"
        with self.assertRaises(ValueError):
            b.propose(q, other, verify)
        with self.assertRaises(ValueError):
            b.propose({"tasks": [{"id": "first", "status": "DONE"}]}, packet(), verify)

    def test_next_proposal_does_not_block_existing_approved_packet(self):
        draft = dict(id="tomorrow", source=packet()["source"], tasks=[definition("third")])
        q = b.propose(approved(), draft, verify)
        q, s, outcome = self.advance(q)
        self.assertEqual("READY", outcome)
        self.assertEqual("first", s["active_task_id"])
        self.assertEqual("PROPOSED", q["batches"][1]["status"])

    def test_native_proposal_after_done_preserves_approved_pending_dispatch(self):
        q, s, _ = self.advance(); self.finish(q, s)
        draft = dict(id="tomorrow", source=packet()["source"], tasks=[definition("third")])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); c.persist_control(root, q, s)
            with patch.object(c, "verify_receipt", verify), patch.object(c, "receipt_seal", seal), patch.object(c, "git_output", return_value="source roadmap"):
                c.batch_propose(root, draft)
            self.assertEqual("DONE", c.load(root / "state.json")["status"])
            q, s, result = self.advance(c.load(root / "queue.json"), c.load(root / "state.json"), "c"*40)
            self.assertEqual("READY", result)
            self.assertEqual("second", s["active_task_id"])

    def test_cancelled_dependency_does_not_count_as_done(self):
        q, s, _ = self.advance(); q["tasks"][0]["status"] = "CANCELLED"
        s.pop("active_task_id"); s["status"] = "IDLE"
        q, s, result = self.advance(q, s)
        self.assertEqual("DEPENDENCIES", result)
        self.assertEqual(1, len(q["tasks"]))

    def test_legacy_history_and_three_list_items_preserved(self):
        legacy = [{"id": "old", "status": "DONE", "summary": "history"}] + [dict(id="game-" + str(i), status="LIST_APPROVED_NOT_DISPATCHED") for i in range(3)]
        q = approved(); q["tasks"] = copy.deepcopy(legacy)
        q, s, _ = self.advance(q)
        self.assertEqual(legacy, q["tasks"][:4])
        self.assertEqual("first", q["tasks"][-1]["id"])
        _, _, result = self.advance({"tasks": legacy})
        self.assertEqual("PLANNING_ONCE", result) # Initial proposal only, no legacy READY.

    def test_unbound_legacy_ready_cannot_bypass_batch(self):
        q = approved(); task = dict(definition("unapproved"), status="READY")
        q["tasks"].append(task)
        with self.assertRaises(ValueError):
            b.task_guard(q, task, [], verify)

    def test_done_without_native_sync_receipt_cannot_dispatch(self):
        q, s, _ = self.advance(); self.finish(q, s)
        q["tasks"][0].pop("local_sync_receipt")
        with self.assertRaises(ValueError):
            self.advance(q, s, "c"*40)

    def test_fresh_master_exact_reference_required(self):
        with self.assertRaises(ValueError):
            b.advance(approved(), {"status": "IDLE"}, quota(), "c"*40, "a"*40, TIME, verify, seal)

    def test_scope_guards_definition_identity_and_both_rename_sides(self):
        q, s, _ = self.advance(); t = q["tasks"][0]
        b.task_guard(q, t, ["tools/agent-control/test_batch.py"], verify)
        for changes in (["Assets/Runtime.cs"], ["tools/agent-control/test_batch.py", "Assets/old.cs"], ["../escape"]):
            with self.assertRaises(ValueError):
                b.task_guard(q, t, changes, verify)
        for key, value in (("prompt", "other"), ("writer_path", "D:/How_I_Fall/develop"), ("base_sha", "c"*40),
                           ("writer_engine", "ZCode"), ("branch", "codex/other"), ("native_qa", {})):
            changed = dict(t, **{key: value})
            with self.assertRaises(ValueError):
                b.task_guard(q, changed, [], verify)

    def test_scope_paths_reject_windows_escapes(self):
        for path in ("Assets/../tools/x", "C:/x", "//x", "tools\\x", ".git/config", "tools/x:stream", "tools/CON", "tools/x.", "tools//x"):
            with self.assertRaises(ValueError):
                b.safe_path(path)

    def test_json_duplicate_keys_and_nonfinite_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "queue.json"
            for source in ('{"tasks":[],"tasks":[]}', '{"quota":NaN}', '{"quota":Infinity}'):
                path.write_text(source, encoding="utf-8")
                with self.assertRaises(ValueError):
                    c.load(path)

    def test_quota_threshold_unknown_nan_stale(self):
        for value, engine in ((25, "ZCode"), (26, "Codex"), (0, "ZCode")):
            q, s, _ = self.advance(limits=quota(value))
            self.assertEqual(engine, q["tasks"][-1]["writer_engine"])
        for update in (dict(mode="UNKNOWN"), dict(source="history"), dict(primary_remaining_percent=float("nan")),
                       dict(updated_at=(TIME - dt.timedelta(seconds=301)).isoformat()), dict(weekly_remaining_percent=True)):
            q, s, outcome = self.advance(limits=dict(quota(), **update))
            self.assertEqual("WAIT_QUOTA", outcome)
            self.assertEqual([], q["tasks"])

    def test_exhaustion_requests_planning_once_restart_waits(self):
        q, s, _ = self.advance(); self.finish(q, s)
        q, s, _ = self.advance(q, s, "c"*40); self.finish(q, s)
        q, s, result = self.advance(q, s, "c"*40)
        self.assertEqual("PLANNING_ONCE", result)
        q2, s2, result = self.advance(q, s, "c"*40)
        self.assertEqual("PLANNING_ONCE", result) # REQUESTED survives restart before claim.
        q2["batch_planning"]["status"] = "CLAIMED"
        q, s = q2, s2
        q2, s2, result = self.advance(q, s, "c"*40)
        self.assertEqual("WAIT_USER", result)
        self.assertEqual(q, q2)

    def test_partial_control_write_is_fatal_even_stop_unwritable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original_save = c.save
            def write(path, value):
                if Path(path).name == "state.json":
                    raise OSError("disk failure")
                original_save(path, value)
            with patch.object(c, "save", side_effect=write), self.assertRaises(c.FatalPersistence):
                c.persist_control(root, {"tasks": []}, {"status": "READY"})
            self.assertTrue((root / "STOP").exists())
            with patch.object(c, "save", side_effect=OSError()), patch.object(Path, "write_text", side_effect=OSError()), self.assertRaises(c.FatalPersistence):
                c.persist_control(root, {}, {})

    def test_noninteractive_native_approval_and_source_only_claim_refused(self):
        with patch.object(sys.stdin, "isatty", return_value=False), self.assertRaises(ValueError):
            c.approve_batch("unused", "morning", "digest", "operator-console:decision")
        with patch.object(sys.stdin, "isatty", return_value=True), patch.object(sys.stdout, "isatty", return_value=True), self.assertRaises(ValueError):
            c.approve_batch("unused", "morning", "digest", "repository:source-only")

    def test_interactive_receipt_action_binds_exact_digest(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); c.save(root / "queue.json", b.propose({"tasks": []}, packet(), verify)); c.save(root / "state.json", {"status": "WAIT_USER"})
            value = c.load(root / "queue.json")["batches"][0]["digest"]
            with patch.object(c, "operator_origin"), patch.object(sys.stdin, "isatty", return_value=True), patch.object(sys.stdout, "isatty", return_value=True), patch.object(c, "verify_receipt", verify), patch.object(c, "receipt_seal", seal), patch("builtins.print"):
                with self.assertRaises(ValueError):
                    c.approve_batch(root, "morning", "wrong", "operator-console:user", lambda _: "APPROVE " + value)
                with self.assertRaises(ValueError):
                    c.approve_batch(root, "morning", value, "operator-console:user", lambda _: "yes")
                c.approve_batch(root, "morning", value, "operator-console:user", lambda _: "APPROVE " + value)
            self.assertEqual("BATCH_PENDING", c.load(root / "state.json")["status"])
            b.validate_queue(c.load(root / "queue.json"), verify)

    def test_native_guard_runs_all_boundaries(self):
        worker = Path(__file__).with_name("hif-worker.ps1").read_text(encoding="utf-8-sig")
        reviewer = Path(__file__).with_name("hif-reviewer.ps1").read_text(encoding="utf-8-sig")
        self.assertLess(worker.index('--phase writer'), worker.index("SetProp $task 'status' 'RUNNING'"))
        self.assertLess(worker.index('--phase publish'), worker.index("RunGit (@('add'"))
        self.assertLess(reviewer.index('--phase review'), reviewer.index('$prompt=@'))
        q, s, _ = self.advance(); t = q["tasks"][0]
        self.assertTrue(c.needs_strong(t, []))
        with patch.object(c, "scope_guard", side_effect=ValueError("scope guard")), self.assertRaisesRegex(ValueError, "scope guard"):
            c.gate(t, None, None, {}, {}, [])

    def test_native_tick_two_bases_and_exhaustion_without_live_transport(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); c.save(root / "queue.json", approved()); c.save(root / "state.json", {"status": "IDLE"})
            with patch.object(c, "verify_receipt", verify), patch.object(c, "receipt_seal", seal), patch.object(c, "fresh_quota", return_value=quota()), patch.object(c, "github", return_value={"commit": {"sha": "a"*40}}), patch.object(c, "git_output", return_value="a"*40):
                self.assertEqual("READY", c.batch_tick(root)["status"])
                q, s = c.load(root / "queue.json"), c.load(root / "state.json")
                self.finish(q, s); c.persist_control(root, q, s)
                with patch.object(c, "github", return_value={"commit": {"sha": "c"*40}}), patch.object(c, "git_output", return_value="c"*40):
                    self.assertEqual("READY", c.batch_tick(root)["status"])
                    self.assertEqual("c"*40, c.load(root / "queue.json")["tasks"][-1]["base_sha"])
            self.assertEqual("account/rateLimits/read", c.load(root / "codex-quota.json")["source"])

    def test_native_actual_scope_boundaries_block_drift_before_git_writes(self):
        q, s, _ = self.advance()
        t = q["tasks"][0]; t.update(head_sha="b"*40)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); c.persist_control(root, q, s)
            def git(repo, *args):
                if args[0] == "branch":
                    return t["branch"]
                if args[0] == "rev-parse":
                    return t["head_sha"]
                return "Assets/outside.cs"
            with patch.object(c, "verify_receipt", verify), patch.object(c, "git_output", side_effect=git), patch.object(c, "task_changed_files", return_value=["Assets/outside.cs"]):
                for phase in ("writer", "publish", "review"):
                    with self.assertRaisesRegex(ValueError, "Out-of-scope"):
                        c.boundary_scope(root, phase)
                with self.assertRaisesRegex(ValueError, "Out-of-scope"):
                    c.gate(t, None, None, {}, {}, ["Assets/outside.cs"], control=root)

    def test_model_origin_cannot_sign_approval_even_via_direct_api(self):
        with patch.dict(os.environ, {"CODEX_THREAD_ID": "fixture-model"}):
            with self.assertRaisesRegex(ValueError, "Model process"):
                c.receipt_seal({"operator": "model", "source": "operator-console:fake"})
            with self.assertRaisesRegex(ValueError, "Model process"):
                c.approve_batch("unused", "morning", "digest", "operator-console:fake")

    def test_origin_rejects_model_ancestry_even_with_tty(self):
        env = {k: v for k, v in os.environ.items() if k not in ("CODEX_THREAD_ID", "CODEX_SESSION_ID", "CODEX_SANDBOX_NETWORK_DISABLED", "ZCODE_SESSION_ID")}
        with patch.dict(os.environ, env, clear=True), patch.object(c.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, '["python.exe", "codex.exe exec"]')):
            with self.assertRaisesRegex(ValueError, "ancestry"):
                c.operator_origin()

    def test_quota_write_failure_fatal_and_unknown_freshness_wait(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.object(c, "save", side_effect=OSError()), self.assertRaises(c.FatalPersistence):
                c.persist_quota(root, quota())
            self.assertTrue((root / "STOP").exists())
        with patch.object(c, "codex_rpc", side_effect=RuntimeError("unavailable")):
            self.assertEqual("UNKNOWN", c.fresh_quota()["mode"])

    @unittest.skipUnless(sys.platform == "win32", "Actual scheduler CAS fixture")
    def test_scheduler_cas_does_not_overwrite_concurrent_native_approval(self):
        source = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig")
        functions = source[source.index("function AssertControlPaths"):source.index("$env:CODEX_HOME")]
        with tempfile.TemporaryDirectory(prefix="hif-batch-cas-") as directory:
            root = Path(directory)
            c.save(root / "state.json", {"status": "BATCH_PENDING", "active_task_id": "old"})
            functions = functions.replace("Local\\HowIFallWriter", "Local\\FixtureWriter-" + root.name)
            script = "$ErrorActionPreference='Stop';$C='" + directory + "'\n" + functions
            script += "\n$old=[pscustomobject]@{status='WAIT_USER';active_task_id='old'};$expected=$old|ConvertTo-Json -Depth 30 -Compress;SaveSchedulerState $old $expected\n"
            (root / "cas.ps1").write_text(script, encoding="utf-8-sig")
            result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root / "cas.ps1")], capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertEqual("BATCH_PENDING", c.load(root / "state.json")["status"])

    def proposal_cli_fixture(self, root):
        c.persist_control(root, {"tasks": [{"id": "history", "status": "DONE"}]}, {"status": "WAIT_USER"})
        c.save(root / "packet.json", packet())
        # Real CLI and Windows mutexes, isolated names; no live control/Git/models.
        driver = root / "proposal.py"
        driver.write_text(
            "import importlib.util, sys\nfrom pathlib import Path\n"
            f"spec=importlib.util.spec_from_file_location('control', {str(Path(c.__file__).resolve())!r})\n"
            "c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)\n"
            f"root=Path({str(root)!r})\n"
            "c.control_paths=lambda value: None if value.resolve()==root.resolve() else sys.exit(2)\n"
            "original_lock=c.writer_lock\n"
            "c.writer_lock=lambda name='Local\\\\HowIFallWriter': original_lock(name+'-'+root.name)\n"
            "c.git_output=lambda *args: 'fixture roadmap'\n"
            "c.fresh_quota=lambda: {'mode':'NORMAL'}\n"
            "original_load=c.load\n"
            "def load(path):\n"
            "    with (root/'reads.txt').open('a') as log: log.write(Path(path).name+'\\n')\n"
            "    return original_load(path)\n"
            "c.load=load\n"
            "try: c.main()\n"
            "except Exception as error: print(str(error), file=sys.stderr);sys.exit(1)\n", encoding="utf-8")
        return [sys.executable, "-B", str(driver), "batch-propose", "--control", str(root), "--output", str(root / "packet.json")]

    @unittest.skipUnless(sys.platform == "win32", "Actual standalone proposal wake contention")
    def test_standalone_proposal_refuses_active_supervisor_before_read_or_write(self):
        with tempfile.TemporaryDirectory(prefix="hif-proposal-lock-") as directory:
            root = Path(directory); argv = self.proposal_cli_fixture(root)
            before = {name: (root / name).read_bytes() for name in ("queue.json", "state.json")}
            with c.writer_lock("Local\\HowIFallSupervisorWake-" + root.name):
                result = subprocess.run(argv, capture_output=True, timeout=15)
            self.assertEqual(1, result.returncode, result.stderr.decode(errors="replace"))
            self.assertIn("busy", result.stderr.decode(errors="replace"))
            self.assertFalse((root / "reads.txt").exists())
            self.assertFalse((root / "STOP").exists())
            for name, value in before.items():
                self.assertEqual(value, (root / name).read_bytes())

    @unittest.skipUnless(sys.platform == "win32", "Actual proposal overlapping snapshot preservation")
    def test_proposal_after_wake_uses_latest_queue_state_not_overlapping_snapshot(self):
        with tempfile.TemporaryDirectory(prefix="hif-proposal-stale-") as directory:
            root = Path(directory); argv = self.proposal_cli_fixture(root)
            with c.writer_lock("Local\\HowIFallSupervisorWake-" + root.name):
                stale_q, stale_s = c.load(root / "queue.json"), c.load(root / "state.json")
                result = subprocess.run(argv, capture_output=True, timeout=15)
                self.assertEqual(1, result.returncode, result.stderr.decode(errors="replace"))
                # Supervisor publishes from its own snapshot, still exclusively held.
                stale_q["tasks"] += [dict(id="active", status="WAIT_CI", head_sha="b"*40)]
                stale_s.update(status="WAIT_CI", active_task_id="active", head_sha="b"*40)
                c.persist_control(root, stale_q, stale_s)
            result = subprocess.run(argv, capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            current = c.load(root / "queue.json")
            self.assertEqual(stale_q["tasks"], current["tasks"])
            self.assertEqual(stale_s, c.load(root / "state.json"))
            self.assertEqual("PROPOSED", current["batches"][0]["status"])
            self.assertIsNone(current["batches"][0]["approval"])
            self.assertFalse((root / "STOP").exists())

    @unittest.skipUnless(sys.platform == "win32", "Actual native wake planning handoff fixture")
    def test_wake_imports_fresh_draft_only_after_success_and_lock_release(self):
        source = Path(__file__).with_name("wake-supervisor.ps1").read_text(encoding="utf-8-sig")
        for mode in ("success", "failed-turn", "missing-draft", "unclaimed", "paused", "nonplanning"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory(prefix="hif-wake-proposal-") as directory:
                root = Path(directory); self.proposal_cli_fixture(root)
                (root / "proposal.py").rename(root / "hif-control.py")
                c.save(root / "controller.json", {"enabled": True})
                q = c.load(root / "queue.json")
                q["batch_planning"] = {"status": "REQUESTED" if mode == "unclaimed" else "CLAIMED"}
                state = {"status": "IDLE" if mode == "nonplanning" else "PLANNING_ONCE"}
                c.persist_control(root, q, state)
                # A stale input from an earlier wake must never be imported.
                c.save(root / "morning-packet-stale.json", packet())
                model = root / "fixture-model.ps1"
                model.write_text(
                    "$text=$input | Out-String\n"
                    "Set-Content -LiteralPath (Join-Path $PSScriptRoot 'model-called') 'fixture'\n"
                    "if($text -match 'ephemeral input: ([^\\r\\n]+)'){\n"
                    "  $draft=$Matches[1].Trim()\n" +
                    ("" if mode == "missing-draft" else
                     "  Get-Content (Join-Path $PSScriptRoot 'packet.json') -Raw | Set-Content -LiteralPath $draft -Encoding UTF8\n") +
                    "}\n" +
                    ("Set-Content -LiteralPath (Join-Path $PSScriptRoot 'STOP') 'fixture pause'\n" if mode == "paused" else "") +
                    ("exit 7\n" if mode == "failed-turn" else "exit 0\n"), encoding="utf-8-sig")
                script = source.replace("D:\\How_I_Fall\\agent-control", directory)
                script = script.replace("Local\\HowIFallSupervisorWake", "Local\\HowIFallSupervisorWake-" + root.name)
                discovery = next(line for line in script.splitlines() if line.startswith("    $codex="))
                invocation = next(line for line in script.splitlines() if line.startswith("    $prompt | & $codex"))
                script = script.replace(discovery, "$codex='" + str(model) + "'")
                script = script.replace(invocation, "    $prompt | & $codex")
                (root / "wake.ps1").write_text(script, encoding="utf-8-sig")
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root / "wake.ps1")], capture_output=True, timeout=20)
                self.assertEqual(0 if mode in ("success", "nonplanning") else 1, result.returncode,
                                 result.stderr.decode(errors="replace"))
                current = c.load(root / "queue.json")
                self.assertEqual(q["tasks"], current["tasks"])
                if mode == "success":
                    self.assertEqual("PROPOSED", current["batches"][0]["status"])
                    self.assertEqual("WAIT_USER", c.load(root / "state.json")["status"])
                    self.assertEqual(1, len(list(root.glob("morning-packet-*.json"))) - 1)
                else:
                    self.assertEqual(q, current)
                    self.assertEqual(state, c.load(root / "state.json"))
                if mode == "unclaimed":
                    self.assertFalse((root / "model-called").exists())

    @unittest.skipUnless(sys.platform == "win32", "Actual operator lock contention fixture")
    def test_scheduler_lock_busy_is_not_fatal_control_write(self):
        source = Path(__file__).with_name("supervisor-loop.ps1").read_text(encoding="utf-8-sig")
        functions = source[source.index("function AssertControlPaths"):source.index("$env:CODEX_HOME")]
        with tempfile.TemporaryDirectory(prefix="hif-batch-busy-") as directory:
            root = Path(directory); name = "Local\\FixtureBusy-" + root.name
            c.save(root / "state.json", {"status": "WAIT_USER"})
            functions = functions.replace("Local\\HowIFallWriter", name)
            script = "$ErrorActionPreference='Stop';$C='" + directory + "'\n" + functions
            script += "\nSaveSchedulerState ([pscustomobject]@{status='BLOCKED'})\n"
            (root / "busy.ps1").write_text(script, encoding="utf-8-sig")
            with c.writer_lock(name):
                result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(root / "busy.ps1")], capture_output=True, timeout=15)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            self.assertFalse((root / "STOP").exists())
            self.assertEqual("WAIT_USER", c.load(root / "state.json")["status"])

    @unittest.skipUnless(sys.platform == "win32", "DPAPI Windows receipt")
    def test_actual_dpapi_receipt_roundtrip_tampering(self):
        payload = {"digest": "a"*64, "source": "fixture"}
        value = c.receipt_seal(payload)
        c.verify_receipt(value, payload)
        with self.assertRaises(ValueError):
            c.verify_receipt(value, dict(payload, digest="b"*64))
        with self.assertRaises(ValueError):
            c.verify_receipt("model-claim", payload)


if __name__ == "__main__":
    unittest.main()
