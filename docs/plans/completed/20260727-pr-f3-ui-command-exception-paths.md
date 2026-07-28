# PR-F3 (#113): UI command exception paths, rebased on the pipe foundation

## Overview

Issue #113 — unhandled exception paths in UI commands — rebased onto the merged pipe foundation
(PR-F1 report+log ring, PR-F2 global backstop).

The foundation narrowed *one* class of this issue, not all of it. F2's `WithExceptionHandler` replaced
the default handler for **unsubscribed** `ThrownExceptions` only — the forgotten-subscription class. It
did NOT de-crash the hotkeys. ReactiveUI delivers a command execution exception to BOTH channels: the
`ThrownExceptions` stream (which F1's handlers report) AND the `Execute()` observable's `OnError`. The
five hotkeys call `.Execute().Subscribe()` with no `onError`, so that second channel rethrows inside a
dispatcher job and unwinds to `Program.Main`'s `Log.Fatal` — the process dies. So today, post-F2, a
Ctrl+C whose command throws BOTH shows a panel report AND kills the app. Tasks 1/4 (hotkeys) and Task 5
(async-void dialogs) are therefore still **crash-prevention**, not polish; Tasks 2/3 add specific per-site
handling and close the `canEdit` gating bypass.

What F3 does:
1. Add an `ExecuteIfPossible` extension and route the five `MainWindow` hotkeys through it — fixes the
   unguarded-invocation crash AND the gating bypass for the *mutating* hotkeys (Delete/Cut/Paste run
   while `canEdit` is false today; Copy and ToggleOrientation are not `canEdit`-gated and only gain
   crash-safety).
2. Wire `RecipeCommandsViewModel`'s four commands through the F1 `ReportThrownExceptions` extension and
   report the failed `Result`s it currently drops.
3. Add the missing `ToggleOrientationCommand` `ThrownExceptions` handler.
4. Guard the two async-void dialog paths (`OnWindowClosing`, `OnSaveCompleted`) — interim, superseded by #114.

Conflict fire-and-forget (original finding 3) is mostly, but not fully, covered by the foundation:
`MainWindowViewModel.cs:86-90` wraps the callback in `Guarded("PLC conflict handling", …)` (F1, catches
the synchronous segment) and `HandleConflictAsync`'s inner try/catch covers the `ShowDialog` await. The
residual hole is the **post-await tail** — `ResolveConflict`/`ReportFailure` throwing after the dialog
faults the discarded task, and `TaskScheduler.UnobservedTaskException` only fires nondeterministically at
GC with no context or user message (a diagnostic, not handling). Task 3 already edits this file, so it
closes that tail (see Task 3). #114 restructures the dialog onto `Interaction` regardless.

What F3 does NOT do (with rationale):
- **Config-side zero-actions fail-fast: DEFERRED to #111, as a tracked task.** With F2's backstop + task 2's
  `RecipeCommandsViewModel` handler, a zero-primary-actions config no longer crashes on `AddStep` — it
  reports a message (the config is visibly broken, not silently degraded). Rejecting such a config at load
  (the proper root-cause fix) means running the action resolver during config validation and routing the
  defect through a `Result`, which is exactly #111's charter ("route config defects through `Result`
  instead of constructor throws"). A partial version here would be redone by #111. **This must be filed as
  an explicit task/sub-issue on #111 before F3 is archived**, so the deferral does not evaporate — not left
  as a plan footnote.

## Context (grounded on current master, post F1+F2)

- Hotkeys unguarded: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs:61,72,78,84,90` — five
  `command.Execute().Subscribe()` with no `onError` and no `CanExecute` check (ToggleOrientation,
  DeleteStep, Copy, Cut, Paste). `ReactiveCommand.Execute()` ignores `canExecute`, so the mutating ones
  run even while `canEdit` is false: `DeleteStepCommand` and `Cut`/`Paste` are `canEdit`-gated
  (`ClipboardViewModel.cs:56-59`). `CopyStepCommand` is gated on selection only (`ClipboardViewModel.cs:55`,
  `CanDeleteStep`), not `canEdit`, so it correctly stays available in read-only mode; `ToggleOrientationCommand`
  has no gate. `OnKeyDown`'s `IsEditing` gate (`:67`) is a real requirement — keep it.
- `RecipeCommandsViewModel` (`SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs`): four
  `ReactiveCommand.Create` commands (`:37-40`), NO `ThrownExceptions` handler anywhere. `AddStep` (`:69-81`)
  calls `_coordinator.GetDefaultActionId()` (throws `InvalidOperationException` on zero actions); `AddStep`/
  `DeleteStep` act only on `IsSuccess` (`:77,95`), silently dropping failed `Result`s. It does NOT inject
  the panel or a logger.
- `ToggleOrientationCommand` (`SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs:63`): no
  `ThrownExceptions` handler (F1 handled `ToggleSyncCommand`/`OpenStyleEditorCommand` at `:71-75` but
  deliberately left this one for #113).
- The F1 seams to reuse: `ReportThrownExceptions(this ReactiveCommand<,>, MessagePanelViewModel, ILogger, string)`
  (`SemiStep/SemiStep.UI/MessageService/ReactiveCommandReportingExtensions.cs`), which delegates to
  `ExceptionReporter.ReportAndLog` (`ExceptionReporter.cs`); and `MessagePanelViewModel.ReportFailure`
  (`ResultReportingExtensions.cs`).
- async-void, unguarded: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs:100-140` (`OnWindowClosing`,
  try/finally, no catch around the awaits) and `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml.cs:31-41`
  (`OnSaveCompleted`, `async void` from an Rx `Subscribe`, awaits `RestartPromptDialog.ShowDialog`). F1
  wired `GridStyleEditorViewModel.SaveCommand.ThrownExceptions` (the command), but this window-side async
  void is separate and still unguarded.
- F2 has no Avalonia dispatcher hook, so an async-void throw is NOT caught by the backstop — it unwinds
  the dispatcher loop into `Program.Main`'s `Log.Fatal` and the process dies. The guards prevent that.
- DI construction sites for `RecipeCommandsViewModel` that gain the new params: `UiDi.cs` (singleton),
  `SemiStep.Tests/UI/Helpers/UIFixture.cs`, `SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`
  (verify exact lines at implementation time; `UIFixture` already exposes `MessagePanel`).

## Development Approach

- Regular (code, then tests). Warnings are errors; build stays clean.
- Reuse the F1 extension and `ExceptionReporter`; do not add a parallel reporting path.
- Tests use the shared `RecordingLogger<T>` (`SemiStep.Tests/Helpers/RecordingLogger.cs`) for log
  assertions and `NullLogger<T>.Instance` elsewhere.
- `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` after each task.

## Acceptance Evidence

**Automatable:**
1. `ExecuteIfPossible`: a command whose execute throws, invoked via the extension, does not rethrow and
   its `ThrownExceptions` fires once; a command with `canExecute == false` does not run. `--filter "FullyQualifiedName~ExecuteIfPossible"`.
2. `RecipeCommandsViewModel`: a thrown command exception reports to the panel (specific context, not the
   generic backstop text) and logs; a failed `Result` from `AddStep`/`DeleteStep` lands a panel entry
   instead of being dropped. `--filter "FullyQualifiedName~RecipeCommandsViewModel"`.
3. `ToggleOrientationCommand` fault reports to the panel. (Covered under MainWindowViewModel filter.)
4. async-void guards: a forced throw in the `OnWindowClosing` / `OnSaveCompleted` path is contained
   (reported/closed gracefully), not crashing the headless app.

**Manual smoke:** with a recipe running on the PLC (`canEdit` false), Delete / Ctrl+V do NOT mutate the
recipe (gating fix). Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: Add the `ExecuteIfPossible` command extension

**Files:**
- Create: a dedicated `SemiStep/SemiStep.UI/`-side invocation-extensions class (e.g. `CommandInvocationExtensions` — invocation is NOT reporting, so it does not belong in `ReactiveCommandReportingExtensions`).
- Create: the matching test file.

- [x] add `ExecuteIfPossible<TResult>(this ReactiveCommand<Unit, TResult> command)`: `if (((ICommand)command).CanExecute(null)) { ((ICommand)command).Execute(null); }` — `ICommand.Execute` is internally `Execute().Catch(Empty).Subscribe()`, so it never rethrows. Comment: it prevents the unguarded-invocation crash, honors `canExecute` (which `ReactiveCommand.Execute()` ignores), AND — because `ICommand.CanExecute` is false while a command is executing — suppresses re-entrant hotkey mashes during an async command for free.
- [x] test: a throwing command invoked via `ExecuteIfPossible` does not rethrow and its `ThrownExceptions` fires once; a `canExecute == false` command does not execute.
- [x] run the filter — green before next task.

### Task 2: Wire `RecipeCommandsViewModel` through the F1 extension + report Results

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelReportingTests.cs`

- [x] inject `MessagePanelViewModel` + `ILogger<RecipeCommandsViewModel>` (add usings for `MessageService`, `Microsoft.Extensions.Logging`).
- [x] `command.ReportThrownExceptions(_messagePanel, _logger, "Add step failed"/"Delete step failed"/"Undo failed"/"Redo failed").DisposeWith(_disposables)` for the four commands.
- [x] `AddStep`/`DeleteStep`: on `result.IsFailed`, `_messagePanel.ReportFailure(result)` and return; else `RequestSelection`.
- [x] update the direct construction sites to pass `MessagePanel` / `NullLogger<RecipeCommandsViewModel>.Instance`: `UIFixture.cs:148` and BOTH sites in `RecipeCommandsViewModelCanExecuteTests.cs` (`:30` and `:109`).
- [x] tests: a thrown command exception reports with the specific context + logs; a failed `Result` reports. `RecipeCoordinator` is `sealed` (not mockable) — use a stub `IRecipeGridSurface` as the throw seam (throw in `RequestSelection`/`SelectedStepIndex` after a successful coordinator result); drive the failed-`Result` path via coordinator state that returns `IsFailed`.
- [x] run the filters — green before next task.

### Task 3: `ToggleOrientationCommand` handler + close the conflict post-await tail

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: the MainWindowViewModel reporting test.

- [x] `ToggleOrientationCommand.ReportThrownExceptions(MessagePanel, _logger, "Orientation toggle failed").DisposeWith(_disposables)` (mirror `:71-75`).
- [x] close the conflict fire-and-forget post-await tail: add a **separate** try/catch around the post-dialog body (`ResolveConflict`/`ReportFailure`, `:240-245`) with a resolution-specific context (e.g. "PLC conflict resolution failed" via `ExceptionReporter.ReportAndLog`) — do NOT widen the existing dialog-show catch (`:227-233`), which reports "Failed to show PLC conflict dialog" and would mislabel a resolution failure. So a throw in the tail is logged + reported instead of faulting the discarded task and relying on the nondeterministic `TaskScheduler` GC hook. (Interim; #114 moves this dialog onto `Interaction`.)
- [x] test: a faulting `ToggleOrientationCommand` reports to the panel; a throw in the conflict post-dialog body is reported, not left to the unobserved-task hook.
- [x] run `--filter "FullyQualifiedName~MainWindowViewModel"` — green.

### Task 4: Route the `MainWindow` hotkeys through `ExecuteIfPossible`

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Create/Modify: a headless hotkey test.

- [x] add the `using` for the extension; replace the five `.Execute().Subscribe()` calls (`:61,72,78,84,90`) with `.ExecuteIfPossible()`. (Tasks 2/3 run first so all five reachable commands have `ThrownExceptions` handlers — Clipboard from F1, DeleteStep from task 2, ToggleOrientation from task 3 — which buys a *specific* panel message; note `ExecuteIfPossible` is crash-safe on its own even before that, since the swallowed `Execute()` channel still delivers to `ThrownExceptions` → F2's generic backstop.)
- [x] test: a hotkey whose command throws does not crash and reports once; a hotkey does not mutate when `canExecute` is false.
- [x] run the filter — green.

### Task 5: Guard the two async-void dialog paths (interim; #114 supersedes)

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs` (`OnWindowClosing`)
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml.cs` (`OnSaveCompleted`)
- Create/Modify: headless tests.

- [x] `OnWindowClosing`: added a `catch (Exception ex)` around the dialog/`HandleExitChoiceAsync` block; reports via `ViewModel?.MessagePanel.ReportError($"Exit failed: {ex.Message}")`; window stays open (`e.Cancel` already true); `finally` still resets `_exitChoiceInProgress`.
- [x] `OnSaveCompleted`: wrapped the body in try/catch; on catch, surfaces on the editor's own `ErrorMessage` via the F1 `ReportSaveException(ex)` seam and still `CompleteEditorClose(false)` so the editor closes.
- [x] each catch carries a concise WHY comment (async-void throw unwinds the dispatcher into `Program.Main` and kills the process). The literal "interim, superseded by #114" marker is deliberately NOT written into code: CLAUDE.md forbids transitional/process comments; the interim nature lives in this plan and the PR. See [decision].
- [x] tests: `OnSaveCompleted` fault-containment covered by a real headless test (`GridStyleEditorWindowTests.OnSaveCompleted_RestartPromptThrows_ContainsFaultAndClosesEditor`). `OnWindowClosing` fault-injection skipped — no headless seam (`WindowClosingEventArgs` has an internal Avalonia ctor; the real Close() flow keeps the owner visible so `ShowDialog` cannot be forced to throw); manual smoke. See [decision].
- [x] `--filter "Component=UI"` — 900 passed, 0 failed, 0 warnings.

### Task 6: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md`

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1489 passed, 0 failed (8 explicit perf probes skipped).
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors
- [x] `dotnet format SemiStep.slnx` — no changes required
- [x] update `error-reporting.md`: `ExecuteIfPossible` as the sanctioned imperative-invocation seam (keeps commands inside the boundary + honors `canExecute`); note the async-void guards are interim pending #114.
- [x] mark this plan for archival at delivery (archival deferred to delivery/ship; do NOT move it mid-run).

## Post-Completion

**Behavior change (PR note):** routing hotkeys through `ExecuteIfPossible` makes Delete / Ctrl+X / Ctrl+V
inert while `canEdit` is false (a recipe running on the PLC) — the intended `canEdit` semantics they bypass
today. **Ctrl+C stays available** (Copy is gated on selection, not `canEdit`), and ToggleOrientation gains
crash-safety only. Call it out in the PR.

**Interim guards:** Task 5's async-void try/catch on `OnWindowClosing`/`OnSaveCompleted` are superseded by
#114, which moves those dialogs onto the awaitable `Interaction` seam and deletes the guards. Guards-now
is correct because F2's backstop does not hook the dispatcher, so without them a dialog throw still crashes.

**Deferred (must be tracked):** the config-side zero-primary-actions fail-fast (reject the config at load,
route through `Result`) rides with #111. File it as an explicit task/comment on #111 before this plan is
archived — do not let it live only here. F2 + task 2 already make a zero-action config non-crashing.

**Executed by exec:**
- branch: ui-command-exception-paths

## Verify it yourself

Most of this is invisible until a command or dialog faults, so the proof is the regression tests that
reproduce each path.

- **Hotkey no longer crashes + reports specifically:** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ExecuteIfPossible"` and `--filter "FullyQualifiedName~MainWindowHotkey"` — a throwing command invoked via `ExecuteIfPossible` does not rethrow and reports once. On `master`, the raw `.Execute().Subscribe()` rethrows into the dispatcher and kills the app.
- **RecipeCommands reports instead of dropping:** `--filter "FullyQualifiedName~RecipeCommandsViewModel"` — a thrown command exception reports the specific context + logs; a failed `Result` from Add/Delete now lands a panel entry.
- **ToggleOrientation + conflict resolution guarded:** `--filter "FullyQualifiedName~MainWindowViewModel"`.
- **async-void dialogs contained:** `--filter "FullyQualifiedName~GridStyleEditorWindow"` (restart-prompt fault → `ErrorMessage`, editor closes) and `--filter "FullyQualifiedName~ExitFlow"` (`ShowExitChoice_DialogShowThrows...` — exit fault contained, window stays open).
- **Gating fix (manual):** with a recipe running on the PLC (`canEdit` false), Delete / Ctrl+X / Ctrl+V do NOT mutate the recipe; Ctrl+C still copies (selection-gated).
- **No regression:** full `dotnet test` (1489 pass) + `dotnet build SemiStep.slnx` (0 warnings).
