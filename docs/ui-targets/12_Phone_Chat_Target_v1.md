# Target 12 — телефон и чат, v1

**APPROVED visual direction / DESIGN ONLY.** Пользователь утвердил визуальное
направление 2026-10-14 ответом «нормально» на прямой вопрос об approval Target 12.
Production-pass **не разрешён**: он требует отдельного явного согласования.
**NO PRODUCTION CHANGE. TECH DEMO ONLY / NOT CANON.**

Четыре PNG/SVG originals сохранены побайтово. Встроенные в них подписи
`NOT APPROVED` / «НА СОГЛАСОВАНИИ» — исторические аннотации исходного design
capture до approval; текущий authority статуса — этот brief и target index.
Смена статуса не требует перегенерации макета или изменения утверждённого вида.

Исходная база: `47724821122219558e1447f5009e48786adcae3b`, проверена через
`git ls-remote origin refs/heads/master` перед созданием ветки
`design/phone-chat-target-v1-4772482` в чистом `D:\How_I_Fall\agent`.
Историческая ветка сохранена. `develop`, reference `master`, `zagent`, archive
и agent-control не менялись. Разрешённое пользователем design-only исключение
до native sync не снимает исторические recovery/review/CI/sync gates.

## Видимый кандидат

- `12_Phone_Chat_Target_v1.svg` / `.png` — основной макет **1920×1080**:
  существующий центрированный `PhoneShell`, header контакта, incoming Text,
  раскрытая Image card и две существующие reply cards.
- `12_Phone_Chat_Target_v1_states.svg` / `.png` — лист деталей **1920×1080**:
  typing/pacing, ожидание ответа, focus/hover presentation, player reply/terminal
  и локальный viewer. Это детали одной сцены, не четыре одновременных окна.
- SVG — редактируемые native-векторные исходники; PNG — детерминированный
  рендер этих исходников, не Unity capture и не runtime evidence.

Одно предпочтительное направление: спокойные navy-поля Target 10/11,
тонкие разделители, локальная cyan-рамка + край активной карточки,
белый читаемый текст. Никакой новой визуальной ОС. Геометрия основного телефона
приближена к текущим `PhoneWidth = 640` и anchors `0.10…0.90`;
не требуется pixel-perfect перенос. Входящие остаются слева, ответ игрока справа.

Основной кадр использует реальные строки `TEST CONTACT`,
`TEST: incoming message`, `TEST: reply A`, `TEST: reply B` из
`test_chat_v1.asset`. Шахматная Image — векторное представление существующего
technical placeholder 64×40, с тем же aspect 8:5; это не предложение заменить
Sprite. Абстрактная подложка — только макет: существующий VN арт-фон сохраняется.
Пояснения за пределами телефона, `scrim`/`aspect-fit` в детали D и подписи
состояний — **аннотации**, не новые runtime controls/captions.

## Runtime truth и presentation proposal

Сверены исходники на exact base, а не недоказанный свежий runtime:

| Источник | Существующий контракт / граница макета |
| --- | --- |
| `Assets/HowIFall/Scripts/ChatPhone/ChatController.cs`: `BuildRuntimeUi`, `BuildHeader`, `BuildTranscriptViewport`, `BuildReplyArea` | Одна phone-root, contact header, masked vertical `ScrollRect`, отдельная reply area, массив ровно из двух `Button`; не новый экран/manager |
| `ChatController`: `BeginEntryPacing`, `RevealEntry`, `RefreshTranscript` | `IncomingTyping` показывает dots до reveal; `Delay` не показывает typing. На reveal transcript прокручивается вниз. Новый scrollbar или закрепление ручной позиции не предлагаются |
| `ChatController`: `ShowChoices`, `TryChoose` | Реальные states: `Active` с `Choice`, `WaitingForEntryReveal`, `ResolvingTerminalChoice`. Условно недоступные варианты **скрываются**, не рисуются disabled. Если скрыты оба — текущий fallback/completion |
| `ChatController`: `BeginTerminalCompletion`, `AdvanceTerminalPresentation` | После terminal reply две прежние карточки отключены, player bubble справа; существующая задержка 0,45 с, затем cleanup/return. Non-terminal идёт по текущему `nextEntryId` |
| `ChatController`: `TryOpenMediaViewer`, `TryCloseMediaViewerOnEscape`, viewer builder | Только уже раскрытая Image с непустым Sprite; клик по карточке. Aspect-fit, scrim и X; underlying input заблокирован. Локальные pacing/terminal timers paused, не `Time.timeScale` |
| `Assets/HowIFall/Data/Chats/test_chat_v1.asset` | Reachable TEST-последовательность: incoming typing 1,5 с → Text → Delay 0,6 с → Image → две terminal choices. Лист C показывает `TEST: reply A` после выбора, не выдуманную более длинную переписку |
| `docs/chat_phone_policy.md`, `docs/chat_media_viewer_policy.md` | BlockingExclusive, transient-only transcript, deterministic exactly-once routing и локальный viewer сохраняются |

