# Configuration cleanup, PLC validation pipeline, and logging foundation

## Overview

After the 20260428 architecture-simplification refactor, three rough edges
remain. They share an underlying theme — "errors are visible, properly
classified, and traceable" — and are bundled into one plan:

1. **Config-error regression.** An invalid PLC layout in YAML produces an
   `ArgumentException` from `ManagingAreaCodec`'s constructor during DI
   resolution. The exception is caught by `catch(Exception)` in `Program.Main`
   and written as `Log.Fatal` with a stack trace. The existing error pipeline
   (`ConfigFacade` -> `Result.Fail` -> `ErrorWindow`) is bypassed entirely.
   The architectural smell: the codec validates configuration, which is not
   its responsibility.

2. **`Core/Configuration/` mixes concerns.** The folder currently contains
   (a) the YAML pipeline (Loaders/Dto/Mapping/Validation/Facade) — one
   cohesive feature; (b) domain definition types (`GroupDefinition`,
   `GridColumnDefinition`); (c) `ConfigRegistry` — a query API whose every
   method is about recipe metadata (no PLC, no style). The 20260428 plan
   already moved `ActionDefinition` and `PropertyDefinition` to
   `Core/Recipes/`, but the migration was incomplete.

3. **Logger.** All eleven files using Serilog rely on the static `Log.*` API,
   so `SourceContext` is never populated. The console and file sinks use
   different output templates. Timestamps are written with the local timezone
   offset (`+03:00`).

The goal: route config errors through the existing pipeline, draw a clean
boundary "Configuration = YAML pipeline only", and adopt the standard
`ILogger<T>`-via-DI pattern across production code.

## Context (from discovery)

**Files / components involved:**

- `SemiStep/Core/Configuration/` — current location of `ConfigRegistry`,
  `GroupDefinition`, `GridColumnDefinition`, plus the YAML pipeline (Loaders,
  Dto, Mapping, Validation, Facade).
- `SemiStep/Core/Plc/Configuration/` — `PlcConfiguration` already lives here;
  layout invariants (`ManagingDbLayout`, `DataDbLayout`, `ExecutionDbLayout`)
  are currently checked inside codec constructors instead of by a config
  validator.
- `SemiStep/Core/Plc/S7/Serialization/` — `ManagingAreaCodec`, `ArrayCodec`,
  `ExecutionStateCodec`.
- `SemiStep/UI/Program.cs`, `SemiStep/UI/App.axaml.cs` — bootstrap and the
  `ErrorWindow` pipeline.
- 11 files with `using Serilog;` and direct static `Log.*` calls (full list
  in Task 7).
- ~13 production consumers of `ConfigRegistry` plus 5 test helpers (affected
  by the rename).

**Related patterns:**

- `Core/Configuration/Validation/` already contains two static validators
  (`CrossReferenceValidator`, `DefaultValueValidator`), each returning
  `Result`. The new `PlcConfigurationValidator` follows the same shape but
  lives next to what it validates: `Core/Plc/Configuration/`.
- The 20260428 plan's testing convention: black-box / integration tests are
  the safety net; new unit tests are introduced only when integration tests
  cannot reach the new behavior path.
- `App.RunErrorWindow(IReadOnlyList<string>)` — existing mechanism for
  reporting startup errors as a window.
- `FluentResults.Result.Try(Func<T>, Func<Exception, IError>)` — already
  available in the codebase via the `FluentResults` package; converts
  throwing code into `Result<T>` without manual try/catch.

**Dependencies identified:**

- `Microsoft.Extensions.Logging` (must be added to `SemiStep.UI.csproj`).
- `Serilog.Extensions.Logging` (Serilog -> Microsoft.Extensions.Logging
  bridge).
- `Microsoft.Extensions.Logging.Abstractions` for `Core` (likely already
  transitive via `DependencyInjection.Abstractions`; verify).

## Development Approach

