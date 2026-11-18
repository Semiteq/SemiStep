# Plan: Strip CancellationToken from Public API

## Overview

`CancellationToken` parameters exist throughout the public API surface (`ICsvService`,
`IS7Service`, `IS7Driver`, `DomainFacade`) but are never supplied with a real token by any
caller. They are decorative and create a false contract. Additionally, their presence produced
several unguarded `OperationCanceledException` escape paths and one silent-swallow bug that
were the subject of issue #4. Removing the tokens from the public boundary resolves issue #4
completely without any new handling logic, and makes the API honest about its actual capabilities.

The S7 internal layer (`PlcTransactionExecutor`, `PlcSyncExecutor`, `PlcExecutionMonitor`,
`KeepAliveLoopAsync`, `ReconnectLoopAsync`) owns real cancellation tokens for loop control and
debounce — those are entirely untouched.

## Solution Overview

- Remove `CancellationToken` parameters from every public/internal interface and its
  implementations, top-down: `ICsvService` → `IS7Service` → `IS7Driver` → `DomainFacade` →
  `PlcLifecycleManager` → test stubs.
- `S7Service`'s private `ConnectInternalAsync(CancellationToken ct)` keeps its parameter
  because it is called by `ReconnectLoopAsync` with the reconnect loop's own token.
  The public `ConnectAsync` will call it with `CancellationToken.None`.
- Similarly, `S7Service`'s four public methods forward to `PlcTransactionExecutor` (which
  retains its internal tokens); they pass `CancellationToken.None` explicitly at the forwarding
  call site, making intent clear.
- The now-dead `catch (OperationCanceledException) { throw; }` in `DomainFacade.SaveRecipeAsync`
  is removed. The `catch (Exception ex) when (ex is not OperationCanceledException)` guard in
  `S7Service.ConnectInternalAsync` becomes a plain `catch (Exception ex)`.
- `IS7Transport` (`ReadBytesAsync`, `WriteBytesAsync`), `PlcTransactionExecutor`, and all
  S7 sync/monitor internals are out of scope and not modified.

**Alternative considered and rejected:** Keeping the tokens and adding `Result.Fail("Operation cancelled")`
catch clauses at every boundary. Rejected because it preserves dead parameters and adds noise
for a capability that is not exercised anywhere in the application today.

## Affected Files

### Modified Files

| File | Change |
| ---- | ------ |
| `SemiStep/TypesShared/Domain/ICsvService.cs` | Remove `CancellationToken` from `LoadAsync` and `SaveAsync` |
| `SemiStep/Csv/CsvService.cs` | Remove `CancellationToken` from `LoadAsync` and `SaveAsync`; update call sites |
| `SemiStep/Csv/CsvFileIo.cs` | Remove `CancellationToken` from `ReadRecipeFileAsync` and `WriteRecipeFileAsync`; update call sites |
| `SemiStep/TypesShared/Domain/IS7Service.cs` | Remove `CancellationToken` from all four methods |
| `SemiStep/S7/Facade/S7Service.cs` | Remove `CancellationToken` from public methods; pass `CancellationToken.None` to internal/executor calls; clean up `when (ex is not OperationCanceledException)` guard |
| `SemiStep/S7/IS7Driver.cs` | Remove `CancellationToken` from `ConnectAsync` and `DisconnectAsync` |
| `SemiStep/S7/S7Driver.cs` | Remove `CancellationToken` from `ConnectAsync` and `DisconnectAsync`; update `_plc.OpenAsync()` call |
| `SemiStep/Domain/Facade/DomainFacade.cs` | Remove `CancellationToken` from `LoadRecipeAsync`, `SaveRecipeAsync`, `LoadRecipeFromPlcAsync`; remove dead OCE catch block in `SaveRecipeAsync` |
| `SemiStep/Domain/Facade/PlcLifecycleManager.cs` | Remove `CancellationToken` from private `PerformReconnectReconciliationAsync`; update call sites |
| `SemiStep/Tests/Helpers/StubIs7Service.cs` | Remove `CancellationToken` from all four `IS7Service` methods |
| `SemiStep/Tests/Helpers/StubCsvService.cs` | Remove `CancellationToken` from `LoadAsync` and `SaveAsync` |
| `SemiStep/Tests/Helpers/FailingCsvService.cs` | Remove `CancellationToken` from `LoadAsync` and `SaveAsync` |
| `SemiStep/Tests/S7/Helpers/StubIs7ServiceForSync.cs` | Remove `CancellationToken` from all four `IS7Service` methods |
| `SemiStep/Tests/S7/Helpers/FakeS7Driver.cs` | Remove `CancellationToken` from `ConnectAsync` and `DisconnectAsync` |

## Tasks

### Task 1: Strip CancellationToken from CSV layer

**Files:**

- Modify: `SemiStep/TypesShared/Domain/ICsvService.cs`
- Modify: `SemiStep/Csv/CsvService.cs`
- Modify: `SemiStep/Csv/CsvFileIo.cs`

