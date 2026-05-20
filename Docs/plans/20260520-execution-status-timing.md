# Execution Status Timing

## Overview

Реализовать требование `Docs/02-ui-requirements.md` §2.6.3: в статус-баре окна показывать две оценки оставшегося времени.

- **Время до конца текущего шага** = `StepDuration[ActualLine] - StepCurrentTime`.
- **Время до конца рецепта** = `TotalDuration - (StepStartTimes[ActualLine] + LoopOffset + StepCurrentTime)`,
  где `LoopOffset` учитывает уже завершённые итерации активных `For`-блоков.

Шаги без `step_duration` (`For`, `End_For`, операторская пауза, ожидание по условию) автоматически дают `null` через существующий `TimingCalculator.ExtractStepDuration` и в сумму не входят.

Формат вывода:

- `чч:мм:сс` при длительности < 24 ч.
- `d.чч:мм:сс` при длительности ≥ 24 ч (авто-переключение).

Состояния отображения:

- `RecipeActive=true` — вычисленные значения.
- `RecipeActive=false` (включая `IsConnected=false`) — `(00:00:00, TotalDuration)`. Прочерк зарезервирован под «снапшот ни разу не приходил / рецепт не загружен».

## Context (from discovery)

**Что уже есть:**

- `SemiStep.Core/Recipes/Analysis/TimingCalculator.cs` — возвращает `(StepStartTimes, TotalDuration)`. В `StepStartTimes` уже зашита expansion циклов: после `End_For` стартовое время следующего шага сдвинуто на `(Iterations - 1) × SingleIterationDuration`. Внутри расчёта `singleDuration` уже считается (строка 31), но наружу не выходит.
- `SemiStep.Core/Recipes/RecipeSnapshot.cs` — `record struct` с `Recipe`, `TotalDuration`, `StepStartTimes`, `Loops`, `LoopByStart`, `LoopByEnd`, `EnclosingLoops` (loop-info по индексу шага, упорядоченный outer→inner).
- `SemiStep.Core/Recipes/Analysis/LoopParser.cs` — отдаёт `LoopInfo(StartIndex, EndIndex, Depth, Iterations)`. Отсутствие свойства `task` молча трактуется как `Iterations=1` — текущее поведение **не меняем**.
- `SemiStep.Core/Plc/State/PlcExecutionInfo.cs` — `RecipeActive`, `ActualLine`, `StepCurrentTime` (float, секунды), `ForLoopCount1..3` (по контракту от ПЛК — число **завершённых** итераций, 0-indexed).
- `SemiStep.UI/Plc/PlcMonitorViewModel.cs` — уже подписан на `RecipeCoordinator.ExecutionState`, проецирует `IsRecipeActive`, `ActualLine`, `StepElapsedTime`, `ForLoopCount1..3` в статус-бар.
- `SemiStep.UI/MainWindow/AppStatusBar.axaml` — содержит колонку с «Step: <elapsed>» (col 6, биндится на `PlcMonitor.StepElapsedTime`) и блок «FOR: c1 c2 c3» (col 8).

**Что отсутствует:**

- Никаких `TimeLeft*` нигде не считается и не отображается.

## Development Approach

- **Testing approach: Regular.** Чистый расчёт = pure-static функция, легко покрывается юнит-тестами; VM-логика покрывается через `RecipeCoordinator` mock + синтетические `PlcExecutionInfo`.
- Вся арифметика — `TimeSpan`/`int`; формат — стандартные форматные строки.
- Поведение под jitter покрывается сразу (см. §«Защитный код»), а не «позже по необходимости».

## Testing Strategy

- **Unit (Component=Core, Area=Timing):**
  - `TimingCalculator` — пополнение `SingleIterationDurations` для линейного рецепта (пусто), одного цикла, вложенных циклов.
  - `ExecutionTimeEstimator` — `ActualLine` до/внутри/после цикла, разные значения `ForLoopCount`, `ActualLine` за пределами рецепта, `StepCurrentTime > StepDuration` (clamp к нулю).
- **Unit (Component=UI, Area=Timing):** `PlcMonitorViewModel`:
  - формат `чч:мм:сс` и переключение на `d.чч:мм:сс` при ≥24ч;
  - монотонный clamp на backward-jitter `StepCurrentTime` в пределах одного шага;
  - hold-last-good при потере связи во время исполнения;
  - переходы `RecipeActive: false → true → false` (значения сбрасываются на `(00:00:00, TotalDuration)`);
  - 1s интерполяция — продвижение значений между PLC-апдейтами с резетом базы при приходе нового `StepCurrentTime`.

## Solution Overview

### 1. Расширение `RecipeSnapshot` и `TimingCalculator`

Добавить в `RecipeSnapshot`:

