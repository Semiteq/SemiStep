# Font-relative column chrome + ComboBox-chevron-aware width (extends #66)

## Overview

Two coupled fixes to recipe-grid column sizing, both surfaced by smoke-testing PR #82:

1. **ComboBox chevron overlaps the selected text.** Combo columns (action + group) are sized to
   `widestText + CellChromeAllowance(26)`, but the Avalonia Fluent ComboBox reserves more chrome
   around its text: a `*,32` template grid (a 32 DIP chevron column) plus `ComboBoxPadding` left 12
   (`Avalonia.Themes.Fluent/Controls/ComboBox.xaml`). 26 < that, so the chevron sits over the text
   ("Set Default Comp⌄", "N2 Nitroge⌄"). Combo columns must budget the real combo chrome.

2. **Hardcoded pixel chrome does not survive font changes.** `CellChromeAllowance = 26` and
   `MinColumnWidth = 72` are absolute DIP, tuned for the current 12/14 fonts. They survive monitor
   DPI (Avalonia lays out in device-independent pixels — text measurement and these constants are
   both DIP and co-scale), but NOT a `cell_size`/`header_size` change in `grid_style.yaml`: a larger
   font shrinks the reserve proportionally and the flush/overlap returns. Make the chrome
   font-proportional so it tracks the font; the combo chevron stays a fixed theme DIP.

The time-column logic is NOT touched: it already computes identically across configs and re-derives
consistently under the new model.

## Context (from discovery)

- Width model: `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`, shape
  `max(content, longest-header-word, MinColumnWidth) + chrome`. It has `GridStyleOptions gridStyle`
  injected (`CellFontSize=12`, `HeaderFontSize=14`).
