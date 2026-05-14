# 7. Нефункциональные требования

[← Назад к оглавлению](index.md) | [← Безопасность](06-security.md)

---

## 7.1. Платформа

| Параметр | Значение |
|----------|----------|
| ОС | Windows 10/11 |
| Framework | .NET 10 |
| UI Framework | Avalonia 12.0.3 |
| Язык | C# 14 |

---

## 7.2. Производительность

| Параметр | Требование |
|----------|------------|
| Количество шагов | до 1000 |
| Количество параметров | до 30 |
| Время открытия рецепта | < 1 сек |
| Время записи в ПЛК | < 5 сек (полный рецепт) |
| Частота обновления статуса | 100-500 мс |

---

## 7.3. Надёжность

- Приложение не должно терять данные при сбоях связи с ПЛК.
- Автосохранение редактируемого рецепта.
- Восстановление состояния после аварийного завершения.

---

## 7.4. Логирование и диагностика

### Уровни логирования

| Уровень | Описание |
|---------|----------|
| Error | Критические ошибки |
| Warning | Предупреждения, нештатные ситуации |
| Information | Ключевые операции |
| Debug | Детальная диагностика |

### Логируемые события

- Операции с ПЛК (подключение, чтение, запись)
- Чтение/запись рецептов (файлы, ПЛК)
- Ошибки валидации
- Действия пользователей
- Исключения и сбои

### Хранение логов

- Библиотека: Serilog
- Путь к файлу: `C:\DISTR\Logs\semistep.log`
- Ротация по размеру: 5 МБ на файл, до 5 файлов хранится одновременно
- Шаблон записи (един для консоли и файла):
  `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}`
- Возможность отправки в централизованную систему (опционально)

---

## 7.5. Миграция конфигурации (Round-2 cleanup)

При обновлении на версию после архитектурной чистки Round-2 учитывать следующие изменения формата YAML-конфигов:

- Поля `plc_data_type:` в `ConfigFiles/columns/columns.yaml` молча игнорируются (тип `PlcDataType` удалён).
  Записи можно безопасно удалить — наличие не вызывает ошибок, но и не имеет эффекта.
- Колонки с `column_type: step_start_time_field` должны явно объявлять `read_only: true` в `ConfigFiles/columns/columns.yaml`.
  Без этого ячейка отрисуется как `Disabled` (раньше эта ветка определялась по `ColumnType`).

### Round-3 (DeployDuration / fail-loud loaders / connection versions)

- Поле `connection_protocol` сменило семантику: теперь это версия реализации PLC-драйвера (строка `"1.0"`).
  Прежнее значение `"S7"` отклоняется жёстко с сообщением «Unsupported connection_protocol». Будущие версии S7-драйвера будут сосуществовать с текущей.
- Поле `connection_file_version` теперь строго проверяется на равенство `"1.0"`. Отсутствие или несоответствие приводят к отказу загрузки с явным сообщением.
- Загрузчики конфигурации больше не маскируют повреждённые файлы дефолтами:
  - `ConnectionLoader`: жёстко падает на всех трёх ветках (отсутствует каталог / отсутствует файл / ошибка парсинга), включая имя файла и текст ошибки в сообщении.
  - `GridStyleLoader`: ошибка парсинга присутствующего файла приводит к отказу; отсутствующий каталог/файл по-прежнему трактуется как «без кастомных стилей» (косметическая опция).
- Регрессия `DeployDuration` исправлена: действия с `deploy_duration: immediate` больше не вносят вклад в кумулятивное время рецепта.
  Поведенческое изменение для рецептов, которые (непреднамеренно) полагались на прежнее суммирование — итоговая длительность таких рецептов уменьшится.
- ⚠️ **Развёрнутый файл `C:\DISTR\Config\Semistep\connection\connection.yaml` ДОЛЖЕН быть мигрирован
  перед обновлением:** заменить `connection_protocol: S7` на `connection_protocol: "1.0"`. Без миграции загрузчик откажется стартовать.

### Round-4 (Avalonia 11 → 12 миграция)

- Все пакеты семейства Avalonia подняты с `11.3.13` до `12.0.2` (DataGrid — до `12.0.0`).
  Транзитивно подтянут `Tmds.DBus.Protocol 0.21.3`, закрывающий security-advisory `NU1903`.
- Пакет `Avalonia.Diagnostics` полностью удалён — в Avalonia 12 он заменён на `AvaloniaUI.DiagnosticsSupport`.
  Поскольку код не вызывал `AttachDevTools()`, F12-инструментарий разработчика никогда не был задействован,
  и замена не добавлена — лишний пакет не нужен. Если кому-то понадобится dev tools, добавить
  `AvaloniaUI.DiagnosticsSupport` и вызов `AttachDeveloperTools()` в `App.axaml.cs`.
