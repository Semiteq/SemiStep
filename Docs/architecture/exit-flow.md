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
runs the guard and terminates the app.

- `File > Exit` (`MainWindowViewModel.ExecuteExit`) calls `MainWindow?.Close()` — same guard as the
  window's close button. Covered by `MainWindowExitFlowTests`.

## The forced variant bypasses the guard

`DesktopShutdownService.Shutdown()` maps to `IClassicDesktopStyleApplicationLifetime.Shutdown()` —
the forced shutdown that raises `Closing` but ignores `e.Cancel`. It skips the dirty guard by design
and must be used only where unsaved-recipe loss is impossible or already handled.

## Known gap

`RestartPromptDialog.OnExitNowClick` (style-editor restart) still calls the forced
`DesktopShutdownService.Shutdown()` and can drop unsaved changes. Tracked as a separate follow-up
issue; not addressed here.
