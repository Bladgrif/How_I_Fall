# Политика Gallery / Replay Foundation

> **Статус:** техническая основа реализована. Реальный replay-контент, thumbnails, категории и канонические unlock-правила отложены до сюжетного этапа.

## Граница

V1 доказывает безопасную Gallery/Replay foundation на нейтральном `TEST REPLAY`. Это не канон и не финальная Extra/Gallery UX.

## Изоляция campaign state

Replay использует transient `ReplaySession` и снимок существующего `GameState`. Перед стартом сохраняются все mutable campaign-поля, во время Replay используется временное replay-state, а при завершении исходный campaign snapshot восстанавливается ровно один раз.

Replay не должен изменять:

- campaign saves;
- campaign `GameState` после выхода;
- campaign backlog;
- profile `DialogueReadHistory`;
- Continue ranking.

## Unlock persistence

Unlock хранится отдельно от campaign saves в небольшом versioned profile JSON под `Application.persistentDataPath`.

- стабильный `replayId`;
- unlock idempotent;
- corrupt/unknown profile fail-closed;
- New Game, Load и удаление saves не relock-ят Gallery item.

`SaveData.CurrentVersion` остаётся `3`.

## Replay permissions

Во время Replay разрешены ordinary reading actions: History, Auto, Skip и Settings.

Save, Quick Save, Auto Save, pre-load save, Load и Quick Load запрещены на UI/controller уровне и дополнительно backend-guard'ом `SaveManager`.

Replay не использует `BlockingExclusive`: это отдельный top-level execution mode, которому нужно позволять обычное VN-чтение.

## Завершение

`EndReplay()` idempotent:

1. блокирует повторный ввод/сохранение;
2. очищает replay-only history/audio state;
3. восстанавливает campaign snapshot;
4. очищает Replay mode;
5. возвращает в Main Menu.

Failure path обязан выполнить тот же безопасный cleanup.

## Вне scope

Категории, фильтры, canonical CG browser, route completion, achievements, statistics, replay variants и отдельная save-schema для replay не входят в foundation.

## Текущий продуктовый статус

Foundation считается `DONE`. Обычный player-facing Gallery entry и настоящий контент не следует добавлять до актуального story/art материала и отдельного продукта/UX-решения.
