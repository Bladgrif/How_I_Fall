# How I Fall — технический план Unity-основы

> **Статус:** живой технический ориентир верхнего уровня. Детальные текущие решения определяются `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md` и relevant `.agents/skills/*`.

## Текущая фаза

**Polished Functional Demo First.**

Приоритет: стабильный функционал, player-facing UX, аккуратный UI, regression coverage, automated/runtime QA и минимальные task-scoped изменения.

Сюжет, routes, canonical flags, финальный art и final visual identity отложены до явного возвращения к актуальному story material.

## Roadmap entrypoint (SourceRepoPlan)

Этот документ — единственный основной roadmap/approved-task entrypoint автономного контура. Документы Drive `02`/`03` остаются research/history и необязательным зеркалом; при конфликте источник истины — репозиторий. Отсутствие Drive write не блокирует `DONE` после остальных обязательных gates, но stale-зеркало помечается явно. Новые product features/canon без user approval здесь не добавляются; существующие принятые решения и защищённые UI/Save контракты сохраняются.

## Текущий infrastructure candidate: утренние bounded пакеты

`approved-morning-batches`, exact base `835e342ebf26c49c372db162a6159985d67c9625`:
расширение единственного existing runtime, 1–15 предложенных bounded task
определений и native exact-digest approval в `queue.json`. Это не утверждение
новых features/canon и не promotion трёх существующих
`LIST_APPROVED_NOT_DISPATCHED` game items. History сохраняется.

Candidate включает serial native selection с fresh base после DONE/native sync,
fresh bounded Supervisor context, read-only dashboard и temporary regression
fixtures. До host publication, fresh independent review и CI — REVIEW CANDIDATE,
не DONE. Полный live morning packet, local deployment и GLM runtime QA —
NOT VERIFIED; закрытые native PlayMode/graphical profiles не ослаблены.
Workflow/source rationale/stop semantics: `tools/agent-control/README.md`.

## Базовая архитектура

Сохраняется существующая простая структура:

- `DialogueSceneData` / `DialogueSceneRegistry` — исполняемый dialogue content;
- `VNDialogueController` — VN execution;
- `GameState` — текущий campaign state;
- `SaveManager` / `SaveData` v3 — persistence;
- `SettingsManager` / `GameSettings` — player preferences;
- `AudioManager` — music/SFX/ambience;
- scene-local UI/controllers — player-facing shell;
- Editor validators/tests/E2E — QA.

Не добавлять service locator, DI framework, generic UI framework, manager-per-feature или универсальную narrative VM без доказанной задачи.

## Текущий функциональный фундамент

Уже существуют и не должны повторно планироваться как отсутствующие:

- dialogue/typewriter и принятый Choice UI target v1 (до 4 visible choices);
- typed conditional choices;
- Manual 60 / Auto 6 / Quick 6 saves;
- Continue newest-valid;
- Auto, seen-aware Skip, History/backlog restore;
- bounded in-memory Rollback по stable-line/pre-choice checkpoints с state/backlog/presentation restore;
- Main Menu, Game Menu, Shared Preferences;
- compact Quick Menu: `Назад / История / Пропуск / Авто / Быстр. сохр.`;
- ordinary root Game Menu: Сохранить / Загрузить / Настройки / Главное меню / Выйти + отдельное Вернуться в игру; без root Назад/Откат;
- unified input/help;
- notifications/confirmations;
- relationship deltas/classification; cue-глиф намеренно подавлен в demo-shell (`RelationshipCuePresentationEnabled = false`), а не ожидает обязательного polish;
- Gallery/Replay technical foundation;
- Character Hub technical foundation;
- Chat/Phone technical foundation;
- Interactive Hotspot, Map Locations и Timed Narrative Beat foundations.

## Save compatibility

`SaveData` v3 — защищённый контракт. Не менять schema, slot capacities, migration или ranking без отдельной причины, migration plan и regression coverage.

- v1 принимается только из Manual paths, v2 — Manual/Auto/Quick; миграции in-memory не переписывают старый JSON, неизвестные версии отвергаются;
- `sceneId`/`lineId` должны разрешаться через registry; legacy `lineIndex` fallback допускается только при пустом `lineId`, не при неизвестном непустом ID;
- `selectedChoiceIndex` — исходный индекс choices, не visible slot; rename/delete released IDs и reorder released choices — compatibility risk;
- Save-scoped backlog (до 100 entries) заменяет текущую History при Load, legacy v1/v2 используют пустой fallback;
- rollback buffer и transient special-mode state не сериализуются. Save сохраняет текущий state/backlog, но не buffer; accepted Load/session/Replay/special-mode barriers очищают rollback. Save/Load guards special modes и Replay сохраняются.

