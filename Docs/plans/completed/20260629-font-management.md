# Full Font Management for Palette-Driven Text

## Overview
- Extend the Grid Style settings from font *size only* to full font control —
  **family (one global), size, weight, and italic** — for every text role the palette
  already drives, and split the status-bar timer block so the label (`Шаг:`/`Рецепт:`) and
  the countdown value are independent roles.
- Builds on the just-added configurable status-bar font sizes (same branch). The earlier
  single `StatusBarTimerFontSize` (one size for label + value) is replaced by two roles.
- Font roles (all currently palette-driven): **grid header, grid cell, status-bar text,
  status-bar timer label, status-bar timer value** (5 after the split).
- One global font **family** applies to all roles; **size + weight + italic** are per-role.
- Changes apply on restart (unchanged model: palettes install at app start, grid fonts are
  assigned in cell/column factories at build time; the editor's existing RestartPromptDialog
  covers this). Live apply is explicitly out of scope.

## Context (from discovery)
Two distinct consumer mechanisms — the plan must handle both:
- **Status-bar roles use a `DynamicResource` cascade** (inherited `TextElement.*` attached
  properties on a container):
  - `AppStatusBar.axaml:25` — root `Border` sets `TextElement.FontSize="{DynamicResource StatusBarFontSize}"`; all non-timer status text inherits it.
  - `AppStatusBar.axaml:124` — `Grid.Column="4"` StackPanel sets `TextElement.FontSize="{DynamicResource StatusBarTimerFontSize}"`; **both** the labels (`:129`,`:138`) and values (`:132`,`:141`) inherit it today. These must be split: labels → timer-label role, values → timer-value role.
- **Grid roles are code-assigned** in factories (NOT resources), read from the injected
  `GridStyleOptions`:
  - Header: `ColumnBuilder.cs:66` `FontSize = gridStyle.HeaderFontSize` (and a hard-coded `FontWeight = FontWeight.Bold` at `:67`).
  - Cells: `TextCellFactory.cs:52,82,128`, `ComboBoxCellFactory.cs:120`, `ColumnBuilder.cs:49` all set `FontSize = gridStyle.CellFontSize`.
  - **Width measurement** `ColumnWidthCalculator.cs` (constructed in `ColumnBuilder.cs:27`, takes `gridStyle`): `MeasureText` (`:198-215`) builds a `Typeface` with hard-coded `FontFamily.Default` and a fixed weight — `FontWeight.Normal` for content (`:133`), `FontWeight.Bold` for headers (`:159`), never italic. This MUST be threaded with the configured family/weight/italic too, or measured widths diverge from rendered widths (clipping / mis-sized columns).
  - `ComboBoxCellFactory.cs:113-114` comment notes the Fluent ComboBox does NOT inherit the grid font — it is set directly in the factory; the new properties go there the same way (not via cascade).
- Palette installer: `CellPaletteInstaller.cs` (font keys `StatusBarFontSizeKey :39`, `StatusBarTimerFontSizeKey :40`; install `:94-95` as `double`). Grid fonts have NO resource keys (code-assigned).
- Model: `GridStyleOptions.cs` (record + `Default`); font sizes are `int`. DTO: `GridStyleFontsDto` (`header_size`,`cell_size`) and `StatusBarStyleDto` (`font_size`,`timer_font_size`). Mappers: `GridStyleMapper` (load), `GridStyleDtoMapper` (save).
- Editor: `GridStyleEditorViewModel.cs` (font-size props `:77-78,:86-87`, ranges `MinFontSize=6`/`MaxFontSize=72` `:24-25`, `Seed`/`BuildRecord`/`NumericsInRange`); `GridStyleEditorWindow.axaml` "Fonts and Layout" card uses `NumericUpDown` and `ColorPicker` — **no ComboBox/enum control exists yet** to model a family/weight picker on; one must be introduced.
- Avalonia `FontFamily`, `FontWeight`, `FontStyle` are inherited attached properties that
  cascade exactly like `TextElement.FontSize` (existing examples: `GridStyleEditorWindow.axaml:28` `FontWeight=SemiBold` style; `ColumnBuilder.cs:67` code `FontWeight.Bold`). So the status-bar cascade approach extends directly, and the grid factories set the extra properties the same way they set size.

## Development Approach
- Testing approach: **Regular** (code first, then tests).
- Mirror the existing flat-field plumbing (the codebase prefers explicit duplication over a
  premature abstraction). Add per-role fields rather than a generic FontSpec record.
