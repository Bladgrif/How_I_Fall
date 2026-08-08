# Gallery / Replay Foundation Policy

## Current State

Reviewed baseline: `origin/master` at `2d23bf639ca3bbf1a8b6411e2d44eadf9b9d56ec`, after functional commit `a088a29449f6fc59496c311db3e8162302fba40e`.

This is a design-only decision. No runtime code, Unity scene, `MainMenu`, `SaveData`, PlayerPrefs key, Gallery UI or replay asset is changed by this document.

- `MainMenuController.OpenGallery()` only writes `Gallery is not implemented yet`; no Gallery panel, entry registry or replay route exists. `MainMenu.unity` has no serialized Gallery panel reference on `MainMenuController`.
- `SceneFlowManager.StartNewGame()` clears pending load, calls `GameState.ResetState()`, then loads `VNPrototype`. `ReturnToMainMenu()` only changes scene; it does not reset `GameState`.
- `GameState` is a `DontDestroyOnLoad` singleton and is the direct state source for `VNDialogueController`: scene/cursor, choice-result state and nine persisted numeric values are mutated during dialogue, conditional choice evaluation and routing.
- `SaveData` is v3. `SaveManager` serializes `GameState` and a save-scoped `DialogueBacklog` snapshot; it can apply a save in place or replace the live singleton before opening `VNPrototype`. Continue selects the newest valid Manual, Quick or Auto slot.
- `VNDialogueController` starts the configured scene, routes `defaultNextScene`/choice targets, requests automatic saves at scene entry and choices, and directly uses `GameState.Instance`. It owns a runtime backlog, but `DialogueReadHistory` is separately profile-persistent in PlayerPrefs.
- `DialogueSceneRegistry` is the canonical lookup for ordinary dialogue scene IDs. `DialogueSceneData` contains display data, music, lines, choices and `defaultNextScene`; its normal routing assumes registry membership.
- `SaveManager`, `AudioManager`, `SettingsManager`, `GameState` and `SceneFlowManager` are all `DontDestroyOnLoad` singletons. `AudioManager` owns the active persistent music/ambience sources; `SettingsManager` owns player preferences.
- Leaving VN for Main Menu destroys the scene-local controller and its backlog/UI, while persistent managers remain. `VNDialogueController.OnDestroy()` clears only its scene-local `SpecialModeCoordinator` lease.
- The current Quick Menu exposes History, Skip, Auto, Save, Quick Save, Quick Load, Load, Settings and Main Menu. Its buttons dispatch to the controller; keyboard routes use the same controller methods. Hide UI and normal modal gates already affect these paths.

The significant leak risks are therefore the singleton `GameState`, persistent `DialogueReadHistory`, persistent audio sources, any outstanding auto/pre-load-save request, and the scene-local backlog/UI that must not be mistaken for campaign state.

## Goals

Gallery / Replay V1 must eventually:

1. persist an unlock for one stable replay ID independently of save slots;
2. show one visible locked/unlocked Gallery item from Main Menu;
3. start an unlocked replay at a separate `DialogueSceneData` graph;
4. keep replay choices and conditions isolated from the campaign;
5. prevent Manual, Auto, Quick saves and all loads during replay;
6. preserve campaign save files, `GameState`, current position, choice state and backlog;
7. leave Continue selecting the real campaign saves; and
8. end normally or after a controlled failure at Gallery/Main Menu.

All initial content is neutral: `TEST REPLAY`, `TEST REPLAY START` and `TEST REPLAY END`, explicitly **TECH DEMO ONLY / NOT CANON**. It must not reuse the classroom/timed narrative-beat demo, real characters or a normal story route.

## Non-Goals

V1 does not add categories, filters, character tabs, route metadata, CG browser, unlock-all player setting, replay statistics, achievements, final artwork, real Extra content or a general flag dictionary.

It also does not introduce a save-schema revision, new Audio snapshot framework, a second story runtime, arbitrary script execution, or a broad Quick Menu redesign.

## Campaign State Isolation Options

| Option | Fit with current code | Advantages | Rejected risk |
|---|---|---|---|
| A. Snapshot then temporary replay values in the existing `GameState` singleton; restore before exit | High | Smallest focused diff; compatible with the controller and `SaveManager`, both of which directly read `GameState.Instance`; supports future choices/conditions. | Unsafe if the snapshot/restore boundary is informal or save/load/read-history paths are not explicitly fenced. |
| B. Inject a separate `ReplayContext`/state provider into `VNDialogueController` instead of `GameState.Instance` | Low for V1 | Strongest semantic separation and cleanest long-term runtime model. | Requires refactoring every direct state read/write, conditional evaluator, save position/restore path and likely SaveManager integration; too large for a first foundation. |
| C. Separate isolated replay scene/runtime that never references campaign `GameState` | Medium-low | Hard scene boundary and no temporary mutation of the singleton. | Duplicates or forks VN execution, Quick Menu, backlog, audio and input behavior; high drift and QA cost. |

