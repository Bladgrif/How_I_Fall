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

Поправка target 07 по hands-on коррекции пользователя (2026-10-03, интеграционная коррекция Hotspot showcase):

- Target 07 остаётся авторитетным для ночной композиции комнаты, размещения и языка hotspot-маркеров и общей подачи Hotspot-режима.
- Верхний правый элемент «Меню», изображённый на target 07, НЕ авторитетен и не должен появляться в runtime: доступ к Game Menu в Hotspot-режиме работает только через существующий установленный путь Esc/RMB.
- Кастомная dialogue/feedback-панель, изображённая на target 07, НЕ авторитетна и не воспроизводится: Hotspot-режим не создаёт собственной панели диалога/фидбека.
- Источник истины для подачи диалога/фидбека в Hotspot-режиме — существующая принятая Reading/dialogue UI игры (обычный Dialogue Box со спикером, текстом и typing-презентацией).

Правила:
- target фиксирует visual/UX направление, но не финальный art или будущий канон;
- не копировать изображения target как production art;
- implementation сохраняет существующие product/runtime contracts, если отдельная задача явно не разрешает их изменение;
- изменение утверждённого target требует отдельного явного решения пользователя/reviewer.
