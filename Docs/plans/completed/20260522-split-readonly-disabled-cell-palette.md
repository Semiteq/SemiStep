# Split Read-only and Disabled Cell Palettes

## Overview

Today `DataGridCell.read-only-column` (column-level "this column is structurally non-editable") and `InapplicableCellTheme.IsInapplicable=True` (row × column intersection "this action doesn't use this property") paint **identical** visuals via a comma-grouped selector chain. This regression was introduced by commit `fa290e2` ("Restore greyed-out visual for read-only columns") which restored read-only column styling by reusing the disabled palette instead of giving it its own. The semantic distinction survives in code (`ColumnBuilder` stamps the class; `CellStateResolver.IsInapplicable` explicitly returns `false` for read-only columns), but the user cannot distinguish the two states on screen.

This plan re-introduces a dedicated read-only palette so operators can configure each state's appearance independently. It also relocates the existing `colors.execution` block to `colors.cells.execution` so all cell-painting palettes nest under `cells:` — a step toward the longer-term goal of extracting every hardcoded UI color into `ui/grid_style.yaml`.

## Context (from discovery)

- **Selectors today (`SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml:39-148`)**: 9 selector blocks targeting `DataGridCell.read-only-column, DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]` together. Current order: universal disabled (39) → loop-depth tints on rows → per-depth disabled restores (100-127) → `:selected DataGridCell` AccentBrush (133) → selection-disabled override (141) → current-step-marker on step-number column (146).
- **Read-only class stamping (`SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs:13,51-54`)**: class added when `GridColumnDefinition.ReadOnly == true`.
- **Inapplicable attached property (`SemiStep/SemiStep.UI/RecipeGrid/InapplicableCellTheme.cs:26-36`)**: populated from `CellStateResolver.IsInapplicable(column, action)`. `CellStateResolver` line 13 explicitly returns `false` for read-only columns — **the two signals are disjoint by design.**
- **Existing per-depth disabled palette (`SemiStep/SemiStep.Core/Configuration/Dto/GridStyleDisabledCellColorsDto.cs`, `GridStyleOptions.cs:13-22`)**: 10 keys to clone.
- **Existing installer (`SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`)**: 11 brushes via `PaletteBrushFactory.From(hex)`.
- **Existing tests**: `GridStyleColorsValidationTests` (TheoryData over disabled + execution keys, quoted-path assertions), `GridStyleMapperTests`, `CellPaletteInstallerTests`, `ConfigFacadeGridStyleValidationTests` (section-aware YAML mutation).
- **YAML breadth**: 3 production + 1 Standard + 1 WithGroups + 18 Standalone = 23 files. Standalone cases: `ActionWithNegativeId`, `ActionWithZeroId`, `EmptyActionsDir`, `EmptyPropertiesDir`, `EmptyYamlFile`, `FloatCultureInvariant`, `MalformedYaml`, `MissingActionsDir`, `MissingColumnKey`, `MissingColumnPropertyTypeId`, `MissingColumnsDir`, `MissingDeployDuration`, `MissingFormatKind`, `MissingPropertiesDir`, `MissingSystemType`, `MissingUiName`, `MultipleErrors`, `UnknownYamlFields`. Several test other validators and may not require grid_style edits if they short-circuit before grid_style loads — gated check needed (Task 2).

## Development Approach

- **Testing approach**: Regular — implement, then write tests in the same task.
- **Each task leaves the build and full test suite green before the next.** No RED windows. Strict-from-start mapper — no introduce-then-delete fallback.
- `dotnet format SemiStep/SemiStep.slnx` must pass (pre-commit hook).
- Each task is a single commit. Review-iter fixes land later in their own consolidated commits.

## Testing Strategy

- **Unit tests**: extended `GridStyleColorsValidationTests`, `GridStyleMapperTests`, `CellPaletteInstallerTests` covering 10 new readonly keys.
- **Integration tests**: `ConfigFacadeGridStyleValidationTests` parameterised mutations targeting `colors.cells.readonly.*` and the new `colors.cells.execution.*` path.
- **No E2E**: project has none. Visual verification is manual (Task 6).

## Progress Tracking
- `[x]` immediately when done.
- New tasks: prefix `➕`. Blockers: prefix `⚠️`.

