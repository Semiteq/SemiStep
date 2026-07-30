# Slice 5a — Transient-warning seam + typed CSV row-count warning

## Overview

The error track has a localizing transient seam (`ReportFailure` → `ReasonLocalizer.Localize` → the panel's operation
slot). The warning track does not. Warnings only localize on the **snapshot-validity** channel
(`RefreshReasons`, which already runs `OfType<Warning>()` through `ReasonLocalizer`); a **transient** warning has only
the raw `ReportWarning(string)` sink, which shows its argument verbatim. So the one transient warning in the app — the
CSV row-count-mismatch surfaced on file load — renders raw English regardless of culture, even after #153 made it
surface at all.

This slice builds the warning-side twin of `ReportFailure` and proves it on that warning:

1. **`RowCountMismatchWarning`** (typed) replaces `CsvService`'s free-text `WithWarning($"…")`.
2. **`ReportWarnings(this MessagePanelViewModel, IResultBase)`** — a new localizing extension mirroring `ReportFailure`
   (without its `context` parameter — no caller needs it): maps `result.Successes.OfType<Warning>()` through
   `ReasonLocalizer.Localize`, joins, and pushes to the transient slot. (Plural name — the existing raw
   `ReportWarning(string)` member stays; this is the collection/localizing seam.)
3. `RecipeFileViewModel.LoadRecipeAsync`'s raw `ReportWarning(string.Join(… warning.Message …))` becomes
   `_messagePanel.ReportWarnings(result)` — the load-warning now localizes.

It is the small mechanism cut of slice 5, before the CSV/clipboard error typing (5b/5c). The `ReportWarnings` seam is
the roadmap's named "genuine new bit" — the transient-warning localizing path that did not exist.

**Behavior-preserving for English.** `RowCountMismatchWarning`'s English base message equals today's exact string, and
the resx en value equals it byte-for-byte. The raw-`.Message` test (`RecipeCoordinatorLoadRecipeTests`) stays green;
only the localized-panel test needs a culture scope (below). Under `ru` the load warning now reads Russian.

**Scope guard:** only the ONE transient warning (CSV row-count). The CSV/clipboard producer ERRORS
(`CsvService`/`CsvFileSerializer`/`CsvRowConverter`/`ClipboardSerializer`) are 5b/5c. Config-loader warnings stay
untyped English (out of the recipe scope). `RefreshReasons` (snapshot-validity warnings) already localizes and is not
touched.

## Acceptance

1. `RowCountMismatchWarning` (public, `SemiStep.Core.Recipes.Import.Warnings`) carries `filePath`/`metadataRows`/`actualRows`
   with an English base message identical to the pre-slice string; `CsvService` raises it instead of `WithWarning(string)`.
2. `ReportWarnings(this MessagePanelViewModel, IResultBase)` localizes each `Successes.OfType<Warning>()` via
   `ReasonLocalizer.Localize` and reports to the transient slot — the exact twin of `ReportFailure`.
3. `RecipeFileViewModel.LoadRecipeAsync` uses `ReportWarnings(result)`; the CSV row-count warning renders Russian under `ru`.
4. `ReasonLocalizer` localizes `RowCountMismatchWarning`; en unchanged, ru Russian. resx parity + coverage test green.
5. `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 1: Type the warning + build the seam + wire the call site

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/Import/Warnings/RowCountMismatchWarning.cs` (public sealed, `Warning` base, `UnclosedForLoopWarning.cs`/`AtStepError.cs` as the shape precedent — fields + English base message).
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvService.cs` (~55-56) — raise the typed warning.
- Modify: `SemiStep/SemiStep.UI/MessageService/ResultReportingExtensions.cs` — add `ReportWarnings`.
- Modify: `SemiStep/SemiStep.UI/RecipeFile/RecipeFileViewModel.cs` (~137-141) — call `ReportWarnings`.
- Modify: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs` (arm + using), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (1 key + accessor), `SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs` (1 sample).
- Modify (test fix): `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelLoadResultTests.cs` (~66).

