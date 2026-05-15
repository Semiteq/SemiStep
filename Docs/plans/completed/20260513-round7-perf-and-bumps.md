# Round-7: scroll perf fixes and dependency bumps

## Overview

Four targeted fixes addressing the memory growth + GC pressure observed during active scrolling of the recipe grid, plus a mass dependency bump to close out the modernization round. Built on top of the existing `feature/avalonia-12-migration` branch (3 commits: Avalonia 12, xUnit v3, ReactiveUI.Avalonia).

Scope is constrained: no `supportsRecycling: true` flip and no XAML-based compiled-binding refactor — those are Round-8. The git history (`91241b8` → `dc85fee`) shows the recycling flip was reverted previously without explanation; the working hypothesis is that ComboBox and editing templates close over `row` instance state (`row.ColumnFormatKinds`, `row.ActionId`, `SelectionChanged` lambda) making recycling visually unsafe. Round-7 fixes only what can land without touching that closure machinery.

Expected outcome:
- One real strong-ref leak gone (ComboBox event handler ↔ row).
- Lower gen-0 churn from converter hot paths.
- Defensive guard against repeat-activation rebuild.
- All dependencies on the latest stable.
- 307/307 tests stay green.

## Context (from discovery)

Source audits in this session (4 subagents):
1. **Reactive subscription audit — clean.** `RecipeRowViewModel` holds no subscriptions, `RaiseAndSetIfChanged` only. Not the leak source. Minor `.DisposeWith(_disposables)` polish on a few command ownerships is unrelated to scroll.
2. **Allocation hotspots audit:** main churn in `PropertyTimeMultiConverter.Convert` (LINQ `.Any()` + boxing + per-cell string format), `TimeFormatHelper.FormatValue` (per-cell interpolation), and `StepStartTime` double-formatted through both the VM and the converter.
3. **DataGrid virtualization audit:** `supportsRecycling: false` on 5 of 6 cell templates is the dominant churn driver. Plus a **real strong-ref leak** at `ComboBoxCellFactory.cs:99-106` — `comboBox.SelectionChanged += (_, _) => { ... row.SetPropertyValue(...) }` captures `row`, never unsubscribes; ComboBox holds row through event, row holds ComboBox through delegate; orphan ComboBoxes accumulate on every scroll.
4. **Avalonia 12 / ReactiveUI 23 perf gotchas:** code-built `new Binding(path)` is always reflection-based (compiled bindings only for XAML); allocations during virtualization realisation. `Avalonia.Controls.DataGrid 12.0.0` is the latest published — no newer patch available.

