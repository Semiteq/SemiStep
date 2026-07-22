# Stale `SelectedStepIndices` After an Index-Shifting Insert/Remove

## Overview
- Correctness bug (data-integrity class, NOT perf), pre-existing, recorded as an out-of-scope follow-up from `Docs/plans/completed/20260715-transposed-selection-perf-fix.md` and `...-recycle-in-place-panel.md`.
- The view-model caches the current selection as `IReadOnlyList<int> SelectedStepIndices` (`RecipeGridSurfaceBase.cs:101`). When a step is inserted or removed at an index at/before a selected step, the cached indices are NOT shifted, so the cache points at the wrong steps. Consumers that read the cache (`ClipboardViewModel`, `RecipeCommandsViewModel`) then act on the wrong steps.
- Fix: maintain the cache in the shared surface. `OnMutation` already runs on every mutation and already carries the signal payload (`RecipeGridSurfaceBase.cs:136`); shift the cached indices arithmetically there, driven by the mutation signal, so the invariant holds for BOTH grids without depending on any control-level signal.

## Context (from discovery)
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs` — shared base for both grids.
  - `SelectedStepIndices` get / private set (`:101`); `UpdateSelection` pushes a materialized list from the view (`:115`).
  - `SelectedStepIndex` singular derives `indices[0]` (`:99`, `:59`); `CanDeleteStep` derives `Count > 0` (`:54`).
  - `Items` is the `ObservableCollection<TItem>` bound as the control's ItemsSource (`:51`).
  - `OnMutation(MutationSignal)` — the mutation pipeline, `switch` over signal at `:150`, calls `ReconcileSelectionWithItems()` at `:194`.
  - `ReconcileSelectionWithItems` (`:216`) — drop-only clamp: early-returns when `All(index => index < itemCount)` (`:225`), never shifts.
  - `CollectSelectedSteps` reads the cache to map indices → steps (`:534`).
- `SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs` — `StepAppended` (`:7`), `StepsInserted(StartIndex, Count)` (`:9`), `StepRemoved(RemovedIndex)` (`:11`), `StepsRemoved(ImmutableArray<int>)` (`:13`), `StepActionChanged` (`:15`), `PropertyUpdated` (`:17`), `RecipeReplaced` (`:19`), `StateRefreshed` (`:21`).
- `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — which command emits which signal: `InsertStep`→`StepsInserted` (`:256`), `InsertSteps`→`StepsInserted` (`:277`), `RemoveStep`→`StepRemoved` (`:263`), `RemoveSteps`→`StepsRemoved` (`:270`), `AppendStep`→`StepAppended` (`:250`), `ChangeStepAction`→`StepActionChanged` (`:284`), `Undo`→`RecipeReplaced` (`:310`), `Redo`→`RecipeReplaced` (`:315`), `NewRecipe`→`RecipeReplaced` (`:320`).
- `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs` — `OnSelectionChanged` re-reads `StepListBox.Selection.SelectedIndexes` and calls `UpdateSelection` (`:191`), wired to the control `SelectionChanged` event (`:58`, disposed `:98`), guarded by `_syncingSelectionFromSurface` (`:34`, `:193`). `StepListBox.Selection` is an `ISelectionModel`.
- `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridView.axaml.cs` — `OnSelectionChanged` re-derives indices via `RecipeRows.IndexOf` over `RecipeGrid.SelectedItems` (`:134`), wired at `:25`. The DataGrid selection is `RecipeGrid.SelectedItems`; it is NOT an `ISelectionModel` and exposes no index-shift signal.
- Consumers of the cache:
  - `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs` — Copy (`:89`), Cut → `RemoveSteps(SelectedStepIndices)` then `RequestSelection` (`:112`, `:119`), Paste insert-position `SelectedStepIndices.Max() + 1` then `RequestSelection` (`:148`, `:159`).
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs` — AddStep uses `SelectedStepIndex + 1` then `RequestSelection` (`:73`, `:79`), DeleteStep uses `SelectedStepIndices` then `RequestSelection` (`:85`, `:97`), Undo/Redo call the coordinator with no follow-up reselect (`:101`, `:106`).
- Test harness: `UIFixture` exposes `Coordinator` (`:42`), `SeedRecipe` (`:133`), `CreateCanonicalSurface` (`:95`), `CreateTransposedSurface` (`:110`). Canonical view harness `ShowViewWithControl` (`CanonicalRecipeGridViewTests.cs:180`); transposed view harness `ShowView` + `UseTransposedColumnsPanel` (`TransposedSelectionIndexTests.cs:162`).
- Package versions: `Directory.Packages.props` pins `Avalonia` 12.0.5 (`:8`), `Avalonia.Controls.DataGrid` 12.0.1 (`:10`). The fix depends on neither — it is version-independent.

## Development Approach
- **testing approach**: Regular (code first, then tests), but LEAD with red surface-level tests that reproduce the stale cache so the fix is falsifiable.
- The fix lives entirely in the shared `RecipeGridSurfaceBase` — one code path, both grids, no per-view work and no Avalonia selection-model dependency.
- **every task includes tests**; all tests pass before the next task starts.
- Preserve existing behavior: multi-select, the O(S) `OnSelectionChanged` read of `Selection.SelectedIndexes`, the `_syncingSelectionFromSurface` guard, `RequestSelection`/`ScrollIntoView`.

## Testing Strategy
- **unit / surface-level tests** (`[AvaloniaFact]`, `Component=UI`, `Area=RecipeGrid`): drive mutations through `UIFixture.Coordinator` and assert `surface.SelectedStepIndices` after `Dispatcher.UIThread.RunJobs()`. These need no realized control, so they cover the canonical grid too (which otherwise has no testable selection signal).
- **live-control agreement tests**: with the control shown, assert cache == control selection indices after the dispatcher settles, for the one case where the control pushes mid-mutation (transposed remove-selected).
- **regression**: existing selection suites stay green — `TransposedSelectionIndexTests`, `TransposedColumnsPanelContractTests` multi-select round-trip, and the selection-cost probe discrimination. One existing test asserts the OLD buggy behavior and MUST be updated (see Task 2).
- **no perf claim**: correctness fix; no baseline/gate.

## Acceptance Evidence
Reproduction is automatable; the evidence is a runnable test, not a description.

- **Baseline, measured 2026-07-22** (throwaway headless probe against current master, since deleted):
  - Canonical (DataGrid), select row 2 then `InsertStep(0)`: cache `[2]` → `[2]` (STALE); the DataGrid shifted its own `SelectedIndex` 2→3 keeping the same item, and fired NO `SelectionChanged` push.
  - Canonical (DataGrid), multi-select rows `{0,2,4}` then `RemoveStep(2)`: cache `[0,2,4]` → `[0,2,4]` (STALE); the DataGrid dropped to 2 selected rows and fired NO push.
  - Transposed (ListBox), select row 2 then `RemoveStep(2)`: control fires `SelectionChanged`, cache reconciles correctly (`TransposedSelectionIndexTests.cs:92` pins this — NOT stale).
  - Transposed (ListBox), select row 2 then `InsertStep(0)`: cache stays `[2]` STALE — `TransposedSelectionIndexTests.cs:130` currently pins this staleness as the known follow-up.
  - Undo/Redo (`RecipeReplaced` → full rebuild): BOTH controls clear the cache to `[]` (selection dropped, not stale).
- **Pass condition after the fix** — run:
  `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~SelectionShift"`
  - Insert-before-selection shifts the cache up by the insert count, on BOTH grids.
  - Remove-before-selection shifts survivors down and drops removed indices, on BOTH grids.
  - Insert/remove AFTER the selection leaves the cache unchanged, on BOTH grids.
  - Consumer end-to-end: select → insert-before → `RemoveSteps(SelectedStepIndices)` deletes the originally-selected step (now at its shifted index), on BOTH grids.
  - `RecipeReplaced` clears the cache to `[]`.

