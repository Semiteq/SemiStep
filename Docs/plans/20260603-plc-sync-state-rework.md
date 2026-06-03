# PLC Sync State Rework (re-enable bug fix + status single-source-of-truth)

## Overview

Two stacked PRs against the PLC sync subsystem (`SemiStep.Core/Plc`).

- **PR 1 — bug fix.** Toggling Sync Off then On emits a spurious `PLC connection lost`. Root cause: `PlcSyncCoordinator.Reset()` sets `Status = Disconnected` and is called for two opposite intents — clean `DisableSync` teardown and runtime connection-loss. A clean disable leaves `Status = Disconnected`; the next `EnableSync` calls `SetSyncEnabled(true)`, which publishes synchronously while the stale `Disconnected` status trips the predicate `(status == Disconnected && isSyncEnabled)` in `PublishSnapshot` before any connection attempt. Fix: split `Reset()` by intent.
- **PR 2 — architecture refactor (stacked on PR 1).** Make `PlcConnectionState` the single source of truth for connectivity. Remove the duplicated `PlcSyncStatus.Disconnected`; replace the connection-loss predicate-inference with an explicit, event-driven signal so the disconnected+enabled ambiguity is structurally impossible.

Problem solved: removes the spurious failure (PR 1) and the duplicated/overloaded state that enabled it (PR 2), leaving a smaller, clearer status model.

## Context (from discovery)

- Files/components involved:
  - `SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs` — owns `_status`, `_connectionState`, `_isSyncEnabled`, the `BehaviorSubject`, `Reset()`, `PublishSnapshot`.
  - `SemiStep.Core/Plc/PlcLifecycleManager.cs` — `DisableSync` (line ~171) and `OnConnectionStateChanged` loss branch (line ~267) both call `Reset()`.
  - `SemiStep.Core/Plc/IPlcSyncService.cs` — declares `Reset()`.
  - `SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs` — `CheckCanSyncAsync` (line ~208) writes `Disconnected` on `!IsConnected`.
  - `SemiStep.Core/Plc/State/PlcSyncStatus.cs`, `PlcSessionSnapshot.cs` — enum + DTO (`InitialState` seeds `Disconnected`).
  - `SemiStep.UI/MainWindow/MainWindowViewModel.cs` — `MapSyncStatus` (has a `Disconnected` case); independent `ConnectionStatus` already renders "Connected/Disconnected" from `IsConnected` (line 99).
  - Tests: `SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`, `SemiStep.Tests/Domain/PlcLifecycleManager*Tests.cs`.
- Related patterns found: status mutation centralized through the coordinator `Status` setter under a single `Lock`; `IPlcSyncService` is the Core/UI boundary; failures currently carried as `Result.Fail(...).WithValue(snapshot)` on a hot `BehaviorSubject`.
- Dependencies: `S7Service.StateChanged` drives `OnConnectionStateChanged`; the UI consumes the stream via a single `ObserveOn(MainThreadScheduler)` + `Publish().RefCount()`.

## Development Approach

- **Testing approach**: Regular (code change, then regression/updated tests within the same task).
- Complete each task fully before the next; build + `dotnet format` + tests must pass before advancing.
- Each task includes new/updated tests. Success and failure scenarios both covered.
- Keep PR 1 minimal — do NOT touch the enum, the predicate, the `Result` wrapper, or the UI. PR 2 owns the structural change.

## Testing Strategy

- **Unit tests** (`SemiStep.Tests`): the headline regression (enable→disable→enable emits no failed snapshot) plus genuine-loss still emits failure; PR 2 updates status-model tests and adds a "disconnected label derives from ConnectionState" assertion where practical.
- **Avalonia headless**: PR 2 touches `MapSyncStatus`; rely on existing UI tests to catch binding regressions.
- Commands: `dotnet build SemiStep/SemiStep.slnx`, `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`, `dotnet format SemiStep/SemiStep.slnx`.

## Progress Tracking

- Mark `[x]` immediately on completion. New tasks prefixed ➕, blockers ⚠️. Keep this file in sync.

## Solution Overview

