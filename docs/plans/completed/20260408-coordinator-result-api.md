# Plan: Coordinator Result API

## Overview

Coordinator public methods that mutate recipe state currently return `void`, hiding the
`Result` they already receive from `DomainFacade`. Callers in `RecipeGridViewModel` and
`ClipboardViewModel` wrap these calls in `try/catch` blocks that are never reached under
normal operation, and have no way to inspect success or failure at the call site. `SaveRecipeAsync`,
`NewRecipe`, and `ResolveConflict` propagate failures only via side-effects (exceptions or
silent log entries) with no structured error surface. The goal is to make every public
coordinator mutation method return `Result` or `Task<Result>`, remove all defensive try/catch
blocks from ViewModels, and let callers react to failure explicitly.

## Solution Overview

**Coordinator methods — return the Result they already have:**
`AppendStep`, `InsertStep`, `RemoveStep`, `RemoveSteps`, `InsertSteps`, `ChangeStepAction`,
`UpdateStepProperty`, `Undo`, `Redo` already receive `Result` from `DomainFacade` and call
`RefreshMessagePanel(result)`. Changing the return type to `Result` requires only removing
`void`, adding `return result`, and updating call sites to check `IsFailed` if needed.
The panel rebuild continues to happen inside the coordinator — callers use the returned
`Result` to decide on local UI reactions (e.g., aborting a grid update).

**SaveRecipeAsync — propagate IO failure as Result:**
`DomainFacade.SaveRecipeAsync` currently rethrows `ICsvService` exceptions. It will wrap
the `_csvService.SaveAsync` call in a try/catch and return `Result.Fail` on `IOException`.
`RecipeMutationCoordinator.SaveRecipeAsync` changes from `Task` to `Task<Result>` and
forwards the result. `RecipeFileViewModel.SaveToFileAsync` replaces its `try/catch` with
`result.IsFailed` check and `AddError` from `result.Errors`.

**NewRecipe — return Result from SetNewRecipe:**
`DomainFacade.SetNewRecipe` currently logs an "impossible" failure silently. It will return
`Result` so that `RecipeMutationCoordinator.NewRecipe` can call `RebuildMessagePanel(result)`
and return `Result`. In practice `Result.Ok` always, but the contract is now honest.

**ResolveConflict — return Result:**
`DomainFacade.ResolveConflict` returns `void` and has no failure path. It will return `Result`
(always `Ok` for now) so `coordinator.ResolveConflict` can return `Result` and
`MainWindowViewModel.HandleConflictAsync` can remove its `try/catch` wrapping the coordinator
call, replacing it with `result.IsFailed` inspection. The dialog interaction itself still
has a `try/catch` for `ShowDialog` exceptions — that is a UI concern unrelated to the
coordinator contract.

**DisableSync — remains Task (void-async):**
`DomainFacade.DisableSync` deliberately swallows exceptions from `DisconnectAsync` (network
teardown). Surfacing those as `Result` would require callers to handle a failure they cannot
meaningfully act on. Left unchanged.

**ViewModels — remove try/catch, react to Result:**
`RecipeGridViewModel.OnCellValueChanged` and `OnActionChanged` remove their `try/catch`
blocks (which are unreachable today) and optionally log or ignore the returned `Result`
— the panel is already updated by the coordinator internally.
`ClipboardViewModel.PasteStepsAsync` and `CutStepsAsync` can check the returned `Result`
from `InsertSteps` / `RemoveSteps` if local reaction is needed; otherwise the panel update
from the coordinator is sufficient.
`RecipeFileViewModel.SaveToFileAsync` and `LoadRecipeAsync` replace `try/catch` with
`result.IsFailed` checks.

## Affected Files

### Modified Files

| File | Change |
| ---- | ------ |
| `SemiStep/Domain/Facade/DomainFacade.cs` | `SaveRecipeAsync` → `Task<Result>` (wrap csv call); `SetNewRecipe` → `Result`; `ResolveConflict` → `Result` |
| `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` | All void mutation methods → `Result`; `SaveRecipeAsync` → `Task<Result>`; `NewRecipe` → `Result`; `ResolveConflict` → `Result` |
| `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs` | Remove `try/catch` in `OnCellValueChanged` and `OnActionChanged`; call coordinator and discard Result (panel already updated) |
| `SemiStep/UI/Clipboard/ClipboardViewModel.cs` | `PasteStepsAsync`: check `InsertSteps` result; `CutStepsAsync`: check `RemoveSteps` result |
| `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs` | `SaveToFileAsync`: replace `try/catch` with result check; `LoadRecipeAsync`: already checks `result.IsFailed`, remove outer `try/catch` |
| `SemiStep/UI/MainWindow/MainWindowViewModel.cs` | `HandleConflictAsync`: check `ResolveConflict` result instead of wrapping coordinator call in `try/catch`; `ExecuteToggleSyncAsync`: capture and check `EnableSync` result |
| `SemiStep/Tests/UI/RecipeMutationCoordinatorTests.cs` | Update assertions: failure-path tests now assert on returned `Result` in addition to panel state |

