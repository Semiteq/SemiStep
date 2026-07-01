# PLC "connecting" indicator on the sync toggle button

## Overview
While a PLC connect attempt is in flight (cable unplugged / no link), the status-bar sync toggle
currently sits on a static "no link" red for ~10s with no sign that anything is happening, then
silently reverts to grey "Sync OFF". Add a distinct, restrained **"Connecting"** state to the button:
an amber fill, the label "Connecting" with a subtle phased three-dot animation, shown for the duration
of the in-flight attempt. This distinguishes "actively trying" from a settled "no link".

Scope is **indicator only** (explicit user decision). The post-failure behavior is unchanged: after the
~10s the connect throws, sync is disabled, and the button returns to grey "Sync OFF". Shortening the
~10s OS/TCP connect timeout and any persistent-retry/Failed behavior are explicitly out of scope.

Follow-up to #65 (merged). This is a separate logical change → separate PR.

## Context (from discovery)
- **`PlcConnectionState.Connecting` already exists** (`SemiStep.Core/Plc/State/PlcConnectionState.cs`) and
  is set at `S7Service.cs:179` at the start of every connect attempt. It is packed into `PlcSessionSnapshot`
  and already flows to the VM via `RecipeCoordinator.PlcStateChanged`. No new Core event/plumbing is needed.
- The tri-state is **flattened to a bool** at the VM boundary: `RecipeCoordinator` exposes only
  `IsConnected` (`RecipeCoordinator.cs:104`), and the VM's `IsSyncNoLink = IsSyncEnabled && !IsConnectedToPlc`
  (`MainWindowViewModel.cs:132`) is true for BOTH `Connecting` and `Disconnected`. That is why "connecting"
  is invisible today.
- **`RecipeCoordinator.OnPlcStateChanged(PlcSessionSnapshot snapshot)`** (`RecipeCoordinator.cs:491`) already
  receives every snapshot and can cache `snapshot.ConnectionState`.
