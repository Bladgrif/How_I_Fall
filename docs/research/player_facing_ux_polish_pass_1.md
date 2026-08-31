# Player-facing UX polish — проход 1

**Исторический статус:** REVIEW CANDIDATE на момент исходного прохода.

Функциональная база была стабильной, но ручной runtime-review повторно открыл polish главного меню, общих настроек и обычной gameplay-навигации. Это не новая feature/story задача.

## Решения

- Плавающая cyan-подчёркивающая линия главного меню отвергнута. Focus/hover должен давать более яркий текст и небольшой HIF-red акцент рядом с пунктом.
- `SharedPreferencesView` остаётся единственным интерфейсом настроек из главного меню и gameplay; не создавать второй независимый settings screen.
- Настройки применяются и сохраняются сразу. Не добавлять staged copy или кнопку Apply без отдельной причины.
- Screen Mode и Resolution должны использовать понятный семантический control; позднее текущим принятым контрактом стали реальные dropdown.
- Legacy top-right gameplay Menu скрывается; нижняя Quick Menu и Esc → Game Menu остаются основными маршрутами.
- Save backend, Manual pagination и `SaveData` не должны меняться в визуальном проходе настроек.

Документ хранит историю решения. Текущий контракт интерфейса нужно брать из master, `docs/eternum_feature_tracker.md` и актуальной reviewer roadmap.
