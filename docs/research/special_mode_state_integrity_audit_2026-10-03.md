# Целостность состояния и lifecycle специальных режимов — 2026-10-03

## Результат и границы

**BOUNDED CORRECTION / локальные проверки PASS.** Исходная база после `fetch` и `master → ff-only origin/master`: `66f2fc0db1706d37f24e7acaa2e29324d7b20765`. Ветка: `codex/special-mode-state-integrity-2026-10-03`. Это observation-first аудит существующей технической основы, не новая фича или redesign.

Обозначения: **REPRODUCED** — дефект реально воспроизведён; **DISPROVED** — конкретное подозрение опровергнуто в проверенном сценарии; **VERIFIED / executed** — выполненные tests/smoke/graphical proof; **VERIFIED / code** — только inspection; **NOT VERIFIED** — поведение не доказано; **NOT RUN** — проверка не запускалась. Local PASS не равен exact-head CI или reviewer acceptance.

Новые runtime fixtures — **TECH DEMO ONLY / NOT CANON**, создаются только тестами. Сцены, prefab, serialized refs, Packages, ProjectSettings, `.meta`, арт и принятая UI-геометрия не входят в commit. `SaveData` остаётся v3; нет persistence специальных режимов, новых story flags или manager/framework.

## H1 — Hotspot feedback / Reading

**REPRODUCED на исходном production-коде:**

1. `HotspotFeedback_DoesNotMarkInterruptedReadingLineSeen`: ordinary line из 200 символов ещё набирается и не seen; разрешённый `TryStartInteractiveScene` показывает короткий feedback. После реального завершения его `TypeText` ordinary `sceneId::lineId` становится seen. Expected false, actual true. Entry действительно разрешает in-progress typewriter: это не обход приватного entry guard.
2. `HotspotFeedback_NoRouteCompletionRestoresReading_AndSaveLoadIdentity`: завершённая ordinary реплика `Ordinary` → feedback → разрешённый `completeScene=true` без next route. После выхода `dialogueText` содержит `D` от `Done`, а не `Ordinary`. Validator допускает такое завершение. Это stale presentation, а не нормальный story transition.

Первый red-run: **0/3 Passed, 3 failed**, включая H2; XML/log в `red/`. Это воспроизведение технического публичного entry/lifecycle API, не утверждение о наличии этого пути в обычном authored demo story.

**Минимальная коррекция:** `VNDialogueController` сохраняет только transient presentation ordinary Reading до первого feedback. Feedback не отмечает underlying line seen и не создаёт stable checkpoint. `InteractiveSceneController` при completion/disable/destroy восстанавливает текст, имя и typing-state до release lease. Прерванный prefix продолжается существующим `TypeText`, без `ShowLine`, нового backlog entry или отдельной dialogue system. При teardown с уже уничтоженным Reading UI восстановление безопасно прекращается, lease всё равно освобождается.

**VERIFIED / executed после коррекции:**

- feedback tail не отмечает непрочитанную ordinary line seen; scene/line identity и backlog сохраняются;
- no-route completion восстанавливает `dialogueText`, `currentFullText`, speaker, завершённое typing-state; нет активного/видимого Hotspot UI или lease;
- после no-route completion настоящий Manual save → advance → load восстанавливает исходные scene/line/text/backlog, seen-history остаётся монотонным, clean re-entry не дублирует backlog;
- при `OnDisable` прерванный prefix восстанавливается сразу, затем реально продолжает набор; seen появляется только после ordinary completion; `OnDestroy` не оставляет feedback coroutine;
- routed completion применяется один раз, underlying interrupted line остаётся unseen, target line легитимно появляется в backlog и seen-history;
- Auto/Skip timers во время feedback отсутствуют, underlying Reading не продвигается; preference Auto не переписывается, после target completion timer возобновляется;
- accepted entry/exit по существующему контракту **очищают rollback buffer**. Это intentional hard barrier, а не обещание сохранить прежнее число checkpoints. Feedback не создаёт checkpoint; normal target после завершения набора создаёт ровно один.

**DISPROVED в этих сценариях:** добавление/дублирование feedback в backlog, самовольная смена scene/line, захват feedback rollback checkpoint, неверная Save/Load identity после восстановления. Исходные read-history и no-route presentation дефекты не опровергнуты — они воспроизведены и исправлены.

## H2 — повторный Timed Beat start

**REPRODUCED:** валидный beat видим, `Running`, владеет lease; повторные `TryStartBeat(null)` / `TryStartBeat(validDefinition)` возвращают false, но исходный код скрывает panel. Red assertion: expected visible true, actual false. `HidePanel()` не освобождает definition/lease и не меняет `Running` — **VERIFIED / code**; скрытый beat сохраняет таймер, поэтому это потеря видимого manual interaction, не доказательство бесконечного deadlock: eventual timeout ещё возможен.

**Коррекция:** отдельный early-return для `IsRunning`, до validation и `HidePanel`. Другие fail-safe rejection paths сохранены.

