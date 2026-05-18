# Startup Options: Installer Preset Selection, Rider and Zed Runners

## Overview

The application already accepts startup parameters (`--config-dir`, `--log-file`, `--logging-level`)
through `StartupOptions.Parse`. The wiring around it is incomplete:

- The Inno Setup script copies configuration files to the wrong path: it writes to
  `C:\DISTR\Config\Semistep\<subdir>\` while `StartupOptions.DefaultConfigDir` points at
  `C:\DISTR\Config\Semistep\MBE`. As a result the installed application cannot find configs
  when launched without arguments.
- The installer shortcuts do not pass `--config-dir`. The CLI mechanism is never used on a
  normal production launch.
- The desired production behaviour is "install both MBE and MOCVD presets, let the user pick
  which one the shortcut activates at install time". The script does not yet support this.
- The Rider run config `Debug Semistep.run.xml` is user-local (untracked), hard-codes Oleg's
  paths, and passes a directory rather than a file path to `--log-file` (which causes the log
  to be written as a file named `Temp` in the parent directory).
- The Zed `tasks.json` and `debug.json` do not pass any startup arguments.

This plan closes those gaps without changing the parser or default behaviour.

## Context (from discovery)

Files involved:
- `Installer/SemiStep.iss` — Inno Setup script. Currently has a temporary `#define ConfigPreset`
  used as a build-time switch. Will be replaced with a runtime user choice.
- `SemiStep/SemiStep.UI/StartupOptions.cs` — CLI parser. Not changed in this plan.
- `SemiStep/SemiStep.UI/Program.cs` — startup entry. Not changed in this plan.
- `SemiStep/.run/Debug Semistep.run.xml` — untracked Rider run config with Oleg's local paths.
  Will be deleted and replaced with two committed presets.
- `SemiStep/.run/Publish.run.xml`, `SemiStep/.run/dotnet_restore.run.xml` — unrelated, left alone.
- `.zed/tasks.json` — has a single `Run UI` task with no args.
- `.zed/debug.json` — has a single `Launch SemiStep.UI` adapter config with no args.

Patterns and conventions observed:
- `Docs/plans/` uses kebab-case names with a `yyyymmdd-` prefix.
- Inno Setup `[Tasks]` with `Flags: exclusive` is the standard way to render mutually
  exclusive radio-button selections during install.
- Rider's `$PROJECT_DIR$` resolves to the `.run` file's project folder (here: `SemiStep/`),
  and `$USER_HOME$` resolves to the user's home directory.
- Zed exposes `$ZED_WORKTREE_ROOT` (tasks) and `${ZED_WORKTREE_ROOT}` (debug) as the
  repository root.

Dependencies:
- The repository already contains the two preset trees `ConfigFiles/MBE/` and
  `ConfigFiles/MOCVD/` after commit `56e503f` ("Add MBE/MOCVD config separation").
- `StartupOptions.DefaultConfigDir` stays as `C:\DISTR\Config\Semistep\MBE` per user decision.

## Development Approach

- **testing approach**: not applicable — all changes are to build/installer configuration and
  IDE runner descriptors. No production code is modified. Verification is performed manually
  by compiling the Inno Setup script and by inspecting that Rider and Zed pick up the new
  run configs.
- complete each task fully before moving to the next.
- update this plan file when scope changes during implementation.

## Testing Strategy

There is no automated test coverage applicable to installer scripts or IDE run-config XML.
Verification is manual:
- `iscc.exe /DAppVersion=0.0.0 Installer/SemiStep.iss` must compile without errors.
- Opening the solution in Rider must list `Debug MBE` and `Debug MOCVD` run configurations.
- Opening the repository in Zed must list `Run UI (MBE)` and `Run UI (MOCVD)` tasks and
  matching debug launch configs.

## Progress Tracking

- mark completed items with `[x]` immediately when done
- add newly discovered tasks with `+` prefix
- document issues/blockers with `!` prefix
- update plan if implementation deviates from original scope

## Solution Overview

Two main mechanisms:

1. **Installer**: install both preset trees unconditionally. Use Inno Setup `[Tasks]` with
   `exclusive` flag to present the user with a radio choice ("MBE" or "MOCVD"). The selected
   task gates which `[Icons]` entries (and which `Parameters:` line) get created. Files of
   both presets always land on disk under preset-specific subfolders, so a user can later
   edit a shortcut by hand to switch presets without reinstalling.

2. **Dev runners**: ship two committed Rider run configs and two Zed tasks/debug configs,
   one per preset. Both point at the in-repo `ConfigFiles/<preset>` directory via the IDE's
   own root variable, so any developer gets working configurations on clone.

## Technical Details

Inno Setup install layout:
```
C:\DISTR\Config\Semistep\
    MBE\
        actions\...
        columns\...
        connection\...
        groups\...
        properties\...
        ui\...
    MOCVD\
        actions\...
        columns\...
        ...
```

Shortcut command line (one per preset, selected at install time). The `Parameters:` value
is a single Inno Setup string; embedded double quotes around the path are written by
doubling them (`""`). Verbatim:
```
Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MBE"""
Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MOCVD"""
```
The path itself contains no spaces, so quoting is not strictly required, but is kept for
robustness against future path edits and clarity for whoever opens the script.

Rider run config parameters (per preset):
```
--config-dir "$PROJECT_DIR$/../ConfigFiles/MBE"
--log-file "$USER_HOME$/AppData/Local/Temp/semistep.log"
--logging-level verbose
```

Zed task arguments (per preset):
```
"--", "--config-dir", "$ZED_WORKTREE_ROOT/ConfigFiles/MBE"
```

Zed debug args (per preset):
```
["--config-dir", "${ZED_WORKTREE_ROOT}/ConfigFiles/MBE"]
```

## What Goes Where

- **Implementation Steps**: `.iss` rewrite, two new Rider XML files, deletion of the
  untracked Rider XML, edits to `.zed/tasks.json` and `.zed/debug.json`.
- **Post-Completion**: compile installer locally, smoke-test in Rider and Zed, manual install
  on a clean Windows box if available.

## Implementation Steps

### Task 1: Rewrite Inno Setup script for preset selection

**Files:**
- Modify: `Installer/SemiStep.iss`

- [x] remove `#define ConfigPreset`, the surrounding `#ifndef` block, and the `SrcCfgDir`
      override that embeds the preset name; restore `SrcCfgDir` to `..\ConfigFiles`
- [x] update the stale "see Program.cs: ConfigDir constant" comment to refer to
      `StartupOptions.DefaultConfigDir`
- [x] add `[Tasks]` entries `preset_mbe` first, then `preset_mocvd`, both with
      `Flags: exclusive` and a shared `GroupDescription`. Inno selects the first entry in an
      exclusive group as the default, so order matters and `preset_mbe` must be first.
- [x] replace the six per-subfolder `Source: "{#SrcCfgDir}\..."` lines in `[Files]` with two
      lines copying `..\ConfigFiles\MBE\*` and `..\ConfigFiles\MOCVD\*` into preset-specific
      destination subdirectories `C:\DISTR\Config\Semistep\MBE` and `...\MOCVD`. This
      changes the install layout from the previous *flat* one
      (`C:\DISTR\Config\Semistep\actions\...`) to a *preset-nested* one
      (`C:\DISTR\Config\Semistep\MBE\actions\...`).
