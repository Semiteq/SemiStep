# Configurable Disabled/Read-only Cell Palette

## Overview

The grey background of disabled / read-only DataGrid cells is currently hardcoded in `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml` (`#E0E0E0` for background, `#808080` for foreground, `#89B4D7` for selected). YAML schema (`ui/grid_style.yaml`) already loads `colors.cells.{enabled,readonly,disabled}.{normal,selected}` into `GridStyleOptions` via `GridStyleMapper`, but none of those fields are wired to the brushes the styles consume. The config is dead data; the hardcoded brushes are the actual source of truth.

This refactor moves the cell palette to YAML, analogous to the execution palette landed in the previous plan. It also fixes a visual regression introduced by the loop-depth tinting: disabled cells inside `For…End_For` blocks lose their grey background to the depth tint because `DataGridRow.for-depth-N DataGridCell` overrides `DataGridCell.read-only-column`'s background via document-order wins.

## Locked decisions

| Question | Decision |
|----------|----------|
| Number of cell-state palettes | **One** unified "disabled" palette (normal + selected + foreground). Visually `read-only-column` and `IsInapplicable=true` already render identically via the same brush — splitting in YAML is dead code. |
| Dead `enabled` / `readonly` sections | **Drop** from DTO, options, mapper, validator, all YAMLs. They are loaded but never consumed in the UI — purging removes confusion. |
| Configurable foreground for disabled cells | **Yes**. Add `colors.cells.disabled.foreground`. Today `#808080` is hardcoded in `ColorPalette.axaml`. |
| Validation policy | Required hex (`^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$`) for all three keys. Missing section or key → hard configuration error, no silent fallback. Mirrors `colors.execution`. |
| Installer | New `SemiStep.UI/Styles/CellPaletteInstaller.cs`, static. Called from `App.OnFrameworkInitializationCompleted` next to `ExecutionPaletteInstaller.Install`. |
| `GridLineBrush` | Currently uses `StaticResource CellDisabledBackgroundColor` via axaml. Decouple — install `GridLineBrush` from `GridStyleOptions.GridLineColor` (already loaded from YAML `colors.grid_line`). |
| Disabled-on-tinted selectors | Single `Style` element with a comma-separated selector list covering `for-depth-{1,2,3}.read-only-column`, `for-depth-{1,2,3}` with `InapplicableCellTheme.IsInapplicable=True`, `past-step.read-only-column`, `past-step` with Inapplicable. Sets only `Background` → `CellDisabledBackgroundBrush`. Placed between depth-tint selectors and the `:selected` override so user selection still wins. |
| Foreground/opacity on tinted disabled cells | The existing `DataGridCell.read-only-column { Foreground, Opacity }` setters at `DataGridStyles.axaml:39-43` already apply universally; the new selectors override only `Background`. |

## Critical files

