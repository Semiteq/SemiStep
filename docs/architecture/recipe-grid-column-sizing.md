# Recipe Grid Column Sizing (issue #66)

> Scope: the canonical (rows-as-steps) `DataGrid`, plus the transposed step column width at the end.
> The transposed view uses one uniform step-column width and `layout.row_height` cell heights.

## Overview

The recipe `DataGrid` must fit a FullHD (1920px) screen without horizontal scroll, keep headers
readable (12 px SemiBold `#6C707E`, one line that ends in an ellipsis, the full text in a tooltip),
and size each column to its real content.
Avalonia's built-in `Auto` sizing measures only the live cells on screen, so it jitters as rows
scroll in and out. Instead `ColumnWidthCalculator` computes a stable pixel width per column once,
from the configured metadata, and `ColumnBuilder` renders the matching header.

The whole model is **min-content**: a column is as wide as the widest thing that must fit, never
wider. Three independent floors are combined with `max`, then a font-proportional chrome allowance
is added (a larger combo allowance for combo columns).

## The three floors (`ColumnWidthCalculator.CalculateWidth`)

1. **Content floor** — the widest representative cell value for the column (see per-column-type
   sections below), measured in the cell font at normal weight.
2. **Longest-header-word floor** (`LongestHeaderWordFloor`) — the CSS `min-content` principle: a
   wrapping text box is only as narrow as its longest single word, because a word never breaks
   mid-glyph. So the floor is the width of the longest word in the header, measured in the header
   font (`HeaderFontWeight`, `HeaderFontSize`). The header is one line, so a header longer than its
   column trims with an ellipsis; the floor keeps at least its longest word whole, and the tooltip
   always holds the full text, so no trim detection is needed.
3. **Absolute minimum** — `MinColumnWidth` = `ceil(CellFontSize × MinColumnWidthEms)`,
   `MinColumnWidthEms = 6.0` (72 at the default 12px cell font). Exposed as a public instance
   property; it floors the fixed numbering column width (`NumberingColumnWidth`, a run glyph and a
   four-digit step number, so a run start moves no column) and `ColumnBuilder` reads it as the Star
   comment column's absorb-floor, so it must not be lowered. The `6.0` is a calibration chosen to
   reproduce 72 at the default font, not a physically derived em count.

`Star`/`TextField` columns bypass all of this (`DataGridLength.Star`); they absorb the leftover
width and are never floored by header words.

## Render == measure coupling

The header floor is only correct if the header is **drawn** exactly as it was **measured**.
`ColumnBuilder.BuildHeaderTemplate` renders the header `TextBlock` at `HeaderFontWeight` and
`HeaderFontSize` — the same font and weight `LongestHeaderWordFloor` measures with. Change one
side and the other must follow, or headers trim differently than the width budgeted for.

The same coupling applies to cell content. `CalculateWidth` measures every representative at
`CellFontSize`, so every cell control must **render** at `CellFontSize` too. Avalonia does not
inherit it for us: a `DataGrid.FontSize` does not reach the Fluent ComboBox selection-box
`ContentControl`, which otherwise shows the theme default (~14) and clips the longest action name
behind the chevron. So each cell control sets `FontSize = CellFontSize` explicitly, mirroring the
header: `ComboBoxCellFactory` on the ComboBox, `TextCellFactory` on the display/editing `TextBlock`
and `TextBox`, and `ColumnBuilder` on the numbering `DataGridTextColumn`. This is also where the
`CellFontSize` config value becomes the real rendered cell font, not just a measurement input.

Two Avalonia-specific traps make the header template work:

- The template is assigned as the column's `HeaderTemplate`, **not** as a
  `ContentTemplate` setter on the `DataGridColumnHeader` style. The Fluent header binds its
  `ContentTemplate` from `DataGridColumn.HeaderTemplate`, so a style setter is silently overridden.
- `DataGridColumnHeader` must keep `HorizontalContentAlignment="Stretch"`
  (`DataGridStyles.axaml`). The Fluent template sizes the header content panel by this alignment;
  anything other than `Stretch` starves the header `TextBlock` of width, so it never trims at the
  column edge.
