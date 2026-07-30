# Slice 4c — Type the remaining recipe errors + FormulaComputationFailedError composition

## Overview

Slices 4a/4b typed the warning track and the recipe *value* producers (`PropertyValidator`,
`RecipeMetadataRegistry`). What remains English on the recipe surface is the lower-frequency free-text raises plus
one existing typed error that still bakes an English child string:

- `RecipeSession` — 4 index/history raises (undo/redo empty, insert-index, step-index). These reach the panel through
  `RecipeCommandsViewModel` → `ReportFailure` (undo/redo since #153; add/delete via the mutation handlers).
- `PropertyParser` — 3 parse raises (unknown-type, parse-int, parse-float). Reached on the interactive-edit path
  (`RecipeSession.ParseAndValidateColumnValue`) and import.
- `RecipeAnalyzer` — max-loop-nesting-depth (1). Surfaces via `RebuildMessagePanel → RefreshReasons`.
- `LoopParser` — the iteration-count unsupported-type **error** (1) (the for/endfor warnings were done in 4a).
- `ImportedRecipeValidator.cs:38` — its own `Unknown action ID {key}` raw. Now that `RecipeMetadataRegistry.GetAction`
  returns the typed `ActionByIdNotFoundError` (4b), forward that instead of raising a bespoke string (dedup).
- `FormulaComputationFailedError` — it localizes its headline but bakes the child's English `.Message` into its
  `reason` string (`FormulaEvaluator.cs:158/166`). Now that the child (`PropertyValidator`) is typed (4b), turn this
  error into a **composing decorator** (like `AtStepError`): carry the typed inner and render `Localize(inner)`.

This is the final recipe-wave cut. After it, the recipe *value/validation* surface localizes end to end.

**7 new typed error classes** (all public `SemiStep.Core.Recipes.Errors`), plus one existing-error refactor
(`FormulaComputationFailedError`) and one dedup (reuse 4b's `ActionByIdNotFoundError`). Each new type is auto-caught by
the coverage test, so it lands with its `ReasonLocalizer` arm + resx pair + Designer accessor + sample in the same task.

**Scope decisions (some settled by the architecture review):**
- `PropertyParser` / `RecipeAnalyzer` / `LoopParser` stay their current accessibility — only the error *types* go public.
- **`UnknownPropertyType` is NOT typed** — `PropertyTypeMapping.FromSystemType` maps every unknown system type to
  `PropertyType.String`, so `PropertyParser.Parse`'s `_` arm (`:18`) is unreachable. Building a localization pipeline for
  dead code is waste; replace that `_ => Result.Fail(...)` with a fail-fast `throw new InvalidOperationException(...)`
  (matching `FormulaEvaluator.ConvertToPropertyValue:245`'s treatment of its own impossible arm). Verify no test drives
  it first.
- **`NoStateToUndo/RedoError` are defensive** — `UndoCommand`/`RedoCommand` are `canExecute`-gated on `_canUndo`/`_canRedo`,
  so the empty-stack failure rarely fires into `ReportFailure`. Still typed (cheap, public API, and it leaves no raw
  `Result.Fail(string)` in `RecipeSession`), but they are defensive, not a user-visible localization win. The index
  errors (insert/step) DO surface (add/delete, paste, analyze-failure) — those are the real ones.
- `FormulaComputationFailedError` stays public; its ctor changes shape `(string target, string reason)` →
  `(string target, IError inner)`, exposing `Inner` in place of `Reason`. This is a **breaking ctor change** — ALL
  **11 construction sites update: 7 production in `FormulaEvaluator` (121, 135, 141, 158, 166, 223, 237) + 4 in tests.**
- Out of scope (slice 5+): clipboard/CSV producers, PLC, style-editor, and the five formula free-text inners (see below).

**Behavior-preserving for English.** Every new typed error keeps today's exact English base message (resx en == it,
byte-for-byte). The `FormulaComputationFailedError` refactor preserves English `.Message` at ALL 7 production sites: its
base message stays `Formula computation for target '{target}' failed: {inner.Message}`, and `inner.Message` is exactly
the child string that used to be baked in. Only the *localized* render changes, and only where the inner is itself typed:
sites 158/166 pass the typed `PropertyValidator` error, so under `ru` their detail is Russian. The other five sites
(121/135/141/223/237) wrap free-text in `new Error(text)`, so their inner detail stays English under `ru` — those
formula-internal messages (null expression, evaluation exception, non-finite, Int32/float overflow) are low-value and
out of scope to type this slice. So: the formula HEADLINE always localizes; the validation-cause detail localizes; the
five arithmetic/exception details stay English for now.

## Acceptance

1. 7 new public typed `Error` subclasses in `SemiStep.Core.Recipes.Errors`, each with its fields + English base message
   equal to the pre-slice string.
2. `RecipeSession` (4 sites), `PropertyParser` (2 parse sites typed; the unreachable `_` arm throws), `RecipeAnalyzer`
   (1), `LoopParser` (1 error) raise the typed errors instead of `Result.Fail(string)`.
3. `ImportedRecipeValidator.cs:38` forwards `ActionByIdNotFoundError` (from `GetAction`) instead of its own raw string;
   the pinned test at `ImportedRecipeValidatorTests.cs:466` updates to the new message.
4. `FormulaComputationFailedError` composes: ctor `(string, IError)`, exposes `Inner`; its `ReasonLocalizer` arm renders
   `Format(ErrorFormulaComputationFailed, Target, Localize(Inner))`; `FormulaEvaluator.cs:158/166` pass the typed inner
   (no `.Message` baking) while 121/135/141/223/237 wrap free-text in `new Error(text)`. All 11 construction sites
   (7 production + 4 tests) updated; English `.Message` preserved at every site.
5. `ReasonLocalizer` localizes all new/changed types; en unchanged, ru Russian.
6. resx parity (`ResourceSyncTests`) for the 7 new keys; coverage test green (all public types have sample+case).
7. `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## The 7 new typed errors (canonical table)

resx key `Error<Name>`; en == current baked string; ru uses guillemets «» where a value is quoted.

### Task 1 batch — additive producers (7 classes)

| Error class | Fields | en (`Error…`) | ru |
|---|---|---|---|
| `NoStateToUndoError` | — | `No state to undo to` | `Нет состояния для отмены` |
| `NoStateToRedoError` | — | `No state to redo to` | `Нет состояния для повтора` |
| `InsertIndexOutOfRangeError` | index:int, stepCount:int | `Insert index {0} is out of range for recipe with {1} steps` | `Индекс вставки {0} вне диапазона для рецепта из {1} шагов` |
| `StepIndexOutOfRangeError` | index:int, stepCount:int | `Step index {0} is out of range for recipe with {1} steps` | `Индекс шага {0} вне диапазона для рецепта из {1} шагов` |
| `PropertyValueParseError` | rawValue:string, targetType:string | `Cannot parse '{0}' as {1}` | `Не удалось разобрать «{0}» как {1}` |
| `MaxLoopNestingDepthExceededError` | maxAllowed:int, actualDepth:int | `Maximum loop nesting depth ({0}) exceeded: {1}` | `Превышена максимальная глубина вложенности циклов ({0}): {1}` |
| `IterationCountUnsupportedTypeError` | type:PropertyType, actionKey:int | `Iteration count property has unsupported type '{0}' in step {1}` | `Свойство счётчика итераций имеет неподдерживаемый тип «{0}» в шаге {1}` |

- `PropertyValueParseError` parameterizes the two parse sites; `targetType` is the literal word `integer`/`float`
  (a type token, NOT localized — matches 4b's `PropertyValueTypeMismatchError.expectedType` precedent). The ru sentence
  keeps the English token, acceptable for a technical type name.
- `MaxLoopNestingDepthExceededError`: `maxAllowed` is the `RecipeAnalyzer.MaxLoopDepth` const (pass its value, 3), so the
  error carries both numbers rather than embedding the const in resx.
- `IterationCountUnsupportedTypeError`: the message says `in step {actionKey}`, but `step.ActionKey` is the ForLoop
  **action id** (constant for every ForLoop), not a step position — a mild pre-existing message-semantics wart. This
  slice preserves the wording verbatim (localization only, English `.Message` unchanged); fixing the message to a real
  step index is a separate concern, not done here.
- The ru column is a first pass — review in Rider before exec.

## Task 1: Type the 8 additive producers (fully wired)

**Files:**
- Create: 7 error classes in `SemiStep/SemiStep.Core/Recipes/Errors/` (public sealed, one per file, BOM, `AtStepError.cs` precedent). Plain int interpolation (no `FormattableString.Invariant` — all fields are int/enum/string, no decimal separator; matches the 4b smells-fix outcome).
- Modify: `RecipeSession.cs` (88, 116, 636, 646), `PropertyParser.cs` (the two parse sites 29/39; the `_` arm at 18 → throw), `RecipeAnalyzer.cs` (27), `LoopParser.cs` (the iteration `new Error(...)` at ~86).
- Modify: `ReasonLocalizer.cs` (7 arms + usings), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (7 keys + accessors), `CoreErrorLocalizationCoverageTests.cs` (7 samples).

- [x] Create the 7 classes per the table. Field types exactly as listed; read each source expression to confirm (`RecipeSession` index is the raw `index` param, `recipe.Steps.Count`; `RecipeAnalyzer` pass `MaxLoopDepth` (=3) and `maxDepth`; `LoopParser` `iterationProperty.Type` is `PropertyType`, `step.ActionKey` is int).
- [x] Raise the typed errors at the 8 reachable sites (RecipeSession 4, PropertyParser 2 parse, RecipeAnalyzer 1, LoopParser 1). `PropertyParser.ParseInt`/`ParseFloat` (29/39) both raise `PropertyValueParseError(rawValue, "integer"|"float")`.
- [x] `PropertyParser.Parse` `_` arm (`:18`): grep the Tests tree first to confirm nothing drives it; then replace `Result.Fail($"Unknown property type '{propertyType}'")` with `throw new InvalidOperationException($"Unknown property type '{propertyType}'")` (unreachable — `FromSystemType` only ever yields Int/Float/String; fail-fast matches `FormulaEvaluator.ConvertToPropertyValue:245`). If a test DOES drive it, keep a typed `UnknownPropertyTypeError` instead and note the deviation.
- [x] 7 `ReasonLocalizer` arms; 7 resx `Error<Name>` keys (en == baked string) + ru + Designer accessors; 7 coverage samples. New error files carry BOM; Designer/resx BOM states preserved.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green (these are additive + English-preserving, so expect no test changes; if a test asserts a raw `.Message` it stays green, and no localized-panel test currently pins these strings — confirm via the sweep in Task 3).

## Task 2: Dedup ImportedRecipeValidator + FormulaComputationFailedError composition

**Files:**
- Modify: `ImportedRecipeValidator.cs` (line 38 area) + `SemiStep/SemiStep.Tests/Domain/Unit/ImportedRecipeValidatorTests.cs:466`.
- Modify: `FormulaComputationFailedError.cs`, `FormulaEvaluator.cs` (all 7 sites: 121, 135, 141, 158, 166, 223, 237), `ReasonLocalizer.cs` (the existing formula arm).
- Modify the 4 test construction sites: `CoreErrorLocalizationCoverageTests.cs:32`, `ReasonLocalizerTests.cs:36`, `ResultReportingExtensionsTests.cs:91,102`.

- [x] **Dedup:** `ImportedRecipeValidator` — when `GetAction(step.ActionKey)` fails, forward its error (the typed
  `ActionByIdNotFoundError`) instead of raising `new Error($"Unknown action ID {step.ActionKey}")`. Keep the `AtStepError`
  wrapping. Update `ImportedRecipeValidatorTests.cs:466` from `"Unknown action ID {UnknownActionId}"` to the registry
  wording `"Action with id {UnknownActionId} not found"` (still asserted on `stepError.Inner.Message`). This is a
  deliberate one-path English wording change (unifying on the registry's message), acceptable in a localization slice.
- [x] **Composition refactor:** `FormulaComputationFailedError` ctor `(string target, string reason)` →
  `(string target, IError inner)`; base message `$"Formula computation for target '{target}' failed: {inner.Message}"`
  (English preserved — `inner.Message` is what used to be baked); replace the `Reason` property with `Inner` (IError);
  keep `Metadata["target"]`, drop `Metadata["reason"]` (grep first — no production consumer exists per the arch review).
  Update the `ReasonLocalizer` arm to `Format(Resources.ErrorFormulaComputationFailed, error.Target,
  Localize(error.Inner))` (composes like the `AtStep`/`AtColumn` arms). resx template unchanged (`… failed: {1}`).
- [x] Update ALL 7 `FormulaEvaluator` construction sites to the `IError` ctor:
  - `:158` → `new FormulaComputationFailedError(target, validation.Errors[0])` (typed inner; drop the `.Message` and the
    now-redundant separate `.CausedBy` — `Inner` is the carrier, matching `AtStepError`).
  - `:166` → `new FormulaComputationFailedError(target, groupCheck.Errors[0])` (first error; the group check yields one —
    `ValidateGroupValue` returns at most one, verified, so no message is lost vs the old `"; "` join).
  - `:121`, `:135`, `:141`, `:223`, `:237` → wrap the existing free-text in `new Error(<same text>)` (e.g.
    `new FormulaComputationFailedError(target, new Error("Expression evaluated to null."))`). `.Message` unchanged; these
    inners stay English (out of scope to type). (None of these five currently carry a separate `.CausedBy` — only
    158/166 did, and those are dropped since `Inner` is now the carrier.)
- [x] Update the 4 test construction sites to pass an `IError` inner: `new FormulaComputationFailedError("temp", new Error("min > max"))` (`CoreErrorLocalizationCoverageTests.cs:32`, `ReasonLocalizerTests.cs:36`, `ResultReportingExtensionsTests.cs:91,102`). Composed output is identical to before for a plain untyped inner (falls through to `.Message`), so assertions on the reported/localized string stay green; a `.Reason` assertion (grep — likely none) becomes `.Inner`.
- [x] Check `FormulaEvaluatorTests` / `RecipeSessionFormulaIntegrationTests` (they use `.OfType<FormulaComputationFailedError>()` — type checks, safe; `:156`/`:179` assert `Contain("NaN")`/`("Infinity")` on `.Message`, which stays green since the free-text inner is preserved) for any `.Reason`/baked-message assertion and update it to `.Inner`/the composed form.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 3: Cross-path tests + doc + verify

**Files:**
- Modify: `ReasonLocalizerTests.cs`, `MessagePanelReportingTests.cs` (or the relevant end-to-end homes), `Docs/architecture/error-reporting.md`.

- [x] ru/en render cases in `ReasonLocalizerTests` for a representative subset of the 7 new types (e.g. `StepIndexOutOfRangeError`, `PropertyValueParseError`, `IterationCountUnsupportedTypeError`) + the en `Localize==Message` pin per convention.
- [x] **Formula composition end-to-end:** a `FormulaComputationFailedError` wrapping a TYPED inner (e.g. `ValueAboveMaximumError`) renders, under `ru`, the Russian headline AND the Russian inner detail (proving the composition recurses) — this is the payoff of the refactor. Home it where the formula/panel tests live.
- [x] **Fragment-preservation sweep:** grep the Tests tree for the new English fragments (`No state to undo`, `is out of range for recipe`, `Unknown property type`, `Cannot parse`, `Maximum loop nesting depth`, `unsupported type`, `Unknown action ID`, `Formula computation for target`). Raw-`.Message` assertions stay as-is; localized-panel assertions under ambient ru culture get `ResourcesCultureScope.Use("en")` or a type assertion. Report what changed (expect: the pinned :466 from Task 2, and any formula-render test).
- [x] `Docs/architecture/error-reporting.md`: note the remaining recipe producers now localize by type, and that `FormulaComputationFailedError` composes its typed inner (add it to the positional-decorator / composition list alongside `AtStep`/`AtColumn`). **Also fix the stale member reference at ~:48** — that line names `FormulaComputationFailedError.Target`/`.Reason` as a structured-data example, but Task 2 removes `.Reason` (replaced by `.Inner`); change `.Reason` → `.Inner` (grep the doc for `.Reason` to catch any other occurrence).
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next (slice 5):** clipboard/CSV producers (`CsvRowConverter`, `ClipboardSerializer`) → typed decorator + `CausedBy`
envelopes; the CSV row-count warning as `RowCountMismatchWarning` on the now-unsealed `Warning` + a `ReasonLocalizer`
case; and the `ReportWarning(IReason)` transient-warning seam (the warning-side twin of `ReportFailure`). Then slice 6
(#120 PLC), slice 7 (style-editor). After slice 5 the config-load-culture boundary is the only remaining English-by-design surface.

---

**Executed by exec:**
- branch: recipe-remaining-errors-typed
- commits: 5405c71 (7 producer errors) · c8460eb (formula composition + unknown-action dedup) · 8f23462 (cross-path tests + doc) · ee17f5a (smells: drop vestigial metadata, unquote type token)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED, zero findings) → smells (3 MINOR fixed) → comment audit (Ship) → critical ×2 (no critical/major). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1603 passed, 0 failed.
3. New producers localize: `--filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — coverage forces a case for every public Error/Warning subclass; render cases pin ru wording (e.g. `Индекс шага 5 вне диапазона…`, `Не удалось разобрать «abc» как integer`).
4. Formula composition payoff: the render test `Localize_FormulaComputationFailed_WrappingTypedInner_UnderRussianCulture_ComposesRussianDetail` proves `FormulaComputationFailedError` recurses into a typed inner — under ru it renders `Вычисление формулы для цели «temp» не выполнено: Значение 500 больше максимума 100 для «temperature»` (Russian headline AND Russian inner detail). Pre-refactor the inner detail was baked English.
5. English preserved: `FormatErrors`/raw-`.Message` gate assertions stay green unchanged; the 5 formula free-text inners (null/exception/non-finite/overflow) stay English by design.
6. Manual (optional): under a Russian UI, trigger an out-of-range formula target or a max-loop-depth recipe — the message shows in Russian.
