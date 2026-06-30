---
name: avalonia-visual-style
description: Use when theming or restyling an Avalonia desktop app toward a JetBrains/IntelliJ "New UI" look (flat, dense, neutral grays, one restrained blue accent, light+dark). Covers the JetBrains Int UI design tokens (color ramps, typography, dimensions, radii), and how to realize them in Avalonia 12 via Semi.Avalonia + token overrides + Inter/JetBrains Mono + theme variants. Triggers on theme/palette/dark-mode/accent/font/corner-radius/design-token work. Distinct from avalonia-form-layout (which is about alignment/spacing inside a single form).
---

# Avalonia visual style — JetBrains/IntelliJ "New UI" look

How to give an Avalonia 12 desktop app the IntelliJ "New UI" aesthetic: flat surfaces, neutral gray ramps, one restrained blue accent, dense fixed control sizing, small uniform rounding, Inter UI font + JetBrains Mono, with a clean path to dark mode. Layout/alignment of an individual form is the separate `avalonia-form-layout` skill — use both together when building a settings window.

**Version assumption:** Avalonia 12.0.x, Semi.Avalonia 12.0.x. Token resolution relies on `ThemeDictionaries` + `{DynamicResource}`, which exist in Avalonia 11+, but the package/key names here are verified on Avalonia 12.0.4 / Semi.Avalonia 12.0.3.

## The two-layer model

1. **Design tokens** — JetBrains' own values (below), grouped as a named palette → semantic roles → component usage. Never hardcode a hex at a control; map it to a role.
2. **Avalonia realization** — a vendor base theme (Semi.Avalonia) supplies control templates; you retint it by **overriding the `DynamicResource` keys its ControlThemes consume**, per theme variant. You do not fork or retemplate to recolor.

## JetBrains Int UI design tokens

All hex below are from JetBrains' own `expUI_light.theme.json` / `expUI_dark.theme.json` (intellij-community) and component metrics from Jewel (`IntUiGlobalMetrics.kt`, `IntUiGlobalColors.kt`). They are the real values, not an approximation.

### Accent — one blue, restrained

`#3574F0` in **both** light and dark (the ramp shifts so the mid index lands on the same hex). Pressed: light `#315FBD`, dark `#375FAD`. Blue is used **only** for: the single default/primary action, focus rings, links, selection. Everything else is neutral gray. One accent button per dialog.

### Neutral gray ramp (the backbone)

| Role | Light | Dark |
|---|---|---|
| primary text | `#000000` | `#DFE1E5` |
| secondary / caption text | `#818594` | `#6F737A` |
| disabled text | `#A8ADBD` | `#5A5D63` |
| default border | `#EBECF0` | `#1E1F22` |
| divider / hairline | `#EBECF0` | `#393B40` |
| input background / surface | `#FFFFFF` | `#2B2D30` |
| panel / tool-window background | `#F7F8FA` | `#2B2D30` |
| editor / content background | `#FFFFFF` | `#1E1F22` |
| input border (rest) | `#C9CCD6` | `#43454A` |
| window chrome | `#F0F1F5` | `#35363B` |

Key dark surfaces to remember: editor `#1E1F22`, panel `#2B2D30`, primary text `#DFE1E5`, border `#1E1F22`.

### Semantic colors (light / dark)

| Role | Light | Dark |
|---|---|---|
| error text | `#DB3B4B` | `#DB5C5C` |
| success | `#208A3C` | `#5FAD65` |
| warning | `#FFAF0F` | `#F2C55C` |
| link / focus ring | `#3574F0` | `#3574F0` |

### Typography

- **UI font: Inter**, base size **13** on every OS. Define all other sizes relative to 13: H1 = +5 (≈18), H2 = +3 (≈16), help/caption = −1 (≈12). **Semibold (600)** for dialog/popup/section headers, regular for body.
- **Editor font: JetBrains Mono**; line numbers −1.
- Both are **SIL OFL 1.1** — free to embed and ship commercially; keep the OFL.txt beside the bundled files.
- Never hardcode a size; express as 13 ± constant.

