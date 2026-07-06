# Exit / Shutdown Flow

How the application terminates and where the unsaved-changes guard sits. Read this before adding a
new app-exit trigger.

## The single sanctioned exit path

Every ordinary app exit must route through `MainWindow.Close()`. Closing the window raises
`Closing`, which `MainWindow.OnWindowClosing` handles: on a dirty session it sets `e.Cancel = true`
and shows the `ExitConfirmationDialog` (Save / Don't Save / Cancel); on a clean session it lets the
close proceed.

The app runs under the default `ShutdownMode.OnLastWindowClose` (never set explicitly), so closing
the single main window cascades to process exit. This is why routing exit through `Close()` both
runs the guard and terminates the app. The process-exit cascade itself is not observable in the
headless test harness (it installs no classic-desktop lifetime); it is covered by manual
verification. The routing up to `MainWindow.Close()` is what the automated tests assert.

- `File > Exit` (`MainWindowViewModel.ExecuteExit`) calls `MainWindow?.Close()` — same guard as the
  window's close button. Covered by `MainWindowExitFlowTests`.

## No forced-shutdown path remains

There is no code path that calls `IClassicDesktopStyleApplicationLifetime.Shutdown()` and bypasses
`OnWindowClosing`. The former `DesktopShutdownService.Shutdown()` helper (a forced shutdown that
raised `Closing` but ignored `e.Cancel`) was its only such call site; it has been removed. Every
app exit now routes through `MainWindow.Close()` and runs the dirty guard.

## Style-editor restart path

The grid style editor's restart prompt exits through the same sanctioned path. `RestartPromptDialog`
is a pure decision dialog: "Exit Now" resolves `ShowDialog<bool>` to `true` (`Close(true)`), "Restart
Later" to `false` (`Close(false)`), and any dismissal (title-bar close / Escape via `IsCancel`)
defaults to `false`. `GridStyleEditorWindow.OnSaveCompleted` reads that decision and calls
`CompleteEditorClose(bool)`, which captures `Owner as Window` before `Close()` (Avalonia detaches the
owned-window link during the close sequence, so reading `Owner` afterwards yields `null` and would
silently drop the exit intent), then closes the captured owner (`MainWindow`) when exit was
requested. Closing `MainWindow` runs `OnWindowClosing` → the same dirty guard → `ExitConfirmationDialog`
on a dirty session, or a clean cascade to exit. This closes the earlier gap where "Exit Now" forced a
shutdown and could drop unsaved recipe changes.
