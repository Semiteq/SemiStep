# Slice 3 — Nest GridStyleOptions to mirror the consumption groups

## Overview

Slice 3 of the #118 debloat, and the structural core: replace the flat, 78-arity `GridStyleOptions` record with a
one-level nested root composing ~10 per-group records. This is the big line-count win and the **only slice that ripples
the runtime consumers** — but every touch is a mechanical property-path change guarded by the compiler, and slice 1's
save→load + Seed/perturbation guards catch any dropped or cross-wired field.

**The YAML format, the DTOs, and `GridStyleValidator` do not change.** The file on disk is byte-identical before and
after; the DTO layer keeps its nested snake_case shape and its per-key error reporting. Only the *record* the DTO maps
to/from changes shape, plus everything that reads that record.

**Shape choices:**
- **One-level nesting** — ~10 group records directly on the root, matching how consumers read (short paths like
  `gridStyle.ReadOnlyCells.Depth1`); the load mapper owns the small DTO-tree walk (`colors.cells.readonly` → the group).
- **Standalone `ExecutionPalette`** — one reusable `DepthPalette` for the two identical 10-field blocks (ReadOnly +
  Disabled); Execution is its own record (8 depths + marker, no selected/foreground).

**Colors stay `string` in this slice.** Typing them as `StyleColor` is slice 4 — doing it here would mean re-touching
all 53 in place. So the group records carry `string …Color` fields exactly as the flat record does today; only the
*grouping* changes.

## The target shape

Root record (one level, ~10 groups + orientation):

```csharp
public sealed record GridStyleOptions(
    GridStyleFonts Fonts,
    GridStyleLayout Layout,
    SelectionColors Selection,
    ChangedCellColors ChangedCells,     // colors.cells.changed / changed_selected
    DepthPalette ReadOnlyCells,         // colors.cells.readonly   (10 fields)
    DepthPalette DisabledCells,         // colors.cells.disabled   (10 fields, identical shape)
    ExecutionPalette Execution,         // colors.cells.execution  (8 depths + marker)
    StatusBarStyle StatusBar,
    ValidationPanelStyle ValidationPanel,
    ChromeColors Chrome,                // 13 chrome + grid_line folded in (see note)
    GridOrientation Orientation);       // unsurfaced, carried through
```

Group records (all `public sealed record`, positional so a missed field is CS7036; Core, `SemiStep.Core.Configuration`;
new files, one per record; colors as `string`, sizes `int`/`double` as today):

| Record | Fields | DTO source |
|---|---|---|
| `GridStyleFonts` | FontFamily, HeaderFontSize, HeaderFontWeight, HeaderItalic, CellFontSize, CellFontWeight, CellItalic (7) | `fonts` |
| `GridStyleLayout` | CellPaddingLeft/Top/Right/Bottom, RowHeight (5) | `layout` |
| `SelectionColors` | Background, Foreground (2) | `colors.selection` |
| `ChangedCellColors` | Changed, ChangedSelected (2) | `colors.cells.changed` / `changed_selected` |
| `DepthPalette` | Depth0/1/2/3, Depth0Past/1Past/2Past/3Past, Selected, Foreground (10) | `colors.cells.readonly` **and** `…disabled` (one type, used twice) |
| `ExecutionPalette` | Depth0/1/2/3, Depth0Past/1Past/2Past/3Past, CurrentStepMarker (9) | `colors.cells.execution` |
| `StatusBarStyle` | Background, Foreground, Padding, ItemSpacing, FontSize, Weight, Italic, TimerLabelFontSize/Weight/Italic, TimerValueFontSize/Weight/Italic (13) | `status_bar` |
| `ValidationPanelStyle` | Background, Foreground, ErrorColor, WarningColor, MaxHeight (5) | `validation_panel` |
| `ChromeColors` | Info, Connected, Disconnected, LocalMode, Connecting, PanelBackground, PanelHeaderBackground, SubtleBorder, Separator, SecondaryForeground, GridBorder, GridBackground, HeaderForeground, **GridLine** (14) | `chrome` (13) + `colors.grid_line` |

