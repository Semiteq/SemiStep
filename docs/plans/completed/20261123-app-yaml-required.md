# ui/app.yaml is delivered and required

## Behavior
- Today no preset ships `ui/app.yaml`, so the installer never delivers it. `AppUiOptionsLoader`
  treats a missing file as "use defaults", `AppUiOptionsMapper` turns a missing or blank `locale`
  into `AppUiOptions.Default` (`ru`), and `UiCultureSelector` turns an unknown culture into `ru`.
  The `locale` setting exists and is documented, but on an installed machine the UI language is
  always the code default, and a typo such as `locale: eng` or `locle: en` is silent.
- After this change every preset (MBE, MOCVD, RIE) ships `ui/app.yaml` with `locale: ru`, and the
  file is required like `ui/grid_style.yaml`. The loading step reports, together with every other
  loading problem:
  - a missing `ui/app.yaml`, naming the file;
  - a missing, null or blank `locale` key (also an empty file or a misspelled key), naming the key;
  - a `locale` other than the two shipped languages, `ru` and `en`, naming the value and the
    accepted values.
- No production code path holds a default locale any more: `AppUiOptions.Default`,
  `AppUiOptions.DefaultLocale`, the optional branch in `AppUiOptionsLoader`, the mapper's fallback,
  `UiCultureSelector` and the pre-config culture line in `Program.Main` are removed. `Program`
  sets the UI culture from the validated `Ui.Locale`.
- The shipped-preset load test (`ShippedPresetLoadTests`) fails if a preset lacks a file the
  loaders require.
- Scope guard: the grid-style file, its editor and the other sections stay as they are. The
  window-geometry question in #185 is out of scope. The startup `ErrorWindow` text is hard-coded
  English and does not depend on the culture.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Each shipped preset | Loads with locale `ru` | issue #185 | A2 |
