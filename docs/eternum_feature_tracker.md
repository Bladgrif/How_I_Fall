# How I Fall — карта возможностей и технический tracker

## Назначение

Краткая карта того, **что реально существует в текущем HIF**, чтобы исследования и новые задачи не создавали дублирующие системы. При конфликте с этим файлом сначала проверить current master, production code и tests; затем обновить tracker.

Сводка контрактов сверена с production code и исходниками тестов на exact base `40c8dd8881039f472fa578f1a76c02fe8dffb073` (2026-10-05). Аудиты `docs/research/full_demo_readiness_audit_2026-10-02.md` и `demo_release_candidate_audit_2026-10-03.md` подтверждают готовность обычного функционального demo-shell на своих SHA, но не полный release-readiness gate и не runtime PASS текущей базы. Новые работы начинаются только от воспроизведённого дефекта, явного product decision или предоставленного story/content need.

Текущая фаза: **Polished Functional Demo First**. Сюжет, canonical routes/flags, финальный art и final visual identity сейчас не являются приоритетом.

## Статусы

- ✅ **DONE** — рабочая возможность существует и не должна повторно планироваться как отсутствующая.
- 🟡 **PARTIAL** — техническая основа существует, но остаётся конкретный UX/content gap.
- 🔬 **RESEARCH** — требуется отдельный feasibility/product contract до implementation.
- ⏸ **DEFERRED** — сознательно отложено текущей фазой.
- 🚫 **NOT PLANNED** — не строить без новой явной продуктовой необходимости.

## Актуальная карта возможностей

| Система | Текущее состояние | Статус | Следующий реальный пробел |
|---|---|---:|---|
| Диалог / typewriter | `VNDialogueController`, стабильные scene/line IDs, переходы, завершение печати | ✅ DONE | Контентный tuning позже |
| Обычные выборы / Choice UI | до 4 visible choices, stat deltas, result/transition; принятый target v1 реализован плавающими строками с focus/hover states | ✅ DONE | Authoring только под реальный контент; не переоткрывать polish без конкретного gap |
| Typed conditional choices | typed numeric conditions, hidden unavailable options, safe fallback | ✅ DONE | Authoring только под реальный контент |
| `GameState` | сохраняемые numeric axes/relationships/choice state | 🟡 PARTIAL by design | Нет generic story-flag registry и сейчас он не нужен |
| Manual saves | 10 страниц × 6 = **60** Manual slots, JSON + PNG preview, overwrite/delete/load confirm | ✅ DONE | Сохранять совместимость |
| Auto saves | 6 циклических Auto slots | ✅ DONE | Content-informed checkpoint policy позже |
| Quick saves | 6 циклических Quick slots | ✅ DONE | — |
| `Quick Load` | загружает самое новое валидное **Quick** сохранение | ✅ DONE | Не менять semantics без отдельного contract |
| `Continue` | самое новое валидное Manual/Auto/Quick; invalid newest пропускается | ✅ DONE | — |
| Save compatibility | `SaveData` v3; поддерживаемые старые данные мигрируют in-memory | ✅ DONE / HIGH RISK | Не менять format без явной миграции |
| Главное меню | Продолжить / Новая игра / Загрузить / Настройки / Выйти; focus/hover и confirmation states скорректированы | ✅ DONE | Только конкретный воспроизведённый дефект |
| Reading surface | нейтральная читаемая dialogue/name surface, temporary non-canon chrome скрыт, 125% читаем | ✅ DONE | — |
| Quick Menu | ordinary player-facing: **Назад / История / Пропуск / Авто / Быстр. сохр.**; `Назад` вызывает guarded `TryRollback` | ✅ DONE | Скрытые APIs/hotkeys сохраняются; не возвращать redundant actions без причины |
| Game Menu / Esc | root: Сохранить / Загрузить / Настройки / Главное меню / Выйти + отдельное Вернуться в игру; без rollback/Назад. Esc stack: confirmation → Save/Load → Game Menu → gameplay | ✅ DONE | Сохранять contract |
| Save/Load IA | Save = Manual only; Load = Manual/Auto/Quick через compact family/page navigation | ✅ DONE | Не переписывать backend |
| Player Journey E2E | continuous core player flow | ✅ DONE | Расширять только для новых concrete gaps |
| Auto | reader auto-advance, блокировки на choice/modal | ✅ DONE | Tuning с реальным текстом |
| Seen-aware Skip | безопасный Skip без авто-выбора choices | ✅ DONE | Сохранять semantics |
| Backlog / History | до 100 entries, save-scoped snapshot/restore; cleanup завершён | ✅ DONE | — |
| Rollback / Rewind | bounded in-memory state/backlog/presentation restore: 12 stable-line/pre-choice checkpoints, guard 65,536 UTF-16 code units; `Назад` в Quick Menu и колесо вниз | ✅ DONE | Сохранять hard barriers; не save-system и не rewind из History |
| Notifications / confirmations | toast + safe modal confirmations | ✅ DONE technically | Presentation polish отдельных случаев |
| Relationship consequence feedback | deltas и positive/negative/mixed classification реализованы; cue-глиф намеренно подавлен в demo-shell (`RelationshipCuePresentationEnabled = false`) | ✅ DONE backend / presentation suppressed by design | Не возвращать cue как якобы незавершённый polish; без visible meters |
| Character Hub / bios | technical runtime foundation существует; ordinary launcher скрыт | ✅ DONE foundation / ⏸ content | Реальные characters/bios/art/unlocks позже |
| Shared Preferences | одна общая runtime implementation из Main Menu/gameplay; staged Apply/Back и slider correction завершены | ✅ DONE | — |
| Input / Help | единая `VNInputMap`, Help от неё, no rebinding by design | ✅ DONE | — |
| Audio / ambience | music/SFX + отдельный ambience crossfade foundation | ✅ DONE foundation | Authored clips/scenes позже |
| Gallery / Replay | profile/replay technical foundation, non-canon fixture | ✅ DONE foundation | Canon replay content позже |
| Chat / Phone | typed technical foundation, special-mode guards | ✅ DONE foundation | Authored use позже |
| Interactive Hotspot | normalized hotspots, interaction, guards/return | ✅ DONE foundation | Authored use позже |
| Map Locations | technical runtime map foundation | ✅ DONE foundation | Authored map/content позже |
| Timed Narrative Beat | bounded timed interaction foundation | ✅ DONE foundation | Authored beats позже; не превращать в generic QTE |
| Suspend / Resume | отдельный feasibility audit завершён | ⏸ DEFERRED | Возвращаться только с real-content autosave/lifecycle need |
| Generic minigame / QTE framework | не нужен текущей фазе | 🚫 NOT PLANNED | Только под конкретную сюжетную функцию |