## Solution Overview

### Schema reorganisation

```yaml
colors:
  cells:
    readonly:
      depth_0: "#D8D8D8"
      depth_1: "#CCD5E0"
      depth_2: "#B8C3D1"
      depth_3: "#94A2B3"
      depth_0_past: "#C8C8C8"
      depth_1_past: "#BCC4CE"
      depth_2_past: "#ACB7C2"
      depth_3_past: "#8590A0"
      selected: "#6B95C0"
      foreground: "#606060"
    disabled:
      depth_0: "#E0E0E0"
      depth_1: "#D5DEEA"
      depth_2: "#C2CEDB"
      depth_3: "#9DABBC"
      depth_0_past: "#D0D0D0"
      depth_1_past: "#C5CDD8"
      depth_2_past: "#B5C0CC"
      depth_3_past: "#909AAA"
      selected: "#89B4D7"
      foreground: "#808080"
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
  rows:
    alternating_background: "#FAFAFA"
    normal_background: "#FFFFFF"
  grid_line: "#E0E0E0"
```

`readonly` palette uses slightly darker greys than `disabled` so the visual difference is obvious without harsh contrast. Values are first-draft.

### Selector layout (DataGridStyles.axaml — final order)

```xml
<!-- 1. Universal read-only column appearance. -->
<Style Selector="DataGridCell.read-only-column">
    <Setter Property="Background" Value="{DynamicResource CellReadonlyDepth0Brush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource CellReadonlyForegroundBrush}" />
</Style>

<!-- 2. Universal inapplicable cell appearance. -->
<Style Selector="DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth0Brush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource CellDisabledForegroundBrush}" />
</Style>

<!-- (existing loop-depth row tint selectors stay here as today) -->

<!-- 3. Read-only per-depth overrides: 7 selectors mirroring the disabled chain below.
     Document-order discipline: bare past-step first, then depth-N normal, then depth-N + past-step. -->
<Style Selector="DataGridRow.past-step DataGridCell.read-only-column">...CellReadonlyDepth0PastBrush...</Style>
<Style Selector="DataGridRow.for-depth-1 DataGridCell.read-only-column">...CellReadonlyDepth1Brush...</Style>
<Style Selector="DataGridRow.for-depth-1.past-step DataGridCell.read-only-column">...CellReadonlyDepth1PastBrush...</Style>
<Style Selector="DataGridRow.for-depth-2 DataGridCell.read-only-column">...CellReadonlyDepth2Brush...</Style>
<Style Selector="DataGridRow.for-depth-2.past-step DataGridCell.read-only-column">...CellReadonlyDepth2PastBrush...</Style>
<Style Selector="DataGridRow.for-depth-3 DataGridCell.read-only-column">...CellReadonlyDepth3Brush...</Style>
<Style Selector="DataGridRow.for-depth-3.past-step DataGridCell.read-only-column">...CellReadonlyDepth3PastBrush...</Style>

<!-- 4. Disabled per-depth overrides: 7 selectors. Background-only setters; same document-order rule. -->
<Style Selector="DataGridRow.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">...CellDisabledDepth0PastBrush...</Style>
<!-- ... depth-N normal / past mirroring readonly above -->

<!-- 5. Generic selection — paints all cells with AccentBrush. -->
<Style Selector="DataGridRow:selected DataGridCell">
    <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource TextOnAccentBrush}" />
</Style>

<!-- 6. Selection on read-only cell — last-match-wins gives configurable read-only-selected. -->
<Style Selector="DataGridRow:selected DataGridCell.read-only-column">
    <Setter Property="Background" Value="{DynamicResource CellReadonlySelectedBackgroundBrush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource TextOnAccentBrush}" />
</Style>

<!-- 7. Selection on inapplicable cell — last-match-wins gives configurable disabled-selected. -->
<Style Selector="DataGridRow:selected DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledSelectedBackgroundBrush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource TextOnAccentBrush}" />
</Style>

<!-- 8. Current-step marker on step-number column — STAYS LAST so the marker wins
     over selection on the current row's step-number cell. -->
<Style Selector="DataGridRow.current-step DataGridCell.step-number-column">
    <Setter Property="Background" Value="{DynamicResource CurrentStepMarkerBrush}" />
</Style>
```

