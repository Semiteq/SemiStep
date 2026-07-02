# Russify UI with multi-language support (resx, locale from ui/app.yaml, default ru)

Closes #69.

## Overview

Localize the static application chrome (menus, dialogs, buttons, status-bar labels,
view-model literals) via .NET `.resx` resources selected by a `locale` setting read
from the equipment YAML config. Neutral/invariant resources are **English** (fallback,
aligns with English logs); `Resources.ru.resx` is **Russian**. Default UI language is
`ru`.

Domain text (column headers, action names, group items) is already Russian, comes from
YAML `ui_name`, and stays there — out of scope. Log message templates stay hardcoded
English — out of scope.

**Explicitly out of scope (stays English this PR, keeps it one logical change):**
- `SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml` (~90 labels) and
  `SemiStep.UI/StyleEditor/RestartPromptDialog.axaml` — the grid-style settings editor and
  its restart prompt, reachable only via the "Grid Style Settings…" menu item. The menu
  item itself is localized; the editor window is a follow-up.
- Notification-panel `ReportError` diagnostic strings carrying exception text.

**`ErrorWindow` is deliberately English, hardcoded, NOT routed through resx.** It renders
only when config load fails, so locale is unknown by construction and can never be
data-driven. Its body (the error list) is already English `error.Message` text matching
the English logs; a Russian frame around English errors is incoherent. It is a diagnostic
screen for the integrator/engineer, not the operator. Hardcoding also keeps this fail-safe
path free of resx/culture-init dependencies. Action: translate its current Russian chrome
("Ошибка загрузки конфигурации", header, "Выход") to English. No `ru` values, no `x:Static`.

**In scope beyond the issue's original list** (default-visible chrome the issue missed):
`SemiStep.UI/MainWindow/RecipeToolBar.axaml` (toolbar buttons + tooltips, visible by
default) and the Error/Warning count strings in
`SemiStep.UI/MessageService/MessagePanelViewModel.cs` (rendered in the in-scope status
bar) plus `MessagePanel.axaml` `Close` tooltip.

**Deviation from issue text:** the issue says set the custom tool to
`PublicResXFileCodeGenerator`. That tool is Visual-Studio-only, but the artifact it
implies — a committed public `Resources` accessor checked into source — is the correct
approach, and that is what we ship: a hand-authored `Localization/Resources.Designer.cs`
committed alongside the resx. Build-time generation via MSBuild `EmbeddedResource`
`StronglyType*` metadata was tried and is wrong: on a cold build the accessor does not
exist when the Avalonia XAML compiler (XamlIl) runs, so every `{x:Static l:Resources.*}`
fails with `AVLN2000`, `App.axaml` is not precompiled, and at test time `App.Initialize()`
throws `XamlLoadException`, killing the headless dispatcher so every `[AvaloniaFact]`
hangs. A committed source file is visible to both the C# and XamlIl passes, so it resolves.

Key design decision from discussion: `locale` lives in its own equipment-config file
`ui/app.yaml` (a dedicated `AppUiOptions` section on `AppConfiguration`), **not** inside
`GridStyleOptions` (that record is purely grid styling; adding language there mixes
responsibilities). A user-writable `%APPDATA%` preferences store is explicitly a
separate, future concern — not this issue.

## Context (from discovery)

Files/areas involved:
- Startup/culture: `SemiStep.UI/Program.cs` (loads config before `App.Run`; logs already
  pinned to `CultureInfo.InvariantCulture`), `SemiStep.UI/StartupOptions.cs`.
- Config layer: `SemiStep.Core/Configuration/Facade/ConfigFacade.cs`
  (`LoadAllSectionsAsync`, `MapToDomain`, `LoadedSections`),
  `SemiStep.Core/Configuration/AppConfiguration.cs`,
  loader pattern `SemiStep.Core/Configuration/Loaders/GridStyleLoader.cs` (~30 lines,
  generic YAML deserialize — the template to copy).
