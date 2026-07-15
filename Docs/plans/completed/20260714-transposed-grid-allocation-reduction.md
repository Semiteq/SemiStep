# Transposed Recipe Grid Allocation Reduction

## Overview

The transposed recipe grid retains ~3x the managed heap of the canonical grid for the same recipe
and generates heavy per-mutation GC ("active GC") while steps are added. Six heap dumps
(`dotnet-gcdump`, transposed vs canonical, 8/40/200 steps) established the facts:

- **No leak.** Retained heap plateaus: transposed 56 / 112 / 117 MB at 8 / 40 / 200 steps;
  canonical 19 / 39 / 41 MB (Debug build). Column virtualization works — realized `ListBoxItem`
  8 / 18 / 24, `CompositionVisual` plateaus 2453 / 5086 / 5086. Heap grew only +0.13 MB from
  40 → 200 steps.
- **The 3x is styling/theming machinery, not raw visual weight.** Per realized cell the transposed
  grid carries roughly double the style-activator/resource machinery of the canonical grid,
  exploded by the descendant-selector matrix in `TransposedGridStyles.axaml`.
- **The "active GC" is per-mutation churn.** Every mutation re-analyzes the whole recipe in Core
  (`RecipeSession.Apply → RecipeAnalyzer.Analyze`), O(N) per mutation → O(N²) per build,
  ~165 KB/append at N=500 (transient, returns to baseline after GC). The `supportsRecycling:false`
  cell templates rebuild each column's visual subtree on every recycle, adding scroll/append churn.

This plan is fixed-cost + churn reduction, not a leak fix. It preserves behavior and the
always-live-editor design. Work is structured as fix-and-measure: each task lands independently and
is checked against a heap-dump / allocation-probe delta before moving on.

### Baseline numbers (record; re-measure on Release for clean absolutes)

Delta drivers — transposed minus canonical at 200 steps (Debug, contains `XamlSourceInfo`
diagnostics, so Release will be lower):

| Type | Δ (tr − nontr) | Source |
| --- | --- | --- |
| `DynamicResourceExpression` | +6.3 MB | per-cell style matrix |
| `StyleInstance` | +4.0 MB | per-cell style matrix |
| `StyleClassActivator` | +3.4 MB (16.8 vs 8.8 per visual) | descendant selectors |
| `EventHandler<AvaloniaPropertyChangedEventArgs>` | +2.9 MB | style/binding machinery |
| `AndActivator` + activator lists | +2.9 MB | compound selectors |
| `TemplateBindingExpression` | +2.1 MB | per-cell templates |
| cell-VM ReactiveUI scaffolding | +2.5–3 MB | `ReactiveObject` per cell VM |

Core per-append allocation: ~165 KB at N=500, ~98% in `RecipeAnalyzer.Analyze`.

Core per-append (measured, Debug) — recorded baseline from `CoreAllocationProbe` (config `WithGroups`,
`RecipeSession.AppendStep`, one append at the given recipe size):

| N (recipe size) | per-append bytes |
| --- | --- |
| 10 | 7,368 |
| 100 | 36,480 |
| 500 | 164,968 |

The linear growth in per-append bytes with N is the O(N)-per-mutation churn the Core tasks reduce;
compare later runs against this table.

## Context (from discovery)