- **Testing approach: rely on existing integration tests.** The project
  follows the 20260428 plan's convention: black-box tests are the primary
  safety net. New unit tests are written only for genuinely new logic that
  integration tests cannot cover. In this plan that means one new test
  class — `PlcConfigurationValidatorTests`.
- Phase 1 (Configuration restructure) is purely structural: file moves,
  rename, namespace updates. Existing tests fail at compile time and are
  fixed with minimal namespace/type-name edits.
- Phase 3 (logger rollout) changes constructor signatures. Test fixtures
  adapt by registering `NullLoggerFactory` or by switching to
  `services.AddLogging(...)` in DI fixtures.
- Each task ends with `dotnet build` + `dotnet test` green before the next
  task starts.
- The plan file is updated in-place during execution (`[x]`, ➕, ⚠️).

## Testing Strategy

- **Unit tests** are written only for `PlcConfigurationValidator` — the only
  new logic with discrete, easily testable conditions (TotalSize >= maxOffset
  + fieldSize, non-negative offsets, etc.).
- **Integration tests** — `Tests/Core/Integration/*`, `Tests/Csv/Integration/*`,
  `Tests/Domain/*`, `Tests/UI/*` — remain the safety net. They should pass
  unmodified after Phase 1 (only `using` updates) and Phase 3 (only DI
  wiring updates in fixtures).
- **Manual smoke test** is mandatory for Phase 2: break `connection.yaml` and
  confirm an `ErrorWindow` is shown instead of a stack trace. Without this
  check the regression is not considered fixed.
- **e2e**: this is an Avalonia GUI project with no automated e2e
  (Playwright/Cypress). A manual GUI smoke-test (launch, load/save a recipe)
  follows each phase.

## Progress Tracking

- `[x]` for completed checklist items.
- ➕ prefix for tasks discovered during execution.
- ⚠️ prefix for blockers.
- If scope changes (for example, an extra codec with runtime throws turns
  up), update the plan in place rather than the conversation.

## Solution Overview

**Phase 1 — Configuration restructure (move domain types into Recipes):**

Goal: `Core/Configuration/` contains exactly the YAML pipeline plus the
aggregate it produces.

- Move `ConfigRegistry.cs` -> `Core/Recipes/RecipeMetadataRegistry.cs`
  (rename).
- Move `GroupDefinition.cs`, `GridColumnDefinition.cs` -> `Core/Recipes/`.
- Delete the unused method `ResolvePropertyType(Recipe, int, string)` —
  verified by grep, zero production call sites.
- `GridStyleOptions.cs` stays in `Core/Configuration/` — read-only UI config,
  nothing to redistribute.

**Phase 2 — PLC validation pipeline + ErrorWindow safety net:**

Goal: a layout error is reported through `ErrorWindow`.

- Create `Core/Plc/Configuration/PlcConfigurationValidator.cs`. Static class
  returning `Result`, accumulating every layout-invariant violation.
- Wire it into `ConfigFacade.LoadAndValidateAsync` after `MapToDomain`,
  following the pattern of the existing `xrefResult` / `defaultsResult`
  branches.
- Where the existing `ConfigFacade` wraps `MapToDomain` in a manual try/catch
  to convert exceptions into `Result.Fail`, replace it with
  `Result.Try(() => MapToDomain(...), ex => new Error("...").CausedBy(ex))`.
  This is idiomatic FluentResults and removes a layer of boilerplate.
- Remove the `Validate(layout)` method and constructor throw from
  `ManagingAreaCodec` (and equivalents in `ArrayCodec` /
  `ExecutionStateCodec` if any are found). By the time a codec is built,
  `PlcConfiguration` is already valid — that is the new invariant.
- In `Program.Main`, the catch-all path additionally calls
  `App.RunErrorWindow([ex.Message])` so the user sees a window rather than
  a silently exiting process if anything ever slips past validation.

**Phase 3 — `ILogger<T>` via DI + unified format:**