- Chrome AXAML: `MainWindow/RecipeMenuBar.axaml`, `MainWindow/MainWindow.axaml`
  (Title binding + DataGrid context menu), `MainWindow/AppStatusBar.axaml`,
  `Dialogs/ErrorWindow.axaml` (already hardcoded Russian), `Dialogs/MessageDialog.axaml`,
  `Plc/PlcConflictDialog.axaml`, `ShutdownService/ExitConfirmationDialog.axaml`.
- Chrome C#: `MainWindow/MainWindowViewModel.cs` (`MapSyncStatus`, `FormatLastSyncTime`,
  `BuildWindowTitle`).
- Project: `SemiStep.UI/SemiStep.UI.csproj` (SDK-style; will host the resx).

Patterns observed:
- Each config section = a static loader + a DTO + entry in `LoadedSections` + mapping in
  `MapToDomain` + a field on `AppConfiguration`. FluentResults for load/validate.
- Config `ui/` folder currently holds only `grid_style.yaml`; `ui/app.yaml` is a natural
  sibling.
- Status bar already has Russian literals (`Шаг:`, `Рецепт:`) hardcoded — these move to
  resources too so both languages are covered from one source.

Dependencies: YamlDotNet (already used), Avalonia XAML `{x:Static}` (no new package).

## Development Approach

- **Testing approach**: Regular (code first, then tests). Most of the work is mechanical
  string extraction; tests target the parts with real logic: locale loading/defaulting,
  culture selection, and resource resolution (a known key differs between `en` and `ru`).
- Complete each task fully before the next; run tests after each; keep this plan in sync.
- **Every task with logic includes tests.** Pure XAML-literal extraction (Task 5) is
  verified by a build + a headless smoke test rather than per-string unit tests.

## Testing Strategy

- **Unit tests**: locale DTO/loader/mapper defaulting (`Component=Config`); culture
  selection helper (`Component=UI`); a resource-resolution smoke test asserting a chosen
  key resolves to the English neutral value under `en` and the Russian value under `ru`.
- **Headless UI test** (`[AvaloniaFact]`): with culture `ru`, a menu/dialog control's
  localized text equals the `ru` resource value — proves `{x:Static}` wiring renders.
- No e2e framework in this project; headless Avalonia tests are the ceiling.

## Solution Overview

- Add `AppUiOptions(string Locale)` to the domain, loaded from `ui/app.yaml`
  (`locale:` key), default `ru` when the file/key is absent or the value is not a valid
  culture. Surface it as `AppConfiguration.Ui`.
- Set the default UI culture to `ru` at the very start of `Main` (before config load), so
  the failure path (`ErrorWindow`, shown when config load fails and locale is unknown)
  already renders in the default language. After a successful load, override
  `CultureInfo.DefaultThreadCurrentUICulture` and `Resources.Culture` from
  `config.Ui.Locale`, before `App.Run`.
- Change **only `UICulture`**, never `CurrentCulture`. Resource lookup keys off UICulture;
  number/date formatting and logs key off CurrentCulture and must stay invariant/English.
- Add `Resources.resx` (English neutral) + `Resources.ru.resx` (Russian) in
  `SemiStep.UI/Localization`, generator `PublicResXFileCodeGenerator` (public class so the
  Avalonia XAML compiler can reference it via `{x:Static}`).
- Replace chrome literals: XAML via `{x:Static l:Resources.Key}`; C# via `Resources.Key`.

## Technical Details

- Culture set-up helper (pure, testable): `string -> CultureInfo` with `ru` fallback on
  null/blank/invalid (`CultureNotFoundException`).
- `ui/app.yaml` shape:
  ```yaml
  locale: ru   # or en; absent/invalid -> ru
  ```
- Resource-key naming: group by area, `PascalCase`, e.g. `MenuFile`, `MenuFileNewRecipe`,
  `StatusSyncOn`, `StatusSyncOff`, `StatusConnecting`, `DialogExitUnsavedMessage`,
  `ButtonSave`, `ButtonCancel`, `WindowTitleNewRecipe`, `ErrorWindowTitle`, etc.
