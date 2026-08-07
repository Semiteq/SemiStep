# PLC sync reconciliation roadmap

**Issues:** #158 (Sync flow bug — the "Rewrite or keep recipe" window is flaky) is the tracking
issue and is closed by the third slice. #180 (torn write leaves `Committed = false`) and #181
(auto-push overwrites the PLC before reconciliation reads it) are defects found while diagnosing
#158; each is closed by its own slice.

## Summary

Enabling sync reconciles the local recipe against the PLC and raises the conflict dialog when the
two differ. Six code paths end that reconciliation, five of them push the local recipe to the PLC,
and three of those five are indistinguishable from "no conflict" at the UI and leave no log line, so
the operator sees the toggle flip to synced and the PLC silently take the local recipe. Five slices
make every outcome observable, stop the reconciliation from overwriting a PLC state it failed to
establish, and remove the two mechanisms that can put the PLC into that unestablished state in the
first place. The roadmap closes when the conflict dialog is a function of the recipe comparison
alone and every other outcome is named in the log.

**Thesis:** every reconciliation outcome is named in the log, and the local recipe reaches the PLC
only when the reconciliation established what the PLC holds.

**Verified against code on 2026-08-07 (21f6929). Trust rule: prefer the shapes over the numbers if
they have drifted.**

## Root cause

The dialog has exactly one trigger: `PlcRecipeConflictDetected` at
`SemiStep.Core/Plc/PlcLifecycleManager.cs:365`, reachable only from `ReconcileWithPlcAsync`
(`:309`), which runs on the `Connected && IsSyncEnabled` edge of `OnConnectionStateChanged`
(`:284-295`). `EnableSync` (`:107`) always drives `Disconnected → Connecting → Connected` through
`S7Service.ConnectInternalAsync` (`S7/S7Service.cs:172-198`), and `State`'s setter raises
`StateChanged` on every real transition (`S7Service.cs:42-53`), so the trigger fires on every sync
toggle. The flakiness is entirely downstream of it.

| Exit | Line | Pushes local to PLC | Logged | Distinguishable by the operator |
| --- | --- | --- | --- | --- |
| Protocol version mismatch | `:311-319` | no | warning | yes — sync is rolled back |
| Managing-area read failed | `:323-330` | yes | warning | no |
| `Committed == false` | `:332-336` | yes | **nothing** | no |
| Recipe read failed | `:340-347` | yes | warning | no |
| Local empty, PLC non-empty | `:352-356` | no — applies PLC recipe | only on failure | yes — the grid changes |
| Recipes differ | `:358-367` | no — raises the dialog | no | yes — the dialog |
| Recipes equal, or PLC empty | `:369` | yes | no | correct outcome |

Three rows carry the defect. `Committed` is written only by this application — false at the start of
a write transaction (`Sync/PlcTransactionExecutor.cs:243-244`), true at the end (`:265-266`) — so
`Committed == false` on connect means the previous write never finished, not that the PLC holds
nothing. The reconciliation answers that state by overwriting the PLC with the local recipe, without
reading what is there and without a log line.

Two mechanisms produce that state and one more can hide a genuine conflict:

- **Torn write.** `DisableSync` (`PlcLifecycleManager.cs:183`) calls `ResetForDisable` →
  `PlcSyncExecutor.Reset()` (`Sync/PlcSyncExecutor.cs:61-70`), which cancels `_debounceCts`. That is
  the same token handed to `WriteRecipeWithRetryAsync` (`PlcSyncExecutor.cs:144` → `:228`), so a
  disable landing between the two managing-area writes aborts the transaction with `Committed`
  false and the arrays half old, half new. The cancellation is then swallowed as an expected
  debounce pre-emption (`PlcSyncExecutor.cs:146-150`). `DisableSync` also disconnects the socket
  (`PlcLifecycleManager.cs:191`) without waiting: `WaitForPendingSyncAsync` exists on the coordinator
  (`Sync/PlcSyncCoordinator.cs:169`) but is absent from `IPlcSyncService` (`IPlcSyncService.cs:8-29`)
  and is never called on this path.
- **Push racing the compare.** `EnableSync` sets sync enabled (`:129`) before it connects (`:130`),
  and every recipe mutation notifies the sync service while enabled
  (`Recipes/RecipeSession.cs:625-631`). An edit made during the connect schedules a debounced write
  (1000 ms, `PlcSyncExecutor.cs:23`) that can reach the PLC before the reconciliation's read at
  `:338` — after which the recipes match and no dialog is due.

