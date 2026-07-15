# Transposed Grid Binding Fix, Serilog Logging, and Comment Cleanup

## Overview

Four related changes, driven by a fully-diagnosed defect. During development the transposed recipe grid
"froze" only under the debugger (F5); Release and Debug-without-debugger (Ctrl+F5) are both smooth. The
cause is a fragile binding that logs ~1155 errors on a short scroll, routed through Avalonia's
`.LogToTrace()` to `System.Diagnostics.Trace`. In production (no trace listener) those go nowhere and cost
nothing — hence no user-facing problem. Under the debugger each write is a synchronous `OutputDebugString`
to Debug Output (~1 ms), so the storm freezes the UI thread. This is **not** a production performance fix;
it fixes a real latent binding bug, unifies the logging channel, adds a CI guard against this whole bug
class, and clears accumulated comment noise.

Root cause (verified): the transposed cell-background `MultiBinding` reads `ListBoxItem.IsSelected` via
`RelativeSource FindAncestor ListBoxItem` (`TransposedColumnCellsPresenter.cs`, the 7th leg in
`BuildCellSlot`). Under virtualization/pool recycling the cell `Border` is transiently not under a
`ListBoxItem` (pooled/detached), so Avalonia logs `[Binding] '(unknown)' to
'$visualParent[ListBoxItem].IsSelected' : 'Ancestor not found.'`. Long-standing (present since the first
build; the round-3 pool amplified the frequency — it is not a round-3 regression).

The four parts (numbered in task/execution order):
1. **Serilog logging sink** — replace `.LogToTrace()` with a custom `Avalonia.Logging.ILogSink` that
   forwards to Serilog (structured, Warning, all areas, per-template throttle). Neutralizes the
   debugger-freeze mechanism for any future binding storm and unifies the two log channels.
2. **CI guard** — a headless test helper that fails on `LogArea.Binding` events, plus a transposed
   scroll+select test asserting zero binding errors. Proves the fix and blocks regressions of this class.
3. **Fix the binding** — source the selection state from the presenter itself (which builds the cell
   slot), bound directly with `Source = this`, and keep it in sync with the container `ListBoxItem`
   imperatively from the host. Zero "ancestor not found", and no tree traversal at all.
4. **Comment cleanup** — apply the `dev:comment-hater` audit (41 DELETE, 19 SHORTEN, 2 stale-wording
   fixes, 37 KEEP) across the transposed grid; the design rationale already lives in
   `Docs/architecture/recipe-grid-surface.md`, so essays are deleted, not relocated.

## Context (from discovery)

