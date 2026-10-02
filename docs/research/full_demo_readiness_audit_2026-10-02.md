# Полный аудит готовности demo — 2026-10-02

## 1. Решение и границы

**NO PRODUCTION CHANGE.** Обычный функциональный demo-shell готов к дальнейшей работе с контентом; новый generic-функционал не нужен. На проверенных обычных player journeys критического runtime-блокера не воспроизведено. Это не утверждение об отсутствии любых дефектов и не новое эстетическое одобрение.

- Exact audited SHA: `e56ba7aee8c292652f5d56949c3a38b3af3cb3ef`, совпал с expected и свежим `origin/master`.
- Checkout: `D:\How_I_Fall\develop`, Unity `6000.5.7f1`, warm `Library`; ветка `audit/full-demo-readiness-2026-10-02`.
- Исходные локальные TMP fallback и `player_ui_graphical_result.txt` сохранены отдельно и в адресный stash; для проверки база очищена от этих локальных дельт. После QA исходные файлы восстановлены, stash сохранён. Другой worktree не создавался.
- Изменение для PR — только этот документ. Production C#, сцены, prefab, references, SaveData, Packages, ProjectSettings, art/audio и baselines не изменяются в итоговом diff.
- Приоритет: **04 Choice → 05 Speaker/Dialogue detail → проверка готовности Hotspot → один небольшой non-canon showcase**. History/Save-Load targets — ниже, не обязательные ворота перед сюжетом.

Обозначения: **VERIFIED / local** — выполненная проверка с локальным evidence; **VERIFIED / code** — установленный факт реализации, не graphical reproduction; **NOT VERIFIED** — гипотеза/непокрытый сценарий; **NOT RUN** — проверка не запускалась. GitHub CI отделён от локального Unity QA.

Источники решений: `AGENTS.md`, `docs/product/{demo_goal,ui_principles,review_workflow,decision_log,agent_orchestration}.md`, `.agents/skills/{hif-visual-qa,hif-debug-routing}/SKILL.md`, текущий код/тесты, `docs/visual-baselines/README.md`, `docs/eternum_feature_tracker.md`; research по Reading, visual benchmark, прежнему readiness и content pipeline. Исторические PASS и прежние defects не перенесены в текущий аудит без проверки.

