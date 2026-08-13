# How I Fall — Eternum Runtime UI Reference

## Status / usage rule

This is the permanent text-only reference for player-facing UI/UX behavior **OBSERVED IN RUNNING ETERNUM 0.9.5** during the 2026-08-12 read-only runtime audit.

- Runtime observations take priority over source-based assumptions for player-facing behavior.
- Future implementation sessions must read this document before reopening Eternum.
- Re-audit Eternum only when required behavior is absent here, marked uncertain, needs a precise visual detail, or the user explicitly asks for a fresh comparison.
- Keep **OBSERVED IN RUNNING ETERNUM** separate from **SOURCE-BASED / UNVERIFIED**.
- HIF may intentionally improve on Eternum where the difference is documented.
- Do not copy Eternum assets, screenshots, source code, fonts, music, UI graphics, exact geometry or copyrighted text into HIF.
- HIF Phase 4 final behavior is recorded at feature commit `91f1f8f5bd1b70e7c29663b35b6f2d44c9733b9d`.

## Main Menu

### OBSERVED IN RUNNING ETERNUM

- Dedicated full-screen composition with a large title and a strongly visible, softly blurred/background-treated artwork.
- Primary navigation is a centered horizontal row of four large illustrated cards: Start, Load, Preferences and About.
- The cards are the dominant interactive elements rather than a conventional vertical text list or heavy enclosing panel.
- Cards have generous spacing; hover changes the card treatment with a bright cyan/teal emphasis.
- Quit is a separate compact icon control below the primary row rather than another equal-size card.
- Small secondary/external icon controls are visually subordinate to the primary navigation.
- Most of the authored background remains visible around and behind navigation.

### SOURCE-BASED / UNVERIFIED

- Exact pixel sizes, font metrics, animation timing and keyboard/controller focus order were not recorded and must not be inferred.

### HIF CURRENT / INTENTIONAL — PHASE 4 DONE

- HIF keeps its existing authored Main Menu background, button objects and UnityEvent wiring rather than cloning Eternum's card composition.
- Final order is Continue, New Game, Load, Preferences, Help, About, Quit.
- Continue is disabled when no compatible valid save exists; Gallery is not inserted as a top-level action.
- Preferences opens the same shared `SharedPreferencesView` used in gameplay.

## Preferences

### OBSERVED IN RUNNING ETERNUM

- Main Menu and in-game routes use the same Preferences screen.
- It is a dedicated full-screen Preferences surface; Game Menu navigation is not visible underneath it.
- The inspected desktop layout is dense and organized as two tall, independently scrollable columns/panels.
- The left side contains the larger slider-oriented group: text speed, auto-forward timing, music/SFX levels, mute and dialogue/textbox presentation controls such as text size, outline, opacity, width and height.
- The right side contains button/toggle-oriented options including display/mode choices, language, rollback/skip behavior, Quick Menu visibility and motion-related options.
- Section labels establish hierarchy while individual rows remain compact; scrollbars make the long control set explicit.
- Preferences Back, when entered from the observed in-game Game Menu, returned directly to gameplay.

### SOURCE-BASED / UNVERIFIED

- Exact numeric ranges, all option values, exact Reset placement and behavior at non-desktop aspect ratios were not recorded in the runtime audit.

### HIF INTENTIONAL IMPROVEMENT

- HIF uses the same shared Preferences screen from Main Menu and gameplay.
- When opened from the HIF Game Menu, Back returns to the Game Menu instead of directly to gameplay.
- B03 Show Quick Menu is DONE: it defaults ON, persists immediately through the shared Settings authority and Reset returns it to ON. Temporary H, BlockingExclusive, Preferences and Game Menu blockers do not overwrite the stored preference.

## Quick Menu

### OBSERVED IN RUNNING ETERNUM

Core left-to-right order in the inspected desktop build:

1. Back / Rollback
2. History
3. Skip
4. Auto
5. Save
6. Quick Save
7. Quick Load
8. Preferences

- It is a low-profile text navigation strip near the bottom of ordinary dialogue.
- It is visually subordinate to the dialogue and does not resemble a large button bar.
- History belongs here rather than in the normal Game Menu.
- Auto and Skip have visible active-state presentation.
- The localized build may also show a final external/promotional link; it is not part of the HIF parity target.

### HIF CURRENT / INTENTIONAL — PHASE 4 DONE

Final left-to-right order:

1. History
2. Skip
3. Auto
4. Save
5. Q.Save
6. Q.Load
7. Preferences
8. Menu

