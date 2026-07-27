# PR-F1: Report+Log command-exception extension

## Overview

The command-boundary ring of the error pipe (see `20260727-error-reporting-pipe-roadmap.md`). Today
the `ThrownExceptions → ObserveOn(MainThreadScheduler) → panel.ReportError($"…{ex.Message}")` block is
copy-pasted 8× and **logs nothing** — user-visible errors discard the exception type and stack. This
PR extracts one extension that reports **and** logs with the stack, wires every existing site through
it, closes three unguarded `Subscribe` faults and one unsubscribed command that crashes today.

No `IMessageSink` — consumers keep the concrete `MessagePanelViewModel` (roadmap decision). No
behavior change on the happy path; on failure, the same panel message appears and a stack-carrying
log line is added.

## Context (grounded)

- The 8 identical blocks: `ClipboardViewModel.cs:56-69` (3), `RecipeFileViewModel.cs:37-50` (3),
  `MainWindowViewModel.cs:71-79` (2). Each is `command.ThrownExceptions.ObserveOn(RxSchedulers.MainThreadScheduler).Subscribe(ex => _messagePanel.ReportError($"…: {ex.Message}"))`.
- The `ObserveOn` is redundant: `MessagePanelViewModel.PostOnUiThread` (`:211-221`) self-marshals.
- Existing extension home: `ResultReportingExtensions` (`SemiStep.UI/MessageService/ResultReportingExtensions.cs:14`) already extends the concrete `MessagePanelViewModel`.
- `ILogger<T>` → class-named logs: the Serilog template prints `{SourceContext}` (`Program.cs`), and the MEL→Serilog bridge maps `ILogger<T>`'s category to it. `MainWindowViewModel` already injects `ILogger<MainWindowViewModel>` (`:32,47`); `ClipboardViewModel`, `RecipeFileViewModel`, `GridStyleEditorViewModel` do not.
- Three bare `Subscribe` (no `onError`, fatal-path today): `MainWindowViewModel.cs:85` (`PlcStateChanged`), `:89` (`PlcRecipeConflictDetected`), `:92-94` (`Interval` tick).
- Live crash path: `GridStyleEditorViewModel.SaveCommand` (`:56-58`) has no `ThrownExceptions` subscription; `Save()` (`:272`) does file I/O. A throw hits `RxApp.DefaultExceptionHandler` → crash. The editor has its own `ErrorMessage` surface (`:65`).
- DI: all VMs are container-registered (`UiDi.cs:36-44`); adding an `ILogger<T>` ctor param resolves automatically. Direct test construction sites that need the new arg:
  - `ClipboardViewModel`: `ClipboardViewModelCanExecuteTests.cs:37,111`, `UIFixture.cs:151`, `MessagePanelReportingTests.cs:147,185`.
  - `RecipeFileViewModel`: `UIFixture.cs:158`, `MessagePanelReportingTests.cs:50,72,97`, `RecipeFileViewModelCanExecuteTests.cs:26`, `RecipeFileViewModelSaveResultTests.cs:31`.
  - `GridStyleEditorViewModel`: `UIFixture.cs:166`, `GridStyleEditorViewModelTests.cs:252`, `GridStyleEditorWindowOwnerRoutingTests.cs:118`, `GridStyleEditorWindowTests.cs:31,59`.

## Development Approach

- Regular (code, then tests). Warnings are errors — build must stay clean.
- Tests use `NullLogger<T>.Instance` (`Microsoft.Extensions.Logging.Abstractions`) except where a test asserts logging, which uses a capturing `ILogger`.
- Run `dotnet build SemiStep.slnx` and `dotnet test …` after each task; all green before the next.
- `dotnet format SemiStep.slnx` before finishing.

## Acceptance Evidence

Automatable:
1. `ReportThrownExceptions`: a command whose execute throws produces exactly one panel entry AND one `LogError(ex,…)` carrying the exception (capturing logger). `--filter "FullyQualifiedName~ReportThrownExceptions"`.
2. The three `MainWindowViewModel` subscriptions: a throw in each callback reports to the panel instead of escaping. `--filter "FullyQualifiedName~MainWindowViewModel"`.
3. `GridStyleEditorViewModel.SaveCommand`: a throw in `Save()` surfaces on `ErrorMessage` and logs, without crashing. `--filter "FullyQualifiedName~GridStyleEditor"`.
4. Regression: existing `MessagePanelReportingTests` stay green (same panel messages).

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: Add the `ReportThrownExceptions` extension

**Files:**
- Create: `SemiStep/SemiStep.UI/MessageService/ReactiveCommandReportingExtensions.cs`
- Create: `SemiStep/SemiStep.Tests/UI/MessageService/ReactiveCommandReportingExtensionsTests.cs`

- [x] add `static IDisposable ReportThrownExceptions<TParam,TResult>(this ReactiveCommand<TParam,TResult> command, MessagePanelViewModel panel, ILogger logger, string context)` that subscribes `ThrownExceptions` and, per exception, `logger.LogError(ex, "{Context} failed", context)` then `panel.ReportError($"{context}: {ex.Message}")`; no `ObserveOn`
- [x] `System` usings first, then others; tabs; braces on new line
- [x] test: a throwing command yields exactly one `ReportError` on the panel (assert via a real `MessagePanelViewModel`) and one `LogError` with the thrown exception (capturing `ILogger`)
- [x] test: a non-throwing execute produces no panel entry and no log
- [x] run `--filter "FullyQualifiedName~ReportThrownExceptions"` — green before next task

