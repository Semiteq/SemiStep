# UI cell templates

## Behavior

- Canonical combo cells (action, object) show plain text until edit, as transposed combo cells do
  today. A chevron shows only while the pointer is over the cell and on the current cell. While
  the PLC runs the recipe, no chevron shows and no editor opens. Keyboard focus stays on the
  DataGrid; a display cell takes no focus. In the transposed grid the chevron shows on the focused
  combo cell and hides while the cell is locked.
- One plain left press on a combo cell opens its list, in both grids. F2, F4 and Alt+Down open the
  list from the keyboard. A pick or Enter commits, Esc closes the list and keeps the old value.
  When the list closes, the cell shows text again and focus returns to the grid.
- Type-to-edit in both grids: a printable key on a focused text cell starts the edit and replaces
  the value with the typed character. F2 starts the edit and keeps the value. Enter commits, Esc
  cancels.
- Headers are 12 px SemiBold, one line, colour `#6C707E`. A header that does not fit ends in an
  ellipsis. Every header shows its full text as a tooltip.
- The step number header reads "№" and comes from the resource set.
- Numeric cells are right-aligned. The unit sits in its own slot to the right of the number, and
  the slot is as wide as the widest unit of that column, so numbers of one column share one right
  edge. Text cells and "Комментарий" are left-aligned. In the transposed grid the unit slot is a
  new visible element.
- Inapplicable cells keep the grey fill and show no text and no "—".
- A "Время" cell of a ramp action shows the tooltip "время рампы, не входит в длительность шага".
  Its look does not change.
- While a loop runs, the action cell of that loop's "Цикл начало" row shows a pill "(2/5)" before
  the chevron slot: current pass / loop count. Each running nesting level shows its own pill.
  Outside a run, or outside the loop's rows, no pill shows.
- Transposed: the step column is as wide as the longest action name, up to 240 px. A longer name
  ends in an ellipsis. Every action name carries its full text as a tooltip.
- The grid holds keyboard focus at window open, after an orientation flip, after "Новый рецепт" and
  after "Открыть".
- Scope guard: toolbar, gestures and the marking rule (batch A), marks surviving undo, the status
  bar and the "с" drop from `time_hms` (batch B), execution and loop palette, selection blend,
  focus frame, auto-scroll and the empty hint (batch D) stay as their branches leave them. Text
  cells keep the DataGrid edit lifecycle. CSV, clipboard and PLC values do not change.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Plain left press on an idle applicable combo cell | Selects the row, builds the editor, opens the list after layout; the release keeps it open | operator: D4; agent: probe in Caveats | A3 |
| Ctrl/Shift/right press on a combo cell | Changes the selection only, no editor | agent: `ComboBoxPressOpener` rule on master | A3 |
| Press that light-dismisses another open list | Closes that list, opens nothing | agent: `ComboBoxPressOpener` tunnel rule on master | A3 |
| Press, F2, F4, Alt+Down, typing on a combo cell while the PLC runs | Nothing opens, no chevron | operator: X4 | A4 |
| PLC run starts while a list is open | The list closes, the cell shows text, focus moves to the grid | agent: `DataGridEditorCloseBehavior` on master | A4 |
| PLC run starts between the press and the deferred open | No list opens; the editor ends and the display shows | manager: autonomous-decisions.md | A4 |
| Transposed combo cell focused, idle or locked | Chevron on the focused cell; none while the presenter is not effectively enabled | manager: autonomous-decisions.md | A4 |
| Pointer over an applicable combo cell, or the cell is current | A chevron in that cell only; every other combo cell shows none | operator: X4 | A4 |
| Inapplicable or read-only combo cell, pointer over or current | No chevron, no editor | operator: X4 | A4 |
| F2 / F4 / Alt+Down on the current combo cell | Opens the list; the canonical view resolves the current cell and starts the edit, focus stays on the DataGrid | operator: D4; manager: autonomous-decisions.md | A5 |
| Arrow from row 3 to row 5 in the action column, then F4 | The list opens on row 5 | manager: autonomous-decisions.md | A5 |
| Printable key on the current combo cell | Nothing; combo cells ignore typing, as transposed combo cells do today | agent: `TransposedLazyCellPresenter.OnPrintableKeyDown` | A5 |
| Down then Enter in an open list | Picks the next item, closes the list, the row stays selected | agent: master contract | A3 |
| Esc in an open list | Closes it, value unchanged | operator: D4 | A5 |
| Delete or Ctrl+C while a list is open | Ignored by the window; the list owns the keys | agent: `IsEditing` chain | A5 |
| Press a combo cell, Esc, then wheel or Up/Down (the master bug path) | Wheel scrolls the grid, arrows move the current cell; no value changes | agent: no live closed combo exists (Caveats) | A6 |
| Printable key on a text cell, canonical or transposed | Starts the edit with the typed character as the value | operator: D4 | A5 |
| F2 / Enter / Esc on a text cell | Starts the edit / commits / cancels | operator: D4 | A5 |
| Header text wider than the column | One line, ellipsis; the longest-word width floor stays, so headers may trim | operator: D5a; manager: autonomous-decisions.md | A7 |
| Pointer over any header | Tooltip with the full header text, trimmed or not | manager: autonomous-decisions.md | A7 |
| Number with a unit, number without a unit, empty value | Right edge shared within the column; empty unit leaves the slot blank | operator: D5b | A8 |
| Inapplicable cell | Grey fill, no text | operator: D5c | A8 |
| Pointer over the "Время" cell of a ramp step (an action on the explicit ramp key list) | Tooltip "время рампы, не входит в длительность шага" | operator: D5g; manager: autonomous-decisions.md | A9 |
| PLC counters 0 (outside a loop) or recipe not active | No pill | operator: D3; agent: #218 counter meaning | A10 |
| Nested loops, outer pass 2 of 3, inner pass 2 of 5 | Outer "Цикл начало" shows "(2/3)", inner shows "(2/5)" | operator: D3 badge B | A10 |
| Run ends or stops | Every pill disappears | operator: D3 "only while that loop runs" | A10 |
| PLC poll changes only `StepCurrentTime` | No pill recompute, no row `PropertyChanged` | manager: autonomous-decisions.md | A10 |
| Long action name plus pill in a narrow cell | The name trims with an ellipsis; the pill stays whole | agent: probe in Caveats | A10 |
| Action name longer than 240 px in the transposed grid | Ellipsis; the tooltip always shows the full name | operator: D5i; manager: autonomous-decisions.md | A11 |
| Window open, orientation flip, New, Open | The active grid holds focus; arrows move without a click | operator: D5i | A12 |

## Decisions

- Canonical combo cells use the transposed lazy model: a display cell holds the text, the chevron
  slot and the pill; the `ComboBox` exists only while the cell is edited. The orientation-free part
  of `TransposedLazyCellPresenter` moves to one shared presenter; each grid keeps only its own
  focus-return target. Reason: X4 and one lazy model in one place. Rejected: a DataGrid
  `CellEditingTemplate` with `BeginEdit` on press. A ComboBox in `CellEditingTemplate` does not
  open in Avalonia 12 (`AvaloniaUI/Avalonia.Controls.DataGrid#236`, closed without a fix); that is
  why `docs/plans/completed/20260514-combobox-celltemplate-migration.md` moved the combos into
  `CellTemplate`.
- Shared presenter scope (manager: autonomous-decisions.md): shared are the display/editor swap, the deferred open,
  close-ends-edit, the blur exit and a focus-return target passed in by each grid. F2, printable
  keys and the text presenter stay transposed only. `TransposedTextEditCoordinator` moves with the
  presenter, one instance per grid. One presenter class, the focus target passed in; no interface
  and no factory.
- Canonical focus (manager: autonomous-decisions.md): canonical display cells are not focusable and focus stays on the
  DataGrid. The canonical view resolves the current cell and starts the edit on F2, F4 and
  Alt+Down. This routing lands in Task 3 with the display cells, so F4 and Alt+Down never break
  between tasks.
- Transposed chevron (manager: autonomous-decisions.md): shown on the focused presenter, hidden while the presenter is
  not effectively enabled (the lock).
- The list opens from a `Background` dispatcher job after the editor is built, in both grids.
  Reason: probed (Caveats); a synchronous open leaves Down and Enter on the DataGrid. The deferred
  job re-reads the applicable-and-unlocked rule before it opens; the lock-transition close also
  ends a live editor whose list is not open yet (manager: autonomous-decisions.md).
- The canonical lock stays one rule: a combo display cell enters edit only while its column is
  applicable and `DataGrid.IsReadOnly` is false (`CellApplicabilityBinding.CreateApplicableAndUnlockedBinding`).
  The chevron reads the same rule.
- `IsEditing` of the canonical view is true while a lazy combo editor is live, as well as during a
  DataGrid text edit, so batch A's `WindowGestures.BelongsToCellEditor` keeps Delete and Ctrl+C/X/V
  off a step while a list is open.
- Combo cells ignore printable keys in both grids. Reason: matches the transposed grid today; D4
  type-to-edit concerns text values.
- Canonical text cells keep the DataGrid edit lifecycle and its native Enter (commit, move to the
  next row); transposed Enter keeps commit-and-stay. Only type-to-edit is added to the canonical
  grid.
- Numeric cell = a property whose `system_type` is `int` or `float`, plus `step_start_time`. A
  `string` property and the `text_field` column ("Комментарий") are text. The unit slot width is
  measured once per column from every action's unit for that column key. In the transposed grid the
  slot width is the widest unit of any parameter, because all step columns share one width.
  The slot reads the unit from the row's `ColumnUnits`, not from the formatted string, so the
  value text carries no unit. `time_hms` and `step_start_time` cells leave the slot empty, and a
  `time_hms` unit ("с") does not count toward the slot width; that keeps batch B's D6c result.
- Headers: `header_size: 12`, `header_weight: 600`, `chrome.header_foreground: "#6C707E"` in the
  three presets and in `GridStyleOptions.Default`. The header template becomes one line. The width
  calculator keeps its longest-word floor, so a long header trims; every header carries its full
  text as a tooltip, so no trim detection is needed (manager: autonomous-decisions.md).