**VERIFIED / executed:** rejection сохраняет видимость, exact lease/definition, оставшееся время и `Running`; Reading/Save/Load остаются заблокированы; настоящий `actionButton.onClick` разрешает original beat, normal success route выполняется один раз, UI и lease очищаются, ordinary eligibility возвращается; повторный resolve и stale exit отвергаются. В существующий `TimedNarrativeBeatSmokeTests` добавлен missing visibility invariant после duplicate rejection.

## Дополнительный воспроизведённый дефект

**REPRODUCED — Character Hub `OnDisable`:** Hub — ordinary modal, не special-mode lease owner. Открытие подавляет Reading shell; disable компонента раньше не закрывал panel и не освобождал suppression. Intermediate runtime run: **7/8 Passed**, единственный failure — `SpecialModeIntegrity_CharacterHubDisableRestoresOrdinaryShellWithoutRouting`.

**Коррекция:** идемпотентные `OnDisable`/`OnDestroy` используют существующий `CloseCharacterHub` для живого активного host; иначе скрывают panel и освобождают только своё shell ownership. Не route и не GameState mutation. Regression доказывает disable → usable Reading → re-entry → destroy, восстановление shell и eligibility при неизменном GameState/backlog.

## Cross-mode integrity matrix

| Граница | Статус и точное покрытие |
|---|---|
| Normal blocker rejection | **VERIFIED / executed:** существующий `SpecialModeCoordinatorSmokeTests` проверяет choice/backlog/settings/confirmation/Manual SaveLoad; rejected entry не снимает current owner. Existing rollback NUnit сохраняет buffer при rejected entry и ordinary Hub |
| Competing/duplicate entry | **VERIFIED / executed:** `SpecialModeIntegrity_AllOwnersRejectCompetitors_AndHostCleanupDoesNotRoute` поочерёдно запускает Hotspot, Chat, Map, Timed; каждый отвергает все четыре competing/duplicate starts и Hub, exact lease сохраняется |
| Failed shell suppression | **VERIFIED / executed:** отдельный runtime test для Chat/Map; новая lease освобождена, чужой shell owner сохранён, его explicit release восстанавливает Reading |
| Active policy/input | **VERIFIED / executed:** четыре runtime owners блокируют advance, Save/Load, Quick Menu, backlog, settings; `AdvanceDialogue` не меняет GameState. Hotspot/Map допускают Game Menu и возврат; Chat/Timed отвергают. Auto/Skip feedback проверен отдельным H1 test; общий coordinator/Auto/Skip smoke Passed |
| Exact/wrong/stale lease | **VERIFIED / executed:** coordinator smoke проверяет чужой coordinator, duplicate/stale exit; runtime matrix — previous lease не снимает нового owner после re-entry |
| Completion / normal routing | **VERIFIED / executed:** Hotspot no-route/routed tests, Timed manual exactly-once test; existing Hotspot 7/7 и Map 1/1 PlayMode. Existing Chat smoke проверяет terminal pacing, completion/return counts, duplicate callbacks и no-route cleanup |
| Component lifecycle | **VERIFIED / executed:** для каждого из четырёх lease owners `enabled=false`, re-entry, destruction; нет lease/shell suppression и GameState/backlog mutation. Для Hub — отдельный lifecycle test. UI-before-owner teardown — новый Hotspot regression |
| Destroyed owner / host | **VERIFIED / executed:** existing coordinator smoke (`DestroyImmediate` owner и force-clear); **VERIFIED / code:** VN host `OnDestroy` force-clears coordinator. Это не exhaustive native lifecycle-order proof |
| Focus / click-through | **VERIFIED / executed:** existing Hotspot keyboard/gamepad/mouse NUnit, Game Menu return, completion/re-entry плюс fresh Hotspot E2E. Для остальных режимов проверены policy/lease/shell и ordinary eligibility; exhaustive real-device focus combinations — **NOT VERIFIED** |
| Replay | **VERIFIED / code:** Chat/Hotspot/Map entry и Hub eligibility отвергают Replay; existing Gallery replay isolation smoke Passed. Новый per-mode Replay lifecycle runtime matrix — **NOT RUN**; technical Timed entry не выдаётся за проверенный Replay contract |
| Save / rollback / read history | **VERIFIED / executed:** H1 сравнения и настоящий save/load, existing rollback 14 regressions, smoke Save backend v3. **VERIFIED / code:** diff не меняет SaveData или persistence contracts |

## Запущенные проверки

Все запуски — штатными repository launchers, Unity `6000.5.7f1`, warm `develop`. XML/logs архивированы до следующего запуска.

| Проверка | Реальный итог |
|---|---|
| Compile/import | PASS: финальные test launches без compilation errors; отдельный graphical preflight exit 0 |
| Targeted final PlayMode | **11/11 Passed**, failed/skipped 0: 9 новых integrity regressions + 2 existing tests для suite interactions |
| Полный PlayMode | **93/93 Passed**, failed/skipped 0; внутри RollbackBackend **23/23**, InteractiveHotspot **7/7**, MapLocations **1/1** |
| Полный EditMode | **40/40 Passed**, failed/skipped 0 |
| `HowIFallCiSmokeTests.RunAll` | **30/30 групп Passed**, финальный `[CI] How I Fall smoke tests passed`, exit 0; включает SpecialMode, Timed, Hotspot, Chat, Map, Hub |
| Hotspot graphical E2E | PASS, exit 0, `playerPrefsRestored=true`, sentinel UTC `2026-10-03T19:59:22.1558668Z` |

