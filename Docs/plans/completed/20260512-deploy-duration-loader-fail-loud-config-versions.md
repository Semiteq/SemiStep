# DeployDuration wiring, fail-loud loaders, connection version validators

## Overview

Three concrete fixes on the SemiStep `refactor/architecture-simplification` branch:

1. **DeployDuration regression** — `ActionDefinition.DeployDuration` is parsed and validated (accepts only `"immediate"` or `"longlasting"`) but never consulted by `TimingCalculator`. Intended semantics: `immediate` actions do not contribute to cumulative time; `longlasting` actions do. Field is currently a `string` everywhere; convert the domain side to an enum so the compiler enforces the two-value contract. Wire the lookup into `TimingCalculator`.
2. **Silent fallback on parse failure** — `ConnectionLoader` (and possibly other loaders) catches parse exceptions and returns `Result.Ok().WithWarning(default-config)`. This hides corrupt or unreadable config behind a default that the user never asked for. Sweep all loaders under `SemiStep.Core/Configuration/Loaders/`, replace every "catch → return Ok with default" with a hard failure that includes filename and exception message.
3. **Connection schema/driver version validation** — two YAML fields are deserialized but never validated:
   - `connection_file_version` (schema version, all files currently `"1.0"`)
   - `connection_protocol` (currently `"S7"` everywhere; semantically being repurposed to mean PLC driver implementation version, value `"1.0"` going forward; future S7 driver versions will live alongside the current one for backward compat)

   Add a strict equality check for both. Migrate every in-repo YAML from `connection_protocol: S7` to `connection_protocol: "1.0"`. Flag deployed configs at `C:\DISTR\Config\Semistep\` in Post-Completion notes — they need the same migration after this lands.

## Context (from discovery)

- **DeployDuration data flow:** `ActionDto.DeployDuration: string?` (YAML deserialize) → validated in `ActionsSectionLoader.cs:168-181` (strict `is "immediate" or "longlasting"`) → mapped to `ActionDefinition.DeployDuration: string` via `ActionMapper.cs:33` → not read anywhere downstream.
- **Cumulative time:** `TimingCalculator.Calculate(Recipe, IReadOnlyList<LoopInfo>)` at `SemiStep.Core/Recipes/Analysis/TimingCalculator.cs` iterates `recipe.Steps`, reads `step_duration` property on every step, and accumulates unconditionally. No reference to actions.
- **Step → Action linkage:** `Step.ActionKey: int`. Action definitions live in `RecipeMetadataRegistry` (already DI-registered), not on `Recipe` itself.
- **RecipeAnalyzer:** sealed class with parameterless implicit ctor; only DI consumer is whoever uses `Result<RecipeSnapshot> Analyze(Recipe)`. No registry injection today.
- **Connection silent-fallback:** `ConnectionLoader.cs:44-45` catches parse exceptions and returns `Result.Ok(PlcConfiguration.Default).WithWarning(...)`. Same anti-pattern in `GridStyleLoader.cs:36-47` (returns `Result.Ok<GridStyleOptionsDto?>(null).WithWarning(...)`) — verify and decide per-loader.
- **YAML connection values in repo:**
  - `connection_file_version: 1.0` (5 files)
  - `connection_protocol: S7` (5 files) ← to be migrated to `"1.0"`
  - Files: `ConfigFiles/connection/connection.yaml`; `SemiStep.Tests/YamlConfigs/{Standard,WithGroups,Invalid/BrokenManagingDbLayout,Standalone/UnknownYamlFields}/connection/connection.yaml`.
- **Test baseline:** 303/303 green at HEAD (`005bff0`). Pre-commit hook runs `dotnet format` verify-no-changes.

## Development Approach

- **Testing approach:** Regular — implementations are mechanical; existing test suite covers the affected pipelines (303 tests). Each task ends with a green `dotnet build` and `dotnet test`.
- Complete each task fully before moving to the next.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- New tests are mandatory for Tasks 1 and 3 (new behaviour). Task 2 only updates existing tests that expected the old silent-default semantics.
- Stage changes per task; commits land on `refactor/architecture-simplification`.

## Testing Strategy

- **Unit / integration tests** (existing): 303 tests in `SemiStep.Tests` cover all affected pipelines (DI graph via `CoreFixture`/`UiFixture`, YAML loading via `ConfigLoadingTests`, recipe analysis via `RecipeAnalyzerTests`, etc.). They must stay green or be updated where behaviour intentionally changes.
- **No e2e tests** in this project. UI smoke is manual.
- **New tests required:**
  - Task 1: `TimingCalculator` test asserting that an `Immediate` action's `step_duration` is excluded from cumulative time, and that a `LongLasting` action's `step_duration` is included. Bonus loop case if cheap. No test for the unknown-`ActionKey` path — it's a contract assertion (throws), not a runtime branch.
  - Task 2: every loader test that previously asserted `IsSuccess + warning` on corrupt input must flip to `IsFailed`. Add at least one new test per loader confirming the failure message includes the offending filename.
  - Task 3: three test cases per field — happy path (`"1.0"` accepted), unsupported value (`"S7"` or other mismatched string rejected with "Unsupported …" message), missing/whitespace (rejected with "Missing required field …" message). Six tests total, or two `[Theory]`-driven groups.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work — update if scope shifts.

## Solution Overview

Three independent fixes, one commit each:

- **Fix 1 (DeployDuration):** introduce enum `DeployDuration { Immediate, Longlasting }` in domain, keep DTO string for YAML, convert in mapper, inject `RecipeMetadataRegistry` into `RecipeAnalyzer`, pass an action-lookup function (or registry) into `TimingCalculator.Calculate`, short-circuit to `TimeSpan.Zero` for `Immediate`.
- **Fix 2 (fail loud):** sweep loaders, replace silent-default branches with `Result.Fail(...)`. Policy pre-decided per loader (see Technical Details > Fail-loud loader contract): `ConnectionLoader` fails on missing-dir, missing-file, and parse-failure. `GridStyleLoader` accepts missing-dir/missing-file as legitimate "no custom styles" (cosmetic config) but fails on parse-failure of a present file. Section loaders already fail loudly — verify only.
- **Fix 3 (versions):** `ConnectionLoader` gets two private const strings `SupportedConnectionFileVersion = "1.0"` and `SupportedConnectionProtocol = "1.0"`. After deserialize, check both via simple equality, fail with explicit message including the offending value. Migrate all in-repo `connection.yaml` files from `S7` to `"1.0"`. Document the deploy-side migration in Post-Completion.

## Technical Details

### DeployDuration enum design

```csharp
// SemiStep.Core/Recipes/DeployDuration.cs
namespace SemiStep.Core.Recipes;

