# Incremental Refresh of the Recipe Grid Mutation Tail

## Overview

Building a recipe by successive step appends drives heavy GC and a large transient memory
climb (~50-100 MB per step observed in the runtime profiler, reclaimed after a delay or once
the next adds trigger a collection). This is not a leak: the memory is GC-reclaimable. The
cause is per-mutation allocation churn in `RecipeGridSurfaceBase.OnMutation`.

After every mutation the base runs a tail — `RefreshStepStartTimes()` and `RefreshLoopDepths()` —
that scans **all** `Items`. `RefreshStepStartTimes` allocates transient strings per row
(`time.TotalSeconds.ToString(...)` plus the intermediate strings inside
`TimeFormatHelper.FormatValue(...)`) **before** the guarded setter, so even unchanged rows pay
the full format cost and then discard the notification. Both surfaces (`CanonicalRecipeGridSurface`
and `TransposedRecipeGridSurface`) are permanently subscribed to `RecipeCoordinator.Mutated`, so
the tail runs twice per mutation. Appending step `M` re-formats `M` rows × 2 surfaces; building
`M` steps is `O(M^2)` transient strings.

The allocation churn lives entirely in `RefreshStepStartTimes` string formatting;
`RefreshLoopDepths` allocates nothing (a `Math.Min` and a guarded `int` setter). Fix: make **only
the start-time refresh** incremental. Start-time is forward-prefix-determined — a mutation at
index `k` cannot change the start-time of any step before `k` — so derive a `refreshFrom` index
from the `MutationSignal` and refresh start-times only from that index down. Append becomes `O(1)`
instead of `O(M)`, collapsing the `O(M^2)` build churn to `O(M)`. Loop-depth stays a full scan
(free, no allocation) because it is a matched-bracket property that a marker mutation can change
retroactively for earlier rows. The change lives entirely in the base class, so both surfaces
benefit from one edit.

## Context (from discovery)

