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
Утром пользователь утверждает exact digest пакета 1–15 bounded задач из repository roadmap.
До native approval задачи — только proposal в существующем `queue.json`.
Исчерпание пакета разрешает один planning pass следующего пакета и `WAIT_USER`.

## Runtime

- `supervisor-loop.ps1`: один tick каждые 15 минут, mutex; dispatch по durable
  state. Запускает writer/reviewer снаружи model sandbox, не вложенные model exec.
- `wake-supervisor.ps1`: НОВАЯ bounded Luna Low session из durable facts, GitHub/Drive plugins, selection,
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
Supervisor получает per-tool `approval_mode=approve` для GitHub `create_pull_request` и `merge_pull_request`
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
  "player_facing": false,
  "native_qa": {
    "unity": [{"mode": "EditMode", "filter": "InteractiveHotspotEditModeTests"}],
    "graphical": []
  }
}
```

Это пример schema, **не утверждение нового product pass**. Состояние реального
GLM runtime/UI rollout после infrastructure acceptance — staged `NOT VERIFIED`.

Whitelisted filters: только `EditMode` → `InteractiveHotspotEditModeTests`,
`SavePaginationEditModeTests`. До шести Unity selections без duplicates.
`PlayMode` и ВСЕ graphical Scenario (`PlayerUi`, `SaveBackendV2`, `GameMenu`,
`ManualSave`, `Hotspot`) закрыты до доказанной save isolation **перед первым**
runtime access. `SaveManager.Awake` обращается к реальному save root раньше
`ConfigureSaveDirectoryForTests`; поздний override не является startup isolation.
Поэтому player-facing selection сейчас fail closed: `REVIEWER VISUAL PROOF NOT AVAILABLE`,
не запускать Unity и не выдавать candidate с неполным QA. `Smoke` также закрыт.
Ни пустой/all filter, ни regex/method/arg/command/executable, ни дополнительный ключ
в `native_qa` не принимаются. Открытие selections требует отдельного inspected
safety diff с startup isolation и bounded proof affected-state coverage, не queue string.
Это ограничение текущего staged rollout, а не утверждение GLM runtime/UI PASS.

`native-plan` закрывает unknown/malformed profile до dispatch. `native-qa` читает
одну active task identity из существующей queue/state, проверяет writer/branch/HEAD,
неизменные approved launchers/isolation harnesses и `ProjectVersion.txt`, который
определяет installed Editor executable. До первого launcher и перед
каждым следующим проверяются ВСЕ Unity write roots (`Assets`, `Packages`,
`ProjectSettings`, `Library`, `Temp`, `Logs`, `UserSettings`, `obj`, `.vs`, `QAArtifacts`),
их существующие поддеревья, root-level entries (включая IDE project files), selected
outputs/sentinels и ancestor reparse points.
Junction/reparse escape блокирует запуск; существующий sentinel не перезаписывается.
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
и все relevant оригиналы на Scenario (не более трёх). Произвольная выборка
первых/фиксированных трёх кадров из broad launcher не допускается: если весь
required proof list не помещается в bound, launch блокируется заранее.
Оригиналы архивируются сразу после invocation, до очистки следующего launcher.
Это generated control evidence, не task PR payload.
`bind-native-proof` после native commit проверяет unchanged source
fingerprint, task/base/engine/path, точный текущий `native-plan`, successful invocations
с exit code 0 и required originals/hashes; затем связывает manifest с точным
candidate head до push. Эти проверки повторяются перед review/merge; изменение
approved check selection делает старый PASS manifest непригодным.
Проверка текущего `native-plan` обязательна для КАЖДОГО Z-Code candidate,
до любого non-runtime bypass и reviewer dispatch. Для `agent-control` повторно
проверяются явные infrastructure `allowed_paths` и exact base/head diff на
review/merge boundary; сохранённый runtime manifest/remote receipt запрещён.
Missing/unknown profile или downgrade со старым proof не дают `READY`/
`MERGE_ALLOWED`, даже при совпадающем canonical exact-head `CLEAN` review.
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

## Утренние bounded пакеты (candidate, ещё не deployment)

Один `queue.json`: `tasks` хранит execution/history; дополнительный `batches` —
не вторая очередь, а exact определения proposal/approval. Runtime task материализуется
только native scheduler'ом, по одной. Pending определения НЕ получают `READY` и
не конфликтуют с serial active-task gate. Старый `DONE` history и три
`LIST_APPROVED_NOT_DISPATCHED` game items сохраняются; их статус не является новым
утренним approval. Этот infrastructure pass не утверждает ни одну новую game feature.

Packet input — строго `{id, source:{path:"docs/technical_plan.md", revision:<40-hex>},
tasks:[...]}`. В определении обязательны ровно:
- `id`, `title`, `goal`;
- `allowed_paths` (точные paths либо repository-prefix с `/`);
- `protected_contracts`, `acceptance` (непустые массивы);
- `dependencies` (IDs внутри пакета; пустой массив допустим), `risk`;
- `player_facing` boolean, `validation`;
- `native_validation_profile`, `native_qa` (`null` для `agent-control`).

1–15 — максимум, не quota на придуманные features/canon. Source revision должен
содержать repository roadmap; соответствие намерения roadmap проверяют оператор
утром и независимый reviewer. Никаких executable/command/shell/provider полей.
Existing fixed `native-plan` проверяется ещё при proposal и при materialization:
закрытые `PlayMode`/graphical profiles НЕ открыты этим пакетом. Если product task
нуждается в таком proof, этот staged native маршрут остаётся недоступен.

Proposal файл внутри existing runtime — только кратковременный input, не tracker.
После принятой host publication оператор/внешний Supervisor transport использует:

```powershell
$Python='C:\Users\roman\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$C='D:\How_I_Fall\agent-control'
# Proposal: без user decision, dispatch запрещён.
& $Python "$C\hif-control.py" batch-propose --output "$C\morning-packet.json"
# ТОЛЬКО пользователь в отдельной обычной native console, не model tool/PTY:
& $Python "$C\hif-control.py" batch-approve --batch-id <id> --digest <full-sha256> --consent-source 'operator-console:2026-10-07-user-morning-decision'
```

Native command печатает exact определения и требует вручную набрать `APPROVE
<full-sha256>`. Нет `--yes`, piped input, approval HTTP или self-approval модели.
Model environment/ancestor guards отказывают даже при PTY; direct signing API
также отказывает approval payload из model environment. Receipt содержит digest,
operator, источник решения, время и user-bound Windows DPAPI seal В ТОМ ЖЕ queue.
Canonical SHA-256 покрывает ВСЕ exact определения, source revision и зависимости.
Изменённый scope/acceptance, stale receipt, extra keys, duplicates/cycles, fake
source-only approval и malformed data закрывают dispatch/review/merge.
DPAPI — integrity для queue-only counterfeit, НЕ security sandbox против
hostile arbitrary code с тем же OS principal: такой код способен заменить и сам
runtime. Runtime source и operator console остаются trusted host boundary.

`batch-tick` — фиксированный native шаг ЕДИНСТВЕННОГО `supervisor-loop.ps1` под
existing `HowIFallSupervisorWake` + `HowIFallWriter` locks. `scope-check` не пишет
state и вызывается внутри writer/reviewer lock. Любой `batch-propose`, включая
standalone operator command, берёт wake lock, затем writer lock ДО чтения снимка.
Planning wake пишет только уникальный кратковременный packet input; после успешного
model turn native host освобождает wake lock и вызывает обычный `batch-propose`
с обоими locks. Bypass нет; старый draft не переиспользуется после restart,
failed turn не импортируется. Operator action не может состязаться с model wake.
Scheduler state write — atomic replacement под
writer lock с compare-and-swap исходного снимка: более свежий operator state не
затирается. Partial queue/state/quota write — `STOP`, fatal exit `78`, даже если
marker невозможно записать. Нет автоматического repair/reset/clean.

Для КАЖДОЙ следующей задачи native selection требует fresh GitHub master SHA и
совпадение с native-synced reference `master`; writer fetch проверяет exact
`origin/master` повторно. После первой задачи следующая получает НОВУЮ base,
а не batch source revision. Native-signed dispatch identity связывает definition,
base, engine, path и branch. Предыдущая задача должна пройти merge, native sync
(с matching sealed receipt), repository roadmap reconciliation и `DONE`.
`ROADMAP_SYNC_READY`, CI/review/auth wait или partial candidate не разрешают
следующий writer. Scope guards проверяют durable определения и реальный diff
(включая обе стороны rename) до writer, до публикации, reviewer и merge/native sync.
Batch approval не заменяет fresh independent Sol High, exact-head CI/proof.
Model processes не получают write-root `master` и не выполняют его git transport.

`quota` теперь read-only RPC/JSON output, не unlocked control write. Native
`batch-tick` получает fresh snapshot под existing locks и сохраняет fallback.
Unknown/stale/incomplete quota → `WAIT_QUOTA`, без модельных idle loops. ≤25% в
любом окне выбирает existing Z-Code Flash Max edit/no-shell; ≥26% в обоих — Sol6.1
High. После dispatch identity engine immutable: retry сохраняет исходный checkout,
head, branch и partial diff, даже при изменении квоты. Нет переноса diff.
Недоступный strong reviewer — `WAIT_STRONG_REVIEW`, no merge.

Exhaustion: `queue.batch_planning` — только durable claim одного planning wake,
не второй execution tracker. `REQUESTED` переживает restart до claim;
`batch-plan-claim` нативно переводит его в `CLAIMED` перед fresh Luna wake.
После единственной попытки — proposal/`WAIT_USER`. Crash после claim не вызывает
повторную оплату/recursive resume; оператор может разобраться с прерванным planning.
Historical `supervisor_thread_id` оставлен для истории, `exec resume` не используется.
Durable task/state/roadmap/review receipts — context каждой новой bounded session.

`STOP`, `MAINTENANCE`, `enabled=false`, `PAUSED` запрещают новые dispatch/wakes,
но НЕ убивают текущий model turn и не теряют partial diff. При восстановлении
сначала inspect identity/diff/receipts; продолжать SAME task, не новый engine.
Обычные CI failures и deterministic fixes — bounded automatic correction;
новое решение, auth или настоящий blocker — честный wait. Subjective UI choices
включаются в утренние определения; objective QA внутри approved scope автономен.

## Read-only dashboard

Текущая задача показывает этап, шкалу семи этапов процесса, время от начала
текущего прохода, последнюю активность и оставшиеся этапы. Проценты явно означают
маршрут `подготовка → implementation → local checks → independent review → PR/CI
→ merge/sync → roadmap`, НЕ готовность кода и НЕ равномерный расход времени.
Correction может вернуть шкалу назад. Для blockers/unknown процент не вычисляется;
ETA неизвестен до накопления сопоставимой истории. Время текущего прохода берётся
из timestamp host log в timezone компьютера, не из количества строк/токенов.
Исторический snapshot/пропавшая связь остаются `НЕ LIVE`.

В repository включены ТОЛЬКО исходные `hif-dashboard.py`, `.html`,
`test_dashboard.py` из `zagent/tools/agent-control`; parent zagent/живой экран не
изменены. Нет installer/autostart/нового queue/loop, executable из task packet,
POST actions или credentials. HTTP — существующий read-only экран localhost.
Progress/approval-recorded/next/wait показываются компактно; recorded receipt не
объявляется native dispatch PASS. Live PID facts отделены от running scheduler
timer. GPT показывает fresh source/age/reset или stale fallback. GLM остаток
читается ТОЛЬКО из `agent-control/glm-quota.json`, который пишет отдельный
opt-in хелпер `hif-glm-quota.py`: он запускается пользователем/планировщиком
вручную (симметрично `hif-control.py quota` для Codex), сам читает coding-plan
api-key из хранилища ZCode (`~/.zcode/v2/credentials.json`, расшифровка
`enc:v1:` AES-256-GCM через штатный node) и делает один запрос к
авторитетному `api.z.ai/api/monitor/usage/quota/limit`. Секреты хелпер не
печатает и в файл не пишет. Сам dashboard процесс учётных данных не читает,
хелпер не запускает, `usage/stats`/`session/usage` (локальный расход запросов)
остатком не считает. Без свежего снимка (stale > 15 минут или сброс окна) GLM
balance честно unknown. Потеря связи/устаревший снимок — `НЕ LIVE`,
исторический preview не доказательство живого подключения.

Fixed `agent-control` host QA: три NAMED suites (`test_control.py`, `test_batch.py`,
`test_dashboard.py`), Python compile в памяти, dashboard JS `node --check`,
`git diff --check`, ровно четыре named PowerShell AST. Ни discovery команд из
queue, ни caller-provided suite/arguments. Все fixtures temporary/mocked; не
запускают реальные модели/Unity/GitHub writes/saves. HTTP proof ≠ screenshot PASS.
Browser inspection отмечается отдельно; при недоступном legitimate browser tool —
`NOT RUN`. Полный autonomous morning packet и GLM runtime QA — `NOT VERIFIED`,
deployment/host publication/fresh reviewer/CI выполняются только последующими gates.

## Источники идей, не готовая установка

Root research: `agent-control/evidence/orchestrator-fit-20261006.md` (read-only).
Применены идеи durable facts, bounded task packets и fresh context; чужой
исполняемый код/framework НЕ вендорился. Обоснование missing native fit:
- [AO STATUS на исследованном commit](https://github.com/OrchestratorInc/agent-orchestrator/blob/eaa1ce480b1a6741858ef19d525009cd170cd8cd/docs/STATUS.md),
  [shipped registry](https://github.com/OrchestratorInc/agent-orchestrator/blob/eaa1ce480b1a6741858ef19d525009cd170cd8cd/backend/internal/adapters/agent/registry/registry.go),
  [lifecycle automation](https://github.com/OrchestratorInc/agent-orchestrator/blob/eaa1ce480b1a6741858ef19d525009cd170cd8cd/frontend/src/docs/content/configuration/lifecycle-automation.mdx):
  по root evidence нет Z-Code adapter/config-only arbitrary executable и
  config-only autoMerge; autoReview не равен merge. Live refetch этих AO links в
  bounded pass недоступен, утверждение относится к исследованному snapshot.
- [OpenHands custom ACP command](https://github.com/OpenHands/docs/blob/main/openhands/usage/agent-canvas/acp-agents.mdx)
  не доказывает совместимость с собственным [Z-Code protocol](https://github.com/zai-org/ZCode/blob/29628c9acdb81b703bbd4080c207a0e7ce5e276e/apps/zcode-cli/packages/bootstrap/src/zcode-protocol-entrypoint.ts).
- [Symphony prototype](https://github.com/openai/symphony/blob/main/elixir/README.md)
  использует Codex App Server и собственный tracker/workspace lifecycle, не native
  HIF fit. [Ralph launcher](https://github.com/snarktank/ralph/blob/6c53cb0b831ebe8739c6a003e22af14902d8b0b5/ralph.sh)
  не переносится с его permission/COMPLETE assumptions.

Не установлены AO/OpenHands/Symphony/Ralph, второй daemon/DB/provider или платный
GLM API; OAuth extraction/dangerous bypass/global config expansion запрещены.
