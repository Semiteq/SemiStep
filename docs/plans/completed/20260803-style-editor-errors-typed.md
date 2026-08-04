# Slice 7 — Localize the style-editor error surface

## Overview

The in-app grid-style editor (`GridStyleEditorViewModel`) is the last unlocalized operator surface in the
error-localization roadmap. Its `ErrorMessage` (bound to the editor window) is built by **joining raw `.Message`
strings**, bypassing `ReasonLocalizer` entirely — `ReasonLocalizer` is not referenced in `StyleEditor/` at all. Three
join sites:

```csharp
// LoadAsync (:193)   — load + validate failures
ErrorMessage = string.Join("; ", result.Errors.Select(error => error.Message));
// Save (:297)        — validate + write failures
ErrorMessage = string.Join("; ", result.Errors.Select(error => error.Message));
// ReportSaveException (:308) — the async-save command's exception
ErrorMessage = $"{Resources.SaveFailed}: {exception.Message}";
```

The eight English strings these can show all come from three free-text producers:

- **`GridStyleValidator`** (validation of `ui/grid_style.yaml`): config-missing, `colors`-section-missing, generic
  section-missing, orientation-invalid, key-missing-or-empty, invalid-hex-color.
- **`GridStyleWriter.Save`**: `Failed to save {file}: {ex.Message}`.
- **`GridStyleLoader.LoadAsync`**: `Grid style config not found: {path}`, `Failed to load {file}: {ex.Message}`.

**Why the whole surface localizes (no English-by-design leftover here).** The roadmap's "config-load boundary stays
English" rule is about the **startup** config load — the app reads YAML at launch to *establish* the UI culture, so an
error there genuinely can't be localized (no culture yet). The **style editor is different**: the operator opens it
*after* startup, so the culture is always up when any of these eight fire. Leaving the loader errors English while the
validator errors are Russian would make one panel show mixed languages depending on whether the file is missing
(English) or a color is malformed (Russian). So this slice types **all eight** producers and routes the three join sites
through `ReasonLocalizer.Localize`. After it, the editor surface has no English leftover.

Pure localization. The structural refactor of this file (issue #118 — the ~60-field declarative map, async `Save`,
`HexColor.Parse` removal) is a **separate concern and out of scope**; this slice touches only the three join sites in the
view model and the eight Core producers. The #118 rewrite will preserve/move the three routed lines, so the two do not
conflict.

**Split guidance.** The slice is two mechanism groups — Task 1 (validation errors, the common in-editor path) and Task 2
(file-I/O Rule-B envelopes). Each is independently shippable: Task 1 alone leaves the loader/writer file-I/O errors
English (a smaller, coherent PR), and Task 2 finishes the surface. Exec both for the complete slice, or stop after Task 1
and run Task 2 as a follow-up — either way the plan carries the whole surface so nothing dangles.

## The typed classes

Eight `public sealed` errors in `SemiStep.Core.Configuration` (leaf-error shape; the two file-I/O ones are Rule-B
exception-envelopes carrying `CausedBy(ex)`). resx key `Error<Name>`; en == the current baked string unless flagged
(Rule-B drops the `: {ex.Message}` tail from the headline). ru uses guillemets «» around quoted values. Making each
public auto-enrolls it in `CoreErrorLocalizationCoverageTests`, so every type's arm + resx + sample must land in the same
task as its make-public (no red window). The ru column is provisional — review before exec.

