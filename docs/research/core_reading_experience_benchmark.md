# Core Reading Experience Benchmark

## Задача

Сформировать практический quality bar для обычного чтения HIF в фазе
**Polished Functional Demo First**. Это не работа над каноном, финальным art
или новой VN-механикой.

## References

- [STEINS;GATE — Steam screenshots](https://store.steampowered.com/app/412830/STEINSGATE): art/character остаются главным кадром, а текстовая оболочка работает как стабильный нижний слой.
- [The House in Fata Morgana — Steam screenshots](https://store.steampowered.com/app/303310/The_House_in_Fata_Morgana/): атмосферный кадр не требует тяжёлых декоративных UI-слоёв; лог и основные reading-controls остаются обычной частью VN-петли.
- [Doki Doki Literature Club Plus! — Steam screenshots](https://store.steampowered.com/app/1388880/Doki_Doki_Literature_Club_Plus/): простой, предсказуемый dialogue/choice baseline и HD 1080p presentation без перегрузки действиями.
- [PARANORMASIGHT: The Seven Mysteries of Honjo — Steam](https://store.steampowered.com/app/2106840/PARANORMASIGHT_The_Seven_Mysteries_of_Honjo/): UI подчинён напряжённому кадру и ясно переключает игрока между чтением и отдельным взаимодействием; это reference, не template layout.
- [Xbox Accessibility Guideline 101 — Text display](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101): масштабирование текста должно сохранять читаемость без одновременного горизонтального и вертикального скролла в одном UI.
- [Xbox Accessibility Guideline 104 — Subtitles and captions](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/104): применять только общие принципы ясного, настраиваемого текста; persistent VN textbox не является субтитрами.

## Полезные patterns

| Area | Наблюдение | Применение в HIF |
|---|---|---|
| Art vs UI | Сцена и персонаж остаются визуальным центром; нижняя текстовая зона стабильна и прозрачна. | Lower-shell с neutral-dark фоном, без ярких заполненных поверхностей. |
| Speaker / body | Имя — компактный якорь, тело текста — самый читаемый слой UI. | Уменьшить name box до заголовка строки, дать body комфортную высоту и left alignment. |
| Long text | Реальная настройка масштаба требует высоты и переноса, а не тихого уменьшения текста. | Поддержать 0.85–1.25, расширить runtime reading area, сохранить настройку пользователя. |
| Choices | Выбор — отдельная, но лёгкая композиция; focus виден не только цветом. | Нейтральный dark fill, cyan outline/selected state, первый видимый вариант получает EventSystem focus. |
| Quick controls | Действия компактны и слабее текста; Auto/Skip сообщают состояние отдельно от focus. | Текущий порядок HIF сохранён; active и focus используют совместимые restrained cyan states. |
| Backlog | Log читается как текст, а не debug dump; свежая запись легко достижима. | Переиспользовать текущий ScrollRect, открыть на newest, отделить speaker и narration spacing. |

## Weaknesses / trade-offs

- Большая непрозрачная панель повышает контраст, но отнимает у art кадр. Для HIF выбран умеренно прозрачный neutral-dark shell, а не ещё одна цветная карточка.
- Две строки в choice требуют высоты. При трёх вариантах слишком высокие кнопки могут столкнуться с title: HIF ограничивает высоту каждой строки и оставляет перенос только для разумного текста.
- Постоянные заметные индикаторы Auto/Skip помогают состоянию, но не должны превращать strip в HUD. Active state остаётся компактным, cyan используется как accent.
- XAG 104 описывает subtitles/captions, поэтому не добавляет HIF caption system и не меняет narrative text semantics.

## Chosen HIF direction

**ART-FIRST READING UI:** нижний dialogue shell читаем на светлом и тёмном фоне,
но уступает сцене по visual weight. Иерархия: speaker → dialogue body → quiet
advance affordance → compact reading actions. Existing SaveData v3, dialog flow,
Auto/Skip timing, Quick Menu actions и story state не меняются.

## Explicitly not adopted

- Копирование чужих fonts, art, layouts или controls one-to-one.
- Финальный art direction, новые шрифты и декоративные animation stacks.
- Новая backlog/save/choice framework или второй dialogue system.
- Subtitle/caption requirements как повод изменить persistent VN textbox.
- Hold-to-skip, новая Auto formula, новые story routes или canonical fixtures.
