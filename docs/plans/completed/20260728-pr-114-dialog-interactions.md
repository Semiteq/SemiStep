# PR-114: Route MainWindowViewModel dialogs through Interactions

## Overview

Issue #114 — `MainWindowViewModel` depends on `Avalonia.Controls.Window` and news two concrete window
types to show dialogs, which makes the style-editor and PLC-conflict flows untestable headless and is
inconsistent with the same file's own use of ReactiveUI `Interaction` for file pickers (via
`RecipeFileViewModel`).

The fix: route the two VM-driven dialogs through `Interaction`s handled in `MainWindow.WhenActivated`
(the established `OpenFileInteraction`/`SaveFileInteraction` pattern), replace `ExecuteExit`'s
`MainWindow?.Close()` with an interaction, and delete the `Window? MainWindow` property entirely. Both
flows become unit-testable by registering a test handler; the silent `if (MainWindow is null) return;`
drops disappear.

### Scope (deliberately tight) and a roadmap correction

The issue's mechanism section names exactly two dialogs the VM news — the **style editor**
(`ExecuteOpenStyleEditorAsync`) and the **PLC conflict** (`HandleConflictAsync`) — plus the
`Window? MainWindow` property and `ExecuteExit`. Those are this PR.

Two dialogs stay view-side and are **out of scope**, because they are genuine window-lifecycle concerns,
not VM MVVM violations:
- **Exit confirmation** (`MainWindow.axaml.cs:137`, shown from `OnWindowClosing`/`ShowExitChoiceAsync`).
  It hangs off `WindowClosingEventArgs`/`e.Cancel`/`Close()`/`_forceClose` — state the VM cannot own. A
  view showing a dialog in response to its own Closing event is correct MVVM.
- **Restart prompt** (`GridStyleEditorWindow.axaml.cs:40`, from `OnSaveCompleted`). It belongs to the
  style-editor window's own save flow, not `MainWindowViewModel`; any cleanup there is #118's area.

**Correction to the pipe roadmap:** it claimed "#114 deletes F3's interim async-void guards." That is
wrong. F3's guards are on `ShowExitChoiceAsync` (exit) and `OnSaveCompleted` (restart prompt) — both
view-side and both out of #114's scope. They are **not** interim and this PR does not remove them. The
only F3 code near a moved dialog is `HandleConflictAsync`'s try/catch and the fire-and-forget `Guarded`
wrapper on the conflict subscription; both are **kept** (the try/catch now guards an interaction-handler
failure instead of a `ShowDialog` failure, and the subscription still cannot await, so it stays
fire-and-forget-with-Guarded).

## Context (grounded on current master, post F1+F2+F3)

