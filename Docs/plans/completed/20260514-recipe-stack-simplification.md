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

- [x] In `SetPropertyValue`, before firing `ActionChanged` for the action column: read current `_step.ActionKey`; skip if `int.TryParse(value) == _step.ActionKey`.
- [x] Before firing `PropertyValueChanged` for non-action columns: read current value via `GetPropertyValue(columnKey)`; skip if string-equal (ordinal).
- [x] Add tests: `SetPropertyValue_ActionWithSameId_DoesNotFireEvent`, `SetPropertyValue_PropertyWithSameValue_DoesNotFirePropertyChanged`, `SetPropertyValue_ActionWithDifferentId_FiresEvent` (positive control), `SetPropertyValue_PropertyWithDifferentValue_FiresPropertyChanged` (positive control).
- [x] Run `dotnet build` + `dotnet test`.

#### Task 2: defensive bounds in `RecipeGridViewModel.OnStateChange` handlers

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/Di/` extension where the grid VM is registered (add `ILogger<RecipeGridViewModel>` injection if not already there)
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`

- [x] In `UpdateSingleRowInPlace`, `RebuildRow`, `AppendRow`, `InsertRows`: validate `stepIndex < recipe.StepCount` and `< RecipeRows.Count` (where applicable) before indexing. Out-of-range → log warning via `ILogger`, return.
- [x] Inject `ILogger<RecipeGridViewModel>` via constructor if not already present.
- [x] Add test: simulating a stale signal (manually invoke OnStateChange with out-of-range index) → no exception, warning logged.
- [x] Run `dotnet build` + `dotnet test`.

