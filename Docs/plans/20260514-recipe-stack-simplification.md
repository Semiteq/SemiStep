# Recipe stack radical simplification (Round-9)

## Overview

Five parallel architecture-audit subagents (2026-05-14 session) converged on a unanimous diagnosis: SemiStep's Recipe state layer is a "CRUD app dressed in event-sourced clothing" — seven state holders, five pass-through classes, a `Subject<MutationSignal> + ObserveOn(MainThreadScheduler)` queue that races against recipe-replace operations, and imperatively-built `FuncDataTemplate<T>` cell templates whose TwoWay bindings fire mutation events on every DataContext swap during virtualization. Every "specific weird issue" the user has reported (action ComboBox NRE on click, signal storm, GC pressure, selection jumping, copy-paste selection inconsistency, recipe-load crash) lives at the seam between these layers.

The architecture was carried in from a legacy project. It is being incrementally simplified. This refactor consolidates that direction in one pass — except for the imperative cell-template seam, which is a Round-10 follow-up.

### Goals (must-do for this round — Round-9)

**Stability and signal-storm mitigation:**
- Eliminate the stale-signal class of bugs (crash on recipe-load) by replacing `Subject<MutationSignal>` with synchronous direct method calls.
- **Mitigate** phantom mutations from TwoWay binding refreshes by guarding row VM setters against equal-value writes. **Structural elimination** is Round-10.
- Roll back `supportsRecycling: true` on **all** cell templates affected by Round-8 (ComboBox + Text), restore the factory-level group-items cache. This closes the phantom-mutation hazard at the cost of allocation regression — acceptable interim until Round-10 lands XAML compiled bindings.
- Eliminate the selection-jumping race by collapsing two competing selection writers into one.
- Eliminate the broken error window by restructuring `Program.Main` to detect startup errors before any `BuildAvaloniaApp()` call.

**Structural flatten:**
- Collapse the Recipe state layer from 7 classes to 2 (`RecipeSession` + `RecipeCoordinator`).
- Delete confirmed-dead code: `Formulas/` directory (every call site passes `null`), `MutationSignal.MetadataChanged` (one emitter, no consumer), unreachable switch defaults.

**Observability:**
- Add structured logging at key boundaries (recipe lifecycle, mutation entry, signal handlers, selection changes). Scope kept tight — no per-cell Debug spam.

### Goals (deferred — Round-10)

