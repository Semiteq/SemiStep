# Exit Menu Dirty-Close Guard (#123)

## Overview
`File > Exit` bypasses the dirty-close confirmation and loses unsaved recipes silently.
`MainWindowViewModel.ExitCommand` calls `DesktopShutdownService.Shutdown()`, which maps to
`IClassicDesktopStyleApplicationLifetime.Shutdown()` — the forced variant that raises the window
`Closing` event but ignores `e.Cancel`. The dirty guard in `MainWindow.OnWindowClosing` (added in
#107/#124) therefore never gets to hold the close.

This change routes the menu Exit through `MainWindow.Close()` so the existing, already-tested guard
runs. Under the default `ShutdownMode.OnLastWindowClose`, closing the single main window terminates
the application, so behaviour for a clean session is unchanged.

Scope is strictly the `File > Exit` menu path. The second consumer of
`DesktopShutdownService.Shutdown()` — `RestartPromptDialog.OnExitNowClick` — is a distinct scenario
(style-editor restart) and is deferred to a separate follow-up issue, per one-PR-one-change.

## Context (from discovery)
- Files/components involved:
  - `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` — `ExecuteExit()` (`:159`), holds
    `Window? MainWindow` (`:92`).
  - `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` — `OnWindowClosing` guard (`:208`).
  - `SemiStep/SemiStep.UI/ShutdownService/DesktopShutdownService.cs` — static forced shutdown.
  - `SemiStep/SemiStep.Tests/UI/MainWindow/MainWindowExitFlowTests.cs` — existing headless coverage.
- Related patterns found:
  - Existing tests drive `_window.Close()` and assert `_window.IsVisible`; the dirty guard shows an
    `ExitConfirmationDialog` and honours Cancel/DontSave/Save.
  - `MainWindowViewModel` already owns the `MainWindow` reference and news up views (known MVVM debt,
    issue #114) — calling `MainWindow.Close()` is consistent with the current pattern.
- Dependencies identified:
  - Default `ShutdownMode` (not set anywhere) = `OnLastWindowClose`, so `Close()` on the last window
    cascades to app exit.
  - Headless test app (`TestAppBuilder`) installs no classic-desktop lifetime, so the current
    `Shutdown()` call is a no-op under test — the `Close()` route is what makes the path testable.

## Development Approach
- **testing approach**: Regular (code change first, then headless tests), matching the existing
  exit-flow test file.
- Small, focused change: one production method redirected, tests added.
- Every code change ships with tests in the same task.
- All tests must pass (and `dotnet format --verify-no-changes` must be clean) before finishing.
- Maintain backward compatibility: clean-session exit still closes the app.

## Testing Strategy
- **unit/integration tests**: headless `[AvaloniaFact]` in `MainWindowExitFlowTests`.
  - dirty session + `ExitCommand.Execute()` → window stays open, `ExitConfirmationDialog` shown.
  - clean session + `ExitCommand.Execute()` → window closes.
- **e2e tests**: none in this project.

## Progress Tracking
- mark completed items `[x]` immediately
- ➕ prefix for newly discovered tasks
- ⚠️ prefix for blockers

## Solution Overview
Redirect `ExecuteExit` from the forced static shutdown to `MainWindow.Close()`:

- `ExecuteExit` becomes an instance method that calls `MainWindow?.Close()`.
- The window's `OnWindowClosing` guard (already covered by #124) then decides:
  clean → closes → `OnLastWindowClose` exits the app; dirty → cancels and shows the confirmation
  dialog.
- `DesktopShutdownService` is left in place; it is still used by `RestartPromptDialog` (out of scope),
  so it is not deleted in this PR.

Rationale over `TryShutdown()`: `Close()` reuses the window guard that is already tested and is
observable under the headless harness (which installs no classic-desktop lifetime), whereas a
`TryShutdown()` change would be a no-op in headless and untestable here.

## Technical Details
- `ExecuteExit()` signature: `private static void` → `private void`; body
  `DesktopShutdownService.Shutdown();` → `MainWindow?.Close();`.
- `ReactiveCommand.Create(ExecuteExit)` binding is unaffected (instance method group).
- If `MainWindow` is null (never wired), Exit is a no-op — acceptable; in production the property is
  set in `MainWindow.WhenActivated`.
- No change to the `using` set is required beyond possibly dropping the now-unused
  `SemiStep.UI.ShutdownService` import if no other reference remains in the file (verify: the file
  references `DesktopShutdownService` only in `ExecuteExit`; the import can be removed if unused).

## What Goes Where
- **Implementation Steps**: production redirect + headless tests + follow-up issue creation prep.
- **Post-Completion**: manual smoke of the real desktop app (real lifetime), and the separate
  RestartPromptDialog issue/PR.

## Implementation Steps

### Task 1: Route menu Exit through the window close guard

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [x] change `ExecuteExit` from `private static void` to `private void`, body `MainWindow?.Close();`
- [x] remove the now-unused `using SemiStep.UI.ShutdownService;` if no other reference remains in the file
- [x] build `SemiStep/SemiStep.UI/SemiStep.UI.csproj` — must compile clean

### Task 2: Headless coverage for the ExitCommand path

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindow/MainWindowExitFlowTests.cs`

- [x] add `ExitCommand_DirtySession_DoesNotCloseAndShowsConfirmation`: dirty session,
      `await _viewModel.ExitCommand.Execute();` → `_window.IsVisible` true and a single
      `ExitConfirmationDialog` present; then `dialog.Close(ExitConfirmationResult.Cancel)` +
      `Dispatcher.UIThread.RunJobs()` to avoid a dangling headless dialog in teardown
      (mirror `WindowClose_DirtySession_IsCancelledAndDialogCancelKeepsWindowOpen`)
- [x] add `ExitCommand_CleanSession_ClosesWindow`: clean session,
      `await _viewModel.ExitCommand.Execute();` → `_window.IsVisible` false (name expresses the
      command→`Close()` wiring intent, distinct from `WindowClose_CleanSession_Closes`)
- [x] the command is a cold observable — MUST `await ...Execute()`, not call `Execute()` bare
- [x] run the exit-flow tests filtered — must pass
- [x] run tests — must pass before next task

### Task 3: Verify acceptance criteria
- [x] dirty Exit does not close without confirmation; clean Exit closes — asserted by Task 2 tests
- [x] run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] `dotnet format --verify-no-changes SemiStep/SemiStep.slnx` clean

### Task 4: [Final] Commit, PR, follow-up issue, move plan
- [x] commit on branch `exit-menu-dirty-guard`
- [x] push and open PR to `master` (closes #123)
- [x] open follow-up issue for `RestartPromptDialog.OnExitNowClick` forced-shutdown data-loss path
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
*Informational only — no checkboxes*

**Manual verification:**
- Run the real desktop build, make the recipe dirty, choose `File > Exit`: the confirmation dialog
  must appear; Save/DontSave/Cancel behave as with the window X. Clean session `File > Exit` exits.

**External / follow-up:**
- Separate issue + PR for `RestartPromptDialog` (style-editor "Exit Now") which still calls the
  forced `DesktopShutdownService.Shutdown()` and can drop unsaved recipe changes.
