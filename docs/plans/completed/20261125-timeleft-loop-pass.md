# Recipe time left counts the PLC loop counter as the current pass

## Behavior

- Today the status-bar "Recipe:" time left ("Рецепт:" in the Russian UI) reads too low on every
  body line of a `For` loop. The estimator counts each PLC loop counter as passes already
  completed. The PLC sends the number of the pass that runs now, so the estimate subtracts one
  whole extra pass per enclosing loop. Example from the recipe below: on line 6 at outer pass 1,
  inner pass 5, the status bar shows 00:00:22 instead of 00:00:48.
- Today the same readout reads too high on a loop end line. The estimate ignores the passes of the
  loop that line closes, so the error grows with (k − 1) × pass length. Example: on line 8 at outer
  pass 3, the status bar shows 00:00:46 instead of 00:00:02.
- After the fix, the time already consumed is the line's start time, plus the step elapsed time,
  plus `single pass duration × (counter − 1)` for each loop that counts. A loop counts on a line
  when it encloses that line, and on its own loop end line. The counter is the one at the loop's
  depth. A counter of 0 or less adds nothing, the same as pass 1.
- Scope guard: the "Step" time left, the `FOR: c1 c2 c3` readout, the idle and no-recipe texts,
  `TimingCalculator`, the snapshot's enclosing-loop map and the emulator stay as they are.

Line numbers in this plan are the 1-based row numbers the grid and the emulator state line show.
The PLC field `actual_line` is that number minus 1.

Reference recipe, MBE preset, `step_duration` in seconds, 70 s in total:

| Line | Action | Value |
|---|---|---|
| 1 | Ждать (id 10) | 2 s |
| 2 | Цикл начало (id 20) | `task` 3 |
| 3 | Ждать | 2 s |
| 4 | Цикл начало | `task` 5 |
| 5 | Ждать | 2 s |
| 6 | Ждать | 2 s |
| 7 | Цикл конец (id 30) | |
| 8 | Цикл конец | |
| 9 | Ждать | 2 s |

The same rows as a SemiStep CSV file, `loops.csv`:

```text
# SEP=";"
# ROWS="9"
action;target;initial_value;task;speed;step_duration;comment
10;;;;;2;
20;;;3;;;
10;;;;;2;
20;;;5;;;
10;;;;;2;
10;;;;;2;
30;;;;;;
30;;;;;;
10;;;;;2;
```

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Body line of one loop, pass k | consumed = line start + (k − 1) × single pass + step elapsed | agent: rule above, start times in Caveats | moved single-loop test |
| Body line of nested loops, outer pass a, inner pass b | adds (a − 1) × outer pass + (b − 1) × inner pass | agent: rule above, start times in Caveats | reference-recipe test, moved nested test |
| Line 6 of the reference recipe, counters (1, 5, 0), elapsed 0 | consumed 6 + 0 × 22 + 4 × 4 = 22 s, time left 48 s | agent: rule above, start times in Caveats | reference-recipe test |
| Line 5 of the reference recipe, counters (2, 2, 0), elapsed 0 | consumed 4 + 1 × 22 + 1 × 4 = 30 s, time left 40 s | agent: rule above, start times in Caveats | reference-recipe test |
| Loop end line, pass k of the loop it closes | adds (k − 1) × that loop's pass, plus the terms of the loops that enclose it | agent: rule above, start times in Caveats | loop-end tests |
| Line 7 of the reference recipe, counters (1, 5, 0), elapsed 0 | consumed 8 + 0 × 22 + 4 × 4 = 24 s, time left 46 s | agent: rule above, start times in Caveats | loop-end tests |
| Line 8 of the reference recipe, counters (3, 0, 0), elapsed 0 | consumed 24 + 2 × 22 = 68 s, time left 2 s | agent: rule above, start times in Caveats | loop-end tests |
| Loop start line | unchanged, already correct: the line's start time holds no pass of its own loop | agent: start times in Caveats | existing tests |
| Line in the second of two sequential loops | subtracts the second loop's single pass only, never the first loop's | agent: `RecipeSnapshot.EnclosingLoops` maps by depth | moved sequential-loop test |
| Counter 0 or negative on a loop that counts | adds nothing, same result as counter 1 | agent: rule above | counter-0 test |
| Line outside every loop | no loop term, unchanged | agent: no enclosing or closed loop | existing linear tests |
| `For` without a `task` column, PLC runs more passes | unchanged; the app plans one pass, consumed passes the total and the readout clamps at 00:00:00 | agent: `LoopParser` assumes 1 pass, documented in readme section 3.3 | not tested |

## Decisions

- Counter reading: the counter is the 1-based number of the current pass, 0 when the loop does not
  run. reason: the PLC contract in Caveats; the emulator and the readme field name agree.
  rejected: 0-based completed passes, the assumption of the completed plan
  `docs/plans/completed/20260520-execution-status-timing.md`, which was never validated on hardware.
- Fix site: the estimator only. reason: it is the only reader of the counters for timing.
  rejected: shifting the counters in the S7 codec or in `PlcExecutionInfo`, because the
  `FOR: c1 c2 c3` readout shows the raw PLC values and must keep doing so. rejected: adding the
  loop end line to the snapshot's enclosing-loop map, because `CoreLoopTests` and
  `CoreLoopEdgeCasesTests` pin that map.
- Documentation home: `docs/readme.md`, the field table in section 7.5 and the time-left rule in
  section 2.6.3, in Russian like the rest of that file. reason: no file in `docs/architecture/`
  describes execution timing, and section 7.5 is the PLC exchange contract that the PLC programmer
  reads. rejected: a new `docs/architecture/execution-timing.md`, because it would hold one fact
  that the readme already has a place for.
- The completed plan stays unchanged. reason: completed plans are history.

## Caveats

- The PLC writes 0 to `for_loop_count_N` while no loop of depth N runs, and the number of the
  current pass, 1 to the loop count, while it runs.
  probed: operator statement about the production PLC, 2026-11-25.
- The emulator writes the same values. `RunPlan.UnrollRange` sets the counter to 1 on the loop
  start line, to the pass number 1 to `Iterations` for the body and the loop end line, and back to
  0 after the loop. `SemiStep/SemiStep.Tools.PlcEmulator/README.md` states "The loop counts are
  1-based".
  probed: read of `RunPlan.UnrollRange`, 2026-11-25.
- ASSUMPTION: on the scan that executes a loop end line, the real PLC still reports the pass that
  just ended, as the emulator does; fallback: if it reports 0, the guard gives today's reading on
  that line and nothing else changes; checked by: the post-completion run on a real PLC.
- ASSUMPTION: the PLC does not revisit the For line on each pass; if it does, that scan reads high
  by (k-1) passes; checked by: the post-completion hardware run, watching the For line.
- `TimingCalculator.Calculate` records each line's start time as if every loop ran its first pass.
  The loop expansion is added after the loop end line's start time is recorded. The fix relies on
  this: the counter term adds the passes before the current one.
  probed: read of `TimingCalculator.Calculate`; the reference recipe gives start times
  0, 2, 2, 4, 4, 6, 8, 24, 68 s, single passes 4 s and 22 s, total 70 s, 2026-11-25.

## Constraints and non-goals

- `TimeLeftInStep` is out of scope. It does not read loop counters.
- The S7 decoding, `PlcExecutionInfo`, the snapshot's enclosing-loop map and the status-bar
  readout of the raw counters do not change.
- Bad input in scope: a counter of 0 or below on a loop that counts. A counter above the loop count
  is garbage from the PLC; `PassesBeforeCurrent` clamps it to the loop's iteration count, so the
  readout stays bounded and `TimeSpan * double` cannot throw `OverflowException` inside the
  execution-state subscription of `PlcMonitorViewModel`.
- Tests stay as simple as the existing ones in `ExecutionTimeEstimatorTests`: build the recipe in
  code, no CSV file in the test project.

## Gates

- None. The change has no measurement that needs an operator decision.

## Acceptance

The test runner prints a localized summary. "0 failed" means the summary's failed count is 0.

- `dotnet build SemiStep.slnx` → exit code 0, 0 warnings, 0 errors.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ExecutionTimeEstimatorTests"`
  → exit code 0, summary shows 0 failed and 20 tests in total. The run includes 48 s at line 6,
  counters (1, 5, 0); 40 s at line 5, counters (2, 2, 0); 46 s at line 7, counters (1, 5, 0); and
  2 s at line 8, counters (3, 0, 0); all at elapsed 0.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Timings"` → exit code 0,
  summary shows 0 failed.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → exit code 0,
  summary shows 0 failed.
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
- manual smoke, emulator with the MBE preset (Rider compound `Emulator and SemiStep MBE` or Zed
  task `Run emulator and SemiStep MBE`):
  1. Open `loops.csv` from the Behavior section in SemiStep, press Connect, wait for the sync to
     finish → "Recipe:" shows 00:01:10.
  2. Press Start in the emulator. While the emulator state reads `Running: line 3 of 9` and the
     SemiStep readout shows `FOR: 1 0 0` → "Recipe:" shows 00:01:06 to 00:01:08. The old reading
     showed 00:00:44 to 00:00:46.
  3. While the emulator reads `Running: line 6 of 9` with `FOR: 1 5 0` → "Recipe:" shows 00:00:46
     to 00:00:48. The old reading showed 00:00:20 to 00:00:22.
  4. During the whole run, "Recipe:" plus the wall time since Start stays within 5 s of 70 s. The
     emulator spends 100 ms on each of the 22 loop start and loop end frames, so the sum drifts up
     by up to 2.2 s by the end.
  5. At the end of line 9 → "Recipe:" reaches 00:00:00; when the emulator returns to `Idle`, it
     shows the total 00:01:10 again.
- not visible to headless tests: the real PLC values on hardware.

---

## Context

- `SemiStep/SemiStep.Core/Recipes/Analysis/ExecutionTimeEstimator.cs`: `TimeLeftInRecipe`, the only
  reader of `ForLoopCount1..3` for timing.
- `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs`: `EnclosingLoops` leaves a loop's own start
  and end lines out; `LoopByEnd` finds the loop a line closes. Read only.
- `SemiStep/SemiStep.Core/Recipes/Analysis/TimingCalculator.cs`: start times and single pass
  durations. Read only.
- `SemiStep/SemiStep.Tests/Core/Unit/Recipes/Analysis/ExecutionTimeEstimatorTests.cs`: 11 tests
  today. Three feed 0-based counters: the single-loop, nested-loop and sequential-loop cases. No
  other test feeds loop counters into the timing path; `PlcMonitorViewModelTimingTests` passes
  counters only on a line outside any loop.
- The test registry in `ExecutionTimeEstimatorTests` has its own immediate and long-lasting
  actions; build the reference layout from those.
- `docs/readme.md` is Russian. Keep it Russian.

## Tasks

### Task 1: Read the loop counter as the current pass on body lines

**Goal:** the recipe time left on a loop body line counts only the passes before the current one
**Entry:** `ExecutionTimeEstimator.ComputeLoopOffset`
**Likely touches:** `ExecutionTimeEstimator.cs`, `ExecutionTimeEstimatorTests.cs`

- [x] multiply each enclosing loop's single pass duration by the counter minus 1; keep the guard
      that skips a counter of 0 or less
- [x] move the single-loop, nested-loop and sequential-loop tests to 1-based counters; each asserts
      the absolute time left as well as the difference, and its name and comments describe the
      current pass, not completed passes
- [x] tests: the reference recipe totals 70 s, and gives 48 s at line 6 with counters (1, 5, 0)
      and 40 s at line 5 with counters (2, 2, 0), both at elapsed 0
- [x] tests: on a loop body line, counter 0 and counter 1 give the same time left
- [x] run the build, the full test suite and the format check - must pass before the next task

### Task 2: Count the closed loop's passes on its loop end line

**Goal:** on a loop end line, the loop that line closes counts like an enclosing loop
**Entry:** `ExecutionTimeEstimator.ComputeLoopOffset`
**Likely touches:** `ExecutionTimeEstimator.cs`, `ExecutionTimeEstimatorTests.cs`

- [x] add the term for the loop the current line closes, with the counter at that loop's depth and
      the same guard; the snapshot's enclosing-loop map stays unchanged
- [x] tests: the reference recipe gives 46 s at line 7 with counters (1, 5, 0) and 2 s at line 8
      with counters (3, 0, 0), both at elapsed 0
- [x] run the build, the full test suite and the format check - must pass before the next task

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`
- [x] manual smoke steps (skipped - operator smoke)

### Task 4: Update documentation

**Goal:** the readme states the counter convention and the time-left rule
**Entry:** `docs/readme.md`, sections 7.5 and 2.6.3

- [x] section 7.5: the three `for_loop_count_N` rows say 0 while no loop of that depth runs, and
      the number of the current pass, 1 to the loop count, while it runs
- [x] section 2.6.3: the "Время до конца рецепта" item says each loop that encloses the current
      line, or that the current line closes, adds its single pass duration times the counter
      minus 1
- [x] section 3.3 needs no change; reread it to confirm it does not contradict 7.5

## Post-Completion

- the operator's manual test: the Acceptance smoke steps on the emulator
- a run on the real PLC with a looped recipe confirms the readout matches the emulator, including
  the loop end lines