## Tasks

### Task 1: DomainFacade — SaveRecipeAsync returns Task\<Result\>

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] Change `SaveRecipeAsync` return type from `Task` to `Task<Result>`
- [x] Wrap `_csvService.SaveAsync(...)` in `try/catch (Exception ex)` and return `Result.Fail(ex.Message)` on failure
- [x] On success: call `_stateManager.MarkSaved()` and return `Result.Ok()`
- [x] Add `using FluentResults;` if not already present

---

### Task 2: DomainFacade — SetNewRecipe returns Result

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] Change `SetNewRecipe` return type from `void` to `Result`
- [x] After `_stateManager.Update(snapshot)`: if `snapshot.IsFailed`, return `snapshot.ToResult()` instead of just logging
- [x] On success: return `Result.Ok().WithReasons(snapshot.Reasons)`
- [x] Remove the `Log.Warning` call for the impossible failure (the Result carries the information now) — or keep it alongside as a belt-and-suspenders diagnostic log

---

### Task 3: DomainFacade — ResolveConflict returns Result

**Files:**

- Modify: `SemiStep/Domain/Facade/DomainFacade.cs`

- [x] Change `ResolveConflict` return type from `void` to `Result`
- [x] Current body has no failure path: return `Result.Ok()` at end of both branches
- [x] No other logic changes required

---

### Task 4: RecipeMutationCoordinator — mutation methods return Result

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] `AppendStep(int actionId)` → `Result`: add `return result` after existing `RefreshMessagePanel(result)` + early return on failure; keep `SuggestedSelection` and `_stateChanged.OnNext` on success path
- [x] `InsertStep(int index, int actionId)` → `Result`: same pattern
- [x] `RemoveStep(int index)` → `Result`: same pattern
- [x] `RemoveSteps(IReadOnlyList<int> indices)` → `Result`: same pattern
- [x] `InsertSteps(int startIndex, IReadOnlyList<Step> steps)` → `Result`: same pattern
- [x] `ChangeStepAction(int stepIndex, int newActionId)` → `Result`: same pattern
- [x] `UpdateStepProperty(int stepIndex, string columnKey, string value)` → `Result`: same pattern
- [x] `Undo()` → `Result`: same pattern
- [x] `Redo()` → `Result`: same pattern

> All nine methods follow identical structure: `var result = domainFacade.X(...); RebuildMessagePanel(); if (result.IsFailed) return result; /* success side-effects */; return result;`

---

