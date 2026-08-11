# How I Fall — Eternum 0.9.5 Main Menu / Settings / Game UI gap audit

**Тип работы:** source/code/scene audit, без runtime-изменений How I Fall.

**HIF baseline:** `35af90fa8ab4f24ab982e2deba3e5185695bd122` (`master`).

**Reference:** Eternum 0.9.5, только как UX/behavior reference. Код и assets не переносятся.

## 1. Executive summary

Главная проблема How I Fall — не отсутствие отдельных настроек, а раздробленная player shell:

- Main Menu открывает `SettingsPanelController` с вкладками и большим набором полей;
- VN открывает отдельный `VNSettingsPresenter` с другим layout и только семью controls;
- Quick Menu одновременно исполняет роль быстрых действий и неполного Game Menu;
- Escape закрывает уже открытые панели, но из обычного диалога не открывает полноценную навигацию;
- часть полей `GameSettings` только сохраняется, часть скрыта, а часть имеет consumer без достижимого player-facing эффекта.

Eternum подтверждает другой принцип: Main Menu и gameplay ведут в одну Preferences-систему; Quick Menu и Game Menu — разные слои; Preferences показывает только реально действующие controls. Для HIF нужен один shared Preferences presenter/controller поверх существующих `GameSettings` и `SettingsManager`, затем отдельный Game Menu. Save backend, Replay, Chat, Character Hub, Hide UI, Auto/Skip и Special Mode переделывать не требуется.

Ключевые решения аудита:

1. `VNSettingsPresenter` как отдельный subset должен быть заменён shared Preferences UI, а не расширяться ещё раз.
2. Полноценный HIF Game Menu отсутствует и нужен.
3. Quick Menu следует сократить до быстрых действий и одной кнопки `Menu`; Manual Load, Characters и прямой Main Menu должны уйти из него.
4. B03 не реализовывать отдельно: включить в shared Preferences phase и дополнить geometry contract.
5. Fake/неполные controls не показывать, пока у них нет проверяемого эффекта.

## 2. Evidence и границы проверки

### Eternum source inspected

- `game/screens.rpy`: `say`, Quick Menu, navigation/game-menu chrome, Main Menu, Preferences, Save/Load, History, Help, confirm, touch variant;
- `game/options.rpy`: defaults, menu music, transitions, persistent UI fields, shortcuts;
- `game/gui.rpy`: базовая geometry/typography/history/save layout;
- `game/save_name.rpy`: save naming, slots, pages, modal behavior;
- `game/gallery.rpy`: Gallery/replay modal, filters, Return;
- `game/pax.rpy`: relationship/extras hub и фактический вход в Gallery;
- `game/text.rpy`, `game/transform.rpy`: consumers custom text geometry и motion preference;
- `game/save_compatibility.rpy`: проверен как связанный save-flow, но не является источником shell layout.

### How I Fall inspected at `HEAD`

- `Assets/HowIFall/Scenes/MainMenu.unity` и `VNPrototype.unity` (serialized structure/wiring);
- `MainMenuController`, `MainMenuAnimator`, `SettingsPanelController`, `ManualSaveLoadPanel`, `VNQuickMenu`;
- `GameSettings`, `SettingsManager`, `SettingsOptionValues`, `AudioManager`;
- `VNDialogueController`, `VNSettingsPresenter`/`VNSettingsService`, `VNInputMap`, `SpecialModeCoordinator`;
- settings/save/quick-menu smoke tests и существующие reference docs.

Изменённые пользователем runtime-файлы в worktree не использовались как новый baseline: выводы сделаны по `HEAD`, чтобы не смешивать незакоммиченный B03/runtime work с утверждённым состоянием.

### Runtime visual reference

Reference build был безопасно запущен и остановлен, но Computer Use не смог захватить окно (`SetIsBorderRequired failed: 0x80004002`). Созданный запуском `log.txt` удалён; source/reference assets не менялись, screenshots не создавались и не коммитились.

Поэтому behavior ниже подтверждён source, но следующие детали помечены **NEEDS RUNTIME VISUAL REFERENCE**:

