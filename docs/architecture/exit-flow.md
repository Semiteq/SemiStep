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

## PLC recipe write at sync disable

A recipe write runs on its own cancellation token, separate from the sync debounce. A sync disable
does not cancel it: `PlcLifecycleManager.DisableSync` waits for it up to 5 s, then disconnects, then
releases the sync ownership lease. The write therefore ends whole unless the connection drops, the
PLC stalls past the bound, or the process dies. The exit keeps the same order, see
[Service teardown at exit](#service-teardown-at-exit).

## Service teardown at exit

`Program.Main` disposes the service container once, after `App.Run` returns (or throws) and before
the log flush. `Program.DisposeServices` runs `S7Di.DisposeInShutdownOrderAsync` on a pool thread and
waits for it at most 10 s. A timeout logs one warning, a failure logs one error, and the exit code
does not change. Past the bound the OS closes the socket and the sync ownership lock file, as it
does on Windows session end.

The teardown follows the order of `PlcLifecycleManager.DisableSync`:

1. `PlcSyncCoordinator.Dispose` cancels the debounce and waits up to 5 s for a started write. Past
   that bound it cancels the write and logs a warning. An edit still in the 1 s debounce is dropped
   on purpose, because only a started write completes.
2. `IS7Connection.DisposeAsync` disconnects from the PLC.
3. The container disposes the rest. `PlcLifecycleManager.Dispose` releases the lease.

The order is explicit because the container disposes singletons in reverse capture order, and
`PlcLifecycleManager` is captured after the coordinator and the S7 forwards it depends on. Left to the
container, the lease release and the disconnect would both run before the write wait (probed on
Microsoft.Extensions.DependencyInjection 11.0.0). The container calls `DisposeAsync` once per
registration that resolved an instance, so `S7Service` guards its teardown and leaves the `S7Driver`
it did not create to the container. Sync `Dispose` throws, because `S7Service` and `S7Driver`
implement only `IAsyncDisposable`.

A throw is handled differently per step. In step 3 the container's `DisposeAsync` continues past a
service that throws and rethrows at the end. A throw in step 1 or 2 skips the rest of those steps,
but the routine runs step 3 in a `finally`, so the container still disposes and the lease is
released. The exception then reaches `Program.DisposeServices`, which logs one error.

Automated tests cover:

- `ShutdownOrderTests`: the order with a held write, the dropped debounced edit, and the container
  step after a connection that throws.
- `S7ServiceDisposeTests`: the `S7Service` dispose guard and the transport it leaves undisposed.
- `ServiceTeardownTests`: the real container after the main window closes, the real container that
  never connected, the bound and the failure path.

Checked by hand only: `StartWithClassicDesktopLifetime` returning and the process exit, the teardown
after `App.Run` throws, and Windows session end.

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
