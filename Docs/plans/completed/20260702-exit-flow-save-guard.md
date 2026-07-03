# Exit Flow Save Guard (issue #107)

## Overview
- On dirty-close with the user picking **Save** in the exit confirmation dialog, the window currently closes regardless of the save outcome: a failed write and a cancelled Save-As picker both complete `SaveRecipeCommand` normally, `Subscribe` fires, and the window closes — discarding the recipe the user explicitly asked to save.
- Second defect on the same lines: `Subscribe(onNext)` without `onError` rethrows a save exception unhandled on the UI thread.
- Fix: the save pipeline reports success (`bool`), and the exit flow closes the window only on confirmed success. Failure/cancel leaves the window open with the message panel visible.
- Closes #107.

## Context (from discovery)
- `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` — `SaveRecipeAsync` (73-83), `SaveAsRecipeAsync` (85-98), `SaveToFileAsync` (100-113) all return `Task`/swallow failure into the message panel; commands are `ReactiveCommand<Unit, Unit>` (57-59). `ThrownExceptions` already wired to the message panel (37-45).
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` — `OnWindowClosing` (206-243) is `async void`; the `ExitConfirmationResult.Save` branch (225-232) is the bug site.
- Command result type change `Unit` → `bool` is binding-compatible: AXAML menu/toolbar bindings go through `ICommand.Execute`, which ignores the result.
- Test infrastructure: `SemiStep.Tests/UI/Helpers/UIFixture.cs` (`Coordinator`, `MessagePanel`, `CreateMainWindowViewModel`), `[AvaloniaFact]` + `IAsyncLifetime` pattern as in `SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`. `SaveFileInteraction`/`OpenFileInteraction` handlers can be registered in tests (see `LoadRecipe_GatedInvocation_WhileExecuting_DoesNotOpenDialog`).
- No existing test constructs the full `MainWindow`; window-level coverage needs a seam (see Task 2) or falls back to VM-level coverage.
- `InternalsVisibleTo("SemiStep.Tests")` is in place; `MainWindow` is `internal`.

## Development Approach
- **testing approach**: Regular (code first, then tests, within each task)
- complete each task fully before moving to the next
- make small, focused changes
- **CRITICAL: every task MUST include new/updated tests** for code changes in that task
  - tests cover both success and error scenarios
- **CRITICAL: all tests must pass before starting next task**
- **CRITICAL: update this plan file when scope changes during implementation**
- run `dotnet format SemiStep/SemiStep.slnx` before committing (pre-commit hook enforces it)
- maintain backward compatibility of command bindings

## Testing Strategy
- unit/headless tests via `[AvaloniaFact]` in `SemiStep.Tests/UI/RecipeFile/` and `SemiStep.Tests/UI/MainWindow/`
- test command: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- no e2e infrastructure in this project; headless Avalonia tests are the equivalent tier

## Progress Tracking
- mark completed items with `[x]` immediately when done
- add newly discovered tasks with ➕ prefix
- document issues/blockers with ⚠️ prefix

## Solution Overview
- `RecipeFileViewModel`: `SaveToFileAsync` returns `true` only when the coordinator save succeeded; `SaveAsRecipeAsync` returns `false` when the picker is cancelled; `SaveRecipeAsync` propagates. Commands become `ReactiveCommand<Unit, bool>`. Message-panel reporting stays exactly as is — the `bool` is additive.
- `MainWindow.OnWindowClosing`: the Save branch `await`s the command inside the existing `async void` handler with try/catch (exception already reported via `ThrownExceptions`; the catch only keeps the window open) and closes only on `true`.
- The dialog-showing part stays in `OnWindowClosing`; the post-choice logic moves to an internal `HandleExitChoiceAsync(ExitConfirmationResult)` so tests can drive the choice without showing the dialog.

## Technical Details
- `ReactiveCommand.CreateFromTask<bool>(SaveRecipeAsync)` — `SaveRecipeCommand`, `SaveAsRecipeCommand` become `ReactiveCommand<Unit, bool>`.
- `await command.Execute()` returns the last value of the execution observable (`bool`); on command exception the awaited observable errors — hence try/catch in the caller.
- `Close()` must be invoked only after the await completes on the UI thread (ReactiveCommand delivers on the main scheduler by default; the handler is already on the UI thread).
- Do not set `_forceClose` before the save completes — a re-entrant close during an in-flight save must still hit the guard.

## What Goes Where
- **Implementation Steps**: code + tests in this repo
- **Post-Completion**: manual smoke test of the real dialog flow

## Implementation Steps

### Task 1: Save pipeline reports success (RecipeFileViewModel)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs` (only if signature change breaks compilation)
- Create: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelSaveResultTests.cs`

- [x] change `SaveToFileAsync` to return `Task<bool>`: `true` on coordinator success, `false` on `result.IsFailed` (message-panel reporting unchanged)
- [x] change `SaveAsRecipeAsync` to return `Task<bool>`: `false` when `SaveFileInteraction` yields `null` (picker cancelled), otherwise the `SaveToFileAsync` result
- [x] change `SaveRecipeAsync` to return `Task<bool>` propagating the inner result
- [x] change `SaveRecipeCommand`/`SaveAsRecipeCommand` declarations to `ReactiveCommand<Unit, bool>`; verify all existing call sites still compile (grep `SaveRecipeCommand`, `SaveAsRecipeCommand` across UI and tests) — call sites: AXAML bindings (`ICommand`, result ignored), `MainWindow.axaml.cs:226` (`Subscribe(_ => ...)`, compiles unchanged), test `await Execute()` calls — all compile
- [x] write tests (new file `RecipeFileViewModelSaveResultTests.cs`, same fixture pattern as the CanExecute tests): successful save emits `true` and sets `CurrentFilePath`; failed coordinator save emits `false` and does NOT set `CurrentFilePath` — trigger the failure deterministically via the invalid-session gate (`RecipeCoordinator.SaveRecipeAsync` returns `Result.Fail` when `_session.IsValid == false`, RecipeCoordinator.cs:376-385), NOT via a bad file path (file-system failures may throw instead of returning `IsFailed` and would exercise the wrong branch); cancelled Save-As picker (interaction handler returns `null`) emits `false` and performs no save; Save with no `CurrentFilePath` routes through Save-As
- [x] run tests — must pass before task 2 (full suite: 1183 passed, 0 failed)

### Task 2: Close only on confirmed save (MainWindow exit flow)

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Create: `SemiStep/SemiStep.Tests/UI/MainWindow/MainWindowExitFlowTests.cs` (see fallback note)

- [x] extract the post-dialog switch from `OnWindowClosing` into `internal async Task HandleExitChoiceAsync(ExitConfirmationResult result)`; `OnWindowClosing` keeps the guard clauses, `e.Cancel = true`, dialog showing, then awaits the new method
- [x] in the `Save` branch: `var saved = await ViewModel!.RecipeFile.SaveRecipeCommand.Execute();` inside try/catch; on `saved == true` set `_forceClose = true` and `Close()`; on `false` or exception do nothing (window stays open; message panel already has the report)
- [x] keep `DontSave` (`_forceClose = true; Close()`) and `Cancel` (no-op) branches unchanged
- [x] PRIMARY window-level coverage: command-contract tests — `await SaveRecipeCommand.Execute()` returns `false` on cancelled picker, `false` on failed coordinator save (invalid session), `true` on success; these carry the regression regardless of `MainWindow` headless viability (covered by Task 1's `RecipeFileViewModelSaveResultTests` — exact same contracts, not duplicated)
- [x] BEST-EFFORT: headless test constructing `MainWindow` with `UIFixture.CreateMainWindowViewModel()`, `window.Show()`, then `await window.HandleExitChoiceAsync(ExitConfirmationResult.Save)`; cancelled-picker case (test handler returns `null`, no `CurrentFilePath`) — assert window still open/visible; successful-save case (handler returns a temp path) — assert window closed. CRITICAL: register the test's `SaveFileInteraction` handler AFTER `window.Show()` — `MainWindow.WhenActivated` registers its own `HandleSaveFileDialogAsync`, and Avalonia invokes interaction handlers LIFO, so a handler registered before activation loses to the window's own (which returns `null` headless). Note: `HandleExitChoiceAsync` bypasses the `IsDirty` guard by design — no dirty-recipe setup needed at seam level (`MainWindowExitFlowTests`: 5 tests — cancelled picker keeps open, success closes, failed coordinator save keeps open, DontSave closes, Cancel keeps open)
- [x] fallback if full `MainWindow` cannot be constructed headless (`BuildGrid()`/`BuildColumns` may not survive the headless fixture): keep only the command-contract tests, drop the window tests, and document the limitation with ⚠️ in this plan (not needed — full `MainWindow` constructs and shows headless; all window tests pass)
- [x] run tests — must pass before task 3 (full suite: 1188 passed, 0 failed)

### Task 3: Verify acceptance criteria
- [x] failed coordinator save on exit keeps the window open — covered at VM level (Task 1: invalid-session `IsFailed` test), via the command contract, and at window level (`HandleExitChoice_Save_FailedCoordinatorSave_KeepsWindowOpen`)
- [x] cancelled Save-As picker on exit keeps the window open — covered by tests
- [x] save exception on exit keeps the window open and reports to the message panel — ➕ no test exercised this path; added `HandleExitChoice_Save_SaveThrows_KeepsWindowOpenAndReportsError` (throwing picker handler → window stays open, message panel gets "Save failed: …")
- [x] `DontSave` and `Cancel` behavior unchanged — covered by `HandleExitChoice_DontSave_ClosesWindow` and `HandleExitChoice_Cancel_KeepsWindowOpen`
- [x] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1189 passed, 0 failed
- [x] run `dotnet format SemiStep/SemiStep.slnx` — no changes

### Task 4: [Final] Update documentation
- [x] check `Docs/user-manual*` for any description of the exit/save flow; update if it promises the old behavior — the user manual is `Docs/readme.md`; neither it nor the root `README.md` describes the exit-confirmation dialog or dirty-close behavior, so no update needed
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
**Manual verification**:
- real dialog flow: dirty recipe → close window → Save → cancel the picker → window must stay open; pick a valid path → window closes
- dirty recipe → close → Save with `CurrentFilePath` set on a locked file → window stays open, message panel shows the failure