```csharp
IReadOnlyDictionary<int, TimeSpan> SingleIterationDurations
```

ключ — индекс `For`-шага, значение — длительность одного прохода тела (тех же шагов, что попадают между `For` и `End_For`, с уже учтёнными вложенными циклами).

В `TimingCalculator.Calculate` уже есть локальная переменная `singleDuration` (строка 31) — её достаточно записывать в новый словарь при срабатывании ветки `loopByEnd.TryGetValue` (ключ = `loopInfo.StartIndex`).

`LoopMaxIterations` отдельно **не добавляем** — уже доступно через `Snapshot.Loops` / `LoopByStart`.

### 2. Чистый расчёт: `ExecutionTimeEstimator`

Новый статический класс `SemiStep.Core/Recipes/Analysis/ExecutionTimeEstimator.cs`:

```csharp
public static TimeSpan TimeLeftInStep(RecipeSnapshot snapshot, PlcExecutionInfo info, RecipeMetadataRegistry registry);
public static TimeSpan TimeLeftInRecipe(RecipeSnapshot snapshot, PlcExecutionInfo info, RecipeMetadataRegistry registry);
```

Алгоритм для `TimeLeftInRecipe`:

1. Если `ActualLine` за пределами `recipe.Steps` — вернуть `TimeSpan.Zero`.
2. `stepStart = snapshot.StepStartTimes[ActualLine]`.
3. `enclosing = snapshot.EnclosingLoops.TryGet(ActualLine)` (outer→inner, depth ≤ 3).
4. `loopOffset = Σ counts[loop.Depth - 1] × snapshot.SingleIterationDurations[loop.StartIndex]` для каждого `loop` из `enclosing`, где `counts[0..2] = ForLoopCount1..3`. Маппинг идёт **по `LoopInfo.Depth`**, а не по позиции в `enclosing`-списке — это устойчиво к ситуации, когда шаг находится внутри loop с `Depth=2`, не имея вокруг себя `Depth=1` (например, `Depth=1` loop предшествует, но не охватывает).
5. `elapsed = TimeSpan.FromSeconds(info.StepCurrentTime)`.
6. `consumed = stepStart + loopOffset + elapsed`, clamp к `[0, TotalDuration]`.
7. Возврат: `TotalDuration - consumed`.

Алгоритм для `TimeLeftInStep`:

1. `duration = ExtractStepDuration(recipe.Steps[ActualLine])`. Reuse существующего приватного метода `TimingCalculator.ExtractStepDuration` — поднять его в `internal static` или продублировать (предпочтительно поднять; см. Task 1).
2. `elapsed = TimeSpan.FromSeconds(info.StepCurrentTime)`.
3. Возврат: `max(TimeSpan.Zero, duration - elapsed)`.

Класс stateless. `null` не возвращает.

### 3. Расширение `PlcMonitorViewModel`

Существующий VM получает:

- зависимость от `RecipeCoordinator.RecipeSnapshot` (или эквивалентного источника текущего снапшота — определить точное имя на этапе implementation; если нет — придётся пробросить через `IRecipeSessionAccessor` или аналогичный intermediary, см. discovery в Task 3);
- зависимость от `RecipeMetadataRegistry`;
- 1-секундный `Observable.Interval` (тот же `IScheduler`, что использует `MainWindowViewModel.LastSyncTimeText`).

Новые observable-свойства:

- `TimeLeftInStepText: string`
- `TimeLeftInRecipeText: string`

Внутреннее состояние VM:

- `PlcExecutionInfo _lastInfo` — последний валидный PLC-снапшот; не сбрасывается, пока в текущей сессии исполнения хоть раз приходил `RecipeActive=true`.
- `DateTime _localBaseUtc` — момент прихода последнего `StepCurrentTime`; используется для интерполяции.
- `float _lastStepElapsed` + `int _lastStepIndex` — для монотонного clamp: если в пределах того же `ActualLine` пришёл меньший `StepCurrentTime`, удерживаем предыдущее значение.

Жизненный цикл:

- На каждое событие `ExecutionState`:
  - применить монотонный clamp;
  - обновить `_localBaseUtc = DateTime.UtcNow`;
  - пересчитать тексты.
- На каждый тик 1s (no-op при `_lastInfo.RecipeActive == false` — idle-тексты не двигаются):
  - вычислить `interpolatedElapsed = _lastInfo.StepCurrentTime + (UtcNow - _localBaseUtc).TotalSeconds`;
  - построить `interpolatedInfo = _lastInfo with { StepCurrentTime = ... }`;
  - пересчитать тексты.
- На событие изменения снапшота рецепта (mutation):
  - закэшировать новый `RecipeSnapshot`;
  - пересчитать тексты.

Маппинг в текст:

- `RecipeActive=true`: формат через `FormatTimeSpan(value)`.
- `RecipeActive=false`: `"00:00:00"` для шага, `FormatTimeSpan(snapshot.TotalDuration)` для рецепта.
- `snapshot == RecipeSnapshot.Empty` или снапшот отсутствует: `"—"` для обоих.

Метод форматирования:

```
FormatTimeSpan(TimeSpan t) =>
    t.TotalHours >= 24
        ? t.ToString(@"d\.hh\:mm\:ss")
        : t.ToString(@"hh\:mm\:ss");
```

### 4. Статус-бар

В `AppStatusBar.axaml`:

- **Удалить** существующий блок «Step: {PlcMonitor.StepElapsedTime}» (Grid.Column=6, строки 139-157 текущей версии).
- Добавить блок с двумя `TextBlock` на месте удалённого: «Шаг: {TimeLeftInStepText}» и «Рецепт: {TimeLeftInRecipeText}». Видимость по `PlcMonitor.IsRecipeActive` **снять** — поля видны всегда, пока рецепт загружен.
- Сепаратор (Grid.Column=5) и блок FOR (Grid.Column=7-8) — оставить как есть.

Свойство `PlcMonitorViewModel.StepElapsedTime` после удаления биндинга больше нигде не используется — удалить и его (поле, property, присвоение в `OnExecutionStateChanged`).

### 5. Обновление документа спецификации

`Docs/02-ui-requirements.md` §2.6.3 — апдейтнуть две вещи:

- условие «прочерк» переформулировать: «при отсутствии снапшота рецепта / при незагруженном рецепте» (вместо «пока ПЛК не передаёт время»);
- добавить упоминание формата `d.чч:мм:сс` для рецептов ≥ 24 ч;
- зафиксировать, что при `RecipeActive=false` отображается `(00:00:00, TotalDuration)`.

## Implementation Steps

### Task 1: Расширить `RecipeSnapshot` и `TimingCalculator`

**Files:**

- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/TimingCalculator.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Recipes/Analysis/TimingCalculatorTests.cs`

- [x] Добавить параметр `SingleIterationDurations` в `RecipeSnapshot` (после `EnclosingLoops`), плюс в `Empty` и `Create`.
- [x] В `TimingCalculator.Calculate` собирать `Dictionary<int, TimeSpan>` параллельно с расчётом; ключ = `loopInfo.StartIndex`, значение = тот самый `singleDuration` после clamp. Сигнатуру `Calculate` расширить третьим элементом кортежа.
- [x] Поднять `ExtractStepDuration` до `internal static` (передавать `Step` и `RecipeMetadataRegistry`).
- [x] Скорректировать `RecipeAnalyzer.Analyze` под новую сигнатуру.
- [x] Тесты: линейный рецепт → пустой словарь; один цикл из 3 шагов → одна запись; вложенные циклы → внешний цикл агрегирует длительность вложенного с учётом его итераций.
- [x] `dotnet build` + `dotnet test --filter "Area=Timing"`.

### Task 2: Чистый `ExecutionTimeEstimator`

**Files:**

- Create: `SemiStep/SemiStep.Core/Recipes/Analysis/ExecutionTimeEstimator.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Recipes/Analysis/ExecutionTimeEstimatorTests.cs`

- [x] Реализовать `TimeLeftInStep` и `TimeLeftInRecipe` согласно §«Solution Overview ⇒ 2».
- [x] Тесты:
  - линейный рецепт, `ActualLine = 0`, `StepCurrentTime = 0` → `TimeLeftInRecipe = TotalDuration`;
  - линейный рецепт, `ActualLine = последний`, `StepCurrentTime = StepDuration` → `0`;
  - один цикл из 3 итераций × 10 сек, `ActualLine = первый шаг тела`, `ForLoopCount1 = 1` → recipe-остаток меньше на 10 сек, чем при `ForLoopCount1 = 0`;
  - вложенные циклы (внешний 2× / внутренний 3×), все три счётчика заданы корректно;
  - два **последовательных** loop-блока `[For][End_For][For][End_For]` со счётчиком `ForLoopCount1`, текущий шаг внутри второго: убедиться, что `LoopOffset` берёт `SingleIter` только второго loop (по `loop.Depth - 1` indexing) — первый loop уже не в `EnclosingLoops`;
  - `ActualLine` за пределами рецепта → `Zero`;
  - `StepCurrentTime > StepDuration` → `TimeLeftInStep = Zero` (не отрицательное).
- [x] `dotnet test --filter "Area=Timing"`.

### Task 3: Расширить `PlcMonitorViewModel`

**Files:**

- Modify: `SemiStep/SemiStep.UI/Plc/PlcMonitorViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/UiDi.cs` (если потребуются новые DI-регистрации)
- Create: `SemiStep/SemiStep.Tests/UI/Plc/PlcMonitorViewModelTimingTests.cs`

- [x] Подмешать `RecipeMetadataRegistry` и `IScheduler` в конструктор. Текущий `RecipeSnapshot` доступен синхронно через `RecipeCoordinator.Snapshot` (`RecipeCoordinator.cs:90`), уведомления о мутациях — через `RecipeCoordinator.Mutated` event (`RecipeCoordinator.cs:86`). Никаких новых publisher'ов не требуется.
- [x] Реализовать монотонный clamp `StepCurrentTime` в пределах одного `ActualLine`.
- [x] Реализовать 1s интерполяцию через `Observable.Interval(TimeSpan.FromSeconds(1), _scheduler)`. На каждом тике строить `interpolatedInfo = _lastInfo with { StepCurrentTime = _lastInfo.StepCurrentTime + (UtcNow - _localBaseUtc).TotalSeconds }` и пересчитывать тексты. `_localBaseUtc` сбрасывается на каждое реальное событие `ExecutionState`.
- [x] **Интерполяция — no-op при `_lastInfo.RecipeActive == false`.** В idle-состоянии тексты остаются `(00:00:00, TotalDuration)` и не двигаются.
- [x] Hold-last-good: при отсутствии новых событий не обнулять `_lastInfo`; при `RecipeActive=false` от ПЛК — переключиться на «idle»-маппинг.
- [x] Метод `FormatTimeSpan` (private static).
- [x] Свойства `TimeLeftInStepText`, `TimeLeftInRecipeText`.
- [x] Тесты — см. §«Testing Strategy». Удаление `StepElapsedTime` отложено в Task 4 (после удаления XAML-биндинга).
- [x] `dotnet test --filter "Component=UI&Area=Timing"`.

### Task 4: Перепрошить статус-бар

**Files:**

- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml.cs` (если был обработчик старого поля)

