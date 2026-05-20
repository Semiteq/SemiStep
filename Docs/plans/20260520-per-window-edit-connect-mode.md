# Per-Window Edit/Connect Mode

## Overview

Every editor window already exposes a binary `IsSyncEnabled` flag (true = `Connect` to PLC, false = `Edit`) and a `ToggleSyncCommand`. The flag is the source of truth for the window's mode; this task does **not** introduce a separate `WindowMode` enum.

What is missing is consistent enforcement of behavioural restrictions from §2.5 and §2.7 of `Docs/02-ui-requirements.md`:

- In `Edit` (sync disabled): full editing, no PLC interaction.
- In `Connect` (sync enabled): no operation that mutates the on-screen recipe — open file, new recipe, paste, cut, add/remove/reorder steps, cell value edit, undo, redo. PLC read/write is the only mutating channel allowed.

§2.7 is explicit: there is **no separate "blocked because PLC is executing"** state. Execution only activates the visual sub-mode (§2.6); blocking is fully owned by the window mode.

### Current state (the actual gap)

The current code has the *wrong* locking model: `RecipeGridViewModel.IsReadOnly` (`RecipeGridViewModel.cs:62-65`) is derived from `coordinator.ExecutionState.RecipeActive` — i.e. cells lock when the PLC reports the recipe is *actively running*, not when the window is in `Connect`. `MainWindow.axaml:34` binds `DataGrid.IsReadOnly` to that flag.

Structural commands (`AddStep`/`DeleteStep` in `RecipeCommandsViewModel.cs:36-37`), undo/redo (`UndoCommand`/`RedoCommand` same file lines 38-39), clipboard mutating commands (`CutStepCommand`/`PasteStepCommand` in `ClipboardViewModel.cs:51-52`), and file commands (`LoadRecipeCommand`/`NewRecipeCommand` in `RecipeFileViewModel.cs:33-34`) are not gated against `IsSyncEnabled` at all. A user in `Connect` can press them freely and break correspondence with the PLC.

### Source of the gating signal

`PlcSyncCoordinator.SetSyncEnabled` (the only writer of `_isSyncEnabled`, `PlcSyncCoordinator.cs:116-125`) always calls `PublishSnapshot`, which feeds `RecipeCoordinator._plcStateChanged`. Therefore **every** path that flips sync — `EnableSync`, `DisableSync`, connection-failure rollback (`PlcLifecycleManager.cs:110`), `Dispose` (`PlcLifecycleManager.cs:120`) — is observable through `RecipeCoordinator.PlcStateChanged`. The signal will be sourced from there, not from `WhenAnyValue(MainWindowViewModel.IsSyncEnabled)` (which only fires when somebody manually re-raises `PropertyChanged`).

### Derived signal (single)

| Signal | Expression |
|--------|------------|
| `RecipeCoordinator.CanEditRecipe` | `IObservable<bool>` from `PlcStateChanged.Select(_ => !IsSyncEnabled).StartWith(!IsSyncEnabled).DistinctUntilChanged()` |

One signal covers structural, value, clipboard-mutating, file-open/new, and undo/redo operations. Earlier drafts split this into `CanEditStructure`/`CanEditValues`; per CLAUDE.md (YAGNI), they are merged. If a future refinement needs finer granularity (e.g. allowing comment edits during Connect), the signal can be split then.

Placing the observable on `RecipeCoordinator` avoids a DI-order problem: sub-VMs are constructed *and injected into* `MainWindowViewModel`, so the observable cannot originate there. All affected sub-VMs already inject `RecipeCoordinator`, so no constructor signature changes — they read `_coordinator.CanEditRecipe` directly. `MainWindowViewModel.CanEditRecipe` (a computed property) is added only if a XAML binding actually needs it; right now nothing does, so it is omitted (YAGNI).

Windows are independent: each has its own `MainWindowViewModel` + `RecipeCoordinator` + `PlcLifecycleManager`, so no cross-window coordination is required.

### Out of scope

- **Execution overlay / running-on-PLC visual** (§2.6) — owned by `Docs/plans/20260520-execution-overlay-and-loop-tinting.md`. `IsRecipeRunningOnPlc`, `ShowExecutionOverlay`, and the disconnect-clears-running behaviour belong there.
- **Push-to-PLC / Receive-from-PLC command gating.** No standalone UI commands exist; auto-sync via `PlcSyncExecutor` handles transitions. `LoadRecipeFromPlcAsync` is invoked only from `PlcConflictDialog.axaml.cs:24`.
- **Conflict-on-Connect with dirty recipe** (§2.5 last paragraph) — already wired through `_plc.PlcRecipeConflictDetected` → `MainWindowViewModel.HandleConflictAsync`. This plan adds an acceptance check on the *timing* (Task 6) but no new code.
- **Filling in missing keyboard shortcuts from §2.4** (`Ctrl+Y` Redo per spec vs current `Ctrl+Shift+Z`; missing `Ctrl+C/V/X`, `Del`, `Ctrl+Shift+T`). Flagged for a separate follow-up task; this plan only verifies that *existing* KeyBindings observe `CanExecute=false`.