### Task 5: RecipeMutationCoordinator — SaveRecipeAsync, NewRecipe, ResolveConflict

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`

- [x] `SaveRecipeAsync(string filePath)` → `Task<Result>`: await `domainFacade.SaveRecipeAsync(filePath)`, store result, call `RebuildMessagePanel()` with it, return result; keep `SuggestedSelection = null` and `_stateChanged.OnNext(MetadataChanged)` on success path only
- [x] `NewRecipe()` → `Result`: call `domainFacade.SetNewRecipe()`, store result, call `RebuildMessagePanel()` with it; keep `SuggestedSelection = null` and `_stateChanged.OnNext(RecipeReplaced)` unconditionally (new recipe always replaces regardless); return result
- [x] `ResolveConflict(bool keepLocal)` → `Result`: call `domainFacade.ResolveConflict(keepLocal)`, store result, return result; no panel update needed (no recipe analysis reasons produced here)

---

### Task 6: RecipeGridViewModel — remove dead try/catch

**Files:**

- Modify: `SemiStep/UI/RecipeGrid/RecipeGridViewModel.cs`

- [x] `OnCellValueChanged`: remove `try/catch (Exception ex)` wrapper; call `_coordinator.UpdateStepProperty(...)` directly; discard returned `Result` (panel is updated inside coordinator)
- [x] `OnActionChanged`: remove `try/catch (Exception ex)` wrapper; call `_coordinator.ChangeStepAction(...)` directly; discard returned `Result`

---

### Task 7: ClipboardViewModel — check Result from InsertSteps and RemoveSteps

**Files:**

- Modify: `SemiStep/UI/Clipboard/ClipboardViewModel.cs`

- [x] `PasteStepsAsync`: capture result of `_coordinator.InsertSteps(...)`; if `result.IsFailed`, add error to panel: `_messagePanel.AddError(result.Errors[0].Message, ClipboardSource)`; the coordinator already called `RebuildMessagePanel` so structural errors are shown — this handles the case where the caller wants to know about failure locally
- [x] `CutStepsAsync`: capture result of `_coordinator.RemoveSteps(...)`; if `result.IsFailed`, add error to panel

---

### Task 8: RecipeFileViewModel — replace try/catch with Result checks

**Files:**

- Modify: `SemiStep/UI/RecipeFile/RecipeFileViewModel.cs`

- [x] `SaveToFileAsync`: remove `try/catch`; `var result = await _coordinator.SaveRecipeAsync(filePath)`; if `result.IsFailed`, call `_messagePanel.AddError(result.Errors[0].Message, FileSource)` and return; on success keep `CurrentFilePath = filePath` and `_messagePanel.AddInfo(...)`
- [x] `LoadRecipeAsync`: already checks `result.IsFailed` on coordinator result; remove outer `try/catch (Exception ex)` wrapping the entire coordinator call (IO exceptions are now wrapped in `Result` by DomainFacade)
- [x] `ThrownExceptions` subscribers on `SaveRecipeCommand`, `SaveAsRecipeCommand`, `LoadRecipeCommand` (lines 37, 42, 47): these remain as the last-resort safety net for any exception escaping outside the Result path (e.g., from `SaveFileInteraction.Handle` or `OpenFileInteraction.Handle`)

---

### Task 9: MainWindowViewModel — use Result from ResolveConflict and EnableSync

**Files:**

- Modify: `SemiStep/UI/MainWindow/MainWindowViewModel.cs`

- [x] `HandleConflictAsync`: keep `try/catch` around `dialog.ShowDialog(MainWindow)` (UI interaction can throw); move `_coordinator.ResolveConflict(dialog.KeepLocal)` outside the try block or into its own check; capture returned `Result`; if `result.IsFailed`, call `MessagePanel.AddError(result.Errors[0].Message, "PLC")`; remove fixed-string `"Failed to resolve PLC recipe conflict — sync disabled"` message
- [x] `ExecuteToggleSyncAsync`: capture result of `await _coordinator.EnableSync()`; result is already consumed by the coordinator internally (panel updated); no additional action needed at ViewModel level — but stop discarding the return value silently

---

### Task 10: Update tests

**Files:**

- Modify: `SemiStep/Tests/UI/RecipeMutationCoordinatorTests.cs`

- [x] `AppendStep_Failure_AddsErrorToMessagePanel`: additionally assert `result.IsFailed == true` on the returned `Result`
- [x] `AppendStep_Failure_DoesNotEmitSignal`: assert returned `Result.IsFailed`
- [x] Add `ChangeStepAction_Failure_ReturnsFailed` test: use invalid action id, assert returned `Result.IsFailed` and `panel.ErrorCount > 0`
- [x] Add `UpdateStepProperty_Failure_ReturnsFailed` test: use out-of-range step index, assert returned `Result.IsFailed`
- [x] Add `SaveRecipeAsync_Failure_ReturnsFailed` test: mock csv service to throw `IOException`, assert returned `Result.IsFailed` and panel shows error
- [x] Add `NewRecipe_ReturnsFailed_WhenAnalysisFails` test: only if a way to make `AnalyzeRecipe(Recipe.Empty)` fail can be injected (may not be feasible — document as skipped if not)

---

### Task 11: Build and Test

**Files:** (none)

- [x] Run build: `dotnet build SemiStep/Application/Application.csproj`
- [x] Run tests: `dotnet test SemiStep/Tests/Tests.csproj`
- [x] All pass

## Open Questions

1. `ClipboardViewModel.PasteStepsAsync` already calls `_messagePanel.AddError` for deserialization failures (line 119). After Task 7, it will also call `AddError` for `InsertSteps` failures. The coordinator will also call `RebuildMessagePanel` internally. This means the panel may show the error twice — once from the coordinator's `RefreshReasons` path and once from the ViewModel's `AddError`. Needs verification during implementation: if `InsertSteps` failure reasons are already shown by `RebuildMessagePanel`, the additional `AddError` in ClipboardViewModel may be redundant. Consider just checking `result.IsFailed` without calling `AddError` and letting the panel rebuild handle it.

2. `NewRecipe` currently calls `messagePanel.Clear()` (which will become `RebuildMessagePanel()` after the first plan is applied). Should `_stateChanged.OnNext(RecipeReplaced)` fire even if `SetNewRecipe` returns `Result.Fail`? In practice impossible, but the contract should be explicit.
