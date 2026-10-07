"""Чистые правила утреннего пакета. Persistence/approval/transport принадлежат control."""
import copy
import datetime as dt
import hashlib
import json
import math
import re

SOURCE = "docs/technical_plan.md"
ID = r"[a-z0-9][a-z0-9-]{0,79}"
SHA = r"[a-f0-9]{40}"
INACTIVE = {"DONE", "CANCELLED", "WAIT_USER", "LIST_APPROVED_NOT_DISPATCHED"}
DEFINITION_KEYS = {"id", "title", "goal", "allowed_paths", "protected_contracts",
                   "acceptance", "dependencies", "risk", "player_facing",
                   "validation", "native_validation_profile", "native_qa"}


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False,
                                     separators=(",", ":"), allow_nan=False).encode("utf-8")).hexdigest()


def text(value):
    return isinstance(value, str) and bool(value.strip()) and len(value) <= 16000 and "\0" not in value


def strings(value, nonempty=True):
    return (isinstance(value, list) and (bool(value) or not nonempty)
            and all(text(item) for item in value) and len(set(value)) == len(value))


def safe_path(path):
    parts = path.rstrip("/").split("/") if isinstance(path, str) else []
    if (not parts or any(part in ("", ".", "..") for part in parts)
            or any(re.search(r'[\\:*?"<>|\x00-\x1f]', part) or part.endswith((" ", "."))
                   or re.fullmatch(r"(?i)(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?", part)
                   for part in parts) or parts[0].casefold() == ".git"):
        raise ValueError("Unsafe repository scope path")
    return path


def scope(task, changed):
    allowed = task.get("allowed_paths")
    if not strings(allowed):
        raise ValueError("Explicit scope required")
    for path in allowed:
        safe_path(path)
    for path in changed:
        if not path:
            continue
        safe_path(path)
        if not any(path.casefold() == p.casefold() or
                   (p.endswith("/") and path.casefold().startswith(p.casefold())) for p in allowed):
            raise ValueError("Out-of-scope diff: " + path)


def validate_packet(packet):
    if not isinstance(packet, dict) or set(packet) != {"id", "source", "tasks"}:
        raise ValueError("Packet schema mismatch")
    if not isinstance(packet["id"], str) or not re.fullmatch(ID, packet["id"]):
        raise ValueError("Unsafe batch ID")
    source = packet["source"]
    if (not isinstance(source, dict) or set(source) != {"path", "revision"}
            or source["path"] != SOURCE or not isinstance(source["revision"], str)
            or not re.fullmatch(SHA, source["revision"])):
        raise ValueError("Packet must cite exact repository roadmap")
    tasks = packet["tasks"]
    if not isinstance(tasks, list) or not 1 <= len(tasks) <= 15:
        raise ValueError("Packet requires 1..15 bounded tasks")
    ids = set()
    for task in tasks:
        if not isinstance(task, dict) or set(task) != DEFINITION_KEYS:
            raise ValueError("Definition schema mismatch; executable/extra fields prohibited")
        if not isinstance(task["id"], str) or not re.fullmatch(ID, task["id"]) or task["id"] in ids:
            raise ValueError("Duplicate/unsafe definition ID")
        ids.add(task["id"])
        for key in ("title", "goal", "validation"):
            if not text(task[key]):
                raise ValueError("Missing bounded " + key)
        for key in ("allowed_paths", "protected_contracts", "acceptance", "dependencies"):
            if not strings(task[key], nonempty=key != "dependencies"):
                raise ValueError("Malformed " + key)
        scope(task, [])
        if len({p.casefold() for p in task["allowed_paths"]}) != len(task["allowed_paths"]):
            raise ValueError("Duplicate case-insensitive scope")
        if task["risk"] not in ("low", "medium", "high") or type(task["player_facing"]) is not bool:
            raise ValueError("Malformed risk/player-facing flag")
        if task["native_validation_profile"] not in ("agent-control", "hif-runtime"):
            raise ValueError("Explicit fixed native QA profile required")
    visiting, visited = set(), set()
    by_id = {t["id"]: t for t in tasks}

    def visit(ident):
        if ident not in ids or ident in visiting:
            raise ValueError("Missing dependency/cycle")
        if ident in visited:
            return
        visiting.add(ident)
        for dependency in by_id[ident]["dependencies"]:
            visit(dependency)
        visiting.remove(ident)
        visited.add(ident)
    for ident in ids:
        visit(ident)
    return packet


def packet_of(record):
    return {key: record[key] for key in ("id", "source", "tasks")}


