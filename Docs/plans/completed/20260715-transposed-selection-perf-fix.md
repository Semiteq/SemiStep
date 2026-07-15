# Transposed Grid Selection Performance Fix

## Overview
- Eliminate the dominant UI-thread CPU cost in the transposed recipe grid: `TransposedRecipeGridView.OnSelectionChanged` maps each selected item back to its index via `ObservableCollection<StepColumnViewModel>.IndexOf`, an O(S·N) linear scan over all step-columns. On a 2100-step recipe with a large live selection this measured 2808 ms / 18.7 % of UI-thread CPU in a weighted `dotnet-trace` sample, the single biggest active cost. It fires whenever selection membership changes — removals of selected items, reset, and programmatic re-selection — routed through `SelectionModel` (not on every collection mutation; a pure index-shifting insert raises `IndexesChanged`, which `SelectingItemsControl` does not surface as `SelectionChanged`).
- Replace the scan with `StepListBox.Selection.SelectedIndexes`, which the selection model already maintains (O(S), pre-sorted).
- Secondary, independent micro-fix: delete two per-cell reactive binding legs that carry a column-invariant constant (`Descriptor.IsReadOnlyParameter`), trimming binding subscribe/unsubscribe traffic on every recycle rebind.
- Add a checked-in selection-cost regression guard and a measurement discipline so this class of CPU regression is caught by an instrument, not by manual complaint.

