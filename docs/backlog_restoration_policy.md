# Backlog Restoration Policy

## Current Behavior

Проверен master после b5b47a1, d79d445 и 9215344.

- DialogueBacklog — runtime-список VNDialogueController с capacity 100; пустой text пропускается, старые entries отбрасываются.
- ShowLine и ShowNarration добавляют raw speaker/text до вывода. В History входят реплики, narration, choice.resultText, terminal и diagnostic narration; тексты choice buttons не входят.
- BuildRichText форматирует raw entries только при выводе и экранирует TMP rich text.
- Переход между DialogueSceneData не очищает backlog.
- Manual, Auto и Quick используют SaveData v2 без backlog или transient UI. Restore повторно отображает current line, а choice-result — resultText.
- Сейчас in-place Load сохраняет future session history и добавляет current line повторно. После restart history пуста.
- DialogueReadHistory — отдельный persistent seen-state для Skip; не player-facing History и не источник восстановления.

Точное post-load behavior Eternum runtime не подтверждено source audit; policy не копирует RenPy rollback.

## Goals

1. После Load показывать history сохранённого продолжения.
2. При старом save убрать future entries другой ветки.
3. Единообразно определить Manual, Auto, Quick, Continue и pre-load checkpoint.
4. Сохранить capacity 100, safe formatting и разделение с DialogueReadHistory.

## Non-Goals

- Rollback/rollforward и reconstruction по story graph.
- Profile-global history между прохождениями.
- TMP rich-text output, typewriter progress и transient UI.
- Изменения runtime, SaveData, Unity scenes или tests в этой задаче.

## Options Considered

| Вариант | Оценка | Решение |
|---|---|---|
| Session only | In-place Load смешивает ветки, restart пустой. | Отклонён |
| Profile-global history | Старый save не отсечёт future entries. | Отклонён |
| Save-scoped snapshot | Точная bounded history каждого slot; нужны schema/migration. | Выбран |
| Story-graph reconstruction | Ненадёжен при choices, conditions, flags и resultText. | Отклонён |

## Decision

Выбран save-scoped bounded snapshot. Каждый Manual, Auto и Quick slot хранит до 100 raw entries. Snapshot принадлежит slot, не profile и не GameState. После успешного Load/Continue runtime backlog полностью заменяется snapshot-ом; merge и append прежней session history запрещены.

Snapshot включает visible current line, потому что она добавляется до вывода. После restart без Load History пуста.

## UX Semantics

| Событие | Policy |
|---|---|
| New Game | Новый пустой runtime backlog. |
| Normal advance | Добавить displayed line/narration; оставить последние 100. |
| Scene transition | Не очищать. |
| Manual / Auto / Quick save | Сохранить snapshot, включая visible current line/result beat. |
| Load / Quick Load | Заменить backlog snapshot-ом slot; не merge/append. |
| Continue | Тот же Load самого нового валидного slot. |
| Pre-load autosave | Сохраняет текущую ветку до Load. |
| Load older save | Future entries исчезают. |
| App restart без Load | Backlog отсутствует. |
| Legacy/corrupt snapshot | Gameplay load продолжается с пустой History. |

## Save / Load Semantics

1. Capture получает копию raw entries, не mutable list.
2. Будущий v3 data валидируется до атомарной JSON/preview записи.
3. Существующий preflight scene/line/choice state сохраняется.
4. In-place Load заменяет backlog до restore display; scene reload передаёт snapshot новому controller до RestoreFromGameState.
5. При неуспешном restore возвращаются прежние GameState и backlog.

## Current Line Duplication Policy

- Snapshot всегда включает visible entry, включая resultText при choiceResultActive.
- Не сравнивать последнюю строку: повторы легитимны. Stable entry identity не нужна.
- До LoadDialogueScene заменить backlog snapshot-ом и на короткий restore scope включить suppressAddToBacklogDuringRestore.
- Suppress действует только на restore-вызовы ShowLine/ShowNarration и сбрасывается через try/finally.
- Scope включает RestoreChoiceResult и ShowFinalLine: resultText уже в snapshot; следующий advance добавляет первую строку новой сцены один раз.

Долгоживущий flag небезопасен: после exception он может подавить реальные новые entries. Нужен scoped contract; rollback stack не нужен.

## Choice Result Semantics

Save на result beat хранит текущие selectedChoiceIndex, choiceResultActive, pendingNextSceneId и raw resultText как обычный backlog entry. Restore валидирует choice/target, отображает resultText под suppress, не применяет choice повторно и затем использует pendingNextScene.

## Proposed Data Shape

Будущий формат — SaveData v3:

    [Serializable]
    public sealed class BacklogEntryData
    {
        public string speaker;
        public string text;
    }

    public List<BacklogEntryData> backlogEntries;

- Максимум 100 entries, oldest → newest.
- Хранить raw speaker/text; null speaker нормализовать в empty string.
- Не хранить TMP string, markup, color, timestamp, scene/line ID, typewriter progress, UI state, choice metadata или identity.
- После deserialize применять существующий EscapeRichText formatting path.

## Migration Policy

- Поднять SaveData.CurrentVersion с v2 до v3 только в будущей implementation-задаче.
- Валидные v1/v2 остаются loadable без snapshot.
- Legacy fallback: пустой backlog, normal restore display добавляет только current line/resultText.
- Следующий save создаёт v3 snapshot.
- Invalid core save остаётся non-loadable. Invalid optional snapshot даёт warning и empty fallback, но не блокирует gameplay load.

## Edge Cases

- null/empty entries пропустить; null speaker — empty string; больше 100 — оставить последние 100.
- Oversized text пропустить с warning по явному лимиту; не обрезать молча.
- Malformed snapshot — empty fallback; malformed core save — отказ Load.
- Символы <, > и & хранить raw и экранировать только при render.

## Test Plan for Future Implementation

Не выполнять сейчас. После реализации проверить snapshot round-trip; Manual/Auto/Quick; старый Load; pre-load autosave; normal line/result beat без дублей и повторных stat-delta; scene transition; New Game/restart; v1/v2/corrupt fallback; escaping после deserialize.

Из-за изменения SaveData/restore flow обязательны Manual Save graphical E2E — YES и Save Backend v2 graphical E2E — YES, обновить до v3 coverage. Запускать графически, без -batchmode и без -nographics.

## Implementation Scope

Текущая задача меняет только docs. Будущая задача затронет DialogueBacklog.cs, DTO entry, VNDialogueController.cs, SaveData.cs, SaveManager.cs, focused tests и graphical E2E. Не менять DialogueReadHistory, story assets, Unity scenes, GameState choice model и rollback.

## Risks

- Merge при Load раскроет future route.
- Display без scoped suppression создаст duplicate line/resultText.
- Широкий suppress потеряет entries после exception; обязателен try/finally.
- Optional malformed snapshot не должен лишить игрока legacy save.

## Acceptance Criteria

- Единственная модель: save-scoped snapshot.
- Backlog и DialogueReadHistory раздельны.
- Определены save/load UX, v3, legacy/corrupt behavior и escaping.
- Snapshot хранит raw speaker/text до 100, никогда не TMP output.
- Выбран scoped способ исключить дубли без stable identity/rollback.
- Будущие files/tests/E2E определены, но сейчас не изменяются.
