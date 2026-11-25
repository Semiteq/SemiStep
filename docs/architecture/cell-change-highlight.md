# Changed-Cell Highlight (issue #63)

## Overview

When a row's action changes, the step is reinitialized with default values, comment included;
the operator may not notice. Issue #63 adds a visual signal: a cell whose value the change
altered gets an orange background (`#FFCC80`). The highlight survives PLC sync and clears on the
explicit events below.

The highlight is a pure UI annotation. It does not call into the seed/recompute machinery itself;
it compares the step before and after a change (the machinery lives in `SemiStep.Core` / the
coordinator) and keeps the result as a parallel per-row set and a parallel attached property, reusing the
inapplicable-cell mechanism (the per-row set + per-column attached-property + style cascade). See
`nested-actions.md` for that base machinery.

## Where the state lives: UI only

The highlight is an editing-session annotation, not recipe data. `Step` and the rest of
`SemiStep.Core` stay ignorant of it. The state is a per-row set of changed column keys on the
view-model:

- `RecipeRowViewModel.ChangedColumns : IReadOnlySet<string>` (OrdinalIgnoreCase, init empty,
  `RaiseAndSetIfChanged`). Mutators each assign a **new** set instance so the one-way cell
  binding re-fires; mutating in place would not notify (same contract as `InapplicableColumns`):
  `MarkChanged`, `ApplyChangedDelta`, `ClearChanged`, `ClearAllChanged`, plus an `IsChanged`
  query used by the click-away tracker.
- The marking rule (operator decision D7, option c): a cell is marked when its new value differs
  from the value the row held for the same key before the change. A key the row did not hold
  counts as empty. One rule, no per-column cases. `RecipeRowViewModel.ChangedValueKeys(previousStep,
  newStep)` is the only place that compares; it walks the new step's keys, so a key the new step
  drops is never marked. `MarkChanged` and `ApplyChangedDelta` store the keys they get and do not
  filter.
- Consequences: an action change wipes a typed comment to empty and marks it; a comment empty
  before and after is not marked, also on a CSV-loaded step that has no comment key; a numeric
  cell is marked when its value changes and not when the new default equals the old value.

## Mechanism

- Per-row `ChangedColumns` set (above).
- Per-column `IsChanged` attached property on `InapplicableCellTheme`
  (`SemiStep.UI/RecipeGrid/InapplicableCellTheme.cs`). A `DataGridColumn` carries a single
  `CellTheme`, so the changed and inapplicable setters share that one `ControlTheme`. The
  `IsChanged` setter binds to `CellApplicabilityBinding.CreateChangedBinding(columnKey)`, which
  evaluates `set is not null && set.Contains(columnKey)`.
- One style rule in `SemiStep.UI/Styles/DataGridStyles.axaml`:
  `DataGridCell[(rg|InapplicableCellTheme.IsChanged)=True]` sets `Background` to
  `CellChangedBrush`.
- `CellChangedBrush` comes from `colors.cells.changed` in `grid_style.yaml`, installed by
  `CellPaletteInstaller`; `changed_selected` is the selected variant. Foreground stays the default
  (black).

## When cells are marked

Marking happens in `SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`, the base class both
orientation surfaces derive from (see `recipe-grid-surface.md`):

- **Action change** — `RebuildItem` reads the step of the row it replaces, builds the new row, and
  marks `ChangedValueKeys(previousStep, newStep)`. Each surface compares against its own old
  row, and both rows hold the same step, so both orientations mark the same cells. Only the
  action-change path marks this way; append / insert / full-rebuild do not.
