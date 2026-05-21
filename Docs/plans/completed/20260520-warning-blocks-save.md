# Block Save and PLC Push When Recipe Has Warnings

**Prerequisite for:** `Docs/plans/20260520-execution-overlay-and-loop-tinting.md` — overlay/tinting behaviour over malformed recipes is safer once such recipes can no longer be saved or pushed to the PLC.

## Overview

A recipe whose structure is broken — currently the only known case being an unclosed `For` loop — is today persisted to CSV without complaint and silently pushed to the PLC. `RecipeSession.IsValid` is `true` because `LoopParser` emits a `Warning`, not an `Error`, and FluentResults' `IsSuccess` ignores warnings. The user sees a yellow message in the panel but nothing in the persistence or sync path consults it.

**Decision (after discussion):** any warning attached to the current recipe snapshot is treated as a blocking defect for Save and PLC push. To make this rule clean, the existing advisory warning "Recipe has no steps" is removed — an empty recipe is a normal initial state that does not warrant a warning. The remaining analyzer warnings (`LoopParser` unclosed-For and unmatched-EndFor) all represent real structural defects, so promoting "any warning blocks Save" causes no false positives.

Goal:

- An empty recipe is valid (no warnings, `IsValid=true`).
- A recipe whose snapshot carries any warning cannot be saved to CSV and cannot be pushed to the PLC.
- Editing of a defective recipe continues to work (so the user can add the missing `EndFor`); `RecipeSession.Apply` continues to gate only on `Result.IsFailed`, not on warnings.
- Loading a defective recipe from disk is still allowed — the user must be able to open a file to fix it.

## Context (from discovery)

**Warning emission sites that land in the recipe snapshot:**

- `SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs:16` — `Result.Ok(RecipeSnapshot.Empty).WithWarning("Recipe has no steps")`. Emitted when `recipe.Steps.Count == 0`. **To be removed.**
- `SemiStep/SemiStep.Core/Recipes/Analysis/LoopParser.cs:45` — `new Warning($"Unmatched EndFor at step {i}")`.
- `SemiStep/SemiStep.Core/Recipes/Analysis/LoopParser.cs:67` — `new Warning($"Unclosed For loop starting at step {frame.StartIndex}")`.

Other `WithWarning(...)` and `new Warning(...)` calls in the codebase (CSV import in `CsvService.cs:55`, config loaders in `SemiStep.Core/Configuration/Loaders/*`, etc.) do **not** flow into `RecipeSession._latestSnapshot`. They surface through other `Result` objects that hit the message panel directly. The "warning blocks Save" rule applies strictly to warnings inside `RecipeSession._latestSnapshot.Reasons`.

**`IsValid` definition and consumers:**

