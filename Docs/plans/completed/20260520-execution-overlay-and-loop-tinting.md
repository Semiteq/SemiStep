# Execution Overlay and Loop Tinting

## Overview

Spec `Docs/02-ui-requirements.md` §2.6 requires two row-level visual signals not currently implemented:

1. **Loop-depth tinting** — every step inside a `For…End_For` block (including the For/End_For rows) is tinted by the loop's nesting depth (4 levels × not-past/past = 8 brushes).
2. **Side-bar marker for the currently executing PLC step** — distinct signal that does not compete with the depth tint and is independent of user selection.

Also fixes `ExecutionHighlightTracker.OnExecutionStateChanged` which handles only linear forward execution; PLC line jumps (mid-recipe start, GoTo, restart) leave stale `IsPastStep` flags.

Palette source of truth is `ui/grid_style.yaml` under `colors.execution`. Missing file, section, key, or malformed hex value is a hard configuration error.

Architectural priority: **native Avalonia idioms, minimum overhead, clean Core layer**. Decisions verified against `AvaloniaUI/avalonia-docs` and Avalonia GitHub discussions (#5692, #8362, #12847, #16495, #7186).

## Locked decisions

| Question | Decision |
|----------|----------|
| Color validation | Regex in Core (`SemiStep.Core.Configuration.Validation.GridStyleValidator`). Core never references Avalonia. UI calls `Color.Parse` once at startup, post-validation. |
| `grid_style.yaml` missing | Hard configuration error. `GridStyleLoader` no longer silently returns `null` for missing file/dir. |
| Palette → Avalonia resources | One-shot `ExecutionPaletteInstaller.Install(IResourceDictionary, GridStyleOptions)` called from `App.OnFrameworkInitializationCompleted` before `MainWindow` construction. No DI registration. |
| Active-row marker | Repaint the step-number `DataGridCell` of the active row. Selector targets a `step-number-column` class added by `ColumnBuilder` (mirrors existing `read-only-column` pattern), not `:nth-child` (unreliable on `DataGridCell`). |
| Depth tint surface | **Cell-level**: `DataGridRow.for-depth-N DataGridCell { Background = ... }`. Matches existing pattern at `DataGridStyles.axaml:76-82`. Row-level `Background` is occluded by default cell painting. |
| `ForDepth` pseudo-class | Three derived bool properties `IsForDepth1/2/3` on `RecipeRowViewModel`, recomputed in `ForDepth` setter. Matches `IsCurrentStep`/`IsPastStep` pattern. No IValueConverter. |
| `ForDepth` cap | Applied at UI layer in `RefreshRowLoopDepths()` via `Math.Min(snapshot.RowLoopDepths[i], 3)`. Core stores honest depth. |
| Tracker early-out | Retained. `_lastActualLine` and `_lastRecipeActive` short-circuit no-op events. |
| `GridStyleOptions.Default` palette | Sentinel `"#000000"` for all 9 execution fields. Used only for shape-tests; production always goes through YAML validation. |

## DataGridStyles.axaml — final selector block

```xml
<Style Selector="DataGridRow.for-depth-1 DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth1Brush}" />
</Style>
<Style Selector="DataGridRow.for-depth-1.past-step DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth1PastBrush}" />
</Style>
<Style Selector="DataGridRow.for-depth-2 DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth2Brush}" />
</Style>
<Style Selector="DataGridRow.for-depth-2.past-step DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth2PastBrush}" />
</Style>
<Style Selector="DataGridRow.for-depth-3 DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth3Brush}" />
</Style>
<Style Selector="DataGridRow.for-depth-3.past-step DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth3PastBrush}" />
</Style>
<Style Selector="DataGridRow.past-step DataGridCell">
    <Setter Property="Background" Value="{DynamicResource ExecRowDepth0PastBrush}" />
</Style>
<Style Selector="DataGridRow.current-step DataGridCell.step-number-column">
    <Setter Property="Background" Value="{DynamicResource CurrentStepMarkerBrush}" />
</Style>
```

## YAML schema (added under `colors:`)

```yaml
colors:
  execution:
    depth_0: "#FFFFFF"
    depth_1: "#E8F3FF"
    depth_2: "#D0E7FF"
    depth_3: "#A8D0FF"
    depth_0_past: "#F0F0F0"
    depth_1_past: "#DCE5EE"
    depth_2_past: "#C4D2E0"
    depth_3_past: "#9CB4CC"
    current_step_marker: "#FF8800"
```

## Development Approach

- Each task leaves the build and full test suite green before the next starts.
- Tests are written in the same task as the implementation (regular testing approach).
- `dotnet format SemiStep/SemiStep.slnx` must pass (pre-commit hook).
- Subagents must check off `[ ]` items in their assigned task and update the plan file.

## Implementation Steps

### Task 1: `RowLoopDepths` in `RecipeSnapshot`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSnapshot.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Integration/Loops/RecipeSnapshotRowLoopDepthsTests.cs`

- [x] Add `IReadOnlyList<int> RowLoopDepths` parameter to the `RecipeSnapshot` record.
- [x] Update `RecipeSnapshot.Empty` with an empty `RowLoopDepths`.
- [x] In `RecipeSnapshot.Create`, build `RowLoopDepths` of length `recipe.StepCount` using the formula: for each loop in `loops`, for `i` in `loop.StartIndex..loop.EndIndex` inclusive, `tint[i] = max(tint[i], loop.Depth + 1)`. Result: `0` outside any loop, `Depth+1` for the deepest containing loop. No cap.
- [x] Add `[Trait("Component","Core")]` `[Trait("Area","Loops")]` `[Trait("Category","Integration")]` unit tests:
  - No loops → all zeros.
  - Single loop `For 1 / Wait / End_For` → `[1, 1, 1]`.
  - Nested `For 1 / For 2 / Wait / End_For / End_For` → `[1, 2, 2, 2, 1]`.
  - Abutting `For / Wait / End_For / For / Wait / End_For` → `[1, 1, 1, 1, 1, 1]`.
  - Depth >= 4 not capped at core level.
- [x] Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~RecipeSnapshotRowLoopDepthsTests"`.
- [x] Run `dotnet build SemiStep/SemiStep.slnx` — full solution still compiles.

### Task 2: `GridStyleExecutionColorsDto` + `GridStyleOptions` extension

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleExecutionColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`

- [x] Create `GridStyleExecutionColorsDto` with 9 nullable string properties under YAML aliases `depth_0`, `depth_1`, `depth_2`, `depth_3`, `depth_0_past`, `depth_1_past`, `depth_2_past`, `depth_3_past`, `current_step_marker`.
- [x] Add `Execution` property of type `GridStyleExecutionColorsDto?` to `GridStyleColorsDto` (alias `execution`).
- [x] Extend `GridStyleOptions` record with 9 string fields: `ExecutionDepth0Color`, `ExecutionDepth1Color`, `ExecutionDepth2Color`, `ExecutionDepth3Color`, `ExecutionDepth0PastColor`, `ExecutionDepth1PastColor`, `ExecutionDepth2PastColor`, `ExecutionDepth3PastColor`, `ExecutionCurrentStepMarkerColor`. Add as constructor parameters.
- [x] Update `GridStyleOptions.Default` with sentinel `"#000000"` for all 9 new fields.
- [x] Extend `GridStyleMapper.Map` to read the 9 fields directly from `dto.Colors?.Execution`. When the DTO section is missing, fall back to `GridStyleOptions.Default` values (validation in Task 4 will catch missing-section before mapping in production paths).
- [x] `dotnet build SemiStep/SemiStep.slnx` — solution compiles, all existing tests still pass.
- [x] Run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.

### Task 3: Update test fixtures and production configs

**Files:**
- Modify: `ConfigFiles/MOCVD/ui/grid_style.yaml`
- Modify: `ConfigFiles/MBE/ui/grid_style.yaml`
- Modify: `ConfigFiles/RIE/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/*/ui/grid_style.yaml` (18 files) — only those that succeed today; cases like `MalformedYaml` keep their broken content.

- [x] Append the `colors.execution` block (9 keys with draft hex values from the YAML schema above) to `ConfigFiles/MOCVD/ui/grid_style.yaml`.
- [x] Append the same block to `ConfigFiles/MBE/ui/grid_style.yaml`.
- [x] Append the same block to `ConfigFiles/RIE/ui/grid_style.yaml`.
- [x] Append the same block to `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`. (Invalid test cases overlay onto Standard per `SemiStep.Tests/Config/Helpers/TestDataCopier.cs`, so Standard is the canonical fixture.)
- [x] Append the same block to `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/ui/grid_style.yaml`.
- [x] For each `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/<Case>/ui/grid_style.yaml`: identify whether the case is expected to load successfully (no `MalformedYaml`/`EmptyYamlFile`-style failure mode for the grid style itself). Append the `execution` block to those. For cases testing yaml malformedness, leave the file as is.
- [x] Run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`. All tests must still pass — at this point validation has not been added, so the new section is informational. Configs without it still load (mapper falls back to `Default`).

### Task 4: `GridStyleValidator` + strict loader + `ConfigFacade` wiring

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/GridStyleLoader.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Facade/ConfigFacade.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs` — switch the 9 execution fields to non-fallback reads (no `?? defaults`).
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleExecutionValidationTests.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleLoaderMissingFileTests.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeExecutionPaletteTests.cs`

- [x] Author `GridStyleValidator.Validate(GridStyleOptionsDto? dto)` returning `Result`. Errors collected (not first-fail): DTO null/missing; `Colors.Execution` null/missing; each of 9 keys null or whitespace; each value not matching `^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$`. Each error names the specific key.
- [x] Change `GridStyleLoader.LoadAsync`: missing `ui/` directory → `Result.Fail`; missing `ui/grid_style.yaml` → `Result.Fail`. Remove the prior `WithWarning + null` behaviour and the "cosmetic" docblock.
- [x] In `ConfigFacade.LoadAndValidateAsync`, call `GridStyleValidator.Validate(gridStyle)` after `LoadAllSectionsAsync` and before `MapToDomain`. On failure use `LogAndPropagate`.
- [x] Switch `GridStyleMapper.Map` to read the 9 execution fields directly from `dto.Colors?.Execution!` without `??` — the validator guarantees presence in production. For the `dto is null` early-return path (mapper still tolerates `null` DTO for tests that pass it), keep `Default` values.
- [x] Tests with traits `[Trait("Component","Config")]` `[Trait("Category","Unit")]`:
  - `GridStyleExecutionValidationTests`: valid YAML passes; DTO null fails; section missing fails; each of 9 keys missing individually fails naming the key; malformed hex (`#ZZZ`, `#FFFFFFFFF`, empty, whitespace) fails naming the key; multiple errors all collected.
  - `GridStyleLoaderMissingFileTests`: missing `ui/` dir → `Result.Fail`; missing `ui/grid_style.yaml` → `Result.Fail`.
  - `ConfigFacadeExecutionPaletteTests`: end-to-end via `ConfigFacade.LoadAndValidateAsync` against a temp copy of Standard; valid passes; mutated copy with bad-execution-key fails with the specific message.
- [x] Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` — all pass.
- [x] Run full suite — all pass (fixtures already received the section in Task 3).

### Task 5: `ExecutionPaletteInstaller` + `App` wiring

**Files:**
- Create: `SemiStep/SemiStep.UI/Styles/ExecutionPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Styles/ExecutionPaletteInstallerTests.cs`

- [x] Create `internal static class ExecutionPaletteInstaller` with `public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)`. Use `Avalonia.Media.Color.Parse(...)` on each of the 9 hex strings, wrap in `SolidColorBrush`, assign to the resource keys: `ExecRowDepth0Brush`, `ExecRowDepth1Brush`, `ExecRowDepth2Brush`, `ExecRowDepth3Brush`, `ExecRowDepth0PastBrush`, `ExecRowDepth1PastBrush`, `ExecRowDepth2PastBrush`, `ExecRowDepth3PastBrush`, `CurrentStepMarkerBrush`.
- [x] In `App.OnFrameworkInitializationCompleted`, resolve `GridStyleOptions` from `_serviceProvider` (via `AppConfiguration` if needed) and call `ExecutionPaletteInstaller.Install(Resources, gridStyle)` before constructing `MainWindow`.
- [x] Verify `AppConfiguration` (or its container) is registered with the DI; if `GridStyleOptions` is not directly registered, add a transient/singleton registration in the UI composition root so `_serviceProvider.GetRequiredService<GridStyleOptions>()` resolves.
- [x] Headless test with `[AvaloniaFact]`, traits `[Trait("Component","UI")]`: build a known `GridStyleOptions` with distinct colors, run `ExecutionPaletteInstaller.Install` against a fresh `ResourceDictionary`, assert all 9 keys present, each is a `SolidColorBrush` with the expected `Color`.
- [x] `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run full suite.

### Task 6: `RecipeRowViewModel.ForDepth` + propagation

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RecipeRowForDepthPropagationTests.cs`

- [x] Add `public int ForDepth { get; set => …; }` on `RecipeRowViewModel`. In the setter, also raise change notifications for three derived bool properties: `IsForDepth1` (= `ForDepth == 1`), `IsForDepth2` (= `ForDepth == 2`), `IsForDepth3` (= `ForDepth >= 3`).
- [x] Add `private void RefreshRowLoopDepths()` to `RecipeGridViewModel` mirroring `RefreshStepStartTimes`: read `_coordinator.Snapshot.RowLoopDepths`, write `RecipeRows[i].ForDepth = Math.Min(snapshot.RowLoopDepths[i], 3)` for each row.
- [x] Call `RefreshRowLoopDepths()` after `RefreshStepStartTimes()` at the bottom of `OnMutation`. `StateRefreshed` continues to early-return before either refresh.
- [x] Headless test with `[AvaloniaFact]`, traits `[Trait("Component","UI")]`: build a `RecipeCoordinator` with a recipe containing nested `For 1 / Wait / For 2 / Wait / End_For / End_For`, wire `RecipeGridViewModel`, push the relevant mutations. Assert each row's `ForDepth` and `IsForDepth1/2/3` after each mutation. Include a depth-4 recipe to verify the UI cap clamps to 3 (`IsForDepth3` true, no `IsForDepth4`).
- [x] `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run full suite.

### Task 7: `ColumnBuilder` stamps `step-number-column` class

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`

- [x] Identify where `ColumnBuilder` constructs the step-number column (it should be the first/leftmost column or one with a known column-key constant; locate via existing logic that mirrors the `read-only-column` class application).
- [x] On that column's resulting `DataGridColumn` (or its associated cell template / cell style), add the class `"step-number-column"` using the same mechanism that adds `"read-only-column"` (`CellTheme` class or equivalent — match the existing pattern).
- [x] `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run full suite.

### Task 8: Pseudo-class stamping + style selectors + delete obsolete brushes

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RowExecutionClasses.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`
- Modify: `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/RowExecutionClassesStampingTests.cs`

- [x] In `RowExecutionClasses`, add string constants `ForDepth1Class = "for-depth-1"`, `ForDepth2Class = "for-depth-2"`, `ForDepth3Class = "for-depth-3"`.
- [x] In `MainWindow.OnDataGridLoadingRow`, after the existing two `BindClass` calls, add three more — one each for `for-depth-1`, `for-depth-2`, `for-depth-3` — bound to `nameof(RecipeRowViewModel.IsForDepth1)`, `IsForDepth2`, `IsForDepth3` respectively.
- [x] Replace lines 76-82 of `DataGridStyles.axaml` with the final selector block from the plan overview (9 selectors total: 6 depth × normal/past, 1 depth-0 past fallback, 1 current-step marker on the step-number column).
- [x] Pre-flight: `Grep "CurrentStepBrush|PastStepBrush"` across `SemiStep/` to confirm no orphan consumers.
- [x] Delete `CurrentStepBackgroundColor`, `PastStepBackgroundColor`, `CurrentStepBrush`, `PastStepBrush` from `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`.
- [x] Headless test with `[AvaloniaFact]`, traits `[Trait("Component","UI")]`: host a `DataGrid` in a `Window`, bind a small `ObservableCollection<RecipeRowViewModel>` (sized small enough that all rows materialize without virtualization — note this assumption inline), set `ForDepth` to 0/1/2/3 on different rows, run `Dispatcher.UIThread.RunJobs()` if needed, locate the materialised `DataGridRow` containers, assert the expected pseudo-class is present per row.
- [x] `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run full suite.

### Task 9: `ExecutionHighlightTracker` rewrite

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ExecutionHighlightTracker.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ExecutionHighlightTrackerJumpTests.cs`

- [x] Rewrite `OnExecutionStateChanged` to the form shown in the plan overview: keep the early-out by `_lastActualLine` and `_lastRecipeActive`, but when state has changed, run a whole-collection refresh setting `IsCurrentStep` and `IsPastStep` per row.
- [x] Verify `Reset` and `ClearAllStepHighlights` behave correctly with the new code paths.
- [x] Unit tests `[Trait("Component","UI")]`:
  - Initial `ActualLine = 5` on a 10-row recipe → rows 0..4 past, row 5 current, 6..9 neither.
  - Forward jump from 2 to 7 → rows 0..6 past, row 7 current.
  - Backward jump from 7 to 3 → rows 0..2 past, row 3 current, 4..7 neither.
  - `RecipeActive` true → false → all flags cleared.
  - No-op event (same active state, same line) → no property writes (verifiable via INPC subscription count).
- [x] `dotnet build SemiStep/SemiStep.slnx`.
- [x] Run full suite.

### Task 10: Final sanity build and format

- [x] Run `dotnet format SemiStep/SemiStep.slnx`.
- [x] Run `dotnet build SemiStep/SemiStep.slnx` — must be clean.
- [x] Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.

## Post-Completion

After all tasks pass, the plan file may be moved to `Docs/plans/completed/` by the orchestrator's finalize phase or by the user.