Прочитаны актуальные Drive [карта возможностей](https://docs.google.com/document/d/1EgQ0I9OHyiv-ROBAt02Weuan73hxV_jPetS65IG7hqI/edit), [roadmap](https://docs.google.com/document/d/1XWzWrjeyYR8FYTDK-lhfBgGRBB3ueiWVqOsb_mRN_AU/edit) и [README UI Target v1](https://drive.google.com/file/d/17xV5OjyfstYYihcIGF0Lcmml39ePVEb2/view). Последняя roadmap фиксирует закрытый цикл Main Menu / Reading / Game Menu и merge PR #34 на audited SHA. Drive не редактировался; дальнейшая синхронизация остаётся задачей reviewer.

## 2. Реально выполненная validation

Команды из checkout: `tools/run-unity-tests.ps1 -Mode EditMode`, `-Mode PlayMode`, `-Mode Smoke`; `tools/run-graphical-e2e.ps1 -Scenario PlayerUi`, `GameMenu`, `ManualSave`, `SaveBackendV2`. Graphical запускался **без `-nographics`**. Использованы штатные сценарии, включая их уже существующие 1280×720 состояния; новой multi-resolution автоматизации нет.

| Проверка | Результат / local evidence |
|---|---|
| Compile/import preflight перед каждым graphical запуском | PASS, exit 0, `error CS` не найден; warm import, не cold reimport |
| EditMode, полный existing suite | **40/40**, failed 0, skipped 0; NUnit XML |
| PlayMode, полный existing suite | **74/74**, failed 0, skipped 0; NUnit XML |
| `HowIFallCiSmokeTests.RunAll` | **30/30 групп**, exit 0; это группы smoke, не 30 NUnit tests |
| `PlayerUi` | PASS; **75 PNG**, sentinel `2026-10-02T06:46:09Z`, `playerPrefsRestored=true` |
| `GameMenu` | PASS; **20 PNG**, sentinel `2026-10-02T06:47:43Z`, `playerPrefsRestored=true` |
| `ManualSave` | PASS; **16 PNG**, sentinel `2026-10-02T06:48:56Z` |
| `SaveBackendV2` | PASS; **10 PNG**, sentinel `2026-10-02T06:49:58Z` |
| Screenshot inspection | **46 свежих кадров**: 42 при 1920×1080, 4 при 1280×720; поимённый список ниже |

В составе 74 PlayMode: PlayerJourney **7**, Rollback **14**, WheelReading **11**, RMB **5**, GameMenuFocus **8**, SaveLoadFocusOwnership **7**, History **2**, PreferencesInteraction **7**, Hotspot **1**, Map **1**, остальные Main Menu/settings/input **11** — все Passed. Smoke дополнительно проверяет Chat, Timed Beat, Character Hub, coordinator, save migrations, conditional choices, project/scene integrity. Функциональное покрытие этих foundations не равно проверке их финального внешнего вида.

**Сохранение evidence.** Логи/XML/PNG/sentinels архивированы в `D:\Temp\hif-audit-20261002-evidence\`; graphical — в подкаталогах `PlayerUi`, `GameMenu`, `ManualSave`, `SaveBackendV2` (`screenshots/`). Первичный последовательный EditMode/PlayMode запуск завершился exit 0, но следующий Unity старт очистил предыдущие результаты в `Temp/CodexTests`. Поэтому EditMode и PlayMode повторены с немедленным архивированием XML; числа выше взяты именно из сохранённых повторных запусков. `GameMenu` перезаписывает папку proof `PlayerUi`, поэтому первый набор сохранён до его запуска. Не считать exit 0 без XML достаточным доказательством.

SHA-256 сохранённых XML: `EditMode_all_results.xml` — `2768a831ca58b7108364f919dec59f8b28503571b222391141b689be2f525228`; `PlayMode_all_results.xml` — `c6a84ce4bf5934c6e10c71f6825ea59c205bbe5fc97f00bceb3c21638855c2a4`. Весь архив остаётся локальным, не опубликованным PR artifact; удаление этой папки потеряет первичное runtime evidence. Сохранность двух исходных локальных файлов после QA проверена SHA-256 byte equality; исходный sentinel возвращён, свежие результаты следует брать из архива, не из корневого старого sentinel.

Smoke/Unity создали transient YAML whitespace, TMP kerning и ProjectSettings reserialization. Дельты просмотрены, сохранены в `test-generated.patch` и адресно возвращены к HEAD; начальные пользовательские изменения хранились отдельно. Graphical начинался после этой очистки. Это побочные записи QA/import, не разрешённая production-реализация.

### Ошибки и ограничения доказательства

- Unhandled HIF runtime exception или compile error в завершённых проверках не найден. В логах есть временные сообщения Unity Licensing (`Access token is unavailable`), после которых запуск и проверки завершаются успешно; это не воспроизведённая ошибка HIF.
- Smoke выдаёт **12** предупреждений о зарегистрированных сценах, недостижимых от `ui_test_scene`. Технические изолированные fixtures не доказывают недостижимость настоящей New Game: она отдельно проверена PlayerJourney. Удалять эти сцены нельзя.
- Ошибки corrupt profile/save и отказ удаления read-only fixture встречаются в негативных тестах. Они не равны сбою пользовательских сохранений; suite завершается PASS. Save runners переключают manager в temporary test directories; строка `saveDirectory` в ManualSave sentinel сама по себе этого не объясняет.
- Упомянутые в старой roadmap три CS0114 в тестах в сохранённых warm-run логах заново не появились. Их отсутствие после полного rebuild **NOT VERIFIED**; cold rebuild ради этого не запускался.
- Merge SHA `e56ba7a…` не имеет PR-triggered workflow в ответе GitHub API. Это соответствует политике без повторного CI на master, но **не означает Unity CI PASS на этом SHA**. **GitHub/CI confirmed:** для предшествующего docs-only PR #34 run [36908570291](https://github.com/Bladgrif/How_I_Fall/actions/runs/36908570291) имеет успешные classification и CI Gate, обе Unity jobs skipped. CI нового audit PR проверяется отдельно; пропуск Unity jobs по классификации не заменяет локальные результаты выше.
- Новый standalone build, длительный soak, физический gamepad, человеческое aesthetic review — **NOT RUN**. Автоматические EventSystem/virtual-input тесты не выдаются за physical-controller QA.

## 3. Main player journey и что сохранять

| Поверхность / переход | Проверенное состояние и решение |
|---|---|
| Main Menu → New Game | Реальный маршрут и музыка проверены PlayerJourney; пять действий, Continue fallback, hover/keyboard/pointer-exit проверены. **KEEP / frozen** layout, target-язык и 0.12s fade |
| Reading: narration / speaker / 125% | Свежие PNG: текст полностью виден, имя отделено cyan accent, narration без пустого nameplate; пересечения с Quick Menu не наблюдались. **KEEP** композицию/scrim; дополнять спецификацию деталей, не redesign |
| 2 / 4 / long choices | Все четыре текста fixture видны целиком, включая четырёхстрочный четвёртый вариант; initial focus/hover различимы, textbox не перекрыт. Старый ellipsis defect не воспроизведён. Стиль выбора остаётся отдельной центральной карточкой |
| History → Reading | Длинные имена, narration, длинные entries, latest emphasis и scroll читаемы. Cyan-square defect из старого research не воспроизведён. Обрезанная крайняя строка на границе scroll viewport — нормальная маска, не потеря записи |
| Auto / Skip / Quick Save | Подчёркнутые active states и краткий feedback видны; guards/seen-aware semantics покрыты smoke и runtime tests. Quick Menu: `Назад / История / Пропуск / Авто / Быстр. сох.` |
| Rollback | Полное восстановление state/presentation и повтор выбора — тесты + PNG; 12 in-memory checkpoints, не save-history. Колесо вперёд/от игрока = forward, назад/к игроку = rollback |
| Reading → Game Menu → Reading | Фон/персонаж/имя/реплика и Quick Menu сохраняются; Quick Menu inert, typewriter pause, scrim 0.38. Пять основных строк + отдельное Return, нет root `Назад`. **KEEP / frozen** |
| Game Menu → Save / Load → Return | Встроенная сетка не пересекает левую навигацию, Return focus восстановлен. Manual save/load и Continue реально восстанавливают state; не только открывают панель |
| Preferences → Reading / Menu | Один `SharedPreferencesView`; dropdown, draft/apply/back, runtime effects, возврат и focus покрыты. **KEEP**, второй settings screen не нужен |
| Main Menu / overwrite / load / delete confirmations | Безопасный Cancel/Нет, dimmer, читаемый текст; guards проверены. Новую modal architecture не строить |

Это совокупное доказательство существующими journey tests и graphical scenarios, **не запись одного непрерывного ручного прохождения**. Для named speaker, длинного текста, choices, History и Game Menu runner использует временные runtime fixtures. Их персонажи/реплики — **TECH DEMO ONLY / NOT CANON**, не утверждённый сюжет.

Смысл «frozen»: сохранить принятый layout, navigation, ownership и presentation contracts; не объявлять все будущие тексты/фоны уже проверенными. Art/story, любое имя любой длины и любой choice-content не заморожены.

## 4. Главные gaps: факт → эффект → решение

| Приоритет | Evidence и классификация | Следующее ограниченное действие |
|---|---|---|
| 1 — Choice specification | **VERIFIED / visual + code:** центральная navy-карточка с заголовком «Что сделать?» заметно плотнее floating Reading; 02 target выбора вообще не показывает. Это разрыв спецификации/вопрос art direction, **не доказанный функциональный дефект** | `04_Choice_Target_v1`: согласовать композицию и состояния, не просто менять цвет/декор |
| 2 — Speaker/dialogue edge cases | **VERIFIED / target inspection:** 02 показывает короткое имя и одну завершённую реплику. Не задаёт длинное имя, narration, typewriter/complete, верхний предел текста и масштабов | `05_Speaker_Dialogue_Target_v1` как detail/state sheet поверх принятого 02, не новый Reading |
| 3 — Hotspot ещё не art-backed showcase | **VERIFIED / code:** `InteractiveSceneData.background` объявлен, но `InteractiveSceneController.TryStart` его не применяет; `BuildRuntimeUi` всегда берёт `GetRuntimeBackgroundSprite()` (стр. 49–61, 123). Map свой `map.background` применяет. Полированный showcase с назначенной иллюстрацией сейчас нельзя обещать | Проверить non-null sprite runtime, затем разрешённая отдельная узкая коррекция binding + regression; для текущей технической схемы это не blocker |
| 4 — Special-mode interaction proof | **VERIFIED / coverage gap:** Hotspot/Map PlayMode выполняют pointer-click routes; их controllers не назначают initial EventSystem focus. Новая клавиатурная сессия, menu-return и смена input modality не подтверждены. Факт отсутствия назначения — code; реальная невозможность пройти с controller — **NOT VERIFIED** | До player-facing showcase проверить mouse/keyboard/virtual gamepad entry → action → menu → return → exit, визуально показать blocked Save/Load и возврат контроля |

Дополнительные инженерные риски, не повод расширять текущий audit:

- `TimedNarrativeBeatController.TryStartBeat`, стр. 87–93: повторный start при `IsRunning` вызывает `HidePanel()` и возвращает false, не заканчивая активный beat. Это **VERIFIED / code** небезопасный re-entry path; runtime воспроизведение скрытого активного beat **NOT RUN**. Smoke вызывает повторный start, но после отказа не проверяет сохранение видимости. Перед выбором Timed Beat для showcase нужна отдельная проверка; обычный demo этот режим сам не запускает.
- В `manual_save_1280x720.png` после synthetic delete-hover остаётся красный delete при keyboard-focus пустого слота 2. Runner отправляет `PointerEnter` delete, затем открывает Save/переносит selection без соответствующего `PointerExit` (стр. 647–715). **Наблюдение QA есть, физическая воспроизводимость product defect NOT VERIFIED**. Не переоткрывать закрытый PR #32 и не redesign Save/Load: сначала узкое воспроизведение смешанного ввода, если этот случай станет следующей задачей.
- Source ownership сосредоточен в большом `VNDialogueController`, но данный аудит не показал необходимость выделять managers/DI или удалять hidden foundations. Дубли helper-кода сами по себе не blocker.
- Knowledge drift: `decision_log.md` всё ещё содержит историческое «rollback implementation не разрешена» и четыре пункта Quick Menu; tracker описывает rollback в Game Menu. Current code/tests и поздний baseline README говорят обратное. Drive capability map также сохраняет устаревшую запись root `Назад`, хотя roadmap уже фиксирует его удаление. Reviewer должен синхронизировать актуальную сводку, не возвращать старое поведение.

## 5. Что должны показать следующие UI Targets

Утверждённый [02_Reading_Target_v1.png](https://drive.google.com/file/d/1l4TXeM0vtKkNBgamq_nHZgu0xcgM4ncM/view) действительно получен с Drive и просмотрен, а не восстановлен по памяти. Он задаёт art-first кадр, floating text/scrim, короткое cyan-имя с dash, continuation cue и нижнюю полосу. **Он уже специфицирует базовый speaker**, но не его полную матрицу состояний. Отсутствие artwork/персонажа в отдельных QA fixtures не считается несовпадением UI target.

| Target | Нужен ли сейчас | Конкретная композиция / acceptance |
|---|---|---|
| **04_Choice_Target_v1** | **Да, первым** | State sheet: 2 коротких, 4 смешанной длины, один четырёхстрочный вариант; normal / pointer-hover / keyboard-controller focus / pointer exit. Зафиксировать, нужен ли общий header, ширину/высоту строк и padding, safe area относительно тела реплики/персонажа, dim и visual weight. Ровно один очевидный input owner, полное содержание без ellipsis, выбор доступен без мыши. Не вводить >4 вариантов, новые effects или route meters |
| **05_Speaker_Dialogue_Target_v1** | **Да, только детализация** | Один и тот же кадр: короткое и длинное имя, без имени; 1/2/4 строки текста, 100% и 125%, typing и complete/cue. Показать name/text baseline, cyan dash, интервалы, контраст на светлом/тёмном существующем фоне, no-name collapse и content budget. Runtime сейчас: имя 18–26 auto-size, width 150–500; dialogue masking и ограниченная высота. Длинный текст вне проверенного budget нельзя молча обрезать или считать автоматически поддержанным |
| **06_History_Target_v1** | **Опционально, ниже showcase** | Не новый backlog: speaker+narration+long entry+latest entry, empty state, scroll viewport, scrollbar, close/focus. Показать информационную плотность и спокойную связь с Reading, не timestamp/debug log. Сохранить 100 entries, save-scoped restore и отсутствие произвольного click-to-rewind |
| **07_Save_Load_Target_v1** | **Опционально, последним** | Один компонентный sheet в standalone/Main Menu и embedded Game Menu контекстах: Save Manual-only; Load QS/AS/1..10; empty/valid/invalid slot, preview, дата, global slot number, active family/page отдельно от transient focus, delete/overwrite/load confirm, toast без наложения на strip. Зафиксировать существующую сетку 3×2, 60/6/6 slots. Не переписывать backend или навигацию |

Targets должны содержать annotations размеров/состояний и реальные русские neutral fixtures, а не только красивый единственный кадр. Сначала subjective approval target, затем отдельное разрешение на bounded implementation. **Нет необходимости реализовывать все четыре target перед началом сюжета.**

### Узкий reference check

Проверены официальные web-источники; это **WEB ONLY**, не hands-on и не pixel-level сравнительное ревью внешних UI. Layout/focus внешних игр здесь не объявляются проверенными. Выводы ниже — рекомендации для HIF, не заимствованные contracts.

- [Doki Doki Literature Club Plus!](https://ddlc.plus/) — официальный материал связывает обычные choices и отдельное действие с поэзией. Для HIF: один узнаваемый bounded interaction вместо набора бессвязных mini-games; для 04/05 полезен как reference-кандидат обычного ADV, но его розовый chrome и композиция не копируются.
- [STEINS;GATE ELITE](https://www.spike-chunsoft.com/games/steinsgate-elite/) — издатель описывает animated presentation и последствия choices. Для HIF: сохранить приоритет сцены и ясность текста/решения; анимация сама по себе не закрывает speaker/readability contract. Не переносить систему телефона или ветвления по описанию другого продукта.
- [80 Days — официальный press kit](https://www.inklestudios.com/press/80days/) — действия в городе/пути входят в интерактивное повествование и имеют последствия. Для HIF: `наблюдение → доступное действие → видимый результат → Reading`; не переносить глобальную карту, экономику и масштаб игры.

Иерархия speaker/body и конкретные размеры 04/05 выводятся прежде всего из HIF target, текущих screenshots и `core_reading_experience_benchmark.md`. Web research не обнаружил причины переоткрывать принятые Main Menu / Reading / Game Menu.

## 6. Existing interactive foundations и первый showcase

| Foundation | Реальная зрелость / границы |
|---|---|
| **Interactive Hotspot — основной кандидат** | PlayMode 1/1 + smoke + EditMode: normalized regions, local prerequisites, one-shot, locked→available, completion route, повторный запуск, Game Menu round-trip и восстановление Save/Load eligibility. Fixture laptop→door плюс optional window уже существует, не меняет narrative stats. Есть art-binding gap и непроверенный initial/navigation focus, см. выше |
| Map | PlayMode 1/1 + smoke/EditMode: доступная location ведёт в зарегистрированную сцену, locked не активируется, background назначается. Хороший второй пример перехода, но отдельная карта до реальных локаций даёт меньше пользы, чем одно действие внутри сцены |
| Timed Narrative Beat | Smoke: success/timeout, exactly-once, invalid start, lease conflict, lifecycle cleanup. `BlockingExclusive`, unscaled countdown, minimum authored duration 2s; normal gameplay сам не стартует. Не первый showcase: давление времени/доступность и re-entry risk требуют отдельного решения |
| Chat / Phone | Smoke проверяет text/image/replies, pacing/typing, отдельный ReplyArea, media viewer, блокировку ввода, one-shot return/cleanup, SFX request counts, изоляцию transcript от dialogue backlog. Существующий typed runtime пригоден позже, но phone/menu/input/scroll визуально в этом audit не проверены; не второй generic messenger |
| Character Hub | Обычный modal, **не exclusive special mode**; technical visible/locked profiles, bios/relationship presentation и guards есть. Player-facing launcher намеренно скрыт, история rollback при обычном modal сохраняется. Без настоящих characters/content не делать showcase справочника |
| SpecialModeCoordinator | Smoke проверяет единственного owner, valid policy, stale/wrong lease, destroyed owner и host cleanup. Hotspot/Map используют `InteractiveScene` (Game Menu разрешён, Save/Load запрещены); Chat/Timed — `BlockingExclusive`. Вход/выход через lease очищает rollback, rejected entry buffer сохраняет |

**Рекомендуемый первый проход:** короткая, самостоятельно понятная **TECH DEMO ONLY / NOT CANON** сцена Hotspot: одна площадка, 2–3 действия, одно локальное prerequisite, один optional one-shot, очевидный выход в существующее Reading. Использовать нынешние typed outcomes/local completion и existing launcher. Без инвентаря, квестового журнала, generic flags, новой валюты, minigame framework, manager или канонических имён.

**Объективный blocker?** Для существующей абстрактной технической комнаты — в выполненном mouse-flow не найден. Для обещания *art-backed, keyboard/controller-ready polished showcase* — **readiness gate ещё не закрыт**: unused background binding установлен по коду; focus и визуальные состояния не доказаны. Сначала воспроизвести эти конкретные случаи, затем отдельно разрешённые minimal fixes, если нужны. Не требовать нового framework или всех UI targets.

Acceptance showcase: понятная инструкция/feedback/exit; недоступный объект объясним; mouse и keyboard-navigation завершают loop; menu-return не теряет активный режим; no click-through/double outcome; Save/Load явно недоступны только внутри режима и снова доступны после; rollback не пересекает границу; повторный вход чистый. Fresh 1920×1080 screenshots и regression обязательны, aesthetic approval отдельно.

Special-mode screenshot QA — **NOT RUN**, не PASS: имеющиеся `Hotspot`/`Map` menu launchers вызывают builders и не имеют автоматического capture/exit сценария в `run-graphical-e2e.ps1`; Chat использует отдельный technical launcher, Timed installer может сохранять сцену. В этой сессии нет live Pipeline editor (`unity status`: `STATUS_NO_INSTANCES`, package не установлен) и доступного desktop-control инструмента для интерактивного обхода. Packages/scene installer не включались ради audit. Это ограничивает visual/input completeness, но не отменяет реально выполненные их PlayMode/smoke.

## 7. Protected engineering / SaveData

- `SaveData.CurrentVersion = 3`; v1 принимается только с Manual paths, v2/v3 поддержаны. Миграции in-memory, неизвестная версия отвергается. `lineId` имеет legacy index fallback только при отсутствии ID; `selectedChoiceIndex` относится к исходному порядку choices.
- Поэтому rename/delete scene/line IDs и reorder released choices — compatibility risk; нынешний аудит не меняет ни формат, ни fixtures. Continue = newest valid Manual/Auto/Quick; Quick Load = newest valid Quick; capacities 60/6/6 сохраняются.
- Special-mode промежуточное состояние не сериализуется. Save/Load запрещены не только presentation-кнопками, но и manager guard; Replay имеет свой запрет. Rollback — bounded in-memory (12 checkpoints, backlog guard 65,536 UTF-16 units), очищается на accepted special-mode transition/load, не превращается в persistence-систему.
- Успех PlayMode не заменяет полный пользовательский контентный маршрут, stress на произвольно длинных строках и physical gamepad; именно эти узкие gaps надо добавлять по нужде, а не запускать бесконечный speculative polish.

## 8. Ordered roadmap — 8 bounded passes с stop conditions

Это рекомендации, **не разрешение менять production**. После каждого candidate — review exact diff/CI и синхронизация живой roadmap. Пункты 2/4 условны: без утверждённой дельты implementation пропускается.

1. **04 Choice target/state sheet.** Результат: один одобренный вариант с 2/4/long states и input ownership; существующий Reading не меняется.
2. **Choice-only target delta.** Только утверждённая presentation-дельта/воспроизведённый focus defect; finish: regression 4th source-index/exactly-once + fresh PlayerUi proof. Если дельты нет — NO PRODUCTION CHANGE.
3. **05 Speaker/Dialogue detail sheet.** Уточнить long-name/no-name/text budget/scales/typewriter; сохранить 02 layout и Quick Menu.
4. **Speaker/dialogue-only correction, если нужна.** Проверить граничный content из sheet и исправить только подтверждённые несоответствия; finish: content readable, no clipping, name/narration transitions, targeted tests + screenshots. Не «дополировать всё Reading».
5. **Hotspot readiness pass.** Non-null background и focus entry/menu-return/re-entry repro; при подтверждении — минимальная коррекция существующего controller и покрытие. Finish: один полный mouse/keyboard loop, ясные blocked actions, graphical proof без generic infrastructure.
6. **Один интегрированный Hotspot showcase.** Короткий TECH DEMO вход → действия → исход → возврат в Reading, локальные non-canon состояния, existing assets. Finish: весь loop воспроизводим, Save/rollback boundaries сохранены. После этого можно перейти к реальному story material; 7/8 не обязательны.
7. **06 History target, только при желании согласовать visual family.** State/component sheet и acceptance; если текущая плотность принята и defect не обнаружен — оставить runtime. Не добавлять search/voice replay/click-to-rewind.
8. **07 Save/Load target, только при подтверждённой presentation-потребности.** Зафиксировать standalone/embedded и slot states; сначала отделить synthetic-hover QA anomaly от реального дефекта. Backend и capacities не менять; без доказанной дельты implementation не открывать.

Knowledge-drift correction (сводка Quick Menu/rollback, superseded decisions, Drive capability map) — короткая reviewer docs-задача, не девятый production pass. Улучшить сохранность QA evidence вне очищаемого Temp можно отдельно; это не повод переписывать launchers сейчас.

**Не делать сейчас:** redesign принятых Main Menu/Reading/Game Menu; обязательный полный UI redesign History/Save/Preferences; generic minigame/QTE/DI/managers; новый SaveData/flag registry; flowchart, glossary, route dashboard, canonical Character Hub; artwork/сюжет «для заполнения пробелов»; importer до реального материала; скрытые foundations не удалять как dead code. Будущий сюжет начинать с принятого Markdown-first workflow в `story_content_pipeline_readiness.md`, не миграции narrative engine.

## 9. Индекс просмотренного visual evidence

Все имена ниже относятся к локальному архиву из §2; originals сохранены. Contact sheets — только вспомогательные previews, не новые baselines. Всего generated **121 PNG**; **просмотрены 46**, остальные не объявляются просмотренными. Baselines не обновлялись.

- `PlayerUi/screenshots/`, 28 кадров 1920×1080 (суффикс `_1920x1080.png`): `main_menu_normal_enabled`, `main_menu_hover`, `main_menu_pointer_exit`, `main_menu_keyboard_focus`, `main_menu_preferences`, `main_menu_preferences_resolution_open`, `preferences_dirty`, `preferences_applied`, `gameplay_dialogue_standard`, `gameplay_choice_two`, `gameplay_choice_hover`, `gameplay_quick_save_feedback`, `gameplay_backlog`, `gameplay_auto_active`, `gameplay_skip_active`, `gameplay_hide_ui`, `game_menu_root`, `game_menu_embedded_load`, `game_menu_main_confirmation`, `gameplay_after_game_menu_close`, `main_menu_load`, `gameplay_preferences`, `main_menu_quit_confirmation`, `gameplay_choice_after_rollback`, `gameplay_dialogue_named_speaker`, `gameplay_choice_four_long`, `gameplay_backlog_long_scroll`, `gameplay_dialogue_long_125pct`.
- `PlayerUi/screenshots/`, 3 кадра `_1280x720.png`: `gameplay_choice_four_long`, `gameplay_dialogue_named_speaker`, `gameplay_backlog`.
- `ManualSave/screenshots/`, 6 кадров `_1920x1080.png`: `save_load_strip_hover_transfers_selection`, `manual_save`, `gameplay_load_confirmation`, `gameplay_overwrite_confirmation`, `gameplay_delete_confirmation`, `gameplay_invalid_save_slot`; дополнительно `manual_save_1280x720.png`.
- `SaveBackendV2/screenshots/`, 4 кадра `_1920x1080.png`: `save_load_manual`, `save_load_auto`, `save_load_quick`, `save_load_manual_page_10`.
- `GameMenu/screenshots/`, 4 кадра `_1920x1080.png`: `game_menu_root`, `game_menu_pointer_hover_quit`, `game_menu_pointer_off_rows`, `game_menu_return_focus_after_save_load`.
- Отдельно просмотрен исходный Drive `02_Reading_Target_v1.png`; это не runtime screenshot и в 46 не входит.

**Итог: REVIEW CANDIDATE / NO PRODUCTION CHANGE.** Отчёт рекомендует ограниченные следующие решения; не объявляет новые targets утверждёнными, special-mode visuals проверенными или весь проект DONE.