## Decision

Select **Option A: transactional snapshot/restore of the existing `GameState`**, owned by a new transient top-level replay session, not by `SpecialModeCoordinator`.

Before a replay starts, `ReplaySession` captures every mutable `GameState` field used by v3: all nine numeric values, `currentSceneId`, `currentLineIndex`, `currentLineId`, `selectedChoiceIndex`, `choiceResultActive` and `pendingNextSceneId`. This is an in-memory typed snapshot, **not** a `SaveData` instance and never serialized.

The session then applies a clean, replay-only `GameState` value set and starts the registered test graph. During replay the existing singleton is temporarily the execution state, but the captured campaign snapshot is the authority and is inaccessible to choices, conditions and dialogue code. A single `RestoreCampaignState()` copies the snapshot back exactly once before any return to Main Menu; it runs from normal End Replay and every supported recovery path.

The session must also capture a `VNDialogueController` backlog snapshot when a controller exists, start replay with an empty backlog, and restore/discard through the same exactly-once cleanup boundary. This preserves the model even in direct-runtime integration tests; Gallery V1 itself starts from Main Menu, where no campaign controller is normally alive.

This is deliberately not an attempt to resume a campaign scene after replay. End Replay returns to Main Menu. The restored snapshot protects live state and future callers; Continue still restores from its pre-existing campaign save files.

`SpecialModeCoordinator` is not the replay owner. It is scene-local and its available `BlockingExclusive` policy blocks normal dialogue progression, Auto and Skip. A replay is a multi-scene top-level VN execution mode that must allow normal dialogue progression, History, Auto, Skip and Settings. The future implementation should instead keep a small plain-C# `ReplaySession` on the existing persistent `SceneFlowManager`, with a narrow replay-mode query exposed to the VN/UI/save paths. It creates no additional global manager GameObject.

## Replay Context

V1 provides immutable, typed metadata only:

```csharp
public readonly struct ReplayContext
{
    public string ReplayId { get; }
}
```

`replayId` is the stable registry key, not display text. V1 test replay has no variants. No replay may read the captured campaign snapshot or a loose `Dictionary<string, object>`.

If authored variants are required later, extend a closed typed value owned by the definition, for example `ReplayVariant` enum plus explicitly named optional fields. A new variant must state its source, unlock rule, UI text and isolation test; it may not infer arbitrary campaign flags at runtime.

## Unlock Persistence

Select a small versioned **profile JSON** file under `Application.persistentDataPath`, separate from the existing `Saves` hierarchy and all `SaveData` slots.

Why not PlayerPrefs: `DialogueReadHistory` and settings legitimately use PlayerPrefs, but Gallery unlocks are an inspectable profile-level registry with duplicate-ID validation, future migration needs and a distinct corruption policy. A small JSON payload makes this boundary explicit without adding unlock fields to campaign saves.

The future `ReplayUnlockRegistry` contract is:

```csharp
bool IsUnlocked(string replayId);
bool Unlock(string replayId);
void ResetForTests();
```

- IDs use an ordinal, non-empty stable string convention, e.g. `test_replay_v1`; display names are never IDs.
- `Unlock` is idempotent: it returns whether the set changed, writes only after successful validation, and never invokes arbitrary content.
- Registry definition validation rejects duplicate replay IDs before Gallery use. A malformed definition fails closed: the affected item stays unavailable and is logged.
- Missing profile file means no unlocks. Corrupt/unknown profile data is quarantined or ignored with a warning and becomes an empty registry; it must never crash boot, unlock all content or touch save slots.
- New Game, loading an old save and deleting Manual/Auto/Quick saves never call the registry and therefore never relock an item.
- A debug unlock-all helper, if needed, is Editor/test-only and cannot be a player-facing setting.

## Replay Entry Data

The future minimal authoring asset is `ReplayEntryDefinition`:

```csharp
string replayId;
string displayName;
Sprite thumbnail;
DialogueSceneData startScene;
```

The Gallery UI owns locked overlay/tint, so V1 needs no separate locked thumbnail asset. `startScene` must be non-null, contain lines, and belong to the replay graph validated for this entry. The replay graph uses separate TECH DEMO ONLY assets such as `replay_demo_start.asset` and `replay_demo_end.asset`; it must not be added to the normal campaign route or silently depend on campaign scene IDs.

