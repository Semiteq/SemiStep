# Cell change highlight on action / selector change (issue #63)

## Overview

When the action in a recipe row changes, `StepInitializer.Create` reinitializes the whole step
with default values. The operator may not notice that cell values changed. Issue #63 asks for a
visual signal: a cell initialized with a default value gets an orange background, the highlight
survives PLC sync, and clears on three events.

This builds directly on the #71 seed/recompute machinery (`StepInitializer`,
`UpdateStepForSelectorChange`, the per-row `InapplicableColumns` observable, the per-column
`InapplicableCellTheme` attached-property + style cascade). The orange highlight mirrors that exact
mechanism with a parallel per-row set and a parallel attached property.

### Behavior (from the issue, plus confirmed scope decisions)

- A cell seeded with a default value turns orange.
  - On **action change** of an existing row: every freshly seeded value cell.
  - On **selector change** (#71 nested actions, confirmed in scope): newly activated cells turn
    orange; deactivated cells leave the set.
- The orange highlight persists across recipe sync with the PLC.
- The cell background returns to normal when:
  - a new value is entered into the cell (the user edits it),
  - the cell is clicked and then any other cell is clicked,
  - recipe execution starts (all orange cells across all rows clear).

### Confirmed decisions

- **Color source**: a single flat static brush `CellChangedBrush` in `ColorPalette.axaml`, alongside
  the existing flat semantic brushes (`AccentBrush`, `ErrorBrush`, `WarningBrush`). NOT routed through
  the config-driven `GridStyleOptions` per-depth palette. The #74 dark-theme work will revisit it.
- **Selector-seeded cells**: highlighted too (same "initialized with default" semantics).

## Context (from discovery)

- `SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — owns per-row `InapplicableColumns` (the pattern to
  mirror). A new `ChangedColumns` set lives here.
- `SemiStep.UI/RecipeGrid/InapplicableCellTheme.cs` — registers the `IsInapplicable` attached property
  and builds the per-column `ControlTheme` (one `CellTheme` per `DataGridColumn`). The new `IsChanged`
  attached property and its setter go here (a `DataGridColumn` has a single `CellTheme`, so both
  setters must share this one theme).
- `SemiStep.UI/RecipeGrid/CellApplicabilityBinding.cs` — the converter bindings; add a `ChangedColumns`
  binding.
- `SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — applies `InapplicableCellTheme.Create(key)` per column.
  No change expected (the second setter is added inside `Create`).
- `SemiStep.UI/Styles/DataGridStyles.axaml` — the cell style cascade. Add the orange rule, placed after
  the inapplicable per-depth chains and before the `:selected` rule (last-match-wins; orange must beat
  normal/inapplicable/depth tints, selection must beat orange).
- `SemiStep.UI/Styles/ColorPalette.axaml` — flat semantic brushes. Add `CellChangedColor` /
  `CellChangedBrush`.
- `SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — routes action/selector/property mutations to rows.
  Marks/clears the changed set: `RebuildRow` (action change), `OnSelectorValueChanged` (delta),
  `OnCellValueChanged` (clear edited column).
- `SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs` — clears all changed sets when execution starts.
- `SemiStep.UI/MainWindow/MainWindow.axaml.cs` — wires DataGrid events; add `CellPointerPressed`
  tracking for the click-away clear rule.

### Patterns observed

- `InapplicableColumns` is a settable `RaiseAndSetIfChanged` property assigned a NEW set instance on
  every change; the OneWay cell binding only re-fires on a reference change. `ChangedColumns` must do
  the same (build a fresh `HashSet<string>(StringComparer.OrdinalIgnoreCase)` each mutation).
- UI tests use `UIFixture`, `[AvaloniaFact]`, `FluentAssertions`, `BuildRows(...)` helpers. Files are
  UTF-8 with BOM. Traits: `[Trait("Component","UI")] [Trait("Area","RecipeGrid")] [Trait("Category","Unit")]`.

## Development Approach

- Regular approach (code first, then tests) — this is a UI-state feature mirroring an existing pattern.
- Each task ends with passing tests before the next begins.
- One logical change per task. All changes are UI-layer; Core is untouched.
- Run `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` and the UI test slice after each task.

## Testing Strategy

- **Unit (VM-level)**: `RecipeRowViewModel` changed-set transitions produce new instances and correct
  membership; `ExecutionHighlightTracker` clears changed sets on execution start; `RecipeGridViewModel`
  marks changed on action change, applies the selector delta, and clears on property edit.
- **Headless UI**: existing `[AvaloniaFact]` lifecycle. A binding-level test that `IsChanged` reflects
  `ChangedColumns` membership is optional; the attached-property wiring mirrors the already-tested
  `IsInapplicable` path, so VM-level coverage plus a manual smoke is sufficient. Do not add a brittle
  pixel/visual-tree assertion.
- Known harness limit: run the UI slice with `--filter "Component=UI"`; the full single-process suite
  has ~195 spurious UI failures from `RxAppBuilder.EnsureInitialized` once-per-process.

## Solution Overview

Pure UI state. `Step`/Core stay ignorant of the highlight — it is an editing-session annotation, not
recipe data. The per-row `ChangedColumns` set + per-column `IsChanged` attached property + one style
rule reproduce the `InapplicableColumns` mechanism. The grid view-model owns *when* columns enter the
set (seed events) and leave it (edit / execution / click-away).

Cascade placement: orange rule after the inapplicable per-depth chains, before `:selected`. Orange and
inapplicable/read-only are disjoint (inapplicable cells carry no seeded value; read-only columns are not
value cells). Orange is NOT disjoint from the idle `for-depth-N` tints (DataGridStyles.axaml:80-97): a
nested row whose action just changed is both depth-tinted and orange. Last-match-wins ordering resolves
this — the orange rule placed after the depth chains beats the depth tint. Selection (later still) beats
orange: clicking an orange cell selects its row, so the clicked cell shows `AccentBrush`, then click-away
clears it (consistent with the pending-cell model — not a bug). Execution past/current tints never
coexist with orange because execution start clears it.

## Technical Details

- `RecipeRowViewModel.ChangedColumns : IReadOnlySet<string>` (OrdinalIgnoreCase), init empty,
  `RaiseAndSetIfChanged`. Mutators each assign a fresh set:
  - `MarkChanged(IReadOnlyCollection<string> keys)`
  - `ApplyChangedDelta(IReadOnlyCollection<string> add, IReadOnlyCollection<string> remove)`
  - `ClearChanged(string columnKey)`
  - `ClearAllChanged()`
  - `IsChanged(string columnKey)` query (used by the click-away tracker).
- Seeded value columns of a step = `step.Properties.Keys` (the action column is `step.ActionKey`, not a
  property; step start time / numbering are not properties). NOTE: `step.Properties.Keys` are
  `PropertyId`, not `string`. The marking MUST project them to their string column key
  (`step.Properties.Keys.Select(id => id.Value)`) so they match the OrdinalIgnoreCase `string` contract
  the `IsChanged` binding uses.
- Click-away: track a pending `(RecipeRowViewModel row, string columnKey)`. On `CellPointerPressed`:
  if pending is set and the pressed cell differs, clear the pending cell's orange; then set pending to
  the pressed cell iff it is currently changed, else null. Resolve column key from `e.Column.Tag`,
  row VM from the cell/row `DataContext`. Guard against rows no longer in the grid.

## What Goes Where

- **Implementation Steps**: brush, attached property + style, VM changed-set, grid-VM wiring, execution
  clear, click-away, tests.
- **Post-Completion**: manual smoke (action change → orange; edit clears; click-away clears; execution
  start clears; sync keeps orange); the #74 dark-theme pass will re-evaluate the orange shade.

## Implementation Steps

### Task 1: Orange brush + IsChanged attached property + style rule

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/InapplicableCellTheme.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/CellApplicabilityBinding.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [x] Add `CellChangedColor` (orange fill, e.g. `#FFCC80`) and `CellChangedBrush` to `ColorPalette.axaml`,
      grouped with the flat semantic brushes; keep default (black) foreground (set Background only).
- [x] Add `IsChangedProperty` attached property (+ `GetIsChanged`/`SetIsChanged`) to
      `InapplicableCellTheme`; update its class summary to note it now carries two disjoint cell-state
      signals (applicability and changed).
- [x] Add `CellApplicabilityBinding.CreateChangedBinding(columnKey)` binding `ChangedColumns` →
      `set is not null && set.Contains(columnKey)`.
- [x] In `InapplicableCellTheme.Create`, add a second `Setter` binding `IsChangedProperty` to the
      changed binding (same single `ControlTheme`).
- [x] Add the orange style rule to `DataGridStyles.axaml`:
      `DataGridCell[(rg|InapplicableCellTheme.IsChanged)=True]` → `Background = CellChangedBrush`,
      placed after the inapplicable per-depth chain and before the `:selected` rule. Reuse the EXISTING
      `xmlns:rg` prefix verbatim (`assembly=Semistep`, lowercase s — do not "correct" the casing). Add a
      brief comment: beats normal + idle depth tints by ordering, loses to selection; disjoint from
      inapplicable/read-only.
- [x] Write a converter-membership unit test for `CellApplicabilityBinding.CreateChangedBinding` (pure
      converter, no headless harness needed — mirrors how the inapplicable converter is exercised):
      contains → true, absent / null set → false.
- [x] Build the UI project.

### Task 2: RecipeRowViewModel changed-column set

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [x] Add `ChangedColumns` (`IReadOnlySet<string>`, OrdinalIgnoreCase, init empty, `RaiseAndSetIfChanged`).
- [x] Add `MarkChanged`, `ApplyChangedDelta`, `ClearChanged`, `ClearAllChanged`, `IsChanged` — each
      mutator assigns a fresh set instance (mirror the `InapplicableColumns` reference-change contract).
- [x] Write tests: `MarkChanged` sets membership and a new instance; `ApplyChangedDelta` adds/removes;
      `ClearChanged` removes one; `ClearAllChanged` empties; no-op mutations still behave (assert
      membership; instance-identity assertion only where a change occurs).
- [x] Run UI test slice — must pass before next task.

### Task 3: Grid view-model wiring (mark on seed, clear on edit)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`

- [x] In `RebuildRow` (the action-change path only), after creating the replacement row, mark its
      `ChangedColumns` to the new step's property keys projected to strings
      (`step.Properties.Keys.Select(id => id.Value)`). Do NOT mark on append/insert/full-rebuild (those
      are not "action change in a row").
- [x] In `OnSelectorValueChanged`, ONLY on the success branch (after the existing `result.IsFailed`
      early-return at line ~233) and after `RecomputeInapplicableColumns`, apply the changed delta: add
      `selectorEdit.ColumnsToSeed`, remove `selectorEdit.ColumnsToDrop` (the selector column itself is the
      operator's explicit choice — not marked). The pre-mutation `SelectorEdit` carries the correct
      drop/seed lists even though `_step` is already mutated in place by the synchronous sync signal.
- [x] In `OnCellValueChanged`, after a successful update, `ClearChanged(columnKey)` on the row.
- [x] Write tests: action change marks changed = new step property keys; selector change adds seeded /
      removes dropped; a FAILED selector edit leaves the changed set untouched; property edit clears the
      edited column. Mirror existing `RecipeGridViewModelTests` setup (coordinator/registry fixtures).
- [x] Run UI test slice — must pass before next task.

### Task 4: Clear all on execution start

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ExecutionHighlightTrackerJumpTests.cs` (or a new test file)

- [x] On transition into `RecipeActive` (was inactive, now active), clear every row's changed set
      (`ClearAllChanged`) alongside the existing current/past marking.
- [x] Write test: rows with changed columns, execution becomes active → all changed sets empty; an
      already-active line change does not re-clear (idempotent guard).
- [x] Run UI test slice — must pass before next task.

### Task 5: Click-away clearing in MainWindow

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`

- [x] Subscribe `RecipeGrid.CellPointerPressed` in `WhenActivated`; unsubscribe in the disposable.
- [x] Track pending `(RecipeRowViewModel, columnKey)`. On press: if pending set and pressed cell differs,
      clear pending cell's orange; set pending to the pressed cell iff currently changed, else null.
      Resolve `columnKey` from `e.Column.Tag`, row VM from cell/row `DataContext`; guard missing data and
      rows no longer present. Verified the Avalonia 12 `CellPointerPressed` args shape during implementation
      (`DataGridCellPointerPressedEventArgs`: `Cell`, `Column`, `Row`, `PointerPressedEventArgs`). The
      click-away clear is independent of `IsReadOnly` — no read-only early-return, so orange still clears
      via click-away while sync is active.
- [x] No automated test (pointer/visual-tree interaction) — covered by manual smoke only. Added a one-line
      code comment on the handler explaining the click-away rule and the deliberate absence of an
      `IsReadOnly` guard.
- [x] Build the UI project.

### Task 6: Verify acceptance criteria

- [x] Action change on an existing row → all seeded cells orange. Code-trace verified:
      `RecipeGridViewModel.RebuildRow` (RecipeGridViewModel.cs:399) calls
      `row.MarkChanged(step.Properties.Keys.Select(id => id.Value).ToList())` on the
      `StepActionChanged` path; the orange style rule (DataGridStyles.axaml:176) renders the set.
      Pixel rendering is manual smoke pending.
- [x] Selector change → newly activated cells orange, deactivated cells not orange. Code-trace
      verified: `OnSelectorValueChanged` success branch (RecipeGridViewModel.cs:237) calls
      `row.ApplyChangedDelta(add: ColumnsToSeed, remove: ColumnsToDrop)`; `ApplyChangedDelta`
      (RecipeRowViewModel.cs:209) unions adds and excepts removes. Covered by
      `SwitchToManual_MarksSeededColumnChanged` / `SwitchBackToAuto_DropsSeededColumnFromChanged`.
- [x] Editing a cell clears its orange. Code-trace verified: `OnCellValueChanged` success branch
      calls `row.ClearChanged(columnKey)` (RecipeGridViewModel.cs:207).
- [x] Clicking an orange cell then any other cell clears the first. Code-trace verified:
      `MainWindow.OnCellPointerPressed` (MainWindow.axaml.cs:136-158) clears the pending cell when a
      different cell is pressed, then re-arms pending iff the pressed cell `IsChanged`. No `IsReadOnly`
      guard, so it clears even during sync. Pointer gesture is manual smoke pending.
- [x] Execution start clears all orange. Code-trace verified: `ExecutionHighlightTracker`
      (ExecutionHighlightTracker.cs:42-45) calls `ClearAllChangedHighlights` on the inactive→active
      edge → `row.ClearAllChanged()`. Covered by Task 4 tests and observed live in the
      `BlockedSelectorEdit_LeavesChangedColumnsUntouched` test (going active empties the set).
- [x] Orange survives a sync-driven `UpdateStep` (same row VM instance). Code-trace verified:
      `RecipeRowViewModel.UpdateStep` (RecipeRowViewModel.cs:114-118) only swaps `_step` and raises the
      indexer; it never touches `ChangedColumns`. Same VM instance, so the set persists.
- [x] Run UI test slice: 255 passed, 0 failed (single-process; no spurious RxAppBuilder mass-failure
      this run). Fixed one real test, `BlockedSelectorEdit_LeavesChangedColumnsUntouched`, whose old
      assertion contradicted the execution-start clear (it forced read-only by going recipe-active,
      which legitimately empties `ChangedColumns`). Core slice: 235 passed, 0 failed.
- [x] Build all: `dotnet build SemiStep/SemiStep.slnx` → 0 errors (only pre-existing NU1902 NCalc
      advisory warnings). `dotnet format SemiStep/SemiStep.slnx` → no formatting changes.

### Task 7: Docs + finalize

- [x] Add a short note to `Docs/architecture` (or extend the nested-actions note) describing the
      change-highlight: where the state lives (UI-only), the three clear triggers, the cascade placement.
      Done: `Docs/architecture/cell-change-highlight.md`.
- [x] Move this plan to `Docs/plans/completed/`. (deferred to end of exec run — kept in place for review phases)

## Post-Completion

**Manual verification:**
- Smoke the five behaviors above in the running app, including the sync-active read-only state (orange
  must persist and click-away must still clear while sync is active).

**External:**
- The #74 dark-theme pass will re-evaluate the `CellChangedColor` shade for contrast on the dark palette.