Evidence exact-base сверки `40c8dd8881039f472fa578f1a76c02fe8dffb073`: `SaveData`, `SaveManager.ReadSlot`/`TryValidateChoiceState`, `ManualSaveSystemV1SmokeTests`, `BacklogRestorationSmokeTests`, `RollbackBackendPlayModeTests`; общий source-index — `docs/eternum_feature_tracker.md`. Исходники проверены, Unity tests/runtime **NOT RUN** в этом docs-only проходе.

## UI и QA

Для player-facing изменений:

1. минимальный diff;
2. targeted regression;
3. relevant smoke;
4. graphical E2E в реальном runtime;
5. inspection screenshots;
6. небольшой curated `docs/visual-baselines/` set;
7. review-candidate push;
8. GitHub diff/CI review;
9. repository roadmap в candidate diff; Drive capability/roadmap — необязательное зеркало с честным статусом записи.

Стандартная QA resolution — 1920×1080, если задача явно не требует responsive coverage.

## Story pipeline — позже

Когда story work явно возобновится:

- canonical source начинается в `docs/story/` / Markdown;
- сначала story skeleton, scene/route structure и stable IDs;
- Unity assets генерируются/собираются только после утверждения материала;
- importer строится только если реальный объём делает manual conversion повторяющейся проблемой;
- не проектировать generic flags/world database вокруг гипотетического канона.

## Ближайший продуктовый принцип

Перед добавлением новой механики сначала проверить, какую реальную player problem она решает. Для текущей фазы предпочтительнее polish/integration существующих систем, чем расширение feature count.

Rollback/Rewind реализован: 12 in-memory checkpoints, guard 65,536 UTF-16 code units; route — `Назад` в Quick Menu и колесо вниз, не Game Menu/History. Исторический feasibility contract сохранён с актуальной пометкой. Обычный функциональный demo-shell собран; full-demo audit 2026-10-02 и release-candidate audit 2026-10-03 не означают полного release-readiness или runtime PASS текущей базы. Flowchart/chapter/glossary/endings остаются отложенными до настоящего story graph.

## Утверждённая очередь после настройки автономного контура

- Infrastructure: один `HIF Supervisor`, одна очередь, quota fallback, независимое review, exact-head CI/merge и нативная ff-only синхронизация чистого `master`. PR #66 принят; текущий finalization pass проверяется отдельно. Оперативные head/PR/CI/статусы принадлежат `agent-control/queue.json` и `state.json`, а не дублирующему дневнику.
- Утверждённая техническая dependency `glm-native-runtime-qa` (повторное подтверждение пользователя, base `df09ed3d3c017cd751698c0b83e3f1b01e680522`): расширить существующий native worker фиксированными runtime/UI checks, свежим XML/graphical proof, ограниченными correction retries и Sol High ownership объективной инспекции GLM screenshots. Writer acceptance — безопасные fixtures/AST/diff, без Unity в protected `develop` и без реальных user saves. Correction закрывает `PlayMode`/graphical selections до доказанной startup save isolation и bounded affected-state proof; late override после `SaveManager.Awake` не считается изоляцией. Настоящая свежая GLM production-задача остаётся staged `NOT VERIFIED`; продуктовую задачу ради проверки инфраструктуры не придумывать. Independent exact-head review → full remote CI → outer merge/native sync остаются обязательными.
- Новых универсальных production/product-задач сейчас не утверждено. Принятые Main Menu v2 и Hotspot marker polish не переоткрывать без reproduced defect.
- После принятия infrastructure pass: один bounded анализ repository/подтверждённых дефектов, до трёх product-предложений с acceptance, затем `WAIT_USER`. Новые features, визуальное направление и story canon выполняются только после согласования пользователя.

## Ближайший ручной план UI и demo QA

Порядок обсуждён с пользователем 2026-10-12. Это список для утреннего выбора
ограниченных задач, **не** approval на автоматический dispatch или изменение
production UI. За один проход брать одну задачу; следующий шаг зависит от
результата предыдущего. `develop` остаётся защищённым пользовательским checkout.

1. **Save/Load — макет и brief, design-only.** Показать Main Menu и встроенный
   Game Menu контексты, пустой/валидный/повреждённый слот, preview, фокус и
   подтверждения. Сохранить сетку 3×2, Manual 60 / Auto 6 / Quick 6 и текущий
   backend. Результат — target со state sheet и ограниченным implementation brief;
   субъективное направление утверждает пользователь.
2. **Save/Load — только утверждённая presentation-дельта.** После approval
   target изменить лишь согласованный UI; SaveData/формат, capacities,
   navigation и safety contracts не менять. Acceptance: targeted regression,
   релевантный smoke, свежий `ManualSave` graphical E2E и просмотр screenshots.
   Если требуемой дельты или воспроизводимого дефекта нет — `NO PRODUCTION CHANGE`.
