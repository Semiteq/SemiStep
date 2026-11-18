# Plan: Surface PLC Operational Events in the Message Panel

## Overview

PLC connection and sync events are currently invisible to the operator: they go only to the
Serilog file log and to a single overwriting `LastError` string in the status bar. The message
panel already shows recipe and file errors as a timestamped, severity-coded, accumulated feed.
This plan routes PLC operational events into that same panel under a `"PLC"` source label, so
the operator sees connection changes, sync results, and failures without opening a log file.

## Solution Overview

- **Event signature fix:** `DomainFacade.ConnectionStateChanged` is `event Action?` — it fires
  with no argument, forcing callers to read state separately. Changing it to
  `Action<PlcConnectionState>?` lets subscribers react to the direction of the change without
  a race and without coupling to `IS7Service` directly.

- **Panel wiring in the coordinator:** `RecipeMutationCoordinator` already subscribes to every
  relevant event in `Initialize()`. Extending those handlers to call `messagePanel.Add*` keeps
  all panel mutations in one class and requires no new types or DI registrations.

- **`AddWarning` method:** `MessagePanelViewModel` has `AddError` and `AddInfo` but no
  `AddWarning`. Although `OutOfSync` no longer writes to the panel (structural recipe warnings
  already cover it), the method is still missing and should be added for completeness and future
  use.

- **`EnableSync` failure surfaced:** `MainWindowViewModel.ExecuteToggleSyncAsync` discards the
  `Result` from `coordinator.EnableSync()`. The connection failure that goes into
  `DomainFacade.LastConnectionError` never reaches the panel. The coordinator's `EnableSync`
  wrapper must forward a failure result to `messagePanel.AddError`.

- **Branching by scope:** each branch is a self-contained, independently buildable change.
  No branch depends on another at the code level.

## Affected Files

### Modified Files

| File | Change |
|------|--------|
| `SemiStep/Domain/Facade/DomainFacade.cs` | Change `ConnectionStateChanged` event type from `Action?` to `Action<PlcConnectionState>?`; pass state when invoking |
| `SemiStep/TypesShared/Domain/IDomainFacade.cs` (if exists) | Mirror the event type change |
| `SemiStep/UI/MessageService/MessagePanelViewModel.cs` | Add `AddWarning(string message, string source)` method |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | Wire connection and sync events to `messagePanel.Add*`; handle `EnableSync` result |

## Branches

### Branch 1 — `ConnectionStateChanged` signature fix

Scope: `Domain` and any consumer that references the event.

**Goal:** Change `DomainFacade.ConnectionStateChanged` from `event Action?` to
`event Action<PlcConnectionState>?` so subscribers receive the new state without polling.

**Files:**
- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`
- Modify: `SemiStep/TypesShared/Domain/IDomainFacade.cs` (if the event is on the interface)
- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` (update subscription lambda)

- [x] Change `public event Action? ConnectionStateChanged` to
      `public event Action<PlcConnectionState>? ConnectionStateChanged` in `DomainFacade`.
- [x] Update the invocation inside `_connectionStateChangedRelay` to pass `state`:
      `ConnectionStateChanged?.Invoke(state)`.
- [x] Update `RecipeMutationCoordinator._connectionStateChangedRelay` lambda signature from
      `() =>` to `(PlcConnectionState _) =>` (or a named handler) to match.
- [x] If `ConnectionStateChanged` is declared on an interface in `TypesShared`, mirror the type change there.
- [x] Build: `dotnet build SemiStep/SemiStep.slnx` — must pass clean.

### Branch 2 — `AddWarning` on `MessagePanelViewModel`

Scope: `UI` only.

**Goal:** Add the missing `AddWarning(string message, string source)` method, symmetric with
`AddError` and `AddInfo`.

**Files:**
- Modify: `SemiStep/UI/MessageService/MessagePanelViewModel.cs`

- [x] Add `public void AddWarning(string message, string source)` that appends a
      `MessageEntry(MessageSeverity.Warning, message, source, DateTime.Now)` and calls
      `RecountAndNotify()`.
- [x] Build: `dotnet build SemiStep/Application/Application.csproj` — must pass clean.

