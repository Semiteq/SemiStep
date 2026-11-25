# Reconnect conflict for a recipe the PLC already holds

## Behavior

- Today a recipe loaded from CSV never equals its own PLC read-back. Every reconnect of sync
  shows the "different recipe in PLC" conflict dialog for it, even right after the app wrote
  that recipe. The dialog shows only step counts, so it reads "12 steps vs 12 steps" and gives
  the operator nothing to decide on.
- Cause: a step keeps only the cells that have a value. A blank CSV cell (most often an empty
  `comment`) and an inactive column of a step created in the grid (for example the RIE etch
  capacitor columns in Auto match mode) have no key in the step. The serializer writes such a
  property as `0` or an empty string, and the read-back from the PLC carries every property of
  the action. `Step.Equals` compares key sets, so the two recipes differ.
- After the change, reconnect reconciliation counts the recipes as equal when they encode to the
  same PLC data. A blank cell and an explicit `0` or empty string are the same recipe on the
  PLC, so no dialog appears. Any value that differs on the PLC still raises the dialog.
- Scope guard: `Step.Equals` and `Recipe.Equals` stay as they are; undo, dirty tracking and
  `RecipeStructuralEqualityTests` rely on them. The conflict dialog, its content and the three
  outcomes of the conflict resolution stay as they are. The write path and its read-back
  verification stay as they are. The CSV import and `StepInitializer` keep omitting blank and
  inactive cells. `RecipeConverter` keeps its visibility and its registration in `AddS7`.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Reconnect; the PLC holds the recipe the PC wrote; the local recipe has blank CSV cells | outcome `Identical`, no dialog, local recipe pushed to the sync service | agent: the converter writes a missing property as its type default | Acceptance 2 |
| Reconnect; a local step created in the grid omits inactive columns (RIE etch, Auto match) | outcome `Identical`, no dialog | agent: `StepInitializer.Create` omits inactive columns on purpose | Acceptance 2 |
| Reconnect; one property value differs on the PLC | outcome `ConflictRaised`, dialog shown | agent: the encoded data differs | Acceptance 2 |
| Reconnect; step counts differ | outcome `ConflictRaised`, dialog shown | agent: the encoded data differs | Acceptance 2 (existing tests) |
| Reconnect; a local value has a type other than its column's type, so encoding throws | outcome `ConflictRaised`, dialog shown, a Warning log line names the step | operator: a recipe the PC cannot encode is not proven equal, so the operator chooses | Acceptance 2 |
| Reconnect; a PLC step has an action the configuration does not know | outcome `RecipeReadFailed` before any comparison: `PlcTransactionExecutor.ReadRecipeFromPlcAsync` decodes through `RecipeConverter.ToRecipe`, which fails on an unknown action; the local recipe is pushed | existing behavior | existing tests |
| Local recipe empty, PLC not empty | unchanged: `AppliedFromPlc` | existing behavior | existing tests |
| PLC empty | unchanged: `PlcEmpty` | existing behavior | existing tests |
| Local recipe invalid but encodes to the PLC data | `Identical`; the push carries `valid False`, so nothing is written | existing behavior of the sync service | existing tests |

A local step with an action the configuration does not know cannot reach the comparison:
`RecipeSession.LoadAsCurrent` runs `RecipeAnalyzer`, and `TimingCalculator.ExtractStepDuration`
throws `InvalidOperationException` for an unknown action id. No reconnect test covers that case.

## Decisions

- Reconnect equality is equality of the PLC data. `PlcRecipeDataComparer.EncodesToSamePlcData` first compares step counts, then
  encodes each local step and the PLC step at the same index with `RecipeConverter.FromRecipe`
  and compares the two results with `PlcRecipeDataComparer.DataMatchesExpected`. Encoding step by
  step lets the log line name the step that failed to encode. Each step writes its action key
  first, and the action key fixes the layout of the rest of the step, so equal steps at every
  index give equal whole arrays and the reverse. This is the rule the write path uses to verify
  its read-back (`PlcTransactionExecutor`): it checks `RecipeLines` against the step count
  separately, then compares the arrays.
  rejected: widening `IS7Reader` to return the raw `PlcRecipeData`. It changes a cross-layer
  interface and every stub of it, and re-encoding the PLC recipe gives the same data (see
  Caveats).
- `PlcLifecycleManager` takes `RecipeMetadataRegistry` from the container and builds its own
  `RecipeConverter`. The converter is stateless and depends only on the registry, which
  `AddRecipe` already registers next to the manager. `PlcTransactionExecutor` builds its
  `ExecutionStateCodec` and `ManagingAreaCodec` the same way.
  rejected: making `RecipeConverter` public and registering it in `AddRecipe`. It adds public
  surface and moves a registration that the test containers of `AddS7` rely on, for a stateless
  helper.
