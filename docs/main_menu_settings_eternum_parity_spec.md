# How I Fall — Eternum Main Menu / Game Menu / Preferences parity spec

**Status:** implementation blueprint; no C# or Unity scene changes in this task.

**Reference priority:** Eternum behavior first, then proven HIF safety improvements.

**Dependency owner:** existing `GameSettings` + `SettingsManager`.

**Last reviewed functional commit:** `f7c07b25fbf68c279d1406aaf8d0eabaabc4c672`.

## 1. Target principles

1. One player-facing Preferences screen from Main Menu and gameplay.
2. Quick Menu is an overlay of fast actions; Game Menu is navigation.
3. No visible setting without a verified runtime consumer and testable effect.
4. Main Menu/Game Menu differ only in navigation context, not in settings truth.
5. Existing SaveManager, Replay, Chat, Character Hub, Hide UI, Auto/Skip, AudioManager and Special Mode contracts survive unchanged.
6. Unity implementation stays simple: no service locator, DI framework, generic UI framework, manager-per-tab or ScriptableObject settings graph.

## 2. Target ownership

```text
GameSettings
    │
SettingsManager
    │  persistence + setters + runtime apply
    ▼
Shared Preferences presenter/controller
    ├─ Main Menu entry (Back → Main Menu)
    └─ Game Menu entry (Back → Game Menu/gameplay stack)
```

Required shape:

- one controller/presenter owns labels, option lists, refresh and callbacks;
- one view/prefab or one structurally identical screen is reused in both contexts;
- context supplies only Back destination, underlying visual layer and modal ownership;
- no second storage and no mirrored `VNSettingsService` subset;
- `VNSettingsPresenter` is removed/replaced after all current VN entry points are redirected;
- `SettingsPanelController` is retired or reduced to a thin compatibility entry during migration, not expanded.

## 3. Target Main Menu

| Element | Eternum | HIF current | Target HIF | Why |
|---|---|---|---|---|
| New Game | yes | yes | KEEP, first primary action | parity |
| Continue | no | yes | KEEP, before New Game or immediately after it according to final visual hierarchy | proven HIF recovery improvement |
| Load | yes | yes | KEEP | parity + working backend |
| Preferences | yes | divergent Main settings | REWORK to shared screen | central architecture goal |
| Gallery/Extras | not in Main Menu; entered from relationship hub | backend panel exists but no wired entry | DEFER top-level button; preserve backend and decide a coherent Extras/Characters hub later | do not relocate solely by assumption |
| Help | screen exists but not wired in reference navigation | yes | KEEP | HIF bindings are non-default and need discoverability |
| Credits/About | yes | yes | KEEP | parity |
| Quit | yes, confirm | yes, confirm | KEEP with shared modal contract | parity |

Recommended information hierarchy:

```text
Continue (enabled only with compatible save)
New Game
Load
Preferences
Help
About
Quit
```

If visual testing shows New Game should remain first for a new profile, it may precede Continue; semantics do not change. Gallery is not inserted into this list during the shell parity work.

### Main Menu behavior

- full-screen screen; existing original HIF art/background remains;
- no hidden gameplay Quick Menu;
- Back from Preferences/Load/Help/About returns to Main Menu without reloading the scene;
- Quit always uses shared confirmation modal;
- Continue unavailable state is visible and non-clickable, with no fake error path;
- Main Menu music continues under child screens unless an explicit audio policy says otherwise.

## 4. Target Game Menu

HIF needs a real Game Menu separate from Quick Menu.

### Entry

- Esc from stable ordinary dialogue opens it;
- right-click may open it only after a focused input QA pass and must not conflict with dialogue advance/context actions;
- Quick Menu `Menu` opens it;
- no opening while H-clean view is active, Character Hub/Chat/media/confirmation/save panel owns input, or BlockingExclusive denies navigation;
- opening stops current Auto/Skip timers without changing their stored/runtime enabled states.

### Normal gameplay navigation

Recommended order:

