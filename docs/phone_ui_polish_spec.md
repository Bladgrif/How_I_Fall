# Спецификация визуального и UX-polish Phone UI

> **Статус:** реализовано. Это техническая demo-спецификация, не финальный visual identity и не канон.

## Цель

Chat должен читаться как отдельный smartphone/messenger overlay поверх VN, а не как generic modal. При этом UI остаётся нейтральным и пригодным для technical demo.

## Действующий layout

- portrait `PhoneShell` по центру Canvas;
- dimmed VN background за ним;
- компактный header;
- scrollable transcript;
- persistent bottom reply area;
- Incoming messages слева, Player messages справа;
- Image entry как aspect-preserving media card;
- две большие читаемые reply cards.

Обычный dialogue shell и Quick Menu скрыты, пока Chat владеет input.

## Поведение reply

Выбор валидируется до применения effects. После выбора обе reply cards немедленно блокируются, выбранный текст добавляется ровно один раз как outgoing bubble, затем продолжается branch/terminal flow.

Terminal reply не требует второго клика для завершения.

## Ownership

Phone UI не создаёт новый manager. Existing `ChatController` и `BlockingExclusive` остаются authority.

Во время Chat ordinary dialogue, Auto, Skip, Save/Load, History, Settings, Quick Menu и Main Menu недоступны согласно текущему Chat policy и backend guards.

## Persistence

Transcript и visual state transient. Они не сериализуются в `SaveData`, `DialogueBacklog`, `DialogueReadHistory` или Replay state. `SaveData` остаётся v3.

## QA closure

Phone UI polish ранее прошёл manual graphical QA на 1280×720, 1920×1080, 2560×1440 и 3840×2160. Проверялись shell, transcript, image card, reply cards, suppression обычного VN UI, outgoing reply, return flow, clipping и overlap.

## Вне scope

Канонические контакты/портреты/сообщения, phone home screen, contact list, notifications, calls, persistent chat history и новый save schema не входят в эту спецификацию.
