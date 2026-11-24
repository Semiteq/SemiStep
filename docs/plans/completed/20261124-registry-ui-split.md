# Registry UI split and config defects through Result

## Behavior
- Today two kinds of configuration defect pass `ConfigFacade.LoadAndValidateAsync` and only fail later,
  when the DI container builds `RecipeMetadataRegistry`: the four string `max_length` rules, and an action
  reference graph that `ActionTreeResolver` rejects although the validators accept it. The startup error
  window never lists them. After the change both fail the config load and appear in the startup error
  window, next to the other config errors of the same load.
- Today the app starts with zero primary actions and fails only when the operator adds a step
  (`RecipeCoordinator.GetDefaultActionId`). After the change such a config does not start, and the startup
  error window names the defect.
- Combo-box items (the action column and every group-bound column) are built in `SemiStep.UI`, not in Core.
  `ComboBoxItemViewModel` moves to the UI project. `RecipeMetadataRegistry` loses both combo-box methods and
  keeps one Result-based lookup contract.
- Scope guard:
  - The visible UI does not change. Every combo box shows the same items in the same order with the same
    text. Group items stay ordered by id. Action items stay in `GetAllActions` order with subactions
    excluded. Rows of the same group share one list instance, as today.
  - A config that starts the app today and defines at least one primary action still starts it. The three
    shipped presets (`ConfigFiles/MBE`, `MOCVD`, `RIE`) load unchanged.
  - The string max length that the PLC codec and the grid cells use (`GetStringMaxLength`) stays the same
    value.
  - The startup window stays English and shows `Message` text only.
  - The registry keeps `InvalidOperationException` throws only where it must compute a value from its
    input: the resolved primary actions and the single string max length. These are assertions of input
    that config validation already checked.
  - `RecipeCoordinator.GetDefaultActionId` keeps its throw as an assertion.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Valid config (Standard test case, shipped presets) | Loads. The grid shows the same combo items in the same order as before | agent: scope guard | A1, A3, A4 |
| No property has `system_type: string` | Load fails with one error for this rule. The other three rules have nothing to check | agent: rule moved from `RecipeMetadataRegistry.ResolveStringMaxLength` | A2 |
| A string property has no `max_length` | Load fails with one error per such property. Each error names the property | agent: same source | A2 |
| A string property has `max_length` of 0 or less | Load fails with one error per such property. Each error names the property and the value | agent: same source | A2 |
| String properties have two or more distinct positive `max_length` values | Load fails with one error that carries the distinct values. Missing and non-positive values do not count toward this rule | agent: same source | A2 |
| A `max_length` defect and an unknown column reference in one config | Both errors appear in one report | agent: same stage as `CrossReferenceValidator` | A2 |
| Every actions file is empty | Load fails with the zero-primary-actions error. The loader's empty-file warnings stay in the result | operator: issue comment on #111 | A2 |
| One actions file is empty, the others define actions | Loads with the empty-file warning as today. No new error | operator: issue comment on #111 | A2 |
| Every action is a subaction, in a 2-cycle | Load fails at cross-reference validation with a cycle or orphan error. No zero-primary error appears | agent: see Caveats | A2 (new 2-cycle overlay case in Task 2) |
| One action lists the same column key twice with different `property_type_id` | Load fails with the resolver's error. Today the registry throws after a successful load | agent: see Caveats | A2 |
| One action lists the same column key twice with the same `property_type_id` | Loads, as today. The resolver merges the two entries | agent: paired control for the row above | A2 |
| UI asks for the items of a group that no action references | Gets an empty list. Validation rejects unknown `group_name` first, so a valid config never shows such a column with items | agent: `CrossReferenceValidator.ValidateActionReferences` | A3 |
| Code builds the registry from an `AppConfiguration` that never passed `ConfigFacade` (tests, hand-built configs) | The registry throws `InvalidOperationException` for an unresolvable action graph or an invalid string max length. A column that references an unknown property no longer throws there | agent: issue allows assertions of validated input; `CrossReferenceTests` with the `MissingPropertyReference` overlay covers the facade side | existing registry tests |

## Decisions
- The four string `max_length` rules get one home, a new validator in `Configuration/Validation` that reads
  the property DTOs. The registry keeps one assertion that the string properties yield exactly one positive
  value. The registry does not repeat the four rule checks or their messages. Rejected: putting the rules
  in `DefaultValueValidator`, because they constrain property types, not action default values. Rejected:
  putting them in `PropertiesSectionLoader`, because it runs per file, while two rules need all property
  files together.
