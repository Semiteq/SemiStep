# Change marks survive undo and redo

## Behavior

- Today Ctrl+Z and Ctrl+Y remove every orange change mark in the recipe. One undo of any edit
  wipes all marks, and a redo of an action change brings the row back unmarked.
- After this change, undo and redo show each recipe state with the marks it had when the operator
  left that state. An action change, then an unrelated edit, then Ctrl+Z keeps the action-change
  marks. Ctrl+Z on the action change shows the old row with its old marks. Ctrl+Y on it shows the
  reset row marked again.
- A fresh recipe starts with no marks: CSV load, PLC load, conflict resolve with the PLC recipe,
  reconnect apply and New recipe.
- The canonical grid and the transposed grid show the same marks after every undo and redo.
- "Снять отметки изменений" adds no undo step and does not make the recipe dirty. After a clear,
  Ctrl+Z shows the earlier state with the marks it had.
- "Снять отметки изменений" is enabled after an undo or redo exactly when the restored state has
  marks.
- Scope guard: the marking rule, the clear triggers (edit, click-away, clear all, execution start),
  `MutationSignal`, the undo history in `SemiStep.Core`, the dirty flag, PLC sync and the selection
  reset on undo stay as they are.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Action change, unrelated edit, Ctrl+Z | the edit is undone; the action-change marks stay | operator: D7 | Acceptance 2.1 |
| Ctrl+Z on an action change | the old row comes back with the marks it had before the change | operator: D7 | Acceptance 2.2 |
| Ctrl+Y on that action change | the reset row comes back with the marks it had when undone | operator: D7 | Acceptance 2.2 |
| Edit of a marked cell, then Ctrl+Z | the old value comes back marked: it is still the unreviewed default | agent: marks are restored as left | Acceptance 2.3 |
| Delete a marked row, then Ctrl+Z | the row comes back with its marks | agent: marks are restored as left | Acceptance 2.4 |
| Paste, then Ctrl+Z, then Ctrl+Y | pasted rows stay unmarked; other rows keep the marks they had | operator: D7 "paste marks nothing" | Acceptance 2.5 |
| CSV load, PLC load, reconnect apply | every mark is gone on both grids; the clear-marks command is disabled | operator: D7 | Acceptance 2.6 |
| New recipe, conflict resolve with the PLC recipe | same as a load | agent: a fresh recipe is a load | Acceptance 2.6 |
| "Снять отметки изменений", then Ctrl+Z | the clear adds no undo step; Ctrl+Z undoes the last recipe edit and shows that earlier state with the marks it had | manager: autonomous-decisions.md 5 | Acceptance 2.7 |
| Action change A, action change B, clear all, Ctrl+Z | the state after A comes back with A's marks; B's row is reverted | manager: autonomous-decisions.md 5 | Acceptance 2.7 |
| Clear all, edit, Ctrl+Z | the cleared state comes back unmarked: the clear is part of that state as left | manager: autonomous-decisions.md 5 | Acceptance 2.8 |
| Click-away clear, or a run start and end, then Ctrl+Z | same rule as clear all: the clear changed only the state it ran on | agent: one rule for every clear | Acceptance 2.11 |
| Undo or redo fails (analysis rejects the stored recipe) | no signal goes out; rows and marks stay as they are | agent: `RecipeCoordinator.TrackVoid` dispatches only on success | not tested: the dispatch-on-success rule is unchanged code |
| History passes 100 entries | the oldest state drops out of history; its stored marks go with it | agent: `RecipeHistory.Push` trims, the store holds no strong reference | not tested: no observable effect |

## Decisions

- Marks live in the UI, in a store each grid surface owns, keyed by the `Recipe` instance of the
  history point (manager: autonomous-decisions.md 4). Reason: Core never sees most mark changes.
  Click-away, the edit clear, clear all and execution start fire no mutation, and
  `RecipeSession.Apply` pushes the outgoing recipe to history before the UI hears of the mutation.
  The `Recipe` instance already is the identity of a history point: `RecipeHistory` stores
  references, `RecipeSession.RestoreFromHistory` analyzes the stored instance, and
  `RecipeAnalyzer.Analyze` passes it unchanged into `RecipeSnapshot.Create`, so after Ctrl+Z
  `RecipeCoordinator.CurrentRecipe` is the same instance the operator left. Every `Recipe` mutation
  method returns `this with { ... }`, a new instance, so two history points never share an
  instance.
  rejected: marks in the Core history next to the recipe. Every mutation entry of `RecipeSession`
  (eight paths) plus `Undo` and `Redo` would have to take the outgoing marks from the UI, or Core
  would call back into the UI for them. Core then carries a UI concept, against the layering rule.
  rejected: a UI map keyed by an integer history position. It must copy `RecipeHistory` behavior
  (redo cleared on push, oldest entry dropped at depth 100, failed restore leaves the stack), which
  breaks "one algorithm lives in one place" and drifts when Core changes.