## Gallery Locked / Unlocked UX

V1 has one visible card:

- **Locked:** generic TEST fixture identity and lock state only; click does not start a replay and produces no spoiler text.
- **Unlocked:** the same card is actionable and starts the entry after definition validation.

`TEST REPLAY` is not canon and has no final art direction. The future Main Menu panel is intentionally a basic dedicated Gallery panel; it does not redesign the current Main Menu.

## Replay Mode

`ReplaySession.IsReplayMode` is a transient explicit query. It is established before the replay VN scene/controller can execute its initial `LoadDialogueScene()`, because ordinary scene entry requests an autosave.

The controller evaluates choices and conditions against the temporary replay `GameState`, so replay mutations never enter the captured campaign state. Direct external calls that change campaign routing are out of contract while a replay session is active.

Replay Mode permits normal dialogue progression and these player actions: **History, Auto, Skip and Settings**. Hide UI remains the existing transient visual feature and does not change replay ownership. End Replay is the only replay navigation action back to Main Menu.

## Quick Menu Behavior

During replay the existing Quick Menu remains visible with the smallest necessary change:

| Action | V1 replay behavior |
|---|---|
| History | Allowed; reads replay-only backlog. |
| Skip | Allowed; applies the configured skip preference to replay-only read tracking. |
| Auto | Allowed; uses the ordinary player setting. |
| Settings | Allowed; keeps existing global settings semantics. |
| Save / Quick Save / Load / Quick Load | Hidden, not merely cosmetically disabled. Hotkeys and public controller methods are also denied. |
| Main Menu | Replaced in place with **End Replay**; it ends without the ordinary campaign-return confirmation. |

The implementation must not retain a clickable hidden button or rely solely on UI state. All hotkeys, Quick Menu callbacks, ManualSaveLoadPanel opens and public request methods share the replay-mode permission check.

## Save / Load Policy

Replay never creates Manual, Auto, Quick or pre-load Auto saves and never reads a save.

A replay-mode guard is required at two layers:

1. the controller/UI layer rejects `OpenSave`, `RequestQuickSave`, `RequestAutoSave`, `RequestPreLoadAutoSave`, `OpenLoad` and `RequestQuickLoad` before screenshot capture, panel opening or routing; and
2. `SaveManager` rejects `SaveSlot`, `SaveAuto`, `SaveQuick`, `LoadSlot` and `LoadLatest` while `ReplaySession.IsReplayMode` is true as defense in depth.

The second layer protects against future or accidental call sites. Denial changes no slot, preview, pending load or `GameState`; in particular it must not create the normal pre-load autosave. Main Menu Continue remains outside replay and continues to use the existing newest-valid campaign-slot selection.

## Backlog / Read History Isolation

Replay starts with an empty `DialogueBacklog`. Its entries remain in memory for the current replay only and are discarded on End Replay or failure. The captured campaign backlog is restored exactly when an active controller supplied one; replay backlog is never merged into it and is never serialized.

**Read-history decision: no profile read-history writes during replay.** `DialogueReadHistory` is save-independent and currently persistent; replay must suppress `MarkSeen`/write calls. V1 Skip may use a replay-local in-memory read set keyed by its separate technical scene IDs, initially empty, or require all-text Skip only until that local set is implemented. It must never add replay or campaign IDs to the profile history.

## Audio Policy

`AudioManager` is persistent, so replay scene music can otherwise remain after replay. V1 does not add an audio snapshot framework.

At replay entry, the session records whether music was playing and its clip; it also records the active ambience clip/playing state if replay later uses ambience. Replay may apply `DialogueSceneData.backgroundMusic` through the current API. On every End Replay/recovery path, it stops replay-owned audio, restores the captured audio state before scene change when valid, then lets `MainMenuMusicPlayer` claim Main Menu music normally. No replay clip may remain selected after the return path.

## End Replay

`EndReplay()` is an idempotent session operation, callable from the Quick Menu replacement and the terminal replay route.

Exact sequence:

1. stop replay input/timers and prevent a new save/load request;
2. stop or restore replay-owned audio;
3. discard replay-only backlog and local seen state;
4. restore the captured campaign `GameState` and campaign backlog snapshot exactly once;
5. clear `IsReplayMode` and session references; and
6. route through `SceneFlowManager.ReturnToMainMenu()`.

A terminal replay graph must call this controlled route; it must never fall through to the existing `EndPrototypeText` as its completion behavior. If a replay attempts to leave through the ordinary Main Menu action, that action resolves to End Replay instead.

## Failure Recovery