- API клипборда изменился: `IClipboard.GetTextAsync()` → `IClipboard.TryGetTextAsync()`
  (та же сигнатура `Task<string?>`, без поведенческих отличий). Затронут один call-site
  (`SemiStep.UI/Clipboard/ClipboardViewModel.cs`); `ClipboardSerializer` (Core) не использует
  Avalonia clipboard, его не трогали.
- Compiled bindings включены по умолчанию в Avalonia 12; в `.axaml` файлах добавлены `x:DataType`
  где требовалось для XAML-компиляции. Loose bindings больше не допустимы.
- Безопасные сопутствующие bump'ы пакетов: YamlDotNet `16.3.0 → 17.1.0`,
  System.Reactive `6.0.1 → 6.1.0`, Microsoft.NET.Test.Sdk `18.3.0 → 18.5.1`,
  Microsoft.Extensions.DependencyInjection / Abstractions `10.0.5 → 10.0.7`,
  System.IO.Hashing `10.0.5 → 10.0.7`.
- UI-тесты под headless-режимом Avalonia 12 потребовали правок dispatcher-плана
  (`SemiStep.Tests/Helpers/HeadlessDispatcher.cs` + три фикстуры с асинхронными
  `Dispatcher.UIThread.RunJobs`-вызовами; удалён в Round-5). Поведение под windowed-runtime не меняется.
- Пакет `Avalonia.ReactiveUI` на момент Round-4 оставался на `11.3.8` — его 12.x не публикуется,
  пакет помечен deprecated. Полноценная миграция на преемника (`ReactiveUI.Avalonia`) выполнена
  в Round-6.

### Round-5 (xUnit v3 миграция и чистка тестов)

- Тестовый проект `SemiStep.Tests` мигрирован с xUnit v2 (`xunit 2.9.3`) на xUnit v3 (`xunit.v3 3.2.2`)
  с подключением пакета `Avalonia.Headless.XUnit 12.0.3` (потребовал форс-бамп `Avalonia.Headless`
  с `12.0.2` до `12.0.3` ради транзитивного ограничения). `xunit.runner.visualstudio` оставлен
  на `3.1.5` — уже v3-совместим.
- **Корневая причина починена структурно.** Под Avalonia 12 `Dispatcher.RequestForegroundProcessing`
  бросает NRE при любой попытке `Dispatcher.UIThread.Post(...)` вне session-scoped dispatcher'а.
  Прежняя обёртка `HeadlessDispatcher.Run(...)` оборачивала только тело теста, но не
  `IAsyncLifetime.InitializeAsync` / `DisposeAsync` и не конструктор тест-класса — что и приводило
  к детерминированному NRE в `RecipeMutationCoordinatorTests` и периодическим зависаниям остальных
  UI-тестов. Атрибут `[AvaloniaFact]` (из `Avalonia.Headless.XUnit`) оборачивает **весь жизненный
  цикл теста** — конструктор, `InitializeAsync`, тело, `DisposeAsync` — в headless-диспетчере,
  устраняя проблему на уровне инфраструктуры.
- Хелпер `SemiStep.Tests/Helpers/HeadlessDispatcher.cs` удалён вместе со всеми вызовами
  (`MessagePanelViewModelTests` — 26 сайтов, `RecipeMutationCoordinatorLoadRecipeTests` — 6,
  `RecipeMutationCoordinatorTests` — 2). Тела тестов восстановлены как плоский `async Task` без
  sync-over-async обёрток.
- Атрибут `[assembly: CollectionBehavior(DisableTestParallelization = true)]` добавлен
  **постоянно**, не как временная мера: `HeadlessUnitTestSession` существует в единственном
  экземпляре на процесс и не переносит параллельного использования между коллекциями. Параллельный
  прогон тестов в будущем — отдельная задача (per-collection sessions), не флаг.
- Чистка тестов: 316 → 307. Сжаты Theory-консолидации в `MessagePanelViewModelTests`
  (default-state факты), `RecipeRowViewModelTests` (`ColumnUnits_*`, `ColumnFormatKinds_*`)
  и `CorePropertyStateTests`; удалены тривиальные одиночные assertion'ы. Более глубокое сжатие
  отклонено сознательно — оставшиеся факты покрывают семантически разные setup-пути.
- Все `Task.Delay(N)`-опросы наблюдаемого состояния (11 сайтов в `PlcLifecycleManagerReconnectTests`,
  `S7ServiceTests`, `PlcSyncCoordinatorTests`, `PlcExecutionMonitorTests`) заменены на
  `TestHelpers.WaitUntilAsync(predicate, timeout, pollInterval, CancellationToken)` — predicate-based
  ожидание с монотонным `Stopwatch` и поддержкой `CancellationToken`. Две `Task.Delay` оставлены
  с пояснительными комментариями: один — окно измерения частоты опроса (SUT-внутренний таймер),
  второй — defensive-settle для негативного утверждения «событие не произошло».