**Предлагается только presentation:** цвета/контуры/интервалы/типографика,
визуальная рамка уже выбранной/hovered reply card; равная подача incoming/player
bubbles и Image; русские pacing-подписи `ВЫБЕРИТЕ ОТВЕТ` / `ОТПРАВКА ОТВЕТА…`
вместо текущих `Choose a reply` / `Sending reply…`. Последняя — подпись
terminal pacing, **не** сетевой статус отправки/доставки.
Существующий декоративный зелёный header-dot в макете опущен; это предложение
подавить неоднозначную декорацию, не изменить статус контакта/связи.
Dots остаются dots: без имени внутри typing bubble и без нового таймера-счётчика.

Рамка в основном кадре и детали B — пример **одного** текущего владельца focus,
не default selection/auto-select. Keyboard focus показан как предлагаемый вид
`Button` selected-state **если** selection уже получен существующим путём.
`ChatController` не задаёт собственного keyboard/gamepad navigation mapping
или default selection; макет не обещает новый input, hotkeys или поддержку
controller. Перед будущим pass проверить реальные reachable input states;
не добавлять mapping/selection mechanics под видом visual polish.

## Защищённые contracts

- Chat — одна authored scene поверх VN, не Phone OS/список контактов/приложения.
  Нет unread/delivery/read badges, звонков, chat cancel/back, persistent history,
  нового канона, произвольных вложений или новых managers.
- Один `SpecialModeCoordinator.BlockingExclusive` lease. Dialogue advance,
  Auto/Skip, Save/Quick/Auto Save, Load/Quick Load, History, Settings, Quick Menu
  и Main Menu остаются заблокированы; backend Save fail-closed тоже защищён.
- `Escape` вне viewer — **no-op**, не отмена Chat. В viewer `X` / `Escape` /
  scrim закрывают **только viewer**; image click не закрывает. После закрытия
  тот же transcript/reply/`GameState`, без повторных incoming/open cues.
- Typed conditions/effects/targets, два existing choices, fallback, local timers
  и audio semantics неизменны. Terminal completion idempotent, lease release
  **до** ровно одного route в заданную `returnScene`.
- Transcript/viewer не попадают в backlog, `DialogueReadHistory`, Replay/profile
  или Save. `SaveData` v3 неизменен; Replay не разрешает старт Chat.
- Scenes, authored assets, API, serialized refs, Packages/ProjectSettings,
  Save format и accepted visual baselines этим pass не меняются.

## Bounded brief после отдельного approval

**Входной gate:** visual direction утверждена пользователем 2026-10-14;
independent review/CI исходного design head зафиксированы ниже. Текстовый
approval follow-up требует собственного exact-head review/CI. Production-pass
по-прежнему требует отдельного approval; эта запись не выдаёт такое разрешение
и не открывает automatic dispatch.

1. На свежей согласованной базе сравнить существующий `ChatController`
   presentation с утверждённым target. Если полезной дельты/defect нет —
   **NO PRODUCTION CHANGE**, без speculative polish.
2. При разрешённой дельте менять только существующие presentation builders:
   colors, TMP readability, padding/spacing, reply `Button` visual states,
   безопасные presentation-подписи. Сохранить single root, masks/layout groups,
   existing API/mechanics/input ownership. Не переписывать lifecycle,
   transcript-scroll, timers, media, audio, choices, scene data или routing.
