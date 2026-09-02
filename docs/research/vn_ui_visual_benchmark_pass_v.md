# PASS V0.5 — визуальный benchmark VN UI

## 1. Executive summary

Это research-only pass на `origin/master` / `4776716c00a050f75673209bc22489d90208896d`. Production UI не менялся. В дополнение к V0 выполнен ограниченный **HANDS-ON** аудит уже установленной Summer Pockets REFLECTION BLUE; screenshots reference-game остаются вне репозитория.

HIF сохраняет удачную основу: art-first Main Menu, читаемое стандартное окно реплики, компактный Quick Menu и ясные modal/focus-состояния. Подтверждённые HIF-проблемы не меняют приоритет: P0 — History рендерит cyan/square glyphs вместо текста; P1 — длинный текст четвёртого выбора теряет смысл из-за ellipsis.

Summer Pockets даёт более надёжный принцип, чем набор кнопок: **сначала объяснить режим управления, затем оставить artwork главным слоем и раскрывать плотную навигацию только в контекстной surface**. Это не повод переносить в HIF её команды, цвета, оформление или геометрию.

**Решение:** V1 остаётся узким pass для информационной целостности History и long-choice. Гипотеза «HIF Game Menu слишком тяжёлый» по Summer Pockets **NOT CONFIRMED**: сам runtime pause/Game Menu не был наблюдён hands-on, поэтому polish нельзя включать в V1 на основании прежней эстетической гипотезы.

## 2. Current HIF visual baseline

### Evidence

`PlayerUiGraphicalE2E` выполнен в V0 на чистом detached worktree с base revision. Sentinel: `status=PASS`, `playerPrefsRestored=true`; созданы fresh proof-файлы. Просмотрены Main Menu, Reading standard, Reading 125 %, 2-choice, 4/long-choice, History, Preferences, Load, Game Menu и Game Menu Rollback, включая 1280×720 для Preferences и Rollback.

### Сильные стороны

- **Main Menu:** full-screen art остаётся главным слоем; компактная левая колонка и небольшая красная focus-планка не конкурируют с композицией.
- **Reading:** полупрозрачный тёмный textbox даёт стабильный контраст; при 125 % текст не клипуется и не пересекается с Quick Menu.
- **Quick Menu:** `История | Пропуск | Авто | Быстр. сох.` компактен; Auto/Skip имеют различимые active states.
- **Preferences:** двухколоночная сетка и semantic controls читаемы на 1920×1080 и 1280×720.
- **Save/Load:** типы Manual/Auto/Quick и paging дают понятную иерархию; empty state не маскируется под сохранение.

### Доказанные gaps

- **P0 — History text rendering.** В `gameplay_backlog_1920x1080.png` тело backlog состоит из bright cyan squares. Это rendering/information defect, а не запрос на redesign.
- **P1 — длинный 4-choice текст.** В `gameplay_choice_four_long_1920x1080.png` labels обрезаются (`…`). Игрок не видит полностью смысл действия.
- **P2 — пустой Save/Load.** Контракт ясен, но large empty cards визуально массивнее своей информации. До закрытия P0/P1 не менять.

## 3. Reference set and audit status

| Reference | Статус аудита | Роль в сравнении |
| --- | --- | --- |
| Summer Pockets REFLECTION BLUE | **HANDS-ON runtime, 2026-09-02.** Already installed by user; executable `C:\Games\Summer Pockets REFLECTION BLUE\SummerPocketsRB.exe`. | Основной benchmark для onboarding выбора управления, art-first title/reading и разделения contextual/full navigation. |
| Senren＊Banka / Yuzusoft | **WEB/STEAM ONLY.** | Контрольно использовать для state clarity и settings density; не claim runtime layout. |
| Katawa Shoujo | **WEB/STEAM ONLY.** | Counter-reference для restraint и простой ADV navigation. |
| DDLC | Не использован. | Новый evidence для текущего решения не нужен. |