| Class | Fields | en | ru | source |
|---|---|---|---|---|
| `GridStyleConfigMissingError` | — | `Grid style configuration is missing (ui/grid_style.yaml).` | `Конфигурация стиля таблицы отсутствует (ui/grid_style.yaml).` | Validator :19 |
| `GridStyleSectionMissingError` | section | `Grid style configuration is missing '{0}' section.` | `В конфигурации стиля таблицы отсутствует раздел «{0}».` | Validator :24 (`colors`) + :197 (generic) — unify |
| `GridStyleOrientationInvalidError` | value, expectedRows, expectedColumns | `Grid style 'orientation' has unknown value: '{0}'. Expected '{1}' or '{2}'.` | `Недопустимое значение 'orientation' стиля таблицы: «{0}». Ожидается «{1}» или «{2}».` | Validator :185 — **carries the two expected values as properties** (see note below) |
| `GridStyleKeyMissingError` | section, key | `Grid style '{0}.{1}' is missing or empty.` | `Параметр стиля таблицы «{0}.{1}» отсутствует или пуст.` | Validator :232 |
| `GridStyleHexColorInvalidError` | section, key, value | `Grid style '{0}.{1}' has invalid hex color: '{2}'. Expected format: '#RRGGBB' or '#AARRGGBB'.` | `Недопустимый цвет в «{0}.{1}»: «{2}». Ожидается формат «#RRGGBB» или «#AARRGGBB».` | Validator :238 |
| `GridStyleConfigNotFoundError` | path | `Grid style config not found: {0}` | `Файл конфигурации стиля таблицы не найден: {0}` | Loader :23/:30 |
| `GridStyleLoadFailedError` | fileName | `Failed to load {0}` (drops `: {ex.Message}`) | `Не удалось загрузить {0}` | Loader :41 — Rule-B, `CausedBy(ex)` |
| `GridStyleSaveFailedError` | fileName | `Failed to save {0}` (drops `: {ex.Message}`) | `Не удалось сохранить {0}` | Writer :39 — Rule-B, `CausedBy(ex)` |

`GridStyleConfigMissingError` (validator, the DTO deserialized to null) and `GridStyleConfigNotFoundError` (loader, the
file/dir is absent) are kept distinct — different layers, different context. All eight in
`SemiStep.Core.Configuration` (BOM, empty-brace leaf shape; the field-carrying ones expose their fields as properties for
the arm, like `ProtocolVersionMismatchError`).

**Orientation error carries its expected values — do NOT reference `GridOrientationValues` from the arm.**
`GridOrientationValues` is `internal` in `SemiStep.Core.Configuration.Dto` and Core's only `InternalsVisibleTo` is
`SemiStep.Tests` — so `ReasonLocalizer` (in `SemiStep.UI`) cannot see it (CS0122). Give
`GridStyleOrientationInvalidError(string value, string expectedRows, string expectedColumns)` three properties; the
validator (which *can* see the internal constants) constructs it as
`new GridStyleOrientationInvalidError(orientation, GridOrientationValues.RowsAsSteps, GridOrientationValues.ColumnsAsSteps)`,
and the arm formats `(e.Value, e.ExpectedRows, e.ExpectedColumns)`. Do not widen `GridOrientationValues` to public or add
an `InternalsVisibleTo` for the UI.

## Task 1: Type the validation errors + route the view-model join sites

Types the five `GridStyleValidator` producers and makes the editor localize validation failures. After this task, a
malformed color or missing section renders Russian in the editor; the loader/writer file-I/O errors still fall through to
English (`.Message`) until Task 2 — the routing localizes what is typed and passes the rest through unchanged (the
established `ReasonLocalizer` fall-through).

**Files:**
- Create: `Configuration/GridStyleConfigMissingError.cs`, `GridStyleSectionMissingError.cs`, `GridStyleOrientationInvalidError.cs`, `GridStyleKeyMissingError.cs`, `GridStyleHexColorInvalidError.cs` (namespace `SemiStep.Core.Configuration`).
- Modify: `Configuration/Validation/GridStyleValidator.cs` (emit the typed errors at :19/:24/:185/:197/:232/:238 instead of `Result.Fail(string)` / `new Error(string)`).
- Modify: `StyleEditor/GridStyleEditorViewModel.cs` (:193 and :297 — route through `ReasonLocalizer`).
- Modify: `ReasonLocalizer.cs` (5 arms + `SemiStep.Core.Configuration` using), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (5 keys), `CoreErrorLocalizationCoverageTests.cs` (5 samples).

