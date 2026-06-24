# Column width: min-content (longest-header-word) floor + unified chrome (issue #66 refactor)

## Overview

Smoke testing #66 exposed that excluding the header from the width budget plus a flat `MinColumnWidth = 72`
floor makes a header-heavy column (e.g. "Начальное значение") narrower than its own longest word, so
`TextWrapping=Wrap` breaks the word mid-glyph ("Начально"/"е") instead of wrapping cleanly on the space.
This refactor replaces the accumulated smoke patches with one coherent model:

- **A column's fixed width = `max(contentBudget, headerWordFloor, AbsoluteMin)`.**
- `headerWordFloor` = the width of the header's **longest single word**, measured with the header's
  rendered font (bold, `HeaderFontSize`), plus chrome. This is the CSS `min-content` width — the minimum
  at which a wrapping header breaks on spaces, never inside a word. It replaces the flat 72px floor.
- `contentBudget` = the widest representative cell string (metadata-driven, as today), plus chrome.
- `chrome` = one additive `CellChromeAllowance` consolidating today's separate `*1.15` buffer and `+16`
  padding into a single named concept (cell `Padding(4,2)` + grid line + centered slack).
- All floor-return paths (PropertyField with no `Max`/unresolved, the dispatch default) route through the
  same `max`, so even header-heavy **empty** columns get the header-word floor — the exact defect's root.

Stays metadata-driven (representative values), so widths are stable and computed once — NOT Avalonia's
built-in `DataGridLengthUnitType.Auto`, which sizes to live cells (columns would jump as the user types
and empty columns would collapse). This is the deliberate reason to keep the hand-rolled calculator.

## Context (from discovery)

- `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs` — the width model. Today: `CalculateWidth(content)`
  = `ceil(max(maxContentWidth * BufferMultiplier + CellContentHorizontalPadding, MinColumnWidth))`; header
  excluded; `CalculatePropertyFieldWidth`/`_ =>` return `new DataGridLength(MinColumnWidth)` (floor-only,
  bypassing any header awareness — where the bug lives); `MeasureText(text, fontSize)` hardcodes
  `Typeface(FontFamily.Default)` (normal weight) and is only ever called at `CellFontSize`.
- `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` — builds columns; `_wrappingHeaderTemplate` (static)
  is the per-column `HeaderTemplate` wrapping `TextBlock` (Wrap, MaxLines=2, ellipsis, centered). The
  TextBlock sets NO FontSize/FontWeight, so it renders at the theme `DataGridColumnHeader` default
  (`FontSize=12`) plus the style's `FontWeight=Bold`. `column.MinWidth = MinColumnWidth` on every column.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` — `DataGridColumnHeader` style: Bold, Black, Stretch,
  Padding `6,3`. An ~18-line comment block duplicates the HeaderTemplate rationale already in ColumnBuilder.
