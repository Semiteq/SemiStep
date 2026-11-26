# UI grid palette

## Behavior

- Today the running step is one orange `#FF8800` cell (the № cell, or the step header and a marker
  strip when transposed). Past rows get grey-blue `*_past` fills, loop depths get blue fills, and
  the run's tints vanish when the run ends. Selection paints an opaque fill (`#CCE4F7`, or one of
  the three `#67` variants on grey and orange cells). Read-only and inapplicable cells have grey
  text. The focus frame is black in the canonical grid; the transposed grid has none.
- After the change every cell colour comes from one rule with four layers: the base (white, grey
  or orange), then the violet loop tint, then the execution green, then the selection. Each layer
  blends over the one below; no layer fills.
- The whole running row is green-tinted and its № cell shows a black "►". Every past row has a
  lighter green tint. There is no left bar and no orange marker.
- When the run ends normally, every row keeps the past tint and the "►" goes away. When the run
  stops early, the past tint stays and the row that was running shows "■". The tints and the "■"
  clear on the next recipe edit or load made while no run is active, and at the next run start.
  Saving does not clear them.
- All cell text is black, including read-only, inapplicable and selected cells.
- The focused cell has a 2 px `#3574F0` frame in both grids.
- While a run is active, the grid scrolls to keep the running step in view. When the operator
  scrolls, auto-scroll pauses for 10 s and then returns to the running step. Auto-scroll never
  moves the selection or the focus.
- An empty recipe shows one centred line in the grid: "Рецепт пуст. Добавьте шаг (Ctrl+N) или
  откройте файл (Ctrl+O)."
- Scope guard: the D7 marking rule (which cells turn orange) belongs to batch A, marks surviving
  undo belong to their own branch, the status bar to batch B, the cell templates and headers to
  batch C. Past rows stay "index below the running line", as on master. The grid lock during a
  run does not change.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Run starts | reset marks clear (as on master); running row green 0.30 + "►"; rows above it green 0.12 | operator: D1a | A3, S2 |
| Running line advances | previous running row turns past; new row turns running; "►" moves | operator: D1a | A3 |
| Loop jumps back to an earlier line | rows from the new line down lose the past tint (master: `ExecutionHighlightTracker`, index below the line) | manager: autonomous-decisions.md item 7 | A3 |
| Run goes inactive, last active line >= step count N | run ended: every row past tint, no glyph | manager: autonomous-decisions.md item 15 (DB996 semantics) | A4, S4 |
| Run goes inactive, last active line in 0..N-1 | run stopped on that row: rows above it past, that row past + "■" | manager: autonomous-decisions.md item 15 | A4, S5 |
| Run goes inactive, last active line N-1 | stop on the last step: "■" on the last row | manager: autonomous-decisions.md item 15 | A4 |
| Run goes inactive, last active line -1 | no glyph | manager: autonomous-decisions.md item 15 | A4 |
| Link loss or sync off during a run | `PlcExecutionMonitor` publishes `PlcExecutionInfo.Empty` (inactive, line 0); it reads as a stop on the last line seen while active, never on Empty's line 0 | manager: autonomous-decisions.md item 15 | A4 |
| Reconnect while the PLC still runs | `RecipeReplaced`, then the surface's full rebuild, then the tracker reset; the next execution emission repaints running, past and "►" | agent: `RecipeGridSurfaceBase` reset path on master | A4 |
| Edit or load after a run, no run active (cell edit, add, delete, paste, action change, undo, redo, `RecipeReplaced`) | every execution tint and the "■" clear | operator: D1a "until the next edit"; manager: autonomous-decisions.md item 18 | A4 |
| Save after a run (`StateRefreshed`) | tints and "■" stay | manager: autonomous-decisions.md item 18 | A4 |
| Action change after a run | the past tint clears in the same mutation; the reset cells turn orange; orange and green never show together | operator: D1, D1a | A4 |
| Cell in a loop, idle | violet tint over its base (white `#F5F1FB` / `#EAE3F6` / `#DDD1F1` at depth 1 / 2 / 3); a depth above 3 uses depth 3 | operator: D1a; agent: clamp as master | A1, A2 |
| Grey cell in a running or past row | full weights (factor 1.0): inapplicable `#A6C6AF` running, `#C9D6CC` past | operator: D1a correction | A2 |
| Marked (orange) cell in a read-only or inapplicable column | orange is the base | agent: master's `TransposedCellBackgroundConverter` order | A2 |
| Orange cell in a selected row | selection blends over orange: `#CDB69C` | operator: D5d | A2 |
| Selected cell in the running row | `#9AC2D0`, green still readable under the blue | operator: D1a | A2, S3 |
| Operator presses a navigation key in the grid during a run (Up, Down, PageUp, PageDown, Home, End, each also with Ctrl), turns the wheel or drags the scrollbar | auto-scroll pauses for 10 s whether or not the view moved; each further input restarts the 10 s | operator: D5h; agent: key list | A6, S6, S7 |
| 10 s pass with no operator input, run still active | grid scrolls to the running step; selection and focus stay where they were | operator: D5h | A6, S6 |
| Run ends while paused | pause ends; nothing scrolls | agent: no running step to follow | A6 |
| Programmatic scroll (auto-scroll, selection after paste) | does not pause | agent | A6 |
| Recipe has no steps, either orientation | centred hint line; hint gone after the first step | operator: D6d | A7, S1 |
| Empty recipe with the grid focused | Ctrl+N adds a step and Ctrl+O opens a file; the hint does not take focus or input | agent: hint is not hit-testable and not focusable | A7, S1 |
| CSV load with 0 steps / with N steps | 0 steps shows the hint; an N-step load never shows it, not even for one frame during the rebuild | agent: the empty flag is set once per mutation | A7 |
| Orientation flip or editing with the hint present | `RecipeGridHost` still swaps `Content` and reports `IsEditing` from the active view | agent: hint lives outside `Content` | A7 |
| Cell gets keyboard or mouse focus, either orientation | 2 px `#3574F0` frame; row height and column widths unchanged; read-only transposed cells are not focusable and show no frame | operator: D5f | A5, S3 |
| Site `grid_style.yaml` without the new keys | loads; the new keys take the Core default values; deleted keys are ignored silently | agent: `IgnoreUnmatchedProperties`, see Decisions | A8 |
| A weight outside 0..1 in `grid_style.yaml` | startup stops with `GridStyleWeightOutOfRangeError` naming section and key | agent: mapper validation pattern | A8 |