### Dimensions & density (dp ≈ px @100%)

- **List/tree/combo row height = 24.** This is the density unit.
- **Button** min `72×28`, padding h12 v6, border 1.
- **Text field** min `144×28`, padding h9.
- **Combo box** min `49×24`, padding h6 v2.
- **Checkbox** touch `24×24`, visible box `16×16`.
- **Focus outline width = 2.**
- **Corner radius = 4, uniform** across button / text field / combo / dropdown. Checkbox box 3 (unchecked) → 4.5 (checked). Borders 1. This is the whole "flat, slightly rounded" feel.

### Five principles (the soul, not the hex)

1. **One named palette drives everything** — discrete Gray/Blue/Red/Green/Yellow ramps, separate Light/Dark, reached through semantic roles, never a literal color at a control. (platform-theme-colors)
2. **Restrained single accent `#3574F0`** — blue only for the one primary action, focus, links, selection; all else neutral gray.
3. **Dense, fixed control sizing** — 24px rows, 28px controls, small even paddings (6/9/12), 4px radius. Not airy, not cramped.
4. **Settings = master-detail** — left category tree + right pane, left-aligned labels, ≤2 input columns, grouping by inset whitespace (see `avalonia-form-layout` for the right-pane mechanics).
5. **Flat sections, not bordered cards** — separation is whitespace + a muted title-case header (~13px, secondary gray `#818594`), not a `Border` box. The "Islands" direction (default since 2025.3) adds *larger container radii (~8–12px) and more padding around major regions*, keeping the flat palette — but inside a dialog, sections stay flat and header-only.

## Avalonia realization

### Base theme — Semi.Avalonia

Semi.Avalonia is the closest mainstream Avalonia theme to an IDE look (flat, neutral, small radii, one blue accent). It is not a JetBrains clone, but Semi + token overrides + Inter/JetBrains Mono gets visually close. **No ready-made JetBrains/IntelliJ Avalonia theme exists** — you assemble it.

Packages (keep every `Semi.Avalonia*` on the same 12.0.x line; Semi 12.0.3 requires Avalonia ≥12.0.3, so 12.0.4 is fine):

| Package | Purpose |
|---|---|
| `Semi.Avalonia` | base theme — replaces `<FluentTheme/>` |
| `Semi.Avalonia.DataGrid` | replaces the Fluent DataGrid include |
| `Semi.Avalonia.ColorPicker` | replaces the Fluent ColorPicker include |
| `Avalonia.Fonts.Inter` | Inter UI font via `.WithInterFont()` |
| (embed) JetBrains Mono `.ttf` | mono cells |

