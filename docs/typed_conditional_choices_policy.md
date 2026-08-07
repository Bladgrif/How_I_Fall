# Typed Conditional Choices Policy

## Current Behavior

The reviewed baseline is `origin/master` after `9d7be27db11ffcdabc6bb2ec56845440c6647b2f` and `1aa2cdaf6814471492c3416871a45f887a813115`.

- `DialogueChoice` currently contains text, result text, stat deltas and an optional `nextScene`; it has no availability data.
- `DialogueSceneData.choices` is the immutable authored source list for a scene. `VNDialogueController.ShowChoices()` currently displays its first choices by the same indices as the three `choiceButtons`, and `Choose(int)` writes that index to `GameState.selectedChoiceIndex`.
- `RememberChoicePosition()` stores the last line before a choice. `RestoreFromGameState()` and its choice-result preflight validate `selectedChoiceIndex` against the source `restoredScene.choices` list and validate the resolved target.
- `GameState` already owns the nine persisted integer values: `lust`, `romance`, `purity`, `corruptionLevel`, `selfControl`, `suspicion`, `trustMasha`, `trustArtem`, and `leraInterest`. There is no generic story-flag store.
- `SaveData` v3 already serializes all nine values together with the selected **source** choice index, choice-result state and pending target. `SaveManager` restores them before dialogue restore.
- `DialogueContentValidator` validates dialogue assets and transitions; `HowIFallProjectValidator` validates the three assigned VN choice buttons. The classroom demo currently has two ordinary choices; the UI test scene has three.
- Auto stops at a choice because `CanAutoAdvance()` requires `!showingChoice`. Skip opens a choice but does not select it; `skipAfterChoices` resumes only after a manual selection.

## Goals

1. Let an authored `DialogueChoice` be shown only when the current `GameState` satisfies typed, serializable numeric conditions.
2. Keep existing choice assets available without migration.
3. Preserve source indices for selection, save/load and result restoration even when the displayed list is filtered.
4. Keep authoring deterministic, inspectable and editor-validatable.

## Non-Goals

- `eval`, `exec`, C# expression strings, reflection-by-field-name, `Dictionary<string, object>`, arbitrary callbacks or script hooks.
- Boolean story flags, resources, inventory, unlock state, OR groups, nested expressions and per-choice disabled-reason UI.
- Changing `SaveData` to v4, mutating current dialogue assets, adding scenes/assets, or auto-selecting a sole available choice.

## Data Model Options

| Option | Decision | Reason |
|---|---|---|
| String expressions or callbacks | Rejected | Unsafe, opaque in Inspector and not statically validatable. |
| Untyped key/value condition data | Rejected | Reintroduces misspelled keys and runtime type ambiguity. |
| One typed condition on each choice | Rejected | Cannot express the required combined stat gates. |
| Typed `List<ChoiceCondition>` with all conditions required | Selected | Unity-serializable, readable and sufficient for V1. |

## Decision

V1 adds a serializable `ChoiceCondition` DTO to `DialogueChoice` through a new list field, conceptually:

```csharp
[Serializable]
public sealed class ChoiceCondition
{
    public ChoiceStateValue stateValue;
    public ChoiceComparisonOperator comparison;
    public int threshold;
}

public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
```

`ChoiceStateValue` and `ChoiceComparisonOperator` are closed enums. A `GameState` switch maps the selected enum to its current integer; it must not use reflection or string field names. The evaluator returns availability only and never changes state.

A missing or empty `conditions` list means **always available**. This is both the V1 authoring rule and legacy compatibility rule: existing assets need no data migration.

## Supported State Values

`ChoiceStateValue` contains exactly the existing persisted numeric state:

| Enum value | `GameState` value |
|---|---|
| `Lust` | `lust` |
| `Romance` | `romance` |
| `Purity` | `purity` |
| `Corruption` | `corruptionLevel` |
| `SelfControl` | `selfControl` |
| `Suspicion` | `suspicion` |
| `TrustMasha` | `trustMasha` |
| `TrustArtem` | `trustArtem` |
| `LeraInterest` | `leraInterest` |

Conditions read the value at the moment availability is evaluated, before a new choice's deltas are applied.

