# Build hygiene: Release symbols, warnings-as-errors, InternalsVisibleTo, name casing

Closes #121.

## Overview

Five build-infrastructure changes. None changes application behaviour; all are cheap now and
progressively more expensive later.

- **Release ships no symbols.** `SemiStep/Directory.Build.props:17-19` sets `DebugSymbols=False`
  and `DebugType=None` for Release. To be precise about what is and is not lost: the log is **not**
  empty. `Program.cs:120` renders `{Exception}`, and exceptions are passed as objects rather than
  strings — `Program.cs:52` and `:68` at the top-level catches, `S7Service.cs:194`,
  `PlcSyncExecutor.cs:153`, `PlcLifecycleManager.cs:280,385`, `RecipeCoordinator.cs:489` below. So
  the type, the message and the full frame chain with method names all arrive today; method names
  live in assembly metadata and need no PDB. What is missing is the ` in <file>:line <n>` suffix on
  every frame, which is exactly what a PDB supplies. On a twenty-frame trace through the sync
  executor, knowing the method is often not enough to know which of its branches threw.
- **Nothing enforces the current zero-warning state.** Verified 2026-07-21:
  `dotnet build SemiStep.slnx -c Release` reports `Предупреждений: 0`. There is no
  `TreatWarningsAsErrors` or `EnforceCodeStyleInBuild` anywhere in the props or the csproj files.
  A clean build is the cheapest possible moment to lock the invariant in.
- **`InternalsVisibleTo` is declared two different ways.** `SemiStep.Core.csproj:15` uses the SDK
  item; `SemiStep.UI.csproj:9-13` uses a raw `AssemblyAttribute`. Same intent, two spellings.
- **The assembly is named `Semistep` while everything else is `SemiStep`.**
  `SemiStep.UI.csproj:5`, and the build output confirms it:
  `Artifacts\bin\SemiStep.UI\release\Semistep.dll`. The name is load-bearing in XAML: eleven
  `avares://Semistep/...` URIs and one `assembly=Semistep` reference resolve against it —
  twelve lines, counted.
- **The deployed `C:\DISTR` paths carry the same wrong casing**, in both the configuration root
  and the log root. Task 5.

Benefits: readable production stack traces, a warning floor that cannot silently rise, style rules
enforced by something that travels with the repository, one way to declare test visibility, and a
product whose executable and deployed directories match its name.

## Context (from discovery)

Every claim below was read during this planning session.

- **`SemiStep/Directory.Build.props`** — repo-wide MSBuild defaults. Note the path: it sits under
  `SemiStep/`, not at the repository root, so it governs the three projects and nothing else.
  `:17-19` is the Release block being changed.
- **`SemiStep/SemiStep.UI/SemiStep.UI.csproj`** — `:5` `<AssemblyName>Semistep</AssemblyName>`;
  `:9-13` the raw `AssemblyAttribute` form of `InternalsVisibleTo`.
- **`SemiStep/SemiStep.Core/SemiStep.Core.csproj:15`** — `<InternalsVisibleTo Include="SemiStep.Tests"/>`,
  the SDK item, the form to standardise on.
- **XAML references keyed on the assembly name** — `App.axaml:9,67,68`;
  `MainWindow/RecipeToolBar.axaml:40,50,62,72,82,94,104`;
  `RecipeGrid/Transposed/TransposedRecipeGridView.axaml:13`, all `avares://Semistep/...`; and
  `Styles/DataGridStyles.axaml:3`, `clr-namespace:SemiStep.UI.RecipeGrid;assembly=Semistep`.
  `Docs/architecture/ui-localization.md:39-40` documents the satellite as
  `ru/Semistep.resources.dll` and states "the entry assembly name is `Semistep`".
- **`SemiStep/SemiStep.Tests/TestAppBuilder.cs:18`** — `AppBuilder.Configure<App>()`. The headless
  suite boots the real `App`, so every `AvaloniaFact` loads `avares://Semistep/Styles/ColorPalette.axaml`
  through `App.axaml:9`. That makes the suite the real assertion for Task 4, not a formality.
