# Special Mode Coordinator Policy

## Current Blocking Model

Reviewed baseline: `origin/master` at `ab64517f2b66915028c0942895c1c83c8b726033`, after functional commit `9647986856fa8c5a545ee375e4a60866a9472212`.

The current VN has several independent normal-UI blockers; it has no special-mode coordinator.

| State / owner | Current behavior | Duplicated or missing ownership rule |
|---|---|---|
| Choice panel | `showingChoice` and the active `choicePanel` stop dialogue advance, Auto and Skip; choices remain part of dialogue flow. | It is checked separately from save blocking, so saving at an open choice remains possible and must stay supported. |
| Backlog | `ShowBacklog()` stops Auto; the active panel blocks advance, Auto, Skip and system save/load; closing restarts Auto and Skip timers. | Opening it is not guarded against another normal panel. |
| VN Settings | Active settings panel blocks advance, Auto, Skip and system save/load; closing restarts Auto and Skip timers. | Opening it is not guarded against another normal panel. |
| Manual Save/Load | `ManualSaveLoadPanel.IsOpen` blocks advance, Auto, Skip and system save/load. It owns its nested overwrite/delete/load confirmation and handles Escape confirmation-first. | It has its own `Update()` and Escape handling, while the dialogue controller separately defers Escape to it. |
| Return-to-main-menu confirmation | The active `confirmExitPanel` blocks advance, Auto, Skip and system save/load. | It is another direct active-state check in the dialogue controller. |
| Quick Menu | `VNQuickMenu` directly invokes History, Skip, Auto, Save, Quick Save, Quick Load, Load, Settings and Main Menu. | It has no permission gate and can request an action while another UI state is active. |

`VNDialogueController.IsAdvanceBlockedByOpenPanel()` checks choice, backlog, main-menu confirmation, settings and the save/load panel. `IsSystemSaveBlockedByModal()` checks backlog, main-menu confirmation, settings and the save/load panel, deliberately **not** the choice panel. `CanAutoAdvance()` and the Skip coroutine depend on the advance blocker, but their timer lifecycle is separate. The six player hotkeys in `VNInputMap` dispatch into the controller without one common modal/special permission query.

Current Escape priority in `VNDialogueController.Update()` is Backlog, main-menu confirmation, Settings, then an open `ManualSaveLoadPanel`; the latter's own `HandleEscape()` first cancels its nested confirmation and only then closes the panel. This works for the presently authored panels but is distributed across two update loops and is not a reusable contract for an authored map, QTE or phone screen.

## Goals

1. Give future authored special interactions one explicit, exclusive owner of normal VN control.
2. Express only cross-system policy: dialogue advance, Auto, Skip, save/load, Quick Menu and cancellation.
3. Keep normal modal UI behavior intact unless a later, separately scoped migration needs it.
4. Fail closed for blocking special modes: no accidental advance, save/load or overlay opening.
5. Keep the solution scene-local and small enough to test without a dependency-injection or global-pause framework.

## Non-Goals

- Implementing a map, hotspot, QTE, timed beat, phone/chat, puzzle or mini-game.
- Putting mini-game logic, timers, success/fail routes or authored content into the coordinator.
- Replacing every existing popup with a global state machine.
- Changing Unity scenes, `SaveData`, `SaveManager` persistence format, `VNDialogueController` behavior, or runtime UI in this policy task.
- A dynamic, partially disabled Quick Menu, input rebinding, a global `Time.timeScale` pause, or a new persistent manager.

## Modal vs Special Mode

A **normal modal UI** is a short-lived VN shell panel that exposes existing VN data or confirms an existing action: History, Settings, Manual Save/Load and its nested confirmations, or the return-to-main-menu confirmation. Its detailed UI lifecycle remains owned by that panel.

An **authored special mode** is an interaction that temporarily owns the player's primary input and completion/cancel route: interactive map, clickable hotspot layer, QTE, timed narrative beat, phone/chat, puzzle or later mini-game. It may have its own UI and state, but it must declare its cross-system permissions before it starts.

