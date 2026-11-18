# Plan: Unified State-Driven Error Panel

## Overview

Error and warning display in the message panel currently has two disconnected flows: recipe
validation results go through `RefreshReasons`, while PLC connection and sync events go through
ad-hoc `AddError`/`AddInfo` calls with manual pre-capture of mutable state before dispatcher
posts. This creates ordering rules that are documented in `AGENTS.md` as workarounds rather
than resolved at the design level. The goal is to unify both flows: every module that produces
state exposes that state as a `Result<TSnapshot>` with reasons, and the coordinator rebuilds
the message panel from all module states on any state change.

## Solution Overview

**PlcSessionSnapshot DTO + Result-based PLC state:**
Introduce `PlcSessionSnapshot` in `TypesShared/Plc/` — an immutable record with connection and
sync status fields. `DomainFacade` aggregates `S7Service.StateChanged` and
`PlcSyncCoordinator.StatusChanged` into a single `PlcStateChanged` event carrying
`Result<PlcSessionSnapshot>`. Errors (disconnected while sync enabled, sync failed) are encoded
as `IError` reasons on the `Result`. This mirrors exactly how `ICoreService` returns
`Result<RecipeSnapshot>` with reasons.

**Single rebuild path in RecipeMutationCoordinator:**
`RecipeMutationCoordinator` replaces the two separate handlers (`OnConnectionStateChanged`,
`OnSyncStatusChanged`) and `_syncErrorChangedRelay` with a single `OnPlcStateChanged` handler.
`RefreshMessagePanel` is extended to accept both recipe and PLC reasons together and call
`messagePanel.RefreshReasons(combinedReasons)`. On any state change from either source the
panel is fully rebuilt from current state.

**Removed `ErrorChanged` event and `LastError` property from `IPlcSyncService`:**
`ErrorChanged` exists solely to propagate the sync error string when `LastError` changes
independently of `Status`. With the snapshot model, both fields are captured atomically in
`PublishPlcSnapshot()` inside the lock, so `ErrorChanged` and `LastError` have no remaining
purpose on the public interface.

**Removed `PlcSyncErrorText` duplication:**
`PlcSyncErrorText` in `MainWindowViewModel` and its binding in `AppStatusBar.axaml` are
removed. The sync error message now appears exclusively in the message panel as a standard
`IError` reason, consistent with recipe validation errors.

**Removed `_syncErrorChangedRelay` workaround:**
The field and its subscription exist only to bridge `ErrorChanged` into
`NotifyConnectionStateChanged`. Both disappear along with `ErrorChanged`.

**`ConnectionStateChanged: IObservable<Unit>` replaced:**
The coordinator exposes `PlcStateChanged: IObservable<Result<PlcSessionSnapshot>>` in place of
the opaque `Subject<Unit>`. `MainWindowViewModel` subscribes to it and raises connection state
properties from the snapshot directly, without re-pulling from `RecipeQueryService`.

## Affected Files

### New Files

| File                                             | Purpose                                              |
| ------------------------------------------------ | ---------------------------------------------------- |
| `SemiStep/TypesShared/Plc/PlcSessionSnapshot.cs` | Immutable record DTO for PLC connection + sync state |

### Modified Files

