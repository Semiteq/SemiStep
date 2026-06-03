# Error-Reporting Tidy (reporting seam + PLC fault channel / P3)

## Overview

Two stacked PRs that tidy how errors reach the user, building on already-merged #57/#58.

- **PR A — reporting seam.** The UI surfaces only the FIRST error of a failed `Result` (`result.Errors[0].Message`, ~8 sites) while logging shows ALL of them (`string.Join("; ", Errors.Select(e => e.Message))`, ~9 sites). The user sees less than the log, and both idioms are duplicated. Introduce one seam (`FormatErrors` / `ReportFailure`) that removes the duplication and fixes the asymmetry.
- **PR B — PLC fault channel ("P3", stacked on PR A).** The PLC state stream is `IObservable<Result<PlcSessionSnapshot>>` on a hot `BehaviorSubject`, conflating state with transient failures that no consumer reads (the failure text is invisible to the user). Make the state stream a bare `IObservable<PlcSessionSnapshot>` and deliver faults as discrete one-shot events routed to the MessagePanel via the PR A seam, so "PLC connection lost" / sync failures become user-visible. Persistent "disconnected" stays in the status bar.

Benefit: errors become consistently visible, event and state are separated, the FluentResults-on-hot-observable anti-pattern is removed, and the reporting path is consistent across the whole app.

## Context (from discovery)

- `SemiStep.UI/MessageService/MessagePanelViewModel.cs` — two channels: transient `_operationEntry` (`ReportError/Warning/Success`) + persistent `_validationEntries` (`RefreshReasons`).
- Panel sites using `Errors[0].Message`: `RecipeFileViewModel.cs:106,126,141`; `RecipeCoordinator.cs:408`; `ClipboardViewModel.cs:115,155`; `MainWindowViewModel.cs:143,181`; `RecipeGridViewModel.cs:204,226`.
- Logging join-sites: `RecipeCoordinator.cs:177,215,339,378,407,504`; `ClipboardViewModel.cs:140`; `App.axaml.cs:107`; `Program.cs:77`.
- PLC: `SemiStep.Core/Plc/IPlcSyncService.cs` (`PlcState`), `Sync/PlcSyncCoordinator.cs` (`BehaviorSubject<Result<PlcSessionSnapshot>>`, `PublishSnapshot`, `_connectionLost`), `Sync/PlcSyncExecutor.cs` (`setStatus` callback, `_pendingErrorMessage`), `State/PlcSessionSnapshot.cs` (`InitialState` is `Result<...>`), `SemiStep.UI/Coordinator/RecipeCoordinator.cs` (subscribes `PlcState`, re-publishes), `MainWindow/MainWindowViewModel.cs` (discards payload), `SemiStep.Tests/Helpers/StubPlcSyncService.cs`.
- Conventions: FluentResults; tabs size 4; usings above file-scoped namespace; constructor injection; KISS/YAGNI.

## Development Approach

- **Testing approach**: Regular (code change, then tests within the same task).
- One PR = one logical change; PR B stacks on PR A.
- Build + `dotnet format` + full test suite green before advancing.
- Preserve untouched: S7Service connection FSM, file-lock ownership, TransportSerializer, the single-`ObserveOn`-at-source + `Publish().RefCount()` threading model.

## Testing Strategy

- Unit tests: `FormatErrors` (single + multi error), `ReportFailure` (context prefix, all-errors visibility); PLC coordinator tests re-pointed from `IsFailed`-on-`PlcState` to the `Faults` channel; fault emitted once on connection-loss and on sync failure; bare `PlcState` always emits Ok-equivalent snapshots.
- Commands: `dotnet build SemiStep/SemiStep.slnx`, `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`, `dotnet format SemiStep/SemiStep.slnx`.

## Progress Tracking

- Mark `[x]` on completion; ➕ new tasks; ⚠️ blockers. Keep in sync.

## Solution Overview

- **PR A**: `ResultReportingExtensions` in `SemiStep.UI/MessageService/` with `FormatErrors(this IResultBase)` and `ReportFailure(this MessagePanelViewModel, IResultBase, string? context = null)`. All panel sites route through `ReportFailure`; logging join-sites route through `FormatErrors`. The `_operationEntry`/validation two-channel model is unchanged — only the routing into it is unified. A short `Docs/` note codifies the contract.
- **PR B**: `PlcState` becomes `IObservable<PlcSessionSnapshot>`; a new `IObservable<IError> Faults` carries transient faults emitted once at the event sites. `RecipeCoordinator` bridges `Faults` to the MessagePanel via the PR A seam. Because faults are now one-shot events (not a retained `Result.Fail`), the `_connectionLost` flag (and its three clear-sites) and the executor's `_pendingErrorMessage` lose their only consumer and are removed — see the deviation note.

> **Deviation from the brief (confirm):** the brief said "preserve `_connectionLost` mechanics." Event-based fault delivery makes the persisted flag and `_pendingErrorMessage` dead code (their sole purpose was to drive the now-removed `Result.Fail` on the state stream). This plan removes them as the clean result; the *trigger* (`HandleConnectionLost`) and the *timing* are preserved. If literal preservation is required, keep the flag and skip its removal — but it will be flagged as dead by review.

## Technical Details

- `Faults` is `Subject<IError>` in the coordinator, exposed as `IObservable<IError>`. The executor gets a new `Action<IError> reportFault` constructor callback wired by the coordinator to `_faults.OnNext`. `HandleConnectionLost` calls `_faults.OnNext(new Error("PLC connection lost"))`.
- **Executor fault sites (enumerate explicitly).** `PlcSyncExecutor` has four `setStatus(Failed)` sites plus one silent abort:
  - `StartDebounce` catch (unhandled sync exception) → emit `reportFault`.
  - `CheckCanSyncAsync` `IsRecipeActiveAsync` failure → emit `reportFault`.
  - `CheckCanSyncAsync` "recipe active" block → emit `reportFault`.
  - `WriteSyncAsync` write failure → emit `reportFault`.
  - `CheckCanSyncAsync` `!IsConnected` early-return → **stays silent (no fault, no `setStatus(Failed)`)** — `HandleConnectionLost` already fired the connection-lost fault; emitting here would duplicate the message. (Documented in completed plan `20260603-plc-sync-state-rework.md`.)
- **`PlcLifecycleManager` is a pass-through facade** (`PlcState => _syncService.PlcState`); it must mirror the new `PlcState` type and add a `Faults => _syncService.Faults` pass-through. `RecipeCoordinator` subscribes `_plc.PlcState`/`_plc.Faults` (the lifecycle manager), not the coordinator directly.
- **`Faults` is single-consumer** (only `RecipeCoordinator`): a plain `ObserveOn(MainThreadScheduler)` subscription is correct; no `Publish().RefCount()` (that pattern is for the shared multi-subscriber channels). The change from a replaying `BehaviorSubject` to a fire-and-forget `Subject` is safe because `RecipeCoordinator` subscribes once in `Initialize()` before any PLC activity — no fault can fire before the subscription exists.
- `PlcSyncStatus` (incl. `Failed`) is retained as the state label; the fault is the additional transient message — the two-channel model end to end (state stream + fault event; persistent connection label in the status bar).
- Fault text is created in Core (typed `Error`), per the agreed ownership; the UI only routes `error.Message` through the seam.

## What Goes Where

- **Implementation Steps** (`[ ]`): code + tests + branch/PR mechanics.
- **Post-Completion** (no checkboxes): live PLC check that a real drop now shows a MessagePanel message and the status bar still shows "Disconnected".

## Implementation Steps

### Task 1 (PR A): Add the reporting seam

**Files:**
- Create: `SemiStep/SemiStep.UI/MessageService/ResultReportingExtensions.cs`
- Create: `SemiStep/SemiStep.Tests/UI/ResultReportingExtensionsTests.cs`

