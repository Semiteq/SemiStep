# Split Status-Bar Message Channel Into Validation + Transient Channels

## Overview

The status bar currently routes two semantically different kinds of messages through a single
`MessagePanelViewModel`, and which behavior a message gets is chosen implicitly by the magic string
`MessageEntry.Source == "Recipe"`. This causes three concrete defects:

- **Transient errors accumulate forever** and inflate the error/warning badge until manual `Clear`,
  never self-healing even after the user fixes the underlying problem.
- **Success reporting is uneven** — only File operations emit success (`Saved/Loaded`); Clipboard,
  RecipeGrid and PLC report only failures.
- **PLC sync/connection state leaks into the panel** and re-renders a structural error on every ~1s
  poll while the PLC is Failed/Disconnected.

This change splits the one channel into two single-responsibility channels:

- **Validation channel** (`MessagePanelViewModel`, slimmed): sticky, self-healing, fed *only* by
  `RefreshReasons`. Drives the badge and the expandable log list.
- **Transient channel** (`OperationStatusViewModel`, new): single slot, latest-only,
  replace-on-next, no timer. Accumulation is structurally impossible.

PLC connection/sync state stops writing to messages entirely — it remains only in the dedicated live
indicators (connection dot + `PlcSyncStatusText`).

No backward compatibility is required (decided with the user).

## Context (from discovery)

Files/components involved:
- `SemiStep.UI/MessageService/MessagePanelViewModel.cs` — dual-role VM (to be slimmed).
- `SemiStep.UI/MessageService/MessageEntry.cs` — record with `IsStructural`/`StructuralSource` (to lose them).
- `SemiStep.UI/MessageService/MessagePanel.axaml` — log list UI (Clear button + Source column to drop).
- `SemiStep.UI/MessageService/MessageSeverity.cs` — shared severity enum (kept, reused by both channels).
- `SemiStep.UI/Coordinator/RecipeCoordinator.cs` — `RebuildMessagePanel()` concatenates PLC reasons; `OnPlcStateChanged` calls it per poll.
- `SemiStep.UI/Clipboard/ClipboardViewModel.cs` — 5 transient `AddError` sites.
- `SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — 2 transient `AddError` sites.
- `SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` — transient `AddError` + 2 `AddInfo` success sites.
- `SemiStep.UI/MainWindow/MainWindowViewModel.cs` — 4 transient `AddError` sites; exposes `MessagePanel`; `StatusText` property.
- `SemiStep.UI/MainWindow/AppStatusBar.axaml` — `StatusText` TextBlock at Column 0 (to be replaced by the transient slot).
- `SemiStep.UI/UiDi.cs` — DI registrations.

Related patterns found:
- ReactiveUI MVVM, primary-/constructor-injected concrete VMs (no interfaces for single concrete VMs).
- UI-thread marshalling via `Dispatcher.UIThread.CheckAccess()/Post()` (see `MessagePanelViewModel.PostOnUiThread`).
- Severity → brush style-class binding pattern in `MessagePanel.axaml` (`severity-dot.error/.warning/.info`).
- Brush resources `ErrorBrush`, `WarningBrush`, `InfoBrush` already defined.

Dependencies identified:
- `SemiStep.UI.csproj` is SDK-style (default compile globbing; `<Compile Update>` only for axaml code-behind).
  **New plain `.cs` files do NOT need manual csproj edits** (the "add to csproj manually" rule in the root
  CLAUDE.md applies to NtoLib, not SemiStep).
- Tests touching removed APIs: `SemiStep.Tests/UI/MessagePanelViewModelTests.cs`,
  `SemiStep.Tests/UI/Helpers/UIFixture.cs`, `SemiStep.Tests/UI/RecipeCoordinatorTests.cs:215`,
  `SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs:64,69`. `RecipeCoordinatorSaveGateTests.cs`
  uses `MessagePanelViewModel` (kept) and is unaffected.

## Development Approach

- **Testing approach**: Regular (code first, then tests within the same task).
- Complete each task fully before moving to the next; small, focused changes.
- **Every task includes new/updated tests** covering success and error/edge scenarios.
- **All tests must pass before starting the next task.**
- Build after each task: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`.
- No backward compatibility constraint — removed APIs are deleted outright, callers migrated in the same change.

