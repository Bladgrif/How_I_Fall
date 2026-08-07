# Eternum 0.9.5 Feature Audit

## Scope / Rules

Документ фиксирует повторный **source-only** аудит player-facing механик локальной русской сборки Eternum 0.9.5 и сопоставляет их с фактическим состоянием How I Fall на Unity.

- Проверенный корень reference: `C:\Users\roman\Downloads\Eternum\Rus\Eternum-0.9.5-pc_Rus\Eternum-0.9.5-pc_Rus`.
- Повторно просмотрены 25 исходных `.rpy` верхнего уровня в `game`, включая `screens.rpy`, `options.rpy`, `save_name.rpy`, `save_compatibility.rpy`, `gallery.rpy`, `chat.rpy`, `pax.rpy`, `look.rpy`, `lock_minigame.rpy`, `text.rpy` и `script.rpy`–`script9.rpy`.
- Переводы в `game/tl` использовались только для подтверждения локализованных подписей; они не считаются отдельной реализацией механик.
- Проверялось поведение и структура систем. Код, тексты, изображения, музыка, UI-assets, названия сюжетных элементов и Ren'Py-архитектура не переносятся.
- Состояние How I Fall проверено по текущим C#-скриптам, Unity-сценам, ScriptableObject-данным и Editor/smoke-тестам на `HEAD 7e2f6bc` до этого docs-коммита. Старый tracker не считался источником истины.
- Runtime Eternum не запускался: выводы о собственном коде подтверждены исходниками, а стандартные действия Ren'Py описаны только там, где они явно подключены. Неочевидные engine edge cases помечены как непроверенные запуском.
- Unity runtime, сцены, `SaveData`, tests и assets в рамках задачи не менялись.

## Legend

- **HIF value:** `REQUIRED` — базовый VN-контур; `USEFUL` — заметная польза после базы; `LATER` — только под конкретную сцену/контент; `NOT NEEDED` — сознательно исключено.
- **HIF state:** `DONE`, `PARTIAL`, `TODO`, `NOT PLANNED`.
- **Old:** `COVERED` — отдельная строка уже была; `EXPANDED` — упоминалось слишком широко/неточно; `NEW` — отдельного пункта в старом tracker не было.
- В колонке **Eternum / edge / input** одновременно указаны player behavior, момент использования, известные edge cases, связанные настройки/input и проверяемый источник.
- **HIF / gap** называет существующие системы How I Fall и конкретно отсутствующую часть.

## Current How I Fall baseline

- Диалог: `VNDialogueController`, `DialogueSceneData`, `DialogueLine`, typewriter, complete-current-line, обычные выборы, переходы между сценами.
- Состояние: `GameState`, типизированные stat-delta в `DialogueChoice`, восстановление scene/line/choice state.
- Save/Load: `SaveManager`, `SaveData` v2, по 6 Manual/Auto/Quick слотов, PNG 384×216, Continue, подтверждения, pre-load autosave, controlled v1 read.
- UX: `VNQuickMenu`, session backlog, Auto, seen-aware Skip, toast, VN settings, главное меню.
- Ограничения: нет rollback, conditional choices, общего unlock registry, relationship feedback, hotspot/map/chat/gallery/QTE/mini-game runtime; значительная часть сохранённых `GameSettings` пока не влияет на игру.

---

## A. Dialogue

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| A01 | Advance dialogue | Click/Enter advances the current say; Space advances without activating a choice. Choice/modal screens take input priority. `screens.rpy:107-142,1713-1721`. | REQUIRED — основной VN input. | DONE — `AdvanceDialogue`; нет единой rebinding-схемы. | Input System | Small | Low | EXPANDED |
| A02 | Typewriter speed | Ren'Py renders say text at `preferences.text_cps`; `0` means instant. Per-line `{cps}` tags can override pacing. `options.rpy:121-130`, `script*.rpy` uses `{cps}`. | REQUIRED — управляемый темп. | DONE/PARTIAL — typewriter есть, но сохранённое `20..100` умножается на `baseCharactersPerSecond`, поэтому шкала требует отдельной UX-проверки. | Settings | Small | Medium | COVERED |
| A03 | Complete current line | Dismiss during slow text completes the current line before moving on; pauses/tags remain engine-managed. | REQUIRED — защита от случайного пропуска. | DONE — повторный advance вызывает `CompleteTyping`. | A01 | Small | Low | EXPANDED |
| A04 | Speaker/name presentation | `say` создаёт отдельный namebox only when `who` exists; character color can flow into history. `screens.rpy:127-136,1620-1629`. | REQUIRED — читаемость голосов. | DONE — `nameBox`/`speakerText`; цвет/CharacterData не реализованы. | Dialogue data | Small | Low | NEW |
| A05 | Narration | `who=None` показывает текст без имени; menu captions can be narrated. `screens.rpy:94-107,277-279`. | REQUIRED — авторская речь. | DONE — пустой speaker скрывает namebox; отдельного narrator style нет. | Dialogue data | Small | Low | NEW |
| A06 | Dialogue window auto-hide | `config.window="hide"`: textbox появляется для реплики и скрывается вне диалога; explicit `window show/hide` доступны сценарию. `options.rpy:100-116`. | USEFUL — чистые CG/переходы. | PARTIAL — UI постоянно сцено-зависим; общего hide/show API нет. | UI state | Medium | Medium | NEW |
| A07 | Textbox accessibility | Player меняет size, outline, opacity, width, height; say-screen перестраивается и учитывает quick menu. `screens.rpy:112-174,1364-1399`. | USEFUL — доступность и разные экраны. | PARTIAL — text speed есть; font-size/outline/opacity/geometry не применяются. | Settings/UI layout | Medium | Medium | NEW |
| A08 | Inline pacing tags | `{w}`, `{nw}`, `{cps}` задают паузы, auto-continue и локальную скорость; активно применяются в scripts. Edge: skip/auto взаимодействуют с engine tags. | LATER — нужен авторский контроль напряжения. | TODO — `DialogueLine.text` не содержит типизированных timing commands; raw TMP tags не заменяют timing. | Dialogue command model | Medium | High | NEW |
| A09 | Custom expressive text | `text.rpy` регистрирует bounce/fade/scare/chaos/rotate/swap/move/omega displayables; используются как эмоциональный эффект. | LATER — точечно для мистики. | NOT PLANNED — до стабильного сценарного command format; не копировать реализацию. | Text effects | Large | High | NEW |
| A10 | Hide/show UI action | `H` and middle click hide interface; Esc/right click restore/open menu per Ren'Py defaults. `screens.rpy:1754-1776`. | USEFUL — screenshots/CG reading. | TODO — публичного hide-UI action нет. | Input + UI state | Small | Low | NEW |
| A11 | Scripted pauses and transitions | `pause`, `with`, dissolve/punch/custom transforms pace scenes; menu enter/exit transitions configured globally. `options.rpy:69-93`, `transform.rpy`. | REQUIRED — сцена не должна быть статичной лекцией. | PARTIAL — scene music/visual swap есть; data-driven pauses/transitions отсутствуют. | Dialogue command model | Medium | Medium | NEW |