- [x] Add `FormatErrors(this IResultBase result)` => `string.Join("; ", result.Errors.Select(e => e.Message))`.
- [x] Add `ReportFailure(this MessagePanelViewModel panel, IResultBase result, string? context = null)` => `panel.ReportError(context is null ? result.FormatErrors() : $"{context}: {result.FormatErrors()}")`.
- [x] Write tests: `FormatErrors` single error, multi error (join order); `ReportFailure` with and without context (assert the panel entry contains ALL error messages).
- [x] `dotnet build SemiStep/SemiStep.slnx -clp:ErrorsOnly` — 0 errors.
- [x] Run the new tests — must pass.

### Task 2 (PR A): Route all panel + logging sites through the seam

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`, `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`, `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`, `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`, `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`, `SemiStep/SemiStep.UI/App.axaml.cs`, `SemiStep/SemiStep.UI/Program.cs`

- [x] Replace the ~8 `ReportError(result.Errors[0].Message)` sites with `ReportFailure(result, context)`, preserving existing prefixes via `context` ("Failed to save recipe", "Step {n}", "PLC reconnect", etc.). Exception-based `ReportError($"... {ex.Message}")` sites are NOT Result-based — leave them.
- [x] **Per-site decision for embedded-literal messages.** `RecipeGridViewModel.cs:226` kept its verbatim wording via option (b): `ReportError($"Step {n}: Failed to change action - {result.FormatErrors()}")`.
- [x] Replace the UI-side logging `string.Join("; ", ...Errors.Select(e => e.Message))` sites with `result.FormatErrors()`.
- [x] `dotnet build SemiStep/SemiStep.slnx -clp:ErrorsOnly` — 0 errors.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green (behavior change: failed operations now surface ALL error messages; update any test asserting a single-message panel entry).

### Task 3 (PR A): Document the contract; format, commit, push, PR

**Files:**
- Create: `Docs/error-reporting.md`

- [x] Write a short `Docs/error-reporting.md`: the two channels (transient operation entry vs persistent validation reasons), Core owns error text, UI routes via `ReportFailure`, events -> operation channel, state -> reasons/status bar. Match the language of surrounding `Docs/` files.
- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] Commit on `error-report-seam` (`refactor: unify Result-to-MessagePanel reporting; surface all operation errors`); verify `git log origin/master..HEAD --oneline` is only this change; push; `gh pr create --base master`.

### Task 4 (PR B): Branch off PR A (stacked)

- [x] From the worktree on `error-report-seam`, `git switch -c plc-fault-channel`.

### Task 5 (PR B): Bare PlcState + Faults channel in Core

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/IPlcSyncService.cs`, `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs`, `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncExecutor.cs`, `SemiStep/SemiStep.Core/Plc/State/PlcSessionSnapshot.cs`, `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`

- [x] `IPlcSyncService`: change `PlcState` to `IObservable<PlcSessionSnapshot>`; add `IObservable<IError> Faults`.
- [x] `PlcSessionSnapshot.InitialState`: bare `PlcSessionSnapshot` (drop the `Result.Ok` wrapper).
- [x] `PlcSyncCoordinator`: `BehaviorSubject<PlcSessionSnapshot>` + `Subject<IError> _faults` (expose as `Faults`); `PublishSnapshot` always publishes the snapshot (remove both `Fail` branches); `HandleConnectionLost` emits `_faults.OnNext(new Error("PLC connection lost"))`; remove `_connectionLost` and its clear-sites (see deviation note); dispose `_faults` in `Dispose`.
- [x] `PlcSyncExecutor`: add `Action<IError> reportFault` ctor callback; emit `reportFault(new Error(message))` at the four `setStatus(Failed)` sites enumerated in Technical Details; keep the `!IsConnected` early-return SILENT (no fault — avoids duplicating the connection-lost message); remove `_pendingErrorMessage`/`PendingErrorMessage` (now unused).
- [x] `PlcLifecycleManager`: change the `PlcState` pass-through property type to `IObservable<PlcSessionSnapshot>`; add `public IObservable<IError> Faults => _syncService.Faults;`.
- [x] `dotnet build SemiStep/SemiStep.slnx -clp:ErrorsOnly` — resolve all references to the removed `Result` wrapper / fields.