| File                                                   | Change                                                                                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SemiStep/TypesShared/Domain/IPlcSyncService.cs`       | Remove `LastError` property and `ErrorChanged` event                                                                                                                                                                                                                                                                                          |
| `SemiStep/S7/Sync/PlcSyncCoordinator.cs`               | Remove `LastError` field/setter/`ErrorChanged` firing; add `PublishSnapshot()` called from `Status` setter and all `LastError` assignment sites; expose `IObservable<Result<PlcSessionSnapshot>> PlcState`                                                                                                                                    |
| `SemiStep/Domain/Facade/DomainFacade.cs`               | Subscribe to both `S7Service.StateChanged` and `PlcSyncCoordinator.PlcState`; assemble `Result<PlcSessionSnapshot>` in `PublishPlcSnapshot()`; expose `IObservable<Result<PlcSessionSnapshot>> PlcState`; remove `SyncLastError` and `ConnectionStateChanged` event; keep remaining delegated properties                                      |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | Remove `_connectionStateChanged Subject<Unit>`, `_syncErrorChangedRelay`, `OnConnectionStateChanged`, `OnSyncStatusChanged`; add `OnPlcStateChanged(Result<PlcSessionSnapshot>)`; update `RefreshMessagePanel` to accept combined reasons from both recipe and PLC results; expose `PlcStateChanged: IObservable<Result<PlcSessionSnapshot>>` |
| `SemiStep/UI/Coordinator/RecipeQueryService.cs`        | Remove `SyncLastError` delegated property                                                                                                                                                                                                                                                                                                     |
| `SemiStep/UI/MainWindow/MainWindowViewModel.cs`        | Replace `ConnectionStateChanged` subscription with `PlcStateChanged`; raise connection properties from snapshot; remove `PlcSyncErrorText` property and `RaiseConnectionStateProperties` call for it                                                                                                                                          |
| `SemiStep/UI/MainWindow/AppStatusBar.axaml`            | Remove `PlcSyncErrorText` `TextBlock` element                                                                                                                                                                                                                                                                                                 |

### Deleted Files

_(none)_

## Tasks

### Task 1: Add PlcSessionSnapshot DTO

**Files:**

- Create: `SemiStep/TypesShared/Plc/PlcSessionSnapshot.cs`

- [x] Define `public sealed record PlcSessionSnapshot(PlcConnectionState ConnectionState, PlcSyncStatus SyncStatus, bool IsSyncEnabled)` in namespace `TypesShared.Plc`
- [x] No logic in the record — pure data

---

### Task 2: Remove ErrorChanged and LastError from IPlcSyncService

**Files:**

- Modify: `SemiStep/TypesShared/Domain/IPlcSyncService.cs`

- [x] Remove `string? LastError { get; }` property
- [x] Remove `event Action<string?>? ErrorChanged`
- [x] Add `IObservable<Result<PlcSessionSnapshot>> PlcState { get; }` (requires `using FluentResults` and `using TypesShared.Plc`)

---

### Task 3: Refactor PlcSyncCoordinator — snapshot publication

**Files:**

- Modify: `SemiStep/S7/Sync/PlcSyncCoordinator.cs`

- [x] Add `BehaviorSubject<Result<PlcSessionSnapshot>> _subject` field (initial value: `Result.Ok(new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Disconnected, false))`)
- [x] Expose `public IObservable<Result<PlcSessionSnapshot>> PlcState => _subject;` (implements interface)
- [x] Add private `PublishSnapshot(PlcConnectionState connectionState)` method: assembles `PlcSessionSnapshot` from `connectionState`, current `_status`, current `_isSyncEnabled` (new field — see below); determines if result is failure based on `_status` (Failed, Disconnected when sync enabled) and emits accordingly
- [x] Add `_isSyncEnabled` field (bool); add `SetSyncEnabled(bool value)` method called by `DomainFacade` when enabling/disabling sync (replaces the need for `DomainFacade` to own this entirely — the coordinator needs it to determine error semantics)
- [x] Remove `_lastError` field, its property, setter logic, and all `LastError = ...` assignments; replace them with equivalent `_errorMessage` local variable captures used only in `PublishSnapshot()` error reasons
- [x] Remove `ErrorChanged` event declaration and all `ErrorChanged?.Invoke(...)` calls
- [x] In `Status` setter: after updating `_status`, call `PublishSnapshot(lastKnownConnectionState)` — requires storing last known connection state (add `_connectionState` field updated via new `UpdateConnectionState(PlcConnectionState)` method called by DomainFacade)
- [x] In `Reset()`: call `PublishSnapshot` with `Disconnected` state
- [x] In all `ExecuteSyncAsync` error paths where `LastError` was set: instead pass the error message into `PublishSnapshot` via a new overload or parameter
- [x] Dispose `_subject` in `Dispose()` if `PlcSyncCoordinator` implements `IDisposable` (check and add if needed)

> **Note on error semantics in PublishSnapshot:**
>
> - `PlcSyncStatus.Failed` → `Result.Fail(new Error(errorMessage ?? "Sync failed"))`
> - `PlcSyncStatus.Disconnected` when `_isSyncEnabled` → `Result.Fail(new Error("PLC connection lost"))`
> - All other statuses → `Result.Ok(snapshot)`

---

### Task 4: Refactor DomainFacade — aggregate PLC state

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] Remove `_connectionStateChangedRelay` field (line 30) and its subscription/unsubscription
- [x] Remove `public event Action<PlcConnectionState>? ConnectionStateChanged` (line 338)
- [x] Remove `public string? SyncLastError => _syncService.LastError;` (line 77)
- [x] Add subscription to `_syncService.PlcState` in `Initialize()`: on each emission, re-emit via `_plcStateSubject`
- [x] Add subscription to `_connectionService.StateChanged` in `Initialize()`: call `_syncService.UpdateConnectionState(state)` then re-emit current snapshot from `_syncService.PlcState` (or trigger re-publish directly)
- [x] Add `BehaviorSubject<Result<PlcSessionSnapshot>>` field `_plcStateSubject`; expose as `public IObservable<Result<PlcSessionSnapshot>> PlcState => _plcStateSubject`
- [x] In `EnableSync`: call `_syncService.SetSyncEnabled(true)` before connecting; call `_syncService.SetSyncEnabled(false)` on failure path
- [x] In `DisableSync`: call `_syncService.SetSyncEnabled(false)` before reset
- [x] Dispose `_plcStateSubject` in `Dispose()`
- [x] Keep remaining delegated properties: `IsConnected`, `IsRecipeActive`, `ExecutionState`, `SyncStatus`, `IsSyncEnabled`, `LastSyncTime`

> **Design note:** `DomainFacade` does not build the `Result<PlcSessionSnapshot>` itself — it forwards the observable from `PlcSyncCoordinator`. The coordinator owns snapshot assembly because it owns the sync state fields. `DomainFacade` bridges the connection state into `PlcSyncCoordinator` so the coordinator can include it in the snapshot.

---

### Task 5: Refactor RecipeMutationCoordinator — unified panel rebuild

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] Remove `_connectionStateChanged Subject<Unit>` field (line 29) and its `ConnectionStateChanged` property (line 35)
- [x] Remove `_syncErrorChangedRelay` field (line 30) and its assignment and subscription (lines 67–68) and unsubscription
- [x] Remove subscription to `syncService.StatusChanged` (line 65) and `syncService.ErrorChanged` (line 68) from `Initialize()`
- [x] Remove subscription to `domainFacade.ConnectionStateChanged` (line 64) from `Initialize()`
- [x] Remove `OnConnectionStateChanged` method (lines 308–330)
- [x] Remove `OnSyncStatusChanged` method (lines 332–350)
- [x] Add `_plcStateChanged Subject<Result<PlcSessionSnapshot>>` field; expose as `public IObservable<Result<PlcSessionSnapshot>> PlcStateChanged => _plcStateChanged`
- [x] Add subscription to `domainFacade.PlcState` in `Initialize()`: `.ObserveOn(RxApp.MainThreadScheduler).Subscribe(OnPlcStateChanged)`
- [x] Implement `OnPlcStateChanged(Result<PlcSessionSnapshot> result)`: emit `_plcStateChanged.OnNext(result)`, then call `RebuildMessagePanel()`
- [x] Add `_lastPlcState` field (`Result<PlcSessionSnapshot>`) to cache the last received PLC state; update in `OnPlcStateChanged`
- [x] Add `_lastRecipeResult` field (`Result`) to cache the last recipe mutation result; update it in `RefreshMessagePanel(Result)`
- [x] Rename `RefreshMessagePanel(Result)` to `RebuildMessagePanel()`: combine reasons from `_lastRecipeResult` and `_lastPlcState`, call `messagePanel.RefreshReasons(combinedReasons)`; on recipe load (`LoadRecipeAsync`, `LoadRecipeFromPlcAsync`, `NewRecipe`) also reset `_lastRecipeResult` to `Result.Ok()` first
- [x] Update all call sites that previously called `RefreshMessagePanel(result)` to first store result in `_lastRecipeResult` then call `RebuildMessagePanel()`
- [x] Remove `Clear()` calls on `messagePanel` from `NewRecipe`, `LoadRecipeAsync`, `LoadRecipeFromPlcAsync` — the unified `RebuildMessagePanel` replaces them (reset `_lastRecipeResult` to `Result.Ok()` instead)
- [x] Remove `messagePanel.AddInfo` / `messagePanel.AddError` calls from `OnConnectionStateChanged` and `OnSyncStatusChanged` (these methods are deleted)
- [x] Remove `EnableSync` `AddError` call (line 104) — failure from `EnableSync` returns a `Result.Fail` which will be picked up when `PlcStateChanged` fires with the error

---

### Task 6: Remove SyncLastError from RecipeQueryService

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeQueryService.cs`

- [x] Remove `public string? SyncLastError => domainFacade.SyncLastError;` (line 29)

---

### Task 7: Update MainWindowViewModel — subscribe to PlcStateChanged

**Files:**

- Modify: `SemiStep/UI/MainWindow/MainWindowViewModel.cs`

- [x] Replace `_coordinator.ConnectionStateChanged` subscription with `_coordinator.PlcStateChanged.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => RaiseConnectionStateProperties())`
- [x] Remove `PlcSyncErrorText` property (line 113)
- [x] Remove `PlcSyncErrorText` from `RaiseConnectionStateProperties()` (line 184)

---

### Task 8: Remove PlcSyncErrorText from AppStatusBar

**Files:**

- Modify: `SemiStep/UI/MainWindow/AppStatusBar.axaml`

- [x] Remove the `TextBlock` element bound to `PlcSyncErrorText` (lines 123–128)

---

### Task 9: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass
