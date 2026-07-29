# Slice 4b — Type the recipe value errors (PropertyValidator + RecipeMetadataRegistry)

## Overview

Slice 4a built the warning-localization track; slices 1-3 built the error track and the `ImportedRecipeValidator`
decorator gate. What still renders English regardless of culture is the **recipe value error surface**: the two
producers every recipe-value check flows through — `PropertyValidator` (range/type/string rules) and
`RecipeMetadataRegistry` (not-found / not-in-group lookups) — still raise `Result.Fail($"english string")`. Their
output reaches the panel two ways, both currently English:

- **Import path** (paste / CSV-load / PLC-read → `ImportedRecipeValidator`): the gate wraps each inner error in the
  slice-3 `AtStepError`/`AtColumnError` decorators, which already compose `Localize(inner)`. The moment the inner is a
  typed error the decorator renders it localized — **no gate change needed**.
- **Interactive-edit path** (`RecipeSession.ParseAndValidateColumnValue` → `PropertyParser`/`PropertyValidator`/
  registry, undecorated): the raw `Result` bubbles to `MessagePanelViewModel`, which localizes by type. A typed error
  localizes standalone here too.

This slice types those two producers so both paths localize by type. It is the high-value cut of the recipe wave
(slice 4b), after 4a (warnings) and before 4c (`RecipeSession` index errors, `PropertyParser`, `RecipeAnalyzer`,
`LoopParser` iteration error, and the `FormulaEvaluator` 158/166 → `CausedBy` refactor).

**13 typed error classes**, all public in `SemiStep.Core.Recipes.Errors`. Each is auto-caught by the reflection
coverage test (`EveryPublicCoreReasonType_...`), so
every new type must land with its `ReasonLocalizer` arm + resx pair + sample in the SAME task or the build goes red.

**Scope decisions:**
- `PropertyValidator` **stays `internal static`** — only the error *types* must be public for `ReasonLocalizer` to
  match them; exposing the validator itself is needless surface.
- `RecipeMetadataRegistry`'s 6 `throw new InvalidOperationException(...)` construction-time fail-fasts (lines
  82/108/230/250/263/280) are **out of scope** — they never become `Result` reasons and never reach the UI.
- `ActionTreeResolver` errors are **out of scope** (startup-fatal, rethrown as `InvalidOperationException` at registry
  construction — 4c or later if ever).
- `FormulaEvaluator.cs:158/166` bake `PropertyValidator`'s `.Message` into the formula error's `reason`. Typing
  `PropertyValidator` here does NOT break them (`.Message` stays English on typed errors); dropping the baked reason in
  favour of the now-typed `CausedBy` is the **4c** formula refactor, not this slice.

**Behavior-preserving for English.** Every typed error passes today's exact English string to its base ctor (the
log/`.Message` text), and each resx **en** value equals it byte-for-byte, so English output is unchanged and the gate
tests' fragment `.Contains` asserts survive. Under `ru` the value errors now read Russian.

## Acceptance

1. 13 public typed `Error` subclasses in `SemiStep.Core.Recipes.Errors`, each carrying its interpolated runtime values
   as fields and an English base message identical to the pre-slice string.
2. `PropertyValidator` (9 `Result.Fail` sites → 7 classes; the three "Expected X value" sites share one) and `RecipeMetadataRegistry` (11 sites → 6 templates via the helpers) raise the typed
   errors instead of `Result.Fail(string)`; `ImportedRecipeValidator`'s own `Group value must be integer` raw
   (slice-3) reuses the shared `GroupValueNotIntegerError` (dedup).
3. `ReasonLocalizer` localizes all 13 by type; en renders the unchanged English, ru renders Russian.
4. resx parity (`ResourceSyncTests`: en == ru == Designer) for the 13 new keys.
5. Coverage test (`EveryPublicCoreReasonType_...`) green — each new type has a sample + a localizing case (a new type
   with no case goes red).
6. Both paths localize: import (through the `AtStep`/`AtColumn` decorators, no gate change) and interactive-edit
   (standalone), verified under `ru`.
