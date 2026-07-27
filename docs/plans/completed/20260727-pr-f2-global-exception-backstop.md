# PR-F2: Global exception backstop

## Overview

The last-resort ring of the error pipe (see `20260727-error-reporting-pipe-roadmap.md`). Today
nothing sits under the running dispatcher: `Program.Main` has a top-level try/catch, but it only
wraps startup — once `App.Run` → `StartWithClassicDesktopLifetime` is pumping, an unsubscribed
`ThrownExceptions`, an unobserved fire-and-forget task fault, or a background-thread throw has no
handler. The first crashes or vanishes silently; the last dies with no stack in the log.

This PR adds one `GlobalExceptionBackstop.Install(IReactiveUIBuilder, IServiceProvider)` wiring three
handlers, installed at `UseReactiveUI` build time **before any command is constructed**. It does not
change the happy path and does not replace the per-command ring (F1) — it catches only what escapes
every inner ring.

## Context (grounded)

- `App.Run` (`SemiStep/SemiStep.UI/App.axaml.cs:77-92`): `BuildAvaloniaApp().AfterSetup(InitializeServices).AfterSetup(set _serviceProvider).StartWithClassicDesktopLifetime`. `InitializeServices` (`:94-108`) is the FIRST `AfterSetup` and resolves `RecipeCoordinator` → which constructs `MessagePanelViewModel` → whose ctor builds `ToggleCommand` (`MessagePanelViewModel.cs:42`). So the first command is built during that first `AfterSetup`.
- The pipeline exception handler is captured by ReactiveUI at `ReactiveCommand` construction (a `ScheduledSubject` default observer), not read at throw time. **It must be set before any command is built.** In ReactiveUI 23.2.28 there is no settable `RxApp.DefaultExceptionHandler`; the handler is supplied through `IReactiveUIBuilder.WithExceptionHandler(...)` (namespace `ReactiveUI.Builder`), applied once at `UseReactiveUI` build time via `RxState.InitializeExceptionHandler`, which runs before the first command is built.
- Serilog is the log sink; static `Log.Fatal`/`Log.Warning` are already used at the composition root (`Program.cs`, `App.axaml.cs:102`). `Log.CloseAndFlushAsync()` is the flush (`Program.cs` finally). DI exposes `ILogger<T>` via `AddLogging(AddSerilog)` (`Program.cs`).
- `MessagePanelViewModel.ReportError` (`:171-174`) self-marshals to the UI thread (`PostOnUiThread`, `:211-221`). Resolving the singleton the first time constructs it — but `InitializeServices` already constructs it during startup, after the backstop is installed, so a lazy resolve at throw time returns the built instance.
- No generic user-facing error string exists in `Localization/Resources.resx`. Use a plain-English message for now (consistent with F1's raw-message error branches); #115 localizes later.
- The extension/panel decision from F1: consumers use the concrete `MessagePanelViewModel` (no `IMessageSink`). The backstop follows suit — it resolves the concrete panel from the provider.

## Development Approach

- Regular (code, then tests). Warnings are errors; build must stay clean.
- The three handlers' report/log LOGIC is extracted into small testable methods asserted directly. The recoverable observer factory (`CreateRecoverableExceptionHandler(provider)`) is verified by one integration test that resolves a real `MessagePanelViewModel` from a minimal provider, pushes `handler.OnNext(...)`, and asserts the panel report; the build-time ordering and the `AppDomain`/`TaskScheduler` OS hooks are covered by code review plus the manual smoke checklist (they cannot be fired deterministically in-process).
- `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` after each task.

## Acceptance Evidence

**Automatable:**
1. `GlobalExceptionBackstopTests` — the recoverable handler logic logs the exception (Error) AND reports a generic message to a real `MessagePanelViewModel`; the unobserved handler logs (Error) only; the fatal handler logs (Critical). Capturing `ILogger` (`RecordingLogger<T>`). `--filter "FullyQualifiedName~GlobalExceptionBackstop"`.
2. Recoverable-handler integration test — build a minimal provider exposing a real `MessagePanelViewModel` + `AddLogging`, obtain the observer via `CreateRecoverableExceptionHandler(provider)`, push `handler.OnNext(new Exception("boom"))`, assert the panel reported the generic message. No global state is mutated (the ReactiveUI 23 handler is un-overridable at runtime), so no save/restore and no `[Collection]` are needed. This exercises the observer factory + DI resolution + panel wiring, NOT the real pipeline routing or build-time ordering (those are manual-smoke).

**Manual smoke** (OS hooks / startup order — not automatable in-process):
1. Trigger an unsubscribed `ThrownExceptions` (temporarily a command with no handler) → app stays alive, panel shows the generic message, log has one Error with the stack; in a DEBUG build with a debugger attached, execution breaks.
2. Fire a fire-and-forget task fault → log has one Error, no crash.
3. Throw on a background thread → `Log.Fatal` with the stack is written (flushed) before the process exits.
4. Confirm `Install` runs before `InitializeServices` (breakpoint/log order, or a temporary ordering assertion).

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Solution Overview

- `GlobalExceptionBackstop` (`SemiStep.UI/Logging/`) exposes `static Install(IReactiveUIBuilder builder, IServiceProvider provider)` and small `internal static` handler-logic methods. `Install` registers the recoverable handler on the builder and subscribes the two OS hooks; each hook closure **lazily** resolves the panel from the provider at fire time (never eagerly inside `Install`, which would construct `MessagePanelViewModel`/`ToggleCommand` under the pre-backstop handler), then calls the pure logic method plus its side effect.
- Handler map:
  - `IReactiveUIBuilder.WithExceptionHandler(...)` → `Observer.Create<Exception>(ex => { ReportRecoverable(panel, logger, ex); <#if DEBUG> Debugger.Break(); })`. Keeps the app alive. Captured once at `UseReactiveUI` build time.
  - `TaskScheduler.UnobservedTaskException` → `LogUnobserved(logger, e.Exception); e.SetObserved();`. Log-only.
  - `AppDomain.CurrentDomain.UnhandledException` → `LogFatal(logger, ex); Log.CloseAndFlushAsync().GetAwaiter().GetResult();`. Process still dies, but with a flushed stack.
- `Install` is called from the `UseReactiveUI` build callback inside `BuildAvaloniaApp` in `App.Run`. That callback runs at build time, before the `InitializeServices` `AfterSetup` builds the first command, so the recoverable handler wins the capture.

## Technical Details

- `ReportRecoverable(MessagePanelViewModel panel, ILogger logger, Exception ex)`: `logger.LogError(ex, "Unhandled exception reached the global backstop")` then `panel.ReportError(<generic English message, e.g. "An unexpected error occurred; see the log for details.">)`. `Debugger.Break()` lives in the `Install` closure under `#if DEBUG`, NOT in this method, so the method stays cleanly testable.
- `LogUnobserved(ILogger logger, Exception ex)`: `logger.LogError(ex, "Unobserved task exception reached the global backstop")`.
- `LogFatal(ILogger logger, Exception ex)`: `logger.LogCritical(ex, "Unhandled exception is terminating the process")` (MEL `Critical` → Serilog `Fatal`).
- `Install(IReactiveUIBuilder builder, IServiceProvider provider)` resolves the logger once from the provider (logger construction has no side effects) and captures it; the panel is resolved lazily inside each closure via `provider.GetRequiredService<MessagePanelViewModel>()`. The recoverable handler is registered via `builder.WithExceptionHandler(...)`; the two OS hooks are subscribed on `TaskScheduler`/`AppDomain`.

## What Goes Where

- **Implementation Steps** (checkboxes): the backstop class, tests, and the `App.Run` wiring.
- **Post-Completion**: the manual smoke checklist and the PR note that this changes the "unsubscribed `ThrownExceptions` crashes loudly in dev" behavior to report-loudly + `Debugger.Break`.

## Implementation Steps

### Task 1: Add `GlobalExceptionBackstop` with testable handler logic

**Files:**
- Create: `SemiStep/SemiStep.UI/Logging/GlobalExceptionBackstop.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Logging/GlobalExceptionBackstopTests.cs`

- [x] create `GlobalExceptionBackstop` with `internal static ReportRecoverable(MessagePanelViewModel, ILogger, Exception)`, `internal static LogUnobserved(ILogger, Exception)`, `internal static LogFatal(ILogger, Exception)` per Technical Details (report+log / log-only / log-critical); no `Debugger.Break` inside these
- [x] write tests: `ReportRecoverable` logs Error with the exception AND reports the generic message to a real `MessagePanelViewModel`; `LogUnobserved` logs Error only; `LogFatal` logs Critical. Use the shared `RecordingLogger<T>` (`SemiStep.Tests/Helpers/RecordingLogger.cs`)
- [x] run `--filter "FullyQualifiedName~GlobalExceptionBackstop"` — green before next task

### Task 2: `Install` the three hooks, lazily and in the right order

**Files:**
- Modify: `SemiStep/SemiStep.UI/Logging/GlobalExceptionBackstop.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Logging/GlobalExceptionBackstopInstallTests.cs`

- [x] add `public static void Install(IReactiveUIBuilder builder, IServiceProvider provider)`: resolve the logger once; register the recoverable observer via `builder.WithExceptionHandler(...)` (an `Observer.Create<Exception>` closure that lazily resolves the panel, calls `ReportRecoverable`, and `#if DEBUG Debugger.Break();`); subscribe `TaskScheduler.UnobservedTaskException` → `LogUnobserved` + `SetObserved()`; subscribe `AppDomain.CurrentDomain.UnhandledException` → `LogFatal` + `CloseAndFlushAsync().GetAwaiter().GetResult()`
- [x] idempotency: no guard needed — `Install` is provably single-call (App.Run is gated by `EnsureSingleStart`; `RunErrorWindow` uses the no-arg `BuildAvaloniaApp` and never installs), so the OS events cannot be double-subscribed. The builder handler is also one-shot in `RxState`.
- [x] write an integration test: build a minimal provider exposing a real `MessagePanelViewModel` + `AddLogging`, obtain the observer via `CreateRecoverableExceptionHandler(provider)`, push `handler.OnNext(new Exception("boom"))`, assert the panel reported the generic message
- [x] run `--filter "FullyQualifiedName~GlobalExceptionBackstop"` — green before next task

➕ **API deviation (ReactiveUI 23.2.28 / ReactiveUI.Avalonia 12.0.3):** `RxApp` is gone. `RxState.DefaultExceptionHandler` is get-only and one-shot; the pipeline handler is set only through `IReactiveUIBuilder.WithExceptionHandler(...)` (namespace `ReactiveUI.Builder`), applied once at `UseReactiveUI` build time via `RxState.InitializeExceptionHandler`. Consequences: (1) `Install` now takes the builder — signature `Install(IReactiveUIBuilder builder, IServiceProvider provider)`. (2) The recoverable observer was extracted into `internal static IObserver<Exception> CreateRecoverableExceptionHandler(IServiceProvider)` so it is testable without the un-overridable global; the integration test exercises the observer directly (no `RxState` save/restore, so no `[Collection]` needed). (3) **Task 3 changes:** wire via `.UseReactiveUI(builder => GlobalExceptionBackstop.Install(builder, serviceProvider))` in `App.Run`, NOT a new `AfterSetup`. `WithExceptionHandler` runs before the first `ReactiveCommand`, so the "set before command construction" ordering constraint still holds. (4) **`_installed` flag dropped in review (commit bc37741):** the originally planned static `_installed` guard was removed as YAGNI. `Install` is provably single-call (App.Run gated by `EnsureSingleStart`; `RunErrorWindow` never installs), so the OS events cannot be double-subscribed, and the "avoid mutable static state" rule favors no flag.

### Task 3: Wire `Install` into `App.Run` at `UseReactiveUI` build time

**Files:**
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`

- [x] give `BuildAvaloniaApp` an optional `Action<IReactiveUIBuilder>? configureReactiveUi = null` parameter passed into `.UseReactiveUI(configureReactiveUi ?? (_ => { }))`; `Run` passes `builder => GlobalExceptionBackstop.Install(builder, serviceProvider)` so `WithExceptionHandler` is captured at build time, before the `InitializeServices` `AfterSetup` builds the first command. Add a comment stating that ordering constraint. (NOT a new `AfterSetup` — the `RxApp.DefaultExceptionHandler` property this version needs does not exist.)
- [x] keep `RunErrorWindow` backstop-free: it calls `BuildAvaloniaApp()` with no argument (default no-op configurator), so it installs no handler and has no service provider to resolve one from
- [x] confirm `serviceProvider` closure is available (it is the `Run` parameter; the backstop uses it directly, not `app._serviceProvider`)
- [x] `dotnet build SemiStep.slnx` — 0 warnings; run the full UI test slice to confirm no startup regression
- [x] (no new unit test here — startup wiring is covered by Task 2's integration test + the manual smoke checklist)

### Task 4: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md`

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1478 passed, 0 failed, 0 skipped (Performance probes are `Explicit`, not run)
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors
- [x] `dotnet format SemiStep.slnx` — no changes
- [x] update `error-reporting.md`: add the global-backstop as the last-resort ring — the three handlers, the "set before command construction" constraint, the lazy panel resolve, and the deliberate dev-behavior trade (report-loudly + `Debugger.Break` instead of crash)
- [x] walk the manual smoke checklist in Acceptance Evidence (OS hooks / startup order need a running app; covered by the manual smoke checklist, not automatable in-process)
- [x] mark this plan for archival at delivery (do NOT move it mid-run) — archival deferred to delivery/ship

## Post-Completion

**Manual verification:** the four smoke scenarios in Acceptance Evidence (OS hooks + startup order) need a running app; they are not automatable in-process.

**PR note:** this converts "an unsubscribed `ThrownExceptions` crashes loudly in dev" into report-loudly + `Debugger.Break` (DEBUG). For an operator-facing PLC tool, a generic panel message in production beats a crash; the dev signal survives via the debugger break. The `AppDomain` handler does not prevent process death — it guarantees a flushed stack in the log, which is the whole point for background-thread throws today invisible to `Program.cs`.

**Executed by exec:**
- branch: global-exception-backstop

## Verify it yourself

The backstop only acts when something escapes every inner ring, so the automated proof is the handler logic; the real OS-hook routing is manual smoke (a running app).

- **Handler logic (report/log policy):** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GlobalExceptionBackstop"` — the recoverable handler logs the exception AND reports the generic message to a real panel; the unobserved handler logs Error; the fatal handler logs Critical. The install test drives the recoverable observer and asserts the panel report.
- **No regression / build:** full `dotnet test` (1478 pass) and `dotnet build SemiStep.slnx` (0 warnings).
- **Manual smoke (needs a running app — not automatable in-process):**
  1. Force an unsubscribed `ThrownExceptions` (temporarily a command with no handler) → app stays alive, panel shows the generic message, log has one Error with the stack; in a DEBUG build with a debugger attached, execution breaks.
  2. Fire a fire-and-forget task fault → log has one Error, no crash.
  3. Throw on a background thread → `LogCritical` (Serilog Fatal) with the stack is flushed to the log before the process exits.
  4. Confirm `Install` runs before `InitializeServices` (it wires at `UseReactiveUI` build time, which precedes the first `AfterSetup`).
