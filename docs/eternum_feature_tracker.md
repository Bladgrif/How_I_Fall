# How I Fall — Eternum Feature Tracker

## Назначение

Компактный roadmap по UX-механикам, повторно проверенным в локальной русской сборке **Eternum 0.9.5**. Полная инвентаризация 199 пунктов A–V, edge cases и источники вынесены в [eternum_full_feature_audit.md](eternum_full_feature_audit.md).

- Срез Eternum: source-only аудит 25 верхнеуровневых `.rpy`, 2026-08-07.
- Срез How I Fall: текущие C#-скрипты, Unity-сцены, данные и тесты на `HEAD 7e2f6bc` до docs-коммита.
- Граница: переносим только полезное поведение. Не копируем код, тексты, визуал, аудио, layout и сюжетные элементы Eternum.
- Runtime Eternum и Unity в этой задаче не запускались; документ отражает проверку исходников и существующей реализации.

## Статусы

- ✅ **DONE** — рабочий контур уже есть.
- 🟡 **PARTIAL** — основа есть, но сценарий неполон или часть UI не действует.
- ⬜ **TODO** — отсутствует; начинать только по roadmap/сцене.
- 🚫 **NOT PLANNED** — сознательно не брать в ближайший план.

## Актуальный срез

| Система | How I Fall сейчас | Статус | Следующий пробел | Приоритет / риск |
|---|---|---:|---|---|
| Диалог и typewriter | `VNDialogueController`, `DialogueSceneData`, завершение текущей печати, переходы между сценами | ✅ DONE | Контентная проверка темпа; диапазон `textSpeed` требует QA | Medium / Low |
| Обычные выборы | Варианты, stat-delta, результат выбора и переход | ✅ DONE | Не смешивать с ещё не начатыми условиями | — / Low |
| Условные выборы | Декларативных условий на вариантах нет | ⬜ TODO | Типизированные условия без `eval`/произвольного кода | High / Medium |
| Game state | Оси, доверие/интерес и состояние выбора сохраняются | 🟡 PARTIAL | Нет общего реестра флагов, ресурсов и unlock-state | Medium / Medium |
| Ручные сохранения | 6 Manual-слотов, JSON + PNG 384×216, overwrite/delete/load confirmations | ✅ DONE | Поддерживать совместимость | — / High |
| Auto/Quick saves | По 6 циклических Auto/Quick-слотов; quick save/load доступны из VN UI и hotkeys | ✅ DONE | Проверять точки autosave с новым контентом | — / Medium |
| Continue | Загружает самое новое валидное Manual/Auto/Quick; в Eternum отдельной активной кнопки нет | ✅ DONE | — | — / Low |
| Совместимость saves | `SaveData` v2 и контролируемое чтение v1 manual | ✅ DONE | Любое расширение формата требует миграции | — / High |
| Quick menu | History, Skip, Auto, Save, Quick Save, Quick Load, Load, Settings, Main Menu | ✅ DONE | Back сознательно отсутствует | — / Medium |
| Auto | Таймер диалога, блокировка на choice/modal, согласование со Skip | ✅ DONE | QA задержки на длинных строках | Low / Low |
| Skip | Seen-aware, не выбирает варианты, согласован с Auto | ✅ DONE | Ctrl сейчас переключает режим, а не работает удержанием как в Eternum | Low / Medium |
| Backlog | До 100 реплик, автор, защита rich text, session-only | 🟡 PARTIAL | Решить, нужен ли backlog после Load/перезапуска | Medium / Medium |
| Rollback | Обратимого состояния исполнения нет | 🚫 NOT PLANNED | Нужна отдельная модель границ и обратимости | — / High |
| Уведомления/confirm | Toast и модальные подтверждения уже применяются в save/load UX | ✅ DONE | Добавить отдельный relationship-event | High / Low |
| Relationships | Данные отношений есть, player-facing реакции на изменение нет | 🟡 PARTIAL | Ненавязчивый toast/beat на фактический delta | **NEXT** / Low |
| Character hub / bios | Отдельного экрана отношений и биографий нет | ⬜ TODO | Только после появления контентной потребности | Low / Medium |
| Settings | Сохраняются звук, текст, экран и другие значения; реально применена только часть | 🟡 PARTIAL | Убрать ложные affordances или подключить исполнители | High / Medium |
| Input/help | Есть клавиши VN и quick menu, но нет единой карты и экрана справки | 🟡 PARTIAL | Формализовать команды и показывать только реальные bindings | Medium / Medium |
| Audio | Music/SFX работают; отдельного ambience-исполнителя нет | 🟡 PARTIAL | Не показывать неработающий ambience либо добавить канал под сцену | Medium / Medium |
| Gallery/replay | Кнопка Gallery пока сообщает `not implemented`; unlock/replay scope отсутствуют | ⬜ TODO | Делать только вместе с первым реальным extra | Low / High |
| Chat/phone | Отдельного формата сцены нет | ⬜ TODO | Typed conditions/effects, медиа и возврат в VN | Low / Medium |
| Hotspots/map | Координатных интерактивных сцен и карты нет | ⬜ TODO | Нужны accessibility и modal-return contract | Low / Medium |
| QTE/special mode | Общего контракта таймер/success/fail/retry/save-policy нет | ⬜ TODO | Сначала определить coordinator на одной авторской сцене | Low / High |
| Mini-games | Отсутствуют | 🚫 NOT PLANNED | Не строить без утверждённой сюжетной функции | — / High |

