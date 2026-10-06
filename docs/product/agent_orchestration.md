# Оркестрация агентов How I Fall

## Назначение

Этот документ — единая orchestration policy для HIF Supervisor, reviewer'ов
и coding-agent'ов: как выбирать среду, модель, bounded task и merge gate.
Репозиторий — durable source of truth, а не память конкретного чата или
приложения. Execution details принадлежат relevant skills, особенно
`$hif-polish-loop` и `$hif-visual-qa`.

Основной автономный контур живёт локально в Codex: persistent HIF Supervisor
координирует writer/reviewer passes, GitHub/Drive и merge gates. Браузерный
ChatGPT остаётся допустимым manual reviewer/fallback, но не является
обязательным звеном обычного рабочего цикла.

## 1. Перед новой задачей

Reviewer:
1. определяет актуальный `master` и accepted base SHA;
2. читает только relevant repository docs/skills;
3. основной roadmap/approved-task entrypoint — repository `docs/technical_plan.md`; Drive capability map/roadmap — research/history и необязательное зеркало, сверяется при доступных tools;
4. при конфликте предпочитает repository; Drive приводится в соответствие при доступной записи, иначе stale-статус фиксируется явно и не блокирует работу;
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
fixes, evidence preparation, механических repo-операций и bounded
follow-up/correction задач. В autonomous Z-Code support lane для независимого
audit/review допустим **Max**, если лимит Flash не является practical
ограничением; deterministic support всё равно не нужно искусственно усложнять.

### GPT-6 Luna
Codex bounded/high-volume worker для tests/docs/configs, повторяемой механики,
standalone/desktop automation и evidence work. В autonomous loop
**GPT-6 Luna Low** — independent reviewer только для low-risk exact-diff
проходов. Significant/high-risk candidate сразу получает отдельный fresh
**Sol High** review, без обязательной промежуточной дешёвой ступени.

### GLM-5.3
Selective alternative/escalation для interdependent systems или длинной
investigation, если Z-Code harness materially полезен либо Sol недоступен /
не прошёл quality bar. Не вставляй GLM-5.3 автоматически между Flash и Sol.

### GPT-6 Astra
Hardest/highest-stakes escalation после неудачной Sol High попытки, unusually
high cost of error или задачи, где reviewer явно хочет дополнительную модельную
мощность. Не используй Astra только потому, что задача большая.

Практический автономный ориентир:
`Writer → один независимый reviewer по риску → exact-head CI/proof → HIF Supervisor merge`

Для high-risk C#/runtime/UI/lifecycle/Save/scene/prefab work:
`Sol High или явный Flash fallback writer → fresh Sol High read-only review → CI/proof → merge`

Независимый support lane:
`Z-Code + GLM-5.3-Flash Max → audits/tests/docs/config/validators/log investigation`

### Quota-save routing

Autonomous Supervisor читает свежий `account/rateLimits/read` из общего
`CODEX_HOME=D:\Codex`, а не старый session-log. Если в rolling 5-hour **или**
weekly окне осталось **25% или меньше**, включается `QUOTA_SAVE`.

В `QUOTA_SAVE` новые утверждённые bounded задачи выполняет
**Z-Code + GLM-5.3-Flash Max**. По явному решению пользователя от 2026-10-05
разрешены также runtime/UI/Save tasks, но high-risk candidate обязательно ждёт
fresh independent **GPT-6.1 Sol High** review до merge. Если Sol quota исчерпана,
candidate остаётся `WAIT_STRONG_REVIEW`; Luna/GLM не заменяют этот gate.

Это явный routing, не скрытая подмена качества. `UNKNOWN` quota — безопасное
ожидание, не предположение «лимит ещё есть». Одна очередь и последовательный
writer в `agent` или `zagent`; retries сохраняют исходный checkout/partial diff.
Нельзя молча переносить незавершённый diff между engines. Полная недоступность
Codex может остановить и Supervisor; GLM не гарантирует обход недоступного
control/review transport.

### Native runtime/UI QA для Flash

Утверждённый bounded brief явно задаёт `native_validation_profile` и check selection;
поддержаны `agent-control` (принятый fixed infrastructure profile) и `hif-runtime`
с `native_qa={unity:[{mode,filter}],graphical:[Scenario]}`. Точный ограниченный список
и пример — `tools/agent-control/README.md`. Unknown/malformed profile закрывается
до model/QA launch; queue/report не могут задать executable, shell или произвольные
arguments. Flash остаётся Z-Code edit/no-shell/Max, Sol writer flow не меняется.