## B. Quick Menu

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| B01 | Desktop quick actions | Back, History, Skip, Auto, Save, Q.Save, Q.Load, Preferences доступны поверх игры. `screens.rpy:308-334`. | REQUIRED — быстрый VN-контур. | DONE except Back — `VNQuickMenu` вызывает существующие APIs. | Existing services | Small | Medium | COVERED |
| B02 | Runtime active state | Ren'Py `Preference`/`Skip` actions expose selected state; HIF visibly colors Auto/Skip. | REQUIRED — игрок понимает режим. | DONE для Auto/Skip; save/load disabled-state не отражается заранее. | UI state | Small | Low | EXPANDED |
| B03 | Persistent quick-menu visibility | Preferences globally enable/disable panel; textbox geometry changes accordingly. `options.rpy:334`, `screens.rpy:112-119,1477-1482`. | USEFUL — минималистичный режим. | TODO — HIF quick menu always scene-configured. | Settings + layout | Small | Low | NEW |
| B04 | Modal/choice priority | Modal screens capture input; quick menu remains an overlay but no custom per-QTE disable policy is declared. Exact hotkey behavior requires runtime confirmation. | REQUIRED — исключить скрытые clicks. | DONE/PARTIAL — HIF dialogue/save APIs block known panels; generic future modal contract отсутствует. | Modal coordinator | Medium | High | EXPANDED |
| B05 | Touch quick menu | Touch variant reduces actions to Back, Skip, Auto, Menu and uses larger controls. `screens.rpy:2185-2204`. | LATER — если будет mobile. | NOT PLANNED for current PC slice. | Platform UX | Medium | Medium | NEW |

## C. Save / Load

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| C01 | Manual save slots | Save screen uses 3×2 cards per page; click writes through `FileAction`. `save_name.rpy:259-333`. | REQUIRED — контроль прогресса. | DONE — 6 Manual slots. | SaveManager | — | Low | COVERED |
| C02 | Manual load slots | Load uses same cards and ignores save-name prompt. Empty/corrupt slots are not loadable. | REQUIRED. | DONE — validation and load errors shown. | SaveManager | — | Medium | COVERED |
| C03 | Autosave page | Built-in `A` page exposes engine autosaves. Source does not add explicit story checkpoints. `save_name.rpy:88-113,352-356`. | REQUIRED — recovery. | DONE — 6 rotating slots, scene-start/choice checkpoints. | SaveManager | — | Medium | COVERED |
| C04 | Quicksave page | Built-in `Q` page plus quick actions. `screens.rpy:326-327`, `save_name.rpy:355-356`. | REQUIRED — fast suspend/resume. | DONE — quick menu + F6/F8 + 6 rotating slots. Old tracker was stale. | SaveManager/VNQuickMenu | — | Medium | COVERED |
| C05 | Quick save shortcut | Shift+S/F5 dispatches `QuickSave`. `options.rpy:343-349`, help `1730-1736`. | USEFUL for PC. | PARTIAL — HIF uses F6 for quick save; F5 opens manual Save. Needs documented binding, not blind imitation. | Input map/help | Small | Low | EXPANDED |
| C06 | Quick load shortcut | Shift+L/F9 dispatches `QuickLoad`. | USEFUL for PC. | PARTIAL — HIF uses F8 quick load; F9 opens manual Load. | Input map/help | Small | Low | EXPANDED |
| C07 | Slot thumbnails | Every card shows engine screenshot; configured 384×216. `gui.rpy:294-299`, `save_name.rpy:323-330`. | REQUIRED — узнаваемость слота. | DONE — PNG preview 384×216. | Capture pipeline | — | Medium | COVERED |
| C08 | Slot metadata | Date/time and save name displayed; empty slot has explicit label. | REQUIRED. | DONE — date, scene/display name, empty/corrupt state. | SaveData | — | Low | COVERED |
| C09 | Save naming | Persistent toggle enables a 30-char modal name before new/overwrite save; Enter confirms, Esc cancels. `save_name.rpy:224-257,275-321`. | LATER — полезно при десятках слотов. | NOT PLANNED — 6 slots and scene name sufficient. | Metadata/UI | Medium | Low | NEW |
| C10 | Page navigation | Auto/Quick plus unbounded numeric pages, step ±1/±10, mouse-wheel switching, remembered page range. `save_name.rpy:8-220,263-368`. | NOT NEEDED now — fixed 6 slots clearer. | NOT PLANNED until slot count grows. | Save UI | Medium | Medium | NEW |
| C11 | Overwrite confirmation | Existing slot prompts before replacement; naming modal distinguishes new/overwrite. | REQUIRED. | DONE — explicit overwrite confirm. | ManualSaveLoadPanel | — | Low | COVERED |
| C12 | Delete save | `save_delete` key removes current slot. Confirmation depends on Ren'Py FileDelete behavior. `screens.rpy:1149`, `save_name.rpy:333`. | REQUIRED action; hotkey optional. | DONE via visible delete button + confirmation; Delete hotkey absent. | Save UI/input | Small | Low | COVERED |
| C13 | Load loss confirmation | Loading from game uses Ren'Py confirm path; main-menu load does not need unsaved-progress warning. | REQUIRED. | DONE — confirmation in gameplay. | Save UI | — | Low | EXPANDED |
| C14 | Pre-load safety checkpoint | Eternum source has no custom “save before load” flow. HIF creates a dedicated pre-load autosave before Manual/Quick load. | REQUIRED for HIF — stronger safety than reference. | DONE — `RequestPreLoadAutoSave`; Auto-slot loads avoid recursive checkpoint. | SaveManager | — | High | NEW |
| C15 | Continue latest | Eternum main menu exposes New Game, Load, Preferences, Credits, Quit; no wired Continue. `screens.rpy:560-690`. | REQUIRED for HIF convenience. | DONE — newest valid Manual/Quick/Auto; disabled if none. | SaveManager/MainMenu | — | Low | COVERED |
| C16 | Save compatibility migration | `after_load` patches older variables/looks then updates game version. `save_compatibility.rpy:2-24`. | REQUIRED once schema evolves. | DONE/PARTIAL — v2 + controlled v1 manual migration; future versions need explicit migrators. | Versioned SaveData | Medium | High | COVERED |
| C17 | Corrupt/incompatible slot behavior | Ren'Py engine owns compatibility; custom `after_load` assumes valid load. | REQUIRED to fail safely. | DONE — HIF rejects invalid JSON/version/type/index/scene/line/preview metadata and preserves previous state. | Validator | — | High | EXPANDED |
| C18 | Special-scene save policy | No explicit `quick_menu=False`, `block_rollback` or custom save guard found around QTE/mini-games; source-only audit cannot promise safe mid-game saves. | REQUIRED policy before HIF QTE. | TODO — generic special-mode save restriction contract absent; do not infer Eternum behavior as best practice. | Modal/game-mode state | Medium | High | NEW |

