# Save/Load UX benchmark

## References

- **The House in Fata Morgana** — conventional VN Save/Load, quick actions and
  simple menu vocabulary; useful as a restraint benchmark rather than a visual
  template. [Gameplay controls](https://strategywiki.org/wiki/The_House_in_Fata_Morgana/Gameplay)
- **STEINS;GATE / STEINS;GATE 0** — mature VN navigation and deliberate
  separation of common reading controls from save actions. [VN functionality
  benchmark](vn_functionality_benchmark.md)
- **Doki Doki Literature Club / DDLC Plus** — a compact, immediately legible
  baseline for Menu and Preferences rather than a source of HIF art or layout.
  [VN functionality benchmark](vn_functionality_benchmark.md)
- **PARANORMASIGHT** — a controller-friendly 2D narrative game with clear
  progression/navigation affordances. [Official site](https://paranormasight.square-enix-games.com/en-us/)
- **AI: THE SOMNIUM FILES** — manual saving alongside structured player
  navigation; the useful principle is visible hierarchy, not its detective UI.
  [Official system page](https://www.spike-chunsoft.com/ai/system/)

## Strong patterns

- Make the current context (Save or Load) and save class visible before slot
  detail.
- Let the scene thumbnail identify a valid save first; keep date and location
  concise and readable.
- Treat empty, valid, and unavailable data as separate states. An unavailable
  save must not borrow empty-state wording or affordance.
- Keep destructive actions visually and navigationally subordinate, with
  Cancel as the confirmation default.
- Give keyboard/gamepad selection a stable visual state independent of hover;
  use a compact, predictable grid rather than controller-only hidden paths.

## What does not fit HIF

- Copying a branded interface, proprietary art, bespoke story charts or a
  multi-page save archive would inflate the functional-demo scope.
- Player naming, suspend slots, cloud sync and backend changes are not needed
  for the current v3 contract.

## Comparison with current HIF

HIF already has a shared six-slot 3x2 prefab, 16:9 previews, Manual/Auto/Quick
classes, safe confirmations and Main Menu/Gameplay entry points. The remaining
polish gap was focus visibility: card emphasis was pointer-only, and dynamic
Load states did not define explicit safe navigation links. Raw slot errors could
also surface technical data through the status line.

## Recommended direction for HIF

Keep the existing neutral-dark, cyan-accent 3x2 thumbnail-led grid. Strengthen
information hierarchy and interaction rather than redesigning the save system:
selected cards use a modest cyan focus treatment, valid previews remain
prominent, unavailable occupied cards use concise player-safe wording, and
runtime navigation links avoid disabled Load cards and destructive defaults.

## Adopted in this pass

- Added EventSystem focus treatment to the existing slot view.
- Added dynamic explicit navigation for tabs, available cards, Close and
  secondary Delete controls.
- Mapped unavailable-slot status to concise player-facing messages.

## Intentionally not adopted

No SaveData change, new slot types, pages, renaming, suspend/cloud saves,
backend rewrite, scene rewrite or global focus manager.
