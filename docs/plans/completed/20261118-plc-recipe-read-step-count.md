# Read a PLC recipe back with the right step count

## Behavior

- Today the app cannot read a recipe back from the PLC when its steps carry integer values besides
  the action id (group selections, enum columns). The read takes the number of integers in the
  integer array as the number of steps, runs out of values in the middle of the recipe and fails.
  Reconciliation on connect then treats the PLC recipe as unreadable and pushes the local recipe
  over it, so the operator is never asked about a real conflict (one cause behind #158).
- After the fix the app reads the step count from the managing area's `recipe_lines`, the same
  value the PLC uses as its line count. A recipe of any shape reads back equal to what was written.
  A header that disagrees with the arrays fails the read with one typed error instead of a crash or
  a silently truncated recipe.
- Verification after a write also checks that `recipe_lines` in the PLC equals the written step
  count; today only the arrays are compared.
- A test transport that stores what is written lets a test write a recipe and read it back through
  the real executor and codec.
- Scope guard: the wire layout stays as it is (fixed-size rows are #174, a separate plan). No
  change to the write sequence, the commit handshake, `IS7Reader`/`S7Service`, or the UI.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| PLC holds a committed recipe whose steps carry extra int columns | Reads it back equal to the written recipe | agent: the read uses `recipe_lines` as the step count (`PlcTransactionExecutor.ReadRecipeFromPlcAsync` already holds the managing area) | Acceptance 2 |
| PLC holds a recipe of id-only steps | Reads it back as today | agent: same path | Acceptance 2 |
| `recipe_lines` is 0 and committed | Reads an empty recipe | agent: `RecipeConverter.ToRecipe` returns `Recipe.Empty` for 0 steps | Acceptance 2 |
| `recipe_lines` is negative | Read fails with the typed length error; no exception escapes | agent: `new List<Step>(n)` throws on a negative count, and `ToRecipe` runs outside any try/catch | Acceptance 2 |
| `recipe_lines` says more steps than the arrays hold | Read fails with the typed length error | agent: today the converter fails with a plain-string error | Acceptance 2 |
| `recipe_lines` says fewer steps than the arrays hold | Read fails with the typed length error; trailing values are not dropped silently | agent: a corrupt header must not pass as a shorter recipe | Acceptance 2 |
| Verify after a write | Passes when arrays and `recipe_lines` match what was written; fails when `recipe_lines` differs | agent: `PlcRecipeDataComparer` compares only array lengths and values today | Acceptance 3 |

## Decisions

- The step count comes from the managing area's `recipe_lines`. The PC writes it last before the
  commit flag, and the PLC uses it as its line count (TIA_Plazma export, example only). rejected:
  deriving the step count by walking the int array with the action metadata, because it duplicates
  the converter and hides a corrupt header.
- The recipe-data read takes the step count as a required argument. Only `ReadRecipeFromPlcAsync`
  passes it from the managing area it already read; verify reads the managing area itself. The
  reconciliation path in `PlcLifecycleManager` keeps calling `ReadRecipeFromPlcAsync` unchanged.
  rejected: an optional argument (two modes in one method) and threading the value through
  `IS7Reader` (wider change, stale value).
- One new typed error for "recipe header and arrays disagree", carrying the header step count and
  which array ran out or had leftovers. Its production consumers are the reconciliation log outcome
  and the message panel through `ReasonLocalizer` (English message plus `ru` resource, enforced by
  the coverage and parity tests). It replaces the plain-string "Insufficient ... values" failures.
- The storing transport lives in `SemiStep.Tests` next to `FakeS7Transport`, not in production code.
- The defect gets its own GitHub issue, referenced in the commit header.

## Caveats

- `PlcRecipeDataComparer.DataMatchesExpected` compares array lengths and values only, never
  `StepCount` (read from source, plan review 2026-11-18). Verify therefore passes today for recipes
  with extra int columns; the new header check is what Acceptance 3 adds.
- The managing area is written as separate slices at `CommittedOffset` and `RecipeLinesOffset`, so
  the storing transport must handle writes at non-zero offsets and reads of regions never written.

## Constraints and non-goals

- No change to the wire layout, `ArrayCodec`, the managing-area codec, the write order,
  `IS7Reader`, `S7Service` or the UI.
- Tests stay simple: one storing transport, the shipped RIE config via
  `ShippedConfigLocator.GetConfigDirectory("RIE")`, no timers, no sockets.

## Acceptance

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcTransactionExecutor"`
   → passes, and includes write-then-read tests on the storing transport for: a multi-step RIE
   recipe with extra int columns, an id-only recipe, an empty recipe; and read tests for a negative,
   a too-large and a too-small `recipe_lines`, each asserting the typed length error.
3. Same filter → includes a test that verify passes for a correct write and fails when
   `recipe_lines` in the PLC differs from the written step count.
4. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed (includes the
   localization coverage and English parity tests for the new error).
- not visible to headless tests: behaviour against a real PLC.

---

## Context

- `SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs`: `ReadRecipeDataAsync`,
  `ReadRecipeFromPlcAsync`, `VerifyWriteAsync`, the write sequence.
- `SemiStep/SemiStep.Core/Plc/S7/Serialization/RecipeConverter.cs`: `ToRecipe`.
- `SemiStep/SemiStep.Core/Plc/State/PlcRecipeData.cs`, `PlcManagingAreaState`,
  `PlcRecipeDataComparer`.
- `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs`, `Resources.resx`, `Resources.ru.resx`.
- `SemiStep/SemiStep.Tests/S7/Helpers/FakeS7Transport.cs`,
  `SemiStep/SemiStep.Tests/S7/PlcTransactionExecutorTests.cs`, `ShippedConfigLocator`.
- Test rules: `docs/architecture/error-reporting.md`, section "What tests assert on errors".

## Tasks

### Task 1: Storing transport, round-trip tests and the step-count fix

**Goal:** a recipe of any shape reads back equal to what was written; a bad header fails typed.
**Entry:** `SemiStep/SemiStep.Tests/S7/Helpers/FakeS7Transport.cs`, then
`PlcTransactionExecutor.ReadRecipeDataAsync`
**Likely touches:** `PlcTransactionExecutorTests.cs`, `RecipeConverter.cs`, a new error type,
`ReasonLocalizer.cs`, the resx files

- [x] file a GitHub issue for the defect (problem, reproduction, link to #158)
- [x] a test transport that keeps written bytes per DB at any offset and serves reads from them
- [x] write the round-trip and header tests from Acceptance 2 first and see the extra-int-column
      cases fail on the current code (red check, noted in the progress log, not committed red)
- [x] the read takes the step count as a required argument from `recipe_lines`; the converter
      reports the typed length error for negative, too-large and too-small counts
- [x] run tests - must pass before the next task

### Task 2: Verify checks the header

**Goal:** a write whose `recipe_lines` did not land as written fails verification.
**Entry:** `PlcTransactionExecutor.VerifyWriteAsync`

- [x] verify reads the managing area and compares `recipe_lines` with the written step count,
      in addition to the array comparison
- [x] tests from Acceptance 3, red-checked first
- [x] run tests - must pass before the next task

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] `dotnet format SemiStep.slnx --verify-no-changes` exits 0
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 4: [Final] Update documentation

**Goal:** docs describe where the read step count comes from
**Entry:** `docs/plans/roadmaps/20260807-plc-sync-reconciliation-roadmap.md`, `docs/architecture/`

- [x] note in the #158 roadmap that the read-path defect is fixed and its manual gate no longer
      depends on it
- [x] update architecture docs if they describe the read sequence

## Post-Completion

- manual check against the emulator or a PLC once one is available: write an RIE recipe with
  group columns, reconnect, see the conflict dialog when the local recipe differs.

**Executed by exec:**
- branch: plc-recipe-read-step-count

## Verify it yourself

No manual repro exists without a PLC or the emulator: the defect lives in the read path behind
the S7 connection. The demonstrating tests run headless.

1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcTransactionExecutor"`
   → 24 passed. The extra-int-column round trip and the header tests fail against the code of
   `3fb77153` (before the fix) and pass from `815274fd` on; the `recipe_lines` verify check passes
   from `600a59b0`; the `int.MaxValue` header case passes from `246a5fae`.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 1444 passed, 0 failed.
3. Once the emulator or a PLC is available: write an RIE recipe with group columns, change the
   local recipe, reconnect. Before: the local recipe silently overwrites the PLC. After: the conflict
   dialog appears.
