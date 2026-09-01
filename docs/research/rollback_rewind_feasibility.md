# How I Fall — feasibility contract Rollback / Rewind

> **Статус:** docs-only reviewer correction на `origin/master` `732ce194bce36181f660f3b90b242b2c3e142cf6`; исходный audit base — `40c7549d7cac6c13b5a2f47089ff3a55a4b79237`.
>
> Этот документ не разрешает production implementation. `SaveData` v3, scenes, prefabs и текущие saves не менялись.

## 1. Executive summary

Для **Polished Functional Demo** рекомендуем **GO WITH LIMITS**: ограниченный in-memory `Rollback` по **предыдущим stable dialogue lines и choice checkpoints**. Это вариант C, но не generic undo, не save-system и не «показать предыдущую строку».

`Rollback` означает: вернуть runtime к последнему доступному стабильному checkpoint той же кампании так, чтобы dialogue position, все девять numeric полей `GameState`, choice-state, текущий `DialogueBacklog` и actual presentation state были согласованы. Если checkpoint находится перед choice, игрок снова видит этот choice и может выбрать другой вариант. Старый effect не остаётся в `GameState` и не может накопиться повторно.

Границы обязательны:

- buffer живёт только в памяти и не попадает в `SaveData`/слоты;
- обычный переход между зарегистрированными `DialogueSceneData` поддерживается только вместе с checkpoint presentation snapshot; это не Unity scene reload;
- любой active special mode, Replay, Load/Continue/New Game/Main Menu и Unity-scene boundary — hard barrier;
- rollback из History, для partial typewriter и визуальных UI side effects не существует;
- Auto и Skip после rollback принудительно останавливаются; checkpoint не восстанавливает их активность.

Такой contract использует уже существующий атомарный набор campaign state и проверенный restore path, но требует отдельной небольшой runtime-реализации и PlayMode покрытия после reviewer approval.

## 2. Current HIF execution model

### Обычная строка

1. `VNDialogueController.Start()` выбирает normal start, pending Save restore или Replay (`Assets/HowIFall/Scripts/VN/VNDialogueController.cs`, `Start`). Для normal start вызывается `LoadDialogueScene(sceneData)`.
2. `LoadDialogueScene` назначает `sceneData`, `activeLines`, `activeChoices`, `currentLineIndex`, очищает transient choice state, записывает `GameState.currentSceneId/currentLineIndex/currentLineId`, пытается восстановить background через `FindLastBackgroundBeforeOrAt` только в target `DialogueSceneData`, затем вызывает `ShowLine`.
3. `ShowLine` сразу помещает raw `speaker/text` в `DialogueBacklog`, запускает `ShowText`/`TypeText` и вызывает `ApplyVisuals`. Строка уже попала в History до завершения typewriter; optional `background`/`characterSprite` могут оставить предыдущую фактическую presentation state.
4. `TypeText` печатает символы realtime. Только по завершении `TypeText` (или через `CompleteTyping`) вызывается `MarkDisplayedLineSeen`; затем может стартовать Auto delay.
5. `AdvanceDialogue` сначала останавливает Auto/Skip timers, затем `ShowNextLine`. Если typewriter ещё активен, первый advance только завершает печать; позиция не меняется.
6. Для следующей обычной строки `ShowNextLine` увеличивает `currentLineIndex`, вызывает `UpdateSavedDialoguePosition`, затем `ShowLine`.

### Choice и переход

1. На последней строке сцены `ShowNextLine` вызывает `ShowChoices` при наличии `activeChoices`.
2. `ShowChoices` строит видимые choices из текущего `GameState`, очищает choice-state, фиксирует позицию последней строки через `RememberChoicePosition`, показывает panel и запрашивает Auto save.
3. `Choose` **сначала** вызывает `GameState.ApplyChoice`, затем записывает `selectedChoiceIndex`, `choiceResultActive`, `pendingNextSceneId`; после этого `ShowFinalLine` показывает `resultText` и `ShowRelationshipCue` запускает transient consequence cue.
4. Следующий `ShowNextLine` после result beat очищает choice state и либо вызывает `LoadDialogueScene(pendingNextScene)`, либо заканчивает demo. Обычный переход между `DialogueSceneData` не загружает Unity scene.

