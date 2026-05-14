# ComboBox CellTemplate migration + recycling flip

## Overview

Fix a production-blocking regression introduced by the Avalonia 11 → 12 upgrade: ComboBoxes inside `DataGridTemplateColumn.CellEditingTemplate` no longer respond to clicks at all. This is `AvaloniaUI/Avalonia.Controls.DataGrid#236` — closed without resolution. The earlier NRE on first click (Round-7 + initial migration era) was fixed by setting `SelectedItem` synchronously in the editing-template lambda, but the popup-never-opens issue is structural in Avalonia 12.0.0 and cannot be patched within the `CellEditingTemplate` path.

The canonical Avalonia 12 idiom — confirmed by official samples (only `NumericUpDown` is shown in `CellEditingTemplate`, never `ComboBox`), avalonia-docs, and accepted community answers in discussions #7086 / #14103 — is to put interactive ComboBoxes directly in `CellTemplate` with the column marked `IsReadOnly = true`. The cell does not go through DataGrid's edit-mode lifecycle at all; the ComboBox lives in the visual tree from row materialization, owns its pointer events, and opens its popup on the first click.

In the same pass we close the Round-7-deferred recycling work. Round-7 explicitly left `supportsRecycling: false` on five templates because they captured `row` (action id, group items list, `SelectionChanged` write-back lambda). Round-7 fixed the strong-ref leak via TwoWay binding but kept `supportsRecycling: false` pending closure elimination. Switching to CellTemplate-only ComboBoxes eliminates the remaining `row` captures structurally — the lambda becomes a pure UI-shape factory, all per-row state flows through bindings against the cell's DataContext. `supportsRecycling: true` becomes safe.

## Context (from discovery)

Files involved:
- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` — rewrite. Today: 4 templates (display + edit × action + group). After: 2 templates (CellTemplate × action + group).
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — extend. Add a `GroupItemsByColumn` lookup so the group ComboBox binds `ItemsSource` instead of capturing the items list in a closure.
- `SemiStep/SemiStep.UI/RecipeGrid/CellPresenter.cs` — likely unchanged. Already exposes `:cell-enabled/:cell-readonly/:cell-disabled` pseudo-classes for cell-state styling.
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — no logic change. `CreateActionColumn` / `CreateGroupComboBoxColumn` keep their signatures; the column shape they produce changes inside the factory.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — likely unchanged. The `:cell-disabled > ComboBox` selector already hides the inner ComboBox; remains valid.
- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemSelectionConverter.cs` — unchanged. Same TwoWay int ↔ `ComboBoxItemViewModel` mapping.
- `SemiStep/SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs` — verify still passes; may need an assertion on `IsReadOnly` if added.
- `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`, `RecipeMutationCoordinatorTests.cs` — don't touch the factory; expected to pass unchanged.
- `Docs/07-non-functional.md` — append Round-8 subsection summarizing this migration.

Related patterns found:
- `RecipeRowViewModel` already exposes `IReadOnlyDictionary<string, CellState>` (`CellStates`), `IReadOnlyDictionary<string, string?>` (`ColumnUnits`), `IReadOnlyDictionary<string, string>` (`ColumnFormatKinds`) — bindable via `[columnKey]` indexer paths. The new `GroupItemsByColumn` follows the same shape.
- `RecipeRowViewModel.UpdateStep` raises `PropertyChanged("Item[]")` to invalidate all indexer-path bindings on row mutation. Same mechanism continues to work.
- On `MutationSignal.StepActionChanged`, `RecipeGridViewModel.RebuildRow` disposes the old VM and constructs a new one with the new action — the new VM has new `GroupItemsByColumn` content. DataGrid sees `CollectionChanged.Replace` and re-renders the row from the CellTemplate against the new DataContext.

Dependencies identified:
- No package bumps. Avalonia 12.0.3 + `Avalonia.Controls.DataGrid 12.0.0` stay as-is.
- No DI registration changes. No new services.
- No XAML changes outside templates produced by the factory.

## Development Approach

- **Testing approach: Regular.** Avalonia headless does not simulate hit-testing, so the canonical observable — "click cell → dropdown opens on first click" — is not unit-testable. Tests verify what they can: factory contract (column has `CellTemplate`, no `CellEditingTemplate`, `IsReadOnly = true`), and indirect data-flow (action change still triggers `ActionChanged` event with new id). Click flow is manual verification at the end.
- Complete each task fully before moving to the next. Build green and existing 309 tests pass after every task.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- One commit per task (5 implementation commits + 1 verify + 1 docs/archive). No bundling — each task is self-contained.

## Testing Strategy

