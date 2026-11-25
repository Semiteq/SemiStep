# UI toolbar and shortcuts

## Behavior

- The toolbar reads, left to right: Открыть, Сохранить | Добавить, Удалить, Копировать, Вырезать,
  Вставить | Отменить, Повторить, and at the right end the toggle "Синхронизация с ПЛК". Today it
  starts at Добавить, has a separator between Удалить and Копировать, and has no file or sync
  controls.
- Toolbar captions are black. Icons are 16 px vector glyphs: grey `#6C707E` at rest, black on
  hover, `#A8ADBD` when the command is unavailable. A disabled caption is `#A8ADBD`. Today the
  captions are accent blue and the icons are PNG files.
- Every toolbar tooltip prints the command name and the same gesture text the menu prints, for
  example "Добавить шаг (Ctrl+N)".
- The sync toggle shows "on" while PLC sync is enabled and "off" while it is disabled. Its
  checked track is neutral grey, never green or accent. A press runs today's sync on/off
  command. While that switch is in progress (the initial connect after "on", or the up-to-5 s
  wait for a started write after "off"), the toggle is disabled.
- Файл gets a checkbox item "Синхронизация с ПЛК" bound to the same state and command.
- The status bar no longer has the sync button. Its other content (sync status text, last-sync
  text, timers, loop counters) stays as it is.
- Shortcuts come from one gesture table that the menu items show. Ctrl+N adds a step,
  Ctrl+Shift+N creates a new recipe, Ctrl+O opens, Ctrl+S saves, Ctrl+Shift+S saves as (dead
  today), Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, Delete removes the selected steps with
  undo. F2 starts editing the current cell, as it does today.
- No shortcut fires while a text field has focus. Delete, Ctrl+C, Ctrl+X and Ctrl+V also do
  nothing while a cell is being edited, and a combo box with its list open counts as editing.
- Правка gets "Снять отметки изменений", and the grid context menu gets the same item. It is
  enabled only while at least one orange change mark exists anywhere in the recipe, and it
  clears every mark in both orientations. No toolbar button, no shortcut.
- An action or selector change marks a cell orange when its new value differs from the value
  the row held for the same key before the change; a key the row did not hold counts as empty
  (decisions.md D7). An action change resets a typed comment to empty, so that comment cell is
  marked; a cell empty before and after is not. A value the operator types is never marked, on
  either orientation.
- Scope guard: the new-recipe command (menu item "Создать рецепт") stays in the menu only. The
  status bar content, the cell templates, keyboard editing inside cells, the grid palette and
  the window `MinWidth` belong to later batches. `ButtonDefaultPrimaryForeground` and its
  `ThemeTests` pin stay as they are.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Ctrl+N, focus on the grid, recipe editable | adds a step | operator: D4 | `MainWindowGestureTests` |