- The four rules interact this way. No string property: one error, and the other rules do not run. A
  missing value and a value of 0 or less each give one error per property. The disagreement rule compares
  only positive values and fires when there are two or more distinct positive values. A property with a
  missing or non-positive value is reported once, by its own rule.
- The `max_length` validator runs in the same stage as `CrossReferenceValidator`, and the facade merges
  both results. A config with a reference error and a `max_length` error reports both. Rejected: a separate
  stage after `DefaultValueValidator`. It would hide `max_length` errors behind reference errors.
- `RecipeMetadataRegistry.EnsureColumnPropertyReferencesResolve` is deleted, not kept as an assertion.
  `CrossReferenceValidator.ValidateColumnReferences` already reports the same defect, and the registry does
  not need a value from the check.
- A new post-mapping check runs `ActionTreeResolver.Resolve` over the mapped actions in `ConfigFacade`,
  after `MapToDomain`. It runs next to `PlcConfigurationValidator`, and the facade merges both results.
  It passes the resolver's own failure through unchanged. It reports zero resolved primary actions as a new
  error. The check counts the resolved primaries, not the raw action count and not per file. Rejected: a
  per-file loader check, because one empty file among several must not fail the load. Rejected: a raw
  pre-resolution count. `ActionTreeResolver` works on domain types, so the check cannot run before
  `MapToDomain`.
- The resolver runs twice at startup, once in the new check and once in the registry constructor. The
  registry still resolves its own input. Rejected: carrying resolved actions in `AppConfiguration`. That
  changes what `AppConfiguration.Actions` means for `CsvRowConverter` and the emulator.
- New errors are `internal sealed` typed errors with public properties, the shape of
  `Configuration/Loaders/MissingKeyError`. They exist only on the English startup path, so they need no
  resx entry. A public type would fail `CoreErrorLocalizationCoverageTests`. Resolver failures stay the
  plain errors `ActionTreeResolver` already returns.
- The UI provider builds every list once, when it is constructed. It collects the group names from the
  properties of `GetAllActions()` and resolves each through `GetGroup`, the way `ColumnWidthCalculator`
  already does. It keeps one list per group, ordered by id, in a case-insensitive map, plus one action list.
  After construction it is read-only, so the lazy caches and their mixed thread-safety go away and the
  provider needs no thread-safety model. Rejected: a new group enumeration on the registry. The registry's
  existing primitives already give every group a column can show. Rejected: lazy per-group caching. It
  brings the concurrency question back.
- The provider is a DI singleton in `AddUi()` and depends on `RecipeMetadataRegistry` only. There is one
  provider per registry. Tests take the provider from the same container or fixture as the registry they
  use. Rejected: injecting `AppConfiguration` as well, which would give one class two sources for the same
  data.
- `ComboBoxItemViewModel` keeps its name and shape, and its namespace changes to the UI project's
  `RecipeGrid` namespace. Renaming it would touch every consumer and add nothing.

## Caveats
- Read from code: an all-subactions config cannot pass cross-reference validation.
  `CrossReferenceValidator.ValidateReferenceGraph` rejects every subaction that no column targets. Targets
  must point at subactions. With no primary actions, every subaction then needs an incoming target from
  another subaction. A finite graph where every node has an incoming edge contains a cycle, and
  `ValidateNoCycles` rejects it. `ConfigFacade.LoadAndValidateAsync` stops at a failed cross-reference
  result. So the zero-primary error is reachable only when every actions file is empty.
  `MergeSectionLoader.LoadFileAsync` turns an empty file into a warning and continues.
- Read from code: `CrossReferenceValidator.TryFindSharedColumn` flags a repeated column key only when the
  two path signatures differ. Action columns are a YAML list (`ActionDto.Columns`). Neither
  `ActionsSectionLoader` nor `ActionMapper.TryMap` rejects a repeated key. So one action with the same key
  twice and two different `property_type_id` values passes all validators.
  `ActionTreeResolver.AppendColumn` then fails, and today `RecipeMetadataRegistry.ResolvePrimaryActions`
  throws. The new post-mapping check routes this through `Result`. The invalid-config test in Task 2
  proves it, and its paired control with equal property types proves the check rejects only the conflict.
- Read from code: `ConfigFacade.Propagate` copies the reasons of earlier stages into a failed result.
  `Standalone/EmptyYamlFile` has no string property, so after Task 1 it fails the load. Its test
  (`FileSystemErrorTests`) checks only the empty-file warning, which still arrives through `Propagate`. If
  the test breaks, give the fixture's properties file a string property with `max_length`. Do not weaken
  the rule.
