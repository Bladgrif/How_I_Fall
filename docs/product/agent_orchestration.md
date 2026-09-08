# Оркестрация агентов How I Fall

## Назначение

Этот документ — компактная policy для reviewer/ChatGPT: как выбирать и
brief-ить coding agents для How I Fall. Репозиторий — durable source of truth,
а не память чата. Детали исполнения уже принадлежат relevant skills, особенно
`$hif-polish-loop` и `$hif-visual-qa`; здесь они не дублируются.

## 1. Перед выдачей новой задачи

Reviewer передаёт задачу только после короткой проверки:

1. определить актуальный repository state, `master` и accepted base SHA;
2. прочитать relevant repository docs и skills;
3. если reviewer tools доступны, сверить Drive capability map и roadmap;
4. при конфликте предпочесть repository, затем передать reviewer'у задачу
   синхронизации Drive;
5. проверить, что нет нерешённого review candidate, который должен быть
   принят или исправлен первым.

Следующую работу нельзя выбирать только по памяти текущего чата. Если
implementation agent не имеет доступа к Drive, это нормально: Drive sync,
roadmap comparison и reviewer verdict принадлежат reviewer.

## 2. Выбор типа работы

Используй самый простой подход, достаточный для риска задачи:

- **Normal bounded fix** — маленький известный баг, docs-only, deterministic
  regression или механическая правка без feedback loop.
- **`$hif-polish-loop`** — одна player-facing поверхность с несколькими
  связанными visual/UX дефектами, где полезны baseline, screenshots и bounded
  correction loop. Соблюдай лимиты самого skill.
- **Research-first** — существенное новое UI/product/functionality решение,
  неопределённая interaction model или решение, которому нужны references и
  rationale. Сначала repository docs, затем сильные benchmark references,
  task-specific examples и сравнение вариантов.
- **Architecture/debugging pass** — трудный root cause, interdependent state,
  безопасность или риск совместимости `SaveData`.

Не включай research или polish machinery для тривиальной правки. Не называй
обычную проверку «research-first», если решение уже закреплено в repository.

## 3. Выбор модели и reasoning

Выбирай самую дешёвую модель, которая разумно способна выполнить задачу:

- **Luna** — tiny fixes, docs, tests, configs и deterministic/mechanical work.
- **Terra** — модель по умолчанию для обычной Unity/C#/UI implementation.
- **Sol** — сложная архитектура, трудный root-cause debugging,
  interdependent state и safety-sensitive работа.
- **Astra** — редкое дорогое autonomous visual/product judgement; не default
  coder и не модель для маленьких тестов/docs.

Уровень reasoning:

- **Low** — deterministic/mechanical работа;
- **Medium** — обычная implementation, UI и умеренная диагностика;
- **High** — трудная архитектура/root cause при реальной неопределённости.

Не выбирай High по умолчанию. Не утверждай, что runtime умеет delegation или
model routing, если это не подтверждено средой; при отсутствии routing не
имитируй его.

## 4. Текущая или новая сессия

- **Текущая сессия** — продолжение той же bounded задачи, поверхности и
  контекста, включая correction pass после evidence.
- **Новая сессия** — независимая задача, другая система/поверхность или случай,
  когда старый контекст уже создаёт шум.

Не держи одну гигантскую сессию бесконечно. При смене scope сначала обнови
task brief и accepted base.

## 5. Контекст и budget

Prompt должен называть точные relevant files, systems и contracts. Не проси
агента читать весь repository и не вставляй большие документы, если агент
может открыть конкретные файлы сам. Для больших файлов сначала укажи поиск и
targeted spans; расширяй context pack только по доказанной dependency.

Для `$hif-polish-loop` действует его default: не более 10 genuinely relevant
файлов в initial context pack и не более 3 implementation/visual итераций,
если более строгий лимит не задан задачей.