The ordinary choice panel is neither. It is a required branch in the dialogue flow, remains owned by `VNDialogueController`, and `ChoiceCondition` remains unrelated to this policy.

## Ownership Model

There may be at most one active **exclusive special-mode owner** in a gameplay scene. The coordinator has the conceptual state `None` or `SpecialMode`; existing normal panels are not registered as coordinator owners in Phase 1.

Before entering a special mode, the coordinator rejects the request when:

- another special-mode lease is active;
- the ordinary choice screen is showing;
- any current normal blocking UI is open; or
- the proposed owner or policy is invalid.

A rejected entry changes nothing and emits a diagnostic naming the attempted owner and the blocking condition. A duplicate entry from the same owner is also rejected rather than silently returning the current lease: idempotence would conceal an ownership bug.

A successful entry returns an opaque lease/token bound to both the owner reference and a monotonically increasing generation. Only that exact lease may call `Exit`. A wrong or stale lease is rejected, logged and leaves the real owner intact. Mode names are diagnostic metadata only, never authorization keys.

Normal modals stay outside the coordinator in Phase 1. Conversely, all future entry points for normal blocking UI must ask the coordinator before opening while a special owner is active. A special mode therefore cannot be covered by History, Settings, Save/Load or the return confirmation unless a later policy explicitly implements that interaction.

A confirmation belonging to an existing normal modal remains nested inside that modal. A special mode that needs its own confirm UI keeps it inside the same special owner; it does not create a second owner. The coordinator never opens a confirmation itself.

## Capability Model

The future coordinator uses one small typed `SpecialModePolicy`, not mode-name checks such as `isQte || isMap || isChat`.

| Capability | Meaning when `true` |
|---|---|
| `BlocksDialogueAdvance` | Normal click/advance and typewriter completion cannot move dialogue while the lease is active. |
| `BlocksAuto` | Auto has no active progression timer. |
| `BlocksSkip` | Skip retains its selected runtime state but cannot complete text or advance lines. |
| `AllowsSave` | Manual save, quick save and ordinary autosave may be requested. |
| `AllowsLoad` | Manual load and quick load may be requested. |
| `AllowsQuickMenu` | The Quick Menu may be shown. Phase 1 has no partially enabled Quick Menu. |
| `AllowsBacklog` | History may be opened. |
| `AllowsSettings` | VN Settings may be opened. |
| `AllowsEscapeCancel` | Escape asks the active special owner to cancel. |
| `AllowsReturnToMainMenu` | A user-initiated Main Menu exit may be requested after the owner has safely released its lease. |

`BlockingExclusive` is the only Phase-1 policy preset: it blocks dialogue, Auto and Skip and sets every `Allows...` capability to `false`. It is the default for all future special modes. The mode must explicitly opt in to a capability; omitted/unknown serialized or constructed values fail closed.

`AllowsSave` or `AllowsLoad` is not a convenience switch. A mode may opt in only after it owns a reviewed serializable-state and restore contract for its own progress. No Phase-1 mode may opt in.

## Dialogue Advance

While an active policy blocks dialogue advance, `AdvanceDialogue()` must return before it mutates typewriter, line, choice or scene state. The normal dialogue UI remains visually frozen at the last safe beat; the special mode is responsible only for completing, cancelling or failing itself and then releasing its lease.

`TryEnter` rejects a visible ordinary choice. It never hides, selects, restores or re-evaluates that choice. On exit, normal dialogue controls return only after the coordinator reports no active owner.

## Auto / Skip

### Auto

Entry into a blocking special mode immediately stops the current Auto coroutine. `CanAutoAdvance()` must include the coordinator query. The existing coroutine already uses realtime and resets its full delay after an advance blocker disappears; the special-mode integration must preserve that behavior:

- no Auto delay accrues while the mode is active;
- no previously elapsed delay fires immediately on exit;
- after a successful exit, Auto starts one new full configured delay from the still displayed line only when Auto is still enabled and normal advance is otherwise legal.

The coordinator does not modify the persisted `SettingsManager` Auto preference.

### Skip

The selected runtime `skipEnabled` state is preserved, but blocking special-mode entry stops the active Skip coroutine. It does **not** call `SetSkip(false)`, complete typing or advance a line. After exit, the existing skip-start predicate decides whether to resume: Skip must still be enabled, the current line must be legal for the configured seen/all-text mode, and no choice/end/modal blocker may remain. This preserves the player's currently selected Skip state while pausing its active progression.

## Save / Load

Normal VN, including an ordinary displayed choice, keeps its current save behavior. The coordinator does not retroactively make choices or normal panels special modes.

A blocking special mode denies Manual Save, Quick Save, ordinary autosave, Manual Load and Quick Load by default. Hotkeys and Quick Menu actions must use the same high-level permission query; they must not bypass it.

For a future explicit opt-in mode:

1. the mode first supplies its own reviewed serializable state and restoration lifecycle;
2. the relevant UI/controller request verifies the coordinator capability before calling the existing save/load path;
3. a permitted load releases/cleans the active special lease before `SaveManager` applies state or routes a scene.

`SaveManager` remains the storage/backend authority and requires no Phase-1 coordinator change. The existing pre-load autosave remains unchanged; because blocking modes deny load, it is not reached from their UI flow. The policy must not use pre-load autosave as a way around a denied load.

## Quick Menu

For a blocking exclusive special mode, the Phase-1 Quick Menu is **hidden**. This is safer than showing a menu with individually disabled controls and avoids a new dynamic-permission UI.

No Phase-1 special policy enables `AllowsQuickMenu`. If a later mode needs it, that implementation must gate every exposed action through the same capability queries and add focused UI tests; merely making the root visible is insufficient. Direct Quick Menu button callbacks, hotkeys and public controller methods must share the gate.

## Escape / Cancel

Phase-1 preserves the existing Manual Save/Load confirmation-first ownership. The unified contract for a user Escape is:

1. nested confirmation owned by the active normal modal;
2. active normal modal according to its existing close behavior;
3. active special mode only when `AllowsEscapeCancel` is true; the request is delivered to that owner, which must exit or report its own controlled refusal;
4. otherwise, existing normal VN behavior.

A special owner is never silently discarded by Escape. A QTE can be non-cancellable; a map can declare cancellation only when its authored return path is safe. Since `TryEnter` and normal modal entry are mutually exclusive, an open normal modal and special owner cannot compete for Escape in the supported model.

## Scene Transition and Cleanup

Coordinator state is transient and may not cross a scene boundary.

- **Normal completion/cancel:** the owner releases its own valid lease in `finally` after it has stopped its mode-local work.
- **Load:** a permitted load performs host cleanup before save state is applied; a denied load changes nothing.
- **Main Menu:** it is denied by `BlockingExclusive`. A future allowed exit first asks the mode to complete/cancel, verifies lease release, then enters the existing confirmation/return flow; it never places that confirmation above an active mode.
- **Scene transition/unload:** `VNDialogueController` force-clears its owned coordinator with a warning and no later owner callback. Scene-local UI is being destroyed, so stale blocking state must not survive.
- **Exception:** the mode integration owns a `try/finally` release. The host additionally force-clears the lease with an error diagnostic when an exception escapes the mode boundary.
- **Destroyed owner:** coordinator queries detect a destroyed `UnityEngine.Object` owner, log it and force-clear the lease. A mode should still release proactively from its own `OnDisable`/teardown.

After every cleanup path, the coordinator state is `None`; no static or `DontDestroyOnLoad` reference exists.

## Proposed API

The following is an API shape, not code for this task. Names can follow the established C# style at implementation time, but the ownership and fail-closed semantics are required.

```csharp
public sealed class SpecialModeCoordinator
{
    public bool HasActiveOwner { get; }
    public bool IsDialogueAdvanceBlocked { get; }
    public bool IsAutoBlocked { get; }
    public bool IsSkipBlocked { get; }
    public bool CanSave { get; }
    public bool CanLoad { get; }
    public bool CanOpenQuickMenu { get; }
    public bool CanOpenBacklog { get; }
    public bool CanOpenSettings { get; }

    public bool TryEnter(
        UnityEngine.Object owner,
        SpecialModePolicy policy,
        out SpecialModeLease lease);
    public bool Exit(SpecialModeLease lease);
    public bool TryRequestEscapeCancel();
    public void ForceClearForHostLifecycle(string reason);
}
```

