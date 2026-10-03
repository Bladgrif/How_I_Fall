# Аудит demo release candidate — 2026-10-03

## Вердикт и границы

**NO PRODUCTION CHANGE / REVIEW CANDIDATE.** В выполненных Editor journeys и просмотренных свежих кадрах новый объективный release-candidate blocker не воспроизведён. Принятые Main Menu, Reading, Game Menu, Choice, Speaker/Dialogue, History, Save/Load и Hotspot не перерабатывались.

**Полный release-readiness gate: PARTIAL.** Свежая standalone-сборка и запуск доказаны, но визуальный standalone-путь Main Menu → New Game → Reading не проверен из-за отсутствия desktop-control. Успех Editor E2E и отсутствие ошибок в стартовом Player.log не закрывают этот пробел. Отчёт не утверждает, что проект свободен от любых ошибок или полностью готов к выпуску.

- Exact audited base/master: `3b16670875482147afa0674adfcc5b64a72bc7d1`; проверен после `git fetch`, перехода на `master` и `merge --ff-only origin/master`.
- Checkout: `D:\How_I_Fall\develop`, существующая warm `Library`, Unity `6000.5.7f1` (`017862109af0`). Disposable worktree не создавался.
- Task branch: `codex/demo-release-candidate-audit-2026-10-03`.
- Единственный файл итогового PR: `docs/research/demo_release_candidate_audit_2026-10-03.md`. Production, tests, сцены, prefabs, references, SaveData, Packages, ProjectSettings, `.meta` и baselines в commit не меняются.
- Прочитаны `AGENTS.md`, relevant visual-QA skill, `demo_goal.md`, `review_workflow.md`, `agent_orchestration.md`, исторические readiness/pass7/pass8, README targets/baselines. Старые PASS не выдаются за текущие проверки.

Обозначения: **VERIFIED / executed** — независимо выполненное локальное доказательство; **VERIFIED / code** — inspection реализации, не runtime reproduction; **NOT VERIFIED** — недоказанное поведение; **NOT RUN** — незапущенная проверка. CI и reviewer acceptance отделены от local QA.

## 1. Compile / tests / smoke

Все запуски выполнены из warm `develop`. NUnit XML и логи сохранялись вне `Temp` сразу после каждого запуска, до следующего Unity старта.

| Проверка | Реальный результат |
|---|---|
| Compile/import preflight | PASS, exit 0, без `error CS`; дополнительно preflight каждого graphical runner |
| Полный EditMode | **40/40 Passed**, failed 0, skipped 0; NUnit XML |
| Полный PlayMode | **84/84 Passed**, failed 0, skipped 0; NUnit XML |
| `HowIFallCiSmokeTests.RunAll` | **30/30 групп Passed**, общий финальный PASS и exit 0; это группы smoke, не число NUnit tests |

В полном PlayMode: PlayerJourney **7/7**, InteractiveHotspot **7/7**, Rollback **14/14**, WheelReading **11/11**, RMB **5/5**, GameMenuFocus **8/8**, SaveLoadFocusOwnership **7/7**, PreferencesInteraction **7/7**, HistoryPresentation **2/2**, SpeakerDialoguePresentation **4/4**. Остальные существующие fixtures также Passed.

SHA-256 архивированных XML:

- `EditMode_all_results.xml`: `fc32a2ef20f384c4afae7e32b3fa85efb35db686cf2be06d08b2e589e005896a`.
- `PlayMode_all_results.xml`: `f9a9c45cb87027df1755ca1e522fc41712eb7242ebf3063a15d35e9cc4a80fd3`.

## 2. Graphical journeys и inspection

Использован существующий `tools/run-graphical-e2e.ps1`; graphical запуски — **без `-nographics`**, с обычным runtime/Game View. Новая QA framework и новые resolution сценарии не добавлялись.

| Сценарий | Результат | Свежие PNG | Просмотрены | Sentinel UTC |
|---|---|---:|---:|---|
| PlayerUi | PASS, `playerPrefsRestored=true` | 77 | 35 | `2026-10-03T12:03:36.2563649Z` |
| GameMenu | PASS, `playerPrefsRestored=true` | 20 | 8 | `2026-10-03T12:04:39.9197342Z` |
| ManualSave | PASS | 16 | 8 | `2026-10-03T12:06:17.0878094Z` |
| SaveBackendV2 | PASS | 10 | 4 | `2026-10-03T12:07:41.3556216Z` |
| Hotspot | PASS, `playerPrefsRestored=true` | 17 | 17 | `2026-10-03T12:08:44.2704840Z` |
| **Итого** | | **140** | **72** | |

Архив PlayerUi сохранён **до** GameMenu: оба runner используют исходную папку proof `PlayerUi`. Проверены freshness/non-empty обязательных PNG штатным launcher, не только PASS sentinel.

Codex самостоятельно просмотрел 72 кадра через подписанные contact sheets; дополнительно в исходном разрешении открыты `gameplay_choice_four_long_1920x1080.png`, `gameplay_backlog_long_scroll_1920x1080.png`, `hotspot_showcase_game_menu_1920x1080.png`. Остальные 68 PNG не объявляются просмотренными. Contact sheets — вспомогательные previews, не новые visual baselines.

Объективных новых clipping/overlap, missing assets/glyphs, malformed controls, неверных hidden/disabled states, stale modal или сломанных return paths в проверенном наборе не обнаружено. Нижняя частичная запись History на границе scroll viewport — штатная маска, не потеря текста. Частичные строки/caret в typing-кадрах — момент набора, не обрезка завершённой реплики. Сравнены принятые baselines Main Menu, Choice, History, Game Menu и Hotspot: явной новой target-регрессии не наблюдается. RGB-кадры History и typing побайтово по пикселям совпали с соответствующими baselines; остальные сравнения не объявляются pixel-perfect PASS.

### Обычный интегрированный путь

**VERIFIED / executed:** PlayerJourney 7/7 совместно с PlayerUi, GameMenu, ManualSave и SaveBackendV2 повторно покрывают Main Menu → New Game → Reading/Quick Menu → Choice/History → rollback → Game Menu → Save/Load/Preferences → возврат в Reading → Main Menu/Continue/load. Проверки Save runners включают фактическое восстановление состояния, подтверждения и возвраты, а не только открытие панели.

Это совокупное доказательство existing tests/runners, **не запись одного непрерывного ручного прохождения**. Named-speaker, длинные choice/dialogue и History states включают runtime QA fixtures: **TECH DEMO ONLY / NOT CANON**. Нового канонического контента для связи fixtures не создавалось.

### Hotspot / Reading после PR #48

**VERIFIED / executed:** 7 PlayMode tests и 17 свежих graphical states подтверждают:

- назначенный approved night background showcase; non-null authored background и null fallback технической комнаты;
- Laptop/Notes actionable, Door locked до обоих prerequisites, one-shot/completed состояния, переход фокуса к разблокированной Door;
- отсутствие собственного `Hotspot Menu Button` и `Feedback Panel`; обычный Reading Dialogue Box выше Hotspot view, narration/спикер и typing presentation;
- существующий Esc/RMB → Game Menu → Hotspot путь; Save/Load недоступны во время special-mode ownership;
- usable focus после menu close, completion → зарегистрированный `interactive_hotspot_complete`/Reading, восстановление ordinary eligibility и чистый повторный вход;
- виртуальные keyboard/gamepad/mouse через существующий InputSystem, запрет click-through. Это не physical-controller QA.

**VERIFIED / code:** `TryShowInteractiveSceneFeedback` меняет shell presentation через `ShowText`, а не добавляет feedback как новую запись `DialogueBacklog`; active special mode запрещает stable rollback capture, accepted entry/exit очищают rollback; Save/Load guards сохраняются. Ordinary rollback/save/backlog regressions отдельно Passed; на completion-кадрах остаточных hotspot controls нет.

**NOT VERIFIED:** отдельный объединённый invariant-тест `Hotspot feedback → completion → save → load → backlog/rollback/seen-history comparison` не запускался. В частности, отсутствие всех возможных побочных эффектов при прерывании ещё непрочитанной ordinary typing-строки не доказано: `TypeText` использует обычный `MarkDisplayedLineSeen`. Это ограничение покрытия, не воспроизведённый release blocker и не основание для speculative production correction. Simple marker artwork сознательно оставлен как принятый non-blocking polish.

## 3. Свежий Windows standalone

**VERIFIED / executed:** normal Windows x86_64 build по штатному CLI-подходу pass8, из текущего checkout, configured scenes `MainMenu` и `VNPrototype`:

```text
Unity.exe -batchmode -nographics -quit -projectPath D:\How_I_Fall\develop -buildWindows64Player <external-output>\HowIFall.exe -logFile <external-output>\unity-build.log
```

`-nographics` применялся только к build, не graphical E2E и не standalone executable. Build завершился `Build Finished, Result: Success`, exit 0. Архивированный Bee build input подтверждает `Architecture=x64`, `Development=False`, `AllowDebugging=False`, `ServerPlayer=False`. Найдены executable, `HowIFall_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`, D3D12 и crash handler. Build binaries/debug information/proof остаются вне репозитория.