- точный default child screen при Esc/right-click (source не переопределяет engine `_game_menu_screen` вне replay scope);
- визуальная последовательность enter/intra/exit transition в реальном build;
- focus/selected/disabled rendering и scrolling feel Preferences;
- точная композиция Save/Load, History, confirm и Gallery на экране;
- сочетание custom Russian patch/external buttons с оригинальной UI-сборкой.

## 3. Eternum Main Menu map

Source: `screens.rpy:398-736`, `options.rpy:15-27,66,77-93`.

| Порядок / элемент | Visible condition | Action / destination | Back/modal/state |
|---|---|---|---|
| 1. New Game | всегда | `Start()` | заменяет Main Menu gameplay-состоянием |
| 2. Load | всегда | `ShowMenu("load")` | открывает Load в menu context; Return ведёт обратно |
| 3. Preferences | всегда | `ShowMenu("preferences")` | тот же Preferences screen, что из gameplay |
| 4. Credits/About | всегда | `ShowMenu("about")` | отдельный About, Esc/right-click/Return закрывают |
| Quit | desktop | `Quit(confirm=True)` | обязательный modal confirm; Esc/right-click = No |
| External social links | всегда в этой сборке | `OpenURL(...)` | не является VN parity и не переносится |

Подтверждения/опровержения:

- **Continue:** отсутствует; wired Continue не найден.
- **Gallery/Extras:** в Main Menu отсутствует.
- **Gallery entry:** открывается из `point` relationship/extras hub (`pax.rpy`), не из Main Menu.
- **Relationship hub:** `screen point`, modal; вход в него существует как отдельная gameplay-facing action, а не Main Menu row.
- **Help:** source screen существует, но Main Menu/Game navigation его не открывают.
- **Version/build text:** версия показана в About, не доказана как Main Menu label.
- **Background/music:** custom layered background; `config.main_menu_music` запускает menu music. Enter/exit/intra transitions — dissolve, end-game — longer dissolve.
- **Main-menu navigation include:** `use navigation` фактически не добавляет стандартные rows при `main_menu=True`; видимые четыре карточки реализованы отдельно.

## 4. Eternum Game Menu map

Eternum не делает Quick Menu «пауз-меню». `screen game_menu(title, ...)` — общий full-screen chrome для menu screens. Он резервирует слева navigation region, справа content region, добавляет title и `Return`.

```text
Gameplay
├─ Quick Menu (overlay)
│  ├─ History / Save / Preferences ...
│  └─ быстрые режимы Auto / Skip
└─ Game Menu chrome (Esc / right-click / ShowMenu)
   ├─ Save
   ├─ Load
   ├─ Preferences
   ├─ Main Menu
   ├─ Quit (PC)
   └─ Return
```

| Navigation action | Normal gameplay | Replay |
|---|---:|---:|
| Save | да | нет |
| Load | да | нет |
| Preferences | да | нет в replay navigation branch; Quick Menu Prefs остаётся отдельным входом |
| Main Menu | да, с engine confirmation | заменено на End Replay |
| End Replay | нет | да, `confirm=True` |
| Quit | да, confirm | да, confirm |
| Return | всегда в game-menu chrome | всегда |

Additional behavior:

- Esc и right-click документированы как вход в Game Menu; на уже открытых menu screens `game_menu` возвращает назад.
- `History` не входит в left navigation, но открывается из Quick Menu и использует тот же game-menu chrome.
- `Preferences` — custom full-screen screen, не transclusion внутри `game_menu`, но использует тот же `ShowMenu` state stack и Return semantics.
- Save/Load используют общий content region и Return. Main Menu Load не требует warning о несохранённом gameplay.
- Точный screen, открываемый первым по bare Esc/right-click, **NEEDS RUNTIME VISUAL REFERENCE**: project source не задаёт обычный `_game_menu_screen`, кроме replay scope.

## 5. Eternum Quick Menu map

Desktop order в source:

1. Back (`Rollback`)
2. History
3. Skip
4. Auto
5. Save
6. Q.Save
7. Q.Load
8. Prefs
9. external localized link в данной сборке — исключён из parity

Contract:

