# Recipe Grid Surface Abstraction

## Overview

Refactor the existing recipe-grid stack so it consumes an abstraction `IRecipeGridSurface` rather than the concrete `RecipeGridViewModel` + an inline Avalonia `DataGrid`. Outcome:

- The current canonical view (rows = steps, columns = parameters) becomes one concrete implementation, `CanonicalRecipeGridSurface`.
- A swap point exists for a future `TransposedRecipeGridSurface` to plug in without touching `MainWindow`, `MessagePanel`, status bar, clipboard, or command view-models.
- Mutation routing, execution-state highlighting, and selection semantics live behind the abstraction.

> **Naming note.** The interface was originally drafted as `IRecipeGridPresentation`. Renamed to `IRecipeGridSurface` to avoid collision with Avalonia's `*Presenter` infrastructure (`ContentPresenter`, `DataGridCellsPresenter`) and to make the «view-facing API of the grid» reading more honest.

**Explicitly out of scope:**

- The transposed implementation itself (separate plan after this lands).
- Any `Orientation` toggle, hotkey, config-driven default, soft-limit, or new visual feature.
- Any behaviour change. **Success criterion: zero observable change** for the operator.

## Context (from discovery)

**Files currently coupled to `DataGrid` / `RecipeGridViewModel`:**

- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — embeds `<DataGrid x:Name="RecipeGrid" .../>` directly.
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` — subscribes to `RecipeGrid.BeginningEdit`, `RecipeGrid.CellEditEnded`, `RecipeGrid.SelectionChanged`. Reads `RecipeGrid.SelectedItems`, `RecipeGrid.SelectedIndex`. Calls `_columnBuilder.BuildColumns(RecipeGrid)`. Subscribes to `ViewModel.RecipeGrid.SelectionRequested`.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — exposes `RecipeRows`, `CanDeleteStep`, `IsReadOnly`, `SelectedRowIndex`, `SelectedRowIndices`, `EditorMustClose`, `SelectionRequested` event, internal `RecipeMetadataRegistry`. Owns `ExecutionHighlightTracker` subscribed to coordinator's `ExecutionState`.
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — orientation-specific column construction over a passed-in `DataGrid` instance.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowExecutionClassBinder.cs` — stamps row pseudo-classes on `DataGridRow` containers. Called from `MainWindow.axaml.cs:OnDataGridLoadingRow`.
- Consumers reading public surface of `RecipeGridViewModel` (complete member inventory — this drives interface completeness):
  - `MainWindowViewModel` — constructor injection; calls `Initialize()`; carries the `ColumnBuilder` pass-through property the view uses today.
  - `RecipeCommandsViewModel` — observes `CanDeleteStep`, reads `SelectedRowIndex`, calls `RequestSelection(...)` after add/delete.
  - `ClipboardViewModel` — observes `CanDeleteStep`, reads `RecipeRows.Count` (→ `StepCount`), calls `CollectSelectedSteps()` and `RequestSelection(...)` after cut/paste.

**What is already orientation-agnostic and must stay so:**

- `RecipeRowViewModel` (`SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`) — `IsCurrentStep`, `IsPastStep`, `ForDepth`, `IsApplicable(columnKey)`, indexer `this[string columnKey]`, `PropertyValueChanged`/`ActionChanged` events. Row VM is per-step; the transposed view will need a separate row VM type (per-parameter), but the abstraction does not assume either shape.
- `ExecutionHighlightTracker` — operates on `RecipeRowViewModel` collection; transposed view will likely need a different highlight router. The abstraction does **not** standardise highlighting — each implementation owns its own.
- `RecipeCoordinator.Mutated`, `RecipeCoordinator.ExecutionState`, `RecipeCoordinator.CanEditRecipe` — Core/Coordinator signals; both implementations subscribe identically.

## Development Approach

- **Testing approach: Regular** — implement, then write tests in the same task.
- This is a refactor. Every task ends with the full test suite passing and a manual smoke (open recipe, edit a cell, select, copy/paste, save) showing **no observable change**.
- `dotnet format SemiStep/SemiStep.slnx` before each commit.
- The abstraction is intentionally small. Add members only when an existing consumer needs them; do not predict future needs for the transposed view (that plan adds members as needed).

