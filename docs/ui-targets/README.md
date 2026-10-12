# UI Targets

Visual targets для player-facing UI How I Fall. Утверждённые targets и макеты на согласование явно различаются по статусу ниже.

Repository copy — source of truth для implementation-задач и coding-agent prompts.
Google Drive остаётся удобным зеркалом для человеческого просмотра.

Текущий набор:
- `01_Main_Menu_Target_v1.png`
- `01_Main_Menu_Target_v2.png` — **APPROVED** visual target для Main Menu; пользователь утвердил 2026-10-04. v1 сохранён только для истории/сравнения.
- `02_Reading_Target_v1.png`
- `03_Game_Menu_Target_v1.png`
- `04_Choice_Target_v1.png`
- `05_Speaker_Dialogue_Target_v1.svg` — detail/state sheet; `02_Reading_Target_v1.png` remains the base Reading composition
- `06_History_Target_v1.png`
- `07_Hotspot_Showcase_Target_v1.png` — approved TECH DEMO ONLY / NOT CANON Hotspot showcase target; apartment-at-night composition with three interactive zones (`Ноутбук`, `Записки`, `Дверь`) and cyan hotspot language. Поправка 2026-10-03 (hands-on): верхний правый элемент «Меню» и кастомная dialogue/feedback-панель на изображении НЕ авторитетны — см. блок ниже
- `08_Hotspot_Showcase_Background_v1.png` — clean approved TECH DEMO ONLY / NOT CANON apartment-at-night background for implementation; no baked hotspot labels, Menu or dialogue UI
- `09_Hotspot_Marker_Polish_Target_v1.png` — утверждённый marker-only target; implementation отдельно одобрена пользователем 2026-10-04; контракт ниже.
- `10_Save_Load_Target_v1.png` — **APPROVED visual direction** 2026-10-12: Main Menu Load и встроенный Game Menu Save; не pixel-perfect спецификация.
- `10_Save_Load_Target_v1_states.png` — **APPROVED visual direction** 2026-10-12: карточки, подтверждения и детали текущей навигации Target 10.

Поправка target 07 по hands-on коррекции пользователя (2026-10-03, интеграционная коррекция Hotspot showcase):

- Target 07 остаётся авторитетным для ночной композиции комнаты, размещения точек и общей подачи Hotspot-режима. Утверждённый Target 09 уточняет только marker presentation.
- Верхний правый элемент «Меню», изображённый на target 07, НЕ авторитетен и не должен появляться в runtime: доступ к Game Menu в Hotspot-режиме работает только через существующий установленный путь Esc/RMB.
- Кастомная dialogue/feedback-панель, изображённая на target 07, НЕ авторитетна и не воспроизводится: Hotspot-режим не создаёт собственной панели диалога/фидбека.
- Источник истины для подачи диалога/фидбека в Hotspot-режиме — существующая принятая Reading/dialogue UI игры (обычный Dialogue Box со спикером, текстом и typing-презентацией).

## Main Menu v2 — design pass, 2026-10-04

Исходный `master`: `2fdd662e4414c1da0dfa62fcc483342075da7d3e`.
**Production runtime этим design-pass не изменялся. Пользователь утвердил v2 2026-10-04; последующая implementation должна соответствовать v2.**
v1 сохранён без изменений только для истории и сравнения; действующая Main Menu visual goal — v2.

Вывод сравнения — **A**: art-first направление v1 остаётся сильным. Свежий runtime уступает ему по чёткости текста и деликатности активной строки. v2 оправдан как уточнение этого направления, а не новый redesign:

- Чуть компактнее логотип, больше воздуха вокруг него; красно-белая brush-идентичность сохранена.
- Чёткая единая типографика без тяжёлого shadow/outline и общая оптическая ось подписей.
- Более тихая короткая navy-подсветка с тонким cyan-маркером вместо яркой широкой плашки.
- Ровный ритм первых четырёх строк и дополнительный интервал перед `Выйти`.
- Плавное поле читаемости слева; светлый key visual остаётся главным справа, tagline — вторичным.

На v2 показан доступный `Продолжить` в активном состоянии. Hover и keyboard/controller focus используют один визуальный язык и одного владельца; это не новый default selection. Недоступный `Продолжить`, его visibility/availability, существующий набор из пяти действий, navigation/input, Esc/Quit и маршруты не меняются. PNG не задаёт анимацию или новые runtime-семантики.