| Ctrl+Shift+N | runs Файл > "Создать рецепт" with today's `NewRecipeCommand` rules | operator: D4 | `MainWindowGestureTests` |
| Ctrl+O, Ctrl+S, Ctrl+Shift+S | open, save, save as | operator: D4; Ctrl+Shift+S dead today (`RecipeMenuBar.axaml` shows it, nothing handles it) | `MainWindowGestureTests` |
| Ctrl+Z; Ctrl+Y or Ctrl+Shift+Z | undo; redo | operator: D4 | `MainWindowGestureTests` |
| Delete, steps selected, no cell editor open, grid focused | removes the selected steps; Ctrl+Z restores them | operator: D4 | `MainWindowGestureTests` |
| Delete while a cell is being edited (text editor, or a combo with its list open, canonical or transposed) | the editor handles the key; no step is removed | operator: D4; manager: combo list open counts as editing | `MainWindowGestureTests` |
| Delete with a canonical combo cell focused, list closed | removes the selected steps, as with grid focus | manager: a closed combo is not editing | `MainWindowGestureTests` |
| Ctrl+Z or Ctrl+N with a canonical combo cell focused | undo or add step, as today | manager | `MainWindowGestureTests` |
| Any table gesture while a `TextBox` has focus | the text box handles the key or nothing happens; no command runs | operator: D4; probe below | `MainWindowGestureTests` |
| Ctrl+Shift+T while a cell editor is open | flips the orientation and commits the value | `ToggleOrientationCommandTests` pins it | existing tests stay green |
| Alt+F4 | the window system closes the window through the existing close guard; the gesture handler runs nothing | agent: display-only on "Выход" | `MainWindowGestureTests` |
| Gesture whose command cannot run now (recipe locked by a running PLC recipe, nothing to undo) | nothing happens | agent: the handler asks `ICommand.CanExecute` | `MainWindowGestureTests` |
| Gesture whose command throws | the failure is reported once to the notification log | agent: the command's `ReportThrownExceptions` | `MainWindowGestureTests` |
| F2 on the current cell, both orientations | starts editing, as today | operator: D4; grid-owned key | `MainWindowGestureTests` |
| Toolbar hidden through Вид > Панель инструментов | shortcuts still work; sync stays reachable through Файл | agent | `MainWindowGestureTests`, `SyncToggleTests` |
| Sync off, toggle or menu item pressed, enable succeeds | shows on; connecting and linked are both "on" | operator: D2 | `SyncToggleTests` |
| Sync off, toggle or menu item pressed, enable fails | returns to off; the failure goes to the notification log as today | agent: `ExecuteToggleSyncAsync` reports it | `SyncToggleTests` |
| Sync on, toggle pressed, a write is in flight | toggle and menu item stay disabled until disable completes (up to 5 s), then show off | operator: D2; #180 wait in `DisableSync` | `SyncToggleTests` |
| Background reconnect after a link loss | toggle stays enabled and checked, so the operator can switch sync off | agent: no switch of the toggle's own is running | `SyncToggleTests` |
| No change marks in the recipe | "Снять отметки изменений" is disabled in the menu and the context menu | operator: D2 | `ClearChangeMarksTests` |
| Marks exist; item pressed | every mark clears in both orientations; the recipe is not dirtied and no undo entry is added | operator: D2; a mark is a UI annotation (`cell-change-highlight.md`) | `ClearChangeMarksTests` |
| The last mark goes away by edit, click-away, execution start, a full rebuild or deleting its row | the item turns disabled | manager: one has-marks value per surface | `ClearChangeMarksTests` |
| PLC recipe running | the item is disabled: execution start cleared all marks, and editing is locked | `ExecutionHighlightTracker` | `ClearChangeMarksTests` |
| Action change, typed comment "abc" | the comment resets to empty and is marked in both orientations | operator: decisions.md D7 | `ValueChangeMarkingTests` |
| Action change, a cell empty before and after (comment never typed) | that cell is not marked | operator: decisions.md D7 | updated surface tests, `ValueChangeMarkingTests` |
| Action change, a numeric cell whose value changes, or stays equal | marked when it changes, not marked when it stays equal | operator: decisions.md D7 | `ValueChangeMarkingTests` |
| Selector change | only the cells whose value changed are marked; the selector cell is the operator's edit and stays unmarked | operator: decisions.md D7 | `ValueChangeMarkingTests` |
| Selector change on a PLC-loaded or CSV-loaded step that holds a hidden value | the reseeded cell is marked in both orientations | operator: decisions.md D7; a held key is no exemption | `ValueChangeMarkingTests` |
| CSV-loaded step without the selector key; the operator sets the selector | the selector cell stays unmarked, the cells it seeds are marked | coordinator: the edited cell is never marked | `ValueChangeMarkingTests` |
| Action change on a CSV-loaded step without a comment key | the empty comment is not marked: absent counts as empty, empty to empty is no change | operator: decisions.md D7 | `ValueChangeMarkingTests` |
| CSV-loaded step without a comment key; the operator types the first comment | the comment cell is marked on neither orientation | manager: an operator value is an edit, not a change to mark | `ValueChangeMarkingTests` |

## Decisions

- One gesture table drives the menu items, the window key handler and the tooltips. The window
  `OnKeyDown` searches every top-level menu for the item whose gesture matches, asks
  `ICommand.CanExecute` itself, and runs the item's command. Precedent: SemiPlot
  `MainWindow.OnKeyDown` plus `AppMenuBar.GoItemFor`, commit `f928554`. Rejected: window
  `KeyBinding` and `MenuItem.HotKey`. Both take the key from a focused `TextBox` (probe below).
- The handler skips a handled event and a `TextBox` source. It tests the source as well as
  `Handled`, because a `TextBox` lets Ctrl+N, Ctrl+S and F2 through unhandled (probe below).