#### Task 3: collapse dual selection wiring

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (drop `SelectedIndex` binding)
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` (audit `OnSelectionChanged`)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` (`SelectedRowIndex` becomes computed)
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs` (paste's `SuggestedSelection` consumption)
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` (load's `SuggestedSelection` consumption)
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeCommandsViewModel.cs` (insert's `SuggestedSelection` consumption)
- Modify tests to match.

- [x] Audit all `_coordinator.ConsumeSuggestedSelection()` call sites. In-app: only `RecipeGridViewModel.ApplyPostMutationUpdates`. Tests: many sites in `RecipeMutationCoordinatorTests.cs`. (Plan note about `ClipboardViewModel`/`RecipeCommandsViewModel`/`RecipeFileViewModel` reflected the post-refactor design — they did not call `ConsumeSuggestedSelection` directly before the change. They now consume the return value instead.)
- [x] Remove `SelectedIndex="{Binding RecipeGrid.SelectedRowIndex}"` from MainWindow.axaml:31.
- [x] Change `SelectedRowIndex` in RecipeGridViewModel to a computed property derived from `SelectedRowIndices`. Remove its setter.
- [x] Change mutation methods that previously set `SuggestedSelection` side-channel to return `Result<int?>` (AppendStep, InsertStep, RemoveStep, RemoveSteps, InsertSteps, ChangeStepAction). Methods that previously set `null` (UpdateStepProperty, Undo, Redo, NewRecipe, Load/Save/PLC ops) keep `Result`. Callers (`RecipeCommandsViewModel`, `ClipboardViewModel`, in-grid `OnActionChanged`) consume the return value and call `RecipeGridViewModel.RequestSelection(idx)`, which raises a `SelectionRequested` event subscribed by code-behind that writes `DataGrid.SelectedIndex`.
- [x] Verify `OnSelectionChanged` handler in MainWindow.axaml.cs is the only writer of `SelectedRowIndices`.
- [x] Add test: paste / insert / append round-trip — coordinator return value carries the suggested index; new tests in `RecipeMutationCoordinatorTests.cs` (`*_ReturnsSuggestedSelection_*`). VM-level `SelectionRequested` event covered by `RequestSelection_RaisesSelectionRequestedEvent` in `RecipeGridViewModelTests.cs`.
- [x] Run `dotnet build` + `dotnet test`.

#### Task 4: fix `App.RunErrorWindow` by restructuring `Program.Main`

**Files:**
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/Program.cs`

- [x] Audit `Program.Main`. Currently calls `App.Run(serviceProvider)` and if that fails, calls `App.RunErrorWindow(errors)` — but `Run` already ran `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)`, calling Setup. The fallback `RunErrorWindow` runs Setup again → crash.
- [x] Restructure: detect startup errors via a `StartupValidator` (or inline in `Program.Main`) BEFORE any `BuildAvaloniaApp()` call. If errors found, pass them to `App.RunErrorWindow` — never call `App.Run`. If no errors, call `App.Run` — never call `App.RunErrorWindow`. (Inlined as `ValidateStartup()` helper; introduced typed `StartupOutcome` record struct to make the Provider-XOR-Errors invariant explicit. Removed the dangerous catch-fallback that re-invoked `BuildAvaloniaApp()` after `App.Run` had already initialised Avalonia.)
- [x] `App.RunErrorWindow` and `App.Run` each call `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)` exactly once per process.
- [x] manual test (skipped - not automatable) Verify by manually triggering a startup error: rename `ConfigFiles/`, launch app, confirm `ErrorWindow` displays.
- [x] No new unit tests (manual smoke verifies). Run `dotnet build` + `dotnet test`.

### Phase 2: Recycling rollback for all affected templates (3 tasks)

Phantom-mutation hazard from Round-8's `supportsRecycling: true` is structurally addressed only by full XAML migration (Round-10). For now, roll back to false on ALL templates that were flipped in Round-8.

#### Task 5: enumerate `supportsRecycling: true` templates

**Files:**
- Read: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Read: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs`
- Read: any other `*CellFactory.cs` or template-building location

- [x] Grep `supportsRecycling` across the UI project. Document every occurrence in a checklist comment in this task (or as a `➕` discovery item). Plan-review found ComboBox + Text; verify there are no others.
- [x] No code changes in this task — discovery only. Tasks 6-7 do the flips.
- [x] Run `dotnet build` (no-op verification).

**Discovery:**

Audit performed via `Grep "supportsRecycling"` across `SemiStep/SemiStep.UI/` (and broader `SemiStep/` to confirm no others). Five occurrences across two files, all under `SemiStep.UI/RecipeGrid/`:

| File | Line | Method / template | Current value | Round-8 flip? | Action for Task 6/7 |
|---|---|---|---|---|---|
| `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` | 77 | `CreateActionCellTemplate` | `true` | Yes (Round-8 plan §"Action display template") | Task 6: flip to `false` |
| `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` | 112 | `CreateGroupCellTemplate` | `true` | Yes (Round-8 plan §"Group display template") | Task 6: flip to `false` |
| `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` | 64 | `CreateStepStartTimeTemplate` | `true` | Pre-Round-8 (pure binding, no `row` closure) | Task 7: see verify note below |
| `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` | 104 | `CreateMultiBindingTemplate` | `true` | Pre-Round-8 (descendant of Round-7 `CreateDisplayTemplate` — "pure MultiBinding, safely true") | Task 7: see verify note below |
| `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` | 137 | `CreateEditingTemplate` | `false` | n/a | Keep `false` (closure captures `row` for `ColumnFormatKinds` lookup) |

No other `*CellFactory.cs` files exist (`Glob "**/*CellFactory.cs"` returned only the two above). No `FuncDataTemplate` usages outside these two factories.

Round-7 archive (`Docs/plans/completed/20260513-round7-perf-and-bumps.md:28`) explicitly documents the Text path as "pure MultiBinding, no closures — safely `true`". However, Round-9 Goals (this plan, line 14) direct: "Roll back `supportsRecycling: true` on **all** cell templates affected by Round-8 (ComboBox + Text), restore the factory-level group-items cache." The plan-review audit referenced at line 58 of this plan asserts the phantom-mutation hazard "applies here too." Decision for Task 7 is therefore: roll back lines 64 and 104 to `false` per the Goals directive, since (a) the Round-9 Goals are unambiguous, (b) Task 7 wording explicitly considers the Round-7 "safely true" exception but is overridden by Goals scope, and (c) the templates contain `OneWay` bindings only — recycling provides marginal benefit while leaving the phantom-mutation seam open for future TwoWay edits. Final call belongs to Task 7 itself; this discovery surfaces the trade-off so the implementer can confirm without re-reading the Round-7 archive.

No code changes performed in this task. Build verified clean (0 warnings, 0 errors).

#### Task 6: flip ComboBox templates to `supportsRecycling: false`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` (drop `GroupItemsByColumn` if no longer needed)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxItemMultiSelectionConverter.cs` (delete if no longer needed)
- Modify: tests in `SemiStep.Tests/UI/`

- [x] Change both `CreateActionCellTemplate` and `CreateGroupCellTemplate` to `supportsRecycling: false`.
- [x] Re-add factory-level `_groupItemsByGroupName` dictionary in `ComboBoxCellFactory`. `InvalidateCaches()` clears it.
- [x] In group template lambda, resolve items list at template-build time via the factory cache (lambda re-captures `row` for `row.GetGroupNameForColumn(columnKey)` — safe because recycling is now `false`).
- [x] Remove `RecipeRowViewModel.GroupItemsByColumn` and re-add `GetGroupNameForColumn` / `GetGroupItemsForColumn` if those were deleted. (Only `GetGroupNameForColumn` re-added — items lookup lives in the factory cache, so `GetGroupItemsForColumn` is unnecessary.)
- [x] Delete `ComboBoxItemMultiSelectionConverter` (no longer needed — single-binding `ComboBoxItemSelectionConverter` works with closure-captured items).
- [x] Update tests in `RecipeRowViewModelTests.cs` and `ComboBoxItemMultiSelectionConverterTests.cs` to match (likely deleting the latter). Deleted `ComboBoxItemMultiSelectionConverterTests.cs`; rewrote four `GroupItemsByColumn_*` tests in `RecipeRowViewModelTests.cs` as `GetGroupNameForColumn_*` tests. Also removed obsolete `ColumnTypes.GroupItemsPath` since no binding references it.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). All 354 tests pass.

#### Task 7: flip Text templates to `supportsRecycling: false`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (verify line numbers from Task 5)
- Modify: tests if applicable

- [x] Flip the `supportsRecycling: true` instances identified in Task 5 to `false`. Both `CreateStepStartTimeTemplate` (line 64) and `CreateMultiBindingTemplate` (line 104) flipped to `false` per Round-9 Goals directive (line 14) — "Roll back `supportsRecycling: true` on **all** cell templates affected by Round-8 (ComboBox + Text)". Audit confirmed neither template captures `row` in its closure: line 64 uses a `OneWay` `Binding` to `StepStartTime` with a pre-built `CellStateConverter`; line 104 uses a `MultiBinding` over converted properties (`ColumnUnits`, `ColumnFormatKinds`) with pre-built converters. The flip is therefore allocation cost only — no closure capture introduced. `CreateEditingTemplate` (line 137) was already `false`. No test updates needed: templates are pure rendering paths with no behavioural assertions in the test suite.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). All 354 tests pass.

### Phase 3: Characterization tests + state-layer collapse (5 tasks)

Highest-risk phase. Characterization tests land BEFORE the merge to lock in semantics.

#### Task 8: characterization tests for Workspace + Editor + StateManager + HistoryManager + QueryService

**Files:**
- Create: `SemiStep/SemiStep.Tests/Core/RecipeBehaviourCharacterizationTests.cs`

- [x] Write tests against the OLD class APIs that pin behaviour soon-to-be-merged. Coverage:
  - Workspace apply mutates current recipe, increments dirty flag.
  - Workspace.Reset clears dirty flag, clears history. (Characterized: Reset clears history but the immediate `analyzer.Analyze(Empty)` + `StateManager.Update` flips IsDirty back on — captured by `Reset_LeavesWorkspaceDirty_BecauseEmptyAnalysisFlipsTheFlag`.)
  - Workspace.MarkSaved clears dirty flag, preserves history.
  - Editor.AppendStep returns the new step index in the result. AppendStep with invalid actionId returns a failed Result.
  - Editor.InsertStep/RemoveStep/RemoveSteps preserve step ordering invariants.
  - Editor.ChangeStepAction does NOT change the action if newActionId == current (no-op result). (Characterized: the current implementation REBUILDS the step from defaults — captured by `ChangeStepAction_SameActionId_RebuildsStepToDefaults`. The plan-described no-op semantic does not exist in the live code; the new RecipeSession in Task 9 should decide whether to introduce the guard or preserve the rebuild.)
  - Editor.UpdateStepProperty validates against the property type definition; failures return a failed Result with a specific reason.
  - HistoryManager.Push records pre-mutation state.
  - HistoryManager.Undo restores the prior state and pushes onto redo stack.
  - HistoryManager.Redo restores the next state.
  - LoadAsCurrent / LoadAsCurrentValidated replace the recipe and clear undo/redo history.
  - QueryService surfaces CurrentRecipe, IsDirty, CanUndo, CanRedo, Snapshot — all reflect Workspace state.
- [x] Tests target the OLD APIs (Workspace.Apply / Editor.AppendStep / HistoryManager.Undo / etc.). After Phase 3 merges these into RecipeSession (Task 9-11), these tests get rewritten to target the new API while preserving the same assertions.
- [x] At least 20 tests covering the above behaviours. Existing `RecipeWorkspaceTests` / `RecipeEditorTests` may already cover some — supplement to gap-fill, do not duplicate. (33 new tests added in `SemiStep.Tests/Core/RecipeBehaviourCharacterizationTests.cs`.)
- [x] Run `dotnet build` + `dotnet test`. All new tests green against the unchanged code. (Test count rose 354 → 387.)

#### Task 9: create `RecipeSession` merging Workspace + StateManager + Editor

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`
- Keep temporarily: `RecipeWorkspace.cs`, `RecipeStateManager.cs`, `RecipeEditor.cs`, `RecipeHistoryManager.cs` (deleted in Task 11)

- [x] Build new class `RecipeSession` containing:
  - State: `Recipe _current`, history stacks (from RecipeHistoryManager — inline or keep as private static helpers).
  - Inlined methods: `Apply`, `Undo`, `Redo`, `Reset`, `LoadAsCurrent`, `LoadAsCurrentValidated`, `MarkSaved`.
  - Inlined mutation methods from Editor: `AppendStep`, `InsertStep`, `RemoveStep`, `RemoveSteps`, `InsertSteps`, `ChangeStepAction`, `UpdateStepProperty`.
  - Public: `Recipe Current`, `bool IsDirty`, `bool CanUndo`, `bool CanRedo`, `Result<RecipeSnapshot> Snapshot`, `Recipe LastValidRecipe`, `bool IsValid`.
  - Mutation methods return `Result<MutationOutcome>` carrying `int? SuggestedSelectionIndex`, replacing the side-channel `SuggestedSelection`. Pre-existing divergences preserved: `Reset` leaves IsDirty=true (analyzer.Analyze(Empty) flips the flag), `ChangeStepAction` with `newActionId == current` rebuilds the step from defaults.
- [x] RecipeHistoryManager is **inlined** into RecipeSession (undo/redo stacks + helpers) — the standalone class remains until Task 11.
- [x] DI registration of new `RecipeSession` added in `RecipeDi.cs` alongside existing `RecipeWorkspace`/`RecipeEditor`/`RecipeStateManager`/`RecipeHistoryManager` (both surfaces live until Task 11 deletes the old). New `MutationOutcome` record introduced in `SemiStep.Core/Recipes/MutationOutcome.cs`.
- [x] Rewrote the Task 8 characterization tests as a parallel suite `SemiStep.Tests/Core/RecipeSessionBehaviourCharacterizationTests.cs` (Option B: duplicate class). Same assertions, RecipeSession surface; the original tests still cover Workspace/Editor. Both suites coexist and pass — Task 11 deletes the old when Workspace/Editor go.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). 426 tests pass (387 → 426 after the +39 new RecipeSession characterization tests).

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