- Files/components involved:
  - Core: `SemiStep/SemiStep.Core/Recipes/Analysis/{TimingCalculator,RecipeAnalyzer,LoopParser}.cs`,
    `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs` (note: NOT under `Analysis/`),
    `RecipeSession.cs`, `RecipeMetadataRegistry` (find exact path).
  - UI: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/{TransposedCellTemplateFactory.cs,StepColumnViewModel.cs,ParameterCellViewModel*.cs,TransposedRecipeGridView.axaml}`,
    `SemiStep/SemiStep.UI/Styles/TransposedGridStyles.axaml`, `RecipeGridSurfaceBase.cs`,
    `RecipeRowViewModel.cs`.
  - Tests: `SemiStep/SemiStep.Tests/UI/RecipeGrid/` + Core analysis tests.
- Related patterns:
  - The canonical `DataGrid` binds cells via templates over the row indexer (cell VMs viewport-bound);
    the transposed grid eagerly builds a `ParameterCellViewModel` per step × parameter and keeps it.
  - The transposed ComboBox cell template is already `supportsRecycling:true` and binding-driven —
    it is the model for making the text/readonly templates recyclable.
- Dependencies / ordering:
  - **Prerequisite (resolve before Task 2):** the `recipe-grid-mutation-tail-churn` branch
    (incremental start-time refresh) is unmerged and rewrites `RecipeGridSurfaceBase.cs` including
    `RefreshStepStartTimes` and `CreateItemChecked`. Task 2 (edits `CreateItemChecked`) and Task 3
    (changes the `StepStartTimes` type consumed by `RefreshStepStartTimes`) both touch that file, so
    land the branch first or rebase this work on it. All line numbers below are indicative — verify
    against the working tree at implementation time.

## Development Approach

- **Testing approach**: Regular (code first, then tests). Behavior must stay identical; correctness
  is pinned by the existing contract/analysis tests, and each task adds targeted tests.
- Complete each task fully (code + tests + measurement gate) before the next.
- Small, focused changes; each task is independently landable and could be its own PR (Core churn
  tasks may share one PR; each UI lever its own PR given differing risk).
- **Every task includes new/updated tests** and passes the full suite before the next starts.
- Keep `dotnet format` clean.
- **Maintain backward compatibility and visual parity** — the projected values, formatting, and
  cell appearance across every state combination must be unchanged.

## Testing Strategy

- **Unit / contract tests**: required per task. Core changes are pinned by
  `SemiStep/SemiStep.Tests/...` analysis/snapshot tests (start-times, loop depths, timing) and the
  `RecipeGridSurfaceContractTests` (both surfaces). UI-style changes are pinned by headless
  `[AvaloniaFact]` tests asserting cell background/state for the relevant `ForDepth` × `IsPastStep`
  × `IsReadOnly` × `IsApplicable` combinations, plus a virtualization/recycling test for the
  recyclable-template change.
- **Measurement gate (first-class, "fix and watch")** after each task — not a unit test, a manual
  check recorded in this plan:
  - *Core tasks*: run the headless Core allocation probe (Task 1) at N=10/100/500 and record the
    per-append byte delta vs the previous baseline.
  - *UI tasks*: on a **Release** build, build the transposed grid to 200 steps,
    `dotnet-gcdump collect`, `dotnet-gcdump report`, and diff the driver type totals
    (`DynamicResourceExpression`, `StyleClassActivator`, `StyleInstance`, `CompositionVisual`,
    `PropertyTextCellViewModel`) against the baseline. Record the delta before moving on.
- **No e2e**: headless `[AvaloniaFact]` is the harness.

## Progress Tracking

- Mark completed items `[x]` immediately.
- Record each task's measured delta inline (append to the task section).
- Add newly discovered work with ➕, blockers with ⚠️.

## Solution Overview

Three cheap, low-risk Core changes cut the per-mutation churn (the "active GC"): stop allocating a
`Result<>` per step, replace the start-time dictionary with a dense array, and drop redundant LINQ
in the enclosing-loop map. Three UI changes cut the transposed grid's fixed retained cost and
scroll/append churn: flatten the per-cell background style matrix into a single binding (kills the
style-activator explosion), lighten the cell VM layer (plain `INotifyPropertyChanged` + lazy cell
build + cached action dictionaries), and make the text/readonly cell templates recyclable (kills
rebuild-on-recycle churn). Higher-risk / design-level levers are deferred and documented.

## Technical Details

- `RecipeMetadataRegistry.TryGetAction(int id, out ActionDefinition action)` — a non-allocating
  lookup parallel to the existing `Result<>`-returning `GetAction(int id)` (actions are keyed by
  `int`; `Step.ActionKey` is `int`). Also called per PLC tick via `ExecutionTimeEstimator`, so the
  win extends beyond the analyze path.
- `RecipeSnapshot.StepStartTimes` type changes from `IReadOnlyDictionary<int,TimeSpan>` to
  `IReadOnlyList<TimeSpan>` / `TimeSpan[]`; every reader indexes by dense step index. Update
  `RecipeSnapshot.Empty` too.
- Cell background: one `IMultiValueConverter` mapping the FULL state set
  `(ForDepth, IsPastStep, IsReadOnly, IsApplicable, IsChanged, IsSelected)` to a brush. Note the
  precedence trap: a `MultiBinding` on `Border.Background` is a LOCAL value and outranks style
  setters, so the existing `changed`/`selected` background rules can never win over it — they must
  be folded INTO the converter (reproducing document/last-match-wins order), or the whole thing
  applied as a style setter (not a local binding) with the changed/selected rules kept after it.
  Decide which at implementation and pin it with tests. The `Foreground` setters that live inside
  the read-only/inapplicable/selected rules must be handled explicitly (kept as separate style
  setters or folded in) — deleting those rules for the background must not drop their foreground.
  Source brushes from the palette resources `CellPaletteInstaller` installs, not hardcoded colors.
- Recyclable templates: Avalonia `MultiBinding`/`IMultiValueConverter` is ONE-WAY (no `ConvertBack`).
  So the text cell becomes a OneWay display `MultiBinding` (mirroring the readonly template's
  `_displayConverter`), and the edit COMMIT moves entirely into `LostFocus`/`KeyDown` handlers that
  read the cell from `DataContext` (the `OnComboBoxSelectionChanged` pattern). Stale-guard: capture
  the cell reference on `GotFocus` (or reset pending text on `DataContextChanged`) and commit only
  to the captured cell, so a still-focused recycled `TextBox` cannot push text into the new cell.

## What Goes Where

- **Implementation Steps** (`[ ]`): code, tests, and the per-task measurement gate.
- **Post-Completion** (no checkboxes): the Release-build gcdump re-measure and subjective
  scroll/add smoothness check on the running app.

## Explicit Exclusions (deferred / out of scope)

- **Do not** replace the always-live per-cell `TextBox` with a `TextBlock` + swap-editor-on-focus.
  It is the single largest visual lever but contradicts the stated always-live-editor design and
  needs a product decision plus focus/tab-navigation rework.
- **Do not** suspend the inactive surface's VM projection. Click-away/selection sync relies on both
  surfaces staying live (`RecipeGridSurfaceBase.cs`).
- **Do not** make Core analysis incremental on append yet — medium risk (loop-marker edge cases,
  undo/redo). Revisit only if churn is still painful after the Core tasks.
- **Do not** virtualize the inner per-column `Cells` `ItemsControl` — the parameter axis is ~21
  fully-visible rows; virtualizing buys nothing and breaks alignment with the frozen name column.

## Implementation Steps

### Task 1: Measurement harness and recorded baseline

**Files:**
- Create: `SemiStep/SemiStep.Tests/Performance/CoreAllocationProbe.cs` (explicit-trait, manual-run)
- Modify: this plan file (record baseline)

- [x] Add a headless Core allocation probe: build a recipe by successive appends and measure
      `GC.GetAllocatedBytesForCurrentThread()` for a single append at recipe size 10 / 100 / 500,
      pure Core (no grid surface). Gate it behind an explicit trait/category so it does not run in
      the normal suite; it is a manual measurement tool.
- [x] Document the gcdump A/B protocol in the probe file header or a short doc note: Release build,
      transposed to 200 steps, `dotnet-gcdump collect -p <pid> -o <name>.gcdump`,
      `dotnet-gcdump report`, diff driver type totals.
- [x] Run the probe once and record the baseline per-append bytes in this plan. (See the
      "Core per-append (measured, Debug)" table under Baseline numbers: 7,368 / 36,480 / 164,968 bytes
      at N=10 / 100 / 500.)
- [x] manual — needs a running Release app + `dotnet-gcdump`; not automatable in a headless run. The
      Debug per-append baseline table above is recorded; the Release UI driver-type "A" is collected
      by hand before Task 5–7's A/B (see the gcdump A/B protocol in `CoreAllocationProbe.cs`).
- [x] Verify the probe compiles and runs; confirm it is excluded from the default `dotnet test` run.
      (Runs with `SEMISTEP_PROBE=1` → 1 passed; without it → 1 skipped. Solution builds clean;
      `dotnet format` reports no changes.)

### Task 2: Non-allocating action lookup (Core churn)

**Files:**
- Modify: `RecipeMetadataRegistry` (add `TryGetAction`)
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/TimingCalculator.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs` (`CreateItemChecked`)
- Modify: Core/registry tests

