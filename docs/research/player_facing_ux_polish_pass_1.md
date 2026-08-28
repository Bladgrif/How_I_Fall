# Player-facing UX polish pass 1

**Status:** REVIEW CANDIDATE — automated verification pending.

The functional baseline was stable, but human runtime review reopened player-facing polish for Main Menu, shared Preferences and ordinary gameplay navigation. This is not a new feature or story pass.

## Decisions

- The floating cyan Main Menu underline is rejected. Focus/hover uses brighter text, a restrained outline and a small adjacent HIF red accent instead.
- Preferences remains the single `SharedPreferencesView` used from Main Menu and gameplay. It is a compact centered modal, not a second full-screen shell.
- Settings remain immediate-apply and immediately persisted; the footer now says this explicitly. No staged copy or Apply action is introduced.
- Screen Mode and Resolution use compact direct selectors instead of oversized dropdown presentation.
- The legacy top-right gameplay Menu control is hidden at runtime. The bottom Quick Menu route and Esc-to-Game-Menu contract remain authoritative.
- Save backend, manual pagination and SaveData are intentionally deferred to Pass 2.
