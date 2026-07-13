# Recipe Grid Surface Dedup

## Overview

Structural dedup follow-up to the transposed grid view (PR #130). `CanonicalRecipeGridSurface` (574 lines) and `TransposedRecipeGridSurface` (580 lines) are line-identical in every method modulo the item type (`RecipeRowViewModel` vs `StepColumnViewModel` + `.Row` indirection) except three spots; the execution-highlight trackers are byte-identical copies; the two transposed combo cell VMs differ only in where `Items` comes from; the string max-length rule is hand-copied between `ColumnBuilder` and `ParameterCellViewModelFactory`. Every future mutation-dispatch fix currently has to be found and applied twice — PR #130's review flagged this as the top long-term risk.

Deliverables:

- `RecipeGridSurfaceBase<TItem>` — shared abstract base owning the mutation dispatch, selection plumbing, reactive contract members, changed-highlight paths, and disposal; the two surfaces shrink to thin item-specific derivations.
- One `ExecutionHighlightTracker` serving both surfaces; `TransposedExecutionHighlightTracker` deleted.
- One `ComboBoxCellViewModel` replacing `ActionComboBoxCellViewModel` + `TargetComboBoxCellViewModel`.
- One shared string max-length resolution used by both `ColumnBuilder` and `ParameterCellViewModelFactory`.
- Fail-loud item creation: an unknown action key during projection throws instead of silently skipping the step and desyncing index-based dispatch (the one deliberate behavior change, in a path config/import validation makes unreachable).

Hard constraints:

- **Zero observable behavior change** everywhere except the fail-loud change above. All 1273 tests stay green unmodified except where a test names a deleted type or pins the old skip behavior.
- Public member names bound from XAML or used by consumers keep their exact names and signatures on the derived classes: `RecipeRows`, `StepColumns`, `ParameterDescriptors`, `ColumnBuilder`, `GridStyle`, `IsReadOnly`, `EditorMustClose`, `ClearChangedByClickAway`. Both view AXAML files and both view code-behinds stay untouched.
- Derived constructor signatures stay unchanged (DI registrations in `UiDi`, `UIFixture` factories, and `ActiveRecipeGridSurface` all construct or receive them as today).

## Context (verified against master `ed42473`)

**The three genuine divergences between the surfaces** (everything else is mechanical type substitution):

1. **Constructor**: same 6-parameter shape except the third parameter — `ColumnBuilder` (canonical) vs `GridStyleOptions` (transposed) — and the `ILogger<T>` generic argument. Transposed additionally builds `ParameterDescriptors = ParameterDescriptor.BuildFromRegistry(...)` and a `ParameterCellViewModelFactory`. Canonical stores the registry as an `internal` property (`RecipeMetadataRegistry`, consumed by its view); transposed stores it in a field.
2. **`ClearChangedByClickAway`**: canonical resolves the step index via `RecipeRows.IndexOf(row)`; transposed linear-scans `ReferenceEquals(StepColumns[i].Row, row)`. For canonical the two are equivalent (reference equality either way), so one linear-scan implementation over `RowOf(Items[i])` serves both without behavior change.
3. **`CreateRowViewModel` / `CreateColumnViewModel`**: canonical builds `BuildInapplicableColumns(...)` then `new RecipeRowViewModel(...)`; transposed news up `StepColumnViewModel(stepNumber, step, action, registry, ParameterDescriptors, factory.Create)`. Event wiring afterwards is identical modulo the row accessor.

**Everything shared already routes through single-copy collaborators**: `RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(rowAccessor, step)` and `ChangedCellClickAwayBroadcaster` are invoked with identical shapes on both sides (the only difference is `RecipeRows[i]` vs `StepColumns[i].Row`).

**Trackers**: `ExecutionHighlightTracker` and `TransposedExecutionHighlightTracker` are 79 lines each, identical state machine (`_lastRecipeActive`/`_lastActualLine`), identical methods (`OnExecutionStateChanged`, `Reset`, `ClearAllStepHighlights`, `ClearAllChangedHighlights`); the only difference is `_rows[i].X` vs `_stepColumns[i].Row.X`. `TransposedExecutionHighlightTracker` has zero direct tests (exercised only through `TransposedRecipeGridSurfaceTests`); `ExecutionHighlightTrackerJumpTests` (~7 cases) pins the canonical one.

**Combo cell VMs**: `ActionComboBoxCellViewModel` stores a registry-wide items list injected by the factory (`_actionItems` cached from `GetActionComboBoxItems()`); `TargetComboBoxCellViewModel` computes `Items => Row.GroupItemsByColumn[Descriptor.ParameterKey]` per access. `TransposedCellTemplateFactory.CreateTemplates` dispatches both through the same generic `CreateComboBoxTemplate<TCell>("Items")` — the two templates are behaviorally identical, distinguished only by the generic type token. `ParameterCellViewModelTests` (~16 cases) is the only test file naming the two types.

**Max-length duplication**: `ColumnBuilder.ResolveMaxLength(GridColumnDefinition)` — per-column, unwraps `GetProperty(...).Value` unguarded; `ParameterCellViewModelFactory.BuildMaxLengths(registry)` — eager `Dictionary<string, int?>` over all columns, guards `IsSuccess`. Same string-typed rule (`SystemTypes.Comparer.Equals(propertyDef.SystemType, SystemTypes.String)` → `GetStringMaxLength()`); the factory's comment self-describes as a mirror of the builder.

**TryCreate skip sites** (identical both sides): `TryCreateRow`/`TryCreateColumn` log + `_messagePanel.ReportError` + return null on unknown action key; callers skip — `AppendRow`/`AppendColumn` and `RebuildRow`/`RebuildColumn` return, `InsertRows`/`InsertColumns` and `FullRebuild` continue. Any skip leaves `Items.Count < recipe.StepCount`, silently desyncing every subsequent index-based dispatch and `CollectSelectedSteps`. Reachability: config loaders validate action keys, and CSV import / clipboard paste run `ImportedRecipeValidator` — the branch is defensive-only, which is exactly why crashing beats corrupting when the invariant is ever breached.

**XAML coupling to preserve** (compiled bindings, `x:DataType` on the concrete surface types): canonical — `RecipeRows`, `IsReadOnly`, `EditorMustClose`; transposed — `ParameterDescriptors`, `StepColumns`. Nested bindings target item VMs, not the surface.

**Test inventory pinning the refactor**: shared contract base `RecipeGridSurfaceContractTests` (13 cases × 2 subclasses); `CanonicalRecipeGridSurfaceTests` (~36), `CanonicalRecipeGridSurfaceReadOnlyTests` (~8), `TransposedRecipeGridSurfaceTests` (~32), plus view/editing/navigation/virtualization suites binding `RecipeRows`/`StepColumns`. `UIFixture.CreateCanonicalSurface`/`CreateTransposedSurface` construct the derived types directly.

## Development Approach

- **Testing approach: Regular.** This is a behavior-preserving refactor; the existing suite is the safety net and must pass unmodified except for tests that (a) name a deleted type, or (b) pin the old silent-skip behavior replaced by fail-loud.
- Pure structure moves land task by task; each task ends with a full-suite run, not just the touched slice — the point of the refactor is that one change now affects both surfaces.
- No public API changes outside the two surface classes' internals; `IRecipeGridSurface` is untouched.

## Testing Strategy

- **Contract tests**: both `RecipeGridSurfaceContractTests` subclasses (13 × 2) pass unmodified — they pin the reactive contract the base class now owns.
- **Surface suites**: `CanonicalRecipeGridSurfaceTests`, `TransposedRecipeGridSurfaceTests`, read-only, selector-edit, cross-surface sync, virtualization, editing, and navigation suites pass unmodified.
- **Tracker**: `ExecutionHighlightTrackerJumpTests` retargets the unified tracker construction; one new case runs the jump scenario through a transposed surface to close the previously-untested transposed tracker path.
- **Combo collapse**: `ParameterCellViewModelTests` renames the two type references; assertions unchanged (items content, write paths).
- **Fail-loud**: new test per surface — a projection over a recipe containing an unknown action key throws `InvalidOperationException` naming the step and key. Concrete route (no production seam exists, do not add one): construct the surface directly — not via the `UIFixture` factory, which shares the fixture registry — passing a registry that lacks the seeded action key, over a coordinator seeded with that action, then call `Initialize()`; the first step throws. (Review follow-up: the mutation path is additionally pinned at coordinator level — drive a mutation whose item creation fails, assert a probe handler subscribed after the failing surface still receives the signal and the message panel got the error.)
- Final gate: full suite green, `dotnet format` clean.

## Solution Overview

### `RecipeGridSurfaceBase<TItem>` shape

```
public abstract class RecipeGridSurfaceBase<TItem> : ReactiveObject, IRecipeGridSurface
    where TItem : class, IDisposable
{
    protected RecipeGridSurfaceBase(
        RecipeCoordinator coordinator,
        RecipeMetadataRegistry recipeMetadataRegistry,  // the base owns the CreateItemChecked lookup
        MessagePanelViewModel messagePanel,
        ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
        ILogger logger)                       // derived passes its ILogger<T>

    protected ObservableCollection<TItem> Items { get; }   // derived exposes RecipeRows / StepColumns as an alias property

    // item-specific hooks
    protected abstract RecipeRowViewModel RowOf(TItem item);
    protected abstract TItem CreateItem(int stepNumber, Step step, ActionDefinition action);
    // no DisposeItem hook: both item types dispose themselves — constrain
    // `where TItem : class, IDisposable` and call item.Dispose() in the base

    // everything below moves from the two copies verbatim:
    // OnMutation dispatch + structural handlers (append/insert/remove/removes/rebuild/full-rebuild)
    // Renumber + ReconcileSelection + RefreshStepStartTimes + RefreshLoopDepths tail
    // CanDeleteStep / EditorMustClose / _selectionRequests reactive block
    // UpdateSelection / RequestSelection / SelectedStepIndex(/-ices) / CollectSelectedSteps
    // OnCellValueChanged / OnSelectorValueChanged / OnActionChanged (RowOf(...) replaces row/.Row access)
    // ClearChangedByClickAway (single linear-scan implementation) + broadcaster subscription
    // RecipeRowUpdateSynchronizer call, stale-signal guards, Dispose
}
```

- The registry lookup lives in the base (`CreateItemChecked`, see fail-loud below); `TryCreateRow`/`TryCreateColumn` disappear.
- `RecipeRowViewModel` event wiring (`PropertyValueChanged`/`ActionChanged`/`SelectorValueChanged` subscribe + unhook on dispose) moves into the base around `CreateItem`, since it is identical on both sides and operates on `RowOf(item)` plus the item-typed handler methods.
- Log message templates unify on neutral wording (`Items`/`ItemCount`); message text is not pinned by any test.
- Canonical keeps `public ColumnBuilder ColumnBuilder { get; }`, `public ObservableCollection<RecipeRowViewModel> RecipeRows => Items;`. (Review follow-up: the canonical `internal RecipeMetadataRegistry` alias turned out to have zero consumers and was dropped; the base exposes the registry as a `protected` property instead.) Transposed keeps `public GridStyleOptions GridStyle { get; }`, `public IReadOnlyList<ParameterDescriptor> ParameterDescriptors { get; }`, `public ObservableCollection<StepColumnViewModel> StepColumns => Items;`. Derived classes should land around 60–100 lines each.

### Unified execution tracker

`ExecutionHighlightTracker` changes its dependency from `ObservableCollection<RecipeRowViewModel>` to `(Func<int> count, Func<int, RecipeRowViewModel> rowAt)` (or an equivalent single accessor interface — executor's call, whichever reads cleaner at both construction sites). The base class constructs it over `Items.Count` / `RowOf(Items[i])`. `TransposedExecutionHighlightTracker` is deleted.

### One combo cell VM

`ComboBoxCellViewModel(RecipeRowViewModel row, ParameterDescriptor descriptor, Func<IReadOnlyList<ComboBoxItemViewModel>> itemsProvider)` with `Items => itemsProvider()`. Factory wiring: action column → `() => _actionItems` (cached list, semantics unchanged); group-bound column → `() => row.GroupItemsByColumn[descriptor.ParameterKey]` (per-access, semantics unchanged). `TransposedCellTemplateFactory` drops one of the two identical template registrations. Both old VM classes are deleted.

### Shared max-length rule

One helper (e.g. `StringColumnMaxLengths.Build(RecipeMetadataRegistry) : IReadOnlyDictionary<string, int?>`, `OrdinalIgnoreCase`, `IsSuccess`-guarded — the factory's variant is the survivor) built once and consumed by both `ParameterCellViewModelFactory` (as today) and `ColumnBuilder` (replacing `ResolveMaxLength`; the unguarded `.Value` unwrap goes away — unreachable divergence, config-validated upstream). The factory's mirror comment is deleted with the mirror.

### Fail-loud item creation

`CreateItemChecked(Step step, int stepNumber)` in the base: registry lookup failure throws `InvalidOperationException("Step {n}: unknown action key '{key}'")` instead of report-and-null. All former null-skip call sites simplify to direct calls. The `_messagePanel.ReportError` in this path is deleted (an exception is the report now); the surrounding stale-signal guards are untouched — they protect a different invariant (signal vs projection races) and stay log-and-return.

**Honest scoping of "loud" (final semantics after review):** the throw genuinely crashes only on the `Initialize()` path (startup projection and the router's `Initialize` fan-out). On the live mutation-dispatch path, letting the throw escape to `RecipeCoordinator.RaiseMutatedSafely` would abort the multicast `Mutated?.Invoke` and starve every later-subscribed handler (the sibling surface included) of the signal — so the base's `OnMutation` catches the specific `UnknownActionKeyException`, error-logs it, reports it to the message panel (matching master's user-visible outcome), and leaves the projection as-is. Sibling subscribers keep receiving signals; nothing escapes to the coordinator.

## Implementation Steps

### Task 1: Unify the execution highlight tracker

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs`
- Delete: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedExecutionHighlightTracker.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridSurface.cs`, `Transposed/TransposedRecipeGridSurface.cs` (construction sites)
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ExecutionHighlightTrackerJumpTests.cs`

- [x] Rework `ExecutionHighlightTracker` to consume an item-count + row-accessor pair instead of `ObservableCollection<RecipeRowViewModel>`; logic byte-identical otherwise.
- [x] Both surfaces construct the unified tracker; delete `TransposedExecutionHighlightTracker`.
- [x] Retarget ALL seven `new ExecutionHighlightTracker(...)` construction sites in `ExecutionHighlightTrackerJumpTests` (plus the two production sites); add one jump-scenario case driven through a transposed surface (closes the untested transposed tracker path).
- [x] Run the full test suite.

### Task 2: Extract `RecipeGridSurfaceBase<TItem>`

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridSurface.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridSurface.cs`

- [x] Move every identical-modulo-type member listed in Solution Overview into the base verbatim; derived classes keep only: constructor (unchanged public signature), the `RowOf`/`CreateItem` hooks, and their view-facing extras (`ColumnBuilder`/`RecipeMetadataRegistry`/`RecipeRows` vs `GridStyle`/`ParameterDescriptors`/`StepColumns`).
- [x] `ClearChangedByClickAway` lands once in the base as the linear scan over `RowOf(Items[i])` (equivalent to canonical's `IndexOf` — reference equality either way).
- [x] XAML compiled bindings still resolve: alias properties keep exact names; both `.axaml`/`.axaml.cs` files untouched; `x:DataType` stays the concrete type.
- [x] No change to `UiDi`, `ActiveRecipeGridSurface`, `UIFixture` factories, or any consumer.
- [x] Run the full test suite — all 26 contract cases and both surface suites green unmodified.

### Task 3: Collapse the combo cell VMs

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/ComboBoxCellViewModel.cs`
- Delete: `Transposed/ActionComboBoxCellViewModel.cs`, `Transposed/TargetComboBoxCellViewModel.cs`
- Modify: `Transposed/ParameterCellViewModelFactory.cs`, `Transposed/TransposedCellTemplateFactory.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/ParameterCellViewModelTests.cs`

- [x] One `ComboBoxCellViewModel` with an injected items provider per Solution Overview; factory passes the cached action list or the per-access group lookup.
- [x] Template factory registers a single combo template; dispatch for text/read-only kinds unchanged.
- [x] `ParameterCellViewModelTests` retargets type names; assertion bodies unchanged.
- [x] Run the full test suite (editing/virtualization suites exercise recycled combo bindings).

### Task 4: Share the string max-length rule

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/StringColumnMaxLengths.cs` (or executor's better-named equivalent in the same namespace)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`, `Transposed/ParameterCellViewModelFactory.cs`

- [x] Single guarded implementation (the factory variant survives); `ColumnBuilder.ResolveMaxLength` and `ParameterCellViewModelFactory.BuildMaxLengths` both delegate to it or are replaced by it.
- [x] The factory's "mirror" comment goes away with the mirror.
- [x] Existing max-length tests (canonical `RecipeGridStringMaxLengthTests`, transposed editing tests) pass unmodified.
- [x] Run the full test suite.

### Task 5: Fail-loud item creation

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/CanonicalRecipeGridSurfaceTests.cs`, `Transposed/TransposedRecipeGridSurfaceTests.cs` (or a new shared test file — executor's call)

- [x] `CreateItemChecked` throws `InvalidOperationException` naming step number and action key on registry-lookup failure; all former null-skip sites become direct calls; the message-panel report in this path is removed.
- [x] Delete or rewrite any test pinning the old silent-skip behavior (the transposed stale-signal suite asserts warnings, not skips — verify).
- [x] New test per surface: registry-mismatch construction route per Testing Strategy (surface built directly with a registry lacking the seeded action key, `Initialize()` throws with step number and key in the message). Do not add a production test seam and do not assert through `coordinator.Mutated` — `RaiseMutatedSafely` swallows handler exceptions.
- [x] Run the full test suite.

### Task 6: Verify and close out

- [x] Full suite green; `dotnet format SemiStep/SemiStep.slnx` clean.
- [x] Line counts: both derived surfaces under ~120 lines; no duplicated method bodies remain between them (spot-check by diffing member lists).
- [x] Update `Docs/architecture/recipe-grid-surface.md`: base-class shape, the hooks, unified tracker, fail-loud creation described honestly (crashes on `Initialize()`; on the `Mutated` path the coordinator's `RaiseMutatedSafely` catches and error-logs it — no crash, no silent desync); remove the deferred-dedup note. Update `Docs/architecture/cell-change-highlight.md` only if it names the deleted tracker type.
- [x] Move this plan to `Docs/plans/completed/`. (harness moves it)

## Post-Completion

**Manual verification:** open RIE (transposed default) and MOCVD (canonical), exercise edit/selection/flip/execution highlighting — behavior identical to pre-refactor master.

**Explicitly out of scope** (rejected during PR #130 review analysis, do not resurrect): merging `ParameterDescriptor` into `ColumnDefinition` (descriptor also carries cell-kind dispatch semantics), replacing the `StepColumnViewModel` cell-factory delegate (live test seam), and any behavior/UX changes beyond fail-loud creation.