Goal: production classes receive `ILogger<T>` through their constructor;
bootstrap-only code uses `Log.ForContext<T>()`; the console and file sinks
share a single output template.

- Add `Microsoft.Extensions.Logging` and `Serilog.Extensions.Logging` to
  `SemiStep.UI.csproj`.
- In `Program.Main`:
  `services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false))`.
- Convert ten production classes: `using Serilog` ->
  `using Microsoft.Extensions.Logging`; constructor receives `ILogger<T>`;
  call sites change `Log.X` -> `_logger.LogX`.
- `ConfigFacade.cs` (static, runs before DI) keeps Serilog with one line:
  `private static readonly ILogger _logger = Log.ForContext(typeof(ConfigFacade));`.
  This is the documented bootstrap exception.
- `Program.Main` keeps `Serilog.Log.Fatal(...)` for the very earliest errors
  (before the logger pipeline is even configured).
- Single template for both sinks:
  `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}`.
- Test fixtures register `NullLoggerFactory.Instance` or call
  `services.AddLogging(...)` so DI can resolve the new constructors.

## Technical Details

### Phase 1 — naming and references

- `RecipeMetadataRegistry` lives in `SemiStep.Core.Recipes`. Method
  signatures stay identical except `ResolvePropertyType` is deleted.
- `GroupDefinition`, `GridColumnDefinition` live in `SemiStep.Core.Recipes`.
- `RecipeDi.cs:25` registration becomes
  `services.AddSingleton(sp => new RecipeMetadataRegistry(...))`.
- Consumer files swap `using SemiStep.Core.Configuration;` ->
  `using SemiStep.Core.Recipes;` for the moved types. Where the file already
  imports `SemiStep.Core.Recipes`, only the type rename matters.

### Phase 2 — PlcConfigurationValidator scope

Minimum scope (exactly what currently throws): layout invariants, per layout.

- `ManagingDbLayout`: `TotalSize >= RecipeLinesOffset + sizeof(int)`,
  `TotalSize > CommittedOffset`, all offsets non-negative.
- `DataDbLayout` (Int / Float / String): `DataStartOffset >= max(CapacityOffset, CurrentSizeOffset) + sizeof(int)`,
  all offsets non-negative. (Exact rules to be confirmed by reading
  `DataDbLayout.cs`.)
- `ExecutionDbLayout`: `TotalSize >= max(<all offsets>) + <field size>`,
  all offsets non-negative.

Each violation produces a separate `Result.Fail($"<DbName>.<Field> ({value}) must be at least <expected> ...")`.
Errors are aggregated (via `Result.Merge` or an explicit list) so the user
sees every problem at once instead of one per launch.

### Phase 2 — codec cleanup

`ManagingAreaCodec` becomes:

```csharp
internal sealed class ManagingAreaCodec(ManagingDbLayout layout)
{
    private readonly ManagingDbLayout _layout = layout;  // no Validate(...)
    // ... rest unchanged
}
```

The `Validate(...)` method is removed entirely. Audit `ArrayCodec` and
`ExecutionStateCodec` for any equivalent runtime throws based on layout
invariants and remove them.

### Phase 2 — Result.Try in ConfigFacade

Existing code:

```csharp
try
{
    var config = MapToDomain(...);
    Log.Information("Configuration loaded successfully");
    return Result.Ok(config).WithReasons(...);
}
catch (Exception ex)
{
    Log.Error("Failed to map configuration to domain: {message}", ex.Message);
    return Result.Fail<AppConfiguration>($"Failed to map configuration to domain: {ex.Message}");
}
```

Replace with `Result.Try`:

```csharp
return Result.Try(
        () => MapToDomain(...),
        ex => new Error($"Failed to map configuration to domain: {ex.Message}").CausedBy(ex))
    .Bind(config =>
    {
        _logger.Information("Configuration loaded successfully");
        return Result.Ok(config);
    })
    .WithReasons(loadResult.Reasons)
    .WithReasons(xrefResult.Reasons)
    .WithReasons(defaultsResult.Reasons);
```

