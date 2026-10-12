# Target 11 — общие настройки, v1

**APPROVED visual direction / DESIGN TARGET.** Пользователь утвердил направление
2026-10-12 и отдельным заданием разрешил ограниченный presentation-pass.
PNG/SVG остаются дизайн-макетами, не runtime proof. Существующий арт-фон сохраняется;
абстрактная подложка макета не переносится в production.

База и проверенный HEAD: `540930a2b645468c57810d59fa182d5952189cb4`.
Ветка: `design/preferences-target-540930a`. Ветка
`docs/near-term-ui-qa-plan` сохранена на `e0ebaa693b6129b9357133858a6403cd5efb44c6`.

## Макеты

- `11_Preferences_Target_v1.png` — 1920×1080: общий экран в Main Menu,
  категория Экран, исходный keyboard focus на категории, Apply недоступен.
- `11_Preferences_Target_v1_states.png` — 1920×2700: тот же экран из gameplay
  с Sound draft; раскрытые dropdown; Текст и Игра; pointer/keyboard focus;
  clean/dirty/applied; Reset и Back/Esc. Preview gameplay уменьшен до 60 %;
  остальные блоки — детали/аннотации, не несколько одновременно открытых экранов.
- Одноимённые `.svg` — редактируемые векторные исходники обоих PNG.

Направление: спокойная navy-поверхность, тонкие cyan-акценты, лёгкие разделители,
общая ось строк, более читаемые значения. Выбранная категория обозначена тихой
подложкой и линией; hover — локальным усилением поля; keyboard focus — тонким
контуром. Disabled Apply приглушён целиком, включая текст. Без новых badges,
toast, preview текста, tabs, настроек или confirmation.

Подложка намеренно абстрактная и независимая от арта; это не предложение заменить
существующий фон/логотип. Подписи статуса дизайна и пояснения вне окна — только
аннотации макета, не новые runtime-элементы. **TECH DEMO ONLY / NOT CANON**.

## Сверка с repository

Просмотрены все `docs/visual-baselines/preferences*.png`:

| Reference | Сохранённая семантика в target |
| --- | --- |
| `preferences.png` | Горизонтальные Экран / Звук / Текст / Игра, только активная страница, contextual hint, footer; Main Menu navigation скрыта |
| `preferences_dropdown.png` | Три пункта режима: Полный экран / Окно / Без рамки; список поверх следующей строки |
| `preferences_dropdown_1280.png` | Та же структура dropdown без новых действий; отдельный 1280 target не создаётся |
| `preferences_windowed_dropdown.png` | Пример desktop 1920×1080: Окно, 1280x720 / 1600x900; displayed draft соответствует Apply |
| `preferences_sound_dirty.png` | Три sliders, пример 37 / 80 / 80 %, подсветка текущей строки, Apply доступен |
| `preferences_applied_disabled.png` | После Apply экран открыт; при отсутствии новых правок Apply недоступен |

Это сохранённые references, **не свежие runtime screenshots**. Сверены
`SharedPreferencesView`, `PreferencesController`, `PreferencesOptions`,
`PreferencesFormatting`, `GameSettings`, Main Menu/gameplay adapters и тесты.
Старое описание левой колонки в `decision_log.md` не переносится: актуальные
код и `docs/visual-baselines/README.md` явно задают горизонтальные категории.
Визуальный язык продолжает Target 01 v2 / Target 10; их артовые различия не копируются.

Ровно 13 существующих настроек:

| Категория | Controls и неизменные диапазоны |
| --- | --- |
| Экран | Два TMP dropdown: режим экрана; разрешение из runtime options. Полный список: 1280x720, 1600x900, 1920x1080, 2560x1440, 3840x2160; Окно фильтруется по desktop |
| Звук | Общая громкость / Музыка / Эффекты: sliders 0…1, отображение процентов |
| Текст | Скорость текста: slider 20…100, текущие словесные уровни; Задержка авто: 0.5…5.0 сек., хранение 50…500; Размер текста: существующие ‹ / ›, Меньше / Обычный / Крупный / Очень крупный (0.85 / 1 / 1.15 / 1.25); Прозрачность окна: 0…1, проценты |
| Игра | Четыре toggles: Пропуск непрочитанного / Пропуск после выбора / Автосохранение / Быстрое меню; смысл и текущие подписи сохранены |

`Размер текста` намеренно **не заменён на dropdown**. Нет controls для скрытых
`muteAll`, `runInBackground`, `skipBehavior`, `autoForward` или настроек без consumer.

## Защищённое поведение