- `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs:56` — `IsValid => _latestSnapshot.IsSuccess`. FluentResults: `IsSuccess` is `true` when there are no `Error` reasons; warnings do not flip it. New definition: `IsSuccess && no warning reasons present`.
- `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs:529` — `NotifySyncIfEnabled` already passes `IsValid` to `IPlcSyncService.NotifyRecipeChanged`. Becomes correct automatically.
- `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs:176,324` — both call sites already pass `_session.IsValid`. Become correct automatically.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs:99-114` — `NotifyRecipeChanged(recipe, isValid)` on `isValid=false` clears pending snapshot and sets `Status = OutOfSync`. Already correct.
- `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs:350-379` — `SaveRecipeAsync` does **not** consult `IsValid`. New gate goes here.

**Why `Apply` must keep gating only on `IsFailed`:**

`RecipeSession.Apply` (`RecipeSession.cs:66-79`) rejects any mutation whose snapshot `IsFailed`. If `IsValid=false` were used to gate `Apply`, adding a single `For` step to an empty recipe (which produces a warning until the matching `EndFor` is added) would be rejected, leaving the user unable to construct any loop. So `Apply` stays as it is; warnings only block Save and PLC push via the new `IsValid` definition.

**Tests affected:**

- `SemiStep/SemiStep.Tests/Core/Integration/Validity/CoreValidityTests.cs:14-23` — `EmptyRecipe_IsValid_ButHasWarning`: rewrite to `EmptyRecipe_IsValid_NoWarnings` — `IsValid=true`, `Warnings` empty.
- `SemiStep/SemiStep.Tests/Core/Integration/Validity/CoreValidityTests.cs:48-56` — `UnclosedLoop_ProducesWarning`: rename to `UnclosedLoop_BlocksValidity` — assert `IsValid=false` and the warning message present.
- `SemiStep/SemiStep.Tests/Core/Integration/Validity/CoreValidityTests.cs:96-103` — `WarningsDoNotAffectValidity`: delete (the asserted invariant is no longer true).
- `SemiStep/SemiStep.Tests/Core/Integration/Loops/CoreLoopTests.cs:43-51` — `UnclosedLoop_ProducesWarning`: add `driver.IsValid.Should().BeFalse()`.
- `SemiStep/SemiStep.Tests/Core/Integration/Loops/CoreLoopTests.cs:53-61` — `UnmatchedEndFor_ProducesWarning`: same treatment.
- `SemiStep/SemiStep.Tests/Core/Integration/Snapshot/CoreSnapshotStateTests.cs:14-34` — `RejectedMutation_LeavesRecipeAndValidStateUnchanged`: the existing state in this test contains an unclosed-For chain; `IsValid` flips to `false`. Update the assertion message and value.
- `SemiStep/SemiStep.Tests/Core/Integration/Snapshot/CoreSnapshotStateTests.cs:36-49` — `LastValidRecipe_UpdatesAfterFix`: after `AddFor(3).AddWait(1f)`, `IsValid=false`; after `AddEndFor()`, `IsValid=true`; `LastValidRecipe.StepCount=3`.
- `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs:104-118` — the test that loads a previously-saved empty recipe and asserts the message panel shows "Recipe has no steps". Update: panel is empty after loading an empty recipe (no warnings expected).
- `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs:77-130` — already covers `isValid=false` paths; no change needed.

## Development Approach

- Testing approach: Regular — implement, then write/update tests in the same task.
- Each task ends with tests written/updated and passing.
- `dotnet format SemiStep/SemiStep.slnx` before any commit (pre-commit hook).
- Backward compatibility: `IsValid` semantics change. Consumers that should observe the new behaviour (Save, PLC sync) already use `IsValid`. No public API is removed.

## Testing Strategy

- **Core unit/integration tests (Component=Core):**
  - Update the existing tests listed above.
  - Add `Save_BlockedWhenWarningPresent_FromRecipeSession`: not needed — the Save gate is a UI-side concern; covered by the UI integration test below.
  - Add `Apply_AcceptsDefectiveSnapshot_KeepsIsValidFalse`: build an unclosed-For via the driver; assert `Apply` returned success, `IsValid=false`, warning present.
- **UI integration test (Component=UI, Category=Integration):** `Save_OnDefectiveRecipe_FailsAndWritesNoFile` — drive a recipe with unclosed `For` through `RecipeCoordinator.SaveRecipeAsync(tempPath)`, assert `Result.IsFailed`, `File.Exists(tempPath) == false`, and the message panel still shows the warning.
- **PLC sync (Component=S7):** add `Apply_OnDefectiveRecipe_NotifiesSyncWithIsValidFalse` — spy on `IPlcSyncService`; drive `AddFor(3).AddWait(1)`; assert the spy received `NotifyRecipeChanged(_, isValid: false)`.

## Solution Overview

1. Remove the "Recipe has no steps" warning from `RecipeAnalyzer.Analyze`. Empty recipe → `Result.Ok(RecipeSnapshot.Empty)` with no reasons.
2. Redefine `RecipeSession.IsValid` to combine `IsSuccess` with absence of any `Warning` in `_latestSnapshot.Reasons`. Leave `Apply` gating untouched.
3. Add an `IsValid` check at the top of `RecipeCoordinator.SaveRecipeAsync`; on `false`, return a failed `Result` without writing the file and refresh the message panel.
4. Update or delete the named tests; add the new tests per Testing Strategy.

## Technical Details

### `RecipeAnalyzer` change

`SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs:14-17` — drop the early-return warning entirely. Empty recipes pass through normally; `LoopParser.Parse` on a zero-step recipe returns `Result.Ok([])` with no reasons (verified by reading the loop body — no warnings emitted on empty input).

```
public Result<RecipeSnapshot> Analyze(Recipe recipe)
{
    var loopParseResult = LoopParser.Parse(recipe);
    if (loopParseResult.IsFailed)
    {
        return Result.Fail(loopParseResult.Errors);
    }
    // ... existing tail unchanged ...
}
```

### `RecipeSession.IsValid` redefinition

`SemiStep/SemiStep.Core/Recipes/RecipeSession.cs:56`:

```
public bool IsValid =>
    _latestSnapshot.IsSuccess && _latestSnapshot.Reasons.OfType<Warning>().Any() == false;