- [x] Add `bool TryGetAction(int id, out ActionDefinition action)` to the registry, without
      allocating a `Result<>` (actions are keyed by `int`).
- [x] Use it in `TimingCalculator.ExtractStepDuration` (was per-step `GetAction`) and in
      `RecipeGridSurfaceBase.CreateItemChecked` (keep the fail-loud `UnknownActionKeyException` path).
      The per-PLC-tick timing path benefits automatically — it flows through
      `TimingCalculator.ExtractStepDuration`, no separate call site to change.
- [x] Write tests: `TryGetAction` success + unknown-key false; timing/analyze results unchanged.
- [x] Run the full suite + the Core probe; record per-append byte delta vs baseline. Must pass.

Measured (Debug, `CoreAllocationProbe`), after vs baseline:

| N | baseline | after | reduction |
| --- | --- | --- | --- |
| 10 | 7,368 | 4,200 | −43.0% |
| 100 | 36,480 | 7,392 | −79.7% |
| 500 | 164,968 | 20,680 | −87.5% |

Removing the per-step `Result<ActionDefinition>` allocation from `ExtractStepDuration` eliminated the
dominant O(N)-per-mutation churn; the remaining per-append growth is near-flat with N.
Full suite: 1305 passed, 1 skipped (probe). `dotnet format`: no changes.

### Task 3: Dense start-time array (Core churn)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/TimingCalculator.cs`,
  `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs` (including `RecipeSnapshot.Empty`)
- Modify: consumers — `RecipeGridSurfaceBase.RefreshStepStartTimes`,
  `ExecutionTimeEstimator.TimeLeftInRecipe` (uses `TryGetValue`, needs the same bounds check);
  grep for any other `StepStartTimes` reader before starting
- Modify: snapshot/timing tests + both surface test files (`CoreTimingTests`/`CoreMutationTests` and
  `RecipeGridSurfaceContractTests`)

- [x] Change `StepStartTimes` from `Dictionary<int,TimeSpan>` to a dense `TimeSpan[]` /
      `IReadOnlyList<TimeSpan>` (indices 0..N-1) and fill it directly in `TimingCalculator`; update
      `RecipeSnapshot.Empty`.
- [x] Update every reader to index by step; preserve the empty/missing behavior the dictionary gave
      (an out-of-range index must behave like the old missing-key path). Readers updated:
      `RecipeGridSurfaceBase.RefreshStepStartTimes` and `ExecutionTimeEstimator.TimeLeftInRecipe`
      (both switched from `TryGetValue` to an `index < Count` bounds check returning the old
      empty-string / `TimeSpan.Zero` fallback), plus the `RecipeGridSurfaceContractTests` oracle.
- [x] Write/adjust tests: start-times identical to before for append/insert/remove/loop recipes on
      both surfaces; snapshot start-times correct. Added
      `TimingCalculatorTests.Calculate_ReturnsDenseStartTimesIndexedByStepIndex` (dense-shape) and
      `ExecutionTimeEstimatorTests.TimeLeftInRecipe_ActualLineBeyondStartTimes_ReturnsZeroLikeMissingKey`
      (out-of-range edge). Existing indexer-based timing/mutation/contract tests stay green.
- [x] Run full suite + Core probe; record delta. Must pass.

Measured (Debug, `CoreAllocationProbe`), after vs Task-2 vs original baseline:

| N | baseline | Task 2 | after | vs Task 2 | vs baseline |
| --- | --- | --- | --- | --- | --- |
| 10 | 7,368 | 4,200 | 3,872 | −7.8% | −47.4% |
| 100 | 36,480 | 7,392 | 5,096 | −31.1% | −86.0% |
| 500 | 164,968 | 20,680 | 9,992 | −51.7% | −93.9% |

Replacing the two `Dictionary<int,TimeSpan>` start-time allocations (one in `TimingCalculator`, plus
the boxed hashing entries) with a single dense `TimeSpan[]` roughly halved the remaining per-append
churn at N=500. Full suite: 1307 passed, 1 skipped (probe). `dotnet format`: no changes.

### Task 4: Trim enclosing-loop map allocations (Core churn)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs` (`BuildEnclosingMap`)
- Modify: snapshot tests

- [x] Replace the per-row `OrderBy().ToList().AsReadOnly()` with a single in-place `List.Sort`.
      Declare the builder as `Dictionary<int, IReadOnlyList<LoopInfo>>` and mutate through the
      concrete `List` reference (a `Dictionary<int,List<>>` is not returnable as the read-only type).
      `List.Sort` is unstable vs the old stable `OrderBy`, but loops enclosing a given row are
      strictly nested with distinct depths, so ordering is preserved — assert this in the test.
      The builder now holds `List<LoopInfo>` values behind the `IReadOnlyList<LoopInfo>` slot,
      appended via a cast, then each list is sorted in place by `Depth` ascending (outer→inner,
      the old `OrderBy(l => l.Depth)` key/direction) and the builder is returned directly — no
      second dictionary pass.