- **PR 1**: `Reset()` → two intent-named methods. `ResetForDisable()` = `_executor.Reset()` + `Status = Idle`. `HandleConnectionLost()` = `_executor.Reset()` + `Status = Disconnected`. `DisableSync` calls the former; the connection-loss branch calls the latter. The predicate and enum are untouched, so genuine loss still surfaces `PLC connection lost` while a clean disable can no longer poison the next enable.
- **PR 2**: Delete `PlcSyncStatus.Disconnected`. Connectivity reads from `PlcConnectionState`. Connection-loss becomes an explicit coordinator field `_connectionLost` (set only by `HandleConnectionLost`, cleared on `SetSyncEnabled(true)`, on `UpdateConnectionState(Connected)`, and by `ResetForDisable`). `PublishSnapshot` emits `Fail("PLC connection lost")` when `_connectionLost && isSyncEnabled` — an event-driven flag instead of a stale-prone status predicate. `CheckCanSyncAsync` still aborts the write when disconnected but no longer writes a status. `InitialState` seeds `Idle`. `MapSyncStatus` loses its `Disconnected` case (connection label already covered by `ConnectionStatus`).

## Technical Details

- `_connectionLost` is owned entirely by the coordinator (no reach into `PlcSyncExecutor` state, preserving the SRP boundary). It is mutated under the existing `_lock` and read inside `PublishSnapshot` alongside `status`/`isSyncEnabled` for a consistent snapshot point.
- The `Result<PlcSessionSnapshot>` wrapper and the failure-as-value channel are intentionally retained (P3 deferred). PR 2 only changes what TRIGGERS the failure, not how it is transported.
- Genuine-loss path is unchanged end to end: `S7Service` detects loss → `StateChanged(Disconnected)` → `OnConnectionStateChanged` → `HandleConnectionLost` sets `_connectionLost` → `PublishSnapshot` emits the failure. `CheckCanSyncAsync` hitting `!IsConnected` implies a drop already signalled by that path (sync is only enabled after a successful `ConnectAsync`), so dropping its status write is safe.

## What Goes Where

- **Implementation Steps** (`[ ]`): all code + tests + branch/PR mechanics, achievable in this repo.
- **Post-Completion** (no checkboxes): live PLC re-verification of the original Off→On scenario; capturing the `.Value` stack trace that gates the deferred P3.

## Implementation Steps

### Task 1 (PR 1): Split Reset() into intent-named operations

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/IPlcSyncService.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`

- [x] In `IPlcSyncService`, replace `void Reset();` with `void ResetForDisable();` and `void HandleConnectionLost();`.
- [x] In `PlcSyncCoordinator`, implement `ResetForDisable()` (`_executor.Reset()` + `Status = PlcSyncStatus.Idle`) and `HandleConnectionLost()` (`_executor.Reset()` + `Status = PlcSyncStatus.Disconnected`); remove old `Reset()`.
- [x] In `PlcLifecycleManager.DisableSync`, call `ResetForDisable()` (was `Reset()`).
- [x] In `PlcLifecycleManager.OnConnectionStateChanged` loss branch, call `HandleConnectionLost()` (was `Reset()`).
- [x] Update any other `Reset()` callers/stubs (`SemiStep.Tests/Helpers/StubPlcSyncService.cs`).
- [x] `dotnet build SemiStep/SemiStep.slnx` — must compile.

### Task 2 (PR 1): Regression test for enable→disable→enable

**Files:**
- Modify: `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`

- [x] Add a test: after `SetSyncEnabled(true)` → `ResetForDisable()` + `SetSyncEnabled(false)` → `SetSyncEnabled(true)`, the latest `PlcState` emission is NOT failed (the spurious-failure regression).
- [x] Add/confirm a test: `HandleConnectionLost()` while enabled DOES produce a failed snapshot (`PLC connection lost`) — genuine loss preserved.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcSyncCoordinator"` — must pass.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.

### Task 3 (PR 1): Format, commit, push, open PR

- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] `git add` changed files; commit on branch `plc-sync-reenable-fix` (message: `fix: stop spurious 'PLC connection lost' on sync re-enable`).
- [x] Verify `git log origin/master..HEAD --oneline` contains only this change.
- [x] `git push -u origin plc-sync-reenable-fix`; `gh pr create --base master` with a focused description.

