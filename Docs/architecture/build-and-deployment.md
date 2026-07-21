# Build and Deployment

What the build enforces, what it does not, and where a released application looks on disk. Read
this before relaxing an MSBuild property, renaming the assembly, or changing a deployed path.

## What the build enforces

`SemiStep/Directory.Build.props` governs the three projects — the file sits under `SemiStep/`,
not at the repository root.

- **`TreatWarningsAsErrors=true`.** Any compiler or analyzer warning fails the build, in Debug and
  Release alike. The repository is at zero warnings; a new one has to be fixed, not accumulated.
- **`WarningsNotAsErrors=NU1900;NU1901;NU1902;NU1903;NU1904`.** NuGet audit results depend on the
  outside world. A newly published advisory against a transitive package, or an unreachable audit
  source, must not turn restore red with no commit in the repository. The warnings still appear in
  the build log.
- **`AnalysisLevel` is deliberately absent.** Without it the rule set derives from
  `TargetFramework` and stays fixed at net10.0's. `global.json` uses `rollForward: latestFeature`,
  so the SDK floats across feature bands; setting `AnalysisLevel=latest` would let a new band break
  the build with no change in the repository.
- **`EnforceCodeStyleInBuild=true` plus `dotnet_diagnostic.IDE1006.severity = warning` in
  `.editorconfig`.** Together these turn naming violations into build errors.

**`EnforceCodeStyleInBuild` alone does not gate naming.** The `dotnet_naming_rule.*.severity`
entries in `.editorconfig` drive the IDE only; build enforcement reads the IDE1006 diagnostic
severity, which is unset by default. Measured: with only `EnforceCodeStyleInBuild=true`, a private
field renamed to `StringMaxLengthProbe` built clean at zero warnings. Removing the
`dotnet_diagnostic.IDE1006.severity` line silently disables the naming gate while leaving every
naming rule visibly in place.

## What the build does not enforce

Whitespace and the `csharp_*` formatting preferences in `.editorconfig` are **not** part of the
build gate. They bind through `dotnet format SemiStep.slnx --verify-no-changes` in
`.git/hooks/pre-commit`, which is not versioned — a fresh clone has no hook — and
`.github/workflows/ci.yml` runs no `dotnet format` step. Formatting is therefore enforced only on
machines where someone installed the hook by hand.

## Release symbols

Release builds emit portable PDBs (`<DebugType>portable</DebugType>` in the Release
`PropertyGroup`). The installer's `[Files]` section copies the publish directory wholesale with no
`pdb` exclusion, so `SemiStep.pdb` ships next to the executable. That is what puts file names and
line numbers into the Serilog stack traces coming off installed machines, which is the primary
diagnostic for a deployed PLC-integration application.

## Assembly name casing

**The assembly is `SemiStep`.** Every `avares://` URI and every XAML `assembly=` reference carries
that casing: `avares://SemiStep/...`, `clr-namespace:SemiStep.UI.RecipeGrid;assembly=SemiStep`. The
build output is `SemiStep.dll`, the executable `SemiStep.exe`, the Russian satellite
`ru/SemiStep.resources.dll`.

`Docs/plans/completed/20260623-cell-change-highlight.md:149` instructs keeping `assembly=Semistep`
lowercase and says "do not correct the casing". That instruction was correct when the assembly was
named `Semistep` and is now obsolete; the historical plan stays as written. This section is the
current rule.

The full `AvaloniaFact` suite boots the real `App` and loads `avares://SemiStep/Styles/ColorPalette.axaml`,
so a broken asset URI shows up as the UI tests failing as a group.

## Deployed paths

An installed application uses two roots under `C:\DISTR`:

| Root | Default |
|---|---|
| Configuration | `C:\DISTR\Config\SemiStep\<preset>` (presets: `MBE`, `MOCVD`, `RIE`) |
| Logs | `C:\DISTR\Logs\SemiStep\semistep.log` |

Both are overridable per launch through `--config-dir` and `--log-file`; the installer's shortcuts
pass `--config-dir` explicitly, one shortcut per preset.

**The two sides of each path are duplicated by hand and nothing checks that they agree.** The
application holds them as `StartupOptions.DefaultConfigDir` and `StartupOptions.DefaultLogFilePath`
(`SemiStep/SemiStep.UI/StartupOptions.cs`); the installer holds them as `#define ConfigRoot` and
`#define LogRoot` (`Installer/SemiStep.iss`). No build step, no test and no CI job compares them.
The only thing that detects a divergence is a manual install followed by launching from a
created shortcut — an application that silently falls back to a directory the installer never
wrote configuration into looks like a missing-config error, not a path mismatch. Change one side,
change the other in the same commit.

NTFS is case-insensitive, so the casing of these paths is cosmetic on disk: an upgrade over an
installation created with the older `Semistep` spelling keeps reading the same directories.
