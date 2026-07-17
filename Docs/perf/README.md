# Performance Harness

This directory holds the black-box performance harness for the recipe grid and the committed
baselines it gates against. The harness turns the July grid-perf work into checks that survive
refactoring: it drives the real headless views through their public surface and reads
framework-boundary signals (allocated bytes, fresh visual instances, GC counts, retained floor),
so a panel rewrite that preserves behavior keeps the gates green, while a regression that
reintroduces subtree rebuild or O(N) scanning turns them red.

The probes live in `SemiStep/SemiStep.Tests/Performance/`; the reusable core (runner, signals,
drivers, baseline store) lives in `Performance/Harness/`. The baselines are `baselines.json` in
this directory.

## Canonical commands

The measurement facts are xunit v3 **explicit tests** (`Explicit = true`). A plain `dotnet test`
and CI never run them; they run only through the built test executable with `-explicit only`.

Build Release, then run all gates:

```powershell
dotnet build SemiStep/SemiStep.slnx -c Release
SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe -explicit only
```

Run a single gate (or a group) by method-name pattern:

```powershell
SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe -explicit only -method "*PerAdd_ScalesFlat*"
```

The current probes and their gate methods:

| Probe file | Method | What it gates |
| --- | --- | --- |
| `TransposedViewAllocationProbe` | `ScrollRoundTrips_CreateZeroFreshVisuals` | The headline invariant: a scrolled viewport creates 0 new visual instances after warmup. |
| `TransposedViewAllocationProbe` | `ViewportJump_BytesPerColumn_WithinParity_AndBaseline` | Bytes per realized column vs the soft baseline, plus the transposed/canonical parity ratio (hard cap). |
| `TransposedViewAllocationProbe` | `PerAdd_ScalesFlat_WithColumnCount` | Per-add bytes stay flat as column count grows (N=120 vs N=20 ratio). |
| `TransposedSelectionCostProbe` | `SelectionChangedCost_StaysFlatAsRecipeGrows` | Same-process selection time ratio at N=4800 vs N=300 (CPU-bound, allocation-neutral). |
| `GridRetentionProbe` | `Transposed_ScrollRetention_FlatFloor`, `Canonical_ScrollRetention_FlatFloor` | Retained-heap floor does not grow across N scroll cycles; bounded container survivors. |
| `CoreAllocationProbe` | `Report_PerAppend_CoreAllocation` | Core per-append allocation against the soft baseline (no view). |

### Optional capture prefix (steadier byte telemetry)

The soft byte baselines are the only signals sensitive to JIT tiering: background rejit and tier
transitions perturb allocation counts. When capturing or re-baselining byte numbers, disable
tiered compilation and dynamic PGO for that shell so the numbers settle:

```powershell
$env:DOTNET_TieredCompilation='0'; $env:DOTNET_TieredPGO='0'
SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe -explicit only
```

These knobs are scoped to the current PowerShell session. They matter only for the soft byte
tier. The hard gates (the `== 0` fresh-visuals invariant and the ratio gates) are tiering-immune
by construction, so a routine gate run does not need the prefix.

## Gate hierarchy

The gates are layered by reliability. The higher a tier sits, the less it depends on the machine
it runs on.

1. **Invariants (exact, never re-baselined).** `FreshVisualInstances == 0` after a scrolled
   round-trip. This is a reference-identity set-diff of the items-panel subtree before and after
   the workload: any newly created control fails it. It is exact, cross-machine, and carries no
   baseline entry in `baselines.json` — it is asserted directly in code. Swap the panel
   implementation and it stays green; reintroduce subtree rebuild and it goes red.

2. **Ratios and scaling (cross-machine, tolerance-gated).** These divide two measurements taken
   in one process, so machine speed cancels out:
   - **Per-add scaling** — per-add bytes at N=120 vs N=20 must stay flat (ratio ≤ 1.5). Catches
     a per-add cost that grows with recipe length.
   - **Transposed/canonical parity** — bytes per realized column for the transposed grid vs the
     canonical grid. This is a **hard cap of 3.3x today**. The current worst case is ~3.0x,
     driven by the `TransposedColumnCellsHost` + `TransposedColumnCellsPool` indirection still
     present in production. That indirection is scheduled for deletion (a separate follow-up);
     once the Host and pool are gone the cap tightens to **2.0x**. Until that deletion lands, the
     live gate is 3.3x, not 2.0x. The 2.0x figure is the target, not the current assertion.
   - **Selection time ratio** — the selection cost probe keeps its own `Stopwatch` and asserts a
     same-process wall-clock ratio at N=4800 vs N=300 ≤ 3x. This is the one CPU-bound,
     allocation-neutral gate; allocation signals cannot see the O(S·N) selection scan, and a
     same-process time ratio is its only viable signal.

3. **Soft byte baselines (telemetry with a hard cap).** Absolute byte metrics compared against
   `baselines.json` with a 20% tolerance on the recorded value, plus a hand-set hard budget cap
   per metric. The tolerance catches a step regression; the budget stops slow compounding drift
   (see the baseline-vs-budget split below).