```

`Apply` (line 66) keeps its current `if (snapshot.IsFailed) return ...` gate — defective edits still produce accepted snapshots, so the user can keep typing.

### Save gate

`SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs:350-379`, inserted before `_csvService.SaveAsync`:

```
if (_session.IsValid == false)
{
    var message = "Cannot save recipe with structural defects (see warnings in the message panel).";
    _logger.LogWarning("Save rejected: recipe is not valid. StepCount={StepCount}", _session.Current.StepCount);
    _lastRecipeResult = Result.Fail(message);
    RebuildMessagePanel();
    return _lastRecipeResult;
}
```

### PLC sync

No new code. `RecipeSession.NotifySyncIfEnabled` and `PlcLifecycleManager` already pass `IsValid`; `PlcSyncCoordinator.NotifyRecipeChanged` already short-circuits on `isValid=false`.

## Implementation Steps

### Task 1: Remove "Recipe has no steps" warning

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs:14-17`

- [ ] Delete the early-return branch for `recipe.Steps.Count == 0`.
- [ ] Verify `LoopParser.Parse` on an empty recipe returns `Result.Ok([])` with no reasons (the loop body in `LoopParser.cs:18-62` is skipped when `Steps.Count == 0` and the post-loop drain at line 64 is a no-op on an empty stack).
- [ ] Build: `dotnet build SemiStep/SemiStep.slnx`.

### Task 2: Redefine `IsValid`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs:56`

- [ ] Replace the `IsValid` property body to combine `IsSuccess` with the absence of any `Warning` reason on `_latestSnapshot.Reasons`.
- [ ] Confirm `Apply` (`RecipeSession.cs:66-79`) still gates only on `snapshot.IsFailed`.
- [ ] Build.

### Task 3: Gate `RecipeCoordinator.SaveRecipeAsync`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs:350-379`

- [ ] First, read `RebuildMessagePanel()` to see how `_lastRecipeResult` is merged with other panel sources. If overwriting `_lastRecipeResult` with the Save-rejection `Result.Fail` would hide the underlying analyzer warning, adjust the gate so both surface — e.g. attach the snapshot warnings to the failure `Result.Fail(message).WithReasons(_session.Snapshot.Reasons)`, or call `RebuildMessagePanel()` with an explicit list that includes both.
- [ ] Insert the early-return guard before `_csvService.SaveAsync`.
- [ ] Set `_lastRecipeResult` to the failure result; call `RebuildMessagePanel()`.
- [ ] Build.