The structural fix for the binding-seam class of bugs is to move imperative `FuncDataTemplate<T>` cell templates to XAML `<DataTemplate>` resources with compiled bindings. That eliminates:
- The per-cell binding weight (6 bindings + ContentControl wrapper today)
- The recycling-vs-no-recycling tension that Round-8 tried to resolve and Round-9 rolls back
- The TwoWay-writeback-on-DataContext-swap hazard structurally (Avalonia owns recycling internally; compiled bindings don't fire spurious source writes)

Round-9 mitigates this class. It does not eliminate it. The equal-value guard in Task 1 and the `supportsRecycling: false` flip in Task 5 are interim defenses. Plan note for Round-10 included at the bottom.

This is a **structural refactor — no feature changes.** All current behaviours preserved.

## Context (from discovery)

Files affected (~25):

**State layer (`SemiStep.Core/Recipes/`):**
- `RecipeWorkspace.cs` (170 LOC) — pass-through over StateManager + HistoryManager + Analyzer.
- `State/RecipeStateManager.cs` (39 LOC) — 2 fields, 3 flags.
- `State/RecipeHistoryManager.cs` (62 LOC) — undo/redo stacks (legitimate).
- `RecipeEditor.cs` (211 LOC) — stateless mutation methods.
- `RecipeQueryService.cs` (72 LOC) — pure forwarder over Workspace + Plc + Clipboard + Validator.
- `Formulas/` directory — confirmed dead in production: every call site passes `formulaDefinition: null`. The whole subsystem is reachable only via tests.
- `Recipes/Analysis/`, `Recipes/Import/`, `Recipes/Validation/`, `Recipes/StepInitializer.cs` — legitimate domain logic; untouched.

**Coordinator layer (`SemiStep.UI/Coordinator/`):**
- `RecipeMutationCoordinator.cs` (334 LOC) — half-delegator: ~180 lines pass-through to `_stepCoordinator`, ~150 lines real content (PLC + message panel + file ops).
- `RecipeStepCoordinator.cs` (183 LOC) — closure-in-a-class; takes 6 callback `Action`s in ctor.
- `MutationSignal.cs` (22 LOC) — discriminated union; keep the type, drop the `MetadataChanged` variant.

**UI layer (`SemiStep.UI/`):**
- `RecipeGrid/RecipeGridViewModel.cs` (408 LOC) — observes coordinator signals via `ObserveOn(MainThreadScheduler)`.
- `RecipeGrid/RecipeRowViewModel.cs` (~190 LOC) — `SetPropertyValue` fires events without equal-value guard.
- `RecipeGrid/ComboBoxCellFactory.cs` — `supportsRecycling: true` (Round-8).
- `RecipeGrid/TextCellFactory.cs` — `supportsRecycling: true` on at least two templates (lines ~64, ~104 per plan-review audit) — phantom-mutation hazard applies here too.
- `MainWindow/MainWindow.axaml` (line 31 — `SelectedIndex` TwoWay binding) and `.cs` (line 127 — imperative `SelectionChanged` handler). Dual selection writer race.
- `App.axaml.cs` (line 98 — `RunErrorWindow` calls `BuildAvaloniaApp()` a second time and fails).
- `Program.cs` (line 35 — `Run` call) and (line 51 — `RunErrorWindow` call). Restructuring entry point is part of Task 4.

**Out of scope (Round-10):**
- `RecipeGrid/CellPresenter.cs`, `RecipeGrid/ColumnBuilder.cs`, the eight converters under `RecipeGrid/`. The Round-10 plan will replace these with XAML `<DataTemplate>` resources.

**Tests affected:**
- `SemiStep.Tests/UI/RecipeRowViewModelTests.cs`
- `SemiStep.Tests/UI/RecipeGridViewModelTests.cs`
- `SemiStep.Tests/UI/RecipeMutationCoordinatorTests.cs`
- `SemiStep.Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs`
- `SemiStep.Tests/UI/ColumnBuilderIdempotencyTests.cs`
- `SemiStep.Tests/Core/*` — many tests pin Workspace/Editor/Manager class shapes; rewrite to pin `RecipeSession` shape preserving the same assertions.
- `SemiStep.Tests/UI/Helpers/UIFixture.cs` — adapt to the new coordinator surface.

**Constraints:**
- All behavioural contracts preserved: undo/redo semantics, PLC sync state machine, CSV load/save format, recipe validation rules.
- Build green and ≥309 tests passing at every task boundary.

## Development Approach

- **Testing approach: Regular** with one critical exception: **characterization tests land BEFORE state-layer collapse** (Task 8). These tests pin the current behaviour of `RecipeWorkspace + RecipeEditor + RecipeStateManager + RecipeHistoryManager + RecipeQueryService` BEFORE we merge them, so the merge is verifiable.
- **Atomic commits per task.** Each task is a self-contained green commit. No bundling.
- **Strict phase ordering.** Stability → recycling rollback → characterization tests → state collapse → direct calls → cleanup → logging → verify. Skipping ahead compounds the refactor surface.
- **No silent behaviour changes.** Anything that changes behaviour is called out in the task description and verified manually.

## Testing Strategy

- **Unit tests at task boundaries.** Each task ends with `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes` green.
- **Characterization tests (Task 8) lock in semantics before the merge.** Tests target the OLD class APIs and assert on outcomes (recipe state, undo/redo, generation/version where relevant). After the merge, these tests are rewritten in place to target the NEW class API while preserving the same assertions — divergence between OLD and NEW behaviour is a fail.
- **No new e2e tests.** Project has no UI-based e2e harness.
- **Test count baseline.** Currently 351 (post-Round-8). After Round-9, expect 320-360 depending on test-class merges. Document final count in archive task.

**Manual UI smoke (mandatory before PR open):**

Establish baselines BEFORE Phase 1 starts:
- **B1:** open `recipe-100row.csv` (or any ≥100-row recipe); record gen-0 collections / second during 30s continuous scroll via `dotnet-counters monitor` or Task Manager. Store as `Docs/plans/work/round-9-baseline.md` if needed for comparison.
- **B2:** record working set MB after the same 30s scroll.

Run all scenarios at the end of Phase 7:
1. Launch app, open a recipe with 100+ rows.
2. Click action ComboBox → dropdown opens on first click.
3. Click group ComboBox → dropdown opens on first click.
4. Change an action → row rebuilds without phantom mutations. Verify via the structured log added in Task 17: the `MutationEntry` log line fires **once** per user action, not multiple times.
5. Edit a property cell → value commits, single `PropertyUpdated` signal in logs.
6. Copy 10 rows, paste at the end → pasted rows are selected (no flicker to last row or to empty selection).
7. Load a small recipe (≤5 rows) over a large one (≥200 rows) — no crash, grid replaced cleanly.
8. Start/stop PLC sync — grid read-only state toggles cleanly.
9. Scroll continuously for 30s — gen-0/sec drops by ≥30% vs B1; working set plateaus within ±5% of B2.
10. Undo/redo operations preserve grid + selection state.
11. Trigger a startup error (rename `ConfigFiles/`) — ErrorWindow displays correctly.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

### Before (current state)

```
Recipe (record)
  └─ owned by RecipeStateManager (2 fields)
      └─ wrapped by RecipeHistoryManager (undo/redo)
          └─ composed in RecipeWorkspace (pass-through)
              └─ mutated via RecipeEditor (stateless)
                  └─ wrapped per-method by RecipeStepCoordinator (callback-bag)
                      └─ exposed via RecipeMutationCoordinator (half pass-through)
                          ├─ Subject<MutationSignal> + ObserveOn(MainThreadScheduler)
                          └─ pure-forward reads via RecipeQueryService

RecipeGridViewModel.OnStateChange ← subscribes
```

### After (target shape)

```
Recipe (record)
  └─ owned by RecipeSession (~400 LOC; state + history + analyzer + mutations + Mutated event/Attach sink)
      └─ direct synchronous method calls on IRecipeSink (RecipeGridViewModel implements)
          
RecipeCoordinator (~200 LOC; PLC orchestration + file ops + message panel + clipboard query forwarding)
  └─ owns one RecipeSession; exposes facade for ViewModels
```

**Class count:** 7 → 2 in the state layer. **Signal channel:** queued pub-sub → synchronous direct method calls. Two real classes own the responsibilities they share today.

### Signal protocol after refactor

`MutationSignal` discriminated union **stays** — it encodes which kind of incremental grid update is needed. It becomes the **parameter type** of `IRecipeSink.OnMutation(MutationSignal signal)`, not the payload of a `Subject`. Synchronous invocation in the same stack frame as the mutation eliminates the queue and the ObserveOn race entirely.

**No generation tag.** Per plan-review YAGNI critique: synchronous calls make generation drift impossible. If a future async path is introduced, add the tag then.

### IRecipeSink wiring — Attach pattern

Constructor cycle (Coordinator needs Sink, Sink needs Coordinator) is resolved via two-phase init:

1. DI constructs `RecipeSession`, `RecipeCoordinator`, and `RecipeGridViewModel` independently — none takes `IRecipeSink` in its ctor.
2. `App.InitializeServices` (already runs post-DI as an AppBuilder.AfterSetup callback) calls `coordinator.Attach(gridViewModel)`. After Attach, mutations on the coordinator route to the grid's `OnMutation` synchronously.
3. Before Attach, the coordinator buffers mutations into a no-op (since no one is observing). In practice the grid attaches before any mutation runs because user input requires the main window to exist.

This pattern satisfies `CLAUDE.md` "constructor injection only" — the sink dependency is a runtime registration, not a constructor parameter.

### Selection wiring after refactor

- Drop `MainWindow.axaml:31` `SelectedIndex="{Binding RecipeGrid.SelectedRowIndex}"` TwoWay binding.
- `RecipeGridViewModel.SelectedRowIndex` becomes a computed property: `SelectedRowIndices.Count > 0 ? SelectedRowIndices[0] : -1`. No setter from binding.
- The imperative `MainWindow.OnSelectionChanged` handler is the sole writer of `SelectedRowIndices`.
- Coordinator's `SuggestedSelection` consumed-once side-channel becomes the **return value** of mutation methods (`Result<int?>` or similar). All call sites that previously read `ConsumeSuggestedSelection` switch to consuming the return value. Caller writes to `RecipeGrid.SelectedIndex` directly via code-behind, not through the VM. Audit list of call sites: `ClipboardViewModel` (paste), `RecipeCommandsViewModel` (insert), `RecipeFileViewModel` (load) — each updated in Task 3.

### Error window after refactor

`Program.Main` is restructured to detect startup errors **before any `BuildAvaloniaApp()` call**:

```
1. Build IServiceProvider (DI)
2. Validate startup state (config files, registry, etc.) — collect any errors
3. If errors: call App.RunErrorWindow(errors) — one and only Setup
4. Else: call App.Run(serviceProvider) — one and only Setup
```

Only one of `Run` / `RunErrorWindow` ever executes per process. Both call `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)` exactly once. The current code already has this structure but the validation step (#2) is implicit — `App.Run` discovers errors mid-init and tries to recover by calling `RunErrorWindow` after `Setup` already ran. Fix: surface validation earlier so the error window path is taken before any Setup.

## Technical Details

### Phase 1: Stability fixes (4 tasks)

Narrow blast radius. No architecture change. Resolves the crash and bulk of the signal storm.

#### Task 1: equal-value guard in `RecipeRowViewModel.SetPropertyValue`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [ ] In `SetPropertyValue`, before firing `ActionChanged` for the action column: read current `_step.ActionKey`; skip if `int.TryParse(value) == _step.ActionKey`.
- [ ] Before firing `PropertyValueChanged` for non-action columns: read current value via `GetPropertyValue(columnKey)`; skip if string-equal (ordinal).
- [ ] Add tests: `SetPropertyValue_ActionWithSameId_DoesNotFireEvent`, `SetPropertyValue_PropertyWithSameValue_DoesNotFirePropertyChanged`, `SetPropertyValue_ActionWithDifferentId_FiresEvent` (positive control), `SetPropertyValue_PropertyWithDifferentValue_FiresPropertyChanged` (positive control).
- [ ] Run `dotnet build` + `dotnet test`.

#### Task 2: defensive bounds in `RecipeGridViewModel.OnStateChange` handlers

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/Di/` extension where the grid VM is registered (add `ILogger<RecipeGridViewModel>` injection if not already there)
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`

- [ ] In `UpdateSingleRowInPlace`, `RebuildRow`, `AppendRow`, `InsertRows`: validate `stepIndex < recipe.StepCount` and `< RecipeRows.Count` (where applicable) before indexing. Out-of-range → log warning via `ILogger`, return.
- [ ] Inject `ILogger<RecipeGridViewModel>` via constructor if not already present.
- [ ] Add test: simulating a stale signal (manually invoke OnStateChange with out-of-range index) → no exception, warning logged.
- [ ] Run `dotnet build` + `dotnet test`.

#### Task 3: collapse dual selection wiring

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (drop `SelectedIndex` binding)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` (audit `OnSelectionChanged`)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` (`SelectedRowIndex` becomes computed)
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs` (paste's `SuggestedSelection` consumption)
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` (load's `SuggestedSelection` consumption)
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeCommandsViewModel.cs` (insert's `SuggestedSelection` consumption)
- Modify tests to match.

- [ ] Audit all `_coordinator.ConsumeSuggestedSelection()` call sites. List them in this task before changes.
- [ ] Remove `SelectedIndex="{Binding RecipeGrid.SelectedRowIndex}"` from MainWindow.axaml:31.
- [ ] Change `SelectedRowIndex` in RecipeGridViewModel to a computed property derived from `SelectedRowIndices`. Remove its setter.
- [ ] Change mutation methods that previously set `SuggestedSelection` side-channel to return the suggested index as part of their result (e.g., `Result<MutationOutcome>` where `MutationOutcome` carries `SuggestedSelectionIndex`). Callers consume the return value and set `DataGrid.SelectedIndex` via code-behind (the grid passed via the ViewModel's `Attach`-able `IRecipeGridView` interface OR via a property on the grid VM read by code-behind — pick one).
- [ ] Verify `OnSelectionChanged` handler in MainWindow.axaml.cs is the only writer of `SelectedRowIndices`.
- [ ] Add test: after a paste mutation, the returned index reflects the pasted range start; no transient flicker observable via VM property changes.
- [ ] Run `dotnet build` + `dotnet test`.

#### Task 4: fix `App.RunErrorWindow` by restructuring `Program.Main`

**Files:**
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/Program.cs`

- [ ] Audit `Program.Main`. Currently calls `App.Run(serviceProvider)` and if that fails, calls `App.RunErrorWindow(errors)` — but `Run` already ran `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)`, calling Setup. The fallback `RunErrorWindow` runs Setup again → crash.
- [ ] Restructure: detect startup errors via a `StartupValidator` (or inline in `Program.Main`) BEFORE any `BuildAvaloniaApp()` call. If errors found, pass them to `App.RunErrorWindow` — never call `App.Run`. If no errors, call `App.Run` — never call `App.RunErrorWindow`.
- [ ] `App.RunErrorWindow` and `App.Run` each call `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)` exactly once per process.
- [ ] Verify by manually triggering a startup error: rename `ConfigFiles/`, launch app, confirm `ErrorWindow` displays.
- [ ] No new unit tests (manual smoke verifies). Run `dotnet build` + `dotnet test`.

### Phase 2: Recycling rollback for all affected templates (3 tasks)

Phantom-mutation hazard from Round-8's `supportsRecycling: true` is structurally addressed only by full XAML migration (Round-10). For now, roll back to false on ALL templates that were flipped in Round-8.

#### Task 5: enumerate `supportsRecycling: true` templates

**Files:**
- Read: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Read: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs`
- Read: any other `*CellFactory.cs` or template-building location

- [ ] Grep `supportsRecycling` across the UI project. Document every occurrence in a checklist comment in this task (or as a `➕` discovery item). Plan-review found ComboBox + Text; verify there are no others.
- [ ] No code changes in this task — discovery only. Tasks 6-7 do the flips.
- [ ] Run `dotnet build` (no-op verification).

#### Task 6: flip ComboBox templates to `supportsRecycling: false`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` (drop `GroupItemsByColumn` if no longer needed)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs` (delete if no longer needed)
- Modify: tests in `SemiStep.Tests/UI/`

- [ ] Change both `CreateActionCellTemplate` and `CreateGroupCellTemplate` to `supportsRecycling: false`.
- [ ] Re-add factory-level `_groupItemsByGroupName` dictionary in `ComboBoxCellFactory`. `InvalidateCaches()` clears it.
- [ ] In group template lambda, resolve items list at template-build time via the factory cache (lambda re-captures `row` for `row.GetGroupNameForColumn(columnKey)` — safe because recycling is now `false`).
- [ ] Remove `RecipeRowViewModel.GroupItemsByColumn` and re-add `GetGroupNameForColumn` / `GetGroupItemsForColumn` if those were deleted.
- [ ] Delete `ComboBoxItemMultiSelectionConverter` (no longer needed — single-binding `ComboBoxItemSelectionConverter` works with closure-captured items).
- [ ] Update tests in `RecipeRowViewModelTests.cs` and `ComboBoxItemMultiSelectionConverterTests.cs` to match (likely deleting the latter).
- [ ] Run `dotnet build` + `dotnet test`.

#### Task 7: flip Text templates to `supportsRecycling: false`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (verify line numbers from Task 5)
- Modify: tests if applicable

- [ ] Flip the `supportsRecycling: true` instances identified in Task 5 to `false`. If any of them are documented as "pure MultiBinding, safely true" per the Round-7 plan, keep the flag (verify by reading the relevant Round-7 doc section). If they were flipped in Round-8 alongside ComboBox cells, roll them back.
- [ ] Run `dotnet build` + `dotnet test`.

### Phase 3: Characterization tests + state-layer collapse (5 tasks)

Highest-risk phase. Characterization tests land BEFORE the merge to lock in semantics.

#### Task 8: characterization tests for Workspace + Editor + StateManager + HistoryManager + QueryService

**Files:**
- Create: `SemiStep/SemiStep.Tests/Core/RecipeBehaviourCharacterizationTests.cs`

- [ ] Write tests against the OLD class APIs that pin behaviour soon-to-be-merged. Coverage:
  - Workspace apply mutates current recipe, increments dirty flag.
  - Workspace.Reset clears dirty flag, clears history.
  - Workspace.MarkSaved clears dirty flag, preserves history.
  - Editor.AppendStep returns the new step index in the result. AppendStep with invalid actionId returns a failed Result.
  - Editor.InsertStep/RemoveStep/RemoveSteps preserve step ordering invariants.
  - Editor.ChangeStepAction does NOT change the action if newActionId == current (no-op result).
  - Editor.UpdateStepProperty validates against the property type definition; failures return a failed Result with a specific reason.
  - HistoryManager.Push records pre-mutation state.
  - HistoryManager.Undo restores the prior state and pushes onto redo stack.
  - HistoryManager.Redo restores the next state.
  - LoadAsCurrent / LoadAsCurrentValidated replace the recipe and clear undo/redo history.
  - QueryService surfaces CurrentRecipe, IsDirty, CanUndo, CanRedo, Snapshot — all reflect Workspace state.
- [ ] Tests target the OLD APIs (Workspace.Apply / Editor.AppendStep / HistoryManager.Undo / etc.). After Phase 3 merges these into RecipeSession (Task 9-11), these tests get rewritten to target the new API while preserving the same assertions.
- [ ] At least 20 tests covering the above behaviours. Existing `RecipeWorkspaceTests` / `RecipeEditorTests` may already cover some — supplement to gap-fill, do not duplicate.
- [ ] Run `dotnet build` + `dotnet test`. All new tests green against the unchanged code.

#### Task 9: create `RecipeSession` merging Workspace + StateManager + Editor

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`
- Keep temporarily: `RecipeWorkspace.cs`, `RecipeStateManager.cs`, `RecipeEditor.cs`, `RecipeHistoryManager.cs` (deleted in Task 11)

- [ ] Build new class `RecipeSession` containing:
  - State: `Recipe _current`, history stacks (from RecipeHistoryManager — inline or keep as private static helpers).
  - Inlined methods: `Apply`, `Undo`, `Redo`, `Reset`, `LoadAsCurrent`, `LoadAsCurrentValidated`, `MarkSaved`.
  - Inlined mutation methods from Editor: `AppendStep`, `InsertStep`, `RemoveStep`, `RemoveSteps`, `InsertSteps`, `ChangeStepAction`, `UpdateStepProperty`.
  - Public: `Recipe Current`, `bool IsDirty`, `bool CanUndo`, `bool CanRedo`.
  - Methods return mutation outcomes that include suggested selection index (replacing the side-channel `SuggestedSelection`).
- [ ] RecipeHistoryManager is **inlined** into RecipeSession (not kept as a separate class) — its 62 LOC are simple enough that the encapsulation doesn't earn its keep at this scale.
- [ ] DI registration of new `RecipeSession` alongside existing classes (both registered until Task 11 deletes the old).
- [ ] Rewrite tests from Task 8 to target `RecipeSession` alongside the old characterization tests. Both must pass simultaneously — divergence = bug.
- [ ] Run `dotnet build` + `dotnet test`. All tests green.

#### Task 10: merge `RecipeMutationCoordinator + RecipeStepCoordinator + RecipeQueryService` → `RecipeCoordinator`

**Files:**
- Rename: `SemiStep/SemiStep.UI/Coordinator/RecipeMutationCoordinator.cs` → `RecipeCoordinator.cs`
- Delete: `SemiStep/SemiStep.UI/Coordinator/RecipeStepCoordinator.cs`
- Delete: `SemiStep/SemiStep.Core/Recipes/RecipeQueryService.cs`
- Modify: consumers of `RecipeQueryService` — split per below
- Modify: tests

**Split of `RecipeQueryService` (per plan-review):**

| Method/Property | New home | Reason |
|---|---|---|
| `CurrentRecipe`, `IsDirty`, `CanUndo`, `CanRedo`, `Snapshot` | `RecipeSession` (already there) | Recipe state |
| `IsConnected`, `IsRecipeActive`, `IsSyncEnabled`, `ExecutionState` | `RecipeCoordinator` (PLC facade) | PLC state |
| `GetDefaultActionId` | `RecipeCoordinator` (or move to `RecipeMetadataRegistry` if pure metadata) | Metadata query |
| `SerializeStepsForClipboard`, `DeserializeStepsFromClipboard` | `ClipboardViewModel` (consumer of `ClipboardSerializer` directly) | Clipboard concern |

- [ ] Inline each of the 11 `RecipeStepCoordinator` methods into the equivalent `RecipeCoordinator` method. Extract one private helper `Track<T>(Result<T> result, Func<T, MutationSignal>, Func<T, int?> suggestedSelection)` invoked from each method.
- [ ] Rename class `RecipeMutationCoordinator` → `RecipeCoordinator`. Update all consumer references.
- [ ] Distribute RecipeQueryService methods per the table above. Update consumers.
- [ ] Delete `RecipeStepCoordinator.cs` and `RecipeQueryService.cs`.
- [ ] DI registration updates: drop StepCoordinator, drop QueryService, rename MutationCoordinator → Coordinator.
- [ ] Tests: rewrite tests that targeted the old classes against the new `RecipeCoordinator` surface. Same assertions, new class name and method paths.
- [ ] Run `dotnet build` + `dotnet test`. All tests green.

#### Task 11: delete `RecipeWorkspace`, `RecipeStateManager`, `RecipeEditor`, `RecipeHistoryManager`

**Files:**
- Delete: `SemiStep/SemiStep.Core/Recipes/RecipeWorkspace.cs`
- Delete: `SemiStep/SemiStep.Core/Recipes/State/RecipeStateManager.cs`
- Delete: `SemiStep/SemiStep.Core/Recipes/State/RecipeHistoryManager.cs` (if separate file; inline state is in Session now)
- Delete: `SemiStep/SemiStep.Core/Recipes/RecipeEditor.cs`
- Modify: any remaining consumer

- [ ] Audit grep — find all references to the four classes. Update each to use `RecipeSession`.
- [ ] Delete the four files.
- [ ] Update DI extensions: remove old registrations, keep `RecipeSession` only.
- [ ] Delete the OLD characterization tests from Task 8 (the same tests against RecipeSession from Task 9 remain).
- [ ] Run `dotnet build` + `dotnet test`. All green.

#### Task 12: collapse `ObserveOn` hops on PLC channels in coordinator

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/Plc/PlcMonitorViewModel.cs`

- [ ] Verify that `PlcLifecycleManager` emits on a serialized stream (single producer per channel; check the underlying Subject usage). If yes:
  - `RecipeCoordinator` exposes PLC observables with `ObserveOn(MainThreadScheduler)` applied AT THE SOURCE (one `Publish().RefCount()` per channel).
  - Subscribers (GridVM, MainWindowVM, PlcMonitorVM) drop their redundant `ObserveOn` hops.
- [ ] If no (PlcLifecycleManager emits from multiple producers):
  - Keep `ObserveOn` at each subscription site. Document the constraint in a code comment.
- [ ] Run `dotnet build` + `dotnet test`.

### Phase 4: Replace `Subject<MutationSignal>` with `IRecipeSink` direct calls (2 tasks)

#### Task 13: define `IRecipeSink` interface

**Files:**
- Create: `SemiStep/SemiStep.UI/Coordinator/IRecipeSink.cs`
- Modify: `SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs` (drop `MetadataChanged` variant)

- [ ] Create `IRecipeSink` with one method: `void OnMutation(MutationSignal signal)`. No generation tag — synchronous calls make it unnecessary.
- [ ] Update `MutationSignal` discriminated union — drop `MetadataChanged` (handled by `break;` everywhere; emitting it is dead).
- [ ] Drop the one call site that emits `MetadataChanged` (currently in `RecipeMutationCoordinator.SaveRecipeAsync` ~line 296; verify in the new `RecipeCoordinator`).
- [ ] Run `dotnet build`.

#### Task 14: wire `RecipeGridViewModel` as `IRecipeSink` via Attach pattern

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — implement `IRecipeSink`
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — add `Attach(IRecipeSink)` method
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs` `InitializeServices` — wire after DI resolution

- [ ] `RecipeGridViewModel` implements `IRecipeSink`. The `OnMutation` method is the body of the current `OnStateChange` switch (the bounds-checked version from Task 2).
- [ ] `RecipeCoordinator.Attach(IRecipeSink sink)` stores the sink. Mutations call `_sink?.OnMutation(signal)` synchronously. Before Attach, signals are no-ops.
- [ ] `RecipeCoordinator` no longer exposes `StateChanged` observable. Drop the `_stateChanged` Subject. Drop `ObserveOn(MainThreadScheduler)` on the state channel.
- [ ] In `App.InitializeServices`, after DI resolution, call `coordinator.Attach(gridViewModel)`.
- [ ] `RecipeGridViewModel.ctor` no longer subscribes to `coordinator.StateChanged` — the channel doesn't exist anymore. Drop the corresponding `_disposables` line.
- [ ] Add an `Avalonia.Threading.Dispatcher.UIThread.VerifyAccess()` at the top of `OnMutation` as a release-time tripwire (or `Debug.Assert(Dispatcher.UIThread.CheckAccess())`).
- [ ] Update tests: tests that subscribed to `coordinator.StateChanged` now invoke `sink.OnMutation` directly or assert on grid state after the mutation.
- [ ] Run `dotnet build` + `dotnet test`.

### Phase 5: Dead code cleanup (2 tasks)

#### Task 15: delete `Formulas/` subsystem

**Files:**
- Delete: entire `SemiStep/SemiStep.Core/Recipes/Formulas/` directory
- Possibly delete: corresponding tests in `SemiStep.Tests/Core/Formulas/`
- Modify: DI registration in `RecipeDi.cs` if `FormulaEngine` etc. are registered
- Verify: `SemiStep.Core.csproj` uses SDK-style wildcards (no explicit `<Compile Include>` to update — verified by plan-review; reconfirm before deletion)

- [ ] Grep audit: find every reference to `FormulaEngine`, `CompiledFormula`, `FormulaApplicationCoordinator`, `StepVariableAdapter`, `FormulaDefinition`. Plan-review found these only in `Formulas/` itself, `RecipeDi.cs`, and old plan docs — production code passes `formulaDefinition: null` everywhere.
- [ ] Delete the directory and corresponding tests.
- [ ] Remove DI registrations.
- [ ] Run `dotnet build` + `dotnet test`. All green.

#### Task 16: delete `MutationSignal.MetadataChanged` + unreachable switch defaults

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs` (already done in Task 13; verify)
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (already done in Task 13; verify)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` (drop the `MetadataChanged` case in `OnMutation` switch and the `default:` case)

- [ ] If `MetadataChanged` was already dropped in Task 13, this task verifies and cleans up any lingering references in the switch statement.
- [ ] Drop `default: FullRebuild` from `OnMutation` — unreachable since switch is exhaustive over the discriminated union (C# compiler enforces this).
- [ ] Run `dotnet build` + `dotnet test`. All green.

### Phase 6: Structured logging (1 task)

#### Task 17: add `ILogger` statements at key boundaries

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs` — log mutation entry, undo/redo, reset, load
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — log file ops, PLC sync state changes, signal dispatch
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — log signal arrival (Information), defensive bounds drops (Warning)

- [ ] Use existing `ILogger<T>` pattern. Inject loggers via constructor.
- [ ] Log levels (per plan-review YAGNI on logging breadth):
  - `Information`: recipe load/save/reset/undo/redo, action changes (one per user gesture), PLC sync enable/disable, mutation dispatch with kind+stepIndex+stepCount.
  - `Warning`: defensive bounds-check failures, stale signal drops, error window dispatch.
  - `Error`: unexpected exceptions in async paths.
  - **No Debug-level per-cell logging.** Per plan-review: would be spam even at Debug.
- [ ] Structured logs: include `stepIndex`, `actionId`, `recipeStepCount` as structured fields, not interpolated strings.
- [ ] Test the log volume — load a recipe, paste 50 rows, scroll for 10s. Inspect `logs/` output: signal dispatch should fire once per actual user mutation, not once per cell virtualization event. If spam appears, the equal-value guard from Task 1 has gaps.
- [ ] Run `dotnet build` + `dotnet test`.

### Phase 7: Verify acceptance (1 task)

#### Task 18: full validation

- [ ] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — all green.
- [ ] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` — clean.
- [ ] `git diff master..HEAD --stat` review — scope confined to the planned files. No incidental edits.
- [ ] Manual UI smoke per Testing Strategy section (11 scenarios). Establish baselines B1 (gen-0/sec) and B2 (working set MB) BEFORE Phase 1 (commit them to `Docs/plans/work/round-9-baseline.md` if a perf comparison is desired). Compare post-refactor measurements to baselines for scenario 9.
- [ ] Document any deviation as `⚠️` in this file.

### Phase 8: Archive plan + Round-9 docs (1 task)

#### Task 19: archive plan + Round-9 docs + Round-10 seed

**Files:**
- Move: `Docs/plans/20260514-recipe-stack-simplification.md` → `Docs/plans/completed/`
- Modify: `Docs/07-non-functional.md` — Round-9 subsection
- Create: `Docs/plans/yyyymmdd-cell-templates-to-xaml.md` (Round-10 seed, optional — only if user wants to keep the next round visible)

- [ ] `git mv` the plan file to `completed/`.
- [ ] Append a Round-9 subsection to `Docs/07-non-functional.md` after Round-8 covering:
  - Diagnosis: 5-subagent architecture audit, root-cause summary.
  - State layer flattening: 7 classes → 2 (`RecipeSession` + `RecipeCoordinator`).
  - Signal channel: `Subject<MutationSignal> + ObserveOn` → synchronous `IRecipeSink.OnMutation` via Attach pattern.
  - Stability fixes: equal-value guard, defensive bounds, selection wiring single-writer, error window via `Program.Main` restructure.
  - Recycling rollback: `supportsRecycling: false` on all Round-8-flipped templates; factory-level group items cache reinstated.
  - Dead code: `Formulas/` subsystem deleted; `MutationSignal.MetadataChanged` removed.
  - Logging: structured `ILogger` at recipe lifecycle and grid signal boundaries.
  - Round-10 deferred work clearly noted.
- [ ] Decide whether to commit a Round-10 seed plan file now or wait. Recommend wait — write Round-10 plan after Round-9 lands and the binding seam is the only major surface remaining.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): code, tests, doc edits. All within this codebase.
- **Post-Completion** (no checkboxes): manual UI smoke, performance baseline measurement, decision whether to start Round-10 immediately.

## Post-Completion

**Manual verification** (required before PR open):
- 11-scenario smoke listed in Testing Strategy. Run against a real recipe with ≥100 steps.
- Scenario 9 perf comparison against baselines B1 and B2 (gen-0/sec target: -30% or better; working set: stable within ±5%).

**Round-10 follow-up (recorded for next round):**

The structural fix for the binding-seam class of bugs (Rounds 4-8 churn) is to move imperative `FuncDataTemplate<T>` cell templates to XAML `<DataTemplate>` resources with compiled bindings (`x:CompileBindings="True"`, `x:DataType="vm:RecipeRowViewModel"`).

This eliminates:
- The per-cell binding weight (6 bindings + ContentControl wrapper today)
- The `supportsRecycling` tension that Round-9 rolled back at allocation cost
- The TwoWay-writeback-on-DataContext-swap hazard structurally (Avalonia owns recycling internally; compiled bindings don't fire spurious source writes during cell reuse)
- The imperative template factories (`ComboBoxCellFactory`, `TextCellFactory`, `ColumnBuilder` rewrite + delete `CellPresenter`)

Estimated effort: ~1 week. Reduces per-cell binding weight from ~6 to ~2. Single Round-10 plan in `Docs/plans/yyyymmdd-cell-templates-to-xaml.md` to be created after Round-9 lands.

**External system updates:**
- None. Internal refactor only.