- [x] add an `[InstallDelete]` section to remove the flat-layout leftovers from prior
      installs: delete `C:\DISTR\Config\Semistep\actions`, `\columns`, `\connection`,
      `\groups`, `\properties`, `\ui` (each `Type: filesandordirs`). This runs before
      `[Files]` and keeps the new `MBE\` and `MOCVD\` siblings intact.
- [x] replace the three `[Icons]` entries with four: each of the start-menu and desktop
      icons gets one variant per preset, gated by `Tasks: preset_mbe` or
      `Tasks: preset_mocvd` (desktop entries additionally need `Tasks: desktopicon and
      preset_xxx`). Use the verbatim `Parameters:` strings from Technical Details above.
- [x] split the `[Run]` entry into two preset-gated entries, each passing the matching
      `--config-dir` so that post-install launch uses the chosen preset
- [x] manual verification (skipped — not automatable in this session)

### Task 2: Replace Rider run configurations

**Files:**
- Delete: `SemiStep/.run/Debug Semistep.run.xml` (currently untracked)
- Create: `SemiStep/.run/Debug MBE.run.xml`
- Create: `SemiStep/.run/Debug MOCVD.run.xml`

- [x] delete the untracked `Debug Semistep.run.xml`
- [x] create `Debug MBE.run.xml` based on the structure of the deleted file, with
      `name="Debug MBE"` and `PROGRAM_PARAMETERS` set to
      `--config-dir "$PROJECT_DIR$/../ConfigFiles/MBE" --log-file "$USER_HOME$/AppData/Local/Temp/semistep.log" --logging-level verbose`
- [x] create `Debug MOCVD.run.xml` as a duplicate with `name="Debug MOCVD"` and the MOCVD
      path
- [x] manual verification (skipped — Rider not available in this session)

### Task 3: Update Zed tasks and debug configurations

**Files:**
- Modify: `.zed/tasks.json`
- Modify: `.zed/debug.json`

- [ ] in `tasks.json`, replace the `Run UI` task with two tasks named `Run UI (MBE)` and
      `Run UI (MOCVD)`, each passing `--`, `--config-dir`, and the preset path under
      `$ZED_WORKTREE_ROOT/ConfigFiles/`
- [ ] in `debug.json`, replace `Launch SemiStep.UI` with two entries
      `Launch SemiStep.UI (MBE)` and `Launch SemiStep.UI (MOCVD)`, each adding an `args`
      array with `--config-dir` and the preset path under `${ZED_WORKTREE_ROOT}/ConfigFiles/`
- [ ] manual verification: open the repository in Zed and confirm both task and debug
      entries are listed

### Task 4: Verify acceptance criteria

- [ ] confirm `Installer/SemiStep.iss` compiles
- [ ] confirm Rider lists `Debug MBE` and `Debug MOCVD` and both run
- [ ] confirm Zed lists `Run UI (MBE)`, `Run UI (MOCVD)`, and matching debug entries
- [ ] confirm `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` still succeeds
      (no code changes expected, but a smoke build confirms nothing else regressed)

### Task 5: Move plan to completed

- [ ] move this plan to `Docs/plans/completed/`

## Post-Completion

**Uninstall behaviour** (deliberate choice, no code change):
- `C:\DISTR\Config\Semistep\MBE\` and `\MOCVD\` are left on disk after uninstall. These
  trees may contain user-edited recipe configurations and removing them on uninstall would
  silently destroy work. Same rationale as for `C:\DISTR\Logs\`.
- The `[InstallDelete]` step described in Task 1 runs only at install/upgrade time and only
  removes the legacy *flat* subfolders from prior installations; it does not affect the new
  preset trees.

**AppId stays the same** (`{{8B3F2C1A-...}}`), so installing this build over an existing
installation triggers Inno's in-place upgrade flow. The `[InstallDelete]` section above
handles the layout migration; without it, the orphaned flat-layout folders would remain
forever next to the new `MBE\` and `MOCVD\` siblings.

**Manual verification on a clean Windows box** (optional, recommended before release):
- run the built installer, pick the MBE radio option, verify the start-menu shortcut command
  line contains `--config-dir "C:\DISTR\Config\Semistep\MBE"`
- repeat with the MOCVD radio option
- launch via shortcut and confirm the application starts with the matching configuration
- launch the `.exe` directly without arguments and confirm it falls back to the MBE default
  (current intentional behaviour per user decision)

**Not in scope for this plan** (deliberately deferred):
- making `StartupOptions.DefaultConfigDir` neutral or removing the default entirely
- `--help` output or a MessageBox-based usage display
- validation of unknown CLI keys or missing-value handling
- logging of parser warnings after Serilog initialisation
- unit tests for `StartupOptions.Parse`
- `SemiStep/Directory.Packages.props` changes visible in working tree (will not be committed)
