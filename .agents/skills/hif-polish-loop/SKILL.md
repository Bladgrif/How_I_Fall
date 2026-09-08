---
name: hif-polish-loop
description: Ограниченный feedback loop для одной player-facing поверхности How I Fall.
---

# HIF Polish Loop

Используй для одной player-facing UI-поверхности с несколькими связанными UX/visual замечаниями. Не используй для backend-only, docs-only или очевидной правки на 1–2 строки: там нужен обычный bounded fix.

## Границы

По умолчанию: одна поверхность, не более 3 implementation/visual итераций и не более 10 genuinely relevant файлов в начальном context pack. Более строгий пользовательский лимит важнее.

Repository contracts важнее предположений, памяти и внешних references. References дают принципы, но не оправдывают копирование чужого layout/assets или изменение HIF contracts.

Не расширяй product scope и не добавляй speculative architecture. Защищай `SaveData` v3, working APIs, scenes/prefabs/serialized refs, `Packages`, `ProjectSettings` и unrelated local changes.

## Старт и компактное состояние

Перед правками коротко зафиксируй reproduced problems, protected behavior, 3–5 objective acceptance criteria и subjective вопросы, не блокирующие automated pass.

В длинном проходе поддерживай один mutable task-state:

```text
Goal:
Base SHA:
Surface / scope:
Protected contracts:
Acceptance:
Iteration: 0/3
Completed:
Validated evidence:
Open objective defects:
Blockers:
Next action:
Budget remaining:
```

Обновляй поля вместо растущего дневника; evidence храни компактно как проверку, результат и artifact path. После production change используй свежий test/runtime/screenshot result. Git/runtime/test evidence выше памяти агента. Это execution discipline, а не обещание стереть history или новая production memory system; task-state не коммить без отдельной причины.

## Context и baseline

Сначала прочитай `AGENTS.md`, только relevant product/research contracts, ближайшие production files и tests, relevant baseline и существующий graphical E2E/launcher. Для больших файлов сначала search и точные spans; расширяй pack только после доказанной зависимости.

До visual change переиспользуй существующий graphical E2E/launcher и, если инфраструктура позволяет, получи current screenshot. Не создавай второй graphical QA framework. Свежий точный baseline не нужно перезапускать только ради формальности.

## Реализация и доказательство

Сделай минимальный diff. После каждой существенной итерации:

1. compile/import preflight;
2. самый узкий meaningful regression test;
3. relevant smoke/regression;
4. для player-facing поверхности — `$hif-visual-qa` через существующий graphical E2E;
5. inspect fresh screenshots изменённых состояний.

Проверяй clipping/overlap, visibility, control geometry, hover/focus/pressed/disabled и stale state, anchors/layout, missing assets и visual weight. Для изменённого поведения добавляй разумное regression coverage по `AGENTS.md`; не добавляй зеркальный тест для low-impact reversible visual change, если существующий smoke/graphical proof уже ловит defect.

После successful meaningful proof не расширяй и не повторяй проверки без нового production change, failure или конкретного риска.

## Bounded visual correction

После inspection перечисли не более 5 существенных objective defects. Исправляй только их, без нового redesign, затем повторяй relevant proof. Остановись раньше лимита, когда acceptance достигнут, остался subjective taste, нужен новый product decision, proof заблокирован инфраструктурой или correction выходит за scope. Не полируй бесконечно.

После значимого successful visual pass обнови небольшой relevant набор `docs/visual-baselines/`, но не весь `QAArtifacts`.

## Routing и экономия

Если среда реально поддерживает delegation/model routing:

- Luna — tiny fixes, docs, deterministic tests/config/boilerplate;
- Terra — normal Unity/C#/UI implementation;
- Sol — сложный root cause, architecture или interdependent state;
- Astra — редкий дорогой autonomous visual/product judgement;
- дешёвый worker — только bulk overview/deterministic extraction либо boilerplate с точной spec и существующим HIF reference.

Не поручай worker root-cause judgement, architecture, Save compatibility, player-facing product decision, reviewer verdict или exact editing без прочитанных source spans. Worker output — предложение, не proof. Не давай агентам конкурирующе редактировать одну поверхность. Если реального routing нет, не имитируй delegation: держи context узким.

Для Astra: если bounded intent уже ясен, действуй без лишнего уточнения безопасными/reversible шагами; не расходуй дорогой context на повторный большой обзор или необоснованное тестирование. User task задаёт цель, но `AGENTS.md` и repository safety contracts обязательны. При настоящем blocker назови точное правило.

## Review и отчёт

Перед commit/push проверь scope и `git diff --check`; stage только task files. Commit/push — только при явном разрешении задачи. После push результат — только **REVIEW CANDIDATE** или **BLOCKED**, не `DONE`.

ChatGPT reviewer отдельно владеет GitHub diff/scope review, mandatory CI, visual evidence review, Drive roadmap/capability sync и verdict `DONE`/`NEEDS CORRECTION`.

Коротко верни: base SHA, surface/problems, прочитанный context, реальную delegation, changed files, checks/graphical E2E/screenshots, iterations, baselines, `NOT RUN`, remaining gaps, commit/push/CI и `REVIEW CANDIDATE` либо `BLOCKED`.