1. Return
2. Save
3. Load
4. Preferences
5. History
6. Characters / Extras entry (only if current Character Hub remains approved)
7. Main Menu
8. Quit

Differences from Eternum are explicit:

- History is included for discoverability because HIF has no rollback `Back` and already exposes B/History.
- Characters may live here rather than Quick Menu; it is an HIF-specific ordinary modal.
- Quit is parity; if product direction later chooses Main Menu-only quit, remove it consistently rather than leave two confirmation styles.

### Replay navigation

1. Return
2. Preferences
3. History
4. End Replay
5. Quit

Replay hides Save, Load and Characters. Preferences/History remain because current HIF replay policy explicitly permits them; this is safer and more useful than copying the narrower Eternum replay navigation branch. End Replay uses confirmation and existing idempotent replay cleanup.

### Presentation/ownership

- full-screen navigation layer over gameplay, not a small floating window;
- original HIF skin, no Eternum assets/layout copy;
- navigation region and content/summary region are separate;
- underlying dialogue cannot receive clicks/advance;
- closing restores the previously eligible dialogue state and starts a fresh Auto/Skip delay;
- Game Menu owns no save/settings/replay state; it calls existing controllers.

## 5. Target Quick Menu

Recommended final PC order:

1. History
2. Skip
3. Auto
4. Save
5. Q.Save
6. Q.Load
7. Preferences
8. Menu

| Current HIF action | Decision | Reason |
|---|---|---|
| History | KEEP | Eternum parity and HIF backlog |
| Skip | KEEP | fast runtime mode; selected state required |
| Auto | KEEP | fast runtime mode; selected state required |
| Save | KEEP | parity |
| Quick Save | KEEP | parity |
| Quick Load | KEEP | parity |
| Load | MOVE TO GAME MENU | reference Quick Menu has no manual Load; reduces crowding |
| Settings | KEEP as Preferences | parity and B03 recovery path remains available through Game Menu/Main Menu too |
| Characters | MOVE TO GAME MENU/Extras | valuable HIF feature, not a universal quick action |
| Main Menu | REPLACE WITH Menu | direct destructive navigation belongs in Game Menu |
| Back/Rollback | DEFER | no reversible-state model; do not create cosmetic Back |

Contract:

- active state for Auto/Skip;
- unavailable actions visibly disabled or hidden according to context, not silently clickable;
- Replay filtering remains authoritative;
- B03 hides only the root; keyboard actions and Game Menu remain available;
- H and BlockingExclusive remain transient visibility blockers;
- runtime-created Characters clone is removed once Characters moves, so final order is serialized/testable.

## 6. Target Preferences information architecture

Eternum's one screen without tabs is the preferred model. HIF target is a **single scrollable Preferences page** with semantic sections. At wide resolutions it may render two columns inside one shared scroll context; at 1280×720 it collapses to one column. Independent tab-specific state is prohibited.

Recommended order:

1. Display
2. Audio
3. Dialogue & Auto
4. Skip & Saves
5. Accessibility & Interface
6. Advanced (only real HIF-specific controls)
7. Reset / Back

### 6.1 Display

| Control | Type | State | Source/reason |
|---|---|---|---|
| Screen Mode: Windowed / Fullscreen / Borderless | segmented/radio | REAL now | HIF Unity-specific extension of Eternum Window/Fullscreen |
| Resolution | dropdown/list of supported values | REAL now, rework static list later | HIF Unity-specific |
| Run in Background | toggle | REAL now / KEEP | direct Unity consumer; Advanced subsection acceptable |
| Interface Motion | toggle | DEFERRED until all owned transitions consume it | Eternum parity; replaces fake animation flags only after migration |

Do not show Refresh Rate until it is applied through a verified Unity display API and tested on supported monitors.

### 6.2 Audio