- "№": one resx key, Russian "№", English "No". The canonical numbering header and the empty
  transposed corner above the parameter names both read it (#157 item 4).
- Ramp tooltip: a resx string on the `step_duration` cell of an action on an explicit list of
  ramp action keys (manager: autonomous-decisions.md). `TimingCalculator.ExtractStepDuration` returns zero for an
  action with `DeployDuration.Immediate`, which is the fact the tooltip states. Task 6 builds the
  list from the three presets and reports to the operator every other action that has
  `deploy_duration: immediate` and a `step_duration` column; those get no tooltip.
- Badge: the shared `RecipeRowViewModel` carries the pill text, set by `RecipeGridSurfaceBase`
  from `ExecutionState` for each loop whose range holds the PLC's actual line and whose counter is
  1 or more. Depth 1, 2, 3 map to `ForLoopCount1..3` (`badge/badge.diff`, emulator `RunPlan`).
  The loop's range counts inclusive of its start and end rows. One shared derivation returns the
  active loops with their pass; batch B's status bar and both surfaces consume it, so the pill and
  the status bar cannot disagree. If batch B holds a text-only version, Task 7 refactors it into
  this one. Pills are recomputed only when `RecipeActive`, `ActualLine` or `ForLoopCount1..3`
  changes, and after every mutation tail, so a full rebuild (marks-survive-undo keeps
  `FullRebuild` for `RecipeReplaced`) cannot leave a new row without its pill. The pill text is
  written only on rows where it changes (manager: autonomous-decisions.md).
- Badge in the transposed grid: `badge/INDEX.md` mocks the canonical grid only. The
  transposed action cell uses the same display cell, so it shows the same pill; the action name
  trims inside the 240 px cap before the pill.
- D6c owner: batch B (its Task 3 and acceptance A9). Batch C does not add the "с" back: the value
  half of the cell formats without a unit, and `time_hms` cells show an empty slot. Batch C keeps
  batch B's tests that pin "00:10:00" without "с".
- The transposed step column width is computed once per surface and stays uniform, so the
  `TransposedColumnsPanel` single-width invariant holds. It is the wider of the longest action
  name with pill and chevron and the widest value plus unit slot, plus cell padding, capped at
  240 px.

## Caveats

- A display cell can swap to a live `ComboBox` on the press and keep the one-press open.
  probed: throwaway headless test (plain `DataGrid`, `DataGridTemplateColumn` with a `Border`
  display, tunnel + bubble `handledEventsToo` handlers on the grid as in `ComboBoxPressOpener`),
  2026-11-25. Synchronous open in the bubble handler: list open after press and after release,
  but Down+Enter did not pick, focus stayed on the `DataGrid`. Open from a `Background` job: open
  after press and release, Down+Enter picked the next item and closed the list, row selected.
  ASSUMPTION: on a real window the release can arrive before the `Background` job runs, with the
  same result; fallback: open on `Loaded` of the editor; checked by: smoke step 2.
- A right-aligned number with a unit slot fills the cell. probed: same test, a `Grid` with a star
  value column (`TextAlignment.Right`) and a fixed unit column, 2026-11-25. `DataGridCell` under
  Semi has `HorizontalContentAlignment=Stretch` and zero padding; the template filled 139 of 140 px
  and the value right edge and the unit left edge were identical in all rows ("%", "см³/мин", "").
  A value wider than its star column is clipped, so the width calculator measures value plus slot.
- A badge in the action cell does not block the press. probed: same test, a hit-test-invisible
  `Border` pill in an `Auto` column between a trimming name and a 32 px chevron slot, 2026-11-25.
  The pill sat vertically centred (20 px in a 28 px row); a press on the pill built the editor
  and opened the list.
- With text-until-edit no closed live combo exists: the editor opens one dispatcher job after it
  is built, and its `DropDownClosed` ends the edit. The `closed-combo-wheel` branch then leaves
  two pieces of dead code: `XYFocus.SetNavigationModes(comboBox, XYFocusNavigationModes.Enabled)`
  in `ComboBoxCellFactory`, and the tunnel `PointerWheelChanged` handler class
  `ClosedComboWheelScroller` attached to the DataGrid in `CanonicalRecipeGridView`. Task 3 removes
  both; their tests (`CanonicalClosedComboInputTests`) stay and target the display cells.
- The `:pressed` workaround in `ComboBoxPressOpener` is removed only after
  `PressAndRelease_LeavesNoPressedState` and `DoublePress_OpensThenClosesList` pass without it,
  retargeted to the presenter's editor, including one re-entry of the pooled editor.
- ASSUMPTION: the Avalonia 12 `DataGrid` (package 12.0.1) does not begin an edit on a printable
  key; fallback: none needed if it does, keep its behaviour; checked by: Task 4 test.
- ASSUMPTION: the typed character can be seeded into the DataGrid editing `TextBox` after
  `BeginEdit` with replace semantics and the caret after it; fallback: forward the key's
  `TextInput` to the editor once it holds focus, as the transposed text presenter does; checked
  by: Task 4 test.
- Recycled row with a live editor: a probe step in Task 3 checks whether a recycled DataGrid row
  detaches its cells. If it does, the presenter's detach commit covers it; if not, the presenter
  ends the edit on a `DataContext` change. A Task 3 test opens a list, scrolls the row out and
  checks that no value lands on the row that reuses the cell.
- ASSUMPTION: depth 1 is the outermost loop and maps to `ForLoopCount1`, as
  `ExecutionTimeEstimator.ComputeLoopOffset` reads the counters, and a loop counts as active on
  its own start and end rows; fallback: invert the mapping or the range test to match the PLC;
  checked by: smoke step 8 against the status bar loop text.
- ASSUMPTION: `+tnum` tabular figures reach the pill text through `TextElement.FontFeatures`, as
  they reach the cells today (`GridFonts.TabularFigures`); fallback: pad the pass with U+2007
  figure spaces to the digit count of the loop count; checked by: Task 7 test comparing the pill
  width for "(1/5)" and "(5/5)".
- ASSUMPTION: the `DataGridCell` `:current` pseudo-class follows the keyboard current cell and can
  drive the chevron; fallback: bind to `DataGrid.CurrentColumn` and the row's `IsSelected`;
  checked by: Task 3 test.
- Header colour lives in `chrome.header_foreground` (`DataGridStyles.axaml` and
  `TransposedGridStyles.axaml` read `HeaderForegroundBrush`). Batch B edits the same `chrome:`
  blocks; the rebase resolves line conflicts only.

## Constraints and non-goals

- Keep from master `62405ee`: one plain press opens the list, the lock from `DataGrid.IsReadOnly`,
  the Esc and focus behaviour (`recipe-grid-surface.md`, "Combo cells: lock and press-to-open").
- Zero binding errors (`BindingErrorGuard`); the transposed perf gates in `docs/perf/README.md`
  stay green; a realized row or column holds no live `ComboBox` when nothing is edited.
- No new `grid_style.yaml` keys; site-edited presets are out of scope.
- Arrow skipping of inapplicable cells in the canonical grid is not part of this plan.
- The shared presenter is one class with a focus target passed in; no interface, no factory.
- Tests stay as simple as possible; no speculative abstractions.

## UI design

Approved decisions, built as stated:

- Combo cells: plain text; chevron only on pointer-over and on the current cell; none while locked,
  inapplicable or read-only (X4, #204).
- Keyboard: F2 edits, Enter commits, Esc cancels, F4 and Alt+Down open a list, type-to-edit in
  both grids, one press opens a list on mouse-down (D4).
- Headers 12 px SemiBold, one line, `#6C707E` (D5a).
- Numbers right-aligned, unit in its own slot sized to the column's widest unit; text left;
  "Комментарий" left (D5b).
- Inapplicable cells keep the grey fill, no "—" (D5c).
- Ramp "Время" tooltip "время рампы, не входит в длительность шага", no visual change (D5g).
- Transposed step column fits the longest action name up to 240 px, then ellipsis and tooltip;
  default focus on the grid at open, flip, New, Open (D5i; cap from designer-review.md).
- "№" from resx (D3).
- Loop badge, placement B (`badge/badge-B.png`): a pill before the chevron in the action cell of
  each active loop's "Цикл начало" row, text "(2/5)". Fill `#FFFFFF`, 1 px border `#5E35A8`, text
  `#5E35A8` 12 px SemiBold with tabular digits, height 20, corner radius 10, padding 8 left and
  right, 8 to the chevron. Colours come from palette keys, not literals.

## Gates

- Press latency, after Task 3 and at Task 9 (manager: autonomous-decisions.md):
  `dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only -method "*PressOn*"`
  → the maximum after one warm-up press is 100 ms or less in both grids. Quiet machine, 4 runs of
  6 presses, runs alternated with master. A miss stops the run.
- At Task 9, the full explicit suite
  `dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only`
  → exit code 0; the realization and byte gates in `docs/perf/README.md` hold. A miss stops the
  run for an operator decision.

## Acceptance

- A1 `dotnet build SemiStep.slnx` → exit code 0
- A2 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0, 0 failed
- A2b `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
- A3 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ComboOpen" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 15 tests; covers: all 14 ComboOpen
  behaviours on master (`CanonicalComboPressOpenTests` and the ComboOpen cases of
  `TransposedComboEditingTests`: press opens before the release and stays open, no pressed state
  left, Ctrl/Shift/right press opens nothing, Down+Enter picks with the row selected, Esc keeps
  the value, a press on a list item picks, a double press opens then closes, a light-dismiss press
  opens nothing), retargeted to the presenter's editor; plus with no edit a row holds no
  `ComboBox`, in both grids (new; fails on `62405ee`)
- A4 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ComboLock" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 9 tests; covers: the existing
  `CanonicalComboLockTests`, retagged from ComboOpen to ComboLock in Task 3; a chevron shows on the
  hovered cell and on the current cell and on no other combo cell (new; fails on `62405ee`); no
  chevron and no editor on press, F2, F4, Alt+Down while locked; lock arriving with an open list
  closes it and returns focus; press, lock, `RunJobs`, then no list and the display shown;
  inapplicable and read-only cells show no chevron; transposed chevron on the focused cell and
  none while locked
- A5 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=CellKeyboard" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 9 tests; covers: F2, F4, Alt+Down open
  a combo list; arrow from row 3 to row 5, F4 opens the list on row 5; Esc keeps the value; typing
  on a combo cell does nothing; typing on a text cell starts the edit with that character in both
  grids (canonical case new; fails on `62405ee`); F2, Enter, Esc on text cells; `IsEditing` true
  while a list is open
- A6 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ComboWheel" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 3 tests; covers: the master bug
  path, press a combo cell, Esc, then wheel or Up/Down, changes no value;
  `CanonicalClosedComboInputTests` retagged from ComboOpen to ComboWheel in Task 3 when that
  branch merged; without that branch these tests are regression guards written in Task 3
- A7 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=GridHeader" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 4 tests; covers: header font 12 / 600 /
  `#6C707E` read from the rendered header; one line; a real column at its default width whose
  header trims shows its full text as a tooltip; "№" equals the resx value
- A8 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=CellAlignment" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 4 tests; covers: shared right edge of
  values in one column with different units; unit slot left edge shared; text and comment cells
  left; inapplicable cell shows no text; transposed number cells show the unit slot with a shared
  right edge
- A9 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=RampTooltip" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 2 tests; covers: ramp step "Время" has
  the tooltip; a non-ramp "Время" has none
- A10 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=LoopBadge" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 6 tests; covers: counters (2,2,0) on the
  `loops.csv` shape give "(2/3)" on the outer and "(2/5)" on the inner "Цикл начало" row only; counters 0 or recipe inactive
  give no pill; run end clears every pill; pill style values read from the rendered pill; the name
  trims before the pill; a poll that changes only `StepCurrentTime` raises no row
  `PropertyChanged`
- A11 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=TransposedStepWidth" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 2 tests; covers: width equals
  the longest action name plus chrome, capped at 240; ellipsis past the cap; the tooltip always holds the full name
- A12 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=GridFocus" -- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, at least 4 tests; covers: focus inside the active
  grid after open, flip, New, Open
- Manual smoke (skipped - manager live check), driven by the manager through UI Automation (`dev:app-screenshots`), MBE preset,
  emulator via `--plc-endpoint`, `loops.csv`:
  1. Open the recipe, sync off → no chevron in any row; hover the action cell of row 3 → one
     chevron in that cell only.
  2. One click on the action cell of row 3 → its list is open after the click.
  3. Down, Enter → row 3 shows the next action as text; no list open.
  4. Arrow to an object cell, press F4 → list opens; Esc → list closes, value unchanged.
  5. Arrow to a "Задание" cell, type "7" → editor shows "7"; Enter → cell shows 7, right-aligned,
     unit in its slot.
  6. Headers → one line each, grey `#6C707E`, 12 px SemiBold; first header "№".
  7. Hover the "Время" cell of a "t°C плавно" step → tooltip "время рампы, не входит в
     длительность шага".
  8. Sync on, run the recipe → no chevron on hover; press on an action cell opens nothing; the
     "Цикл начало" rows of running loops show "(n/count)" pills matching the status bar loop text.
  9. Wait for the run to end → no pill remains.
  10. Ctrl+Shift+T → transposed grid; arrows move the current cell without a click; the step
      column width fits "t°C скачком + ждать" without an ellipsis.
  11. File > New, then File > Open → arrows move in the grid without a click each time.
- not visible to headless tests or smoke: real font widths, the pill and chevron rendering on real
  hardware, and "open before the release" on a real window (owned by A3, the probe in Caveats and
  the latency gate; posted UI Automation input does not split a click).

---

## Context

- Combo cells: `SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`, `ComboBoxPressOpener.cs`,
  `CellApplicabilityBinding.cs`, `DataGridEditorCloseBehavior.cs`,
  `CanonicalRecipeGridView.axaml.cs` (`IsEditing`, `BeginningEdit`).
- Lazy model: `RecipeGrid/Transposed/TransposedLazyCellPresenter.cs`,
  `TransposedComboCellPresenter.cs`, `TransposedTextCellPresenter.cs`,
  `TransposedTextEditCoordinator.cs`, `TransposedCellTemplateFactory.cs`,
  `TransposedGridSelectionController.cs`.
- Text and numbers: `TextCellFactory.cs`, `PropertyTimeMultiConverter.cs`,
  `ColumnWidthCalculator.cs`, `ColumnBuilder.cs` (numbering header literal, header template).
- Badge: `RecipeGridSurfaceBase.cs`, `RecipeRowViewModel.cs`; reference demo
  `.git/manager/ui/badge/badge.diff` (not to be copied: env switches and literals).
- Style: `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml`,
  `SemiStep.Core/Configuration/GridStyleOptions.cs`, `Styles/DataGridStyles.axaml`,
  `Styles/TransposedGridStyles.axaml`, `Styles/ColorPalette.axaml`, `Localization/Resources*.resx`.
- Focus: `RecipeGrid/RecipeGridHost.axaml.cs`, `MainWindow/MainWindow.axaml.cs`.
- Tests: `SemiStep.Tests/UI/RecipeGrid/*`, helpers `CanonicalGridWindow`, `BindingErrorGuard`;
  `docs/architecture/headless-ui-testing.md`.
- Branches merged before this one: `ui-toolbar-shortcuts` (batch A: `WindowGestures`,
  `RecipeGridSurfaceBase`, `RecipeRowViewModel`, `ColorPalette.axaml`, resx),
  `marks-survive-undo`, `ui-status-bar` (batch B: `TimeFormatHelper`, `grid_style.yaml`
  `chrome:`, `GridStyleOptions.Default`, resx), possibly `closed-combo-wheel`.

## Boundaries

- Task 2 creates the shared lazy cell presenter (focus-return target passed in) that Task 3
  consumes for canonical combo cells and Task 7 extends with the pill.
- Task 7 creates the shared active-loop derivation (active loops with their pass) that batch B's
  status bar and both surfaces consume.

## Tasks

### Task 1: Rebase onto the merged batches and take the baseline

**Goal:** the branch sits on `origin/master` with batch A, marks-survive-undo and batch B merged
**Entry:** `git fetch origin && git rebase origin/master`

- [x] confirm `git log origin/master` holds batches A, marks-survive-undo and B; stop if not
- [x] note whether `closed-combo-wheel` merged; if so, list its combo handler and tests for Task 3
- [x] baseline: A1, A2, A2b → exit code 0 before any change

### Task 2: One lazy cell presenter for both grids

**Goal:** the transposed grid behaves as today on a presenter that the canonical grid can reuse
**Entry:** `TransposedLazyCellPresenter`
**Likely touches:** `Transposed/TransposedLazyCellPresenter.cs`, `TransposedComboCellPresenter.cs`,
`TransposedCellTemplateFactory.cs`

- [x] move the swap, deferred open, close-ends-edit and blur exit into one presenter outside
      `Transposed/`; the focus-return target is passed in per grid
- [x] F2, printable keys and the text presenter stay transposed only
- [x] `TransposedTextEditCoordinator` moves with the presenter, one instance per grid
- [x] the deferred open re-reads the applicable-and-unlocked rule before opening
- [x] tests: every existing transposed editing and navigation test passes unchanged
- [x] run tests - must pass before the next task

### Task 3: Canonical combo cells show text until edit

**Goal:** X4 in the canonical grid, with press-to-open, lock, chevron rule and `IsEditing`
**Entry:** `ComboBoxCellFactory.CreateActionCellTemplate`, `ComboBoxPressOpener`
**Likely touches:** `ComboBoxCellFactory.cs`, `ComboBoxPressOpener.cs`, `CellApplicabilityBinding.cs`,
`CanonicalRecipeGridView.axaml.cs`, `DataGridEditorCloseBehavior.cs`, `DataGridStyles.axaml`

- [x] action and object cells render the shared presenter: text plus a chevron slot, not
      focusable; the editor is built on edit entry and opened after layout
- [x] a plain left press enters edit; the master tunnel rule (no open for a handled press) stays;
      the canonical view resolves the current cell and starts the edit on F2, F4 and Alt+Down
- [x] chevron on pointer-over and on the current cell, only when applicable and unlocked; the
      transposed chevron on the focused presenter, hidden while not effectively enabled
- [x] `IsEditing` true while the combo editor is live; the lock transition closes it, also before
      the deferred open
- [x] probe step: does a recycled DataGrid row detach its cells? If not, end the edit on a
      `DataContext` change
- [x] remove the closed-combo-wheel dead code (Caveats); remove the `:pressed` workaround only
      after the two pressed-state tests pass without it on the retargeted editor, with one pooled
      re-entry
- [x] retarget `GroupComboBoxRecyclingTests`, `ColumnBuilderIdempotencyTests`,
      `RecipeGridHostTests`, `CanonicalGridWindow.ComboAt` and `ComboPressOpenProbe` to the
      presenter's editor, keeping the recycling cases; the canonical probe method presses the
      display cell and reads the presenter's editor, and the press alone still opens the list
- [x] retag `CanonicalComboLockTests` to Area=ComboLock and `CanonicalClosedComboInputTests` to
      Area=ComboWheel
- [x] tests: A3, A4, A6, the F2/F4/Alt+Down part of A5; binding-error guard around a scroll
- [x] latency gate (Gates) after this task
- [x] run tests - must pass before the next task

### Task 4: Keyboard edit contract in both grids

**Goal:** D4 type-to-edit on text cells; F4 and Alt+Down in the transposed grid
**Entry:** `CanonicalRecipeGridView`, `TransposedGridNavigator`
**Likely touches:** `CanonicalRecipeGridView.axaml.cs`, `TextCellFactory.cs`,
`Transposed/TransposedGridNavigator.cs`

- [x] canonical: a printable key on the current text cell begins the DataGrid edit and seeds the
      typed character
- [x] transposed: F4 and Alt+Down open a combo list, as F2 does today
- [x] combo cells ignore printable keys in both grids
- [x] tests: the type-to-edit and transposed F4/Alt+Down parts of A5
- [x] run tests - must pass before the next task

### Task 5: Headers and the "№" resource

**Goal:** D5a and D3 header text
**Entry:** `ColumnBuilder.BuildWrappingHeaderTemplate`, `AddNumberingColumn`
**Likely touches:** three `grid_style.yaml`, `GridStyleOptions.cs`, `ColumnBuilder.cs`,
`ColumnWidthCalculator.cs`, `TransposedRecipeGridView.axaml`, `Resources.resx`, `Resources.ru.resx`

- [x] presets and Core default: header 12 / 600 / `#6C707E`
- [x] header template one line with ellipsis; every header carries its full text as a tooltip;
      the longest-word width floor stays
- [x] "№" from resx in the canonical header and the transposed corner
- [x] tests: A7; update config and width tests that pin 14 / 700 / black or two-line headers
- [x] run tests - must pass before the next task

### Task 6: Number alignment, unit slot, inapplicable text, ramp tooltip

**Goal:** D5b, D5c, D5g in both grids
**Entry:** `TextCellFactory.CreateMultiBindingTemplate`, `TransposedCellTemplateFactory`
**Likely touches:** `TextCellFactory.cs`, `PropertyTimeMultiConverter.cs`, `ColumnWidthCalculator.cs`,
`Transposed/TransposedCellTemplateFactory.cs`, resx

- [x] numeric cells: value right-aligned, unit in a slot sized per column (transposed: per grid,
      a new visible element there)
- [x] text and comment cells left-aligned; editors align as their display does
- [x] inapplicable cells render no text
- [x] ramp "Время" tooltip from resx on the explicit ramp key list; report the other immediate
      actions with a `step_duration` column to the operator
- [x] tests: A8, A9; update width tests for the value-plus-slot measure
- [x] run tests - must pass before the next task

### Task 7: Loop pass badge

**Goal:** D3 badge placement B in both grids
**Entry:** `RecipeGridSurfaceBase` `ExecutionState` subscription
**Likely touches:** `RecipeGridSurfaceBase.cs`, `RecipeRowViewModel.cs`, the shared presenter,
`ColorPalette.axaml`

- [x] one shared derivation returns the active loops with their pass; batch B's status bar and
      both surfaces consume it; refactor batch B's text-only version into it if one exists
- [x] recompute only when `RecipeActive`, `ActualLine` or `ForLoopCount1..3` changes and after
      mutation tails; write the pill text only on rows where it changes
- [x] the action display cell shows the pill between the trimming name and the chevron slot, style
      per UI design, colours as palette keys
- [x] pills clear when the recipe stops being active
- [x] tests: A10
- [x] run tests - must pass before the next task

### Task 8: Transposed step width and default grid focus

**Goal:** D5i
**Entry:** `TransposedRecipeGridView` `StepColumnWidthResourceKey`, `RecipeGridHost`
**Likely touches:** `TransposedRecipeGridView.axaml.cs`, `ColumnWidthCalculator.cs`,
`RecipeGridHost.axaml.cs`, `MainWindow.axaml.cs`

- [x] step column width from the longest action name with pill and chevron, and the widest value
      plus unit slot, plus padding, capped at 240, uniform
- [x] action name ellipsis past the cap; the tooltip always shows the full name
- [x] focus the active grid at open, flip, New, Open
- [x] tests: A11, A12; perf harness unchanged
- [x] run tests - must pass before the next task

### Task 9: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] A1 to A12 → exit code 0 with the stated test counts
- [x] latency gate and the full explicit suite (Gates) pass
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0

### Task 10: Update documentation

**Goal:** docs describe the display-cell model, keyboard contract, headers, alignment and badge
**Entry:** `docs/architecture/recipe-grid-surface.md`

- [x] `recipe-grid-surface.md`: "Combo cells: lock and press-to-open" and the lazy swap text for
      both grids; the keyboard contract
- [x] `recipe-grid-column-sizing.md`: one-line headers, unit slot, transposed step width
- [x] `grid-style-configuration.md`: new header defaults; `ui-localization.md`: new resx keys
- [x] `headless-ui-testing.md` if the deferred-open test pattern changes (unchanged: tests still pump the deferred open with `RunJobs`)

## Post-Completion

- the manager runs the smoke steps through UI Automation and reports anything posted input cannot
  verify as unverified
- installed machines get the new presets only through the installer (X9)
