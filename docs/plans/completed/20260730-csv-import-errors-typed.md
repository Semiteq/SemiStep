# Slice 5b — Type the CSV import errors

## Overview

The CSV file-load path (`CsvService` → `CsvFileSerializer` → `CsvRowConverter`) still raises free-text
`Result.Fail(string)` / `new Error(string)` for every deserialize failure, so a CSV import error renders raw English
regardless of culture. These reach the panel via `RecipeCoordinator.LoadRecipeAsync` → `ReportFailure` (which
localizes by type). Slice 5a typed the CSV row-count *warning*; this slice types the CSV *errors*.

The payoff is the value-error chain. Today a bad cell produces
`new Error("Row 2").CausedBy(new Error("Column 'gas': <inner.Message>"))` — a stringified wrapper around a
now-typed inner (`PropertyParser`/`PropertyValidator` errors, typed in 4b/4c). Turning the wrappers into **composing
decorators** recovers full localization: `AtRowError(2, AtColumnError("gas", ValueAboveMaximumError(...)))` renders
under `ru` as `Строка 2: Столбец «gas»: Значение 5 больше максимума 4 для «amount»` — Russian end to end.

**9 new typed classes** — `AtRowError` joins the composing-decorator family in `SemiStep.Core.Recipes.Errors` (next
to `AtStepError`/`AtColumnError`, which it always composes with); the 8 CSV-specific leaves/envelopes go in
`SemiStep.Core.Recipes.Import.Errors`. Plus **two reuses** of existing typed errors, and the two stringifying wrappers
become composing. The shared row/column decorators (`AtRowError`, `AtColumnError`) and the action-column errors are
introduced here and **reused by 5c** (clipboard).

**Scope decisions:**
- `CsvRowConverter`'s `Column '{k}': {inner.Message}` wrap (`:89/:100`) **reuses the slice-3 `AtColumnError`** — its
  message is byte-identical and it already composes `Localize(inner)`. No new "ColumnParseError" class.
- **`AtRowError` is new** — a positional row decorator mirroring `AtStepError` (a CSV/clipboard row is not a recipe
  step), homed with its siblings in `SemiStep.Core.Recipes.Errors`. Its message becomes `Row {n}: {inner.Message}`
  (was bare `Row {n}` with the inner stringified on `CausedBy`) — a display fix: **today the column/value DETAIL is
  dropped**, not the prefix. `ReasonLocalizer.Localize(new Error("Row 2").CausedBy(inner))` finds no arm for the plain
  `Row 2` wrapper, the fallback recursion into the (currently untyped, stringified) inner returns null, so `Localize`
  falls back to `reason.Message` = bare `"Row 2"` — the panel shows just "Row 2". `AtRowError` composing the inner into
  its `.Message` + arm fixes it (row prefix AND localized inner both show).
- `CsvRowConverter:45`'s `Unknown action ID '{actionKey}'` (**`actionKey` is `int`**, parsed at `:38`) **reuses
  `ActionByIdNotFoundError` (4b)** — same semantics; changes the English to `Action with id {n} not found` (a
  deliberate unification, like the 4c `ImportedRecipeValidator:38` dedup).
- `CsvService` IO/access failures (load `:37/:42`, save `:77/:82`) become **Rule-B exception-envelopes**: a typed error
  carrying `filePath`, raised with `.CausedBy(ex)` (structural — carries the exception for future consumers; note the
  coordinator log path `FormatErrors` reads only top-level `.Message` and does not walk `CausedBy`). The raw `ex.Message`
  detail already survives in the log via `CsvService`'s existing `_logger.LogWarning(..., ex.Message)` calls, which stay.
  The localized headline drops `: {ex.Message}` from the user-facing message. This changes the English message; flagged below.
- `PropertyParser`/`PropertyValidator` are already typed (4b/4c) — this slice only re-wraps their output, no change to them.
- Out of scope: clipboard (5c), PLC (6), style-editor (7).

**Behavior-preserving for English — mostly.** Leaf errors keep byte-identical English (resx en == baked string).
`AtColumnError` reuse is byte-identical. Two deliberate English changes: `AtRowError` gains `: {inner}` (improvement),
and the `ActionByIdNotFoundError` reuse + the exception-envelopes reword. Each is flagged with its test impact.