## Testing Strategy

- **Unit tests**: required per task. Use `[AvaloniaFact]`/`[AvaloniaTheory]` (project convention for UI VMs),
  traits `[Trait("Component","UI")] [Trait("Area", "...")] [Trait("Category","Unit")]`.
- **No e2e harness** in this project — manual smoke verification is listed under Post-Completion.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with ➕ prefix; document blockers with ⚠️ prefix.
- Keep this file in sync with actual work.

## Solution Overview

Two channels, each one responsibility:

1. **Validation channel** — `MessagePanelViewModel` keeps `Entries`, counts, `HasErrors/HasWarnings/HasStatusErrors`,
   count texts, `StatusErrorSummary`, `ShowPanel/IsVisible/ToggleCommand`, and `RefreshReasons`. It loses
   `AddError/AddWarning/AddInfo`, `Clear`, `ClearCommand`. `MessageEntry` loses `IsStructural`/`StructuralSource`
   (and its `Source` field — every entry now originates from `RefreshReasons`, so the source is constant and the
   per-entry Source column is removed from the panel UI).

2. **Transient channel** — new `OperationStatusViewModel` holding a single nullable `OperationOutcome` that each
   `Report*` call overwrites. Replace-on-next is intrinsic; no collection, no timer.

`RecipeCoordinator.RebuildMessagePanel()` feeds only recipe reasons; the per-poll `RebuildMessagePanel()` call in
`OnPlcStateChanged` is removed. PLC state never touches either message channel.

The transient slot replaces `StatusText` in the status bar (Column 0). Document dirtiness is still shown via the
window-title asterisk, so dropping `StatusText` loses nothing.

### Naming decisions (called out, defaults chosen)

- New type names: `OperationStatusViewModel` and `OperationOutcome`.
- Namespace/folder: new folder `SemiStep.UI/OperationStatus/` with namespace `SemiStep.UI.OperationStatus`.
  `MessageSeverity` stays in `SemiStep.UI.MessageService` and is referenced via `using`.
- `MessageEntry.Source` is **removed** (panel always shows recipe-origin entries). If a future need arises to show
  origin, reintroduce explicitly — YAGNI for now.

## Technical Details

`OperationOutcome`:
```csharp
public sealed record OperationOutcome(MessageSeverity Severity, string Message);
```

`OperationStatusViewModel`:
```csharp
public sealed class OperationStatusViewModel : ReactiveObject
{
    private OperationOutcome? _current;
    public OperationOutcome? Current
    {
        get => _current;
        private set => this.RaiseAndSetIfChanged(ref _current, value);
    }

    public void ReportSuccess(string message)
    {
        Report(MessageSeverity.Info, message);
    }

    public void ReportWarning(string message)
    {
        Report(MessageSeverity.Warning, message);
    }

    public void ReportError(string message)
    {
        Report(MessageSeverity.Error, message);
    }

    private void Report(MessageSeverity severity, string message)
    {
        // marshal to UI thread (Dispatcher.UIThread.CheckAccess()/Post), mirror MessagePanelViewModel.PostOnUiThread
        Current = new OperationOutcome(severity, message);
    }
}
```

Status-bar slot binding (in `AppStatusBar.axaml`, `x:DataType` = `MainWindowViewModel`):
- `Text="{Binding OperationStatus.Current.Message}"`, `IsVisible` driven by `Current != null`
  (null-safe binding; an empty `Current` collapses the slot), single line with `TextTrimming="CharacterEllipsis"`.
- Foreground colored by severity. Mirror the existing style-class pattern: e.g. classes
  `op-status.error/.warning/.info` bound off the severity, or a small `IValueConverter` from
  `MessageSeverity` to brush. Prefer the style-class approach to stay consistent with `severity-dot`.

## What Goes Where

- **Implementation Steps** (checkboxes): all code, XAML, DI, and test changes in this repo.
- **Post-Completion** (no checkboxes): manual smoke verification of the new behavior in the running app.

## Implementation Steps

> **Ordering invariant (build-safety):** the removal of `MessagePanelViewModel`/`MessageEntry`/`StatusText`
> members happens **last** (Task 8), only after every producer, XAML binding, and test has been migrated off
> them. Each task below ends on a genuinely green build/test run. Do not move the removal earlier — the old
> `Add*`/`ClearCommand`/`Source`/`StatusText` members must stay alive while their consumers are migrated.