### Branch 3 — Wire PLC events to the message panel

Scope: `UI` only. Depends on Branch 1 (event carries state) and Branch 2 (`AddWarning` exists).
Merge Branches 1 and 2 first, then cut this branch from the updated base.

**Goal:** Extend `RecipeMutationCoordinator.Initialize()` to forward PLC connection and sync
events into `MessagePanelViewModel`.

**Decisions:**

- **Connection lost when sync disabled:** suppress the `"PLC connection lost"` panel entry
  when sync is not enabled — a connectivity gap is not an error if the operator has not asked
  for sync. `Connected` info entries are always shown regardless of sync state.
- **`EnableSync` failure accumulates:** do not clear previous PLC entries on a new connection
  attempt; always append.

**Events to handle and their panel mapping:**

| Event | Condition | Method | Message |
|-------|-----------|--------|---------|
| `ConnectionStateChanged` | `Connected` | `AddInfo` | `"Connected to PLC ({ip})"` |
| `ConnectionStateChanged` | `Disconnected` AND sync enabled | `AddError` | `"PLC connection lost"` |
| `ConnectionStateChanged` | `Disconnected` AND sync disabled | — | no panel entry |
| `syncService.StatusChanged` | `Synced` | `AddInfo` | `"Recipe synced to PLC"` |
| `syncService.StatusChanged` | `Failed` | `AddError` | `syncService.LastError ?? "Sync failed"` |
| `syncService.StatusChanged` | `OutOfSync` | — | no panel entry (recipe invalidity is already shown as structural warnings) |
| `syncService.StatusChanged` | others | — | no panel entry |

`ErrorChanged` currently forwards to `_connectionStateChanged` — keep that for UI property
refresh; no panel write needed (covered by `StatusChanged → Failed`).

**`EnableSync` failure:** `RecipeMutationCoordinator.EnableSync()` returns `Result`. Currently
`MainWindowViewModel.ExecuteToggleSyncAsync` discards the result. Instead, surface it in the
panel: after `await domainFacade.EnableSync(...)`, if `result.IsFailed`, call
`messagePanel.AddError($"Failed to enable sync: {result.Errors[0].Message}", "PLC")`.
Append — do not clear prior entries.

**Files:**
- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] Replace the current `_connectionStateChangedRelay = () => ...` with a named private method
      `OnConnectionStateChanged(PlcConnectionState state)` that:
      - calls `_connectionStateChanged.OnNext(Unit.Default)`
      - on `Connected`: calls `messagePanel.AddInfo($"Connected to PLC ({appConfiguration.PlcConfiguration.Connection.IpAddress})", "PLC")`
      - on `Disconnected` and `queryService.IsSyncEnabled`: calls `messagePanel.AddError("PLC connection lost", "PLC")`
      - on `Disconnected` and sync disabled: no panel call
- [x] Replace `_statusChangedRelay = _ => _connectionStateChanged.OnNext(...)` with a named
      private method `OnSyncStatusChanged(PlcSyncStatus status)` that:
      - calls `_connectionStateChanged.OnNext(Unit.Default)`
      - on `Synced`: calls `messagePanel.AddInfo("Recipe synced to PLC", "PLC")`
      - on `Failed`: calls `messagePanel.AddError(syncService.LastError ?? "Sync failed", "PLC")`
      - all other statuses: no panel call
- [x] In `EnableSync()`: after `await domainFacade.EnableSync(...)`, if `result.IsFailed`,
      call `messagePanel.AddError($"Failed to enable sync: {result.Errors[0].Message}", "PLC")`.
- [x] `EnableSync()` is called from `MainWindowViewModel` which does not `await` the result —
      verify the `Result` is returned through and handled; no `Dispatcher.UIThread.Post` needed
      here since `EnableSync` is awaited on the UI thread via `ReactiveCommand`.
- [x] All event handler methods registered in `Initialize()` must be unregistered by the same
      delegate reference in `Dispose()` — update accordingly.
- [x] Build: `dotnet build SemiStep/Application/Application.csproj` — must pass clean.
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj --filter "Component=UI"` — must pass.