- [x] Удалить блок Grid.Column=6 с «Step:» и его биндинг на `PlcMonitor.StepElapsedTime`.
- [x] Вставить два `TextBlock` на том же месте: «Шаг: {Binding PlcMonitor.TimeLeftInStepText}» и «Рецепт: {Binding PlcMonitor.TimeLeftInRecipeText}». Биндинг `IsVisible` по `IsRecipeActive` снять — поля всегда видны, текст полей сам управляет видимостью значения через idle-маппинг.
- [x] Удалить из `PlcMonitorViewModel` свойство `StepElapsedTime` и одноимённое приватное поле; убрать присвоение в `OnExecutionStateChanged`. Кроме старого XAML-биндинга других потребителей нет (`grep StepElapsedTime` → 0 совпадений после удаления XAML-блока).
- [x] visual verification (deferred — pending UI session)

### Task 5: Апдейт спецификации §2.6.3

**Files:**

- Modify: `Docs/02-ui-requirements.md`

- [x] Переформулировать абзац про прочерк (см. §«Solution Overview ⇒ 5»).
- [x] Добавить условие формата `d.чч:мм:сс` при ≥ 24 ч.
- [x] Зафиксировать `(00:00:00, TotalDuration)` для idle-состояния.

### Task 6: Финальная проверка

- [x] `dotnet build SemiStep/SemiStep.slnx`
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] deferred to post-review finalize — Переместить план в `Docs/plans/completed/`.

### Task 7: Ручная валидация на ПЛК (deferred)

Отдельный шаг, не блокирующий закрытие плана — выполняется при первом доступе к рабочему ПЛК (или адекватному эмулятору).

- [x] manual PLC run (deferred — pending hardware access): For-блок ≥ 3 итерации.
- [x] manual PLC run (deferred — pending hardware access): подтвердить семантику `ForLoopCount` (0-indexed completed).
- [x] manual PLC run (deferred — pending hardware access): вложенные циклы, `ForLoopCount2`.

## Post-Completion

**Manual verification:**

- Linear recipe: значения уменьшаются равномерно, секунда в секунду между PLC-тиками за счёт интерполяции.
- Recipe с одним циклом: при изменении `ForLoopCount1` остаток рецепта проседает скачком на одну `SingleIterationDuration`.
- Recipe с вложенными циклами: оба счётчика учитываются.
- Разрыв связи во время исполнения: значения зависают, не превращаются в прочерк.
- `RecipeActive: true → false`: значения сбрасываются на `(00:00:00, TotalDuration)`.

## Open Risks

- **Семантика `ForLoopCount` (0-indexed completed iterations)** — пока подтверждено только конвенцией. Валидация перенесена в Task 7 (deferred manual run); до этого момента отгрузка не блокируется, но в release-notes — упомянуть статус «pending hardware validation».