### Task 1: Create the transient channel (`OperationStatusViewModel`)

**Files:**
- Create: `SemiStep/SemiStep.UI/OperationStatus/OperationOutcome.cs`
- Create: `SemiStep/SemiStep.UI/OperationStatus/OperationStatusViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/OperationStatusViewModelTests.cs`

- [x] create `OperationOutcome` record (`MessageSeverity`, `Message`) in `SemiStep.UI.OperationStatus`
- [x] create `OperationStatusViewModel : ReactiveObject` with `Current` (private setter via `RaiseAndSetIfChanged`) and block-bodied `ReportSuccess/ReportWarning/ReportError` delegating to a private `Report`
- [x] implement UI-thread marshalling in the private `Report` (mirror `MessagePanelViewModel.PostOnUiThread`)
- [x] annotate the new test class with `[Trait("Component","UI")] [Trait("Area","OperationStatus")] [Trait("Category","Unit")]` so the filter below is non-empty
- [x] write `[AvaloniaFact]` tests: initial `Current` is `null`; each `Report*` sets `Current` with the correct `Severity`
- [x] write test: a second `Report*` replaces `Current` (latest-only, no accumulation — `Current` reflects only the last call)
- [x] run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=OperationStatus"` (confirm it selects >0 tests) — must pass before Task 2

### Task 2: Register the transient channel in DI

**Files:**
- Modify: `SemiStep/SemiStep.UI/UiDi.cs`

- [x] add `services.AddSingleton<OperationStatusViewModel>();` to `AddUi()`
- [x] add the `using SemiStep.UI.OperationStatus;` directive
- [x] build `SemiStep.UI` to confirm registration compiles (no test needed for a pure DI line; covered indirectly by later tasks)
- [x] run build — must pass before Task 3

### Task 3: Stop PLC state from writing to the validation panel

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`

- [x] change `RebuildMessagePanel()` to pass only `_lastRecipeResult.Reasons` to `RefreshReasons` (drop `_lastPlcState.Reasons` concat)
- [x] remove the `RebuildMessagePanel()` call from `OnPlcStateChanged` (kills per-second churn); keep `_lastPlcState`/`_plcStateChanged.OnNext` for the live indicators
- [x] ⚠️ verify the coordinator-internal PLC-failure paths that previously surfaced via the concat (the `RebuildMessagePanel()` callers around lines 216 and 246 — PLC load / conflict-resolution failures) still reach the user: confirm the failure `Result` returned to `MainWindowViewModel` is reported via `OperationStatus.ReportError` (Task 5), and document that. Do NOT silently drop PLC failure feedback.
  - Finding: Both failure paths write their reasons into `_lastRecipeResult` (not `_lastPlcState`) — `LoadRecipeFromPlcAsync` at line 212 (`_lastRecipeResult = result`) and `ResolveConflict` at line 242. After this change `RebuildMessagePanel()` still feeds `_lastRecipeResult.Reasons` to the panel, so those reasons are NOT dropped. Dropping only `_lastPlcState.Reasons` (which carried PLC connection/sync state, the per-poll churn) is the intended behavior. Additionally, `ResolveConflict` failures surface to the user through `MainWindowViewModel.HandleConflictAsync` line 183 (`MessagePanel.AddError(result.Errors[0].Message, "PLC")`) — one of the 4 transient sites Task 5 will route to `OperationStatus.ReportError`. `LoadRecipeFromPlcAsync` currently has NO caller in the solution (verified via grep), so its returned failure Result is not surfaced anywhere yet; that is a pre-existing condition unrelated to this change (the recipe reasons still appear in the panel regardless). No PLC failure becomes silent as a result of this task.
- [x] assess whether `_lastPlcState` is still read elsewhere; leave it if so, otherwise note for cleanup
  - Note for cleanup: after dropping the concat, `_lastPlcState` is assigned in `OnPlcStateChanged` (line 502) but never read anywhere. Live indicators read `_plc` directly and the observable is fed by `_plcStateChanged.OnNext`. Left in place per the build-safety ordering; flag for removal in a later cleanup task if desired.
- [x] write/adjust a coordinator test asserting that a PLC state change does NOT add entries to the panel (panel reflects only recipe reasons)
- [x] run tests: `dotnet test ... --filter "FullyQualifiedName~RecipeCoordinator"` — must pass before Task 4

### Task 4: Migrate File + Clipboard + RecipeGrid call sites to the transient channel

*(Old `Add*` members still exist here — build stays green.)*

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`