7+5+2+2+10+10+9+13+5+14 = 77 surfaced + `Orientation` = 78 fields, all accounted for.

**Two loose fields** sit outside a sub-DTO in the source (`colors.grid_line`, `colors.cells.changed`/`changed_selected`).
(a) `grid_line` folds into `ChromeColors.GridLine` — it is grid chrome (beside `grid_border`/`grid_background`), and
one-level nesting means no lone `string` on the root; the mapper reads it from `colors.grid_line` into `Chrome`.
(b) `changed`/`changed_selected` become a small `ChangedCellColors` group. Both are the small cross-section reads the
one-level shape absorbs into the mapper.

## Acceptance Evidence

- `GridStyleOptions` is the nested root above; the ~10 group records exist; `DepthPalette` is one type used for both
  ReadOnly and Disabled. `Default` is composed from per-group values, byte-equal to today's flat `Default`.
- Both mappers rebuilt as per-group construction; **no YAML/DTO/validator change** — `SaveThenLoad_DistinctFixture…`
  (record→dto→yaml→dto→record) stays green, proving the nested record round-trips losslessly through the unchanged file
  format.
- Every consumer reads the nested path (`gridStyle.Fonts.CellFontSize`, `gridStyle.ReadOnlyCells.Depth1`, …); the app
  compiles with zero flat-field references left.
- Slice 1's guards, adapted to the nested record, still bite (proven by scratch checks); the `SurfacedEditablePropertyCount`
  (77) is unchanged — the VM's public property surface does NOT change in this slice (that's slice 5).
- `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format` clean; screenshot tests
  (`SemiStep.Screenshots/GridStyleEditorScreenshotTests`, if present) unchanged.

## Task 1: The full nested rewrite — record, mappers, consumers, VM, fixture, ALL test fixes (one atomic commit)

The record-shape change ripples until nothing references a flat field — this is **one atomic edit** across production AND
tests. A partial state does not compile. This task includes the slice-1 guard-test *adaptation* too (the name map + the
leaf walk), because those tests must reach green in the same commit — deferring them would leave committed red tests.
Do it all, reach full build+test green, commit once. (Task 2 only *proves* the adapted guards still bite and updates the
doc — no source change there.)

**Files:**
- Create: `Configuration/GridStyleFonts.cs`, `GridStyleLayout.cs`, `SelectionColors.cs`, `ChangedCellColors.cs`, `DepthPalette.cs`, `ExecutionPalette.cs`, `StatusBarStyle.cs`, `ValidationPanelStyle.cs`, `ChromeColors.cs` (all `SemiStep.Core.Configuration`, BOM, positional `public sealed record`).
- Rewrite: `Configuration/GridStyleOptions.cs`, `Mapping/GridStyleMapper.cs`, `GridStyleDtoMapper.cs`.
- Modify: `SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` (`Seed`/`BuildRecord` nested paths — the ~77 public properties do NOT change; `RecomputeCanSave`/`Validate` unchanged).
- Modify consumers (path rename only): `RecipeGrid/GridFontApplier.cs`, `ColumnBuilder.cs`, `ColumnWidthCalculator.cs`, `TextCellFactory.cs`, `Transposed/TransposedCellTemplateFactory.cs`, `Transposed/TransposedRecipeGridView.axaml.cs`, `ActiveRecipeGridSurface.cs`, `Styles/CellPaletteInstaller.cs`, `Styles/ExecutionPaletteInstaller.cs`. (`App.axaml.cs`/`ConfigFacade`/DI: carrier only, verify untouched.)
- Modify tests (flat-path → nested; compiler drives): `Helpers/GridStyleOptionsTestData.cs`, and every test that reads/`with`-mutates a flat record field — at least `GridStyleEditorFacadeTests`, `GridStyleEditorViewModelTests` (the non-guard tests + the guard adaptation), `GridStyleEditorWindowTests`, `ConfigFacadeGridStyleValidationTests`, `GridStyleMapperTests`, `CellPaletteInstallerTests`, `ExecutionPaletteInstallerTests`, `GridFactoryFontTests`, `ColumnWidthCalculatorTests`, `AppStatusBarTimerFontTests`, `TransposedCellBackgroundConverterTests`, `ActiveRecipeGridSurfaceTests`, `GridStyleWriterTests`, `GridStyleOrientationTests`, `SemiStep.Screenshots/RecipeGridScreenshotTests`. **This list is a starting point — grep the whole solution for the flat field names and fix every hit; the build gate is the ground truth.**