## Comparison Operators

V1 supports the smallest useful set for integer thresholds:

- `Equal`
- `GreaterOrEqual`
- `LessOrEqual`

`GreaterThan` and `LessThan` are omitted: authors can express normal integer gates with an adjacent inclusive threshold. `NotEqual` is omitted because its useful general form needs an OR expression, which V1 deliberately does not have. Operators are enum values, never text entered by an author.

## Multiple Condition Semantics

A choice is available when **all** of its conditions are true (AND-only). Evaluation short-circuits on the first false or invalid condition. There are no OR groups, negation, nesting or precedence rules in V1.

Example: a choice with `Romance >= 3` and `Suspicion <= 2` is shown only when both comparisons pass.

## Hidden / Disabled UX

Unavailable choices are **hidden**. They do not reserve a button slot, are not dimmed and reveal neither their text nor their requirements to the player.

This fits the mystery/detective tone, keeps hidden consequences hidden, and matches the current UI, which has no requirement-reason presentation. Disabled choices and reason UX are explicitly out of scope for V1.

## Source Index vs Display Index

The authored `DialogueSceneData.choices` list is the source of truth and is never filtered or mutated. At `ShowChoices`, the controller builds a transient ordered list such as:

```csharp
VisibleChoice
{
    DialogueChoice choice;
    int sourceChoiceIndex;
}
```

For source choices `[hidden, visible, visible]`, displayed button slots map to source indices `[1, 2]`. Button callbacks pass the displayed slot to the transient list, and `Choose` resolves it to the original source choice before applying deltas, resolving the target and storing state.

`GameState.selectedChoiceIndex` always stores the **source choice index**. A display/filtered index must never be saved, restored or used as a target-validation index.

## Evaluation Timing

- Evaluate availability every time `ShowChoices()` is entered, including a restored choice screen.
- Preserve source order among available choices.
- Do not cache evaluated booleans in `GameState`, `SaveData` or a ScriptableObject.
- Build only a transient visible mapping for the current UI display; do not destructively filter `activeChoices` or `DialogueSceneData.choices`.
- The ordinary autosave immediately before the displayed choice captures the already persisted `GameState` position and values; it does not need a visible-choice snapshot.

## Zero Available Choices

If a scene has source choices but evaluation produces zero available choices:

1. If `defaultNextScene` is assigned, clear inactive choice state and transition to it immediately.
2. If it is absent, do not open an empty choice panel or leave input soft-locked. Log a content error and enter a controlled terminal/error presentation with a generic player-safe message.

The editor validator cannot prove that arbitrary runtime stat conditions will become true. Therefore a scene with conditional choices and no `defaultNextScene` receives a warning: it relies on at least one condition being available at runtime. It is not a proof of reachability.

## Save / Load Semantics

No `SaveData` version bump is required. Numeric availability is derived only from fields already persisted in v3.

- Manual save, ordinary autosave before choices, load before the choice screen, and Continue restore `GameState` first, then re-evaluate availability when displaying choices.
- Do not serialize visible choices, button slots, mapping data or evaluated condition results.
- `selectedChoiceIndex` remains a source index and remains validated against the unfiltered scene choice list.

## Choice Result Restore

When `choiceResultActive` is `true`, restoration validates the saved source index and the exact configured target as it does now, then restores the result beat. It **must not** re-evaluate whether that choice would be available in a newly built visible list.

Conditions gate a new player selection only. They never invalidate a choice that was already selected and saved.

## Auto / Skip Semantics

Conditional choices keep the current guarantees:

- Auto stops at any displayed choice.
- Skip can reach the choice screen but never selects an option.
- One available choice still requires a manual click.
- `skipAfterChoices` may resume only after that manual selection.

For zero available choices, the deterministic fallback in the preceding section applies instead of an automatic choice selection.

## Validation Rules

Future `DialogueContentValidator` and project validation must report errors for:

- null `DialogueChoice` and existing empty text/invalid target cases;
- a null condition entry;
- an unknown `ChoiceStateValue` enum value;
- an unknown or unsupported comparison enum value;
- a source choice count greater than the number of assigned usable choice buttons;
- a runtime available count greater than usable button capacity.

