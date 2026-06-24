# Lighten selected-row background so the focused cell stays visible (issue #67)

## Overview

Feedback point 11: the selected-row blue (`AccentBrush` = `#0078D7`, saturated dark blue) dominates the
row, so the actual focused/edited cell inside the selection is hard to see, and white selection text sits
on a heavy fill. Switch selection to a light-blue background with dark text across all three selected-cell
variants (generic, read-only, inapplicable), so cell content stays readable and the focused cell is still
distinguishable.

While here, resolve the dead-config discrepancy: `GridStyleOptions.SelectionBackgroundColor` /
`SelectionForegroundColor` already exist and are already mapped from YAML (`GridStyleMapper.cs:31-32`,
`dto.Colors?.Selection?.Background/Foreground`) but are never installed as brushes — the grid uses the
static `AccentBrush` instead. Wire them through (the issue's preferred option), so selection styling is
decoupled from `AccentColor` and configurable from YAML.

## Context (from discovery)

- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — selection rules:
  - generic selected cell (`:selected DataGridCell`, ~170-173): bg `AccentBrush`, fg `TextOnAccentBrush`.
  - read-only selected (`:selected DataGridCell.read-only-column`, ~178-181): bg `CellReadOnlySelectedBackgroundBrush`, fg `TextOnAccentBrush`.
  - inapplicable selected (`:selected DataGridCell[IsInapplicable=True]`, ~185-188): bg `CellDisabledSelectedBackgroundBrush`, fg `TextOnAccentBrush`.
  - current-step marker (~190-192) MUST remain the final selector (wins over selection on the step-number cell).
- `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml` — `AccentColor`/`AccentBrush` (#0078D7), `TextOnAccentBrush` (#FFFFFF). Keep `AccentColor` unchanged (do NOT mutate it); other UI may use `AccentBrush`.
- `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs` — installs 21 cell brushes from `GridStyleOptions`. Does NOT install the selection brushes today. Brush-key constants live here.
- `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` — `SelectionBackgroundColor`/`SelectionForegroundColor` fields exist (Default `#0078D7` / `#FFFFFF`).
- `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs:31-32` — already maps `Colors.Selection.Background/Foreground` into the options (so the YAML path is live; only the brush install is missing). No DTO/mapper change needed.
- `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml` — each has `colors.selection.background/foreground: "#0078D7"` and `colors.cells.read_only.selected` / `colors.cells.disabled.selected` (mid-blues).
- `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs` — asserts each installed brush and `resources.Count.Should().Be(21)`. Adding 2 selection brushes makes it 23; this test MUST be updated.

### Patterns observed

- All cell colors are config-driven via `GridStyleOptions` → `CellPaletteInstaller` → `DynamicResource` brush keys consumed by `DataGridStyles.axaml`. The new selection brushes follow the identical pattern.
- C# UTF-8 with BOM; one class per file; the installer is a static class with `…BrushKey` const strings.

### Confirmed decision

- **Wire the dead config** (issue's preferred option): add `SelectionBackgroundBrush`/`SelectionForegroundBrush`, install them from the already-mapped `GridStyleOptions` fields, set light defaults in YAML. No new DTO/mapper/validator. Removes the dead-field smell.
- Generic selection uses the new `SelectionBackgroundBrush`; read-only/inapplicable selected keep their own (config-driven) background fields but lightened in YAML; all three selected foregrounds switch to the dark `SelectionForegroundBrush`.

## Development Approach

- Regular approach (config/style change with a brush-install unit test).
- One logical change per task; all UI/config layer.
- After each task: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` and the relevant test slice.

## Testing Strategy

- **Unit**: extend `CellPaletteInstallerTests` to assert the two new selection brushes install with the expected colors and the resource count is 23.
- **Config**: run the Config slice to confirm the YAML changes still load/validate.
- **Manual smoke**: the actual rendered selection (light bg, dark readable text, focused cell visible within a multi-row selection, across generic/read-only/inapplicable cells) — no headless visual assertion.

## Solution Overview

Selection becomes a first-class, config-driven palette entry like the other cell states. The DataGrid
cascade keeps its structure; only the brush references inside the three `:selected` rules change (generic
bg + all-three fg), and the previously-dead `Selection*` config fields are now the source of truth for the
selection background/foreground.

## Technical Details

- New brush keys in `CellPaletteInstaller`: `SelectionBackgroundBrushKey = "SelectionBackgroundBrush"`, `SelectionForegroundBrushKey = "SelectionForegroundBrush"`, installed from `gridStyle.SelectionBackgroundColor` / `gridStyle.SelectionForegroundColor`.
- Light defaults (exact hexes are reviewer-tweakable): selection background `#CCE4F7` (light blue), selection foreground `#202020` (near-black); read-only selected and inapplicable selected backgrounds lightened to light tints (e.g. `#DCE8F4` / `#E2E9F2`) so all three read as light with dark text.
- `GridStyleOptions.Default` selection values updated to the light default (so shape/wiring tests and any default fallback are consistent).

## What Goes Where

- **Implementation Steps**: brush install + keys, DataGrid cascade repoint, YAML light defaults, installer test, verification.
- **Post-Completion**: manual visual smoke; the #74 dark-theme pass will re-evaluate the selection shade for the dark palette.

## Implementation Steps

### Task 1: Install selection brushes from config

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [x] Add `SelectionBackgroundBrushKey` and `SelectionForegroundBrushKey` const strings.
- [x] In `Install`, register `resources[SelectionBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.SelectionBackgroundColor)` and the foreground equivalent.
- [x] Update `CellPaletteInstallerTests`: add `SelectionBackgroundColor`/`SelectionForegroundColor` to the input `GridStyleOptions`, add an `AssertBrush` for each new key, and update `resources.Count.Should().Be(21)` to `23`.
- [x] Build and run: `dotnet test … --filter "FullyQualifiedName~CellPaletteInstallerTests"`. Must pass before next task.

### Task 2: Repoint DataGrid selection rules

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [x] Generic `:selected DataGridCell`: change `Background` from `AccentBrush` to `{DynamicResource SelectionBackgroundBrush}` and `TextElement.Foreground` from `TextOnAccentBrush` to `{DynamicResource SelectionForegroundBrush}`.
- [x] Read-only selected rule: keep `Background = CellReadOnlySelectedBackgroundBrush`, change `Foreground` to `SelectionForegroundBrush`.
- [x] Inapplicable selected rule: keep `Background = CellDisabledSelectedBackgroundBrush`, change `Foreground` to `SelectionForegroundBrush`.
- [x] Confirm the current-step marker rule remains the final selector and rule order is otherwise unchanged. Update/keep the section comment to reflect that selection bg/fg now come from the dedicated selection brushes.
- [x] Build the UI project (no unit test — visual; covered by manual smoke). Must build clean before next task.

### Task 3: Light selection defaults in config

**Files:**
- Modify: `ConfigFiles/MBE/ui/grid_style.yaml`
- Modify: `ConfigFiles/MOCVD/ui/grid_style.yaml`
- Modify: `ConfigFiles/RIE/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`

- [x] In each production `grid_style.yaml`, set `colors.selection.background` to the light blue. NOTE: `colors.selection.foreground` currently reads `"#0078D7"` (blue, harmless today because the live foreground comes from the static `TextOnAccentBrush`); once Task 1 wires `SelectionForegroundBrush` from config this value goes live, so it MUST be set to the dark color (`#202020`) in all three YAMLs, not left as-is. Lighten `colors.cells.read_only.selected` and `colors.cells.disabled.selected` to light tints so all three selected variants read light.
- [x] Update `GridStyleOptions.Default.SelectionBackgroundColor`/`SelectionForegroundColor` to the light defaults.
- [x] Confirm no test asserts the old `#0078D7` selection color (review found none — `GridStyleMapperTests` omits the `Selection` DTO and `Map_NullDto_ReturnsDefaults` is a record self-comparison, both stay green). Do not change mapper tests.
- [x] `GridStyleValidator` does NOT validate the `selection` section (only execution/readonly/disabled) — that is pre-existing and OUT OF SCOPE; do not add selection validation here.
- [x] Run the Config slice: `dotnet test … --filter "Component=Config"`. Note: test YAML configs (`YamlConfigs/Standard/...`) have no `selection:` block and fall back to the mapper default, so this slice does not exercise the new YAML keys directly; it only confirms nothing regressed. Add a `selection:` block to `SemiStep.Tests/YamlConfigs/Standard/ui/grid_style.yaml` so the keys get real load coverage. Must pass before next task.

### Task 4: Verify acceptance criteria

- [x] All four issue tasks satisfied: selected bg light, selected fg dark, applied to generic/read-only/inapplicable, dead `Selection*` config wired (not removed). Acceptance trace: generic selected bg `SelectionBackgroundBrush` + fg `SelectionForegroundBrush` (`DataGridStyles.axaml:183-186`); read-only selected keeps `CellReadOnlySelectedBackgroundBrush` bg, fg `SelectionForegroundBrush` (`:192-195`); inapplicable selected keeps `CellDisabledSelectedBackgroundBrush` bg, fg `SelectionForegroundBrush` (`:199-202`); brushes installed from config in `CellPaletteInstaller.cs:58-59` (`SelectionBackgroundColor`/`SelectionForegroundColor`); options default `#CCE4F7`/`#202020` (`GridStyleOptions.cs:70-71`); mapped from YAML (`GridStyleMapper.cs:31-32`); all three YAMLs set selection `#CCE4F7`/`#202020`, read-only selected `#DCE8F4`, disabled selected `#E2E9F2`. Config wired, not removed.
- [x] Build all: `dotnet build SemiStep/SemiStep.slnx` — 0 errors, 12 warnings (all pre-existing NU1902 NCalc).
- [x] Run UI + Config slices. UI: 267 passed, 0 failed (no spurious mass-failure this run; CellPaletteInstallerTests included and pass). Config: 212 passed, 0 failed. Core (regression check): 235 passed, 0 failed.
- [x] `dotnet format SemiStep/SemiStep.slnx` clean (`--verify-no-changes` exit 0, no changes).

### Task 5: Finalize

- [x] Confirm no stray dead reference to the old selection styling remains: `AccentBrush` is no longer used by selection. Grep across `.axaml`/`.cs` shows `AccentBrush` only in its own `ColorPalette.axaml` definition plus a decoupling comment in `DataGridStyles.axaml:182`; kept as a general palette entry. `AccentColor` unchanged.
- [x] Grep `TextOnAccentBrush` / `TextOnAccentColor`: zero consumers remain across `.axaml`/`.cs` (only historical mentions in plan markdown). Now orphaned, so both resources were removed from `ColorPalette.axaml` along with the stale "Selected text foreground (white-on-accent)" comment. Rebuilt clean; re-grep confirms no live reference.
- [x] Move this plan to `Docs/plans/completed/`. (deferred to end of exec run — kept in place for review phases)

## Post-Completion

**Manual verification:**
- Select rows (single and multi); confirm the focused/edited cell is visible within the light selection, text is readable, and read-only and inapplicable cells under selection also read light with dark text.
- A changed (orange) cell that is also selected renders the selection background, not orange (the `IsChanged` rule sits before `:selected` in the cascade). This is pre-existing #63 behavior, unchanged here — not a regression.
- The #74 dark-theme pass will re-evaluate the selection shade against the dark palette.
