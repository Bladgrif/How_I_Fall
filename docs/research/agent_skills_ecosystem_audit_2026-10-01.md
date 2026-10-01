# Аудит agent/skills/tooling экосистемы HIF — 2026-10-01

## Контекст

Вопрос: нужен ли How I Fall более сильный agent/skill/tooling слой. Проверялись текущие repository-возможности и актуальная внешняя экосистема (официальные Unity skills, Scenario Unity expert skills). Гипотеза «внешние репозитории содержат skills, значит HIF нужны такие же» проверялась против реальных gaps. Отдельный research-документ оправдан: результат определяет agent-инфраструктуру проекта и к нему нужно будет возвращаться.

Ограничения аудита: production Unity не менялся, внешние сервисы не подключались, платные API не использовались. Исследование выполнено J-Code: coordinator/writer + два read-only investigator.

## Источники

- Repository: `AGENTS.md`, `docs/product/{agent_orchestration,review_workflow,decision_log,demo_goal,ui_principles}.md`, `.agents/skills/{hif-polish-loop,hif-visual-qa}`, `tools/run-graphical-e2e.ps1`, `tools/run-unity-tests.ps1`, `.github/workflows/unity-ci.yml`, листинги `Assets/HowIFall/Tests/{EditMode,PlayMode}` и `Assets/HowIFall/Editor/*QaLauncher.cs`.
- [Unity-Technologies/skills](https://github.com/Unity-Technologies/skills) — 32 официальных skill'а, лицензия Unity Companion License. Проверены README и содержимое skill'ов: `unity-cli`, `ui`, `ui-ugui`, `unity-package-management`, `new-unity-project`. Skill'ы — agent-agnostic prompt-файлы, установка через `npx skills add`; работают с Codex и 50+ агентами. `unity-cli` управляет открытым Editor через companion-пакет (`unity status/command/eval`, `editor_play`, install/test/build/vcs), включая PlayMode-верификацию со скриншотом, readback консоли и freeze detection. Официальный plugin-путь для Codex — [Unity-Technologies/unity-agent-plugin](https://github.com/Unity-Technologies/unity-agent-plugin): `codex plugin marketplace add Unity-Technologies/unity-agent-plugin` + `codex plugin add unity@unity-agent-plugin` (v0.1.8-beta), Unity 6+, требует локального executor'а, управляет Unity через CLI + Pipeline package, не MCP.
- [scenario-labs/skills](https://github.com/scenario-labs/skills) — MIT. Семейство scenario-unity-expert (14 skill'ов, Python `ut_*` toolkit + C# AgentKit, AGENT_RESULT envelopes, evidence gates, blind-graded 63%→97%) — но с жёсткими предположениями: macOS Apple Silicon (пути `/Applications`, Metal), Unity 6000.3.21f1, URP 17.3. Генеративные skill'ы (`consistency`, `identity-library`, `game-assets`, `image`, `asset-analysis`, `quality-gate`, `refine-loop`, `audio`) управляют Scenario MCP (`mcp.scenario.com`) через платные кредиты и OAuth/API key; `quality-gate` — Enterprise add-on.

## Карта возможностей HIF

Классификация 17 активностей (детальная evidence — в отчёте аудита от 2026-10-01):

| Активность | Классификация |
|---|---|
| Bounded Unity/C# implementation | GOOD AS-IS |
| uGUI implementation | GOOD AS-IS |
| Focus/navigation debugging | IMPROVE EXISTING |
| Runtime lifecycle/state debugging | IMPROVE EXISTING |
| Save/Load work | GOOD AS-IS |
| Compile/import automation | GOOD AS-IS |
| EditMode/PlayMode tests | GOOD AS-IS |
| Smoke tests | GOOD AS-IS |
| Graphical E2E/screenshots | GOOD AS-IS |
| CI investigation | GOOD AS-IS |
| Code review | GOOD AS-IS |
| Visual review | GOOD AS-IS |
| Asset import/replacement | GOOD AS-IS |
| Будущий art consistency pipeline | DEFER |
| Будущий audio pipeline | DEFER |
| Performance investigation | NEW SKILL JUSTIFIED (при появлении perf-проходов) |
| Package management | GOOD AS-IS |

Реальные gaps:
- **G1 (системный):** инфраструктура отладки фокуса/навигации и runtime-состояния сильна (4 QA launcher'а, 16 PlayMode и 10 EditMode suites, 4 graphical E2E сценария), но нигде не задокументирована маршрутизация «симптом → какой launcher/тест/E2E запускать». Агент тратит context на переоткрытие карты инструментов.
- **G2 (условный):** performance/profiling workflow отсутствует полностью; становится актуальным только при perf-фазе.
- **G3 (minor):** список E2E-сценариев захардкожен в `ValidateSet` PowerShell-скрипта; процедура добавления новой поверхности не описана.

Противоречия в документации: не обнаружено blocking-расхождений; классификация CI в `review_workflow.md` точно совпадает с `unity-ci.yml`, baseline-правила согласованы между skill'ами.

## Официальные Unity skills: оценка

**Что реально делают:** agent-agnostic prompt-файлы + (для `unity-cli`) Unity CLI, управляющий открытым Editor: status/command/eval, PlayMode с readback консоли и скриншотами, install/test/build/vcs. `ui`/`ui-ugui` — лучшие практики Canvas/RectTransform/LayoutGroup/TMP и interaction-readiness checks (EventSystem, GraphicRaycaster, raycast targets).

**Совместимость с HIF:** Windows поддерживается; Codex-плагин официальный; внешние аккаунты не нужны сверх лицензии Unity. Works with Unity 6+ — версию Unity HIF проверять перед установкой.

**Дублирование с HIF:** частичное и взаимодополняющее. uGUI-гайды и interaction-readiness не заменяют `ui_principles.md` (другой уровень — общие практики vs проектные контракты); Editor-driving и CLI test/build не заменяют существующие PowerShell-раннеры и PlayMode E2E, но предлагают channel для live-Editor проверки.

**Риск:** skill'ы по умолчанию толкают live-правки сцен/префабов — против правил HIF «не менять сцены/prefab без необходимости». Требуется явное ограничение: skill — инструмент чтения/запуска, не вход для production-правок. Лицензия Unity Companion License: использование внутри Unity-проекта допустимо, вендорить текст skill'ов в repository docs нельзя.

**Adoption mode: OPTIONAL PLUGIN для Codex** (лёгкая установка/откат), standalone-репозиторий — reference only.

## Scenario Unity expert: оценка

Технически глубокое семейство (evidence gates, структурированные verdict'ы, blind-graded improvement), но перенос невозможен напрямую: macOS Apple Silicon only, Unity 6000.3.21f1 + URP 17.3, у HIF Windows и другое окружение. Лучший режим — **перенять паттерны, не код**:

- **AGENT_RESULT envelope:** job возвращает структурированный verdict `{ok, errors[], metrics, evidence}` вместо parse логов. HIF уже делает это sentinel-файлами в графическом E2E; паттерн подтверждён внешне.
- **Numbers before pixels:** доказательство = числа (тайминги, счётчики) + обязательный просмотр, а не «скриншот существует». Совпадает с текущим правилом `unity_agent_guide.md` «наличие скриншота не является доказательством».
- **«Не верить exit 0»:** compile error в любой сборке абортит батч; HIF уже ловит через compile preflight в графическом E2E.
- **Anti-trap таблицы** (Safe Mode, `-nographics` скриншоты, `-quit` с `-runTests`): сверены, недостающего нет — HIF-правила уже покрывают эти ловушки.

**Adoption mode: REFERENCE ONLY / адаптация концепций; инсталляция отклонена** (платформа не совпадает).

## Scenario генеративные skill'ы (арт/аудио): будущая релевантность

Все требуют Scenario MCP с платными кредитами и OAuth/API key. Для будущей арт-фазы VN потенциально сильны `consistency` (контроль идентичности персонажа между кадрами), `identity-library` (библиотека персонажей с гейтами), `game-assets` (спрайты с alpha), `refine-loop` (rubric-first итерация с round cap — переносимый паттерн), `asset-analysis` (batch-ревью против брифа). `image`/`audio` — стандартные обёртки; `quality-gate` — Enterprise.

**Adoption mode: DEFER** до появления финальной арт-фазы и бюджета/аккаунта Scenario. Согласовано с `decision_log.md` от 2026-08-27: финальный арт подключается позже.

## Codex vs Z-Code vs J-Code

- **Codex:** единственная среда с официальным plugin-путём для Unity skills (`codex plugin add unity@unity-agent-plugin`). Опциональное усиление, не требование.
- **Z-Code (default):** ничего из внешнего не требуется; существующие skills + документация покрывают default-работу. Ручная установка skill-файлов в `~/.agents/skills` возможна, но сейчас не даёт того, чего нет в repository.
- **J-Code:** многоагентная координация уже покрыта `agent_orchestration.md` (§4, §11) и сама по себе не требует новых skill'ов. J-Code — механизм для периодических audit/investigation проходов, как этот; не нуждается в постоянных agent-определениях.

Постоянные agent-определения (persistent roles) отклонены: в `AGENTS.md`/`agent_orchestration.md` уже есть ролевая модель Implementer/Reviewer/User, а swarm-использование описано как исключение. Роль должна задаваться brief'ом задачи, не репозиторием.

## Рассмотренные варианты архитектуры

1. **Ничего не менять** — отклонено: G1 реален, agents waste context на переоткрытие debug-карты.
2. **Вендорить Unity skills в репозиторий** — отклонено: дублирование официального контента, лицензионный риск (Unity Companion License не позволяет свободное копирование текста), prompt bloat.
3. **Постоянные J-Code agent-роли (implementation worker / reviewer / QA investigator)** — отклонено: дублирует существующую ролевую модель в source of truth; J-Code остаётся reserved для genuinely parallel work.
4. **Отдельный skill для каждой внешней концепции** — отклонено: создало бы 10+ мелких skill'ов против принципа минимальности.
5. **Принято: минимальная архитектура** — один узкий repository-skill для G1, документация опционального Unity plugin для Codex, паттерны Scenario зафиксированы как уже-принятые-в-духе (sentinel verdict, numbers-before-pixels), генеративный Scenario — defer. Agent-архитектура не меняется: default остаётся single-agent Z-Code; J-Code — для genuinely parallel work.

## Рекомендация для HIF

1. Принять `hif-debug-routing` — узкий repository-skill, маршрутизирующий debug-симптомы (фокус/навигация, runtime state, Save/Load, визуальные дефекты) к существующим launcher'ам/тестам/E2E. Только маршрутизация, без новых фреймворков.
2. Задокументировать официальный `unity-agent-plugin` как опциональное Codex-усиление (Editor-driving, CLI test/build) с явными ограничениями: не вход для production-правок, сцены/prefab по-прежнему protected, текст skill'ов не вендорить.
3. Зафиксировать Scenario как deferred asset-production tooling (список кандидатов для арт-фазы) и как внешний источник подтверждения уже принятых HIF паттернов.
4. Performance и генеративные пайплайны — отложены до соответствующих фаз; при старте perf-фазы вернуться к вопросу о perf-skill.
5. Постоянные agent-определения не создавать; роли задаются brief'ом по `agent_orchestration.md`.

## Решение

- `APPROVED` в объёме рекомендаций 1–3 (ограниченная documentation/skill интеграция);
- `DEFERRED`: performance skill, Scenario генеративная интеграция, постоянные agent-роли;
- `REJECTED`: вендоринг внешних skill'ов, отдельная архитектура слоёв, подключение Scenario MCP сейчас.

## Применение

Изменения этого прохода: новый skill `.agents/skills/hif-debug-routing/` (маршрутизация к существующим инструментам отладки), раздел «Внешние agent-инструменты» в `docs/product/agent_orchestration.md`, уточнение ссылки в `hif-polish-loop`. Production Unity не затронут; CI Gate для такого diff — docs-only режим.