Two upsides: idiomatic FluentResults composition and the original exception
is preserved in `Error.CausedBy` instead of being flattened into a string.

### Phase 3 — logger format details

- `{Timestamp:yyyy-MM-dd HH:mm:ss.fff}` — local time, no offset (was `:O`,
  ISO 8601 with zone).
- `{SourceContext}` — full class name (e.g.
  `SemiStep.Core.Recipes.Import.CsvService`).
- `formatProvider: CultureInfo.InvariantCulture` — kept for reproducibility.

Sample output line:

```
2026-04-29 11:53:31.850 [INF] SemiStep.Core.Recipes.Import.CsvService: Loaded recipe from C:\Users\admin\Desktop\recipe.csv: 11 steps
```

## What Goes Where

- **Implementation Steps (`[ ]`):** all file moves, code edits, csproj edits,
  test fixture updates, and plan-file marker updates.
- **Post-Completion (no checkboxes):** manual GUI smoke-test against a real
  PLC if hardware is available, manual broken-config scenario verification,
  archiving the plan to `docs/plans/completed/`.

## Implementation Steps

### Task 1: Move GroupDefinition + GridColumnDefinition into Core/Recipes

**Files:**

- Move: `SemiStep/Core/Configuration/GroupDefinition.cs` -> `SemiStep/Core/Recipes/GroupDefinition.cs`
- Move: `SemiStep/Core/Configuration/GridColumnDefinition.cs` -> `SemiStep/Core/Recipes/GridColumnDefinition.cs`

- [x] physically move the two files into `Core/Recipes/`
- [x] update the namespace in both: `SemiStep.Core.Configuration` ->
      `SemiStep.Core.Recipes`
- [x] update consumers' `using` directives (grep for
      `SemiStep.Core.Configuration` in files that reference
      `GroupDefinition` or `GridColumnDefinition`; add
      `SemiStep.Core.Recipes;` and remove the old `using` if nothing else
      from `Configuration` remains)
- [x] `dotnet build SemiStep/SemiStep.slnx` — green
- [x] `dotnet test SemiStep/Tests/Tests.csproj` — all pass without changes
      to test logic (only `using` updates)

### Task 2: Rename ConfigRegistry -> RecipeMetadataRegistry, move into Core/Recipes, delete dead method

**Files:**

- Move + Rename: `SemiStep/Core/Configuration/ConfigRegistry.cs` -> `SemiStep/Core/Recipes/RecipeMetadataRegistry.cs`
- Modify: `SemiStep/Core/Recipes/RecipeDi.cs` (registration)
- Modify: ~13 production consumers (type + namespace)
- Modify: 5 test helpers (`Tests/Core/Helpers/CoreFixture.cs`,
  `Tests/UI/Helpers/UIFixture.cs`, `Tests/UI/RecipeRowViewModelTests.cs`,
  `Tests/UI/RecipeGridViewModelTests.cs`,
  `Tests/Domain/Unit/ImportedRecipeValidatorTests.cs`,
  `Tests/S7/*.cs` — exact list emerges from the first failing build)

- [x] move and rename the file: `ConfigRegistry.cs` ->
      `RecipeMetadataRegistry.cs`
- [x] change namespace `SemiStep.Core.Configuration` ->
      `SemiStep.Core.Recipes` and class name
      `ConfigRegistry` -> `RecipeMetadataRegistry`
- [x] delete the `ResolvePropertyType(Recipe, int, string)` method —
      dead code
- [x] update `RecipeDi.cs:25`: singleton registration under the new name
- [x] global Find/Replace `ConfigRegistry` -> `RecipeMetadataRegistry`
      across production and tests (use `replace_all`)
- [x] update consumer `using` directives (Configuration -> Recipes)
- [x] `dotnet build` — green
- [x] `dotnet test` — all pass (integration tests automatically validate
      that the rename did not change behavior)

