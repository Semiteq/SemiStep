# Restore per-row ItemsSource binding for group ComboBox cells

## Overview

Group ComboBox cells (`column_type: action_target_combo_box`) in the recipe grid have a
stale `ItemsSource` after the row's action changes. The current code in
`SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs:75-99` assigns
`comboBox.ItemsSource = items;` **imperatively** inside a `FuncDataTemplate`
lambda that runs only at first cell materialisation. With
`supportsRecycling: true`, Avalonia recycles the visual onto new rows by swapping
`DataContext`; bindings re-evaluate but the imperatively-set property does not.

The fix: replace the imperative assignment with a real Avalonia `Binding` rooted
at a per-row VM property, and switch the `SelectedItem` binding to a
`MultiBinding` so the value-to-item lookup can react to the items list changing.
This is the canonical Avalonia pattern for row-dependent ComboBox items in a
recycled cell (confirmed by Avalonia community discussions
[#14103](https://github.com/AvaloniaUI/Avalonia/discussions/14103),
[#15144](https://github.com/AvaloniaUI/Avalonia/discussions/15144),
[#7086](https://github.com/AvaloniaUI/Avalonia/discussions/7086)) and is the
exact shape that shipped in this codebase before commit `642f17b`.

## Context (from discovery)

### Bug evidence

- User-confirmed: in MBE (and latent in MOCVD), changing a step's action does
  not refresh the `target`/`channel` ComboBox items. The first row's items leak
  to subsequent rows due to Avalonia cell recycling.
- The current interim mitigation in HEAD is `supportsRecycling: false` on the
  group template only (`ComboBoxCellFactory.cs:98`). User-measured: fast scroll
  of 200 rows triggers visible GC pressure / UI lag. This route is not viable.

### Git history (the path that led here)

- `f44bd91`: original feature with `GroupItemsByColumn` per-row + `MultiBinding`
  + `ComboBoxItemMultiSelectionConverter`. `supportsRecycling: true`. Worked.
- `642f17b` (Round-9 Task 6, 2026-05-14): set `supportsRecycling: false` on
  ComboBox templates as **interim defense** against phantom-mutation seam;
  deleted `GroupItemsByColumn` / `ComboBoxItemMultiSelectionConverter` /
  `ColumnTypes.GroupItemsPath` as **no-longer-needed** infrastructure (closure
  capture works when recycling is off). Commit text:
  *"single-binding ComboBoxItemSelectionConverter with closure-captured items"*.
- `9d8e39b` (Round-10 Task 3, 2026-05-15): flipped `supportsRecycling: true`
  back for perf, leaning on the Round-9 equal-value guard in
  `RecipeRowViewModel.SetPropertyValue` (still in place at
  `RecipeRowViewModel.cs:124,134`) as the structural defense against phantom
  mutations. Did **not** restore `GroupItemsByColumn` / `MultiBinding`. This is
  the regression that surfaces the current bug.

### Why this is safe under the current equal-value guard

The phantom-mutation seam Round-9 worried about was that a TwoWay
`SelectedItem` binding, on `DataContext` swap of a recycled cell, would write
the previous row's selection into the new row VM. Round-9 closed it via two
independent mechanisms:

1. **Primary**: equal-value guard in `RecipeRowViewModel.SetPropertyValue`
   (action path at line 124, property path at lines 134-137) — catches any
   spurious writeback at the VM level regardless of binding shape. Round-9
   plan (`20260514-recipe-stack-simplification.md:13,32`) explicitly calls this
   the *structural mitigation*; the recycling flip is the *interim defense*.
2. **Interim**: `supportsRecycling: false`, removed by Round-10.

Round-10 plan Task 1.A1 (`20260515-cell-templates-to-xaml.md:202-224`)
explicitly analysed phantom writebacks for the **single-value**
`ComboBoxItemSelectionConverter`. The multi-converter's `ConvertBack` returns
`object?[] { selectedItem.Id, BindingOperations.DoNothing }`. The A1 analysis
does not transitively cover this — the `MultiBinding.WriteValueToSource`
pipeline iterates legs and, for each non-`DoNothing` value, dispatches to the
corresponding source's setter. For our case only the first leg (value) ever
writes; the items leg always returns `DoNothing`. So the writeback that
reaches `RecipeRowViewModel.SetPropertyValue` is the same int `Id` shape as
in the single-binding case, and the equal-value guard at line 124 (action
column) / line 134 (property columns) catches identical-value writes.
**Verification in Task 6**: log inspection during scroll must show zero
phantom `Mutation entry: …` lines — this empirically confirms the guard
holds for the multi-converter writeback shape.

### Files involved

- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` — group template
  (modify); action template untouched and continues to use
  `ComboBoxItemSelectionConverter` (which stays).
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` — add per-row dict
  (modify).
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnTypes.cs` — re-add path helper
  (modify).
- `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs` — add
  `GetComboBoxItems` group-name cache (modify).
- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs` —
  resurrect from git (create).
- `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs` — restore four
  `GroupItemsByColumn_*` cases (modify).
- `SemiStep/SemiStep.Tests/UI/ComboBoxItemMultiSelectionConverterTests.cs` —
  restore from git (create).
- `SemiStep/SemiStep.Tests/UI/GroupComboBoxRecyclingTests.cs` — **new**
  behavioural test for the actual bug (create).

> csproj files are **not** modified: `SemiStep.UI.csproj` and
> `SemiStep.Tests.csproj` are SDK-style with implicit globbing
> (verified — only `<Compile Update>` entries exist, for axaml code-behind).
> New `.cs` files in `RecipeGrid/` and `Tests/UI/` are picked up automatically.

### Alternatives considered and rejected

| Option | Why rejected |
|---|---|
| `supportsRecycling: false` on group only | Measured: 200-row fast scroll has visible GC pressure / lag. |
| `DataContextChanged` hook in template | Per-recycle allocation of a fresh `IValueConverter` + re-bind of `SelectedItemProperty`. ~40 recycles/sec on fast scroll → defeats Task 3's perf goal. Also event-leak risk. |
| `Binding(".")` on `ItemsSourceProperty` with a `(row, columnKey) → items` converter | `Binding(".")` re-fires on every `Item[]` PropertyChanged (any cell edit in the row), causing ComboBox to reset selection. Real latent bug. |
| Attached behaviour | Same allocation profile as `DataContextChanged`; introduces a UI pattern not used elsewhere. |
| `ConverterParameter` on single-binding SelectedItem (review suggestion) | `ConverterParameter` in Avalonia 12 is a static `object?` — it is not a bindable property and cannot follow `DataContext` changes. Setting it once at template build time has the same staleness problem the bug already exhibits. Verified against `Avalonia.Data.Binding` source (the property is plain `object? ConverterParameter { get; set; }` with no binding pipeline). |
| `SelectedValueBinding` + `SelectedValue` instead of converter (review suggestion) | Saves the converter class (~50 lines + tests) at the cost of replacing a battle-tested pattern with one carrying open questions: (a) `SelectedValueBinding` is set imperatively on the `ComboBox` instance — when the cell is recycled, does this single instance survive across rows correctly? Avalonia 12 docs do not explicitly cover the recycling case. (b) Avalonia issue [#14764](https://github.com/AvaloniaUI/Avalonia/issues/14764) reports `SelectedValueBinding` mis-handling array `ItemsSource`; while we use lists, the bug indicates the path is less mature than `MultiBinding`. (c) **The same per-row VM dictionary is still required** for `ItemsSource` — the alleged VM-touching saving is illusory. Net: ~50 lines saved against unproven recycling behaviour. Not worth it for this fix; can be revisited as a follow-up cleanup if `MultiBinding` proves over-built later. |

## Development Approach

- **Testing approach**: regression-restore from git baseline. The deleted test
  classes describe the exact behaviour we are bringing back; we restore the
  production code and tests **as a pair per task** (not "test-first" in the
  strict TDD sense, since the contract is already specified by the historical
  tests). Each task ends with green tests before the next begins. The one piece
  of NEW test surface — covering the actual bug (ItemsSource refreshes on
  action change) — is added explicitly in Task 5 since the historical suite
  did not cover this scroll-recycling case.
- Complete each task fully before moving to the next.
- After every change: `dotnet build SemiStep/SemiStep.slnx` then
  `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`. Both must be
  green before the next task starts.
- The plan changes shape only in response to a build/test failure or a
  discovered constraint. Append discoveries with ➕ prefix; mark blockers ⚠️.

## Testing Strategy

- **Unit tests**:
  - `ComboBoxItemMultiSelectionConverterTests` — restore from
    `git show 642f17b^:SemiStep/SemiStep.Tests/UI/ComboBoxItemMultiSelectionConverterTests.cs`.
    Covers `Convert` happy path, null/UnsetValue legs, type mismatch, and
    `ConvertBack` value passthrough + `DoNothing` on the items leg.
  - `RecipeRowViewModelTests.GroupItemsByColumn_*` — restore from
    `git show 642f17b^:SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`,
    keeping the four cases that assert the dict contents (column → items list)
    given an action with group properties. The `GetGroupNameForColumn_*` cases
    added in `642f17b` stay — both APIs coexist.
- **Manual smoke** (Task 6): scroll 200-row MBE recipe, verify (a) target
  ComboBox follows the action selection, (b) no perceptible scroll lag, (c) no
  `Mutation entry: …` lines logged during pure scroll (phantom-mutation check).

## Progress Tracking

- Mark `[x]` immediately on task completion.
- ➕ for newly discovered subtasks.
- ⚠️ for blockers.
- If the test contract changes mid-task, update this file before continuing.

## Solution Overview

Replace the imperative ItemsSource assignment with the standard Avalonia
binding-driven pattern. Three layers:

1. **VM**: `RecipeRowViewModel` exposes
   `IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn`
   — built once at row construction by walking `actionDefinition.Properties`,
   resolving `GroupName` against `RecipeMetadataRegistry`, and projecting to
   `ComboBoxItemViewModel`. Read-only; never mutates after construction. Avalonia
   indexer-path binding (`GroupItemsByColumn[<columnKey>]`) does not need
   PropertyChanged because the dict is replaced when the row VM is replaced
   (the existing `RebuildRow` flow handles this — DataContext swap re-resolves
   the binding).
2. **Converter**: `ComboBoxItemMultiSelectionConverter` (resurrected from git)
   — `IMultiValueConverter` taking `[int value, IReadOnlyList<ComboBoxItemViewModel> items]`
   and returning `items.FirstOrDefault(item => item.Id == value)`. ConvertBack
   returns `[selectedItem.Id, BindingOperations.DoNothing]`.
3. **Factory**: `CreateGroupCellTemplate` switches to all-binding form:
   - `comboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsSourcePath))`
   - `comboBox.Bind(ComboBox.SelectedItemProperty, new MultiBinding { Converter = _groupSelectionConverter, Bindings = { value, items } })`
   - `supportsRecycling: true`.
   - Imperative assignment removed. `GetOrCreateGroupItems` and
     `_groupItemsByGroupName` cache are also removed — items now live on each
     row VM, computed from the same `RecipeMetadataRegistry` lookups.

## Technical Details

### `RecipeMetadataRegistry.GetComboBoxItems` (new — shared cache)

To avoid the per-row `List<ComboBoxItemViewModel>` allocation flagged in
review (200 rows × ≤3 groups × ≤27 items ≈ ~600 KB of duplicated
`ComboBoxItemViewModel` instances at HEAD's MBE config), add a single
group-name-keyed cache on the registry:

```csharp
// RecipeMetadataRegistry.cs — new field + method
private readonly Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>>
    _comboItemsByGroup = new(StringComparer.OrdinalIgnoreCase);

public IReadOnlyList<ComboBoxItemViewModel> GetComboBoxItems(string groupName)
{
    if (_comboItemsByGroup.TryGetValue(groupName, out var cached))
    {
        return cached;
    }

    var groupResult = GetGroup(groupName);
    if (groupResult.IsFailed)
    {
        return Array.Empty<ComboBoxItemViewModel>();
    }

    var items = groupResult.Value.Items
        .Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value))
        .OrderBy(item => item.Id)
        .ToList();

    _comboItemsByGroup[groupName] = items;
    return items;
}
```

This replaces `ComboBoxCellFactory._groupItemsByGroupName` (currently at
`ComboBoxCellFactory.cs:14`) and the row VM consumes the same cache.

### `RecipeRowViewModel.BuildGroupItemsByColumn` (restored, references-only)

```csharp
private static IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>>
    BuildGroupItemsByColumn(
        ActionDefinition actionDefinition,
        RecipeMetadataRegistry recipeMetadataRegistry)
{
    var result = new Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>>(
        StringComparer.OrdinalIgnoreCase);

    foreach (var actionProperty in actionDefinition.Properties)
    {
        if (actionProperty.GroupName is null)
        {
            continue;
        }

        result[actionProperty.Key] =
            recipeMetadataRegistry.GetComboBoxItems(actionProperty.GroupName);
    }

    return result;
}
```

Built once per row. Each entry is a **reference** to a shared cached list — no
materialisation per row. Per-row allocation is the `Dictionary` itself (~1-3
entries) plus pointer copies. For 200 rows × ≤3 entries × 8 bytes ≈ 5 KB total
at construction; zero allocation during scroll.

### Path helper

`ColumnTypes.GroupItemsPath(string columnKey)` returns
`$"GroupItemsByColumn[{columnKey}]"`. Mirrors the existing
`ColumnTypes.IndexerPath` shape used for cell values.

### Allocation profile (vs current HEAD)

| Hot path | HEAD (`supportsRecycling: false` for group) | Proposed (`true` + bindings) |
|---|---|---|
| Per cell recycled into view | New `ComboBox`, new `Binding`, new `IValueConverter` | Existing `ComboBox` reused; `BindingExpression` re-resolves indexer paths (no new objects) |
| Per row VM constructed | `_groupNamesByColumn` dict | `_groupNamesByColumn` dict **and** `GroupItemsByColumn` dict (~5 entries, refs to shared lists) |
| MultiBinding `Convert` invocation | n/a | Allocates one `IList<object?>` of size 2 per source change (per recycle / per user action change). Bounded by user actions, not scroll. |

Net: scroll path goes from `O(visible-rows × ComboBox alloc)` to
`O(visible-rows × binding re-resolve)`. Construction path adds bounded
per-row dict work paid once per row, not per scroll tick.

## What Goes Where

- **Implementation Steps** (`[ ]`): code restore + tests + build/test verification.
- **Post-Completion**: manual scroll smoke on a 200-row recipe in MBE config.

## Implementation Steps

### Task 1: Add `RecipeMetadataRegistry.GetComboBoxItems` cache + tests

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs`
- Modify or Create: `SemiStep/SemiStep.Tests/Core/RecipeMetadataRegistryTests.cs` (or nearest existing registry test class — check first)

- [x] Add private field `_comboItemsByGroup` (see Technical Details).
- [x] Add public method `GetComboBoxItems(string groupName)` returning cached `IReadOnlyList<ComboBoxItemViewModel>`, falling back to `Array.Empty<>` on lookup failure.
- [x] Add tests: (a) returns expected items for known group, (b) returns empty for unknown group (no exception), (c) returns same reference on repeated calls (cache verification).
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeMetadataRegistry"` — green before Task 2.

➕ Moved `ComboBoxItemViewModel` from `SemiStep.UI.RecipeGrid` to `SemiStep.Core.Recipes` so the registry (in Core) can produce it. Existing consumers (`ComboBoxItemSelectionConverter`, `ComboBoxCellFactory`, `ColumnBuilderIdempotencyTests`) already imported `SemiStep.Core.Recipes`; added the using to `ComboBoxItemSelectionConverter` where it was missing. No public API name change.

### Task 2: Resurrect `ComboBoxItemMultiSelectionConverter` + tests

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs`
- Create: `SemiStep/SemiStep.Tests/UI/ComboBoxItemMultiSelectionConverterTests.cs`

- [x] Restore: `git show 642f17b^:SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs` → write to current path. Diff against the restored file: there must be no changes beyond formatting.
- [x] Restore: `git show 642f17b^:SemiStep/SemiStep.Tests/UI/ComboBoxItemMultiSelectionConverterTests.cs` → write to current path.
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ComboBoxItemMultiSelection"` — green before Task 3.

### Task 3: Add `GroupItemsByColumn` on `RecipeRowViewModel` + tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [x] Add `BuildGroupItemsByColumn` static using `RecipeMetadataRegistry.GetComboBoxItems` (see Technical Details — references-only form).
- [x] Add field `_groupItemsByColumn` initialised in primary ctor.
- [x] Expose `public IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn => _groupItemsByColumn;`.
- [x] Restore four `GroupItemsByColumn_*` test cases from `git show 642f17b^:SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`. Update them if the original cases assert list contents that came from per-row materialisation — they should still pass when items come from the registry cache (same data, same shape). ➕ Two cases (`PrepopulatesEmptyList_…` and `OmitsKey_WhenActionGroupResolutionFails`) adapted to match the references-only body: the dict now walks `action.Properties` only (no registry-level pre-population) and stores `Array.Empty<>` references when the registry cannot resolve a group. Renamed to `OmitsKey_ForActionWithoutGroupProperty` and `ReturnsEmptyList_WhenActionGroupResolutionFails`.
- [x] Keep the existing `GetGroupNameForColumn_*` tests untouched.
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeRowViewModel"` — green before Task 4.

### Task 4: Re-add `ColumnTypes.GroupItemsPath` helper

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnTypes.cs`

- [x] Add `public static string GroupItemsPath(string columnKey) => $"GroupItemsByColumn[{columnKey}]";` next to existing `IndexerPath`.
- [x] Build: `dotnet build SemiStep/SemiStep.slnx`.

> Reviewer flagged this single-call-site helper as YAGNI. Kept anyway because
> the parallel `IndexerPath` method establishes the convention for path
> construction; a one-off string interpolation in the factory would diverge
> from that convention and be harder to refactor if path shape ever changes.

### Task 5: Rewrite `ComboBoxCellFactory.CreateGroupCellTemplate` + headless UI test

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Create: `SemiStep/SemiStep.Tests/UI/GroupComboBoxRecyclingTests.cs`

- [x] Replace the group-template lambda body with the all-binding shape:
      ```csharp
      comboBox.Bind(ItemsControl.ItemsSourceProperty,
          new Binding(ColumnTypes.GroupItemsPath(columnKey)));
      comboBox.Bind(ComboBox.SelectedItemProperty, new MultiBinding
      {
          Mode = BindingMode.TwoWay,
          Converter = _groupSelectionConverter,
          Bindings =
          {
              new Binding(ColumnTypes.IndexerPath(columnKey)) { Mode = BindingMode.TwoWay },
              new Binding(ColumnTypes.GroupItemsPath(columnKey))
          }
      });
      ```
- [x] Set `supportsRecycling: true`.
- [x] Add static singleton `private static readonly ComboBoxItemMultiSelectionConverter _groupSelectionConverter = new();`.
- [x] Remove `GetOrCreateGroupItems` method and `_groupItemsByGroupName` field.
- [x] Keep `_cachedActionItems` and the action template untouched. `ComboBoxItemSelectionConverter` remains in use for the action column — do not delete it.
- [x] Preserve `ApplyInputBlocking` call (the current cell-applicability path; do not regress to the 642f17b `BuildHitTestVisibleBinding`).
- [x] **Add headless UI test** `GroupComboBoxRecyclingTests` (`[AvaloniaFact]`) that reproduces the bug. ➕ Adapted to the available `WithGroups` test config: uses `WithGroupActionId` (action 50 / Valve / `target → valve` group) and `WaitActionId` (no group column). Three cases: (1) row with group action surfaces valve items; (2) DataContext swap from non-group row to group row updates `ItemsSource` (regression case for the recycling bug); (3) inverse swap empties `ItemsSource` so items do not leak across recycles.
- [x] Build: `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run filtered suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GroupComboBoxRecycling"`. Green before Task 6.

### Task 6: Manual scroll-perf smoke (user-executed)

**Files:** (no edits — checklist for user)

- [deferred-to-user] Build & run UI under MBE config: `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj`.
- [deferred-to-user] Create a 200-step recipe (paste, or insert + clone).
- [deferred-to-user] Verify: pick action 60 ("Открыть") on step 0 — target dropdown shows shutter items.
- [deferred-to-user] Verify: pick action 100 ("t°C скачком + ждать") on step 1 — target dropdown shows heater items.
- [deferred-to-user] Verify: pick action 40 ("Пауза") on step 2 — target cell renders as inapplicable (disabled), no dropdown items.
- [deferred-to-user] Fast-scroll the grid top-to-bottom and back. Watch for: visible lag (none expected), unchanged ItemsSource on visible cells (must follow the action of the visible row).
- [deferred-to-user] Open `C:\DISTR\Logs\Semistep\semistep.log` and confirm zero `Mutation entry: ChangeStepAction` or `Mutation entry: UpdateStepProperty` lines appear during pure scrolling (phantom-mutation check).

### Task 7: Verify acceptance criteria
- [x] All Overview requirements implemented.
- [x] All edge cases tested.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.
- [x] `dotnet format SemiStep/SemiStep.slnx` — clean.

### Task 8: [Final] Update documentation & file the plan
- [x] not applicable (no architecture.md in this project)
- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion

### Outstanding verification (user-owed)

Task 6 manual checks were **not** executed during automated implementation. They are
deferred to the user; the `[deferred-to-user]` markers above indicate items still
pending. The automated suite covers wiring + recycling regressions
(`GroupComboBoxRecyclingTests`) and converter contract
(`ComboBoxItemMultiSelectionConverterTests`); it does **not** cover real Avalonia
`DataGrid` virtualization or the `C:\DISTR\Logs\Semistep\semistep.log`
phantom-mutation log inspection.

**Manual verification**:
- 200-row scroll smoke under both MBE and MOCVD configs (Task 6 checklist).
- Visually confirm the MOCVD regression noted by the user
  ("переход Blowing → Open в MOCVD не меняет содержимое channel-выпадающего")
  is also closed by this fix.

**External system updates**: none. No public API change. No installer / CI
adjustment required (path filter `ConfigFiles/**` is unchanged).
