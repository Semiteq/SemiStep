# Transposed Recipe Grid View

## Overview

Add a second concrete implementation of `IRecipeGridSurface` where the recipe is displayed rotated 90°: parameters as rows, steps as columns. The two views share Core/coordinator wiring; only the visual layer differs. The original UI-requirements document that specified this feature was rewritten into a single user guide; the transposition requirement is now recorded in this plan.

Deliverables:

- `TransposedRecipeGridSurface` — peer of `CanonicalRecipeGridSurface`.
- `TransposedRecipeGridView` — UserControl built on a **`ListBox` of step-columns** (decision recorded below; no DataGrid).
- `ActiveRecipeGridSurface` — delegating router so single-surface consumers survive orientation flips.
- `RecipeGridHost` gains orientation switching driven by a per-window flag.
- Toggle item in the View menu of `RecipeMenuBar` + `Ctrl+Shift+T` hotkey.
- Per-machine default in `ui/grid_style.yaml` (`orientation: rows_as_steps | columns_as_steps`), surviving the in-app style-editor save round-trip.
- Prototype target: smooth at 100–150 steps; the architecture has no structural ceiling below the original ~1000-step scale requirement (columns virtualize), so no soft cap is planned.

Constraints from previously settled decisions:

- Cell click selects the whole step-column (whole column visually highlighted).
- Orientation choice is per-session; on next launch the config default applies.
- CSV import/export stays canonical regardless of view.
- Copy/paste does not cross orientations.

## Prerequisite — landed

`20260520-recipe-grid-presentation-abstraction.md` merged to master as PR #129 (`e0511e3`), plan moved to `Docs/plans/completed/`. `IRecipeGridSurface` is live, canonical is extracted into `CanonicalRecipeGridView` behind `RecipeGridHost`, and the contract-test base `RecipeGridSurfaceContractTests` holds **13 cases** (grown from the originally planned eight during review). This plan adds the transposed sibling without touching canonical internals.

## Backing-widget decision (2026-07): ListBox of step-columns

Verified against `AvaloniaUI/Avalonia.Controls.DataGrid` sources (active repo, package 12.0.1) and current docs. Supersedes the earlier Task-0 spike between «DataGrid + custom layers» and «ItemsControl with parameter rows» — both rejected.

**Why not DataGrid:**

1. **No column virtualization.** `DataGridRow` creates a `DataGridCell` for every column (`Debug.Assert(Cells.Count == ColumnsItemsInternal.Count)`); only measure/arrange skips off-screen cells. In transposed mode every parameter row is on screen simultaneously, so live element count = steps × parameters. At the ~1000-step scale (~1000 steps × ~21 parameters) that is ~21 000 cells with templated content — unusable. Practical ceiling ≈ 100–150 steps.
2. **Dynamic columns are hostile.** `Columns` is not bindable; every recipe mutation needs imperative column surgery, and `Columns.Clear()` resets horizontal scroll and selection.
3. **Edit lifecycle mismatch.** `BeginningEdit`/`CellEditEnded` engage only through `CellEditingTemplate`; always-live editors in `CellTemplate` (which the cell-type dispatch requires) bypass them.
4. **No column-level selection, columns not styleable** (`DataGridColumn` is an `AvaloniaObject`, not a `Visual`), keyboard navigation methods are `private`. Every one of these needs a workaround layer.

**Why not ItemsControl with parameter rows** (rows as items, each row a `UniformGrid`/`Grid` of step cells): it inherits the same missing column virtualization (all steps realized in every row), and column-level selection/tinting is again per-cell converter work.

**Why ListBox of step-columns:** invert the item axis — one item = one step.

- **Column virtualization for free:** horizontal `VirtualizingStackPanel` realizes only the ~20–30 visible columns; realized element count is viewport-bound (~500 cells) regardless of recipe length.
- **Column selection for free:** a step-column is a `ListBoxItem`; `SelectionMode="Multiple"` gives whole-column highlight via the `:selected` style and multi-step selection for clipboard flows.
- **Column tinting in one place:** `current-step`/`past-step`/`for-depth-N` style classes go on the `ListBoxItem` container — same class-binder idiom as canonical's `RecipeRowExecutionClassBinder`, same palette brushes from `CellPaletteInstaller`.
- **Cheap mutations:** append/insert/remove step = `ObservableCollection` operation; no column objects to rebuild, scroll and selection survive.

**Costs accepted with this choice** (each scoped in a task below):

- Parameter-name column and per-column layout are hand-rolled (fixed cell heights keep it trivial).
- Arrow-key cell navigation is hand-rolled (Task 7.5); Tab order works natively.
- Container recycling must not leak editor state between columns (Task 0 validates).
- Alternatives ruled out: `TreeDataGrid` is archived upstream and moved to the commercial Avalonia Accelerate; no third-party Avalonia grid offers transposition — it is always a model projection.

## Context (verified against master `e0511e3`)

**`IRecipeGridSurface` final shape** (`SemiStep/SemiStep.UI/RecipeGrid/IRecipeGridSurface.cs`): `Initialize`, `StepCount`, `IsReadOnly`, `SelectedStepIndices`, `SelectedStepIndex`, `UpdateSelection`, `RequestSelection`, `SelectionRequests`, `CanDeleteStep`, `EditorMustClose`, `CollectSelectedSteps`, `IDisposable`. Members `HasSelection` and `SelectedStepIndicesChanged` from the original draft were deleted during review — do not resurrect them. Contract pins to honour: `CanDeleteStep` replays current value on subscription and is `DistinctUntilChanged`; `EditorMustClose` fires only on false→true `IsReadOnly` edges with no replay; all consumer-facing calls are safe no-ops after `Dispose`.

**Mutation signal** (`SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs`) — real variants, not the draft names: `PropertyUpdated(stepIndex)`, `StepAppended(index)`, `StepsInserted(startIndex, count)`, `StepRemoved(removedIndex)`, `StepsRemoved(removedIndices)`, `StepActionChanged(stepIndex)`, `RecipeReplaced`, `StateRefreshed` (no-op for projection).

