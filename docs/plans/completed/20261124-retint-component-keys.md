# Retint Semi's chrome through the component keys

## Behavior
- Today `SemiStep.UI/App.axaml` retints Semi.Avalonia to the JetBrains Int UI light palette by
  overriding primitive keys (`SemiColorPrimary`, `SemiColorText1`, ...). Semi's control themes read
  derived component keys that resolve the primitives with `StaticResource` inside Semi's own
  dictionaries, so buttons, the menu bar, text, text boxes and checkboxes keep Semi's stock colours
  (`#0064FA` accent, `#1C1F23` text). Four of the eight primitive overrides move nothing. The grid
  and the status bar already follow the palette through SemiStep's own keys.
- After this change the chrome paints the palette: the accent `#3574F0` on default-button captions
  (toolbar labels), primary buttons, focused text-box borders and checked checkboxes; black text on
  windows, labels, menus, text boxes and checkboxes; JetBrains greys for gesture, placeholder and
  disabled text. The four primitive overrides with no consumer are deleted. These keys apply in
  every SemiStep window, not only the three the screenshots show.
- Headless tests show real controls and read back what they paint, one assertion per overridden
  key, so an override that stops working fails a test.
- The repository's skill `.claude/skills/avalonia-visual-style/SKILL.md` teaches the component keys
  as the tier that moves a control, so the next retint does not repeat the inert one.
- Scope guard: no layout, size, font or radius change; the grid, the status bar, the cell and
  execution palettes (`CellPaletteInstaller`, `ExecutionPaletteInstaller`) stay as they are. Light
  only; dark stays unreachable as today. Text boxes keep Semi's borderless filled look.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Main window idle | Toolbar labels `#3574F0`, menu text black, disabled toolbar text `#A8ADBD` | operator: approved screenshots | A1, A4 smoke |
| Edit menu open | Item text black, gestures `#818594`, disabled items `#A8ADBD` | operator: approved screenshots | A1, A4 smoke |
| Grid-style editor | "Сохранить" primary button and dropdown arrows `#3574F0`, labels black | operator: approved screenshots | A4 smoke |
| Another window with default buttons (a confirmation dialog) | Same palette: captions `#3574F0`, text black | agent: the keys are app-wide | A4 smoke |
| Primary button hover / pressed | `#3369D6` / `#315FBD`, background and border | operator: approved values | A1 |
| Checkbox checked / hover / pressed; unchecked hover and pressed border | `#3574F0` / `#3369D6` / `#315FBD`; `#3574F0` and `#315FBD` | operator: approved values | A1 |
| Text box focused | Border `#3574F0` | operator: approved values | A1 |
| Color picker flyout in the grid-style editor | Selected tab icon, checked RGB/HSV fill and unchecked RGB/HSV text `#3574F0` | master behaviour, kept | A1 |
| A component override stops reaching its control (theme update, key renamed) | A theme test fails | issue #187 | A1 |

## Decisions
- Override Semi's component keys, the ones its templates read through `DynamicResource`, in the
  existing Light theme dictionary of `App.axaml`. Each key was confirmed on 2026-11-23 by reading a
  realised control in the real `SemiStep.UI.App`; a lookup through `Application.TryGetResource` is
  not enough, because it returns the declaration whether or not a control reads it.
  `TextBlockDefaultForeground` is read from a `SelectableTextBlock`: Semi's plain `TextBlock` theme has
  no rest foreground setter, so a plain label inherits `WindowDefaultForeground` from its window, and
  `TextBlockDefaultForeground` paints `SelectableTextBlock` and `Label`. Rejected: keep
  overriding primitives (they do not reach the controls).