- [x] Write/adjust tests: enclosing-loop map and loop depths unchanged for nested-loop recipes.
      Added `CoreLoopEdgeCasesTests.EnclosingLoops_ThreeNested_OrderedOuterToInner` pinning the exact
      outer→inner order (depths 1/2/3, start indices 0/1/2) for a row inside three nested loops; the
      existing `EnclosingLoops_OrderedOuterToInner` (two nested) and `EnclosingLoopsMap_CorrectlyBuilt`
      stay green.
- [x] Run full suite + Core probe; record delta. Must pass.

Measured (Debug, `CoreAllocationProbe`), after vs Task-3 baseline:

| N | Task 3 | after | reduction |
| --- | --- | --- | --- |
| 10 | 3,872 | 3,816 | −1.4% |
| 100 | 5,096 | 5,040 | −1.1% |
| 500 | 9,992 | 9,936 | −0.6% |

Caveat: the probe recipe (bare `Wait` steps) has NO loops, so `BuildEnclosingMap` is near-empty there
and the delta is only the ~56 bytes/append saved by dropping the second dictionary+`AsReadOnly` pass on
the empty map. This task's real win lands on loop-heavy recipes, where the per-row
`OrderBy().ToList().AsReadOnly()` re-materialization is eliminated in favor of a single in-place sort.
Full suite: 1308 passed, 1 skipped (probe). `dotnet format`: no changes.

### Task 5: Flatten the per-cell background style matrix (UI fixed cost)

**Files:**
- Create: cell-background `IMultiValueConverter` under `SemiStep/SemiStep.UI/...`
- Modify: `SemiStep/SemiStep.UI/Styles/TransposedGridStyles.axaml`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` /
  `TransposedCellTemplateFactory.cs` (bind cell `Border.Background`)
- Modify: UI tests

- [x] First read ALL background-setting rules in `TransposedGridStyles.axaml` and enumerate the full
      state set. It is not four inputs: it includes `Border.transposed-cell.changed` (the changed
      highlight) and the `ListBoxItem:selected Border.transposed-cell...` rules (selection tint), on
      top of `for-depth-N` / `past-step` / `read-only-cell` / `inapplicable`. Also list the
      `TextElement.Foreground` setters that live inside those rules.
- [x] Add a converter over the full input set
      `(ForDepth, IsPastStep, IsReadOnly, IsApplicable, IsChanged, IsSelected)` → brush, reproducing
      the existing document-order (last-match-wins) precedence exactly. Five inputs are cell/row VM
      properties; `IsSelected` has NO VM property — bind `$parent[ListBoxItem].IsSelected` in the
      template (or add a synced VM property). Source brushes from the palette resources the
      `CellPaletteInstaller` / `ExecutionPaletteInstaller` install (selection brushes come from the
      theme layer).
- [x] Resolve the local-value-vs-style precedence: EITHER fold the changed/selected rules into the
      converter and apply it as the cell `Border.Background` (local value), OR apply the converter as
      a style SETTER (not a local binding) and keep the changed/selected rules after it. Pick one and
      state which in the plan. Handle the `Foreground` setters explicitly (keep as separate style
      setters or fold in) — removing background rules must not drop their foreground.
- [x] Remove the now-redundant descendant-selector background rules from `TransposedGridStyles.axaml`.
- [x] Write `[AvaloniaFact]` tests asserting the resolved cell background AND foreground for the FULL
      state matrix — every `ForDepth` × `IsPastStep` × `IsReadOnly` × `IsApplicable` × `IsChanged` ×
      `IsSelected` combination that the old rules produced — matches the pre-change brush.
- [x] Run full suite; must pass. Measurement gate: Release gcdump A/B — record
      `DynamicResourceExpression` / `StyleClassActivator` / `StyleInstance` deltas.
      **Manual** — the gcdump A/B needs a running Release app + `dotnet-gcdump` (not automatable in a
      headless run). Follow the protocol in `CoreAllocationProbe.cs`; the 29 removed
      background setter rules are expected to drop `DynamicResourceExpression` /
      `StyleClassActivator` / `AndActivator` / `StyleInstance` per realized cell.

**Precedence approach (local binding).** Chose option (a): the full state matrix
`(ForDepth, IsPastStep, IsReadOnly, IsApplicable, IsChanged, IsSelected)` — including the changed and
selection tints — is folded into `TransposedCellBackgroundConverter` and applied as a **local**
`Border.Background` MultiBinding in `TransposedRecipeGridView.axaml`. A local value outranks every
style setter, so all 29 background setter rules (base / read-only / inapplicable / the
exec depth·past chains / changed / the four selection variants) were removed from
`TransposedGridStyles.axaml`. The converter reproduces the old document-order, last-match-wins
precedence exactly: selection wins over everything (changed > inapplicable > read-only > plain
selection among selected cells), else changed beats the depth/read-only/inapplicable tints, else
inapplicable > read-only, else the execution depth/past tint (depth-0 idle = plain grid background).
Brushes resolve through the target `Border` as an `IResourceHost` (`TryFindResource`), so the same
visual-tree + application `{DynamicResource}` lookup still applies (works for both the app-scoped
production install and window-scoped test installs).

**Foreground handling (kept as style setters).** The read-only / inapplicable / selected rules also
set `TextElement.Foreground`; those setters were kept (background stripped out of them). The four
selected variants set the identical `SelectionForegroundBrush`, so they collapse to one
`ListBoxItem:selected Border.transposed-cell` foreground rule. Result: three foreground setters
survive (`read-only-cell` → `CellReadOnlyForegroundBrush`, `inapplicable` → `CellDisabledForegroundBrush`
with inapplicable last so it wins when both, and `:selected` → `SelectionForegroundBrush` last).
Foreground precedence is unchanged by construction.

**Tests.** `TransposedCellBackgroundConverterTests` cross-checks the converter against an independent
oracle (a literal transcription of the 29 old rules, last-match-wins) over the full 256-combo state
matrix, plus depth clamp (≥3 → depth-3) and negative-depth (→ depth-0) cases.
`TransposedCellStyleRenderTests` renders the real grid and asserts the live `Border.Background` (grid /
disabled / changed / selection families) and the kept `TextElement.Foreground` setters resolve to the
installed palette brushes. (The `WithGroups` test config has no read-only column, so the read-only
class is exercised at the converter level, not the render level.) Full suite: 1316 passed, 1 skipped
(the manual Core probe). `dotnet format` (UI + Tests): no changes.

### Task 6: Lighten the cell view-model layer (UI per-step growth)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ParameterCellViewModel*.cs` (and subtypes)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/StepColumnViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: UI tests

- [x] Convert `ParameterCellViewModel` and its subtypes from `ReactiveObject` to plain
      `INotifyPropertyChanged` (they only raise `PropertyChanged`; verify nothing `WhenAnyValue`s a
      cell VM before changing).
- [x] Materialize `StepColumnViewModel.Cells` lazily so never-visited columns never build the cell
      VMs; ensure `Dispose` tolerates an unmaterialized `Cells`. Note the win is bounded: once a
      column is scrolled to or keyboard-traversed, `TransposedGridNavigator` reads its `Cells` and
      materialization is permanent — savings cover only columns never reached, not all off-screen.
- [x] Cache the action-derived dictionaries (Units / FormatKinds / GroupItems) per `ActionDefinition`
      instead of rebuilding them per row in `RecipeRowViewModel`.
- [x] Write tests: cell values/formatting and change notifications unchanged on both surfaces; a
      disposed column with unmaterialized cells disposes cleanly.
- [x] Run full suite; must pass. Measurement gate: Release gcdump A/B — record
      `PropertyTextCellViewModel` + ReactiveUI-scaffolding deltas.
      **Manual** — the gcdump A/B needs a running Release app + `dotnet-gcdump` (not automatable in a
      headless run). Follow the protocol in `CoreAllocationProbe.cs`; per realized cell the removed
      `ReactiveObject` base (dropping the per-VM `PropertyChangedEventManager`/`Subject` scaffolding)
      is expected to lower the `PropertyTextCellViewModel` + ReactiveUI-scaffolding totals, and lazy
      `Cells` removes cell VMs entirely for columns never scrolled to.

