# SemiStep PLC emulator

A desktop program that stands in for the PLC. It serves the PLC memory over the S7 protocol, so
SemiStep can connect, write a recipe and watch a run without hardware. The program has one window
that shows the PLC memory and runs the recipe. It is a development tool. The installer does not
ship it.

## What it emulates

The emulator loads a preset folder (`--config-dir`) and serves the five data blocks of that
preset's layout on `127.0.0.1`:

| Block | Content |
|---|---|
| managing | recipe state and commit flag; the protocol version is written at start |
| execution | the running line, step time and loop counts; the emulator writes it |
| int | recipe integer values |
| float | recipe float values |
| string | recipe string values |

- The managing DB holds the protocol version from the first moment, so the app's version check
  passes.
- The emulator stores every write the app makes. The data DB sizes come from the capacity options.
  The emulator writes the capacity into the header of each data DB (int, float, string).
- The Start button decodes the committed recipe from that memory the way the app does. The
  emulator then walks the lines and writes the execution area every 100 ms tick.
- The run engine follows the PLC logic of the TIA_Plazma export. That export may be stale.
- A step shows for its duration, rounded up to whole ticks (at least one tick). A `Pause` step
  waits for the Continue button. `StepCurrentTime` is the elapsed time of the line in seconds.
- Loops are unrolled. The loop counts are 1-based and are written to the execution area.
- After the last line or after Stop, the emulator shows the final line for 10 ticks (1 s), with
  the step time and loop counts at zero. After the last line, the final line is the line count.
  Then it clears the recipe-active flag.

## Start it

The Rider profiles and the Zed task use the MBE preset and port `10102`.

Rider run configurations in `.run/`:

- `Emulator and SemiStep MBE` is a compound. It builds the solution once, then starts both
  programs below.
- `PLC emulator MBE` starts the emulator with the MBE preset.
- `SemiStep against emulator MBE` starts SemiStep with the MBE preset and
  `--plc-endpoint 127.0.0.1:10102`.

The two single profiles do not build. They start the last build output, so run the compound or
build the solution first.

Zed task in `.zed/tasks.json`: `Run emulator and SemiStep MBE` builds the solution, then starts
the emulator and SemiStep.

From the command line, in the repository root:

```powershell
dotnet run --project SemiStep/SemiStep.Tools.PlcEmulator/SemiStep.Tools.PlcEmulator.csproj -- --config-dir ConfigFiles/MBE --port 10102
```

Options:

| Option | Default | Meaning |
|---|---|---|
| `--config-dir` | required | preset folder the app loads, for example `ConfigFiles/MBE` |
| `--port` | `102` | TCP port, 1 to 65535 |
| `--int-capacity` | `3000` | number of ints in the integer DB |
| `--float-capacity` | `6000` | number of floats in the float DB |
| `--string-capacity` | `900` | number of strings in the string DB |

Port `102` is the standard S7 port. A normal user may be unable to bind it, so the profiles use
`10102`.

If the options are wrong, the preset fails validation or the port is busy, the emulator opens an
error window with the reasons and an Exit button. The process exit code is 1.

To use another preset, change `--config-dir` to `ConfigFiles/RIE` or `ConfigFiles/MOCVD`, and pass
the same folder to SemiStep. The emulator and the app must load the same preset, or the layouts
differ.

## Connect SemiStep

Start SemiStep with `--plc-endpoint host:port`, for example `--plc-endpoint 127.0.0.1:10102`. The
option replaces the PLC address and port from `connection.yaml`. Without it, SemiStep uses the
yaml address. A malformed value stops SemiStep at startup with exit code 1.

## The window

The title shows the preset and the endpoint. The window opens when the server is up.

The top row has the run state, three buttons and the connection info:

| Item | Meaning |
|---|---|
| state | `Idle`, `Running: starting`, `Running: line N of M`, `Paused at line N: press Continue` or `Finishing: line N of M` |
| Start | runs the committed recipe. Enabled when idle |
| Stop | ends the active run. Enabled while a run is running or paused |
| Continue | releases a `Pause` line. Enabled while paused |
| Config | the preset name |
| Listening | the endpoint, for example `127.0.0.1:10102` |
| client | `SemiStep connected` or `No client` |

The list on the left has the five data blocks with their sizes. Select a block to see its fields in
the table: offset, name, type and value. The execution block is selected at start. The table
refreshes every 100 ms. A value that changed stays highlighted for 1 s. The data blocks show the
header fields (`capacity`, `current_size`) and one row per stored element.

Start refuses and shows the reason on the message line when:

- no recipe is committed;
- the committed recipe has no lines;
- the stored data does not decode, for example when the line count differs from the stored arrays
  or a string header holds a length above the configured string length;
- the recipe fails analysis: an unmatched `For` or `EndFor`, a `For` count of 0 or less, or loops
  nested deeper than 3.

## Edit memory

Double-click a value, type the new value and press Enter. The emulator writes it to the PLC memory,
as if a client had written it. Escape cancels the edit.

- Accepted input: `TRUE` or `FALSE` (also `1` and `0`) for a bool, a whole number for a DInt, a
  decimal number for a Real (the comma works as the separator), text for a string. The emulator
  removes single quotes around a string, so `'abc'` and `abc` store the same text.
- An input that does not fit the type is refused, and so is a string longer than the configured
  string length. The message line shows the reason and the memory stays as it was.
- The table is read-only while a recipe runs: a double-click does not open the editor.

## Message line

The line at the bottom shows the latest event. It turns red for an error. It shows:

- `Recipe started`, `Recipe stopped`, `Continued after the pause` and `Recipe finished`;
- `SemiStep committed a recipe: N lines` when the app sets the commit flag;
- the refusal text for each client request the server refused, for example a write past the end of
  a DB. Successful requests show nothing;
- a warning when the `current_size` of a data DB is negative or above the number of elements the
  block holds. The table then stops at the block size;
- the reason when a button action or an edit is refused;
- `The emulator tick stopped` with the reason when the 100 ms tick fails. After that the run does
  not advance. Restart the emulator.

Closing the window stops the emulator.

## Limits

- No fault injection. The emulator never drops a connection on its own. It refuses only requests
  outside a DB, as a real PLC does.
- No persistence. A restart clears all memory, including the committed recipe.
- The RIE preset's `For` action has no loop-count column, so loops run once there.

## snap7.dll

The emulator uses `snap7.dll` (x64), the native library of the Snap7 project, to serve S7.
The repository commits the binary in `native/`, next to the licence texts. The build copies it
to the output folder. It needs no network access.

- Source: the unmodified x64 build from the S7netplus repository, commit
  `9198fc168659934792d1b70e029c95a9c652f9e1`, path
  `S7.Net.UnitTest/runtimes/win-x64/native/snap7.dll`.
- SHA-256: `17336585c8f5ebe55d6db85152bfd64d5456f6c30b26ac862980af85f725072d`

Snap7 is licensed under LGPLv3. The notice and both licence texts are in
`SemiStep/SemiStep.Tools.PlcEmulator/native/`: `SNAP7-NOTICE.txt`, `LGPL-3.0-LICENSE.txt` and
`GPL-3.0-LICENSE.txt`. You can replace the DLL with a build of the Snap7 source.
