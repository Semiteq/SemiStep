# Architecture Cleanup — Round 2

## Overview

Follow-up cleanup after the project rename refactor (`SemiStep.Core` / `SemiStep.UI` / `SemiStep.Tests`). Four independent cleanups, one commit each:

1. **DI lambdas** — eliminate redundant factory lambdas where DI can auto-resolve constructor parameters.
2. **Static conversions** — convert `LoopParser` and `PropertyParser` to `static class`, remove from DI.
3. **Magic strings** — eliminate the cross-layer leak in `CellStateResolver` (Core code branching on UI column-type strings) by promoting the existing `ReadOnly` flag and replacing the `"action"` literal with the existing `StepValueParser.ActionColumnKey` constant.
4. **`PlcDataType` deletion** — remove the field from `GridColumnDefinition`, the DTO, the mapper, and all YAML configs. The PLC layer already uses `PropertyTypeDefinition.SystemType` resolved via `PropertyTypeId`; `PlcDataType` is dead duplication.

The changes are independent; each task is self-contained, leaves the build green and tests passing, and can be reviewed separately.

## Context (from prior investigation)

- **DI lambdas** confirmed: 5 redundant in `RecipeDi.cs` and `CsvDi.cs`. Forwarding singletons in `S7Di.cs` are NOT redundant (they preserve identity for multi-interface registration) and stay.
- **Statics**: `LoopParser` and `PropertyParser` have no instance state, are not mocked, are not behind any interface. `CompiledFormula` / `FormulaEngine` look stateless but hold immutable data fields and are kept as instance for DI graph clarity.
- **Magic strings architectural verdict**: the only real smell is `CellStateResolver.cs` (in Core) branching on `column.ColumnType is "step_start_time_field"` — Core depending on a UI-facing discriminator. The fix is to promote the existing `column.ReadOnly` flag (already on the record, already in YAML schema) and mark step-start-time columns as `read_only: true` in YAML. The `"action"` literal in the same file is replaced with the existing `StepValueParser.ActionColumnKey` constant. Other in-file constants (`TimingCalculator` `"step_duration"`, `LoopParser` `"task"`) are already correctly file-local.
- **`PlcDataType`**: every PLC write goes through `RecipeConverter.SerialiseProperty` → `RecipeMetadataRegistry.GetProperty(PropertyTypeId)` → `SystemType` → `PropertyTypeMapping.FromSystemType`. The column's `PlcDataType` is loaded by `ColumnMapper` and never read. Deleting requires updating ~20 YAML test configs.

## Development Approach

- **Testing approach**: Regular — implementations are mechanical; tests already cover the affected behavior (303 tests). Each task ends with a green `dotnet build` and `dotnet test`.
- Complete each task fully before moving to the next.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- Stage changes per task; do not commit until user approves.
- Existing test suite (303 tests) is the regression net. New tests added only if a task introduces a new behaviour worth covering.

## Testing Strategy

- **Unit / integration tests**: existing 303 tests in `SemiStep.Tests` cover all affected paths (DI graph resolution via `CoreFixture`/`UiFixture`, YAML loading via `ConfigLoadingTests`, recipe semantics, PLC serialization). They must stay green.
- **No e2e tests** in the project. UI smoke check is manual: `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj` should start without exceptions after Task 4 (PlcDataType deletion touches the YAML schema).
- **New tests** are not required for any task — none introduces new behavior, only consolidates existing.

## Progress Tracking

- mark completed items with `[x]` immediately when done
- add newly discovered tasks with ➕ prefix
- document issues/blockers with ⚠️ prefix

## Solution Overview

Four sequential, independent cleanups. Each is small, mechanical, and verifiable by the existing test suite. No architectural redesign — just removing duplication, dead code, and a cross-layer leak.

## Technical Details

### Task 1 — DI lambda elimination

5 registrations replaced. DI auto-resolves constructor parameters when the type is registered as `AddSingleton<T>()`.

### Task 2 — Static conversions

`LoopParser` and `PropertyParser` lose their instance form; callers invoke static methods. DI registrations removed. Dependent classes (`RecipeAnalyzer`, `RecipeEditor`, `CsvRowConverter`, `ClipboardSerializer`, etc.) drop the constructor parameter.

### Task 3 — CellStateResolver cleanup

Two changes in `CellStateResolver.cs`:
- Replace `if (column.Key is "action")` with `if (column.Key == StepValueParser.ActionColumnKey)`.
- Replace `if (column.ColumnType is "step_start_time_field")` with `if (column.ReadOnly)`.

The second change requires marking step-start-time columns in YAML with `read_only: true`. Currently the `ReadOnly` flag exists on the record and DTO but is `false` for these columns; updating the test YAMLs is mechanical.

### Task 4 — PlcDataType deletion

Remove from:
- `GridColumnDefinition` record.
- `ColumnBusinessLogicDto`.
- `ColumnMapper` mapping line.
- All `business_logic.plc_data_type:` lines in `SemiStep.Tests/YamlConfigs/`.
- Any test code that constructs `GridColumnDefinition` with the parameter (CorePropertyStateTests has 4 such instances).

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all changes are local code/YAML edits.
- **Post-Completion**: manual UI smoke test after Task 4 to confirm YAML parses and the recipe grid renders.

## Implementation Steps

### Task 1: Eliminate redundant DI lambdas

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvDi.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeDi.cs`

- [x] `CsvDi.cs:12-14`: replace `services.AddSingleton(sp => new CsvService(...))` with `services.AddSingleton<CsvService>()`; drop the `using Microsoft.Extensions.Logging;` if it becomes unused. (completed via Task 1b)
- [x] `RecipeDi.cs:26`: replace `services.AddSingleton(sp => new RecipeMetadataRegistry(...))` with `services.AddSingleton<RecipeMetadataRegistry>()`.
- [x] `RecipeDi.cs:31-36`: replace `RecipeWorkspace` factory lambda with `services.AddSingleton<RecipeWorkspace>()`. (completed via Task 1b)
- [x] `RecipeDi.cs:38-42`: replace `RecipeEditor` factory lambda with `services.AddSingleton<RecipeEditor>()`. (completed via Task 1b)
- [x] `RecipeDi.cs:44-51`: replace `PlcLifecycleManager` factory lambda with `services.AddSingleton<PlcLifecycleManager>()`. (completed via Task 1b)
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303/303 green.

### Task 1b: Promote internal constructors on DI-registered services

**Rationale:** Task 1's `[x] (skipped)` items were skipped because the affected types have `internal` constructors and `AddSingleton<T>()` requires a public constructor. The `internal` ctor pattern provides no real protection here (only Core and UI assemblies exist; UI gets these types through DI anyway; tests have `InternalsVisibleTo`). It just forces the factory-lambda boilerplate the plan is trying to eliminate. Promoting the four ctors to `public` resolves the contradiction and unblocks the DI simplification. No cascading promotions are needed — `AddSingleton<T>()` only inspects T's own constructor; T's dependencies are resolved from DI as already-instantiated singletons regardless of their own ctor visibility.

**Scope confirmed via codebase inventory:** exactly four DI-registered types use `internal` ctor with factory-lambda registration: `CsvService`, `RecipeWorkspace`, `RecipeEditor`, `PlcLifecycleManager`. `PlcConflictDialog` is also `internal` ctor but is NOT in DI (instantiated directly from `MainWindowViewModel`), so it stays as-is.

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvService.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeWorkspace.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeEditor.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvDi.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeDi.cs`

