# Recipe Grid Column Sizing (issue #66)

## Overview

The recipe `DataGrid` must fit a FullHD (1920px) screen without horizontal scroll, keep headers
readable (bold, black, wrapping to two lines when long), and size each column to its real content.
Avalonia's built-in `Auto` sizing measures only the live cells on screen, so it jitters as rows
scroll in and out. Instead `ColumnWidthCalculator` computes a stable pixel width per column once,
from the configured metadata, and `ColumnBuilder` renders the matching header.

The whole model is **min-content**: a column is as wide as the widest thing that must fit, never
wider. Three independent floors are combined with `max`, then a single chrome allowance is added.

## The three floors (`ColumnWidthCalculator.CalculateWidth`)

1. **Content floor** — the widest representative cell value for the column (see per-column-type
   sections below), measured in the cell font at normal weight.
2. **Longest-header-word floor** (`LongestHeaderWordFloor`) — the CSS `min-content` principle: a
   wrapping text box is only as narrow as its longest single word, because a word never breaks
   mid-glyph. So the floor is the width of the longest word in the header, measured in the header
   font (bold, `HeaderFontSize`). A column narrower than this would force an ugly mid-word break;
   wider lets the header wrap cleanly on spaces to at most two lines.
3. **Absolute minimum** — `MinColumnWidth` (72px). Also used by `ColumnBuilder` as the numbering
   column's `MinWidth` and as the Star comment column's absorb-floor, so it must not be lowered.

`Star`/`TextField` columns bypass all of this (`DataGridLength.Star`); they absorb the leftover
width and are never floored by header words.

## Render == measure coupling

The header floor is only correct if the header is **drawn** exactly as it was **measured**.
`ColumnBuilder.BuildWrappingHeaderTemplate` renders the header `TextBlock` bold at
`HeaderFontSize` — the same font and weight `LongestHeaderWordFloor` measures with. Change one
side and the other must follow, or headers wrap differently than the width budgeted for.

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

## CellChromeAllowance

`MeasureText` sizes glyphs only — it knows nothing of cell padding, grid lines, or the gap that
keeps text off the border. `CellChromeAllowance` (26px) is the single additive budget covering all
of that, added once to both the content floor and the header-word floor.

It is sized for the tightest case: a column whose representative equals the rendered cell exactly,
which is the HH:MM:SS time columns ("00:00:00 с"). Other columns get visual slack for free because
their representative is a range extreme wider than typical cells; the time columns do not, so they
set the floor for the allowance. The cell text is centered with 4px side padding, so the on-screen
reserve per side is roughly `(allowance − 8) / 2`. The same allowance must also clear the header
padding (6px per side); 26 covers both consumers, so do not lower it below the header-padding need.