7. `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## The 13 typed errors (canonical table)

resx key convention `Error<Name>`; en value MUST equal the current baked string; ru uses guillemets «» where a value
is quoted (the established ru typography precedent). `{n}` placeholders map to the ctor fields in order.

### Task 1 batch — PropertyValidator (7 classes)

| Error class | Fields | en (`Error…` value) | ru |
|---|---|---|---|
| `PropertyValueTypeMismatchError` | expectedType, actualType, id | `Expected {0} value but got {1} for '{2}'` | `Ожидалось значение типа {0}, получено {1} для «{2}»` |
| `UnsupportedPropertySystemTypeError` | systemType | `Unsupported property system type: {0}` | `Неподдерживаемый системный тип свойства: {0}` |
| `GroupValueNotIntegerError` | actualType | `Group value must be integer, got {0}` | `Значение группы должно быть целым числом, получено {0}` |
| `ValueBelowMinimumError` | value, min, id | `Value {0} is below minimum {1} for '{2}'` | `Значение {0} меньше минимума {1} для «{2}»` |
| `ValueAboveMaximumError` | value, max, id | `Value {0} exceeds maximum {1} for '{2}'` | `Значение {0} больше максимума {1} для «{2}»` |
| `StringContainsNulError` | id | `String value contains embedded NUL character for '{0}'` | `Строковое значение содержит встроенный символ NUL для «{0}»` |
| `StringTooLongError` | length, max, id | `String length {0} exceeds maximum {1} for '{2}'` | `Длина строки {0} превышает максимум {1} для «{2}»` |

`PropertyValueTypeMismatchError` covers all three "Expected X value but got…" sites (int/float/string) — `expectedType`
is the literal type word (`int`/`float`/`string`), not localized.

### Task 2 batch — RecipeMetadataRegistry (6 classes)

| Error class | Fields | en | ru |
|---|---|---|---|
| `ActionByIdNotFoundError` | id | `Action with id {0} not found` | `Действие с идентификатором {0} не найдено` |
| `ActionByNameNotFoundError` | name | `Action with name '{0}' not found` | `Действие с именем «{0}» не найдено` |
| `PropertyNotFoundError` | propertyTypeId | `Property '{0}' not found` | `Свойство «{0}» не найдено` |
| `ColumnNotFoundError` | key | `Column '{0}' not found` | `Столбец «{0}» не найден` |
| `GroupNotFoundError` | groupId | `Group '{0}' not found` | `Группа «{0}» не найдена` |
| `ValueNotInGroupError` | key, groupId | `Value {0} is not a valid member of group '{1}'` | `Значение {0} не является допустимым членом группы «{1}»` |

The ru column is a starting translation — review it in Rider before exec and correct wording/typography as needed.

## Task 1: Type PropertyValidator (7 classes, fully wired)

**Files:**
- Create: 7 error classes in `SemiStep/SemiStep.Core/Recipes/Errors/` (one per file, public sealed, `AtStepError.cs` as the shape precedent — ctor fields + English base message).
- Modify: `SemiStep/SemiStep.Core/Recipes/PropertyValidator.cs` — raise the typed errors (stays `internal static`).
- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/ImportedRecipeValidator.cs` — swap its own `new Error("Group value must be integer, got {type}")` for `new GroupValueNotIntegerError(...)` (dedup; removes a slice-3 pinned English string).
- Modify: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs` (7 arms + using), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (7 keys + hand-written accessors).
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs` (7 samples).

