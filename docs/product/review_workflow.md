# Review workflow

Этот документ описывает обязательный review workflow для How I Fall. Отчёт
Codex сам по себе не является доказательством: reviewer по возможности
проверяет реальные изменённые файлы и commit, scope, тесты, GitHub CI и
production risks.

## Общий review

Работа не считается `DONE`, пока обязательные GitHub CI checks не зелёные:

- `Unity Test Framework` — `GREEN`;
- `Unity smoke tests` — `GREEN`.

Локальные тесты, graphical E2E и screenshots должны указываться отдельно от
GitHub CI. Непроверенные состояния помечаются `NOT RUN`.

## Player-facing UI

Для значимого UI/UX изменения Codex:

1. делает минимальный task-scoped diff;
2. запускает targeted tests и релевантные regression/smokes;
3. запускает соответствующий graphical E2E в реальном runtime;
4. получает свежие screenshots и самостоятельно их inspect-ит;
5. исправляет objective defects: clipping, overlap, broken anchors/layout,
   missing sprites/textures, malformed dropdowns, wrong visibility и очевидные
   runtime UI defects;
6. повторяет graphical proof после исправления.

Human manual QA не заменяет эту автоматизацию. Пользовательское субъективное
aesthetic approval нужно только там, где остаются вопросы вкуса, настроения или
художественного направления.

## Visual baselines

После успешного graphical E2E для значимого player-facing visual pass Codex
обновляет релевантные screenshots в `docs/visual-baselines/`. Это небольшой
curated set ключевых review states (например, `main_menu.png` и
`preferences.png`), а не копия всех `QAArtifacts/`. `QAArtifacts/` остаются
временным gitignored proof.

Commit/push UI вместе с baselines после automated PASS — только
`REVIEW CANDIDATE`, не финальное visual acceptance. Reviewer открывает реальные
baselines, сравнивает их с предыдущим состоянием и при крупном redesign — с
несколькими хорошими внешними game/VN references. Проверяются composition,
hierarchy, spacing, readability, consistency, visual weight, interaction states
и соответствие HIF UI principles. Существенный defect возвращает задачу на
небольшой correction pass с повторными graphical E2E и обновлением baselines.

Baselines не являются final art или automatic aesthetic approval.

## Google Drive visual-review mirror

GitHub repository остаётся source of truth для code, docs и curated visual
baselines. Для удобного human/reviewer visual inspection используется отдельный
Google Drive mirror:

`How I Fall/Visual Review/`

- `Current Screens/` — свежие review screenshots по конкретным UI areas;
- `References/` — внешние visual references для сравнения;
- `Archive/` — старые review-кандидаты при необходимости.

Для значимого visual pass fresh screenshots должны быть доступны reviewer'у не
только как текстовый report. Codex обязан создать и inspect-ить runtime proof.
Если в его текущем environment есть уже настроенный авторизованный способ загрузки
в Google Drive mirror, screenshots следует туда скопировать. Если такого доступа
нет, Drive upload не является причиной считать graphical QA failed: в отчёте он
помечается `NOT RUN / reviewer handoff required`, а repository baseline и
`QAArtifacts` всё равно обновляются/сохраняются по обычным правилам.

Google Drive mirror не заменяет repository baseline, tests, graphical E2E или CI
и не является source of truth.

## Living reviewer roadmap

Помимо visual-review mirror, Google Drive используется reviewer'ом как living
progress tracker для benchmark-driven product passes. Это не заменяет repository
source of truth и не даёт Drive-документам права переопределять актуальный код или
утверждённые repository docs.

Основные reviewer documents:

`How I Fall/Research & Roadmap/VN UI UX Benchmark 2026-08-31/`

- `01 — VN UI UX benchmark — research synthesis 2026-08-31` — benchmark/rationale;
- `02 — HIF capability map & benchmark decisions` — что уже есть, чего нет и что
  сознательно deferred;
- `03 — Polished Demo implementation roadmap` — ordered living backlog и текущий
  progress;
- `05 — Benchmark sources & evidence index` — evidence/source lookup при спорных
  решениях.

После каждого review-candidate push reviewer обязан до выбора следующей задачи:

1. проверить реальный GitHub commit/diff, scope, tests, graphical proof,
   visual baselines и mandatory CI;
2. открыть актуальные `02` и `03`; при необходимости сверить `01`/`05`;
3. сравнить результат commit с capability map, benchmark decisions и ordered
   roadmap;
4. обновить `03`: reviewed commit SHA, CI status, статус pass (`DONE`, `PARTIAL`,
   `BLOCKED` или `NEEDS CORRECTION`) и следующий bounded pass;
5. если commit материально изменил capability проекта, синхронизировать `02`;
6. только после этой синхронизации формировать следующий Codex prompt.

Следующая задача не выбирается только по памяти текущего чата. Если repository и
Drive расходятся, reviewer явно фиксирует рассинхронизацию, предпочитает актуальный
repository и затем приводит living roadmap/capability map в соответствие.

## Разделение ролей

- **Codex:** implementation, automated tests, objective graphical QA и
  screenshot proof; при доступном настроенном канале — доставка review screenshots
  в Google Drive mirror.
- **Reviewer (ChatGPT):** diff/scope/risk review, проверка test evidence,
  visual baselines и Drive review screenshots, внешних references при необходимости,
  GitHub CI, синхронизация living capability map/roadmap и решение о correction
  pass / следующей bounded задаче.
- **User:** финальное субъективное aesthetic approval, когда оно действительно
  нужно.

## Research-first

Для крупных UI/UX/product решений порядок такой:

1. сначала прочитать relevant repository knowledge base;
2. затем выбрать несколько сильных benchmark-референсов — прежде всего
   признанные/top-tier visual novels и качественные релевантные игры, а не
   случайные примеры;
3. после этого найти task-specific references, которые особенно хорошо решают
   именно текущую задачу (Main Menu, Preferences, Save/Load, Backlog и т. п.);
4. при необходимости добавить общий game UX/accessibility guidance;
5. сравнить варианты по конкретным критериям и рекомендовать один основной
   подход для HIF;
6. после принятия зафиксировать reusable вывод в repository docs.

Top-tier benchmark задаёт высокий уровень качества, но не является автоматическим
образцом для каждого отдельного экрана. Reviewer должен оценивать конкретное
решение, а не копировать его из-за известности игры. Для visual review важны
composition, hierarchy, spacing, readability, visual weight, consistency и
interaction states.

Internet даёт идеи, patterns и external references. Repository docs содержат
утверждённые решения How I Fall. Чужие copyrighted assets, code и уникальный
layout one-to-one не копируются.

## Standard workflow

`Task` → research/decision при необходимости → implementation → targeted tests →
regression/smokes → graphical E2E для player-facing UI → screenshot inspection →
обновление baselines → optional Drive visual-review mirror → scoped
review-candidate commit/push → review реального commit и screenshots → living
roadmap/capability sync → correction pass при необходимости → обязательные GitHub
CI `GREEN` → user aesthetic approval при genuinely subjective вопросе → `DONE` →
выбор следующего bounded pass только из синхронизированного текущего состояния.
