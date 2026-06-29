# Notification Log: Close Button

## Overview
- Add a close ("X") button to the Notification log panel header so the user can hide the
  panel directly, without going through the `View` menu or clicking the status-bar counts.
- Implements the primary task of issue #73. The optional `GridSplitter` resize is explicitly
  out of scope for this plan (deferred as a possible follow-up).
- The panel already exposes `MessagePanelViewModel.ToggleCommand`, which flips `IsVisible`.
  Since `ShowPanel = HasEntries && IsVisible`, invoking `ToggleCommand` while the panel is
  visible hides it — exactly the "close" behavior. No new command or VM state is needed.

## Context (from discovery)
- `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml` — header is a
  `Grid ColumnDefinitions="*,Auto"`; the right `Auto` column (`Grid.Column="1"`) is empty.
  That is the slot for the close button.
- The panel's `DataContext` is the `MessagePanelViewModel` itself (set in
  `MainWindow.axaml:68`), so a binding to `ToggleCommand` from inside `MessagePanel.axaml`
  is direct (no parent-relative binding required).
- `MessagePanelViewModel.ToggleCommand` (`MessagePanelViewModel.cs:39`) flips `IsVisible`.
  `ShowPanel` (`:81-84`) is `HasEntries && IsVisible`. `MainWindow.axaml:66-69` binds the
  panel's `IsVisible` to `ShowPanel`.
- The menu toggle (`RecipeMenuBar.axaml:59-60`) and the status-bar counts button
  (`AppStatusBar.axaml:41-74`) both bind to the same `ToggleCommand`, so they continue to
  bring the panel back after it is closed.
- Test patterns: `SemiStep/SemiStep.Tests/UI/MainWindow/RecipeToolBarTests.cs` shows the
  control-rendering harness — host the `UserControl` in a `Window`, `Show()`, `RunJobs()`,
  then `FindControl<Button>(name)` and assert `Command.Should().BeSameAs(...)`. VM-level
  behavior is covered by `SemiStep.Tests/UI/MessagePanelViewModelTests.cs`.

## Development Approach
- Testing approach: **Regular** (code first, then tests).
- Complete the task fully before moving on; tests are a required deliverable of the task.
- All tests must pass before the verification task. Run `dotnet format` before finishing.
- Labels and any visible text stay in their current language (russification is issue #69).

## Testing Strategy
- **Unit tests**: a new control test verifies the close button exists, binds to
  `ToggleCommand`, and hides the panel when executed; plus a re-open check at VM level.
- No e2e harness exists in this project; headless `[AvaloniaFact]` tests are the standard.

## Solution Overview
- Add one chromeless `Button` (`x:Name="CloseButton"`) into the header's empty `Auto`
  column, right-aligned, content a "✕" glyph, `Command="{Binding ToggleCommand}"`,
  `ToolTip.Tip="Close"`. Keep the transparent/borderless chrome inlined on the button,
  matching the single-button convention of `AppStatusBar.axaml:44-48`.
- No changes to `MessagePanelViewModel`, `MainWindow.axaml`, `RecipeMenuBar.axaml`, or
  `AppStatusBar.axaml` — the existing `ToggleCommand` wiring already covers close and re-open.

## Technical Details
- `MessagePanel.axaml` header `Grid` keeps `ColumnDefinitions="*,Auto"`; the counts
  `StackPanel` stays in `Grid.Column="0"`. The new button goes to `Grid.Column="1"` with
  `HorizontalAlignment="Right"` / `VerticalAlignment="Center"`.
- Inline the chrome on the button: `Background="Transparent" BorderThickness="0"` with a
  compact `Padding` (mirrors `AppStatusBar.axaml`). The glyph is a `TextBlock` ("✕") so no
  asset is needed.

## What Goes Where
- **Implementation Steps** (`[ ]`): XAML edit + tests + verification.
- **Post-Completion** (no checkboxes): manual visual smoke test of the close button and the
  menu / status-bar re-open paths.

## Implementation Steps

### Task 1: Add close button to the Notification log header

**Files:**
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml`
- Create: `SemiStep/SemiStep.Tests/UI/MessagePanelCloseButtonTests.cs`

- [x] Add `<Button x:Name="CloseButton" Grid.Column="1" Command="{Binding ToggleCommand}"
      ToolTip.Tip="Close" Background="Transparent" BorderThickness="0" Padding="6,2"
      HorizontalAlignment="Right" VerticalAlignment="Center">` with a "✕" `TextBlock`
      content into the header's empty `Auto` column (chrome inlined, per `AppStatusBar`).
- [x] Create `MessagePanelCloseButtonTests.cs` (`[Trait]` Component=UI, Area=MessagePanel,
      Category=Unit) following the `RecipeToolBarTests` host-in-Window pattern.
- [x] Test: `CloseButton` exists and its `Command` `BeSameAs` `viewModel.ToggleCommand`.
- [x] Test: seed entries with `viewModel.RefreshReasons([new Error("e")])` so `HasEntries`
      is true (the setter is private — see `MessagePanelViewModelTests.cs:62-72`); with
      `IsVisible` true (`ShowPanel` true), executing `ToggleCommand` sets `ShowPanel` to
      false; executing again restores it to true (close + re-open, success and return paths).
- [x] Run tests — must pass before the verification task.

### Task 2: Verify acceptance criteria

- [x] Confirm `MessagePanelViewModel`, `MainWindow.axaml`, `RecipeMenuBar.axaml`,
      `AppStatusBar.axaml` are unchanged (close reuses the existing `ToggleCommand`).
- [x] Run the UI test suite:
      `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"`.
- [x] Run `dotnet format SemiStep/SemiStep.slnx` and confirm no diff remains.

### Task 3: Finalize

- [x] Move this plan to `Docs/plans/completed/`.

## Post-Completion
*Items requiring manual intervention — no checkboxes, informational only.*

**Manual verification:**
- Launch the app, trigger a warning/error so the panel appears, click the "✕" — panel hides.
- Re-open via `View -> Notification Log` and via the status-bar counts — panel returns.
