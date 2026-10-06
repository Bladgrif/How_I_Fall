# Процесс ревью

Этот документ описывает обязательный review workflow How I Fall. Отчёт Codex сам по себе не является доказательством: reviewer по возможности проверяет реальные изменённые файлы и commit, scope, тесты, GitHub CI и риски production-систем.

## Общий review

Работа не считается `DONE`, пока обязательный PR-check `CI Gate` не `GREEN`.

`CI Gate` сам выбирает минимально достаточный CI по diff:
- docs-only (`docs/**`, Markdown и `.agents/**`) → Unity jobs пропускаются;
- безопасная in-place замена бинарного Art/Audio asset без `.meta`/rename/add/delete → `Unity smoke tests`;
- C#/runtime/UI/scene/prefab/Save/Packages/ProjectSettings/workflow/config/mixed → `Unity Test Framework` + `Unity smoke tests`;
- `workflow_dispatch` всегда запускает full CI.

После merge обязательный duplicate Unity CI на `master` не запускается: reviewer проверяет exact merged `master` SHA. Manual full CI остаётся для exceptional/high-risk случаев.

Локальные тесты, graphical E2E и скриншоты указываются отдельно от GitHub CI. Незапущенные проверки отмечаются `NOT RUN`.

## Player-facing UI

Для значимого UI/UX изменения Codex:
1. делает минимальный diff строго в рамках задачи;
2. запускает targeted tests и релевантные regression/smoke;
3. запускает соответствующий graphical E2E в реальном runtime;
4. получает свежие скриншоты и самостоятельно их просматривает;
5. исправляет объективные дефекты: clipping, overlap, broken anchors/layout, missing sprite/texture, malformed controls, неправильную visibility и очевидные runtime UI bugs;
6. повторяет graphical proof после исправления.

Ручной QA пользователя не заменяет эту автоматизацию. Субъективное эстетическое одобрение нужно только для вопросов вкуса, атмосферы и художественного направления.

Для утверждённого GLM fallback ownership объективной инспекции screenshots явно
передаётся независимому **Sol High reviewer**, если Flash не имеет image tools.
Flash пишет bounded diff без shell; native worker выполняет только выбранные
фиксированные HIF checks. Свежие XML/logs/sentinels/оригиналы и manifest — machine
evidence, а не writer visual PASS. Missing/zero/stale local proof запрещает native
commit/push; native implementation failure получает максимум два SAME-engine
correction retry без переноса partial diff. Полный contract — orchestration/README.

До objective review Supervisor публикует bounded оригиналы и manifest через
существующий `visual-review` artifact (только реально изменённые curated baselines),
иначе Drive `03 — UI — implementation & QA proof`, иначе `evidence/<task>`.
`native_remote_proof` связывает route/URL с exact head и SHA-256 manifest; reviewer
проверяет реальные доступные оригиналы, hashes и source identity. Local path или
receipt без просмотра не дают `visual_proof_verified=true`. Без permitted route —
`WAIT_VISUAL_PROOF`, после неуспешных попыток `BLOCKED`; merge запрещён.
Evidence-ветка не мержится и удаляется после reviewer acceptance. Sol writer flow
с самостоятельной объективной инспекцией остаётся прежним.

## Визуальные baseline-скриншоты

После успешного graphical E2E для значимого player-facing visual pass Codex обновляет только релевантный небольшой набор в `docs/visual-baselines/`, а не копирует весь `QAArtifacts/`. `QAArtifacts/` остаётся временным gitignored proof.

UI commit/push вместе с baselines после automated PASS — это `REVIEW CANDIDATE`, а не финальное визуальное одобрение. Reviewer открывает реальные baselines, сравнивает их с предыдущим состоянием и при крупном redesign — с несколькими хорошими внешними референсами. Проверяются композиция, иерархия, интервалы, читаемость, согласованность, визуальный вес и состояния взаимодействия.

Baselines не являются финальным артом или автоматическим эстетическим одобрением.

Для PR с изменёнными PNG в `docs/visual-baselines/` или `docs/ui-targets/` GitHub CI публикует artifact `visual-review-<run_number>` только с этими изменёнными изображениями и manifest с base/head SHA. Reviewer скачивает artifact и открывает реальные PNG для независимого визуального сравнения. Artifact — транспорт для ревью и не заменяет repository baseline, graphical E2E или утверждённый target.

## Зеркало визуального ревью на Google Drive

GitHub остаётся источником истины для code, docs и curated visual baselines. Для удобного просмотра reviewer'ом используется:

`How I Fall/Визуальное ревью/`

- `Текущие скриншоты/` — свежие review-скриншоты по UI-областям;
- `Референсы/` — внешние визуальные референсы;
- `Архив/` — история старых кандидатов при необходимости.