- Все предупреждения `xUnit1051` (`CancellationToken` не пробрасывается в `await`-вызовы)
  устранены — суммарно 19 → 0 за счёт явной передачи `TestContext.Current.CancellationToken`.
- Финальный прогон: 307/307 зелёные, ~8–10 секунд на чистой машине, детерминированно в 3 запусках
  подряд.
- Пакет `Avalonia.ReactiveUI` на момент Round-5 ещё оставался на `11.3.8` (deprecated) — миграция
  на преемника `ReactiveUI.Avalonia` выполнена в Round-6.

### Round-6 (миграция Avalonia.ReactiveUI → ReactiveUI.Avalonia)

- Пакет `Avalonia.ReactiveUI 11.3.8` (поддерживался командой Avalonia, помечен deprecated на NuGet)
  заменён на его преемника `ReactiveUI.Avalonia 12.0.1` (поддерживается командой ReactiveUI;
  обратите внимание на перестановку слов в имени пакета). Совместим с Avalonia 12.0.2.
- Изменение namespace: `using Avalonia.ReactiveUI;` → `using ReactiveUI.Avalonia;` в трёх файлах
  (`SemiStep.UI/App.axaml.cs`, `SemiStep.UI/MainWindow/MainWindow.axaml.cs`,
  `SemiStep.Tests/TestAppBuilder.cs`).
- **Скрытые breaking changes** при апгрейде транзитивно подтянутого `ReactiveUI 23.x`:
  - Тип `RxApp` удалён, заменён на `RxSchedulers` (тот же namespace `ReactiveUI`).
    Затронуто 7 ViewModel'ей: `MainWindowViewModel`, `RecipeCommandsViewModel`,
    `RecipeGridViewModel`, `RecipeFileViewModel`, `ClipboardViewModel`, `PlcMonitorViewModel`,
    `RecipeMutationCoordinator` — ~18 замен `RxApp.MainThreadScheduler` → `RxSchedulers.MainThreadScheduler`.
  - `DisposeWith(IDisposable, CompositeDisposable)` extension переехал в namespace
    `System.Reactive.Disposables.Fluent` (из `System.Reactive.Disposables`). Добавлен новый
    `using System.Reactive.Disposables.Fluent;` в 8 файлах, использующих `.DisposeWith(disposables)`.
  - `AppBuilder.UseReactiveUI()` теперь требует обязательный параметр `Action<ReactiveUIBuilder>`.
    Заменено на `.UseReactiveUI(_ => { })` в `App.axaml.cs:58` и `TestAppBuilder.cs:19`.
- Прогон тестов после миграции: 307/307 зелёные, поведение не изменилось.

### Round-7 (scroll-perf фиксы и патч-бампы пакетов)

