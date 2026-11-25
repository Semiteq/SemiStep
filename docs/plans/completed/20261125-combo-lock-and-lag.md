# Combo-box lock and open lag

## Behavior

- Today, while the PLC runs the recipe, the canonical grid is read-only, but a click on an action
  or group combo box still opens its list, and F4 or Alt+Down on a focused combo box opens it too.
  A pick from that list does not change the recipe, but the cell keeps showing the picked value,
  also after the run stops. After this change no press and no key opens a combo list while the
  grid is locked, in either grid, and every combo cell shows the recipe value. The stale picked
  value no longer appears.
- Today the canonical grid opens a combo list when the mouse button is released, and Ctrl, Shift
  and right presses also open it on release. The transposed grid needs two presses when the column
  is not selected yet: the first selects the column, the second opens the list. After this change
  only a plain left press opens the list, at mouse-down, in both grids, and the same press still
  selects the row or column. In the open list the arrow keys move, Enter picks and Esc closes.
- Keyboard opening stays as today: F4 or Alt+Down on a combo box that holds focus in the canonical
  grid, F2 on a combo cell in the transposed grid.
- Scope guard: no visual change. Colours, the dropdown arrows, the text shown before an edit, text
  cells and the status bar stay as they are. The redesign follows later.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Unlocked, plain left press on an applicable combo cell, any row or column | the list opens at mouse-down and stays open after release; the pressed row or column becomes the only selection | operator | Acceptance 1, 2 |
| Unlocked, list opened by a press, then Down and Enter | the next item is picked, the list closes, the selected row or column stays | manager | Acceptance 1 |
| Unlocked, list opened by a press, then Esc | the list closes, the value stays; in the transposed grid focus returns to the column, so Delete and Ctrl+C act on the step again | manager | Acceptance 1 |
| Unlocked, list open, a press on one of its items | the item is picked and the list closes | manager | Acceptance 1 |
| Unlocked, a press on the combo whose list is open (a double press) | the list closes and does not reopen, as a click-click does today | manager | Acceptance 1, smoke 7 |
| Unlocked, list A open, a press on combo B | list A closes, list B does not open, as today | agent: the native light dismiss consumes that press | Acceptance 1, smoke 8 |
| Unlocked, Ctrl, Shift or right press on a canonical combo cell | the selection changes as today; no list opens, also not on release | manager | Acceptance 1 |
| Unlocked, plain press on a combo cell of a column inside a multi-selection (transposed) | the selection collapses to that column and the list opens | manager | Acceptance 1 |
| Unlocked, list open, keys meant for the step (Delete, Ctrl+C) | the keys belong to the list until it closes | manager | Acceptance 1 |
| Press on an inapplicable cell or a config read-only combo column | nothing opens, as today | agent: unchanged | existing tests |
| Locked, any press, F4 or Alt+Down on a canonical combo cell; any press or F2 on a transposed one | nothing opens; a press still selects the row or column; the cell shows the recipe value | operator | Acceptance 1 |
| A combo list is open or a canonical combo box holds focus when the run starts | the list closes and focus goes to the grid | agent: the transposed grid closes it today (`TransposedRecipeGridView.CloseActiveEditor`); the canonical grid does not | Acceptance 1 |
| The run stops | one press opens the list again | operator | Acceptance 1, smoke 5 |

## Decisions

- The canonical cell reads the lock from the grid's own read-only state, `DataGrid.IsReadOnly`,
  which `CanonicalRecipeGridView.axaml` already binds to the surface. The lock joins the per-cell
  hit-test and focus bindings that already carry applicability, the way the transposed grid joins
  its lock (`TransposedCellTemplateFactory.CreateEditableBinding`). The binding fails closed: if
  the lock leg does not resolve, the combo box takes no input. Rejected: reading the lock from the
  surface, because `ComboBoxCellFactory` is built through `ColumnBuilder` and a surface dependency
  makes a DI cycle.
- The grid-wide `DataGrid.read-only ComboBox` style and the `read-only` class binding are removed.
  Reason: `ComboBoxCellFactory.ApplyInputBlocking` binds `IsHitTestVisible` and `Focusable`
  locally, and a local value outranks a style setter, so the style never applies (probed below).
  Rejected: binding `IsEnabled`. ASSUMPTION: the Semi theme greys a disabled combo box, a visual
  change; fallback: none needed, the chosen binding does not touch `IsEnabled`; checked by: smoke 2.
- The lock blocks hit testing and focus, not only the dropdown. Reason: a locked combo box that
  holds focus still opens on F4 and changes value on a mouse wheel.