## Decisions

- Computed opaque colours (manager: autonomous-decisions.md item 9). The palette installer
  computes every cell colour once per config load and puts a table of 96 brushes (4 bases x 4
  depths x 3 execution states x 2 selection states) into the application resource dictionary
  under one key. Both grids resolve a cell's brush from that table through the host's resource
  lookup, as `TransposedCellBackgroundConverter` does today. No static mutable state. This
  replaces the last-match-wins cascade of 40 rules in `DataGridStyles.axaml` and the key switch
  in `TransposedCellBackgroundConverter`. Rejected: a semi-transparent overlay `Border` per cell
  (adds a visual to every realised cell, misses the approved values by up to 2 per channel, and
  headless tests cannot read a composited colour back); a layered brush (Avalonia 12 paints one
  `Background` per control, so stacked fills need a change to Semi's `DataGridCell` template);
  96 AXAML selectors (order-dependent and unreviewable).
- Blend arithmetic (manager: autonomous-decisions.md item 16): integer per channel, weights in
  thousandths, rounded half up: `(base x (1000 - w) + colour x w + 500) / 1000`. The result does
  not depend on operand order or floating point. It shifts some approved hexes by 1 per channel
  from the designer's double-precision truncated values (white running `#BCDCC5` against
  `#BCDBC4`); a 1-step shift is not visible.
- Layer order: base, loop tint, execution green, selection. Base priority: orange (marked), then
  inapplicable grey, then read-only grey, then white (`chrome.grid_background`). Orange and green
  never meet in the app: run start clears the marks, and the first edit after a run clears the
  tints. The table still holds the orange + past and orange + running colours, so no state falls
  through.
- Grey-cell factor 1.0: grey cells take the same weights as white cells. No factor key exists.
- Config keys, all under `colors:`:
  - new: `cells.execution.running` `#208A3C`, `cells.execution.running_weight` 0.30,
    `cells.execution.past_weight` 0.12, `cells.loop.color` `#7D4DC8`,
    `cells.loop.depth_1_weight` 0.08, `depth_2_weight` 0.16, `depth_3_weight` 0.26,
    `selection.accent` `#3574F0`, `selection.weight` 0.25.
  - deleted: every `execution.depth_*` and `depth_*_past` key, `current_step_marker`; `readonly`
    and `disabled` `depth_1..3`, `depth_*_past`, `selected`, `foreground`; `cells.changed_selected`;
    `selection.background` and `selection.foreground`.
  - required (as today): `readonly.depth_0`, `disabled.depth_0`.
  - optional with Core defaults: `cells.changed`, `grid_line`, every new key. The
    `cells.execution` section becomes optional; a missing section no longer reports
    `GridStyleSectionMissingError`.
  - A weight outside 0..1 reports a new `GridStyleWeightOutOfRangeError(section, key, value)`,
    accumulated with the other per-key errors.
  - The selection colour gets a new name (`accent`), so a site file's old `selection.background`
    `#CCE4F7` is ignored instead of being blended at 0.25, where it would be nearly invisible.
  - `GridStyleOptions.Default` holds the approved values, because it now renders for old site
    files; its "test-only placeholder" comment and the `#000000` cell placeholders go. Hard to
    reverse once sites save files with the new names.
