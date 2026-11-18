# Plan: Thread Safety and Async Correctness Refactoring

## Overview

A systematic audit of the entire codebase reveals four categories of threading problems: a confirmed
race condition on `ObservableCollection` mutations from thread-pool continuations; a missing
`[STAThread]` entry point that causes COM clipboard failures on Windows; a fire-and-forget
reconciliation task in `DomainFacade` that has no cancellation token and unsynchronised access to
domain state; and several minor convention violations and silent failure risks. This plan addresses
all confirmed defects and eliminates the latent ones by applying consistent thread-marshalling
discipline at every layer boundary.

## Solution Overview

**Principle:** Each layer owns the thread on which it delivers results. The S7 layer publishes on
the thread pool and makes no promise otherwise. The Domain layer passes through unchanged and makes
no promise. The UI layer is solely responsible for marshalling to the Avalonia UI thread before
touching any UI state — via `ObserveOn(RxApp.MainThreadScheduler)` on every subscription that
mutates UI-bound collections or raises `PropertyChanged`.

**Entry point (STA):** The correct Avalonia-on-Windows pattern is `[STAThread] static void Main()`.
Async startup work (config load, DI) is run on a background pool thread via `Task.Run(...).GetAwaiter().GetResult()`.
This is safe because `Task.Run` captures no `SynchronizationContext`, so continuations inside the
lambda run on the pool and never try to marshal back to the blocked STA thread — no deadlock.
`App.Run(provider)` is then called on the original STA thread, which is what Avalonia's Win32
backend requires for COM/clipboard.

**`RecipeGridViewModel.StateChanged` subscription:** The missing `ObserveOn` is the only confirmed
race condition. Adding it (matching the pattern already used for `ExecutionState` in the same
constructor) is a one-line fix.

**`RecipeMutationCoordinator` async methods:** `LoadRecipeAsync`, `SaveRecipeAsync`, and
`LoadRecipeFromPlcAsync` call `RefreshMessagePanel` and `_stateChanged.OnNext` from the thread
that completes the `await` — which is a pool thread for file I/O. The coordinators call into
`MessagePanelViewModel.RefreshReasons` (mutates `ObservableCollection<MessageEntry>`) and into
`RecipeGridViewModel.OnStateChange` (mutates `ObservableCollection<RecipeRowViewModel>`) off the
UI thread. The fix is not in the coordinator (it is not a UI class) but in the downstream
subscribers, which must `ObserveOn` before touching UI state. For `MessagePanel`, which receives
direct method calls (not observable subscriptions), the call sites in `RecipeFileViewModel` must
be wrapped in `Avalonia.Threading.Dispatcher.UIThread.Post`.

**`DomainFacade.PerformReconnectReconciliationAsync`:** Add a `CancellationToken` parameter
threaded from `EnableSync`/`DisableSync`. On `DisableSync`, cancel the token. This ensures a
stale reconciliation cannot write a recipe after the user disconnects.

**`ToggleSyncCommand` silent failure:** Add a `ThrownExceptions` subscription matching the pattern
used in `ClipboardViewModel` and `RecipeFileViewModel`.

**`Log.Warning(ex, ...)` violation:** Change to `Log.Warning(ex.Message)` in `MainWindowViewModel`.

**Alternatives rejected:**

- Marshalling at the coordinator level (making coordinator UI-thread-aware) — violates the
  architecture: `RecipeMutationCoordinator` is a UI-layer class but `DomainFacade` is not; the
  coordinator should not know about schedulers.
- Using `ObservableCollection` on a background thread with Avalonia's dispatcher binding — fragile
  and non-obvious. Explicit `ObserveOn` is the standard ReactiveUI contract.

## Affected Files

### Modified Files

| File                                            | Change                                                                                                        |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `SemiStep/Application/Program.cs`               | `[STAThread]` + `void Main()` + `Task.Run` for async startup                                                  |
| `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs` | Add `ObserveOn(RxApp.MainThreadScheduler)` to `StateChanged` subscription (line 59)                           |
| `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs` | Wrap `MessagePanel` calls after async coordinator awaits with `Dispatcher.UIThread.Post` or `InvokeAsync`     |
| `SemiStep/UI/MainWindow/MainWindowViewModel.cs` | Add `ToggleSyncCommand.ThrownExceptions` subscription; fix `Log.Warning(ex, ...)` → `Log.Warning(ex.Message)` |
| `SemiStep/Domain/Facade/DomainFacade.cs`        | Add `CancellationToken` to `PerformReconnectReconciliationAsync`; wire it through `EnableSync`/`DisableSync`  |

## Tasks

### Task 1: Fork new branch

**Files:** (none)

- [x] `git checkout -b fix/thread-safety` from current branch (`fix/s7-protocol` or `fix/clipboard-com-and-ux`)

### Task 2: Fix STA entry point in Program.cs

**Files:**

- Modify: `SemiStep/Application/Program.cs`

- [x] Add `[STAThread]` attribute to `Main`
- [x] Change `public static async Task Main()` to `public static void Main()`
- [x] Extract async startup (config load + DI build + `InitializeServices`) into `private static async Task<StartupResult> StartupAsync()` returning a discriminated union or out-style struct that carries either a `IServiceProvider` (success) or `IReadOnlyList<string>` errors (failure)
- [x] In `Main`, call `Task.Run(StartupAsync).GetAwaiter().GetResult()` to run the async work on a pool thread without an STA sync context — no deadlock because `Task.Run` strips the sync context
- [x] Call `App.Run(provider)` or `App.RunErrorWindow(errors)` on the returned result, still on the STA thread
- [x] Call `Log.CloseAndFlushAsync().GetAwaiter().GetResult()` in `finally` — safe since it runs on pool thread inside the lambda or in a second `Task.Run`