- [x] Create the 5 validator error classes `public sealed`, BOM, empty-brace leaf shape; field-carrying ones expose their fields as `public` properties (`Section`, `Key`, `Value`, and for orientation `Value`/`ExpectedRows`/`ExpectedColumns`). Base message == the current baked string (byte-identical en).
- [x] `GridStyleValidator`: replace each `Result.Fail("…")` / `errors.Add(new Error("…"))` with the typed error — `:19` → `GridStyleConfigMissingError`, `:24` → `GridStyleSectionMissingError("colors")`, `:185` → `GridStyleOrientationInvalidError(orientation, GridOrientationValues.RowsAsSteps, GridOrientationValues.ColumnsAsSteps)`, `:197` → `GridStyleSectionMissingError(sectionPath)`, `:232` → `GridStyleKeyMissingError(sectionPath, keyName)`, `:238` → `GridStyleHexColorInvalidError(sectionPath, keyName, value)`. The `List<IError>` accumulation and `Result.Fail(errors)` shape stays (multiple typed errors join at the seam).
- [x] `GridStyleEditorViewModel` `:193` and `:297`: `string.Join("; ", result.Errors.Select(error => error.Message))` → `string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize))`. Add the `SemiStep.UI.Localization` using if needed (same assembly). Keep it inline — two call sites do not warrant an extension helper (KISS).
- [x] `ReasonLocalizer`: 5 arms — the fieldless/single-field ones bare or `Format(...)`; `GridStyleOrientationInvalidError e => Format(Resources.ErrorGridStyleOrientationInvalid, e.Value, e.ExpectedRows, e.ExpectedColumns)` (values off the error, NOT `GridOrientationValues` — see the orientation note); `GridStyleHexColorInvalidError e => Format(Resources.ErrorGridStyleHexColorInvalid, e.Section, e.Key, e.Value)`; etc. Add the `SemiStep.Core.Configuration` using.
- [x] resx: 5 keys per the table (en/ru) + Designer accessors. Coverage: 5 samples (`GridStyleSectionMissingError("colors")`, `GridStyleHexColorInvalidError("colors.cells", "depth_0", "zzz")`, etc.). New files BOM; Designer/resx as-is.
- [x] **Test blast radius:** grep the Tests tree for the raw strings (`missing 'colors' section`, `invalid hex color`, `is missing or empty`, `unknown value`). The actual validator-test files are `GridStyleColorsValidationTests.cs` and `GridStyleOrientationTests.cs` (there is no `GridStyleValidatorTests`); their asserts are `.Message.Contains(fragment)` and the typed errors keep byte-identical base messages, so **they pass unchanged** — Task 1's validator-test churn is near zero. Optionally convert a few `.Message.Contains` to `HasError<T>()` for durability (polish, not required). Localized-editor assertions under ambient ru → `ResourcesCultureScope.Use("en")`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Type the file-I/O errors (Rule-B envelopes) + the save-exception path

Finishes the surface: the loader/writer file-I/O errors localize by type, the raw `ex.Message` rides `CausedBy` +
a log line instead of the headline.

**Files:**
- Create: `Configuration/GridStyleConfigNotFoundError.cs`, `GridStyleLoadFailedError.cs`, `GridStyleSaveFailedError.cs`.
- Modify: `Configuration/Loaders/GridStyleLoader.cs` (:23/:30 → `GridStyleConfigNotFoundError`; :41 → `GridStyleLoadFailedError(file).CausedBy(ex)`), `Configuration/Loaders/GridStyleWriter.cs` (:39 → `GridStyleSaveFailedError(file).CausedBy(ex)` — note: `GridStyleWriter` lives under `Loaders/`).
- Modify: `StyleEditor/GridStyleEditorViewModel.cs` (`LoadAsync`/`Save` exception-sink; `:305-308 ReportSaveException`), and the startup error handler (`Program.cs` / the startup facade-error loop — the second `ExceptionalError` sink).
- Modify: `ReasonLocalizer.cs` (3 arms), resx trio (3 keys), coverage test (3 samples — `GridStyleConfigNotFoundError` sample uses the **file** form, e.g. `.../ui/grid_style.yaml`).

