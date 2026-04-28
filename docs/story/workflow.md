# Workflow работы с сюжетными файлами

## Зачем нужен этот файл

Этот файл нужен как навигация по сюжетной документации проекта:

- в каком порядке читать материалы;
- какие файлы являются опорными;
- как открывать сцены при написании новых;
- как не терять связь между сценами, персонажами и переменными.

На текущем этапе приоритет — `docs/story`, а не папка `game/`.

## Базовый порядок чтения файлов

1. `README.md`
    - Вход в story-документацию.

2. `workflow.md`
    - Правила работы с файлами, сценами и вкладками.

3. `flow.md`
    - Карта сюжета и порядок сцен.

4. `core/world.md`
    - Правила мира, тон истории и скрытый мистический слой.

5. `core/characters.md`
    - Характеры, роли и функции персонажей.

6. `core/variables.md`
    - Внутренняя логика выборов, состояний героя, route-влияния и последствий.

## Порядок чтения сцен

### День 0 — пролог

- `scenes/day_00_prologue/prologue_party.md`
- `scenes/day_00_prologue/scene_succubus_first_contact.md`

### Альтернативные ранние route-сцены

- `scenes/day_00_prologue/route_lust_intro.md`
- `scenes/day_00_prologue/route_romance_intro.md`
- `scenes/day_00_prologue/route_purity_intro.md`

Эти три файла не читаются как линейная цепочка одной истории. Это альтернативные ранние отклонения внутри конца
пролога / ночи ритуала.

### День 1 — aftermath / afterparty

- `scenes/day_01_afterparty/first_morning_after.md`
- `scenes/day_01_afterparty/first_whispers.md`
- `scenes/day_01_afterparty/school_tension.md`
- `scenes/day_01_afterparty/summer_free_days.md`
- `scenes/day_01_afterparty/investigation_intro.md`
- `scenes/day_01_afterparty/artem_private_talk.md`
- `scenes/day_01_afterparty/lera_followup.md`

## Как работать над новой сценой

Рекомендуемый порядок открытия файлов в IDE:

1. `flow.md`
2. нужная текущая сцена
3. `core/characters.md`
4. `core/variables.md`

Зачем:

- `flow.md` нужен, чтобы понимать место сцены в истории;
- текущая сцена — основной рабочий файл;
- `core/characters.md` нужен, чтобы не ломать характеры;
- `core/variables.md` нужен, чтобы не забывать про последствия, route-влияние и внутренние сдвиги.

## Как проверять уже написанную сцену

Для проверки связности открывай:

- предыдущую сцену;
- текущую сцену;
- следующую сцену по `flow.md`.

Так проще увидеть:

- повтор текста;
- сломанный тон;
- резкий скачок состояния героя;
- выпадение персонажей из логики.

## Как работать с route-сценами

Файлы:

- `scenes/day_00_prologue/route_lust_intro.md`
- `scenes/day_00_prologue/route_romance_intro.md`
- `scenes/day_00_prologue/route_purity_intro.md`

нужно использовать как сравнительный блок.

При проектировании новых route-событий полезно держать открытыми сразу:

- `flow.md`
- `core/variables.md`
- все три route-сцены

Чтобы:

- маршруты не сливались по тону;
- отличались по внутреннему конфликту;
- отличались по типу контакта с сущностью.

## Основной рабочий маршрут проекта на текущем этапе

```text
README.md
workflow.md
flow.md

core/world.md
core/characters.md
core/variables.md

planning/visual_bible.md
planning/locations.md
planning/assets_plan.md
planning/audio_plan.md
planning/art_generation_brief.md
planning/prompt_strategy.md

scenes/day_00_prologue/prologue_party.md
scenes/day_00_prologue/scene_succubus_first_contact.md

scenes/day_00_prologue/route_lust_intro.md
scenes/day_00_prologue/route_romance_intro.md
scenes/day_00_prologue/route_purity_intro.md

scenes/day_01_afterparty/first_morning_after.md
scenes/day_01_afterparty/first_whispers.md
scenes/day_01_afterparty/school_tension.md
scenes/day_01_afterparty/summer_free_days.md
scenes/day_01_afterparty/investigation_intro.md
scenes/day_01_afterparty/artem_private_talk.md
scenes/day_01_afterparty/lera_followup.md
```

## Рекомендуемый набор вкладок в IDE

Удобно держать закреплёнными:

- `flow.md`
- `core/characters.md`
- `core/variables.md`
- текущую сцену

При необходимости дополнительно открывать:

- предыдущую сцену;
- следующую сцену;
- `core/world.md`, если нужно свериться с тоном и правилами мира.

## Чего не делать

Не стоит:

- писать сцену без `flow.md`;
- делать выборы без сверки с `core/variables.md`;
- менять характер персонажей без сверки с `core/characters.md`;
- читать route-сцены как одну линейную цепочку;
- переходить в папку `game/`, пока не устаканена `docs/story`.