Replay startup validates unlock, definition, unique ID, entry scene and replay-graph membership **before** mutating live `GameState` or changing scene. A rejection leaves the campaign untouched and Gallery open with a diagnostic/toast.

After mutation begins, `ReplaySession` owns a `try/finally` cleanup boundary and subscribes to the relevant scene-host lifecycle. Invalid scene, load failure, replay exception, terminal-routing error or destroyed VN host runs one fail-safe operation: restore the captured state/backlog/audio, clear replay mode, log a diagnostic, then return to Main Menu. Duplicate cleanup is harmless.

If the entire application process terminates, no in-memory state can be restored; the contract remains safe because replay writes no campaign save data and does not alter the profile read history. On the next launch, Continue still reads the untouched campaign saves.

## Save Compatibility

**No `SaveData` v4.** Unlocks are profile-level JSON; replay execution, snapshots, context and local history are transient. `SaveData.CurrentVersion` remains 3, and current v1/v2 migration behavior is unchanged.

The unlock profile is not nested in a save slot and must not be copied into save previews or slot JSON. A later decision to persist an in-progress replay would be a separate feature with its own schema/migration review; V1 explicitly does not support it.

## Technical Demo

Future Phase 1 uses only neutral fixtures:

- Gallery card: `TEST REPLAY`;
- start asset/text: `TEST REPLAY START`;
- terminal asset/text: `TEST REPLAY END`.

Every relevant fixture is marked **TECH DEMO ONLY / NOT CANON**. No current classroom/timed demo asset, character, relationship, background or story scene is a Gallery item or replay prerequisite.

## Future Implementation Scope

**Phase 1 — one implementation scope**

- profile JSON unlock registry and focused tests;
- `ReplayEntryDefinition` validation and one TEST entry/graph;
- `SceneFlowManager`-owned transient `ReplaySession` using the selected snapshot model;
- replay-aware controller/save/input/Quick Menu permission gates;
- minimal Main Menu Gallery panel/card plus End Replay;
- smoke/integration tests for the acceptance criteria below.

**Later, separately scoped**

- team-supplied canon replay data, thumbnails and artwork;
- typed variants with explicit authoring rules;
- categories, filters, tabs, CG browser, achievements, statistics and QA-only unlock-all tooling.

## Test Plan

Future coverage must prove at least:

1. locked item cannot start replay;
2. `Unlock` is idempotent;
3. unlock survives registry recreation/restart;
4. New Game, old-save load and deleted saves do not relock;
5. replay start requires a valid unlocked entry and begins at TEST scene;
6. Manual, Quick and Auto saves, pre-load Auto, Manual load and Quick Load are denied; no files change;
7. campaign `GameState` cursor, stats, relationships and choice state are preserved;
8. campaign backlog is preserved, replay backlog starts empty and is discarded;
9. replay does not update profile `DialogueReadHistory`;
10. History, Skip, Auto and Settings remain usable; save/load controls are absent and Main Menu becomes End Replay;
11. terminal replay invokes End Replay;
12. invalid entry, scene-load failure, exception and destroyed host restore the campaign safely;
13. Continue still points to campaign data after replay;
14. `SaveData.CurrentVersion` remains 3; save JSON contains no unlock/replay state.

## Risks

- The selected model temporarily writes the singleton, so a missing guard or missed cleanup can leak replay values. The exact snapshot, two-layer save/load gate and idempotent `finally` recovery are mandatory.
- `VNDialogueController` currently requests autosave from normal scene/choice methods. Replay mode must be active before those methods run; testing only visible Quick Menu state is insufficient.
- `DialogueReadHistory` is profile-persistent and its current line-display path writes eagerly. It needs a specific replay-mode suppression, not merely separate technical IDs.
- A controller is scene-local while the replay transaction crosses Main Menu/VN scene changes. The transaction must stay on existing persistent `SceneFlowManager`, not a scene-local special-mode lease.
- Current `AudioManager` has no full snapshot API. V1 must restore explicit recorded state and verify Main Menu music ownership before expanding authored replay audio.

## Acceptance Criteria

Phase 1 is acceptable only when one unlocked TEST replay can be started and ended from Gallery while all of the following hold:

- locked/unlocked UI is truthful and uses no canon content;
- replay graph uses only separate TECH DEMO ONLY assets;
- campaign state, backlog, saves, Continue target and read history remain unchanged;
- no Manual/Auto/Quick/pre-load save or load path can execute during replay, including hotkeys/direct public paths;
- terminal and failure paths perform controlled, idempotent recovery to Main Menu;
- `SaveData` remains v3; and
- focused automated tests cover the listed isolation and failure cases.
