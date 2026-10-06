# How I Fall

- Визуальная новелла на Unity/C#. Предпочитай минимальные изменения строго в рамках задачи и не делай несвязанного рефакторинга.
- Читай только файлы и системы, относящиеся к задаче, если зависимость явно не требует расширить область работы.
- Уже принятые repository/roadmap решения считай ограничениями, а не поводом заново их обсуждать. Переоткрывай решение только при новом воспроизводимом дефекте, противоречии source of truth или явном запросе пользователя.
- Не меняй игровые сцены, prefab-файлы, сериализованные ссылки, формат сохранений, `ProjectSettings` и `.meta`, если задача этого не требует. Сохраняй работающие API и пользовательские изменения.
- Новое или изменённое поведение должно получать регрессионное покрытие, когда это разумно автоматизировать. Для чистой логики используй EditMode NUnit, для runtime/UI lifecycle — PlayMode NUnit, для сцен/prefab/serialized/project integrity — существующие smoke/validators. Для исправления бага по возможности добавляй тест исходного дефекта. Изменения только документации обычно не требуют тестов.
- Предпочитай обычные тесты Unity Test Framework NUnit. Пользуйся специальными graphical/runtime E2E только для реальных runtime-потоков, Game View, скриншотов или жизненного цикла Editor.
- После реализации: проверь ошибки компиляции, запусти узкий целевой тест, затем релевантные regression/smoke проверки. Для player-facing работы запускай runtime/E2E proof, если среда это позволяет, просматривай визуальные доказательства, исправляй объективные дефекты в рамках задачи и повторяй проверку.
- Успешная компиляция не доказывает правильность поведения. Никогда не заявляй, что тест прошёл, если он не запускался; непройденные проверки отмечай `NOT RUN`.
- Полный контракт ревью, graphical proof и visual baselines описан в `docs/product/review_workflow.md`.

## Автономность и finish line

- Task brief должен задавать цель, bounded scope и объективный finish line/acceptance criteria. Внутри этих границ агент сам выбирает разумный путь exploration → implementation → validation и не требует подтверждения каждого безопасного/reversible шага.
- Не микроменеджерь внутренний процесс без причины. Указывай конкретные ограничения, protected contracts и критерии готовности вместо длинной последовательности «сначала сделай X, потом Y», если порядок не является частью correctness.
- Не добавляй в prompt пустые усилители вроде `think carefully`, `be extremely thorough` или `take your time`. Нужная глубина задаётся выбором модели/reasoning и конкретными acceptance criteria.
- Не останавливайся на плане, если задача разрешает implementation и нет реального blocker. Доводи bounded pass до проверки и требуемого review-candidate состояния.
- Перед вопросом пользователю сначала проверь, можно ли безопасно продолжить из repository context. Спрашивай только если недостающая информация реально может изменить результат, затронуть защищённый contract или расширить scope.
- Для длинного прохода можно вести один компактный mutable task-state/checklist; не превращай его в растущий дневник и не коммить без отдельной причины.

## Модельный routing