- **Unit tests stay at 309+.** The existing 309 tests already cover the behavioural surface this migration affects: action-change wiring, row rebuild on `StepActionChanged`, mutation-coordinator flows. They are the regression net for binding-equivalence — if our new binding shape diverges from current semantics, those tests fail.
- **New assertions (Task 5):** add explicit `IsReadOnly == true` assertion on ComboBox columns built by `ColumnBuilder`, and shape-of-CellTemplate assertions where headless can introspect.
- **No new e2e tests.** Project has no UI-based e2e harness today.
- **Manual UI smoke (mandatory before PR open):**
  1. Launch app against a real recipe.
  2. Click an action cell → ComboBox dropdown opens **on the first click**.
  3. Pick a different action → row rebuilds, group columns show the new groups, no flicker beyond the rebuild itself.
  4. Click a group ComboBox cell → dropdown opens on the first click.
  5. Set a row to a disabled state (via cell-state metadata) → ComboBox renders disabled, click does nothing.
  6. Open a recipe and start it on PLC (`RecipeActive = true`) → all ComboBoxes disable; click does nothing. Stop the recipe → ComboBoxes re-enable.
  7. Scroll a recipe with ≥100 rows continuously for ~30s. With recycling on, gen-0 churn should drop noticeably vs Round-7 baseline. Memory should plateau.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

Replace `DataGridTemplateColumn { CellTemplate=TextBlock, CellEditingTemplate=ComboBox }` with `DataGridTemplateColumn { CellTemplate=ComboBox, IsReadOnly=true }` for action and group columns. The ComboBox is always materialized in the cell. DataGrid never enters edit mode for these columns, so the broken edit-template path is bypassed entirely.

For the recycling flip to be safe, the template lambda must not capture `row`. Today it captures `row` for two purposes:
1. Computing initial `SelectedItem` (group items list + selected id resolution) — eliminated by binding `SelectedItem` to `[columnKey]` via the existing `ComboBoxItemSelectionConverter`.
2. Resolving the group items list (`GetOrCreateGroupItems(row, columnKey)`) — eliminated by exposing `row.GroupItemsByColumn[columnKey]` as a bindable property; ComboBox binds `ItemsSource` to that path. The dictionary is built once at row construction (same point where `ColumnUnits` and `ColumnFormatKinds` are built), since the action is fixed for the lifetime of a row VM (`MutationSignal.StepActionChanged` always rebuilds the row).

For disable-when-grid-readonly, the ComboBox binds `IsEnabled` to a multi-source: cell state (from row indexer + converter) AND DataGrid's `IsReadOnly` (via `RelativeSource` ancestor). Column-level readonly is folded in as a static factor at template-build time.

## Technical Details

### New row property — `RecipeRowViewModel.GroupItemsByColumn`

```csharp
public IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn { get; }
```

Built once at row construction by iterating `action.Properties`, resolving each property's `GroupName` through `recipeMetadataRegistry.GetGroup`, materializing the items list with `ComboBoxItemViewModel` projections. For columns with no group (string/numeric properties), the dictionary omits the key entirely — bindings against missing keys yield null which Avalonia treats as empty `ItemsSource`. Lifecycle is identical to `ColumnUnits` / `ColumnFormatKinds`.

