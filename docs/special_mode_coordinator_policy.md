# Политика координатора специальных режимов

> **Статус:** реализован технический foundation. Документ фиксирует ownership и fail-closed semantics для authored interactions.

## Проблема

У обычного VN уже есть Choice, History, Settings, Save/Load и confirmations. Для карты, hotspot, timed beat, Chat и других authored interactions нужен один явный owner основного input, чтобы они не конфликтовали с обычным VN UI.

## Модель владения

В gameplay может быть максимум один active exclusive special-mode owner.

`SpecialModeCoordinator` принадлежит scene-local `VNDialogueController`, не является singleton/`DontDestroyOnLoad` manager и не живёт между сценами.

Успешный вход выдаёт opaque lease/token. Только владелец корректного lease может выйти. Duplicate enter, stale/wrong lease и неизвестный owner fail-closed и диагностируются.

## `BlockingExclusive`

Базовый policy preset блокирует:

- dialogue advance;
- Auto;
- Skip;
- Save/Quick Save/Auto Save;
- Load/Quick Load;
- Quick Menu;
- History;
- Settings;
- Main Menu;
- Escape cancel, если конкретный mode явно не имеет безопасного cancel route.

Capabilities типизированы. Проверки по имени mode вроде `isQte || isMap || isChat` запрещены.

## Обычные modals

History, Settings, Save/Load, confirmations и ordinary Choice не превращаются в special modes. Они сохраняют собственное ownership, но special-mode entry запрещается, пока конфликтующий ordinary blocker активен, и наоборот.

## Auto / Skip

При входе в blocking mode текущие coroutines останавливаются, но пользовательские enabled states не переписываются. После выхода они могут возобновиться только через обычные eligibility-предикаты и с новым полным delay, без накопленного скрытого advance.

## Save / Load

Blocking mode по умолчанию запрещает Save/Load. UI/hotkeys используют общий permission contract. Backend `SaveManager` для систем с отдельными defense-in-depth guards остаётся authority хранения.

Разрешать mid-mode save можно только после отдельного reviewed persistence/restore contract конкретного mode.

## Escape / cleanup

Special owner не сбрасывается молча по Escape. Если cancel разрешён, request передаётся владельцу.

Scene unload, destroyed owner или exception обязаны безопасно очистить lease. Состояние coordinator transient и не сериализуется. `SaveData` остаётся v3.

## Использование

Эта foundation уже применяется Chat/Phone, Interactive Hotspot, Map и Timed Narrative Beat. Не создавать параллельный modal manager или новый global pause framework для следующих authored interactions.
