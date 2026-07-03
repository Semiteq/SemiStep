# Test Suite Audit Cleanup

## Overview

A four-area audit (Config, Core, S7/Domain/Csv, UI) of the agent-written test suite identified
~105 disposable test methods (~1400-1500 lines of 22.7k), 3 fully disposable files, dead helper
code, and 3 weak tests worth strengthening. This plan removes the disposable material and applies
the 3 fixes. Every deletion has a surviving test exercising the same production branch with equal
or stronger assertions, so branch coverage does not drop.

Categories of removed tests:
- TAUTOLOGICAL: assert what the setup constructed, test a pure function's determinism, or mirror
  the production formula in the test.
- REDUNDANT: strictly subsumed by a stronger sibling test (named per item below).
- WEAK: only NotNull / IsSuccess / no-throw assertions that cannot catch a real regression.
- OVERSPECIFIED: lock incidental details (exact user-facing message strings).

## Context (from discovery)

- Test project: `SemiStep/SemiStep.Tests/` (153 files, xUnit + FluentAssertions + Avalonia.Headless).
- Full audit reports produced by four parallel reviewers; every finding below carries the surviving
  test that keeps the coverage.
- Test command: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- Format command (pre-commit hook enforces): `dotnet format SemiStep/SemiStep.slnx`

## Development Approach

- **testing approach**: Regular. This plan deletes/strengthens tests; no production code changes.
- Complete each task fully before the next; run the affected area's tests after each task.
- **CRITICAL: no production code under `SemiStep/SemiStep.UI|Core|Domain|S7|Csv` may be modified,**
  with the single exception of a YAML *test fixture* value in Task 1.
- **CRITICAL: all tests must pass before starting the next task.**
- When deleting a test, also delete `using` directives, helper methods, and fields that become
  unused as a result (the build treats warnings accordingly; `dotnet format` must stay clean).
- If a listed test/method name is not found verbatim, locate it by close-match in the named file;
  if genuinely absent, note with ⚠️ and continue — do not delete anything not listed.

## Testing Strategy

- The deliverable is test removal/strengthening. Verification per task = affected filter passes;
  final verification = full suite green + `dotnet format` clean.
- No e2e tests in this project.

## Progress Tracking

- Mark completed items `[x]` immediately.
- Newly discovered tasks: ➕ prefix. Blockers: ⚠️ prefix.

## Solution Overview

Four area-scoped cleanup tasks (Config, Core, S7/Domain/Csv, UI) plus final verification.
Deletions are exact test-method removals; three tests are strengthened instead of deleted;
dead helper methods are removed.

## Implementation Steps

### Task 1: Config area cleanup

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Config/Integration/Loading/ConfigLoadingTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Config/Integration/Errors/FileSystemErrorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Config/Integration/Validation/CrossReferenceTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Config/Integration/Errors/ActionErrorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Config/Integration/Errors/PropertyErrorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Config/Helpers/TestDataCopier.cs`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/InvalidDeployDuration/actions/service.yaml` (fixture)

- [x] ConfigLoadingTests.cs: delete `StandardConfig_LoadsSuccessfully`, `StandardConfig_HasProperties`,
      `StandardConfig_HasColumns`, `StandardConfig_HasActions` (subsumed by the `StandardConfig_HasExpected*`
      tests and `StandardConfig_NoErrors` in the same file)
- [x] FileSystemErrorTests.cs: delete `ConfigurationNotProducedOnError` (duplicate of
      `MissingConfigDirectory_HasError` with weaker assertions)
- [x] CrossReferenceTests.cs: delete `ValidCrossReferences_NoErrors` (asserts nothing about
      cross-references; duplicates ConfigLoadingTests happy path)
- [x] ActionErrorTests.cs: merge `DuplicateActionId_IdentifiesDuplicateId` into
      `DuplicateActionId_HasError` (single load, error assertion checks both "Duplicate action" wording
      and id "10"), then delete the merged-away test
- [x] PropertyErrorTests.cs: merge `DuplicatePropertyId_IdentifiesDuplicateId`,
      `InvalidSystemType_ShowsInvalidValue`, `MinGreaterThanMax_ShowsValues` into their `_HasError`
      siblings (one load per case, combined Contains assertions), then delete the merged-away tests