- [x] Create the 3 file-I/O errors `public sealed`, BOM. `GridStyleConfigNotFoundError(string path)` is a leaf (`Grid style config not found: {path}`). `GridStyleLoadFailedError(string fileName)` / `GridStyleSaveFailedError(string fileName)` are Rule-B envelopes — base message `Failed to load/save {fileName}` (NO `: {ex.Message}`; the exception rides `CausedBy`).
- [x] `GridStyleLoader`: `:23`/`:30` → `Result.Fail(new GridStyleConfigNotFoundError(uiDir/filePath))`; `:41` catch → `Result.Fail(new GridStyleLoadFailedError(Path.GetFileName(filePath)).CausedBy(ex))`. `GridStyleWriter:39` catch → `Result.Fail(new GridStyleSaveFailedError(Path.GetFileName(filePath)).CausedBy(ex))`.
- [x] **Diagnostics sink for the dropped `ex.Message` — TWO consumers, both must log the `CausedBy` exception.** `GridStyleLoader`/`GridStyleWriter` are logger-less internal helpers (`LoadAsync` is static; `GridStyleWriter` is `new()`-ed inside the facade), so threading `ILogger` through them is a structural change this slice avoids — the sink lives at each consumer. **The `ExceptionalError` is nested under `CausedBy`, not top-level**, so extraction walks one level: `result.Errors.SelectMany(e => e.Reasons).OfType<ExceptionalError>()`.
  - **Editor consumer** — `GridStyleEditorViewModel.LoadAsync`/`Save` (has `_logger`): after setting the localized `ErrorMessage`, log any `ExceptionalError` via `_logger.LogWarning(ex, "Grid style load/save failed")`.
  - **Startup consumer** — `GridStyleLoader`/`GridStyleValidator` are ALSO on the startup path: `ConfigFacade.LoadAndValidateAsync` (`ConfigFacade.cs:~86`) calls the same loader/validator, consumed by `Program.cs`/`StartupAsync`, which today logs `error.Message` only. After the Rule-B tail drop, a YAML parse error's detail would land NOWHERE at startup (the editor sink never runs on that path). Task 2 MUST add the same `ExceptionalError` sink to the startup error loop — `Log.Error(ex, "…")` for each `error.Reasons.OfType<ExceptionalError>()`. Read `Program.cs`/the startup facade-error handler to place it. (The typed validator errors are byte-identical `.Message` there, so startup display is unchanged; only the load-failed exception detail needs the new sink.) This is a real regression if omitted — do not skip it.
- [x] `ReportSaveException` (:305-308): **commit to `Resources.SaveFailed` alone** — `ErrorMessage = Resources.SaveFailed;` (drop the `: {exception.Message}` tail), keep the existing `_logger.LogError(exception, ...)`. Do NOT route through `GridStyleSaveFailedError`: exceptions reaching `SaveCommand.ThrownExceptions` are precisely the ones the writer did NOT catch (the writer catches file-I/O into `Result.Fail`), so they come from `BuildRecord`/`HexColor.ToHex`/the command pipeline — before any file is written — and naming `grid_style.yaml` would be a lie. `Resources.SaveFailed` (en "Save failed" / ru "Не удалось сохранить") is already localized, so this path just drops the raw exception tail from the UI (it stays in the log). **`SaveFailed` is a shared resx key** (also used by the recipe-save/MainWindow flow) — do NOT change its resx value, only the view-model's use of it here.
- [x] `ReasonLocalizer`: 3 arms. resx: 3 keys (en/ru) + Designer. Coverage: 3 samples (`GridStyleLoadFailedError("grid_style.yaml")`, etc.).
- [x] **Test blast radius:** grep for `config not found`, `Failed to load`, `Failed to save`, **and `SaveFailed`** (the resx-symbol form — `GridStyleEditorViewModelTests` asserts `$"{Resources.SaveFailed}: disk gone"` / `": boom"` around :277/:296, which the string-fragment grep misses; the `:308` change breaks them → update to assert `Resources.SaveFailed` without the tail). Also any `GridStyleLoaderTests`/`GridStyleWriterTests`/`LoaderFailLoudTests`/facade test asserting those `.Message` strings — the Rule-B headlines dropped the `: {ex.Message}` tail (asserts on the tail move to `CausedBy`/the exception; asserts on `"Failed to load" + filename` survive). ru editor assertions → scope en.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 3: Cross-path tests + doc + verify

**Files:** `ReasonLocalizerTests.cs`, the style-editor test home (`GridStyleEditorViewModelTests` / `GridStyleEditorFacadeTests`), `Docs/architecture/error-reporting.md` (and `grid-style-configuration.md` if it documents the error strings).