3. **Preferences — макет и brief, design-only.** Один `SharedPreferencesView` для
   Main Menu и gameplay; показать иерархию, читаемость, pointer/keyboard focus,
   состояния реальных настроек и возврат. Не добавлять controls без consumer.
   Результат — target со state sheet и ограниченным implementation brief;
   субъективное направление утверждает пользователь.
   **2026-10-12:** Target 11 утверждён пользователем; два PNG и два SVG сохранены
   побайтово, approval зафиксирован в brief/индексе. Это не production QA approval.
4. **Preferences — только утверждённая presentation-дельта.** Сохранить один
   общий экран и текущую Settings authority. Acceptance: targeted regression,
   релевантный smoke, свежий `PlayerUi` graphical E2E и просмотр screenshots.
   Без нужной дельты — `NO PRODUCTION CHANGE`.
   **2026-10-12: локальный кандидат для final review.** Presentation-дельта реализована
   локально; compile/import, targeted EditMode 12/12, PlayMode 11/11, smoke 30/30
   и machine PlayerUi E2E успешны. Reviewer сверил remote originals: отсутствие
   tabs/footer не подтвердилось; ошибочный блокер отозван после проверки оригиналов
   и вырезок. TMP diagnostics сохранены без установленной связи с визуальным дефектом.
   Обновлены 8 curated Preferences baselines из того же proof; executable hashes
   неизменны, Unity повторно не запускался. LocalLow/settings ранее восстановлены
   и сверены. Evidence triage выполнен; final review/CI/commit/push/merge ещё
   не завершены. Следующий шаг — final review reconciliation, не redesign.
   **2026-10-13:** независимый final review reconciliation — без блокирующих
   замечаний; 20 final files и 8 baseline copies сверены с remote proof.
   Код/tests/runner совпадают с проверенным runtime pass. Интеграция ещё требует
   exact-head `CI Gate`, merge и native master sync; затем — пункт 5, Windows QA.
5. **Windows demo player journey — QA после визуальных проходов.** Проверить
   реальный собранный player: запуск → Main Menu → Reading → Preferences и
   Save/Load → возврат. До теста изолировать/сохранить реальные LocalLow данные,
   после — восстановить и проверить их; приложить свежие runtime screenshots,
   logs и список только воспроизведённых дефектов. QA не равно автоматическому
   объявлению release-ready.

Статус 2026-10-12: пункты 1–2 локально выполнены в Target 10 / PR #71:
направление утверждено, presentation-дельта прошла целевые EditMode 8/8,
PlayMode 15/15, smoke 30/30 и ManualSave graphical E2E. Это не полный visual
PASS Game Menu и не release-readiness. Интеграционный статус определяется
exact-head CI и merge PR #71; пункт 3 выбирать только после этих gates.

Новые мини-игры, story canon и дополнительные product features в этот список не
входят. После него выбрать не более одной следующей функции по конкретной player
problem и отдельному решению пользователя; существующие technical foundations
не планировать заново как отсутствующие.

**Сверка интеграции 2026-10-13:** PR #71, #72 и #73 смержены; exact-head
Unity CI #274, #275 и #276 успешны. Remote `master` —
`63ba4cc7b2299ba550a66648843c0e5a2e05f564`; локальный reference остаётся на
`0a4dad45a73177ac74d9740647ec890e5313c935`, native sync не выполнен.
PR #73 добавил строгую merge/squash recovery-chain и отдельное exact-digest
operator approval. Live installation и recovery **NOT VERIFIED**: для ручных
PR отсутствуют пригодные canonical native review archives. Пользователь разрешил
отдельный bounded historical-review host pass в existing reviewer/control:
настоящие новые Sol High sessions с exact original identities, без подмены
writer history, queue/state и без ослабления operator/scheduler gates.
Локальный код этого прохода требует независимого review и exact-head CI;
установка helpers и реальные historical sessions — отдельный следующий gate.

**Пункт 5 — bounded Windows QA выполнен 2026-10-13, NO PRODUCTION CHANGE.**
Пользователь явно разрешил QA-before-native-sync исключение: чистый writer на
exact remote source `63ba4cc7…`, без изменения reference checkout. Реально
собран и проверен Windows player: Main Menu → Reading → Preferences и
Save/Load → возврат. Проверены draft discard/apply/persistence и load после
подтверждённого продвижения Reading. Reviewer просмотрел 9 оригинальных кадров;
воспроизводимых production defects не обнаружено. После QA восстановлены и
сверены 44/44 LocalLow файла и оба PlayerPrefs namespace. Remote evidence:
[bounded Windows player journey proof](https://drive.google.com/file/d/1p_b-3nAQVKzg4Of9WIkDQT8fn1tzrKCf/view).
Это не human physical keyboard/controller QA, full feature sweep или release-ready.

Следующий ограниченный шаг — authentic historical reviews и operator-approved
native reference recovery, затем per-PR repository roadmap reconciliation.
QA-before-sync не заменяет recovery, не снимает sync barrier и не разрешает
новые product features. До этих gates интеграция не `DONE`.
