# Исследование Suspend / Resume

## Текущее поведение HIF

`Continue` вызывает `SaveManager.LoadLatest()` и выбирает самое новое валидное сохранение среди **60 Manual**, **6 Quick** и **6 Auto** слотов. Каждый кандидат читается и валидируется до ранжирования; повреждённая, несовместимая или неразрешимая самая новая запись пропускается, после чего выбирается следующий валидный кандидат.

После загрузки восстанавливаются сохранённая narrative position, поля `GameState` и при наличии v3 snapshot Истории. Обычный выход из Main Menu/OS сейчас не создаёт специального suspend-save.

Auto и Quick имеют по 6 циклических слотов. Ручное сохранение — 60 адресов. Обычные autosave создаются в существующих стабильных точках runtime; Quick Save вызывается игроком.

## Какую проблему решал бы suspend/resume

Единственный существенный дополнительный сценарий: игрок закрывает приложение **после последнего успешного checkpoint**, но до следующего Auto/Manual/Quick save, и ожидает вернуться точно туда же.

Suspend не улучшает восстановление при повреждённом newest save — `Continue` уже умеет безопасно пропускать такой кандидат. Suspend также не может честно обещать crash recovery: Unity не гарантирует callback, завершённый screenshot capture и запись файлов при crash/forced termination.

## Рассмотренные варианты

| Вариант | Польза | Стоимость / риск | Решение сейчас |
|---|---|---|---|
| A. Оставить текущий `Continue` | Высокая для всех успешно созданных сохранений; есть fallback при corruption | Не добавляет новый lifecycle/persistence contract | Лучший текущий baseline |
| B. Явный suspend/resume | Уменьшает разрыв между checkpoints | Quit/pause lifecycle, transient ownership, ranking, invalidation, special-mode handling | Не оправдан для текущего demo |
| C. Позже улучшить autosave policy | Восстановление около реальных важных событий | Требует реального контента, но переиспользует стабильный Auto | Лучшее будущее вложение |

## `SaveData` v3

Текущий v3 уже хранит стабильное campaign state: scene/line identity, choice result state, persisted numeric values и optional backlog snapshot. Он намеренно не сериализует modal state, special-mode state, typewriter progress, animation/timer state или screenshot capture in flight.

Любой будущий resume должен восстанавливать только стабильное gameplay state. Он не должен обещать точное возвращение в Preferences, Save/Load confirmation, History scroll, середину typewriter-анимации и другие transient UI-состояния.

Не менять `SaveData` v3 ради этого без отдельного одобренного compatibility решения.

## Особые режимы — безопасная политика

| Состояние | Решение |
|---|---|
| Preferences / History / Game Menu / Save-Load confirmation | Не сериализовать modal; checkpoint только после завершения операции и возврат в обычный gameplay UI |
| Replay | Никогда не создавать/использовать suspend record |
| Chat/Phone | Не suspend, пока режим активен |
| Map / Interactive Hotspot | Не suspend, пока режим активен |
| Timed Narrative Beat | Не suspend, пока активен timer/outcome |
| Другой special mode | Fail closed, пока у режима нет явного deterministic restore contract |

## Модель отказа для возможного будущего решения

Понадобилось бы явно определить:
- best-effort triggers для normal Quit / Main Menu Quit / application pause; без обещания crash guarantee;
- запрет нового save при активном write/capture, confirmation или exclusive special mode;
- безопасную запись через temporary files и проверку до публикации нового record;
- игнорирование corrupt/stale/unsupported/partial suspend без влияния на обычный Manual/Auto/Quick recovery;
- ranking/freshness так, чтобы поздняя shutdown-запись не вытесняла более новый реальный Auto/Quick progress нелогичным образом;
- явную invalidation/one-time consumption policy.

## Тестирование возможной реализации

Потребовались бы persistence/validation tests плюс PlayMode lifecycle coverage: normal quit intent, Main Menu quit, pause, stale/corrupt/partial suspend, successful one-time resume, New Game invalidation, ranking против Auto/Quick и все deny states выше. Crash/Alt+F4 нельзя обещать как гарантируемый автоматический результат.

## Рекомендация

**DEFER.** Текущий `Continue` уже надёжно возвращает игрока к самому новому загрузочному Manual/Auto/Quick checkpoint и переживает повреждённый самый новый кандидат. Оставшийся gap — только расстояние от последнего checkpoint. Его разумнее позже уменьшить content-informed autosave policy, чем вводить второй persistence/lifecycle contract.

## Решение

**DEFER — реализация suspend/resume не разрешена.** Возвращаться к вопросу только вместе с реальным content/autosave policy и ограниченным lifecycle-дизайном. Не менять `SaveData` v3, сцены, prefab или production C# из-за этого аудита.