- Files/components involved:
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPresenter.cs` — `BuildCellSlot`
    builds the cell `Border` + background `MultiBinding`; the fragile `IsSelected` leg is here (~line
    117-136). Presenters are pooled (`TransposedColumnCellsPool`) and hosted by
    `TransposedColumnCellsHost`; cells are always children of the presenter, even when the presenter is
    detached from the top level.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsHost.cs` — the seam inside the
    `ListBoxItem`: `OnAttachedToVisualTree` / `OnDetachedFromVisualTree` / `AcquireAndBind` /
    `ReleasePresenter`. Knows how to resolve the container `ListBoxItem`.
  - `SemiStep/SemiStep.UI/App.axaml.cs:73` — `.LogToTrace()` in `BuildAvaloniaApp()`.
  - `SemiStep/SemiStep.UI/Program.cs` — Serilog bootstrap; `Log.Logger` is set before `App.Run`, so the
    Avalonia sink can read the static `Serilog.Log` lazily and startup ordering is safe.
  - Test harness: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`, `TransposedVirtualizationTests.cs`,
    selection-controller tests.
  - `Docs/architecture/recipe-grid-surface.md` — already documents the pool, lazy swap,
    commit-before-rebind, select-then-edit, arrow semantics, stale-guard, background converter.
- Verified facts: Release and Debug-no-debugger are smooth; Avalonia binding errors are LOGGED, not
  thrown (exception breakpoints never fire); `.LogToTrace()` -> Trace is the routing; ~1155 errors per
  short scroll from the single fragile leg.
- Swept: this `RelativeSource FindAncestor ListBoxItem` leg is the ONLY declarative ancestor binding in
  the whole UI project (zero `RelativeSource` usages in any `.axaml`); the imperative `FindAncestorOfType`
  lookups (`TransposedGridNavigator.cs:64`, `TransposedRecipeGridView.axaml.cs:253`,
  `TransposedCellTemplateFactory.cs:158`, `TransposedColumnCellsHost.cs:21`) are one-shot and do not log.
  No wider sweep needed.
- Dependencies: Avalonia 12 `Avalonia.Logging` (`ILogSink`, `Logger.Sink`, `LogArea`,
  `LogEventLevel`); Serilog + Microsoft.Extensions.Logging; `[AvaloniaFact]` headless tests.

## Development Approach

- **testing approach**: Regular (code-first), mandatory tests per task. The binding-error guard is itself
  the key regression test for this whole effort.
- complete each task fully before the next; small focused changes; keep the full suite green throughout.
- **CRITICAL: every task adds/updates tests and they must pass before the next task.**
- **CRITICAL: keep every task green** — the guard helper (Task 2) ships WITHOUT a failing assertion; the
  zero-binding-errors assertion test lands WITH the fix (Task 3) so it is green when committed.
- **CRITICAL: update this plan file when scope changes.**
- Comments-only cleanup (Task 4) must not change behavior; run last so line numbers have settled.

## Testing Strategy

- **unit tests**: the Serilog sink (throttle full-then-sample, level mapping, structured template
  forwarding) via a captured Serilog test sink.
- **headless [AvaloniaFact] tests**: selection background still paints after the binding fix; recycled
  column shows correct selection; transposed scroll-sweep + select logs ZERO `LogArea.Binding` events
  (the guard). Keep `TransposedVirtualizationTests` / selection tests green.
- **guard infrastructure**: a `BindingErrorGuard` that installs a collecting `ILogSink` for the duration
  of a test and restores the prior sink; handle static-`Logger.Sink` scoping and test parallelization
  (serialize or scope so concurrent tests do not race the global sink).
- **measurement gate (not CI)**: none new; this is a correctness + hygiene change, not a perf change.

## Progress Tracking

- mark completed items `[x]` immediately; add discovered work with `➕`, blockers with `⚠️`.
- record the guard's before/after binding-error count (should go from ~1155 on a scroll to 0 after Task 3).
- **Task 3 binding-error count:** before ~1155 (`Ancestor not found` on a short transposed scroll, from the
  `RelativeSource FindAncestor ListBoxItem` leg) -> after **0**. `TransposedSelectionBindingTests.ScrollSweepAndSelect_LogsZeroBindingErrors`
  wraps a scroll start->end->start plus a column select in `BindingErrorGuard` and asserts zero `LogArea.Binding` events.

## Solution Overview

- **Selection sourced from the presenter (Task 3)**: add `IsColumnSelected` (an Avalonia
  `StyledProperty<bool>` or `DirectProperty<TransposedColumnCellsPresenter,bool>`) to the presenter.
  `BuildCellSlot` is an instance method on the presenter, so the cell's selection leg binds DIRECTLY to
  it: `new Binding(nameof(IsColumnSelected)) { Source = this }` — no `RelativeSource`, no tree traversal,
  cannot fail off-tree by construction. The host keeps `presenter.IsColumnSelected` in sync with the
  container `ListBoxItem.IsSelected` imperatively: one held `IDisposable` subscription, resolved when the
  container is available, disposed-before-resubscribe across recycle, tolerant of a null container
  pre-attach (re-resolve on attach), and reset to `false` in `ReleasePresenter` so a pooled presenter
  never carries stale selection into its next column. Selection source of truth stays the `ListBoxItem`
  (no divergence, no VM/surface changes).
- **Serilog sink (Task 1)**: `AvaloniaSerilogSink : ILogSink` forwards `(level, area, source, template,
  args)` to `Serilog.Log.ForContext("SourceContext","Avalonia."+area).Write(mapped, template, args)` —
  structured, not pre-formatted. Cheap on the hot path: `IsEnabled = level >= min` gate first, throttle
  before any Serilog call (the throttle key concat + `ConcurrentDictionary` update do allocate — do not
  claim "allocation-free"). Per-(area+template) throttle via `ConcurrentDictionary<string,int>`: first 20
  in full, then every 500th with the running count. Note Avalonia funnels most binding errors through one
  shared template, so this throttle merges distinct binding failures under one counter — the
  `BindingErrorGuard` CI test, not the log, is the regression net (which is why min-Warning/all-areas is
  acceptable). `LogToSerilog(this AppBuilder, level=Warning)` sets `Logger.Sink` and replaces `LogToTrace`.
- **Guard (Task 2)**: `BindingErrorGuard` installs a collecting `ILogSink` (records `LogArea.Binding`
  events, restores the previous sink on dispose) for use inside headless tests.
- **Comment cleanup (Task 4)**: re-run `dev:comment-hater` on the transposed scope (line numbers shift
  after Tasks 1-3), apply DELETE/SHORTEN, fix the 2 stale comments, keep the load-bearing set.

## Technical Details

- The two `LogEventLevel` enums (Avalonia vs Serilog) collide by name — alias both and map with an
  explicit `switch`, never a cast.
- Avalonia logs on the thread that hit the issue (usually the UI thread); the sink must stay cheap on the
  hot path (`IsEnabled` gate first, throttle before any Serilog call).
- `Logger.Sink` is a single global slot (last assignment wins) — set it once in `BuildAvaloniaApp`; in
  tests set/restore it around the guarded interaction.
- Do NOT add `Serilog.Sinks.Async` or any package; the throttle plus the buffered file sink are enough.
- Selection behavior must be pixel-identical: the selected column's cells paint the selected background
  exactly as before; read-only/inapplicable/changed/execution states unaffected.
- No new binding may reintroduce a visual-ancestor lookup that can fail off-tree.

## What Goes Where

- **Implementation Steps** (checkboxes): sink, guard, binding fix, comment cleanup, verify, docs — all in
  this repo.
- **Post-Completion** (no checkboxes): manual confirmation that F5 debugging no longer freezes on a
  transposed scroll at ~200-1000 steps; decision to keep or push the branch.

## Implementation Steps

### Task 1: Forward Avalonia diagnostics to Serilog (replace LogToTrace)

**Files:**
- Create: `SemiStep/SemiStep.UI/Logging/AvaloniaSerilogSink.cs`
- Create: `SemiStep/SemiStep.UI/Logging/SerilogLoggingExtensions.cs`
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Logging/AvaloniaSerilogSinkTests.cs`

