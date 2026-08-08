# How I Fall — Eternum Feature Tracker

## Назначение

Компактный roadmap по UX-механикам, повторно проверенным в локальной русской сборке **Eternum 0.9.5**. Полная инвентаризация 199 пунктов A–V, edge cases и источники вынесены в [eternum_full_feature_audit.md](eternum_full_feature_audit.md).

- Срез Eternum: source-only аудит 25 верхнеуровневых `.rpy`, 2026-08-07.
- Срез How I Fall: текущие C#-скрипты, Unity-сцены, данные и тесты на functional `1d1d7dabf927dd764c6272877d038da9927b1bb0` и docs `ab64517f2b66915028c0942895c1c83c8b726033`.
- Граница: переносим только полезное поведение. Не копируем код, тексты, визуал, аудио, layout и сюжетные элементы Eternum.
- Runtime Eternum в этой задаче не запускался; Unity 6000.5.7f1 прошёл общий CI/validator/scene validation и оба graphical Save E2E на `9d7be27db11ffcdabc6bb2ec56845440c6647b2f`.

## Статусы

- ✅ **DONE** — рабочий контур уже есть.
- 🟡 **PARTIAL** — основа есть, но сценарий неполон или часть UI не действует.
- ⬜ **TODO** — отсутствует; начинать только по roadmap/сцене.
- 🚫 **NOT PLANNED** — сознательно не брать в ближайший план.

## Актуальный срез

| Система | How I Fall сейчас | Статус | Следующий пробел | Приоритет / риск |
|---|---|---:|---|---|
| Диалог и typewriter | `VNDialogueController`, `DialogueSceneData`, завершение текущей печати, переходы между сценами | ✅ DONE | Контентная проверка темпа; диапазон `textSpeed` требует QA | Medium / Low |
| Обычные выборы | Варианты, stat-delta, результат выбора и переход | ✅ DONE | Не смешивать с ещё не начатыми условиями | — / Low |
| Conditional choices | Typed numeric `ChoiceCondition` gates for the nine saved `GameState` integers; hidden unavailable options, transient source-index mapping, zero/capacity fail-safe and focused smoke coverage. | DONE | Author conditional content only where a scene needs it; keep a default route for all-hidden branches. | Medium / Low |
| Game state | Оси, доверие/интерес и состояние выбора сохраняются | 🟡 PARTIAL | Нет общего реестра флагов, ресурсов и unlock-state | Medium / Medium |
| Ручные сохранения | 6 Manual-слотов, JSON + PNG 384×216, overwrite/delete/load confirmations | ✅ DONE | Поддерживать совместимость | — / High |
| Auto/Quick saves | По 6 циклических Auto/Quick-слотов; quick save/load доступны из VN UI и hotkeys | ✅ DONE | Проверять точки autosave с новым контентом | — / Medium |
| Continue | Загружает самое новое валидное Manual/Auto/Quick; в Eternum отдельной активной кнопки нет | ✅ DONE | — | — / Low |
| Совместимость saves | `SaveData` v3; v1 Manual и v2 Manual/Auto/Quick мигрируют только in-memory, следующий save пишет v3 | ✅ DONE | Любое расширение формата требует явной миграции | — / High |
| Quick menu | History, Skip, Auto, Save, Quick Save, Quick Load, Load, Settings, Main Menu | ✅ DONE | Back сознательно отсутствует | — / Medium |
| Auto | Таймер диалога, блокировка на choice/modal, согласование со Skip | ✅ DONE | QA задержки на длинных строках | Low / Low |
| Skip | Seen-aware, не выбирает варианты, согласован с Auto | ✅ DONE | Ctrl сейчас переключает режим, а не работает удержанием как в Eternum | Low / Medium |
| Backlog | До 100 raw speaker/text entries; Manual/Auto/Quick сохраняют save-scoped snapshot, Load/Continue заменяют History без merge и дублей | ✅ DONE | Поддерживать v1/v2 fallback и проверять новые restore beats | — / High |
| Rollback | Обратимого состояния исполнения нет | 🚫 NOT PLANNED | Нужна отдельная модель границ и обратимости | — / High |
| Уведомления/confirm | Toast и модальные подтверждения применяются в save/load UX; тот же toast показывает feedback об изменении отношений после ручного выбора | ✅ DONE | Проверять читабельность сообщений при новом контенте | High / Low |
| Relationships | После ручного выбора существующий toast показывает применённые изменения `trustMasha`, `trustArtem` и `leraInterest` без чисел; порядок Masha → Artem → Lera | ✅ DONE | Контентно проверять формулировки при добавлении новых персонажей/отношений | — / Low |
| Character hub / bios | Отдельного экрана отношений и биографий нет | ⬜ TODO | Только после появления контентной потребности | Low / Medium |
| Settings | Main Menu and VN Settings share `SettingsManager`: audio, text speed, auto, skip, autosave, display mode, resolution and background mode apply immediately; unsupported controls are hidden | DONE | Verify runtime Screen API and UI when future display/localization/theme systems are added | Medium / Low |
| Input/help | `VNInputMap` — единый source-of-truth для player hotkeys; Main Menu Help строится из него | ✅ DONE | Rebinding сознательно отсутствует; graphical QA Help pending | Medium / Low |
| Audio | Music/SFX работают; отдельного ambience-исполнителя нет | 🟡 PARTIAL | Не показывать неработающий ambience либо добавить канал под сцену | Medium / Medium |
| Gallery/replay | Кнопка Gallery пока сообщает `not implemented`; unlock/replay scope отсутствуют | ⬜ TODO | Делать только вместе с первым реальным extra | Low / High |
| Chat/phone | Отдельного формата сцены нет | ⬜ TODO | Typed conditions/effects, медиа и возврат в VN | Low / Medium |
| Hotspots/map | Координатных интерактивных сцен и карты нет | ⬜ TODO | Нужны accessibility и modal-return contract | Low / Medium |
| QTE/special mode | Scene-local `VNDialogueController` coordinator owns one exclusive special lease with fail-closed `BlockingExclusive`; authored special scenes remain absent | DONE | Keep map/QTE/chat as separately scoped work with their own local state and exit route | Medium / High |
| Mini-games | Отсутствуют | 🚫 NOT PLANNED | Не строить без утверждённой сюжетной функции | — / High |

