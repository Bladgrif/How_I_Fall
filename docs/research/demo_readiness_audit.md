# Demo Readiness Audit

## Current update — 2026-08-31

- **Baseline:** `6e9e6eaa6380c924a9bd0b9ba05c361903191e30`.
- **Main Menu final polish:** **ACCEPTED**.
- **Mandatory CI:** Unity CI #90 / run `33386581463` — Unity Test Framework **SUCCESS**; Unity smoke tests **SUCCESS**.
- **Final player-facing audit:** all audited items PASS; PlayerJourney **6/6 PASS**; `HowIFallCiSmokeTests.RunAll` PASS; PlayerUi graphical E2E PASS; no objective defects found; 1920×1080 PASS; 1280×720 PASS.
- **Standalone:** Windows x64 build from the previous accepted production baseline succeeded; executable launch was blocked by Codex environment policy. A new final Windows build from current HEAD remains the last pre-review validation step. Standalone startup is **not** marked PASS.

## Baseline

- **Commit:** `62647fb8eb906a38cd9be4b7904e8e2435480f57` (`HEAD` = `origin/master`, verified after `git fetch origin`).
- **Date:** 2026-08-28.
- **Phase:** Polished Functional Demo First.
- **Committed CI evidence:** requested baseline GitHub CI #83 is recorded as GREEN for `Unity Test Framework` and `Unity smoke tests`. Local audit did not query GitHub again.

## Executive verdict

**READY**

The primary flow is automated, save data contracts remain intact, and the corrected steady-state proof shows a readable confirmation modal. The previous P1 was invalid graphical proof captured during the 0.12-second transition.

## P0 blockers

None found.

## P1 blockers

None. The earlier P1 was a proof-capture timing artifact, not a demonstrated production defect.

## P2 polish

None recorded. The fresh PlayerUi and save/load surfaces showed no objective clipping, overlap, missing texture, malformed dropdown, or wrong visibility defect at 1920x1080.

## P3 minor

None recorded; this audit does not turn subjective visual preferences into backlog items.

## Correctly deferred

- Real story, routes, canonical flags, characters, and authored narrative content.
- Final backgrounds, portraits, art direction, and visual identity.
- Glossary/lore, flowchart, chapter select, endings, canonical replay content, narrative-driven choice-autosave policy.
- Suspend/resume, rollback, and a generic minigame framework.
- Ordinary player-facing Character Hub launcher: intentionally hidden; the technical foundation remains available for technical coverage.

## Player journey result

`PlayerJourneyE2ETests`: **PASS, 6/6**. It covers Main Menu preferences/quit/load return, New Game, modal back-stack restoration, manual save/load, quick save/load, and Continue fallback from a newer invalid candidate. The mandatory primary functional journey is therefore supported by fresh targeted runtime evidence.

## Save/Load result

Read-only contract audit and fresh graphical scenarios support: `SaveData.CurrentVersion = 3`; Manual, Auto, and Quick slot families; 384x216 previews; invalid/corrupt candidate handling; Continue selecting the newest valid candidate; pre-load autosave route; and replay save/load denial. The fresh ManualSave and SaveBackendV2 scenarios passed. The corrected steady-state confirmation proof is readable; no production Save/Load defect remains demonstrated.

## Reading result

Fresh PlayerUi proof covers standard and 125% dialogue, choice focus/wrap, backlog, Auto, Skip, Hide UI, and gameplay Preferences. No objective reading-layout defect was observed. Character Hub launcher is hidden as required.

## Input/controller result

Fresh Player Journey PASS and the committed CI baseline support main player flow, deterministic focus, cancel/back, and modal restoration. No new controller/input regression was found. The full local suite caveat below is not classified as a production input defect.

## Technical foundations result

The CI smoke runner passed and explicitly includes Gallery/Replay, Timed Narrative Beat, Interactive Hotspot, Map Locations, Character Hub, and Chat Phone checks. Source/test inspection confirms their intended technical-demo contracts: entry, blocking where required, return/cleanup, and save/replay guards. No new concrete integration defect was found in this audit.

## Graphical proof result

Fresh graphical E2E at 1920x1080:

| Scenario | Result | Screenshots inspected |
| --- | --- | ---: |
| `PlayerUi` | PASS; twelve required states captured | 12 |
| `ManualSave` | PASS; save, load confirmation, invalid slot, Main Menu Load | 4 |
| `SaveBackendV2` | PASS; Manual, Auto, Quick surfaces | 3 |

All **19** fresh screenshots were visually inspected. Objective defect found: none in the corrected steady-state frame. The original screenshot was captured while `AnimateConfirmation` was still transitioning (0.12s). `ManualSavePlayModeE2ERunner` now polls a bounded readiness stage until the confirmation is fully opaque/interactable, at scale 1, with parent content disabled and Cancel still selected, before queuing the capture. The first post-edit harness attempt hit the bounded readiness timeout; the immediate rerun completed after Unity reloaded the edited assembly and passed. The relevant confirmation baseline was replaced with the steady-state frame; unrelated baselines were not changed.

## Local-worktree caveats

The worktree was already dirty before the audit and was preserved. It includes deletions/modifications in art, dialogue data, scenes, project settings, screenshots, an untracked `MainMenuFocusPlayModeTests.cs`, and other user work.

One full local PlayMode attempt produced **10/11 PASS, 1 FAIL**. The sole failure is the untracked `MainMenuFocusPlayModeTests.MainMenuFocusAndCloseOrCancel_UsesSafeModalAndRootContracts` at its first label assertion: it assumes every action button exposes a `TextMeshProUGUI` child, but the first discovered action label is empty. The test fails before exercising focus/cancel behavior. This is inconclusive local test work, not evidence of a committed production defect; the file was not changed or removed. Clean GitHub CI #83 remains the committed-baseline suite evidence.

The fresh visual evidence is also from this dirty checkout, so changed local art/settings can affect presentation. The committed confirmation baseline was itself stale transition-state proof and is now replaced by the corrected frame.

## Drive handoff result

**PASS.** The existing `How I Fall / Visual Review / Current Screens / Save Load` file `gameplay_load_confirmation_1920x1080.png` was replaced in place with the fresh steady-state proof; no duplicate was created.

## Recommended next action

No production correction batch is recommended. Keep the graphical harness readiness gate in place and use the corrected confirmation proof for review; no Save/Load redesign or save-format change is needed.