- Each surface keeps its own store and is its only writer. Both surfaces already reach the same
  marks from the same signals, the shared clear broadcaster, and their own execution trackers. A
  shared store would have two writers. (Easy to reverse.)
- The store captures the rows' marks on every mutation signal, before the rows change, under the
  recipe the rows showed. When `Mutated` arrives, Core already holds the new recipe but the rows
  still show the old state, so this is the only point where the outgoing marks are complete.
- After every `RecipeReplaced` rebuild, the surface restores the marks stored for the new current
  recipe, if the store has an entry. Loads find no entry because every load yields a fresh
  instance (see Caveats), so `RecipeReplaced` stays as it is and needs no reason.
  rejected: a load or history-move reason on `RecipeReplaced` (manager: KISS). The instance
  identity already tells the two apart; the reason would add a parameter at seven dispatch sites
  and a second source of truth.
- "Снять отметки изменений" adds no undo step and does not make the recipe dirty. After a clear,
  Ctrl+Z shows the earlier state with the marks it had (manager: autonomous-decisions.md 5).
  Reason: undo returns a past state as it was shown, and the clear changes no recipe data; an undo
  step for it would make Ctrl+Z skip real edits. The same rule covers click-away and the
  execution-start clear. Cost: after a clear, Ctrl+Z can show orange on the earlier state; the
  operator clears it again.
  rejected: clear all also wipes the stored marks of every history point. It changes how past
  states are shown, and it needs a second rule for the single-cell clears, whose cells shift
  between history points.
- A restore sets the stored set verbatim and bypasses the marking rule. The rule compares a new
  value with the previous value at mark time; at restore time that previous value is gone.

## Caveats

- `ConditionalWeakTable` matches keys by reference, not by `Equals`. This matters because
  `Recipe.Equals` compares steps by content: an edit to X and back to X gives two equal recipes
  that must keep separate marks. The table also lets a recipe that left history be collected
  together with its marks.
  probed: `dotnet run probe.cs` with two equal records as keys, `equal=True foundB=False
  foundA=True`, 2026-11-25.
- Every load yields a `Recipe` instance the UI never showed, except New recipe, which yields the
  shared `Recipe.Empty`. That instance has no steps, so its stored marks are always empty and a
  restore onto zero rows sets nothing.
  probed: code read, 2026-11-25. CSV load: `CsvFileSerializer.Deserialize` builds
  `new Recipe([.. steps])`, and `CsvService.LoadAsync` and `ImportedRecipeValidator` pass it
  through. PLC load: `PlcTransactionExecutor.ReadRecipeFromPlcAsync` returns
  `RecipeConverter.ToRecipe`, which builds `new Recipe([.. steps])` from the bytes read.
  Reconnect apply and conflict resolve take the recipe from the same reader in
  `PlcLifecycleManager.CompareWithPlcRecipeAsync`; conflict resolve loads the pending instance
  stored there. New recipe: `RecipeSession.Reset` analyzes `Recipe.Empty`.
- The test stub `StubS7Service.RecipeToReturn` returns the same instance on every read. A test
  that loads twice from the stub must set a new instance before each load, or the second load
  finds the marks stored for the first.
