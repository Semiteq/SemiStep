# Unify Message System into the Message Panel

## Overview
Collapse the two parallel user-facing message APIs into one. Today operation outcomes
(success/failure of save/load/paste/edit/sync) flow through `OperationStatusViewModel` and
render in a single-slot status-bar widget, while structural validation reasons flow through
`MessagePanelViewModel.RefreshReasons` and render in the collapsible message panel.

This change:
- Folds `OperationStatusViewModel` into `MessagePanelViewModel` so there is one reporting API
  and one visual home (the panel). The status-bar transient slot is removed.
- Keeps the two **lifecycles** distinct internally (industry consensus — derived diagnostics vs.
  point-in-time events cannot share one TTL governor), but presents them in one `Entries` list.
- Governs the operation outcome by "clear on the next successful state change", defined precisely
  as the next `DispatchMutation` (which fires only on a successful mutation), not `RebuildMessagePanel`.
- Applies **Option C** for panel auto-open: operation errors/warnings make the panel appear;
  operation successes stay quiet (visible only if the panel is already open).

Problem it solves: the current split surprises the user (panel looks empty because everything
lands transiently in the status bar), and maintaining two APIs/VMs is more machinery than the
feature needs.

## Context (from discovery)
Files/components involved:
- `SemiStep/SemiStep.UI/OperationStatus/OperationStatusViewModel.cs` — to be deleted (folded in)
- `SemiStep/SemiStep.UI/OperationStatus/OperationOutcome.cs` — to be deleted (reuse `MessageEntry`)
- `SemiStep/SemiStep.UI/OperationStatus/MessageSeverityToBrushConverter.cs` — dead once the slot is removed, to be deleted
- `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs` — gains the operation slot + unified API
- `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml` — add Info/neutral severity-dot handling for success rows
- `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — drop `OperationStatusViewModel` dep; clear operation slot on successful mutation; reconnect site reports to panel
- `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml` — remove transient slot (cols 0-1) and the converter resource
- `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` — drop `OperationStatus` property/dep; report to panel
- `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`, `RecipeFile/RecipeFileViewModel.cs`, `RecipeGrid/RecipeGridViewModel.cs` — swap `_operationStatus` -> `_messagePanel`
- `SemiStep/SemiStep.UI/UiDi.cs` — remove `AddSingleton<OperationStatusViewModel>()`

Test files referencing `OperationStatus` / the converter (14 — confirmed by grep), grouped by the
production change that breaks each so test fixes land in the SAME task:
- shared seam: `SemiStep.Tests/UI/Helpers/UIFixture.cs`
- coordinator ctor change: `RecipeCoordinatorTests.cs`, `RecipeCoordinatorLoadRecipeTests.cs`, `RecipeCoordinatorSaveGateTests.cs`, `OperationStatusReportingTests.cs`
- RecipeGridViewModel ctor change: `RecipeGridViewModelTests.cs`, `RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`, `RecipeGrid/RecipeGridViewModelReadOnlyTests.cs`, `RecipeGrid/RecipeRowForDepthPropagationTests.cs`
- Clipboard/RecipeFile/MainWindow ctor change: `Clipboard/ClipboardViewModelCanExecuteTests.cs`, `RecipeFile/RecipeFileViewModelCanExecuteTests.cs`, `MainWindowViewModelReportingTests.cs`
- type deletion: `OperationStatusViewModelTests.cs` (rewrite/fold), `MessageSeverityToBrushConverterTests.cs` (delete)

Related patterns found:
- `MessagePanelViewModel.RefreshReasons` does `Entries.Clear()` then rebuilds — the root reason a
  shared list is impossible today; restructured so no single source clears the public list.
- `RecipeCoordinator.DispatchMutation` (line ~454) has TWO paths: a direct call when
  `Dispatcher.UIThread.CheckAccess()` is true, and a `Post` otherwise; both route through
  `RaiseMutatedSafely`. The operation-clear must run on BOTH paths — place it in `RaiseMutatedSafely`.
- `RecipeCoordinator.RebuildMessagePanel` runs on both success and rejection paths, so it must NOT
  be the operation-clear trigger.
- `MessageSeverity` is `{ Error, Warning, Info }`; operation success uses `Info`.

Convention notes (verified):
- `SemiStep.UI.csproj` is SDK-style with implicit compile globbing — there are NO `<Compile Include>`
  entries. New `.cs` files are picked up automatically; do NOT add explicit entries (causes duplicate-item errors).
- One class per file, file-scoped namespaces, no transitional comments.

## Development Approach
- **testing approach**: Regular (code first, then tests) — matches repo convention.
- complete each task fully before the next; small, focused changes.
- **every task leaves the build green and all tests passing** — production signature changes are
  bundled with the test files they break (see grouping above).
- run tests after each change; `dotnet format` before commit (pre-commit hook enforces it).

## Testing Strategy
- **unit tests**: required per task. xUnit v3 + Avalonia headless (`[AvaloniaFact]`/`[AvaloniaTheory]`).
  Command: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- **e2e tests**: none; UI is exercised via Avalonia headless unit tests.

## Progress Tracking
- mark completed items `[x]` immediately; add ➕ for newly discovered tasks; ⚠️ for blockers.
- update this plan if scope shifts during implementation.

## Solution Overview
`MessagePanelViewModel` owns two private sources and one public collection:
- `_validationEntries` (list) — derived from `Snapshot.Reasons`, self-healing, replace-on-refresh.
- `_operationEntry` (single nullable `MessageEntry`) — latest-only operation outcome.
- public `Entries` is reassembled by a private `Rebuild()` from both sources (operation row first).

`RefreshReasons` only ever rewrites `_validationEntries`; it never clears `Entries` directly.
The new `ReportSuccess/Warning/Error` (same names as today's API) write `_operationEntry`.
`RecipeCoordinator` clears `_operationEntry` from `RaiseMutatedSafely` (reached on every successful
`DispatchMutation`, both threading paths) so a stale outcome disappears on the next successful state
change, while a rejected mutation — which returns before `DispatchMutation` — keeps its error.

Counts (`ErrorCount`/`WarningCount`, and therefore the status-bar badge) derive from
`_validationEntries` only. `HasEntries` (drives `ShowPanel`) is true when validation entries exist OR
the operation slot holds an error/warning — implementing Option C (successes do not pop the panel).

`MessageKind` is intentionally NOT introduced: nothing would consume it (counts read
`_validationEntries`, Option C reads the slot, the dot style is severity-driven). YAGNI.

## Technical Details
- `Rebuild()`: `Entries.Clear(); if (_operationEntry is not null) Entries.Add(_operationEntry); foreach validation -> Add;`
  then `RecountAndNotify()`. UI-thread via the existing `PostOnUiThread`.
- `RecountAndNotify()`: `ErrorCount`/`WarningCount` from `_validationEntries` by severity;
  `HasEntries = _validationEntries.Count > 0 || _operationEntry is { Severity: MessageSeverity.Error or MessageSeverity.Warning }`.
- Operation success uses `MessageSeverity.Info`. The panel's `severity-dot` classes cover only
  `error`/`warning`; add an `info`/neutral style so a success row renders sensibly.
- After the slot is removed, `MessageSeverityToBrushConverter` has no consumer and is deleted.

## What Goes Where
- **Implementation Steps**: all code, XAML, test, and doc changes below.
- **Post-Completion**: manual UI smoke test of the panel pop/clear behavior in the running app.

## Implementation Steps

### Task 1: Add the operation slot and unified API to MessagePanelViewModel

**Files:**
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelViewModelTests.cs`

