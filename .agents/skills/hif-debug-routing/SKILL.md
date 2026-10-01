---
name: hif-debug-routing
description: Маршрутизирует debug-симптомы How I Fall к существующим QA launcher'ам, тестам и graphical E2E без переоткрытия карты инструментов.
---

# Маршрутизация отладки How I Fall

Используй, когда задача — воспроизвести/локализовать дефект фокуса, навигации, runtime-состояния, Save/Load или visual regression, и нужно быстро выбрать правильный существующий инструмент. Не используй для реализации новых фич.

## Правила

- Сначала найди существующий инструмент в таблице ниже; не создавай новый launcher/тест, пока существующий реально не покрывает симптом (см. `AGENTS.md`).
- Названия тестов — фильтры для `tools/run-unity-tests.ps1 -TestFilter` (только EditMode/PlayMode); launcher'ы лежат в `Assets/HowIFall/Editor/`.
- Изменённое поведение покрывай существующим suite'ом по возможности; новый тест добавляй по правилам тестирования из `AGENTS.md`.
- Инструмент не доказывает сам себя: результаты и скриншоты проверяй по `docs/codex/unity_agent_guide.md` (наличие скриншота не является доказательством).

## Симптом → инструмент

| Симптом | Основной инструмент | Дополнительно |
|---|---|---|
| Фокус/навигация главного меню | `MainMenuFocusPlayModeTests`, `MainMenuInteractionStatePlayModeTests` | `MainMenuQaLauncher`, E2E `PlayerUi` (`main_menu_keyboard_focus_*`, hover/focus скриншоты) |
| Фокус/владение вводом Save/Load strip | `SaveLoadFocusOwnershipPlayModeTests` | `Phase5SaveLoadQaLauncher`, E2E `ManualSave` (`save_load_strip_*`, `save_load_delete_*`) |
| Фокус Game Menu / embedded save-load | `GameMenuFocusInteractionPlayModeTests` | E2E `GameMenu` (`game_menu_keyboard_focus_*`, `game_menu_embedded_*`) |
| Preferences/настройки (иерархия, draft, apply) | `PreferencesInteractionPlayModeTests`, `PreferencesControllerResolutionDraftEditModeTests`, `PreferencesApplyDisabledPresentationEditModeTests` | E2E `PlayerUi` (`main_menu_preferences_*`) |
| Сохранения: пагинация, слоты, бэкенд | `SavePaginationEditModeTests`, `SaveSlotFocusPresentationEditModeTests`, `RollbackBackendPlayModeTests` | E2E `SaveBackendV2`, `ManualSave`; контракт — `docs/product/decision_log.md` (2026-08-31) |
| Rollback/rewind | `RollbackCheckpointEditModeTests`, `QuickMenuRollbackButtonEditModeTests`, `RollbackBackendPlayModeTests` | E2E `GameMenu` (`gameplay_quick_menu_rollback_*`) |
| Reading: backlog, история, wheel, auto | `DialogueBacklogEditModeTests`, `HistoryPresentationPlayModeTests`, `WheelReadingRuntimePlayModeTests` | внутренние проверки smoke suite (запускаются только в составе `HowIFallCiSmokeTests.RunAll`, отдельно не фильтруются): `BacklogRestorationSmokeTests`, `AutoDialogueSmokeTests`, `DialogueBacklogSmokeTests` |
| Interactive hotspot / карта | `InteractiveHotspotPlayModeTests`, `InteractiveHotspotEditModeTests`, `MapLocationsPlayModeTests`, `MapLocationsEditModeTests` | `InteractiveHotspotQaLauncher`, `MapLocationsQaLauncher` |
| Сквозной путь игрока (маршрут/состояние) | `PlayerJourneyE2ETests` | E2E `PlayerUi` полный набор proof-скриншотов |
| Настройки вне главного меню | `SettingsPanelWithoutMainMenuPlayModeTests` | `SettingsManagerResolutionEditModeTests` |
| Input map / RMB-навигация | `VNInputMapControllerTests`, `RMBOneLevelRuntimePlayModeTests` | — |
| Visual regression (clipping, overlap, missing asset) | ближайший E2E-сценарий + inspect скриншотов | skill `$hif-visual-qa`; базовые правила — `docs/product/ui_principles.md` |
| Целостность сцен/prefab/проекта | smoke suite: `HowIFallCiSmokeTests.RunAll` | профиль `Smoke` в `tools/run-unity-tests.ps1` |
| Производительность/профилирование | — | отдельного workflow нет; при появлении perf-прохода вернуться к `docs/research/agent_skills_ecosystem_audit_2026-10-01.md` |

## Как запускать

- Таргетные тесты: `tools/run-unity-tests.ps1 -Mode <EditMode|PlayMode> [-TestFilter <ИмяТеста>]` — фильтр передаётся в Unity `-testFilter` как regex; несколько тестов объединяй через `|`, разделитель `;` даёт 0 совпадений.
- Smoke suite: `tools/run-unity-tests.ps1 -Mode Smoke` — без `-TestFilter` (раннер его отклоняет) и всегда целиком: все проверки `HowIFallCiSmokeTests.RunAll`.
- Graphical E2E: `tools/run-graphical-e2e.ps1 -Scenario <ManualSave|SaveBackendV2|PlayerUi|GameMenu>`; запускать без `-nographics`, результат — sentinel-файл в корне репозитория + proof-скриншоты в `QAArtifacts/GraphicalE2E/<Scenario>` (для `GameMenu` — в `QAArtifacts/GraphicalE2E/PlayerUi`).
- Новый QA launcher при реальной необходимости — только по образцу существующих в `Assets/HowIFall/Editor/`; для мелкой кнопки launcher не создаётся (`$hif-visual-qa`).