## Проверенные различия с прежним tracker

- Quick Save и Quick Load больше не `PARTIAL`: обе команды подключены в `VNQuickMenu`; `F6` — quick save, `F8` — quick load.
- Реальные player bindings: `Ctrl` — Toggle Skip, `F5` — Save, `F6` — Quick Save, `F8` — Quick Load, `F9` — Load, `B` — Backlog, `Esc` — Back/Close. `F2`/`F3` остаются internal debug bindings и не показываются в Help.
- Ctrl в How I Fall **переключает** Skip. Это осознанно не совпадает с удержанием Ctrl в Eternum и явно показано в Help.
- Quick menu, Auto и seen-aware Skip уже готовы; их нельзя повторно планировать как отсутствующие механики.
- Continue — собственное улучшение How I Fall, а не функция для копирования из активного главного меню Eternum.
- Настройки resolution, refresh rate, language, font size, game look/interface style и часть animation toggles сохраняются, но пока не меняют игру. Их нельзя отмечать `DONE`.
- Backlog реализован по [backlog_restoration_policy.md](backlog_restoration_policy.md): `SaveData` v3 хранит save-scoped snapshot, Load/Continue заменяют runtime History, v1/v2 используют empty fallback с одной восстановленной visible line/resultText. Rollback, общий unlock registry и gallery/replay отсутствуют; special-mode coordinator now has a scene-local runtime bridge with opaque leases, permission gates and focused smoke coverage; authored special scenes remain absent. Relationship feedback remains transient toast and is not saved.