### Task 3: Create PlcConfigurationValidator

**Files:**

- Create: `SemiStep/Core/Plc/Configuration/PlcConfigurationValidator.cs`
- Create: `SemiStep/Tests/Core/Configuration/PlcConfigurationValidatorTests.cs`
  (the only new test class in this plan)

- [x] read `DataDbLayout.cs` and `ExecutionDbLayout.cs` to lock in the
      exact invariants for each layout
- [x] create `PlcConfigurationValidator` as `internal static class` with
      `public static Result Validate(PlcConfiguration config)`. Aggregate
      every layout-invariant violation into a `List<IError>`, return
      `Result.Fail(errors)` if any are present, otherwise `Result.Ok()`
- [x] for `ManagingDbLayout`:
      `TotalSize >= RecipeLinesOffset + sizeof(int)`,
      `TotalSize > CommittedOffset`, offsets >= 0
- [x] for `DataDbLayout` (Int/Float/String):
      `DataStartOffset >= max(CapacityOffset, CurrentSizeOffset) + sizeof(int)`,
      offsets >= 0
- [x] for `ExecutionDbLayout`:
      `TotalSize >= max(<all offsets>) + <field size>`, offsets >= 0
- [x] write `PlcConfigurationValidatorTests`: success case with defaults;
      a failure case for each violation type (managing too small, data
      offset overlap, execution overflow); aggregation case (multiple
      violations at once — all returned together)
- [x] `dotnet build` — green
- [x] `dotnet test` — new tests pass, old tests still pass

### Task 4: Wire validator into ConfigFacade, switch MapToDomain to Result.Try, remove Validate from codecs

**Files:**

- Modify: `SemiStep/Core/Configuration/Facade/ConfigFacade.cs`
- Modify: `SemiStep/Core/Plc/S7/Serialization/ManagingAreaCodec.cs`
- Modify: `SemiStep/Core/Plc/S7/Serialization/ExecutionStateCodec.cs`
  (six `ArgumentException` throws over layout invariants — confirmed by
  reviewer, all to be removed)
- No-op: `SemiStep/Core/Plc/S7/Serialization/ArrayCodec.cs` — verified
  clean (no runtime throws), no edit needed

**Critical sequencing:** validator must be wired **before** the codec
throws are removed, otherwise an invalid config briefly bypasses validation
in mid-task.

- [x] in `ConfigFacade.LoadAndValidateAsync`, after `MapToDomain`, add the
      validator branch (matching the existing `xrefResult` / `defaultsResult`
      pattern):
      ```
      var plcResult = PlcConfigurationValidator.Validate(config.PlcConfiguration);
      if (plcResult.IsFailed) { /* log + return Result.Fail with propagated reasons */ }
      ```
- [x] replace the existing manual try/catch around `MapToDomain` with
      `Result.Try(() => MapToDomain(...), ex => new Error("...").CausedBy(ex))`.
      Compose with `.Bind`/`.WithReasons` as shown in Technical Details.
      Verify that `.WithReasons(loadResult.Reasons)` ordering matches the
      previous behavior so existing reason-list assertions in tests pass
- [x] in `ManagingAreaCodec`: delete the `Validate(layout)` method; the
      constructor uses `_layout = layout` directly
- [x] in `ExecutionStateCodec`: delete the six `ArgumentException` throws
      over layout invariants from the constructor (the layout is now
      guaranteed valid by `PlcConfigurationValidator`)
- [x] `dotnet build` — green
- [x] `dotnet test` — all pass (existing S7 tests included)

### Task 5: Defensive ErrorWindow for unexpected DI / startup exceptions

**Files:**

- Modify: `SemiStep/UI/Program.cs`