- `FluentResults` `Result.Merge` keeps the errors and warnings of every input. The repository already
  depends on this in `CrossReferenceValidator.Validate`, `ConfigFacade.LoadAllSectionsAsync` and
  `MergeSectionLoader.LoadAsync`, where an empty file's warning survives the merge.
  ASSUMPTION: merging the stage results keeps every error of both validators; fallback: collect the errors
  of both into one list and fail once with all of them; checked by: the Task 1 test that expects both a
  `max_length` error and a reference error.

## Constraints and non-goals
- `AppConfiguration` keeps its shape. `RecipeMetadataRegistry` keeps every Result-based lookup and
  `GetStringMaxLength`.
- No change to the existing validator rules, the wording of existing errors, or the localization of
  operator errors.
- Threat model: hand-edited YAML in the config folder. A registry built from a hand-made
  `AppConfiguration` gets an assertion, not a report.
- Tests assert error types, typed properties and counts, not wording (`docs/architecture/error-reporting.md`,
  "What tests assert on errors").
- Provider tests compare against independent production output: action items equal `GetAllActions()`
  mapped to `(Id, UiName)`, and group items equal the group's `Items` from `GetGroup` ordered by key. A test
  never compares the provider with itself.
- Invalid-config tests use the overlay pattern: `YamlConfigs/Standard/` plus only the changed files from
  `YamlConfigs/Invalid/<CaseName>/`.
- No new abstractions beyond the one UI provider and the new validators. No interface for the provider: it
  has one implementation and its tests build it from a real registry.

## Gates
- `ShippedPresetLoadTests` fails after Task 1 or Task 2. Stop and ask the operator: a shipped preset breaks a
  rule, and changing a shipped config is a product decision.

## Acceptance
- A1: `dotnet build SemiStep.slnx` → 0 errors, 0 warnings
- A2: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` → all pass. The
  run includes the new invalid-config cases for every Flow branch marked A2. Each case asserts the error type
  and its properties, or the warning, never the wording. Exception: the resolver's own failure is an untyped
  plain error, so its case asserts that the load fails with no zero-primary error, and the paired control
  asserts that the load succeeds.
- A3: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` → all pass,
  including the new provider tests. In the existing UI combo-box tests the assertions are unchanged; the
  source of the expected item list moves from the registry to the provider.
- A4: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → all pass, 0 failed
- A5: `grep -rn "ComboBox" SemiStep/SemiStep.Core --include=*.cs | wc -l` → `0` (11 on master, 2026-11-24)
- A6: `grep -c "ConcurrentDictionary" SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs` → `0`
- A7: `grep -c "throw new InvalidOperationException" SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs`
  → `2` throw sites (6 on master, 2026-11-24)
