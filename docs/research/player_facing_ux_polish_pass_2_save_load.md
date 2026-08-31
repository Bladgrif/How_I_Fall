# Player-facing UX polish — проход 2: Save/Load

**Исторический статус:** REVIEW CANDIDATE на момент исходного прохода.

## Реализованный на тот момент контракт

- Ручные сохранения используют 10 страниц по 6 карточек: глобальные адреса `1..60`. Страница 1 остаётся `1..6`; существующие имена файлов `slot_01.json`…`slot_06.json` не менялись. Миграция не выполнялась.
- `SaveData.CurrentVersion` оставался `3`.
- Auto и Quick оставались независимыми циклическими группами по шесть слотов (`auto_01..06`, `quick_01..06`). `Continue` учитывал все Manual/Auto/Quick сохранения.
- Общая панель Save/Load открывалась на Manual page 1. При переключении на Auto/Quick строка ручной пагинации скрывалась; возврат в Manual сохранял текущую страницу в рамках открытой панели.
- Пустые карточки показывали только `Пусто`; гигантские фоновые номера и повторные подписи типа сохранения были убраны. Заполненные карточки сохраняли thumbnail, сцену, дату/время и небольшой локальный индекс.
- Удаление оставалось компактным и защищённым подтверждением.
- В историческом responsiveness-проходе панель была адаптирована к 1280×720 без потери шести карточек и навигации.

Позднее Save/Load IA была дополнительно улучшена: текущий утверждённый контракт — Save показывает только Manual, а Load позволяет просматривать Manual/Auto/Quick через одну компактную область навигации. Актуальный контракт всегда брать из текущего master и `docs/eternum_feature_tracker.md`.

## Доказательства исходного прохода

- `SavePaginationEditModeTests`: PASS, 2/2. Проверялись старые Manual stems 1..6, Manual 7/60, отклонение Manual 61, ограничения Auto/Quick и mapping страниц.
- `HowIFallCiSmokeTests.RunAll`: PASS. Проверялись шестислотная ротация Auto/Quick и выбор `Continue` более нового Manual slot 7.
- `PlayerJourneyE2ETests.ManualSaveLoadJourney_FilledSlotRestoresStateAndGameplay`: PASS, 1/1; запись/восстановление Manual page 2 / global slot 7.
- `SaveBackendV2` graphical E2E: PASS; Manual page 1/2, Auto, Quick и responsive Manual page 2.
- `ManualSave` graphical E2E: PASS; Save, Main Menu Load, unavailable state и стабильное подтверждение загрузки с безопасным Cancel focus.
- Было просмотрено 9 свежих Save/Load screenshots. Ранний clipping на 1280×720 был исправлен, после чего оба graphical сценария прошли повторно.

## Curated baselines

На том проходе обновлялись `save_load_manual.png` и `save_load_manual_page_2.png`; `save_load_confirmation.png` оставался репрезентативным. Зеркало Drive в той среде не запускалось из-за отсутствия авторизованного connector.

Этот документ исторический. Он не должен переопределять более новые решения Save/Load IA.