- [x] Create the ~10 group records (positional, `string` colors, per the table). `DepthPalette` is ONE type used for both `ReadOnlyCells` and `DisabledCells`.
- [x] Rewrite `GridStyleOptions` as the nested root + `Default`, every value byte-equal to today's flat `Default`.
- [x] **Rewrite both mappers, preserving the CURRENT fallback regimes exactly** (read `GridStyleMapper.cs` first — do NOT blanket-add `?? defaults`):
  - Whole-DTO-null → early `return GridStyleOptions.Default` (unchanged).
  - `?? defaults.<group>.<field>` per field ONLY for fonts / layout / selection / changed-cells / grid_line / status_bar / validation_panel / chrome — the defaults now read off the nested `GridStyleOptions.Default`'s group instances.
  - **The three cell palettes (readonly / disabled / execution) keep their NO-fallback `!` chain** (`dto.Colors!.Cells!.ReadOnly!.Depth0!`, …) — validator-guaranteed presence; adding `?? defaults` there would silently swap a validator failure for a `#000000` placeholder. Keep the validator-precondition intent.
  - `Orientation` via `GridOrientationValues.Parse`, not `??`.
  - `Chrome.GridLine` ↔ `colors.grid_line`; `ChangedCells` ↔ `colors.cells.changed`/`changed_selected` (the two cross-section reads the one-level nesting accepts). `GridStyleDtoMapper` reads `options.<Group>.<Field>` into each DTO section field-for-field.
- [x] Rewire the VM `Seed`/`BuildRecord` to nested paths (`BuildRecord` builds each group positionally; `Orientation` carried from `_source.Orientation`); rewire all ~9 consumers; rebuild `Distinct()` nested (same 77 distinct values).
- [x] **Adapt the slice-1 guards to the nested record** (`GridStyleEditorViewModelTests`):
  - `RecordField` → a VM-property→**nested-path** map (`SelectionBackground`→`Selection.Background`, `HeaderFontSize`→`Fonts.HeaderFontSize`, `RowHeight`→`Layout.RowHeight`, `ReadOnlyCellDepth1`→`ReadOnlyCells.Depth1`, `ExecutionCurrentStepMarker`→`Execution.CurrentStepMarker`, `Info`→`Chrome.Info`, `GridLine`→`Chrome.GridLine`, …). Exhaustive (all 77); count assert stays 77. This map is **hand-maintained, not derived** — several leaf names differ from the VM property name (`StatusBarFontWeight`→`StatusBar.Weight`, `StatusBarTimerLabelFontWeight`→`StatusBar.TimerLabelWeight`, `FontFamily`→`Fonts.FontFamily`), so a mistyped entry surfaces as a red guard test, not a silent pass. Cross-check each entry against the group records.
  - `ChangedRecordFields` → a **typed recursive leaf walk**: a property whose type is `string`/primitive/`enum` is a LEAF reported at its dotted path; a property whose type is a `record` group RECURSES. This keeps root-level `Orientation` (an enum) a first-class leaf — otherwise a `BuildRecord` bug that also flips Orientation would slip the exact-one-leaf assertion. Compare two `GridStyleOptions`, return the set of differing leaf paths; the perturbation asserts that set == `{ mappedPath }`.
  - Seed-populates compares each VM property to the fixture's mapped nested-leaf value (`Convert.ToDecimal` for boxed numerics, `HexColor.Parse` for colors — only the record access nests).
- [x] `dotnet build SemiStep.slnx` 0 warnings (zero flat-field references left) AND full `dotnet test` green (all non-guard tests + the adapted guards). One commit.

## Task 2: Prove the adapted guards still bite + doc

No source change — verification + doc only.