- [x] `RecipeFileViewModel`: swap constructor dependency `MessagePanelViewModel` → `OperationStatusViewModel`; map failure `AddError` → `ReportError`, the 2 success `AddInfo` ("Saved:"/"Loaded:") → `ReportSuccess`
- [x] `ClipboardViewModel`: swap dependency; map all 5 `AddError` sites → `ReportError`
- [x] `RecipeGridViewModel`: swap dependency; map both `AddError` sites (step edit error, unknown action) → `ReportError`
- [x] update `using` directives in all three
- [x] write/adjust unit tests for each VM's outcome reporting (success + error) against `OperationStatusViewModel.Current`
- [x] run tests for affected areas — must pass before Task 5

### Task 5: Update `MainWindowViewModel` (hold both channels)

*(Keep the `StatusText` property for now — it is removed in Task 8 together with the XAML binding from Task 6.)*

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [x] add constructor param `OperationStatusViewModel` and expose it as `public OperationStatusViewModel OperationStatus { get; }`; keep the existing `MessagePanel` property
- [x] map the 4 transient `AddError` sites (sync-toggle `ThrownExceptions`, `EnableSync` fail, conflict-dialog fail, `ResolveConflict` fail) → `OperationStatus.ReportError`
- [x] leave `StatusText` and its `RaisePropertyChanged(nameof(StatusText))` in place (removed in Task 8)
- [x] write/adjust tests covering the migrated PLC error reporting (asserts `OperationStatus.Current` severity/message)
- [x] run tests — must pass before Task 6

### Task 6: Wire the transient slot into the status bar and clean the log panel UI

*(This removes the XAML bindings to `StatusText`, `ClearCommand`, and per-entry `Source`, so the underlying members become dead code, ready for removal in Task 8. The members still exist now, so the build stays green.)*

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml`

- [x] in `AppStatusBar.axaml` Column 0, replace the `StatusText` TextBlock with the transient slot bound to `OperationStatus.Current.Message`, visibility on `Current != null`, `TextTrimming="CharacterEllipsis"`
- [x] add severity → foreground styling (style classes `op-status.error/.warning/.info` mirroring `severity-dot`, or a converter) using existing `ErrorBrush`/`WarningBrush`/`InfoBrush`
- [x] in `MessagePanel.axaml` remove the "Clear" button (bound to `ClearCommand`) and the per-entry `Source` column; change the entry template `Grid ColumnDefinitions="16,Auto,*"` to two columns (`16,*`) and re-index the message `TextBlock` from `Grid.Column="2"` to `Grid.Column="1"`
- [x] build `SemiStep.UI` and confirm XAML compiles (bindings now reference only members that still exist)
- [x] run build — must pass before Task 7

### Task 7: Rewrite affected tests onto the new model (old APIs still present)

*(Rewrite tests so they no longer reference `Add*`/`ClearCommand`/`IsStructural`/`Source`; this makes Task 8's removal safe. The old members still compile here, so this task ends green.)*

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs`

- [x] `UIFixture`: construct an `OperationStatusViewModel` and pass it to any VM whose constructor now requires it; expose it if tests need to assert outcomes
- [x] `MessagePanelViewModelTests`: delete cases for `AddError`/`AddInfo`/`Clear`/`ClearCommand`/`IsStructural`/non-structural; keep/adapt `RefreshReasons` (counts, replace-old-on-refresh, severity), `StatusErrorSummary` (now seeded only via `RefreshReasons`), `ShowPanel`
- [x] `RecipeCoordinatorTests.cs:215`: replace the `!e.IsStructural` assertion with the new model (panel holds only recipe reasons)
- [x] `RecipeCoordinatorLoadRecipeTests.cs`: rewrite `LoadRecipeAsync_Failure_LeavesPanelIntact` — seed panel state via `RefreshReasons` (not the removed `AddError`), drop `IsStructural`/`Source` assertions (incl. the `e.Source == "Test"` check), assert a failed load does not perturb existing validation reasons
- [x] grep the test project for any remaining references to `AddError`/`AddInfo`/`AddWarning`/`ClearCommand`/`IsStructural`/`StructuralSource`/`.Source` on entries and fix
- [x] run the full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must pass before Task 8