- A8: `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
- manual smoke:
  1. `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj` with the RIE preset → the action combo box
     lists the same actions in the same order as on master
  2. In the same run, open a group-bound cell → the dropdown lists the same items in the same order as on
     master
  3. Copy a preset, set one string property's `max_length` to 0, start with that folder → the startup error
     window lists the `max_length` error and the app does not open the main window
  4. Copy a preset, empty every file in `actions/`, start with that folder → the startup error window lists
     the zero-primary-actions error
- not visible to headless tests: the startup error window itself (steps 3 and 4)

---

## Context
- `SemiStep.Core/Recipes/RecipeMetadataRegistry.cs`: the constructor, `ResolvePrimaryActions`,
  `EnsureColumnPropertyReferencesResolve`, `ResolveStringMaxLength` and its three helpers,
  `GetComboBoxItems`, `GetActionComboBoxItems`
- `SemiStep.Core/Recipes/ComboBoxItemViewModel.cs`: moves to `SemiStep.UI/RecipeGrid/`
- `SemiStep.Core/Configuration/Facade/ConfigFacade.cs`: `LoadAndValidateAsync` stage order, `Propagate`
- `SemiStep.Core/Configuration/Validation/`: `CrossReferenceValidator`, `DefaultValueValidator`. Read
  `PlcConfigurationValidator` for the post-mapping stage
- `SemiStep.Core/Recipes/ActionTreeResolver.cs`: `Resolve`, failure cases in `Walk` and `AppendColumn`
- Typed error shape: `SemiStep.Core/Configuration/Loaders/MissingKeyError.cs`, `DuplicateKeyError.cs`
- Group collection pattern for the provider: `SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`
- Production consumers of the combo-box methods or of the registry handed to rows and cell factories:
  `RecipeGrid/ColumnBuilder`, `RecipeGrid/ComboBoxCellFactory`, `RecipeGrid/RecipeRowViewModel` (static
  `BuildGroupItemsByColumn`, reached through `GetActionMetadata`), `RecipeGrid/CanonicalRecipeGridSurface`,
  `RecipeGrid/Transposed/TransposedRecipeGridSurface`, `RecipeGrid/Transposed/StepColumnViewModel`,
  `RecipeGrid/Transposed/ParameterCellViewModelFactory`. Type-only users: `ComboBoxItemSelectionConverter`,
  `ComboBoxItemMultiSelectionConverter`, `Transposed/ComboBoxCellViewModel`,
  `Transposed/ComboBoxDisplayTextConverter`, `Transposed/TransposedCellTemplateFactory`
- `SemiStep.UI/UiDi.cs`: `AddUi()` registration
- Tests that change:
  - `Tests/Core/Unit/Recipes/RecipeMetadataRegistryTests`: combo-box tests move to a UI test class. The
    cycle and conflicting-type throw tests stay
  - `Tests/Core/Unit/Properties/RecipeMetadataRegistryStringMaxLengthTests`: the four throw tests become
    config validation cases. The tests for a valid max length value stay
  - `Tests/Core/Unit/Properties/RecipeMetadataRegistryColumnPropertyReferenceTests`: delete the fact that
    expects a throw for a dangling column property reference. The two non-throw facts stay
  - `Tests/Config/Integration/NestedActions/RieEtchManualBranchConfigTests`: reads action ids through
    `GetActionComboBoxItems`; switch it to `GetAllActions`, which has the same order
  - `Tests/UI/Helpers/UIFixture`: exposes one provider built from the fixture's `RecipeMetadataRegistry`
    and passes it wherever production code now takes it
  - UI tests that build rows, cell factories, surfaces or step columns directly change their setup where a
    constructor now takes the provider; their assertions stay: `ColumnBuilderIdempotencyTests`,
    `GroupComboBoxRecyclingTests`, `RecipeRowViewModelTests`, `RecipeGridStringMaxLengthTests`,
    `RecipeGrid/ChangedCellClickResolverTests`, `RecipeGrid/ExecutionHighlightTrackerJumpTests`,
    `RecipeGrid/GridFactoryFontTests`, `RecipeGrid/RowExecutionClassesStampingTests`,
    `RecipeGrid/RecipeGridSurfaceFailLoudTests`, `RecipeGrid/RecipeRowSelectorEditTests`,
    `RecipeGrid/Transposed/TransposedSelectorEditTests`, `RecipeGrid/Transposed/StepColumnViewModelTests`
  - Callers of `GetActionComboBoxItems` in `RecipeGrid/Transposed/TransposedComboEditingTests`,
    `TransposedEditingTests` and `ParameterCellViewModelTests`: the expected list comes from the provider.
    `ParameterCellViewModelTests` compares against `GetAllActions()` mapped to `(Id, UiName)`
  - `Tests/UI/*` that import `ComboBoxItemViewModel` from Core change their using
- Fixture at risk: `Tests/YamlConfigs/Standalone/EmptyYamlFile` (see Caveats). No other test loads a config
  through `ConfigFacade` and expects success without a string property or without actions. Tests that
  write config files inline (`ConfigFolderPolicyTests`, `ConfigDuplicateKeyTests`) start from the Standard
  case or call a section loader directly
- The emulator loads config through `ConfigFacade` (`SemiStep.Tools.PlcEmulator/EmulatorStartup`), so it
  gets the new checks with no change
- Test traits: new config tests `Component=Config`, `Category=Integration`. New provider tests
  `Component=UI`, `Area=RecipeGrid`, `Category=Unit`

## Tasks

### Task 1: String max_length rules move to config validation

**Goal:** the four `max_length` rules fail the config load with typed errors, and the registry keeps one assertion
**Entry:** `RecipeMetadataRegistry.ResolveStringMaxLength`, `ConfigFacade.LoadAndValidateAsync`
**Likely touches:** `Configuration/Validation/` (new validator, new error types), `ConfigFacade.cs`,
`RecipeMetadataRegistry.cs`, `Tests/Config/Integration/Validation/`, `Tests/YamlConfigs/Invalid/`,
`Tests/Core/Unit/Properties/`

- [x] add the validator with the four rules over the property DTOs, as the Decisions define their
      interaction. Match `system_type` case-insensitively, as the registry does today. Each rule reports its
      own typed error
- [x] run it in the `CrossReferenceValidator` stage of `ConfigFacade` and merge both results
- [x] the registry checks string max length with one assertion only, and no longer checks column property
      references itself
- [x] tests: each rule fails the load of an overlay case with its error type and properties. A config
      with a `max_length` defect and an unknown column reference reports both errors. A registry built from
      an invalid hand-made config still throws for an invalid string max length. A dangling column property
      reference in a hand-made config no longer throws (its throw fact is deleted; no replacement test). The valid max length tests still return the same
      value
- [x] run tests - must pass before the next task

### Task 2: Action tree check at config load

**Goal:** resolver failures and zero primary actions fail the config load through `Result`
**Entry:** `ConfigFacade.LoadAndValidateAsync`, after `MapToDomain`
**Likely touches:** `Configuration/Validation/` (new post-mapping check, new error type), `ConfigFacade.cs`,
`Tests/Config/Integration/Validation/`, `Tests/YamlConfigs/Invalid/`

- [x] add the post-mapping check: it runs `ActionTreeResolver.Resolve` over the mapped actions, passes a
      resolver failure through, and reports zero resolved primaries as a typed error
- [x] run it next to `PlcConfigurationValidator` and merge both results. Keep the reasons of earlier
      stages
- [x] tests: every actions file empty fails with the zero-primary error, and the empty-file warning stays
      in the result. One empty actions file among non-empty ones loads. A config of only two subactions
      that target each other fails with a cycle or orphan error and no zero-primary error. One action with
      the same column key twice and different property types fails the load; the same overlay with equal
      property types loads. The Standard case and the shipped presets still load
- [x] run tests - must pass before the next task

### Task 3: UI combo-box provider next to the registry methods

**Goal:** the UI has a provider that builds every combo-box list, proven equal to the registry's primitives
**Entry:** `RecipeMetadataRegistry.GetComboBoxItems`, `GetActionComboBoxItems`
**Likely touches:** `SemiStep.UI/RecipeGrid/` (new provider, moved record), `UiDi.cs`,
`RecipeMetadataRegistry.cs` (using only), `Tests/UI/Helpers/UIFixture.cs`, new provider tests

- [x] move `ComboBoxItemViewModel` to the UI project. The registry's combo-box methods stay for now
      (deferred to Task 4: Core cannot return a UI type while its methods stay)
- [x] add the provider as a DI singleton. It builds all lists at construction as the Decisions describe.
      `UIFixture` exposes one provider built from its registry
- [x] tests: group items equal the group's `Items` from `GetGroup` ordered by key, with the group's display
      text. Group lookup ignores case. A group no action references gives an empty list. Action items equal
      `GetAllActions()` mapped to `(Id, UiName)` in order, with subactions excluded. Repeated lookups return
      the same list instance
- [x] run tests - must pass before the next task

### Task 4: Consumers switch to the provider and the registry drops combo-box methods

**Goal:** every combo-box list in the UI comes from the provider, and Core has no combo-box concept
**Entry:** the production consumers listed in Context
**Likely touches:** `ColumnBuilder.cs`, `ComboBoxCellFactory.cs`, `RecipeRowViewModel.cs`,
`CanonicalRecipeGridSurface.cs`, `Transposed/TransposedRecipeGridSurface.cs`,
`Transposed/StepColumnViewModel.cs`, `Transposed/ParameterCellViewModelFactory.cs`,
`RecipeMetadataRegistry.cs`, the tests listed in Context

- [x] switch every production consumer in Context to the provider
- [x] remove both combo-box methods and their caches from the registry, with their XML docs
- [x] tests: the registry combo-box tests move to the provider test class. In the existing UI combo-box
      tests the assertions are unchanged; the source of the expected item list moves from the registry to
      the provider. `RieEtchManualBranchConfigTests` reads action ids through `GetAllActions`
- [x] run tests - must pass before the next task

### Task 5: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command A1 to A8 passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 6: Update documentation

**Goal:** docs describe where config defects are reported and where combo-box items come from
**Entry:** `docs/architecture/error-reporting.md`, `docs/readme.md`

- [x] `error-reporting.md`, the paragraph on loading errors that are typed but not localized: add the new
      internal error types and their properties
- [x] `docs/readme.md` section 6.4 (`properties.yaml`): a `max_length` violation now appears in the startup
      error window. Section 6.7 (`actions`): the config must define at least one primary action. Keep the
      file in Russian
- [x] `docs/architecture/recipe-grid-surface.md`: where it describes what the item factories get from the
      registry, name the UI provider as the source of combo-box items

## Post-Completion
- the operator's manual test: Acceptance smoke steps 1 to 4
- closes #111