- [x] Create the 7 Task-1 error classes per the table (fields + English base message identical to the current strings). Field types — read the source, and note two distinct `actualType`s: `PropertyValueTypeMismatchError.actualType` is a **`string`** (`value.GetType().Name`, e.g. "Int32"/"Single" — pinned by `PropertyValidatorStringTypeMismatchTests.cs:25` `.Contain("Int32")`), whereas `GroupValueNotIntegerError.actualType` is the **`PropertyType` enum** (`parsed.Type`). `value`/`min`/`max` are `double`, `length`/`max` are `int`, `id` is `string`. For the numeric-field base messages, format with `FormattableString.Invariant($"…")` (or `CultureInfo.InvariantCulture`) so the `.Message`/log text is culture-stable regardless of process culture — today's interpolation is not, and this keeps logs English-invariant per the log-English rule. (The panel render already formats via `Resources.Culture`, so a fractional bound reads "1.5" en / localized-separator ru — culture-correct; no existing test uses fractional bounds.)
- [x] `PropertyValidator.cs`: replace all 7 `Result.Fail($"…")` with the typed errors (lines 13, 16, 18, 34, 44, 50, 62, 67, 73 — note 13/16/62 all map to `PropertyValueTypeMismatchError` with expectedType int/float/string).
- [x] `ImportedRecipeValidator.cs`: replace its own `Group value must be integer` raw with `GroupValueNotIntegerError` (same typed error PropertyValidator now raises). Verify it still wraps in `AtColumnError` as before.
- [x] `ReasonLocalizer`: add 7 arms `Format(Resources.Error<Name>, error.Field0, …)` + `using SemiStep.Core.Recipes.Errors;` (may already be present from slice 3).
- [x] resx: 7 `Error<Name>` keys in Resources.resx (en, equal to the baked string) + Resources.ru.resx (ru) + 7 hand-written Designer accessors. Keep BOM state of each file as-is (Designer.cs no-BOM outlier; resx match siblings; ReasonLocalizer.cs keeps BOM).
- [x] Coverage: seed 7 samples in `_typeData`; add `using SemiStep.Core.Recipes;` to the coverage test for the `PropertyType` value in the `GroupValueNotIntegerError` sample (the file imports `.Errors`/`.Analysis.Warnings`/`.Formulas.Errors`/`.Shared` but not `.Recipes`).
- [x] The 13 new `Recipes/Errors/*.cs` files carry a UTF-8 BOM, matching the `AtStepError.cs`/`AtColumnError.cs` precedent.
- [x] **Rewrite the slice-3 interim test that this batch supersedes.** `MessagePanelReportingTests.cs` `GateValidationFailure_...ShowsRussianPositionEnglishDetail` (~:200-245) asserts, under `ru`, a Russian position with the inner detail STILL English (`.Contain("exceeds maximum")`) — that inner is exactly `ValueAboveMaximumError`, which Task 1 types. The moment this batch lands, the inner renders Russian. Rewrite it: rename to `...ShowsRussianPositionAndDetail` and assert the Russian detail (Russian position + Russian range message). This is the slice-3 "position localized, detail English" interim state being retired for value errors — expected and correct.
- [x] `dotnet build SemiStep.slnx` 0 warnings; run the **full** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (NOT just the ResourceSync/coverage filters — the superseded test above lives outside them). All green. All other value-error assertions read raw `.Message` (English-preserved), so expect only the one rewrite above.

## Task 2: Type RecipeMetadataRegistry (6 classes, fully wired)

**Files:**
- Create: 6 error classes in `SemiStep/SemiStep.Core/Recipes/Errors/`.
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs` — change `TryGetOrFail`/`ContainsOrFail` to take an `IError` (built at each call site) instead of a `string errorMessage`; raise typed errors at all 11 sites; `GroupHasIntKey:295` raises `ValueNotInGroupError`.
- Modify: `ReasonLocalizer.cs` (6 arms), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (6 keys), `CoreErrorLocalizationCoverageTests.cs` (6 samples).

- [x] Create the 6 Task-2 error classes per the table. Field types: `ActionByIdNotFoundError.id` is **`int`** (the registry key); `name`/`propertyTypeId`/`key`/`groupId` are `string`; `ValueNotInGroupError` is (`int key`, `string groupId`).
- [x] `RecipeMetadataRegistry.cs`: change the two private helpers' signature from `string errorMessage` to `IError error` (`Result.Fail(error)`); update the **10 helper call sites** to pass the typed error (114/129 → `ActionByIdNotFoundError`, 124/134 → `ActionByNameNotFoundError`, 144/149 → `PropertyNotFoundError`, 154/159 → `ColumnNotFoundError`, 169/174 → `GroupNotFoundError`); `GroupHasIntKey:295` is a **direct** `Result.Fail` (not a helper call) → `Result.Fail(new ValueNotInGroupError(key, groupId))`.
- [x] `ReasonLocalizer`: 6 arms. resx: 6 keys (en == baked string) + ru + Designer accessors. Coverage: 6 samples. New error files carry BOM; resx/Designer BOM states preserved.
- [x] `dotnet build SemiStep.slnx` 0 warnings; run the **full** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` green (full suite at this boundary too, symmetry with Tasks 1/3 — the sweep shows no registry test asserts a localized panel entry, but run it to catch any stray break).