- **Массовые патч-бампы пакетов.** Семейство Avalonia поднято с `12.0.2` до `12.0.3`
  (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Win32`,
  `Avalonia.HarfBuzz`). Семейство `Microsoft.Extensions.*` унифицировано на `10.0.8`
  (`Microsoft.Extensions.DependencyInjection` / `.Abstractions` c `10.0.7`,
  `Microsoft.Extensions.Logging` / `.Abstractions` c `10.0.5` — устранён трёхпатчевый
  отставание). `FluentAssertions` поднят `8.9.0 → 8.10.0`, `System.IO.Hashing`
  `10.0.7 → 10.0.8`. `Serilog.Extensions.Logging` обновлён `9.0.2 → 10.0.0` —
  major-бамп ради выравнивания с MEL10, фактического API-слома нет.
  `Avalonia.Controls.DataGrid` оставлен на `12.0.0` (свежее не опубликовано),
  `S7netplus 0.20.0` — последний релиз 2023 года.
- **Утечка strong-ref в ComboBox-шаблоне action-колонки исправлена.** В
  `ComboBoxCellFactory` шаблон ячейки действия использовал
  `comboBox.SelectionChanged += (_, _) => { ... row.SetPropertyValue(...) }`. Лямбда
  захватывала `row` и никогда не отписывалась: ComboBox удерживал `row` через
  делегат, `row` удерживал ComboBox через event-handler. При прокрутке грида
  накапливались осиротевшие ComboBox'ы. Подписка заменена на `SelectedItem`
  TwoWay-биндинг через `ComboBoxItemSelectionConverter` — жизненным циклом подписки
  управляет Avalonia, цикл сильных ссылок исчезает.
- **Re-entry guard в `MainWindow.BuildGrid`.** Добавлено поле `_columnsBuilt` в
  `MainWindow.axaml.cs`: повторные вызовы `BuildGrid()` (например, при повторном
  срабатывании `WhenActivated` в сценариях модальных окон / hide-show) возвращаются
  немедленно, не пересоздавая колонки через `ColumnBuilder.BuildColumns`. Не утечка
  по сути, но устраняет ненужную gen-0 churn от пересоздания шаблонов и
  конвертеров. Добавлен идемпотентный тест на `ColumnBuilder.BuildColumns`.
- **Allocation-hotspots в конвертерах сглажены.**
  В `PropertyTimeMultiConverter.Convert` LINQ-проход
  `values.Any(v => v == AvaloniaProperty.UnsetValue)` заменён на индексированный
  `for`-цикл — устраняет аллокацию enumerator'а на каждую оценку привязки ячейки;
  `ToString()` пропускается, если значение уже `string`.
  В `TextCellFactory.CreateDisplayTemplate` для ключа `StepStartTimeColumnKey`
  добавлен короткий путь: простой `Binding(nameof(RecipeRowViewModel.StepStartTime))`
  напрямую к `TextBlock.Text` без обёртки `MultiBinding` + `PropertyTimeMultiConverter`.
  Значение `StepStartTime` уже предформатировано во view-model — повторное форматирование
  через конвертер было избыточным.
- **Round-8 follow-up.** Полное включение `supportsRecycling: true` в шаблонах
  editing/combo-ячеек требует переписывания замыканий по `row` в чистые биндинги.
  XAML-шаблоны `<DataTemplate x:DataType="vm:RecipeRowViewModel" x:CompileBindings="True">`
  ради compiled-bindings (вместо текущих reflection-based `new Binding(path)` из кода) —
  отдельный объём работ. Manual scroll smoke с реальным рецептом ≥100 шагов остаётся
  обязательной верификацией перед открытием PR (в headless не автоматизируется).

### Round-8 (ComboBox CellTemplate миграция + включение recycling)

- **Регрессия Avalonia 12: `CellEditingTemplate` + ComboBox не работает.** После апгрейда
  Avalonia 11 → 12 (Round-4) ComboBox внутри `DataGridTemplateColumn.CellEditingTemplate`
  перестал реагировать на клики — popup не открывается ни при первом, ни при повторных
  кликах по ячейке. Причина задокументирована в `AvaloniaUI/Avalonia.Controls.DataGrid#236`
  и закрыта без апстрим-фикса. Промежуточный воркараунд из `dd0d7f7` (синхронная установка
  `SelectedItem` в editing-template) снимал NRE при первом клике, но не решал основную
  проблему — popup всё равно не открывался. Единственный рабочий паттерн в Avalonia 12,
  подтверждённый официальными сэмплами (`NumericUpDown` в `CellEditingTemplate` — да,
  ComboBox — нет), avalonia-docs и community-обсуждениями `#7086`/`#14103`, — это
  размещение интерактивных ComboBox'ов **прямо в `CellTemplate`** при выставленном
  `IsReadOnly = true` на колонке. Ячейка не проходит edit-mode lifecycle DataGrid'а;
  ComboBox живёт в визуальном дереве с момента материализации строки, владеет своими
  pointer-events и открывает popup на первом клике.
- **Миграция шаблонов action/group ComboBox.** В `ComboBoxCellFactory` четыре шаблона
  (display + edit × action + group) свёрнуты в два (`CellTemplate` × action + group).
  Колонки помечены `IsReadOnly = true`, `CellEditingTemplate` снят. Шаблоны построены
  как `FuncDataTemplate<RecipeRowViewModel>` без захвата `row` в замыкание — все
  per-row данные текут через биндинги к свойствам `DataContext`.
- **Включение `supportsRecycling: true` — закрытие Round-7 follow-up.** Round-7
  оставил `supportsRecycling: false` на пяти шаблонах из-за замыканий на `row`
  (resolving action id, group items list, write-back lambda). Round-8 структурно
  устранил эти замыкания:
  - Action items живут как глобальный кэш `_cachedActionItems` в инстансе
    `ComboBoxCellFactory` — одинаковы для всех строк, лямбда захватывает константу,
    не строку. `InvalidateCaches()` вызывается **только** из `ColumnBuilder.BuildColumns`
    сразу после `grid.Columns.Clear()`; очистка колонок уничтожает все материализованные
    ячейки, переиспользованной ячейки со stale-кэшом не существует.
  - Group items вынесены из фабрики в view-model: `RecipeRowViewModel.GroupItemsByColumn`
    (`IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>>`) строится
    однократно в конструкторе строки рядом с `BuildColumnMetadata`. Action для строки
    фиксирован на её время жизни (`MutationSignal.StepActionChanged` всегда пересоздаёт
    строку через `RecipeGridViewModel.RebuildRow`), поэтому словарь иммутабельный.
    ComboBox в group-шаблоне биндит `ItemsSource` к пути `GroupItemsByColumn[<key>]`.
  - Для group-колонок введён `ComboBoxItemMultiSelectionConverter` (`IMultiValueConverter`):
    source 0 — int id из `[<columnKey>]`, source 1 — список айтемов из
    `GroupItemsByColumn[<columnKey>]`, прямое преобразование возвращает совпавший
    `ComboBoxItemViewModel`, обратное — `[item.Id, BindingOperations.DoNothing]`.
    Action-колонка осталась на одиночном `ComboBoxItemSelectionConverter` — у неё
    список айтемов глобален.
  - Сознательная регрессия аллокаций: ранее `_groupItemsByGroupName` кэш в фабрике
    шарил списки между строками с одинаковым action. Теперь каждая строка строит
    собственный — группы малы (<20 элементов), компромисс оправдан устранением
    замыканий ради recycling. Если профилирование покажет горячую точку, кэш
    реинтродуцируется на уровне registry (проекция — чистая функция от
    `ActionDefinition` и group-данных).
- **Disable-state через `IsHitTestVisible`, не `IsEnabled`.** Для read-only/disabled
  ячеек ComboBox биндит `IsHitTestVisible` на `MultiBinding` (`HitTestVisibleMultiConverter`)
  с двумя источниками: `CellStates[<columnKey>]` проектируется в bool через cell-state →
  bool логику (Enabled → true), и `DataGrid.IsReadOnly` через `RelativeSource = FindAncestor`
  с инверсией. Третий фактор — статический `isColumnReadOnly` — короткозамыкается на
  константный `false`-биндинг при `true` (без MultiBinding'а вовсе). Выбор
  `IsHitTestVisible` вместо `IsEnabled` сделан сознательно: Fluent-тема Avalonia
  выкрашивает `IsEnabled = false` в серый — это визуальный регресс UX относительно
  master. `IsHitTestVisible = false` сохраняет текущий рендер ComboBox'а, игнорируя
  только клики. Полностью disabled-ячейки скрывают ComboBox целиком через стиль
  `:cell-disabled > ComboBox { IsVisible: false }` в `Styles/DataGridStyles.axaml` —
  без изменений.
- **Headless ограничение.** Avalonia.Headless не симулирует hit-testing, поэтому
  каноническое наблюдаемое — «клик по ячейке → popup открылся с первого раза» — не
  покрывается unit-тестами. Покрытие в Round-8 косвенное: assertion'ы на форму
  колонок (`IsReadOnly == true`, `CellEditingTemplate == null`, `CellTemplate != null`),
  раунд-трип `ComboBoxItemMultiSelectionConverter`, существующие 309 тестов работают
  как regression-сеть для биндинг-семантики (action-change wiring, row rebuild,
  mutation-coordinator flows). Manual scroll/click smoke (7 сценариев из плана —
  клик по action-ячейке, смена action → перестроение строки, клик по group-ячейке,
  disabled-state, RecipeActive read-only, скролл рецепта ≥100 шагов 30 сек) остаётся
  обязательной верификацией перед открытием PR.
- Финальный прогон: 344/344 зелёных, `dotnet format --verify-no-changes` чистый,
  `dotnet build` 0 ошибок 0 предупреждений.

### Round-9 (радикальное упрощение Recipe-стека)

- **Диагностика: 5-subagent аудит архитектуры.** Сессия 2026-05-14 параллельно запустила
  пять архитектурных аудиторов на Recipe-стек. Все пять сошлись на едином диагнозе:
  это «CRUD-приложение в одежде event-sourcing'а» — семь state-классов (`Recipe` →
  `RecipeStateManager` → `RecipeHistoryManager` → `RecipeWorkspace` → `RecipeEditor` →
  `RecipeStepCoordinator` → `RecipeMutationCoordinator`) плюс `RecipeQueryService`
  pass-through, `Subject<MutationSignal> + ObserveOn(MainThreadScheduler)` очередь,
  гоняющаяся с recipe-replace, и императивные `FuncDataTemplate<T>` шаблоны ячеек
  с TwoWay-биндингами, стреляющими mutation-события при каждой подмене DataContext'а
  во время виртуализации. Все наблюдаемые баги (NRE при клике на action-ComboBox,
  signal storm, GC-давление, прыжки выделения, рассинхронизация copy-paste,
  crash при загрузке рецепта) жили на швах между этими слоями.
- **Уплощение state-слоя: 7 классов → 2.** Объединены в `RecipeSession`
  (~410 LOC, `SemiStep.Core/Recipes/`): inlined state из `RecipeStateManager`,
  стеки undo/redo из `RecipeHistoryManager`, мутации из `RecipeEditor`,
  оркестрация и dirty-flag из `RecipeWorkspace`. `RecipeMutationCoordinator` +
  `RecipeStepCoordinator` + `RecipeQueryService` свёрнуты в `RecipeCoordinator`
  (`SemiStep.UI/Coordinator/`) — PLC-фасад + файловые операции + message panel +
  clipboard query forwarding. Раздел `RecipeQueryService` распределён по новым
  владельцам: state-запросы (`CurrentRecipe`, `IsDirty`, `CanUndo`, `CanRedo`,
  `Snapshot`) — на `RecipeSession`; PLC-фасад (`IsConnected`, `IsRecipeActive`,
  `IsSyncEnabled`, `ExecutionState`) и `GetDefaultActionId` — на `RecipeCoordinator`;
  `SerializeStepsForClipboard`/`DeserializeStepsFromClipboard` инлайнены в
  `ClipboardViewModel` с прямой инъекцией `ClipboardSerializer` +
  `ImportedRecipeValidator`.
- **`MutationOutcome.SuggestedSelectionIndex` вместо side-channel.** Прежний
  `ConsumeSuggestedSelection()` consumed-once канал на координаторе устранён.
  Мутации возвращают `Result<MutationOutcome>` с полем `int? SuggestedSelectionIndex`
  для методов, требующих смещения выделения (`AppendStep`, `InsertStep`, `RemoveStep`,
  `RemoveSteps`, `InsertSteps`, `ChangeStepAction`); методы без перестановки строк
  (`UpdateStepProperty`, `Undo`, `Redo`, `NewRecipe`, файловые операции) возвращают
  тот же `MutationOutcome` с `SuggestedSelectionIndex = null`. Тип Coordinator-мутаций
  единый — `Result<MutationOutcome>` — упрощает caller-логику и устраняет два хелпера
  `Track`/`TrackVoid` (один остался — `Track`).
- **Канал сигналов: `Subject<MutationSignal> + ObserveOn` → синхронные вызовы
  `IRecipeSink.OnMutation` через Attach-паттерн.** Прежний `_stateChanged` Subject
  на `RecipeCoordinator` с `ObserveOn(MainThreadScheduler)` на стороне
  `RecipeGridViewModel` устранён полностью. Введён интерфейс `IRecipeSink` с одним
  методом `void OnMutation(MutationSignal signal)`. `RecipeGridViewModel` реализует
  `IRecipeSink` (метод `OnStateChange` переименован в `OnMutation`, сделан public);
  `RecipeCoordinator.Attach(IRecipeSink sink)` сохраняет ссылку, все
  `_stateChanged.OnNext(...)` сайты заменены на один приватный
  `DispatchMutation(signal)` хелпер, вызывающий `_sink?.OnMutation(signal)`
  синхронно в том же стек-фрейме мутации. Wiring двух-фазный: DI собирает
  `RecipeSession`, `RecipeCoordinator`, `RecipeGridViewModel` независимо
  (никто не принимает `IRecipeSink` в ctor); `App.InitializeServices` после
  `coordinator.Initialize()` резолвит grid VM из DI и вызывает
  `coordinator.Attach(gridViewModel)`. Сохранён плоский C# `event Action? Mutated`
  на координаторе для не-grid консьюмеров (`MainWindowViewModel`,
  `RecipeCommandsViewModel`), рефрешащих `CanExecute` команд — fire синхронно после
  sink-dispatch, без Rx ObserveOn. `Avalonia.Threading.Dispatcher.UIThread.VerifyAccess()`
  в начале `OnMutation` — release-time tripwire от случайных вызовов с фоновых
  потоков. Generation-tag сознательно не добавлен (YAGNI): синхронные вызовы
  делают drift невозможным; при появлении async-пути тег вводится тогда.
- **PLC-каналы: `ObserveOn` поднят на координатор + `Publish().RefCount()`.**
  Раньше каждый subscriber (`RecipeGridViewModel`, `PlcMonitorViewModel`,
  `MainWindowViewModel`) ставил собственный `ObserveOn(MainThreadScheduler)` хоп
  на `ExecutionState` / `PlcStateChanged` / `PlcRecipeConflictDetected`.
  Теперь `RecipeCoordinator` применяет `ObserveOn(MainThreadScheduler).Publish().RefCount()`
  один раз для каждого канала; subscriber-стороны pure-subscribe без хопа.
  Аудит подтвердил, что все три канала single-producer-per-channel
  (`PlcExecutionMonitor._subject` — один `Subject<PlcExecutionInfo>` за одним
  `PollLoopAsync`; `PlcSyncCoordinator._subject` — `BehaviorSubject` с `OnNext`
  под `_lock`; `PlcRecipeConflictDetected` — event, raised только из
  `PerformReconnectReconciliationAsync` continuation).
- **Стабилизирующие фиксы:**
  - Equal-value guard в `RecipeRowViewModel.SetPropertyValue`. Перед эмиссией
    `ActionChanged` сравнивается `int.TryParse(value) == _step.ActionKey`; перед
    эмиссией `PropertyValueChanged` сравнивается текущее значение через
    `GetPropertyValue(columnKey)` ordinal-strings. Гасит phantom-mutation шквал
    от TwoWay-биндинг refresh'ей во время виртуализации до момента, пока
    Round-10 не уберёт сам шов структурно.
  - Defensive bounds в `RecipeGridViewModel.OnMutation` обработчиках
    (`UpdateSingleRowInPlace`, `RebuildRow`, `AppendRow`, `InsertRows`):
    проверка `stepIndex < recipe.StepCount` и `< RecipeRows.Count` перед
    индексацией. Out-of-range → `_logger.LogWarning(...)`, ранний return.
    Защита от устаревших сигналов на случай race'ов между recipe-replace и
    in-flight обновлениями строки (с переходом на синхронные `IRecipeSink`
    вызовы race структурно невозможен, но bounds-проверка оставлена как
    дешёвая страховка от будущих регрессий).
  - Selection wiring single-writer. `SelectedIndex="{Binding RecipeGrid.SelectedRowIndex}"`
    TwoWay-биндинг в `MainWindow.axaml:31` удалён. `RecipeGridViewModel.SelectedRowIndex`
    стал computed-свойством `SelectedRowIndices.Count > 0 ? SelectedRowIndices[0] : -1`
    без сеттера. Императивный `MainWindow.OnSelectionChanged` handler — единственный
    писатель `SelectedRowIndices`. Для случаев, когда мутация требует сдвига выделения
    (paste/insert/append/remove/change-action), `RecipeCoordinator` возвращает
    `MutationOutcome.SuggestedSelectionIndex`; consumer вызывает
    `RecipeGridViewModel.RequestSelection(idx)`, который raises `SelectionRequested`
    event; code-behind в `MainWindow.axaml.cs` подписан и пишет напрямую
    в `DataGrid.SelectedIndex`. Устраняет гонку двух писателей.
  - Error window via `Program.Main` restructure. Прежний `App.Run` →
    `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)` → если падает,
    `App.RunErrorWindow` вызывал `BuildAvaloniaApp()` повторно (Setup уже отработал →
    crash). Переструктурировано: `Program.Main` валидирует стартовое состояние
    через `ValidateStartup()` (config-файлы, registry) ДО любого `BuildAvaloniaApp()`
    вызова; если ошибки — только `App.RunErrorWindow(errors)`; если без ошибок —
    только `App.Run(serviceProvider)`. Каждая ветка дёргает
    `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)` ровно один раз
    за процесс. Введён `StartupOutcome` record struct, делающий
    Provider-XOR-Errors инвариант явным. Catch-fallback в `App.Run`,
    повторно вызывавший `BuildAvaloniaApp()`, удалён.
- **Откат `supportsRecycling: true` на всех Round-8-flipped шаблонах.** Phantom-mutation
  hazard от Round-8 закрывается **только** структурно (Round-10, XAML compiled
  bindings). Промежуточная мера: вернуть `supportsRecycling: false` на всех
  пяти шаблонах, на которые Round-8 поставил `true` (либо подразумевал «безопасно
  true» по Round-7-архиву):
  - `ComboBoxCellFactory.CreateActionCellTemplate` (line 77) → `false`.
  - `ComboBoxCellFactory.CreateGroupCellTemplate` (line 112) → `false`.
  - `TextCellFactory.CreateStepStartTimeTemplate` (line 64) → `false`.
  - `TextCellFactory.CreateMultiBindingTemplate` (line 104) → `false`.
  - `TextCellFactory.CreateEditingTemplate` (line 137) — уже был `false`, не трогали.
  Восстановлен factory-level `_groupItemsByGroupName` кэш в `ComboBoxCellFactory`
  (после Round-8 он был перенесён в `RecipeRowViewModel.GroupItemsByColumn`).
  `InvalidateCaches()` очищает обе action+group мапы; вызывается из
  `ColumnBuilder.BuildColumns` сразу после `grid.Columns.Clear()`. В group-шаблоне
  lambda резолвит items список из factory-кэша при build-time (повторно
  захватывает `row` для `row.GetGroupNameForColumn(columnKey)` — безопасно при
  `supportsRecycling: false`). `RecipeRowViewModel.GroupItemsByColumn` удалён;
  `GetGroupItemsForColumn` тоже не нужен — items живут в factory-кэше. Удалён
  `ComboBoxItemMultiSelectionConverter` (multi-binding больше не нужен —
  один-биндинг `ComboBoxItemSelectionConverter` достаточен с closure-captured
  items списком). Удалён `ColumnTypes.GroupItemsPath` (на него больше никто
  не биндился). Сознательная регрессия аллокаций до Round-10 — за гарантию
  отсутствия phantom-mutation'ов в TwoWay-биндингах ComboBox'ов.
- **Удалён мёртвый код:**
  - `SemiStep.Core/Recipes/Formulas/` директория (шесть файлов: `CompiledFormula.cs`,
    `FormulaApplicationCoordinator.cs`, `FormulaDefinition.cs`, `FormulaEngine.cs`,
    `StepAdapterResult.cs`, `StepVariableAdapter.cs`). Аудит подтвердил единственную
    production-точку входа — `RecipeSession.UpdateStepProperty`, передававший
    `formulaDefinition: null` всегда. Branch был unreachable в runtime; тестов
    под `SemiStep.Tests/Core/Formulas/` не существовало. DI-регистрация
    (`CompiledFormula`-словарь, `FormulaEngine`, `FormulaApplicationCoordinator`)
    удалена из `RecipeDi.cs`. `RecipeSession.UpdateStepProperty` инлайнен:
    `updatedStep` используется напрямую вместо routing'а через `ApplyIfExists`.
  - `MutationSignal.MetadataChanged` вариант discriminated union'а. Единственный
    emitter был в `RecipeCoordinator.SaveRecipeAsync` (line ~311 до Task 13);
    единственный consumer в `RecipeGridViewModel.OnStateChange` был `break;`
    плюс indirection через `ApplyPostMutationUpdates`, существовавший только
    чтобы пропустить `RefreshStepStartTimes()` для этого варианта. Удалены:
    эмиттер, кейс в свитче, indirection-метод; `RefreshStepStartTimes()`
    зовётся безусловно после остальных сигналов; save-операция теперь не
    шлёт сигнал вовсе (state не изменился). Удалён `default: FullRebuild`
    fallback в `RecipeGridViewModel.OnMutation` switch — все семь оставшихся
    `MutationSignal` вариантов покрыты явно.
- **Структурированное логирование на ключевых границах.** Инжектированы
  `ILogger<RecipeSession>`, `ILogger<RecipeCoordinator>`, `ILogger<RecipeGridViewModel>`
  (последний из Task 2 уже был). Уровни:
  - `Information`: recipe load/save/reset/undo/redo на `RecipeSession`,
    mutation entry per public mutation method на `RecipeSession` (`AppendStep`,
    `InsertStep`, `RemoveStep`, `RemoveSteps`, `InsertSteps`, `ChangeStepAction`,
    `UpdateStepProperty`), file ops на `RecipeCoordinator`, PLC sync enable/disable,
    mutation dispatch (один per signal kind), signal arrival на
    `RecipeGridViewModel` (один per signal kind), PLC state change (один per
    реальный transition — backed by `BehaviorSubject` в источнике, не per
    poll-tick).
  - `Warning`: bounds-check drops на `RecipeGridViewModel` (Task 2), failed
    file/PLC operations, failed recipe analysis в `RecipeSession.LoadAsCurrent`,
    failed PLC state results в `RecipeCoordinator.OnPlcStateChanged`.
  - `Error`: уже присутствовал на `RecipeCoordinator.SaveRecipeAsync` для save
    failure (оставлен).
  - **Debug-уровневое per-cell логирование сознательно не вводилось.** Ближайший
    per-edit лог — `Mutation entry: UpdateStepProperty`, fires один раз на
    user-committed value (equal-value guard в RowVM из Task 1 short-circuits'ит
    неизменённые writes до того, как они достигают session-слоя).
  - Все сообщения — structured logs с именованными placeholder'ами
    (`{StepIndex}`, `{ActionId}`, `{StepCount}`, `{ColumnKey}`, `{FilePath}`,
    `{Kind}`, `{ConnectionState}`, `{SyncStatus}`, `{IsSyncEnabled}`,
    `{KeepLocal}`, `{Errors}`) — string-interpolation в log-вызовах не
    используется.
- **Метрики Round-9.** State-слой: 7 классов → 2 (`RecipeSession` +
  `RecipeCoordinator`). Тесты: 351 baseline (Round-8) → 393 после Round-9
  (новые: 33 characterization для `RecipeSession`, ~10 для
  `RequestSelection`/`SelectionRequested` event, ~5 для возвращаемого
  `MutationOutcome.SuggestedSelectionIndex`; удалены: 33 OLD characterization
  для Workspace/Editor, тесты `ComboBoxItemMultiSelectionConverterTests`,
  тесты `Formulas/`). Финальный прогон: 393/393 зелёных,
  `dotnet format --verify-no-changes` чистый, `dotnet build` 0 ошибок 0
  предупреждений.
- **Round-10 deferred.** Структурный фикс binding-seam класса багов
  (per-cell binding weight, supportsRecycling tension, TwoWay-writeback-on-
  DataContext-swap hazard) — миграция императивных `FuncDataTemplate<T>`
  шаблонов на XAML `<DataTemplate>` resources с compiled bindings
  (`x:CompileBindings="True"`, `x:DataType="vm:RecipeRowViewModel"`).
  Сохраняется для Round-10 (`Docs/plans/yyyymmdd-cell-templates-to-xaml.md`,
  создаётся после landing'а Round-9 на master). Round-9 mitigates этот класс
  через equal-value guard + recycling rollback; не устраняет структурно.

---

## 7.6. Локализация

| Язык | Статус |
|------|--------|
| Русский | Основной |
| Английский | Планируется |

---

[← Назад к оглавлению](index.md)