### Task 8: Remove the now-dead validation-channel APIs

*(Everything below is unreferenced after Tasks 4–7, so removal compiles green.)*

**Files:**
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MessageService/MessageEntry.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [x] remove `AddError`, `AddWarning`, `AddInfo`, `Clear`, `ClearCommand` (and the `ClearNonStructural` helper) from `MessagePanelViewModel`
- [x] remove `IsStructural`, `StructuralSource`, and the `Source` field from `MessageEntry`; update its construction in `RefreshReasons`
- [x] simplify `RefreshReasons`/`RemoveByPredicate` so `RefreshReasons` clears all entries then re-adds from reasons (no structural/non-structural split)
- [x] remove the `StatusText` property and its `RaisePropertyChanged(nameof(StatusText))` from `MainWindowViewModel`
- [x] verify remaining `MessagePanelViewModel` surface still compiles and behaves: `Entries`, counts, `Has*`, count texts, `StatusErrorSummary`, `ShowPanel/IsVisible/ToggleCommand`, `RefreshReasons`
- [x] run the full test suite — must pass before Task 9

### Task 9: Verify acceptance criteria

- [x] verify transient outcomes never accumulate (single slot, latest-only) and never feed the badge — `OperationStatusViewModel` holds a single nullable `Current` (no collection); `MessagePanelViewModel` counts come only from `RefreshReasons`→`RecountAndNotify` feeding `HasStatusErrors`/`StatusErrorSummary`; proven by `OperationStatusViewModelTests.SecondReport_ReplacesCurrent_LatestOnly`
- [x] verify validation badge/list reflects only recipe reasons and self-heals — `RecipeCoordinator.RebuildMessagePanel` (line 538-540) feeds only `_lastRecipeResult.Reasons`; `RecipeCoordinatorTests.NewRecipe_RebuildsPanelFromFreshRecipeReasons` shows entries clear when reasons clear (self-heal)
- [x] verify PLC Failed/Disconnected produces no message-channel writes (only the dot + `PlcSyncStatusText` change) — `OnPlcStateChanged` (line 491-505) only sets `_lastPlcState` and `_plcStateChanged.OnNext`, no `RebuildMessagePanel`; proven by `RecipeCoordinatorTests.PlcStateChange_Failure_DoesNotAddEntriesToMessagePanel`
- [x] verify success is reported uniformly for File/Clipboard/RecipeGrid operations via the transient slot — all three VMs call `OperationStatus.ReportSuccess/ReportError`; covered by `OperationStatusReportingTests` (RecipeFile_Save_ReportsSuccess, RecipeFile_LoadMissingFile_ReportsError, RecipeGrid_InvalidCellEdit_ReportsError, Clipboard_PasteInvalidContent_ReportsError)
- [x] run full test suite and `dotnet format SemiStep/SemiStep.slnx` — 663 passed / 0 failed; format `--verify-no-changes` reported no changes needed
- [x] build the UI project: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` — succeeded, 0 warnings / 0 errors

### Task 10: [Final] Documentation and plan archival

**Files:**
- Modify: `SemiStep/CLAUDE.md` (only if a durable convention emerges, e.g. "transient outcomes go through `OperationStatusViewModel`, validation through `MessagePanelViewModel.RefreshReasons`")

- [x] update `SemiStep/CLAUDE.md` if the two-channel split is a convention worth recording
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion

*Manual verification — no checkboxes, informational only.*

- Run the app (`dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj`) and smoke-test:
  - Save/Load → success text appears in the left slot and is replaced by the next operation.
  - Trigger a paste/edit failure → error text appears; performing a successful op replaces it (no pile-up).
  - Make the recipe invalid → badge/log shows validation reasons; fix it → they self-heal.
  - Enable sync with PLC down → only the connection dot + sync label change; the message channels stay quiet.
- Confirm the window-title asterisk still conveys dirty state now that `StatusText` is gone.
