# PASS 8 — standalone Windows release smoke

**Дата:** 2026-09-02
**Base/current master:** `606cff442bcdeee0aea1fa10b9adaa0c933f54ab`

## Среда и сборка

- Для проверки создан новый чистый detached worktree: `C:\Temp\HIF-pass8-standalone`.
- Unity: `6000.5.7f1` (`017862109af0`).
- Build target: Windows x86_64, normal non-development player.
- Использован штатный Unity BuildPipeline CLI: `Unity.exe -batchmode -nographics -quit -projectPath ... -buildWindows64Player C:\Temp\HIF-standalone-rc\HowIFall.exe`.
- Постоянного build helper/framework в репозитории не найдено и не добавлялось.
- Использован текущий configured build scene list: `MainMenu` и `VNPrototype`.
- Build log: `C:\Temp\HIF-standalone-rc\unity-build.log`.
- Результат Unity: `Build Finished, Result: Success` / return code `0`.

## Артефакты

Выходная папка вне репозитория: `C:\Temp\HIF-standalone-rc\`.

Подтверждены `HowIFall.exe`, `HowIFall_Data`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`, `MonoBleedingEdge` и `D3D12`. Размер standalone-артефактов без логов и proof: около **141.98 MiB**.

## Standalone launch и визуальная проверка

Запущен именно `C:\Temp\HIF-standalone-rc\HowIFall.exe` с отдельным логом `C:\Temp\HIF-standalone-rc\player.log`, без Unity Editor.

Фактически наблюдались:

1. Процесс `HowIFall` запустился, отвечал и показал Main Menu.
2. Main Menu содержит читаемые `Продолжить`, `Новая игра`, `Загрузить`, `Настройки`, `Выйти`; явных missing texture/sprite/font, clipping или overlap нет.
3. Нажатие `Новая игра` открыло обычную reading surface сцены `classroom_first_lesson`; текст, Quick Menu и фон отображаются корректно.
4. Безопасный выход через `Alt+F4` завершил standalone-процесс без crash.

OS-level proof вне репозитория:

- `C:\Temp\HIF-standalone-rc\proof\main-menu.png`;
- `C:\Temp\HIF-standalone-rc\proof\new-game-reading.png`.

Скриншоты инспектированы Codex: square/missing glyphs, missing textures, ошибочная resolution/layout, clipping и overlap не обнаружены.

## Player.log

Проверен `C:\Temp\HIF-standalone-rc\player.log` после закрытия player. Нет `Error`, `Exception`, `Assert`, `NullReferenceException`, failed scene load, missing asset/resource, player-facing shader/material failure или save-path/permission failure.

Строка `Main menu music clip is not assigned.` классифицирована как существующее нефатальное отсутствие назначенного menu music clip: она не является standalone blocker и не сопровождалась ошибкой/исключением. New Game успешно создала autosave в стандартном LocalLow save path; ошибку доступа это не вызвало.

## Generated Unity dirtiness

До Unity worktree был clean. После build Unity автоматически изменила только:

- `Assets/Settings/PC_RPAsset.asset`;
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`;
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`;
- `ProjectSettings/EditorBuildSettings.asset`;
- `ProjectSettings/GraphicsSettings.asset`;
- `ProjectSettings/ProjectAuditorSettings.asset`;
- `ProjectSettings/ProjectSettings.asset`;
- `ProjectSettings/ShaderGraphSettings.asset`.

Это не PASS 8 implementation. В disposable worktree каждый перечисленный путь точечно возвращён к `HEAD`; production-файлы не изменялись и не коммитятся.

## Ограничения

- PASS 8 не заменяет обязательный GitHub CI и reviewer review после push.
- Скриншоты и build binaries остаются вне репозитория; visual baselines не обновлялись, поскольку UI surface не менялась.
- В ходе smoke создано/обновлено autosave-состояние в пользовательском LocalLow save path; оно намеренно не удалялось, чтобы не затронуть потенциальные пользовательские данные.

## Verdict

**REVIEW CANDIDATE.** Windows x86_64 standalone build успешна; executable запускается вне Editor, Main Menu и ordinary reading наблюдались, явного standalone-only blocker и production defect не найдено. Production changes: **NONE**.