- [x] `RowCountMismatchWarning(string filePath, int metadataRows, int actualRows) : Warning($"Row count mismatch in '{filePath}': metadata says {metadataRows}, actual is {actualRows}")` exposing exactly these three get-only properties: `public string FilePath { get; } = filePath;`, `public int MetadataRows { get; } = metadataRows;`, `public int ActualRows { get; } = actualRows;` (these names are pinned — the `ReasonLocalizer` arm and coverage sample reference them). Namespace `SemiStep.Core.Recipes.Import.Warnings`. English base message byte-identical to the current `CsvService` string. Plain interpolation (ints, no `Invariant`). BOM on the new file.
- [x] `CsvService.cs`: replace `okResult.WithWarning($"Row count mismatch in '{filePath}': metadata says {metadata.Rows}, actual is {result.Value.StepCount}")` with `okResult.WithSuccess(new RowCountMismatchWarning(filePath, metadata.Rows, result.Value.StepCount))` (`Warning : Success`, so `WithSuccess` carries it on `Successes` exactly as before — no new `WithWarning` overload needed). Add the `using`.
- [x] `ResultReportingExtensions.cs`: add
  `public static void ReportWarnings(this MessagePanelViewModel panel, IResultBase result)` that does
  `var message = Join(result.Successes.OfType<Warning>().Select(ReasonLocalizer.Localize)); if (message.Length > 0) panel.ReportWarning(message);`
  — mirrors `ReportFailure`, reuses the private `Join`, guards the empty case (no warning → no transient entry). `Warning` is `SemiStep.Core.Shared` — add the using.
