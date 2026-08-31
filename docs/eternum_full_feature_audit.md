# Полный аудит функций Eternum 0.9.5

> **Статус:** исторический исследовательский архив. Актуальные решения How I Fall находятся в `docs/eternum_feature_tracker.md`, `docs/product/*`, `docs/research/*` и reviewer-roadmap на Google Drive. Если этот архив расходится с текущим repository state, приоритет у актуального репозитория.

## Назначение

Документ возник как подробный source-only аудит player-facing механик локальной русской сборки Eternum 0.9.5 и сравнение с ранним состоянием How I Fall.

Из Eternum брались только наблюдаемые UX-паттерны и поведение. Код, тексты, изображения, музыка, UI-assets, сюжет и Ren'Py-архитектура не переносились.

## Что было исследовано

Проверялись, в частности:

- dialogue/typewriter/advance;
- Quick Menu;
- Manual/Auto/Quick Save/Load;
- Continue и recovery;
- Preferences;
- History/Backlog;
- rollback;
- confirmations;
- Gallery/Replay;
- Character/relationship surfaces;
- Chat/Phone;
- interactive scenes, timed beats и другие authored interactions.

## Что из аудита осталось актуальным

- базовые VN QoL-функции важнее количества редких механик;
- Save/Load должен быть безопасным и предсказуемым;
- seen-aware Skip и History — базовые reading/recovery функции;
- Preferences должны показывать только реально работающие настройки;
- Quick Menu и Game Menu должны иметь разные роли;
- специальным authored interactions нужен единый fail-closed ownership contract;
- чужую реализацию нельзя копировать one-to-one.

## Что устарело

Многие строки старой матрицы описывали HIF до реализации текущих систем. В частности, больше нельзя считать отсутствующими:

- Auto и seen-aware Skip;
- save-scoped Backlog restore;
- typed conditional choices;
- Shared Preferences;
- Game Menu;
- Character Hub technical foundation;
- Gallery/Replay foundation;
- Chat/Phone foundation;
- Interactive Hotspot / Map / Timed Narrative Beat foundations;
- текущую Save/Load IA;
- compact Quick Menu `История | Пропуск | Авто | Быстр. сохр.`.

Rollback также больше не следует помечать как безусловное `NOT PLANNED`: по текущему roadmap он **переоткрыт для отдельного feasibility research**, но всё ещё не реализован.

## Правило использования

Не брать следующую задачу напрямую из этого архива. Сначала читать текущий `docs/eternum_feature_tracker.md`, `docs/product/*`, relevant `docs/research/*`, затем capability map и living roadmap на Drive.

Этот файл сохраняется как история исследования и источник контекста, а не как текущий backlog.