- overlay с `zorder 100`, bottom-centered;
- показывается только при `quick_menu && persistent.quick_menu`;
- Auto/Skip используют action-selected state Ren'Py;
- Back зависит от rollback stack;
- touch variant сокращён до Back / Skip / Auto / Menu;
- H/engine interface hide скрывает overlay, но persistent preference не меняется;
- disabling persistent Quick Menu скрывает только панель, не hotkeys и не Game Menu.

Geometry correction для B03: `persistent.quick_menu` используется не только в `screen quick_menu`, но и в `say` layout. При ON textbox поднят на 32 reference pixels над lower edge; при OFF этот reserve исчезает. Width/height остаются значениями textbox preferences. HIF policy должен требовать аналогичный **safe-area reserve contract**, а не буквальные `32 px`.

## 6. Eternum Preferences map

Preferences — **один full-screen screen без tabs**. Внутри два независимых вертикально scrollable viewport:

- wide left column: dialogue/audio/textbox controls;
- narrow right column: display/language/behavior toggles;
- отдельный Return control;
- Esc/right-click вызывает `Return()`;
- значения применяются сразу и сохраняются engine preferences или `persistent`.

### Player-facing settings

| Section | Control | UI type | Default/range | Persistence / runtime consumer |
|---|---|---|---|---|
| Dialogue | Text Speed | slider | `preferences.text_cps=0`; engine range | engine preference; say text reveal, immediate |
| Dialogue | Auto-Forward Time | slider | default `15`, valid engine range 0..30 | engine preference; AFM timer, immediate |
| Audio | Music Volume | slider | source does not override engine numeric default | music mixer, immediate |
| Audio | Sound Volume | slider | source does not override engine numeric default | sound mixer, immediate |
| Audio | Mute All | toggle/button | engine preference | all mixers, immediate |
| Accessibility | Text Size | slider + reset | 20..50, default `gui.text_size=32` | `persistent.text_size`; say/custom text consumers |
| Accessibility | Text Outline | slider + reset | 0..4, default 2 | `persistent.text_outline`; say/custom text consumers |
| Accessibility | Textbox Opacity | slider + reset | 0..1, default 0 | textbox alpha, immediate |
| Accessibility | Textbox Width | slider + reset | 1116..1646, default `gui.dialogue_width=1130` | say/custom text geometry |
| Accessibility | Textbox Height | slider + reset | 100..350, default `gui.textbox_height=270` | say/custom text geometry |
| Context | Edit Nickname | text input | only in-game, not replay, after unlock | store variable; not a general setting for HIF |
| Display | Window / Fullscreen | radio | engine display preference | window mode, immediate |
| Language | available languages | radio/list | only if translations are discovered | engine language, immediate |
| Input | Rollback Side | Disable / Left / Right | engine preference | screen-edge rollback; not needed without rollback |
| Skip | Unseen Text | check | engine preference | whether unseen dialogue may be skipped |
| Skip | After Choices | check | engine preference | skip resume after a choice |
| Interface | Quick Menu | Enabled / Disabled | default ON | `persistent.quick_menu`; overlay + say offset |
| Interface | Interface Motion | Enabled / Disabled | desktop ON, mobile OFF | `persistent.motion`; custom transitions/transforms |

### Не player-facing в Preferences

- Voice slider закомментирован: `config.has_voice=True` не делает его видимым.
- Transitions toggle закомментирован.
- Save naming — отдельный toggle на Save screen, default ON; не Preferences row.
- Gallery unlock override — developer/profile persistence, не standard preference.
- Нет отдельного Master Volume, Ambience, resolution list, refresh-rate selector, Auto mode toggle или autosave toggle.

## 7. Eternum Save/Load navigation и confirmations

- Main Menu → Load: без gameplay-loss confirmation.
- Gameplay → Save/Load: внутри common Game Menu chrome.
- Quick Menu → Save/Q.Save/Q.Load: прямые быстрые входы.
- Save naming default ON открывает modal до new/overwrite; Enter подтверждает, Esc/right-click отменяет.
- Slot grid: 3×2; Auto, Quick и numeric pages; page switching remembered.
- Generic `confirm` modal имеет `modal True`, `zorder 200`; Yes/No; Esc/right-click выбирает No.
- Quit имеет custom appearance, но тот же modal/back contract.
- Gallery — отдельный modal поверх relationship/extras hub; Return/Esc скрывает Gallery и возвращает в hub.

