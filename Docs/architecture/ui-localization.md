# UI Localization

## Overview

The static application chrome (menus, toolbar, status-bar labels, dialogs, view-model literals) is
localized through .NET `.resx` resources selected by a `locale` setting read from the equipment YAML
config. Two languages ship: Russian (default) and English. Domain text (column headers, action names,
group items) comes from YAML `ui_name` and is not part of this mechanism; logs, diagnostics, and
number/date formatting stay English/invariant.

## The `locale` setting

Locale lives in its own equipment-config file, `{configDir}/ui/app.yaml`, a sibling of
`grid_style.yaml` in the `ui/` folder. One key:

```yaml
locale: ru   # or en; absent/invalid -> ru
```

It is loaded as its own config section, mirroring the `GridStyle` pattern:
`AppUiOptionsLoader` reads the file, `AppUiOptionsMapper` maps the DTO to the immutable
`AppUiOptions(string Locale)` record, and `ConfigFacade` bundles it onto `AppConfiguration.Ui`.

Unlike `GridStyleLoader`, which fails when its file is absent, `AppUiOptionsLoader` returns
`Ok(null)` when `ui/app.yaml` or the `ui/` directory is missing — the file is optional and its
absence does not fail the config load; the mapper converts null to `AppUiOptions.Default`. The mapper
also returns `Default` (locale `ru`) when the DTO or the `locale` value is null or blank; it does not
validate the culture. An invalid-but-present culture is caught later in `UiCultureSelector.Resolve`,
which falls back to `ru`. So the default UI language is `ru` whenever the file, the key, or a valid
value is missing.

## The resx model: English is the neutral fallback

Two resource files live in `SemiStep.UI/Localization`:

- `Resources.resx` — the neutral/invariant set, holding **English** values. English is the fallback
  because it aligns with the English logs, so a missing satellite degrades to English rather than to
  a partial mix.
- `Resources.ru.resx` — the Russian set, compiled into the `ru/Semistep.resources.dll` satellite
  assembly (the entry assembly name is `Semistep`).

Resources are referenced from AXAML via `{x:Static l:Resources.Key}` (with
`xmlns:l="clr-namespace:SemiStep.UI.Localization"`) and from C# via `Resources.Key`. Composite labels
that need plural-neutral Russian phrasing or invariant-culture number formatting (`Шагов: {0}`,
`Ошибок: {0}`, last-sync elapsed text) are folded into view-model properties rather than formatted in
XAML.

## Gotcha: the accessor is a committed source file, not build-time-generated

The strongly-typed accessor is a **committed** source file,
`SemiStep.UI/Localization/Resources.Designer.cs` — a hand-authored public partial class in the
standard auto-generated shape (lazy `ResourceManager` on base name
`SemiStep.UI.Localization.Resources`, a public static `Culture`, one `public static string Key => ...`
property per key). It is deliberately **not** produced by MSBuild `EmbeddedResource`
`StronglyType*` metadata.

The reason is a build-ordering race. Build-time generation emits the `Resources` class too late: on a
**cold** build the class does not exist when the Avalonia XAML compiler (XamlIl) runs, so every
`{x:Static l:Resources.*}` reference fails with `AVLN2000: Unable to resolve "Resources.X"`.
`App.axaml` then fails to precompile, and at test time `App.Initialize()` throws `XamlLoadException`,
killing the shared headless dispatcher — so every `[AvaloniaFact]` deadlocks. A committed `.cs` file
is visible to both the C# compiler and XamlIl before their passes, so the reference resolves on a cold
build.

**Consequence for contributors.** Adding a localized string means editing **three** files in sync:

1. `Resources.resx` — the English (neutral) value.
2. `Resources.ru.resx` — the Russian value.
3. `Resources.Designer.cs` — a new `public static string Key => ...` property.

Miss the third and XAML/C# cannot reference the key; miss either resx and one language falls back to
the other (or to the key-less default).

## Culture wiring: only UICulture changes

Config is loaded on a worker thread (`Program.cs` `StartupAsync`), which sets the process-global
`CultureInfo.DefaultThreadCurrentUICulture` and `Resources.Culture` from
`UiCultureSelector.Resolve(config.Ui.Locale)` after a successful load. `UiCultureSelector.Resolve`
returns the requested culture, or `ru` on null/blank input, on `CultureNotFoundException`, and on
ICU's synthesized custom-culture LCID `0x1000` (which ICU produces for structurally-valid but unknown
tags instead of throwing).

`CurrentCulture` is **never** assigned. Resource lookup keys off `UICulture`; number/date formatting
and Serilog timestamps key off `CurrentCulture` and stay invariant/English. So `{elapsed:0.0}`, step
counts, and log timestamps stay invariant regardless of UI language — the surrounding words localize,
the numbers do not. Log message templates and notification-panel diagnostic strings (which carry
exception text) stay hardcoded English by design.

## `ErrorWindow` is deliberately hardcoded English

`Dialogs/ErrorWindow.axaml` is hardcoded English and is **not** routed through resx (no `xmlns:l`, no
`{x:Static}`). It renders only when config load fails — the point at which locale is unknown by
construction and can never be data-driven. Its body is already English `error.Message` text matching
the English logs, and it is a diagnostic screen for the integrator, not the operator. Keeping it
resx-free also keeps this fail-safe path free of any resx/culture-init dependency.

## Domain text stays outside resx

Column headers, action names, and group items come from YAML `ui_name`; logs, diagnostics, and
number/date formatting stay English/invariant. Font-weight names in the grid-style editor's weight
picker (`Light`, `Normal`, `SemiBold`, …) are kept in English as conventional typographic tokens.
