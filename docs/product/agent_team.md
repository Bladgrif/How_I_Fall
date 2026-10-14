# Команда How I Fall

## Границы

Project-scoped Codex profiles и инструкции для существующего координатора,
не новый Supervisor/scheduler/queue/daemon. Общая policy — `agent_orchestration.md`,
acceptance — `review_workflow.md`. Profile не permission на editing/publication/
merge/operator approval: полномочия задаёт конкретная утверждённая задача.

Profiles находятся в `.codex/agents/`, reusable entrypoint — `$hif-agent-team`.
TOML `name` — идентификатор роли. Эти файлы не создают постоянно работающих
процессов и не интегрируют роли автоматически в existing host workers.
Если текущий tool не поддерживает named role, основной координатор передаёт
instructions из файла реальной bounded сессии, честно называя это manual handoff.

## Состав: четыре основные роли и optional QA

| Роль | Задача / результат | Граница |
| --- | --- | --- |
| `hif_pm` — project manager | Evidence-backed состояние, ближайший план, dependencies, acceptance и briefs | Специализация existing Supervisor; как субагент advisory, не второй durable координатор |
| `hif_ui_designer` — UI-дизайнер / арт-директор | Игровые референсы с provenance, разные concepts, рекомендованный target и implementation brief | Нет production editing, новых механик/canon или self-approval направления |
| `hif_coder` — coding-agent | Минимальный approved diff, regression, runtime proof, review candidate | Не independent review своего кода, merge или native sync |
| `hif_reviewer` — независимый reviewer | Реальный exact diff/test/visual review, CLEAN / NEEDS CORRECTION / BLOCKED | Не fixes своего candidate, user approval или permission merge |
| `hif_qa` — optional playtester | Самостоятельный journey/standalone/reproduction, observed defects и evidence | Не обязательная пятая ступень; не production fixes/release-ready/concurrent Unity |

Отдельные постоянные researcher/illustrator/architect/Git/release агенты пока
не нужны. Research и generated art входят в designer; сложную реализацию
обсуждают coder/reviewer по необходимости. Publication/gates — existing
координатор, fixed master transport — только existing external scheduler.
Обычные tests и objective runtime inspection остаются ответственностью writer.

## Дизайн до кода

Designer читает реальное existing behavior, accepted targets и product constraints.
Для substantial redesign смотрит реальные изображения нескольких сильных игр
именно для текущей поверхности: ссылки/происхождение, увиденное состояние,
композиция, hierarchy, spacing, readability и states. Поисковый snippet и
известное название не visual evidence. Источники/недоступное inspection отмечаются
честно; чужие assets и уникальные layouts не копируются в проект.

Результат — небольшой набор действительно разных композиций (обычно два-три
варианта), объяснение что адаптировать/отвергнуть для HIF, рекомендация одного
направления и красивые макеты в игровом контексте. Не просто recolor wireframe.
Сам inspect originals и реальную читабельность/geometry/controls. Imagegen
concept не доказывает runtime layout/input/lifecycle; TEST art/text — NOT CANON.

Пользователь выбирает направление. После этого передаётся target, relevant
state sheet/assets map, concrete presentation delta, protected contracts и
bounded implementation brief. Design approval не production permission.
Отказ от aesthetic candidate не reproduced runtime defect; предыдущие
targets/evidence сохраняются, новое направление — отдельная версия.

## Ближайший план и галочки

Единственный repository roadmap/approved-task entrypoint — `docs/technical_plan.md`.
PM не заводит второй backlog/database/дневник и не подменяет operational
`agent-control/queue.json`/`state.json`. В autonomous mode execution identity,
approval receipts и statuses по-прежнему принадлежат existing native state.

Используй короткий rolling horizon: следующий bounded pass и несколько
зависимых предложений с goal/surface, approval, dependencies, acceptance,
evidence и next gate. Дальние идеи — proposal, не автоматически approved
features/canon. Обновляй один компактный раздел вместо растущего журнала.

Можно зачеркнуть завершённую **подзадачу**, например «макет показан» или
«targeted XML проверен». Task остаётся REVIEW CANDIDATE/PARTIAL до обязательных
review/CI/merge/native-sync/roadmap gates. Merge без local sync не DONE.
Не сочиняй receipts и не редактируй live state ради галочек. Следующую задачу
выбирай из actual repository/approval/evidence, не памяти чата.

## Handoff и полномочия

Координатор передаёт каждой роли короткий brief:

```text
Goal / expected result:
Accepted base SHA / branch / checkout:
Allowed paths / protected contracts:
Approval / Git permissions:
Objective acceptance / required proof:
Existing target, APIs, tests and launchers:
Source/head evidence locators / next gate:
```

Обычный UI flow:
`PM → designer/research → user direction approval → approved production brief →
coder/validation → optional bounded QA → independent reviewer → existing
publication/CI/merge/native-sync/roadmap gates`.

Не запускай всех сразу и двух writers на одном scope. Reviewer — fresh independent
session, не автор diff. QA получает frozen source и разрешённый runtime scope;
до Unity нужны безопасный Editor ownership и startup save isolation. Late
override после Awake не evidence. Повторять tests/screenshots следует при
executable change/failure/новом риске, не ради одной docs-строки.

Handoff содержит exact SHA, scope, actual checks/NOT RUN, просмотренные
originals и unresolved findings. Source/head/hash binding и разрешённый
remote visual transport — по review_workflow. Synthetic input не physical QA.

Модель не выполняет operator attestation/approval и не пишет fixed master.
Profiles не разрешают новый pass до required reconciliation, обход protected
develop или отмену native barriers. Research/role setup не автоматический dispatch.

## Модели и лимиты

Profiles следуют existing routing:
- PM — GPT-6 Luna Low для bounded planning/support, не strong review.
- Designer/coder/reviewer — GPT-6.1 Sol High для соответствующих значимых passes,
  если разрешены quota/доступность и task scope.
- QA — GPT-6 Luna Low для ясной existing-launcher automation; сложный defect
  передаётся coder/Sol reviewer, а не исправляется дешёвым QA агентом.

До dispatch fresh `account/rateLimits/read` обязателен. В QUOTA_SAVE нельзя
вызывать Sol writer/designer из-за fixed model в TOML: existing approved
Z-Code/GLM-5.3-Flash lane для разрешённого scope либо WAIT_QUOTA. Unknown quota
не доступная. Missing Sol High reviewer — WAIT_STRONG_REVIEW, не downgrade.
Partial diff остаётся у исходного engine; смена role profile его не переносит.

Custom agents наследуют permissions/provider/tools parent session.
Read-only reviewer — поведенческий contract, не новый enforced sandbox.
Неподдерживаемые sandbox/provider/MCP overrides в profiles не задаются.
Недоступный browser/imagegen/QA tool показывается честно, не эмулируется.
Модели профилей не изменяют настройки existing worker/reviewer/Supervisor.

## Validation и activation

TOML parse и skill/frontmatter validation проверяют файлы, не качество будущих
запусков. Независимый bounded dry-run на scenarios проверяет approval boundary,
quota/base guard, reviewer proof gate и PM DONE/sync; mock cases не live dispatch.

В этой setup-задаче native discovery/role dispatch, live controller integration,
deployment и native sync не заявляются выполненными без реального исполнения.
Commit/push — только при task permission, независимое review перед integration.

Формат: [OpenAI Docs — Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents).
