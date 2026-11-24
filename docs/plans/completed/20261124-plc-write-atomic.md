# Torn recipe write on sync disable

Roadmap: slice plc-write-atomic

## Behavior
- Today a sync disable or a coordinator dispose during a recipe write cancels the write between its
  own managing-area writes. The PLC keeps `Committed = false` and arrays that are part old, part new,
  and nothing is logged. After the change a write that started runs to its end, success or failure,
  on disable and on dispose.
- A write starts in the state-lock acquisition that applies `Syncing`. Before that acquisition a
  disable cancels the pending debounce or pre-check, and no write begins. After it, only a connection
  loss or an expired dispose bound cancels the write. The uncancelled unit is the whole
  `PlcTransactionExecutor.WriteRecipeWithRetryAsync`, retries and verify included, because a retry
  writes `Committed = false` again.
- Disable: the toggle and the status change at once, as today (sync off, status `Idle`). The app then
  waits up to 5 s for a write in flight, disconnects, and releases the sync ownership. The release
  runs even when the disconnect throws. A normal write ends within that wait, so the PLC is left
  committed. When the wait expires, the app logs a warning and disconnects. `ToggleSyncCommand` stays
  disabled during the wait, up to 5 s.
- Dispose: the coordinator cancels a pending debounce, waits up to 5 s for a write in flight, and only
  when that bound expires logs a warning and cancels the write. The aborted write logs its own warning
  when it ends.
- A started write that ends in a way no code logs today logs one coordinator warning: cancelled,
  failed with `NotConnectedError`, or failed with an outcome dropped as stale. Other failures already
  log an error (`PlcTransactionExecutor.WriteRecipeDataAsync`, `PlcSyncExecutor.WriteAsync`,
  `PlcSyncCoordinator.RunAttemptAsync`). A process that dies mid-write is reported at the next connect
  by the existing reconciliation outcome `NotCommitted` at Warning
  (`PlcLifecycleManager.RunReconciliationAsync`, `ResolveOutcomeLevel`).