Никаких downloads, installs, resource extraction или изменения installation files Summer Pockets не выполнялись. Temporary screenshots не коммитятся и оставлены в `C:\Temp\HIF-VN-References\SummerPockets\`.

## 4. Summer Pockets REFLECTION BLUE — hands-on runtime audit

**Дата:** 2026-09-02. **Время активного исследования:** около 35 минут. Игра была уже установлена пользователем. Оценка ниже основана только на лично увиденном runtime; прошлые WEB/MANUAL observations отдельно не заменяют её.

### Observed states — HANDS-ON

| Surface/state | Observation |
| --- | --- |
| First-launch control screen | До title игра ясно предлагает `Legacy` и `Touch Control`, с большой preview-картинкой каждого режима и одной фразой, где изменить выбор позже (`OPTIONS → Basic → Shortcut Menu`). Это onboarding с конкретным решением, а не длинная справка. |
| Main Menu | Full-screen illustrated key art остаётся главным слоем. `START / LOAD / OPTIONS / MANUAL / EXIT` находятся в одной спокойной полупрозрачной горизонтальной полосе; title и actions визуально отделены от персонажей. |
| Options / Basic | Полноэкранная configuration surface с крупными верхними вкладками (`Basic`, `Text1`, `Text2`, `Sound`, `Voice`, `Keyboard`, `Mouse`, `Touch`, `System`). Внутри — строки label + choice/slider, teal pill для выбранного состояния, disabled control заметно приглушён, снизу контекстная подсказка. |
| New Game / opening reading | Быстрые transitions через белый fade и плавное появление иллюстрации. В раннем narrator opening показан один или два коротких централизованных ряда светлого текста с тёмным outline непосредственно на art, без постоянного textbox; маленький butterfly/advance cue не конкурирует с изображением. |
| Motion | Title и opening используют fade как ясную смену режима, а не непрерывную декоративную анимацию. |
| Legacy command model | Directly shown in onboarding preview: extensive commands скрыты у нижнего/правого края message area и раскрываются только по hover; preview содержит Q.Save/Q.Load, Save/Load, Back/Next, Auto, Rewind/Skip, Title, Exit, Options, Record, Manual и Lock. Это observation preview, не claim, что каждая команда была открыта в live scene. |

### NOT OBSERVED HANDS-ON

Не тратилось более ~15–20 минут на один state и не проходилась игра ради UI.

- Main Menu hover/selected/active state;
- обычный character dialogue с namebox и постоянным textbox;
- actual edge-revealed Legacy controls, Hide UI, Auto и Skip active state;
- History/Log surface, scrolling, speaker hierarchy и separators;
- pause/Game Menu через Esc/right-click;
- Save, Load, Quick Save/Quick Load и confirmations;
- choice UI, включая wrap/long choices;
- later map, activities, minigames, location selection или phone-like systems.

### Strongest patterns — HANDS-ON

1. **Onboarding выбирает interaction model визуально.** Две большие preview-карточки объясняют реальное последствие режима и место обратимого изменения.
2. **Title art remains primary.** Navigation организована одной лёгкой полосой, а не набором тяжёлых карточек.
3. **Settings earn their density.** Tabs, labels, selected pills и contextual help собирают много controls без смешивания их с Reading.
4. **Reading may remove chrome for a short narrator beat.** Короткий текст на art оправдан только при проверяемом контрасте и ограниченной длине.
5. **Progressive disclosure is explicit.** Preview Legacy отделяет редкие actions от base reading; большая command set не обязана быть постоянно видимой.

### Patterns rejected for HIF

- копировать artwork, logo, butterfly icon, colours, typeface, title bar или exact layout;
- переносить весь Legacy command set, edge shortcuts, Record/Manual/Lock и их naming;
- использовать text-on-art как замену HIF standard textbox: HIF должен сохранять свой проверенный readable textbox для ordinary dialogue;
- добавлять tabs/options только ради commercial feature parity.

## 5. Other references

### Yuzusoft / Senren Banka — WEB/STEAM ONLY

Источник — [официальная Steam page Senren＊Banka](https://store.steampowered.com/app/1144400/SenrenBanka/). Это не hands-on screen-layout evidence.

**KEEP AS PRINCIPLE:** видимые focus/hover/selected/disabled состояния и дисциплина spacing. **REJECT:** branding, ornaments, постоянный bottom strip и feature count.

### Katawa Shoujo — WEB/STEAM ONLY

Источник — [официальная Steam page](https://store.steampowered.com/app/3068300/Katawa_Shoujo/). Runtime UI не открывался.

**KEEP AS PRINCIPLE:** классический ADV textbox и restraint остаются достаточным baseline. **REJECT:** exact Ren'Py presentation как шаблон HIF.

## 6. Cross-reference visual principles

1. **Artwork first, legibility guaranteed.** Для short cinematic beat допустим text-on-art, но ordinary dialogue требует стабильной readable surface.
2. **Onboarding must explain a consequential mode visually and reversibly.** Это future principle; в HIF нет доказанной потребности добавлять такой экран сейчас.
3. **Один primary layer на экран.** Reading — текст; choice — решение; pause — navigation; Save/Load — records; Settings — dense controls.
4. **Progressive disclosure, not permanent command clutter.** Frequent HIF actions остаются quiet; infrequent остаются в существующем Game Menu.
5. **Состояния важнее декоративности.** Focus, hover, selected, disabled и active читаются небольшим consistent набором accent rules.
6. **Полный текст выбора важнее фиксированной высоты.** Semantic truncation недопустима; bounded wrap/row height лучше ellipsis.

## 7. Per-surface HIF gap matrix

| Surface | Current HIF | Summer Pockets HANDS-ON pattern | Observed gap | Recommended change | Priority |
| --- | --- | --- | --- | --- | --- |
| Main Menu | Art-first, compact left actions, clear focus | Art-first title with light horizontal action band | Нет доказанного HIF defect | **NO CHANGE** | NO CHANGE |
| Reading / textbox | Stable dark translucent textbox, 125 % fits | Short narrator beat can be text-on-art; not a general textbox replacement | Нет доказанного HIF defect | **NO CHANGE** | NO CHANGE |
| Quick controls | Four compact actions, active Auto/Skip | Progressive disclosure separates command density from reading | Нет доказанного gap | **NO CHANGE** | NO CHANGE |
| Choices | Long fourth label ellipsized | Choice surface **NOT OBSERVED** | HIF semantic truncation remains independently proven | Bounded wrap/row height; keep input contract | P1 |
| History | Text renders as cyan squares | Log **NOT OBSERVED** | HIF is unreadable independent of reference styling | Diagnose/fix rendering before design work | P0 |
| Preferences | Clear two-column functional grid | Dense scoped Basic settings with strong selected/disabled/help hierarchy | Нет objective defect | **NO CHANGE** | NO CHANGE |
| Save / Load | Clear type/paging/empty slots | Save/Load **NOT OBSERVED** | No new evidence | Defer | P2 |
| Game Menu / pause | Functionally clear; prior V0 called it visually heavy | Pause/Game Menu **NOT OBSERVED** | Prior visual-weight hypothesis lacks this hands-on comparison | Remove from V1; retain existing routes and rollback contract | NOT CONFIRMED |

## 8. Recommended HIF direction

**Restrained art-first cinematic VN shell** remains correct, with a narrower evidence statement:

- keep HIF Main Menu, standard Reading, compact four-action Quick Menu and Preferences contracts;
- fix reading information integrity first: History glyph rendering, then complete long-choice text;
- do not turn Summer Pockets' full Legacy command strip into HIF UI;
- do not schedule Game Menu polish in V1 without a demonstrated HIF player-facing defect or a direct comparison state.

## 9. V1 scope

### V1 — core reading information integrity

1. Fix the demonstrated History glyph/fallback/rendering defect with targeted regression coverage.
2. Make four-long-choice labels fully readable through bounded wrap/row height; validate 1920×1080 and 1280×720.

**Explicitly out:** Main Menu redesign, standard Reading redesign, new onboarding, Quick Menu actions, Game Menu polish, SaveData/settings changes, new systems, and copied reference UI.

## 10. Future interaction inspiration — NOT CURRENT SCOPE

**NOT OBSERVED HANDS-ON:** later location/activity/map/minigame/phone-like systems. Поэтому Summer Pockets не создаёт HIF backlog и не служит evidence для конкретной mechanics.

Единственный переносимый design principle из увиденного: optional interaction modes должны быть сначала понятны игроку, локализованы в своём context и обратимо настраиваемы. Реализовывать это сейчас не нужно: HIF demo не имеет актуального story material, которому требуется такая interaction surface.

## 11. QA / baseline plan for V1

1. compile/import preflight;
2. targeted History/choice tests и relevant PlayerJourney/smoke;
3. existing `PlayerUiGraphicalE2E` в настоящем runtime без `-nographics` — History, 2-choice, 4/long-choice, плюс sensitive 1280×720 states;
4. inspect fresh screenshots for glyphs, clipping, overlap и focus;
5. при PASS обновить только relevant curated visual baselines (History и long choice).

## 12. Final recommendation

**Вторая hands-on reference VN перед V1 не нужна — A.** Summer Pockets hands-on плюс fresh V0 HIF graphical evidence уже достаточно разделяют proven defects от speculative polish: History и long-choice требуют fix, Main Menu/Reading/Quick Menu/Preferences — нет, а Game Menu нужно вывести из V1.

**RECOMMENDED NEXT PASS:** `V1 — History information integrity + long-choice readability`.

**IMPLEMENTATION STATUS: NOT STARTED**
