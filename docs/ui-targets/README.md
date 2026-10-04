# UI Targets

Утверждённые visual targets для player-facing UI How I Fall.

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

Поправка target 07 по hands-on коррекции пользователя (2026-10-03, интеграционная коррекция Hotspot showcase):

- Target 07 остаётся авторитетным для ночной композиции комнаты, размещения и языка hotspot-маркеров и общей подачи Hotspot-режима.
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

Правила:
- target фиксирует visual/UX направление, но не финальный art или будущий канон;
- не копировать изображения target как production art;
- implementation сохраняет существующие product/runtime contracts, если отдельная задача явно не разрешает их изменение;
- изменение утверждённого target требует отдельного явного решения пользователя/reviewer.