- `MinColumnWidth` is a `public const int` consumed by `ColumnBuilder.cs` (lines 50, 88: numbering
  column `MinWidth` + every column's `MinWidth`) and referenced statically in the tests in ~9 places.
- Combo columns route through `CalculateActionColumnWidth` / `CalculateGroupColumnWidth` →
  `CalculateWidth`. Text/property/time route through `CalculateWidth` / `CalculatePropertyFieldWidth`
  / `CalculateTimeColumnWidth`.
- **Known inconsistency (from plan review):** `TextCellFactory.cs` renders every content cell
  `TextBlock` with a hardcoded `Padding = new Thickness(4, 2)` (lines 52, 77, 120) — it does NOT read
  `gridStyle.CellPaddingLeft/Right` (6). So config cell padding does not flow into content-cell
  layout. The width chrome is therefore expressed as a font multiple (covering the rendered padding +
  reserve together), NOT as `configPadding + reserve` — the latter would be a false story. The header
  path is consistent (header style padding `6,3` is rendered). The `Thickness(4,2)` hardcode is left
  as-is (out of scope); the plan only documents it.
- Combo cells: `ComboBoxCellFactory.cs`; the `DataGrid ComboBox` style in
  `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` sets Background/BorderThickness and can trim
  `Padding`.
- Fluent ComboBox chrome (verified from the theme source): grid `ColumnDefinitions="*,32"`, chevron
  column 32 DIP, glyph 12 DIP centered (≈10 DIP whitespace each side — the off-border margin),
  `ComboBoxPadding 12,5,0,7`, `ComboBoxMinHeight 32`. The `32` is a template LITERAL, not a themeable
  resource: a combo column cannot be narrower than `text + ~32` without re-templating.
- Tests: `SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs` mirror the production chrome
  constant and re-derive expected widths from `GridStyleOptions.Default` (used live).

## Development Approach

- **testing approach**: Regular (code first, then update/extend tests in the same compile unit).
- Keep the calculator a pure function (no live-control construction). `dotnet format` clean after
  each task. Each task leaves the build AND tests green — see the sequencing note below.

**Sequencing note (from plan review):** removing the two `const`s breaks `ColumnBuilder` and the test
project until they are rewired. So the calculator change, the `ColumnBuilder` wiring, and the existing
test re-derivation are ONE compile unit (Task 1) — the green-bar gate applies at the end of Task 1,
not between its sub-steps. Later tasks (combo style, new tests, doc) each independently leave green.

## Testing Strategy

- Unit (`ColumnWidthCalculatorTests`): existing assertions re-derived against the font-proportional
  chrome; PLUS two NEW tests written as assertions on RELATIONS between calculator outputs (not
  re-evaluations of the mirrored formula, which would be tautological):
  - font-relativity: the SAME column built at a larger `CellFontSize`/`HeaderFontSize` is strictly
    wider, and the width delta tracks the font increase — compared between two real calculator
    outputs.
  - combo-chevron: on identical content, the combo-path output exceeds the text-path output by ≈ the
    chevron budget — an output delta inequality.
- The ComboBox padding trim (XAML) and "fits 1920" / "chevron visually clears" are visual — manual
  smoke (Post-Completion), gating before merge. No automated assertion.

## Solution Overview

- **Content chrome** (text/property/time): `Ceiling(CellFontSize × ChromeFontMultiple)` with
  `ChromeFontMultiple = 2.0` → 24 at the default 12. This is a calibration: ≈ the prior 26 px at the
  default font, now font-proportional. Honest reserve note: the rendered cell padding is the fixed
  `Thickness(4,2)` (8 DIP total), so the on-screen reserve ≈ `chrome − 8` ≈ 8 px/side at default (was
  ~9). The 1 px/side shrink must be confirmed non-flush in smoke.
- **Header-word-floor chrome**: `Ceiling(HeaderFontSize × ChromeFontMultiple)` → 28 at 14 (header
  renders style padding `6,3`, consistent).
- **Combo chrome**: `ComboBoxChromeWidth = 38` — a named constant citing the Fluent template
  (chevron column 32 + trimmed ComboBox left padding 6). Fixed theme DIP, NOT font-scaled: the
  chevron column is a fixed template literal, so budgeting `text + 38` keeps the text column
  (`comboWidth − 32`) ≥ `text + 6` at any font — font-robust for overlap without scaling. Trim the
  in-grid ComboBox left padding to 6 via the `DataGrid ComboBox` style so the rendered inset matches
  the budget and combo text aligns with text-cell content. Net combo width ≈ `text + 38` vs the old
  `text + 26` (≈ +12/combo).
- **MinColumnWidth**: `Ceiling(CellFontSize × MinColumnWidthEms)` with `MinColumnWidthEms = 6.0` → 72
  at 12, exposed as a public instance property; `ColumnBuilder` reads it from the calculator. Honest
  comment: a calibration — the floor holds ~a few characters at the cell font, sized to 72 at the
  default font; the `6.0` is chosen to reproduce 72, not physically derived.

The combo chevron column (`32`) is a template literal, so the +12/combo widening is unavoidable
without re-templating. On the densest config (RIE, ~10 combo columns) this could pressure the 1920
budget — quantified in Task 4 and gated by smoke, with a documented re-template fallback.

## Technical Details

- New members in `ColumnWidthCalculator` (instance, reading `gridStyle`): `ContentChrome`,
  `HeaderFloorChrome`, `MinColumnWidth` (public). New consts: `ChromeFontMultiple = 2.0`,
  `MinColumnWidthEms = 6.0`, `ComboBoxChromeWidth = 38` (comment cites the Fluent template).
  `MaxStringSampleLength` (12) stays a content cap, unrelated to chrome.
- `CalculateWidth` keeps `max(contentBudget, headerWordFloor, MinColumnWidth)`; add a chrome
  parameter (default `ContentChrome`) so combo paths pass `ComboBoxChromeWidth`.
- `ColumnBuilder` reads `_widthCalculator.MinColumnWidth` at the numbering and per-column `MinWidth`
  sites.
- `DataGridStyles.axaml`: `DataGrid ComboBox` style trims left padding to 6.
- Tests: replace the mirrored chrome constant with helpers mirroring `ContentChrome` /
  `HeaderFloorChrome` / `ComboBoxChromeWidth` from `GridStyleOptions.Default`, and compute the
  `MinColumnWidth` floor from `Default` (the static `ColumnWidthCalculator.MinColumnWidth` references
  become the instance value or the mirrored floor).

## What Goes Where

- **Implementation Steps**: calculator + builder + test re-derivation (one compile unit), combo style
  trim, the two relational tests, acceptance + RIE-budget quantification, architecture doc.
- **Post-Completion**: manual smoke (MOCVD combo no-overlap; RIE fits 1920; time not flush) and the
  documented re-template fallback if RIE overflows.

## Implementation Steps

### Task 1: Font-relative + combo-aware chrome in the calculator, wired through builder and existing tests

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnWidthCalculator.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`

This is ONE compile unit: removing the `const`s breaks `ColumnBuilder` and the tests until all three
are updated. Green-bar gate applies at the end of the task.

- [x] Replace `const CellChromeAllowance = 26` with instance `ContentChrome` =
      `(int)Math.Ceiling(gridStyle.CellFontSize * ChromeFontMultiple)` and `HeaderFloorChrome` =
      `(int)Math.Ceiling(gridStyle.HeaderFontSize * ChromeFontMultiple)`; add `const double
      ChromeFontMultiple = 2.0` with a comment: calibrated ≈ the prior 26 px at the default 12 px
      font; rendered cell padding is the fixed `TextCellFactory` `Thickness(4,2)`, so on-screen
      reserve ≈ chrome−8.
- [x] Add `const double ComboBoxChromeWidth = 38` with a comment citing
      `Avalonia.Themes.Fluent/Controls/ComboBox.xaml` (`ColumnDefinitions="*,32"` chevron column +
      trimmed left padding 6). Add a chrome parameter to `CalculateWidth` (default `ContentChrome`);
      `CalculateActionColumnWidth` and `CalculateGroupColumnWidth` pass `ComboBoxChromeWidth`,
      text/property/time stay on `ContentChrome`.
- [x] Use `ContentChrome` in the content budget and `HeaderFloorChrome` in `LongestHeaderWordFloor`.
- [x] Replace `public const int MinColumnWidth = 72` with `public int MinColumnWidth =>
      (int)Math.Ceiling(gridStyle.CellFontSize * MinColumnWidthEms)`; add `const double
      MinColumnWidthEms = 6.0` with an honest "calibration, not derivation" comment.
- [x] `ColumnBuilder` reads `_widthCalculator.MinColumnWidth` at both `MinWidth` sites (numbering +
      per-column) instead of the removed static const.
- [x] Re-derive the tests: replace the mirrored `CellChromeAllowance` with `ContentChrome` /
      `HeaderFloorChrome` / `ComboBoxChromeWidth` helpers from `GridStyleOptions.Default`; resolve the
      static `ColumnWidthCalculator.MinColumnWidth` references to the instance value (or a mirrored
      floor helper). Existing assertions must pass unchanged in intent.
- [x] `dotnet build`; `dotnet test --filter FullyQualifiedName~ColumnWidthCalculatorTests` green;
      `dotnet format --verify-no-changes` clean.

### Task 2: Trim the in-grid ComboBox left padding to match the budget

**Files:**
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml` (the `DataGrid ComboBox` style)
- Modify (only if the style cannot carry it): `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`

No automated test — this is a visual style, covered by the manual smoke in Post-Completion.

- [x] Trim the in-grid ComboBox left padding to ~6 so the rendered left inset matches the
      `ComboBoxChromeWidth` assumption and combo text aligns with text-cell content.
- [x] Verify the dropdown still opens/selects and the transparent-background / borderless styling is
      intact (no regression to the existing `DataGrid ComboBox` style). (visual — covered by manual smoke)
- [x] `dotnet build`; `dotnet format --verify-no-changes` clean.

### Task 3: Add relational guard tests

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/ColumnWidthCalculatorTests.cs`

Both tests assert RELATIONS between real calculator outputs, NOT re-evaluations of the mirrored
formula (which would pass tautologically).

- [x] Font-relativity guard: build one calculator from `GridStyleOptions.Default` and another from a
      `Default with { CellFontSize = 24, HeaderFontSize = 28 }` variant; on the same column assert the
      larger-font width is strictly greater, and the width delta is positive and grows with the font
      (e.g. `widthLarge − widthSmall` ≥ a positive bound derived from the font increase) — compared
      between the two outputs, not against the mirror.
- [x] Combo-chevron guard: on identical content (e.g. the same representative string), assert the
      combo-path column output exceeds the text/property-path output by ≈
      `ComboBoxChromeWidth − ContentChrome` — an output-delta inequality, not equality vs the mirror.
- [x] `dotnet test --filter FullyQualifiedName~ColumnWidthCalculatorTests` green.

### Task 4: Verify acceptance and quantify the RIE 1920 budget

- [x] Quantify headroom: sum the per-column widths the calculator produces over the RIE config and
      compare against 1920, so the +chevron pressure is a number, not a guess. Record it in the plan.
      **Measured (real calculator over the shipped `ConfigFiles/RIE`, default fonts 12/14): RIE
      fixed-column total ≈ 2506 px (numbering 72 + all non-Star columns; the Star "comment" column
      absorbs the remainder), headroom vs 1920 ≈ −586 px.** The fixed columns alone OVERFLOW 1920 by
      ~586 px, so the Star comment column has zero remainder and the grid horizontal-scrolls at 1920.
      This is a gating overflow — the documented re-template fallback (Post-Completion) applies; the
      orchestrator/user decides. Not fixed here per the task constraint.
- [x] `dotnet build SemiStep/SemiStep.slnx` — 0 errors.
- [x] Full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` green (883 passed, 0
      failed; no spurious RxApp/Avalonia-init block observed this run).
- [x] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` clean.
- [x] No absolute magic chrome constant remains: chrome traces to a font multiple (`ContentChrome` =
      `ChromeFontMultiple × CellFontSize`, `HeaderFloorChrome` = `ChromeFontMultiple × HeaderFontSize`),
      the cited Fluent template constant (`ComboBoxChromeWidth = 38`), or the labeled
      `MinColumnWidthEms = 6.0` calibration. No bare 26/72 absolute remains in `ColumnWidthCalculator`.

### Task 5: [Final] Update architecture doc and move the plan

**Files:**
- Modify: `Docs/architecture/recipe-grid-column-sizing.md`

- [x] Update the chrome section: chrome is now font-proportional (`ChromeFontMultiple × fontSize`);
      document DPI-robust (DIP model) vs font-robust (now derived) and the ComboBox chevron budget
      with the Fluent template citation; note the `TextCellFactory` `Thickness(4,2)` hardcode as a
      known inconsistency (config cell padding is not applied to content cells).
- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion

**Manual smoke (operator, gating before merge):**
- MOCVD: combo cells ("Имя команды", "Канал", "Режим") show the selected text in full with the
  chevron clear of it — no overlap, no mid-text ellipsis from the chevron.
- RIE (densest): all columns still fit 1920 with no horizontal scroll after the +chevron widening.
- Time columns ("Время"/"Время абс.") still not flush against the border (chrome calibrated ≈ prior).

**Fallback if RIE overflows 1920:**
- The chevron column (`32`) is a literal in the Fluent ComboBox template, so the only way to narrow a
  combo column below `text + ~32` is a compact in-grid ComboBox `ControlTemplate` with a narrower
  chevron column (e.g. `*,20`). Heavier and must preserve dropdown/selection/theming. **If applied,
  re-sync `ComboBoxChromeWidth` in the calculator to the new column width** (template literal and
  budget constant must not diverge, or the overlap returns inverted). Implement only if the smoke
  shows RIE overflow.