**Post-mutation refreshes:** canonical's `OnMutation` tail runs `ReconcileSelectionWithRows`, `RefreshStepStartTimes`, `RefreshRowLoopDepths` after every signal (`CanonicalRecipeGridSurface.cs:169-171`), and the structural handlers additionally call `RenumberRows` internally — step numbers, cumulative start times, loop-depth tint, and the validity of selection indices all shift when a step is inserted or removed. The transposed surface must mirror all four; selection reconciliation drops indices that fall past the shrunken collection after a removal.

**Inline formulas:** a cell edit can update a formula-coupled cell of the same step within one mutation (`NCalcSync`, per-action `formula:` blocks). Canonical handles this because `PropertyUpdated` refreshes the whole row. The transposed equivalent: `PropertyUpdated(stepIndex)` refreshes **all cells of that column**, never just the edited one.

**`RecipeRowViewModel` is orientation-agnostic and gets reused.** Its constructor is `(stepNumber, Step, ActionDefinition, RecipeMetadataRegistry, inapplicableColumns)` — no Avalonia or DataGrid dependency. It already owns the value indexer with INPC, `IsApplicable`/`RecomputeInapplicableColumns`, the changed-cell set (`MarkChanged`/`ApplyChangedDelta`/`ClearChanged`/`ClearAllChanged`/`IsChanged`), group-combo item lists, format-kind/unit metadata, execution flags (`IsCurrentStep`/`IsPastStep`/`ForDepth`), and the three write events (`PropertyValueChanged`, `ActionChanged`, `SelectorValueChanged`). Duplicating any of this for transposed would be a bug farm; the transposed step-column VM wraps a `RecipeRowViewModel` and cells are thin adapters over it.

