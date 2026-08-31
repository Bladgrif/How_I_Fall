# Готовность пайплайна сюжетного контента

## Контекст

Текущий приоритет — функциональная демо-версия. Реальный сюжет, канонические маршруты и финальный арт отложены. Этот аудит не разрешает менять рабочий dialogue runtime, `SaveData`, технические fixtures или Unity-сцены.

Каталога `docs/story/` пока намеренно нет. Существующие dialogue assets в основном технические/demo и должны оставаться отдельно от будущего authored story material.

## Текущая runtime-модель

`DialogueSceneData` — входные данные runtime:
- registry разрешает `sceneId` в `DialogueSceneData`;
- сцена содержит `displayName`, опциональную музыку, `stopMusicOnStart`, последовательность lines/choices и `defaultNextScene`;
- line содержит стабильный `lineId`, ссылки на background/character `Sprite`, положение/visibility, speaker и text;
- choice содержит текст, опциональные typed numeric conditions, result text, существующие stat deltas и target scene.

Сохранённая сцена восстанавливается по `sceneId`, позиция — по `lineId` с legacy fallback на индекс. Read history также использует `(sceneId, lineId)`. Выбранный вариант сейчас сохраняется как **индекс в исходном списке choices**, поэтому порядок выборов в уже совместимом контенте нельзя менять неосторожно.

## Текущая валидация

`DialogueContentValidator` уже проверяет:
- registry и уникальность scene ID;
- missing scene references;
- пустые/дублирующиеся line ID;
- корректность choices и UI capacity;
- typed numeric conditions;
- переходы в зарегистрированные сцены.

Статическая проверка не может доказать, что runtime-condition когда-либо станет истинной; для conditional choices нужен безопасный default route.

## Проблема авторинга

Unity Inspector удобен для небольших технических fixtures, но плохо подходит как каноническая поверхность для длинного текста и ветвлений: YAML смешивает prose с object references, ухудшает editorial review и повышает риск merge/Inspector ошибок.

## Рассмотренные варианты

| Вариант | Плюсы | Риск/стоимость | Решение |
|---|---|---|---|
| Inspector-only ScriptableObjects | Не требует tooling, напрямую подходит runtime | Плох для длинного текста и review | Не использовать как канонический writing workflow |
| Markdown + ручная конвертация | Отличный diff/review, ничего не меняет сейчас | Ручная работа и риск ошибок при росте контента | Лучший первый шаг |
| Markdown + маленький deterministic importer | Markdown остаётся source of truth, генерация повторяема | Синтаксис рано фиксировать без реального материала | Построить позже при реальной необходимости |
| Markdown → JSON → Unity | Машиночитаемо | Добавляет лишнюю схему и границу валидации | Не добавлять |
| Новый narrative framework | Может дать authoring tools | Переписывает работающий runtime/save contracts | Отклонить |

## Рекомендация

Когда сюжет явно будет открыт, использовать **Markdown как будущий канонический source**, но **не строить importer заранее**. Сначала написать небольшой реальный набор сцен в `docs/story/`, после чего решить, стала ли ручная конвертация достаточно повторяющейся и ошибкоопасной, чтобы оправдать узкий deterministic Markdown-to-assets importer.

Это thin adapter plan, а не migration на новый narrative framework.

## Политика стабильных ID

- `sceneId` — постоянный lower-case ASCII slug и идентичность сцены, а не заголовок/filename.
- `lineId` — постоянный scene-local lower-case ASCII slug, не производный от текста или позиции.
- Редактирование prose не меняет ID.
- Вставленные lines получают новые ID; старые ID не переиспользуются.
- Удаление/переименование уже сохранённых позиций считается save-affecting изменением.
- Пока choice хранится по source-list index, не переупорядочивать и не вставлять выборы перед существующими в released/save-compatible сценах без отдельного compatibility решения.

## Политика asset references

В будущем Markdown должен ссылаться на project-relative Unity paths, например `Assets/HowIFall/Art/...`. При импорте они разрешаются в существующие `Sprite`/`AudioClip` references через Unity tooling. Не вводить отдельный asset-ID catalogue без реальной необходимости.

## Граница будущей валидации

Importer, если он будет одобрен позже, должен проверять только существенные authoring ошибки сверх существующего runtime validator:
1. malformed Markdown/front matter и unknown keys;
2. duplicate/missing scene/line IDs;
3. unknown condition/state/operator/effect names;
4. missing/wrong-type asset paths;
5. unresolved `next_scene`;
6. изменение ID/choice order, способное молча сломать сохранённые позиции.

После import всё равно запускается `DialogueContentValidator`.

## Политика generated assets — если importer когда-нибудь будет принят

- authored source — `docs/story/`;
- generated assets — отдельный стабильный каталог вроде `Assets/HowIFall/Data/Dialogues/StoryGenerated/`;
- generated `.asset` и `.meta` коммитятся;
- source scene детерминированно соответствует одному стабильному asset path;
- обычный import не удаляет orphaned assets автоматически;
- importer не трогает технические/manual dialogue assets вне своего каталога.

## Первый реальный content workflow

1. Написать и отревьюить небольшой реальный набор сцен в `docs/story/`.
2. Назначить стабильные scene/line IDs и проверить route/assets.
3. Решить, достаточно ли ещё ручной конвертации; если нет — реализовать узкий importer.
4. Convert/import в существующий `DialogueSceneData` и запустить `DialogueContentValidator`.
5. Добавить focused runtime/save-position coverage для нового поведения.
6. Для player-facing сцен выполнить graphical/content QA и просмотреть screenshots.

## Сейчас не строить

- importer до появления реального материала;
- JSON intermediate schema;
- generic narrative framework / Ink/Yarn migration;
- string-key generic flag system или arbitrary scripting;
- global content database/asset catalogue;
- `SaveData`/`GameState` redesign;
- канонический сюжет, routes, characters, art или lore.

## Решение

**OPTION 2 — Markdown как будущий source of truth, tooling отложить до первого реального сюжетного материала.**
