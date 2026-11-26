# UI Localization

## Overview

The static application chrome (menus, toolbar, status-bar labels, dialogs, view-model literals) is
localized through .NET `.resx` resources selected by a `locale` setting read from the equipment YAML
config. Two languages ship: Russian (`ru`) and English (`en`). The presets set `ru`. Domain text
(column headers, action names, group items) comes from YAML `ui_name` and is not part of this
mechanism; logs, diagnostics, and number/date formatting stay English/invariant.

## The `locale` setting

Locale lives in its own equipment-config file, `{configDir}/ui/app.yaml`, a sibling of
`grid_style.yaml` in the `ui/` folder. One key:

```yaml
locale: ru   # ru or en
```

The file and the key are required. Every preset (`MBE`, `MOCVD`, `RIE`) ships `ui/app.yaml` with
`locale: ru`. No code path holds a default locale.

It is loaded as its own config section, like `GridStyle`: `AppUiOptionsLoader` reads the file and
`ConfigFacade` bundles the immutable `AppUiOptions(string Locale)` record onto `AppConfiguration.Ui`.
`AppUiOptionsLoader` reports these problems in the loading step, together with every other loading
problem:

- `ui/app.yaml` is missing (`AppFileNotFoundError`, names the file path);
- the `locale` key is missing, null or blank, or the file is empty or misspells the key
  (`MissingKeyError`, names the key and the file);
- the value is not `ru` or `en` (`UnsupportedLocaleError`, names the value and the accepted values).

The loader trims the value and compares it case-insensitively, so `RU` and ` ru ` load as `ru`.
The loader returns the lower-case value. The accepted list lives only in Core, in
`AppUiOptionsLoader`. A value such as `de` is rejected on purpose: the UI has no `de` resources, so
it would show the English neutral text without a warning.

## The resx model: English is the neutral fallback

Two resource files live in `SemiStep.UI/Localization`:

- `Resources.resx` — the neutral/invariant set, holding **English** values. English is the fallback
  because it aligns with the English logs, so a missing satellite degrades to English rather than to
  a partial mix.
- `Resources.ru.resx` — the Russian set, compiled into the `ru/SemiStep.resources.dll` satellite
  assembly (the entry assembly name is `SemiStep`).

Resources are referenced from AXAML via `{x:Static l:Resources.Key}` (with
`xmlns:l="clr-namespace:SemiStep.UI.Localization"`) and from C# via `Resources.Key`. Composite labels
that need plural-neutral Russian phrasing or invariant-culture number formatting (`Шагов: {0}`,
`Ошибок: {0}`, the last-exchange age from `PlcLinkView.Build`) are folded into view-model properties rather than formatted in
XAML.

The last-exchange age is a set of bucket strings, one resource per bucket: `LinkAgeSecondsFormat`
(`{0} s ago`, whole seconds below 30 s), `LinkAgeOver30Seconds`, `LinkAgeOverMinute` and
`LinkAgeOverHour`; `LinkNever` stands in before the first successful read. Only the first bucket
carries a number. A number next to a Russian noun needs plural forms (`1 минута` / `3 минуты` /
`5 минут`), and resx cannot select one for a single label. `PlcLinkView` picks the bucket from the
injected `IScheduler.Now` and the last successful PLC read (`RecipeCoordinator.LastExchangeTime`).

The status-bar strings follow the same rule: the link texts (`Link*`), the execution texts
(`ExecutionRunningFormat`, `ExecutionRunningStepOnlyFormat`, `ExecutionRunning`, `ExecutionIdle`,
`ExecutionNoData`, `ExecutionLockToolTip`),
the loop texts (`LoopPass*`, `LoopLevel*`), the timer captions (`StatusStepLabel`, `StatusRecipeLabel`,
`StepTimerRunningCaption`, `RecipeTimerRunningCaption`, `RecipeTimerIdleCaption`) and
`WindowTitleRunningSuffix` live in both resx files and fit the fixed slots in both languages. The step
and pass numbers are padded with U+2007 figure spaces to the digit count of their total.
`ExecutionRunningStepOnlyFormat` carries no total: it shows the PLC step when that step is past the
local recipe's step count, and that number is not padded.

## The generated accessor

`Microsoft.CodeAnalysis.ResxSourceGenerator` writes the accessor. `Directory.Packages.props` pins the
version exactly (the package has no stable release), and `SemiStep.UI.csproj` references it with
`PrivateAssets="all"` and one item:

```xml
<EmbeddedResource Update="Localization/Resources.resx" GenerateSource="true"/>
```

The generated type is `internal static partial class SemiStep.UI.Localization.Resources`. It has one
`public static string` per key, a static `Culture` and a `ResourceManager`. `InternalsVisibleTo` gives
the test project access, and XamlIl rewrites the same assembly, so the item needs no `Public="true"`.
`NeutralLanguage` is `en` in the csproj: without it the analyzer baseline fails the build with `CA1824`.

The generator runs inside the compiler, so the accessor exists before XamlIl runs. MSBuild
`StronglyTyped*` generation writes the class in a target that runs after the Avalonia XAML compile. On a
**cold** build the class does not exist when XamlIl runs, every `{x:Static l:Resources.*}` reference fails with
`AVLN2000: Unable to resolve "Resources.X"`, `App.Initialize()` throws `XamlLoadException` at test time,
and every `[AvaloniaFact]` deadlocks. That is why the project does not use MSBuild generation.

