# Player-facing UX polish pass 2 — Save/Load

**Status: REVIEW CANDIDATE**

## Implemented contract

- Manual saves use 10 pages of 6 cards: global addresses `1..60`. Page 1 remains `1..6`; existing file stems `slot_01.json` through `slot_06.json` are unchanged. No migration is performed.
- `SaveData.CurrentVersion` remains `3`.
- Auto and Quick remain independent cyclic groups of six (`auto_01..06`, `quick_01..06`). Continue considers all 60 Manual addresses plus Auto/Quick.
- The shared panel opens Save/Load on Manual page 1. Auto/Quick hide the Manual row; returning to Manual retains the current page in the open-panel session. Arrows clamp at 1/10.
- Empty cards show only `Пусто`; giant background numerals and repeated category labels are hidden. Occupied cards retain thumbnail, scene name, date/time and a small local index. Delete remains compact and confirmation-gated.
- At 1280×720 the panel now scales to remain within the viewport while retaining all six cards, tabs, Back and the pagination row.

## Final validation evidence

- `SavePaginationEditModeTests`: PASS, 2/2. Covers old Manual file stems 1..6, Manual 7/60, Manual 61 rejection, Auto/Quick 7 rejection, capacities and page mapping.
- `HowIFallCiSmokeTests.RunAll`: PASS. Save backend coverage proves Auto/Quick six-slot rotation and Continue selecting newer Manual slot 7 over older page-1/Auto/Quick candidates.
- `PlayerJourneyE2ETests.ManualSaveLoadJourney_FilledSlotRestoresStateAndGameplay`: PASS, 1/1. Writes and restores Manual page 2 / global slot 7.
- `SaveBackendV2` graphical E2E: PASS. Fresh proof includes Manual page 1, Manual page 2, Auto, Quick, and responsive Manual page 2 at 1280×720.
- `ManualSave` graphical E2E: PASS. Fresh proof includes Save, Main Menu Load, unavailable save state and a settled load confirmation with safe Cancel focus.
- Nine fresh Save/Load screenshots were inspected. The initial 1280×720 run exposed viewport clipping; the panel presentation scale was corrected, then both graphical scenarios were rerun. No remaining objective clipping, overlap, duplicate empty labels or giant background slot numbers were found.

## Curated baselines

- Updated: `save_load_manual.png`, `save_load_manual_page_2.png`.
- `save_load_confirmation.png` is retained: the confirmation composition remains representative after the pass.
- Drive mirror: not run; no authorized connector was available in this environment.