### Task 6 (PR B): Bridge faults to the MessagePanel in the UI

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`, `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [x] `RecipeCoordinator`: subscribe `_plc.Faults`, `ObserveOn(MainThreadScheduler)`, route each `IError` to the MessagePanel via the PR A seam (`ReportFailure`/`ReportError(error.Message)`); dispose the subscription with the others. Single-consumer channel → a plain `ObserveOn` subscription, NOT `Publish().RefCount()`.
- [x] Change `PlcState`/`PlcStateChanged` and `OnPlcStateChanged`/`LogPlcStateChange` to carry bare `PlcSessionSnapshot` (drop `Result`/`IsFailed` handling). `MainWindowViewModel` already discards the payload — adjust the type only.
- [x] `dotnet build SemiStep/SemiStep.slnx -clp:ErrorsOnly` — 0 errors.

### Task 7 (PR B): Update stub + tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Helpers/StubPlcSyncService.cs`, `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`, `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs`, `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`, any other test asserting `IsFailed` on `PlcState`

- [x] `StubPlcSyncService`: `PlcState` -> bare snapshot subject; add a `Faults` subject + push helpers. Update `PushPlcState(Result.Ok(...))` signature to bare snapshot and fix its callers — incl. `UIFixture.cs` (`PushPlcState(Result.Ok(...))`) and `RecipeCoordinatorTests.cs` (`PushPlcState(Result.Fail(...))`).
- [x] **Replace the now-contradicting UI test** `RecipeCoordinatorTests.PlcStateChange_Failure_DoesNotAddEntriesToMessagePanel` (it asserted a `Result.Fail` on `PlcState` adds NO panel entry — the opposite of the new contract). Replace with a positive test: a `Faults` emission routes to the MessagePanel operation channel (entry appears with the fault message).
- [x] **Rewrite, don't re-point, the `_connectionLost`-clearing regression tests** in `PlcSyncCoordinatorTests` (the re-enable-clears-flag / reconnect-clears-flag tests). With one-shot faults there is nothing lingering to clear; replace with: connection-loss emits exactly ONE fault on `Faults`, and a subsequent reconnect/re-enable emits NO further fault.
- [x] Re-point remaining assertions that checked `IsFailed`/`Errors` on `PlcState` to assert on `Faults`; `PlcState` now always carries a snapshot.
- [x] Add: sync-write failure emits a fault carrying the message; the `!IsConnected` debounce-abort emits NO fault (no duplicate of the connection-lost fault).
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.

### Task 8 (PR B): Format, commit, push, stacked PR

- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] Commit on `plc-fault-channel` (`refactor: deliver PLC faults via a discrete channel; bare PlcState snapshot stream`); push; `gh pr create --base error-report-seam` (stacked; note dependency on PR A).

### Task 9: Verify acceptance criteria

- [x] PR A: failed operations surface all error messages. Grep acceptance: `Errors[0]` and `string.Join("; "` across `SemiStep/SemiStep.UI` return only intentionally-excluded sites (exception-based handlers), nothing Result-based.
- [x] PR B: `PlcState` is bare; `Faults` channel delivers connection-loss/sync-failure once; `_connectionLost`/`_pendingErrorMessage` removed; preserved subsystems untouched (`git diff origin/master...HEAD --stat`).
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green on the PR B branch.

### Task 10: [Final] Plan housekeeping

- [x] Mark all checkboxes; note deviations.
- [x] Move this plan to `Docs/plans/completed/` (commit on the PR B branch).

## Post-Completion

*Manual / external — no checkboxes.*

- Live PLC (192.168.0.150): trigger a real connection drop while sync is enabled → a "PLC connection lost" message must appear in the MessagePanel (transient) AND the status bar must still show "Disconnected" (persistent). Confirm a sync write failure surfaces its message in the panel.