- **Selector change** (#71 nested actions) — the coordinator raises `PropertyUpdated`, and
  `RecipeRowUpdateSynchronizer` updates the row on each surface, the originating one included:
  every key `ChangedValueKeys` reports is marked, held before or not, and a dropped key is
  unmarked. Key presence does not decide: a PLC-loaded step holds every action property and a
  CSV-loaded step every non-blank cell, so a selector change can overwrite a held hidden value
  with its default, and that cell is marked. The selector column itself is the operator's edit
  and is not marked: after a successful change `OnSelectorValueChanged` publishes the selector
  key through `ChangeMarkClearBroadcaster`, and both surfaces clear it.

## The clear triggers

- **Edit** — a new value entered into the cell. After a successful update `OnCellValueChanged`
  publishes the step and column through `ChangeMarkClearBroadcaster`, so both orientation
  surfaces clear the cell. Both surfaces would otherwise keep a mark: `RecipeRowUpdateSynchronizer`
  marks every key whose value changed, the edited one included. The cell the operator edited is
  never marked; only cells the edit changed as a consequence (a formula recalculation) keep a
  mark.
- **Click-away** — the cell is clicked, then any other cell is clicked.
  `SemiStep.UI/RecipeGrid/CanonicalRecipeGridView.axaml.cs` tracks a pending
  `(RecipeRowViewModel, columnKey)`; the pending-vs-pressed decision itself lives in
  `ChangedCellClickResolver`. On `CellPointerPressed`, if a pending cell is set and the pressed
  cell differs, it clears the pending cell's orange, then re-arms pending to the pressed cell iff
  that cell `IsChanged`. There is no `IsReadOnly` guard, so click-away still clears while PLC
  sync is active. The view does not clear the row directly: it calls the surface's
  `ClearChangedByClickAway`, which maps the row to its step index (rows that left the projection
  are skipped) and publishes through `ChangeMarkClearBroadcaster` so both orientation
  surfaces clear their own row for that step.
- **Clear all** — Правка > "Снять отметки изменений" and the same grid context-menu item run
  `RecipeCommandsViewModel.ClearChangeMarksCommand`. It calls
  `IRecipeGridSurface.ClearAllChangedMarks`, which `ActiveRecipeGridSurface` fans out to both
  surfaces. It does not dirty the recipe and adds no undo entry. Its `canExecute` is
  `IRecipeGridSurface.HasChangedMarks`: each surface keeps the set of rows that carry a mark. A
  row's `ChangedColumns` notification and an `Items` add, remove or replace adjust the set for
  those rows only; an `Items` reset rebuilds it. The active surface ORs the two surfaces.
- **Execution start** — `SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs` clears every row's
  set (`ClearAllChanged`) on the inactive→active edge only; an already-active line change does
  not re-clear.

## Cascade placement

The orange rule sits after the inapplicable per-depth chains and before the
`DataGridRow:selected` rule in `DataGridStyles.axaml`. Last-match-wins ordering means it beats
the normal background and the idle per-depth tints, and loses to selection. Orange and
inapplicable/read-only are disjoint (inapplicable cells carry no seeded value; read-only columns
are not value cells). Clicking an orange cell selects its row, so that cell shows `SelectionBackgroundBrush`
until click-away clears it — consistent with the pending-cell model, not a bug. Execution
past/current tints never coexist with orange because execution start clears it.

The highlight persists across PLC sync because `RecipeRowViewModel.UpdateStep` only swaps the
backing `_step` and raises the indexer; it never touches `ChangedColumns`, and the same row VM
instance is reused.

## Transposed parity

Both orientation surfaces derive from `RecipeGridSurfaceBase<TItem>`, so every mark and clear
path above is implemented once in the base and applies to the transposed surface identically —
including the execution-start clear (each surface constructs its own `ExecutionHighlightTracker`
over its items). The state still lives in `RecipeRowViewModel.ChangedColumns`, but each surface
owns its **own** row instances. The transposed-specific pieces are on the view side only:

- Click-away: `TransposedRecipeGridView` resolves cell presses through the shared
  `ChangedCellClickResolver` (same no-`IsReadOnly`-guard rule) and routes the clear through
  the surface's `ClearChangedByClickAway`.
- Painting: the `changed` style class on the cell border, rules in
  `Styles/TransposedGridStyles.axaml`.

Because both surfaces are live simultaneously and hold separate row VMs, the changed sets
would diverge if one surface alone applied them. `RecipeRowUpdateSynchronizer` keeps them
aligned: on every `PropertyUpdated` each surface runs it on its own row. It marks every key
`ChangedValueKeys(oldStep, newStep)` reports and unmarks every removed key. An added key with an
empty value is empty to empty and stays unmarked. An applicability recompute runs alongside. The
edited cell, a typed cell or the selector cell, is then cleared on both surfaces through
`ChangeMarkClearBroadcaster`, so only the consequences of the edit stay marked: seeded or
reseeded columns and formula-coupled cells, which receive new values inside the same mutation.

The click-away clear fires no mutation, so the synchronizer never sees it. It has its own
cross-surface channel instead: `ChangeMarkClearBroadcaster` (a DI singleton both surfaces
subscribe to; the edit clear above uses it too). The acknowledging view calls its surface's `ClearChangedByClickAway(row,
columnKey)`; the surface maps the row to a step index and publishes, and every subscribed
surface — the originator included — clears its own row at that index. The execution-start clear
needs no channel because each surface's highlight tracker observes the coordinator directly.
