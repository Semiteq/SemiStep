# Black-Box Performance Harness (driver + boundary signals + committed baselines)

## Overview
- Formalize the July perf work (PRs #131-#138) into a black-box performance harness that survives refactoring. Today's five probes encode hard-won lessons but half of them are white-box: they name `TransposedColumnCellsHost`, count host re-attaches, and pin implementation details that the already-planned follow-up (deleting the Host/pool indirection) will legitimately break.
- The design is two stable contracts at the edges, with everything in between free to change:
  - **Top: user actions.** `IRecipeGridDriver` — scroll, add/remove steps, select — implemented over the PUBLIC surface of the real views (ScrollViewer, view-model commands), the same entry points user input hits. No production interfaces added for testing (consumer-side interface, per project conventions); the only production concession is stable `x:Name` on anchor elements where missing.
  - **Bottom: framework-boundary signals.** `PerfSignals` — allocated bytes, fresh visual instances (reference-identity set-diff of `GetVisualDescendants()`), GC collection counts, retained floor after full GC. No SemiStep type names in any assertion.
- The headline gate: **a scrolled viewport creates 0 new visual instances after warmup**. Swap the panel implementation and the gate stays green; reintroduce subtree rebuild and it goes red.
- Gate hierarchy (reliability order):
  1. **Invariants** (fresh visuals == 0) — exact, cross-machine, never re-baselined.
  2. **Ratios/scaling** (per-add bytes at N vs 2N flat; transposed vs canonical parity; selection wall-clock flat vs N at fixed S, same-process ratio) — cross-machine, tolerance-gated. Same-process wall-clock RATIOS belong to this tier: dividing two timings from one process cancels machine speed, so they discriminate CPU-bound regressions (like the O(S·N) selection scan) that allocation signals cannot see.
  3. **Absolute bytes** — telemetry against a committed baseline with soft tolerance (20%) plus a hand-set hard budget cap per metric. Re-baselining within budget is routine (runtime/Avalonia bumps); the budget stops compounding re-baseline drift and changes only by deliberate hand edit in a PR.
  - ABSOLUTE wall-clock milliseconds are never asserted (machine-dependent, flaky by construction); same-process wall-clock ratios are permitted in the ratio tier.
- Gating: measurement facts are xunit v3 **explicit tests** (`Explicit = true`). Default `dotnet test`/CI does not run them; probes run via `SemiStep.Tests.exe -explicit only`. Probes are single-mode (measure → report to the actuals fixture → assert vs committed baselines) and read no environment variables. Re-baselining is a file copy: the actuals artifact is the proposed next `baselines.json` (see Technical Details). Production code is untouched.

## Context (from discovery)
- Probes on master: `SemiStep/SemiStep.Tests/Performance/` — `TransposedViewAllocationProbe` (per-add sweep, viewport-jump, host-reattach counter), `TransposedSelectionCostProbe` (O(S·N) discrimination via same-process time ratio), `GridRetentionProbe`, `CoreAllocationProbe`, `TransposedScrollTraceScenario` (dotnet-trace diagnostic scenario). All five env-gated (`SEMISTEP_PROBE` / `SEMISTEP_TRACE_SCENARIO`), skipped by default, write reports to `%TEMP%`; no committed baselines, no gate-vs-baseline comparison. Env handling is test-project-only (zero SEMISTEP_ references in SemiStep.UI/Core). Separately, `SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedViewportJumpTests.cs` is an ALWAYS-ON Integration test (no env gate) pinning the realized-container-count-stays-viewport-bound invariant in CI — it is NOT part of this migration and stays always-on.
- xunit v3 explicit-test support verified on the built runner: `SemiStep.Tests.exe -?` lists `-explicit on|off|only` with `off` (skip explicit) as default. Remaining to confirm in Task 1: `[AvaloniaFact(Explicit = true)]` compiles (property inherited from the v3 `FactAttribute`) and plain `dotnet test` reports explicit tests as not-run. Repo is on xunit.v3 3.2.2.
- Existing fixtures/harness: probes build the REAL headless views via `SemiStep.Tests.UI.Helpers` (window + config scenarios `WithGroups`/`WideParams`); the drivers adapt these helpers, they do not duplicate them.
- CI (`.github/workflows/ci.yml`): full `SemiStep.Tests` on windows-latest, Release. No CI change in this plan (Post-Completion option).
- `scripts/perf/speedscope-shares.py` exists (trace analyzer); `Docs/perf/` does not exist yet.
- Planned follow-up that motivates the migration: delete `TransposedColumnCellsHost` + pool (recorded in `Docs/plans/completed/20260716-transposed-child-recycle-fix.md` Post-Completion) — white-box host probes would break; black-box gates must not.

## Development Approach
- **testing approach**: Regular (code first, then tests).
- The harness core (runner, signals, baseline comparer, fixture, drivers) gets ALWAYS-ON unit tests — fast, deterministic, part of the normal suite; only the measurement scenarios themselves are explicit-gated.
- Migration replaces white-box duplicates without weakening coverage: every lesson currently pinned by a probe must remain pinned by an equivalent-or-stronger black-box gate before the white-box assert is deleted. The old→new map in Technical Details is the contract.
- Every task fully green (`dotnet test`, full suite) before the next.
- Gates assert mechanisms; the felt smoothness on real hardware stays a manual oracle (headless sees no Skia/composition) and the README says so.

## Testing Strategy
- **unit tests (always-on)**: runner detects a synthetically created control (`FreshVisualInstances > 0`) and reports 0 for a no-op workload; bytes measurement sane; baseline comparer pass/fail/tolerance/budget paths; fixture merge; driver smoke tests for BOTH driver implementations.
- **explicit scenarios (`Explicit = true`, `Category=Performance` trait for grouping)**: the migrated gates, each asserting an exact invariant or comparing against `Docs/perf/baselines.json`. Run: `SemiStep.Tests.exe -explicit only` (all) or `-explicit only -method "*Name*"` (selective).
- **diagnostic layer**: `TransposedScrollTraceScenario` + `speedscope-shares.py` are NOT gates — they are the diagnosis kit used when a black-box gate trips. Explicit-gated like the rest; launch: `dotnet-trace collect ... -- SemiStep.Tests.exe -explicit only -method "*TransposedScrollTraceScenario*"`.

## Progress Tracking
- Mark `[x]` immediately. `➕` new tasks, `⚠️` blockers.

## Solution Overview
- **Chosen**: consumer-side driver interface + scenario runner + boundary signals inside `SemiStep.Tests` (new `Performance/Harness/` folder); committed baselines with runtime/testbed context; re-baseline via copy-promotion of the fixture-merged proposed-baselines artifact. No new project: fixtures are the dominant reuse, and allocation-bytes APIs are GC-mode-insensitive, so nothing forces a runtimeconfig split.
- **Rejected**:
  - Separate `SemiStep.PerfTests` project — no forcing function; revisit when BenchmarkDotNet or a pinned-GC runtimeconfig is actually needed.
  - BenchmarkDotNet for these scenarios — wrong tool for windowed headless dispatcher scenarios (off-label per its maintainers); deferred as its own console project for Core micro-benchmarks (Post-Completion).
  - JetBrains dotMemory Unit — deprecated 2026-05 with no replacement and no modern-.NET support; hand-rolled `GC.*` signals are the surviving mainstream option (precedented in `dotnet/runtime` no-alloc tests).
  - Wrapper/runner script — build+run are two documented commands, comparer failure messages carry metric/baseline/actual, and promotion is a plain file copy; `scripts/perf/` keeps only the diagnostic `speedscope-shares.py`.
  - Asserting absolute wall-clock — flaky by construction (same-process time ratios are fine, see gate hierarchy).
- Baselines live in `Docs/perf/baselines.json` (versioned, reviewed in PRs). A re-baseline commit MUST state the reason (runtime/Avalonia bump, deliberate behavior change).

## Prior art & standard-practice alignment
- **Two-harness split is vendor-standard.** `dotnet/performance` runs BenchmarkDotNet for microbenchmarks and a separate purpose-built scenario harness for app-level scenarios. Our scenario runner + committed baselines, distinct from BDN, is the same shape.
- **Real-headless-control allocation measurement mirrors Avalonia's own** `tests/Avalonia.Benchmarks` (real controls in a `TestRoot` tree, layout pass, `[MemoryDiagnoser]`); the baseline/gate layer is what we add on top.
- **The `FreshVisualInstances == 0` reference-identity invariant is bespoke deliberately** — the question "what is a recycled column" is domain-specific; no off-the-shelf tool answers it, and it is the only signal immune to bytes, timing, GC nondeterminism, and SDK bumps.
- **`Explicit=true` gating is idiomatic** (official xunit v3 API, xunit#2518), with a known caveat: some runners mis-honor `Explicit` (microsoft/vscode-dotnettools#2261) — hence the Task 1 spike verifies actual skip behavior per runner.
- **Baseline-vs-budget split and testbed labels follow continuous-benchmarking practice**: Bencher models the measurement environment as a named `testbed` role; Lighthouse CI separates drifting goals from hard budgets. Full result-history tracking a la Bencher/github-action-benchmark is a deferred upgrade; until then the git history of `baselines.json` is the measurement history.

## Technical Details
`Performance/Harness/PerfSignals.cs`:
- `sealed record PerfSignals(long AllocatedBytes, int FreshVisualInstances, int Gen0, int Gen1, int Gen2, long RetainedBytesAfterFullGc)`.

`Performance/Harness/PerfScenarioRunner.cs`:
- `MeasureAsync(Visual snapshotScope, Func<Task> warmup, Func<Task> workload)` → `PerfSignals`.
- **`snapshotScope` is the items-panel subtree (the `ItemsPresenter` / items panel), not the whole `TopLevel`.** Walking the full root pulls in ScrollBar chrome and focus adorners, which would make the `== 0` invariant flaky on a stray +1/+2 of framework chrome; scoping to the realized-container subtree keeps `== 0` exact while still catching subtree rebuild (hundreds of instances). The driver exposes this scope.
- Sequence: run warmup → settle (`Dispatcher.UIThread.RunJobs`, `GC.Collect` x2 + `WaitForPendingFinalizers`) → snapshot visuals (`HashSet<Visual>` with reference-equality comparer over `snapshotScope.GetVisualDescendants()`) → read `GC.GetAllocatedBytesForCurrentThread()` + collection counts → run workload → settle jobs → read deltas → fresh visuals = descendants not in snapshot.
- Warmup MUST reach steady-state peak realization (scroll the full measured range once) so the recycle pool is pre-filled; otherwise the first workload pass legitimately creates containers and `== 0` is unachievable.
- The snapshot pins visuals for the measurement window by design; `RetainedBytesAfterFullGc` is measured in a separate pass after the snapshot is dropped, so pinning cannot skew the floor.
- Two-point retention support: the runner also exposes `SampleRetainedFloorAsync()` (settle jobs → full blocking GC → `GC.GetTotalMemory(true)`) as a standalone call — the retention gate is a DELTA (floor sampled before and after N workload cycles); a single `MeasureAsync` window cannot produce it.
- Fixed workload only (iteration counts as parameters) — never fixed duration.

`Performance/Harness/IRecipeGridDriver.cs` + `TransposedGridDriver.cs` + `CanonicalGridDriver.cs`:
- `ScrollToColumnAsync(int index)` (canonical: row), `AddStepsAsync(int count)`, `RemoveStepsAsync(int count)`, `SelectRangeAsync(int from, int to)`, `WaitForIdleAsync()`; a `TopLevel Root` property and the snapshot-scope accessor for the runner.
- Implemented over the existing test view builders (`SemiStep.Tests.UI.Helpers`) + public view surface: `ScrollIntoView`/ScrollViewer offset, surface mutation commands, selection model. Resolve controls by stable `x:Name` (add names in axaml only where missing).
- Parity: the same scenario body runs against both drivers; transposed-vs-canonical ratios come from one code path.

`Performance/Harness/PerfBaselines.cs` + `Performance/Harness/PerfActualsFixture.cs` + `Docs/perf/baselines.json`:
- Schema: `{ "context": { "runtime", "avalonia", "os", "testbed", "capturedUtc" }, "metrics": { "<name>": { "value", "tolerancePct", "budget" } } }`. Invariant gates (== 0) are asserted in code, not in the file.
- Context fields: `os` is family+arch (`win-x64`); `testbed` is a role label (`dev-primary`, later `ci-hosted`) identifying the measurement environment. No hostnames or usernames — the repo is public, and hardware identity is irrelevant to these metrics (allocated bytes depend on runtime+code, not CPU).
- Anti-drift, two levels: `value` is the measured baseline — moves on re-baseline, catches step regressions via `tolerancePct`; `budget` is a hand-set absolute cap — the gate `actual <= budget` always applies and promotion never writes it (the fixture merge carries it through verbatim). A missing/null budget fails the probe with "set the budget by hand" guidance; `budget < value` is rejected at load as a config error. Raising a budget is a deliberate PR edit with its own justification.
- Compare: fail if `actual > value * (1 + tolerancePct/100)` OR `actual > budget`; improvement beyond tolerance prints a "baseline is stale, consider re-baselining down" advisory (does not fail).
- Baseline-file location at runtime: the test exe runs from `SemiStep/Artifacts/bin/.../release/`, so `PerfBaselines` walks up from the assembly directory to the repo root (marker: `.git` dir or repo-root `global.json`; the `.slnx` lives one level down in `SemiStep/` and is NOT the root marker) and resolves `Docs/perf/baselines.json`. If not found: fail with paths searched + the exact capture-and-copy commands.
- Actuals artifact: probes report each measured metric to `PerfActualsFixture` (xunit v3 assembly fixture) via a thread-safe in-memory collector; the fixture writes `%TEMP%/semistep-perf-actuals-<pid>.json` once, on assembly disposal (single writer — safe under xunit test-class parallelization, fresh per process; the PID suffix prevents two concurrent test processes from clobbering each other, and failing probes print the exact path).
- The written artifact is the PROPOSED NEXT `baselines.json`: the fixture loads current baselines and overlays only the measured metrics (values + refreshed context); budgets, tolerances, and unmeasured metrics carry through untouched (a metric absent from baselines gets `"budget": null` — the field is always present, schema identical). `Copy-Item` promotion is therefore always safe, including after a `-method`-filtered run.
- Re-baseline flow: explicit run (failures expected when numbers drifted — that is the point: see what changed before accepting) → `Copy-Item` actuals over `Docs/perf/baselines.json` (failing probes print this exact command) → review git diff → commit with the reason → re-run to confirm green.

Canonical commands (documented in `Docs/perf/README.md`):
- Run gates: `dotnet build SemiStep/SemiStep.slnx -c Release` then `SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe -explicit only` (`-method "*Name*"` selective).
- Capture runs (optional, steadies the soft byte telemetry; hard gates are tiering-immune): prefix with `$env:DOTNET_TieredCompilation='0'; $env:DOTNET_TieredPGO='0'` (.NET host knobs, scoped to that shell).

Measurement determinism (absolute-byte baselines are only sound with these):
- SDK pinned via the repo-root `global.json` (10.0.100). Its `rollForward: latestFeature` can shift the runtime within the feature band and thus allocation shapes — acceptable because absolute bytes are soft-tolerance telemetry, and a re-baseline on an SDK bump is expected and documented.
- Tiered compilation / dynamic PGO disabled for capture runs via the documented prefix (also in the dotnet-trace launch): background rejit and tier transitions perturb allocation counts. Only the soft byte telemetry depends on this; invariant and ratio gates are tiering-immune by construction.
- Workstation GC x64 (the app default); do not measure under server GC.
- Warmup + forced blocking `GC.Collect` around the measurement window (in the runner) tames JIT-warmup allocations and GC nondeterminism.

Gating:
- `Explicit = true` on every measurement fact; `Category=Performance` trait kept for grouping/reporting. All `SEMISTEP_PROBE`/`SEMISTEP_TRACE_SCENARIO` reads are deleted during migration — five files carry them today (the five probes). `TransposedViewportJumpTests` reads no env var and stays always-on, untouched.

Old→new gate map (Task 4-5 contract; nothing deleted before its replacement is green):
- host re-attach counter (white-box) → `FreshVisualInstances == 0` on scroll round-trips (catches ANY newly-created control). CAVEAT: fresh-visuals==0 catches subtree REBUILD (new instances), not a re-attach of an EXISTING instance without rebuild. That second lesson stays pinned by the `DetachedFromVisualTree`-assertion contract tests (`TransposedChildRecycleTests`, `TransposedColumnsPanelContractTests`, `TransposedColumnsPanelItemsChangedTests`) — which live in `UI/`, not `Performance/`, and are not migrated or deleted by this plan.
- viewport-jump bytes/column (absolute) → same metric via runner, baseline-gated at 20% + transposed/canonical parity ratio ≤ 2.0x (cross-machine).
- per-add sweep across seeds → scaling gate: per-add bytes at N=120 vs N=20 ratio ≤ 1.5 (flat-growth invariant), absolute values telemetry.
- selection discrimination → unchanged mechanism: same-process `Stopwatch` wall-clock RATIO at N=4800 vs N=300 (fix ~flat, restored regression ~an order of magnitude over the 3x limit — the probe already records these). The timing stays local to the probe, OUTSIDE `PerfSignals` (the runner measures allocation/visual signals only); the driver supplies the actions (`SelectRangeAsync`). This is the one CPU-bound, allocation-neutral gate, and the time-ratio is its only viable signal.
- retention floor → PRIMARY gate is the flat-delta invariant (retained floor after N cycles − floor at start ≈ 0, machine-independent); the absolute floor is baseline-gated telemetry only (a 20% tolerance on ~100 MB would hide a slow leak, so it cannot be the primary gate). The probe also carries a weak-reference bounded-container-survivor check and the transposed-vs-canonical layer isolation — BOTH are preserved in the migrated probe (the survivor check as-is, layer isolation via running the same scenario on both drivers).
- `CoreAllocationProbe` → no driver (Core API is already black-box); adopts the baseline mechanism only.

## What Goes Where
- **Implementation Steps** (checkboxes): harness core, drivers, baseline store, probe migration, initial baseline capture, docs.
- **Post-Completion** (no checkboxes): CI guard and optional nightly CI job; BenchmarkDotNet tier for Core micro-benchmarks if ever wanted; the manual on-hardware smoothness oracle.

## Implementation Steps

### Task 1: PerfSignals + PerfScenarioRunner (measurement core)

**Files:**
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfSignals.cs`
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfScenarioRunner.cs`
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfScenarioRunnerTests.cs`

- [x] SPIKE (first, cheap, de-risks the gating design): confirm `[AvaloniaFact(Explicit = true)]` compiles and behaves — a scratch explicit test is NOT run by plain `dotnet test` (reported not-run/skipped, suite green) and IS run by `SemiStep.Tests.exe -explicit only`. Verify the skip against the actual CI invocation (`dotnet test`), not just the exe (runner-specific bugs exist where `Explicit` is not honored — microsoft/vscode-dotnettools#2261), and check once that Rider run-all does not execute explicit tests (probes take tens of seconds each). If `AvaloniaFact` does not surface the v3 `Explicit` property, fall back to a thin `PerfFactAttribute : AvaloniaFactAttribute` setting it. Record the outcome here. **OUTCOME:** `[AvaloniaFact(Explicit = true)]` compiles as-is on xunit.v3 3.2.2 — `AvaloniaFactAttribute` inherits the v3 `Explicit` property; NO `PerfFactAttribute` fallback needed. Verified empirically on the built exe (Debug): plain `dotnet test --filter` reports the explicit test `Пропущен`/skipped with the suite green (0 counted); `SemiStep.Tests.exe -explicit only -method "*Scratch*"` runs it (Total: 1, Failed: 0, Skipped: 0); the exe default (`-explicit off`) reports it `Not Run: 1`. So `dotnet test` (the CI invocation) honors the skip. Rider run-all behavior is not automatable from this environment — manual check pending, but the underlying xunit-v3 discovery honors `Explicit` for `dotnet test`, and Rider uses the same VSTest/xunit path.
- [x] Implement `PerfSignals` record and `PerfScenarioRunner` (`MeasureAsync` sequence per Technical Details; `SampleRetainedFloorAsync` standalone; fixed workload only).
- [x] Write ALWAYS-ON unit tests (`[AvaloniaFact]`): no-op workload → `FreshVisualInstances == 0` and near-zero bytes; workload adding a `TextBlock` INSIDE the snapshotScope subtree → `FreshVisualInstances == 1`, and one added OUTSIDE the scope → 0 (pins the scoping); workload allocating a known array → `AllocatedBytes` at least that size.
- [x] Write error-path test: workload throwing propagates and does not corrupt subsequent measurement on the same runner.
- [x] Run tests — must pass before Task 2.

### Task 2: IRecipeGridDriver + both drivers

**Files:**
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/IRecipeGridDriver.cs`
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/TransposedGridDriver.cs`
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/CanonicalGridDriver.cs`
- Modify: view axaml ONLY if an anchor element lacks a stable `x:Name` (record which, if any) — NONE added: both anchors already had stable names (`x:Name="StepListBox"` on the transposed view, `x:Name="RecipeGrid"` on the canonical view). No production axaml touched; the snapshot scope resolves from a realized container's visual parent, needing no extra anchor.

- [x] Implement the interface and both drivers over the existing `SemiStep.Tests.UI.Helpers` view builders and public view surface (no production interfaces; resolve by `x:Name`; expose the runner's snapshot scope).
- [x] Write ALWAYS-ON smoke tests for BOTH drivers: `ScrollToColumnAsync` changes the realized range/viewport offset; `AddStepsAsync`/`RemoveStepsAsync` changes column (row) count; `SelectRangeAsync` is reflected in the selection model; `WaitForIdleAsync` drains dispatcher jobs.
- [x] Run tests — must pass before Task 3.

### Task 3: Baseline store + actuals fixture

**Files:**
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfBaselines.cs`
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfActualsFixture.cs`
- Create: `Docs/perf/baselines.json` (placeholder with context schema, empty metrics)
- Create: `SemiStep/SemiStep.Tests/Performance/Harness/PerfBaselinesTests.cs`

- [x] Implement `PerfBaselines` load/compare (tolerance gate, budget gate, budget>=value load validation, stale-baseline advisory, repo-root file resolution) and `PerfActualsFixture` (thread-safe in-memory collector; writes the merged proposed-baselines artifact once on assembly disposal; no promotion code — promotion is a documented file copy).
- [x] Write ALWAYS-ON unit tests: within-tolerance pass; over-tolerance fail naming metric/baseline/actual; improvement prints advisory but passes; over-budget fails even within baseline tolerance; `budget < value` rejected at load; missing metric fails with capture-and-copy guidance naming the exact commands; missing/null budget fails with "set the budget by hand" guidance; fixture merge — measured metrics overlaid, unmeasured metrics and all budgets carried through untouched, absent budget written as explicit null; concurrent metric reports from parallel test classes all land in the artifact.
- [x] Run tests — must pass before Task 4.

### Task 4: Migrate scroll/recycle gates to black-box scenarios

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Performance/TransposedViewAllocationProbe.cs` (rewrite on the harness; delete the white-box host re-attach counter)
- Modify: `SemiStep/SemiStep.Tests/Performance/TransposedScrollTraceScenario.cs` (only if reusing the driver is trivial; otherwise leave for Task 5's gating swap — it is diagnostics, not a gate)

- [x] Rewrite the scroll gate black-box: warmup round-trips, then fixed round-trips via `ScrollToColumnAsync`; assert `FreshVisualInstances == 0`. **PASSES** (`ScrollRoundTrips_CreateZeroFreshVisuals`, WideParams, 420 cols, 20↔220, 20 round-trips → freshVisuals=0). The headline invariant is proven falsifiable-ready and green.
- [x] Rewrite viewport-jump: bytes/realized-column via the runner against baseline (20% tolerance) + transposed/canonical parity ratio (honest hard cap, see ✅ below) using the same scenario body on both drivers. Byte-baseline compare wired as record-only (empty baselines); parity ratio is a HARD assert. **PASSES.**
- [x] Rewrite per-add sweep as the scaling gate (N=120 vs N=20 per-add ratio ≤ 1.5), absolute values recorded as telemetry. **PASSES** (`PerAdd_ScalesFlat_WithColumnCount`, ratio well under 1.5).
- [x] Delete the replaced white-box asserts ONLY after the new gates pass; keep report output (`%TEMP%` + console). Convert the file's facts to `Explicit = true` and delete its `SEMISTEP_PROBE` reads. White-box host-reattach counter deleted; three facts now `[AvaloniaFact(Explicit = true)]`; no `SEMISTEP_PROBE`/`SkipUnless` remain in the file.
- [x] Run the migrated probes (`SemiStep.Tests.exe -explicit only -method "*AllocationProbe*"`) AND the full normal suite — must pass before Task 5. Full normal suite green (1437 passed, 0 failed, explicit probes not run). Migrated probes: all three facts PASS.

➕ Registered `[assembly: AssemblyFixture(typeof(PerfActualsFixture))]` in `SemiStep.Tests/AssemblyAttributes.cs`; probe class constructor-injects `PerfActualsFixture` and calls `Report(...)` for every measured byte metric. Confirmed the empty-guard in `Dispose` keeps a plain `dotnet test` clean (probes skipped → nothing reported → no artifact written). Added `PerfBaselines.Contains(metric)` to drive the assert-or-record helper (records-only when the metric is absent, hard-asserts once Task 6 captures it).

✅ **Parity gate resolved — honest cap now, tighten later (user decision).** The transposed column rebind measures deterministically ~2.95–3.01x a canonical row on current master (both jumps recycle cleanly: `FreshVisualInstances == 0` on both; ~3x holds per-cell too, 7096 vs 2406 bytes/cell, so it is not a window/denominator artifact). Root cause is the `TransposedColumnCellsHost` + `TransposedColumnCellsPool` indirection, still present in production (Overview §Context line 22 schedules its deletion as the motivating follow-up); the 2.0x target is achievable only after that deletion. Per the decision, the parity assert is a **HARD cap set to ≤ 3.3x** (~10% headroom over the 3.01x worst-case) so it catches a regression above ~3.3x today, with an inline comment (`ParityRatioTargetAfterHostPoolDeletion = 2.0`) recording that the cap tightens to 2.0x once the Host + pool are deleted. Scope unchanged: Host/pool NOT deleted here, that stays the scheduled follow-up.

### Task 5: Migrate selection, retention, and Core probes

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Performance/TransposedSelectionCostProbe.cs`
- Modify: `SemiStep/SemiStep.Tests/Performance/GridRetentionProbe.cs`
- Modify: `SemiStep/SemiStep.Tests/Performance/CoreAllocationProbe.cs`
- Modify: `SemiStep/SemiStep.Tests/Performance/TransposedScrollTraceScenario.cs` (gating swap only)

- [x] Selection probe: drive the actions via `SelectRangeAsync`; KEEP its own `Stopwatch` same-process time-ratio as the gate (per the old→new map — this metric is CPU-bound and allocation-neutral, `PerfSignals` cannot express it; do not route the measurement through the runner). **PASSES.** View built via `TransposedGridDriver.CreateAsync`; the fixed 200-column tail range is selected via `driver.SelectRangeAsync` (setup, outside the timed window); the timed single-index toggle uses the driver's exposed index-based `Selection` (Deselect/Select), which keeps the O(N) item→index lookup and the dispatcher pump OUT of the stopwatch. Measured ratio N=4800/N=300 = **0.57x** (flat/decreasing), hard-asserted ≤ 3.0x.
- [x] Retention probe: two-point flat-delta via `SampleRetainedFloorAsync()` before/after N cycles — the PRIMARY gate is delta ≈ 0; the absolute floor is baseline-gated telemetry only. Preserve the existing weak-reference bounded-container-survivor check and the transposed-vs-canonical layer isolation (both drivers). **PASSES.** Floor sampled before/after 150 round-trips on BOTH drivers; measured per-round-trip delta transposed = **-551 bytes**, canonical = **3,121 bytes** (both flat, guard 100,000). Absolute floor reported as record-only telemetry (`{driver}.retention.floorBytes`). Survivor check preserved and now hard-asserted (viewport-bound realization: transposed 16, canonical 32 realized/survivors, both ≪ 300 steps).
- [x] Core probe: no driver; adopt baseline compare only. **PASSES.** Per-append bytes at N=10/100/500 reported to the actuals fixture (record-only until Task 6); `bytes > 0` kept as the hard sanity assert. Converted to `[Fact(Explicit = true)]`.
- [x] Gating sweep: convert all remaining measurement facts to `Explicit = true` and delete every `SEMISTEP_PROBE`/`SEMISTEP_TRACE_SCENARIO` read (`grep -rn "GetEnvironmentVariable(\"SEMISTEP"` must return zero hits in the repo — the plain SEMISTEP_ string legitimately survives in comments). Update the trace scenario's header comments (its documented launch command changes). **DONE.** All five probes' facts are `Explicit = true`; `GetEnvironmentVariable("SEMISTEP` returns ZERO hits repo-wide. Trace scenario launch comment updated to `dotnet-trace collect ... -- SemiStep.Tests.exe -explicit only -method "*TransposedScrollTraceScenario*"` (env-var form deleted).
- [x] Run all migrated probes (`-explicit only`) + full suite — must pass before Task 6. **DONE.** Explicit probes all green (selection, retention x2, core, allocation x3, trace); full `dotnet test` = 1437 passed, 0 failed, explicit probes not-run; `dotnet format --verify-no-changes` clean.

### Task 6: Initial baseline capture

**Files:**
- Modify: `Docs/perf/baselines.json` (first real capture)

- [x] Capture initial baselines by walking the documented flow exactly as a user would: Release build → full explicit run with the capture prefix (`$env:DOTNET_TieredCompilation='0'; $env:DOTNET_TieredPGO='0'`) → probes fail on the empty baselines and print the capture-and-copy command → run it → hand-set each metric budget (README guidance: round up generously from the initial value, ~1.5-2x, or derive from an acceptance criterion such as the 2x-canonical parity) → review the diff → commit `Docs/perf/baselines.json` (context: runtime, Avalonia 12.0.5, win-x64, testbed `dev-primary`, date) with the reason "initial capture" in the message. **DONE.** Captured **9 metrics** (canonical/transposed viewportJump.bytesPerColumn, transposed perAdd.bytes.n20/n120, canonical/transposed retention.floorBytes, core.perAppend.bytes.n10/n100/n500). Budgets hand-set with ~1.5-2x headroom over each measured value; the **transposed.viewportJump.bytesPerColumn budget = 290,000 (~3.3x the 86,670 canonical baseline)** — the honest parity cap from Task 4, not the deferred 2.0x target. Context: runtime `10.0.9`, Avalonia `12.0.5`, `win-x64`, testbed `dev-primary`, capturedUtc `2026-07-17`. No budget null, no budget < value.
- [x] Verify the gate loop end-to-end: re-run the full explicit suite → all green against the just-committed baselines; temporarily inflate one metric tolerance-breakingly in a scratch copy to confirm the failure message names metric/baseline/actual and the copy command (do not commit the scratch). **DONE.** Re-run: all explicit probes green (0 failed) against the populated baselines. Falsifiability: deflated `transposed.viewportJump.bytesPerColumn` value 255439→1000 in a swap-then-restore copy; the failure printed `Metric 'transposed.viewportJump.bytesPerColumn' regressed: actual=255445.5 exceeds baseline 1000 +20% (limit 1200)` plus the full capture-and-copy command. Real baselines.json restored (`git status` clean of the scratch edit).
- [x] Verify merge-promotion safety on a filtered run: run ONE probe via `-method`, confirm the actuals artifact still contains ALL baseline metrics (unmeasured ones carried through) with only the measured one updated. **DONE.** Filtered `*PerAdd_ScalesFlat*` run: the actuals artifact carried ALL 9 metrics; only the two perAdd values refreshed (275290→275806, 271918→271592), the other 7 metrics + every budget carried through verbatim.
- [x] Run full suite — must pass before Task 7. **DONE.** `dotnet test` = 1437 passed, 0 failed, 8 explicit probes skipped.

### Task 7: Docs/perf/README.md + architecture pointer

**Files:**
- Create: `Docs/perf/README.md`
- Modify: `Docs/architecture/recipe-grid-surface.md` (short pointer from the perf-lessons section to Docs/perf)

- [x] Write README: the canonical commands (Release build + `SemiStep.Tests.exe -explicit only`; optional capture prefix for byte telemetry), gate hierarchy (invariants → ratios → soft byte baselines; absolute wall-clock never asserted, same-process time ratios permitted), re-baseline procedure (explicit run → copy actuals over baselines (command printed by failing probes) → review diff → commit with reason → re-run green; legit reasons: runtime/Avalonia bump, deliberate behavior change; the artifact is the full proposed baselines, so the copy is safe even after filtered runs), the baseline-vs-budget split (tolerance catches steps, budget stops compounding drift; budgets change only by hand edit with justification), the context policy (testbed role labels, no machine identity), headless blind spots (no Skia/composition — `CreateSKFont`/`CreateCompositionVisual` only visible in live traces; on-hardware smoothness stays the manual oracle), and the diagnostic layer (trace scenario + speedscope-shares.py — what to reach for when a gate trips). **DONE.** `Docs/perf/README.md` written; commands/method-names verified against the actual probe files; parity documented as the honest 3.3x hard cap today → 2.0x aspiration.
- [x] Update the architecture doc pointer; retire any statements this plan obsoletes (e.g. "allocation gate pending live-app measurement" where the black-box gate now stands). **DONE.** Three obsolete statements retired in `recipe-grid-surface.md`: the "manual step ... stays with the user" allocation gate now cites the automated black-box gate (only composition/Skia share stays manual); "byte/gen0 allocation gate is still pending live-app measurement" now points to the standing harness; and `TransposedSelectionCostProbe` re-described from env-gated `SEMISTEP_PROBE=1` to `Explicit = true`. Pointer to `Docs/perf/README.md` added to the perf-discipline section.
- [x] Run `dotnet format SemiStep/SemiStep.slnx --verify-no-changes`. **DONE.** Clean, exit 0, no changes.

### Task 8: Verify acceptance criteria
- [x] Every lesson from the old probes maps to a passing new gate (walk the old→new map; no coverage lost). **DONE — all 6 lessons mapped to a green gate:**
  | Old lesson (probe) | New black-box gate | Result |
  | --- | --- | --- |
  | host re-attach counter (white-box) | `TransposedViewAllocationProbe.ScrollRoundTrips_CreateZeroFreshVisuals` → `FreshVisualInstances == 0` | PASS (freshVisuals=0) |
  | ↳ CAVEAT: re-attach of an EXISTING instance w/o rebuild | UI contract tests `TransposedChildRecycleTests`, `TransposedColumnsPanelContractTests`, `TransposedColumnsPanelItemsChangedTests` | all 3 files present, NOT migrated/deleted/weakened |
  | viewport-jump bytes (absolute) | `ViewportJump_BytesPerColumn_WithinParity_AndBaseline` → runner metric + 20% baseline + hard parity ratio ≤3.3x | PASS |
  | per-add sweep | `PerAdd_ScalesFlat_WithColumnCount` → N=120 vs N=20 ratio ≤1.5 | PASS |
  | selection discrimination | `TransposedSelectionCostProbe` → same-process time ratio ≤3x (N=4800/N=300) | PASS |
  | retention floor | `GridRetentionProbe` Transposed+Canonical → two-point flat-delta primary + weak-ref survivor + both-drivers layer isolation | PASS (both drivers) |
  | `CoreAllocationProbe` | baseline compare only (assert-or-record) | PASS |
- [x] `FreshVisualInstances == 0` scroll gate red-tests correctly: temporarily break recycling in a scratch worktree (or revert the `TransposedStepListBox` override locally) and confirm the gate FAILS, then restore — the gate must be proven falsifiable, not just green. **DONE — proven falsifiable.** Made `ClearContainerForItemOverride` call `base.ClearContainerForItemOverride(...)` (discards the recyclable subtree), Release-rebuilt, ran `-method "*ScrollRoundTrips*"` → **freshVisuals=108, gate FAILED** as expected. `git restore`d `TransposedStepListBox.cs` (clean), rebuilt, re-ran → **freshVisuals=0, gate GREEN**. The break was never committed.
- [x] Normal `dotnet test` run (no arguments, no env vars) does not execute any explicit scenario and stays green — CI unaffected. `grep -rn "GetEnvironmentVariable(\"SEMISTEP"` over the repo returns nothing. **DONE.** `dotnet test` (no args/env) = **1437 passed, 0 failed**; all 8 explicit probes reported `Пропущен`/skipped (not executed). `grep GetEnvironmentVariable("SEMISTEP` over `SemiStep/` → **zero hits** (the only repo-wide hit is this plan file's own documentation of the grep string).
- [x] Full suite + explicit run (`SemiStep.Tests.exe -explicit only`) both green. **DONE.** Normal suite = 1437 passed, 0 failed. Explicit run (`-explicit only`) = Total 1445, **Failed 0**, Not Run 1437 → all **8 explicit probes green**. Working tree clean except this plan file (no stray break, no scratch files).

### Task 9: Final documentation
- [x] Update `CLAUDE.md` test section: the Performance trait note becomes "explicit tests (xunit v3 `Explicit = true`), not run by default; run via `SemiStep.Tests.exe -explicit only`; see Docs/perf/README.md".
- [x] Move this plan to `Docs/plans/completed/`. (harness moves the plan after all phases — not moved here per exec protocol)

## Post-Completion
*Items requiring external action or future decisions — no checkboxes*

**CI guard (cheap, in the main workflow):** assert the default `dotnet test` does not run any explicit scenario (suite count/time must not include them) — protects against a runner regression silently pulling the slow probes into every PR run (the vscode-dotnettools#2261 bug class).

**Optional nightly CI job (deferred decision):** a scheduled GitHub Actions job running the deterministic tier via `SemiStep.Tests.exe -explicit only`, windows-latest, Release, with the capture prefix, once baselines prove stable for a few weeks. Bytes are reproducible on hosted runners with the SDK pinned via `global.json`; absolute wall-clock never gates, and the selection time-ratio is same-process and machine-independent, so runner noise is irrelevant. Not PR-blocking initially.

**BenchmarkDotNet tier (only if Core micro-benchmarks are wanted):** separate `SemiStep.Benchmarks` console project with `[MemoryDiagnoser]` and `[Benchmark(Baseline=true)]` + ResultsComparer, mirroring the `dotnet/performance` micro tier and `Avalonia.Benchmarks`; `CoreAllocationProbe` moves there rather than being duplicated.

**The manual oracle stays:** felt smoothness on real hardware (Release RIE, large recipe) remains a human check after significant grid work — the harness narrows what reaches the eye and attributes what the eye finds; it does not replace it.