## Проверенные различия с прежним tracker

- Quick Save и Quick Load больше не `PARTIAL`: обе команды подключены в `VNQuickMenu`; `F6` — quick save, `F8` — quick load.
- `F5`/`F9` вызывают ручные Save/Load; `B` открывает backlog; `Esc` закрывает известные модальные панели.
- Ctrl в How I Fall **переключает** Skip. Это осознанно не совпадает с удержанием Ctrl в Eternum и должно быть явно показано в help.
- Quick menu, Auto и seen-aware Skip уже готовы; их нельзя повторно планировать как отсутствующие механики.
- Continue — собственное улучшение How I Fall, а не функция для копирования из активного главного меню Eternum.
- Настройки resolution, refresh rate, language, font size, game look/interface style и часть animation toggles сохраняются, но пока не меняют игру. Их нельзя отмечать `DONE`.
- Backlog не сериализуется. Rollback, conditional choices, relationship feedback, общий unlock registry, gallery/replay и special-mode coordinator отсутствуют.

## Top 10 рекомендуемых следующих механик

Порядок учитывает narrative value, существующие зависимости, размер, риск и полезность для будущих сцен.

| # | Механика | Почему сейчас | Зависимости | Размер | Риск | Решение |
|---:|---|---|---|---|---|---|
| 1 | **Relationship change feedback** | Сразу делает уже применяемые stat-delta заметными и усиливает моральный выбор | `GameState`, существующий toast, точка применения delta | Small | Low | **NEXT** |
| 2 | Settings truth pass | Убирает UI, который обещает несуществующее поведение | `SettingsManager`, обе панели Settings | Small | Low | После NEXT |
| 3 | Единая input map + Help | Сводит реальные клавиши, controller-навигацию и подсказки без расхождений | VN actions, modal policy | Medium | Medium | После truth pass |
| 4 | Backlog restoration policy | Определяет, должна ли история переживать Load, до расширения `SaveData` | backlog model, save migration decision | Medium | High | Сначала design note |
| 5 | Typed conditional choices | Даёт прошлым решениям менять доступные варианты без произвольного кода | story requirement, condition schema, tests | Medium | Medium | Не начинать в этой задаче |
| 6 | Unified modal/special-mode coordinator | Предотвращает конфликт input, Auto/Skip и saves в будущих интерактивах | modal ownership, pause/save rules | Medium | High | Перед первым special mode |
| 7 | Hide UI + screenshot UX | Дешёвый VN comfort без влияния на state | input map, UI visibility owner | Small | Low | После input map |
| 8 | Ambience channel/crossfade | Поддерживает скрытую тревогу и разделяет ambience от SFX | `AudioManager`, настройки, scene command | Medium | Medium | Под конкретную сцену |
| 9 | Timed narrative beat | Лёгкое напряжение без полноценной mini-game системы | special-mode contract, success/fail routing | Medium | Medium | После coordinator |
| 10 | Gallery/replay foundation | Нужна для Extra только когда есть реальный replay-контент | unlock registry, scoped state, safe return | Large | High | Later |

## Единственный NEXT

### Relationship change feedback

Минимальный будущий scope:

1. Реагировать только на реально применённый ненулевой relationship delta.
2. Использовать существующую toast-инфраструктуру, не копировать hearts UI Eternum.
3. Не показывать точное число, если story design должен сохранить неопределённость.
4. Не менять `SaveData`: сохраняется итоговое значение, а уведомление остаётся кратким UI-событием.
5. Проверить последовательность `choice result → delta feedback → next line/scene`, Auto/Skip и повторную загрузку.

**Вне NEXT:** Conditional Choices, новый экран отношений, rollback, gallery, QTE и любые mini-games.

## Отложено или исключено

- Прямой перенос rollback Ren'Py и жеста rollback со стороны экрана.
- Voice controls без voice pipeline; мобильный quick menu при текущей PC-цели.
- Имена saves и безлимитные страницы при понятной текущей модели 6×3.
- Пользовательский resize textbox, economy HUD, unlockable looks и внешние community links.
- Lock-picking, code lock, card/lyre/ball/reaction/score/slot loops без конкретной авторской сцены.
- Любой `eval`/`exec` для условий диалога или чата.

## Правило обновления

После каждой реализованной механики обновлять фактический статус здесь, а подробные edge cases добавлять в полный audit только при появлении нового подтверждённого поведения. Не считать сохранённую настройку реализованной, пока её эффект не виден в runtime.
