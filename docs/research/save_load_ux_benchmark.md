# Бенчмарк UX сохранения и загрузки

## Референсы

- **The House in Fata Morgana** — традиционный Save/Load и быстрые действия; полезен как пример сдержанности, а не визуальный шаблон.
- **STEINS;GATE / STEINS;GATE 0** — зрелая навигация и разделение частых действий чтения от операций сохранения.
- **Doki Doki Literature Club / DDLC Plus** — компактный и сразу понятный baseline Menu/Preferences.
- **PARANORMASIGHT** — ясная навигация и controller-friendly interaction.
- **AI: THE SOMNIUM FILES** — ручные сохранения рядом со структурированной player navigation; важен принцип иерархии, а не detective UI.

## Сильные паттерны

- Текущий контекст (`Save` или `Load`) и тип сохранения должны быть понятны до чтения деталей слота.
- Thumbnail помогает первым распознать валидное сохранение; дата и контекст должны быть краткими.
- Empty, valid и unavailable/corrupt — разные состояния и не должны иметь одинаковую формулировку/affordance.
- Разрушающие действия визуально и навигационно вторичны; в подтверждениях безопасный Cancel/No является default.
- Keyboard/gamepad selection имеет стабильное понятное состояние независимо от pointer hover.
- Компактная предсказуемая сетка лучше скрытых controller-only маршрутов.

## Текущий HIF

HIF использует общую 3×2 панель из шести карточек с 16:9 preview и семьями Manual/Auto/Quick. Подтверждения, Main Menu/Game Menu entry points и backend уже существуют.

Актуальная информационная архитектура:
- Save → только Manual;
- Load → Manual/Auto/Quick через одну компактную область family/page navigation;
- `Quick Load` → самое новое валидное Quick-сохранение;
- `Continue` → самое новое валидное Manual/Auto/Quick;
- отдельный `Latest Load` не добавлен;
- `SaveData` v3 и backend semantics не переписываются.

## Рекомендованное направление

Сохранять существующую нейтральную сетку, preview-first и безопасные подтверждения. Улучшать hierarchy/focus/navigation, а не перестраивать систему сохранений.

## Не принимать без отдельной задачи

- изменение `SaveData`;
- новые типы слотов;
- cloud/suspend save;
- backend rewrite;
- массовое удаление всех сохранений;
- глобальный focus manager;
- копирование branded интерфейса другой игры.