- [x] `RecipeFileViewModel.LoadRecipeAsync`: replace lines ~137-141's manual `result.Successes.OfType<Warning>()` filter + raw `ReportWarning(string.Join(… .Message …))` with the localizing `_messagePanel.ReportWarnings(result)`, keeping the `else` `ReportSuccess(Loaded)` branch. (The empty-guard lives in `ReportWarnings`, but keep the VM's `if warnings.Count > 0 … else ReportSuccess` shape so a clean load still shows "Loaded" — i.e. compute the warning list once, call `ReportWarnings(result)` in the `if`, `ReportSuccess` in the `else`.)
- [x] `ReasonLocalizer`: arm `RowCountMismatchWarning warning => Format(Resources.WarningRowCountMismatch, warning.FilePath, warning.MetadataRows, warning.ActualRows)` + `using SemiStep.Core.Recipes.Import.Warnings;`.
- [x] resx: `WarningRowCountMismatch` en `Row count mismatch in '{0}': metadata says {1}, actual is {2}` (== baked) + ru `Несоответствие количества строк в «{0}»: метаданные указывают {1}, фактически {2}` + hand-written Designer accessor. Coverage: 1 sample. BOM states preserved (Designer/resx as-is).
- [x] **Culture-scope the localized-panel test.** `RecipeFileViewModelLoadResultTests.cs:66` asserts a panel entry `.Message.Should().Contain("Row count mismatch")` (Severity=Warning) with no scope; after the warning localizes via `ReportWarnings`, on this ru-locale machine the panel renders Russian → the assertion fails. Wrap the load+assert in `ResourcesCultureScope.Use("en")` (the test's intent is the English wording), OR update it to assert the ru render — pick the smaller edit; keep the "Loaded" success assertions (81-83/102-103) untouched. `ResourcesCultureScope` is `internal` in namespace `SemiStep.Tests.UI.Localization` (same assembly) — add the `using`. Confirm `RecipeCoordinatorLoadRecipeTests.cs:205-207` STAYS as-is: it reads the raw `Warning.Message` off `Successes` (English-preserved) and `OfType<Warning>()` still matches the subclass — do not touch it.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` green.

## Task 2: Cover the seam + doc + verify

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/ReasonLocalizerTests.cs`, a MessagePanel/report test home, `Docs/architecture/error-reporting.md`.

- [x] `ReasonLocalizerTests`: ru render case `new RowCountMismatchWarning("recipe.csv", 5, 3)` → `Несоответствие количества строк в «recipe.csv»: метаданные указывают 5, фактически 3` (under `ResourcesCultureScope.Use("ru")`) + an en `Localize(sample).Should().Be(sample.Message)` pin.
- [x] **Seam end-to-end**: a `Result` carrying a `RowCountMismatchWarning` reported via `panel.ReportWarnings(result)` under `ResourcesCultureScope.Use("ru")` puts the Russian text in the transient operation slot (assert the panel entry is Severity=Warning + the ru string). Add an empty-result case: `ReportWarnings` on a result with no warnings reports NOTHING (no transient entry). Home it in `SemiStep/SemiStep.Tests/UI/MessagePanelViewModelTests.cs` (the transient-slot tests live there) — NOT a `MessagePanelReportingTests`, which does not exist.
- [x] fragment sweep: grep the Tests tree for `Row count mismatch` — confirm only `RecipeCoordinatorLoadRecipeTests` (raw `.Message`, unchanged) and the now-scoped `RecipeFileViewModelLoadResultTests` remain; nothing else asserts it unscoped.
- [x] `Docs/architecture/error-reporting.md`: document the transient-warning seam — `ReportWarnings` is the warning-side twin of `ReportFailure` (both localize into the transient slot), distinct from `RefreshReasons` (snapshot-validity, already localizing). Note `RowCountMismatchWarning` as the first typed transient warning.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next (slice 5b):** type the CSV producer chain — `CsvService` (not-found + IO exception-envelopes, Rule B),
`CsvFileSerializer` (empty, header-mismatch, an `AtRowError` positional wrapper analogous to `AtStepError`),
`CsvRowConverter` (action-column errors; turn the `Column '{k}': {inner.Message}` stringifier into a **composing**
`ColumnParseError` envelope so the now-typed inner from 4b/4c localizes). Reuse `ActionByIdNotFoundError` (4b) for
unknown-action where the semantics match. Then 5c (clipboard: `ClipboardParseFailedError` exception-envelope,
`ColumnCountMismatchError`, `NoValidStepsError`, reusing 5b's shared `AtRowError`/action-column/`ColumnParseError`).
After 5b/5c the clipboard+CSV ingress localizes; the config-load-culture boundary is the last English-by-design surface.

---

**Executed by exec:**
- branch: warning-transient-seam
- commits: 2d77a6e (typed warning + ReportWarnings seam + call-site swap) · 6131d86 (seam/render tests + doc) · d46a44f (review-1 fix: .Any() simplification + arch-doc clarifications)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer d46a44f (2 LOW: VM double-filter, doc CSV-producers over-claim) → smells (clean, 1 no-fix MINOR) → comment audit (Ship) → critical ×2 (no critical/major). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1608 passed, 0 failed.
3. Row-count warning localizes: `--filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — coverage forces a case for every public Warning subclass; the ru render pins `Несоответствие количества строк в «recipe.csv»: метаданные указывают 5, фактически 3`.
4. The transient seam: `--filter "FullyQualifiedName~MessagePanelViewModel"` — `ReportWarnings(result)` under ru puts the Russian warning in the transient slot (Severity=Warning); an empty result reports nothing.
5. English preserved: `RecipeCoordinatorLoadRecipeTests` reads the raw `Warning.Message` off `Successes` unchanged; `RecipeFileViewModelLoadResultTests` is en-scoped and still asserts the English wording.
6. Manual (optional): load a recipe CSV whose `# ROWS=` header disagrees with the body under a Russian UI — the panel shows the row-count mismatch in Russian (previously raw English).
