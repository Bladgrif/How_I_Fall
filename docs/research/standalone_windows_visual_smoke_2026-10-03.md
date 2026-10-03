# Визуальная проверка Windows standalone — 2026-10-03

## Результат

**PASS для запрошенного standalone proof / NO PRODUCTION CHANGE.** На точном source SHA `a1a5e6dccd94fffb35ec3884e844c95bd6b92bd0` свежая Windows x86_64 сборка запущена как отдельный player, не Unity Editor. Main Menu → `Новая игра` → обычная Reading отрисовались и проверены на реальном рабочем столе. Явных standalone-only проблем не обнаружено.

## Среда и сборка

- Unity `6000.5.7f1` (`017862109af0`), Windows 10 Pro x64.
- Build выполнен Unity BuildPipeline CLI из чистого detached worktree `C:\Temp\HIF-standalone-visual-smoke`, `HEAD` совпадает с проверенным SHA выше. Worktree исходного `D:\How_I_Fall\develop` содержал посторонние локальные изменения и для сборки не использовался.
- Target: normal Windows x86_64, non-development; команда использовала `-buildWindows64Player` без Development Build флага.
- Build result: `Build Finished, Result: Success`; Unity завершилась с кодом `0`. В compile output есть предупреждения об устаревших API; ошибок компиляции и build failure нет.
- Executable: `C:\Temp\HIF-standalone-rc\HowIFall.exe`; рядом присутствуют `HowIFall_Data`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`, `MonoBleedingEdge` и `D3D12`.
- Build log: `C:\Temp\HIF-standalone-rc\unity-build.log`.
- SHA-256 standalone executable: `D05C5848F7CC2EB32FF6016F54762EE3C666A69B862BE297DF2390830C1EA8CF`.

## Проверка standalone player

Player запущен из собранного `HowIFall.exe`; процесс `HowIFall` отвечал (`Responding=True`). Реальное окно было видно на рабочем столе 1920×1080; игровая область окна отображала 1600×900.

- Main Menu: фон холла, логотип и пять строк меню (`Продолжить`, `Новая игра`, `Загрузить`, `Настройки`, `Выйти`) видны; русский шрифт/glyphs читаемы. Явных clipping, overlap, missing assets/materials или дефекта масштаба не найдено.
- `Новая игра` нажата мышью в standalone-окне. Player перешёл в ordinary Reading (`classroom_first_lesson` подтверждена в `Player.log`). Видны фон класса, текст реплики, индикатор продолжения и Quick Menu. Текст читаем; явных missing font/texture/material, clipping, overlap или standalone-only input/layout дефекта не обнаружено. Персонаж на этом кадре отсутствует.
- Скриншоты захвачены с реального desktop после отрисовки и лично просмотрены: `C:\Temp\HIF-standalone-rc\proof\main-menu.png`, `C:\Temp\HIF-standalone-rc\proof\new-game-reading.png`. Также создан диагностический снимок desktop до старта: `desktop-probe.png`; он не используется как proof игры.
- Player закрыт обычным `Alt+F4`; после закрытия процесс отсутствовал в списке процессов. Числовой exit code получить не удалось: процесс запускался отдельно для desktop interaction, его код не был сохранён оболочкой.

## Player.log и сохранения

- Проверен `C:\Temp\HIF-standalone-rc\player.log`, SHA-256 `2E1D2DBBF256C42545C1F032718F7F56176FB2B6DA274516AE8D1349723C267F`.
- В этом логе нет `Error`, `Exception`, `Assert`, `NullReferenceException`, ошибок загрузки сцен/ресурсов или доступа к save path. `classroom_first_lesson` загружена. Существующие Unity/driver строки про выбор D3D11 не повлияли на отображение.
- До запуска сохранения LocalLow были инвентаризированы и захэшированы. `Новая игра` перезаписала `Saves\Auto\auto_02.json` и `.png`; оба пользовательских файла восстановлены из внешней копии, SHA-256 совпал с исходными. После восстановления полный набор JSON/PNG save-файлов совпал с pre-run hashes. Backup находится вне репозитория: `C:\Temp\HIF-standalone-rc\save-backup`.

## Ограничения и verdict

- Production changes: **NONE**. Visual baselines не менялись.
- Build-output warnings об obsolete C# API не относятся к обнаруженному standalone-дефекту и не исправлялись.
- Числовой player exit code: **NOT AVAILABLE**; факт завершения после `Alt+F4` проверен.
- Human aesthetic approval, иные маршруты и длительный standalone soak: **NOT RUN**.

**Вердикт: standalone Main Menu → New Game → ordinary Reading visual proof закрыт для проверенного SHA.**