- [x] Inline each of the 11 `RecipeStepCoordinator` methods into the equivalent `RecipeCoordinator` method. Extract one private helper `Track(Result result, Func<MutationSignal> signalFactory, Func<int?> suggestedSelection)` invoked from each mutation method. (Helper signature simplified from `Track<T>(Result<T>, Func<T, MutationSignal>, Func<T, int?>)` because `RecipeEditor` mutation methods return non-generic `Result`; suggested-selection and signal are derived from coordinator state, not from a result value. A second `TrackVoid(Result, MutationSignal)` helper covers `UpdateStepProperty`/`Undo`/`Redo`/`NewRecipe` which return plain `Result`.)
- [x] Rename class `RecipeMutationCoordinator` → `RecipeCoordinator`. Updated all consumer references: `ClipboardViewModel`, `RecipeCommandsViewModel`, `RecipeFileViewModel`, `PlcMonitorViewModel`, `MainWindowViewModel`, `RecipeGridViewModel`, `App.axaml.cs`, `UiDi.cs`, and tests.
- [x] Distributed RecipeQueryService methods per the table: PLC facade members (`IsConnected`, `IsRecipeActive`, `IsSyncEnabled`, `ExecutionState`, `SyncStatus`, `LastSyncTime`) live on `RecipeCoordinator`. Recipe state (`CurrentRecipe`, `IsDirty`, `CanUndo`, `CanRedo`, `Snapshot`) forwarded from the still-present `RecipeWorkspace` (Task 11 will collapse to `RecipeSession`). `GetDefaultActionId` moved to `RecipeCoordinator`. `SerializeStepsForClipboard` and `DeserializeStepsFromClipboard` inlined into `ClipboardViewModel` with direct `ClipboardSerializer` + `ImportedRecipeValidator` injection. `GetCellState` collapsed at its sole call site (`RecipeGridViewModel.CreateRowViewModel`) to a direct `CellStateResolver.GetCellState` static call.
- [x] Deleted `RecipeStepCoordinator.cs` and `RecipeQueryService.cs` via `git rm`.
- [x] DI registration: dropped `RecipeQueryService` and `RecipeStepCoordinator` registrations; registered `RecipeCoordinator` in place of `RecipeMutationCoordinator`.
- [x] Tests rewritten: renamed `RecipeMutationCoordinatorTests.cs` → `RecipeCoordinatorTests.cs` and `RecipeMutationCoordinatorLoadRecipeTests.cs` → `RecipeCoordinatorLoadRecipeTests.cs` via `git mv`; updated class names, ctor signatures, and `UIFixture` to target `RecipeCoordinator`. Same assertions throughout.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). 426 tests pass.

#### Task 11: delete `RecipeWorkspace`, `RecipeStateManager`, `RecipeEditor`, `RecipeHistoryManager` [COMPLETED 2026-05-14, commit e6daffd]

**Files:**
- Delete: `SemiStep/SemiStep.Core/Recipes/RecipeWorkspace.cs`
- Delete: `SemiStep/SemiStep.Core/Recipes/State/RecipeStateManager.cs`
- Delete: `SemiStep/SemiStep.Core/Recipes/State/RecipeHistoryManager.cs` (if separate file; inline state is in Session now)
- Delete: `SemiStep/SemiStep.Core/Recipes/RecipeEditor.cs`
- Modify: any remaining consumer

- [x] Audit grep — find all references to the four classes. Update each to use `RecipeSession`. Migration touched: `RecipeCoordinator` (now consumes `RecipeSession` directly; `Track` helper takes `Result<MutationOutcome>` and signal factory; new `TrackVoid(Result<MutationOutcome>, MutationSignal)` overload added for `UpdateStepProperty`), `PlcLifecycleManager` (workspace → session), `App.InitializeServices` (workspace.Reset → session.Reset), `UIFixture`/`CoreFixture`/`CoreTestHelper`/`CsvTestHelper`/`RecipeTestDriver` (all now expose `RecipeSession`; `RecipeTestDriver` takes a single ctor arg), `RecipeRowViewModelTests`, `PlcLifecycleManagerReconnectTests`, `RecipeCoordinatorLoadRecipeTests`, and every Core integration test (Validity, Timings, Snapshot, Mutation, MutationEdgeCases, GroupValidation, Loops, LoopEdgeCases).
- [x] Delete the four files (`RecipeWorkspace.cs`, `RecipeEditor.cs`, `State/RecipeStateManager.cs`, `State/RecipeHistoryManager.cs`). The `State/` directory removed automatically when empty.
- [x] Update DI extensions: removed `RecipeWorkspace`/`RecipeEditor`/`RecipeStateManager`/`RecipeHistoryManager` from `RecipeDi`, kept `RecipeSession` only. `UiDi` already referenced `RecipeCoordinator` only.
- [x] Delete the OLD characterization tests from Task 8 (`SemiStep.Tests/Core/RecipeBehaviourCharacterizationTests.cs`); the parallel `RecipeSessionBehaviourCharacterizationTests.cs` remains as the safety net. `CoreTestHelper.BuildSessionAsync` removed (it was a redundant alias for `BuildAsync` once the latter returns `RecipeSession`); its single caller updated to `BuildAsync`.
- [x] Run `dotnet build` + `dotnet test` + `dotnet format`. Build clean (0/0). 393 tests pass (426 → 393 after deleting the 33 OLD characterization tests).