## Progress Tracking
- mark completed items `[x]` immediately.
- add newly discovered tasks with ➕ prefix; blockers with ⚠️ prefix.
- keep this plan in sync with actual work.

## Solution Overview
Maintain `SelectedStepIndices` as a pure function of `(previous indices, mutation signal)`, applied in the shared surface at mutation time. This is the same transaction that drove the `Items` mutation applied to the selection projection — not a hand-maintained duplicate of a control's state.

Why here and not via a control signal (the rejected alternative):
- The canonical grid is an Avalonia `DataGrid` whose selection is `RecipeGrid.SelectedItems`, with no `ISelectionModel` and no `IndexesChanged`. The 2026-07-22 probe confirmed it fires no push on either insert-before or remove-selected, so there is no signal to subscribe. A control-signal fix would work on the ListBox and silently fail on the DataGrid.
- Both surfaces subscribe `coordinator.Mutated` in the base constructor (`RecipeGridSurfaceBase.cs:80`), so `OnMutation` runs even for the orientation-flipped-away grid whose view is detached. A view-side fix cannot cover that surface; a surface-side fix does.
- `OnMutation` sees every signal including `RecipeReplaced`, so the cache-clear-on-rebuild lives in the same place.

Reentrancy (the one sharp edge): inside `OnMutation`, `Items.Insert/RemoveAt` raises `CollectionChanged` synchronously and the transposed ListBox reacts before the code after the `switch` runs. On a remove that includes a selected index the ListBox fires `SelectionChanged` mid-`switch` and pushes the correct post-shift indices. To avoid double-shifting, snapshot the cache at the TOP of `OnMutation`, compute the shift from the snapshot, and assign AFTER the `switch`. For the transposed remove-selected case the snapshot-computed value equals the control's pushed value, so a sequence-equality guard makes the assignment a no-op; for every other case (all DataGrid cases, ListBox insert-before) the surface is the sole writer. Verified by the 2026-07-22 probe: the only control that pushes mid-mutation is the ListBox on remove-selected, and it agrees.