- [x] today's catch handler at `Program.cs:42-45` only logs and falls
      through to `finally`; add `App.RunErrorWindow(["Application startup failed unexpectedly:", ex.Message])`
      **after** the existing `Log.Fatal(ex, ...)` and **before** the
      implicit return so the user-visible window is shown before
      `Log.CloseAndFlushAsync()` runs in `finally`
- [x] manual test (skipped - not automatable in autonomous run; user verifies in Task 9)
- [x] manual test (skipped - not automatable in autonomous run; user verifies in Task 9)
- [x] update the plan: mark the regression-fix verification as completed

### Task 6: Add Microsoft.Extensions.Logging packages and AddSerilog wire-up

**Files:**

- Modify: `SemiStep/UI/SemiStep.UI.csproj`
- Modify: `SemiStep/Core/SemiStep.Core.csproj` (only if
  `ILogger.Abstractions` is not already transitive)
- Modify: `SemiStep/UI/Program.cs`

- [ ] add `<PackageReference>` for `Microsoft.Extensions.Logging` and
      `Serilog.Extensions.Logging` (latest stable) to `SemiStep.UI.csproj`
- [ ] verify `Microsoft.Extensions.Logging.Abstractions` is reachable from
      `SemiStep.Core` (it is normally transitive through
      `Microsoft.Extensions.DependencyInjection.Abstractions`). Seven of
      the ten conversion targets in Task 7 live in `SemiStep.Core`, so this
      must be solid. **If transitive resolution is not present, add
      `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions">`
      to `SemiStep/Core/SemiStep.Core.csproj` explicitly** rather than
      relying on transitivity that future DI version bumps could change
- [ ] in `Program.CreateLogger`: apply the unified `outputTemplate`
      `"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"`
      to **both** sinks (Console and File)
- [ ] in `Program.StartupAsync`, after building `ServiceCollection` and
      before `BuildServiceProvider`:
      `services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false))`
- [ ] `dotnet build` — green

### Task 7: Convert production classes to ILogger<T>

**Files (10 production classes; `Program.cs` and `ConfigFacade.cs` are
intentionally excluded — both are documented bootstrap exceptions and use
Serilog static API directly):**