- All cell text black: the grey foreground setters and their keys go, and selected cells keep the
  default black text (manager: autonomous-decisions.md item 8).
- "Next edit" is any step-graph signal (`StepAppended`, `StepsInserted`, `StepRemoved`,
  `StepsRemoved`, `StepActionChanged`, `PropertyUpdated`) or `RecipeReplaced`, received while no
  run is active. `StateRefreshed` (save) does not count. Undo and redo arrive as these signals, so
  a restored orange mark never lands on a past row.
- End vs stop comes from the last line seen while the run was active (manager:
  autonomous-decisions.md item 15). A later inactive emission's own line is ignored.
- The transposed running column: its step header shows "►" (or "■" on the stop column) before the
  step number, on the normal header background. Its cells carry the green tint. The marker strip
  goes.
- Operator scrolling is detected from input, never from offset changes: the navigation keys listed
  in Flow branches, the pointer wheel over the grid, and a scrollbar drag. A click that selects a
  cell does not pause. When the pause expires during a run, the grid scrolls to the running step
  at once. Auto-scroll only scrolls; it never sets selection, current cell or focus.
- The focus frame is a stated project exception to the desktop-ui rule "In a DataGrid hide the
  cell focus visual": the recipe grid is an editor. The exception goes into
  `docs/architecture/recipe-grid-surface.md`.
- Empty hint source: the surfaces get a notifying `IsRecipeEmpty` member, set once at the end of
  each mutation the surface applies (not per `Items` change), and `ActiveRecipeGridSurface`
  forwards the active one. Its consumer is the hint in `RecipeGridHost.axaml`, layered beside the
  host's `Content` so the orientation swap and `IsEditing` keep working. The hint uses
  `SecondaryForegroundBrush`, is neither hit-testable nor focusable, and its text comes from resx.
- The nested-loop recipe for smoke checks is checked in as a test fixture (manager:
  autonomous-decisions.md item 17): `SemiStep/SemiStep.Tests/TestData/Recipes/loops-timeleft.csv`,
  copied from the manager's `loops-timeleft.csv` (9 rows, separator ";").

## Caveats

- Blend mechanism. probed: code read and arithmetic, 2026-11-25.
  - Avalonia `Border` and `DataGridCell` paint a single `Background` brush; nothing composes two
    fills on one control without an extra visual.
  - A Python script with exact fractions recomputed every table entry under the integer rule and
    agreed with an integer implementation on all 96 entries. A separate script showed that one
    overlay colour with alpha reproduces the stack only within 2.04 per channel.
  - Headless tests run without Skia (`TestAppBuilder` uses `UseHeadless` with default options), so
    a test reads the cell's `Background` as `ISolidColorBrush` and compares its `Color`. Avalonia
    has no frozen brushes; the table holds immutable brushes created once per load.
  - The transposed grid already resolves each realised cell's brush through a 6-input
    `MultiBinding` (`TransposedColumnCellsPresenter`); the running state becomes a seventh input.
    The canonical `DataGrid` virtualises rows, so only realised cells (about 30 rows) carry
    bindings.
  - ASSUMPTION: a table lookup per realised cell plus master's loop over all rows on a line change
    scales with the realised viewport, not with the step count; fallback: if the Gate fails, keep
    the table but drive the row-level layers from row classes instead of a per-cell binding;
    checked by: the Gate.
- Auto-scroll detection. probed: code read, 2026-11-25. Master has no auto-scroll in the
  canonical grid. The transposed `TransposedColumnsPanel.ScrollIntoView` runs a layout pass inside
  the call, so its offset moves synchronously.
  ASSUMPTION: a tunnel `KeyDown` handler with handled events included, `PointerWheelChanged` with
  handled events included, and the scrollbar's drag events reach a handler on each grid for every
  operator input; fallback: subscribe on the scrollbar parts found after template apply; checked
  by: A6 and smoke S6, S7.
