# How I Fall — технический план Unity-основы

> **Статус:** живой технический ориентир верхнего уровня. Детальные текущие решения определяются `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md` и relevant `.agents/skills/*`.

## Текущая фаза

**Polished Functional Demo First.**

Приоритет: стабильный функционал, player-facing UX, аккуратный UI, regression coverage, automated/runtime QA и минимальные task-scoped изменения.

Сюжет, routes, canonical flags, финальный art и final visual identity отложены до явного возвращения к актуальному story material.

## Roadmap entrypoint (SourceRepoPlan)

Этот документ — единственный основной roadmap/approved-task entrypoint автономного контура. Документы Drive `02`/`03` остаются research/history и необязательным зеркалом; при конфликте источник истины — репозиторий. Отсутствие Drive write не блокирует `DONE` после остальных обязательных gates, но stale-зеркало помечается явно. Новые product features/canon без user approval здесь не добавляются; существующие принятые решения и защищённые UI/Save контракты сохраняются.

## Базовая архитектура

Сохраняется существующая простая структура:

- `DialogueSceneData` / `DialogueSceneRegistry` — исполняемый dialogue content;
- `VNDialogueController` — VN execution;
- `GameState` — текущий campaign state;
- `SaveManager` / `SaveData` v3 — persistence;
- `SettingsManager` / `GameSettings` — player preferences;
- `AudioManager` — music/SFX/ambience;
- scene-local UI/controllers — player-facing shell;
- Editor validators/tests/E2E — QA.

Не добавлять service locator, DI framework, generic UI framework, manager-per-feature или универсальную narrative VM без доказанной задачи.

## Текущий функциональный фундамент

Уже существуют и не должны повторно планироваться как отсутствующие:

- dialogue/typewriter и принятый Choice UI target v1 (до 4 visible choices);
- typed conditional choices;
- Manual 60 / Auto 6 / Quick 6 saves;
- Continue newest-valid;
- Auto, seen-aware Skip, History/backlog restore;
- bounded in-memory Rollback по stable-line/pre-choice checkpoints с state/backlog/presentation restore;
- Main Menu, Game Menu, Shared Preferences;
- compact Quick Menu: `Назад / История / Пропуск / Авто / Быстр. сохр.`;
- ordinary root Game Menu: Сохранить / Загрузить / Настройки / Главное меню / Выйти + отдельное Вернуться в игру; без root Назад/Откат;
- unified input/help;
- notifications/confirmations;
- relationship deltas/classification; cue-глиф намеренно подавлен в demo-shell (`RelationshipCuePresentationEnabled = false`), а не ожидает обязательного polish;
- Gallery/Replay technical foundation;
- Character Hub technical foundation;
- Chat/Phone technical foundation;
- Interactive Hotspot, Map Locations и Timed Narrative Beat foundations.

## Save compatibility

`SaveData` v3 — защищённый контракт. Не менять schema, slot capacities, migration или ranking без отдельной причины, migration plan и regression coverage.

- v1 принимается только из Manual paths, v2 — Manual/Auto/Quick; миграции in-memory не переписывают старый JSON, неизвестные версии отвергаются;
- `sceneId`/`lineId` должны разрешаться через registry; legacy `lineIndex` fallback допускается только при пустом `lineId`, не при неизвестном непустом ID;
- `selectedChoiceIndex` — исходный индекс choices, не visible slot; rename/delete released IDs и reorder released choices — compatibility risk;
- Save-scoped backlog (до 100 entries) заменяет текущую History при Load, legacy v1/v2 используют пустой fallback;
- rollback buffer и transient special-mode state не сериализуются. Save сохраняет текущий state/backlog, но не buffer; accepted Load/session/Replay/special-mode barriers очищают rollback. Save/Load guards special modes и Replay сохраняются.

Evidence exact-base сверки `40c8dd8881039f472fa578f1a76c02fe8dffb073`: `SaveData`, `SaveManager.ReadSlot`/`TryValidateChoiceState`, `ManualSaveSystemV1SmokeTests`, `BacklogRestorationSmokeTests`, `RollbackBackendPlayModeTests`; общий source-index — `docs/eternum_feature_tracker.md`. Исходники проверены, Unity tests/runtime **NOT RUN** в этом docs-only проходе.

## UI и QA

Для player-facing изменений:

1. минимальный diff;
2. targeted regression;
3. relevant smoke;
4. graphical E2E в реальном runtime;
5. inspection screenshots;
6. небольшой curated `docs/visual-baselines/` set;
7. review-candidate push;
8. GitHub diff/CI review;
9. repository roadmap в candidate diff; Drive capability/roadmap — необязательное зеркало с честным статусом записи.

Стандартная QA resolution — 1920×1080, если задача явно не требует responsive coverage.

## Story pipeline — позже

Когда story work явно возобновится:

- canonical source начинается в `docs/story/` / Markdown;
- сначала story skeleton, scene/route structure и stable IDs;
- Unity assets генерируются/собираются только после утверждения материала;
- importer строится только если реальный объём делает manual conversion повторяющейся проблемой;
- не проектировать generic flags/world database вокруг гипотетического канона.

## Ближайший продуктовый принцип

Перед добавлением новой механики сначала проверить, какую реальную player problem она решает. Для текущей фазы предпочтительнее polish/integration существующих систем, чем расширение feature count.

Rollback/Rewind реализован: 12 in-memory checkpoints, guard 65,536 UTF-16 code units; route — `Назад` в Quick Menu и колесо вниз, не Game Menu/History. Исторический feasibility contract сохранён с актуальной пометкой. Обычный функциональный demo-shell собран; full-demo audit 2026-10-02 и release-candidate audit 2026-10-03 не означают полного release-readiness или runtime PASS текущей базы. Flowchart/chapter/glossary/endings остаются отложенными до настоящего story graph.

## Утверждённая очередь после настройки автономного контура

- Infrastructure: один `HIF Supervisor`, одна очередь, quota fallback, независимое review, exact-head CI/merge и нативная ff-only синхронизация чистого `master`. PR #66 принят; текущий finalization pass проверяется отдельно. Оперативные head/PR/CI/статусы принадлежат `agent-control/queue.json` и `state.json`, а не дублирующему дневнику.
- Утверждённая техническая dependency `glm-native-runtime-qa` (повторное подтверждение пользователя, base `df09ed3d3c017cd751698c0b83e3f1b01e680522`): расширить существующий native worker фиксированными runtime/UI checks, свежим XML/graphical proof, ограниченными correction retries и Sol High ownership объективной инспекции GLM screenshots. Writer acceptance — безопасные fixtures/AST/diff, без Unity в protected `develop` и без реальных user saves. Correction закрывает `PlayMode`/graphical selections до доказанной startup save isolation и bounded affected-state proof; late override после `SaveManager.Awake` не считается изоляцией. Настоящая свежая GLM production-задача остаётся staged `NOT VERIFIED`; продуктовую задачу ради проверки инфраструктуры не придумывать. Independent exact-head review → full remote CI → outer merge/native sync остаются обязательными.
- Новых универсальных production/product-задач сейчас не утверждено. Принятые Main Menu v2 и Hotspot marker polish не переоткрывать без reproduced defect.
- После принятия infrastructure pass: один bounded анализ repository/подтверждённых дефектов, до трёх product-предложений с acceptance, затем `WAIT_USER`. Новые features, визуальное направление и story canon выполняются только после согласования пользователя.