`SpecialModeLease` is opaque and includes the private generation/owner identity required by `Exit`; callers cannot construct a valid lease. `ForceClearForHostLifecycle` is for the `VNDialogueController` host only, not an ordinary mode API. `TryRequestEscapeCancel()` calls a typed callback/interface supplied by the active owner; it returns whether that owner accepted the request and never releases a lease on the coordinator's own initiative.

The controller exposes narrow high-level queries to existing UI instead of exposing coordinator internals. All user-facing routes call those queries before acting.

## Lifetime / Ownership Location

Select **VNDialogueController-owned, scene-local coordinator**.

The future `SpecialModeCoordinator` is a plain C# object created and held by `VNDialogueController`, not a `MonoBehaviour`, static service, `DontDestroyOnLoad` object or additional manager GameObject. This matches the fact that special modes belong to VN gameplay, naturally discards state on scene destruction, keeps Main Menu free of gameplay state, and avoids manager proliferation.

Future mode UI receives its lease through a narrow controller entry method or an explicit reference supplied by its VN host. It does not find a global singleton.

## Existing Modal Migration Scope

Phase 1 does **not** register History, Settings, Manual Save/Load, return confirmation or the ordinary choice panel as coordinator owners. Their visual hierarchy, nested confirmation behavior and existing local state remain unchanged.

Phase 1 adds only the bridge needed to make the future special owner exclusive:

- special entry rejects existing choice/normal blockers;
- dialogue advance, Auto, Skip, hotkeys and Quick Menu actions query the active special policy;
- normal panel entry points reject requests while a blocking special owner is active;
- scene/load/Main Menu host paths clear an active owner safely.

This is intentionally a one-way compatibility boundary. A later modal-unification task may replace duplicated normal-panel predicates only after the current panel behavior has dedicated regression coverage.

## Save Compatibility

`SaveData` v3 is unchanged. The coordinator contains only transient ownership and permissions, so the coordinator itself requires **no `SaveData` v4**.

A save taken outside a special mode restores ordinary VN state exactly as today. A mid-mode save is unsupported until an individual mode has a separately approved persisted data contract, schema decision, migration strategy and load cleanup rule. Such a future mode may require a schema change; that decision belongs to that mode, not to this coordinator policy.

## Failure Handling

| Failure | Required result |
|---|---|
| Duplicate `TryEnter` | Reject, log owner/policy context, preserve current state. |
| Enter while another owner or normal blocker is active | Reject, log blocker, do not stack UI or alter timers. |
| Wrong/stale `Exit` | Reject and log; keep the actual owner locked. |
| Owner destroyed unexpectedly | Detect on coordinator query/host cleanup, log warning and clear to `None`. |
| Exception in mode integration | Log error and force-clear in a host-safe `finally` path. |
| Scene unload, Main Menu or permitted load | Force-clear before the old scene/state is discarded. |
| Unknown policy/capability data | Fail closed as `BlockingExclusive`; log a diagnostic. |

Diagnostics use clear prefixes such as `[SPECIAL MODE]` and include the owner type, lease generation and cleanup reason. They must never silently unlock normal VN control or silently leave it blocked.

## Future Integration Examples

### Map

A map enters with `BlockingExclusive`, disables normal VN controls and owns its own hotspots. It can declare `AllowsEscapeCancel` only if closing the map returns to the same stable dialogue beat. It cannot save/load or show Quick Menu in Phase 1.

### QTE

A QTE enters with `BlockingExclusive`; it owns timing, success/failure and retry locally. Escape and Main Menu are denied when interruption would invalidate the beat. Its completion/failure route releases the lease before it requests the next authored VN action.

### Chat / Phone

A phone/chat view enters with `BlockingExclusive` and renders its own navigation. A later read-only phone screen may propose a narrower policy only after the user-facing conflict rules and tests are implemented; it does not gain permissions from its display name.

