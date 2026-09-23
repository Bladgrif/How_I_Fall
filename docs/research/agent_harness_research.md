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

**HIF выводит:** сначала нужен небольшой relevant context pack, затем расширение по доказанной dependency; budget и stop conditions задаются заранее.

**HIF не принимает:** обязательную новую planner/critic architecture, полный repository context на каждом шаге или метрики эффективности без локального evidence.

## Spotify Portal `shunt` — дешёвое delegation

Источник: [Spotify Portal AI plugins](https://github.com/spotify/portal-ai-plugins), [`shunt` README](https://github.com/spotify/portal-ai-plugins/tree/main/plugins/shunt). Это официальный публичный Spotify repository; HIF от него не зависит.

**Источник измерил:** на 162K-line Java monorepo bulk-read savings 82% для одного файла, 94% для source+test и 94% для cross-service; заявленный mean — 90%. Эти числа относятся к конкретным I/O-heavy experiments, не к любой coding task.

**HIF выводит:** при реально доступном routing дешёвому worker можно дать deterministic overview или boilerplate с точной spec/reference; перед editing/debugging implementer читает точные source spans; worker output не является proof.

**HIF не принимает:** Portal/AiKA dependency, универсальную гарантию «90% savings» или передачу worker'у root-cause, Save compatibility и product judgement.

## OpenAI — official GPT-6 model guidance

Источник: [OpenAI latest model guide](https://developers.openai.com/api/docs/guides/latest-model). Это official guidance, а не HIF quality benchmark.

**Источник указывает:** GPT-6 model family выбирается по требуемому reasoning, latency и cost; Luna позиционируется как efficient/high-volume модель, Sol — для demanding coding/agentic work, Astra — для hardest end-to-end work. Guidance отдельно подчёркивает initiative/follow-through, чувствительность к instructions в skills/AGENTS, явную delegation policy и соразмерную задаче testing breadth. Reasoning effort остаётся реальным model control.

**HIF выводит:** bounded task должен иметь ясную цель/finish line и protected contracts, после чего агент действует автономно внутри scope, не спрашивая подтверждение каждого safe/reversible шага. Prompt не дублирует выбранный reasoning пустыми усилителями. Testing расширяется только при новом change/failure/risk. В Codex Luna считается high-volume workhorse для focused bounded задач; Sol/Astra — escalation по complexity/risk, а не по размеру prompt.

**HIF не принимает:** предположение, что API pricing/позиционирование напрямую равно subscription allowance, обещание наличия delegation в любой среде или автоматическую эскалацию на Astra/Sol для обычной implementation.

## Anthropic — Opus 5.5 prompting guidance

Источник: пользователь предоставил официальный guide URL `https://claude.dev/blog/getting-the-most-out-of-opus-5-5/` и его ключевые рекомендации. Reviewer tooling не смогло независимо загрузить страницу, поэтому здесь фиксируются только пункты из предоставленного материала, без расширения внешними предположениями.

**Предоставленный источник рекомендует:** меньше process micromanagement, ясную цель и finish line/definition of done, subagents для действительно больших/разделимых задач и отдельный checklist/task state для длинной работы; не засорять prompt бессодержательными «думай тщательно».

**HIF выводит:** сохранять жёсткий bounded scope, но давать агенту автономию внутри него; для длинного pass допустим один mutable checklist; subagents использовать только когда harness и независимость workstreams реально оправдывают overhead.

**HIF не принимает:** обязательный swarm для каждой задачи, бесконечный autonomous run или отказ от HIF review/CI gates.

## SKILL.state — structured execution state

Источник: [SKILL.state: Scalable Long-Horizon Agent Skills](https://arxiv.org/abs/2608.26263), Badhe, Tiwari, Chung (Google LLC / Purdue University). Это research paper, не гарантия поведения coding-agent runtime.

**Источник сообщил:** в своём experiment при horizon `T=200` `SKILL.state` достиг `0.94` accuracy при примерно 122,384 cumulative tokens, против `0.84` и примерно 6,175,509 tokens у summary-memory baseline — около 50× разницы именно в этом experiment.

**HIF выводит:** хранить компактный mutable task-state: goal, base SHA, scope, contracts, acceptance, iteration, completed/evidence, defects, blockers, next action и budget. Fresh Git/runtime/test evidence выше памяти агента; устаревшие hypotheses удаляются из current state.

**HIF не принимает:** claims об «infinite tokens», универсальной 50× экономии, физическом стирании conversation history или новый state manager/database в Unity/repository.

## Применение

`hif-polish-loop` — короткая execution policy, а этот документ — rationale. Для player-facing pass остаётся HIF sequence: minimal diff, targeted proof, existing graphical E2E, screenshot inspection, небольшой curated baseline set, reviewer/CI boundary.