## D. Auto

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| D01 | Auto toggle | Quick menu and Alt+A toggle Ren'Py auto-forward. `screens.rpy:324,1746-1749`, `options.rpy:354`. | REQUIRED comfort. | DONE — quick menu/VN settings; no Alt+A. | Settings | Small | Low | COVERED |
| D02 | Auto delay | `preferences.afm_time` and slider control delay. `options.rpy:127-130`, `screens.rpy:1323-1325`. | REQUIRED. | DONE — 0.5–5.0 sec mapping; label still stored/displayed as historical percent in some UI. | Settings UI | Small | Medium | EXPANDED |
| D03 | Stop at choice | Engine auto waits at menu; player input selects branch. | REQUIRED — Auto must not choose. | DONE — `showingChoice` blocks timer. | Choice state | — | Low | EXPANDED |
| D04 | Pause on modal/menu | Game menu/modal interaction pauses/interrupts auto; resume starts from visible dialogue. | REQUIRED. | DONE for known backlog/settings/save/confirm panels; future generic modals not registered. | Modal coordinator | Medium | Medium | EXPANDED |
| D05 | Auto vs Skip | Ren'Py modes are separate controls; active skip drives advancement. | REQUIRED deterministic input. | DONE — enabling Skip stops Auto timer; disabling restarts Auto if configured. | VNDialogueController | — | Medium | NEW |

## E. Skip

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| E01 | Hold-to-skip | Ctrl skips while held in Ren'Py. Help documents hold behavior. | USEFUL replay speed. | PARTIAL — HIF Ctrl press toggles persistent runtime Skip rather than hold. Must document or redesign intentionally. | Input UX | Small | Medium | EXPANDED |
| E02 | Toggle skip | Tab toggles skip; quick menu button also starts/stops it. `screens.rpy:1738-1744`. | REQUIRED for accessibility. | DONE via quick menu/Ctrl toggle; Tab absent. | Input map | Small | Low | COVERED |
| E03 | Seen-only mode | Preference “Unseen Text” decides whether unseen text blocks skip. `screens.rpy:1470-1474`. | REQUIRED — safe reread. | DONE — read history in PlayerPrefs, keyed by sceneId/lineId. | Stable IDs | — | Medium | EXPANDED |
| E04 | Skip all text mode | Same preference can allow unseen text. | USEFUL for QA/replays. | DONE — `IsAllTextSkipMode`; UI exists in main settings, not compact VN settings. | Settings UI | Small | Medium | NEW |
| E05 | Stop at choice | Standard menu stops skip and never activates an answer. | REQUIRED. | DONE — choice state blocks; choice not auto-selected. | Choice state | — | Low | EXPANDED |
| E06 | Resume after choice | “After Choices” controls whether skip resumes. `screens.rpy:1473-1474`. | USEFUL. | DONE — `skipAfterChoices`. | Settings | — | Medium | EXPANDED |
| E07 | Transition skipping | Preference exists only as commented-out UI; no player-facing transition toggle. `screens.rpy:1475`. | NOT NEEDED — avoid false feature. | NOT PLANNED. | — | — | Low | NEW |

## F. History / Rollback

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| F01 | Backlog list | History iterates `_history_list`, shows speaker/text and empty state, strips disallowed tags. `screens.rpy:1601-1641`. | REQUIRED readability. | DONE/PARTIAL — session list of 100 entries; not restored after load/restart. | Dialogue log | Medium | Medium | COVERED |
| F02 | History capacity | Ren'Py caps history at 250. `gui.rpy:398`. | USEFUL memory bound. | DONE with different cap 100; intentional for prototype. | — | — | Low | NEW |
| F03 | History formatting safety | Engine filters tags and preserves character name color. | REQUIRED — prevent rich-text injection/layout break. | DONE/PARTIAL — HIF escapes TMP rich text; no color metadata. | Backlog model | Small | Low | NEW |
| F04 | Back action | Quick menu Back invokes `Rollback()`. | LATER — исправление accidental advance. | TODO — absent by explicit design. | Reversible state model | Large | High | COVERED |
| F05 | Mouse/gamepad rollback | Wheel up/rollback side and gamepad left trigger move back; wheel down/right shoulder roll forward. `screens.rpy:1778-1799`. | LATER only with rollback. | NOT PLANNED before rollback scope. | F04/Input | Medium | High | NEW |
| F06 | Rollback side preference | Player chooses disabled/left/right screen edge. `screens.rpy:1463-1468`. | NOT NEEDED without rollback. | NOT PLANNED. | F04 | Medium | Medium | NEW |
| F07 | Rollback boundaries | No custom `block_rollback` found; choices/save/load rely on Ren'Py engine stack. Exact post-load stack behavior not verified by launch. | REQUIRED decision if HIF adds Back. | TODO — define dialogue-only vs reversible choices; do not copy engine stack semantics. | State snapshots | Large | High | EXPANDED |

## G. Choices

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| G01 | Normal menu choice | 337 `menu:` blocks across `script.rpy`–`script9.rpy`; selected caption continues its branch. | REQUIRED agency. | DONE — up to 3 wired choice buttons and `nextScene`. | DialogueChoice | — | Low | COVERED |
| G02 | Conditional hidden option | At least 81 native options use `"caption" if condition:`; unavailable options are omitted, not disabled. | REQUIRED for consequences, but not immediate NEXT automatically. | TODO — no conditions on `DialogueChoice`. | Typed conditions + validator | Medium | Medium | COVERED |
| G03 | Conditional screen hotspot | Image maps/buttons appear only when flags/routes permit; e.g. survival safe and map evidence. `screens.rpy:2489-2495,3583-3715`. | LATER under interactive scenes. | TODO. | Story state + hotspot | Medium | High | NEW |
| G04 | Disabled choice feedback | Native `choice` screen has no custom disabled/requirement reason; options are normally hidden. | USEFUL if HIF wants transparent morality. | NOT PLANNED until a scene requires disabled-visible choices. | Condition UX | Medium | Medium | NEW |
| G05 | Choice effects | Branch scripts mutate flags, counters, points and route booleans. | REQUIRED. | DONE for fixed stat-deltas; arbitrary flags absent. | GameState | —/Medium | Medium | EXPANDED |
| G06 | Choice result beat | Eternum branch immediately shows consequence content. HIF has `resultText` before routing. | REQUIRED feedback. | DONE — choice result and pending next scene are saveable. | SaveData | — | Medium | NEW |
| G07 | Timed choice | `millionairescreen` has hotspots plus 30s timeout; several QTEs are also timed decisions. `screens.rpy:3822-3834`. | LATER — only for authored pressure scene. | TODO. | Timer + accessibility | Medium | High | NEW |
| G08 | Choice history | No separate durable choice-history UI; consequences live in variables/rollback. | NOT NEEDED as standalone now. | NOT PLANNED; `selectedChoiceIndex` stores only active result state. | — | — | Low | NEW |