- [x] Fix `InvalidDeployDuration_ShowsInvalidValue` in ActionErrorTests.cs: change the fixture value
      `deploy_duration: invalid` to `deploy_duration: bogus_duration` in
      `YamlConfigs/Invalid/InvalidDeployDuration/actions/service.yaml` and assert the error message
      contains `bogus_duration` (currently `Contains("invalid")` matches any generic error wording;
      production `ActionsSectionLoader` echoes the value in the message, so the assertion holds).
      Update the stale line-1 fixture comment mentioning 'invalid' to match the new value
- [x] TestDataCopier.cs: delete unused method `EnsureDirectories` (zero call sites)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` — must pass
      (255 passed, 0 failed)

### Task 2: Core area cleanup — GridStyle and configuration tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleColorsValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleWriterTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleLoaderMissingFileTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/AppUiOptionsLoaderTests.cs`

- [x] GridStyleColorsValidationTests.cs: delete theories `Validate_MalformedDisabledHex_FailsNamingKey`
      and `Validate_MalformedReadOnlyHex_FailsNamingKey` (22 cases riding the single shared
      `ValidateSection`/hex-regex path already covered by `Validate_MalformedHex_FailsNamingKey` for
      format and by the missing-key theories for per-key naming); delete the switch helpers
      `SetDisabledKey`/`SetReadOnlyKey` that become unused
- [x] GridStyleColorsValidationTests.cs: delete `Validate_OmittedChromeSection_Succeeds` and
      `Validate_OmittedOptionalSections_Succeeds` (both re-run `Validate_ValidDto_Succeeds`);
      delete `Validate_WhitespaceValue_FailsNamingKey` (same `IsNullOrWhiteSpace` branch as
      `Validate_EmptyValue_FailsNamingKey`, keep the Empty one)
- [x] ConfigFacadeGridStyleValidationTests.cs: delete
      `LoadAndValidateAsync_GridStyleMalformedDisabledHex_Fails`,
      `LoadAndValidateAsync_GridStyleMalformedDisabledDepth2PastHex_Fails`,
      `LoadAndValidateAsync_GridStyleMalformedReadOnlyDepth2PastHex_Fails`,
      `LoadAndValidateAsync_GridStyleMissingDisabledForegroundKey_Fails`
      (facade pipeline needs one exemplar per failure kind; keep happy path, one missing-key,
      one malformed-hex, missing-file)
- [x] GridStyleWriterTests.cs: delete `Save_HexRoundTrip_ParsesToSameColor` and its
      `AssertColorEqual` helper (subsumed by `Save_SemanticRoundTrip_PreservesRecord`'s whole-record
      equality; the `Color.TryParse` part tests Avalonia, not project code)
- [x] GridStyleLoaderMissingFileTests.cs: delete `LoadAsync_MissingUiDirectory_Fails` (same
      "config not found" branch as `LoadAsync_MissingGridStyleFile_Fails`, keep the latter)
- [x] AppUiOptionsLoaderTests.cs: delete `LoadAndMap_PresentRussianLocale_ResolvesRu` (same
      pass-through path as the English case, and "ru" equals the default so it cannot distinguish
      pass-through from fallback)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"` — must pass
      (235 passed, 0 failed; the edited files carry Component=Config, so `--filter "Component=Config"`
      was also run: 223 passed, 0 failed)

### Task 3: Core area cleanup — session, loops, timing, formulas

**Files:**
- Delete: `SemiStep/SemiStep.Tests/Core/Unit/Properties/CorePropertyStateTests.cs`
- Delete: `SemiStep/SemiStep.Tests/Core/Integration/Targets/CoreTargetsTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Validity/CoreValidityTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Loops/CoreLoopTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Mutation/CoreMutationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Timings/CoreTimingTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Mutation/CoreMutationEdgeCasesTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/Formulas/FormulaEvaluatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Integration/Recipes/RecipeSessionFormulaIntegrationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/RecipeMetadataRegistryTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/RecipeStructuralEqualityTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Helpers/RecipeTestDriver.cs`

- [x] delete file `CorePropertyStateTests.cs` (entire theory duplicated branch-for-branch by
      `Core/Recipes/Helpers/CellStateResolverTests.cs`)
- [x] delete file `CoreTargetsTests.cs` (all three tests assert only NotBeEmpty/IsSuccess on the
      fixture config; `CoreTargetsEdgeCasesTests` and `CoreGroupValidationTests` keep the real coverage).
      Note: after this deletion `RecipeMetadataRegistry.GroupExists` has no direct unit test; its
      success branch stays covered indirectly via `StepInitializer` in
      `CoreGroupValidationTests.UpdateProperty_ValidGroupKey_Succeeds` — accepted trade-off
