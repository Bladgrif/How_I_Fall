# How I Fall

`How I Fall` — мистическая визуальная новелла / интерактивная история на Unity.

Жанры: мистика, подростковая драма, эротическое напряжение, романтика, детектив.

История о старшекласснике, который после мистического ритуала получает связь с суккубой и оказывается между желанием,
человечностью и сопротивлением.

## Текущий статус проекта

Проект находится на этапе pre-production / Unity prototype.

Unity-проект находится в корне репозитория.

Сюжетная документация находится в `docs/story/`.

Ren'Py-прототип удалён из рабочей структуры после перехода на Unity.

## Структура проекта

```text
How I Fall/
  Assets/              # Unity assets, scenes, scripts, art
  Packages/            # Unity package manifest
  ProjectSettings/     # Unity project settings
  docs/story/           # сюжетная документация
  README.md
  .gitignore
```

## Текущий технический вектор

- Основной движок: Unity.
- Целевые платформы: PC и Android.
- Причина выбора Unity:
    - поддержка PC и Android из одного проекта
    - живые фоны
    - 2.5D-композиция
    - анимированный свет
    - кастомный UI
    - мини-игры и интерактивные сцены

## Unity project

Unity-проект открыт из корня репозитория.

Текущая сцена:

`Assets/HowIFall/Scenes/VNPrototype.unity`

Что уже есть:

- VN textbox
- namebox
- кнопка Next
- выборы
- debug stats
- базовые переменные маршрутов
- тестовая Android APK сборка

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

Сначала фиксируется сюжетная основа в `docs/story`, затем игровые сцены и механики реализуются в Unity-проекте.