## 8. Current HIF Main Menu map

Serialized order и wiring в `MainMenu.unity`:

| Order | Element | Exists/wired | Current status | Decision |
|---:|---|---|---|---|
| 1 | Новая игра | `MainMenuController.StartGame` | working | KEEP |
| 2 | Продолжить | newest compatible Manual/Quick/Auto; disabled if none | HIF improvement | KEEP |
| 3 | Загрузить | `ManualSaveLoadPanel.OpenLoad` | working, fixed tabs/slots | KEEP |
| 4 | Настройки | `SettingsPanelController.Show` | working but divergent architecture | REWORK to shared Preferences |
| 5 | Об игре | separate panel | working/basic | KEEP, unify back/modal style |
| 6 | Помощь | `VNInputMap.BuildHelpText()` | working, graphical QA historically pending | HIF IMPROVEMENT / KEEP |
| 7 | Выход | explicit confirm panel | working | KEEP, unify modal style |
| Gallery | panel/controller/replay backend exist | **no wired Main Menu entry found** | inaccessible technical foundation | DEFER placement; preserve backend |

Other facts:

- `MainMenuAnimator` owns fade/background motion only; it does not consume `characterAnimations`, `backgroundAnimations` or `interfaceStyle`.
- menu music is a separate `MainMenuMusicPlayer` using `AudioManager`.
- Settings panel hides the logo/menu while open and always returns to Audio tab.
- Main Menu has no general Escape routing controller; individual panels rely on wired close buttons.

## 9. Current HIF Game Menu reality

**GAP: полноценного Game Menu нет.**

Current gameplay navigation is split between:

- bottom Quick Menu;
- standalone History panel;
- standalone VN Settings panel;
- `ManualSaveLoadPanel`;
- standalone Main Menu confirmation;
- Character Hub runtime modal;
- Chat/media/special-mode owners.

Ordinary-dialogue Esc does not open navigation. It only restores H-clean view or closes, in order, Character Hub → media viewer → cancellable Special Mode → History → Main Menu confirm → VN Settings; `ManualSaveLoadPanel` consumes its own Escape. Main Menu is reached from a Quick Menu button through confirmation. This is panel routing, not a Game Menu information architecture.

## 10. Current HIF Quick Menu map

Serialized order plus runtime-created Characters button:

1. History
2. Skip
3. Auto
4. Save
5. Quick Save
6. Quick Load
7. Load
8. Settings
9. Characters (runtime clone after Settings)
10. Main Menu / End Replay

Strengths:

- reuses existing controller/backend APIs;
- Auto/Skip selected state visible;
- Replay hides all save/load controls and changes Main Menu to End Replay;
- H and BlockingExclusive visibility ownership are guarded;
- Character Hub ordinary modal does not steal Special Mode ownership.

Gaps:

- no Back/rollback by explicit design;
- ten controls overload the quick strip compared with reference eight;
- Manual Load, Characters and direct Main Menu belong to broader navigation;
- Characters is runtime-cloned from Settings, so layout/order is not fully serialized or obvious;
- visible root state and action permission are related but still owned by several code paths.

## 11. Current HIF Settings architecture

```text
GameSettings
   └─ SettingsManager (PlayerPrefs + selected runtime consumers)
      ├─ SettingsPanelController (Main Menu, 3 tabs, many fields)
      └─ VNSettingsService → VNSettingsPresenter (gameplay, 7 controls)
```

Persistence authority уже один, но presentation/labels/control set — два. `VNSettingsService` лишь проксирует subset, а `VNSettingsPresenter.Reset()` сбрасывает **все** настройки, включая невидимые в этой панели. Это создаёт drift и неочевидный reset.

## 12. Full HIF settings truth table

`Main UI`: `yes`, `hidden` (serialized, затем `SetActive(false)`), `no`.

