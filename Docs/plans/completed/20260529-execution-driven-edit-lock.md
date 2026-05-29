# Execution-Driven Edit Lock

## Overview

Switch the recipe edit-lock from being driven by **sync mode** (`IsSyncEnabled`) to being
driven by **execution state** (`recipe_active`). This makes the three operating states behave
as intended:

- **Not connected (sync off):** full editing, no PLC interaction. Editing is available because
  no connection implies no execution (`recipe_active=false`).
- **Connected, recipe not executing (`recipe_active=false`):** full editing; every change
  auto-syncs to the PLC through the existing 1 s debounce.
- **Connected, recipe executing (`recipe_active=true`):** editing is fully locked; PLC writes are
  already blocked by `PlcSyncExecutor.CheckCanSyncAsync`.

This reverts the core decision of the completed plan
`Docs/plans/completed/20260520-per-window-edit-connect-mode.md`, which tied the edit-lock to the
window's Connect/Edit mode. That decision neutralized the already-implemented online-sync feature:
`RecipeSession.NotifySyncIfEnabled` only pushes edits when sync is enabled, yet the UI forbade
edits while sync was enabled, so user-edit-driven sync could never fire in Connect mode.

The problem it solves: enables online editing of a connected (but idle) recipe with auto-push to
the PLC, while still protecting a running recipe from edits.

## Context (from discovery)

Files/components involved:
- `SemiStep.UI/Coordinator/RecipeCoordinator.cs` — the **single** production change point. Builds
  the `CanEditRecipe` observable (lines 81-87) from `_plcStateChangedShared` (`IsSyncEnabled`).
- `SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs:62-64` — `IsReadOnly = !CanEditRecipe`.
- `SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs:36`, `Clipboard/ClipboardViewModel.cs:48`,
  `RecipeFile/RecipeFileViewModel.cs:30` — all gate mutating commands on the same
  `coordinator.CanEditRecipe`. No changes required in these consumers.
- `SemiStep.Core/Plc/State/PlcExecutionInfo.cs` — carries `RecipeActive`; `Empty` has it `false`.
- `SemiStep.Core/Plc/Sync/PlcExecutionMonitor.cs` — on every disconnect path publishes
  `PlcExecutionInfo.Empty` (`RecipeActive=false`) on the execution stream.

Wiring verification (the user's explicit concern — "no connection must set executing=false"):
- `S7Service.ExecutionState => executionMonitor.State`, `IsRecipeActive => executionMonitor.LastKnown.RecipeActive`.
- `DisconnectAsync()` -> `executionMonitor.StopAsync()` -> `PublishAndTrack(Empty)`.
- `OnConnectionLost()` (keep-alive failure / drop) -> `executionMonitor.Stop()` -> `PublishAndTrack(Empty)`.
- Poll loop on `NotConnectedError` -> `onConnectionLost()` -> same path.
- Conclusion: driving the lock from `ExecutionState.RecipeActive` automatically yields
  "editable when disconnected".

Related patterns found:
- `RecipeCoordinator` already exposes `_executionState` as `ObserveOn(MainThreadScheduler).Publish().RefCount()`
  (lines 68-71) and uses the `Replay(1) + Connect()` pattern for `CanEditRecipe`.
- `StubS7Service.PushExecutionState(PlcExecutionInfo)` already exists and feeds the execution stream;
  `UIFixture.S7Service` is reachable from tests.
- Tests currently drive the lock via `UIFixture.SetSyncEnabled`; we add a parallel `SetRecipeActive`.

Dependencies identified:
- `IsSyncEnabled` keeps its current role: it controls whether edits push to the PLC
  (`RecipeSession.NotifySyncIfEnabled`). The two signals become orthogonal; no change there.
- PLC write-block during execution already exists (`PlcSyncExecutor.cs:233-238`). No change there.

## Development Approach

- **Testing approach: Regular** (change production signal first, then update tests to the new behavior).
- Complete each task fully before moving to the next; small, focused changes.
- Every task includes new/updated tests for the code it touches; all tests pass before the next task.
- `dotnet format SemiStep/SemiStep.slnx` before any commit (pre-commit hook enforces it).
- Backward compatibility: not a public API; internal behavior change is the point of the task.

## Testing Strategy

- **Unit/integration tests:** required per task. UI behavior is covered by Avalonia headless tests
  (`[AvaloniaFact]`) in `SemiStep.Tests`.
- **No e2e harness** exists in this project; manual verification is listed in Post-Completion.
- Test commands:
  - Full: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
  - Coordinator: `--filter "Area=Coordinator"`
  - UI: `--filter "Component=UI"`

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with the ➕ prefix; blockers with the ⚠️ prefix.
- Keep this plan in sync with actual work.

## Solution Overview

Change the **source** of one observable. In `RecipeCoordinator`, replace

```csharp
var canEditConnectable = _plcStateChangedShared
    .Select(plcState => plcState.IsSuccess ? !plcState.Value.IsSyncEnabled : !IsSyncEnabled)
    .StartWith(!IsSyncEnabled)
    .DistinctUntilChanged()
    .Replay(1);
```

with

```csharp
var canEditConnectable = _executionState
    .Select(info => !info.RecipeActive)
    .StartWith(!IsRecipeActive)
    .DistinctUntilChanged()
    .Replay(1);
```

All consumers (`RecipeGridViewModel.IsReadOnly`, the three command view-models) already read
`coordinator.CanEditRecipe`, so they follow automatically — no consumer code changes. The
`EditorMustClose` mechanism (forces an open cell editor closed when `IsReadOnly` flips true) is
reused as-is: it now fires when `recipe_active` flips true mid-edit.

Key design decisions:
- The lock signal is exactly `!RecipeActive` — no `IsConnected` term is needed, because disconnect
  forces `RecipeActive=false` (verified above).
- `_plcStateChanged` / `_plcStateChangedShared` / `PlcStateChanged` are kept: they still feed
  connection/sync status to other consumers (e.g. status bar, conflict handling).
- `IsSyncEnabled` keeps controlling auto-push; orthogonality is the goal.

## Technical Details

- `_executionState` is already on the UI scheduler and shared via `Publish().RefCount()`.
  `canEditConnectable.Connect()` adds one long-lived subscriber (disposed by the existing
  `_canEditRecipeConnection.Dispose()` in `Dispose()`), so RefCount stays alive for the
  coordinator's lifetime — same lifecycle as today.
- Initial value: at construction no execution has been observed, so `IsRecipeActive` is `false`
  and `StartWith(!IsRecipeActive)` emits `true` (editable). Matches the disconnected default.
- Under `[AvaloniaFact]`, the headless dispatcher runs scheduler jobs, so pushing execution state
  then asserting synchronously works the same way `SetSyncEnabled` does today.

## What Goes Where

- **Implementation Steps** (`[ ]`): production change + test rewrites in this repo.
- **Post-Completion** (no checkboxes): spec-doc edits (owned by the user) and manual verification.

## Implementation Steps

### Task 1: Drive `CanEditRecipe` from execution state (+ fixture helper + coordinator tests)

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Coordinator/RecipeCoordinatorCanEditRecipeTests.cs`

- [x] In `RecipeCoordinator`, change `canEditConnectable` to derive from `_executionState`:
      `.Select(info => !info.RecipeActive).StartWith(!IsRecipeActive).DistinctUntilChanged().Replay(1)`.
- [x] Leave `_plcStateChanged`, `_plcStateChangedShared`, and the `PlcStateChanged` property intact.
- [x] Add `UIFixture.SetRecipeActive(bool active)` that calls
      `S7Service.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = active, ActualLine = ... })`
      (a minimal `PlcExecutionInfo`).
- [x] Rewrite `RecipeCoordinatorCanEditRecipeTests`: emits `true` on subscribe (no execution);
      flips to `false` when `recipe_active` becomes true; flips back to `true` when it becomes false.
- [x] Add cases: late subscriber receives current value; `DistinctUntilChanged` suppresses duplicate
      `recipe_active=true` emissions.
- [x] Run tests `--filter "Area=Coordinator"` — must pass before next task.

### Task 2: Invert grid read-only tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeGridViewModelReadOnlyTests.cs`

- [x] `IsReadOnly` false initially (no execution); true when `recipe_active=true`; back to false when
      execution stops — driven via `SetRecipeActive`.
- [x] Replace the `IsReadOnly_StaysFalse_WhenExecutionActive_AndSyncDisabled` §2.7 regression test
      with its inverse: `IsReadOnly` is **true** when execution is active.
- [x] Add `IsReadOnly_StaysFalse_WhenSyncEnabled_ButNotExecuting` — the new core behavior
      (connected-idle is editable): call `SetSyncEnabled(true)` with no execution, assert false.
- [x] Update `CellValueChanged_WhenReadOnly_DoesNotMutateSession` to enter read-only via
      `SetRecipeActive(true)`.
- [x] Update `EditorMustClose` tests to fire on `recipe_active` flipping true (not on sync enable).
- [x] Run tests `--filter "Area=RecipeGrid"` — must pass before next task.

### Task 3: Update command CanExecute tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Clipboard/ClipboardViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`

- [x] In each file, change the "gated-false" cases from `SetSyncEnabled(true)` to
      `SetRecipeActive(true)`; rename tests (Connect-mode -> executing).
- [x] Add one test per view-model: gated commands (Add/Delete/Undo/Redo, Cut/Paste, Load/New) remain
      **enabled** when `SetSyncEnabled(true)` but not executing (connected-idle).
- [x] Keep unconditional-command tests (Copy, Save, SaveAs) unchanged.
- [x] Run tests `--filter "Component=UI"` — must pass before next task.

### Task 4: Verify acceptance criteria

- [x] Verify the three states: offline edit (no PLC), connected-idle edit (+ `NotifyRecipeChanged`
      fires), executing locked.
- [x] Confirm `RecipeSessionSyncIsValidTests` and other `SetSyncEnabled`-using tests still pass
      (sync-push path is unchanged).
- [x] Run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- [x] Run `dotnet format SemiStep/SemiStep.slnx`.

### Task 5: Finalize

- [x] Confirm no `CLAUDE.md` / `README.md` change is needed (no new build/test pattern introduced).
- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion

*Items requiring manual intervention or owned externally — informational only.*

**Spec documentation (owned by the user, per their instruction):**
- `Docs/02-ui-requirements.md` §2.5 / §2.7 — currently state blocking is owned by window mode and
  there is "no separate blocked-because-executing state". This must be rewritten to: editing is
  locked by execution (`recipe_active`), not by Connect/Edit mode.
- `Docs/04-plc.md` §4.5 — `recipe_active` rows already describe execution-based locking; verify they
  match the implemented model.
- `Docs/05-plc-protocol.md` — the `recipe_active` field description ("редактирование таблицы
  блокируется") is now accurate for editing; keep the "PLC writes forbidden" clause as-is.

**Manual verification:**
- With a real/simulated PLC: connect to an idle recipe, edit a cell, confirm the debounced write
  reaches the PLC; start execution, confirm the grid and commands lock; stop execution, confirm
  editing unlocks; drop the connection mid-execution, confirm editing unlocks.

**Out of scope:**
- Conflict-on-connect dialog behavior, execution overlay/loop tinting, keyboard-shortcut gaps —
  unchanged by this task.