- **StringFormat wrinkle**: you cannot embed `{x:Static}` *inside* a literal
  `StringFormat` string, but you **can** set `StringFormat="{x:Static l:Resources.Key}"`
  on the binding. For the composite labels (`Last sync: {0}`, `Local: {0} steps`,
  `PLC: {0} steps`, `{elapsed:0.0} s ago`) we still fold formatting into the source
  view-model property, because they also need Russian plural/word handling and the
  elapsed number must stay `InvariantCulture` while the words localize. Bind the property
  directly. Applies to `AppStatusBar` last-sync and `PlcConflictDialog` step counts.
- **Russian plurals**: `{0} steps`, `Error/Errors`, `Warning/Warnings` have no single
  correct Russian form (шаг/шага/шагов, ошибка/ошибки/ошибок). To avoid a pluralization
  engine (YAGNI), use plural-neutral phrasing: label-then-count, e.g. `Шагов: {0}`,
  `Ошибок: {0}`, `Предупреждений: {0}`. Record this wording choice in the resx.
- **Number/format culture**: keep `{elapsed:0.0}` and step counts formatted with
  `CultureInfo.InvariantCulture` explicitly; only surrounding words come from resx. This
  is consistent with "only UICulture changes".
- **Menu mnemonics**: `_File`, `E_xit`, etc. — assign Russian access keys in the `ru`
  values (e.g. `_Файл`, `В_ыход`, `_Открыть рецепт…`). Keep `InputGesture` bindings as-is.

## Progress Tracking

- Mark completed items `[x]` immediately. New tasks get `➕`; blockers get `⚠️`.

## What Goes Where

- Implementation Steps (checkboxes): config field, culture wiring, resx, extraction, tests.
- Post-Completion (no checkboxes): deploying `ui/app.yaml` into real `C:\DISTR\Config\...`
  equipment configs; visual proofreading of Russian wording by a native reviewer.

## Implementation Steps

### Task 1: Add `locale` to the equipment config (`ui/app.yaml`)

**Files:**
- Create: `SemiStep.Core/Configuration/Dto/AppUiOptionsDto.cs`
- Create: `SemiStep.Core/Configuration/AppUiOptions.cs`
- Create: `SemiStep.Core/Configuration/Loaders/AppUiOptionsLoader.cs`
- Create: `SemiStep.Core/Configuration/Mapping/AppUiOptionsMapper.cs`
- Modify: `SemiStep.Core/Configuration/AppConfiguration.cs`
- Modify: `SemiStep.Core/Configuration/Facade/ConfigFacade.cs` (deconstruction `:32`,
  `LoadedSections` record + ctor `:173-179`/`:115-121`, `MapToDomain` signature+call
  `:52`/`:126`, `AppUiOptionsLoader` call + `Result.Merge` in `LoadAllSectionsAsync`)
- Modify: all `new AppConfiguration(...)` call sites — 14 test files incl.
  `SemiStep.Tests/Helpers/TestRecipeMetadataRegistryFactory.cs`, S7 tests, Core unit/
  integration tests, `UI/GroupComboBoxRecyclingTests.cs`
- Modify: `SemiStep.Tests/YamlConfigs/Standard/ui/app.yaml` (create) and other test config
  roots as needed
- Create: `SemiStep.Tests/Core/Configuration/AppUiOptionsLoaderTests.cs`

- [x] add `AppUiOptionsDto` with `[YamlMember(Alias = "locale")] string? Locale`
- [x] add domain record `AppUiOptions(string Locale)` with `public static AppUiOptions Default { get; }` = `ru`
- [x] add `AppUiOptionsLoader.LoadAsync` adapting the `GridStyleLoader` pattern, reading
      `ui/app.yaml`. **Intentional deviation**: return `Ok(Default)` when the file/`ui` dir
      is absent (optional file), unlike `GridStyleLoader` which fails on absent — do not
      "fix" this back to fail-on-absent
- [x] add `AppUiOptionsMapper.Map` returning `Default` when DTO/locale is null or blank
- [x] thread `Ui` through `ConfigFacade`: deconstruction site, `LoadedSections` record and
      ctor, `MapToDomain` signature and call, the merge in `LoadAllSectionsAsync`
- [x] add `Ui` as the 7th param on `AppConfiguration`; update every `new AppConfiguration(`
      call site (helper factory + 13 test files) so the solution compiles
