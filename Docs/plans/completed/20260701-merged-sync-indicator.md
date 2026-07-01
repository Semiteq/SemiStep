# Merged color-coded sync/connection control (Issue #65)

## Overview
Merge the two separate status-bar elements that describe the PLC link into one prominent,
color-coded toggle button:
- the left plain "Sync: ON/OFF" `Button` (reads as text, `FontSize=12`), and
- the far-right 8px `Ellipse` + "Connected/Disconnected" label (barely noticeable).

The single button sits on the left (where the sync toggle is now). Its fill color encodes the
combined state of `IsSyncEnabled` + `IsConnectedToPlc`; the right-hand indicator is removed.
Clicking still toggles sync (`ToggleSyncCommand`). Covers feedback points 2, 3 and partially 13
(drop the "Out of sync" text).

State-to-color mapping:

| IsSyncEnabled | IsConnectedToPlc | Color | Brush | Meaning |
|---|---|---|---|---|
| false | — | grey | `LocalModeBrush` (new) | local mode, PLC not used |
| true | false | red | `DisconnectedBrush` (exists, `#FF4444`) | sync on, no PLC link |
| true | true | green | `ConnectedBrush` (exists, `#44BB44`) | sync on, link up |

Only **one** new palette color is needed — the grey "local mode". The issue text assumed two new
brushes; in fact `DisconnectedColor` already defaults to red and `ConnectedColor` to green.

## Context (from discovery)
- **UI files**: `SemiStep.UI/MainWindow/AppStatusBar.axaml` (sync toggle 92-103, connection indicator
  193-206, `connection-dot` styles 10-18), `SemiStep.UI/MainWindow/MainWindowViewModel.cs`
  (`IsConnectedToPlc`/`ConnectionStatus`/`IsSyncEnabled`/`PlcSyncStatusText` 120-134,
  `RaiseConnectionStateProperties`/`MapSyncStatus` 242-262).
- **Palette is config-driven, not literal AXAML.** `ColorPalette.axaml` holds no status brushes; they
  are installed at runtime by `CellPaletteInstaller` from `GridStyleOptions`. Adding a color threads
  through: `GridStyleOptions` → `GridStyleChromeColorsDto` → `GridStyleMapper` / `GridStyleDtoMapper`
  → `GridStyleValidator` → `CellPaletteInstaller` → Grid Style editor picker.
- **Existing color precedent**: `ConnectedColor: "#44BB44"`, `DisconnectedColor: "#FF4444"` in
  `GridStyleOptions.Default`; mirror this exactly for `LocalModeColor`.
- **Global Button baseline**: `Styles/Buttons.axaml` sets `Button` `MinHeight=26`, `Padding=12,4` —
  must be overridden locally so the toggle does not inflate the thin bar.
- **Semi.Avalonia gotcha**: the `Button` ControlTheme paints `ContentPresenter#PART_ContentPresenter`
  and overrides its `Background` on `:pointerover`/`:pressed`; the state color must be re-asserted on
  the presenter or hover reverts to Semi's neutral grey.

## Development Approach
- **Testing approach**: Regular (code first, then tests) — matches project convention (xUnit).
- Complete each task fully before the next; run tests after each change.
- Every task with logic changes includes tests. UI-only AXAML styling is verified at runtime (`/run`),
  not unit-tested (no headless assertion for brush-on-hover).
- All tests pass before starting the next task.
- One PR = one logical change (this feature). Branch `merged-sync-indicator` off fresh `origin/master`.

## Testing Strategy
- **Unit tests**: `MainWindowViewModel` state booleans + `PlcSyncStatusText` mapping;
  `CellPaletteInstaller` installs `LocalModeBrush`; `GridStyleMapper` maps/falls back `local_mode`.
- **No e2e harness** in this project. The button's color-on-hover/pressed behavior and cross-room
  salience are verified manually via `/run` (listed under Post-Completion).

## Progress Tracking
- Mark `[x]` immediately when done. `➕` for newly discovered tasks, `⚠️` for blockers.
- Keep this file in sync if scope shifts.

