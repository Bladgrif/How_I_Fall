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
GLM-среда для low-risk routine/support work: tests/docs/config, validators,
deterministic fixes, CI/log investigation, evidence preparation и bounded
follow-up/correction passes. Также используй Z-Code для координированной
multi-agent работы, когда параллельные независимые workstreams дают реальную
ценность: 2+ независимых направления, отдельный read-only reviewer, параллельное
исследование production/tests/QA, длинное repo-wide investigation.

Subagent'ы — не default: используй столько, сколько оправдано независимыми
workstreams (2/3/5+), без swarm ради количества. Типовая форма — один
coordinator/writer + read-only investigator'ы; coordinator отвечает за итоговый
diff/tests/report. Не давай нескольким агентам одновременно редактировать одни
scenes/prefabs/serialized assets. Не используй swarm для простой single-scope
задачи.

### J-Code
Опциональная специализированная среда. Используй только когда её конкретное
harness-поведение даёт реальное преимущество для этой задачи; сам факт
multi-agent задачи причиной не является.

### Codex
**Default implementation environment для значимых HIF production/player-facing
задач**, потому что текущий preferred primary implementer — GPT-6.1 Sol High.
Также используй Codex для GPT-6 Luna bounded work и independent second pass.

### Внешние agent-инструменты Unity