## Task 3: Cross-path tests + doc + full verify

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/ReasonLocalizerTests.cs` (representative ru/en render cases).
- Modify: `SemiStep/SemiStep.Tests/Domain/Unit/ImportedRecipeValidatorTests.cs` and/or `MessagePanelReportingTests.cs` (both-path end-to-end).
- Modify: `Docs/architecture/error-reporting.md`.

- [x] ru render cases in `ReasonLocalizerTests` for a representative subset spanning both batches (e.g. `ValueAboveMaximumError`, `StringTooLongError`, `ColumnNotFoundError`, `ValueNotInGroupError`) + one en case pinning `Localize(sample).Should().Be(sample.Message)` per the 4a convention (the coverage test already forces a case for ALL 13; these pin exact wording for the representative few).
- [x] **Import path (decorated):** an `ImportedRecipeValidator` gate failure whose inner is a typed value error renders, under `ru`, as `AtStep`→`AtColumn`→localized-inner (e.g. Russian position + Russian range message). Extends the slice-3 end-to-end.
- [x] **Interactive-edit path (undecorated):** a `RecipeSession.UpdateStepProperty` value failure surfaced through the panel renders the typed error localized under `ru` (standalone, no decorator).
- [x] **Gate fragment-preservation sweep:** grep the Tests tree for the old English fragments (`is below minimum`, `not found`, `is not a valid member`, `Expected int value`, etc.); any assertion reading a raw `.Message` stays as-is (English-preserving); any assertion reading a LOCALIZED panel entry under ambient culture on this ru machine must be culture-scoped (`ResourcesCultureScope.Use("en")`) or switched to a type assertion — same rule as 4a (localized-panel → scope/type; raw `.Message` → leave).
- [x] `Docs/architecture/error-reporting.md`: note that the recipe value producers (`PropertyValidator`, `RecipeMetadataRegistry`) now localize by type on both the decorated import path and the undecorated interactive-edit path.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next (slice 4c):** `RecipeSession` index errors (undo/redo/insert/step-index, 4), `PropertyParser` (3),
`RecipeAnalyzer` max-loop-depth (1), `LoopParser` iteration-type error (1), `ImportedRecipeValidator`'s own
`Unknown action ID` raw (line 38 — forward the now-typed `ActionByIdNotFoundError`), and the `FormulaEvaluator`
158/166 → `CausedBy` refactor (drop the baked `.Message`, now that `PropertyValidator` is typed). Then slice 5
(clipboard/CSV + CSV row-count warning on the unsealed `Warning` + `ReportWarning(IReason)` seam), slice 6 (#120 PLC),
slice 7 (style-editor).

---

**Executed by exec:**
- branch: recipe-value-errors-typed
- commits: ead9b61 (PropertyValidator 7) · 5eff2df (RecipeMetadataRegistry 6) · 81af63d (both-path tests + doc) · 1ddad50 (review-1 doc guillemet fix) · 6693015 (smells fix: drop needless Invariant on int msg)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer 1ddad50 (doc self-contradiction on AtColumnFormat ru guillemets) → smells → fixer 6693015 (Invariant consistency) → comment audit (Ship) → critical ×2 (no critical/major). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1591 passed, 0 failed.
3. All 13 localize by type: `--filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — coverage forces a case for every public Error/Warning subclass; the render cases pin ru wording (e.g. `Значение 500 больше максимума 100 для «temperature»`).
4. resx parity: `--filter "FullyQualifiedName~ResourceSync"` — en == ru == Designer for the 13 new keys.
5. Both paths under ru: `--filter "FullyQualifiedName~MessagePanelReporting"` — import (AtStep→AtColumn→localized inner) and interactive-edit (undecorated `ValueAboveMaximumError` bubbles + localizes standalone).
6. English preserved: the raw-`.Message` gate assertions (`ImportedRecipeValidatorTests`, `PropertyValidatorStringTypeMismatchTests`, `CsvPropertyValidationTests`) stay green unchanged — proof the en text is byte-identical.
7. Manual (optional): under a Russian UI, paste/import a recipe with an out-of-range value or unknown column — the panel shows the value error in Russian.
