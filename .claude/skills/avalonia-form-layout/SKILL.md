---
name: avalonia-form-layout
description: Use when building or reviewing Avalonia settings, preferences, options, or editor windows, forms, dialogs, property panes, or any AXAML where labels and controls must align across groups. Triggers on control alignment, ragged card widths, shared label/control columns, right-alignment consistency, numeric-input alignment, and dialog sizing/clipping. Avalonia 12+ (uses Grid shared-size scope + ColumnSpacing/RowSpacing).
---

# Avalonia form & settings-window layout

How to design and audit desktop settings/editor windows in Avalonia so labels and controls form clean vertical scan lines, cards are uniform width, and nothing clips under DPI scaling.

**Version assumption:** Avalonia 12 (verified on 12.0.4: `IsSharedSizeScopeProperty`, `SharedSizeGroupProperty`, `Grid.ColumnSpacing`/`RowSpacing` all present). On Avalonia 11.x there is no shared-size mechanism and this entire scheme changes — do not apply it there.

## Core mechanism in one sentence

Independent row `Grid`s align their columns to one shared width **only** when they sit inside a `Grid.IsSharedSizeScope="True"` boundary and their `ColumnDefinition`s carry a matching `SharedSizeGroup` on `Width="Auto"` (never `Width="*"`). That single fact resolves ragged labels, drifting controls and stranded font-size selectors at once.

## Core rules

Must:

- **One scope per co-visible surface** — put `Grid.IsSharedSizeScope="True"` on the single container that holds all rows that must align; one scope for a single scroll, one per tab if tabbed (cross-surface alignment is impossible and pointless once surfaces are not co-visible).
- **One column grammar, reused verbatim** in every row grid: `[Auto SettingLabel] [Auto SettingControl] [Auto WeightCol] [Auto ItalicCol] [* spacer]`; simple rows fill cols 0–1, unused Auto cols collapse to zero (one grammar is what makes label/control/right-alignment complaints collapse into a single fix).
- **Label columns are `Width="Auto"` with `SharedSizeGroup`, never `Width="*"`** — `SharedSizeGroup` is silently ignored on Star columns, so a Star label column resolves per-card and that *is* the misalignment.
- **Cards stretch to one constrained container, not shrink-wrap** — `HorizontalAlignment="Stretch"` inside a `MaxWidth`-limited single-column container; remove any `HorizontalAlignment="Left"` on cards and abolish `Auto,Auto` masonry (shrink-wrapped cards have different right edges, which no shared-size group can bridge; uniform width is owned by the container).
- **Every primary control shares the `SettingControl` column** — standalone `NumericUpDown`, `ColorPicker`, font-family `ComboBox`, *and* the font-table Size `NumericUpDown` all sit in col 1; Weight/Italic extend rightward (this is what puts the font Size selector directly under every other numeric).
- **Left-anchor controls in their shared columns with fixed widths** — `HorizontalAlignment="Left"` + fixed `Width`; push slack into the trailing Star spacer (pinning to a card's right edge re-creates the ragged-edge bug; a shared left edge gives one predictable scan line).
- **Fixed `Width` on every control type** — `NumericUpDown` 116 + `HorizontalContentAlignment="Right"`, `ColorPicker` ~200, Weight `ComboBox` 120, font-family `ComboBox` 220, Italic `CheckBox` auto; never auto-size to content (a value of 8 vs 1000 must not move the column). Note: the `SettingControl` column resolves to the **widest** member — with these numbers that is the 220 font-family combo, so set all primaries to one width if you want the column driven by a specific control.
- **Alignment via `HorizontalAlignment`/`HorizontalContentAlignment`, not `TextBlock TextAlignment="Right"`** inside a shared column; verify in the running app, not the previewer (a reported Avalonia bug — issue #13777, treat as unverified — makes `TextAlignment="Right"` fail at runtime inside a shared-size column; the element-alignment guidance is correct regardless).
- **Never put `Padding` on the `ScrollViewer`; put the inset on the scrollable child** (`Margin`/`Padding` on the inner `StackPanel`). `ScrollViewer.Padding` pads the content visually but is excluded from the scroll extent, so the last `Padding`-worth of content cannot be scrolled into view and the final row stays clipped behind a docked footer (confirmed Avalonia bug #12182, reproduces on 12.0.4). This bites silently: a top-anchored or non-scrolling render looks fine — only scrolling to the very end exposes it, so verify at the end of scroll, not just at the top.

Should:

- **Primary shape: one vertical, importance-ordered column of full-width cards.** It is strictly best for cross-feature Size alignment because both font sub-tables stay co-visible on one shared control line. Tabs/left-nav are an option for height relief, but tabs *dissolve* the cross-feature Size-alignment intent by hiding one table, they do not satisfy it — so only adopt tabs if the user accepts that.
- **Resizable window, modest minimums** — `CanResize="True"`, `MinWidth` near design width, `MinHeight` ~480–600 (NOT the design height — a ~900 MinHeight itself clips a 150%-scaled laptop), `VerticalScrollBarVisibility="Auto"`, `HorizontalScrollBarVisibility="Disabled"`. Do **not** use `SizeToContent` here — with an inner vertical ScrollViewer it measures to infinity, the scroll never engages and the window can exceed the screen.
- **Label column = longest label in scope + ~8–12px `ColumnSpacing`**; accept that short labels then sit further from their control (the documented cost of left-aligned labels). If a dense color table looks too sparse, switch those rows to top-aligned labels (gap zero) rather than stretching controls across the void.
- **Pick scope breadth deliberately** — window-wide scope aligns the two font tables' Size selectors but inflates short-label gaps to the window's longest label; per-tab/per-card scope tightens gaps but drops cross-surface alignment.
- **Centralize spacing as resources on an 8px module** (`x:Double`/`Thickness` keys) applied via `ColumnSpacing`/`RowSpacing`/`StackPanel.Spacing`, not scattered per-child `Margin`; intra-card spacing must be visibly tighter than inter-card spacing or grouping reads ambiguously.
- **Group by feature, sub-sections as plain sub-headers** — keep one bordered card per feature; render Dimensions/Fonts/Cell-states as text sub-headers, not nested bordered cards-in-cards; rename generic headers like "Global" to what they theme ("Application theme" / "Shared colors").
- **Restructure long flat color lists into a matrix** — e.g. Read-only/Disabled/Execution rows become a `Depth × {Current, Past}` grid instead of 20+ stacked single rows; this halves dialog height and is the main dead-space fix.
- **Provide a Reset-to-defaults affordance** (per-section or global) — standard for a ~50-value settings editor.
- **Define vertical alignment and tab order** — labels `VerticalAlignment="Center"` against taller `ColorPicker` rows; keep center/baseline consistent and decide how a top-aligned-label exception coexists with center-aligned neighbours in the same scope; set keyboard focus order to follow the new visual order.

May:

- **Accessibility (out of core complaint scope):** hit targets ≥24px, keep focus visuals, never encode state by color alone. For a dialog whose purpose is picking arbitrary theme colors, do not enforce a hard 4.5:1/3:1 rule — *warn/preview* contrast instead. (`tnum=1` tabular figures on numerics is cosmetic-only and does not reliably inherit into the inner TextBox — skip it; fixed width + right alignment already pin the arrows.)

## Layout recipe for a settings window

**Shape (primary).** One `ScrollViewer` (`HorizontalScrollBarVisibility="Disabled"`) over a single vertical `StackPanel` of full-width feature cards, importance-ordered. The panel carries `Grid.IsSharedSizeScope="True"`. Cap content width with a `MaxWidth` (~480–520px) container so label+gap+control fills the line; remaining window width is margin. A docked Save/Cancel/Reset footer sits in a separate `Grid.Row` below the scroll.

*(Option — tabs)* `TabControl`, one pane per feature, each pane its own `ScrollViewer` + `Grid.IsSharedSizeScope="True"`. Use only if height relief outweighs losing cross-feature Size alignment.

**Window:**

```xml
<Window CanResize="True"
        MinWidth="860" MinHeight="560"
        Width="900" Height="700">
```

**Scope placement** (the attached property goes on the parent once). The window inset lives on the inner `StackPanel`'s `Margin`, **not** on `ScrollViewer.Padding` (bug #12182 — padding on the ScrollViewer clips the last row at end-of-scroll):

```xml
<ScrollViewer HorizontalScrollBarVisibility="Disabled"
              VerticalScrollBarVisibility="Auto">
    <StackPanel Grid.IsSharedSizeScope="True"
                Spacing="{StaticResource CardGap}"
                Margin="20,16,20,20"
                MaxWidth="520"
                HorizontalAlignment="Stretch">
        <!-- feature cards here -->
    </StackPanel>
</ScrollViewer>
```

**The canonical column grammar** — `SharedSizeGroup` requires long-form `ColumnDefinition`s (the `ColumnDefinitions="Auto,Auto,*"` shorthand cannot carry it). Reuse this verbatim in every row grid:

```xml
<Grid ColumnSpacing="{StaticResource LabelGap}" RowSpacing="{StaticResource RowGap}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" SharedSizeGroup="SettingLabel"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="SettingControl"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="WeightCol"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="ItalicCol"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- simple row: label + one control, cols 2-3 collapse to zero -->
    <TextBlock Grid.Column="0" Text="Row height"
               VerticalAlignment="Center"/>
    <NumericUpDown Grid.Column="1" Width="{StaticResource ControlWidth}"
                   HorizontalAlignment="Left"
                   HorizontalContentAlignment="Right"/>
</Grid>
```

**Font sub-table** — same grammar, the cluster extends rightward; Size stays in `SettingControl` so it lines up with every simple numeric:

```xml
<Grid ColumnSpacing="{StaticResource LabelGap}" RowSpacing="{StaticResource RowGap}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" SharedSizeGroup="SettingLabel"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="SettingControl"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="WeightCol"/>
        <ColumnDefinition Width="Auto" SharedSizeGroup="ItalicCol"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <TextBlock Grid.Column="0" Text="Header font" VerticalAlignment="Center"/>
    <NumericUpDown Grid.Column="1" Width="{StaticResource ControlWidth}"
                   HorizontalAlignment="Left" HorizontalContentAlignment="Right"/>
    <ComboBox Grid.Column="2" Width="{StaticResource WeightWidth}"
              HorizontalAlignment="Left"/>
    <CheckBox Grid.Column="3" Content="Italic" HorizontalAlignment="Left"/>
</Grid>
```

**Spacing & width tokens** (centralize the magic numbers, 8px module):

```xml
<x:Double x:Key="ControlWidth">116</x:Double>
<x:Double x:Key="ColorWidth">200</x:Double>
<x:Double x:Key="WeightWidth">120</x:Double>
<x:Double x:Key="FontFamilyWidth">220</x:Double>
<x:Double x:Key="LabelGap">12</x:Double>
<x:Double x:Key="RowGap">6</x:Double>
<x:Double x:Key="CardGap">14</x:Double>
```

**Card style** — stretch, no shrink-wrap, no nested cards:

```xml
<Style Selector="Border.section-card">
    <Setter Property="HorizontalAlignment" Value="Stretch"/>
    <Setter Property="Padding" Value="16"/>
    <!-- sub-sections inside are plain TextBlock sub-headers, not Borders -->
</Style>
```

**Color list → matrix** (replaces long flat row stacks): a single grid with one `SharedSizeGroup` label column on the left and `{Current, Past}` (or state) columns of fixed-width `ColorPicker`s, so 20 stacked rows become ~10 and pickers align in two clean columns.

## Review checklist

Audit an existing settings window against these. Each line is pass/fail; verify in the **running app** under 100/125/150% Windows scaling, not the previewer.

Structure & width:
- [ ] No `Auto,Auto` (or any masonry) outer grid; sections are one vertical column (or tabs).
- [ ] No card has `HorizontalAlignment="Left"`; all cards `Stretch` to one `MaxWidth` container — **all card widths equal**.
- [ ] No nested bordered cards-in-cards; sub-sections are plain sub-headers.
- [ ] Grouping is by feature, not by control type; no generic "General"/"Global" headers.

Shared columns:
- [ ] Exactly one `Grid.IsSharedSizeScope="True"` per co-visible surface.
- [ ] No label column uses `Width="*"`; all are `Width="Auto"` + `SharedSizeGroup`.
- [ ] Every row grid uses the identical column grammar / group names.
- [ ] All labels sit on one vertical line; all primary controls' left edges on one vertical line.

Controls:
- [ ] Every control has a fixed `Width` (no auto-to-content); numerics 116 + right content alignment.
- [ ] Controls are `HorizontalAlignment="Left"` in their shared column, **not** pinned to the card's right edge.
- [ ] Every numeric spinner arrow — including the font-table **Size** — lands on one vertical line.
- [ ] Color-picker right edges aligned in their own column/matrix.
- [ ] No `TextBlock TextAlignment="Right"` inside a shared-size column.

Spacing & alignment:
- [ ] Intra-card spacing visibly tighter than inter-card gap.
- [ ] Label↔control gap minimal at the longest label; no oversized voids except the accepted short-label cost (or top-aligned labels in dense tables).
- [ ] Spacing/widths come from shared resources, not scattered per-child `Margin`.
- [ ] Labels vertically centered against taller (color-picker) rows; consistent baseline.

Window & sizing:
- [ ] `CanResize="True"`; `MinHeight` modest (~480–600), **not** the design height.
- [ ] No `SizeToContent` combined with an inner vertical scroll.
- [ ] No `Padding` on the `ScrollViewer` — inset is on the inner content's `Margin` (bug #12182); confirm the **last row scrolls fully clear of the footer**, checked at end-of-scroll.
- [ ] Horizontal scroll disabled; vertical auto.
- [ ] **No horizontal clipping and no large dead space at 125% and 150% scaling.**
- [ ] Footer (Save/Cancel/Reset) stays docked below the scroll; a Reset-to-defaults affordance exists.

Long lists & extras:
- [ ] Long flat color/state lists restructured into a matrix where applicable.
- [ ] Keyboard tab order follows the new visual order.
- [ ] Color state never conveyed by color alone; contrast warned/previewed (not hard-blocked).

## References

- Avalonia `SharedSizeGroup` / `IsSharedSizeScope` docs; Avalonia issue #13777 (TextAlignment-in-shared-column, unverified); Avalonia ScrollViewer infinite-width pitfall (issue #5020); Avalonia issue #12182 (ScrollViewer.Padding clips content / not scrollable to end — put inset on the child).
- WPF Shared Size Group model (same semantics).
- JetBrains UI Guidelines: groups of controls, align right borders of inputs, ">2 dialog heights → tabs", group headers (avoid "General"/"Global").
- WinUI settings (single column, scrollable if necessary); macOS preferences panes; GNOME Boxed Lists; KDE settings sidebar.
- NN/g Common Region & proximity; Penzo / UXmatters eye-tracking on label alignment; Wroblewski label-alignment tradeoffs; Kontur 8px module & false-relationships.
- WCAG 2.2 SC 2.5.8 (target size), 2.4.11 (focus visible).
