# Fix #126: RestartPromptDialog 'Exit Now' Bypasses the Dirty-Close Guard

## Overview

`RestartPromptDialog.OnExitNowClick` performs a forced `IClassicDesktopStyleApplicationLifetime.Shutdown()`
via `DesktopShutdownService.Shutdown()`. This skips `MainWindow.OnWindowClosing`, so a user with unsaved
recipe changes who opens the grid style editor, changes a restart-requiring setting, and clicks "Exit Now"
loses the recipe silently. Same data-loss class as #107/#123/#124, reached through a third exit path;
deliberately deferred from #123 to keep one PR one change.

**Fix:** route the "Exit Now" intent back up the owner chain to `MainWindow.Close()` — the single sanctioned
exit path (`Docs/architecture/exit-flow.md`) — instead of forcing a shutdown that bypasses the guard. The
restart dialog returns a decision; the style-editor window closes itself, then closes its owner
(`MainWindow`), whose `OnWindowClosing` runs the dirty guard. Under the default
`ShutdownMode.OnLastWindowClose`, closing the last window cascades to process exit.

Benefits:
- Unsaved changes trigger the `ExitConfirmationDialog` (Save / Don't Save / Cancel) on this path too.
- Removes the last forced-shutdown call site; `DesktopShutdownService` becomes dead code and is deleted,
  eliminating the footgun.
- The routing is observable in the headless harness (plain `Window.Close()` calls, no lifecycle shutdown),
  so a real regression test becomes feasible — unlike the `TryShutdown()` alternative.

## Context (from discovery)

Ownership chain when "Exit Now" is clicked:
1. `MainWindowViewModel.ExecuteOpenStyleEditorAsync` (`MainWindow/MainWindowViewModel.cs:179-180`) —
   `new GridStyleEditorWindow(...); await window.ShowDialog(MainWindow)` → `editor.Owner == MainWindow`.
2. `GridStyleEditorWindow.OnSaveCompleted` (`StyleEditor/GridStyleEditorWindow.axaml.cs:30-40`) —
   `new RestartPromptDialog(); await dialog.ShowDialog(this); Close()` → `restartDialog.Owner == editor`.
   The editor itself is opened via `new GridStyleEditorWindow { DataContext = viewModel }`.
3. `RestartPromptDialog.OnExitNowClick` (`StyleEditor/RestartPromptDialog.axaml.cs:15-19`) —
   `Close(); DesktopShutdownService.Shutdown()` → forced shutdown, bypasses guard.

Files/components involved:
- `SemiStep/SemiStep.UI/StyleEditor/RestartPromptDialog.axaml.cs` — the offending forced shutdown.
- `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml.cs` — owns the restart dialog; where the
  routing to `owner.Close()` lands.
- `SemiStep/SemiStep.UI/ShutdownService/DesktopShutdownService.cs` — to be deleted (only caller removed).
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs:14` — `using SemiStep.UI.ShutdownService;` **stays**.
  `ExitConfirmationResult` and `ExitConfirmationDialog` live in `SemiStep.UI.ShutdownService`, and
  `MainWindow.axaml.cs` uses both (lines ~239-240, ~250) while never referencing `DesktopShutdownService`.
  So the using is unconditionally required; it was never about the deleted service. Do NOT remove it and do
  NOT delete the `ShutdownService/` folder.
- `Docs/architecture/exit-flow.md` — rewrite "Known gap" (gap closed) and the "forced variant" section
  (no forced-shutdown path remains).

Related patterns found:
- Sanctioned exit path documented in `Docs/architecture/exit-flow.md`: every ordinary exit routes through
  `MainWindow.Close()`; the guard sits in `MainWindow.OnWindowClosing`.
- Test seams in `SemiStep.Tests/UI/MainWindow/MainWindowExitFlowTests.cs`: `_window.OwnedWindows.OfType<T>()`
  to grab a headless dialog, `dialog.Close(result)` to resolve it, `Dispatcher.UIThread.RunJobs()` to flush
  the fire-and-forget `OnWindowClosing` continuation. `[AvaloniaFact]` lifecycle.
- `GridStyleEditorWindowTests.cs` drives `SaveCommand` directly, but does not currently mount the window
  under an owner.

Dependencies identified:
- Avalonia `Window.ShowDialog<TResult>` for the dialog's return value.
- `ShutdownMode.OnLastWindowClose` (never set explicitly) makes `MainWindow.Close()` terminate the app.

Namespace note (verified): `ExitConfirmationResult`, `ExitConfirmationDialog`, and `DesktopShutdownService`
all live under `SemiStep.UI.ShutdownService`. Deleting only `DesktopShutdownService.cs` leaves the namespace
and folder populated, so the `using` in `MainWindow.axaml.cs` stays and the `ShutdownService/` folder is NOT
removed.

## Development Approach

- **testing approach**: Regular (code first, then tests) — matches the existing exit-flow test style.
- complete each task fully before moving to the next.
- make small, focused changes; one logical change per commit.
- **every task with a code change includes new/updated tests** (success + error/edge scenarios).
- **all tests pass before starting the next task.**
- update this plan file if scope changes during implementation.
- maintain backward compatibility for the "Restart Later" path (must still just close the dialog, no exit).

## Testing Strategy

- **unit / integration tests** (`[AvaloniaFact]`, headless): required per task.
  - `RestartPromptDialog` returns the correct decision from each button (`Close(true)` / `Close(false)`).
  - `GridStyleEditorWindow` routes an exit request to `Owner.Close()`; a "Restart Later" request does not.
  - The routed close runs the dirty guard: an owner with a dirty session shows the `ExitConfirmationDialog`
    and stays open on Cancel; a clean owner closes.
- **e2e tests**: project has none (no Playwright/Cypress). Skip.
- **not headless-observable**: the true process shutdown (`ShutdownMode.OnLastWindowClose` cascade) is not
  reachable in the headless harness (it installs no classic-desktop lifetime). Cover this by manual
  verification (see Post-Completion), as the issue requests. The routing up to `MainWindow.Close()` IS
  observable and is what the automated tests assert.

## Progress Tracking

- mark completed items `[x]` immediately when done.
- add newly discovered tasks with ➕ prefix.
- document blockers with ⚠️ prefix.
- keep the plan in sync with actual work.

## Solution Overview

Replace the forced-shutdown call site with an intent that flows back through the owner chain:

- `RestartPromptDialog` becomes a pure decision dialog: "Exit Now" → `Close(true)`, "Restart Later" →
  `Close(false)`. It no longer references any shutdown service.
- `GridStyleEditorWindow.OnSaveCompleted` reads the decision via `ShowDialog<bool>`, **captures the owner
  before** closing itself, then, if exit was requested, closes that captured owner. Capturing first is
  mandatory: Avalonia clears the owned-window relationship during the close sequence, so reading `Owner`
  after `Close()` would evaluate to `null` and silently drop the exit intent.
- Closing `MainWindow` runs `OnWindowClosing` → dirty guard → `ExitConfirmationDialog` on a dirty session,
  or clean cascade to exit. Ordering (dialogs closed first, then owner closed) avoids the double-modal
  conflict that the `TryShutdown()` alternative would hit while the editor modal is still open.
- `DesktopShutdownService` is deleted (no remaining callers).

Key decisions:
- **Routing over `TryShutdown()`**: with the editor modal still open at click time, `TryShutdown()` would try
  to close `MainWindow` (its disabled modal owner) and show a second modal (`ExitConfirmationDialog`) over the
  same disabled parent — a conflict. Routing closes the modals first, then closes `MainWindow` cleanly.
- **Delete the service**: its only purpose was the forced bypass; keeping it invites reintroducing the bug.

## Technical Details

- `RestartPromptDialog.OnExitNowClick(object?, RoutedEventArgs)` → `Close(true)`.
- `RestartPromptDialog.OnRestartLaterClick(object?, RoutedEventArgs)` → `Close(false)` (unchanged behavior,
  now via the typed result).
- `GridStyleEditorWindow.OnSaveCompleted(bool saved)` — capture the owner BEFORE `Close()`:
  ```
  if (!saved) return;
  var dialog = new RestartPromptDialog();
  var exitRequested = await dialog.ShowDialog<bool>(this);
  var owner = Owner as Window;
  Close();
  if (exitRequested && owner is not null)
  {
      owner.Close();
  }
  ```
  Reading `Owner` after `Close()` would return `null` (Avalonia detaches the owned-window link on close) and
  drop the exit — the exact data-loss class this fix targets. Capture-before-close is required.
  `OnSaveCompleted` stays `async void` (its try/catch hardening is #113's scope, explicitly out of scope
  here; do not change its signature).
- Prefer extracting the "capture owner, close self, route to owner" body into an `internal` method (e.g.
  `internal void CompleteEditorClose(bool exitRequested)`), mirroring how `MainWindow.HandleExitChoiceAsync`
  is `internal` for direct testing. `OnSaveCompleted` then only awaits the dialog and calls the seam. This is
  what makes Task 2's tests deterministic rather than driving the private `async void` end to end.
- The `IsCancel="True"` "Restart Later" button dismisses via the title-bar/Escape too; ensure the dialog's
  default result for a dismissal is `false` (default(bool)) so a dismissed restart prompt never triggers an
  exit. Verify no `Close()`-without-result path remains that would yield a non-deterministic result.

## What Goes Where

- **Implementation Steps** (`[ ]`): dialog change, editor routing, service deletion, doc update, tests.
- **Post-Completion** (no checkboxes): manual verification of the real process-exit cascade, which the
  headless harness cannot exercise.

## Implementation Steps

### Task 1: Make RestartPromptDialog return an exit decision

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/RestartPromptDialog.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/StyleEditor/RestartPromptDialog.axaml` (add `x:Name` to the two buttons)
- Create: `SemiStep/SemiStep.Tests/UI/StyleEditor/RestartPromptDialogTests.cs`

- [x] change `OnExitNowClick` to `Close(true)`; remove the `DesktopShutdownService.Shutdown()` call.
- [x] change `OnRestartLaterClick` to `Close(false)`.
- [x] remove `using SemiStep.UI.ShutdownService;` from the dialog.
- [x] confirm dismissal paths (title-bar X / Escape via `IsCancel`) resolve `ShowDialog<bool>` to `false`.
- [x] add `x:Name="ExitNowButton"` / `x:Name="RestartLaterButton"` to the two buttons so headless tests can
      raise their `Click` without depending on localized `Content` (the handlers are `private`; the buttons
      are currently unnamed — "grab by name/content" is not otherwise possible).
- [x] new test file carries `[Trait("Component","UI")]`, `[Trait("Area","GridStyleEditor")]`,
      `[Trait("Category","Unit")]` so the gating filters below cover it.
- [x] write test: show an owner window, open the dialog non-awaited via `ShowDialog<bool>(owner)`, `RunJobs()`,
      raise the "Exit Now" button's `Click` → the returned `bool` is `true`.
- [x] write test: "Restart Later" button `Click` → returned `bool` is `false`; dialog dismissal (Close with no
      argument) → `default(bool)` == `false`.
- [x] run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=GridStyleEditor"` —
      must pass before Task 2.

### Task 2: Route the exit intent through the owner chain in GridStyleEditorWindow

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorWindowTests.cs`

- [x] extract an `internal void CompleteEditorClose(bool exitRequested)` that captures `Owner as Window`,
      calls `Close()`, then `owner?.Close()` when `exitRequested`. This is the primary test seam (mirrors
      `MainWindow.HandleExitChoiceAsync` being `internal`).
- [x] in `OnSaveCompleted`, read the decision `var exitRequested = await dialog.ShowDialog<bool>(this);` and
      call `CompleteEditorClose(exitRequested)`. Capture-before-close lives inside the seam.
- [x] keep `OnSaveCompleted` signature `async void` (do NOT add try/catch — that is #113's scope).
- [x] mark the new/updated tests `[Trait("Component","UI")]`, `[Trait("Area","GridStyleEditor")]`; the
      owner-routing tests that cross into `MainWindow` are `[Trait("Category","Integration")]` (the existing
      pure-VM tests stay `Category=Unit`).
- [x] write test (CLEAN owner): editor shown via `ShowDialog(mainWindow)` on a clean session; call
      `CompleteEditorClose(true)`; assert editor closes and `mainWindow.IsVisible == false` (clean cascade
      through the guard).
- [x] write test (DIRTY owner): dirty session (`RecipeTestDriver(...).AddWait(1f)`); `CompleteEditorClose(true)`
      → owner `OnWindowClosing` fires, `mainWindow.OwnedWindows.OfType<ExitConfirmationDialog>()` has one entry,
      `dialog.Close(Cancel)` + `RunJobs()`, `mainWindow.IsVisible == true` (guard honored, no silent loss).
- [x] write test (Restart Later): `CompleteEditorClose(false)` → editor closes, `mainWindow.IsVisible == true`
      (no exit routed).
- [x] ⚠️ driving the full private `async void OnSaveCompleted` end to end (execute `SaveCommand` → nested modal
      → grab from `OwnedWindows`) is the flaky path `headless-ui-testing.md` warns about; the `internal` seam
      above is deliberately the primary approach. NOTE: no full-drive smoke test added — the seam plus Task 1's
      `RestartPromptDialog` return-value tests cover the `ShowDialog<bool>` wiring; new tests live in a
      dedicated `GridStyleEditorWindowOwnerRoutingTests.cs` (IAsyncLifetime + UIFixture) and use the realistic
      `ShowDialog(mainWindow)` owner relationship, fired non-awaited.
- [x] run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=GridStyleEditor"` —
      must pass before Task 3.

### Task 3: Delete DesktopShutdownService and clean up references

**Files:**
- Delete: `SemiStep/SemiStep.UI/ShutdownService/DesktopShutdownService.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` (only if the `using` becomes unused)

- [x] grep the whole solution for `DesktopShutdownService` — confirm zero references after Tasks 1-2.
- [x] confirm whether `ExitConfirmationResult` / `ExitConfirmationDialog` share the `SemiStep.UI.ShutdownService`
      namespace/folder; if so, keep the folder and the `MainWindow.axaml.cs` using, delete only the one file.
- [x] delete `DesktopShutdownService.cs`.
- [x] remove the `SemiStep.UI.ShutdownService` using from `MainWindow.axaml.cs` ONLY if nothing else in that
      file needs it.
- [x] build the UI project: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` — must compile clean.
- [x] run the full UI test area to catch any broken reference:
      `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass before
      Task 4.

### Task 4: Update the exit-flow architecture doc

**Files:**
- Modify: `Docs/architecture/exit-flow.md`

- [x] rewrite the "Known gap" section (lines ~26-30): the restart-prompt "Exit Now" gap is closed; describe
      that it now routes through the owner chain to `MainWindow.Close()`.
- [x] update the "forced variant" section (lines ~20-24): no forced-shutdown call site remains; note the
      `DesktopShutdownService` removal and why the routed path is the only exit.
- [x] add a one-line note that the process-exit cascade is not headless-observable and is covered by manual
      verification.
- [x] no automated test for a doc change; verify wording against the implemented behavior.

### Task 5: Verify acceptance criteria

- [x] verify: "Exit Now" on a dirty session shows the `ExitConfirmationDialog` (no silent loss).
      Confirmed by `CompleteEditorClose_ExitRequested_DirtyOwner_ShowsGuardAndCancelKeepsOwnerOpen`.
- [x] verify: "Exit Now" on a clean session exits without a prompt.
      Confirmed by `CompleteEditorClose_ExitRequested_CleanOwner_CascadesToOwnerClose`.
- [x] verify: "Restart Later" closes only the restart dialog and the editor, app stays open.
      Confirmed by `CompleteEditorClose_RestartLater_ClosesEditorOnly` and `RestartPromptDialogTests`.
- [x] verify: no remaining reference to `DesktopShutdownService` anywhere in the solution.
      Zero references in source/test `.cs`; only markdown docs mention the removed helper.
- [x] run the full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`. 1089 passed, 0 failed, 0 skipped.
- [x] run `dotnet format SemiStep/SemiStep.slnx` (pre-commit hook enforces it). Clean, no changes.

### Task 6: Finalize documentation and plan

- [x] confirm `Docs/architecture/exit-flow.md` matches the shipped behavior. Verified against
      `RestartPromptDialog.axaml.cs`, `GridStyleEditorWindow.axaml.cs`, and the removal of
      `DesktopShutdownService` (zero `.cs` references). Doc is accurate; no change.
- [x] update `CLAUDE.md` only if a new reusable pattern emerged. No change needed — owner-chain routing
      here is a bug fix, not a new project-wide convention.
- [x] move this plan to `docs/plans/completed/`. Marked done; not moved (harness handles the move).

## Post-Completion

*Manual / external items — no checkboxes, informational only.*

**Manual verification** (the real process-exit cascade is not reachable in the headless harness, which
installs no classic-desktop lifetime — run the app):

1. Launch the app, make a recipe change so the session is dirty (do NOT save).
2. Open the grid style editor, change a setting that requires a restart, click Save.
3. On the restart prompt, click **Exit Now**.
   - Expect: the `ExitConfirmationDialog` (Save / Don't Save / Cancel) appears; nothing is lost silently.
   - Choose **Cancel** → app stays open, recipe intact.
   - Repeat and choose **Don't Save** → app exits, change discarded intentionally.
4. Repeat with a **clean** session → **Exit Now** exits immediately with no prompt.
5. Repeat and click **Restart Later** → only the editor closes; the app keeps running.

**External system updates**: none. No consuming projects, deployment config, or third-party integration is
affected.
