# Recipe session split: history and mutation validation

## Behavior
- Nothing the operator sees changes. Every edit, undo, redo, load, reset and save gives the same
  result, the same error type, the same dirty flag and the same PLC sync notification as today.
- `RecipeSession` (`SemiStep.Core/Recipes/RecipeSession.cs`, 610 lines on master `a023142`) gives
  two of its concerns to two new Core classes:
  - `RecipeHistory` holds the undo stack, the redo stack and the 100-entry depth cap.
  - `StepMutationValidator` holds the step index check, the insert index check, the combined "index
    check, then the action of that step" lookup, and the per-cell parse and validate chain that
    `ParseAndValidateColumnValue` runs today.
- The eight mutation entry points (`AppendStep`, `InsertStep`, `RemoveStep`, `RemoveSteps`,
  `InsertSteps`, `ChangeStepAction`, `UpdateStepProperty`, `UpdateStepForSelectorChange`) keep their
  public signatures. Seven of them carry an index check; they call the validator for it. Six of them
  end in the same apply-then-outcome tail; one helper replaces it.
- `Undo` and `Redo` share one restore path in the session.
- `RecipeSession` receives `ImportedRecipeValidator` through its constructor.
  `LoadAsCurrentValidated` loses its validator parameter. `PlcLifecycleManager` and
  `RecipeCoordinator` stop depending on `ImportedRecipeValidator`.
- Final size of `RecipeSession.cs`: 491 lines, down from 610. The issue's 300-line target is
  not reached by the two extractions it names. The remaining candidates are the selector-change
  composition in `UpdateStepForSelectorChange` and the formula recalculation gating.
