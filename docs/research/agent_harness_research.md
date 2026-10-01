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

## OpenAI — GPT-6.1 Sol и current model guidance

Источники:
- [GPT-6.1 Sol model page](https://developers.openai.com/api/docs/models/gpt-6.1-sol);
- [OpenAI model selection](https://developers.openai.com/api/docs/guides/model-selection);
- [Using GPT-6](https://developers.openai.com/api/docs/guides/latest-model);
- [OpenAI API changelog, 2026-09-29](https://developers.openai.com/api/docs/changelog);
- [OpenAI Developer Community announcement: GPT-6.1 Sol](https://community.openai.com/t/gpt-6-1-sol-in-the-api-a-meaningful-cost-performance-step/1402388).

Это external model evidence, не локальный HIF benchmark.

**Official positioning:** GPT-6.1 Sol — current Sol-tier с near-Astra performance для complex coding/computer use/professional work при меньшей стоимости. Model-selection guidance рекомендует Luna для scoped/high-volume work, GPT-6.1 Sol Medium для complex technical work и higher effort для polished/connected deliverables, Astra — для наиболее demanding задач.

**Спецификации/цена API на запуске:** 1.05M context, 128K max output, reasoning `low/medium/high/xhigh/max`; standard pricing $2/M input и $10/M output, то есть uncached цена совпадает с GPT-6 Sol, а cached input снижен с $0.20/M до $0.10/M. API pricing не интерпретируется как subscription quota.

**Опубликованные launch benchmark signals:** OpenAI Developer Community announcement приводит DeepSWE v1.1 `75.2%` у GPT-6.1 Sol High против `68.8%` лучшего результата GPT-6 Sol Max, AutomationBench `31.7%` на Medium (+4.8 п.п. против GPT-6 Sol на том же effort) и OSWorld 2.0 offline `71.4%` против `73.5%` у Astra на Max. В том же материале reported cost per task для этих сравнений существенно ниже Astra/старого Sol. Эти числа относятся к конкретным launch evaluations и не являются гарантией Unity/HIF quality.

**HIF выводит:** GPT-6.1 Sol заменяет GPT-6 Sol как preferred Sol-tier в Codex. Он используется не как default worker, а как selective escalation/finisher: high-visibility UI target matching, ambiguous root cause, lifecycle/state/Save/Load, high regression-risk и polished final correction. GLM-5.3-Flash остаётся default worker; GPT-6 Luna — bounded/high-volume Codex worker; GLM-5.3 остаётся альтернативной escalation при interdependent systems. Astra отодвигается на hardest/high-risk случаи, несколько неудачных попыток или задачи, где GPT-6.1 Sol не проходит quality bar.

**HIF не принимает:** автоматическую замену Flash на Sol из-за новых benchmark цифр, вывод о реальных Plus/Codex лимитах из API pricing, или обещание, что near-Astra aggregate benchmark обязательно означает near-Astra результат на конкретной Unity visual/debugging задаче. Для HIF важнее локальный diff/tests/graphical proof и фактическая стоимость лимита пользователя.

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
