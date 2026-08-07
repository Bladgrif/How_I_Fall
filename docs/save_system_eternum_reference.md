# How I Fall — референс системы сохранений Eternum

> Исторический документ: разделы о текущем состоянии How I Fall и план реализации фиксируют состояние до Save Backend v2. Актуальные статусы функций находятся в `eternum_feature_tracker.md`; этот файл сохраняется как технический референс и журнал принятых решений.

Статус: аналитический документ, реализация не начата.  
Дата анализа: 2026-08-06.  
Eternum: распакованная русская сборка `0.9.5`.  
How I Fall: текущее рабочее дерево Unity-проекта, включая незакоммиченные изменения.

## 0. Границы анализа

- Код, изображения, шрифты и другие ассеты Eternum не переносятся.
- Из Eternum берутся только наблюдаемое поведение, структура взаимодействий и требования к данным.
- Unity-проект на этом шаге не изменялся, кроме добавления этого документа.
- Unity Editor и Eternum не запускались. Выводы сделаны по исходным `.rpy`/`.py`, сериализованным Unity-сценам, C#-коду и существующим файлам сохранений на диске.
- Под «фактической реализацией Eternum» ниже понимаются одновременно игровые экраны из `game/` и действия/формат сохранений из поставляемого с игрой Ren'Py 8.3.2.

## 1. Краткие выводы

1. Eternum использует один общий экран слотов для Save и Load: сетка **3 × 2**, то есть **6 слотов на странице**.
2. Внизу есть `A`, `Q`, десять номеров ручных страниц и стрелки `<`, `>`, `<<`, `>>`. Колесо мыши также меняет страницы.
3. Слот показывает скриншот **384 × 216**, дату/время и `save_name`. Автоматического названия главы и автоматического текста текущей реплики в карточке нет.
4. Ручное сохранение поддерживает пользовательское имя до 30 символов. Окно имени одновременно подтверждает создание или перезапись.
5. Авто- и быстрые сохранения циклические. Движок настроен на 10 файлов каждого типа, хотя экран показывает только позиции 1–6. Это фактическое несоответствие реализации.
6. Загрузка из игры требует подтверждения и перед загрузкой обычного/quick-слота создаёт autosave. Из главного меню занятый слот загружается сразу.
7. Удаление вызывается клавишей `Delete`/`KP Delete`; отдельной видимой кнопки удаления в карточке нет. Удаление всегда подтверждается.
8. В главном меню Eternum есть `Start` и `Load`, но **нет подключённой кнопки Continue**. Движок Ren'Py имеет действие `Continue`, однако экран Eternum его не использует.
9. How I Fall уже умеет сохранять и восстанавливать `sceneId`, `lineId`, fallback-индекс, результат текущего выбора и поля `GameState`, но пока не имеет реальных autosave/quicksave, подтверждений, удаления конкретного слота и UX, близкого к Eternum.
10. Существующий `SaveManager` нужно расширять, а не заменять. Основной риск — две сериализованные копии Save/Load UI в `MainMenu.unity` и `VNPrototype.unity` и два параллельных пути UI-кода внутри `MainMenuController.cs`.

## 2. Что именно просмотрено в Eternum

### 2.1. Игровые файлы

| Файл | Фактическая роль |
|---|---|
| `game/screens.rpy` | `screen quick_menu`, `screen navigation`, `screen main_menu`, `screen game_menu`, `screen save`, `screen load`, базовый `screen file_slots`, `screen confirm`, touch-вариант quick menu. |
| `game/save_name.rpy` | Фактически активная переопределённая версия `file_slots` благодаря `init offset = 99`; пользовательские имена сохранений; страницы `A/Q/1..`; переходы по страницам. |
| `game/gui.rpy` | Размер карточки, размер thumbnail, сетка 3 × 2, отступы и цвета состояний. |
| `game/options.rpy` | `config.version`, каталог сохранений, переход после загрузки, F5/F9 и дополнительные горячие клавиши quick save/load. |
| `game/save_compatibility.rpy` | `label after_load`, миграция старых игровых переменных и обновление `game_version`. |
| `game/script.rpy` | Проверка установки `game_version`; пользовательское действие Continue или собственная подсистема сохранений не найдены. |

`game/screens.rpy` содержит стандартную более раннюю версию `file_slots`, но активной является версия из `game/save_name.rpy`: у неё более высокий init offset. Поэтому количество страниц, ввод имени и `<<`/`>>` следует определять по `save_name.rpy`, а не по шаблонному экрану в `screens.rpy`.

### 2.2. Поставляемые файлы Ren'Py, от которых зависит поведение Eternum

| Файл | Просмотренные функции/настройки |
|---|---|
| `renpy/common/00action_file.rpy` | `FileLoadable`, `FileScreenshot`, `FileTime`, `FileJson`, `FileSaveName`, `FileSave`, `FileLoad`, `FileDelete`, `FileAction`, `FileTakeScreenshot`, `QuickSave`, `QuickLoad`, формирование имён слотов. |
| `renpy/common/00action_menu.rpy` | `Continue`, `MainMenu`, `Quit`. |
| `renpy/common/00gamemenu.rpy` | `_enter_menu`: создание screenshot до открытия game menu; включение quicksave. |
| `renpy/common/00layout.rpy` | `autosave_on_quit`, тексты/вызовы подтверждений, `auto_save_extra_info`, которое возвращает текущий `save_name`. |
| `renpy/common/00gui.rpy` | Тексты подтверждения удаления, перезаписи, загрузки, возврата в меню и Continue. |
| `renpy/common/00keymap.rpy` | `save_delete = Delete/KP Delete`, стандартные события колеса для страниц. |
| `renpy/loadsave.py` | Состав metadata JSON, сохранение screenshot, циклирование autosave, частота autosave, поиск самого нового слота. |
| `renpy/config.py` | `has_autosave = True`, `autosave_slots = 10`, `autosave_frequency = 200`, autosave при choice/input. |
| `renpy/defaultstore.py` | `_autosave = True` по умолчанию. |
| `renpy/exports/menuexports.py` | Принудительный autosave перед выбором. |
| `renpy/exports/inputexports.py` | Принудительный autosave перед пользовательским вводом. |
| `renpy/__init__.py` | Физический суффикс файлов `-LT1.save`. |

