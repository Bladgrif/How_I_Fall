# PASS V0 — визуальный benchmark VN UI

## 1. Executive summary

Это **research pass** на базе `origin/master` / `d2323ede6ebc704c8b3a87d9e3a7be700de3766f`. Production UI не менялся.

Свежий runtime HIF подтверждает удачную основу: art-first Main Menu, читаемое окно реплики, компактное Quick Menu и ясные modal/focus-состояния. Наиболее важная находка — не «сделать больше VN-кнопок», а закрепить **сдержанную кинематографичную оболочку, где art и текст первичны, а состояния управления точны и спокойны**.

Есть один доказанный P0: в свежем `gameplay_backlog_1920x1080.png` строки истории отображаются бирюзовыми квадратами вместо читаемого текста. До следующего visual-polish pass это блокирует принятие History как player-facing поверхности. Длинные подписи четырёх вариантов выбора также обрезаются многоточием; это P1: видимость всех четырёх действий есть, но игрок теряет смысл окончания текста.

**Решение:** принять направление **restrained art-first cinematic VN shell**: визуальная тишина Summer Pockets как принцип композиции, ясность состояний и плотности Yuzusoft как принцип interaction design, плюс простая навигация Katawa Shoujo. Сохраняются текущие четыре действия Quick Menu; никакой большой постоянной полосы кнопок не предлагается.

## 2. Current HIF visual baseline

### Evidence

`PlayerUiGraphicalE2E` выполнен на чистом detached worktree с base revision. Sentinel: `status=PASS`, `playerPrefsRestored=true`; все требуемые proof-файлы созданы заново. Лично просмотрены Main Menu, Reading standard, Reading 125 %, 2-choice, 4/long-choice, History, Preferences, Load, Game Menu и Game Menu Rollback, включая 1280×720 для Preferences и Rollback.

### Сильные стороны

- **Main Menu:** full-screen art остаётся главным слоем; компактная левая колонка и небольшая красная focus-планка не конкурируют с композицией.
- **Reading:** полупрозрачный тёмный textbox держит хороший контраст на светлом classroom background; при 125 % текст не клипуется и не пересекается с Quick Menu.
- **Quick Menu:** `История | Пропуск | Авто | Быстр. сох.` компактен и визуально тих; Auto/Skip имеют различимые active states в отдельном proof.
- **Preferences:** единая двухколоночная сетка и semantic controls читаемы на 1920×1080 и 1280×720 без наложений.
- **Save/Load:** шесть карточек, типы Manual/Auto/Quick и paging имеют понятную иерархию; empty state не маскируется под сохранение.
- **Game Menu / Rollback:** затемнение корректно отделяет pause navigation от чтения; enabled/disabled rollback state доступен и не ломает компоновку в 1280×720.

### Доказанные gaps

- **P0 — History text rendering.** В `gameplay_backlog_1920x1080.png` тело backlog состоит из ярких бирюзовых квадратов. Это не вопрос вкуса: история перестаёт передавать информацию.
- **P1 — длинный 4-choice текст.** В `gameplay_choice_four_long_1920x1080.png` все четыре кнопки видимы, но их labels обрезаны (`…`). Для meaningful choice это ухудшает сравнение вариантов.
- **P1 — Game Menu визуально тяжелее Reading.** Левая непрозрачная колонка и шесть одинаковых boxed rows дают больше visual weight, чем нужен для pause layer. Это не functional defect, но следующий единый polish pass может сделать его спокойнее без смены маршрутов.
- **P2 — пустой Save/Load.** Контракт ясен, но large empty cards выглядят более массивно, чем их информационная ценность. Не менять до того, как P0/P1 reading issues будут закрыты.

## 3. Reference set and why selected

| Reference | Статус аудита | Роль в сравнении |
| --- | --- | --- |
| Summer Pockets | **WEB/MANUAL ONLY.** Открыты официальный manual и official demo download page; demo не скачивалась. | Основной benchmark для quiet art-first reading и разделения частых/редких действий. |
| Senren＊Banka / Yuzusoft | **WEB/STEAM ONLY.** Открыта официальная Steam product page; Steam trial не устанавливался. | Контрольно использовать для ясности interaction states, settings density и QoL, но не копировать ornamental presentation. |
| Katawa Shoujo | **WEB/STEAM ONLY.** Открыта официальная Steam product page; free Steam release не устанавливался. | Простой counter-reference для скромной навигации и прозрачных in-game overlays. |
| DDLC | Не использован. | Три выбранных reference уже дают достаточный контраст; дополнительный baseline не меняет решение. |

Перед загрузками проверены C: **24.9 GB free**. Summer Pockets official demo заявлена как `1,651,626,504 bytes`; места достаточно, но download не нужен для этого ограниченного решения. В Steam common не обнаружены Summer Pockets, Senren＊Banka или Katawa Shoujo; не использовались login, purchase, DRM workaround, mirrors или сторонние downloads. External disk usage: **0 B**.

## 4. Summer Pockets observations

