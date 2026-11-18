# Error / Logging Pipe Rework + Open-Issue Roadmap

## Overview

The app has no single, enforced path for how a failure reaches the user and the log. Reporting
is an opt-in ritual repeated at each site (subscribe `ThrownExceptions`; check `IsFailed`;
try/catch the async void), and nothing fails the build or startup when a site skips it. The
result is a cluster of defects — crashes on forgotten subscriptions, user-visible errors whose
exception type and stack never reach the log, and dialogs shown from code that cannot route their
own exceptions.

This document defines the target "general pipe" (how every failure is handled and logged exactly
once), the minimal foundation that realizes it, and a dependency-ordered roadmap that sequences
the whole open-issue board on top. Issue #113 stops being a special case and becomes one rebased
step in the sequence.

Scope note: this is a roadmap, not a single implementation plan. Each PR below gets its own focused
plan/branch when it is picked up. The foundation is ~120 lines across two small PRs, then #113.

## The target pipe

A failure is born onto one of two disjoint tracks and never crosses over: an expected outcome is a
`Result`; an unexpected fault is an exception. They are not stacked layers a failure "passes
through" — a `Result` is never on its way to becoming a caught exception. The architecture is three
failure classes and where each is caught. Everything else is plumbing that serves them.

### Failure classes — the policy (this is the whole architecture)

| Class | Born as | User sees | Log | App |
|------|---------|-----------|-----|-----|
| Expected, recoverable | `Result` + typed `IError` (text owned by Core) | full message via the panel (`ReportFailure`) | at origin; `CausedBy(ex)` when an exception was the cause (#120) | lives |
| Unexpected, recoverable | exception escaping a command/dialog | context-prefixed generic message | `LogError(ex, …)` with stack at the boundary | lives |
| Unexpected, fatal | exception outside any boundary | nothing (process dies) | `Log.Fatal` + flush (`Program.cs:50-57`; `AppDomain` for background threads) | dies |

Core still owns the error text (`Docs/architecture/error-reporting.md:20-25`); the UI only routes.

### Rings — only the exception track has them

The exception track is caught at two rings. **The `Result` track has none** — a dropped `Result`
vanishes with no handler, no log, no crash. That is #113 defect 2 (`RecipeCommandsViewModel.cs:77,95`
acting only on `IsSuccess`). Standing review rule: **every `Result` must be consumed**, because
nothing catches a dropped one.

- **Command boundary** — `ThrownExceptions` on every command → the `ReportThrownExceptions` extension
  (report + log with stack). One enforcement point for the "unexpected, recoverable" class.
- **Global backstop** — installed once at startup via `GlobalExceptionBackstop.Install(...)` from
  `App.Run`'s `AfterSetup` (`App.axaml.cs:81-91`, after `UseReactiveUI`). Three registration points,
  one ring:

  | Handler | Catches | Action |
  |---------|---------|--------|
  | `RxApp.DefaultExceptionHandler` | unsubscribed `ThrownExceptions` and other ReactiveUI-routed faults (OAPH, `canExecute` pipelines) — **not** plain `Observable.Subscribe` callbacks | `Log.Error(ex)` + generic `MessagePanelViewModel.ReportError` + `Debugger.Break()` in DEBUG; keeps the app alive |
  | `TaskScheduler.UnobservedTaskException` | forgotten fire-and-forget faults at GC time | `Log.Error(ex)` + `SetObserved()`; log-only |
  | `AppDomain.CurrentDomain.UnhandledException` | background-thread throws (invisible to `Program.cs:50`) | `Log.Fatal(ex)` + flush; process still dies, but with a stack |

  **Timing constraint:** `RxApp.DefaultExceptionHandler` is captured at `ReactiveCommand`
  *construction* (the `ScheduledSubject` default observer), not read at throw time, so it must be set
  **before any command is built**. Commands are already built during startup init — `InitializeServices`
  (first `AfterSetup`, `App.axaml.cs:82-85`) resolves `RecipeCoordinator` → `MessagePanelViewModel`,
  whose ctor builds `ToggleCommand` (`MessagePanelViewModel.cs:42`). So the backstop's handler-set
  must run in an `AfterSetup` ordered **before** `InitializeServices` (or at the top of `Run` before
  the chain).

  No Avalonia dispatcher hook — raw UI-thread throws already unwind into `Program.cs:50-53`'s
  `Log.Fatal` + flush. This holds only while Avalonia's `Dispatcher.UnhandledException` keeps its
  default rethrow behavior (true in Avalonia 12.0.3); state the assumption so a future upgrade that
  changes the default does not silently open a hole. Deliberate trade: this converts "unsubscribed
  `ThrownExceptions` crashes loudly in dev" into report-loudly + `Debugger.Break` in debug. For an
  operator-facing PLC tool a generic panel message in production beats a crash; the dev signal
  survives via the debugger break.