- `IPlcSyncService` gains `WaitForPendingSyncAsync`.
- Scope guard: the write protocol (order, offsets, retry count), the reconciliation, the auto-push gate
  (#181), container disposal (#112) and PLC-side behavior stay as they are. Nothing disposes the
  container in production today (#112), so the dispose guarantee reaches application exit only once
  #112 lands. This plan covers `PlcSyncCoordinator.Dispose`, the seam #112 calls.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Disable while no attempt runs | sync off, status `Idle`, disconnect, ownership released; no wait | agent: `PlcLifecycleManager.DisableSync` | existing `PlcLifecycleManagerOwnershipTests` |
| Disable during the debounce or the pre-check | the debounce token cancels the attempt; the `Syncing` outcome is dropped; no write; the wait ends at once | agent: `PlcSyncCoordinator.SyncPendingRecipeAsync` returns before the write when `Syncing` is dropped | existing `ResetForDisableDuringHeldPrecheck_StartsNoWrite`, unchanged |
| Disable during a write | the write runs to its end; its outcome is dropped (status stays `Idle`, last sync time not stamped, no fault); then disconnect and ownership release | agent: issue scope; #109 stale-outcome rule | Acceptance 3, 4 |
| Disable during a write that outlasts 5 s | one Warning from `PlcLifecycleManager`; disconnect and ownership release; the write then fails on the dead link and, being stale, logs one coordinator Warning | agent: issue "bounded" | Acceptance 4 |
| Disconnect throws during disable | the exception is logged as today; ownership is still released | operator (manager decision) | Acceptance 4 |
| Connection lost during a write | the write is cancelled, as today; status `Failed` when it was `Syncing`, one `ConnectionLostError`, one coordinator Warning for the cancelled write | agent: the link is gone and every further transport call fails; a hard failure the invariant allows, now logged | Acceptance 3 |
| Connection lost during the disable wait | `PlcLifecycleManager.OnConnectionStateChanged` calls `HandleConnectionLost` only while sync is enabled, so the write is not cancelled. It either fails on the dead link (stale, one coordinator Warning) or completes whole on a session the reconnect loop opened (`Recipe synced to PLC successfully` at Information) before the disconnect. Neither tears | operator (manager decision): non-goal | code review |
| Dispose during a write that ends inside 5 s | `Dispose` blocks until the write ends; the transaction is whole; no debounce starts; no write after `Dispose` returns | agent: issue scope; #109 dispose guarantee | Acceptance 3 |
| Dispose during a write that outlasts 5 s | `Dispose` returns after 5 s, logs one Warning for the timeout and cancels the write; when the write ends on the cancelled token it logs its own coordinator Warning | operator (manager decision) | Acceptance 3 |
| Recipe edit during the disable wait | `RecipeSession.NotifySyncIfEnabled` drops it, because sync is already off | agent: `RecipeSession.NotifySyncIfEnabled` reads `IsSyncEnabled` | code review |
| Protocol version handshake fails on enable or reconnect | `FailProtocolVersionHandshakeAsync` disconnects with no wait, as today | agent: no recipe edit reaches the coordinator before the reconciliation, except the #181 race | non-goal |

## Decisions
- One write token source per attempt, separate from the debounce token source. The coordinator creates
  it in the state-lock acquisition that applies `Syncing`, stores it in a write-source field and passes
  its token to `PlcSyncExecutor.WriteAsync`. The debounce source keeps its jobs: attempt identity for
  the currency check, the debounce delay and the pre-check. Reason: today one source is all three plus
  the write token (`PlcSyncCoordinator.StartDebounceLocked`, `RunAttemptAsync`), so `ResetForDisable`,
  `HandleConnectionLost` and `Dispose` reach the write. Creating the write source in the `Syncing`
  acquisition makes "started" one lock acquisition: a reset or dispose before it drops `Syncing`, after
  it they leave the write alone. rejected: a source linked to the debounce token, because a reset still
  cancels it; a write with no token, because dispose then has no way to stop a write past its bound,
  which breaks the #109 "no write after `Dispose` returns" guarantee.
- Write source ownership follows the existing `Retire` rule: whoever takes the source out of its field
  under the state lock owns its `Cancel` and `Dispose`, and runs both outside the lock.
  `HandleConnectionLost` and an expired `Dispose` bound take it out, then cancel and dispose it. The
  attempt takes out its own source when the write ends, compared by reference, so it never clears a
  later attempt's source; that take-out bypasses the disposed early return of the transition paths,
  because otherwise the source leaks after `Dispose`. The attempt keeps using the token it captured.
  rejected: a separate owner flag, because the field swap already says who owns the source.
- `HandleConnectionLost` still cancels the write. rejected: leaving the write alone on connection loss,
  because the write cannot finish on the lost link and would otherwise continue on the session the
  reconnect loop opens one second later, under the reconciliation that session starts.
- `DisableSync` order: sync off, reset, bounded wait, disconnect, ownership release; the release runs
  even when the disconnect throws. Reason: the ownership lease (`IPlcSyncOwnership`) stops a second app
  instance from writing to this PLC. rejected: today's order with the release before the disconnect,
  because a lease released while our write or our session still runs lets that instance start a write
  over ours.
- The disable bound is a named constant in `PlcLifecycleManager`, 5 s; the lifecycle manager passes a
  timeout token to `WaitForPendingSyncAsync`. The dispose bound stays the coordinator's dispose-wait
  constant, 5 s. Reason: each caller owns how long its own caller waits. A transaction that verifies on
  its first pass is 16 transport calls (9 writes, 7 reads, `WriteRecipeDataAsync` and
  `VerifyWriteAsync`), 3 passes at most (`PlcProtocolSettings.MaxRetryAttempts`). rejected: the
  configured `WritingTimeoutMs` and `CommitTimeoutMs` in `PlcProtocolSettings`, because no code reads
  them today (only `ConnectionMapper` maps them) and wiring them is a separate change.
- `WaitForPendingSyncAsync` moves onto `IPlcSyncService` with its current signature and behavior: it
  waits for the attempt task in flight at the call and throws `OperationCanceledException` when its
  token cancels. `StubPlcSyncService` implements it over a task the test controls; the default is a
  completed task, and its wait observes the token.
- The new coordinator Warning covers only the endings logged nowhere today: cancelled, failed with
  `NotConnectedError` (logged at Debug in `PlcTransactionExecutor.DriverNotConnected`), and failed with
  a stale outcome (dropped without a line by the currency check). Today a cancelled write is swallowed
  in `RunAttemptAsync`. No fault and no status change come with the line, so a stale outcome still
  changes nothing. rejected: a Warning for every unsuccessful write, because it duplicates the existing
  Error lines.
- Tests prove the tear with a write hold on `FakeS7Transport` that mirrors `BeforeRead`. rejected: only
  the existing read hold, because `PlcTransactionExecutor.WriteRecipeDataAsync` makes no read between
  the `Committed = false` write and the `Committed = true` write, so a held read sits after the commit
  and passes on master. A held array write, released after a reset, shows the tear on master: the next
  `WriteBytesAsync` throws on the cancelled token and no committed byte 1 follows the last array write.

## Caveats
- `System.Threading.Lock` is re-entrant on one thread; `OnNext` on a disposed subject throws;
  `Task.Wait(TimeSpan)` returns `false` on timeout and does not throw. The dispose timeout Warning relies
  on that `false`. probed: see `docs/plans/completed/20261124-sync-executor-lock.md`, Caveats,
  2026-11-24.
- The service container disposes singletons in reverse creation order. probed: file-based `dotnet run`
  app on SDK 11.0.100 with Microsoft.Extensions.DependencyInjection 10.0.0; a singleton taking a
  factory-registered connection was disposed before that connection, 2026-11-24. The probe did not
  cover the S7Di shape, where `IPlcSyncService` is a factory forwarding to the `PlcSyncCoordinator`
  registration. ASSUMPTION: 11.0.0, which the project pins in `Directory.Packages.props`, keeps the
  order; fallback: #112 disposes in an explicit order; checked by: #112.
- Hand-off contract for #112: `PlcLifecycleManager` takes `IPlcSyncService` in its constructor, so the
  container creates it after the coordinator and disposes it first. Its `Dispose` releases the
  ownership lease (`PlcLifecycleManager.ReleaseOwnershipLease`) before the coordinator waits for the
  write. #112 must dispose the coordinator explicitly before the lifecycle manager and the container.
- `ReactiveCommand.CreateFromTask` cannot execute again while it executes, so the toggle cannot start
  an enable during the disable wait (`MainWindowViewModel.ToggleSyncCommand`). verified: ReactiveUI
  23.2.28 (the version `SemiStep.UI` resolves), XML docs of `IReactiveCommand.CanExecute`: "A command
  that is currently executing will always yield `false` from this observable", 2026-11-24.
- `S7Driver.ConnectAsync` sets no `ReadTimeout` or `WriteTimeout` on the S7netplus `Plc` (verified:
  no `Timeout` in `S7Driver.cs`, 2026-11-24). How S7netplus 0.20.0 bounds an async call without them is
  unverified; one stalled call can outlast 5 s. The bound covers that stall, and the warning names it.
- A write whose outcome is dropped after a disable leaves the last sync time unstamped although the
  PLC got the recipe. The next enable runs the reconciliation and reports `Identical`.
- `PlcSyncCoordinator.NotifyRecipeChanged` does not check the enabled flag. A reconciliation that calls
  `NotifyLocalRecipe` after the disable queues a recipe; the stale attempt's end starts its debounce,
  and the 1 s delay outlasts the disconnect, so its pre-check skips. That window belongs to #181.
- No Flow row covers a re-enable while the write of the disabled session still runs after the disable
  bound expired. It needs a transport call that stalls past the disconnect and a Connect click before
  the stall ends. The rest of that write can then reach the session the enable opens, beside the
  reconciliation; its outcome is dropped as stale. Not probed and not handled: the window is narrow.

## Constraints and non-goals
- Keep #109's guarantees: no `OnNext` under the state lock; every `Cancel` and `Dispose` of a token
  source runs outside the state lock; a stale outcome changes no status, last sync time or fault and
  starts no debounce; no write starts after `Dispose` returns within the bound.
- A connection loss during the disable wait does not cancel the write (Flow branches). Not handled.
- No change to `PlcTransactionExecutor`, `S7Service`, `S7Driver`, `PlcExecutionMonitor`, the
  reconciliation or the container setup in `Program`.
- Threat model: interleavings of the UI caller, the sync pool task, the connection callback and
  dispose. A hostile PLC and a misbehaving subscriber are out of scope.
- Tests stay simple; two tests wait out a 5 s bound in real time.

## Gates
- none

## Acceptance
All commands run from the repository root in Git Bash. A `grep -c` with no match prints `0` and exits
1; the printed count is what is checked.
1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors
2. `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=SyncCoordinator"` → 0 failed,
   at least 32 passed (27 on 2026-11-24 in `PlcSyncCoordinatorTests` and
   `PlcSyncCoordinatorInterleavingTests` together, plus at least 5 new). Includes: a reset during a held
   array write, then release, leaves the committed byte 1 at `CommittedOffset` after the last array
   write (fails on master); the held write's token is not cancelled by `ResetForDisable` or by `Dispose`
   within the bound (fails on master); a connection loss during a held write cancels the held token and
   logs one coordinator Warning; `Dispose` during a held write blocks until release and the transaction
   is whole; `Dispose` with a write held past the bound returns within about 10 s and cancels the held
   token, and after the test releases the hold, the log holds one timeout Warning and one aborted-write
   Warning; the reshaped dispose-during-held-write test still sees no write after `Dispose` returns
4. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcLifecycleManager"`
   → 0 failed. Includes: `DisableSync` neither disconnects nor releases ownership while the stub's
   pending sync is open, and does both after it completes; with a pending sync that never completes,
   `DisableSync` disconnects after the bound and logs one Warning; a throwing disconnect still releases
   ownership
5. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed
6. `grep -c 'Task WaitForPendingSyncAsync(' SemiStep/SemiStep.Core/Plc/IPlcSyncService.cs` → `1`
7. `git diff --quiet origin/master...HEAD -- SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs SemiStep/SemiStep.Core/Plc/S7`
   → exit code 0
- not visible to headless tests: the write-window branch on a real PLC (a disable inside a write lasts
  milliseconds and cannot be timed by hand; only the headless tests prove it); S7netplus behavior on a
  real link; the ReactiveUI toggle during the wait; application exit, which needs #112

---

## Context
- `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`: `StartDebounceLocked`, `CancelAttemptLocked`,
  `RunAttemptAsync`, `SyncPendingRecipeAsync`, `ApplyOutcome`, `Transition` and `StartDebounceWhen`
  (both return early when disposed), `Dispose`, `WaitForSyncTaskOnDispose` (ignores the `Task.Wait`
  result today), `Retire`.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs`: `WriteAsync` passes on the token it takes.
- `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`: `DisableSync`, `ReleaseOwnershipLease`.
- `SemiStep/SemiStep.Core/Plc/IPlcSyncService.cs`; implementations `PlcSyncCoordinator` and
  `SemiStep/SemiStep.Tests/Helpers/StubPlcSyncService.cs`.
- Tests: `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorInterleavingTests.cs` (`ReadHold`;
  `DisposeDuringHeldWriteWithQueuedRecipe_NoWriteAfterDisposeReturns` waits for the held token to be
  cancelled and hangs after the change), `PlcSyncCoordinatorTests.cs` (`Build` passes
  `NullLoggerFactory`), `Helpers/FakeS7Transport.cs`, `SemiStep/SemiStep.Tests/Helpers/RecordingLogger.cs`,
  `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerOwnershipTests.cs`,
  `PlcLifecycleManagerReconnectTests.cs`, `Helpers/StubS7Service.cs` (`DisconnectCallCount`).
- Error and log assertions check type and level, not wording (`docs/architecture/error-reporting.md`).
- Rules: `~/.claude/rules/csharp.md` Rx section; project `CLAUDE.md` code style.

## Boundaries
- Task 1 keeps and Task 2 lifts onto `IPlcSyncService`:
  `Task WaitForPendingSyncAsync(CancellationToken ct = default)`, which completes when the attempt in
  flight at the call has ended, at once when none runs, and throws `OperationCanceledException` when
  `ct` cancels first.

## Tasks

### Task 1: Separate the write token from the debounce token
**Goal:** a started write survives `ResetForDisable` and `Dispose` within its bound; a connection loss
and an expired dispose bound still cancel it; the endings logged nowhere today log one warning
**Entry:** `PlcSyncCoordinator.SyncPendingRecipeAsync`, `PlcSyncCoordinator.Dispose`
**Likely touches:** `PlcSyncCoordinator.cs`, `FakeS7Transport.cs`, a new
`SemiStep/SemiStep.Tests/Helpers/RecordingLoggerFactory.cs`, `PlcSyncCoordinatorInterleavingTests.cs`,
`PlcSyncCoordinatorTests.cs`

- [x] the write source is created in the acquisition that applies `Syncing` and its token goes to the
      write step; the debounce source keeps identity, delay and pre-check; ownership follows the
      `Retire` rule in Decisions
- [x] `HandleConnectionLost` takes out and cancels the write source; `ResetForDisable` and a new
      debounce do not; `Dispose` logs a Warning and cancels it only after its bound expires
- [x] one coordinator Warning when a started write ends cancelled, with `NotConnectedError`, or failed
      with a stale outcome
- [x] tests: `FakeS7Transport` gains a write hook mirroring `BeforeRead`; a held array write reset and
      released leaves the committed byte 1 after the last array write; the held token survives reset
      and `Dispose` within the bound; connection loss cancels it with one Warning; `Dispose` past the
      bound returns within about 10 s and cancels, and after release the log holds the timeout Warning
      and the aborted-write Warning; the dispose-during-held-write test is reshaped to wait for `Dispose`
      to block instead of for a cancelled token; the held-precheck test stays unchanged
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=SyncCoordinator"` - must pass before the next task

### Task 2: Disable waits for the write before it disconnects
**Goal:** `DisableSync` disconnects and releases ownership only after the in-flight write ends or the
5 s bound expires
**Entry:** `IPlcSyncService`, `PlcLifecycleManager.DisableSync`
**Likely touches:** `IPlcSyncService.cs`, `StubPlcSyncService.cs`, `PlcLifecycleManager.cs`,
`PlcLifecycleManagerOwnershipTests.cs` or `PlcLifecycleManagerReconnectTests.cs`

- [x] `WaitForPendingSyncAsync` moves onto `IPlcSyncService`; the stub implements it over a task the test
      controls, completed by default, and its wait observes the token
- [x] order: sync off, reset, bounded wait, disconnect, ownership release; the release runs when the
      disconnect throws; the bound is a named constant; an expired bound logs one Warning
- [x] check the ReactiveUI `ReactiveCommand` source for the caveat that a command does not execute
      again while it executes; record the result in Caveats
- [x] tests: no disconnect and no lease release while the stub's pending sync is open, both after it
      completes; a pending sync that never completes leads to disconnect after the bound and one
      Warning; a throwing disconnect still releases the lease; existing `DisableSync` tests pass
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcLifecycleManager"` - must pass before the next task

### Task 3: Verify acceptance
**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`
- [x] the timing-sensitive tests pass 5 runs in a row
- [x] `terse` over the touched files exits 0 (exits 1 on 5 findings that predate this branch, none on a
      changed line; left as they are)

### Task 4: Update documentation
**Goal:** docs describe the write token, the disable wait and the exit seam
**Entry:** `docs/plans/roadmaps/20260807-plc-sync-reconciliation-roadmap.md`, `docs/architecture/exit-flow.md`

- [x] roadmap slice `plc-write-atomic`: plan path, branch; drop line numbers that no longer hold
- [x] roadmap manual verification gate step 5: split into a disable within one second of an edit (no
      write, no coordinator warning) and a disable after synced (clean disconnect), and state that
      only the headless tests prove the write-window branch
- [x] `exit-flow.md`: one short section stating that `PlcSyncCoordinator.Dispose` waits up to its bound
      for a started write, that nothing calls it in production until #112, and the #112 disposal order
      from Caveats
- [x] the `PlcSyncCoordinator` summary names the separate write token

## Post-Completion
- closes #180
- manual smoke with the emulator (`SemiStep/SemiStep.Tools.PlcEmulator/README.md`):
  1. enable sync, edit a step, disable sync within one second → no write reaches the PLC (no
     `Recipe synced to PLC successfully` line) and no coordinator warning (skipped - operator smoke)
  2. enable sync, edit a step, wait for synced, disable → a clean disconnect line follows the disable,
     with no warning (skipped - operator smoke)
  3. re-enable → the reconciliation outcome is `Identical` or `ConflictRaised`, never `NotCommitted` (skipped - operator smoke)
- the write-window branch (a disable while the write runs) is proven only by the headless tests
- the roadmap manual verification gate step 5 runs against a real PLC
- no changes outside this repository
