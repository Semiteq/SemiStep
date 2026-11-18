# Plan: MessagePanelViewModel Self-Marshalling Thread Safety

## Overview

`MessagePanelViewModel` mutates an `ObservableCollection<MessageEntry>` and raises
`PropertyChanged` on `ErrorCount`, `WarningCount`, and `HasEntries` from multiple call sites,
some of which run on thread-pool threads (async coordinator continuations, S7 event callbacks).
These off-thread mutations crash with `System.InvalidOperationException: Call from invalid
thread` because ReactiveUI's `WhenAnyValue` pipeline drives `ClearCommand.CanExecute`, which
updates `Button.Command` — a property that Avalonia enforces on the UI thread. The fix makes
`MessagePanelViewModel` self-marshalling: every public mutating method dispatches to the UI
thread internally, removing the obligation from every caller and making the invariant impossible
to violate regardless of call site.

## Solution Overview

**Chosen approach — internal dispatch in `MessagePanelViewModel`:**
Every public method that mutates `Entries`, `ErrorCount`, `WarningCount`, or `HasEntries`
wraps its body in `Dispatcher.UIThread.InvokeAsync` when not already on the UI thread, or
executes synchronously when called from the UI thread. The standard Avalonia idiom for this is
`Dispatcher.UIThread.CheckAccess()` — if true, call directly; if false, post. Since all
mutations are fire-and-forget from the caller's perspective (no return value to await), `Post`
is sufficient and avoids blocking.

**Why not keep `Dispatcher.UIThread.Post` at each call site:**
Call sites are spread across `RecipeMutationCoordinator`, `RecipeFileViewModel`,
`ClipboardViewModel`, `RecipeGridViewModel`, `MainWindowViewModel`, and potentially future
classes. Enforcing the constraint at every call site requires all future authors to remember it
and is already proven fragile — the current crash is the result of a missed call site.
Centralising the dispatch in the ViewModel is the only robust solution.

**`AddWarning` addition:**
The current state on disk adds `AddWarning` to `MessagePanelViewModel`. This is kept as it is
consistent with the existing `AddError` / `AddInfo` pattern.

**Callers to simplify:**
Once `MessagePanelViewModel` is self-marshalling, the `Dispatcher.UIThread.Post(() =>
messagePanel.*(...))` wrappers that exist in `RecipeMutationCoordinator` and
`RecipeFileViewModel` become redundant. They are safe to remove (direct calls are fine
since the ViewModel will dispatch if needed) but removing them is optional cleanup — the plan
removes them to avoid confusion.

**`RecipeFileViewModel` — `Dispatcher.UIThread.Post` removal:**
The `Dispatcher.UIThread.Post` wrappers around `_messagePanel.Add*` in `SaveToFileAsync` and
`LoadRecipeAsync` are removed; direct calls are used instead, since the ViewModel now handles
dispatch.

**`RecipeMutationCoordinator` — `Dispatcher.UIThread.Post` on `messagePanel` calls:**
The `Post` wrappers around `messagePanel.Clear()` and `RefreshMessagePanel()` inside
`LoadRecipeAsync` and `LoadRecipeFromPlcAsync` are removed for the same reason.
The `Post` wrappers on `OnConnectionStateChanged`, `OnSyncStatusChanged`, and
`OnPlcRecipeConflictDetected` that protect non-messagePanel UI operations (`_plcRecipeConflictDetected.OnNext`)
are left in place — those fire `Subject.OnNext` into the Avalonia binding system and
must remain on the UI thread independently of the messagePanel fix.

**Alternatives rejected:**

- `ObserveOn(RxApp.MainThreadScheduler)` applied to `ClearCommand.CanExecute` source: would
  require modifying ReactiveUI internals or wrapping the `WhenAnyValue` pipeline, fragile.
- Making `MessagePanelViewModel` use `IScheduler` injection for testability: unnecessary
  complexity; Avalonia's `Dispatcher.UIThread.CheckAccess()` is already the correct primitive
  and tests that construct `MessagePanelViewModel` without a running Avalonia dispatcher call
  methods on the UI thread (xUnit test runner), so the check returns false and `Post` is used
  — which is safe in test context since Avalonia initialises a headless dispatcher.

## Affected Files

### Modified Files