**Not covered by any ring:** a throw inside a plain `Observable.Subscribe` callback with no `onError`
unwinds to the fatal path, not the recoverable ring. `MainWindowViewModel` has three today —
`PlcStateChanged` (`:85`), `PlcRecipeConflictDetected` (`:89`), the `Interval` tick (`:94`). Second
standing review rule: **every non-command `Subscribe` must pass an `onError`** (report via the panel).
F1 fixes these three; new ones are a review item like dropped `Result`s.

### Supporting mechanisms — plumbing, NOT layers

These serve the classes above; none handles a failure on its own.

| Mechanism | Kind | Serves |
|-----------|------|--------|
| `ReportThrownExceptions(panel, logger, context)` | command-boundary impl | the "unexpected, recoverable" ring. Replaces the 4-line block pasted 8× (`ClipboardViewModel.cs:56-69`, `RecipeFileViewModel.cs:37-50`, `MainWindowViewModel.cs:71-79`). Sits next to `ResultReportingExtensions` (`:14`), which already extends the concrete `MessagePanelViewModel`. Takes the caller's `ILogger<TVm>` so the log carries the class via `{SourceContext}`; drops the redundant `ObserveOn` (the panel self-marshals via `PostOnUiThread`, `MessagePanelViewModel.cs:211-221`). `context` is a plain string until #115, a resx lookup after. |
| `ExecuteIfPossible` | boundary preserver | keeps imperative hotkey invocations (`MainWindow.axaml.cs:61-90`) inside the command boundary instead of bypassing it; also fixes the `canEdit` gating bypass. Two lines via the `ICommand` cast (`CanExecute(null)` then `Execute(null)`, internally `Execute().Catch(Empty).Subscribe()`). |
| `Interaction<TIn,TOut>` | boundary preserver | keeps dialog code inside command execution (proven at `RecipeFileViewModel.cs:27-28`) so the command ring applies; retires the async-void / fire-and-forget escapes (`MainWindowViewModel.cs:89`, `GridStyleEditorWindow.axaml.cs:31`). This is #114. |
| async-void `try/catch` guards | interim boundary patch | the command ring for the two bypassing dialogs until #114 moves them onto `Interaction`; superseded then. |

No `IMessageSink` interface. `MessagePanelViewModel` has one implementation, is not mocked (tests
assert on the real `UIFixture.MessagePanel`), and never crosses a project boundary — every consumer
lives in `SemiStep.UI`. It scores zero of four on the repo's interface rule, and there are no
plans for a second reporting surface. The extension and backstop depend on the concrete panel. If a
second sink ever materializes, extracting the interface then is a one-hour mechanical refactor.

The code that gives the command-boundary ring its shape:

```csharp
public static IDisposable ReportThrownExceptions<TParam, TResult>(
	this ReactiveCommand<TParam, TResult> command,
	MessagePanelViewModel panel, ILogger logger, string context)
	=> command.ThrownExceptions.Subscribe(ex =>
	{
		logger.LogError(ex, "{Context} failed", context);
		panel.ReportError($"{context}: {ex.Message}");
	});
```

`ExecuteIfPossible` and `Interaction` are the same kind of device — both funnel code back inside the
one command boundary so a single policy covers it. Keep `OnKeyDown` in code-behind: the `IsEditing`
gate (`MainWindow.axaml.cs:67`) is a real behavioral requirement, not a candidate for XAML KeyBindings.

### YAGNI — do not build

`IMessageSink` (above); base ViewModel class; command factory wrapping `Create`/`CreateFromTask`
(signatures vary too much across the ~17 commands); `IDialogService.Show<TVm>` (three dialogs;
per-dialog Interactions are the house style); error mediator/event bus; custom
`SynchronizationContext`; crash-report dialog; any `IErrorPipeline` abstraction.

## Foundation PRs (dependency order)