**Reactive-observer check.** Grepped the whole solution for `WhenAnyValue` / `ObservableForProperty` /
`.Changed` / `.Changing` targeting a cell VM: none exists. Cell VMs are consumed only through Avalonia
`{Binding}` (plain `INotifyPropertyChanged`) on `Value` / `IsApplicable` / `IsChanged` / `FormatKind` /
`Units` / `Items` and by direct reads in `TransposedGridNavigator`. The base was flipped from
`ReactiveObject` to `INotifyPropertyChanged` with a minimal `RaisePropertyChanged` helper; the same
properties notify with the same guard-on-unchanged semantics (the guards live in `RecipeRowViewModel`,
which stays `ReactiveObject`, so cell notifications are unchanged). Subtypes (`PropertyTextCellViewModel`,
`ReadOnlyCellViewModel`, `ComboBoxCellViewModel`) carried no reactive members and needed no change.

**Caching scope.** The `(Units, FormatKinds, GroupItemsByColumn)` triple is cached per
`ActionDefinition` in a static `ConditionalWeakTable<ActionDefinition, ActionColumnMetadata>` on
`RecipeRowViewModel`. The registry hands out a stable `ActionDefinition` instance per action id
(`_actionsById[id]`), so the reference key gives one shared, immutable metadata set across every row
with that action; the weak table lets an entry drop when its action (a discarded test registry) is
collected. The dictionaries are build-once and read-only, so sharing is safe. Pinned by
`RecipeRowViewModelTests.ActionMetadata_TwoRowsSameAction_ShareCachedDictionaries` (reference-equal).

### Task 7: Recyclable text/readonly cell templates (UI churn)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellTemplateFactory.cs`
- Modify: UI tests

