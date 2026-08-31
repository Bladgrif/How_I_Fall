# How I Fall — карта возможностей и технический tracker

## Назначение

Краткая карта того, **что реально существует в текущем HIF**, чтобы исследования и новые задачи не создавали дублирующие системы. При конфликте с этим файлом сначала проверить current master, production code и tests; затем обновить tracker.

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
| Обычные выборы | варианты, stat deltas, result/transition | ✅ DONE | Visual/interaction polish выбора |
| Typed conditional choices | typed numeric conditions, hidden unavailable options, safe fallback | ✅ DONE | Authoring только под реальный контент |
| `GameState` | сохраняемые numeric axes/relationships/choice state | 🟡 PARTIAL by design | Нет generic story-flag registry и сейчас он не нужен |
| Manual saves | 10 страниц × 6 = **60** Manual slots, JSON + PNG preview, overwrite/delete/load confirm | ✅ DONE | Сохранять совместимость |
| Auto saves | 6 циклических Auto slots | ✅ DONE | Content-informed checkpoint policy позже |
| Quick saves | 6 циклических Quick slots | ✅ DONE | — |
| `Quick Load` | загружает самое новое валидное **Quick** сохранение | ✅ DONE | Не менять semantics без отдельного contract |
| `Continue` | самое новое валидное Manual/Auto/Quick; invalid newest пропускается | ✅ DONE | — |
| Save compatibility | `SaveData` v3; поддерживаемые старые данные мигрируют in-memory | ✅ DONE / HIGH RISK | Не менять format без явной миграции |
| Главное меню | Продолжить / Новая игра / Загрузить / Настройки / Выйти | ✅ DONE structurally | Только конкретный polish/bug |
| Reading surface | нейтральная читаемая dialogue/name surface, temporary non-canon chrome скрыт, 125% читаем | ✅ DONE | Choice/History polish отдельными проходами |
| Quick Menu | player-facing: **История / Пропуск / Авто / Быстр. сох.** | ✅ DONE | Скрытые APIs/hotkeys сохраняются; не возвращать redundant actions без причины |
| Game Menu / Esc | Esc stack: confirmation → Save/Load → Game Menu → gameplay | ✅ DONE | Сохранять contract |
| Save/Load IA | Save = Manual only; Load = Manual/Auto/Quick через compact family/page navigation | ✅ DONE | Не переписывать backend |
| Player Journey E2E | continuous core player flow | ✅ DONE | Расширять только для новых concrete gaps |
| Auto | reader auto-advance, блокировки на choice/modal | ✅ DONE | Tuning с реальным текстом |
| Seen-aware Skip | безопасный Skip без авто-выбора choices | ✅ DONE | Сохранять semantics |
| Backlog / History | до 100 entries, save-scoped snapshot/restore | ✅ DONE | Visual cleanup при конкретных дефектах |
| Rollback / Rewind | reversible execution state отсутствует | 🔬 RESEARCH | Явно открыт для bounded feasibility; visual-only Back запрещён |
| Notifications / confirmations | toast + safe modal confirmations | ✅ DONE technically | Presentation polish отдельных случаев |
| Relationship consequence feedback | stat delta применяется; сейчас есть явный text feedback | 🟡 PARTIAL | Короткий nonverbal cue без текста/чисел |
| Character Hub / bios | technical runtime foundation существует; ordinary launcher скрыт | ✅ DONE foundation / ⏸ content | Реальные characters/bios/art/unlocks позже |
| Shared Preferences | одна общая runtime implementation из Main Menu/gameplay | ✅ DONE | Только concrete layout/UX defects |
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

- `218421803a58374b39057f3c13cf139869fec2fa` — исправление interaction states: focus/hover, Main Menu marker, Quit confirmation, Preferences/Choice objective defects.
- `a1df10aac4fef2dc788478eea4144c6452786a51` — reading surface + compact Quick Menu.
- `8b16bba34da1136417e414030660270a544a64e3` — Save/Load information architecture: Manual-only Save, compact Manual/Auto/Quick Load navigation, backend semantics сохранены.

На каждом таком review-candidate обязательны `Unity Test Framework` и `Unity smoke tests` GREEN плюс релевантный graphical E2E/visual proof.

## Ближайшие продуктовые проходы

1. **Choice UI + subtle consequence feedback** — visual/interaction polish выбора и nonverbal relationship cue без чисел/спойлерного текста.
2. **Preferences / History visual cleanup** — только подтверждённые remaining defects, без system rewrite.
3. **Rollback / Rewind feasibility contract** — state restoration/checkpoints/barriers; implementation только после принятого bounded contract.
4. **Integrated player journey / demo release candidate** после завершения принятых correction passes.

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
