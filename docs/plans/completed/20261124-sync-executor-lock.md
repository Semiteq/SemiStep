# Sync state machine in one lock

## Behavior
- The operator sees no change. Sync status, the last sync time, the 1 s debounce, faults in the
  panel and the enable/disable flow stay as they are today.
- `PlcSyncCoordinator` owns the whole sync state under one lock: status, enabled flag, connection
  state, pending recipe, debounce, in-flight sync task and the disposed flag. `PlcSyncExecutor`
  holds no state and no lock. It runs two steps for the coordinator, a pre-check and a write, and
  returns each outcome as a value.
- After `Dispose` starts, no new debounce starts. When the in-flight write ends within the dispose
  bound, no write reaches the PLC after `Dispose` returns.
- An outcome that arrives after `ResetForDisable`, `HandleConnectionLost` or `Dispose` changes
  nothing: no status, no last sync time, no fault, no new debounce.
- No `OnNext` on `PlcState` or `Faults` runs while the state lock is held. A subscriber that reads
  `Status` or `IsSyncEnabled` from another thread during an emission gets its answer.
- Emissions stay serialized and in state order. The last snapshot on `PlcState` equals the current
  state. Nothing is emitted after the streams complete.
- The bounded wait for the in-flight sync task in `Dispose` is a named constant.
- Scope guard: `IPlcSyncService` is unchanged. `PlcExecutionMonitor`, `S7Service`, the write
  protocol, the cancellation of an in-flight write on disable and on `Dispose` (#180) and container
  disposal (#112) are unchanged.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Valid recipe change, no sync running | debounce 1 s, pre-check, status `Syncing`, write, status `Synced`, last sync time set | agent: today's behavior, `PlcSyncExecutor.StartDebounce`, `WriteSyncAsync` | existing `Area=SyncCoordinator` tests |
| Valid change while a write runs | recipe queued; after a successful write a new debounce starts and a second transaction follows | agent: today's behavior, `PlcSyncExecutor.OnRecipeChanged`, `WriteSyncAsync` | new re-debounce test (Task 1) |
| Valid change while a write runs, then the write fails | status `Failed`, one fault; the queued recipe waits for the next change, no second transaction | agent: today's behavior, `WriteSyncAsync` returns before the re-debounce | new failed-write test (Task 1) |
| Invalid recipe | pending recipe cleared and status `OutOfSync` | agent: today's behavior, `PlcSyncCoordinator.NotifyRecipeChanged` | existing tests |
| Debounce fires while disconnected | pre-check skips, status unchanged (never `Syncing`), no fault | agent: `PlcSyncExecutor.CheckCanSyncAsync` | existing debounce-abort test |
| Recipe active on PLC, or the active-flag read fails | pre-check fails: status `Failed` without `Syncing` first, one typed fault, no write | agent: `CheckCanSyncAsync` | existing recipe-active test |
| Write fails, or a step throws | status `Failed`, one typed fault (`PlcCommandFailedError` for a throw) | agent: `WriteSyncAsync`, `StartDebounce` catch | existing write-failure test |
| `ResetForDisable` | pending debounce cancelled, pending recipe cleared, status `Idle`. An in-flight write is still cancelled through the same token | agent: today's behavior; the torn write is #180 | existing tests |
| `HandleConnectionLost` | reset as above, one `ConnectionLostError`; while the status is `Syncing` it becomes `Failed` in the same transition, with no second fault | agent: today's behavior; manager decision (review round 1) for the `Syncing` case | existing tests; new connection-loss status test |
| Outcome arrives after `ResetForDisable`, `HandleConnectionLost` or `Dispose` | outcome dropped: status, last sync time and faults unchanged. When the dropped outcome is `Syncing`, the attempt returns before the write and no transport write starts. After a reset, a recipe queued since then still gets a debounce once the stale attempt ends; after `Dispose`, nothing starts | manager decision; the queued-recipe case keeps today's re-debounce | new stale-outcome test (Task 1); new stale-write and restart tests (review round 1) |
| Valid change after the attempt's final outcome, before its task ends | after `Synced` with nothing queued, a debounce starts at once; after a skipped pre-check or a dropped outcome, the attempt's end starts it | manager decision (review round 1) | new queued-inside-`Synced` test |
| `Dispose` while a write runs and a recipe is queued | waits up to the named bound for the write; no new debounce; when the write ends inside the bound, no write after `Dispose` returns; streams complete | agent: finding 2 | new dispose-during-write test (Task 1) |
| `Dispose` while a write outlasts the bound | `Dispose` returns after the bound; the write may still finish afterwards; its outcome is dropped | agent: today's behavior, `PlcSyncExecutor.Dispose` | code review only |
| Subscriber on `PlcState` or `Faults` reads `Status`/`IsSyncEnabled` from another thread inside `OnNext` | the read returns | agent: finding 3 | new no-deadlock test (Task 2) |
| Several threads change state at once | emissions never overlap; last snapshot equals the current state | agent: Rx serialization rule | new concurrent-emission test (Task 2) |
| `Dispose` races an emission from another thread | no `ObjectDisposedException` escapes; no emission after completion | agent: probe below | new dispose-race test (Task 2) |
| Call after `Dispose` | ignored | agent: today's behavior | existing dispose test |

## Decisions
- Merge the state into `PlcSyncCoordinator` and keep `PlcSyncExecutor` as a stateless, lock-free
  pair of steps. Reason: the defect is one invariant split over two files that share one `Lock`
  (the `PlcSyncCoordinator` constructor passes its own lock and three callbacks to
  `PlcSyncExecutor`). With all state in one class there is one state lock and no cross-file lock
  rule. The steps (connection check, active-flag read, write, logging) need no state and return
  outcomes as values, so the callback bundle goes away. #180 then separates the debounce token from
  the write token in one class and passes the write token to the write step. rejected: (a) the
  issue's split variant, executor with its own lock plus a status-sink interface. The sink has one
  implementation and one consumer, which the project's Interface Design rule forbids, and two locks
  over one state machine still leave status and debounce changes in separate acquisitions. (b)
  Merge the steps into the coordinator too: it adds about 90 lines of I/O and logging that need no
  lock to the class that holds the lock, for no gain in correctness.
- Two steps, not one: the coordinator sets `Syncing` between the pre-check and the write, at the
  same moment as today. A skipped pre-check therefore never shows `Syncing`, and a blocked one goes
  straight to `Failed`. rejected: one combined step, which cannot report `Syncing` at today's moment
  without a callback.
- Size estimate from today's 198 lines (coordinator) and 260 lines (executor): the coordinator ends
  near 330 lines, over the 300-line preference. Accepted: the only further split is the emission
  part, and moving it out puts the lock order back across two files.
- An outcome is applied only when the coordinator is not disposed and the attempt is still current.
  Both are checked in the same state-lock acquisition that applies the outcome. The attempt is
  current while its debounce token source is still the coordinator's current one. An attempt whose
  `Syncing` outcome is dropped returns before the write: `Cancel` runs after the gate is released,
  so the token may still be live, and the first transport call would write `Committed = false`.
  `ResetForDisable` and `HandleConnectionLost` replace that source, so their stale outcomes drop by
  the currency check. `Dispose` keeps the source and sets the disposed flag, so the disposed check
  alone drops its stale outcome and blocks the re-debounce. A recipe queued after a reset waits for
  the running stale attempt, as today; when that attempt ends, the coordinator starts a debounce for
  it, so two writes never overlap and the queued recipe is not lost. rejected: a separate generation
  counter, a second field that tracks what the token source already identifies.
- `NotifyRecipeChanged` queues while a lock-owned running-attempt field is set, compared by attempt
  reference. The attempt's final outcome clears it in the same acquisition (`Synced` with nothing
  queued, `Failed`); otherwise the attempt's end clears it in the acquisition that decides the
  restart. A re-debounce from `CompleteWriteLocked` replaces the field, so its predecessor's end does
  not clear it. rejected: `_syncTask.IsCompleted`, which turns true outside the state lock, so a recipe
  queued after the attempt's last pending check waited for the next edit.
- Emission goes through one emission gate, separate from the state lock. Under the gate the
  coordinator changes and reads state under the state lock, releases the state lock, then calls
  `OnNext`. Every emission checks the disposed flag after it takes the gate.
- Lock order: gate, then state lock, never the reverse. No lock is held across an `await` or a
  blocking wait. Every `Cancel()` and `Dispose()` of a `CancellationTokenSource` runs after the
  state lock is released: the coordinator swaps the source under the lock and cancels the old one
  outside it, because `Cancel()` runs registered callbacks on the calling thread.
- `Dispose` sets the disposed flag under the state lock, cancels the debounce token outside it,
  waits for the sync task with no lock held, then completes and disposes both subjects under the
  gate. The debounce token is also the write token, so `Dispose` still cancels an in-flight write,
  as today; separating the two is #180.
- Why a lock held during `OnNext` is allowed here: `csharp.md` requires a Subject written from
  several threads to be serialized (`Synchronize()`), and every serializer that does not queue holds
  a gate during `OnNext`. What finding 3 forbids is holding the state lock, which the property
  getters take. The getters and every other reader never take the gate; only the emission paths
  and `Dispose` do. rejected: (a) capture inside the lock and emit with no gate, because two threads
  can then emit in reverse order and leave the `BehaviorSubject` stale, and an emission can race
  `Dispose`. (b) `Synchronize()` on the subjects: it serializes `OnNext` but the snapshot is built
  before its gate, so the reverse-order case stays, and it does not order completion after the last
  emission. (c) A queue-drain serializer that holds no lock during `OnNext`: more code, and it only
  helps a subscriber that blocks on another emitting thread, which no subscriber does.
- The dispose wait bound is a named constant in `PlcSyncCoordinator`, 5 s as today. It is not shared
  with `PlcExecutionMonitor`'s stop timeout: the two bound different waits (poll loop exit, recipe
  write finish), #180 sizes the write bound against shutdown on its own, and `PlcExecutionMonitor`
  belongs to the #108 lane. rejected: one shared constant, which needs an edit in
  `PlcExecutionMonitor.cs` and couples two bounds that will diverge.