- An exception while encoding a step, on either side, counts as a conflict. `PropertyValue.AsInt`,
  `AsFloat` and `AsString` unbox, so a value whose type differs from its column makes
  `FromRecipe` throw `InvalidCastException`. `PlcRecipeDataComparer.EncodesToSamePlcData` catches only that exception at the
  comparison and logs it at Warning with the step number. The reconciliation then writes its
  normal `ConflictRaised` outcome line, never the unhandled-error line.
- A blank cell and an explicit type default (`0`, `0f`, `""`) compare as equal, by design. The PLC
  cannot tell them apart, and writing the local recipe would change nothing.
- Floats compare bit-exact, by design. Two values the grid shows alike (`10` and `10.9` seconds,
  both shown as `00:00:10`) still conflict, because the PLC would run different values. Display
  rounding is outside this fix.
- Keep `Step.Equals` unchanged. rejected: comparing missing keys as type defaults inside
  `Step.Equals`. Undo and dirty tracking need a key-exact comparison, and
  `RecipeStructuralEqualityTests` pins the key-count check.
- Do not normalise recipes on load. rejected: filling every missing property with its type
  default at each entry point (CSV import, `StepInitializer`, clipboard paste, PLC apply). It
  copies the serializer's default rule into a second place, and every entry point that is missed
  brings the defect back. It also contradicts `StepInitializer`, which leaves inactive columns
  out so that their PLC slot is written as `0` or empty, and it changes what a CSV round trip and
  the dirty flag see.

## Caveats

- Re-encoding a recipe read from the PLC gives back the same data: `RecipeConverter.ToRecipe`
  produces every property of the action in definition order, and `FromRecipe` writes them back in
  the same order with the same values. probed by reading the code, 2026-11-25. The opposite round
  trip is pinned by the round-trip test in `RieEtchManualBranchSerializationTests`.
- ASSUMPTION: every value in the session carries its column's type, so the encode exception is a
  guard, not an expected path. `DefaultValueValidator` rejects a `default_value` that does not
  parse to the column type, so `StepInitializer` never falls back to a string default;
  `ImportedRecipeValidator` (through `PropertyValidator`) rejects a CSV or PLC value of the wrong
  type before `LoadAsCurrentValidated` loads it. fallback: the catch above turns a missed case
  into a conflict with a Warning line; checked by: the throw-case test in Task 1, which loads a
  mistyped value through the unvalidated `LoadAsCurrent`.
- The comparison follows whatever `FromRecipe` writes. If the fixed-slot wire layout in
  `docs/plans/20260804-plc-wire-layout.md` lands, reconnect equality follows it without further
  change.
- The local recipe and its validity are still read from the session as two separate reads. The
  race described in `PlcLifecycleManager.CompareWithPlcRecipeAsync` stays as it is.

## Constraints and non-goals

- `Step.Equals`, `Recipe.Equals`, the conflict dialog, `PlcReconciliationOutcome` and the outcome
  log lines do not change.
- The CSV import, `StepInitializer` and `RecipeConverter` do not change.
- Showing what differs inside the conflict dialog is a separate change, not part of this plan.
- Threat model: none. Inputs are the PC's own recipe and the PLC read-back already decoded by the
  converter.
- Tests stay as simple as possible; no new abstraction for the comparison.

## Gates

- An existing test in `PlcLifecycleManagerReconnectTests` or `PlcReconciliationOutcomeLogTests`
  changes outcome after Task 1: stop and report. Their "recipes differ" fixtures differ in
  `step_duration` (`10` from the Standard config default against `0` in a step with no
  properties), so none should flip; a flip means a fixture relied on absent keys and the operator
  decides how to rewrite it.

## Acceptance

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Reconnect"` →
   0 failed, 40 passed (35 on master, plus 5 new reconnect tests: CSV-shaped step, real
   difference, RIE etch step created in the grid, mistyped local value,
   `WhenPlcHoldsValueInKeyLocalOmits`).
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeStructuralEqualityTests"`
   → 0 failed, with no change to that file.
4. `git diff origin/master --stat -- SemiStep/SemiStep.Core/Recipes/Step.cs SemiStep/SemiStep.Core/Recipes/Recipe.cs SemiStep/SemiStep.Core/Plc/S7/Serialization/RecipeConverter.cs SemiStep/SemiStep.Core/Plc/S7/S7Di.cs SemiStep/SemiStep.Tests/Core/Unit/Recipes/RecipeStructuralEqualityTests.cs`
   → empty output.
5. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed.
6. `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
7. manual smoke, emulator with the MBE preset (start as in
   `SemiStep/SemiStep.Tools.PlcEmulator/README.md`, section "Connect SemiStep"; the Zed task
   `Run emulator and SemiStep MBE` starts both):
   1. Open `ConfigFiles/recipes/mbe-process-example.csv` (12 steps, every `comment` cell blank)
      → the grid shows 12 steps.
   2. Enable sync and wait for the synced status → the emulator shows the recipe committed.
   3. Disable sync, then enable it again → no conflict dialog; the log has
      `PLC reconciliation outcome Identical`.
   4. Disable sync, change one recipe value in the emulator table (double click), enable sync →
      the conflict dialog appears; the log has `PLC reconciliation outcome ConflictRaised`.
- not visible to headless tests: the dialog over a real S7 connection; the emulator's read-back
  path end to end.

---

## Context

- `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`: `ResolveRecipeDifferenceAsync` holds the
  `Recipe.Equals` check that decides `ConflictRaised`. The rest of the reconciliation stays.
- `SemiStep/SemiStep.Core/Plc/S7/Serialization/RecipeConverter.cs`: `FromRecipe`, `ToRecipe`.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcRecipeDataComparer.cs`: `DataMatchesExpected`, the
  bit-exact comparison the write verification uses.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs`: its constructor builds its codecs
  itself; the manager follows that pattern for the converter.
- `PlcLifecycleManagerReconnectTests` builds its container from the `Standard` test config. The
  RIE test needs the shipped `ConfigFiles/RIE` preset; `RieEtchManualBranchSerializationTests`
  loads it through `ShippedConfigLocator`. The etch action is `300`; its default match mode Auto
  leaves `icp_load`, `icp_tune`, `rie_load`, `rie_tune` out of a step from `StepInitializer`.
- `RecipeSession.AppendStep` seeds every active column, `comment = ""` included. A CSV-shaped
  test step goes into the session through `RecipeSession.LoadAsCurrent`, never `AppendStep`,
  or the test passes on master.
- `RecipeSession.Current` is `Recipe.Empty` while the session snapshot is failed, so a test
  recipe that fails analysis reaches the comparison as an empty local recipe.
- Test conventions: `CLAUDE.md` (traits, headless rules), `docs/architecture/error-reporting.md`
  (tests assert types and outcomes, not wording).
- No page under `docs/architecture` describes reconciliation. The behavior text lives in
  `docs/readme.md`, section 4.2 and the outcome table in section 5, in Russian.

## Tasks

### Task 1: Compare recipes on reconnect by their PLC data

**Goal:** reconnect reconciliation raises the conflict only when the encoded PLC data differs
**Entry:** `PlcLifecycleManager.ResolveRecipeDifferenceAsync`
**Likely touches:** `PlcLifecycleManager.cs`, `PlcLifecycleManagerReconnectTests.cs`

- [x] tests first, in `SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs`; the local
      side goes in through `RecipeSession.LoadAsCurrent` or `AppendStep` as named, the PLC side
      through `StubS7Service.RecipeToReturn`:
      a CSV-shaped local step with no `comment` key (`LoadAsCurrent`), the PLC returning its
      converter round trip → `Identical`, no `PlcRecipeConflictDetected` (fails on master);
      the same local step, the PLC returning the round trip with one value changed → conflict;
      an RIE etch step from `AppendStep` in Auto match mode, the PLC returning its round trip →
      `Identical`, no conflict (fails on master);
      a local step whose `comment` holds an int (`LoadAsCurrent`), the PLC returning a copy →
      conflict (fails on master, which reports `Identical`);
      a PLC step holding a value under a key the local step omits → conflict
- [x] the manager takes `RecipeMetadataRegistry` and builds its own `RecipeConverter`
- [x] replace the `Recipe.Equals` check with the step count and per-step encoded-data
      comparison; an encode failure or `InvalidCastException` on either side counts as a
      conflict and logs a Warning with the step number
- [x] check the Gate: existing reconnect and outcome-log tests keep their outcomes
- [x] run tests - must pass before the next task

### Task 2: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance items 1 to 6 pass with their expected results
- [x] Acceptance item 7 (skipped - operator smoke)
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`

### Task 3: Update documentation

**Goal:** the user guide states what "the recipes match" means on reconnect
**Entry:** `docs/readme.md`, section 4.2

- [x] in section 4.2, state in Russian that recipes match when they give the same data in the
      PLC: an empty cell equals `0` or an empty string, and numbers compare exactly
- [x] in the section 5 outcome table, keep the `Identical` and `ConflictRaised` rows consistent
      with section 4.2

## Post-Completion

- the operator runs Acceptance item 7 against the emulator with the MBE preset
- no changes outside this repository
