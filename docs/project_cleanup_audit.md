# How I Fall — Project Cleanup Audit

## Goal

Minimal technical classroom demo: сохранить рабочий контур `MainMenu` → `VNPrototype`, текущий classroom dialogue graph и regression coverage; удалить старый сюжет, Unity template leftovers и только доказанно неиспользуемые prototype assets.

Baseline до зачистки: `907abdb34fa32aa05c648f2c14af462b3052478c`.

## KEEP

- `Assets/HowIFall/Scenes/MainMenu.unity` и `VNPrototype.unity` — единственные enabled build scenes и текущий переход New Game / Continue / Load.
- `Assets/HowIFall/Data/Dialogues/classroom_first_lesson.asset`, `classroom_choice_investigate.asset`, `classroom_choice_ignore.asset` — достижимый classroom-граф, используемый New Game и обоими Play Mode E2E.
- `Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset` — registry для runtime и save validation.
- `Assets/HowIFall/Data/Dialogues/ui_test_scene.asset` — зарегистрированная техническая сцена; нужна для legacy-save/UI compatibility, поэтому не удалялась.
- `Assets/HowIFall/Prefabs/UI/ManualSaveLoadPanel.prefab` и все связанные runtime UI/scripts — используются обеими сценами и E2E.
- `Assets/HowIFall/Editor/` — smoke tests, Save Backend v2 E2E, Manual Save E2E, project/scene/content validators и используемые repair/build tools.
- `Assets/HowIFall/Art/Backgrounds/classroom_day.png`, placeholder-персонаж и сериализованные MainMenu/VN/Settings sprites — имеют прямые GUID references из текущих assets/scenes.
- `Assets/TextMesh Pro/`, `Assets/Settings/`, `Packages/`, `ProjectSettings/`, `.github/workflows/unity-ci.yml` — runtime rendering/UI, package/project configuration и CI.
- `docs/technical_plan.md`, `docs/eternum_feature_tracker.md`, `docs/save_system_eternum_reference.md`, `docs/editor_builders_replacement.md` — техническая документация; устаревший save reference явно помечен историческим.

## REMOVE

| Path | Причина удаления | Проверенные зависимости |
|---|---|---|
| `docs/story/**` | Сюжет текущей версии объявлен obsolete и будет написан заново. | Runtime/Editor/technical docs не загружают Markdown; ссылки в README и technical plan обновлены. |
| `docs/archive/old_story/**` | Архив ещё более ранней версии старого сюжета. | Runtime/Editor references отсутствуют. |
| `Assets/TutorialInfo/**`, `Assets/Readme.asset*`, `Assets/Editor.meta` | Остатки Unity URP Empty Template, не часть How I Fall. | Вся GUID-цепочка замкнута внутри template Readme; build scenes её не используют. |
| `.idea/**` | Tracked IDE-generated metadata при уже существующем правиле `.idea/` в `.gitignore`. | Не участвует в Unity/runtime/CI. |
| `Assets/HowIFall/Art/Backgrounds/school_*` | Фоны старого сюжетного прототипа. | Ноль serialized GUID refs; classroom demo использует `classroom_day.png`. |
| `Assets/HowIFall/Art/UI/Settings/Doodles/**`, `settings_doodles.png*` | Неиспользуемые prototype decoration assets. | Ноль serialized GUID refs и path references. |
| `Assets/HowIFall/Art/UI/VN/Icons/**`, `vn_menu_button.png*`, `vn_quick_*.png*` | Старые графические варианты quick menu после возврата к текстовым кнопкам. | Ноль serialized GUID refs; code/path references отсутствуют. |
| `Assets/HowIFall/Audio/SFX/ui_click.mp3*`, `ui_hover.mp3*` | Отключённые prototype UI sounds. | Ноль serialized GUID refs; runtime не загружает их по имени/пути. |
| `docs/screenshots/save_load_ui/manual_save_polished_*.png` | Устаревший набор generated QA screenshots, не создаваемый текущим runner. | References и screenshot comparisons отсутствуют. |
| `manual_save_playmode_result.txt`, `save_backend_v2_playmode_result.txt` | Локальные E2E sentinels. | Runner создаёт их заново; добавлены точечные `.gitignore` rules. |
| Корневые `*.log` | Игнорируемые локальные Unity build/test logs. | Удалён 161 старый и 7 post-cleanup test logs после переноса результатов в этот отчёт; source/assets не затронуты. |

Удалено 127 tracked-файлов, 2 untracked E2E result-файла и 168 ignored root logs (старые logs занимали 7 416 420 bytes). Для удалённых Unity assets проверено 37 GUID: оставшихся references на них нет. Каждый удалённый Unity asset удалён вместе с `.meta`.

## Resolved findings

- `ProjectSettings/EditorBuildSettings.asset`: после cleanup audit stored GUID сцены `MainMenu` исправлен с `e9c0930f6246da6418a08316898a237c` на `5cddddbc4dfd1fe4ebb0b5f815c3ee94` и теперь совпадает с `Assets/HowIFall/Scenes/MainMenu.unity.meta`. GUID `VNPrototype` не изменялся и уже совпадал.

## REVIEW / NOT DELETED

