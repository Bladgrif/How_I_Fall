---
name: hif-polish-loop
description: Бюджетный bounded-loop для player-facing polish How I Fall: узкий context pack → implementation → targeted tests → graphical QA → максимум несколько visual corrections → review candidate.
---

# HIF Polish Loop

Используй этот skill для **одной ограниченной player-facing поверхности** How I Fall, когда есть несколько связанных UX/visual замечаний и выгоднее сделать короткий feedback loop, чем серию микропромптов.

Не используй его для backend-only задач, docs-only правок или очевидного маленького бага на 1–2 строки — там нужен обычный bounded fix.

## 1. Бюджет и границы до начала работы

По умолчанию:

- одна UI surface;
- максимум **3 implementation/visual iterations**;
- сначала читать не более **10 действительно релевантных файлов**;
- не сканировать весь repository;
- не создавать новую architecture ради loop;
- не расширять product scope по собственной инициативе.

Если пользователь задал более строгий лимит времени/итераций — он имеет приоритет.

При достижении лимита остановись с лучшим достигнутым результатом и коротко перечисли оставшиеся gaps. Не продолжай бесконечный self-polish.

## 2. Context pack

Сначала прочитай:

1. `AGENTS.md`;
2. только relevant `docs/product/*` / `docs/research/*`;
3. ближайшие production files поверхности;
4. ближайшие regression tests;
5. relevant `docs/visual-baselines/*`;
6. существующий graphical launcher/E2E для этой поверхности.

Расширяй context только после доказанной зависимости.

Repository определяет текущие HIF contracts. References используются для принципов, а не для копирования чужого layout/assets.

## 3. Зафиксируй цель до implementation

Перед правками сформулируй коротко:

- reproduced problems;
- protected behavior;
- 3–5 objective acceptance criteria;
- что является субъективным и не должно блокировать automated pass.

Не придумывай новые функции, story/canon или final art.

## 4. Baseline

До изменения player-facing UI:

- переиспользуй существующий graphical E2E/launcher;
- получи current screenshot/state, если infrastructure это позволяет;
- если пользователь дал reference — сравни hierarchy, spacing, readability, visual weight и interaction states;
- не создавай отдельный QA framework.

Если baseline уже свежий и точный для текущего SHA, не гоняй лишний тяжёлый прогон только ради формальности.

## 5. Implementation

Сделай минимальный task-scoped diff.

Защищай:

- SaveData v3;
- working APIs;
- scenes/prefabs/serialized refs;
- Packages/ProjectSettings;
- unrelated assets/code;
- пользовательские local changes.

Не добавляй managers, DI, generic UI framework или новую settings/navigation architecture без доказанной необходимости.

## 6. Проверка после каждой существенной итерации

Порядок:

1. compile/import preflight;
2. самый узкий regression test изменённого поведения;
3. relevant smoke/regression;
4. для player-facing surface — `$hif-visual-qa` через существующий graphical E2E;
5. inspect fresh screenshots.

На screenshot проверяй именно изменённые состояния, а не только общий вид:

- clipping/overlap;
- visibility;
- malformed control geometry;
- hover/focus/pressed/disabled states;
- stale/double-active state;
- anchors/layout;
- missing sprite/texture;
- очевидную несогласованность visual weight.

## 7. Visual correction loop

После screenshot inspection составь максимум **5 существенных objective defects**.

Если defects есть:

- исправь только их;
- не начинай новый redesign;
- повтори targeted proof.

Максимум 3 итерации всего.

Остановись раньше, если:

- objective acceptance выполнен;
- остаётся только subjective taste;
- следующий шаг требует нового product decision;
- infrastructure блокирует proof;
- следующая correction выходит за исходный scope.

## 8. Работа с дорогой моделью

Если задача запущена на дорогом/high-effort агенте (например Astra), не трать его контекст на механическую работу без необходимости:

- держи context узким;
- не перечитывай уже известные файлы;
- не повторяй успешные проверки без причины;
- после крупной implementation/visual judgement предпочитай остановиться с чётким correction list, если оставшееся — маленькие fixes/tests/docs, которые разумно передать более дешёвому агенту.

Не пытайся расходовать весь доступный budget только потому, что он есть.

## 9. Baselines и git

После accepted-looking visual iteration:

- обнови только небольшой relevant набор `docs/visual-baselines/`;
- не коммить весь `QAArtifacts`;
- `git diff --check`;
- проверь changed files;
- не используй `git add .` при unrelated changes;
- commit/push делай только если исходная задача это явно разрешает.

После push результат всегда **REVIEW CANDIDATE**, не `DONE`. ChatGPT reviewer отдельно проверяет GitHub diff/CI и синхронизирует Drive.

## 10. Короткий отчёт

Верни:

1. base SHA;
2. surface и reproduced problems;
3. context files actually read;
4. production files changed;
5. tests changed;
6. фактически запущенные tests;
7. graphical E2E;
8. screenshots inspected;
9. количество visual iterations;
10. baselines changed;
11. `NOT RUN`;
12. remaining objective/subjective gaps;
13. commit/push/CI, если были;
14. `REVIEW CANDIDATE` либо `BLOCKED`.