### Save/Load и restore

- `SaveManager.SaveSlot` получает active `sceneId/lineId/index` через `TryGetSavePosition`, копирует `GameState` и `CaptureBacklogSnapshot`, затем валидирует choice-state перед записью.
- `SaveManager.ApplyAndRoute` при gameplay in-place применяет `GameState`, заменяет backlog и вызывает `VNDialogueController.RestoreFromGameState`; при restore error возвращает прежние `GameState` и backlog.
- `RestoreFromGameState` preflight-валидирует scene, line, selected choice и pending transition. Для inactive choice на последней строке с choices `RestoreDialogueDisplay` снова вызывает `ShowChoices(false)`. Для активного result beat `RestoreChoiceResult` не применяет delta повторно.
- `SceneFlowManager.StartNewGame()` очищает pending load и делает `GameState.ResetState()`; `ReturnToMainMenu()` загружает Unity scene `MainMenu`.

### Auto, Skip, special modes

- Auto запускает единственный realtime coroutine только при завершённой строке, без choice/modal/typewriter/Skip. Открытый modal начинает новый полный delay.
- Skip хранится runtime-полем `skipEnabled`, не сериализуется в `SaveData`; он завершает typewriter и никогда не выбирает choice автоматически.
- `TryEnterSpecialMode` выдаёт scene-local lease и останавливает Auto/Skip для blocking policy. Rejected `TryEnterSpecialMode` не меняет runtime mode. `SpecialModeCoordinator` — transient и не сериализуется.

## 3. State mutation map

| Mutation | Concrete current method/file | Значение для rollback |
|---|---|---|
| Active dialogue scene/line | `LoadDialogueScene`, `UpdateSavedDialoguePosition`, `RememberChoicePosition` — `Assets/HowIFall/Scripts/VN/VNDialogueController.cs` | Обязательная позиция checkpoint: stable `sceneId`, `lineId`, `lineIndex`. |
| Visible text/typewriter | `ShowText`, `TypeText`, `CompleteTyping` — `VNDialogueController.cs` | Partial progress transient; checkpoint создаётся только после полного текста. |
| Read/seen | `MarkDisplayedLineSeen` → `DialogueReadHistory.MarkSeen` — `VNDialogueController.cs`, `Assets/HowIFall/Scripts/VN/DialogueReadHistory.cs` | Профильное persistent-множество, не откатывать. |
| Current History | `ShowLine`/`ShowNarration` → `AddToBacklog`; `CaptureBacklogSnapshot`/`ReplaceBacklogFromSnapshot` — `VNDialogueController.cs` | Нужна глубокая snapshot-копия; future entries после rollback исчезают. |
| Numeric state | `GameState.ApplyChoice` → `ApplyChoiceStateDelta` — `Assets/HowIFall/Scripts/Core/GameState.cs` | Девять numeric полей восстанавливаются точно, а не обратными delta. |
| Choice selected/result/target | `Choose`, `ClearChoiceState`, `RestoreChoiceResult` — `VNDialogueController.cs` | `selectedChoiceIndex`, `choiceResultActive`, `pendingNextSceneId` входят в state invariant. |
| Choice UI and conditional availability | `ShowChoices` — `VNDialogueController.cs`; `ConditionalChoiceEvaluator` | Recomputable из restored `GameState` и позиции последней строки. |
| Consequence cue | `Choose` → `ShowRelationshipCue` — `VNDialogueController.cs` | Не snapshot; при rollback отменить/hide. Новый выбор создаёт ровно один новый cue. |
| Background presentation | `ApplyVisuals`; `FindLastBackgroundBeforeOrAt`; `LoadDialogueScene` — `VNDialogueController.cs` | `DialogueLine.background` optional, а fallback ищет только target data-scene. Actual `Sprite`/`enabled`/`color` обязательны в checkpoint. |
| Character presentation | `ApplyVisuals` — `VNDialogueController.cs` | `hideCharacter` действует только explicit line, а null `characterSprite` сохраняет previous state. Actual `Sprite`/`enabled`/`anchoredPosition`/`sizeDelta` обязательны в checkpoint. |
| Music presentation | `ApplySceneAudio` — `VNDialogueController.cs`; `AudioManager.PlayMusic`/`StopMusic` — `Assets/HowIFall/Scripts/Audio/AudioManager.cs` | `backgroundMusic == null` при `stopMusicOnStart == false` carry-over не реконструирует. Actual music clip/wasPlaying обязательны в checkpoint. |
| Ambience | `AudioManager.PlayAmbience`/`StopAmbience` — `AudioManager.cs` | Ordinary `DialogueSceneData` ambience не author'ит; не добавлять в payload без отдельного contract. |
| SFX | `AudioManager.PlaySfx` — `AudioManager.cs` | One-shot feedback не rollback-ить. |
| Auto | `SetAutoForward`, `StartAutoForwardDelayIfReady`, `StopAutoForwardTimer` — `VNDialogueController.cs`; preference — `SettingsManager` | Не сохранять и не восстанавливать как checkpoint state. |
| Skip | `SetSkip`, `StartSkipDelayIfReady`, `StopSkipTimer` — `VNDialogueController.cs` | Не сохранять и не восстанавливать; при rollback выключать. |
| Save slot state | `CaptureGameState`, `ApplyGameState`, `TryApplyInPlace` — `Assets/HowIFall/Scripts/Save/SaveManager.cs` | Удобный reference shape, но не использовать `SaveData` как rollback buffer. |
| Normal scene route | `ShowNextLine`/`LoadDialogueScene` — `VNDialogueController.cs` | SAFE TO SNAPSHOT только с deterministic presentation snapshot. |
| Unity scene route | `StartNewGame`, `OpenLoadedGame`, `ReturnToMainMenu` — `Assets/HowIFall/Scripts/Core/SceneFlowManager.cs` | Hard barrier: runtime host уничтожается/пересоздаётся. |
| Exclusive modes | `TryEnterSpecialMode`/`ExitSpecialMode` — `VNDialogueController.cs`; `SpecialModeCoordinator.cs` | Их transient state не имеет restore contract; hard barrier только после successful lease acquisition. |
## 4. Recommended rollback contract