- Files/components involved:
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs` — `OnMutation` (~132-195),
    `RefreshStepStartTimes` (~486-507), `RefreshLoopDepths` (~509-516), `Initialize`/`FullRebuild`
    (~127-130 / ~473-484).
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — `UpdateStepStartTime`/`StepStartTime`
    and `ForDepth` are already guarded with `RaiseAndSetIfChanged`; the waste is the string
    allocation upstream, not the notification.
  - `SemiStep/SemiStep.Core/Recipes/Analysis/LoopParser.cs`,
    `SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs`,
    `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`,
    `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs` — establish that imbalanced recipes commit
    as warnings (not failures) and still dispatch a mutation signal.
  - Tests under `SemiStep/SemiStep.Tests/UI/RecipeGrid/` — `RecipeGridSurfaceContractTests`
    seeds 4 steps, calls `Initialize()`, and runs each case against both surfaces.
- Related patterns found:
  - `RecipeGridSurfaceBase` is the single home for mutation logic shared by both orientations
    (PR #131 base-class extraction, `ab4b20f`). Shared optimizations belong here, not per surface.
  - The prior subscription/leak fix (`f9f939d`, later evolved into the `IDisposable` item +
    `CompositeDisposable` ownership model) governs object lifetime and event teardown. It is
    **orthogonal** to this change; the disposal plumbing is not touched.
- Dependencies identified:
  - `RecipeCoordinator.Snapshot.StepStartTimes` (dict keyed by absolute step index) and
    `.RowLoopDepths` (`int[]` indexed by absolute step index). Both are absolute-indexed, so a
    partial start-time sweep is a straight substitution of the loop start.
  - `MutationSignal` variants already carry the changed index/range.

## Development Approach

- **Testing approach**: Regular (code first, then tests) — matches the existing suite; the change
  is behavioral and verified through the contract test base.
- Complete the implementation task fully before verification.
- Small, focused change confined to the base class.
- **Every task includes new/updated tests.** Behavior is pinned through the existing
  `RecipeGridSurfaceContractTests` (each case runs against both surfaces) plus a per-append
  allocation-scaling guard.
- **All tests must pass before moving on.**
- Keep the change `dotnet format` clean (pre-commit hook enforces it).
- Maintain backward compatibility: the projected start-times and loop-depths must be identical to
  the full-scan behavior for every reachable mutation.

## Testing Strategy

- **Unit / contract tests**: extend `RecipeGridSurfaceContractTests` (runs x2 surfaces) to cover
  start-time and loop-depth correctness after append, insert, single remove, multi remove,
  action-change, and recipe-replace. For each mutating case, assert the rows **before** the
  mutation index are unchanged **and** the rows at/after it are correct — the before-index
  assertions catch both under-refresh and the `Initialize` baseline gap.
- **Contract-base row accessor**: the shared base holds only `IRecipeGridSurface Surface`, which
  exposes no per-row accessor, and the two surfaces reach rows differently
  (`CanonicalRecipeGridSurface.RecipeRows[i]` vs `TransposedRecipeGridSurface.StepColumns[i].Row`).
  Add a `protected abstract RecipeRowViewModel RowAt(int index)` to `RecipeGridSurfaceContractTests`,
  overridden per surface, so the x2-surface assertions can read start-time/`ForDepth` uniformly.
- **Distinct durations**: `SeedRecipe` appends bare `Wait` steps with no `step_duration`, so every
  seeded start-time is 0 and a stale middle row would read the same 0 as its neighbours. Give the
  non-loop mutation cases distinct per-step `step_duration` values (or assert each row against
  the formatted `Snapshot.StepStartTimes[i]`) so the before/after under-refresh direction is
  actually distinguishable.
- **Loop-depth regression (matched-bracket)**: remove an `EndForLoop` mid-recipe (`StepRemoved`)
  and assert the rows **above** it drop to the correct depth. This is the direction that an
  incremental depth refresh would have corrupted; it pins the decision to keep depth a full scan.
- **Action-change rebuild**: change a step's action mid-recipe and assert the rebuilt row's
  start-time and `ForDepth` are repopulated (not left `null`/`0`), since `RebuildItem` installs a
  fresh row.
- **Initialize baseline**: after the seed-then-`Initialize` setup, assert every row (including row
  0) has its start-time and loop-depth populated, before any mutation.
- **Allocation-scaling guard**: one test using `GC.GetAllocatedBytesForCurrentThread()` that
  appends a single step to a small recipe and to a much larger recipe, asserting the per-append
  allocation does not scale with recipe size. Warm up the JIT path first, and bracket the counter
  around only the single `AppendStep` call (exclude setup/assertion allocations between samples).
  The buggy-vs-fixed gap is ~100x, so a small multiplier threshold is safe and non-flaky. Both the
  test body and the synchronous mutation dispatch run on the same headless dispatcher thread, so
  the counter captures the tail.
- **No e2e**: this subsystem has no UI-based e2e suite; headless `[AvaloniaFact]` contract tests
  are the harness.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with ➕ prefix.
- Document blockers with ⚠️ prefix.
- Keep the plan in sync with actual work.

## Solution Overview

- Derive `refreshFrom` from the `MutationSignal` inside `OnMutation`, after the structural handler
  has applied the mutation, and pass it to `RefreshStepStartTimes(int fromIndex)` only.
- Parameterize `RefreshStepStartTimes(int fromIndex)` to loop `for (var i = fromIndex; i < Items.Count; i++)`.
  It reads the absolute-indexed `StepStartTimes` snapshot, so only the loop start changes.
- Leave `RefreshLoopDepths()` a full `0..Count` scan (unchanged). It allocates nothing, so a full
  scan costs nothing, and loop-depth is not incremental-safe (see rationale).
- `Initialize()` must run the tail from index 0 after `FullRebuild` so the incremental start-time
  refresh has a correct baseline (see the `Initialize` note below).
- Clamp `refreshFrom` to `>= 0`. An out-of-range `fromIndex` (e.g. after a stale-signal drop)
  yields an empty loop; the existing stale-signal early-returns in the structural handlers are
  unaffected because the tail still runs.
- `ReconcileSelectionWithItems()` stays unchanged (selection-sized, cheap, allocation-free on the
  common path).

### Per-signal `refreshFrom` (drives the start-time refresh only)

| Signal | refreshFrom | Rationale |
| --- | --- | --- |
| `PropertyUpdated(k)` | `k` | Step `k`'s own start-time is unchanged; a duration/iteration-count edit shifts the tail after `k`. Re-formatting `k` itself is a no-op guarded by `RaiseAndSetIfChanged`. |
| `StepAppended(k)` | `k` | The `O(1)` win — only the new last row is refreshed. |
| `StepsInserted(start, count)` | `start` | Inserted rows plus the shifted tail. |
| `StepRemoved(k)` | `k` | Shifted tail below the removal. |
| `StepsRemoved(indices)` | `min(indices)` | Shifted tail below the first removal. |
| `StepActionChanged(k)` | `k` | Critical: `RebuildItem` replaces `Items[k]` with a fresh row (`StepStartTime=null`); `k` must be refreshed to repopulate it, and it also covers any downstream duration change. |
| `RecipeReplaced` | `0` | Full refresh. |
| `StateRefreshed` | — | Already returns early before the tail. |

### Correctness rationale (document in code comment and preserve in tests)

- **Start-time is safe to refresh incrementally.** `start-time[i]` is computed forward from step 0
  and depends only on steps `0..i-1`, so a mutation at index `k >= i` cannot change it. This holds
  even for imbalanced or loop-affecting mutations, because timing is a forward walk and loop
  iteration counts do not change earlier rows' first-arrival times. Therefore refreshing
  start-times only from `refreshFrom` down is behavior-preserving.
- **Loop-depth is NOT safe to refresh incrementally, so it stays a full scan.** `loop-depth[i]` is
  a matched-bracket property: a marker mutation at `k` can retroactively change the depth of rows
  before `k` — for example deleting an `EndForLoop` below `k` (its opening `ForLoop` above `k` no
  longer nests), or appending an `EndForLoop` that closes a previously-unclosed `ForLoop` above it.
  Such mutations do reach the tail: `LoopParser` emits a **warning, not a failure**, on imbalance
  (`LoopParser.cs:43-48, 64-68`), `RecipeAnalyzer.Analyze` only fails on an iteration-count parse
  error or `maxDepth > 3` (`RecipeAnalyzer.cs:9-39`), and `RecipeSession.Apply` commits the
  warning-carrying snapshot and dispatches the signal (`RecipeSession.cs:68-81`; `IsValid` +
  the Save gate exist precisely because imbalanced recipes commit). A full `0..Count` depth scan
  costs nothing here (no string allocation), so the entire allocation win comes from the
  incremental start-time refresh alone.

### `Initialize` baseline

`Initialize()` calls `FullRebuild` directly and does **not** run the tail today; fresh rows carry
`StepStartTime=null` and `ForDepth=0`. Under the current full-scan tail, the first post-init
mutation backfills every row. Incremental start-time refresh removes that backfill: after
`Initialize` on a populated session, a `StepAppended(k)` with `refreshFrom=k` would leave rows
`0..k-1` with `null` start-times permanently. Fix: run `RefreshStepStartTimes(0)` and
`RefreshLoopDepths()` at the end of `Initialize()`, after `FullRebuild`, to establish the baseline.
This also closes a pre-existing latent display gap (a populated-session init would show blank
start-times until the first mutation). `RecipeReplaced` keeps refreshing through the `OnMutation`
tail (`refreshFrom = 0`), so there is no double refresh.

## Technical Details

- `OnMutation` gains a `refreshFrom` local computed from `signal` (a `switch` expression that
  reuses the index/range each case already carries), computed after the `try/catch` structural
  dispatch so the mutation is already applied. It is passed to `RefreshStepStartTimes(refreshFrom)`;
  `RefreshLoopDepths()` is still called with no argument.
- `RefreshStepStartTimes` signature changes from `()` to `(int fromIndex)`. `RefreshLoopDepths`
  is unchanged.
- `Initialize()` calls `RefreshStepStartTimes(0)` and `RefreshLoopDepths()` after `FullRebuild`.
- No change to `RecipeRowViewModel`, the coordinator, the snapshot, or Core analysis.

## What Goes Where

- **Implementation Steps** (`[ ]`): the base-class change and its tests, verification, docs.
- **Post-Completion** (no checkboxes): manual profiler re-check that the runtime memory climb is
  gone; this needs the running app and cannot be asserted in headless tests.

## Explicit Exclusions (YAGNI — decided during research and plan review)

- Do **not** make `RefreshLoopDepths` incremental. Loop-depth is a matched-bracket property that a
  committed marker mutation can change for rows before the mutation index; an incremental depth
  refresh would leave those rows stale. It allocates nothing, so a full scan is both correct and
  free.
- Do **not** make Core `RecipeSnapshot`/analysis incremental. Its `O(N)`/mutation cost is a small
  constant (one dict + one `int[]`), it is the correctness-critical path, and a full re-analyze is
  simple and safe. Revisit only if profiling at realistic recipe sizes shows Core allocations
  dominate — they will not for PLC-bounded recipes.
- Do **not** route the tail once through `ActiveRecipeGridSurface`. Each surface owns distinct row
  view models, so the refresh must write into both sets; it cannot be done once. The 2x is
  inherent to keeping both projections live for instant orientation flips, and `refreshFrom` makes
  it moot (append = 1 row × 2 surfaces).
- Do **not** flip `supportsRecycling: false` → `true` on the transposed cell templates. That is a
  different axis (scroll-time container recycling), and the text / read-only templates bake
  per-cell state into the control (per-cell `PropertyTimeEditingConverter`, `MaxLength`, closures);
  recycling them safely needs a separate rework.
- Do **not** add a per-row `TimeSpan` cache to skip formatting. `refreshFrom` simply does not visit
  the skipped rows, which subsumes it.

## Implementation Steps

### Task 1: Incremental start-time refresh in RecipeGridSurfaceBase

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeGridSurfaceContractTests.cs`

