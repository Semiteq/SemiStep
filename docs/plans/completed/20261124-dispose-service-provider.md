# Dispose the service provider at exit

## Behavior
- Today nothing disposes the service container. `Program.BuildServices` returns it, `App.Run` calls
  `StartWithClassicDesktopLifetime` and never disposes it, and no `ShutdownRequested` or `Exit` hook
  exists. The PLC teardown (`S7Service.DisposeAsync`, `PlcLifecycleManager.Dispose`,
  `PlcSyncCoordinator.Dispose`) runs only in tests. At exit the OS drops the TCP connection and
  closes the sync ownership lock file.
- After the change `Program.Main` disposes the container once, after `App.Run` returns and before
  the log flush. The PLC sees a clean disconnect, and the app releases the ownership lease itself.
- Order at exit mirrors `PlcLifecycleManager.DisableSync`: the sync coordinator first (it waits up
  to 5 s for a recipe write that already started), then the S7 connection (disconnect), then the
  container (`PlcLifecycleManager` releases the lease, the rest of the singletons follow).
- An edit that still waits in the 1 s debounce at exit stays dropped on purpose. #180 decided that
  only a started write completes; the debounce is not a write.
- Exit is bounded. The whole teardown runs on a pool thread, and `Main` waits for it at most 10 s.
  When the bound expires, the app logs a warning and exits; the OS then closes what is left, as
  today. Worst-case time from the main window closing to process exit is 10 s plus the log flush.
  With no write in flight the teardown takes milliseconds (probes below: 3 ms connected, 6 ms with a
  connect to an unreachable PLC pending).
- "Disposed once" means each singleton's teardown body runs once. The container calls `Dispose` or
  `DisposeAsync` once per registration that resolved the instance, and the explicit steps add one
  more call; every call after the first returns at once or does nothing.
- Scope guard: the window close path and its dirty guard (`exit-flow.md`), the error-window startup
  path (it builds no container), the PLC write protocol and all other PLC behavior stay as they are.
  On Windows session end today's behavior holds: the OS closes the socket and the lock file. #110,
  #117 and #181 are out of scope.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Exit, PLC disconnected, sync off | teardown runs, no PLC traffic, exits at once | agent: `S7Service.DisconnectAsync` skips the transport when not connected | `ServiceTeardownTests.RealContainer_NeverConnected_TearsDownWithoutWarnings` |
| Exit, PLC connected, no write | coordinator disposed, one clean disconnect, lease released, exits at once | agent: issue scope | Acceptance 1, smoke 1-4 |
| Exit while an edit waits in the debounce | the edit is not written; the PLC keeps the last committed recipe | operator: #180 design, `PlcSyncCoordinator.Dispose` cancels the debounce | Acceptance 2 |
| Exit during a started recipe write | the write ends whole (up to 5 s), then the disconnect, then the lease release | agent: #180 contract, `DisableSync` order | Acceptance 2 |
| Exit while the write stalls past 5 s | the coordinator logs a warning and cancels the write; teardown continues | agent: `PlcSyncCoordinator.Dispose` bound | existing #180 coordinator tests |
| Exit while a connect to an unreachable PLC is pending | teardown ends in milliseconds; the pending connect dies with the process | agent: probe below | probe (Caveats) |
| Exit while the reconnect loop sits in a connect to an unreachable PLC | `S7Service.DisconnectAsync` waits for that attempt; the 10 s bound ends the wait, warning logged, process exits | agent: exit bound | Acceptance 3 |
| Teardown throws | one error logged with the exception, exit continues, exit code unchanged | agent: a failed cleanup does not change the session outcome | Acceptance 4 |
| `App.Run` throws | fatal logged as today, then the same bounded teardown runs | agent: teardown sits in a `finally` around `App.Run` | not visible to headless tests |
| Startup fails (error window) | no container exists, nothing to dispose | agent: `Program.Main` | existing startup tests |

## Decisions
- Hook point: in `Program.Main`, after `App.Run` returns, before `Log.CloseAndFlushAsync`.
  `Program` built the container, so `Program` disposes it. rejected: the desktop `Exit` or
  `ShutdownRequested` events, because a handler runs on the UI thread inside the dispatcher loop:
  blocking it waits on a 5 s write while the UI is frozen, and an `async void` handler can lose the
  race with the dispatcher ending.
- Teardown runs on a pool thread (`Task.Run`), and `Main` waits on it with the bound. The container
  holds `IAsyncDisposable`-only services, so only `DisposeAsync` works (probe below). `Main` is the
  synchronous `[STAThread]` entry point, so the wait is the one allowed sync-over-async point, as
  the existing `Log.CloseAndFlushAsync` call already is.
- `S7Service.DisposeAsync` stops disposing its injected transport (`S7Driver`). The container
  created `S7Driver` and disposes it. `S7Driver` is already safe to dispose twice, so it gets no
  guard. `S7Service` keeps disposing its `PlcExecutionMonitor`: the `S7Di` factory creates the
  monitor and hands it only to `S7Service`, so `S7Service` is its only owner.
- Multiple dispose: only `S7Service.DisposeAsync` becomes idempotent; the first call does the work,
  later calls return at once. `PlcSyncCoordinator.Dispose` and `PlcLifecycleManager.Dispose`
  already guard with a disposed flag, and `ActiveRecipeGridSurface.Dispose` holds no state.
  rejected: forwarding registrations the container does not own. Microsoft.Extensions.
  DependencyInjection has no per-registration opt-out from disposal tracking: the probe shows every
  factory forward captured. The only way out is to build `S7Service` by hand and register the
  instance, which moves composition out of `S7Di`.
- Shutdown order: the PLC composition code in Core (`S7Di`, where `PlcSyncCoordinator` is
  registered and stays `internal`) owns one public teardown routine. It disposes the coordinator,
  then disposes `IS7Connection`, then awaits the container's `DisposeAsync`. It resolves the
  coordinator and the connection only when the container registers them. The container step runs
  in a `finally`, so a throw in the first two steps still disposes the container; the exception
  propagates (`ShutdownOrderTests.Teardown_WhenTheConnectionDisposeThrows_StillDisposesTheContainer`).
  rejected:
  `PlcLifecycleManager.Dispose` disposing `IPlcSyncService`, because it disposes an injected
  dependency (`csharp.md`); rejected: reordering registrations, because `PlcLifecycleManager`
  depends on `IPlcSyncService`, `IS7Reader` and `IS7ExecutionStream` and is therefore always
  captured after them.
- `Program` gets an internal member that takes the built `ServiceProvider` and the bound, runs the
  routine on a pool thread with that bound, logs a timeout as one Warning and a failure as one
  Error, and returns normally. `Main` is its production caller and passes the 10 s constant.
- `MainWindowViewModel.Dispose` stops disposing its injected `PlcMonitorViewModel`. The container
  owns that singleton and disposes it; the extra call breaks the injected-dependency rule.
- Exit bound of 10 s: the coordinator's write wait takes up to 5 s, and the S7 disconnect gets the
  remaining 5 s, which matches the execution monitor's own 5 s stop bound. A longer bound keeps the
  window gone but the process alive with no visible sign.
- `EmulatorLoopbackTests.ConnectedClient` disposes its container instead of only disconnecting, and
  its comment about a second dispose failing goes away.

## Caveats
- The container disposes singletons in reverse capture order and captures a factory result once per
  registration that resolved it.
  probed: temporary headless test, real `Program.BuildServices` with the shipped RIE config, the
  probe's resolve order (`MessagePanelViewModel`, `RecipeSession`, `RecipeCoordinator`,
  `GridStyleOptions`, `MainWindowViewModel`, `PlcLifecycleManager`), capture list read by
  reflection from the root scope. Microsoft.Extensions.DependencyInjection 11.0.0.0
  (11.0.0+297acf93). Captures: `S7Service` 4 times, `S7Driver` 2, `PlcSyncCoordinator` 2,
  `ActiveRecipeGridSurface` 2. Reverse order disposes `MainWindowViewModel` first, then
  `PlcLifecycleManager`, then `S7Service` twice, then `PlcSyncCoordinator`. Without the explicit
  steps the lease release and the disconnect both run before the write wait. Constructor
  dependencies fix the PLC part of this order whatever the resolve order. 2026-11-24.