- [x] Make the text `TextBox.Text` a OneWay DISPLAY `MultiBinding` fed `FormatKind`/`MaxLength` into
      a shared stateless converter (mirror the readonly template's `_displayConverter`), NOT a TwoWay
      binding — Avalonia `MultiBinding` has no `ConvertBack`. Bind `TextBox.MaxLength` instead of
      baking it. New `PropertyTextEditingMultiConverter` reproduces the old per-cell
      `PropertyTimeEditingConverter.Convert` exactly (units-less, `value.ToString()`); `MaxLength`
      binds through a null→0 converter (0 = Semi's "unlimited", matching the old leave-unset default).
- [x] Move the edit COMMIT out of the binding into the `LostFocus` / `KeyDown` handlers, reading the
      cell from `DataContext` (the `OnComboBoxSelectionChanged` pattern). Implement the stale-guard:
      capture the cell on `GotFocus` (or reset pending text on `DataContextChanged`) and commit ONLY
      to the captured cell, so a still-focused recycled `TextBox` cannot write into the new cell.
- [x] Set `supportsRecycling: true` on the text and read-only templates.
- [x] Write tests: a recycling/virtualization test covering the focused-editor-scrolled-out-of-view
      case (assert no stale write into the recycled-into cell); container reuse without stale-cell
      writes; edit/commit still works; formatting unchanged; rejected/unparseable commit snaps the
      display back and Escape-cancel restores the original value (both unchanged from today).
- [x] Run full suite; must pass. Measurement gate: this is CHURN, not retained heap — use
      `dotnet-counters` (gen0 count, allocation rate) or a `dotnet-trace` GC-events capture while
      scrolling/adding, and note subjective GC/jank. (gcdump measures retention, not churn.)
      **Manual** — gen0/alloc-rate via `dotnet-counters` on a running app while scrolling/adding is
      not automatable in a headless run; expected drop in per-scroll allocation as the cell subtree is
      reused rather than rebuilt on recycle.

**Stale-guard implementation (captured-cell, per-editor attached property).** The edit target is
pinned on `GotFocus` into a private `AttachedProperty<PropertyTextCellViewModel?>` (`EditingCell`) on
the `TextBox` itself, so each recycled editor carries its own captured cell. `CommitEditor` writes
ONLY to that captured cell — never `DataContext` — so a still-focused editor rebound onto a different
cell (recycle) commits its pending text to the cell the user was editing and can never leak it into
the rebind-target cell. The display snap-back runs only while `DataContext` still equals the captured
cell; after a rebind the OneWay display binding already shows the new cell, so it is left untouched.
This keeps the existing `PendingEdit_CommitsWhenItsColumnIsRecycledOut` behavior (recycle-out commits
to the correct cell) intact.

**Reject/Escape preserved: yes.** The commit reuses the exact former parse (`ParseForCommit`, factored
out of `PropertyTimeEditingConverter.ConvertBack`), so rejected/unparseable input returns
`BindingOperations.DoNothing` (no write) and the snap-back restores the model's formatted value — same
as before. Escape overwrites the pending text with the captured cell's formatted value, then the
ensuing commit re-parses that reverted text to an unchanged (no-op) write. Read-only-dropped edits are
unchanged: the write still flows through the coordinator's read-only guard.

**Tests.** `TransposedEditingTests.RecycledEditor_CommitsToCapturedCell_NotTheRebindTarget` rebinds a
focused editor's `DataContext` onto another column's cell and asserts the pending "777" lands on the
captured cell, not the rebind target (would fail with a naive `DataContext`-reading commit).
`TransposedVirtualizationTests.FocusedEditorWithPendingText_RecycledAcrossScroll_DoesNotCorruptOtherCells`
scrolls a real narrow-viewport grid with a pending edit and asserts no other column is corrupted;
`RecycledTextEditor_ShowsRebindTargetCellValue_AfterScroll` asserts a recycled editor renders its
rebind-target cell's formatted value. Existing editing/selector/contract tests (commit, HMS
formatting, invalid-stays-uncommitted, Escape-reverts, read-only-drop, MaxLength) stay green against
the recyclable template. Full suite: 1323 passed, 1 skipped (manual Core probe). `dotnet format`
(UI + Tests): no changes.

### Task 8: Verify acceptance criteria
- [x] Confirm behavior and visual parity are unchanged (contract + style-matrix + recycling tests
      green on both surfaces). Contract tests both surfaces (`RecipeGridSurfaceContractTests`,
      `TransposedRecipeGridSurfaceContractTests`, `CanonicalRecipeGridSurfaceContractTests`), the
      Task 5 cell-background parity tests (`TransposedCellBackgroundConverterTests`,
      `TransposedCellStyleRenderTests`), and the Task 7 recycling tests (`TransposedEditingTests`,
      `TransposedVirtualizationTests`) are all present and green (86 in the combined filter).
- [x] Run the full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
      Result: 1323 passed, 1 skipped (the gated Core allocation probe), 0 failed.
- [x] Confirm `dotnet format` reports no changes. (This dotnet-format version rejects a `.slnx`
      path, so verified per project — `SemiStep.Core`, `SemiStep.UI`, `SemiStep.Tests` all clean.)
- [x] manual — collect on running Release app; the UI retained-heap reduction
      (DynamicResourceExpression/StyleClassActivator/StyleInstance from Task 5,
      PropertyTextCellViewModel/ReactiveUI scaffolding from Task 6) is verified by the headless
      parity/behavior tests but its byte reduction is measured by hand. Collect a final Release
      gcdump at 200 steps transposed per the protocol in `CoreAllocationProbe.cs` and diff the
      driver-type totals vs the Debug "A" baseline.

**Final Core allocation probe (Debug, `CoreAllocationProbe`), cumulative vs original baseline:**

| N | original baseline | final | cumulative reduction |
| --- | --- | --- | --- |
| 10 | 7,368 | 3,816 | −48.2% |
| 100 | 36,480 | 5,040 | −86.2% |
| 500 | 164,968 | 9,936 | −94.0% |

The four Core churn tasks (non-allocating action lookup, dense start-time array, trimmed
enclosing-loop map, plus the earlier `Result<>` removal) cut the per-append allocation at N=500 from
164,968 to 9,936 bytes — a 94.0% cumulative reduction — and flattened the O(N) per-mutation growth.
Full suite: 1323 passed, 1 skipped. Format (Core/UI/Tests): no changes.

### Task 9: [Final] Update documentation
- [x] Note the allocation characteristics in `Docs/architecture/recipe-grid-surface.md` (Core
      analysis cost per mutation; transposed cell-background via converter; recyclable cell templates).
- [x] (harness moves the plan after all phases finish) Move this plan to `Docs/plans/completed/`.

## Post-Completion
*Informational only — no checkboxes.*

**Manual verification:**
- Rebuild Release, build a large recipe by paste and one-by-one adds on the transposed grid, and
  confirm the GC/jank is gone and total retained heap dropped toward the canonical grid's.
- Re-run the full gcdump A/B on Release for clean absolute numbers (the baseline dumps were Debug).