- `IDisposable` on `PlcSyncExecutor`: the executor has nothing to dispose after the change, so it
  drops `Dispose` and `IDisposable`. The coordinator, which owns the task, is the disposable.
- Finding 2 is closed by structure: the coordinator checks the disposed flag and starts the
  re-debounce in one acquisition of the state lock. The two-acquisition window cannot be hit
  deterministically from a test. The dispose-during-write test guards the end state: it lets the
  attempt finish as written after `Dispose` began, so the re-debounce branch runs, and removing the
  disposed check turns it red.

## Caveats
- `System.Threading.Lock` is re-entrant on the same thread, so a synchronous subscriber that calls
  back into the coordinator on the emitting thread does not self-deadlock on the gate.
  probed: console app on net11.0, nested `lock` on one `Lock`, ran, 2026-11-24.
- `OnNext` on a disposed `BehaviorSubject` or `Subject` throws `ObjectDisposedException`; `OnNext`
  after `OnCompleted` without `Dispose` is a silent no-op. This is why completion and emission share
  the gate. probed: console app, System.Reactive 6.1.0, 2026-11-24.
- `Task.Wait(TimeSpan)` returns `false` on timeout and does not throw.
  probed: console app, 200 ms wait on a 2 s delay returned `false` after 207 ms, 2026-11-24.