## Files involved

- `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — expose `IObservable<bool> CanEditRecipe` derived from `_plcStateChanged`.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — **replace** the existing `_isReadOnly` source: drop the `coordinator.ExecutionState.Select(info => info.RecipeActive)` subscription; project `coordinator.CanEditRecipe.Select(canEdit => !canEdit)`. Expose `IObservable<Unit> EditorMustClose` that emits when `IsReadOnly` flips to `true`.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs` — gate `AddStepCommand`, `DeleteStepCommand`, `UndoCommand`, `RedoCommand` on `coordinator.CanEditRecipe`.
- `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs` — gate `CutStepCommand` and `PasteStepCommand`; `CopyStepCommand` stays unconditional.
- `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` — gate `LoadRecipeCommand` and `NewRecipeCommand`. `SaveRecipeCommand`/`SaveAsRecipeCommand` stay unconditional.
- `SemiStep/SemiStep.UI/RecipeGrid/DataGridEditorCloseBehavior.cs` — **new** attached behavior that subscribes to a `RecipeGridViewModel.EditorMustClose` observable on the bound DataGrid and calls `CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true)`. Attached behavior (not code-behind) so the future transposed-grid view (separate plan) and any additional `DataGrid` host pick the behavior up automatically.
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — attach the new behavior on the existing `DataGrid`.

## Related patterns found

- `RecipeCoordinator` already publishes shared observables via `.Publish().RefCount()` (e.g. `_plcStateChangedShared`, `RecipeCoordinator.cs:69-77`). `CanEditRecipe` follows the same pattern.
- Sub-VMs already inject `RecipeCoordinator`. No constructor signatures change.
- Headless tests use `[AvaloniaFact]` / `[AvaloniaTheory]` in `SemiStep.Tests`.
- Only one place constructs `RecipeGridViewModel` directly outside DI: `SemiStep.Tests/UI/RecipeGridViewModelTests.cs:33`. Since the new code does not change the constructor signature, that test continues to compile.

## Dependencies

- No new packages.
- No Core-layer changes — purely `SemiStep.UI`.

## Development Approach

- **Testing approach: Regular** (implement, then write tests in the same task).
- Each task ends with tests passing.
- `dotnet format SemiStep/SemiStep.slnx` before any commit (pre-commit hook).

## Testing Strategy

- **Unit tests (Component=UI):** `RecipeCoordinator.CanEditRecipe` emits `true` initially, flips to `false` when sync is enabled, back to `true` when disabled, including the failure-rollback path (`EnableSync` followed by a connection failure that calls `SetSyncEnabled(false)` again).
- **Integration tests (Component=UI, Category=Integration):** With a headless Avalonia host, verify `AddStep`, `DeleteStep`, `Cut`, `Paste`, `LoadRecipe`, `NewRecipe`, `Undo`, `Redo` all report `CanExecute=false` after `ToggleSyncCommand` is fired, and back to `true` after toggling again. `Copy`, `Save`, `SaveAs` remain available throughout.
- **Cell-edit suppression test:** With `IsSyncEnabled=true`, `RecipeGridViewModel.IsReadOnly` is `true`; cell commits do not produce mutations in `RecipeSession`.
- **In-flight edit cancellation test:** Open a cell editor with `IsSyncEnabled=false`, flip to `true`, assert the editor closed (DataGrid has no current edit row) — driven by the attached behavior.
- **Execution-active does NOT lock (regression for §2.7):** With `IsSyncEnabled=false` and `ExecutionState.RecipeActive=true`, `IsReadOnly` stays `false`.
- **KeyBinding gating test:** Existing `Ctrl+Z`, `Ctrl+N`, `Ctrl+O` `KeyBinding`s respect `CanExecute=false` in `Connect`. (Avalonia `KeyBinding` honors `ICommand.CanExecute`; this is a confirmation check, not new code.)

## Solution Overview