v2 создан встроенным `imagegen`, просмотрен в исходных **1672×941** (как v1). Это композиционный макет, не pixel-perfect спецификация размеров для 1920×1080. Генеративные отличия фона/логотипа НЕ являются предложением заменить production assets; при реализации используются существующие assets. Сценография остаётся `TECH DEMO ONLY / NOT CANON`, без нового сюжетного канона. Альтернативы не предлагаются.

Сравнение: v1, `main_menu.png`, `main_menu_hover.png`, свежие 1920×1080 `normal_enabled`, `disabled_continue`, `hover`, `pointer_exit`, `keyboard_focus`, `settings_focus`, `quit_confirmation`. Ориентир согласованности с Hotspot — target 07 с его оговорками и `hotspot_showcase_initial.png` из exact base: читаемые подписи, navy-поле и локальный cyan-сигнал, но не круглые маркеры или HUD в Main Menu. Незавершённый Hotspot-runtime из защищённого `develop` не проверялся. Самостоятельно проверены читаемость, иерархия, единичный фокус, интервалы и отсутствие clipping/overlap на макете.

Runtime capture: существующий `PlayerUiGraphicalE2ERunner.StartAutomatedPlayMode`, Unity `6000.5.7f1`, `-batchmode` **с графикой, без `-nographics`**, exit 0, `status=PASS`, `playerPrefsRestored=true`, 77 PNG. Обычный launcher сначала заблокировался на Editor layout до старта QA; batchmode обошёл этот startup blocker. Лог не объявляется чистым: отдельно отмечены Editor Search `ArgumentOutOfRangeException` и TMP inconsistent-import diagnostic. Исправления runtime/importer вне scope. NUnit/regression suite и человеческий aesthetic QA — `NOT RUN`; baselines не заменялись.

