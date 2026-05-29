# Status-Bar Routing: Validation State vs Operation Outcome

## Overview

Follow-up correctness fix on the just-completed two-channel status-bar split
(`Docs/plans/completed/20260526-status-message-channel-split.md`).

**Bug:** a rejected cell-edit (and action-change) validation error appears in BOTH the transient slot
AND the validation panel/badge, and the badge shows "1 Error" even though the recipe is still valid.
(Screenshot: editing a cell to `999999` shows "Step 1: Value 999999 exceeds maximum 200…" in the slot
AND "1 Error / Value 999999 exceeds maximum 200…" in the panel.)

**Root cause (verified):**
- `RecipeSession.UpdateStepProperty` (`SemiStep.Core/Recipes/RecipeSession.cs:387`) returns a FAILED
  `Result` BEFORE applying the change when the value is invalid (parse/type/range/group, lines 426-444).
  The edit is REJECTED — the recipe is unchanged and remains VALID. The error is an OPERATION OUTCOME,
  not a current structural defect.
- `RecipeCoordinator.RebuildMessagePanel()` (`SemiStep.UI/Coordinator/RecipeCoordinator.cs:537-540`)
  feeds the validation panel from `_lastRecipeResult.Reasons` — the LAST OPERATION's reasons. On a failed
  mutation, that holds the rejection error, so it wrongly lands in the validation panel + badge.
- `RecipeGridViewModel.OnCellValueChanged`/`OnActionChanged` also report the failed `Result` to the
  transient slot. Hence the same error shows in both channels.
- On a SUCCESSFUL mutation `result = Result.Ok().WithReasons(snapshot.Reasons)`, so
  `_lastRecipeResult.Reasons == snapshot.Reasons` — panel == snapshot only on success; they diverge ONLY
  on failed operations.

