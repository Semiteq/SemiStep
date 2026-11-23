# PLC emulator window

## Behavior
- Today the PLC emulator is a console program. The operator types `run`, `stop`, `continue`,
  `status` and `quit`, and sees only log lines. After this change the emulator is a desktop window.
  It shows the memory of DB995 to DB999 live, lets the operator change values while no recipe
  runs, and starts, stops and continues a recipe with buttons. The console commands are removed.
- One launch entry starts the emulator window first, then SemiStep pointed at it, with the MBE
  config. The entry is a Rider compound profile and a Zed task. The RIE emulator entries are
  replaced by MBE entries.
- `snap7.dll` is committed to the repository with its licence files. The build no longer
  downloads it, and CI no longer needs network access for it.
- Scope guard: what the emulator does with memory stays as it is. That covers the S7 server, the
  DB layout built from the config, the protocol version seed, the decoding of the committed recipe
  in SemiStep's current packed format, and the run timeline in DB996 (`actual_line` is -1 for the
  first tick, then the 0-based row, and N after a natural end). SemiStep itself does not change.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Emulator starts with a valid config on a free port | The window opens Idle, title `PLC emulator - <preset> - 127.0.0.1:<port>`, DB996 selected | operator: approved demo | A3, smoke 1 |
| Config does not load, or the port is busy | Startup returns a failure and the window does not open | agent | A3 |
| Same, as the operator sees it | An error window names the cause. The process exits with code 1 when it is closed | agent: same pattern as SemiStep `App.RunErrorWindow` | smoke 6, smoke 7 |
| Command-line options do not parse (missing `--config-dir`, `--port 0`) | The error window lists the parser errors. Exit code 1 | agent: a WinExe has no console for System.CommandLine's stderr | A3, smoke 7 |
| SemiStep connects or disconnects | The top bar shows "SemiStep connected" or "No client" | operator: approved demo | A3, smoke 2 |
| SemiStep commits a recipe | The message line shows "SemiStep committed a recipe: N lines" | agent: replaces the console line | A3, smoke 2 |
| Start with a committed recipe | The run starts. The state reads "Running: starting" for the first tick, then "Running: line K of N" with K = `actual_line` + 1. DB996 changes and the changed rows flash | operator; agent: K per phase | A3, smoke 3 |
| Start with no committed recipe, or a recipe that does not decode | The run does not start. The message line names the reason in red | agent: the refusals the engine already returns | A3 |
| The run reaches a Pause step | The state reads "Paused at line K: press Continue". Continue is enabled | operator | A3 |
| Stop during a run or a pause | The state reads "Finishing: line K of N" with the stopped line. After the hold of about 1 s, Idle | operator | A3, smoke 4 |
| The last step ends | The state reads "Finishing: line N of N", with K clamped to N. The table shows `actual_line` = N. Then Idle and the message "Recipe finished" | operator; agent: frozen timeline | A3, smoke 3 |
| Operator edits a value while Idle | Enter writes the value to the PLC memory. The message line shows the new value. Every field is editable, `capacity` and `version` included | operator | A3, smoke 5 |
| Operator types a value the type does not accept, or a string longer than the configured length | Nothing is written. The message line names the field and the expected format in red | operator | A3, smoke 5 |
| A cell is in edit mode when the refresh runs | The refresh does not overwrite the cell until Enter or Escape | agent | A3 |
| A run is active (Running, Paused or Finishing) | The table is read-only. The subtitle reads "Values are read-only while a recipe runs." | operator | A3 |
| `current_size` of an array changes | The array rows follow: capacity, current_size, then `data[0]` up to `current_size - 1` | operator | A3 |
| `current_size` is negative or above the allocated capacity | The rows stop at the allocated capacity (none when negative). A red message names the value | agent: the refresh must never read past the buffer | A3 |
| snap7 refuses a client request | The message line shows the snap7 text in red | agent: replaces the console line | A3 |
| Operator closes the window | The engine tick and the refresh stop first, then the S7 server stops. Exit code 0 | agent | A3, smoke 8 |

## Decisions
- Avalonia window in the emulator project, with the same stack as SemiStep: Avalonia, Semi theme,
  ReactiveUI. Reason: one toolkit in the repository, and headless tests already work for it.
  Rejected: a web page, because it is a second toolkit for one tool.
- The window replaces the console completely. `EmulatorConsole`, the command parsing of typed
  lines, `RunEngine.Status()` and their tests are deleted. Reason: tests start the emulator
  in-process and call the engine directly, so nothing depends on the console. Rejected: keeping a
  `--headless` console mode, because it has no user.
- `EmulatorLoop` stays the owner of the 100 ms tick. It loses the console and its `TextWriter`,
  and emits typed events instead of text lines. Reason: the tick, the commit detection and the
  failed-request reporting already live there, and the engine tests drive it on a fake clock.
- UI text is English literals, with no resource set. Reason: a developer tool with one operator.
  Rejected: localization through resx, because no second language is needed.
- The window carries its own resources (palette keys `ChangedValueBrush` `#FFCC80`,
  `ErrorTextBrush` `#DB3B4B`, a monospace font key, the state brushes). Reason: the window then
  looks the same under any `Application`, including the test host, which runs `SemiStep.UI.App`.
  The Semi theme and the DataGrid theme come from the emulator's `App.axaml`.
- `snap7.dll` (x64, 244,224 bytes, SHA-256
  `17336585c8f5ebe55d6db85152bfd64d5456f6c30b26ac862980af85f725072d`) is committed in
  `SemiStep/SemiStep.Tools.PlcEmulator/native/`. `Snap7.targets` and the download are deleted.
  Reason: no NuGet package carries a trusted snap7 server, and the download needs custom build
  logic and network access in CI. Rejected: Snap7Server.Net 1.0.1 (2022, no licence, no source
  link), S7Mock (no native DLL), HslCommunication (commercial licence), PlcSimulator.Core (5 months
  old, one author), and our own S7 server (about 300 to 400 lines of protocol code to maintain).
  `.gitattributes` already marks `*.dll` as binary. The folder is not named `x64`, `bin` or
  `Release`, because `.gitignore` ignores those names anywhere.
- The licence files sit next to the DLL: `SNAP7-NOTICE.txt` (what the file is, the copyright,
  where the source is), `LGPL-3.0-LICENSE.txt` and `GPL-3.0-LICENSE.txt`. Reason: the repository is
  public, so committing the DLL distributes it. The LGPLv3 is a set of additions to the GPLv3, so
  both texts travel with the library.
- The launch entry builds once, then starts both programs without building. Reason: two parallel
  builds of projects that share `SemiStep.Core` write the same `obj` outputs.
- The state text counts lines from 1. The table shows the raw `actual_line` value. Reason: the
  title is for a person, and the table shows memory as it is.
- Startup failures are tested as a failed result only (`IsFailed`). No new error type is added,
  because no production code branches on the kind of failure; the error window shows text.
- `--help` is a non-goal: a WinExe has no console to print it to. The README lists the options.
- The emulator config stays as before: the preset folder from `ConfigFiles/`, with SemiStep's
  `--plc-endpoint` override pointing at the emulator.

## Caveats
- Headless tests run under `SemiStep.UI.App` (`SemiStep.Tests/TestAppBuilder.cs`, one
  `AvaloniaTestApplication` per assembly). The emulator's `App.axaml` never loads in tests, so
  its theme includes are checked by smoke 1 only. A program that starts its own Avalonia lifetime
  cannot run inside the test process, so startup is tested up to the point where it would open a
  window.
- Avalonia's Win32 platform must start on the STA main thread. `SemiStep.UI/Program.cs` uses a
  synchronous `[STAThread] int Main` and finishes async config loading before Avalonia starts. The
  emulator follows the same pattern. Today's `Task<int> Main` with `InvokeAsync` resumes on a
  thread-pool thread after the config load and cannot host the window.
- snap7 reports client connects and disconnects as server events. Today
  `Snap7Server.PickFailedRequestText` drains the event queue and drops every event without an
  error, connect events included. One reader must drain `Srv_PickEvent` and sort each event.
  ASSUMPTION: `Srv_PickEvent` returns a client-added and a client-disconnected event code;
  fallback: read the client count with `Srv_GetStatus` on each tick; checked by: Task 2 tests (an
  S7.Net client connects and disconnects, and the reported client state follows).
- snap7 writes into the pinned DB buffers from its own threads. Every read by the window takes the
  area lock, as `EmulatedPlc.Read` and `ReadBlocks` do now. A DB999 read copies about 119 KB, so
  the refresh reads only the shown block plus DB995.
- `EmulatedPlc.Dispose` destroys the snap7 handle. A tick or a refresh that runs after it calls
  `Srv_LockArea` on a destroyed handle, which crashes the process. Closing stops both first.
- Avalonia requires bound property changes on the UI thread, and the tick runs on a timer thread.
  State published to the window crosses to the UI thread first (`~/.claude/rules/avalonia.md`).
- Window tests advance the tick, the refresh and the flash expiry on a fake clock, with no
  wall-clock waits. Posted UI updates need the `RunJobs` exception from
  `docs/architecture/headless-ui-testing.md`.
- ASSUMPTION: two parallel `dotnet run` builds of the emulator and SemiStep collide on shared
  `obj` outputs; fallback: none needed, building once is correct either way; checked by: smoke 1.
- ASSUMPTION: under `pwsh -C`, `Start-Process` returns at once and the Zed task continues to
  SemiStep; fallback: start SemiStep with `Start-Process` too; checked by: smoke 1.
- ASSUMPTION: a Rider compound whose emulator profile has no build step, plus one shared build
  step, starts both without a build clash; fallback: one shell profile that runs the same commands
  as the Zed task; checked by: smoke 1.

## Constraints and non-goals
- No change to SemiStep (`SemiStep.UI`, `SemiStep.Core`) and no change to the wire format. #174
  is out of scope.
- No process values (flows, temperatures), no `SendEnabled`, no time speed-up, no edits during a
  run, no fault injection, no Aspire, no `--help` output.
- No new threat model. The emulator listens on 127.0.0.1 only, as before.
- The run engine and the recipe decoding keep their behavior. Existing engine and loopback tests
  stay green with only the changes the console removal forces.
- Tests stay as simple as possible. No speculative abstractions.

## UI design
Approved demo: `C:\Users\admin\AppData\Local\Temp\claude\001cf81029086ef2\795b1ce6-0a14-42c9-b4e5-5964e77f44bc\scratchpad\emu-ui-demo`
(run with `dotnet run --project <that folder>\EmuUiDemo.csproj`; screenshots in `shots\`). The
scratchpad can be cleared, so the layout is restated here.

- Window: resizable, opens at 1100 x 720, minimum 820 x 480, background `SemiColorBackground1`,
  margin 16. Font and theme tokens as in `SemiStep.UI/App.axaml` (Semi theme, JetBrains retint,
  `ChromeFontFamily`, size 12).
- Top bar, one row:
  - left: a state glyph (square when Idle, triangle otherwise; grey Idle, green otherwise) and the
    state text, semibold 14, minimum width 260. The texts are listed in the Flow branches.
  - 24 px gap, then the buttons Start (primary solid), Stop and Continue, each 32 high and at
    least 88 wide, 8 apart. Start is enabled only when Idle. Stop is enabled when Running or
    Paused. Continue is enabled only when Paused.
  - right: "Config: MBE", "Listening: 127.0.0.1:10102" and the client indicator, 16 apart. Labels
    are secondary text and values primary text. The indicator is green "SemiStep connected" or
    secondary "No client".
- 16 px gap, then two panes 16 apart, white with a 1 px `SemiColorBorder` border and radius 4:
  - left pane, 220 wide: a list of the five DBs. Each item shows "DB995" semibold plus the block
    title on the first line, and the size ("10 bytes") in secondary text on the second line.
  - right pane, fills the rest, padding 16: a title "DB996 Execution" (semibold 14), a subtitle
    line (secondary), then the table. The subtitle reads "Double-click a value to change it.
    Enter writes it to the PLC memory." when Idle, and "Values are read-only while a recipe runs."
    otherwise.
  - table: `DataGrid` with the columns Offset (90), Name (200), Type (110) and Value (fill, minimum
    200). Row height 28, horizontal grid lines only, no sorting, no cell focus frame. Offset and
    Value use the monospace font. Offset reads `<byte>.0`. Types: Bool, Byte, DInt, Real, WString.
    Values: `TRUE`/`FALSE`, whole numbers, reals with 1 to 3 decimals, strings in single quotes.
  - names: DB995 `version`, `committed`, `recipe_lines`. DB996 `recipe_active`, `actual_line`,
    `step_current_time`, `for_loop_count1` to `for_loop_count3`. DB997 to DB999 `capacity`,
    `current_size`, then `data[i]`. Block titles: Managing, Execution, Int array, Float array,
    String array. Offsets and DB numbers come from the loaded layout; the names map one-to-one to
    the layout fields.
  - a changed row: every cell of the row gets the `ChangedValueBrush` background (`#FFCC80`, the
    colour SemiStep uses for changed cells) for 1 s after its value changes.
- 8 px gap, then a reserved message line, 40 high, secondary text, `ErrorTextBrush` for errors. It
  shows the latest event or error. Every colour comes from a palette key.

## Gates
- none

## Acceptance
1. `git clone . <scratch>/clone && dotnet build <scratch>/clone/SemiStep.slnx` → 0 warnings,
   0 errors, and no `snap7.dll: downloading` line.
2. `git ls-files SemiStep/SemiStep.Tools.PlcEmulator/native` → `snap7.dll`, `SNAP7-NOTICE.txt`,
   `LGPL-3.0-LICENSE.txt`, `GPL-3.0-LICENSE.txt`.
   `sha256sum SemiStep/SemiStep.Tools.PlcEmulator/native/snap7.dll` →
   `17336585c8f5ebe55d6db85152bfd64d5456f6c30b26ac862980af85f725072d`.
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Emulator"` → all pass.
   These tests cover every Flow branch marked A3, headless and in-process.
4. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
5. `git grep -n -e Snap7.targets -e DownloadFile -e EmulatorConsole -- SemiStep docs .github ':!docs/plans'`
   → no output.
6. Manual smoke:
   1. Run "Emulator and SemiStep MBE" in Rider, or "Run emulator and SemiStep MBE" in Zed → the
      emulator window opens, then SemiStep opens with no build error.
   2. In SemiStep, load a three-step recipe and enable sync → the emulator shows "SemiStep
      connected" and the message "SemiStep committed a recipe: 3 lines".
   3. Press Start → the state counts "line 1 of 3" to "line 3 of 3". DB996 rows flash. SemiStep
      highlights the same rows. After the end, Idle and "Recipe finished".
   4. Press Start, then Stop mid-step → Idle after about 1 s. SemiStep unlocks editing.
   5. While Idle, set DB998 `data[1]` to `5` and press Enter → the message shows the new value.
      Type `abc` into a DInt → a red message names the field. Nothing changes.
   6. With one emulator running, run
      `(Start-Process <emulator exe> -ArgumentList '--config-dir ConfigFiles/MBE --port 10102' -Wait -PassThru).ExitCode`
      in PowerShell → an error window names the busy port. After it is closed, the command prints
      `1`.
   7. Run the same command with `--config-dir C:/missing`, and once with no `--config-dir` → an
      error window names the cause. The command prints `1`.
   8. Close the emulator window → the process exits without a crash. SemiStep shows the PLC
      disconnected.
- Not visible to headless tests: the real window look and the emulator's theme includes, the
  launch entries, the Rider compound build order, the startup error window and the exit codes.

---

## Context
- Emulator project: `SemiStep/SemiStep.Tools.PlcEmulator/`. Kept: `EmulatedPlc` (server plus
  locked memory access), `PlcMemory`, `Snap7Server`/`Snap7Native`, `RunEngine` (typed refusals
  `RunRefusal`/`RunRefusalReason`), `RunPlan`, `CommittedRecipeLoader`, `ExecutionAreaEncoder`,
  `EmulatorLoop` (as the tick owner). Deleted: `EmulatorConsole`. Rewritten: `Program` (console
  entry and `ServeAsync`). `EmulatorCommand` keeps the System.CommandLine options: `--config-dir`
  required, `--port` default 102, the capacities.
- `RunEngine` writes progress text to a `TextWriter` and has a `Starting` phase before `Running`.
  The window needs the phase, the line and the line count as state.
- Tests that depend on the text output or the console: `RunEngineHarness.Output`,
  `EmulatorLoopbackTests.CountServerEvents` (parses `snap7:` lines), `EmulatorServeTests`,
  `EmulatorConsoleTests`, `EmulatedPlcTests.Main_PortInUse_ExitsWithCodeOne` (calls `Program.Main`
  in-process). `EmulatedPlcTests.Start_PortInUseByAnotherEmulator_Fails` already covers the busy
  port at the `EmulatedPlc` level.
- SemiStep startup pattern: `SemiStep.UI/Program.cs` (STA entry, async load first) and
  `App.RunErrorWindow` in `SemiStep.UI/App.axaml.cs`. Theme tokens: `SemiStep.UI/App.axaml`.
  Changed-cell colour: `docs/architecture/cell-change-highlight.md`.
- Headless UI tests: `[AvaloniaFact]`, see `docs/architecture/headless-ui-testing.md`. Error
  assertions check types, not wording. The fake clock in tests: `RunEngineHarness`.
- Launch entries: `.run/*.run.xml` (`PLC emulator RIE`, `SemiStep against emulator RIE`,
  `Emulator and SemiStep RIE`) and `.zed/tasks.json` (`Run PLC emulator RIE`,
  `Run UI against emulator RIE`). Zed tasks run through `pwsh -C`.
- Docs to keep in sync: `SemiStep/SemiStep.Tools.PlcEmulator/README.md`,
  `docs/architecture/build-and-deployment.md` (section "PLC emulator and snap7.dll"),
  `docs/readme.md` 6.1 and the root `readme.md` (both Russian).
- House rules: `~/.claude/rules/avalonia.md` and `csharp.md`. Compiled bindings with `x:DataType`,
  commands with an observable `canExecute`, one class per file.

## Boundaries
- Task 2 gives Task 3 the run state (phase, current line, line count) and a stream of typed
  events (recipe committed with its line count, request refused with the snap7 text, client
  connected, client disconnected), both published by `EmulatorLoop`.

## Tasks

### Task 1: Commit snap7.dll and remove the download

**Goal:** the build copies a committed `snap7.dll` and never downloads it
**Entry:** `SemiStep/SemiStep.Tools.PlcEmulator/Snap7.targets`
**Likely touches:** the emulator `.csproj`, `Snap7.targets`, `SNAP7-NOTICE.txt`, `Snap7Native.cs`
(its summary names `Snap7.targets`), the new `native/` folder

- [x] check the SHA-256 of `SemiStep/Artifacts/snap7/snap7.dll`, then add it to `native/` with
      `SNAP7-NOTICE.txt` (moved and rewritten for the committed file), `LGPL-3.0-LICENSE.txt` and
      `GPL-3.0-LICENSE.txt`
- [x] copy the DLL to the output of the emulator and of every project that references it. Delete
      `Snap7.targets`
- [x] tests: after `rm -rf SemiStep/Artifacts/snap7 SemiStep/Artifacts/bin SemiStep/Artifacts/obj`,
      `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Emulator"` passes
- [x] run tests - must pass before the next task

### Task 2: Typed run state and events from the tick

**Goal:** the window can read the run state and the events without parsing text
**Entry:** `EmulatorLoop`, `RunEngine`, `Snap7Server.PickFailedRequestText`
**Likely touches:** `EmulatorLoop.cs`, `RunEngine.cs`, `Snap7Server.cs`, `Snap7Native.cs`, new
small types for the state and the events, `RunEngineHarness.cs`, `RunEngineTests.cs`,
`EmulatorLoopbackTests.cs`, `EmulatorServeTests.cs`

- [x] the engine exposes its phase, current line and line count. Start, Stop and Continue keep
      their typed results. Its `TextWriter` and `Status()` are deleted
- [x] `EmulatorLoop` stays the tick owner, without the console. One reader drains the snap7 event
      queue and sorts each event into request refused, client connected and client disconnected.
      The loop publishes these and the recipe commit as typed events
- [x] the state-text rule from the Flow branches (K per phase: starting, running, paused,
      finishing after Stop, finishing after the end clamped to N) lives next to the state, not in
      the view
- [x] tests: the three-step timeline gives the right phase and K for each tick, including the
      first tick, Pause, Stop and the end. Each event arrives once. An S7.Net client connect and
      disconnect is reported. The harness and loopback tests assert on typed events, not text
- [x] run tests - must pass before the next task

### Task 3: Emulator window with the memory table

**Goal:** the window from `## UI design`, with the flow branches for viewing, editing and run
control, tested headless
**Entry:** a new window and view model in the emulator project
**Likely touches:** the emulator `.csproj` (Avalonia and Semi packages), the window, its resource
dictionary (palette keys and the monospace font), view models for the window, a DB and a row

- [x] the window, its own resources and the layout as the UI design states
- [x] the table rows come from the loaded layout. Arrays follow `current_size`, clamped to the
      allocated capacity. Values refresh every 100 ms under the area locks. A cell in edit mode is
      not overwritten. Changed rows flash for 1 s
- [x] editing works only while Idle. A valid value is written with the area lock. An invalid value
      or an over-long string writes nothing and shows the error on the message line
- [x] Start, Stop and Continue call the engine. Their enabled state follows the phase. Refusals
      and the events from Task 2 show on the message line. The client indicator follows the events
- [x] tests (headless, in-process emulator, fake clock): each Flow branch marked A3 that the window
      owns
- [x] run tests - must pass before the next task

### Task 4: Startup, shutdown and console removal

**Goal:** the program opens the window or an error window, closes cleanly, and the console is gone
**Entry:** `Program.cs`, `EmulatorConsole.cs`
**Likely touches:** the emulator `.csproj` (WinExe), `App.axaml` with the Semi and DataGrid
themes, an error window, `Program.cs`, `EmulatorCommand.cs`, `EmulatorConsole.cs`,
`EmulatorConsoleTests.cs`, `EmulatedPlcTests.cs` (`Main_PortInUse_ExitsWithCodeOne` is replaced),
`EmulatorCommandTests`

- [x] startup follows `SemiStep.UI/Program.cs`: a synchronous STA entry parses the options and
      loads the config before Avalonia starts. A parser, config or port failure opens the error
      window and exits with code 1. The options keep their names and defaults
- [x] closing the main window stops the tick and the refresh, then disposes the server. Exit code 0
- [x] delete the console, its command list and its tests
- [x] tests: the startup step before the window returns a failure for a bad config dir, a busy
      port and unparsable options (`IsFailed`). After close, the server is disposed and no tick
      runs
- [x] run tests - must pass before the next task

### Task 5: One launch entry for MBE

**Goal:** one entry in Rider and one in Zed start the emulator, then SemiStep, with MBE
**Entry:** `.run/Emulator and SemiStep RIE.run.xml`, `.zed/tasks.json`
**Likely touches:** the three RIE emulator profiles in `.run/`, `.zed/tasks.json`

