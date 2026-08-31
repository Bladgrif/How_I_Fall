# Процесс ревью

Этот документ описывает обязательный review workflow How I Fall. Отчёт Codex сам по себе не является доказательством: reviewer по возможности проверяет реальные изменённые файлы и commit, scope, тесты, GitHub CI и риски production-систем.

## Общий review

Работа не считается `DONE`, пока обязательные GitHub CI checks не зелёные:
- `Unity Test Framework` — `GREEN`;
- `Unity smoke tests` — `GREEN`.

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

## Визуальные baseline-скриншоты

После успешного graphical E2E для значимого player-facing visual pass Codex обновляет только релевантный небольшой набор в `docs/visual-baselines/`, а не копирует весь `QAArtifacts/`. `QAArtifacts/` остаётся временным gitignored proof.

UI commit/push вместе с baselines после automated PASS — это `REVIEW CANDIDATE`, а не финальное визуальное одобрение. Reviewer открывает реальные baselines, сравнивает их с предыдущим состоянием и при крупном redesign — с несколькими хорошими внешними референсами. Проверяются композиция, иерархия, интервалы, читаемость, согласованность, визуальный вес и состояния взаимодействия.

Baselines не являются финальным артом или автоматическим эстетическим одобрением.

## Зеркало визуального ревью на Google Drive

GitHub остаётся источником истины для code, docs и curated visual baselines. Для удобного просмотра reviewer'ом используется:

`How I Fall/Визуальное ревью/`

- `Текущие скриншоты/` — свежие review-скриншоты по UI-областям;
- `Референсы/` — внешние визуальные референсы;
- `Архив/` — история старых кандидатов при необходимости.

Если у Codex есть уже настроенный авторизованный способ загрузки на Drive, релевантные screenshots следует зеркалировать туда. Если такого доступа нет, отсутствие Drive upload не делает graphical QA failed: в отчёте это указывается отдельно, а repository baseline и `QAArtifacts` всё равно следуют обычным правилам.

Drive не заменяет repository baseline, tests, graphical E2E или CI.

## Живая reviewer-дорожная карта

Google Drive используется reviewer'ом как живой tracker benchmark-driven проходов, но не заменяет репозиторий как источник истины.

Основная папка:
`How I Fall/Исследования и дорожная карта/Бенчмарк UI UX визуальных новелл 2026-08-31/`

Ключевые документы:
- `01 — Бенчмарк UI UX визуальных новелл — сводка исследования 2026-08-31` — исследовательская база и аргументация;
- `02 — Карта возможностей HIF и решения по бенчмарку` — что уже есть, чего нет и что сознательно отложено;
- `03 — Дорожная карта Polished Functional Demo` — текущий упорядоченный backlog и прогресс;
- `05 — Источники бенчмарка и индекс доказательств` — источники и evidence.

После каждого review-candidate push reviewer обязан до выбора следующей задачи:
1. проверить реальный GitHub commit/diff, scope, tests, graphical proof, visual baselines и обязательный CI;
2. открыть актуальные `02` и `03`; при необходимости сверить `01`/`05`;
3. сравнить commit с capability map, benchmark decisions и roadmap;
4. обновить `03`: SHA, статус CI, статус прохода (`DONE`, `PARTIAL`, `BLOCKED`, `NEEDS CORRECTION`) и следующий ограниченный pass;
5. если возможности проекта материально изменились, синхронизировать `02`;
6. только после этого формировать следующую задачу Codex.

Следующая задача не выбирается только по памяти чата. При расхождении repository и Drive предпочитается repository, после чего Drive приводится в соответствие.

## Разделение ролей

- **Codex:** implementation, автоматические тесты, объективный graphical QA и screenshot proof; при доступном настроенном канале — доставка review screenshots на Drive.
- **Reviewer (ChatGPT):** review diff/scope/risks, проверка test evidence, baselines, Drive screenshots, внешних references при необходимости, GitHub CI, синхронизация capability map/roadmap и решение о correction/следующей задаче.
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

`Задача` → исследование/решение при необходимости → implementation → targeted tests → regression/smoke → graphical E2E для player-facing UI → просмотр screenshots → обновление baselines → при возможности зеркало на Drive → scoped review-candidate commit/push → review реального commit → синхронизация roadmap/capability → correction при необходимости → обязательный GitHub CI `GREEN` → субъективное одобрение пользователя, если действительно нужно → `DONE` → следующий ограниченный pass из синхронизированного состояния.
