# Character Hub / Bios Technical Policy

## Status and Scope

Reviewed baseline: `origin/master` / `HEAD` at `33ed52548c2c74ebe0c0065310cd8d70a75832c8`.

This policy defines a future **technical** Character Hub only. It changes no runtime code, Unity scene, asset, `SaveData`, or project setting.

All future V1 fixtures are explicitly **TECH DEMO ONLY / NOT CANON**:

- `TEST CHARACTER A` — visible; biography text `TEST BIO A`.
- `TEST CHARACTER B` — locked; biography and relationship value hidden.

Real characters, biographies, portraits, relationship rules, and unlock rules are supplied later by the narrative and art teams. This document does not define canon content, relationship thresholds, or labels such as Friend/Love/Hate.

## Audit Findings

- `GameState` is the persistent runtime singleton. It currently owns the three technical relationship integers `trustMasha`, `trustArtem`, and `leraInterest`; it resets them in `ResetState()` and applies choice deltas directly.
- `SaveData.CurrentVersion` is **3**. `SaveManager` serializes and restores those three integers together with the existing game state. Character Hub state is not present.
- `RelationshipFeedback` only creates the current post-choice toast for those three values. It is not a character-profile system.
- `VNDialogueController` owns dialogue progression, Auto/Skip timers, Backlog, in-VN Settings, Hide UI state, confirm exit, and the scene-local `SpecialModeCoordinator`. Existing normal modal code stops timers on open and reevaluates normal eligibility on close.
- `VNQuickMenu` dispatches existing actions through `VNDialogueController`, observes its availability, is hidden by Hide UI and by special-mode Quick Menu restrictions, and is the preferred V1 access point.
- Backlog, Settings, `ManualSaveLoadPanel` (including confirmations), and the main-menu confirmation already have independent modal ownership. Their open state must remain mutually exclusive with Character Hub.
- `SpecialModeCoordinator.BlockingExclusive` is reserved for exclusive authored interactions. It blocks advance, Auto, Skip, Quick Menu, save/load, Backlog, Settings, Escape cancellation, and Main Menu. Character Hub must not acquire a special-mode lease.
- Replay is a transient `SceneFlowManager` mode. During replay the Quick Menu retains History, Auto, Skip, and Settings, while save/load is denied; Character Hub must be unavailable for the whole replay.
- `MainMenuController` owns main-menu panels and the Gallery/Replay entry only. V1 Character Hub does not add a main-menu route.

Therefore `trustMasha`, `trustArtem`, and `leraInterest` are only the current **technical bridge**, not the final architecture for characters or relationships.

## V1 Static Profile Data

Future static character data lives in a `CharacterProfileDefinition` ScriptableObject, never in `GameState` or `SaveData`:

```csharp
string characterId;
string displayName;
Sprite portrait;
string biography;
```

Rules:

- `characterId` is a stable, non-empty ordinal identifier; display text is not an ID.
- A catalogue validates IDs before the Hub is usable and rejects null definitions, empty IDs, and duplicates. Rejection fails closed: the invalid profile is not shown and a diagnostic is emitted.
- The asset is read-only at runtime. Opening, refreshing, locking, or closing the Hub must not mutate its fields.
- Static profile data is authored data, not campaign progress. It is not copied into `GameState`, save JSON, or replay snapshots.

V1 intentionally has no generic biography progression, spoiler layer model, profile editing, filter/category system, or global unlock framework.

## Typed Relationship Contract

Relationship lookup uses a closed typed contract:

```csharp
public enum CharacterRelationshipSource
{
    None = 0,
    TrustMasha = 1,
    TrustArtem = 2,
    LeraInterest = 3
}
```

`CharacterRelationshipResolver` receives a `GameState` and resolves with an explicit `switch` over this enum. `None` means no relationship value is exposed.

Forbidden: reflection, string field lookup, `Dictionary`-based arbitrary field access, `eval`, or dynamic expressions.

The technical demo may bind `TEST CHARACTER A` to one explicit non-`None` source solely to prove typed numeric reading. It displays only the numeric value. `TEST CHARACTER B` exposes no value while locked.