| Control | Type | State | Source/reason |
|---|---|---|---|
| Mute All | toggle/button | IMPLEMENT TO PARITY | Eternum player-facing behavior; define restoration of previous slider values |
| Master Volume | slider 0..100% | REAL / KEEP | HIF useful extension |
| Music Volume | slider 0..100% | REAL / KEEP | parity |
| Sound/SFX Volume | slider 0..100% | REAL / KEEP | parity; currently inconsistent visibility |
| Ambience Volume | slider 0..100% | DEFER visibility until authored ambience is present | HIF audio architecture |
| Music During Pause | toggle | DEFER until Game Menu audio-pause policy produces a reachable effect | HIF-only |

No Voice row until HIF has voice playback and a separate mixer. Eternum's commented Voice slider is not evidence to add one.

### 6.3 Dialogue & Auto

| Control | Type | State | Default/behavior |
|---|---|---|---|
| Text Speed | slider with units | REAL / KEEP | 50 chars/sec current default; immediate |
| Auto-Forward Delay | slider with seconds | REAL / RELABEL | 2.5 s current default; 0.5..5.0 s; immediate |
| Auto enabled | not a Preferences row | MOVE to Quick Menu state | mode, not configuration |

### 6.4 Skip & Saves

| Control | Type | State | Default/behavior |
|---|---|---|---|
| Allow skipping unseen text | toggle | REWORK current `skipMode` | default OFF (`Seen only`); remove misleading `Nothing` option |
| Resume Skip after choices | toggle | REAL / KEEP | default OFF |
| Skip speed | Classic/Fast segmented control | REAL HIF-specific / KEEP | clear player benefit; cadence consumer exists |
| Autosave | toggle | REAL HIF-specific / KEEP | default ON; must not disable pre-load safety checkpoint |

Save naming remains NOT NEEDED for fixed six-slot HIF UI.

### 6.5 Accessibility & Interface

| Control | Type | State | Target behavior |
|---|---|---|---|
| Show Quick Menu | toggle | B03 / IMPLEMENT in this unified phase | default ON; immediate persistent |
| Text Size | slider/presets + reset | IMPLEMENT TO PARITY | changes actual dialogue TMP size |
| Text Outline | slider/presets + reset | IMPLEMENT TO PARITY after visual proof | changes actual outline/material safely |
| Textbox Opacity | slider + reset | IMPLEMENT TO PARITY | changes only dialogue box background |
| Textbox Width | slider/presets + reset | DEFER until responsive layout consumer exists | normalized/anchored, not copied pixels |
| Textbox Height | slider/presets + reset | DEFER until responsive layout consumer exists | normalized/anchored, not copied pixels |

Text size/opacity may ship before width/height if each phase leaves a truthful screen. Hidden deferred controls are not placeholders.

### 6.6 Removed/deferred current fields

| Field | Target |
|---|---|
| `refreshRate` | hide/remove after migration unless real implementation approved |
| `gameLook` | hide/remove |
| `interfaceStyle` | hide/remove |
| `rewindVhsFilter` | hide/remove until rollback/VHS feature exists |
| `characterAnimations` | hide/remove |
| `backgroundAnimations` | hide/remove; do not silently reuse as Interface Motion |
| `language` | hide until localization backend and content exist |
| `fontSizeMode` | migrate/replace with real text-size value |
| `showHints` | hide/remove until hint system exists |
| `fullscreen` + `hif_fullscreen` | migrate away as independent truth; `screenMode` is authority |

Unknown legacy PlayerPrefs keys may be read for one migration release if required, but must not continue as competing authorities.

## 7. B03 integration contract

Canonical state:

```text
effectiveQuickMenuVisible = showQuickMenu
                         && !hiddenByPlayerCleanView
                         && !hiddenByBlockingSpecialMode
```

Add layout contract:

```text
quickMenuSafeAreaReserve = effectiveQuickMenuVisible
    ? measuredBottomReserveIncludingSpacing
    : 0
```

Requirements:

- default ON; saved outside SaveData;
- applies immediately from Main Menu and gameplay shared Preferences;
- OFF hides the whole quick strip but not hotkeys or Game Menu;
- dialogue shell uses anchors/safe-area reserve, not copied Eternum pixels;
- H/Special Mode do not mutate preference;
- changing preference under a blocker updates stored truth but does not reveal the root early;
- Reset returns ON;
- Replay button filtering runs inside the effective-visible root and remains unchanged;
- B03 is delivered with shared Preferences, not as an isolated UI patch.