#### Task 12: collapse `ObserveOn` hops on PLC channels in coordinator

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/Plc/PlcMonitorViewModel.cs`

- [x] Verified `PlcLifecycleManager` emits PLC observables on serialized streams. `ExecutionState` flows from `PlcExecutionMonitor._subject` (single `Subject<PlcExecutionInfo>`, fed by a single `PollLoopAsync` task plus `Stop*` paths that await the poll task before publishing). `PlcState` flows from `PlcSyncCoordinator._subject` (single `BehaviorSubject` with all `OnNext` calls already gated under `_lock`). `PlcRecipeConflictDetected` is a single C# event raised only from `PerformReconnectReconciliationAsync` (background task continuation). All three are single-producer-per-channel.
- [x] Applied `ObserveOn(MainThreadScheduler).Publish().RefCount()` at `RecipeCoordinator` for the three PLC channels (`ExecutionState`, `PlcStateChanged`, `PlcRecipeConflictDetected`). Each channel hops to the UI thread exactly once at the coordinator and is shared across subscribers via a single connection.
- [x] Dropped redundant `ObserveOn(MainThreadScheduler)` calls at subscriber sites: `RecipeGridViewModel` (2 sites on `ExecutionState`), `PlcMonitorViewModel` (1 site on `ExecutionState`), `MainWindowViewModel` (1 site on `PlcStateChanged`, 1 site on `PlcRecipeConflictDetected`). The `StateChanged` (mutation) channel keeps its subscriber-side `ObserveOn` for now — Task 14 replaces it with synchronous `IRecipeSink` calls.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). All 393 tests pass.

### Phase 4: Replace `Subject<MutationSignal>` with `IRecipeSink` direct calls (2 tasks)

#### Task 13: define `IRecipeSink` interface

**Files:**
- Create: `SemiStep/SemiStep.UI/Coordinator/IRecipeSink.cs`
- Modify: `SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs` (drop `MetadataChanged` variant)

- [x] Create `IRecipeSink` with one method: `void OnMutation(MutationSignal signal)`. No generation tag — synchronous calls make it unnecessary.
- [x] Update `MutationSignal` discriminated union — drop `MetadataChanged` (handled by `break;` everywhere; emitting it is dead).
- [x] Drop the one call site that emits `MetadataChanged` (currently in `RecipeMutationCoordinator.SaveRecipeAsync` ~line 296; verify in the new `RecipeCoordinator`). Located at `RecipeCoordinator.SaveRecipeAsync` (was line 311); dropped together with the now-unreachable `MutationSignal.MetadataChanged` case in `RecipeGridViewModel.OnStateChange` and the `ApplyPostMutationUpdates` indirection (which existed solely to skip `RefreshStepStartTimes()` for `MetadataChanged`). `RefreshStepStartTimes()` now called unconditionally — save no longer dispatches a signal at all. Task 16 will verify nothing else needs cleanup after Task 14 lands.
- [x] Run `dotnet build`. Clean (0 errors, 0 warnings). Tests 393 pass, format clean.

#### Task 14: wire `RecipeGridViewModel` as `IRecipeSink` via Attach pattern [COMPLETED 2026-05-14, commit f5132ef]

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — implement `IRecipeSink`
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — add `Attach(IRecipeSink)` method
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs` `InitializeServices` — wire after DI resolution

