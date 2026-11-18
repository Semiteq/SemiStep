# Plan: Fix Recurring Threading Architecture Problems (STA + Dispatch Model)

## Overview

The project has a recurring cycle of threading bugs that keep resurfacing because two
independent root causes are addressed separately, then one fix gets lost when the other is
applied. The two problems are:

1. **Missing `[STAThread]` entry point** -- Windows OLE clipboard requires an STA apartment.
   Without it, `IClipboard.SetTextAsync()`/`GetTextAsync()` fail with
   `CO_E_NOTINITIALIZED (0x800401F0)`. This was fixed in commit `e1a5838` but that commit
   lives on a **parallel branch** that was never merged into the current HEAD (`26df301`).
   The current uncommitted changes reverted to `async Task Main()` without `[STAThread]`,
   and the clipboard is broken again.

2. **`RxApp.MainThreadScheduler` set too late** -- `ReactiveCommand` captures
   `RxApp.MainThreadScheduler` at construction time. If the scheduler is not set before DI
   resolves singletons (which construct `ReactiveCommand` instances), commands permanently
   capture `DefaultScheduler` (thread pool). `CanExecute` then fires off the UI thread,
   causing `VerifyAccess()` crashes. The fix in the uncommitted changes (setting the scheduler
   at the top of `Main()`) is correct, but it conflicts with the `[STAThread]` fix because
   that fix used `void Main()` + `Task.Run(StartupAsync).GetAwaiter().GetResult()`.

3. **Inconsistent dispatch model in `RecipeMutationCoordinator`** -- Event handlers
   (`OnConnectionStateChanged`, `OnSyncStatusChanged`) fire from background PLC threads. The
   current uncommitted changes removed the `Dispatcher.UIThread.Post` wrappers from the
   `messagePanel` calls (relying on the self-marshalling `MessagePanelViewModel`), but also
   removed them from the `NotifyConnectionStateChanged()` call (which fires `Subject.OnNext`
   into the Rx pipeline). This creates a split: `NotifyConnectionStateChanged()` is posted
   separately from the `messagePanel` calls, but the `messagePanel` calls themselves now run
   on the background PLC thread (self-marshalling delays them via `Post`). The ordering between
   the two groups becomes non-deterministic.

4. **"Row 1 Row 2 ... Row 46" paste error** -- This is a symptom of problem #1. When the
   copy fails silently (COM not initialized), nothing is written to the clipboard. The
   clipboard still contains whatever text was there before (possibly a DataGrid text
   representation or other non-TSV content). When the user then pastes, the deserializer
   tries to parse that stale text as TSV, finds it does not match the expected column
   structure, and produces per-row errors like "Row 1", "Row 2", etc. for every line it
   encounters.

The fundamental architectural issue is: **there is no single, documented threading contract
for the application entry point that satisfies both the STA requirement (for COM/clipboard)
and the ReactiveUI scheduler requirement (for `ReactiveCommand` construction)**. Each time one
is fixed, the other regresses.

## Solution Overview

**Combine both fixes in `Program.Main`:**

The correct Windows/Avalonia entry point pattern is:

```csharp
[STAThread]
public static void Main()
{
    RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
    CreateLogger(LogFilePath);
    // ... async startup on pool thread ...
    // ... App.Run on STA thread ...
}
```

Key points:

- `[STAThread]` + `void Main()` -- ensures the calling thread is STA, which Avalonia's Win32
  backend requires for COM/clipboard.
- `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance` as the first statement -- ensures
  all `ReactiveCommand` instances created during DI resolution capture the correct scheduler.
  This is safe because `AvaloniaScheduler.Instance` is a static singleton that delegates to
  `Dispatcher.UIThread`, which exists from process start.
- Async startup (`ConfigFacade.LoadAndValidateAsync`, DI build, `InitializeServices`) runs
  inside `Task.Run(...).GetAwaiter().GetResult()` -- this executes on a thread-pool thread
  with no `SynchronizationContext`, so there is no deadlock risk. The STA thread blocks
  synchronously until startup completes.
- `App.Run(provider)` executes on the original STA thread.

**Unify the dispatch model in `RecipeMutationCoordinator`:**

All event handlers that fire from background threads (`OnConnectionStateChanged`,
`OnSyncStatusChanged`, `OnPlcRecipeConflictDetected`) should wrap their **entire body** in a
single `Dispatcher.UIThread.Post(() => { ... })`. This means both the `Subject.OnNext` call
and the `messagePanel` calls execute atomically on the UI thread in a single dispatch, with
deterministic ordering. The `messagePanel` calls will short-circuit inside `PostOnUiThread`
(`CheckAccess()` returns true since we are already on the UI thread).

The `_syncErrorChangedRelay` lambda should also dispatch via `Dispatcher.UIThread.Post`.

**Keep `RecipeGridViewModel.StateChanged` subscription without `ObserveOn`:**

The `StateChanged` subscription in `RecipeGridViewModel` at line 59 currently has no
`.ObserveOn(RxApp.MainThreadScheduler)`. This is safe **only if** every `_stateChanged.OnNext()`
call in `RecipeMutationCoordinator` runs on the UI thread. With the unified dispatch model
above, all background-originated signals go through `Dispatcher.UIThread.Post` before hitting
`Subject.OnNext`. The synchronous mutation methods (which call `_stateChanged.OnNext` directly)
are always invoked from UI-thread command handlers. So the invariant holds. However, this is
fragile -- a single future caller that fires `_stateChanged.OnNext` from a background thread
will break it silently. Adding `.ObserveOn(RxApp.MainThreadScheduler)` is cheap insurance.

