# UI Targets

Утверждённые visual targets для player-facing UI How I Fall.

Repository copy — source of truth для implementation-задач и coding-agent prompts.
Google Drive остаётся удобным зеркалом для человеческого просмотра.

Текущий набор:
- `01_Main_Menu_Target_v1.png`
- `02_Reading_Target_v1.png`
- `03_Game_Menu_Target_v1.png`
- `04_Choice_Target_v1.png`
- `05_Speaker_Dialogue_Target_v1.svg` — detail/state sheet; `02_Reading_Target_v1.png` remains the base Reading composition
- `06_History_Target_v1.png`
- `07_Hotspot_Showcase_Target_v1.png` — approved TECH DEMO ONLY / NOT CANON Hotspot showcase target; apartment-at-night composition with three interactive zones (`Ноутбук`, `Записки`, `Дверь`) and cyan hotspot language. Поправка 2026-10-03 (hands-on): верхний правый элемент «Меню» и кастомная dialogue/feedback-панель на изображении НЕ авторитетны — см. блок ниже
- `08_Hotspot_Showcase_Background_v1.png` — clean approved TECH DEMO ONLY / NOT CANON apartment-at-night background for implementation; no baked hotspot labels, Menu or dialogue UI
- `09_Hotspot_Marker_Polish_Target_v1.png` — рекомендуемый marker-only target, **НА СОГЛАСОВАНИЕ**, не утверждён для реализации; контракт ниже.

Поправка target 07 по hands-on коррекции пользователя (2026-10-03, интеграционная коррекция Hotspot showcase):

- Target 07 остаётся авторитетным для ночной композиции комнаты, размещения и языка hotspot-маркеров и общей подачи Hotspot-режима.
- Верхний правый элемент «Меню», изображённый на target 07, НЕ авторитетен и не должен появляться в runtime: доступ к Game Menu в Hotspot-режиме работает только через существующий установленный путь Esc/RMB.
- Кастомная dialogue/feedback-панель, изображённая на target 07, НЕ авторитетна и не воспроизводится: Hotspot-режим не создаёт собственной панели диалога/фидбека.
- Источник истины для подачи диалога/фидбека в Hotspot-режиме — существующая принятая Reading/dialogue UI игры (обычный Dialogue Box со спикером, текстом и typing-презентацией).

## Target 09 — только визуальная подача маркеров

**TECH DEMO ONLY / NOT CANON. USER APPROVAL REQUIRED BEFORE IMPLEMENTATION.**

- Основа — свежий стартовый runtime-кадр `Hotspot` graphical E2E от master `6643b6098927dbac7764e37d740999164f56a90f`, 1920×1080. В target заменены только три marker-области; Reading shell, TECH-каптион и остальной кадр сохранены попиксельно. Незавершённое «Зд» с caret — реальный момент typing, не новая реплика.
- Рекомендуемая подача: небольшие контурные глифы и открытые угловые скобки вместо крупных круглых чипов и pill-плашек; тонкая линия с маленьким полым ромбом сохраняет привязку к объекту. Названия `Ноутбук`, `Записки`, `Дверь` неизменны, подписи вторичны относительно сцены.
- В кадре: `Ноутбук` — focus, `Записки` — available idle, `Дверь` — locked idle. Hover/focus для pointer, keyboard и controller имеет одну визуальную семантику: усиленный контур, локальное cyan-свечение, более ясная подпись и маленький шеврон. Фокус отличается не только оттенком. Idle остаётся обнаруживаемым без крупных контрастных плашек.
- Locked дополнительно обозначается знаком замка, а не только серым цветом. При фокусе locked-маркера сохраняется этот знак; focus не означает доступность действия. Существующее completed-состояние остаётся сдержанным глифом без подписи. Кадр задаёт визуальное намерение, не анимационную систему и не новые hit-регионы.
- Механики и интеграция Hotspot неизменны: три существующие точки и их семантические позиции, prerequisites/locked-door, one-shot/completion, input/navigation, невидимые hit-регионы, Save и Esc/RMB Game Menu. Нет отдельной кнопки «Меню» или частной feedback-панели; фон 08 и принятый Reading shell не переоткрываются.
- Это **design/target-only** результат: production C#, scenes, prefabs, assets и visual baselines не меняются. Target 09 не заменяет принятый marker-contract 07 до явного одобрения пользователя. Реализация допустима только отдельной задачей после такого одобрения.

Правила:
- target фиксирует visual/UX направление, но не финальный art или будущий канон;
- не копировать изображения target как production art;
- implementation сохраняет существующие product/runtime contracts, если отдельная задача явно не разрешает их изменение;
- изменение утверждённого target требует отдельного явного решения пользователя/reviewer.