## 8. Save/Load navigation target

Backend remains unchanged.

| Route | Target |
|---|---|
| Main Menu → Load | opens existing Load UI; no gameplay-loss warning |
| Game Menu → Save | existing Save UI; Return → Game Menu/gameplay stack |
| Game Menu → Load | existing Load UI; load confirmation + pre-load autosave |
| Quick Menu → Save | existing Save UI |
| Quick Menu → Q.Save | existing guarded quick save |
| Quick Menu → Q.Load | existing confirmation/pre-load pipeline |
| Replay | all Save/Load entry points hidden/denied; backend guard remains |
| Return | closes confirmation first, then Save/Load, then Game Menu |

Preserve fixed six Manual/Auto/Quick slots, validation, corrupt-save handling and pre-load autosave. Do not import Eternum naming/unbounded-page behavior.

## 9. Shared modal and confirmation contract

All shell confirmations eventually use one visual/behavior contract even if backend actions remain separate.

Required behavior:

- modal blocks raycasts/input to underlying screen;
- one clear prompt and primary/destructive + cancel action;
- Esc/right-click always means Cancel/No, never destructive Yes;
- default focus is Cancel for destructive actions;
- close one modal layer at a time;
- wording names the consequence, but final canon text is outside this spec;
- operation-in-progress state disables repeat submission;
- Auto/Skip resume only after the final modal/panel owner exits.

| Confirmation | Required |
|---|---|
| Quit | yes |
| Return to Main Menu | yes during gameplay |
| Load | yes during gameplay; no in Main Menu |
| Overwrite | yes |
| Delete | yes |
| End Replay | yes; idempotent cleanup |
| New Game with active progress | product decision later; do not add silently |

## 10. Input / Escape / Back precedence

| Context | Eternum | HIF current | Target HIF |
|---|---|---|---|
| Ordinary dialogue | Esc/right-click Game Menu | Esc no-op | Esc opens Game Menu; right-click only after QA |
| Quick Menu | action overlay | direct panels/Main Menu | `Menu` opens Game Menu; other fast actions remain |
| Game Menu | Esc/Return closes/back | absent | Esc closes top Game Menu layer |
| Preferences | Esc/right-click Return | Esc closes VN panel only | Esc returns to invoking context |
| Save/Load | Return/back within menu stack | own Escape; confirmation first | keep confirmation-first, then panel, then Game Menu |
| History | Esc Return | Esc closes | keep; Return destination preserved |
| Confirm modal | Esc/right-click No | Esc usually cancels | standardize Cancel/No |
| Character Hub | modal Return | Esc closes | closes before Game Menu can open |
| Chat | authored owner | BlockingExclusive denies Escape | unchanged; no Game Menu |
| Media Viewer | nested modal | Esc closes viewer first | unchanged |
| Special Mode | screen-specific | coordinator may accept/deny cancel | coordinator wins; Game Menu never bypasses it |
| H clean view | engine hide/restore | H/Esc restore | restore first; do not open Game Menu on same press |
| Replay | End Replay route | Quick Menu End Replay | Game Menu shows End Replay; Save/Load remain denied |

Canonical target priority for Esc:

```text
H hidden → restore
Character Hub / media child → close child
active Special Mode → delegate or consume denial
confirmation → cancel
Save/Load nested confirmation → cancel
open panel (Preferences/History/Save/Load) → close one level
Game Menu → close
stable ordinary dialogue → open Game Menu
```

## 11. Responsive layout contract

All shell screens must pass at:

- 1280×720
- 1920×1080
- 2560×1440
- 3840×2160

Global rules:

- Canvas Scaler/anchors and safe areas, no single-resolution absolute layout;
- minimum readable text size and minimum button hit target defined once;
- no overlap with Quick Menu reserve;
- vertical scrolling appears before content clipping;
- keyboard/controller focus remains visible after scroll;
- no text truncation for Russian labels at 1280×720.