## H. Game State

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| H01 | Boolean story flags | Hundreds of `default` booleans gate branches, hotspots and scenes. | REQUIRED under concrete story needs. | PARTIAL — fixed typed fields only; no general flags. | Story schema | Medium | Medium | COVERED |
| H02 | Counters/resources | Money, scores, rounds, evidence and interaction counters drive UI and branches. | USEFUL only when scenes need them. | PARTIAL — fixed stats/relationships; no generic counters. | Story schema | Medium | Medium | NEW |
| H03 | Relationship values | Per-character `_points` values feed thresholds and routes. | REQUIRED for romance/drama. | DONE data-side for three relationships; UI feedback missing. | GameState | — | Medium | EXPANDED |
| H04 | Route/path state | `*path`, met/unlocked flags and branch booleans control content availability. | REQUIRED once routes exist. | TODO/PARTIAL — `pendingNextSceneId` and direct scene graph exist, route locks do not. | Stable IDs/flags | Medium | High | NEW |
| H05 | Persistent preferences/unlocks | `persistent` stores gallery override, quick menu/textbox/motion/save naming/page state, separate from rollback saves. | REQUIRED separation principle. | DONE for settings/read history; universal unlock state absent. | PlayerPrefs/profile | Medium | Medium | EXPANDED |
| H06 | Seen state | `renpy.seen_image` unlocks replay thumbnails; engine seen text supports skip. | REQUIRED for Skip, LATER for gallery. | DONE for line seen-state; gallery scene seen-state absent. | Stable IDs | Medium | Medium | EXPANDED |
| H07 | Saveable scene position | Ren'Py captures interpreter state; HIF must store stable scene/line identifiers. | REQUIRED. | DONE — sceneId/lineId + fallback index + choice state. | SaveData | — | High | EXPANDED |
| H08 | State migration | `after_load` normalizes old fields/version. | REQUIRED as content evolves. | PARTIAL — v1→v2 only; migration registry/process should grow only with schema. | C16 | Medium | High | EXPANDED |

## I. Relationship UX

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| I01 | Relationship-point changes | Story/chat actions increment per-character points; heartbeat/heart overlays mark important beats. `chat.rpy:155-159`, repeated `show screen heart/heartbeat`. | REQUIRED feedback principle. | PARTIAL — stats change silently. | GameState + toast | Small | Low | COVERED |
| I02 | Character hub | Heart button opens `point` hub with met/path gating, currency and character selection. `pax.rpy:29-268`, `screens.rpy:2367-2372`. | LATER — useful when cast/content grows. | TODO. | Character registry | Large | Medium | NEW |
| I03 | Met/locked character state | Unknown characters are grayscale/disabled; lost routes can be visually distinguished. | LATER. | TODO; no character unlock state. | Flags/profile | Medium | Medium | NEW |
| I04 | Heart thresholds | `girls_hearts` maps points to 1–6 heart tiers; unavailable tiers use `-1`. `bios.rpy:23-32`, `pax.rpy:280-311`. | USEFUL, but HIF should not expose exact hidden model by default. | TODO — design subtle tier/toast if story needs it. | Relationship rules | Medium | Low | EXPANDED |
| I05 | Character status/biography | Selected character view combines portrait/status assets and route-specific variants. | LATER. | NOT PLANNED until character docs/data stabilize. | CharacterData | Large | Medium | NEW |
| I06 | Unlockable looks | Met character can cycle unlocked appearance variants; `after_load` migrates old lists. `look.rpy`, `save_compatibility.rpy:9-16`. | NOT NEEDED for current VN slice. | NOT PLANNED. | Cosmetic registry | Large | Medium | NEW |
| I07 | Relationship-to-gallery bridge | Character hub opens gallery prefiltered to selected character. `pax.rpy:229-249`. | LATER. | TODO only with gallery. | Gallery + character hub | Small | Low | NEW |

## J. Settings

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| J01 | Master mute | “Mute All” toggles all mixers. `screens.rpy:1355-1360`. | REQUIRED accessibility. | DONE via master volume, but no one-click mute button in compact VN panel. | Settings UI | Small | Low | EXPANDED |
| J02 | Music volume | Separate music mixer. | REQUIRED. | DONE and applied. | AudioManager | — | Low | EXPANDED |
| J03 | SFX volume | Separate sound mixer. | REQUIRED. | DONE and applied. | AudioManager | — | Low | EXPANDED |
| J04 | Voice volume | `config.has_voice=True`, but voice slider is commented out and no dedicated player-facing voice control is active. | NOT NEEDED until voice exists. | NOT PLANNED. | Voice pipeline | Medium | Low | NEW |
| J05 | Ambience volume | Eternum routes ambience-like loops through SFX channels; no separate ambience preference. | USEFUL for HIF atmosphere. | PARTIAL — setting is stored/UI field exists, but `AudioManager` has no ambience source. | AudioManager | Medium | Medium | NEW |
| J06 | Text speed | Slider controls `text_cps`. | REQUIRED. | DONE functionally; scale semantics need QA. | A02 | Small | Medium | EXPANDED |
| J07 | Auto-forward delay | Slider controls AFM delay. | REQUIRED. | DONE. | D02 | — | Low | EXPANDED |
| J08 | Text size | Persistent 20–50 range. | USEFUL accessibility. | PARTIAL — HIF stores font size mode in main settings but runtime dialogue ignores it. | Typography | Medium | Medium | NEW |
| J09 | Text outline | Persistent 0–4. | USEFUL on bright backgrounds. | TODO. | TMP material/style | Medium | Medium | NEW |
| J10 | Textbox opacity | Persistent 0–100%. | USEFUL. | TODO. | UI theme | Small | Low | NEW |
| J11 | Textbox width | Player-resizable dialogue width. | NOT NEEDED now; responsive layout should own width. | NOT PLANNED. | Layout | Medium | Medium | NEW |
| J12 | Textbox height | Player-resizable dialogue height. | NOT NEEDED now. | NOT PLANNED. | Layout | Medium | Medium | NEW |
| J13 | Window/fullscreen | PC radio choice between window/fullscreen. | REQUIRED. | DONE for fullscreen bool; borderless option is stored in main settings but not actually applied. | Screen API | Medium | Medium | EXPANDED |
| J14 | Resolution | Ren'Py relies on virtual resolution/display mode; no explicit resolution list in this Preferences screen. | USEFUL for Unity. | PARTIAL — HIF cycles/stores values but never calls `Screen.SetResolution`. | Screen API | Small | Medium | NEW |
| J15 | Refresh rate | Not an Eternum player-facing setting. | NOT NEEDED until verified demand. | PARTIAL false affordance — HIF stores/cycles but does not apply. Prefer remove from roadmap or implement later. | Screen API | Medium | Medium | NEW |
| J16 | Language | Known languages are listed dynamically; selected language swaps translated strings. `screens.rpy:1446-1461`. | LATER when translation exists. | PARTIAL false affordance — value is stored, localization system absent. | Localization | Large | High | NEW |
| J17 | Skip unseen | Toggle described in E03. | REQUIRED. | DONE runtime; full settings UI exposes it. | Skip | — | Medium | EXPANDED |
| J18 | Skip after choices | Toggle described in E06. | USEFUL. | DONE. | Skip | — | Medium | EXPANDED |
| J19 | Quick menu enabled | Persistent toggle. | USEFUL. | TODO. | B03 | Small | Low | NEW |
| J20 | Interface motion | Persistent on/off scales UI animation durations; mobile defaults off. `options.rpy:341`, `screens.rpy:1483-1487`, `transform.rpy`. | USEFUL accessibility/reduced motion. | PARTIAL — HIF stores character/background animation toggles but runtime does not consistently consume them; no global reduced-motion mode. | Animation policy | Medium | Medium | NEW |
| J21 | Rollback side | Left/right/disable, only relevant with rollback. | NOT NEEDED now. | NOT PLANNED. | F04 | Medium | Medium | NEW |
| J22 | Save naming toggle | Persistent toggle described in C09. | NOT NEEDED now. | NOT PLANNED. | Save UI | Medium | Low | NEW |