- ASSUMPTION: `Subject<T>` does not serialize concurrent `OnNext` calls; the Rx contract leaves
  that to the caller. fallback: none needed, the gate serializes either way. checked by: Task 2
  concurrent-emission test.
- ASSUMPTION: `CancellationTokenSource.Cancel()` runs registered callbacks synchronously on the
  calling thread. fallback: none needed, cancelling outside the lock is safe either way. checked
  by: code review in Task 1.
- Today's only subscribers (`RecipeCoordinator` in `SemiStep.UI/Coordinator/RecipeCoordinator.cs`)
  use `ObserveOn(RxSchedulers.MainThreadScheduler)`, so no synchronous subscriber exists in
  production. The no-deadlock test is the guard for a future one.
- A synchronous subscriber that mutates coordinator state inside `OnNext` gets no ordering
  guarantee: its nested emission runs inside the outer one on the same thread.
- A synchronous subscriber that blocks on another thread which itself emits can still deadlock on
  the gate. That is a subscriber defect outside this scope.
- After the final outcome clears the running-attempt field, a new debounce can start while the old
  task is still in its `finally`. The old task does no I/O after its final outcome, so writes never
  overlap.
- `Dispose` blocks the calling thread for up to the bound. It runs at container disposal, which
  `docs/architecture/exit-flow.md` does not cover; #112 owns that path.

