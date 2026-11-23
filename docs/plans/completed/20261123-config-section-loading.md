# Config section loading: one rule, one loader

## Behavior
- Today four section folders (`actions/`, `columns/`, `groups/`, `properties/`) read every `*.yaml`
  file and merge them. Each of the four already rejects a key repeated across files, with its own
  check and its own message, and the message names only the file where the repeat was found. A key
  repeated exactly inside one file is dropped silently: the last value wins. Three named files
  (`connection/connection.yaml`, `ui/grid_style.yaml`, `ui/app.yaml`) are read by name, and every
  other file beside them is ignored without a word. A file with the `.yml` extension is ignored
  everywhere, also without a word.
- After this change every YAML file in the six section folders is either read or reported:
  - merge sections keep reading every `.yaml` file, now in ordinal name order. One shared check
    rejects a key that appears twice after case folding, in two files or inside one file. Its
    typed error names the key, the file where the key was first seen and the file where it repeats
    (the same file for a repeat inside one file). Each section keeps its own key comparison:
    case-insensitive for property ids, column keys and group ids, numeric for action ids;
  - a key repeated exactly inside any YAML file stops startup as a parse error of that file, and
    the message names the key;
  - `connection/` accepts only `connection.yaml`, and `ui/` accepts only `grid_style.yaml` and
    `app.yaml`. Any other `.yaml` or `.yml` file there stops startup, naming the file and the
    accepted names;
  - a `.yml` file in a merge folder stops startup, naming the file and the required `.yaml`
    extension.
- Every reported file appears once, and the other files are still read, so the startup error
  window lists every problem of the loading step. The content checks (cross-references, default
  values, PLC layout) run only after loading succeeds, as today. SemiStep and the PLC emulator
  both load through `ConfigFacade.LoadAndValidateAsync`; the emulator shows the error messages as
  text.
- The four merge loaders share one load skeleton, and all loaders share one YAML deserializer.
- Scope guard: the content checks of each section and their messages stay as they are; only the
  duplicate-key check moves into the shared skeleton. The positive action-id check stays in the
  actions loader. `ui/app.yaml` stays optional; making it required is #185. The grid-style editor
  keeps reading and writing `ui/grid_style.yaml` and does not scan the folder; it also gets the
  exact-repeat parse error, because it shares the deserializer.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Each shipped preset (MBE, MOCVD, RIE) | Loads as today | agent: no preset has an extra file | A2 |