- The Fluent theme reserves `DataGridSortIconMinWidth` (default 32px) for a sort glyph on every
  header, even when sorting is disabled. Sorting is off on every column here, so `ColorPalette.axaml`
  overrides that resource to `0`; otherwise the reserved glyph column just crowds the header text.

## PropertyField representative widths

A single visual property column does not map to a single property type. Each `ActionDefinition`
binds its own `ActionPropertyDefinition` (with its own `PropertyTypeId`) to a column key, so the
same column hosts cells of different types depending on the row's action — and the units live on
that per-action type, not on the column. Example (MBE): the `task` column's own
`property_type_id` is `float` (no units), but actions bind it to `arsenic_source_flow` ("см³/мин"),
`chamber_temp` ("°C"), `temp`, `percent` ("%"), and more.

So `CalculatePropertyFieldWidth` collects the representatives of **every** property type any action
binds to the column key, unioned with the column's own default type, and feeds them all to
`CalculateWidth` (which takes the widest). This mirrors `CollectGroupDisplayStrings`, which already
walks all actions for a column key. Measuring only the column default (the prior behaviour) ignored
the units entirely and truncated unit-bearing values like "10 см³/мин".

For each type, `PropertyRepresentatives` yields:

- string types → a capped `'0'` sample (`MaxStringSampleLength` = 12). Production string max_length
  is 64/255; sampling uncapped would let one text column dominate the FullHD budget.
- numeric/time types → **both** `Max` and `Min`, formatted without the unit. Both extents matter
  because for a symmetric range the negative `Min` is wider than `Max` by the minus sign:
  `pressure_pa` (−200000…200000) renders "−200000", wider than "200000". A `time_hms` type formats as
  hh:mm:ss, so `time` (max 86400) renders "24:00:00".

### Unit slot

A number column (a property of `system_type` `int` or `float`) shows the unit in its own slot to the
right of the right-aligned value (`TextCellLayout`). `UnitSlotWidth` is the widest unit any action
binds to the column, unioned with the column's own type, plus `TextCellLayout.UnitGap` (4); a
`time_hms` unit does not count, so a time column has no slot. The column width adds that slot to the
content chrome: value width + slot + chrome. The transposed grid shares one slot width,
`WidestUnitSlotWidth`, because all its step columns share one width.

## Chrome allowance (font-proportional)

`MeasureText` sizes glyphs only — it knows nothing of cell padding, grid lines, or the gap that
keeps text off the border. The chrome allowance covers all of that and is **font-proportional**, so
it tracks a `cell_size`/`header_size` change in `grid_style.yaml` instead of staying a fixed pixel
budget tuned for one font:

- **Content budget** (text/property/time): `ceil(CellFontSize × ChromeFontMultiple)`.
- **Header-word-floor budget**: `ceil(HeaderFontSize × ChromeFontMultiple)`.
- `ChromeFontMultiple = 2.0` — a calibration, ≈ the prior fixed 26px at the default 12px cell font,
  now proportional. The same multiple is added once to both the content floor and the header-word
  floor.

The content budget is sized for the tightest case: a column whose representative equals the rendered
cell exactly, which is the HH:MM:SS time columns ("00:00:00"). Other columns get visual slack for
free because their representative is a range extreme wider than typical cells; the time columns do
not, so they set the floor for the multiple. The header budget must also clear the header padding
(6px per side); the multiple covers both consumers, so do not lower it.

### Combo columns add a fixed chevron budget

Combo columns (action + group) add `ComboBoxChromeWidth = 39` as the chrome term instead of the
content chrome (`maxContentWidth` is still added). This is
a fixed theme constant, **not** font-scaled, because it tracks the Avalonia Fluent ComboBox template
(`Avalonia.Themes.Fluent/Controls/ComboBox.xaml`): a `ColumnDefinitions="*,32"` grid reserves a 32
DIP chevron column, plus the in-grid ComboBox left padding trimmed to 6, plus the 1 px vertical grid
line that `GridLinesVisibility="All"` draws inside the right edge of every cell (32 + 6 + 1 = 39).
The 32 is a template literal, not a themeable resource, so it does not co-scale with the font;
budgeting `text + 39` keeps the text column ≥ `text + 6` at any font, which clears the chevron without
re-templating. The `DataGrid ComboBox` style in `DataGridStyles.axaml` trims the in-grid ComboBox
left padding to 6 so the rendered inset matches this budget and combo text aligns with text-cell
content.

