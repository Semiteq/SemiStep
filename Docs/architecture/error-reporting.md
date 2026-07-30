# Error Reporting Contract

How a failed operation reaches the user. Read this before adding a new error-surfacing site.

## The two MessagePanel channels

`MessagePanelViewModel` holds two independent channels that it merges into one `Entries` list:

![Connection error surfaced in the notification panel](../img/app_net_error_example.png)

- **Transient operation channel** (`_operationEntry`). A single slot for the outcome of the most
  recent user-initiated operation. Written through `ReportError` / `ReportWarning` / `ReportSuccess`
  and cleared on the next successful mutation (`ClearOperation`). One operation entry at a time; a
  new outcome overwrites the previous one. Not counted in the panel's error/warning totals.
- **Persistent validation channel** (`_validationEntries`). The structural validity of the *current*
  recipe. Rebuilt wholesale from the recipe snapshot's `IReason` list through `RefreshReasons`.
  These entries drive the status-bar error/warning counts and persist until the recipe changes.

## Ownership

- **Core owns the error text.** Failures are `FluentResults.Result` with typed `IError` reasons; the
  message string lives in Core, never in the UI.
- **The UI only routes.** A view model takes a failed `Result` and hands it to the panel; it does not
  compose error wording beyond an optional operation prefix (e.g. `"Step 3"`).

## The reporting seam

`Result`-to-panel routing goes through `ResultReportingExtensions`
(`SemiStep.UI/MessageService/`):

- `string FormatErrors(this IResultBase)` — joins every raw English `.Message` with `"; "`. It is the
  log joiner (feeds Serilog directly) plus the not-yet-routed panel sites; the primary panel path no
  longer uses it (see below).
- `void ReportFailure(this MessagePanelViewModel, IResultBase, string? context = null)` — surfaces the
  **localized** join of the result's errors (`ReasonLocalizer.Localize` over `result.Errors`) on the
  transient operation channel, optionally prefixed with `"{context}: "`. It no longer delegates to
  `FormatErrors`.
- `void ReportWarnings(this MessagePanelViewModel, IResultBase)` — the warning-side twin of
  `ReportFailure`. It surfaces the **localized** join of the result's warnings
  (`ReasonLocalizer.Localize` over `result.Successes.OfType<Warning>()`) on the same transient operation
  channel, and guards the empty case (no warning → no transient entry). This is the transient-warning
  localizing path; it delegates to the raw `MessagePanelViewModel.ReportWarning(string)` member, which
  stays as the internal sink that accepts an already-composed string. `RowCountMismatchWarning` (the CSV
  row-count mismatch on file load) is the first typed warning to flow through it.

A failed operation surfaces **all** of its error messages, not just `Errors[0]`. The message *set* is
the same one the log records; under `Resources.Culture = ru` the panel text is localized while the log
stays English (see "The log path is unchanged").

## Localizing failures on type

Operator-facing Core failures are modelled as **typed `FluentResults.Error` subclasses**, not free-text
`Result.Fail("...")`. Each subclass owns its English message string in Core (aligned with the English
log), and carries its data as structured properties (e.g. `OwnedByAnotherInstanceError.Holder`,
`FormulaComputationFailedError.Target`/`.Inner`) rather than baking them into the string. The same
seam also localizes typed `Success`-derived `Warning` subclasses (e.g. the loop warnings) by type, so
warnings and errors share one localization path.

The UI localizes these **on type**, not by parsing text or by error codes. `ReasonLocalizer`
(`SemiStep.UI/Localization/`) takes any `IReason` and switches on its concrete type to select a resx
template, formatting the structured data into it. An unmapped type falls through to the raw English
`.Message`, so a failure never renders blank and English is always the floor. `ReasonLocalizer` also
recurses over `CausedBy` (`IError.Reasons`), so a typed cause nested inside an untyped wrapper still
localizes.

Localization is applied at exactly **three panel seams**, each of which routes its reasons through
`ReasonLocalizer.Localize`:

- `ResultReportingExtensions.ReportFailure` — the transient operation channel, error side.
- `ResultReportingExtensions.ReportWarnings` — the transient operation channel, warning side (the twin
  of `ReportFailure`). Distinct from `RefreshReasons`: `ReportWarnings` carries the outcome of a single
  operation, while `RefreshReasons` rebuilds the persistent snapshot-validity list.
- `MessagePanelViewModel.RefreshReasons` — the persistent validation channel (both the error and the
  warning branch).

Every other surface that reads `.Message`/`FormatErrors` directly stays English until its own wave
routes it. This uses the same resx pipeline as the rest of the UI chrome — see `ui-localization.md`.

### Positional decorators (compose, not discriminate)

`AtStepError`/`AtColumnError` (`SemiStep.Core/Recipes/Errors/`) are typed *positional* decorators: each
adds a position (`StepNumber` / `ColumnKey`) and delegates the sentence to an inner reason held in
`Inner`. `ImportedRecipeValidator` — the shared recipe-value gate — wraps each step's failures in
`AtStepError` and each column's in `AtColumnError`, **preserving the typed inner** rather than
stringifying it.

These localize by **composition**, which is distinct from the fallback recursion above. A decorator's
`ReasonLocalizer` case formats its *own* localized template (`AtStepFormat` = `"Step {0}: {1}"`, ru
`"Шаг {0}: {1}"`; `AtColumnFormat` = `"Column '{0}': {1}"`, ru `"Столбец «{0}»: {1}"`) with
`Localize(Inner)` as the trailing argument, so nesting composes:
`Localize(AtStep(3, AtColumn("gas", inner)))` → `"Шаг 3: Столбец «gas»: <inner>"`. The two modes are
mirror images:

- **Fallback recursion** finds a typed cause inside an *untyped* wrapper — it walks `CausedBy` and
  localizes the first typed reason it reaches.
- **Composition** is the reverse — a *typed* decorator localizes its own position and folds the
  localized inner into it.

`FormulaComputationFailedError` composes the same way, but folds a *cause* instead of a position: it
carries the typed inner in `Inner` and its `ReasonLocalizer` case renders
`Format(ErrorFormulaComputationFailed, Target, Localize(Inner))`. When the inner is a typed value error
(the `PropertyValidator` cause on the formula path), `Localize(Inner)` recurses and the whole sentence —
headline *and* detail — reads in the current culture; a free-text inner falls through to its English
`.Message`.

Because these decorators are public non-abstract Core `Error` subclasses, the coverage test forces a
`ReasonLocalizer` case for each; a missing case is a red build.

### Recipe value errors localize on both paths

The two producers every recipe-value check flows through — `PropertyValidator` (range/type/string rules)
and `RecipeMetadataRegistry` (not-found / not-in-group lookups) — raise **typed** value errors, so they
localize by type on both routes that carry them to the panel:

- **Import path (decorated).** `ImportedRecipeValidator` wraps each failure in `AtStepError`/`AtColumnError`,
  which compose `Localize(Inner)`. Because the inner is now typed, `Localize(Inner)` renders it localized
  rather than falling through to `Inner.Message`. Under `ru` a gate failure reads position *and* detail in
  Russian, e.g. `"Шаг 3: Столбец «gas»: Значение 5 больше максимума 4 для «amount»"` — no gate change was
  needed; the decorators placed in slice 3 already compose the now-typed inner.
- **Interactive-edit path (undecorated).** `RecipeSession.UpdateStepProperty` →
  `ParseAndValidateColumnValue` → `PropertyValidator`/registry bubbles the typed error straight to the panel
  with no decorator, so it localizes standalone.

The remaining lower-frequency recipe producers now localize by type too: `RecipeSession`
(undo/redo-empty and insert/step index errors), `PropertyParser` (parse failures), `RecipeAnalyzer`
(max loop-nesting depth), and `LoopParser` (iteration-count unsupported type) all raise typed errors
instead of `Result.Fail(string)`. `ImportedRecipeValidator` no longer raises its own unknown-action
string — it forwards the registry's typed `ActionByIdNotFoundError`. With these typed, `Localize(Inner)`
renders them localized rather than falling through to `.Message`.

Still free-text (deferred to a later wave): clipboard/CSV producer errors (the CSV row-count warning is
already typed — see above), PLC, the style-editor, and the
five formula-internal free-text inners (null expression, evaluation exception, non-finite, Int32/float
overflow), which stay English under `ru` because their inner is wrapped in a plain `new Error(text)`.

### The published rule (public error surface)

> A public `Error` type exists **iff** a distinct localized operator sentence exists. Everything else
> is internal and crosses the UI boundary only wrapped in an envelope type carrying an English `Detail`.
> The identical build-time contract applies to typed `Warning` subclasses: a public, non-abstract
> `Warning` type exists **iff** a distinct localized operator sentence exists.

This bounds the localizable public surface to the operator sentences, regardless of how many internal
failure modes Core grows. A build-time reflection test
(`SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests`,
`EveryPublicCoreReasonType_...`) enforces it over both reason kinds: it enumerates every public,
non-abstract `Error` subclass **and** every public, non-abstract `Warning` subclass in `SemiStep.Core`
(the two enumerations concatenated, keyed through a shared `Dictionary<Type, IReason>` sample map). Each
must have a registered sample and must localize to a non-empty string that differs from its English
`.Message` under `Resources.Culture = ru`. A new public error or warning type without a switch case and
resx pair fails the build instead of leaking English silently.

### The log path is unchanged (always English)

`FormatErrors` stays the **raw English** joiner (`string.Join("; ", result.Errors.Select(e => e.Message))`).
It is dual-use — it feeds both the panel and Serilog directly at several sites — so localizing it would
localize the log, which must stay English. `ReportFailure` therefore localizes its reasons itself
(`result.Errors.Select(ReasonLocalizer.Localize)`) rather than delegating to `FormatErrors`. Logs are
always English; only the panel seams localize.

## Routing rules

- **Transient operation outcomes** (save/load failed, paste rejected, step edit rejected) go to the
  operation channel via `ReportFailure` (or `ReportError` when the message is composed mid-string).
- **Persistent structural state** (the current recipe's validity) goes to the validation channel via
  `RefreshReasons`, and from there to the status-bar counts.
- **PLC connection-loss / sync failures** arrive as a typed `IError` on `IPlcSyncService.Faults`, a
  discrete one-shot channel. `RecipeCoordinator` bridges each fault to the operation channel via
  `ReportError(error.Message)` — the message text is owned in Core. The persistent "disconnected"
  label stays in the status bar.
- **Command exceptions** (a `ReactiveCommand` fault on `ThrownExceptions`) are not `Result`-based;
  they route through `ReportThrownExceptions` (see below) rather than a hand-written handler.
- **One deliberate exception** to the `"; "` idiom: the clipboard *paste* failure lists each error on
  its own line (`Environment.NewLine`), because a rejected paste can carry many per-step errors that
  read better stacked. It still surfaces every error (never just `Errors[0]`).

## Command-exception pipe (report + log)

A command whose `Execute` throws (file I/O, PLC calls) faults on `ReactiveCommand.ThrownExceptions`.
These faults route through one extension:

- `IDisposable ReportThrownExceptions<TParam, TResult>(this ReactiveCommand<TParam, TResult>, MessagePanelViewModel panel, ILogger logger, LocalizedText context)`
  (`SemiStep.UI/MessageService/ReactiveCommandReportingExtensions.cs`). Per thrown exception it both
  `logger.LogError(ex, "{Context}", context.Invariant)` **and** `panel.ReportError($"{context.Localized}: {ex.Message}")`.
  The split is deliberate: the log gets `context.Invariant` (English, stable for grepping), the panel gets
  `context.Localized` (the current-culture value) — see `ui-localization.md`'s "Operational, error, and status
  messages" section. The panel keeps the user-facing message it always showed; the log now carries the
  exception type and full stack that the message drops.

The `logger` argument is the caller's own `ILogger<TVm>`, so the Serilog `{SourceContext}` field names
the originating view model in every logged fault. The extension takes the concrete
`MessagePanelViewModel`, not an `IMessageSink` abstraction — one panel implementation exists, so the
seam stays concrete (matches `ResultReportingExtensions`).

Modal dialogs are the exception: `GridStyleEditorViewModel.SaveCommand` surfaces its fault on the
editor's own `ErrorMessage` property (and logs), not the shared panel, because a modal owns its error
surface while it is open.

## Invoking a command imperatively

When code fires a command directly rather than through a bound control (a hotkey handler, for example),
invoke it through `ExecuteIfPossible` (`SemiStep.UI/Reactive/CommandInvocationExtensions.cs`), never
through `ReactiveCommand.Execute()`. `ExecuteIfPossible` routes through `ICommand.Execute`, which keeps
the command inside the reporting boundary: a fault still reaches `ThrownExceptions`, so the report+log
ring above (or the global backstop below) handles it, and nothing rethrows on the caller thread. It also
honors `canExecute` — `ReactiveCommand.Execute()` ignores the guard and runs anyway — and, because
`ICommand.CanExecute` is false while a command is executing, it suppresses re-entrant invocation during
an in-flight command. A raw `.Execute().Subscribe()` with no `onError` bypasses all three: the second
delivery channel (the `Execute()` observable's `OnError`) rethrows inside the dispatcher and unwinds to
`Program.Main`, killing the process.

The two async-void dialog paths (`OnWindowClosing`, `OnSaveCompleted`) carry their own try/catch guards.
Those are the current cover for dialog faults the global backstop cannot catch: it installs no Avalonia
dispatcher hook, so an async-void throw would otherwise unwind the dispatcher into `Program.Main`.

For the VM-driven dialogs that route through `Interaction`s registered in `WhenActivated` (rather than
view code-behind), see `dialogs-and-interactions.md` for the convention.

## Global backstop (last-resort ring)

The per-command pipe above catches faults on a *subscribed* `ThrownExceptions`. What escapes every
inner ring reaches `GlobalExceptionBackstop` (`SemiStep.UI/Logging/`), installed once at startup. It
wires three handlers, each for a different escape route:

- **Recoverable UI faults** — set through ReactiveUI's `IReactiveUIBuilder.WithExceptionHandler(...)`
  (namespace `ReactiveUI.Builder`). Fires when a `ReactiveCommand` faults and nothing else observed
  it. Logs the exception at `Error` and reports a generic message
  (`"An unexpected error occurred; see the log for details."`) to the panel; in a DEBUG build it then
  calls `Debugger.Break()`. The app stays alive. This is the pipeline's default observer for
  `ThrownExceptions`, so it is the safety net beneath every command that forgot to subscribe.
- **`TaskScheduler.UnobservedTaskException`** — a fire-and-forget task fault that no one awaited. Logs
  at `Error` and calls `SetObserved()` so the runtime does not escalate. Log-only, no panel report.
- **`AppDomain.CurrentDomain.UnhandledException`** — a throw on a background thread with no handler.
  Logs at `Critical` (MEL `Critical` maps to Serilog `Fatal`) and runs `Log.CloseAndFlushAsync()` so
  the stack reaches the log file before the process dies. It does not stop the death; it guarantees a
  flushed stack, which is the whole point for background-thread throws that `Program.cs` never sees.

**Ordering.** `Install(IReactiveUIBuilder, IServiceProvider)` runs inside the `UseReactiveUI` build
callback in `App.Run`. `WithExceptionHandler` captures the handler once at build time, which happens
before the `InitializeServices` `AfterSetup` builds the first `ReactiveCommand`. The recoverable
handler is therefore in place before any command can throw. (ReactiveUI 23 dropped the settable
`RxApp.DefaultExceptionHandler`; the handler is configured only through the builder, so this is the
one wiring point that satisfies the "set before command construction" constraint.)

**Lazy panel resolve.** Each handler closure resolves `MessagePanelViewModel` from the provider at
fire time, never inside `Install`. Resolving it eagerly would construct the panel and its
`ToggleCommand` before the backstop is in place, defeating the purpose. The logger is resolved once
up front (logger construction has no side effects).

**Deliberate dev trade.** For an unsubscribed `ThrownExceptions`, the old behaviour was a loud crash
in dev. The backstop converts that into report-loudly plus `Debugger.Break` (DEBUG). An operator-facing
PLC tool is better served by a generic panel message in production than by a crash, and the dev signal
survives through the debugger break. `Install` is idempotent (a static `_installed` guard) so a second
call does not double-subscribe the OS events.
