# How I Fall

Unity-проект минимальной технической визуальной новеллы. Текущий baseline — короткая classroom demo для проверки VN-механик; сюжет и персонажи старой версии удалены и позже будут спроектированы заново.

## Текущий игровой контур

- `MainMenu` → New Game / Continue / Load / Settings.
- `VNPrototype` → диалог, typewriter, выбор, короткие ветки, History, Save/Load и возврат в меню.
- Manual / Auto / Quick: по 6 слотов, JSON + PNG preview, восстановление сцены, строки, выбора и `GameState`.
- Текущий граф диалогов: `classroom_first_lesson` → `classroom_choice_investigate` или `classroom_choice_ignore`.
- `ui_test_scene` остаётся зарегистрированной технической сценой для UI/legacy-save совместимости и не является новым сюжетным каноном.

## Основные файлы

```text
Assets/HowIFall/
  Art/                 # только используемые demo/UI assets и один review-placeholder
  Data/Dialogues/      # DialogueSceneData и DialogueSceneRegistry
  Editor/              # validators, smoke tests и Play Mode E2E
  Prefabs/UI/          # ManualSaveLoadPanel
  Scenes/              # MainMenu, VNPrototype
  Scripts/             # runtime VN, Save, Settings, UI, Audio

docs/
  technical_plan.md
  save_system_eternum_reference.md
  eternum_feature_tracker.md
  project_cleanup_audit.md
```

`docs/story/` сейчас намеренно отсутствует. Новую сюжетную документацию следует создавать с чистого листа, когда будет утверждён новый канон.

## Техническая документация

- [Технический план](docs/technical_plan.md)
- [Eternum Feature Tracker](docs/eternum_feature_tracker.md)
- [Исторический референс Save/Load](docs/save_system_eternum_reference.md)
- [Аудит зачистки](docs/project_cleanup_audit.md)

## Проверки

- `HowIFallCiSmokeTests.RunAll`
- `HowIFallProjectValidator.ValidateProject`
- `ManualSaveSystemSceneInstaller.RunValidationBatchMode`
- graphical `ManualSavePlayModeE2ERunner.StartAutomatedPlayMode`
- graphical `SaveBackendV2PlayModeE2ERunner.StartAutomatedPlayMode`

Screenshot-dependent Play Mode E2E нельзя запускать с `-nographics`.