- `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleColorsDto.cs` — replace `Enabled`, `Readonly`, `Disabled`, `NormalForeground` with just `Disabled` of type `GridStyleDisabledCellColorsDto?`.
- `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleDisabledCellColorsDto.cs` — new. Three nullable string properties: `Normal` (alias `normal`), `Selected` (alias `selected`), `Foreground` (alias `foreground`).
- `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellStateColorsDto.cs` — delete (no longer referenced).
- `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellColorsDto.cs` — delete or thin to just the parent; consider whether it still has reason to exist. If only `Disabled` remains, the parent could be flattened (`colors.cells: {...}` → `colors.disabled: {...}` directly), but keeping the nesting matches the YAML schema documentation precedent. **Decision**: keep `Cells` wrapper for namespace clarity in YAML.
- `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` — remove `EnabledCellNormalColor`, `EnabledCellSelectedColor`, `ReadonlyCellNormalColor`, `ReadonlyCellSelectedColor`, `DisabledCellSelectedColor`, `NormalForegroundColor`. Keep `DisabledCellNormalColor`. Add `DisabledCellForegroundColor`, `DisabledCellSelectedBackgroundColor` (rename if needed for clarity). Update `Default` with `#000000` sentinels for the new fields and remove sentinels for the deleted ones.
- `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs` — drop the removed fields, add the three new disabled-palette reads from `dto.Colors?.Disabled` without `??` (validator guarantees presence).
- `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs` — extend `Validate` to also require `colors.cells.disabled.{normal,selected,foreground}`. Re-use the existing hex regex and `ValidateKey` helper.
- `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs` — new static class. Writes `CellDisabledBackgroundBrush`, `CellDisabledSelectedBackgroundBrush`, `CellDisabledForegroundBrush`, `GridLineBrush` to the supplied `IResourceDictionary`. Uses `Avalonia.Media.Color.Parse` (safe because validator already ran).
- `SemiStep/SemiStep.UI/App.axaml.cs` — in `OnFrameworkInitializationCompleted`, call `CellPaletteInstaller.Install(Resources, gridStyle)` next to the existing `ExecutionPaletteInstaller.Install` call.
- `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml` — delete `CellDisabledBackgroundColor`, `CellDisabledForegroundColor`, `CellDisabledSelectedBackgroundColor`, the three `<SolidColorBrush>` declarations that consume them, and the `GridLineBrush` declaration (now installer-driven). Pre-flight `Grep` to verify no other consumer of the deleted Color keys.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — between the last depth selector (`for-depth-3.past-step DataGridCell`) and the `:selected` selector, add one new `Style` with a comma-separated selector covering all combinations of `for-depth-{1,2,3}` / `past-step` × `read-only-column` / `InapplicableCellTheme.IsInapplicable=True`, setting `Background` only.
- `ConfigFiles/MOCVD/ui/grid_style.yaml`, `MBE/ui/grid_style.yaml`, `RIE/ui/grid_style.yaml` — drop `colors.cells.enabled`, `colors.cells.readonly`, `colors.cells.normal_foreground`. Add `colors.cells.disabled.foreground: "#808080"`. The `disabled.normal` and `disabled.selected` keys are already present.
- `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml` — currently has **no `colors.cells` section at all**. Task 2 must ADD the full `colors.cells.disabled: { normal, selected, foreground }` block (three keys, not just `foreground`) because the validator becomes hard-fail. Per worktree CLAUDE.md, Standalone overlays copy from Standard and overlay only differences, so a single Standard edit propagates to all Standalone cases unless an overlay explicitly replaces `ui/grid_style.yaml`.
- `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/ui/grid_style.yaml` — verify it is loaded standalone (not as an overlay onto Standard). If standalone, also needs the full block. If overlaid, inherits from Standard.
- `SemiStep/SemiStep.Tests/YamlConfigs/Standalone/<Case>/ui/grid_style.yaml` — confirm via the test harness whether each Standalone case **overlays** onto Standard (no edit needed — inherits the section) or **replaces** `ui/grid_style.yaml` entirely (would need the block added). Per the existing fixture content, every Standalone case ships its own `ui/grid_style.yaml` file, so they replace rather than overlay; each must be checked. Best to script this in Task 2.
- `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleExecutionValidationTests.cs` — rename to `GridStyleColorsValidationTests.cs` (or split) since the validator now covers both `execution` and `cells.disabled`. Existing execution test cases remain; add equivalent cases for the three disabled keys (missing section, each key missing, malformed hex).
- `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs` — new, mirrors `ExecutionPaletteInstallerTests`. Asserts the four installed resources after `Install(...)`.
- `SemiStep/SemiStep.Tests/Core/Configuration/ConfigFacadeExecutionPaletteTests.cs` — extend or add a sibling test that mutates `colors.cells.disabled` to verify the new validation reaches `ConfigFacade.LoadAndValidateAsync`.

## DataGridStyles.axaml — final selector block (additions)

```xml
<!-- Restore disabled background on tinted rows.
     Placed after depth selectors and before :selected: depth tint loses to
     disabled grey, but user selection still wins on top via the :selected rule
     below. The existing read-only / Inapplicable Foreground+Opacity setters
     (above) apply universally; this rule only touches Background. -->
<Style Selector="DataGridRow.for-depth-1 DataGridCell.read-only-column,
                 DataGridRow.for-depth-1 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True],
                 DataGridRow.for-depth-2 DataGridCell.read-only-column,
                 DataGridRow.for-depth-2 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True],
                 DataGridRow.for-depth-3 DataGridCell.read-only-column,
                 DataGridRow.for-depth-3 DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True],
                 DataGridRow.past-step DataGridCell.read-only-column,
                 DataGridRow.past-step DataGridCell[(rg|InapplicableCellTheme.IsInapplicable)=True]">
    <Setter Property="Background" Value="{DynamicResource CellDisabledBackgroundBrush}" />
</Style>
```

## YAML schema (final shape of `colors.cells`)

```yaml
colors:
  cells:
    disabled:
      normal: "#E0E0E0"
      selected: "#89B4D7"
      foreground: "#808080"
```

`enabled`, `readonly`, `normal_foreground` are removed.

## Development Approach

- Each task leaves the build and full test suite green.
- Pre-commit hook (`dotnet format`) must pass.
- Subagents check off `[ ]` items in their task section.

## Implementation Steps

