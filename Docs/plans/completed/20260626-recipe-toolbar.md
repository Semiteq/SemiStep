# Recipe Row-Actions Toolbar (toggleable from View)

## Overview
- Add a horizontal toolbar with buttons for the recipe row actions — Add, Delete, Copy, Cut, Paste, Undo, Redo — placed between the menu bar and the `DataGrid` in `MainWindow`.
- Buttons reuse the existing commands; no new command logic is introduced. Each button's enabled state comes for free from the bound `ReactiveCommand.CanExecute`.
- The toolbar is toggleable from the `View` menu (default: visible), mirroring the existing `Notification Log` toggle.
- Problem solved: row actions are currently reachable only via the `Edit` menu, the grid context menu, and keyboard shortcuts. An operator who does not know the shortcuts must open a menu. A visible toolbar improves discoverability and gives one-click access.
- Integrates as a new `Auto` grid row; the `Edit` menu and grid context menu stay unchanged (multiple entry points to one command are intentional, per issue #64).

## Context (from discovery)
- Files/components involved:
  - `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — root grid `RowDefinitions="Auto,*,Auto,Auto"` (menu / grid / message panel / status bar). New toolbar row goes between menu and grid.
  - `SemiStep/SemiStep.UI/MainWindow/RecipeMenuBar.axaml` — `UserControl` with `x:DataType="local:MainWindowViewModel"`, no own VM; `View` menu has `Notification Log` (`MessagePanel.ToggleCommand`) and `Grid Style Settings...`.
  - `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` — exposes `RecipeCommands` (`AddStepCommand`, `DeleteStepCommand`, `UndoCommand`, `RedoCommand`) and `Clipboard` (`CopyStepCommand`, `CutStepCommand`, `PasteStepCommand`); owns `ExitCommand` / `OpenStyleEditorCommand` as `ReactiveCommand`.
  - `SemiStep/SemiStep.UI/RecipeGrid/RecipeCommandsViewModel.cs` — all commands are `ReactiveCommand<Unit, Unit>` with `CanExecute` observables already wired.
  - `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs` — the toggle pattern to mirror (`ToggleCommand` flips a bool; control `IsVisible` binds to it).
  - `SemiStep/SemiStep.UI/SemiStep.UI.csproj` — no `AvaloniaResource` asset glob yet; needs one for icon PNGs.
- Related patterns found:
  - `RecipeMenuBar` is the template for a chromeless `UserControl` bound to `MainWindowViewModel`.
  - `MessagePanelViewModel.ToggleCommand = ReactiveCommand.Create(() => { IsVisible = !IsVisible; })` is the toggle template.
  - UI VM tests use `UIFixture` + `[AvaloniaFact]` (see `SemiStep.Tests/UI/MainWindowViewModelReportingTests.cs`); headless window/control tests exist (`SemiStep.Tests/UI/StyleEditor/GridStyleEditorWindowTests.cs`).
- Dependencies identified:
  - Icon source: `C:\Users\admin\projects\NtoLib\NtoLib\Resources` — 40x40 32bpp BMP line-art with alpha. Usable for the toolbar: `ImgInsertAfter.bmp` (Add), `ImgDelete.bmp` (Delete). No matching icons for Copy/Cut/Paste/Undo/Redo in that set (`ImgOpen`/`ImgSave`/`ImgWrite` are File/PLC actions, out of scope).

## Development Approach
- **testing approach**: Regular (code first, then tests in the same task).
- Complete each task fully before moving to the next.
- Make small, focused changes; keep build + full test suite green and `dotnet format` clean after each task.
- **Every task includes new/updated tests** for the code it changes.
- All tests must pass before starting the next task.
- Maintain backward compatibility: `Edit` menu, grid context menu, and keyboard shortcuts stay untouched.

## Testing Strategy
- **unit tests**: `MainWindowViewModel` toggle command (flips state, defaults visible).
- **headless UI tests** (`[AvaloniaFact]`): `RecipeToolBar` instantiates and binds without throwing; a named button's `IsEnabled` reflects its command `CanExecute` (pump `Dispatcher.UIThread.RunJobs()` first).
- Reuse `UIFixture` for VM construction; reuse the headless control-build pattern from `GridStyleEditorWindowTests`.
- **Headless PNG decode is confirmed working** in this test app (`UseHeadless(AvaloniaHeadlessPlatformOptions)` without Skia): a throwaway spike decoded a 40x40 PNG via `new Bitmap(path)` and assigned it to an `Image.Source` with no exception. So `avares:`-sourced icons are safe to construct inside headless control tests; icon rendering does not need to be quarantined to manual verification.
- **`MainWindow` integration (row placement + `IsVisible` binding + View-menu item) is verified by build + manual Win32 check, not a headless test.** No existing test constructs the full `MainWindow` control (it pulls in the DataGrid column-build graph), and asserting `Grid.Row` integers is brittle. The toggle logic is already covered by the Task 1 VM unit test; the XAML wiring is declarative and low-risk.

## Progress Tracking
- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with the ➕ prefix; blockers with ⚠️.
- Keep this plan in sync with actual work.

## Solution Overview
- **Toggle state lives on `MainWindowViewModel`**, not a dedicated VM. The toolbar has no state of its own — its buttons re-trigger commands that already live on `MainWindowViewModel`, and the `UserControl` must bind to `MainWindowViewModel` anyway (like `RecipeMenuBar`) to reach them. A dedicated VM would hold a single bool and could not reach the commands without extra injection. So: add `bool IsToolBarVisible` (default `true`) + `ReactiveCommand ToggleToolBarCommand` to `MainWindowViewModel`.
- **`RecipeToolBar` is a chromeless `UserControl`** (`x:DataType="local:MainWindowViewModel"`, no own VM), modelled on `RecipeMenuBar`. It holds a horizontal band of flat buttons grouped by separators: `Add | Delete | Copy Cut Paste | Undo Redo`. Each button binds `Command` to the existing command, so enabled state follows `CanExecute` automatically.
- **Icons are "icon + always-visible text".** Every button shows its caption, so partial icon coverage does not look broken. Add and Delete also show an icon now (PNGs converted from the NtoLib BMPs). The remaining five buttons are text-only until matching line-art icons are supplied — purely additive, no rework.
- **View menu** gets a `_Toolbar` toggle bound to `ToggleToolBarCommand`, next to `Notification Log`.

## Technical Details
- Root grid changes in `MainWindow.axaml`: `RowDefinitions="Auto,*,Auto,Auto"` -> `RowDefinitions="Auto,Auto,*,Auto,Auto"`. Menu stays `Grid.Row=0`; new `RecipeToolBar` `Grid.Row=1`; `DataGrid` `1->2`; `MessagePanel` `2->3`; `AppStatusBar` `3->4`. Toolbar `IsVisible="{Binding IsToolBarVisible}"`.
- `RecipeToolBar` button -> command map (all already on `MainWindowViewModel`):
  - Add -> `RecipeCommands.AddStepCommand`
  - Delete -> `RecipeCommands.DeleteStepCommand`
  - Copy -> `Clipboard.CopyStepCommand`
  - Cut -> `Clipboard.CutStepCommand`
  - Paste -> `Clipboard.PasteStepCommand`
  - Undo -> `RecipeCommands.UndoCommand`
  - Redo -> `RecipeCommands.RedoCommand`
- Icon assets: convert `NtoLib\Resources\ImgInsertAfter.bmp` -> `Assets/Icons/add.png`, `ImgDelete.bmp` -> `Assets/Icons/delete.png` (PNG with alpha). Reference via `avares://Semistep/Assets/Icons/<name>.png` (assembly name is `Semistep`). Add `<AvaloniaResource Include="Assets/**" />` to the UI csproj.
- Flat toolbar button look: a local `Styles` block on the `UserControl` (transparent background, light hover), so buttons read as a toolbar rather than chunky Fluent buttons. Disabled buttons dim via default `:disabled` opacity.
- Separators: vertical dividers between command groups (thin `Border`/styled `Separator`) inside a horizontal `StackPanel`.

## What Goes Where
- **Implementation Steps** (`[ ]`): VM toggle state, `RecipeToolBar` control, icon assets, `MainWindow`/`View`-menu wiring, tests, docs.
- **Post-Completion** (no checkboxes): supplying the five missing icons; manual visual check on real Win32.

## Implementation Steps

### Task 1: Add toolbar visibility state to MainWindowViewModel

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelToolBarToggleTests.cs`

- [x] add `private bool _isToolBarVisible = true;` and `public bool IsToolBarVisible { get => _isToolBarVisible; private set => this.RaiseAndSetIfChanged(ref _isToolBarVisible, value); }` (mirror `MessagePanelViewModel.IsVisible`; `private set` is enough since the toggle lambda is in-class)
- [x] add `public ReactiveCommand<Unit, Unit> ToggleToolBarCommand { get; }`, initialized as `ReactiveCommand.Create(() => { IsToolBarVisible = !IsToolBarVisible; })`
- [x] write test: `IsToolBarVisible` defaults to `true`
- [x] write test: executing `ToggleToolBarCommand` flips `IsToolBarVisible` (true->false->true)
- [x] run tests - must pass before next task

### Task 2: Create RecipeToolBar UserControl bound to existing commands

**Files:**
- Create: `SemiStep/SemiStep.UI/MainWindow/RecipeToolBar.axaml`
- Create: `SemiStep/SemiStep.UI/MainWindow/RecipeToolBar.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj` (add `Compile Update` / `DependentUpon` entry, mirroring `RecipeMenuBar`)
- Create: `SemiStep/SemiStep.Tests/UI/MainWindow/RecipeToolBarTests.cs`

- [x] create `RecipeToolBar.axaml` as a `UserControl` with `x:DataType="local:MainWindowViewModel"` (mirror `RecipeMenuBar` header); horizontal `StackPanel` of flat buttons, icon+text captions, grouped `Add | Delete | Copy Cut Paste | Undo Redo` with vertical separators
- [x] bind each button `Command` to its existing command per the map in Technical Details (no new commands)
- [x] give every button an `x:Name` (`AddButton`/`DeleteButton`/`CopyButton`/`CutButton`/`PasteButton`/`UndoButton`/`RedoButton`) for deterministic lookup
- [x] add a local flat-toolbar-button `Styles` block (transparent background, hover highlight)
- [x] create `RecipeToolBar.axaml.cs` code-behind (`InitializeComponent`, mirror `RecipeMenuBar.axaml.cs`); add the csproj `Compile Update`/`DependentUpon` entry
- [x] write headless test (`[AvaloniaFact]`): constructing `RecipeToolBar` with a `UIFixture`-built `MainWindowViewModel` DataContext builds and exposes all seven action buttons
- [x] write headless test: each button's `Command` is the same instance as the corresponding VM command (proves wiring without depending on headless `CanExecuteChanged` timing; `CanExecute` logic itself is covered by `RecipeCommandsViewModelCanExecuteTests`)
- [x] run tests - must pass before next task

### Task 3: Add Add/Delete icon assets and wire them into the buttons

**Files:**
- Create: `SemiStep/SemiStep.UI/Assets/Icons/add.png`
- Create: `SemiStep/SemiStep.UI/Assets/Icons/delete.png`
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj` (add `<AvaloniaResource Include="Assets/**" />`)
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeToolBar.axaml`

- [x] convert `NtoLib\Resources\ImgInsertAfter.bmp` -> `Assets/Icons/add.png` and `ImgDelete.bmp` -> `Assets/Icons/delete.png` via Python PIL (preserve the 32bpp alpha). Provenance: NtoLib is the same author's sibling project, so reuse is fine — no third-party licensing concern.
- [x] add `<AvaloniaResource Include="Assets/**" />` to the UI csproj (with `Remove` for `*.md` so the folder README is not embedded)
- [x] all seven buttons are "icon + text" (Image from `avares://Semistep/Assets/Icons/*.png` beside the caption). The user supplied real 256x256 line-art for all seven; a folder `README.md` documents the name->action map for future swaps.
- [x] confirm the Task 2 headless test still passes after icons are added (decode was spiked green); added an assertion that the Add button content includes an `Image`
- [x] run tests - must pass before next task

### Task 4: Insert toolbar into MainWindow and add the View-menu toggle

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml`
- Modify: `SemiStep/SemiStep.UI/MainWindow/RecipeMenuBar.axaml`

- [x] in `MainWindow.axaml`: change root `RowDefinitions` to `Auto,Auto,*,Auto,Auto`; add `<local:RecipeToolBar Grid.Row="1" IsVisible="{Binding IsToolBarVisible}" />`; bump `DataGrid` to `Grid.Row=2`, `MessagePanel` to `3`, `AppStatusBar` to `4`
- [x] in `RecipeMenuBar.axaml`: add `<MenuItem Header="_Toolbar" Command="{Binding ToggleToolBarCommand}" />` in the `View` menu next to `Notification Log` (no checked/toggle glyph — deliberately consistent with the existing `Notification Log` item, which also shows none)
- [x] verify by build + manual: no automated headless test here. The toggle logic is unit-tested in Task 1; this task is declarative XAML wiring (row placement, `IsVisible` binding, menu item). Constructing the full `MainWindow` headless pulls in the DataGrid column-build graph and is untested territory; `Grid.Row` integer assertions are brittle and low-value.
- [x] build the UI project and confirm it compiles: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`

### Task 5: Verify acceptance criteria
- [x] verify all issue #64 tasks are met: toolbar with the 7 buttons bound to existing commands; inserted as an `Auto` row between menu and grid; enabled-state bound to `CanExecute`; `View` toggle default visible
- [x] verify `Edit` menu, grid context menu, and keyboard shortcuts are unchanged
- [x] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (947 passed, 0 failed)
- [x] run `dotnet format SemiStep/SemiStep.slnx` and confirm clean (verify-no-changes exit 0)
- [x] build the UI entry project: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` (0 errors)

### Task 6: [Final] Documentation and cleanup
- [x] no new pattern — reuses RecipeMenuBar/MessagePanel patterns, no CLAUDE.md change
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
*Items requiring manual intervention or external systems - no checkboxes, informational only*

**Manual verification:**
- Visual check on real Win32: toolbar placement, button spacing, separators, icon rendering, flat hover, disabled dimming.
- Confirm toggling from the `View` menu hides/shows the toolbar without disturbing grid layout.

**External / follow-up:**
- All seven icons are now real (user-supplied 256x256 line-art). The `cut.png` glyph reads as a crop/trim mark rather than scissors; swap it later if a scissors glyph is preferred — copy a new PNG over `Assets/Icons/cut.png`, no code change.
