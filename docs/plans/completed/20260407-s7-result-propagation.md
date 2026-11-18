# Plan: S7 Module — Result Propagation and Logging Refactor

## Overview

The S7 module uses exceptions as internal control flow for business-logic failures, causing full
stack traces to appear in Warning-level log output and double-logging at two catch levels. This
refactor replaces all internal throw sites with `Result`/`Result<T>` propagation consistent with
every other module in the codebase, rationalises logging so stack traces appear only at Error
level, and fixes a latent `OperationCanceledException` crash risk in two `S7Service` methods.

## Solution Overview

- **Typed error class** (`NotConnectedError : Error`, internal to S7) introduced so
  `PlcExecutionMonitor` can distinguish "not connected" from other poll failures without string
  comparison. Kept internal — no `TypesShared` change needed.
- **`RecipeConverter`** methods changed to return `Result<PlcRecipeData>` and `Result<Recipe>`.
  All six `throw` sites become `Result.Fail(...)`. The detailed step/column messages are
  preserved as failure reasons.
- **`PlcTransactionExecutor`** converts all public methods to `Result`/`Result<T>`. `EnsureConnected()`
  becomes a bool-returning helper; callers wrap its false return in `Result.Fail<NotConnectedError>`.
  The internal `try/catch` + `Log.Warning(ex,...)` in `ReadRecipeFromPlcAsync` is removed — the
  converter already returns a Result.
- **`S7Service`** facade: the `try/catch` blocks in `ReadManagingAreaAsync` and
  `ReadRecipeFromPlcAsync` become Result propagation (no catch). `OperationCanceledException`
  guards added to both. WRN log calls changed to message-only; ERR on connect failure stays.
  Keep-alive and reconnect WRN calls also changed to message-only.
- **`PlcSyncCoordinator`**: explicit `catch (PlcNotConnectedException)` removed; generic
  `catch (Exception)` removed; both replaced with Result failure checks after each awaited call.
- **`PlcExecutionMonitor`**: `catch (PlcNotConnectedException)` removed; poll loop checks
  `Result.IsFailed` and uses `NotConnectedError` type to trigger `onConnectionLost`.
  `Log.Warning(ex,...)` on unexpected poll errors changed to message-only.
- **Deleted**: `PlcNotConnectedException.cs`, `PlcWriteVerificationException.cs` — both become
  dead code.
- **`OperationCanceledException`**: silently swallowed everywhere, consistent with the 9 other
  occurrences in the codebase. Never wrapped in `Result.Fail`.

## Affected Files

### New Files

| File                                        | Purpose                                                           |
| ------------------------------------------- | ----------------------------------------------------------------- |
| `SemiStep/S7/Protocol/NotConnectedError.cs` | Typed FluentResults `Error` subclass for "not connected" failures |

### Modified Files

| File                                           | Change                                                                                             |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `SemiStep/S7/Serialization/RecipeConverter.cs` | `ToRecipe` → `Result<Recipe>`, `FromRecipe` → `Result<PlcRecipeData>`; throw sites → `Result.Fail` |
| `SemiStep/S7/Sync/PlcTransactionExecutor.cs`   | All public methods → `Result`/`Result<T>`; `EnsureConnected` → bool; remove internal log           |
| `SemiStep/S7/Facade/S7Service.cs`              | Remove try/catch in two read methods; add OCE guards; WRN → message-only                           |
| `SemiStep/S7/Sync/PlcSyncCoordinator.cs`       | Remove exception catches; add Result failure checks                                                |
| `SemiStep/S7/Sync/PlcExecutionMonitor.cs`      | Remove exception catch; add Result failure check with `NotConnectedError`                          |

### Deleted Files

| File                                                | Reason                                                 |
| --------------------------------------------------- | ------------------------------------------------------ |
| `SemiStep/S7/Protocol/PlcNotConnectedException.cs`  | Replaced by `NotConnectedError` and Result propagation |
| `SemiStep/S7/Sync/PlcWriteVerificationException.cs` | Never caught by type; write failure now a Result       |

## Tasks

### Task 1: Add NotConnectedError

**Files:**

- Create: `SemiStep/S7/Protocol/NotConnectedError.cs`

- [x] Create `internal sealed class NotConnectedError : Error` in `S7.Protocol` namespace
- [x] Constructor accepts a message string, passes it to `base(message)`

---

### Task 2: Refactor RecipeConverter

**Files:**

- Modify: `SemiStep/S7/Serialization/RecipeConverter.cs`

- [x] Change `FromRecipe(Recipe recipe)` signature to `Result<PlcRecipeData>`
- [x] Change `ToRecipe(PlcRecipeData data)` signature to `Result<Recipe>`
- [x] Change `ResolveAction` to return `Result<ActionDefinition>` (or inline the check); remove throw
- [x] Change `ResolvePropertyType` to return `Result<PropertyType>` (or inline the check); remove throw
- [x] In `DeserialiseStep`: replace throw with `Result.Fail(...)` propagated up to `ToRecipe`
- [x] In `DeserialiseProperty` (all 3 branches + default): replace throws with `Result.Fail(...)`
- [x] In `SerialiseStep` / `SerialiseProperty`: return `Result` and propagate resolver failures
- [x] All diagnostic messages (step index, column key) preserved verbatim in failure reasons