## Testing Strategy

- **Headless UI tests (Component=UI):** retarget existing `RecipeGridViewModel` tests onto the canonical implementation; their assertions remain valid.
- **New abstraction contract tests** (Component=UI): operations against `IRecipeGridSurface` (selection update in both directions, edit-close, can-delete, collect-selected, dispose) produce the same observable outcome regardless of implementation. Cases defined in Task 1 (extended during review). In this plan only the canonical implementation runs them; the transposed plan reuses them.
- **Manual parity smoke:** end of Task 6.

## Backing-widget decision — made in the transposed plan

The transposed view's backing widget is decided and recorded in `20260520-transposed-grid-view.md` («Backing-widget decision»): a **`ListBox` of step-columns** with a horizontal `VirtualizingStackPanel`. DataGrid was rejected for the transposed scenario (no column virtualization — element count scales as steps × parameters; hostile dynamic columns; row-only selection; columns not styleable), and the «ItemsControl with parameter rows» alternative was rejected for inheriting the same missing column virtualization.

**This plan's interface is deliberately widget-agnostic.** Nothing below assumes either implementation uses a `DataGrid`. Canonical keeps its existing `DataGrid` unchanged.

## Solution Overview

### `IRecipeGridSurface` — minimal surface

Driven strictly by what current consumers use. Two-way selection direction is explicit; everything else stays minimal.

```csharp
public interface IRecipeGridSurface : IDisposable
{
    // Lifecycle: initial projection from the coordinator's current recipe.
    // Called once by MainWindowViewModel.Initialize(), exactly as today.
    void Initialize();

    // Coordinator-derived state.
    int StepCount { get; }
    bool IsReadOnly { get; }

    // Selection — model coordinates only.
    IReadOnlyList<int> SelectedStepIndices { get; }
    int SelectedStepIndex { get; }            // -1 when empty

    // View → Surface: actual selection produced by the control (replaces public setter on
    // SelectedRowIndices). The view calls this after mapping its native selection into step indices.
    void UpdateSelection(IReadOnlyList<int> stepIndices);

    // Consumer → Surface: programmatic selection push (replaces the RequestSelection method +
    // SelectionRequested event pair). null = clear. Callers: RecipeCommandsViewModel (add/delete
    // repositioning), ClipboardViewModel (cut/paste repositioning), internal action-change handler.
    void RequestSelection(int? stepIndex);

    // Surface → View: the stream RequestSelection pushes into. View subscribes and positions
    // its native selection.
    IObservable<int?> SelectionRequests { get; }

    // View-bound observation streams. CanDeleteStep emits its current value on subscription
    // (WhenAnyValue semantics) — consumers build CombineLatest canExecute gates from it and
    // must not stay disabled until the first change.
    IObservable<bool> CanDeleteStep { get; }
    IObservable<Unit> EditorMustClose { get; }   // derived from IsReadOnly: false → true transition

    // Clipboard support — pulled onto the interface so ClipboardViewModel never sees a concrete impl.
    IReadOnlyList<Step> CollectSelectedSteps();
}
```

Changes from the original draft:

- `UpdateSelection(...)` replaces the implicit `SelectedRowIndices` setter — direction view → surface is now explicit and the contract is closed.
- `RequestSelection(int?)` + `SelectionRequests` split the old method/event pair by direction: consumers push through the public method (four external call sites today: `RecipeCommandsViewModel.AddStep`/`DeleteStep`, `ClipboardViewModel.CutStepsAsync`/`PasteStepsAsync`), the view subscribes to the observable. The surface's internal `Subject<int?>` is the implementation detail.
- `Initialize()` is on the interface — `MainWindowViewModel.Initialize()` calls it today on the concrete class and keeps doing so through the interface.
- `CollectSelectedSteps()` and `StepCount` are on the interface so `ClipboardViewModel` (which uses both today via the concrete class) does not need a concrete-class fallback.
- `IsReadOnlyChanged` removed as redundant — `EditorMustClose` already conveys the actionable transition; `IsReadOnly` can be observed by consumers via standard ReactiveUI `WhenAnyValue` if needed.
- `HasSelection` and `SelectedStepIndicesChanged` removed during review — no production consumer existed (call sites derive the predicate from `SelectedStepIndices.Count`), and the minimality rule above outranks the original sketch: every dead member would have to be implemented and contract-tested by each future surface.

Not on the interface (stays on concrete `CanonicalRecipeGridSurface`):

- `ObservableCollection<RecipeRowViewModel> RecipeRows` — canonical-specific shape, consumed only by `CanonicalRecipeGridView`.
- Internal `RecipeMetadataRegistry` — accessed only by canonical column builder.

### `RecipeGridHost` UserControl

A new control at `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridHost.axaml`. Holds a child view (today: canonical only). Exposes a single `IRecipeGridSurface Surface { get; }` property — the swap point the transposed plan consumes, exercised by tests. `MainWindow.axaml` replaces its inline `DataGrid` with `<rg:RecipeGridHost x:Name="RecipeGridHost" .../>`; `MainWindow` code-behind reads only `RecipeGridHost.IsEditing`.

In this plan, `RecipeGridHost` does NOT switch implementations — there is only one. The swap mechanism is wired by the transposed plan.

### `CanonicalRecipeGridSurface`

The existing `RecipeGridViewModel` is renamed to `CanonicalRecipeGridSurface` (file rename + class rename), implements `IRecipeGridSurface`, and continues to own the canonical-only members (`RecipeRows`, etc.).

Code-behind that today lives in `MainWindow.axaml.cs` (`OnBeginningEdit`, `OnCellEditEnded`, `OnSelectionChanged`, `OnSelectionRequested`, `OnDataGridLoadingRow`, `BuildGrid`) moves into a new `CanonicalRecipeGridView.axaml.cs` — a UserControl that wraps the `DataGrid` and owns its event subscriptions.

### Dependency injection

Today: `RecipeGridViewModel` is registered as a concrete service. After: register `CanonicalRecipeGridSurface` as both itself and as `IRecipeGridSurface`. Consumers (`MainWindowViewModel`, `RecipeCommandsViewModel`, `ClipboardViewModel`) take `IRecipeGridSurface` by interface **only** — no fallback to the concrete class is permitted (see Task 6).

## Technical Details

### Selection translation (view → surface)

Today `OnSelectionChanged` in `MainWindow.axaml.cs` walks `RecipeGrid.SelectedItems`, maps each to a row VM, derives the index via `RecipeRows.IndexOf(row)`. That logic moves into `CanonicalRecipeGridView.axaml.cs`. The result — a `IReadOnlyList<int>` of step indices — is fed via `surface.UpdateSelection(indices)`.

The transposed implementation will have its own selection-translation; the surface sees only step indices.

### Programmatic selection (surface → view)

`RequestSelection(int?)` is the single public push point; internally the concrete surface owns a `Subject<int?>` exposed as `SelectionRequests`. Callers: the two command view-models and the clipboard view-model (post-mutation repositioning), plus the surface's own `OnActionChanged` after a step-action change. The view subscribes in its activation block: canonical sets `DataGrid.SelectedIndex`, transposed sets the selected column. One method, one stream, both directions closed.

### Edit-must-close signal

`EditorMustClose` derives from `IsReadOnly` (`false → true`) on the concrete surface. Canonical already consumes it declaratively through the `DataGridEditorCloseBehavior.Trigger` attached-property binding in AXAML (introduced by `20260520-per-window-edit-connect-mode.md`); Task 4 relocates that binding into the extracted view. Transposed will consume the same observable its own way. The signal is kept distinct from raw `IsReadOnly` because it is **actionable** (close any open editor) rather than a state to observe.

### Read-only routing

`IsReadOnly` is computed inside each concrete surface from `coordinator.CanEditRecipe.Select(canEdit => !canEdit)`. Exposed on the interface as a plain `bool` for one-shot reads (e.g. cancelling `BeginningEdit`). Consumers needing reactive notification observe `EditorMustClose` or `WhenAnyValue(x => x.IsReadOnly)` on the concrete instance — there is no separate `IsReadOnlyChanged` member because it would duplicate `EditorMustClose` for every existing use site.