## Technical Details
Add a pure static shift function and call it from `OnMutation`; retire `ReconcileSelectionWithItems`.

The shift applies to the four index-affecting signals ONLY. Every other signal leaves the cache untouched — assigning for them is what would fight the control (see `StepActionChanged` below).

`ShiftSelection(IReadOnlyList<int> selection, MutationSignal signal) -> IReadOnlyList<int>`, defined for:
- `StepsInserted(startIndex, count)`: each `i` → `i >= startIndex ? i + count : i`.
- `StepRemoved(removedIndex)`: drop `i == removedIndex`; each survivor → `i > removedIndex ? i - 1 : i`.
- `StepsRemoved(removedIndices)`: drop any `i` in `removedIndices`; each survivor → `i - (number of removed indices < i)`.
- `RecipeReplaced`: return `[]` (full rebuild invalidates every index).

Signals the shift does NOT assign for (cache left as-is):
- `StepAppended` — adds at the tail, past every selected index; no index moves.
- `PropertyUpdated` — no structural change.
- `StepActionChanged` — `RebuildItem` (`:461`) does `Items[stepIndex] = item`, replacing the reference at a stable index. The cached index stays valid, so the shift leaves it alone. In the live UI the control drops the replaced item and `OnActionChanged` (`:324`) calls `RequestSelection(result.Value)` (`:346`), which re-selects and repushes the cache. Assigning here would re-assert the pre-mutation snapshot and fight that push — hence no assignment for this signal.
- `StateRefreshed` — `OnMutation` already `return`s early at `:181` before reaching the shift.

`OnMutation` shape:
- At the top, before the `switch`: `var selectionSnapshot = SelectedStepIndices;`
- Keep the existing `switch` that mutates `Items` unchanged, preserving both early-outs that bypass the shift: the `StateRefreshed` `return` (`:181`) and the `UnknownActionKeyException` catch-`return` (`:191`).
- Replace the `ReconcileSelectionWithItems()` call (`:194`) with: for `StepsInserted`/`StepRemoved`/`StepsRemoved`/`RecipeReplaced` only, compute `var shifted = ShiftSelection(selectionSnapshot, signal);` and assign `SelectedStepIndices = shifted` when `shifted` is not sequence-equal to the current `SelectedStepIndices` (a mid-`switch` control push may already have set the same value — the guard avoids churn and, for the transposed remove-selected case, a redundant re-assign). For all other signals, do nothing.
- Delete `ReconcileSelectionWithItems` (`:216`): a correct shift drops out-of-range indices on removal and clears on rebuild, so the drop-only clamp is subsumed. Leaving it would be a second, disagreeing writer.

Invariant after the change: for the four index-affecting signals, `SelectedStepIndices` is consistent with `Items` and equal to what the live control selection would report — independent of whether a control is attached. Non-index signals leave the cache untouched; where selection needs to move for them (`StepActionChanged`), the existing `RequestSelection` path governs it.

## What Goes Where
- **Implementation Steps** (`[ ]`): the shift function, the `OnMutation` change, test coverage, doc note.
- **Post-Completion** (no checkboxes): manual smoke on the live app; unrelated follow-ups tracked separately.

## Implementation Steps

### Task 1: Red surface-level tests reproducing the stale cache on both grids

