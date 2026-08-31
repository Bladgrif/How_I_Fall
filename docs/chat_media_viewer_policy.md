# Политика встроенного просмотра медиа в Chat (R06)

> **Статус:** реализовано. Feature commit: `b2477d392d1816b983f33ee9f42825f15506e84a`.

## Назначение

R06 добавляет одно ограниченное действие:

`открытая карточка Image в Chat → локальный полноэкранный viewer → закрытие → тот же активный Chat`.

Это **TECH DEMO ONLY / NOT CANON**. Существующая Phone UI и transcript остаются владельцами контекста.

## Владение

Viewer — локальная подмодалка существующего `ChatController`.

- `ChatController` продолжает владеть единственным `SpecialModeCoordinator.BlockingExclusive` lease.
- Viewer не создаёт второй lease.
- Нельзя добавлять `MediaViewerManager`, `PhoneMediaManager`, новый singleton или новый special mode.

## Открытие

Viewer открывается только для уже показанного `ChatEntryKind.Image` с непустым `Sprite` и только прямым кликом по media card. До reveal, после cleanup, из Text/Choice/typing-состояния или повторным запросом открыть viewer нельзя.

## Представление и ввод

- поверх Phone UI показывается тёмный полупрозрачный scrim;
- изображение отображается aspect-fit, без crop и stretch;
- есть безопасная кнопка `X`;
- underlying Phone UI остаётся визуально сзади, но не получает input;
- закрытие: `X`, `Escape` или клик по scrim;
- клик по самому изображению не закрывает viewer.

Пока viewer открыт, `Escape` сначала закрывает **только viewer**. После этого обычный Chat-контракт снова делает Escape no-op.

## Сохранение состояния Chat

Закрытие viewer не должно:

- завершать или перезапускать Chat;
- менять transcript;
- менять reply state;
- менять `GameState`;
- маршрутизировать в `returnScene`;
- повторять incoming/open audio cue.

## Таймеры и звук

Локальные Chat pacing/terminal timers приостанавливаются на время viewer и продолжаются с оставшегося времени после закрытия. `Time.timeScale` не изменяется.

Viewer не добавляет собственных SFX и не меняет R08 semantics.

## Persistence

Viewer state полностью runtime-only. Он не попадает в `SaveData`, backlog, `DialogueReadHistory`, Replay history или profile JSON. `SaveData` остаётся v3.

При Chat Complete, failure, `OnDisable` или `OnDestroy` viewer безопасно очищается без утечки input blocker или повторного route.

## Вне scope

Zoom, pan, rotate, download/save, carousel, captions, video, animated media, Gallery integration и generic media viewer вне Chat не входят в R06.

## Итог реализации

Реализация сохраняет существующий PhoneShell, использует aspect-fit, блокирует underlying input, даёт viewer приоритет Escape только во время открытия и локально паузит таймеры. Manual graphical/functional QA для R06 и повторная R08 audio QA ранее прошли.