## Constraints and non-goals
- `IPlcSyncService` members and the `PlcSyncCoordinator` type name, its constructor parameters
  (`S7Di` registration, the test `Build` helper) and its `WaitForPendingSyncAsync` stay as they are.
- The debounce token is still the token passed to the write, on disable and on `Dispose`.
  Separating them is #180.
- No change to `PlcExecutionMonitor`, `S7Service`, `PlcTransactionExecutor` or the write protocol.
- Threat model: thread interleavings between the UI caller, the sync pool task and the connection
  callback. Nothing else.
- Tests use the existing `FakeS7Transport`; it gains only a way to hold a read until the test
  releases it.

## Gates
- none

## Acceptance
All commands run from the repository root in Git Bash. A `grep -c` with no match prints `0` and
exits 1; the printed count is what is checked.
- `dotnet build SemiStep.slnx` → 0 warnings, 0 errors
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=SyncCoordinator"` →
  0 failed, at least 23 passed (16 today plus at least 7 new tests)
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed
- `grep -cE '\bLock\b|lock \(|Action<' SemiStep/SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs` → `0`
- `grep -c '\.Wait(TimeSpan\.From' SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs` → `0`
- `grep -cE 'static readonly TimeSpan _[A-Za-z]+ = TimeSpan\.FromSeconds\(5\)' SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`
  → `1`
- `git diff --quiet origin/master...HEAD -- SemiStep/SemiStep.Core/Plc/IPlcSyncService.cs SemiStep/SemiStep.Core/Plc/Sync/PlcExecutionMonitor.cs`
  → exit code 0
- not visible to headless tests: none; the change has no visible surface

---

## Context
- `SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`: today owns status, enabled flag, connection state,
  the two subjects; `EmitFault` and `PublishSnapshot` call `OnNext` inside the lock (finding 3).
- `SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs`: today owns debounce, pending recipe, sync task,
  disposed flag on the coordinator's lock; `WriteSyncAsync` rechecks in two acquisitions
  (finding 2); `Dispose` uses an inline 5 s wait (finding 4).
- `SemiStep.Core/Plc/Sync/PlcTransactionExecutor.WriteRecipeWithRetryAsync`: the last transport call
  of a successful transaction is the final `ReadBytesAsync` of the verify pass.
- `SemiStep.Core/Plc/S7/S7Di.cs` registers the coordinator as a singleton and as `IPlcSyncService`.
- `SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs` and `SemiStep.Tests/S7/Helpers/FakeS7Transport.cs`.
- Callers of the public surface: `PlcLifecycleManager`, `RecipeSession`. `StubPlcSyncService` is the
  second `IPlcSyncService` implementation and does not change.