### Mutations and subscription-ownership change

Both implementations subscribe to `RecipeCoordinator.Mutated` independently. The abstraction does **not** expose a "refresh me" method — implementations own that responsibility.

**This is a change of subscription ownership.** Today `App.axaml.cs:114` wires `coordinator.Mutated += gridViewModel.OnMutation` externally. After this refactor, the surface subscribes itself in its constructor. Other `Mutated` subscribers (`PlcMonitorViewModel`, `MainWindowViewModel`, `RecipeCommandsViewModel`) continue to subscribe themselves as today, but **the order of invocation across handlers changes** because the surface now subscribes at construction time rather than at app startup.

Expected conclusion (verify statically in Task 3 by reading the handlers): the four `Mutated` subscribers (grid row re-projection, `PlcMonitorViewModel`, `MainWindowViewModel` state-property raise, `RecipeCommandsViewModel` undo/redo refresh) have no cross-handler side-effect dependency within a single dispatch, so order is immaterial. If reading them contradicts this, fix the dependency explicitly (e.g. by routing through an ordered dispatcher) — do not paper over with subscription timing.

## Implementation Steps

### Task 1: Define `IRecipeGridSurface`

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/IRecipeGridSurface.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeGridSurfaceContractTests.cs` (abstract base; concrete fixture follows in Task 3)

- [x] Define the interface as in Solution Overview.
- [x] Write the contract test base class with the cases below (the last four were added during review) — given an `IRecipeGridSurface`:
  1. `UpdateSelection([1, 3])` → `SelectedStepIndices == [1, 3]`, `SelectedStepIndex == 1`.
  2. `UpdateSelection([])` → `SelectedStepIndices.Count == 0`, `SelectedStepIndex == -1`.
  3. `RequestSelection(2)` → `SelectionRequests` emits `2`.
  4. `RequestSelection(null)` → `SelectionRequests` emits `null` (the view interprets it as "clear selection").
  5. `EditorMustClose` emits when `IsReadOnly` transitions `false → true`; does **not** emit on `true → false`.
  6. `CanDeleteStep` is `true` iff `SelectedStepIndices.Count > 0`; reacts to `UpdateSelection`.
  7. `CollectSelectedSteps()` returns steps in ascending `stepIndex` order; the result is consistent with the current `SelectedStepIndices`.
  8. After `Dispose()`, coordinator signals (`Mutated`, `ExecutionState`, `CanEditRecipe`) produce no further emissions on any surface observable and no state changes — deterministic no-leak check instead of a GC/WeakReference assertion.
  9. `Initialize()` projects the seeded recipe: `StepCount` equals the seeded step count.
  10. `IsReadOnly` tracks `coordinator.CanEditRecipe` in both directions.
  11. `EditorMustClose` does not replay to a subscriber that subscribes while already read-only.
  12. After `Dispose()`, the consumer-facing calls (`RequestSelection`, `UpdateSelection`) are safe no-ops — they must not throw.
- [x] Run tests (vacuously pass — no implementation yet).

### Task 2: Rename `RecipeGridViewModel` → `CanonicalRecipeGridSurface`

**Files:**
- Rename: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` → `CanonicalRecipeGridSurface.cs`
- Modify (text): every reference across `SemiStep.UI` and `SemiStep.Tests`.

- [x] Mechanical rename. Run `dotnet build` to confirm zero compile errors.
- [x] Run full test suite — must pass unchanged.

