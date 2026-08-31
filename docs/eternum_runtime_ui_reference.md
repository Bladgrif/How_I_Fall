# How I Fall — runtime-референс UI Eternum 0.9.5

> **Статус:** исторический read-only runtime reference от 2026-08-12. Использовать для UX-сравнения, но не как текущую спецификацию HIF.

## Правило использования

Наблюдения из реально запущенного Eternum приоритетнее старых source-only предположений о его player-facing поведении. Но текущие решения How I Fall определяются репозиторием HIF, а не Eternum.

Не копировать assets, screenshots, source code, fonts, music, exact geometry или copyrighted text.

## Главное меню Eternum — наблюдалось в runtime

- полноэкранная композиция с крупным key visual;
- четыре заметные карточки основных действий;
- Quit вынесен отдельно и визуально слабее;
- много свободного пространства;
- hover использует заметный cyan/teal accent.

### Решение HIF

HIF не копирует карточный layout. Текущий Main Menu использует собственный full-bleed background, компактную левую навигацию и пять действий: Continue, New Game, Load, Settings, Quit. Help/About/Gallery скрыты из обычного меню текущего demo.

## Preferences Eternum

Наблюдался единый full-screen экран настроек из Main Menu и gameplay, с плотной структурой и большим количеством реально действующих контролов.

### Решение HIF

HIF использует один `SharedPreferencesView` из Main Menu и gameplay. Показываются только настройки с реальным runtime effect. Back возвращает в вызвавший контекст по HIF-контракту.

## Quick Menu Eternum

Наблюдавшийся порядок примерно соответствовал стандартным VN quick actions: Back/Rollback, History, Skip, Auto, Save, Q.Save, Q.Load, Preferences.

### Текущее решение HIF

Текущий компактный player-facing strip:

`История | Пропуск | Авто | Быстр. сохр.`

Save/Load/Settings/Menu остаются доступны через Game Menu или hotkeys согласно текущему контракту. Не возвращать старый восьмикнопочный HIF strip ради parity.

## Esc / Game Menu

Eternum использовал другие Esc/RMB semantics. HIF намеренно отличается:

- `H` — clean view;
- `Esc` — Back / Game Menu;
- confirmation и дочерние modal states имеют приоритет;
- special mode ownership нельзя обходить Game Menu.

Не «исправлять» HIF под Eternum без отдельного решения.

## Save / Load

Eternum подтверждает полезность общей Save/Load shell, 3×2 grid, ясной pagination и безопасных confirmations.

HIF использует собственный контракт:

- Manual: 10 страниц × 6 = 60;
- Auto: 6;
- Quick: 6;
- Save mode = только Manual;
- Load mode = Manual + Auto + Quick;
- Continue = newest valid среди всех семейств;
- Quick Load = newest valid **Quick**, а не cross-family latest.

## Character Hub / Gallery

Eternum показал крупный character-centric full-screen hub и Gallery внутри этого контекста. Для HIF это только визуальный reference. Текущие Character Hub и Gallery/Replay — технические foundations; канонический контент и player-facing placement отложены.

## Когда повторно запускать Eternum

Только если:

1. нужного поведения нет в этом документе;
2. старое наблюдение явно неопределённо;
3. для конкретного решения нужна точная runtime-композиция;
4. пользователь явно просит свежий hands-on comparison.
