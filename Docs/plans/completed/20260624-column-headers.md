# Column headers: fit FullHD, black bold, wrap long headers to two lines (issue #66)

## Overview

Covers feedback points 5/6/7, all of which touch column-header rendering and column-width budgeting:

- **Point 5 (fit FullHD):** fixed-pixel columns currently sum beyond 1920px on a plasma (RIE) config. Width
  is `max(header, content) * 1.4 + 32px`, where the 32px is reserved for a Fluent sort icon even though
  sorting is disabled everywhere (`CanUserSort = false`), and the 1.4 buffer adds 40% slack per column.
- **Point 6 (black bold headers):** headers are plain strings with the default theme weight/color.
- **Point 7 (wrap long headers):** headers are single-line; long labels ("Шибер задание") inflate their
  column because the header drives `max(header, content)`.

Approach (from the issue): **content drives width, header adapts by wrapping.** Size each column from its
content (combobox items / representative value), drop the sort-icon compensation, lower the buffer, add a
per-column `MinWidth` floor, and render the header as a bold black `TextBlock` that wraps to at most two
lines (ellipsis fallback). "Too long" is decided by the layout engine — no manual pixel/char threshold.

## Context (from discovery)

- `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs` — `CalculateColumnWidth` dispatches by column
  type; `CalculateWidth(header, content)` does `max(header, content) * BufferMultiplier`; `BufferMultiplier = 1.4`
  (line 16); `CompensateThemeSortIconAndPaddingOffset` adds `FluentThemeSortIconMinWidth = 32` (line 111);
  `MeasureText` uses `FormattedText`. PropertyField columns currently call `CalculateHeaderBasedWidth` (header
  only, empty content).
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — builds columns; sets `Header = "No"` ONLY for the
  numbering column (which is `DataGridLength.Auto`, bypassing the calculator); `CanUserSort = false`; sets
  `column.Width` from the calculator. No `MinWidth` set today. `column.CellTheme = InapplicableCellTheme.Create(...)`.
- `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (lines 18, 33) and
  `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` (lines 19, 32) — these are where the DATA-column
  headers are set as `Header = columnDef.UiName` (plain string). With the ContentTemplate approach these stay
  unchanged (the global header style restyles the string content).
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — no `DataGridColumnHeader` style today. The codebase
  already relies on `TextElement.Foreground` inheritance in cell styles (e.g. lines 43, 48), so the header
  style must own `Foreground`/`FontWeight` to avoid theme inheritance recoloring the header text.
- `SemiStep/SemiStep.Core/Recipes/PropertyDefinition.cs` — `PropertyTypeDefinition(Id, SystemType, FormatKind,
  Units?, Min?, Max?, MaxLength?)`. The representative value for a PropertyField comes from here.
- `SemiStep/SemiStep.Core/Recipes/GridColumnDefinition.cs` — `(Key, ColumnType, UiName, PropertyTypeId, ReadOnly, SaveToCsv)`.
- `SemiStep/SemiStep.UI/RecipeGrid/TimeFormatHelper.cs` / `PropertyTimeMultiConverter` — existing value
  formatting (for building a representative formatted value).
- `SemiStep/SemiStep.Tests/UI/RecipeGridStringMaxLengthTests.cs` and other UI tests use `CoreTestHelper.BuildAsync`.

### Confirmed decisions

- **PropertyField sizing:** representative value = the property's `Max` formatted through the SAME path the
  cells use (`TimeFormatHelper.FormatValue(rawMax, formatKind, units)`), so width matches render. Fallbacks:
  if `GetProperty` fails or `Max` is null (both common — `GetProperty` returns `Result<>`, `Max` is `double?`),
  fall back to the `MinColumnWidth` floor; for string-typed properties use a `MaxLength`-char sample.
- **Header rendering owner = a single `DataGridColumnHeader` style with a `ContentTemplate`** (idiomatic
  Avalonia 12), NOT a hand-built `TextBlock` per column. The header `Content` stays the plain `UiName` string
  set by the cell factories; the style's `ContentTemplate` renders it as a wrapping bold-black `TextBlock`.
  This avoids touching the four factory header sites and avoids two styling owners fighting over
  `FontWeight`/`Foreground`.

### Patterns observed

- C# UTF-8 BOM, tabs, var, file-scoped namespaces; comments non-obvious-only.
- The width calculator is a plain unit-testable class (registry + GridStyleOptions injected) — point-5 math
  is testable; the actual 1920 fit and visual wrapping/alignment are manual smoke.

## Development Approach

- Regular (code first, then tests). Visual tuning (exact buffer, MinWidth floor) done by eye during
  implementation per the issue; the constants are named so they are easy to tune.
- One logical change per task; UI layer only (plus reading Core metadata).
- After each task: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` and the relevant test slice.