### Task 4 (PR 2): Branch off PR 1 (stacked)

- [ ] From the worktree on `plc-sync-reenable-fix`, `git switch -c plc-sync-status-refactor`.

### Task 5 (PR 2): Make connection-loss an explicit signal; remove PlcSyncStatus.Disconnected

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/State/PlcSyncStatus.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/State/PlcSessionSnapshot.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs`

- [ ] Add `_connectionLost` (bool) to `PlcSyncCoordinator`, mutated under `_lock`. Set `true` in `HandleConnectionLost()`; set `false` in `SetSyncEnabled(true)`, `ResetForDisable()`, and `UpdateConnectionState(PlcConnectionState.Connected)`.
- [ ] Change `HandleConnectionLost()` to set `_connectionLost = true` instead of `Status = Disconnected`.
- [ ] Rewrite the loss branch in `PublishSnapshot`: emit `Fail("PLC connection lost")` when `_connectionLost && isSyncEnabled` (read under lock with the other fields); keep the `Failed` branch unchanged.
- [ ] Remove `Disconnected` from `PlcSyncStatus`; set `PlcSessionSnapshot.InitialState` `SyncStatus` to `Idle`.
- [ ] In `PlcSyncExecutor.CheckCanSyncAsync`, keep the `!connection.IsConnected` early abort (`return Result.Fail`) but remove the `setStatus(PlcSyncStatus.Disconnected)` write.
- [ ] `dotnet build SemiStep/SemiStep.slnx` — resolve all references to the removed enum value.

### Task 6 (PR 2): Re-point UI status label to ConnectionState

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [ ] Remove the `PlcSyncStatus.Disconnected` case from `MapSyncStatus`; confirm `ConnectionStatus` (line ~99) still renders the connection label independently.
- [ ] Verify no other UI/XAML binding depends on a `Disconnected` sync-status string.
- [ ] `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`.

### Task 7 (PR 2): Update and extend tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`
- Modify: affected tests under `SemiStep/SemiStep.Tests` referencing `PlcSyncStatus.Disconnected`

- [ ] Update any test asserting `PlcSyncStatus.Disconnected` to the new model (connectivity via `ConnectionState`; loss via the failed snapshot).
- [ ] Re-run the PR 1 regression test against the new representation; adjust if it referenced the enum value.
- [ ] Add a test that `HandleConnectionLost` then `SetSyncEnabled(true)` clears `_connectionLost` (no lingering failure on re-enable).
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.

### Task 8 (PR 2): Format, commit, push, open stacked PR

- [ ] `dotnet format SemiStep/SemiStep.slnx`.
- [ ] Commit on `plc-sync-status-refactor` (message: `refactor: make ConnectionState the single source of truth for PLC connectivity`).
- [ ] `git push -u origin plc-sync-status-refactor`; `gh pr create --base plc-sync-reenable-fix` (stacked) with a description noting it depends on PR 1.

### Task 9: Verify acceptance criteria

- [ ] Re-read Overview: spurious failure gone (PR 1), `PlcSyncStatus.Disconnected` removed and loss explicit (PR 2).
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green on the PR 2 branch.
- [ ] Confirm out-of-scope items (P3/P4/P5) and preserved subsystems (S7Service FSM, file-lock ownership, TransportSerializer, threading model) were not modified: `git diff origin/master...HEAD --stat`.

### Task 10: [Final] Plan housekeeping

- [ ] Mark all checkboxes; note any deviations.
- [ ] Move this plan to `Docs/plans/completed/` (commit on the PR 2 branch).

## Post-Completion

*Manual / external — no checkboxes.*

**Manual verification:**
- Re-run the original scenario against the real PLC (192.168.0.150): Sync On → Off → On must reconnect with no `PLC connection lost`.
- Confirm a genuine link drop (disconnect cable / power) still surfaces the failure and auto-reconnect resumes.

**Deferred (separate future PR, gated):**
- Capture the real `InvalidOperationException: Result is in status failed. Value is not set` stack trace (first-chance on `InvalidOperationException`) before executing P3 (drop the `Result` wrapper / move faults to a discrete channel with Core-owned text routed to `MessagePanel`).