- **`Installer/SemiStep.iss:11`** — `#define AppExeName "Semistep.exe"`. `:15` points `SrcBinDir`
  at `..\SemiStep\Artifacts\publish\SemiStep.UI\release_win-x64`.
- **`Installer/SemiStep.iss` `[Files]`** — the binaries line is
  `Source: "{#SrcBinDir}\*"; Flags: ignoreversion recursesubdirs createallsubdirs`, and a grep for
  `pdb` and `Excludes` across the file returns nothing. **The PDB therefore ships automatically
  once it exists; no installer change is needed for that half of the work.**
- **`Installer/SemiStep.iss` `[InstallDelete]` at `:71-79`** — six entries removing the legacy flat
  config layout (`actions`, `columns`, `connection`, `groups`, `properties`, `ui`) from
  `C:\DISTR\Config\Semistep\`, per the comment above them. Note the section: `[InstallDelete]`, run
  at install time, not `[UninstallDelete]`. Uninstall has never removed configuration, a deliberate
  decision recorded in `Docs/plans/completed/20260518-startup-options-installer-and-dev-runners.md:220`.
- **`.zed/debug.json:6,23,40`** — three `program` entries pointing at
  `Artifacts/bin/SemiStep.UI/debug/Semistep.dll`.
- **`.github/workflows/ci.yml`** — restore, build, test. **No formatting or style check.** Its
  `paths` filter is `SemiStep/**`, `ConfigFiles/**`, `global.json`, so edits confined to
  `Installer/` or `.zed/` do not trigger it at all.
- **`.git/hooks/pre-commit`** — runs `dotnet format SemiStep.slnx --verify-no-changes`. Not
  versioned: a fresh clone has no hook. Style is therefore enforced today only on machines where
  someone installed it by hand.
- **`global.json`** — `"version": "10.0.100"`, `"rollForward": "latestFeature"`. The SDK floats
  across feature bands within 10.0; the installed one is 10.0.302.
- **`.github/workflows/release.yml:57`** — `dotnet publish SemiStep/SemiStep.UI/SemiStep.UI.csproj -c Release -p:Version=...`;
  `:71` — `iscc.exe /DAppVersion=... Installer/SemiStep.iss`. Neither names the assembly, so both
  survive the rename untouched.

**The deployed `C:\DISTR` paths are in scope.** The product is below v1.0.0 and no installation
in the field needs protecting, so consistency wins over compatibility. Two deployed roots exist,
not one:

- **Configuration** — `StartupOptions.cs:11`, `DefaultConfigDir = @"C:\DISTR\Config\Semistep\MBE"`,
  mirrored by `Installer/SemiStep.iss` in `[Files]` at `:88-90` and in the `--config-dir` parameter
  of every `[Icons]` and `[Run]` line.
- **Logs** — `StartupOptions.cs:14`, `DefaultLogFilePath = @"C:\DISTR\Logs\Semistep\semistep.log"`,
  documented for users at `Docs/readme.md:408`.

`Temp/Semistep` in `.run/*.run.xml` stays as is: a developer-machine log path with no deployed
counterpart.

## Development Approach

- **Testing approach**: Regular. This is build and deployment configuration; MSBuild properties
  have no unit tests, and inventing some would be theatre. Each task instead states the exact
  command that proves it landed, run against a real build.
- Complete each task fully before starting the next; run the test suite between tasks.
- **Tasks 4 and 5 are the last substantive changes, in that order.** They are the only ones that
  rename a shipped artifact — the executable and the deployed directories — so everything else is
  proven green before the renames muddy the diff. Tasks 6 and 7 verify and document.
- **The existing suite is a real regression guard for Task 4.** `TestAppBuilder` boots the real
  `App`, so all 615 `AvaloniaFact` tests load an `avares://Semistep/` URI. If the rename breaks
  asset resolution they go red as a group. Whether Avalonia resolves that URI case-insensitively is
  unknown and is assumed nowhere in this plan — Task 4 settles it by running the suite.
- **Task 2's settings are measured, not predicted.** Applied to `Directory.Build.props`, a clean
  Release build on 2026-07-21 reports `Предупреждений: 0`, `Ошибок: 0`. Its "if some other
  diagnostic fires" branch is a guard against future change, not a forecast about this one.

## Acceptance Evidence

Before-state measured 2026-07-21, on this machine, each as a command.

| Claim | Before | After |
|---|---|---|
| Release ships no symbols | `dotnet publish ... -c Release` then `ls **/release_win-x64/*.pdb` → nothing | the same glob lists `SemiStep.pdb` |
| Warnings are not enforced | `grep -rn 'TreatWarningsAsErrors' --include='*.props' --include='*.csproj' .` → no hits | the grep hits `Directory.Build.props`, and a deliberately introduced warning fails the build |
| Style is enforced only where a hook exists | `grep -n 'format' .github/workflows/ci.yml` → no hits | a naming violation from `.editorconfig` fails `dotnet build`, so CI catches it |
| Two spellings of InternalsVisibleTo | `grep -rn 'AssemblyAttribute' --include='*.csproj' .` → one hit in `SemiStep.UI.csproj` | no hits; `SemiStep.Tests` still compiles and passes |
| Assembly casing | build output names `Semistep.dll`; `grep -rn 'Semistep' --include='*.axaml' SemiStep/` → 12 hits | build output names `SemiStep.dll`; that grep returns nothing; the full suite is green |
| Deployed paths carry the wrong casing | `grep -rn 'Config.Semistep\|Logs.Semistep' --include='*.cs' --include='*.iss' --include='*.md' .` → hits in `StartupOptions.cs`, `SemiStep.iss`, `Docs/readme.md` | that grep returns nothing outside `Docs/plans/` |

**End-to-end proof** (manual, and the gate before shipping): build the installer with
`iscc.exe /DAppVersion=0.0.1 Installer/SemiStep.iss`, install it, launch from the created
shortcut, confirm the application starts and reads its configuration from the **new**
`C:\DISTR\Config\SemiStep\<preset>`, then force an exception and confirm the Serilog file carries
file and line numbers.

## Progress Tracking

- Mark completed items `[x]` immediately.
- `➕` for newly discovered tasks, `⚠️` for blockers.

## Solution Overview

Four of the five changes are build configuration confined to `Directory.Build.props` and two
csproj files. **Task 5 is not**: it changes two `public const` values on `StartupOptions` and
therefore changes where a deployed application looks for its configuration and writes its log.
Task 4 reaches into XAML, which the assembly name is embedded in. Neither is behaviour-neutral in
the way the other three are, and the diff deserves reading with that in mind.

PDBs **ship alongside the executable** rather than being archived per release tag. That costs a
few megabytes in the installer and makes internal method names readable to anyone who looks; in
exchange, a stack trace from a customer machine is actionable without matching a build against an
archived symbol file. Given that the Serilog file is the primary diagnostic for a deployed
PLC-integration application, that trade is worth making.

`DebugType=portable` rather than `embedded`: portable keeps the PDB a separate file, which the
installer already picks up, and leaves the assembly itself unchanged in size.

## Technical Details

**`Directory.Build.props` Release block**, replacing `:17-19`:

```xml
<PropertyGroup Condition="'$(Configuration)'=='Release'">
  <DebugType>portable</DebugType>
</PropertyGroup>
```

`DebugSymbols` is dropped rather than set to `True` — the SDK derives it from `DebugType`, and
stating both invites them to disagree. `portable` restates the SDK default, so the line is
technically redundant; it is kept deliberately, because the Release block existed only to hold the
`None` override and an empty-looking block is a worse record of the reversal than an explicit one.

**Warning enforcement**, in the unconditional `PropertyGroup` so it binds Debug as well as
Release. The build is at zero warnings, so the cost today is nil, and a violation is better seen
at the keystroke than at the push:

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

**The NuGet audit codes are exempted for the same reason `AnalysisLevel=latest` is refused.**
`NuGetAudit` defaults on with audit mode `all` on the .NET 10 SDK, and nothing here disables it, so
`TreatWarningsAsErrors` alone would turn NU1901-NU1904 into restore errors the day an advisory is
published against any direct or transitive package — CI red with no commit in the repository.
`dotnet list package --vulnerable --include-transitive` reports nothing today, so the exemption
costs nothing and the warnings still surface in the build log.

**`AnalysisLevel` is deliberately absent.** Without it the level derives from `TargetFramework`,
which fixes the rule set at net10.0's. Setting it to `latest` makes the set track whatever the
installed SDK considers newest — and `global.json` uses `rollForward: latestFeature`, so the SDK
already floats across feature bands within 10.0 (pinned floor 10.0.100, installed 10.0.302).
Combined with `TreatWarningsAsErrors`, a new band could break the build with no change in the
repository at all. The TFM-derived default is equally strict today and does not move on its own.

**`EnforceCodeStyleInBuild` is not a duplicate of the pre-commit hook**, which is what it looks
like at first glance. `ci.yml` runs no formatting check, and `.git/hooks/pre-commit` is not
versioned, so a fresh clone has none. Today the five `severity = warning` naming rules in
`.editorconfig` bind only on machines where someone installed the hook by hand. This setting makes
them part of the build, which is the only style gate that travels with the repository.

## What Goes Where

- **Implementation Steps** — changes inside this repository.
- **Post-Completion** — the installer build and the install-and-launch check, which need a Windows
  session and Inno Setup.

## Implementation Steps

### Task 1: Emit portable PDBs in Release

**Files:**
- Modify: `SemiStep/Directory.Build.props`

- [x] replace the Release `PropertyGroup` at `:17-19` with `<DebugType>portable</DebugType>`,
      dropping the explicit `DebugSymbols`
- [x] build: `dotnet build SemiStep.slnx -c Release` — succeeds
- [x] verify the symbol file appears next to the assembly:
      `ls SemiStep/Artifacts/bin/SemiStep.UI/release/*.pdb`
- [x] publish as the release workflow does:
      `dotnet publish SemiStep/SemiStep.UI/SemiStep.UI.csproj -c Release -p:Version=0.0.1`
      and confirm a `.pdb` under `SemiStep/Artifacts/publish/SemiStep.UI/release_win-x64/`
- [x] run the test suite: `dotnet test SemiStep.slnx` — must pass before Task 2

### Task 2: Lock the zero-warning state in centrally

**Files:**
- Modify: `SemiStep/Directory.Build.props`
- Modify: `.editorconfig` (added during the task — see the `➕` note below)

- [x] add `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` to the unconditional
      `PropertyGroup`. Do **not** add `AnalysisLevel` — see Technical Details
- [x] rebuild clean: `dotnet build SemiStep.slnx -c Release --no-incremental`
- [x] prove the warning gate bites: introduce an unused local in any file, confirm the build
      fails, remove it
- [x] prove the style gate bites: rename a private field to violate one of the five
      `severity = warning` naming rules in `.editorconfig`, confirm the build fails, revert it
- [x] if some other diagnostic fires, record its ID here under `➕` and suppress it individually in
      `NoWarn` — never by weakening the two properties. The measurement says nothing should fire
- [x] run the test suite — must pass before Task 3

➕ **The style gate did not bite until `.editorconfig` named IDE1006 explicitly.** With only
`EnforceCodeStyleInBuild=true`, both a private field renamed to `StringMaxLengthProbe` and a
private method renamed to `resolveStringMaxLengthProbe` built clean: `Предупреждений: 0`. The
`dotnet_naming_rule.*.severity = warning` entries drive the IDE, not the build; build enforcement
reads the IDE1006 diagnostic severity, which is unset by default. Added to `.editorconfig` above
the naming rules:

```
dotnet_diagnostic.IDE1006.severity = warning
```

Both probes then failed the build with `error IDE1006`, and with the probes reverted the clean
Release build stays at `Предупреждений: 0`, `Ошибок: 0` — the existing code has no naming
violations. `.editorconfig` is therefore a fourth file in this task, beyond the Files block. No
`NoWarn` entry was needed; nothing unanticipated fired.

### Task 3: One spelling of InternalsVisibleTo

**Files:**
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj`

- [x] replace the `AssemblyAttribute` `ItemGroup` at `:9-13` with
      `<ItemGroup><InternalsVisibleTo Include="SemiStep.Tests"/></ItemGroup>`, matching
      `SemiStep.Core.csproj:15`
- [x] verify no raw form survives: `grep -rn 'AssemblyAttribute' --include='*.csproj' .` returns
      nothing
- [x] run the test suite — a test reaching an `internal` member of `SemiStep.UI` is the real
      assertion here, so a green suite is the proof the attribute still applies
- [x] must pass before Task 4

### Task 4: Align the assembly name casing

The name is embedded in XAML, so this is not a one-line rename. Whether Avalonia resolves
`avares://` case-insensitively is unknown; the suite settles it rather than an assumption.

**Files:**
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj`
- Modify: `SemiStep/SemiStep.UI/App.axaml`
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeToolBar.axaml`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml`
- Modify: `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`
- Modify: `Installer/SemiStep.iss`
- Modify: `.zed/debug.json`
- Modify: `Docs/architecture/ui-localization.md`

- [x] `SemiStep.UI.csproj:5` — `<AssemblyName>SemiStep</AssemblyName>`
- [x] every `avares://Semistep/` → `avares://SemiStep/`: `App.axaml:9,67,68`,
      `RecipeToolBar.axaml:40,50,62,72,82,94,104`, `TransposedRecipeGridView.axaml:13`
- [x] `DataGridStyles.axaml:3` — `assembly=Semistep` → `assembly=SemiStep`. A plan under
      `Docs/plans/completed/` instructs leaving this casing alone; that instruction keeps the
      reference matching the assembly name, which this task renames, so it no longer applies
- [x] `Installer/SemiStep.iss:11` — `#define AppExeName "SemiStep.exe"`
- [x] `.zed/debug.json:6,23,40` — `Semistep.dll` → `SemiStep.dll`
- [x] `Docs/architecture/ui-localization.md:39-40` — the satellite becomes
      `ru/SemiStep.resources.dll` and the entry assembly name `SemiStep`
- [x] leave the `C:\DISTR` paths alone in this task — they are Task 5
- [x] delete the stale output so a leftover `Semistep.dll` cannot mask the change:
      `rm -rf SemiStep/Artifacts/bin SemiStep/Artifacts/publish`
- [x] rebuild and confirm the new name:
      `dotnet build SemiStep.slnx -c Release` prints `SemiStep.dll`
- [x] **run the full test suite — this is the assertion, not a formality.** All 615 `AvaloniaFact`
      tests boot the real `App` and load `avares://SemiStep/Styles/ColorPalette.axaml`. If asset
      resolution broke, they fail as a group and the cause is this task
- [x] confirm no reference to the old casing survives outside the `C:\DISTR` paths:
      `grep -rn 'Semistep' .zed Installer .github Docs SemiStep --include='*.json' --include='*.iss' --include='*.yml' --include='*.axaml' --include='*.cs' --include='*.md'`
      returns only `C:\DISTR` lines and completed-plan history. Note `--include='*.axaml'`: without
      it the check is blind to the twelve references above

### Task 5: Align the deployed `C:\DISTR` paths

**Files:**
- Modify: `SemiStep/SemiStep.UI/StartupOptions.cs`
- Modify: `Installer/SemiStep.iss`
- Modify: `Docs/readme.md`

- [x] `StartupOptions.cs:11` — `C:\DISTR\Config\Semistep\MBE` → `C:\DISTR\Config\SemiStep\MBE`
- [x] `StartupOptions.cs:14` — `C:\DISTR\Logs\Semistep\semistep.log` →
      `C:\DISTR\Logs\SemiStep\semistep.log`. Renaming one root and leaving the other is worse
      than either doing both or doing neither
- [x] `Installer/SemiStep.iss` — the `[Files]` `DestDir` at `:88-90` and the `--config-dir`
      parameter of every `[Icons]` and `[Run]` line
- [x] **delete the six `[InstallDelete]` entries at `:71-79`.** No machine carries the legacy flat
      layout, so the cleanup has nothing left to do and the entries are dead weight. Renaming them
      to the new casing would have worked just as well — NTFS is case-insensitive, so
      `C:\DISTR\Config\Semistep\actions` and `C:\DISTR\Config\SemiStep\actions` are the same
      directory — but keeping cleanup for a layout nothing carries is not worth the lines
- [x] `Docs/readme.md:408` — the user-facing log path
- [x] verify the old literals are gone, not merely consistent:
      `grep -rn 'Config.Semistep\|Logs.Semistep' --include='*.cs' --include='*.iss' --include='*.md' .`
      returns nothing outside `Docs/plans/`
- [x] verify the code and the installer agree on the new literal, since nothing enforces it at
      build time: `grep -rn 'DISTR' --include='*.cs' --include='*.iss' .` shows `SemiStep` in every
      hit
- [x] run the test suite. **It proves nothing about this task** — no test references
      `DefaultConfigDir` or `DefaultLogFilePath` — and is run only to confirm nothing else broke.
      The real check is the manual install in Post-Completion

**Consequence, for the release notes.** None on Windows, and that is worth stating explicitly.
NTFS is case-insensitive: `C:\DISTR\Config\Semistep\MBE` and `C:\DISTR\Config\SemiStep\MBE`
resolve to the same directory, verified empirically on this machine. A machine that already has
the application therefore keeps reading its existing configuration, the installer overwrites the
existing tree rather than writing a second copy, and no edit made before the upgrade is stranded.
The rename is cosmetic on disk; only newly created directories pick up the new casing.

### Task 6: Verify acceptance criteria

- [x] every row of the Acceptance Evidence table checked, After column reproduced
- [x] full suite green: `dotnet test SemiStep.slnx`
- [x] clean Release build green: `dotnet build SemiStep.slnx -c Release --no-incremental`
- [x] publish green, with a `.pdb` in the output
- [x] `git diff --stat` against the branch base touches only the files named in the Files blocks
      of Tasks 1-5, plus the follow-on files listed below, plus `CLAUDE.md`,
      `Docs/architecture/` and this plan file once Task 7 has run.
      The branch base commit is a separate change that moves the solution file to the repository
      root and drags `.run/*`, `.zed/tasks.json`, `.github/workflows/ci.yml`,
      `.claude/skills/make-release/SKILL.md` and `CLAUDE.md` with it; every command in this plan
      assumes that layout, so compare against that commit, not against `origin/master`

**Verification run, 2026-07-21.** Base commit `af24bc4`.

| Row | Command | Result |
|---|---|---|
| Release ships symbols | `dotnet publish ... -c Release -p:Version=0.0.1` | `release_win-x64/SemiStep.pdb` present |
| Warnings enforced | `grep -rn 'TreatWarningsAsErrors' --include='*.props' --include='*.csproj' .` | one hit, `SemiStep/Directory.Build.props:10` |
| Warning gate bites | probe file with an unused local | `error CS0219`, build fails |
| Style gate bites | same probe, private field `BadlyNamedField` | `error IDE1006`, build fails |
| One spelling of InternalsVisibleTo | `grep -rn 'AssemblyAttribute' --include='*.csproj' .` | no hits |
| Assembly casing | clean Release build | `Artifacts\bin\SemiStep.UI\release\SemiStep.dll`; `grep -rn 'Semistep' --include='*.axaml' SemiStep/` returns nothing |
| Deployed paths | `grep -rn 'Config.Semistep\|Logs.Semistep' --include='*.cs' --include='*.iss' --include='*.md' .` | hits only under `Docs/plans/` |
| Clean Release build | `dotnet build SemiStep.slnx -c Release --no-incremental` | `Предупреждений: 0`, `Ошибок: 0` |
| Full suite | `dotnet test SemiStep.slnx` | 1444 passed, 0 failed |

➕ **The diff-scope check needed widening.** `git diff --stat af24bc4 HEAD` also touches
`.github/workflows/ci.yml`, `.run/*.run.xml`, `Docs/perf/README.md` and
`SemiStep/SemiStep.UI/Assets/Icons/README.md`, none of which appear in a Files block. Each is a
direct consequence of the work rather than unrelated drift, so the check's file list was wrong,
not the diff: `ci.yml` gained `.editorconfig` and `SemiStep.slnx` to its `paths` filter so a change
to either actually triggers CI; the two README files carried `avares://Semistep/` and a stale
`SemiStep/.run/` path; the `.run` configurations named `SemiStep.UI.exe`, which the Task 4 rename
made `SemiStep.exe`. The checkbox above now names them.

➕ **`Installer/SemiStep.iss:21` reads `#define LogRoot "C:\DISTR\Logs"`**, the parent, so Task 5's
"`grep -rn 'DISTR' ...` shows `SemiStep` in every hit" does not hold literally for that line or for
the `; ... we write to Program Files and C:\DISTR` comment at `:59`. Not a defect: the installer
only pre-creates the `Logs` root and the application appends `SemiStep\semistep.log` itself. The
config side, where the installer does write into the product directory, is
`#define ConfigRoot "C:\DISTR\Config\SemiStep"` and carries the correct casing.

### Task 7: [Final] Update documentation

`CLAUDE.md` closes with "do not add specifics here", so it gets one line and the details go to
`Docs/`.

**Files:**
- Modify: `CLAUDE.md`
- Add: `Docs/architecture/build-and-deployment.md`

- [x] `CLAUDE.md`, Build section, one line: the build treats warnings and `.editorconfig` **naming**
      violations (IDE1006) as errors, so a new one fails CI rather than accumulating. Say naming
      specifically — whitespace and the `csharp_*` formatting preferences still bind only through
      the unversioned `.git/hooks/pre-commit`, and `ci.yml` runs no `dotnet format` step
- [x] `Docs/architecture/` — record the deployed paths in their new form,
      `C:\DISTR\Config\SemiStep\` and `C:\DISTR\Logs\SemiStep\`, and that they are duplicated
      between `StartupOptions.cs` and `Installer/SemiStep.iss` with nothing checking that the two
      agree
- [x] `Docs/architecture/` — one line stating the current rule: the assembly is `SemiStep` and every
      `avares://` URI and `assembly=` reference carries that casing. `Docs/plans/completed/20260623-cell-change-highlight.md:149`
      instructs the opposite ("do not correct the casing"); that historical record stays as written,
      so a future reader who hits it needs the current rule stated somewhere live
- [x] move this plan to `Docs/plans/completed/`

**Decision: a new file, `Docs/architecture/build-and-deployment.md`.** Every existing document
under `Docs/architecture/` is scoped to one subject — `error-reporting.md`, `exit-flow.md`,
`ui-localization.md`, `headless-ui-testing.md` — and none covers build configuration or deployment
layout, so there was no host to extend. It also records the IDE1006 finding from Task 2, which
otherwise survives only in an `.editorconfig` comment and in this plan.

## Post-Completion

*No checkboxes — these need a Windows session with Inno Setup and a real install.*

**Manual verification, the gate before shipping:**

1. `iscc.exe /DAppVersion=0.0.1 Installer/SemiStep.iss` builds without error.
2. The produced installer installs, and `{app}` contains `SemiStep.exe` and `SemiStep.pdb`.
3. A shortcut created by the installer launches the application, and it reads its configuration
   from `C:\DISTR\Config\SemiStep\<preset>` — the new path. This is the check that Task 5 landed in
   both the code and the installer, which nothing at build time verifies.
4. The log appears at `C:\DISTR\Logs\SemiStep\semistep.log`.
5. Force an exception; the Serilog file shows file names and line numbers in the stack trace.

**Existing installations:** nothing to clean up. NTFS is case-insensitive, so Inno writes
`SemiStep.exe` to the same filesystem path the old `Semistep.exe` occupies and replaces it in
place; an in-place upgrade leaves no second executable behind.

**CI:** `.github/workflows/release.yml:57,71` names neither the assembly nor the executable, so no
workflow change is required. Note that `ci.yml`'s `paths` filter (`SemiStep/**`, `ConfigFiles/**`,
`global.json`) does not cover `Installer/` or `.zed/`, so the Task 4 and 5 edits to those
directories will not trigger CI. Pre-existing, and the reason the first release run is the
confirmation rather than a green check on the pull request.

**Executed by exec:**
- branch: build-hygiene
- progress file: /tmp/progress-20260721-build-hygiene.txt