- End vs stop. probed by the manager from the DB996 emulator (`RunState.cs`,
  `RunEngine.BeginFinish`, `Ui/MainWindowViewModel.cs`), 2026-11-25: a finished run leaves the
  line at or past N; a stopped run leaves it on the stopped row. `PlcExecutionInfo` carries no
  reason field. `PlcExecutionMonitor` publishes `PlcExecutionInfo.Empty` when it stops (link loss,
  sync off). ASSUMPTION: the real PLC matches the emulator; fallback: stop and ask; checked by:
  smoke S4.
- Focus frame. Semi's `DataGridCell` draws the current and focus rectangles from
  `DataGridCellCurrentBorderBrush` / `DataGridCellFocusBorderBrush`; those rectangles are
  theme-private parts, not `[TemplatePart]`. So A5 reads the rendered rectangle's stroke and
  thickness back from the realised cell, and a Semi update that renames them fails A5.
  ASSUMPTION: the rectangle is already 2 px; fallback: set the thickness through a `/template/`
  selector on that part, still guarded by A5; checked by: A5.
- Batch C overlaps (plan on branch `ui-cell-templates`, read 2026-11-25): it touches
  `DataGridStyles.axaml`, `TransposedGridStyles.axaml`, `GridStyleOptions.Default` (header
  values), `ColumnBuilder` (the "№" header from resx, header template) and
  `RecipeGridHost.axaml.cs` (default focus). Canonical combo cells become display text cells until
  edit, so every canonical cell takes its background from the `DataGridCell` style setter this
  plan replaces. This branch rebases onto C and keeps C's header and focus changes.
- Batch B edits the same Core record, DTOs, both mappers, `CellPaletteInstaller`, the style editor
  and the three presets (status bar and chrome). Conflicts are textual, in neighbouring lines.
- Glyph font. probed: the cmap tables of `segoeui.ttf` and `SegUIVar.ttf`, 2026-11-26. Both map
  U+25A0 "■" and U+25BA "►" but not U+25B6 "▶", so "▶" falls back to another font. The running
  glyph is U+25BA "►", the same black right-pointing triangle; the stop glyph stays U+25A0. The
  status bar's "▶ Выполняется" strings (batch B) have the same fallback and are out of this plan.
- The perf probe measures binding, style and layout cost only; headless has no Skia, so render
  cost is not in it.

## Constraints and non-goals

- The colour tables below are exact; tests pin them.
- Which cells turn orange, and when marks clear other than at run start, stay as batch A and
  marks-survive-undo leave them.
- The lock during a run, combo behaviour and selection behaviour do not change.
- Site-edited `grid_style.yaml` files are not migrated; they load with defaults for the new keys.
- Dark mode is out of scope.
- No new interface: one resolver, one implementation.
- Threat model: a hand-edited `grid_style.yaml` with a bad weight or colour is in scope (config
  error); nothing else.

## UI design

Values approved by the operator. Build them, do not redesign.

| Layer | Colour | Weight |
|---|---|---|
| Loop depth 1 / 2 / 3 | `#7D4DC8` | 0.08 / 0.16 / 0.26 |
| Running row | `#208A3C` | 0.30 |
| Past row | `#208A3C` | 0.12 |
| Selection | `#3574F0` | 0.25 |

Grey cells take the same weights. Blend per channel in integer thousandths, rounded half up, in
the order above. Full table, base by loop depth; columns idle / past / running, then the same
three selected. Entries marked "not reachable" are pinned anyway.

| Base | Depth | Idle | Past | Running | Sel. idle | Sel. past | Sel. running |
|---|---|---|---|---|---|---|---|
| White `#FFFFFF` | 0 | `#FFFFFF` | `#E4F1E8` | `#BCDCC5` | `#CDDCFB` | `#B8D2EA` | `#9AC2D0` |
| White | 1 | `#F5F1FB` | `#DBE5E4` | `#B5D2C2` | `#C5D2F8` | `#B2C9E7` | `#95BBCE` |
| White | 2 | `#EAE3F6` | `#D2D8E0` | `#ADC8BE` | `#BDC7F5` | `#ABBFE4` | `#8FB3CB` |
| White | 3 | `#DDD1F1` | `#C6C8DB` | `#A4BCBB` | `#B3BAF1` | `#A2B3E0` | `#88AAC8` |
| Inapplicable `#E0E0E0` | 0 | `#E0E0E0` | `#C9D6CC` | `#A6C6AF` | `#B5C5E4` | `#A4BED5` | `#8AB2BF` |
| Inapplicable | 1 | `#D8D4DE` | `#C2CBCB` | `#A1BEAD` | `#AFBCE3` | `#9FB5D4` | `#86ACBE` |
| Inapplicable | 2 | `#D0C8DC` | `#BBC1C9` | `#9BB5AC` | `#A9B3E1` | `#9AAED3` | `#82A5BD` |
| Inapplicable | 3 | `#C6BADA` | `#B2B4C7` | `#94ACAB` | `#A2A9E0` | `#93A4D1` | `#7C9EBC` |
| Read-only `#D8D8D8` | 0 | `#D8D8D8` | `#C2CFC5` | `#A1C1A9` | `#AFBFDE` | `#9FB8D0` | `#86AEBB` |
| Read-only | 1 | `#D1CDD7` | `#BCC5C4` | `#9CB9A9` | `#AAB7DD` | `#9AB1CF` | `#82A8BB` |
| Read-only | 2 | `#C9C2D5` | `#B5BBC3` | `#96B1A7` | `#A4AFDC` | `#95A9CE` | `#7EA2B9` |
| Read-only | 3 | `#C0B4D4` | `#ADAFC2` | `#90A7A6` | `#9DA4DB` | `#8FA0CE` | `#799AB9` |
| Orange `#FFCC80` | 0 | `#FFCC80` | `#E4C478` not reachable | `#BCB86C` not reachable | `#CDB69C` | `#B8B096` not reachable | `#9AA78D` not reachable |
| Orange | 1 | `#F5C286` | `#DBBB7D` not reachable | `#B5B170` not reachable | `#C5AFA1` | `#B2A99A` not reachable | `#95A290` not reachable |
| Orange | 2 | `#EAB88C` | `#D2B282` not reachable | `#ADAA74` not reachable | `#BDA7A5` | `#ABA39E` not reachable | `#8F9D93` not reachable |
| Orange | 3 | `#DDAB93` | `#C6A789` not reachable | `#A4A179` not reachable | `#B39DAA` | `#A29AA3` not reachable | `#889697` not reachable |

- Glyphs: black "►" before the step number in the № cell of the running row; black "■" there on
  the stop row. Transposed: the same glyph in the step header, normal header background. No left
  bar, no marker strip, no orange step marker.
- All cell text `#000000`.
- Focus frame: 2 px `#3574F0` on the current cell, both grids.
- Auto-scroll: follows the running step; pause 10 s after each operator input listed above.
- Empty hint, centred in the grid area: "Рецепт пуст. Добавьте шаг (Ctrl+N) или откройте файл
  (Ctrl+O)."

## Gates

- Probe `ExecutionLineChangeCostProbe` (`[AvaloniaFact(Explicit = true)]`, `Category=Performance`,
  per `docs/perf/README.md`), both orientations. Its one assertion is a same-process ratio: the
  time of one running-line change at N=2100 steps divided by the time at N=300 must stay at or
  below 3.0. The absolute medians are printed, not asserted. Task 1 runs it on the rebased master
  and records the medians with the date; Task 3 runs it again. The manager stops the run for an
  operator decision when the ratio fails, or when a branch median exceeds twice the master median
  (manager: autonomous-decisions.md item 9). No master number exists as of 2026-11-25.
- Command: `dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release --
  -explicit only -method "*ExecutionLineChangeCostProbe*"` → exit code 0.

## Acceptance

