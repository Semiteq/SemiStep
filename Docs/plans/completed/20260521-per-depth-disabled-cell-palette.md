# Per-Depth Disabled/Read-only Cell Palette

## Overview

The previous plan (`20260521-configurable-disabled-cell-palette`) moved disabled-cell colors from hardcoded brushes into a single configurable `colors.cells.disabled.{normal,selected,foreground}` block. With loop-depth row tinting active, a single `normal` colour for disabled cells is too coarse: on a depth-2 row a disabled cell still paints the same grey as on a depth-0 row, breaking the visual signal that the row belongs to a loop. The new requirement is full operator control of the disabled background per loop-depth × past-state, mirroring the execution palette's `depth_0..3` and `depth_0..3_past` matrix.

`selected` and `foreground` remain shared (one value applied regardless of depth or past-state) — these signal cell intent (selection, "this is read-only text"), not row context.

## Locked decisions

| Question | Decision |
|----------|----------|
| Schema for disabled backgrounds | 4 depths × {normal, past} = 8 hex keys under `colors.cells.disabled`. Names mirror the execution palette: `depth_0`, `depth_1`, `depth_2`, `depth_3`, `depth_0_past`, `depth_1_past`, `depth_2_past`, `depth_3_past`. |
| Shared keys | `colors.cells.disabled.selected` and `colors.cells.disabled.foreground` remain single shared values. Selected paints over any depth tint; foreground is the text colour of any disabled cell. |
| Old `normal` key | Removed. `depth_0` carries the same semantic ("disabled cell, outside any loop, not past"). Old configs become invalid — they must be migrated in the same commit that lands the validator strict-check. |
| Resource key naming | Rename `CellDisabledBackgroundBrush` → `CellDisabledDepth0Brush`. Add 7 new brush keys: `CellDisabledDepth1Brush`, `CellDisabledDepth2Brush`, `CellDisabledDepth3Brush`, `CellDisabledDepth0PastBrush`, `CellDisabledDepth1PastBrush`, `CellDisabledDepth2PastBrush`, `CellDisabledDepth3PastBrush`. `CellDisabledSelectedBackgroundBrush`, `CellDisabledForegroundBrush`, `GridLineBrush` keep their names. |
| Selector strategy | Mirror the execution-palette selector chain. Bare `past-step` paints depth-0 past first; depth-N normal next; depth-N + past-step last. Each per-depth disabled selector overrides `Opacity` to `1.0` (otherwise the universal disabled-selector's `Opacity=0.5` from `DataGridStyles.axaml:42` blends depth tint through). |
| Validation | All 10 keys mandatory, regex hex check, no silent fallback. Mapper drops `??` on the 10 reads under validator-first invariant. |
| `selected + tinted` interaction | Unchanged: user selection still wins over per-depth disabled, the existing `DataGridRow:selected DataGridCell.read-only-column, ...IsInapplicable=True` selector at the end of the cascade paints `CellDisabledSelectedBackgroundBrush` for disabled cells in selected rows. |

## Critical files

- `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleDisabledCellColorsDto.cs` — replace 3 properties (`Normal`, `Selected`, `Foreground`) with 10 properties: `Depth0`, `Depth1`, `Depth2`, `Depth3`, `Depth0Past`, `Depth1Past`, `Depth2Past`, `Depth3Past`, `Selected`, `Foreground` (aliases `depth_0`..`depth_3_past`, `selected`, `foreground`).
- `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` — rename `DisabledCellNormalColor` → `DisabledCellDepth0Color`. Add 7 new fields: `DisabledCellDepth1Color`, `DisabledCellDepth2Color`, `DisabledCellDepth3Color`, `DisabledCellDepth0PastColor`, `DisabledCellDepth1PastColor`, `DisabledCellDepth2PastColor`, `DisabledCellDepth3PastColor`. `DisabledCellSelectedBackgroundColor` and `DisabledCellForegroundColor` unchanged. Update `Default` with `#000000` sentinels for the 7 new fields.
- `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs` — read the 8 depth/past fields directly from `dto.Colors!.Cells!.Disabled!` without `??` (validator-first invariant). `Selected` and `Foreground` reads are unchanged.
- `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs` — extend the disabled-cells validation block: 8 new `(key, value)` tuples in the tuple-array loop covering `depth_0..3` and `depth_0..3_past`. Keep `selected` and `foreground` entries. All hex regex checked.
- `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs` — rename `CellDisabledBackgroundBrush` resource key to `CellDisabledDepth0Brush`. Add 7 new `Install` lines for the new depth keys. `CellDisabledSelectedBackgroundBrush`, `CellDisabledForegroundBrush`, `GridLineBrush` lines unchanged.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — change the universal disabled selector at line 39-43 to use `CellDisabledDepth0Brush` (the depth-0 case). Replace the single combined disabled-restore selector (currently around lines 109-118) with 7 per-depth selectors mirroring the execution-palette structure. Each new selector sets `Background` and `Opacity = 1.0`.
- `ConfigFiles/MOCVD/ui/grid_style.yaml`, `MBE/ui/grid_style.yaml`, `RIE/ui/grid_style.yaml` — replace `disabled.normal` with `disabled.depth_0`; add 7 new keys (`depth_1..3` and `depth_0..3_past`). `selected` and `foreground` unchanged.
- `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`, `WithGroups/ui/grid_style.yaml`, all 18 `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/<Case>/ui/grid_style.yaml` — same migration. Each ships its own file; all 21 fixtures need an update.
- `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleColorsValidationTests.cs` — extend disabled-palette test cases: parameterise over all 8 depth/past key names for `Validate_MissingDisabledKey_FailsWithKeyName` and `Validate_MalformedDisabledHex_FailsNamingKey`. Keep `selected` and `foreground` entries.
- `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs` — extend resource count assertion from 4 to 11, add asserts for the 7 new depth brushes.
- `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs` — adjust the existing mutation-test cases to use the new key names (e.g. `disabled.normal` → `disabled.depth_0`).
- `CLAUDE.md` — update the "Disabled-cell palette" Conventions entry to list the 10 keys.
- `Docs/02-ui-requirements.md` §2.6.5 — update the prose to describe the per-depth matrix.

## YAML schema (final shape of `colors.cells.disabled`)

```yaml
colors:
  cells:
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
```

Concrete `depth_1..3` values are first-draft — tune during manual visual review.

## DataGridStyles.axaml — final selector layout

```xml
<!-- Universal disabled cell appearance: foreground, opacity, depth-0 normal background. -->
<Style Selector="DataGridCell.read-only-column, DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth0Brush}" />
    <Setter Property="TextElement.Foreground" Value="{DynamicResource CellDisabledForegroundBrush}" />
    <Setter Property="Opacity" Value="0.5" />
</Style>

<!-- (existing grid-level / read-only mode / depth tint selectors stay where they are) -->

<!-- Per-depth disabled background overrides. Document-order discipline mirrors the execution palette:
     bare past-step first, then depth-N normal, then depth-N + past-step. Each sets Opacity=1.0 so the
     depth tint underneath does not bleed through 50% opacity grey. -->
<Style Selector="DataGridRow.past-step DataGridCell.read-only-column,
                 DataGridRow.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth0PastBrush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-1 DataGridCell.read-only-column,
                 DataGridRow.for-depth-1 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth1Brush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-1.past-step DataGridCell.read-only-column,
                 DataGridRow.for-depth-1.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth1PastBrush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-2 DataGridCell.read-only-column,
                 DataGridRow.for-depth-2 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth2Brush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-2.past-step DataGridCell.read-only-column,
                 DataGridRow.for-depth-2.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth2PastBrush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-3 DataGridCell.read-only-column,
                 DataGridRow.for-depth-3 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth3Brush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>
<Style Selector="DataGridRow.for-depth-3.past-step DataGridCell.read-only-column,
                 DataGridRow.for-depth-3.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledDepth3PastBrush}" />
    <Setter Property="Opacity" Value="1.0" />
</Style>

<!-- (existing :selected, :selected disabled, and current-step marker selectors follow unchanged) -->
```

## Development Approach

- Each task leaves the build and the full test suite green.
- Pre-commit hook (`dotnet format`) must pass.
- Subagents check off `[ ]` items in the assigned task.

## Implementation Steps

### Task 1: Extend DTO + GridStyleOptions + Mapper (tolerant)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleDisabledCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`

- [ ] In `GridStyleDisabledCellColorsDto`: rename `Normal` → `Depth0` (alias `depth_0`). Add `Depth1`/`Depth2`/`Depth3`, `Depth0Past`/`Depth1Past`/`Depth2Past`/`Depth3Past` properties. Keep `Selected` and `Foreground`.
- [ ] In `GridStyleOptions`: rename `DisabledCellNormalColor` → `DisabledCellDepth0Color`. Add `DisabledCellDepth1Color`, `DisabledCellDepth2Color`, `DisabledCellDepth3Color`, `DisabledCellDepth0PastColor`, `DisabledCellDepth1PastColor`, `DisabledCellDepth2PastColor`, `DisabledCellDepth3PastColor`. Update `Default` with `#000000` sentinels for the new fields.
- [ ] In `GridStyleMapper`: update the existing read for `Depth0` (renamed from `Normal`). Add 7 new reads for the depth fields. Use `?? defaults.X` fallback temporarily so unchanged fixtures still load (Task 3 will switch to strict).
- [ ] `dotnet build SemiStep/SemiStep.slnx` clean.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green. Existing tests should pass because `??` fallback masks the missing-section problem in fixtures.

### Task 2: Migrate all YAML configs to new schema

**Files:**
- Modify: `ConfigFiles/MOCVD/ui/grid_style.yaml`
- Modify: `ConfigFiles/MBE/ui/grid_style.yaml`
- Modify: `ConfigFiles/RIE/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/ui/grid_style.yaml`
- Modify: 18 × `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/<Case>/ui/grid_style.yaml`

- [ ] In every YAML config that today has `colors.cells.disabled.normal`, rename `normal` → `depth_0`. Add `depth_1`, `depth_2`, `depth_3`, `depth_0_past`, `depth_1_past`, `depth_2_past`, `depth_3_past` with the draft hex values from the schema block above. Keep `selected` and `foreground`.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` full suite green. The tolerant mapper from Task 1 means missing keys (if any fixture is missed) still load with `Default` sentinels — but every fixture should be updated explicitly.

### Task 3: Strict validator + drop mapper fallback

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleColorsValidationTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeGridStyleValidationTests.cs`

- [ ] Extend `GridStyleValidator.Validate` disabled-palette block: 8 new tuples for `depth_0..depth_3_past` keys in the tuple-array loop. Keep `selected` and `foreground` entries. Hex regex reused.
- [ ] Drop `?? defaults.X` from the 8 depth-field reads in `GridStyleMapper` (validator-first invariant guarantees presence).
- [ ] Parameterise `Validate_MissingDisabledKey_FailsWithKeyName` and `Validate_MalformedDisabledHex_FailsNamingKey` over all 10 keys (or extend the existing `MemberData`). Use the structural match `Contains($"colors.cells.disabled.{keyName}")` already in place.
- [ ] Update any existing mutation-key reference in `ConfigFacadeGridStyleValidationTests.cs` to the new naming (`normal` → `depth_0`).
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` green.
- [ ] Full suite green.

### Task 4: Update installer + style selectors

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [x] In `CellPaletteInstaller`: rename `CellDisabledBackgroundBrushKey` → `CellDisabledDepth0BrushKey` (string value `"CellDisabledDepth0Brush"`). Add 7 new const keys + 7 new install lines for `Depth1..3` and `Depth0..3Past`.
- [x] In `DataGridStyles.axaml`:
    - Change the universal disabled selector at L39-43 to reference `{DynamicResource CellDisabledDepth0Brush}`.
    - Replace the single combined disabled-restore selector (currently around L109-118) with the 7 per-depth selectors from the "DataGridStyles.axaml — final selector layout" section above. Document ordering in a leading comment.
    - Verify `:selected DataGridCell` (L124-127) and `:selected DataGridCell.read-only-column` (currently the last "selected disabled" override) remain in their winning positions.
- [x] Extend `CellPaletteInstallerTests`: bump count assertion from 4 to 11. Add asserts for the 7 new depth brushes.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] Full suite green.

### Task 5: Docs

**Files:**
- Modify: `CLAUDE.md`
- Modify: `Docs/02-ui-requirements.md`

- [x] Update the "Disabled-cell palette in `ui/grid_style.yaml`" entry in `CLAUDE.md` Conventions: list the 10 keys, mention that disabled background is now per-depth × past while `selected` and `foreground` remain shared.
- [x] Update `Docs/02-ui-requirements.md` §2.6.5 prose: describe the per-depth matrix; note that the `selected` colour wins over depth when the user selects the row, and `foreground` applies universally.
- [x] Run full suite (docs changes; sanity check only).

### Task 6: Manual visual verification

- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)

### Task 7: Final format + build + test

- [x] `dotnet format SemiStep/SemiStep.slnx` — no changes.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` full suite green.
- [x] Move plan to `Docs/plans/completed/`.

## What is explicitly NOT in this change

- No change to `colors.execution` palette (execution overlay stays as-is).
- No change to the active-row marker or current-step rendering.
- No change to `RowLoopDepths` semantics or `ForDepth` propagation.
- No new colors for `:selected` per depth — `selected` stays shared as one value.
- No backwards-compatibility shim for old `disabled.normal` key. Validator hard-fails on its absence.

## Risks

- **Fixture churn**: 21 YAML files updated in Task 2. Each ships its own `colors.cells.disabled` block; missing one means full-suite failure in Task 3 after the validator turns strict. Suggest scripting the migration (replace `normal:` → `depth_0:`, append the 7 new keys).
- **Resource key rename**: `CellDisabledBackgroundBrush` → `CellDisabledDepth0Brush` affects `DataGridStyles.axaml` (universal disabled selector at L40). Verify no other consumer of the old key remains via `Grep "CellDisabledBackgroundBrush"` pre-flight in Task 4.
- **Selector order regression**: 7 new selectors must respect Avalonia document order (last-match-wins). The risk is identical to the execution-palette ordering problem already encountered — apply the same discipline.
- **Opacity gotcha**: the universal disabled selector sets `Opacity=0.5`. Each per-depth disabled selector must override to `1.0`, otherwise depth tint will bleed through. The plan shows this explicitly; subagents must include it.
