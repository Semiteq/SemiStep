# Changed-Cell Highlight (issue #63)

## Overview

When a row's action changes, the step is reinitialized with default values; the operator may
not notice. Issue #63 adds a visual signal: a cell seeded with a default value gets an orange
background (`#FFCC80`). The highlight survives PLC sync and clears on three explicit events.

The highlight is a pure UI annotation. It does not call into the seed/recompute machinery itself;
it observes the column keys that machinery reseeds (in `SemiStep.Core` / the coordinator) and
mirrors them as a parallel per-row set and a parallel attached property, reusing the
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
- A static flat brush `CellChangedBrush` / `CellChangedColor` (`#FFCC80`) in
  `SemiStep.UI/Styles/ColorPalette.axaml`, alongside the other flat semantic brushes
  (`AccentBrush`, `ErrorBrush`, `WarningBrush`). It is **not** routed through the config-driven
  `GridStyleOptions` per-depth palette; foreground stays the default (black). The #74 dark-theme
  work will revisit the shade for contrast.

## When cells are marked

Marking happens in `SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`, the base class both
orientation surfaces derive from (see `recipe-grid-surface.md`):

- **Action change** — `RebuildItem` marks the replacement row's `ChangedColumns` to the new
  step's property keys projected to strings (`step.Properties.Keys.Select(id => id.Value)`).
  Only the action-change path marks; append / insert / full-rebuild do not.
- **Selector change** (#71 nested actions) — `OnSelectorValueChanged`, on the success branch
  only, applies a delta after `RecomputeInapplicableColumns`:
  `ApplyChangedDelta(add: selectorEdit.ColumnsToSeed, remove: selectorEdit.ColumnsToDrop)`.
  The selector column itself is the operator's explicit choice and is not marked.

## The three clear triggers

- **Edit** — a new value entered into the cell. `OnCellValueChanged` calls
  `ClearChanged(columnKey)` on the row after a successful update.
- **Click-away** — the cell is clicked, then any other cell is clicked.
  `SemiStep.UI/RecipeGrid/CanonicalRecipeGridView.axaml.cs` tracks a pending
  `(RecipeRowViewModel, columnKey)`; the pending-vs-pressed decision itself lives in
  `ChangedCellClickResolver`. On `CellPointerPressed`, if a pending cell is set and the pressed
  cell differs, it clears the pending cell's orange, then re-arms pending to the pressed cell iff
  that cell `IsChanged`. There is no `IsReadOnly` guard, so click-away still clears while PLC
  sync is active. The view does not clear the row directly: it calls the surface's
  `ClearChangedByClickAway`, which maps the row to its step index (rows that left the projection
  are skipped) and publishes through `ChangedCellClickAwayBroadcaster` so both orientation
  surfaces clear their own row for that step.
- **Execution start** — `SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs` clears every row's
  set (`ClearAllChanged`) on the inactive→active edge only; an already-active line change does
  not re-clear.

## Cascade placement

The orange rule sits after the inapplicable per-depth chains and before the
`DataGridRow:selected` rule in `DataGridStyles.axaml`. Last-match-wins ordering means it beats
the normal background and the idle per-depth tints, and loses to selection. Orange and
inapplicable/read-only are disjoint (inapplicable cells carry no seeded value; read-only columns
are not value cells). Clicking an orange cell selects its row, so that cell shows `AccentBrush`
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
would diverge (edit clears on one surface only; selector deltas apply on one surface only).
`RecipeRowUpdateSynchronizer` keeps them aligned: on every `PropertyUpdated` it diffs the old
step against the new one and applies the equivalent adjustments — a changed value is a
successful edit of that cell (clear), an added key is a selector-seeded column (mark), a
removed key is a selector-dropped column (unmark) — alongside an applicability recompute.
This also makes the "edit clears" rule hold for the selector cell itself and for
formula-coupled cells, which receive real new values inside the same mutation.

The click-away clear fires no mutation, so the synchronizer never sees it. It has its own
cross-surface channel instead: `ChangedCellClickAwayBroadcaster` (a DI singleton both surfaces
subscribe to). The acknowledging view calls its surface's `ClearChangedByClickAway(row,
columnKey)`; the surface maps the row to a step index and publishes, and every subscribed
surface — the originator included — clears its own row at that index. The execution-start clear
needs no channel because each surface's highlight tracker observes the coordinator directly.
