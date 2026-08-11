# Chat / Phone Technical Policy

## Current State

On current `origin/master` after `75814ac` and `178dcee`, How I Fall has no Chat/Phone runtime or scene-data format. The existing audit shows useful chat behavior in the Eternum reference, but requires typed conditions/effects, bounded media/contact scope, and explicit return routing. Ren'Py labels, screens, strings, code, and content are not transferred.

Current relevant HIF contracts are:

- `DialogueSceneData` with direct VN scene references;
- `DialogueChoice` with `ChoiceCondition`, nine numeric delta fields, and `nextScene`;
- `GameState` with nine numeric state values and typed read/apply paths;
- `VNDialogueController` owning normal VN routing, Auto/Skip, Backlog, and save/load entry points;
- `SpecialModeCoordinator.BlockingExclusive` for one active authored interaction;
- `TimedNarrativeBeatController` as the existing exactly-once terminal-routing pattern;
- `ReplaySession` and `SceneFlowManager` with isolated replay state and fail-closed save/load;
- `SaveData` v3 and save-scoped normal VN Backlog.

## Content Boundary

This is a functional contract only. The narrative, names, contacts, messages, and images are created by a separate team. Any V1 fixture must be marked `TECH DEMO ONLY / NOT CANON` and use only `TEST CONTACT`, `TEST: incoming message`, `TEST: reply A`, `TEST: reply B`, and a neutral technical placeholder.

## Goals

V1 must support a separate phone/chat screen, a contact display name, ordered text and image entries, Incoming and Player visual sides, one optional image entry, exactly two response choices, typed conditions, typed GameState effects, stable-ID branching, and exactly-once deterministic return to an authored VN scene.

## Non-Goals

No Phone OS, app/contact lists, calls, voice, video, persistent messenger history, unread badges, notification subsystem, generic attachments, emoji/stickers, audio/video/file framework, or general scripting language.

## Chat Scene Data

The authored asset is `ChatSceneData : ScriptableObject`:

- `string chatId` - non-empty stable ID, unique in the registry;
- `string contactDisplayName` - display name only, with no Character ID or contact database integration;
- `List<ChatEntry> entries` - ordered closed typed entries;
- `DialogueSceneData returnScene` - required concrete VN return scene.

`returnScene` is not an optional fallback. An asset without it cannot start. IDs, payloads, targets, and scene registration are validated before the first entry is shown. Chat data adds no state to `GameState` or `SaveData`.

`ChatEntry` is a closed V1 model with only `Text`, `Image`, and `Choice`. No dictionary/object payloads, string command names, reflection, eval, arbitrary callbacks, or runtime extension points are allowed. The implementation may use a Unity-serializable discriminated model or sealed typed payloads, but validation must reject mismatched or incomplete payloads.

## Entry Types

### Text

`ChatTextEntry` contains `entryId`, `ChatSenderSide sender` with only `Incoming` or `Player`, and required non-empty `text`. It advances to the next ordered entry and has no command or state action.

### Image

`ChatImageEntry` contains `entryId`, `sender`, and one `Sprite image`. A ChatSceneData allows at most one Image entry. It uses normal bubble-side presentation. A fullscreen viewer is not part of V1.

### Choice

`ChatChoiceEntry` contains `entryId`, exactly two authored response options, and optional `fallbackEntryId` for the case where both options are unavailable. Each option contains:

- required `string text`;
- optional `List<ChoiceCondition> conditions`;
- optional typed `ChatGameStateDelta effects`, defaulting to zero;
- optional `string nextEntryId`, a stable target in the same ChatSceneData.

An empty `nextEntryId` means terminal chat. The selected response is shown in the transient transcript as an outgoing `Player` bubble. It is not a normal VN Backlog entry. V1 has no `resultText` or `nextScene` field on a chat option; return is controlled only by `ChatSceneData.returnScene`.

## Message Contract

`Incoming` and `Player` are visual sides, not full character identities. The contact is represented only by `contactDisplayName`. CharacterData, portraits registry, and canonical identity remain out of scope. The ordered list is deterministic. The controller must not invent hidden messages or replay an entry after a branch.

## Media Contract

V1 supports only an optional `Sprite image` in one `Image` entry. A null image is invalid during validation. If invalid data reaches runtime, it must not crash or render a broken empty UI: record a diagnostic, apply no effect, and fail safely through `returnScene`. No audio, video, file, or generic attachment abstraction is designed.

## Choice Conditions / Effects

Reuse the existing `ChoiceCondition` and `ConditionalChoiceEvaluator`. An empty condition list is available; multiple conditions use AND; null or unsupported conditions make the option unavailable. V1 uses the same nine `ChoiceStateValue` members: `Lust`, `Romance`, `Purity`, `Corruption`, `SelfControl`, `Suspicion`, `TrustMasha`, `TrustArtem`, and `LeraInterest`.

`ChatGameStateDelta` is a closed typed delta with the same nine numeric fields and sign semantics as current `DialogueChoice` and `GameState.ApplyChoice`. Field names cannot be supplied as strings. Implementation should share the typed mapping/application path or extract a shared typed helper, not create a second condition system or use reflection.

An option target is preflight-validated before its effects are applied. An invalid or unavailable option never changes GameState.

## Branching

Stable `entryId` is the only target mechanism. `nextEntryId` points only within the same ChatSceneData. No common label graph or scripting language is created.