- `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml` — `DataGridSortIconMinWidth = 0` override (keep).
- `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` — `HeaderFontSize` (14), `CellFontSize` (12).
  `HeaderFontSize` is currently unused (it fed the deleted header measurement); this refactor uses it again
  for BOTH rendering the header and measuring the longest-word floor, so it stops being dead (drop the
  earlier idea of moving it to #79).
- `SemiStep/SemiStep.UI/RecipeGrid/TimeFormatHelper.cs` — `FormatValue`, time format/units for the
  representative time value.
- `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs` — mirrors the formula; updated here.

### Research grounding

- "Min column width = longest header word" is CSS `min-content` applied to wrapping text (MDN/W3C: a wrapping
  box is "only as wide as the longest word"). It is the correct floor for clean header wrapping.
- Avalonia `DataGridLengthUnitType.{Auto,SizeToCells,SizeToHeader}` exist; `Auto` = `max(header,content)` over
  LIVE cells. Rejected here for stability (representative metadata widths instead).

### Confirmed decisions

- Keep hand-rolled, representative-based widths (stable). Add a header-word (`min-content`) floor measured at
  the rendered header font (bold, `HeaderFontSize`). Consolidate `*1.15` + `+16` into one additive
  `CellChromeAllowance`. Route all floor-returns through the unified `max`. Render the header at
  `HeaderFontSize` bold so the measured floor matches what is drawn.

## Development Approach

- Regular (code first, then tests). The width math is fully unit-testable; the visual wrap is manual smoke.
- One coherent change to the calculator, a small ColumnBuilder/style alignment, then verify.

## Testing Strategy

- **Unit (ColumnWidthCalculator):** longest-word floor raises a tiny-content header-heavy column to its
  longest word (measured bold at HeaderFontSize, NOT cell font); the floor is the longest WORD, not the whole
  header (FullHD safety); content wider than the header is a no-op for the floor; the star column is
  unaffected; chrome is the single additive allowance.
- **Manual smoke:** "Начальное значение" wraps cleanly to "Начальное"/"значение" at the computed width (no
  mid-word break); single-word headers stay one line; everything fits 1920; time value not flush.

## Solution Overview

`ColumnWidthCalculator` gains header awareness: every fixed-width column passes its `UiName` into one sink
that returns `ceil(max(contentBudget, headerWordFloor, AbsoluteMin))`. `MeasureText` gains a typeface/size so
the header floor is measured bold at `HeaderFontSize`. The flat 72 floor demotes to a small `AbsoluteMin`;
the buffer+padding collapse to one `CellChromeAllowance`. `ColumnBuilder`'s header `TextBlock` renders at
`HeaderFontSize` bold so the drawn header matches the measured floor.

## Technical Details

- New/changed in `ColumnWidthCalculator`:
  - `CellChromeAllowance` (additive, ~18) replaces `BufferMultiplier` (1.15) and `CellContentHorizontalPadding`
    (16). ONE constant, sized to cover the LARGER of cell padding (`Thickness(4,2)` = 8px) and header padding
    (`6,3` = 12px) + grid line + a couple px slack, so it correctly budgets BOTH the content and the header
    floors. `contentBudget = maxContentWidth + CellChromeAllowance`.
  - KEEP `MinColumnWidth` at its current 72 as the absolute floor (one of the three `max` terms). Do NOT lower
    it — the numbering column (Auto, built in `ColumnBuilder`) uses it as its `MinWidth` and lowering it would
    narrow that column with nothing compensating. 72 is a reasonable readable minimum; the longest-word floor
    is the new header guard, not a replacement for the absolute min.
  - `MeasureText(string text, double fontSize, FontWeight weight)` — the weight MUST flow into the `Typeface`
    constructor (`new Typeface(FontFamily.Default, FontStyle.Normal, weight)`), NOT into `FormattedText`.
    Header words measured Bold at `gridStyle.HeaderFontSize`; content stays normal weight at `CellFontSize`.
  - `LongestHeaderWordFloor(string header)`: split on whitespace, `Max` of `MeasureText(word, HeaderFontSize,
    Bold)`, `+ CellChromeAllowance`; returns 0 for empty header.
  - `CalculateWidth(IEnumerable<string> contentStrings, string headerText)`: returns
    `ceil(max(contentBudget, LongestHeaderWordFloor(headerText), MinColumnWidth))`. Every dispatch branch
    passes `columnDef.UiName`. The `TextField => Star` branch stays `Star` (no floor; star absorbs). The
    PropertyField fail/null-Max branches pass empty content + the header (so the header floor applies — this
    is the exact "Начальное значение" empty-column defect).
- `ColumnBuilder`:
  - The header `TextBlock` sets `FontSize = gridStyle.HeaderFontSize` and `FontWeight = FontWeight.Bold` so the
    rendered header matches the measured floor (render==measure, and `HeaderFontSize` becomes genuinely used).
    This needs `gridStyle`, so the header template (today `static _wrappingHeaderTemplate`) becomes an INSTANCE
    member; consequently `AddNumberingColumn` (today `static`, references the static template) MUST become an
    instance method too. Keep `Foreground=Black` from the style.
  - `column.MinWidth = MinColumnWidth` (= 72) stays on every column (resize-collapse guard incl. the star
    column); the numbering column keeps `MinWidth = MinColumnWidth` unchanged.
- `DataGridStyles.axaml`: keep `DataGridColumnHeader` Bold/Black/Stretch/Padding; if FontWeight now lives on
  the TextBlock, the style's `FontWeight=Bold` is redundant but harmless — keep one owner, remove the other;
  trim the duplicated ~18-line comment block (the rationale lives in ColumnBuilder).
- `ColorPalette.axaml`: unchanged (`DataGridSortIconMinWidth=0` stays).

## What Goes Where

- **Implementation Steps:** calculator refactor + tests; header render-font alignment + comment dedup; verify.
- **Post-Completion:** manual smoke (clean wrap, 1920 fit, time not flush). The #74 theme pass may revisit
  header typography.

## Implementation Steps

### Task 1: min-content width model in ColumnWidthCalculator

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`

- [x] Replace `BufferMultiplier` + `CellContentHorizontalPadding` with one additive `CellChromeAllowance`
      (~18, named; comment: covers the larger of cell padding 8px and header padding 12px + gridline + slack).
      `contentBudget = maxContentWidth + CellChromeAllowance`.
- [x] KEEP `MinColumnWidth` = 72 (the absolute floor / one `max` term). Do NOT lower it (numbering column depends on it).
- [x] Overload `MeasureText(text, fontSize, FontWeight weight)` — pass `weight` into the `Typeface` ctor
      (`new Typeface(FontFamily.Default, FontStyle.Normal, weight)`), NOT into `FormattedText`. Content path
      stays normal weight at `CellFontSize`; header path Bold at `gridStyle.HeaderFontSize`.
- [x] Add `LongestHeaderWordFloor(string header)` — split on whitespace, max word measured Bold at
      `HeaderFontSize`, `+ CellChromeAllowance`; 0 for empty header.
- [x] Change `CalculateWidth` to `(IEnumerable<string> contentStrings, string headerText)` returning
      `ceil(max(contentBudget, LongestHeaderWordFloor(headerText), MinColumnWidth))`. Route EVERY fixed-width
      branch (action/group/property/time and the PropertyField fail/null-Max and `_ =>` paths) through it with
      `columnDef.UiName`. Keep `TextField => Star` as `Star`.
- [x] Update/extend tests (name each):
      - NEW `MultiWordHeader_FloorsAtLongestWord`: tiny-content column with `UiName="Начальное значение"` →
        width ≥ longest-word floor; clean no-mid-word guarantee.
      - NEW `HeaderFloorMeasuredBoldAtHeaderFontSize`: same word measured Bold@HeaderFontSize > Normal@CellFontSize.
      - NEW `LongestWordFloor_FarBelowWholeHeaderWidth` (FullHD safety): floor << whole-header single-line width.
      - NEW `StarColumn_UnaffectedByHeaderFloor`.
      - UPDATE `PropertyField_WithMax_SizesFromMaxRepresentative` and `ActionColumn_NoSortIconReservation`:
        expected formula `ceil(max(content + CellChromeAllowance, headerWordFloor, MinColumnWidth))` (was
        `content*1.15+16`); update the mirrored test consts.
      - UPDATE `PropertyField_StringTyped_...`: re-derive BOTH sides — the `fullLengthWidth` upper bound used
        `content*1.15` (no +16); switch to the additive formula consistently so the `<` relationship still holds.
      - UPDATE `TinyContent_FallsBackToMinColumnWidthFloor` and `PropertyField_PropertyResolveFails_FallsBackToFloor`:
        these asserted `== MinColumnWidth`; now width = `max(headerWordFloor, MinColumnWidth)` for that column's
        UiName — assert the correct one (flips to the header-word floor if that header's longest word exceeds 72).
      - UPDATE `LongHeader_DoesNotInflateColumnBeyondContent`: re-scope to "does not exceed the longest-word
        floor" AND rewrite the stale reason string ("header must not participate" is no longer true).
      Run the tests — must pass before next task.

### Task 2: render header at HeaderFontSize bold; dedup comment

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`

- [x] In `ColumnBuilder`, set the header `TextBlock`'s `FontSize = gridStyle.HeaderFontSize` and
      `FontWeight = FontWeight.Bold` so the drawn header matches the floor measurement. Convert
      `_wrappingHeaderTemplate` to an INSTANCE member (needs `gridStyle`); consequently convert
      `AddNumberingColumn` from `static` to an instance method (it references the template) — verify it still
      compiles and the numbering column still builds.
- [x] In `DataGridStyles.axaml`, keep `Foreground=Black`, `HorizontalContentAlignment=Stretch`, padding; pick a
      single FontWeight owner (TextBlock vs style) and drop the redundant one; trim the duplicated header
      rationale comment block (keep the one in ColumnBuilder).
- [x] Build the UI project (0 errors). Visual — manual smoke. Must build clean.

### Task 3: Verify acceptance criteria

- [x] Trace: longest-word floor applies on all fixed-width branches incl. empty PropertyField; measured bold at
      HeaderFontSize; chrome is one additive constant; star unaffected; HeaderFontSize now used (render+measure).
      Verified: dispatch default `_ => CalculateWidth([], UiName)` (CWC.cs:41); PropertyField GetProperty-fail (74)
      and null-Max (89) both route empty content + header through `CalculateWidth`; floor via
      `LongestHeaderWordFloor` inside `Math.Max` (117); `MeasureText(word, HeaderFontSize, FontWeight.Bold)` (137)
      with weight in `Typeface` ctor (183); `CellChromeAllowance = 18` additive only (30/115/144); TextField stays
      Star (40); ColumnBuilder TextBlock FontSize=HeaderFontSize + Bold (ColumnBuilder.cs:67-68), render==measure.
- [x] Build all: `dotnet build SemiStep/SemiStep.slnx`. Result: 0 errors, 12 pre-existing NU1902 NCalc warnings.
- [x] Run UI + Core slices. UI: 277 passed, 0 failed. Core: 235 passed, 0 failed. The known ~195 spurious
      single-process UI failure signature did NOT appear, so no narrow re-runs were needed.
- [x] `dotnet format SemiStep/SemiStep.slnx` clean (`--verify-no-changes` exit 0, no changes).

### Task 4: Finalize

- [x] Confirm no dead leftover (old `BufferMultiplier`/`CellContentHorizontalPadding` fully gone; no stale
      comment claiming the flat floor is the header-wrap room).
- [x] Move this plan to `Docs/plans/completed/`. (deferred to end of exec run — kept in place for review phases)

## Post-Completion

**Manual verification:**
- "Начальное значение" wraps cleanly to two lines at the computed width with no mid-word break; single-word
  headers stay one line; the grid fits 1920 with no horizontal scroll; "00:00:00 с" is not flush.
- Tune `CellChromeAllowance` / `AbsoluteMinColumnWidth` by eye if needed.
- The #74 theme pass may revisit header typography.
