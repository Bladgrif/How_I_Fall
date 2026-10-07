"""Опциональный GLM-quota хелпер: обновляет agent-control/glm-quota.json для дашборда.

Один вызов = один запрос: читает coding-plan api-key из хранилища ZCode
(~/.zcode/v2/credentials.json), расшифровывая его ровно так же, как desktop-app
(``enc:v1:`` + AES-256-GCM, ключ = sha256 от ZCODE_CREDENTIAL_SECRET либо
fallback ``zcode-credential-fallback:{platform}:{homedir}:{username}``; AES-GCM
выполняет штатный node из системной установки), запрашивает авторитетный
api.z.ai/api/monitor/usage/quota/limit и атомарно пишет снимок glm-quota.json.

Секреты не печатаются, не логируются и не попадают в файл: stdout содержит
только проценты, счётчики и времена сброса. Сам dashboard процесс не читает
учётные данные и не запускает этот хелпер — его запускает пользователь или
внешний планировщик вручную (симметрично hif-control.py quota для Codex).
"""
import argparse
import base64
import datetime as dt
import getpass
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.error
import urllib.request

CREDENTIALS_PATH = Path.home() / ".zcode/v2/credentials.json"
CREDENTIAL_KEY_PREFIX = "account-provider:coding-plan:account:zai-individual-coding-plan"
CREDENTIAL_KEY_SUFFIX = ":api-key"
QUOTA_URL = "https://api.z.ai/api/monitor/usage/quota/limit"
ENVELOPE_PREFIX = "enc:v1:"
SECRET_ENV_VAR = "ZCODE_CREDENTIAL_SECRET"
DEFAULT_TIMEOUT = 20.0
NODE_FALLBACKS = (
    Path.home() / ".cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe",
)
IV_BYTES = 12
TAG_BYTES = 16
QUOTA_SAVE_THRESHOLD = 25
# Redirects запрещены полностью: urllib иначе передал бы Authorization другому host.
REDIRECT_CODES = (301, 302, 303, 307, 308)


class QuotaHelperError(Exception):
    """Ошибка с фиксированным безопасным текстом и проверенным числовым кодом.

    Текст никогда не содержит API messages, response bodies, исходный текст
    исключений или subprocess stderr: сервер не может использовать его,
    чтобы отразить ключ обратно в stdout/stderr/снимок.
    """

    def __init__(self, message, code=None):
        super().__init__(message)
        self.message = message
        self.code = code if isinstance(code, int) and not isinstance(code, bool) else None


