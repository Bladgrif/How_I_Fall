# How I Fall — технический план Unity-основы

> **Статус:** живой технический ориентир верхнего уровня. Детальные текущие решения определяются `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md` и relevant `.agents/skills/*`.

## Текущая фаза

**Polished Functional Demo First.**

Приоритет: стабильный функционал, player-facing UX, аккуратный UI, regression coverage, automated/runtime QA и минимальные task-scoped изменения.

Сюжет, routes, canonical flags, финальный art и final visual identity отложены до явного возвращения к актуальному story material.

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

- dialogue/typewriter и choices;
- typed conditional choices;
- Manual 60 / Auto 6 / Quick 6 saves;
- Continue newest-valid;
- Auto, seen-aware Skip, History/backlog restore;
- Main Menu, Game Menu, Shared Preferences;
- compact Quick Menu;
- unified input/help;
- notifications/confirmations;
- Gallery/Replay technical foundation;
- Character Hub technical foundation;
- Chat/Phone technical foundation;
- Interactive Hotspot, Map Locations и Timed Narrative Beat foundations.

## Save compatibility

`SaveData` v3 — защищённый контракт. Не менять schema, slot capacities, migration или ranking без отдельной причины, migration plan и regression coverage.

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
9. Drive capability/roadmap sync.

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

Rollback/Rewind не реализован: он открыт только для отдельного feasibility contract с state-safe restoration и hard barriers. Flowchart/chapter/glossary/endings остаются отложенными до настоящего story graph.
