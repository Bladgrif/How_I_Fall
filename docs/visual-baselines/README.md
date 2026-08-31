# Визуальные baseline-скриншоты player-facing UI

Эти изображения — текущий небольшой набор принятых/review baseline-состояний ключевого player-facing интерфейса.

- `QAArtifacts/` остаётся временным graphical E2E proof, игнорируется Git и не коммитится.
- Baselines — небольшой отобранный review-набор, а не копия всех `QAArtifacts`.
- Полные repository baselines остаются основным runtime evidence в source of truth.
- `review/` при наличии может содержать облегчённые копии для tooling/reviewer access; они не заменяют graphical regression proof.
- Для удобного человеческого просмотра используется зеркало Google Drive `How I Fall/Визуальное ревью/`.
- `Текущие скриншоты/` содержит свежие review images; `Референсы/` — внешние visual references; `Архив/` — необязательную историю.
- Копии на Drive не заменяют repository baselines, graphical E2E, tests или CI.
- Review previews создаются только для curated baselines, а не для каждого QA screenshot.
- После значимого visual pass и успешного graphical E2E обновляются только релевантные baseline-файлы.
- UI + baseline push может иметь статус `REVIEW CANDIDATE`, а не финальное визуальное одобрение.
- Reviewer должен открывать реальные изображения и сравнивать их с предыдущим состоянием; при большом redesign полезны внешние game/VN references.
- Baselines не являются final art, pixel-perfect golden-image tests или автоматическим эстетическим одобрением.

Имена PNG в репозитории остаются техническими идентификаторами и не переименовываются только ради перевода.

## Главное меню и настройки

- `main_menu.png` — обычное главное меню и выравнивание focus marker.
- `main_menu_quit_confirmation.png` — подтверждение выхода с одним однозначным active/hover state.
- `preferences.png` — реальные Screen Mode / Resolution TMP dropdown и максимальный Text Speed без overlap.

## Сохранение и загрузка

- `save_load_save.png` — gameplay Save / Manual после успешной записи.
- `save_load_manual.png` — gameplay Load / Manual с валидными и пустыми слотами.
- `save_load_confirmation.png` — подтверждение загрузки: безопасный Cancel default, родительский контент заблокирован.
- `save_load_slot_types.png` — controlled invalid occupied slot, визуально отличимый от empty slot. Auto/Quick дополнительно покрываются свежим graphical E2E proof.

## Основной опыт чтения

- `reading_standard.png` — обычная reading surface с `История / Пропуск / Авто / Быстр. сох.` и без временного title chrome.
- `reading_dialogue_125.png` — длинный диалог при поддерживаемом масштабе текста 125%, без clipping/Quick Menu overlap.
- `reading_choice_focus.png` — wrapped choice labels и детерминированный первый keyboard/controller focus.
- `reading_choice_hover.png` — mouse hover переносит choice focus без второго одновременно selected state.
- `reading_backlog.png` — прокрученная История с различимыми speaker/narration entries.
- `reading_auto_active.png` — заметный, но спокойный Auto active state.
- `reading_skip_active.png` — заметный, но спокойный Skip active state.
- `reading_quick_save_feedback.png` — краткий feedback быстрого сохранения поверх обычной reading surface.