### Puzzle

A puzzle enters with `BlockingExclusive`, owns all puzzle state and releases the lease on solve, controlled cancel or failure. Mid-puzzle save/load stays denied until that puzzle defines and verifies its own serializable state.

## Implementation Scope

This task changes documentation only. The future minimal implementation task should add:

- `Assets/HowIFall/Scripts/VN/SpecialModeCoordinator.cs` — scene-local coordinator, typed policy and opaque lease.
- `Assets/HowIFall/Editor/SpecialModeCoordinatorSmokeTests.cs` — focused non-UI coordinator tests.

It should make focused changes to:

- `Assets/HowIFall/Scripts/VN/VNDialogueController.cs` — own the coordinator; gate advance, Auto, Skip, hotkeys, History/Settings/return entry and lifecycle cleanup.
- `Assets/HowIFall/Scripts/UI/VNQuickMenu.cs` — hide/gate the root and callbacks through controller permissions.
- `Assets/HowIFall/Scripts/UI/ManualSaveLoadPanel.cs` — reject save/load/quick-load opening while the controller reports the active special policy denies it; preserve its confirmation-first Escape behavior.
- `Assets/HowIFall/Editor/HowIFallCiSmokeTests.cs` — include the focused suite.

`SaveManager.cs`, `SaveData.cs`, Unity scenes, dialogue assets, choice data and settings panels are intentionally unchanged in the minimal phase. `SaveManager` stays unchanged because the controller/UI permission layer owns user-initiated access. Do not implement map/QTE/chat/puzzle content in the coordinator task.

## Test Plan

Do not execute this plan in the policy task. The implementation task must add smoke coverage for:

1. no owner: all coordinator queries preserve normal VN permissions;
2. `BlockingExclusive`: dialogue, Auto, Skip, save, load, Quick Menu, Backlog, Settings, Escape and Main Menu requests are blocked as specified;
3. entry rejects an open choice and each existing normal blocker;
4. entry rejects duplicate and competing owners without changing the first lease;
5. only the exact active lease exits; stale/wrong exit leaves the active owner intact;
6. Auto stops on entry and restarts only after a new full delay on exit while its setting remains enabled;
7. Skip stops progression but retains `skipEnabled`, then resumes only through normal existing eligibility after exit;
8. hotkeys and every Quick Menu callback cannot bypass denied permissions;
9. Manual Save/Load and Quick Load preserve their current nested Escape behavior outside special modes and reject opening during one;
10. permitted cleanup on load, Main Menu, scene destruction and owner destruction leaves `HasActiveOwner == false`;
11. exception and unknown-policy paths fail closed and log a `[SPECIAL MODE]` diagnostic;
12. existing Auto, Skip, Save backend, input-map and conditional-choice smoke suites still pass.

## Risks

- Gating only a visual button and not its hotkey/public method would create a save/load or input bypass.
- Treating an ordinary choice as a special mode could break its existing source-index save/restore contract.
- Making the coordinator persistent/static would risk a stale owner after load or Main Menu.
- Silently accepting duplicate entry or wrong exit would hide ownership bugs until a soft-lock occurs.
- A mode that opts into save/load without its own serialized state would restore an invalid interaction.
- Replacing all normal modal state at once would risk the confirmed Manual Save/Load confirmation-first Escape behavior.

## Acceptance Criteria

- One selected model: scene-local `VNDialogueController`-owned coordinator with one exclusive special owner and an opaque lease.
- Normal modal UI and ordinary dialogue choices remain outside coordinator ownership in Phase 1.
- `BlockingExclusive` is explicit and fail-closed; no special mode has implicit Save/Load or Quick Menu access.
- Auto starts a fresh delay after exit; Skip pauses without clearing its active runtime selection.
- `SaveData` remains v3 and `SaveManager` has no Phase-1 change.
- Entry, exit, destruction, exception, load and scene-transition cleanup cannot leave a stale blocker.
- A bounded implementation file list and smoke-test plan exist, while map/QTE/chat/puzzle implementation remains deferred.