- [x] CoreValidityTests.cs: delete `MaxDepth3Exceeded_RejectsMutation` and
      `ExceedingMaxDepth_RejectsMutation_AndRecipeRemainsValid` (canonical max-depth test stays in
      `CoreLoopTests.MaxDepthExceeded_RejectsMutation`); delete `RecipeWithClosedLoop_IsValid`
      (duplicate of `CoreLoopTests.ClosedLoop_IsValid`)
- [x] CoreLoopTests.cs: delete `UnclosedLoop_ProducesWarning` (weaker twin of
      `CoreValidityTests.UnclosedLoop_BlocksValidity`, which also pins the warning text)
- [x] CoreMutationTests.cs: delete `AppendStep_CreatesStepWithDefaults`,
      `AppendStep_MultipleSteps_IncreasesCount`, `RemoveStep_LastStep_LeavesEmptyRecipe`,
      `NewRecipe_ResetsToEmpty`, `RemoveSteps_AllSteps_LeavesEmptyRecipe` (all subsumed by
      `RecipeSessionBehaviourCharacterizationTests` counterparts with stronger assertions;
      keep every timing-asserting test in the file)
- [x] CoreTimingTests.cs: delete `UpdateDuration_RecalculatesTotal` (same scenario as
      `CoreMutationTests.UpdateProperty_ChangesDuration`), `SingleWaitStep_TotalDurationMatchesStepDuration`
      and `MultipleWaitSteps_TotalDurationIsCumulative` (subsets of `StepStartTimes_AccumulateCorrectly`
      and `MixedActions_OnlyLongLastingContributeToTotalDuration` in the same file)
- [x] CoreMutationEdgeCasesTests.cs: delete `UpdateProperty_NonExistentColumn_Fails` and
      `UpdateProperty_TypeMismatch_Fails` (assertion-for-assertion duplicates of
      `UpdateStepProperty_UnknownColumn_Fails` / `UpdateStepProperty_NonParsableValue_Fails` in
      RecipeSessionBehaviourCharacterizationTests; keep the RemoveStep index-bounds tests)
- [x] FormulaEvaluatorTests.cs: delete `Recalculate_TargetOutOfRange_ReturnsComputationFailedError`
      (strict subset of `Recalculate_TargetOutOfRange_ErrorCarriesTargetAndDescriptiveMessage`)
- [x] RecipeSessionFormulaIntegrationTests.cs: move the `formulaError!.Target.Should().Be(...)`
      assertion from `UpdateStepProperty_DivideByZero_ReasonPropagatesAsFormulaComputationFailedError`
      into `UpdateStepProperty_DivideByZero_RejectsEditAndRecipeUnchanged`, then delete the former