Дорогой context и модель резервируются для judgement. Bulk overview,
boilerplate и механические проверки отдавай дешёвому worker'у только при
реально доступном routing и точной спецификации. Worker output — предложение,
не proof; root cause, Save compatibility, product decision и exact editing
остаются у implementer/reviewer.

## 6. Контракт implementation prompt

Обычный paste-ready prompt должен содержать:

- `Модель`, `Reasoning`, `Сессия`;
- goal и ожидаемый accepted/base SHA, если он важен;
- task scope и read-first files;
- защищаемые contracts, включая сцены/prefabs/serialized refs и SaveData,
  когда они релевантны;
- разрешённые и запрещённые изменения;
- objective acceptance criteria;
- targeted tests, smoke и validation level;
- graphical QA и screenshot inspection для player-facing работы;
- git/commit/push policy;
- короткий формат отчёта и stop conditions.

Ссылайся на `AGENTS.md` и relevant skill вместо копирования общих правил, но
всегда формулируй task-specific protections и acceptance criteria.

## 7. Worktree и Git safety

Перед независимой implementation проверь expected accepted base. При mismatch
остановись и сообщи actual SHA — не делай автоматический rebase на новый
`master`.

Если обычный checkout dirty, divergent или содержит unrelated work, используй
clean disposable worktree от verified base. Не делай destructive reset/clean,
не затирай пользовательские изменения, не используй `git add .` при чужом
diff и не делай force-push. Stage только явно названные task files.

## 8. Validation routing

- чистая логика/state/save/choice — EditMode NUnit;
- runtime/UI/lifecycle — PlayMode NUnit;
- prefab/serialized/project integrity — существующие smoke/validators;
- bug fix — regression, ловящий исходный defect;
- docs-only — обычно Unity tests не нужны;
- player-facing — существующий graphical E2E в реальном runtime,
  screenshot inspection и при значимом visual pass небольшой curated baseline.

Не запускай broad expensive suites без конкретной причины. После push
обязательные GitHub checks остаются `Unity Test Framework` и `Unity smoke
tests`; локальные проверки, graphical E2E, screenshots и CI сообщаются
раздельно. Review candidate не является `DONE` до reviewer review и зелёного
обязательного CI.

## 9. Бюджет и stop conditions

Остановись, а не импровизируй, если:

- expected base изменился;
- отсутствует необходимое product decision;
- требуемое изменение выходит за scope;
- неожиданно требуется менять scene, prefab или SaveData contract;
- infrastructure блокирует обязательное proof;
- исчерпан согласованный iteration/context budget.

После достижения objective acceptance не используй оставшийся бюджет «потому
что он есть». Не перечитывай тот же corpus без нового риска или dependency.

## 10. Граница implementer/reviewer

**Implementer** владеет scoped implementation, local tests, graphical proof,
screenshot inspection и коротким отчётом со статусом `REVIEW CANDIDATE` или
`BLOCKED`.

**Reviewer/ChatGPT** владеет реальным diff/scope review, mandatory CI,
сравнением с repository и Drive roadmap/capability map, Drive 02/03 sync и
вердиктом `DONE`/`NEEDS CORRECTION`, а также выбором следующей задачи.

**User** нужен только для genuinely subjective aesthetic approval или другого
неавтоматизируемого решения вкуса.

## 11. Короткие примеры маршрутизации

- Известный двухстрочный баг: **Luna / Low / текущая сессия / normal bounded
  fix**.
- Обычная Unity UI feature в новом scope: **Terra / Medium / новая сессия**.
- Одна UI-поверхность с несколькими visual defects: **Terra / Medium / текущая
  или новая сессия по контексту + `$hif-polish-loop`**.
- Сложная ошибка состояния и Save compatibility: **Sol / High**, только при
  явно доказанной неопределённости и отдельном bounded scope.
- Редкая автономная visual/product critique: **Astra**, bounded и дорогой
  pass, не обычная implementation.