The asset-level capacity rule is intentionally conservative: without solving author-defined state constraints, `choices.Count` is the only safe upper bound for all possible visible choices. At runtime, hidden choices consume no capacity; if the actual available count still exceeds capacity, log an error and fail safely rather than silently dropping options.

Malformed or forward-incompatible serialized condition data is unavailable at runtime: the evaluator logs a diagnostic and treats that condition as `false`. Invalid data must never evaluate to `true` and must never cause an arbitrary selection. The validator reports it as an error so it is fixed before release.

V1 imposes no arbitrary condition-list length limit. Inspector readability is handled by review; a limit may be added only when a concrete authoring problem appears.

## Future Story Flags

Do not add a generic string-key flag framework now. When the story schema has real boolean flags, add a separate typed flag enum and a typed flag-condition DTO/evaluator branch. Numeric `ChoiceCondition` remains unchanged; a future availability evaluator combines supported typed condition families with the same AND rule.

This extends the design without forcing string keys, reflection or a rewrite of existing numeric conditions.

## Implementation Scope

This task changes documentation only. The future implementation should make focused changes to:

- `Assets/HowIFall/Scripts/VN/DialogueChoice.cs` — add the conditions list.
- `Assets/HowIFall/Scripts/VN/ChoiceCondition.cs` — new serializable DTO and enums.
- `Assets/HowIFall/Scripts/Core/GameState.cs` — typed state-value lookup/evaluation helper.
- `Assets/HowIFall/Scripts/VN/VNDialogueController.cs` — transient visible mapping, source-index selection, zero/capacity fallback and restore-safe display.
- `Assets/HowIFall/Editor/DialogueContentValidator.cs` — condition and conservative content-capacity validation.
- `Assets/HowIFall/Editor/HowIFallProjectValidator.cs` — expose/validate usable UI choice capacity as needed.
- a focused new conditional-choice smoke-test file and `Assets/HowIFall/Editor/HowIFallCiSmokeTests.cs` — evaluator, mapping, restore and failure-path coverage.

`SaveData.cs` and `SaveManager.cs` require no code or schema change for numeric V1 conditions; their v3 persisted state is sufficient.

## Test Plan

Do not execute this plan in the policy task. The implementation task must add focused smoke coverage for:

1. empty/missing conditions remain visible for legacy assets;
2. each supported operator and every supported state value;
3. AND semantics and short-circuit false behavior;
4. three source choices: `0` always available, `1` `Romance >= 3`, `2` `Suspicion <= 2`; assert source order, hidden filtering and button-slot-to-source mapping;
5. selected source index, applied deltas and target resolution after choosing a filtered option;
6. save/load before a choice and Continue recompute from restored state without saving display data;
7. `choiceResultActive` restores a saved source choice even if conditions would now fail;
8. zero available choices with and without `defaultNextScene`;
9. asset and runtime UI-capacity failures;
10. null/unknown/malformed conditions fail closed and are validator errors;
11. Auto, Skip and `skipAfterChoices` retain their current manual-choice behavior.

## Risks

- Treating a display index as a source index would corrupt result restore or route to the wrong target.
- Destructive filtering of authored data would create cross-display/save side effects.
- A cached visible list or serialized evaluation result would become stale after load.
- Static validation cannot prove conditional reachability; the default-transition fallback prevents a silent soft-lock.
- Increasing source choice count beyond the three-button UI requires an explicit UI-capacity change, not silent truncation.

## Acceptance Criteria

- One selected model: typed numeric condition DTOs, closed enums and AND-only evaluation.
- V1 supports exactly the nine current numeric `GameState` values and `Equal`, `GreaterOrEqual`, `LessOrEqual`.
- Existing choices with no conditions remain available without asset migration.
- Unavailable choices are hidden; disabled-choice UX is not implemented.
- Displayed slots map to original source indices, and only source indices are saved.
- Availability is recomputed from restored `GameState`; no v4 save schema is introduced.
- Choice-result restore never re-gates an already selected saved choice.
- Zero, invalid and capacity cases fail safely without automatic option selection or silent soft-lock.
- The next implementation task has a bounded file list and test plan.