The factory-level `_groupItemsByGroupName` cache in `ComboBoxCellFactory` is removed (Task 4). **Deliberate allocation regression:** previously the cache shared group lists across all rows with the same action; now each row builds its own. Group sizes are small (<20 items typically) and the trade is justified by closure elimination — without it `supportsRecycling: true` is unsafe. If post-Round-8 profiling shows this dominates allocation, a registry-level cache can be reintroduced (the items projection is a pure function of `ActionDefinition` and the registry's group data).

### Action ComboBox template (CellTemplate, recycling enabled)

```csharp
return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
{
    var comboBox = new ComboBox
    {
        ItemsSource = _cachedActionItems, // global, same for every row
        DisplayMemberBinding = new Binding("DisplayText"),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
    };

    comboBox.Bind(ComboBox.SelectedItemProperty,
        new Binding(ColumnTypes.ActionIndexerBindingPath)
        {
            Mode = BindingMode.TwoWay,
            Converter = _actionSelectionConverter,
        });

    comboBox.Bind(ComboBox.IsHitTestVisibleProperty, BuildHitTestVisibleBinding(ColumnTypes.Action, isColumnReadOnly));

    return CellPresenter.Wrap(comboBox, cellStateConverter);
}, supportsRecycling: true);
```

`_cachedActionItems` (action items) is global per `ComboBoxCellFactory` instance. With recycling on, the lambda runs once and the ComboBox is reused across rows. `InvalidateCaches()` is called only from `ColumnBuilder.BuildColumns` / `BuildColumnsFromConfiguration` immediately after `grid.Columns.Clear()` — clearing columns destroys all materialized cells, so no recycled cell ever sees a stale cache. Safe.

`BuildHitTestVisibleBinding(columnKey, isColumnReadOnly)` returns a `MultiBinding` combining:
- `CellStates[columnKey]` (cell state) — projected through a converter to `bool` (true when `Enabled`)
- `DataGrid.IsReadOnly` via `RelativeSource = new RelativeSource { Mode = FindAncestor, AncestorType = typeof(DataGrid) }` — negated in the multi-converter
- Static `isColumnReadOnly` factor — when true, short-circuit to a constant `false` binding (skip MultiBinding entirely)

**Visual semantics — keeping `IsHitTestVisible` over `IsEnabled` deliberately.** With `IsHitTestVisible = false` the ComboBox renders identically to the enabled state (same colors, same chrome) but ignores clicks — the current master behavior. With `IsEnabled = false` Avalonia's Fluent theme greys out the control. We preserve the current visual to avoid a UX regression for read-only/disabled cells. `:cell-disabled > ComboBox` style (DataGridStyles.axaml:63-65) still hides the ComboBox entirely for fully-disabled cells.

**RelativeSource note**: ancestor lookup walks the visual tree at binding-attach time. For recycled cells, the binding re-evaluates on each visual-tree attach. Standard Avalonia behavior — no special handling needed.

### Group ComboBox template (CellTemplate, recycling enabled)

```csharp
return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
{
    var comboBox = new ComboBox
    {
        DisplayMemberBinding = new Binding("DisplayText"),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
    };

    comboBox.Bind(ComboBox.ItemsSourceProperty,
        new Binding($"GroupItemsByColumn[{columnKey}]"));

    comboBox.Bind(ComboBox.SelectedItemProperty, BuildGroupSelectedItemBinding(columnKey));

    comboBox.Bind(ComboBox.IsHitTestVisibleProperty, BuildHitTestVisibleBinding(columnKey, isColumnReadOnly));

    return CellPresenter.Wrap(comboBox, cellStateConverter);
}, supportsRecycling: true);
```

`BuildGroupSelectedItemBinding(columnKey)` returns a TwoWay `MultiBinding` using a new converter `ComboBoxItemMultiSelectionConverter`:

- Source 0: `[columnKey]` — the int id from the row indexer.
- Source 1: `GroupItemsByColumn[columnKey]` — the row's items list for this column.
- `Convert(values)`: when `values[0] is int id` and `values[1] is IReadOnlyList<ComboBoxItemViewModel> items` → return `items.FirstOrDefault(x => x.Id == id)`. Otherwise `null`.
- `ConvertBack(value)`: when `value is ComboBoxItemViewModel item` → return `[item.Id, BindingOperations.DoNothing]` (write only to source 0). Otherwise `[BindingOperations.DoNothing, BindingOperations.DoNothing]`.

This is recycling-safe because both sources refresh on DataContext change. The action template keeps the existing `ComboBoxItemSelectionConverter` (single-binding) because its items list is global, captured in the lambda closure once.

`BuildHitTestVisibleBinding` is the helper introduced in Task 2 (reused as-is — same shape works for both action and group columns).

### Column shape change in `ColumnBuilder`

Inside `CreateColumn`, action and group columns return `DataGridTemplateColumn { CellTemplate = …, IsReadOnly = true, … }`. The numbering column and text columns are untouched.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): row-VM extension, factory rewrite, ColumnBuilder column-shape change, tests for the factory contract, doc update.
- **Post-Completion** (no checkboxes): manual UI verification scenarios listed in Testing Strategy.

## Implementation Steps

### Task 1: Add `GroupItemsByColumn` to `RecipeRowViewModel`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [ ] Add a private `_groupItemsByColumn` backing field populated by a new `BuildGroupItemsByColumn(ActionDefinition, RecipeMetadataRegistry)` static helper, called from the constructor next to the existing `BuildColumnMetadata` call.
- [ ] Expose public `IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn { get; }`.
- [ ] The helper iterates `action.Properties`; for each property with a non-null `GroupName`, resolves the group via `recipeMetadataRegistry.GetGroup`; on success materializes a `List<ComboBoxItemViewModel>` (`new(kvp.Key, kvp.Value)`) ordered by `Id`; on failure inserts an empty list. For properties without a group, omit the key entirely — bindings against missing keys return null which Avalonia treats as empty `ItemsSource`.
- [ ] Write tests: row with at least one group-bound property exposes a non-empty list under the property key; row with a non-group property has no key for that property; row with an action whose group does not resolve has an empty list under the key (negative path).
- [ ] Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 309/309+ pass.