| Screen | Layout contract |
|---|---|
| Main Menu | full-screen; primary actions remain in safe region; Continue disabled state visible |
| Game Menu | full-screen; navigation column + content region on wide screens; stack/collapse safely at 720p |
| Preferences | one semantic scroll page; two columns wide, one column at 720p; sticky Back/Reset region |
| Save/Load | existing card grid may scale/collapse per proven prefab; tabs/title/back never overlap |
| History | one scroll region; speaker/text wrapping; close control outside content viewport |
| Confirm | centered modal within safe area; underlying screen dimmed and non-interactive |

## 12. Incremental migration architecture

### Phase 1 — Shared Preferences Foundation

- **Status:** **DONE** at `f7c07b25fbf68c279d1406aaf8d0eabaabc4c672`.
- `GameSettings` remains the DTO and `SettingsManager` remains the only persistence/runtime owner; no second settings storage was added.
- Shared typed `IPreferencesService` / `PreferencesService` and `PreferencesController` now drive both entry points over current `SettingsManager` state.
- `VNSettingsService` was removed. `VNSettingsPresenter` was retired as an independent subset; its stable source file now contains only the thin gameplay `VNPreferencesAdapter`.
- `SettingsPanelController` is the Main Menu view/tab/legacy wiring adapter and delegates approved working settings behavior to the shared controller.
- `screenMode` is canonical. Legacy `fullscreen` field/key/API remain only as a compatibility layer derived from `screenMode`.
- Fake/unused fields and partial audio fields were not promoted into the approved player-facing contract. `SaveData.CurrentVersion` remains 3.
- `MainMenu.unity` and `VNPrototype.unity` were unchanged. Focused tests, full CI, ProjectValidator and scene validation passed with `missingScripts=0` and `invalidEvents=0`.
- Manual cross-context QA passed: Music Volume changed from either entry point was immediately visible from the other.
- **Intentional limitation:** Phase 1 unified ownership and behavior only. Main Menu and gameplay still use different legacy visual/control surfaces; for example, Master Volume remains visible only in the current Main Menu surface. Identical controls/layout belong to Phase 2.

### Phase 2 — Preferences UI Parity

- **Goal:** truthful one-page UI; Mute All, dialogue accessibility subset, labels/units; fake controls absent.
- **Likely files:** shared Preferences prefab/screen, controller, dialogue UI consumers, tests.
- **Risk:** HIGH (responsive text geometry).
- **Model/session:** Codex App, GPT-5.6 Sol, High, new session.
- **Manual QA:** mandatory four resolutions.
- **Dependency:** Phase 1.

### Phase 3 — Game Menu / navigation

- **Goal:** Esc/Menu navigation layer and shared back stack without bypassing ownership.
- **Likely files:** new focused Game Menu controller/prefab, `VNDialogueController`, `VNInputMap`, tests.
- **Risk:** HIGH (input precedence).
- **Model/session:** Codex App, GPT-5.6 Sol, High, new session.
- **Manual QA:** modal/special/replay matrix mandatory.
- **Dependency:** Phase 1; preferably Phase 2 stable.

### Phase 4 — Main Menu and Quick Menu cleanup + B03

- **Goal:** redirect Main Menu to shared Preferences; reduce Quick Menu; implement persistent visibility and safe-area reserve.
- **Likely files:** `MainMenuController`, shared view wiring, `VNQuickMenu`, dialogue layout consumer, tests; scene/prefab edits only if approved.
- **Risk:** HIGH because current scene wiring is serialized.
- **Model/session:** Codex App, GPT-5.6 Sol, High, new session.
- **Manual QA:** both menu contexts + H/Special/Replay + four resolutions.
- **Dependencies:** Phases 1 and 3.

### Phase 5 — Save/Load shell integration

- **Goal:** consistent Return/back/modal destinations under Game Menu without backend rewrite.
- **Likely files:** `ManualSaveLoadPanel` presentation/routing adapter and tests.
- **Risk:** MEDIUM/HIGH.
- **Model/session:** Codex App, GPT-5.6 Sol, High, new session.
- **Manual QA:** existing graphical E2E plus navigation stack.
- **Dependency:** Phase 3.

