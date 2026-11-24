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

- `File > Exit` (`MainWindowViewModel.ExitCommand`) fires `RequestCloseInteraction`, whose handler in
  `MainWindow.WhenActivated` calls `Close()` — same guard as the window's close button. The view model
  no longer holds a `Window` reference (see `dialogs-and-interactions.md`). Covered by
  `MainWindowExitFlowTests`.

## No forced-shutdown path remains

There is no code path that calls `IClassicDesktopStyleApplicationLifetime.Shutdown()` and bypasses
`OnWindowClosing`. The former `DesktopShutdownService.Shutdown()` helper (a forced shutdown that
raised `Closing` but ignored `e.Cancel`) was its only such call site; it has been removed. Every
app exit now routes through `MainWindow.Close()` and runs the dirty guard.

## PLC recipe write at exit

A recipe write runs on its own cancellation token, separate from the sync debounce. A sync disable
does not cancel it: `PlcLifecycleManager.DisableSync` waits for it up to 5 s, then disconnects, then
releases the sync ownership lease. `PlcSyncCoordinator.Dispose` waits up to 5 s for a started write,
and only after that bound cancels it and logs a warning. The write therefore ends whole unless the
connection drops, the PLC stalls past the bound, or the process dies.

Nothing calls `PlcSyncCoordinator.Dispose` in production today: the service container is never
disposed (#112). When #112 adds container disposal, it must dispose the coordinator explicitly before
`PlcLifecycleManager` and the container. The container disposes singletons in reverse creation order,
and `PlcLifecycleManager` is created after the coordinator, so it would otherwise release the lease
before the coordinator waits for the write. That disposal order was observed on
Microsoft.Extensions.DependencyInjection 10.0.0; #112 confirms it on 11.0.0, the version the project
pins, and for the S7Di shape, where `IPlcSyncService` is a factory that forwards to the
`PlcSyncCoordinator` registration.

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