def validate_queue(queue, verify):
    if not isinstance(queue, dict) or not isinstance(queue.get("tasks"), list):
        raise ValueError("Malformed existing queue")
    execution_ids = [t.get("id") for t in queue["tasks"] if isinstance(t, dict)]
    if len(execution_ids) != len(queue["tasks"]) or not all(isinstance(i, str) for i in execution_ids) or len(set(execution_ids)) != len(execution_ids):
        raise ValueError("Malformed/duplicate execution IDs")
    records = queue.get("batches", [])
    if not isinstance(records, list):
        raise ValueError("Malformed batches")
    batch_ids, definition_ids = set(), set()
    for record in records:
        if not isinstance(record, dict) or set(record) != {"id", "source", "tasks", "digest", "status", "approval"}:
            raise ValueError("Malformed batch record")
        packet = validate_packet(packet_of(record))
        if record["id"] in batch_ids or record["digest"] != digest(packet):
            raise ValueError("Duplicate batch/digest drift")
        batch_ids.add(record["id"])
        if record["status"] not in ("PROPOSED", "APPROVED"):
            raise ValueError("Invalid batch status")
        if record["status"] == "PROPOSED":
            if record["approval"] is not None:
                raise ValueError("Draft cannot claim approval")
        else:
            receipt = record["approval"]
            if (not isinstance(receipt, dict) or set(receipt) != {"digest", "operator", "source", "approved_at", "seal"}
                    or receipt["digest"] != record["digest"] or not all(text(receipt[k]) for k in receipt)
                    or not receipt["source"].startswith("operator-console:")):
                raise ValueError("Explicit native operator receipt required, not model/source claim")
            payload = {k: v for k, v in receipt.items() if k != "seal"}
            try:
                moment = dt.datetime.fromisoformat(receipt["approved_at"].replace("Z", "+00:00"))
                if moment.tzinfo is None or moment > dt.datetime.now(dt.timezone.utc) + dt.timedelta(minutes=5):
                    raise ValueError("Invalid approval timestamp")
            except (ValueError, TypeError) as error:
                raise ValueError("Malformed approval timestamp") from error
            verify(receipt["seal"], payload)
        for definition in packet["tasks"]:
            if definition["id"] in definition_ids:
                raise ValueError("Duplicate definition across packets")
            definition_ids.add(definition["id"])
            runtime = next((t for t in queue["tasks"] if t["id"] == definition["id"]), None)
            if runtime and runtime.get("batch_id") != record["id"]:
                raise ValueError("Definition collides with legacy history")
    return records


def propose(queue, packet, verify):
    validate_queue(queue, verify)
    validate_packet(packet)
    result = copy.deepcopy(queue)
    result.setdefault("batches", []).append(dict(copy.deepcopy(packet), digest=digest(packet), status="PROPOSED", approval=None))
    validate_queue(result, verify)
    return result


def runtime_fields(record, definition):
    return dict(id=definition["id"], title=definition["title"], batch_id=record["id"],
                definition_digest=digest(definition), approved_source=record["source"],
                prompt=definition["goal"] + "\nProtected contracts:\n" + "\n".join(definition["protected_contracts"])
                + "\nAcceptance:\n" + "\n".join(definition["acceptance"]),
                **{k: copy.deepcopy(definition[k]) for k in ("allowed_paths", "validation", "risk", "player_facing",
                                                          "native_validation_profile", "native_qa")})


def task_guard(queue, task, changed, verify):
    records = validate_queue(queue, verify)
    scope(task, changed)
    if not task.get("batch_id"):
        if task.get("status") == "READY":
            raise ValueError("Unbound legacy task cannot bypass morning approval")
        return
    record = next((r for r in records if r["id"] == task["batch_id"]), None)
    if not record or record["status"] != "APPROVED":
        raise ValueError("Task is not explicitly batch-approved")
    definition = next((t for t in record["tasks"] if t["id"] == task["id"]), None)
    if not definition:
        raise ValueError("Missing approved definition")
    expected = runtime_fields(record, definition)
    if any(task.get(key) != value for key, value in expected.items()):
        raise ValueError("Runtime definition/scope drift")
    identity = {k: task.get(k) for k in ("id", "batch_id", "definition_digest", "base_sha", "writer_engine", "writer_path", "branch")}
    if isinstance(identity["writer_path"], str):
        identity["writer_path"] = identity["writer_path"].replace("\\", "/")
    verify(task.get("dispatch_seal"), identity)
    if not re.fullmatch(SHA, task.get("base_sha", "")) or task.get("branch") != "codex/" + task["id"]:
        raise ValueError("Dispatch identity malformed")


