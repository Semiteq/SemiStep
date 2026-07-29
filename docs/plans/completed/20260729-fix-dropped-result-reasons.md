# Fix silently-dropped Result reasons (undo/redo failure + CSV load warning)

## Overview

Two operator-facing signals currently vanish with no panel entry and no log — both are the same defect
class: a `Result` (or its `Reasons`) reaches no sink, violating the error-pipe roadmap's standing rule
*"every `Result` must be consumed"* (`20260727-error-reporting-pipe-roadmap.md`). Neither is a
localization issue; both are live correctness bugs. (They are the roadmap's internal "issues to file"
#4/#5 — NOT GitHub issue numbers; file the GitHub issues before delivery if a `(#N)` header reference is
wanted, else the commit header carries none.) This PR consumes both.

Small, self-contained, and independent of the Core-error-typing waves. It touches recipe-command and
recipe-load files the later waves also touch, so clearing it first avoids re-churning them.

## The two bugs (grounded on current master, post #151/#152)

**Bug A — Undo/Redo drop the coordinator's failed `Result`.**
`RecipeCommandsViewModel.Undo()`/`Redo()` (`:129-137`) call `_coordinator.Undo()`/`Redo()` and discard
the return value. Those coordinator methods return `Result` (`RecipeCoordinator.cs:311,316`). Their
sibling handlers `AddStep`/`DeleteStep` (`:99-103,120-124`) both do
`if (result.IsFailed) { _messagePanel.ReportFailure(result); return; }`. So an undo/redo failure is
swallowed — no panel, no log. Two failure vectors: for an operator, the `_canUndo`/`_canRedo` UI gate
blocks the empty-history "No state to undo to" (`RecipeSession.cs:88`), leaving a re-analysis failure on
the restored snapshot as the operator-reachable case (real but hard to stage in a test). In a test,
ReactiveUI `Execute()` bypasses `canExecute`, so an empty-history `UndoCommand.Execute()` returns the real
"No state to undo to" failure — that is the practical test vector.

**Bug B — CSV file-load drops the row-count-mismatch warning.**
`CsvService.LoadAsync` attaches a `Warning` to the success result on a metadata/actual step-count
mismatch (`CsvService.cs:53-56`, `okResult.WithWarning(...)`). But `RecipeCoordinator.LoadRecipeAsync`
(`:336-347`) sets `result = validateResult` (the session's validation result) and never merges
`loadResult.Reasons`, so the warning is discarded. The success tail returns `result` (`:367`) with no
warning, and `RebuildMessagePanel()` (`:365`) rebuilds only from `_session.Snapshot.Reasons` (structural
validity), which cannot re-derive the file-metadata check. `RecipeFileViewModel.LoadRecipeAsync`
(`:125-135`) then calls `ReportSuccess("Loaded: X")`, so a corrupt/hand-edited recipe loads with its
integrity warning silently lost.

## Mechanism

**Bug A** — mirror the sibling handlers exactly:
```csharp
private void Undo()
{
    var result = _coordinator.Undo();
    if (result.IsFailed)
    {
        _messagePanel.ReportFailure(result);
    }
}
```
Same for `Redo()`. `Undo()`/`Redo()` return `Result` (no value), so there is no `RequestSelection` to do
— the restored recipe surfaces through the coordinator's existing `RecipeReplaced` mutation.

**Bug B** — carry ONLY the load-integrity reason through the return, then surface it:
- `RecipeCoordinator.LoadRecipeAsync` success branch: inside the `InvokeAsync` lambda, on
  `validateResult.IsSuccess` return `Result.Ok().WithReasons(loadResult.Successes)` (the CSV `Warning` is
  an `ISuccess`, in `.Successes`); on failure return `validateResult` unchanged. **Do NOT merge
  `validateResult`'s reasons** — `LoadAsCurrentValidated → LoadAsCurrent` returns
  `Result.Ok().WithReasons(snapshot.Reasons)` (`RecipeSession.cs:190`), so `validateResult` already carries
  the STRUCTURAL analysis warnings (unclosed For / unmatched EndFor), which already have their sink via
  `RebuildMessagePanel → RefreshReasons` (`RecipeCoordinator.cs:365`, the validation list). Merging them
  into the return would double-surface them, and because `Warning` is one sealed class the VM could not
  tell a load-integrity warning from a structural one. Nothing consumes `validateResult`'s reasons from the
  return (`RecipeFileViewModel:126-135` checks only `IsFailed`), so dropping them from the return value is
  safe; the load warning rides alone.
- `RecipeFileViewModel.LoadRecipeAsync` success tail: `if (result.Successes.OfType<Warning>().Any())
  ReportWarning(<joined warning messages>); else ReportSuccess(Loaded…)`. The panel's `_operationEntry` is
  single-slot; the integrity warning is the signal that matters, so it takes the slot (a Warning entry
  counts toward `HasEntries`, `MessagePanelViewModel:227-228`). The warning text is Core free-text English
  today; typing/localizing it (a `Warning` subclass) is the recipe/CSV wave — surfacing it in English now
  is strictly better than losing it.

`Warning` lives in `SemiStep.Core.Shared`; the UI already imports it (`MessagePanelViewModel` filters
`OfType<Warning>()`).

## Scope

**In:** the two fixes above + tests. **Out:** typing/localizing the CSV warning (recipe/CSV wave);
`LoadRecipeFromPlcAsync` (the PLC read produces no such warning); any other dropped-Result site not named
here.

## Development Approach

- Regular (code, then tests). `dotnet build SemiStep.slnx` 0 warnings; `dotnet test` green after each task.
- Tests use the existing UI test conventions (`[AvaloniaFact]` where a real `MessagePanelViewModel` /
  coordinator is driven; a `RecordingLogger` where a log assertion is needed).

## Acceptance Evidence

**Automatable:**
1. **Bug A:** `RecipeCoordinator` is concrete and non-mockable, so force a REAL failure through the
   `UIFixture.Coordinator` (the file's own precedent — `RecipeCommandsViewModelReportingTests.cs:69-84`
   `DeleteStep_WhenCoordinatorRejectsIndex_...`). With empty undo history, `UndoCommand.Execute()` bypasses
   the `canExecute` gate (ReactiveUI `Execute()` does not check `CanExecute`) and the real session returns
   "No state to undo to" (`RecipeSession.cs:88`) — assert a panel error entry. Same for `RedoCommand`.
2. **Bug B (coordinator):** `LoadRecipeAsync` on a file whose `# ROWS="N"` metadata line deliberately
   disagrees with the body data-row count returns a success `Result` whose `Successes` contains the
   row-count `Warning` (was: dropped). **Stage it** by writing a temp CSV and patching its `# ROWS=` header
   to a wrong value — a save-then-load round-trip cannot trigger it (`CsvService.SaveAsync` always writes a
   correct header, `CsvMetadata.cs:16`; body rows are counted at `CsvFileIo.cs:57-65`). This is NOT the
   YAML-config overlay pattern.
3. **Bug B (viewmodel):** a metadata-mismatch load surfaces a **warning** panel entry, not a plain success.
   **Pinning test:** a structurally-warned recipe — an **unclosed For** (the proven warning-level case,
   `RecipeCoordinatorLoadRecipeTests.cs:50-53`; do NOT use a failure-level structural error) — with a CLEAN
   file still shows the "Loaded: X" success, its structural warnings appearing only in the validation list,
   not the operation slot. **Stage the fixture via `csvService.SaveAsync(recipe, path)` DIRECTLY** (the
   coordinator save gate rejects an invalid recipe, `RecipeCoordinator.cs:377`), following
   `LoadRecipeAsync_AnalyzerFailure` (`RecipeCoordinatorLoadRecipeTests.cs:111`). This guards the fix from
   re-triggering on `validateResult`'s reasons.
4. Regression: existing load/undo/redo tests stay green.

**Manual smoke:** hand-edit a recipe CSV so its metadata row count disagrees with the body, load it — the
panel shows the mismatch warning (English) instead of a silent success.

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: Undo/Redo consume the failed Result (Bug A)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs` (`Undo`/`Redo` `:129-137`)
- Create/Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelReportingTests.cs`

- [x] capture the `Result` from `_coordinator.Undo()`/`Redo()`; on `IsFailed`, `_messagePanel.ReportFailure(result)`, matching `AddStep`/`DeleteStep`.
- [x] tests (real coordinator, no stub — `RecipeCoordinator` is concrete/non-virtual): via `UIFixture.Coordinator`, an empty-history `UndoCommand.Execute()` / `RedoCommand.Execute()` reports a panel error entry ("No state to undo to"); a state with valid history reports nothing on undo. Follow `DeleteStep_WhenCoordinatorRejectsIndex_...` (`:69-84`) as the template.
- [x] `dotnet build SemiStep.slnx` (0 warnings) + `--filter` green.

### Task 2: Preserve and surface the CSV load warning (Bug B)

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (`LoadRecipeAsync` success branch `:340-346`)
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` (`LoadRecipeAsync` success tail `:133-135`)
- Create/Modify: coordinator + viewmodel tests; reuse the CSV load fixtures

- [x] coordinator: in the `InvokeAsync` lambda, on `validateResult.IsSuccess` return `Result.Ok().WithReasons(loadResult.Successes)` (carry the CSV warning alone); on failure return `validateResult`. Do NOT merge `validateResult`'s reasons — they already surface via `RefreshReasons` and would double-count / confuse the VM's warning check.
- [x] viewmodel: on success, if `result.Successes.OfType<Warning>()` is non-empty, `ReportWarning(<joined warning messages>)`; else keep `ReportSuccess(Loaded…)`.
- [x] tests — fixtures staged as in Acceptance #2/#3 (temp CSV with a patched wrong `# ROWS=` header for the mismatch; `csvService.SaveAsync` + an unclosed-For recipe for the pinning case). The viewmodel tests register an `OpenFileInteraction` handler to feed the temp path — template at `RecipeFileViewModelSaveResultTests.cs:53`. Cases: (a) mismatch load → success `Successes` carries the `Warning` (coordinator) + a warning panel entry (viewmodel); (b) clean load → success entry, no warning; (c) **pinning:** unclosed-For recipe + clean file → "Loaded: X" in the operation slot, structural warnings only in the validation list.
- [x] build + `--filter` green.

### Task 3: Verify

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] `dotnet build SemiStep.slnx` — 0 warnings; `dotnet format SemiStep.slnx`.
- [x] confirm no other reachable dropped-`Result`/dropped-`Reasons` site was introduced; note the two fixed.
- [x] mark this plan for archival at delivery (do NOT move it mid-run).