Не скрытые промежуточные failures: первый compile test имел missing `using System.Linq`, исправлен до runtime reproduction. Первый full PlayMode дал **90/92 Passed**: teardown вызвал `MissingReferenceException` в новом восстановлении shell, а новый save/load test оставлял synthetic `VNPrototype` scene и конфликтовал с existing save regression. Добавлены null-safe teardown и его отдельный regression; тест освобождает только созданную им сцену. Затем targeted **11/11** и полный **93/93** Passed. Это причины follow-up правок, не незакрытые failures.

SHA-256 final XML:

- PlayMode: `2235db95cba50431b768f423030b1a26676bd379de062045432ad7a14305812a`.
- EditMode: `82fab532926467b1ec556e79317be843c627ae3c69879b1734b3edd113ce235b`.
- Targeted: `2a2490131661edbfb76441195c99512cb064783769035fdcae4ca258f2835ea1`.

## Graphical inspection

Dedicated Hotspot runner запущен **без `-nographics`**. Просмотрены все **17/17** fresh PNG на трёх подписанных contact sheets; отдельно full-size открыты `hotspot_showcase_menu_return_1920x1080.png`, `hotspot_showcase_game_menu_1920x1080.png`, `hotspot_showcase_completion_reading_1920x1080.png`. Список всех имён/размеров/hash и inspection-флаг — в `Hotspot/screenshot-manifest.json` evidence-архива. Стандарт — 1920×1080; два 1280×720 кадра принадлежат существующему runner, новая multi-resolution автоматизация не добавлялась.

Проверены Reading shell/speaker/caret, отсутствие отдельной feedback panel/menu button, locked/available/completed markers, disabled Game Menu Save/Load, menu return и usable focus, completion без stale Hotspot controls, clean re-entry/fallback. Новых объективных clipping/overlap/missing glyph/asset/layout defects не обнаружено. Частичные реплики соответствуют typewriter capture, не обрезке завершённого текста.

Сравнены шесть соответствующих accepted Hotspot baselines: `hotspot_showcase_initial` и `hotspot_showcase_door_unlocked` RGB pixel-identical; в четырёх остальных differences ограничены typing text/caret внутри Dialogue Box. Presentation geometry/style не изменялись, поэтому baselines не перезаписаны ради recapture. Hub/Timed cleanup проверен runtime/smoke, отдельного visual redesign/capture этих поверхностей нет.

## Evidence, сохранность и ограничения

Локальный evidence root:

`D:\Codex\visualizations\2026\10\03\01a10349-74d3-7651-bafc-053e12fd0f04\special-mode-audit\`

`red/`, `hub-red/`, `primary-green/`, `targeted-green/`, `targeted-final/`, `PlayMode-first-failure/`, `PlayMode/`, `EditMode/`, `Smoke/`, `Hotspot/` сохраняют реальные logs/XML/sentinel/screenshots. Это локальный архив, не опубликованный PR artifact; не удалять при ревью.

Исходные 7 dirty tracked + 2 untracked файла защищены stash `75b350e126f2d983911cd4d90bdd571a8f8200ad` и external byte backups; все девять восстановлены byte-identical. Unity-generated asset/settings/sentinel изменения отдельно сохранены в stash `fea9e9e84279f0053ac9b5a80d5d3a9507d20007` и patch, не staged. Root sentinels опять исходные: свежий результат читать из evidence-архива. LocalLow backup сделан до runtime; все 23 исходных non-telemetry файла после QA сверены по SHA-256 (0 mismatches), изменённый `TestResults.xml` сохранён отдельно и восстановлен. Registry PlayerPrefs snapshot перед full suites импортирован обратно. Unity analytics/telemetry lifecycle не считается пользовательским save-contract.

**NOT RUN:** новая standalone сборка/прохождение этого diff, physical controller, soak, cold-Library import, отдельные graphical Chat/Map/Hub/Timed сценарии, human aesthetic QA, Drive roadmap sync. Эти проверки не подменены старым readiness/standalone PASS.

**NOT VERIFIED:** exhaustive permutations всех modes/modals, re-enable полностью деактивированного VN host с прерванным typewriter, все native destruction orders, Replay × каждый special mode, все будущие authored data. Editor shutdown содержит `JobTempAlloc` diagnostics; их production root cause/leak не доказан. Это bounded regression evidence, не гарантия отсутствия всех будущих special-mode bugs.

Commit содержит только четыре production `.cs`, два existing test/smoke `.cs` и этот отчёт. PR — против `master`, без merge. Точный head SHA, PR и exact-head `Unity Test Framework` / `Unity smoke tests` / `CI Gate` фиксируются после push в PR и финальном сообщении, чтобы не создавать самоссылочный SHA или выдавать предварительный local PASS за CI. Итог coding-agent — **REVIEW CANDIDATE**, не reviewer `DONE`.