---

### Task 3: Refactor PlcTransactionExecutor

**Files:**

- Modify: `SemiStep/S7/Sync/PlcTransactionExecutor.cs`

- [x] Change `EnsureConnected()` from `void` (throws) to `bool` (returns false when not connected)
- [x] Change `IsRecipeActiveAsync` to `Task<Result<bool>>`; fail with `NotConnectedError` when not connected
- [x] Change `ReadExecutionStateAsync` to `Task<Result<PlcExecutionState>>`; same pattern
- [x] Change `ReadRecipeDataAsync` to `Task<Result<PlcRecipeData>>`; same pattern
- [x] Change `ReadManagingAreaAsync` to `Task<Result<ManagingAreaState>>`; same pattern
- [x] Change `WriteRecipeWithRetryAsync` to `Task<Result>`; check `FromRecipe` result; fail with `NotConnectedError` or `Result.Fail` on verification exhaustion
- [x] Remove the `try/catch` block wrapping `_converter.ToRecipe` in `ReadRecipeFromPlcAsync` — converter now returns `Result`; propagate directly
- [x] Remove `Log.Warning(ex, ...)` call inside `ReadRecipeFromPlcAsync` — logging moves to S7Service boundary
- [x] All transport exceptions from `_transport.ReadBytesAsync`/`WriteBytesAsync` propagate as-is (S7Service will catch them)
- [x] `OperationCanceledException` from transport calls: swallow silently (empty catch or `when` guard)

---

### Task 4: Refactor S7Service

**Files:**

- Modify: `SemiStep/S7/Facade/S7Service.cs`

- [x] `ReadManagingAreaAsync`: remove `try/catch`; propagate `Result` from `transactionExecutor.ReadManagingAreaAsync` directly; add `when (ex is not OperationCanceledException)` guard if any catch remains; log `WRN` as message-only on failure
- [x] `ReadRecipeFromPlcAsync`: same — remove `try/catch`, propagate Result, WRN message-only
- [x] `KeepAliveLoopAsync`: change `Log.Warning(ex, ...)` to `Log.Warning("Keep-alive probe failed: {Message}", ex.Message)`
- [x] `ReconnectLoopAsync`: change `Log.Warning(ex, ...)` to `Log.Warning("Reconnection attempt failed, retrying in {Delay}s: {Message}", delay.TotalSeconds, ex.Message)`
- [x] `ConnectInternalAsync`: keep `Log.Error(ex, ...)` unchanged — stack trace appropriate for connection failure
- [x] Verify `OperationCanceledException` is never wrapped in a `Result.Fail` or re-thrown from catch blocks

---

### Task 5: Refactor PlcSyncCoordinator

**Files:**

- Modify: `SemiStep/S7/Sync/PlcSyncCoordinator.cs`

- [x] In `ExecuteSyncAsync`: replace `catch (PlcNotConnectedException)` with `Result.IsFailed` check on `IsRecipeActiveAsync` result; map `NotConnectedError` to `PlcSyncStatus.Failed` and `LastError = "Not connected to PLC"`
- [x] Replace generic `catch (Exception ex) when (ex is not OperationCanceledException)` with `Result.IsFailed` check on `WriteRecipeWithRetryAsync` result; map failure to `PlcSyncStatus.Failed`, `LastError = result.Errors[0].Message`; log `Log.Error("Sync failed: {Message}", ...)` for unexpected errors only (not `NotConnectedError`)
- [x] `OperationCanceledException` from `Task.Delay` or transport: keep existing `catch (OperationCanceledException) { }` swallow

---

### Task 6: Refactor PlcExecutionMonitor

**Files:**

- Modify: `SemiStep/S7/Sync/PlcExecutionMonitor.cs`

- [x] In `PollLoopAsync`: replace `catch (PlcNotConnectedException)` with `result.IsFailed` check after `ReadExecutionStateAsync`
- [x] If failure contains `NotConnectedError`: log `Log.Debug(...)` and invoke `onConnectionLost()` (same logic as before)
- [x] If failure is any other error: log `Log.Warning("Execution monitor poll error: {Message}", result.Errors[0].Message)` — message only, no exception object
- [x] Remove the existing `catch (Exception ex)` block that logs `Log.Warning(ex, ...)`

---

### Task 7: Delete obsolete exception classes

**Files:**

- Delete: `SemiStep/S7/Protocol/PlcNotConnectedException.cs`
- Delete: `SemiStep/S7/Sync/PlcWriteVerificationException.cs`

- [x] Update `PlcTransactionExecutorTests.cs`: rewrite 3 affected tests to assert on `Result.IsFailed` instead of thrown exceptions
- [x] Verify no remaining references to `PlcNotConnectedException` in the codebase
- [x] Verify no remaining references to `PlcWriteVerificationException` in the codebase
- [x] Delete both files

---

### Task 8: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All 272 tests pass, 0 build errors
