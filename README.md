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
    world.md
    characters.md
    variables.md
    flow.md
    workflow.md
    scenes/
      prologue_party.md
      scene_succubus_first_contact.md
      route_lust_intro.md
      route_romance_intro.md
      route_purity_intro.md
      first_morning_after.md
      first_whispers.md
      school_tension.md
      summer_free_days.md
  game/
```

## Где читать сюжет

Начинать лучше с:

1. `docs/story/README.md`
2. `docs/story/world.md`
3. `docs/story/characters.md`
4. `docs/story/variables.md`
5. `docs/story/flow.md`
6. `docs/story/workflow.md`

`workflow.md` описывает рекомендуемый порядок чтения и работы со сценами.

## Ранний блок сцен

Текущая последовательность:

1. `docs/story/scenes/prologue_party.md`
2. `docs/story/scenes/scene_succubus_first_contact.md`

После первого контакта идут альтернативные ранние route-сцены:

- `docs/story/scenes/route_lust_intro.md`
- `docs/story/scenes/route_romance_intro.md`
- `docs/story/scenes/route_purity_intro.md`

Затем история снова собирается в общий поток:

- `docs/story/scenes/first_morning_after.md`
- `docs/story/scenes/first_whispers.md`
- `docs/story/scenes/school_tension.md`
- `docs/story/scenes/summer_free_days.md`

## Основные маршруты

- `lust` — похоть, падение, искушение и потеря контроля.
- `romance` — человечность, близость, честность и сложное сопротивление искушению.
- `purity` — самоконтроль, страх, сопротивление и борьба с демоническим влиянием.

## Рабочий принцип

Сначала фиксируется сюжетная основа в `docs/story`, затем материалы можно переносить в Ren'Py-структуру внутри `game/`.