Ruled out as suspects, both by code read: recipe equality is structural, not reference-based
(`Recipes/Recipe.cs:47`, `Recipes/Step.cs:13`), and every transport round-trip is serialised by one
semaphore inside the single `S7Driver` instance (`S7/S7Driver.cs:11,46-58`, registered as a
singleton at `S7/S7Di.cs:18-19`), so concurrent reads cannot interleave on the wire.

## Target end state

| Concern | Today | Target |
| --- | --- | --- |
| Reconciliation outcome | implicit in which `return` ran; 3 of 7 exits silent | one named outcome value, logged on every exit with step counts and the commit flag |
| `Committed == false` | refuses to read, overwrites the PLC | reads the recipe anyway and compares; the dialog decides |
| Read failure | overwrites the PLC, reports success | no push; the failure reaches the message panel through the existing fault seam |
| Write transaction | cancellable between its own managing-area writes | ends at `Committed = true` or fails loudly; disable waits for it |
| Auto-push vs reconciliation | can land first and erase the conflict | held until the reconciliation has read the PLC |
| `ReadRecipeFromPlcAsync` (Load-from-PLC button) | commit-gated | unchanged — the button keeps refusing uncommitted data |

## Why it is safe

`IS7Reader.ReadRecipeFromPlcAsync` (`Plc/IS7Reader.cs:11`) has exactly two callers: the
reconciliation (`PlcLifecycleManager.cs:338`) and the operator's Load-from-PLC command
(`PlcLifecycleManager.cs:211` ← `UI/Coordinator/RecipeCoordinator.cs:204`). Only the first changes;
the second keeps the commit gate, so a torn PLC recipe still cannot be loaded into the editor by a
button press.

`IPlcSyncService` (`Plc/IPlcSyncService.cs:8`) has two implementations: `PlcSyncCoordinator`
(`Sync/PlcSyncCoordinator.cs:12`) and `StubPlcSyncService`
(`SemiStep.Tests/Helpers/StubPlcSyncService.cs:11`). Any member added to it lands in both.

The fault channel already runs end to end as typed `IError`: `PlcSyncCoordinator.EmitFault` →
`Faults` → `RecipeCoordinator.OnPlcFault` (`UI/Coordinator/RecipeCoordinator.cs:520-532`) →
`MessagePanel.ReportFailure(IError)`, localized by `ReasonLocalizer`
(`UI/Localization/ReasonLocalizer.cs:45`). Reporting a reconciliation failure reuses errors that
already exist and are already localized (`PlcCommandFailedError`, `NotConnectedError`,
`RecipeNotCommittedError`), so no new error type and no resx work is required.

The conflict event crosses to the UI thread through `ObserveOn(MainThreadScheduler)` before
`Publish().RefCount()` (`UI/Coordinator/RecipeCoordinator.cs:76-79`) with a single subscriber
established at view-model construction (`UI/MainWindow/MainWindowViewModel.cs:94-100`); no slice
touches that path.

## Guard strategy

Each named guard is a hypothesis the owning slice plan must confirm fires at HEAD.

- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Reconnect"` drives the
  whole reconciliation headlessly: `PlcLifecycleManagerReconnectTests` builds the manager over
  `StubS7Service` and triggers the path with `RaiseStateChanged(PlcConnectionState.Connected)`
  (`SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs:96`), with the PLC answer set through
  `ManagingAreaToReturn` / `RecipeToReturn` (`:82-83`).
- `StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe` (`:147`) asserts today's silent
  overwrite. It is the pinned repro and must be rewritten by the slice that changes the behaviour —
  a slice that leaves it green has not changed anything.
- `dotnet test ... --filter "Area=PlcConflict"` covers the dialog and its two resolutions
  (`SemiStep.Tests/UI/MainWindow/MainWindowConflictResolutionTests.cs`).
- `PlcTransactionExecutorTests` already asserts managing-area write order and offsets
  (`SemiStep.Tests/S7/PlcTransactionExecutorTests.cs:100-111`), which is the seam the write-atomicity
  slice extends with a cancel-mid-transaction case.
- `dotnet build SemiStep.slnx` must stay at zero warnings; the build treats warnings as errors.

## Slices

### Slice plc-reconcile-logging — Status: PENDING
- **Scope:** Make the reconciliation outcome explicit and observable without changing any behaviour.
  Introduce a single named outcome value covering the seven exits of `ReconcileWithPlcAsync`
  (`PlcLifecycleManager.cs:309-370`), have every exit produce one, and log it once at the end of the
  method with the local step count, the PLC step count where known, and the commit flag. Existing
  per-branch warnings stay. After this slice a field log states which exit ran on a toggle where the
  dialog did not appear, which is what the diagnosis of #158 currently lacks.
- **Issue:** #158 (partial — does not close it)
- **Blast radius:** one private method plus one new internal enum; no public surface, no behaviour
  change. Mechanism: an outcome value threaded to a single log call. Surface: nothing observable
  except log lines.
- **Risk:** low
- **Depends on:** independent
- **Stacking base:** master
- **Scope guard:** no behaviour change of any kind — every exit still does exactly what it does
  today, including the three silent pushes. No new error types, no resx, no UI.
- **Plan:** —
- **PR:** —
- **Branch:** —

### Slice plc-reconcile-uncommitted — Status: PENDING
- **Scope:** Stop the commit flag from deciding whether a conflict is possible. Add a
  commit-ignoring recipe read to `PlcTransactionExecutor` (the body of `ReadRecipeFromPlcAsync`
  minus the `Committed` gate at `Sync/PlcTransactionExecutor.cs:205-208`), expose it on `IS7Reader`,
  and have the reconciliation use it so a PLC holding a differing non-empty recipe raises the dialog
  whatever the commit flag says. `ReadRecipeFromPlcAsync` keeps its gate for the Load-from-PLC
  button. The commit flag stays in the log as diagnostic context. `StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe`
  (`PlcLifecycleManagerReconnectTests.cs:147`) is rewritten to assert the dialog instead of the push.
- **Issue:** #158 (partial — does not close it)
- **Blast radius:** `IS7Reader` gains one member, implemented by `S7Service` and `StubS7Service`;
  the reconciliation swaps one call. The Load-from-PLC path is untouched.
- **Risk:** medium — a PLC holding torn data now surfaces as a conflict dialog quoting a step count
  that may be half old and half new. That is the intended trade: an operator prompt beats a silent
  overwrite, and the write-atomicity slice removes the source of torn data.
- **Depends on:** plc-reconcile-logging
- **Stacking base:** master
- **Scope guard:** the read-failure exits keep pushing local in this slice; the commit gate on
  `ReadRecipeFromPlcAsync` stays.
- **Plan:** —
- **PR:** —
- **Branch:** —

### Slice plc-reconcile-read-failure — Status: PENDING
- **Scope:** Stop an I/O failure from being answered with an overwrite. When the managing-area read
  or the recipe read fails (`PlcLifecycleManager.cs:323-330`, `:340-347`), the reconciliation no
  longer calls `NotifyLocalRecipe`; it reports the underlying typed error through the existing fault
  seam so the message panel shows it, and leaves the sync status un-synced. Sync stays enabled and
  connected — the next local edit pushes normally. This needs one semantic entry point on
  `IPlcSyncService` alongside `HandleConnectionLost`, implemented by `PlcSyncCoordinator` as a
  status change plus `EmitFault`, and by `StubPlcSyncService` as a recorder.
- **Issue:** #158 (closes it)
- **Blast radius:** `IPlcSyncService` gains one member (two implementations); the coordinator gains
  one method; the UI path is the already-wired `Faults` observable.
- **Risk:** medium — this is the slice that can make a transient PLC read failure visible as an
  operator-facing message where today it is invisible. That is the point, but it changes what the
  panel shows on a flaky link.
- **Depends on:** plc-reconcile-uncommitted
- **Stacking base:** master
- **Scope guard:** no new error type and no resx changes — the errors raised already exist and are
  already localized. No retry logic.
- **Plan:** —
- **PR:** —
- **Branch:** —

### Slice plc-write-atomic — Status: PENDING
- **Scope:** Make the recipe write transaction untearable by a sync disable. Separate the debounce
  cancellation from the write cancellation in `PlcSyncExecutor` so `Reset()` (`:61-70`) can cancel a
  pending debounce without aborting a write already inside `WriteRecipeDataAsync`, and make
  `DisableSync` (`PlcLifecycleManager.cs:183-197`) wait for the in-flight write, bounded, before it
  disconnects — which requires `WaitForPendingSyncAsync` (`Sync/PlcSyncCoordinator.cs:169`) on
  `IPlcSyncService`. The invariant to prove: no operator action can leave the PLC with
  `Committed == false`; only a hard failure or process death can, and both are logged.
- **Issue:** #180 (closes it)
- **Blast radius:** the sync executor's cancellation model and the disable path. Application
  shutdown still has to cancel promptly, so the bound on the wait is load-bearing.
- **Risk:** high — this changes cancellation semantics on the path that also runs during exit.
  `SemiStep.Tests/S7/PlcSyncCoordinatorTests` and the exit flow (`Docs/architecture/exit-flow.md`)
  are the surfaces to re-check.
- **Depends on:** independent — no file overlap with the reconcile slices beyond `IPlcSyncService`
- **Stacking base:** master
- **Scope guard:** no change to the write protocol itself (order, offsets, retry count) and no
  change to the reconciliation.
