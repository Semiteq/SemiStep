# Recipe Grid Surface

## Overview

The recipe grid is consumed through the abstraction `IRecipeGridSurface`
(`SemiStep.UI/RecipeGrid/IRecipeGridSurface.cs`). The canonical view (rows = steps,
columns = parameters) is one concrete implementation; a transposed implementation can be added
as a sibling surface + view pair without touching `MainWindow`, the command view-models, or the
clipboard view-model.

The pieces:

- `IRecipeGridSurface` — the view-facing API. Selection is expressed in step indices only.
- `CanonicalRecipeGridSurface` — the concrete surface for the canonical orientation. Owns row
  projection (`RecipeRows`), mutation handling, execution highlighting, and the canonical-only
  members (`ColumnBuilder`, internal `RecipeMetadataRegistry`). `ColumnBuilder` is
  constructor-injected into the surface solely so the view can reach it through `DataContext` —
  a recorded trade-off chosen over a service locator, to revisit when the transposed view lands.
- `CanonicalRecipeGridView` (`ReactiveUserControl<CanonicalRecipeGridSurface>`) — wraps the
  `DataGrid`, owns its event handlers (`BeginningEdit`, `CellEditEnded`, `SelectionChanged`,
  `CellPointerPressed`, `LoadingRow`), column building, and the changed-cell click-away state.
- `RecipeGridHost` — the swap point hosted by `MainWindow.axaml`. Today it is a thin
  pass-through around `CanonicalRecipeGridView`; the transposed plan wires the actual switching.
  Its `Surface` property (`DataContext as IRecipeGridSurface`) has no production consumer —
  `MainWindow` reads only `IsEditing`; `Surface` is kept as the transposed-plan seam and is
  pinned by tests.

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
  `RecipeRows.IndexOf`, and sorts ascending.
- **Consumer to surface to view:** consumers push a post-mutation reposition through
  `RequestSelection(int?)`; the surface forwards it into `SelectionRequests`; the view
  subscribes and sets its native selection (`null` clears). `RequestSelection` is a safe no-op
  after `Dispose()`.

## Dependency injection

`UiDi` registers `CanonicalRecipeGridSurface` as a singleton and forwards
`IRecipeGridSurface` to that same instance. All three consumers (`MainWindowViewModel`,
`RecipeCommandsViewModel`, `ClipboardViewModel`) take the interface only; the concrete type is
referenced solely by the surface itself, `CanonicalRecipeGridView`, and the DI registration.
The forwarding factory registration means the container tracks the instance twice for disposal;
`Dispose` is idempotent, so the double call at teardown is harmless.

## Mutation subscription ownership

The surface subscribes itself to `RecipeCoordinator.Mutated` in its constructor and
unsubscribes in `Dispose` — there is no external wiring and no "refresh me" method on the
interface. This moved subscription order relative to the other three `Mutated` handlers
(`PlcMonitorViewModel`, `MainWindowViewModel`, `RecipeCommandsViewModel`); order is immaterial
because each handler reads only coordinator state committed before the event fires plus its own
private state — no handler consumes another handler's output within a dispatch.

## IsEditing forwarding chain

Editing state is a view concern and is not on the interface. The chain:

1. `CanonicalRecipeGridView.IsEditing` — set true in `BeginningEdit` (unless cancelled for an
   inapplicable column), false in `CellEditEnded`, reset to false on view deactivation.
2. `RecipeGridHost.IsEditing` — forwards to the hosted view.
3. `MainWindow.OnKeyDown` — suppresses the Delete/Ctrl+C/X/V global shortcuts while
   `RecipeGridHost.IsEditing` is true, so typing inside a cell editor never deletes or
   cut/pastes steps.

## Context menu placement

The grid's context menu lives on the `Panel` wrapping `RecipeGridHost` in `MainWindow.axaml`
because its commands bind to `MainWindowViewModel`. Right-clicks over grid rows bubble
`ContextRequested` out of the DataGrid unhandled; a headless test in `RecipeGridHostTests`
pins this.