## The typed classes

resx key `Error<Name>` (or `<Name>Format` for the composing decorators, matching `AtStepFormat`/`AtColumnFormat`);
en == current baked string unless flagged; ru guillemets «» where a value is quoted.

### Task 1 batch — converter/row errors (4 new + 2 reuse)

| Class | Fields | en | ru | note |
|---|---|---|---|---|
| `AtRowError` | rowNumber:int, inner:IError | `Row {0}: {1}` (`AtRowFormat`) | `Строка {0}: {1}` | NEW composing decorator; message was bare `Row {n}` |
| `AtColumnError` (reuse, slice 3) | — | `Column '{0}': {1}` | `Столбец «{0}»: {1}` | byte-identical; CsvRowConverter column wrap |
| `ActionColumnNotFoundError` | — | `Action column not found` | `Столбец действия не найден` | leaf |
| `ActionColumnEmptyError` | — | `Action column is empty` | `Столбец действия пуст` | leaf (shared w/ 5c) |
| `ActionValueNotIntegerError` | rawAction:string | `Cannot parse action value '{0}' as integer` | `Не удалось разобрать значение действия «{0}» как целое число` | leaf (shared w/ 5c) |
| `ActionByIdNotFoundError` (reuse, 4b) | — | `Action with id {0} not found` | (existing ru) | replaces `Unknown action ID '{n}'`; English change |

### Task 2 batch — serializer/service errors (5 new)

| Class | Fields | en | ru | note |
|---|---|---|---|---|
| `CsvBodyEmptyError` | — | `CSV body is empty` | `Тело CSV пусто` | leaf |
| `CsvHeaderMismatchError` | expected:string, actual:string | `CSV header mismatch. Expected: [{0}], Actual: [{1}]` | `Несоответствие заголовка CSV. Ожидалось: [{0}], фактически: [{1}]` | leaf |
| `RecipeFileNotFoundError` | filePath:string | `Recipe file not found: {0}` | `Файл рецепта не найден: {0}` | leaf |
| `RecipeLoadFailedError` | filePath:string | `Failed to load recipe from '{0}'` | `Не удалось загрузить рецепт из «{0}»` | Rule-B envelope, `.CausedBy(ex)`; English drops `: {ex.Message}` |
| `RecipeSaveFailedError` | filePath:string | `Failed to save recipe to '{0}'` | `Не удалось сохранить рецепт в «{0}»` | Rule-B envelope, `.CausedBy(ex)`; English drops `: {ex.Message}` |

The ru column is a first pass — review in Rider before exec.

## Task 1: Converter/row errors + composing wrappers

**Files:**
- Create: `AtRowError.cs` in `SemiStep/SemiStep.Core/Recipes/Errors/` (with its `AtStepError`/`AtColumnError` siblings); `ActionColumnNotFoundError.cs`, `ActionColumnEmptyError.cs`, `ActionValueNotIntegerError.cs` in `SemiStep/SemiStep.Core/Recipes/Import/Errors/`. All public sealed, one per file, BOM; `AtStepError.cs`/`AtColumnError.cs` are the precedents — `AtRowError` composes `: {inner.Message}` and exposes `RowNumber`/`Inner`.
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvRowConverter.cs` (29, 35, 40, 45, 89, 100) and `CsvFileSerializer.cs` (61 the row wrap).
- Modify: `ReasonLocalizer.cs` (arms + usings), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`, `CoreErrorLocalizationCoverageTests.cs`.