**Files:**
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedSelectionShiftTests.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/CanonicalSelectionShiftTests.cs`

- [x] Transposed: seed a recipe, select index 2, `Coordinator.InsertStep(0, ...)`, `RunJobs`, assert `surface.SelectedStepIndices` == `[3]` (currently FAILS — proves the bug).
- [x] Canonical: same scenario via the DataGrid harness, assert cache == `[3]` (currently FAILS).
- [x] Canonical: multi-select `{0,2,4}`, `Coordinator.RemoveStep(2)`, assert cache == `[0,3]` (currently FAILS — DataGrid fires no push).
- [x] Run — the new tests FAIL for the right reason (stale indices); the rest of the suite stays green.

### Task 2: Shift the cache in `OnMutation`; retire the clamp

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedSelectionIndexTests.cs`

- [x] Add the pure `ShiftSelection(selection, signal)` per Technical Details — defined for `StepsInserted`/`StepRemoved`/`StepsRemoved` (shift) and `RecipeReplaced` (clear).
- [x] Snapshot `SelectedStepIndices` at the top of `OnMutation`; after the `switch`, for those four signals ONLY assign `ShiftSelection(snapshot, signal)` guarded by sequence-equality; assign nothing for other signals; preserve the `StateRefreshed` (`:181`) and exception-catch (`:191`) early returns; remove the `ReconcileSelectionWithItems()` call and delete the method.
- [x] Update `TransposedSelectionIndexTests.cs:130` (`InsertStepBeforeSelection_...LeavesSurfaceIndicesUntouched`) — it pins the OLD stale behavior; rename/retarget it to assert the shifted indices and the control/cache agreement. This is a deliberate expectation flip, not a regression.
- [x] Build green; the Task 1 red tests now PASS.
- [x] Run the existing selection suites — all green.

### Task 3: Cover both directions, multi-select blocks, and the consumers

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedSelectionShiftTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/CanonicalSelectionShiftTests.cs`

- [x] Both grids: insert-before shifts up by the count; remove-before shifts survivors down; insert/remove AFTER the selection leaves the cache unchanged; a multi-select range shifts as a block.
- [x] Both grids: `RecipeReplaced` (drive via `Coordinator.Undo()` after an undoable mutation) clears the cache to `[]`.
- [x] Both grids: `Coordinator.ChangeStepAction(k)` on a selected step `k` leaves the cached index unchanged and still valid (`[k]`) — pins that the shift does not clobber the action-change case, whose live re-selection is governed by `OnActionChanged`→`RequestSelection` (`:346`), not the shift.
- [x] Consumer end-to-end, at least one per grid: select → insert-before → `RemoveSteps(surface.SelectedStepIndices)` removes the originally-selected step (verify by identity/content, not index).
- [x] Transposed live-control agreement: with the view shown, remove-selected leaves cache == `Selection.SelectedIndexes`.
- [x] Run the full suite — green.

### Task 4: Verify acceptance criteria
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~SelectionShift"` — all pass (18 passed, 0 failed).
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green (1462 passed, 0 failed, 0 skipped run by default; 8 Performance explicit probes not run).
- [x] `dotnet format SemiStep.slnx --verify-no-changes` — exit 0, no formatting diffs.
- [x] Confirm the O(S) `OnSelectionChanged` read of `Selection.SelectedIndexes` is untouched (no reintroduced O(S·N) scan). `TransposedRecipeGridView.axaml.cs:200` still reads `StepListBox.Selection.SelectedIndexes.ToList()`. `ShiftSelection` (`RecipeGridSurfaceBase.cs:218`) is O(S) arithmetic: insert/remove map over the selection only; StepsRemoved is O(S·R) over the small removed set; no `IndexOf`-per-item scan over all N items.

### Task 5: Documentation + cleanup
- [x] If `Docs/architecture/recipe-grid-surface.md` documents selection sync, note that index shifts are maintained in `OnMutation`, not via a control signal.
- [x] Move this plan to `Docs/plans/completed/`. (deferred to delivery — plan not moved by exec)

## Post-Completion
*Manual / external — no checkboxes*
- Manual smoke on the live app: with a multi-selection active, exercise Cut/Paste/Delete after an index-shifting mutation and confirm the intended steps are affected. Under current UI gestures the stale window is largely latent (every consumer command calls `RequestSelection` after mutating, and undo clears), so the surface-level tests are the primary evidence; the smoke is a sanity pass.
- Related follow-ups tracked separately (NOT this plan): `TransposedGridSelectionController.ExtendSelectionTo` shift-select batching; the deferred black-box perf harness (`Docs/plans/20260717-blackbox-perf-harness.md`). This bug is correctness and invisible to the perf harness by construction.

**Executed by exec:**
- branch: selection-index-shift
