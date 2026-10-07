"""Локальный read-only экран состояния HIF-агентов (http://127.0.0.1:8765).

Только стандартная библиотека. Дашборд только читает runtime-контур
D:/How_I_Fall/agent-control и никогда не пишет queue/state/controller/
codex-quota файлы, Git и сохранения. Живая квота GPT берётся из уже
установленного hif-control.py (codex_rpc + quota_result), кэш только в памяти;
обновление — throttled single-flight не чаще 60 c, UI не блокируется.
Остаток GLM читается из agent-control/glm-quota.json, который пишет отдельный
opt-in хелпер hif-glm-quota.py (свой запрос к api.z.ai с coding-plan api-key);
сам дашборд учётные данные не читает и процессов не запускает. Без свежего
снимка остаток GLM честно неизвестен, расход отдельных запросов остатком
не считается.
Никаких mutation/execute маршрутов: только /, /api/status, /health, favicon.
"""
import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import threading
import time
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


DEFAULT_CONTROL_DIR = Path("D:/How_I_Fall/agent-control")
DEFAULT_CONTROL_MODULE = DEFAULT_CONTROL_DIR / "hif-control.py"
DEFAULT_HTML_PATH = Path(__file__).with_name("hif-dashboard.html")
QUOTA_REFRESH_MIN_INTERVAL = 60.0
QUOTA_FRESH_TTL = 60.0
PROCESS_POLL_MIN_INTERVAL = 10.0
PROCESS_HELPER_TIMEOUT = 10.0
EVENT_TAIL_MAX_BYTES = 256 * 1024
GLM_USAGE_RECHECK_INTERVAL = 60.0
GLM_LEGACY_SCAN_FILES = 4
GLM_QUOTA_FILE_STALE_SECONDS = 900.0
STALE_WRITE_NOTE_SECONDS = 300.0
SUPPORT_RUNNING_STATUSES = {"SUPPORT_RUNNING", "RUNNING", "IN_PROGRESS"}
GROUP_ORDER = ("running", "review", "waiting", "error", "done", "unknown")

HTML_CSP = ("default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; "
            "img-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none'; "
            "frame-ancestors 'none'")
FAVICON_SVG = (b'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">'
               b'<rect width="16" height="16" rx="3" fill="#0d1b2a"/>'
               b'<circle cx="8" cy="8" r="5" fill="#35c4b5"/></svg>')

# Фиксированный запрос списка процессов: никакой подстановки аргументов,
# shell=False, вывод — только для внутренней фильтрации; наружу идут pid/role/
# engine/created_at, но не commandline.
POWERSHELL_SCRIPT = (
    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; "
    "$rows = Get-CimInstance Win32_Process | ForEach-Object { "
    "[pscustomobject]@{ pid = $_.ProcessId; ppid = $_.ParentProcessId; name = $_.Name; "
    "created = $(if ($null -ne $_.CreationDate) { $_.CreationDate.ToString('o') } else { $null }); "
    "cmd = $_.CommandLine } }; "
    "@($rows) | ConvertTo-Json -Compress -Depth 3"
)
POWERSHELL_ARGV = ["powershell", "-NoProfile", "-NonInteractive", "-Command", POWERSHELL_SCRIPT]

HIF_SCRIPT_ROLES = (
    ("supervisor-loop.ps1", "scheduler"),
    ("wake-supervisor.ps1", "wake"),
    ("hif-worker.ps1", "worker"),
    ("hif-reviewer.ps1", "reviewer"),
)
GLM_AGENT_RE = re.compile(r"--cwd\s+\"?d:\\how_i_fall\\agent(?=[\\\"'\s]|$)")
GLM_ZAGENT_RE = re.compile(r"--cwd\s+\"?d:\\how_i_fall\\zagent(?=[\\\"'\s]|$)")


def iso_utc(epoch=None):
    moment = dt.datetime.fromtimestamp(epoch if epoch is not None else time.time(), dt.timezone.utc)
    return moment.isoformat()


def batch_snapshot(queue_value, state):
    """Компактные durable facts. Это read-only запись receipt, не dispatch gate."""
    output = {"packets": [], "next_task": None, "wait_reason": _optional_str((state or {}).get("next_action"), 600),
              "approval_verified": False, "note": "Receipt показан из queue; только native guard подтверждает право dispatch."}
    records = (queue_value or {}).get("batches", [])
    runtime = (queue_value or {}).get("tasks", [])
    if not isinstance(records, list) or not isinstance(runtime, list):
        output["error"] = "Malformed batch/queue"
        return output
    done = {t.get("id") for t in runtime if isinstance(t, dict) and t.get("status") == "DONE"}
    dispatched = {t.get("id") for t in runtime if isinstance(t, dict)}
    for record in records:
        if not isinstance(record, dict) or not isinstance(record.get("tasks"), list):
            output["error"] = "Malformed batch record"
            continue
        definitions = record["tasks"]
        if not all(isinstance(t, dict) and isinstance(t.get("id"), str) for t in definitions):
            output["error"] = "Malformed definitions"
            continue
        canonical = {k: record.get(k) for k in ("id", "source", "tasks")}
        actual = hashlib.sha256(json.dumps(canonical, sort_keys=True, ensure_ascii=False,
                                         separators=(",", ":")).encode("utf-8")).hexdigest()
        receipt = record.get("approval")
        recorded = (record.get("status") == "APPROVED" and record.get("digest") == actual
                    and isinstance(receipt, dict) and receipt.get("digest") == actual
                    and isinstance(receipt.get("seal"), str) and bool(receipt["seal"]))
        ids = {t["id"] for t in definitions}
        output["packets"].append(dict(id=_optional_str(record.get("id"), 80), total=len(definitions),
                                       done=len(ids & done), pending=len(ids - dispatched),
                                       status="APPROVAL_RECORDED" if recorded else "WAIT_APPROVAL",
                                       digest=actual[:12]))
        if recorded and output["next_task"] is None:
            for task in definitions:
                deps = task.get("dependencies")
                if task["id"] not in dispatched and isinstance(deps, list) and all(isinstance(d, str) for d in deps) and set(deps) <= done:
                    output["next_task"] = task["id"][:80]
                    break
    return output


def load_json_file(path):
    """Чтение UTF-8 (с BOM или без). Ошибка/гонка — явный статус, не пустой успех."""
    try:
        text = Path(path).read_text(encoding="utf-8-sig")
    except FileNotFoundError:
        return None, "файл отсутствует"
    except IsADirectoryError:
        return None, "это каталог, а не файл"
    except (OSError, ValueError) as error:
        return None, "ошибка чтения: " + str(error)[:160]
    try:
        value = json.loads(text)
    except json.JSONDecodeError as error:
        return None, "некорректный JSON (" + str(error)[:120] + ")"
    if not isinstance(value, dict):
        return None, "JSON не является объектом"
    return value, None