| A merge folder holds two `.yaml` files with different keys | Merges both, files read in ordinal name order | issue #188 | A1 |
| A merge folder holds one key in two files | Startup fails; the typed duplicate-key error names the key, the first file in ordinal order and the repeat file | issue #188 | A1 |
| A merge folder holds two keys that differ only in case (`Int`, `int`) in one file | Startup fails with the typed duplicate-key error; both file names are that file | agent: keeps today's case-insensitive check, which also protects `ConfigFacade.MapToDomain` | A1 |
| Any YAML file holds one key twice exactly | Startup fails; the parse error of that file names the key | agent: probed, see Caveats | A1 |
| A merge folder has no `.yaml` file, or does not exist | Startup fails as today ("section missing") | existing behavior; existing standalone cases | A1 |
| A merge folder holds only `a.yml` | Startup fails with both the `.yml` error and "section missing" | agent: every loading problem is listed | A1 |
| `connection/` or `ui/` holds a `.yml` file, for example only `connection.yml` | The unexpected-file error, plus "required config not found" when the named file is absent | agent: every loading problem is listed | A1 |
| `connection/` holds `connection_test.yaml` beside `connection.yaml` | Startup fails; the typed unexpected-file error names the extra file and `connection.yaml` | agent: see Decisions | A1 |
| `ui/` holds `extra.yaml` beside the two accepted files | Startup fails; the error names `extra.yaml` and the two accepted names, exactly once | agent: see Decisions | A1 |
| An accepted name differs only in case (`CONNECTION.YAML`) | Accepted and read | agent: Windows file names are case-insensitive; see Caveats | A1 |
| `ui/app.yaml` is absent | Defaults apply, as today | scope guard (#185) | A1 (existing `AppUiOptionsLoaderTests`) |
| A non-YAML file sits in a section folder (`.tmp`, `.bak`, `.md`, `.yaml2`) | Neither read nor reported | agent: the grid-style writer's temp file is `.grid_style.<guid>.tmp` | A1 |
| A subfolder sits in a section folder | Ignored, as today | agent: out of scope | none |
| The style editor opens while `ui/` holds an extra file | Opens as today; the folder check runs only at startup | agent: the style editor localizes its errors by type, the new errors are startup-only | A1 (`GridStyleEditorFacade` level) |

## Decisions
- `connection/` and `ui/` report foreign files instead of merging them: the grid-style editor
  writes back exactly one file (`GridStyleWriter.SaveAsync`), so a grid style split over two files
  could not be saved without losing one; and two connection files are almost always a live file
  plus a variant, which a merge would turn into a silent mix. Rejected: merge every folder (the
  literal wording of #188), because of the writer. The one rule is: no YAML file in a section
  folder is silent.
- One folder policy owns the rule. It knows the six folders, which of them merge, and the accepted
  names of `connection/` and `ui/`, and it decides which extension counts as YAML. A scan step
  that `ConfigFacade.LoadAllSectionsAsync` runs once per folder reports the files the policy
  rejects; the merge skeleton asks the same policy which files to read. Its errors join the merged
  loading result. Rejected: checks inside the loaders, because two loaders read `ui/` and would
  report each extra file twice, and `GridStyleLoader` also serves the style editor.
- The duplicate-key check moves into the shared merge skeleton and replaces the four per-loader
  checks. The skeleton is generic over the section's DTO and takes the section's key selector, key
  comparer and mapping; the groups loader keeps dropping empty-valued items, and the actions loader
  keeps its positive-id check. The three tests that assert the old duplicate wording
  (`ActionErrorTests.DuplicateActionId_HasError`, `PropertyErrorTests.DuplicatePropertyId_HasError`,
  `NestedActionReferenceTests.DuplicateSubactionId_FailsWithClearMessage`) move to type assertions.
  Rejected: keep the four checks and add the second file name to each, because the skeleton exists
  to hold this rule once.
- The shared deserializer enables YamlDotNet duplicate-key checking, so an exact repeat inside one
  file fails as a parse error naming the key instead of being dropped. This applies to all seven
  readers, the style editor's grid-style read included.
- Ordinal file order: the order, and the file a duplicate is blamed on, must not depend on the
  machine's culture.
- The two new error types are `internal sealed`. They appear only on the English startup path, so
  they need no localization, and `CoreErrorLocalizationCoverageTests` (it checks visible types
  only) does not apply. Tests reach them through `InternalsVisibleTo`.
- One shared deserializer instance for all seven readers (#119): all seven are built with the same
  options today. No per-section builder options.

## Caveats
- YamlDotNet and a repeated key. probed: a scratch console project with YamlDotNet 18.1.0 and the
  loaders' builder options, 2026-11-23. `Deserialize<Dictionary<string,string>>("int: a\nint: b\nInt: c\n")`
  returns `int=b, Int=c` without an error; `Deserialize<Dictionary<int,string>>("10: a\n10: b\n")`
  returns `10=b`. With `WithDuplicateKeyChecking()` both throw `YamlException: Encountered duplicate
  key ...`. A scan of the 224 tracked `*.yaml` files found no exact repeat, so no preset or fixture
  breaks. ASSUMPTION: the option also rejects a repeated property key in a DTO mapping such as
  `connection.yaml`; fallback: state in the docs that only section maps are checked; checked by: a
  Task 2 test with a repeated key in `connection.yaml`.
- File order. ASSUMPTION: today's `OrderBy(f => f)` sorts with the culture-sensitive default
  comparer, which puts `a.yaml` before `B.yaml`, while ordinal order puts `B.yaml` (0x42) first;
  fallback: none needed, the new order is set explicitly; checked by: the Task 2 test with
  `B.yaml` and `a.yaml`.
- Name case. ASSUMPTION: NTFS opens `CONNECTION.YAML` for the path `connection/connection.yaml`;
  fallback: the scan accepts the name case-insensitively and the loader opens the name the scan
  found; checked by: the Task 3 test. The test renames the file in the temp copy at run time
  (delete, then write): a committed fixture cannot hold both cases, and writing to the uppercase
  name over an existing lowercase file keeps the lowercase name.
- `.gitignore` excludes `*.tmp`. Tests for non-YAML files create those files in the temp copy at
  run time. `.yml` fixtures can be committed.
- The invalid-config cases copy `SemiStep.Tests/YamlConfigs/Standard/` and overlay one case
  folder. A case that needs a file absent uses `ConfigTestHelper.LoadStandaloneCaseAsync`.

## Constraints and non-goals
- No change to the content checks other than the duplicate rule, to the DTOs, `AppConfiguration`,
  the grid-style writer or the presets' YAML. The duplicate-action check in
  `ConfigFacade.MapToDomain` stays as a second guard.
- No installer change and no upgrade step: the product is below 1.0 and has no installations to
  migrate.
- Subfolders of section folders stay ignored. No hot reload. The emulator keeps showing errors as
  joined text.
- Threat model: a config folder edited by hand. Hostile input is out of scope.
- Tests stay simple: one test per Flow branch, typed assertions for the new errors.

## Gates
- none

## Acceptance
1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` → 0 failed;
   tests cover every Flow branch marked A1.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ShippedPresetLoadTests"`
   → 3 passed (MBE, MOCVD and RIE loaded through `ConfigFacade.LoadAndValidateAsync`).
3. `dotnet build SemiStep.slnx` → 0 warnings; `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
4. `git grep --untracked -n "new DeserializerBuilder" -- SemiStep/SemiStep.Core` → exactly one line.

---

## Context
- Loaders: `SemiStep.Core/Configuration/Loaders/` (`ActionsSectionLoader`, `ColumnsSectionLoader`,
  `GroupsSectionLoader`, `PropertiesSectionLoader` merge; `ConnectionLoader`, `GridStyleLoader`,
  `AppUiOptionsLoader` read one file). Entry: `ConfigFacade.LoadAllSectionsAsync`. Domain mapping:
  `ConfigFacade.MapToDomain`. Stale comment to update: `CrossReferenceValidator` says duplicate
  action ids are rejected by `ActionsSectionLoader`.
- Error reporting rules: `docs/architecture/error-reporting.md`. Localization coverage:
  `SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs`. Startup error window:
  `SemiStep.UI/Program.cs` sends error messages to `App.RunErrorWindow`.
- Config tests: `SemiStep.Tests/Config/` with `ConfigTestHelper`, `TestDataCopier`, the overlay
  cases in `SemiStep.Tests/YamlConfigs/Invalid/` and standalone cases. Shipped presets:
  `SemiStep.Tests/Helpers/ShippedConfigLocator.cs`. Style editor facade tests:
  `SemiStep.Tests/Core/Configuration/GridStyleEditorFacadeTests.cs` (`Component=Config`).
- Docs that describe the folders: `docs/readme.md` section 6 (Russian),
  `docs/architecture/grid-style-configuration.md` (the "globbing" paragraph).

## Tasks

### Task 1: Preset guard, shared merge skeleton and shared deserializer

**Goal:** a test loads all three presets fully, then the four merge loaders share one skeleton and all readers share one deserializer, with no change in behavior
**Entry:** `PropertiesSectionLoader.LoadAsync`
**Likely touches:** a new `ShippedPresetLoadTests`, the seven loaders, one shared skeleton type, one shared deserializer holder

- [x] the `ShippedPresetLoadTests` theory (`Component=Config`) loads MBE, MOCVD and RIE through `ConfigFacade.LoadAndValidateAsync`, before any refactor
- [x] one shared deserializer used by all seven readers, with today's options
- [x] one generic merge skeleton used by the four merge loaders; each keeps its own key, mapping and validation; the per-loader duplicate checks stay in this task
- [x] tests: the existing config tests and the new theory pass unchanged
- [x] run tests - must pass before the next task

### Task 2: Ordinal order, one duplicate-key check, exact-repeat parse error

**Goal:** merge folders read files in ordinal order, one typed check reports a repeated key with both file names, and an exact repeat inside any YAML file fails as a parse error
**Entry:** the shared merge skeleton and the shared deserializer from Task 1
**Likely touches:** the skeleton, the shared deserializer, the four merge loaders, a new internal duplicate-key error, the three duplicate tests, `CrossReferenceValidator` (comment), new overlay cases

- [x] files enumerate in ordinal name order
- [x] the skeleton reports a repeat (across files or inside one file, under the section's comparer) with the internal typed duplicate-key error: key, first file, repeat file; the four per-loader duplicate checks are removed; the positive action-id check and the groups filtering stay
- [x] the shared deserializer turns on duplicate-key checking
- [x] the three tests asserting the old duplicate wording assert the type and the named files; the `CrossReferenceValidator` comment names the new owner
- [x] tests: two files merge; key in two files; case-only repeat in one file; exact repeat in a merge file and in `connection.yaml` (the Caveat); ordinal order through `B.yaml` and `a.yaml` (the duplicate error names `B.yaml` as the first file)
- [x] run tests - must pass before the next task

### Task 3: Folder policy and startup scan

**Goal:** one folder policy decides which files each section folder reads and rejects, and one startup scan reports the rejected files
**Entry:** `ConfigFacade.LoadAllSectionsAsync`
**Likely touches:** `ConfigFacade`, one new folder policy type, the merge skeleton (asks the policy), a new internal unexpected-file error, standalone and overlay cases

- [x] the policy covers the six folders; the scan visits each existing folder once, skips a missing folder (the loaders report it), and reports every rejected file once with the internal typed unexpected-file error naming the file and the accepted names; its errors join the loaders' results
- [x] the merge skeleton reads the files the policy accepts, so `.yaml2` is neither read nor reported
- [x] tests: extra `.yaml` in `connection/`; extra file in `ui/` gives exactly one error; `.yml` in `connection/` and in `ui/`; only `a.yml` in a merge folder gives both errors; `CONNECTION.YAML` renamed at run time is accepted; `.tmp`, `.bak`, `.yaml2` created at run time are ignored; a missing `groups/` gives only the loader's error; `GridStyleEditorFacade` still loads with an extra file in `ui/`
- [x] run tests - must pass before the next task

### Task 4: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 5: [Final] Update documentation

**Goal:** the docs state the rule for files in section folders and the new typed errors
**Entry:** `docs/readme.md` section 6

- [x] `docs/readme.md` (Russian) and `docs/architecture/grid-style-configuration.md` state the rule: merge folders read every `.yaml` file in ordinal order and reject a repeated key; `connection/` and `ui/` accept only their named files; any other YAML file stops startup; a key repeated inside one file stops startup
- [x] `docs/architecture/error-reporting.md`: the sentence that config loader errors have no typed surface now names the two typed startup errors, and that they reach the startup window as English text
- [x] CLAUDE.md only if a convention changed (no convention changed, no edit)

## Post-Completion
- the operator's manual test: start SemiStep with each preset; add `ui/extra.yaml` and see the startup error window name it
- closes #188 and #119

**Executed by exec:**
- branch: config-section-loading

## Verify it yourself
1. Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Config"` → 291 passed.
   The new behavior lives in `ConfigDuplicateKeyTests`, `ConfigFolderPolicyTests`, `ShippedPresetLoadTests`
   and `GridStyleEditorFacadeTests.Load_WithExtraFileInUiFolder_StillLoads`.
2. Copy `ConfigFiles/MBE` to a scratch folder, add `ui/extra.yaml` with any content, and start SemiStep with
   `--config-dir <scratch>` → the startup error window names `extra.yaml` and the accepted names
   `grid_style.yaml, app.yaml`. On master the file is ignored and SemiStep starts.
3. In the same copy, remove `ui/extra.yaml` and copy `actions/service.yaml` to `actions/service_copy.yaml` →
   the window lists every repeated action id with both file names. On master the message names only the
   second file.
4. In the same copy, delete `actions/service_copy.yaml` and repeat one key exactly inside `ui/grid_style.yaml`
   → the window shows "Failed to load grid_style.yaml:" followed by the parser text naming the key. On master
   the repeat is dropped silently and the last value wins.
5. A group key with no items (`mygroup:` alone in a `groups/` file) now reports "Key 'mygroup' has no value"
   instead of a parse error; this came from review and is covered by
   `LoaderFailLoudTests.GroupsLoader_WithEmptyGroupBody_FailsNamingTheGroup`.