def route(quota, timestamp):
    try:
        age = (timestamp - dt.datetime.fromisoformat(quota["updated_at"].replace("Z", "+00:00"))).total_seconds()
        remaining = [quota["primary_remaining_percent"], quota["weekly_remaining_percent"]]
        if (quota.get("source") != "account/rateLimits/read" or quota.get("mode") not in ("NORMAL", "QUOTA_SAVE")
                or not 0 <= age <= 300 or any(type(v) not in (int, float) or not math.isfinite(v) or not 0 <= v <= 100 for v in remaining)):
            return None
        return "ZCode" if min(remaining) <= 25 else "Codex"
    except (KeyError, ValueError, TypeError, AttributeError):
        return None


def advance(queue, state, quota, remote_base, master_head, timestamp, verify, seal):
    """Следующий task только после DONE+native sync; ничего не переносит между writers."""
    result, new_state = copy.deepcopy(queue), copy.deepcopy(state)
    records = validate_queue(result, verify)
    active = [t for t in result["tasks"] if t.get("status") not in INACTIVE]
    if active:
        if len(active) != 1 or active[0]["id"] != state.get("active_task_id"):
            raise ValueError("Serial active-task identity conflict")
        if active[0].get("batch_id"):
            task_guard(result, active[0], [], verify)
        return result, new_state, "ACTIVE"
    previous = next((t for t in result["tasks"] if t["id"] == state.get("active_task_id")), None)
    if previous and previous["status"] != "DONE":
        raise ValueError("Previous task must be DONE")
    if previous and state.get("status") not in ("DONE", "BATCH_PENDING", "WAIT_QUOTA", "PLANNING_ONCE", "WAIT_USER"):
        return result, new_state, "WAIT"
    for task in result["tasks"]:
        if task.get("batch_id"):
            task_guard(result, task, [], verify)
            if task["status"] == "DONE" and (task.get("local_sync_done") is not True or not re.fullmatch(SHA, task.get("merge_sha", ""))):
                raise ValueError("DONE requires native post-merge sync")
            if task["status"] == "DONE":
                verify(task.get("local_sync_receipt"), {k: task.get(k) for k in
                       ("id", "base_sha", "head_sha", "merge_sha", "local_sync_done")})
    if state.get("status") in ("STOP", "MAINTENANCE", "PAUSED", "BLOCKED", "WAIT_AUTH", "WAIT_USER"):
        return result, new_state, "WAIT"
    completed = {t["id"] for t in result["tasks"] if t["status"] == "DONE"}
    dispatched = {t["id"] for t in result["tasks"]}
    pending = [(r, t) for r in records if r["status"] == "APPROVED" for t in r["tasks"] if t["id"] not in dispatched]
    eligible = next(((r, t) for r, t in pending if set(t["dependencies"]) <= completed), None)
    if pending and not eligible:
        new_state.update(status="BLOCKED", next_action="Зависимости не завершены; scope/partial diff сохраняются.")
        return result, new_state, "DEPENDENCIES"
    if not pending:
        if any(r["status"] == "PROPOSED" for r in records):
            new_state.update(status="WAIT_USER", next_action="Утвердите exact digest утреннего пакета native operator action.")
            return result, new_state, "WAIT_USER"
        if result.get("batch_planning") is None:
            result["batch_planning"] = {"after_batch": records[-1]["id"] if records else "initial", "status": "REQUESTED"}
            new_state.update(status="PLANNING_ONCE", next_action="Один bounded planning pass: proposal 1..15, без approval/dispatch.")
            return result, new_state, "PLANNING_ONCE"
        if result["batch_planning"].get("status") == "REQUESTED":
            new_state.update(status="PLANNING_ONCE", next_action="Native claim перед единственным fresh planning wake.")
            return result, new_state, "PLANNING_ONCE"
        new_state.update(status="WAIT_USER", next_action="Пакет исчерпан; planning уже запрошен, ждём утреннего approval.")
        return result, new_state, "WAIT_USER"
    engine = route(quota, timestamp)
    if not engine:
        new_state.update(status="WAIT_QUOTA", next_action="UNKNOWN/stale quota: следующий writer не выбран.")
        return result, new_state, "WAIT_QUOTA"
    if not re.fullmatch(SHA, remote_base or "") or master_head != remote_base:
        raise ValueError("Next task requires fresh remote master and exact native-synced reference")
    record, definition = eligible
    task = runtime_fields(record, definition)
    task.update(status="READY", base_sha=remote_base, branch="codex/" + task["id"], writer_engine=engine,
                writer_path="D:/How_I_Fall/" + ("zagent" if engine == "ZCode" else "agent"))
    task["dispatch_seal"] = seal({k: task[k] for k in ("id", "batch_id", "definition_digest", "base_sha", "writer_engine", "writer_path", "branch")})
    result["tasks"].append(task)
    new_state.update(status="READY", active_task_id=task["id"], base_sha=remote_base,
                     head_sha=None, next_action="Serial approved writer: " + engine)
    return result, new_state, "READY"