def load_control_module(path):
    """Импорт уже установленного hif-control.py; кэш модуля только в памяти."""
    key = os.path.normcase(str(path))
    module = _CONTROL_MODULE_CACHE.get(key)
    if module is not None:
        return module
    spec = importlib.util.spec_from_file_location("hif_control_installed", str(path))
    if spec is None or spec.loader is None:
        raise ImportError("cannot load control module " + str(path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    _CONTROL_MODULE_CACHE[key] = module
    return module


_CONTROL_MODULE_CACHE = {}


def default_quota_rpc(module_path=DEFAULT_CONTROL_MODULE):
    def rpc():
        module = load_control_module(module_path)
        return module.quota_result(module.codex_rpc("account/rateLimits/read", {}))
    return rpc


GLM_QUOTA_PROVIDER_LABEL = "GLM-5.3-Flash (аккаунт Z-Code Coding Plan)"
# Остаток GLM попадает в дашборд только через снимок glm-quota.json, который
# пишет отдельный opt-in хелпер hif-glm-quota.py (свой запрос к
# api.z.ai/api/monitor/usage/quota/limit с coding-plan api-key из хранилища
# ZCode). Сам dashboard процесс учётные данные не читает и хелпер не запускает.
# Список ниже документирует, почему без снимка остаток не показывается.
GLM_QUOTA_CHECKED_SOURCES = (
    {"name": "glm-quota.json (хелпер hif-glm-quota.py)", "result": "основной источник",
     "reason": "снимок авторитетного API; пишется пользователем/планировщиком вручную"},
    {"name": "app-server usage/stats", "result": "не остаток",
     "reason": "локальная статистика расхода запросов (agent-db), а не остаток подписки"},
    {"name": "app-server session/usage", "result": "не остаток",
     "reason": "расход одной сессии, а не остаток подписки"},
    {"name": "~/.zcode/v2/coding-plan-cache.json", "result": "не остаток",
     "reason": "только статусы доступности планов, без остатка и времени сброса"},
)


def glm_quota_unknown(level="unknown", detail=None, error=None):
    detail = detail or ("Остаток квоты GLM неизвестен: свежего снимка glm-quota.json нет; "
                        "значение не придумывается и не показывается зелёным. "
                        "Запусти tools/agent-control/hif-glm-quota.py для обновления.")
    return {
        "provider": GLM_QUOTA_PROVIDER_LABEL,
        "available": False,
        "level": level,
        "mode": "UNKNOWN",
        "windows": [],
        "source": "none",
        "source_label": "нет данных",
        "age_seconds": None,
        "updated_at": None,
        "plan_level": None,
        "error": error,
        "detail": detail,
        "limitation": ("Официальный источник остатка Coding Plan "
                       "(api.z.ai/api/monitor/usage/quota/limit) требует api-key из "
                       "хранилища учётных данных ZCode; read-only дашборд сам не читает "
                       "учётные данные — снимок пишет хелпер hif-glm-quota.py."),
        "checked_sources": [dict(source) for source in GLM_QUOTA_CHECKED_SOURCES],
        "request_usage_note": ("Потребление ниже — расход прошлого запуска, НЕ остаток "
                               "подписки; кэшированный ввод не считается свежей тратой."),
    }


def _parse_snapshot_time(value):
    """ISO-время снимка -> aware datetime; None при missing/invalid значении."""
    if not isinstance(value, str) or not value.strip():
        return None
    try:
        moment = dt.datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
    except ValueError:
        return None
    return moment if moment.tzinfo is not None else None


def glm_quota_snapshot(control_dir, now_fn=time.time):
    """Остаток GLM из снимка хелпера glm-quota.json; без свежего снимка — не «норма».

    Свежесть определяется ТОЛЬКО по валидированному updated_at снимка (mtime
    файла свежестью не считается). Missing/invalid/future время, возраст >15
    минут или прошедший сброс окна дают stale даже при QUOTA_SAVE: проценты
    остаются как исторические, текущим остатком они не объявляются.
    """
    fallback_path = Path(control_dir) / "glm-quota.json"
    value, read_error = load_json_file(fallback_path)
    now = now_fn()
    if value is None:
        unknown = glm_quota_unknown()
        if read_error and read_error != "файл отсутствует":
            unknown["error"] = read_error
            unknown["level"] = "error"
            unknown["detail"] = ("Снимок glm-quota.json не читается: " + read_error
                                 + " Остаток неизвестен.")
        return unknown
    if value.get("mode") == "UNKNOWN":
        unknown = glm_quota_unknown(
            level="error",
            detail="Последний запуск хелпера не удался, остаток неизвестен.",
            error=_optional_str(value.get("error"), 200))
        unknown["updated_at"] = _optional_str(value.get("updated_at"), 40)
        return unknown
    windows = normalize_windows(value.get("windows"))
    if not windows:
        return glm_quota_unknown(level="error",
                                 detail="Снимок glm-quota.json без пригодных окон; остаток неизвестен.")
    mode = value.get("mode") if value.get("mode") in ("NORMAL", "QUOTA_SAVE") else None
    moment = _parse_snapshot_time(value.get("updated_at"))
    age = (now - moment.timestamp()) if moment is not None else None
    if mode is None:
        staleness = "Неизвестный режим снимка glm-quota.json; остаток не актуален."
    elif moment is None:
        staleness = "Время снимка glm-quota.json отсутствует или некорректно; остаток не актуален."
    elif age < 0:
        staleness = "Время снимка glm-quota.json в будущем; остаток не актуален."
    elif age > GLM_QUOTA_FILE_STALE_SECONDS:
        staleness = ("Снимок glm-quota.json устарел (возраст %d минут > %d): проценты "
                     "исторические, текущий остаток не утверждается. Запусти хелпер "
                     "hif-glm-quota.py." % (int(age // 60), int(GLM_QUOTA_FILE_STALE_SECONDS // 60)))
    elif any(window["resets_at_epoch"] is not None and window["resets_at_epoch"] <= now
             for window in windows):
        staleness = ("Окно уже сбросилось: использовано в снимке заведомо устарело. "
                     "Запусти хелпер hif-glm-quota.py для обновления.")
    else:
        staleness = None
    if staleness is not None:
        # Устаревание важнее QUOTA_SAVE: «норма» и низкий остаток не утверждаются.
        level = "stale"
        detail = staleness
    elif mode == "QUOTA_SAVE":
        level = "warn"
        detail = ("QUOTA_SAVE: остаток ≤25% в одном из окон — свежий снимок glm-quota.json "
                  "(helper hif-glm-quota.py).")
    else:
        level = "ok"
        detail = ("Снимок из glm-quota.json (helper hif-glm-quota.py, читает api.z.ai с "
                  "coding-plan api-key). Сам дашборд учётные данные не читает.")
    return {
        "provider": GLM_QUOTA_PROVIDER_LABEL,
        "available": True,
        "level": level,
        "mode": mode or "UNKNOWN",
        "windows": windows,
        "source": "helper-file",
        "source_label": "файл glm-quota.json (хелпер hif-glm-quota.py)",
        "age_seconds": round(age, 1) if age is not None and age >= 0 else None,
        "updated_at": _optional_str(value.get("updated_at"), 40),
        "plan_level": _optional_str(value.get("plan_level"), 40),
        "error": None,
        "detail": detail,
        "limitation": None,
        "checked_sources": [],
        "request_usage_note": ("Потребление ниже — расход прошлого запуска, НЕ остаток "
                               "подписки; кэшированный ввод не считается свежей тратой."),
    }


def probe_processes():
    """Фиксированный read-only опрос процессов; любая неудача -> None (unknown)."""
    try:
        result = subprocess.run(
            POWERSHELL_ARGV, capture_output=True, timeout=PROCESS_HELPER_TIMEOUT,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    except (OSError, subprocess.SubprocessError):
        return None
    if result.returncode != 0:
        return None
    try:
        rows = json.loads(result.stdout.decode("utf-8-sig", "replace"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None
    if isinstance(rows, dict):
        rows = [rows]
    if not isinstance(rows, list):
        return None
    return select_hif_processes(rows)


def _row_cmd(row):
    value = row.get("cmd")
    if not isinstance(value, str):
        return ""
    return value.replace("/", "\\").lower()


def _row_pid(row):
    try:
        return int(row.get("pid"))
    except (TypeError, ValueError):
        return None


def select_hif_processes(rows):
    """Отбор известных HIF-процессов; возвращает safe-поля или None при сбое."""
    if rows is None:
        return None
    by_pid = {}
    for row in rows:
        if isinstance(row, dict) and _row_pid(row) is not None:
            by_pid[_row_pid(row)] = row
    selected = {}

    def safe_row(row, role, engine):
        pid = _row_pid(row)
        if pid is None:
            return
        created = row.get("created")
        selected[pid] = {"pid": pid, "role": role, "engine": engine,
                         "created_at": created if isinstance(created, str) else None}

    # Первый проход: известные скрипты (предки могут идти после потомков).
    for row in rows:
        if not isinstance(row, dict) or _row_pid(row) is None or _row_pid(row) in selected:
            continue
        cmd = _row_cmd(row)
        if str(row.get("name") or "").lower() not in ("powershell.exe", "pwsh.exe"):
            continue
        match = re.search(r"(?:^|\s)-(?:file|f)\s+['\"]?d:\\how_i_fall\\agent-control\\([a-z-]+\.ps1)(?=['\"\s]|$)", cmd)
        if not match or re.search(r"(?:^|\s)-(?:command|c|encodedcommand|ec)\b", cmd[:match.start()]):
            continue
        role = dict(HIF_SCRIPT_ROLES).get(match.group(1))
        if role == "worker":
            if re.search(r"-engine\s+[\"']?zcode", cmd):
                safe_row(row, "writer_glm", "GLM")
            else:
                safe_row(row, "writer_codex", "Codex")
        elif role == "reviewer":
            safe_row(row, "reviewer_strong" if "-strong" in cmd else "reviewer", "Codex")
        elif role is not None:
            safe_row(row, role, None)

    # Второй проход: native codex.exe только при ancestry от HIF-процесса.
    for row in rows:
        if not isinstance(row, dict):
            continue
        pid = _row_pid(row)
        if pid is None or pid in selected:
            continue
        if str(row.get("name") or "").lower() != "codex.exe":
            continue
        ancestor = _ancestor_role(by_pid, selected, row.get("ppid"), 0)
        if ancestor is not None:
            safe_row(row, ancestor, "Codex")

    # Третий проход: zcode.cjs только с --cwd D:\How_I_Fall\agent или zagent.
    for row in rows:
        if not isinstance(row, dict):
            continue
        pid = _row_pid(row)
        if pid is None or pid in selected:
            continue
        cmd = _row_cmd(row)
        if str(row.get("name") or "").lower() != "node.exe" or "zcode.cjs" not in cmd or "--cwd" not in cmd:
            continue
        if GLM_AGENT_RE.search(cmd):
            safe_row(row, "writer_glm", "GLM")
        elif GLM_ZAGENT_RE.search(cmd):
            safe_row(row, "glm_support", "GLM")
    return sorted(selected.values(), key=lambda item: (item["role"], item["pid"]))


def _ancestor_role(by_pid, selected, pid, depth):
    if depth > 12 or pid is None:
        return None
    row = by_pid.get(pid)
    if row is None:
        return None
    own = selected.get(pid)
    if own is not None:
        return own["role"]
    return _ancestor_role(by_pid, selected, row.get("ppid"), depth + 1)


def task_group(status):
    value = str(status or "").upper()
    if not value:
        return "unknown"
    if value in ("DONE", "MERGED", "MERGED_LOCAL_SYNC_DONE", "ROADMAP_SYNC_READY") or value.startswith("DONE"):
        return "done"
    if "REVIEW" in value or "CANDIDATE" in value or "WAIT_CI" in value or value.endswith("_CI"):
        return "review"
    if "BLOCK" in value or "NEEDS_CORRECTION" in value or "ERROR" in value or "FAILED" in value or "FATAL" in value:
        return "error"
    if ("RUNNING" in value or "IN_PROGRESS" in value or "DISPATCH" in value
            or "SYNC_MASTER_PENDING" in value or "WORKING" in value):
        return "running"
    if value.startswith("WAIT") or "PENDING" in value or "QUEUED" in value or "READY" in value or "PROPOS" in value:
        return "waiting"
    return "waiting"


GROUP_HINTS = {
    "running": "выполняется writer'ом",
    "review": "ожидает независимого ревью / CI",
    "waiting": "ожидает диспетчера или пользователя",
    "error": "требует коррекции",
    "done": "завершена в очереди",
    "unknown": "неизвестный статус",
}


def short_sha(value, size=10):
    if isinstance(value, str) and re.fullmatch(r"[a-fA-F0-9]{40}", value):
        return value[:size]
    return value if isinstance(value, str) else None


def _optional_str(value, limit=200):
    return value[:limit] if isinstance(value, str) and value else (value if isinstance(value, str) else None)


def classify_event(event):
    """Один транспортный event -> безопасная подпись без команд/вывода."""
    if not isinstance(event, dict):
        return None
    etype = event.get("type")
    item = event.get("item") if isinstance(event.get("item"), dict) else {}
    if etype == "turn.started":
        return {"label": "Шаг запущен", "level": "info"}
    if etype == "turn.completed":
        return {"label": "Шаг завершён", "level": "ok"}
    if etype == "turn.failed":
        return {"label": "Шаг завершился ошибкой", "level": "error"}
    if etype == "error":
        return {"label": "Ошибка транспорта", "level": "error"}
    if etype not in ("item.started", "item.completed", "item.updated"):
        return None
    itype = item.get("type")
    status = item.get("status")
    if itype == "command_execution":
        # Команда не раскрывается: только общая подпись проверки/команды.
        command = item.get("command")
        check = isinstance(command, str) and any(token in command.lower() for token in
                ("test_control.py", "test_dashboard.py", "test_batch.py", "unittest", "--check", "parsefile", "run-unity-tests.ps1"))
        finished = etype == "item.completed" or status == "completed"
        failed = finished and isinstance(item.get("exit_code"), int) and item["exit_code"] != 0
        label = ("Проверки" if check else "Команда") + (" завершены" if finished else " выполняются")
        if failed:
            label += " с ошибкой"
        level = "error" if failed else ("ok" if finished and item.get("exit_code") == 0 else "info")
        return {"label": label, "level": level}
    if itype == "file_change":
        names = _item_file_names(item)
        detail = ", ".join(names) if names else None
        return {"label": "Изменение файлов", "detail": detail, "level": "info"}
    if itype == "agent_message":
        return {"label": "Сообщение агента", "level": "info"}
    if itype == "reasoning":
        return None
    if isinstance(itype, str) and itype:
        return {"label": "Шаг: " + itype[:40], "level": "info"}
    return None


def _item_file_names(item):
    names = []
    for key in ("changes", "paths", "files"):
        value = item.get(key)
        if isinstance(value, list):
            for entry in value:
                if isinstance(entry, str) and entry:
                    names.append(Path(entry.replace("\\", "/")).name)
                elif isinstance(entry, dict) and isinstance(entry.get("path"), str):
                    names.append(Path(entry["path"].replace("\\", "/")).name)
    if isinstance(item.get("path"), str) and item["path"]:
        names.append(Path(item["path"].replace("\\", "/")).name)
    ordered = []
    for name in names:
        if name and name not in ordered:
            ordered.append(name)
    return ordered[:3]


PROGRESS_STEPS = ("Подготовка", "Реализация", "Локальные проверки",
                  "Независимое ревью", "PR и CI", "Merge и синхронизация", "Roadmap")
PROGRESS_PHASE = {
    "READY": 0, "PREPARING": 0, "RUNNING": 1, "PARTIAL_RETRY": 1,
    "CORRECTION_READY": 1, "REVIEW_CANDIDATE": 3, "REVIEW_CANDIDATE_NO_CHANGE": 3,
    "WAIT_STRONG_REVIEW": 3, "REVIEWING": 3, "STRONG_REVIEWING": 3,
    "REVIEW_READY": 3, "STRONG_REVIEW_READY": 3, "WAIT_CI": 4,
    "SYNC_MASTER_PENDING": 5, "MERGED_LOCAL_SYNC_DONE": 6, "ROADMAP_SYNC_READY": 6,
}


def progress_snapshot(state, tasks, events, now):
    """Discrete pipeline milestones, NOT code completion or a predicted ETA."""
    state = state if isinstance(state, dict) else {}
    tasks = tasks if isinstance(tasks, list) else []
    ident = state.get("active_task_id")
    selected = [t for t in tasks if isinstance(t, dict) and t.get("id") == ident]
    out = {"active": bool(ident), "task_id": _optional_str(ident, 80),
           "title": None, "phase": "Нет активной задачи", "percent": None,
           "completed_steps": None, "total_steps": len(PROGRESS_STEPS),
           "remaining_steps": [], "started_epoch": None, "elapsed_seconds": None,
           "elapsed_source": None, "last_activity_age_seconds": None,
           "last_action": None, "eta_seconds": None,
           "eta_note": "Прогноз времени неизвестен: истории сопоставимых задач пока недостаточно.",
           "disclaimer": "Шкала этапов приблизительная: это НЕ процент готовности кода. Этапы занимают разное время; исправление может вернуть задачу назад."}
    if not ident or len(selected) != 1:
        if ident:
            out["phase"] = "Состояние задачи неоднозначно"
        return out
    out["title"] = _optional_str(selected[0].get("title"), 200) or out["task_id"]
    status = state.get("status")
    phase = PROGRESS_PHASE.get(status)
    if status == "DONE":
        out.update(phase="Завершено", percent=100, completed_steps=len(PROGRESS_STEPS))
    elif phase is not None:
        out.update(phase=PROGRESS_STEPS[phase], percent=round(phase * 100 / len(PROGRESS_STEPS)),
                   completed_steps=phase, remaining_steps=list(PROGRESS_STEPS[phase:]))
        if status in ("REVIEW_CANDIDATE", "REVIEW_CANDIDATE_NO_CHANGE", "WAIT_STRONG_REVIEW"):
            out["phase"] = "Ожидание независимого ревью"
        elif status == "WAIT_CI":
            out["phase"] = "Ожидание PR / CI"
    else:
        # Do not synthesize green progress for blockers, auth waits or unknown statuses.
        out["phase"] = "Ожидание / остановка: " + str(status or "UNKNOWN")[:60]
    if isinstance(events, dict):
        age = events.get("age_seconds")
        if isinstance(age, (int, float)) and age >= 0:
            out["last_activity_age_seconds"] = age
        step = events.get("last_step")
        if isinstance(step, dict):
            out["last_action"] = _optional_str(step.get("label"), 120)
        # The existing host log name is a bounded timestamped execution artifact.
        # Its timestamp is local host time. It is not a duration prediction.
        name = events.get("name")
        match = re.fullmatch(r"(\d{8}-\d{6})-" + re.escape(str(ident)) + r"-events\.jsonl", name or "")
        if match:
            try:
                start = dt.datetime.strptime(match[1], "%Y%m%d-%H%M%S").timestamp()
                if 0 <= now - start <= 30 * 86400:
                    out.update(started_epoch=start, elapsed_seconds=round(now-start),
                               elapsed_source="Время начала текущего прохода из host log; не весь срок задачи с повторами")
            except (ValueError, OverflowError, OSError):
                pass
    return out


def classify_tail(text):
    """Последняя осмысленная подпись в ограниченном хвосте событий."""
    last = None
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        signature = classify_event(event)
        if signature is not None:
            last = signature
    return last


def sanitize_usage(usage):
    """Только числовые счётчики конверта; никаких текстов/промптов."""
    allowed = ("inputTokens", "outputTokens", "totalTokens", "cacheReadTokens",
               "cacheWriteTokens", "reasoningTokens", "modelRequestCount")
    result = {}
    for key in allowed:
        value = usage.get(key)
        if isinstance(value, bool):
            continue
        if isinstance(value, (int, float)):
            result[key] = int(value)
    return result or None


def extract_envelope_usage(text):
    """Последний Z-Code-конверт с usage (ключи totalTokens*) в хвосте файла."""
    starts = [match.start() for match in re.finditer(r"\{", text)]
    decoder = json.JSONDecoder()
    for start in reversed(starts[-400:]):
        try:
            value, _ = decoder.raw_decode(text[start:])
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict) and isinstance(value.get("usage"), dict):
            usage = value["usage"]
            if isinstance(usage.get("totalTokens"), (int, float)):
                return sanitize_usage(usage)
    return None


class Dashboard:
    """Сборка снимка состояния; все источники — внедряемые, только чтение."""

    def __init__(self, control_dir=DEFAULT_CONTROL_DIR, html_path=DEFAULT_HTML_PATH,
                 process_probe=None, quota_rpc=None,
                 quota_rpc_module_path=DEFAULT_CONTROL_MODULE,
                 now_fn=time.time, inline_probes=False):
        self.control_dir = Path(control_dir)
        self.logs_root = self.control_dir / "logs"
        self.evidence_root = self.control_dir / "evidence"
        self.logs_root_resolved = self.logs_root.resolve()
        self.evidence_root_resolved = self.evidence_root.resolve()
        self.receipt_path = self.evidence_root / "dashboard-glm-run.json"
        self.quota_fallback_path = self.control_dir / "codex-quota.json"
        self.html_path = Path(html_path)
        self.process_probe = process_probe or probe_processes
        if quota_rpc is None:
            quota_rpc = default_quota_rpc(quota_rpc_module_path)
        self.quota_rpc = quota_rpc
        self.now_fn = now_fn
        self.inline_probes = inline_probes
        self._quota_lock = threading.Lock()
        self._quota_state = {"value": None, "error": None, "fetched_at": None,
                             "attempted_at": None, "inflight": False}
        self._process_lock = threading.Lock()
        self._process_state = {"value": None, "attempted_at": None, "inflight": False}
        self._glm_usage_lock = threading.Lock()
        self._glm_usage_cache = {"checked_at": None, "value": None}

    # ---------- probes (throttled, single-flight) ----------

    def processes_cached(self):
        now = self.now_fn()
        due = False
        with self._process_lock:
            state = self._process_state
            due = (state["attempted_at"] is None
                   or now - state["attempted_at"] >= PROCESS_POLL_MIN_INTERVAL) and not state["inflight"]
            if due:
                state["inflight"] = True
                state["attempted_at"] = now
                if not self.inline_probes:
                    threading.Thread(target=self._process_worker, daemon=True).start()
        if due and self.inline_probes:
            self._process_worker()
        with self._process_lock:
            state = self._process_state
            if state["value"] is not None:
                transport = "ok"
            elif state["inflight"]:
                transport = "pending"
            else:
                transport = "error"
            return {"value": state["value"], "transport": transport}

    def _process_worker(self):
        try:
            value = self.process_probe()
            if value is not None and not isinstance(value, list):
                value = None
        except Exception:
            value = None
        with self._process_lock:
            self._process_state["value"] = value
            self._process_state["inflight"] = False

    def quota_snapshot(self):
        now = self.now_fn()
        due = False
        with self._quota_lock:
            state = self._quota_state
            due = (state["attempted_at"] is None
                   or now - state["attempted_at"] >= QUOTA_REFRESH_MIN_INTERVAL) and not state["inflight"]
            if due:
                state["inflight"] = True
                state["attempted_at"] = now
                if not self.inline_probes:
                    threading.Thread(target=self._quota_refresh_worker, daemon=True).start()
        if due and self.inline_probes:
            self._quota_refresh_worker()
        with self._quota_lock:
            memory_value = self._quota_state["value"]
            memory_error = self._quota_state["error"]
            fetched_at = self._quota_state["fetched_at"]
            refreshing = self._quota_state["inflight"]
        return self._compose_quota(memory_value, memory_error, fetched_at, refreshing, now)

    def _quota_refresh_worker(self):
        try:
            value = self.quota_rpc()
            if not isinstance(value, dict):
                raise ValueError("quota_result вернул не объект")
            with self._quota_lock:
                self._quota_state["value"] = value
                self._quota_state["error"] = None
                self._quota_state["fetched_at"] = self.now_fn()
        except Exception as error:
            text = str(error).strip()[:300] or error.__class__.__name__
            with self._quota_lock:
                self._quota_state["error"] = text
        finally:
            with self._quota_lock:
                self._quota_state["inflight"] = False

    def _compose_quota(self, memory_value, memory_error, fetched_at, refreshing, now):
        fallback_value, fallback_error = load_json_file(self.quota_fallback_path)
        active = None
        source = "none"
        source_label = "нет данных"
        age = None
        error = memory_error
        if memory_value is not None and memory_value.get("mode") != "UNKNOWN":
            active = memory_value
            source = "live" if not memory_error and fetched_at is not None and now - fetched_at <= QUOTA_FRESH_TTL else "cache"
            source_label = ("живой RPC account/rateLimits/read" if source == "live"
                            else "память последнего успешного RPC")
            age = max(0.0, now - fetched_at) if fetched_at is not None else None
        elif isinstance(fallback_value, dict) and fallback_value.get("mode") != "UNKNOWN":
            active = fallback_value
            source = "fallback"
            source_label = "файл codex-quota.json (fallback, не живое чтение)"
            try:
                age = max(0.0, now - self.quota_fallback_path.stat().st_mtime)
            except OSError:
                age = None
        windows = normalize_windows((active or {}).get("windows"))
        mode = (active or {}).get("mode") if isinstance((active or {}).get("mode"), str) else None
        if active is None:
            scheduler_error = None
            if isinstance(fallback_value, dict) and fallback_value.get("mode") == "UNKNOWN":
                scheduler_error = _optional_str(fallback_value.get("error"), 200)
            level = "error" if (error or scheduler_error) else "unknown"
            detail = "Свежее чтение недоступно, пригодного fallback-файла нет; остаток неизвестен."
            if fallback_value is None and fallback_error:
                detail += " Fallback: " + fallback_error + "."
            if scheduler_error:
                detail = "Последний scheduler-запрос квоты не удался: " + scheduler_error
            windows = []
            mode = "UNKNOWN"
        else:
            if source in ("fallback", "cache") or error:
                level = "stale"
                detail = ("Значение из fallback-файла; живое чтение ещё не выполнялось "
                          "или не удалось. Не «green».")
            else:
                level = "warn" if mode == "QUOTA_SAVE" else "ok"
                detail = ("QUOTA_SAVE: остаток ≤25% в одном из окон — новые задачи уходят в "
                          "GLM-контур." if mode == "QUOTA_SAVE"
                          else "Живое/кэшированное значение лимитов Codex-аккаунта.")
        return {
            "provider": "Codex (аккаунт GPT)",
            "available": active is not None,
            "windows": windows,
            "mode": mode or "UNKNOWN",
            "source": source,
            "source_label": source_label,
            "age_seconds": round(age, 1) if age is not None else None,
            "updated_at": (active or {}).get("updated_at"),
            "error": error,
            "refreshing": refreshing,
            "level": level,
            "detail": detail,
        }

    # ---------- event tails ----------

    def _path_allowed(self, resolved):
        candidate = os.path.normcase(str(resolved))
        roots = [os.path.normcase(str(root)) for root in
                 (self.logs_root_resolved, self.evidence_root_resolved)]
        return any(candidate == root or candidate.startswith(root + os.sep) for root in roots)

    def _read_tail_text(self, target):
        stat = target.stat()
        size = stat.st_size
        with open(target, "rb") as handle:
            # BOM остаётся в начале большого лога, а не в bounded tail.
            # Native Windows PowerShell Tee-Object пишет UTF-16LE; Z-Code — UTF-8.
            prefix = handle.read(4)
            if prefix.startswith(b"\xff\xfe") or (len(prefix) == 4 and prefix[1::2] == b"\x00\x00"):
                encoding = "utf-16-le"
            elif prefix.startswith(b"\xfe\xff"):
                encoding = "utf-16-be"
            else:
                encoding = "utf-8"
            start = max(0, size - EVENT_TAIL_MAX_BYTES)
            if encoding.startswith("utf-16"):
                # Округляем вверх: не разрываем code unit и не превышаем cap,
                # даже при нечётном cap/незавершённой записи последнего байта.
                start += start % 2
            handle.seek(start)
            data = handle.read(max(0, size - start))
        return data.decode(encoding, "replace").lstrip("\ufeff"), stat

    def read_event_tail(self, events_path):
        meta = {"present": False, "name": None, "size_bytes": None, "age_seconds": None,
                "last_step": None, "usage": None, "error": None}
        if not isinstance(events_path, str) or not events_path.strip():
            meta["error"] = "путь к событиям не задан"
            return meta
        try:
            target = Path(events_path).resolve(strict=True)
        except (OSError, RuntimeError, ValueError):
            meta["error"] = "файл событий недоступен"
            return meta
        if not self._path_allowed(target):
            meta["error"] = "путь вне разрешённых каталогов (logs/evidence)"
            return meta
        try:
            text, stat = self._read_tail_text(target)
        except OSError:
            meta["error"] = "ошибка чтения файла событий"
            return meta
        meta["present"] = True
        meta["name"] = target.name
        meta["size_bytes"] = stat.st_size
        meta["age_seconds"] = round(max(0.0, self.now_fn() - stat.st_mtime), 1)
        meta["last_step"] = classify_tail(text)
        meta["usage"] = extract_envelope_usage(text)
        return meta

    # ---------- sections ----------

    def support_snapshot(self, processes):
        out = {"receipt_present": False, "receipt_error": None, "status": "absent",
               "status_label": "нет квитанции — GLM-поддержка не зафиксирована",
               "task_id": None, "title": None, "engine": None, "model": None,
               "started_at": None, "branch": None, "base_sha": None, "pid": None,
               "pid_alive": None, "note": None, "events_path": None, "report_name": None,
               "live_process_detected": False,
               "disclaimer": ("Квитанция — только свидетельство активности поддержки. "
                              "GLM-поддержка не является второй производственной очередью "
                              "и не управляет выбором задач.")}
        rows = processes.get("value") if isinstance(processes, dict) else None
        out["live_process_detected"] = bool(rows) and any(
            row.get("role") in ("glm_support", "writer_glm") for row in rows)
        receipt, error = load_json_file(self.receipt_path)
        if receipt is None:
            out["receipt_error"] = error
            return out
        out["receipt_present"] = True
        for key in ("task_id", "title", "engine", "model", "started_at", "branch", "note"):
            value = receipt.get(key)
            out[key] = _optional_str(value, 200) if isinstance(value, str) else value
        out["base_sha"] = short_sha(receipt.get("base_sha"))
        out["events_path"] = receipt.get("events_path") if isinstance(receipt.get("events_path"), str) else None
        report = receipt.get("report_path")
        out["report_name"] = Path(report.replace("\\", "/")).name if isinstance(report, str) and report else None
        pid = receipt.get("pid")
        out["pid"] = pid if isinstance(pid, int) else None
        status_raw = str(receipt.get("status") or "").upper()
        if rows is None:
            out["pid_alive"] = None
        elif isinstance(pid, int):
            out["pid_alive"] = any(row.get("pid") == pid for row in rows)
        else:
            out["pid_alive"] = False
        if status_raw in SUPPORT_RUNNING_STATUSES:
            if out["pid_alive"] is None:
                out["status"] = "unknown"
                out["status_label"] = "неизвестно: транспорт процессов недоступен"
            elif out["pid_alive"]:
                relevant = any(row.get("pid") == pid and row.get("role") in ("glm_support", "writer_glm")
                               for row in rows or [])
                if relevant:
                    process = next(row for row in rows if row.get("pid") == pid)
                    try:
                        created = dt.datetime.fromisoformat(str(process.get("created_at")).replace("Z", "+00:00"))
                        expected_text = receipt.get("process_created_at") or receipt.get("started_at")
                        expected = dt.datetime.fromisoformat(str(expected_text).replace("Z", "+00:00"))
                        if created.tzinfo is None or expected.tzinfo is None:
                            raise ValueError("missing timezone")
                        delta = (created - expected).total_seconds()
                        identity_match = abs(delta) <= .001 if receipt.get("process_created_at") else 0 <= delta <= 5
                    except (TypeError, ValueError):
                        identity_match = None
                    if identity_match:
                        out["status"] = "running"
                        out["status_label"] = "выполняется (PID и время запуска подтверждены)"
                    else:
                        out["status"] = "unknown" if identity_match is None else "interrupted"
                        out["status_label"] = "PID найден, но идентичность запуска не подтверждена; старая квитанция не считается работающей"
                else:
                    out["status"] = "unknown"
                    out["status_label"] = ("квитанция активна, процесс найден, но не похож на "
                                           "GLM-процесс — статус неизвестен")
            else:
                out["status"] = "interrupted"
                out["status_label"] = "квитанция помечена активной, но процесс не найден — поддержка прервана"
        else:
            out["status"] = status_raw.lower() or "unknown"
            out["status_label"] = {"SUPPORT_REVIEW_CANDIDATE": "GLM закончила — проверяется результат",
                                   "SUPPORT_LOCAL_VERIFIED": "GLM закончила — локальные проверки пройдены",
                                   "SUPPORT_BLOCKED": "GLM остановлена: есть блокер"}.get(status_raw, "статус квитанции: " + (status_raw or "не указан"))
        return out

    def agents_snapshot(self, processes, support):
        rows = processes.get("value") if isinstance(processes, dict) else None
        transport = processes.get("transport") if isinstance(processes, dict) else "error"

        def pick(*roles):
            if rows is None:
                return None
            return next((row for row in rows if row.get("role") in roles), None)

        def status_for(row):
            if transport != "ok":
                return "unknown"
            return "running" if row is not None else "stopped"

        labels = {"running": "процесс работает", "stopped": "не запущен",
                  "unknown": "неизвестно (нет данных о процессах)"}
        scheduler_row = pick("scheduler")
        supervisor_row = pick("wake")
        writer_row = pick("writer_codex")
        reviewer_row = pick("reviewer", "reviewer_strong")
        glm_rows = [] if rows is None else [row for row in rows if row.get("role") in ("glm_support", "writer_glm")]
        glm_row = glm_rows[0] if glm_rows else None
        glm_status = "unknown" if transport != "ok" else ("running" if glm_rows else "stopped")
        if glm_status == "stopped" and support.get("status") == "running":
            glm_status = "unknown"
        cards = [
            {"id": "scheduler", "title": "Внешний scheduler (Supervisor)",
             "status": status_for(scheduler_row), "status_label": labels[status_for(scheduler_row)],
             "process": scheduler_row,
             "hints": ["Один scheduler будит supervisor по таймеру; живой таймер не означает, "
                       "что GPT прямо сейчас обрабатывает задачу."]},
            {"id": "supervisor", "title": "Диспетчер (Supervisor)",
             "status": status_for(supervisor_row), "status_label": labels[status_for(supervisor_row)],
             "process": supervisor_row,
             "hints": ["Активная сессия диспетчера показана отдельно от постоянно живого таймера."]},
            {"id": "sol_writer", "title": "Sol writer (Codex)",
             "status": status_for(writer_row), "status_label": labels[status_for(writer_row)],
             "process": writer_row,
             "hints": ["hif-worker.ps1 -Engine Codex или codex.exe из его процесса."]},
            {"id": "glm", "title": "GLM writer / поддержка (Z-Code)",
             "status": glm_status,
             "status_label": labels.get(glm_status, glm_status),
             "process": glm_row,
             "hints": ["zcode.cjs с --cwd D:\\How_I_Fall\\agent или zagent; подтверждается "
                       "квитанцией поддержки."]},
            {"id": "reviewer", "title": "Независимый reviewer",
             "status": status_for(reviewer_row), "status_label": labels[status_for(reviewer_row)],
             "process": reviewer_row,
             "hints": ["hif-reviewer.ps1 (Luna) / -Strong (Sol High); запускается по событию, "
                       "отсутствие процесса — норма."]},
        ]
        return {"transport": transport, "cards": cards, "processes": rows or []}

    def extract_tasks(self, queue_value, queue_error, state_value):
        if queue_value is None:
            return [], (queue_error or "очередь недоступна")
        raw = queue_value.get("tasks")
        if not isinstance(raw, list):
            return [], "queue.tasks отсутствует или не является списком"
        active_id = state_value.get("active_task_id") if isinstance(state_value, dict) else None
        next_action = state_value.get("next_action") if isinstance(state_value, dict) else None
        last_error = state_value.get("last_error") if isinstance(state_value, dict) else None
        tasks = []
        for item in raw:
            if not isinstance(item, dict):
                continue
            status = item.get("status") if isinstance(item.get("status"), str) else None
            group = task_group(status)
            pr = item.get("pr_number")
            updated = item.get("last_run_finished_at") or item.get("last_run_started_at")
            task = {
                "id": _optional_str(item.get("id"), 120),
                "title": _optional_str(item.get("title"), 220),
                "status": status,
                "group": group,
                "engine": _optional_str(item.get("writer_engine"), 40),
                "model": _optional_str(item.get("model"), 60),
                "branch": _optional_str(item.get("branch"), 160),
                "pr_number": pr if isinstance(pr, int) else None,
                "head_sha": short_sha(item.get("head_sha")),
                "base_sha": short_sha(item.get("base_sha")),
                "risk": _optional_str(item.get("risk"), 20),
                "player_facing": bool(item.get("player_facing")),
                "is_active": isinstance(active_id, str) and item.get("id") == active_id,
                "updated_at": _optional_str(updated, 40),
                "blocker": _optional_str(item.get("blocked_reason") or item.get("next_action"), 600) or GROUP_HINTS.get(group),
            }
            if task["is_active"]:
                if isinstance(next_action, str) and next_action:
                    task["blocker"] = next_action[:600]
                if isinstance(last_error, str) and last_error:
                    task["last_error"] = last_error[:300]
            tasks.append(task)
        tasks.sort(key=lambda task: (GROUP_ORDER.index(task["group"]) if task["group"] in GROUP_ORDER else 99,
                                     str(task["id"])))
        return tasks, None

    def glm_idle_reasons(self, tasks, state_value, support_running, production_running=False):
        if support_running or production_running:
            return {"support_running": support_running, "production_running": production_running, "reasons": []}
        reasons = []
        active = next((task for task in tasks if task.get("is_active")), None)
        if active is not None and active.get("group") in ("running", "review") and active.get("engine") not in (None, "GLM", "ZCode"):
            reasons.append("Производственная очередь последовательна: активна одна задача "
                           "«%s» (движок %s); параллельной производственной GLM-задачи нет."
                           % (active.get("title") or active.get("id") or "?", active.get("engine")))
        open_tasks = [task for task in tasks if task.get("group") in ("waiting", "running", "review")]
        if not open_tasks:
            reasons.append("Нет готовой утверждённой работы: в очереди нет активных задач; "
                           "новые появляются только через предложения и WAIT_USER.")
        elif active is None or active.get("group") not in ("running", "review"):
            waiting_user = [task for task in open_tasks if str(task.get("status") or "").upper().startswith("WAIT")]
            if waiting_user:
                reasons.append("Задачи в очереди ждут действия пользователя/диспетчера; "
                               "назначенной GLM-задачи нет.")
            else:
                reasons.append("Задачи в очереди ещё не назначены writer'у; GLM-задача не назначена.")
        if not reasons:
            if active and active.get("engine") in ("GLM", "ZCode"):
                reasons.append("GLM-задача назначена, но writer сейчас не запущен: результат ждёт ревью/CI или возобновления прохода.")
            else:
                reasons.append("Активная производственная GLM-задача не назначена.")
        return {"support_running": support_running, "production_running": production_running, "reasons": reasons}

    def _find_latest_glm_usage(self):
        candidates = []
        for root in (self.logs_root, self.evidence_root):
            try:
                candidates.extend(root.glob("*.jsonl"))
            except OSError:
                continue
        if self.receipt_path.exists():
            candidates.append(self.receipt_path)
        try:
            candidates.sort(key=lambda path: path.stat().st_mtime, reverse=True)
        except OSError:
            return None
        for path in candidates[:GLM_LEGACY_SCAN_FILES]:
            try:
                text, stat = self._read_tail_text(path)
            except OSError:
                continue
            usage = extract_envelope_usage(text)
            if usage:
                return {"usage": usage, "file": path.name, "age_seconds": round(max(0.0, self.now_fn() - stat.st_mtime), 1)}
        return None

    def glm_usage_snapshot(self, support_events):
        now = self.now_fn()
        with self._glm_usage_lock:
            cache = self._glm_usage_cache
            if cache["checked_at"] is not None and now - cache["checked_at"] < GLM_USAGE_RECHECK_INTERVAL:
                return cache["value"]
        if support_events.get("usage"):
            value = {"usage": support_events["usage"], "file": support_events.get("name"),
                     "age_seconds": support_events.get("age_seconds")}
        else:
            value = self._find_latest_glm_usage()
        with self._glm_usage_lock:
            self._glm_usage_cache["checked_at"] = now
            self._glm_usage_cache["value"] = value
        return value

    def snapshot(self):
        now = self.now_fn()
        processes = self.processes_cached()
        quota = self.quota_snapshot()
        controller_value, controller_error = load_json_file(self.control_dir / "controller.json")
        state_value, state_error = load_json_file(self.control_dir / "state.json")
        queue_value, queue_error = load_json_file(self.control_dir / "queue.json")
        support = self.support_snapshot(processes)
        tasks, tasks_error = self.extract_tasks(queue_value, queue_error, state_value)
        task_events = self.read_event_tail(state_value.get("events_path") if isinstance(state_value, dict) else None)
        support_events = self.read_event_tail(support.get("events_path"))
        glm_usage = self.glm_usage_snapshot(support_events)
        groups = {name: 0 for name in GROUP_ORDER}
        for task in tasks:
            groups[task["group"]] = groups.get(task["group"], 0) + 1
        try:
            maintenance = (self.control_dir / "MAINTENANCE").exists()
            stop = (self.control_dir / "STOP").exists()
        except OSError:
            maintenance = stop = False
        return {
            "api": "hif-dashboard/1",
            "generated_at": iso_utc(now),
            "generated_epoch": now,
            "auto_refresh_seconds": 5,
            "controller": {
                "enabled": controller_value.get("enabled") if isinstance(controller_value, dict) else None,
                "scheduler_mode": _optional_str(controller_value.get("scheduler_mode"), 60) if isinstance(controller_value, dict) else None,
                "repo": _optional_str(controller_value.get("repo"), 80) if isinstance(controller_value, dict) else None,
                "native_master_sync_enabled": controller_value.get("native_master_sync_enabled") if isinstance(controller_value, dict) else None,
                "maintenance_marker": maintenance,
                "stop_marker": stop,
                "error": controller_error,
            },
            "state": {
                "status": _optional_str(state_value.get("status"), 60) if isinstance(state_value, dict) else None,
                "active_task_id": _optional_str(state_value.get("active_task_id"), 120) if isinstance(state_value, dict) else None,
                "branch": _optional_str(state_value.get("branch"), 160) if isinstance(state_value, dict) else None,
                "base_sha": short_sha(state_value.get("base_sha")) if isinstance(state_value, dict) else None,
                "head_sha": short_sha(state_value.get("head_sha")) if isinstance(state_value, dict) else None,
                "next_action": state_value.get("next_action")[:600] if isinstance(state_value, dict) and isinstance(state_value.get("next_action"), str) else None,
                "last_error": state_value.get("last_error")[:300] if isinstance(state_value, dict) and isinstance(state_value.get("last_error"), str) else None,
                "updated_at": _optional_str(state_value.get("updated_at"), 40) if isinstance(state_value, dict) else None,
                "error": state_error,
            },
            "queue": {
                "updated_at": _optional_str(queue_value.get("updated_at"), 40) if isinstance(queue_value, dict) else None,
                "error": tasks_error,
                "task_count": len(tasks),
            },
            "tasks": tasks,
            "progress": progress_snapshot(state_value, tasks, task_events, now),
            "batch": batch_snapshot(queue_value, state_value),
            "groups": groups,
            "agents": self.agents_snapshot(processes, support),
            "support": support,
            "activity": {
                "task_events": task_events,
                "support_events": support_events,
                "glm_usage": glm_usage,
                "stale_write_note_seconds": STALE_WRITE_NOTE_SECONDS,
            },
            "quota_gpt": quota,
            "quota_glm": glm_quota_snapshot(self.control_dir, self.now_fn),
            "glm_idle": self.glm_idle_reasons(tasks, state_value, support.get("status") == "running",
                                               any(row.get("role") == "writer_glm" for row in processes.get("value") or [])),
        }

    def html_bytes(self):
        return self.html_path.read_bytes()


class DashboardHTTPServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def __init__(self, address, dashboard):
        self.dashboard = dashboard
        super().__init__((address[0], address[1]), DashboardHandler)


def make_server(dashboard, port=0):
    """Только 127.0.0.1; публичный bind не предусмотрен сознательно."""
    return DashboardHTTPServer(("127.0.0.1", port), dashboard)


class DashboardHandler(BaseHTTPRequestHandler):
    server_version = "HIFDashboard/1.0"
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        pass

    def _port(self):
        return self.server.server_address[1]

    def _access_allowed(self):
        host = (self.headers.get("Host") or "").strip().lower()
        port = self._port()
        allowed_hosts = {f"127.0.0.1:{port}", f"localhost:{port}", "127.0.0.1", "localhost"}
        if host not in allowed_hosts:
            return False
        for header in ("Origin", "Referer"):
            value = self.headers.get(header)
            if not value:
                continue
            try:
                parts = urllib.parse.urlsplit(value)
                hostname = (parts.hostname or "").lower()
                if (parts.scheme != "http" or hostname not in ("127.0.0.1", "localhost")
                        or parts.username is not None or parts.password is not None or parts.port != port):
                    return False
            except ValueError:
                return False
        return True

    def _send(self, code, body, content_type, extra=None):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        if code >= 400:
            self.send_header("Connection", "close")
        for key, value in (extra or {}).items():
            self.send_header(key, value)
        self.end_headers()
        self.wfile.write(body)

    def _forbidden(self):
        self._send(403, b'{"error":"forbidden: bad Host/Origin"}', "application/json; charset=utf-8")

    def _method_not_allowed(self):
        self._send(405, b'{"error":"method not allowed: GET only"}',
                   "application/json; charset=utf-8", extra={"Allow": "GET"})

    def do_GET(self):
        try:
            if not self._access_allowed():
                return self._forbidden()
            path = urllib.parse.urlsplit(self.path).path
            if path == "/api/status":
                try:
                    payload = self.server.dashboard.snapshot()
                except Exception as error:
                    body = json.dumps({"error": "ошибка сборки снимка: " + str(error)[:200]},
                                      ensure_ascii=False)
                    return self._send(500, body.encode("utf-8"), "application/json; charset=utf-8")
                body = json.dumps(payload, ensure_ascii=False)
                return self._send(200, body.encode("utf-8"), "application/json; charset=utf-8")
            if path == "/health":
                body = json.dumps({"ok": True, "time": iso_utc()}, ensure_ascii=False)
                return self._send(200, body.encode("utf-8"), "application/json; charset=utf-8")
            if path == "/favicon.ico":
                return self._send(200, FAVICON_SVG, "image/svg+xml")
            if path == "/":
                try:
                    body = self.server.dashboard.html_bytes()
                except OSError:
                    return self._send(500, "html file unavailable".encode("utf-8"), "text/plain; charset=utf-8")
                return self._send(200, body, "text/html; charset=utf-8", extra={"Content-Security-Policy": HTML_CSP})
            return self._send(404, b'{"error":"not found"}', "application/json; charset=utf-8")
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_POST(self):
        if not self._access_allowed():
            return self._forbidden()
        self._method_not_allowed()

    do_PUT = do_POST
    do_DELETE = do_POST
    do_PATCH = do_POST
    do_OPTIONS = do_POST
    do_HEAD = do_POST


def normalize_windows(windows):
    if not isinstance(windows, list):
        return []
    result = []
    for window in windows:
        if not isinstance(window, dict) or not isinstance(window.get("usedPercent"), (int, float)):
            continue
        minutes = window.get("windowDurationMins")
        if minutes == 300:
            label = "5-часовое окно"
        elif minutes == 10080:
            label = "Недельное окно"
        elif isinstance(minutes, (int, float)):
            label = "Окно %d мин" % int(minutes)
        else:
            label = "Окно (длительность неизвестна)"
        used = float(window["usedPercent"])
        resets = window.get("resetsAt")
        result.append({
            "label": label,
            "window_minutes": int(minutes) if isinstance(minutes, (int, float)) else None,
            "used_percent": round(used, 1),
            "remaining_percent": round(max(0.0, 100.0 - used), 1),
            "resets_at_epoch": resets if isinstance(resets, (int, float)) else None,
        })
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="HIF read-only agents dashboard (127.0.0.1 only)")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args(argv)
    server = make_server(Dashboard(), port=args.port)
    print("How I Fall dashboard: http://127.0.0.1:%d/ (read-only, Ctrl+C to stop)"
          % server.server_address[1])
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
