# Практическое руководство по разработке визуальной новеллы

## Назначение

Повторно используемая сводка практических принципов разработки VN, наблюдений игроков о UI и текущих решений How I Fall.

Это **исследовательское руководство**, а не автоматическое разрешение реализовывать новые функции. Актуальный master и repository docs остаются главным источником истины для HIF. Внешние источники дают паттерны и evidence, но не шаблон для копирования один-в-один.

Текущая фаза: **Polished Functional Demo First**. Сюжетные маршруты, канонические flags, финальный арт и финальная визуальная идентичность отложены до явного возвращения к ним.

## Классы evidence

- **A — репозиторий HIF / продуктовая истина.** Текущий master, `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md`, `AGENTS.md`.
- **B/C — опыт разработчиков/практиков.** Полезный production experience, но не универсальный стандарт VN.
- **D — мнения игроков/сообщества.** Полезны для friction, ожиданий и discoverability; не являются единственным основанием архитектурного решения.

## 1. Принципы производства

### Визуальная новелла не обязательно маленький и дешёвый проект

Даже проект, который кажется технически простым, быстро обрастает art, animation, testing, localization, marketing и sound work. Поэтому HIF должен сохранять небольшие проходы, явный scope и защиту от feature creep.

**Правило HIF:** оптимизировать небольшую polished product slice, а не максимальное количество функций.

### Сначала понимать, что именно должен доказать проект

До дорогого production должны быть понятны цель продукта, ограничения и критерий успеха. Сейчас HIF должен доказать качество **Polished Functional Demo First**, а не незаметно превратиться в final-story production или универсальный VN framework.

### Каждая механика должна иметь причину существовать

Перед добавлением системы спросить:
1. приносит ли она реальную пользу целевой аудитории;
2. может ли текущий scope безопасно её поддерживать;
3. нужна ли она именно HIF;
4. поддерживает ли она общее experience, а не конкурирует с ним.

Для VN bespoke-механика обычно должна поддерживать narrative, atmosphere, pacing или meaningful choice. Сам факт, что Unity способен её реализовать, недостаточен.

**Следствие для HIF:** generic minigames, generic QTE, большие relationship dashboards и speculative frameworks остаются отложенными. Task-specific interaction строится только под реальную сцену/задачу.

### Планирование должно показывать стоимость, а не создавать бюрократию

Полезная лёгкая цепочка:
`цель продукта → приоритетный список возможностей → roadmap → content plan → небольшие implementation passes`.

У HIF уже есть repository docs, feature tracker и Drive roadmap. Не создавать ещё один project-management framework только ради терминологии из внешнего источника.

## 2. Story/content workflow — позже

Не запускать этот процесс в текущей no-canon фазе.

Когда работа над сюжетом будет явно открыта:
1. сначала broad story skeleton;
2. только сюжетно нужные characters/locations/major branches;
3. короткие character cards для внутренней согласованности;
4. разбить крупные блоки на chapters/scenes;
5. для каждой сцены записать location, dramatic purpose, важные связи/branches и необходимость interaction;
6. многократно читать/проигрывать материал и редактировать его в движении;
7. только затем author story-dependent mechanics, autosave checkpoints, flowchart/replay/lore UI.

**Правило хранения HIF:** story work начинается в `docs/story/` / Markdown, а не с редактирования сцен, prefab или C#.

Не создавать гигантскую энциклопедию мира. Документировать только то, что реально необходимо этой игре для согласованности.

## 3. Narrative design шире текста диалога

История может передаваться через:
- choices/mechanics;
- environment и spatial staging;
- визуальную подачу персонажей;
- музыку и звук;
- transitions/camera/cinematic direction;
- diegetic UI вроде phone/chat;
- authored hotspots, map use или timed beats.

Это соответствует стратегии HIF на Unity: использовать движок позже для **конкретных cinematic/spatial/interactive narrative moments**, а не для размножения generic-систем.

Существующие foundations Chat/Phone, Map, Interactive Hotspots и Timed Narrative Beat остаются foundations, пока реальный контент не даст им конкретную работу.

## 4. Повторяющиеся наблюдения игроков об UI

### Читаемость прежде всего

Игроки ценят читаемое текстовое окно и раздражаются, когда текст кладётся прямо на изображение с нестабильным контрастом.

**HIF:** сохранять нейтральную тёмную dialogue surface и читаемость при 125%. UI не должен конкурировать с будущим art.

### Частые действия должны быть доступны, но визуально тихи

Игроки ценят Skip/Auto/History/save actions, но также любят интерфейсы, способные убирать лишний chrome. Summer Pockets часто приводят как пример сочетания полного набора действий и минимального режима чтения.

**HIF:** текущий компактный player-facing strip:
`История | Пропуск | Авто | Быстр. сох.`

Обычная навигация остаётся в `Esc → Game Menu`. Не возвращать постоянные кнопки только ради ощущения «богатого» интерфейса.

### История — восстановление информации; rollback — более сильное восстановление состояния

