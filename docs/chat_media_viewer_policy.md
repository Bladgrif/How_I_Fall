# Embedded Chat Media Viewer (R06) Policy

**Status:** POLICY DECIDED / IMPLEMENTATION TODO
**Scope:** docs-only contract for the existing Chat/Phone technical demo.
**Baseline:** `origin/master` at `39872ab947884ec84f56d8330741e68a47ca699c` (`docs: finalize chat notification sounds`).

## Purpose and boundary

R06 adds one narrow player-facing interaction:

`revealed Chat Image card -> fullscreen/local media viewer -> close -> exact same active Chat state`.

The current in-transcript Image card and approved Phone UI remain unchanged. This is **TECH DEMO ONLY / NOT CANON** and uses the existing neutral technical placeholder image. No runtime, scene, asset, or test code is changed by this policy task.

## Ownership

The viewer is a local sub-modal of the current `ChatController`. It is not a new global or special mode.

- `ChatController` keeps owning the existing `SpecialModeCoordinator.BlockingExclusive` lease.
- The viewer does not acquire a second lease.
- Do not create `MediaViewerManager`, `PhoneMediaManager`, another singleton, or another `SpecialMode` lease.
- The existing Phone UI remains the owner of its current layout and stays visually behind the viewer.

## Open contract

The viewer may open only when all conditions hold:

1. Chat is running and has not entered cleanup or completion.
2. The source is an already revealed `ChatEntryKind.Image`.
3. The source Image has a non-null `Sprite`.
4. The player performs a direct click on that media card.

Opening is rejected before reveal, from a typing indicator, from Text or Choice entries, and after Chat cleanup. A second open request while the viewer is open is a no-op. The click must be on the media card; unrelated transcript clicks are not open requests.

## Presentation and input

The viewer is an overlay above the existing Phone UI:

- add a dark translucent scrim;
- show one large image area with the source `Sprite` using aspect-fit;
- preserve aspect ratio, with no crop or stretch;
- keep safe margins and provide a close `X` in the upper corner;
- do not modify the existing `PhoneShell` layout.

While open, the Phone UI may remain visible behind the overlay but must not receive input. Transcript/media cards, reply cards, and all other underlying Chat controls are blocked. The existing Chat `BlockingExclusive` continues to block background VN input, Save/Load, Quick Menu, and other controls according to the current Chat policy.

Close is allowed through the close `X`, `Escape`, or a click on the viewer background/scrim. A click on the displayed image itself does **not** close the viewer.

### Escape precedence

Normally, active Chat `BlockingExclusive` makes Escape a no-op. While the local media viewer is open, the viewer gets first precedence and Escape closes **only** the viewer. After close, Escape immediately returns to normal Chat behavior and is a no-op. Escape must not complete Chat, route to `returnScene`, or enter another modal.

## Close and state preservation

Closing removes only the viewer overlay and restores input to the same active Chat state. It must:

- leave Chat running;
- leave the transcript unchanged;
- leave the revealed Image entry and reply-choice state unchanged;
- leave `GameState` unchanged;
- never route to `returnScene`;
- never complete or restart Chat;
- never duplicate an incoming cue.

## Chat pacing

While the viewer is open, local Chat entry pacing and terminal presentation timers are paused. The transcript must not mutate behind an image being inspected.

- Pause through local `ChatController` gating only.
- Do not change `Time.timeScale`.
- Existing unscaled countdown state resumes from its remaining time after close.
- If no pacing or terminal timer is pending, no special action is required.

This pause applies only to Chat pacing; it does not create a new timer or global pause mode.

## Audio

R06 adds no open or close SFX. R08 semantics remain unchanged:

- opening or closing the viewer cannot replay the Chat open cue or an incoming cue;
- an incoming cue is still emitted only by the existing actual Image/Text reveal path;
- the viewer does not call or alter the existing audio ownership.

## Transient state and cleanup

Viewer-open state is runtime-only. Do not add it to `SaveData`, backlog, `DialogueReadHistory`, Replay history, or profile JSON. `SaveData` remains v3.

On Chat Complete, failure, `OnDisable`, or `OnDestroy`, the viewer must close safely and release all local UI/input references. Cleanup must leave no stale overlay, duplicate route, or leaked input blocker. Existing Chat exactly-once completion and return behavior remains authoritative.

## Out of scope

R06 does not include zoom, pinch, pan, rotate, image download/save, gallery unlock integration, a multi-image carousel, captions or metadata, video, animated media, attachments, external links, canon images/content, or a generic media viewer outside Chat.

## Technical demo flow

The manual future demo uses the existing technical placeholder only:

`typing -> message -> Image reveal -> click Image card -> fullscreen placeholder -> close -> same Chat transcript/replies -> reply -> normal return`.

No new canon artwork or content is authorized.

## Required implementation tests

The future implementation must cover:

- only a revealed Image card with a non-null Sprite opens;
- pre-reveal, typing, Text, Choice, post-cleanup, and non-card clicks do not open;
- only one viewer instance exists and a second open is a no-op;
- aspect-fit presentation preserves aspect ratio without crop/stretch;
- underlying transcript and reply input are blocked;
- `X`, Escape, and scrim/background close the viewer;
- clicking the displayed image does not close it;
- Escape closes the viewer only, while the next Escape in normal Chat is a no-op;
- pending pacing/terminal timers pause and resume from remaining time;
- R08 sound counts and cue semantics are unchanged;
- transcript, GameState, reply state, and return routing are unchanged by open/close;
- Chat completion, failure, disable, and destroy remove the viewer safely;
- no viewer state is serialized and `SaveData.CurrentVersion == 3`;
- `VNPrototype.unity` remains unchanged.

## Implementation boundary

The sole next step is:

**Implement Embedded Chat Media Viewer (R06).**

Implementation must preserve the existing ChatController ownership, PhoneShell layout, technical placeholder, SaveData v3, R08 audio behavior, and unchanged `VNPrototype.unity`. Do not begin implementation as part of this policy commit.