public enum DeployDuration
{
    Immediate,
    LongLasting
}
```

`ActionDto.DeployDuration` stays `string?` (YAML serialization round-trip).
`ActionDefinition.DeployDuration` becomes `DeployDuration` (non-nullable).
`ActionMapper.MapToDomain` converts string → enum via switch: `"immediate" → Immediate`, `"longlasting" → LongLasting`, `default → throw new InvalidOperationException`. Validator upstream has already filtered to these two values; the throw is a contract assertion.

### TimingCalculator signature change

```csharp
// before
public static (IReadOnlyDictionary<int, TimeSpan>, TimeSpan) Calculate(
    Recipe recipe,
    IReadOnlyList<LoopInfo> loops)

// after
public static (IReadOnlyDictionary<int, TimeSpan>, TimeSpan) Calculate(
    Recipe recipe,
    IReadOnlyList<LoopInfo> loops,
    RecipeMetadataRegistry registry)
```

Pass the registry itself rather than broadening its public API with a new `Actions` collection. `TimingCalculator` uses the existing `registry.GetAction(int)` accessor. Zero new public surface on the registry.

`RecipeAnalyzer` gets `RecipeMetadataRegistry` injected via primary ctor (DI resolves it automatically, no `RecipeDi.cs` change expected):

```csharp
public sealed class RecipeAnalyzer(RecipeMetadataRegistry registry)
{
    public Result<RecipeSnapshot> Analyze(Recipe recipe)
    {
        ...
        var (stepStartTimes, totalDuration) = TimingCalculator.Calculate(recipe, parsedLoops, registry);
        ...
    }
}
```

`ExtractStepDuration` short-circuits on `Immediate`. Unknown `ActionKey` is treated as a contract violation (upstream validation guarantees every step's action exists):

```csharp
private static TimeSpan ExtractStepDuration(
    Step step,
    RecipeMetadataRegistry registry)
{
    var action = registry.GetAction(step.ActionKey)
        ?? throw new InvalidOperationException(
            $"Step references unknown action id {step.ActionKey}");

    if (action.DeployDuration == DeployDuration.Immediate)
    {
        return TimeSpan.Zero;
    }

    // ... existing step_duration property extraction
}
```

The throw is consistent with `ActionMapper`'s defensive throw on unknown deploy-duration strings: both code paths trust upstream validation and fail hard if it didn't hold. No test added for the throw path — it's a contract assertion, not a runtime branch.

> ⚠️ During implementation: verify `registry.GetAction(int)`'s return type (nullable vs throws). If it already throws on unknown id, drop the `?? throw` and keep the call site clean. If it returns non-nullable but the value can still be "missing", use whatever sentinel the existing API uses.

### Fail-loud loader contract

`ConnectionLoader` has three silent-default branches today: missing-directory, missing-file, and parse-failure. Same shape applies to `GridStyleLoader`. Pre-decided per-loader policy:

| Loader | Missing dir | Missing file | Parse failure |
|---|---|---|---|
| `ConnectionLoader` (required) | **Fail** | **Fail** | **Fail** |
| `GridStyleLoader` (cosmetic) | Defaults OK | Defaults OK | **Fail** |
| Section loaders (Actions/Columns/Properties/Groups) | already fail | already fail | already fail |

Rationale: PLC connectivity cannot meaningfully default; missing or absent connection config is operator error. Grid styles are pure cosmetics — operator legitimately running without a style file is fine, but a *present-and-broken* style file is corruption that must surface.

Pattern to enforce on all "convert" branches:

```csharp
// missing dir / missing file
return Result.Fail($"Required config not found: {expectedPath}");