- [x] Add a private `RefreshStartIndexFor(MutationSignal signal)` returning the per-signal
      `refreshFrom` from the table above, clamped to `>= 0`; add a comment stating that it drives
      the start-time refresh only, with the correctness rationale (start-time is forward-prefix;
      loop-depth is a matched-bracket property and stays full-scan).
- [x] In `OnMutation`, compute `refreshFrom` after the structural dispatch and pass it to
      `RefreshStepStartTimes(refreshFrom)`; keep `RefreshLoopDepths()` a full scan.
- [x] Change `RefreshStepStartTimes()` to `RefreshStepStartTimes(int fromIndex)` looping from
      `fromIndex`. Leave `RefreshLoopDepths()` unchanged.
- [x] In `Initialize()`, call `RefreshStepStartTimes(0)` and `RefreshLoopDepths()` after
      `FullRebuild` to establish the baseline.
- [x] Add a `protected abstract RecipeRowViewModel RowAt(int index)` accessor to
      `RecipeGridSurfaceContractTests`, overridden by the Canonical (`RecipeRows[i]`) and Transposed
      (`StepColumns[i].Row`) fixtures.
- [x] Extend `RecipeGridSurfaceContractTests` (runs x2 surfaces): for append, insert, single
      remove, multi remove, action-change, and recipe-replace, assert rows **before** the mutation
      index are unchanged and rows at/after it match the expected snapshot values. Give the non-loop
      cases distinct per-step `step_duration` values so a stale middle row is distinguishable.