- Redo has two gestures. The menu item and the tooltip print Ctrl+Y, the WinUI redo
  accelerator; the table maps Ctrl+Shift+Z to the same item.
- Ctrl+C, Ctrl+X, Ctrl+V and Ctrl+Shift+T move into the table with their current rules: the
  clipboard gestures and Delete keep the editing gate. Ctrl+Shift+T keeps firing from an open
  cell editor; it only changes the view and commits the open value.
- The editing gate is `RecipeGridHost.IsEditing` or a key source inside a combo box whose list
  is open. A canonical combo cell lives in `CellTemplate`, so an open list is not a `DataGrid`
  edit and `IsEditing` alone misses it. Branch `combo-lock-and-lag`, in flight, changes press
  handling in the combo cells; expect a rebase there.
- Alt+F4 stays display-only on "Выход" and is excluded from the handler: the window system
  closes the window and the existing close guard runs.
- F2 stays a grid key. The canonical `DataGrid` and `TransposedLazyCellPresenter` handle it
  today; this batch pins it with a test and builds nothing new.
- Black captions come from a `Foreground` setter on `Button.toolbar`, and a
  `Button.toolbar:disabled` setter reads the disabled palette key (`#A8ADBD`). Rejected:
  changing `ButtonDefaultPrimaryForeground`, which every default button reads and `ThemeTests`
  pins (ledger #203 C1). Rejected: Semi's `BorderlessButton` theme with the `Tertiary` class
  (SemiPlot's minimap steps). Its caption is the tertiary grey from Semi's component keys, not
  black, so it needs the same caption override plus overrides of keys that other borderless
  buttons share.
- Icons are `StreamGeometry` keys in `Styles/Icons.axaml`, drawn by `PathIcon` and tinted
  through `Foreground` from palette keys. The file and the key convention follow SemiPlot
  `SemiPlot.UI/Styles/Icons.axaml` (in-house 16 x 16 geometries, no third-party licence). That
  file has none of the nine toolbar glyphs, so they are drawn in-house on the same 16 px grid.
  The PNG set and its README are deleted.
- The toggle binds its checked state one way to `IsSyncEnabled` and runs `ToggleSyncCommand`.
  Its disabled state comes from the command: a running `ReactiveCommand` cannot execute, so the
  control is disabled for the whole switch. No hand-toggled flag.
- The Файл sync item follows the Вид pattern: `ToggleType=CheckBox`, one-way `IsChecked`,
  `Command`.
- `IRecipeGridSurface` owns the has-marks signal and the clear-all operation, implemented in
  `RecipeGridSurfaceBase`. Each surface keeps a notifying has-marks value, updated from each
  live row's `ChangedColumns` (wired where `CreateItemChecked` wires the row events) and from
  `Items` changes. `RebuildItem` marks after it replaces the item, so the row notification, not
  the collection change, raises the value. `ActiveRecipeGridSurface` ORs both surfaces, emits
  on subscription, and fans out clear-all to both.
- The marking rule is decisions.md D7 option (c): a cell is marked when its new value differs
  from the previous row's value for the same key, and an absent key counts as empty. One rule,
  no per-column cases. It replaces this batch's earlier "empty seeds are not marked" rule. The
  comparison lives in one place, `RecipeRowViewModel.ChangedValueKeys(previousStep, newStep)`,
  over the new step's keys only, so a key the new step drops is never marked. The action change
  (`RecipeGridSurfaceBase.RebuildItem`, with the replaced row's step) and the per-surface update
  of a selector change (`RecipeRowUpdateSynchronizer`) both call it, so the two orientations
  mark the same cells. Key presence never decides: a PLC-loaded or CSV-loaded step can hold a
  hidden value that a selector change reseeds, and that cell is marked. `OnSelectorValueChanged`
  applies no delta of its own: the coordinator raises `PropertyUpdated` before it returns, and
  the synchronizer already updated the originating row.
- The cell the operator edited is never marked, a typed cell or the selector cell; only cells
  changed as a consequence are. The synchronizer marks every changed key, then the edit path
  (`OnCellValueChanged`, `OnSelectorValueChanged`) clears the edited cell on both surfaces
  through the cross-surface clear channel. A formula recalculation after a typed edit is a
  consequence, so its cells are marked.
- The four `chrome:` sync fills (`connected`, `disconnected`, `local_mode`, `connecting`) and
  their brushes stay installed. They lose their last reader here; batch B (#201) decides their
  fate together with the link-state slot.
- Off-scale insets in the toolbar file (`Padding="4,2"`, separator `Margin="4,6"`,
  `Spacing="2"` and `6`) move to the 4/8 scale while the file is open (`dev:desktop-ui`
  spacing rule).

## Caveats

- Versions: Avalonia 12.0.5, Semi.Avalonia 12.0.3 (`SemiStep/Directory.Packages.props`).
- `MenuItem.InputGesture` only prints the gesture; it runs nothing.
  probed: headless xunit test, a `Menu` with a `MenuItem` built in code (`InputGesture`
  Ctrl+Shift+S plus a counting command), `KeyPressQwerty` with a `Button` focused; command
  count stayed 0. Avalonia 12.0.5, 2026-11-25.
- A window `KeyBinding` takes the key from a focused `TextBox`.
  probed: same test, window `KeyBinding` Ctrl+Z, `TextBox` focused after a Delete edit; the
  binding fired and the text box did not undo. 2026-11-25.
- `MenuItem.HotKey` and `Button.HotKey` also take the key from a focused `TextBox`, and a
  `MenuItem.HotKey` inside a submenu fires only after that submenu has been opened once.
  probed: same test; `HotKey` Ctrl+K and Delete on submenu items fired 0 times before the
  File submenu opened and 1 time after; with the `TextBox` focused, `HotKey` Delete fired and
  the text stayed "abc"; `Button.HotKey` Ctrl+Y fired from the `TextBox`. Shape probed: menu
  items created in code through `ItemsSource`, not inline AXAML items. 2026-11-25.
- A focused `TextBox` handles Delete and Ctrl+Z itself (event `Handled`), and lets Ctrl+N,
  Ctrl+S, Ctrl+K and F2 bubble to the window unhandled, with `Source` = the `TextBox`.
  probed: same test, a bubble handler on the window with `handledEventsToo`. 2026-11-25.
- ASSUMPTION: a window key handler that looks up the matching menu item runs its command while
  the menu has never been opened, because AXAML submenu items exist as logical children from
  the start; fallback: the handler reads the commands from the table directly and the menu items
  read their gestures from it; checked by: Task 1 gesture tests, which never open a menu first.
- ASSUMPTION: a `Foreground` setter on bare `Button.toolbar` does not override the theme's
  disabled and pointer-over arms, which set the content presenter's foreground at a higher
  priority; so the plan adds a `Button.toolbar:disabled` caption setter that reads the disabled
  palette key, and a pointer-over setter if the theme's hover arm changes the caption; fallback:
  the setters target the template's content presenter, with the read-back test below guarding
  the theme dependency; checked by: Task 2 tests that read the caption and icon colours back at
  rest, on hover and when disabled.
- ASSUMPTION: a `ToggleSwitch` with a one-way `IsChecked` binding and a `Command` returns to the
  bound value when the command finishes without changing it (the toggle sets its value through
  `SetCurrentValue`, and the view model raises `IsSyncEnabled` after every attempt); fallback:
  the view re-applies the bound value when the command finishes, no subclass of the control;
  checked by: the "enable fails" toggle test in Task 3.
- ASSUMPTION: Semi's `ToggleSwitch` theme reads its checked track and thumb brushes through
  `DynamicResource` component keys that can be overridden in the toolbar's scope; fallback: an
  own keyed `ControlTheme` for the toolbar toggle, per the `avalonia.md` customisation order,
  with a test that reads the rendered track colour back; checked by: Task 3.
- Between this batch and batch B, the status bar has no linked, no-link or connecting cue. That
  cue lived only in the sync button's fill (`AppStatusBar.axaml`, classes `sync-linked`,
  `sync-nolink`, `sync-connecting`); the toggle shows only on or off. `PlcSyncStatusText`
  stays.

## Constraints and non-goals

- `ToggleSyncCommand` semantics stay: enable, disable with the #180 wait, failure reported to
  the notification log.
- The link-state gap above is accepted: batch B executes directly after this batch merges, and
  no installer release ships between them.
- Shortcut behavior while a dialog (style editor, conflict dialog) is open is unchanged: those
  windows do not route through the main window handler.
- Not built: status-bar content (batch B), cell templates and in-cell keyboard editing
  (batch C), grid palette (batch D), a non-colour cue for the orange mark.
- Threat model: none; local desktop input only.
- Tests stay simple: drive real keys through the headless window, assert commands and state; no
  new test-only members on production types.

## UI design

Approval record: `.git/manager/ui/decisions.md` D1, D2 and D4. The implementing agent builds
this and does not redesign it.

- Toolbar groups, separated by the existing 1 px group separator: [Открыть, Сохранить]
  [Добавить, Удалить, Копировать, Вырезать, Вставить] [Отменить, Повторить]. No separator
  inside a group. The toggle "Синхронизация с ПЛК" sits at the right end of the same row.
- Each button: a 16 px icon, then the caption. Caption `#000000`, `#A8ADBD` disabled. Icon
  `#6C707E` at rest, `#000000` on pointer-over, `#A8ADBD` disabled.
- Toggle: the switch with its function label "Синхронизация с ПЛК". Checked: track `#6C707E`,
  white thumb. Unchecked: `#A8ADBD` outline. Never green, never accent `#3574F0`.
- Tooltip text: the command's context label plus the gesture in parentheses, the same text the
  menu prints (for example "Сохранить рецепт (Ctrl+S)"). A command with no gesture shows its
  label only.
- Menu: "Создать рецепт" shows Ctrl+Shift+N, "Добавить шаг" Ctrl+N, "Сохранить как" Ctrl+Shift+S,
  "Повторить" Ctrl+Y. "Снять отметки изменений" is the last item of Правка, after a separator,
  with no gesture. The grid context menu adds the same item after a separator. Файл gets the
  checkbox item "Синхронизация с ПЛК" after "Сохранить как", before the separator above
  "Выход".
- New resource strings, Russian and English: toolbar captions "Открыть", "Сохранить", the
  toggle and menu label "Синхронизация с ПЛК", the item "Снять отметки изменений". Each fits on
  one line.

## Gates

- If a gesture test shows that the menu-item lookup does not run a never-opened item's command
  (first ASSUMPTION), apply its fallback; if the fallback also fails, stop and report.
- If the toggle cannot reach the grey checked look without a selector on a theme-private part,
  stop and show the operator the rendered result before continuing.

## Acceptance

- `dotnet build SemiStep.slnx` → exit code 0, 0 warnings
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m` → exit
  code 0, 0 failed
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=MainWindow" --list-tests`
  lists `MainWindowGestureTests`, `RecipeToolBarTests` and `SyncToggleTests`; the same filter
  without `--list-tests`, plus `-- RunConfiguration.TreatNoTestsAsError=true` → exit code 0,
  0 failed. The set proves: each table gesture runs its command with focus on the grid and with
  no menu opened; each runs nothing with a `TextBox` focused; Ctrl+Shift+S runs save as;
  Ctrl+Y and Ctrl+Shift+Z both redo; Delete removes the selection and Ctrl+Z restores it;
  Delete with an open text editor or an open combo list (both orientations) removes nothing;
  Delete with a closed canonical combo focused removes the step; Ctrl+Z and Ctrl+N fire from a
  focused canonical combo; Alt+F4 runs nothing through the handler; a throwing command is
  reported once; F2 starts editing in both orientations; the toolbar buttons appear in the
  approved order with an icon each and tooltips equal to the menu gesture text; caption and
  icon colours read back right at rest, on hover and when disabled; the toggle and the Файл
  item are checked exactly while sync is enabled, return to off after a failed enable, and are
  disabled while a switch is in flight; the status bar has no sync button
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=ChangeHighlight" --list-tests`
  lists `ClearChangeMarksTests` and `ValueChangeMarkingTests`; the same filter without
  `--list-tests`, plus `-- RunConfiguration.TreatNoTestsAsError=true` → exit code 0, 0 failed.
  The set proves: the clear-marks item is disabled with no marks, enabled with one, clears
  every mark in both orientations, turns disabled when the last marked row is deleted, and
  appears in the grid context menu; an action change marks a wiped comment and a changed
  numeric cell and leaves an empty-to-empty or equal cell unmarked, a selector change marks only
  changed cells, in both orientations; a first typed comment on a CSV step is marked on neither
  orientation
- manual smoke:
  1. Launch with the MBE preset and open a recipe → the toolbar reads Открыть, Сохранить |
     Добавить … Вставить | Отменить, Повторить, with the toggle at the right end; captions
     black, icons grey.
  2. Hover a toolbar button, then select nothing so Удалить is disabled → the hovered icon turns
     black; the disabled icon and caption are pale grey.
  3. Switch the toggle on, then off → the checked track is grey, not green or blue; the toggle
     is unavailable for the moment it takes to switch.
  4. Hide the toolbar through Вид, press Ctrl+N → a step is added; Файл > "Синхронизация с ПЛК"
     switches sync.
  5. Change a step's action → cells whose value changed turn orange, an empty "Комментарий" does
     not, a typed one is wiped and turns orange; Правка >
     "Снять отметки изменений" clears them and then turns unavailable.
- not visible to headless tests: hover colour on a real pointer, the toggle's look at real
  DPI, tooltip timing.

---

## Context

- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml`: `Window.KeyBindings` (Ctrl+O, Ctrl+S,
  Ctrl+Z, Ctrl+Shift+Z, Ctrl+N) and the grid `Panel.ContextMenu`.
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`: `OnKeyDown` (Ctrl+Shift+T, Delete,
  Ctrl+C/X/V behind `RecipeGridHost.IsEditing`).
- `SemiStep/SemiStep.UI/MainWindow/RecipeMenuBar.axaml`: display-only `InputGesture` values.
- `SemiStep/SemiStep.UI/MainWindow/RecipeToolBar.axaml`, `Assets/Icons/*.png`,
  `Assets/Icons/README.md`.
- `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`: `Button.sync-toggle` and its styles.
- `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`: `ToggleSyncCommand`,
  `IsSyncEnabled` and the `IsSync*` state members.
- `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs`, `IRecipeGridSurface.cs`,
  `ActiveRecipeGridSurface.cs`, `RecipeGridSurfaceBase.cs`, `RecipeRowViewModel.cs`
  (`ChangedColumns` and its mutators), `RecipeRowUpdateSynchronizer.cs`,
  `ExecutionHighlightTracker.cs`, `ChangedCellClickAwayBroadcaster.cs`.
- `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`, `App.axaml` (palette keys; `App.axaml` stays
  an include manifest).
- `SemiStep/SemiStep.UI/Localization/Resources.resx`, `Resources.ru.resx`;
  `ResourceTranslationTests` pins key parity.
- SemiPlot precedent: `SemiPlot.UI/MainWindow/MainWindow.axaml.cs` `OnKeyDown`,
  `AppMenuBar.axaml.cs` `GoItemFor`, `Navigation/WindowGestures.cs`, `Styles/Icons.axaml`.
- Tests: `SemiStep.Tests/UI/MainWindow/RecipeToolBarTests.cs` (asserts an `Image` per button,
  changes with the icons), `ToggleOrientationCommandTests.cs` (key-press pattern,
  Ctrl+Shift+T with an open editor), `MainWindowViewModelSyncStateTests.cs`,
  `SemiStep.Tests/UI/RecipeGrid/CanonicalRecipeGridSurfaceTests.cs`,
  `Transposed/TransposedRecipeGridSurfaceTests.cs`,
  `SemiStep.Tests/Domain/PlcLifecycleManagerDisableSyncTests.cs` (the #180 wait),
  `docs/architecture/headless-ui-testing.md`.
- Rules: `CLAUDE.md`, `~/.claude/rules/avalonia.md` (customisation order, compiled bindings),
  `dev:desktop-ui` (spacing, palette keys, one term per concept).

## Boundaries

- `IRecipeGridSurface.HasChangedMarks`: an observable of bool that emits the current value on
  subscription. `IRecipeGridSurface.ClearAllChangedMarks()`: clears every mark;
  `ActiveRecipeGridSurface` applies it to both orientation surfaces. Task 5 builds them; the
  menu and context menu bind the command built on them.

## Tasks

### Task 1: One gesture table and the window key route

**Goal:** every shortcut comes from one table shown by the menu, runs from the window key
handler, and never fires from a focused text box
**Entry:** `MainWindow.axaml` `Window.KeyBindings` and `MainWindow.axaml.cs` `OnKeyDown`
**Likely touches:** `MainWindow.axaml(.cs)`, `RecipeMenuBar.axaml(.cs)`, a new gesture table
type in `MainWindow/`

- [x] build the table: Ctrl+N add step, Ctrl+Shift+N new recipe, Ctrl+O, Ctrl+S, Ctrl+Shift+S,
      Ctrl+Z, Ctrl+Y plus Ctrl+Shift+Z for redo, Delete, Ctrl+C/X/V, Ctrl+Shift+T; Alt+F4
      display-only and excluded from the handler
- [x] menu items read their gesture text from the table; remove `Window.KeyBindings`
- [x] window key handler: skip a handled event and a `TextBox` source (Ctrl+Shift+T excepted),
      apply the editing gate (`IsEditing` or an open combo list) to Delete and Ctrl+C/X/V,
      search every top-level menu, ask `ICommand.CanExecute`, run the command
- [x] tests (`MainWindowGestureTests`): each gesture runs its command with focus on the grid and
      no menu opened; each runs nothing with a `TextBox` focused; Ctrl+Shift+S runs save as;
      both redo gestures redo; Delete removes the selection, Ctrl+Z restores it; Delete with an
      open text editor, an open canonical combo list and an open transposed combo removes
      nothing; Delete with a closed canonical combo focused removes the step; Ctrl+Z and Ctrl+N
      fire from a focused canonical combo; Alt+F4 runs nothing through the handler; a throwing
      command is reported once; F2 starts editing in both orientations
- [x] run tests - must pass before the next task

### Task 2: Toolbar order, vector icons and black captions

**Goal:** the toolbar matches the UI design section, with tooltips that print the table's
gesture text
**Entry:** `RecipeToolBar.axaml`
**Likely touches:** `RecipeToolBar.axaml`, `Styles/Icons.axaml` included from `App.axaml`,
`ColorPalette.axaml`, `Resources*.resx`, `Assets/Icons/`

- [x] add Открыть and Сохранить as the first group; drop the separator between Удалить and
      Копировать; keep the order from the UI design section
- [x] replace the PNG `Image`s with 16 px `PathIcon` geometries in `Styles/Icons.axaml`, tinted
      from palette keys (rest, hover, disabled); delete the PNGs and `Assets/Icons/README.md`
- [x] `Foreground` setter on `Button.toolbar` for black captions plus a `:disabled` caption
      setter from the disabled palette key; move the toolbar insets to the 4/8 scale (the `:disabled`
      caption setter proved inert, the theme's disabled arm already paints `#A8ADBD`; dropped)
- [x] tooltips: context label plus the table's gesture text
- [x] tests (`RecipeToolBarTests`): command bindings including Open and Save, button order, an
      icon per button, tooltip text equal to the menu gesture text, caption and icon colours
      read back at rest, on hover and when disabled; `ThemeTests` stays unchanged and green
- [x] run tests - must pass before the next task

### Task 3: Sync toggle in the toolbar and the menu, out of the status bar

**Goal:** the toolbar's right end holds the grey "Синхронизация с ПЛК" toggle, Файл holds the
same switch as a checkbox item, and the status-bar sync button is gone
**Entry:** `AppStatusBar.axaml` `Button.sync-toggle`, `MainWindowViewModel.ToggleSyncCommand`
**Likely touches:** `RecipeToolBar.axaml`, `RecipeMenuBar.axaml`, `AppStatusBar.axaml`,
`ColorPalette.axaml`, `Resources*.resx`, `MainWindowViewModel.cs`,
`MainWindowViewModelSyncStateTests.cs`

- [x] add the toggle at the toolbar's right end and the Файл checkbox item: checked state one
      way from `IsSyncEnabled`, `ToggleSyncCommand` as the command
- [x] grey checked track and white thumb, `#A8ADBD` unchecked outline, through the
      `avalonia.md` customisation order (see the Semi `ToggleSwitch` ASSUMPTION and Gates)
- [x] remove the status-bar sync button, its styles, the `IsSync*` members other than
      `IsSyncEnabled`, their assertions in `MainWindowViewModelSyncStateTests`, and the resx
      keys with no other reader (`StatusSyncOn`, `StatusSyncOff`, `StatusConnecting`)
- [x] tests (`SyncToggleTests`): the toggle and the menu item are checked exactly while sync is
      enabled (off, connecting, linked, no link); both return to off after a failed enable;
      both are disabled while a disable is in flight and enabled after; the status bar has no
      sync control
- [x] run tests - must pass before the next task

### Task 4: Mark only cells whose value changed

**Goal:** an action or selector change marks only cells whose value changed (decisions.md D7), and
an operator-typed value is marked on neither orientation
**Entry:** `RecipeRowViewModel.MarkChanged`, `RecipeGridSurfaceBase.RebuildItem`
**Likely touches:** `RecipeRowViewModel.cs`, `RecipeGridSurfaceBase.cs`,
`RecipeRowUpdateSynchronizer.cs`, `CanonicalRecipeGridSurfaceTests.cs`,
`TransposedRecipeGridSurfaceTests.cs`

- [x] one row-level comparison of the old step against the new step drives every mark path
      (action change, cross-surface update of a selector change)
- [x] an operator edit clears the cell on both surfaces through the cross-surface clear channel
- [x] `CanonicalRecipeGridSurfaceTests` and `TransposedRecipeGridSurfaceTests`
      `ChangeStepAction_MarksOnlyCellsWhoseValueChanged`: Wait to For marks only `task`
- [x] tests (`ValueChangeMarkingTests`, both surfaces): a typed comment is wiped and marked; an
      empty-to-empty cell is not marked; a changed numeric cell is marked and an equal one is
      not; a selector change marks only changed cells; an action change on a CSV-shaped step
      without a comment key leaves the comment unmarked; its first typed comment is marked on
      neither surface; the mirror marks an added non-empty key and not an added empty one
- [x] run tests - must pass before the next task

### Task 5: "Снять отметки изменений"

**Goal:** one command, in Правка and in the grid context menu, enabled only while a mark exists,
that clears every mark in both orientations
**Entry:** `IRecipeGridSurface`, `RecipeGridSurfaceBase.CreateItemChecked`
**Likely touches:** `IRecipeGridSurface.cs`, `RecipeGridSurfaceBase.cs`,
`ActiveRecipeGridSurface.cs`, `RecipeCommandsViewModel.cs`, `RecipeMenuBar.axaml`,
`MainWindow.axaml`, `Resources*.resx`

- [x] per-surface has-marks value from row notifications and `Items` changes; the OR and the
      clear-all fan-out in `ActiveRecipeGridSurface` (Boundaries)
- [x] the command with `HasChangedMarks` as its `canExecute`; it does not dirty the recipe or add
      an undo entry
- [x] the item at the end of Правка and in the grid context menu, no gesture
- [x] tests (`ClearChangeMarksTests`): disabled with no marks; enabled after an action change;
      one press clears every mark on both surfaces; disabled again after the last mark goes by
      edit, click-away, execution start, a full rebuild or deleting its row; the context menu
      holds the item bound to the same command
- [x] run tests - must pass before the next task

### Task 6: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] every Acceptance command passes with its expected result
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`
- [x] manual smoke steps 1 to 5 (skipped - operator smoke)

### Task 7: Update documentation

**Goal:** docs describe the gesture table, the toolbar, the toggle and the clear-marks command
**Entry:** `docs/architecture/cell-change-highlight.md`

- [x] `cell-change-highlight.md`: the D7 value comparison, the both-surface edit clear, the
      clear-all trigger and the has-marks signal; fix the stale `CellChangedBrush` (installed
      from `colors.cells.changed` by `CellPaletteInstaller`) and `AccentBrush` (now
      `SelectionBackgroundBrush`) text
- [x] `dialogs-and-interactions.md`: a gesture section (one table, the handled and `TextBox`
      source rule, the editing gate with the open combo list, the Ctrl+Shift+T exception, the
      probe results)
- [x] `grid-style-configuration.md`: the four `chrome:` sync fills no longer paint a status-bar
      button
- [x] update `CLAUDE.md` only if a convention changed (no convention changed)

## Post-Completion

- The operator's manual test: the Acceptance smoke steps 1 to 5, plus hover colour, the toggle
  at real DPI and tooltip timing.
- The PR body closes #203 and #169 and carries `Refs #201` for the toggle move.
- Batch B owns the link-state cue the status bar loses here and the fate of the four `chrome:`
  sync fills.
