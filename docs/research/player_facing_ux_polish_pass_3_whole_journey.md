# Player-facing UX polish — проход 3: полный путь игрока

**Исторический статус:** ACCEPTED.

Закрытие прохода: commit `79e9f3e412cb243fd9a98aabf451ecf7194f1616`; обязательный CI run `33203001166` — GREEN.

## Результат аудита

| Приоритет | Наблюдение | Результат |
| --- | --- | --- |
| P1 | Уже открытая Save/Load панель сохраняла масштаб 1920×1080 после переключения на 1280×720, из-за чего `SaveBackendV2` находил clipping. | Исправлено: панель повторно применяет существующий viewport-fit scale, пока открыта. |
| P2 | EventSystem selection в Game Menu был плохо различим на root/alternate-focus proof. | Исправлено: выбранное действие получает узкий HIF-red marker справа. |
| P3 | После исправлений новых объективных дефектов не подтверждено. | Без изменений. |

## Исторические контракты этого прохода

В исходном проходе Quick Menu ещё содержал больше действий. Этот состав **устарел и заменён** текущим компактным player-facing контрактом `История | Пропуск | Авто | Быстр. сох.`. Скрытые маршруты/API/hotkeys не удалены.

Остальные защищённые контракты сохранялись: Game Menu и Esc/back ownership, безопасные confirmation defaults, Character Hub deferral, `SaveData` v3, группы сохранений и пагинация.

## Проверки исходного прохода

- `PlayerJourneyE2ETests`: **PASS, 6/6** после исправления Game Menu.
- `HowIFallCiSmokeTests.RunAll`: **PASS**.
- `PlayerUi` graphical E2E: **PASS**, 17 screenshots, включая `game_menu_root_1920x1080.png` и `game_menu_alternate_focus_1920x1080.png`.
- `ManualSave`: **PASS**, 4 screenshots.
- `SaveBackendV2`: первый запуск FAIL выявил P1 на 1280×720; после исправления повторный запуск PASS, 5 screenshots.
- Всего в проходе было открыто и проверено 26 свежих screenshots. В итоговом proof не осталось clipping, overlap, missing texture, wrong visibility, malformed control или неоднозначного focus Game Menu.

## Scope исходного прохода

Production-файлы: `ManualSaveLoadPanel.cs`, `VNGameMenuView.cs`. QA proof: `PlayerUiGraphicalE2ERunner.cs`, `tools/run-graphical-e2e.ps1`. Сцены, prefab, art и ProjectSettings не менялись.

Этот файл сохраняет историю конкретного прохода. Текущие UX-контракты определяются master, feature tracker и живой reviewer roadmap.