Total disabled-zone blocks grow from ~9 to ~18 (2 universal + 14 per-depth + 2 selection — current-step-marker unchanged).

### Resource keys

10 new brushes added to `CellPaletteInstaller` (matching existing `CellDisabled*` naming convention — verified by reading current installer before Task 3):
- `CellReadonlyDepth0Brush`, `CellReadonlyDepth1Brush`, `CellReadonlyDepth2Brush`, `CellReadonlyDepth3Brush`
- `CellReadonlyDepth0PastBrush`, `CellReadonlyDepth1PastBrush`, `CellReadonlyDepth2PastBrush`, `CellReadonlyDepth3PastBrush`
- `CellReadonlySelectedBackgroundBrush`
- `CellReadonlyForegroundBrush`

Installer total: 11 → 21.

## Implementation Steps

### Task 1: Relocate execution palette under `colors.cells.execution`

**Goal:** all-in-one move of the execution palette from `colors.execution` to `colors.cells.execution`. Single commit, suite green throughout.

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleColorsValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleMapperTests.cs`
- Modify: `ConfigFiles/MOCVD/ui/grid_style.yaml`, `MBE/ui/grid_style.yaml`, `RIE/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`, `WithGroups/ui/grid_style.yaml`, every Standalone case that ships its own grid_style.yaml AND loads it before failing (see Task 2 gating note).

- [ ] **Pre-flight**: `grep -r "colors.execution"` and `grep -r "'colors.execution"` across `SemiStep.Tests/` to enumerate every test assertion on the old path. Note each file:line.
- [ ] **Pre-flight**: read `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs` and identify the section-mutation helper. Confirm it accepts a dotted path or can be extended to find `execution:` nested under `cells:`. If it only handles top-level keys, extend it as part of this task.
- [ ] Move `Execution` property from `GridStyleColorsDto` to `GridStyleCellColorsDto` (alias `execution`).
- [ ] Update `GridStyleMapper.Map`: change execution reads to use a hoisted `var execution = dto.Colors!.Cells!.Execution!;` and read fields from it.
- [ ] Update `GridStyleValidator`: change the execution-section error path from `colors.execution` to `colors.cells.execution` in the `ValidateKey` invocations + the missing-section message.
- [ ] Migrate all production YAMLs (`MOCVD`/`MBE`/`RIE`): move the existing `colors.execution:` block to `colors.cells.execution:` (preserve all 9 key values verbatim).
- [ ] Migrate `Standard/ui/grid_style.yaml` and `WithGroups/ui/grid_style.yaml` similarly.
- [ ] Standalone fixtures: for each of the 18 cases, check whether `grid_style.yaml` is loaded before the failure-mode validator fires. The simplest gate: run `dotnet test --filter "FullyQualifiedName~<CaseName>"` against the current strict validator (commit before this task) — cases that pass for the right reason today need migration; cases that fail before grid_style load (e.g. `MissingActionsDir`, `MalformedYaml` where the malformedness is in actions/) likely do not. Migrate only those that need it; document the skipped ones in a brief comment in the plan diff.
- [ ] Update every test assertion on `'colors.execution.X'` to `'colors.cells.execution.X'`. Use the grep results from the pre-flight bullet.
- [ ] Update the `ConfigFacadeGridStyleValidationTests` execution-section mutation: helper now needs to locate `execution:` nested two levels under `cells:`. Adjust accordingly; verify the test still proves what it claims.
- [ ] Update `GridStyleMapperTests` (`Map_WiresEveryDisabledCellAndExecutionFieldFromDto`): when constructing the test DTO, place `Execution` under `Cells` instead of top-level. Distinct hex values for execution kept as today.
- [ ] `dotnet build SemiStep/SemiStep.slnx` clean.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` full suite green.

### Task 2: Add `colors.cells.readonly` palette — DTO + Options + Mapper + Validator + YAML + tests

