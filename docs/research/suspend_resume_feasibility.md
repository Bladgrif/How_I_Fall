# Suspend / Resume Feasibility

## Current HIF behavior

`Continue` calls `SaveManager.LoadLatest()`. It scans all six Manual, six Quick and six Auto slots, reads and validates each candidate before ranking it by `createdAtUtc` (Manual, then Quick, then Auto only as a timestamp tie-break). A malformed, incompatible, mismatched or otherwise unresolvable newest record is non-loadable and is skipped; `Continue` selects the next newest valid record. It then restores the saved narrative position, `GameState` fields and the optional v3 backlog snapshot, either in place or after routing to `VNPrototype`.

Auto and Quick are six-slot rotations. Ordinary autosave is requested on normal dialogue-scene entry and when choices are shown; a confirmed load has its own pre-load Auto checkpoint. Quick Save is player-invoked. Save capture requires an active `VNDialogueController`, a valid stable dialogue position and an end-of-frame screenshot. A normal Main Menu/OS quit does **not** currently create a save. Thus returning after ordinary exit is easy when a recent Auto/Quick/Manual save exists, but `Continue` cannot recover progress made after the last successful checkpoint.

## User problem

Suspend/resume would primarily cover one narrow gap: a player closes the app between ordinary Auto checkpoints and expects to resume exactly there without deliberately saving. It does not improve corrupted-save recovery, because `Continue` already rejects a bad newest candidate, nor does it promise crash recovery: Unity does not guarantee a reliable callback, completed screenshot capture or completed file write during a crash or forced termination.

## Options considered

| Option | Player value | Cost / risk | Fit now |
|---|---|---|---|
| A. Keep current Continue | High for every successfully created save; corruption fallback already exists | No new persistence or lifecycle contract | Best current baseline |
| B. Explicit suspend/resume | Removes the between-checkpoint exit gap | Requires quit/pause lifecycle policy, transient-slot ownership, ranking, invalidation and special-mode handling | Not justified for the technical demo |
| C. Later autosave-policy pass | Gives content-authored recovery at meaningful choices and scene boundaries | Needs real routes/choices, but reuses tested Auto semantics | Better future investment |

A separate transient slot is preferable to silently overwriting an Auto slot if this is ever implemented: it preserves the player's visible Auto history and makes one-time resume/invalidation explicit. A metadata pointer alone does not contain the required narrative state. Reusing the existing Auto format could avoid a `SaveData` v4 only if a new storage path/type is introduced outside the closed `SaveSlotType` contract; otherwise it changes that contract and every slot UI/ranking/validation path.

## SaveData v3 impact

The current v3 record already contains stable campaign state: scene and line identity, choice-result state, the nine persisted numeric values and an optional backlog snapshot. It deliberately does not serialize normal modal state, special-mode state, typewriter progress, screenshot capture in flight or animation/timer state.

A future resume must restore only this stable gameplay state. It must not promise exact restoration of Preferences, Save/Load confirmation, Backlog scroll/modal state, typewriter progress or transient animation. Retaining v3 is feasible only with a clearly isolated storage envelope and no new fields in `SaveData`; it still requires a reviewed migration/compatibility decision because the current enum, directories, slot validation and Continue scan admit only Manual/Auto/Quick.

## Special-mode risks

| State at close/pause | Audit finding | Safe future policy |
|---|---|---|
| Preferences, Backlog, Game Menu, return or Save/Load confirmation | Normal modal state is scene-local and unsaved; Save/Load can also be mid-operation | Do not suspend the modal. Either reject/defer the checkpoint or save only after it has fully completed and resume normal gameplay UI. |
| Replay | Replay deliberately denies all saves/loads and restores an in-memory campaign snapshot on exit | Never create or consume a suspend record in Replay. |
| Chat/Phone | `BlockingExclusive`; transcript, pacing, active entry and branch state are runtime-only and unsaved | Do not suspend while active. |
| Map / Interactive Hotspot | Exclusive interactive state and completion progress are runtime-only | Do not suspend while active. |
| Timed Narrative Beat | Exclusive unscaled timer and pending outcome are runtime-only | Do not suspend while active. |
| Other special mode | `SpecialModeCoordinator` is fail-closed unless the mode owns a serializable restore contract | Default deny; no implicit opt-in. |

## Failure/recovery model

A future implementation would need all of the following before it can be considered safe:

- normal Quit, Main Menu Quit, `OnApplicationQuit` and application pause must be distinct best-effort triggers; none is a crash guarantee;
- no save may start while a capture/write is already in flight, a normal modal confirmation is active, or an exclusive special mode is active;
- write JSON and preview to temporary files, validate the finished record before publishing, and keep the previous suspend until replacement is complete;
- corrupted, stale, unsupported-version or partial suspend must be ignored without affecting Manual/Auto/Quick `Continue` recovery;
- an accepted suspend must have an explicit freshness/ranking rule. It must not displace a newer valid Auto/Quick save merely because it was written late during shutdown;
- delete/invalidate only after successful resume. New Game must invalidate it; normal save/load invalidation needs a product decision rather than an implicit destructive rule.

The existing JSON/preview temporary-file cleanup reduces ordinary-write damage, but it is not an atomic paired-file transaction and does not make shutdown capture reliable.

## Testing implications

This is not a small unit-test-only feature. It would require focused persistence/validation tests plus PlayMode lifecycle coverage for normal quit intent, Main Menu quit, pause, a stale/corrupt/partial suspend, successful one-time resume, New Game invalidation, ranking against newer Auto/Quick saves, and each deny state above. Crash/Alt+F4 correctness cannot be asserted as a guaranteed automated outcome; it needs explicitly documented best-effort behavior and platform QA.

## Recommendation

**DEFER.** Explicit suspend has real value only for progress after the last checkpoint, but that value is currently smaller than the lifecycle and recovery surface it adds. HIF already provides robust latest-valid `Continue`, six rotating Auto/Quick slots and pre-load recovery. For **Polished Functional Demo First**, the next save-related improvement should be a content-informed autosave policy, after real choices/checkpoints exist.

## If ACCEPT: minimal implementation outline

Not applicable while this decision is DEFERRED. A later proposal must first define a separate transient-record envelope, stable-state-only restore, deny list for modals/special modes, one-time consumption, timestamp/ranking semantics and the test matrix above. It must preserve `SaveData.CurrentVersion == 3` or explicitly justify a version migration.

## If REJECT/DEFER: why existing Continue is sufficient

For the current demo, `Continue` already returns the player to the newest loadable Manual/Quick/Auto checkpoint and survives a corrupted newest candidate by falling back safely. The remaining gap is not a loss of all recovery, but the distance since the last checkpoint. That is better addressed later by authored Auto timing than by a second persistence contract that cannot safely serialize the demo's transient UI and special modes.

## Decision

**DEFER — no suspend/resume implementation is authorized.** Revisit only together with real-content autosave policy and a bounded lifecycle design; do not alter `SaveData` v3, scenes, prefabs or production C# from this audit.