Defensive verification subagents:
- **RecipeRows mutation:** verified `ObservableCollection<RecipeRowViewModel>` is get-only, mutated in place across `AppendRow` / `InsertRows` / `RebuildRow` / `FullRebuild` (Clear+Add). Avalonia DataGrid `Items` reassignment leak (issue #87) does NOT apply.
- **BuildGrid re-entry:** `ColumnBuilder.BuildColumns` already does `grid.Columns.Clear()` + `InvalidateCaches()` first — columns don't accumulate. But `WhenActivated` may fire repeatedly (modal interactions, hide/show), triggering full column rebuild + template re-allocation. Not a leak, but unnecessary gen-0 churn. Minimal guard: `_columnsBuilt` flag in MainWindow.

Git history audit: `91241b8` (2026-02-25) flipped `supportsRecycling: true` along with attach/detach handler plumbing; `dc85fee` (2026-03-02) silently reverted with "UI refactoring, bug fixes" — no explanation, no issue cited. The pattern is preserved in `TextCellFactory.CreateDisplayTemplate` (pure MultiBinding, no closures — safely `true`) and reverted everywhere else (closures over `row` make recycled cells visually stale).

Package bump audit produced a clean list (see Task 1).

## Development Approach

- **Testing approach: Regular.** Existing 307 tests are the regression net — they cover DI graph composition, ReactiveUI command bindings, recipe analysis, config loading. Headless tests don't measure scroll allocation; that's manual. New tests added only where new logic appears (Task 3 guard).
- Complete each task fully before moving to the next.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- Commit per task (4 task commits + 1 verify + 1 archive). No bundling — each task is self-contained.

## Testing Strategy

- **Unit / integration tests:** existing 307 tests stay green. No new tests for Task 1 (bumps) or Task 4 (converter perf cleanup — output unchanged, only allocation pattern differs).
- **Task 2 (ComboBox leak):** at least one new `[AvaloniaFact]` asserting that picking an item in the action ComboBox still triggers the same row state update (regression net for the lambda → binding rewrite). Existing `RecipeGridViewModelTests` or `RecipeRowViewModelTests` may already cover this — check first.
- **Task 3 (BuildGrid guard):** one unit test asserting `BuildGrid()` invokes `_columnBuilder.BuildColumns` exactly once across repeated calls.
- **Manual UI smoke (Task 5):** launch the app, scroll a recipe of ≥100 rows up/down repeatedly. Observe memory in Task Manager / dotnet-counters. Pre-fix: linear growth + frequent gen-0 collections. Post-fix: bounded memory + lower gen-0 rate. Document baseline-vs-after numbers in commit message or progress log if measurable.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

Six tasks executed sequentially on `feature/avalonia-12-migration`:

1. **Task 1 — Bumps.** Patch versions across Avalonia, MEL, FluentAssertions, Serilog. Single commit.
2. **Task 2 — ComboBox event-handler leak.** Replace the `SelectionChanged += lambda` pattern with a `SelectedItem` binding using a converter that maps `ActionDefinition` ↔ action key. Eliminates the strong ref cycle. Single commit.
3. **Task 3 — BuildGrid re-entry guard.** `_columnsBuilt` boolean field in MainWindow.axaml.cs, early return in `BuildGrid()`. Trivial. Single commit.
4. **Task 4 — Converter allocation hotspots.** Three targeted fixes inside `PropertyTimeMultiConverter` and `TimeFormatHelper`, plus removing `StepStartTime`'s second pass through MultiConverter. Single commit.
5. **Task 5 — Verify.** Build, tests, manual UI smoke. Plan checkbox-only commit.
6. **Task 6 — Archive plan + Round-7 docs.** Move plan to `Docs/plans/completed/`, extend `Docs/07-non-functional.md` with a Round-7 subsection. Single commit.

After Task 6 the PR is ready to open.

## Technical Details

### Task 1 — Package bumps

In `SemiStep/SemiStep.Core/SemiStep.Core.csproj`, `SemiStep/SemiStep.UI/SemiStep.UI.csproj`, `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`:

| Package | From | To | Notes |
|---|---|---|---|
| `Avalonia` | 12.0.2 | 12.0.3 | patch |
| `Avalonia.Desktop` | 12.0.2 | 12.0.3 | patch |
| `Avalonia.Themes.Fluent` | 12.0.2 | 12.0.3 | patch |
| `Avalonia.Win32` | 12.0.2 | 12.0.3 | patch |
| `Avalonia.HarfBuzz` | 12.0.2 | 12.0.3 | patch |
| `Microsoft.Extensions.DependencyInjection` | 10.0.7 | 10.0.8 | patch |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.7 | 10.0.8 | patch |
| `Microsoft.Extensions.Logging` | 10.0.5 | 10.0.8 | patch (3 behind) |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.5 | 10.0.8 | patch (3 behind) |
| `System.IO.Hashing` | 10.0.7 | 10.0.8 | patch |
| `FluentAssertions` | 8.9.0 | 8.10.0 | minor |
| `Serilog.Extensions.Logging` | 9.0.2 | 10.0.0 | major (MEL10 alignment — not a real API break) |

NOT bumped:
- `Avalonia.Controls.DataGrid 12.0.0` — latest published, no newer patch.
- `S7netplus 0.20.0` — last release 2023, no newer.
- Everything else verified at latest.

### Task 2 — ComboBox event-handler leak

Current (`ComboBoxCellFactory.cs:74-110` approximately):

```csharp
return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
{
    var comboBox = new ComboBox { ... ItemsSource = row.ActionItems, ... };
    comboBox.SelectionChanged += (_, _) =>
    {
        if (comboBox.SelectedItem is ActionDefinition action)
        {
            row.SetPropertyValue(columnKey, action.Id);
        }
    };
    return comboBox;
}, supportsRecycling: false);
```

After (target shape, mirroring the group editing template at ComboBoxCellFactory.cs:161):

```csharp
return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
{
    var converter = new ComboBoxItemSelectionConverter(row.ActionItems);
    var comboBox = new ComboBox { ItemsSource = row.ActionItems };
    comboBox.Bind(ComboBox.SelectedItemProperty, new Binding($"[{columnKey}]")
    {
        Mode = BindingMode.TwoWay,
        Converter = converter,
    });
    return comboBox;
}, supportsRecycling: false);
```

Key decisions per plan-review:
- **Reuse `ComboBoxItemSelectionConverter`** (already exists in the file). Its `ConvertBack` returns `int` (action id); confirm `RecipeRowViewModel`'s indexer setter accepts int directly (or wraps to `PropertyValue`-compatible type). The current leak code writes `selected.Id.ToString()` — the new path writes the int from `ConvertBack`. Verify the indexer write path handles both; adjust `ConvertBack` to return string if the indexer demands it.
- **Constructor-injected converter, NOT `ConverterParameter`.** For the action column, `row.ActionItems` is the globally cached list (`GetOrCreateActionItems` returns `_cachedActionItems`), so one converter instance per row is fine — same pattern the group editing template already uses.
- **`BindingMode.TwoWay`** so the ComboBox writes back to the row property when the user picks an item.
- **`supportsRecycling: false` STAYS** — this task fixes the strong-ref leak only. Recycling is Round-8 (requires eliminating closures over `row` more broadly). Document this in the commit body so reviewers understand the gen-0 churn from re-allocating ComboBox per scroll persists until Round-8.

The TwoWay binding replaces both the `SelectionChanged += lambda` and any manual `SetPropertyValue` write. Binding lifecycle is managed by Avalonia: when the ComboBox is detached/finalized the binding subscription releases — no strong-ref cycle survives.

### Task 3 — BuildGrid re-entry guard

In `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`:

```csharp
private bool _columnsBuilt;

private void BuildGrid()
{
    if (_columnsBuilt || _columnBuilder is null || ViewModel is null)
    {
        return;
    }
    _columnBuilder.BuildColumns(RecipeGrid);
    _columnsBuilt = true;
}
```

The flag is private, not reset anywhere — if dynamic column rebuild is ever needed (config reload), the call site that triggers it must reset `_columnsBuilt = false` explicitly. Today there is no such path; document via XML comment on the field.

### Task 4 — Converter allocation hotspots

**4a. `PropertyTimeMultiConverter.Convert` (`SemiStep.UI/RecipeGrid/PropertyTimeMultiConverter.cs`):**

Replace `values.Any(v => v == AvaloniaProperty.UnsetValue)` with a `for (var i = 0; i < values.Count; i++)` loop. Eliminates the LINQ enumerator/closure allocation per cell binding evaluation.

If `values[0]` is already a `string`, skip `ToString()` — assign directly.

**4b. `StepStartTime` direct binding** (the column routing already exists — this task only trims the MultiBinding wrap):

`TextCellFactory.ResolveBindingPath` already special-cases `columnKey == TimeFormatHelper.StepStartTimeColumnKey` and returns `nameof(StepStartTime)` as the binding path. The constant is hard-coded — config-supplied alternative keys would already break today, independent of this round; no new coupling is introduced here.

What changes: in `TextCellFactory.CreateDisplayTemplate`, when `columnKey == StepStartTimeColumnKey`, build a simple `new Binding(nameof(StepStartTime))` directly bound to `TextBlock.Text` — bypass `PropertyTimeMultiConverter` entirely on this branch. Saves one MultiConverter invocation per visible row per scroll frame, and `StepStartTime` is pre-formatted by `RecipeRowViewModel` so no re-formatting needed.

**Task 4b (memoization) explicitly DROPPED.** Original plan proposed `Dictionary<(double, string, string), string>` cache with `lock` + `Clear()` overflow. Per plan-review: this is premature optimization (per CLAUDE.md YAGNI rule). The `lock` is unjustified (Avalonia value converters run on UI thread by contract). After 4a + 4c, `time_hms` formatting is no longer on the StepStartTime hot path; remaining call sites are rare. Real allocation win is 4a (LINQ removal) + 4c (StepStartTime direct bind). If post-Round-7 profiling still shows `FormatValue` allocations dominating, revisit then with profiling evidence.

### Task 5 — Verify

- `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 307/307 (or slightly higher if Task 2/3 added tests).
- Manual UI smoke: open a recipe with ≥100 rows, scroll continuously for 30+ seconds. Observe memory in Task Manager. Pre-fix baseline (if measured before this round): linear growth, ~X MB/min. Post-fix expectation: bounded memory, stabilises after warm-up.
- `git diff master..HEAD --stat` — confirm scope confined to UI cell factories + 1 main window file + converters + csprojs + plan/docs. No incidental edits.

### Task 6 — Archive plan + docs

- `git mv Docs/plans/20260513-round7-perf-and-bumps.md Docs/plans/completed/`.
- Extend `Docs/07-non-functional.md` with a Round-7 subsection after Round-6, summarising: package patch bumps, ComboBox event-handler leak fix (real strong-ref leak, not just churn), BuildGrid re-entry guard, converter allocation hotspots flattened. Note Round-8 follow-up: `supportsRecycling: true` requires rewriting cell template closures to pure bindings; XAML data templates for compiled bindings is a separate effort.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): csproj bumps, ComboBox binding refactor, MainWindow guard, converter edits, docs.
- **Post-Completion** (no checkboxes): manual scroll smoke is on the user (Task 5 marks it `[x] manual scroll smoke (skipped - not automatable in headless)`).

## Implementation Steps

### Task 1: Mass package bumps

**Files:**
- Modify: `SemiStep/SemiStep.Core/SemiStep.Core.csproj`
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj`
- Modify: `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

- [x] Apply the version table from Technical Details. Verify each PackageReference Version attribute in each csproj individually — some packages appear in multiple csprojs (Microsoft.Extensions.Logging.Abstractions etc.) and must unify.
- [x] Run `dotnet restore SemiStep/SemiStep.slnx`. Confirm no unexpected transitive bumps. **Specifically check for `NU1608` (version conflict) or `NU1605` (downgrade) warnings caused by `Avalonia.Controls.DataGrid 12.0.0` vs `Avalonia 12.0.3` skew** — Avalonia controls packages are normally version-locked. If a warning appears, the fallback is to pin Avalonia back to 12.0.2 until DataGrid catches up; record the decision in commit message.
- [x] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 307/307 green. Kill testhost first if file locks block rebuild.
- [x] Commit:
  ```
  chore(deps): bump dependencies to latest stable
  ```
  Body: list bump table.

### Task 2: Fix ComboBox event-handler leak

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Possibly modify / extend: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemSelectionConverter.cs`
- Possibly add tests: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs` or `RecipeRowViewModelTests.cs`

- [x] Read `ComboBoxCellFactory.cs` around lines 74-110 and 135 (group editing template — reference shape using binding).
- [x] Reuse `ComboBoxItemSelectionConverter` (decided per plan-review — do NOT add a sibling class). Verify it accepts a list of items via ctor and does `ActionDefinition ↔ int id` mapping. If its current contract only matches the group editing path, extend the same converter to handle the action case.
- [x] Replace the `comboBox.SelectionChanged += lambda` block with `comboBox.Bind(ComboBox.SelectedItemProperty, new Binding(...))` per Technical Details. The binding path is the indexer `[{columnKey}]` (existing convention).
- [x] `dotnet build` green.
- [x] Locate or write a test: when an action is selected in the row's combo column, `row.GetPropertyValue(columnKey)` returns the new action id. The test must NOT touch the actual ComboBox control — it asserts the data flow through `SetPropertyValue` ↔ binding ↔ converter ↔ ComboBox. May be straightforward via `RecipeRowViewModel` directly if `SetPropertyValue` is the binding target.
- [x] `dotnet test` green.
- [x] Commit:
  ```
  fix: remove ComboBox event-handler leak in action column template
  ```
  Body: explain that `SelectionChanged += lambda` captured `row`, never unsubscribed, accumulated orphan ComboBoxes during scroll. Replaced with `SelectedItem` binding to the row indexer using a value converter — Avalonia manages binding lifecycle so no strong-ref cycle survives.

### Task 3: BuildGrid re-entry guard

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/...` — add idempotency test (locate the test class that exercises ColumnBuilder, or create a focused one).

- [x] Add `private bool _columnsBuilt;` field to `MainWindow`.
- [x] Add early return in `BuildGrid()` per Technical Details. Set `_columnsBuilt = true` after a successful build.
- [x] Add XML doc comment on `_columnsBuilt`: "Guards against repeat column rebuild on re-activation. If dynamic column rebuild is ever needed, reset to false at the call site."
- [x] **Add idempotency test for `ColumnBuilder.BuildColumns`** at the existing test seam. The test instantiates the builder with the standard fixture, calls `BuildColumns(grid)` twice in a row, and asserts that the column count after the second call equals the count after the first (no duplication). This is the regression net for the guard's intent. If testing the MainWindow code-behind field directly is awkward, an alternative: add an `internal` method or test seam that exposes the guard. Prefer the ColumnBuilder-level test — it covers the actual concern (column count stability) without coupling to the MainWindow widget.
- [x] `dotnet build` + `dotnet test` green.
- [x] Commit:
  ```
  fix: guard MainWindow.BuildGrid against repeat invocation on re-activation
  ```
  Body: WhenActivated may fire on hide/show or modal interactions; without this guard, ColumnBuilder.BuildColumns re-allocates all columns + templates + converters on every activation, churning gen-0.

### Task 4: Converter allocation hotspots

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/PropertyTimeMultiConverter.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TimeFormatHelper.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` and/or `TextCellFactory.cs` (for StepStartTime direct binding)

- [x] **4a.** In `PropertyTimeMultiConverter.Convert`, replace `values.Any(v => v == AvaloniaProperty.UnsetValue)` with a `for` loop over `values.Count`. If `values[0]` is already `string`, skip `ToString()` — cast directly.
- [x] **4b.** In `TextCellFactory.CreateDisplayTemplate`, when `columnKey == TimeFormatHelper.StepStartTimeColumnKey`, build a plain `new Binding(nameof(RecipeRowViewModel.StepStartTime))` directly to `TextBlock.Text` — bypass `PropertyTimeMultiConverter` entirely on this branch. The routing already special-cases the key in `ResolveBindingPath`; we just stop wrapping it in MultiBinding. `StepStartTime` is pre-formatted by the view model, so no converter is needed on this path.
- [x] (memoization Task 4b dropped from original plan per review — see Technical Details for rationale.)
- [ ] `dotnet build` + `dotnet test` green.
- [ ] Run a quick eyeball test: confirm `StepStartTime` still displays correctly in the grid (the bound property is already pre-formatted).
- [ ] Commit:
  ```
  perf: trim allocation hotspots in PropertyTimeMultiConverter and TimeFormatHelper
  ```

### Task 5: Verify acceptance

- [x] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 309/309 green.
- [x] `git diff master..HEAD --stat` review — scope confined (Round-7 files: 3 csprojs, ComboBoxCellFactory.cs, MainWindow.axaml.cs + ColumnBuilderIdempotencyTests.cs, PropertyTimeMultiConverter.cs, TextCellFactory.cs, plan file; remaining diff is prior migrations baseline on feature/avalonia-12-migration).
- [x] manual scroll smoke (skipped - not automatable in headless)
- [ ] Commit:
  ```
  chore: verify Round-7 acceptance criteria
  ```

### Task 6: Archive plan and document Round-7

**Files:**
- Move: `Docs/plans/20260513-round7-perf-and-bumps.md` → `Docs/plans/completed/`
- Modify: `Docs/07-non-functional.md`

- [x] `git mv Docs/plans/20260513-round7-perf-and-bumps.md Docs/plans/completed/`.
- [x] In `Docs/07-non-functional.md`, add a Round-7 subsection after Round-6 with:
  - Package patch bumps (Avalonia 12.0.2→12.0.3, MEL family to 10.0.8, etc.).
  - ComboBox action-column event-handler strong-ref leak fix (`SelectionChanged += lambda` → `SelectedItem` binding).
  - BuildGrid re-entry guard against re-activation churn.
  - Converter allocation hotspots flattened (LINQ `.Any()` → for-loop; StepStartTime bound directly without MultiBinding wrap).
  - Round-8 follow-up note: `supportsRecycling: true` requires rewriting closures over `row` in cell templates to pure bindings; XAML-based data templates with compiled bindings is a separate effort.
- [x] Commit:
  ```
  docs: archive Round-7 plan and document scroll perf fixes
  ```

## Post-Completion

**Manual verification (required before PR open):**
- Launch the app against a real recipe with ≥100 steps. Scroll continuously for ≥30 seconds in both directions. Memory should plateau, not grow linearly. Gen-0 collections should be less frequent. Document the observation in PR description.
- Confirm action ComboBox selection still works in the recipe grid — the Task 2 rewrite is the main behavioural risk.

**External system updates:**
- None. Internal performance work + dep bumps.

**Round-8 deferred work (recorded for follow-up PR):**
- Rewrite ComboBox/editing cell templates to eliminate closures over `row`; enable `supportsRecycling: true` on all of them. This is what eliminates the structural per-scroll allocation, not just the strong-ref leak.
- Move cell templates to XAML `<DataTemplate>` with `x:DataType="vm:RecipeRowViewModel"` and `x:CompileBindings="True"` to enable compiled bindings (currently all code-built `new Binding(...)` paths are reflection-based).
- These two together address the remaining gen-0 churn after Round-7 lands.