Native worker запускает только существующие `run-unity-tests.ps1` и
`run-graphical-e2e.ps1` в bound `writer_path` (`agent`/`zagent`); launcher'ы и
изолирующие QA harnesses не должны отличаться от approved base. `develop`/`master`
не используются для QA. Только inspected temp-save harnesses включаются в whitelist;
без доказанной isolation/backup/restore destructive selection блокируется.

Exit 0 не proof: обязательны fresh log, XML с nonzero executed totals без failures,
для graphical — preflight log, PASS sentinel, restoration evidence и все свежие
оригиналы из списка существующего launcher'а. Standard proof — 1920x1080 без
`-nographics`; existing responsive checks launcher'а не удаляются и не расширяются.
Native manifest связывает task/base/head, source fingerprint, invocations и SHA-256;
публикуется небольшой набор (до трёх оригиналов на Scenario), не весь `QAArtifacts`.

`validation_complete` native manifest означает выполненные machine checks, НЕ
visual PASS. При отсутствующем local proof он всегда false, transport запрещён.
GLM не заявляет objective screenshot PASS: эту инспекцию, включая layout/focus/
readability/runtime errors, обязательно выполняет независимый **Sol High** до merge.
Это явное исключение ownership, не ослабление QA. Remote publication — existing
Supervisor transport по review contract; до неё `WAIT_VISUAL_PROOF`, нет clean visual
review/merge. Если разрешённые routes после попыток не работают — `BLOCKED` и
`REVIEWER VISUAL PROOF NOT AVAILABLE`. Receipt не заменяет inspection/readback.

Исправимый native implementation test/compile failure получает `PARTIAL_RETRY`
и correction brief с manifest/log; не более **двух** автоматических correction
попыток. Checkout/branch/engine/resume HEAD и partial diff сохраняются; drift,
missing/zero/stale proof и повторный failure — `BLOCKED`, permission/auth —
`WAIT_AUTH`. Нет reset/clean/stash или переноса diff. Новая quota применяется к
СЛЕДУЮЩЕЙ задаче, не rerouting уже начатого retry. Настоящий GLM runtime/UI pass
после infrastructure fixtures остаётся staged `NOT VERIFIED`, пока не выполнен
на отдельно утверждённой product-задаче.

При необходимости hardest escalation:
`Sol High → GPT-6 Astra`.

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

### Локальный автономный HIF Supervisor

Основной autonomous loop может работать полностью внутри локального Codex без
браузерного ChatGPT в обязательной цепочке.

Роли:
- persistent **HIF Supervisor** — **GPT-6 Luna Low**; выбирает следующий bounded
  pass, координирует writer/reviewer, GitHub/Drive, CI/proof и merge gates;
- **Writer** — **GPT-6.1 Sol High**; делает одну production-задачу и validation;
- fresh **cheap reviewer** — **GPT-6 Luna Low**, read-only только для low-risk exact base/head;
- fresh **strong reviewer** — **GPT-6.1 Sol High**, read-only для
  C#/runtime/UI/lifecycle/Save/scene/prefab/high-risk work либо при escalation;
- **Z-Code / GLM-5.3-Flash Max** — независимый support lane для audits,
  tests/docs/config/validators/CI-log investigation и других непересекающихся
  workstreams.

State machine:
`IDLE → READY → RUNNING → REVIEW_CANDIDATE → REVIEWING`.
Objective defect возвращает задачу в `CORRECTION_READY → RUNNING`; clean
high-risk candidate сразу проходит strong review без обязательного cheap first pass;
clean candidate затем проходит
exact-head PR/CI/proof gate. Только после GREEN acceptance Supervisor делает
merge, через GitHub plugin подтверждает remote master/merge SHA и ставит
`SYNC_MASTER_PENDING`; внешний scheduler нативно синхронизирует чистый `master`
(`hif-control.py sync-master`, fixed checkout/origin, `native_master_sync_enabled`)
и переводит task/state в `MERGED_LOCAL_SYNC_DONE`/`ROADMAP_SYNC_READY`; затем
Supervisor синхронизирует roadmap и выбирает следующий bounded pass. Quota/
interruption → `PARTIAL_RETRY`; решение, которого нет в source of truth,
→ `WAIT_USER`.

Scheduler/Automation — только wake-up mechanism: он не выбирает product direction
сам, а просит persistent Supervisor продолжить из durable state.