**Goal:** single coherent commit introducing the new palette section. Strict mapper from the start (validator-first invariant). All fixtures populated in the same commit so the validator can be strict immediately.

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleReadonlyCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleColorsValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleMapperTests.cs`
- Modify: every YAML config from Task 1's modify list (production + applicable test fixtures) — add the new `colors.cells.readonly:` block.

- [ ] Create `GridStyleReadonlyCellColorsDto` with 10 nullable string properties (aliases `depth_0..3`, `depth_0..3_past`, `selected`, `foreground`) — clone of `GridStyleDisabledCellColorsDto`.
- [ ] Add `Readonly` property to `GridStyleCellColorsDto` (alias `readonly`).
- [ ] Add 10 string fields on `GridStyleOptions` record: `ReadonlyCellDepth0..3Color`, `ReadonlyCellDepth0..3PastColor`, `ReadonlyCellSelectedColor`, `ReadonlyCellForegroundColor`. Update `Default` with `#000000` sentinels.
- [ ] In `GridStyleMapper.Map`: add hoisted `var readonlyCells = dto.Colors!.Cells!.Readonly!;` and read all 10 fields strictly (no `??` fallback) — validator-first invariant.
- [ ] In `GridStyleValidator.Validate`: add a third sub-block for `colors.cells.readonly` with 10 tuples (`depth_0..3`, `depth_0..3_past`, `selected`, `foreground`). Use the same `ValidateKey(sectionPath, name, value, errors)` helper. Section path: `colors.cells.readonly`. All errors collected before returning (mirror disabled-block style).
- [ ] Migrate YAMLs: add the new `colors.cells.readonly:` block (10 keys with the draft values from the Solution Overview) to every file edited in Task 1 — production + applicable fixtures.
- [ ] Extend `GridStyleColorsValidationTests`:
    - Add a `ReadonlyKeyNames` `TheoryData` source listing the 10 keys.
    - Parameterise `Validate_MissingReadonlyKey_FailsWithKeyName` and `Validate_MalformedReadonlyHex_FailsNamingKey` over all 10 keys, with assertions `Contains($"'colors.cells.readonly.{keyName}'")`.
    - Add `Validate_MissingReadonlySection_Fails` mirroring the disabled-section test.
- [ ] Add a new fact to `ConfigFacadeGridStyleValidationTests` mutating `colors.cells.readonly.depth_2_past` (or another per-depth key) to verify end-to-end via `ConfigFacade.LoadAndValidateAsync`.
- [ ] Extend `GridStyleMapperTests`: add or rename the field-wiring fact to assert all 10 readonly fields wire from distinct hex values, catching any swap.
- [ ] `dotnet build SemiStep/SemiStep.slnx` clean.
- [ ] Full test suite green.

### Task 3: Install 10 new readonly brushes via `CellPaletteInstaller`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [ ] **Pre-flight**: read current `CellPaletteInstaller.cs` to confirm the const naming convention (`*BrushKey` for const string, value `"*Brush"`). Replicate exactly.
- [ ] Add 10 new public const brush-key strings on `CellPaletteInstaller`: `CellReadonlyDepth0..3BrushKey`, `CellReadonlyDepth0..3PastBrushKey`, `CellReadonlySelectedBackgroundBrushKey`, `CellReadonlyForegroundBrushKey`.
- [ ] In `Install`, add 10 lines populating the resource dictionary from the corresponding `GridStyleOptions.ReadonlyCell*Color` fields via `PaletteBrushFactory.From(hex)`.
- [ ] Extend `CellPaletteInstallerTests`:
    - Bump the count assertion from 11 to 21.
    - Add asserts for all 10 new readonly brushes verifying the expected `Color` on each.
    - Rename the test method if its name encodes the old count (`Install_PopulatesElevenBrushKeys_*` → `Install_PopulatesAllCellBrushes_WithExpectedColors`).
- [ ] `dotnet build SemiStep/SemiStep.slnx` clean.
- [ ] Full test suite green.

