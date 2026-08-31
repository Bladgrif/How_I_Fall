# Аудит зачистки проекта How I Fall

> **Статус:** исторический отчёт о выполненной зачистке. Текущая фаза и scope определяются `docs/product/demo_goal.md` и актуальным tracker/roadmap.

## Цель зачистки

Сохранить рабочий технический VN-контур и regression coverage, удалить старый сюжет, Unity template leftovers и только доказанно неиспользуемые prototype assets.

## Что было сохранено

- рабочие сцены `MainMenu` и `VNPrototype`;
- technical classroom dialogue graph и registry;
- `ui_test_scene` для технической/legacy-save совместимости;
- текущий Save/Load runtime и prefab;
- Editor validators, smoke tests и graphical E2E;
- все реально используемые runtime art/UI assets;
- Packages, ProjectSettings и CI без лишнего риска.

## Что было удалено

- старый `docs/story/**` и архив старого сюжета;
- Unity template Readme/Tutorial leftovers;
- tracked IDE metadata;
- доказанно неиспользуемые prototype backgrounds/UI sprites/SFX;
- устаревшие generated screenshots и локальные test/log artifacts.

Удаление Unity assets выполнялось вместе с `.meta` и только после проверки отсутствия serialized/GUID dependencies.

## Что намеренно не удалялось

- technical `ui_test_scene` и её background;
- узкие repair/build tools, безопасность удаления которых не была доказана;
- story-like поля в `GameState`/`SaveData`, поскольку они участвуют в save schema и regression tests;
- Packages/ProjectSettings без отдельной безопасной задачи.

## Результаты проверки после cleanup

Исторически были подтверждены:

- compile/import PASS;
- Manual Save graphical E2E PASS;
- Save Backend graphical E2E PASS после повторной попытки из-за transient file lock;
- `HowIFallCiSmokeTests.RunAll` PASS;
- scene/manual-save validation PASS;
- project validator PASS;
- отсутствие missing scripts/invalid UnityEvents в проверяемых сценах.

## Сюжет

Старый сюжет полностью удалён и не является каноном. `docs/story/` создаётся заново только после явного возвращения к story work и получения актуального материала.

## Действующее правило

Не использовать этот cleanup audit как текущий roadmap. Он фиксирует историческое решение: удалять только доказанно безопасный мусор и не превращать cleanup в архитектурный rewrite.