- Для **значимой HIF implementation** по умолчанию используй **Codex + GPT-6.1 Sol + High**, если модель доступна и пользовательский лимит позволяет: player-facing UI/visual polish, runtime C#, lifecycle/state, Save/Load, связанные multi-file изменения, ambiguous root cause и high-regression-risk work.
- Оптимизируй не минимальное число токенов первого прохода, а **total cost of quality**: хороший первый implementation предпочтительнее дешёвого прохода, который затем приходится существенно переделывать.
- Независимое ревью — одна обязательная ступень: **GPT-6.1 Sol High** сразу для significant/high-risk runtime/UI/Save/scene/prefab/automation/workflow; **GPT-6 Luna Low** только для low-risk bounded docs/mechanical work. Эскалация дешёвого ревью допустима при обнаружении нового риска; дешёвый предварительный проход не обязателен перед сильным.
- **GLM-5.3-Flash** — independent support lane и явный quota-save writer; runtime/UI fallback разрешён только с обязательным сильным независимым review до merge.
- Autonomous Supervisor читает свежий `account/rateLimits/read`. При остатке **25% или меньше** в rolling 5-hour или weekly окне включается `QUOTA_SAVE`: новые bounded tasks идут в **Z-Code + GLM-5.3-Flash Max**, включая runtime/UI по явному разрешению пользователя. Для high-risk candidate обязательно независимое **Sol High** review до merge; при недоступности reviewer — `WAIT_STRONG_REVIEW`, без снижения gate.
- **GLM-5.3** и **GPT-6 Astra** — selective escalation/fallback, а не обязательная ступень: используй при конкретной причине, недоступности/неудаче Sol High или unusually high cost of error.
- В autonomous mode persistent **HIF Supervisor** в Codex владеет task selection, correction routing, PR/CI/proof gate, merge и roadmap sync; browser ChatGPT — manual reviewer/fallback, а не обязательное звено.
- Полная policy выбора среды, reasoning, сессии и autonomous orchestration находится в `docs/product/agent_orchestration.md`.

## Язык документации

- Документация HIF, предназначенная для чтения человеком, должна быть написана по-русски.
- Имена файлов и путей репозитория не переименовываются только ради перевода: они считаются техническими идентификаторами.
- Имена классов, API, методов, тестов, Unity-сцен, enum, hotkey, форматов и других идентификаторов сохраняются как в коде и выделяются обратными кавычками при необходимости.
- Названия игр, продуктов и внешних источников не переводятся искусственно, если это ухудшает точность поиска.

## Визуальный и ручной QA

- Стандартное разрешение QA — **1920x1080**. Не добавляй автоматизацию нескольких разрешений, если задача явно не проверяет адаптивность.
- Для player-facing работы переиспользуй ближайший существующий QA launcher из `How I Fall/QA/<Feature Name>`; не создавай новый launcher для каждой мелкой кнопки. Launcher не заменяет автоматические тесты.
- Graphical/runtime E2E и скриншоты не должны запускаться с `-nographics`. Проверка coding-agent — автоматизированное доказательство, а не человеческий Manual QA PASS.
- Проси ручной QA пользователя только для субъективного визуального вкуса/атмосферы, если визуальное доказательство недоступно или есть реальный пробел автоматизации. Объективные критерии должны проверяться автоматикой и runtime proof.
- Если graphical/screenshots входят в evidence review candidate, reviewer должен получить **удалённо доступный proof**, а не только локальный путь coding-agent. Приоритет: существующий GitHub `visual-review` artifact для реально изменённых curated baselines; иначе Google Drive `03 — UI — implementation & QA proof`; если Drive-upload в среде недоступен — временная GitHub-ветка `evidence/<task>` с ограниченным набором релевантных PNG и manifest (`source PR/head SHA`, filenames, SHA-256). Evidence-ветка не мержится и удаляется после reviewer acceptance.
- Локальные `C:\Temp`, `QAArtifacts` или agent-local archive не считаются достаточным reviewer visual proof, если доступен хотя бы один publish route. Если публикация реально не удалась после попытки, явно пиши `REVIEWER VISUAL PROOF NOT AVAILABLE` и причину. Не коммить весь `QAArtifacts`, build payload или массовый screenshot dump в task PR/master.

## Git и завершение задачи

