# Closed combo cells ignore arrows and the wheel

## Behavior
- Today, in the table view, a combo cell (action, object or any other selector column) that holds
  keyboard focus with its list closed changes its value on one Down or Up key or one mouse-wheel
  notch. An action change resets the whole step to the new action's defaults, so one stray key
  loses the step's settings.
- After the change, a closed combo cell never changes its value from Up, Down, PageUp, PageDown,
  Home, End or the mouse wheel. Up and Down move the row selection, as in any other cell. The wheel
  scrolls the grid.
- Opening the list stays the only way to change the value: a plain left press, F4 or Alt+Down.
  Inside an open list the keys work as today: arrows move the highlight, Enter picks, Esc and a
  click elsewhere pick nothing.
- Scope guard: the transposed view, the run lock and press-to-open stay as they are.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Focused closed combo, Down or Up | value unchanged, selection moves one row, the grid takes focus | operator: task brief | Acceptance 1 |
| Focused closed combo, PageUp, PageDown, Home, End | value unchanged, the grid handles the key | operator: task brief | Acceptance 1 |
| Focused closed combo, Left or Right | value unchanged, the grid moves the current column | agent: grid navigation as today | Acceptance 1 |
| Wheel over a focused closed combo | value unchanged, the grid scrolls, the grid takes focus | operator: task brief | Acceptance 1 |
| Wheel over an unfocused closed combo | value unchanged, the grid scrolls (as today) | agent: `ComboBox.OnPointerWheelChanged` gates on focus | existing behavior |
| Open list, Down then Enter | picks the next item | operator: task brief | Acceptance 1 |
| Focused closed combo, F4 or Alt+Down | opens the list | operator: task brief | Acceptance 1 |
| Run lock active | combo cannot take focus, nothing changes | existing lock | existing lock tests |

## Decisions
- Arrow keys: the canonical combo cell turns on Avalonia's directional (XY) keyboard navigation
  through the `XYFocus.NavigationModes` attached property. In that mode a closed `ComboBox` leaves
  Up and Down unhandled, so the key bubbles to the `DataGrid`, which moves the row and takes focus.
  Set once where `ComboBoxCellFactory` builds every canonical combo, so it covers every selector
  column. rejected: a `ComboBox` subclass that overrides key handling (avalonia.md prefers an
  attached property over a subclass); a tunnel `KeyDown` handler that marks the key handled (the
  `DataGrid` then skips it too, and its row navigation is private, so the key would have to be
  re-raised by hand).
- Mouse wheel: a tunnel `PointerWheelChanged` handler on the `DataGrid` hands focus from a focused
  closed combo to the grid before the combo sees the notch. The combo then ignores the wheel and the
  grid scrolls. It lives in its own small class next to `ComboBoxPressOpener`, because press-to-open
  is a different job. rejected: marking the notch handled (the grid stops scrolling; the `DataGrid`
  has no public scroll-by-delta call); a `ComboBox` property (none exists, see Caveats).

## Caveats
- `ComboBox.OnKeyDown` (Avalonia 12.0.5) changes the selection on Up or Down only when the list is
  closed and `XYFocus.NavigationModes` does not allow the key's device. It does not handle PageUp,
  PageDown, Home or End while closed.
  probed: ilspycmd decompile of `Avalonia.Controls.ComboBox` 12.0.5, 2026-11-25.
- With `XYFocus.NavigationModes` set on a focused closed action combo, Down leaves the value
  unchanged, the selection moves from row 3 to row 4, focus goes to the `DataGrid`, and a second
  Down moves to row 5.
  probed: headless throwaway test on 62405ee, 2026-11-25.
- `ComboBox.OnPointerWheelChanged` changes the selection only while the combo has focus and the
  list is closed; Avalonia 12.0.5 has no property that turns this off.
  probed: ilspycmd decompile, 2026-11-25.