- `Menu` opens the Phase 3 Game Menu. Manual Load, direct Main Menu and Characters are absent from the Quick Menu strip.
- Character Hub remains reachable from a narrow dedicated HIF launcher outside the strip, with Replay, Hide UI, modal and special-mode restrictions preserved.
- Effective Quick Menu visibility composes B03 with H, BlockingExclusive, Preferences and Game Menu blockers. Hotkeys remain available according to their existing action gates rather than being disabled by the visibility preference.
- A measured Quick Menu `RectTransform` reserve moves only the dialogue shell and collapses to zero whenever the Quick Menu is effectively hidden.

## Esc / RMB behavior

### OBSERVED IN RUNNING ETERNUM

- Esc does **not** open the Game Menu.
- During ordinary dialogue, Esc hides/restores the dialogue presentation.
- Quick Menu and the heart icon remained visible during the observed Esc hide state.
- RMB opens and closes the Game Menu.
- Esc did nothing inside the observed Game Menu.
- Esc did nothing inside the observed Character Hub.

### HIF INTENTIONAL DIFFERENCE

- HIF uses `H` for clean view.
- HIF uses Esc for Game Menu / Back.
- HIF retains stronger modal precedence.

This difference is intentional and must not be ?fixed? to Eternum behavior without explicit user approval.

## Game Menu

### OBSERVED IN RUNNING ETERNUM

- Dedicated full-screen screen, not a small gameplay overlay; the gameplay presentation is visually replaced.
- Compact left vertical navigation occupies roughly one quarter of the width.
- Observed normal order: Save, Load, Preferences, Main Menu, Quit, external ?More Games?, Back.
- HIF excludes the external/promotional entry.
- Back is visually separated near the bottom.
- Selected navigation is clearly emphasized; hover uses a distinct blue/cyan response.
- There is no generic ?Game Menu? content placeholder.
- History is absent.
- Characters are absent.
- Opening the Game Menu initially showed Save.
- The large right region contains real Save/Load content rather than decoration or an empty frame.

### HIF CURRENT / INTENTIONAL

- HIF Phase 3 provides a clean navigation-focused full-screen shell with no empty placeholder region.
- Phase 3 intentionally does not embed Save/Load content in that shell. Later Save/Load shell integration owns that work.

## Save / Load shell

### OBSERVED IN RUNNING ETERNUM

- Save and Load share the Game Menu shell; the left navigation remains visible.
- The right side contains save-slot content.
- The observed grid is `3 ? 2`.
- Page navigation is present.
- Switching Save ? Load changes active content/title without replacing the surrounding shell.
- RMB exits the Game Menu back to gameplay.
- Esc did not close this screen in the observed build.

### HIF CURRENT / INTENTIONAL

- The existing `ManualSaveLoadPanel` remains separate for safety.
- Phase 5 owns deeper shell integration; do not rewrite the Save backend for visual parity.

## Character Hub

### OBSERVED IN RUNNING ETERNUM

- A persistent heart button exists at the top-left during ordinary gameplay.
- It opens a dedicated full-screen Character Hub.
- A horizontal character portrait strip sits along the bottom; the selected portrait is visibly highlighted.
- The selected character receives a large central/full-body presentation over a blurred/background-treated scene.
- Name, relationship hearts and additional character facts are shown.
- Currency, Gallery and Back controls exist in the reference.
- Back returns to gameplay.
- Esc did not close the observed screen.

### HIF CURRENT / INTENTIONAL

- The current HIF Character Hub is only a technical foundation.
- Canon characters, art, biographies, unlock rules and final visual design remain deferred.
- Do not implement currency or personal-stat fields only because Eternum contains them.
- Future visual work may use the large portrait plus bottom portrait-strip structure as reference.

## Gallery

### OBSERVED IN RUNNING ETERNUM

- Gallery can be entered from the selected character context.
- The current character acts as a filter/context.
- Scenes appear as a preview grid.
- Back returns to the selected character context, preserving that context.

Costume-change behavior was not verified and must not be documented as fact.

## HIF intentional improvements

- Esc Game Menu / Back.
- `H` clean view.
- Safer modal precedence.
- Preferences Back to Game Menu.
- History/child return context where supported.
- Stronger Save/Load and replay guards.
- Continue loads the latest valid save.
- No promotional/external ?More Games? entry.
- No currency system without a real gameplay need.

## Future reference rule

Before future work on Main Menu, Preferences, Quick Menu, Game Menu, Save/Load navigation, Character Hub or Gallery, read this runtime reference first.

Only reopen Eternum if:

1. requested behavior is missing from this document;
2. an existing observation is marked uncertain;
3. an exact visual composition not captured here becomes necessary;
4. the user explicitly requests a fresh runtime comparison.