## Post-Completion

**Next:** the Core-error-typing waves — recipe-edit wave (which will type + localize the undo/redo and
CSV warnings surfaced here), then the shared `ImportedRecipeValidator` rework, per the roadmap.

**Executed by exec:**
- branch: fix-dropped-result-reasons

## Verify it yourself

Both bugs are silent drops, so the proof is that the signal now reaches the panel where it did not
before. Fastest automated proof:
`dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeCommandsViewModelReporting|FullyQualifiedName~RecipeFileViewModelLoadResult|FullyQualifiedName~RecipeCoordinatorLoadRecipe"`.

- **Bug A:** `Undo_WhenNoHistory_ReportsFailedResultToPanel` / `Redo_WhenNoHistory_...` — on `master` these
  fail (the failed `Result` was discarded → no panel entry); after the fix they pass (an error entry
  appears). Revert `RecipeCommandsViewModel.Undo()` to discard the result and the test goes red.
- **Bug B:** `LoadRecipeAsync_RowCountMismatch_CarriesWarningOnSuccessResult` (coordinator carries the
  warning) + the `RecipeFileViewModelLoadResultTests` trio (mismatch → Warning entry; clean → success; the
  **pinning** test: an unclosed-For recipe from a clean file still shows "Loaded: X", structural warnings
  only in the validation list). The pinning test is the guard — merge `validateResult.Reasons` instead of
  `loadResult.Successes` in the coordinator and it goes red.
- **Manual smoke (Bug B):** hand-edit a saved recipe CSV so its `# ROWS="N"` header disagrees with the body
  row count, load it — the panel now shows the mismatch warning (English) instead of a silent "Loaded".

Full gate: `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` (1554 passed).