- [x] RecipeMetadataRegistryTests.cs: delete `GetAllActions_FirstAction_IsNeverASubaction`
      (implied by `Subaction_DoesNotEnterRuntimeActionCollections`'s OnlyContain)
- [x] RecipeStructuralEqualityTests.cs: delete `RecipeEquals_FloatAndStringStepWithFreshInstances_AreEqual`
      (re-walks `RecipeEquals_ContentEqualStepWithFreshInstances_AreEqual` +
      `StepEquals_IdenticalFloatAndStringContent_AreEqual`; keep the per-type StepEquals variants)
- [x] RecipeTestDriver.cs: delete unused methods `InsertFor`, `InsertEndFor`, `AddStep` (zero call sites)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"` — must pass
      (209 passed, 0 failed)

### Task 4: S7 / Domain / Csv cleanup and fixes

**Files:**
- Modify: `SemiStep/SemiStep.Tests/S7/SyncOwnershipEndpointTokenTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/ManagingAreaCodecTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/ExecutionStateCodecTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/PlcExecutionMonitorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/PlcTransactionExecutorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Domain/Unit/ImportedRecipeValidatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Csv/Integration/CsvPropertyValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Csv/Integration/CsvDeserializationTests.cs`

- [x] SyncOwnershipEndpointTokenTests.cs: delete `For_SameEndpoint_ProducesIdenticalTokenAcrossCalls`,
      `For_EqualEndpointInstances_ProduceIdenticalTokens` (determinism of a pure string interpolation),
      and `For_RepresentativeEndpoints_ContainsNoPathInvalidCharacters` (all inputs walk the
      no-sanitization path already proven by the hostile-characters test)
- [x] ManagingAreaCodecTests.cs: delete `EncodePcData_ZeroRecipeLines_Writes4ZeroBytes` (passes even
      if the encoder never writes the field — buffer is zero-initialized),
      `Decode_AllZeroBytes_ReturnsCommittedFalseAndZeroLines`,
      `RoundTrip_CommittedFalseWithLines_PreservesValues`, and
      `Constructor_WithInvalidLayout_DoesNotThrow_ValidationIsConfigFacadeResponsibility`
- [x] ExecutionStateCodecTests.cs: delete `Decode_AllZeroBytes_ReturnsInactiveRecipeAndZeroFields`
      and `Constructor_WithInvalidLayout_DoesNotThrow_ValidationIsConfigFacadeResponsibility`
- [x] PlcExecutionMonitorTests.cs: delete `Stop_PublishesEmptyExecutionInfo` (subsumed by
      `Stop_WithoutStart_PublishesEmpty`)
- [x] PlcTransactionExecutorTests.cs: delete `WriteRecipeWithRetryAsync_EmptyRecipe_WritesCommittedTrueLast`
      (subsumed by the `..._CommitsArraysAndLines_AfterUncommittedWrite` ordering test)
- [x] ImportedRecipeValidatorTests.cs: delete `Validate_StepWithOutOfRangeInt_IsRejected` (identical
      path to `Validate_NonGroupColumnWithViolatingValue_IsRejected`) and `Validate_InvalidGroupKey_ReturnsFail`
      (subsumed by `Validate_InvalidGroupKey_ErrorMessageContainsStepNumberAndGroupName`)
- [x] CsvPropertyValidationTests.cs: delete `Deserialize_ValidValues_ImportsSuccessfully` (duplicates
      `CsvAssemblyTests.Deserialize_FullyApplicableRow_NoErrors`)
- [x] Fix `CsvDeserializationTests.Deserialize_RoundTrip_PreservesRecipe`: strengthen assertions to
      verify the deserialized step's property values (at minimum `step_duration` 5.0f and the comment
      string) survive the round trip — currently only StepCount and ActionKey are checked
- [x] Fix `PlcSyncCoordinatorTests.HandleConnectionLost_EmitsExactlyOneFault`: relax
      `faults[0].Message.Should().Be("PLC connection lost")` to a case-insensitive
      `Contain("connection")`; keep the exactly-one-fault assertion
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=S7"` and
      `--filter "Component=Domain"` and `--filter "Component=Csv"` — must pass
      (S7: 90 passed, Domain: 32 passed, Csv: 16 passed; 0 failed)

### Task 5: UI area cleanup — straight deletions

**Files:**
- Delete: `SemiStep/SemiStep.Tests/UI/Localization/ResourceResolutionTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Clipboard/ClipboardViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeCommandsViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeFile/RecipeFileViewModelCanExecuteTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindow/RecipeToolBarTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorWindowTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelToolBarToggleTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`

- [x] delete file `ResourceResolutionTests.cs` (all three tests duplicate `ChromeLocalizationTests`
      key pinning and `ResourceSyncTests` satellite completeness)
- [x] delete the six vacuous GatedInvocation tests: `Cut_GatedInvocation_WhileExecuting_DoesNotRemoveStep`
      and `Paste_GatedInvocation_...` (Clipboard), `AddStep_GatedInvocation_...` and
      `DeleteStep_GatedInvocation_...` (RecipeCommands), `NewRecipe_GatedInvocation_...` and
      `LoadRecipe_GatedInvocation_...` (RecipeFile) — the `if (CanExecute) Execute` guard means the
      mutation assertion is vacuously true; the live assertion duplicates each file's
      `*_CanExecuteFalse_WhenRecipeExecuting` sibling
