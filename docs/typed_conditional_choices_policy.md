# Политика типизированных условных выборов

> **Статус:** реализовано. Документ фиксирует действующий V1-контракт availability для `DialogueChoice`.

## Цель

Choice может быть показан только когда текущий `GameState` удовлетворяет типизированным числовым условиям, при этом save/load должен продолжать хранить исходный source choice index.

## Модель данных

`DialogueChoice` содержит список `ChoiceCondition`.

Условие использует закрытые enums:

- `ChoiceStateValue`;
- `ChoiceComparisonOperator`.

Поддерживаются только существующие сохранённые numeric values `GameState`:

`Lust`, `Romance`, `Purity`, `Corruption`, `SelfControl`, `Suspicion`, `TrustMasha`, `TrustArtem`, `LeraInterest`.

Операторы V1:

- `Equal`;
- `GreaterOrEqual`;
- `LessOrEqual`.

Несколько условий объединяются через AND. Reflection, string field lookup, arbitrary expressions, callbacks, `eval` и generic dictionary запрещены.

Пустой список conditions означает «всегда доступен» и сохраняет совместимость старых assets.

## Player-facing UX

Недоступные choices **скрываются**, а не показываются disabled. Их text/requirements не раскрываются.

Один доступный вариант всё равно требует ручного выбора.

## Source index и display index

`DialogueSceneData.choices` остаётся неизменяемым source list. UI строит transient mapping видимых кнопок на исходные индексы.

`GameState.selectedChoiceIndex` всегда хранит **source index**, никогда не filtered/display index.

Это критично для save/load, result restore и branch routing.

## Save / Load

`SaveData` v3 уже содержит все нужные numeric values, поэтому отдельная schema не нужна.

Перед показом choice после Load/Continue availability вычисляется заново из восстановленного `GameState`. Видимый mapping не сериализуется.

Если `choiceResultActive == true`, уже выбранный и сохранённый choice повторно по условиям не фильтруется: restore сначала валидирует исходный index/target и восстанавливает result beat без повторного применения delta.

## Ноль доступных вариантов

Если после фильтрации доступных choices нет:

1. при наличии `defaultNextScene` происходит детерминированный переход;
2. без fallback показывается controlled error/terminal presentation и пишется diagnostic;
3. пустой choice panel и soft-lock запрещены.

## Auto / Skip

- Auto останавливается на показанном choice;
- Skip может дойти до choice, но не выбирает его;
- `skipAfterChoices` возвращается только после ручного selection согласно текущему контракту.

## Validation

Validator должен fail/report для null/unknown/malformed condition, unsupported enum и capacity problem. Invalid runtime condition fail-closed и никогда не делает choice доступным случайно.

## Будущие story flags

Не добавлять generic string-key flag system заранее. Когда реальный сюжет потребует boolean flags, добавить отдельную typed family условий и reviewed persistence contract, не ломая numeric V1.