### Phase 6 — Visual polish and regression closure

- **Goal:** original HIF skin, consistent confirmations, focus/audio/transition polish.
- **Likely files:** UI prefabs/assets/styles and QA scripts; no backend redesign.
- **Risk:** MEDIUM.
- **Model/session:** Codex App, GPT-5.6 Terra or Sol, High, new session selected before start.
- **Manual QA:** mandatory four-resolution full matrix.
- **Dependencies:** Phases 1–5.

Each phase must leave the game shippable and truthful; hidden future controls are preferable to fake placeholders.

## 13. Required automated smoke tests

### Shared settings

1. Main Menu and gameplay adapters use the same settings instance and control definitions.
2. Load/save/reset roundtrip for every approved field.
3. Legacy `fullscreen` migration cannot override `screenMode` after migration.
4. Hidden/removed fields are not player-facing.
5. Reset updates both open contexts without stale UI.

### Runtime effects

6. Master/Music/SFX volumes apply immediately.
7. Screen mode/resolution/run-in-background apply correctly.
8. Text speed/Auto delay/Skip unseen/Skip after choices/Skip speed/Autosave consumers remain correct.
9. Mute All preserves/restores expected volume semantics.
10. Accessibility fields change actual dialogue rendering and survive reload.

### Navigation

11. Stable dialogue Esc opens Game Menu; second Esc closes it.
12. Open child panel Esc closes one layer at a time.
13. Confirmation Esc chooses Cancel.
14. H restore, Character Hub, Chat/media and BlockingExclusive win over Game Menu entry.
15. Replay hides/denies Save/Load and End Replay restores isolated state.

### B03

16. Default ON, persistence, Reset ON.
17. OFF hides root but hotkeys/Game Menu work.
18. H/Special blockers do not mutate preference.
19. Quick Menu reserve appears/disappears without textbox overlap.
20. Replay filtering and Character Hub ownership do not regress.

### Existing regression suites

21. Save backend and both graphical Save E2E suites.
22. Quick Menu, Auto, Skip, Hide UI, Special Mode, Replay, Character Hub, Chat and VN input/help tests.
23. Project validator, scene validation, missing scripts and invalid events.

## 14. Required manual QA

At each relevant phase:

- Main Menu → Preferences → Back;
- gameplay → Game Menu → Preferences/History/Save/Load → Back;
- Esc and optional right-click across every context in section 10;
- Auto/Skip active before opening and after closing panels;
- Quit, Main Menu, Load, Overwrite, Delete and End Replay confirms;
- no input leakage to dialogue under modal;
- keyboard/controller focus and mouse wheel scrolling;
- Russian label wrapping;
- 1280×720, 1920×1080, 2560×1440, 3840×2160;
- original HIF visuals only; no Eternum screenshots/assets in repository.

## 15. Definition of DONE

Parity shell is DONE only when:

1. exactly one player-facing Preferences screen is used from both contexts;
2. every visible setting has persistence, reset, immediate/defined apply and tested runtime consumer;
3. no fake/unused setting is visible;
4. Game Menu is distinct from Quick Menu and obeys the input precedence matrix;
5. Quick Menu matches the approved eight-action contract or documents an approved exception;
6. B03 controls root and dialogue safe-area reserve without disabling hotkeys;
7. Save/Load safety, Replay isolation, Hide UI and BlockingExclusive have no regression;
8. all required smoke/validator/scene tests pass;
9. manual four-resolution QA passes;
10. no copyrighted Eternum source/assets/text are copied.

## 16. Exact next step

### Implement Preferences UI Parity (Phase 2)

Build one identical shared player-facing Preferences screen used from both Main Menu and gameplay, following this approved parity specification and the completed Phase 1 foundation. Keep `SettingsManager` as the only truth, expose only verified controls, and preserve context-specific Back behavior. B03 remains absorbed into later unified Preferences work and was not implemented by Phase 1.
