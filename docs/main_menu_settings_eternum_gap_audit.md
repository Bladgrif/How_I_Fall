# Аудит различий Main Menu / Settings / Game UI относительно Eternum

> **Статус:** исторический аудит. Большинство выводов уже реализовано в последующих HIF passes. Текущий source of truth: `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md` и reviewer roadmap.

## Исходная проблема

На момент аудита HIF имел раздробленную player shell:

- Main Menu и gameplay использовали разные Settings UI;
- Quick Menu выполнял слишком много навигационных задач;
- полноценный Game Menu отсутствовал;
- часть `GameSettings` сохранялась без реального player-facing эффекта.

Сравнение с Eternum показало полезный принцип: один Preferences-контракт, отдельные роли Quick Menu и Game Menu, отсутствие fake settings.

## Решения, которые были приняты

1. Один `SharedPreferencesView` из Main Menu и gameplay.
2. Отдельный Game Menu для navigation/back-stack.
3. Quick Menu — только частые reading actions.
4. Не показывать настройку без реального runtime consumer.
5. `Show Quick Menu` — persistent setting, а не отдельная runtime-система.
6. Save backend не переписывать ради визуального parity.

## Текущий результат HIF

Эти решения уже реализованы и дальнейшие задачи не должны планировать их заново:

- Shared Preferences — DONE;
- Game Menu — DONE;
- compact Quick Menu — `История | Пропуск | Авто | Быстр. сохр.`;
- Help/About/Gallery скрыты из обычного Main Menu текущего demo;
- Save mode = Manual only;
- Load mode = Manual + Auto + Quick;
- Esc/back-stack и safe confirmation defaults покрыты тестами/графическим QA.

## Что из старого аудита больше не является текущим контрактом

- старый расширенный Quick Menu с Save/Q.Load/Preferences/Menu;
- старые шесть Manual slots — теперь Manual 60 = 10×6;
- старые ожидания parity с Eternum по Esc/RMB;
- идея механически повторять полный набор Preferences Eternum;
- Character Hub/Gallery placement как прямое следствие reference UI.

## Правило использования

Этот файл объясняет происхождение архитектурных решений shell/UI. Для новых задач сначала читать актуальные product/research docs и living roadmap, а не извлекать backlog из исторической gap-матрицы.
