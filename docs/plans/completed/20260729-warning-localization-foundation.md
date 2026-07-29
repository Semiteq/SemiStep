# Slice 4a — Warning-localization foundation

## Overview

Typed **errors** localize by type at the panel seams (`ReasonLocalizer`, since #151/#152/#154). Typed
**warnings** do not: `Warning` is a single `sealed` `Success` subclass carrying a baked English string, so
every warning renders English regardless of culture even though `MessagePanelViewModel.RefreshReasons`
already calls `ReasonLocalizer.Localize` on each `OfType<Warning>()`. The localizer's type-switch has no
warning arms, so those calls fall through to `.Message`.

This slice builds the warning side of the pipe — the twin of the error mechanism — and proves it on the two
`LoopParser` structural warnings (`Unmatched EndFor`, `Unclosed For loop`). It is deliberately the small,
self-contained mechanism cut of the recipe wave (slice 4), split out ahead of the ~23-class error
conversion (4b/4c) because:

1. It is a distinct concern (warning track: unseal + localizer arms + coverage-test widening), independent
   of every error conversion.
2. Slice 5's CSV row-count warning (`RowCountMismatchWarning`) **needs `Warning` unsealed** — this unblocks it.
3. It establishes the warning-localization pattern once, so 4b/4c and slice 5 only add classes + cases.

**Behavior-preserving for English.** Each typed warning passes today's exact English string to the base
`Warning(message)` ctor (that string is the log/`.Message` text, English by the log-English invariant), and
the resx English value equals it, so English panel output is unchanged. Under `ru` the two loop warnings now
read in Russian.

**Scope guard — what this slice does NOT touch:** no error typing (that is 4b/4c), no config-loader or CSV
warnings (they keep untyped `new Warning(string)` / `WithWarning(string)` and stay English — CSV is typed in
slice 5), no `LoopParser` *iteration-type* error (line 86 — that is an `Error`, belongs to 4c), no
`ReportWarning(IReason)` transient seam (slice 5).

## Acceptance

1. `Warning` is non-sealed; `UnmatchedEndForWarning` and `UnclosedForLoopWarning` are public Core types, each
   with its positional field and an English base message identical to the pre-slice string.
2. `LoopParser` raises the two typed warnings instead of `new Warning($"...")`.
3. `ReasonLocalizer` localizes both warning types by type; under `ru` they render Russian, under `en` the
   unchanged English.
4. `CoreErrorLocalizationCoverageTests` is widened to also enumerate public concrete `Warning` subclasses and
   assert each has a sample + a localizing case (a new warning type with no case goes red).
5. resx parity holds (`ResourceSyncTests`: en == ru == Designer) for the two new keys.
6. `RecipeSession.IsValid` (`!Reasons.OfType<Warning>().Any()`) and the panel warning filter still flip on the
   typed warnings (subclass matches `OfType<Warning>()`).
7. `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 1: Unseal Warning + add the two typed loop warnings

**Files:**
- Modify: `SemiStep/SemiStep.Core/Shared/Warning.cs` — drop `sealed` (base stays concrete + instantiable for the untyped config/CSV warnings).
- Create: `SemiStep/SemiStep.Core/Recipes/Analysis/Warnings/UnmatchedEndForWarning.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/Analysis/Warnings/UnclosedForLoopWarning.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/LoopParser.cs` (lines 45, 67)
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (the ~line 351 comment)

**Namespace convention (decided here, for 4b/4c/slice-5 to follow):** typed warnings live under the
emitter's area — loop/analysis warnings in `SemiStep.Core.Recipes.Analysis.Warnings`; slice 5's CSV warning
will go under its own emitter area (e.g. `SemiStep.Core.…Csv…`), NOT a flat `Recipes.Warnings`. Errors keep
their existing `Recipes.Errors` home.

- [x] `Warning`: `public sealed class` → `public class`. No other change; ctor `Warning(string message) : Success(message)` stays.
- [x] `RecipeCoordinator.cs:~351`: the comment justifying the non-merge of analysis reasons says "the same **sealed** Warning type the viewmodel inspects" — the premise (sealed) dies with the unseal, though the mechanism it protects (reasons not merged, panel inspects `OfType<Warning>()`) still holds. Update the wording to drop "sealed" (keep the rationale). No behavior change.
- [x] `public sealed class UnmatchedEndForWarning(int stepIndex) : Warning($"Unmatched EndFor at step {stepIndex}")` exposing `public int StepIndex { get; } = stepIndex;`. Namespace `SemiStep.Core.Recipes.Analysis.Warnings`. `sealed` to match the Error subclass convention (`AtStepError`/`AtColumnError`/`FormulaComputationFailedError` are all `public sealed`; sealing still satisfies the coverage predicate `IsVisible && !IsAbstract`). English base message identical to the current `LoopParser.cs:45` string.
- [x] `public sealed class UnclosedForLoopWarning(int startIndex) : Warning($"Unclosed For loop starting at step {startIndex}")` exposing `public int StartIndex { get; } = startIndex;`. English base message identical to the current `LoopParser.cs:67` string.
- [x] `LoopParser.cs:45`: `new Warning($"Unmatched EndFor at step {i}")` → `new UnmatchedEndForWarning(i)`.
- [x] `LoopParser.cs:67`: `new Warning($"Unclosed For loop starting at step {frame.StartIndex}")` → `new UnclosedForLoopWarning(frame.StartIndex)`. Add the `using SemiStep.Core.Recipes.Analysis.Warnings;` (or rely on same-namespace parent — the warnings sit under `Analysis.Warnings`, `LoopParser` under `Analysis`, so a using is needed).
- [x] build the Core project — 0 warnings.

## Task 2: ReasonLocalizer arms + resx pair + Designer accessors

**Files:**
- Modify: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs` (switch + using)
- Modify: `SemiStep/SemiStep.UI/Localization/Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`

- [x] `ReasonLocalizer`: add two switch arms alongside the existing ones —
  `UnmatchedEndForWarning warning => Format(Resources.WarningUnmatchedEndFor, warning.StepIndex)` and
  `UnclosedForLoopWarning warning => Format(Resources.WarningUnclosedForLoop, warning.StartIndex)`.
  Add `using SemiStep.Core.Recipes.Analysis.Warnings;`. No change to the `IError` fallback recursion (warnings are `Success`, not `IError`, and are now matched directly by the switch anyway).
- [x] resx: add `WarningUnmatchedEndFor` — en `Unmatched EndFor at step {0}` (identical to the old baked string), ru `Непарный EndFor на шаге {0}`.
- [x] resx: add `WarningUnclosedForLoop` — en `Unclosed For loop starting at step {0}`, ru `Незакрытый цикл For, начатый на шаге {0}`.
- [x] Add the two hand-written accessors to `Resources.Designer.cs` (this file is hand-maintained), matching the existing `AtStepFormat`/`AtColumnFormat` accessor shape.
- [x] Encoding: `ReasonLocalizer.cs` keeps its UTF-8 BOM. `Resources.Designer.cs` is a known no-BOM outlier — preserve its existing no-BOM state (do not add one, to keep the diff minimal; charset is not build-gated). For the resx files, match whatever BOM state the sibling resx files already use. Confirm the Cyrillic round-trips in both resx files.
- [x] build the solution — 0 warnings; confirm `ResourceSyncTests` (en == ru == Designer parity) is green.

## Task 3: Widen the coverage test + warning localization tests + verify

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs`
- Modify/Create: `SemiStep/SemiStep.Tests/UI/Localization/ReasonLocalizerTests.cs` (the two warning render cases)

- [x] Widen the coverage test: re-key `_typeData` from `Dictionary<Type, Error>` to `Dictionary<Type, IReason>` (samples for both errors and warnings live in one map), add a `PublicCoreWarningTypes()` enumeration — public, concrete, `typeof(Warning).IsAssignableFrom(type) && type != typeof(Warning)` on the Core assembly — and drive the SAME assertion loop over `PublicCoreErrorTypes().Concat(PublicCoreWarningTypes())`. Seed `UnmatchedEndForWarning`/`UnclosedForLoopWarning` samples. Keep the existing error assertions intact. Guard: a new public warning subclass with no case must fail this test (the base `Warning` is excluded, mirroring how base `Error` is excluded).
- [x] `ReasonLocalizerTests`: add ru render cases — `UnmatchedEndForWarning(2)` → `Непарный EndFor на шаге 2`, `UnclosedForLoopWarning(5)` → `Незакрытый цикл For, начатый на шаге 5` (under `ResourcesCultureScope.Use("ru")`); add an `en` case (under `ResourcesCultureScope.Use("en")`) asserting `Localize(sample).Should().Be(sample.Message)` — pin resx-en to the ctor message with no third copied literal (the existing `ReasonLocalizerTests.cs:49-57` convention), so English behavior-preservation cannot silently drift.
- [x] **De-brittle the loop-warning assertions by dropping text where the identity check should not live there — NOT by culture-scoping.** Grep `Unmatched EndFor` / `Unclosed For loop` across Tests; three hits, two layers:
  - **Reason layer — already English-preserving, leave as-is.** `CoreValidityTests.cs:43` reads `driver.Warnings`, which is `IReadOnlyList<string>` — `RecipeTestDriver` (`:25-29`) already flattens `snapshot.Reasons.OfType<Warning>().Select(w => w.Message)` to the raw English base-ctor strings, which do NOT pass through `ReasonLocalizer`. So the `.Contains("Unclosed For loop")` match stays green regardless of culture. No change, no culture scope. (Typed identity is not reachable through the driver's `string` surface; adding a `Warning`-typed driver accessor just to type-check an already-passing test is not worth it — the identity contract is covered by the coverage test + the new render cases.)
  - **Panel layer — the culture-sensitive pair; drop the text.** `RecipeCoordinatorTests.cs:410` and `RecipeCoordinatorSaveGateTests.cs:51` assert over `MessagePanel.Entries`, where an entry is `MessageEntry(MessageSeverity, string)` (the localized string; the source `IReason` and its type are gone by design). Their real contract is the lifecycle — "a structural warning surfaces in the panel, then self-heals on the fixing mutation" — not the wording. Replace the `.Contain(e => e.IsWarning && e.Message.Contains("Unclosed For loop", ...))` with `.ContainSingle(e => e.IsWarning)` — drops the culture-sensitive text AND encodes the "exactly one warning by construction" invariant as an assertion (no comment needed), so a future edit that adds a second warning to either scenario fails loudly instead of passing on the wrong entry. The existing `Entries.Should().BeEmpty()` after the fixing mutation stays (the self-heal half). Do NOT make `MessageEntry` carry the `IReason` to enable a type-check — that resurrects the cut per-reason-display feature (roadmap issue #6) for a test's sake.
  - Exact localized wording is pinned ONLY in the new `ReasonLocalizerTests` render cases below — the one place where asserting text is the point.
- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`; `dotnet build` 0 warnings; `dotnet format`.
- [x] confirm `RecipeSession.IsValid` and panel warning filtering still see the typed warnings, and close the warning-through-panel loop with ONE `ru` end-to-end assertion: drive an unclosed-For snapshot through `MessagePanelViewModel.RefreshReasons` under `ResourcesCultureScope.Use("ru")` and assert a warning-severity entry whose `.Message` is the Russian render (`Незакрытый цикл For…`). Home it where slice 3 put its ru end-to-end (`MessagePanelReportingTests.cs`). Also assert the unclosed-For recipe is `!IsValid` (subclass matches `OfType<Warning>()`). This is the only regression test that exercises a warning through the full `RefreshReasons → Localize → panel` path; the panel-layer tests above stay text-free.

## Post-Completion

**Next (slice 4b):** type the high-value value errors — `PropertyValidator` (make public + type its 7–9
errors) and `RecipeMetadataRegistry` (6 not-found/not-in-group templates), which surface on both the import
(decorated) and interactive-edit (undecorated) paths. Then 4c (`RecipeSession` index errors, `PropertyParser`,
`RecipeAnalyzer`, `LoopParser` iteration error, `ImportedRecipeValidator` line 38, and the `FormulaEvaluator`
158/166 → `CausedBy` refactor). Slice 5 (clipboard/CSV) can now type its `RowCountMismatchWarning` on the
unsealed `Warning` and add the `ReportWarning(IReason)` transient seam.

---

**Executed by exec:**
- branch: warning-localization-foundation
- commits: df0bd71 (unseal + typed warnings) · 7e28860 (localizer arms + resx) · 294cbcc (coverage widen + tests + de-brittle panels) · ecf82ba (arch-doc + coverage-enum guard, review-1 fix)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer ecf82ba (doc drift + NotBeEmpty guards) → critical re-check (2 agents, clean) → smells (clean) → comment audit (Ship). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1571 passed, 0 failed.
3. Warning localizes by type: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ReasonLocalizer"` — ru renders `Непарный EndFor на шаге {n}` / `Незакрытый цикл For, начатый на шаге {n}`; en equals the English base message.
4. Coverage guard: `--filter "FullyQualifiedName~CoreErrorLocalizationCoverage"` — enumerates public Error AND Warning subclasses; a new typed warning with no ReasonLocalizer case + sample goes red.
5. End-to-end panel render: `--filter "FullyQualifiedName~MessagePanelReporting"` — an unclosed-For snapshot through RefreshReasons under ru shows the Russian warning entry and `IsValid == false`.
6. Manual (optional): in the running app under a Russian UI, build a recipe with an unclosed For loop — the message panel shows the warning in Russian instead of English.