- **PR-F1** — add `ReportThrownExceptions(panel, logger, context)`; replace all 8 existing blocks;
  give the three bare `MainWindowViewModel` subscriptions (`:85,:89,:94`) an `onError`; reporting VMs
  gain `ILogger<TVm>`. Wire the currently-unsubscribed `GridStyleEditorViewModel.SaveCommand`
  (`:56-58`, a live crash path today) to the **editor's own `ErrorMessage` surface** (`:65`), not the
  shared panel — a modal's fault must not land behind the modal — **and** log `ex` with its stack
  (inject `ILogger<GridStyleEditorViewModel>`), since routing to the surface alone would still drop
  the stack the pipe exists to preserve. Depends: none. *(File new issue: "command error reporting
  drops exception stack; extract report+log extension.")*
- **PR-F2** — `GlobalExceptionBackstop` wiring the three handlers, its handler-set ordered before
  startup command construction (see Timing constraint). The handler closures resolve
  `MessagePanelViewModel` **lazily at throw time** via the provider — never eagerly inside `Install`,
  which would construct the panel (and its `ToggleCommand`) under the pre-backstop handler; the
  singleton makes the lazy resolve return the same instance. Register the set in an `AfterSetup`
  before `InitializeServices` (not at the top of `Run`, which precedes `UseReactiveUI`). Depends:
  none. *(File new issue: "no global exception backstop.")*
- **PR-F3 = #113**, rebased on F1–F2 — `ExecuteIfPossible` + hotkey rewiring; `RecipeCommandsViewModel`
  wired via the F1 extension (its planned private helper disappears); async-void try/catch guards;
  `ContinueWith` backstop; tests. Depends: F1 (mechanical), F2 (recommended, updates the "crashes
  loudly in dev" rationale).

### Config-side fix that lands with F3 (#113 Task 7 vs #111)

The #113 plan adds a constructor throw to `RecipeMetadataRegistry` for zero primary actions, but
#111 wants config defects routed through `Result`, not constructor throws. Resolution: validate at
the **resolved-primary aggregate**, not the constructor and not the per-file loader.

- Do **not** turn `ActionsSectionLoader.cs:69-73` into an error — that is the *per-file* empty check;
  a single empty YAML among several action files would abort a legitimately non-empty load
  (`Result.Merge`, `:44-47`).
- The invariant is zero *resolved primary* actions: `ResolvePrimaryActions` (`RecipeMetadataRegistry.cs:31,76`)
  consumes subactions, so a config where every action is a subaction has non-empty files, passes the
  loader, yet yields zero primaries and still crashes `GetDefaultActionId` (`RecipeCoordinator.cs:171`).
- Fix: surface empty-primaries as an `IError` from the **resolve stage** (`ResolvePrimaryActions`,
  after subactions are consumed) so it flows into the existing startup error window (`App.axaml.cs:40`).
  A raw-aggregate `allActions.Count == 0` check (`ActionsSectionLoader.cs:50-55`) would **not** cover
  the all-subactions case — that list still holds the subactions pre-resolution — so the check must be
  post-resolve. Same fail-fast, no constructor throw for #111 to unwind. Update #113 Task 7 accordingly
  when it becomes F3.

## Full board roadmap

Four tracks. Track D runs parallel to the pipe.

**Track A — independent production bug, start immediately:**
1. **#112** ServiceProvider disposal (PLC teardown unreachable). Touches `Program.cs`/`App.axaml.cs`;
   land before/alongside F2 to avoid merge friction in the same files.

**Track B — the pipe:**
2. **PR-F1** (report+log extension) and **PR-F2** (backstop) — independent, can run in parallel.
3. **PR-F3 = #113** (depends F1; F2 recommended).
4. **#114** dialogs via Interaction (depends F3). Explicitly *supersedes* two of F3's guards — the
   conflict `ContinueWith` backstop and the `OnSaveCompleted` try/catch become dead weight once those
   dialogs are awaited Interactions. Guards-then-remove is the right order; note it in #114's PR.
5. **#115** localization of operational messages (depends F1 — the context strings now flow through
   one extension). Own PR.
6. **#118** style editor: sync I/O off the UI thread + field explosion. The RestartPrompt part
   depends on #114's pattern; the I/O part depends on nothing.

**Track C — Core/coordinator reshaping (independent of the pipe):**
7. **#117** — split PLC-sync out of `RecipeCoordinator` (557 lines). Its "depend on a message sink"
   clause is **won't-do** (see the no-`IMessageSink` note; close/edit that half of the issue citing
   the interface rule); only the coordinator split remains.
