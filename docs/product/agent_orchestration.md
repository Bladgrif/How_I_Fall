# Оркестрация агентов How I Fall

## Назначение

Этот документ — компактная policy для reviewer/ChatGPT: как выбирать среду,
модель и brief coding-agent для How I Fall. Репозиторий — durable source of
truth, а не память чата. Execution details принадлежат relevant skills,
особенно `$hif-polish-loop` и `$hif-visual-qa`.

## 1. Перед новой задачей

Reviewer:
1. определяет актуальный `master` и accepted base SHA;
2. читает только relevant repository docs/skills;
3. при доступных tools сверяет Drive capability map/roadmap;
4. при конфликте предпочитает repository и затем синхронизирует Drive;
5. не открывает новый pass, пока предыдущий review candidate не принят,
   исправлен или явно закрыт.

Уже принятые repository/roadmap решения — constraints, не тема повторного
обсуждения. Переоткрывай их только при новом reproduced defect, contradiction
source of truth или явном запросе пользователя.

## 2. Базовый стиль задачи

Используй **bounded scope + autonomous execution**.

Task brief должен задавать:
- цель;
- objective finish line / acceptance criteria;
- protected contracts;
- allowed/forbidden scope;
- relevant files/docs;
- required validation;
- git/report expectations.

После этого агент сам выбирает разумный путь exploration → implementation →
validation. Не микроменеджерь порядок шагов без причины и не требуй approval
на каждый безопасный/reversible action.

Если implementation разрешена и blocker отсутствует, агент не должен
останавливаться на плане. Вопрос пользователю нужен только когда недостающая
информация реально может изменить результат, затронуть protected contract или
расширить scope.

Не добавляй бессодержательные фразы вроде `think carefully`,
`be extremely thorough` или `take your time`. Глубина задаётся model/reasoning
и concrete acceptance criteria.

## 3. Тип работы

- **Normal bounded pass** — известный fix, обычная Unity/C#/UI implementation,
  tests/docs/configs, CI/log investigation.
- **`$hif-polish-loop`** — одна player-facing поверхность с несколькими
  связанными visual/UX дельтами и screenshot feedback loop.
- **Research-first** — существенное новое UI/product/functionality решение,
  которому реально нужны references/rationale.
- **Architecture/debugging pass** — трудная root cause, interdependent state,
  SaveData/lifecycle/safety risk.
- **Observation-first** — сначала воспроизвести или опровергнуть defect/delta;
  допустимый итог `NO PRODUCTION CHANGE`.

Не включай research/polish machinery ради мелкой deterministic правки.

## 4. Среда

Среду и модель выбирай отдельно.

### Z-Code
Default GLM-среда для single-agent bounded implementation, routine Unity/C#/UI,
fixes с ясным scope/root cause, tests/docs/validators, graphical E2E,
screenshot QA, CI/log investigation и correction passes.

### J-Code
Используй, когда multi-agent harness реально полезен:
- 2+ независимых workstreams;
- отдельный read-only reviewer;
- параллельное исследование production/tests/QA;
- длинное repo-wide investigation;
- bounded swarm.

Default: 2–3 агента, один coordinator отвечает за итоговый diff/tests/report.
Не давай нескольким агентам одновременно редактировать одни scenes/prefabs/
serialized assets. Не используй swarm для простой single-scope задачи.

### Codex
Используй для GPT-6 моделей, когда Codex harness удобнее или нужен независимый
второй стек implementation/review.

## 5. Модель и budget

Главный принцип: **самая дешёвая модель, стабильно проходящая quality bar**.

### GLM-5.3-Flash
Default для HIF. Weekly allowance пользователя ориентировочно ~292M tokens.
Подходит для repo exploration, routine Unity/C#/UI, больших, но ясных bounded
passes, fixes, tests/docs/validators, graphical QA, CI/log investigation и
correction cycles.

Default reasoning: Medium. Low — простая deterministic работа. High — если
нужна дополнительная глубина. Перед эскалацией сначала рассмотри Flash + High.

### GLM-5.3
Weekly allowance ориентировочно ~97M tokens. Используй только когда Flash
недостаточно надёжен: сложные interdependent systems, тяжёлый lifecycle/state
debugging, ambiguous root cause, высокая цена ошибки или неудачная Flash
попытка после уточнения scope.

