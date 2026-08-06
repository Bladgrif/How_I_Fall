# Судьба удалённых Editor builders

Проверена версия файлов перед commit `abac71b516b9335eafa917aaf54875b1b52326a0`.

## MainMenuSceneBuilder

Builder полностью пересоздавал `MainMenu.unity`: фон, анимацию меню, настройки, справку, подтверждение выхода, Build Settings и старую Save/Load-панель.

Актуальная функциональность сохранена в сериализованной сцене `Assets/HowIFall/Scenes/MainMenu.unity` и runtime-классах `MainMenuController`, `MainMenuAnimator`, `SettingsPanelController`. Build Settings и обязательные ссылки проверяет `HowIFallProjectValidator`. Новую ручную Save/Load-панель устанавливает только `ManualSaveSystemSceneInstaller`.

Builder не восстановлен, потому что его повторный запуск целиком перезаписывал сцену и возвращал удалённую Save/Load-архитектуру.

## VNPrototypeSceneBuilder

Builder полностью пересоздавал `VNPrototype.unity`: VN HUD, фон и персонажа, выборы, quick menu, настройки, историю, уведомления, debug-панель и старую Save/Load-панель.

Актуальная runtime-функциональность находится в сериализованной сцене `Assets/HowIFall/Scenes/VNPrototype.unity`, `VNDialogueController`, `DialogueBacklog`, `VNSettingsPresenter` и `DebugStatsPanelController`. Сценарный тестовый asset обслуживает `VNUITestSceneContentBuilder`; отдельные безопасные операции оставлены в `VNPrototypeAudioListenerBuilder` и `VNPrototypeDebugStatsBuilder`. Новую Save/Load-панель устанавливает `ManualSaveSystemSceneInstaller`.

Builder не восстановлен: он был монолитным генератором всей сцены и содержал прежнюю Save/Load-систему.

## VNPrototypeBacklogUiBuilder

Builder пересоздавал не только backlog, но и quick menu, настройки, уведомления, подтверждение выхода и старые quick save/load-кнопки.

Backlog уже сериализован в `VNPrototype.unity`, его поведение реализуют `VNDialogueController` и `DialogueBacklog`, а базовую логику проверяет `DialogueBacklogSmokeTests`. Настройки обслуживает `VNSettingsPresenter` и `VNSettingsPresenterSmokeTests`.

Builder не восстановлен, потому что изменял несколько несвязанных UI-систем и возвращал удалённые Save/Load bindings. Для текущей сцены он не требуется.