### Chosen target: C — previous stable line + choice checkpoints

`Rollback` выбирает **предыдущий доступный stable checkpoint**, а не предыдущую запись History.

- Stable normal line — строка, чей typewriter уже полностью завершён. Такой checkpoint включает саму строку в backlog и полностью согласованный `GameState`.
- Перед `GameState.ApplyChoice` обязательно существует/фиксируется checkpoint последней строки с открытым choice. Он возвращает choice UI в inactive choice-state и позволяет новый выбор.
- Choice `resultText` — **не отдельный stable checkpoint**. Это transitional result beat: после rollback с него или с первой строки ветки игрок возвращается к choice checkpoint, а не к визуальному тексту результата. Это сознательно ставит корректность ветки выше пошагового перематывания каждого transient beat.
- При обычной линейной строке B rollback возвращает к предыдущей stable строке A. При пустом buffer действие недоступно.

### Choice guarantees

После rollback перед choice:

1. восстанавливаются все numeric поля `GameState` до pre-choice snapshot;
2. восстанавливаются `selectedChoiceIndex = -1`, `choiceResultActive = false`, `pendingNextSceneId = ""`;
3. `ShowChoices` заново вычисляет conditional availability из восстановленного state;
4. прошлый `relationshipCue` остановлен и скрыт;
5. при новом `Choose` `GameState.ApplyChoice` применяется единожды к восстановленной базе — старый effect не суммируется;
6. History заменяется snapshot, поэтому future `resultText` и реплики старой ветки не остаются видимыми.

`Rollback` нельзя выполнять во время open choice panel только как «закрыть UI»: он обязан применить целый checkpoint либо ничего не делать.

## 5. Checkpoint payload

Внутренний тип должен быть отдельным transient plain-C# payload, не `SaveData` и не JSON. `Sprite`/`AudioClip` references допустимы: buffer не переживает Unity-scene boundary и не сериализуется.

