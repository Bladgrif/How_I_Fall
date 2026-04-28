# How I Fall

`How I Fall` — визуальная новелла на Ren'Py.

Жанры: мистика, подростковая драма, эротическое напряжение, романтика, детектив.

История о старшекласснике, который после мистического ритуала получает связь с суккубой и оказывается между желанием,
человечностью и сопротивлением.

## Текущий статус проекта

Проект находится на этапе pre-production / сюжетной разработки.

Основной фокус сейчас — папка `docs/story`.

Папка `game/` пока не является главным направлением работы. Сначала собирается сюжетная документация: структура сцен,
ветки, персонажи, переменные и правила развития маршрутов.

## Структура проекта

```text
How I Fall/
  docs/story/
    README.md
    workflow.md
    flow.md

    core/
      world.md
      characters.md
      variables.md

    planning/
      visual_bible.md
      locations.md
      assets_plan.md
      audio_plan.md
      art_generation_brief.md
      prompt_strategy.md

    scenes/
      day_00_prologue/
      day_01_afterparty/
      day_02_tbd/
      day_03_tbd/
  game/
```

## Где читать сюжет

Начинать лучше с:

1. `docs/story/README.md`
2. `docs/story/workflow.md`
3. `docs/story/flow.md`
4. `docs/story/core/world.md`
5. `docs/story/core/characters.md`
6. `docs/story/core/variables.md`

`workflow.md` описывает рекомендуемый порядок чтения и работы со сценами.

## Ранний блок сцен

Текущая последовательность:

1. `docs/story/scenes/day_00_prologue/prologue_party.md`
2. `docs/story/scenes/day_00_prologue/scene_succubus_first_contact.md`

После первого контакта идут альтернативные ранние route-сцены:

- `docs/story/scenes/day_00_prologue/route_lust_intro.md`
- `docs/story/scenes/day_00_prologue/route_romance_intro.md`
- `docs/story/scenes/day_00_prologue/route_purity_intro.md`

Общий поток после первой ночи:

- `docs/story/scenes/day_01_afterparty/first_morning_after.md`
- `docs/story/scenes/day_01_afterparty/first_whispers.md`
- `docs/story/scenes/day_01_afterparty/school_tension.md`
- `docs/story/scenes/day_01_afterparty/summer_free_days.md`
- `docs/story/scenes/day_01_afterparty/investigation_intro.md`
- `docs/story/scenes/day_01_afterparty/artem_private_talk.md`
- `docs/story/scenes/day_01_afterparty/lera_followup.md`

## Основные маршруты

- `lust` — похоть, падение, искушение и потеря контроля.
- `romance` — человечность, близость, честность и сложное сопротивление искушению.
- `purity` — самоконтроль, страх, сопротивление и борьба с демоническим влиянием.

## Рабочий принцип

Сначала фиксируется сюжетная основа в `docs/story`, затем материалы можно переносить в Ren'Py-структуру внутри `game/`.
