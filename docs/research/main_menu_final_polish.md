# Финальная полировка главного меню — 2026-08-31

## Статус

**REVIEW CANDIDATE** на момент исходного прохода — автоматические доказательства записаны ниже; это не означало финальное одобрение арта.

## Наблюдения до прохода

Свежий capture 1920×1080 хорошо использовал authored background, но логотип и навигация выглядели разрозненно: группа действий начиналась слишком низко, старое оформление сохраняло text outline, а legacy `Press any button` мог снова появляться после закрытия настроек главного меню.

## Сравнение с референсами

Исследования и примеры **STEINS;GATE**, **The House in Fata Morgana**, **AI: The Somnium Files** и **PARANORMASIGHT** подтверждали общий принцип: один доминирующий key visual, ясно отделённый логотип, короткая навигация, спокойные состояния покоя и небольшой однозначный selected state. HIF использует принцип, но не копирует layout или assets.

## Выбранное направление и изменения

- Сохранён полноэкранный временный non-canon background и левый readability wash.
- Логотип и навигация собраны в более компактную левую композицию; `Выйти` отделён от четырёх обычных действий.
- Убраны outline/cyan treatment. Hover и controller focus используют более яркий текст и небольшой HIF-red marker слева; `Выйти` приглушён в покое.
- Сохранена динамическая primary semantics: `Продолжить` при наличии валидного save, иначе `Новая игра`.
- Existing PlayerUi graphical journey был расширен состояниями Main Menu Load, Quit и root capture 1280×720.
- Legacy `Press any button` остаётся скрытым после возврата из Preferences.

Арт не добавлялся и не менялся; фон оставался временным/non-canon.

## Доказательства исходного прохода

- Pre-change `PlayerUi` graphical E2E: PASS, 2026-08-31.
- Final `PlayerUi` graphical E2E: PASS, 20 screenshots, включая 9 состояний главного меню на 1920×1080 и 1280×720.
- Исправлен объективный дефект: legacy `Press any button` появлялся под Load/Quit после закрытия Preferences.
- Curated baseline: `docs/visual-baselines/main_menu.png`.
- `MainMenuVisualPassASmokeTests.RunBatchMode`: PASS.
- `PlayerJourneyE2ETests`: PASS, 6/6.
- `HowIFallCiSmokeTests.RunAll`: PASS.

Документ исторический; актуальные состояния и дальнейшие решения определяются текущим master, feature tracker и reviewer roadmap.