## Solution Overview
The grey "local mode" color joins the config-driven palette exactly like the other chrome colors.
The VM exposes three derived booleans (`IsSyncLocalMode`, `IsSyncNoLink`, `IsSyncLinked`) that drive
style classes on a single toggle button. The button carries a filled state color, a hairline border,
padding, radius 4, and SemiBold 13px white text, sized down to fit the status bar. Hover/pressed
re-assert the state color on the Semi content presenter, with a translucent scrim for press feedback
(no extra pressed brushes — keeps the new config surface to one color). The right-hand ellipse
indicator is removed (button color now carries connection state). The secondary `PlcSyncStatusText`
label **stays** — the button color does not encode the pipeline states `Syncing.../Synced/Failed`,
so that text remains next to the button (issue #65 requirement). Only the `OutOfSync => "Out of sync"`
mapping is dropped (mapped to empty). `Last sync` stays.

## Technical Details

### Design spec (from avalonia-ui-reviewer, JetBrains/Semi tokens)
- **`LocalModeColor` default**: `#6C707E` (JetBrains neutral grey; white text ≈ 4.6:1, reads across the
  room). Not `#9E9E9E` — white-on-`#9E9E9E` is ≈ 2.6:1, too faint against the `#F7F8FA` bar.
- **Foreground**: white + `SemiBold` on all three states (button reads as one object that changes fill).
  Do not alter the shared `ConnectedBrush`; white on green `#44BB44` is acceptable for a SemiBold
  indicator label.
- **Content**: text `Sync ON` / `Sync OFF`, no dot, no icon (fill already carries link state; no icon
  system exists in the bar). `MinWidth=76` so width does not jump on toggle.

### Button style block (goes in `AppStatusBar.axaml` `UserControl.Styles`)
```xml
<Style Selector="Button.sync-toggle">
    <Setter Property="Theme" Value="{StaticResource {x:Type Button}}" />
    <Setter Property="CornerRadius" Value="4" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="BorderBrush" Value="#33000000" />
    <Setter Property="Padding" Value="10,3" />
    <Setter Property="MinHeight" Value="24" />
    <Setter Property="MinWidth" Value="76" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="#FFFFFF" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="Cursor" Value="Hand" />
</Style>

<Style Selector="Button.sync-toggle.sync-local">
    <Setter Property="Background" Value="{DynamicResource LocalModeBrush}" />
</Style>
<Style Selector="Button.sync-toggle.sync-nolink">
    <Setter Property="Background" Value="{DynamicResource DisconnectedBrush}" />
</Style>
<Style Selector="Button.sync-toggle.sync-linked">
    <Setter Property="Background" Value="{DynamicResource ConnectedBrush}" />
</Style>

<!-- Hold the state color on hover/press; Semi otherwise repaints the presenter grey -->
<Style Selector="Button.sync-toggle.sync-local:pointerover /template/ ContentPresenter#PART_ContentPresenter,
                 Button.sync-toggle.sync-local:pressed    /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource LocalModeBrush}" />
</Style>
<Style Selector="Button.sync-toggle.sync-nolink:pointerover /template/ ContentPresenter#PART_ContentPresenter,
                 Button.sync-toggle.sync-nolink:pressed    /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource DisconnectedBrush}" />
</Style>
<Style Selector="Button.sync-toggle.sync-linked:pointerover /template/ ContentPresenter#PART_ContentPresenter,
                 Button.sync-toggle.sync-linked:pressed    /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource ConnectedBrush}" />
</Style>
<Style Selector="Button.sync-toggle:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="#22000000" />
</Style>
```
State selectors must follow the base `Button.sync-toggle` in the same `Styles` block (last-match-wins).

### Button markup (replaces AppStatusBar.axaml 92-103)
```xml
<Button Command="{Binding ToggleSyncCommand}"
        Classes="sync-toggle"
        Classes.sync-local="{Binding IsSyncLocalMode}"
        Classes.sync-nolink="{Binding IsSyncNoLink}"
        Classes.sync-linked="{Binding IsSyncLinked}">
    <Panel>
        <TextBlock Text="Sync ON"  IsVisible="{Binding IsSyncEnabled}" />
        <TextBlock Text="Sync OFF" IsVisible="{Binding IsSyncEnabled,
                                    Converter={x:Static BoolConverters.Not}}" />
    </Panel>
</Button>
```

### VM state derivation
- `IsSyncLocalMode => !IsSyncEnabled`
- `IsSyncNoLink    => IsSyncEnabled && !IsConnectedToPlc`
- `IsSyncLinked    => IsSyncEnabled && IsConnectedToPlc`
- `MapSyncStatus`: `PlcSyncStatus.OutOfSync => string.Empty` (enum value unchanged).

## What Goes Where
- **Implementation Steps** (`[ ]`): all code, AXAML, and unit tests in this repo.
- **Post-Completion** (no checkboxes): runtime/visual verification via `/run`.

## Implementation Steps

### Task 1: Add the `LocalMode` (grey) config color through the palette chain

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleChromeColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleDtoMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`

- [x] `GridStyleOptions`: add positional param `string LocalModeColor` next to `DisconnectedColor`;
      set `LocalModeColor: "#6C707E"` in the `Default` instance.
- [x] `GridStyleChromeColorsDto`: add `[YamlMember(Alias = "local_mode", ScalarStyle = DoubleQuoted)] string? LocalMode`.
- [x] `GridStyleMapper`: `LocalModeColor: dto.Chrome?.LocalMode ?? defaults.LocalModeColor`.
- [x] `GridStyleDtoMapper`: `LocalMode = options.LocalModeColor` in the `Chrome` block.
- [x] `GridStyleValidator`: add `("local_mode", chrome.LocalMode)` to the chrome color tuple list.
- [x] `CellPaletteInstaller`: add `LocalModeBrushKey = "LocalModeBrush"` + install
      `resources[LocalModeBrushKey] = PaletteBrushFactory.From(gridStyle.LocalModeColor);`.
- [x] Fix every positional `GridStyleOptions(...)` construction broken by the new param (grep;
      notably test builders). Named-arg call sites just add the arg.
- [x] Update `CellPaletteInstallerTests`: assert `LocalModeBrush` installed from `LocalModeColor`, and
      bump the hard count assertion `resources.Count.Should().Be(56)` → `57`.
- [x] Update `GridStyleMapperTests`: `local_mode` maps through, and falls back to default when absent.
- [x] Run tests — must pass before Task 2.

### Task 2: Expose the color in the Grid Style editor

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml`

- [x] Add `Color LocalMode` property; wire `LocalModeColor = HexColor.ToHex(LocalMode)` in `BuildRecord()`
      and `LocalMode = HexColor.Parse(options.LocalModeColor)` in `Seed()`. (`BuildRecord` uses
      `_source with { ... }`, so `LocalModeColor` round-trips losslessly even before the picker is wired.)
- [x] Add a "Local mode" color-picker row next to Connected/Disconnected in the editor AXAML.
- [x] Extend any existing GridStyleEditor round-trip test to cover `LocalMode` (if such a test exists;
      otherwise rely on the mapper tests from Task 1).
- [x] Run tests — must pass before Task 3.

### Task 3: VM state booleans and sync-status text

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelSyncStateTests.cs` (matches existing
  `MainWindowViewModel*Tests` split: `MainWindowViewModelReportingTests`, `...ToolBarToggleTests`)

- [x] Add `IsSyncLocalMode`, `IsSyncNoLink`, `IsSyncLinked` derived properties.
- [x] Raise all three in `RaiseConnectionStateProperties()`.
- [x] `MapSyncStatus`: change `OutOfSync => "Out of sync"` to `=> string.Empty` (enum value unchanged;
      `Syncing.../Synced/Failed/Idle` mappings stay — the secondary label still renders them).
- [x] ⚠️ Remove the `ConnectionStatus` property **and** its `RaisePropertyChanged(nameof(ConnectionStatus))`
      line in `RaiseConnectionStateProperties()` (MainWindowViewModel.cs:245) — else `nameof` won't
      compile. Verified no test references `ConnectionStatus`.
      **Deferred to Task 4**: the plan assumed `{Binding ConnectionStatus}` in `AppStatusBar.axaml`
      resolves only at runtime. It does not — Avalonia's XAML compiler resolves it at build time
      (`AVLN2000`), so removing the property fails the UI build while the AXAML binding still exists.
      The property (and its `RaisePropertyChanged` line) must be removed together with the AXAML
      binding in Task 4, not before.
- [x] Write tests: truth table for the three booleans across all four (enabled × connected) combos.
- [x] Write test: `PlcSyncStatusText` returns `""` for `OutOfSync`, and still renders
      `Syncing.../Synced/Failed` for those statuses.
- [x] Run tests — must pass before Task 4.

### Task 4: Replace the status-bar elements with the merged toggle button

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`

- [x] Add the `Button.sync-toggle` style block (base + three state classes + hover/pressed presenter
      overrides + press scrim) to `UserControl.Styles`.
- [x] Replace the col-2 sync `Button` (92-103) with the single class-bound toggle button.
- [x] **Keep** the secondary `PlcSyncStatusText` label (106-108) — it shows `Syncing.../Synced/Failed`,
      which the button color does not encode (issue #65). `OutOfSync` now renders as empty via the VM.
- [x] Keep the `Last sync` text (110-113).
- [x] Delete the right-hand connection indicator: the col-8 `StackPanel` (193-206) plus its col-8
      `ColumnDefinition`; the col-7 `*` spacer (191) can also go since nothing is right-aligned anymore.
- [x] Delete the `connection-dot` styles (10-18).
- [x] Build the UI project — must compile before Task 5.

### Task 5: Verify acceptance criteria
- [x] Verify each issue task box is satisfied (merged button, VM state mapping, grey brush, prominent
      styling, secondary text handling, `Last sync` kept).
- [x] Run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- [x] `dotnet format SemiStep/SemiStep.slnx --verify-no-changes` on changed files.

### Task 6: [Final] Docs and cleanup
- [x] Update `Docs/architecture/grid-style-configuration.md` with the new `local_mode` chrome color.

## Post-Completion
*Runtime/visual checks — no headless assertion exists for these; verify via `/run`.*

**Finalization (manual, after review):**
- Move this plan to `Docs/plans/completed/` after the branch is merged.
- Push branch, open PR referencing #65, merge, and fast-forward `master` — gated on user confirmation.

**Manual verification:**
- State color holds on hover and press for all three classes (no revert to Semi grey).
- Button not clipped by bar height at 100% / 125% / 150% scaling.
- White text legible on the green fill; width does not jump between `Sync ON` / `Sync OFF`.
- Grey `#6C707E` reads as an active-but-off button against the `#F7F8FA` bar across the room.

**Optional follow-up (not in scope):**
- If exact darkened press tint is wanted instead of the translucent scrim, add parity pressed brushes
  (`LocalModePressedBrush` / `DisconnectedPressedBrush` / `ConnectedPressedBrush`) through the same
  `CellPaletteInstaller` path.
- Russian labels (feedback point 8) are handled separately.