| File                                                   | Change                                                                                                                                                                                             |
| ------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SemiStep/UI/MessageService/MessagePanelViewModel.cs`  | All public mutating methods (`AddError`, `AddWarning`, `AddInfo`, `RefreshReasons`, `Clear`, private `ClearNonStructural`) dispatch via `Dispatcher.UIThread.CheckAccess()` / `Post`               |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | Remove `Dispatcher.UIThread.Post` wrappers around `messagePanel.*` calls in async methods; keep `Post` wrappers for non-messagePanel subjects                                                      |
| `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs`        | Remove `using Avalonia.Threading;`; remove `Dispatcher.UIThread.Post` wrappers; call `_messagePanel.Add*` directly                                                                                 |
| `SemiStep/Tests/UI/MessagePanelViewModelTests.cs`      | Add `Dispatcher.UIThread.RunJobs(null)` after mutating calls where the test asserts on `ErrorCount`, `WarningCount`, or `HasEntries` — because these are now set via `Post`, which is asynchronous |

## Tasks

### Task 1: Make MessagePanelViewModel self-marshalling

**Files:**

- Modify: `SemiStep/UI/MessageService/MessagePanelViewModel.cs`

- [x] Add `using Avalonia.Threading;` directive (System group first, then others)
- [x] Extract a private helper `private void PostOnUiThread(Action action)` that checks
      `Dispatcher.UIThread.CheckAccess()` and either calls `action()` directly or calls
      `Dispatcher.UIThread.Post(action)`
- [x] Wrap the body of `AddError` with `PostOnUiThread(() => { ... })`
- [x] Wrap the body of `AddWarning` with `PostOnUiThread(() => { ... })`
- [x] Wrap the body of `AddInfo` with `PostOnUiThread(() => { ... })`
- [x] Wrap the body of `Clear` with `PostOnUiThread(() => { ... })`
- [x] Wrap the body of `RefreshReasons` with `PostOnUiThread(() => { ... })` — capture the
      `reasons` list before posting (materialise with `.ToList()`) to avoid deferred enumeration
      of a caller-owned collection
- [x] Wrap the body of `ClearNonStructural` with `PostOnUiThread(() => { ... })`
- [x] Verify `RecountAndNotify` is private and only called from within already-dispatched bodies
      (no direct dispatch needed there)

### Task 2: Remove redundant Dispatcher.UIThread.Post wrappers from RecipeFileViewModel

**Files:**

- Modify: `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs`

- [x] Remove `using Avalonia.Threading;` directive
- [x] In `SaveToFileAsync`: replace `Dispatcher.UIThread.Post(() => _messagePanel.AddInfo(...))` with direct `_messagePanel.AddInfo(...)`
- [x] In `SaveToFileAsync`: replace `Dispatcher.UIThread.Post(() => _messagePanel.AddError(...))` with direct `_messagePanel.AddError(...)`
- [x] In `LoadRecipeAsync`: replace `Dispatcher.UIThread.Post(() => _messagePanel.AddInfo(...))` with direct `_messagePanel.AddInfo(...)`
- [x] In `LoadRecipeAsync`: replace `Dispatcher.UIThread.Post(() => _messagePanel.AddError(...))` with direct `_messagePanel.AddError(...)`

### Task 3: Remove redundant Dispatcher.UIThread.Post wrappers from RecipeMutationCoordinator

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

The coordinator's `LoadRecipeAsync` and `LoadRecipeFromPlcAsync` currently have no `Post`
wrappers on the committed HEAD (they were added by the fixer agent but not committed). The
unstaged diff on disk shows additional changes (connection state wiring, `OnConnectionStateChanged`,
`OnSyncStatusChanged`, `_subjectLock`). The current disk state is the target — it already has
`Post` wrappers on the non-messagePanel UI subjects. The only change needed is to ensure
`messagePanel.Clear()` and `RefreshMessagePanel()` in the async methods are called directly
(no `Post` wrapper) since the ViewModel handles dispatch.

- [x] In `LoadRecipeFromPlcAsync`: if a `Dispatcher.UIThread.Post(() => { messagePanel.Clear(); RefreshMessagePanel(...); })` block exists, unwrap it to direct calls
- [x] In `LoadRecipeAsync`: same unwrapping
- [x] Confirm that `OnConnectionStateChanged`, `OnSyncStatusChanged`, `OnPlcRecipeConflictDetected`
      retain their `Dispatcher.UIThread.Post` wrappers (these protect `_plcRecipeConflictDetected.OnNext`
      and are unrelated to the messagePanel change)
- [x] Confirm `EnableSync`'s `Dispatcher.UIThread.Post(() => messagePanel.AddError(...))` is
      also unwrapped to a direct call

### Task 4: Update MessagePanelViewModel tests

**Files:**

- Modify: `SemiStep/Tests/UI/MessagePanelViewModelTests.cs`

Because `PostOnUiThread` uses `Dispatcher.UIThread.Post` when called off the UI thread, and
xUnit tests run on a non-Avalonia thread, assertions on `ErrorCount`, `WarningCount`,
`HasEntries`, and `Entries` that follow a mutating call must flush the dispatcher queue first.
Avalonia's headless mode is required, or alternatively the tests can use
`Dispatcher.UIThread.RunJobs(null)` after each mutating call.

- [x] Check how existing `MessagePanelViewModelTests` are structured — do they use a headless
      Avalonia fixture or plain instantiation?
- [x] If plain instantiation (no headless dispatcher): for each test that asserts on
      `ErrorCount`, `WarningCount`, `HasEntries`, `Entries` after a mutating call, add
      `Dispatcher.UIThread.RunJobs(null)` immediately after the mutating call and before the
      assertion
- [x] Verify that the `ClearCommand.Execute().Subscribe()` tests also flush (`RunJobs`) since
      `ClearNonStructural` is now dispatched
- [x] Add `using Avalonia.Threading;` if not already present

### Task 5: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass
