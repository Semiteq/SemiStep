# Core-error localization mechanism (prestep for #115)

## Overview

Core produces failures as `Result`/`Result<T>` (FluentResults); the UI shows them via two chokepoints
that read `error.Message` raw. Most Core failures are free-text (`Result.Fail("Not connected")` → the
generic `Error` base with a hardcoded English string), so a Russian operator gets English on every
unhappy path. The fix is to model operator-facing failures as **domain error types** in Core and route
them to localized text **on type** at the UI boundary — the same resx pipeline everything else uses.

This PR lands the **mechanism only**: the UI-side resolver, both chokepoints routed through it, a
build-time coverage test, and the two Core error types that are *already public* wired end-to-end as
proof-of-pipe. It adds no new Core error types and touches no Core/PLC source. It is
**behavior-preserving for English** (see below) and is the foundation the per-subsystem waves and #115
build on.

**Ordering:** this is the prestep for #115. #115 (UI-origin strings via `LocalizedText`) then lands on
a codebase where the Core-error boundary already exists; the two efforts use different seams and do not
conflict.

### The architecture (target, for context — not all built here)

The published rule, recorded in `Docs/plans/20260727-error-reporting-pipe-roadmap.md`:

> A public `Error` type exists **iff** a distinct localized operator sentence exists. Everything else is
> internal and crosses the boundary only wrapped in an envelope type carrying an English `Detail`.

That bounds the public surface at ~21 sentence-types + 3 envelopes regardless of how many internal
failure modes Core grows. The types are rolled out per subsystem in later waves (recipe, clipboard/CSV,
PLC-into-#120), each wave starting with a curation pass that collapses that subsystem's `Result.Fail`
sites onto the contract table in the roadmap. **This PR builds none of those types** — only the resolver
that will localize them and the two already-public ones.

### Behavior-preserving claim

`FormatErrors` is left unchanged (raw English `.Message` joiner — it feeds the log). In the panel seams
(`ReportFailure`, `RefreshReasons`), any free-text reason falls through to `.Message`, and the resx
English for the two wired types is copied byte-for-byte from their current `.Message`, so under the
default (English) culture panel output is identical too. The **only** observable change in this PR:
under `Resources.Culture = ru`, `OwnedByAnotherInstanceError` and `FormulaComputationFailedError` render
Russian where they surface via `ReportFailure`/`RefreshReasons`.

**Proof-of-pipe leads with `OwnedByAnotherInstanceError`** — it is fully structured (user + timestamp, no
baked free-text cause), so it localizes end-to-end cleanly (enable sync while another instance holds the
lease → `MainWindowViewModel:213 ReportFailure`). `FormulaComputationFailedError` localizes its **headline**
by type, but its min/max **detail** is baked English into the `reason` constructor arg and its causes are
untyped, so the detail stays English until the recipe-value wave types those causes and preserves them via
`CausedBy` (roadmap wave doctrine). Both cases are still required now — the coverage test forces every
public Core `Error` type to be mapped.

## Mechanism

### `ReasonLocalizer` (new, `SemiStep.UI/Localization/ReasonLocalizer.cs`)

Static — `Resources` is static, two locales, no DI seam needed beyond setting `Resources.Culture` in
tests. It localizes any `IReason` (both `IError` and `Warning` — see the warnings note below), because
severity is the panel's concern, not the localizer's: an error and a warning are both one operator
sentence selected by type.

```csharp
public static class ReasonLocalizer
{
    public static string Localize(IReason reason)
    {
        return TryLocalize(reason, out var text) ? text : reason.Message;
    }

    private static bool TryLocalize(IReason reason, [NotNullWhen(true)] out string? text)
    {
        text = reason switch
        {
            OwnedByAnotherInstanceError e   => Format(Resources.ErrorOwnedByAnotherInstance, e.Holder.UserName, e.Holder.AcquiredUtc),
            FormulaComputationFailedError e => Format(Resources.ErrorFormulaComputationFailed, e.Target, e.Reason),
            // warning cases join here as data in the recipe wave (once Warning is subclassable)
            _ => null
        };

        if (text is { Length: > 0 })
        {
            return true;
        }

        // only IError nests causes (IReason exposes Message + Metadata only) — guard before recursing
        if (reason is IError error)
        {
            foreach (var cause in error.Reasons.OfType<IError>())
            {
                if (TryLocalize(cause, out text))
                {
                    return true;
                }
            }
        }

        text = null;
        return false;
    }

    private static string Format(string template, params object[] args)
    {
        return string.Format(Resources.Culture ?? CultureInfo.CurrentUICulture, template, args);
    }
}
```

- **Type-switch, not codes.** A missing type falls to `_ => null` → recurse over `CausedBy` → English
  `.Message`. Compile-safe on the resx side (the `Resources.X` accessors are real properties; a deleted
  key is a compile error).
- **Composites need nothing.** FluentResults flattens `Result.Fail(errors)` into `result.Errors`; the
  seams already iterate that list. `CausedBy` nesting is handled by the recursion, so a typed cause
  inside an untyped wrapper still localizes; a fully untyped chain falls back to the outer English.
  **Only `IError` exposes `Reasons`** (in FluentResults `IReason` has just `Message` + `Metadata`), so
  the recursion is guarded by `is IError`; `Warning`/`Success` are leaves.
- **Empty is a miss.** The designer accessors return `string.Empty` for a missing key, so `TryLocalize`
  treats a non-empty result as localized (`is { Length: > 0 }`) — a broken key falls through to
  `.Message`, never a blank panel entry.
- **Args are formatted in, never switched on** — the discriminator litmus. `Holder`, `Target`, `Reason`
  are data rendered into the sentence.
- **Forward note (waves, not this PR):** the recursion above is the *fallback* mode — find a typed cause
  inside an untyped wrapper. Later waves add positional-context *decorators* (`AtStep`/`AtRow`/`AtColumn`)
  whose cases **compose** explicitly: `Format(Resources.AtStep, e.StepIndex, Localize(e.Inner))`. No code
  for that here (no decorator type exists yet); the switch just grows cases. See the roadmap's wave doctrine.

### Route the panel seams through it — leave `FormatErrors` raw for the log

`FormatErrors` is dual-use: it feeds the panel via `ReportFailure` **and** Serilog directly at six sites
(`App.axaml.cs:107`, `RecipeCoordinator.cs:182,219,354,393,419`). Localizing `FormatErrors` itself would
localize the log — violating the log-English decision (and `RecipeCoordinator.cs:182` is plausibly the
exact path `OwnedByAnotherInstanceError` travels). So `FormatErrors` stays the raw English joiner; the
localization goes into the panel seams only:

```csharp
// ResultReportingExtensions.cs:9-12 — FormatErrors UNCHANGED (raw English `.Message`, feeds the log)

// ResultReportingExtensions.ReportFailure — localize HERE, not via FormatErrors
var message = string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize));
panel.ReportError(context is null ? message : $"{context}: {message}");

// MessagePanelViewModel.cs:149 (RefreshReasons — the IError branch)
_validationEntries.Add(new MessageEntry(MessageSeverity.Error, ReasonLocalizer.Localize(error)));

// MessagePanelViewModel.cs:154 (RefreshReasons — the Warning branch, now symmetric)
_validationEntries.Add(new MessageEntry(MessageSeverity.Warning, ReasonLocalizer.Localize(warning)));
```

**Known limitation (honest scope) — exactly which panel surfaces this PR localizes.** Localization is
added at the two seams that pass through `ReasonLocalizer`. Every *other* panel surface reads raw
`.Message`/`FormatErrors` and stays English until its own wave rewrites it. Both wired types reach a
localized seam on a real user path, so the pipe is proven live — not just in a unit test.

Localized now (routed through `ReasonLocalizer`):
- `ReportFailure` — sync enable/load (`MainWindowViewModel:213,249`), cell/selector edits
  (`RecipeGridSurfaceBase:317,346`), file load/save, clipboard, etc. `OwnedByAnotherInstanceError`
  (enable-sync; type preserved through `PlcLifecycleManager.EnableSync:118` `ToResult()`) and
  `FormulaComputationFailedError` (cell edit → `UpdateStepProperty`) both land here.
- `RefreshReasons` validation entries (error + warning branches).

Still raw English, deferred to the owning wave (not a defect — these paths surface no *typed* error this
PR wires):
- `RecipeCoordinator.OnPlcFault:522` → `ReportError(error.Message)` — periodic PLC sync-loop faults,
  which are also type-laundered upstream (`PlcSyncExecutor:205` re-wraps into `new Error(...)`). The PLC
  wave routes this seam and applies Rule A so the type survives.
- `RecipeGridSurfaceBase:372` → `ReportError($"Step {n}: Failed to change action - {FormatErrors()}")` —
  change-action failures; recipe/grid wave rewrites the literal.
- `RecipeGridSurfaceBase:191` → `ReportError(exception.Message)` — raw exception text; stays English by
  the envelope rule regardless.

Keeping `FormatErrors` raw is what protects the log; these unrouted panel sites are the accepted price,
picked up as each wave rewrites its own literals.

### Warnings — same treatment as errors, seam wired now, cases in the recipe wave

`Warning` is `public sealed class Warning(string message) : Success(message)` — today it is free-text,
one type, message-only, exactly the stringly state errors are in, on the `Success` channel. Operator
warnings exist and reach the panel: `LoopParser.cs:45,67` (`"Unmatched EndFor at step {i}"`,
`"Unclosed For loop starting at step {startIndex}"`) — the recipe-unsendable state, a real severity
distinct from a value error. Localization does not care about that distinction; the panel renders
severity, the localizer renders text by type.

So warnings get the **identical** typed→switch treatment. This PR wires the **seam** — the resolver
takes `IReason` and the `RefreshReasons` warning branch routes through it now. That is not speculative
and preserves English (no warning case yet → falls back to `.Message`); it just removes the asymmetry.
What defers to the **recipe wave** is the typed warning *classes*: `Warning` is `sealed`, so typing the
for/endfor warnings needs one structural change (unseal `Warning` or add an abstract base) plus the
subclasses, and that belongs with the recipe-analysis error types in the same wave (`LoopParser` is
recipe analysis). The wave then adds the warning switch cases + resx as pure data and extends the
coverage test to warning types.

### Coverage test — the curation rule, enforced at build time

A reflection test over `SemiStep.Core`'s **public, non-abstract `Error` subclasses** (excludes the
FluentResults `Error` base and internal types). Two assertions:

1. Every such type has a sample registered in the test's `TypeData` map (a new public error with no
   sample fails here — forces the author to acknowledge it).
2. Under `Resources.Culture = ru`, `ReasonLocalizer.Localize(sample)` is non-empty and `!= sample.Message`
   (proves a switch case exists and localizes — a forgotten case fails here).

The recipe wave that introduces the first typed `Warning` subclass widens the reflected set to public
non-abstract `Warning` subclasses too (same two assertions), so warning cases are enforced identically.

The sample map is the one maintained test artifact; adding a sample is a per-wave checklist item. This
converts "forgot the case / forgot the resx pair" from silent English leakage into a red build, which is
what makes the later waves safe pure-data-adds.

## Scope

**In:** `ReasonLocalizer` (takes `IReason`); localize inside `ReportFailure` + **both** `RefreshReasons`
branches (error and warning); keep `FormatErrors` the raw English log joiner; resx keys + Russian +
Designer accessors for the two wired error types; coverage + behavior tests.

**Out (later waves / other PRs):** the ~21 sentence-types + 3 envelopes; the typed `Warning` subclasses +
unsealing `Warning` (recipe wave); making `NotConnectedError`/`ProtocolVersionMismatchError` public
(their PLC wave, folds into #120); the `ex.Message` laundering fixes (Rule A/B, per-subsystem waves);
#115's UI-origin strings; config-load errors (pre-locale `ErrorWindow`, English by design, permanently).
Note: the warning *seam* is in scope (routed now); only typed warning *cases* are out.

## Context (grounded on current master)

- `SemiStep.UI/MessageService/ResultReportingExtensions.cs:9-12` — `FormatErrors` = `string.Join("; ",
  result.Errors.Select(e => e.Message))`. **Dual-use**: `ReportFailure` (`:14-18`) feeds it to the panel,
  and Serilog consumes it directly at six sites (`App.axaml.cs:107`, `RecipeCoordinator.cs:182,219,354,393,419`).
  So `FormatErrors` stays raw; localization moves into `ReportFailure` (which stops delegating to it).
- `SemiStep.UI/MessageService/MessagePanelViewModel.cs:147-150` — `RefreshReasons` error loop adds
  `new MessageEntry(Error, error.Message)`; `:152-155` warning loop adds `new MessageEntry(Warning,
  warning.Message)`. Both routed through `ReasonLocalizer` by this PR.
- `SemiStep.Core/Shared/Warning.cs` — `public sealed class Warning(string message) : Success(message)`;
  free-text, message-only. Operator warnings that reach the panel: `Recipes/Analysis/LoopParser.cs:45,67`
  (`"Unmatched EndFor at step {i}"`, `"Unclosed For loop starting at step {startIndex}"`). Typing these
  (and unsealing `Warning`) is the recipe wave; this PR only routes the branch.
- The two wired types:
  - `SemiStep.Core/Plc/Sync/Ownership/OwnedByAnotherInstanceError.cs` — **public sealed**, `Holder`
    (`OwnerInfo`, has `UserName`, `AcquiredUtc`). `.Message`: `"PLC sync is owned by another instance
    (user {UserName}, since {AcquiredUtc:HH:mm} UTC)."`
  - `SemiStep.Core/Recipes/Formulas/Errors/FormulaComputationFailedError.cs` — **public sealed**,
    `Target`/`Reason`. `.Message`: `"Formula computation for target '{Target}' failed: {Reason}"`.
- Not wired here (internal — excluded from the coverage test until a wave makes them public):
  `NotConnectedError`, `ProtocolVersionMismatchError` (both `internal sealed`).
- The UI references none of these types by name today (they surface only via `.Message`), so adding
  switch cases breaks no existing coupling.
- Parity is already enforced by `SemiStep.Tests/UI/Localization/ResourceSyncTests.cs` (en-keys == ru-keys
  == designer accessors); the two new keys extend it automatically — no new parity test.
- Culture in tests: `SemiStep.Tests/UI/Localization/ResourcesCultureScope.cs` (save/restore
  `Resources.Culture`); assembly-wide parallelization is already disabled.

## Development Approach

- Regular (code, then tests). Warnings are errors; build stays clean after each task.
- Each new key: `<data name="X">` in `Resources.resx` + Russian in `Resources.ru.resx` + a
  `public static string X => ResourceManager.GetString("X", resourceCulture) ?? string.Empty;` accessor
  in `Resources.Designer.cs`. English text copied byte-for-byte from the current `.Message` so English
  output is unchanged. Russian is a first pass, flagged for a native-speaker review in the PR body (not
  build-blocking).
- `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` after each task.
- The `csharp` snippets in this plan use spaces for readability; the repo mandates **tabs** — write tabs,
  braces on their own line, file-scoped namespace, one class per file, usings ordered (System first).

## Acceptance Evidence

**Automatable:**
1. Under `Resources.Culture = ru`, a `Result.Fail(new OwnedByAnotherInstanceError(...))` (the clean demo)
   and a `FormulaComputationFailedError(...)` reported via `ReportFailure` show the Russian sentence in a
   real `MessagePanelViewModel`; the same under English shows the byte-identical original message. (The
   formula case asserts the localized *headline*; its baked detail staying English is expected — deferred
   to the recipe-value wave.)
2. A free-text `Result.Fail("anything")` reported via `ReportFailure` shows `"anything"` unchanged under
   both cultures (fallback proven).
3. **Log stays English:** under `Resources.Culture = ru`, `result.FormatErrors()` on a
   `FormulaComputationFailedError`/`OwnedByAnotherInstanceError` returns the raw English `.Message`
   (the log seam is untouched). Assert directly on `FormatErrors`.
4. A typed error nested under an untyped `CausedBy` wrapper still localizes (recursion proven).
5. The coverage test is green and *fails* when a stub public Core `Error` subclass without a case is
   introduced (demonstrate locally, then remove the stub).
6. resx parity green (`ResourceSyncTests`).

**Manual smoke:** run with `ui.locale=ru`, trigger a PLC-ownership conflict or a formula failure — the
panel shows Russian; the log stays English (the log path is unchanged by this PR).

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: `ReasonLocalizer` + resx keys + the two switch cases

**Files:**
- Create: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs`
- Modify: `SemiStep/SemiStep.UI/Localization/Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`

- [x] add `ErrorOwnedByAnotherInstance` and `ErrorFormulaComputationFailed` keys: English copied
  byte-for-byte from each type's current `.Message` (as `{0}`/`{1}` format templates), Russian first
  pass, Designer accessors. Keep the two resx entry counts equal.
- [x] add `ReasonLocalizer` with `Localize(IReason)` / `TryLocalize` (switch on the two error types →
  `Format`; treat empty as a miss `is { Length: > 0 }`; recurse **only** under `if (reason is IError
  error)` over `error.Reasons.OfType<IError>()` — `IReason` has no `Reasons`) / `Format` (culture =
  `Resources.Culture ?? CultureInfo.CurrentUICulture`). No implicit string paths; no DI.
- [x] `dotnet build SemiStep.slnx` (0 warnings).

### Task 2: Route the chokepoints + coverage and behavior tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/MessageService/ResultReportingExtensions.cs` (`ReportFailure` `:14-18`
  — `FormatErrors` `:9-12` stays raw), `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs`
  (`:149` error branch, `:154` warning branch)
- Create: `SemiStep/SemiStep.Tests/UI/Localization/ReasonLocalizerTests.cs`,
  `SemiStep/SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs`
- Modify (existing): `SemiStep/SemiStep.Tests/UI/ResultReportingExtensionsTests.cs` — add a case that
  `FormatErrors` stays raw English under ru and that `ReportFailure` localizes (likely an addition, not a
  rewrite)

- [x] localize inside `ReportFailure` via `string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize))`
  (stop delegating to `FormatErrors`); leave `FormatErrors` the raw joiner. Route **both** `RefreshReasons`
  branches (`IError` and `Warning`) through `ReasonLocalizer.Localize(...)`. The warning branch has no case
  yet → falls back to `.Message` (English preserved); the seam is now symmetric.
- [x] behavior tests (`ReasonLocalizerTests`, using `ResourcesCultureScope`): the two typed errors render
  Russian under ru and the byte-identical original under English; a free-text error is unchanged under
  both; a free-text `Warning` is unchanged under both (routes through the localizer, falls back); a typed
  error under an untyped `CausedBy` wrapper localizes; assert via a real `MessagePanelViewModel` through
  `ReportFailure` for at least one case (end-to-end through the chokepoint).
- [x] coverage test (`CoreErrorLocalizationCoverageTests`): reflect public non-abstract `Error`
  subclasses in the `SemiStep.Core` assembly; assert each has a sample in a `TypeData` map, and that
  `Localize(sample)` under ru is non-empty and `!= sample.Message`. Seed the map with the two wired types.
  (The recipe wave widens this to public `Warning` subclasses.)
- [x] verify the coverage test fails on a temporary stub public error with no case, then remove the stub.
  (Stub placed in `SemiStep.Core` — where the reflection looks — not the test project; confirmed red, then removed.)
- [x] `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` green.

### Task 3: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md` (and `ui-localization.md` if it already documents the
  panel path)

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors; `dotnet format SemiStep.slnx`.
- [x] confirm `ResourceSyncTests` parity green with the two new keys.
- [x] document in `error-reporting.md`: operator-facing Core failures are typed `Error` subclasses (text
  owned by Core, English); the UI localizes them on type via `ReasonLocalizer` at the two chokepoints,
  falling back to English `.Message`; the published rule (a public error type iff a distinct localized
  sentence; internals cross only inside envelope types); the coverage test enforces it; the log path is
  unchanged (English).
- [x] mark this plan for archival at delivery (do NOT move it mid-run).

## Post-Completion

**Manual verification:** run with `ui.locale=ru`; trigger a PLC-ownership conflict and a formula
failure; confirm the panel is Russian and the log file is English.

**Translations:** the two Russian strings are a first pass — flag in the PR body for native-speaker
review; not build-blocking.

**Next:** #115 (UI-origin `LocalizedText` strings) lands on this foundation, then the per-subsystem
error-typing waves (recipe → clipboard/CSV → PLC-into-#120) per the roadmap.

**Executed by exec:**
- branch: core-error-localization-mechanism

## Verify it yourself

The change is behavior-preserving under English (the default), so the visible effect only appears under
`ui.locale=ru`. Reproduce and prove it:

1. **Automated, fastest proof** — `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~ResultReportingExtensions|FullyQualifiedName~CoreErrorLocalizationCoverage"`.
   - Before the fix (`git stash` the branch or check out `master`): `ReasonLocalizer` does not exist — these tests are absent, and the panel renders Core errors in English regardless of culture.
   - After (`HEAD`): green. The RU cases (`..._UnderRussianCulture_...` in `ReasonLocalizerTests` and `ResultReportingExtensionsTests`) assert the Russian sentence in a real `MessagePanelViewModel`; the EN cases assert byte-identical `.Message`; `FormatErrors_TypedError_UnderRussianCulture_StaysRawEnglish` proves the log seam stays English.
2. **The de-localization guard bites** — in `MessagePanelViewModel.cs:149` revert `ReasonLocalizer.Localize(error)` to `error.Message` and re-run: `RefreshReasons_TypedError_UnderRussianCulture_...` goes red. Restore it.
3. **The coverage guard bites** — add a throwaway `public sealed class XError() : Error("x");` in `SemiStep.Core` and run `CoreErrorLocalizationCoverageTests`: it fails (no `_typeData` sample / no switch case). Remove the stub.
4. **Manual smoke** (optional) — run with `ui.locale=ru`; enable PLC sync while another instance holds the lease (ownership conflict) → the panel shows the Russian sentence; the log file line stays English. A formula failure (edit a cell to violate a formula) localizes its headline (its baked min/max detail stays English by design — deferred to the recipe-value wave).

Full gate: `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (1512 passed).