- Modify: `SemiStep/Core/Recipes/Import/CsvService.cs`
- Modify: `SemiStep/Core/Plc/PlcLifecycleManager.cs`
- Modify: `SemiStep/Core/Recipes/RecipeWorkspace.cs`
- Modify: `SemiStep/Core/Plc/Sync/PlcSyncExecutor.cs`
- Modify: `SemiStep/Core/Plc/Sync/PlcTransactionExecutor.cs`
- Modify: `SemiStep/Core/Plc/Sync/PlcExecutionMonitor.cs`
- Modify: `SemiStep/Core/Plc/S7/S7Service.cs`
- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs`
- Modify: `SemiStep/UI/MainWindow/MainWindowViewModel.cs`

- [ ] for each file: `using Serilog;` ->
      `using Microsoft.Extensions.Logging;`
- [ ] add a constructor parameter `ILogger<ThisClass> logger`, store in a
      `private readonly ILogger<ThisClass> _logger`
- [ ] replace call sites by mapping:
      `Log.Information` -> `_logger.LogInformation`,
      `Log.Error` -> `_logger.LogError`,
      `Log.Warning` -> `_logger.LogWarning`,
      `Log.Debug` -> `_logger.LogDebug`,
      `Log.Fatal` -> `_logger.LogCritical`
- [ ] update test fixtures (`Tests/Core/Helpers/CoreFixture.cs`,
      `Tests/UI/Helpers/UIFixture.cs`, `Tests/Csv/Helpers/CsvTestHelper.cs`,
      and any test that constructs `new XService(...)` directly) — register
      `services.AddLogging(b => b.AddDebug())` or pass
      `NullLogger<T>.Instance`
- [ ] `dotnet build` — green
- [ ] `dotnet test` — all pass

### Task 8: ConfigFacade uses Log.ForContext (bootstrap exception)

**Files:**

- Modify: `SemiStep/Core/Configuration/Facade/ConfigFacade.cs`

- [ ] add `private static readonly ILogger _logger = Log.ForContext(typeof(ConfigFacade));`
      (the `using Serilog;` import stays — this is the documented
      bootstrap exception)
- [ ] replace every static `Log.X(...)` call with `_logger.X(...)`
- [ ] `dotnet build` — green
- [ ] `dotnet test` — all pass

### Task 9: Manual verification — log format and broken-config scenario

- [ ] launch the application, perform save recipe -> load recipe
- [ ] open the console and `C:\DISTR\Logs\semistep.log` — both must contain
      identical formatting, e.g.
      `2026-04-29 11:53:31.850 [INF] SemiStep.Core.Recipes.Import.CsvService: Loaded recipe ...`.
      Verify: no `+03:00` in the timestamp; `SourceContext` is present
      (full class name)
- [ ] temporarily break `connection.yaml` (e.g.
      `managing_db_total_size: 4`), launch — an `ErrorWindow` with a clear
      message must open, not a stack trace; the log entry should also use
      the new format
- [ ] restore `connection.yaml`

### Task 10: Verify acceptance criteria

- [ ] every requirement from the Overview is implemented:
  - config regression -> user-friendly `ErrorWindow` (no stack trace)
  - `Core/Configuration/` contains only the YAML pipeline +
    `AppConfiguration` + `GridStyleOptions` (no domain definition types)
  - 10 production classes use `ILogger<T>` via DI
  - console and file logs share an identical format
  - timestamp has no timezone offset
  - `SourceContext` is present
- [ ] `dotnet build SemiStep/SemiStep.slnx` — green
- [ ] `dotnet test SemiStep/Tests/Tests.csproj` — all pass (286+ tests)
- [ ] manual GUI smoke-test (load/save recipe) — no regressions

### Task 11: Update documentation and archive plan

- [ ] update `SemiStep/AGENTS.md`: replace any `ConfigRegistry` references
      with `RecipeMetadataRegistry`; add a logging-policy section
      ("`ILogger<T>` via DI for production classes; `Log.ForContext<T>()`
      is the documented bootstrap exception in `ConfigFacade`")
- [ ] update `Docs/02-architecture.md` (or equivalent) if it references the
      old type locations
- [ ] move
      `docs/plans/20260429-config-cleanup-and-logging-foundation.md` ->
      `docs/plans/completed/20260429-config-cleanup-and-logging-foundation.md`

## Post-Completion

*Items requiring manual intervention or external systems — informational only*

**Manual verification:**

- Run the application against a real PLC (or PLCSIM) and execute a full
  cycle: connect -> load recipe -> write recipe -> conflict detection ->
  reconnect. Confirm that PLC byte layouts are unaffected by codec edits
  (removing `Validate` must not change encode/decode logic).
- Verify that log files rotate correctly (5 MB limit, 5 retained files,
  per `Program.CreateLogger`).
- If the PLC is later reconfigured with new layout parameters (e.g. a
  different `managing_db_total_size`), `PlcConfigurationValidator` will
  catch any mismatch with a readable message at startup.

**External system updates:**

- None — `ConfigFiles/connection/connection.yaml` schema is unchanged;
  existing user configs continue to work without migration.

**Out of scope (decided during planning):**

- Do not split `RecipeMetadataRegistry` into separate ActionsRegistry /
  PropertiesRegistry / ColumnsRegistry / GroupsRegistry. It is a narrow
  query API over a single connected metadata graph; splitting would only
  add classes and DI registrations without reducing real coupling.
- Do not move `GridStyleOptions` — it stays in `Core/Configuration/`.
- Do not introduce a bootstrap DI container just to inject the logger into
  `ConfigFacade` — `Log.ForContext<T>()` is sufficient.
- Do not write a custom enricher that shortens class names in
  `SourceContext` — full names are kept.
- Do not change YAML schemas.