def error_code(error):
    return str(error.code) if isinstance(error, QuotaHelperError) and error.code is not None else "—"


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    """Любой 3xx -> HTTPError без follow-up запроса (redirect_request=None)."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

# Совпадает с вызовом node:crypto в desktop-app; вход/выход через stdin/stdout,
# чтобы секрет не попадал в argv/списки процессов.
NODE_DECRYPT_SCRIPT = (
    "const crypto=require('crypto');"
    "let raw='';process.stdin.on('data',d=>raw+=d).on('end',()=>{"
    "try{const {key,iv,tag,data}=JSON.parse(raw);"
    "const decipher=crypto.createDecipheriv('aes-256-gcm',Buffer.from(key,'hex'),"
    "Buffer.from(iv,'base64url'));"
    "decipher.setAuthTag(Buffer.from(tag,'base64url'));"
    "process.stdout.write(Buffer.concat([decipher.update(Buffer.from(data,'base64url')),"
    "decipher.final()]).toString('utf-8'));"
    "}catch(error){process.stderr.write('decrypt failed');process.exit(1)}});"
)


def resolve_node(explicit=None):
    candidates = []
    if explicit:
        candidates.append(Path(explicit))
    found = shutil.which("node")
    if found:
        candidates.append(Path(found))
    candidates.extend(NODE_FALLBACKS)
    for candidate in candidates:
        if candidate and candidate.is_file():
            return str(candidate)
    return None


def credential_secret(env=None):
    env = os.environ if env is None else env
    from_env = (env.get(SECRET_ENV_VAR) or "").strip()
    if from_env:
        return from_env
    return "zcode-credential-fallback:%s:%s:%s" % (sys.platform, Path.home(), getpass.getuser())


def parse_credential_blob(blob):
    if not isinstance(blob, str) or not blob.startswith(ENVELOPE_PREFIX):
        raise ValueError("credential value is not an encrypted envelope")
    parts = blob[len(ENVELOPE_PREFIX):].split(".")
    if len(parts) != 3:
        raise ValueError("credential envelope must have 3 parts")
    iv = base64.urlsafe_b64decode(parts[0] + "=" * (-len(parts[0]) % 4))
    tag = base64.urlsafe_b64decode(parts[1] + "=" * (-len(parts[1]) % 4))
    data = base64.urlsafe_b64decode(parts[2] + "=" * (-len(parts[2]) % 4))
    if len(iv) != IV_BYTES:
        raise ValueError("credential envelope IV must be 12 bytes")
    if len(tag) != TAG_BYTES:
        raise ValueError("credential envelope auth tag must be 16 bytes")
    return iv, tag, data


def decrypt_credential(blob, node_path, env=None):
    """enc:v1: envelope -> plaintext credential. Ошибка без секрета в тексте."""
    iv, tag, data = parse_credential_blob(blob)
    key = hashlib.sha256(credential_secret(env).encode("utf-8")).hexdigest()
    payload = json.dumps({"key": key, "iv": base64.urlsafe_b64encode(iv).decode("ascii").rstrip("="),
                          "tag": base64.urlsafe_b64encode(tag).decode("ascii").rstrip("="),
                          "data": base64.urlsafe_b64encode(data).decode("ascii").rstrip("=")})
    result = subprocess.run([node_path, "-e", NODE_DECRYPT_SCRIPT], input=payload,
                            capture_output=True, text=True, timeout=30,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode != 0 or not result.stdout:
        raise ValueError("credential decrypt failed (key mismatch or node error)")
    return result.stdout


def load_coding_plan_api_key(credentials_path, node_path, env=None):
    try:
        values = json.loads(Path(credentials_path).read_text(encoding="utf-8-sig"))
    except FileNotFoundError:
        raise QuotaHelperError("хранилище учётных данных ZCode не найдено")
    except (OSError, ValueError):
        raise QuotaHelperError("хранилище учётных данных не читается")
    if not isinstance(values, dict):
        raise QuotaHelperError("хранилище учётных данных не является объектом")
    names = sorted(name for name in values
                   if name.startswith(CREDENTIAL_KEY_PREFIX) and name.endswith(CREDENTIAL_KEY_SUFFIX))
    if not names:
        raise QuotaHelperError("coding-plan api-key не найден в хранилище ZCode")
    try:
        return decrypt_credential(values[names[0]], node_path, env)
    except ValueError:
        raise QuotaHelperError("coding-plan api-key не расшифрован")


def fetch_quota(api_key, timeout=DEFAULT_TIMEOUT):
    request = urllib.request.Request(QUOTA_URL, headers={"authorization": "Bearer " + api_key})
    opener = urllib.request.build_opener(NoRedirectHandler)
    try:
        with opener.open(request, timeout=timeout) as response:
            raw = response.read()
    except urllib.error.HTTPError as error:
        if error.code in REDIRECT_CODES:
            raise QuotaHelperError("redirect квоты запрещён", code=error.code)
        raise QuotaHelperError("HTTP-ошибка квоты", code=error.code)
    except (urllib.error.URLError, TimeoutError, OSError):
        raise QuotaHelperError("эндпоинт квоты недоступен")
    try:
        body = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, ValueError):
        raise QuotaHelperError("ответ квоты не является корректным JSON")
    if not isinstance(body, dict):
        raise QuotaHelperError("ответ квоты не является объектом")
    return body


def describe_limit(limit):
    """Окно -> (window_minutes, label). Известные пары соответствуют плану Z.AI."""
    number, unit = limit.get("number"), limit.get("unit")
    if number == 5 and unit == 3:
        return 300, "5-часовое окно"
    if number == 1 and unit == 6:
        return 10080, "Недельное окно"
    return None, "Окно (unit=%s, number=%s)" % (unit, number)


def quota_result(body, now=None):
    """Авторитетный ответ -> снимок glm-quota.json. Числовые поля валидируются."""
    now = now or dt.datetime.now(dt.timezone.utc)
    code = body.get("code")
    if body.get("success") is False or code not in (None, 0, 200):
        # Только проверенный числовой код: msg сервера может отражать ключ.
        numeric = code if isinstance(code, int) and not isinstance(code, bool) else None
        raise QuotaHelperError("API квоты вернуло бизнес-ошибку", code=numeric)
    data = body.get("data") if isinstance(body.get("data"), dict) else {}
    limits = [limit for limit in (data.get("limits") or []) if isinstance(limit, dict)]
    windows = []
    for limit in limits:
        percentage = limit.get("percentage")
        resets_ms = limit.get("nextResetTime")
        if not isinstance(percentage, (int, float)) or not 0 <= percentage <= 100:
            continue
        minutes, _label = describe_limit(limit)
        windows.append({
            "usedPercent": round(float(percentage), 1),
            "windowDurationMins": minutes,
            "resetsAt": (resets_ms / 1000.0) if isinstance(resets_ms, (int, float)) and resets_ms > 0 else None,
            "remainingCount": limit.get("remaining") if isinstance(limit.get("remaining"), (int, float)) else None,
        })
    windows.sort(key=lambda window: (window["windowDurationMins"] is None,
                                     window["windowDurationMins"] or 0))
    if not windows:
        return {"version": 1, "updated_at": now.isoformat(), "source": QUOTA_URL,
                "mode": "UNKNOWN", "threshold_remaining_percent": QUOTA_SAVE_THRESHOLD,
                "plan_level": data.get("level") if isinstance(data.get("level"), str) else None,
                "windows": [], "error": "ответ API не содержит пригодных окон лимитов"}
    remaining = [max(0.0, 100.0 - window["usedPercent"]) for window in windows]
    return {
        "version": 1,
        "updated_at": now.isoformat(),
        "source": QUOTA_URL,
        "mode": "QUOTA_SAVE" if min(remaining) <= QUOTA_SAVE_THRESHOLD else "NORMAL",
        "threshold_remaining_percent": QUOTA_SAVE_THRESHOLD,
        "plan_level": data.get("level") if isinstance(data.get("level"), str) else None,
        "windows": windows,
        "error": None,
    }


def write_snapshot(control_dir, snapshot):
    target = Path(control_dir) / "glm-quota.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    tmp = target.with_name(target.name + ".tmp")
    tmp.write_text(json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(tmp, target)
    return target


def reset_label(epoch_seconds):
    if not isinstance(epoch_seconds, (int, float)) or epoch_seconds <= 0:
        return "время неизвестно"
    moment = dt.datetime.fromtimestamp(epoch_seconds).astimezone()
    return moment.strftime("%d.%m %H:%M")


def main(argv=None):
    parser = argparse.ArgumentParser(description="Обновить agent-control/glm-quota.json (без вывода секретов)")
    parser.add_argument("--control-dir", default=os.environ.get("HIF_CONTROL_DIR", "D:/How_I_Fall/agent-control"))
    parser.add_argument("--credentials", default=str(CREDENTIALS_PATH))
    parser.add_argument("--node", default=None, help="путь к node (по умолчанию PATH)")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT)
    args = parser.parse_args(argv)

    node_path = resolve_node(args.node)
    if node_path is None:
        print("node не найден: расшифровка credential store невозможна", file=sys.stderr)
        return 4
    try:
        api_key = load_coding_plan_api_key(args.credentials, node_path)
    except QuotaHelperError as error:
        print("Учётные данные недоступны (%s)" % error.message, file=sys.stderr)
        return 2
    try:
        snapshot = quota_result(fetch_quota(api_key, timeout=args.timeout))
    except QuotaHelperError as error:
        print("Запрос квоты не удался (%s, код %s)" % (error.message, error_code(error)),
              file=sys.stderr)
        return 3
    except Exception:
        # Фиксированный текст: исходное исключение может содержать отражённый ключ.
        print("Запрос квоты не удался (неожиданная ошибка)", file=sys.stderr)
        return 3
    target = write_snapshot(args.control_dir, snapshot)
    if snapshot["mode"] == "UNKNOWN":
        print("Снимок записан, но пригодных окон нет: %s" % snapshot.get("error"))
        return 1
    print("План: level=%s · источник: %s" % (snapshot.get("plan_level") or "?", snapshot["source"]))
    for window in snapshot["windows"]:
        remaining = max(0.0, 100.0 - window["usedPercent"])
        count = window.get("remainingCount")
        extra = " · осталось %s" % format(round(count), ",") if isinstance(count, (int, float)) else ""
        print("- остаток %.0f%% (использовано %.0f%%)%s · сброс: %s"
              % (remaining, window["usedPercent"], extra, reset_label(window.get("resetsAt"))))
    print("Снимок: %s" % target)
    return 0


if __name__ == "__main__":
    sys.exit(main())