- `dotnet build SemiStep.slnx` → exit code 0.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0.
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
- Headless tests read the realised `Background` of the cell (canonical `DataGridCell`, transposed
  cell `Border`) as `ISolidColorBrush`. Each item is
  `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=<Area>"`:
  - A1 and A2 share `Area=GridPalette` → exit code 0, at least 12 tests in total.
  - A1: the installed table matches the UI
    design table for every entry, including the unreachable orange ones; read-only depth 3
    running `#90A7A6` and its selected variant `#799AB9` are pinned literally; depth 4 resolves
    as depth 3.
  - A2: canonical and
    transposed, one realised cell per base in idle, past, running, depths 1 to 3 and selected
    shows the table hex; a marked cell in a read-only column shows orange; read-only, inapplicable
    and selected cell text reads `#000000`.
  - A3 and A4 share `Area=ExecutionHighlight` → exit code 0, at least 12 tests in total.
  - A3: start, forward line change and
    backward jump set running and past; the № cell text of the running row starts with "►"; the
    transposed running header starts with "►".
  - A4: last active line N: every
    row past, no glyph; N-1: "■" on the last row; 2: "■" on row 2, rows 0 and 1 past; -1: no
    glyph; an `Empty` emission after line 4: "■" on row 4, not row 0; a cell edit, an undo and an
    action change after a run each clear every tint and glyph, and after the action change the
    reset cells are orange and no row is past; a save (`StateRefreshed`) keeps them; a
    `RecipeReplaced` during an active run, then the next emission, repaints running and past.
  - A5 `Area=FocusFrame` → exit code 0, at least 3 tests: the canonical current cell's rendered
    rectangle reads stroke `#3574F0` and thickness 2; the transposed focused cell shows the frame
    with the cell's size unchanged; a read-only transposed cell takes no focus and shows no frame.
  - A6 `Area=AutoScroll` → exit code 0, at least 6 tests, on a virtual scheduler: a line change
    scrolls the running row into view; a wheel event, a PageDown key and a scrollbar drag each
    pause, and a line change during the pause does not scroll; 10 s later the running row is in
    view; a code-driven `ScrollIntoView` does not pause; selection and current cell never change.
  - A7 `Area=EmptyHint` → exit code 0, at least 4 tests: the hint shows for an empty recipe in
    both orientations and hides after the first step; a 0-step CSV load shows it; an N-step CSV
    load never sets `IsRecipeEmpty` true during the load; Ctrl+N works with the hint shown.
  - A8 `Area=GridStyleMapping` → exit code 0, at least the current count plus 3: a file holding
    only the old keys loads with the new keys at default values; a file without the
    `cells.execution` section loads; a `running_weight` of 1.5 fails with
    `GridStyleWeightOutOfRangeError` whose section is `colors.cells.execution` and key is
    `running_weight`.
- Manual smoke, driven through UI Automation with the DB996 emulator; colours read as a pixel hex
  from a PrintWindow capture (`dev:app-screenshots`), glyphs and texts through UIA:
  1. start with no recipe, focus the grid → the hint line is centred; Ctrl+N adds a step and hides
     it. (skipped - manager live check)
  2. open `SemiStep/SemiStep.Tests/TestData/Recipes/loops-timeleft.csv`, enable sync, start the
     run → running row pixel `#BCDCC5` in a white cell, № cell text starts with "►", rows above
     show `#E4F1E8`. (skipped - manager live check)
  3. select a cell in the running row → that cell reads `#9AC2D0` and shows a blue 2 px frame. (skipped - manager live check)
  4. let the run finish → every row reads the past hex, no "►" or "■". (skipped - manager live check)
  5. start again, stop at step 3 → step 3 shows "■" on `#E4F1E8`; save → unchanged; edit one
     cell → all tints and "■" gone. (skipped - manager live check)
  6. during a run, turn the wheel away from the running row → the view stays for 10 s, then
     returns to the running row; the selection has not moved. (skipped - manager live check)
  7. during a run, drag the vertical scrollbar away → the view stays for 10 s, then returns. (skipped - manager live check)
  8. flip to transposed (Ctrl+Shift+T) and repeat 2 and 3 → the same hexes; "►" in the step header. (skipped - manager live check)
- Not visible to headless tests: real pixel colours after compositing, the scroll feel, the
  frame's look over display and combo cells, render cost.

---

## Context

- Palette: `SemiStep.UI/Styles/CellPaletteInstaller.cs`, `ExecutionPaletteInstaller.cs`,
  `PaletteBrushFactory.cs`, `DataGridStyles.axaml`, `TransposedGridStyles.axaml`, `App.axaml`
  (`DataGridCellCurrentBorderBrush`, `DataGridCellFocusBorderBrush`).
- Cell state: `RecipeGrid/InapplicableCellTheme.cs` (`IsInapplicable`, `IsChanged`),
  `ColumnBuilder.cs` (`read-only-column`, `step-number-column`), `RecipeRowExecutionClassBinder.cs`,
  `RowExecutionClasses.cs`, `Transposed/TransposedCellBackgroundConverter.cs`,
  `Transposed/TransposedColumnCellsPresenter.cs`, `Transposed/TransposedStepColumnClassBinder.cs`,
  `Transposed/TransposedRecipeGridView.axaml` (`transposed-current-marker`,
  `transposed-step-header`).