- Rules: `~/.claude/rules/csharp.md` Rx section ("A Subject written from more than one thread gets
  Synchronize()", "Shared UI state has exactly one writer"); project `CLAUDE.md` code style.

## Boundaries
- Task 1 creates and Task 2 keeps: `PlcSyncExecutor` exposes two async steps. The pre-check takes a
  `CancellationToken` and returns one of: skipped (not connected), failed (one typed `IError`),
  ready. The write takes a `Recipe` and a `CancellationToken` and returns failed (one typed
  `IError`) or written. Neither step touches coordinator state.
- Cancellation propagates out of both steps as `OperationCanceledException` and is not an outcome.
  Any other exception propagates to the coordinator, which maps it to `Failed` plus
  `PlcCommandFailedError`.

## Tasks

### Task 1: Move sync state into the coordinator
**Goal:** one class and one state lock own the sync state machine; the executor is a stateless pair
of steps; no debounce can start after dispose; stale outcomes are dropped; the dispose wait bound
is named
**Entry:** `PlcSyncCoordinator` constructor, `PlcSyncExecutor`
**Likely touches:** `PlcSyncCoordinator.cs`, `PlcSyncExecutor.cs`, `FakeS7Transport.cs`,
`PlcSyncCoordinatorTests.cs`

- [x] move debounce, pending recipe, sync task and the disposed check into `PlcSyncCoordinator`
      under its state lock; the debounce delay constant moves with them; status changes from the
      outcomes happen there, `Syncing` between the two steps
- [x] reduce `PlcSyncExecutor` to the two steps in Boundaries; drop its lock, callbacks, `Dispose`
      and `IDisposable`; keep its log lines and typed faults
- [x] apply an outcome only when not disposed and the attempt is current, in one acquisition; the
      post-write re-debounce starts in that same acquisition
- [x] token sources are swapped under the state lock and cancelled and disposed outside it; the
      dispose wait bound is a named constant in the coordinator
- [x] tests: `FakeS7Transport` can hold a chosen read until the test releases it, exposing the
      call's token; the hold ignores cancellation. Tests sync `Recipe.Empty` and hold the last
      transport call of the transaction, the final `ReadBytesAsync` of the verify pass, so a
      released attempt completes as written even after its token was cancelled
- [x] tests: a recipe queued while the last read is held produces a second transaction after the
      release; with the held read released as a failure, status is `Failed`, one fault arrives and
      no second transaction follows within 1.5 s
- [x] tests: `ResetForDisable` while the last read is held, then release: status stays `Idle`, last
      sync time stays empty, no fault. `Dispose` started while the last read is held and a second
      recipe is queued, released once the held call's token is cancelled: `Dispose` returns and no
      further write reaches the transport within 1.5 s
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=SyncCoordinator"`;
      must pass before the next task

### Task 2: Emit outside the state lock
**Goal:** no `OnNext` under the state lock; emissions serialized, in state order, never after
completion
**Entry:** `PlcSyncCoordinator.PublishSnapshot`, `PlcSyncCoordinator.EmitFault`,
`PlcSyncCoordinator.Dispose`
**Likely touches:** `PlcSyncCoordinator.cs`, `PlcSyncCoordinatorTests.cs`

- [x] tests first: a subscriber on `PlcState` (skipping the `BehaviorSubject` replay) and one on
      `Faults` read `Status` and `IsSyncEnabled` from another thread inside `OnNext` with a 2 s
      bound and record a timeout instead of throwing; the test asserts no timeout was recorded. Run
      it against the coordinator as Task 1 left it and record that it fails
- [x] route every emission through one emission gate with the lock order, the disposed check after
      the gate and the dispose ordering in Decisions
- [x] tests: several threads changing the enabled flag and connection state at once never overlap
      inside `OnNext`, and the last snapshot equals the current state
- [x] tests: `Dispose` racing emissions from another thread lets no exception escape and nothing
      arrives after completion
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=SyncCoordinator"`;
      must pass before the next task

### Task 3: Verify acceptance
**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`

### Task 4: Update documentation
**Goal:** docs name the new home of the sync state and the fault forwarding
**Entry:** `docs/architecture/error-reporting.md`, `docs/plans/roadmaps/20260807-plc-sync-reconciliation-roadmap.md`

- [x] `error-reporting.md`: the passages on un-laundering the fault seam and on codec decode failures
      name `reportFault` in `PlcSyncExecutor`; point them at the coordinator forwarding the step's
      typed error
- [x] roadmap: every `PlcSyncExecutor` reference points at the symbol that now holds that behavior
      (debounce, `Reset`, the debounce token, the debounce delay); drop line numbers that no longer
      hold
- [x] the `PlcSyncExecutor` summary describes the stateless steps

## Post-Completion
- closes #109
- the operator's manual test: enable sync against a PLC, edit a recipe, see `Syncing` then `Synced`;
  disable and re-enable; close the app with sync on. Behavior matches master
- no changes outside this repository