**Alternatives rejected:**

- Keeping `async Task Main()` and adding `Thread.CurrentThread.SetApartmentState(ApartmentState.STA)`
  -- does not work; .NET ignores `SetApartmentState` on the main thread after it has already
  started. The `[STAThread]` attribute must be present at entry.
- Using `Dispatcher.UIThread.InvokeAsync` (synchronous/awaitable) instead of `Post` in event
  handlers -- adds unnecessary complexity; the handlers are fire-and-forget.

## Affected Files

### Modified Files

| File                                                   | Change                                                                                                                                      |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `SemiStep/Application/Program.cs`                      | `[STAThread]` + `void Main()` + `RxApp.MainThreadScheduler` first + `Task.Run` for async startup                                            |
| `SemiStep/UI/App.axaml.cs`                             | Remove `RxApp.MainThreadScheduler` if present (already done in uncommitted changes)                                                         |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | Wrap entire body of `OnConnectionStateChanged` and `OnSyncStatusChanged` in single `Dispatcher.UIThread.Post`; fix `_syncErrorChangedRelay` |
| `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs`        | Add `.ObserveOn(RxApp.MainThreadScheduler)` to `StateChanged` subscription as safety net                                                    |

## Tasks

### Task 1: Fix Program.cs entry point (STA + scheduler + async startup)

**Files:**

- Modify: `SemiStep/Application/Program.cs`

- [x] Add `[STAThread]` attribute to `Main`
- [x] Change signature from `public static async Task Main()` to `public static void Main()`
- [x] Keep `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;` as the first statement
      (already present in uncommitted changes)
- [x] Keep `CreateLogger(LogFilePath)` immediately after
- [x] Extract async startup (config load, DI build, `InitializeServices`) into
      `private static async Task<(IServiceProvider? Provider, IReadOnlyList<string>? Errors)> StartupAsync()`
- [x] In `Main`, call `var outcome = Task.Run(StartupAsync).GetAwaiter().GetResult();` to run
      async startup on a pool thread without STA sync context
- [x] After `Task.Run` completes, on the STA thread: if `outcome.Errors` is not null, call
      `App.RunErrorWindow(outcome.Errors)`; else call `App.Run(outcome.Provider)`
- [x] Move `Log.CloseAndFlushAsync()` into a `finally` block, called as
      `Log.CloseAndFlushAsync().GetAwaiter().GetResult()`
- [x] Keep the existing `using` directives for `ReactiveUI` and `Avalonia.ReactiveUI`

### Task 2: Verify App.axaml.cs has no scheduler assignment

**Files:**

- Modify: `SemiStep/UI/App.axaml.cs` (if needed)

- [x] Confirm `RxApp.MainThreadScheduler = AvaloniaScheduler.Instance` is NOT in `App.Run()`
      (already removed in uncommitted changes)
- [x] Confirm `using ReactiveUI;` is removed if no longer needed
- [x] No other changes needed

### Task 3: Unify dispatch model in RecipeMutationCoordinator

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] `OnConnectionStateChanged`: wrap the **entire** method body in a single
      `Dispatcher.UIThread.Post(() => { ... })`. Inside the lambda:
      `NotifyConnectionStateChanged()` first, then the `switch` with `messagePanel.AddInfo`/
      `messagePanel.AddError`. Capture `appConfiguration.PlcConfiguration.Connection.IpAddress`
      outside the lambda (or read it inside -- it is a config value, thread-safe)
- [x] `OnSyncStatusChanged`: same pattern -- wrap entire body in single `Dispatcher.UIThread.Post`.
      Capture `syncService.LastError` **before** the `Post` (it may change on the background
      thread between the event firing and the lambda executing)
- [x] `_syncErrorChangedRelay` in `Initialize()`: confirm it dispatches via
      `Dispatcher.UIThread.Post(NotifyConnectionStateChanged)` (already correct in uncommitted
      changes)
- [x] `OnPlcRecipeConflictDetected`: confirm it wraps in `Dispatcher.UIThread.Post` (already
      correct, no change)
- [x] `EnableSync`: `messagePanel.AddError` call is fine as direct call (runs on UI thread
      since it is inside an async method invoked from a UI-thread command) -- no change needed
- [x] Remove `_subjectLock` field and its usages -- no longer needed since all `Subject.OnNext`
      calls are serialised on the UI thread

### Task 4: Add ObserveOn safety net to RecipeGridViewModel.StateChanged

**Files:**

- Modify: `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs`

- [x] Change line 59 from:
      `coordinator.StateChanged.Subscribe(OnStateChange).DisposeWith(_disposables);`
      to:
      `coordinator.StateChanged.ObserveOn(RxApp.MainThreadScheduler).Subscribe(OnStateChange).DisposeWith(_disposables);`
- [x] Verify consistency with `ExecutionState` subscriptions above (lines 48-57) which already
      use `.ObserveOn(RxApp.MainThreadScheduler)`

### Task 5: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass

## Open Questions

1. The `MessagePanelViewModel` self-marshalling changes and test `RunJobs(null)` additions in
   the uncommitted diff are correct and should be kept. This plan does not modify
   `MessagePanelViewModel` or its tests -- those changes are orthogonal and compatible.

2. The `PlcSyncCoordinator` ordering fix (setting `LastError` before `Status` to ensure
   subscribers see the error when the status event fires) in the uncommitted diff is correct
   and should be kept. This plan does not touch `PlcSyncCoordinator`.
