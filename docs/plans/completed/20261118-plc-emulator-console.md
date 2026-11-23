# PLC emulator, first usable slice

## Behavior

- Today SemiStep can only be tested against a real PLC. No emulator exists.
- After this plan the operator starts a console emulator, `SemiStep.Tools.PlcEmulator`, and then
  SemiStep against it, from two launch profiles (emulator, app) in Rider or Zed, plus a Rider
  compound that starts both where Rider supports it. SemiStep connects, enables sync, writes and
  reads back the recipe exactly as with the real PLC.
- The emulator speaks the real S7 protocol (native snap7 server on 127.0.0.1), builds its memory
  from the same preset folder the app loads (`ConfigFiles/<preset>`), and acts like the PLC: it
  seeds the protocol version, stores what the app writes, and on a `run` console command executes
  the committed recipe, updating the execution area the app polls (recipe active, current line,
  step elapsed time, loop counters) until the recipe ends or `stop` is typed.
- SemiStep gets one startup option, `--plc-endpoint host:port`, that overrides the PLC address from
  `connection.yaml`. A malformed value stops the app at startup with a clear error instead of
  falling back to the configured address (which is the real PLC).
- Scope guard: the wire format is the one SemiStep writes today (#174 is not done: the PLC code is
  not owned). No memory-editing window, no fault injection, no Aspire, no CI integration job; each is
  a later PR. No change to how the app talks to a real PLC when the option is absent.

## Flow branches

| Situation | The emulator / app does | Decided by | Checked by |
|---|---|---|---|
| Emulator starts with a valid preset | Prints the endpoint, the DB list with sizes, and "ready"; version reads 1 | agent: DB numbers and the managing/execution sizes come from `ConnectionMapper`; data DB sizes per the rule below | Acceptance 3, smoke 1 |
| Data DB sizes | DB997/998 hold capacity for 3000 ints / 6000 floats, DB999 for 900 strings of the configured length, plus the 8-byte header; overridable with `--int-capacity`, `--float-capacity`, `--string-capacity` | agent: the config has no data DB size; the defaults copy the TIA export's array bounds (example only) | Acceptance 3 |
| App writes past the emulated size | snap7 rejects the write; the app's write fails and reports it as with a real PLC; the console prints the rejected range | agent: a real PLC rejects out-of-range writes too | Acceptance 6 |
| snap7.dll not yet cached | The build downloads it from the pinned URL, checks the pinned SHA-256, caches it, and copies it to the emulator and test outputs | agent | Acceptance 1 |
| snap7.dll cached with the right hash | No download | agent | Acceptance 1 |
| Download fails or hash mismatches | Build fails naming the URL and the expected hash | agent | Gates |
| Port already in use | Emulator exits with code 1 and names the port | agent: probed in Task 2 (see Caveats) | Acceptance 4 |
| SemiStep started with a valid `--plc-endpoint` | Connects to that endpoint instead of the yaml address | agent: override applied in `Program` with `with` on the public config records | Acceptance 5 |
| `--plc-endpoint` malformed or missing its value | App does not start: error dialog/console message names the option, exit code 1 | agent: a silent fallback would connect a dev app to the real PLC | Acceptance 5 |
| SemiStep started without the option | Uses `connection.yaml` as today | agent | Acceptance 5 |
| App enables sync and writes a recipe | Emulator stores it; the app's verify read-back passes; on the committed flag's rising edge the console prints the line count | agent: the engine polls the managing area each tick; no native callback | Acceptance 6, smoke 3 |
| `run` with no committed recipe | Prints "no committed recipe", no change | agent: the PLC needs the committed flag | Acceptance 7 |
| `run` and the recipe fails to decode or analyse (length mismatch, unmatched For/EndFor, For count 0 or less, nesting over 3) | Prints the reason, no run | agent: the PLC checker rejects such recipes; `RecipeAnalyzer` reports loop errors | Acceptance 7 |
| `run` with a valid committed recipe | Recipe active set; current line -1 for one tick, then 0..N-1 by step durations (a zero-duration step lasts one tick); step time counts elapsed seconds; loops unrolled, counters 1-based per depth; after the last line, current line stays N and recipe active holds about 1 s, then clears | agent: FB Recipe in the TIA export (example only); elapsed seconds per the operator | Acceptance 7, smoke 4 |
| A Pause step (waits for the operator) | Holds on that line until `continue` is typed | agent: Pause has no duration on the PLC | Acceptance 7 |
| `run` while a run is active | Prints "already running", no change | agent | Acceptance 7 |
| `stop` while running | Stops within one tick, holds recipe active about 1 s, then clears | agent: TIA state 1000 then idle | Acceptance 7, smoke 5 |
| App writes a new recipe while running | Stored; the run continues on the decoded copy taken at `run` | agent: the PLC does not block rewrites; the app blocks edits while active | - |
| stdin closes (headless start) | Emulator keeps serving until the process is stopped | agent: launchers may not attach a console | Task 5 check |
| Emulator restarts | Memory starts empty again (version seeded) | agent: no persistence | - |

## Decisions

- Native snap7 server in-process, bound to 127.0.0.1, buffers allocated pinned. probed: a research
  probe crashed 3/3 with unpinned buffers under forced GC and passed 9/9 pinned, 2026-11-18.
  rejected: python-snap7 (second runtime, PDU 480 vs 960), PLCSIM Advanced (licence and TIA project),
  a .NET S7 server (none exists).
- A minimal own P/Invoke layer over the snap7 server functions the emulator needs, including the
  area lock/unlock calls around every engine read and write of a registered buffer. rejected:
  vendoring S7netplus's `snap7.net.cs` (it does not pin buffers).
- snap7.dll (x64, LGPLv3) is fetched at build time from a pinned URL on GitHub (the copy in
  S7netplus's repository at a fixed commit) and checked against a pinned SHA-256, cached under the
  build artifacts folder (git-ignored), and skipped when the cached file matches. The tool folder
  carries the LGPL notice and the source link. No binary is committed. The test project references
  the emulator project, so the DLL reaches the test output.
- The emulator reuses SemiStep.Core's config loader, `ConnectionMapper`, layout, recipe decoding and
  `RecipeAnalyzer` through `InternalsVisibleTo` for the tool project. The engine unrolls loops itself
  (`TimingCalculator` gives first-pass start times only). The execution area is written by the
  emulator's own encoder from the layout; engine tests read it back with Core's
  `ExecutionStateCodec`, so the app's decoder checks the emulator's encoder.
- The run engine follows FB Recipe's behaviour from the TIA export and says so in its startup line
  and README ("per TIA_Plazma export, may be stale").
- Endpoint override parsed in SemiStep.UI with an explicit error and applied in `Program` on the
  public `AppConfiguration`/`PlcConnectionSettings` records; Core gets no new surface.
- Launch profiles for RIE only; the README shows MBE and MOCVD.

## Caveats

- ASSUMPTION: the snap7 server can listen on a chosen port (local-port parameter); fallback: the
  emulator listens on 102 only, the launch profiles use 102, and the loopback test uses 102 (the test
  assembly runs serially); checked by: Task 2.
- ASSUMPTION: a second snap7 server on an occupied port fails to start (no address reuse);
  fallback: the emulator checks the port with a plain TCP listener before starting snap7; checked by:
  Task 2.
- ASSUMPTION: the P/Invoke layer builds under the analyzer baseline; fallback: use `LibraryImport`
  with `AllowUnsafeBlocks` in the tool project only, or set the specific interop rule's severity in
  the tool project with a one-line reason; checked by: Task 2.
- The app polls the execution area every 100 ms and reads the managing area every 5 s for keep-alive
  (read from source, `PlcExecutionMonitor`, `S7Service`); the engine ticks at 100 ms.

## Constraints and non-goals

- No behaviour change for SemiStep when `--plc-endpoint` is absent.
- The emulator project builds under the repository's analyzer baseline (warnings as errors).
- No memory window, no faults, no Aspire, no CI integration job, no persistence.
- Tests stay simple: the run engine on a fake time provider; one loopback round trip through the real
  S7 stack. New tests carry `[Trait("Component","S7")]`, `[Trait("Area","Emulator")]` and
  Category `Unit` or `Integration`.

## Gates

- The pinned snap7.dll source is not reachable or its hash does not match during the run: stop that
  task for an operator decision; do not switch to another source silently.

## Acceptance

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors; snap7.dll present beside the emulator and the
   test assembly; a second build does not download again.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed (CI included: the build
   downloads the DLL from GitHub).
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Emulator"` → passes,
   including: built from `ConfigFiles/RIE`, the emulator exposes DB995-999 with the managing and
   execution sizes from config, the data DB sizes from the capacity rule, and version 1.
4. Same filter → includes a test that a second emulator on an occupied port fails to start.
5. Same filter or `--filter "FullyQualifiedName~StartupOptions"` → passes, including a valid
   `--plc-endpoint`, a malformed one (startup error), its absence, and a test that the override
   reaches the `PlcConfiguration` the app registers.
6. Same filter → includes a loopback test: SemiStep's real S7 stack connects to an emulator on a
   free local port, writes an RIE recipe with group columns, reads it back equal, and the emulator
   reports the committed line count; and a write past an emulated size fails.
7. Same filter → includes run-engine tests on a fake time provider for each engine row in Flow
   branches, read back through `ExecutionStateCodec`.
8. manual smoke:
   1. Run the "PLC emulator RIE" profile → the console prints the endpoint, five DBs and "ready".
   2. Run "SemiStep against emulator RIE" → the status bar shows the PLC connected.
   3. Enable sync with a recipe loaded → the console prints the stored line count; the app shows
      synced.
   4. Type `run` in the emulator console → the app highlights rows one by one, the step timer counts,
      and the recipe is read-only while active; at the end it unlocks.
   5. Type `stop` mid-run → highlight stops and editing unlocks after about 1 s.
- not visible to headless tests: the app's row highlight and timers during a run.

---

## Context

- PLC contract and TIA behaviour: `C:/Users/admin/AppData/Local/Temp/claude/001cf81029086ef2/795b1ce6-0a14-42c9-b4e5-5964e77f44bc/scratchpad/manager-findings-20261118.md`
  (sections "PLC contract", "TIA_Plazma", "NtoLib").
- Config: `SemiStep.Core/Configuration/Facade/ConfigFacade.LoadAndValidateAsync`,
  `Configuration/Mapping/ConnectionMapper`, `Plc/Configuration/PlcConnectionSettings`,
  `Plc/S7/S7Di`, `Plc/S7/DataDbLayout`, `ConfigFiles/RIE/connection/connection.yaml`.
- Codec and analysis: `Plc/S7/Serialization/*` (`ExecutionStateCodec`, `RecipeConverter`,
  `ArrayCodec`), `Recipes/Analysis/*` (`RecipeAnalyzer`, `LoopParser`).
- Startup: `SemiStep.UI/StartupOptions.cs`, `Program.cs` (`StartupAsync`).
- CI: `.github/workflows/ci.yml`, `.github/workflows/perf.yml`.
- Launch profiles: `.run/Debug RIE.run.xml`, `.zed/tasks.json` (Zed runs tasks through PowerShell).
- Reference tool shape: `C:/Users/admin/projects/SemiSpec/SemiSpec/SemiSpec.Tools.BoardEmulator`.
- Test helpers: `SemiStep.Tests/S7/Helpers`, `ShippedConfigLocator`.

## Boundaries

- Task 2 builds the server wrapper and emulator memory (start on a port from a loaded preset, stop,
  lock-guarded access to a DB's bytes) that Task 3's engine and Task 2's loopback test use.

## Tasks

### Task 1: Emulator project and snap7.dll download

**Goal:** the solution has the emulator project, and every build has a verified snap7.dll beside the
emulator and the tests.
**Entry:** new `SemiStep/SemiStep.Tools.PlcEmulator`, added to `SemiStep.slnx`
**Likely touches:** `SemiStep.Core.csproj` (InternalsVisibleTo), `SemiStep.Tests.csproj`
(ProjectReference), `Directory.Packages.props`, `.gitignore`

- [x] project with System.CommandLine options `--config-dir`, `--port`, capacity options
- [x] build-time download of snap7.dll by pinned GitHub URL and SHA-256, cached, skipped when the
      cache matches; LGPL notice and source link in the tool folder
- [x] test project references the emulator; the DLL reaches the test output
- [x] build twice: DLL present in both outputs, second build does not download
- [x] run tests - must pass before the next task

### Task 2: snap7 server, memory and loopback round trip

**Goal:** the emulator serves the five DBs from a preset on 127.0.0.1, seeds the version, and the real
S7 stack round-trips a recipe through it.
**Entry:** the emulator project

- [x] probe the three Caveats ASSUMPTIONs first and record the results in the progress log
- [x] P/Invoke with pinned buffers and area locking; memory from the loaded preset and the capacity
      rule; version seeded; console prints endpoint, DBs, "ready"; occupied port exits 1
- [x] committed-edge detection each tick prints the line count
- [x] tests: DB sizes and version from `ConfigFiles/RIE`; occupied port; loopback round trip with an
      RIE recipe with group columns; write past an emulated size fails
- [x] run tests - must pass before the next task

### Task 3: Run engine

**Goal:** `run`/`stop`/`continue` execute the committed recipe and update the execution area like
FB Recipe.
**Entry:** the emulator's console loop

- [x] decode the committed recipe with Core's decoding and `RecipeAnalyzer`; refuse with the reason on
      any failure; copy the decoded recipe at `run`
- [x] engine on a time provider, 100 ms tick, loops unrolled, own encoder for the execution area;
      every engine row in Flow branches; startup line names the TIA export as the source
- [x] console commands `run`, `stop`, `continue`, `status`, `quit`; stdin EOF keeps serving
- [x] tests on a fake time provider for each engine row, read back with `ExecutionStateCodec`
- [x] run tests - must pass before the next task

### Task 4: Endpoint override

**Goal:** SemiStep reaches the emulator through `--plc-endpoint`; a bad value stops startup.
**Entry:** `SemiStep.UI/StartupOptions.cs`, `Program.cs`

- [x] parse `--plc-endpoint host:port` in UI with an explicit error for a malformed or missing value;
      apply it on the loaded configuration's PLC connection settings with `with`
- [x] tests: valid, malformed, absent; the override reaches the registered `PlcConfiguration`
- [x] run tests - must pass before the next task

### Task 5: Launch profiles

**Goal:** the operator starts emulator and app from Rider or Zed.
**Entry:** `.run/`, `.zed/tasks.json`

- [x] `.run` profiles "PLC emulator RIE" and "SemiStep against emulator RIE" (and a compound starting
      both if Rider supports it for these project types), using `ConfigFiles/RIE` and the endpoint
      option; matching `.zed` tasks; no parentheses in names
- [x] build once and start the emulator command line headless with stdin closed: it prints "ready"
      and keeps serving; stop it afterwards

### Task 6: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] `dotnet format SemiStep.slnx --verify-no-changes` exits 0
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 7: [Final] Update documentation

**Goal:** a developer can run SemiStep against the emulator from the docs
**Entry:** a README in the tool folder, `docs/architecture/build-and-deployment.md`

- [x] tool README: what it emulates and from which source, commands, launch profiles, other presets,
      capacity options, the snap7 licence and download
- [x] build doc: the snap7 download and cache; the `--plc-endpoint` option where startup options are
      documented (`docs/readme.md` keeps Russian)

## Post-Completion

- the operator's manual test: Acceptance smoke steps 1 to 5 (this slice ships only after it).
- later PRs: memory window, fault injection, Aspire launcher, CI integration job (#184).

**Executed by exec:**
- branch: plc-emulator-console

## Verify it yourself

1. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors; `snap7.dll` sits beside
   `SemiStep/Artifacts/bin/SemiStep.Tools.PlcEmulator/debug` and the test output.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 1502 passed, 0 failed;
   `--filter "Area=Emulator"` covers the emulator, `--filter "FullyQualifiedName~StartupOptions"` the
   endpoint option.
3. In Rider: run "PLC emulator RIE" (or the compound "Emulator and SemiStep RIE"). The console prints
   the endpoint `127.0.0.1:10102`, five DBs and `ready`.
4. Run "SemiStep against emulator RIE". The status bar shows the PLC connected.
5. Load a recipe and enable sync. The emulator console prints `committed recipe: recipe_lines=N`;
   the app shows synced.
6. Type `run` in the emulator console. The app highlights rows in turn, the step timer counts, the
   grid is read-only while active, and it unlocks about 1 s after the last row. RIE loops run once:
   the RIE preset's For action has no loop-count column.
7. Type `stop` mid-run. The highlight stops and editing unlocks after about 1 s.
8. Start SemiStep with `--plc-endpoint=garbage`. Before this branch the app silently used the
   configured PLC address; now an error window names the option and the app exits with code 1.
