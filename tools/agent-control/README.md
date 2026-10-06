# Один локальный контур How I Fall

Код helpers хранится в репозитории (`tools/agent-control`); живое состояние —
`D:\How_I_Fall\agent-control`. Policy: `docs/product/agent_orchestration.md`.
Это не новый agent framework: один существующий Supervisor, один scheduler,
одна очередь, последовательный writer и отдельный свежий reviewer.

## Работа пользователя

Проект Codex **How I Fall**, папка `D:\How_I_Fall` (общая папка контура).
`develop` сейчас защищён из-за пользовательского diff; автоматизация работает только в
`agent`/`zagent`, эталон — чистый `master`. Не открывать отдельные Codex projects
для этих технических папок. Общий `CODEX_HOME=D:\Codex`; bundled CLI приложения,
не старый npm shim. Основной пользовательский чат — Sol 6.1 High.

Обычная команда: «Продолжай How I Fall по утверждённому плану».
При окончании утверждённых задач Supervisor предлагает до трёх product-задач
в `proposals.md`, ставит `WAIT_USER` и ждёт выбора в Codex.

## Runtime

- `supervisor-loop.ps1`: один tick каждые 15 минут, mutex; dispatch по durable
  state. Запускает writer/reviewer снаружи model sandbox, не вложенные model exec.
- `wake-supervisor.ps1`: persistent Luna Low, GitHub/Drive plugins, selection,
  correction, CI, merge, roadmap. Не отдельный второй implementation-agent.
- `hif-worker.ps1 -Engine Codex|ZCode`: одна очередь, общий writer/reviewer lock,
  approved bounded brief, exact expected base, scoped stage, machine-readable
  acceptance. Exit 0 без validation не разрешает commit/push. Partial diff
  сохраняется в исходном checkout; автоматического переноса между engines нет.
- `hif-reviewer.ps1` / `-Strong`: новый независимый read-only Luna Low / Sol High
  для exact head; отчёт привязан к task/base/head. Значимые/runtime/UI/Save и
  automation/workflow задачи сразу идут одному Sol High reviewer. Luna остаётся
  только low-risk reviewer; escalation — исключение при новом риске, не лестница.
- `hif-control.py quota`: свежий `account/rateLimits/read`; ≤25% в любом окне
  направляет новые tasks в Flash Max, включая runtime/UI. Неизвестная квота
  блокирует автоматический выбор. Недоступен Sol reviewer → `WAIT_STRONG_REVIEW`.
- `hif-control.py merge-gate`: свежие public GitHub PR/checks, latest exact-head
  GREEN `CI Gate`, matching clean reviews, неизменный master, graphical proof.
  Supervisor после успеха сразу вызывает GitHub merge plugin с
  `expected_head_sha`; устаревший receipt не является разрешением.
- `hif-control.py sync-master`: native post-merge transport, вызываемый ТОЛЬКО
  внешним scheduler'ом до любого model wake. Fixed checkout `D:\How_I_Fall\master`,
  fixed origin, `controller.native_master_sync_enabled=true`, аргумента repo нет.
  До любых git-записей: один active task identity, exact 40-hex base/head/merge SHA,
  integer PR id, live merged PR с matching head/merge SHA, свежий remote master,
  GREEN exact-head CI, matching CLEAN review нужного уровня. Только чистый
  `master` на task base или expected merge HEAD; после fetch recheck clean/head и
  ff-only exact merge SHA, затем verify HEAD/clean. Пользовательский diff
  сохраняется; reset/clean/stash не выполняются. Успех атомарно переводит
  task/state в `MERGED_LOCAL_SYNC_DONE`/`ROADMAP_SYNC_READY`; уже
  синхронизированный master идемпотентен; persistence failure — fatal exit 78.

`controller.json`: `enabled`, `supervisor_thread_id`, версия runtime/scheduler.
`queue.json`: tasks с `id`, `status`, `approved_source`, `base_sha`, `prompt`,
`allowed_paths`, `validation` (включая acceptance), `risk`, `player_facing`.
`state.json`: `status`, `active_task_id`, durable next action.
Статусы candidate/review/WAIT_CI/WAIT_STRONG_REVIEW не означают `DONE`.
После merge state идёт через `SYNC_MASTER_PENDING` (native scheduler sync,
модельные сессии не выполняют git-записи в `master`) → `ROADMAP_SYNC_READY`:
repository roadmap (`docs/technical_plan.md` — единственный основной
approved-task entrypoint) уже покрыт candidate diff; Drive roadmap —
research/history и необязательное зеркало, stale помечается явно и не блокирует
`DONE` после остальных gates.

