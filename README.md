# How I Fall

Unity-проект технической визуальной новеллы. Текущая продуктовая фаза — **Polished Functional Demo First**: сначала стабильный функционал, удобный player-facing UX и проверяемый UI; реальный сюжет, канон и финальный арт будут подключаться позже отдельным этапом.

## Текущий игровой контур

- `MainMenu` → Новая игра / Продолжить / Загрузить / Настройки / Выйти.
- `VNPrototype` → диалог, typewriter, выборы, История, Авто, безопасный Пропуск, компактное быстрое меню, игровое меню, сохранение/загрузка и возврат в главное меню.
- Ручные сохранения: 10 страниц × 6 слотов. Авто и быстрые сохранения: по 6 циклических слотов. Используются JSON + PNG preview.
- `Continue` загружает самое новое валидное сохранение среди Manual/Auto/Quick и пропускает повреждённый самый новый кандидат.
- `Quick Load` сохраняет обычную семантику: загружает самое новое валидное Quick-сохранение.
- `SaveData` остаётся версии v3; совместимость старых поддерживаемых данных защищена.
- `ui_test_scene` остаётся зарегистрированной технической сценой для UI/legacy-save совместимости и не является сюжетным каноном.

## Основные каталоги

```text
Assets/HowIFall/
  Art/                 # используемые demo/UI assets
  Data/Dialogues/      # DialogueSceneData и DialogueSceneRegistry
  Editor/              # validators, smoke tests и PlayMode/graphical E2E
  Prefabs/UI/          # существующие UI prefab-файлы
  Scenes/              # MainMenu, VNPrototype и технические сцены
  Scripts/             # runtime VN, Save, Settings, UI, Audio

docs/
  product/              # утверждённые продуктовые решения и workflow
  research/             # повторно используемые исследования
  visual-baselines/     # небольшой набор визуальных baseline-скриншотов
  eternum_feature_tracker.md
```

`docs/story/` сейчас намеренно отсутствует. Когда работа над сюжетом будет явно возобновлена, новый актуальный материал сначала оформляется в Markdown, а не через изменения C#/сцен/prefab.

## Основные документы

- [Цель текущей демо-фазы](docs/product/demo_goal.md)
- [Принципы UI/UX](docs/product/ui_principles.md)
- [Процесс ревью](docs/product/review_workflow.md)
- [Карта реализованных возможностей](docs/eternum_feature_tracker.md)
- [Бенчмарк функциональности VN](docs/research/vn_functionality_benchmark.md)
- [Практическое руководство по разработке VN](docs/research/vn_development_playbook.md)

## Проверки

- `HowIFallCiSmokeTests.RunAll`
- `HowIFallProjectValidator.ValidateProject`
- целевые EditMode/PlayMode NUnit-тесты для изменяемой системы;
- существующие graphical E2E, включая `PlayerUiGraphicalE2ERunner`, `ManualSavePlayModeE2ERunner` и `SaveBackendV2PlayModeE2ERunner`.

Graphical E2E, зависящие от скриншотов, нельзя запускать с `-nographics`. Стандартное разрешение QA — 1920x1080, если задача отдельно не проверяет адаптивность.

## Язык документации

Человеко-читаемая документация проекта ведётся по-русски. Технические пути, имена файлов, классов, API, тестов и других идентификаторов сохраняются как в коде.