## Последние принятые player-facing проходы

Исторические проходы ниже сохранены. Более поздний Choice target v1 вошёл в master через PR #37 (`743d620`); `docs/visual-baselines/README.md` фиксирует его presentation и proof. Аудит release candidate 2026-10-03 уже перечисляет Choice среди принятых поверхностей; это не новое эстетическое одобрение в данной docs-сверке.

- `91d4f3a7ac4d8c720a064bdd62c737c07e7902cf` — smoke принятого Windows standalone release.
- `606cff442bcdeee0aea1fa10b9adaa0c933f54ab` — Player Journey и demo release-candidate proof.
- `8184ea36f13e536e3b88e151784671fdfb8ae106` — correction Main Menu interaction states: focus/hover, marker и Quit confirmation.
- `1e4705ed804b94c261f7aafe9056e9f1b4014134` — correction Shared Preferences slider после staged Apply/Back pass `2ce584671e4ae474c03fd7e908ec8539f70253c1`.
- `d2323ede6ebc704c8b3a87d9e3a7be700de3766f` — correction player-facing Rollback route в Game Menu; основа: `a19d4e67d147f3fc4e3b390c855a93d7bb2dceae`, `0ee5f9b6cf998b62099880b5609740d42b6ac73a`.
- `68916d49a2be5a4fc4253f869d64b87c6ae5dbef` — correction пустой пагинации Save/Load после основы `a17b024aa08ca73430e13c797f4af8e1e1a8c404`: Manual `1..60`, Load Manual/Auto/Quick, backend semantics сохранены.
- `0f7bedd382176e69c3d9e5205825e0adc17942d5` — correction QA для принятого Choice UI + consequence feedback pass (`35bfe48c684f8262cb43707e288e321328982018`, `07ac9aeca3426940ff95aff445e11d727e668bc9`).