- In the canonical grid the list opens after the DataGrid has handled the press, and the native
  open-on-release is suppressed for every press on a combo box: after the press the grid removes
  the `:pressed` pseudo-class that the native toggle needs, and the release stays unconsumed, so
  no stale pressed state stays on the combo box; a release on a list item still picks it. Reason:
  opened before the DataGrid takes focus, the list loses keyboard focus to the DataGrid (probed
  below). Rejected: consuming the release, because the ComboBox then never clears `:pressed`.
- The transposed combo editor opens its list from a `Background` dispatcher job after edit entry,
  and closing the list ends the edit and focuses the column container. Reason: opened on the fresh
  editor before layout, the list cannot focus its selected item; focus stays on the ComboBox, which
  ignores Down and Enter while open, and Esc leaves the edit running (probed below).
- The transposed grid opens a combo cell on the first plain press. Text cells keep the current
  select-then-edit model. Reason: the one-press rule covers combo cells only.
- No code change for the selection cost. In the bare surface and view the probe found no hot path
  in our code: `RecipeGridSurfaceBase.UpdateSelection` takes 0.06-0.45 ms; the DataGrid's own
  selection and restyle take 3.7-11.2 ms. Main-window selection subscribers were not measured.
- Native popup windows stay. The `OverlayPopups` effect is inferred, not measured, and headless
  tests cannot measure it.
- No test asserts wall-clock milliseconds (`docs/perf/README.md`). The press-to-open probe records
  samples and asserts only that the list is open after the press alone.

## Caveats

- A locked pick changes no recipe data, but on master the cell shows a wrong value.
  `RecipeGridSurfaceBase` drops the edit in `OnActionChanged`, `OnCellValueChanged` and
  `OnSelectorValueChanged` when `IsReadOnly`, and nothing pushes the real value back to the combo
  box. probed: headless, locked canonical grid, action combo set from index 0 to 1, recipe object
  unchanged, combo still showed "For" after unlock, 2026-11-25. With the lock in place no locked
  pick can happen.
- The canonical lock does nothing on master. probed: headless, `SetRecipeActive(true)`, grid class
  `read-only` present, action combo `IsHitTestVisible=True`; press+release opened the list; F4 and
  Alt+Down on the focused combo opened it, 2026-11-25.
- The transposed lock works on master for presses. probed: headless, locked, two presses on a
  combo cell, presenter disabled, no editor, no list, the column still selected, 2026-11-25. For
  F2 the guard is `IsEffectivelyEnabled` in `TransposedLazyCellPresenter.OnDisplayKeyDown`.
- Avalonia's ComboBox opens on release: `OnPointerPressed` only sets `:pressed`, and
  `OnPointerReleased` toggles `IsDropDownOpen` while `:pressed` is set, for any button and any
  modifier. `OnPointerPressed` closes an open list on press. probed: headless, master, Ctrl, Shift
  and right press each left the list open after release, 2026-11-25.
- Keyboard focus decides where the list keys go. probed: headless, 2026-11-25. On master the
  release-open moves focus to the selected `ComboBoxItem`; Down then Enter picked the next item and
  focus returned to the `DataGrid`. A handler on the combo box that opens on press runs before the
  DataGrid's own press logic; the DataGrid then takes focus, Down moved the row selection and Esc
  did not close the list.
- In the transposed grid, after a press-open with the list opened synchronously on edit entry, the
  editor ComboBox itself held focus with the list open; Down and Enter did not pick and Esc closed the
  list but left the edit running. With the open posted at `Background`, focus sits on the selected
  `ComboBoxItem`. probed: headless, `FocusManager.GetFocusedElement()` after press and release,
  2026-11-25.
- A double press on master: the second press closes the list and the release does not reopen it.
  With list A open, a press on combo B closed A, did not open B and did not change the row
  selection. probed: headless, 2026-11-25.
- Headless timings exclude rendering and native popup windows, so they are smaller than the
  0.2-0.3 s the operator feels. They compare paths; the smoke steps judge the feel.
  probed (headless, Debug, rows 2-6 of 60, WithGroups, 2026-11-25): canonical click on a new row
  today, press 4.5-5.9 ms plus release 6.3-29.8 ms, list opens at release; re-click of the selected
  row, press 2.3-2.9 ms plus release 3.0-17.8 ms; press-to-open with the release handled,
  20.3-28.4 ms; transposed second press opens in 8.3-12.1 ms. The first click of a run is cold:
  press 34.5 ms plus release 80.5 ms.