- [x] Implement `AvaloniaSerilogSink : Avalonia.Logging.ILogSink` (ctor takes minimum
      `Avalonia.Logging.LogEventLevel`): `IsEnabled` = level >= min; both `Log` overloads forward the
      template + args structured to `Serilog.Log.ForContext("SourceContext","Avalonia."+area)`, level
      mapped by explicit switch (alias the two `LogEventLevel` enums).
- [x] Add the per-(area+template) throttle: `ConcurrentDictionary<string,int>` counter, first 20 in full,
      then every 500th carrying the running `Occurrence` count; below-threshold repeats are dropped.
- [x] Add `SerilogLoggingExtensions.LogToSerilog(this AppBuilder, LogEventLevel level = Warning)` that
      sets `Avalonia.Logging.Logger.Sink = new AvaloniaSerilogSink(level)`; replace `.LogToTrace()` at
      `App.axaml.cs:73` with `.LogToSerilog()`. Policy: min Warning, all areas, no `#if`.
- [x] Write tests: feeding N identical (area,template) events yields full-then-sampled output with a
      correct running count; distinct templates are throttled independently; each Avalonia level maps to
      the expected Serilog level; template+args reach Serilog structured (assert via a captured Serilog
      sink / test logger, not string matching).
- [x] Build + run tests - must pass before Task 2.

### Task 2: Binding-error test guard (collecting ILogSink)

**Files:**
- Create: `SemiStep/SemiStep.Tests/UI/Helpers/BindingErrorGuard.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Helpers/BindingErrorGuardTests.cs`

- [x] Implement `BindingErrorGuard` (IDisposable): on construction install a collecting
      `Avalonia.Logging.ILogSink` that records events (filterable to `LogArea.Binding`), remembering and
      restoring the previous `Logger.Sink` on dispose. Expose the captured binding-error messages/count
      and an `AssertNoBindingErrors()` helper.
- [x] Restore safely with try/finally (dispose): test parallelization is ALREADY disabled assembly-wide
      (`SemiStep.Tests/AssemblyAttributes.cs` has `[assembly: CollectionBehavior(DisableTestParallelization
      = true)]`) and `TestAppBuilder.cs` installs no sink, so the prior `Logger.Sink` is null in tests —
      the guard just needs to capture and restore the previous sink (including null) on dispose. Do NOT add
      an xUnit collection for this; it is not needed.
- [x] Write tests: the guard captures a deliberately-induced binding error (e.g. a throwaway control with
      a binding to a missing ancestor) and reports it; reports zero + restores the previous sink when no
      error occurs.
- [x] Build + run tests - must pass before Task 3.