- [x] `ICsvService.LoadAsync`: remove `CancellationToken cancellationToken = default` parameter
- [x] `ICsvService.SaveAsync`: remove `CancellationToken cancellationToken = default` parameter
- [x] `CsvService.LoadAsync`: remove parameter; drop `cancellationToken` arg in `CsvFileIo.ReadRecipeFileAsync` call
- [x] `CsvService.SaveAsync`: remove parameter; drop `cancellationToken` arg in `CsvFileIo.WriteRecipeFileAsync` call
- [x] `CsvFileIo.ReadRecipeFileAsync`: remove parameter; change `File.ReadAllTextAsync(filePath, _fileEncoding, cancellationToken)` to `File.ReadAllTextAsync(filePath, _fileEncoding)`
- [x] `CsvFileIo.WriteRecipeFileAsync`: remove parameter; change `writer.WriteAsync(csvBody.AsMemory(), cancellationToken)` to `writer.WriteAsync(csvBody.AsMemory())`

### Task 2: Strip CancellationToken from S7 driver layer

**Files:**

- Modify: `SemiStep/S7/IS7Driver.cs`
- Modify: `SemiStep/S7/S7Driver.cs`

- [x] `IS7Driver.ConnectAsync`: remove `CancellationToken ct = default` parameter
- [x] `IS7Driver.DisconnectAsync`: remove `CancellationToken ct = default` parameter
- [x] `S7Driver.ConnectAsync`: remove parameter; change `await _plc.OpenAsync(ct)` to `await _plc.OpenAsync()`
- [x] `S7Driver.DisconnectAsync`: remove parameter (body is synchronous `_plc?.Close()`, no other changes)

### Task 3: Strip CancellationToken from IS7Service interface and S7Service

**Files:**

- Modify: `SemiStep/TypesShared/Domain/IS7Service.cs`
- Modify: `SemiStep/S7/Facade/S7Service.cs`

- [x] `IS7Service.ConnectAsync`: remove `CancellationToken ct = default` parameter
- [x] `IS7Service.DisconnectAsync`: remove `CancellationToken ct = default` parameter
- [x] `IS7Service.ReadManagingAreaAsync`: remove `CancellationToken ct = default` parameter
- [x] `IS7Service.ReadRecipeFromPlcAsync`: remove `CancellationToken ct = default` parameter
- [x] `S7Service.ConnectAsync`: remove parameter; call `ConnectInternalAsync(CancellationToken.None)`
- [x] `S7Service.DisconnectAsync`: remove parameter; call `transport.DisconnectAsync(CancellationToken.None)` (the IS7Driver.DisconnectAsync no-CT overload used after Task 2)
- [x] `S7Service.ReadManagingAreaAsync`: remove parameter; call `transactionExecutor.ReadManagingAreaAsync(CancellationToken.None)`
- [x] `S7Service.ReadRecipeFromPlcAsync`: remove parameter; call `transactionExecutor.ReadRecipeFromPlcAsync(CancellationToken.None)`
- [x] `S7Service.ConnectInternalAsync` (private): **keep signature unchanged** — still `private async Task ConnectInternalAsync(CancellationToken ct)`
- [x] `S7Service.ConnectInternalAsync`: change `catch (Exception ex) when (ex is not OperationCanceledException)` to plain `catch (Exception ex)` — guard is no longer meaningful since the only external entry point passes `CancellationToken.None` (internal reconnect loop still uses its own token, but that exception will propagate naturally out of `ReconnectLoopAsync` where it is already caught)

### Task 4: Strip CancellationToken from DomainFacade

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] `LoadRecipeAsync`: remove `CancellationToken ct = default` parameter; drop `ct` from `_csvService.LoadAsync` call
- [x] `SaveRecipeAsync`: remove `CancellationToken ct = default` parameter; drop `ct` from `_csvService.SaveAsync` call; remove the dead `catch (OperationCanceledException) { throw; }` clause entirely
- [x] `LoadRecipeFromPlcAsync`: remove `CancellationToken ct = default` parameter; drop `ct` from `_connectionService.ReadRecipeFromPlcAsync` call

### Task 5: Strip CancellationToken from PlcLifecycleManager

**Files:**

- Modify: `SemiStep/Domain/Facade/PlcLifecycleManager.cs`

- [x] `PerformReconnectReconciliationAsync` (private): remove `CancellationToken ct = default` parameter
- [x] Update call to `connectionService.ReadManagingAreaAsync(ct)` → `connectionService.ReadManagingAreaAsync()`
- [x] Update call to `connectionService.ReadRecipeFromPlcAsync(ct)` → `connectionService.ReadRecipeFromPlcAsync()`

### Task 6: Update test stubs and fakes

**Files:**

- Modify: `SemiStep/Tests/Helpers/StubIs7Service.cs`
- Modify: `SemiStep/Tests/Helpers/StubCsvService.cs`
- Modify: `SemiStep/Tests/Helpers/FailingCsvService.cs`
- Modify: `SemiStep/Tests/S7/Helpers/StubIs7ServiceForSync.cs`
- Modify: `SemiStep/Tests/S7/Helpers/FakeS7Driver.cs`

- [x] `StubIs7Service`: remove `CancellationToken` from `ConnectAsync`, `DisconnectAsync`, `ReadManagingAreaAsync`, `ReadRecipeFromPlcAsync`
- [x] `StubCsvService`: remove `CancellationToken` from `LoadAsync` and `SaveAsync`
- [x] `FailingCsvService`: remove `CancellationToken` from `LoadAsync` and `SaveAsync`
- [x] `StubIs7ServiceForSync`: remove `CancellationToken` from `ConnectAsync`, `DisconnectAsync`, `ReadManagingAreaAsync`, `ReadRecipeFromPlcAsync`
- [x] `FakeS7Driver`: remove `CancellationToken` from `ConnectAsync` and `DisconnectAsync`

### Task 7: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass
