# Cell-state modernization, safe recycling, and conditional XAML migration (Round-10)

> Filename retained for git continuity. Scope evolved from "full XAML migration" to "canonical Classes-binding pattern + recycling-first perf experiment + conditional XAML for the only template where it pays off". See Overview for the redesign rationale.

## Overview

Round-10 originally proposed full XAML cell-templates migration with compiled bindings. Plan-review (2026-05-15) found 3 critical design flaws:
1. **Approach (A) for group columns** (clone XAML shape, substitute paths) is not implementable in Avalonia 12 — a compiled `DataTemplate` cannot have its bindings rewritten post-XAML-compile.
2. **Approach (B) for text cells** leaves them at the same allocation profile — compiled-binding win on text cells reduces to "marginal cleanup"; the real perf gain comes from `supportsRecycling: true`, not compiled bindings.
3. **CellPresenter removal in the same PR** as XAML migration is high-risk visual regression with no perf payoff — CellPresenter is small, cheap, and not the perf bottleneck.

Subsequent research (subagent against `AvaloniaUI/avalonia-docs` and `Avalonia.Controls.DataGrid` 12.0.0 source) confirmed the **canonical Avalonia 12 pattern** for VM-driven cell visual state:
- `Classes.foo="{Binding ...}"` on the cell content root (Classes is `public`, mutable, bindable from XAML).
- `<Style Selector="DataGridCell.foo">` (or `ComboBox.foo`, `TextBlock.foo`) selectors in `DataGrid.Styles`.
- Avalonia docs (`docs/styling/pseudoclasses.md`, `docs/data-binding/binding-classes.md`) explicitly recommend Classes for data-driven external state and reserve `PseudoClasses` (which is `protected`) for intrinsic control state.

**The current `CellPresenter` exists only because `PseudoClasses` is `protected` — it's an anti-pattern fix to a problem that disappears with the documented Classes pattern.** Three styled-properties + three reflection bindings + handlers, all to access `PseudoClasses.Set` which only the control itself can call. Documented canonical replacement: bind `Classes` directly from XAML. No custom control needed.

Round-10 thus splits into three independent scopes, sequenced with a decision gate:

### Scope A — Recycling-first experiment (mandatory)

Test the hypothesis that Round-9's equal-value guard in `RecipeRowViewModel.SetPropertyValue` is sufficient to make `supportsRecycling: true` safe (the guard no-ops phantom writebacks at the row VM level, breaking the cascade Round-8 hit at the binding level). If yes, the perf goal may already be met by simply flipping recycling — no further work needed for the visible symptoms.

### Scope B — Canonical cell-state pattern (mandatory; orthogonal to Scope A)

Modernize the legacy `CellPresenter` wrapper to the canonical Avalonia Classes-binding pattern. Delete the wrapper. Simplify the cell-state data model: editability becomes a property of the cell **template**, not a runtime state flag; applicability becomes a single boolean. Delete 4 redundant converters (CellStateConverter, CellStateToBoolConverter, HitTestVisibleMultiConverter, plus eventual ContentControl wrapping). This applies the project's stated modernization goal regardless of perf outcomes.

### Scope C — XAML compiled bindings (conditional)

Executed **only if Scope A doesn't hit the perf target**. Migrate the action ComboBox cell to XAML compiled binding — the single template where the dynamic-columns constraint doesn't apply (action column key is the static literal `"action"`). Group and text cells stay in imperative templates (no compiled-binding win there given dynamic column keys, per plan-review).

### Goals (must-do)

- Eliminate visible GC pressure and UI freezes during scroll/row-add. Target: gen-0/sec drops ≥50% during 30s scroll vs Round-9 baseline; working set stable.
- Replace the `CellPresenter` anti-pattern with canonical `Classes`-binding pattern. Delete the wrapper.
- Simplify cell-state model: editability is a property of the cell template, applicability is a data-driven boolean.
- Reduce converter zoo (delete `CellStateConverter`, `CellStateToBoolConverter`, `HitTestVisibleMultiConverter`).
- Tests stay green at every task boundary (~402 baseline).
- Round-8 / Round-9 invariants preserved: ComboBox in `CellTemplate` + `IsReadOnly=true` (Avalonia DataGrid 12.0.0 issue #236), single-click opens dropdown, Round-9 equal-value guard intact.

### Behavior diff (user-visible) — Scope B

Two intentional UX changes follow from the cell-state simplification:

1. **`step_start_time` column** — currently rendered with light-grey background (`:cell-readonly`). After Scope B: normal background, text visible. It looks like normal data (because it IS data — just computed and uneditable). Editability removed via template choice (TextBlock with no editing affordance), not via a data-state flag.

2. **Not-applicable cells** (column doesn't apply to row's action — e.g., `temperature_target` in an `OpenValve` step) — currently dark-grey background with content hidden via `:cell-disabled > Child { IsVisible = false }`. After Scope B: greyed-out ComboBox/TextBlock **visible** on greyed background, `IsHitTestVisible = false`. Modern UX paradigm (Fluent / Material both prefer this over hidden) — informative ("column exists, doesn't apply here") rather than confusing-empty.

All other behavior preserved: selection wiring, copy-paste, undo/redo, PLC sync read-only, action change cascade, equal-value guard.

**Rollback path for the two UX diffs:** explicit decision is **"revert the Scope B commit if the user dislikes the new look"**. No in-product toggle. The old look is preserved in git history (HEAD before Phase 2 starts). If only one of the two diffs is contested, revert and re-apply the other via cherry-pick. This is documented here so the decision isn't litigated post-merge.

This refactor continues on branch `refactor/cell-templates-to-xaml` (forked from `refactor/recipe-stack-simplification`).

## Context (from discovery + canonical research)

Files in scope (~15):

**Cell-state surface (refactor or delete):**
- `SemiStep/SemiStep.UI/RecipeGrid/CellPresenter.cs` (~90 LOC) — **delete**; canonical pattern doesn't need a wrapper.
- `SemiStep/SemiStep.UI/RecipeGrid/CellStateConverter.cs` — **delete**; replaced by direct boolean property on row VM.
- `SemiStep/SemiStep.UI/RecipeGrid/CellStateToBoolConverter.cs` — **delete**; redundant with direct boolean.
- `SemiStep/SemiStep.UI/RecipeGrid/HitTestVisibleMultiConverter.cs` — **delete**; replaced by style trigger or single-binding negation.

**Cell-state data model:**
- `SemiStep/SemiStep.UI/RecipeGrid/CellState.cs` (or wherever the enum lives) — **simplify** to `bool IsApplicable` semantics, or replace with that boolean directly on the row VM.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — `CellStates: Dictionary<string, CellState>` → `IsApplicable(string columnKey)` accessor or `InapplicableColumns: ISet<string>`. Editability handled by ColumnBuilder via template choice.
- `SemiStep.Core/Recipes/RecipeQueryService.GetCellState` (or whatever Core method returns CellState) — review return type / refactor.

**Cell templates (where Classes binding lives):**
- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` — modify templates to bind `Classes.disabled` on the ComboBox.
- `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` — same for TextBlock / TextBox.
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — uses `DataGridColumn.CellStyleClasses` for column-level classes (if useful for column-static styling); template choice for readonly columns (TextBlock vs TextBox) per column type.

**Styles:**
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — rewrite all 11 `controls|CellPresenter:*` selectors to `DataGridCell.disabled`, `DataGridCell.current-step`, etc. or to inner-content selectors as appropriate.

**Row-level state:**
- `RecipeRowViewModel.IsCurrentStep / IsPastStep` — already row-level. Migrate driving mechanism: `DataGrid.LoadingRow` event handler in code-behind sets `e.Row.Classes`. Or attached behavior. The current CellPresenter binds these per-cell-per-row which is wasteful — they're constant across cells of a row.

**XAML host (Scope C only):**
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — only if Scope C is executed: add Window.Resources reference.
- New file (Scope C only): `SemiStep/SemiStep.UI/RecipeGrid/CellTemplates.axaml` — ActionCellTemplate as XAML `<DataTemplate x:DataType="vm:RecipeRowViewModel" x:CompileBindings="True">`.

**Tests:**
- Existing `ColumnBuilderIdempotencyTests`, `RecipeRowViewModelTests` — adjust to new shape.
- Converters' tests (`CellStateToBoolConverterTests`, `HitTestVisibleMultiConverterTests`) — already deleted in Round-9 fixup as those converters are scheduled for deletion.

**Constraints:**
- Avalonia 12 DataGrid quirk preserved (issue #236): ComboBox in `CellTemplate` + column `IsReadOnly = true`. Never use `CellEditingTemplate`.
- Tests use `[AvaloniaFact]` from headless XUnit. Headless cannot simulate hit-testing — manual UI smoke is pre-PR gate.
- Avalonia 12 docs confirmed: `<DataGridRow Classes.foo="{Binding ...}">` is NOT directly usable (rows are generated internally by DataGrid, not authored in XAML). Use `DataGrid.LoadingRow` event in code-behind to set `e.Row.Classes`.

## Development Approach

- Testing approach: **Regular**. UI refactors validated by existing test coverage + manual smoke.
- Atomic commits per task. Each task is a self-contained green commit.
- **Scope A then Scope B then (conditional) Scope C.** Decision gate between A and B is the perf measurement. Scope B happens regardless (it's the modernization goal, independent of perf).
- After Scope A perf measurement, document whether Scope C is required (write the result into this plan with `⚠️` if perf goal missed).

## Testing Strategy

- Unit tests at task boundaries: `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes` green.
- Existing 402-baseline tests catch binding-equivalence regressions.
- **Baseline measurement (Task 1) BEFORE Phase 1 starts.** Document gen-0/sec and working set during 30s scroll on Round-9 HEAD using `dotnet-counters monitor -p <pid> System.Runtime`. Store in `Docs/plans/work/round-10-baseline.md` (gitignored or committed — your call).
- **Manual UI smoke (mandatory before PR open):**
  1. Open 100+ row recipe. Click action ComboBox → dropdown opens on FIRST click.
  2. Click group ComboBox → dropdown opens on first click.
  3. Edit a text cell → value commits, no signal storm in logs.
  4. Change an action → row rebuilds without phantom mutations (verify via Round-9's "Mutation entry" log).
  5. Copy-paste 50 rows → selection accurately tracks pasted range.
  6. Load small recipe over large → no crash, grid replaced cleanly.
  7. PLC sync start/stop → grid read-only state toggles cleanly.
  8. **Continuous scroll 30s** → gen-0/sec drops ≥50% vs baseline. Working set plateaus. *Pass criterion for Scope A.*
  9. **Add 100 rows in batch** → no perceptible UI freeze.
  10. Undo/redo preserves grid + selection state.
  11. **step_start_time column** renders with normal background + visible time text (Scope B behavior diff #1).
  12. **Not-applicable cells** show greyed-out ComboBox/TextBlock on greyed background (Scope B behavior diff #2). Click is no-op.
  13. **Current step row** + **past step row** highlighting works (LoadingRow-driven Classes).

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

### Before (Round-9 state)

```
Cell visual:
  DataGridCell (Avalonia built-in)
  └── CellPresenter : ContentControl (custom, Focusable=false)
       ├── 3 StyledProperty<CellState/bool>
       ├── 3 reflection Bindings (CellStates, IsCurrentStep, IsPastStep)
       ├── 5 PseudoClasses driven by On*Changed handlers
       └── Content: ComboBox / TextBlock / TextBox
            ├── More reflection Bindings (ItemsSource, SelectedItem TwoWay, …)
            └── HitTestVisible via MultiBinding + 2 converters

Styles:
  controls|CellPresenter:cell-enabled  { Background = ... }
  controls|CellPresenter:cell-readonly { Background = ... }
  controls|CellPresenter:cell-disabled { Background = ... }
  controls|CellPresenter:cell-disabled > ComboBox  { IsVisible = false }
  controls|CellPresenter:cell-disabled > TextBlock { IsVisible = false }
  controls|CellPresenter:step-current  { Background = ... }
  controls|CellPresenter:step-past     { Background = ... }
  DataGridRow:selected controls|CellPresenter:cell-enabled  { ... }
  DataGridRow:selected controls|CellPresenter:cell-readonly { ... }
  DataGridRow:selected controls|CellPresenter:cell-disabled { ... }
```

### After Scope B

```
Cell visual:
  DataGridCell (Avalonia built-in, recycled)
  └── Content: ComboBox / TextBlock (no wrapper)
       ├── Classes.disabled="{Binding InapplicableColumns, Converter=ContainsKeyConverter, ConverterParameter=<columnKey>}"
       │   OR row VM exposes static-shape boolean property per column
       └── HitTestVisible follows from style: <Style Selector="DataGridCell.disabled ComboBox"> { IsHitTestVisible = false }

Styles (canonical):
  DataGridCell.disabled   { Background = ... }
  DataGridCell.disabled ComboBox  { IsHitTestVisible = false, Opacity = 0.5 }
  DataGridCell.disabled TextBlock { IsHitTestVisible = false, Opacity = 0.5 }
  DataGridCell.current-step  { Background = ... }   (set by LoadingRow)
  DataGridCell.past-step     { Background = ... }   (set by LoadingRow)
  DataGridRow:selected DataGridCell.disabled  { ... }
  …
```

### Why correctness is preserved with recycling

Round-9's equal-value guard in `RecipeRowViewModel.SetPropertyValue` (line ~109): for both `action` column writes AND non-action property writes, the setter checks if the new value matches current; if yes, returns without firing `ActionChanged` / `PropertyValueChanged`. This guard catches phantom writebacks AT THE ROW VM, independent of binding pattern. So even if a reflection-based TwoWay binding fires a spurious source-write during DataContext swap on a recycled cell, the row VM no-ops, the mutation cascade does not start.

Scope A is the empirical test of this hypothesis. If it works, recycling is safe with the current reflection-binding stack; we don't need compiled bindings for correctness, only (potentially) for additional perf.

### Avalonia 12 ComboBox-in-DataGrid pattern preserved

Round-8 issue #236 still applies. ComboBox lives in `CellTemplate` + column `IsReadOnly="True"`. Single click opens dropdown. Never use `CellEditingTemplate`.

## Technical Details

### Phase 0: Pre-implementation audit + baseline measurement (2 tasks)

#### Task 1: pre-implementation audit (verify unstated assumptions)

**Files:**
- Read only: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` (action + group ConvertBack paths)
- Read only: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemSelectionConverter.cs`
- Read only: `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs`
- Read only: `SemiStep.Tests/YamlConfigs/**/columns.yaml` (production-shaped configs)
- Read only: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs:109-134` (equal-value guard)

This task answers three questions whose negative answers would invalidate Scope A or Scope B. Document findings inline in this task before moving on.

- [ ] **A1 (Scope A correctness):** What does `ComboBoxItemSelectionConverter.ConvertBack` return when the input is `null` or a non-`ComboBoxItemViewModel`? If `null` or empty string, can it reach `RecipeRowViewModel.SetPropertyValue` with that value while `currentValue` is a real string? Trace the action path AND the group path. If the answer is "yes, null/empty can pass through while currentValue is real" → the equal-value guard does NOT fully protect against phantom writebacks on recycled-cell DataContext swap. Plan response: either harden the guard (treat null-or-empty input from a converter as "skip" instead of "write"), OR add a recycle-suppression flag set by `LoadingRow` that the row VM checks.
- [ ] **A2 (Scope B readonly audit):** Search YAML configs for `readonly: true` on columns OTHER than `step_start_time`. If found, the Scope B claim "Readonly is for computed columns like step_start_time" is wrong — those other readonly columns would also lose their light-grey background after the cell-state collapse. Document any such columns. Update Scope B behavior diff #1 to include them OR keep their visual via a separate column-template-choice.
- [ ] **A3 (Scope B Avalonia code-built Classes binding):** Avalonia 12 does NOT have `StyledProperty<Classes>` for `BindingBase`-style binding to `control.Classes`. Verify the idiomatic code-built pattern: is it (a) `control.Classes.Set(name, bool)` driven by an explicit `INotifyPropertyChanged` subscription with cleanup on `DetachedFromVisualTree`, (b) an Avalonia helper that wraps subscribe+cleanup automatically, or (c) something else? Read Avalonia source or community sample. If the answer is (a), Scope B Task 6 has a lifecycle-management cost comparable to `CellPresenter`'s — plan-review explicitly flagged this. Document the chosen pattern and add a cleanup-on-detach subtask to Task 6 if needed.

Document A1, A2, A3 results IN THIS TASK before marking complete. If A1 confirms the guard gap → Scope A needs a harden-guard subtask before Task 2. If A2 finds non-step_start_time readonly columns → expand Scope B behavior diff or split readonly handling. If A3 requires per-cell subscribe-cleanup → add explicit lifecycle subtask to Task 6.

#### Task 2: capture baseline metrics on Round-9 HEAD

**Files:**
- Create (optional): `Docs/plans/work/round-10-baseline.md` (or paste numbers into Phase 4 verify)

- [ ] On Round-9 HEAD (currently this branch but pre-Phase-1), launch `SemiStep.UI` in **Release** config (`dotnet build -c Release`). Note: csproj uses Server GC if `<ServerGarbageCollection>true</ServerGarbageCollection>` is set — record GC mode for fair comparison.
- [ ] Warm-up: scroll for 5s to stabilize JIT/cache.
- [ ] Run `dotnet-counters monitor -p <pid> System.Runtime` while:
  - (B1) Continuously scrolling a 100+ row recipe for 30s. Record gen-0/sec, gen-1/sec, working set MB at the end.
  - (B2) Adding 50 rows in batch (e.g., via paste). Record peak working set, time-to-stable.
- [ ] Document numbers. This is the baseline Scope A must beat by 50% on gen-0/sec (Task 4 thresholds).

### Phase 1: Scope A — Recycling experiment (3 tasks)

#### Task 3: flip `supportsRecycling: true` on all factory templates

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` (`CreateActionCellTemplate`, `CreateGroupCellTemplate`)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (`CreateStepStartTimeTemplate`, `CreateMultiBindingTemplate`)
- Modify (conditional on Task 1.A1 finding): `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — harden equal-value guard against null/empty writeback from converter (if A1 confirmed gap)

- [ ] If Task 1.A1 confirmed a guard gap: extend `SetPropertyValue` to treat null/empty incoming value as no-op when current value is real (i.e., don't fire `PropertyValueChanged`/`ActionChanged` on a null write over a real value). Add a test pinning this behavior.
- [ ] Set `supportsRecycling: true` in all 4 cell template factories.
- [ ] Run `dotnet build` + `dotnet test` — must be green.

#### Task 4: log-based verification — no phantom ActionChanged on scroll

**Files:**
- No code change. Use Round-9's `[Information] "Mutation entry"` log lines.

- [ ] Launch SemiStep.UI with Serilog Information level visible.
- [ ] Open 100+ row recipe. Scroll continuously for 30s. Then add 50 rows via paste. Then change one action.
- [ ] Inspect log: **the only** `Mutation entry: …` lines should be the legitimate user-initiated paste and action-change. **Zero** unsolicited `Mutation entry: ChangeStepAction` or `Mutation entry: UpdateStepProperty` lines during scroll.
- [ ] If phantom mutations appear, document them in this plan as `⚠️`. Phase 1 may need a more aggressive guard (extending Task 3).

#### Task 5: re-measure post-flip; decision gate

**Files:**
- Update this plan with measurement results.

- [ ] Repeat Task 2 scenarios B1 + B2 with `supportsRecycling: true` enabled.
- [ ] Compare to baseline. **Numeric decision gate:**
  - **Pass** (gen-0/sec drop ≥50%, working set stable within ±5%, no perceptible UI freezes during scroll): Scope A is sufficient. Scope C is **skipped**.
  - **Partial** (gen-0/sec drop between 25% and 50%, OR working set drift up to ±15%): Scope C **is required**. Document the numbers in this plan as `⚠️`. Continue to Scope B; execute Scope C at end.
  - **Fail** (gen-0/sec drop <25% OR working set still climbing during scroll): something else is the bottleneck. **Halt the round.** Re-investigate (likely a memory leak in row-VM event subscriptions, or non-recycling-related allocation hot path). Do NOT proceed to Scope B until root cause is identified — proceeding might mask the underlying problem.
- [ ] Commit measurement results into the plan (or `Docs/plans/work/round-10-baseline.md`).

### Phase 2: Scope B — Canonical cell-state pattern (7 tasks)

#### Task 6: simplify cell-state model — Editability via template choice, Applicability via boolean

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` (replace `CellStates: Dictionary<string, CellState>` with `IReadOnlySet<string> InapplicableColumns` or `bool IsApplicable(string columnKey)`)
- Modify: `SemiStep.Core/Recipes/Helpers/CellStateResolver.cs` (return type changes to bool or stays enum-but-mapped externally)
- Possibly modify: `CellState.cs` (enum may be deletable if no consumer needs the Readonly distinction; or kept internal to Core and mapped to bool at UI boundary)

**Invariant check (added per plan-review):**
- [ ] Confirm `RecipeRowViewModel.SetPropertyValue` (lines 109-134, the Round-9 equal-value guard) is **unchanged** by this task. Diff against Round-9 HEAD before commit. The guard's correctness is independent of the CellStates → IsApplicable refactor.

- [ ] Use Task 1.A2 findings: if non-`step_start_time` readonly columns exist in production YAML, **do not** collapse their visual treatment into the not-applicable greyed look; preserve their light-grey background via a separate column-template-choice (e.g., a third template kind `ReadonlyValueTemplate` that has the existing light-grey style baked in).
- [ ] If A2 found ONLY `step_start_time` as readonly: collapse readonly → "use TextBlock template, normal background" as planned in behavior diff #1.
- [ ] Refactor row VM: expose `InapplicableColumns` (or equivalent) — the only data-driven cell state.
- [ ] Editability becomes column-declaration-time, not row-data-time: ColumnBuilder for readonly columns uses TextBlock template (no editing affordance); editable columns use TextBox/ComboBox templates.
- [ ] Update existing tests pinning `CellStates[key]` enum values: rewrite to assert against the new boolean shape.
- [ ] Run `dotnet build` + `dotnet test` — green.

#### Task 7: bind `Classes.disabled` on cell content roots

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` (cell templates set `comboBox.Classes` per binding)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (text templates same)
- Possibly create: `SemiStep/SemiStep.UI/RecipeGrid/CellClassBinder.cs` (helper that encapsulates subscribe + cleanup on `DetachedFromVisualTree`, if A3 found this pattern is needed)

**Use Task 1.A3 finding to commit to a pattern:**
- [ ] **If A3 confirmed `Classes.Set + subscribe + cleanup-on-detach`** is the canonical pattern: implement a small `CellClassBinder` helper that takes the cell control, a property-change observable, and a class name; subscribes on attach; unsubscribes on detach. Apply in each cell template factory. Per-cell allocation is one helper + one subscription + cleanup hook — meaningfully less than `CellPresenter`'s ContentControl + 3 styled-properties + 3 reflection-bindings.
- [ ] **If A3 found an Avalonia helper API** that wraps subscribe+cleanup (e.g., a `ClassesBindingExtensions`): use it directly without writing the helper.
- [ ] **If A3 found neither pattern works cleanly from code-built `FuncDataTemplate`**: STOP. Document the gap in this plan as `⚠️`. Consider deferring Scope B's CellPresenter deletion to a later round where XAML templates carry the Classes binding declaratively. Tasks 9-10 don't run; tasks 11, 14, 15 still run for Scope A's perf gain.

If pattern is implementable:
- [ ] For each cell template, add `Classes.disabled` binding tied to `row.IsApplicable(columnKey)` (or `InapplicableColumns.Contains(columnKey)`).
- [ ] Verify no handler leak: cycle 100 rows in viewport (scroll), confirm via `dotMemory` snapshot or `GC.GetTotalAllocatedBytes` delta that subscriptions are released on cell detach.
- [ ] Optionally use `DataGridColumn.CellStyleClasses` for column-level static classes (e.g., column type) and bind row-data classes per template.
- [ ] Run `dotnet build` + `dotnet test` — green.

#### Task 8: rewrite `DataGridStyles.axaml` — DataGridCell selectors replace CellPresenter selectors

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [ ] Rewrite all 11 `controls|CellPresenter:*` selectors to canonical equivalents:
  - `controls|CellPresenter:cell-enabled` → no selector needed (default DataGridCell appearance).
  - `controls|CellPresenter:cell-readonly` → deleted (state no longer exists; readonly is template-choice).
  - `controls|CellPresenter:cell-disabled` → `DataGridCell.disabled` (Class, not pseudo-class).
  - `controls|CellPresenter:cell-disabled > ComboBox/TextBlock { IsVisible = false }` → `DataGridCell.disabled ComboBox / TextBlock { IsHitTestVisible = false, Opacity = 0.5 }` (Scope B behavior diff #2).
  - `controls|CellPresenter:step-current` → `DataGridCell.current-step` (Task 8 sets the class via LoadingRow).
  - `controls|CellPresenter:step-past` → `DataGridCell.past-step` (same).
  - `DataGridRow:selected controls|CellPresenter:cell-*` → `DataGridRow:selected DataGridCell.*` (descendant chain).
- [ ] Run `dotnet build` — XAML compile must succeed.

#### Task 9: row-level state via `DataGrid.LoadingRow` code-behind

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (add `LoadingRow` event handler)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` (handler implementation)
- Modify: `RecipeRowViewModel` — already has IsCurrentStep / IsPastStep, no model change

- [ ] In MainWindow.axaml: `<DataGrid LoadingRow="OnDataGridLoadingRow" />`.
- [ ] In code-behind: handle `LoadingRow` event. For each row, subscribe to `row.PropertyChanged(IsCurrentStep / IsPastStep)` and toggle `e.Row.Classes.Add/Remove("current-step" / "past-step")` accordingly. Also handle `UnloadingRow` to unsubscribe.
- [ ] Verify visual: scroll, run a recipe (or simulate via test instrumentation), confirm current/past step highlighting works.
- [ ] Run `dotnet build` + `dotnet test`.

#### Task 10: delete `CellPresenter.cs`

**Files:**
- Delete: `SemiStep/SemiStep.UI/RecipeGrid/CellPresenter.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` (drop `CellPresenter.Wrap(...)` — return content directly)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (same)
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` (drop the `xmlns:controls="clr-namespace:..."` declaration if unused after Task 7)

- [ ] Audit `CellPresenter.Wrap` call sites — drop them; templates return content directly.
- [ ] Delete the file.
- [ ] Run `dotnet build` + `dotnet test` — green.

#### Task 11: delete redundant converters

**Files:**
- Delete: `SemiStep/SemiStep.UI/RecipeGrid/CellStateConverter.cs` (no consumers after Tasks 5-7)
- Delete: `SemiStep/SemiStep.UI/RecipeGrid/CellStateToBoolConverter.cs` (no consumers after Tasks 5-7)
- Delete: `SemiStep/SemiStep.UI/RecipeGrid/HitTestVisibleMultiConverter.cs` (no consumers after Task 7's style trigger)
- Delete: any associated test files (audit)
- Modify: DI registration if any of these are registered

- [ ] Grep audit each converter. Delete if zero references.
- [ ] Delete files.
- [ ] Update DI in `UiDi.cs` if applicable.
- [ ] Run `dotnet build` + `dotnet test` — green.

#### Task 12: update tests for new shape

**Files:**
- Modify: `SemiStep.Tests/UI/RecipeRowViewModelTests.cs` (CellStates assertions → InapplicableColumns assertions)
- Modify: `SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs` if needed
- Delete: any tests pinning deleted converter shapes

**Invariant check (added per plan-review):**
- [ ] Confirm `RecipeRowViewModel.SetPropertyValue` (lines 109-134, the Round-9 equal-value guard) is **unchanged** by Phase 2. Diff against Round-9 HEAD before commit. If Task 3 hardened the guard against null/empty inputs, the new behaviour is intentional — note in the diff review.

- [ ] Replace `CellStates[key]` enum assertions with the new boolean shape.
- [ ] Adjust any `CellStateResolver.GetCellState` test that pinned the enum.
- [ ] Run `dotnet build` + `dotnet test` — full suite green.

### Phase 3: Scope C — XAML compiled binding for action ComboBox (conditional, 2 tasks)

⚠️ **Execute only if Phase 1 Task 4 decision gate determined perf goal missed.**

#### Task 13: create `CellTemplates.axaml` with `ActionCellTemplate`

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/CellTemplates.axaml`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (add Window.Resources reference)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` (expose `ActionItems` static property if not already + `SelectedActionItem` TwoWay routing through `SetPropertyValue("action", ...)` to preserve Round-9 single-mutation funnel)

- [ ] Create `<ResourceDictionary x:CompileBindings="True">` with `<DataTemplate x:Key="ActionCellTemplate" x:DataType="vm:RecipeRowViewModel" x:CompileBindings="True">` containing a `<ComboBox>` with compiled bindings to `ActionItems` and `SelectedActionItem`.
- [ ] Expose `RecipeRowViewModel.ActionItems` and `SelectedActionItem`. Setter on `SelectedActionItem` routes through `SetPropertyValue("action", value?.Id.ToString())` — preserves Round-9 invariant per plan-review feedback.
- [ ] Add `Classes.disabled` binding to the ComboBox same as Scope B.
- [ ] Reference template from `ColumnBuilder.CreateActionColumn`: `column.CellTemplate = (IDataTemplate)Application.Current!.FindResource("ActionCellTemplate")!`.
- [ ] Verify the imperative `CreateActionCellTemplate` is no longer called — delete it.
- [ ] Run `dotnet build` + `dotnet test` — green.

#### Task 14: re-measure perf

**Files:**
- Update this plan with results.

- [ ] Repeat Task 1 scenarios. Compare to baseline AND to Phase 1 measurement.
- [ ] If hit perf goal — done. If not — investigate further (likely outside this plan's scope).

### Phase 4: Verify + archive (2 tasks)

#### Task 15: full validation + manual smoke + perf comparison

- [ ] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — all green.
- [ ] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` — clean.
- [ ] `git diff master..HEAD --stat` review — scope confined.
- [ ] Manual UI smoke per Testing Strategy (13 scenarios). Document any deviation as `⚠️`.

#### Task 16: archive plan + Round-10 docs

**Files:**
- Move: `Docs/plans/20260515-cell-templates-to-xaml.md` → `Docs/plans/completed/`
- Modify: `Docs/07-non-functional.md` — Round-10 subsection

- [ ] `git mv` plan to completed/.
- [ ] Append Round-10 subsection covering:
  - Recycling experiment outcome (Scope A perf delta).
  - Canonical cell-state pattern (Scope B): CellPresenter deleted, 3 enum states → 1 boolean (applicability), editability via template choice, 4 converters deleted, total LOC -~250.
  - DataGridStyles.axaml rewritten to canonical Avalonia 12 patterns (Classes + DataGridCell selectors).
  - Row-level state migrated to `LoadingRow`-driven `DataGridRow.Classes`.
  - Behavior diff for users: step_start_time normal-looking; not-applicable cells show greyed-out controls.
  - Scope C outcome (executed or skipped, perf reason).
  - Round-11 deferred work (none planned).

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all in-repo code/tests/styles/docs work.
- **Post-Completion** (no checkboxes): manual UI smoke including perf comparison.

## Post-Completion

**Manual verification** (required before PR open):
- 13-scenario smoke listed in Testing Strategy. Run against a real recipe with ≥100 steps.
- Scenario 8 perf comparison against baseline (gen-0/sec ≥50% drop). If missed and Scope C wasn't executed, log it in the plan and decide whether to escalate.

**External system updates:**
- None. Internal refactor only.
