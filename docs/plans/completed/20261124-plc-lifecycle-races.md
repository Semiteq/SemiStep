# PLC connection lifecycle races

## Behavior
- When the execution monitor's poll detects a lost link, the app today stays in "disconnected" for about
  6 s before the first reconnect attempt shows "connecting": 5 s of a blocked self-wait in
  `PlcExecutionMonitor.Stop`, then the 1 s reconnect delay. After the change the first attempt starts after
  the 1 s reconnect delay alone.
- The poll, keep-alive and reconnect loops run on the thread pool, whatever thread calls connect. Today
  they resume on the UI thread after a connect from the UI, so a keep-alive-detected loss blocks the UI for
  up to 5 s while `Stop` waits for a poll loop that needs the UI thread.
- When the operator presses disconnect while a connection loss is being handled, the app stays
  disconnected. No reconnect attempt starts after the operator's disconnect, whatever the order of the two
  events.
- After a disconnect that raced a connection loss, the next session still reconnects automatically. Today
  such a race can leave a finished reconnect loop registered, and every later connection loss in the
  session then starts no reconnect.
- Concurrent stop requests to the execution monitor (keep-alive thread and operator disconnect) never throw
  `ObjectDisposedException` and never leave a poll loop running.
- An operation that starts after the driver dropped its PLC instance fails with `NotConnectedError` ("Not
  connected to PLC", localized) instead of the `PlcCommandFailedError` headline "PLC command failed" with a
  `NullReferenceException` in its cause chain and in the warning log line.
- Scope guard: reconnect delays (1 s initial, doubling to 30 s), the keep-alive interval, the 5 s monitor
  stop timeout, the operator-facing texts, and the code of `PlcSyncExecutor` and `PlcSyncCoordinator` stay
  as they are.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Connect is called from the UI thread | the poll, keep-alive and reconnect loops start on the thread pool with no synchronization context, so no loop step and no wait in `Stop` touches the UI thread | operator (manager decision); probe 1 | Acceptance 3 |
| Poll loop reads `NotConnectedError` while its own cancellation is not set | the loop detaches itself from the monitor, then raises the connection-lost callback outside the monitor lock; `S7Service.OnConnectionLost` calls `Stop`, which finds no running loop and returns at once; reconnect starts after 1 s | agent: `PlcExecutionMonitor.PollLoopAsync` raises the callback from inside the loop task, probe 2 | Acceptance 2, 3 |
| Poll loop's cancellation is set (by `Stop`, `StopAsync`, `Dispose` or a newer `Start`) | the loop only exits: no publish, no callback, no lock. This is what makes a cancel inside the monitor lock safe, even when the cancel runs the loop's continuation inline | agent: `Stop` and `Start` always cancel a loop before they replace it | not visible to headless tests |
| A poll read fails with `NotConnectedError` because the driver dropped its PLC instance mid-session | same as the row above it: the loop raises the connection-lost callback and ends. Today such a read fails with `PlcCommandFailedError`, the loop keeps polling, and the next tick's connection pre-check ends it | agent: consequence of the Task 3 mapping | Acceptance 5 |
| Keep-alive probe fails while the poll loop runs | `OnConnectionLost`, now on a pool thread, calls `Stop`, which cancels the loop and waits for it up to 5 s; a timeout is logged as a warning, the same as `StopAsync` | agent: `PlcExecutionMonitor.StopAsync` already logs the timeout, `Stop` swallows it | not visible to headless tests |
| Old poll loop is mid-read when `Stop` returns | the stopped loop drops its snapshot because its own cancellation is set; the window between that check and the publish is closed by the wait in `Stop`, so it stays open only when the 5 s wait times out, and that timeout is logged | agent: `Stop` publishes `PlcExecutionInfo.Empty` after its wait | not visible to headless tests |
| `Stop`, `Start` and `StopAsync` called from several threads at once | each call detaches and cancels the current loop exactly once; at most one loop runs afterwards; Subject writes are serialized | agent: issue finding 2, `csharp.md` rule on multi-writer subjects | Acceptance 2 |
| A loop that detached itself is still inside the callback when `StopAsync` or `Dispose` returns | `Dispose` completes the Subject without disposing it, so the late `Stop` from the callback publishes into a completed Subject, which ignores it | agent: `S7Service.DisposeAsync` calls `DisconnectAsync`, then `PlcExecutionMonitor.Dispose`, while a callback can still run on the loop thread; probe 4, probe 5 | Acceptance 2 |
| Operator disconnects while `OnConnectionLost` runs (inside its `StateChanged` callback, or between the state flip and the reconnect start) | the reconnect start re-checks the auto-reconnect flag in the lock section that creates the loop, and the disconnect clears the flag and stops the loop in one lock section: either the loop starts first and the disconnect stops it, or the disconnect clears the flag first and no loop starts | agent: issue finding 3; `StateChanged` and the monitor's Empty publish run subscribers synchronously, probe 4 | Acceptance 4 |
| A lagging `OnConnectionLost` (held up to 5 s in `PlcExecutionMonitor.Stop`) resumes after the operator disconnected and connected again | the reconnect start also requires the Disconnected state in its lock section, so it sees the newer session's Connected state and starts no loop | agent: review round 1; the flag alone is set again by the newer connect | Acceptance 4 |
| Any reconnect loop exit (success, cancellation) | the exit leaves no task handle and no token source behind; a loop clears them only while they are still its own, so a newer loop is never cleared; a cancellation thrown by a connect attempt itself counts as a failed attempt and backs off | agent: on master `S7Service.ReconnectLoopAsync` cleared the task handle only on success, and its flag-cleared exit left it set, which blocked every later `StartReconnectLoop` | Acceptance 4 |
| A loss arrives after a reconnect sets Connected and before that reconnect loop releases its handles | the loss finds the finishing loop still registered and starts none; the release then starts a new loop in the same lock section, because the state is Disconnected and the flag is set. `StartKeepAlive` starts nothing unless the state is Connected, so no keep-alive outlives the session that the loss ended | agent: review round 1; master has the same window | Acceptance 4 |
| Read or write on an `S7Driver` with no PLC instance (never connected, or disconnected) | the driver raises its internal not-connected exception; `PlcTransactionExecutor` maps it to `NotConnectedError` and logs one Debug line with no stack trace | agent: `S7Driver.ReadBytesAsync` and `WriteBytesAsync` null-forgive the PLC instance; every `PlcTransactionExecutor` catch site wraps every exception in `PlcCommandFailedError` | Acceptance 5 |
| A sync check races a disconnect | `PlcSyncExecutor.CheckCanSyncAsync` receives `NotConnectedError` and logs its existing Warning "Sync blocked: not connected to PLC" instead of nothing; the executor no longer logs a Warning with the `NullReferenceException` stack. `PlcSyncExecutor.WriteSyncAsync` logs nothing in either case, as today | agent: `PlcSyncExecutor` branches on `NotConnectedError` already; its code does not change | Acceptance 5 |
| `Close` runs while a read is already on the wire | unchanged: S7netplus fails the read with its own transport exception, which stays `PlcCommandFailedError` | agent: the S7netplus `Close` caveat | manual smoke |

## Decisions
- The poll, keep-alive and reconnect loops start through the thread pool, so they never capture the
  caller's synchronization context. Rejected: `ConfigureAwait(false)` on every `await` in Core, which is
  wide and breaks again with one missed `await`. Rejected: leaving the loops where the caller runs them,
  which keeps the 5 s UI block of the keep-alive path.
- Re-entrant stop: the poll loop detaches itself from the monitor before it raises the connection-lost
  callback, so a `Stop` from inside the callback has nothing to wait for. `Stop` keeps its bounded wait for
  the external caller (keep-alive path) and logs its timeout. This deviates from the issue's "keep the
  blocking wait only in Dispose". The wait is safe because every loop runs on the thread pool, so it never
  blocks the UI thread and never waits for a loop that needs the waiting thread. The wait closes the window
  between a loop's cancellation check and its publish without a publish under a lock. Rejected: a flag set
  around the callback, because an instance flag also makes a concurrent `Stop` from another thread skip its
  wait, and a thread-static flag is static mutable state (`base.md`). Rejected: dropping the wait from
  `Stop`, because the keep-alive path could then see a late snapshot after Empty.
- One `Lock` in `PlcExecutionMonitor` guards the loop handle and its cancellation source. No task wait and
  no Subject publish runs while it is held. Subject writes go through a synchronized subject, because the
  poll loop and the stop callers write from different threads (`csharp.md`). `Dispose` completes the
  Subject and does not dispose it. This matches `S7Service`, which guards its loop handles with its state
  lock.
- The auto-reconnect flag and the connection settings live under the existing state lock in `S7Service`.
  `DisconnectAsync` clears the flag and stops the reconnect loop in one lock section; the reconnect start
  re-checks the flag and the Disconnected state in the lock section that creates the loop;
  `ConnectInternalAsync` reads the settings once under the lock. Every reconnect loop exit clears its own
  task handle and token source, and in the same lock section starts the next loop when the state is
  Disconnected and the flag is set. `StartKeepAlive` starts a keep-alive only in the Connected state.
  Rejected: deciding at the state flip only, because `StateChanged` subscribers run between the flip and
  the loop start.
- `S7Driver` raises a dedicated internal not-connected exception when it has no PLC instance, and every
  `PlcTransactionExecutor` catch site maps it to `NotConnectedError`. That exception never reaches a caller
  above `S7Service`. The executor logs one Debug line without the stack. The `S7Service` keep-alive loop
  also calls the driver directly and treats any exception as a lost connection. The
  `PlcNotConnectedException` removed in commit `14368b2` was different: it crossed layers as control flow,
  and callers caught it. This mapping also closes the gap between the executor's `IsConnected` pre-check
  and the read. Rejected: a plain
  `InvalidOperationException`, which keeps the "PLC command failed" headline for a routine disconnect.
  Rejected: an `IS7Transport` that returns `Result`, which rewrites every transport and fake.
- `S7Driver.DisconnectAsync` stays outside the transport serializer, so a disconnect never waits behind a
  stalled read.
- Tests use only the existing seams: the injected connection-lost callback, `IS7Driver` with
  `FakeS7Driver`, and the internal `S7Service.OnConnectionLost`. No production member is added or widened
  for tests. A test that passes on master too is labelled a contract pin.

## Caveats
1. The loops capture the UI synchronization context today. probed: a temporary `[AvaloniaFact]` connected
   `S7Service` over a recording `IS7Driver` inside the headless dispatcher; the caller had
   `AvaloniaSynchronizationContext`, and every poll read (DB 6) and keep-alive read (DB 2) ran on the
   caller's thread with `SynchronizationContext.Current` equal to `AvaloniaSynchronizationContext`,
   2026-11-24. In the headless runner the dispatcher thread is itself a pool thread, so the regression test
   asserts on the synchronization context, not on `Thread.IsThreadPoolThread`.
2. For an async method, `Task.CurrentId` inside the method after its first `await` is `null`, never the
   returned task's `Id`, so the guard in `PlcExecutionMonitor.Stop` never fires, and a self-wait burns its
   full timeout. probed: file-based `dotnet run` app on SDK 11.0.100; result `Task.CurrentId=null`,
   `equal=False`, `Wait(500 ms)` on itself returned `False` after 513 ms, 2026-11-24.
3. `System.Threading.Lock` allows nested entry by the thread that holds it. probed: same app, nested `lock`
   on one `Lock` instance succeeded with `IsHeldByCurrentThread=True`, 2026-11-24. `DisconnectAsync` can
   stop the reconnect loop inside its own flag section, and an inline continuation run by `Cancel` inside
   the monitor lock does not deadlock.
4. `Subject<T>.OnNext` from System.Reactive 6.1.0 runs subscribers synchronously on the calling thread, so
   the Empty publish inside `Stop` and the `StateChanged` event both run caller code in the middle of
   `OnConnectionLost`. probed: same app, subscriber thread id equal to caller thread id, 2026-11-24.
5. `Subject<T>.OnNext` after `OnCompleted` alone is ignored and does not throw; after `OnCompleted` and
   `Dispose` it throws `ObjectDisposedException`. probed: the temporary `[AvaloniaFact]` above printed
   `OnNext after OnCompleted: no throw`, and the file-based app printed `ObjectDisposedException` for the
   disposed case, 2026-11-24.
6. The UI already marshals PLC events: `RecipeCoordinator` applies `ObserveOn(RxSchedulers.MainThreadScheduler)`
   to `ExecutionState`, `PlcState` and `Faults`, so `StateChanged` and execution snapshots raised from pool
   threads are safe for the UI.
7. ASSUMPTION: S7netplus 0.20.0 `ReadBytesAsync` honours its `CancellationToken`; fallback: when a read
   ignores cancellation, `Stop` on the keep-alive path waits its full 5 s on a pool thread and logs the
   timeout warning, the UI stays responsive, and reconnect starts 5 s late; checked by: smoke step 2 (no
   "did not stop within 5 seconds" warning in the log).
8. ASSUMPTION: S7netplus 0.20.0 `Plc.Close` during an in-flight `ReadBytesAsync` fails that read with a
   socket or S7netplus exception, not a `NullReferenceException` from our code; fallback: route
   `S7Driver.DisconnectAsync` through the transport serializer so `Close` waits for the in-flight
   operation; checked by: smoke step 4.

## Constraints and non-goals
- The code of `PlcSyncExecutor` and `PlcSyncCoordinator` does not change. The S7-1500 CPU type in
  `S7Driver.ConnectAsync` and container disposal are not touched.
- `S7Driver.ConnectAsync` replaces an earlier PLC instance without closing it after a connection loss. That
  leak is outside the four findings and stays as it is.
- Connect takes no `CancellationToken`; a reconnect attempt that is already connecting runs to its end, and
  `DisconnectAsync` waits for it. That stays as it is.
- Threat model: concurrent calls from the poll loop, the keep-alive loop, the reconnect loop and the UI
  path. A misbehaving subscriber or a hostile PLC is out of scope.
- Every mutable field the change shares across threads carries a comment that names its threads
  (`csharp.md`).
- Tests stay as simple as possible; concurrency stress tests are bounded and short.

## Gates
- None.

## Acceptance
1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ExecutionMonitor"` → 0
   failed. Includes: a `Stop` called from inside the connection-lost callback returns in under 1 s (fails
   on master: about 5 s); concurrent `Start` and `Stop` from several threads throw nothing and leave no
   loop polling (contract pin, may pass on master by chance); `Stop` after `Dispose` throws nothing (fails
   on master: `ObjectDisposedException`).
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~S7ServiceTests"`
   → 0 failed. Includes: after a connect from inside the headless dispatcher, every driver call from the
   poll, keep-alive and reconnect loops runs with no synchronization context (fails on master); after a
   poll-detected loss the service is connected again within 3 s (fails on master: about 6 s).
4. Same command as item 3 → also includes: a disconnect issued from the `StateChanged` handler during
   `OnConnectionLost` leaves no `Connecting` state after a 1.5 s settle (contract pin; master has no hook
   inside its race window); after that raced disconnect, a new connect and a connection loss reconnect
   again (contract pin); loss, reconnect, loss, reconnect completes both reconnects (contract pin).
5. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Driver|FullyQualifiedName~PlcTransactionExecutorTests"`
   → 0 failed. Includes: read and write on a never-connected `S7Driver` raise the not-connected exception,
   not `NullReferenceException` (fails on master); an executor read and a recipe write whose transport
   raises it return `NotConnectedError` (fails on master: `PlcCommandFailedError`).
6. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=S7"` → 0 failed, more
   than 147 total (baseline 147 passed on 2026-11-24).
7. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed.
8. `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
- Not visible to headless tests: release of the reconnect token source; the silent exit of a cancelled
  poll loop; the logged timeout of the wait in `Stop`; a dropped snapshot of a loop stopped mid-read; the
  S7netplus behavior in caveats 7 and 8.

---

## Context
- `SemiStep/SemiStep.Core/Plc/Sync/PlcExecutionMonitor.cs`: `Start`, `Stop`, `StopAsync`, `Dispose`,
  `CancelAndDetachPollTask`, `PollLoopAsync`.
- `SemiStep/SemiStep.Core/Plc/S7/S7Service.cs`: `ConnectAsync`, `DisconnectAsync`, `OnConnectionLost`,
  `ConnectInternalAsync`, `StartKeepAlive`, `StartReconnectLoop`, `StopReconnectLoop`, `ReconnectLoopAsync`.
- `SemiStep/SemiStep.Core/Plc/S7/S7Driver.cs`: `ReadBytesAsync`, `WriteBytesAsync`, `DisconnectAsync`;
  `TransportSerializer.cs` runs the operation after its gate.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs`: five `catch` sites that wrap exceptions in
  `PlcCommandFailedError`; `NotConnectedError` lives in `SemiStep/SemiStep.Core/Plc/S7/Protocol/`.
- `S7Di.AddS7` wires the connection-lost callback to `S7Service.OnConnectionLost`; the test helper
  `S7ServiceTests.BuildService` wires it the same way.
- Tests: `SemiStep/SemiStep.Tests/S7/PlcExecutionMonitorTests.cs`, `S7ServiceTests.cs`,
  `PlcTransactionExecutorTests.cs`, helpers `Helpers/FakeS7Driver.cs`, `Helpers/FakeS7Transport.cs` and
  `Helpers/FakeExecutionTransport.cs`, `SemiStep.Tests/Helpers/TestHelpers.WaitUntilAsync`. A negative wait
  uses the "(c) Defensive settle" comment form of `S7ServiceTests`. Headless tests use `[AvaloniaFact]`
  (`docs/architecture/headless-ui-testing.md`).
- Error assertions check types, not wording (`docs/architecture/error-reporting.md`).

## Tasks

### Task 1: Execution monitor stops without self-wait and without races

**Goal:** a stop from inside the connection-lost callback returns at once, concurrent start and stop are
safe, the poll loop runs on the thread pool, and the remaining blocking wait logs its timeout
**Entry:** `PlcExecutionMonitor.PollLoopAsync` and `CancelAndDetachPollTask`
**Likely touches:** `PlcExecutionMonitor.cs`, `PlcExecutionMonitorTests.cs`

- [x] the loop handle and its cancellation source change only under one lock; no wait and no publish runs
      inside it; the poll loop starts on the thread pool
- [x] a loop that reads `NotConnectedError` with its cancellation not set detaches itself, then raises the
      callback outside the lock; a cancelled loop only exits
- [x] a cancelled loop publishes nothing; `Stop` logs its wait timeout like `StopAsync`; the
      `Task.CurrentId` guard is gone; Subject writes are serialized; `Dispose` completes the Subject without
      disposing it
- [x] tests: a `Stop` called from inside the callback returns in under 1 s; concurrent `Start` and `Stop`
      throw nothing and leave the read count still after the final `Stop`; `Stop` after `Dispose` throws
      nothing; existing monitor tests pass
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ExecutionMonitor"` - must pass before the next task

### Task 2: S7Service loops run on the pool and reconnect is atomic against disconnect

**Goal:** no loop runs on the caller's synchronization context, an operator disconnect always wins over a
concurrent connection loss, and every reconnect loop exit releases its state
**Entry:** `S7Service.OnConnectionLost`, `StartKeepAlive` and `StartReconnectLoop`
**Likely touches:** `S7Service.cs`, `S7ServiceTests.cs`, `Helpers/FakeS7Driver.cs`

- [x] the keep-alive and reconnect loops start on the thread pool
- [x] the auto-reconnect flag and the connection settings are read and written only under the state lock
- [x] a disconnect clears the flag and stops the reconnect loop in one lock section; the reconnect start
      re-checks the flag in the section that creates the loop
- [x] every reconnect loop exit clears its own task handle and disposes its own token source
- [x] tests: after a connect inside the headless dispatcher, driver calls from all three loops run with no
      synchronization context; after a poll-detected loss the service is connected again within 3 s; a
      disconnect from the `StateChanged` handler during `OnConnectionLost` leaves no `Connecting` state
      after a 1.5 s settle; after that raced disconnect, a new connect and a loss reconnect again; loss,
      reconnect, loss, reconnect completes both reconnects
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~S7ServiceTests"` - must pass before the next task

### Task 3: S7Driver reports not-connected instead of a null dereference

**Goal:** an operation that starts after the driver dropped its PLC instance fails with `NotConnectedError`
**Entry:** `S7Driver.ReadBytesAsync` and `WriteBytesAsync`
**Likely touches:** `S7Driver.cs`, a new exception file in `SemiStep.Core/Plc/S7/`,
`PlcTransactionExecutor.cs`, a new `S7DriverTests.cs` (traits `Component=S7`, `Area=Driver`,
`Category=Unit`), `PlcTransactionExecutorTests.cs`

- [x] a read or write takes the PLC instance when the serialized operation starts; with no instance it
      raises the not-connected exception; the instance is safe to read from pool threads
- [x] every executor catch site turns the not-connected exception into `NotConnectedError` and logs one
      Debug line without the exception
- [x] tests: read and write on a never-connected `S7Driver` raise the not-connected exception; an executor
      read and a recipe write whose transport raises it return `NotConnectedError`
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Driver|FullyQualifiedName~PlcTransactionExecutorTests"` - must pass before the next task

### Task 4: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed
- [x] `terse` over the touched files exits 0 (global tool `terse` 1.1.0; the repository pins no local
      tool manifest)

### Task 5: Update documentation

**Goal:** docs describe where `NotConnectedError` comes from
**Entry:** `docs/architecture/error-reporting.md`, section "PLC faults localize by type"

- [x] state that `PlcTransactionExecutor` returns `NotConnectedError` both from its connection pre-check and
      when the driver reports a dropped PLC instance mid-operation
- [x] check `docs/readme.md` (reconnect and keep-alive passages) for any statement the change makes false;
      it stays in Russian

## Post-Completion
- Manual smoke with the emulator (`SemiStep/SemiStep.Tools.PlcEmulator/README.md`, run configuration
  `Emulator and SemiStep MBE`):
  1. connect, then close the emulator → the status shows disconnected within the keep-alive interval
     (`keep_alive_interval_ms`, 5000 by default)
  2. this checks the keep-alive path, which detects the loss when the emulator closes: the window stays
     responsive during the drop, the first `Connecting to PLC at` log line follows `PLC connection lost`
     by about 1 s, and the log has no "did not stop within 5 seconds" warning; start the emulator again →
     the app reconnects
  3. close the emulator, then press disconnect while the app is reconnecting → it stays disconnected after
     the emulator starts again
  4. start a recipe sync and press disconnect during it → the log has no `NullReferenceException`
- closes #108