- Moving focus to the `DataGrid` in a tunnel wheel handler leaves the value unchanged and scrolls
  the grid (vertical scroll bar 0 to 50 on a 60-row recipe in a 600 px window).
  probed: headless throwaway test on 62405ee, 2026-11-25.
- A press on a combo cell, followed by Esc, Enter, an item pick or a second press, leaves focus on
  the `DataGrid`, not on the combo. Only Tab puts keyboard focus on a combo cell, so the defect is
  reached through Tab.
  probed: headless throwaway test on 62405ee, 2026-11-25.
- The property inherits into the open list popup and does not change the list keys: Down then Enter
  in an open action or group list still picks the next item.
  probed: open-list tests with the change, 2026-11-25.
- Left and Right on a focused closed combo leave the value unchanged and keep focus inside the grid.
  probed: closed-combo key tests with the change, 2026-11-25.
- Not tested: arrows the open list leaves unhandled (Up at the first item, Left, Right) with the
  inherited XY mode.

## Constraints and non-goals
- Press-to-open, the run lock and all ComboOpen tests stay green.
- The transposed grid is out of scope: it has no live combo until edit.
- No new interface or subclass.

## Acceptance
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ComboOpen"` → 0 failed;
  fail-first on 62405ee: Up and Down value checks, Up and Down row moves, the wheel and typed
  letters, each for the action and object columns. The 15 cases for PageUp, PageDown, Home, End,
  Left, Right, F4, Alt+Down and the open-list pick pass on 62405ee: they guard the side effects of
  the XY navigation mode, not the defect. The Enter, Space and Alt+Up open keys, the open-list
  type-ahead and the real Tab press are guards of the same kind.
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → 0 failed.
- `dotnet build SemiStep.slnx` → exit 0; `dotnet format SemiStep.slnx --verify-no-changes` → exit 0.
- manual smoke: 1. click a cell left of the action column, press Tab until the action cell holds
  focus, press Down → the row below is selected and the action is unchanged. 2. Tab to the action
  cell again, roll the wheel over it → the grid scrolls and the action is unchanged.
- not visible to headless tests: a physical wheel with high-resolution deltas.

---

## Context
- `SemiStep.UI/RecipeGrid/ComboBoxCellFactory.cs` builds every canonical combo cell.
- `SemiStep.UI/RecipeGrid/ComboBoxPressOpener.cs` is the neighbor to follow for a DataGrid-level
  tunnel handler; `CanonicalRecipeGridView.axaml.cs` attaches it.
- Tests: `SemiStep.Tests/UI/RecipeGrid/CanonicalComboPressOpenTests.cs`, helper
  `SemiStep.Tests/UI/Helpers/CanonicalGridWindow.cs`.
- Docs: `docs/architecture/recipe-grid-surface.md` section "Combo cells", `docs/readme.md`
  section 2.4 (Russian).

## Tasks

### Task 1: Closed combo cells ignore arrows and the wheel

**Goal:** the Behavior and Flow branches hold in the canonical grid
**Entry:** `ComboBoxCellFactory`, `ComboBoxPressOpener`
**Likely touches:** `ComboBoxCellFactory.cs`, a new wheel handler class, `CanonicalRecipeGridView.axaml.cs`, `CanonicalGridWindow.cs`

- [x] tests first: closed focused combo keys and wheel for the action and object columns, open
      list pick, F4 and Alt+Down; record which fail on master
- [x] turn on directional keyboard navigation on canonical combo cells
- [x] add the wheel handler and attach it in the canonical view
- [x] run tests - must pass before the next task

### Task 2: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`

### Task 3: Update documentation

**Goal:** the docs describe the closed-list rule
**Entry:** `docs/architecture/recipe-grid-surface.md`, `docs/readme.md`

- [x] add the closed-list rule and its mechanism to the "Combo cells" section
- [x] add the user rule to section 2.4 of the user guide, in Russian

## Post-Completion
- the operator's manual smoke steps above