| Path | Почему подозрительно | Почему пока оставлено |
|---|---|---|
| `Assets/HowIFall/Data/Dialogues/ui_test_scene.asset` | Не входит в New Game classroom-граф. | Есть в `DialogueSceneRegistry`, используется legacy-save compatibility и поддерживается `VNUITestSceneContentBuilder`. |
| `Assets/HowIFall/Art/Backgrounds/demo_vn_background.png` | Нужен только технической UI-сцене. | Прямая GUID-ссылка из сохранённого `ui_test_scene`. |
| `Assets/HowIFall/Editor/VNUITestSceneContentBuilder.cs` | One-shot prototype builder. | Поддерживает зарегистрированную review-сцену; безопасность удаления не доказана. |
| `Assets/HowIFall/Editor/VNPrototypeAudioListenerBuilder.cs`, `VNPrototypeDebugStatsBuilder.cs` | Конфигурационные builders уже применены к сцене. | Могут быть repair tools; удаление не требуется для runtime и не доказано безопасным. |
| `GameState` / `SaveData` / `DialogueChoice` story-like fields (`lust`, `romance`, `purity`, trust и т. п.) | Часть полей пришла из старого сюжета. | Поля участвуют в save schema, demo choice state и regression tests; удаление потребовало бы migration/runtime refactor. |
| `docs/screenshots/save_load_ui/{manual_save,save_load_*}.png` | Generated graphical E2E output, не assertion baselines. | До задачи имели локальные изменения и перезаписываются обязательными graphical tests; не удалялись в high-risk cleanup. |
| `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json` | Настройки пакета, которого нет среди direct dependencies. | ProjectSettings не удалялись без проверки Unity import; риска для минимизации не создаёт. |
| `com.unity.collab-proxy` и широкий набор Unity modules | Возможно, часть пакетов не нужна маленькой demo. | Package removal меняет project resolution и выходит за безопасный DELETE-only scope. |

## Story cleanup

- Полностью удалены `docs/story/**` и `docs/archive/old_story/**`.
- README больше не объявляет старый сюжет каноном и не содержит битых ссылок на удалённые главы/персонажей/routes.
- `technical_plan.md` фиксирует, что новый `docs/story/` будет создан с нуля после утверждения канона.

## Runtime cleanup

- Runtime scripts, build scenes, registry и save schema не переписывались.
- Ни один `DialogueSceneData` не удалён: три classroom assets образуют достижимый demo-граф, четвёртый registry asset оставлен в REVIEW.
- Legacy-классы не удалялись по одному только отсутствию C# references: scene/prefab/ScriptableObject GUID checks выполнены.

## Assets cleanup

- Удалены только disconnected prototype backgrounds, disabled UI SFX, старые quick-menu sprites, settings doodles и Unity template assets.
- Все оставшиеся runtime art/UI assets имеют serialized reference либо входят в сохранённую review-сцену.

## Test impact

- Сохранены все зависимости `HowIFallCiSmokeTests.RunAll`, Manual Save Play Mode E2E, Save Backend v2 Play Mode E2E, scene validation и project validator.
- `classroom_first_lesson` и обе choice-сцены остались в registry и сохраняют New Game / Save/Load regression path.
- Полный post-cleanup прогон выполнен последовательно. Первая попытка Save Backend v2 E2E не дошла до Play Mode из-за transient file lock на generated screenshot; остальные проверки продолжены, отдельный повтор этого E2E прошёл полностью.

## Post-cleanup regression report

| Проверка | Результат | Evidence |
|---|---|---|
| Compile/import | PASS | Unity 6000.5.7f1, exit 0, `error CS` / compilation failure отсутствуют (captured `cleanup_compile.log`, затем удалён как generated). |
| Graphical Manual Save Play Mode E2E | PASS | 16 scenario PASS markers; `COMPLETE PASS`; result sentinel `status=PASS` (`cleanup_manual_save_e2e.log`). |
| Graphical Save Backend v2 Play Mode E2E | PASS on retry | 19 scenario PASS markers; `COMPLETE PASS`; result sentinel `status=PASS` (`cleanup_save_backend_v2_e2e_retry.log`). Первая попытка: `UnauthorizedAccessException` при удалении предыдущего generated PNG, не runtime assertion. |
| `HowIFallCiSmokeTests.RunAll` | PASS | Dialogue backlog, VN settings presenter и Save backend v2 smoke tests passed (`cleanup_ci_smoke.log`). |
| Scene/manual-save installation validation | PASS | Обе сцены `VALID`; managers=1, panels=1, slots=6 (`cleanup_scene_validation.log`). |
| `HowIFallProjectValidator.ValidateProject` | PASS | `How I Fall validation passed` (`cleanup_project_validator.log`). |
| Missing Script / invalid UnityEvent / Console audit | PASS with environment warnings | Для обеих сцен `missingScripts=0`, `invalidEvents=0`; C# compile errors отсутствуют. В логах есть non-blocking Unity Licensing/D3D12 warnings. |
| `DialogueSceneRegistry`, New Game, MainMenu → VNPrototype | PASS | Project validator проверил registry; оба graphical E2E стартовали из MainMenu через New Game и завершили VN/Save/Continue сценарии. |

## Resulting project scope

После cleanup проект содержит две runtime-сцены, один classroom demo-граф с двумя короткими исходами, зарегистрированную техническую UI compatibility scene, VN runtime, Manual/Auto/Quick Save/Load, Continue, History, Settings, validators и regression tests. Новый сюжет отсутствует намеренно.