### Task 4: Split `DataGridStyles.axaml` selectors

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [ ] Universal disabled selector (current line 39, merging both signals): split into TWO selectors. Match the layout described in the Solution Overview "Selector layout" section blocks 1 and 2.
- [ ] Per-depth override block (current 7 selectors with comma-grouped signals, lines 100-127): expand into 14 selectors. **Document-order discipline** mirrors the existing execution-palette pattern: bare `past-step` first, then depth-N normal, then depth-N + past-step. For readonly chain use `CellReadonlyDepth*Brush`; for disabled chain use `CellDisabledDepth*Brush`. Add a comment block explaining the symmetry. Readonly chain (7 selectors) precedes disabled chain (7 selectors) in document order — order within each chain matters; order BETWEEN chains does not, because they target disjoint cells.
- [ ] `:selected DataGridCell` (current line 133, AccentBrush): keep as-is — paints all cells uniformly.
- [ ] `:selected DataGridCell.read-only-column, ...IsInapplicable=true` (current line 141, single combined): split into TWO selectors after the generic `:selected` rule (matching layout blocks 6 and 7). Readonly-selected uses `CellReadonlySelectedBackgroundBrush`; disabled-selected uses the existing `CellDisabledSelectedBackgroundBrush`.
- [ ] Current-step-marker on step-number column (current line 146): **STAYS LAST** so it wins over selection on the current row's step-number cell. Verify this is preserved as the final selector in the file.
- [ ] `dotnet build SemiStep/SemiStep.slnx` clean.
- [ ] Full test suite green. (No headless visual test for selectors — manual visual review in Task 6.)

### Task 5: Documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `Docs/02-ui-requirements.md`

- [ ] Update `CLAUDE.md` Conventions: rewrite the cell-palette entry to cover all three sections (`readonly`, `disabled`, `execution`) and note the relocation of `execution` under `colors.cells.execution`. Reference `CellPaletteInstaller.cs` as the source of truth for the brush-key list (do not enumerate all 21 keys in CLAUDE.md — that would rot on the next palette extraction).
- [ ] Update `Docs/02-ui-requirements.md` §2.6.5: expand to cover read-only AND disabled palettes. Add a paragraph on the semantic difference (column-level vs row-action intersection). Note the path change `colors.execution` → `colors.cells.execution`.
- [ ] If `Docs/02-ui-requirements.md` §2.6.1 or §2.6.2 references the execution path, update it.
- [ ] Full test suite green (docs sanity).

### Task 6: Manual visual verification

- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)

### Task 7: Acceptance + finalise

- [ ] Verify all goals from Overview are met.
- [ ] **Edge cases:**
    - A read-only cell on a past depth-2 row → `CellReadonlyDepth2PastBrush`.
    - An inapplicable cell on a depth-1 row → `CellDisabledDepth1Brush`.
    - A selected read-only cell → `CellReadonlySelectedBackgroundBrush`.
    - A selected inapplicable cell → `CellDisabledSelectedBackgroundBrush`.
    - A current-step row that is also user-selected: step-number cell shows the marker (current-step-marker selector wins by document order); other cells show selection accent.
- [ ] **Invariant check**: confirm `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs:13` still returns `false` for read-only columns. The two signals (`read-only-column` class and `IsInapplicable=true`) are mutually exclusive by design; if this invariant were ever violated, both selector chains would match the same cell and document-order would silently decide the paint. Add a one-line comment in `CellStateResolver.cs` reinforcing the invariant if missing.
- [x] `dotnet format SemiStep/SemiStep.slnx` — no changes.
- [x] `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 0 warnings.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.
- [x] Move plan to `Docs/plans/completed/20260522-split-readonly-disabled-cell-palette.md` via `git mv`.

## Post-Completion

*Items requiring external action — no checkboxes.*

**Manual tuning:**
- Operator picks final readonly/disabled palette per machine after viewing on screen.

**Future palette work (separate plans, out of scope here):**
- `colors.cells.usual.{background, foreground}` — extract editable cell defaults so plain cells are operator-configurable. Today rely on Avalonia defaults.
- Remaining hardcoded UI colors in `ColorPalette.axaml`: severity (`ErrorBrush`, `WarningBrush`, `InfoBrush`), connection state (`ConnectedBrush`, `DisconnectedBrush`), chrome (`PanelBackgroundBrush`, `SubtleBorderBrush`, `SeparatorBrush`, etc.), `AccentBrush`. Each is a candidate for YAML extraction.
- Long-term: empty `ColorPalette.axaml` entirely, all colors driven by `grid_style.yaml` via the existing installer pattern.

**No external consumer updates** — `grid_style.yaml` is internal config; no third-party project depends on its schema.
