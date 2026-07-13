# Recipe Grid Surface

## Overview

The recipe grid is consumed through the abstraction `IRecipeGridSurface`
(`SemiStep.UI/RecipeGrid/IRecipeGridSurface.cs`). Two concrete surface + view pairs exist —
canonical (rows = steps, columns = parameters) and transposed (rows = parameters,
columns = steps) — plus a delegating router, `ActiveRecipeGridSurface`, that owns the
orientation choice. Consumers hold one `IRecipeGridSurface` (the router) and survive
orientation flips without re-wiring; the interface itself is orientation-free, and the
concrete surfaces know nothing about switching.

The pieces:

- `IRecipeGridSurface` — the view-facing API. Selection is expressed in step indices only.
- `CanonicalRecipeGridSurface` — the canonical-orientation surface, a thin derivation of
  `RecipeGridSurfaceBase<RecipeRowViewModel>` (see "Shared surface base class" below). It keeps
  the projection alias (`RecipeRows`), the two item hooks, and the canonical-only member
  (`ColumnBuilder`). `ColumnBuilder` is
  constructor-injected into the surface solely so the view can reach it through `DataContext` —
  a trade-off originally recorded as "revisit when the transposed view lands"; the transposed
  view landed and adopted the same idiom (`TransposedRecipeGridSurface` carries
  `GridStyleOptions` as its public `GridStyle` property, the transposed view's style carrier),
  so surface-carried view dependencies reached through `DataContext` are the settled pattern
  for both orientations, not a service locator workaround.
- `CanonicalRecipeGridView` (`ReactiveUserControl<CanonicalRecipeGridSurface>`) — wraps the
  `DataGrid`, owns its event handlers (`BeginningEdit`, `CellEditEnded`, `SelectionChanged`,
  `CellPointerPressed`, `LoadingRow`), column building, and the changed-cell click-away state.
- `TransposedRecipeGridSurface` (`RecipeGrid/Transposed/`) — the transposed peer, an equally
  thin derivation of `RecipeGridSurfaceBase<StepColumnViewModel>`. It keeps the projection
  alias (`StepColumns`, one item per step), `ParameterDescriptors` (the frozen name-column
  rows, built in canonical registry order), and `GridStyle`. Each step-column wraps a reused,
  orientation-agnostic `RecipeRowViewModel`; cells are thin `ParameterCellViewModel` adapters
  over it, so changed-cell state, applicability, and the three write events have exactly one
  home.
- `TransposedRecipeGridView` (`ReactiveUserControl<TransposedRecipeGridSurface>`) — a `ListBox`
  of step-columns over a horizontal `VirtualizingStackPanel` (no DataGrid): realized element
  count is viewport-bound regardless of recipe length, and whole-column selection comes from
  `SelectionMode="Multiple"` natively. Cell templates are built in code by
  `TransposedCellTemplateFactory` (a per-cell format kind is baked into each editing converter);
  `TransposedStepColumnClassBinder` stamps execution classes on item containers via
  `ContainerPrepared`/`ContainerClearing`. A tunnel pointer-pressed hook implements the
  select-then-edit press model (editors would otherwise swallow the bubbling press): a plain
  left click on a not-yet-selected column selects it and focuses the item container — keeping
  Delete/Ctrl+C live — while a second click on the selected column falls through to the
  always-live editor; Ctrl/Shift clicks toggle/extend the multi-selection; right/middle clicks
  never change selection. A tunnel key-down handler implements the transposed arrow-key
  semantic (Right = next step, Down = next parameter), Enter commits by defocusing, and Escape
  reverts the pending text before defocusing.
- `ActiveRecipeGridSurface` — the delegating router and the single owner of orientation; see
  "Orientation switching" below.
- `RecipeGridHost` — the swap point hosted by `MainWindow.axaml`. Constructs both child views
  eagerly, keeps them alive across flips, and swaps its `Content` on orientation changes.
  Its `Surface` property (`DataContext as IRecipeGridSurface`) has no production consumer —
  `MainWindow` reads only `IsEditing`; `Surface` is pinned by tests.

## Shared surface base class

`RecipeGridSurfaceBase<TItem>` (`SemiStep.UI/RecipeGrid/RecipeGridSurfaceBase.cs`,
`TItem : class, IDisposable`) owns everything the two orientations have in common — which is
everything except item construction:

- the `Items` projection (`ObservableCollection<TItem>`) that each derived class re-exposes
  under its XAML-bound alias (`RecipeRows` / `StepColumns`);
- `MutationSignal` dispatch (`OnMutation`): the structural handlers (append, insert, remove,
  bulk remove, action-change rebuild, full rebuild — the inserting and removing ones renumber
  the items they shift), the one non-structural in-place property update, the stale-signal
  guards (log-and-return — they protect against signal-vs-projection races, not against bad
  data), and the post-mutation tail (selection reconcile, step start times, loop depths);
- the reactive contract members (`CanDeleteStep`, `IsReadOnly`, `EditorMustClose`,
  `SelectedStepIndex`/`SelectedStepIndices`, `SelectionRequests`) and the selection plumbing
  (`UpdateSelection`, `RequestSelection`, `CollectSelectedSteps`);
- the three write paths raised by `RecipeRowViewModel` (`OnCellValueChanged`,
  `OnSelectorValueChanged`, `OnActionChanged`) and their event wiring around item creation;
- the changed-highlight paths: `ClearChangedByClickAway` as a single linear scan over
  `RowOf(Items[i])`, plus the `ChangedCellClickAwayBroadcaster` subscription;
- the `RecipeCoordinator.Mutated` subscription and `Dispose` (items dispose themselves —
  hence the `IDisposable` constraint);
- the `RecipeMetadataRegistry` reference, exposed to the derived item factories as a
  `protected` property;
- one `ExecutionHighlightTracker` per surface, constructed over `Items.Count` /
  `RowOf(Items[i])`.

Two abstract hooks carry all the orientation-specific knowledge:

- `RowOf(TItem)` — maps an item to its `RecipeRowViewModel` (identity for canonical, `.Row`
  for transposed);
- `CreateItem(stepNumber, step, action)` — builds the item (canonical computes
  `BuildInapplicableColumns` and news up a `RecipeRowViewModel`; transposed news up a
  `StepColumnViewModel` with `ParameterDescriptors` and its cell factory delegate).

The derived classes are ~50 lines each: the constructor (public signature unchanged), the two
hooks, and the view-facing extras listed in their bullets above.

**Unified execution tracker.** `ExecutionHighlightTracker` consumes a
`(Func<int> rowCount, Func<int, RecipeRowViewModel> rowAt)` pair instead of a concrete
collection, so one type serves both orientations.

**Fail-loud item creation.** `CreateItemChecked` resolves the step's action key against
`RecipeMetadataRegistry` and throws `UnknownActionKeyException` (an
`InvalidOperationException`, `"Step {n}: unknown action key '{key}'"`) on lookup failure,
instead of the former log-report-and-skip. A skipped item would leave
`Items.Count < recipe.StepCount` and silently desync every index-based dispatch and
`CollectSelectedSteps`. Honest scoping of "loud": the throw crashes the app only on the
`Initialize()` path (startup projection and the router's `Initialize` fan-out). On the
`Mutated` dispatch path, `OnMutation` catches this specific exception, error-logs it, reports
it to the message panel, and leaves the projection as-is — matching the old skip's
user-visible outcome without the silent desync. Catching inside the handler (rather than
letting the throw escape to `RecipeCoordinator.RaiseMutatedSafely`, which wraps the whole
multicast `Mutated?.Invoke`) keeps signal delivery intact for the sibling surface and every
later-subscribed handler. The branch is defensive-only: config loading validates action keys,
and CSV import / clipboard paste run `ImportedRecipeValidator`.

## Interface member inventory

Members exist only because an existing consumer needs them (the plan's minimality rule):

| Member | Consumers |
| --- | --- |
| `Initialize()` | `MainWindowViewModel.Initialize()` |
| `StepCount` | `ClipboardViewModel` (paste insert index) |
| `IsReadOnly` | view one-shot reads (edit gating) |
| `SelectedStepIndices` / `SelectedStepIndex` | command + clipboard view-models |
| `UpdateSelection(indices)` | the view (native selection mapped to step indices) |
| `RequestSelection(int?)` | command + clipboard view-models, surface-internal action change |
| `SelectionRequests` | the view (positions native selection) |
| `CanDeleteStep` (`IObservable<bool>`, emits current value on subscription) | canExecute gates in command + clipboard view-models |
| `EditorMustClose` (no replay) | `DataGridEditorCloseBehavior.Trigger` binding in the view |
| `CollectSelectedSteps()` | `ClipboardViewModel` |

`HasSelection` and `SelectedStepIndicesChanged` were removed: no production consumer existed,
and every dead member would have to be implemented and contract-tested by each future surface.

## Two selection directions

- **View to surface:** the view translates its native selection into step indices and calls
  `UpdateSelection`. Canonical walks `DataGrid.SelectedItems`, maps rows through
  `RecipeRows.IndexOf`, and sorts ascending. Transposed maps the `ListBox`'s selected
  step-column items to indices and sorts ascending — same contract, different native control.
- **Consumer to surface to view:** consumers push a post-mutation reposition through
  `RequestSelection(int?)`; the surface forwards it into `SelectionRequests`; the view
  subscribes and sets its native selection (`null` clears). `RequestSelection` is a safe no-op
  after `Dispose()`.

## Orientation switching

`ActiveRecipeGridSurface` is the single owner of orientation. Everything else observes or
delegates:

- It owns reactive `Orientation` (the Core `GridOrientation` enum: `RowsAsSteps` canonical,
  `ColumnsAsSteps` transposed — the UI reuses the config enum instead of mapping it into a
  parallel one), initialized from `GridStyleOptions.Orientation` (see
  `grid-style-configuration.md`). `ToggleOrientation()` flips it, transferring
  `SelectedStepIndices` to the incoming surface **before** raising the change, so subscribers
  that re-attach on the flip observe a surface whose `CanDeleteStep` already reflects the
  carried-over selection (no transient false).
- Every interface member delegates to the active surface — **except `Initialize()`, which fans
  out to both surfaces.** `Mutated` is a plain no-replay event; a surface left uninitialized
  would stay blank until the next `RecipeReplaced`. With both initialized and both permanently
  subscribed to `Mutated`, either surface is current-state-correct the moment it becomes active.
- The three observables (`SelectionRequests`, `CanDeleteStep`, `EditorMustClose`) are
  switch-subscriptions over `Orientation`: consumers keep one subscription and transparently
  follow the active surface across swaps.
- The router does not dispose the concrete surfaces — they are container-owned singletons; DI
  disposes them.

**Host wiring and the DataContext pitfall.** `MainWindow.axaml` still binds the host's
DataContext to `{Binding RecipeGrid}`, which now resolves to the router. `RecipeGridHost`
casts it to `ActiveRecipeGridSurface` in `OnDataContextChanged`, sets each child view's
DataContext **explicitly** to the matching concrete surface (`CanonicalSurface` /
`TransposedSurface`), and subscribes `Orientation` to swap `Content` between the two views.
Explicit wiring is mandatory: letting the views inherit the router would silently null out
`ReactiveUserControl<T>.ViewModel`, because the router is not assignable to either concrete
surface type — the grid would never build. After each swap the host calls the incoming view's
`SyncSelectionFromSurface()`: both views stay alive across flips and their native selection
controls still hold the pre-flip selection, while the surface received the carried-over one —
without the resync the visible highlight would diverge from what Delete/Ctrl+C act on.

**Two live surfaces share one recipe.** Both surfaces stay permanently subscribed to
`Mutated`, but only the originating surface's edit handlers adjust applicability and the
changed-cell set. `RecipeRowUpdateSynchronizer` closes that gap: every `PropertyUpdated`
applies the old-vs-new step delta to the row (recompute applicability; clear the changed flag
of an edited cell; mark selector-seeded keys; unmark selector-dropped keys), so the inactive
surface's rows are already correct when it becomes active.

**Entry points.** The View menu carries a `ToggleType="CheckBox"` item
(`MenuViewTransposedGrid` resource) bound to `MainWindowViewModel.IsTransposedOrientation`
with `ToggleOrientationCommand`; `MainWindow.OnKeyDown` maps `Ctrl+Shift+T` to the same
command. `MainWindowViewModel` holds no orientation state of its own — both members are
pass-throughs to the router. Orientation is per-session; the config default applies on the
next launch.

## Dependency injection

`UiDi` registers `CanonicalRecipeGridSurface`, `TransposedRecipeGridSurface`, and
`ActiveRecipeGridSurface` as singletons and forwards `IRecipeGridSurface` to the router.
Interface consumers (`RecipeCommandsViewModel`, `ClipboardViewModel`) take the interface only;
`MainWindowViewModel` additionally receives the concrete router (the same singleton the alias
points to) for the orientation pass-throughs. The forwarding factory registration means the
container tracks the router twice for disposal; `Dispose` is idempotent, so the double call at
teardown is harmless.

## Mutation subscription ownership

Each surface subscribes itself to `RecipeCoordinator.Mutated` in the base-class constructor
and unsubscribes in `Dispose` — there is no external wiring and no "refresh me" method on the
interface. Both surfaces stay subscribed regardless of which one is active; that is what keeps
the inactive surface current for the next flip. Subscription order relative to the other
`Mutated` handlers (`PlcMonitorViewModel`, `MainWindowViewModel`, `RecipeCommandsViewModel`)
is immaterial because each handler reads only coordinator state committed before the event
fires plus its own private state — no handler consumes another handler's output within a
dispatch.

## IsEditing forwarding chain

Editing state is a view concern and is not on the interface. The chain:

1. `CanonicalRecipeGridView.IsEditing` — set true in `BeginningEdit` (unless cancelled for an
   inapplicable column), false in `CellEditEnded`, reset to false on view deactivation.
   `TransposedRecipeGridView.IsEditing` — true while an always-live cell editor holds keyboard
   focus (the transposed view has no DataGrid edit lifecycle; focus is the editing signal).
2. `RecipeGridHost.IsEditing` — forwards to whichever view is currently hosted as `Content`.
3. `MainWindow.OnKeyDown` — suppresses the Delete/Ctrl+C/X/V global shortcuts while
   `RecipeGridHost.IsEditing` is true, so typing inside a cell editor never deletes or
   cut/pastes steps.

## Context menu placement

The grid's context menu lives on the `Panel` wrapping `RecipeGridHost` in `MainWindow.axaml`
because its commands bind to `MainWindowViewModel`. Right-clicks over grid rows bubble
`ContextRequested` out of the DataGrid unhandled; a headless test in `RecipeGridHostTests`
pins this.
