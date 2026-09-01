# How I Fall — feasibility contract Rollback / Rewind

> **Статус:** architecture/product audit на `origin/master` `40c7549d7cac6c13b5a2f47089ff3a55a4b79237`.
>
> Этот документ не разрешает production implementation. `SaveData` v3, scenes, prefabs и текущие saves не менялись.

## 1. Executive summary

Для **Polished Functional Demo** рекомендуем **GO WITH LIMITS**: ограниченный in-memory `Rollback` по **предыдущим stable dialogue lines и choice checkpoints**. Это вариант C, но не generic undo, не save-system и не «показать предыдущую строку».

`Rollback` означает: вернуть runtime к последнему доступному стабильному checkpoint той же кампании так, чтобы dialogue position, все девять numeric полей `GameState`, choice-state и текущий `DialogueBacklog` были согласованы. Если checkpoint находится перед choice, игрок снова видит этот choice и может выбрать другой вариант. Старый effect не остаётся в `GameState` и не может накопиться повторно.

Границы обязательны:

- buffer живёт только в памяти и не попадает в `SaveData`/слоты;
- обычный переход между зарегистрированными `DialogueSceneData` поддерживается; это не Unity scene reload;
- любой active special mode, Replay, Load/Continue/New Game/Main Menu и Unity-scene boundary — hard barrier;
- rollback из History, для partial typewriter и визуальных UI side effects не существует;
- Auto и Skip после rollback принудительно останавливаются; checkpoint не восстанавливает их активность.

Такой contract использует уже существующий атомарный набор campaign state и проверенный restore path, но требует отдельной небольшой runtime-реализации и PlayMode покрытия после reviewer approval.

## 2. Current HIF execution model

### Обычная строка

1. `VNDialogueController.Start()` выбирает normal start, pending Save restore или Replay (`Assets/HowIFall/Scripts/VN/VNDialogueController.cs`, `Start`). Для normal start вызывается `LoadDialogueScene(sceneData)`.
2. `LoadDialogueScene` назначает `sceneData`, `activeLines`, `activeChoices`, `currentLineIndex`, очищает transient choice state, записывает `GameState.currentSceneId/currentLineIndex/currentLineId`, восстанавливает background через `FindLastBackgroundBeforeOrAt`, затем вызывает `ShowLine`.
3. `ShowLine` сразу помещает raw `speaker/text` в `DialogueBacklog`, запускает `ShowText`/`TypeText` и применяет визуалы строки. Строка уже попала в History до завершения typewriter.
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
- `TryEnterSpecialMode` выдаёт scene-local lease и останавливает Auto/Skip для blocking policy. `SpecialModeCoordinator` — transient и не сериализуется.

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
| Auto | `SetAutoForward`, `StartAutoForwardDelayIfReady`, `StopAutoForwardTimer` — `VNDialogueController.cs`; preference — `SettingsManager` | Не сохранять и не восстанавливать как checkpoint state. |
| Skip | `SetSkip`, `StartSkipDelayIfReady`, `StopSkipTimer` — `VNDialogueController.cs` | Не сохранять и не восстанавливать; при rollback выключать. |
| Save slot state | `CaptureGameState`, `ApplyGameState`, `TryApplyInPlace` — `Assets/HowIFall/Scripts/Save/SaveManager.cs` | Удобный reference shape, но не использовать `SaveData` как rollback buffer. |
| Normal scene route | `ShowNextLine`/`LoadDialogueScene` — `VNDialogueController.cs` | Поддерживаемо в пределах зарегистрированного `DialogueSceneRegistry`. |
| Unity scene route | `StartNewGame`, `OpenLoadedGame`, `ReturnToMainMenu` — `Assets/HowIFall/Scripts/Core/SceneFlowManager.cs` | Hard barrier: runtime host уничтожается/пересоздаётся. |
| Exclusive modes | `TryEnterSpecialMode`/`ExitSpecialMode` — `VNDialogueController.cs`; `SpecialModeCoordinator.cs` | Их transient state не имеет restore contract; hard barrier. |

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

Внутренний тип должен быть отдельным transient plain-C# payload, не `SaveData` и не JSON.

| Field | Статус | Причина |
|---|---|---|
| `currentSceneId`, `currentLineId`, `currentLineIndex` | **REQUIRED** | Идентифицируют точную зарегистрированную dialogue position. |
| `lust`, `romance`, `purity`, `corruptionLevel`, `selfControl`, `suspicion`, `trustMasha`, `trustArtem`, `leraInterest` | **REQUIRED** | Это все существующие mutable numeric axes `GameState`. |
| `selectedChoiceIndex`, `choiceResultActive`, `pendingNextSceneId` | **REQUIRED** | Сохраняют валидный choice invariant; normal checkpoint обычно хранит inactive состояние. |
| Deep copy `DialogueBacklogEntry` (`speaker`, `text`, до текущего 100-entry cap) | **REQUIRED** | Убирает History будущей ветки и сохраняет текущий session History согласованным. |
| Checkpoint kind/ordinal (normal line или choice checkpoint) | **REQUIRED** | Управляет выбором предыдущего checkpoint и исключает result beat. |
| Choice UI visibility/visible choices | **RECOMPUTABLE** | `ShowChoices` строит UI из линии и restored `GameState`. |
| Background, character visual, audio | **RECOMPUTABLE** | `LoadDialogueScene`/`ShowLine` уже восстанавливают line-driven presentation. |
| Full dialogue text | **RECOMPUTABLE** | Берётся из current `DialogueLine`; snapshot не должен дублировать authored content. |
| Typewriter progress/coroutine | **SHOULD NOT RESTORE** | Checkpoint бывает только после complete text; restore показывает target line полностью. |
| Auto timer, Auto preference, Skip enabled/timer | **SHOULD NOT RESTORE** | Это control mode, а не story state; rollback их останавливает. |
| `DialogueReadHistory` / replay-local seen set | **SHOULD NOT RESTORE** | Seen — monotonic profile/replay semantics, не History и не save slot. |
| Modals, focus, notifications, `relationshipCue`, special-mode lease, Chat transcript/map/hotspot/timed state | **SHOULD NOT RESTORE** | Transient UI/interaction state не имеет общего deterministic restore contract. |
| Future story flags | **SHOULD NOT ADD** | В current `GameState` их нет; contract не проектирует будущий canon. |

## 6. Checkpoint lifecycle

### Ordered sequence

**Normal line:**

1. `ShowLine` добавляет строку в backlog и запускает typewriter.
2. `TypeText`/`CompleteTyping` заканчивает текст и помечает line seen.
3. Только теперь `CaptureStableCheckpoint` deep-copies current `GameState` и `DialogueBacklog`.
4. Auto/Skip могут обычным способом ждать/advance. Checkpoint не создаётся на partial text.
5. Перед показом следующей line state меняется через существующий `ShowNextLine`/`LoadDialogueScene`; следующий stable checkpoint появится только после полного отображения новой line.

**Choice:**

1. Последняя normal line уже имеет stable checkpoint.
2. `ShowChoices` очищает choice-state, запоминает last-line position, строит UI и может сделать существующий Auto save.
3. Перед первым `GameState.ApplyChoice` implementation гарантирует presence отдельного choice checkpoint для этой позиции (если stable line уже представляет его, duplicate не добавляется).
4. `Choose` применяет delta, choice-state, result text и cue.
5. Result beat не checkpoint-ится. После перехода следующая normal line создаст stable checkpoint только по завершении текста.

**Rollback request:**

1. Проверить availability: runtime ready, нет active special mode/Replay/modal/choice transaction/save-load operation; buffer содержит предшествующий checkpoint.
2. Остановить `typingCoroutine`, `autoForwardCoroutine`, `skipCoroutine`, coroutine consequence cue; не вызывать `CompleteTyping` для future partial line.
3. Скрыть transient UI/cue; принудительно выключить Skip и effective Auto mode.
4. Deep-restore `GameState` и backlog snapshot, затем вызвать один preflighted restore path, эквивалентный `RestoreFromGameState(snapshotContainsVisibleEntry: true)`.
5. При любом restore failure не оставлять half-restored runtime: вернуть прежний state/backlog либо fail closed, очистить buffer и сделать действие недоступным. Нельзя продолжать с несогласованными dialogue/UI state.
6. После success target line fully visible; Auto/Skip не стартуют сами.

### Capacity and clear rules

