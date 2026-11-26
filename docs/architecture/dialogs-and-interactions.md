# Dialogs and Interactions

How a view-model-driven dialog reaches the screen without the view model depending on
`Avalonia.Controls.Window`. Read this before adding a new dialog that a view model opens.

## The convention

A view model that needs to show a dialog declares a ReactiveUI `Interaction<TInput, TOutput>` as a
property, constructed in its constructor, and calls `await interaction.Handle(input)` where it once
newed a window. It never references `Window`, never news a concrete dialog type, and never holds a
back-reference to the view.

The view supplies the window. `MainWindow.WhenActivated` registers one handler per interaction with
`.RegisterHandler(HandleX).DisposeWith(disposables)`; the handler news the concrete dialog, `await`s
`ShowDialog(this)`, and calls `context.SetOutput(...)` with the result. This is the same idiom the
file pickers already use (`RecipeFileViewModel.OpenFileInteraction` / `SaveFileInteraction`), so a new
VM-driven dialog follows an established pattern rather than inventing one.

`MainWindowViewModel` routes three dialogs this way:

- `ShowStyleEditorInteraction` (`Interaction<GridStyleEditorViewModel, Unit>`) — the grid style editor.
  The VM builds and `LoadAsync`es the editor view model, then hands it to the interaction; the handler
  news `GridStyleEditorWindow { DataContext = context.Input }`.
- `ResolveConflictInteraction` (`Interaction<PlcConflictDialogViewModel, bool?>`) — the PLC recipe
  conflict dialog. Output encodes the three outcomes: `null` = cancelled, `true` = keep local,
  `false` = load from PLC. The handler news `PlcConflictDialog(context.Input)` and reports
  `dialog.Confirmed ? dialog.KeepLocal : null`.
- `RequestCloseInteraction` (`Interaction<Unit, Unit>`) — `File > Exit`. The handler calls `Close()`,
  which runs `OnWindowClosing`'s dirty-close guard (see `exit-flow.md`).

`RecipeFileViewModel.UnsavedChangesInteraction` (`Interaction<Unit, ExitConfirmationResult>`) asks
before "Создать рецепт" or "Открыть рецепт" drops a dirty recipe; see "Unsaved changes" below.

Because the dialog now goes through an interaction, the flow is unit-testable headless: a test
registers a fake handler and asserts the VM behavior with no `Window` in play. A missing handler
raises `UnhandledInteractionException` instead of silently dropping the dialog, so the conflict flow's
try/catch turns that into a reported failure rather than a no-op.

## The view-side boundary

Two dialogs deliberately stay in view code-behind and are **not** routed through VM interactions,
because they are window-lifecycle concerns the view model cannot own:

- **Exit confirmation** — `ExitConfirmationDialog`, shown from `MainWindow.OnWindowClosing` via
  `ShowExitChoiceAsync`. It hangs off `WindowClosingEventArgs` / `e.Cancel` / `Close()`, state that
  belongs to the window's own `Closing` event. The dialog and the meaning of each answer are shared
  with New and Open; see "Unsaved changes" below.
- **Style-editor restart prompt** — `RestartPromptDialog`, shown from
  `GridStyleEditorWindow.OnSaveCompleted`. It belongs to the style-editor window's own save flow.

A view showing a dialog in response to its own lifecycle event is correct MVVM, not a violation. Both
paths run as `async void` event handlers, so they carry their own try/catch guards; the global backstop
installs no Avalonia dispatcher hook and cannot catch an `async void` throw. Those guards remain in
place and are not superseded by the interaction convention above. See `error-reporting.md` for the
guard rationale and `exit-flow.md` for the exit path.

## Unsaved changes

Three actions drop the current recipe: closing the window, "Создать рецепт" (menu item and
Ctrl+Shift+N) and "Открыть рецепт" (menu item, toolbar button "Открыть" and Ctrl+O). On a dirty recipe each one shows
`ExitConfirmationDialog` with Save / Don't save / Cancel. The dialog text does not name the action.
On a clean recipe nothing is asked.

- `RecipeFileViewModel.ResolveUnsavedChangesAsync` is the one home of the answers. Save runs
  `SaveRecipeCommand`, which goes through save-as when the recipe has no file, and returns `true` only
  after a successful save. A cancelled save-as, a failed save, an exception and a save already in
  flight return `false`. Don't save returns `true`. Cancel and a dismissed dialog return `false`.
- New and Open call it through `UnsavedChangesInteraction` before they act, so Open asks before the
  file picker. `false` leaves the recipe untouched.
- The close path calls it from `MainWindow.HandleExitChoiceAsync` and closes on `true`. It keeps its
  own dirty check, because `OnWindowClosing` must set `e.Cancel` before the first `await`.
- `MainWindow.ShowUnsavedChangesDialogAsync` is the one place that news the dialog. Both the close
  path and the interaction handler call it.

Covered by `RecipeFileUnsavedChangesTests` (stub handler, each answer for New and Open, save-as, a
cancelled save-as, a failed save, no prompt when clean), `MainWindowUnsavedChangesTests` (the real
dialog from Ctrl+Shift+N and Ctrl+O) and `MainWindowExitFlowTests` (the close path).

## Window shortcuts

`MainWindow/WindowGestures.cs` is the one gesture table. Each menu item in `RecipeMenuBar.axaml`
shows its gesture from the table, each toolbar tooltip prints the same text (`TipOf`), and
`MainWindow.OnKeyDown` runs the item whose gesture matches the key (`RecipeMenuBar.ItemFor`). The pattern follows SemiPlot
(`MainWindow.OnKeyDown` plus `AppMenuBar.GoItemFor`).

- No `Window.KeyBindings` and no `HotKey`: both take the key from a focused `TextBox` (probed
  2026-11-25, Avalonia 12.0.5). A `MenuItem.InputGesture` alone only prints the gesture.
- The handler skips a handled event and an event whose source is a `TextBox`. A `TextBox` lets
  Ctrl+N, Ctrl+S and F2 through unhandled, so testing `Handled` alone is not enough.
- Delete, Ctrl+C, Ctrl+X and Ctrl+V also skip while a cell is being edited: `RecipeGridHost.IsEditing`
  or a key source inside a `ComboBox` whose list is open. A canonical combo cell lives in
  `CellTemplate`, so its open list is not a `DataGrid` edit.
- After Delete and Ctrl+X the next step stays selected and focus returns to the grid, so the key
  repeats; see "Selection after a delete" in `recipe-grid-surface.md`.
- Ctrl+Shift+T runs from an open cell editor too: it commits the value and only changes the view.
- Ctrl+Shift+Z is a second redo gesture; the menu prints Ctrl+Y. Alt+F4 is display-only on
  "Выход" and the handler never runs it.
- The handler searches the items of every top-level menu and asks `ICommand.CanExecute` itself.
- F2 stays a grid key (`DataGrid` and `TransposedGridNavigator`).