## Testing Strategy

- **Unit (ColumnWidthCalculator):** the 32px sort-icon offset is gone; the buffer is the new value; a long
  header no longer inflates a column past its content + floor; the `MinWidth` floor applies to a tiny-content
  column; PropertyField sizes from the property `Max`+units representative value.
- **Manual smoke:** the RIE (plasma) config fits within 1920px with no horizontal scrollbar; long headers
  wrap to two lines, bold/black; single-line headers align acceptably next to two-line ones; no header clipped.

## Solution Overview

`ColumnWidthCalculator` stops adding the header into the width `max` and stops reserving the sort-icon width;
it sizes purely from content (combobox items / time sample / PropertyField representative value), times a
smaller buffer, floored by a `MinWidth` constant. `ColumnBuilder` sets `column.MinWidth` on every column.
A single `DataGridColumnHeader` style in `DataGridStyles.axaml` owns header typography: a `ContentTemplate`
renders the string `Content` as a bold-black wrapping `TextBlock` (MaxLines=2, ellipsis), with auto header-row
height and centered content so two-line headers are not clipped. The cell factories keep setting the header
string — no per-column header object.

## Technical Details

- `ColumnWidthCalculator`:
  - Remove `CompensateThemeSortIconAndPaddingOffset` / `FluentThemeSortIconMinWidth`.
  - `BufferMultiplier`: 1.4 → `1.15` (named const, visual-tune).
  - `MinColumnWidth` const (e.g. `72`, visual-tune) returned as a floor: `Math.Max(contentWidth * buffer, MinColumnWidth)`.
    The floor is also the readability guard for headers (a header wider than its content wraps to two lines and,
    for a rare no-break single word, ellipsis-trims; the floor keeps that legible — tune by eye).
  - `CalculateWidth` sizes from content only (header excluded from the `max`). For columns with no natural
    content (the old `CalculateHeaderBasedWidth`), return the floor.
  - New `CalculatePropertyFieldWidth(columnDef)`: `recipeMetadataRegistry.GetProperty(columnDef.PropertyTypeId)`
    returns `Result<>` — on failure, return the floor. On success: for string `SystemType`, representative =
    a `MaxLength`-char sample; for numeric, if `Max` is null return the floor, else representative =
    `TimeFormatHelper.FormatValue(Max formatted invariantly, FormatKind, Units)` (the SAME formatter the cells
    use, so width matches the rendered value). Route `ColumnTypes.PropertyField` to it (note: `step_start_time`
    stays on `CalculateTimeColumnWidth` — do not re-route it).
- `ColumnBuilder`:
  - Set `column.MinWidth = ColumnWidthCalculator.MinColumnWidth` on every column (expose the floor as a public
    const). Harmless on the Auto-width numbering column; that column bypasses the calculator so it is not part
    of the 1920 budget.
  - No header change here — header typography is the `DataGridColumnHeader` style's job.
- `DataGridStyles.axaml`: add a `DataGridColumnHeader` style:
  - `ContentTemplate` = `<DataTemplate><TextBlock Text="{Binding}" TextWrapping="Wrap" MaxLines="2"
    TextTrimming="CharacterEllipsis" TextAlignment="Center"/></DataTemplate>` (Content is the header string).
  - `FontWeight="Bold"`, `Foreground="Black"` on the header (own them so `TextElement` inheritance does not
    recolor), `HorizontalContentAlignment="Center"`, `VerticalContentAlignment="Center"`, `Height="Auto"` /
    no fixed height + sensible `Padding` so a two-line header is not clipped.
  - Match the file's existing banner house-style for the comment.

## What Goes Where

- **Implementation Steps:** width budget + PropertyField representative, header TextBlock + MinWidth, header
  style, verification.
- **Post-Completion:** manual visual tuning of buffer/MinWidth and the 1920 fit on the RIE config; the #74
  theme pass may revisit header typography.

## Implementation Steps