Codex Supervisor может использовать подключённые GitHub и Google Drive plugins
напрямую. Remote Desktop Commander допустим как bootstrap/recovery transport, но
пользователь не должен быть copy/paste relay между Supervisor, writer и reviewer.

Каждый execution явно задаёт model/reasoning/sandbox/task scope. Не полагайся на
глобальный model default Codex. Не используй dangerous bypass. Не ослабляй
protected contracts, exact-head CI, reviewer-visible proof и merge gates ради
автономности.

Z-Code — явный quota-save fallback по разделу 5; reviewer/CI/proof contracts
не ослабляются. После окончания approved queue Supervisor один раз сверяет
утверждённый roadmap/подтверждённые defects. Если они исчерпаны, предлагает до
трёх новых product-задач с acceptance и ставит `WAIT_USER`; не выполняет новые
product proposals без согласования и не придумывает canon ради расхода токенов.

Maintainable helpers находятся в `tools/agent-control`; локально остаются одна
`queue.json`, `state.json`, `controller.json` и один startup scheduler. Git
transport после merge (fetch + ff-only чистого `master`) выполняет только внешний
scheduler нативно; модельные сессии не делают git-записи в `master`. Подробная
runtime wiring/проверки — `tools/agent-control/README.md`. Старые worker loops,
вторая Z-Code очередь и recursive Supervisor helper не запускаются.

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

Default human/interactive workflow — **две постоянные папки**, а не новый
worktree на каждый pass:

- `master` — чистый exact `origin/master`, reference/recovery checkout;
- `develop` — обычная рабочая Unity-папка пользователя с прогретой `Library`.

`develop` — имя папки, не постоянная git-ветка. Для каждой bounded задачи в
этой папке создавай отдельную task branch от свежего `origin/master`. После
review + merge синхронизируй `develop` с новым master и используй ту же папку
для следующей задачи. Это сохраняет Unity Library/cache.

Для локального autonomous loop разрешены две дополнительные постоянные
изолированные папки:

- `D:\How_I_Fall\agent` — Codex writer checkout;
- `D:\How_I_Fall\zagent` — Z-Code support/review checkout.

Это не generic правило «плодить worktree», а intentional isolation автоматизации:
`develop` считается protected user checkout и не reset/clean/stash/overwrite.
Writer и Z-Code не должны одновременно редактировать одну task surface.

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
workstreams. Основной production writer остаётся один.

Z-Code Workflows подходят для fan-out/fan-in support passes: несколько
read-only investigator/reviewer веток → один synthesis. Z-Code Automations
подходят для recurring/idle support sweep, но не должны самостоятельно выбирать
новое product direction или параллельно редактировать writer scope.

Coordinator/Supervisor задаёт границы, собирает evidence и отвечает за итоговый
verdict. Worker/subagent output — proposal/evidence, не reviewer proof.

Не поручай параллельно нескольким агентам редактировать одну serialized
поверхность. Не используй delegation только потому, что задача «большая».

## 13. Stop conditions и роли

Остановись и сообщи blocker, если expected base изменился, отсутствует
необходимое product decision, scope неожиданно требует scene/prefab/SaveData
contract change или infrastructure блокирует обязательное proof.

После достижения acceptance не трать остаток budget на дополнительный polish.

**Writer / Implementer** владеет scoped implementation, local tests,
graphical proof и коротким `REVIEW CANDIDATE`/`BLOCKED` report. Writer не
merge'ит собственный candidate.

**Independent reviewer** — один fresh read-only pass: Sol High сразу для
high-risk candidate, Luna Low только для low-risk. Reviewer не принимает
agent report автоматически и проверяет exact diff/gates.

**HIF Supervisor** владеет durable state, выбором следующего bounded pass,
GitHub PR/CI, repository roadmap sync (`docs/technical_plan.md` entrypoint) и
optional Drive mirror — stale/недоступное зеркало явно фиксируется и не блокирует
`DONE` после остальных gates, correction routing, merge и
verdict `DONE`/`NEEDS CORRECTION`. В обычном autonomous mode Supervisor живёт
в Codex; браузерный ChatGPT может выполнить ту же reviewer/supervisor роль как
manual fallback.

**Z-Code / GLM-5.3-Flash** выполняет independent support и явные quota-save
implementation tasks по разделу 5; merge/strong-review gate остаётся у Codex.

**User** нужен для genuinely subjective aesthetic approval, нового product
decision, необходимой authentication/permission или другого неавтоматизируемого
реального blocker.