- [x] RecipeToolBarTests.cs: delete `ToolBar_Visibility_FollowsIsToolBarVisible` (test wires its own
      binding, so it verifies Avalonia's binding engine, not the production AXAML wiring) and
      `ToolBar_BuildsAndExposesAllActionButtons` (existence already proven by
      `ToolBar_Buttons_BindToTheExistingViewModelCommands`)
- [x] GridStyleEditorWindowTests.cs: delete `Window_ConstructsAndBindsViewModel_WithoutThrowing`
      (asserts the DataContext the test itself just set)
- [x] MainWindowViewModelToolBarToggleTests.cs: delete `IsToolBarVisible_DefaultsToTrue`
      (asserts an auto-property initializer); keep the toggle test
- [x] RecipeRowViewModelTests.cs: delete `UpdateStepNumber_ChangesStepNumber`,
      `UpdateStepStartTime_ChangesStepStartTime` (setter round-trips),
      `GetPropertyValue_KnownColumn_ReturnsPropertyValue` (NotNull-only), and
      `Indexer_Get_UnknownKey_ReturnsNull` (delegation already proven by
      `Indexer_Get_DelegatesToGetPropertyValue` + `GetPropertyValue_UnknownKey_ReturnsNull`)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass
      (528 passed, 0 failed)

### Task 6: UI area cleanup — trims inside kept files

The three items below intentionally leave room for executor judgment (which relational assertions
remain meaningful, which single canary case to keep). That judgment is acceptable; the binding
constraints are: never weaken a surviving assertion, never delete a test not rooted in the listed
findings, and the area test run at the end of the task is the gate.

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Localization/ViewModelLocalizationTests.cs`

- [ ] ColumnWidthCalculatorTests.cs: remove every exact-width assertion of the form
      `pixelWidth.Should().Be(expectedWidth)` where `expectedWidth` is computed by the test's own
      mirror of the production formula (`ExpectedWidth()`/`LongestHeaderWordFloor()` helpers and the
      mirrored constants `StringSampleCap`, `ChromeFontMultiple`, `MinColumnWidthEms`,
      `ComboBoxChromeWidth`); keep all relational assertions (floor < whole-header width, IsStar,
      combo-vs-content delta, chrome scaling comparisons, bold-header measurement). Delete mirror
      helpers/constants once unused. If removing the mirror assertion leaves a test with no
      meaningful assertion, delete that test.
- [ ] GridStyleEditorViewModelTests.cs: delete the per-field Seed/BuildRecord clone pairs
      (`Seed_PopulatesLocalMode_FromRecord`, `BuildRecord_AfterEditingLocalMode_CarriesItBack`,
      `Seed_PopulatesConnecting_FromRecord`, `BuildRecord_AfterEditingConnecting_CarriesItBack`,
      `BuildRecord_AfterEditingStatusBarFontSizes_CarriesBothBack`,
      `BuildRecord_RoundTripsFamilyAndWeight_NotInOfferedLists`) — the generic
      `Seed_RoundTripsShippedHexValues_Losslessly` (whole-record equality) and
      `BuildRecord_AfterEditingColorAndFontSize_ChangesOnlyThoseFields` cover the path; keep one
      color pair and one numeric pair if any listed test uniquely covers a field kind the generic
      tests do not touch
- [ ] ViewModelLocalizationTests.cs: delete the EN duplicates `MapSyncStatus_UnderEnglishCulture_ReturnsNeutralText`
      and `MapSyncStatus_OutOfSync_ReturnsEmpty` (duplicated by MainWindowViewModelSyncStateTests
      theories) and `FormatErrorCount_UnderEnglishCulture` (duplicated by
      `MessagePanelViewModelTests.ErrorCountText_UsesLabelThenCount`); thin the RU theory matrices
      (`FormatErrorCount`/`FormatWarningCount` 0/1/5, `PlcConflictStepCounts`) to one canary case per
      formatter; keep `FormatLastSyncTime_Elapsed_KeepsNumberInvariant` untouched
- [ ] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass

### Task 7: Verify acceptance criteria

- [ ] run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — zero failures
- [ ] run `dotnet build SemiStep/SemiStep.slnx` — zero warnings introduced by the cleanup
      (no orphaned usings/fields/helpers)
- [ ] run `dotnet format SemiStep/SemiStep.slnx` — no diff after formatting
- [ ] grep the test project for the deleted helper names (`EnsureDirectories`, `InsertFor`,
      `InsertEndFor`, `AddStep` on RecipeTestDriver, `SetDisabledKey`, `SetReadOnlyKey`,
      `AssertColorEqual`, `ExpectedWidth`) — zero remaining references
- [ ] verify no production code changed: `git diff --stat` touches only `SemiStep/SemiStep.Tests/**`,
      `SemiStep/SemiStep.Tests/YamlConfigs/**`, and this plan file

### Task 8: [Final] Update documentation

- [ ] no README/CLAUDE.md changes expected (test-only cleanup); confirm
- [ ] move this plan to `Docs/plans/completed/` (create the directory if needed)

## Post-Completion

- PR to `master`, merge after CI is green (handled by the orchestrating session, not by exec subagents).
- No external systems affected.
