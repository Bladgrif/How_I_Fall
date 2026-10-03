# Аудит транзакции Save и атомарности failed-load — 2026-10-03

## Base и итог

- Exact base: `fad0add28123ba2b10ff067c0354aeb793442129`.
- Итог: **BOUNDED CORRECTION / REVIEW CANDIDATE**, не `DONE`.
- H1 и H2: **REPRODUCED**, исправлены и покрыты регрессиями.
- Использован warm `D:\How_I_Fall\develop`. Первоначальные несвязанные dirty/untracked файлы сохранены побайтно и исключены из commit. Нового worktree не создавалось.
- Reviewer-visible evidence: [Drive — Save transaction atomicity](https://drive.google.com/drive/folders/10-r9iLDQHwAX1LmxX1rXH09J_abqV2bp). Manifest связывает proof с task head SHA и SHA-256 файлов; ZIP содержит red/green XML, seam-only patch и выдержки smoke/graphical log. Пользовательских save-файлов в proof нет.

## REPRODUCED: H1 — overwrite publication

Сначала выполнены тесты на исходном production-поведении base, с двумя `File.Copy`, без исправления. Добавлены только editor-only hooks перед preview publication и после неё. Отдельная Windows-проверка использует реальный `FileStream` JSON с `FileShare.Read`, без hook и без timing race.

Точная граница: preview B уже опубликован, JSON B ещё не опубликован. Исключение `IOException` в этой точке либо sharing violation при записи JSON приводит к `SaveSlot=false`.

| Файл | До overwrite | После failed overwrite на base |
|---|---|---|
| `slot_01.json` | Generation A: scene `publish`, `line-0` | Те же байты A |
| `slot_01.png` | Красный preview A, 1571 байт | Синий preview B, 1573 байта; первый различающийся байт — индекс 36 |
| `slot_01.json.tmp`, `slot_01.png.tmp` | Нет | Удалены исходным `finally` |

Такая же потеря соответствия A JSON / B preview воспроизведена sharing violation, а не только injected exception. Исходный `ReadSlot` валидирует JSON/identity/имя preview, но не проверяет generation preview — это **VERIFIED / code**, не отдельная гарантия runtime-проверки целостности PNG.

### Минимальная коррекция

`SaveManager` сохраняет slot-local `.bak` копии старых JSON/preview до publication; `.png.new` отмечает отсутствие старого preview. Новые данные остаются sibling `.tmp` файлами. Preview публикуется rename/replace, JSON — последним: потребление `json.tmp` является commit boundary. Не изменены `SaveData`, версии, публичные API, final filenames и validation `previewFileName`.

- При исключении до commit восстанавливается старая пара, включая отсутствие preview и точные байты corrupt target.
- При interruption до commit первый `ReadSlot` восстанавливает старую пару до validation/Continue ranking. При недоступном recovery слот fail-closed, а backups/commit signal не уничтожаются.
- После commit сохраняется B; cleanup-only artifacts не делают завершённый save недоступным. Пока старый backup нельзя удалить, новый overwrite отклоняется **до** изменения staging/данных.
- `DeleteSlot` очищает также slot-local backup/restore artifacts. Это узкие helpers в существующем `SaveManager`, не filesystem framework, database или отдельная journaling subsystem.

В промежуточной реализации отдельный red-test поймал poisoned `ReadSlot` при заблокированном удалении committed `.bak`; это исправлено до final validation. Его red XML опубликован отдельно от red исходного base.

Windows native replacement имеет документированные failure states с отсутствующим destination; поэтому JSON также сохраняется в backup. Это учтено отдельным simulated missing-destination recovery test, но не выдаётся за воспроизведённый native error 1176. Ограничения платформы: [Microsoft — ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).

## REPRODUCED: H2 — failed in-place load

Editor-only exception внедрён в конце `RestoreFromGameState`, после реальных target dialogue/choice/presentation mutations, но до сообщения о завершённой restoration. `LoadSlot` возвращает `false`.

Точный red residue для stable ordinary Reading:

- GameState вернулся к `load-current/line-1`, но `controller.sceneData` остался `load-target`.
- Visible text — `T`; `currentFullText` — `Target result`; `isTyping=true`.
- Остались target background/character и runtime `pendingNextScene=load-next`.
- Current Skip был выключен target unread line (`skip=false`), хотя до попытки был включён.
- Во втором сценарии target choice prompt уже показан: `targetSeen=true`, focus=`Choice 1`, choice panel активен, при этом старый Game Menu всё ещё открыт.
- Само очищение rollback не признано дефектом: это intentional accepted-load hard barrier.

### Коррекция и контракт fallback

`TryApplyInPlace` дополнительно захватывает fallback текущего Reading. После возвращения GameState/backlog восстанавливает display существующим restore path и применяет уже имеющийся `RollbackPresentationSnapshot`. Mark-seen updates откладываются до успешного завершения restore; failed restore их не публикует. Fallback не добавляет seen keys и checkpoints.

**Восстанавливается:** scene/line identity, все persisted numeric/choice поля, visible dialogue и `currentFullText`, speaker/name box, background/character sprites и geometry, music clip/playing, final-choice/pending-scene presentation, backlog, прежние логические Auto/Skip настройки и прежний доступный focus. Modal/special-mode ownership не передаётся target; pending SaveManager fields/snapshot очищены. Subsequent advance/save/load и retry работают.

**Намеренно не переносится:** прежняя rollback history, elapsed Auto/Skip timers, точный typewriter progress, позиция воспроизведения music и transient notification progress. Fallback возвращает стабильный текущий beat; timers запускаются заново с полной задержкой. Preferences/History/modal/special-mode state не сериализуются в SaveData; новый transient persistence contract не введён. Это соответствует ограничениям `suspend_resume_feasibility.md`; Suspend/Resume остаётся **DEFER**.

## DISPROVED — ограниченные отрицательные результаты

- Исходный empty-slot failure после preview publication не создаёт loadable JSON: остаётся orphan PNG, но Continue его не выбирает; delete/retry успешны. Это не объявлено отдельным исходным дефектом.
- Temp-only future-dated record не outrank'ит опубликованный валидный save.
- Очищенная rollback history при accepted load не является failed-load atomicity bug.
- Не сделано заявления, что H1/H2 полностью опровергнуты: обе основные гипотезы подтвердились.

## VERIFIED / executed

Unity `6000.5.7f1`, Windows Editor; ни тесты, ни graphical runner не запускались с `-nographics`.

| Проверка | Final результат |
|---|---|
| Compile/import | PASS; в том числе отдельный graphical preflight |
| `PlayMode -TestFilter 'SavePublication_\|FailedLoad_'` | **9/9**, failed=0, skipped=0 |
| `PlayMode -TestFilter RollbackBackendPlayModeTests` | **32/32** |
| Full EditMode | **40/40** |
| Full PlayMode | **102/102** |
| `HowIFallCiSmokeTests.RunAll` | **30/30 групп**, включая Save backend v3 и Backlog restoration |
| `SaveBackendV2` graphical E2E | **PASS**, exit=0; 12 fresh PNG, все 12 просмотрены |
| Task-scoped `git diff --check` | PASS |

Red на base + seam-only patch: **8 тестов, 7 failed, 1 passed**; тот же regression set стал green. Дополнительная cleanup regression: red на промежуточном исправлении → final green. Исправлены две fixture-isolation ошибки нового запуска: старый тест оставлял синтетическую `VNPrototype`, и focus test создавал второй EventSystem вместо использования текущего. Production UI для этого не менялась.

Publication matrix: **24 комбинации** — Manual/Auto/Quick × before/after preview × empty/valid/corrupt/valid-without-preview. Во всех final случаях проверены disk bytes/absence, loadability, retry и delete. Отдельно проверены real Windows sharing violation, reconstruction точных interrupted boundary bytes, recovery на чтении, отсутствие JSON destination и committed-cleanup refusal. Две Windows-sharing проверки сознательно `Ignore` на других Editor platforms; их нельзя учитывать как Linux CI PASS.

В существующем smoke исполнены v1 Manual compatibility, v1 denial вне Manual, v2/backlog compatibility, выбор corrupt rotation target перед valid oldest, newest-valid Continue fallback, capacities и выборочные delete failure tests. Graphical runner исполнил публичные Auto/Quick rotation и Manual/Quick/Continue restore без новых autosave.

### Graphical proof

В существующий `SaveBackendV2PlayModeE2ERunner` добавлен один bounded failed-load probe на реальном Reading result: target choice prompt действительно мутирует UI, restore падает, исходный result/backlog/commands возвращаются. Неожиданные runtime errors не подавляются; исключён только точный ожидаемый probe error внутри scoped flag.

Публикуются пять 1920×1080 PNG: `save_load_manual_failed_load_before`, `save_load_manual_failed_load_after`, Manual/Auto/Quick. До/после сохраняются текст и art; исчезновение текущего quick-save toast — transient, не потеря saved state. Clipping/overlap/missing assets/ошибочной visibility в просмотренных кадрах не найдено. Остальные семь кадров штатного launcher также просмотрены; новые adaptive-resolution сценарии не добавлялись. Accepted visual baselines не менялись ради доставки proof.

### LocalLow и несвязанные изменения

До QA сохранены inventory, backup и SHA-256 всех **16** файлов нормального HIF `Saves`. Новые regressions используют `ConfigureSaveDirectoryForTests`, изолированные read-history keys и `autoSave=false`. После всех запусков **16/16 исходных hashes совпали**, изменений/new files в normal saves нет; восстановление пользовательских saves не потребовалось. QA-generated дельты поверх первоначальных dirty assets/TMP/ProjectSettings возвращены из pre-audit копий; все первоначальные hashes сверены. Пользовательские изменения не staged и не удалены.

## VERIFIED / code

Не изменены SaveData v3 schema/version, 60/6/6 capacities, Quick Load family semantics, sceneId/lineId resolution, v1/v2 controlled compatibility, strict `previewFileName`, special-mode/Replay deny policy, scenes/prefabs, `.meta`, Packages, ProjectSettings и story/flags. Изменено только восстановление failed publication/load; новые transient или suspend records не создаются.

## NOT VERIFIED / NOT RUN и оставшиеся риски

- **NOT RUN:** standalone build/player, physical power-loss и принудительное завершение процесса внутри native syscall. Interruption проверена deterministic reconstruction файлов, а не flaky kill timing.
- **NOT VERIFIED:** все возможные disk/media/ACL failures, filesystem durability при потере питания, другие player platforms. Невозможность записать recovery оставляет backups и fail-closed slot; гарантии восстановления уничтоженного носителя нет.
- После завершения graphical Editor есть два `JobTempAlloc` shutdown warnings; происхождение **NOT VERIFIED**, не исправлялось в bounded Save pass. Во время Save/VN probe непредусмотренных runtime errors не зарегистрировано.
- На момент записи отчёта PR exact-head CI ещё **NOT RUN**; после push его фактический статус сообщается в PR/финальном отчёте отдельно от local PASS. Reviewer acceptance/roadmap sync/merge — **NOT RUN**, coding-agent не merge'ит PR.

## Changed files

- `Assets/HowIFall/Scripts/Save/SaveManager.cs`
- `Assets/HowIFall/Scripts/VN/VNDialogueController.cs`
- `Assets/HowIFall/Tests/PlayMode/RollbackBackendPlayModeTests.cs`
- `Assets/HowIFall/Editor/SaveBackendV2PlayModeE2ERunner.cs`
- Этот отчёт.