1. `RecipeCoordinator` exposes `IObservable<bool> CanEditRecipe` built from `_plcStateChangedShared` (already `.Publish().RefCount()`-ed); start with current value, distinct.
2. `RecipeGridViewModel._isReadOnly` is **re-sourced** from `coordinator.CanEditRecipe.Select(c => !c)`; the old `coordinator.ExecutionState.Select(info => info.RecipeActive)` derivation is removed. `ExecutionHighlightTracker` keeps its own `ExecutionState` subscription (unchanged).
3. Each affected sub-VM combines `coordinator.CanEditRecipe` with any existing `CanExecute` predicate via `CombineLatest`. `BehaviorSubject<bool>` and `WhenAnyValue` both emit on subscribe, so the combined observable fires immediately.
4. The DataGrid editor-close on lock transition is implemented as an attached behavior, not code-behind: the behavior subscribes to the bound VM's `EditorMustClose` and calls `CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true)`. Any future host of the same VM (transposed view, additional windows) picks it up declaratively.

## Implementation Steps

### Task 1: Expose `CanEditRecipe` on `RecipeCoordinator`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Coordinator/RecipeCoordinatorCanEditRecipeTests.cs`

- [x] Add public `IObservable<bool> CanEditRecipe` initialised in the constructor:
  `_canEditRecipe = _plcStateChangedShared.Select(_ => !IsSyncEnabled).StartWith(!IsSyncEnabled).DistinctUntilChanged().Replay(1).AutoConnect(0);`
  (`AutoConnect(0)` keeps the replay buffer alive for the coordinator's lifetime; `Replay(1).RefCount()` would drop the cached value if the subscriber count ever falls to zero, breaking late-subscriber tests.)
- [x] Unit test: subscribe, then drive `EnableSync` → expect `false`; `DisableSync` → expect `true`; simulate `EnableSync` failure path (`SetSyncEnabled(true)` then `SetSyncEnabled(false)` via the rollback in `PlcLifecycleManager.cs:110`) — expect `false` then `true`.
- [x] Unit test: late subscriber after sync enabled receives `false` on subscribe (verifies `Replay(1)`).
- [x] Run tests.

### Task 2: Gate structural and undo/redo commands

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`

- [x] No constructor change — read `_coordinator.CanEditRecipe` inside the constructor.
- [x] `AddStepCommand`: `canExecute = _coordinator.CanEditRecipe`.
- [x] `DeleteStepCommand`: `_coordinator.CanEditRecipe.CombineLatest(canDelete, (a, b) => a && b)`.
- [x] `UndoCommand`: `_coordinator.CanEditRecipe.CombineLatest(_canUndo, (a, b) => a && b)`.
- [x] `RedoCommand`: `_coordinator.CanEditRecipe.CombineLatest(_canRedo, (a, b) => a && b)`.
- [x] Tests: each command reports `CanExecute=false` in `Connect`; `true` in `Edit` with prerequisites met.
- [x] Run tests.

### Task 3: Gate clipboard mutating commands

**Files:**
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Clipboard/ClipboardViewModelCanExecuteTests.cs`

- [x] `CutStepCommand`: combine `_coordinator.CanEditRecipe` with existing `canCopyOrCut`.
- [x] `PasteStepCommand`: gate on `_coordinator.CanEditRecipe`.
- [x] `CopyStepCommand`: unchanged (copy does not mutate).
- [x] Tests: `Cut` and `Paste` blocked in `Connect`; `Copy` available in both modes.
- [x] Run tests.

### Task 4: Gate file-open and new-recipe commands

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`

- [x] `LoadRecipeCommand` and `NewRecipeCommand` gated on `_coordinator.CanEditRecipe`.
- [x] `SaveRecipeCommand` / `SaveAsRecipeCommand` — confirmed pure-disk via `RecipeCoordinator.SaveRecipeAsync` (lines 346-386: `_csvService.SaveAsync` + `_session.MarkSaved()`, no step mutation). Remain unconditional.
- [x] Tests: `Load` and `NewRecipe` blocked in `Connect`; `Save`/`SaveAs` available in both modes.
- [x] Run tests.

### Task 5: Re-source `RecipeGridViewModel.IsReadOnly` and add `EditorMustClose`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Create: `SemiStep/SemiStep.UI/RecipeGrid/DataGridEditorCloseBehavior.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (attach the behavior on `RecipeGrid` DataGrid)
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeGridViewModelReadOnlyTests.cs`

- [ ] Replace `_isReadOnly` source: drop `coordinator.ExecutionState.Select(info => info.RecipeActive)`; use `_coordinator.CanEditRecipe.Select(c => !c)`.
- [ ] Keep `coordinator.ExecutionState.Subscribe(_executionHighlightTracker.OnExecutionStateChanged)` (lines 67-69) untouched.
- [ ] Add `IObservable<Unit> EditorMustClose` derived from `this.WhenAnyValue(x => x.IsReadOnly).Where(r => r).Select(_ => Unit.Default)`.
- [ ] Create `DataGridEditorCloseBehavior` — an Avalonia `Behavior<DataGrid>` (or attached property) with a `Trigger` `IObservable<Unit>` property; on each emission, dispatch `dataGrid.CommitEdit(DataGridEditingUnit.Cell, true)`. Call `CommitEdit` on the UI thread defensively (e.g. wrap in `Dispatcher.UIThread.Post` if `CheckAccess()` is false) — the source observable is already main-thread (`_plcStateChangedShared` observes on `MainThreadScheduler`), but the behavior should not rely on that invariant.
- [ ] In `MainWindow.axaml` attach the behavior on the `DataGrid` declared at line 28, bound to `{Binding RecipeGrid.EditorMustClose}`.
- [ ] Find and update tests that assert old behaviour:
  - [ ] `Grep` the test project for tests asserting `IsReadOnly` becomes true when execution starts; rewrite them to assert `IsReadOnly` stays `false` while `IsSyncEnabled=false`, and becomes `true` when `IsSyncEnabled=true`.
  - [ ] Re-check `SemiStep.Tests/UI/RecipeGridViewModelTests.cs:33` — constructor signature unchanged, but assertions about the lock semantics may need to be inverted.
- [ ] Headless test: `IsSyncEnabled=true` ⇒ `IsReadOnly=true`; cell commit attempt produces no `RecipeSession` mutation.
- [ ] Headless test: open editor with `IsSyncEnabled=false`, flip to `true`, assert DataGrid `CurrentColumn`/edit row cleared.
- [ ] Headless test: `ExecutionState.RecipeActive=true` while `IsSyncEnabled=false` — `IsReadOnly` stays `false` (regression check for §2.7).
- [ ] Run tests.

### Task 6: Verify acceptance criteria

- [ ] `RecipeCoordinator.CanEditRecipe` exposed and verified across all sync-flip paths (including failure rollback).
- [ ] All gated commands (`Add`, `Delete`, `Undo`, `Redo`, `Cut`, `Paste`, `Load`, `NewRecipe`) report `CanExecute=false` in `Connect`.
- [ ] `Copy`, `Save`, `SaveAs` remain available in both modes.
- [ ] Cell edits suppressed in `Connect`; in-flight editor closes via the attached behavior on transition.
- [ ] Execution-active no longer blocks editing on its own (§2.7 compliance).
- [ ] Existing `KeyBinding`s (`Ctrl+O`, `Ctrl+S`, `Ctrl+Z`, `Ctrl+Shift+Z`, `Ctrl+N` in `MainWindow.axaml:16-22`) honor `CanExecute=false` in `Connect`: manual repro — toggle into `Connect`, press `Ctrl+N` / `Ctrl+Z`, confirm no effect.
- [ ] Conflict-on-Connect timing: load a dirty recipe diverging from PLC, press `Connect`, confirm `PlcConflictDialog` appears before any further mutation is possible (i.e. the dialog fires from `EnableSync` flow, not from a subsequent polling cycle). If timing is wrong, file a follow-up plan — do not patch in this task without revisiting scope.
- [ ] Switching mode in one window leaves other windows unaffected (manual two-window check).
- [ ] Run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.

### Task 7: Documentation, follow-up, close-out

- [ ] Verify `Docs/02-ui-requirements.md` §2.5–§2.7 still match the implementation (no edits expected).
- [ ] File a follow-up plan stub for missing §2.4 shortcuts (`Ctrl+Y` for Redo, `Ctrl+C`/`Ctrl+V`/`Ctrl+X`/`Del`/`Ctrl+Shift+T`) — outside this plan's scope, but flagged.
- [ ] Move this plan to `Docs/plans/completed/`.

## Post-Completion

**Manual verification:**

- Open two windows. In window A press Sync (Connect), in B leave it off.
- In A: `Add Step`, `Delete Step`, `Paste`, `Cut`, `Open`, `New`, `Undo`, `Redo` are all greyed; in B all active.
- Press `Ctrl+N` / `Ctrl+Z` in A — no effect (KeyBinding respects `CanExecute`).
- Toggle A back to `Edit`; everything becomes available.
- With PLC connected and a recipe running, confirm a window in `Edit` still allows editing — i.e. execution-active alone does not lock the grid (§2.7 compliance).
- With the same PLC running and window A in `Connect`, confirm cell edits are blocked and an in-flight editor closes when toggling into `Connect`.
- With a dirty recipe diverging from PLC, press `Connect` — `PlcConflictDialog` opens before any further action is possible.

**Future tasks unblocked by this one:**

- Execution overlay (§2.6) — separate plan, consumes `IsSyncEnabled && ExecutionState.RecipeActive`.
- Execution status timing — separate plan.
- §2.4 keyboard shortcut completion — separate follow-up plan filed in Task 7.
