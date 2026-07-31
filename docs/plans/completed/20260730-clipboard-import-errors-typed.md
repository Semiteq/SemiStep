# Slice 5c — Type the clipboard import errors

## Overview

The clipboard paste path (`ClipboardSerializer.DeserializeSteps`) still raises free-text `Result.Fail(string)` /
`new Error(string)` for every parse failure, so a paste error renders raw English regardless of culture. It reaches
the panel via `ClipboardViewModel.PasteStepsAsync` → `ReportFailure` (localizes by type, routed in #115). Slice 5b
typed the CSV import path and introduced the shared tabular-parse errors; this slice types the clipboard path,
**reusing** them.

The payoff mirrors 5b, with one path difference: `ClipboardSerializer.ParseProperties` (`:186`) calls
`PropertyParser.Parse` **only** — NOT `PropertyValidator.Validate` (unlike `CsvRowConverter`, which does both). Range
checks (min/max) happen later, in `ImportedRecipeValidator` on the paste ViewModel path (`ClipboardViewModel.
DeserializeStepsFromClipboard:172`), which wraps as `AtStepError` (`Шаг N`, already localized in slice 3). So the
clipboard `AtRowError → AtColumnError → inner` chain only ever carries a **parse** error (`PropertyValueParseError`,
typed in 4c). The standard test config's property columns are `step_duration`/`task` (float) and `comment` (string) —
there is no int property column — so a non-parseable cell yields `PropertyValueParseError(rawValue, "float")`. The
payoff: pasting `abc` into the `task` (float) column composes, under `ru`, to
`Строка 1: Столбец «task»: Не удалось разобрать «abc» как float` — Russian end to end.

**3 new typed classes** in `SemiStep.Core.Recipes.Clipboard.Errors`, plus **6 reuses** of errors shipped in 5b/4b/slice-3.
After this the clipboard+CSV ingress fully localizes.

**Reuse map (no new class — the shared tabular-parse errors from 5b/4b/slice-3):**
- `:95` `new Error($"Row {rowNumber}").CausedBy(error)` → `new AtRowError(rowNumber, error)` (5b composing decorator; English gains `: {inner}` — the display fix, same as 5b).
- `:126` `Action column is empty` → `ActionColumnEmptyError` (5b, byte-identical).
- `:131` `Cannot parse action value '{rawAction}' as integer` → `ActionValueNotIntegerError(rawAction)` (5b, byte-identical).
- `:136` `Unknown action ID '{actionKey}'` (**`actionKey` is `int`**, parsed at `:129`) → `ActionByIdNotFoundError(actionKey)` (4b; English rewords to `Action with id {n} not found`, same unification as 5b).
- `:191` `new Error($"Column '{column.Key}': {error.Message}")` → `new AtColumnError(column.Key, error)` (slice-3, byte-identical, composes the typed inner).
- `:218` `Action column with key '{key}' not found in configuration` → `ActionColumnNotFoundError` (5b, fieldless) — same defect/trigger as CSV's `CsvRowConverter:29` (action column missing under the same `SaveToCsv` filter; the key is always the constant `"action"`), so reuse and accept the English change to `Action column not found` (the `:136` unification precedent). No new class.

**New clipboard-specific classes:**
- `:57` catch-all `Failed to parse clipboard data: {ex.Message}` → `ClipboardParseFailedError` — **Rule-B exception-envelope**: no fields, message is the headline only (`Failed to parse clipboard data`), raised `.CausedBy(ex)`. **Mirror CSV's Rule-B exactly**: inject `ILogger<ClipboardSerializer>` and add `_logger.LogWarning("Failed to parse clipboard data: {Message}", ex.Message)` at the catch, so the exception detail survives in the log (this catch is NOT rare — CsvHelper throws `BadDataException` on malformed quoted pastes, covered by `ClipboardDeserialize_MalformedQuotedData`). Without the log line `ex.Message` would vanish from both panel and log.
- `:85` `ColumnCountMismatchError(int rowNumber, int expected, int actual)` — message byte-identical (`Column count mismatch on row {0}: expected {1}, got {2}. The clipboard data does not match the current configuration.`).
- `:111` `No valid steps found in clipboard data` → `NoValidStepsError` (leaf, no fields).

**Scope decisions:**
- The shared errors live in `SemiStep.Core.Recipes.Import.Errors` (5b) and `Recipes.Errors` (slice-3/4b). Reusing them
  from `Recipes.Clipboard` is a mild namespace cross (clipboard referencing "import" errors), but they are the shared
  tabular-parse errors 5b explicitly introduced for 5c to reuse — accept it rather than churn shipped code with a rename.
- `PropertyParser` is already typed (4c) — `AtColumnError` reuse composes its typed inner.
- Out of scope: PLC (6), style-editor (7), the config-load-culture boundary (English by design).

**Behavior-preserving for English — mostly.** The reused byte-identical errors and the two new leaf/mismatch classes
keep English unchanged. Three deliberate changes (all flagged, same shape as 5b): `AtRowError` adds `: {inner}`, the
`ActionByIdNotFoundError` reuse rewords unknown-action, and `ClipboardParseFailedError` drops `: {ex.Message}`.

## The typed classes

resx key `Error<Name>`; en == current baked string unless flagged; ru guillemets «» where a value is quoted.

| Class | Fields | en | ru | note |
|---|---|---|---|---|
| `ClipboardParseFailedError` | — | `Failed to parse clipboard data` | `Не удалось разобрать данные буфера обмена` | Rule-B; drops `: {ex.Message}`, `.CausedBy(ex)` + `LogWarning` |
| `ColumnCountMismatchError` | rowNumber:int, expected:int, actual:int | `Column count mismatch on row {0}: expected {1}, got {2}. The clipboard data does not match the current configuration.` | `Несоответствие количества столбцов в строке {0}: ожидалось {1}, получено {2}. Данные буфера обмена не соответствуют текущей конфигурации.` | leaf |
| `NoValidStepsError` | — | `No valid steps found in clipboard data` | `В данных буфера обмена не найдено допустимых шагов` | leaf |

The ru column is a first pass — review in Rider before exec.

## Task 1: Type the clipboard errors + rewire all sites

**Files:**
- Create: `ClipboardParseFailedError.cs`, `ColumnCountMismatchError.cs`, `NoValidStepsError.cs` in `SemiStep/SemiStep.Core/Recipes/Clipboard/Errors/` (public sealed, one per file, BOM; leaf-error precedents).
- Modify: `SemiStep/SemiStep.Core/Recipes/Clipboard/ClipboardSerializer.cs` (ctor + sites 57, 85, 95, 111, 126, 131, 136, 191, 218).
- Verify (no edit expected): `ClipboardDi.AddClipboard()` uses `AddSingleton<ClipboardSerializer>()` (open registration), and logging is already registered (`Program.cs` `AddLogging`, tests `.AddLogging()`), so the new `ILogger<ClipboardSerializer>` ctor param auto-resolves with NO `ClipboardDi.cs` edit. Confirm this; do not hunt for a registration change that does not exist.
- Modify: `ReasonLocalizer.cs` (3 arms + usings), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (3 keys), `CoreErrorLocalizationCoverageTests.cs` (3 samples).

- [x] Create the 3 new classes per the table. `ClipboardParseFailedError` is fieldless — base message `Failed to parse clipboard data` (headline only). `ColumnCountMismatchError(int rowNumber, int expected, int actual)` base message byte-identical to the concatenated `:85-87` string. Plain int interpolation.
- [x] `ClipboardSerializer.cs` ctor: add `ILogger<ClipboardSerializer>` (primary-ctor param, matching `CsvService`'s logger injection). No `ClipboardDi.cs` edit needed (open registration auto-resolves). Update the **5** direct test construction sites that call `new ClipboardSerializer(...)`: `UIFixture.cs:155`, `MessagePanelReportingTests.cs:169` and `:370`, `ClipboardViewModelCanExecuteTests.cs:36` and `:117` — pass `NullLogger<ClipboardSerializer>.Instance` (bounded). `CsvTestHelper.cs` resolves via DI (auto-injects), so it needs no change — confirm.
- [x] `ClipboardSerializer.cs` — rewire all 9 sites: `:57` → `_logger.LogWarning("Failed to parse clipboard data: {Message}", ex.Message);` then `return Result.Fail(new ClipboardParseFailedError().CausedBy(ex));`; `:85` → `new ColumnCountMismatchError(rowNumber, csvColumns.Count, csvReader.ColumnCount)`; `:95` → `new AtRowError(rowNumber, error)`; `:111` → `NoValidStepsError`; `:126` → `ActionColumnEmptyError`; `:131` → `ActionValueNotIntegerError(rawAction)`; `:136` → `new ActionByIdNotFoundError(actionKey)`; `:191` → `new AtColumnError(column.Key, error)` (pass the typed inner, do NOT stringify); `:218` → `ActionColumnNotFoundError` (reuse 5b; English drops the key detail → `Action column not found`). Add usings (`Microsoft.Extensions.Logging`, `SemiStep.Core.Recipes.Clipboard.Errors`, `SemiStep.Core.Recipes.Errors`, `SemiStep.Core.Recipes.Import.Errors`).
- [x] `ReasonLocalizer`: 3 arms for the new types (`Format`/bare `Resources.Error<Name>`). The reused types' arms (incl. `ActionColumnNotFoundError` from 5b) already exist. Add usings.
- [x] resx: `ErrorClipboardParseFailed`, `ErrorColumnCountMismatch`, `ErrorNoValidSteps` (en per table; ru per table) in BOTH resx + Designer accessors. Coverage: 3 samples. New error files BOM; Designer/resx BOM as-is.
- [x] **Test blast radius**: grep the Tests tree for `Failed to parse clipboard`, `Column count mismatch`, `No valid steps`, `not found in configuration`, `Unknown action ID`, `Row ` (clipboard), `Action column`. Update any clipboard test pinning `Unknown action ID '{n}'` → `Action with id {n} not found`. Verify any clipboard `FlattenMessage`/`.Message` assert survives the `AtRowError` composition (fragments now in `.Message`). If a test asserted the dropped `: {ex.Message}` of the parse-failed catch, update it to the headline / typed error. Localized-panel assertions under ambient ru → `ResourcesCultureScope.Use("en")`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Cross-path tests + doc + verify

**Files:** `ReasonLocalizerTests.cs`, the clipboard integration/panel test home, `Docs/architecture/error-reporting.md`.

- [x] ru/en render cases: the 3 new types' ru render (literal Russian strings) + the en `Localize==Message` pin. (The reused types are already pinned by 5b.)
- [x] **Value-error end-to-end (the payoff):** a TSV clipboard body with a **non-parseable** cell (e.g. `abc` in an int column — NOT out-of-range, which clipboard-deserialize does not check; see the Overview), deserialized via the real `ClipboardSerializer.DeserializeSteps` and surfaced through `ReportFailure` under `ru`, renders `Строка 1: Столбец «task»: Не удалось разобрать «abc» как float` — proving the `AtRow`→`AtColumn`→`PropertyValueParseError` composition works for the clipboard path. Use a TSV body like `10\t5.0\tabc\tc` (no header; field order action, step_duration, task, comment; first data row = row 1). Compute the exact expected string from the ru resx (`AtRowFormat`/`AtColumnFormat`/`ErrorPropertyValueParse`) — confirm the `task` column is float-typed in the standard config. Home it beside the existing `ClipboardDeserialize_*` cases in `SemiStep.Tests/Csv/Integration/CsvDeserializationTests.cs` (uses `CsvFixture` / `fixture.ClipboardSerializer`). [decision: action 10 (Wait) declares no `task` property, so the task column is skipped for it — used action `20` (For), which declares the float `task`, body `20\t5.0\tabc\tc`; expected string unchanged.]
- [x] fragment sweep across the Tests tree for all Task 1 fragments; confirm raw-`.Message` asserts stay green and only the flagged sites changed; report what moved.
- [x] `Docs/architecture/error-reporting.md`: note the clipboard import producers now localize by type (the CSV+clipboard ingress is now fully typed); the shared tabular-parse errors serve both. Keep it proportionate.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next:** with 5c the clipboard+CSV ingress fully localizes; the only remaining English-by-design surface is the
config-load-culture boundary (`Program.cs` sets `Resources.Culture` only after a successful config load, so a config-load
failure shows hardcoded English by necessity). Then slice 6 (#120 PLC — makes `NotConnectedError`/`ProtocolVersionMismatchError`
public + typed transport envelopes, un-launders `PlcSyncExecutor`/`PlcTransactionExecutor`, routes the `OnPlcFault` sink),
and slice 7 (style-editor `GridStyleEditorViewModel` `.Message`-join surface).

---

**Executed by exec:**
- branch: clipboard-import-errors-typed
- commits: b82a87e (type + rewire 9 sites, ILogger inject, 6 reuses) · 44554f1 (cross-path tests + doc, payoff via action 20) · 27249bb (review-1: type-pin parse-failed/no-valid-steps end-to-end + new unescaped-quote catch test) · 1cb96b3 (smells: doc logger ref) · 701b397 (comments: trim payoff-test decode-key tail)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer 27249bb (LOW: end-to-end type pins) → smells → fixer 1cb96b3 (MINOR doc) → comment audit → fixer 701b397 (MINOR) → critical (satisfied by comprehensive; test/doc-only delta since). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1638 passed, 0 failed.
3. The clipboard composition payoff: `--filter "FullyQualifiedName~CsvDeserialization"` — a non-parseable cell pasted (`20\t5.0\tabc\tc`) through the real `ClipboardSerializer.DeserializeSteps` renders, under ru, `Строка 1: Столбец «task»: Не удалось разобрать «abc» как float` (AtRow→AtColumn→PropertyValueParseError). Pre-slice this showed just "Row 1". The typed `ClipboardParseFailedError` (unescaped-quote → BadDataException catch), `NoValidStepsError` (empty input), and `ColumnCountMismatchError` (too-many-columns) are each pinned end-to-end.
4. All 3 new types localize: `--filter "FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — coverage forces a case per public Error subclass; the render cases pin each new ru string.
5. English preserved: the reused shared errors are byte-identical; `ColumnCountMismatchError`'s message is byte-identical (the existing `.Contains("Column count mismatch")` assert stays green). The three flagged changes (AtRowError `: {inner}`, ActionByIdNotFoundError reword, ClipboardParseFailedError drops `: {ex.Message}` → kept in the log via `logger.LogWarning`) are the only English deltas.
6. Manual (optional): under a Russian UI, paste TSV with a bad cell / too many columns / malformed quoting — the error shows in Russian with row/column context.
