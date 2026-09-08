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

## Текущая модель routing для HIF

- **Luna** — маленькие fixes, docs, deterministic tests/config/boilerplate, дешёвая механическая работа.
- **Terra** — основной Unity/C#/UI implementer, targeted tests и обычные corrections.
- **Sol** — сложный root cause / архитектурный review / взаимозависимые изменения.
- **Astra** — редкий bounded autonomous visual/product pass, когда её высокая стоимость окупается несколькими связанными итерациями.
- **ChatGPT reviewer** — GitHub diff, CI, visual evidence и Drive sync; implementation-agent не объявляет себя `DONE`.

Если execution environment умеет явное model/subagent routing, следовать этой таблице. Если не умеет — не симулировать delegation; просто держать дорогой контекст узким и отдавать маленький correction отдельной более дешёвой сессии позже.