На каждом review-candidate обязательны `Unity Test Framework` и `Unity smoke tests` GREEN плюс релевантный graphical E2E/visual proof. Текущая известная инфраструктурная оговорка: GitHub `Unity Test Framework` временно блокируется внешним Unity Personal licensing (`no available seats`); это не меняет правило DoD и не требует менять CI policy. Unity smoke tests остаются usable.

## Текущий статус следующих продуктовых проходов

Обычный функциональный demo-shell собран; новый generic VN backlog не требуется. Full-demo audit 2026-10-02 сохраняет исторические target/readiness рекомендации, а последующий release-candidate audit — отдельные ограничения release proof. Не превращать эти записи в новые задачи без сверки текущего master. Следующая инженерная работа должна начинаться только от конкретного воспроизведённого дефекта или нового явного product decision. Story/content-dependent work остаётся в разделе ниже.

## Evidence текущей docs-сверки

Это inspection на exact base выше, не запуск Unity: tests/runtime/graphical E2E **NOT RUN** в этой docs-only задаче. Результаты старых аудитов не переносятся на текущий SHA.

| Контракт | Production source | Исходники проверок / repository evidence |
|---|---|---|
| Quick Menu / root Game Menu | `VNQuickMenu.ApplyPlayerFacingPresentation`, `RollbackOnce`; `VNGameMenuView.CreateNavigation` | `QuickMenuRollbackButtonEditModeTests`, `GameMenuSmokeTests`, `RollbackBackendPlayModeTests.GameMenuHasNoRollbackAction_AndQuickRollbackRequiresCheckpoint` |
| Rollback / barriers | `RollbackCheckpoint.cs`, `VNDialogueController.TryRollback`, `IsRollbackRequestAllowed`, `TryEnterSpecialMode`; `SaveManager.ApplyAndRoute` | `RollbackCheckpointEditModeTests` (capacity/deep copy/guard), `RollbackBackendPlayModeTests` (choice/presentation/automation/load/barriers/failure); `VNInputMap` ReadingBack/ReadingForward |
| Choice / demo relationship shell | `VNDialogueController.ApplyChoicePresentation`, `Choose`, `RelationshipCuePresentationEnabled`; `RelationshipFeedback.GetCueKind` | `PlayerUiGraphicalE2ERunner.VerifyFourthChoiceSlot`, `ConditionalChoicesSmokeTests`, `RelationshipFeedbackSmokeTests`, `RollbackBackendPlayModeTests.ChoiceRollback_RestoresAllState_ThenAppliesAlternativeExactlyOnce`; baselines README и release-candidate audit |
| SaveData v3 / History | `SaveData.cs`, `SaveManager.ReadSlot`, `TryValidateChoiceState`; `DialogueBacklog` | `ManualSaveSystemV1SmokeTests`, `BacklogRestorationSmokeTests`; `docs/backlog_restoration_policy.md`, `docs/technical_plan.md` |

Source paths: runtime — `Assets/HowIFall/Scripts/{UI,VN,Save}/`; smoke/E2E — `Assets/HowIFall/Editor/`; NUnit — `Assets/HowIFall/Tests/{EditMode,PlayMode}/`.

## Отложено до реальной истории

- autosave policy вокруг настоящих важных choices;
- flowchart / Story Chart;
- canonical chapter/scene replay authoring;
- glossary/tips/files;
- canonical Character Hub data;
- endings/route completion;
- authored Phone/Chat/Map/Hotspot/Timed Beat usage;
- investigation/evidence UI, если появится конкретная playable detective slice;
- cinematic Timeline/video pass под конкретную сцену.

## Защищённые контракты

Без явной необходимости не менять:
- `SaveData` v3 и migration behavior;
- Manual/Auto/Quick capacities и rotation semantics;
- `Continue` ranking и `Quick Load` quick-only semantics;
- scene/prefab/serialized references;
- протестированный Esc/Game Menu stack;
- unified input map;
- working special-mode coordinator;
- foundations, которые сейчас просто скрыты/deferred;
- unrelated APIs/assets/.meta/Packages/ProjectSettings.

## Главное правило

Если benchmark или новый prompt называет систему из ✅ DONE «отсутствующей функцией», задача неверно сформулирована, пока не назван конкретный defect, UX-gap или content contract. HIF сейчас нуждается прежде всего в **качестве и интеграции**, а не в повторном создании уже существующих систем.

## Язык

Человеко-читаемая документация HIF ведётся по-русски. Технические file paths, class/API/test names и другие идентификаторы сохраняются как в коде.