Официальный [Unity agent plugin](https://github.com/Unity-Technologies/unity-agent-plugin)
(`codex plugin add unity@unity-agent-plugin`) — опциональное Codex-усиление:
Editor-driving, CLI test/build, uGUI-гайды. Обоснование и границы —
`docs/research/agent_skills_ecosystem_audit_2026-10-01.md`:

- установка только по явной задаче; это не requirement для обычных bounded pass;
- плагин — инструмент чтения/запуска, не вход для production-правок: сцены,
  prefabs, serialized refs остаются protected по `AGENTS.md`;
- текст внешних skill'ов не вендорится в repository (Unity Companion License);
- если внешний skill противоречит repository contracts, приоритет у repository;
- Scenario-инструменты (Unity expert и генеративные) — reference/defer до
  арт-фазы; MCP и платные кредиты не подключать без отдельного решения.

## 5. Модель и budget

Главный принцип: **quality-first при разумном budget; оптимизируй total cost of
quality, а не только токены первого прохода**. Для значимой HIF implementation
лучший default — сильный первый проход, который уменьшает correction/rework.

### GPT-6.1 Sol
**Primary HIF implementer в Codex.** Для значимых production/player-facing задач
по умолчанию используй **GPT-6.1 Sol High**, если модель доступна и текущий
пользовательский лимит позволяет.

Sol High — default для:
- high-visibility UI, graphical polish и match-to-approved-target;
- обычной, но значимой Unity/C# runtime implementation;
- lifecycle/state, Save/Load и связанных multi-file систем;
- ambiguous root cause, observation-first debugging и high regression-risk;
- законченных implementation → validation → graphical proof deliverables.

Причина — локальный HIF evidence: Sol High уже дал сильные bounded результаты в
special-mode lifecycle/state и Save transaction/failed-load integrity passes,
а также качественный visual target work, при приемлемом фактическом расходе
пользовательского лимита. Это project-local routing evidence, а не обещание
универсального превосходства модели.

**Medium** допустим для ясной non-visual/low-risk задачи, когда High не даёт
ожидаемой практической выгоды. **Max** — только после неудачной High-попытки или
при действительно высокой цене ошибки. Старый GPT-6 Sol — fallback только если
GPT-6.1 Sol недоступен.

### GLM-5.3-Flash
Routine/support worker, а не default significant implementer. Используй для
tests/docs/config, validators, CI/log investigation, deterministic low-risk
fixes, evidence preparation, механических repo-операций и дешёвых bounded
follow-up/correction задач. Default reasoning Medium; Low — deterministic;
High — только если есть конкретная причина не перейти на Sol.

### GPT-6 Luna
Codex bounded/high-volume worker для tests/docs/configs, повторяемой механики,
standalone/desktop automation, evidence work и independent second pass.
Используй, когда задача не требует качества/связности Sol High.

### GLM-5.3
Selective alternative/escalation для interdependent systems или длинной
investigation, если Z-Code harness materially полезен либо Sol недоступен /
не прошёл quality bar. Не вставляй GLM-5.3 автоматически между Flash и Sol.

### GPT-6 Astra
Hardest/highest-stakes escalation после неудачной Sol High попытки, unusually
high cost of error или задачи, где reviewer явно хочет дополнительную модельную
мощность. Не используй Astra только потому, что задача большая.

Практический ориентир:
`GPT-6.1 Sol High primary → ChatGPT review → GLM-5.3-Flash / GPT-6 Luna на bounded хвосты`

При необходимости:
`Sol High → GLM-5.3 или GPT-6 Astra`

Размер context, число файлов и длительность сами по себе не определяют модель.

## 6. Reasoning

Project-facing уровни:
- **Low** — deterministic/mechanical;
- **Medium** — low-risk обычная implementation/support work;
- **High** — default для значимой Sol implementation: player-facing UI/visual,
  runtime C#, lifecycle/state, Save/Load и сложный debugging;
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

### Постоянная локальная схема

Default workflow — **две постоянные папки**, а не новый worktree на каждый pass:

- `master` — чистый exact `origin/master`, reference/recovery checkout;
- `develop` — единственная обычная рабочая Unity-папка с прогретой `Library`.

`develop` — имя папки, не постоянная git-ветка. Для каждой bounded задачи в
этой папке создавай отдельную task branch от свежего `origin/master`. После
review + merge синхронизируй `develop` с новым master и используй ту же папку
для следующей задачи. Это сохраняет Unity Library/cache и не плодит копии
проекта.

Не создавай disposable worktree по умолчанию. Он нужен только как исключение,
если постоянный `develop` реально заблокирован unrelated/uncommitted work,
другой активной задачей или требует изоляции для рискованного эксперимента.
После завершения такой временный worktree удаляется, если в нём нет
незапушенных изменений.

Dirty другой checkout сам по себе не причина создавать свежую Unity Library.
Не трать время на полный cold import, если можно безопасно работать в чистом
warm `develop`.

При mismatch expected base остановись до editing. Если `develop` dirty перед
новой задачей, сначала определи происхождение изменений и сохрани их; не
reset/clean/overwrite user changes. Не force-push и не используй `git add .`.
Stage только task files.

## 10. Validation

- logic/state/save/choice → EditMode;
- runtime/UI/lifecycle → PlayMode;
- prefab/serialized/project integrity → smoke/validators;
- bug fix → regression исходного defect;
- docs-only → обычно Unity tests не нужны;
- player-facing → existing graphical E2E + screenshot inspection + небольшой
  curated baseline при значимом visual pass.

Запускай проверки, соразмерные изменению. Расширяй suite только при failure,
новом риске или unresolved concern. После push mandatory PR check — `CI Gate`:
docs-only без Unity; безопасная in-place Art/Audio asset replacement → smoke;
C#/runtime/UI/scene/prefab/Save/Packages/ProjectSettings/workflow/mixed →
`Unity Test Framework` + `Unity smoke tests`. После merge duplicate Unity CI на
`master` не нужен; reviewer только проверяет exact merged SHA. `workflow_dispatch`
остаётся для exceptional/high-risk full CI.

## 11. Reviewer-visible evidence handoff

Если screenshots/graphical proof materially участвуют в acceptance или reviewer
decision, implementer обязан опубликовать bounded proof так, чтобы reviewer мог
сам открыть исходные кадры. Не оставляй единственную копию evidence в
`C:\Temp`, `QAArtifacts` или другом agent-local архиве.

Порядок публикации:
1. Если meaningful visual pass уже меняет небольшой curated набор
   `docs/visual-baselines/`, используй существующий GitHub `visual-review`
   artifact exact-head CI как основной handoff.
2. Если свежие proof-кадры нужны reviewer'у, но baseline менять не следует,
   загрузи релевантные originals/contact sheet в Google Drive
   `03 — UI — implementation & QA proof`, если upload tool доступен в среде.
3. Если Drive-upload недоступен, создай отдельную временную ветку
   `evidence/<task>` от review-candidate head и добавь только bounded proof set
   + manifest с source PR/head SHA, filenames и SHA-256. Эта ветка не входит в
   task PR, никогда не мержится в `master` и удаляется после reviewer acceptance.

Не публикуй сотни кадров только ради полноты: reviewer должен получить все
состояния, materially нужные для решения, а полный локальный archive может
остаться дополнительным evidence. Build binaries, весь `QAArtifacts` и прочие
массовые generated outputs в task PR/master не коммить.

Финальный report всегда указывает publish location/branch/folder и source head
SHA. Если после реальной попытки ни один publish route недоступен, пиши
`REVIEWER VISUAL PROOF NOT AVAILABLE` с причиной; agent-local inspection тогда
остаётся отдельным уровнем evidence и не выдаётся за independent reviewer proof.

## 12. Delegation

Subagents — не default. Используй их только при реально независимых
workstreams — по умолчанию в Z-Code. Coordinator задаёт
границы, собирает evidence и отвечает за финальный diff. Worker output —
proposal/evidence, не reviewer proof.

Не поручай параллельно нескольким агентам редактировать одну serialized
поверхность. Не используй delegation только потому, что задача «большая».

## 13. Stop conditions и роли

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