## Рекомендуемые следующие механики

Порядок учитывает narrative value, существующие зависимости, размер, риск и полезность для будущих сцен.

| # | Механика | Почему сейчас | Зависимости | Размер | Риск | Решение |
|---:|---|---|---|---|---|---|
| 1 | Settings truth pass | Removed false UI affordances and connected small runtime consumers | `SettingsManager`, both Settings panels | Small | Low | **DONE** |
| 2 | Unified input map + Help | Canonical map drives runtime and Main Menu Help; no rebinding framework | VN actions, modal policy | Medium | Low | **DONE** |
| 3 | Backlog restoration | Save-scoped v3 snapshot без merge; legacy fallback и scoped suppression проверены | v3 schema, migration/tests, graphical E2E | Medium | High | **DONE** |
| 4 | Typed conditional choices | Typed numeric AND conditions, source-index mapping and fail-safe fallback are implemented and covered by CI. | `ChoiceCondition`, `GameState`, dialogue validator | Medium | Low | **DONE** |
| 5 | Unified modal/special-mode coordinator | Scene-local exclusive owner, opaque lease, fail-closed permissions and normal-modal entry gates are implemented; map/QTE/chat remain deferred. | [special_mode_coordinator_policy.md](special_mode_coordinator_policy.md), input/modal entry gates | Medium | High | **DONE** |
| 6 | Hide UI + screenshot UX | `H` enters transient clean view; H/Esc restore without dialogue advance. Dialogue shell and Quick Menu hide; authored background/character remain. System/Steam capture stays player-owned. | `VNInputMap`, `VNDialogueController`, `VNQuickMenu` | Small | Low | **DONE** |
| 7 | Ambience channel/crossfade | Поддерживает скрытую тревогу и разделяет ambience от SFX | `AudioManager`, настройки, scene command | Medium | Medium | Под конкретную сцену |
| 8 | Timed narrative beat | Лёгкое напряжение без полноценной mini-game системы | special-mode contract, success/fail routing | Medium | Medium | После coordinator |
| 9 | Gallery/replay foundation | Нужна для Extra только когда есть реальный replay-контент | unlock registry, scoped state, safe return | Large | High | Later |

## Единственный NEXT

### Ambience channel/crossfade

Implement a separate ambience channel and a small scene command only when an authored scene needs it. Keep music and SFX stable; do not start it automatically from this tracker update.

**Out of NEXT:** relationship screen, rollback, gallery, map, QTE, chat/phone, mini-games and screenshot file capture.

## Отложено или исключено

- Прямой перенос rollback Ren'Py и жеста rollback со стороны экрана.
- Voice controls без voice pipeline; мобильный quick menu при текущей PC-цели.
- Имена saves и безлимитные страницы при понятной текущей модели 6×3.
- Пользовательский resize textbox, economy HUD, unlockable looks и внешние community links.
- Lock-picking, code lock, card/lyre/ball/reaction/score/slot loops без конкретной авторской сцены.
- Любой `eval`/`exec` для условий диалога или чата.

## Hide UI implementation notes

- Implemented: `H` clean view and `H`/`Esc` restore; no dialogue/save state mutation.
- Not implemented: middle-click Hide UI and any custom screenshot file writer/gallery/Steam API integration.
- Intended screenshot UX: clean authored frame for the player's system or Steam screenshot tool.
- Persistent Quick Menu preference remains **TODO** (`B03`): transient player Hide UI is not that setting.

## Maintenance log