- Empty target on a Choice is terminal chat.
- Missing target records a diagnostic, applies no effect, and fails safely through `returnScene` exactly once.
- Empty or duplicate entry IDs, and empty or duplicate chat IDs, invalidate the asset and prevent start.
- If all choices are unavailable, a valid `fallbackEntryId` is followed without effects. If no valid fallback exists, record a diagnostic and return safely through `returnScene`.
- Terminal Text, Image, or Choice without a target completes after presentation or selection.
- Cycles are not a general feature. Validation must reject malformed graphs that can run forever and require an authored terminal path for any intentionally reachable loop.

## Special Mode Ownership

Chat is one authored interactive sequence and uses the existing `SpecialModeCoordinator` with `SpecialModePolicy.BlockingExclusive`. No second coordinator or global modal subsystem is created.

While active, the ChatController owns the lease and the following are exact policy: normal dialogue advance blocked; Auto and Skip blocked with current timers stopped; Save, Quick Save, Auto Save, pre-load save, Load, Quick Load, and Continue blocked in controller and `SaveManager` paths; Backlog, Settings, Quick Menu, and Main Menu blocked; Escape is a no-op because cancellation is not allowed.

Only the chat screen accepts its own input. Destroy or disable must release the lease without random routing. A second completion after cleanup is rejected.

## Save / Load

Chat mid-state is not saved in V1. `SaveData.CurrentVersion` remains `3`. ChatSceneData, active entry, transcript, branch target, and chat flags are not added to SaveData. While the lease is active, all manual, quick, auto, pre-load, and load operations are denied. This requires both UI/controller gates and backend `SaveManager` guards.

## Backlog / Read History

Chat uses a separate transient in-memory transcript for the active chat. It is not added to normal `DialogueBacklog`, not included in save-scoped snapshots, and does not affect campaign `DialogueReadHistory`. It is cleared on success, failure, and return. Replay-local read history is not used.

## Replay

`StartChat` is rejected while `SceneFlowManager.IsReplayModeActive`. Chat is unavailable during Gallery Replay. `ReplaySession`, its snapshot, and its context are unchanged; no chat replay state is introduced.

## Return To VN

The only flow is:

`VN -> StartChat(ChatSceneData) -> active chat -> terminal entry or selection -> mark complete -> release BlockingExclusive lease -> route to returnScene -> normal VN resumes`.

Completion has an idempotent guard and happens exactly once. The lease is released before `VNDialogueController.TryRouteToScene(returnScene)`, so the special-mode gate cannot block return. The route always uses authored `returnScene`, never a random current line. Invalid or unregistered routing performs controlled cleanup and one diagnostic without leaving VN in active special mode.

## Technical Demo

The future fixture contains only `TEST CONTACT`, `TEST: incoming message`, one optional Image with a neutral technical placeholder, and `TEST: reply A` / `TEST: reply B`. It is marked `TECH DEMO ONLY / NOT CANON`. No real names, messages, images, or story branches are added.

## Runtime Scope

Minimal future implementation scope:

- `ChatSceneData.cs` - asset and closed authoring validation;
- `ChatEntry.cs` - typed entry, payload, condition, effect, and target contract;
- `ChatController.cs` - transient transcript, input, lease, branch, and exactly-once completion;
- one phone/chat screen wiring;
- narrow integration changes in `VNDialogueController`, `VNQuickMenu` or input gates, `SaveManager`, and the replay guard.

Do not create `PhoneManager`, `ChatManager`, `MessageManager`, or `ContactManager` singleton systems. This policy task changes no Unity scenes, Assets, SaveData, or runtime code.

## Validation

Before start, validate non-empty and unique `chatId`, non-empty and unique entry IDs, exactly two choices, non-empty choice text, valid sender, matching payload kind, non-null image, valid registered `returnScene`, same-asset targets, at most one Image entry, and no duplicate IDs. Runtime repeats critical guards and fails closed.

## Tests

Future implementation must cover unique chatId and entry IDs; incoming/outgoing text; image and null-image safety; typed conditions/effects; hidden responses; branch target; all-hidden fallback and fail-safe; BlockingExclusive ownership and second-owner rejection; Auto/Skip/save/load blocking including backend guards; exactly-once completion; correct authored return; invalid target safety; transcript isolation from Backlog and DialogueReadHistory; Replay denial without ReplaySession changes; `SaveData.CurrentVersion == 3`; and absence of chat runtime state in SaveData.

## Risks

The main risks are a parallel condition/effect system, UI-only save denial, routing before lease release, and treating transient transcript as campaign history. Shared typed condition/effect paths, defense-in-depth save gates, idempotent cleanup, and isolation tests address these risks.

## Acceptance Criteria

1. The V1 model is closed and typed, with no dictionary, string command, reflection, eval, or callback mechanism.
2. Conditions reuse `ChoiceCondition`; effects are limited to typed numeric GameState deltas.
3. Chat owns `BlockingExclusive` and blocks Auto, Skip, save/load, Backlog, Quick Menu, Settings, and Escape as specified.
4. Replay denial, SaveData v3, and transcript/read-history isolation are fixed.
5. Terminal completion exactly once releases the lease and routes to a concrete `returnScene`.
6. Scope remains minimal and creates no singleton manager family.
7. The tracker has exactly one next step: `Implement Chat / Phone technical foundation`.
