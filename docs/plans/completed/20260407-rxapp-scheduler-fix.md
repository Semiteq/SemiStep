# Plan: Fix ReactiveCommand CanExecute Threading Crash

## Overview

`ReactiveCommand.Create` captures `RxApp.MainThreadScheduler` at construction time to marshal
`CanExecute` emissions. In `Program.Main`, the DI container is resolved (constructing all
singletons, including `MessagePanelViewModel` and its `ReactiveCommand` instances) **before**
`RxApp.MainThreadScheduler` is set to `AvaloniaScheduler.Instance`. The commands therefore
permanently capture `DefaultScheduler` (thread-pool / long-running), so when `CanExecute`
re-evaluates after a background-thread event, it emits on a pool thread, `Button.CanExecuteChanged`
fires there, and Avalonia's `VerifyAccess()` throws `InvalidOperationException`.

## Solution Overview

**Fix the scheduler initialisation order.** Move `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance`
to the very top of `Program.Main()`, before the config load, before `ServiceCollection` is
built, and before any singleton is resolved. This is the only line that must move; no
architectural change is needed.

**Unify the threading model in `OnConnectionStateChanged` and `OnSyncStatusChanged`.**
Both handlers fire on PLC background threads. Currently they call `messagePanel.AddInfo/AddError`
directly (which posts to the UI thread via `PostOnUiThread`) while `NotifyConnectionStateChanged()`
fires synchronously on the background thread. This is an inconsistent hybrid. The unified
approach — already used by `OnPlcRecipeConflictDetected` — is to wrap the entire handler body
in `Dispatcher.UIThread.Post(...)`. With `PostOnUiThread` inside `MessagePanelViewModel`, the
inner `messagePanel.*` calls will then short-circuit (`CheckAccess() == true`) and execute
directly, with no double-post.

**`RxApp.MainThreadScheduler` and `AvaloniaScheduler`:** setting the scheduler before Avalonia
initialises is safe because `AvaloniaScheduler.Instance` is a static singleton that does not
require the application loop to be running; it only needs `Dispatcher.UIThread` to exist, which
it does from process start in Avalonia's Windows backend.

**Alternatives rejected:**

- Moving the `ReactiveCommand` creation to a lazy/deferred point: requires significant
  refactoring of `MessagePanelViewModel` and is unnecessary.
- Keeping the hybrid (direct `messagePanel` call + synchronous subject notification): produces
  an ordering difference between the panel message and the state notification that reaches
  the UI thread, and the user confirmed a unified coherent model is required.

## Affected Files

### Modified Files

| File                                                   | Change                                                                                                        |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------- |
| `SemiStep/Application/Program.cs`                      | Move `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance` to top of `Main`, before config load            |
| `SemiStep/UI/App.axaml.cs`                             | Remove `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance` from `Run()` (it now lives in `Program.Main`) |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | Wrap entire body of `OnConnectionStateChanged` and `OnSyncStatusChanged` in `Dispatcher.UIThread.Post(...)`   |

## Tasks

### Task 1: Move RxApp.MainThreadScheduler initialisation to Program.Main

**Files:**

- Modify: `SemiStep/Application/Program.cs`
- Modify: `SemiStep/UI/App.axaml.cs`

- [x] In `Program.Main()`, add `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;` as
      the very first statement, before `CreateLogger(...)` and before any other call
- [x] Add `using ReactiveUI;` to `Program.cs` if not already present
- [x] Add `using Avalonia.ReactiveUI;` to `Program.cs` if not already present (for `AvaloniaScheduler`)
- [x] Remove `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;` from `App.Run()` in
      `App.axaml.cs` (it is now set earlier and does not need to be repeated)
- [x] Verify that `App.axaml.cs` still has `using Avalonia.ReactiveUI;` only if it is still
      needed for other uses; remove it if `AvaloniaScheduler` was its only use

### Task 2: Unify threading model in RecipeMutationCoordinator event handlers

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] In `OnConnectionStateChanged`: wrap the entire method body (the `NotifyConnectionStateChanged()`
      call and the `switch` statement including `messagePanel.AddInfo`/`messagePanel.AddError`) in
      `Dispatcher.UIThread.Post(() => { ... })` — matching the pattern of `OnPlcRecipeConflictDetected`
- [x] In `OnSyncStatusChanged`: wrap the entire method body (the `NotifyConnectionStateChanged()`
      call and the `switch` statement including `messagePanel.AddInfo`/`messagePanel.AddError`) in
      `Dispatcher.UIThread.Post(() => { ... })`
- [x] Confirm `OnPlcRecipeConflictDetected` already has `Dispatcher.UIThread.Post` wrapping its body
      (no change needed there, just verification)
- [x] Confirm `using Avalonia.Threading;` is already present in the file (it is — no change needed)

### Task 3: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass
