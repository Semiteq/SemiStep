# Plan: Architecture Simplification

## Overview

The solution is split into 10 csproj files. The split predates the codebase's actual size
and forces structural patterns that are now hurting maintenance:

- `TypesShared` has become a god-project (39 files) because every cross-module type and
  every service interface gravitates to it; service interfaces (`ICsvService`, `IClipboardService`,
  `ICoreService`, `IS7Service`, `IPlcSyncService`) end up on the producer/shared side, contradicting
  the project's own `AGENTS.md:91` rule "Interfaces belong on the consumer side".
- The domain model is anemic (`Recipe`, `Step`, `PropertyValue` are pure data containers).
  All recipe operations live in static utilities (`RecipeMutator`) and service classes
  (`CoreFacade`, `RecipeEditService`, `DomainFacade`) — producing 5-layer pure-delegation chains
  for every operation.
- `DomainFacade` is a god-class (12 dependencies, 330 lines) that aggregates every domain
  operation into one type.
- `Config` carries 18 DTO files. Most mappers (`PropertyMapper`, `ColumnMapper`,
  `ActionMapper`, `GridStyleMapper`, `ConnectionMapper`) do real validation and structural
  transformation (nested-to-flat, required-field checks, `ip:port` parsing). Only
  `GroupMapper` is genuine ceremony — a 1:1 dictionary-to-record loop with no validation.
  Earlier draft over-claimed how much was eliminable; the actual saving from
  Config-cleanup alone is small. The folder-reorganization in Phase 6 is the larger win.
- Comparable production Avalonia applications (SourceGit: 1 csproj, OpenUtau: 4, UVtools: 6)
  achieve more functionality with far less project-level fragmentation.

This plan removes the over-engineering in six sequential, individually-buildable phases.
Each phase ends with build green and all tests passing. After all phases the solution is
3 csprojs (Core / UI / Tests), `Recipe` owns its operations, `RecipeWorkspace` replaces
`DomainFacade`, folders are organized by feature, and the simple Config DTOs are gone.

## Solution Overview

- **Strategy: phased, eight commits** (six core + one optional UI threading + one
  IS7Service decomposition). Each phase is self-contained: build green, tests pass, app
  runs. No feature freeze required.
- **Target csproj layout: 3 projects.**
  - `SemiStep.Core` — all non-UI code (domain, plc, csv, config, clipboard, shared types)
  - `SemiStep.UI` — Avalonia views, viewmodels, and `Program.cs` entry point
  - `Tests` (assembly name) / `Tests.csproj` (file name) — xUnit test project, kept as-is
- **Interface policy after refactor (matches `AGENTS.md:90-92`):**
  - **Delete** `ICsvService`, `IClipboardService`, `ICoreService` — single production
    implementation each. `FailingCsvService` is a test fixture, not a real second impl;
    a `protected virtual` method on `CsvService` plus a test-only `ThrowingCsvService`
    subclass replaces it without an interface. `ICoreService` dies with `CoreFacade`
    (Phase 4). `IClipboardService` dies after Phase 1 removes the unused stub.
  - **Keep** `IS7Transport`, `IS7Driver` — real internal hardware boundaries; tests use
    `FakeS7Transport` / `FakeS7Driver` to simulate the external PLC.
  - **Keep** `IPlcSyncService` — single impl currently, but exposes runtime state to
    UI across an architectural boundary; conservative keep. Re-evaluate as a follow-up
    after main refactor lands.
  - **Decompose** `IS7Service` into role-based interfaces (Phase 8): `IS7Connection`
    (Connect/Disconnect/IsConnected/StateChanged), `IS7Reader` (ReadRecipeFromPlc /
    ReadManagingArea), `IS7ExecutionStream` (ExecutionState/IsRecipeActive). One
    `S7Service` implements all three; each consumer takes only the role it needs.
- **Rich-domain `Recipe`, but pure.** `Recipe` exposes structural operations on immutable
  state (`AppendStep(Step)`, `RemoveStep(int)`, `ReplaceStep(int, Step)`) — no `ConfigRegistry`,
  no validation, no analyzer. Purely manipulates its own data shape.
- **`DomainFacade` -> three smaller pieces, none a god-class.** The 330-line god-class splits
  along three single responsibilities:
  - `RecipeWorkspace` (~80 lines, 4 deps): current state + history + analysis + `Apply(Recipe)`
    pipeline. Owns `RecipeStateManager`, `RecipeHistoryManager`, `RecipeAnalyzer`,
    `IPlcSyncService`. Public surface: state queries (CurrentRecipe, IsDirty, Snapshot, ...)
    + `Apply(Recipe)`, `Undo`, `Redo`, `MarkSaved`, `Reset`.
  - `RecipeEditor` (~80 lines, 4 deps): edit operations. Owns `RecipeWorkspace`,
    `ConfigRegistry`, `FormulaApplicationCoordinator`, `IPropertyParser`. Public surface:
    `AppendStep(int actionId)`, `InsertStep`, `RemoveStep`, `ChangeStepAction`,
    `UpdateStepProperty`, `InsertSteps`, `RemoveSteps`. Each method does Registry-lookup,
    builds a new `Step` (via `StepInitializer`), calls `recipe.AppendStep(step)`, then
    `workspace.Apply(newRecipe)`.
  - `PlcLifecycleManager` (already exists internally, ~150 lines after promotion): PLC
    connection, sync enable/disable, conflict resolution, read-from-PLC. Owns `IS7Service`,
    `IPlcSyncService`, `RecipeWorkspace` (to apply read recipes).
  - I/O (CSV load/save, clipboard serialize/deserialize) stays on `CsvService` /
    `ClipboardService` directly. UI's `RecipeMutationCoordinator` orchestrates the flow:
    `csvService.LoadAsync(path)` -> `validator.Validate(recipe)` -> `workspace.Apply(recipe)`.
- **Feature-folder layout** inside `SemiStep.Core`: `Recipe/`, `Plc/`, `Configuration/`,
  with current technical-grouping folders (Csv/, Clipboard/) absorbed where they belong.
- **Minimal Config DTO simplification.** Eliminate only `GroupMapper` (pure 1:1
  dict-to-record copy with no validation; group loading goes directly from
  `Dictionary<string, Dictionary<int, string>>` into `GroupDefinition` records inline in
  `ConfigFacade.MapToDomain`). Keep all other DTOs and mappers — they do real
  required-field validation, nested-to-flat transformation (`ColumnMapper`, `GridStyleMapper`),
  and `ip:port` parsing (`ConnectionMapper`). Eliminating them would require either
  a YAML schema break (forcing existing user configs to migrate) or re-introducing the
  intermediate types under different names — neither is worth the trade-off.

## Constraints

- **PLC protocol/layout immutable.** Byte layouts in PLC DB blocks, protocol constants in
  `S7/Protocol/ProtocolConstants.cs`, the `RecipeConverter`/`ArrayCodec`/`ManagingAreaCodec`
  encoding contracts must not change. Refactoring may move these files but must not alter
  their byte-level behavior.
- **Performance non-regression.** Recipe load time and UI mutation latency must not regress.
  Snapshot computation in `RecipeAnalyzer` and `TimingCalculator` is on a hot path.
- **CSV and YAML schemas may evolve.** Files do not need to be byte-identical between
  before/after; if simpler shapes require minor schema adjustments (e.g. flattening nested
  YAML), that is acceptable.

## Testing Approach

The primary safety net is the existing **black-box / integration test suite** —
`Tests/Core/Integration/*`, `Tests/Csv/Integration/*`, `Tests/UI/*`,
`Tests/Domain/*`. These tests assert observable behavior (recipe load -> mutate -> save,
loop nesting, observable emissions, conflict resolution) and survive internal refactoring
because they do not reference the layer types being moved or deleted.

Black-box tests take priority over unit tests. Do not write new unit tests for newly
introduced types (`Recipe.AppendStep`, `RecipeEditor`, `RecipeWorkspace`) unless an
integration test cannot reach the new behavior path. The existing integration tests
already cover Recipe mutation surface end-to-end via `RecipeTestDriver` and will continue
to validate behavior after Phase 4/5 without modification.

After each phase:

- All existing tests must remain green. A failing test is a signal — read the failure
  before assuming the test needs an update.
- Test setup updates are made only when their setup references types that no longer
  exist (`StubCsvService`, `DomainFacade`).
- `DomainFacadeReconnectTests` is rewritten in Phase 5 because it manipulates
  `StubIs7Service` property setters and constructs `DomainFacade` manually — both vanish.
  The replacement asserts the same reconnect behavior end-to-end via
  `PlcLifecycleManager`.

## Affected Files

Phase 1 to Phase 6 progressively touch most of the codebase. The full file list per phase
appears in each phase's task block below. High-level summary:

| Phase | Files Created | Files Deleted | Files Modified |
| ----- | ------------- | ------------- | -------------- |
| 1 | 0 | 2 (StubCsvService, StubClipboardService) | ~4 (test helpers) |
| 2 | 3 (csproj) | 8 (csproj) | 2 (slnx, AGENTS.md) |
| 3 | 1 (ThrowingCsvService) | 4 (ICsvService, IClipboardService, ICoreService, FailingCsvService) | ~8 (DI, consumers, CsvService virtual, 1 test rewire) |
| 4 | 0 | 3 (RecipeMutator, CoreFacade, RecipeEditService) | ~5 (Recipe, Step, DomainFacade transitional, CoreDi) |
| 5 | 2 (RecipeWorkspace, RecipeEditor) | 1 (DomainFacade) | ~8 (UI coordinators, DI, PlcLifecycleManager, RecipeTestDriver, CoreFixture, 1 test) |
| 6 | 0 | 1 (GroupMapper) | ~30 (folder moves, namespace updates) |
| 7 (optional) | 0 | 0 | 2 (RecipeMutationCoordinator, AGENTS.md) |
| 8 | 3 (IS7Connection, IS7Reader, IS7ExecutionStream) | 1 (IS7Service) | ~10 (S7Service, PlcLifecycleManager, PlcSyncCoordinator, PlcSyncExecutor, S7Di, both stubs, DI fixtures, PlcLifecycleManagerReconnectTests) |

## Tasks

### Task 1: Cleanup unused stubs

**Files:**

- Delete: `SemiStep/Tests/Helpers/StubCsvService.cs`
- Delete: `SemiStep/Tests/Helpers/StubClipboardService.cs`
- Modify: `SemiStep/Tests/Core/Helpers/CoreTestHelper.cs` (registers both stubs)
- Modify: `SemiStep/Tests/Csv/Helpers/CsvTestHelper.cs` (registers both stubs)
- Modify: `SemiStep/Tests/Domain/DomainFacadeReconnectTests.cs` (registers both stubs in
  manual fixture build)
- Modify: `SemiStep/Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs` (registers
  StubClipboardService — confirmed via grep)

- [x] Verify via Grep across the test tree that `StubCsvService` and `StubClipboardService`
      are only registered via DI and never resolved + invoked. `StubCsvService.LoadAsync`
      throws `NotSupportedException`; `StubCsvService.SaveAsync` returns
      `Result.Fail("StubCsvService does not support saving.")`; `StubClipboardService`
      methods throw `NotSupportedException`. Any test path that actually calls these would
      hit the failure — re-confirm none does.
- [x] Delete `StubCsvService.cs` and `StubClipboardService.cs`.
- [x] Remove their DI registrations from the four sites listed above.
- [x] Build, run all tests; expect green.

### Task 2: Collapse csproj 10 -> 3

**Files:**

- Create: `SemiStep/Core/SemiStep.Core.csproj` (new project absorbing all non-UI code)
- Create: `SemiStep/UI/SemiStep.UI.csproj` (new project absorbing UI + Application/Program.cs)
- Modify: `SemiStep/Tests/Tests.csproj` (collapse to one ProjectReference)
- Modify: `SemiStep/SemiStep.slnx` (3 entries instead of 10)
- Delete (replaced by SemiStep.Core): `SemiStep/Application/Application.csproj`,
  `SemiStep/Clipboard/Clipboard.csproj`, `SemiStep/Config/Config.csproj`,
  `SemiStep/Core/Core.csproj`, `SemiStep/Csv/Csv.csproj`, `SemiStep/Domain/Domain.csproj`,
  `SemiStep/S7/S7.csproj`, `SemiStep/TypesShared/TypesShared.csproj`
- Move: `SemiStep/Application/Program.cs` -> `SemiStep/UI/Program.cs`; delete `Application/` folder
- Modify: `SemiStep/AGENTS.md` (build commands now reference Core/UI csproj names)

#### Pre-move

- [x] Decide on root namespace policy for the merged Core project. Recommended:
      keep current namespaces (`Core`, `Domain`, `Config`, `Csv`, `S7`, `Clipboard`, `TypesShared`)
      so this phase is structural-only, no namespace renames yet. Phase 6 reorganizes
      namespaces alongside the folder move.
- [x] Locate all `<Compile Remove="..." />` exclusions in current csprojs (e.g.
      `Application/ApplicationServiceCollectionExtensions.cs` excluded in `Application.csproj`).
      Either include those files in the merged project or delete them outright if they were
      excluded because they are dead code.

#### Create new csprojs

- [x] Create `SemiStep/Core/SemiStep.Core.csproj` with `<AssemblyName>SemiStep.Core</AssemblyName>`,
      `<RootNamespace>` left empty (per-file file-scoped namespaces drive everything).
      `PackageReference` — union of what current Core/Domain/Config/Csv/S7/Clipboard/TypesShared
      csprojs reference: `Microsoft.Extensions.DependencyInjection.Abstractions`, `Serilog`,
      `FluentResults`, `YamlDotNet`, `CsvHelper`, `S7netplus`, `System.IO.Hashing`,
      `System.Reactive`. Audit each existing csproj to confirm the union before writing.
      Single `<InternalsVisibleTo Include="Tests"/>`. Note: do NOT rename the test
      assembly — `Tests.csproj` keeps its default `AssemblyName=Tests` so all existing
      `<InternalsVisibleTo Include="Tests"/>` declarations work without renaming the
      target.
- [x] Create `SemiStep/UI/SemiStep.UI.csproj` with `<OutputType>WinExe</OutputType>`,
      `<ApplicationIcon>logo.ico</ApplicationIcon>`. `PackageReference` for all Avalonia
      packages currently in `UI.csproj` plus `Microsoft.Extensions.DependencyInjection`
      (full DI, not Abstractions, since this is the entry assembly), Serilog sinks (Console,
      File), enrichers. `ProjectReference` to `SemiStep.Core`. Preserve all
      `<Compile Update>` entries that mark `.axaml.cs` partial classes as code-behind.

#### Move source

- [x] Physically move `Application/`, `Clipboard/`, `Config/`, `Core/`, `Csv/`, `Domain/`,
      `S7/`, `TypesShared/` source folders under the new `SemiStep.Core` project. Keep
      subfolder structure intact.
- [x] Move `Application/Program.cs` and `Application/logo.ico` into the new `SemiStep.UI`
      project root. Delete the now-empty `Application/` folder.
- [x] Delete the 8 old csproj files.

#### Update solution + tests

- [x] Edit `SemiStep.slnx` to contain exactly 3 `<Project Path>` entries.
- [x] Update `Tests.csproj`: replace 8 ProjectReferences with 2 (`SemiStep.Core.csproj`,
      `SemiStep.UI.csproj`). Drop the unused `Microsoft.Extensions.DependencyInjection`
      duplication if already brought in transitively.

#### Update docs

- [x] In `SemiStep/AGENTS.md` update the Build section commands to reference
      `SemiStep/UI/SemiStep.UI.csproj` (entry point). Update Test command path. Update
      Solution path if it changed.

#### Verify

- [x] `dotnet build SemiStep/SemiStep.slnx` green.
- [x] `dotnet test SemiStep/Tests/Tests.csproj` all green.
- [x] `dotnet run --project SemiStep/UI/SemiStep.UI.csproj` launches and renders the main
      window without errors. [verified via build (GUI smoke-test deferred to manual verification)]

### Task 3: Eliminate single-implementation service interfaces

**Files:**

- Delete: `SemiStep/TypesShared/Domain/ICsvService.cs`
- Delete: `SemiStep/TypesShared/Domain/IClipboardService.cs`
- Delete: `SemiStep/TypesShared/Domain/ICoreService.cs`
- Delete: `SemiStep/Tests/Helpers/FailingCsvService.cs` (replaced by `ThrowingCsvService` below)
- Create: `SemiStep/Tests/Helpers/ThrowingCsvService.cs` (subclass of `CsvService` with
  the failure path overridden via `virtual`)
- Modify: `SemiStep/Csv/CsvService.cs` (drop `: ICsvService`, change to `public`,
  mark `LoadAsync` and `SaveAsync` as `public virtual`)
- Modify: `SemiStep/Clipboard/ClipboardService.cs` (drop `: IClipboardService`, public)
- Modify: `SemiStep/Core/Facade/CoreFacade.cs` (drop `: ICoreService`, public; class
  itself is deleted in Phase 4)
- Modify: `SemiStep/Csv/CsvDi.cs`, `SemiStep/Clipboard/ClipboardDi.cs`,
  `SemiStep/Core/CoreDi.cs` (register concrete classes)
- Modify: `SemiStep/Domain/Facade/DomainFacade.cs` (constructor parameter types switch
  from interfaces to concrete classes)
- Modify: `SemiStep/Domain/DomainDi.cs` (remove interface registrations)
- Modify: `SemiStep/Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs`
  (`BuildCoordinatorWithThrowingCsvAsync` registers `ThrowingCsvService` as override)
- Move: `SemiStep/TypesShared/Domain/IPlcSyncService.cs` to `SemiStep/Domain/Plc/`
  (consumer-side relocation). **Do not move** `IS7Service.cs` — it stays in
  `TypesShared/Domain/` for now and is deleted entirely in Phase 8 (moving it just to
  delete it later would be churn).

- [x] Mark `CsvService.LoadAsync` and `CsvService.SaveAsync` as `public virtual`.
- [x] Create `ThrowingCsvService : CsvService` in `Tests/Helpers/`. Override `SaveAsync`
      to return `Task.FromResult(Result.Fail("Simulated disk write failure."))`. Override
      `LoadAsync` to return `Task.FromResult(Result.Fail<Recipe>("ThrowingCsvService does not support loading."))`.
- [x] Delete `FailingCsvService.cs`.
- [x] Update `BuildCoordinatorWithThrowingCsvAsync` in
      `RecipeMutationCoordinatorLoadRecipeTests.cs`. The current setup does not call
      `AddCsv()` (it only registers stubs). After the change the setup must:
      1. call `services.AddCsv()` first — this registers `CsvFileSerializer` and the
         default `CsvService`;
      2. then call `services.AddSingleton<CsvService, ThrowingCsvService>()` — MS.DI
         resolves the latest registration last-wins, so `GetService<CsvService>()`
         returns `ThrowingCsvService` while the inherited `CsvFileSerializer` dependency
         is satisfied by the same container.
      This keeps `ThrowingCsvService`'s inherited `CsvService(CsvFileSerializer)`
      constructor satisfied without any `null!` arguments or parameterless-constructor
      hacks.
- [x] Delete `ICsvService.cs`, `IClipboardService.cs`, `ICoreService.cs`.
- [x] Update `CsvService`, `ClipboardService`, `CoreFacade` to drop interface declarations
      and become `public sealed` (or `public` for `CsvService` since it must allow
      subclassing for `ThrowingCsvService`).
- [x] Update DI registrations in `CsvDi`, `ClipboardDi`, `CoreDi` to register concrete
      classes.
- [x] Update `DomainFacade` constructor: `ICsvService -> CsvService`,
      `IClipboardService -> ClipboardService`, `ICoreService -> CoreFacade`.
- [x] Move `IPlcSyncService.cs` to `SemiStep/Domain/Plc/`. Update namespace and `using`
      directives in producer (`PlcSyncCoordinator.cs`) and consumers. Leave
      `IS7Service.cs` in place — Phase 8 deletes it.
- [x] Build green, all tests green. The `BuildCoordinatorWithThrowingCsvAsync` test path
      should keep working with the subclass approach.

### Task 4: Pure-data Recipe; collapse delegation chains

`Recipe` is pure immutable data with only structural operations. It does not know about
`ConfigRegistry`, `RecipeAnalyzer`, `FormulaApplicationCoordinator`, or any service.
`ConfigRegistry`-dependent work (action lookup, step initialization, property validation,
formula application) lives on `RecipeEditor` (Phase 5). The contract for `Recipe`: "I am
data; I produce a new version of myself given already-resolved inputs."

`ConfigRegistry` holds read-only configuration loaded once at startup (actions, columns,
properties, groups). It does not hold runtime state, business operations, or I/O. It is
constructed once at the top of the application graph and held by `RecipeEditor`,
`PlcLifecycleManager`, `ConfigFacade`. It is not passed through entity method signatures.

**Files:**

- Modify: `SemiStep/TypesShared/Core/Recipe.cs` (add **only** structural mutation methods)
- Modify: `SemiStep/TypesShared/Core/Step.cs` (add `WithProperty(string key, PropertyValue value)` for the property-update flow)
- Delete: `SemiStep/Core/Services/RecipeMutator.cs` (its work splits: structural moves to
  `Recipe`; action-aware initialization stays in `StepInitializer`; formula coordination
  stays in `FormulaApplicationCoordinator`)
- Delete: `SemiStep/Core/Facade/CoreFacade.cs` (its 8 methods reappear in Phase 5 on
  `RecipeEditor`)
- Delete: `SemiStep/Domain/Facade/RecipeEditService.cs` (was pure delegation)
- Modify: `SemiStep/Domain/Facade/DomainFacade.cs` (transitional only — this whole class
  is replaced in Phase 5; the transitional version inlines the lookup/initialize/apply
  steps that `CoreFacade` used to do)
- Modify: `SemiStep/Core/CoreDi.cs` (no more `CoreFacade` registration; `RecipeAnalyzer`,
  `FormulaApplicationCoordinator`, `StepInitializer`, `PropertyValidator` registered for
  direct injection)
- Modify: `SemiStep/Domain/DomainDi.cs` (no more `RecipeEditService` registration)

- [x] On `Recipe`, add **only** structural operations on its own data:
      ```
      public Recipe AppendStep(Step step)
      public Recipe InsertStep(int index, Step step)
      public Recipe RemoveStep(int index)
      public Recipe InsertSteps(int startIndex, IReadOnlyList<Step> steps)
      public Recipe RemoveSteps(IReadOnlyList<int> indices)
      public Recipe ReplaceStep(int index, Step step)
      ```
      No `Result<T>` here — at this level the operations cannot semantically fail
      (preconditions like index range are the caller's contract; use `ArgumentOutOfRangeException`
      via standard `ImmutableList` operations or guard clauses where appropriate). No
      `ConfigRegistry`. No `ActionDefinition`. No formula coordinator. Pure immutable
      record manipulation.
- [x] On `Step`, add `WithProperty(string key, PropertyValue value)` returning a new `Step`
      with the property dictionary updated. Used by Phase 5's `RecipeEditor.UpdateStepProperty`.
- [x] In `DomainFacade` (transitional only — fully removed in Phase 5): replace
      `_coreService.AppendStep(...)` and similar calls with their inlined equivalent. For
      example `AppendStep` becomes:
      ```
      var actionResult = _configRegistry.GetAction(actionId);
      if (actionResult.IsFailed) return actionResult.ToResult();
      var step = _stepInitializer.CreateForAction(actionResult.Value);
      var newRecipe = _stateManager.Current.AppendStep(step);
      var snapshot = _analyzer.Analyze(newRecipe);
      _stateManager.Update(snapshot);
      // existing post-mutation flow (sync notify, etc.) unchanged
      return Result.Ok().WithReasons(snapshot.Reasons);
      ```
      Apply the same flat pattern for the other mutation methods. This is intentionally
      verbose — Phase 5 cleans it up by introducing `RecipeEditor`. Reproduce the index
      validation that currently lives in `CoreFacade.ValidateInsertIndex` /
      `ValidateStepIndex` (lines 36-211 of `CoreFacade.cs`) inside the inlined transitional
      methods — `InsertStep`, `RemoveStep`, `ChangeStepAction`, `UpdateStepProperty`,
      `RemoveSteps` all need an index range check before mutation, otherwise behavior
      regresses against existing tests.
- [x] Inject `RecipeAnalyzer`, `FormulaApplicationCoordinator`, `StepInitializer`,
      `PropertyValidator`, `ConfigRegistry` directly into `DomainFacade` for the
      transition. Drop `CoreFacade`/`RecipeEditService` constructor dependencies.
- [x] Delete `RecipeMutator.cs`, `CoreFacade.cs`, `RecipeEditService.cs`.
- [x] Build green. Run `dotnet test` — `Tests/Core/Integration/Mutation/*` should pass
      unchanged (they exercise behavior end-to-end).

### Task 5: Split DomainFacade into RecipeWorkspace + RecipeEditor + PlcLifecycleManager

`DomainFacade` splits along four single responsibilities:

- `RecipeWorkspace` — state holder (4 deps, ~7 members)
- `RecipeEditor` — edit operations (4 deps, ~7 members)
- `PlcLifecycleManager` — PLC orchestration (4 deps, ~12 members; already exists internally,
  promoted public)
- `CsvService`, `ClipboardService` — concrete classes consumed directly by UI for I/O

UI's `RecipeMutationCoordinator` orchestrates which backend handles a given user action.
Each backend class stays small and single-purpose.

**Files:**

- Create: `SemiStep/Domain/RecipeWorkspace.cs` (public)
- Create: `SemiStep/Domain/RecipeEditor.cs` (public)
- Modify: `SemiStep/Domain/Facade/PlcLifecycleManager.cs` (promoted to public; takes
  `RecipeWorkspace` to call `Apply` after PLC reads; exposes its own
  `event Action<Recipe, Recipe>? PlcRecipeConflictDetected`)
- Modify: `SemiStep/Domain/Helpers/ImportedRecipeValidator.cs` (currently `internal sealed`,
  must become `public sealed` because UI consumes it directly in the load flow)
- Delete: `SemiStep/Domain/Facade/DomainFacade.cs`
- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` (takes `RecipeWorkspace`,
  `RecipeEditor`, `PlcLifecycleManager`, `ICsvService`, `ClipboardService`,
  `ImportedRecipeValidator` instead of single `DomainFacade`; subscribes to
  `_plc.PlcRecipeConflictDetected` instead of `_domainFacade.PlcRecipeConflictDetected`;
  preserves the existing `_lastRecipeResult` / `_lastPlcState` -> `RebuildMessagePanel`
  flow unchanged)
- Modify: `SemiStep/UI/Coordinator/RecipeQueryService.cs` (queries `RecipeWorkspace`)
- Modify: `SemiStep/UI/Coordinator/RecipeStepCoordinator.cs` (calls `RecipeEditor`)
- Modify: `SemiStep/Domain/DomainDi.cs` (register `RecipeWorkspace`, `RecipeEditor`,
  `PlcLifecycleManager` as singletons; drop `DomainFacade` registration)
- Modify: `SemiStep/Tests/Domain/DomainFacadeReconnectTests.cs` (replace
  `StubIs7Service` property-setter mock pattern with constructor-based test driver)
- Modify: `SemiStep/Tests/Core/Helpers/RecipeTestDriver.cs` (currently typed against
  `DomainFacade`; retype to `RecipeWorkspace` + `RecipeEditor`. State queries -> workspace,
  mutations -> editor)
- Modify: `SemiStep/Tests/Core/Helpers/CoreFixture.cs` (build the new trio instead of
  `DomainFacade`)
- Modify: `SemiStep/Tests/UI/RecipeMutationCoordinatorTests.cs` (constructor changes)
- Modify: `SemiStep/Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs` (drop
  `services.GetRequiredService<DomainFacade>()`; use the new trio)

#### Visibility changes (all required for cross-assembly UI consumption post-Phase 2)

- [x] `RecipeWorkspace` -> `public sealed class`.
- [x] `RecipeEditor` -> `public sealed class`.
- [x] `PlcLifecycleManager` -> `public sealed class` (promoted from internal-collaborator).
- [x] `ImportedRecipeValidator` -> `public sealed class`.
- [x] Audit `RecipeStateManager`, `RecipeHistoryManager`, `RecipeAnalyzer`,
      `FormulaApplicationCoordinator`, `StepInitializer`, `PropertyValidator`: each stays
      `internal sealed` if only the new trio consumes it; otherwise -> public. Most should
      stay internal — only the workspace/editor/manager need to be public surface.

#### RecipeWorkspace — state holder only

- [x] Create `RecipeWorkspace`. Internal: `RecipeStateManager`, `RecipeHistoryManager`,
      `RecipeAnalyzer`, `IPlcSyncService` (only for `NotifyRecipeChanged` post-Apply).
      Public surface — **state and history, nothing else**:
      ```
      public Recipe CurrentRecipe { get; }
      public Recipe LastValidRecipe { get; }
      public bool IsDirty { get; }
      public bool IsValid { get; }
      public Result<RecipeSnapshot> Snapshot { get; }
      public bool CanUndo { get; }
      public bool CanRedo { get; }

      public Result Apply(Recipe newRecipe)   // analyze -> state update -> history push -> sync notify
      public Result Undo()
      public Result Redo()
      public Result Reset()                   // SetNewRecipe replacement
      public void MarkSaved()
      ```
      Total: 7 properties + 5 methods. ~80 lines. Single responsibility: own the editing
      session's state.

      **`Reset()` semantics (explicit, not "Apply(Recipe.Empty)"):**
      ```
      _historyManager.Clear();
      _stateManager.Reset();
      var snapshot = _analyzer.Analyze(Recipe.Empty);
      _stateManager.Update(snapshot);
      if (_syncEnabledProvider()) _syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
      return snapshot.ToResult();
      ```
      Reset clears history (does not push Recipe.Empty into it). It still notifies sync
      because the workspace's recipe identity changed and any PLC sync needs to pick that
      up. This mirrors the current `DomainFacade.SetNewRecipe()` semantics exactly.

      **`Apply(Recipe)` body** for non-Reset paths:
      ```
      var snapshot = _analyzer.Analyze(newRecipe);
      _historyManager.Push(_stateManager.Current);  // push CURRENT (pre-mutation) onto history
      _stateManager.Update(snapshot);
      if (_syncEnabledProvider()) _syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
      return snapshot.ToResult();
      ```
      Note: history-push uses pre-mutation state, matching how the current code does it
      via `RecipeEditService` calling `_historyManager.Push(...)` before applying. Confirm
      against `RecipeEditService` before locking in.

#### RecipeEditor — edit operations only

- [x] Create `RecipeEditor`. Internal: `RecipeWorkspace`, `ConfigRegistry`,
      `FormulaApplicationCoordinator`, `IPropertyParser` (or `StepInitializer` if more
      convenient — check existing layering). Public surface — **mutation operations only**:
      ```
      public Result AppendStep(int actionId)
      public Result InsertStep(int index, int actionId)
      public Result RemoveStep(int index)
      public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
      public Result RemoveSteps(IReadOnlyList<int> indices)
      public Result ChangeStepAction(int stepIndex, int newActionId)
      public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
      ```
      Each method follows the same template:
      ```
      public Result AppendStep(int actionId)
      {
          var actionResult = _registry.GetAction(actionId);
          if (actionResult.IsFailed) return actionResult.ToResult();
          var step = _stepInitializer.CreateForAction(actionResult.Value);
          return _workspace.Apply(_workspace.CurrentRecipe.AppendStep(step));
      }
      ```
      `UpdateStepProperty` is slightly heavier (parse value, validate, apply formulas)
      but follows the same shape: lookup -> build new Step -> workspace.Apply. Total:
      7 methods. ~80 lines. Single responsibility: convert user-action-id-shaped intents
      into validated `Recipe` mutations and hand them to the workspace.

#### PlcLifecycleManager — PLC orchestration only

- [x] Promote `PlcLifecycleManager` from internal-collaborator to public. Constructor
      takes `RecipeWorkspace` (to call `Apply` when reading from PLC), plus its existing
      `IS7Service`, `IPlcSyncService`, `ImportedRecipeValidator`, etc. Public surface:
      ```
      public bool IsSyncEnabled { get; }
      public bool IsConnected { get; }
      public bool IsRecipeActive { get; }
      public IObservable<PlcExecutionInfo> ExecutionState { get; }
      public PlcSyncStatus SyncStatus { get; }
      public DateTimeOffset? LastSyncTime { get; }
      public IObservable<Result<PlcSessionSnapshot>> PlcState { get; }
      public event Action<Recipe, Recipe>? PlcRecipeConflictDetected;

      public Task<Result> EnableSync(PlcConfiguration config)
      public Task DisableSync()
      public Task<Result> LoadRecipeFromPlcAsync()       // calls _workspace.Apply on success
      public Result ResolveConflict(bool keepLocal)      // also calls _workspace.Apply
      ```
      Single responsibility: PLC lifecycle.

#### File I/O and clipboard live where they belong

- [x] `CsvService` (concrete, no interface, public) keeps `LoadAsync`, `SaveAsync`. UI's
      `RecipeMutationCoordinator` orchestrates the flow:
      `csvService.LoadAsync(path) -> validator.Validate(recipe) -> workspace.Apply(recipe)`.
      The orchestration logic that currently lives in `DomainFacade.LoadRecipeAsync` (about
      30 lines: history clear, mark-saved, sync notify) moves into a small private helper
      either on `RecipeWorkspace` (e.g. a separate `Reset()` + `Apply(...)` flow) or as a
      thin extension method. Avoid creating a new "RecipeFileService" class — the call site
      is one place (UI coordinator).
- [x] `ClipboardService` (concrete, no interface, public) exposes serialize/deserialize.
      UI calls them directly. Validation flow uses `ImportedRecipeValidator` directly.
- [x] Delete `DomainFacade.cs` entirely.

#### UI rewiring

- [x] Update `RecipeMutationCoordinator` constructor: parameters become
      `RecipeWorkspace workspace`, `RecipeEditor editor`, `PlcLifecycleManager plc`,
      `CsvService csv`, `ClipboardService clipboard`, `ImportedRecipeValidator validator`,
      plus existing `AppConfiguration`, `RecipeQueryService`, `MessagePanelViewModel`.
      Inside, route each call to the right backend:
      - mutations -> `_editor.X()`
      - undo/redo/save-marker/snapshot -> `_workspace.X()`
      - PLC -> `_plc.X()`
      - file/clipboard -> `_csv.X()` / `_clipboard.X()`
- [x] Update `RecipeQueryService` and `RecipeStepCoordinator` similarly: queries hit
      `RecipeWorkspace`, mutations hit `RecipeEditor`.
- [x] Update `DomainDi` to register `RecipeWorkspace`, `RecipeEditor`, `PlcLifecycleManager`
      as singletons. Drop `DomainFacade`.

#### Test rewiring

Tests are a safety net for the refactor, not a deliverable to over-specify. Pattern:
build, observe what breaks, fix the touched test setup, re-run. The items below
enumerate the call sites known to break at compile time so the executor is not
surprised.

- [x] `Tests/Core/Helpers/RecipeTestDriver.cs` — currently constructs `DomainFacade`
      directly. Retype to take `RecipeWorkspace` + `RecipeEditor`; state queries route
      to workspace, mutations route to editor.
- [x] `Tests/Core/Helpers/CoreFixture.cs` — build the new trio instead of `DomainFacade`.
- [x] `Tests/Csv/Helpers/CsvTestHelper.cs` — same DI changes.
- [x] `Tests/UI/RecipeMutationCoordinatorTests.cs` — constructor of the system under
      test changes; setup edits expected.
- [x] `Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs` — replace
      `services.GetRequiredService<DomainFacade>()` calls with the new trio.
- [x] `Tests/Domain/DomainFacadeReconnectTests.cs` — rename to
      `PlcLifecycleManagerReconnectTests.cs`. The system under test becomes
      `PlcLifecycleManager` directly. The `StubIs7Service` property-setter pattern can
      stay — it is justified by the public-API testing surface; do not refactor it
      unless build forces it.
- [x] Build green. Integration test assertions should not need rewriting — only
      setup/wiring changes.

### Task 6: Feature-folder reorganization + Config DTO simplification

**Files:**

Folder moves (into `SemiStep.Core`):

- Move: `SemiStep/TypesShared/Core/{Recipe.cs, Step.cs, PropertyValue.cs, RecipeSnapshot.cs,
  PropertyId.cs, PropertyType.cs, PropertyTypeMapping.cs, ActionDefinition.cs,
  ActionPropertiesDefinition.cs, CellState.cs, IPropertyParser.cs, LoopInfo.cs,
  PropertyDefinition.cs, StepValueParser.cs}` -> `SemiStep/Core/Recipe/`
- Move: `SemiStep/Core/Analysis/*` -> `SemiStep/Core/Recipe/Analysis/`
- Move: `SemiStep/Core/Formulas/*` -> `SemiStep/Core/Recipe/Formulas/`
- Move: `SemiStep/Core/Services/*` (PropertyParser, PropertyValidator, StepInitializer)
  -> `SemiStep/Core/Recipe/`
- Move: `SemiStep/Domain/{State, Helpers, RecipeWorkspace.cs}` -> `SemiStep/Core/Recipe/`
- Move: `SemiStep/Csv/*` -> `SemiStep/Core/Recipe/Import/`
- Move: `SemiStep/Clipboard/*` -> `SemiStep/Core/Recipe/Clipboard/`
- Move: `SemiStep/TypesShared/Plc/*` (settings + connection) -> `SemiStep/Core/Plc/Configuration/`,
  `SemiStep/TypesShared/Plc/Memory/*` (DataDbLayout, ExecutionDbLayout, ManagingDbLayout)
  -> `SemiStep/Core/Plc/Configuration/Memory/`,
  `SemiStep/TypesShared/Plc/{PlcConnectionState, PlcSessionSnapshot, PlcExecutionInfo, PlcManagingAreaState, PlcRecipeData, PlcSyncStatus}`
  -> `SemiStep/Core/Plc/State/`
- Move: `SemiStep/S7/Sync/*` -> `SemiStep/Core/Plc/Sync/`
- Move: `SemiStep/S7/{Facade, Protocol, Serialization, S7Driver.cs, IS7Driver.cs,
  IS7Transport.cs, S7Di.cs}` -> `SemiStep/Core/Plc/S7/`
- Move: `SemiStep/Domain/{Facade/PlcLifecycleManager.cs}` -> `SemiStep/Core/Plc/`
- Move: `SemiStep/Domain/Plc/IPlcSyncService.cs` (placed there in Phase 3) -> `SemiStep/Core/Plc/`.
  Note: if Phase 8 ran before Phase 6, `IS7Service.cs` is already gone; if not,
  `SemiStep/TypesShared/Domain/IS7Service.cs` moves to `SemiStep/Core/Plc/` here and
  Phase 8 deletes it from there.
- Move: `SemiStep/TypesShared/Config/*` -> `SemiStep/Core/Configuration/`
- Move: `SemiStep/TypesShared/Style/GridStyleOptions.cs` -> `SemiStep/Core/Configuration/`
- Move: `SemiStep/Config/{Loaders, Mapping, Validation, Facade, Dto}` -> `SemiStep/Core/Configuration/`
  (subfolders)
- Move: `SemiStep/TypesShared/Results/*` -> `SemiStep/Core/Shared/`
- Delete: empty `SemiStep/{TypesShared, Core/Services, Core/Facade, Core/Analysis,
  Core/Formulas, Domain, Csv, Clipboard, S7, Config}` folders after moves.

Namespace updates (per moved file): the old `Domain.X` and `Core.X`, `TypesShared.X`,
`Config.X`, `Csv.X`, `S7.X`, `ClipBoard` namespaces consolidate. Recommended new shape:

- `SemiStep.Core.Recipe`
- `SemiStep.Core.Recipe.Analysis`
- `SemiStep.Core.Recipe.Formulas`
- `SemiStep.Core.Recipe.Import`
- `SemiStep.Core.Recipe.Clipboard`
- `SemiStep.Core.Plc`
- `SemiStep.Core.Plc.Configuration`
- `SemiStep.Core.Plc.State`
- `SemiStep.Core.Plc.Sync`
- `SemiStep.Core.Plc.S7`
- `SemiStep.Core.Plc.S7.Protocol`
- `SemiStep.Core.Plc.S7.Serialization`
- `SemiStep.Core.Configuration`
- `SemiStep.Core.Configuration.Loaders`
- `SemiStep.Core.Configuration.Validation`
- `SemiStep.Core.Shared`

Config DTO simplification (minimal — see Solution Overview rationale):

- Delete: `SemiStep/Config/Mapping/GroupMapper.cs` (1:1 dictionary loop with no
  validation; replaced by 4 inline lines in `ConfigFacade.MapToDomain`).
- Keep: all 18 files in `Config/Dto/` and the remaining 5 mappers (`PropertyMapper`,
  `ColumnMapper`, `ActionMapper`, `GridStyleMapper`, `ConnectionMapper`). They do real
  required-field validation, nested-to-flat structural transformation, or
  string-parsing — not ceremony. Removing them would either break existing user YAML
  schemas or re-introduce intermediate types under different names.
- Modify: `SemiStep/Core/Configuration/Facade/ConfigFacade.cs` — replace `GroupMapper.Map(...)`
  call with the 4-line inline loop:
  ```
  var groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);
  foreach (var (groupId, items) in groupsDto)
      groups[groupId] = new GroupDefinition(groupId, items.AsReadOnly());
  ```
- No changes to YAML schemas. No changes to user config files. No `[YamlMember]` attributes
  on domain types.
- No changes to `Tests/Config/*` other than namespace updates from the folder reorg.

Tasks:

#### Folder + namespace move

- [ ] Make folder moves in chunks: Recipe first, Plc next, Configuration last. After each
      chunk: build, fix `using` directives in dependent files (Find/Replace per old namespace).
- [ ] After all moves: build green. Run all tests.
- [ ] Update `SemiStep.Core.csproj` if any explicit `<Compile Update>` or `<None Update>`
      paths reference the old structure. Same for `SemiStep.UI.csproj`.
- [ ] Update `AGENTS.md` "File Layout" and namespace examples to match.
- [ ] Update `SemiStep/Tests/YamlConfigs/` paths only if testing helpers reference old
      namespaces (likely none — YAML files are content).

#### Config DTO simplification (minimal)

- [ ] In `ConfigFacade.MapToDomain`, replace the `GroupMapper.Map(...)` call with the
      4-line inline loop shown above.
- [ ] Delete `Config/Mapping/GroupMapper.cs`.
- [ ] Build green. Run `Tests/Config/*` — should pass without modification (only the
      mapping path changed, the resulting `GroupDefinition` records are identical).

### Task 7 (optional): Unify UI thread marshalling

This phase is optional. It does not address architectural debt; it addresses cognitive
load from three parallel marshalling patterns currently coexisting in the UI layer.
Skip if the friction is not material.

**Current state:**

- **Pattern 1 — `.ObserveOn(RxApp.MainThreadScheduler)` on consumer.** Used in 8 ViewModels:
  `MainWindowViewModel`, `RecipeFileViewModel`, `RecipeCommandsViewModel`,
  `RecipeGridViewModel`, `ClipboardViewModel`, `PlcMonitorViewModel`,
  `RecipeMutationCoordinator`. The default ReactiveUI way — clean and consistent.
- **Pattern 2 — self-marshalling inside the VM.** Only `MessagePanelViewModel` does this,
  per `AGENTS.md:97`. Every public mutating method (`AddError`, `AddWarning`,
  `RefreshReasons`, `Clear`) calls `Dispatcher.UIThread.CheckAccess()` and `Post`s if
  off-thread. **Justified** because this VM is called from many sites, some on UI
  thread, some not — pushing the marshalling responsibility to every caller would be
  worse.
- **Pattern 3 — ad-hoc `Dispatcher.UIThread.Post`.** One site:
  `RecipeMutationCoordinator.OnPlcRecipeConflictDetected:263`. The handler runs on the
  PLC sync thread and calls `_plcRecipeConflictDetected.OnNext(...)` into a `Subject` that
  is later observed and bound to UI. The current code marshals **at the producer** — it
  wraps the `OnNext` in `Dispatcher.UIThread.Post`. This is the inconsistent one.

**Files:**

- Modify: `SemiStep/UI/Coordinator/RecipeMutationCoordinator.cs` (replace ad-hoc
  `Dispatcher.UIThread.Post` with `.ObserveOn(RxApp.MainThreadScheduler)` at the
  consumer side of `_plcRecipeConflictDetected`)
- Modify: `SemiStep/AGENTS.md` (Threading section — document the simplified two-pattern
  policy)

- [ ] In `RecipeMutationCoordinator`, change `OnPlcRecipeConflictDetected` to call
      `_plcRecipeConflictDetected.OnNext((local, plc))` directly without `Dispatcher.UIThread.Post`.
- [ ] Verify the consumer in `MainWindowViewModel:67` already has
      `.ObserveOn(RxApp.MainThreadScheduler)` on the conflict subscription (it does, per
      audit). No consumer-side change needed.
- [ ] Run an integration test that triggers conflict detection (or test against PLC
      simulator). The conflict dialog must still open on the UI thread without exceptions.
- [ ] Update `SemiStep/AGENTS.md` Threading section: state the policy as
      "Pattern A — `.ObserveOn(RxApp.MainThreadScheduler)` at the subscription site is
      the default for marshalling background work onto the UI thread.
      Pattern B — self-marshalling (`Dispatcher.UIThread.CheckAccess` + `Post` inside the
      callee) is reserved for VMs/services that are widely called from mixed thread
      contexts; `MessagePanelViewModel` is the only example. Do not introduce ad-hoc
      `Dispatcher.UIThread.Post` calls at producer sites."
- [ ] Build green, all tests pass.

### Task 8: Decompose IS7Service into role-based interfaces

`IS7Service` currently has 8 mixed members (connection state, query methods, observable
streams, events) — a classic wide-facade interface. This phase decomposes it into three
narrow role interfaces, one production class implementing all three, and consumers
taking only the role they need. This satisfies Interface Segregation properly.

**Files:**

- Create: `SemiStep/Domain/Plc/IS7Connection.cs` (Connect/Disconnect/IsConnected/StateChanged)
- Create: `SemiStep/Domain/Plc/IS7Reader.cs` (ReadRecipeFromPlcAsync/ReadManagingAreaAsync)
- Create: `SemiStep/Domain/Plc/IS7ExecutionStream.cs` (ExecutionState/IsRecipeActive)
- Delete: `SemiStep/TypesShared/Domain/IS7Service.cs` (untouched since Phase 3)
- Modify: `SemiStep/S7/Facade/S7Service.cs` (implements all three role interfaces)
- Modify: `SemiStep/Domain/Facade/PlcLifecycleManager.cs` (constructor takes
  `IS7Connection` + `IS7Reader` instead of `IS7Service`)
- Modify: `SemiStep/S7/Sync/PlcSyncCoordinator.cs` (audit which role(s) it consumes)
- Modify: `SemiStep/S7/Sync/PlcSyncExecutor.cs` (current ctor takes `IS7Service`;
  replace with the narrower role(s) it actually needs — likely `IS7Reader` for write
  verification + `IS7Connection` for connection state)
- Modify: `SemiStep/S7/S7Di.cs` (register `S7Service` as itself plus three forwarding
  factories for the role interfaces — see DI snippet below)
- Modify: `SemiStep/Tests/Helpers/StubIs7Service.cs` (rename to `StubS7Service`,
  declare `: IS7Connection, IS7Reader, IS7ExecutionStream`)
- Modify: `SemiStep/Tests/S7/Helpers/StubIs7ServiceForSync.cs` (same treatment — rename
  and re-declare against the three role interfaces)

#### Interface definitions

- [ ] `IS7Connection`:
      ```
      public interface IS7Connection : IAsyncDisposable
      {
          bool IsConnected { get; }
          event Action<PlcConnectionState>? StateChanged;
          Task ConnectAsync(PlcConnectionSettings settings);
          Task DisconnectAsync();
      }
      ```
- [ ] `IS7Reader`:
      ```
      public interface IS7Reader
      {
          Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync();
          Task<Result<Recipe>> ReadRecipeFromPlcAsync();
      }
      ```
- [ ] `IS7ExecutionStream`:
      ```
      public interface IS7ExecutionStream
      {
          bool IsRecipeActive { get; }
          IObservable<PlcExecutionInfo> ExecutionState { get; }
      }
      ```

#### Implementation and DI

- [ ] Update `S7Service` declaration: `public sealed class S7Service : IS7Connection, IS7Reader, IS7ExecutionStream`.
- [ ] Delete `IS7Service.cs`.
- [ ] Update `S7Di`: register `S7Service` as singleton, then forward each role to the
      same instance:
      ```
      services.AddSingleton<S7Service>();
      services.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<S7Service>());
      services.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<S7Service>());
      services.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<S7Service>());
      ```
      Note: this is correct disposal-wise. Only `S7Service` is owned by the container
      (registered with type-mapped factory); the three forwarding lambdas return the
      already-tracked instance, so `IAsyncDisposable` fires once via the `S7Service`
      registration. Do **not** add `RemoveAll` / `Replace` calls — they break this.

#### Consumer rewiring

- [ ] `PlcLifecycleManager` constructor: depend on `IS7Connection` + `IS7Reader`. Drop
      the `IS7Service` parameter. The connection lifecycle and PLC-read flows are
      separate concerns, so taking both is honest about what the class does.
- [ ] `PlcSyncExecutor` constructor: currently `IS7Service connectionService`. Audit the
      method bodies to determine which role members are actually accessed; depend on
      that narrower set (likely `IS7Connection` for state checks plus `IS7Reader` for
      write-verification reads).
- [ ] `PlcSyncCoordinator`: audit the same way and narrow the parameter type(s).
- [ ] Confirm `RecipeMutationCoordinator` does not depend on `IS7Service` directly
      (verified via grep: it does not — it goes through `PlcLifecycleManager`).

#### Test rewiring

- [ ] `Tests/Helpers/StubIs7Service.cs` -> `StubS7Service` declared against the three
      role interfaces. Keep the property-setter pattern (`ManagingAreaToReturn`,
      `RecipeToReturn`, `IsConnectedOverride`) as is.
- [ ] `Tests/S7/Helpers/StubIs7ServiceForSync.cs` -> `StubS7ServiceForSync` declared
      against the same three role interfaces.
- [ ] Update DI fixtures (`CoreTestHelper`, `CsvTestHelper`, any other helper that
      registered `IS7Service`) to register the new stubs against the three role
      interfaces — using the same forwarding pattern as production `S7Di`.
- [ ] Build green. `PlcLifecycleManagerReconnectTests` (rewritten in Phase 5) should
      survive — its interactions go through the role contract.

### Per-task verification

After each phase, run:

- [ ] `dotnet build SemiStep/SemiStep.slnx` — green
- [ ] `dotnet test SemiStep/Tests/Tests.csproj` — all pass
- [ ] `dotnet run --project SemiStep/UI/SemiStep.UI.csproj` (post-Phase 2) — app launches,
      load a recipe from `ConfigFiles/`, perform a few mutations, save, verify file is
      readable on next launch.

After Phase 6 (and optionally Phase 7 + Phase 8) final verification:

- [ ] Run `dotnet format SemiStep/SemiStep.slnx` (per `AGENTS.md` pre-commit hook).
- [ ] Spot-check public API surface remains unchanged from UI's perspective:
      `RecipeWorkspace` (state) + `RecipeEditor` (mutations) + `PlcLifecycleManager` (PLC) +
      `CsvService` (file I/O) + `ClipboardService` (clipboard) together expose every
      method/property/event that `DomainFacade` did.
- [ ] If a real PLC is available, smoke-test connect / read recipe / write recipe /
      detect conflict / reconnect — to confirm PLC byte layout was not disturbed by the
      file moves through `S7/Serialization/`.
- [ ] Update `Docs/03-data-model.md` if internal model documentation references old type
      paths.
- [ ] Move this plan to `Docs/plans/completed/`.

## Post-Completion

*Items requiring manual intervention or external systems — informational only*

- **Reviewer guidance.** Each phase is a separate commit. PR strategy: either one PR with
  the 6 core commits (preserves bisectability) or six small PRs (preserves per-phase
  isolation in CI). Phase 7 (UI threading) and Phase 8 (IS7Service decomposition) ship
  as their own commits/PRs and may be deferred or interleaved without blocking the core
  refactor.
- **AGENTS.md drift.** After Phase 2 and Phase 6, `AGENTS.md` must reflect the new layout.
  Verify the build/test sections, file-layout section, and any folder-tree diagrams.
- **External readme.** `readme.md` at repo root may reference the old structure for getting
  started — verify and update if so.
- **Performance smoke test.** Manually load a large-ish recipe (50+ steps) before and after
  the refactor; observe load time and UI mutation latency. Refactor should be neutral or
  slightly positive (fewer indirection layers).
- **PLC integration test.** If a Siemens S7 PLC or PLCSIM is available, run the full
  connect/read/write/sync cycle. The byte-level codecs (`RecipeConverter`, `ArrayCodec`,
  `ManagingAreaCodec`, `ExecutionStateCodec`) are moved but not modified — but it is worth
  one round of hardware verification before considering this fully shipped.