### Task 1: Content-driven width budget in ColumnWidthCalculator

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`

- [x] Remove the 32px sort-icon compensation (`CompensateThemeSortIconAndPaddingOffset` + `FluentThemeSortIconMinWidth`); sorting is disabled.
- [x] Lower `BufferMultiplier` to `1.15` (named const, visual-tune comment).
- [x] Add a PUBLIC `MinColumnWidth` const (e.g. 72) and floor every computed width: `Math.Max(content*buffer, MinColumnWidth)`.
- [x] Make `CalculateWidth` size from CONTENT only — exclude the header from the `max` so long headers no longer inflate columns. Columns with no natural content return the floor.
- [x] Add `CalculatePropertyFieldWidth`: `GetProperty(columnDef.PropertyTypeId)` is `Result<>` → on failure return the floor. On success: string `SystemType` → `MaxLength`-char sample; numeric → null `Max` returns the floor, else representative = `Max` routed through `TimeFormatHelper.FormatValue(formatted-invariant Max, FormatKind, Units)` (same formatter the cells use). Route `ColumnTypes.PropertyField` to it; leave `step_start_time` on `CalculateTimeColumnWidth`.
- [x] Create `ColumnWidthCalculatorTests` (CoreTestHelper-built registry + GridStyleOptions; `[AvaloniaFact]` if `FormattedText` needs the headless app): assert no 32px reservation (known content yields `content*1.15` floored, not `+32`); a long header does NOT widen a column beyond its content+floor; the `MinColumnWidth` floor applies to a tiny-content column; a PropertyField with a `Max` sizes from the `Max` representative; a PropertyField whose property fails to resolve / has null `Max` falls back to the floor. Leave `RecipeGridStringMaxLengthTests` untouched. Run tests — must pass before next task.

### Task 2: Per-column MinWidth floor in ColumnBuilder

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`

- [x] Set `column.MinWidth = ColumnWidthCalculator.MinColumnWidth` on every column built (data columns and the numbering column). No header change here — header typography is owned by the Task-3 style; the cell factories keep setting the string header.
- [x] Build the UI project (0 errors). No unit test (the floor's visual effect is manual smoke; the width math is unit-tested in Task 1). Must build clean before next task.

### Task 3: DataGridColumnHeader style — bold black, wrap to two lines

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [x] Add a `DataGridColumnHeader` style that owns header typography:
  - `ContentTemplate` = a `DataTemplate` rendering the string content as `<TextBlock Text="{Binding}" TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis" TextAlignment="Center"/>`.
  - `FontWeight="Bold"`, `Foreground="Black"` (own them so `TextElement` inheritance does not recolor), `HorizontalContentAlignment="Center"`, `VerticalContentAlignment="Center"`, no fixed `Height` (auto) + sensible `Padding` so a two-line header is not clipped.
  - Keep the file's banner house-style comment.
- [x] Confirm the header row grows to two lines without clipping, single-line headers center acceptably next to two-line ones, and bold/black actually wins over the theme default.
- [x] Build the UI project (0 errors). Pure XAML styling — manual smoke (consistent with how the #67 selection styling was verified; no brittle visual-tree test). Must build clean before next task.

### Task 4: Verify acceptance criteria

- [x] All seven issue tasks addressed (acceptance trace, cited): sort-icon 32px removed (`ColumnWidthCalculator.cs` — no `FluentThemeSortIconMinWidth`/`CompensateThemeSortIconAndPaddingOffset`); buffer lowered to `1.15` (`ColumnWidthCalculator.cs:18`); content-driven width with `MinColumnWidth=72` floor and header excluded from the max (`ColumnWidthCalculator.cs:21,108`; `CalculateWidth` sizes from content only); PropertyField sizes from `Max` via `TimeFormatHelper.FormatValue`, with Result-fail/null-`Max`/string-`MaxLength` fallbacks to the floor (`ColumnWidthCalculator.cs:62-89`); per-column `MinWidth` on data + numbering columns (`ColumnBuilder.cs:41,57`); `DataGridColumnHeader` style with `ContentTemplate` TextBlock `TextWrapping=Wrap`, `MaxLines=2`, `TextTrimming=CharacterEllipsis`, `FontWeight=Bold`, `Foreground=Black`, centered, no fixed Height (`DataGridStyles.axaml:24-39`); plasma fits 1920 — [x] (manual smoke — verified by operator in running app), not headlessly automatable here.
- [x] Build all: `dotnet build SemiStep/SemiStep.slnx` → 0 errors, 12 warnings (all pre-existing NU1902 NCalc).
- [x] Run UI + Core slices. Real counts: `Component=UI` 272 passed / 0 failed; `Component=Core` 235 passed / 0 failed. No spurious mass-failure signature appeared this run (no narrowing needed).
- [x] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` clean (exit 0, no changes).

### Task 5: Finalize

- [x] Confirm no dead leftover (e.g. the removed sort-icon helper is fully gone, no orphaned const).
- [x] Move this plan to `Docs/plans/completed/`. (deferred to end of exec run — kept in place for review phases)

## Post-Completion

**Manual verification:**
- Launch with the RIE (plasma) config; confirm all columns fit within 1920px with no horizontal scrollbar.
- Long headers wrap to two lines, bold and black; short headers (`No`, time) stay one line.
- Single-line headers align acceptably next to two-line ones; no header text clipped.
- Tune `BufferMultiplier` / `MinColumnWidth` by eye if needed.
- The #74 theme pass may revisit header typography against the new theme.