- [x] `CsvService.cs`: change `internal CsvService(...)` to `public CsvService(...)`.
- [x] `RecipeWorkspace.cs`: change `internal RecipeWorkspace(...)` to `public RecipeWorkspace(...)`.
- [x] `RecipeEditor.cs`: change `internal RecipeEditor(...)` to `public RecipeEditor(...)`.
- [x] `PlcLifecycleManager.cs`: change `internal PlcLifecycleManager(...)` to `public PlcLifecycleManager(...)`.
- [x] `CsvDi.cs`: replace the `CsvService` factory lambda with `services.AddSingleton<CsvService>()`; drop `using Microsoft.Extensions.Logging;` if it becomes unused.
- [x] `RecipeDi.cs`: replace the `RecipeWorkspace` factory lambda with `services.AddSingleton<RecipeWorkspace>()`.
- [x] `RecipeDi.cs`: replace the `RecipeEditor` factory lambda with `services.AddSingleton<RecipeEditor>()`.
- [x] `RecipeDi.cs`: replace the `PlcLifecycleManager` factory lambda with `services.AddSingleton<PlcLifecycleManager>()`.
- [x] Update the `(skipped — ...)` notes on lines 85, 87-89 of this plan: remove the skip rationale, leave the items as `[x]` with the corrected outcome (factory lambda replaced via Task 1b).
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303/303 green.

**Note on cascading promotions:** the Rationale section's claim that no cascading promotions would be needed was incorrect — the C# compiler enforces accessibility consistency for `public` ctor signatures (CS0051). To unblock the four target ctors, dependency types referenced as parameters were also promoted to `public`: `CsvFileSerializer`, `RecipeStateManager`, `RecipeHistoryManager`, `RecipeAnalyzer`, `LoopParser` (to be reverted to static in Task 2), `FormulaApplicationCoordinator`, `FormulaEngine`, `CompiledFormula`, `FormulaDefinition`. DI resolution itself works regardless of ctor visibility, but ctor *signatures* require accessible parameter types.