### Task 3: `CanonicalRecipeGridSurface` implements the interface

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridSurface.cs`
- Modify: `SemiStep/SemiStep.UI/UiDi.cs` — register as `IRecipeGridSurface`.
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs` — remove the external `coordinator.Mutated += gridViewModel.OnMutation` wiring (the surface now subscribes itself in its constructor).
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/CanonicalRecipeGridSurfaceContractTests.cs` — inherits from the Task 1 base.

- [x] Add `: IRecipeGridSurface` to the class. Adapt member names: `SelectedRowIndices` → `SelectedStepIndices`, `SelectedRowIndex` → `SelectedStepIndex`. Drop the public setter on `SelectedStepIndices` in favour of `UpdateSelection(IReadOnlyList<int>)`. Replace the `SelectionRequested` event with the public `RequestSelection(int?)` pushing into an internal `Subject<int?>` exposed as `SelectionRequests`; `OnActionChanged` calls the same public method. The existing `RequestSelection` method signature is unchanged for the four consumer call sites.
- [x] Rewrite the two tests that assert the old event shape (`RecipeGridViewModelTests.RequestSelection_RaisesSelectionRequestedEvent` and `..._WithNull`, `RecipeGridViewModelTests.cs:223,235`) against the `SelectionRequests` observable — they do not survive the rename mechanically.
- [x] Add the `CanDeleteStep` observable (delegates to the existing `WhenAnyValue` path). (`SelectedStepIndicesChanged` was added here and later removed in review as a dead member.)
- [x] Subscribe to `coordinator.Mutated` in the constructor; unsubscribe in `Dispose`. Keep `OnMutation` public — the stale-signal tests (`RecipeGridViewModelTests.cs:304-397`) call it directly.
- [x] Remove the manual `Coordinator.Mutated += _grid.OnMutation` wiring from every test fixture that constructs the surface (`RecipeGridViewModelTests`, `ClipboardViewModelCanExecuteTests`, `MessagePanelReportingTests`, `RecipeGrid/*Tests` — grep for the pattern). Leaving them in place would double-subscribe and apply every mutation twice.
- [x] Add `CollectSelectedSteps()` and `StepCount` (returns `RecipeRows.Count`).
- [x] Wire contract tests against the canonical implementation; all cases pass.
- [x] **Subscription-order verification:** read the four `Mutated` handlers and confirm the static no-cross-dependency argument from Technical Details holds; record the confirmation in the PR description. (Confirmed: each handler reads only coordinator state committed before the event fires plus its own private state — grid re-projects rows from `CurrentRecipe`/`Snapshot`, `PlcMonitorViewModel` recalculates texts from `Snapshot`, `MainWindowViewModel` raises property notifications, `RecipeCommandsViewModel` pushes `CanUndo`/`CanRedo` into its own subjects. No handler reads another handler's output within a dispatch; order is immaterial. Recorded in the commit message.)

### Task 4: Extract `CanonicalRecipeGridView` UserControl

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridView.axaml` (+ `.axaml.cs`)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — remove the inline `DataGrid` block (will be replaced in Task 5).
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` — remove `OnBeginningEdit`, `OnCellEditEnded`, `OnSelectionChanged`, `OnSelectionRequested`, `OnDataGridLoadingRow`, `OnCellPointerPressed`, `BuildGrid`, the `_columnBuilder`, `_isEditing`, `_columnsBuilt`, `_pendingChangedCell` fields, and the column-builder / `CellPointerPressed` wiring inside `WhenActivated`.
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` — remove the `ColumnBuilder` pass-through property (the view now reaches it via the surface, see below).

- [x] The UserControl hosts the `DataGrid` and re-implements every event handler currently in `MainWindow.axaml.cs`, talking to the `CanonicalRecipeGridSurface` via `DataContext`. The view binds canonical-only members (`RecipeRows`, `IsReadOnly`), so its `x:DataType` is the concrete `CanonicalRecipeGridSurface`, not the interface.
- [x] `OnCellPointerPressed` + `_pendingChangedCell` (orange-cell click-away clearing via `ChangedCellClickResolver`) move into the UserControl with the rest — they are fully DataGrid-bound.
- [x] `RecipeRowExecutionClassBinder.BindAll` invocation moves into the UserControl's `OnDataGridLoadingRow`.
- [x] Column building: `ColumnBuilder` is exposed as a property on `CanonicalRecipeGridSurface` (constructor-injected there instead of into `MainWindowViewModel`), so the view reaches it through `DataContext` — no service locator, no XAML-unfriendly constructor injection into the UserControl. `_columnsBuilt` guard moves with `BuildGrid`. Update the Task 3 contract-test fixture to pass the new constructor parameter (`UIFixture` already builds a `ColumnBuilder`).
- [x] **Editing gate for global shortcuts:** `MainWindow.OnKeyDown` (`MainWindow.axaml.cs:84`) suppresses Delete/Ctrl+C/X/V while a cell editor is open by reading `_isEditing`. The flag moves into the view; the view exposes it as a public `IsEditing` CLR property, `RecipeGridHost` forwards it (`Host.IsEditing => activeView.IsEditing`), and `OnKeyDown` reads the host property. Editing state stays a view concern — it does not go on `IRecipeGridSurface`. (`OnKeyDown` reads the view's `IsEditing` directly until Task 5 introduces the host and re-routes it.)
- [x] On `DataGrid.SelectionChanged`: walk `RecipeGrid.SelectedItems`, derive step indices, call `surface.UpdateSelection(indices)`.
- [x] Subscribe to `surface.SelectionRequests`: on each value, set `RecipeGrid.SelectedIndex` (or clear when `null`).
- [x] `EditorMustClose` handling stays declarative: move the existing `DataGridEditorCloseBehavior.Trigger` binding from `MainWindow.axaml` into `CanonicalRecipeGridView.axaml` unchanged. Do not add a code-behind subscription — the attached behavior is the single close mechanism (see completed plan `20260520-per-window-edit-connect-mode.md`).
- [x] Test the UserControl headlessly: load with a surface containing 3 steps, assert rows render; push a value on `SelectionRequests` from the test and assert the corresponding row is selected.

### Task 5: `RecipeGridHost` UserControl

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridHost.axaml` (+ `.axaml.cs`)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — replace removed `DataGrid` with `<rg:RecipeGridHost x:Name="RecipeGridHost" .../>`.

- [x] The host's content is the `CanonicalRecipeGridView` instance, bound to the same `DataContext` as today's grid. No orientation logic yet; the host is a thin pass-through.
- [x] Expose `Surface` (the `IRecipeGridSurface`) as a CLR property for tests and as the transposed-plan swap point; `MainWindow` reads only `IsEditing`.
- [x] Expose `IsEditing` forwarding to the active view (see Task 4's editing gate) — `MainWindow.OnKeyDown` reads it.
- [x] Headless test: instantiate the host, assert it renders the canonical view and forwards selection.

### Task 6: Migrate consumers to `IRecipeGridSurface` (no concrete fallback)

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` — accept `IRecipeGridSurface`.
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs` — accept `IRecipeGridSurface`.
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs` — accept `IRecipeGridSurface`. Replace `_recipeGrid.CollectSelectedSteps()` and `_recipeGrid.RecipeRows.Count` with the corresponding interface members (`CollectSelectedSteps()`, `StepCount`).

- [x] Replace constructor parameter types with the interface. **No concrete-class fallback is accepted in this plan.** If any consumer still requires a canonical-only member (`RecipeRows`, `RecipeMetadataRegistry`, etc.), one of two things is true: either the interface is missing a member (add it and update contract tests), or the consumer is doing something that crosses the abstraction boundary (refactor the consumer). The leak is the bug.
- [x] Run full test suite. Manual smoke: open recipe, edit, select, copy/paste, save, exit. **No observable change.** (manual smoke covered by headless tests - not automatable here)

### Task 7: Verify acceptance criteria

- [x] `IRecipeGridSurface` defined and used by all three consumer view-models — **none of them references the concrete class**.
- [x] Canonical view extracted to its own UserControl; `MainWindow.axaml.cs` no longer touches the DataGrid directly.
- [x] `RecipeGridHost` in place as the swap point.
- [x] All contract-test cases pass against canonical.
- [x] Full test suite green.
- [x] Manual parity smoke (headless-covered; real-app smoke performed by orchestrator post-run).

### Task 8: Final — close-out

- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] Move this plan to `Docs/plans/completed/`. (deferred to harness)

## Post-Completion

This plan adds **no user-facing change**. Its value is purely structural: the transposed implementation can now be added as a sibling UserControl + presentation pair without touching `MainWindow`, command view-models, or clipboard view-model.

The next plan in this series — implementing the transposed view — depends on this one. Its scope is a single new surface + view pair (a `ListBox` of step-columns, per the decision recorded there), plus the orientation toggle and config default; no work inside the existing canonical view.