### GPT-6 Luna
**Codex high-volume workhorse**, не только tiny fixes. Используй для focused,
repeatable bounded work at scale: обычная Unity/C#/UI implementation,
tests/docs/configs, correction passes и independent second implementation/check,
когда Codex удобнее. Не эскалируй на Sol только из-за размера задачи.

### GPT-6 Sol
Для demanding coding/agentic work: несколько связанных systems, сложный
Unity lifecycle/state, Save/Load, нетривиальный debugging, высокая
regression-risk.

### GPT-6 Astra
Для hardest end-to-end work: неясная root cause, несколько неудачных попыток,
architecture/system reasoning, крупные interdependent изменения и высокая
стоимость ошибки.

Практический ориентир:
`GLM-5.3-Flash / GPT-6 Luna → GLM-5.3 / GPT-6 Sol → GPT-6 Astra`

Это не автоматическая лестница. Размер context, число файлов и длительность
сами по себе не являются причиной эскалации.

## 6. Reasoning

Project-facing уровни:
- **Low** — deterministic/mechanical;
- **Medium** — обычная implementation/UI;
- **High** — сложный debugging/interdependent state;
- **Max** — только когда High недостаточен и цена ошибки высока.

Не дублируй reasoning словесными усилителями внутри prompt. Для GPT-6 runtime
может поддерживать дополнительные effort values, но project brief использует
эти четыре уровня для стабильной маршрутизации.

## 7. Сессия и context

- текущая сессия — продолжение той же bounded implementation/correction;
- новая — независимая задача, другая система или накопленный context создаёт шум.

Не проси читать весь repository. Называй relevant files/systems; большие файлы
сначала search/targeted spans. Ссылайся на `AGENTS.md` и skill вместо
копирования общих правил.

Для длинного pass допустим один mutable task-state/checklist. Обновляй его,
а не накапливай дневник. Не коммить task-state без отдельной причины.

## 8. Prompt contract

Paste-ready prompt обычно содержит:
- **Среда / Модель / Reasoning / Сессия**;
- goal и base SHA;
- bounded scope;
- relevant files/docs;
- protected contracts;
- allowed/forbidden changes;
- objective finish line / acceptance criteria;
- tests/validation и graphical QA;
- commit/push/PR policy;
- короткий report;
- stop conditions.

Task-specific protections важнее длинного пересказа общих инструкций.

## 9. Git/worktree safety

При mismatch expected base остановись до editing. Если primary checkout dirty,
divergent или содержит unrelated work, используй clean disposable worktree от
verified base. Не reset/clean/overwrite user changes, не force-push и не
используй `git add .`. Stage только task files.

## 10. Validation

- logic/state/save/choice → EditMode;
- runtime/UI/lifecycle → PlayMode;
- prefab/serialized/project integrity → smoke/validators;
- bug fix → regression исходного defect;
- docs-only → обычно Unity tests не нужны;
- player-facing → existing graphical E2E + screenshot inspection + небольшой
  curated baseline при значимом visual pass.

Запускай проверки, соразмерные изменению. Расширяй suite только при failure,
новом риске или unresolved concern. После push mandatory CI:
`Unity Test Framework` + `Unity smoke tests`.

## 11. Delegation

Subagents — не default. Используй их только при реально независимых
workstreams и подходящем harness, прежде всего J-Code. Coordinator задаёт
границы, собирает evidence и отвечает за финальный diff. Worker output —
proposal/evidence, не reviewer proof.

Не поручай параллельно нескольким агентам редактировать одну serialized
поверхность. Не используй delegation только потому, что задача «большая».

## 12. Stop conditions и роли

Остановись и сообщи blocker, если expected base изменился, отсутствует
необходимое product decision, scope неожиданно требует scene/prefab/SaveData
contract change или infrastructure блокирует обязательное proof.

После достижения acceptance не трать остаток budget на дополнительный polish.

**Implementer** владеет scoped implementation, local tests, graphical proof и
коротким `REVIEW CANDIDATE`/`BLOCKED` report.

**Reviewer/ChatGPT** владеет real diff/scope review, mandatory CI, repository +
Drive comparison/sync, merge и verdict `DONE`/`NEEDS CORRECTION`.

**User** нужен для genuinely subjective aesthetic approval или другого
неавтоматизируемого решения вкуса.
