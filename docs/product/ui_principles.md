# UI/UX principles

- Удобство и читаемость важнее декоративности.
- UI должен оставаться пригодным даже без финального art.
- Main Menu и другие экраны не следует перегружать ненужными действиями.
- Каждый player-facing control должен иметь понятную пользу и реальный runtime effect.
- `dropdown` используется для discrete single-select.
- `slider` используется для continuous value.
- `toggle` используется для bool.
- Destructive action не получает unsafe default focus.
- Keyboard, mouse и gamepad interaction должны оставаться предсказуемыми.
- Main Menu, Game Menu, Preferences и Save-Load должны ощущаться одной UI-системой.
- Не следует создавать новый UI framework ради отдельных экранов.
- Хорошие игры/VN и проверенные UX patterns используются как reference, а не копируются буквально.
- Чужие assets, layout и code не копируются.
- Перед крупным UI redesign нужно сравнить несколько референсов и сформулировать решение для HIF.
- Для внешнего visual research сначала используй сильные benchmark-референсы: признанные/top-tier visual novels и качественные релевантные игры, а не случайный набор примеров.
- После benchmark-референсов ищи task-specific примеры именно для текущего экрана или interaction; при необходимости добавляй общий game UX/accessibility guidance.
- Известность игры сама по себе не делает конкретный экран хорошим: оценивай composition, hierarchy, spacing, readability, visual weight, consistency и interaction states, затем адаптируй вывод под HIF.

## Источники решений

**Internet** — источник идей, UX patterns и внешних референсов.

**Repository docs** — источник утверждённых решений How I Fall.