3. Переиспользовать existing `ChatPhoneSmokeTests` (включая runtime bootstrap,
   pacing, media и audio assertions); выбрать targeted checks по реальному diff.
   Для runtime proof — существующий `ChatPhoneTechnicalDemoLauncher` через
   `How I Fall/Chat Phone/Run Technical Demo (Play Mode)`, не новый framework.
   Dedicated Chat graphical-сценария в `run-graphical-e2e.ps1` на этой базе нет;
   не объявлять его существующим. Проверить incoming/Delay/Choice/terminal/viewer/return,
   существующие save/special-mode guards и реально достижимый input.
4. Получить свежие graphical originals в 1920×1080, без `-nographics`;
   inspect clipping/overlap/readability/focus/visibility/errors. Viewer:
   aspect-fit, input blocker, X/Escape/scrim, same-state resume без audio replay.
   Baselines обновлять только после успешного реального visual pass.
5. Scoped review candidate → independent review → exact-head CI.
   Не называть runtime accepted/DONE по статическому target.

## Evidence этого design-pass

SVG/XML parse, полный PNG decode/size и визуальная инспекция обоих originals
1920×1080 выполнены. Clipping/overlap/нечитаемых labels в финальных макетах
не обнаружено; это **static design inspection**, не graphical E2E PASS.
Проверены text bounds внутри canvas и статический contrast: incoming 10,90:1,
reply 10,35:1, annotations 8,04:1, disabled reply 4,64:1. Это не доказательство
runtime layout или новых accessibility/input capabilities.
Одна objective correction: typing без добавочного имени; viewer scrim
полупрозрачен и оставляет underlying context. Новых вариантов направления нет.

Рендер: `sharp` 0.35.5 / librsvg 2.63.2, density 72, PNG compression 9,
Segoe UI. SVG self-contained, без удалённых fonts/assets. Технические helpers
и локальный manifest находятся вне repository в
`D:\Codex\visualizations\2026\10\14\01a13976-7261-77d2-a409-9ef31b054ad2\phone-chat-design`.
Remote originals — два PNG в task branch/PR; existing CI `visual-review`
пакует реально изменённые `docs/ui-targets/*.png` с base/head manifest.
SHA-256 originals/source identity переданы reviewer'у с публикацией.
Независимый **CLEAN** review и CI **#280 success** относятся только к exact
design head `7d5c3377f16d3b7157fb37dbc02031199068476e` в PR #75.
Пользовательское visual-direction approval получено 2026-10-14 после этого
ревью. Новый approval-status follow-up — **REVIEW CANDIDATE**, его собственные
exact-head review/CI ещё pending; предыдущий CLEAN/GREEN на него не переносится.

| Original | SHA-256 |
| --- | --- |
| `12_Phone_Chat_Target_v1.svg` | `b8b3aa0766b1cd20c685850eeb6a784090e24878c94754ae78065b069c23d21d` |
| `12_Phone_Chat_Target_v1.png` | `7df6ed80f60e0112dabd0df6dde509aac57061eb83591a30d983a32513bfae1e` |
| `12_Phone_Chat_Target_v1_states.svg` | `5eacfbe33cc8af721b82f6e43426812bfa311f7e6297f0cb41bcee40477c354e` |
| `12_Phone_Chat_Target_v1_states.png` | `701f74fc8c23a1d8d3c1cab4213a6934d482ec932bc35f0619bd8c97ac634160` |

SVG hashes относятся к canonical repository blobs с LF; Windows CRLF-копии
семантически эквивалентны, но имеют другой bytehash. PNG hashes побайтовые.

**NOT RUN:** Unity compile/import, EditMode/PlayMode/smoke, graphical E2E,
hands-on keyboard/controller — для static design-only diff они не запускались.
Render/inspection/tests/E2E в текстовом approval follow-up не повторялись;
исторические static проверки выше сохранены. Человеческое approval направления
получено; это не runtime QA approval. Drive mirror — NOT SYNCED. Historical/native sync
barriers сохранены; production implementation и merge не разрешены.