### Task 3: Fix the selection binding (source it from the presenter)

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPresenter.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsHost.cs`
- Create/Modify: a transposed selection + binding-error test (e.g.
  `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedSelectionBindingTests.cs`)

- [x] Add `IsColumnSelected` (Avalonia `StyledProperty<bool>` or `DirectProperty`) to
      `TransposedColumnCellsPresenter`. Used a `DirectProperty<TransposedColumnCellsPresenter, bool>`.
- [x] Change the selection leg in `BuildCellSlot` from `Binding(IsSelected){RelativeSource FindAncestor
      ListBoxItem}` to `new Binding(nameof(IsColumnSelected)) { Source = this }` (direct source, no
      `RelativeSource`, no ancestor lookup); keep the other 6 legs and the converter unchanged.
- [x] In `TransposedColumnCellsHost`, keep `presenter.IsColumnSelected` in sync with the container
      `ListBoxItem.IsSelected`: hold ONE `IDisposable` subscription; resolve the `ListBoxItem` when
      available (tolerate a null container pre-attach and re-resolve on attach); dispose-before-resubscribe
      across recycle so no double-subscription; push the current value on bind; and reset
      `presenter.IsColumnSelected = false` in `ReleasePresenter` so a released presenter carries no stale
      selection into its next acquisition. `AcquireAndBind` runs from `OnAttachedToVisualTree`,
      `OnDataContextChanged`, and in-place recycle — the subscription logic must be idempotent across all
      three.
- [x] Write tests: selecting a column paints its cells' selected background; DESELECTING reverts it
      (proves the imperative sync propagates change notifications, not just the initial push); a presenter
      recycled out of a selected column into an unselected one paints unselected; using `BindingErrorGuard`,
      a transposed scroll start->end->start plus a column select logs ZERO `LogArea.Binding` events (record
      the before number ~1155 in the plan for contrast). The parity oracle is
      `TransposedCellStyleRenderTests` (it already asserts `Border.Background` same-instance vs
      `CellPaletteInstaller.SelectionBackgroundBrushKey` / the changed+selected brush) — those cases must
      stay green unmodified. New file: `TransposedSelectionBindingTests.cs` (4 `[AvaloniaFact]`).
- [x] Keep `TransposedVirtualizationTests` and selection-controller tests green. Build + run full suite -
      must pass before Task 4. Full suite: 1370 passed / 2 skipped / 0 failed (baseline 1366 + 4 new).

➕ **Discovered subtlety (`OnAttachedToVisualTree` re-announce):** the old `RelativeSource FindAncestor`
leg did double duty — it re-emitted when its `ListBoxItem` ancestor was found on attach, which re-ran the
background converter *after* the slot `Border` could reach the palette resources. A pooled presenter's
legs otherwise settle while detached, so the converter runs once against an unreachable resource host and
yields no brush (idle cells rendered `null`, breaking the parity oracle). Fix: `TransposedColumnCellsPresenter.OnAttachedToVisualTree`
re-announces the `IsColumnSelected` leg (`RaisePropertyChanged(IsColumnSelectedProperty, !_isColumnSelected, _isColumnSelected)`)
so the converter re-evaluates once the `Border` is attached. Isolated probe confirmed the `Source = this`
leg does emit its initial value; the failure was purely resource-host reachability at first evaluation.

### Task 4: Apply the comment-hater cleanup across the transposed grid

**Files:**
- Modify: all `.cs` under `SemiStep/SemiStep.UI/RecipeGrid/Transposed/`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`,
  `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`

- [x] Re-run `dev:comment-hater` on the scope above (line numbers have shifted after Tasks 1-3) to get a
      current ranked kill-list.
- [x] DELETE the class-header design essays and next-line restatements whose content is already in
      `Docs/architecture/recipe-grid-surface.md` (do not relocate — it would duplicate the doc).
- [x] SHORTEN the verbose-but-real comments to their one-line load-bearing "why"; KEEP the load-bearing
      set (Avalonia gotchas, stale-guard decode, ordering constraints, tri-state decodes, recycle hazards)
      untouched.
- [x] Fix the 2 stale-wording comments that no longer match the lazy-editor code: "always-live editors"
      (`TransposedRecipeGridView.axaml.cs`, ~lines 64 and 236) and "FontSize explicit"
      (`TransposedCellTemplateFactory.cs`, ~line 166 — code calls `ApplyCellFont`, no explicit FontSize).
- [x] Comments-only, no behavior change: build + run full suite (must stay green) and
      `dotnet format --verify-no-changes` clean before Task 5.