### Task 2: Convert LoopParser and PropertyParser to static

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Analysis/LoopParser.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/PropertyParser.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeDi.cs`
- Modify: every caller of `LoopParser` and `PropertyParser` (constructor injection sites). Confirmed callers: `RecipeAnalyzer`, `RecipeEditor`, `ClipboardSerializer`, `CsvRowConverter`, plus the test fixture `SemiStep.Tests/Core/Unit/Properties/CorePropertyParsingTests.cs` (instantiates `PropertyParser` directly).

- [x] `LoopParser.cs`: change to `internal static class LoopParser`; convert all instance methods to static; ensure no instance fields.
- [x] `PropertyParser.cs`: change to `public static class PropertyParser`; convert all instance methods to static; ensure no instance fields.
- [x] `RecipeDi.cs:18`: remove `services.AddSingleton<LoopParser>()`.
- [x] `RecipeDi.cs:24`: remove `services.AddSingleton<PropertyParser>()`.
- [x] Update all callers — drop the constructor parameter; replace `_loopParser.Parse(...)` and `_propertyParser.Parse(...)` with static calls (`LoopParser.Parse(...)`, `PropertyParser.Parse(...)`).
- [x] Update `CorePropertyParsingTests.cs` — replace `new PropertyParser().Parse(...)` with `PropertyParser.Parse(...)`.
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303/303 green.

### Task 3: Remove cross-layer leak in CellStateResolver

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Properties/CorePropertyStateTests.cs` (test `StepStartTimeColumn_IsReadonly` builds a `GridColumnDefinition` with `ColumnType: "step_start_time_field"` and `ReadOnly: false`; after this task, the test must reflect the new contract — `ReadOnly: true`)
- ⚠️ **External configs (out of repo)**: any deployed config (e.g. `C:\DISTR\Config\Semistep\`) that uses `column_type: step_start_time_field` must add `read_only: true` to those columns. Without this update the column will render as editable — silent UX regression. No in-repo YAML uses this column type today, so no test YAML changes are needed.

- [x] Read `CellStateResolver.cs`. Confirm: there is already a later `column.ReadOnly` branch returning `Readonly`, so the early `ColumnType is "step_start_time_field"` branch is fully redundant once columns are correctly marked `ReadOnly: true`.
- [x] Replace `column.Key is "action"` (or equivalent) with `column.Key == StepValueParser.ActionColumnKey`.
- [x] **Delete** the `column.ColumnType is "step_start_time_field"` branch entirely. Do not duplicate the `ReadOnly` check — the existing branch covers it.
- [x] Update `CorePropertyStateTests.StepStartTimeColumn_IsReadonly`: change the column construction to `ReadOnly: true`. The test now asserts the new contract — readonly cells are determined by `ReadOnly`, not by `ColumnType`.
- [x] Grep production-code occurrences of literal `"step_start_time_field"` and `"action"` in Core to confirm no remaining leaks.
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303/303 green.

**Side-effect note**: `DefaultValueValidator.ValidateReadOnlyConflict` emits a warning when a `read_only: true` column has a default value defined in an action. If any external config defines a default for the step-start-time column, a new warning will appear after this change. Benign (warning, not error), but worth knowing.

### Task 4: Delete PlcDataType

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/GridColumnDefinition.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/ColumnBusinessLogicDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/ColumnMapper.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Properties/CorePropertyStateTests.cs` (4 instances)
- Modify: every YAML file under `SemiStep/SemiStep.Tests/YamlConfigs/` containing `plc_data_type:`
- Modify: any sample/distributed config under `Artifacts/` or distribution paths if present

- [x] Remove `PlcDataType` parameter from `GridColumnDefinition` record.
- [x] Remove `PlcDataType` property from `ColumnBusinessLogicDto`.
- [x] Remove the corresponding line from `ColumnMapper.cs`.
- [x] Update test fixtures in `CorePropertyStateTests.cs` — drop the `PlcDataType:` argument from each `new GridColumnDefinition(...)` call.
- [x] Sweep `SemiStep.Tests/YamlConfigs/` and remove every `plc_data_type:` line.
- [x] Verify with grep that no source mentions `PlcDataType` or `plc_data_type` anymore.
- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 303/303 green.

### Task 5: Verify acceptance criteria

- [x] Confirm 303/303 tests still pass. (303/303 green, 8s)
- [x] manual UI smoke (skipped - not automatable)
- [x] `git diff --stat` review — confirm scope matches expectation, no incidental changes. Scope confirmed against `899b4ac..HEAD`: DI lambda removal in `RecipeDi.cs`/`CsvDi.cs`, ctor visibility bumps on the four DI-registered types and their dependency types, static conversion of `LoopParser`/`PropertyParser` with caller updates, `CellStateResolver` cleanup, `PlcDataType` removal from `GridColumnDefinition`/`ColumnBusinessLogicDto`/`ColumnMapper` plus `plc_data_type:` lines stripped from `ConfigFiles/columns/columns.yaml` and all `SemiStep.Tests/YamlConfigs/**/columns.yaml`, test fixtures updated, plan file edits. No incidental changes detected.

### Task 6: Update plan and documentation

- [x] Move this plan to `Docs/plans/completed/`.
- [x] Update `CLAUDE.md` only if any conventions changed (none expected). No relevant section in CLAUDE.md changed — it does not document YAML schema details, `PlcDataType`, or visibility conventions for DI-registered services.

## Post-Completion

**Manual verification:**
- Launch `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj` against an external config that contains a `step_start_time_field` column. Confirm that column renders as readonly **after** the deployed YAML has been updated with `read_only: true`. Without that YAML update the column will render as editable — silent regression.
- Confirm the action column is editable.

**External system updates (REQUIRED before deploying):**
- ⚠️ **`step_start_time_field` columns in deployed configs (e.g. `C:\DISTR\Config\Semistep\`) must be updated to `read_only: true`.** Task 3 removes the type-based readonly inference; cells now derive their readonly state from the explicit `read_only` flag only. Configs that omit it will render those columns as editable.
- `plc_data_type:` lines in deployed configs become silently ignored after Task 4. YAML's unknown-key tolerance keeps loading working; the lines should be cleaned up at the next config edit but require no urgent action.