| `ui/app.yaml` missing | Startup fails; typed error names `ui/app.yaml` | issue #185 | A1 |
| `locale` key missing, null or blank; empty file; misspelled key (`locle: en`) | Startup fails; typed error names `locale` | issue #185 | A1 |
| `locale: en` | Loads; the UI culture is English | existing behavior | A1 (Ui.Locale), A3 |
| `locale: RU` or ` ru ` | Loads as `ru` (trimmed, case-insensitive) | agent: matches today's trim, avoids a failure on case | A1 |
| `locale: de` or `locale: eng` | Startup fails; typed error names the value and `ru, en` | agent: only two languages ship; an unshipped culture would silently show the English neutral resources | A1 |
| `app.yaml` unparsable or with a repeated key | Startup fails with the existing parse error naming the file | existing behavior (#188 change) | A1 |
| `app.yaml` problem together with another loading problem | Both are listed | agent: all three checks run in the loading step | A1 |
| SemiStep starts before the config is read (the startup error window) | The window shows its English text; no culture is set before config | agent: `ErrorWindow` text is hard-coded English | A3 |

## Decisions
- Allowed locales are exactly `ru` and `en`, compared case-insensitively after trimming: the UI
  ships those two resource sets (`Resources.resx` English neutral, `Resources.ru.resx`). Rejected:
  accept any valid .NET culture, because `de` would load and show English text without a word.
- All three checks run in the loading step (`AppUiOptionsLoader`), so they surface together with
  the other loading errors and stop SemiStep and the PLC emulator alike. The accepted-locale list
  lives only in Core. Rejected: checks in the mapping step, because that runs after the section
  validators and would hide an `app.yaml` problem behind a cross-reference error.
- `UiCultureSelector` is deleted: once Core returns `ru` or `en`, its job is one
  `CultureInfo.GetCultureInfo` call, which `Program` makes directly. The pre-config
  `Resolve(null)` line in `Program.Main` is deleted with it; the only pre-config UI, `ErrorWindow`,
  has hard-coded English text.
- The three new errors are `internal sealed` startup errors, English, like the #188 errors: the
  style editor never loads `app.yaml`, so nothing localizes them, and
  `CoreErrorLocalizationCoverageTests` (visible types only) does not apply.
- The presets set `locale: ru`, the language the UI runs in today on every machine.
- Tests that construct `AppUiOptions.Default` today get an explicit `new AppUiOptions("ru")` (or a
  shared test constant not named `Default` or `DefaultLocale`).

## Caveats
- A missing `app.yaml` is a loading error, and `ConfigFacade.LoadAndValidateAsync` returns right
  after a loading failure, before grid-style mapping, cross-references, default values, domain
  mapping and PLC validation. A fixture without `app.yaml` therefore loses every later error it was
  built to produce. Every fixture folder that loads through `ConfigFacade` gets `ui/app.yaml`:
  `SemiStep.Tests/YamlConfigs/WideParams`, `YamlConfigs/WithGroups`, the 19 folders under
  `YamlConfigs/Standalone/`, and test code that writes a config folder at run time. `Standard`
  already has it, so overlay cases are covered.
- `YamlConfigDeserializer` ignores unmatched properties, so `locle: en` deserializes to a null
  `locale`; the loader's missing-key check catches it.

## Constraints and non-goals
- No change to the installer: it copies every file under each preset, so the new file is
  delivered. The product is below 1.0 with no installations to migrate.
- No new UI language, no change to the resx files.
- Threat model: a config file edited by hand. Hostile input is out of scope.
- Tests stay simple: one test per Flow branch, typed assertions on the new errors and their
  properties.

## Gates
- none

## Acceptance
1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` → 0 failed;
   tests cover every Flow branch marked A1 through `ConfigFacade.LoadAndValidateAsync`.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ShippedPresetLoadTests"`
   → 3 passed, each preset loading with locale `ru`.
3. `dotnet build SemiStep.slnx` → 0 warnings; `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
4. `git grep -I -n -e "DefaultLocale" -e "AppUiOptions.Default" -e "UiCultureSelector" -- 'SemiStep/*.cs'` → no output.
5. `ls ConfigFiles/MBE/ui/app.yaml ConfigFiles/MOCVD/ui/app.yaml ConfigFiles/RIE/ui/app.yaml` → all three exist.

---

## Context
- `SemiStep.Core/Configuration/Loaders/AppUiOptionsLoader.cs`, `Mapping/AppUiOptionsMapper.cs`,
  `AppUiOptions.cs`, `Dto/AppUiOptionsDto.cs`; `ConfigFacade` maps it onto `AppConfiguration.Ui`.
- `SemiStep.UI/Localization/UiCultureSelector.cs` and its test `UiCultureSelectorTests`;
  `SemiStep.UI/Program.cs` (the pre-config `Resolve(null)` line and the post-config culture set).
- Typed startup errors from #188: `Loaders/DuplicateKeyError.cs`, `Loaders/UnexpectedFileError.cs`
  (shape to follow). Folder and file names: `Loaders/ConfigFolderPolicy.cs` constants.
- Tests: `SemiStep.Tests/Core/Configuration/AppUiOptionsLoaderTests.cs` (its absent-file, blank and
  `not-a-culture` cases become the new error cases), `ShippedPresetLoadTests`, the 16 test files
  that use `AppUiOptions.Default` (`grep -rl "AppUiOptions.Default" SemiStep/SemiStep.Tests`).
- Docs: `docs/architecture/ui-localization.md` ("The locale setting" and "Culture wiring"),
  `docs/architecture/error-reporting.md` (the `ui/app.yaml` loader line and the typed-errors
  paragraph), `docs/architecture/grid-style-configuration.md` (the folder-policy paragraph),
  `docs/readme.md` sections 2.2 and 6 (6.9 marks `locale` as optional).

## Tasks

### Task 1: Ship app.yaml, require it, remove every default locale

**Goal:** the presets carry `ui/app.yaml`; a missing file, key or unsupported value stops startup in the loading step; no default locale or culture fallback remains
**Entry:** `AppUiOptionsLoader.LoadAsync`
**Likely touches:** `ConfigFiles/{MBE,MOCVD,RIE}/ui/app.yaml`, the loader, the mapper, `AppUiOptions`, three new internal error types, `SemiStep.UI/Program.cs`, `UiCultureSelector` and its test (deleted), 21 fixture folders, 16 test files using `AppUiOptions.Default`

- [x] add `ui/app.yaml` with `locale: ru` to MBE, MOCVD and RIE, and to every fixture folder listed in the Caveats
- [x] the loader reports a missing file, a missing or blank key, and a value other than `ru`/`en` with their internal typed errors (file, key, value plus accepted values); `ru`/`en` are trimmed and case-insensitive; the optional branch, its comment and the mapper fallback are gone
- [x] `AppUiOptions.Default`, `DefaultLocale` and `UiCultureSelector` are deleted; `Program` sets the culture from `Ui.Locale` and the pre-config line goes; test call sites use an explicit locale
- [x] tests through `ConfigFacade.LoadAndValidateAsync`: one per A1 Flow branch, asserting the error type and its named file, key or value; `ShippedPresetLoadTests` asserts locale `ru`
- [x] run tests - must pass before the next task

### Task 2: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 3: [Final] Update documentation

**Goal:** the docs state that `ui/app.yaml` is delivered and required, and list the accepted locales
**Entry:** `docs/architecture/ui-localization.md`

- [x] `ui-localization.md`: "The locale setting" (required file, `ru` and `en`, no fallback) and "Culture wiring" (no selector, no pre-config culture)
- [x] `error-reporting.md`: the `ui/app.yaml` loader line and the typed-errors paragraph (five typed startup errors)
- [x] `grid-style-configuration.md` (folder-policy paragraph) records the rule: every named file a loader requires (`connection.yaml`, `grid_style.yaml`, `app.yaml`) is part of the delivery and its absence stops startup; `docs/readme.md` sections 2.2 and 6 (Russian, 6.9 marks `locale` required) point to it
- [x] CLAUDE.md only if a convention changed (no convention changed; no edit)

## Post-Completion
- the operator's manual test: start SemiStep with each preset (Russian UI); set `locale: en` in a scratch copy and see the English UI; delete `ui/app.yaml` in the copy and see the startup error window name it
- closes #185

**Executed by exec:**
- branch: app-yaml-required

## Verify it yourself
1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` → 301 passed; the
   new cases are in `Core/Configuration/AppUiOptionsLoaderTests.cs`, and `ShippedPresetLoadTests` asserts `ru`.
2. Start SemiStep with each preset → the UI is Russian, as before.
3. Copy `ConfigFiles/MBE` to a scratch folder, set `locale: en` in `ui/app.yaml`, start with
   `--config-dir <scratch>` → the UI is English.
4. In the copy, set `locale: eng` → the startup error window names `eng` and the accepted values `ru, en`.
   On master the typo is ignored and the UI starts in Russian.
5. In the copy, delete `ui/app.yaml` → the startup error window names the missing file. On master SemiStep
   starts in Russian without a word.