This bug has been present since the transposed grid shipped (commit `ed42473`, PR #130) and was untouched by three prior optimization rounds (allocation-reduction, realize-cost-reduction, binding-fix-and-logging), all of which measured allocation or binding errors, never CPU self-time.

## Context (from discovery)
- Files/components involved:
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs` — `OnSelectionChanged` (line 150), `SyncSelectionFromSurface` (120), `OnSelectionRequested` (176).
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPresenter.cs` — `BuildCellSlot` (line 106): the `BindClass("read-only-cell", ...)` and the `read-only` MultiBinding leg.
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs` — `UpdateSelection` (115), `ReconcileSelectionWithItems` (216) consume the index list.
- Related patterns found:
  - `StepListBox.Selection` (`ISelectionModel`) is already used in `TransposedGridSelectionController.cs:80` (`Selection.AnchorIndex`), so `Selection.SelectedIndexes` is idiomatic and available in this Avalonia version.
  - The cell background is a per-slot `MultiBinding` through `TransposedCellBackgroundConverter` (kept deliberately; a prior round chose the converter over class selectors for allocation reasons). `BuildCellSlot` builds the `MultiBinding` per slot, so a per-slot `ConverterParameter` is free.
  - `EnsureSlotsBuilt` already assumes the descriptor set is column-invariant ("slot count is descriptor-driven and constant across columns"), so per-slot descriptor constants are safe to fold in at build time.
  - Existing perf probe pattern: `SemiStep/SemiStep.Tests/Performance/TransposedViewAllocationProbe.cs` (env-gated, `[Trait("Category","Performance")]`, `SEMISTEP_PROBE=1`).
  - Existing selection/render tests: `TransposedRecipeGridViewTests.cs`, `TransposedSelectionBindingTests.cs`, `TransposedCellBackgroundConverterTests.cs`, `TransposedCellStyleRenderTests.cs`, `TransposedViewportJumpTests.cs`.
- Dependencies identified: none new. Fix is self-contained in the UI layer.

## Development Approach
- **testing approach**: Regular (code first, then tests).
- Complete each task fully before the next; small focused changes; all tests pass before moving on.
- Every task includes new/updated tests as separate checklist items.
- Preserve behavior: multi-select semantics, the `_syncingSelectionFromSurface` reentrancy guard, and read-only cell rendering must be unchanged.

## Testing Strategy
- **unit / headless UI tests**: `[AvaloniaFact]` headless tests for selection-index correctness under multi-select across insert/remove; render tests confirming read-only cells still get their background/class after the leg removal.
- **performance guard**: an env-gated probe (`Category=Performance`) that asserts selection-changed handling does not scale with recipe size N — the regression instrument for this exact bug.
- **no e2e**: project has no Playwright/Cypress layer; headless Avalonia tests are the ceiling.

## Progress Tracking
- Mark completed items `[x]` immediately.
- `➕` for newly discovered tasks, `⚠️` for blockers.

## Solution Overview
- **Primary (Fix 1):** rewrite `OnSelectionChanged` body to read `StepListBox.Selection.SelectedIndexes` (materialized with `ToList()` before handing to the surface, since it is a live view over the model's ranges), drop the per-item `StepColumns.IndexOf` loop and the now-redundant `indices.Sort()`. Keep the `_syncingSelectionFromSurface`/`ViewModel is null` guards. `SelectedIndexes` never contains stale/out-of-range indices, so the implicit `IndexOf >= 0` filtering is preserved for free.
- **Secondary (Fix 2):** in `BuildCellSlot`, set the `read-only-cell` class statically from `_descriptors[slotIndex].IsReadOnlyParameter` at build time (no `BindClass`), and feed that bool into the background `MultiBinding` as a `ConverterParameter` instead of a reactive leg. Removes two reactive legs per cell with zero visual change.
- **Guard:** a selection-cost probe that select-all's a large recipe and performs inserts, asserting handling cost stays flat in N.

## Technical Details
- `OnSelectionChanged` new body shape:
  ```csharp
  if (_syncingSelectionFromSurface || ViewModel is null)
  {
      return;
  }
  var indices = StepListBox.Selection.SelectedIndexes.ToList();
  ViewModel.UpdateSelection(indices);
  ```
  (Confirm during implementation that `Selection.SelectedIndexes` is ascending and index-space-aligned with `ViewModel.StepColumns`; the model is the same `ItemsSource`, so indices map 1:1.)
- `TransposedCellBackgroundConverter` gains the read-only bool via `ConverterParameter` rather than a bound leg. Exact leg map (do not miscount):
  - Current 7 legs: `[0]=Self host`, `[1]=Row.ForDepth`, `[2]=Row.IsPastStep`, `[3]=IsReadOnlyParameter`, `[4]=IsApplicable`, `[5]=IsChanged`, `[6]=IsColumnSelected`. Converter guards `values.Count < 7` and reads indices 3-6.
  - Target 6 legs: drop `[3]=IsReadOnlyParameter`. Guard becomes `values.Count < 6`; `isApplicable = values[3]`, `isChanged = values[4]`, `isSelected = values[5]`; `isReadOnly = parameter as bool? ?? false`.
- `read-only-cell` class: currently `border.BindClass("read-only-cell", new Binding(readOnlyParameterPath), border)`. Replace with `if (_descriptors[slotIndex].IsReadOnlyParameter) { border.Classes.Add("read-only-cell"); }` (needs the slot index passed into `BuildCellSlot`).

## What Goes Where
- **Implementation Steps** (checkboxes): the two code fixes, their tests, the perf guard, verification, docs.
- **Post-Completion** (no checkboxes): the manual before/after `dotnet-trace` + `dotnet-counters` capture on the live app, and the branch/merge sequencing that depends on the three prior rounds landing in `master` first.

## Implementation Steps

### Task 1: Replace OnSelectionChanged O(S·N) scan with Selection.SelectedIndexes

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs`
- Modify/Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedRecipeGridViewTests.cs` (or a new `TransposedSelectionIndexTests.cs`)

- [x] Rewrite `OnSelectionChanged` to read `StepListBox.Selection.SelectedIndexes.ToList()` and pass it to `ViewModel.UpdateSelection`, removing the `foreach`/`StepColumns.IndexOf` loop and `indices.Sort()`.
- [x] Keep the `_syncingSelectionFromSurface` and `ViewModel is null` guards; drop the now-unused `StepListBox.SelectedItems` local if no longer referenced.
- [x] `SelectedIndexes` is index-aligned with `StepColumns` (verified: `TransposedRecipeGridView.axaml:48` binds `ItemsSource="{Binding StepColumns}"` with no collection-view wrapping) and ascending (the model keeps ranges sorted). Pin both with a test assertion, not a comment.
- [x] Write headless test: multi-select 3 non-contiguous columns → assert `ViewModel.SelectedStepIndices` equals the expected ascending indices.
- [x] Write headless test: with a live multi-selection, remove a selected column → assert `SelectedStepIndices` is the correctly pruned set (removal of a selected item does raise `SelectionChanged` with `RemovedItems`).
- [x] Confirm which source mutations actually raise `StepListBox.SelectionChanged` in Avalonia 12.0.3 (an index-shifting insert is expected NOT to). Assert the real contract: after any event that fires, `SelectedStepIndices == Selection.SelectedIndexes`. Do NOT assert shift-tracking on insert here — record the pre-existing stale-after-insert gap as a `➕` follow-up (see Post-Completion) if the event indeed does not fire. (Confirmed: `RemoveStep` fires `SelectionChanged` and the contract holds; `InsertStep` before the selection does not fire it, so surface indices stay untouched — pinned by `InsertStepBeforeSelection_DoesNotRaiseSelectionChanged_LeavesSurfaceIndicesUntouched`; stale gap already tracked in Post-Completion.)
- [x] Write headless test: select-all then deselect one → assert indices update correctly.
- [x] Run tests — must pass before Task 2: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~TransposedRecipeGridView|FullyQualifiedName~TransposedSelectionIndex"`

### Task 2: Delete the two constant IsReadOnlyParameter reactive legs in BuildCellSlot

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPresenter.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedCellBackgroundConverter.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedCellBackgroundConverterTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedCellStyleRenderTests.cs`

- [x] Pass the slot index into `BuildCellSlot` (or read `_descriptors[slotIndex]`), set the `read-only-cell` class statically when `IsReadOnlyParameter` is true instead of `BindClass`.
- [x] Remove the read-only `MultiBinding` leg; supply the bool to `TransposedCellBackgroundConverter` via `ConverterParameter` on the per-slot `MultiBinding`.
- [x] Update `TransposedCellBackgroundConverter` per the exact leg map in Technical Details: guard `values.Count < 6`, `isApplicable=values[3]`, `isChanged=values[4]`, `isSelected=values[5]`, `isReadOnly = parameter as bool? ?? false`.
- [x] Update the precedence oracle `TransposedCellBackgroundConverterTests.Convert_MatchesOldRulePrecedence_AcrossFullStateMatrix` (and the depth-clamp cases): the read-only bool moves out of the 7-element values array into the `parameter` argument across the whole state cross-product. This test is the pin from the style-flatten round; a wrong edit loses it.
- [x] Update/confirm render test: a read-only cell still shows the read-only background and carries the `read-only-cell` class; a normal cell does not. (New `WithReadOnlyColumn` config makes `comment` an applicable read-only column; `ReadOnlyCell_CarriesReadOnlyClass_AndUsesReadOnlyBackground` renders it and asserts class + `CellReadOnlyDepth0BrushKey`, and that a normal cell lacks the class.)
- [x] Run tests — must pass before Task 3: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~TransposedCellBackground|FullyQualifiedName~TransposedCellStyleRender"`

### Task 3: Add a selection-cost regression guard

**Files:**
- Create: `SemiStep/SemiStep.Tests/Performance/TransposedSelectionCostProbe.cs`

- [x] Model on `TransposedViewAllocationProbe`: env-gated (`SEMISTEP_PROBE=1`), `[Trait("Category","Performance")]`, `[Trait("Component","UI")]`, `[Trait("Area","RecipeGrid")]`. Note in a comment that, unlike the report-only allocation probe, this one ASSERTS a ratio (deliberate departure).
- [x] Scenario: hold the selection size CONSTANT (a fixed tail range, S=200) while N grows (300 / 1200 / 4800). Select-all would make S=N and force O(N) even with the fix — the fixed-S design is what isolates the `IndexOf`-in-N regression. Drive operations that actually raise `SelectionChanged` (toggle a selected tail column off then on), not index-shifting inserts. (Implemented as a fixed 200-column TAIL range `[N-200..N-1]` toggled off/on, so the old scan was ~N-deep per item.)
- [x] Measure per-event handler cost as a median of repeated runs. CRITICAL: only the selection mutations are inside the stopwatch; the `Dispatcher.RunJobs()` re-render floor stays OUT of the timed window (the handler fires synchronously inside `SelectedItems.Remove/Add`). The first cut of the probe timed the whole toggle including the render, whose large N-independent GC-noisy floor diluted the O(S·N) signal below the 3× guard. Write results to `%TEMP%/semistep_selection_probe.txt`.
- [x] Assert a pre-committed ratio: per-op cost at N=4800 ≤ 3× per-op cost at N=300. The old `IndexOf` scan shows ~16× (linear in N at fixed S); the fix stays flat.
- [x] Run the probe with `SEMISTEP_PROBE=1` and verify discrimination by temporarily restoring the `IndexOf` scan. **Fix in place:** per-selection-event median N=300 → 104.6 µs, N=1200 → 146.4 µs, N=4800 → 93.1 µs; **ratio N=4800/N=300 = 0.89× (limit 3.0×)** — flat. **Regression (IndexOf restored):** N=300 → 309.0 µs, N=1200 → 913.3 µs, N=4800 → 2073.4 µs; **ratio = 6.71×** — FAILS the guard. The 3× threshold sits cleanly between the flat fix and the linear regression.
- [x] Run tests — must pass before Task 4 (probe skips without the env var). (Verified: PASS with `SEMISTEP_PROBE=1`, SKIP without it.)

### Task 4: Verify acceptance criteria
- [x] Verify Fix 1 and Fix 2 are implemented and behavior is unchanged (selection, read-only rendering). (Fix 1: `OnSelectionChanged` reads `StepListBox.Selection.SelectedIndexes.ToList()`, no `StepColumns.IndexOf` loop. Fix 2: `BuildCellSlot` sets `read-only-cell` class statically from `_descriptors[slotIndex].IsReadOnlyParameter`, background `MultiBinding` is 6 legs with `ConverterParameter = isReadOnlyParameter`; converter guards `values.Count < 6` and reads `isReadOnly = parameter as bool? ?? false`. Behavior pinned by the Task 1/Task 2 headless tests.)
- [x] Run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (PASS: 1377 passed, 0 failed, 3 skipped — the env-gated Performance probes — total 1380, 36 s.)
- [x] Run formatter: `dotnet format "SemiStep/SemiStep.slnx" --verify-no-changes` (clean, exit 0.)
- [x] manual gate — see Post-Completion (requires live-app dotnet-trace); the before/after weighted CPU trace cannot run in the automated task loop.

### Task 5: Update documentation
- [x] Update `Docs/architecture/recipe-grid-surface.md`: note that transposed selection sourcing reads `Selection.SelectedIndexes` (not an item→index scan), and record the measurement-discipline gate (open each perf round with a weighted CPU trace + GC counters on the scripted 2100-step scenario; exit criterion is a pre-committed number; ship the after-trace). (Updated the "Two selection directions" transposed leg with the `StepListBox.Selection.SelectedIndexes` sourcing and the O(S·N) / 2808 ms measurement, and added a new "Performance measurement discipline" section covering the open-with-a-weighted-trace gate, the pre-committed exit number, and the checked-in `TransposedSelectionCostProbe` guard.)
- [x] No `CLAUDE.md` change expected (its footer says "do not add specifics"); the measurement-discipline note lands in `Docs/architecture/recipe-grid-surface.md`. (No change needed.)
- [x] Move this plan to `Docs/plans/completed/`. (moved by harness after finalize)

## Post-Completion
*Items requiring manual intervention or external systems — no checkboxes, informational only*

**Branch / merge sequencing (per user's instruction, done outside this plan's task loop):**
- This fix is logically independent of the three stacked rounds (`transposed-grid-allocation-reduction` → `transposed-grid-realize-cost-reduction` → `transposed-binding-fix-and-logging`). Those three carry real, separate value (allocation reductions, the debugger-freeze binding fix, the Serilog channel) and are to be squashed and merged into `master` first.
- After `master` holds the merged rounds, this plan executes on a fresh branch off the updated `master` tip, landing as its own PR.

**Manual verification (the discipline gate):**
- Reproduce on the RELEASE `RIE` config with a ~2100-step recipe. Capture before/after with:
  ```
  dotnet-trace collect --name SemiStep.UI --format Speedscope --duration 00:00:15
  dotnet-counters monitor -n SemiStep.UI System.Runtime[time-in-gc,gen-0-gc-count]
  ```
  Script the same scroll+add interaction both times. Confirm `OnSelectionChanged` inclusive self-time collapses and the felt lag on add-with-selection is gone.
- Keep a 2100-step recipe as a checked-in perf fixture so traces stay comparable across future rounds.

**Follow-up rounds (not this plan):**
- After Fix 1/Fix 2, re-trace to settle whether the `Monitor.Enter` contention bucket (~2048 ms) and the GC bucket (~1831 ms, `StyleBase.Attach` + `TextLayout`) share one allocation root. If the per-mutation `LogInformation` in `RecipeGridSurfaceBase.OnMutation` shows up as Serilog queue contention, downgrade it to `LogDebug`. Do not migrate the cell background to class selectors — weighted data shows `StyleBase.Attach`/`ApplyStyles` is the top allocator, so selectors would feed it.
- Stale-after-insert selection gap: an index-shifting insert does not raise `SelectionChanged` (it raises `IndexesChanged`), and nothing syncs the shift, so `SelectedStepIndices` can go stale after an insert before the selection. Pre-existing, out of scope here; fix by handling `IndexesChanged` or re-reading `Selection.SelectedIndexes` on mutation.
- `TransposedGridSelectionController.ExtendSelectionTo` (`TransposedGridSelectionController.cs:86-90`) clears then adds items one by one, raising one `SelectionChanged` per add → O(K²) handler work plus K `UpdateSelection` notifications for a K-column shift-select. Collapse with `Selection.BeginBatchUpdate()`/`EndBatchUpdate()` or a range select. Weighted trace attributed the cost elsewhere, so it stays out of this plan.