`VN UI`: compact VN Settings panel.

| Setting / default / key | Main UI | VN UI | Actual consumer/effect | Status | Parity decision / destination |
|---|---:|---:|---|---|---|
| `masterVolume=0.8`, `hif_master_volume` | yes | yes | `AudioListener.volume` | REAL | KEEP / Audio |
| `musicVolume=0.8`, `hif_music_volume` | yes | yes | `AudioManager.musicSource.volume` | REAL | KEEP / Audio |
| `sfxVolume=0.8`, `hif_sfx_volume` | hidden | yes | `AudioManager.sfxSource.volume` | REAL, inconsistent visibility | KEEP / Audio, show in shared UI |
| `ambientVolume=0.8`, `hif_ambient_volume` | yes in scene | no | ambience sources × fade gain; no authored ambience in current slice | PARTIAL | HIDE UNTIL AUTHORED CONTENT, then Audio |
| `musicDuringPause=false`, `hif_music_during_pause` | yes | no | sets `ignoreListenerPause`, but no `AudioListener.pause` path found | PARTIAL / currently no reachable effect | HIDE UNTIL GAME MENU pause policy exists |
| `screenMode=Fullscreen`, `hif_screen_mode` | yes | alias toggle | maps Exclusive/Windowed/Borderless to `Screen.fullScreenMode` | REAL | KEEP / Display |
| `resolution=1920x1080`, `hif_resolution` | yes | no | parsed and sent to `Screen.SetResolution` | REAL | KEEP / Display; validate available resolutions later |
| `refreshRate=60`, `hif_refresh_rate` | hidden | no | saved only | PLACEHOLDER | HIDE; remove unless real Unity API consumer added |
| `gameLook=Чистый`, `hif_game_look` | hidden | no | saved only | PLACEHOLDER | HIDE/REMOVE |
| `interfaceStyle=Классический`, `hif_interface_style` | hidden | no | saved only | PLACEHOLDER | HIDE/REMOVE; do not confuse with motion |
| `rewindVhsFilter=true`, `hif_rewind_vhs_filter` | hidden | no | saved only | PLACEHOLDER | HIDE/REMOVE until rollback/VHS exists |
| `runInBackground=false`, `hif_run_in_background` | yes | no | `Application.runInBackground` | REAL | KEEP as HIF-specific / Display-Advanced |
| `characterAnimations=true`, `hif_character_animations` | hidden | no | saved only | PLACEHOLDER | HIDE/REMOVE |
| `backgroundAnimations=true`, `hif_background_animations` | hidden | no | saved only; `MainMenuAnimator` ignores it | PLACEHOLDER | HIDE/REMOVE; replace only with real motion policy |
| `language=Русский`, `hif_language` | hidden | no | saved only, no localization switch | PLACEHOLDER | HIDE UNTIL TRANSLATIONS |
| `fontSizeMode=Мелкий`, `hif_font_size_mode` | hidden | no | saved only | PLACEHOLDER | REPLACE with real text-size parity setting |
| `skipMode=Виденное`, `hif_skip_mode` | yes | no | Seen vs All is consumed; third value `Ничего` collapses to Seen behavior | PARTIAL / misleading option | REWORK to boolean Allow Unseen / Dialogue |
| `skipBehavior=Classic`, `hif_skip_behavior` | yes | no | Classic/Fast changes cadence | REAL | KEEP HIF-specific / Dialogue |
| `textSpeed=50`, `hif_text_speed` | yes | yes | typewriter characters per second | REAL | KEEP / Dialogue |
| `autoForwardDelay=250`, `hif_auto_forward_delay` | yes, mislabeled `%` | yes | mapped to 0.5..5.0 seconds | REAL effect, PARTIAL UI semantics | KEEP; display seconds / Dialogue |
| `skipAfterChoices=false`, `hif_skip_after_choices` | yes | no | controls Skip resume after choice | REAL | KEEP / Dialogue |
| `autoForward=false`, `hif_auto_forward` | yes | yes | Auto timer and Quick Menu state | REAL | KEEP state, MOVE toggle out of Preferences to Quick Menu |
| `autoSave=true`, `hif_auto_save` | yes | no | gates authored autosave path; pre-load safety remains separate | REAL | KEEP HIF-specific / Saves |
| `showHints=true`, `hif_show_hints` | hidden | no | saved only | UNUSED | HIDE/REMOVE |
| `fullscreen=true`, `hif_fullscreen` | no | yes | derived alias of `screenMode`; key is saved but never loaded | PARTIAL / duplicate truth | REMOVE as independent field/UI; shared Display uses `screenMode` |
| `showQuickMenu` / `hif_show_quick_menu` | no field at HEAD | no | policy only | DEFERRED B03 | IMPLEMENT WITH shared Preferences, not separately |