8. **#116** RecipeSession split.
9. **#119** config loader dedup, then **#111** registry Result-routing + combo provisioning to UI
   (same area; #119's skeleton first makes #111 cheaper; F3 already moved the zero-actions check
   config-side).
10. **#110** invert Recipes↔Plc cycle — last in this track; easier after #116 and #109 reshape both ends.

**Track D — PLC concurrency (parallel to Track B):**
11. **#109** sync state-machine consolidation, then **#108** lifecycle races (same files; #109
    shrinks the surface #108 must lock).
12. **#120** CancellationToken threading + `CausedBy(ex)` — after #108/#109 (it changes `IS7Reader`
    signatures the concurrency refactors would otherwise churn twice). Its `CausedBy` half is the
    expected-failure stack-preservation piece arriving late, which is fine.

## Core-error localization (typed errors + UI resolver)

#115 localizes UI-origin strings only (command-failure contexts, file-picker titles, backstop
message) via `LocalizedText`. It deliberately leaves Core-produced `Result`/`IError` text English —
which, once a Russian operator hits an unhappy path (undo with nothing to undo, PLC not connected,
paste parse failure), is a mixed-language status bar. This section is the follow-on that closes that
gap without a `string Code` discriminator (rejected as a stringly-typed smell) and without localizing
config-load errors (they surface in the pre-locale `ErrorWindow`; English by design, permanently).

**Pattern — typed errors, routed on type at the UI boundary, resolved via the same resx:**

- **Core** owns one `sealed` `Error` subclass per distinct operator-facing message (per message, not
  per call site — shared texts collapse to one type), carrying structured data as properties and an
  English `.Message`. C# 14 primary constructors make a no-arg one a single line. No enum/`Code`
  discriminator — the type *is* the discriminator.
- **UI** has one static `ReasonLocalizer.Localize(IReason)` (in `UI/Localization`) that `switch`es on
  the reason type → a compile-safe `Resources.X` accessor, `string.Format`s with the typed properties,
  recurses over `reason.Reasons.OfType<IError>()` for `CausedBy` trees, and falls back to English
  `.Message` for unmapped types. It takes `IReason` (not `IError`) so `Warning` reasons localize by the
  identical mechanism — severity is the panel's concern, not the localizer's. FluentResults already
  flattens `Result.Fail(errors)` into `result.Errors`, so composites need nothing extra. The two
  existing chokepoints — `ResultReportingExtensions.FormatErrors` and `MessagePanelViewModel.RefreshReasons`
  (both its error and warning branches) — route through it. A reflection test asserts every public Core
  `Error`/`Warning` subclass has a switch case, turning a forgotten case from silent English leakage into
  a red build.
- **The localizer's two flattening modes.** Untyped wrapper → recurse `reason.Reasons.OfType<IError>()`
  to find a typed cause (fallback, the mechanism PR ships this). Typed *decorator* (positional context —
  `AtStep`/`AtRow`/`AtColumn`) → the case **composes**: `Format(Resources.AtStep, e.StepIndex, Localize(e.Inner))`.
  Decorators are not discriminators — position is data, the sentence is delegated to the inner error.

**Wave doctrine — an audit (42 confirmed findings, `error-flow-hidden-difficulties` workflow) proved
that typing Core errors localizes almost nothing on its own.** The current flow launders types to
strings and uses raw `ReportError(string)` sinks pervasively, so each wave must do THREE things per
subsystem, and the whole effort reduces to one rule:

> Never turn a reason into a string until `ReasonLocalizer` does it — at exactly one place, the sink.

The three obligations and the five moves that satisfy them:

| Obligation | Move | Replaces |
|---|---|---|
| TYPE | domain error subclass (English `.Message` + structured props) | `Result.Fail("literal")` |
| UN-LAUNDER | batch validation as `List<IError>` (not `List<string>`); positional context via a typed decorator carrying the inner as an `Inner` property (or `CausedBy` — equivalent; the composing localizer case calls `Localize(Inner)`). The batch stays intact — the sink localizes each error then joins | `List<string>`, `new Error($"{ctx}: {inner.Message}")` |
| UN-LAUNDER | preserve the typed cause via `CausedBy`; never bake it into a `reason` string | `new FormulaComputationFailedError(t, inner.Message)` |
| ROUTE | hand the `Result`/`IReason` to the **right** panel channel (see below) | `ReportError(string.Join(Errors.Select(e => e.Message)))` |
| ROUTE | object-channels get an `IReason`-accepting seam | `OnPlcFault → ReportError(error.Message)` |

**Two panel channels, do not cross them** (verified against the code). `RefreshReasons` is the
*snapshot-validity* channel — `RecipeCoordinator.RebuildMessagePanel` drives it from
`_session.Snapshot.Reasons` and it REPLACES its entries wholesale, so it must not carry operation
outcomes (`RecipeCoordinator.cs:550-551` states exactly this contract, and the failed-load path calls
`RebuildMessagePanel()` at `:350`, which would wipe anything transient pushed there). So:
- **Persistent** snapshot validity (e.g. the `LoopParser` for/endfor reasons already in the snapshot) →
  `RefreshReasons` — already routed, localizes for free.
- **Transient** operation failures (a rejected paste/CSV-load with many errors) → `ReportFailure`, which
  localizes each error by type then joins them into one operation entry. Showing a transient batch as one
  entry *per error* is a new panel capability (a transient block cleared with the operation) — a
  deliberate UX decision, filed separately, NOT a localization obligation.

Rule B (exception → typed envelope `PlcCommandFailedError`/`CsvLoadFailedError`/`ClipboardParseFailedError`,
English detail rides along) is the TYPE obligation for `ex.Message` sites.

Two guards lock it in once the sinks are clean (they cannot land before the raw sinks below are routed):
- **Single flattening authority** — no `.Message`/`FormatErrors` join feeds a `ReportError`/`ErrorMessage`,
  *except* a raw `exception.Message` under Rule B (`GridStyleEditorViewModel.cs:308` `$"Save failed:
  {ex.Message}"` is legitimate diagnostic English).
- **Log-English** — a string's locale is not statically detectable, so enforce it structurally: an
  architecture test that `ReasonLocalizer` is referenced only from the panel-seam namespace (never a
  logging path), or a standing review rule if that proves too coarse.

**Slices (resolver-before-waves; the audit turns each wave from "find the sites" into "fix this list"):**

1. **Mechanism PR** (`Docs/plans/20260728-core-error-localization-mechanism.md`) — **prestep, ships
   first.** `ReasonLocalizer` + localization moved into `ReportFailure` (`FormatErrors` stays raw for the
   log) + `RefreshReasons` both branches + coverage test + cases for the two *already-public* typed
   errors. Behavior-preserving for English. Touches no Core/PLC files. Proof-of-pipe leads with
   `OwnedByAnotherInstanceError` (fully structured — clean end-to-end); `FormulaComputationFailedError`
   localizes its headline but its baked `reason` detail stays English until the recipe-value wave types
   its causes.
2. **#115** — UI-origin `LocalizedText`. Lands on the mechanism; different seams, no conflict.
3. **Prerequisite — `ImportedRecipeValidator` batch-typed.** The shared validation gate for paste +
   CSV-load + PLC-read flattens every recipe-value error to `List<string>` (`ImportedRecipeValidator.cs:26`),
   defeating localization for all three ingresses at once. Rework it to accumulate `List<IError>` with
   positional decorators, **before** the recipe/clipboard/CSV waves that all pass through it. Its
   `List<string>` maps 1:1 (`"Step {n}: "` → `AtStep`, `"Property '{key}': "` → `AtColumn` over the inner
   error). Interim: until the recipe wave types `PropertyValidator`, a decorated inner renders localized
   position + English detail — acceptable. The batch surfaces via `ReportFailure` as one joined entry;
   per-error display is the separate panel feature above.
4. **Recipe wave** — type `RecipeSession`, `PropertyParser`, `PropertyValidator`, `RecipeMetadataRegistry`,
   `ActionTreeResolver`, `RecipeAnalyzer`; type the `LoopParser` for/endfor **warnings** (unseal `Warning`,
   widen the coverage test). Preserve the formula cause (`FormulaEvaluator.cs:158/166` → `CausedBy`, not a
   baked `reason`). ~~Route the one raw mutation sink `RecipeGridSurfaceBase.cs:372` (ChangeStepAction)~~ —
   **done-early in #115** (routed to `ReportFailure` with a localized context); the wave only needs to type
   the errors it now carries.