### Loop start names reserve the loop pass pill

While a loop runs, the action cell of its start row shows the loop pass pill between the name and the
chevron slot (see "Loop pass pill" in `recipe-grid-surface.md`). The action column and the transposed
step column measure each action name, and add the widest pill to the name of an action that can show
it: `ActionDefinition.StartsLoop`, the loop start action (`ServiceActionId.ForLoop`, id 20) that
`LoopParser` opens a loop on. The widest of these name displays then gets the usual combo chrome.
Other actions keep their plain name width, so the pill widens a column only when a loop start name
with the pill is wider than every other name.

The widest pill is `LoopBadge.WidestWidth()`: the text "(99/99)" measured in the pill font
(`GridFonts.DefaultFamily`, 12 px SemiBold), plus 8 px padding and 1 px border on each side. The pill
has no gap element: it sits directly before the 32 px chevron slot, which the combo chrome already
reserves. `LoopBadge.Create` sets the same constants on the rendered pill, so measure and render share
one source; `DataGridStyles.axaml` sets only the pill colours. A loop of 100 passes or more shows a
wider pill, and the name trims before it.

### Scaling robustness

- **DPI / display scaling** is handled by Avalonia's device-independent-pixel model: text
  measurement and the chrome are both DIP and co-scale, so monitor DPI does not break the reserve.
- **Font-size changes** (`cell_size`/`header_size`) are now tracked too, because the content and
  header budgets are font-proportional. The combo chevron stays fixed by design (it is a template
  literal).

### Content-cell padding is config-driven, but unknown at measure time

`TextCellFactory` builds each content cell's `Padding` from
`gridStyle.CellPaddingLeft/Top/Right/Bottom`, so config cell padding does flow into content-cell
layout. The chrome is still expressed as a font multiple rather than as `configPadding + reserve`
because `ColumnWidthCalculator` measures column widths before any cell is templated and does not read
the runtime padding at measure time. The font multiple covers the rendered padding plus reserve
together without the calculator needing to know the exact padding values. The header path is
consistent (header style padding `6,3` is both measured and rendered).

### "Fits 1920" is a per-config property

The sizing model does not guarantee a config fits 1920 — that depends on the config. The densest
shipped config (RIE, numbering + 20 non-Star fixed columns) does fit at the default fonts: its
fixed columns sum to under 1920, so the Star comment column still absorbs the remainder and there
is no horizontal scroll (verified by rendering RIE through the screenshot harness). A wider config,
a larger `cell_size`/`header_size`, or simply a smaller window could still make the column sum
exceed the viewport. "Fits 1920" is therefore a property of each config, not a guarantee of the
sizing math.

## Narrow viewport scrolls, never clips

When the viewport is narrower than the column sum, the Avalonia `DataGrid` shrinks every column
toward its `MinWidth` rather than scrolling — and at the default `MinWidth` (the small absolute
floor) the action ComboBox's selected text gets clipped behind its chevron. To prevent that,
`ColumnBuilder` pins each absolute column's `MinWidth` to its **own calculated width**
(`width.IsAbsolute ? width.Value : MinColumnWidth`). A column can then never shrink below the width
its content needs, so a too-narrow window scrolls horizontally and the content stays whole. Only
Star columns keep the small floor, because they are meant to absorb or yield remainder.

## Transposed step column width

`ColumnWidthCalculator.TransposedStepColumnWidth` gives every transposed step column one width, computed
once per surface: the wider of the longest action name plus the name chrome and the widest value plus
`WidestUnitSlotWidth` plus the content chrome, floored at `MinColumnWidth` and capped at 240 px. The name
chrome is what the transposed name display takes: `CellPaddingLeft` + `CellPaddingRight` + the 32 px
chevron slot + `TransposedColumnCellsPresenter.GridLineWidth`, 45 px at the default paddings. A
longer action name trims with an ellipsis and carries its full text as a tooltip. A loop start name
adds the widest loop pass pill, as in the canonical action column (see "Loop start names reserve the
loop pass pill"); the cap still applies to the sum, so past 240 px the name trims before the pill.