- Execution: `RecipeGrid/ExecutionHighlightTracker.cs`, `RecipeGridSurfaceBase.cs` (subscribes the
  tracker, handles `MutationSignal`, resets on rebuild; past the 300-line cap, so new logic goes
  into its own small classes), `RecipeRowViewModel.cs` (`IsCurrentStep`, `IsPastStep`),
  `Coordinator/MutationSignal.cs`, `SemiStep.Core/Plc/State/PlcExecutionInfo.cs`,
  `SemiStep.Core/Plc/Sync/PlcExecutionMonitor.cs`.
- Scroll: `CanonicalRecipeGridView.axaml.cs`, `Transposed/TransposedRecipeGridView.axaml.cs`,
  `Transposed/TransposedColumnsPanel.cs`. Empty hint: `RecipeGrid/RecipeGridHost.axaml` (+ `.cs`),
  `ActiveRecipeGridSurface.cs`, `Localization/Resources*.resx`.
- Config: `SemiStep.Core/Configuration/GridStyleOptions.cs`, `Dto/*`, `Mapping/GridStyleMapper.cs`,
  `Mapping/GridStyleDtoMapper.cs`, `Loaders/YamlConfigDeserializer.cs`, the `GridStyle*Error.cs`
  files beside them; `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml`; all 23
  `SemiStep.Tests/YamlConfigs/**/grid_style.yaml` hold the deleted keys and load unchanged;
  `Standard` gets the new keys; `SemiStep.UI/StyleEditor/*Draft.cs`, `GridStyleEditorWindow.axaml`.
- Tests to adapt: `UI/Styles/CellPaletteInstallerTests.cs`, `ExecutionPaletteInstallerTests.cs`,
  `ThemeTests.cs`, `UI/RecipeGrid/ExecutionHighlightTrackerJumpTests.cs` (moves to
  `Area=ExecutionHighlight`), `Transposed/TransposedCellBackgroundConverterTests.cs`,
  `Core/Configuration/GridStyle*Tests.cs`, `Helpers/GridStyleOptionsTestData.cs`; perf probes under
  `SemiStep.Tests/Performance/`.
- Reference: `NtoLib/NtoLib/Recipes/MbeTable/ModulePresentation/Style/ColorStyleHelpers.cs`
  (`Blend`, `ApplyLoopTint`, `ApplyExecutionTint`).
- Docs: `docs/architecture/grid-style-configuration.md`, `cell-change-highlight.md`,
  `recipe-grid-surface.md`, `headless-ui-testing.md`, `docs/perf/README.md`.

## Boundaries

- Task 2 gives the Core record the new palette values (execution green and two weights, loop
  colour and three weights, selection accent and weight) that Task 3's installer reads.
- Task 3 gives one resolver: cell state (base kind, depth 0..3, execution state idle / past /
  running, selected) to a brush from the resource table. Task 4 adds no colours; it changes only
  the execution states and glyphs.

## Tasks

### Task 1: Rebase, fixture and perf baseline

**Goal:** the branch starts from master with batches A, marks-survive-undo, B and C merged, and
master's line-change cost is recorded
**Entry:** `git fetch origin && git rebase origin/master`
**Likely touches:** `SemiStep.Tests/Performance/`, `SemiStep.Tests/TestData/Recipes/`, test csproj

- [x] confirm A, marks-survive-undo, B and C are on `origin/master`; if one is missing, stop
- [x] read C's and B's merged diffs; adjust this plan's Context if a file moved
- [x] build, full test suite and format check pass before any change
- [x] add the `loops-timeleft.csv` fixture, copied to the build output
- [x] write `ExecutionLineChangeCostProbe` as an explicit performance probe; run it and record
      the medians with the date
- [x] run tests - must pass before the next task

### Task 2: Palette keys in Core, presets and the style editor

**Goal:** the new keys load, validate and round-trip; the deleted keys are gone
**Entry:** `GridStyleOptions.Default`, `GridStyleMapper.Map`
**Likely touches:** `GridStyleOptions.cs`, the palette group records, DTOs, `GridStyleMapper.cs`,
`GridStyleDtoMapper.cs`, a new `GridStyleWeightOutOfRangeError.cs`, three presets,
`YamlConfigs/Standard/ui/grid_style.yaml`, style-editor drafts and window

- [x] replace the execution, read-only, disabled, changed and selection groups with the kept and
      new keys listed in Decisions; required and optional exactly as listed
- [x] weights validated to 0..1 with `GridStyleWeightOutOfRangeError`
- [x] presets and the Core default carry the approved values
- [x] style editor shows the new colours and weights and drops the deleted ones
- [x] tests: A8; save then load is lossless
- [x] run tests - must pass before the next task

### Task 3: One blended cell palette for both grids