### Task 1: Drop dead `colors.cells.{enabled,readonly}` from configs and DTO

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellColorsDto.cs`
- Delete: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellStateColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `ConfigFiles/{MOCVD,MBE,RIE}/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Tests/YamlConfigs/WithGroups/ui/grid_style.yaml`
- (Standalone Standalone test fixtures generally lack `enabled`/`readonly` sections — verify with grep, only edit the ones that do.)

- [x] Pre-flight: `Grep "EnabledCell|ReadonlyCell|NormalForegroundColor|DisabledCellSelectedColor"` across the solution to confirm only DTO/mapper/test sites consume them — no UI consumers. NormalForegroundColor has no axaml or runtime consumer (default DataGrid foreground will paint normal-cell text).
- [x] Remove `Enabled`, `Readonly`, `NormalForeground` properties from `GridStyleColorsDto`.
- [x] Reduce `GridStyleCellColorsDto` to only the `Disabled` property.
- [x] Delete `GridStyleCellStateColorsDto.cs` if it has no remaining references. (Skipped — still referenced by `GridStyleCellColorsDto.Disabled`; Task 2 will replace its type.)
- [x] Remove `EnabledCellNormalColor`, `EnabledCellSelectedColor`, `ReadonlyCellNormalColor`, `ReadonlyCellSelectedColor`, `NormalForegroundColor` from `GridStyleOptions` record + `Default`.
- [x] Update `GridStyleMapper.Map` accordingly.
- [x] Update production YAML configs (MOCVD/MBE/RIE) to remove `enabled`, `readonly`, `normal_foreground`. Standalone fixtures generally do not have these sections — grep first, only edit hits. Standard and WithGroups currently lack `colors.cells` entirely, so nothing to remove there.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` full suite green.

### Task 2: Add `Foreground` to disabled DTO and `GridStyleOptions`; validator covers all three

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleDisabledCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleCellColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: all YAML configs (production + fixtures) — add `colors.cells.disabled.foreground`.
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleDisabledPaletteValidationTests.cs` (or extend the execution validation test).

- [x] Create `GridStyleDisabledCellColorsDto` with three nullable string properties under YAML aliases `normal`, `selected`, `foreground`.
- [x] Switch `GridStyleCellColorsDto.Disabled` to the new DTO type.
- [x] Rename `DisabledCellSelectedColor` → `DisabledCellSelectedBackgroundColor`. Keep `DisabledCellNormalColor` as-is (existing name, consistent with sibling fields, semantic "background" is implicit). Add `DisabledCellForegroundColor`. Net: three fields total in `GridStyleOptions` (`DisabledCellNormalColor`, `DisabledCellSelectedBackgroundColor`, `DisabledCellForegroundColor`).
- [x] Update `Default` with `#000000` sentinels for all three.
- [x] In `GridStyleMapper`, read the three fields from `dto.Colors?.Disabled!.Normal!` etc. without `??` — validator-first invariant.
- [x] Extend `GridStyleValidator.Validate`: validate that `Colors.Cells?.Disabled` is non-null and `Normal`, `Selected`, `Foreground` are present + match hex regex. Reuse the existing `ValidateKey` and key-tuple-array loop pattern.
- [x] Add the full `colors.cells.disabled: { normal: "#E0E0E0", selected: "#89B4D7", foreground: "#808080" }` block to every YAML config that does not already have it. **Inventory** (verify with grep before editing):
    - Production: `ConfigFiles/{MOCVD,MBE,RIE}/ui/grid_style.yaml` — already have `disabled.normal` and `disabled.selected`, only need to append `foreground`.
    - Test fixtures: `SemiStep/SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml`, `WithGroups/ui/grid_style.yaml`, and every `Standalone/<Case>/ui/grid_style.yaml` that the test harness loads as a complete file (not as an overlay onto Standard) — need to add the entire `colors.cells.disabled` block with all three keys, since they currently lack the section and the new validator will hard-fail.
- [x] After editing, run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` and fix any fixture that the harness loads standalone but was missed.
- [x] Add validation test cases: each of the three disabled keys missing individually fails naming the key; malformed hex on each fails naming the key; missing `Cells.Disabled` section fails. Collect all errors at once.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` green.
- [x] Full suite green.

### Task 3: `CellPaletteInstaller` + App wiring

**Files:**
- Create: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Create: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [x] Create `internal static class CellPaletteInstaller` with `public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)`. Parse the four hex values (`DisabledCellNormalColor`, `DisabledCellSelectedBackgroundColor`, `DisabledCellForegroundColor`, `GridLineColor`) via `Color.Parse`, wrap in `SolidColorBrush`, write to keys `CellDisabledBackgroundBrush`, `CellDisabledSelectedBackgroundBrush`, `CellDisabledForegroundBrush`, `GridLineBrush`.
- [x] In `App.OnFrameworkInitializationCompleted`, call `CellPaletteInstaller.Install(Resources, gridStyle)` immediately after the existing `ExecutionPaletteInstaller.Install(...)` call, before constructing `MainWindow`. Use the same `_serviceProvider`-resolved `gridStyle`. Add a comment alongside both installer calls: `// Both installers must run before MainWindow construction. {DynamicResource} lookups against Application.Resources resolve lazily at first realize; missing keys at that point render as default/nothing.`
- [x] Headless test with `[AvaloniaFact]`, traits `[Component=UI][Category=Unit]`: build a known `GridStyleOptions`, install into a fresh `ResourceDictionary`, assert four keys present with the expected `Color` on each brush.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] Full test suite green.

### Task 4: Remove hardcoded brushes from `ColorPalette.axaml`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`

- [x] Pre-flight: `Grep "CellDisabledBackgroundColor|CellDisabledForegroundColor|CellDisabledSelectedBackgroundColor|CellDisabledBackgroundBrush|CellDisabledForegroundBrush|CellDisabledSelectedBackgroundBrush|GridLineBrush"` across `SemiStep/` to enumerate all consumers. Known consumers to verify: `DataGridStyles.axaml` (lines 39-43, 64, 80-100), `MessagePanel.axaml:85` (uses `GridLineBrush` for `BorderBrush`). All consumers must use `{DynamicResource ...}` so the installer-published brushes resolve at first realize.
- [x] Delete the four `<Color>` resources: `CellDisabledBackgroundColor`, `CellDisabledForegroundColor`, `CellDisabledSelectedBackgroundColor`.
- [x] Delete the three `<SolidColorBrush>` declarations that referenced them: `CellDisabledBackgroundBrush`, `CellDisabledForegroundBrush`, `CellDisabledSelectedBackgroundBrush`. (They are now installer-driven.)
- [x] Delete the `GridLineBrush` declaration (was `Color="{StaticResource CellDisabledBackgroundColor}"`). Now installer-driven.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] manual test (skipped - not automatable)
- [x] Full test suite green.

### Task 5: Add disabled-in-tinted-row selector in `DataGridStyles.axaml`

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [x] Insert the combined disabled-restore `Style` immediately after `Style Selector="DataGridRow.for-depth-3.past-step DataGridCell"` (currently at lines 98-100) and before `Style Selector="DataGridRow:selected DataGridCell"` (currently at lines 106-109). The placement is order-sensitive: depth Background loses to disabled grey, then `:selected` Background wins on top of grey, then `current-step` marker (lines 111-113) wins on the step-number cell only. Cite both anchor selectors verbatim in a comment above the new style.
- [x] Add a one-paragraph comment block above the new style explaining: it overrides the depth-tint Background back to the disabled grey; document-order placement is between depth and `:selected` so user selection still wins.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean.
- [x] Full test suite green.

### Task 6: Manual visual verification

- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)
- [x] manual test (skipped - not automatable)

### Task 7: Final format + build + test

- [x] `dotnet format SemiStep/SemiStep.slnx` — no changes.
- [x] `dotnet build SemiStep/SemiStep.slnx` clean (0 errors, 0 warnings).
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` full suite green.
- [x] Move plan to `Docs/plans/completed/`.

## What is explicitly NOT in this change

- No new behavior for the `enabled` cell visual state — the dropped YAML keys did not affect rendering. The default DataGrid cell appearance (white background, black foreground) continues to apply through Avalonia defaults.
- No change to the execution palette (`colors.execution`) — that is the previous plan's domain.
- No change to selection brush (`AccentBrush`) — separate hardcoded resource, still in `ColorPalette.axaml`.
- No runtime palette reload (rebuild required).
- No structural changes to recipe data or analysis.

## Risks

- **`GridLineBrush` decoupling**: previously visually identical to `CellDisabledBackgroundColor` (both `#E0E0E0`); after refactor they are independent. If `colors.grid_line` and `colors.cells.disabled.normal` are ever set to different values, the visual will change. Acceptable — this is the goal.
- **Fixture churn**: production configs (3 files) already have `disabled.normal/selected` — only `foreground` is appended. Test fixtures: Standard + WithGroups + each Standalone case that ships its own `ui/grid_style.yaml` need the full `colors.cells.disabled` block (three keys), because the validator becomes hard-fail. Exact count depends on whether each Standalone overlays vs. replaces — confirm during Task 2.
- **Removal of dead fields**: pre-flight greps must confirm no consumer of `EnabledCellNormalColor` etc. before deletion. If any test or sample directly references them via `GridStyleOptions.Default`, replace the read with literal values or delete the test.