Источник — [официальный manual](https://key.visualarts.gr.jp/summer/manual/index.html) и [official demo page](https://key.visualarts.gr.jp/summer/sp/download.html), без runtime claim.

Manual разделяет title actions (`START`, `CONTINUE`, `LOAD`, `CONFIG`, `QUIT`) от reading actions (`Q.SAVE`, `Q.LOAD`, `SAVE`, `LOAD`, `BACK`, `AUTO`, `SKIP`, `TITLE`, `CONFIG`, `LOG` и др.). Он также показывает, что save/load — отдельная поверхность с pages, Auto/Quick histories и возвратом в игру.

**KEEP AS PRINCIPLE:** частые reading actions должны иметь чёткий вес и не разрушать artwork; глубокая navigation может жить в отдельном menu; save/load должен быть отдельным читаемым task surface.

**DO NOT COPY:** правый edge-save shortcut, названия/набор команд, 100 pages, record/social controls, конкретные изображения, typography и оформление Key.

**REJECT FOR HIF:** расширять current compact Quick Menu до полного Summer Pockets strip. HIF уже подтверждает достаточный контракт `History | Skip | Auto | Quick Save`; Save/Load/Preferences остаются в Game Menu.

## 5. Yuzusoft / Senren Banka observations

Источник — [официальная Steam page Senren＊Banka](https://store.steampowered.com/app/1144400/SenrenBanka/); это WEB/STEAM audit, не hands-on runtime inspection. Страница подтверждает PC VN, controller support и Steam Cloud, но не является доказательством конкретного screen-layout.

Поэтому для HIF берётся только безопасный benchmark principle, который следует проверить в будущих runtime references: modern VN UI должен ясно различать focus, hover, selected, disabled и active состояния, а settings должны быть плотными только пока сохраняют сканируемость.

**KEEP AS PRINCIPLE:** видимое состояние управления, ясный contrast selected/disabled и дисциплина spacing.

**DO NOT COPY:** Yuzusoft branding, ornaments, character/art composition, цветовую идентичность, конкретные панели или menus.

**REJECT FOR HIF:** добавлять большой QoL inventory, flowchart, постоянный bottom strip либо dozens of settings только ради parity с mature commercial VN.

## 6. Katawa Shoujo observations

Источник — [официальная Steam page](https://store.steampowered.com/app/3068300/Katawa_Shoujo/), WEB/STEAM ONLY. Страница подтверждает free release, traditional ADV text-box model и Ren'Py engine; gameplay UI runtime не открывался.

**KEEP AS PRINCIPLE:** классический ADV textbox и простая navigation hierarchy остаются достаточным baseline; complexity не является самостоятельной ценностью.

**DO NOT COPY:** конкретную типографику, Ren'Py assets/theme, menus или original layout.

**REJECT FOR HIF:** возвращаться к bare-minimum presentation ценой уже работающих HIF focus, rollback и Save/Load states.

## 7. Optional DDLC observations

Не использован: после Summer Pockets, Yuzusoft и Katawa Shoujo дополнительный простой baseline не добавил бы нового решения. Это осознанный scope control, не пробел evidence.

## 8. Cross-reference visual principles

1. **Artwork first, legibility guaranteed.** Artwork не должен становиться фоном для неограниченного текста; dark/translucent reading layer нужен, когда контраст нестабилен.
2. **Один primary layer на экран.** Reading — текст; choice — решение; pause — navigation; Save/Load — records. Не оставлять несколько равноправных панелей одновременно.
3. **Состояния важнее декоративности.** Focus, hover, selected, disabled и active должны читаться одним небольшим набором accent rules.
4. **Density должна следовать задаче.** Preferences и Save/Load могут быть плотнее Reading; они не должны переносить этот вес в textbox или Quick Menu.
5. **Полный текст выбора важнее одинаковой высоты строк.** Если label длиннее, surface обязан дать wrap/height, а не скрывать смысл ellipsis.
6. **Feature restraint.** Набор стандартных VN actions полезен только когда он соответствует HIF contract и имеет runtime effect.

## 9. Per-surface HIF gap matrix

| Surface | Current HIF | Strong reference pattern | Observed gap | Recommended change | Priority | Production files likely touched | Risk |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Main Menu | Art-first, compact left actions, clear focus | Art leads; navigation subordinate | Нет доказанного defect | NO CHANGE | NO CHANGE | — | Не превратить text menu в boxed dashboard |
| Reading / textbox | Good contrast, 125 % fits | Quiet textbox protects art | Нет clipping/overlap | NO CHANGE | NO CHANGE | — | Не расширять chrome |
| Quick controls | Four compact actions, active states | Frequent actions stay quiet | Нет доказанного gap | NO CHANGE | NO CHANGE | `VNDialogueController` only if regression later proves need | Не вернуть лишние actions |
| Choices | 2-choice clear; four long labels ellipsized | Full decision text, clear focus | Semantic truncation | Wrap labels; allow bounded dynamic row height; retain four-slot and input contract | P1 | `VNDialogueController`, existing choice presentation/view | Layout regression at 1280×720 |
| History | Correct modal layer, but unreadable body glyphs | Log restores information | Cyan square glyphs replace text | Diagnose/fix font/fallback/rendering before visual styling | P0 | `DialogueBacklog`, backlog presentation/font setup | Must preserve existing history data and navigation |
| Preferences | Clear two-column layout at 1920/1280 | Dense only while scanable | Нет clipping/overlap | NO CHANGE | NO CHANGE | — | Не добавлять speculative controls |
| Save / Load | Clear type/paging/empty cards | Separate information surface | Empty cards visually over-weighted | Defer: re-evaluate after core reading fixes | P2 | `ManualSaveLoadPanel` | Avoid SaveData/slot semantic changes |
| Game Menu / pause | Functionally clear but visually heavy column | Simple overlay hierarchy | Six equal boxed rows dominate pause layer | Reduce visual weight only after P0/P1; preserve routes and rollback visibility | P1 | `VNGameMenuView` | Modal/focus/rollback regression |

## 10. Recommended unified HIF direction

**Restrained art-first cinematic VN shell.**

- **Composition:** keep the image/background readable and reserve stable quiet zones for text/navigation; HIF Main Menu is already the correct starting point.
- **Reading:** retain the dark translucent textbox, broad margins and current compact Quick Menu. It is stronger than a feature-heavy bottom control strip for the demo.
- **State language:** use the current limited cyan/red accent vocabulary consistently for actionable focus, active Auto/Skip and safe/destructive differentiation; no new visual identity is proposed.
- **Navigation:** keep Game Menu as the home of infrequent actions and Save/Load as a dedicated surface.
- **Density:** keep Preferences functional and grid-based; do not import commercial-VN option count.

This direction validates the initial hypothesis only after narrowing it: **Summer Pockets contributes quiet layering, not its command set; Yuzusoft contributes state clarity, not its branded density; Katawa Shoujo contributes restraint, not a visual template.**

## 11. What NOT to copy

- чужие artwork, sprites, screenshots, fonts, sound, UI textures, branded motifs, names or layouts one-to-one;
- Summer Pockets edge shortcut, its large command set, 100-page save model or social/record features;
- Yuzusoft decorative identity, high-QoL feature count and any commercial menu arrangement;
- Katawa Shoujo’s exact Ren'Py presentation or low-feature menu as a requirement;
- чужой text-box geometry as a substitute for HIF runtime proof.

## 12. Proposed implementation scope

Один bounded pass достаточен; искусственно делить его на V1/V2/V3 не нужно.

### V1 — core reading information integrity and restrained pause consistency

1. Исправить доказанную читаемость History (glyph/fallback/rendering) с targeted regression coverage.
2. Сделать 4-choice/long-choice labels полностью читаемыми через bounded wrap/row height, проверив 1920×1080 и 1280×720.
3. Только если два пункта не расширяют scope: облегчить visual weight существующего Game Menu без изменения actions, routes, rollback semantics, Quick Menu или input map.

**Не входит:** новый art, новая font family, Main Menu redesign, SaveData/settings changes, новые Quick Menu actions, new layouts copied from references, новые systems.

## 13. QA / baseline plan

Для V1:

1. compile/import preflight;
2. targeted History/choice tests и relevant PlayerJourney/smoke;
3. existing `PlayerUiGraphicalE2E` в настоящем runtime (без `-nographics`); доказать History, 2-choice, 4/long-choice, Game Menu and Rollback at 1920×1080 plus existing sensitive 1280×720 states;
4. лично inspect fresh screenshots for glyphs, clipping, overlap, incorrect focus/active/disabled state;
5. при PASS обновить только curated relevant images in `docs/visual-baselines/` (History, long choice, Game Menu), не весь `QAArtifacts`;
6. UI + baseline push = **REVIEW CANDIDATE**, не финальное aesthetic acceptance.

## 14. Risks

- P0 screenshot может быть обусловлен конкретной font fallback/rendering path; сначала воспроизвести на targeted runtime fixture, не менять data model на основании одного visual symptom.
- Dynamic choice height может столкнуться с textbox/Quick Menu на 1280×720; обязательно ограничить layout и повторить graphical proof.
- Game Menu polish не должен размыть safe focus, modal layering или доступность Rollback.
- Эта рекомендация опирается на Summer Pockets manual и Steam/web material для остальных references, а не на installed hands-on sessions. Если V1 требует более тонкого comparison, сначала провести отдельный, разрешённый пользователем installed audit вне repository.

## 15. Final recommendation

Не начинать широкий aesthetic redesign. Сначала выполнить **один V1 pass**: восстановить информационную читаемость History, показать полный смысл длинных choices и только затем при необходимости облегчить существующий Game Menu. Main Menu, standard Reading, compact Quick Menu, Preferences и базовая Save/Load hierarchy уже достаточно сильны и должны быть сохранены как contracts.

**IMPLEMENTATION STATUS: NOT STARTED**

**RECOMMENDED NEXT PASS:**
`V1 — core reading information integrity: History glyph rendering + long-choice readability, with a bounded Game Menu weight correction only if it stays task-scoped.`