**Cell kinds** (`ColumnTypes.cs`): `action_combo_box` (changes the step's action, rebuilds the step), `action_target_combo_box` (group-bound combo; selector semantics for nested subactions via `SelectorEdit`), `property_field` (editable text with `time`/unit format kinds), `step_start_time_field` (read-only cumulative time), `text_field`. The original draft's Text/ComboBox/ReadOnly triple is too narrow — without the action combo the transposed view cannot change a step's action at all.

**Changed-cell highlight (issue #63):** cells seeded by an action/selector change paint orange (`colors.cells.changed` / `changed_selected`). Mark points: `RebuildRow → MarkChanged` on action change, `ApplyChangedDelta` on selector change. Clear triggers: successful edit of the cell, click on any other cell (`ChangedCellClickResolver`, no read-only guard), execution inactive→active edge (`ExecutionHighlightTracker.ClearAllChangedHighlights`). See `Docs/architecture/cell-change-highlight.md`. The transposed view must reproduce all mark and clear paths; state lives in the reused `RecipeRowViewModel`, so the work is surface calls + painting + the click-away hook.

**Fonts and layout from config:** `GridFontApplier` + `GridFonts` apply `grid_style.yaml` font roles (cell/header) and tabular figures; `layout.row_height` drives cell height. Transposed templates consume the same resources — no hardcoded fonts or heights.

**Formatting converters to reuse:** `PropertyTimeMultiConverter` (display: HH:MM:SS for time kinds, `0.###` numerics, unit suffixes), `PropertyTimeEditingConverter` + `TimeFormatHelper` (parse-back, `BindingOperations.DoNothing` on invalid input).

**Consumers hold a single `IRecipeGridSurface`:** `MainWindowViewModel`, `RecipeCommandsViewModel`, `ClipboardViewModel` receive one surface via constructor DI (`UiDi.cs` registers `CanonicalRecipeGridSurface` + interface alias). With two live implementations an orientation flip must not re-create consumers — hence the `ActiveRecipeGridSurface` router below.

**`RecipeGridHost` today:** hardcodes `CanonicalRecipeGridView` as its only child, `Surface => DataContext as IRecipeGridSurface`, `IsEditing => CanonicalView.IsEditing`. Both members become orientation-aware in Task 6. The `IsEditing` chain (view → host → `MainWindow.OnKeyDown`) gates Delete/Ctrl+C/X/V while an editor is open; the transposed view needs an equivalent signal (an always-live editor with keyboard focus counts as editing).

**Localization:** menu items bind `{x:Static l:Resources.*}` from resx. The toggle needs new resource entries, not hardcoded strings.

**New types required in this plan:**

- `StepColumnViewModel` — one per step, bound as a `ListBox` item. Wraps the reused `RecipeRowViewModel` (owns its lifetime) and exposes `Cells: IReadOnlyList<ParameterCellViewModel>` in parameter-row order plus pass-throughs the item template needs (`StepNumber`, `IsCurrentStep`, `IsPastStep`, `ForDepth`). Selection state lives on the `ListBoxItem`, not the VM.
- `ParameterDescriptor` — slim per-parameter metadata for the frozen name column and cell construction: `ParameterKey`, `ParameterDisplayName`, `ColumnType` (from `ColumnTypes`), `IsReadOnlyParameter` (column-level `read_only: true`, e.g. `step_start_time`). Ordered exactly as canonical columns are (registry order).
- `ParameterCellViewModel` — leaf cell adapter over `(RecipeRowViewModel, ParameterDescriptor)`: abstract base + concrete kinds mirroring `ColumnTypes` (action combo, target/selector combo, property text, read-only/step-start-time). Exposes `Value` (INPC wrapper — never bind the row indexer directly from templates), `IsApplicable`, `IsChanged`, `Items` for combos. Writes route through the row VM's existing events; no new write path.
- `ActiveRecipeGridSurface` — `IRecipeGridSurface` router registered as the DI interface alias, and the **single source of truth for orientation**. Constructor takes both concrete surfaces + `GridStyleOptions` (startup default). Exposes reactive `Orientation`, `ToggleOrientation()`, and the concrete `CanonicalSurface`/`TransposedSurface` properties (the host needs them for explicit child `DataContext` wiring — see below). Forwards every interface call to the active surface, re-emits `SelectionRequests`/`CanDeleteStep`/`EditorMustClose` across swaps (switch-subscription), transfers `SelectedStepIndices` on flip. The interface itself stays orientation-free — concrete implementations know nothing about switching.
- `TransposedExecutionHighlightTracker`, `TransposedStepColumnClassBinder` — peers of the canonical tracker/binder.

**Current-step marker placement:**

Top of each step-column, inside the column item template: a `Border` (`Height=4`, background from the execution palette's current-step marker brush, `IsVisible="{Binding IsCurrentStep}"`), directly under the step-number header cell.

## Development Approach

- **Testing approach: Regular.**
- The plan introduces new types incrementally; each task ends with tests covering its surface.
- No change to canonical view code paths. Reusing `RecipeRowViewModel` (unchanged) is allowed; modifying it or any canonical class indicates the abstraction is incomplete — stop and reassess first. The only planned canonical-adjacent edits are `RecipeGridHost` (designated swap point) and DI registration.
- Manual performance smoke (Task 9) on synthetic 50-, 100- and 200-step recipes; record observations in this plan file before close-out.

## Testing Strategy

- **Unit tests (Component=UI):** `StepColumnViewModel`, `ParameterCellViewModel` semantics — applicability, changed flag, current-step propagation, read-only inheritance, value round-trip, format kinds.
- **Contract tests:** all **13** `RecipeGridSurfaceContractTests` cases run against `TransposedRecipeGridSurface` via a subclass (same pattern as `CanonicalRecipeGridSurfaceContractTests`); `UIFixture` gains a `CreateTransposedSurface` helper next to `CreateCanonicalSurface`.
- **Headless UI tests (Component=UI, Category=Integration):** open the host with transposed orientation, click a cell, assert the corresponding step is selected; flip orientation back and assert canonical renders unchanged; type-and-commit a value in a transposed cell, assert the coordinator receives the right `(stepIndex, propertyKey, value)` triple.
- **Headless visual tests:** column container for a step where `IsCurrentStep=true` carries the `current-step` class and the marker `Border.IsVisible=true`; toggling `ForDepth` swaps the container's depth class; a changed cell paints the changed brush and clears on click-away.
- **Configuration tests (Component=Config):** loader accepts both `orientation` values, rejects unknown, defaults to `rows_as_steps` when absent; `GridStyleWriter` round-trip preserves the field.
- **Manual smoke (Task 9):** open RIE config (default `columns_as_steps` once that lands), exercise edits, select, copy/paste within transposed; flip to canonical, confirm same recipe state.

## Solution Overview

### View structure

```
TransposedRecipeGridView
└── ScrollViewer (vertical; engages only when parameter count overflows the window)
    └── DockPanel
        ├── (left, frozen by construction) parameter-name column:
        │     header spacer (step-number row height + marker height)
        │     ItemsControl over ParameterDescriptors — fixed cell height
        └── (fill) ListBox — ItemsSource = StepColumns
              SelectionMode="Multiple"
              ItemsPanel: VirtualizingStackPanel Orientation="Horizontal"
              inner ScrollViewer: horizontal Auto, vertical Disabled
              ItemTemplate (one step-column):
                  StackPanel
                  ├── header cell: step number
                  ├── current-step marker Border (Height=4)
                  └── ItemsControl over Cells — fixed cell height, one editor per parameter
```

Fixed, uniform cell height (`layout.row_height` from config) is a hard rule: it keeps the name column and every virtualized step-column row-aligned with zero cross-column measure coordination. Step-columns share one fixed width (config-font-aware constant is enough for the prototype; no per-column min-content pass like canonical's `ColumnWidthCalculator`). The header row scrolls with the vertical ScrollViewer; with the widest live config (RIE, 21 parameters) everything fits a 1080p window without vertical scroll, so pinning the header is deferred until a config actually overflows.

### Data flow

```
Recipe mutation
  → RecipeCoordinator.Mutated (MutationSignal)
  → TransposedRecipeGridSurface dispatches surgically:
      StepAppended(i)          → StepColumns.Add(...)
      StepsInserted(start, n)  → StepColumns.Insert(...) × n
      StepRemoved(i)           → StepColumns.RemoveAt(i)
      StepsRemoved(indices)    → RemoveAt descending
      PropertyUpdated(i)       → refresh ALL cells of column i (covers formula-coupled cells)
      StepActionChanged(i)     → rebuild column i's row VM + cells, MarkChanged on new action's cells
      RecipeReplaced           → rebuild the collection (viewport realizes ~25 columns; cheap)
      StateRefreshed           → no projection change
    then, after any structural change: renumber subsequent columns, reconcile
    selection with the new column count, refresh step start times, refresh loop
    depths (mirror of canonical's ReconcileSelectionWithRows + refresh tail)
  → ListBox reflects collection changes natively; scroll and selection survive

PlcExecutionInfo (running recipe)
  → TransposedExecutionHighlightTracker updates IsCurrentStep/IsPastStep by StepIndex,
    clears all changed highlights on the inactive→active edge (parity with canonical)
  → container class binder re-stamps ListBoxItem classes → column re-tints

Selection
  → view: ListBox.SelectionChanged maps selected items → sorted step indices → surface.UpdateSelection
  → surface → view: SelectionRequests observable → view selects the item + BringIntoView; null clears
```

### Editing

Cells host always-live editors (`TextBox` / `ComboBox` / read-only `TextBlock`) selected by cell-VM type via `ContentControl` + `DataTemplates` — the same shape canonical already uses for combo cells. Two-way binding into `ParameterCellViewModel.Value`; display and parse go through the existing `PropertyTimeMultiConverter` / `PropertyTimeEditingConverter` so time cells render `HH:MM:SS`, numerics get `0.###` + unit suffixes, and invalid time input stays uncommitted. Commit semantics: `TextBox` on `LostFocus`/`Enter`, `ComboBox` on selection change. There is no DataGrid edit lifecycle to emulate: `IsReadOnly` and `IsApplicable` gate the editor via `IsEnabled`/template choice, and `EditorMustClose` moves focus off the active editor (which commits or discards). The action combo routes through `RecipeRowViewModel.ActionChanged` (step rebuild + changed marking); the target/selector combo routes through `SelectorValueChanged` (one undo unit, applicability recompute). Changed-cell clear-on-click-away: a pointer-pressed hook on the cells panel resolves the pending changed cell, mirroring `ChangedCellClickResolver` semantics.

### Toggle wiring

Orientation has exactly one owner: `ActiveRecipeGridSurface`. Everything else observes or delegates.

`ActiveRecipeGridSurface` (registered as `IRecipeGridSurface` in DI, also resolvable as its concrete type):

- Owns reactive `Orientation` (enum `{ Canonical, Transposed }`), initialized from `GridStyleOptions` in the constructor; `ToggleOrientation()` flips it.
- Delegates every interface member to the current concrete surface — **except `Initialize()`, which fans out to BOTH surfaces.** The initial projection is a pull (`Initialize()` → full rebuild from `coordinator.CurrentRecipe`), not a `Mutated` push: the coordinator loads the startup recipe before the surfaces are constructed, and `Mutated` is a plain no-replay event. Initializing only the active surface leaves the other blank until an unrelated `RecipeReplaced` fires. With both initialized at startup and both subscribed to `Mutated`, either surface stays current-state-correct when it becomes active.
- On flip: carries `SelectedStepIndices` from old to new surface, switches the observable subscriptions.

**DataContext pitfall (the reason child wiring is explicit):** `CanonicalRecipeGridView` is `ReactiveUserControl<CanonicalRecipeGridSurface>` and binds concrete members; today it inherits the host's DataContext, which `MainWindow.axaml` binds to `{Binding RecipeGrid}`. Once `RecipeGrid` resolves to the router, inheritance would hand the canonical view a router instance and its `ViewModel` cast silently yields null — the grid never builds. Therefore:

`RecipeGridHost`:

- DataContext remains `{Binding RecipeGrid}` (now the router). The host casts it to `ActiveRecipeGridSurface`, subscribes `Orientation`, and swaps its content between the two views — setting each child view's `DataContext` **explicitly** to the matching concrete surface from the router's `CanonicalSurface`/`TransposedSurface` properties, never letting it inherit.
- `IsEditing` reads from whichever view is active.

`MainWindowViewModel`:

- Additionally receives the concrete `ActiveRecipeGridSurface` (same singleton the alias points to). Exposes a reactive `IsTransposedOrientation` pass-through for the menu check state and `ToggleOrientationCommand` delegating to `ToggleOrientation()`. No orientation state of its own.

`MainWindow.axaml.cs.OnKeyDown`:

- `Ctrl+Shift+T` invokes `ToggleOrientationCommand.Execute()`.

`RecipeMenuBar.axaml`, View menu:

- `MenuItem` with `ToggleType="CheckBox"`, `IsChecked="{Binding IsTransposedOrientation, Mode=OneWay}"`, command `ToggleOrientationCommand` — same idiom as the toolbar/notification-log toggles. Header via new localization resource entries.

### Config

`ui/grid_style.yaml` gains a top-level field:

```yaml
orientation: rows_as_steps   # or columns_as_steps; default rows_as_steps
```

Pipeline: `GridStyleLoader` deserializes into `GridStyleOptionsDto` (adding a DTO property auto-parses the field; the loader itself needs no change) → `GridStyleValidator` rejects unknown values → `GridStyleMapper` (`Configuration/Mapping/`) applies the default-when-absent and surfaces the value on the `GridStyleOptions` record (DI singleton). `ActiveRecipeGridSurface` reads the startup default from the record. **Round-trip requirement:** the in-app style editor (`View > Grid Style Settings`, `GridStyleEditorFacade` + `GridStyleWriter` + `GridStyleDtoMapper`) rewrites the whole file on save and re-validates the mapped DTO — `GridStyleDtoMapper` must always emit a valid orientation, or a style-editor save strips or corrupts the field.

## Technical Details

### Column tinting and selection visual

`ListBoxItem` containers get style classes `current-step`, `past-step`, `for-depth-1/2/3` stamped by `TransposedStepColumnClassBinder` (peer of `RecipeRowExecutionClassBinder`), attached via `ItemsControl.ContainerPrepared`/`ContainerClearing` so recycled containers re-bind correctly. Selection tint comes from the theme's `:selected` pseudo-class, overridden in `TransposedGridStyles.axaml` to use the palette's `selected` brushes. Read-only and inapplicable cells paint from the existing `colors.cells.readonly` / `colors.cells.disabled` palettes; changed cells from `colors.cells.changed`. All brushes come from the existing `CellPaletteInstaller` resources — no new palette keys.

### Container recycling

`VirtualizingStackPanel` recycles item containers during horizontal scroll. Two invariants to protect (validated in Task 0):

- An editor with uncommitted text must commit (or revert) when its container is recycled — `LostFocus` fires on recycle, which is the commit trigger.
- Style classes stamped by the class binder must be cleared on `ContainerClearing` and re-stamped on `ContainerPrepared`.

### Execution highlight

`TransposedExecutionHighlightTracker` subscribes to the coordinator's execution state and updates `IsCurrentStep` / `IsPastStep` on the matching `StepColumnViewModel` by step index, and clears all changed highlights on the inactive→active edge. Symmetrical to canonical's `ExecutionHighlightTracker` including both clear paths (deactivation clears step highlights; activation clears changed highlights).

### Read-only parameter

A parameter flagged `read_only: true` (e.g. `step_start_time`) yields `ParameterDescriptor.IsReadOnlyParameter=true`; cell construction picks the read-only cell kind for that row in every column, painted with the read-only palette.

## Implementation Steps

### Task 0: Thin-slice spike — validate the ListBox column architecture

**Files:** scratch project or throwaway branch commit; only the «Decision notes» block below lands in this file.

A 0.5–1 day vertical slice with hard-coded VMs: 4 parameter rows × 30 step-columns in the ListBox layout from Solution Overview.

- [x] Horizontal `VirtualizingStackPanel` realizes only visible columns (verify with live visual-tree count).
- [x] Click selects the whole column via `:selected`; `SelectionMode="Multiple"` + Ctrl/Shift works.
- [x] Editors: type into a `TextBox` cell, scroll the column out of view and back — value committed, no state leaked into recycled containers.
- [x] Append/remove a column mid-scroll — selection and scroll position survive.
- [x] Record a short «Decision notes» block in this plan (element counts, any recycling surprises). If recycling breaks editor state irreparably, fall back to `VirtualizationMode.None` for the prototype scale and note the ceiling.

**Decision notes (2026-07-09, headless Avalonia spike — 6 `[AvaloniaFact]` tests, 4 parameter rows × 30 step-columns, 100 px columns in a 460 px window, throwaway file deleted after this record):**

- **Virtualization confirmed viewport-bound.** Initial realization: 4 `ListBoxItem` containers of 30 (16 live `TextBox` editors = 4 columns × 4 params). After scrolling to column 15 and to the end: still 4 realized (mid-scroll one of them is a hidden recycle-pool container). Realized count never grew with collection size — the architecture's core claim holds.
- **Selection works as designed.** Header click stamps `IsSelected` + `:selected` pseudo-class on the container; Ctrl-click adds a disjoint column; Shift-click extends the range — all native `SelectionMode="Multiple"` behaviour, no custom code.
- **One selection surprise:** a click landing *inside a `TextBox` cell* does **not** select the column — `TextBox` handles the pointer press before it bubbles to the `ListBox`. The real view (Task 4) needs an explicit pointer-pressed hook (tunnel handler or handledEventsToo) on the cell panel to satisfy the "cell click selects the whole step-column" constraint. `ComboBox` cells will need the same treatment.
- **Recycling is editor-safe with `UpdateSourceTrigger.LostFocus`.** Typed-but-uncommitted text stays out of the VM while typing; recycling the focused container fires `LostFocus`, which commits the pending text to the VM. No stale editor text appeared in any recycled container (unique per-cell seed values verified across the full scroll range), and scrolling back re-realized the column with the committed value. No `VirtualizationMode.None` fallback needed.
- **Mutations mid-scroll are stable.** With column 15 selected and the viewport mid-recipe, appending and removing an off-viewport column preserved the selected index, the selected item identity, and the horizontal scroll offset exactly.

### Task 1: Introduce `ParameterDescriptor` and `StepColumnViewModel`

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ParameterDescriptor.cs`
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/StepColumnViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/StepColumnViewModelTests.cs`

- [x] `ParameterDescriptor` built from `RecipeMetadataRegistry` column definitions: key, display name, `ColumnType`, read-only flag — ordered exactly as canonical `ColumnBuilder` orders columns.
- [x] `StepColumnViewModel` wraps a `RecipeRowViewModel` (constructed the same way canonical constructs it, including `BuildInapplicableColumns`), owns its disposal, exposes `Cells` and template pass-throughs (`StepNumber`, `IsCurrentStep`, `IsPastStep`, `ForDepth`). A minimal `ParameterCellViewModel` abstract base (row + descriptor skeleton) was created so `Cells` compiles; Task 2 fleshes it out.
- [x] Tests: descriptor order matches registry order; pass-through notifications fire; disposal cascades to the wrapped row VM.
- [x] Run tests.

### Task 2: Introduce the `ParameterCellViewModel` adapter hierarchy

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ParameterCellViewModel.cs` (abstract base)
- Create: concrete kinds beside it — action combo, target/selector combo, property text, read-only (one class per file)
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/ParameterCellViewModelTests.cs`

- [x] Each cell adapts `(RecipeRowViewModel, ParameterDescriptor)`: exposes `Value` (INPC wrapper over the row indexer — templates never bind `[key]` directly), `IsApplicable`, `IsChanged`, combo `Items` where applicable. Cell kind chosen from `ParameterDescriptor.ColumnType` covering all `ColumnTypes` (`action_combo_box`, `action_target_combo_box`, `property_field`, `step_start_time_field`, `text_field`).
- [x] All cell writes call the row VM's single public write path — `SetPropertyValue(columnKey, value)` / the indexer setter — which internally decides action vs. selector vs. property and raises the matching event (`ActionChanged`, `SelectorValueChanged`, `PropertyValueChanged`) for the surface to observe. No new write machinery, no calls into row-VM privates.
- [x] Row-VM `PropertyChanged`/indexer notifications propagate to `Value`, `IsApplicable`, `IsChanged` on the adapters.
- [x] Tests: value round-trip raises INPC; `IsApplicable`/`IsChanged` track the row VM; each write path reaches a stub with the right payload; time/unit format kinds surface for the template converters.
- [x] Run tests.

### Task 3: Implement `TransposedRecipeGridSurface`

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridSurface.cs`
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedExecutionHighlightTracker.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedRecipeGridSurfaceContractTests.cs` (subclass of `RecipeGridSurfaceContractTests` — all 13 cases)
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs` (add `CreateTransposedSurface`)

- [x] Implements `IRecipeGridSurface` exactly (no extra public members). `CanDeleteStep` built as `WhenAnyValue(...SelectedStepIndices).Select(count > 0).DistinctUntilChanged()`; `EditorMustClose` on false→true `IsReadOnly` edges only; post-`Dispose` calls are no-ops.
- [x] Owns `ObservableCollection<StepColumnViewModel> StepColumns`, `ParameterDescriptors`, the highlight tracker, `_selectionRequests` subject (deliberately not disposed — guarded no-op after `Dispose`, same as canonical).
- [x] Full `MutationSignal` dispatch per the Data-flow table, including `StepsRemoved` (descending) and `StateRefreshed` (ignore). `PropertyUpdated` refreshes all cells of the column — add a test where a formula-coupled second cell updates in the same mutation (simulated via a session-level write standing in for a formula recalc; no yaml test config carries a valid `formula:` block).
- [x] After structural mutations: renumber subsequent columns (mirror `RenumberRows` inside the structural handlers), then run the shared tail — reconcile selection with the new column count (mirror `ReconcileSelectionWithRows`: drop indices past the shrunken collection), refresh step start times, refresh loop depths.
- [x] Test: delete the last selected step → `SelectedStepIndices` no longer contains the stale index and `CanDeleteStep` reflects the reconciled selection.
- [x] Changed-highlight parity: `MarkChanged` on action-change rebuild, `ApplyChangedDelta` on selector edit, `ClearChanged` on successful cell edit, `ClearAllChanged` via the tracker on execution start.
- [x] Run all 13 contract cases against `TransposedRecipeGridSurface`; re-run canonical's contract subclass to confirm no divergence.

### Task 4: `TransposedRecipeGridView` UserControl — layout, tinting, selection

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`)
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedStepColumnClassBinder.cs`
- Create: `SemiStep/SemiStep.UI/Styles/TransposedGridStyles.axaml`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedRecipeGridViewTests.cs`

- [x] Implement the Solution-Overview structure: name column + `ListBox` with horizontal `VirtualizingStackPanel`, fixed cell heights from `layout.row_height`, fixed uniform column width, header cell + marker per column.
- [x] Apply config fonts via `GridFontApplier`/`GridFonts` resources (cell/header roles, tabular figures) — no hardcoded typography.
- [x] Read-only and inapplicable cells paint from the existing palettes; grid line/background brushes from the shared resources.
- [x] `TransposedStepColumnClassBinder` stamps execution classes on containers via `ContainerPrepared`/`ContainerClearing`.
- [x] `ListBox.SelectionChanged` → sorted step indices → `surface.UpdateSelection`; subscribe `surface.SelectionRequests` → select item + `BringIntoView`, `null` clears.
- [x] Headless tests: 3-step recipe renders 3 columns + name column; click second column → `surface.SelectedStepIndex == 1`; `IsCurrentStep=true` puts `current-step` class and shows the marker; depth class swaps with `ForDepth`.
- [x] Run tests.

Task-4 implementation notes: `TransposedRecipeGridSurface` gained a `GridStyleOptions` constructor dependency (`GridStyle` property) — the view's style carrier, analog of canonical's surface-owned `ColumnBuilder`. ReactiveUI activation fires on `Loaded` (after the first layout pass), so the view stamps already-realized containers retroactively when wiring `ContainerPrepared`. The current-step marker sits in a fixed 4 px slot so the current column stays row-aligned with idle columns. The step-number header uses the cell font (parity with canonical's numbering column); the parameter-name column uses the header font (parity with canonical headers). No valid test config carries a `read_only: true` column, so the headless test asserts the read-only cell class mirrors the descriptor flag; read-only painting gets covered by Task 9 manual smoke on RIE.

### Task 5: Editing wiring

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`)
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedEditingTests.cs`

- [x] Cell templates: `ContentControl` + `DataTemplates` dispatch over the cell-VM kinds (action combo / target combo / property text / read-only), two-way `Value` binding through `PropertyTimeMultiConverter` / `PropertyTimeEditingConverter`, `IsEnabled` gated on `IsApplicable` and surface `IsReadOnly` (combos additionally non-hit-testable when blocked, matching canonical).
- [x] Changed-cell painting from `IsChanged` + click-away clear: pointer-pressed hook resolves the pending changed cell (mirror `ChangedCellClickResolver` semantics, including the deliberate absence of a read-only guard).
- [x] Subscribe `surface.EditorMustClose`: move focus off the active editor to force commit/close.
- [x] Expose `IsEditing` (an always-live editor holds keyboard focus) for the host chain that gates window shortcuts.
- [x] Headless tests: type-and-commit updates the coordinator; time cell renders `HH:MM:SS` and parses back; inapplicable cell is disabled; read-only mode disables editors and `EditorMustClose` defocuses an active one; changed cell clears on click-away.
- [x] Run tests.

Task-5 implementation notes: templates are built in code (`TransposedCellTemplateFactory`, peer of `TextCellFactory`/`ComboBoxCellFactory`) because `PropertyTimeEditingConverter` bakes a per-cell format kind, and installed into the view's `DataTemplates` from the constructor (WhenActivated fires after the first layout pass — too late). The tunnel pointer-pressed hook on the ListBox also selects the pressed cell's column on unmodified clicks (the spike's "TextBox swallows pointer presses" finding). Enter commits by moving focus off the editor (`LostFocus` is the binding trigger); `EditorMustClose` reuses the same defocus path. Avalonia 12 has no `FocusManager.ClearFocus()` — the clear-focus API is `IFocusManager.Focus(null)`. Headless pointer tests must install `CellPaletteInstaller` resources on the test window: without the palette the cell borders have null backgrounds and are not hit-testable, so presses over disabled editors fall through to the item container.

### Task 6: Orientation switching — router, host, DI

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/ActiveRecipeGridSurface.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridHost.axaml` (+ `.axaml.cs`)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/UiDi.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs` (`CreateMainWindowViewModel` gains the router dependency)
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ActiveRecipeGridSurfaceTests.cs`

- [x] `ActiveRecipeGridSurface` implements `IRecipeGridSurface`, owns reactive `Orientation` (startup default from `GridStyleOptions`) + `ToggleOrientation()`, exposes `CanonicalSurface`/`TransposedSurface`, delegates to the active surface, re-emits the three observables across swaps (switch-subscription), transfers `SelectedStepIndices` on flip. `Initialize()` fans out to both concrete surfaces (see Toggle wiring for why). The router does not dispose the surfaces — they are container-owned singletons; DI disposes them.
- [x] DI: register both concrete surfaces + the router (as itself and as the `IRecipeGridSurface` alias); interface consumers (`RecipeCommandsViewModel`, `ClipboardViewModel`) stay untouched.
- [x] `MainWindowViewModel`: receives the concrete router; reactive `IsTransposedOrientation` pass-through and `ToggleOrientationCommand` delegating to `ToggleOrientation()`. No orientation state of its own.
- [x] `RecipeGridHost`: casts DataContext to the router, subscribes `Orientation`, swaps content between the two views, and sets each child view's `DataContext` explicitly to the matching concrete surface (see the DataContext pitfall in Toggle wiring — inheritance would null out `ReactiveUserControl<T>.ViewModel`). `IsEditing` reads the active view.
- [x] Headless tests: flip orientation → host child type changes, the child's DataContext is the matching concrete surface, and the router delegates to the other surface; the newly active surface's `StepCount` equals the recipe's step count (proves the `Initialize()` fan-out); selection on a step survives the flip; `CanDeleteStep` subscribers keep receiving values after a swap.
- [x] Run tests.

Task-6 implementation notes: `grid_style.yaml` carries no orientation field yet, so `ActiveRecipeGridSurface.ReadStartupOrientation(GridStyleOptions)` is the Task-8 seam — it takes the record and currently always returns `Canonical`. `ToggleOrientation` transfers the selection to the incoming surface *before* flipping `Orientation`, so switch-subscribers re-attach to a surface whose `CanDeleteStep` already reflects the carried-over selection (no transient false). The host constructs both child views eagerly and keeps them alive across flips; it wires child DataContexts and the orientation subscription in `OnDataContextChanged` (router arrives via the `{Binding RecipeGrid}` DataContext, not the constructor).

### Task 7: Toggle menu item + hotkey

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeMenuBar.axaml`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Modify: localization resx (new `MenuViewTransposedGrid` entries)
- Create: `SemiStep/SemiStep.Tests/UI/MainWindow/ToggleOrientationCommandTests.cs`

- [x] View menu gains a `MenuItem` with `ToggleType="CheckBox"`, `IsChecked` bound to the transposed state, command `ToggleOrientationCommand` — same idiom as the toolbar/notification-log toggles. Header from a new localization resource.
- [x] In `MainWindow.axaml.cs.OnKeyDown`, add `Key.T when e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)` (respecting the `IsEditing` gate pattern of the surrounding shortcuts).
- [x] Tests: hotkey and menu command both flip the router's `Orientation` (observable via `IsTransposedOrientation`).
- [x] Run tests.

### Task 7.5: Arrow-key navigation for the transposed semantic

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedNavigationTests.cs`

The operator's mental model: Right = next step (column), Down = next parameter (cell below).

- [x] Register `AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel)` on the view. `Left`/`Right` move column selection (and focus to the same parameter row in the neighbour column); `Up`/`Down` move focus between cells within the column; `e.Handled = true` when consumed. Keys inside an open `ComboBox` dropdown and text-caret movement inside a focused `TextBox` are left alone.
- [x] Tests `[AvaloniaFact]`: 3×3 grid, focus a cell in column 0, synthesise `Key.Right` → focus/selection in column 1, same row; `Key.Down` → next parameter row, same column.

Task-7.5 implementation notes: arrow keys are consumed even when the move is an edge no-op — letting them fall through would reach a closed `ComboBox`, which cycles its value on arrows. `Up`/`Down` skip non-focusable rows (read-only or inapplicable cells) in the travel direction; when the neighbour column's same-row editor is not focusable, `Left`/`Right` fall back to focusing the `ListBoxItem` container so native list navigation stays available. `Up`/`Down` from a `TextBox` navigate (committing the pending edit via the LostFocus trigger); only `Left`/`Right` are reserved for the caret.

### Task 8: Config field `orientation` in `grid_style.yaml`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`, `Dto/GridStyleOptionsDto.cs`, `Mapping/GridStyleMapper.cs`, `Mapping/GridStyleDtoMapper.cs`, `Validation/GridStyleValidator.cs` (`GridStyleLoader` needs no change — the DTO property auto-parses)
- Modify: `ConfigFiles/RIE/ui/grid_style.yaml` — set `orientation: columns_as_steps`.
- Confirm: `ConfigFiles/MOCVD/ui/grid_style.yaml`, `ConfigFiles/MBE/ui/grid_style.yaml` — absent field defaults to `rows_as_steps`.
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleOrientationTests.cs`

- [x] New top-level DTO property; `GridStyleValidator` rejects unknown values with a config error; `GridStyleMapper` applies the `rows_as_steps` default when absent and surfaces the value on the `GridStyleOptions` record.
- [x] `ActiveRecipeGridSurface` (Task 6) consumes the record value as the startup default — verify the wiring end to end.
- [x] **Writer round-trip:** `GridStyleWriter`/`GridStyleDtoMapper` serialize the field back, so a style-editor save does not strip it (test: load → save → reload keeps the value).
- [x] Tests: both values accepted; unknown value errors; missing field defaults; round-trip preserved.
- [x] Run tests.

Task-8 implementation notes: the record carries a Core enum `GridOrientation` (not the raw string), parsed/serialized through `GridOrientationValues` (`Configuration/Dto/`) — `GridStyleDtoMapper` therefore always emits a valid value, and a save on a config that never had the field writes the explicit `orientation: rows_as_steps`. The DTO property is declared last so serialized output keeps `fonts:` as the first key (pinned by `Save_InjectsNoHeader_WhenSourceHasNone`). The style-editor round-trip needs no editor change: `GridStyleEditorViewModel.BuildRecord` rebuilds via `with` over the seeded source, so the unsurfaced `Orientation` is preserved. End-to-end wiring is covered by `ActiveRecipeGridSurfaceTests.StartupOrientation_ColumnsAsSteps_StartsTransposed`.

### Task 9: Verify acceptance criteria and measure performance

- [x] All 13 contract-test cases pass against `TransposedRecipeGridSurface` — `TransposedRecipeGridSurfaceContractTests` 13/13; canonical subclass re-run 13/13, no divergence.
- [x] Cell click selects the entire step-column — headless equivalents: `TransposedEditingTests.CellClick_SelectsWholeColumn` (press inside a cell → `SelectedStepIndex` + container `IsSelected`), `TransposedRecipeGridViewTests.ClickOnSecondColumnHeader_SelectsStepOne`; on-screen `:selected` tint deferred to user verification.
- [x] Current-step marker + past/depth tinting — headless equivalents: `TransposedRecipeGridViewTests.CurrentStep_StampsClassOnContainer_AndShowsMarker`, `ForDepth_Toggle_SwapsDepthClassOnContainer`, `TransposedRecipeGridSurfaceTests.ExecutionStart_ClearsChangedHighlights_AndStampsCurrentAndPastColumns`; visual layering during a live run deferred to user verification.
- [x] Changed-cell highlight — headless equivalents: `TransposedEditingTests.ActionCombo_SelectionChange_ChangesStepAction_AndMarksCells`, `TransposedRecipeGridSurfaceTests.SuccessfulCellEdit_ClearsChangedFlag`, `TransposedEditingTests.ChangedCell_ClearsOnClickAway`, `TransposedRecipeGridViewTests.CellBorders_CarryReadOnlyInapplicableAndChangedClasses` (orange = `changed` class → palette brush); on-screen paint deferred to user verification.
- [x] Time cells + step-start-time — headless equivalents: `TransposedEditingTests.TimeCell_RendersHms_AndParsesBack`, `ParameterCellViewModelTests.Cell_FormatKindAndUnits_SurfaceForTemplateConverters`, `TransposedRecipeGridSurfaceTests.StepStartTimes_RefreshedAfterMutation`, `ParameterCellViewModelTests.StepStartTimeCell_ValueFollowsRowStepStartTime`.
- [x] Parameter-name column stays put during horizontal scroll — true by construction: the name column is a `DockPanel` sibling outside the `ListBox`'s horizontal `ScrollViewer`, so the offset cannot move it; the perf probe's scroll-offset changes re-realized only `ListBox` containers. Visual smoke deferred to user verification.
- [x] Arrow-key semantic matches Task 7.5 — `TransposedNavigationTests` (9 cases); manual feel-check deferred to user verification.
- [x] Edit in transposed → canonical persists; selection survives the round trip — new test `ActiveRecipeGridSurfaceTests.EditInTransposed_PersistsToCanonical_AndSelectionSurvivesRoundTrip`.
- [x] Toggle menu item + Ctrl+Shift+T both flip orientation — `ToggleOrientationCommandTests` (menu command both ways, hotkey once and twice).
- [x] CSV save in transposed mode produces the canonical layout — CSV export reads the coordinator's canonical recipe model and never touches a view; all CSV tests pass in the full-suite run.
- [x] **Performance probe with hard gate:** measured headless at 50 / 100 / 200 steps — see «Performance Notes» below. **Gate at 100 steps: PASS** (edit-commit 24 ms, incremental scroll 45 ms, both under 100 ms). Probe deleted after recording (hardcoded scratch output path; same lifecycle as the Task 0 spike).
- [x] Full test suite green — 1236/1236.

**Performance Notes (2026-07-10, headless `[AvaloniaTheory]` probe — debug build, headless renderer, 1200×800 window, WithGroups test config, JIT warm-up pass before each measurement; two runs, numbers consistent):**

| Steps | Projection build (`Initialize`) | First layout (`Show` + jobs) | Realized containers | Edit-commit (Enter → committed) | Incremental scroll (one column) | Jump scroll to end |
| ----- | ------------------------------- | ---------------------------- | ------------------- | ------------------------------- | ------------------------------- | ------------------ |
| 50    | 0.4 ms                          | 288 ms                       | 12                  | 5.2 ms                          | 23.3 ms                         | 307 ms             |
| 100   | 8.4 ms                          | 292 ms                       | 12                  | 24.2 ms                         | 45.2 ms                         | 305 ms             |
| 200   | 3.7 ms                          | 330 ms                       | 12                  | 13.0 ms                         | 13.2 ms                         | 207 ms             |

- **Nothing scales with step count.** Realized container count is 12 at every size and every scroll position (initial, end, after jump-back) — the viewport-bound claim from the Task 0 spike holds at 200 steps with the full cell-template stack.
- **Gate verdict (100 steps): PASS.** Edit-commit 24 ms and one-column scroll 45 ms are under the 100 ms perceived-lag gate with headroom; both are measured under a debug build and a headless renderer, so release numbers only improve.
- **The two ~300 ms figures are one-off, size-independent viewport realizations,** not per-frame lag: first layout and a full-extent jump (scrollbar teleport) each generate one fresh viewport of 12 columns × full cell templates. They stay flat from 50 to 200 steps, confirming per-viewport cost rather than per-recipe cost. Interactive scrolling pays the incremental figure (13–45 ms per column).
- **No cap needed.** The architecture shows no structural ceiling at the probed scale; extrapolation to the original ~1000-step requirement holds since no measured cost grows with the collection.

### Task 10: Final — close-out

- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] Update `Docs/architecture/recipe-grid-surface.md`: second implementation, the router, orientation switching; resolve the recorded `ColumnBuilder`-in-surface trade-off note. Update `Docs/architecture/grid-style-configuration.md` with the `orientation` field.
- [x] Move this plan to `Docs/plans/completed/`. (harness moves it)

## Post-Completion

**Scale follow-up:** the ListBox architecture is viewport-bound, so the ~1000-step requirement needs no cap in principle; Task 9's notes confirm or refute this with data before any `max_steps_for_transposition` discussion.

**Manual verification:**

- Open RIE config — defaults to transposed; exercise edit/copy/paste/save.
- Open MOCVD config — defaults to canonical; toggle to transposed and back; confirm both views reflect the same recipe state.
- Run a recipe on PLC (or synthetic execution): observe current-step marker tracking column position; verify past-step tinting layered correctly with depth tinting; verify execution start clears changed highlights in transposed.
- Save styles from the in-app editor (`View > Grid Style Settings`) and confirm `orientation` survives in the yaml.
