# Name every PLC reconciliation outcome in the log

## Overview

`PlcLifecycleManager.ReconcileWithPlcAsync` (`SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs:309-370`)
decides, on every sync-enable, whether the operator is asked to resolve a PLC/local recipe conflict.
It ends at six explicit `return` statements (`:318`, `:329`, `:335`, `:346`, `:355`, `:366`) plus a
fall-through at `:369`. Three of those exits hand the local recipe to the sync executor and are
indistinguishable at the UI from "the recipes matched", and one of the three (`Committed == false`,
`:332-336`) writes nothing to the log at all. A field log therefore cannot say which exit ran on a
toggle where the conflict dialog failed to appear, which is exactly what the diagnosis of #158 needs.

This slice makes the outcome an explicit value produced by every exit and logged once per
reconciliation, alongside a line at the trigger so "the reconciliation never ran" is distinguishable
from "it ran and chose X". No behaviour changes: every exit still does exactly what it does today,
including the three silent pushes. The behaviour changes land in the two slices that follow.

## Context (from discovery)

Roadmap: `docs/plans/roadmaps/20260807-plc-sync-reconciliation-roadmap.md` - slice
`plc-reconcile-logging` (#158)

- Files involved: `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`, two new files under
  `SemiStep/SemiStep.Core/Plc/`, one line in `SemiStep/SemiStep.UI/Program.cs`,
  `SemiStep/SemiStep.Tests/Helpers/RecordingLogger.cs`, a new test file under
  `SemiStep/SemiStep.Tests/Domain/`, and `docs/readme.md` §5.
- The reconciliation is reached only from `OnConnectionStateChanged` on the
  `Connected && IsSyncEnabled` edge (`PlcLifecycleManager.cs:284-295`), returns silently when
  `_disposed` (`:286-289`), and is started fire-and-forget with a faulted-task continuation that
  logs (`:292-294`). `S7Service.State`'s setter suppresses `StateChanged` when the value is
  unchanged (`SemiStep/SemiStep.Core/Plc/S7/S7Service.cs:46-49`), so a toggle that never produces a
  transition never reaches the reconciliation at all.
- `PerformReconnectReconciliationAsync` (`:298-307`) swallows an `OperationCanceledException` when
  the lifetime token is cancelled. **A read that times out is logged nowhere.** S7.Net surfaces a
  socket read timeout as a `TaskCanceledException` (`SemiStep/SemiStep.Tests/Helpers/StubS7Service.cs:62-67`);
  `PlcTransactionExecutor`'s catches exclude `OperationCanceledException`
  (`Plc/Sync/PlcTransactionExecutor.cs:232`), so it propagates; the catch filter at `:304` does not
  match because the lifetime token is not cancelled; and an unhandled `OperationCanceledException`
  leaving an async method completes the task as **Canceled**, not Faulted, so the
  `TaskContinuationOptions.OnlyOnFaulted` continuation at `:292-294` never runs. The exception ends
  unobserved and silent. The established distinguisher in this codebase is the token, not the
  exception type: `OperationCanceledException` with the token cancelled is a genuine abort, with the
  token live it is a timeout wearing an `OperationCanceledException` costume
  (`docs/plans/completed/20260731-plc-cancellation-plumbing.md`).
- `NotifyLocalRecipe` (`:401-404`) calls `PlcSyncCoordinator.NotifyRecipeChanged`
  (`Plc/Sync/PlcSyncCoordinator.cs:101-116`), which returns early with `ClearPendingSnapshot()` and
  `Status = OutOfSync` when the recipe is invalid. Handing the recipe to the executor is therefore
  not the same as writing it to the PLC, and `_session.IsValid` decides which happened.
- `ApplyReconnectPlcRecipeAsync` (`:372-399`) returns without applying in three cases: no callback
  registered (`:375-380`), the callback threw (`:387-391`), the apply failed (`:393-398`).
- `ValidateProtocolVersionAsync` (`:154-169`) fails both on a genuine version mismatch and on a
  failed version read; the existing suite separates the two
  (`SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs:278` versus `:297`).
- `RecordingLogger<T>` (`SemiStep/SemiStep.Tests/Helpers/RecordingLogger.cs:5`) captures
  `LogEntry(Level, Message, Exception)` with the message already formatted. Its backing store is a
  plain `List<LogEntry>` (`:7`, appended at `:28`) with no synchronization; all eight existing users
  are single-threaded UI tests.
- `StubS7Service` drives the PLC answers through `ManagingAreaToReturn` and `RecipeToReturn`
  (`SemiStep/SemiStep.Tests/Helpers/StubS7Service.cs:48,53`); the trigger is
  `RaiseStateChanged(PlcConnectionState.Connected)` (`PlcLifecycleManagerReconnectTests.cs:96`).
- `SemiStep.Core` grants `InternalsVisibleTo SemiStep.Tests`
  (`SemiStep/SemiStep.Core/SemiStep.Core.csproj:15`), so the new types can be `internal`.
- **The field log records `Warning` and above.** `StartupOptions.DefaultLoggingLevel` is
  `LogEventLevel.Warning` (`SemiStep/SemiStep.UI/StartupOptions.cs:16-17`) and none of the six
  installer shortcuts passes `--logging-level` (`Installer/SemiStep.iss:91-102`). An
  Information-level line is invisible on an installed machine. The Serilog configuration applies one
  global floor, `MinimumLevel.Is(logLevel)` (`SemiStep/SemiStep.UI/Program.cs:135-137`), and has no
  per-source override today.
- Dependencies: none. The slice is independent of the other four in the roadmap.

## Development Approach

- **testing approach**: Regular (code first, then tests)
- complete each task fully before moving to the next
- make small, focused changes
- **CRITICAL: every task MUST include new/updated tests** for code changes in that task
- **CRITICAL: all tests must pass before starting next task** - no exceptions
- **CRITICAL: update this plan file when scope changes during implementation**
- run tests after each change
- maintain backward compatibility

## Testing Strategy

- **unit tests**: `SemiStep.Tests`, xunit v3, traits
  `[Trait("Component", "Domain")] [Trait("Area", "Reconnect")] [Trait("Category", "Integration")]`
  to match the existing reconnect suite (`PlcLifecycleManagerReconnectTests.cs:24-26`).
- **e2e tests**: none — this project has no browser e2e layer, and this slice touches no UI.
- The reconciliation is fully drivable headlessly: build the manager over `StubS7Service`, call
  `EnableSync`, then `RaiseStateChanged(PlcConnectionState.Connected)`. No PLC hardware is involved.
- The reconciliation runs on a thread-pool thread while the test thread reads the captured log, so
  every "exactly one line" assertion uses the settle-window pattern already in the suite
  (`PlcLifecycleManagerReconnectTests.cs:381-385`): wait for the first line, then a bounded delay,
  then count.

## Acceptance Evidence

**The defect today.** `ReconcileWithPlcAsync` contains no logger call between `:332` and `:336`, so
the `Committed == false` exit is silent; the read-failure exits log a warning naming the read that
failed but not the decision that followed it (`:325-327`, `:342-344`); the three exits that do the
right thing (`:352-356`, `:358-367`, `:369`) log nothing. Reproduce by running the reconnect suite
with a recording logger attached: today no entry names the exit taken.

**The measurement.**

```
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Reconnect"
```

must pass with the new `PlcReconciliationOutcomeLogTests` asserting, for each of the twelve outcomes,
exactly one entry whose message starts with `PLC reconciliation outcome <Outcome>` at the level
assigned in the Technical Details table, plus the begin line. The one log line with no test is the
`_disposed` trigger-side warning, which Technical Details states as untestable and why. The level is
part of every assertion: a `Warning` outcome silently demoted to `Information` would still be
invisible in the field, which is the failure this slice exists to prevent.

```
dotnet build SemiStep.slnx
```

must report 0 warnings — the build treats warnings and IDE1006 naming violations as errors.

## Progress Tracking

- mark completed items with `[x]` immediately when done
- add newly discovered tasks with ➕ prefix
- document issues/blockers with ⚠️ prefix
- update plan if implementation deviates from original scope
- keep plan in sync with actual work done

## Solution Overview

`ReconcileWithPlcAsync` becomes a thin wrapper: it awaits an inner method that returns a
`PlcReconciliationResult`, then emits one log line. The inner method keeps today's control flow and
today's side effects verbatim — every `return` gains a result value and nothing else changes.

Twelve outcomes cover nine exits: the seven `return` statements, plus the two ways the reconciliation
unwinds through an `OperationCanceledException`. The unwinding pair is what closes the hole that
makes a timeout invisible today, and it is what lets the manual gate hold a simple invariant — every
begin line is followed by exactly one outcome line, except when an exception other than
`OperationCanceledException` escapes the reconciliation, which the faulted-task continuation reports
as `Unhandled error in reconnect reconciliation`. Those are the only two shapes a begin line can
have.

Three of the returning exits split because one name would assert something the code does not
guarantee:

- the fall-through at `:369` is reached both when the PLC holds no steps and when the two recipes
  match, and the difference is what a triage of #158 needs;
- `ApplyReconnectPlcRecipeAsync` can discard the PLC recipe on three paths, so "applied" and
  "attempted and failed" are separate;
- the version branch covers a genuine mismatch and a failed read, which send triage to different
  places.

Both halves of each split keep the side effect they have today, so these are naming splits, not
behaviour splits.

**Levels.** The seven outcomes that mean something went wrong or that the PLC was overwritten without
the operator being asked are `Warning`; the four healthy ones are `Information`. A genuine
shutdown-cancel is `Information` too: `Debug` would put it below the pin described next, and a
shutdown-cancel with no outcome line reads in the field exactly like a hang. The level says what the
outcome means, and a separate mechanism makes it visible: a `MinimumLevel.Override` on
`typeof(PlcLifecycleManager).FullName` in `Program.cs` pins this one source above the global floor,
so the healthy outcomes and the begin line reach an installed machine's log without lowering the
level for everything else.

A Serilog per-source override *replaces* the effective minimum in both directions, so pinning at
`Information` unconditionally would also drop this source's `Debug` lines under
`--logging-level debug`. The pin is therefore the lower of the global level and `Information`
(`Program.ResolvePinnedSourceLevel`). The trap is documented in
`docs/architecture/error-reporting.md` and held by `SemiStep.Tests/UI/Logging/PinnedLogSourceTests.cs`.

Pinning is what makes the slice deliver. Relying on the operator to relaunch with
`--logging-level info` would assume the failure reproduces on demand, and intermittency is the whole
complaint in #158 — the run that must be readable is the one that already happened. Two of the
signatures the roadmap names as suspects are `Information`: `Identical` right after a toggle where a
dialog was expected (the #181 push race) and `ConflictRaised` with a live subscriber but no dialog
(the UI hop). Neither is visible without the pin. The volume is one begin line plus one outcome line
per operator-initiated toggle, against a 5 MB × 5 rotation
(`SemiStep/SemiStep.UI/Program.cs:138-147`).

The decision half is extracted into its own method so neither method exceeds the 50-line preference
in `CLAUDE.md`. It takes the local snapshot as parameters rather than re-reading `_session.Current`:
today `localRecipe` is one snapshot captured at `:350` and used three times, and a second read
inside the extracted method would be a second snapshot of a session the UI thread can mutate in
between. It builds the result on each of its three exits, because only it knows whether the exit
pushed and therefore which of the two local pairs the line must name.

## Technical Details

`PlcReconciliationOutcome` (internal enum, `SemiStep/SemiStep.Core/Plc/`):

| Value | Produced at | Level | Side effect today (unchanged) |
| --- | --- | --- | --- |
| `ProtocolVersionMismatch` | `:311-319` when the error is `ProtocolVersionMismatchError` | Warning | rolls back sync, disconnects |
| `ProtocolVersionCheckFailed` | `:311-319` otherwise | Warning | rolls back sync, disconnects |
| `ManagingAreaReadFailed` | `:323-330` | Warning | hands the local recipe to the sync executor |
| `NotCommitted` | `:332-336` | Warning | hands the local recipe to the sync executor |
| `RecipeReadFailed` | `:340-347` | Warning | hands the local recipe to the sync executor |
| `AppliedFromPlc` | `:352-356` when the apply succeeded | Information | replaces the local recipe |
| `ApplyFromPlcFailed` | `:352-356` when it did not | Warning | nothing — the PLC recipe is discarded |
| `ConflictRaised` | `:358-367` | Information | stores the pending recipe, raises the event |
| `PlcEmpty` | `:369` when `plcRecipe.Steps.Count == 0` | Information | hands the local recipe to the sync executor |
| `Identical` | `:369` otherwise | Information | hands the local recipe to the sync executor |
| `TimedOut` | new catch in `PerformReconnectReconciliationAsync` | Warning | nothing — the reconciliation unwound |
| `Cancelled` | existing catch at `:304-306` | Information | nothing — the application is shutting down |

`PlcEmpty` takes precedence when both recipes are empty, since both predicates hold there; the
`Identical` test therefore uses a non-empty recipe, as
`StateChanged_Connected_WhenRecipesIdentical_DoesNotFireConflict_AndPushesLocal`
(`PlcLifecycleManagerReconnectTests.cs:108`) already does.

`ApplyReconnectPlcRecipeAsync` returns `bool` so the caller can tell the two apply outcomes apart.
Its three existing warning logs stay where they are.

`PlcReconciliationResult` (internal readonly record struct, `SemiStep/SemiStep.Core/Plc/`):

| Field | Type | Meaning |
| --- | --- | --- |
| `Outcome` | `PlcReconciliationOutcome` | which exit ran |
| `LocalStepCount` | `int` | the local recipe the exit acted on. On the five exits that hand the local recipe to the sync executor it is the pair `NotifyLocalRecipe` returned, so the number names what the executor received. On `AppliedFromPlc`/`ApplyFromPlcFailed`/`ConflictRaised` it is the `localRecipe` snapshot, because a successful apply has already replaced the session recipe with the PLC one. On the exits that unwind before the snapshot exists it is a fresh `_session.Current` read |
| `LocalValid` | `bool` | false means nothing reached the PLC even on a "push" outcome; same provenance as `LocalStepCount`, so on a push exit it is the flag that decided whether the write happened |
| `PlcStepCount` | `int?` | null until the PLC recipe was read |
| `Committed` | `bool?` | null until the managing area was read |
| `HasConflictSubscriber` | `bool` | `PlcRecipeConflictDetected is not null` at the decision |

`HasConflictSubscriber` is what separates "Core never raised the conflict" from "Core raised it and
the UI hop dropped it": the event is a null-conditional raise (`:365`) and the coordinator
republishes it through `ObserveOn(MainThreadScheduler).Publish().RefCount()`
(`SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs:76-79`).

The single log call, at the end of `ReconcileWithPlcAsync`, with the level chosen from the table:

```
_logger.Log(
    level,
    "PLC reconciliation outcome {Outcome}: local {LocalStepCount} steps (valid {LocalValid}), PLC {PlcStepCount} steps, committed {Committed}, conflict subscriber {HasConflictSubscriber}",
    ...);
```

`NotifyLocalRecipe` returns the exact pair it pushed, so the five push exits name the recipe the
executor received rather than a second read of a session the UI thread can mutate. That is what the
suite pins (`AssertLoggedRecipeIsThePushedOneAsync`, all five push exits). On `PlcEmpty`/`Identical`
the pushed recipe and the compared snapshot can in principle be different revisions, but the window
is straight-line synchronous code with no await; the line carries the pushed pair, uniform with the
other push exits, and no separate diagnostic is emitted for a divergence no test can stage and the
non-volatile session field cannot be trusted to detect.

A null `PlcStepCount` or `Committed` renders as `null`, which is noise on an exit that never
reached the read. The five outcomes that unwind before both values exist — `ProtocolVersionMismatch`,
`ProtocolVersionCheckFailed`, `ManagingAreaReadFailed`, `TimedOut`, `Cancelled` — use a second,
shorter template carrying only the outcome and the local fields; `HasConflictSubscriber` is dropped
there too, since none of those exits reached the conflict decision. `NotCommitted` keeps the long
template: `Committed` is established there and `null` for `PlcStepCount` is the honest value, since
the recipe genuinely was not read. `TimedOut` also attaches the caught exception, which is the only
field that tells one hung read from another under the short template.

The existing per-branch warnings stay where they are; the outcome line is additional, never a
replacement.

**Trigger-side lines**, in `OnConnectionStateChanged` (`:284-295`):

- one `LogInformation` on the `Connected && IsSyncEnabled` branch stating the reconciliation was
  started, so a started-but-never-finished run is visible as a begin line with no outcome line;
- one `LogWarning` on the `_disposed` early return (`:286-289`), which is a dropped reconciliation
  and belongs at a level the field records. This line carries no test: `Dispose` sets `_disposed`
  (`:88`) and then unsubscribes the handler (`:90-93`), so the branch is reachable only when a state
  change is already in flight inside `Dispose` — a race no headless test can stage with the current
  stubs. It is kept because that race is real in production, and its absence from the suite is
  stated rather than hidden.

`PerformReconnectReconciliationAsync` gains a second, unfiltered `catch (OperationCanceledException)`
emitting the `TimedOut` outcome at `Warning`; the existing filtered catch (`:304-306`) emits
`Cancelled` at `Information`. The second catch carries no filter on purpose: a filter mirroring the
first would leave the exception uncaught if `Dispose` cancelled the lifetime token between the two
filter evaluations, and that run would end with a begin line and no outcome line — the silent path
this slice exists to close. Neither swallows anything that is observed today — the exception is already discarded
unobserved on both paths — so this adds a log line and no behaviour.

Two reconciliations can overlap: `Connected` can be published while `EnableSync` is still inside
`ConnectAsync`, which the suite already pins
(`SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs:314`). Begin and outcome lines
from two overlapping runs interleave with no correlation id to pair them. The manual gate reads the
log per operator toggle, where overlap does not arise; correlating concurrent runs is not worth a
correlation id in this slice.

Processing flow after the change:

```
OnConnectionStateChanged (Connected && sync enabled)   -> begin line, or warning if disposed
  -> PerformReconnectReconciliationAsync               token cancelled -> Cancelled, else -> TimedOut
       -> ReconcileWithPlcAsync                        await inner, then one outcome line
            -> RunReconciliationAsync                  reads; returns a result on every path
                 -> ResolveRecipeDifferenceAsync(localRecipe, plcRecipe)   compares; returns an outcome
```

In the `AppliedFromPlc` path the awaited callback completes on the Avalonia dispatcher
(`RecipeCoordinator.ApplyReconnectPlcRecipeAsync` marshals through `Dispatcher.UIThread`), so the
outcome line for that path plausibly runs on the UI thread against the synchronous file sink. This
is the one place where an added log call touches a thread it did not touch before; a single
`LogInformation` against a `shared: true` Serilog sink is acceptable there, and the path runs at most
once per connect.

## What Goes Where

- **Implementation Steps**: the two new types, the restructure of the reconciliation, the log calls,
  the `RecordingLogger` thread-safety fix, the tests, and the `docs/readme.md` §5 note.
- **Post-Completion**: the hardware verification, which belongs to the roadmap's manual gate after
  the fourth slice, not to this PR.

## Implementation Steps

### Task 1: Introduce the outcome types, thread them through the reconciliation, and log them

**Files:**
- Create: `SemiStep/SemiStep.Core/Plc/PlcReconciliationOutcome.cs`
- Create: `SemiStep/SemiStep.Core/Plc/PlcReconciliationResult.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`
- Modify: `SemiStep/SemiStep.UI/Program.cs`
- Modify: `SemiStep/SemiStep.Tests/Helpers/RecordingLogger.cs`
- Create: `SemiStep/SemiStep.Tests/Domain/PlcReconciliationOutcomeLogTests.cs`

The types, the restructure and the logging are one task: the restructure has no observable effect of
its own, so splitting it out would produce a task whose only deliverable is a deliberately discarded
return value, and three of the branches it rewrites (`ManagingAreaReadFailed`, `RecipeReadFailed`,
`PlcEmpty`) have no coverage in the existing suite to catch a mis-wiring.

- [x] create `PlcReconciliationOutcome` as an internal enum in namespace `SemiStep.Core.Plc` with the
      twelve values from the Technical Details table
- [x] create `PlcReconciliationResult` as an internal readonly record struct in the same namespace
      with the six fields from the Technical Details table
- [x] change `ApplyReconnectPlcRecipeAsync` (`PlcLifecycleManager.cs:372-399`) to return `bool`,
      keeping its three existing warning logs unchanged
- [x] rename the body of `ReconcileWithPlcAsync` to `RunReconciliationAsync` returning
      `Task<PlcReconciliationResult>`, giving each exit the matching result and changing nothing else
      about what each branch does; split the version branch on
      `versionResult.HasError<ProtocolVersionMismatchError>()`
- [x] extract `ResolveRecipeDifferenceAsync(Recipe localRecipe, Recipe plcRecipe)` from
      `PlcLifecycleManager.cs:352-369` returning the outcome, so both methods stay under the 50-line
      preference; extract a third method if the read half still exceeds it
- [x] make `ReconcileWithPlcAsync` await `RunReconciliationAsync` and emit exactly one log entry at
      the level from the table, using the short template when `PlcStepCount` and `Committed` are null
- [x] add the begin line and the `_disposed` warning in `OnConnectionStateChanged`
- [x] add the second, unfiltered `catch (OperationCanceledException)` in
      `PerformReconnectReconciliationAsync` emitting `TimedOut` at Warning with the exception
      attached, and make the existing filtered catch emit `Cancelled` at Information
- [x] add a `MinimumLevel.Override` on `typeof(PlcLifecycleManager).FullName` to the Serilog
      configuration in `SemiStep/SemiStep.UI/Program.cs:135-137`, pinned at the lower of the global
      level and `Information`, so the five healthy outcomes and the begin line survive the field's
      Warning floor without capping this source under `--logging-level debug`
- [x] make `RecordingLogger<T>` thread-safe: lock the append at `:28` and replace the `Entries`
      property with a `Snapshot()` method that copies under the same lock — the reconciliation appends
      from a thread-pool thread while the test thread reads, and the copy cost belongs at the call site
- [x] create `PlcReconciliationOutcomeLogTests` with the reconnect suite's three traits and its own
      DI builder registering `RecordingLogger<PlcLifecycleManager>` as `ILogger<PlcLifecycleManager>`
      after `AddLogging()`; a separate file because `PlcLifecycleManagerReconnectTests.cs` is already
      408 lines against a 300-line preference and adding the logger to its shared builder would
      rewrite 13 deconstruction sites that never use it
- [x] write tests for the five read-phase Warning outcomes: `ProtocolVersionMismatch`,
      `ProtocolVersionCheckFailed`, `ManagingAreaReadFailed`, `NotCommitted`, `RecipeReadFailed` —
      each asserting the level as well as the name
- [x] write tests for the four Information outcomes: `AppliedFromPlc`, `ConflictRaised`, `PlcEmpty`,
      `Identical`, with `Identical` using a non-empty recipe so it cannot collide with `PlcEmpty`
- [x] write a test for `ApplyFromPlcFailed` by building the manager with no reconnect apply callback
      registered (`PlcLifecycleManager.cs:375-380`)
- [x] write a test for `TimedOut` using `StubS7Service.ProtocolVersionReadShouldThrowCanceled`
      (`StubS7Service.cs:62-67`) with the lifetime token left live — this is the case that logs
      nothing at all today
- [x] write a test for `Cancelled` using the gate-then-dispose pattern already in the suite
      (`PlcLifecycleManagerReconnectTests.cs:344-394`)
- [x] write a test that the begin line is emitted at Information on the Connected edge
- [x] write a test that a reconciliation emits exactly one outcome line, using the settle-window
      pattern from `PlcLifecycleManagerReconnectTests.cs:381-385`
- [x] write a test that `LocalValid` is false and the outcome is still emitted when the session holds
      an invalid recipe
- [x] run `dotnet build SemiStep.slnx` — 0 warnings
- [x] run tests - must pass before next task

### Task 2: Document the field procedure for the Information-level lines

**Files:**
- Modify: `docs/readme.md`

- [x] in §5 (`docs/readme.md:405-413`), state that the default level is `Warning`, that the PLC
      reconciliation source is pinned to `Information` so every sync toggle leaves a begin line and
      an outcome line regardless of that default, and what those two lines look like — written in
      Russian, the language of that file
- [x] verify no other documentation claims the log records Information by default
- [x] no test applies to a documentation-only change; the acceptance is that §5 names the default
      level and the pinned source

### Task 3: Verify acceptance criteria

- [x] verify every exit of `RunReconciliationAsync` carries an outcome and no branch is unreachable
- [x] verify no behaviour changed: the three silent pushes still call `NotifyLocalRecipe`, both
      version exits still roll back and disconnect, the conflict exit still stores the pending recipe
      before raising the event, and the recipe comparison in `ResolveRecipeDifferenceAsync` reads only
      the `localRecipe` parameter, never a second `_session.Current` — the one remaining read is the
      pre-existing one inside `NotifyLocalRecipe`, which this slice must not touch
- [x] verify both new catches only log — neither swallows an exception that any code observes today
- [x] verify the `Program.cs` change is the single `MinimumLevel.Override` line and nothing else
- [x] verify no new error type and no resx entry was added
- [x] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] run `dotnet build SemiStep.slnx` — 0 warnings
- [x] run `dotnet format SemiStep.slnx` — the pre-commit hook enforces it

### Task 4: [Final] Close out

- [x] no CLAUDE.md change is expected — confirm and state so in the PR body if none is made —
      confirmed, no change made. The slice adds no build or test command, no naming or layout rule,
      and no pattern that generalises past this one source. `CLAUDE.md` is the overview file and
      states "do not add specifics here"; the operator-facing fact this slice produces (the default
      log level and the pinned `PlcLifecycleManager` source) is documentation and landed in
      `docs/readme.md` §5 in Task 2
- [x] the PR body carries no `Closes` line: this slice does not close #158 — instruction to the
      delivery step, recorded here; two later slices (`plc-reconcile-uncommitted`,
      `plc-reconcile-read-failure`) carry the close. No pull request is opened by this task
- [x] move this plan to `docs/plans/completed/` — deferred to delivery. The move records a
      completion that has not happened until the operator has tested the branch, and moving the file
      mid-run breaks the later review and stats phases that read it

## Post-Completion

**Manual verification:**

No hardware test belongs to this PR. The roadmap runs one manual gate against a real PLC after the
fourth slice; this slice's contribution is that every toggle in that session leaves exactly one
outcome line, and that every outcome — the seven suspicious ones by level, the five `Information` ones and
the begin line by the pinned source override — is visible without the operator changing anything.

**Field triage this enables:**

A `ConflictRaised` line with `conflict subscriber True` and no dialog on screen points at the UI hop
— `RecipeCoordinator`'s republish or the interaction handler — not at Core. That is the next place to
look, and it is out of scope here.

**Remaining slices:**

- `plc-reconcile-uncommitted` — compare regardless of the commit flag, so a differing PLC recipe
  raises the dialog whatever `Committed` says. Depends on this slice.
- `plc-reconcile-read-failure` — a read failure no longer answers with an overwrite; it reports
  through the fault seam. Closes #158.
- `plc-write-atomic` (#180) — the write transaction becomes untearable by a sync disable.
- `plc-push-gate` (#181) — BLOCKED, held pending the hardware test.

**Executed by exec:**

- branch: plc-reconcile-logging

## Verify it yourself

The slice adds no behaviour, so there is no user-visible before/after to click through. What changed
is what the log says, and that is provable two ways.

**1. The headless proof (30 seconds, no PLC).**

```
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Reconnect"
```

35 tests pass. Twelve of them drive one reconciliation each through `StubS7Service` and assert the
outcome line by name *and* by level; `Reconciliation_WhenSessionChangesDuringPush_LogsThePushedRecipe`
mutates the session inside the push window and asserts the logged step count equals the pushed
recipe's. To see one fail on purpose, change any arm of `ResolveOutcomeLevel`
(`SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`) from `Warning` to `Information` and re-run: the
level assertion for that outcome fails. Revert.

**2. The Serilog proof (the part that decides whether the field ever sees this).**

```
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Logging"
```

`PinnedLogSourceTests` builds a real `LoggerConfiguration` and proves the pin lifts `Information` over
the shipped `Warning` default for `SemiStep.Core.Plc.PlcLifecycleManager` and for no other source.
Without this, every `Information` outcome would be invisible on an installed machine — the defect that
would have made the whole slice ship nothing.

**3. The manual look (needs a PLC or any reachable S7 endpoint).**

Run the app, toggle sync on, then open `C:\DISTR\Logs\SemiStep\semistep.log`. Every toggle now leaves
a begin line followed by exactly one `PLC reconciliation outcome <Name>` line carrying the local step
count, its validity, the PLC step count, the commit flag and whether a conflict subscriber was
attached. Before this branch that same toggle wrote nothing on three of the seven exits — including
the `Committed == false` exit, which is the one most likely to explain #158.

**Known residual, not fixed here:** `docs/readme.md` §5 says the only way a begin line lacks an
outcome line is an unhandled error line. There is a second, pre-existing way: `Dispose` landing
between the `_disposed` check and the `_lifetimeCts.Token` read throws `ObjectDisposedException` out
of the event handler, leaving neither. The race predates this branch; only the doc claim is new.
