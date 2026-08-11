# Phone UI Polish — visual and UX specification

## Scope and boundary

This document defines the next presentation pass for the existing Chat / Phone technical foundation. It is a **technical / demo UI** specification only.

- It does not add or rewrite story, characters, contacts, dialogue, images, relationships or choices.
- Existing TEST assets remain **TECH DEMO ONLY / NOT CANON**.
- The typed `ChatSceneData` contract, closed Text/Image/Choice entry types, typed conditions/effects, transient transcript, `BlockingExclusive`, Replay denial, SaveData v3 and exactly-once return contract remain authoritative.
- No new phone menu, contact list, save data, story command format or global manager is part of this pass.

## Visual goal

Use the approved hybrid direction:

1. **Clean structure and readable reply cards:** restrained glass surfaces, clear spacing, high legibility and an unambiguous action area.
2. **Phone / messenger presence:** the chat is visibly a smartphone-style overlay, not a generic VN modal.

The result should feel like a focused messenger opened over the VN scene, while remaining a deliberately neutral technical demo rather than final art direction.

## Phone overlay layout

- Center a portrait phone panel inside the existing Canvas, with safe margins at all supported desktop resolutions.
- Use one rounded outer shell with a subtle glass/translucent treatment, restrained border and shadow.
- Reserve a compact top header, a flexible scrollable transcript, and a persistent bottom reply area.
- The transcript must not be obscured by the reply cards; the reply area must not cover the last visible message.
- Phone proportions may scale responsively, but text, bubbles and reply targets must remain readable and clickable without clipping.
- The overlay is the only active foreground interaction surface during Chat mode.

## Background and ordinary VN UI

- Keep the authored VN background visible behind the phone, dimmed by a neutral translucent scrim. The current technical demo may therefore show the classroom background, but this pass must not author or modify it.
- Hide the ordinary dialogue box and its choice presentation while Chat mode is active.
- Do not expose the Quick Menu, Backlog, Settings, Save/Load or other ordinary VN controls through or above the phone overlay.
- On close, remove the scrim and restore normal VN UI ownership only after the existing Chat completion/return flow has resolved.

## Header

- Place the contact display name centrally or prominently in the phone header; the technical demo displays `TEST CONTACT`.
- Keep header height compact and visually separate from the transcript.
- A decorative status/device treatment is optional, but must not suggest unimplemented phone functions.
- Do not add canon portraits, contact metadata, notifications or navigation controls in this pass.

## Transcript and message presentation

- Use a scrollable transcript with consistent vertical rhythm and automatic reveal of the newest entry.
- Incoming messages are left-aligned bubbles; player/outgoing messages are right-aligned bubbles.
- Differentiate sender direction through alignment, surface treatment and restrained colour contrast, not through canon art or lore.
- Bubble width must preserve comfortable line length and allow wrapping without overlap or clipping.
- Incoming text, image entries and selected outgoing replies are all visible in the same transient local transcript.
- The transcript remains in memory only for the active chat and must not enter DialogueBacklog, DialogueReadHistory, SaveData or Replay history.

## Image card

- Render an Image entry as a neutral in-transcript media card with rounded corners, internal padding and a bounded aspect-preserving image area.
- The V1 technical placeholder must stay visibly neutral and technical; do not introduce narrative, character or illustrated content.
- A null/missing image remains a validation/runtime failure case, not a blank decorative card.
- Fullscreen media viewing, zoom, gallery integration and image actions are out of scope.

## Reply cards

- Render the exactly two available Choice options as large, clearly separated reply cards in the bottom action area.
- Each card must make the full authored reply text readable and provide a reliable click/tap target.
- Hidden/unavailable options must not leave misleading empty controls; existing typed condition and fallback behaviour remains unchanged.
- After selection, disable both cards immediately so the selected reply/effect cannot be applied twice.
- The selected reply appears as one outgoing bubble before the existing branch or terminal completion presentation continues.

## Input and modal behaviour

During an active Chat `BlockingExclusive` lease:

- only the currently available reply cards accept Chat input;
- ordinary dialogue advance, Auto, Skip, Save, Quick Save, Auto Save, pre-load autosave, Load, Quick Load, Backlog, Settings, Quick Menu and Main Menu remain blocked;
- Escape is a no-op and must not close or cancel the chat;
- background clicks must not advance the underlying VN dialogue;
- repeated reply callbacks during terminal resolution are no-ops.

This polish pass must preserve the existing generic SaveManager backend guard; UI-only blocking is insufficient.

## Expected open and close behaviour

### Open

1. The existing `ChatController` successfully acquires its `BlockingExclusive` lease.
2. Ordinary dialogue UI is hidden, the neutral background scrim and phone overlay appear as one focused transition.
3. Header, transcript and available reply cards become visible; the first current entry is readable before any input is expected.

### Choice and branch

1. Selecting one reply validates its target and typed effects, applies valid effects once and appends one outgoing bubble.
2. A non-terminal choice reveals its next entry without an additional generic advance input.
3. A terminal choice keeps the outgoing bubble visible for the bounded existing presentation step, then completes automatically.

### Close

1. The existing completion flow marks the chat resolved, stops Chat input, releases the valid lease, clears transient chat state, then requests the authored return scene exactly once.
2. The phone overlay and scrim disappear without a second player input.
3. Normal VN UI resumes only through the existing return-scene route. Failure and destruction cleanup must release the lease but must not cause an accidental success route.

## Known foundation limitation

The technical foundation is functionally complete. The terminal reply transition can currently **feel** like it requires a second click, although the return route completes automatically and exactly once after the first reply input. This does not block the technical foundation. Phone UI polish may refine the presentation flow, timing and visual feedback, but must not weaken exactly-once completion or require a second input.

## Acceptance criteria for implementation

- The active chat reads immediately as a phone/messenger overlay, with clear glass UI and a dimmed VN background.
- `TEST CONTACT`, incoming text, the neutral image card, both reply cards and the chosen outgoing bubble are readable at 1920x1080.
- No clipping, overlap or inaccessible reply target occurs at the resolutions selected for the implementation QA pass.
- Ordinary dialogue box and ordinary VN controls are not visible or actionable while chat is active.
- Reply selection has one semantic action: it selects the reply and never advances the underlying VN dialogue.
- A terminal reply completes and routes automatically exactly once; no second mouse, keyboard or background input is required.
- A non-terminal reply branches directly to its target entry.
- The transcript stays transient and existing Replay, SaveData v3, SaveManager and special-mode contracts remain intact.
- No new canon story, character, art, asset or normal campaign route is introduced.

## Explicitly out of scope

- Canon contacts, character portraits, messages, images, story branches or relationship writing.
- Phone home screen, contact list, notifications, typing indicators, sound design, attachments, media viewer/zoom, calls or messaging outside an active chat scene.
- New persistent chat state, save schema changes, Replay history, global Chat/Phone managers or Addressables.
- Changes to `VNPrototype.unity`, scene/prefab installation, normal campaign routing, runtime mechanics, tests or special-mode policy as part of this documentation task.