### Task 2: Rewire `ClipboardViewModel`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Clipboard/ClipboardViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelReportingTests.cs`

- [x] add `ILogger<ClipboardViewModel>` ctor param + field
- [x] replace the three `ThrownExceptions` blocks (`:56-69`) with `command.ReportThrownExceptions(_messagePanel, _logger, "Copy failed"/"Cut failed"/"Paste failed").DisposeWith(_disposables)` (keep the existing user-facing text; extension appends `": {ex.Message}"`)
- [x] update the 5 direct construction sites to pass `NullLogger<ClipboardViewModel>.Instance`
- [x] confirm existing clipboard reporting tests still pass (same messages)
- [x] run clipboard-related filters — green before next task

### Task 3: Rewire `RecipeFileViewModel`

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelReportingTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelSaveResultTests.cs`

- [x] add `ILogger<RecipeFileViewModel>` ctor param + field
- [x] replace the three `ThrownExceptions` blocks (`:37-50`) with the extension ("Save failed"/"Save As failed"/"Load failed")
- [x] update the 5 direct construction sites to pass `NullLogger<RecipeFileViewModel>.Instance`
- [x] run recipe-file filters — green before next task

### Task 4: Rewire `MainWindowViewModel` + guard the three bare subscriptions

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Create/Modify: `SemiStep/SemiStep.Tests/UI/MainWindow/MainWindowViewModelReportingTests.cs`

- [x] replace the two `ThrownExceptions` blocks (`:71-79`) with the extension ("Sync toggle failed"/"Style editor failed"); `_logger` already exists
- [x] give `PlcStateChanged` (`:85`), `PlcRecipeConflictDetected` (`:89`), and the `Interval` tick (`:94`) an `onError` that `_logger.LogError(ex,…)` + `MessagePanel.ReportError(…)` (do not let a callback throw escape to the fatal path)
- [x] test: a forced throw in each of the three callbacks reports to the panel and does not crash the headless app
- [x] run `--filter "FullyQualifiedName~MainWindowViewModel"` — green before next task

### Task 5: Wire `GridStyleEditorViewModel.SaveCommand` to its own surface + log

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorWindowOwnerRoutingTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorWindowTests.cs`

- [x] add `ILogger<GridStyleEditorViewModel>` to both constructors (thread it through the delegating `:42` ctor)
- [x] subscribe `SaveCommand.ThrownExceptions` → `_logger.LogError(ex, "Style editor save failed")` + set `ErrorMessage` to a message (the editor's own surface, not the shared panel); dispose with the VM
- [x] update the 4+ direct construction sites to pass `NullLogger<GridStyleEditorViewModel>.Instance`
- [x] test: a forced throw in `Save()` sets `ErrorMessage`, logs, and does not crash
- [x] run `--filter "FullyQualifiedName~GridStyleEditor"` — green before next task

### Task 6: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md`

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (1472 passed, 0 failed)
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors
- [x] `dotnet format SemiStep.slnx` (no changes)
- [x] update `error-reporting.md`: the exception-handler line now reads "route through `ReportThrownExceptions` (reports + logs with stack)"; note the concrete-panel decision
- [ ] ⚠️ move this plan to `docs/plans/completed/` (deferred to delivery — plan stays until branch is shipped; not performed)

## Post-Completion

- Open the PR: "feat(ui): route command exceptions through report+log extension". Note in the body that user-visible errors now carry a class-named stack trace to the log; no happy-path behavior change.
- Follow-on: PR-F2 (global backstop), then #113 rebased on both.

**Executed by exec:**
- branch: report-log-extension

## Verify it yourself

The change is invisible on the happy path; it only shows up when a command or subscription faults. Prove it with the tests that reproduce each fault.

- **Command exception reports + logs (the core outcome):**
  `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ReportThrownExceptions"` — a throwing `ReactiveCommand` produces exactly one panel entry AND one `LogError` carrying the same exception instance. Before this branch the log entry did not exist (handlers only reported).
- **Style-editor save crash is closed (was a live crash):**
  `dotnet test ... --filter "FullyQualifiedName~GridStyleEditor"` — `SaveCommand_WhenSaveThrows_...` drives `SaveCommand.Execute()` through a throwing facade and asserts `ErrorMessage` is set + logged, no crash. On `master`, `SaveCommand` has no `ThrownExceptions` subscription, so that throw hits `RxApp.DefaultExceptionHandler` and kills the process.
- **Subscription-callback throws are contained (was fatal for the Interval tick):**
  `dotnet test ... --filter "FullyQualifiedName~MainWindowViewModel"` — `Guarded_ThrowInOnNextBody_...` forces a throw inside the guarded onNext and asserts it reports without escaping.
- **No happy-path regression / no doubled log:** full suite `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (1474 pass) and `dotnet build SemiStep.slnx` (0 warnings). Existing reporting tests still assert the same user-facing panel messages ("Copy failed:", "Load failed:", …).
- **Manual (optional):** run the app, trigger a command failure (e.g. paste malformed clipboard content), confirm the panel message is unchanged AND the log file now carries the exception with `SourceContext` = the originating class.