Если у Codex есть уже настроенный авторизованный способ загрузки на Drive, релевантные screenshots следует зеркалировать туда. Если такого доступа нет, отсутствие Drive upload не делает graphical QA failed: в отчёте это указывается отдельно, а repository baseline и `QAArtifacts` всё равно следуют обычным правилам.

Drive не заменяет repository baseline, tests, graphical E2E или CI.

## Живая reviewer-дорожная карта

Основной roadmap/approved-task entrypoint в репозитории — `docs/technical_plan.md`
(SourceRepoPlan); repository — источник истины. Документы Drive `02`/`03`
остаются research/history и необязательным зеркалом: reviewer синхронизирует их
при доступном Drive write, а при недоступности явно фиксирует stale-статус.
Отсутствие Drive write не блокирует `DONE` после остальных обязательных gates
(commit/diff review, tests, graphical proof, `CI Gate`).

Google Drive хранит историю benchmark-driven проходов; необязательное зеркало не является вторым обязательным tracker.

Основная папка:
`How I Fall/Исследования и дорожная карта/Бенчмарк UI UX визуальных новелл 2026-08-31/`

Ключевые документы:
- `01 — Бенчмарк UI UX визуальных новелл — сводка исследования 2026-08-31` — исследовательская база и аргументация;
- `02 — Карта возможностей HIF и решения по бенчмарку` — что уже есть, чего нет и что сознательно отложено;
- `03 — Дорожная карта Polished Functional Demo` — текущий упорядоченный backlog и прогресс;
- `05 — Источники бенчмарка и индекс доказательств` — источники и evidence.

После каждого review-candidate push reviewer обязан до выбора следующей задачи:
1. проверить реальный GitHub commit/diff, scope, tests, graphical proof, visual baselines и обязательный CI;
2. прочитать актуальный `docs/technical_plan.md` и relevant product contracts; при доступных Drive tools дополнительно сверить `02`/`03` и relevant research;
3. сравнить commit с repository capability source-index, утверждёнными решениями и roadmap; обновления repository roadmap включаются в scoped candidate diff до independent review;
4. обновить `03` при доступном Drive write: SHA, статус CI, статус прохода (`DONE`, `PARTIAL`, `BLOCKED`, `NEEDS CORRECTION`) и следующий ограниченный pass; иначе явно пометить Drive stale;
5. если возможности проекта материально изменились, обновить repository source-index в candidate diff; `02` синхронизировать только при доступной записи;
6. только после этого формировать следующую задачу coding-agent.

Следующая задача не выбирается только по памяти чата. При расхождении repository и Drive предпочитается repository, после чего Drive приводится в соответствие при доступной записи; недоступная запись фиксируется как stale и не блокирует `DONE`.

## Разделение ролей

- **Codex:** implementation, автоматические тесты, объективный graphical QA и screenshot proof; при доступном настроенном канале — доставка review screenshots на Drive.
- **Независимый reviewer и HIF Supervisor в Codex (ChatGPT только manual fallback):** review diff/scope/risks, проверка test evidence, baselines, Drive screenshots, внешних references при необходимости, GitHub CI, синхронизация capability map/roadmap и решение о correction/следующей задаче.
- **Пользователь:** финальное субъективное эстетическое одобрение там, где оно действительно необходимо.

## Research-first

Для крупного UI/UX/product решения:
1. прочитать релевантную repository knowledge base;
2. выбрать несколько сильных benchmark-референсов;
3. найти task-specific references для текущей задачи;
4. при необходимости добавить общий UX/accessibility guidance;
5. сравнить варианты по конкретным критериям и рекомендовать один основной подход для HIF;
6. после принятия зафиксировать повторно используемый вывод в repository docs.

Интернет даёт идеи, patterns и references. Репозиторий хранит утверждённые решения How I Fall. Чужие copyrighted assets/code и уникальные layouts один-в-один не копируются.

## Стандартный поток

`Задача` → исследование/решение при необходимости → implementation → targeted tests → regression/smoke → graphical E2E для player-facing UI → просмотр screenshots → обновление baselines → при возможности зеркало на Drive → scoped review-candidate commit/push → review реального commit → синхронизация roadmap/capability → correction при необходимости → PR `CI Gate` `GREEN` → субъективное одобрение пользователя, если действительно нужно → merge → `SYNC_MASTER_PENDING` → native scheduler verify/fetch/ff-only exact `master` → `ROADMAP_SYNC_READY` → optional Drive mirror (`NOT SYNCED`, если недоступно) → `DONE` → следующий ограниченный pass из синхронизированного состояния.