- [x] **Non-vacuity scratch checks (do NOT commit any break):**
  - **Perturbation** — a dropped `BuildRecord` field is a compile error now (positional construction → CS7036), so the runtime check is a **same-type cross-wire**: temporarily pass a sibling's value into the wrong positional slot (e.g. `SelectionForeground` into `SelectionColors`'s Background slot, or swap the `ReadOnlyCells`/`DisabledCells` `DepthPalette` instances in `BuildRecord`) → the perturbation test goes red for the mis-wired leaf(s). Revert.
  - **Seed-populates** — delete a `Seed` line (a nullable numeric or an italic) → red. Revert.
  - **Save→load** — delete a `GridStyleDtoMapper` line (nullable object-initializer → compiles, drifts through the fallback) → `SaveThenLoad_DistinctFixture` red. Revert.
  - Record all three results in the progress log. If the perturbation cross-wire does NOT go red, the leaf walk is wrong — that's a Task-1 defect to fix (re-open Task 1), not something to paper over.
- [x] Interim doc note in `Docs/architecture/grid-style-configuration.md`: the record is now nested per group (full rewrite is slice 5); YAML/DTO/validator unchanged. **Note for slice 4:** the error-path key `colors.grid_line` and the record path `Chrome.GridLine` deliberately diverge — slice 4's mapper-resident validation must keep emitting the `colors.grid_line` key, not rename it.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green (incl. screenshot tests); `dotnet format`. (screenshot project pre-existing-broken at HEAD, excluded from slnx)

## Post-Completion

**Next:** slice 4 — introduce `StyleColor` (readonly record struct, Core, Avalonia-free) and flip the 53 color fields
(now living in the group records) from `string` to `StyleColor`; fold `GridStyleValidator`'s per-key checks into the
load mapper (aggregate, same error identity); delete `HexColor`; retarget `PaletteBrushFactory.From(StyleColor)`. Then
slice 5 — split the VM into per-group drafts + grouped AXAML, which closes #118. Two forks await slice 4/5 planning:
`StyleColor` struct-vs-class + the `#RGB`/`#ARGB` parse discrepancy (slice 4), and the AXAML rename scope + facade
`Validate` fate (slice 5).

**Executed by exec:**
- branch: grid-style-nest-record

## Verify it yourself

This slice is a pure structural refactor: the on-disk YAML is byte-identical, the DTOs/validator are untouched,
and the operator-visible behaviour of the style editor and grid is unchanged. There is no manual repro that
distinguishes before from after — the whole point is that nothing observable moves. The evidence is in the tests
and the diff.

1. **Nothing on disk changed.** `git diff master...HEAD -- '*Dto*.cs' '*Validator*.cs' 'SemiStep/**/grid_style.yaml'`
   returns empty — only the record shape and its readers moved.
2. **The record round-trips losslessly through the unchanged file format.** Run the acceptance test that drives
   record→dto→yaml→dto→record with a distinct-value fixture:
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~SaveThenLoad_DistinctFixture"`
   — green means every one of the 78 fields survived the nesting and lands on the correct target field.
3. **The silent-drop guards still bite the nested record.** The slice-1 guards were adapted (nested-path map +
   typed leaf walk) and their non-vacuity was re-proven by scratch checks — see the progress log entries for the
   cross-wire → `BuildRecord_PerturbingEachProperty_ChangesOnlyThatMappedField` RED, delete-Seed-line →
   `Seed_PopulatesEverySurfacedProperty_FromDistinctFixture` RED, delete-DtoMapper-line → `SaveThenLoad_DistinctFixture` RED.
   Run all three green: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GridStyleEditorViewModelTests"`.
4. **Zero flat-field references remain and the app builds clean.** `dotnet build SemiStep.slnx` → 0 warnings, 0 errors;
   full `dotnet test` → 1687 passed. A dropped or cross-wired field would surface as a red guard or a build error, not
   a silent revert.
5. **Manual smoke (optional):** open the style editor, change a handful of values across groups (a font size, a
   selection color, a status-bar timer weight, orientation), save, reopen — every value persists. Pre-fix commit that
   would fail a dropped-field scenario is demonstrated by the Task-2 scratch checks, not reproducible by hand.