### Task 2: Rewrite action ComboBox template (CellTemplate, recycling on)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Possibly create: `SemiStep/SemiStep.UI/RecipeGrid/CellStateToBoolConverter.cs` (for the hit-test-visible multi-converter component, if no existing converter fits)
- Possibly create: `SemiStep/SemiStep.UI/RecipeGrid/HitTestVisibleMultiConverter.cs` (multi-converter for the IsHitTestVisible binding)

- [ ] In `ComboBoxCellFactory`, replace `CreateActionColumn` body to build a `DataGridTemplateColumn { Header, Tag, Width, IsReadOnly = true, CanUserSort = false, CellTemplate = CreateActionCellTemplate(columnDef.ReadOnly) }`. Remove `CellEditingTemplate` assignment.
- [ ] Replace `CreateActionDisplayTemplate` and `CreateActionEditingTemplate` with a single private `CreateActionCellTemplate(bool isColumnReadOnly)` returning a `FuncDataTemplate<RecipeRowViewModel>` with `supportsRecycling: true`. No closure over `row`. ComboBox binds `SelectedItem` TwoWay via `ComboBoxItemSelectionConverter` (action items captured as a closure constant — same for every row, never mutates per-row).
- [ ] Add `BuildHitTestVisibleBinding(string columnKey, bool isColumnReadOnly)` private helper. Returns either a constant `false` binding (when `isColumnReadOnly == true`) or a `MultiBinding` combining `CellStates[columnKey]` projected to bool (Enabled → true, else false) AND negated `DataGrid.IsReadOnly` from a `RelativeSource = new RelativeSource { Mode = FindAncestor, AncestorType = typeof(DataGrid) }`. The multi-converter ANDs the two sources.
- [ ] Add unit tests for `HitTestVisibleMultiConverter.Convert`: (Enabled, gridReadOnly=false) → true; (Enabled, gridReadOnly=true) → false; (Disabled, _) → false; (Readonly, _) → false. ConvertBack is OneWay-only; return `BindingOperations.DoNothing` or throw `NotSupportedException`.
- [ ] Run `dotnet build` + `dotnet test` — all green.

### Task 3: Introduce `ComboBoxItemMultiSelectionConverter` and rewrite group template

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs`
- Create: `SemiStep/SemiStep.Tests/UI/ComboBoxItemMultiSelectionConverterTests.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`

- [ ] Create `ComboBoxItemMultiSelectionConverter` implementing `IMultiValueConverter`. `Convert(values, ...)`: source 0 is the int id, source 1 is `IReadOnlyList<ComboBoxItemViewModel>` — return matching item or null. `ConvertBack(value, targetTypes, ...)`: when `value is ComboBoxItemViewModel item` return `new object?[] { item.Id, BindingOperations.DoNothing }`; otherwise both `DoNothing`.
- [ ] Write tests: round-trip (int 7 with list → matching VM → int 7 back), missing id (int 99 not in list → null), null inputs, non-int source, ConvertBack with non-ComboBoxItemViewModel input → DoNothing.
- [ ] Replace `CreateGroupComboBoxColumn` body to set `IsReadOnly = true` and only `CellTemplate = CreateGroupCellTemplate(columnDef.Key, columnDef.ReadOnly)`. Drop `CellEditingTemplate` assignment.
- [ ] Replace `CreateGroupDisplayTemplate` and `CreateGroupEditingTemplate` with a single `CreateGroupCellTemplate(string columnKey, bool isColumnReadOnly)` returning a `FuncDataTemplate<RecipeRowViewModel>` with `supportsRecycling: true`. No closure over `row`.
- [ ] ComboBox binds `ItemsSource` to path `GroupItemsByColumn[<columnKey>]` (string built once at template-build time).
- [ ] ComboBox binds `SelectedItem` TwoWay using a `MultiBinding` with the new `ComboBoxItemMultiSelectionConverter`: source 0 is `[<columnKey>]`, source 1 is `GroupItemsByColumn[<columnKey>]`.
- [ ] ComboBox binds `IsHitTestVisible` via `BuildHitTestVisibleBinding(columnKey, isColumnReadOnly)` from Task 2.
- [ ] Run `dotnet build` + `dotnet test` — all green.

### Task 4: Remove dead code (factory + row VM)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [ ] In `ComboBoxCellFactory`: confirm `CreateActionDisplayTemplate`, `CreateActionEditingTemplate`, `CreateGroupDisplayTemplate`, `CreateGroupEditingTemplate`, `ResolveGroupDisplayText`, `GetOrCreateGroupItems`, and the `_groupItemsByGroupName` field are all removed (most should already be gone from Tasks 2–3; this task is the audit pass). `InvalidateCaches()` keeps only `_cachedActionItems = null`.
- [ ] In `RecipeRowViewModel`: remove `GetGroupNameForColumn(string columnKey)` and `GetGroupItemsForColumn(string columnKey)` — no remaining production consumers after Task 1 added `GroupItemsByColumn` and Task 4 removed the factory's display template that called `GetGroupItemsForColumn` indirectly. Verify with grep before deleting.
- [ ] In `RecipeRowViewModelTests`: remove tests covering the deleted methods (`RecipeRowViewModelTests.cs:197,207` neighborhood — locate exact tests by scanning the file).
- [ ] Verify `using` directives in both files — drop unused ones.
- [ ] Run `dotnet build` — 0 warnings, 0 errors. `dotnet test` — all green.

### Task 5: Tests for column shape and binding wiring

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs` — add assertions on action/group column `IsReadOnly` and absence of `CellEditingTemplate`.
- Possibly create: `SemiStep/SemiStep.Tests/UI/ComboBoxCellFactoryTests.cs` — direct tests on the factory if the existing `ColumnBuilder`-level coverage is insufficient.