- [x] add `ui/app.yaml` (`locale: ru`) to the `Standard` test config; confirm other config
      roots still load (absent file -> default)
- [x] write tests: present `en`, present `ru`, absent file, blank/garbage value -> all
      resolve to expected locale (garbage -> `ru`)
- [x] run tests - must pass before next task

### Task 2: Select UI culture at startup

**Files:**
- Create: `SemiStep.UI/Localization/UiCultureSelector.cs`
- Modify: `SemiStep.UI/Program.cs`
- Create: `SemiStep.Tests/UI/Localization/UiCultureSelectorTests.cs`

Seam note: config is loaded on a worker thread (`Task.Run(() => StartupAsync(...))`) and
`Main` receives only a `StartupOutcome` exposing `Provider`, not the `AppConfiguration`.
So the locale-driven override happens **inside `StartupAsync`** after a successful load
(both `DefaultThreadCurrentUICulture` and `Resources.Culture` are process-global, so
setting them from the worker thread is fine). No early culture set-up is required for the
`ErrorWindow` path: that window is hardcoded English and does not depend on culture/resx.

- [x] add `UiCultureSelector.Resolve(string? locale) : CultureInfo` returning `ru` on
      null/blank/`CultureNotFoundException`. **Note**: ICU does not throw for structurally
      valid but unknown tags (e.g. `zz-ZZ-garbage`); it synthesises a custom culture with
      LCID `0x1000`. The helper also treats that LCID as invalid and falls back to `ru`.
- [x] in `StartupAsync`, after a successful load, set `DefaultThreadCurrentUICulture` from
      `UiCultureSelector.Resolve(config.Ui.Locale)`. **Deferred**: the `Resources.Culture`
      assignment is wired in Task 3, once the generated `Resources` class exists (a
      single clearly-marked note marks the seam in `Program.cs`).
- [x] set the same `ru` default once at process start as a harmless baseline (optional;
      not relied on by any pre-config UI)
- [x] do not touch `CurrentCulture` (keep number/date formatting and logs invariant)
- [x] write tests for `Resolve`: `en`, `ru`, null, blank, invalid tag
- [x] run tests - must pass before next task

### Task 3: Create the resx infrastructure

**Files:**
- Create: `SemiStep.UI/Localization/Resources.resx` (English neutral)
- Create: `SemiStep.UI/Localization/Resources.ru.resx` (Russian)
- Modify: `SemiStep.UI/SemiStep.UI.csproj` (strongly-typed accessor via MSBuild metadata)
- Create: `SemiStep.Tests/UI/Localization/ResourceResolutionTests.cs`

Mechanism: the strongly-typed accessor is a **committed** source file,
`SemiStep.UI/Localization/Resources.Designer.cs`, hand-authored in the standard
auto-generated shape (public partial class `Resources`, lazy `ResourceManager` on base
name `SemiStep.UI.Localization.Resources`, public static `Culture`, one static property per
key). It is a normal `.cs` file, so it is visible to both the C# compiler and XamlIl before
their passes; `{x:Static l:Resources.*}` resolves on a cold build. Build-time
`StronglyType*` generation was rejected: it produces the accessor too late for XamlIl,
causing cold-build `AVLN2000` and headless `[AvaloniaFact]` hangs (App.axaml fails to
precompile). The two resx files stay `EmbeddedResource` (SDK auto-include); `Resources.resx`
is the neutral English set and `Resources.ru.resx` compiles into the
`ru/Semistep.resources.dll` satellite.

- [x] add both resx files with one seed key pair to validate wiring (`TestGreeting`:
      en `Hello`, ru `Привет`)
- [x] commit a hand-authored `Localization/Resources.Designer.cs` (public partial class
      `Resources`) so the accessor is a source file reachable from XAML `{x:Static}` on a
      cold build. **Accessor mechanism used**: committed public `Resources.Designer.cs`.
      The earlier build-time `StronglyType*` metadata approach was reverted — it emitted
      the class after XamlIl needed it, producing 58 cold-build `AVLN2000` errors and
      `[AvaloniaFact]` hangs. Base name resolves to `SemiStep.UI.Localization.Resources`.