### Task 3: Fix missing ObserveOn in RecipeGridViewModel

**Files:**

- Modify: `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs`

- [x] Change line 59 from:
  ```csharp
  coordinator.StateChanged.Subscribe(OnStateChange).DisposeWith(_disposables);
  ```
  to:
  ```csharp
  coordinator.StateChanged
      .ObserveOn(RxApp.MainThreadScheduler)
      .Subscribe(OnStateChange)
      .DisposeWith(_disposables);
  ```
- [x] Verify this is consistent with the `ExecutionState` subscriptions above it on lines 48–57

### Task 4: Fix MessagePanel cross-thread mutation in RecipeFileViewModel

**Files:**

- Modify: `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs`

The issue: after `await _coordinator.LoadRecipeAsync(...)` or `await _coordinator.SaveRecipeAsync(...)`,
the continuation runs on a pool thread. Calls to `_messagePanel.AddInfo(...)` and `_messagePanel.AddError(...)`
that appear after those awaits mutate `ObservableCollection<MessageEntry>` from the pool thread.

- [x] Add `using Avalonia.Threading;` directive
- [x] Wrap `_messagePanel.AddInfo(...)` and `_messagePanel.AddError(...)` calls that appear after
      async awaits in `SaveToFileAsync` and `LoadRecipeAsync` with `Dispatcher.UIThread.Post(() => { ... })`
- [x] Verify: the calls inside `ThrownExceptions` subscribers are already `ObserveOn`'d (lines 36–48) and do not need changes

### Task 5: Add ToggleSyncCommand ThrownExceptions subscription in MainWindowViewModel

**Files:**

- Modify: `SemiStep/UI/MainWindow/MainWindowViewModel.cs`

- [x] Add to the constructor (after the existing subscriptions):
  ```csharp
  ToggleSyncCommand.ThrownExceptions
      .ObserveOn(RxApp.MainThreadScheduler)
      .Subscribe(ex => MessagePanel.AddError($"Sync toggle failed: {ex.Message}", "Sync"))
      .DisposeWith(_disposables);
  ```
- [x] Fix `Log.Warning(ex, "...")` at line 154 to `Log.Warning("Unexpected error while handling PLC recipe conflict: {Message}", ex.Message)` — passing the message, not the exception object, per the project logging convention

### Task 6: Add CancellationToken to reconciliation in DomainFacade

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] Add a `CancellationTokenSource? _reconciliationCts` field
- [x] In `PerformReconnectReconciliationAsync`, add a `CancellationToken ct = default` parameter and pass it to all awaited calls
- [x] In the `_connectionStateChangedRelay` lambda (line 114), before fire-and-forgetting: cancel and dispose any existing `_reconciliationCts`, create a new one, capture its token, then pass to `PerformReconnectReconciliationAsync(ct)`
- [x] In `DisableSync`, cancel `_reconciliationCts` before disconnecting
- [x] In `Dispose()`, cancel and dispose `_reconciliationCts`
- [x] Update the `ContinueWith` faulted handler to skip logging on `OperationCanceledException` (use `TaskContinuationOptions.OnlyOnFaulted` — already set — which excludes cancelled tasks, so no change needed here)

### Task 7: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run full test suite: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass

## Open Questions

(none — the `Log.Warning` fix from the previous plan is included here in Task 5)

## Findings Reference

For traceability, here is the complete severity ranking of every finding from the audit:

| Severity           | Location                                    | Issue                                                                                                                                                                 |
| ------------------ | ------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Confirmed race** | `RecipeGridViewModel.cs:59`                 | `StateChanged.Subscribe` without `ObserveOn` — `ObservableCollection` mutated from pool thread on file load/save                                                      |
| **Confirmed race** | `RecipeFileViewModel.cs:104,129,133`        | `_messagePanel.AddInfo/AddError` after async awaits run on pool thread, mutating `ObservableCollection<MessageEntry>`                                                 |
| **Confirmed bug**  | `Program.cs:32`                             | `async Task Main()` without `[STAThread]` — COM clipboard calls fail with `CO_E_NOTINITIALIZED`                                                                       |
| **Latent bug**     | `DomainFacade.cs:114,452`                   | `PerformReconnectReconciliationAsync` has no cancellation token — stale reconciliation can overwrite recipe after `DisableSync`                                       |
| **Silent failure** | `MainWindowViewModel.cs:49`                 | `ToggleSyncCommand` has no `ThrownExceptions` subscription                                                                                                            |
| **Convention**     | `MainWindowViewModel.cs:154`                | `Log.Warning(ex, ...)` — exception object passed to `Log.Warning`, violates project convention                                                                        |
| **Fragile**        | `S7Service.cs:151`                          | `_ = StopKeepAlive()` discards task — safe only because all exceptions are caught inside the loop                                                                     |
| **Fragile**        | `PlcExecutionMonitor.cs:54,91`              | `Subject<T>.OnNext` called from two possible threads (poll loop + `Stop()` caller) without a lock — race mitigated by cancellation ordering but not formally excluded |
| **Info**           | `IS7Service.cs:12`, `IPlcSyncService.cs:18` | Observable/event interfaces document no threading guarantees                                                                                                          |