- Proposed maximum: **12 checkpoints**. При добавлении 13-го удалить только oldest checkpoint.
- Payload каждый раз deep-copies максимум 100 `DialogueBacklogEntry`; shared mutable lists/entries запрещены.
- Обычный demo text (примерно 100–500 символов на entry) даёт ориентировочно 20–100 KiB backlog-string payload на checkpoint, то есть порядка 0.25–1.2 MiB на 12 checkpoints плюс object overhead.
- Текущий defensive ceiling `16384` символа на entry формально допускает до ~3.1 MiB UTF-16 text на один 100-entry snapshot и ~37.5 MiB на 12, без object overhead. Поэтому implementation обязан иметь отдельный **64 KiB total backlog-character capture guard**: oversized checkpoint не создаётся целиком (без truncation). Уже существующие checkpoints остаются валидны; rollback из текущей большой строки доступен только к предыдущему валидному checkpoint.
- Clear buffer: до входа в любой special mode, при старте/конце Replay, accepted Load/Quick Load/Continue, New Game, Main Menu, destroy/Unity scene unload и restore failure. Безопаснее очистить buffer в начале accepted load transaction; даже если Load затем failed, stale buffer не возвращается.
- **Не clear:** Manual/Auto/Quick Save сами по себе и обычный `DialogueSceneData` transition. Save после rollback записывает ровно текущий восстановленный state/backlog, но никогда не serializes rollback buffer.

## 7. Hard barriers

`DialogueSceneData` и Unity scene — разные понятия. Обычный `LoadDialogueScene` остаётся внутри текущей Unity gameplay scene, а `RestoreFromGameState` уже валидирует registered `sceneId/lineId`; поэтому normal registered `DialogueSceneData` transitions **SAFE TO SNAPSHOT**.

| State/mode | Classification | Contract |
|---|---|---|
| Ordinary registered `DialogueSceneData` | **SAFE TO SNAPSHOT** | Restore only through registry-validated line identity; no Unity reload. |
| Character Hub | **NOT RELEVANT** | Это ordinary read-only modal, не `SpecialModeCoordinator`; rollback unavailable while open, но closed Hub не очищает buffer. |
| Gallery/Replay | **HARD BARRIER** | Replay имеет отдельные campaign snapshot, local backlog и seen state; campaign rollback внутри/через него не допускается. Disable and clear on replay start/end. |
| Chat/Phone | **HARD BARRIER** | Transcript, pacing, viewer и Chat choice effects не имеют rollback restore contract. Clear before entry and after return route. |
| Map Locations | **HARD BARRIER** | Scene-local map UI/availability/route ownership не snapshot-ятся. Clear before entry and after location route. |
| Interactive Hotspot | **HARD BARRIER** | Completed hotspot IDs и multiple runtime state effects не включены в campaign payload. Clear before entry and after completion/cleanup. |
| Timed Narrative Beat | **HARD BARRIER** | Countdown, terminal race protection и success/timeout route transient; no mid-beat restore. |
| Any future `SpecialModeCoordinator` owner | **HARD BARRIER by default** | Only separate reviewed persistence/restore contract can opt in; no generic exception. |
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

Не добавлять Rollback в принятый compact strip `История | Пропуск | Авто | Быстр. сох.`: пятая постоянная action ухудшит текущий compact contract, а History не должна выглядеть как rollback.

После отдельного UI approval logical route:

- **primary:** `Esc → Game Menu → Откат`, context-action с disabled state/краткой причиной при empty buffer или barrier;
- **keyboard:** новый единый `VNInputAction.Rollback` в `VNInputMap`, proposed binding `Backspace`;
- **mouse:** secondary/right click по reading surface только когда она получает обычный advance input и нет UI blocker;
- **controller:** `Left Shoulder`, routed через тот же `VNInputAction.Rollback`;
- все routes вызывают единственный guarded command. No direct mutation from UI.

Если checkpoint недоступен, visible Game Menu action disabled; direct input не меняет state и может показать короткий existing toast «Откат недоступен». Никакого permanent Rollback button или нового UI в этом audit pass нет.

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
2. Full backlog deep copies — главный memory risk; обязателен capacity и total-character guard без silent truncation.
3. Choice `resultText` не будет individually rewindable: это намеренное UX limit ради возможности сменить choice без double delta.
4. Any new mutable story state, side effect или special mode автоматически не поддерживается. До explicit audit это hard barrier.
5. Current `DialogueReadHistory` monotonic: after rollback Skip может считать future line seen. Это сохранение принятой profile semantics, а не defect.
6. Input mapping, Game Menu action, mouse/controller route и toast требуют отдельного player-facing pass с graphical proof; они не одобрены этим документом.

## 14. Test matrix

Ниже — будущая matrix; в этом docs pass тесты не пишутся.

| Scenario | Main level | Required assertions | Additional proof |
|---|---|---|---|
| Buffer: empty, ordinal pop, 12-depth eviction, deep copy, 64 KiB guard | EditMode | No aliasing, exact selected prior checkpoint, fail-closed overflow/invalid payload | Smoke: no `SaveData` schema change. |
| Line A → line B → rollback | PlayMode | Position/text/backlog/GameState return to A; B future History absent | Graphical E2E normal reading state. |
| Active typewriter → rollback | PlayMode | Partial B not marked seen by rollback; restored A full text; no active typing coroutine | Graphical E2E readable restored line. |
| Auto → rollback | PlayMode | Timer cancelled; no automatic advance after wait; manual Auto needed | Graphical E2E action state. |
| Skip → rollback | PlayMode | Skip disabled; no automatic advance; seen-aware predicate still profile-based | PlayMode is sufficient. |
| Choice + stat delta → rollback | PlayMode | All nine state fields restored; choice panel reopens; selected/pending state inactive; cue hidden | Graphical E2E choice re-open. |
| Rollback → choose another choice | PlayMode | Old delta absent, new delta applied once, old result/backlog removed, new cue once | Graphical E2E branch proof. |
| Repeated rollback | PlayMode | Ordered stable checkpoints, no duplicate backlog entries, deterministic empty-buffer behavior | — |
| History consistency | PlayMode | `CaptureBacklogSnapshot` exactly equals checkpoint; no future entries | EditMode serializer-copy helper. |
| Save after rollback | PlayMode | Manual/Quick/Auto Save records restored state/backlog; buffer itself absent from serialized JSON | Smoke validates `SaveData.CurrentVersion == 3`. |
| Load/Quick Load/Continue clears history | PlayMode | Successful load restores slot and empty rollback buffer; accepted failed load remains fail-closed | Existing Save/Load smoke regression. |
| Normal `DialogueSceneData` transition | PlayMode | Cross-data-scene normal checkpoint restores registry-valid position/presentation | Graphical E2E one normal transition. |
| Unity scene transition barrier | PlayMode | Main Menu/New Game/unknown scene clears buffer | Scene-flow smoke. |
| Special-mode barrier | PlayMode per Chat/Hotspot/Map/Timed plus Replay | Entry disables/clears rollback; no rollback in mode; post-route starts empty | Existing special-mode smokes; graphical only where player-facing route exists. |
| Character Hub modal | PlayMode | Rollback blocked while open, buffer preserved when Hub closes | Existing Hub runtime proof if UI changes. |
| Input/UI contract | PlayMode + smoke | Unified input has one binding; Game Menu disabled reason; no Quick Menu fifth action | Graphical E2E at 1920×1080. |

## 15. Minimal implementation plan

Only after reviewer approves this contract:

1. Add a small non-serialized `RollbackCheckpoint`/bounded buffer owned by `VNDialogueController` (or another existing controller-owned runtime component), with explicit deep copies and 12/64 KiB guards.
2. Add one transactional restore method around the existing preflighted `GameState` + backlog + `RestoreFromGameState` behavior; include state/backlog rollback on error.
3. Hook stable capture only after full text completion, plus deduplicated choice checkpoint before `GameState.ApplyChoice`; never checkpoint choice result beat.
4. Add explicit barrier clears to existing scene flow, load acceptance, replay lifecycle and `TryEnterSpecialMode`; do not add a generic special-mode restore framework.
5. Add unified guarded `VNInputAction.Rollback` and approved Game Menu/UI routes; keep compact Quick Menu at four actions.
6. Implement matrix tests first (EditMode buffer, PlayMode state/choice/load/barrier), then run relevant smoke and graphical E2E; inspect screenshots and update only relevant visual baselines.

## 16. GO / NO-GO recommendation

**GO WITH LIMITS — bounded implementation разумна только с перечисленными hard barriers.**

The current ordinary dialogue architecture already has stable scene/line IDs, an explicit `GameState` shape, deep-copyable backlog and validated in-place restore. That is sufficient for a narrow, in-memory recovery feature. It is **not** sufficient to make all existing authored interactions rewindable. Any implementation that omits atomic state/backlog restore, buffer limits, Auto/Skip pause or special-mode barriers is out of contract and must not ship.

**IMPLEMENTATION STATUS: NOT IMPLEMENTED**