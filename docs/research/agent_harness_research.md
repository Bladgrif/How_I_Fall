# How I Fall — исследование agent harness и бюджетной оркестрации

## Назначение

Этот документ хранит краткие reusable выводы из внешних материалов про agent harness, context engineering, delegation и visual feedback loops.

Правило: внешняя статья/skill не становится HIF requirement автоматически. В `hif-polish-loop` попадают только конкретные инструкции, которые уменьшают расход, повышают качество или делают Unity-проход безопаснее.

## 1. Dream Loop — iterative visual feedback

Источник: https://github.com/achimala/dream-loop

Статус источника: внешний open-source agent skill; использовать как pattern reference, не копировать pipeline один-в-один.

Полезные выводы для HIF:

- один пользовательский запрос может запускать bounded цикл `baseline/target -> implementation -> screenshot -> critic -> correction`;
- visual critic полезнее общего самоотчёта implementer'а;
- сравнивать нужно конкретные hierarchy / spacing / readability / visual weight / interaction states;
- цикл обязан иметь stop condition по числу итераций или времени;
- для существующего Unity-проекта нельзя подменять product contracts красивым prototype-first результатом.

Применение в HIF:

- одна player-facing surface за loop;
- existing graphical E2E вместо нового framework;
- максимум несколько corrections;
- финал всегда `REVIEW CANDIDATE`, а не самооценка `DONE`.

## 2. Y Combinator — agent harness / context engineering

Источник: https://youtu.be/n9xKblqyQ28

Статус источника: practitioner discussion / conceptual reference.

Полезные выводы для HIF:

- способность агента зависит не только от модели, но и от context selection, environment, memory/state, tool routing и feedback loop;
- сильной модели не нужно каждый раз передавать весь repository;
- context следует рассматривать как ограниченный ресурс: сначала краткий relevant pack, затем расширение только по доказанной dependency;
- planner / implementer / critic / reviewer не обязаны быть одним и тем же дорогим агентом;
- budget должен быть явным constraint задачи, а не остаточным наблюдением после исчерпания лимита.

Применение в HIF:

- context pack сначала <=10 релевантных файлов;
- clean disposable worktree для независимых задач;
- bounded visual iterations;
- дорогой агент используется только там, где нужен сильный judgement, а не для рутинной механики.

## 3. Spotify Portal `shunt` — дешёвое delegation для I/O и boilerplate

Источник: https://github.com/spotify/portal-ai-plugins

Relevant implementation: `plugins/shunt/README.md`.

Статус источника: официальный публичный репозиторий Spotify, Apache-2.0. Сам `shunt` в текущем README описан как Claude Code-only; HIF не добавляет зависимость от Portal/AiKA.

Что реально измерено авторами:

- benchmark на 162K-line Java monorepo;
- bulk-read savings: 82% для одного большого файла, 94% для source+test pair, 94% для multi-file cross-service;
- заявленный mean bulk-read saving = 90%;
- это benchmark конкретных I/O-heavy сценариев, не универсальная гарантия «90% на любой coding task».

Архитектурный pattern:

- `bulk-reader`: большая сырая кодовая масса отправляется дешёвому worker, сильная модель получает компактный ответ на конкретный вопрос;
- `code-writer`: boilerplate генерируется дешёвым worker по точной spec и обязательному reference file;
- сильная модель сохраняет debugging, editing с точным контекстом и архитектурные решения;
- hard/soft routing важнее надежды, что агент сам всегда вспомнит сэкономить контекст.

Что берём в HIF:

1. **Не отправлять Astra полный большой файл только ради обзора.** Сначала targeted read/search; если среда поддерживает дешёвый worker/subagent — делегировать overview туда.
2. **Summary не заменяет exact code при debugging/editing.** Перед правкой сильный implementer читает точные relevant spans сам.
3. **Boilerplate можно делегировать только с reference.** Test/config/helper generation получает существующий HIF-файл как образец и чёткий contract.
4. **Не делегировать judgement.** Root-cause analysis, architecture, Save compatibility, player-facing product decision и reviewer verdict остаются у сильной модели/reviewer.
5. **Worker output считается предложением.** Production diff всё равно проходит HIF tests, graphical QA и reviewer.
6. **Не внедрять Portal в проект ради идеи.** Сначала используем routing policy средствами доступной среды; отдельная external-tool dependency потребует собственного обоснования.

## 4. OpenAI — official GPT-6 Astra model guidance

Источник: https://developers.openai.com/api/docs/guides/latest-model

Статус источника: официальный OpenAI guide для GPT-6 Astra; прямой источник поведения модели и prompt guidance.

Ключевые наблюдения OpenAI, полезные для HIF:

- Astra лучше держит длинные задачи, но чаще останавливается за уточнением там, где более ранняя модель сделала бы разумное предположение;
- Astra чувствительнее к инструкциям в skills и `AGENTS.md`, поэтому конфликтующие или слишком жёсткие правила могут преждевременно заблокировать работу;
- Astra может делегировать меньше, чем ожидается, если явно не задать policy delegation;
- на coding-задачах Astra склонна тестировать тщательнее, чем нужно для маленького изменения;
- OpenAI рекомендует явно настраивать initiative/follow-through, instruction priority, subagent delegation и test breadth.

Что берём в HIF:

1. **Bias to action для уже авторизованной работы.** Если пользователь явно попросил исправить/реализовать bounded task, агент сначала выполняет все reversible/read-only/подготовительные шаги и не останавливается на плане или лишнем вопросе.
2. **Approval только у реальной границы.** Не спрашивать разрешение на read-only действия, isolated worktree, targeted tests и reversible fixes, когда они уже следуют из задачи. Отдельное подтверждение остаётся для genuinely destructive/irreversible действий или когда repository safety contract этого требует.
3. **Instruction priority должна быть явной.** User task определяет конкретную цель; project `AGENTS.md` и skills задают guardrails. Если skill реально блокирует выполнение, агент должен назвать точный `SKILL.md` и правило, а не молча остановиться.
4. **Delegation задаётся policy, а не надеждой.** При доступных subagents дешёвый I/O/boilerplate делегируется; judgement и exact editing остаются подходящей сильной модели.
5. **Calibrated testing.** После targeted regression + required smoke/graphical proof не расширять и не повторять тесты без нового изменения, failure или конкретного unresolved risk. Низко-impact reversible change не требует собственного зеркального теста только ради количества coverage.
6. **Короткий technical report.** Astra склонна к подробным Markdown-ответам; HIF сохраняет короткий report contract, чтобы не расходовать контекст на narrative self-report.

Отдельный вывод для HIF Astra-pass:

- дорогая модель должна получить заранее явные stop conditions;
- не задавать уточняющий вопрос, если bounded intent уже достаточно определён и недостающую деталь можно безопасно вывести из repository/current context;
- после достижения objective acceptance перейти к review candidate, а не продолжать polishing/test expansion из-за собственной склонности к thoroughness.

## 5. SKILL.state — structured execution state вместо растущей истории

Источник: https://arxiv.org/abs/2608.26263

Название: `SKILL.state: Scalable Long-Horizon Agent Skills`.

Авторы: Sanket Badhe, Priyanka Tiwari, Jonghyun Chung; аффилиации Google LLC / Purdue University.

Статус источника: research paper, arXiv 2608.26263; использовать как архитектурный reference для long-horizon agent state, не считать прямой гарантией для Codex/Astra runtime.

Ключевая идея:

- history-based agent каждый шаг тащит всё более длинный transcript;
- `SKILL.state` вместо этого подаёт модели только immutable skill specification, mutable structured execution state и latest observation;
- intermediate reasoning после validated state update не становится частью следующего prompt;
- canonical execution record — текущее структурированное состояние, а не реконструкция из полной истории.

Что реально показано в long-horizon scaling:

- при horizon `T=200` `SKILL.state` получил accuracy `0.94` при примерно `122,384` cumulative tokens;
- summary-memory baseline получил `0.84` при примерно `6,175,509` tokens;
- это около 50× разницы по token consumption в данном эксперименте;
- paper также показывает почти flat prompt footprint для `SKILL.state`, в то время как history-based baselines растут с длиной выполнения.

Что берём в HIF:

1. **Текущее состояние важнее transcript.** Для длинного polish pass держать компактный canonical task-state: goal, accepted base SHA, protected contracts, current iteration, completed work, validated evidence, current blockers, remaining defects, next action, budget left.
2. **State заменяется, а не бесконечно дописывается.** После существенного шага обновлять существующие поля; не хранить в рабочем state полный журнал команд, промежуточные гипотезы и повторяющиеся test logs.
3. **Evidence хранить как ссылки/результаты.** Например `PreferencesInteraction 5/5 PASS`, `PlayerUiGraphicalE2E PASS`, paths к fresh screenshots — а не копировать весь stdout в контекст.
4. **Не переносить stale hypotheses.** Если root cause опровергнут или defect закрыт проверкой, удалить его из active state; исторический narrative остаётся в Git/report при необходимости, но не участвует в следующей итерации.
5. **Latest observation должен быть свежим.** После production change следующая итерация опирается на новый test/screenshot result, а не на старое впечатление.
6. **State recovery сверять с внешней реальностью.** Git SHA/status, Unity test results и screenshots важнее внутренней памяти агента; при расхождении обновить state по repository/runtime evidence.

Ограничение применения:

- обычный repository skill не может сам гарантировать, что runtime физически удалит старые turns из model context;
- поэтому HIF заимствует принцип как **execution discipline**: не перечитывать и не пересказывать историю, поддерживать компактный mutable state и использовать его как canonical checkpoint;
- отдельный state manager/runtime сейчас НЕ добавляем. Если Work/Codex позже даст нативную structured-state/context-compaction primitive, можно адаптировать skill без изменения product code.

Минимальный HIF task-state schema:

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

Этот state — ephemeral agent/worktree metadata, не новая production система и не обязательный committed artifact.

## Текущая модель routing для HIF

- **Luna** — маленькие fixes, docs, deterministic tests/config/boilerplate, дешёвая механическая работа.
- **Terra** — основной Unity/C#/UI implementer, targeted tests и обычные corrections.
- **Sol** — сложный root cause / архитектурный review / взаимозависимые изменения.
- **Astra** — редкий bounded autonomous visual/product pass, когда её высокая стоимость окупается несколькими связанными итерациями.
- **ChatGPT reviewer** — GitHub diff, CI, visual evidence и Drive sync; implementation-agent не объявляет себя `DONE`.

Если execution environment умеет явное model/subagent routing, следовать этой таблице. Если не умеет — не симулировать delegation; просто держать дорогой контекст узким и отдавать маленький correction отдельной более дешёвой сессии позже.