### 2.3. Проверенный реальный файл сохранения Eternum

На диске найден каталог:

`C:\Users\roman\AppData\Roaming\RenPy\Eternum-1610153667`

Проверены существующие файлы вида:

- `9-2-LT1.save` — ручная страница 9, слот 2;
- `auto-1-LT1.save` … `auto-10-LT1.save` — кольцо autosave;
- `persistent` — отдельные постоянные настройки/имена страниц.

`.save` является ZIP-контейнером. В проверенном ручном сохранении были записи:

- `screenshot.png` — 384 × 216;
- `extra_info` — имя сохранения;
- `json` — `_save_name`, версия Ren'Py, версия игры, runtime, время создания;
- `log` — сериализованное состояние выполнения/rollback Ren'Py;
- `renpy_version` и `signatures`.

Дата в карточке берётся из времени модификации файла (`FileTime`), а не напрямую из `_ctime`. Позиция сценария и игровые переменные восстанавливаются из внутреннего состояния Ren'Py, а не из публичного metadata JSON.

## 3. Eternum глазами игрока

### 3.1. Элементы экрана Save/Load

На общем экране присутствуют:

- заголовок `Save` или `Load`;
- навигация game menu и кнопка `Return`;
- редактируемое название текущей страницы по центру;
- переключатель `Save naming enabled/disabled` справа сверху;
- шесть карточек сохранений;
- нижний ряд пагинации: `<<`, `<`, `A`, `Q`, десять номеров страниц, `>`, `>>`;
- модальное окно имени/перезаписи при сохранении;
- общее модальное окно Yes/No для загрузки, удаления и перезаписи без naming.

При Load из главного меню дополнительно выводятся отдельный фон и крупная надпись `LOAD`. Save из главного меню штатно недоступен.

### 3.2. Расположение слотов и кнопок

- Базовое разрешение интерфейса — 1920 × 1080.
- Слоты расположены в центре сеткой 3 столбца × 2 строки.
- Размер карточки — 414 × 309.
- Скриншот внутри карточки — 384 × 216.
- Между карточками — 15 px по настройке GUI.
- Название страницы находится над сеткой.
- Пагинация находится внизу.
- В игровом game menu слева зарезервирована вертикальная навигация; `Return` — отдельная кнопка общего контейнера.

### 3.3. Сколько слотов на странице

**6 слотов:** три в верхнем ряду и три в нижнем.

Это относится к ручной странице, странице autosave и странице quicksave. При этом движок хранит 10 auto- и 10 quick-файлов; карточки 7–10 этим экраном не показываются.

### 3.4. Как работают страницы

- `A` открывает страницу автоматических сохранений.
- `Q` открывает страницу быстрых сохранений.
- Числа открывают ручные страницы.
- Одновременно показан блок из десяти ручных страниц: 1–10, 11–20 и так далее.
- `<` и `>` переходят последовательно: `A → Q → 1 → 2 → ...`; назад — в обратном порядке.
- `<<` и `>>` перескакивают примерно на десять ручных страниц и меняют десятичный блок.
- Колесо мыши выполняет последовательный переход назад/вперёд.
- Выбранная страница имеет selected-состояние.
- Заголовок ручной страницы можно активировать и переименовать; это имя хранится в `persistent`.
- Жёсткий максимум ручной страницы в вызовах Eternum не задан.

### 3.5. Данные в каждом слоте

Занятый слот показывает:

1. screenshot момента сохранения;
2. локализованную дату и время в формате, эквивалентном `день недели, месяц, число, год, часы:минуты`;
3. `save_name` — пользовательское имя или имя, унаследованное текущим состоянием игры.

Не показываются автоматически:

- номер/название главы;
- label сценария;
- текст текущей реплики;
- отношения и флаги;
- версия игры.

Имя autosave может быть не пустым, потому что Ren'Py передаёт в autosave текущий `save_name`. Это не автоматическое название точки истории.

### 3.6. Нажатие на пустой слот

На экране Save:

- если naming включён, открывается модальное окно `Save name (new)`;
- можно ввести до 30 символов;
- исключены `\`, `[` и `{`;
- `Yes`/Enter сохраняет, `No`/Escape/правый клик отменяет;
- если naming выключен, сохранение создаётся сразу.

На экране Load:

- действие `FileLoad` неактивно, так как `renpy.can_load` возвращает false;
- нажатие не запускает загрузку;
- отдельного toast-сообщения «пустой слот» код не показывает.

### 3.7. Нажатие на занятый слот

На экране Save:

- при включённом naming открывается окно `Save name (overwrite)` с текущим именем;
- подтверждение одновременно принимает имя и разрешает перезапись;
- при выключенном naming появляется стандартное подтверждение перезаписи.

На экране Load:

- из главного меню сохранение загружается сразу;
- из игры появляется подтверждение, предупреждающее о потере несохранённого прогресса;
- перед подтверждением загрузки не-auto слота Ren'Py создаёт autosave текущего состояния;
- после подтверждения загружается сохранённое состояние, а экран меню больше не остаётся поверх игры.

### 3.8. Подтверждение перезаписи и удаления

Перезапись:

- с naming — специализированное окно имени с красной пометкой overwrite и Yes/No;
- без naming — общий modal confirm с текстом о перезаписи;
- Escape и правый клик означают No.

Удаление:

- запускается `Delete` или `KP Delete` для активного слота;
- пустой слот удалить нельзя;
- появляется общий modal confirm Yes/No;
- после Yes файл удаляется;
- видимой иконки корзины в карточке нет.

Точная зависимость клавиши Delete от фокуса/hover конкретной карточки требует runtime-проверки.

### 3.9. Autosave и quicksave

#### Autosave

- включён по умолчанию;
- кольцо рассчитано на 10 файлов: новый становится `auto-1`, предыдущие сдвигаются;
- периодический autosave запускается примерно после 200 взаимодействий;
- autosave принудительно вызывается перед choice и перед текстовым input;
- autosave вызывается перед выходом, возвратом в Main Menu и загрузкой не-auto сохранения;
- в main menu, replay, rollback и при отключённом `_autosave` запись блокируется;
- выполняется в фоне, если платформа это допускает;
- на странице `A` доступны для просмотра/загрузки только auto-1 … auto-6.

#### Quicksave

- доступен из desktop quick menu через `Q.Save`;
- горячие клавиши Eternum: `S`, `Shift+S`, `F5`;
- перед записью берётся screenshot текущего игрового кадра;
- новый quicksave записывается как quick-1, старые циклически сдвигаются до quick-10;
- после успеха показывается уведомление о завершении quick save;
- `Q.Load`, `L`, `Shift+L`, `F9` загружают quick-1;
- quick load из игры использует подтверждение загрузки;
- на странице `Q` UI показывает quick-1 … quick-6.

Touch-вариант quick menu содержит только Back, Skip, Auto и Menu. Прямых Q.Save/Q.Load-кнопок в нём нет; Save/Load доступны через Menu.

### 3.10. Continue

В пользовательском главном меню Eternum **Continue отсутствует**. Есть `Start` и `Load`.

Поставляемый Ren'Py содержит готовое действие `Continue`, которое:

- ищет самый новый слот по времени модификации;
- по умолчанию учитывает manual, auto и quick;
- становится неактивным, если сохранений нет;
- загружает найденный слот.

Но `screen main_menu` Eternum это действие не вызывает. Поэтому нельзя утверждать, что игрок Eternum использует Continue без модификации экрана.

### 3.11. Отличия Save/Load из игры и из главного меню

| Контекст | Save | Load | Возврат |
|---|---|---|---|
| Из игры | Доступен через quick menu и game menu. После ручной записи экран остаётся открыт. | Занятый слот требует подтверждения. Перед не-auto загрузкой создаётся autosave. | `Return`/Escape возвращает в ту же игру и к тому же состоянию. |
| Из главного меню | Кнопки Save нет; `FileSave` дополнительно запрещает сохранение при `main_menu`. | Доступен через кнопку Load; занятый слот загружается сразу без предупреждения о потере прогресса. | `Return`/Escape возвращает в main menu. |

### 3.12. Визуальные состояния кнопок и слотов

По screen language и GUI-стилям присутствуют:

- idle и hover-фоны карточки;
- pressed/focus-состояния базовой кнопки Ren'Py;
- selected-состояние текущей страницы;
- selected-выделение самого нового слота;
- insensitive-состояние пустого Load-слота и недоступной стрелки;
- занятый слот: screenshot + дата + имя;
- пустой слот: пустой thumbnail, текст `empty slot`, пустое имя;
- enabled/disabled-состояние naming-toggle;
- modal overlay, блокирующий ввод под окном подтверждения;
- hover-анимация отдельных кнопок главного меню.

Ассеты состояний были только идентифицированы по ссылкам (`slot_idle_background.png`, `slot_hover_background.png`, `confirm.png`) и не копировались.

### 3.13. Поведение при полном отсутствии сохранений

- Кнопка Load в главном меню остаётся доступной.
- Открывается обычный Load screen.
- Все шесть карточек текущей страницы пустые и неактивны для загрузки.
- Пользователь может переключать `A`, `Q` и ручные страницы и вернуться назад.
- Отдельного экрана, крупного сообщения или автозапуска New Game нет.
- Continue отсутствует; поэтому отдельное disabled-состояние Continue в Eternum не видно.

## 4. Текущая реализация How I Fall

### 4.1. Просмотренные классы

| Файл/класс | Текущее поведение |
|---|---|
| `Assets/HowIFall/Scripts/Save/SaveData.cs` | Формат version 3; `currentSceneId`, `currentLineId`, fallback `currentLineIndex`, состояние последнего выбора, slot metadata и все текущие поля `GameState`; миграции v0→v3; будущая версия отклоняется. |
| `Assets/HowIFall/Scripts/Save/SaveManager.cs` | Singleton + `DontDestroyOnLoad`; JSON; `.tmp`/`.bak`; manual/auto-пути; PNG preview; загрузка latest; legacy `save_01.json`; применение данных к существующему `GameState`. |
| `Assets/HowIFall/Scripts/Core/GameState.cs` | Singleton + `DontDestroyOnLoad`; текущие отношения/параметры; позиция диалога; состояние выбранного ответа; явный `ResetState` только для New Game. |
| `Assets/HowIFall/Scripts/VN/VNDialogueController.cs` | Обновляет position по `sceneId`/`lineId`; восстанавливает сцену через registry; fallback по индексу; восстанавливает result выбранного ответа; F5/F9 вызывают Save/Load UI. |
| `Assets/HowIFall/Scripts/VN/DialogueSceneRegistry.cs` | Линейный поиск `DialogueSceneData` по стабильному `sceneId`. |
| `Assets/HowIFall/Scripts/VN/DialogueSceneData.cs` | Содержит `sceneId`, lines, choices, переходы; ищет `lineId`. |
| `Assets/HowIFall/Scripts/Core/SceneFlowManager.cs` | New Game сбрасывает `GameState`; Continue загружает latest и открывает `VNPrototype`; загрузка уже применённого save не вызывает reset. |
| `Assets/HowIFall/Scripts/UI/MainMenuController.cs` | Содержит `MainMenuController`, `SaveLoadSlotButton` и `SaveLoadPanelController`; 8 карточек, 20 страниц, режимы Auto/Load/Save; Save запрещён в Main Menu. |
| `Assets/HowIFall/Scripts/Settings/GameSettings.cs` | Есть настройка `autoSave = true`. |
| `Assets/HowIFall/Scripts/Settings/SettingsManager.cs` | Сохраняет `autoSave` в PlayerPrefs, но SaveManager её не использует. |
| `Assets/HowIFall/Scripts/UI/SettingsPanelController.cs` | Показывает и изменяет toggle autosave, не запускает autosave. |
| `Assets/HowIFall/Scripts/UI/QuickSaveStatusView.cs` | Проверяет только наличие legacy `save_01.json`; реального quick-ring нет. |
| `Assets/HowIFall/Editor/SaveSystemSmokeTests.cs` | Проверяет миграции, future-version rejection, stable line lookup, atomic write и recovery из `.bak`. |
| `Assets/HowIFall/Editor/MainMenuSceneBuilder.cs` | Генерирует текущую Main Menu Save/Load-панель, 8 карточек и связи событий; его потребуется синхронизировать с изменениями сцены. |

### 4.2. Сцены и UI

Проверены:

- `Assets/HowIFall/Scenes/MainMenu.unity`;
- `Assets/HowIFall/Scenes/VNPrototype.unity`.

Фактическое состояние:

- в обеих сценах есть `SaveManager`, `GameState`, `SceneFlowManager`; singleton-защита уничтожает сценовые дубликаты после перехода;
- в обеих сценах Save/Load UI встроен непосредственно в сцену;
- UI-prefab Save/Load в `Assets/HowIFall` отсутствует;
- в каждой панели сериализовано 8 `SaveLoadSlotButton`;
- Main Menu: `saveEnabled = false`, `vnController = null`;
- VNPrototype: `saveEnabled = true`, `vnController` назначен;
- Main Menu скрывает часть основного меню при открытии панели;
- VNPrototype накладывает панель на текущую сцену;
- стандартные Unity Button states: normal/highlighted/pressed/selected/disabled.

### 4.3. Диск и текущие файлы

Текущая схема:

```text
Application.persistentDataPath/
├── save_01.json
└── Saves/
    ├── slot_1_manual.json
    ├── slot_1_manual_preview.png
    ├── slot_1_manual.json.bak
    ├── slot_1_auto.json
    └── slot_1_auto_preview.png
```

В данной Windows-конфигурации `Application.persistentDataPath` соответствует:

`C:\Users\roman\AppData\LocalLow\Bladgrif\How I Fall`

На момент первоначального аудита был найден legacy-файл `save_01.json` без явного `version`, с `currentSceneId = ui_test_scene`. Сейчас `ui_test_scene` присутствует в `DialogueSceneRegistry.asset`, но loadability любого legacy-файла всё равно должна определяться полной валидацией версии, сцены, строки и состояния, а не одним фактом существования файла.

### 4.4. Текущие сильные стороны

- Один `SaveManager`; второй создавать не нужно.
- Atomic replace JSON через `.tmp` и `.bak`.
- Recovery из backup при повреждении основного JSON.
- Версионирование и последовательная миграция v0→v3.
- Стабильные `sceneId`/`lineId` плюс fallback index.
- Все текущие числовые отношения/параметры сохраняются.
- Выбор и короткое choice continuation восстанавливаются.
- New Game и Load разделены: `StartNewGame` сбрасывает state, `LoadLoadedGameScene` — нет.
- Preview уже создаётся и читается UI.
- Основные save/load этапы логируются.

### 4.5. Текущие ограничения и дефекты UX

- `isAutoSave` и auto-пути есть, но ни один runtime-вызов не создаёт autosave.
- Настройка autosave является только PlayerPrefs-флагом и не связана с SaveManager.
- Quick slot type и quick-ring отсутствуют.
- F5 при назначенной панели открывает ручной Save, а не делает quicksave; F9 открывает Load, а не quick-load.
- 8 слотов на странице и фиксированные `1 / 20` не совпадают с Eternum.
- Нет страницы Q и decade-pagination.
- Перезапись происходит сразу без подтверждения.
- Удалить конкретный manual/auto слот нельзя. `DeleteSave()` удаляет только `save_01.json` и не удаляет preview/backup.
- In-game Load не запрашивает подтверждение и не создаёт защитный autosave.
- Пустая карточка прозрачная и без текста; в Eternum есть `empty slot` и insensitive load state.
- `SaveSlotInfo` отдаёт UI только дату и preview path; scene title, save name, line preview и compatibility state не отдаются.
- `linePreview` записывается в JSON, но `SaveLoadSlotButton` всегда очищает `previewText`.
- Нет пользовательского имени сохранения.
- Дата хранится локальной строкой без timezone/UTC, что осложняет сортировку и миграции.
- Continue выбирает файл по filesystem mtime до проверки, что JSON читается и `sceneId` есть в registry.
- Root `save_01.json` и manual slot 1 могут дублировать одно и то же состояние.
- Preview PNG пишется отдельно и не участвует в общей атомарной транзакции.
- Две сцены содержат отдельные копии панели, поэтому их легко рассинхронизировать.
- В `MainMenuController` остаётся старый внутренний Save/Load-код параллельно используемому `SaveLoadPanelController`.

## 5. Таблица сравнения

Обозначения риска: **низкий** — локальное расширение данных/UI; **средний** — меняет сценарий взаимодействия или сериализованные ссылки; **высокий** — затрагивает порядок загрузки и восстановление gameplay state.

| Возможность в Eternum | How I Fall сейчас | Чего не хватает | Файлы How I Fall для изменения | Риск |
|---|---|---|---|---|
| Общий Save/Load screen | Общая панель уже есть | Привести режимы и состояния к единой модели | `MainMenuController.cs`, обе `.unity` | Средний |
| 6 слотов, сетка 3 × 2 | 8 слотов | Перестроить layout и page size | `MainMenuController.cs`, `MainMenu.unity`, `VNPrototype.unity`, `MainMenuSceneBuilder.cs` | Средний |
| Manual pages | 20 фиксированных страниц по 8 | 6/page, номер ручной страницы без ложных пустых лимитов | `MainMenuController.cs`, `SaveManager.cs` | Низкий |
| Блоки по 10 страниц | Только Previous/Next и `N/20` | A/Q/1..10, `<<`/`>>`, selected page | `MainMenuController.cs`, сцены/builder | Средний |
| Autosave page A | UI-режим есть, файлов нет | Реальное кольцо, триггеры и связь с setting | `SaveManager.cs`, `VNDialogueController.cs`, `SettingsManager.cs`, `SaveData.cs` | Высокий |
| Quicksave page Q | Нет | Тип quick, ring, методы QuickSave/QuickLoad | `SaveManager.cs`, `SaveData.cs`, `VNDialogueController.cs`, UI | Высокий |
| F5/F9 quick save/load | F5/F9 открывают панели | Прямые quick actions и уведомления | `VNDialogueController.cs`, `SaveManager.cs` | Средний |
| Screenshot до открытия menu | Панель скрывается через CanvasGroup и делается ScreenCapture | Зафиксировать единый capture flow и размер thumbnail | `SaveLoadPanelController` в `MainMenuController.cs`, `SaveManager.cs` | Средний |
| Дата и время | Есть строка `savedAt` | UTC machine value + локализованный display | `SaveData.cs`, `SaveManager.cs`, `SaveLoadSlotButton` | Низкий |
| Пользовательское имя save | Нет | `displayName`, input, toggle naming | `SaveData.cs`, `MainMenuController.cs`, сцены/builder | Средний |
| Chapter/save point | Eternum автоматически не показывает; How I Fall пишет `sceneTitle = sceneId` | Решить: имя пользователя как основной title; sceneId оставить metadata | `SaveData.cs`, `SaveManager.cs` | Низкий |
| Текущая реплика в metadata | Хранится `linePreview`, UI её скрывает | Для parity не обязательна; оставить debug/optional secondary text | `SaveLoadSlotButton`, `SaveSlotInfo` | Низкий |
| Пустой slot state | Полностью пустая прозрачная карточка | Текст «Пустой слот», disabled Load, сохранение по клику в Save | `SaveLoadSlotButton`, `SaveLoadPanelController` | Низкий |
| Occupied slot state | Screenshot/date/«Слот N» | Имя, compatibility/error state, newest marker | `SaveManager.cs`, `MainMenuController.cs` | Средний |
| Overwrite confirm | Нет, перезапись сразу | Modal confirm или naming dialog | `SaveLoadPanelController`, сцены/builder | Средний |
| Delete slot + confirm | Нет; удаляется только root legacy file | Удаление конкретного JSON/PNG/backup, dialog | `SaveManager.cs`, `SaveLoadPanelController`, сцены/builder | Средний |
| In-game load confirm | Нет | Предупреждение о потере прогресса; optional safety autosave | `SaveLoadPanelController`, `SaveManager.cs` | Высокий |
| Main-menu load без confirm | Уже фактически immediate | Сохранить отличие по контексту | `SaveLoadPanelController` | Низкий |
| Return в игру/menu | Close панели уже есть | Гарантировать сохранение исходного контекста и focus | `SaveLoadPanelController`, сцены | Низкий |
| Continue latest | Есть, в отличие от UI Eternum | Искать newest **валидный** save, корректно disable/notify | `SaveManager.cs`, `SceneFlowManager.cs`, `MainMenuController.cs` | Высокий |
| Нет saves | Toast при Continue; пустые карточки | Load должен оставаться доступным; слоты disabled; Continue disabled или понятный toast | `MainMenuController.cs`, сцена MainMenu | Низкий |
| Restore scene/line | Уже есть ID + fallback | Предварительная валидация registry до применения GameState | `SaveManager.cs`, `SceneFlowManager.cs`, `VNDialogueController.cs` | Высокий |
| Restore choice/GameState | Уже есть для текущего vertical slice | Транзакционность и тесты нескольких путей | `SaveData.cs`, `SaveManager.cs`, tests | Средний |
| Старые saves | v0→v3 и backup recovery | Миграция путей/типов, incompatible state, dedup root/manual1 | `SaveData.cs`, `SaveManager.cs`, tests | Высокий |
| Newest selected | Нет явного newest marker | Metadata `isNewest` и visual state | `SaveManager.cs`, `SaveLoadSlotButton` | Низкий |
| Имена страниц | Нет | Необязательно для первой итерации; можно хранить в PlayerPrefs | `SaveLoadPanelController` | Низкий |
| 10 auto/quick-файлов при 6 видимых | Auto/quick отсутствуют | Не копировать скрытые 7–10 без решения по UI | `SaveManager.cs`, UI | Средний |

## 6. Технический план переноса на Unity

### 6.1. Принципы

1. Расширять существующий `SaveManager`; второй менеджер не создавать.
2. `GameState` остаётся единственным runtime-источником текущего состояния прохождения.
3. `DialogueSceneRegistry` и стабильные `sceneId`/`lineId` остаются механизмом восстановления.
4. Сначала полностью проверить save snapshot, затем разрешать Load/Continue.
5. Не применять частично прочитанный save к `GameState`.
6. Не запускать `StartNewGame` после успешной загрузки.
7. UI не должен знать физические пути; он работает через metadata и команды `SaveManager`.
8. Настройки пользователя остаются в PlayerPrefs и не входят в gameplay save.
9. Повторить понятное поведение Eternum, но не воспроизводить очевидные технические несоответствия вроде скрытых auto-7…10.

### 6.2. Модель данных сохранения

Повысить версию `SaveData` и сохранить текущие поля. Добавить только необходимые metadata:

#### Идентичность и совместимость

- `version` — schema version;
- `saveId` — уникальный GUID конкретной записи;
- `slotType` — `manual`, `auto`, `quick`;
- `slotIndex` — индекс внутри типа;
- `applicationVersion` — информационная версия билда;
- `createdAtUtc` — ISO-8601 UTC или ticks;
- `playTimeSeconds` — опционально, если уже доступно без новой системы;
- `unitySceneName` — сейчас `VNPrototype`;
- `isValid` не хранить в JSON: это вычисляемое состояние UI.

#### Позиция истории

- существующие `currentSceneId`;
- существующие `currentLineId`;
- существующий fallback `currentLineIndex`;
- `selectedChoiceIndex`;
- `choiceResultActive`;
- `pendingNextSceneId`.

#### GameState

Сохранять все существующие поля явно:

- `lust`, `romance`, `purity`, `corruptionLevel`, `selfControl`, `suspicion`;
- `trustMasha`, `trustArtem`, `leraInterest`;
- добавлять новые реальные сюжетные флаги в `GameState` и `SaveData` одновременно с новой migration step.

Не вводить на этом этапе универсальные `StoryState`/`RelationshipState`.

#### UI metadata

- `displayName` — имя пользователя, максимум 30 символов;
- `sceneTitle` — пока стабильный fallback, не заменяет `sceneId`;
- `linePreview` — хранить для diagnostics/возможного secondary text;
- `previewFileName` — относительное имя PNG, не абсолютный путь;
- `savedAtLocalText` не хранить: форматировать из UTC при отображении.

### 6.3. Типы слотов

Ввести enum рядом с `SaveData`, не новый manager:

- `Manual` — произвольный слот на выбранной ручной странице;
- `Auto` — циклическое системное сохранение;
- `Quick` — циклическое быстрое сохранение.

Рекомендуемая конфигурация первого переноса:

- `ManualSlotsPerPage = 6`;
- manual pages отображаются блоками по 10;
- `AutoSlotCount = 6`;
- `QuickSlotCount = 6`.

Почему 6, а не 10: игрок Eternum фактически видит только шесть карточек A/Q. Хранить 10 и скрывать четыре — повторение дефекта, а не полезного UX. Если позже нужны 10, UI должен дать доступ ко всем десяти.

### 6.4. Структура файлов на диске

Предлагаемая схема без manifest, чтобы не создавать вторую точку отказа:

```text
Application.persistentDataPath/
└── Saves/
    ├── manual/
    │   ├── manual_0001.json
    │   ├── manual_0001.png
    │   └── manual_0001.json.bak
    ├── auto/
    │   ├── auto_01.json
    │   ├── auto_01.png
    │   └── ... auto_06.*
    ├── quick/
    │   ├── quick_01.json
    │   ├── quick_01.png
    │   └── ... quick_06.*
    └── LegacyBackup/
        └── ... исходные файлы до миграции
```

Номер manual slot вычисляется как `(page - 1) * 6 + visibleIndex + 1`. UI показывает страницу, но физический файл имеет стабильный глобальный номер.

Не хранить абсолютный `previewPath` в новом JSON: `Application.persistentDataPath` может измениться между устройствами.

### 6.5. Запись сохранения

Единый внутренний flow существующего `SaveManager`:

1. Получить snapshot из `GameState` и текущего `VNDialogueController`.
2. Проверить непустые `sceneId`/`lineId`, registry reference и допустимый slot.
3. Получить screenshot до показа Save/Load UI либо временно скрыть только эту панель.
4. Масштабировать preview до 384 × 216 или другого единого 16:9-размера.
5. Записать PNG во временный файл.
6. Записать JSON во временный файл UTF-8.
7. Заменить старые PNG/JSON атомарно настолько, насколько позволяет платформа; оставить `.bak` JSON.
8. Только после успеха обновить UI и показать toast.
9. При ошибке оставить предыдущий занятый slot рабочим.

Для manual slot:

- пустой слот + naming on → input `new`;
- занятый + naming on → input `overwrite`;
- naming off → пустой сохраняется сразу, занятый требует обычный confirm.

### 6.6. Screenshot preview

Сохранить существующий подход `ScreenCapture.CaptureScreenshotAsTexture`, но сделать его единым:

- capture выполняет UI controller до фактической записи;
- Save/Load modal не должен попадать в thumbnail;
- после capture исходный UI восстанавливается даже при исключении;
- texture обязательно уничтожается;
- preview читается лениво только для видимых шести карточек;
- при закрытии/перелистывании старые runtime Sprite/Texture уничтожаются;
- отсутствие PNG не делает JSON-save незагружаемым — показывается placeholder.

### 6.7. Метаданные и карточка слота

Расширить `SaveSlotInfo`, чтобы UI получал:

- slot type/index;
- occupied/empty;
- loadable/incompatible/corrupt;
- display name;
- локализованную дату;
- preview path;
- scene title;
- optional line preview;
- newest marker;
- diagnostic reason для несовместимого слота.

Карточка по умолчанию, близкая к Eternum:

- thumbnail;
- дата/время;
- display name;
- для пустого — «Пустой слот»;
- line preview хранится, но не обязан отображаться в основной карточке.

### 6.8. Пагинация

Один `SaveLoadPanelController` должен хранить:

- текущий контекст: Main Menu или In Game;
- режим: Load/Save;
- выбранный тип страницы: A/Q/Manual;
- номер manual page;
- начало текущего блока из десяти страниц.

Кнопки:

- `A`, `Q`, `1..10`;
- `<`/`>` — последовательный переход;
- `<<`/`>>` — предыдущий/следующий блок из десяти manual pages;
- selected и disabled states;
- колесо мыши — опционально после основной keyboard/mouse-проверки.

Не хранить `TotalPages = 20` как UI-константу. Пустые manual pages не требуют заранее созданных файлов.

### 6.9. Quicksave

Добавить методы в существующий `SaveManager`:

- quick save: сдвинуть `quick_05→quick_06` … `quick_01→quick_02`, затем записать quick_01;
- quick load: загрузить первый валидный quick slot;
- при успехе показать короткий toast;
- quicksave запрещён в Main Menu и во время неподходящих modal/transition состояний;
- F5 — quick save, F9 — quick load;
- кнопки Quick Save/Quick Load позже используют те же методы.

Перед quick load из игры показывать подтверждение. Из Main Menu отдельной quick-load кнопки не требуется; Q-страница Load достаточна.

### 6.10. Autosave

Не копировать счётчик «200 interactions» буквально: в Unity нет идентичного понятия взаимодействия Ren'Py.

Минимальные безопасные checkpoints:

1. после подтверждённого выбора и применения `GameState`;
2. после перехода в новый `DialogueSceneData` и показа первой строки;
3. перед подтверждённым возвратом в Main Menu;
4. перед in-game загрузкой manual/quick slot, чтобы сохранить теряемый прогресс.

Autosave выполняется, только если `SettingsManager.Instance.settings.autoSave == true`. Кольцо сдвигается так же, как quick. Не запускать autosave:

- в Main Menu;
- во время применения загружаемого save;
- в New Game reset до первой валидной строки;
- одновременно с другим save operation;
- если `sceneId`/`lineId` ещё невалидны.

Нужен простой reentrancy guard в существующем `SaveManager`, не новый scheduler/manager.

### 6.11. Перезапись и удаление

#### Перезапись

- Определять occupied до открытия dialog.
- Не менять файл до Yes.
- No/Escape закрывает dialog и оставляет slot без изменений.
- После успеха обновить только видимые metadata/preview.

#### Удаление

- Добавить `DeleteSlot(slotType, slotIndex)` в существующий `SaveManager`.
- Удалять JSON, PNG, `.bak` и `.tmp` этого слота.
- Удаление доступно только occupied slot.
- Перед удалением — confirm.
- Для desktop можно поддержать Delete, но нужна также видимая кнопка/контекстное действие для discoverability и touch.

Последний пункт — осознанное улучшение относительно Eternum, где delete скрыт за клавишей.

### 6.12. Continue

How I Fall уже имеет Continue, поэтому его сохраняем как полезное расширение над фактическим main menu Eternum.

Алгоритм:

1. Просканировать manual/auto/quick и legacy только во время миграции.
2. Для каждого кандидата прочитать header/JSON и выполнить migration без применения к `GameState`.
3. Проверить schema, `sceneId` в registry и возможность восстановить line ID или fallback index.
4. Отбросить corrupt/incompatible candidates.
5. Выбрать newest valid по `createdAtUtc`; mtime использовать только для legacy.
6. Если кандидата нет — Continue disabled или показывает «Нет совместимых сохранений».
7. Если есть — применить snapshot, установить `hasLoadedSave`, загрузить `VNPrototype` через `LoadLoadedGameScene`.

Не вызывать `StartNewGame` после Continue.

### 6.13. Восстановление сцены, строки, выбора и GameState

Load должен быть транзакционным:

1. `SaveManager` читает и мигрирует данные во временный объект.
2. Проверяет `DialogueSceneData` через `DialogueSceneRegistry`.
3. Сначала ищет `currentLineId`.
4. Если ID отсутствует только в старом save — использует `currentLineIndex` в допустимых границах.
5. Проверяет `selectedChoiceIndex` и `pendingNextSceneId`.
6. Только после полной проверки применяет все поля к `GameState` одним шагом.
7. Ставит `hasLoadedSave = true`.
8. Из Main Menu загружает `VNPrototype`; `VNDialogueController.Start` видит loaded state и вызывает restore вместо line 0.
9. Из VNPrototype можно восстановить in-place через существующий `RestoreLoadedSaveFromPanel`, затем закрыть панель.
10. После успешного restore сбросить transient-флаг так, чтобы следующий обычный вход не повторял старую загрузку; сам restored state не сбрасывать.

Если scene/line/choice невалидны, текущий `GameState` и текущая игра не должны измениться.

### 6.14. Save/Load UI и контексты

#### Из игры

- Save, Load, A/Q/manual pages доступны.
- Save остаётся на панели после записи.
- Load occupied slot требует confirm.
- Back/Escape закрывает панель и возвращает в ту же реплику.
- Dialogue advance блокируется, пока панель открыта.

#### Из Main Menu

- Save disabled/скрыт.
- Load occupied slot выполняется без предупреждения о потере текущего прогресса.
- Back/Escape возвращает в Main Menu.
- Load доступен даже без saves; пользователь видит empty slots.
- Continue зависит только от наличия valid save.

### 6.15. Старые и несовместимые сохранения

Состояния чтения должны различаться:

- `Empty` — файла нет;
- `Loadable` — валидный и совместимый;
- `LegacyLoadable` — мигрируется в памяти;
- `IncompatibleNewerVersion` — создан более новой schema;
- `MissingScene` — `sceneId` отсутствует в registry;
- `MissingLine` — нет ID и безопасного fallback;
- `Corrupt` — JSON/backup не читаются;
- `MissingPreview` — save загружаем, но без thumbnail.

Нельзя показывать incompatible/corrupt slot как обычный empty: пользователь должен понимать, почему файл не загружается.

`applicationVersion` сам по себе не должен блокировать загрузку. Решение принимается по schema migration и существованию контента.

### 6.16. Миграция текущих сохранений How I Fall

Источники legacy:

- `Application.persistentDataPath/save_01.json`;
- `Application.persistentDataPath/Saves/slot_{N}_manual.json`;
- `Application.persistentDataPath/Saves/slot_{N}_auto.json`;
- соответствующие preview и `.bak`.

Одноразовый безопасный порядок:

1. Найти legacy-файлы без удаления.
2. Скопировать их в `Saves/LegacyBackup/` с исходными именами.
3. Прочитать через текущий `TryMigrateToCurrentVersion`.
4. Legacy manual N импортировать в новый manual N.
5. Legacy auto N импортировать в auto ring с сохранением порядка по времени.
6. `save_01.json` трактовать как legacy quick slot 1, потому что текущий код и `QuickSaveStatusView` используют его как compatibility quick-save.
7. Если `save_01.json` полностью совпадает с manual slot 1 по scene/line/savedAt, не создавать дубликат.
8. Абсолютный `previewPath` заменить относительным и скопировать PNG, если он существует.
9. Валидировать scene/line через registry.
10. Невалидный legacy оставить в backup и показать как incompatible, но не выбирать для Continue.
11. После успешного импорта записать небольшой migration marker с version/result; исходники не удалять в первом релизе миграции.

Конкретный legacy `save_01.json` с `ui_test_scene` следует считать loadable только после успешной миграции и полной проверки через текущий registry. Само наличие `ui_test_scene` в registry не отменяет backup и validation-first подход.

### 6.17. Файлы How I Fall, которые потребуется менять при реализации

Обязательные:

- `Assets/HowIFall/Scripts/Save/SaveData.cs`;
- `Assets/HowIFall/Scripts/Save/SaveManager.cs`;
- `Assets/HowIFall/Scripts/UI/MainMenuController.cs`;
- `Assets/HowIFall/Scripts/VN/VNDialogueController.cs`;
- `Assets/HowIFall/Scripts/Core/SceneFlowManager.cs`;
- `Assets/HowIFall/Scenes/MainMenu.unity`;
- `Assets/HowIFall/Scenes/VNPrototype.unity`;
- `Assets/HowIFall/Editor/SaveSystemSmokeTests.cs`;
- `Assets/HowIFall/Editor/MainMenuSceneBuilder.cs`.

Возможные минимальные изменения:

- `Assets/HowIFall/Scripts/Settings/SettingsManager.cs` — только чтение существующего autosave flag из SaveManager или безопасный accessor;
- `Assets/HowIFall/Scripts/UI/SettingsPanelController.cs` — только если нужно немедленно обновлять состояние autosave;
- `Assets/HowIFall/Scripts/VN/DialogueSceneRegistry.cs` — helper проверки ID без изменения модели;
- `Assets/HowIFall/Editor/DialogueContentValidator.cs` — проверка save-critical ID.

UI-prefab сейчас отсутствует. Безопасный вариант первой итерации — синхронно обновить обе сцены и builder. После подтверждения поведения можно вынести одинаковую карточку/панель в один prefab, сохранив существующие публичные классы и ссылки.

### 6.18. Рекомендуемый порядок реализации

#### Подэтап A — данные и чтение без UI

- SaveData v4;
- enum slot type;
- новые пути;
- scan metadata;
- valid/incompatible states;
- migration и tests;
- newest valid для Continue.

#### Подэтап B — операции

- manual save/load;
- overwrite confirm hook;
- delete конкретного слота;
- quick ring;
- auto ring и checkpoints;
- screenshot transaction.

#### Подэтап C — UI parity

- 3 × 2;
- A/Q/manual tabs;
- блоки по 10 страниц;
- naming dialog;
- empty/occupied/incompatible/newest states;
- context differences Main Menu/In Game;
- keyboard/touch/focus verification.

#### Подэтап D — regression

- New Game никогда не загружается поверх Load;
- Main Menu → Load;
- VN → Save → Return;
- VN → Load → confirm → restore;
- quick save/load;
- auto rotation;
- overwrite/delete No/Yes;
- corrupt/current/future/legacy saves;
- отсутствие preview;
- отсутствие saves;
- перезапуск приложения и Continue.

## 7. Критерии готовности будущей реализации

1. Один существующий `SaveManager` обслуживает manual/auto/quick.
2. На странице отображается 6 карточек 3 × 2.
3. Manual, A и Q переключаются без потери page state.
4. Empty/occupied/incompatible визуально различимы.
5. Manual save имеет screenshot, локальную дату и optional user name.
6. Перезапись и удаление невозможно выполнить без подтверждения.
7. Quick и auto циклически сдвигаются без потери newest.
8. In-game Load предупреждает, Main Menu Load — нет.
9. Continue выбирает newest valid, а не просто newest file.
10. После Load восстановлены `sceneId`, `lineId`/fallback, выбор и все поля `GameState`.
11. New Game не запускается поверх loaded state.
12. Legacy current saves либо мигрированы, либо явно показаны несовместимыми и сохранены в backup.
13. При отсутствии saves Load screen остаётся рабочим, а Continue не приводит к пустой VN-сцене.
14. Все paths находятся внутри `Application.persistentDataPath` и не зависят от абсолютного пути старого устройства.

## 8. Что нельзя достоверно определить только по исходным файлам

- Субъективную скорость и плавность hover/transition-анимаций без запуска игры.
- Точное ощущение навигации gamepad/touch и фокус клавиши Delete на каждой платформе.
- Поведение конкретной локализации всех подписей, если перевод переопределён скомпилированными translation-файлами.
- Поведение облачной синхронизации и конфликтов между устройствами в конкретном магазине/лаунчере.
- Реакцию пользовательского интерфейса на специально повреждённый `.save` без runtime-теста.
- Было ли отсутствие Continue намеренным UX-решением или просто неиспользованной возможностью Ren'Py.
- Является ли несоответствие 10 auto/quick-файлов и 6 видимых карточек намеренным архивом или ошибкой экрана.
- Фактическую работоспособность текущей How I Fall в Unity после незакоммиченных изменений: Unity Editor на этом шаге не запускался.

## 9. Итог

Целевой перенос не требует новой архитектуры: существующие `SaveManager`, `SaveData`, `GameState`, `SceneFlowManager`, `VNDialogueController` и `SaveLoadPanelController` уже образуют нужный контур. Нужны расширение типов слотов, транзакционная валидация до применения state, реальная ротация auto/quick, подтверждения, миграция legacy и перестройка UI с 8 на 6 карточек.

Наиболее опасная часть — не внешний вид, а порядок Load: файл должен быть мигрирован и проверен по registry до изменения `GameState`; затем `VNPrototype` должен открыться без вызова New Game. Наиболее заметная UX-часть — 3 × 2, A/Q/manual pages, screenshot/date/name, empty/occupied states и подтверждения.

Реализация по этому документу не начиналась.
