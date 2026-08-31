# Политика persistent-видимости Quick Menu (B03)

> **Статус:** реализовано. Этот документ сохраняется как контракт настройки `Show Quick Menu`.

## Решение

B03 добавляет пользовательскую persistent-настройку **«Показывать быстрое меню»**. Default = `ON`. Изменение применяется сразу и сохраняется между запусками.

Единственный владелец — существующие `GameSettings` / `SettingsManager`. `VNQuickMenu` не владеет persistent state, а только применяет effective visibility.

## Persistence

Каноническое поле: `bool showQuickMenu = true`.

Ключ PlayerPrefs: `hif_show_quick_menu`.

Настройка не попадает в `SaveData`, `GameState`, dialogue data, backlog или Replay snapshot. `SaveData.CurrentVersion` остаётся `3`.

## Effective visibility

Концептуально:

```text
effectiveQuickMenuVisible = showQuickMenu
                         && !hiddenByPlayerCleanView
                         && !hiddenByBlockingSpecialMode
                         && !hiddenByPreferences
                         && !hiddenByGameMenu
```

Transient blockers не изменяют persistent preference.

Примеры:

- OFF → H hide → H restore = Quick Menu остаётся скрытым;
- ON → H hide → H restore = Quick Menu возвращается, если других blockers нет;
- изменение setting под blocker сохраняет новое значение, но не показывает root раньше времени.

## Actions и hotkeys

OFF скрывает только player-facing strip. Оно не переписывает hotkeys и не превращается в общий gameplay-permission flag.

Replay filtering, Character Hub restrictions и Special Mode ownership остаются отдельными контрактами.

## Settings UI

Toggle доступен через общий `SharedPreferencesView`; отдельного duplicated storage или recovery shortcut нет. Reset возвращает ON.

## Layout

Dialogue shell учитывает реальный safe-area reserve Quick Menu, а не копирует фиксированные пиксели из Eternum. Когда strip эффективно скрыт, reserve схлопывается.

## Текущий статус

B03 реализован и покрыт regression/graphical QA в составе завершённых Preferences/Quick Menu passes. Повторно планировать его как `TODO` нельзя.