- ASSUMPTION: batch A ships the D7 marking rule (a cell is marked when its new value differs from
  the previous row's value for the same key, an absent key counts as empty) before it merges. On
  2026-11-25 batch A still holds the older empty-seed rule. fallback: the plan does not depend on
  the rule; restore is verbatim, and the acceptance tests use an action change from Wait to For,
  whose keys differ under both rules. checked by: Task 1.
  result: confirmed on origin/master c683d18. `RecipeRowViewModel.ChangedValueKeys` marks a key
  of the new step whose value differs from the previous step's, an absent key counts as empty;
  `RebuildItem` and `RecipeRowUpdateSynchronizer` both call it.
- ASSUMPTION: the PLC load test route works through `RecipeCoordinator.LoadRecipeFromPlcAsync` with
  `StubS7Service.RecipeToReturn` set on the fixture's reader. No UI test drives it today.
  fallback: assert the PLC side through the conflict-resolve and reconnect routes of 2.6, which
  feed the same kind of fresh instance. checked by: Task 2.

## Constraints and non-goals

- `SemiStep.Core` and `MutationSignal` do not change: no mark, colour or annotation type enters
  Core.
- Invariant: after a successful undo or redo, `RecipeCoordinator.CurrentRecipe` is the same
  `Recipe` instance that history stored. Acceptance items 2.2 and 2.9 fail if a Core change breaks
  it, for example an analyzer that returns a rebuilt recipe.
- Invariant: a load never yields a `Recipe` instance with steps that the UI showed before. The
  extended 2.6 fails if a load path starts to return a cached instance.
- The undo history depth, the dirty flag and PLC sync calls stay as they are.
- Rows still hold their marks in `RecipeRowViewModel.ChangedColumns`; batch A's has-marks count
  keeps reading that property.
- Threat model: none; all inputs are local operator actions and recipes already validated by Core.
- Tests stay simple: headless tests through the coordinator and the surfaces, no new fakes.

## UI design

- No new visual element. Marks keep batch A's orange fill (`CellChangedBrush`) and its clear
  command.
- After "Снять отметки изменений", Ctrl+Z undoes the last recipe edit and shows that earlier state
  with the orange cells it had. The clear itself is not an undo step.

## Gates

- Capture runs on every mutation signal and copies one set reference per row. After Task 2 run
  `dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only`.
  The probes assert the thresholds in their code and in `docs/perf/baselines.json`. A non-zero
  exit code stops the run: report the failing probe and its numbers to the operator.

## Acceptance

- `dotnet build SemiStep.slnx` → exit code 0 (the build treats warnings as errors, so 0 warnings)
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ChangeHighlight"` →
  exit code 0. The new headless tests prove, on both surfaces (cited elsewhere as 2.1 to 2.11).
  Items marked "regression guard" pass on master too; the rest fail on master.
  1. action change, unrelated edit, Ctrl+Z: the action-change marks stay;
  2. Ctrl+Z on the action change: the old row with its old marks; Ctrl+Y: the reset row marked;
  3. edit of a marked cell, Ctrl+Z: the cell is marked again;
  4. delete a marked row, Ctrl+Z: the row comes back marked;
  5. other rows carry marks; paste, Ctrl+Z, Ctrl+Y: pasted rows unmarked, the other rows keep
     their marks after each step;
  6. regression guard: with marks present, each of CSV load, PLC load, New recipe, conflict
     resolve with the PLC recipe (`ResolveConflict(false)`, the `MainWindowConflictResolutionTests`
     route) and reconnect apply (`ApplyReconnectPlcRecipeAsync`, the `RecipeCoordinatorTests` stub
     route) leaves no marks and disables the clear-marks command;
  7. two action changes, clear all, Ctrl+Z: the clear adds no undo step and leaves the recipe
     clean as before (regression guard), and the state after the first change shows its marks;
  8. regression guard: clear all, edit, Ctrl+Z: the cleared state comes back unmarked;
  9. change an action so cell C is marked, edit C to another value, edit C back to the default,
     Ctrl+Z twice: C is marked (an `Equals`-keyed store fails this);
  10. the clear-marks command is enabled after an undo or redo exactly when marks are restored;
  11. action change, unrelated edit, click-away clear of every mark, Ctrl+Z: the action-change
      marks come back, Ctrl+Y: no marks; the same with a run start and end in place of the
      click-away.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
- manual smoke (skipped - manager live check):
  1. change a step's action, edit a cell in another step, press Ctrl+Z → the action-change cells
     stay orange
  2. press Ctrl+Z again → the old action comes back, no orange on that row
  3. press Ctrl+Y → the new action comes back with the same orange cells
  4. switch to the transposed view → the same cells are orange
  5. load a CSV file → no orange anywhere, "Снять отметки изменений" is disabled
- not visible to headless tests: the orange brush itself on screen (smoke steps 1 to 4).

---

## Context

- `SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`: `OnMutation`, `FullRebuild`, `Initialize`;
  batch A's mark tracking (`HasChangedMarks`, the marked-row set). The file is past the 300-line
  cap, so the store goes into its own small class in `SemiStep.UI/RecipeGrid/` that the base owns.
  No interface: one implementation.
- `SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`: the mark API (`MarkChanged`,
  `ApplyChangedDelta`, `ClearChanged`, `ClearAllChanged`). `ChangedColumns` is replaced, never
  mutated in place, so a captured set stays valid.
- `SemiStep.UI/Coordinator/RecipeCoordinator.cs`: the seven `RecipeReplaced` dispatch sites; read
  only.
- `SemiStep.Core/Recipes/RecipeSession.cs`, `RecipeHistory.cs`, `Analysis/RecipeAnalyzer.cs`:
  read-only evidence for the instance identity; not edited.
- Tests: `SemiStep.Tests/UI/RecipeGrid/ClearChangeMarksTests.cs` (batch A) shows the fixture setup;
  `SemiStep.Tests/UI/RecipeCoordinatorLoadRecipeTests.cs` shows the CSV load route;
  `SemiStep.Tests/UI/MainWindow/MainWindowConflictResolutionTests.cs` the conflict-resolve route;
  `SemiStep.Tests/UI/RecipeCoordinatorTests.cs` the reconnect-apply route. No test constructs
  `RecipeReplaced`; `BeOfType` checks compile unchanged.
- Docs: `docs/architecture/cell-change-highlight.md`.

## Tasks

### Task 1: Rebase onto master with batch A merged

**Goal:** the branch starts from master that holds batch A, with a green baseline
**Entry:** `git fetch origin && git rebase origin/master`

- [x] confirm `git log origin/master --oneline` holds batch A (the clear change marks command)
- [x] rebase the branch; only the plan commits sit on top
- [x] check the Caveats ASSUMPTION on the D7 marking rule in `RecipeRowViewModel`; record the
      result in the plan
- [x] run build and full test suite - must pass before the next task

### Task 2: Store and restore marks per history point

**Goal:** undo and redo show each state with the marks it had when left, on both surfaces; loads
start empty
**Entry:** `RecipeGridSurfaceBase.OnMutation`
**Likely touches:** `RecipeGridSurfaceBase.cs`, a new store class in `SemiStep.UI/RecipeGrid/`,
`RecipeRowViewModel.cs`, a new test file with traits `Component=UI`, `Area=ChangeHighlight`,
`Category=Integration`

- [x] the row can take a stored mark set verbatim: `ChangedColumns` becomes exactly those keys,
      the change notification fires, and the marking rule does not filter them
- [x] the surface remembers the recipe its rows show: the empty recipe before `Initialize`, then
      `CurrentRecipe` after `Initialize` and after every signal, a failed projection included
- [x] on every mutation signal, before the rows change, capture the rows' marks under the
      remembered recipe
- [x] after every `RecipeReplaced` rebuild, restore the marks stored for the new current recipe
      when an entry exists; a step-count mismatch restores nothing and logs a warning
- [x] tests: Acceptance items 2.1 to 2.11, each asserted on the canonical and transposed rows,
      2.6 with all five load routes. No existing test pins "undo wipes marks" (grep for Undo/Redo
      next to `ChangedColumns`, 2026-11-25); update any test that fails because it relied on that
- [x] run tests - must pass before the next task

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0
- [x] run the explicit Performance probes per the Gates section

### Task 4: Update documentation

**Goal:** the docs describe how marks behave on undo, redo and loads
**Entry:** `docs/architecture/cell-change-highlight.md`

- [x] `cell-change-highlight.md`: a section on undo and redo (store per surface, keyed by the
      recipe instance, capture on every signal, restore after every `RecipeReplaced`, loads find
      no entry because each load yields a fresh instance); extend the clear-all paragraph with
      "after a clear, Ctrl+Z shows the earlier state with the marks it had"
- [x] `docs/readme.md` section 2.4.2 (the user guide, in Russian): undo and redo show each state
      with the marks it had when left, Ctrl+Z after a clear can bring the earlier marks back, and
      open, PLC load and New recipe start without marks

## Post-Completion

- the operator runs the manual smoke steps from Acceptance
- no changes outside this repository
