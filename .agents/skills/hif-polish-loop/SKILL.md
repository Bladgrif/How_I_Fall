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

До independent implementation определи и проверь accepted base SHA текущей задачи. Если заданный expected base больше не совпадает, остановись до редактирования и сообщи actual SHA. Не reset/clean/overwrite dirty, divergent или содержащий unrelated user changes checkout: при авторизованной implementation и доступной среде работай в clean disposable worktree от verified accepted base, сохраняя пользовательские изменения.

## Finish line и автономность

Перед правками коротко зафиксируй goal, reproduced problems/material deltas, protected behavior и 3–5 objective acceptance criteria.

После этого действуй автономно внутри bounded scope: сам выбери нужные source spans, минимальную implementation и соразмерную validation. Не запрашивай подтверждение каждого безопасного/reversible шага и не останавливайся на плане, если implementation разрешена и blocker отсутствует.

Settled repository/roadmap decisions — constraints. Не переоткрывай их без нового reproduced defect, contradiction source of truth или явного user request.

Не добавляй в prompt/план пустые усилители вроде `think carefully` или `be extremely thorough`; глубина задаётся model/reasoning и acceptance criteria.

## Компактное состояние

Для длинного прохода поддерживай один mutable task-state:

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

Обновляй поля вместо растущего дневника; evidence храни компактно как проверку, результат и artifact path. После production change используй свежий test/runtime/screenshot result. Git/runtime/test evidence выше памяти агента. Task-state не коммить без отдельной причины.

## Context и baseline

Сначала прочитай `AGENTS.md`, только relevant product/research contracts, ближайшие production files/tests, relevant baseline и существующий graphical E2E/launcher. Для больших файлов сначала search и точные spans; расширяй pack только после доказанной зависимости.

До visual change переиспользуй существующий graphical E2E/launcher и, если инфраструктура позволяет, получи current screenshot. Не создавай второй graphical QA framework. Свежий точный baseline не нужно перезапускать только ради формальности.

## Реализация и доказательство

Сделай минимальный diff. После существенной итерации используй проверки, соразмерные изменению:

1. compile/import preflight;
2. самый узкий meaningful regression test;
3. relevant smoke/regression;
4. для player-facing поверхности — `$hif-visual-qa` через существующий graphical E2E;
5. inspect fresh screenshots изменённых состояний.

Расширяй suite только при failure, новом риске или unresolved concern.

Проверяй clipping/overlap, visibility, control geometry, hover/focus/pressed/disabled и stale state, anchors/layout, missing assets и visual weight. Для изменённого поведения добавляй разумное regression coverage по `AGENTS.md`; не добавляй зеркальный тест для low-impact reversible visual change, если существующий smoke/graphical proof уже ловит defect.

После successful meaningful proof не расширяй и не повторяй проверки без нового production change, failure или конкретного риска.

## Bounded visual correction

После inspection перечисли не более 5 существенных objective defects. Исправляй только их, без нового redesign, затем повторяй relevant proof. Остановись раньше лимита, когда acceptance достигнут, остался subjective taste, нужен новый product decision, proof заблокирован инфраструктурой или correction выходит за scope. Не полируй бесконечно.

После значимого successful visual pass обнови небольшой relevant набор `docs/visual-baselines/`, но не весь `QAArtifacts`.

## Routing и delegation

Среду и модель выбирает reviewer по `docs/product/agent_orchestration.md`.

- **GLM-5.3-Flash** — default Z-Code/J-Code worker для routine Unity/C#/UI, visual polish, QA и correction loops.
- **GPT-6 Luna** — Codex high-volume workhorse для focused bounded implementation, tests/docs и independent second pass.
- **GLM-5.3 / GPT-6 Sol** — escalation для реально сложного reasoning, lifecycle/state или high regression-risk.
- **GPT-6 Astra** — hardest/high-risk end-to-end work, не default visual-polish coder.

Если нужен multi-agent harness, предпочитай J-Code только при 2+ genuinely independent workstreams, отдельном read-only review или полезном parallel production/tests/QA investigation. Обычно 2–3 агента и один coordinator.

Не давай нескольким агентам конкурирующе редактировать одну поверхность, scene/prefab или serialized asset. Worker output — proposal/evidence, не reviewer proof. Если реального routing/delegation нет, не имитируй его.

## Review и отчёт

Перед commit/push проверь scope и `git diff --check`; stage только task files. Commit/push — только при явном разрешении задачи. После push результат — только **REVIEW CANDIDATE** или **BLOCKED**, не `DONE`.

ChatGPT reviewer отдельно владеет GitHub diff/scope review, mandatory CI, visual evidence review, Drive roadmap/capability sync и verdict `DONE`/`NEEDS CORRECTION`.

Коротко верни: base SHA, surface/problems, changed files, реально запущенные checks/graphical E2E/screenshots, iterations, baselines, `NOT RUN`, remaining gaps, commit/push/CI и `REVIEW CANDIDATE` либо `BLOCKED`.
