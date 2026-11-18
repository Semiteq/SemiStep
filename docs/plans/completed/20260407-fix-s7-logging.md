# Plan: Fix S7 Module Logging Broken by Result Refactor

## Overview

Commit `14368b2` refactored the S7 module to replace exception-based control flow with
`Result`/`Result<T>` propagation. In doing so it introduced four categories of logging defects:
the execution-monitor poll loop now terminates permanently on any transient non-connection error
(silencing all PLC execution-state output); a sync-coordinator error path logs nothing; and
exception catch sites throughout `PlcTransactionExecutor` swallow exceptions into bare string
messages with no log call, losing stack traces entirely.

## Solution Overview

- **Poll loop:** Remove the unconditional `return` on non-`NotConnectedError` failure in
  `PlcExecutionMonitor`. Log the warning and continue the loop, matching the prior behaviour.
- **Sync coordinator:** Add the missing `Log.Warning` for the generic (non-disconnected) error
  branch in `PlcSyncCoordinator.ExecuteSyncAsync`.
- **Executor catch sites:** Add `Log.Warning(ex, "...")` (with the exception object as first
  argument, per AGENTS.md) at every `catch` block in `PlcTransactionExecutor` that currently
  only returns `Result.Fail(ex.Message)`.
- **`S7Service` error guard:** Guard `result.Errors[0]` accesses with `result.Errors.Count > 0`
  to avoid `ArgumentOutOfRangeException` when a failed result carries no typed errors.

All changes are inside the `S7` project; no public contracts change.

## Affected Files

### Modified Files

| File                                         | Change                                                           |
| -------------------------------------------- | ---------------------------------------------------------------- |
| `SemiStep/S7/Sync/PlcExecutionMonitor.cs`    | Continue poll loop on non-connection errors instead of returning |
| `SemiStep/S7/Sync/PlcSyncCoordinator.cs`     | Add missing `Log.Warning` for generic `activeResult` failure     |
| `SemiStep/S7/Sync/PlcTransactionExecutor.cs` | Add `Log.Warning(ex, ...)` at all exception catch sites          |
| `SemiStep/S7/Facade/S7Service.cs`            | Guard `result.Errors[0]` with count check                        |

## Tasks

### Task 1: Fix PlcExecutionMonitor — poll loop must not exit on transient errors

**Files:**

- Modify: `SemiStep/S7/Sync/PlcExecutionMonitor.cs`

- [x] In `PollLoopAsync`, when `result.IsFailed` and the error is **not** `NotConnectedError`:
      log `Log.Warning(...)` and `continue` the loop (not `return`).
- [x] The `NotConnectedError` branch must still `return` (stop the loop and call `onConnectionLost`).

### Task 2: Fix PlcSyncCoordinator — log missing for generic activeResult failure

**Files:**

- Modify: `SemiStep/S7/Sync/PlcSyncCoordinator.cs`

- [x] In `ExecuteSyncAsync`, in the `activeResult.IsFailed` block, add
      `Log.Warning("Sync blocked: {Message}", activeResult.Errors[0].Message)` (or equivalent)
      for the non-disconnected branch.

### Task 3: Fix PlcTransactionExecutor — log exceptions at catch sites

**Files:**

- Modify: `SemiStep/S7/Sync/PlcTransactionExecutor.cs`

- [x] `ReadRecipeDataAsync` catch block: add `Log.Warning(ex, "Failed to read recipe data from PLC")`.
- [x] `WriteRecipeDataAsync` catch block: add `Log.Warning(ex, "Failed to write recipe data to PLC")`.
- [x] `WriteManagingAreaAsync` catch block: add `Log.Warning(ex, "Failed to write managing area to PLC")`.
- [x] `ReadAndDecodeAsync` catch block: add `Log.Warning(ex, "Failed to read and decode PLC data")`.

### Task 4: Fix S7Service — guard Errors[0] access

**Files:**

- Modify: `SemiStep/S7/Facade/S7Service.cs`

- [x] In `ReadManagingAreaAsync`, replace bare `result.Errors[0].Message` with a safe access
      pattern (e.g. `result.Errors.FirstOrDefault()?.Message ?? "Unknown error"`).
- [x] In `ReadRecipeFromPlcAsync`, same guard.

### Task 5: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/SemiStep.slnx`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj --filter "Component=S7"`
- [x] All pass
