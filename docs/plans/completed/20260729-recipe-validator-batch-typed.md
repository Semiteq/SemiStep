# Recipe-value validation gate — typed batch + positional decorators (localization slice 3)

## Overview

`ImportedRecipeValidator` is the shared validation gate every untrusted recipe passes through — paste,
CSV file-load, and PLC-read all call it after deserialization. It accumulates a `List<string>`,
stringifying every inner error under two positional prefixes (`"Step {n}: "` and `"Property/Group
'{key}': "`), and at one point discards a typed error outright (reads only `.IsFailed`). That single
class defeats localization for the entire recipe-value surface: its output reaches `ReportFailure`
(routed through `ReasonLocalizer` since #151), but the reasons arrive as plain `Error` strings the
type-switch can't match, so every path renders English regardless of culture.

This slice de-launders the gate: `List<string>` → `List<IError>`, positions carried by **typed
decorators** (`AtStep`/`AtColumn`) instead of baked prefixes, and inner errors **preserved** (via the
decorator's `Inner`) instead of stringified. It also adds the decorator-**composition** mode to
`ReasonLocalizer` — the half the mechanism PR deferred (the resolver shipped only the fallback
recursion). It is the roadmap's slice-3 prerequisite, standalone, before the recipe/clipboard/CSV waves.

**Interim result (honest):** positions localize now; the inner *detail* stays English until the recipe
wave (slice 4) types the value errors (`PropertyValidator`, `GroupHasIntKey`, …). Under `ru` a gate error
reads e.g. `"Шаг 3: Столбец 'gas': value 5 is out of range"` — Russian position, English detail. This is
strictly better than today's all-English, and it's the stepping stone the roadmap names.

**Not behavior-preserving for English** (unlike the mechanism PR): the positional wording becomes uniform
(`"Property 'x'"` / `"Group property 'x'"` → `"Column 'x'"`), so a handful of existing gate-validation
assertions update to the new English. The *inner detail* text is preserved verbatim.

## Mechanism

### Positional decorators (Core — `SemiStep.Core/Recipes/Errors/`)

Typed `Error` subclasses that add positional context and delegate the sentence to an inner reason. Not
discriminators — position is data, the sentence is the inner error.

```csharp
public sealed class AtStepError(int stepNumber, IError inner)
    : Error($"Step {stepNumber}: {inner.Message}")           // English baked for the log
{
    public int StepNumber { get; } = stepNumber;             // 1-based, as displayed
    public IError Inner { get; } = inner;
}

public sealed class AtColumnError(string columnKey, IError inner)
    : Error($"Column '{columnKey}': {inner.Message}")
{
    public string ColumnKey { get; } = columnKey;
    public IError Inner { get; } = inner;
}
```

### `ReasonLocalizer` composition cases (UI)

The mechanism PR's `TryLocalize` switch handles leaf types + a fallback recursion. Decorators **compose**
— the case localizes its own template with the localized inner as an argument:

```csharp
AtStepError e   => Format(Resources.AtStepFormat,   e.StepNumber, Localize(e.Inner)),
AtColumnError e => Format(Resources.AtColumnFormat,  e.ColumnKey,  Localize(e.Inner)),
```

Nesting composes naturally: `Localize(AtStep(3, AtColumn("gas", inner)))` →
`Format(AtStepFormat, 3, Format(AtColumnFormat, "gas", Localize(inner)))`. `Localize(inner)` on a
still-free-text inner falls through to `inner.Message` (English) — the interim detail.

resx: `AtStepFormat` = `"Step {0}: {1}"` (ru `"Шаг {0}: {1}"`); `AtColumnFormat` = `"Column '{0}': {1}"`
(ru `"Столбец '{0}': {1}"`). Designer accessors; parity held.

`AtStepError`/`AtColumnError` are public Core `Error` subclasses, so the mechanism PR's reflection
coverage test **forces** their `ReasonLocalizer` cases — a missing case is a red build. Seed the test's
`TypeData` map with a constructed sample of each (inner = a plain `Error`).

### The gate rework (`ImportedRecipeValidator`)

`List<string>` → `List<IError>`. Wrap each step's errors in `AtStepError(stepNumber, inner)`; wrap each
column error in `AtColumnError(column.Key, inner)`. **Preserve the typed inner** where a sub-result
already carries one — do NOT stringify:
- `PropertyValidator.Validate` failures: `foreach (var error in validationResult.Errors) errors.Add(new AtColumnError(column.Key, error));` (was `$"Property '{key}': {error.Message}"`).
- `GetProperty` failure: `new AtColumnError(column.Key, propertyDefResult.Errors[0])` (was a `string.Join` of messages).
- `GroupHasIntKey` failure: capture the result and forward its error — `var groupResult = registry.GroupHasIntKey(...); if (groupResult.IsFailed) errors.Add(new AtColumnError(column.Key, groupResult.Errors[0]));` (was `.IsFailed`-only + a fabricated string).
- The validator's **own** directly-raised messages stay free-text `new Error(text)` (typed later), wrapped in the right decorator — and their text **drops the position the decorator now carries**, so nothing is duplicated: `new Error($"Unknown action ID {step.ActionKey}")` → `AtStepError` only (step-level, no column); `new Error($"Group value must be integer, got {type}")` (NOT `"Group property '{key}' must be integer…"`, since `AtColumnError` already prepends `"Column '{key}': "`) → `AtColumnError(column.Key, …)`.

`Validate` returns `Result.Fail(errors)` (a `List<IError>`); the batch stays intact and each reason
localizes independently at the sink.

## Scope

**In:** the two decorators; the `ReasonLocalizer` composition cases + resx + coverage samples; the
`ImportedRecipeValidator` rework; tests.

**Out (later slices):** typing the inner value errors — `PropertyValidator`, `PropertyParser`,
`RecipeMetadataRegistry.GroupHasIntKey`, the unknown-action/must-be-integer raises (recipe wave, slice 4;
once typed, they localize through the already-placed decorators with **no** gate change); the clipboard/CSV
*deserialize-time* value errors at `ClipboardSerializer.cs:191` / `CsvRowConverter.cs:89` (slice 5 — a
separate parse surface, not this post-deserialize gate); PLC/style-editor.

Note: an invalid paste can interleave both surfaces in one failure — this gate's localized-position
errors alongside the still-all-English deserialize errors (`ClipboardSerializer`/`CsvRowConverter`). That
is the accepted interim until slice 5, not a defect.

## Context (grounded on current master, post #151/#152/#153)

- `SemiStep.Core/Recipes/Helpers/ImportedRecipeValidator.cs` — the full gate (read the file): `Validate`
  (`:8-27`) `List<string>` + `$"Step {n}: {error}"`; `ValidateStep` (`:29-60`) `"Unknown action ID"`;
  `ValidateGroupColumn` (`:62-78`) `"Group property '{key}' must be integer…"` + the discarded
  `GroupHasIntKey` typed error (`:73`); `ValidatePropertyColumn` (`:80-100`) `$"Property '{key}': …"`.
- Consumers (all reach `ReportFailure`, routed): `RecipeSession.LoadAsCurrentValidated` →
  `RecipeCoordinator.LoadRecipeAsync` → `RecipeFileViewModel.LoadRecipeAsync` `ReportFailure`; the paste
  path → `ClipboardViewModel` `ReportFailure(recipeResult, Resources.PasteStepFailed)` (routed as of #115);
  the PLC-read path → `MainWindowViewModel:249` `ReportFailure`. Injected also at `PlcLifecycleManager.cs`.
- `ReasonLocalizer.cs` (from #151) — `TryLocalize` switch + `is IError` fallback recursion; the mechanism
  plan's forward note already anticipates these decorator composition cases.
- Coverage test: `SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs` — reflects public
  non-abstract Core `Error` subclasses; the two decorators join it (add samples).
- resx parity enforced by `ResourceSyncTests`; the two new keys extend it. `Warning` is untouched here
  (no warnings in this gate).

## Development Approach

- Regular (code, then tests). `dotnet build SemiStep.slnx` 0 warnings; `dotnet test` green after each task.
- Two new resx keys with EN + first-pass RU + designer accessors; keep resx counts equal.
- Tests set `ru` via `ResourcesCultureScope`. The existing gate tests mostly use fragment `Contains`
  assertions that survive the new wording — run them and fix only real failures, not a blanket rewrite.

## Acceptance Evidence

**Automatable:**
1. **Decorator composition:** under `ru`, `ReasonLocalizer.Localize(new AtStepError(3, new AtColumnError("gas", new Error("bad"))))` == `"Шаг 3: Столбец 'gas': bad"` (position localized, inner English via fallback); under en == `"Step 3: Column 'gas': bad"`.
2. **Gate emits typed decorated reasons:** `ImportedRecipeValidator.Validate` on a recipe with a bad group value / failed property validation returns `Result.Fail` whose `Errors` are `AtStepError` wrapping `AtColumnError` wrapping the **preserved inner** (assert the inner is the original `IError`, not a fabricated string) — including the `GroupHasIntKey` path that previously discarded its error.
3. **End-to-end (paste or load) under ru:** a recipe-value validation failure surfaces to a real
   `MessagePanelViewModel` via `ReportFailure` with the Russian position + English detail.
4. **Coverage:** the reflection coverage test is green with the two decorators mapped, and goes red if a
   decorator case is removed.
5. resx parity green.

**Manual smoke:** under `ui.locale=ru`, paste / load a recipe with an out-of-range value — the panel shows
`"Шаг N: Столбец '…': <english detail>"`.

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: Decorators + ReasonLocalizer composition cases + resx

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/Errors/AtStepError.cs`, `AtColumnError.cs`
- Modify: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs`, `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs` (add a `TypeData` sample per decorator); `SemiStep/SemiStep.Tests/UI/Localization/ReasonLocalizerTests.cs` (composition unit tests — same `ResourcesCultureScope.Use(...)` + `.Should().Be(...)` pattern as its existing cases)

- [x] add `AtStepError(int stepNumber, IError inner)` and `AtColumnError(string columnKey, IError inner)` (public sealed, `Inner`/`StepNumber`/`ColumnKey` props, English `.Message` baked).
- [x] add `AtStepFormat`/`AtColumnFormat` resx keys (EN + RU) + a hand-written `Resources.Designer.cs` accessor for each (`public static string X => ResourceManager.GetString("X", resourceCulture) ?? string.Empty;` — the designer is hand-maintained, no regeneration step); keep the two resx counts equal.
- [x] add the two composition cases to `ReasonLocalizer.TryLocalize` (`Format(template, position, Localize(Inner))`).
- [x] seed `CoreErrorLocalizationCoverageTests` `TypeData` with a sample of each decorator (inner = plain `Error`); in `ReasonLocalizerTests.cs` add composition tests (acceptance #1): single, NESTED (`AtStep`→`AtColumn`→inner) under en/ru, and inner-fallback for a free-text inner.
- [x] `dotnet build SemiStep.slnx` (0 warnings) + `--filter` green.

### Task 2: Rework `ImportedRecipeValidator` to `List<IError>` + decorators

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/ImportedRecipeValidator.cs`
- Modify: `SemiStep/SemiStep.Tests/Domain/Unit/ImportedRecipeValidatorTests.cs` (fragment asserts survive — run/fix only failures, then add the new assertions below)
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelReportingTests.cs` (or the recipe-load/paste test that already drives the gate) — the ru end-to-end

- [x] `Validate`/`ValidateStep`/`ValidateGroupColumn`/`ValidatePropertyColumn`: `List<string>` → `List<IError>`; wrap steps in `AtStepError`, columns in `AtColumnError`; **preserve** the typed inner from `PropertyValidator`/`GetProperty`/`GroupHasIntKey` (do not stringify; forward the `IError`); keep the validator's own direct raises as free-text `new Error(text)` wrapped in the right decorator; `Result.Fail(errors)`.
- [x] run the suite; the existing gate assertions use fragment `Contains` checks (step number, column key, detail fragments) that SURVIVE the new `"Step N: Column 'k': <detail>"` shape — fix ONLY actual failures (expect near-zero). The one repo-wide exact `"Step 1:"` match is `ResultReportingExtensionsTests.cs:85` (synthetic, not the gate — verify unaffected).
- [x] add tests (acceptance #2, in `ImportedRecipeValidatorTests.cs`): the gate's reasons are `AtStepError`→`AtColumnError`→inner, and the inner is the ORIGINAL `IError` object (not a fabricated string) — for the `PropertyValidator`, `GetProperty`, AND `GroupHasIntKey` paths (the last previously discarded its error at the old `:73`).
- [x] end-to-end (acceptance #3): under `ru` (`ResourcesCultureScope`), a gate failure surfaced via `ReportFailure` to a real `MessagePanelViewModel` shows the Russian position + English detail.
- [x] build + `--filter` green.

### Task 3: Verify + document

**Files:**
- Modify: `Docs/architecture/error-reporting.md` (or `ui-localization.md`) — the decorator composition mode

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`; `dotnet build` 0 warnings; `dotnet format`.
- [x] confirm coverage + parity tests green.
- [x] document the decorator composition mode (`AtStep`/`AtColumn` compose position + `Localize(inner)`; the fallback recursion is for untyped wrappers) and the interim "position localized, detail English until slice 4" state.
- [x] mark this plan for archival at delivery (do NOT move it mid-run).

## Post-Completion

**Next (slice 4, recipe wave):** type the inner value errors (`PropertyValidator`, `PropertyParser`,
`RecipeMetadataRegistry.GroupHasIntKey`, unknown-action/must-be-integer) — they then localize through the
decorators placed here with no gate change — plus the recipe-edit/analysis errors and `LoopParser`
warnings (unseal `Warning`). Then slice 5 (clipboard/CSV deserialize surface), slice 6 (#120 PLC).

---

**Executed by exec:**
- branch: recipe-validator-batch-typed
- commits: be2dc53 (decorators) · 6116f0d (validator rework) · 1a8d8d5 (doc) · b65f4ae (pin raises) · 44f313f (ru guillemets)
- review chain: comprehensive (5 agents, ACHIEVED) → smells (guillemets fix) → comment audit (clean) → critical ×2 (no major/critical). codex phase skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — expect 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — expect 1565 passed, 0 failed.
3. Localization composition: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ReasonLocalizer"` — the ru cases render `Шаг {n}: Столбец «{key}»: {detail}`.
4. Coverage guard: `--filter "FullyQualifiedName~CoreErrorLocalizationCoverage"` — AtStepError/AtColumnError each force a ReasonLocalizer case.
5. Validator inner-preservation: `--filter "FullyQualifiedName~ImportedRecipeValidator"` — the PropertyValidator / GetProperty / GroupHasIntKey paths forward the original typed IError, wrapped in AtStep→AtColumn decorators.