## 13. Fake, misleading и partial controls

| Control | Finding | Required action |
|---|---|---|
| Refresh Rate | storage/UI shell only | remain hidden; remove if not scheduled |
| Game Look | no renderer/post-process consumer | remain hidden |
| Interface Style | no skin switch consumer | remain hidden |
| Rewind VHS Filter | no rollback/VHS runtime | remain hidden |
| Character Animations | no animation owner reads it | remain hidden |
| Background Animations | no owner reads it | remain hidden; do not alias to Interface Motion without migration |
| Language | no localization backend | remain hidden |
| Font Size | no TMP/textbox consumer | replace with real text-size setting, not rename-only |
| Show Hints | no hint system consumer | remain hidden/remove |
| Music During Pause | consumer flag exists, but no audio pause path | hide until Game Menu pause semantics are real |
| Ambient Volume | mixer consumer exists, but current content has no authored ambience; scene currently exposes it despite earlier policy | hide until first authored ambience; fix during shared UI rework |
| Skip Mode = Nothing | runtime treats it like Seen | remove third option; preserve only Seen/All semantics |
| Auto delay label | `%` does not describe runtime seconds | relabel to seconds |
| Fullscreen toggle | duplicates `screenMode` and has write-only legacy key | remove from shared UI/model truth |

## 14. Eternum ↔ HIF gap matrix

| Area | Eternum | HIF current | Gap / decision |
|---|---|---|---|
| Shared Preferences | one screen from both contexts | two presentations/subsets | build one shared UI |
| Preferences layout | one screen, two scroll columns, no tabs | Main 3 tabs + VN compact panel | replace, do not extend tabs |
| Game Menu | full-screen navigation chrome | absent | implement after shared Preferences |
| Quick Menu | 8 core actions | 10 actions, no Back | reduce to fast actions + Menu; Back deferred |
| Esc/right-click | opens/returns Game Menu | ordinary Esc does nothing; no right-click route | add only after ownership precedence tests |
| Textbox accessibility | size/outline/opacity/width/height real | absent/fake font-size field | implement real consumers incrementally |
| Quick Menu visibility | persistent + textbox offset | policy only | absorb B03; add safe-area geometry |
| Interface motion | one global consumer value | multiple fake animation fields | implement one real policy or defer |
| Mute All | player-facing | absent | implement with clear restore semantics |
| Save naming/pages | large Ren'Py library | fixed 6×3-type HIF system | HIF version intentionally kept |
| Gallery placement | relationship/extras hub | backend panel exists, no wired entry | do not add Main Menu button merely for parity; decide Extras hub later |
| Help | source exists but unwired | wired Main Menu Help | keep HIF improvement |

## 15. HIF improvements to preserve

| HIF difference | Eternum behavior | Why HIF version is better here | Keep? |
|---|---|---|---:|
| Continue latest | no wired Continue | immediate recovery across valid slot types | YES |
| Strict SaveData validation | mostly engine-owned compatibility | predictable corrupt/incompatible failure | YES |
| Pre-load autosave | no custom checkpoint found | protects against accidental load | YES |
| Fixed 6 Manual/Auto/Quick slots | unbounded pages | clearer prototype UX, bounded QA | YES |
| Visible delete + confirmations | engine action/hotkey oriented | discoverable and consistent | YES |
| Hide UI safety gates | engine hide is broader/default-driven | prevents hidden progression/save/modal conflicts | YES |
| `SpecialModeCoordinator` | no equivalent explicit cross-system contract found | fail-closed ownership for authored modes | YES |
| Typed conditions | Ren'Py expressions | safe, serializable Unity content contract | YES |
| Replay isolation | engine replay scope | protects campaign state/backlog/read history/audio | YES |
| Unified `VNInputMap` Help | default Help not wired to navigation | actual HIF bindings remain discoverable | YES |

