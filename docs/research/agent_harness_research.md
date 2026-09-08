# How I Fall — исследование agent harness и бюджетной оркестрации

## Назначение

Этот документ фиксирует внешние evidence и осторожные выводы для `hif-polish-loop`. Внешние материалы не становятся HIF requirement автоматически: repository contracts и `AGENTS.md` выше них.

## Dream Loop — iterative visual feedback

Источник: [achimala/dream-loop](https://github.com/achimala/dream-loop). Это open-source agent skill, а не Unity/HIF benchmark.

**Источник показывает:** pattern `baseline/target → implementation → screenshot → critique → correction` и bounded visual iteration.

**HIF выводит:** одна player-facing surface, конкретная screenshot inspection и явный stop condition помогают не заменять проверку самоотчётом агента.

**HIF не принимает:** чужой pipeline/layout/assets, prototype-first обход product contracts или бесконечный цикл. Используется существующий graphical E2E, итог implementation agent — `REVIEW CANDIDATE`, не `DONE`.

## Y Combinator — agent harness / context engineering

Источник: [дискуссия Y Combinator](https://youtu.be/n9xKblqyQ28). Это practitioner discussion, не воспроизводимый HIF benchmark.

**Источник утверждает:** качество агента зависит от выбора context, tools, state, routing и feedback loop, а не только от модели.

**HIF выводит:** сначала нужен небольшой relevant context pack (до 10 файлов), затем расширение по доказанной dependency; budget и stop conditions задаются заранее.

**HIF не принимает:** обязательную новую planner/critic architecture, полный repository context на каждом шаге или метрики эффективности без локального evidence.

## Spotify Portal `shunt` — дешёвое delegation

Источник: [Spotify Portal AI plugins](https://github.com/spotify/portal-ai-plugins), [`shunt` README](https://github.com/spotify/portal-ai-plugins/tree/main/plugins/shunt). Это официальный публичный Spotify repository; в README `shunt` описан для Claude Code, HIF от него не зависит.

**Источник измерил:** на 162K-line Java monorepo bulk-read savings 82% для одного файла, 94% для source+test и 94% для cross-service; заявленный mean — 90%. Эти числа относятся к конкретным I/O-heavy experiments, не к любой coding task.

**HIF выводит:** при реально доступном routing дешёвому worker можно дать deterministic overview или boilerplate с точной spec и reference; перед editing/debugging implementer читает точные source spans; worker output не является proof.

**HIF не принимает:** Portal/AiKA dependency, универсальную гарантию «90% savings» или передачу worker'у root-cause, Save compatibility и product judgement.

## OpenAI — official GPT-6 Astra guidance

Источник: [OpenAI latest model guide](https://developers.openai.com/api/docs/guides/latest-model). Это official guidance, а не HIF quality benchmark.

**Источник указывает:** инициативность, instruction priority, delegation policy и test breadth стоит задавать явно; длительные задачи требуют ясных stop conditions.

**HIF выводит:** при ясной bounded задаче агент действует без лишнего уточнения безопасными/reversible шагами, не перечитывает большой corpus и не расширяет testing без нового change/failure/risk. Routing: Luna — tiny mechanical work, Terra — ordinary Unity/UI, Sol — сложный diagnosis/state, Astra — редкий дорогой visual/product pass.

**HIF не принимает:** обещание, что конкретный runtime умеет delegation или что Astra должна доминировать обычные passes. При отсутствии реальных subagents routing не имитируется.

## SKILL.state — structured execution state

Источник: [SKILL.state: Scalable Long-Horizon Agent Skills](https://arxiv.org/abs/2608.26263), Badhe, Tiwari, Chung (Google LLC / Purdue University). Это research paper, не гарантия поведения Codex runtime.

**Источник сообщил:** в своём experiment при horizon `T=200` `SKILL.state` достиг `0.94` accuracy при примерно 122,384 cumulative tokens, против `0.84` и примерно 6,175,509 tokens у summary-memory baseline — около 50× разницы именно в этом experiment.

**HIF выводит:** хранить компактный mutable task-state: goal, base SHA, scope, contracts, acceptance, iteration, completed/evidence, defects, blockers, next action и budget. Fresh Git/runtime/test evidence выше памяти агента; устаревшие hypotheses удаляются из current state.

**HIF не принимает:** claims об «infinite tokens», универсальной 50× экономии, физическом стирании conversation history или новый state manager/database в Unity/repository.

## Применение

`hif-polish-loop` — короткая execution policy, а этот документ — rationale. Для player-facing pass остаётся HIF sequence: minimal diff, targeted proof, existing graphical E2E, screenshot inspection, небольшой curated baseline set, reviewer/CI boundary.
