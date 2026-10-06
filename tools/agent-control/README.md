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

`controller.json`: `enabled`, `supervisor_thread_id`, версия runtime/scheduler.
`queue.json`: tasks с `id`, `status`, `approved_source`, `base_sha`, `prompt`,
`allowed_paths`, `validation` (включая acceptance), `risk`, `player_facing`.
`state.json`: `status`, `active_task_id`, durable next action.
Статусы candidate/review/WAIT_CI/WAIT_STRONG_REVIEW не означают `DONE`.
После merge проверяется новый master и синхронизируется repository/Drive roadmap.

`MAINTENANCE` или `STOP` в control запрещает новые wakes; `enabled=false` также
останавливает scheduler. Они не прерывают уже исполняющийся model turn.
`WAIT_USER`/`BLOCKED` молчат до действия пользователя; quota/CI waits можно
перепроверять. Не запускать пустые research loops ради расходования токенов.

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