5. **Clipboard/CSV wave** — `CsvRowConverter.cs:89/100` and `ClipboardSerializer.cs:191` → typed
   decorator + `CausedBy` (not `new Error(text)`); envelopes for `CsvService.cs:36` / `ClipboardSerializer.cs:57`
   (Rule B). ~~Route the raw paste sink `ClipboardViewModel.cs:141` → `ReportFailure`~~ — **done-early in
   #115** (routed with a localized context); the wave only types the errors it now carries.
   ~~Fix the dropped-warning bug~~ — **done** (`fix-dropped-result-reasons` branch: the CSV row-count
   warning is now surfaced, in English). Localize it here: (a) type `CsvService.cs:55`'s row-count warning
   as `RowCountMismatchWarning(metadataRows, actualRows)` (needs `Warning` unsealed in slice 4) + a
   `ReasonLocalizer` case + resx pair; (b) add a **`ReportWarning(IReason)` transient-warning seam** and
   swap `RecipeFileViewModel.LoadRecipeAsync`'s raw `ReportWarning(<joined string>)` to route the typed
   warning through `ReasonLocalizer`. Transient warnings have no localizing seam today — `RefreshReasons`
   is the snapshot-validity channel, `ReportWarning(string)` is raw — so this seam is a genuine new bit,
   the warning-side twin of the `ReportFailure` transient-error path.
6. **PLC wave — folds into #120** — makes `NotConnectedError`/`ProtocolVersionMismatchError` public + their
   cases; typed `ConnectionLostError`/`RecipeActiveError` + `PlcCommandFailedError` envelope. UN-LAUNDER
   `PlcSyncExecutor.cs:205,212,235` (forward the typed reason, don't re-wrap `.Message` — `:212` also
   launders the returned `Result` on the same path) and `PlcTransactionExecutor.cs:105,191` (typed
   transport envelopes). ROUTE the whole fault channel:
   `RecipeCoordinator.OnPlcFault:522` currently reads `error.Message` into a raw sink — give it an
   `IReason`-accepting seam. Lands with #120's `CausedBy(ex)` work so the churny PLC-sync files are touched once.
7. **Style-editor surface** — `GridStyleEditorViewModel.cs:297` writes `ErrorMessage` from a `.Message`
   join, a third unrouted panel surface; localize it via `ReasonLocalizer` (it bypasses both panel seams,
   so it needs its own call). Pairs naturally with #118.

## New issues to file

1. **Command error reporting drops exception stack; extract report+log extension** (PR-F1). Note
   `GridStyleEditorViewModel.SaveCommand` and the three bare `MainWindowViewModel` subscriptions as
   part of it.
2. **No global exception backstop** (PR-F2).
3. Edit **#117**: close the "message sink" clause as won't-do (interface rule), keep the coordinator split.
4. **CSV load drops the row-count-mismatch warning** (live bug, independent of localization).
   `RecipeCoordinator.LoadRecipeAsync` replaces the load carrier with `validateResult` and never merges
   `loadResult.Reasons`, so the integrity warning from `CsvService.cs:55` (metadata vs actual step count)
   reaches no panel and no log — a corrupt/hand-edited recipe loads silently. Fix: `result =
   validateResult.WithReasons(loadResult.Reasons)` and surface warnings on a succeeded-with-warnings load.
5. **Undo/Redo drop the coordinator's failed `Result`** (dropped-Result, violates this roadmap's own
   "every `Result` must be consumed" rule). `RecipeCommandsViewModel.Undo/Redo` (`:130,:135`) ignore the
   `Result` from `_coordinator.Undo()/Redo()`, unlike `AddStep`/`DeleteStep` which `ReportFailure` on it,
   so a re-analysis failure on the restored snapshot vanishes. Fix: capture and `ReportFailure` in both,
   matching the sibling mutation handlers.

## Honest scope read

Foundation-first is correct here **with a strict cap**: the true foundation is F1 (report+log
extension) + F2 (backstop) — ~120 lines, two ~1-day PRs. It is not a grand rework; it is the minimal
version of what #113 would otherwise partially duplicate (the #113 plan already writes a third
private copy of the subscription helper), and it fixes a real defect the board underweights:
user-visible errors currently discard the exception type and stack, so a field-reported error leaves
nothing in the log.

The over-build trap is pulling #116, #117's coordinator split, #110, or #111 under the "foundation"
banner — none block or are blocked by the pipe, and sequencing them first delays real correctness
bugs (#108, #109, #112) behind dependency-free refactors. The roadmap keeps Track D parallel.

Minimum viable: F1 alone unblocks #113 cleanly; F2 is strongly recommended but not a mechanical
dependency. Given the explicit choice to go pipe-first, two small PRs then #113 is the clean version
and is not gold-plating.