[Свежий proof и рекомендуемый target на Drive](https://drive.google.com/drive/folders/1W0gZW2E0dusap1KS2zwyFYgbAYxVKxnF) — папка в `03 — UI — implementation & QA proof`; manifest содержит source SHA, filenames и SHA-256. Reviewer проверил реальные изображения; пользователь утвердил эстетическое направление 2026-10-04. Implementation разрешена отдельной bounded задачей.

## Target 09 — только визуальная подача маркеров

**TECH DEMO ONLY / NOT CANON. Реализация явно одобрена пользователем 2026-10-04.**

- Основа — свежий стартовый runtime-кадр `Hotspot` graphical E2E от master `6643b6098927dbac7764e37d740999164f56a90f`, 1920×1080. В target заменены только три marker-области; Reading shell, TECH-каптион и остальной кадр сохранены попиксельно. Незавершённое «Зд» с caret — реальный момент typing, не новая реплика.
- Утверждённая подача: небольшие контурные глифы и открытые угловые скобки вместо крупных круглых чипов и pill-плашек; тонкая линия с маленьким полым ромбом сохраняет привязку к объекту. Названия `Ноутбук`, `Записки`, `Дверь` неизменны, подписи вторичны относительно сцены.
- В кадре: `Ноутбук` — focus, `Записки` — available idle, `Дверь` — locked idle. Hover/focus для pointer, keyboard и controller имеет одну визуальную семантику: усиленный контур, локальное cyan-свечение, более ясная подпись и маленький шеврон. Фокус отличается не только оттенком. Idle остаётся обнаруживаемым без крупных контрастных плашек.
- Locked дополнительно обозначается знаком замка, а не только серым цветом. При фокусе locked-маркера сохраняется этот знак; focus не означает доступность действия. Существующее completed-состояние остаётся сдержанным глифом без подписи. Кадр задаёт визуальное намерение, не анимационную систему и не новые hit-регионы.
- Механики и интеграция Hotspot неизменны: три существующие точки и их семантические позиции, prerequisites/locked-door, one-shot/completion, input/navigation, невидимые hit-регионы, Save и Esc/RMB Game Menu. Нет отдельной кнопки «Меню» или частной feedback-панели; фон 08 и принятый Reading shell не переоткрываются.
- Target создан отдельным **design/target-only** проходом. После явного одобрения пользователя 2026-10-04 Target 09 заменяет marker presentation из Target 07; фон, Reading shell и gameplay-контракты остаются неизменными. Target PNG не используется как runtime overlay или baked production asset.

Правила:
- target фиксирует visual/UX направление, но не финальный art или будущий канон;
- не копировать изображения target как production art;
- implementation сохраняет существующие product/runtime contracts, если отдельная задача явно не разрешает их изменение;
- изменение утверждённого target требует отдельного явного решения пользователя/reviewer.

## Target 10 — Save/Load, design-only, 2026-10-12

**APPROVED visual direction / DESIGN ONLY.** Пользователь согласовал макет 2026-10-12; production не изменён. Implementation требует отдельной bounded задачи и не разрешается автоматически одним approval target.

База: `0a4dad45a73177ac74d9740647ec890e5313c935`, ветка `docs/near-term-ui-qa-plan`, checkout `D:\How_I_Fall\agent`. Существующее незакоммиченное изменение `docs/technical_plan.md` сохранено без правок.

### Что показано

- `10_Save_Load_Target_v1.png` (1145×1374) — два композиционных кадра: **A**, standalone Load из Main Menu с существующим key visual; **B**, Save в правой content-области Game Menu с сохранённой левой навигацией. В каждом — шесть одинаковых карточек 3×2, валидный слот под фокусом, пустой и занятый недоступный слот, три дополнительных пустых слота, metadata-сводка и компактная пагинация.
- `10_Save_Load_Target_v1_states.png` (1422×1106) — увеличенные карточки; отдельные примеры Load/Overwrite/Delete confirmation с безопасным `Отмена` default; различие текущей страницы и временного focus; полосы Load/Save и отдельный toast. Это независимые образцы состояний, **не** несколько модалей или владельцев focus одновременно в игре.
- Направление: navy-плоскости, чёткий светлый текст, спокойные blue-gray metadata, тонкий cyan-контур с тёмной подложкой у focus, небольшой warning-глиф у недоступного сохранения. Заголовок, preview, название и дата имеют ясную иерархию; удаление остаётся вторичным компактным `×` с собственным hit-регионом.
- Состояния сохраняют текущие формулировки `Пусто` и `Недоступное сохранение`. Повреждённый файл — пример занятого недоступного слота, **не** новая backend-классификация. Макет не обещает диагностику конкретной причины, восстановление или автоматическое удаление.

### Защищённые контракты

- **Manual 60 / Auto 6 / Quick 6**, сетка **3×2**. В Load одна полоса `QS | AS | 1..10`; в Save только `1..10`. `QS`/`AS` — Quick/Auto, числа — страницы Manual. Без дополнительного ряда family-tabs, стрелок, сортировки или новых действий. Страница 2 соответствует глобальным слотам 07–12; локальный номер в карточке и глобальный в сводке сохраняют текущую семантику.
- Save — только Manual: пустой слот записывается существующим маршрутом, занятый (в том числе invalid) требует текущего подтверждения перезаписи. В Load empty/invalid-карточка не loadable; отдельное удаление занятого слота остаётся доступным через существующее подтверждение. Недоступная карточка не получает load-focus из-за warning-глифа.
- Hover/keyboard/gamepad сохраняют существующее владение и Submit: **один видимый интерактивный owner**. Current page не равна focus: на state sheet текущая страница 1, временный focus — 3, без перехода страницы до Submit. В контекстном кадре белая подпись/приглушённый route-маркер `Сохранить` — текущий раздел, а не второй cyan-focus.
- Confirmations сохраняют существующие тексты/действия и safe Cancel default. Dimmer блокирует родительский ввод; после закрытия остаются текущие правила возврата фокуса. Toast имеет собственную резервную полосу и не накрывает страницы.
- Standalone `← Назад` и embedded-маршрут не унифицируются новым действием. Esc stack: confirmation → Save/Load → Game Menu → gameplay. Root Game Menu сохраняет пять действий и отдельное `Вернуться в игру`, без root `Назад`/Rollback. Main Menu не получает Save.
- `SaveData` v3, migration/validation, Manual/Auto/Quick paths, capacities/rotation, Continue newest-valid, Quick Load quick-only, pre-load autosave, failed-load recovery, History/rollback barriers, Replay/special-mode guards, hit regions и navigation/input остаются без изменений.
- Preview берётся из существующего capture и сохраняет **16:9 / preserveAspect**, без растяжения; отсутствие preview у валидного сохранения не означает corrupt. Генеративные миниатюры в PNG иллюстрируют композицию, **не** задают пиксельную геометрию, замену фоновых assets или изменение capture. Размеры/fit берутся из текущего UI, не из измерения этих board-изображений.
- Фон, миниатюра, `Первый урок` и пример даты — демонстрационные данные: **TECH DEMO ONLY / NOT CANON**. Не добавлены сюжет, главы, персонажи или канонические имена. PNG не используется как baked UI/production asset.

### Сравнение и bounded brief после approval

Просмотрены существующие targets `01_Main_Menu_Target_v2.png`, `03_Game_Menu_Target_v1.png` и baselines `save_load_manual.png`, `game_menu_embedded_load.png`, `save_load_slot_types.png`, `save_load_confirmation.png`, `save_load_delete_confirmation.png`, `save_load_strip_hover_ownership.png`. Это **сохранённые repository references, не свежий runtime proof этой задачи**. Устаревший root `Назад` на Target 03 не воспроизводится: актуальный repository/runtime-контракт главнее изображения.

Относительно текущей поверхности предложена только presentation-дельта: более цельная navy/cyan-подача карточек, различимость unavailable не одним цветом, более ясные metadata и разделение current-route/current-page/focus. Сетка, metadata-зоны, компактная навигация и лёгкая confirmation-композиция уже существуют и не планируются как новые функции. Генеративные отличия фона/логотипа не входят в предлагаемую дельту.

После approval разрешён отдельный bounded pass только по согласованной presentation-дельте существующих `ManualSaveLoadPanel`/`ManualSaveSlotView` и, лишь если необходимо, их embedded presentation. Не добавлять систему focus, новый backend, поля SaveData или новые routes. Не менять текущую initial selection ради кадра макета. Если утверждённая дельта уже обеспечена текущим UI и объективного дефекта нет — `NO PRODUCTION CHANGE`.

Acceptance будущего implementation-pass: targeted slot/focus/pagination regression, релевантный Save smoke, свежий `ManualSave` graphical E2E; при изменении embedded presentation — соответствующий `GameMenu` proof. Самостоятельно inspect screenshots и обновлять только действительно изменённые curated baselines. Этот brief **не запускает** implementation.

### Решение пользователя

2026-10-12 пользователь одобрил общую navy/cyan-подачу и сдержанный warning-глиф недоступного слота с условием аккуратной реализации. Макет задаёт направление, а не пиксельную геометрию или новый runtime contract. В implementation нужно сверять визуальный вес red-gray состояния с реальным кадром, не копировать его из PNG буквально.

Capacity, IA и Save safety уже приняты и не предлагаются на повторное согласование. Approval target не означает автоматического запуска implementation без отдельной задачи.

### Изготовление и проверки

Использован встроенный `imagegen`, не CLI/API. Brief основного prompt: «Два отдельных HIF-контекста Main Menu Load / embedded Game Menu Save, текущая сетка 3×2 и полосы QS/AS/1..10 либо 1..10, empty/valid/unavailable, тонкий единичный cyan-focus, navy/cyan-язык принятых targets, без новых действий и канона». Brief state-sheet prompt: «Три состояния карточек, три текущих confirmation с Отмена default, current-page ≠ focus, точные полосы Load/Save и отдельный toast». Коррекции: вернуть отсутствующую страницу 10 в Save; приглушить sidebar route вместо второго cyan-focus; убрать рамку пустой toast-зоны; уточнить contained-preview без изменения карточек.

Финальные два PNG просмотрены coding-agent: читаемость, иерархия, интервалы, clipping/overlap, шесть карточек в каждом контексте, полный набор 1..10, отсутствие QS/AS в Save, различимые slot states, safe Cancel и focus. PNG декодированы и проверены; `git diff --check`, точный task scope и SHA-256 исходного `technical_plan.md` проверены отдельно. Visual inspection макета не доказывает runtime-поведение.

**NOT RUN:** Unity compile/import, NUnit, smoke, graphical E2E, standalone player, physical controller и human aesthetic approval — production этим design-only проходом не менялся. Visual baselines не обновлялись. Commit/push/PR/CI не выполнялись по заданию. `master`/`develop` не изменялись.