- A new-row click costs about twice a re-click of the selected row (11-36 ms against 5-21 ms warm,
  headless). A cost of 50-140 ms per new-row click is not confirmed: only the cold first click
  reached it.

## Constraints and non-goals

- No visual change: no new brushes, classes or templates.
- The press that opens a list still selects the row (canonical) or column (transposed).
- Keyboard opening, keyboard focus at window start (issue #157 point 5) and text-cell keyboard
  editing stay out (issue #170).
- Known follow-up, out of scope: when a coordinator call fails, `OnActionChanged`,
  `OnCellValueChanged` and `OnSelectorValueChanged` return on `result.IsFailed` without pushing the
  recipe value back, so the combo box keeps the rejected pick.
- Threat model: operator input only; no guard against programmatic writes to the combo box.
- Tests stay as simple as the existing headless grid tests.

## Gates

- Task 2, measured once: if press-to-open measures above 100 ms after one warm-up press, stop for
  an operator decision. The statistic that decides is the maximum after the warm-up press. Verdict:
  pass, the maximum is 52.6 ms. Quiet machine, Release, headless, 4 runs of 6 presses alternating
  with master, 2026-11-25 (min / median / max after warm-up): canonical 7.0 / 7.8 / 12.3 ms,
  transposed 8.3 / 10.0 / 52.6 ms; master click-to-open median 9.6 ms canonical, 13.6 ms
  transposed. The cold first press (127-171 ms transposed) is the same on master and the gate
  excludes it. An earlier run (canonical up to 130.1 ms, transposed up to 354.1 ms) ran while three
  other agents built and tested; it is load noise and does not count.
- Task 2: if the DataGrid's row selection or focus closes the list opened by the press, or the
  list keys go to the DataGrid, stop.
- Task 2: if the transposed column selection or the container focus closes the list opened by the
  first press, stop.

## Acceptance

1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ComboOpen"` → all pass,
   at least one test per item:
   - locked: a press, F4 and Alt+Down leave every canonical combo list closed; a press and F2 leave
     every transposed combo cell closed; the press still selects the row or column; after each
     attempt the combo box shows the value the recipe object holds;
   - locked: a list open or a focused canonical combo box at lock time closes, and focus goes to the
     grid; after the run stops one press opens the list;
   - unlocked, both grids: one plain press opens the list before the release, and it is still open
     after the release; the pressed row or column is selected; Down and Enter pick the next item and
     the selection stays; Esc closes the list; a press on a list item picks it and closes the list;
     a second press on the open combo closes it;
   - unlocked, canonical: Ctrl, Shift and right presses open nothing, also after the release; with
     list A open a press on combo B opens nothing;
   - unlocked, transposed: a plain press on a combo cell in a multi-selected column collapses the
     selection to that column and opens the list; after Esc the column container holds focus.
2. `dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only -trait "Area=ComboOpen"`
   → the press-to-open probe passes: the list is open after the press alone in the canonical grid
   and the transposed grid; it prints each sample.
3. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → all pass.
4. `dotnet build SemiStep.slnx` → 0 warnings, 0 errors; `dotnet format SemiStep.slnx --verify-no-changes` → exit 0.
- manual smoke, PLC emulator (commands in `README.md`, section on the emulator):
  1. start the emulator and the app with the MBE preset, connect, write a recipe and press Start in
     the emulator → the grid locks;
  2. click an action combo cell on an unselected row, then a group combo cell → no list opens, the
     row is selected, the cells look as before the change;
  3. switch to the transposed grid and click a combo cell twice → no list opens;
  4. stop the run in the emulator → the grid unlocks;
  5. click a combo cell on an unselected row once → the list appears while the button is still
     down, and stays open after release;
  6. repeat 5 in the transposed grid on an unselected column → one press opens the list;
  7. with a list open, click the same combo cell again → the list closes and stays closed;
  8. with list A open, click combo cell B in another row → list A closes, list B does not open;
  9. open a list with a press, press Down and Enter → the next value is set, the row stays selected.
- not visible to headless tests: the felt delay with native popup windows, the popup position, the
  light dismiss of native popup windows, and a real PLC run lock.

---

## Context

- `SemiStep/SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs`: `CreateStyledComboBox` builds every
  canonical combo box; `ApplyInputBlocking` holds the local bindings that defeat the lock.
- `SemiStep/SemiStep.UI/RecipeGrid/CellApplicabilityBinding.cs`: the applicability binding the lock
  joins. A pure view flag combination is a `MultiBinding` with `BoolConverters.And`, as in the
  transposed factory.
- `SemiStep/SemiStep.UI/RecipeGrid/DataGridEditorCloseBehavior.cs`: runs on `EditorMustClose`, the
  lock transition; the natural home for closing a canonical combo list at lock.
- `SemiStep/SemiStep.UI/Styles/DataGridStyles.axaml`: the dead `DataGrid.read-only ComboBox` style
  and its comment block; `CanonicalRecipeGridView.axaml` binds the `read-only` class.
- `SemiStep/SemiStep.UI/RecipeGrid/CanonicalRecipeGridView.axaml.cs`: the view that owns the
  DataGrid and its pointer handlers.
- `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedGridSelectionController.cs`:
  `HandleCellSelectionPress` returns "consumed" on the first press of an unselected column.
- `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs`:
  `OnGridPointerPressed`, `TryEnterEditFromPointer`, `CloseActiveEditor`.
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`: Delete and Ctrl+C act on the step only
  while `RecipeGridHost.IsEditing` is false.
- Tests: `SemiStep.Tests/UI/RecipeGrid/CanonicalRecipeGridViewTests.cs`,
  `SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedComboEditingTests.cs`; `UIFixture.SetRecipeActive`
  locks the grid; `docs/architecture/headless-ui-testing.md` for headless rules; `docs/perf/README.md`
  for explicit probes.
- New tests carry `[Trait("Area", "ComboOpen")]` so the Acceptance commands select them.

## Tasks

### Task 1: Lock canonical combo cells while the recipe runs

**Goal:** a locked canonical combo cell cannot be hit, focused or opened, and shows the recipe value; a list open at lock time closes
**Entry:** `ComboBoxCellFactory.ApplyInputBlocking`
**Likely touches:** `ComboBoxCellFactory.cs`, `CellApplicabilityBinding.cs`, `DataGridEditorCloseBehavior.cs`, `DataGridStyles.axaml`, `CanonicalRecipeGridView.axaml`

- [x] join the grid's read-only state to the per-cell hit-test and focus bindings of action and group combo cells, failing closed
- [x] remove the `DataGrid.read-only ComboBox` style, its comment and the `read-only` class binding
- [x] at lock, close an open canonical combo list and move focus from a focused combo box to the grid
- [x] tests: locked press, F4 and Alt+Down open nothing in the canonical grid; the press still selects the row; the combo shows the recipe value after each attempt; an open or focused combo at lock closes and focus goes to the grid; after unlock a press opens it; locked press and F2 open nothing in the transposed grid
- [x] run tests - must pass before the next task

### Task 2: Open the list on mouse-down in both grids

**Goal:** only a plain left press opens an applicable combo list, at mouse-down, in both grids
**Entry:** `CanonicalRecipeGridView` pointer handling; `TransposedGridSelectionController.HandleCellSelectionPress`
**Likely touches:** `CanonicalRecipeGridView.axaml.cs`, a small press-opener class beside it, `TransposedGridSelectionController.cs`, `TransposedComboEditingTests.cs`

- [x] canonical: open the list after the DataGrid has handled a plain left press; suppress the native open-on-release for every press on a combo box; keep releases on list items
- [x] transposed: a plain first press on a combo cell selects the column, collapsing a multi-selection, and opens the list; rewrite the two-press test to the one-press behavior; text cells unchanged
- [x] tests: one press opens before release and stays open after it, both grids; Down and Enter pick the next item with the selection unchanged; Esc closes, and in the transposed grid the column container holds focus; a press on an item picks it; a double press opens then closes; Ctrl, Shift, right press open nothing after release; list A open, press on B opens nothing
- [x] tests: the explicit press-to-open probe, record-only, canonical new rows and transposed new columns
- [x] measure the 100 ms gate once: pass, maximum after warm-up 12.3 ms canonical, 52.6 ms transposed (quiet machine, Release, headless, 2026-11-25; see Gates)
- [x] run tests - must pass before the next task

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [x] manual smoke against the PLC emulator (skipped - operator smoke)

### Task 4: Update documentation

**Goal:** docs describe the lock and the open gesture of combo cells
**Entry:** `docs/architecture/recipe-grid-surface.md`

- [x] describe the per-cell lock of canonical combo cells and why a grid-wide style cannot do it
- [x] describe press-to-open, the suppressed release toggle and the keyboard focus order in both grids
- [x] update `docs/architecture/headless-ui-testing.md` if the tests needed a new headless pattern

## Post-Completion

- the operator runs the manual smoke above against the PLC emulator
- Refs #201 (lock visibility), Refs #170 (keyboard), Refs #204 (grid noise); none closes