### Task 4: Update / delete existing tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Validity/CoreValidityTests.cs` (`EmptyRecipe_IsValid_ButHasWarning` at line 14, `UnclosedLoop_ProducesWarning` at line 48, **delete** `WarningsDoNotAffectValidity` at line 96)
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Loops/CoreLoopTests.cs` (`UnclosedLoop_ProducesWarning` at line 43, `UnmatchedEndFor_ProducesWarning` at line 53)
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Snapshot/CoreSnapshotStateTests.cs` (`RejectedMutation_LeavesRecipeAndValidStateUnchanged` at line 14, `LastValidRecipe_UpdatesAfterFix` at line 36)
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs:104-118` (the load-empty-recipe test that asserts the "Recipe has no steps" panel state)

- [ ] Rewrite `EmptyRecipe_IsValid_ButHasWarning` → `EmptyRecipe_IsValid_NoWarnings`: `IsValid=true`, `Warnings` empty.
- [ ] Delete `WarningsDoNotAffectValidity`.
- [ ] In `CoreLoopTests` and `CoreValidityTests`, add `driver.IsValid.Should().BeFalse()` next to the existing warning assertions; the warning message assertions remain.
- [ ] In `CoreSnapshotStateTests`, flip `IsValid` expectations on the defective intermediate states. Before writing the new `LastValidRecipe.StepCount=3` assertion in `LastValidRecipe_UpdatesAfterFix`, read `RecipeSession.UpdateSnapshot` and confirm `_lastValidRecipe` is assigned on every accepted snapshot regardless of warnings — the assertion only holds if it is. Adjust the expected value if not.
- [ ] Audit `CoreValidityTests.MultipleWarnings_AllCaptured` (line 106). No behavioural change is expected — the test should continue to pass with the two unclosed-For warnings — but verify the assertions still read coherently after the "no steps" warning removal.
- [ ] In `RecipeCoordinatorLoadRecipeTests`, update the post-load assertion: panel has no warnings after loading an empty recipe.
- [ ] Run: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"` and `--filter "Component=UI"`.

### Task 5: Add new tests

**Files:**
- Modify (or create new file under): `SemiStep/SemiStep.Tests/Core/Integration/Validity/` — add `Apply_AcceptsDefectiveSnapshot_KeepsIsValidFalse`.
- Create: `SemiStep/SemiStep.Tests/UI/Coordinator/RecipeCoordinatorSaveGateTests.cs` — `Save_OnDefectiveRecipe_FailsAndWritesNoFile`.
- Modify (or create new file under): `SemiStep/SemiStep.Tests/S7/` — add `Apply_OnDefectiveRecipe_NotifiesSyncWithIsValidFalse`.

- [ ] `Apply_AcceptsDefectiveSnapshot_KeepsIsValidFalse`: build an unclosed-For via `RecipeTestDriver`; assert the `Apply` call returned a successful `Result`, `session.IsValid=false`, at least one warning present in `Warnings`.
- [ ] `Save_OnDefectiveRecipe_FailsAndWritesNoFile`: drive an unclosed-For recipe through a real `RecipeCoordinator`, call `SaveRecipeAsync(tempFilePath)`, assert `Result.IsFailed`, `File.Exists(tempFilePath) == false`, and the message panel still surfaces the underlying warning.
- [ ] `Apply_OnDefectiveRecipe_NotifiesSyncWithIsValidFalse`: spy on `IPlcSyncService`; drive `AddFor(3).AddWait(1)`; assert the spy received `NotifyRecipeChanged(_, isValid: false)` for the final state.
- [ ] Run: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.

### Task 6: Verify acceptance criteria and close out

- [ ] `dotnet build SemiStep/SemiStep.slnx` succeeds.
- [ ] `dotnet format SemiStep/SemiStep.slnx` — no diff.
- [ ] Full test suite green.
- [ ] Manual smoke (optional but recommended): launch the UI, add `For 3 / Wait 10`, attempt Save — observe failure message; add `EndFor`, Save succeeds. Start the app with an empty recipe — no warning panel, Save succeeds (and produces an empty CSV).
- [ ] Move this plan to `Docs/plans/completed/`.

## Out of Scope

- The existing `Result.Fail` path in `RecipeAnalyzer.cs:33-36` for `MaxLoopDepth > 3` continues to reject the mutation via `Apply` (so the user cannot add a fourth nested `For`). This is intentional and orthogonal to the warning-blocks-save change.
- Warnings emitted outside the recipe snapshot (CSV import warnings, config loader warnings, etc.) are unaffected and do not block Save.
- Localisation of the Save-rejection user-facing message. English-only for now, consistent with other coordinator messages.
- Any change to the message-panel rendering. The existing yellow warning is sufficient; failed Save additionally surfaces the coordinator's `Result.Fail` message through the existing `_lastRecipeResult` path.