- [x] build to confirm the designer class and the `ru` satellite assembly are produced
      (`obj/.../Resources.Designer.cs`, `bin/.../ru/Semistep.resources.dll`)
- [x] write a smoke test: seed key resolves to the English value under `en` culture and
      the Russian value under `ru` culture (asserts the `ru` satellite is copied to test bin)
- [x] run tests - must pass before next task

### Task 4: Extract AXAML chrome literals

**Files:**
- Modify: `SemiStep.UI/MainWindow/RecipeMenuBar.axaml` (File/Edit/View/Help + items,
  incl. `_` mnemonics — give the `ru` values their own access keys)
- Modify: `SemiStep.UI/MainWindow/RecipeToolBar.axaml` (button text + tooltips; visible by
  default)
- Modify: `SemiStep.UI/MainWindow/MainWindow.axaml` (DataGrid context menu; Title stays a
  binding, localized in Task 5)
- Modify: `SemiStep.UI/MainWindow/AppStatusBar.axaml` (`Sync ON/OFF`, `Connecting`,
  `Шаг:`, `Рецепт:`, `FOR:`; last-sync via VM per StringFormat wrinkle)
- Modify: `SemiStep.UI/MessageService/MessagePanel.axaml` (`Close` tooltip)
- Modify: `SemiStep.UI/Dialogs/ErrorWindow.axaml` (translate the hardcoded Russian chrome
  — Title, header, `Выход` — to English; hardcoded, NOT via resx; see Overview rationale)
- Modify: `SemiStep.UI/Dialogs/MessageDialog.axaml` (`Message` title, `OK`)
- Modify: `SemiStep.UI/Plc/PlcConflictDialog.axaml` (title, **both** prompt lines — "The
  PLC contains a different recipe." and "Which version do you want to keep?" — buttons;
  step-count labels via VM per StringFormat wrinkle)
- Modify: `SemiStep.UI/ShutdownService/ExitConfirmationDialog.axaml` (title, message,
  `Save`/`Don't Save`/`Cancel`)
- Modify: `SemiStep.UI/Localization/Resources.resx`, `Resources.ru.resx` (add all keys)
- Create: `SemiStep.Tests/UI/Localization/ChromeLocalizationTests.cs`

- [x] add `xmlns:l="clr-namespace:SemiStep.UI.Localization"` and replace each literal with
      `{x:Static l:Resources.Key}` across the listed AXAML
- [x] give Russian menu items their own `_`-mnemonic letters
- [x] add every English neutral value and its Russian counterpart to both resx files
- [x] keep `InputGesture`/shortcuts and brand tokens (`SemiStep`) unlocalized
- [x] write an `[AvaloniaFact]` under culture `ru` asserting a menu header and a dialog
      button render their `ru` resource values
- [x] run tests - must pass before next task

### Task 5: Extract view-model literals and fold StringFormat prefixes

**Files:**
- Modify: `SemiStep.UI/MainWindow/MainWindowViewModel.cs` (`MapSyncStatus`,
  `FormatLastSyncTime` — `Never` **and** the `" s ago"` suffix, `BuildWindowTitle`
  `New Recipe`; expose fully localized `LastSyncTimeText`)
- Modify: `SemiStep.UI/Plc/PlcConflictDialogViewModel.cs` (expose localized step-count
  strings)
- Modify: `SemiStep.UI/MessageService/MessagePanelViewModel.cs` (`ErrorCountText`,
  `WarningCountText` — currently `$"{c} {(c==1?"Error":"Errors")}"`, lines 57/62)
- Modify: `SemiStep.UI/Localization/Resources.resx`, `Resources.ru.resx`
- Modify/Create: `SemiStep.Tests/UI/...` view-model tests

- [x] replace VM literals with `Resources.Key`; `MapSyncStatus` states, `Never`,
      `New Recipe`
- [x] localize `FormatLastSyncTime`: `Never` key + an elapsed format key; keep
      `{elapsed:0.0}` formatted with `InvariantCulture`, only the words from resx
