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

Because the dialog now goes through an interaction, the flow is unit-testable headless: a test
registers a fake handler and asserts the VM behavior with no `Window` in play. A missing handler
raises `UnhandledInteractionException` instead of silently dropping the dialog, so the conflict flow's
try/catch turns that into a reported failure rather than a no-op.

## The view-side boundary

Two dialogs deliberately stay in view code-behind and are **not** routed through VM interactions,
because they are window-lifecycle concerns the view model cannot own:

- **Exit confirmation** — `ExitConfirmationDialog`, shown from `MainWindow.OnWindowClosing` via
  `ShowExitChoiceAsync`. It hangs off `WindowClosingEventArgs` / `e.Cancel` / `Close()`, state that
  belongs to the window's own `Closing` event.
- **Style-editor restart prompt** — `RestartPromptDialog`, shown from
  `GridStyleEditorWindow.OnSaveCompleted`. It belongs to the style-editor window's own save flow.

A view showing a dialog in response to its own lifecycle event is correct MVVM, not a violation. Both
paths run as `async void` event handlers, so they carry their own try/catch guards; the global backstop
installs no Avalonia dispatcher hook and cannot catch an `async void` throw. Those guards remain in
place and are not superseded by the interaction convention above. See `error-reporting.md` for the
guard rationale and `exit-flow.md` for the exit path.