- [x] replace the RIE emulator profiles with MBE ones named "PLC emulator MBE", "SemiStep against
      emulator MBE" and the compound "Emulator and SemiStep MBE". The compound builds once and
      starts both without a build clash
- [x] replace the two Zed emulator tasks with one task "Run emulator and SemiStep MBE" that builds
      the solution once, starts the emulator window without waiting, then runs SemiStep with
      `--plc-endpoint 127.0.0.1:10102` and the MBE config, both without building
      (Zed task proven by running its command through `pwsh -NoProfile -Command`; Rider launch
      skipped - not automatable)
- [x] run tests - must pass before the next task

### Task 6: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance 1 to 5 pass with their expected results (5 lists the stale README and architecture mentions that Task 7 rewrites)
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 7: [Final] Update documentation

**Goal:** docs describe the window, the committed DLL and the MBE launch entry
**Entry:** `SemiStep/SemiStep.Tools.PlcEmulator/README.md`

- [x] the emulator README: the window, the buttons, editing, the message line, the startup
      options, snap7 and its licence. Drop the console sections
- [x] `docs/architecture/build-and-deployment.md`: the committed DLL replaces the download
- [x] `docs/readme.md` 6.1 and the root `readme.md` (Russian): the launch entry and the window
- [x] CLAUDE.md only if a convention changed (no convention changed; no edit)

## Post-Completion
- the operator's manual test: Acceptance 6, smoke steps 1 to 8
- the branch stays local. Shipping waits for the operator

**Executed by exec:**
- branch: plc-emulator-console

## Verify it yourself
1. Close any running emulator and SemiStep. In Zed run "Run emulator and SemiStep MBE", or in Rider
   "Emulator and SemiStep MBE" → the emulator window "PLC emulator - MBE - 127.0.0.1:10102" opens,
   then SemiStep opens. The Rider compound is untested; if it builds nothing, run "Build Solution"
   first, then the compound.
2. In SemiStep load a three-step recipe and enable sync → the emulator top bar shows "SemiStep
   connected" and the message line "SemiStep committed a recipe: 3 lines". Edit the recipe so it
   syncs again → the message appears again (before `ac45b29` only the first commit showed; the test
   `WriteRecipe_TwiceThroughRealS7Stack_ReportsEachCommitOnce` proves it).
3. Press Start → the state counts "Running: line 1 of 3" to "line 3 of 3", DB996 rows flash orange,
   SemiStep highlights the same row. At the end "Finishing: line 3 of 3", then "Idle" and "Recipe
   finished". The `actual_line` row shows 3 after the end.
4. Press Start, then Stop mid-step → "Finishing" at the stopped line, Idle after about 1 s, SemiStep
   unlocks editing.
5. While Idle, select DB998, double-click `data[1]`, type `5`, Enter → the message line shows the new
   value. Type `abc` into a DInt → a red message names the field and nothing changes. Set DB997
   `current_size` to `5000` → a red warning stays on the message line (fixed in `5975993`, test
   `Edit_CurrentSizeOutsideTheBuffer_KeepsTheRedMessage`).
6. Start a field edit, then click another DB in the list → the edit goes to the DB it started in (fixed
   in `5975993`, test `View_SwitchingTheBlockMidEdit_WritesTheEditToTheEditedBlock`). Ctrl+click the
   selected DB → the window stays open with a DB selected.
7. With one emulator running, in PowerShell:
   `(Start-Process SemiStep/Artifacts/bin/SemiStep.Tools.PlcEmulator/debug/SemiStep.Tools.PlcEmulator.exe -ArgumentList '--config-dir ConfigFiles/MBE --port 10102' -Wait -PassThru).ExitCode`
   → an error window names the busy port; after Exit the command prints `1`. Repeat with
   `--config-dir C:/missing` and with no `--config-dir` → the same, `1`.
8. Close the emulator window → it exits without a crash; SemiStep shows the PLC disconnected.
- Automated: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Emulator"`
  covers every Flow branch marked A3.
