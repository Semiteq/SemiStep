# YAML-Backed In-App Style Settings Editor

## Overview

Give the operator an in-app window to edit recipe-grid styles, persisted to the existing
`grid_style.yaml`. YAML stays the single source of truth; the UI becomes the editor instead of
hand-editing the file. Restart applies changes (live preview is optional, deferred). The end state
is **no hardcoded styles** — every style flows from `GridStyleOptions`.

Researched via a multi-agent workflow (Avalonia editor mechanics, YamlDotNet round-trip, repo audit,
other-project patterns). This plan is the actionable distillation; the full design write-up is the
workflow synthesis.

## Architecture decision

```
grid_style.yaml        →  GridStyleOptions          →  Application.Resources
(file / source of       (typed bridge, SemiStep.Core,  (live projection: brushes
 truth, per-equipment)   Avalonia-free, read as          via {DynamicResource})
                         numbers by the calculator)
```

**Layering decision (settled by adversarial review): the grid-style config STAYS in `SemiStep.Core`.**
Core owns the per-equipment config bundle (precedent: `ComboBoxItemViewModel`, `GridColumnDefinition`,
`ColumnUiDto`, status-bar/validation DTOs already live there); Core is Avalonia-free either way, so
there is no layering leak to fix. Moving it would break 14 `AppConfiguration` arity sites + rewrite the
`ConfigFacadeGridStyleValidationTests` class for a marginal nicety the editor does not even need.
Rejected — the move, if ever wanted, is its own PR decoupled from the editor.

- **YAML** is the file/source. `GridStyleLoader.LoadAsync` → `GridStyleOptionsDto` → `GridStyleValidator`
  → `GridStyleMapper.Map` → `GridStyleOptions` (immutable record, DI singleton via `UiDi.AddUi`), loaded
  by `ConfigFacade` into `AppConfiguration` as today.
- **Resources** are the runtime projection (`CellPaletteInstaller`/`ExecutionPaletteInstaller` push
  `SolidColorBrush` objects; grid consumes via `{DynamicResource}`).
- **The editor (UI)** reaches the Core config through one thin public `GridStyleEditorFacade`
  (`Load(configDir)` / `Validate(options) → Result` / `Save(configDir, options) → Result`) over the
  already-public `GridStyleOptions` record. The 12 DTOs stay `internal`. The editor edits the record
  (merged space — defaults applied), so `Save` writes a fully-populated file; that is acceptable for a
  one-file settings editor and simplifies write-back. **Restart re-runs the normal pipeline.**
- The typed model cannot be replaced by raw resources: `ColumnWidthCalculator.MeasureText` needs
  `CellFontSize` as a number in code.

## Config layout (folder-based, but styles are one file)

The config is a **folder** per equipment. `actions/`, `columns/`, `groups/`, `properties/` are loaded
by globbing `*.yaml` in each subfolder (`Directory.GetFiles(dir, "*.yaml")`) and merging. **Grid styles
are the exception:** `GridStyleLoader.LoadAsync` reads exactly one file, `{configDir}/ui/grid_style.yaml`
— not globbed, not merged. So the editor's write target is that single file, derived from the active
`configDir`; the folder-glob loading of sibling sections does not affect the style editor. (If styles
were ever split across `ui/*.yaml`, write-back would become ambiguous — keeping them single-file is the
right call for an editor.)

## Decisions (locked)

1. **Per-equipment target — single launched config.** The editor writes only the launched
   `--config-dir`'s `ui/grid_style.yaml` (default `C:\DISTR\Config\Semistep\MBE`). No in-app equipment
   switch in v1.
2. **Restart-to-apply for v1.** Live preview deferred (final optional task, color-only when added).
3. **YAML output — machine format.** Top header comment re-prepended; no inline comments (none exist);
   hex emitted double-quoted via per-property `ScalarStyle.DoubleQuoted`.
