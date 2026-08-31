# Политика Chat / Phone

> **Статус:** техническая основа реализована. Документ описывает границы системы; TEST-контент не является каноном.

## Назначение

Chat / Phone — отдельная авторская интерактивная сцена поверх VN, а не универсальная телефонная ОС. Система поддерживает типизированные Text/Image/Choice записи, временный transcript, typed conditions/effects и детерминированный возврат в конкретную `DialogueSceneData`.

## Данные

`ChatSceneData : ScriptableObject` содержит стабильный `chatId`, отображаемое имя контакта, упорядоченные entries и обязательную `returnScene`.

Поддерживаются только закрытые типы:

- `Text`;
- `Image`;
- `Choice`.

Произвольные команды, reflection, `eval`, string callbacks и generic payload dictionary запрещены.

## Выборы и состояние

Choice использует существующие `ChoiceCondition` и закрытые numeric deltas `GameState`. Target задаётся стабильным `entryId` внутри того же Chat. Невалидный target не применяет effect и приводит к безопасному завершению через `returnScene`.

Transcript остаётся transient и не попадает в обычный `DialogueBacklog`, `DialogueReadHistory` или `SaveData`.

## Special Mode

Активный Chat владеет одним `SpecialModeCoordinator.BlockingExclusive` lease.

Пока Chat активен, блокируются обычный dialogue advance, Auto, Skip, Save/Quick Save/Auto Save, Load/Quick Load, History, Settings, Quick Menu и Main Menu. `Escape` по обычному Chat-контракту не отменяет сцену. Локальный media viewer R06 имеет отдельный приоритет только внутри Chat.

Backend `SaveManager` также обязан fail-closed; одного UI-block недостаточно.

## Save / Replay

Chat mid-state не сериализуется. `SaveData.CurrentVersion` остаётся `3`.

Старт Chat запрещён в Replay. `ReplaySession` и campaign state не изменяются.

## Завершение

Единственный путь:

`VN → StartChat → active Chat → terminal/choice → cleanup → release lease → returnScene`.

Completion idempotent и выполняется ровно один раз. Lease освобождается до routing, чтобы special-mode gate не заблокировал возврат.

## Не входит в систему

Phone OS, список приложений/контактов, звонки, persistent messenger history, unread badges, generic attachments, video/file framework и отдельные глобальные `PhoneManager`/`ChatManager` не нужны без конкретного сюжетного требования.

## Текущий статус

Technical foundation, Phone UI polish, pacing, viewer и sound-контракты реализованы. Реальные контакты, сообщения, изображения и сюжетные ветки остаются отложенными до актуального story material.