## K. Input

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| K01 | Mouse advance/activate | Left click advances or activates focused UI. | REQUIRED. | DONE through Unity buttons/event system. | Input System | — | Low | EXPANDED |
| K02 | Enter advance/activate | Enter advances and activates interface. | REQUIRED keyboard access. | PARTIAL — scene button focus can work, but no controller-level Enter binding. | Input map | Small | Medium | NEW |
| K03 | Space safe advance | Space advances without selecting choice. | USEFUL anti-misclick. | TODO. | Input map | Small | Low | NEW |
| K04 | Escape/menu/back | Esc opens menu or cancels modal; screens bind `game_menu` to Return/No. | REQUIRED. | PARTIAL/DONE — closes known HIF panels/confirm; does not open a general pause menu from bare dialogue. | Modal stack | Medium | Medium | EXPANDED |
| K05 | Quick save/load keys | Shift+S/F5 and Shift+L/F9. | USEFUL. | DONE with different F5/F6/F8/F9 scheme; help/prompt documentation missing. | Input help | Small | Low | COVERED |
| K06 | Skip keys | Ctrl hold and Tab toggle. | USEFUL. | PARTIAL — Ctrl toggles; Tab absent. | Skip | Small | Medium | EXPANDED |
| K07 | Auto key | Alt+A toggles Auto. | USEFUL. | TODO. | Auto | Small | Low | NEW |
| K08 | History key | No dedicated history key is documented by Eternum help; quick menu opens it. | USEFUL HIF extension. | DONE — B opens backlog, but UI help is absent. | Help | Small | Low | NEW |
| K09 | Hide UI key | H and middle click. | USEFUL. | TODO. | A10 | Small | Low | NEW |
| K10 | Screenshot key | S takes screenshot; notify confirms. `screens.rpy:1754-1760`. | LATER/community sharing. | TODO; Unity platform screenshot action absent. | Capture + toast | Small | Low | NEW |
| K11 | Delete save key | `save_delete` removes selected slot. | LATER; visible button safer. | NOT PLANNED until focus/confirmation UX supports it. | Save UI | Small | Medium | EXPANDED |
| K12 | Arrow/gamepad navigation | Arrows and D-pad/sticks navigate; gamepad actions advance, rollback, menu, hide UI and calibrate. `screens.rpy:1722-1728,1787-1814`. | LATER for controller/accessibility. | PARTIAL via Unity selectable defaults; no verified full navigation/help/calibration. | EventSystem | Medium | Medium | NEW |

## L. Main Menu / Navigation

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| L01 | New Game | Main menu `Start()` begins the script. | REQUIRED. | DONE — resets `GameState` and opens `VNPrototype`. | SceneFlow | — | Low | NEW |
| L02 | Continue | No wired Eternum Continue action was found. | REQUIRED HIF improvement. | DONE — latest valid save. | SaveManager | — | Low | COVERED |
| L03 | Load | Main-menu Load opens file slots without unsaved-progress flow. | REQUIRED. | DONE. | Save UI | — | Low | NEW |
| L04 | Preferences | Main menu and game navigation open the same Preferences. | REQUIRED. | DONE/PARTIAL — main and VN panels use same manager but expose different subsets. | SettingsManager | Small | Medium | NEW |
| L05 | Credits/About | Main menu exposes Credits; About screen contains build/community info. | LATER for release. | PARTIAL — About panel hook exists; final content not audited here. | Content | Small | Low | NEW |
| L06 | Extras/relationship hub | Not a main-menu button; the in-game heart opens `point`, which links gallery. | LATER. | TODO — `OpenGallery` is log-only; no extras hub. | Character/gallery | Large | Medium | NEW |
| L07 | Quit confirmation | Quit uses a modal confirmation; in-game Quit can vary confirmation by context. `screens.rpy:687-707,1867-1937`. | REQUIRED PC safety. | DONE — main-menu confirm; gameplay main-menu-return confirm is separate. | Confirm UI | — | Low | EXPANDED |
| L08 | Replay navigation | While replaying, game navigation replaces Save/Load with “End Replay” and confirms exit. `screens.rpy:365-378`. | LATER with gallery. | TODO. | Replay mode | Medium | High | NEW |
| L09 | External/community links | Menu contains external URLs. | NOT NEEDED for current development slice. | NOT PLANNED. | Platform URL | Small | Low | NEW |

## M. Notifications / Confirmations

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| M01 | Timed toast | `notify` appears above UI and hides after 3.25s with fade. `screens.rpy:2026-2042`. | REQUIRED non-blocking feedback. | DONE — 1.5s VN / 2s main menu; no queue. | Toast UI | — | Low | COVERED |
| M02 | System-action feedback | Ren'Py notify is used for quicksave/screenshot and scripted actions. | REQUIRED. | DONE for quick/auto save and settings reset; screenshot absent. | M01 | Small | Low | EXPANDED |
| M03 | Relationship feedback | Heartbeat/heart overlays act as diegetic point-change feedback. | USEFUL, must be original. | TODO — preferred next roadmap item. | GameState + M01 | Small | Low | NEW |
| M04 | Generic Yes/No modal | `confirm` is modal/zorder 200; Esc/right-click selects No. | REQUIRED. | DONE for overwrite/delete/load/exit, implemented by separate panels. | Modal stack | — | Medium | COVERED |
| M05 | Error presentation | Ren'Py engine errors are not represented by a custom player-facing recovery UI in inspected scripts. | REQUIRED for saves only. | DONE/PARTIAL — save UI shows slot errors; other service errors mostly log. | Error policy | Medium | Medium | NEW |
| M06 | Confirmation focus/cancel | Modal blocks underlying input and offers keyboard cancel. | REQUIRED. | DONE for known panels; a single reusable modal coordinator is absent. | Modal stack | Medium | Medium | EXPANDED |