| Field | Статус | Причина |
|---|---|---|
| `currentSceneId`, `currentLineId`, `currentLineIndex` | **REQUIRED** | Идентифицируют точную зарегистрированную dialogue position. |
| `lust`, `romance`, `purity`, `corruptionLevel`, `selfControl`, `suspicion`, `trustMasha`, `trustArtem`, `leraInterest` | **REQUIRED** | Это все существующие mutable numeric axes `GameState`. |
| `selectedChoiceIndex`, `choiceResultActive`, `pendingNextSceneId` | **REQUIRED** | Сохраняют валидный choice invariant; normal checkpoint обычно хранит inactive состояние. |
| Deep copy `DialogueBacklogEntry` (`speaker`, `text`, до текущего 100-entry cap) | **REQUIRED** | Убирает History будущей ветки и сохраняет текущий session History согласованным. |
| Checkpoint kind/ordinal (normal line или choice checkpoint) | **REQUIRED** | Управляет выбором предыдущего checkpoint и исключает result beat. |
| Background: current `Sprite` reference, `enabled`, `color` | **REQUIRED** | Target authored line может не иметь `background`; scene-local fallback не исключает future background. |
| Character: current `Sprite` reference, `enabled`, `RectTransform.anchoredPosition`, `RectTransform.sizeDelta` | **REQUIRED** | Null `characterSprite` и отсутствие `hideCharacter` оставляют previous character presentation. |
| Music: current `AudioClip`, `wasPlaying` | **REQUIRED** | `ApplySceneAudio` не реконструирует carry-over music при null `backgroundMusic`. |
| Choice UI visibility/visible choices | **RECOMPUTABLE** | `ShowChoices` строит UI из линии и restored `GameState`. |
| Full dialogue text | **RECOMPUTABLE** | Берётся из current `DialogueLine`; snapshot не должен дублировать authored content. |
| Typewriter progress/coroutine | **SHOULD NOT RESTORE** | Checkpoint бывает только после complete text; restore показывает target line полностью. |
| Auto timer, Auto preference, Skip enabled/timer | **SHOULD NOT RESTORE** | Это control mode, а не story state; rollback их останавливает. |
| `DialogueReadHistory` / replay-local seen set | **SHOULD NOT RESTORE** | Seen — monotonic profile/replay semantics, не History и не save slot. |
| Ambience | **SHOULD NOT ADD** | Ordinary `DialogueSceneData` его не author'ит. Future ordinary ambience mutation требует отдельного contract update либо barrier. |
| SFX | **SHOULD NOT RESTORE** | One-shot feedback не имеет meaningful reverse semantics. |
| Modals, focus, notifications, `relationshipCue`, special-mode lease, Chat transcript/map/hotspot/timed state | **SHOULD NOT RESTORE** | Transient UI/interaction state не имеет общего deterministic restore contract. |
| Future story flags | **SHOULD NOT ADD** | В current `GameState` их нет; contract не проектирует будущий canon. |
## 6. Checkpoint lifecycle

### Ordered sequence

**Normal line:**

1. `ShowLine` добавляет строку в backlog, запускает typewriter и применяет текущую presentation state.
2. `TypeText`/`CompleteTyping` заканчивает текст и помечает line seen.
3. Только теперь `CaptureStableCheckpoint` deep-copies current `GameState`, `DialogueBacklog` и actual background/character/music presentation snapshot.
4. Auto/Skip могут обычным способом ждать/advance. Checkpoint не создаётся на partial text.
5. Перед показом следующей line state меняется через существующий `ShowNextLine`/`LoadDialogueScene`; следующий stable checkpoint появится только после полного отображения новой line.

**Choice:**

1. Последняя normal line уже имеет stable checkpoint, включая presentation.
2. `ShowChoices` очищает choice-state, запоминает last-line position, строит UI и может сделать существующий Auto save.
3. Перед первым `GameState.ApplyChoice` implementation гарантирует presence отдельного choice checkpoint для этой позиции (если stable line уже представляет его, duplicate не добавляется).
4. `Choose` применяет delta, choice-state, result text и cue.
5. Result beat не checkpoint-ится. После перехода следующая normal line создаст stable checkpoint только по завершении текста.

**Rollback request:**

1. Проверить availability: runtime ready, нет active special mode/Replay/modal/choice transaction/save-load operation; buffer содержит предшествующий checkpoint.
2. Остановить `typingCoroutine`, `autoForwardCoroutine`, `skipCoroutine`, coroutine consequence cue; не вызывать `CompleteTyping` для future partial line.
3. Скрыть transient UI/cue; принудительно выключить Skip и effective Auto mode.
4. Deep-restore `GameState` и backlog snapshot, затем вызвать один preflighted restore path, эквивалентный `RestoreFromGameState(snapshotContainsVisibleEntry: true)`.
5. **После** successful dialogue restore детерминированно применить checkpoint background/character/music snapshot. Это обязательно выполняется после authored restore, чтобы future visual/audio state не мог остаться.
6. При любом restore failure не оставлять half-restored runtime: вернуть прежний state/backlog/**presentation** либо fail closed, очистить buffer и сделать действие недоступным. Нельзя продолжать с несогласованными dialogue/UI state.
7. После success target line fully visible; Auto/Skip не стартуют сами.

### Capacity and clear rules

- Proposed maximum: **12 checkpoints**. При добавлении 13-го удалить только oldest checkpoint.
- Payload каждый раз deep-copies максимум 100 `DialogueBacklogEntry`; shared mutable lists/entries запрещены.
- Capture допускается только при суммарном `speaker.Length + text.Length` всех backlog entries **не более 65,536 UTF-16 code units**. Это ровно до 128 KiB raw UTF-16 character storage на checkpoint, без object overhead и без ambiguity «KiB characters».
- Для обычного demo текста (примерно 100–500 combined code units на entry) это ориентировочно 10,000–50,000 code units, то есть ~20–100 KiB raw UTF-16 text на checkpoint и ~0.23–1.17 MiB на 12 checkpoints плюс object/presentation-reference overhead.
- Без truncation: если backlog snapshot превышает 65,536 code units, новый checkpoint целиком не создаётся. Уже существующие checkpoints остаются валидны; rollback из current large line доступен только к предыдущему валидному checkpoint.
- Rejected `TryEnterSpecialMode` buffer **не** очищает. Только после successful lease acquisition, до начала special-mode mutable interaction, buffer очищается; после successful exit новая rollback history начинается с нуля.
- Clear buffer: при старте/конце Replay, accepted Load/Quick Load/Continue, New Game, Main Menu, destroy/Unity scene unload и restore failure. Безопаснее очистить buffer в начале accepted load transaction; даже если Load затем failed, stale buffer не возвращается.
- **Не clear:** Manual/Auto/Quick Save сами по себе и ordinary registered `DialogueSceneData` transition. Save после rollback записывает ровно текущий восстановленный state/backlog, но никогда не serializes rollback buffer.
## 7. Hard barriers

`DialogueSceneData` и Unity scene — разные понятия. Обычный `LoadDialogueScene` остаётся внутри текущей Unity gameplay scene, а `RestoreFromGameState` уже валидирует registered `sceneId/lineId`; normal registered `DialogueSceneData` transitions **SAFE TO SNAPSHOT только вместе с deterministic actual presentation snapshot**.

| State/mode | Classification | Contract |
|---|---|---|
| Ordinary registered `DialogueSceneData` | **SAFE TO SNAPSHOT** | Restore through registry-validated line identity, then checkpoint background/character/music; no Unity reload. |
| Character Hub | **NOT RELEVANT** | Это ordinary read-only modal, не `SpecialModeCoordinator`; rollback unavailable while open, но closed Hub не очищает buffer. |
| Gallery/Replay | **HARD BARRIER** | Replay имеет отдельные campaign snapshot, local backlog и seen state; campaign rollback внутри/через него не допускается. Clear on replay start/end. |
| Chat/Phone | **HARD BARRIER** | Transcript, pacing, viewer и Chat choice effects не имеют rollback restore contract. Clear before entry and after return route. |
| Map Locations | **HARD BARRIER** | Scene-local map UI/availability/route ownership не snapshot-ятся. Clear before entry and after location route. |
| Interactive Hotspot | **HARD BARRIER** | Completed hotspot IDs и multiple runtime state effects не включены в campaign payload. Clear before entry and after completion/cleanup. |
| Timed Narrative Beat | **HARD BARRIER** | Countdown, terminal race protection и success/timeout route transient; no mid-beat restore. |
| Any future `SpecialModeCoordinator` owner | **HARD BARRIER by default** | Rejected entry preserves buffer; successful lease clears it before mutable interaction. Only separate reviewed persistence/restore contract can opt in. |
| Load/Quick Load/Continue/New Game/Main Menu/unknown Unity scene load | **HARD BARRIER** | Persistent host lifecycle or a different save session invalidates in-memory checkpoints. |

## 8. Save/load interaction

- `SaveData.CurrentVersion` остаётся **3**. Новый buffer не является slot data, не меняет migrations, JSON, preview или slot rotation.
- Manual/Auto/Quick Save не удаляют и не serialизуют buffer. Они сохраняют current restored state/backlog точно так же, как текущий `SaveManager.SaveSlot`.
- Любая accepted операция Load, Quick Load или Continue очищает buffer до применения loaded state. Имеющийся pre-load Auto save продолжает быть независимой save safety pipeline.
- После успешного load `VNDialogueController.RestoreFromGameState` восстанавливает slot snapshot как сейчас; новый rollback buffer пуст. Следующий stable line может начать новую history.
- New Game очищает buffer одновременно с `GameState.ResetState()`.
- Main Menu transition и Unity host destruction очищают buffer. Continue из Main Menu стартует с пустым buffer.
- Rollback никогда не переписывает, не удаляет и не выбирает Manual/Auto/Quick slots.

## 9. History/read semantics

`DialogueBacklog` и `DialogueReadHistory` остаются разными системами.

- Current play-session History — snapshot state. После successful rollback она **заменяется** checkpoint backlog. Все реплики/resultText из future линии или отменённой ветки исчезают; entries прошлого не merge-ятся.
- Save-scoped backlog snapshot сохраняется только при обычном Save и восстанавливается обычным Load. Rollback buffer не записывается в slot.
- Seen/read semantics не делают reverse: `DialogueReadHistory` хранит persistent `sceneId::lineId` и используется seen-aware Skip. Линия, уже полностью прочитанная до rollback, остаётся seen, даже если player вернулся назад или сменил choice.
- Это намеренно: «unsee» сделало бы Skip profile-state зависимым от временной навигации и расходилось бы с текущей save-independent моделью. В Replay rollback не поддерживается; replay-local seen не затрагивается.

## 10. Auto/Skip/typewriter semantics

Для детерминизма rollback — явная пауза reading automation:

- если typewriter активен, отменить coroutine **без** `CompleteTyping`; затем target checkpoint показывается полностью;
- остановить Auto timer и выключить effective Auto mode; сохранённая настройка Auto не snapshot-ится и не должна автоматически запустить transition после restore;
- остановить Skip timer и вызвать `SetSkip(false)`; при rollback Skip не переносится на restored line;
- не восстанавливать elapsed delay, partial text, coroutine reference или hidden wait;
- игрок вручную возобновляет Auto/Skip существующими action после rollback.

Это допускает, что rollback сознательно меняет активный reader mode на paused, но не меняет story-state и исключает auto-advance сразу после восстановления.

## 11. UX route

Rollback не добавляется пятой постоянной кнопкой в принятый compact strip `История | Пропуск | Авто | Быстр. сох.`: это ухудшит current compact contract, а History не должна выглядеть как rollback.

Product-level contract после отдельного UI approval:

- должен существовать один guarded backend command; UI не делает direct state mutation;
- `Esc → Game Menu → Откат` — основной кандидат player-facing route, с disabled state/краткой причиной при empty buffer или barrier;
- exact keyboard, mouse и controller bindings требуют отдельного player-facing UI/input review и не являются частью backend pass;
- `Backspace`, right click и `Left Shoulder` — **PROPOSAL ONLY / DEFERRED**, не approved bindings и не изменение `VNInputMap` в этом pass.

Если checkpoint недоступен, будущая Game Menu action disabled; direct route не меняет state и может использовать existing toast «Откат недоступен». Никакого permanent Rollback button или нового UI в этом audit pass нет.
## 12. Rejected alternatives

| Alternative | Complexity/correctness | Memory / SaveData / special modes | Verdict |
|---|---|---|---|
| Visual-only previous line | Низкая сложность, но choice delta, `selectedChoiceIndex`, backlog и route остаются в будущем. | Не решает special-mode state; создаёт ложную навигацию. | Rejected: прямо запрещено. |
| Full `SaveData` snapshot на каждый step | Переиспользует знакомую форму, но смешивает transient feature с versioned slot contract и migration/validation. | Много JSON/preview/I/O, риск `SaveData` v3 и нет special-mode restore. | Rejected. |
| Reload scene/save для каждого rollback | Требует materialized save либо Unity reload, медленнее и не возвращает несохранённые checkpoints. | Перезаписывает/нагружает Auto/Quick rotation, конфликтует с pending restore и special modes. | Rejected. |
| Generic command/undo framework | Каждый existing/future side effect должен получить reversible command; неполное покрытие снова создаст desync. | Не решает Chat/Map/Hotspot/Timed internal state; чрезмерно для demo. | Rejected. |
| Bounded in-memory stable checkpoints | Small explicit payload, существующий restore shape, clear barriers и fail-closed guards. | 12 checkpoints, no SaveData writes, special modes declared hard barriers. | **Chosen.** |

## 13. Risks

1. Current restore API рассчитан на save path; implementation должна извлечь минимальный shared restore seam или аккуратно использовать verified equivalent без дублирования logic. Нельзя копировать restore code в UI.
2. Presentation state не полностью authored/recomputable: без actual snapshot future background/character/music могут остаться после правильного `GameState` restore. Restore order и rollback-on-failure должны покрывать presentation так же атомарно, как state/backlog.
3. Full backlog deep copies — главный memory risk; обязательны 12-checkpoint capacity и точный guard `<= 65,536 UTF-16 code units` без silent truncation.
4. Choice `resultText` не будет individually rewindable: это намеренное UX limit ради возможности сменить choice без double delta.
5. Any new mutable story state, ordinary ambience mutation, side effect или special mode автоматически не поддерживается. До explicit audit это hard barrier или отдельный contract update.
6. Current `DialogueReadHistory` monotonic: after rollback Skip может считать future line seen. Это сохранение принятой profile semantics, а не defect.
7. Game Menu route и exact input bindings deferred: их нельзя silently добавить к backend implementation без отдельного player-facing review, graphical E2E и baseline.
## 14. Test matrix

Ниже — будущая matrix; в этом docs pass тесты не пишутся.

| Scenario | Main level | Required assertions | Additional proof |
|---|---|---|---|
| Buffer: empty, ordinal pop, 12-depth eviction, deep copy | EditMode | No aliasing, exact selected prior checkpoint, deterministic empty-buffer behavior | Smoke: no `SaveData` schema change. |
| Backlog memory guard | EditMode | Total `speaker.Length + text.Length <= 65,536` UTF-16 code units accepted; 65,537 rejected without truncation or partial checkpoint | — |
| Line A → line B → rollback | PlayMode | Position/text/backlog/GameState return to A; B future History absent | Graphical E2E normal reading state. |
| Presentation rollback | PlayMode | A has background/character/music A; future line/data-scene changes presentation; rollback makes actual background/character/music exactly equal checkpoint A | Graphical E2E visual/audio-state proxy. |
| Presentation carry-over | PlayMode | Target line has no explicit character/background/music directive; future state differs; rollback leaves no future presentation | PlayMode exact Image/AudioSource assertions. |
| Active typewriter → rollback | PlayMode | Partial B not marked seen by rollback; restored A full text; no active typing coroutine | Graphical E2E readable restored line. |
| Auto → rollback | PlayMode | Timer cancelled; no automatic advance after wait; manual Auto needed | Graphical E2E action state. |
| Skip → rollback | PlayMode | Skip disabled; no automatic advance; seen-aware predicate still profile-based | PlayMode is sufficient. |
| Choice + stat delta → rollback | PlayMode | All nine state fields restored; choice panel reopens; selected/pending state inactive; cue hidden | Graphical E2E choice re-open. |
| Rollback → choose another choice | PlayMode | Old delta absent, new delta applied once, old result/backlog removed, new cue once | Graphical E2E branch proof. |
| Repeated rollback | PlayMode | Ordered stable checkpoints, no duplicate backlog entries, deterministic empty-buffer behavior | — |
| History consistency | PlayMode | `CaptureBacklogSnapshot` exactly equals checkpoint; no future entries | EditMode serializer-copy helper. |
| Save after rollback | PlayMode | Manual/Quick/Auto Save records restored state/backlog; buffer itself absent from serialized JSON | Smoke validates `SaveData.CurrentVersion == 3`. |
| Load/Quick Load/Continue clears history | PlayMode | Successful load restores slot and empty rollback buffer; accepted failed load remains fail-closed | Existing Save/Load smoke regression. |
| Normal `DialogueSceneData` transition | PlayMode | Cross-data-scene normal checkpoint restores registry-valid position **and** checkpoint presentation | Graphical E2E one normal transition. |
| Unity scene transition barrier | PlayMode | Main Menu/New Game/unknown scene clears buffer | Scene-flow smoke. |
| Failed special-mode entry | PlayMode | Rejected `TryEnterSpecialMode` leaves existing rollback history intact | Special-mode regression. |
| Successful special-mode entry | PlayMode per Chat/Hotspot/Map/Timed plus Replay | Successful lease clears history before mutable interaction; exit/route starts empty | Existing special-mode smokes; graphical only where player-facing route exists. |
| Character Hub modal | PlayMode | Rollback blocked while open, buffer preserved when Hub closes | Existing Hub runtime proof if UI changes. |
| Backend/UI separation | EditMode/PlayMode smoke | Backend has no permanent Quick Menu fifth action or concrete new binding in pass A | UI route proof deferred to pass C. |
## 15. Minimal implementation plan

Только после reviewer approval и отдельными staged passes:

### A. Backend/state pass

1. Добавить small non-serialized `RollbackCheckpoint`/bounded buffer, owned by `VNDialogueController` (or another existing controller-owned runtime component), с deep copies, 12-capacity и guard `<= 65,536 UTF-16 code units`.
2. Добавить transactional restore вокруг existing preflighted `GameState` + backlog + `RestoreFromGameState`; capture/restore actual background/character/music snapshot после dialogue restore и rollback state/backlog/presentation на error.
3. Hook stable capture только после full text completion, плюс deduplicated choice checkpoint перед `GameState.ApplyChoice`; никогда не checkpoint choice result beat.
4. Add barrier clears to load/new-game/Main Menu/Replay и только **after successful** `TryEnterSpecialMode` lease acquisition; rejected entry сохраняет buffer. Не создавать generic special-mode restore framework.
5. Добавить EditMode/PlayMode regression из backend portions matrix. **Без нового player-facing input/UI, без изменения `VNInputMap`, без permanent кнопки.**

### B. Reviewer review backend correctness

Проверить code/diff, atomic state+backlog+presentation restore, memory boundary, choice re-selection, load and special-mode barriers. До этого backend не считается approved для player-facing route.

### C. Separate player-facing route pass

Принять Game Menu/input decision; затем добавить unified bindings только по reviewed choice, выполнить graphical E2E на 1920×1080 и обновить relevant visual baseline. Не объединять этот pass с backend/state implementation.
## 16. GO / NO-GO recommendation

**GO WITH LIMITS — bounded implementation разумна только с перечисленными hard barriers.**

The current ordinary dialogue architecture already has stable scene/line IDs, an explicit `GameState` shape, deep-copyable backlog, capturable in-memory presentation references and validated in-place restore. That is sufficient for a narrow, in-memory recovery feature only when presentation snapshot restore is part of the same transaction. It is **not** sufficient to make all existing authored interactions rewindable. Any implementation that omits atomic state/backlog restore, buffer limits, Auto/Skip pause or special-mode barriers is out of contract and must not ship.

**IMPLEMENTATION STATUS: NOT IMPLEMENTED**