`MAINTENANCE` или `STOP` в control запрещает новые wakes; `enabled=false` также
останавливает scheduler. Они не прерывают уже исполняющийся model turn.
`WAIT_USER`/`BLOCKED` молчат до действия пользователя; quota/CI waits можно
перепроверять. Не запускать пустые research loops ради расходования токенов.

Пользователь явно разрешил autonomous merge 2026-10-06. Только процесс HIF
Supervisor получает per-tool `approval_mode=approve` для GitHub `merge_pull_request`
через CLI override; глобальный `config.toml` и другие tools не расширяются.
Это разрешение инструмента, не новый GitHub repository scope: Supervisor по-прежнему
работает только с HIF и требует свежий exact-head merge gate, review и CI.
При `WAIT_AUTH` scheduler не повторяет model wake до ручного восстановления.

Scheduler запускается скрыто из единственного Windows Startup shortcut.
Нужны включённый PC, вошедший пользователь, сеть и доступные аккаунты; это не
облачный сервис. При полной недоступности Codex Supervisor тоже не может работать:
GLM не подменяет управление/сильное ревью; безопасная остановка до восстановления.

## Установка и проверки

После independent acceptance scripts копируются в `agent-control` named paths;
очередь/состояние никогда не перезаписываются массовым копированием.
После обновления policy/scripts заново проверить syntax, fixtures и transport.

```powershell
python D:\How_I_Fall\agent\tools\agent-control\test_control.py
powershell -NoProfile -File D:\How_I_Fall\agent-control\supervisor-loop.ps1 -Once
```

Первый command не обращается к моделям/GitHub. Второй с `MAINTENANCE` — безопасный
no-op; снятие marker разрешено только после acceptance runtime установки.
Тестовые fixtures не доказывают реальный autonomous PR→CI→merge→Drive цикл.
Проверки Unity для инфраструктуры локально обычно `NOT RUN`; workflow классифицирует
`tools` как mixed и требует обычный CI Gate, исключения не добавляются.

## Если master сдвинулся после выбора задачи

`MASTER_MOVED` не означает approval или вечное ожидание. Supervisor ставит
`NEEDS_CORRECTION`, сохраняет старые base/head и в том же bounded task задаёт
reconciliation с новым exact `origin/master`. Git transport готовится снаружи
model turn, только в чистом task checkout: безопасное объединение с новым master,
без reset/clean/force-push и без изменения `develop`. После reconciliation новая
base/head identity записывается в brief; заново выполняются acceptance и свежие
cheap/strong reviews, затем новый exact-head CI. Старые approvals непригодны.
Конфликт вне task scope или unrelated dirty diff — `BLOCKED`, а не скрытый overwrite.

Read-only reviewer не обязан повторно запускать fixtures, создающие временные
файлы: sandbox может запрещать это. Для них writer прикладывает полный локальный
лог с exact head, exit code и SHA-256; reviewer читает его и проверяет тесты/код.
Ошибку записи temp в readonly sandbox не считать production failure, но явно
отмечать независимый rerun `NOT RUN`/environment-blocked, без выдуманного PASS.

Коды helpers: `75` — ожидаемое ожидание квоты; `78` — fatal persistence failure.
При `78` единственный scheduler немедленно завершается даже если запись `STOP`
невозможна. Состояние/marker сохраняются best-effort; повторного dispatch в этом
процессе нет. Обычные transport failures ограничены тремя попытками.
CI poll молчит, пока актуальные classification/Unity/Gate jobs не завершены;
успех предыдущей попытки на том же head не заменяет текущую незавершённую.

## Native GLM runtime/UI acceptance

Это расширение существующего `hif-worker`, не новый scheduler/provider/QA launcher.
У approved Flash brief обязателен `native_validation_profile`: `agent-control`
сохраняет fixed fixtures + все четыре PS AST; `hif-runtime` получает только selection:

```json
{
  "native_validation_profile": "hif-runtime",
  "player_facing": true,
  "native_qa": {
    "unity": [{"mode": "PlayMode", "filter": "SaveLoadFocusOwnershipPlayModeTests"}],
    "graphical": ["GameMenu"]
  }
}
```

Это пример schema, **не утверждение нового product pass**. Состояние реального
GLM runtime/UI rollout после infrastructure acceptance — staged `NOT VERIFIED`.

Whitelisted filters: `EditMode` → `InteractiveHotspotEditModeTests`,
`SavePaginationEditModeTests`; `PlayMode` → `SaveLoadFocusOwnershipPlayModeTests`.
Whitelisted graphical Scenario: `PlayerUi`, `GameMenu`, `SaveBackendV2`.
До шести Unity selections и трёх Scenario, без duplicates. Player-facing обязательно
имеет graphical selection. Ни пустой/all filter, ни regex/method/arg/command/executable,
ни дополнительный ключ в `native_qa` не принимаются. `Smoke`, `ManualSave`, `Hotspot`
пока закрыты: broad/unproven save isolation не обходится ради автономности.
Расширять whitelist можно только отдельным inspected safety diff, не queue string.

`native-plan` закрывает unknown/malformed profile до dispatch. `native-qa` читает
одну active task identity из существующей queue/state, проверяет writer/branch/HEAD,
неизменные approved launchers/isolation harnesses и отсутствие reparse escape.
Native invocation использует fixed Windows PowerShell, fixed script и args;
`UNITY_EDITOR_PATH` override не наследуется. Flash остаётся edit/no-shell без Bash/yolo.
Процесс запускается только в `agent`/`zagent`, никогда `develop`/`master`.

Exit code недостаточен: fresh XML с nonzero passed cases/согласованными totals,
без failures; fresh logs; graphical preflight + sentinel + PlayerPrefs restoration,
все оригиналы existing launcher list и PNG dimensions (обычно 1920x1080, без
`-nographics`). Existing responsive proof 1280x720 сохраняется, новая multi-resolution
автоматизация не добавляется. Изоляция saves переиспользует inspected temp directories
существующих harnesses. Native worker не трогает real LocalLow и не создаёт свой
backup/restore механизм; недоказанная/destructive selection не запускается.

`evidence/<task>-<run>/manifest.json`: task/base/source head, source fingerprint,
engine/path, timestamps, fixed invocations/exit codes, SHA-256 XML/logs/sentinels
и до трёх curated оригиналов на Scenario. Это generated control evidence, не task
PR payload. `bind-native-proof` после native commit проверяет unchanged source
fingerprint и связывает manifest с точным candidate head до push.
Missing/zero/stale proof → false completeness, `BLOCKED`, без stage/commit/push;
implementation test/compile failure → `PARTIAL_RETRY` с correction brief/log.
Не более двух correction попыток, SAME checkout/branch/engine/resume HEAD; drift
или повторный failure → `BLOCKED`, реальные permission/auth failures → `WAIT_AUTH`.
Ни reset/clean/stash, ни partial-diff transfer нет. Quota ≤25% меняет только
следующий task; retry сохраняет исходный engine. Sol flow и `agent-control` сохранены.

Machine `validation_complete=true` не означает objective visual PASS: Flash его не
заявляет. Для GLM image inspection — обязательный независимый Sol High gate.
`native-proof-status` перед reviewer направляет `WAIT_VISUAL_PROOF` в существующий
Supervisor wake; Supervisor публикует bounded evidence по existing review routes и
записывает `native_remote_proof={route,url,head_sha,manifest_sha256}` после readback.
Недоступные routes после попыток → `BLOCKED`, no merge. Reviewer открывает remote
оригиналы; receipt и local archive не заменяют inspection. `merge-gate` повторно
проверяет manifest/hashes, matching canonical CLEAN Sol High, latest exact-head CI
и возвращает `expected_head_sha`. Публикация/PR/merge/native sync — outer transport,
не writer model. Исчерпанный approved product backlog → proposals/`WAIT_USER`.

Fixtures мокируют native invocations и работают в temporary directories с собственными
mutex names; модели, GitHub writes, Unity/user checkout/LocalLow не запускаются.
Полный writer log и head-bound manifest хранить в `agent-control/evidence`; если
writer sandbox не разрешает live control path, использовать gitignored
`QAArtifacts/agent-control/evidence` и передать этот archive outer transport.
