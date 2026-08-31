# Player-facing UX polish pass 3 — whole journey

**Status: ACCEPTED.**

Closure: accepted at commit `79e9f3e412cb243fd9a98aabf451ecf7194f1616`; mandatory CI run `33203001166` is GREEN.

## Audit result

| Severity | Finding | Result |
| --- | --- | --- |
| P1 | An already-open Save/Load panel kept its 1920×1080 presentation scale after switching to 1280×720; `SaveBackendV2` reported clipping. | Fixed: the panel reapplies its existing viewport-fit scale while open. |
| P2 | Game Menu's EventSystem selection was not visually distinguishable in the new root/alternate-focus proof. | Fixed: a narrow HIF-red right-edge focus marker now follows selected Game Menu actions. |
| P3 | None demonstrated after the corrections. | No change. |

## Intentionally unchanged

- Quick Menu order and actions: История, Пропуск, Авто, Сохранить, Быстр. сохр., Быстр. загр., Настройки, Меню.
- Game Menu actions, Esc/back ownership, confirmation defaults, Character Hub deferral, SaveData v3, save groups and pagination.
- Main Menu, Preferences, scenes, prefabs, art and project settings.

## Verification

- `PlayerJourneyE2ETests`: **PASS, 6/6** after the Game Menu correction.
- `HowIFallCiSmokeTests.RunAll`: **PASS** after the correction; includes `Preferences UI parity Phase 2`, `VN quick menu`, `Game Menu navigation Phase 3`, and `Save backend v3`.
- Graphical E2E at 1920×1080:
  - `PlayerUi`: **PASS**, **17** screenshots. New proof: `QAArtifacts/GraphicalE2E/PlayerUi/game_menu_root_1920x1080.png` and `QAArtifacts/GraphicalE2E/PlayerUi/game_menu_alternate_focus_1920x1080.png`.
  - `ManualSave`: **PASS**, 4 screenshots.
  - `SaveBackendV2`: initial **FAIL** exposed the 1280×720 P1; post-fix rerun **PASS**, 5 screenshots including Manual page 2 at 1280×720.
- **26** fresh screenshots were opened and inspected across Pass 3. No remaining clipping, overlap, missing texture, wrong visibility, malformed control, or visually ambiguous Game Menu focus was found in the passing proof.
- Game Menu root: dialogue shell and Quick Menu are suppressed; no child confirmation, legacy top-right Menu, Character Hub, History, or empty placeholder is visible. Return has deterministic red focus marker and stays visually subordinate.
- Alternate focus: `Настройки` was selected without activation; only that action carries the red marker, with no layout shift, cyan fill/glow, or text clipping.
- Responsive result: 1920×1080 PASS; 1280×720 Preferences PASS; 1280×720 Save/Load PASS after the correction.

## Pass 1 / Pass 2 guards

- Pass 1: preserved by PlayerUi and PlayerJourney — no legacy top-right gameplay Menu, compact Preferences and direct selectors remain present.
- Pass 2: preserved by SaveBackendV2 and smoke — Manual pages 1..10, 60 Manual addresses, six Auto/Quick slots, `Пусто` empty cards, no giant slot numbers, and page 2 all pass.

## Scope

- Production: `Assets/HowIFall/Scripts/UI/ManualSaveLoadPanel.cs`, `Assets/HowIFall/Scripts/UI/VNGameMenuView.cs`.
- QA proof: `Assets/HowIFall/Editor/PlayerUiGraphicalE2ERunner.cs`, `tools/run-graphical-e2e.ps1`.
- No baselines changed: Game Menu baseline policy does not require a new curated image for this narrow proof correction; `QAArtifacts` remain uncommitted.
- Commit/push were not run. Unrelated local changes were preserved.