- [x] Add the `Initialize` baseline assertion: after seed + `Initialize`, every row (row 0
      included) has a populated start-time and loop-depth.
- [x] Add the action-change rebuild assertion: the rebuilt row's start-time and `ForDepth` are
      repopulated (not `null`/`0`).
- [x] Add the loop-depth regression: remove an `EndForLoop` mid-recipe and assert the rows above
      it show the corrected depth (pins the full-scan depth decision).
- [x] Add a per-append allocation-scaling guard using `GC.GetAllocatedBytesForCurrentThread()`
      (warm-up, bracket only the single `AppendStep`; assert per-append allocation does not scale
      with recipe size). Implemented as a two-surface delta: the extra allocation a second
      subscribed surface adds to one append isolates that surface's tail from the coordinator's
      one-per-mutation Core re-analysis (which is O(rows) and dilutes a plain small-vs-large append
      ratio to a fixed ~1.3x). Verified the guard fails on the pre-fix full-scan tail (~10x) and
      passes the incremental tail (well under the 4x threshold).
- [x] Run tests and `dotnet format` — must pass before Task 2.

### Task 2: Verify acceptance criteria
- [x] Verify the projected start-times and loop-depths are identical to the pre-change full-scan
      behavior for every mutation variant (covered by the contract tests on both surfaces).
- [x] Run the full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- [x] Confirm `dotnet format SemiStep/SemiStep.slnx` reports no changes.

### Task 3: [Final] Update documentation
- [x] Add a short note to `Docs/architecture/recipe-grid-surface.md` documenting that the
      `OnMutation` tail refreshes start-times incrementally from the mutation index
      (forward-prefix invariant) while loop-depth stays a full scan (matched-bracket property),
      and that `Initialize` seeds the baseline.
- [x] Move this plan to `Docs/plans/completed/`. (harness moves the plan after all phases finish)

## Post-Completion
*Informational only — no checkboxes.*

**Manual verification:**
- Re-run the app under the runtime memory profiler, build a large recipe by successive appends,
  and confirm the per-step memory climb and heavy GC are gone (the headless suite cannot observe
  the managed working-set behavior that surfaced the bug).