Запущен именно свежий `Standalone\HowIFall.exe`, без Unity Editor и с отдельным `Standalone\Player.log`. Процесс PID `12600` оставался живым и `Responding=True`; лог подтверждает Direct3D 11/Intel UHD 630, инициализацию engine/input/SceneFlowManager/SaveManager/GameState. Запуск был скрытым, `MainWindowHandle=0`; **это не доказательство корректно отрисованного Main Menu**.

Обычный `CloseMainWindow` скрытого процесса вернул false. Для завершения отправлен `WM_CLOSE` в принадлежащее только этому проверенному process/executable Unity-окно; процесс завершился, лог содержит штатные Physics/Input/CodeReload cleanup. Force-kill не применялся. Numeric player exit code отдельно **NOT VERIFIED**; выход через player-facing пункт меню/Alt+F4 **NOT RUN**. Native shutdown использовался только для cleanup процесса, не для подмены недоступного visual/input proof.

### Player.log

В сохранённом логе запуска и завершения **не найдено** `Error`, `Exception`, `Assert`, `NullReferenceException`, failed scene/asset load или permission/save-path failure. Строка `D3D12 API denied by user filter` относится к штатному выбору GPU API с успешным Direct3D 11 fallback, не отказу доступа к сохранениям.

**NOT VERIFIED / blocker доказательства:** standalone Main Menu render, New Game → ordinary Reading, fonts/assets/Reading layout и standalone-only input/visual defects. В сессии нет desktop-control tool, `unity status` сообщает `STATUS_NO_INSTANCES`; Pipeline не устанавливался и Packages не менялись. Editor evidence не переносится на standalone. Следующий ограниченный шаг — standalone visual smoke в доступной desktop-control среде, **без новой Hotspot route** и без повторного UI polish.

## 4. Findings, сохранность и ограничения

| Класс | Наблюдение / решение |
|---|---|
| A — objective product blocker | В выполненном scope не воспроизведён; production correction/regression diff отсутствуют |
| B — cosmetic/subjective | Simple Hotspot marker artwork и финальный art/style approval отложены; никаких beautification изменений |
| C — infrastructure / test observation | Licensing `Access token is unavailable` не препятствовал успешным Unity запускам; штатные негативные corrupt-save/profile/read-only-fixture сообщения smoke не выданы за пользовательскую потерю данных |
| C — suite-only warning | Полный PlayMode содержит `There are 2 event systems in the scene`; в пяти свежих graphical runtime logs это предупреждение не повторилось. Root cause именно этого suite warning **NOT VERIFIED**; production дефект не воспроизведён, scenes/EventSystem не переделывались |
| C — Editor shutdown | `JobTempAlloc`/memory-leak diagnostics при закрытии graphical Editor; в standalone Player.log аналогичной ошибки не найдено, production leak этим не доказан |
| C — proof gap | Недоступный standalone desktop-control: partial readiness, не продуктовый баг и не повод добавлять runtime QA routes/framework |
| D — new product/canon/architecture | Нового решения не требуется и не принималось |

Исходные dirty/untracked файлы checkout защищены stash `323e475960b5312d00a62e28b029057dcb0cd0fd` и external backup. После QA/build новые generated YAML whitespace, TMP kerning/material, URP/build prefilter и settings reserialization сохранены в `qa-generated.patch` и отдельном stash `aac2aa2e36bbf340f3de6e204af98a3e4671cbbb`. Исходный stash применён обратно; Git binary patch совпал с initial patch по SHA-256, оба первоначальных untracked sentinels восстановлены из backup с byte-identical SHA-256. Stashes сохранены. В итоговом worktree остаются **исходные** локальные изменения, не task implementation; они не staged.

До Unity запусков весь существующий LocalLow каталог `C:\Users\roman\AppData\LocalLow\Bladgrif\How I Fall` скопирован вне repository, создан hash manifest. QA изменил исходные `Saves\Auto\auto_02.json` и `auto_02.png` (startup autosave может предшествовать переключению runner на temporary directory). Эти QA-версии сохранены отдельно, оригиналы адресно восстановлены. Все исходные JSON save/profile и файлы `Saves` сверены с initial SHA-256 после закрытия player. Новый Unity shader analytics JSON не удалялся; он не пользовательское сохранение. Sentinel ManualSave с default `saveDirectory` не означает, что все его test saves выполнялись в пользовательской директории.

**NOT RUN:** physical gamepad, длительный soak, cold-Library rebuild, human aesthetic QA, standalone New Game interaction и полноценный standalone visual smoke. Синхронизация Drive roadmap/capability map и merge принадлежат reviewer; здесь не выполнялись. Baselines не обновлялись только ради recapture.