- [x] `RecipeGridViewModel` implements `IRecipeSink`. `OnStateChange` renamed to `OnMutation` (the body is the bounds-checked switch from Task 2) and made public to satisfy the interface.
- [x] `RecipeCoordinator.Attach(IRecipeSink sink)` stores the sink. All `_stateChanged.OnNext(...)` call sites replaced with a single private `DispatchMutation(signal)` helper that invokes `_sink?.OnMutation(signal)` synchronously. Before Attach the sink call is a no-op.
- [x] `RecipeCoordinator` no longer exposes `StateChanged` observable. `_stateChanged` Subject dropped along with its disposal and the `ObserveOn(MainThreadScheduler)` subscriber hop in `RecipeGridViewModel`. The coordinator now also exposes a plain C# `event Action? Mutated` for non-grid consumers (`MainWindowViewModel`, `RecipeCommandsViewModel`) that previously observed `StateChanged` only to refresh property/command CanExecute state — the event fires synchronously after the sink dispatch, no Rx ObserveOn, so it shares the UI-thread guarantee provided by `OnMutation`.
- [x] In `App.InitializeServices`, after `coordinator.Initialize()`, resolve `RecipeGridViewModel` from DI and call `coordinator.Attach(gridViewModel)`.
- [x] `RecipeGridViewModel.ctor` no longer subscribes to `coordinator.StateChanged` — the channel doesn't exist anymore. Subscription block deleted.
- [x] Added `Avalonia.Threading.Dispatcher.UIThread.VerifyAccess()` at the top of `OnMutation` as a release-time tripwire.
- [x] Tests updated: introduced `SemiStep.Tests/UI/Helpers/RecordingRecipeSink.cs` (an `IRecipeSink` test double that records signals). `RecipeCoordinatorTests` and `RecipeCoordinatorLoadRecipeTests` now attach a `RecordingRecipeSink` instead of subscribing to `StateChanged`. `RecipeGridViewModelTests` now attaches the grid as the sink in `InitializeAsync` and calls `_grid.OnMutation(...)` (renamed from `OnStateChange`). Test method names with `OnStateChange_` prefix renamed to `OnMutation_`.
- [x] Run `dotnet build` + `dotnet test` + `dotnet format`. Build clean (0/0). All 393 tests pass.