- [x] fold `Last sync: {0}`, step counts, and Error/Warning counts into VM properties
      using plural-neutral Russian phrasing (`Шагов: {0}`, `Ошибок: {0}`,
      `Предупреждений: {0}`); format numbers with `InvariantCulture`
- [x] add the corresponding keys to both resx files
- [x] write/adjust tests asserting these VM properties return the `ru` text under `ru`
      (incl. count strings at 0/1/many)
- [x] run tests - must pass before next task

### Task 6: Confirm logs and diagnostics stay English

**Files:**
- Modify (if needed): none expected
- Reference: `SemiStep.UI/Program.cs`, `MainWindowViewModel.cs` `ReportError` sites

- [x] grep for `Log.` / `_logger.` templates and confirm none are routed through resources
      — `grep -rnE "Log(ger)?\.|_logger\.|LogInformation|LogWarning|LogError|ForContext"
      SemiStep.UI SemiStep.Core --include=*.cs | grep -i "Resources\."` returns nothing.
      All Serilog templates (e.g. `Program.cs:81` `"Application startup failed: ..."`) are
      hardcoded English literals.
- [x] decision: notification-panel `ReportError` diagnostic strings (e.g. `Sync toggle
      failed: {ex.Message}`, `Failed to show PLC conflict dialog`, `Style editor failed:
      {ex.Message}`) stay **English** — they carry exception text and are diagnostic, not
      chrome. Verified: all `ReportError` sites (MainWindowViewModel, ClipboardViewModel,
      RecipeFileViewModel, RecipeGridViewModel) use English interpolated literals, none
      route through `Resources`. Revisit only if the user wants the log panel localized.
- [x] confirm `CurrentCulture` untouched so `{elapsed:0.0}` and log timestamps stay
      invariant — `grep -rn "CurrentCulture" SemiStep.UI` shows only a `Program.cs` comment,
      the `DefaultThreadCurrentUICulture` assignment (`Program.cs:90`, UI culture only), and a
      read of `CultureInfo.CurrentCulture` for text measurement (`ColumnWidthCalculator.cs:230`).
      No `CurrentCulture` or `DefaultThreadCurrentCulture` is ever assigned; only `*UICulture`
      and `Resources.Culture` are set. `ErrorWindow.axaml` is hardcoded English (no `xmlns:l`,
      no `{x:Static}`).

### Task 7: Verify acceptance criteria

- [x] main-window chrome (menu, toolbar, status bar incl. Error/Warning counts, context
      menu, dialogs) renders in Russian by default (`ui/app.yaml` absent or `locale: ru`);
      the settings editor + restart prompt are the known, documented English exclusions —
      verified via ChromeLocalizationTests headless [AvaloniaFact] under ru culture (full
      manual app run not automatable here)
- [x] switching `ui/app.yaml` to `locale: en` renders English chrome — covered by resx
      neutral-English fallback + UiCultureSelector tests (manual app run not automatable here)
- [x] config-load-failure `ErrorWindow` renders in English — ErrorWindow is hardcoded
      English (verified Task 6)
- [x] run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 0 failed,
      1039 passed, 0 skipped (41 s, --blame-hang 60000ms)
- [x] run `dotnet format SemiStep/SemiStep.slnx` — no changes (already formatted)
- [x] build entry exe: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` — 0 AVLN2000,
      0 errors, build succeeded

### Task 8: Documentation

**Files:**
- Create: `Docs/architecture/ui-localization.md`
- Modify (if a config reference exists): equipment-config docs mentioning the `ui/` folder

- [x] document the `ui/app.yaml` `locale` key, the en-neutral/ru model, default `ru`, and
      the "logs and CurrentCulture stay English" rule
- [x] moved to `Docs/plans/completed/` after review + finalize phases

## Post-Completion

**Manual verification**:
- Native-speaker proofread of all Russian resource values for wording/context.
- Smoke-run the packaged app against a real `C:\DISTR\Config\Semistep\MBE` config.

**External system updates**:
- Add `ui/app.yaml` (with the intended `locale`) to each deployed equipment config root;
  absent file falls back to `ru`, so this is optional per site.