- [x] Create the 4 new classes. `AtRowError(int rowNumber, IError inner) : Error($"Row {rowNumber}: {inner.Message}")` exposing `RowNumber`/`Inner` — mirror `AtStepError.cs` exactly (no `CausedBy`; the inner rides `Inner`). Leaves match the leaf-error precedent.
- [x] `CsvRowConverter.cs`: `:29` → `ActionColumnNotFoundError`; `:35` → `ActionColumnEmptyError`; `:40` → `ActionValueNotIntegerError(rawAction)`; `:45` → `new ActionByIdNotFoundError(actionKey)` (int; reuse 4b — add its using); `:89` (parse) and `:100` (validate) → `new AtColumnError(columnKey, e)` (reuse slice-3 decorator; `e` is the typed inner — do NOT stringify). Add usings.
- [x] `CsvFileSerializer.cs:61`: `new Error($"Row {rowNumber}").CausedBy(error)` → `new AtRowError(rowNumber, error)`.
- [x] `ReasonLocalizer`: arms — `AtRowError e => Format(Resources.AtRowFormat, e.RowNumber, Localize(e.Inner))`; leaves `Format`/bare `Resources.Error<Name>`; `ActionByIdNotFoundError` already has its arm (4b). Add usings.
- [x] resx: `AtRowFormat` + `ErrorActionColumnNotFound` + `ErrorActionColumnEmpty` + `ErrorActionValueNotInteger` (en == baked string; ru per table) in both resx + Designer accessors. Coverage: samples for the 4 new types (`AtRowError` sample needs an inner, e.g. `new AtRowError(1, new Error("x"))` — mirror the `AtStepError` coverage sample). New error files BOM; Designer/resx BOM as-is.
- [x] **Test blast radius (Task 1):** grep the Tests tree for the old fragments (`Row `, `Column '`, `Action column`, `Cannot parse action value`, `Unknown action ID`). `CsvPropertyValidationTests` uses `FlattenMessage` (reads `.Message`, walks `CausedBy`) — after the change `AtRowError.Message` = `Row 2: Column 'gas': <inner>` (fragments still present, no `CausedBy`), so the `.Contains` fragment asserts survive; verify. Update any test pinning `Unknown action ID '{n}'` to `Action with id {n} not found`. Any localized-panel assertion under ambient ru culture → `ResourcesCultureScope.Use("en")`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Serializer/service errors (Rule-B envelopes)

**Files:**
- Create: `CsvBodyEmptyError.cs`, `CsvHeaderMismatchError.cs`, `RecipeFileNotFoundError.cs`, `RecipeLoadFailedError.cs`, `RecipeSaveFailedError.cs` in `SemiStep/SemiStep.Core/Recipes/Import/Errors/`.
- Modify: `CsvFileSerializer.cs` (41, 85), `CsvService.cs` (25, 37, 42, and the save block ~77/82 — read it to confirm), `ReasonLocalizer.cs`, resx trio, coverage test.

- [x] Create the 5 classes per the table. `RecipeLoadFailedError(string filePath) : Error($"Failed to load recipe from '{filePath}'")` (and save equivalent) — the message is the localizable headline only; the exception rides `CausedBy` at the raise site. `CsvHeaderMismatchError(string expected, string actual)` carries the two joined column lists.
- [x] `CsvFileSerializer.cs`: `:41` → `CsvBodyEmptyError`; `:85` → `CsvHeaderMismatchError(string.Join("; ", expected), string.Join("; ", actual))`.
- [x] `CsvService.cs`: `:25` → `RecipeFileNotFoundError(filePath)`; `:37`/`:42` → `Result.Fail(new RecipeLoadFailedError(filePath).CausedBy(ex))` (drop the `: {ex.Message}` from the message — it survives in the log via `CausedBy` and the existing `_logger.LogWarning`); the save block `:77`/`:82` → `RecipeSaveFailedError(filePath).CausedBy(ex)`. Keep the existing `_logger.LogWarning(..., ex.Message)` calls.
- [x] `ReasonLocalizer` arms (5) + resx `Error<Name>` keys (en == baked, EXCEPT the two Rule-B envelopes whose en drops `: {ex.Message}` — flagged) + ru + Designer accessors + coverage samples.
- [x] **Resx watch:** an existing key `SaveRecipeFailed` (`Failed to save recipe`) sits one word from the new `ErrorRecipeSaveFailed` (`Failed to save recipe to '{0}'`) — no collision, but glance deliberately so you edit the right entry (and its ru).
- [x] **Test blast radius (Task 2):** grep for `Recipe file not found`, `Failed to load recipe`, `Failed to save recipe`, `CSV body is empty`, `CSV header mismatch`. The two Rule-B envelopes drop `ex.Message` from the message — update any test asserting the exception text in the load/save error message. Note: `CsvDeserializationTests`'s header/empty/invalid-action cases assert `IsFailed` ONLY (they never read `.Message`), so they stay green with no edit — do not spend effort "preserving English" for asserts that don't check it. Any localized-panel assertion under ambient ru culture → `ResourcesCultureScope.Use("en")`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 3: Cross-path tests + doc + verify

