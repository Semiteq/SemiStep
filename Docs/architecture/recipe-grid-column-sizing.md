# Recipe Grid Column Sizing (issue #66)

> Scope: the canonical (rows-as-steps) `DataGrid` only. The transposed view uses one fixed
> uniform step-column width and `layout.row_height` cell heights — see `recipe-grid-surface.md`.

## Overview

The recipe `DataGrid` must fit a FullHD (1920px) screen without horizontal scroll, keep headers
readable (bold, black, wrapping to two lines when long), and size each column to its real content.
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
   font (bold, `HeaderFontSize`). A column narrower than this would force an ugly mid-word break;
   wider lets the header wrap cleanly on spaces to at most two lines.
3. **Absolute minimum** — `MinColumnWidth` = `ceil(CellFontSize × MinColumnWidthEms)`,
   `MinColumnWidthEms = 6.0` (72 at the default 12px cell font). Exposed as a public instance
   property; `ColumnBuilder` reads it as the numbering column's `MinWidth` and as the Star comment
   column's absorb-floor, so it must not be lowered. The `6.0` is a calibration chosen to reproduce
   72 at the default font, not a physically derived em count.

`Star`/`TextField` columns bypass all of this (`DataGridLength.Star`); they absorb the leftover
width and are never floored by header words.

## Render == measure coupling

The header floor is only correct if the header is **drawn** exactly as it was **measured**.
`ColumnBuilder.BuildWrappingHeaderTemplate` renders the header `TextBlock` bold at
`HeaderFontSize` — the same font and weight `LongestHeaderWordFloor` measures with. Change one
side and the other must follow, or headers wrap differently than the width budgeted for.

The same coupling applies to cell content. `CalculateWidth` measures every representative at
`CellFontSize`, so every cell control must **render** at `CellFontSize` too. Avalonia does not
inherit it for us: a `DataGrid.FontSize` does not reach the Fluent ComboBox selection-box
`ContentControl`, which otherwise shows the theme default (~14) and clips the longest action name
behind the chevron. So each cell control sets `FontSize = CellFontSize` explicitly, mirroring the
header: `ComboBoxCellFactory` on the ComboBox, `TextCellFactory` on the display/editing `TextBlock`
and `TextBox`, and `ColumnBuilder` on the numbering `DataGridTextColumn`. This is also where the
`CellFontSize` config value becomes the real rendered cell font, not just a measurement input.

Two Avalonia-specific traps make the header wrap work:

- The wrapping template is assigned as the column's `HeaderTemplate`, **not** as a
  `ContentTemplate` setter on the `DataGridColumnHeader` style. The Fluent header binds its
  `ContentTemplate` from `DataGridColumn.HeaderTemplate`, so a style setter is silently overridden
  and the header would not wrap.
- `DataGridColumnHeader` must keep `HorizontalContentAlignment="Stretch"`
  (`DataGridStyles.axaml`). The Fluent template sizes the header content panel by this alignment;
  anything other than `Stretch` starves the wrapping `TextBlock` of width, so it never wraps.
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
- numeric/time types → **both** `Max` and `Min`, each formatted with the type's units. Both extents
  matter because for a symmetric range the negative `Min` is wider than `Max` by the minus sign:
  `pressure_pa` (−200000…200000) renders "−200000 Па", wider than "200000 Па". Time formats via
  HMS, so `time` (max 86400) renders "24:00:00 с".

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
cell exactly, which is the HH:MM:SS time columns ("00:00:00 с"). Other columns get visual slack for
free because their representative is a range extreme wider than typical cells; the time columns do
not, so they set the floor for the multiple. The header budget must also clear the header padding
(6px per side); the multiple covers both consumers, so do not lower it.

### Combo columns add a fixed chevron budget

Combo columns (action + group) add `ComboBoxChromeWidth = 38` as the chrome term instead of the
content chrome (`maxContentWidth` is still added). This is
a fixed theme constant, **not** font-scaled, because it tracks the Avalonia Fluent ComboBox template
(`Avalonia.Themes.Fluent/Controls/ComboBox.xaml`): a `ColumnDefinitions="*,32"` grid reserves a 32
DIP chevron column, plus the in-grid ComboBox left padding trimmed to 6 (32 + 6 = 38). The 32 is a
template literal, not a themeable resource, so it does not co-scale with the font; budgeting
`text + 38` keeps the text column ≥ `text + 6` at any font, which clears the chevron without
re-templating. The `DataGrid ComboBox` style in `DataGridStyles.axaml` trims the in-grid ComboBox
left padding to 6 so the rendered inset matches this budget and combo text aligns with text-cell
content.

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
