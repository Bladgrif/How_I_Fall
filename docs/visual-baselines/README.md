# Player UI visual baselines

These images are the current accepted/review baselines for key player-facing UI states.

- `QAArtifacts/` remains temporary graphical E2E proof, is gitignored, and is not committed.
- Baselines are a small curated review set of key states, not a copy of all `QAArtifacts`.
- Full baselines are the primary runtime review evidence and remain in the repository source of truth.
- `review/` may contain lightweight copies for tooling/reviewer access; they are not graphical regression proof.
- Google Drive `How I Fall/Visual Review/` is the preferred visual-review mirror when the reviewer needs direct image access.
- `Current Screens/` contains fresh review screenshots; `References/` contains external visual references; `Archive/` is optional history.
- Drive copies do not replace repository baselines, graphical E2E, tests, or CI.
- Keep review previews only for curated baselines, not for every QA screenshot.
- Update relevant baselines after a significant visual pass and successful graphical E2E.
- A UI + baseline push may be a `REVIEW CANDIDATE`, not final visual acceptance.
- Reviewer should open the real images and compare them with the previous state; for a major redesign, external game/VN references may also be useful.
- Baselines are not final art, pixel-perfect golden-image tests, or automatic aesthetic approval.

## Main Menu and Preferences

- `main_menu.png` — default Main Menu focus marker alignment.
- `main_menu_quit_confirmation.png` — Quit confirmation with explicit destructive-action hover and a single active state.
- `preferences.png` — real Screen Mode / Resolution TMP dropdowns and maximum Text Speed label without slider overlap.

## Save/Load

- `save_load_save.png` — gameplay Save / Manual after a successful write.
- `save_load_manual.png` — gameplay Load / Manual with valid and empty slots.
- `save_load_confirmation.png` — gameplay Load confirmation: Cancel safe default and blocked parent content.
- `save_load_slot_types.png` — gameplay Load with a controlled invalid occupied slot, distinct from an empty slot. Auto and Quick are covered by fresh graphical E2E proof.

## Core reading experience

- `reading_standard.png` — ordinary reading surface with only History / Skip / Auto / Quick Save and no temporary title chrome.
- `reading_dialogue_125.png` — long dialogue at the supported 125% text scale, without clipping or Quick Menu overlap.
- `reading_choice_focus.png` — wrapped choice labels and deterministic first keyboard/controller focus.
- `reading_choice_hover.png` — mouse hover transfers choice focus without a second selected state.
- `reading_backlog.png` — scrolled history with speaker/narration separation.
- `reading_auto_active.png` — distinct Auto active state in the compact Quick Menu.
- `reading_skip_active.png` — distinct Skip active state in the compact Quick Menu.
- `reading_quick_save_feedback.png` — brief Quick Save feedback over the ordinary reading surface.