- [ ] In `ColumnBuilderIdempotencyTests`, add a test that the column produced for the action key has `IsReadOnly == true` and `CellEditingTemplate == null` and `CellTemplate != null`. Same for at least one group column.
- [ ] Add a test that exercises the `ActionChanged` data-flow path: change `SelectedItem` on the ComboBox materialized from the template (use `IDataTemplate.Build(row)`); verify `row.ActionChanged` fires with the expected id. This covers the TwoWay binding contract without needing hit-test simulation.
- [ ] If headless materialization of the template is awkward (binding evaluation needs visual tree), accept the limitation and document with a one-line `// Manual verification required: …` comment in the test seam.
- [ ] Run `dotnet test` — all green.

### Task 6: Verify acceptance criteria

- [ ] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 309/309+ green.
- [ ] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` — pass.
- [ ] `git diff master..HEAD --stat` review — scope confined to `RecipeRowViewModel.cs`, `ComboBoxCellFactory.cs`, `ColumnBuilderIdempotencyTests.cs`, `RecipeRowViewModelTests.cs`, optionally `ColumnBuilder.cs`, plan/docs files. No incidental edits.
- [ ] Manual UI verification per the Testing Strategy section (7 scenarios). Document any deviation as `⚠️` in this file.

### Task 7: Archive plan + document Round-8

**Files:**
- Move: `Docs/plans/20260514-combobox-celltemplate-migration.md` → `Docs/plans/completed/`
- Modify: `Docs/07-non-functional.md`

- [ ] `git mv Docs/plans/20260514-combobox-celltemplate-migration.md Docs/plans/completed/`
- [ ] In `Docs/07-non-functional.md`, add a Round-8 subsection after Round-7 covering:
  - Avalonia.Controls.DataGrid#236: `CellEditingTemplate` + ComboBox broken in 12.0.0, no upstream fix planned.
  - Migration to CellTemplate-only ComboBoxes with `IsReadOnly = true` — the only working pattern in Avalonia 12 per official samples and accepted community answers.
  - `supportsRecycling: true` flip across all four (now two) ComboBox templates — closes the Round-7-deferred recycling work. `row` closures eliminated by exposing `RecipeRowViewModel.GroupItemsByColumn` as a bindable lookup and introducing `ComboBoxItemMultiSelectionConverter` for the group case (action items remain global, no multi-binding needed).
  - Disable-state binding switched to `IsHitTestVisible` (not `IsEnabled`) so disabled cells render with the same visual chrome as enabled cells — only clicks are ignored. Preserves master-branch visual UX.
  - Manual smoke checklist for click flow, disabled-state behaviour, and `RecipeActive` read-only mode.

## Post-Completion

**Manual verification** (required before PR open):
- The 7-scenario click/scroll smoke listed in Testing Strategy. Run against a real recipe with ≥100 steps.
- Optional perf measurement: open Task Manager / dotnet-counters during the 30s scroll. Compare gen-0 collection rate vs Round-7 baseline. Bounded memory + lower gen-0 rate is the expected outcome of `supportsRecycling: true`.

**External system updates:**
- None. Internal UI fix + perf carry-over.