## N. Audio

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| N01 | Music playback | Multiple music channels permit layers/overlap; main-menu music configured. `options.rpy:66,184-190`. | REQUIRED atmosphere. | DONE/PARTIAL — one looping music source, no crossfade/layers. | AudioManager | Medium | Medium | NEW |
| N02 | One-shot SFX | Nine one-shot SFX channels support overlapping cues. `options.rpy:191-199`. | REQUIRED. | DONE/PARTIAL — one `PlayOneShot` source supports overlap but no bus/category routing. | AudioManager | Small | Low | NEW |
| N03 | Looping ambience/SFX | Four loop channels support persistent ambience. `options.rpy:200-203`. | REQUIRED for hidden anxiety/locations. | TODO — no ambience source/runtime despite stored volume. | AudioManager | Medium | Medium | NEW |
| N04 | Low-priority/secondary SFX | Separate `soundlow` channels prevent important sounds from being replaced. | LATER. | NOT PLANNED until audio contention appears. | Audio routing | Medium | Low | NEW |
| N05 | Voice channel | Config declares voice capability, but player slider is disabled and inspected scripts do not establish a voice pipeline. | NOT NEEDED now. | NOT PLANNED. | Voice assets/system | Large | Medium | NEW |
| N06 | Fades and cross-scene continuity | Scripts use fadein/fadeout and several channels; menu music can continue until replaced. | REQUIRED polish. | PARTIAL — music persists via `DontDestroyOnLoad`, but no fades/crossfade policy. | AudioManager | Medium | Medium | NEW |
| N07 | Audio volume buses | Music/SFX mixers controlled separately; all-mute available. | REQUIRED. | DONE for master/music/SFX; ambience missing. | Settings | Small | Low | EXPANDED |
| N08 | Audio in pause/modal | Eternum source does not expose a dedicated “music during pause” preference. | NOT REQUIRED as reference feature. | PARTIAL HIF-only — setting affects `ignoreListenerPause`, but global pause policy is not documented. | Pause model | Small | Medium | NEW |

## O. Visual Presentation

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| O01 | Background changes | Script switches still backgrounds by scene/beat. | REQUIRED. | DONE — per-line optional background. | DialogueLine | — | Low | NEW |
| O02 | Character sprites/positions | Character images, side images and transforms compose foreground layers. | REQUIRED. | DONE/PARTIAL — one character image with left/center/right/solo; no multi-character layer model. | CharacterData | Medium | Medium | NEW |
| O03 | Layer/z-order | Ren'Py screens use zorder, modal and transient layers; gallery card selection explicitly uses transient layer. | REQUIRED for safe overlays. | PARTIAL — current known hierarchy validated; no generic layer contract. | UI architecture | Medium | High | NEW |
| O04 | Transitions | Dissolves, motion transforms, punch/shake and custom scene transitions are pervasive. | USEFUL emotional pacing. | TODO/PARTIAL — no data-driven transition commands. | Dialogue commands | Medium | Medium | NEW |
| O05 | UI motion toggle | `persistent.motion` scales many menu animations to zero. | USEFUL reduced-motion accessibility. | TODO as global behavior. | J20 | Medium | Medium | NEW |
| O06 | Video playback | Build archives `.webm`; scripts use movie-capable Ren'Py presentation, though inventory did not find a standalone player setting. | LATER for authored cutscene only. | TODO/NOT PLANNED now. | Video player | Medium | High | NEW |
| O07 | Time/location title cards | Reusable overlay screens show elapsed time/day/time-of-day and disappear with scripted pacing. | USEFUL for VN clarity. | TODO — best implemented as typed overlay command when story needs it. | Dialogue commands | Small | Low | NEW |
| O08 | Emotional overlays | Heartbeat, death text, news text and counters layer over scenes. | LATER under authoring needs. | TODO — no general overlay command. | Overlay system | Medium | Medium | NEW |
| O09 | Alternate scene angles | Small buttons toggle alternate views during selected scenes. | NOT NEEDED as general mechanic; content-specific. | NOT PLANNED. | Scene-specific UI | Medium | Medium | NEW |

## P. Interactive Scenes

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| P01 | Image-map hotspots | `imagemap` defines ground/idle/hover and coordinate hotspots that jump or mutate state. | LATER after stable graph. | TODO. | Hotspot component | Medium | Medium | COVERED |
| P02 | Conditional hotspot | Hotspots appear only after flags/items/round conditions. | LATER for investigation. | TODO. | Typed conditions | Medium | High | NEW |
| P03 | Object interaction | Evidence, safes, doors, camera buttons and other objects perform actions without leaving the visual scene. | LATER. | TODO. | Interaction action model | Medium | Medium | NEW |
| P04 | Interaction hover feedback | Dedicated hover image and sound signal clickability. | REQUIRED if hotspots ship. | TODO. | Hotspot accessibility | Small | Low | NEW |
| P05 | Modal exploration scene | Crime/survival screens are modal and route among locations until objective state changes. | LATER for one authored investigation. | TODO. | Scene-mode coordinator | Large | High | NEW |
| P06 | Media inspection/zoom | Chat images expand fullscreen; `zoom_image` supports pan/zoom but appears development-oriented and has debug text. `chat.rpy:240-261`, `image_zoom.rpy`. | USEFUL for clues later. | TODO; do not adopt debug implementation. | Media viewer | Medium | Medium | NEW |
| P07 | Camera/photo interaction | Repeated camera clicks, flash overlay and thresholded success drive a scene. `screens.rpy:4672-4688`. | LATER only for concrete scene. | NOT PLANNED now. | Input counter + feedback | Medium | Medium | NEW |

## Q. Map / Locations

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| Q01 | Room-to-room navigation | Survival-horror screens form a small location graph through directional imagebuttons. `screens.rpy:2379-2495`. | LATER — useful for one exploration set-piece. | TODO. | Stable location IDs | Large | High | COVERED |
| Q02 | World/location selection map | `scrollwarthogs` exposes several destinations from one imagemap. `screens.rpy:2617-2630`. | LATER when multiple routes exist. | TODO. | Location registry | Medium | High | COVERED |
| Q03 | Locked location | Safe/evidence destinations appear only when state allows them. | LATER. | TODO. | Conditions + map | Medium | Medium | EXPANDED |
| Q04 | Return routing | Every location screen includes explicit back/adjacent destinations; state survives revisits. | REQUIRED if map exists. | TODO — scene graph currently linear/direct. | Location graph validator | Medium | High | NEW |
| Q05 | Location sound cue | Footsteps/doors/paper sounds are attached to transitions. | USEFUL polish. | TODO with map; current scene music only. | Audio actions | Small | Low | NEW |

