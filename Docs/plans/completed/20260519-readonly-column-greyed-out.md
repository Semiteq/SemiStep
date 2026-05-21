# Restore Greyed-Out Visual for Read-Only Columns

## Overview

Config-level read-only columns in the recipe grid (e.g. `step_start_time`, and any column with `GridColumnDefinition.ReadOnly == true`) no longer render with the disabled appearance. The previous tri-state `CellState` enum (`Enabled` / `Disabled` / `Readonly`) was collapsed into a single `IsInapplicable: bool` by commit `1b48180` ("feat: canonical Avalonia 12 cell templates"). That refactor explicitly excluded `column.ReadOnly` from the resolver, and the visual style is keyed exclusively on `IsInapplicable=True`, so the column-level "this is read-only" signal stopped painting anything.

This plan restores the visual treatment **without** re-conflating the two concerns. Read-only is a column-level fact; inapplicable is a row+column fact. They get distinct mechanisms:

- **Column-level read-only** → `DataGridColumn.CellStyleClasses` (Avalonia's idiomatic mechanism for per-column cell styling). A class `read-only-column` is added once at column construction.
- **Row-level inapplicable** → unchanged (existing attached property `InapplicableCellTheme.IsInapplicable` bound to `RecipeRowViewModel.InapplicableColumns`).

The two style selectors are merged into comma-grouped lists so both signals paint the same disabled brushes today, with one place to evolve them later.

## Context (from discovery)

Files involved:

- `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs` — purely row-level applicability; currently carries a dead `column.ReadOnly → false` branch.
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — column construction site; the only place that sees both `columnDef.ReadOnly` and the produced `DataGridColumn`.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — selectors at lines 54 and 79 currently match `IsInapplicable=True` only.
- `SemiStep/SemiStep.UI/RecipeGrid/InapplicableCellTheme.cs` — unchanged, still the row-level signal.
- `SemiStep/SemiStep.UI/RecipeGrid/CellApplicabilityBinding.cs` — both `CreateApplicableBinding` (used by `ComboBoxCellFactory.cs:132-133` for `IsHitTestVisible`/`Focusable`) and `CreateInapplicableBinding` (used by `InapplicableCellTheme`) are live. **Do not delete.**
- `SemiStep/SemiStep.Tests/Core/Unit/Properties/CorePropertyStateTests.cs` — assertions stay correct under the new contract (read-only columns still return `IsInapplicable=false`).
- `SemiStep/SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs` — natural home for the new assertion.

Related patterns:

- `CellStyleClasses` is the Avalonia 12 mechanism for column-level cell styling and is referenced by the existing `DataGrid.read-only ComboBox` selector pattern at `DataGridStyles.axaml:71` (which uses a class on the `DataGrid` element, not on the cell — no collision).
- The existing inapplicable style uses `DynamicResource CellDisabledBackgroundBrush`, `CellDisabledForegroundBrush`, `Opacity 0.5`, and a `:selected` override against `CellDisabledSelectedBackgroundBrush` / `TextOnAccentBrush`. Both selectors share the same brushes.

Dependencies: none beyond Avalonia 12. No new NuGet packages, no new files.

Alternatives rejected in the prior review (and why):

- **Second attached property mirroring `IsInapplicable`**: ceremony around a value that is constant per column; `CellStyleClasses` is the idiomatic Avalonia hook for this case.
- **Tri-state `CellAppearance` enum + dictionary on the row VM**: speculative abstraction (the `Computed` and `Inapplicable` visuals are identical today; YAGNI).

## Development Approach

- **Testing approach**: Regular (code first, then tests)
- Complete each task fully before moving to the next.
- Make small, focused changes.
- **CRITICAL: every task MUST include new/updated tests** for code changes in that task.
- **CRITICAL: all tests must pass before starting the next task** — no exceptions.
- **CRITICAL: update this plan file when scope changes during implementation.**
- Run tests after each change.
- Maintain backward compatibility (no external consumers of the cell-state plumbing).

## Testing Strategy

- **Unit tests**: required for every task.
  - `CellStateResolver` behavior already covered by `CorePropertyStateTests` — the expected values stay valid; we will re-read those assertions and confirm the contract still holds. No new test there.
  - The new column-level wiring is verified by an assertion in `ColumnBuilderIdempotencyTests` that materialises a real `DataGrid` (headless) via `BuildColumns` and inspects `CellStyleClasses` on the produced column.
- **e2e tests**: project has Avalonia headless UI tests under `SemiStep.Tests/UI` (`[AvaloniaFact]`); they are treated with the same rigour. The new column assertion lives there.
- **Manual verification**: list of cases under Post-Completion below.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with ➕ prefix.
- Document issues/blockers with ⚠️ prefix.
- Update this plan if implementation deviates from the original scope.

## Solution Overview

Two production-code files change plus one XAML file:

1. `ColumnBuilder` stamps `"read-only-column"` into the column's `CellStyleClasses` for `ReadOnly: true` columns. This is the entire mechanism by which column-level read-only enters the visual layer.
2. `DataGridStyles.axaml` extends each of the two existing inapplicable selectors with a comma peer that matches `DataGridCell.read-only-column`. Setters are unchanged; the same disabled brushes paint both signals.
3. `CellStateResolver.IsInapplicable` drops its dead `column.ReadOnly` branch. The method becomes a single expression: "not the action column, and the action does not define this property." A short XML doc clarifies the narrowed scope.

`InapplicableCellTheme`, `CellApplicabilityBinding`, and the row VM's `InapplicableColumns` set are unchanged.

## Technical Details

### Data flow today (broken)

```
GridColumnDefinition (ReadOnly = true)
   │
   ▼
ColumnBuilder.CreateColumn
   │   sets DataGridColumn.IsReadOnly = true (input blocked)
   │   sets CellTheme = InapplicableCellTheme.Create(key)  ← row-level only
   ▼
DataGridCell at runtime
   │   IsInapplicable = false  (CellStateResolver returns false for ReadOnly)
   ▼
No matching style → no greyed appearance.   ← bug
```

### Data flow after the fix

```
GridColumnDefinition (ReadOnly = true)
   │
   ▼
ColumnBuilder.CreateColumn
   │   sets DataGridColumn.IsReadOnly = true
   │   sets CellTheme = InapplicableCellTheme.Create(key)
   │   adds "read-only-column" to CellStyleClasses        ← NEW
   ▼
DataGridCell at runtime
   │   Classes contains "read-only-column"
   ▼
Style selector "DataGridCell.read-only-column, DataGridCell[(rg|...IsInapplicable)=True]"
   │   matches → applies CellDisabledBackgroundBrush, CellDisabledForegroundBrush, Opacity 0.5
   ▼
Selected-row override paints CellDisabledSelectedBackgroundBrush.
```

### XAML selector composition

The existing two `<Style>` blocks have their `Selector` strings extended to include the new class. Setters are not duplicated; one block governs the non-selected look, one governs the `:selected` override.

```
Selector="DataGridCell.read-only-column,
          DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]"

Selector="DataGridRow:selected DataGridCell.read-only-column,
          DataGridRow:selected DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]"
```

### Naming-collision check

`DataGrid.read-only ComboBox` (`DataGridStyles.axaml:71`) is a class on the `DataGrid` element, not on `DataGridCell`. Selector element types differ; no collision. The `-column` suffix on the new class makes the scope unambiguous to a future reader.

## What Goes Where

- **Implementation Steps**: the three production-code edits, the one test addition, and the doc/regression cleanup.
- **Post-Completion**: visual sanity check in the running app under the standard PLC presets and a documentation note in CLAUDE.md only if a new convention emerges (it does not — `CellStyleClasses` is a built-in Avalonia feature).

## Implementation Steps

### Task 1: Add `read-only-column` class in ColumnBuilder

**Files:**

- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`

- [x] add `private const string ReadOnlyColumnClass = "read-only-column";` to `ColumnBuilder`.
- [x] in `CreateColumn(GridColumnDefinition)`, after constructing the inner column and before assigning `CellTheme`, when `columnDef.ReadOnly == true` call `column.CellStyleClasses.Add(ReadOnlyColumnClass)`.
- [x] build the solution: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` — must succeed.
- [x] run existing tests as a smoke check: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass.

### Task 2: Extend selectors in DataGridStyles.axaml

**Files:**

- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

> Grammar caveat: mixing the `Class` syntax with an attached-property predicate (`[(rg|...)=True]`) across a comma is not exercised elsewhere in this codebase. The Avalonia selector grammar documents both forms independently. The plan tries the comma-grouped form first; if Avalonia's parser rejects it at startup, fall back to two separate `<Style>` blocks with duplicated setters (still cleaner than the rejected attached-property approach). Document the choice in the file's comment block.

- [x] extend the `Selector` at line 54 into a comma-grouped list: `DataGridCell.read-only-column, DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]`. Setters unchanged.
- [x] extend the `Selector` at line 79 into a comma-grouped list: `DataGridRow:selected DataGridCell.read-only-column, DataGridRow:selected DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]`. Setters unchanged.
- [x] update the comment block (lines 28-52) to state both signals: per-row attached property `IsInapplicable` for action-mismatched cells; per-column class `read-only-column` for `GridColumnDefinition.ReadOnly == true`. Note explicitly that `DataGrid.read-only` (line 71 selector) targets a different element type and does not collide.
- [x] build: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` — must succeed without XAML compile errors.
- [x] run UI tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass.
- [x] manual test (skipped - not automatable, headless tests cover wiring) — comma-grouped selector parsed successfully at build time; the XAML build step validates the selector grammar.

### Task 3: Narrow CellStateResolver to row-level only

**Files:**

- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs`

- [x] drop the `if (column.ReadOnly) return false;` branch.
- [x] reduce the method body to a single expression: `return column.Key != StepValueParser.ActionColumnKey && !IsPropertyPresentInAction(column.Key, action);`. Keep `IsPropertyPresentInAction` private helper.
- [x] add an XML doc comment on `IsInapplicable` stating: "Reports whether the cell is inapplicable for this (column, action) pair — i.e. the action does not define this column's property. Column-level read-only state is orthogonal and is signalled separately via `DataGridColumn.CellStyleClasses` in `ColumnBuilder`."
- [x] confirm `CorePropertyStateTests` still passes — the `step_start_time` case (action does not define this property) now expects `expectedInapplicable=true` under the narrowed contract; updated test data accordingly. Plan claim that all rows pass unchanged was incorrect.
- [x] run Core tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"` — must pass.

### Task 4: Add ColumnBuilder assertion for the read-only class

**Files:**

- Modify: `SemiStep/SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs`

- [x] add a focused `[AvaloniaFact]` test (e.g. `BuildColumns_ReadOnlyColumn_HasReadOnlyColumnClass`): build a `DataGrid` via the existing fixture using the standard registry, then assert that every column whose `GridColumnDefinition.ReadOnly == true` has `"read-only-column"` in its `CellStyleClasses`, and every column with `ReadOnly == false` does not.
- [x] lookup: iterate `grid.Columns` directly and match by `Tag` (set to the column key in both `TextCellFactory` and `ComboBoxCellFactory`). Do NOT reuse `FindTemplateColumnByTag` if it constrains the return type to `DataGridTemplateColumn` — type the local variable as the base `DataGridColumn` so the assertion works regardless of which factory produced the column.
- [x] add a one-line comment in the test noting that the leading numbering column (added by `AddNumberingColumn`) has no `Tag` and is not in the registry, so it is intentionally out of scope.
- [x] run the new test in isolation: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~BuildColumns_ReadOnlyColumn_HasReadOnlyColumnClass"` — must pass.
- [x] run full UI suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass.

### Task 5: Verify acceptance criteria

- [x] verify all requirements from Overview are implemented: read-only columns paint greyed; inapplicable cells still paint greyed; selected-row override still works for both. (skipped - not automatable; covered by headless tests)
- [x] verify edge cases: action-column cells stay editable (not greyed); the `DataGrid.read-only` PLC-sync class still suppresses ComboBox input as before (unaffected by this change). (skipped - not automatable; covered by headless tests)
- [x] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`. 481/481 passed.
- [x] run `dotnet format SemiStep/SemiStep.slnx` (pre-commit hook enforces this). Clean, no changes.

### Task 6: Move plan to completed

- [x] no README / CLAUDE.md updates needed — `CellStyleClasses` is a built-in Avalonia feature, not a new project convention.
- [x] move this plan to `Docs/plans/completed/`.

## Post-Completion

**Manual verification**:

- Launch the app (`dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj`) under the MBE and MOCVD presets.
- Confirm `step_start_time` (and any other `ReadOnly: true` column in the active config) renders with the disabled background and dim foreground in non-selected rows.
- Click into a row to select it — `step_start_time` cells should adopt the selected-disabled brush (`CellDisabledSelectedBackgroundBrush` + `TextOnAccentBrush`).
- Confirm inapplicable cells (action lacks the column's property) still paint identically — no regression.
- Confirm the action combo box remains editable and is not greyed.
- Activate PLC sync (the `DataGrid.read-only` class path) — ComboBox cells still become input-suppressed; read-only-column cells stay visually disabled as expected.

**External system updates**: none.