- **Palette is config-driven** (not `ThemeDictionaries`; app is Light-only). Status colors thread through
  `GridStyleOptions` → `GridStyleChromeColorsDto` → `GridStyleMapper`/`GridStyleDtoMapper` →
  `GridStyleValidator` → `CellPaletteInstaller` → Grid Style editor. `ConnectingColor` mirrors
  `LocalModeColor` exactly (added in #65).
- **Button** (`AppStatusBar.axaml`, from #65): `Button.sync-toggle` base + `.sync-local`/`.sync-nolink`/
  `.sync-linked` state classes with `:pointerover`/`:pressed` presenter overrides; content is a `Panel`
  with "Sync ON"/"Sync OFF" TextBlocks; `MinWidth=76`.
- **Avalonia 12.0.3** supports declarative `Style.Animations` KeyFrames; `Animation.Delay` is a one-shot
  pre-first-iteration delay, so on an infinite equal-duration animation it yields a permanent phase offset
  — the mechanism behind the dot "wave" with no timer.

## Development Approach
- **Testing approach**: Regular (code first, then tests). xUnit.
- The dot animation and its start/stop, amber legibility, and fixed-width sizing are verified at runtime
  via `/run` (no headless assertion for animation) — listed under Post-Completion.
- All tests pass before the next task. One PR = one logical change. Branch `connecting-indicator` off
  fresh `origin/master`.

## Testing Strategy
- **Unit**: VM state derivations (four mutually-exclusive states incl. connecting; the two text-gate bools);
  `RecipeCoordinator.IsConnecting` reflects the snapshot; `ConnectingBrush` install; `connecting` chrome
  color map-through / default fallback / reverse serialization; editor round-trip.
- **Runtime `/run`**: dot animation subtlety + stop-on-exit, amber+white legibility, fixed width at
  100/125/150% scaling.

## Solution Overview
`RecipeCoordinator` surfaces the connection tri-state (`IsConnecting`) instead of only `IsConnected`
(the snapshot already carries it). The VM adds `IsSyncConnecting` and tightens the state booleans so the
four button states — local (grey), connecting (amber), nolink (red, settled), linked (green) — are mutually
exclusive; plus two text-gate bools so the ON/OFF labels hide during connecting. The button gains a
`.sync-connecting` class → amber `ConnectingBrush` fill and a "Connecting" label followed by three
opacity-phased dots animated declaratively. Button width is fixed so entering the state does not reflow the
bar. The amber color joins the config-driven palette exactly like the other status colors.

## Technical Details

### State derivations (MainWindowViewModel)
Given `IsConnecting` (new, from coordinator) and existing `IsConnectedToPlc`/`IsSyncEnabled`; at rest
`IsConnecting` is false, and `IsConnecting`/`IsConnectedToPlc` are never both true:
- `IsSyncLocalMode  => !IsSyncEnabled`
- `IsSyncConnecting => IsSyncEnabled && _coordinator.IsConnecting`
- `IsSyncLinked     => IsSyncEnabled && IsConnectedToPlc`
- `IsSyncNoLink     => IsSyncEnabled && !IsConnectedToPlc && !_coordinator.IsConnecting`
- `IsSyncOnIdle     => IsSyncEnabled && !IsSyncConnecting`   (drives "Sync ON")
"Sync OFF" is driven by the existing `IsSyncLocalMode` (`=> !IsSyncEnabled`) — no separate `IsSyncOffIdle`
property (it would be identical to `IsSyncLocalMode`). All new/changed properties raised in
`RaiseConnectionStateProperties()`.

### ConnectingColor default
`#FFAF0F` (JetBrains warning amber), added to `GridStyleOptions.Default` next to `LocalModeColor`.

### AXAML — styles (add to UserControl.Styles, after the existing state classes)
```xml
<Style Selector="Button.sync-toggle.sync-connecting">
    <Setter Property="Background" Value="{DynamicResource ConnectingBrush}" />
</Style>
<Style Selector="Button.sync-toggle.sync-connecting:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource ConnectingBrush}" />
</Style>

<Style Selector="Button.sync-toggle.sync-connecting TextBlock.sync-dot.d1">
    <Style.Animations>
        <Animation Duration="0:0:1.2" IterationCount="Infinite">
            <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
            <KeyFrame Cue="50%"><Setter Property="Opacity" Value="1.0" /></KeyFrame>
            <KeyFrame Cue="100%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
<Style Selector="Button.sync-toggle.sync-connecting TextBlock.sync-dot.d2">
    <Style.Animations>
        <Animation Duration="0:0:1.2" Delay="0:0:0.4" IterationCount="Infinite">
            <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
            <KeyFrame Cue="50%"><Setter Property="Opacity" Value="1.0" /></KeyFrame>
            <KeyFrame Cue="100%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
<Style Selector="Button.sync-toggle.sync-connecting TextBlock.sync-dot.d3">
    <Style.Animations>
        <Animation Duration="0:0:1.2" Delay="0:0:0.8" IterationCount="Infinite">
            <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
            <KeyFrame Cue="50%"><Setter Property="Opacity" Value="1.0" /></KeyFrame>
            <KeyFrame Cue="100%"><Setter Property="Opacity" Value="0.25" /></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```
The dot animations are gated by the descendant selector `…​.sync-connecting TextBlock.sync-dot…`; when the
class is removed the selector stops matching and Opacity returns to its base — no timer to stop.

### AXAML — button content + class + width
Add `Classes.sync-connecting="{Binding IsSyncConnecting}"`; gate ON/OFF by the idle bools; add the
connecting cluster; replace `MinWidth="76"` with a fixed `Width` (start at `108`, tune at runtime):
```xml
<Panel>
    <TextBlock Text="Sync ON"  IsVisible="{Binding IsSyncOnIdle}" />
    <TextBlock Text="Sync OFF" IsVisible="{Binding IsSyncLocalMode}" />
    <StackPanel Orientation="Horizontal" IsVisible="{Binding IsSyncConnecting}">
        <TextBlock Text="Connecting" />
        <TextBlock Classes="sync-dot d1" Text="." />
        <TextBlock Classes="sync-dot d2" Text="." />
        <TextBlock Classes="sync-dot d3" Text="." />
    </StackPanel>
</Panel>
```

## What Goes Where
- **Implementation Steps** (`[ ]`): code, AXAML, unit tests.
- **Post-Completion**: runtime `/run` visual checks.

## Implementation Steps

### Task 1: Surface the connection tri-state from RecipeCoordinator

**Files:**
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs`
- Create/Modify: `SemiStep/SemiStep.Tests/UI/RecipeCoordinator*Tests.cs` (match existing coordinator test file naming)

- [x] Cache `snapshot.ConnectionState` in `OnPlcStateChanged` (RecipeCoordinator.cs:491) and expose
      `public bool IsConnecting => <cached> == PlcConnectionState.Connecting;` next to `IsConnected`
      (RecipeCoordinator.cs:104). Keep `IsConnected` semantics unchanged.
- [x] CRITICAL ordering: assign the cache **before** `_plcStateChanged.OnNext(snapshot)` (RecipeCoordinator.cs:502).
      The VM subscriber (`MainWindowViewModel.cs:76`) discards the snapshot and re-reads `_coordinator.IsConnecting`
      synchronously inside `OnNext`; caching after the emit would make the VM read a stale value and lag the
      amber state by one snapshot.
- [x] Write a test: after an `OnPlcStateChanged` with `ConnectionState = Connecting`, `IsConnecting` is
      true and `IsConnected` false; after `Connected`, `IsConnecting` false / `IsConnected` true; after
      `Disconnected`, both false. Assert the cache-before-emit ordering (a subscriber to `PlcStateChanged`
      observes the already-updated `IsConnecting` synchronously). Drive it via the same seam the existing
      coordinator tests use.
- [x] Build + run tests — must pass before Task 2.

### Task 2: Add the `Connecting` (amber) config color through the palette chain

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleChromeColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleDtoMapper.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/GridStyleValidator.cs`
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`

- [x] `GridStyleOptions`: add `string ConnectingColor` after `LocalModeColor`; `ConnectingColor: "#FFAF0F"`
      in `Default`.
- [x] `GridStyleChromeColorsDto`: add `[YamlMember(Alias = "connecting", ScalarStyle = DoubleQuoted)] string? Connecting`.
- [x] `GridStyleMapper`: `ConnectingColor: dto.Chrome?.Connecting ?? defaults.ConnectingColor`.
- [x] `GridStyleDtoMapper`: `Connecting = options.ConnectingColor` in the Chrome block.
- [x] `GridStyleValidator`: add `("connecting", chrome.Connecting)` to the chrome tuple.
- [x] `CellPaletteInstaller`: add `ConnectingBrushKey = "ConnectingBrush"` + install from `gridStyle.ConnectingColor`.
- [x] Fix any positional `GridStyleOptions(...)` construction broken by the new param (named-arg sites just
      add the arg; the only positional site is `GridStyleMapper`).
- [x] Update `CellPaletteInstallerTests`: assert `ConnectingBrush` installed; bump count assertion `57` → `58`.
- [x] Update `GridStyleMapperTests`: `connecting` map-through, default fallback, and reverse (options→DTO)
      serialization (mirror the local_mode tests added in #65).
- [x] Build + run tests — must pass before Task 3.

### Task 3: Expose the color in the Grid Style editor

**Files:**
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml`
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`

- [x] Add `Color Connecting` property; wire `ConnectingColor = HexColor.ToHex(Connecting)` in `BuildRecord()`
      and `Connecting = HexColor.Parse(options.ConnectingColor)` in `Seed()` (mirror `LocalMode`).
- [x] Add a "Connecting" ColorPicker row next to the Local mode row in the editor AXAML (bump RowDefinitions).
- [x] Add editor round-trip tests for `Connecting` (mirror the `LocalMode` tests).
- [x] Build + run tests — must pass before Task 4.

### Task 4: VM connecting state + text-gate booleans

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/MainWindowViewModelSyncStateTests.cs`

- [x] Add `IsSyncConnecting` and `IsSyncOnIdle` (no `IsSyncOffIdle` — "Sync OFF" reuses `IsSyncLocalMode`);
      tighten `IsSyncNoLink` to `IsSyncEnabled && !IsConnectedToPlc && !_coordinator.IsConnecting` (see
      Technical Details).
- [x] Raise all new/changed properties in `RaiseConnectionStateProperties()`.
- [x] Extend the truth-table test to cover the connecting state (drive `StubS7Service` to report
      `Connecting`): assert exactly one of local/connecting/nolink/linked is true per case, and that the
      text-gate bools hide ON/OFF during connecting.
- [x] Build + run tests — must pass before Task 5.

### Task 5: Button connecting visuals in AppStatusBar.axaml

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`

- [x] Add the `.sync-connecting` fill + hover-override styles and the three `sync-dot d1/d2/d3` KeyFrame
      animations (copy from Technical Details).
- [x] Add `Classes.sync-connecting="{Binding IsSyncConnecting}"`; bind "Sync ON" to `IsSyncOnIdle` and
      "Sync OFF" to `IsSyncLocalMode` (removing the old `IsSyncEnabled` + `BoolConverters.Not` bindings so no
      stale binding remains); add the "Connecting" + three-dot cluster bound to `IsSyncConnecting`.
- [x] Change the `MinWidth="76"` Setter in the base `Button.sync-toggle` style (AppStatusBar.axaml:18) to a
      fixed `Width="108"` Setter (final value tuned at runtime; applies to all four states uniformly).
- [x] Build the UI project — must compile before Task 6.

### Task 6: Verify + docs
- [x] Verify the four button states are mutually exclusive and the connecting state renders; run full suite
      `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`; `dotnet format --verify-no-changes`.
- [x] Update `Docs/architecture/grid-style-configuration.md` with the `connecting` chrome color and the
      four-state (grey/amber/red/green) sync button mapping.
- [x] Update `Docs/readme.md` section 4.4 to mention the connecting state.

## Post-Completion
*Runtime `/run` checks — no headless assertion exists:*
- Dot animation reads as restrained (not blinking); amplitude 0.25↔1.0 / period 1.2s subjectively subtle
  (raise low bound to 0.4 if too strong).
- Animation stops cleanly on connecting → linked and connecting → nolink (no stuck translucent dot).
- Fixed `Width` fits "Connecting···" (SemiBold 13) at 100/125/150% without clipping or large right gap;
  finalize the px value.
- Amber `#FFAF0F` with white SemiBold text is legible.

**Finalization (manual, after review):** move this plan to `Docs/plans/completed/` after merge; push, PR,
merge — gated on user confirmation.
