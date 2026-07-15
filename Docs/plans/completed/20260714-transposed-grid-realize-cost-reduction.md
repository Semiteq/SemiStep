# Transposed Grid Column-Realize Cost Reduction

## Overview

The transposed recipe grid stutters ("тормозит") and churns ~50-100 MB of working set per added
step column, with a gen0 GC roughly every second add. Two prior optimization rounds (Core allocation
reductions in PR #132 and the `transposed-grid-allocation-reduction` branch) produced zero perceptible
change, because both optimized paths that are not on the hot path the user feels: Core re-analysis and
retained heap. The retained heap was never the problem (6 gcdumps confirmed it plateaus — no leak); the
problem is transient per-realize churn plus UI-thread layout latency, which a gcdump erases by
construction (it forces a full GC before capture and only shows live objects).

A headless allocation probe that realizes the real view and measures allocation across an append
*including the layout pass* (`TransposedViewAllocationProbe`) located the true cost, verified by two
adversarial reviews with additive precision:

- Per-add cost is CONSTANT in N (2.32 MB at N=20/60/120) — a fixed per-column realization cost, not a
  leak and not O(N) accumulation.
- Decomposition of 2,316 KB/add (WithGroups config, 5 cells/column): 755 KB realization chrome (cell
  `Border` + 7-input background MultiBinding + `ContentControl` template resolve + `ListBoxItem`
  container) + 647 KB for 2 live `ComboBox` + 915 KB for 3 live `TextBox`. Live editors are 67%.
- The canonical grid is cheap (~382 KB/add at steady state) NOT because it lazy-builds editors (its
  combo cells are also always-live) but because the DataGrid RECYCLES realized rows: a recycled row
  rebinds at 382 KB versus a fresh build at ~970 KB. The transposed grid DEFEATS its own
  `supportsRecycling:true`: on container recycle, the inner non-virtualized `ItemsControl`
  (`TransposedRecipeGridView.axaml` `ItemsSource="{Binding Cells}"`) receives a fresh `Cells` list and
  rebuilds every cell subtree from scratch.
- The observed 50-100 MB + stutter is the real "add step" auto-scrolling to the inserted step: if the
  viewport is far from the insertion point, the jump realizes a full viewport of columns (~20-25) in
  one dispatcher frame. The one-column-shift probe does not reproduce this, so the probe must be
  extended before and after the fix, and the viewport-jump number is the primary success metric.

This plan applies two levers, in order of root-cause and risk:

1. **Fix the container-recycling defeat first** (root cause, zero UX risk). Realized column containers
   rebind their cell subtrees to the new column's data instead of rebuilding from scratch, the way the
   canonical DataGrid does. The code is already engineered for rebind-on-recycle (the
   `_editingCellProperty` stale-guard, OneWay display bindings, bound `MaxLength`). This turns the
   viewport jump from ~20-25 fresh column builds into ~20-25 rebinds and is required for canonical
   parity (even all-`TextBlock` columns cost ~2x a recycled canonical row without it).
2. **Lazy display cells for both editor kinds, scoped by measurement.** A lightweight `TextBlock`
   display by default, swapping in the `TextBox`/`ComboBox` editor only on edit entry. This removes the
   67%-live-editor weight from the fresh-container build (first realize) and shrinks the live visual
   tree the compositor carries. Its exact scope (both kinds, or only the heavier remaining one) is set
   by the post-recycling measurement, since once containers rebind, live editors leave the jump hot
   path. Click-and-type UX is preserved via the existing select-then-edit gesture, with the editor
   built on the second (edit) press / keystroke.

The two levers compose: recycling reuse removes the rebuild on every scroll-in; laziness reduces the
weight of the remaining fresh builds and the resident visual tree.

## Context (from discovery)

- Files/components involved:
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`) — the
    view: outer vertical `ScrollViewer`, left parameter-name column, horizontal-virtualizing `ListBox`
    of step columns; each column's inner `ItemsControl ItemsSource="{Binding Cells}"` (axaml:74) is the
    rebuild-on-recycle site; cell `Border` carries the 7-input background MultiBinding and a
    `ContentControl Content="{Binding}"`. The code-behind owns `GetActiveEditor` / `CloseActiveEditor` /
    `IsEditing` and the arrow-key tunnel guard; these define "editing" as a focused editor and are
    consumed by `RecipeGridHost`/`MainWindow` exit gating and the `EditorMustClose` subscription.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellTemplateFactory.cs` — builds the per-kind
    always-live editor templates; holds commit logic (`_editingCellProperty` stale-guard captured on
    `GotFocus`, `LostFocus`/`KeyDown` commit, `OnComboBoxSelectionChanged` writeback), input-blocking
    bindings for read-only/inapplicable.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedGridNavigator.cs` — arrow-navigation via
    `FindFocusableEditor`, which searches visual descendants for `TextBox`/`ComboBox` (lines ~152-167);
    breaks if unfocused cells hold only `TextBlock`.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedGridSelectionController.cs` — select-then-edit
    gesture: first press selects the column and focuses the container WITHOUT an editor; second press on
    the sole selected column falls through to the editor.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/StepColumnViewModel.cs` — exposes `Cells` (a `Lazy<>`
    list, one `ParameterCellViewModel` per `ParameterDescriptor`; cell count and row order are CONSTANT
    across columns — this invariance is what enables slot reuse; the never-realized-column `Lazy<>`
    optimization must be preserved).
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ParameterCellViewModel.cs`, `ComboBoxCellViewModel.cs`,
    `PropertyTextCellViewModel.cs`, `ReadOnlyCellViewModel.cs` — cell VMs.
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — `SetPropertyValue` already Ordinal-equality
    guards writes (line ~182) and the action path guards `actionId == _step.ActionKey` (~172), so
    same-value writeback no-ops at the model.
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs` — incremental mutation projection;
    `RecipeCommandsViewModel` + `TransposedRecipeGridView.axaml.cs` drive `ScrollIntoView` on selection
    after an add (the source of the viewport jump).
  - `SemiStep/SemiStep.Tests/Performance/TransposedViewAllocationProbe.cs` — the measurement instrument.
- Related patterns found: canonical `TextCellFactory` / `ComboBoxCellFactory` and DataGrid row
  recycling; headless harness `UIFixture`, `TransposedVirtualizationTests`
  (`PendingEdit_CommitsWhenItsColumnIsRecycledOut`, `RecycledContainer_CarriesExactlyOneSetOfExecutionClasses`,
  `RecycledTextEditor_ShowsRebindTargetCellValue_AfterScroll`), `TransposedEditingTests`,
  `TransposedGridSelectionController` tests (`PlainCellClick_SelectsColumn_WithoutFocusingEditor`,
  `SecondClickOnSelectedColumn_FocusesEditor`, `IsEditing_TracksEditorFocus`).
- Dependencies identified: `[AvaloniaFact]` headless tests; `Dispatcher.UIThread.RunJobs()` runs layout
  on the calling thread, so `GC.GetAllocatedBytesForCurrentThread()` captures managed layout
  allocations (not render/composition-thread cost); `Docs/architecture/recipe-grid-surface.md`.

## Development Approach

- **testing approach**: Regular (code-first), with mandatory parity/regression tests every task. This is
  behavior-preserving refactoring guarded by strong existing UI tests; the risk is regressions in
  edit/commit/recycling/navigation/selection correctness, so every task adds targeted headless tests and
  must keep the full suite green.
- complete each task fully before moving to the next; make small, focused changes.
- **CRITICAL: every task MUST include new/updated tests** and they must pass before the next task.
- **CRITICAL: update this plan file when scope changes during implementation.**
- Measurement discipline: each lever ends by re-running `TransposedViewAllocationProbe`
  (`SEMISTEP_PROBE=1`) and recording the per-add / viewport-jump / gen0 deltas in Progress Tracking.
- Behavior preservation is non-negotiable: select-then-edit gesture, click-and-type, keyboard
  navigation, commit-on-blur, Enter/Escape, read-only/inapplicable gating, ComboBox writeback,
  `IsEditing`/exit gating, and container recycling correctness must all stay identical in behavior.

## Testing Strategy

- **unit/headless tests**: required every task; `[AvaloniaFact]` tests realize the real view and assert
  edit-entry, commit, cancel, selection, keyboard navigation, recycling, and parity.
- **regression gate**: `TransposedVirtualizationTests`, `TransposedEditingTests`, and the selection-
  controller tests must keep passing in behavior (adjust only where the display/editor split
  legitimately changes the tree; the commit-on-recycle test moves from "LostFocus fires" to an explicit
  commit-before-rebind hook — see Task 2).
- **measurement gate (not CI-asserted)**: the allocation probe is a manual `SEMISTEP_PROBE=1` run; its
  numbers are recorded here with ratio-based acceptance targets, not asserted in CI (byte counts are
  environment/framework-version sensitive and would flake).
- no e2e/browser test framework in this project.

## Progress Tracking

- mark completed items with `[x]` immediately when done.
- add newly discovered tasks with plus prefix; document blockers with warning prefix.
- record probe baselines and per-lever deltas here as they are measured.
- **Baseline (Debug, WithGroups, 560x800, one-column shift)**: transposed ~2,316 KB/add, gen0/add ~0.42;
  canonical ~382 KB/add, gen0/add ~0.08. Decomposition: chrome 755 KB, 2 ComboBox 647 KB, 3 TextBox
  915 KB. Wide-config baseline and full-viewport-jump baseline: measured in Task 1.
- **Task 1 measured baselines (Debug, `TransposedViewAllocationProbe`, per-add is bytes/add over 12
  measured appends after 6 warmups; viewport-jump is one `ScrollIntoView(last)` frame after a warmup
  round-trip so it exercises container recycling):**
  - *WithGroups (560x800, 5 cells/column: ComboBox=2, TextBox=5, TextBlock=7):*
    - transposed per-add: N=20 2,322,812 B (gen0/add 0.50); N=60 2,321,343 B (0.42); N=120 2,319,876 B
      (0.42) — CONSTANT in N.
    - canonical per-add: N=20 970,435 B (0.17); N=60 382,061 B (0.08); N=120 383,855 B (0.08) — recycled
      steady state ~382 KB.
    - viewport-jump N=120: total 11,518,432 B over 5 realized columns → **2,303,686 B per realized
      column** (≈ full per-add cost per column: the transposed grid rebuilds cell subtrees on recycle).
  - *WideParams (1400x800, 36 cells/column: ComboBox=4, TextBox=34, TextBlock=39):*
    - transposed per-add: N=20 14,913,892 B (gen0/add 2.50); N=60 14,853,975 B (2.50); N=120 14,854,554 B
      (2.58) — CONSTANT in N.
    - canonical per-add: N=20 2,869,264 B (0.42); N=60 1,016,171 B (0.17); N=120 1,017,318 B (0.17) —
      recycled steady state ~1,017 KB.
    - viewport-jump N=120: total 207,011,992 B over 14 realized columns → **14,786,570 B per realized
      column** (≈ full per-add cost per column; ~14.5x the canonical recycled per-add — the primary
      target for the Task 2 container-reuse fix).
- **Success criteria (ratio, vs same-run canonical)**: primary — viewport-jump bytes per realized
  column <= ~2x the canonical recycled-row cost; secondary — transposed recycled per-add within ~2x
  canonical per-add; gen0/add materially reduced. "Substantially reduced" is not a gate; ratios are.
- **Task 2 measured post-recycling (Debug, `TransposedViewAllocationProbe`, pooled-presenter reuse):**
  - *WithGroups (560x800):* transposed per-add ~638,060 B (was 2,321,343; **3.6x** less), gen0/add
    0.08–0.25 (was 0.42); canonical per-add ~382,910 B → **~1.67x canonical**. Viewport-jump per
    realized column **623,788 B** (was 2,303,686; **3.69x** less) → **~1.63x** the canonical recycled
    cost — **meets the ≤2x primary target**.
  - *WideParams (1400x800):* transposed per-add ~3,376,326 B (was 14,853,975; **4.4x** less), gen0/add
    ~0.50–0.58 (was ~2.58); canonical per-add ~1,016,764 B → **~3.32x canonical**. Viewport-jump per
    realized column **3,302,412 B** (was 14,786,570; **4.48x** less) → **~3.25x** the canonical recycled
    cost — better than 14.5x, but **still above the ≤2x target**.
  - *Mechanism note:* the fixed-slot-in-ItemTemplate design in the plan cannot work — Avalonia's
    `VirtualizingStackPanel` detaches recycled containers (`RemoveInternalChild`) and `ContentPresenter`
    resets its recycling key on reattach, so the ENTIRE ItemTemplate content (verified: the panel
    instance changes on a one-column scroll) is rebuilt on every recycle. Reuse is achieved instead by a
    view-owned POOL of `TransposedColumnCellsPresenter`s (direct editor controls, no per-cell
    `ContentControl`, so they survive detach/reattach) injected into each realized container by
    `TransposedColumnCellsHost`; the ListBox still virtualizes, the heavy cell subtrees are reused.
- **Task 3 measured post-text-lazy (Debug, `TransposedViewAllocationProbe`, lazy TextBox display/editor
  swap):**
  - *WithGroups (560x800):* transposed per-add ~468,168 B (was post-Task-2 ~638,060; canonical ~384,197)
    → **~1.22x canonical** (was 1.67x), gen0/add 0.08–0.17. Viewport-jump per realized column
    **452,419 B** (was 623,788; **1.38x** less) → **~1.18x** the canonical recycled cost — well under the
    ≤2x primary target. Realized-column census now ComboBox=2, TextBox=2 (combo-internal only),
    TextBlock=7: the property-text editors are no longer live.
  - *WideParams (1400x800):* transposed per-add ~1,456,493 B (was post-Task-2 ~3,376,326; canonical
    ~1,018,231) → **~1.43x canonical** (was 3.32x), gen0/add 0.17–0.25 (was ~0.50–0.58). Viewport-jump
    per realized column **1,418,376 B** (was 3,302,412; **2.33x** less) → **~1.39x** the canonical
    recycled cost — **now under the ≤2x target** (was ~3.25x). Realized-column census now ComboBox=4,
    TextBox=4 (combo-internal only), TextBlock=39: all 34 property-text editors are lazy.
  - *Note:* text-lazy alone brings BOTH configs under the ≤2x primary target. The remaining >1x gap and
    the 4 live `ComboBox` per wide column are the Task-4 combo-lazy target.
- **Task 4 measured post-combo-lazy (Debug, `TransposedViewAllocationProbe`, lazy ComboBox display/editor
  swap; both editor kinds now lazy):**
  - *WithGroups (560x800):* transposed per-add ~284,107 B (was post-Task-3 ~468,168; canonical ~383,764)
    → **~0.74x canonical** (below the recycled canonical row), gen0/add 0.00–0.08. Viewport-jump per
    realized column **265,342 B** (was 452,419; **1.71x** less) → **~0.69x** the canonical recycled cost.
    Realized-column census now **ComboBox=0, TextBox=0, TextBlock=6**: with no cell in edit the column
    holds only display TextBlocks — every editor is lazy.
  - *WideParams (1400x800):* transposed per-add ~1,088,428 B (was post-Task-3 ~1,456,493; canonical
    ~1,017,700) → **~1.07x canonical** (was 1.43x), gen0/add 0.17–0.25. Viewport-jump per realized column
    **1,049,855 B** (was 1,418,376; **1.35x** less) → **~1.03x** the canonical recycled cost (was ~1.39x).
    Realized-column census now **ComboBox=0, TextBox=0, TextBlock=37**: all 4 combos + 34 text editors are
    lazy — the realized column is now all-TextBlock chrome.
  - *Result:* combo-lazy pulls BOTH configs to canonical parity (WithGroups ~0.69x, WideParams ~1.03x of
    the canonical recycled row on the viewport-jump metric) — well inside the ≤2x primary target, and the
    residual live-ComboBox weight is gone (0 combos when no cell is in edit; one is built only during an
    active edit).
- **Lazy-editor scope decision (Tasks 3-4)**: keep BOTH lazy levers. Post-recycling, WithGroups already
  sits at ~1.63x canonical, but WideParams is still ~3.25x because each fresh-container build and each
  jump still instantiates all live editors (34 `TextBox` + 4 `ComboBox` per wide column). `TextBox`
  laziness (Task 3) is justified by COUNT (34/column drives the wide gap); `ComboBox` laziness (Task 4)
  by per-instance WEIGHT (each combo is the heaviest single cell). Both are needed to pull WideParams
  from ~3.25x down toward the ≤2x target; the user chose both, and the measurement confirms neither is
  redundant.

## Solution Overview

- **Recycling reuse (Task 2, the root fix)**: the inner presenter's slot structure is stable because
  every column has one cell per `ParameterDescriptor` in the same row order. Replace the rebuild-on-swap
  inner `ItemsControl` with a fixed-slot presenter built once per container from `ParameterDescriptors`,
  where slot i's data context resolves to `Cells[i]`. When the `ListBoxItem` recycles to a new
  `StepColumnViewModel`, each slot rebinds `Cells[i]` and the subtree persists (rebind, not rebuild),
  mirroring DataGrid row recycling. A recycled editor now persists and may keep focus, so commit-on-
  recycle must move from the current "editor destroyed -> LostFocus -> commit" side effect to an
  explicit commit-before-rebind hook (on container `DataContext` change / `ContainerClearing`), with the
  `_editingCellProperty` stale-guard as backstop.
- **Lazy cell (Tasks 3-4)**: a view-level edit coordinator (see Technical Details) owns the single
  active edit. Cells render a display `TextBlock` by default; the editor is constructed only on edit
  entry via the existing select-then-edit gesture (or F2 / typing on a focused display visual) and
  released on commit. Read-only cells stay a `TextBlock` always. Display visuals are focusable so
  keyboard navigation traverses cells; `FindFocusableEditor` is reworked to target display visuals and
  enter edit on demand.
- **Why this fits**: it converges the transposed grid onto the canonical reuse model, keeps the
  router/surface projection untouched, and is contained to the view + template factory + navigator +
  selection controller + a view-level edit coordinator.

## Technical Details

- **Edit-state ownership (pinned decision)**: a single view-level edit coordinator (owned by
  `TransposedRecipeGridView`) holds the one active edit (its container/cell and the built editor),
  performs the display->editor swap, and commits/reverts on exit, on entering another cell, and before a
  container rebind/recycle. Cells hold NO `IsEditing` state (at most a coordinator-driven projection).
  This gives one place to reset on recycle and one definition of `IsEditing` for exit gating.
- **Recycling reuse mechanism**: fixed-slot presenter; slot count built once per container from the
  config-constant `ParameterDescriptors`, not per rebind. Slot i binds `Cells[i]`; verify the
  `Classes.*` bindings, the 7-input background MultiBinding, and the `IsSelected`
  `RelativeSource AncestorType=ListBoxItem` leg all rebind cleanly on leaf `DataContext` change. The
  execution-class binder and current-step marker operate at `ListBoxItem`/Row level and are unaffected.
  Preserve the never-realized-column `Lazy<>` optimization in `StepColumnViewModel`.
- **Task 2 spike finding (chosen mechanism, verified against Avalonia 12.0.5 by decompiling
  `Avalonia.Base`/`Avalonia.Controls`)**: the inner `ItemsControl` is replaced by
  `TransposedColumnCellsPanel` (a vertical `StackPanel` subclass). It builds N cell-`Border` slots
  ONCE (N = `Cells.Count`, which equals the config-constant `ParameterDescriptors.Count`, and only
  ever realizes on an actually-realized container so the `Lazy<>` stays intact). Each slot's
  `DataContext` binds to `Cells[i]` via `CellSlotConverter` (index passed as `ConverterParameter`),
  so a container recycle rebinds every slot from `columnA.Cells[i]` to `columnB.Cells[i]` with the
  subtree persisting. The cell `Border` keeps its exact structure — `Classes.*` bindings, the 7-input
  background MultiBinding, and the inner `ContentControl Content="{Binding}"` editor. `ContentPresenter`
  reuses the realized editor across the DataContext change because the same recycling `FuncDataTemplate`
  re-resolves (`ContentPresenter.CreateChild`: `recyclingDataTemplate == _recyclingDataTemplate` →
  passes `oldChild` as `existing`), and the cell kind is constant per slot (descriptor-determined). The
  commit-before-rebind hook is `TransposedColumnCellsPanel.OnDataContextBeginUpdate`, which Avalonia
  calls top-down and (per `StyledElement.DataContextNotifying`) stops at children whose `DataContext`
  is locally set (the bound slot Borders), so the panel's hook fires while the still-focused editor
  holds its pending text and old cell, before any slot rebinds. The editor re-captures its stale-guard
  cell on `DataContextChanged` while focused, so an edit after an in-place recycle targets the shown
  cell.
- **Commit-before-rebind**: an explicit hook fires before a container is rebound to a new column; it
  commits the active edit to the captured cell (stale-guard target), then the slots rebind. Without
  this, a focused persistent editor never raises `LostFocus` and the OneWay display binding overwrites
  pending text — silent edit loss.
- **Select-then-edit under lazy**: first press selects the column and focuses the container without
  entering edit; second press on the sole selected column (or F2 / a printable keystroke on a focused
  display visual) enters edit — the coordinator builds and focuses the editor. Do NOT enter edit on raw
  first pointer-press (that would break column selection).
- **Click-and-type / first-keystroke fidelity**: prefer synchronous editor construction and focus inside
  the input handler so the first character is not dropped or doubled; if a `Dispatcher.Post` is
  unavoidable, buffer `TextInput` during the pending swap. Cover both the `TextInput` and `KeyDown`
  routes.
- **ComboBox writeback**: same-value writes already no-op at the model (`RecipeRowViewModel`), and the
  initial `SelectionChanged` already fires on every always-live realize today; the deliverable is a test
  proving the lazy-open initial selection produces no recipe edit and no dirty marking, not new guard
  code (handler-level guard is optional hardening).
- **Row alignment**: the slot reuse and the display/editor swap must not change cell height or the
  header/marker/row alignment across the frozen left name column and the step columns.

## What Goes Where

- **Implementation Steps** (checkboxes): probe extension, recycling reuse, lazy text cells, lazy combo
  cells, verification, documentation — all in this repo.
- **Post-Completion** (no checkboxes): the user runs the probe against their real PLC YAML config in a
  Release build and captures a `dotnet-counters`/Rider snapshot of a real "add step while scrolled away"
  to confirm the render/composition-side reduction that headless cannot measure.

## Implementation Steps

### Task 1: Extend the allocation probe with real-scale config and the viewport-jump scenario

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Performance/TransposedViewAllocationProbe.cs`
- Create: a wide test config fixture under `SemiStep/SemiStep.Tests/YamlConfigs/` (e.g. `WideParams/`)
  approximating real scale (~30-40 parameters, several combo columns), or extend a config helper to seed
  a high parameter count.
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedViewportJumpTests.cs`

- [x] Parameterize the probe over config name and window width so it runs WithGroups (5 cells) and a
      wide config; keep the existing per-add + gen0 + column-composition output.
- [x] Add a "viewport-jump" measurement: seed N, realize, scroll to horizontal start, then measure the
      allocation of a single `ScrollIntoView(last)` jump (start -> end); report bytes, bytes per
      realized column, and realized-container count for that one frame.
- [x] Add the wide config fixture and confirm the probe realizes multiple combo + text columns with it.
      (`YamlConfigs/WideParams/`, 36 cells/column: probe reports ComboBox=4, TextBox=34, TextBlock=39.)
- [x] Run `SEMISTEP_PROBE=1 dotnet test ... --filter "FullyQualifiedName~TransposedViewAllocationProbe"`;
      record WithGroups, wide-config, and viewport-jump baselines in Progress Tracking.
- [x] Write a suite-resident headless test asserting the viewport-jump path keeps realized-container
      count within a viewport bound after the jump (reuse the `TransposedVirtualizationTests` bound
      pattern); run tests - must pass before Task 2.
      (`TransposedViewportJumpTests.cs`, 2 tests, no SEMISTEP_PROBE gate, both green.)

### Task 2: Reuse column-container cell subtrees on recycle (root fix: rebind, not rebuild)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellTemplateFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/StepColumnViewModel.cs` (only if a stable slot
  projection / indexer access is needed; preserve the `Lazy<>` optimization)

- [x] Time-boxed spike (default to the fixed-slot presenter; do not spend time confirming that a plain
      `ItemsControl` resets on `ItemsSource` ref change — it does): verify the `Classes.*` bindings, the
      7-input background MultiBinding, and the `IsSelected` `RelativeSource` leg rebind cleanly on leaf
      `DataContext` change, and that slot count is built once from `ParameterDescriptors`. Record the
      chosen mechanism in this plan.
      (Spike DISPROVED the fixed-slot-in-ItemTemplate mechanism: Avalonia detaches recycled containers
      and rebuilds the whole ItemTemplate content on recycle — see the mechanism note in Progress
      Tracking. Chosen mechanism: a view-owned pool of direct-editor presenters injected via a host.)
- [x] Replace the rebuild-on-swap inner `ItemsControl` with the fixed-slot presenter so a recycled
      container rebinds slot i from the old column's `Cells[i]` to the new column's `Cells[i]`; keep cell
      height and row alignment identical.
      (`TransposedColumnCellsPresenter` + `TransposedColumnCellsPool` + `TransposedColumnCellsHost`;
      `CellSlotConverter` binds each slot to `Cells[i]`; cell height/row alignment unchanged.)
- [x] Implement the explicit commit-before-rebind hook (container `DataContext` change /
      `ContainerClearing`) so an in-progress edit commits to the captured cell before slots rebind; keep
      the `_editingCellProperty` stale-guard as backstop.
      (`TransposedCellTemplateFactory.CommitFocusedEditorWithin`, called from the host on recycle-out and
      from the presenter's `OnDataContextBeginUpdate`; `_editingCellProperty` stale-guard kept.)
- [x] Preserve the background-state converter, execution-class binding, current-step marker, and frozen
      left name-column alignment across reuse; preserve the never-realized-column `Lazy<>` optimization.
      (Header/marker/name column stay in the ItemTemplate; presenters only touch `Cells` for realized
      columns; `TransposedCellStyleRenderTests` + execution-class tests stay green.)
- [x] Write headless tests: scroll a column out and a new one in reuses the SAME container/editor
      instance (assert reference identity before/after) and shows correct per-cell data; a focused editor
      with pending text whose container rebinds IN PLACE (no focus move) commits to the captured cell
      then shows the rebind-target value; no execution-class or handler leakage. Update
      `PendingEdit_CommitsWhenItsColumnIsRecycledOut` to the commit-before-rebind mechanism.
      (`RecycledContainer_ReusesSameEditorInstance_ReboundToNewColumn`,
      `FocusedEditor_PendingText_CommitsToCapturedCell_ThenReusedEditorShowsRebindTarget`;
      `PendingEdit_CommitsWhenItsColumnIsRecycledOut` reworded to commit-before-rebind. Note: Avalonia
      blurs the detached editor on recycle, so commit fires via LostFocus + the explicit hook, not an
      in-place no-focus-move rebind.)
- [x] Run the probe; record post-recycling per-add + viewport-jump deltas vs canonical. Decide and record
      the remaining lazy-editor scope (which cell kinds still justify laziness given the measured
      fresh-container and compositor-tree cost). Run full tests - must pass before Task 3.
      (Numbers + lazy-scope decision recorded in Progress Tracking; full suite green: 1331 passed, 2
      skipped, 0 failed.)

### Task 3: Lazy display/editor swap for property-text (TextBox) cells

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`,
  the view-level edit coordinator)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellTemplateFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedGridNavigator.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedGridSelectionController.cs`

- [x] Add the view-level edit coordinator (single active edit; owns display->editor swap, focus, commit,
      revert, and reset on exit / entering another cell / before recycle). Redefine `IsEditing` /
      `GetActiveEditor` / `CloseActiveEditor` in terms of the coordinator; keep `EditorMustClose` and
      exit gating behavior intact.
      (`TransposedTextEditCoordinator` owns the one active edit; `GetActiveEditor` returns the
      coordinator's active editor first, then a focused ComboBox; `_editCoordinator.Reset()` on surface
      rebind. Host/MainWindow exit-gating and `EditorMustClose`→`CloseActiveEditor` unchanged.)
- [x] Render a display `TextBlock` (shared display converter) by default for property-text cells; build
      the `TextBox` editor only on edit entry, and release it on commit.
      (`TransposedTextCellPresenter`: display `TextBlock` via `PropertyTextEditingMultiConverter`; editor
      lazily built by `CreateTextBoxEditor` on first edit and swapped back to display on blur/commit.)
- [x] Preserve the select-then-edit gesture: first press selects column without editing; second press on
      the sole selected column enters edit; make display visuals focusable and rework
      `FindFocusableEditor` so arrow navigation traverses cells and a keystroke/F2 enters edit.
      (Selection controller reports fall-through on the second press; `TryEnterTextEditFromPointer` opens
      the editor; presenter is `Focusable`; `FindFocusableEditor` targets `TransposedTextCellPresenter or
      ComboBox`; F2/printable keystroke on a focused display enters edit.)
- [x] Preserve commit semantics: Enter/LostFocus commit through the existing parse path, Escape reverts;
      read-only/inapplicable and surface read-only block entry; reset via coordinator on recycle.
      (Enter/LostFocus → `CommitEditor` (ParseForCommit); Escape reverts and exits; `CanEnterEdit` gates on
      `IsEffectivelyEnabled`; commit-before-rebind via `CommitEdit` on detach + `CommitActiveEditor` on
      the pooled presenter, replacing the focus-based `CommitFocusedEditorWithin`.)
- [x] Write headless tests: select-then-edit (first press selects, second enters edit — mirror
      `PlainCellClick_SelectsColumn_WithoutFocusingEditor` / `SecondClickOnSelectedColumn_FocusesEditor`);
      keyboard traversal focuses display visuals and F2/typing enters edit; click-and-type keeps EXACTLY
      one first character (no drop, no double) over both TextInput and KeyDown, including a rapid
      two-character burst in order; Enter commits, Escape reverts, blur commits; read-only/inapplicable
      does not enter edit; recycle across scroll leaks no editor or stale text; `IsEditing` tracks the
      coordinator.
      (Editing/Navigation/Virtualization tests reworked to the display+F2 gesture; new tests:
      `PrintableKeyThenTextInput_EntersEdit_KeepsExactlyOneCharacter`,
      `TextInputWithoutKeyDown_EntersEdit_KeepsExactlyOneCharacter`, `RapidTwoCharacterBurst_TypesBothInOrder`,
      `KeyboardTraversal_FocusesDisplayPresenter_ThenF2EntersEdit`, `InapplicableTextCell_DoesNotEnterEdit`,
      `SurfaceReadOnly_TextCell_DoesNotEnterEdit`; `EscapeKey_RevertsPendingText` asserts the display + exit;
      `IsEditing_TransposedArm_TracksEditorFocus_ThroughHostForwarding` enters edit via the gesture.)
- [x] Run the probe; record post-text-lazy per-add + viewport-jump delta. Run full tests - must pass
      before Task 4.
      (Numbers recorded in Progress Tracking: WithGroups viewport-jump ~1.18x canonical, WideParams ~1.39x
      — both under the ≤2x target. Full suite green: 1337 passed, 2 skipped, 0 failed.)

### Task 4: Lazy display/editor swap for ComboBox cells

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellTemplateFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (+ `.axaml.cs`)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ComboBoxCellViewModel.cs` (only if a selected-
  display projection is needed)

- [x] Render a display `TextBlock` showing the selected item's text by default for combo cells; build
      the `ComboBox` only on edit entry (second press / focus / dropdown-open intent) via the coordinator.
      (`TransposedComboCellPresenter` shares the `TransposedLazyCellPresenter` base + the one edit
      coordinator with the text path; display TextBlock binds (Value, Items) through
      `ComboBoxDisplayTextConverter`; `TransposedCellTemplateFactory.CreateComboCellPresenter` builds the
      ComboBox lazily via `CreateComboBox` and opens its dropdown on entry.)
- [x] Keep the display text correct across recycling (rebind shows the new cell's selection) and across
      external value changes (selector edit / action change).
      (The OneWay (Value, Items) MultiBinding rebinds on the slot DataContext change and re-fires on any
      Value PropertyChanged; tests `RecycledComboCell_ShowsRebindTargetSelection_NotStale` and
      `ExternalActionChange_UpdatesComboDisplay`.)
- [x] Preserve read-only/inapplicable gating (non-hit-testable, non-focusable when column read-only) and
      surface read-only state; reset edit state via the coordinator on recycle.
      (Presenter binds IsHitTestVisible/Focusable to the interactive expression and IsEnabled to the
      editable expression, so a read-only/inapplicable/surface-read-only combo cannot enter edit;
      `CommitActiveEditor` reverts a combo slot on recycle via the shared base.)
- [x] Write headless tests: entering a combo cell builds the ComboBox and shows the current selection;
      the lazy-open initial selection produces NO recipe edit and NO dirty/IsChanged marking; changing
      selection writes back once; read-only combo does not enter edit; recycle shows the rebind-target
      selection.
      (`TransposedComboEditingTests`: `ComboCell_RendersDisplayText_NoLiveComboBox_ByDefault`,
      `SecondClickOnSelectedComboColumn_EntersEdit_BuildsComboBox_ShowsSelection`,
      `F2OnFocusedComboDisplay_EntersEdit_BuildsComboBox`,
      `LazyComboOpen_InitialSelection_ProducesNoRecipeEditOrDirtyMarking`,
      `ComboSelectionChange_WritesBackExactlyOnce`, `InapplicableCombo_DoesNotEnterEdit`,
      `SurfaceReadOnly_Combo_DoesNotEnterEdit`, `ExternalActionChange_UpdatesComboDisplay`,
      `RecycledComboCell_ShowsRebindTargetSelection_NotStale`. Editing/Navigation tests reworked to the
      combo display + F2/second-click gesture.)
- [x] Run the probe; record post-combo-lazy per-add + viewport-jump delta. Run full tests - must pass
      before Task 5.
      (Numbers recorded in Progress Tracking: WithGroups viewport-jump ~0.69x canonical, WideParams ~1.03x
      — both at parity, ComboBox census 0 when not editing. Full suite green: 1346 passed, 2 skipped, 0
      failed.)

### Task 5: Verify acceptance criteria

- [x] Verify success criteria by ratio (Progress Tracking): viewport-jump bytes per realized column
      <= ~2x canonical recycled-row cost (primary); transposed recycled per-add within ~2x canonical
      per-add (secondary); gen0/add materially reduced. Record final numbers for WithGroups and wide
      config.
      (Fresh `SEMISTEP_PROBE=1` run confirms the recorded post-Task-4 numbers. **WithGroups:** viewport-jump
      265,385 B/realized-col vs canonical recycled 382,499 B → **0.69x** (primary, ≤2x met); transposed
      per-add 283,992 B vs canonical 382,499 → **0.74x** (secondary, ≤2x met); gen0/add 0.00–0.08 vs
      baseline 0.42 — materially reduced. Realized-column census ComboBox=0 TextBox=0 TextBlock=6.
      **WideParams:** viewport-jump 1,050,096 B/realized-col vs canonical 1,017,256 B → **1.03x** (primary);
      transposed per-add 1,084,161 B vs canonical 1,017,256 → **1.07x** (secondary); gen0/add 0.17–0.25 vs
      baseline ~2.58 — materially reduced. Census ComboBox=0 TextBox=0 TextBlock=37. Both configs at
      canonical parity, well inside every gate.)
- [x] Verify behavior parity: select-then-edit, click-and-type, keyboard navigation, commit/cancel,
      read-only/inapplicable, ComboBox writeback, `IsEditing`/exit gating, recycling correctness — all
      unchanged.
      (Confirmed by test inventory + full green run. select-then-edit: `PlainCellClick_SelectsColumn_WithoutFocusingEditor`,
      `SecondClickOnSelectedColumn_FocusesEditor`, `SecondClickOnSelectedComboColumn_EntersEdit_BuildsComboBox_ShowsSelection`.
      click-and-type first-keystroke fidelity: `PrintableKeyThenTextInput_EntersEdit_KeepsExactlyOneCharacter`,
      `TextInputWithoutKeyDown_EntersEdit_KeepsExactlyOneCharacter`, `RapidTwoCharacterBurst_TypesBothInOrder`.
      keyboard nav: `TransposedNavigationTests` (13 tests). commit/cancel: `TypeAndCommitWithEnter_UpdatesCoordinator`,
      `EscapeKey_RevertsPendingText`, `ClickOnOtherColumn_CommitsPendingEditByDefocusing`,
      `Down_FromTextBox_CommitsPendingEditByDefocusing`. read-only/inapplicable: `InapplicableCell_EditorIsDisabled`,
      `ReadOnlyMode_DisablesEditors_AndEditorMustCloseDefocusesActiveOne`, `Inapplicable{Text,Combo}Cell_DoesNotEnterEdit`,
      `SurfaceReadOnly_{TextCell,Combo}_DoesNotEnterEdit`. ComboBox writeback: `LazyComboOpen_InitialSelection_ProducesNoRecipeEditOrDirtyMarking`,
      `ComboSelectionChange_WritesBackExactlyOnce`. IsEditing/exit gating: `IsEditing_TracksEditorFocus`,
      `EditorMustClose_ClosesOpenComboBoxDropdown`. recycling correctness: `RecycledContainer_ReusesSamePresenterInstance_ReboundToNewColumn`,
      `FocusedEditor_PendingText_CommitsToCapturedCell_ThenReusedSlotShowsRebindTarget`,
      `PendingEdit_CommitsWhenItsColumnIsRecycledOut`. No gaps found — no new parity tests needed.)
- [x] Verify edge cases: read-only surface state, inapplicable cells, action change / selector edit,
      empty recipe, single-step recipe, rapid add-while-scrolled-away.
      (Already covered: read-only surface `SurfaceReadOnly_*`/`ReadOnlySurface_BlocksSelectorEdit`;
      inapplicable `Inapplicable*_DoesNotEnterEdit`; action change / selector edit
      `ActionCombo_SelectionChange_ChangesStepAction_AndMarksCells`, `ExternalActionChange_UpdatesComboDisplay`,
      `TransposedSelectorEditTests` (4 tests). Added `TransposedEdgeCaseTests` (3 view-level tests) to close
      the remaining gaps: `EmptyRecipe_View_RendersNameColumn_AndNoStepContainers` (empty recipe: name
      column renders, zero realized containers, no crash), `SingleStepRecipe_View_RealizesOneColumn_WithAllEditorsLazy`
      (single step: one column, zero live TextBox/ComboBox, display presenters only),
      `AddStepWhileScrolledFarAway_RealizesNewColumn_ShowsItsValue_AndStaysViewportBound` (rapid
      add-while-scrolled: realized count stays viewport-bound after auto-scroll, pooled slot rebinds to the
      new column's own value). All 3 green.)
- [x] Run the full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
      (**1349 passed, 2 skipped, 0 failed** — +3 vs the post-Task-4 1346 from the new edge-case tests. The 2
      skips are the env-gated allocation probes.)
- [x] Run `dotnet format SemiStep/SemiStep.slnx` (pre-commit hook enforces it).
      (`dotnet format SemiStep/SemiStep.slnx` ran on this toolchain; it fixed a CHARSET issue on the new
      test file, then `--verify-no-changes` passes clean. No other formatting drift.)

### Task 6: Update documentation

- [x] Update `Docs/architecture/recipe-grid-surface.md` allocation section: document the container-reuse
      model (canonical parity comes from recycling, not lazy editors alone), the view-level edit
      coordinator + lazy swap, and the commit-before-rebind hook.
      (Rewrote the "Allocation characteristics" section: container recycling reuse via the view-owned
      pool of `TransposedColumnCellsPresenter`/`Pool`/`Host` + `CellSlotConverter` and why fixed-slot-in-
      ItemTemplate is not viable in Avalonia; the lazy display/editor swap for both kinds
      (`TransposedLazyCellPresenter` base, `TransposedText`/`ComboCellPresenter`, shared
      `TransposedTextEditCoordinator`); the commit-before-rebind hook; final ratios (WithGroups ~0.69x,
      WideParams ~1.03x). Also corrected the now-stale "always-live editor" mentions in the overview and
      the IsEditing chain.)
- [x] Update `CLAUDE.md` only if a new durable convention emerged (e.g., the edit-coordinator pattern).
      (No new durable convention; skipped. The project CLAUDE.md explicitly forbids specifics — "do not
      add specifics here. See the human readable docs in Docs\*." The edit-coordinator / pooled lazy-cell
      pattern is a grid-specific detail, now documented in recipe-grid-surface.md.)
- [x] Move this plan to `Docs/plans/completed/`.
      ([x] harness moves the plan after finalize (not moved here).)

## Post-Completion

*Items requiring manual intervention or external systems - no checkboxes, informational only*

**Manual verification**:
- Run `TransposedViewAllocationProbe` against the real PLC YAML config in a **Release** build with a
  realistic wide window; the headless number omits render/composition-thread cost and the test config is
  smaller than production, so the real reduction is confirmed on the running app.
- Capture a Rider Timeline (CPU + allocations with stacks) or `dotnet-counters` (gen0 count, allocation
  rate) of a real "add step while scrolled far away" before/after to confirm the stutter and working-set
  churn are gone on the compositor side, which headless cannot measure.
- Sanity-check click-and-type latency and first-keystroke fidelity by hand in the running app (the
  swap-on-edit path is the main UX risk).