- Delete `SemiColorText1`, `SemiColorText3`, `SemiColorBackground1` and `SemiColorDisabledText`: the
  probe found no consumer. Keep `SemiColorText0` (read by SemiStep's own setters), `SemiColorText2`
  (read by SemiStep's setters and the DataGrid theme), `SemiColorBorder` (read by the DataGrid
  column-header separator) and `SemiColorPrimary` (read by the ColorPicker theme: its `Light.axaml`
  aliases `ColorViewTabItemSelectedForeground`, `ColorViewRadioButtonForeground` and
  `ColorViewRadioButtonCheckedBackground` to it with `StaticResource` and does not define it, so the
  key resolves from the app; without the override a realised `ColorView` paints those parts `#0064FA`,
  with it `#3574F0`).
- The overrides stay in `App.axaml` beside the existing ones. Rejected: moving the retint to its own
  resource file now; that is a layout change outside this issue.
- The theme tests follow SemiPlot's `EverySemiControl_PaintsItselfFromThePalette`
  (`SemiPlot/SemiPlot.Tests.Unit/UI/ThemeTests.cs`): realise each control in a shown window; reach
  hover and press through headless pointer input, focus through `Focus()`, checked and disabled
  through the properties; assert that the state's pseudo-class is present before reading the brush,
  so a test that never reached the state cannot pass. A test cannot set pseudo-classes itself:
  `PseudoClasses` is protected and `Classes.Add(":pointerover")` throws.
- One test per control family (text and window, default button, primary button, checkbox, text box,
  menu), each building its controls directly. Rejected: one theory over a role table, which needs a
  per-role construction switch.

## UI design
Approved by the operator on 2026-11-24 from before/after screenshots of the main window, the open
Edit menu and the grid-style editor. The values, Light theme:

| Value | Component keys |
|---|---|
| `#000000` | `TextBlockDefaultForeground`, `WindowDefaultForeground`, `TextBoxForeground`, `CheckBoxForeground`, `MenuItemForeground`, `MenuItemPointeroverForeground` |
| `#818594` | `MenuItemInputGestureTextForeground`, `TextBoxPlaceholderForeground` |
| `#A8ADBD` | `TextBlockDisabledForeground`, `ButtonDefaultDisabledForeground`, `TextBoxDisabledForeground`, `CheckBoxDisabledForeground`, `MenuItemDisabledForeground`, `MenuItemDisabledInputGestureTextForeground` |
| `#3574F0` | `ButtonDefaultPrimaryForeground`, `ButtonSolidPrimaryBackground`, `ButtonSolidPrimaryBorderBrush`, `TextBoxFocusBorderBrush`, `CheckBoxCheckedDefaultBackground`, `CheckBoxCheckedDefaultBorderBrush`, `CheckBoxPointeroverBorderBrush` |
| `#3369D6` | `ButtonSolidPrimaryPointeroverBackground`, `ButtonSolidPrimaryPointeroverBorderBrush`, `CheckBoxCheckedPointeroverBackground`, `CheckBoxCheckedPointeroverBorderBrush` |
| `#315FBD` | `ButtonSolidPrimaryPressedBackground`, `ButtonSolidPrimaryPressedBorderBrush`, `CheckBoxCheckedPressedBackground`, `CheckBoxCheckedPressedBorderBrush`, `CheckBoxPressedBorderBrush` |

Not changed: the text-box rest border (transparent, filled box), the default button hover and
pressed fills (neutral grey).

## Caveats
- The headless test host runs `SemiStep.UI.App` (`SemiStep.Tests/TestAppBuilder.cs`), so the theme
  tests read the real `App.axaml` overrides; headless pointer input already works in this repo
  (`RecipeGridHostTests` drives `window.MouseDown`).
- ASSUMPTION: each realised control exposes the painted brush on the template part the 2026-11-23
  probe read (button `PART_ContentPresenter`, checkbox `NormalRectangle` and `PART_ContentPresenter`,
  text box `PART_ContentPresenterBorder`, menu item `PART_HeaderPresenter` and `PART_InputGestureText`);
  `TextBlockDefaultForeground` is read from a `SelectableTextBlock`, not a plain `TextBlock`, because a
  plain `TextBlock` takes its rest foreground from the window; fallback: read the nearest template part that
  paints and name it in the test; checked by: the tests failing on master before the overrides and
  passing after.
- The PLC emulator's own `App.axaml` carries the same primitive retint (`SemiStep.Tools.PlcEmulator`).

## Constraints and non-goals
- No dark theme, no font, size, radius or layout change, no change to the cell or execution
  palettes.
- The PLC emulator's `App.axaml` keeps its primitive retint; its chrome is out of scope for #187.
- Tests stay simple: one test per control family, no shared probe framework.

## Gates
- none

## Acceptance
1. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ThemeTests"` → all
   pass. Every key in the `## UI design` table has an assertion, equal-value background and border
   keys separately. The tests fail on master before the overrides are added (red check for all keys
   at once) and fail again when one override is removed afterwards.
2. `dotnet build SemiStep.slnx` → 0 warnings; `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed.
3. `git grep -n -w -e SemiColorText1 -e SemiColorText3 -e SemiColorBackground1 -e SemiColorDisabledText -- SemiStep/SemiStep.UI`
   → no output.
4. Manual smoke: start SemiStep with MBE; the toolbar, the Edit menu and the grid-style editor look
   like the approved after-screenshots; one confirmation dialog shows the same palette.

---

## Context
- `SemiStep/SemiStep.UI/App.axaml` (Light theme dictionary and its retint comment),
  `SemiStep/SemiStep.UI/Styles/Buttons.axaml` (its `Button.primary` comment names `SemiColorPrimary`).
- SemiPlot reference test: `SemiPlot/SemiPlot.Tests.Unit/UI/ThemeTests.cs` in the SemiPlot repository
  (pointer helpers, `top.Open()` for the menu pointer-over key).
- Palette tests live in `SemiStep.Tests/UI/Styles/`; traits `Component=UI`, an `Area`, `Category=Unit`.
  Headless UI testing: `docs/architecture/headless-ui-testing.md`.
- Skill to correct: `.claude/skills/avalonia-visual-style/SKILL.md`, passages: the colour XML
  example, the claim that redefining any key at a higher scope works live, the three-tier list with
  "retarget these", the "core retint" JetBrains-to-Semi mapping table, the "override options" list
  with "smallest blast radius, preferred", and the advice to find keys by reading Semi's AXAML.
- Docs: `docs/architecture/grid-style-configuration.md` and any other page found by
  `grep -rln "SemiColor\|retint" docs/architecture`.

## Tasks

### Task 1: Component-key overrides and the theme tests

**Goal:** the chrome paints the approved palette, the inert primitives are gone, and tests read every overridden brush back
**Entry:** `SemiStep/SemiStep.UI/App.axaml`
**Likely touches:** `App.axaml`, `Styles/Buttons.axaml`, a new `SemiStep.Tests/UI/Styles/ThemeTests.cs`

- [x] write the theme tests first: one test per control family, one assertion per key in the `## UI design` table (border keys separately from equal-value backgrounds), states reached through pointer input, `Focus()` and properties with the pseudo-class asserted first; run them on the unchanged code and see them fail
- [x] add the component-key overrides to the Light theme dictionary and delete the four inert primitive overrides; rewrite the retint comment in `App.axaml` and the `Button.primary` comment in `Buttons.axaml` to name the component keys
- [x] the tests pass; remove one override, see one fail, restore it
- [x] run tests - must pass before the next task

### Task 2: Correct the visual-style skill

**Goal:** the skill teaches the component keys as the tier that moves a control
**Entry:** `.claude/skills/avalonia-visual-style/SKILL.md`

- [x] state that Semi's control themes read component keys through `DynamicResource` and resolve primitives with `StaticResource`, so primitive overrides do not reach controls; lead with the component tier; replace "smallest blast radius, preferred" for colour; tell the reader to confirm a key by reading a realised control, not `Application.TryGetResource`, and point to `ThemeTests`
- [x] the colour example and the mapping table show component keys; primitives appear only as what the component keys alias; `grep -n -e "retarget these" -e "core retint" .claude/skills/avalonia-visual-style/SKILL.md` prints nothing

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance 1 to 3 pass
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 4: [Final] Update documentation

**Goal:** the docs describe the retint as component-key overrides
**Entry:** `docs/architecture/grid-style-configuration.md`

- [x] update any page that describes the primitive retint; record which primitives stay and why
- [x] CLAUDE.md only if a convention changed (no change: it is an overview file and no convention changed)

## Post-Completion
- the operator's manual test: Acceptance 4
- closes #187