## R. Phone / Chat-like UI

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| R01 | Message timeline | Chat builds a scrollable history of MC/NPC bubbles and auto-scrolls after replies. `chat.rpy:121-153,313-379`. | USEFUL for teenage drama. | TODO. | Chat data/runtime | Large | Medium | COVERED |
| R02 | Player replies | Valid replies appear as buttons and return selected step. | USEFUL. | TODO. | Typed choice conditions | Medium | Medium | EXPANDED |
| R03 | Conditional messages | Message `condition` strings choose valid NPC/player branches. | USEFUL behavior, unsafe implementation. | TODO — only typed conditions; never `eval`. | Condition engine | Medium | High | NEW |
| R04 | Message actions | Selected message executes variable changes/notifications from code strings. | USEFUL behavior. | TODO — typed effect list only; never `exec`. | Effect engine | Medium | High | NEW |
| R05 | Typing indicator/delay | NPC typing indicator animates before a delayed response; short pauses separate consecutive messages. `chat.rpy:192-213,388-401`. | USEFUL pacing. | TODO. | Chat timeline | Small | Low | NEW |
| R06 | Embedded media | Messages can include images; click opens modal fullscreen viewer. | LATER. | TODO. | Media viewer | Medium | Medium | NEW |
| R07 | Contact identity | Chat data provides background, NPC thumbnail/name; messages align by sender. | REQUIRED if chat exists. | TODO; needs CharacterData/contact registry. | CharacterData | Medium | Medium | NEW |
| R08 | Chat notification sound | Opening and incoming messages have separate SFX. | USEFUL. | TODO with chat. | AudioManager | Small | Low | NEW |

## S. Galleries / Extras

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| S01 | Seen-image unlock | Replay card is sensitive after a specific image was seen. `gallery.rpy:41-49,247-259`. | LATER when replayable scenes exist. | TODO. | Seen-scene registry | Medium | Medium | EXPANDED |
| S02 | Persistent unlock-all | `persistent.gallery_unlocked` bypasses seen checks. | NOT NEEDED for normal roadmap; useful QA cheat only. | NOT PLANNED player-facing. | Debug/profile | Small | Low | NEW |
| S03 | Locked thumbnail state | Locked cards are grayscale/blurred and display a lock label. | LATER. | TODO with gallery. | Gallery UI | Medium | Low | NEW |
| S04 | Scene replay | `Replay(label, scope, locked=False)` launches a replay label; navigation exposes End Replay. | LATER. | TODO; replay mode must isolate story/save state. | Replay runtime | Large | High | COVERED |
| S05 | Replay scope variables | Each replay passes only named variables needed to reconstruct variants. `gallery.rpy:47-79,188-193`. | REQUIRED if replay exists. | TODO — define immutable replay context, never reuse live `GameState`. | Replay runtime | Medium | High | NEW |
| S06 | Filter by category | Modal selector filters replay tags. | LATER after enough content. | NOT PLANNED initially. | Gallery metadata | Medium | Low | NEW |
| S07 | Filter by character | Character availability gates filter options; relationship hub opens prefiltered. | LATER. | NOT PLANNED initially. | Character registry | Medium | Medium | NEW |
| S08 | Persistent unlock state | Seen/unlock preference survives sessions independently of normal save. | LATER. | TODO only with profile/unlock registry. | Profile data | Medium | High | COVERED |

## T. QTE

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| T01 | Single-target timed QTE | Click a highlighted target before timer expires; bar visualizes remaining time. `screens.rpy:2632-2676`. | LATER for rare tension peak. | TODO. | QTE runner | Medium | Medium | COVERED |
| T02 | Success/failure branch | Target jumps to success; timeout jumps to fail/death/alternate label. | REQUIRED if QTE ships. | TODO. | Story routing | Medium | High | COVERED |
| T03 | Multi-step QTE chain | Arannis sequence changes correct screen target and timing across five steps. `screens.rpy:4036-4165`. | NOT NEEDED for first slice. | NOT PLANNED. | QTE runner | Large | High | NEW |
| T04 | Wrong-target failure | Some QTE screens show multiple targets where incorrect click immediately fails. | LATER. | NOT PLANNED until accessibility design. | QTE runner | Medium | High | NEW |
| T05 | Difficulty/time variants | Timers vary roughly 1.5–5s; fail can set a flag before routing. | LATER; needs accessibility multiplier/skip. | TODO only with first QTE. | Settings + QTE | Medium | High | NEW |
| T06 | Retry/death flow | Scripts route fail to lose/death labels; retry is story-specific, not a universal QTE control. | LATER. | TODO policy. | Checkpoint policy | Medium | High | NEW |
| T07 | Save/Auto/Skip restrictions | No explicit source guard was found around QTE calls; safe behavior is not demonstrated. | REQUIRED architecture before implementation. | TODO — QTE mode must block save/auto/skip or serialize complete QTE state. | Scene-mode coordinator | Large | High | EXPANDED |

## U. Mini-games

| ID | Type | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|---|
| U01 | Combination code lock | Four digits rotate, check against target, then return success/open screen. `screens.rpy:3513-3571`. | LATER for authorial puzzle. | TODO. | Mini-game contract | Medium | Medium | COVERED |
| U02 | Lock-picking | Mouse chooses pick angle; hold click tensions cylinder; wrong angle damages pick; attempts can fail; success returns bool. `lock_minigame.rpy`. | NOT NEEDED now. | NOT PLANNED. | Custom interaction + audio | Large | High | COVERED |
| U03 | Ball timing/reaction | Oscillating marker; Space/click scores by angle and routes to continue/success/fail. `screens.rpy:4312-4386`, `script9.rpy:6419-6436`. | NOT NEEDED first slice. | NOT PLANNED. | Timing loop | Medium | High | EXPANDED |
| U04 | Musical sequence | Click/Q–O keys play notes, append sequence and branch after expected input count. `screens.rpy:4388-4422`, `script9.rpy:6209-6212`. | LATER if a clue/music scene demands it. | TODO/NOT PLANNED now. | Audio/input sequence | Large | High | EXPANDED |
| U05 | Card selection | Animated cards route to separate labels; three sets are data-defined. `screens.rpy:4523-4552`. | LATER for original divination/choice beat. | TODO. | Data-driven card UI | Medium | Medium | COVERED |
| U06 | Investigation map | Multi-room crime map exposes suspects/evidence based on round/alive/key flags. `screens.rpy:3583-3715`. | LATER for mystery gameplay. | TODO; split into hotspot + clue registry, not one monolith. | P/Q + state | Large | High | NEW |
| U07 | Survival exploration | Room graph, first-visit flags, phone/safe clues and ending state form a scavenger puzzle. `script2.rpy:2162-2565`. | LATER for authored horror set-piece. | NOT PLANNED now. | Map + inventory/flags | Large | High | NEW |
| U08 | Audio-log clue board | Multiple selectable recordings and conditional final log progress the scene. `screens.rpy:2853-3274`. | LATER; useful investigation pattern. | TODO only with clue registry. | Media/clue system | Large | High | COVERED |
| U09 | Trial/quiz sequence | Data-defined answer sets route through a 14-step trial and cumulative success/fail labels. `screens.rpy:3910-4034`, `script9.rpy:1012-1875`. | NOT NEEDED current slice. | NOT PLANNED. | Quiz runner | Large | Medium | NEW |
| U10 | Timed multi-answer quiz | Four hotspot answers plus 30s timeout continue to a result. `screens.rpy:3822-3834`. | LATER. | NOT PLANNED until accessibility timer policy. | Timed choice | Medium | High | NEW |
| U11 | Camera-count challenge | Repeated photo action counts captures until threshold; flash feedback and branch. | NOT NEEDED now. | NOT PLANNED. | P07 | Medium | Medium | NEW |
| U12 | Reaction target | A modal target button routes on click; no visible timer in `logchopbutton`, so it is a simple reaction prompt rather than a full QTE. `screens.rpy:4609-4615`. | NOT NEEDED. | NOT PLANNED. | — | Small | Low | NEW |
| U13 | Score/round party game | Card rounds maintain per-player points and render a scoreboard overlay. `screens.rpy:4444-4552`, `script9.rpy:13511+`. | NOT NEEDED. | NOT PLANNED. | Score system | Large | Medium | NEW |
| U14 | Slot-machine decision loop | `slotmenu` offers play/leave and story loop; no reusable probability engine was found in the screen itself. `screens.rpy:2680-2692`, `script4.rpy:538-636`. | NOT NEEDED. | NOT PLANNED. | — | Small | Low | NEW |