4. **Wire the dead/marginal fields that have a real target** (padding, row height, status bar
   bg/fg/padding/spacing, validation panel bg/fg/error/warning/max-height).
   - `ValidationPanelMaxHeight`: **YAML is the source of truth** — adopt the YAML value, drop the
     hardcoded `200`/`160` in `MessagePanel.axaml`.
   - `ValidationPanelErrorColor` / `ValidationPanelWarningColor` are the **app-wide severity colors**:
     one field per severity drives both the message panel and the status-bar error/warning dots (the
     shared `ErrorBrush` / `WarningBrush`). Two surfaces, one field each — accepted; no separate
     status-bar severity fields in v1.
   - `ExecutionDepth0Color` / `ExecRowDepth0Brush`: **stay installed but intentionally UNWIRED.** There
     is no `for-depth-0` class (`RecipeRowExecutionClassBinder` binds only `current-step`/`past-step`/
     `for-depth-1/2/3`; `RecipeRowViewModel` has no `IsForDepth0`), and depth-0 already renders default
     white (`#FFFFFF`), so a `for-depth-0` selector would be a no-op requiring invented machinery. Keep
     the brush installed for palette symmetry and its existing installer comment; **do not add a
     selector and do not remove the brush.**
   - `GridLineThickness`, `AlternatingRowBackgroundColor`, `NormalRowBackgroundColor`: **REMOVE** — no
     Avalonia wire target. Avalonia's Fluent `DataGrid` exposes neither an inner-gridline-thickness nor
     an alternating-row-background property; all three are referenced only by `GridStyleMapper` + the
     record, never in any `.axaml`. Drop from `GridStyleOptions` + DTO + mapper + the 3 shipped configs.
5. **App-chrome colors — make tunable.** Add operator-tunable `GridStyleOptions` fields + YAML keys +
   installer brushes for `Info`, `Connected`, `Disconnected`, `Panel*`, `SubtleBorder`, `Separator`,
   `SecondaryForeground` (currently fixed in `ColorPalette.axaml`). Expands the model, the configs, and
   the editor surface. Also wire `MainWindow` `BorderBrush` (was `"Gray"`), `Background` (was `"White"`),
   and header foreground — resolving the "no hardcoded styles" goal.
6. **Size/thickness validation** lives in the editor VM (range checks); the Core `GridStyleValidator`
   stays colors-only.

## Review corrections (folded in — from auto-review + sanity subs)

Verdict was **NEEDS REVISION**; all factual claims (Avalonia API + repo grounding + YamlDotNet)
confirmed. Structural fixes applied to the task breakdown below:

- **Split the consume-vs-model-shape work into three isolated commits.** Exec produces one branch, not
  three PRs; the boundaries below are Task boundaries kept for the same isolation rationale. The consume
  step must NOT change the `GridStyleOptions` record shape (any field add/drop breaks `Default` + every
  call site at once):
  - **Task 1 — consume existing dead fields** (no record change): padding, row height, status bar,
    validation panel; delete the duplicate `StaticResource` brushes (`Error`/`Warning`/
    `StatusBarBackground`) and source them from the **existing** installer fields. `ExecutionDepth0Color`
    stays installed-but-unwired (Decisions #4). Screenshot-verifiable on its own.
  - **Task 2 — remove fields with no wire target** (pure subtraction): `GridLineThickness`,
    `AlternatingRowBackgroundColor`, `NormalRowBackgroundColor` from model + `GridStyleBordersDto` +
    `GridStyleRowColorsDto` + mapper + the 3 shipped `ConfigFiles/*/ui/grid_style.yaml`. No
    `SemiStep.Tests/YamlConfigs` fixture carries these keys (verified) — nothing to scrub there.
  - **Task 3 — add app-chrome fields** (additive model expansion) + `StaticResource`→installer for them.
- **Core visibility gap → one thin facade.** Add a single public `GridStyleEditorFacade` in
  `SemiStep.Core` exposing `Load(configDir)` / `Validate(GridStyleOptions) → Result` /
  `Save(configDir, GridStyleOptions) → Result` over the already-public record; keep the 12 DTOs and the
  loader/validator/writer `internal`. The editor never sees Core internals directly.
- **Write-back maps the edited record → DTO and writes a fully-populated file** (the editor edits the
  merged record, so every key is emitted — accepted per the layering verdict; no omitted-key
  preservation, which also removes the "edit the loaded DTO in place" gymnastics).
- **Round-trip test = semantic equality** (re-load yields an equal record), not byte-identity; test
  against the real shipped MBE/MOCVD/RIE files, not only synthetic ones.
- **Byte-identical-on-Cancel** requires deliberate encoding/newline handling: standardize LF + UTF-8
  no-BOM on write; assert invariant-culture decimals (`6.5`, not `6,5`) — target is a Windows deploy of
  unknown locale; preserve the header's trailing blank line.
- **`ScalarStyle.DoubleQuoted`** must go on **every** color `string?` DTO prop (~50) — miss one → mixed
  quoting.
- **Border/header-foreground ambiguity is resolved to WIRE** (end state is "no hardcoded styles"):
  `MainWindow` `BorderBrush`/`Background`/header foreground move to installer-driven chrome fields in
  Task 3.
- **Harness is headless:** font/render claims are gated by the manual Win32 pass (Post-Completion), not
  by harness evidence alone.

## Development Approach

- Each Task is an isolated, independently verifiable commit; exec runs them on one branch.
- TDD where a unit boundary exists (the YAML writer); UI verified via the local screenshot harness.
- Every task ends with tests/verification; all green before the next.
- `dotnet format` clean; comments English, only where non-obvious.

## Testing Strategy

- **Unit:** `GridStyleWriter` round-trip (load → serialize → re-load identity; header preserved;
  `OmitNull` drops untouched optionals; hex round-trips); mapper/installer tests extended per new field.
- **UI / visual:** the local screenshot harness (`SemiStep.Screenshots`, gitignored) renders the grid
  after hand-editing YAML (Task 1) and after editor Save + restart (Task 7).
- Full suites: `Component=Config`, `Component=UI` green; `dotnet format --verify-no-changes` clean.

## Implementation Steps

### Task 1: (wiring) Consume existing dead style fields — no record-shape change

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` (3 hardcoded `Thickness(4, 2)` sites)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs` (`RowHeight` in `BuildColumns`)
- Modify: `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml` (font / padding / spacing / colors)
- Modify: `SemiStep/SemiStep.UI/MessageService/MessagePanel.axaml` (`ValidationPanel*`; drop `MaxHeight` 200/160)
- Modify: `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs` (install StatusBar/Error/Warning/Separator brushes)
- Modify: `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml` (delete the now-installer-driven static brushes)
- Modify: `SemiStep/SemiStep.Tests/UI/Styles/CellPaletteInstallerTests.cs`, mapper/validator tests as needed

- [x] Wire `CellPadding*` in `TextCellFactory`: replace `new Thickness(4, 2)` at the 3 template sites with
      `new Thickness(gridStyle.CellPaddingLeft, gridStyle.CellPaddingTop, gridStyle.CellPaddingRight, gridStyle.CellPaddingBottom)`
- [x] Set `RowHeight` on the DataGrid: `grid.RowHeight = gridStyle.RowHeight;` in `ColumnBuilder.BuildColumns`
      (where `gridStyle` is already a primary-ctor field)
- [x] Wire `AppStatusBar` font (`CellFontSize`), padding (`StatusBarPadding`), item spacing
      (`StatusBarItemSpacing`), and bg/fg (`StatusBar*`) to config; install + bind a `StatusBarForegroundBrush`
      (numeric layout props installed as `StatusBarFontSize`/`StatusBarPadding`/`StatusBarItemSpacing` resources,
      bound via `{DynamicResource}`; fg applied via `TextElement.Foreground` on the border)
- [x] Wire `ValidationPanel*` in `MessagePanel` (bg/fg via new installer brushes); adopt the YAML
      `max-height` and delete the hardcoded `MaxHeight="200"` and `MaxHeight="160"`
- [x] Install `StatusBarBackgroundBrush`, `StatusBarForegroundBrush`, `ErrorBrush`, `WarningBrush`
      from existing `GridStyleOptions` fields; delete the matching `StaticResource`
      brushes in `ColorPalette.axaml` so config drives them. `ValidationPanel{Error,Warning}Color` is the
      app-wide severity color (drives both status bar and message panel). `SeparatorBrush` kept as a
      static — no backing `GridStyleOptions` field (per Decisions #4 / ground-truth)
- [x] Leave `ExecutionDepth0Color` / `ExecRowDepth0Brush` installed-but-unwired — no `for-depth-0` class
      exists; do NOT add a selector, do NOT remove the brush; keep the installer comment
- [x] Update `CellPaletteInstallerTests` (resource count 25 → 35 + new keys) and mapper/validator tests
      (mapper/validator untouched — not needed for Task 1)
- [x] Screenshot-harness: hand-edit YAML → restart → padding / row-height / status-bar / validation change
      (screenshot-harness check — local/manual, not automated here)
- [x] `Component=Config` + `Component=UI` green; `dotnet format` clean

### Task 2: (model) Remove fields with no Avalonia wire target

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` (record + `Default`)
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/GridStyleBordersDto.cs`, `Dto/GridStyleRowColorsDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/GridStyleMapper.cs`
- Modify: `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml`
- Modify: mapper tests referencing the removed fields

- [x] Remove `GridLineThickness` (no DataGrid inner-gridline-thickness property)
- [x] Remove `AlternatingRowBackgroundColor` + `NormalRowBackgroundColor` (no alternating-row-background
      property; referenced only by mapper + record, never in axaml)
- [x] Drop the keys (`borders.grid_line_thickness`, `rows.alternating_background`, `rows.normal_background`)
      from the 3 shipped configs (verified: no `SemiStep.Tests/YamlConfigs` fixture carries them)
- [x] Update `GridStyleMapper` + any mapper tests for the dropped fields
- [x] Suites green; `dotnet format` clean

### Task 3: (model) Add app-chrome fields (additive model expansion)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` (+ DTO + mapper + validator)
- Modify: `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml`
- Modify: `SemiStep/SemiStep.UI/Styles/ColorPalette.axaml`, `CellPaletteInstaller.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` (border / background / header foreground)

- [x] Add `GridStyleOptions` fields + YAML keys + installer brushes for `Info`, `Connected`,
      `Disconnected`, `Panel*`, `SubtleBorder`, `Separator`, `SecondaryForeground`
- [x] Repoint `ColorPalette.axaml` `StaticResource` consumers to installer-driven brushes; wire
      `MainWindow` `BorderBrush` (was `"Gray"`), `Background` (was `"White"`), header foreground
- [x] Mapper/installer/validator tests for new fields; screenshot-harness: chrome colors change on restart
      (screenshot-harness check — local/manual, not automated here)
- [x] Suites green; `dotnet format` clean

### Task 4: (write-back) GridStyleWriter + reverse mapper + DoubleQuoted scalar style

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Loaders/GridStyleWriter.cs` (`internal`, beside the loader)
- Create: a `GridStyleOptions → GridStyleOptionsDto` reverse mapper for Save
- Modify: DTO color props (`[YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]` on every color prop)
- Create: `SemiStep/SemiStep.Tests/Configuration/GridStyleWriterTests.cs`

- [x] `Save`: map the edited record → DTO → `SerializerBuilder().WithNamingConvention(Underscored)`
      serialize; re-prepend the file's leading comment block; LF/UTF-8 no-BOM; atomic temp-then-move
      (temp in the same `ui/` dir); return `Result`
- [x] `ScalarStyle.DoubleQuoted` on **every** color `string?` DTO prop (~50) — miss one → mixed quoting
- [x] Tests: semantic round-trip against the real MBE/MOCVD/RIE files (re-load yields an equal record);
      header preserved; `Color.TryParse(ToHex(c)) == c`; invariant-culture decimals (`6.5` not `6,5`)
- [x] Suites green; `dotnet format` clean

### Task 5: (write-back) GridStyleEditorFacade (public seam) + config-dir DI

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/GridStyleEditorFacade.cs` (the one **public** editor seam)
- Modify: `SemiStep/SemiStep.UI/Program.cs` / `UiDi.cs` (register `StartupOptions` / resolved config-dir)
- Create: facade tests

- [x] Facade `Load(configDir)` / `Validate(GridStyleOptions) → Result` / `Save(configDir, options) → Result`
      over the public record; loader/validator/writer/DTOs stay `internal`
- [x] Register the resolved config-dir / `StartupOptions` in DI so the editor knows the write path
- [x] Facade tests; suites green; `dotnet format` clean

### Task 6: (editor) GridStyleEditorViewModel

**Files:**
- Create: `SemiStep/SemiStep.UI/.../GridStyleEditorViewModel.cs`
- Create: VM unit tests

- [x] Mutable draft seeded from the loaded record (not the singleton); `Color` props (hex↔`Color` via
      manual `ToHex`, not `Color.ToString()`); numeric props (`decimal?` for `NumericUpDown`); `CanSave`
      gated by validation + range checks (size/thickness checks live in the VM; Core validator stays colors-only)
- [x] VM unit tests (range checks, hex round-trip, `CanSave` gating); suites green

### Task 7: (editor) GridStyleEditorWindow + open command + ColorPicker package

**Files:**
- Create: `GridStyleEditorWindow.axaml(.cs)`
- Modify: `App.axaml` (ColorPicker Fluent theme include), `.csproj` + `Directory.Packages.props`
  (`Avalonia.Controls.ColorPicker` 12.0.x), a menu/command to open it

- [x] Add `Avalonia.Controls.ColorPicker` package + theme include
- [x] Window: `ColorPicker` (`IsHexInputVisible`) per color, `NumericUpDown` per size; Save → validate →
      `GridStyleEditorFacade.Save` → prompt restart; Cancel → discard, file byte-identical
- [x] Open from a menu/command
- [x] (screenshot-harness — local/manual, not automated here) open window, change a color + a font size,
      Save; assert YAML changed and a restart reflects it; Cancel leaves the file byte-identical
- [x] Suites green; `dotnet format` clean

### Task 8 (optional): (preview) Live color preview via `IGridStyleProvider`

**Files:**
- Create: `IGridStyleProvider` + impl; Modify: `UiDi.cs` (replace singleton), installers subscribe to
  `Changed`, editor gains Apply

- [x] Provider holds `Current` + `Changed`; Apply swaps it and re-pushes color brushes (live via
      `DynamicResource`); font/size still restart-gated (they feed code-side measurement/layout) (deferred — optional live preview, out of v1 per the restart-to-apply decision; revisit if hot-reload is wanted)
- [x] Screenshot-harness: Apply changes a color without restart; font change still prompts restart (deferred — optional live preview, out of v1 per the restart-to-apply decision; revisit if hot-reload is wanted)

### Task 9: Verify & document

- [x] All open decisions resolved and reflected
- [x] Architecture doc created: `Docs/architecture/grid-style-configuration.md` records: YAML source,
      resources projection, editor write-back, restart-to-apply, the calculator's number constraint
- [x] Move this plan to `Docs/plans/completed/` (move deferred until after exec review/finalize phases)

## Post-Completion

- Manual: operator edits each style category in the window, saves, restarts, confirms on the real
  Win32 app (the harness is headless — confirm fonts/render on the real app per prior lessons).
- Future: live preview for size/font (needs a `ColumnBuilder.BuildColumns` rebuild trigger);
  per-equipment switch in-app.

## Risks

- **Comment loss** on reserialize (header preserved only). Accept machine-formatted YAML.
- **Harness ≠ real Win32** for fonts/render — verify the editor's effects on the real app, not only
  the harness (per repeated lessons this session).
- **Live preview is partial** by nature: colors free via `DynamicResource`, but font/padding/row-height
  feed code-side layout and need a rebuild — hence restart-to-apply for v1.
- **`GridStyleValidator` is colors-only** — size/range validation must be added (VM-side recommended).