- `MainWindowViewModel.cs:103` — `public Window? MainWindow { get; set; }`, set only at `MainWindow.axaml.cs:35` (`ViewModel.MainWindow = this;`). (`App.axaml.cs:41,60-61` set `desktop.MainWindow`, Avalonia's lifetime property — unrelated, do not touch.)
- `ExecuteOpenStyleEditorAsync` (`:183-195`) — `if (MainWindow is null) return;`, then `new GridStyleEditorWindow { DataContext = viewModel }; await window.ShowDialog(MainWindow);`. The VM builds the `GridStyleEditorViewModel` via `_gridStyleEditorViewModelFactory()` and `await viewModel.LoadAsync()` first.
- `HandleConflictAsync` (`:216-254`) — `if (MainWindow is null) return;`, news `PlcConflictDialogViewModel(local.StepCount, plc.StepCount)` + `PlcConflictDialog(viewModel)`, `await dialog.ShowDialog(MainWindow)` inside a try/catch ("Failed to show PLC conflict dialog"), then reads `dialog.Confirmed`/`dialog.KeepLocal` and runs the F3 `Guarded("PLC conflict resolution failed", …)` resolution tail. Called fire-and-forget from the subscription at `:89-93` via `Guarded("PLC conflict handling", () => _ = HandleConflictAsync(...))`.
- `ExecuteExit` (`:172-176`) — `MainWindow?.Close()`, with a comment that this routes through `OnWindowClosing`'s dirty-close guard.
- `PlcConflictDialog` (`SemiStep.UI/Plc/PlcConflictDialog.axaml.cs`) — `internal` ctor takes `PlcConflictDialogViewModel`; exposes `bool Confirmed` / `bool KeepLocal` set by the two button handlers before `Close()`.
- The `Interaction` pattern to mirror: `RecipeFileViewModel` declares `public Interaction<Unit, string?> OpenFileInteraction { get; }` etc.; `MainWindow.axaml.cs:38-44` registers handlers in `WhenActivated` with `.RegisterHandler(...).DisposeWith(disposables)`; a handler is `async Task Handle(IInteractionContext<TIn,TOut> ctx) { …; ctx.SetOutput(value); }` (`:217-250`).

## Development Approach

- Regular (code, then tests). Warnings are errors; build stays clean after every task.
- Each task must build — so the `Window? MainWindow` property is removed only in the task that migrates
  its LAST remaining usage (Task 2), never before.
- Reuse the file-picker `Interaction` idiom exactly (declaration on the VM, handler in `WhenActivated`).
- Tests register a fake interaction handler and assert the VM behavior headless (`[AvaloniaFact]`).
- `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` after each task.

## Acceptance Evidence

**Automatable (the whole point of the issue — headless testability):**
1. Style editor: a test registers a `ShowStyleEditorInteraction` handler and asserts `OpenStyleEditorCommand` builds the editor VM, calls `LoadAsync`, and invokes the interaction with it — no `Window`. `--filter "FullyQualifiedName~MainWindowViewModel"`.
2. Conflict: a test registers a `ResolveConflictInteraction` handler returning "keep local" / "load from PLC" / cancelled, and asserts `HandleConflictAsync` calls `RecipeCoordinator.ResolveConflict(keepLocal)` (which delegates through to `PlcLifecycleManager.ResolveConflict`) for the first two and does nothing on cancel; a handler that throws is contained + reported ("Failed to show PLC conflict dialog").
3. Exit: `ExitCommand` invokes the exit/close interaction (assert the handler is called).
4. The `Window? MainWindow` property no longer exists (compile-time — grep confirms removal).

**Manual smoke:** open the style editor from the toolbar (dialog shows, edits apply); trigger a PLC recipe conflict and resolve both ways; File→Exit closes with the dirty guard intact.

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Solution Overview

- Add three `Interaction`s as properties on `MainWindowViewModel`, constructed in the ctor
  (`ShowStyleEditorInteraction`/`RequestCloseInteraction` public; `ResolveConflictInteraction` `internal`):
  - `Interaction<GridStyleEditorViewModel, Unit> ShowStyleEditorInteraction` — input is the loaded editor VM; output `Unit`.
  - `Interaction<PlcConflictDialogViewModel, bool?> ResolveConflictInteraction` — output `null` = cancelled/not-confirmed, `true` = keep local, `false` = load from PLC. This property is `internal`, not public, because CS0053 forbids exposing the `internal sealed PlcConflictDialogViewModel` through a public member. A one-line doc comment on the property spells out the three states; no wrapper record — a binary dialog does not need one (same YAGNI call as no `IMessageSink`).
  - `Interaction<Unit, Unit> RequestCloseInteraction` — for `ExecuteExit`.
- `MainWindow.WhenActivated` registers a handler for each: style-editor handler news `GridStyleEditorWindow { DataContext = ctx.Input }` and `await ShowDialog(this)`; conflict handler news `PlcConflictDialog(ctx.Input)`, `await ShowDialog(this)`, then `ctx.SetOutput(dialog.Confirmed ? dialog.KeepLocal : null)`; close handler calls `Close()`. Remove `ViewModel.MainWindow = this;`.
- Remove `public Window? MainWindow` and the `if (MainWindow is null) return;` guards; drop `using Avalonia.Controls;` from the VM if now unused.

## Implementation Steps

### Task 1: Migrate the style-editor and conflict dialogs to Interactions

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Create/Modify: `SemiStep/SemiStep.Tests/UI/MainWindow/` tests

- [x] add `ShowStyleEditorInteraction` (`Interaction<GridStyleEditorViewModel, Unit>`, public) and `ResolveConflictInteraction` (`Interaction<PlcConflictDialogViewModel, bool?>`, `internal` — CS0053 forbids a public member exposing the `internal sealed PlcConflictDialogViewModel`) as properties, constructed in the ctor, each with a one-line doc comment (the conflict one spells out null=cancel / true=keep-local / false=load-from-PLC).
- [x] rewrite `ExecuteOpenStyleEditorAsync`: build + `LoadAsync` the editor VM as today, then `await ShowStyleEditorInteraction.Handle(viewModel)` (no `MainWindow`, no `new GridStyleEditorWindow`).
- [x] rewrite `HandleConflictAsync`: `var keepLocal = await ResolveConflictInteraction.Handle(new PlcConflictDialogViewModel(local.StepCount, plc.StepCount))` inside the existing try/catch (keep the "Failed to show PLC conflict dialog" report on handler failure — it now also covers `UnhandledInteractionException` when no handler is registered, converting today's silent drop into a report); on `keepLocal is null` return; else run the existing `Guarded("PLC conflict resolution failed", …)` tail with `keepLocal.Value`. Keep the fire-and-forget `Guarded` subscription wrapper at `:89-93` unchanged.
- [x] in `MainWindow.WhenActivated`, register the two handlers (news the respective window, `ShowDialog(this)`, `SetOutput`), disposing with `disposables`, mirroring the file-picker handlers. Do NOT yet remove `ViewModel.MainWindow = this;` (ExecuteExit still uses it — removed in Task 2).
- [x] tests (register fake handlers; no `Window`):
  - style-editor: the interaction fires with a non-null editor VM. For a happy-path `LoadAsync`-succeeded assertion, use a loadable config via the `CopyShippedConfig("MBE")` pattern (`GridStyleEditorWindowOwnerRoutingTests` does this) — the fixture's default factory points at a non-existent path (`UIFixture.cs:174-179`) so `LoadAsync` would fault; otherwise assert only that the interaction was invoked with a non-null VM.
  - conflict branches: `HandleConflictAsync` is `private` and `RecipeCoordinator` is sealed (not mockable). Provide a seam — make `HandleConflictAsync` `internal` (mirroring `Guarded`/`OnSubscriptionError`) and assert an observable coordinator effect of `ResolveConflict(true/false)`, OR drive a real conflict via `PlcRecipeConflictDetected` and check the recipe outcome. Assert: confirm-keep → `ResolveConflict(true)`, confirm-load → `ResolveConflict(false)`, cancel (`null`) → no-op.
  - a throwing conflict handler is contained + reported; AND no handler registered reports "Failed to show PLC conflict dialog" (the `UnhandledInteractionException` path) rather than silently vanishing.
- [x] `dotnet build SemiStep.slnx` (0 warnings) + `--filter "FullyQualifiedName~MainWindowViewModel"` green — before next task.

### Task 2: Route ExecuteExit through an interaction and delete the `MainWindow` property

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelReportingTests.cs` (namespace `SemiStep.Tests.UI` — NOT the same-named file under `UI/MainWindow/`). Line 56 does `viewModel.MainWindow = new Window();` inside `OpenStyleEditor_WhenFactoryThrows_...` — a compile break once the property is deleted. Drop that line; the test still validates (the factory throws before any interaction, reported via `OpenStyleEditorCommand.ReportThrownExceptions("Style editor failed")`).

- [x] add `RequestCloseInteraction` (`Interaction<Unit, Unit>`); change `ExitCommand` from `ReactiveCommand.Create(ExecuteExit)` to `ReactiveCommand.CreateFromTask(async () => await RequestCloseInteraction.Handle(Unit.Default))`, and add `ExitCommand.ReportThrownExceptions(MessagePanel, _logger, "Exit failed").DisposeWith(_disposables)`. This routes exit through the command's own error channel (a handler failure or missing handler reports with context, no fire-and-forget). Do NOT use `_ = Handle(...).Subscribe()` — it reintroduces the unobserved-fault path this series eliminates. Delete the old `ExecuteExit` method. Keep a comment that closing routes through `OnWindowClosing`'s dirty guard.
- [x] in `MainWindow.WhenActivated`, register the close handler (`ctx => { Close(); ctx.SetOutput(Unit.Default); }`) and REMOVE `ViewModel.MainWindow = this;`.
- [x] delete `public Window? MainWindow { get; set; }` and the two `if (MainWindow is null) return;` guards; remove `using Avalonia.Controls;` from the VM if now unused.
- [x] tests: `ExitCommand` invokes the close interaction; grep-confirm no `MainWindow` property remains on the VM. The existing `MainWindowExitFlowTests.ExitCommand_DirtySession_DoesNotCloseAndShowsConfirmation` (`:270`) and `ExitCommand_CleanSession_ClosesWindow` (`:289`) — which `await ExitCommand.Execute()` against a shown window — MUST stay green **unmodified**; they are the real regression for the `CreateFromTask` + `RequestCloseInteraction` migration and the dirty-close routing (the shown window registers the close handler in `WhenActivated`; `Close()` runs `OnWindowClosing`'s dirty guard). Note: "Exit failed" is now a report context in two disjoint sites — the VM's `ExitCommand.ReportThrownExceptions` (Handle/no-handler throw) and the view's `ShowExitChoiceAsync` catch (dialog-show throw); they never both fire for one failure.
- [x] `dotnet build SemiStep.slnx` (0 warnings) + full UI slice green — before next task.

### Task 3: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md` or the relevant architecture doc (whichever documents dialog/interaction conventions)

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1496 passed, 0 failed, 0 skipped (Performance explicit probes not run by default).
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors
- [x] `dotnet format SemiStep.slnx` — no changes
- [x] document the convention: VM-driven dialogs go through `Interaction`s registered in `WhenActivated` (like the file pickers); the VM does not depend on `Window`. Note that the exit-confirmation and restart-prompt dialogs remain legitimately view-side (window-lifecycle) — so F3's async-void guards on them stay and are not superseded here. Added `Docs/architecture/dialogs-and-interactions.md` (focused new doc, cross-linked from/to `error-reporting.md` and `exit-flow.md`); also corrected the stale `ExecuteExit` reference in `exit-flow.md`.
- [x] mark this plan for archival at delivery (do NOT move it mid-run). (archival deferred to delivery/ship)

## Post-Completion

**Manual verification:** the three smoke scenarios in Acceptance Evidence need a running app.

**Follow-ups (not this PR):** the restart-prompt dialog (`GridStyleEditorWindow.OnSaveCompleted`) could move
to an `Interaction` on `GridStyleEditorViewModel` as part of #118's style-editor cleanup; the exit flow is
a genuine window-lifecycle concern and is expected to stay view-side.

**Executed by exec:**
- branch: dialog-interactions

## Verify it yourself

The win is headless testability; the proof is that the dialog flows now run in tests with no `Window`.

- **The `Window` dependency is gone:** `grep -n "Window? MainWindow\|Avalonia.Controls" SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` returns nothing.
- **Dialogs run headless via Interactions:** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~MainWindowConflictResolution"` and `--filter "FullyQualifiedName~MainWindowStyleEditorInteraction"` — register a fake handler, drive the command, assert the branch. No window.
- **Silent drop → report:** `MainWindowConflictResolutionTests` includes a no-handler test — a conflict with no registered handler now reports "Failed to show PLC conflict dialog" instead of vanishing (the old `if (MainWindow is null) return;`).
- **Exit routing intact:** `--filter "FullyQualifiedName~ExitFlow"` — the two existing `ExitCommand_*` tests pass unmodified (`Close()` still runs `OnWindowClosing`'s dirty guard); the new `ExitCommand_InvokesRequestCloseInteraction` proves the command drives the close interaction.
- **No regression:** full `dotnet test` (1496 pass) + `dotnet build SemiStep.slnx` (0 warnings).
- **Manual smoke:** open the style editor from the toolbar; force a PLC recipe conflict and resolve both ways; File→Exit with unsaved changes shows the confirm dialog.