### Phase 5: Dead code cleanup (2 tasks)

#### Task 15: delete `Formulas/` subsystem

**Files:**
- Delete: entire `SemiStep/SemiStep.Core/Recipes/Formulas/` directory
- Possibly delete: corresponding tests in `SemiStep.Tests/Core/Formulas/`
- Modify: DI registration in `RecipeDi.cs` if `FormulaEngine` etc. are registered
- Verify: `SemiStep.Core.csproj` uses SDK-style wildcards (no explicit `<Compile Include>` to update — verified by plan-review; reconfirm before deletion)

- [x] Grep audit: confirmed the only production reference was `RecipeSession.UpdateStepProperty` (line 381 pre-change), which passed `formulaDefinition: null` — the entire branch was unreachable. No tests under `SemiStep.Tests/Core/Formulas/` existed. References were limited to the six files under `Formulas/`, the `RecipeDi.cs` registration block, the single call site in `RecipeSession.cs`, and old plan documents.
- [x] Deleted the directory (six files): `CompiledFormula.cs`, `FormulaApplicationCoordinator.cs`, `FormulaDefinition.cs`, `FormulaEngine.cs`, `StepAdapterResult.cs`, `StepVariableAdapter.cs`. No test files to delete. The `Formulas/` directory was removed automatically when empty.
- [x] Removed DI registrations from `RecipeDi.cs` (the `CompiledFormula` dictionary, `FormulaEngine`, `FormulaApplicationCoordinator`) and dropped the `using SemiStep.Core.Recipes.Formulas;`. Removed `_formulaCoordinator` field and ctor parameter from `RecipeSession` together with the `using`. Inlined `UpdateStepProperty` to use `updatedStep` directly instead of routing through `FormulaApplicationCoordinator.ApplyIfExists` with `formulaDefinition: null`.
- [x] Run `dotnet build` + `dotnet test`. Build clean (0/0). All 393 tests pass. `dotnet format --verify-no-changes` clean.

#### Task 16: delete `MutationSignal.MetadataChanged` + unreachable switch defaults

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/MutationSignal.cs` (already done in Task 13; verify)
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (already done in Task 13; verify)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` (drop the `MetadataChanged` case in `OnMutation` switch and the `default:` case)

- [x] Verified `MetadataChanged` was already dropped in Task 13. `MutationSignal.cs` no longer declares the variant, no consumer references it, and the only remaining mentions are in plan documents. No code references in the UI or test surfaces.
- [x] Dropped `default: FullRebuild` from `RecipeGridViewModel.OnMutation` (lines 186-188 pre-change). The switch now exits after the explicit `RecipeReplaced` case. All seven `MutationSignal` variants (`StepAppended`, `StepsInserted`, `StepRemoved`, `StepsRemoved`, `StepActionChanged`, `PropertyUpdated`, `RecipeReplaced`) are handled explicitly. No other unreachable handlers found in the audit (other `default:` matches in `RecipeConverter.cs` and `CsvMetadata.cs` parse external/unknown input — legitimate, not over a discriminated union).
- [x] Run `dotnet build` + `dotnet test` + `dotnet format`. Build clean (0/0). All 393 tests pass. Format clean.

### Phase 6: Structured logging (1 task)