This enum is a **technical bridge**, not a canon relationship model. It does not define characters, affinity semantics, thresholds, ranks, statuses, unlock conditions, or future save-schema requirements. A later narrative-approved model replaces or extends it through a separately reviewed typed migration.

## Locked Technical Demo State

V1 does not build a generic story-unlock framework.

The two fixture visibility states are fixed transient/demo configuration:

| Fixture | Hub state | Biography | Relationship |
|---|---|---|---|
| `TEST CHARACTER A` | visible | `TEST BIO A` visible | numeric value may be shown through the typed bridge |
| `TEST CHARACTER B` | locked | hidden | hidden |

The lock is not stored in `SaveData`, profile JSON, PlayerPrefs, `GameState`, or a generic flag registry. It may be a narrow controller/test-fixture configuration until narrative/art teams supply real authored rules.

## Access and Modal Contract

### Access point

V1 uses one entry point: a **Character Hub** button in the existing `VNQuickMenu`.

It is an ordinary VN modal panel, not a `SpecialModeCoordinator` mode and not a Main Menu feature.

### Open eligibility

Opening is denied without state mutation when any of these is true:

- Settings, Backlog, Manual Save/Load, or any Save/Load confirmation is open;
- the existing main-menu confirmation is open;
- a `BlockingExclusive` special mode is active;
- Hide UI is active;
- replay mode is active.

The hub also cannot open over an ordinary choice or any future modal that already blocks normal dialogue interaction. Conversely, while open, it must block entry to those conflicting modals and special modes.

### While open

- dialogue advance is blocked;
- Auto is paused;
- Skip is paused;
- `Esc` closes the Hub;
- normal dialogue eligibility is restored after close;
- Auto/Skip resume only according to their existing enabled state and normal eligibility; opening the Hub must not toggle, persist, or otherwise rewrite the Auto preference or Skip state;
- opening, refreshing, and closing do not mutate `GameState`.

Implementation follows the existing normal-modal path: stop active timers on successful open, add Hub state to normal advance/modal eligibility checks, and call the existing timer eligibility restart path after close. It does not use a special-mode lease merely to obtain blocking behavior.

The close action must be idempotent. Input ownership follows existing modal precedence: because conflicts are denied at open time, `Esc` closes the Hub when it is the active ordinary modal; it must not advance dialogue on that same press.

### Persistence

Hub open state is transient and is not serialized. `SaveData.CurrentVersion` remains **3**.

## Future Runtime Scope

Keep the first implementation narrow; do not add singleton managers.

- `CharacterProfileDefinition.cs` — static ScriptableObject definition and validation helpers only.
- `CharacterRelationshipResolver.cs` — typed enum-to-`GameState` numeric resolver only.
- `CharacterHubController.cs` — scene-local modal state, fixture/profile presentation, open/close eligibility, refresh, and integration with the existing controller/Quick Menu.

The implementation may use the controller's serialized references for the two test definitions and panel UI. It must not add a general unlock service, generic flag dictionary, new persistent manager, or a parallel dialogue runtime.

The future implementation adds only TECH DEMO assets/references when separately authorized; it does not author or substitute real character content.

## Required Tests

Focused future tests must cover:

1. unique IDs and duplicate rejection;
2. visible `TEST CHARACTER A`;
3. locked `TEST CHARACTER B`;
4. hidden biography and relationship for locked B;
5. typed relationship reads from `GameState` for every allowed source and `None`;
6. displayed numeric value refreshes after a `GameState` change;
7. definition assets are not mutated;
8. modal dialogue blocking;
9. `Esc` close without dialogue advance;
10. Auto/Skip pause and eligibility-based restore;
11. conflicts with Settings, Backlog, Save/Load (including confirm), special mode, Hide UI, and Replay;
12. `SaveData.CurrentVersion == 3`;
13. opening, refreshing, and closing the Hub do not mutate `GameState`.

## Deferred Inputs

Before replacing the technical fixtures, narrative and art teams must supply the real character IDs, display names, biographies, portraits, relationship/unlock rules, and spoiler policy. Their content is deliberately outside this technical policy.