**Consequence for contributors.** Adding a localized string means editing **two** files:

1. `Resources.resx` - the English (neutral) value. The key becomes `Resources.<Key>` at the next build.
2. `Resources.ru.resx` - the Russian value.

Miss the Russian value and the Russian UI falls back to English. `ResourceTranslationTests` catches
that: it compares the raw key sets of both files, rejects blank values and checks that each key uses
the same `{n}` placeholders in English and Russian. Remove a key and every remaining use fails the
build.

## Culture wiring: only UICulture changes

Config is loaded on a worker thread (`Program.cs` `StartupAsync`), which sets the process-global
`CultureInfo.DefaultThreadCurrentUICulture` and `Resources.Culture` from
`CultureInfo.GetCultureInfo(config.Ui.Locale)` after a successful load. The loader has already
limited `Ui.Locale` to `ru` or `en`, so there is no selector and no fallback. `Program` sets no
culture before the config is read: the only UI before that point is `ErrorWindow`, which has
hard-coded English text.

`CurrentCulture` is **never** assigned. Resource lookup keys off `UICulture`; number/date formatting
and Serilog timestamps key off `CurrentCulture` and stay invariant/English. So the last-sync seconds, step
counts, and log timestamps stay invariant regardless of UI language — the surrounding words localize,
the numbers do not. Log message templates stay hardcoded English by design. The operational/error/status
strings the operator sees (command-fault contexts, report/success messages, file-picker titles) localize —
see "Operational, error, and status messages" below. Typed Core failures localize on type at the panel
seams through `ReasonLocalizer` — see `error-reporting.md` ("Localizing failures on type"); free-text
`Result.Fail("…")` and exception `.Message` detail stay English until their per-subsystem waves.

## Operational, error, and status messages

The UI-composed strings on the error and status paths — the ones an operator reads when a command fails
or a file is saved — go through resx like the rest of the chrome. They split into two shapes by whether
the same string is also logged.

**Report+log context (dual-use).** Command faults funnel through a single `context` argument that is BOTH
logged AND shown in the notification panel (`ExceptionReporter.ReportAndLog` logs the context, then reports
`"{context}: {ex.Message}"`). Logs must stay English (they align with the English log templates) while the
panel must localize. A `LocalizedText` (in `SemiStep.UI.Localization`) carries a resx key and resolves it
two ways: `.Invariant` for the log (`GetString(key, CultureInfo.InvariantCulture)`) and `.Localized` for the
panel (`GetString(key, Resources.Culture)`). Call sites build it compile-safely against the generated accessor,
`new LocalizedText(nameof(Resources.CopyStepFailed))` — a typo fails the build because the accessor must exist.
`ReportThrownExceptions`, `ExceptionReporter.ReportAndLog`, and `MainWindowViewModel.Guarded`/`OnSubscriptionError`
take `LocalizedText`; so the same fault logs English and shows the current-culture value from one key.

**Panel-only strings.** Sites that only report (no log) take the localized accessor directly:
`ReportError(Resources.X)`, `ReportFailure(result, Resources.X)`, and
`ReportSuccess(string.Format(CultureInfo.InvariantCulture, Resources.SavedFormat, name))` for the count/name
formats. The generic backstop message and the PLC-conflict-dialog-show failure resolve the same way.

**File pickers.** The open/save dialog `Title` and the `FilePickerFileType` display names ("Recipe Files",
"All Files", "CSV Files") come from resx accessors. The window title is not localized: it is the product
name plus user data plus a `" *"` dirty marker, and its only natural-language part (the new-recipe placeholder)
already uses `Resources.WindowTitleNewRecipe`.

**What stays English.** Exception `.Message` detail (the suffix after the localized context), free-text
Core `Result` text not yet typed by its wave, and every `logger.Log*` template. The `ReasonLocalizer` seam
localizes typed Core error *content* on type; #115 localizes only the UI-composed *context* around it — a
disjoint seam. See `error-reporting.md`.

## `ErrorWindow` is deliberately hardcoded English

`Dialogs/ErrorWindow.axaml` is hardcoded English and is **not** routed through resx (no `xmlns:l`, no
`{x:Static}`). It renders only when config load fails — the point at which locale is unknown by
construction and can never be data-driven. Its body is already English `error.Message` text matching
the English logs, and it is a diagnostic screen for the integrator, not the operator. Keeping it
resx-free also keeps this fail-safe path free of any resx/culture-init dependency.

## Domain text stays outside resx

Column headers, action names, and group items come from YAML `ui_name`; the one exception is the
step number header (`StepNumberHeader`: "№" in Russian, "No" in English), which the canonical
numbering column and the transposed corner read from resx. Grid chrome texts in resx also include
`RampTimeToolTip`, the tooltip of a ramp step's time cell. Logs, diagnostics, and
number/date formatting stay English/invariant. Font-weight names in the grid-style editor's weight
picker (`Light`, `Normal`, `SemiBold`, …) are kept in English as conventional typographic tokens.
