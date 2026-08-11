# B03 — Persistent Quick Menu visibility policy

**Status:** POLICY DECIDED / IMPLEMENTATION TODO
**Scope:** docs-only policy. Runtime, tests, scenes and assets are unchanged.

## Решение

B03 добавляет пользовательскую persistent-настройку **«Показывать быстрое меню»** (`Show Quick Menu`). Это toggle с default `ON`; изменение применяется сразу, без `Apply` и перезапуска, и сохраняется между запусками.

Настройка управляет только видимостью компактной панели Quick Menu во время обычного VN gameplay. Она не является разрешением на действия и не меняет существующие input bindings.

## Владение и persistence

- Единственный владелец настройки — существующий `GameSettings` через `SettingsManager`.
- Каноническое поле: `bool showQuickMenu = true`.
- Канонический `PlayerPrefs` key: `hif_show_quick_menu`.
- Загрузка, сохранение, immediate setter и Reset выполняются существующим `SettingsManager` flow.
- Настройка не попадает в `SaveData`, `GameState`, scene/dialogue data, backlog, Replay snapshot/profile или campaign save. `SaveData.CurrentVersion` остаётся `3`.
- `VNQuickMenu` не владеет persistent state; он только применяет результат к своему `root`.

## Effective visibility и precedence

Каноническая формула:

```text
effectiveQuickMenuVisible = showQuickMenu
                         && !hiddenByPlayerCleanView
                         && !hiddenByBlockingSpecialMode
```

Приоритет сверху вниз:

1. Persistent preference (`showQuickMenu`) задаёт базовую видимость.
2. Existing H / clean-view blocker временно скрывает dialogue shell и Quick Menu.
3. Existing `BlockingExclusive` Special Mode временно скрывает Quick Menu согласно текущему `SpecialModeCoordinator` ownership.
4. Ordinary modals, включая Character Hub, не становятся владельцами persistent или special-mode visibility. Их текущий action/modal gating не меняется.

`showQuickMenu = OFF` скрывает `Quick Menu root` в обычном VN. `ON` показывает root, только если временные blockers сняты.

### H / clean view

H остаётся transient-контрактом: он не читает и не изменяет `showQuickMenu`. Восстановление должно повторно применять актуальную effective visibility, а не безусловно активировать root:

- `OFF → H hide → H restore` оставляет Quick Menu скрытым;
- `ON → H hide → H restore` возвращает Quick Menu, если нет Special Mode blocker.

### Special Mode

`BlockingExclusive` временно скрывает Quick Menu как сейчас и не изменяет preference. После выхода `ON` возвращает меню, `OFF` оставляет его скрытым. Контракт `SpecialModeCoordinator` и semantic gating `CanOpenQuickMenu` не меняются.

### Изменение настройки под blocker

Setter SettingsManager сохраняет новое значение сразу. Пока H или Special Mode активны, root не появляется. После снятия blocker применяется последнее значение из SettingsManager.

`wasVisibleBeforePlayerHide` и аналогичные snapshot-поля могут описывать только transient ownership; они не являются authority поверх persistent preference и не должны восстанавливать устаревшее `true`.

## Actions, hotkeys и Replay

Preference OFF скрывает только панель. Не отключаются Backlog, Skip, Auto, Quick Save, Quick Load, Load/Save keyboard actions и другие существующие `VNInputMap` bindings. Новая B03-кнопка или shortcut для этого не добавляется.

`CanOpenQuickMenu` сохраняет текущую semantic permission. Visual hidden root не превращается в gameplay/action lockout.

Replay-фильтрация кнопок остаётся без изменений: OFF скрывает весь root, ON показывает существующий разрешённый subset. Логика End Replay и Save-кнопок не переносится в B03.

## Settings UI и lockout prevention

Канонический player-facing label: **«Показывать быстрое меню»** (английский fallback: `Show Quick Menu`). Тип — Toggle.

Toggle должен быть доступен через оба существующих settings entry points:

- VN Settings;
- Main Menu Settings.

Оба экрана используют один `GameSettings`/`SettingsManager` state, без второго флага или дублирующего storage. Main Menu Settings обязан позволять вернуть `ON`, даже когда VN Quick Menu скрыта. Отдельный shortcut для восстановления не нужен.

Reset Settings использует существующий `GameSettings` default/`SettingsManager.ResetSettings` flow и возвращает `showQuickMenu = true`; отдельный reset path запрещён.

## Fallback и layout safety

Если `SettingsManager` или его `settings` недоступен, safe fallback — default-visible (`showQuickMenu = true`), без `NullReferenceException`. Это сохраняет текущее поведение до появления settings service.

Реализация должна предпочитать существующие settings prefab/runtime wiring и существующий layout contract. Изменение textbox geometry допускается только как точечная часть существующей UI wiring; широкие правки `Assets/HowIFall/Scenes/VNPrototype.unity` не одобрены без доказанной необходимости. Не создавать нового singleton и не делать `VNQuickMenu`, Character Hub или modal owner владельцами preference.

## Ожидаемая зона реализации

Следующий implementation scope ограничен существующими owners:

- `Assets/HowIFall/Scripts/Settings/GameSettings.cs` и `SettingsManager.cs` — поле, key, load/save/set/reset;
- `Assets/HowIFall/Scripts/UI/VNQuickMenu.cs` — effective visibility и refresh без изменения action wiring;
- существующий VN settings presenter/panel и `SettingsPanelController` — общий Toggle и два entry points;
- точечные runtime tests для контракта ниже.

`SaveData`, `GameState`, `VNInputMap`, `SpecialModeCoordinator` и `VNPrototype.unity` не входят в B03 scope, кроме явно доказанной unavoidable wiring.

## Future implementation test contract

Минимальные проверки:

1. Default ON.
2. SettingsManager load/save persistence с `hif_show_quick_menu`.
3. OFF скрывает root; ON восстанавливает его без blocker.
4. OFF не отключает keyboard actions.
5. H не мутирует preference; ON/H restore видим, OFF/H restore скрыт.
6. Special Mode не мутирует preference; OFF после него остаётся скрыт.
7. Изменение под H/Special Mode не показывает root раньше времени; после снятия blocker побеждает latest preference.
8. Reset Settings возвращает ON.
9. Main Menu Settings может включить настройку обратно.
10. Replay filtering, Character Hub ownership и ordinary modal ownership не изменились.
11. SaveData остаётся v3, `showQuickMenu` не сериализуется.
12. `VNPrototype.unity` остаётся без изменений, если policy-approved runtime wiring не докажет обратное.

## Единственный следующий шаг

**Implement Persistent Quick Menu visibility preference (B03).**
