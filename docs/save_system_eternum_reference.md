# How I Fall — исторический референс системы сохранений Eternum

> **Статус:** исторический аналитический документ от ранней стадии Save/Load. Актуальный HIF-контракт находится в `docs/eternum_feature_tracker.md`, `docs/research/save_load_ux_benchmark.md` и текущем коде. Старые утверждения о количестве слотов/отсутствующих функциях не являются текущим backlog.

## Что было полезно в референсе Eternum

Исследование подтвердило несколько устойчивых VN-паттернов:

- Save и Load используют один понятный card/grid language;
- шесть карточек на странице — удобная плотность;
- thumbnail помогает узнавать момент сохранения;
- Manual, Auto и Quick должны быть различимы;
- overwrite/delete/load требуют безопасных confirmations;
- corrupted/incompatible save не должен ломать весь recovery flow;
- save metadata должна быть player-readable, а не debug dump.

Код, assets и Ren'Py architecture Eternum не переносятся.

## Текущий HIF-контракт, который supersede-ит старые разделы

- `SaveData.CurrentVersion == 3`;
- Manual: **60 адресов = 10 страниц × 6**;
- Auto: 6 rotating slots;
- Quick: 6 rotating slots;
- preview PNG 384×216;
- Manual overwrite/delete/load confirmations;
- invalid/corrupt slot имеет отдельное состояние;
- Continue выбирает newest valid Manual/Auto/Quick и пропускает invalid newest;
- Quick Load выбирает newest valid **Quick**;
- gameplay Load сохраняет существующий confirmation + pre-load Auto safety pipeline;
- Main Menu Load не требует gameplay-loss warning;
- Save mode показывает только Manual;
- Load mode позволяет просматривать Manual/Auto/Quick.

## Что не переносится из Eternum

- Ren'Py `.save` формат;
- unlimited/manual page behavior;
- save naming только ради parity;
- engine rollback state;
- чужие labels/text/layout;
- отдельный suspend contract без текущей необходимости.

## Историческая ценность

Этот документ сохраняется как объяснение происхождения Save/Load решений и reference behavior Eternum. Для реализации новой задачи нельзя использовать старые разделы «How I Fall сейчас» без проверки current master.
