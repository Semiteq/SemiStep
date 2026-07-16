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
  of step-columns over the recycle-in-place `TransposedColumnsPanel` (a custom
  `VirtualizingPanel`, no DataGrid): realized element
  count is viewport-bound regardless of recipe length, and whole-column selection comes from
  `SelectionMode="Multiple"` natively. Realized columns do not render cells through a
  `FuncDataTemplate`; a view-owned pool hands each realized container a
  `TransposedColumnCellsPresenter` whose fixed per-descriptor slots rebind when the container
  re-points to a different column, and
  `TransposedCellTemplateFactory.CreateEditor` builds each slot's lazy display/editor cell (see
  "Allocation characteristics" for the recycle-in-place panel, the pooled presenter, and the lazy editor swap);
  `TransposedStepColumnClassBinder` stamps execution classes on item containers via
  `ContainerPrepared`/`ContainerClearing`. A tunnel pointer-pressed hook implements the
  select-then-edit press model (editors would otherwise swallow the bubbling press): a plain
  left click on a not-yet-selected column selects it and focuses the item container — keeping
  Delete/Ctrl+C live — while a second click on the selected column enters edit, building the
  lazy editor for that cell; Ctrl/Shift clicks toggle/extend the multi-selection; right/middle clicks
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

**Post-mutation tail refresh.** The tail treats its two projections differently:

- **Start-times refresh incrementally**, from a `refreshFrom` index derived from the
  `MutationSignal` down to `Items.Count`. `start-time[i]` is forward-prefix-determined — it depends
  only on steps `0..i-1` — so a mutation at index `k` cannot change any start-time before `k`.
  Refreshing only from `refreshFrom` is behavior-preserving and makes an append `O(1)` instead of
  `O(rows)`, which removes the per-append string-formatting churn that previously scaled with recipe
  length.
- **Loop-depths refresh with a full `0..Count` scan** on every mutation. Loop-depth is a
  matched-bracket property, so a committed marker mutation (e.g. deleting an `EndForLoop`) can change
  the depth of rows **above** the mutation index; an incremental depth refresh would leave those rows
  stale. The scan allocates nothing (a `Math.Min` and a guarded `int` setter), so the full pass is
  free.
- **`Initialize()` seeds the baseline** by running the tail from index 0 after `FullRebuild`, so the
  first post-init mutation's incremental start-time refresh has a correct starting point (rows before
  `refreshFrom` are already populated rather than left `null`).

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
  `RecipeRows.IndexOf`, and sorts ascending. Transposed reads
  `StepListBox.Selection.SelectedIndexes` — the index set the `SelectionModel` already
  maintains, pre-sorted and O(S) — rather than scanning each selected item back to its position
  through `StepColumns.IndexOf`. The step-column list is the model's `ItemsSource` with no
  collection-view wrapping, so those indices map 1:1 onto `StepColumns` and need no re-sort. The
  earlier item→index scan was O(S·N): on a 2100-step recipe with a large live selection it
  measured 2808 ms / 18.7 % of UI-thread CPU in a weighted `dotnet-trace` sample, the single
  biggest active cost, since it re-scanned the full step-column list per selected item on every
  membership change.
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
   `TransposedRecipeGridView.IsEditing` — true while the `TransposedTextEditCoordinator` holds an
   active edit (its lazily-built editor focused). The transposed view has no DataGrid edit lifecycle;
   the coordinator's single active edit is the editing signal.
2. `RecipeGridHost.IsEditing` — forwards to whichever view is currently hosted as `Content`.
3. `MainWindow.OnKeyDown` — suppresses the Delete/Ctrl+C/X/V global shortcuts while
   `RecipeGridHost.IsEditing` is true, so typing inside a cell editor never deletes or
   cut/pastes steps.

## Allocation characteristics

The transposed surface previously retained ~3x the managed heap of the canonical surface for the
same recipe and churned gen0 on every mutation. The reductions land in three places:

- **Core analysis cost per mutation.** Every mutation re-analyzes the whole recipe
  (`RecipeSession.Apply → RecipeAnalyzer.Analyze`), so the analyze path is O(N) per mutation and is
  kept allocation-lean. `TimingCalculator` resolves each step's action through
  `RecipeMetadataRegistry.TryGetAction` (a non-allocating `out`-parameter lookup) instead of the
  `Result<>`-returning `GetAction`, and the snapshot's `StepStartTimes` is a dense `TimeSpan[]`
  indexed by step position rather than a `Dictionary<int,TimeSpan>`. Readers bounds-check the index
  and fall back to the old empty/missing behavior. The per-PLC-tick timing path shares
  `ExtractStepDuration`, so it inherits the same win.
- **Cell background via a converter, not a style matrix.** The transposed cell background is
  resolved by a single `IMultiValueConverter`
  (`TransposedCellBackgroundConverter`) over five reactive state legs
  `(ForDepth, IsPastStep, IsApplicable, IsChanged, IsSelected)` plus the Self host, applied as a local
  `Border.Background` MultiBinding, reproducing the old document-order last-match-wins precedence.
  `IsReadOnly` is column-invariant, so it is not a reactive leg: it arrives through the per-slot
  `ConverterParameter` as a build-time constant, and the `read-only-cell` class is added statically in
  `BuildCellSlot` rather than via a `BindClass`.
  This replaces the descendant-selector background rules in `TransposedGridStyles.axaml`, avoiding
  the per-cell style-activator / dynamic-resource machinery those rules multiplied. The
  `TextElement.Foreground` setters stay as style setters (background stripped out), so foreground
  precedence is unchanged. The `IsSelected` leg is sourced from the presenter's own
  `TransposedColumnCellsPresenter.IsColumnSelected` (a `DirectProperty`, bound `Source = this`),
  which `TransposedColumnCellsHost` keeps in sync with the container `ListBoxItem.IsSelected`
  imperatively (one held subscription, resolved on attach, re-subscribed only when the container
  identity actually changes — stable across an in-place scroll recycle, so a scroll leaves it
  untouched; it re-resolves on surface swap / re-acquire — reset to `false` on release). It is deliberately NOT a `RelativeSource FindAncestor ListBoxItem`
  lookup: a pooled presenter is transiently off-tree (detached from any `ListBoxItem`), so that
  ancestor leg logged ~1155 "Ancestor not found" binding errors on a short scroll. The presenter
  re-announces the leg in `OnAttachedToVisualTree` so the background converter re-evaluates once the
  slot `Border` can reach the palette resources.
- **Lighter cell VMs.** Transposed cell view models are plain `INotifyPropertyChanged`, not
  `ReactiveObject` (nothing observes a cell VM reactively); `StepColumnViewModel.Cells` materialize
  lazily, so a column never scrolled to or keyboard-traversed never builds its cell VMs; and the
  per-action metadata dictionaries (Units / FormatKinds / GroupItems) are cached per
  `ActionDefinition` instead of rebuilt per row.

- **Recycle-in-place virtualizing panel (`TransposedColumnsPanel`).** Step-columns virtualize
  through a purpose-built `VirtualizingPanel` (`RecipeGrid/Transposed/TransposedColumnsPanel.cs`),
  not the horizontal `VirtualizingStackPanel` it replaced. VSP recycles a container by
  `RemoveInternalChild` (full visual + logical detach) followed by `AddInternalChild` (re-attach) on
  every viewport crossing, and a ~115-element column subtree then re-runs style-attach
  (`StyleBase.Attach` / `Setter.Instance`), property-store growth (`Dictionary.Resize`), and
  composition-visual creation (`CreateCompositionVisual`) on each recycle — a gc-verbose trace put the
  realization subtree at ~41–56% of scroll-time UI-thread allocation. `TransposedColumnsPanel`
  recycles IN PLACE, the mechanism Avalonia's own `DataGridRowsPresenter` uses: an idle container is
  hidden (`IsVisible=false`) and pushed to an idle `Stack`, never removed from the panel; on reuse it
  is shown and its `DataContext` re-points through the generator
  (`PrepareItemContainer` / `ItemContainerPrepared`). `AddInternalChild` runs once per physical slot,
  ever, so the style-attach, property-store, and composition-visual allocators are paid once per
  container and never again on scroll. Peak `Children.Count` is viewport columns plus a small buffer
  (~20-25), never the full step count.

  Uniform column width is load-bearing: every column is `ColumnWidth` wide (bound in the
  `ItemsPanelTemplate` to the `TransposedStepColumnWidth` resource), so viewport math is exact —
  `firstIndex = floor(viewportX / ColumnWidth)`, extent = `Items.Count * ColumnWidth`, arrange rect
  `(index*W, 0, W, height)` — with none of VSP's size-estimation or scroll-anchoring. The panel
  assumes a single fixed width; a width change arrives only with a new surface (= Reset), so there is
  no live re-flow path.

  Focus/edit deferral mirrors VSP exactly: the container equal to
  `KeyboardNavigation.GetTabOnceActiveElement(ItemsControl)` (the selection anchor) is NOT unrealized
  when scrolled out of the window — it stays measured and arranged, and a
  `TabOnceActiveElementProperty` listener releases it once the anchor moves. This lets a
  container-focused selected column survive scroll-out. An open editor holds keyboard focus itself, so
  the editor (not its container) becomes the `TabOnceActiveElement`; that container is unrealized
  normally and commits through the `ContainerClearing` hook (see the commit-before-rebind hook below).

  **Allocation gate.** The keep-attached panel is only half the recycle fix; the child-recycle half
  below ("Child recycled in place") lands the other half and carries the chain's first attached
  after-trace. The live-app gc-verbose byte gate — realization-subtree allocation share must drop from
  ~41–56% to <20%, with `Dictionary.Resize`, `CreateCompositionVisual`, and `StyleBase.Attach` /
  `Setter.Instance` absent from the scroll path — is a manual step on the Release build against a
  ~2100-step recipe and stays with the user (`CreateCompositionVisual` / `CreateSKFont` are
  composition/Skia costs headless cannot see). Headless tests pin the keep-attached contract
  (container reuse across scroll, no `DetachedFromVisualTree` on scroll, bounded `Children.Count`,
  focus-anchor deferral, selection round-trip).