`App.axaml` — drop the Fluent theme + Fluent control-theme includes, add Semi (app's own `DataGridStyles.axaml` stays **after** the Semi DataGrid theme so its Style setters win):

```xml
<Application xmlns:semi="https://irihi.tech/semi"
             RequestedThemeVariant="Light">
  <Application.Styles>
    <semi:SemiTheme />
    <semi:ColorPickerSemiTheme />
    <semi:DataGridSemiTheme />
    <!-- app overrides + DataGridStyles.axaml come after -->
  </Application.Styles>
</Application>
```

Semi ships Light + Dark (plus four custom variants); `RequestedThemeVariant="Light"|"Dark"` selects the standard two.

### Token override — the retint mechanism

Semi assigns brushes/dimensions via `{DynamicResource <Key>}` in its ControlThemes. Override by **redefining the same key at a higher scope** — live, no retemplate.

Dimensions (single value) go straight in `Application.Resources`:

```xml
<Application.Resources>
  <CornerRadius x:Key="ButtonCornerRadius">4</CornerRadius>
</Application.Resources>
```

Colors go **per variant** in `ThemeDictionaries` (so light/dark stay correct), consumed via `DynamicResource`:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
      <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="SemiColorPrimary" Color="#3574F0"/>
      </ResourceDictionary>
      <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="SemiColorPrimary" Color="#3574F0"/>
      </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

Semi's keys are three tiers; **retarget the semantic tier** for a whole-app shift:

- Raw palette: `Semi{Color}{0..9}` (e.g. `SemiBlue3`).
- **Semantic (retarget these):** `SemiColorPrimary`, `SemiColorText0..3`, `SemiColorFill0..2`, `SemiColorBackground0..3`, `SemiColorBorder`, `SemiColorDisabledText`, `SemiShadowElevated`.
- Control keys: `ButtonCornerRadius`, `ButtonDefaultPrimaryForeground`, and DataGrid keys `DataGridCellBackground`, `DataGridCellMinHeight`, `DataGridRowBackground`, `DataGridRowSelectedBackground`, `DataGridRowPointeroverBackground`, `DataGridCellCurrentBorderBrush`, `DataGridCellFocusBorderBrush`.

To find the exact key a control property uses, read that control's AXAML in the Semi repo (`/src/Semi.Avalonia/Controls/<Control>.axaml` for the setter; `/src/Semi.Avalonia/Themes/Shared/...` for the default value).

**JetBrains → Semi semantic mapping (the core retint):**

| JetBrains role | Semi key | Light | Dark |
|---|---|---|---|
| accent | `SemiColorPrimary` | `#3574F0` | `#3574F0` |
| content bg | `SemiColorBackground0` | `#FFFFFF` | `#1E1F22` |
| panel bg | `SemiColorBackground1` | `#F7F8FA` | `#2B2D30` |
| primary text | `SemiColorText0` | `#000000` | `#DFE1E5` |
| secondary text | `SemiColorText2` | `#818594` | `#6F737A` |
| disabled text | `SemiColorDisabledText` | `#A8ADBD` | `#5A5D63` |
| divider/border | `SemiColorBorder` | `#EBECF0` | `#393B40` |
| radius (all) | `*CornerRadius` | `4` | `4` |

(Verify each `SemiColorBackground*`/`Fill*` index against the shipped value before overriding — the index→surface mapping is Semi's, read it from the Palette demo or the variant token files in `Semi.Avalonia.Tokens.Palette`.)

### Fonts

URI is always `avares://Assembly/Path#Internal Family Name` — the `#name` is the font's **internal** metadata name, not the filename; omit it and the font silently falls back. Mark files `<AvaloniaResource Include="Assets/Fonts/*"/>`.

- **Inter**: `dotnet add package Avalonia.Fonts.Inter`, `.WithInterFont()` in `BuildAvaloniaApp()`, reference `avares://Avalonia.Fonts.Inter/Assets#Inter` (a full `avares://` URI into the package — no XML-namespace prefix is declared for fonts).
- **JetBrains Mono**: embed `JetBrainsMono-*.ttf` under `Assets/Fonts/`, declare inside `MergedDictionaries` (the documented workaround for fonts failing when declared bare in `Application.Resources`):
  ```xml
  <FontFamily x:Key="MonoFont">avares://YourApp/Assets/Fonts#JetBrains Mono</FontFamily>
  ```
- **App-wide default Inter** via the inherited `FontFamily` on a base `Window` style (`FontFamily` inherits down the tree):
  ```xml
  <Style Selector="Window"><Setter Property="FontFamily" Value="avares://Avalonia.Fonts.Inter/Assets#Inter"/></Style>
  ```
  Optionally also set `FontManagerOptions.DefaultFamilyName` for the framework's ultimate fallback. For grid numerics, `FontFeatures="+tnum"` gives tabular figures.

### Dark mode

- `RequestedThemeVariant` (on Application/Window/`ThemeVariantScope`) is the knob; `ActualThemeVariant` (inherited) is what styling resolves against. `"Default"` follows the OS.
- Variant brushes live in `ThemeDictionaries` keyed `Light`/`Dark` and resolve **only via `{DynamicResource}`** — `{StaticResource}` throws unless the key also exists outside a theme dictionary. This is the cooperation rule.
- To add dark later: flip `RequestedThemeVariant`, and split every app-owned token (the custom `ColorPalette.axaml` brushes) into Light/Dark `ThemeDictionaries`. Any override of Semi's own keys must likewise supply both variants. A config-driven palette tuned in light hex needs a parallel dark palette (or a derived darkening) — budget for it.

### ControlTheme precedence (why overrides, not forks)

Value precedence high→low: local/inline > **Style setter** (`Application.Styles`/`Window.Styles`, selector-gated) > **ControlTheme setter** (vendor default) > inherited > default. So a Style setter always beats a vendor ControlTheme setter for the same property. Three override options, least invasive first:

1. Override the `DynamicResource` keys the vendor ControlTheme consumes (color/dimension) — smallest blast radius, preferred.
2. Add a Style selector that sets the property directly — wins over the ControlTheme setter (how bespoke DataGrid cell theming survives a base-theme swap).
3. Replace the whole ControlTheme (retemplate) — only when structure must change.

### DataGrid reconciliation (when the app has bespoke cell theming)

Swapping Fluent→Semi DataGrid is low-risk: Semi's `DataGridCell` ControlTheme sets `Background` to `DataGridCellBackground` as a **ControlTheme setter** and paints the cell visual with `Background="{TemplateBinding Background}"`. An app that sets cell `Background` via **Style** setters therefore still wins (same contract Fluent gives). Re-verify after the swap:

- Row-level selection/hover (`DataGridRowSelectedBackground`, `DataGridRowPointeroverBackground`) is painted on a row `Border` beneath cells — it only shows through cells left at default background; opaque app cell backgrounds cover it. Neutralize those keys if a fully app-controlled look is wanted.
- Current/focus is drawn as overlay Rectangles (`DataGridCellCurrentBorderBrush`, `DataGridCellFocusBorderBrush`); confirm they render cleanly at the configured row height (`DataGridCellMinHeight` now replaces the Fluent MinHeight floor).
- Grid-line and MinHeight Style setters at `DataGrid` level survive; confirm Semi's denser default padding/borders don't double up.

## Pitfalls

- **`ThemeDictionaries` + `StaticResource` = runtime throw.** Variant brushes must be consumed with `DynamicResource`.
- **Font without `#FamilyName` silently falls back.** Use the internal family name; wrap bare `Application.Resources` font declarations in `MergedDictionaries`.
- **Semi's GitHub README version table is stale** (stops ~11.3.7). NuGet is the source of truth — pin all `Semi.Avalonia*` to one 12.0.x line.
- **Don't fork the vendor theme to recolor.** Tokens + targeted Styles cover the retint; forking inherits maintenance of the whole template.
- **Don't scatter hex at controls.** Map every color to a semantic key so dark mode is a dictionary swap, not a hunt.
- **One accent only.** Resist coloring secondary actions; JetBrains' restraint is the look.

## References

- JetBrains tokens (authoritative): `intellij-community` `platform/platform-resources/src/themes/expUI/expUI_light.theme.json` / `expUI_dark.theme.json`; Jewel (`github.com/JetBrains/jewel`, Apache-2.0) for metrics/color mappings.
- JetBrains guidelines: typography `plugins.jetbrains.com/docs/intellij/typography.html`; layout `…/layout.html`; theme colors `…/platform-theme-colors.html`; New UI `jetbrains.com/help/idea/new-ui.html`; Islands `blog.jetbrains.com/platform/2025/12/meet-the-islands-theme-…`.
- Semi.Avalonia: `github.com/irihitech/Semi.Avalonia`; docs `docs.irihi.tech/semi`; resource customization `…/docs/basic/resource-customization` + `…/advanced/resource-customization`; NuGet `nuget.org/packages/Semi.Avalonia`.
- Avalonia: themes `docs.avaloniaui.net/docs/styling/themes`; theme variants `…/theme-variants`; custom fonts `…/custom-fonts`.
- Fonts: Inter `github.com/rsms/inter` (OFL-1.1); JetBrains Mono `jetbrains.com/lp/mono` (OFL-1.1).