- Scope guard:
  - PLC sync notification stays in `RecipeSession`, through the same `IPlcSyncService` seam (#110).
  - `RecipeCoordinator` keeps its shape apart from the removed parameter and one moved comment
    (#117).
  - `PlcLifecycleManager.DisableSync` is not edited (#180).
  - Formula recalculation gating and the selector-change composition stay in `RecipeSession`.
  - `ClipboardViewModel` keeps its own `ImportedRecipeValidator`.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Accepted edit, undo, redo, reset, load | replaces the snapshot and records exactly one sync notification that carries the new `Current` and `IsValid` | agent: `RecipeSession.NotifySyncIfEnabled` callers | Task 1 sync pins |
| Rejected edit, undo on an empty stack, failed validated load | records no sync notification and changes no state | agent: early returns in `RecipeSession` | Task 1 sync pins |
| Accepted edit | pushes the prior recipe, clears redo, sets dirty | agent: `RecipeSession.Apply` | characterization suite |
| Accepted edit with an analysis warning | succeeds and carries the warning on the result, for both result shapes | agent: `RecipeSession.Apply` reasons | Task 1 tail pins |
| Edit with an out-of-range step index | fails with `StepIndexOutOfRangeError`, state unchanged | agent: `StepMutationValidator.CheckStepIndex` | Task 1 index pins |
| Insert at an index below 0 or above the step count | fails with `InsertIndexOutOfRangeError`; an index equal to the count is accepted | agent: `StepMutationValidator.CheckInsertIndex` | Task 1 index pins |
| Bad index and unknown action in one call (`InsertStep`, `ChangeStepAction`) | the index error wins | agent: check order in both methods | Task 1 index pins |
| `RemoveSteps` with duplicate or bad indices | duplicates count once; the first bad index in input order fails the call and is the index on the error | agent: `RecipeSession.RemoveSteps` | Task 1 index pins |
| `RemoveSteps` accepted | suggests the lowest removed index clamped to the new last row, or no selection when the recipe is empty | agent: `RecipeSession.RemoveSteps` | Task 1 tail pins |
| 101st accepted edit | the oldest undo entry drops; exactly 100 undos remain | agent: `RecipeHistory.MaxDepth` | Task 1 history pins |
| Three undos, then redos | three redos restore each state in order; a fourth fails with `NoStateToRedoError` | agent: `RecipeSession.Redo` | Task 1 history pins |
| Undo or redo with an empty stack | fails with `NoStateToUndoError` or `NoStateToRedoError` | agent: `RecipeSession.Undo`, `Redo` | Task 1 history pins |
| Undo or redo after `MarkSaved` | succeeds and sets dirty | agent: `UpdateSnapshot` marks dirty | Task 1 history pins |
| `LoadAsCurrent` of a recipe that fails analysis | clears history, the snapshot becomes the failure, `Current` is empty, `LastValidRecipe` keeps the prior recipe, dirty is set | agent: `RecipeSession.LoadAsCurrent` | Task 1 history pins |
| `Reset` | clears history and dirty | agent: `RecipeSession.Reset` | characterization suite |

## Decisions
- `IsDirty` stays on `RecipeSession`. The issue puts the dirty flag into `RecipeHistory`, but every
  writer of the flag is in the session (`UpdateSnapshot`, `ResetSnapshotToEmpty`, `MarkSaved`), and
  no history operation reads it. Moving it would make the history a second owner of state that the
  session alone changes. `RecipeHistory` holds the two stacks and the cap only.
- `RecipeSession` creates its own `RecipeHistory`. The history is per-session state, and the session
  is a singleton. Rejected: a DI registration. Nothing else consumes the history.
- `RecipeSession` creates its own `StepMutationValidator` from the `RecipeMetadataRegistry` it
  already receives. The validator has no state and no second consumer. Rejected: a DI registration.
  `ImportedRecipeValidator` is injected because it is a shared DI singleton with a second consumer,
  `ClipboardViewModel`.
- "Action by id" stays out of the validator. `AppendStep`, `InsertStep` and `ChangeStepAction` keep
  calling `RecipeMetadataRegistry.GetAction` directly: a one-call forward would add a layer and save
  no line.
- Undo and redo use a two-step history API: read the top entry, then move the stacks only after the
  session's analysis of that entry succeeds. A failed analysis leaves both stacks as they were,
  as today.
- Both new classes are `internal sealed`. The public surface of `RecipeSession` stays the same apart
  from the `LoadAsCurrentValidated` parameter.
- The validator does not log. Every log line stays in `RecipeSession`, under the `RecipeSession`
  logger category, with today's text.
- Task 1 pins behavior before any code moves, in its own commit with no production change. The
  existing suite checks `IsFailed` on index errors but not the error type, and it does not cover sync
  notification counts, multi-step redo, the depth cap, dirty after undo, redo or load, warnings on
  accepted edits, or the check order.
- The `ImportedRecipeValidator` injection stays in this branch, because it is part of the shape #116
  names. It is the last code task, in its own commit, so it can be reverted alone.

## Caveats
- The issue says 651 lines, seven mutation entry points and six repeated skeletons. Today's file
  has 610 lines, eight mutation entry points, seven index checks and five action lookups.
- DI: `ImportedRecipeValidator` depends only on `RecipeMetadataRegistry`, which depends only on
  `AppConfiguration`. Injecting the validator into `RecipeSession` adds no cycle.
  probed: `dotnet test --filter "Area=Characterization"` resolves `RecipeSession` and
  `ImportedRecipeValidator` from the container through `CoreTestHelper`; 43 passed, 2026-11-24.
  ASSUMPTION: the container resolves the new constructor parameter with no registration change;
  fallback: stop and report the container's resolution error; checked by: the Task 5 test run.
- `PlcLifecycleManager.cs` is open in the #180 lane. This plan edits only its constructor parameter
  list, the matching field, `ApplyRecipeFromPlc` and the private load helper it inlines. A merge
  conflict there is textual and small.

## Constraints and non-goals
- Behavior does not change. No existing test assertion is removed or rewritten. The only edits to
  existing tests are constructor arguments and the two `LoadAsCurrentValidated` call sites in the
  characterization class.
- No new interface. No change to `IPlcSyncService`, `NotifySyncIfEnabled` or the sync call order.
- No new dependency.
- Threat model: the same bad inputs as today (out-of-range indices, unknown action ids, unparsable
  cell text, untrusted CSV and PLC recipes). No new guards.
- Tests stay simple. Session-level pins go through the public surface. Unit tests of the new classes
  cover only what the session pins do not already prove, and do not repeat them one for one.

## Gates
- After Task 4, if `RecipeSession.cs` is above 500 lines and getting under it needs code outside
  the two extractions and the tail helper, stop and report the count.

## Acceptance
- `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
- `git show --stat 84734e4 -- SemiStep/SemiStep.Core SemiStep/SemiStep.UI` → empty output.
  (Task 1 commit after the rebase onto #215: `84734e4`; the squash merge keeps it only in the PR's
  per-task log.)
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Characterization"` →
  0 failed; 74 passed (43 on master, 27 Task 1 pins, 4 review-round pins).
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
- `git diff origin/master...HEAD -- SemiStep/SemiStep.Tests | grep '^-[^-]' | grep -c 'Should()\|Assert\.'`
  → 0.
- `git diff -U0 origin/master...HEAD -- SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs | grep -c '^@@.*DisableSync'`
  → 0.
- `wc -l SemiStep/SemiStep.Core/Recipes/RecipeSession.cs` → 500 or fewer.
- `grep -c "ImportedRecipeValidator" SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
  → 0 for both files.
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
- manual smoke: 1. start the app, add three steps, edit a duration, press Ctrl+Z three times and
  Ctrl+Shift+Z once → the grid shows each prior state in order, and the window title ends with ` *`.
  2. press Ctrl+S, then Ctrl+Z → the title ends with ` *` again.
- not visible to headless tests: none beyond the smoke steps.

---

## Context
- `SemiStep.Core/Recipes/RecipeSession.cs`: the history lives in the two stack fields,
  `MaxHistoryDepth`, `PushHistory`, `ClearHistory`, `Undo`, `Redo`. The dirty flag is written in
  `UpdateSnapshot`, `ResetSnapshotToEmpty` and `MarkSaved`. The validation lives in
  `ParseAndValidateColumnValue`, `ValidateStepIndex`, `ValidateInsertIndex` and the step-action
  lookups inside `UpdateStepProperty` and `UpdateStepForSelectorChange`.
- `SemiStep.Core/Recipes/RecipeDi.cs`: `AddRecipe` registers `ImportedRecipeValidator`,
  `RecipeSession` and `PlcLifecycleManager` as singletons.
- `SemiStep.Core/Plc/PlcLifecycleManager.cs`: the constructor takes `ImportedRecipeValidator`; its
  private `ValidateAndLoad`, called from `ApplyRecipeFromPlc`, is the only reader.
- `SemiStep.UI/Coordinator/RecipeCoordinator.cs`: the constructor takes `ImportedRecipeValidator`;
  the CSV load path is the only reader. The comment about the main-thread hop sits above the
  validator field, not above the observables it describes.
- `SemiStep.Tests/Helpers/StubPlcSyncService.cs` records every `NotifyRecipeChanged` call with its
  recipe and validity; `SetSyncEnabled(true)` turns notification on.
- Test construction sites: three `new RecipeSession(` (in `RecipeSessionFormulaIntegrationTests`,
  `RecipeSessionSelectorSeedIntegrationTests`, `RecipeSessionValueRangeTests`) and five
  `new RecipeCoordinator(` (in `UIFixture`, `RecipeCoordinatorLoadRecipeTests`,
  `RecipeCoordinatorSaveGateTests`, `RecipeRowSelectorEditTests`, `TransposedSelectorEditTests`).
- Error assertions check types, not wording (`docs/architecture/error-reporting.md`).
- New session pins go into `RecipeSessionBehaviourCharacterizationTests` (`Area=Characterization`).

## Boundaries
- `RecipeHistory` (Task 2): push a recipe (clears redo, drops the oldest past 100), clear, read the
  top undo entry, read the top redo entry, commit an undo (current recipe onto redo, top undo
  removed), commit a redo (current recipe onto undo, top redo removed), the undo count, and whether
  redo is possible. `RecipeSession.CanUndo` derives from the undo count. The undo count's only
  consumers are `RecipeSession.CanUndo` and the `internal` `RecipeSession.UndoCount` that the tests
  read. One private restore helper in the session serves both `Undo` and `Redo`: analyze the top
  entry, commit the move, update the snapshot, notify sync.
- `StepMutationValidator(RecipeMetadataRegistry)` (Task 3): step index check, insert index check,
  "index check, then the action of the step at that index", and parse-and-validate of a column value
  for an action. Each returns a `Result` with today's error types.

## Tasks

### Task 1: Pin the behavior the moved code owns

**Goal:** the characterization suite covers every rule that Tasks 2 to 5 move, on unchanged production code
**Entry:** `RecipeSessionBehaviourCharacterizationTests`
**Likely touches:** `SemiStep.Tests/Core/RecipeSessionBehaviourCharacterizationTests.cs`

- [x] tests (sync pins): with sync enabled on `StubPlcSyncService`, `Apply`, `Undo`, `Redo`, `Reset`, a successful `LoadAsCurrent` and a `LoadAsCurrent` that fails analysis each record exactly one notification carrying the post-change `Current` and `IsValid`; a rejected edit, an undo on an empty stack and a failed `LoadAsCurrentValidated` record none
- [x] tests (history pins): three edits and three undos, then three redos restore each state in order and a fourth fails with `NoStateToRedoError`; after 101 accepted edits exactly 100 undos succeed and the next fails with `NoStateToUndoError`; empty-stack redo carries `NoStateToRedoError`; undo and redo after `MarkSaved` set dirty; `LoadAsCurrent` sets dirty on success and on failed analysis, and on failed analysis clears history, empties `Current` and keeps `LastValidRecipe`
- [x] tests (index pins): a step index of -1 or of the step count fails with `StepIndexOutOfRangeError`; an insert index of -1 or above the count fails with `InsertIndexOutOfRangeError`, and an insert at the count succeeds; a bad index beats an unknown action in `InsertStep` and `ChangeStepAction`; `RemoveSteps` counts duplicates once and fails on the first bad index in input order, with that index on the error
- [x] tests (tail pins): `AppendStep` of a ForLoop succeeds and carries `UnclosedForLoopWarning`; `UpdateStepProperty` on a recipe with an unclosed loop succeeds and carries it; `RemoveSteps` suggests the clamped lowest index, and no selection when every step is removed
- [x] run tests: the full suite passes on unchanged production code; write the Task 1 commit hash and the characterization count into Acceptance; commit this task alone

### Task 2: Extract RecipeHistory

**Goal:** the undo stack, redo stack and depth cap live in `RecipeHistory`; `Undo` and `Redo` share one restore helper
**Entry:** `RecipeSession.PushHistory`, `RecipeSession.Undo`, `RecipeSession.Redo`
**Likely touches:** `SemiStep.Core/Recipes/RecipeHistory.cs` (new), `SemiStep.Core/Recipes/RecipeSession.cs`

- [x] create `RecipeHistory` per Boundaries; the session owns one instance
- [x] `Undo` and `Redo` read the top entry, then call the shared restore helper, which commits the move only after analysis succeeds; each keeps its own log lines
- [x] `CanUndo`, `CanRedo` and `UndoCount` on the session read through to the history; `IsDirty` stays in the session
- [x] tests: a `RecipeHistory` unit test that commit-redo after commit-undo restores the stack sizes, which the session pins reach only through analysis
- [x] run tests: full suite passes

### Task 3: Extract StepMutationValidator

**Goal:** the seven indexed mutation methods and the two cell-edit methods get their checks from `StepMutationValidator`
**Entry:** `RecipeSession.ParseAndValidateColumnValue`, `ValidateStepIndex`, `ValidateInsertIndex`
**Likely touches:** `SemiStep.Core/Recipes/StepMutationValidator.cs` (new), `SemiStep.Core/Recipes/RecipeSession.cs`

- [x] create `StepMutationValidator` per Boundaries; the session builds it from its registry
- [x] replace the index checks in the seven indexed methods and the index-then-step-action preamble in `UpdateStepProperty` and `UpdateStepForSelectorChange`; keep each method's check order
- [x] update the `RecipeSession` XML summary to the concerns it keeps
- [x] tests: no new unit test; the Task 1 index pins and the existing parse and value-range tests cover every validator branch
- [x] run tests: full suite passes

### Task 4: Collapse the apply-then-outcome tail

**Goal:** the six structural mutation methods share one helper that applies the new recipe and builds the outcome
**Entry:** `RecipeSession.AppendStep`, `RemoveSteps`
**Likely touches:** `SemiStep.Core/Recipes/RecipeSession.cs`

- [x] add one private helper that applies the recipe, returns the failure as today, and otherwise builds the `MutationOutcome` from the post-apply step count with the apply reasons attached
- [x] route `AppendStep`, `InsertStep`, `RemoveStep`, `RemoveSteps`, `InsertSteps` and `ChangeStepAction` through it with today's selection rule each
- [x] check the Gate with `wc -l SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`
- [x] tests: the Task 1 tail pins (warnings on both result shapes, `RemoveSteps` selection) and the existing selection pins cover it; no new test
- [x] run tests: full suite passes

### Task 5: Inject ImportedRecipeValidator into RecipeSession

**Goal:** `LoadAsCurrentValidated(Recipe)` validates with the injected validator; the two callers drop the dependency
**Entry:** `RecipeSession.LoadAsCurrentValidated`
**Likely touches:** `RecipeSession.cs`, `PlcLifecycleManager.cs`, `RecipeCoordinator.cs`, the three `new RecipeSession(` and five `new RecipeCoordinator(` test sites, the characterization test file

- [x] add the constructor parameter to `RecipeSession`; drop the method parameter
- [x] remove the parameter and field from `PlcLifecycleManager` and inline `ValidateAndLoad` into `ApplyRecipeFromPlc`, without touching `DisableSync`
- [x] remove the parameter and field from `RecipeCoordinator` and move the main-thread-hop comment above the observable properties it describes
- [x] tests: update the construction sites and the two `LoadAsCurrentValidated` calls with no assertion change; the existing `LoadAsCurrentValidated` pins, the Task 1 sync pin for a failed validated load, and the PLC reconnect and coordinator load tests cover both callers
- [x] run tests: full suite passes; commit this task alone

### Task 6: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented; write the final `RecipeSession.cs` line count into Behavior
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [ ] manual smoke steps (operator smoke)

### Task 7: Update documentation

**Goal:** the architecture docs name the class that owns each moved rule
**Entry:** `docs/architecture/error-reporting.md` (interactive-edit path), `docs/architecture/nested-actions.md` (one-undo-unit selector edit)

- [x] `error-reporting.md`: the interactive-edit path runs through `StepMutationValidator`; index errors come from the validator
- [x] `nested-actions.md`: the undo boundary stays `RecipeSession.Apply`; the stacks live in `RecipeHistory`

## Post-Completion
- the operator's manual test: the Acceptance smoke steps
- closes #116