## 16. Current architecture risks

| Severity | Risk | Why | Fix phase |
|---|---|---|---|
| HIGH | duplicate Settings UI | different controls/labels/back behavior; reset affects unseen fields | Shared Preferences Foundation |
| HIGH | no Game Menu | navigation scattered across Quick Menu/modals | Game Menu phase |
| HIGH | fake/partial fields in persisted model | PlayerPrefs is mistaken for functionality | Foundation truth cleanup |
| HIGH | `fullscreen` + `screenMode` dual truth | derived alias plus write-only key can drift | Foundation migration |
| HIGH | direct `SetActive` ownership | new screen can resurrect/hide another owner's UI | shared shell routing + precedence tests |
| MEDIUM | serialized Main Menu wiring | high change risk, no scene edits should occur before controller contract | Preferences UI/Main Menu phase |
| MEDIUM | `SettingsPanelController` size | one class owns tabs, labels, options and every setter | replace, do not grow |
| MEDIUM | VN subset drift | VN exposes SFX while Main hides it; lacks skip/autosave/display detail | shared UI |
| MEDIUM | `controlsHiddenUntilImplemented` array | hides SFX but leaves current ambient row visible; intent is opaque | shared UI rebuild |
| MEDIUM | runtime-created Characters button | ordering/layout not visible in scene; strip becomes crowded | Quick Menu phase |
| MEDIUM | legacy builder assumptions | builders are retired; scene/prefab are authoritative | do not resurrect builders |
| LOW | fixed option lists | resolutions may not match monitor; labels are duplicated strings | Preferences parity/QA |

## 17. Visual information architecture findings

| Screen | Eternum evidence | HIF current | Certainty |
|---|---|---|---|
| Main Menu | full-screen custom composition, four primary cards, separate quit/socials | full-screen left list | source-confirmed; final visuals need runtime reference |
| Game Menu | full-screen background; left navigation + right content + Return/title | absent | source-confirmed layout regions |
| Preferences | full-screen; wide left + narrow right scroll viewports; Return | two unrelated UIs | source-confirmed, exact visual feel pending |
| Save/Load | game-menu chrome + 3×2 cards + page controls | modal/prefab with fixed slot tabs | source + existing HIF docs |
| History | game-menu chrome + vertical viewport | standalone modal | source-confirmed |
| Confirm | modal zorder, blocked underlying input, Yes/No, Esc=No | several separate panels | behavior source-confirmed, art pending |
| Gallery | modal from relationship hub, 3-column scroll/filter | technical panel with no wired entry | source-confirmed placement |

## 18. Corrections to prior audit/policy

No evidence-backed Eternum correction requires editing `docs/eternum_full_feature_audit.md`: its B03/J/Main Menu statements remain materially correct.

B03 policy requires one implementation-spec clarification:

```text
quickMenuSafeAreaReserve = effectiveQuickMenuVisible ? measuredQuickMenuReserve : 0
```

The reserve must affect dialogue/textbox bottom geometry immediately, use anchors/safe area rather than a copied `32 px`, and obey the same persistent/H/Special Mode precedence as the Quick Menu root. Hotkeys remain active when the panel is persistently hidden.

## 19. Conclusions

- HIF has strong backend mechanics but fragmented player-facing shell.
- The next safe dependency is shared Preferences ownership, not B03 alone and not scene polish.
- `SettingsPanelController` and `VNSettingsPresenter` should not coexist as long-term player screens.
- A full Game Menu is required after shared Preferences exists.
- Fake controls stay hidden; storage alone is not DONE.
- HIF safety improvements remain authoritative when they exceed Eternum.
- Final art direction remains original HIF work; source audit supplies behavior and information architecture only.
