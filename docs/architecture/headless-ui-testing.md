# Headless UI Testing

## Overview

UI tests run on the Avalonia headless platform via `[AvaloniaFact]` / `[AvaloniaTheory]` from
`Avalonia.Headless.XUnit`. These attributes wrap the full test lifecycle — including
`IAsyncLifetime.InitializeAsync` and `DisposeAsync` — in the headless dispatcher, so the test body
executes on the UI thread. Manual `Dispatcher.UIThread.RunJobs(...)` pumping and sync-over-async
wrappers are not needed as a rule.

The full `MainWindow` constructs and shows under the headless fixture, so window-level integration
tests (close flow, dialogs, owned windows) are viable; there is no need for a stripped-down window
stand-in. See `SemiStep.Tests/UI/MainWindow/MainWindowExitFlowTests.cs` for a working example.

## When `RunJobs` is still required

`RunJobs` remains necessary only to pump work the test never awaits:

- fire-and-forget `async void` continuations (e.g. a `Closing` event handler that awaits a dialog);
- `ObserveOn(RxApp.MainThreadScheduler)` deliveries, such as the `RecipeCoordinator.PlcStateChanged`
  snapshots that the status-bar link slot (`PlcLinkViewModel`) reads;
- the deferred list open in `LazyComboCellPresenter`: an edit entry posts the job that sets
  `IsDropDownOpen` at `Background` priority, and nothing awaits it.

Call `Dispatcher.UIThread.RunJobs()` after triggering such work and before asserting on its effect.

## The headless dispatcher runs on a pool thread

The headless dispatcher thread is a thread-pool thread, so `Thread.IsThreadPoolThread` is true inside an
`[AvaloniaFact]` body. A test that checks whether code left the UI thread asserts on
`SynchronizationContext.Current` instead: it is `AvaloniaSynchronizationContext` on the dispatcher and
`null` in a loop that `Task.Run` started. A new background loop in Core starts with `Task.Run`, so it never
captures the caller's context.

## Interaction handlers are resolved LIFO

ReactiveUI `Interaction<TInput, TOutput>` invokes registered handlers in LIFO order. A test handler
that must override the window's own handler (e.g. `SaveFileInteraction`, whose production handler
opens a file picker) has to be registered **after** `window.Show()`; otherwise the window's
`WhenActivated`-registered handler wins and the test hangs on a picker that never appears.

Because `WhenActivated` registration is driven by dispatched activation work, follow
`window.Show()` with a `Dispatcher.UIThread.RunJobs()` in the test setup so the window's handlers
are deterministically in place before the test registers its overriding handler.

## Pointer tests need hit-testable backgrounds

Headless pointer tests hit-test the real visual tree. A `Border` (or panel) with a null
`Background` is not hit-testable, so synthesized presses fall through it to whatever sits
underneath. Production installs the palette resources at startup; a test window must do the
same (e.g. `CellPaletteInstaller.Install(window.Resources, gridStyle)`) before clicking cells,
or presses over disabled editors miss their cell border.

## Typing a printable key

On Windows a key press is a `WM_KEYDOWN` followed by a `WM_CHAR`. Avalonia.Win32 drops the `WM_CHAR`
when the `KeyDown` came back handled, so a handler that marks a printable `KeyDown` handled loses its
character in the real app. Headless input has no such link: `KeyPressQwerty` and `KeyTextInput` are
independent calls. Type a printable key with `HeadlessTyping.TypeKey` (`SemiStep.Tests/UI/Helpers`): it
sends the `TextInput` only when the `KeyDown` was not handled, as Win32 does. Call `KeyTextInput` alone
only for input that has no `KeyDown`, such as IME text.

## Clicking a combo list item

An open `ComboBox` list lives in a popup with its own `TopLevel`, so a press sent to the window
never reaches an item. Find the item with `comboBox.ContainerFromIndex(i)`, take its top level
with `TopLevel.GetTopLevel(item)`, translate the item's centre into that top level and send
`MouseDown` and `MouseUp` to it (`ComboBoxTestHelper.ClickListItem` in `SemiStep.Tests/UI/Helpers`).

## `WhenActivated` fires after the first layout pass

ReactiveUI activation is driven by `Loaded`, which fires **after** the first layout pass has
already realized item containers. Two consequences for view wiring:

- container-prepared handlers attached inside `WhenActivated` must also stamp the
  already-realized containers retroactively (`GetRealizedContainers()`);
- code-built `DataTemplates` and layout-driving resources must be installed from the
  constructor (keyed off the `ViewModel` property), or the first layout runs without them.