**Files:** `ReasonLocalizerTests.cs`, the CSV integration/panel test home, `Docs/architecture/error-reporting.md`.

- [x] ru/en render cases for a representative subset (`AtRowError` wrapping an `AtColumnError` wrapping a typed value error — the full compose; `CsvHeaderMismatchError`; `ActionValueNotIntegerError`) + the en `Localize==Message` pins.
- [x] **Value-error end-to-end (the payoff):** a CSV body with an out-of-range cell, deserialized and surfaced via `ReportFailure` under `ru`, renders `Строка N: Столбец «k»: <Russian value error>` — proving the `AtRow`→`AtColumn`→typed-inner composition. Home it in `SemiStep.Tests/Csv/Integration/` (alongside `CsvPropertyValidationTests`); name the file at exec time.
- [x] fragment sweep across the Tests tree for all Task 1/2 fragments; confirm the raw-`.Message` asserts stay green and only the flagged sites (unknown-action wording, the two exception-envelopes) changed; report what moved.
- [x] `Docs/architecture/error-reporting.md`: note the CSV import producers now localize by type; add `AtRowError` to the positional-decorator/composition list (`AtStep`/`AtColumn`/`FormulaComputationFailed`); note the Rule-B exception-envelope pattern (`CausedBy(ex)`, headline localizes, detail to the log).
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next (slice 5c):** clipboard producer errors (`ClipboardSerializer`) — `ClipboardParseFailedError` (Rule-B
exception-envelope for the top-level catch), `ColumnCountMismatchError` (rowNumber/expected/actual), `NoValidStepsError`,
and its `Action column with key '…' not found in configuration` variant — reusing 5b's shared `AtRowError`,
`AtColumnError`, `ActionColumnEmptyError`, `ActionValueNotIntegerError`, and `ActionByIdNotFoundError`. After 5c the
clipboard+CSV ingress localizes; the config-load-culture boundary is the last English-by-design surface. Then slice 6
(#120 PLC), slice 7 (style-editor).

---

**Executed by exec:**
- branch: csv-import-errors-typed
- commits: f69bd74 (converter/row errors + composing AtRowError/AtColumnError reuse) · c3d9bb8 (serializer/service errors + Rule-B envelopes) · 7cdb343 (cross-path tests + doc) · 1d484f3 (review-1: 6 ru render pins) · 1d5a1c8 (smells: doc namespace paths)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer 1d484f3 (LOW: pin ru render for the 6 remaining new types) → smells → fixer 1d5a1c8 (MINOR: doc path accuracy) → comment audit (Ship) → critical (satisfied by comprehensive; test/doc-only delta since). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1629 passed, 0 failed.
3. The composition payoff: `--filter "FullyQualifiedName~CsvImportLocalization"` — a bad-value CSV deserialized through the real `CsvFileSerializer` renders, under ru, the full chain `Строка 2: Столбец «step_duration»: Значение 100000 больше максимума 86400 для «time»` (both the `Localize` and the panel `ReportFailure` paths). Pre-slice this showed just "Row 2".
4. All 9 typed: `--filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — coverage forces a case for every public Error subclass; the render cases pin each new type's ru string.
5. English preserved: `CsvPropertyValidationTests` FlattenMessage asserts (`Row 2`, column key, bounds) stay green unchanged — the composed `.Message` carries the full English chain. The two Rule-B envelopes drop `: {ex.Message}` from the message; the detail stays in the log via the retained `CsvService._logger.LogWarning`.
6. Manual (optional): under a Russian UI, import a CSV with an out-of-range cell or a bad header — the error shows in Russian, row/column context included.
