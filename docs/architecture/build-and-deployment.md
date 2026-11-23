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
  `TargetFramework` and stays fixed at net11.0's. `global.json` uses `rollForward: latestFeature`,
  so the SDK floats across feature bands; setting `AnalysisLevel=latest` would let a new band break
  the build with no change in the repository.
- **`EnforceCodeStyleInBuild=true` plus `dotnet_diagnostic.IDE1006.severity = warning` in
  `.editorconfig`.** Together these turn naming violations into build errors.
- **`AnalysisMode=Recommended` plus `dotnet_analyzer_diagnostic.category-Style.severity = warning`
  in `.editorconfig`.** Every quality rule in the Recommended set and every IDE style rule fails the
  build unless `.editorconfig` lowers it with an explicit `dotnet_diagnostic` entry; each opt-out
  there carries its reason. The category line overrides the `:severity` suffix on option entries,
  so a `:silent` suffix alone does not turn a rule off.
- **`GenerateDocumentationFile=true` with `CS1591` in `NoWarn`.** The compiler reports unused
  usings (IDE0005) at build only when it writes a documentation file. Doc-comment coverage is not
  required.

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

## Target runtime and CPU

The projects target `net11.0` with C# 15, and `global.json` pins SDK `11.0.100`. Release publishes
are self-contained for `win-x64`, so an installed machine needs no separate .NET runtime.

.NET 11 does not start on a CPU without the x86-64-v2 instruction sets (`CX16`, `POPCNT`, `SSE3`,
`SSSE3`, `SSE4.1`, `SSE4.2`). It exits with "The current CPU is missing one or more of the baseline
instruction sets". Every CPU that Windows 11 supports has them, but an old industrial PC may not.
The ReadyToRun images that `PublishReadyToRun=true` produces target x86-64-v3 (AVX2 and others). On
a CPU with v2 but not v3 the app still runs, and those images fall back to JIT, so startup is
slower. Source: Microsoft Learn, "Breaking change: Minimum hardware requirements updated" (.NET 11).

## Release symbols

Release builds emit portable PDBs (`<DebugType>portable</DebugType>` in the Release
`PropertyGroup`). The installer's `[Files]` section copies the publish directory wholesale with no
`pdb` exclusion, so `SemiStep.pdb` ships next to the executable. That is what puts file names and
line numbers into the Serilog stack traces coming off installed machines, which is the primary
diagnostic for a deployed PLC-integration application.

## PLC emulator and snap7.dll

`SemiStep.Tools.PlcEmulator` is a development tool with its own window. It serves the PLC memory
over S7 through the native `snap7.dll`. The repository commits the x64 DLL in
`SemiStep/SemiStep.Tools.PlcEmulator/native/`, and the project copies everything in that folder to
the output (`<Content Include="native\*" ...>`). The build downloads nothing and needs no network
access.

The emulator is in `SemiStep.slnx`. `SemiStep.Tests` references it. Building `SemiStep.UI` alone
does not build it.

The installer does not ship the emulator. `Installer/SemiStep.iss` copies one binary folder,
`SrcBinDir` (`SemiStep\Artifacts\publish\SemiStep.UI\release_win-x64`), plus the three preset
configuration folders. The emulator is not in the UI publish output, so neither it nor
`snap7.dll` reaches an installed machine. The LGPLv3 notice and licence texts
are in `SemiStep/SemiStep.Tools.PlcEmulator/native/`.

## Assembly name casing

**The assembly is `SemiStep`.** Every `avares://` URI and every XAML `assembly=` reference carries
that casing: `avares://SemiStep/...`, `clr-namespace:SemiStep.UI.RecipeGrid;assembly=SemiStep`. The
build output is `SemiStep.dll`, the executable `SemiStep.exe`, the Russian satellite
`ru/SemiStep.resources.dll`.

`docs/plans/completed/20260623-cell-change-highlight.md:149` instructs keeping `assembly=Semistep`
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