**Goal:** every cell shows the colour from the four-layer rule; text is black
**Entry:** `CellPaletteInstaller.Install`, `TransposedCellBackgroundConverter`
**Likely touches:** the installers, a resolver beside them, `DataGridStyles.axaml`,
`TransposedGridStyles.axaml`, `TransposedColumnCellsPresenter.cs`

- [x] compute the 96 brushes once per load with the integer rule; install the table under one key
- [x] canonical and transposed cells take their background from the table through the host's
      resource lookup; the transposed binding gains the running input; the old per-depth, past,
      selected and marker rules and keys go
- [x] remove grey and selection foreground setters
- [x] tests: A1, A2
- [x] Gate: run the probe command and hand both medians and the ratio to the manager
- [x] run tests - must pass before the next task

### Task 4: Running, past and stop states with glyphs

**Goal:** the run's tints follow D1a, survive the run's end, and clear on the next edit
**Entry:** `ExecutionHighlightTracker.OnExecutionStateChanged`
**Likely touches:** `ExecutionHighlightTracker.cs`, `RecipeRowViewModel.cs`, `RecipeGridSurfaceBase.cs`,
`ColumnBuilder.cs`, `TransposedRecipeGridView.axaml`, `RowExecutionClasses.cs`

- [x] whole running row tinted; "►" in the № cell and the transposed step header
- [x] end, stop, -1 and `Empty` rules from Flow branches, keyed on the last line seen while active
- [x] step-graph signals and `RecipeReplaced` clear tints and glyphs while no run is active;
      `StateRefreshed` does not; the reconnect path repaints from the next emission
- [x] remove the transposed marker strip and the orange step marker
- [x] tests: A3, A4
- [x] run tests - must pass before the next task

### Task 5: Accent focus frame in both grids

**Goal:** the focused cell shows a 2 px `#3574F0` frame and no layout moves
**Entry:** `App.axaml` DataGrid frame keys; the transposed cell `Border`

- [x] retint the canonical frame keys to the accent
- [x] add a transposed frame on the focused editable cell that does not change the cell's size
- [x] tests: A5, reading the rendered rectangle back
- [x] run tests - must pass before the next task

### Task 6: Auto-scroll to the running step with operator pause

**Goal:** the running step stays in view unless the operator gave scroll input in the last 10 s
**Entry:** the surface's execution subscription in `RecipeGridSurfaceBase`
**Likely touches:** a new small scroll-follow class, both grid views

- [x] scroll to the running step on each line change while not paused, without touching
      selection, current cell or focus
- [x] the listed keys, the wheel and a scrollbar drag pause for 10 s; each input restarts it
- [x] scroll to the running step when the pause ends during a run; time through an injectable
      scheduler
- [x] tests: A6
- [x] run tests - must pass before the next task

### Task 7: Empty-recipe hint

**Goal:** an empty recipe shows the hint line in both orientations
**Entry:** `RecipeGridHost.axaml`, `RecipeGridSurfaceBase`

- [x] `IsRecipeEmpty` on the surfaces, set once per applied mutation, forwarded by
      `ActiveRecipeGridSurface`
- [x] centred hint beside the host's `Content`, not hit-testable, not focusable, text from resx
      (ru and invariant); the orientation swap and `IsEditing` unchanged
- [x] tests: A7
- [x] run tests - must pass before the next task

### Task 8: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result and test count
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0

### Task 9: Update documentation

**Goal:** docs describe the blended palette, the keys, the run states, the frame exception,
auto-scroll and the probe
**Entry:** `docs/architecture/grid-style-configuration.md`

- [x] `grid-style-configuration.md`: keys, required and optional, defaults, compatibility, the
      resource table and resolver; drop "Execution depth-0" from "What is intentionally not wired"
- [x] `cell-change-highlight.md`: cascade placement replaced by the layer order
- [x] `recipe-grid-surface.md`: run states, end vs stop, auto-scroll, focus frame exception,
      empty hint
- [x] `docs/perf/README.md`: a row for `ExecutionLineChangeCostProbe` in the probe table; no
      `baselines.json` entry

## Post-Completion

- the operator's manual test: smoke steps 1 to 8, plus the items not visible to headless tests
- the PR body: `Closes #202` for the grid items (running-row tint and glyph, keep in view, marker
  colour); `Refs #157` (selection, focus frame, read-only text); `Refs #169` (orange as base)
- shipped presets reach installed machines through the installer (ledger X9); site-saved files
  keep their kept keys and take defaults for the new ones