History/Backlog уже DONE и должен оставаться легко доступным. Но History не равно rollback.

**Текущий rollback:** **ОТКРЫТ ДЛЯ FEASIBILITY**, но не реализован. Запрос пользователя и повторяющийся player sentiment усиливают ценность функции, однако нужна безопасная state restoration. Не добавлять визуальную кнопку «назад», оставляющую `GameState`/choices несинхронизированными.

### Hover/focus должны быть согласованы

Mouse hover и keyboard/controller focus не должны оставлять два несвязанных элемента визуально выбранными. Safe modal defaults обязательны.

### Количество настроек — не цель

Другие VN могут иметь font/window opacity/per-character voice и десятки options. HIF показывает только настройки, у которых есть реальный runtime effect и понятная ценность. Semantic control type и сохранение важнее количества.

### Не перегружать постоянный chrome

Clutter, большие постоянные иконки и чрезмерно анимированные advance indicators часто раздражают. HIF предпочитает art-first composition и restrained chrome.

### Не перемещать курсор мыши игрока автоматически

EventSystem focus может меняться, но mouse position остаётся под контролем игрока. Cursor warping не вводить.

## 5. Решения HIF после исследования

### ПРИНЯТЬ/СОХРАНЯТЬ СЕЙЧАС

- читаемая dialogue surface;
- компактный Quick Menu и обычная навигация через Esc/Game Menu;
- лёгкий доступ к History;
- seen-aware Skip;
- согласованные hover/focus состояния mouse/keyboard/gamepad;
- ясная семантика Save/Load: Manual Save только Manual;
- минимальные task-scoped изменения;
- Rollback/Rewind feasibility остаётся near-term research task.

### ПОЗЖЕ, С РЕАЛЬНЫМ КОНТЕНТОМ

- story skeleton → chapter/scene graph → authored branch semantics;
- character dossiers и consistency голоса;
- content-informed autosave checkpoints;
- Flowchart/Story Chart, chapter select, endings/route completion;
- Glossary/Tips/Files с реальным lore;
- voice features при реальном voiced content;
- bespoke investigation/map/hotspot/phone/chat/timed interactions под конкретные сцены;
- cinematic/spatial Unity presentation под authored scenes.

### КАНДИДАТЫ, НО НЕ BACKLOG

Стоит помнить, но не реализовывать автоматически:
- voice replay из History/current line;
- shortcut/key hints/tooltips;
- optional minimal/auto-hide reading chrome, если hands-on докажет дополнительную пользу;
- skip-to-next-choice/scene для replay-heavy контента;
- per-character voice controls после появления реальной озвучки;
- timeline/bookmarks только после появления реальной story topology.

### ОТЛОЖИТЬ/НЕ ДЕЛАТЬ СЕЙЧАС

- generic minigame framework;
- generic QTE framework;
- постоянный visible relationship/reputation meter как основной feedback;
- большие icon strips/decorative HUD clutter;
- forced cursor movement;
- giant settings expansion без подтверждённой нужды;
- branch timeline/flowchart до реальной истории;
- feature только потому, что она есть в известной VN.

## 6. Сильные кандидаты для hands-on benchmark

Если web screenshots/manuals недостаточно и реально нужен installed-game audit, выбирать **маленький** набор под текущий вопрос:
1. **Summer Pockets** — full/minimal reading chrome, History/voice replay, Save, Esc/options split.
2. **Современная Yuzusoft VN** — mature QoL, skip variants, settings density.
3. **Hoshizora no Memoira** — configurability/text-window preferences.
4. **Katawa Shoujo** — rollback/history recovery.
5. **9-nine** — Save/Load presentation, только если снова нужен отдельный Save/Load research.

Не устанавливать и не исследовать все игры автоматически. Брать только ту, которая отвечает на текущий product question.

## Источники

### Практики/разработчики
- Konstantin Sakhnov / Kallist, Habr: https://habr.com/ru/users/Kallist/
- Part 1 — preparation: https://habr.com/ru/companies/miip/articles/824424/
- Part 2 — scenario writing: https://habr.com/ru/companies/miip/articles/838680/
- Part 3 — game design: https://habr.com/ru/companies/miip/articles/840926/
- Narrative design: https://habr.com/ru/articles/740746/
- Planning / vision / roadmap: https://habr.com/ru/articles/734978/

### Player/community sentiment
- https://www.reddit.com/r/visualnovels/comments/s7x16/best_vn_menu_systemsuser_interfaces/
- https://www.reddit.com/r/visualnovels/comments/ual80h/which_vns_have_an_amazing_ui_and_which_have_ui/

### Primary cross-check
- Summer Pockets official operation manual: https://key.visualarts.gr.jp/summer/manual/index.html

## Решение

**APPROVED AS REUSABLE RESEARCH GUIDANCE.** Исследование подтверждает текущий курс HIF и усиливает приоритет ограниченного Rollback/Rewind feasibility contract. Оно не добавляет автоматически новые implementation tasks вне упорядоченной roadmap.