- Один общий `SharedPreferencesView`, текущие `SettingsManager`/service API/PlayerPrefs keys.
- Правки, включая смену категории, остаются в draft. Apply пишет только изменённые
  поля, обновляет baseline и оставляет окно открытым. Если выбранный Apply стал
  disabled, текущий код переводит фокус на Back; новые правки снова разрешают Apply.
- Back / Esc без открытого списка отбрасывает лишь draft после последнего Apply,
  без confirmation. Повторное открытие читает committed settings.
- Esc при раскрытом TMP dropdown закрывает **только список**, возвращая focus
  полю; то же нажатие не закрывает родительское окно.
- Reset задаёт defaults **всех 13 player-facing настроек**, не только текущей
  категории, только в draft. Скрытые compatibility-поля не сбрасываются;
  доступность Apply зависит от отличия draft от baseline.
- Начальный focus — Экран. Текущая navigation сохраняется: Up/Down между
  категориями; Submit выбирает категорию; Right входит в controls; Up/Down по
  controls к footer; Left/Right на slider меняют значение. Footer сохраняет
  Reset / Back / Apply и существующие переходы. Hover не применяет настройки.
- Вход из Main Menu → возврат в Main Menu. Вход через Game Menu → восстановление
  Game Menu. Прямой вход из gameplay → чтение. Не добавлять второй settings screen
  или видимую навигацию Game Menu под Preferences.

## Bounded brief после approval

Только отдельная согласованная presentation-дельта существующего view:
цвета/контраст, typography значений, лёгкие separators, видимость текущих
selected/highlighted/disabled состояний. SVG/PNG не запекаются в production UI
и не задают обязательный новый размер окна, hit regions или input ownership.
Не менять категории, типы controls, ranges, Settings authority, draft lifecycle,
routes, Save, scenes, prefabs, serialized refs, Packages или ProjectSettings.
Если требуемая подача уже достигнута и дефекта нет — `NO PRODUCTION CHANGE`.

Для будущего pass: targeted `PreferencesInteractionPlayModeTests`,
`MainMenuPreferencesHierarchyPlayModeTests`,
`PreferencesControllerResolutionDraftEditModeTests`,
`PreferencesApplyDisabledPresentationEditModeTests`; relevant Preferences smoke;
свежий `PlayerUi` graphical E2E с самостоятельной проверкой обоих контекстов,
dropdown, focus, dirty/applied и возврата. Baselines менять только по фактическому
изменению UI, не ради публикации макета. Это не запуск implementation.

## Проверка исходного design pass

PNG созданы рендером статических SVG; оба просмотрены coding-agent. Проверены
читаемость, интервалы, отсутствие clipping/overlap, все 13 controls, три режима,
windowed list, цикл размера текста, focus и disabled Apply. Векторные исходники
проверены как XML, PNG декодированы и размеры проверены; Git scope проверен.

**NOT RUN в design pass:** Unity compile/import, NUnit, smoke, graphical E2E, standalone player,
physical input и human aesthetic approval — это design-only, не production pass.
Visual baselines не изменены. Commit/push/PR/CI не выполнялись.

Design pass остановился на согласовании; approval и отдельное разрешение
implementation получены. Результаты implementation/validation фиксируются отдельно ниже.

## Implementation pass 2026-10-12 — локальный кандидат для final review

Минимальный diff `SharedPreferencesView`: navy-палитра; тонкие focus-контуры;
отдельная линия активной категории; более читаемые значения/footer/hints;
неинтерактивные separators. Существующий арт и геометрия controls сохранены.
Presentation раскрытого dropdown привязана к его полю/строке, без смены
фактического EventSystem selection. Types/ranges/settings/draft/routes не менялись.
В два существующих test-файла добавлены regressions; существующий PlayerUi runner
расширен только для отсутствовавшего proof Reset, Preferences hover/focus,
dropdown Cancel и возврата в Game Menu. Новых файлов под `Assets` нет.

Реально запущены существующие HIF launchers, Unity `6000.5.7f1`, с графикой:

- Compile/import preflight — exit 0, без C# compiler errors.
- `PreferencesApplyDisabledPresentationEditModeTests` +
  `PreferencesControllerResolutionDraftEditModeTests` — **12/12 PASS**.
- `PreferencesInteractionPlayModeTests` +
  `MainMenuPreferencesHierarchyPlayModeTests` — **11/11 PASS**.
- `tools/run-unity-tests.ps1 -Mode Smoke` — **30/30 PASS**, включая Preferences
  foundation/parity, Settings truth и project/scene validators.
- `tools/run-graphical-e2e.ps1 -Scenario PlayerUi` — финальный exit 0,
  sentinel `status=PASS`, `playerPrefsRestored=true`, **84 свежих PNG**.
  Это machine PASS, **не visual acceptance**.

Три итерации: 1 — ошибка нового QA lookup после Reset/Back и просвечивание
Outline через плашки; 2 — lookup/плашки исправлены, machine E2E PASS, обнаружена
устаревшая focus-строка раскрытого dropdown; 3 — presentation владельца списка
исправлена с regression, финальные targeted/smoke/E2E повторены.

**Уточнение evidence после reviewer triage 2026-10-12:** прежнее утверждение об
отсутствующих tabs/footer было ошибочным. Reviewer сверил удалённые оригиналы:
26/26 PNG, manifest и XML совпали с локальным proof. Повторно просмотрены все
26 оригиналов в original detail и девять неувеличенных диагностических вырезок:
«Текст»/«Игра», Reset/Back/Apply присутствуют в gameplay 1920×1080/1280×720 и
Main Menu screen/sound 1280×720. Реальный визуальный дефект не подтверждён;
причина расхождения полноэкранных preview не установлена. В логе остаются два
TMP inconsistent-import diagnostics, но причинная связь с визуальным дефектом
**не установлена**. Шрифты/TMP assets не исправлялись, redesign не выполнялся.
Лимит runtime visual/correction итераций остался 3/3; reconciliation не меняет
исполняемые файлы и не требует нового Unity запуска.

Отобраны шесть существующих Preferences baselines и два состояния
gameplay/Cancel; это побайтовые копии проверенных оригиналов текущего E2E.
Dropdown baselines используют settled open/cancelled кадры; selected-кадры во
время существующего Hide fade исключены. Source mapping — в baseline README.

До первого Unity запуска внешний QA-контур переместил реальные LocalLow-данные
в проверенный карантин и сохранил/очистил оба project PlayerPrefs namespace
(Editor/player). Любой ранний startup доступ видел только пустое disposable
состояние; поздний SaveManager override не использовался как доказательство
изоляции. После последнего Unity exit восстановлены **44/44 файла**, точный
набор и SHA-256 совпали; registry names/values/types обоих namespace совпали.
QA sandbox архивирован отдельно, реальные snapshots не публикуются.

Локальное evidence: `QAArtifacts/GraphicalE2E/PlayerUi/`, bounded manifest и
handoff — `QAArtifacts/PreferencesTarget11/manifest.json`. Durable XML/logs —
`QAArtifacts/PreferencesTarget11/`; временные — `Temp/CodexTests/`.
При упаковке предыдущие XML уже отсутствовали в Temp, поэтому только два targeted
прогона повторены без production-правок под новой startup isolation; 12/12 и
11/11 повторно подтверждены, XML сразу архивированы. Данные снова восстановлены
и сверены; это не дополнительная visual/correction итерация.
Просмотренные кадры перечислены в manifest; проверены все
четыре категории, dropdown, pointer/keyboard focus, clean/dirty/applied, Reset,
dropdown dismissal и возврат из Main Menu/gameplay. Заявленный gameplay label
defect отозван после проверки оригинальных пикселей и диагностических вырезок.
Generated TMP/ProjectSettings/serialized QA fixtures восстановлены только по
известным точным путям, не включены в diff; четыре target PNG/SVG SHA-256 сохранены.

Исходный `QAArtifacts/PreferencesTarget11/manifest.json` и remote ZIP сохранены
побайтово как исторический снимок; их visual-blocked вывод заменён отдельным
`QAArtifacts/PreferencesTarget11/reconciliation.json`. Уточнение связывает
source manifest/ZIP и reviewer triage по SHA-256, содержит final file hashes,
mapping baselines, проверенные оригиналы/вырезки и неизменную runtime identity.
Исходный manifest не описывает текущие хеши отредактированных документов.

**NOT RUN в reconciliation:** новый Unity запуск/tests/E2E; executable hashes
не менялись, исходные результаты выше переиспользованы, а не выданы за повторный
прогон. Independent evidence triage выполнен; final review полного обновлённого
кандидата ещё ожидается. Standalone player, physical keyboard/controller QA и
GitHub CI — **NOT RUN**. Commit/push/PR/merge не выполнялись; это не `DONE`.

Reviewer-visible proof: [исходный runtime proof и отдельное reconciliation-уточнение на Drive](https://drive.google.com/drive/folders/1D0HUI6hTuoy4XhqsjnD9bK0uyxL4QzWS).
Старое имя `VISUAL-BLOCKER_gameplay_preferences_1920x1080.png` — историческое,
не подтверждённый дефект. Доступ к исходному proof проверен reviewer; доставка
уточнения не означает final review acceptance.
