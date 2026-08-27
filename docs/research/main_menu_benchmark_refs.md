# Main Menu benchmark references

## Цель

Зафиксировать benchmark-first research для следующего **Main Menu visual polish pass** HIF. Это research-документ, а не утверждение о финальном art или каноне.

## Recommended benchmark set

Следующие признанные сильные VN-референсы рекомендуется использовать как сравнительный набор, без спорных рейтинговых утверждений:

- **STEINS;GATE**
- **The House in Fata Morgana**
- **CLANNAD**
- **Aokana**
- **Doki Doki Literature Club!**

### Что брать из benchmark set

- fullscreen art / key visual first;
- lightweight navigation second;
- compact left-side vertical group;
- clear title area;
- readable, но не тяжёлые focus states;
- без вида толстых boxed buttons;
- достаточно empty space для композиции.

### Что не брать

- layout one-to-one;
- чужой copyrighted art;
- overly cute/pink identity, не соответствующую HIF;
- heavy decorative UI, который требует уже готового финального canon art.

## Task-specific references

- **Reimei no Gakuen** — ориентир для полноэкранной визуальной подачи и подчинённой ей навигации.
- **DDLC** — genre-standard reference для понятной логики menu/preferences; берём UX-паттерны, а не оформление или layout.

## Рекомендация для HIF

Главное направление следующего pass:

- temporary fullscreen background art, явно **non-canon**;
- компактное меню слева;
- text-led items с subtle accent/focus;
- **Quit** немного отделён от основных действий;
- title area над меню;
- правая сторона оставлена под композицию и background art.

Это продолжает functional-demo direction: меню должно быть лёгким, читаемым и не зависеть от финальной истории или иллюстраций.

## Следующий prerequisite

Нужен отдельный temporary background asset. Если подходящего existing legal asset нет в репозитории, лучше использовать original generated non-canon art, а не procedural placeholder.

Следующий visual pass требует отдельного asset review и последующего graphical proof; этот документ сам по себе не изменяет production code, scenes, assets или tests.
