# Decision log

## 2026-08-27 — Functional Demo First

**Decision:**

- polished functional demo является текущей product phase;
- story work paused;
- final art позже.

**Why:** Сначала нужно получить стабильный и убедительный player-facing demo без предположений о будущем каноне.

**Status:** APPROVED

## 2026-08-27 — Research workflow

**Decision:**

- перед крупными UI/UX/functional решениями при полезности проводится внешнее research;
- сравниваются варианты;
- утверждённый вывод фиксируется в repository knowledge base.

**Why:** Внешние референсы помогают принимать обоснованные решения, но не заменяют утверждённую документацию проекта.

**Status:** APPROVED

## 2026-08-27 — Benchmark-first visual research

**Decision:** Для значимых UI/UX visual decisions внешнее исследование начинается
с нескольких сильных benchmark-референсов — прежде всего признанных/top-tier VN
и качественных релевантных игр. Затем добавляются task-specific examples и при
необходимости общий UX/accessibility guidance. Случайный набор малоизвестных
примеров не используется как основной benchmark.

**Why:** Сначала нужен высокий quality bar жанра, а затем — лучшие решения именно
для конкретного экрана. Известность игры не означает, что каждый её UI-screen
следует копировать; решения оцениваются и адаптируются под HIF.

**Status:** APPROVED

## 2026-08-27 — Main Menu demo scope

**Decision:**

Текущий player-facing Main Menu содержит:

- Continue;
- New Game;
- Load;
- Settings;
- Quit.

Help, About и Gallery не являются обычными Main Menu actions текущей demo.

**Why:** Scope меню должен оставаться небольшим и поддерживать основной демонстрационный поток.

**Status:** ACCEPTED

## 2026-08-27 — Shared Preferences

**Decision:** Один Shared Preferences UI используется из Main Menu и gameplay; независимые settings screens не создаются.

**Why:** Единая точка настроек сохраняет предсказуемость и целостность UI-системы.

**Status:** APPROVED

## 2026-08-27 — Deferred visual polish

**Decision:** Зафиксирован известный неблокирующий visual debt.

Main Menu:

- текущий background временный и слишком простой;
- button styling требует будущего visual polish;
- перед следующим visual pass нужно исследовать хорошие игровые/VN Main Menu references.

Preferences:

- ширина и geometry окна требуют будущего polish;
- пользователь отметил визуальное обрезание или слишком узкое окно в Main Menu Preferences и Gameplay Preferences.

Это не обозначается как bug, ломающий функционал.

**Why:** Долг важен для будущей подачи, но не блокирует текущий functional demo.

**Status:** SUPERSEDED/RESOLVED

**Resolution:** Main Menu/Preferences structural polish выполнен. Дальнейшие
aesthetic passes проходят через новый visual-review workflow.

## 2026-08-27 — Visual review workflow

**Decision:** Для player-facing visual review используются Codex graphical proof,
свежие committed visual baselines и reviewer visual comparison. Objective UI
defects исправляются в correction pass; baselines не означают финальное art или
автоматическое aesthetic approval.

**Why:** Reviewer должен иметь доступ к реальному runtime proof и небольшому
набору screenshots, а subjective aesthetic approval должна оставаться отдельной
ответственностью пользователя.

**Status:** APPROVED
