# Бенчмарк функциональности визуальных новелл

## Вопрос

Какие player-facing функциональные паттерны сильных визуальных новелл полезно учитывать для How I Fall в фазе **Polished Functional Demo First**, до подключения реального сюжета?

## Контекст

Сюжет, routes, canonical story flags и финальный арт сейчас отложены. Цель — проверить quality bar функционала и UX, а не проектировать будущий канон.

HIF уже имеет широкую VN-базу: Manual/Auto/Quick saves, Continue, Game Menu, компактный Quick Menu, Auto, seen-aware Skip, Backlog, Shared Preferences, unified input, Hide UI, Character Hub foundation, Gallery/Replay foundation, Chat/Phone, Interactive Hotspots, Map Locations и Timed Narrative Beat. Текущий статус проверяется по `docs/eternum_feature_tracker.md` и production/tests.

Исследование не разрешает implementation автоматически.

## Набор benchmark-референсов

- **STEINS;GATE / STEINS;GATE 0** — read/unread Skip, зрелая навигация, Tips/glossary-style recall.
- **The House in Fata Morgana** — conventional VN controls, History, Auto/Skip, Save/replay/chapter patterns.
- **Zero Escape: Virtue's Last Reward / The Nonary Games** — flowchart как player navigation между ветками.
- **AI: THE SOMNIUM FILES** — flowchart, Files, investigation, interactive sequences.
- **PARANORMASIGHT** — Story Chart, autosave/manual save, latest-save recovery и понятная navigation.
- **Muv-Luv / Alternative** — зрелый conventional VN QoL; suspend/resume полезен как отдельный референс, но не обязательная функция.
- **Doki Doki Literature Club / Plus** — простой и предсказуемый Menu/Preferences/input baseline.
- **Necrobarista / 1000xRESIST / Goodbye Volcano High** — показывают, где Unity особенно полезен: cinematic/spatial/interactive narrative, а не дополнительные generic menus.

## Функциональное сравнение

| Паттерн | Статус HIF | Решение |
|---|---|---|
| Manual / Auto / Quick saves | DONE | Сохранять совместимость и улучшать UI только при конкретном UX-gap |
| Continue newest valid | DONE | Сохранять; пропускает invalid newest candidate |
| Quick Load | DONE | Самое новое валидное Quick-сохранение; не переопределять как cross-family load |
| Auto | DONE | Настраивать позже с реальным контентом |
| Seen-aware Skip | DONE | Сохранять безопасную semantics |
| Backlog / History | DONE, save-scoped restore | Сохранять; History не равно rollback |
| Compact Quick Menu | DONE | `История | Пропуск | Авто | Быстр. сох.`; polish only |
| Shared Preferences | DONE | Одна общая implementation |
| Keyboard/mouse/gamepad parity | Базовый контракт покрыт | Расширять только для конкретного gap |
| Save/Load IA | DONE | Save=Manual; Load=Manual/Auto/Quick через compact navigation |
| Choice UI | Поведение есть, presentation требует polish | Ближайший UX pass |
| Relationship feedback | Технически есть, presentation PARTIAL | Заменить явный текст на короткий nonverbal cue |
| Autosave policy вокруг важных choices | Infrastructure есть, policy content-dependent | LATER с реальной историей |
| Suspend/resume | Исследован | DEFER; текущий Continue достаточно силён |
| Story Flowchart / Story Chart | Не реализован | DEFER UNTIL STORY |
| Chapter/scene replay authoring | Foundation есть | DEFER UNTIL STORY |
| Glossary/Tips/Files | Нет canonical content | DEFER UNTIL CONTENT |
| Ending/route completion | Нет canonical model | DEFER UNTIL STORY |
| Rollback/Rewind | NOT IMPLEMENTED | **REOPENED FOR BOUNDED FEASIBILITY**; implementation пока не разрешён |
| Generic minigame/QTE framework | Не нужен сейчас | NOT PLANNED |

## Что полезно делать до сюжета

- улучшать качество и интеграцию уже существующих систем;
- сохранять понятный Esc/Game Menu contract;
- защищать focus/hover consistency;
- довести Choice UI и subtle consequence feedback;
- провести отдельный rollback feasibility audit, потому что запрос пользователя подтверждён и player sentiment поддерживает recovery value;
- продолжать graphical E2E и regression coverage для значимых player-facing passes.

## Что должно ждать реального контента

- choice-safety autosave policy;
- interactive route/flowchart;
- chapter select и canonical replay entries;
- glossary/tips/lore files;
- ending list и route progress;
- canonical Character Hub content;
- story-driven Phone/Chat/Map/Hotspot/Timed Beat usage;
- investigation/evidence UI под конкретную detective scene;
- cinematic Timeline/video integration под конкретную authored scene.

## Главный вывод про Unity

Сильная сторона Unity для HIF — не возможность добавить больше меню. Его ценность — cinematic camera/timeline, spatial scenes, interactive investigation, phone/chat/diegetic UI, maps/hotspots, timed authored beats и другие конкретные narrative interactions. Использовать это только тогда, когда реальная сцена требует такого решения.

## Чего не копировать

- copyrighted code/art/text и unique layouts;
- feature только потому, что она есть в известной VN;
- route/navigation architecture до реального story graph;
- generic framework для гипотетического будущего;
- visual-only rollback без восстановления состояния.

## Рекомендация для HIF

Функциональное покрытие HIF уже широкое. Самая высокая ценность до сюжета — **качество, согласованность, discoverability и интеграция**, а не количество features.

## Решение

**APPROVED AS RESEARCH BASELINE.** Этот документ хранит benchmark knowledge и приоритеты, но сам по себе не разрешает новую gameplay feature.
