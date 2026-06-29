# Configurable Status-Bar Font Sizes

## Overview
- Make two status-bar font sizes user-configurable through the existing in-app Grid Style
  editor, so the operator can tune them and pick a default:
  - **Status-bar text font size** — the general status-bar font (counts, sync, connection,
    FOR counters), currently the inline `TextElement.FontSize="12"` on the status-bar root.
  - **Status-bar timer font size** — the step/recipe countdown readouts (`Шаг:` / `Рецепт:`),
    which issue #72 wants enlarged (>= 24) for at-a-glance reading.
- Satisfies #72: the timer font becomes adjustable with a default of 24. The earlier attempt
  hard-coded 24 inline; the user found a fixed size unsatisfactory and asked to wire both
  status-bar font sizes into the settings instead.
- The change threads two new integer settings end-to-end through the established style
  pipeline (options record → YAML DTO → both mappers → palette installer → consumer XAML →
  editor view model → editor window), mirroring the existing `StatusBarPadding` /
  `StatusBarItemSpacing` settings exactly.

## Context (from discovery)
The style pipeline, traced on the existing `StatusBarPadding` setting:
- `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` — immutable `record` with a static
  `Default`. Font sizes are `int` (`HeaderFontSize`, `CellFontSize`); sizes/paddings are
  `double`.
- `SemiStep/SemiStep.Core/Configuration/Dto/StatusBarStyleDto.cs` — YAML DTO for the
  `status_bar` section (`background`, `foreground`, `padding`, `item_spacing`). All members are
  nullable; absence falls back to defaults.
- `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs` — DTO → options on load,
  each field `dto.StatusBar?.X ?? defaults.X`.
- `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleDtoMapper.cs` — options → DTO on save.
- `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs` — maps options into a resource
  dictionary under string keys (e.g. `StatusBarPaddingKey` → `new Thickness(...)`,
  `StatusBarItemSpacingKey` → the raw double).
- `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml` — consumer. Root `Border` sets
  `TextElement.FontSize="12"` (cascades to all status-bar text); the execution-time block is the
  `Grid.Column="4"` StackPanel holding the two timer readouts.
- `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` — editable draft. Numeric
  fields are `decimal?` with `SetNumber`; `NumericsInRange` validates ranges
  (`MinFontSize=6`, `MaxFontSize=72`); `Seed` and `BuildRecord` mirror every field.
- `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml` — editor UI; numeric settings
  are `NumericUpDown` rows in the "Fonts and Layout" card.
- Tests: `GridStyleMapperTests`, `CellPaletteInstallerTests`, `GridStyleEditorViewModelTests`,
  `GridStyleWriterTests` cover the pipeline.
- `GridStyleValidator` is colors-only (it validates only `status_bar.background`/`foreground`);
  the new numeric fields intentionally bypass it, exactly like `padding`/`item_spacing`. No
  validator change is needed — this omission is deliberate, not a missed layer.
- The "Fonts and Layout" card in `GridStyleEditorWindow.axaml` holds ALL numeric settings
  (header/cell font, paddings, row height, `StatusBarPadding` at row 7, `StatusBarItemSpacing`
  at row 8, `ValidationPanelMaxHeight` at row 9) under a fixed `RowDefinitions` of ten `Auto`
  rows (indices 0-9). The two new rows go here and require extending `RowDefinitions`.

## Development Approach
- Testing approach: **Regular** (code first, then tests).
- New settings are `int` font sizes (consistent with `HeaderFontSize`/`CellFontSize`), default
  text = 12, timer = 24. Editor range 6–72 (reuse `MinFontSize`/`MaxFontSize`).
- DTO members stay nullable → existing `grid_style.yaml` files need no edits (backward
  compatible; missing keys fall back to defaults). Do not touch the YAML test fixtures.
