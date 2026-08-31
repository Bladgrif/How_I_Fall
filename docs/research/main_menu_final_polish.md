# Main Menu final polish — 2026-08-31

## Status

**REVIEW CANDIDATE** — automated evidence is recorded below; this is not final
art approval.

## Pre-pass findings

The fresh 1920x1080 capture used the authored background well, but the logo and
navigation read as separate islands: the action group started too low, the old
presentation still carried text outlines, and the legacy `Press any button`
prompt could reappear after Main Menu Preferences closed.

## Benchmark comparison

Repository research plus title-screen references for **STEINS;GATE**, **The
House in Fata Morgana**, **AI: The Somnium Files**, and **PARANORMASIGHT** point
to the same reusable pattern: one dominant key visual, a clearly isolated logo,
a short navigation stack, transparent resting states, and a small unambiguous
selected state. HIF adopts that principle without copying a layout or assets.

## Chosen HIF direction and changes

- Preserved the authored full-bleed non-canon background, left readability wash,
  logo, and five-action contract.
- Moved and reduced the logo/navigation as one compact left-side composition;
  `Выйти` remains separated below the four ordinary actions.
- Removed outlines/cyan treatment. Hover and controller focus use brighter text
  plus a compact HIF-red left marker; `Выйти` remains muted at rest.
- Preserved dynamic primary semantics: `Продолжить` when a valid save exists;
  otherwise `Новая игра`.
- Extended the existing PlayerUi graphical journey with Main Menu Load, Quit,
  and 1280x720 root captures. The quit modal keeps its existing safe `Нет`
  focus. The legacy prompt is kept hidden after Preferences restores its old
  scene objects.

No art was added or changed. The authored Main Menu background remains
**temporary / non-canon**.

## Evidence

- Pre-change PlayerUi graphical E2E: PASS, 2026-08-31.
- Final PlayerUi graphical E2E: PASS, 2026-08-31; 20 screenshots, including 9
  Main Menu-related states inspected at 1920x1080 and 1280x720.
- Objective defect fixed and rechecked: legacy `Press any button` text visible
  under Load/Quit after closing Preferences.
- Curated steady-state baseline: `docs/visual-baselines/main_menu.png`.

- MainMenuVisualPassASmokeTests.RunBatchMode: PASS (Unity batch exit 0).
- PlayerJourneyE2ETests: PASS, 6/6 (XML result).
- HowIFallCiSmokeTests.RunAll: PASS, including Project validator and scene validation.