- [x] introduce `_validationEntries` (List) and `_operationEntry` (nullable `MessageEntry`); make public `Entries` assembled by a private `Rebuild()`
- [x] rewrite `RefreshReasons` to rewrite only `_validationEntries` then `Rebuild()` (no direct `Entries.Clear()`)
- [x] add `ReportSuccess`/`ReportWarning`/`ReportError` and a private `ReportOperation(severity, message)` that set `_operationEntry` and `Rebuild()` (UI-thread marshalled); add `ClearOperation()` that nulls the slot and `Rebuild()`s
- [x] make `RecountAndNotify` count `_validationEntries` only; set `HasEntries = validation>0 || operation is error/warning`
- [x] add an `info`/neutral `severity-dot` style in `MessagePanel.axaml` for success/Info rows
- [x] write tests: `RefreshReasons` preserves an operation entry; operation report shows as a row (latest-only replace); counts ignore operation entries; `ClearOperation` removes only the operation row; Info/success does NOT set `HasEntries`
- [x] run tests — must pass before next task

### Task 2: Rewire RecipeCoordinator (drop OperationStatus dep, clear-on-success) and fix coordinator tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorSaveGateTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/OperationStatusReportingTests.cs`

- [x] remove the `OperationStatusViewModel` ctor parameter, `_operationStatus` field, and `using` from `RecipeCoordinator`
- [x] change the reconnect-apply failure site (~line 412) to `_messagePanel.ReportError(...)`
- [x] call `_messagePanel.ClearOperation()` inside `RaiseMutatedSafely` (covers both `DispatchMutation` threading paths) so the slot clears only on a successful mutation
- [x] update `UIFixture`: drop the `OperationStatus` property and the `OperationStatusViewModel` ctor argument
- [x] update the three `RecipeCoordinator*Tests` and `OperationStatusReportingTests` to assert via `MessagePanel` (operation rows / counts) instead of `OperationStatus.Current`; include a test for the reconnect-apply failure reporting path
- [x] add tests: a successful mutation clears a prior operation message; a rejected edit keeps its operation error
- [x] run tests — must pass before next task

### Task 3: Swap RecipeGridViewModel to the panel and fix its tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeGridViewModelReadOnlyTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeRowForDepthPropagationTests.cs`

- [x] replace `OperationStatusViewModel _operationStatus` ctor param/field with `MessagePanelViewModel _messagePanel`; fix `using`
- [x] update the `_operationStatus.Report*` calls (~204/225/445) to `_messagePanel.Report*`
- [x] update the four RecipeGrid test files to construct the VM with `MessagePanel` and assert accordingly
- [x] add/adjust a test that a rejected cell edit reports an operation error to the panel
- [x] run tests — must pass before next task

### Task 4: Swap Clipboard/RecipeFile/MainWindow VMs, remove the status-bar slot, fix their tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/Clipboard/ClipboardViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelReportingTests.cs`

- [x] swap `_operationStatus` -> `_messagePanel` in `ClipboardViewModel` and `RecipeFileViewModel` (update all `Report*` sites; fix `using`)
- [x] in `MainWindowViewModel`: remove the `OperationStatus` property and ctor param; route its three `ReportError` sites and the `ToggleSyncCommand.ThrownExceptions` handler to `MessagePanel`
- [x] remove the transient slot `TextBlock` (col 0) + separator (col 1), the `MessageSeverityToBrushConverter` resource, and the `converters` namespace from `AppStatusBar.axaml`; renumber remaining grid columns (this must land with the `MainWindowViewModel.OperationStatus` removal — compiled bindings reference it)
- [x] update the three test files to construct the VMs with `MessagePanel` and assert via panel rows
- [x] run tests — must pass before next task

### Task 5: Delete the dead Operation types and DI registration

**Files:**
- Delete: `SemiStep/SemiStep.UI/OperationStatus/OperationStatusViewModel.cs`
- Delete: `SemiStep/SemiStep.UI/OperationStatus/OperationOutcome.cs`
- Delete: `SemiStep/SemiStep.UI/OperationStatus/MessageSeverityToBrushConverter.cs`
- Delete: `SemiStep/SemiStep.Tests/UI/MessageSeverityToBrushConverterTests.cs`
- Modify: `SemiStep/SemiStep.UI/UiDi.cs`
- Modify/Delete: `SemiStep/SemiStep.Tests/UI/OperationStatusViewModelTests.cs`

- [x] remove `services.AddSingleton<OperationStatusViewModel>()` and the unused `using` from `UiDi.cs`
- [x] delete the three production Operation* files (no csproj edits needed — implicit globbing); remove the `OperationStatus` folder if empty
- [x] delete `MessageSeverityToBrushConverterTests.cs`
- [x] rewrite `OperationStatusViewModelTests.cs` against `MessagePanelViewModel.Report*` (latest-only replace, severity mapping) or fold its cases into `MessagePanelViewModelTests` and delete the file
- [x] build the solution — must compile clean
- [x] run tests — must pass before next task

### Task 6: Verify acceptance criteria
- [x] all messages render in the panel; status bar no longer has a transient slot — confirmed: `AppStatusBar.axaml` has no `OperationStatus` text slot (only badge/sync/execution/connection); `MessagePanelViewModel` is the sole sink exposing `ReportSuccess/Warning/Error` + `RefreshReasons`
- [x] operation error/warning pops the panel; success stays quiet unless already open (Option C) — confirmed: `RecountAndNotify` sets `HasEntries = validation>0 || operation is Error/Warning`, `ShowPanel = HasEntries && IsVisible`; tests `ReportSuccess_DoesNotSetHasEntries` + `OperationErrorOrWarning_SetsHasEntries` (MessagePanelViewModelTests.cs)
- [x] a stale operation message clears on the next successful mutation/load/save; a rejected edit keeps its error — confirmed: `RecipeCoordinator.RaiseMutatedSafely` calls `_messagePanel.ClearOperation()`; tests `SuccessfulMutation_ClearsPriorOperationMessage` + `RejectedMutation_KeepsPriorOperationError` (RecipeCoordinatorTests.cs) and `LoadRecipeAsync_Success_ClearsMessagePanelBeforeAddingNewReasons` (RecipeCoordinatorLoadRecipeTests.cs)
- [x] validation reasons still self-heal and the count badge reflects validation only — confirmed: `ErrorCount`/`WarningCount` count `_validationEntries` only, `RefreshReasons` rewrites `_validationEntries` from snapshot reasons; test `SuccessfulMutation_StructuralWarning_SurfacesInPanelAndSelfHeals` (RecipeCoordinatorTests.cs)
- [x] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 0 failed, 675 passed
- [x] `dotnet format SemiStep/SemiStep.slnx` — ran clean; no source files reformatted

### Task 7: Update documentation
- [x] rewrite the "Status-bar message channels (two-channel split)" section in `SemiStep/CLAUDE.md` to describe the unified panel, the operation-slot lifecycle (clear-on-next-successful-mutation), Option C auto-open, and validation-only counts
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
*Manual, outside the automated suite — informational only*

**Manual verification:**
- Run the app (`dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj`) and confirm:
  - saving shows a quiet success row (panel does not auto-open if collapsed)
  - a rejected cell edit pops the panel with the error and the error survives until the next valid edit
  - introducing then fixing a structural defect shows/heals validation rows and updates the badge
  - the status bar shows only sync/connection/execution indicators (no transient text slot)
