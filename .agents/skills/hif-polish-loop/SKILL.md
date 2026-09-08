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

Для большого файла сначала используй targeted search/read по нужному symbol/section. Не загружай весь большой corpus в дорогую модель только ради общего обзора.

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
2. самый узкий meaningful regression test изменённого поведения;
3. relevant smoke/regression;
4. для player-facing surface — `$hif-visual-qa` через существующий graphical E2E;
5. inspect fresh screenshots.

После успешного targeted proof и обязательных checks **не расширяй и не повторяй тестирование автоматически**. Повтор/расширение оправданы только если был новый production change, failure или конкретный unresolved risk.

Для bug fix или изменённого поведения сохраняй разумное regression coverage по `AGENTS.md`. Для низко-impact reversible visual change не добавляй отдельный тест, который только зеркалит implementation, если существующий graphical/smoke proof уже объективно ловит defect.

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

## 8. Routing работы по стоимости модели

Следуй принципу: **дорогой judgement не должен тратиться на дешёвый I/O и boilerplate**.

Если execution environment поддерживает model/subagent delegation:

- большой file/corpus overview, deterministic summary, docs extraction → дешёвый worker;
- boilerplate test/config/helper → дешёвый worker, но только с точной spec и существующим HIF reference file;
- обычная Unity/C#/UI implementation → Terra-class worker;
- маленький correction/docs/test → Luna-class worker;
- сложный root cause / architecture / взаимозависимый state → Sol-class judgement;
- редкий autonomous visual/product loop → Astra-class judgement, только если цена оправдана surface-level задачей.

Параллелизуй только независимые read/research/analysis части или не пересекающиеся задачи. Не давай нескольким агентам одновременно редактировать одни и те же production files или competing implementations одной UI surface.

Не делегируй дешёвому worker:

- debugging root cause;
- архитектурные решения;
- SaveData / compatibility judgement;
- player-facing product decision;
- точное editing, если implementer ещё не прочитал relevant code spans;
- reviewer verdict.

Если среда **не умеет** реальное delegation/model routing — не имитируй его. Держи текущий context узким и оставляй маленькие последующие corrections для отдельной более дешёвой сессии.

Любой worker summary/code — только вход для основной работы, не доказательство корректности. Финальный diff всё равно проходит HIF regression/graphical QA/reviewer.

## 9. Astra: initiative, instruction priority и stop behavior

Для GPT-6 Astra следуй official model guidance OpenAI и настрой автономность явно.

Если user task уже однозначно авторизует bounded implementation:

- bias to action: не останавливайся на плане или вопросе, если безопасное разумное решение можно вывести из repository/current context;
- выполняй reversible/read-only шаги самостоятельно: `git fetch`, inspect, isolated worktree, targeted reads, tests, screenshots;
- если основной checkout dirty/divergent, но verified `origin/master` даёт нужную accepted base, создай clean disposable worktree вместо запроса разрешения, если task уже разрешает implementation;
- спрашивай пользователя только когда недостающая информация материально меняет product decision или действие genuinely destructive/irreversible.

Конкретная user task instruction имеет приоритет над **default/guideline** правилами этого skill. `AGENTS.md`, repository contracts и явно защищённые safety constraints остаются обязательными.

Если skill/instruction действительно заставляет остановиться, запросить разрешение или отклониться от цели, в отчёте назови точный файл/правило, которое стало blocker. Не маскируй интерпретацию под жёсткое требование.

Не продолжай работу ради «идеальной полноты», когда objective acceptance уже выполнен. Astra склонна к broad verification; HIF stop condition важнее дополнительного необязательного тестирования.

## 10. Работа с дорогой моделью

Если задача запущена на дорогом/high-effort агенте (например Astra):

- не перечитывай один и тот же большой corpus после того, как objective context уже извлечён;
- предпочитай targeted reads полному чтению больших файлов;
- не повторяй успешные проверки без причины;
- не трать Astra на механический git/QA loop, если это можно безопасно оставить Terra/Luna;
- после крупной implementation/visual judgement остановись с чётким correction list, если оставшееся — маленькие fixes/tests/docs;
- не расходуй весь доступный budget только потому, что он есть.

Если дешёвый `bulk-reader`-подобный worker доступен, сильная модель получает от него ответ только на конкретный вопрос. Но перед изменением кода implementer обязан прочитать точные relevant spans сам: summary не заменяет source при editing/debugging.

## 11. Baselines и git

После accepted-looking visual iteration:

- обнови только небольшой relevant набор `docs/visual-baselines/`;
- не коммить весь `QAArtifacts`;
- `git diff --check`;
- проверь changed files;
- не используй `git add .` при unrelated changes;
- commit/push делай только если исходная задача это явно разрешает.

После push результат всегда **REVIEW CANDIDATE**, не `DONE`. ChatGPT reviewer отдельно проверяет GitHub diff/CI и синхронизирует Drive.

## 12. Короткий отчёт

Пиши отчёт компактно; не пересказывай ход работы по шагам, если он не нужен для review.

Верни:

1. base SHA;
2. surface и reproduced problems;
3. context files actually read;
4. что было delegated и какой worker использован, если delegation реально была;
5. production files changed;
6. tests changed;
7. фактически запущенные tests;
8. graphical E2E;
9. screenshots inspected;
10. количество visual iterations;
11. baselines changed;
12. `NOT RUN`;
13. remaining objective/subjective gaps;
14. commit/push/CI, если были;
15. `REVIEW CANDIDATE` либо `BLOCKED`.
