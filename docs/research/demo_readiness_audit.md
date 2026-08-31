# Аудит готовности демо

## Актуальное обновление — 2026-08-31

- Базовый commit: `6e9e6eaa6380c924a9bd0b9ba05c361903191e30`.
- Финальная полировка главного меню: **ACCEPTED**.
- Обязательный CI: run `33386581463` — `Unity Test Framework` SUCCESS; `Unity smoke tests` SUCCESS.
- Финальный player-facing audit: все проверенные пункты PASS; `PlayerJourney` 6/6 PASS; `HowIFallCiSmokeTests.RunAll` PASS; `PlayerUi` graphical E2E PASS.
- Проверка 1920×1080 PASS; дополнительный 1280×720 проход использовался в старом responsiveness-аудите.
- Предыдущая Windows x64 сборка создавалась успешно, но запуск exe был заблокирован средой Codex; поэтому standalone startup не считался PASS без реального запуска.

## Итог на момент аудита

**READY** для ручного review build того состояния проекта. Блокирующих P0/P1 production-дефектов не было подтверждено. Один ранний P1 оказался ошибкой времени screenshot capture во время 0.12-секундной анимации подтверждения, а не дефектом production UI.

## Что было корректно отложено

- реальный сюжет, маршруты, канонические flags и authored narrative content;
- финальные фоны, портреты, art direction и visual identity;
- glossary/lore, flowchart, chapter select, endings и canonical replay content;
- content-driven политика autosave вокруг выборов;
- suspend/resume и generic minigame framework;
- обычный player-facing launcher Character Hub при сохранённой технической основе.

Rollback в этом старом аудите был отложен; позднее, 2026-08-31, явный запрос пользователя открыл его для отдельного feasibility-исследования. Реализация всё ещё не разрешена.

## Player Journey

`PlayerJourneyE2ETests`: **PASS, 6/6**. Покрывались маршруты Main Menu → Preferences/Quit/Load return → New Game → gameplay → modal back stack → Manual Save/Load → Quick Save/Load → Continue с fallback при повреждённом самом новом сохранении.

## Save/Load

Проверки подтверждали:
- `SaveData.CurrentVersion = 3`;
- Manual/Auto/Quick;
- preview 384×216;
- корректную обработку invalid/corrupt кандидатов;
- Continue по самому новому валидному кандидату;
- pre-load autosave;
- запрет Save/Load в Replay.

Сценарии `ManualSave` и `SaveBackendV2` проходили. После исправления момента capture подтверждение загрузки было читаемым и стабильным.

## Экран чтения

`PlayerUi` proof покрывал обычный диалог, масштаб 125%, выбор/focus/wrap, Историю, Auto, Skip, Hide UI и gameplay Preferences. Объективных дефектов layout в прошедшем proof не было найдено.

## Graphical proof

Свежий graphical E2E на 1920×1080:
- `PlayerUi` — PASS, 12 обязательных состояний;
- `ManualSave` — PASS, 4 состояния;
- `SaveBackendV2` — PASS, 3 состояния.

Всего было просмотрено 19 свежих screenshots. После исправления capture-ready gate объективных clipping/overlap/missing texture/malformed control/wrong visibility дефектов не оставалось.

## Важное ограничение исторического аудита

Проверка выполнялась на рабочем дереве с уже существовавшими пользовательскими изменениями, поэтому визуальные доказательства могли зависеть от локальных art/settings. Коммитнутый baseline подтверждения был обновлён только после стабильного кадра.

## Результат передачи на Drive

Свежий screenshot подтверждения загрузки был зеркалирован в Drive без создания дубля. В дальнейшем Drive-путь русифицирован: `How I Fall/Визуальное ревью/Текущие скриншоты/Сохранение и загрузка/`.

## Вывод

Этот файл хранит исторический readiness-аудит и не определяет текущий backlog. Актуальные статусы берутся из `docs/eternum_feature_tracker.md`, текущего master и reviewer roadmap на Drive.