#### Task 17: add `ILogger` statements at key boundaries

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs` — log mutation entry, undo/redo, reset, load
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` — log file ops, PLC sync state changes, signal dispatch
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs` — log signal arrival (Information), defensive bounds drops (Warning)

- [x] Use existing `ILogger<T>` pattern. Inject loggers via constructor. All three components already had loggers injected from earlier tasks (Task 2 added `ILogger<RecipeGridViewModel>`, Task 9 added `ILogger<RecipeSession>`, Task 10 added `ILogger<RecipeCoordinator>`). This task adds the log statements only.
- [x] Log levels (per plan-review YAGNI on logging breadth):
  - `Information`: recipe load/save/reset/undo/redo (RecipeSession), mutation entry per public mutation method on RecipeSession (AppendStep, InsertStep, RemoveStep, RemoveSteps, InsertSteps, ChangeStepAction, UpdateStepProperty), file ops on RecipeCoordinator (LoadRecipeAsync, SaveRecipeAsync, LoadRecipeFromPlcAsync), PLC sync enable/disable on RecipeCoordinator, mutation dispatch on RecipeCoordinator (one per signal kind), signal arrival on RecipeGridViewModel (one per signal kind), PLC state change on RecipeCoordinator (one per real transition — backed by BehaviorSubject at source, not per-poll-tick).
  - `Warning`: defensive bounds-check drops in RecipeGridViewModel (already added in Task 2), failed file/PLC operations in RecipeCoordinator, failed recipe analysis in RecipeSession.LoadAsCurrent, failed PLC state results in RecipeCoordinator.OnPlcStateChanged.
  - `Error`: already present at `RecipeCoordinator.SaveRecipeAsync` for save failure (kept). No try-catch wrappers added to async methods — adding them would change exception-handling semantics; exceptions still propagate up to existing top-level handlers.
  - **No Debug-level per-cell logging.** Confirmed: no log statement is reachable per cell-binding-refresh event. The closest per-edit log is `Mutation entry: UpdateStepProperty`, which fires once per user-committed value (RowVM equal-value guard from Task 1 short-circuits unchanged writes before they reach the session).
- [x] Structured logs: all messages use named placeholders (`{StepIndex}`, `{ActionId}`, `{StepCount}`, `{ColumnKey}`, `{FilePath}`, `{Kind}`, `{ConnectionState}`, `{SyncStatus}`, `{IsSyncEnabled}`, `{KeepLocal}`, `{Errors}`) — no string interpolation in any log call.
- [x] Skipped per plan note: "Skip the 'test the log volume' smoke checkbox — manual smoke is Phase 7 / not automatable." Phase 7 (Task 18) covers the manual smoke verification.
- [x] Run `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes`. Build clean (0/0). All 393 tests pass. Format clean.

### Phase 7: Verify acceptance (1 task)

#### Task 18: full validation

- [x] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — all green (393/393 passed).
- [x] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` — clean (exit 0).
- [x] `git diff master..HEAD --stat` review — scope confined to the planned files. Round-9 deltas live under `SemiStep/SemiStep.Core/Recipes/`, `SemiStep/SemiStep.UI/`, `SemiStep/SemiStep.Tests/`, `Docs/plans/`, `Docs/07-non-functional.md`. Incidental tooling/config edits visible in the stat (`.zed/`, `.github/instructions/`, `CLAUDE.md`, `SemiStep/Directory.Packages.props`, csproj files, minor whitespace in `.axaml`) are carry-over from earlier merged commits on the branch (Round-7 dependency bumps, Avalonia 12 migration, ReactiveUI-Avalonia migration) — not introduced by Round-9 tasks.
- [x] manual test (skipped - not automatable; pre-PR gate) Manual UI smoke per Testing Strategy section (11 scenarios). Establish baselines B1 (gen-0/sec) and B2 (working set MB) BEFORE Phase 1 (commit them to `Docs/plans/work/round-9-baseline.md` if a perf comparison is desired). Compare post-refactor measurements to baselines for scenario 9.
- [x] Document any deviation as `⚠️` in this file. No deviations to document; all four automated checks green.

### Phase 8: Archive plan + Round-9 docs (1 task)

#### Task 19: archive plan + Round-9 docs + Round-10 seed

**Files:**
- Move: `Docs/plans/20260514-recipe-stack-simplification.md` → `Docs/plans/completed/`
- Modify: `Docs/07-non-functional.md` — Round-9 subsection
- Create: `Docs/plans/yyyymmdd-cell-templates-to-xaml.md` (Round-10 seed, optional — only if user wants to keep the next round visible)

- [x] `git mv` the plan file to `completed/`.
- [x] Append a Round-9 subsection to `Docs/07-non-functional.md` after Round-8 covering:
  - Diagnosis: 5-subagent architecture audit, root-cause summary.
  - State layer flattening: 7 classes → 2 (`RecipeSession` + `RecipeCoordinator`).
  - Signal channel: `Subject<MutationSignal> + ObserveOn` → synchronous `IRecipeSink.OnMutation` via Attach pattern.
  - Stability fixes: equal-value guard, defensive bounds, selection wiring single-writer, error window via `Program.Main` restructure.
  - Recycling rollback: `supportsRecycling: false` on all Round-8-flipped templates; factory-level group items cache reinstated.
  - Dead code: `Formulas/` subsystem deleted; `MutationSignal.MetadataChanged` removed.
  - Logging: structured `ILogger` at recipe lifecycle and grid signal boundaries.
  - Round-10 deferred work clearly noted.
- [x] Decide whether to commit a Round-10 seed plan file now or wait. Decision: wait — Round-10 plan to be written after Round-9 lands on master.

**Task 19 completed 2026-05-14, commit 8824254. Plan archived; Round-9 documented in `Docs/07-non-functional.md`; Round-10 seed deferred. Round-9 fully landed.**

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