## 5. Локальный evidence и review

Архив вне repository:

`D:\Codex\visualizations\2026\10\03\01a101a0-4741-7290-bd53-9d59cd3be378\rc-audit\`

- `compile-preflight.log`; `EditMode/`, `PlayMode/`, `Smoke/` с сохранёнными log/XML.
- `{PlayerUi,GameMenu,ManualSave,SaveBackendV2,Hotspot}/`: соответствующий fresh sentinel/log, `screenshots/` originals, `inspection/manifest.json` и просмотренные previews.
- `screenshot-evidence-manifest.json`: все 140 PNG, SHA-256 и признак inspection; 72 inspected, остальные не выдаются за просмотренные.
- `Standalone/`: executable/build payload, `unity-build.log`, `build-inputdata.json`, `Player.log`.
- initial/generated patches и LocalLow backup/manifest/сохранённые QA autosaves.

Evidence локальный, **не опубликованный PR artifact**; удаление этого архива потеряет первичное доказательство. Root sentinels checkout снова исходные: текущие результаты следует читать из архива.

### Поимённый индекс просмотренных кадров

Ниже stems; если не указано иначе, суффикс `_1920x1080.png`. Полные имена также в manifests.

- **PlayerUi, 32 при 1920×1080:** `main_menu_disabled_continue`, `main_menu_normal_enabled`, `main_menu_hover`, `main_menu_pointer_exit`, `main_menu_keyboard_focus`, `main_menu_preferences`, `main_menu_preferences_resolution_open`, `preferences_dirty`, `preferences_applied`, `gameplay_dialogue_standard`, `gameplay_dialogue_typing`, `gameplay_dialogue_named_speaker`, `gameplay_dialogue_long_125pct`, `gameplay_quick_save_feedback`, `gameplay_quick_menu_hover`, `gameplay_choice_two`, `gameplay_choice_hover`, `gameplay_choice_four_long`, `gameplay_backlog`, `gameplay_backlog_long_scroll`, `gameplay_backlog_empty`, `gameplay_reading_after_backlog_close`, `gameplay_auto_active`, `gameplay_skip_active`, `gameplay_hide_ui`, `game_menu_root`, `game_menu_embedded_save`, `game_menu_embedded_load`, `gameplay_preferences`, `gameplay_after_game_menu_close`, `main_menu_load`, `main_menu_quit_confirmation`; **3 при 1280×720:** `gameplay_choice_four_long`, `gameplay_dialogue_long_125pct`, `gameplay_backlog`.
- **GameMenu, 6 при 1920×1080:** `game_menu_root`, `game_menu_pointer_hover_quit`, `game_menu_pointer_off_rows`, `game_menu_return_focus_after_save_load`, `gameplay_reading_after_rollback`, `gameplay_quick_menu_rollback_choice_ready`; **2 при 1280×720:** `game_menu_root`, `game_menu_embedded_save`.
- **ManualSave, 7 при 1920×1080:** `manual_save`, `gameplay_load_confirmation`, `gameplay_overwrite_confirmation`, `gameplay_delete_confirmation`, `gameplay_invalid_save_slot`, `save_load_strip_hover_transfers_selection`, `save_load_delete_hover_ownership`; дополнительно `manual_save_1280x720.png`.
- **SaveBackendV2, 4 при 1920×1080:** `save_load_manual`, `save_load_auto`, `save_load_quick`, `save_load_manual_page_10`.
- **Hotspot, все 17:** `hotspot_after_menu_return`, `hotspot_after_unlock`, `hotspot_completion_reading_restored`, `hotspot_game_menu_save_load_disabled`, `hotspot_initial_authored_background`, `hotspot_keyboard_focus_moved`, `hotspot_locked_door_focus`, `hotspot_showcase_after_laptop`, `hotspot_showcase_completion_reading`, `hotspot_showcase_door_locked_focus`, `hotspot_showcase_door_unlocked`, `hotspot_showcase_game_menu`, `hotspot_showcase_initial`, `hotspot_showcase_menu_return`, `hotspot_showcase_notes_focus` при 1920×1080; `hotspot_reentry_fallback_background_1280x720.png`, `hotspot_showcase_1280x720.png`.

PR отправляется против `master`, без merge. Для docs-only diff допускается пропуск Unity jobs; mandatory exact-head `CI Gate` проверяется после push и указывается в итоговом отчёте/PR, не подменяется local PASS. Reviewer должен сохранить **PARTIAL standalone gate**, проверить реальный commit/diff/CI и только затем синхронизировать roadmap. Этот документ не содержит самоссылочного head SHA: точный head фиксируется Git/PR и финальным отчётом.