### Task 5: Verify acceptance criteria

- [x] Confirm the guard test reports zero `LogArea.Binding` events on the transposed scroll+select
      (the core acceptance signal); record the before (~1155) / after (0) in Progress Tracking.
      Verified: `TransposedSelectionBindingTests` 4/4 passed (incl. `ScrollSweepAndSelect_LogsZeroBindingErrors`);
      before ~1155 / after 0 already recorded in Progress Tracking above.
- [x] Confirm selection-background parity: `TransposedCellStyleRenderTests` (the selected-column and
      changed+selected `Border.Background` same-instance cases) stays green unmodified; no regression in
      `TransposedVirtualizationTests`, selection, editing, navigation tests.
      Verified: `TransposedCellStyleRenderTests` 7/7 passed unmodified; `Area=RecipeGrid` 361 passed / 1 skipped / 0 failed.
- [x] Confirm the Serilog sink is wired (`.LogToSerilog()` replaces `.LogToTrace()`), min Warning, all
      areas; the throttle is exercised by the sink unit test.
      Verified: `App.axaml.cs:74` calls `.LogToSerilog()` (default `LogEventLevel.Warning`, no area filter in `IsEnabled`);
      no `.LogToTrace()` remains in code (only plan-doc prose); `AvaloniaSerilogSinkTests` 9/9 passed.
- [x] Run the full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
      Verified: 1370 passed / 2 skipped / 0 failed (matches the Task 4 baseline).
- [x] Run `dotnet format SemiStep/SemiStep.slnx` (pre-commit hook enforces it).
      Verified: `dotnet format "SemiStep\SemiStep.slnx" --verify-no-changes` exits 0 with no output (clean).
- [x] Decide the fate of the uncommitted investigation artifact
      `SemiStep/SemiStep.Tests/Performance/TransposedViewLatencyProbe.cs`: recommend REMOVE (it measures a
      forced synchronous worst-case that did not reflect production and misled; the allocation probe
      stays). Delete it (or, if kept, relabel clearly as a manual latency probe) and note the choice.
      Verified REMOVED: file absent on disk and not in `git ls-files` (only `CoreAllocationProbe.cs` and
      `TransposedViewAllocationProbe.cs` remain tracked under `Performance/`).

### Task 6: Update documentation

- [x] Update `Docs/architecture/recipe-grid-surface.md`: note the logging channel (Avalonia diagnostics
      forwarded to Serilog via `AvaloniaSerilogSink`, replacing `LogToTrace`) and that the cell selection
      state is sourced from `TransposedColumnCellsPresenter.IsColumnSelected` (fed from the container
      `ListBoxItem`) rather than a `RelativeSource` ancestor lookup.
      Done: added a "Framework diagnostics logging" section (sink, throttle, `BindingErrorGuard`, zero
      binding errors invariant) and expanded the "Cell background via a converter" bullet with the
      presenter-sourced `IsColumnSelected` leg.
- [x] Update `CLAUDE.md` only if a durable convention emerged (e.g. "Avalonia diagnostics go through the
      Serilog sink; a healthy build logs zero binding errors, enforced by BindingErrorGuard"). Keep it to
      a short line; skip if it does not rise to a durable cross-cutting rule.
      No new durable convention added to CLAUDE.md; it is the project overview file ("do not add specifics
      here"), so this grid/logging fact was routed to recipe-grid-surface.md instead.
- [x] Move this plan to `Docs/plans/completed/`.
      [x] harness moves the plan after finalize (not moved here).

## Post-Completion

*Items requiring manual intervention or external systems - no checkboxes, informational only*

**Manual verification**:
- Run the app under the debugger (F5), RIE / transposed, at ~200-1000 steps, and confirm scrolling no
  longer freezes and the Debug Output no longer floods with `[Binding] ... Ancestor not found`.
- Confirm the app's Serilog file now carries any Avalonia Warning-level diagnostics (throttled), and that
  a normal session logs zero binding errors.

**Branch / PR shape**:
- This runs on `transposed-grid-realize-cost-reduction` (round-3, unpushed) or a fresh branch off it —
  decide at exec time. Round-3 and this change are both unpushed.
- Commit per task (clean per-task commits). Intended PR split is three logical changes: (a) the logging
  sink + binding-error guard, (b) the binding fix, (c) the comment cleanup — either as one PR with clean
  per-task commits or three stacked PRs, named explicitly so the large comment-churn diff does not obscure
  the fix review. Comment cleanup (Task 4) stays last regardless.