- **Plan:** —
- **PR:** —
- **Branch:** —

### Slice plc-push-gate — Status: BLOCKED — held for the hardware test after plc-write-atomic; set to PENDING if the race is observed, DROPPED if it is not
- **Scope:** Hold the debounced auto-push until the reconciliation has read the PLC, so an edit made
  while the connection is being established cannot overwrite the PLC recipe before it is compared.
  The gate opens when the reconciliation reaches an outcome; a snapshot queued while it was closed
  is flushed afterwards, and is dropped if the operator resolves the conflict by loading the PLC
  recipe.
- **Issue:** #181 (closes it)
- **Blast radius:** the coordinator's scheduling path, which every recipe mutation reaches.
- **Risk:** high — a gate that fails to open strands the sync silently. Needs an explicit
  open-on-every-outcome guarantee, including the exception paths.
- **Depends on:** plc-reconcile-read-failure
- **Stacking base:** master
- **Scope guard:** the gate covers the reconciliation window only; it is not a general write lock.
- **Plan:** —
- **PR:** —
- **Branch:** —

## Manual verification gate

After `plc-write-atomic` merges and before `plc-push-gate` is planned, the four merged slices are
tested against a real PLC. The headless suite proves the branches; only hardware proves the operator
sees the dialog on the toggle that used to swallow it. Each step names one observable outcome.

1. Load a recipe of several steps, enable sync, wait for the status to read synced. The PLC holds
   that recipe and `Committed` is true.
2. Disable sync, delete two steps, enable sync again. The conflict dialog appears and quotes the
   local and PLC step counts. This is the #158 repro.
3. Answer "keep local". The PLC takes the shortened recipe; the status returns to synced.
4. Repeat step 2 and answer "load from PLC". The grid takes the PLC recipe; no further write follows.
5. Edit a step and, within one second, disable sync. Re-enable it. The dialog appears rather than a
   silent switch to synced — the write either finished or is reported, never left half-applied.
6. Pull the network cable, then enable sync. The message panel reports the read failure and the
   status does not read synced; the PLC keeps its recipe.
7. Enable sync and edit a step while the connection is still being established. If the dialog is
   swallowed, the #181 race is real and `plc-push-gate` moves to PENDING. If several attempts cannot
   reproduce it, the slice is DROPPED and the issue closed as not-reproducible.

The application log is read after every step: each toggle must carry exactly one reconciliation
outcome line naming the exit taken.

## Close condition

Every slice not marked DROPPED has a MERGED PR. #158 closes with `plc-reconcile-read-failure`, #180
with `plc-write-atomic`, #181 with `plc-push-gate` or as not-reproducible at the manual gate.

## Rejected alternatives

Settled during design — do not relitigate without new facts.

- **Log the branches and change nothing else** — the diagnosis is worth shipping first, which is why
  it is the first slice, but it leaves the data-loss shape intact: `Committed == false` still means
  the PLC gets overwritten without being read.
- **Treat `Committed == false` as "the PLC holds nothing" and keep pushing local** — defensible only
  if the flag were firmware-owned. It is written exclusively by this application
  (`Sync/PlcTransactionExecutor.cs:243-266`), so false means "our own last write did not finish",
  and the data behind it may be almost entirely a recipe the operator cares about.
- **Abort the sync enable when the PLC recipe cannot be read** — heavier than the failure warrants:
  the connection is up and the operator asked for sync. Reporting the failure and holding the push
  keeps the operator informed without a forced re-toggle.
- **Drop the commit gate on `ReadRecipeFromPlcAsync` as well** — the Load-from-PLC button replaces
  the editor's contents; loading a half-written recipe there with no signal is worse than refusing.
- **Fix the torn write first, then the dialog** — the dialog is what the operator reported, the
  reconcile slices are lower-risk, and the reconciliation must be honest about an uncommitted PLC
  regardless of how it got that way (a PLC restart or a DB re-download also produce it).
- **Ship all five slices, then test on hardware once** — the first four are what the operator
  reported and what the headless tests can prove; the fifth targets a race no field log has shown.
  Testing against a real PLC after the fourth slice is what tells the two apart.

## Open forks for the operator

- **Is the push gate worth its risk?** Settled as held: the slice stays BLOCKED until step 7 of the
  manual verification gate either reproduces the race or fails to. The other four slices stand
  regardless.
- **Should the conflict dialog say why the PLC state is suspect?** Once the commit flag no longer
  blocks the comparison, the dialog can quote step counts only (today's shape,
  `UI/MainWindow/MainWindowViewModel.cs:229-230`) or add "the PLC recipe was not committed". The
  second is a resx change and an extra slice. The design stands either way.