- Для обычной interactive работы предпочитай **две постоянные папки**: `master` и `develop`. `master` — чистый exact `origin/master` и эталон; `develop` — рабочая Unity-папка пользователя с прогретой `Library`.
- Для локального autonomous loop разрешены отдельные постоянные checkout'ы `D:\How_I_Fall\agent` (Codex writer) и `D:\How_I_Fall\zagent` (Z-Code support). Это intentional isolation; `develop` остаётся protected user checkout.
- После merge локальная синхронизация чистого `master` (fetch + ff-only exact merge SHA) выполняется только внешним scheduler'ом нативно через `tools/agent-control/hif-control.py sync-master` (`controller.native_master_sync_enabled=true`); модельные сессии не выполняют git-записи в `master`.
- Имя папки `develop` **не означает постоянную git-ветку `develop`**. Под каждую interactive задачу в этой папке создавай task branch от свежего `origin/master`; после merge синхронизируй папку с новым master и начинай следующую task branch там же.
- Не создавай отдельный disposable worktree для каждой задачи по умолчанию. Используй его только как исключение, если постоянный `develop` реально заблокирован несвязанными/незакоммиченными изменениями или другой активной работой.
- Не выбрасывай прогретую Unity `Library` и не запускай полный импорт свежей Library только потому, что другой checkout dirty. Для обычной работы переиспользуй warm `develop`.
- Если `develop` dirty перед новой задачей, сначала выясни происхождение изменений и сохрани их; не reset/clean/overwrite. В нормальном завершённом цикле после merge `develop` должен вернуться к чистому состоянию.
- Не добавляй в stage несвязанные пользовательские изменения и не используй `git add .`, если worktree содержит изменения вне задачи.
- Не делай destructive reset пользовательских изменений.
- Не создавай commit/push, если задача явно этого не просит.
- После push техническая задача не считается полностью проверенной, пока GitHub `CI Gate` не зелёный. Gate пропорционален diff: docs-only не запускает Unity; безопасная in-place Art/Audio asset replacement запускает smoke; C#/runtime/UI/scene/prefab/Save/Packages/ProjectSettings/workflow/mixed запускает `Unity Test Framework` + `Unity smoke tests`.
- Review candidate не становится `DONE`, пока reviewer не проверил реальный commit/diff и не синхронизировал живую product roadmap по правилам `docs/product/review_workflow.md`. При рассинхронизации репозиторий остаётся главным источником истины.
- Для выбора среды/модели/сессии, reasoning, бюджета контекста и orchestration implementation prompts используй `docs/product/agent_orchestration.md`; execution-specific player-facing loops остаются в `$hif-polish-loop`.

## Синхронизация дорожной карты reviewer'ом

- Основной roadmap/approved-task entrypoint в репозитории — `docs/technical_plan.md` (SourceRepoPlan). Документы Drive `02`/`03` остаются research/history и необязательным зеркалом; repository главный источник истины.
- Отсутствие Drive write не блокирует `DONE` после остальных обязательных gates, но stale-статус Drive фиксируется явно; никогда не заявлять синхронизированное зеркало без реальной записи.
- После каждого review-candidate push reviewer сравнивает результат с текущей картой возможностей HIF и упорядоченной дорожной картой до выбора следующей задачи coding-agent.
- Необязательное исследовательское зеркало находится на Google Drive: `How I Fall/Исследования и дорожная карта/Бенчмарк UI UX визуальных новелл 2026-08-31/`.
- Документы зеркала: `02 — Карта возможностей HIF и решения по бенчмарку` и `03 — Дорожная карта Polished Functional Demo`; `01` и `05` используются, когда нужна аргументация или проверка источников.
- Reviewer фиксирует SHA проверенного commit, статус CI, статус прохода (`DONE`, `PARTIAL`, `BLOCKED`, `NEEDS CORRECTION`) и следующий ограниченный проход. Если возможности проекта существенно изменились, синхронизируется и карта возможностей.
- Не выбирай следующий implementation pass только по памяти чата. Если репозиторий и Drive расходятся, предпочитай репозиторий; обновляй Drive только при доступной записи, иначе отмечай `NOT SYNCED`.

## Финальный отчёт coding-agent

Отчёт должен быть коротким:
1. изменённые файлы;
2. что реализовано;
3. реально запущенные тесты и результаты;
4. `NOT RUN`;
5. graphical/manual QA только если действительно нужен;
6. оставшиеся риски.