- **Child recycled in place (`TransposedStepListBox`).** The panel keeping the container attached was
  only half the recycle fix. On every recycle the generator's
  `ItemContainerGenerator.ClearItemContainer` clears the container's `Content` AND `ContentTemplate`;
  the item `DataTemplate` is typed (`x:DataType="StepColumnViewModel"`) and cannot match the
  intermediate `null`, so `FindDataTemplate` falls to `FuncDataTemplate.Default`, which flips
  `ContentPresenter._recyclingDataTemplate` and DISCARDS the recyclable child. `PrepareItemContainer`
  then restored `Content` and rebuilt the whole ~115-element column subtree from scratch (plus two
  throwaway default-template `TextBlock`s per cycle), re-attaching it to the visual tree and re-running
  `ApplyStyling` / `StyleBase.Attach` / `CreateCompositionVisual` and re-acquiring the pooled presenter
  — the exact cascade `VirtualizingStackPanel.RecycleElement` pays, reproduced inside the keep-attached
  panel. `TransposedStepListBox : ListBox` closes the gap with three overrides:
  `ClearContainerForItemOverride` skips base (clears only `IsSelected`, so the child survives),
  `PrepareContainerForItemOverride` re-points `Content` explicitly on a recycled container (base
  `SetIfUnset` skips an already-set `Content` and would leave the stale column), and
  `StyleKeyOverride => typeof(ListBox)` keeps the Semi.Avalonia control template and the type-scoped
  selectors resolving. Both prepares now resolve the SAME `IRecyclingDataTemplate`, so
  `ContentPresenter` recycles the existing child in place: a scroll reuse touches nothing but a
  `DataContext` re-point — the panel kept the container attached, this keeps the child attached, and
  together the two land the full recycle-in-place win. The overrides are pinned to Avalonia 12.0.5
  `SetIfUnset` / `IsSet` / ClearContainer semantics.

  **Acceptance — first round shipped WITH its after-measurement.** Every prior perf round in this
  chain deferred the after-trace and reopened as "no perceptible change". This one carries an
  agent-run headless trace gate: a fixed scripted workload (300 viewport round-trips, add/remove 50
  steps, a 200-step execution-tick sweep) captured before and after under `dotnet-trace`, compared
  against pre-committed numbers. Results — attach/styling frame sum
  (`OnAttachedToVisualTreeCore` + `StyleBase.Attach` + `ApplyStyling`) collapsed **131 587 → 1 181 ms**
  (0.9% of baseline; `OnAttachedToVisualTreeCore` ABSENT under scroll-phase `Realize`);
  `MeasureOverride` inclusive **129 354 → 24 529 ms** (−81%); host re-attaches **36 → 0** per
  round-trip; viewport-jump allocation **1 008 701 → 235 399 B/realized column** (4.3× lower,
  WideParams). One honest caveat: the `MeasureOverride` absolute bar (Gate #2) missed its target by
  445 ms / 1.8%. The bar was derived as `baseline − 0.8 × attach_sum`, but the baseline attach sum
  (131 587) exceeds baseline `MeasureOverride` (129 354) because `StyleBase.Attach` nests inside the
  `OnAttachedToVisualTreeCore` cascade and part of the attach work runs outside `MeasureOverride`
  (initial window attach, add-step `AddInternalChild`), so the bar mis-assumed the whole attach cost
  lived inside `MeasureOverride`. The fix's success rests on Gate #1 (attach provably gone) and Gate #3
  (host re-attach 0) plus the 81% `MeasureOverride` drop; the 1.8% miss is the documented frame-overlap
  artifact, accepted, not a root-cause failure.

- **Pooled cell presenter, now a per-surface factory (the source of canonical parity).** The dominant
  transient cost is per-realized-column, not retained heap (six gcdumps confirmed the heap plateaus).
  The canonical `DataGrid` is cheap on scroll because it RECYCLES realized rows — a recycled row
  rebinds its subtree to the new data instead of rebuilding it. The transposed grid mirrors that.
  Realized columns do not render cells through a `FuncDataTemplate`: `TransposedColumnCellsHost` (a
  `Decorator` in the `ItemTemplate`) hands each container a `TransposedColumnCellsPresenter` (a
  `StackPanel` subclass building one cell `Border` slot per `ParameterDescriptor`, no per-cell
  `ContentControl`), and each slot binds its `DataContext` to `Cells[i]` via `CellSlotConverter`
  (index passed as `ConverterParameter`), so a container re-pointing from `columnA` to `columnB`
  rebinds every slot from `columnA.Cells[i]` to `columnB.Cells[i]` and the subtree persists rather
  than rebuilds. Under recycle-in-place the host never detaches on scroll, so it holds one presenter
  for life and rebinds it (never re-acquires) when its column changes. `TransposedColumnCellsPool` is
  therefore demoted from a scroll-time cycle to a **per-surface presenter factory**: it serves only
  the surface-swap teardown — a `RecipeReplaced` / Reset detaches every container
  (`RemoveInternalChild` on all children, realized and idle), each host releases its presenter into
  the dying pool, and the next surface's pool starts fresh. Cell height and the frozen left
  name-column alignment are unchanged; the execution-class binder and current-step marker still
  operate at `ListBoxItem` / row level. Slots are only built for actually-realized columns, so the
  never-realized-column `Lazy<>` optimization stays intact.

- **Lazy display/editor swap for both cell kinds.** With reuse in place, live editors also leave the
  jump hot path, and the remaining fresh-container weight is cut by rendering a display `TextBlock` by
  default and building the `TextBox`/`ComboBox` editor only on edit entry. `TransposedLazyCellPresenter`
  is the shared base; `TransposedTextCellPresenter` (display via `PropertyTextEditingMultiConverter`)
  and `TransposedComboCellPresenter` (display via `ComboBoxDisplayTextConverter`, showing the selected
  item's text) each build their editor lazily and swap back to the display on commit/blur. A single
  view-level `TransposedTextEditCoordinator` owns the one active edit across both kinds — the
  display→editor swap, focus, commit, and revert — so `IsEditing`/`GetActiveEditor`/`CloseActiveEditor`
  and exit gating have one definition and one reset point on recycle. Read-only and inapplicable cells
  stay display-only. The select-then-edit gesture is preserved: the first press selects the column
  without editing, and a second press on the sole selected column (or F2 / a printable keystroke on a
  focused display visual) enters edit; `FindFocusableEditor` targets the display presenters so arrow
  navigation traverses cells and enters edit on demand. When no cell is in edit a realized column holds
  only display `TextBlock`s — zero live `TextBox`/`ComboBox`.

- **Commit-before-rebind hook.** Once editors persist across a rebind, the old commit path (editor
  destroyed → `LostFocus` → commit) no longer fires reliably: a focused persistent editor whose
  container rebinds in place raises no `LostFocus`, and the OneWay display binding would overwrite the
  pending text — silent edit loss. An explicit commit runs before the slots rebind:
  `TransposedColumnCellsPresenter.CommitActiveEditor` (invoked from its `OnDataContextBeginUpdate`,
  which Avalonia calls top-down and stops at children whose `DataContext` is locally set, so it fires
  while the editor still holds its pending text and captured cell). On a scroll recycle-out the host no
  longer detaches (it only hides — see "host never detaches on scroll" above), so the commit runs from
  the view's `TransposedRecipeGridView.OnContainerClearing` hook: it finds the still-attached presenter
  and commits it before the container rebinds. That view hook is the primary scroll-recycle-out commit
  path; the host's own release path now commits only on surface-swap teardown, when a container is
  genuinely detached. The `_editingCellProperty`/captured-cell stale-guard remains the backstop so a
  still-focused editor cannot write into the cell it was rebound onto.

- **Measured result.** These figures were captured under the earlier `VirtualizingStackPanel` +
  pooled-rebind path; they measure the pooled-presenter reuse, not the recycle-in-place panel that
  later replaced VSP (whose byte/gen0 allocation gate is still pending live-app measurement — see the
  recycle-in-place panel note below). On the viewport-jump metric (one `ScrollIntoView(last)` frame
  after a round-trip, so it exercises container recycling), transposed bytes per realized column dropped
  from ~14.5x the canonical recycled-row cost to ~1.03x (WideParams, 36 cells/column) and from ~2.3x to
  ~0.69x (WithGroups, 5 cells/column); gen0/add fell from ~2.58 to ~0.17-0.25 (WideParams) and from
  ~0.42 to ~0.00-0.08 (WithGroups). With no cell in edit the live-editor census is 0.

- **Known follow-ups (separate PRs, not this change).** Two items are recorded, not done. First, the
  `TransposedColumnCellsHost` + `TransposedColumnCellsPool` indirection is now dead weight: post-fix the
  host never detaches on scroll, so its acquire/release cycling is gone and one presenter can be inlined
  per physical container — a deliberate follow-up touching surface-swap teardown, `CommitActiveEditor`
  ordering, and `_editCoordinator.Reset`. Second, the residual render-thread `CreateSKFont` text-shaping
  cost is headless-invisible (no Skia in the trace); if the live re-trace still shows it hot, add a
  display-text `TextLayout` cache keyed by (string, typeface, size), since grid values repeat heavily.

## Performance measurement discipline

Each transposed-grid performance round is gated on measurement, not on felt lag or code review.
The rules:

- **Open with a weighted trace.** Before touching code, capture a weighted CPU trace
  (`dotnet-trace`, viewed in Speedscope) plus GC counters (`dotnet-counters` on
  `System.Runtime[time-in-gc,gen-0-gc-count]`) on the scripted 2100-step scroll+add scenario. The
  round targets whatever the trace names as the dominant active cost, not a suspected hotspot. The
  `IndexOf`-in-N selection scan was found this way; three prior rounds that measured allocation and
  binding errors never surfaced it because it was CPU self-time, not heap.
- **Pre-commit the exit number.** Write the exit criterion as a concrete number before the code
  (for the selection fix: per-selection-event cost must not scale with N). Ship the after-trace
  with the change so the collapse is documented, not asserted.
- **Keep a checked-in regression instrument.** `TransposedSelectionCostProbe`
  (`SemiStep.Tests/Performance/`, env-gated `SEMISTEP_PROBE=1`, `Category=Performance`, skipped in
  CI) holds selection size constant at S=200 while N grows (300 / 1200 / 4800) and asserts the
  per-event cost at N=4800 stays within 3× of N=300. The fixed-S design isolates the `IndexOf`-in-N
  regression: select-all would make S=N and force O(N) even with the fix. Restoring the `IndexOf`
  scan makes the ratio return at ~7×–16× (linear in N at fixed S); the fix stays flat. This is the
  guard that catches the class of regression by instrument rather than by manual complaint.

## Context menu placement

The grid's context menu lives on the `Panel` wrapping `RecipeGridHost` in `MainWindow.axaml`
because its commands bind to `MainWindowViewModel`. Right-clicks over grid rows bubble
`ContextRequested` out of the DataGrid unhandled; a headless test in `RecipeGridHostTests`
pins this.

## Framework diagnostics logging

Avalonia framework diagnostics (binding errors, layout warnings, and the rest) are forwarded into
the app's Serilog pipeline by a custom `Avalonia.Logging.ILogSink`, `AvaloniaSerilogSink`, installed
via `LogToSerilog()` in `BuildAvaloniaApp` (`App.axaml.cs`) in place of `LogToTrace`. `LogToTrace`
routed diagnostics to `System.Diagnostics.Trace`, where under a debugger each write is a synchronous
`OutputDebugString`; a binding-error storm on the UI thread then froze the grid under F5 while
Release stayed smooth. The Serilog sink neutralizes that freeze mechanism and unifies the two log
channels.

The sink runs at minimum `Warning`, across all `LogArea`s, with a per-`(area + template)` throttle
(first 20 in full, then every 500th carrying the running count) so a repeating template cannot flood
the file. It forwards the template and args structured (not pre-formatted) under a
`SourceContext` of `"Avalonia.<area>"`, mapping the Avalonia log level to the Serilog level by an
explicit switch.

A healthy build logs zero binding errors. That invariant is enforced in headless tests by
`BindingErrorGuard` (`SemiStep.Tests/UI/Helpers/BindingErrorGuard.cs`), an `IDisposable` that
installs a collecting `ILogSink` for the duration of a test, records `LogArea.Binding` events, and
restores the previous sink on dispose. `TransposedSelectionBindingTests.ScrollSweepAndSelect_LogsZeroBindingErrors`
wraps a transposed scroll start→end→start plus a column select in the guard and asserts zero binding
errors (down from ~1155 before the presenter-sourced selection fix above).