- Sync `Dispose` on this container fails.
  probed: same test, `ServiceProvider.Dispose()` threw `AggregateException` with
  "'SemiStep.Core.Plc.S7.S7Service' type only implements IAsyncDisposable. Use DisposeAsync to
  dispose the container." and the same for `S7Driver`. 2026-11-24.
- The container's `DisposeAsync` continues past a service that throws and rethrows at the end.
  probed: temporary test, three `IAsyncDisposable` singletons. With one thrower the earlier and
  later services were both disposed and the original `InvalidOperationException` was rethrown;
  with two throwers it threw `AggregateException` holding both. 2026-11-24.
- Repeated `S7Service.DisposeAsync` does not throw today.
  probed: same test as the order probe, `DisposeAsync` on the full container, once disconnected and
  once connected to the in-process emulator (`EmulatorTestSupport.StartOrFail`): no exception, 3 ms
  connected. The repeat calls still rerun the disconnect and the monitor dispose. 2026-11-24.
- Cause of the old `ConnectedClient` failure: at 85c3246 (#205), when that comment was written,
  `PlcExecutionMonitor.Dispose` also disposed its `Subject`, so the second `S7Service.DisposeAsync`
  published into a disposed subject and threw `ObjectDisposedException`. d744d84 (#212) removed that
  call. The `S7Service` guard keeps a later change to the monitor from bringing the failure back.
- Unreachable PLC.
  probed: temporary headless test, real container with `--plc-endpoint 192.0.2.1:102`
  (RFC 5737 TEST-NET-1). `IS7Connection.ConnectAsync` failed with `SocketException` after 21.0 s,
  twice. Container `DisposeAsync` 1 s into a pending connect took 6 ms; after a failed connect,
  0 ms. 2026-11-24. The reconnect loop runs the same connect, and `DisconnectAsync` awaits that loop,
  so a reconnect attempt to an unreachable PLC can hold the teardown until the attempt fails; that
  case was not measured end to end, and the 10 s bound covers it.
- ASSUMPTION: `StartWithClassicDesktopLifetime` returns on the main thread after the last window
  closes; fallback: hook `desktop.Exit` to start the same pool-thread teardown and wait on it there
  with the same bound; checked by: smoke step 4.
- ASSUMPTION: the main thread may still carry Avalonia's synchronization context after the lifetime
  ends, so awaiting there could post continuations to a dispatcher that no longer runs; the
  pool-thread teardown avoids depending on it. fallback: none needed; checked by: smoke step 4.
- The teardown never awaits the UI dispatcher. The reconnect reconciliation runs fire-and-forget
  (`PlcLifecycleManager.OnConnectionStateChanged` starts `PerformReconnectReconciliationAsync`), and
  `PlcLifecycleManager.Dispose` only cancels its lifetime token; it does not wait for that work.
- ASSUMPTION: the view-model `Dispose` bodies only dispose subscriptions and commands and touch no
  control, so they run safely off the UI thread after the window closed (read:
  `MainWindowViewModel`, `PlcMonitorViewModel`, `RecipeCoordinator`, `MessagePanelViewModel`,
  `RecipeGridSurfaceBase`, `RecipeCommandsViewModel`, `ClipboardViewModel`, `RecipeFileViewModel`);
  fallback: dispose the UI singletons on the UI thread from `desktop.Exit`, the PLC part stays on
  the pool thread; checked by: Acceptance 1.
- The coordinator completes and disposes its subjects while view models are still subscribed.
  `PlcSyncCoordinator.Status` and `LastSyncTime` read fields under the state lock, not the subject,
  so a later view-model `Dispose` that reads them does not throw. Connection and sync callbacks that
  arrive after the dispose return without emitting: `PlcSyncCoordinator.Transition` checks the
  disposed flag under the state lock, and `Dispose` completes the subjects under the same emission
  gate. Checked by Acceptance 1.
- Log capture in tests swaps the static `Log.Logger` before the container is built. That is safe
  only because the test assembly disables parallelization (`AssemblyAttributes.cs`,
  `DisableTestParallelization = true`).

## Constraints and non-goals
- #180's contract holds: the coordinator is disposed before `PlcLifecycleManager` and before the
  container.
- `PlcSyncCoordinator` stays `internal` to Core.
- Disposal finishes before `Log.CloseAndFlushAsync`, so teardown warnings reach the log file.
- No new interface, no shutdown service, no configurable bound in production.
- Tests stay as simple as possible; existing fakes and the in-process emulator are reused.

## Acceptance
1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Shutdown"` → at least
   4 tests run, all pass, 0 failed. Among them a headless test in `SemiStep.Tests/UI/Startup` builds
   the real container with `Program.BuildServices` pointed at the in-process emulator
   (`--plc-endpoint`), connects, creates `MainWindow` with its view model from the container, closes
   it, then runs the `Program` teardown member on a pool thread. It finishes with no exception and
   the log holds exactly one "Disconnected from PLC" line.
2. Same filter: a test builds the `AddRecipe` and `AddS7` registrations with these overrides: the
   real `S7Driver` connected to the in-process emulator; a fake `IS7Transport` for the executor
   that answers the protocol version and holds a recipe write open; a fake `IPlcSyncOwnership` whose
   lease records its release. It enables sync, starts a write, starts the teardown routine on a pool
   thread, and releases the write. Observation points: the write log of the fake transport, the
   `Disconnected` event of `IS7Connection.StateChanged`, the lease release count. Asserted order:
   the write completes (commit byte written), then the disconnect, then the lease release (once).
   A second test notifies an edit and starts the teardown inside the debounce: the fake transport
   receives no write.
3. Same filter: the `Program` teardown member, given a container holding an `IAsyncDisposable`
   singleton that never completes and a short bound, returns normally after the bound and logs one
   Warning.
4. Same filter: the same member, given a container holding a singleton whose `DisposeAsync` throws,
   returns normally and logs one Error.
5. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
6. `dotnet format SemiStep.slnx --verify-no-changes` → exit 0.
7. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → all pass, 0 failed.
- manual smoke against the PLC emulator:
  1. Start the emulator:
     `dotnet run --project SemiStep/SemiStep.Tools.PlcEmulator/SemiStep.Tools.PlcEmulator.csproj -- --config-dir ConfigFiles/MBE --port 10102`
     → emulator window shows no client.
  2. Start the app:
     `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj -- --config-dir ConfigFiles/MBE --plc-endpoint 127.0.0.1:10102`,
     connect, enable sync → emulator shows one connected client; the app shows sync on.
  3. Edit a cell, wait 2 s, close the main window (choose Don't Save) → the emulator shows the
     client disconnected at once, not after a timeout.
  4. Read the app log → one "Disconnected from PLC" line after the window close, no teardown
     warning, no exception; the process is gone within 1 s; the sync ownership lock file is released
     (a second app instance can enable sync right away).
  5. Repeat 2, edit a cell and close the window within 1 s → exit is immediate; reconnect and the
     reconciliation reports the PLC recipe without that last edit.
- not visible to headless tests: `StartWithClassicDesktopLifetime` returning, the teardown running
  when `App.Run` throws, the real process exit, Windows session end.

---

## Context
- `SemiStep/SemiStep.UI/Program.cs`: `Main`, `BuildServices`, `StartupOutcome` (holds the provider
  as `IServiceProvider`; the owner needs the disposable type).
- `SemiStep/SemiStep.Core/Plc/S7/S7Di.cs`: `AddS7`, the forwards for `S7Service`, `S7Driver`,
  `PlcSyncCoordinator`.
- `SemiStep/SemiStep.Core/Plc/S7/S7Service.cs` `DisposeAsync`, `DisconnectAsync`.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs` `Dispose`;
  `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs` `Dispose`, `DisableSync`.
- `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` `Dispose`.
- Tests: `SemiStep/SemiStep.Tests/UI/Startup/StartupOptionsPlcEndpointTests.cs` builds the real
  container; `SemiStep/SemiStep.Tests/Emulator/EmulatorLoopbackTests.cs` `ConnectedClient`;
  `S7/PlcSyncCoordinatorWriteTokenTests.cs` and `S7/Helpers/TransportHold.cs` hold a write open;
  `Helpers/StubPlcSyncOwnership.cs`; `docs/architecture/headless-ui-testing.md`. New tests carry
  `[Trait("Area", "Shutdown")]`.

## Boundaries
- Task 2 builds, Task 3 calls: a public static method in `S7Di` that takes the built
  `ServiceProvider`, disposes the sync coordinator, then `IS7Connection`, then awaits the
  container's `DisposeAsync`, and returns a `ValueTask`. The implementing agent names it.
- Task 3 builds, its tests call: an internal static member of `Program` that takes the built
  `ServiceProvider` and a `TimeSpan` bound and returns when the teardown ends or the bound expires.

## Tasks

### Task 1: S7Service owns only what it creates, and disposes once
**Goal:** `S7Service.DisposeAsync` runs its teardown once and leaves the transport to the container
**Entry:** `S7Service.DisposeAsync`
**Likely touches:** `S7Service.cs`, S7 tests

- [x] `S7Service.DisposeAsync` no longer disposes the injected transport
- [x] guard `S7Service.DisposeAsync` so later calls return at once
- [x] tests: `S7Service.DisposeAsync` does not dispose the transport; a second call does not
  disconnect again
- [x] run tests - must pass before the next task

### Task 2: Shutdown-order teardown routine
**Goal:** one Core routine disposes the coordinator, then the connection, then the container
**Entry:** `S7Di`
**Likely touches:** `S7Di.cs`, `MainWindowViewModel.cs`, `UIFixture.cs`,
`EmulatorLoopbackTests.cs`, new tests under `SemiStep.Tests/S7`

- [x] add the routine from `## Boundaries`
- [x] `MainWindowViewModel.Dispose` no longer disposes the injected `PlcMonitorViewModel`;
  `UIFixture` disposes the `PlcMonitorViewModel` it builds
- [x] tests: Acceptance 2, both the held write and the debounced edit
- [x] `EmulatorLoopbackTests.ConnectedClient` disposes its container; drop its second-dispose comment
- [x] run tests - must pass before the next task

### Task 3: Bounded teardown at exit in Program
**Goal:** `Program.Main` runs the routine once after `App.Run`, on a pool thread, bounded by 10 s
**Entry:** `Program.Main`
**Likely touches:** `Program.cs`, new tests under `SemiStep.Tests/UI/Startup`

- [x] add the `Program` member from `## Boundaries`; `Main` keeps the built container as its owner
  type and calls the member with 10 s in a `finally` around `App.Run`, before the log flush
- [x] timeout logs one Warning, an exception logs one Error; the exit code does not change
- [x] tests: Acceptance 1, 3 and 4
- [x] run tests - must pass before the next task

### Task 4: Verify acceptance
**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance 1-7 pass with their expected results
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] manual smoke steps 1-5 (skipped - operator smoke)

### Task 5: Update documentation
**Goal:** docs describe the exit teardown and its order
**Entry:** `docs/architecture/exit-flow.md`

- [x] `exit-flow.md` "PLC recipe write at exit": replace the #112 note with the teardown routine,
  the shutdown order, the 10 s bound and the dropped debounced edit; state which parts the headless
  tests cover and which stay manual
- [x] `docs/architecture/build-and-deployment.md` or `CLAUDE.md` only if a convention changed
- [x] `docs/readme.md` 4.1: the exit with sync on (added in review round 1); section 5: the exit log
  lines

## Post-Completion
- the operator runs the manual smoke above, including the items not visible to headless tests
- closes #112; the closing comment on #112 says that an edit still in the debounce at exit is
  dropped on purpose (#180: only a started write completes)