**Absolute wall-clock milliseconds are never asserted.** They are machine-dependent and flaky by
construction. Same-process wall-clock *ratios* are permitted (tier 2) because dividing two
timings from one process cancels the machine's speed.

## Baseline vs budget split

Every soft byte metric in `baselines.json` carries two anti-drift levels:

- **`value`** — the measured baseline. It moves on re-baseline and, with `tolerancePct`, catches a
  step regression: the gate fails when `actual > value * (1 + tolerancePct/100)`. An improvement
  beyond tolerance does not fail; it prints a "baseline is stale, consider re-baselining down"
  advisory.
- **`budget`** — a hand-set absolute cap. The gate `actual <= budget` always applies, independent
  of the tolerance band. Re-baseline promotion never rewrites the budget (the merge carries it
  through verbatim), so it stops a sequence of small within-tolerance re-baselines from ratcheting
  the ceiling upward forever. A missing or null budget fails the probe with "set the budget by
  hand" guidance, and a `budget < value` is rejected at load as a config error. Raising a budget is
  a deliberate PR edit that carries its own justification.

In short: `value` drifts on re-baseline and catches per-step regressions; `budget` is a hard wall
that changes only by a hand edit with a stated reason.

## Re-baseline procedure

Re-baselining is a file copy. The actuals artifact that a probe run writes IS the proposed next
`baselines.json`.

1. **Run the explicit suite.** Failures are expected when the numbers have drifted — that is the
   point: see what changed before accepting it.
   ```powershell
   $env:DOTNET_TieredCompilation='0'; $env:DOTNET_TieredPGO='0'
   SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe -explicit only
   ```
2. **Copy the actuals artifact over the baselines.** The fixture writes
   `%TEMP%/semistep-perf-actuals-<pid>.json`. A failing probe prints the exact `Copy-Item`
   command with the resolved path; run it.
3. **Review the diff.** `git diff Docs/perf/baselines.json` — confirm the metric changes are the
   ones you expected and nothing else moved.
4. **Commit with a stated reason.** A re-baseline commit must name why the numbers moved.
   Legitimate reasons: a runtime or Avalonia bump, or a deliberate behavior change. "Tests were
   red" is not a reason.
5. **Re-run to confirm green.** The suite must pass against the just-committed baselines.

The written artifact is the FULL proposed baselines, not just the metrics you ran: the fixture
loads the current baselines and overlays only the measured metrics (values plus refreshed
context), carrying every unmeasured metric and every budget through untouched. So the copy is
safe even after a `-method`-filtered run that touched a single metric — the other metrics survive
verbatim.

## Context policy

The `context` block in `baselines.json` records the measurement environment, not the machine:

- `runtime`, `avalonia`, `os` (family + arch, e.g. `win-x64`), and `capturedUtc`.
- `testbed` is a **role label** identifying the environment: `dev-primary` today, `ci-hosted`
  later.

There are **no hostnames, machine names, or usernames**. This repo is public, and hardware
identity is irrelevant to these metrics: allocated bytes depend on the runtime and the code, not
on the CPU. Cross-machine reliability comes from the invariant and ratio tiers, not from pinning
the box.

## Headless blind spots

The harness runs on the Avalonia headless platform. It has no Skia and no compositor, so a whole
class of costs is invisible to it:

- `CreateSKFont` and text-layout / text-shaping cost.
- `CreateCompositionVisual` and composition-visual creation.

These show up only in a live, on-hardware trace. The harness attributes what the eye finds and
narrows what reaches the eye, but it does not measure felt smoothness. **Felt smoothness on real
hardware stays the manual oracle**: after significant grid work, a human still checks a Release
build against a large recipe. The harness does not replace that check.

## Diagnostic layer

`TransposedScrollTraceScenario` and `scripts/perf/speedscope-shares.py` are **not gates** — they
assert nothing. They are the diagnosis kit for when a black-box gate trips and you need to find
where the cost went.

`TransposedScrollTraceScenario.Drive_FixedWorkload_ForCpuTrace` drives the real transposed view
through a fixed workload (viewport jumps, add/remove steps, an execution-tick sweep) for a CPU
trace. It is explicit-gated like the rest; launch it under `dotnet-trace` in child-launch mode
against the Release test build:

```powershell
$env:DOTNET_TieredCompilation='0'; $env:DOTNET_TieredPGO='0'
dotnet-trace collect --format Speedscope -o after.speedscope.json -- `
  SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe `
  -explicit only -method "*TransposedScrollTraceScenario*"
```

`scripts/perf/speedscope-shares.py` then reads the resulting Speedscope JSON and prints the
absolute inclusive time spent in a fixed set of frames (attach, styling, measure, realize, and
cell-host acquire-and-bind — `TransposedColumnCellsHost.AcquireAndBind`), plus whether the
attach/styling frames appear under a `Realize` stack. That
mechanism-presence check is what tells you whether a scroll is rebuilding subtrees rather than
recycling them.

```powershell
python scripts/perf/speedscope-shares.py after.speedscope.json
```
