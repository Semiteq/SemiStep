# Test suite cleanup: no string-based assertions

## Behavior

- Today the test files listed in the Audit verdicts section assert on message text: Russian UI
  sentences, English error wording, log text and resource values typed out again in the test.
  Any wording change breaks them, and the Cyrillic literals block the ASCII-only source rule
  that the next cleanup step enforces.
- After this plan no test in those files asserts on a typed-out literal of product wording. A
  test checks one of three things instead: the error type and its public properties, the
  message severity and counts, or a relation between two production outputs (for example "a
  wrapped error localizes to the same text as the inner one").
- Tests that check nothing real are deleted: wording pins, duplicates, tautologies, and tests
  whose only subject is test code.
- No C# file contains Cyrillic after this plan.
- Four product defects are fixed, each in its own commit:
  1. A step duration of exactly 24 hours (the configured maximum, 86400 s) shows as `24:00:00`
     and is rejected when the operator edits it. After the fix the time editor accepts hours
     00 to 99 with minutes and seconds under 60. The property's min and max still decide the
     range: `25:00:00` is rejected by the maximum, and the message panel reports the value
     above the maximum. Today that edit is reverted with no message.
  2. Every text column in both grids parses `hh:mm` or `hh:mm:ss` input as a duration, whatever
     its format. Today a comment typed as `12:30` is stored as `45000`, and `1:30` in a numeric
     column is stored as `5400`. After the fix only `time_hms` columns parse durations; a string
     column stores the text as typed, and a numeric column rejects it as not a number.
  3. The two loop warnings (unclosed `For`, unmatched `EndFor`) name the step by its 0-based
     index. Every other step reference counts from 1. After the fix they count from 1.
  4. The step start-time column shows a seconds label hard-coded in C# source. After the fix it
     shows the units of the property behind the step-duration column, the same label the
     duration column shows.
- Scope guard: no other production behavior changes. No production member is added or widened
  for a test. Text the operator sees stays the same except for the four defects.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Operator types `24:00:00` into a duration cell | Stores 86400 s | agent: `TimeFormatHelper.ParseValue` rejects hours >= 24 while `FormatValue` writes them | Acceptance 5 |
| Operator types `25:00:00` into a duration cell | The step keeps its old value and the message panel shows a value-above-maximum error; `PropertyValidator` (max 86400, inclusive) rejects 90000 on the path `RecipeGridSurfaceBase.OnCellValueChanged` → `RecipeCoordinator.UpdateStepProperty` → `RecipeSession.UpdateStepProperty` | agent: range belongs to the property validator, not the time parser | Acceptance 5 |
| Operator types `00:61:00` into a duration cell | Rejected as malformed, as today | agent: minutes and seconds keep the under-60 rule | Acceptance 5 |
| Operator types `12:30` into a string column | Stores `12:30` | agent: `PropertyTimeEditingConverter.ParseForCommit` ignores the format kind; `TextCellFactory` uses it for every text column; `TransposedCellTemplateFactory.CommitEditor` calls the static parse with no format kind | Acceptance 6 |
| Operator types `1:30` into a numeric column | The step keeps its old value and the panel reports a type error | agent: without duration parsing the session sees text that is not a number | Acceptance 6 |
| Recipe has an unclosed `For` on row 1 | Warning names step 1 | agent: `LoopParser` passes the 0-based index; the grid numbers rows from 1 (`RecipeGridSurfaceBase` builds rows with `index + 1`) | Acceptance 7 |
| Recipe has an `EndFor` with no `For` on row 3 | Warning names step 3 | same | Acceptance 7 |
| Config gives the duration property units | Start-time column shows the same units | agent: all three shipped configs give `time` the units `"с"` | Acceptance 8 |
| Config gives the duration property empty units | Start-time column shows no units | agent: no fallback literal in C# | Acceptance 8 |

## Decisions

- Scope is the files in Audit verdicts plus `ActionMapperFormulaTests.cs` (Cyrillic). Config
  loader tests that check `Message.Contains(...)` stay as they are: the config loaders fail with
  plain-text errors (`ActionsSectionLoader`, `PropertiesSectionLoader`), so these tests have no
  typed surface, and deleting them removes the only guard on config validation. Typing config
  errors is a separate plan. rejected: deleting those checks now.
- English parity between an error's `Message` and its neutral resource value is one data-driven
  test over every error type the coverage test enumerates. It replaces the 11 hand-written parity
  tests and keeps a check on argument order in the format strings. rejected: dropping parity,
  because nothing would then catch swapped format arguments.
- Panel tests assert `Severity`, `IsError`, `IsWarning`, `ErrorCount` and `WarningCount`.
  `MessageEntry` carries only `Severity` and `Message` (`SemiStep.UI/MessageService/MessageEntry.cs`).
  rejected: adding the source reason to `MessageEntry`, because its only consumer would be tests.
- Validator, CSV and formula tests match the error chain by type and public properties
  (`AtStepError.StepNumber`, `AtRowError.RowNumber`, `AtColumnError.ColumnKey`, the inner error
  type and its values). All of these exist today.
- A test whose subject has no typed surface and whose only check is wording is deleted. The
  localizer per-type sentence tests fall here: the coverage test already proves each type gets a
  translation.
- Display text that is the product contract and has no typed source (the PLC monitor timing text)
  keeps its assertions. Its non-ASCII placeholder is written as a `\u` escape in production and
  tests, so the source is ASCII and the display is unchanged.
- Members that are `internal` only so a test can reach them become `private`:
  `MainWindowViewModel.MapSyncStatus`, `MainWindowViewModel.FormatLastSyncTime`,
  `MessagePanelViewModel.FormatErrorCount`, `MessagePanelViewModel.FormatWarningCount`,
  `ColumnWidthCalculator.CellTypeface`, `ColumnWidthCalculator.HeaderTypeface`.
  `MainWindow.ShowExitChoiceAsync` and `MainWindow.HandleExitChoiceAsync` stay `internal`: the
  window builds its exit dialog itself, so the 17 exit-flow tests have no public route. Moving the
  dialog behind an injected service is a separate change.
- The loop warnings carry a 1-based step number, set by `LoopParser`, the same as `AtStepError`.
  The only reader of the warnings' current index properties is `ReasonLocalizer`; timing code
  reads `LoopInfo`, which keeps its 0-based indices.
- The four defect fixes are separate commits after the test commits, so delivery can open each
  as its own pull request.

## Caveats

- Tests share static `Resources.Culture` through `ResourcesCultureScope`. This is safe only
  because `SemiStep.Tests/AssemblyAttributes.cs` disables parallel test runs.
  probed: plan review read of `AssemblyAttributes.cs`, 2026-11-18.
- `ResourcesCultureScope` changes `Resources.Culture`, not the thread culture. A test cannot prove
  invariant number formatting under Russian through this scope alone.
- ASSUMPTION: every error type the coverage test enumerates has an English `Message` equal to its
  neutral resource value filled with the same arguments; fallback: stop and report the mismatches,
  and do not edit production wording to make the test pass; checked by: Task 1, first step.
- ASSUMPTION: the start-time column can reach the duration property's units through the registry
  that `RecipeRowViewModel` and `RecipeGridSurfaceBase` already hold; fallback: stop and report if
  it needs a new registry member or dependency; checked by: Task 9.
- `git grep -P` with a Unicode range needs a UTF-8 locale in Git Bash.
  probed: `LC_ALL=C.UTF-8 git grep -lP '[\x{0400}-\x{04FF}]'` listed 21 files, 2026-11-18.

## Constraints and non-goals

- No production API is added or widened for tests. A rewrite that would need one becomes a
  deletion.
- Tests stay as simple as possible: no new helper unless two or more tests use it.
- Out of scope: config-loader message checks, the BOM, comment and `#region` cleanup, non-ASCII
  characters other than Cyrillic, the `terse` gate and CI changes, the wall-clock polling in
  `TestHelpers.WaitUntilAsync`.
- Cyrillic in `ConfigFiles/**/*.yaml` stays: it is configuration data, not C# source.

## Gates

- A rewrite that needs a new production member or a widened visibility stops that test's rewrite.
  The test is deleted, and the case is listed in the run report.
- The parity ASSUMPTION or the start-time-units ASSUMPTION fails: stop the run for an operator
  decision.

## Acceptance

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
3. `LC_ALL=C.UTF-8 git grep -lP '[\x{0400}-\x{04FF}]' -- 'SemiStep/*.cs'` → no output.
4. `git grep -nE '(Message|Text|Title)\)?\.Should\(\)\.(Contain|StartWith|EndWith|Be)\("|Message\.Contains\("|WithMessage\(' -- <every file in Audit verdicts that still exists>`
   → only PLC monitor timing assertions that compare display text with ASCII literals, plus the
   config-mapping message checks in `ActionMapperFormulaTests.cs`, which Decisions keep.
5. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PropertyTimeEditingConverterTests|FullyQualifiedName~MessagePanelReportingTests"`
   → passes, and includes tests that prove: the converter turns `24:00:00` into 86400 and rejects
   `00:61:00`; `25:00:00` set through a wired grid surface leaves the step value unchanged and
   adds one error to the panel.
6. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PropertyTimeEditingConverterTests|FullyQualifiedName~Transposed|FullyQualifiedName~CanonicalRecipeGridViewTests"`
   → passes, and includes tests that prove `12:30` committed to a string column stores `12:30` in
   the canonical editor and in the transposed editor.
7. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~CoreLoop"`
   → passes, and includes tests that prove an unclosed `For` on row 1 yields a warning with step
   number 1 and an unmatched `EndFor` on row 3 yields step number 3, asserted on the property.
8. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeRowViewModelTests"`
   → passes, and includes a test that proves the start-time column's units equal the duration
   property's units from the loaded configuration.
9. manual smoke:
   1. Load a recipe, type `24:00:00` into a step duration cell, press Enter → the cell keeps
      `24:00:00` and no error appears.
   2. Type `25:00:00` into the same cell → the cell returns to its old value and the message panel
      shows a value-above-maximum error.
   3. Type `12:30` into a comment cell, press Enter → the cell shows `12:30`. Repeat in the
      transposed view → same result.
   4. Add a `For` step on row 1 with no `EndFor` → the message panel names step 1.
   5. Look at the start-time column → it shows the same units as the duration column.
- not visible to headless tests: the visual look of the units label.

---

## Audit verdicts

Read-only audit, 2026-11-18. The implementing agent re-reads each file and applies the rules in
Decisions; where a file reads differently than listed, the rules win. Paths are under
`SemiStep/SemiStep.Tests/`.

**Localization (Task 1)**
- `UI/Localization/ReasonLocalizerTests.cs`: delete the ~41 per-type Russian sentence tests;
  replace the 11 English parity tests with the one parity check; keep the free-text fallback tests,
  asserting `Be(error.Message)`; rewrite decorator, wrapper and formula composition as relations
  between two `Localize` results and merge the duplicate wrapper case; delete the report-failure
  duplicate; rewrite the refresh-reasons case as `Be(ReasonLocalizer.Localize(error))` under ru.
- `UI/Localization/CoreErrorLocalizationCoverageTests.cs`: keep; it hosts the parity loop.
- `UI/Localization/ResourceSyncTests.cs`: keep the satellite, non-empty and placeholder checks;
  merge the per-key resolve theory and the designer-accessor check into one set equality.
- `UI/Localization/UiCultureSelectorTests.cs`: keep the fallback case, compare against
  `AppUiOptions.DefaultLocale`; drop the cases that test the library.
- `UI/Localization/ChromeLocalizationTests.cs`: delete the resource-value-equals-literal tests;
  rewrite the menu-bar test to compare headers with `Resources` accessors.
- `UI/Localization/CategoryBLocalizationTests.cs`: delete.
- `UI/Localization/CommandErrorLocalizationTests.cs`: rewrite: panel text starts with the localized
  context, the log carries the invariant context, the two differ; stop swallowing the exception.
- `UI/Localization/ViewModelLocalizationTests.cs`: delete, including the clock-reading test.
- `Csv/Integration/CsvImportLocalizationTests.cs`: rewrite the chain test as a typed match
  `AtRowError` → `AtColumnError` → `ValueAboveMaximumError`; delete the panel duplicate.

**Message panel and reporting (Task 2)**
- `UI/MessagePanelReportingTests.cs`: delete the Russian gate, interactive-edit and unclosed-for
  text tests and the load-success duplicate of `RecipeFileViewModelLoadResultTests`; move the
  `ValueAboveMaximumError` type check into a `RecipeSession` test; rewrite save, invalid edit,
  unknown action and invalid paste to severity and `ErrorCount`; keep missing-file and valid paste.
- `UI/MessagePanelViewModelTests.cs`: delete the count-text wording tests, the Russian warning test
  and the remove-before-add duplicate; rewrite the status summary to compose from the VM's own
  count texts; rewrite the operation-channel tests to tell channels apart by severity and order;
  split tests that branch on a theory parameter.
- `UI/ResultReportingExtensionsTests.cs`: delete the `FormatErrors` join tests, the Russian report
  tests and the single-untyped-error test; keep the with-context test; rewrite the typed Russian
  format test to `Be(error.Message)`.
- `UI/RecipeGrid/RecipeCommandsViewModelReportingTests.cs`: assert log level and the logged
  exception instance, panel `IsError`; stop catching every exception in the test body.
- `UI/MainWindowViewModelSyncStateTests.cs`: delete the status-text and Russian title tests; keep
  the boolean state tests.
- `UI/Plc/PlcMonitorViewModelTimingTests.cs`: delete the integration duplicate; keep the rest with
  the placeholder written as an escape.
- `UI/RecipeFile/RecipeFileViewModelLoadResultTests.cs`: rewrite to severity; drop `RunJobs()`
  after awaited commands.
- `UI/RecipeCoordinatorTests.cs`: delete the duplicate append-failure test; rewrite PLC fault,
  reconnect-apply and rejected-mutation tests to severity and `ErrorCount`.
- `UI/MainWindow/MainWindowExitFlowTests.cs`: rewrite the save-throws and dialog-throws cases to
  severity Error; drop `RunJobs()` after awaited calls; keep the rest.
- `Performance/Harness/PerfBaselinesTests.cs`: delete; it tests test-only harness code.

**Domain, CSV and formula (Task 3)**
- `Domain/Unit/ImportedRecipeValidatorTests.cs`: delete the step-number-in-message and
  non-group-column string copies; rewrite type mismatch, over-length, multiple violations and the
  decorator tests to typed matches (`ValueNotInGroupError`, `ValueAboveMaximumError`,
  `PropertyValueTypeMismatchError`, `StringTooLongError`, `PropertyNotFoundError`,
  `GroupValueNotIntegerError`, `ActionByIdNotFoundError` with their public values).
- `Csv/Integration/CsvDeserializationTests.cs`: rewrite the Russian chain test as a typed chain;
  the column-count test asserts `ColumnCountMismatchError`; every failure test asserts the error
  type; rename tests named "returns null" that assert a failure; the clipboard round trip compares
  whole steps.
- `Core/Unit/Recipes/Formulas/FormulaEvaluatorTests.cs`: out-of-range asserts the inner
  `ValueAboveMaximumError` and its maximum; NaN and infinity drop the message line.
- `Core/Unit/Configuration/Mapping/ActionMapperFormulaTests.cs`: replace its Cyrillic with ASCII
  test data or English names.

**Grid, row, style editor, config (Task 4)**
- `UI/RecipeGrid/ColumnWidthCalculatorTests.cs`: delete the tests that measure with the test's own
  algorithm copy or compare against a removed formula; rewrite header, multi-word, string-length,
  max, chrome and combo tests as relations between production outputs and the public minimum
  width; replace the typeface threading test with a width relation (bold header wider than the
  same text in a cell), since the typefaces become private; ASCII test data (`cm3/min`, `%/min`).
- `UI/RecipeRowViewModelTests.cs`: delete the action-changed duplicate, the indexer tautology, the
  rejected-state group test, the two-rows `NotBeSameAs` test and the inapplicable-recompute
  duplicate; the units theory checks against configuration (Task 9); rewrite the indexer
  notification through a binding or delete it.
- `UI/RecipeGrid/RecipeRowSelectorEditTests.cs`: delete the in-test converter test; English mode
  names in comments.
- `UI/StyleEditor/GridStyleEditorViewModelTests.cs`: delete the Russian hex-color message test,
  the duplicate save-exception path, the reflection-heavy leaf test and the two font tests covered
  by the round trip; hex tests compare parsed colors; merge the three status-bar theories.
- `S7/RieEtchManualBranchSerializationTests.cs`: keep; English action and mode names in comments;
  cut the history from the XML doc.
- `Config/Integration/NestedActions/RieEtchManualBranchConfigTests.cs`: delete the load-and-validate
  duplicate; English names in comments.
- `Core/Integration/Recipes/RecipeSessionSelectorSeedIntegrationTests.cs`: keep; English names in
  comments; assert the claimed single undo unit or drop the claim.

## Context

- Localization: `SemiStep.UI/Localization/ReasonLocalizer.cs` (private switch over error types),
  `Resources.resx`, `Resources.ru.resx`, `Docs/architecture/error-reporting.md` (design: localize
  on type, not on codes).
- Message panel: `SemiStep.UI/MessageService/MessagePanelViewModel.cs`, `MessageEntry.cs`.
- Time format: `SemiStep.UI/RecipeGrid/TimeFormatHelper.cs`, `PropertyTimeEditingConverter.cs`,
  `TextCellFactory.cs`, `RecipeRowViewModel.cs` (start-time units), `RecipeGridSurfaceBase.cs`,
  `ColumnWidthCalculator.cs`; range check in `SemiStep.Core/Recipes/PropertyValidator.cs`.
- Loop warnings: `SemiStep.Core/Recipes/Analysis/LoopParser.cs`,
  `Warnings/UnclosedForLoopWarning.cs`, `Warnings/UnmatchedEndForWarning.cs`; tests in
  `CoreLoopTests.cs` and `CoreLoopEdgeCasesTests.cs`.
- Headless UI tests: `Docs/architecture/headless-ui-testing.md`; `[AvaloniaFact]` runs the whole
  test on the dispatcher, so `Dispatcher.UIThread.RunJobs()` after an awaited command is noise.
- Test traits and commands: `CLAUDE.md`.

## Tasks

### Task 1: Localization tests

**Goal:** localization tests check translation coverage, resource consistency and composition,
never wording.
**Entry:** `SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs`
**Likely touches:** the Localization files in Audit verdicts, `Csv/Integration/CsvImportLocalizationTests.cs`

- [x] write the parity check first and run it; on any mismatch stop under Gates
- [x] apply the Localization verdicts
- [x] run tests - must pass before the next task

### Task 2: Message panel and reporting tests

**Goal:** reporting tests check severity, counts, channels and order.
**Entry:** `SemiStep.Tests/UI/MessagePanelReportingTests.cs`
**Likely touches:** the reporting files in Audit verdicts, `SemiStep.UI/Plc/PlcMonitorViewModel.cs`

- [x] apply the Message panel and reporting verdicts
- [x] write the PLC monitor placeholder as a `\u` escape in production and tests
- [x] run tests - must pass before the next task

### Task 3: Domain, CSV and formula tests

**Goal:** these tests match error chains by type and public values.
**Entry:** `SemiStep.Tests/Domain/Unit/ImportedRecipeValidatorTests.cs`

- [x] apply the Domain, CSV and formula verdicts
- [x] run tests - must pass before the next task

### Task 4: Grid, row, style editor and config tests

**Goal:** these tests check production behavior through production output only.
**Entry:** `SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`
**Likely touches:** the grid files in Audit verdicts, the comment in `SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`

- [x] apply the Grid, row, style editor and config verdicts, except the units theory (Task 9)
- [x] replace the Cyrillic comment in `ColumnWidthCalculator.cs` with English
- [x] run tests - must pass before the next task

### Task 5: Make test-only internals private

**Goal:** no production member is `internal` only for a test, except the two exit-flow members.
**Entry:** the members listed in Decisions

- [x] make each listed member `private`
- [x] where a surviving test used one, rewrite it through the public surface that exposes the
      same value, or delete it under Gates
- [x] run tests - must pass before the next task

### Task 6: Loop warnings count steps from 1

**Goal:** both loop warnings name the step the operator sees.
**Entry:** `SemiStep.Core/Recipes/Analysis/LoopParser.cs`
**Likely touches:** `UnclosedForLoopWarning.cs`, `UnmatchedEndForWarning.cs`, `ReasonLocalizer.cs`

- [x] both warnings carry a 1-based step number set by `LoopParser`; their English message and the
      localized text use it
- [x] tests: unclosed `For` on row 1 gives step number 1; unmatched `EndFor` on row 3 gives 3
- [x] run tests - must pass before the next task

### Task 7: Durations of 24 hours and more parse

**Goal:** the time editor accepts every value the formatter writes up to 99 hours.
**Entry:** `SemiStep.UI/RecipeGrid/TimeFormatHelper.cs`
**Likely touches:** `PropertyTimeEditingConverter.cs`

- [x] parsing accepts hours 00 to 99; minutes and seconds keep the under-60 rule
- [x] tests in `PropertyTimeEditingConverterTests`: format then parse round-trips 0, 59, 3600,
      86399 and 86400 seconds; `00:61:00` is rejected
- [x] test in `MessagePanelReportingTests`, which wires a real grid surface: `25:00:00` set on a
      duration cell leaves the step value unchanged and adds one error
- [x] run tests - must pass before the next task

### Task 8: Only time columns parse durations

**Goal:** text typed into a non-time column is stored as typed, in both grids.
**Entry:** `SemiStep.UI/RecipeGrid/PropertyTimeEditingConverter.cs`
**Likely touches:** `TextCellFactory.cs`, `Transposed/TransposedCellTemplateFactory.cs`

- [x] the converter and the static commit parse take the column's format kind and parse durations
      only for `time_hms`; the transposed editor passes `ParameterCellViewModel.FormatKind`
- [x] the format kind comes from the row's column metadata; a duration column never falls back
      to `DefaultFormatKind` (`"numeric"`), which would turn its parsing off
- [x] tests: `12:30` committed to a string column stores `12:30` in the canonical and the
      transposed editor; a duration column still parses in both
- [x] run tests - must pass before the next task

### Task 9: Start-time units come from configuration

**Goal:** no unit label lives in C# source.
**Entry:** `SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs` (start-time units)
**Likely touches:** `TimeFormatHelper.cs`, `RecipeGridSurfaceBase.cs`, `ColumnWidthCalculator.cs`,
`SemiStep.Tests/UI/RecipeGrid/RecipeGridSurfaceContractTests.cs`

- [x] the start-time column and the width calculation take the units of the property behind the
      step-duration column; empty units stay empty
- [x] remove the unit constant from `TimeFormatHelper`
- [x] tests: start-time units equal the duration property's units from configuration; the
      row-units theory checks against configuration, not a literal
- [x] run tests - must pass before the next task

### Task 10: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch.
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result (item 9 manual smoke: skipped - not automatable)
- [x] `dotnet format SemiStep.slnx --verify-no-changes` exits 0
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 11: [Final] Update documentation

**Goal:** docs describe how tests assert on errors and the fixed behaviors.
**Entry:** `Docs/architecture/error-reporting.md`, `Docs/architecture/ui-localization.md`

- [x] state that tests assert on error types, severities and relations between outputs, never on
      wording, and that English parity is one data-driven check
- [x] update `Docs/readme.md` (Russian) where it describes time entry or loop warnings, if it does

## Post-Completion

- the operator's manual test: Acceptance smoke steps 1 to 5.
- delivery: tasks 1 to 5 form the test cleanup pull request; tasks 6, 7, 8 and 9 are one pull
  request each.
- follow-up plans: typed config-loader errors, then the config message checks; the exit dialog
  behind an injected service.
- the four fix commits also tick this plan's checkboxes, so their pull requests stack on the test
  cleanup pull request, or the plan hunks are dropped from them.

**Executed by exec:**
- branch: test-suite-cleanup

## Verify it yourself

Automated proof, from the repository root:

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 1408 passed, 0 failed.
3. `LC_ALL=C.UTF-8 git grep -lP '[\x{0400}-\x{04FF}]' -- 'SemiStep/*.cs'` → no output.

Each defect fix has a test that fails on the commit before it and passes on the fix commit:

| Defect | Test | Fails against the code of | Passes on |
|---|---|---|---|
| Loop warnings count from 0 | `CoreLoopEdgeCasesTests.UnclosedFor_OnRowOne_WarnsStepNumberOne` | `e06ee14b` | `201813e2` |
| `24:00:00` rejected | `PropertyTimeEditingConverterTests.ConvertBack_FormattedTimeValue_RoundTripsToSameSeconds` (86400 case) | `201813e2` | `967e9e4e` |
| Text columns parse `12:30` | `PropertyTimeEditingConverterTests.ConvertBack_ClockLikeText_NonTimeColumn_IsKeptAsTyped` | `967e9e4e` | `959a5505` |
| Start-time units hard-coded | `RecipeRowViewModelTests.ColumnUnits_StartTimeColumn_EqualsDurationPropertyUnits` | `959a5505` | `ee16cc7e` |

Manual check in the app (the RIE config, any recipe):

1. Type `24:00:00` into a step duration cell and press Enter. Before: the cell reverts. After: the
   cell keeps `24:00:00`, no error.
2. Type `25:00:00` into the same cell. Before: the cell reverts with no message. After: the cell
   reverts and the message panel shows a value-above-maximum error.
3. Type `12:30` into a comment cell. Before: the cell shows `45000`. After: it shows `12:30`. Repeat
   in the transposed view: same result.
4. Add a `For` step on row 1 with no `EndFor`. Before: the panel names step 0. After: step 1.
5. Look at the start-time column. It shows the same units as the duration column (`с` in the
   shipped configs). The test cleanup itself has no visible effect.