## V. Misc UX

| ID | Feature | Eternum / edge / input | HIF value / why | HIF / gap | Dep | Size | Risk | Old |
|---|---|---|---|---|---|---|---|---|
| V01 | Nickname editing | In-game Preferences can edit nickname only when unlocked and outside replay; Enter/dismiss closes input. `screens.rpy:1414-1423`. | LATER if protagonist naming exists. | TODO/NOT PLANNED now. | Persistent/player identity | Medium | Medium | NEW |
| V02 | Currency HUD | `eternals` overlay shows current currency; relationship hub also shows money with negative-state color. | NOT NEEDED until economy exists. | NOT PLANNED. | Economy | Medium | Low | NEW |
| V03 | Counter/score overlays | Contest/round/attack overlays expose temporary state without leaving dialogue. | LATER under concrete mini-game. | TODO only with its feature. | Overlay system | Small | Low | NEW |
| V04 | Dynamic help page | Help switches keyboard/mouse/gamepad and hides gamepad tab if unavailable. | USEFUL before public build. | PARTIAL — main-menu Help hook exists; current bindings are not generated/documented. | Input map | Medium | Low | NEW |
| V05 | Responsive touch variant | Ren'Py supplies small/touch styles and changes quick menu. | LATER mobile. | NOT PLANNED current PC target. | Platform UI | Large | Medium | NEW |
| V06 | Screenshot notification | Screenshot action is documented and uses notify feedback. | LATER. | TODO/NOT PLANNED now. | K10/M01 | Small | Low | NEW |
| V07 | Replay-aware navigation | Save actions disappear in replay and End Replay replaces them. | REQUIRED only with replay. | TODO. | S04 | Medium | High | NEW |
| V08 | External-link actions | UI opens community pages in browser. | NOT NEEDED core game. | NOT PLANNED. | Platform | Small | Low | NEW |

---

## Features missing from previous tracker

Старый tracker содержал 33 крупные строки и смешивал несколько независимо реализуемых функций. Повторный аудит добавил или существенно раскрыл следующие группы:

- **Dialogue:** speaker/narration, auto-hide dialogue window, textbox accessibility, inline pacing, expressive text effects, hide UI, scripted pauses/transitions (`A04`–`A11`).
- **Quick Menu:** persistent visibility, explicit modal contract, touch variant (`B03`–`B05`).
- **Save/Load:** separate shortcut actions, save naming, multi-page navigation, corrupt-slot behavior, HIF-only pre-load checkpoint, explicit special-scene save policy (`C05`–`C10`, `C14`, `C17`–`C18`).
- **Auto/Skip:** Auto-vs-Skip ownership, hold-vs-toggle distinction, skip-all and transition-skip absence (`D05`, `E01`, `E04`, `E07`).
- **History/Rollback:** capacity, tag safety, mouse/gamepad rollforward/rollback, rollback-side setting and unverified engine boundaries (`F02`–`F07`).
- **Choices/State:** conditional hotspots, disabled-choice policy, result beat, timed choice, choice-history absence, counters/resources, route state and separate persistent/seen state (`G03`–`G08`, `H02`, `H04`–`H06`).
- **Relationships:** character hub, met/locked/lost states, biography/status, unlockable looks and gallery bridge (`I02`–`I07`).
- **Settings:** 22 settings/actions audited separately instead of one broad row, including false HIF affordances that are stored but not applied (`J01`–`J22`).
- **Input/navigation:** safe Space advance, Enter/Alt+A/Tab/H/screenshot, controller help, About/Extras/replay navigation/external links (`K02`–`K12`, `L05`–`L09`).
- **Notifications/audio/visual:** error/cancel policy, layered audio and ambience, fades, reduced motion, z-order, title cards and overlays (`M05`–`M06`, `N01`–`N08`, `O01`–`O09`).
- **Interactive/map:** conditional objects, modal exploration, media zoom, photo interaction, return routing and transition SFX (`P02`–`P07`, `Q04`–`Q05`).
- **Phone/gallery:** typed condition/effect requirement, typing/media/contact/SFX, replay scope, filters and lock presentation (`R03`–`R08`, `S02`–`S07`).
- **QTE/mini-games:** multi-step/wrong-target/difficulty/retry/save-policy, investigation, survival, trial, camera, scoreboard and slot loop (`T03`–`T07`, `U06`–`U14`).
- **Misc:** nickname, currency/counter overlays, dynamic help, touch layout and screenshot UX (`V01`–`V08`).

## Features intentionally excluded from How I Fall

- Direct Ren'Py rollback stack and side-of-screen rollback gesture — until HIF defines a bounded reversible-state model.
- Voice controls without a voice pipeline.
- Player-resizable textbox width/height — responsive Unity layout should remain authoritative.
- Save naming and unlimited pages — current 6-slot model is clearer.
- Touch/mobile quick menu — current target is PC.
- Unlockable character outfits/looks, economy HUD and community links — no current story/system need.
- Multi-stage reaction combat, lock-picking, ball timing, party scoreboard, slot loop and camera grind — low VN value for the next slice.
- Any `eval`/`exec` model from chat — HIF must use typed conditions/effects.
- Eternum assets, texts, fonts, screen layouts, labels, story names and audiovisual presentation.

## How I Fall gaps that matter now

1. Relationship changes have no player-facing feedback despite existing relationship fields.
2. Main/VN settings store several options that do nothing: resolution, refresh rate, language, font size, look/style, animation toggles; ambience volume has no source.
3. Input works but is not a single documented/rebindable map; HIF bindings intentionally differ from Eternum.
4. Backlog is session-only.
5. There is no generic modal/special-scene coordinator for future QTE/map/chat/save restrictions.
6. Conditional choices remain absent, but they are not automatically the best NEXT before smaller high-value UX cleanup.

## Audit conclusion

Eternum is strongest as a reference for a mature VN shell: save families, readable navigation, Auto/Skip/History, relationship feedback, persistent seen/unlock state, and occasional self-contained interactive modes. How I Fall already covers the highest-risk save backbone and core dialogue comfort. The safest next feature is not a large branching or mini-game subsystem, but **relationship change feedback** using existing `GameState` and toast infrastructure; it gives immediate narrative value without changing `SaveData` or scene architecture.