**Fix (Variant A — source-based, agreed via brainstorm):** the validation panel reflects the recipe's
CURRENT structural validity and is fed ONLY from `RecipeSession.Snapshot.Reasons`. The transient slot
reflects OPERATION OUTCOMES, fed from operation `Result`s at the VM call sites. Never feed the panel from
an operation `Result`. No typed `IReason` subtypes — explicitly rejected as over-engineering (the "type"
axis is the message's origin, already embodied by the two channels).

## Context (from discovery)

Files/components involved:
- `SemiStep.UI/Coordinator/RecipeCoordinator.cs` — `RebuildMessagePanel()` + `_lastRecipeResult` field.
- `SemiStep.Core/Recipes/RecipeSession.cs` — `Snapshot` (`Result<RecipeSnapshot>`), `IsValid`, the
  reject-before-apply behavior of `UpdateStepProperty`/`ChangeStepAction`/append/insert/etc.
- `SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — `OnCellValueChanged`/`OnActionChanged` transient
  `ReportError` (KEPT as-is).
- Tests: `SemiStep.Tests/UI/RecipeCoordinatorTests.cs`, `RecipeCoordinatorLoadRecipeTests.cs`,
  `RecipeCoordinatorSaveGateTests.cs`, `OperationStatusReportingTests.cs`, `Helpers/UIFixture.cs`.
- `CLAUDE.md` — the two-channel convention note (to be refined).

`_lastRecipeResult` read sites (verified): only `RecipeCoordinator.cs:538` (the panel feed, being changed)
and `:375` (returned in the `SaveRecipeAsync` rejection path, assigned one line above at `:373` — trivially
replaceable by a local). All other occurrences are write-only assignments → field becomes removable.

Existing tests that ENCODE the old (buggy) "failed-op populates panel" semantics and must flip:
- `RecipeCoordinatorLoadRecipeTests.cs:60` `LoadRecipeAsync_Failure_SurfacesErrorInValidationPanel`
  — asserts a failed load fills the panel with the load-error messages. Under Variant A a failed load is
  an operation outcome → must NOT populate the panel; it is surfaced transiently by `RecipeFileViewModel`
  (already covered by `OperationStatusReportingTests` load-missing-error).
- `RecipeCoordinatorTests.cs:234` `AppendStep_Failure_AddsErrorToMessagePanel` — asserts a failed
  `AppendStep(9999)` fills the panel. Under Variant A it must NOT; the panel reflects snapshot validity.

Test that stays GREEN and confirms the design (no change):
- `RecipeCoordinatorSaveGateTests.cs:32` `Save_OnDefectiveRecipe_FailsAndWritesNoFile` — the recipe is
  genuinely defective (`session.IsValid == false`, "Unclosed For loop" warning lives in the snapshot), so
  the snapshot-fed panel still surfaces that warning. This is exactly the intended behavior.

## Development Approach

- **Testing approach**: Regular (code first, then tests within the same task) — consistent with the prior
  plan for this area.
- Small, focused changes; complete each task fully before the next.
- Every task includes new/updated tests covering success and failure/edge scenarios.
- All tests pass before starting the next task.
- Build: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`. Test:
  `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.

## Testing Strategy

- Unit tests via `[AvaloniaFact]`/`[AvaloniaTheory]`, traits `Component=UI`, `Area=...`, `Category=Unit`.
- `UIFixture` builds `RecipeCoordinator` with a real `MessagePanelViewModel`; reuse it for coordinator-level
  panel assertions. No e2e harness — manual smoke under Post-Completion.

## Progress Tracking

- Mark `[x]` immediately when done; ➕ for new tasks; ⚠️ for blockers. Keep this file in sync.

## Solution Overview

One authoritative feeder per channel:
- **Validation panel** ← `RecipeSession.Snapshot.Reasons` (current recipe validity, self-healing). Changes
  only when the snapshot changes (successful mutation/load). A rejected operation leaves it untouched.
- **Transient slot** ← operation `Result`s surfaced by the initiating VM (unchanged behavior).

Invariant: never feed the panel from an operation `Result`. This removes the double-display and the false
badge in one move, and collapses `RebuildMessagePanel()` to a one-liner. `_lastRecipeResult` becomes dead
and is removed.

## Technical Details

`RebuildMessagePanel()` becomes:
```csharp
private void RebuildMessagePanel()
{
    _messagePanel.RefreshReasons(_session.Snapshot.Reasons);
}
```

`SaveRecipeAsync` rejection path (currently `RecipeCoordinator.cs:373-376`) drops the field:
```csharp
var rejection = Result.Fail(SaveRejectionMessage).WithReasons(_session.Snapshot.Reasons);
RebuildMessagePanel();
return rejection;
```
(`RebuildMessagePanel()` already shows the snapshot reasons; the returned `rejection` carries the message
for the caller — `RecipeFileViewModel` surfaces it transiently.)

Routing outcomes after the change:
- Rejected cell-edit/action-change → transient slot only (recipe valid → panel/badge clean). **Bug fixed.**
- Successful mutation that yields a structural warning → panel only (snapshot), self-heals on fix.
- Failed load/save → transient (VM) only; panel keeps showing current recipe validity.
- Save rejected on a defective recipe → "Cannot save…" transient; the genuine structural warnings already
  show in the panel from the snapshot.

## What Goes Where

- **Implementation Steps** (checkboxes): code + tests + the CLAUDE.md note in this repo.
- **Post-Completion**: manual smoke verification in the running app.

## Implementation Steps

### Task 1: Feed the validation panel from the snapshot; remove dead `_lastRecipeResult`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs` (add regression test)

- [ ] change `RebuildMessagePanel()` to `_messagePanel.RefreshReasons(_session.Snapshot.Reasons)`
- [ ] grep-confirm `_lastRecipeResult` has no reader other than `:375`/`:538`; convert the `SaveRecipeAsync` rejection path (`:373-376`) to a local `rejection` variable and remove the `_lastRecipeResult` field plus all its assignments (`:47,211,241,337,395,412,417,431,446`)
- [ ] build `SemiStep.UI` — must be clean
- [ ] add `[AvaloniaFact]` regression test: a rejected `UpdateStepProperty` (invalid value, e.g. exceeds max) on a valid recipe leaves the panel with `Entries` empty / `ErrorCount == 0` (rejection does NOT enter the panel), using `UIFixture`
- [ ] add `[AvaloniaFact]` test: a successful mutation that produces a structural warning surfaces that warning in the panel from the snapshot, and correcting it clears the panel (self-heal)
- [ ] run `dotnet test --filter "FullyQualifiedName~RecipeCoordinator"` — must pass before Task 2

### Task 2: Reconcile existing tests that encoded "failed-op populates panel"

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/OperationStatusReportingTests.cs`

- [ ] flip `LoadRecipeAsync_Failure_SurfacesErrorInValidationPanel`: a failed load must leave `panel.Entries` empty (panel reflects unchanged snapshot); assert the failure is carried on the returned `Result` (the transient surfacing is covered separately). Rename to reflect the new intent (e.g. `LoadRecipeAsync_Failure_DoesNotPopulateValidationPanel`)
- [ ] flip/rewrite `AppendStep_Failure_AddsErrorToMessagePanel`: a failed `AppendStep(9999)` must NOT add to the panel (`ErrorCount == 0`). Rename accordingly (e.g. `AppendStep_Failure_DoesNotPopulateMessagePanel`)
- [ ] confirm `RecipeCoordinatorSaveGateTests.Save_OnDefectiveRecipe_FailsAndWritesNoFile` still passes unchanged (snapshot warning legitimately shown) — if it fails, investigate before proceeding
- [ ] augment the existing RecipeGrid invalid-cell-edit reporting test in `OperationStatusReportingTests` to ALSO assert the validation panel is NOT populated by the rejected edit (the key regression guard tying both channels together)
- [ ] add the same panel-not-populated assertion to the action-change reporting test `RecipeGrid_ChangeToUnknownAction_ReportsError` (`OperationStatusReportingTests.cs`) for symmetry with the cell-edit guard (both arms of the bug share `Track`/`TrackVoid` routing)
- [ ] ⚠️ fix two tests whose dirty precondition becomes INERT under the new model (they used a failed op to populate the panel, which no longer works → they degrade to tautologies): `RecipeCoordinatorTests.cs:210 NewRecipe_RebuildsPanelFromFreshRecipeReasons` and `RecipeCoordinatorLoadRecipeTests.cs:36 LoadRecipeAsync_Success_ClearsMessagePanelBeforeAddingNewReasons`. Re-establish the dirty precondition via a REAL snapshot warning (e.g. `RecipeTestDriver.AddFor(...)` to create an unclosed-For warning) so they still verify clear-before-rebuild; or, if that is impractical, document explicitly why each remains inert
- [ ] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (full suite) — must pass before Task 3

### Task 3: Close the reconnect silent-drop; audit for lost feedback; verify acceptance

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs` (and any other manual `RecipeCoordinator` construction)
- Modify: test files as needed for any gap found

- [ ] audit every `RebuildMessagePanel()` caller and confirm that for each FAILED path the failure still reaches the user, now that the panel no longer shows operation reasons. The callers are: `Track`/`TrackVoid` (all mutations incl. `UpdateStepProperty`/`ChangeStepAction`/`AppendStep`/etc.), `LoadRecipeAsync` (file), `LoadRecipeFromPlcAsync`, `ResolveConflict`, `SaveRecipeAsync`, and **`ApplyReconnectPlcRecipeAsync` (`RecipeCoordinator.cs:402-427`)**
- [ ] ⚠️ FIX the reconnect silent-drop: `ApplyReconnectPlcRecipeAsync`'s failure `Result` is consumed fire-and-forget in `PlcLifecycleManager` (~:270) and reaches NO VM, so under snapshot-feeding an analyzer failure on the reconnected PLC recipe would be silent. Inject `OperationStatusViewModel` into `RecipeCoordinator` (already DI-registered) and have `ApplyReconnectPlcRecipeAsync` call `OperationStatus.ReportError(...)` on the failure path. This is a coordinator-initiated operation with no VM, so the coordinator is the right place to report it. Add a coordinator test asserting a failed reconnect-apply surfaces a transient error (and does NOT populate the panel)
- [ ] update every manual `RecipeCoordinator` construction for the new constructor arg: `UIFixture.cs` (already exposes `OperationStatus`), and the `BuildCoordinatorAsync` helpers in `RecipeCoordinatorSaveGateTests.cs` and `RecipeCoordinatorLoadRecipeTests.cs`
- [ ] check `RecipeCommandsViewModel` (Add Step / Undo / Redo / Delete): the review found Undo/Redo are gated by `CanExecute` (failures unreachable) and Add/Delete use valid action ids from the UI — confirm this holds and document that these command failures are unreachable from the real UI (so no transient reporting is needed); if any reachable silent failure is found, add `OperationStatus.ReportError` + a test
- [ ] verify the four acceptance criteria: (1) rejected edit shows ONLY in the transient slot; (2) badge/panel stay clean when the recipe is valid; (3) genuine structural defects still show in the panel and self-heal; (4) failed load/save/sync/reconnect still surface transiently
- [ ] run full suite + `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` (apply if needed) + `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`

### Task 4: [Final] Documentation and plan archival

**Files:**
- Modify: `CLAUDE.md`
- Move: this plan → `Docs/plans/completed/`

- [ ] refine the two-channel convention note in `CLAUDE.md`: state the validation panel is fed from `RecipeSession.Snapshot.Reasons` (current recipe validity), NOT from operation `Result`s; operation outcomes (including rejected edits) go to the transient `OperationStatusViewModel`
- [ ] `git mv` this plan to `Docs/plans/completed/20260526-status-bar-validation-vs-outcome-routing.md` and tick remaining boxes

## Post-Completion

*Manual verification — informational only.*

- Run the app and reproduce the original screenshot scenario: edit a cell to an out-of-range value
  (e.g. `999999`). Expect: the transient slot shows "Step N: …exceeds maximum…", and the panel/badge stay
  clean (no "1 Error"), because the recipe still holds the previous valid value.
- Then create a genuine structural defect (e.g. an unclosed `For` loop): expect the badge/panel to show the
  warning, and clearing the defect to self-heal it.
- Confirm a failed Save/Load still shows its message in the transient slot.