// parse failure
catch (Exception ex)
{
    return Result.Fail($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
}
```

Removed: `Result.Ok(default).WithWarning("Failed to parse ..., using defaults")` for the "Fail" cells above.

Kept (with explicit justification comment in code): missing-dir and missing-file paths in `GridStyleLoader`.

### Connection version validators

```csharp
internal sealed class ConnectionLoader
{
    private const string SupportedConnectionFileVersion = "1.0";
    private const string SupportedConnectionProtocol = "1.0";

    public async Task<Result<ConnectionDto?>> LoadAsync(string configDirectory)
    {
        // ... read + deserialize ...

        var fileVersionResult = ValidateVersion(
            "connection_file_version", dto.ConnectionFileVersion, SupportedConnectionFileVersion);
        if (fileVersionResult.IsFailed) return fileVersionResult;

        var protocolResult = ValidateVersion(
            "connection_protocol", dto.ConnectionProtocol, SupportedConnectionProtocol);
        if (protocolResult.IsFailed) return protocolResult;

        return Result.Ok(dto);
    }

    private static Result ValidateVersion(string fieldName, string? actualValue, string expected)
    {
        if (string.IsNullOrWhiteSpace(actualValue))
        {
            return Result.Fail($"Missing required field '{fieldName}'. Expected: '{expected}'.");
        }

        if (actualValue != expected)
        {
            return Result.Fail($"Unsupported {fieldName}: '{actualValue}'. Expected: '{expected}'.");
        }

        return Result.Ok();
    }
}
```

Two distinct error shapes: "Missing required field …" for null/whitespace, "Unsupported …: '<value>'" for mismatched. This keeps the operator's diagnostic message unambiguous (empty quotes for null would have been confusing).

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all code/YAML changes inside the repo.
- **Post-Completion** (no checkboxes): deployed configs at `C:\DISTR\Config\Semistep\` must be migrated by hand (`connection_protocol: "1.0"`). Manual UI smoke also lives here.

## Implementation Steps

### Task 1: Wire DeployDuration into cumulative-time calculation

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/DeployDuration.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/ActionDefinition.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/ActionMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/RecipeAnalyzer.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/TimingCalculator.cs`
- Touch (no changes expected): `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs` — verify `GetAction(int)` accessor exists; do not add an `Actions` collection.
- Modify: callers of `TimingCalculator.Calculate` (grep first; likely only `RecipeAnalyzer`)
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/Analysis/TimingCalculatorTests.cs` (path approximate; locate actual test file)
- Modify: any other test that constructs `ActionDefinition(...)` with a string `DeployDuration` — update to enum

- [x] Add `public enum DeployDuration { Immediate, LongLasting }` in a new file under `SemiStep.Core/Recipes/`.
- [x] Change `ActionDefinition.DeployDuration` from `string` to `DeployDuration` (non-nullable).
- [x] Update `ActionMapper.MapToDomain` to convert string → enum: `"immediate" → Immediate`, `"longlasting" → LongLasting`, `default → throw InvalidOperationException` (validator already filters; this is defensive).
- [x] Convert `RecipeAnalyzer` to a primary-ctor class taking `RecipeMetadataRegistry`. No DI registration change expected — constructor-injection-only types resolve automatically.
- [x] Change `TimingCalculator.Calculate` signature to take a third parameter `RecipeMetadataRegistry registry`. Plumb it from `RecipeAnalyzer.Analyze`. Use the existing `registry.GetAction(int)` accessor — do not broaden the registry's public surface with a new `Actions` collection.
- [x] Update `TimingCalculator.ExtractStepDuration` to look up the step's action via `registry.GetAction(step.ActionKey)`. If the action's `DeployDuration` is `Immediate`, return `TimeSpan.Zero`. If `GetAction` returns null (or otherwise indicates "missing"), throw `InvalidOperationException` — upstream validation guarantees every step's action exists, this is a contract assertion.
- [x] Update all callers of `TimingCalculator.Calculate` to pass the new parameter.
- [x] Update every test fixture that constructs `ActionDefinition` with a string `DeployDuration` to use the enum.
- [x] Write a new unit test: two-step recipe, first step uses an `Immediate` action with YAML token `"immediate"` (enum `Immediate`) and `step_duration=10s`, second uses a `LongLasting` action with YAML token `"longlasting"` (enum `LongLasting`) and `step_duration=20s`. Verify the `stepStartTimes` dictionary semantics (start-of-step vs end-of-step) by reading existing tests first, then assert: index 0 starts at `TimeSpan.Zero`, index 1 starts at `TimeSpan.Zero` (immediate contributed nothing), `totalDuration = 20s`.
- [x] Write a second unit test for the loop case: a 3-step loop body (`Immediate`, `LongLasting`, `Immediate`) repeated 4 times. Assert per-iteration delta equals `step_duration` of the `LongLasting` step, and `totalDuration` after 4 iterations matches `4 * delta`.
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303+/303+ green.

### Task 2: Make config loaders fail loudly on parse errors

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/ConnectionLoader.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/GridStyleLoader.cs` (after confirming styles should also fail loudly; otherwise leave a justification comment and skip)
- Modify: any other loader using the silent-default pattern (grep `Result.Ok(.*WithWarning` across `Configuration/Loaders/`)
- Modify: tests that asserted silent-default semantics. Locate via `grep "WithWarning.*Failed to parse"` then trace test fixtures.

- [x] Grep `SemiStep/SemiStep.Core/Configuration/Loaders/` for both silent-default shapes: `Result.Ok.*WithWarning` and the trio of "missing-dir / missing-file / parse-failure" early returns. Enumerate every match. Apply the pre-decided policy from Technical Details:
  - `ConnectionLoader`: **all three branches** (missing dir, missing file, parse fail) → `Result.Fail(...)` with descriptive message.
  - `GridStyleLoader`: missing dir / missing file → keep `Result.Ok(default).WithWarning(...)` and add a one-line code comment justifying why styles are optional. Parse failure on a present file → `Result.Fail(...)`.
  - Section loaders (Actions/Columns/Properties/Groups): verify they already fail loudly on all three branches; if not, fix to match the table.
- [x] Update tests that previously expected `IsSuccess + warning` on corrupt YAML — flip them to expect `IsFailed` with a message containing the filename. List the affected tests below.
- [x] Add at least one new test per converted loader: feed it a deliberately-broken YAML (e.g. unparseable token), assert `Result.IsFailed` and that the error message contains the filename.
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` green.

### Task 3: Validate connection_file_version and connection_protocol

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/ConnectionLoader.cs`
- Modify: `ConfigFiles/connection/connection.yaml` — change `connection_protocol: S7` → `connection_protocol: "1.0"`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/connection/connection.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/connection/connection.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/BrokenManagingDbLayout/connection/connection.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/UnknownYamlFields/connection/connection.yaml`
- Modify: tests for ConnectionLoader (locate; add cases for unsupported version and unsupported protocol)
- Modify: any test that programmatically constructs a `ConnectionDto` without setting both fields — set them to `"1.0"`

- [x] Add `private const string SupportedConnectionFileVersion = "1.0";` and `private const string SupportedConnectionProtocol = "1.0";` to `ConnectionLoader`.
- [x] Add private static helper `ValidateVersion(fieldName, actualValue, expected)` per the Technical Details template — distinct error shapes for null/whitespace ("Missing required field") vs mismatched ("Unsupported …: '<value>'").
- [x] After deserialize, call `ValidateVersion` for both `connection_file_version` and `connection_protocol`. Short-circuit on failure.
- [x] Migrate all five in-repo `connection.yaml` files to `connection_protocol: "1.0"`. Confirm `connection_file_version` is already `"1.0"` in all of them.
- [x] Grep for programmatic `ConnectionDto(...)` instantiations in tests and fixtures; ensure they pass `"1.0"` for both fields.
- [x] Write tests: `ConnectionLoader_RejectsUnsupportedFileVersion`, `ConnectionLoader_RejectsMissingFileVersion`, `ConnectionLoader_RejectsUnsupportedProtocol`, `ConnectionLoader_RejectsMissingProtocol`, `ConnectionLoader_AcceptsSupportedVersions`. Each loads via the existing loader infrastructure (no shortcutting the deserialize path) and asserts `IsFailed`/`IsSuccess` with explicit reason text (covering both "Unsupported …" and "Missing required field …" message shapes).
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` green.

### Task 4: Verify acceptance criteria

- [x] Confirm `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` is green and the test count has increased by the new tests added in Tasks 1–3.
- [x] Run `git diff master..HEAD --stat` and review scope — confirm only the planned areas changed; no incidental edits.
- [x] manual UI smoke (skipped - not automatable)

### Task 5: Archive plan and update documentation

- [x] Move this plan to `Docs/plans/completed/`.
- [x] Update `Docs/07-non-functional.md` (or the existing migration-notes section added in Round 2) with:
  - `connection_protocol` field semantic change: now PLC driver implementation version (string `"1.0"`); legacy `"S7"` value rejected.
  - Loaders now fail loudly on corrupt config — operators will see explicit error rather than silently using defaults.
- [x] Update `CLAUDE.md` only if a new convention emerged (none expected — both changes are bug fixes within existing patterns). No new convention emerged; CLAUDE.md left unchanged.

## Post-Completion

**Manual verification:**
- Launch the UI against an external config and confirm the recipe grid still renders and cumulative time looks correct (especially: steps using `immediate` actions should not advance the start-time column).

**External system updates (REQUIRED before deploying):**
- ⚠️ **Deployed file `C:\DISTR\Config\Semistep\connection\connection.yaml` must be migrated:** change `connection_protocol: S7` to `connection_protocol: "1.0"`. After this lands, any config with the old value will fail loader validation with a clear "Unsupported connection_protocol" message. This is a hard break — operators must migrate before upgrading.
- Verify the same deployed file already has `connection_file_version: "1.0"` (expected — same as in-repo fixtures).
- Loader behaviour change: previously, a corrupt or unreadable `connection.yaml` would silently fall back to `PlcConfiguration.Default`. After this lands, the same situation will surface as a hard failure with the filename and parse error in the message. Operators encountering this for the first time should treat it as a real configuration error to fix, not as a regression.