- `1d1d7dabf927dd764c6272877d038da9927b1bb0` - Hide UI clean view: `H` is a canonical Help binding. In a stable ordinary dialogue state it hides only the existing dialogue shell and Quick Menu; background and character stay visible. Hidden view is transient, blocks advance/Auto/Skip/save/load/Backlog/Settings/Main Menu/special-mode entry, stops timers without changing Auto or Skip preference, and restores on H or Esc with fresh normal eligibility. Quick Menu preserves previous intentional visibility and special-mode ownership. No middle-click binding or custom screenshot writer was added: player screenshots use system/Steam tools. `HideUiSmokeTests`, full CI, validator and scene validation passed in Unity 6000.5.7f1; `SaveData` remains v3 and `SaveManager` is unchanged.
- `e15003f28ee1029d3aa6fe712438c63c1d786b15` — Unified modal/special-mode coordinator: scene-local `VNDialogueController` owns a plain-C# exclusive coordinator with opaque generation-bound leases and fail-closed `BlockingExclusive`. Advance, Auto, Skip, save/load, Backlog, Settings, Quick Menu, Escape and Main Menu paths use permission gates; normal modals and ordinary choices remain outside coordinator ownership. Focused coordinator/integration smoke, full CI, validator and scene validation passed in Unity 6000.5.7f1; `SaveData` remains v3 and `SaveManager` is unchanged.
- `9d7be27db11ffcdabc6bb2ec56845440c6647b2f` — Backlog restoration: `SaveData` v3 хранит до 100 raw entries на slot; Manual/Auto/Quick и pre-load Auto capture один runtime source. In-place Load и scene reload/Continue заменяют History, restore suppression исключает duplicate current line/resultText, а failure возвращает прежние GameState/backlog. V1 Manual и v2 Manual/Auto/Quick остаются loadable без переписывания JSON; malformed optional snapshot не блокирует core Load. Focused smoke, общий CI/validator/scene validation, Manual graphical E2E и Save Backend graphical E2E прошли в Unity 6000.5.7f1.
- `b5b47a108366e70c329e8de9ed63bf8b5abe8af2` — Unified input map + Help: `VNInputMap` используется runtime и Main Menu Help. Player Help показывает Ctrl/F5/F6/F8/F9/B/Esc; F2/F3 остаются скрытыми debug bindings; rebinding и InputAction asset не добавлялись. CI, project validator и scene validation passed в Unity 6000.5.7f1; visual Help QA pending.
- `b62435651f3a2af3606584724989eccb6108b461` — Settings mapping fix: canonical Unicode-escape option constants are shared by UI, runtime and smoke tests. Windowed, borderless fullscreen compatibility and fast skip cadence now map to their intended runtime values; no autosave/pre-load routing changed.
- `3e83fca55dc59efc3f960a9827dd1db9ac45ac3a` — Settings truth pass: `Screen.fullScreenMode` and `Screen.SetResolution` apply display values; autosave and skip cadence have runtime consumers; Main Menu hides controls that need absent subsystems. `SettingsTruthSmokeTests` is in CI; `SaveData` is unchanged.
- `23358b6ed856c7e3b1da379d78085c0b84557f2c` — Relationship change feedback: после ручного `VNDialogueController.Choose()` применённые relationship delta собираются в один existing toast. Нулевые и неотношенческие delta не показываются, порядок Masha → Artem → Lera детерминирован, значения не выводятся; Save/Load и restore не создают событие.

- `9647986856fa8c5a545ee375e4a60866a9472212` - Typed conditional choices: added closed numeric condition enums for the nine persisted `GameState` values and three inclusive operators; unavailable choices are hidden through transient source-index mapping. Choice saves and result restore continue to use source indices, `SaveData` remains v3, and zero/capacity paths fail safely. Focused conditional smoke plus the full Unity 6000.5.7f1 CI, project validator and scene validation passed; visual Play Mode QA remains pending.

**Last reviewed functional commit:** `1d1d7dabf927dd764c6272877d038da9927b1bb0`

## Правило обновления

После каждой реализованной механики обновлять фактический статус здесь, а подробные edge cases добавлять в полный audit только при появлении нового подтверждённого поведения. Не считать сохранённую настройку реализованной, пока её эффект не виден в runtime.
