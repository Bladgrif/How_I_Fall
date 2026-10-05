# Спецификация parity shell: Main Menu / Game Menu / Preferences

> **Статус:** историческая спецификация; Phases 1–6 завершены. Текущий UI-контракт фиксируется в `docs/product/*`, `docs/research/*` и `docs/eternum_feature_tracker.md`.

## Основные принципы

- один player-facing Preferences screen из Main Menu и gameplay;
- Quick Menu отвечает за частые reading actions, Game Menu — за навигацию;
- нет видимых настроек без реального runtime effect;
- SaveManager, Replay, Chat, Character Hub, Hide UI, Auto/Skip, AudioManager и Special Mode не переписываются ради shell parity;
- HIF использует свои UX-решения, а не копирует Eternum layout.

## Текущий Main Menu

Обычный demo-контракт:

1. Continue;
2. New Game;
3. Load;
4. Settings;
5. Quit.

Help/About/Gallery скрыты из обычной player-facing композиции. Continue активен только при наличии валидного сохранения.

## Текущий Game Menu

`Esc` из стабильного gameplay открывает Game Menu. Он остаётся отдельным от Quick Menu и использует существующие backend/actions.

Обычный root: `Сохранить / Загрузить / Настройки / Главное меню / Выйти` и отдельное `Вернуться в игру`. Root `Назад`/`Откат` отсутствует; закрытие — Esc/RMB или Return. Reading остаётся видимым под меню, Quick Menu видим, но неинтерактивен, typewriter приостановлен.

Back-stack:

`confirmation → Save/Load → Game Menu → gameplay`.

Special modes, Hide UI и активные child modals имеют более высокий input ownership и не обходятся Game Menu.

## Текущий Quick Menu

Текущий компактный strip для обычного demo:

`Назад | История | Пропуск | Авто | Быстр. сох.`

Старые восьмиэлементный и четырёхэлементный варианты больше не являются текущим контрактом. `Назад` и колесо вниз используют guarded `TryRollback`; в Replay rollback скрыт/заблокирован. Underlying Save/Load/Settings/Menu APIs и hotkeys сохраняются. Evidence exact-base сверки см. в `docs/eternum_feature_tracker.md`.

## Preferences

Используется один `SharedPreferencesView`. Контролы семантические и отображаются только при наличии реального consumer. Persistent `Show Quick Menu` сохраняется через Settings authority и не попадает в `SaveData`.

## Save / Load

Текущий контракт supersede-ит старую часть этой spec:

- Manual = 60 адресов, 10 страниц × 6;
- Auto = 6;
- Quick = 6;
- Save mode показывает только Manual;
- Load mode позволяет просматривать Manual/Auto/Quick;
- Quick Load загружает newest valid Quick;
- Continue выбирает newest valid среди Manual/Auto/Quick;
- gameplay Load сохраняет существующий confirmation/pre-load safety contract.

## Modal / Escape

Для destructive actions безопасный default — Cancel/No. `Esc` отменяет верхний modal layer и не выбирает destructive action.

## Использование документа

Сохранять как историю формирования shell architecture. Не планировать новые passes по старым `Target`/`TODO` строкам без сверки с текущим repository state и Drive roadmap.