- [x] ru/en render cases for all 8 types (the field-carrying ones with representative args — `GridStyleHexColorInvalidError("colors.cells", "depth_0", "zzz")` → the composed ru; `GridStyleOrientationInvalidError` → the two expected values) + the en `Localize == .Message` pins (Rule-B ones: `Localize == base headline`, sans exception).
- [x] **End-to-end payoff:** drive the editor's `LoadAsync`/`Save` with a malformed `grid_style.yaml` (or a facade result carrying a typed validation error) and assert the `ErrorMessage` renders Russian under `ru` — e.g. an invalid hex color surfaces `Недопустимый цвет в «…»: «zzz»…`. Read the existing `GridStyleEditorViewModelTests` fixture for the load/save harness (overlay a bad file per the invalid-config overlay pattern in CLAUDE.md).
- [x] **CausedBy preservation:** a `GridStyleLoadFailedError`/`GridStyleSaveFailedError` retains its exception on `.Reasons` (the `ExceptionalError` from `CausedBy`) — assert the load/save-failed path carries the original exception.
- [x] fragment sweep across the Tests tree for all Task 1/2 fragments; confirm only the flagged sites changed; report what moved.
- [x] `Docs/architecture/error-reporting.md`: the style-editor error surface now localizes by type (validation + file I/O); note the two Rule-B envelopes (`CausedBy(ex)` + log, headline localizes) and that the **startup** config-load boundary remains the sole English-by-design surface (the editor surface no longer is). Update the "still free-text"/producer list.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

After this slice the error-localization roadmap's operator surfaces are fully typed; the only remaining English-by-design
surface is the **startup** config load (before the UI culture is established), plus the two PLC malformed-wire
diagnostic edges (short protocol-version read, codec decode-Fail) documented in `error-reporting.md`. Issue #118 (the
`GridStyleEditorViewModel` structural refactor — declarative field map, async `Save`, `HexColor.Parse` removal) remains
open and independent; the three join sites this slice routes will be carried through that rewrite.

**Executed by exec:**
- branch: style-editor-errors-typed

## Verify it yourself

The behavior — style-editor errors render Russian under a Russian UI — has no reliable pure-manual repro without
hand-corrupting `ui/grid_style.yaml`; verify by the tests and the diff:

1. **All 8 producers localize by type + parity gates hold:**
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~Localization"` —
   `ReasonLocalizerTests` pins each of the 8 types' ru render (e.g. `GridStyleHexColorInvalidError("colors.cells","depth_0","zzz")` → the composed `Недопустимый цвет…`) and the en `Localize == .Message` pins; `CoreErrorLocalizationCoverageTests` fails if any of the 8 public errors lacks an arm/sample; `ResourceSyncTests` fails on any en/ru/Designer key or placeholder mismatch.
2. **End-to-end payoff (malformed file → Russian in the editor):**
   `dotnet test ... --filter "FullyQualifiedName~GridStyleEditorViewModelTests"` — `LoadAsync_MalformedHexColor` overlays a corrupted `changed_selected` on the shipped config, runs `LoadAsync` under `ru`, and asserts `ErrorMessage` is the exact Russian hex-color sentence (a regression to raw `.Message` yields English → fails).
3. **Rule-B exception detail survives the tail-drop (both sinks):** the same file's two log-sink tests assert `LoadAsync`/`Save` on a `CausedBy` failure emit a `LogLevel.Warning` whose `Exception` is the original — removing the VM's `LogCausedByExceptions` call empties the capture and fails them. The startup `Program.cs` `Log.Error` sink is covered by inspection (Serilog static `Log` is not capturable) and exercises the same nested `SelectMany(Reasons).OfType<ExceptionalError>()` walk.
4. **CausedBy preservation:** `dotnet test ... --filter "FullyQualifiedName~GridStyleEditorFacadeTests"` — an unparseable yaml yields `GridStyleLoadFailedError` with the exception retained on the nested `.Reasons`; a write-into-a-file-as-dir yields `GridStyleSaveFailedError` likewise.
5. **Whole suite:** `dotnet build SemiStep.slnx` (0 warnings) and `dotnet test` (1685 passed, 0 failed).