- Resource values are installed as `double` (the type Avalonia `FontSize` expects).
- Each task completes with passing tests before the next. Run `dotnet format` before finishing.
- Labels stay in their current language (russification is #69).

## Testing Strategy
- **Unit**: extend `GridStyleMapperTests` (round-trip carries both new fields),
  `CellPaletteInstallerTests` (both resource keys installed with correct values/type),
  `GridStyleEditorViewModelTests` (seed + BuildRecord + range validation for both).
- Headless `[AvaloniaFact]` only where a control is involved; the mapper/installer tests are
  plain unit tests.

## Solution Overview
- Add `StatusBarFontSize` (int, default 12) and `StatusBarTimerFontSize` (int, default 24) to
  `GridStyleOptions`, threaded through DTO + both mappers + installer + editor VM + editor XAML.
- `CellPaletteInstaller` exposes `StatusBarFontSizeKey` and `StatusBarTimerFontSizeKey`,
  installed as `double`.
- `AppStatusBar.axaml`: root `Border` `TextElement.FontSize` → `{DynamicResource StatusBarFontSize}`;
  the `Grid.Column="4"` timer StackPanel gets `TextElement.FontSize="{DynamicResource StatusBarTimerFontSize}"`,
  overriding the cascade for just the two readouts.

## Technical Details
- YAML keys under `status_bar`: `font_size`, `timer_font_size` (both `int?`).
- Resource keys: `StatusBarFontSize`, `StatusBarTimerFontSize`.
- Editor VM properties: `decimal? StatusBarFontSize`, `decimal? StatusBarTimerFontSize`, both
  validated with `InRange(..., MinFontSize, MaxFontSize)` and round-tripped via `ToInt`.
- Editor window: two `NumericUpDown` rows (`Minimum="6" Maximum="72" Increment="1"`) labelled
  "Status bar font size" and "Status bar timer font size".

## What Goes Where
- **Implementation Steps** (`[ ]`): Core model/DTO/mappers, UI installer/consumer/editor, tests.
- **Post-Completion** (no checkboxes): the operator visually tunes the two values in the editor
  and chooses the shipped defaults; manual confirmation that the status-bar height looks
  acceptable at the chosen timer size.

## Implementation Steps

### Task 1: Thread the two settings through the Core model and persistence

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/StatusBarStyleDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleDtoMapper.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleMapperTests.cs`

- [x] Add `StatusBarFontSize` (int) and `StatusBarTimerFontSize` (int) to the `GridStyleOptions`
      record and to its `Default` (12 and 24), placed next to the other status-bar fields.
- [x] Add `font_size` and `timer_font_size` (`int?`) to `StatusBarStyleDto`.
- [x] Read both in `GridStyleMapper` (`dto.StatusBar?.X ?? defaults.X`) and write both in
      `GridStyleDtoMapper` (`StatusBar.FontSize = options.StatusBarFontSize`, etc.).
- [x] Extend `GridStyleMapperTests` so a DTO carrying both values round-trips, and absence falls
      back to the defaults (12 / 24).
- [x] Add an options→DTO assertion (the `GridStyleDtoMapper` direction is otherwise unguarded —
      the writer round-trip uses a fixture without the new keys, so both sides default and a
      missing write would not fail): assert `GridStyleDtoMapper.Map(options).StatusBar.FontSize`
      and `.TimerFontSize` equal non-default `options` values. This guards the #72 save path.
- [x] Run tests — `dotnet test --filter "FullyQualifiedName~GridStyleMapper"` must pass.

### Task 2: Install the resources and consume them in the status bar

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [x] Add `StatusBarFontSizeKey` and `StatusBarTimerFontSizeKey` consts and install both with an
      EXPLICIT cast to `double` (`resources[StatusBarFontSizeKey] = (double)gridStyle.StatusBarFontSize;`)
      — the source field is `int`, and an unboxed `int` resource can fail coercion into the
      `double` `FontSize` property.
- [x] In `AppStatusBar.axaml`, replace the root `Border` `TextElement.FontSize="12"` with
      `{DynamicResource StatusBarFontSize}`, and add
      `TextElement.FontSize="{DynamicResource StatusBarTimerFontSize}"` to the `Grid.Column="4"`
      timer StackPanel.
- [x] Extend `CellPaletteInstallerTests` to assert both keys are installed with the expected
      `double` values from a known `GridStyleOptions`, AND update the existing
      `resources.Count.Should().Be(46)` assertion to `48` (two new keys).
- [x] Run tests — `dotnet test --filter "FullyQualifiedName~CellPaletteInstaller"` must pass.
- [x] Build the UI project to confirm the XAML resources resolve.

### Task 3: Surface the settings in the style editor

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`

- [x] Add `decimal? StatusBarFontSize` and `decimal? StatusBarTimerFontSize` (with `SetNumber`),
      seed them in `Seed`, write them in `BuildRecord` (`ToInt`), and validate both in
      `NumericsInRange` with `InRange(..., MinFontSize, MaxFontSize)`.
- [x] In the **"Fonts and Layout" card** of `GridStyleEditorWindow.axaml`: extend its
      `RowDefinitions` from ten to twelve `Auto` entries, then add two rows at `Grid.Row="10"`
      and `Grid.Row="11"` — each a `field-label` `TextBlock` (Grid.Column 0) plus a
      `NumericUpDown` (Grid.Column 1, `Minimum="6" Maximum="72" Increment="1"`) bound to
      `StatusBarFontSize` and `StatusBarTimerFontSize`. Labels: "Status bar font size" and
      "Status bar timer font size". (A child whose `Grid.Row` exceeds the defined rows is
      clamped onto the last row — the `RowDefinitions` extension is mandatory, and no test
      catches its omission.)
- [x] Extend `GridStyleEditorViewModelTests`: seeding from a record populates both; `BuildRecord`
      carries both; an out-of-range value (e.g. 5 or 73) makes `CanSave` false.
- [x] Run tests — `dotnet test --filter "FullyQualifiedName~GridStyleEditorViewModel"` must pass.

### Task 4: Verify acceptance criteria

- [x] Run the affected suites: `dotnet test --filter "Component=Config"` and
      `dotnet test --filter "Component=UI"` (use `VSTEST_CONNECTION_TIMEOUT=600`; a testhost
      "connect timeout" is environmental, re-run).
- [x] Run `dotnet format SemiStep/SemiStep.slnx` and confirm no diff remains.
- [x] Confirm existing `grid_style.yaml` fixtures were not modified and still load (backward
      compatibility via nullable DTO fields).

### Task 5: Finalize
- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion
*Items requiring manual intervention — no checkboxes, informational only.*

**Manual verification / tuning:**
- Open `View > Grid Style Settings...`, adjust "Status bar font size" and "Status bar timer
  font size", Save, and confirm the status bar updates and its height/alignment look acceptable.
- Choose the shipped defaults for both values (the record defaults to 12 / 24).