- Types: sizes `int`; weight stored as an `int` (100–900) in the record and YAML (stable,
  culture-free, easy to validate); italic `bool`; family `string`. `FontWeight` in Avalonia is
  an `int`-backed **enum** (not a struct), so `(FontWeight)weightInt` is valid for the curated
  members; the editor ComboBox constrains choices so no undefined value is produced. The
  installer/factories convert weight-int → `FontWeight`, italic → `FontStyle.Italic`/`Normal`,
  and family-string → `FontFamily`.
- **Family default is `""` (empty) meaning "theme default"**: consumers must NOT set
  `FontFamily` when the value is empty/whitespace, leaving the Fluent theme's bundled default
  (Inter) in place — this preserves the current appearance and avoids depending on a font that
  is not in `SystemFonts`. A non-empty value sets the family. Do NOT default to "Inter" (no such
  asset exists in the repo and it is not a system font).
- Backward compatibility: every new YAML member is optional/nullable → existing
  `grid_style.yaml` files load unchanged (missing → defaults). Do not edit YAML fixtures.
- Each task completes with passing tests before the next; run `dotnet format` before finishing.
- Labels stay in their current language (russification is #69).

## Testing Strategy
- Unit: extend `GridStyleMapperTests` (round-trip all new fields, defaults on omission, and the
  options→DTO save direction with non-default values), `CellPaletteInstallerTests` (new
  resource keys + types + count), `GridStyleEditorViewModelTests` (seed/build/range for the new
  numeric props; weight/family/italic round-trip).
- Grid factories: assert the factories read weight/family/italic from `GridStyleOptions` (where
  a headless control test is feasible, mirroring existing factory tests; otherwise a focused
  unit check).
- A screenshot-harness pass (gitignored `SemiStep.Screenshots`) renders the status bar and
  dumps each TextBlock's effective font to confirm label vs value diverge — verification only,
  not a committed gate.

## Solution Overview
Per-role fields on `GridStyleOptions` (flat), one global family:
- Global: `FontFamily` (string, default `""` = theme default; see Technical Details).
- Grid header: `HeaderFontSize` (exists, 14), `HeaderFontWeight` (int, 700/Bold — preserves the
  current hard-coded bold), `HeaderItalic` (bool, false).
- Grid cell: `CellFontSize` (exists, 12), `CellFontWeight` (400), `CellItalic` (false).
- Status-bar text: `StatusBarFontSize` (exists, 12), `StatusBarFontWeight` (400), `StatusBarItalic` (false).
- Timer label: `StatusBarTimerLabelFontSize` (14), `StatusBarTimerLabelFontWeight` (400), `StatusBarTimerLabelItalic` (false).
- Timer value: `StatusBarTimerValueFontSize` (24), `StatusBarTimerValueFontWeight` (400), `StatusBarTimerValueItalic` (false).
- Remove `StatusBarTimerFontSize` (replaced by the two timer roles).

Consumers:
- Status bar: install resources for family (global) + per status-role weight/style/size; set
  `TextElement.FontFamily/FontWeight/FontStyle/FontSize` via `DynamicResource` — on the root
  Border for the global family + status-text role, and per-TextBlock for the timer label vs
  value (set the attached props directly on the four timer TextBlocks).
- Grid: `ColumnBuilder`/`TextCellFactory`/`ComboBoxCellFactory` set `FontFamily`, `FontWeight`,
  `FontStyle`, `FontSize` from `GridStyleOptions` (replacing the hard-coded header bold).

Editor: a new **"Fonts"** card in `GridStyleEditorWindow.axaml`:
- One global **Font family** `ComboBox` (items from `FontManager.Current.SystemFonts` names).
- A row per role: **[role label] [Size NumericUpDown] [Weight ComboBox] [Italic CheckBox]**.
- VM exposes `AvailableFontFamilies` and `AvailableFontWeights` lists for the ComboBoxes.

## Technical Details
- Weight model: store `int` (100–900). Editor `ComboBox` binds to a curated list
  (Light 300, Normal 400, Medium 500, SemiBold 600, Bold 700, ExtraBold 800, Black 900);
  display the name, value is the int. Validation: must be one of the offered values.
- Family: `string`; if the saved family is not installed, Avalonia falls back automatically —
  keep "Inter" as the default and do not hard-fail on an unknown family (validate non-empty).
- YAML: extend `GridStyleFontsDto` with a global `family` plus per-role `*_weight` / `*_italic`
  and the two timer-size keys; or add a dedicated `fonts:` sub-structure. Decide the exact YAML
  shape in Task 1 (keep aliases snake_case, members nullable).
- Installer resource keys (status roles): `AppFontFamily`, `StatusBarFontWeight`,
  `StatusBarFontStyle`, `StatusBarTimerLabel{FontSize,FontWeight,FontStyle}`,
  `StatusBarTimerValue{FontSize,FontWeight,FontStyle}` — family as `FontFamily`, weight as
  `FontWeight`, style as `FontStyle`, size as `double`.

## What Goes Where
- **Implementation Steps** (`[ ]`): model/DTO/mappers; status-bar consumer + split; grid
  consumers; editor card; verification; finalize.
- **Post-Completion** (no checkboxes): the operator opens the editor, tunes family/size/weight/
  italic per role, restarts, and confirms the look; chooses shipped defaults.

## Implementation Steps

### Task 1: Core model, DTO, mappers — font family + per-role weight/italic + timer split

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleFontsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/StatusBarStyleDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleDtoMapper.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleMapperTests.cs`

- [x] Add to `GridStyleOptions`: global `string FontFamily` (default `""` = theme default); per-role
      `int *FontWeight` (Header 700, Cell/StatusBar/TimerLabel/TimerValue 400) and `bool *Italic`
      (all false); `int StatusBarTimerLabelFontSize` (14) and `int StatusBarTimerValueFontSize`
      (24). Remove `StatusBarTimerFontSize`. Update `Default`.
- [x] Decide and implement the YAML shape (nullable members, snake_case aliases) across
      `GridStyleFontsDto` (global `family`, grid header/cell `*_weight`/`*_italic`) and
      `StatusBarStyleDto` (text `weight`/`italic`; `timer_label_*` and `timer_value_*`). The
      `family` string member uses `[YamlMember(..., ScalarStyle = ScalarStyle.DoubleQuoted)]`
      to match the existing string members in `StatusBarStyleDto`; numeric/bool members stay plain.
- [x] Map every new field in `GridStyleMapper` (load, `?? defaults`) and `GridStyleDtoMapper`
      (save).
- [x] Extend `GridStyleMapperTests`: full round-trip of new fields, omission→defaults, and the
      options→DTO direction with non-default values (guards the save path).
- [x] Run: `dotnet test --filter "FullyQualifiedName~GridStyleMapper"` — must pass.

### Task 2: Status-bar consumer — global family, per-role weight/style, timer label/value split

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`

- [x] Add resource keys + install: `AppFontFamily` (`FontFamily`), `StatusBarFontWeight`
      (`FontWeight`), `StatusBarFontStyle` (`FontStyle`), and the timer label/value
      size(`double`)/weight(`FontWeight`)/style(`FontStyle`) keys. Remove `StatusBarTimerFontSize`
      key. Convert weight-int→`FontWeight`, italic→`FontStyle.Italic`/`Normal`, family→`FontFamily`.
- [x] In `AppStatusBar.axaml`: on the root `Border` set `TextElement.FontFamily`/`FontWeight`/
      `FontStyle`/`FontSize` to the global-family + status-text resources. For the `Grid.Column="4"`
      timer block, set the label TextBlocks (`Шаг:`/`Рецепт:`) to the timer-label resources and the
      value TextBlocks to the timer-value resources (per-TextBlock attached props; remove the old
      block-level `StatusBarTimerFontSize`).
- [x] Extend `CellPaletteInstallerTests`: assert each new key installs with the right value and
      runtime type (`FontFamily`/`FontWeight`/`FontStyle`/`double`); update the resource count.
- [x] Run: `dotnet test --filter "FullyQualifiedName~CellPaletteInstaller"`; build the UI project.

### Task 3: Grid consumer — apply family/weight/italic/size in the cell & column factories

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`
- Modify/Create: a focused test under `SemiStep/SemiStep.Tests/UI/RecipeGrid/`

- [x] In `ColumnBuilder` header template: set `FontFamily`/`FontWeight`/`FontStyle`/`FontSize`
      from `gridStyle` (the hard-coded `FontWeight.Bold` becomes `gridStyle.HeaderFontWeight`).
      Apply the empty-family rule: when `gridStyle.FontFamily` is empty, leave `FontFamily` unset.
- [x] In `ColumnBuilder` numbering column, `TextCellFactory` (all three templates), and
      `ComboBoxCellFactory`: set `FontFamily`/`FontWeight`/`FontStyle`/`FontSize` from the cell-role
      fields of `gridStyle` (same empty-family rule).
- [x] In `ColumnWidthCalculator.MeasureText` (`:198-215`): build the `Typeface` from the
      configured family/weight/italic instead of the hard-coded `FontFamily.Default` +
      `FontWeight.Bold`(`:159`)/`FontWeight.Normal`(`:133`), so measured width matches the rendered
      typeface for both header and content. Empty family → `FontFamily.Default` for measurement.
- [x] Add a focused test asserting a built header/cell carries the configured weight/family/italic
      from a non-default `GridStyleOptions` (mirror existing factory tests; if a headless control
      assertion is impractical for a template, assert via the factory's produced control).
- [x] Run the relevant grid tests (incl. existing `ColumnWidthCalculatorTests`); build the UI project.

### Task 4: Editor — "Fonts" card with family/size/weight/italic per role

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`

- [x] VM: add the global `FontFamily` (string) prop; per-role weight (int) and italic (bool)
      props; the two timer-size props (replace `StatusBarTimerFontSize`). Seed/BuildRecord/
      validation cover all. Size validation reuses `MinFontSize`/`MaxFontSize`.
- [x] VM lists for the pickers, designed so seeded values ALWAYS round-trip even when not in the
      list (protects `Seed_RoundTripsShippedHexValues_Losslessly`): `AvailableFontFamilies` =
      a `""`→"(Default)" sentinel + `FontManager.Current.SystemFonts` names, **plus the
      currently-seeded family prepended if not already present**; `AvailableFontWeights` = the
      curated int list, plus the seeded weight if absent. Bind the family/weight so the underlying
      string/int is preserved regardless of list membership (do not let a `SelectedItem` miss null
      the value).
- [x] Window: add a new **"Fonts"** card. Layout: a global Font-family `ComboBox` row, then a
      header row and one row per role using a multi-column grid
      (`ColumnDefinitions="170,Auto,Auto,Auto"` → `[role label][Size NumericUpDown][Weight ComboBox][Italic CheckBox]`).
      Move the existing font-size `NumericUpDown`s into this card (leave paddings/row-height/status
      padding/spacing/validation-height in the existing "Fonts and Layout" → rename to "Layout").
      `ComboBox`/`CheckBox` render with theme defaults (no new style needed). Watch the
      `RowDefinitions`/`Grid.Row` count (a child past the last row is silently clamped).
- [x] Extend `GridStyleEditorViewModelTests`: seed from a non-default record populates the new
      props; `BuildRecord` carries them; an out-of-range size makes `CanSave` false; a chosen
      weight/family/italic round-trips; AND a family/weight value NOT in the offered lists still
      round-trips through `BuildRecord` (guards the lossless-seed contract).
- [x] Run: `dotnet test --filter "FullyQualifiedName~GridStyleEditorViewModel"`; build the UI.

### Task 5: Verify acceptance criteria

- [x] Run `dotnet test --filter "Component=Config"` and `--filter "Component=UI"`
      (`VSTEST_CONNECTION_TIMEOUT=600`; testhost connect-timeout is environmental — re-run).
- [x] Render via the screenshot harness (`dotnet test SemiStep/SemiStep.Screenshots/SemiStep.Screenshots.csproj --filter "DisplayName~MOCVD"`) and read the dump: confirm the timer **label** and **value** TextBlocks now carry distinct, configured fonts, and grid header/cell reflect the configured weight/family.
- [x] `dotnet format SemiStep/SemiStep.slnx` — no diff. Confirm YAML fixtures untouched (`git diff --stat origin/master...HEAD -- 'SemiStep/SemiStep.Tests/YamlConfigs/**'` empty).
- [x] Update `Docs/architecture/grid-style-configuration.md` to document the new font roles,
      resources, and YAML keys (the doc is the config reference).

### Task 6: Finalize
- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion
*Manual, no checkboxes.*
- Open `View > Grid Style Settings... > Fonts`, set family/size/weight/italic per role, Save,
  restart, and confirm each role renders as configured (especially the now-separate timer label
  vs value). Choose the shipped defaults.
- Optional follow-up (separate change): live apply without restart, and folding the
  MessagePanel/dialog text into the palette.
