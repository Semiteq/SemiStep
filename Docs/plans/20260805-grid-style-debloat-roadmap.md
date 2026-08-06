# Grid-Style Debloat Roadmap (#118)

## Summary

Issue #118: the grid-style configuration/editor stack maintains ~78 fields in parallel across a flat
record, two hand-written mappers, a color-enumerating validator, a three-way-mirrored editor view
model, and a 555-line editor window. Every touch point is untyped repetition: adding or renaming one
style field means editing up to 13 places across 9 files, and forgetting one of them fails silently
(an omitted field reverts to its previous value on save, or a color never reaches the screen).

This roadmap restructures the stack in five independent slices so that a missed field becomes a
**compile error or a red test**, not a silent no-op. Slice 1 (the anti-regression guard net, PR #175)
is merged; slice 2 (async save) has its own plan; slices 3–5 (nest the record, type the colors,
split the view model) are designed here and get focused plans when picked up. #118 closes after
slice 5.

Scope note: this is a roadmap, not an implementation plan. Each slice below gets its own plan/branch
when picked up (slice 2's already exists: `Docs/plans/20260805-grid-style-async-save.md`; slice 1's
completed plan is `Docs/plans/completed/20260805-grid-style-guards.md`).

All counts and line numbers below were verified against the code on 2026-08-05 (post-#175). Prefer
the shapes over the line numbers if they have drifted.

## Root-cause diagnosis

### The flat pivot record

`SemiStep/SemiStep.Core/Configuration/GridStyleOptions.cs` is a positional record of **arity 78**:
53 colors as raw `#RRGGBB` strings, 13 numerics, 5 font weights, 5 italics, the font family, and the
orientation enum — one flat parameter list, 163 lines with the `Default` instance. The YAML side is
already **nested** (`GridStyleOptionsDto` composes 11 per-section DTOs: fonts, layout,
colors.selection, colors.cells.{readonly,disabled,execution}, status_bar, validation_panel, chrome).
The flat record is the pivot everything else must flatten into and unflatten out of, and it forces:

- **Two hand-written mappers.** `Mapping/GridStyleMapper.cs` (100 lines, DTO → record, one line per
  field) and `Mapping/GridStyleDtoMapper.cs` (124 lines, record → DTO, one line per field). Both
  exist only to bridge nested ↔ flat.
- **A color-enumerating validator.** `Validation/GridStyleValidator.cs` (243 lines) re-lists every
  color key as a `(name, value)` tuple per section to run one regex over each — the section/key
  structure restated a third time.
- **A ×3 view-model mirror.** `SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` (523 lines)
  declares all 77 surfaced fields three times: the property declarations (`Color` per color,
  `decimal?` per numeric), the `Seed` method (77 assignment lines), and `BuildRecord` (77
  `with`-initializer lines). Orientation is the one unsurfaced field (78 − 1 = 77; pinned by the
  `SurfacedEditablePropertyCount` constant in `GridStyleEditorViewModelTests`).

### The per-field tax (verified counts)

Places one field is declared, mapped, bound, validated, or consumed today:

| Touch point | Color field (`ReadOnlyCellDepth1Color`) | Numeric field (`RowHeight`) |
|---|---|---|
| Record positional parameter | `GridStyleOptions.cs` | `GridStyleOptions.cs` |
| Record `Default` argument | yes | yes |
| DTO property | `GridStyleReadOnlyCellColorsDto` | `GridStyleLayoutDto` |
| Load-mapper line | `GridStyleMapper.cs` | `GridStyleMapper.cs` |
| Save-mapper line | `GridStyleDtoMapper.cs` | `GridStyleDtoMapper.cs` |
| Validator tuple entry | `GridStyleValidator.cs` | — |
| VM property declaration | yes | yes |
| VM `Seed` line | yes | yes |
| VM `BuildRecord` line | yes | yes |
| VM range check | — | `NumericsInRange` |
| AXAML binding | `GridStyleEditorWindow.axaml` | yes |
| Resource-key constant | `CellPaletteInstaller` | `RowHeightKey` |
| Installer projection line | `CellPaletteInstaller.Install` | yes |
| Direct runtime reads | — (consumed via resource) | `ColumnBuilder`, `TransposedRecipeGridView` |
| Test fixture value | `GridStyleOptionsTestData.Distinct()` | yes |

**13 touch points across 9 files for a color; 13–15 for a numeric.** The target design does not
drive this to zero — DTOs, consumers, and the fixture legitimately remain — but it converts every
remaining bookkeeping point into something the compiler or a guard test checks, and merges the three
restatements (mappers' flatten layer, validator tuples, VM mirror) into structure.

### The silent-revert mechanism

`BuildRecord` rebuilds via `_source with { … }`. The `with` expression preserves any field not
listed — which is the intended mechanism for the unsurfaced `Orientation`, but it also means **a
field accidentally dropped from `BuildRecord` (or `Seed`) silently reverts to its loaded value on
save**. Before slice 1 nothing detected this; the compiler cannot, because `with` has no
completeness check. The same failure mode exists in both mappers (a dropped line falls back to
`Default` or serializes nothing) and in `Seed` (a dropped line leaves the property at its type
default). This is the second structural cause, independent of the line count: the shape makes
omission *legal*.

## Consumer survey (why the redesign is safe)

Verified against every non-test reader of `GridStyleOptions` (13 UI files plus the config layer).
The consumers read the record **per group**, never as a flat whole:

| Group | Consumers | How consumed |
|---|---|---|
| Fonts (family + per-role size/weight/italic) | `RecipeGrid/GridFontApplier.cs`, `RecipeGrid/ColumnBuilder.cs`, `RecipeGrid/ColumnWidthCalculator.cs` | code-assigned per role; the width calculator measures with the same typeface |
| Layout (cell paddings, row height) | `RecipeGrid/TextCellFactory.cs`, `RecipeGrid/Transposed/TransposedCellTemplateFactory.cs`, `ColumnBuilder`, `Transposed/TransposedRecipeGridView.axaml.cs` | `Thickness`/height construction |
| Cell-state + selection + status-bar + validation-panel + chrome palettes | `Styles/CellPaletteInstaller.cs` | wholesale projection into `Application.Resources` brush/value keys at startup (`App.axaml.cs`) |
| Execution palette | `Styles/ExecutionPaletteInstaller.cs` | same projection, execution depth brushes + marker |
| Orientation | `RecipeGrid/ActiveRecipeGridSurface.cs` | read once at construction |
| Whole record (carrier only) | `App.axaml.cs`, `ComboBoxCellFactory`, `TransposedRecipeGridSurface` (`GridStyle` property), `ConfigFacade`/`AppConfiguration` (DI wiring) | passed through, no field logic |

Hex strings become Avalonia types in exactly **one runtime place**:
`Styles/PaletteBrushFactory.From(string)` → `Color.Parse`. (The editor has its own edge,
`StyleEditor/HexColor.cs`, for `Color` ↔ hex in the pickers.) No consumer wants the flat shape; the
grouping the DTO already has matches consumption exactly. The flat record is accidental structure,
and nesting it ripples only property paths, not logic.

## Target design

Four coordinated changes, delivered by slices 3–5:

**1. Nest the record to mirror the consumption groups.** `GridStyleOptions` becomes a ~15-line root
record composing per-group records — fonts, layout, selection, cell-state, status-bar,
validation-panel, chrome, execution — plus `Orientation`. The identical ReadOnly and Disabled
10-field blocks (`Depth0..3`, `Depth0Past..3Past`, `Selected`, `Foreground`) collapse into one
reusable `DepthPalette` record used twice; Execution (8 depths + `CurrentStepMarker`, no
selected/foreground) gets its own small record. Group records stay positional so construction is
compile-checked (CS7036 on omission).

**2. Type the colors.** A `StyleColor` value type in Core (readonly record struct, A/R/G/B channels,
`Parse`/`TryParse`/`ToString`) replaces the 53 `string` fields. `ToString` absorbs `HexColor.ToHex`'s
round-trip rule: opaque colors emit `#RRGGBB` (uppercase), translucent `#AARRGGBB` — never the
`#FF`-prefixed form `Avalonia.Media.Color.ToString()` would inject. Hex parsing then exists only at
the I/O edge (the load mapper) and hex formatting only at the save edge; everywhere else a color is
channels. Core stays Avalonia-free (a hard constraint — the record is consumed in Core and tests
without Avalonia), so `StyleColor` cannot be `Avalonia.Media.Color`; the UI converts channel-wise.

**3. Fold the validator into the load mapper.** With typed colors, "is this hex valid" is asked
exactly where the string is parsed. The load mapper accumulates the same typed errors the validator
emits today (`GridStyleSectionMissingError`, `GridStyleKeyMissingError`,
`GridStyleHexColorInvalidError` with section path + key name, `GridStyleOrientationInvalidError`) —
**aggregate, not fail-fast**, preserving per-key error identity for `ReasonLocalizer` and the
existing error tests. The 243-line validator and its third restatement of the key structure are
deleted.

**4. Split the view model into per-group drafts.** The monolithic VM becomes a thin parent (~150
lines: facade wiring, `SaveCommand`, `CanSave`, font-picker sources) plus one small `ReactiveObject`
draft per group. A draft's property initializers *are* the seed (constructed from its group record —
a property without an initializer is visibly unseeded in one screenful), and its `Build()` calls the
group record's positional constructor — so an omitted field is a **compile error**, replacing the
`with`-rebuild and its silent revert. The AXAML binding paths become grouped
(`{Binding Fonts.HeaderSize}`); paths are already build-checked today via the window-level
`x:DataType` (verified empirically: a bogus path fails the build with AVLN2000), so every rename is
caught at compile time.

### End-state shape

| Artifact | Today | Target |
|---|---|---|
| `GridStyleOptions.cs` | 163 lines, arity-78 flat | ~15-line root + ~8 small group records (incl. `DepthPalette`, reused ×2) |
| `GridStyleMapper.cs` + `GridStyleDtoMapper.cs` | 100 + 124 lines, one line per field | per-group mapping, positional construction; load side absorbs validation |
| `GridStyleValidator.cs` | 243 lines | deleted (checks live in the load mapper) |
| `GridStyleEditorViewModel.cs` | 523 lines, ×3 mirror | ~150-line parent + per-group draft classes |
| `HexColor.cs` | 27 lines | deleted (absorbed by `StyleColor`) |
| `GridStyleEditorWindow.axaml` | 555 lines, flat paths | same size, grouped compiled-checked paths |
| Missed field on rename/add | silent revert / silent default | compile error in 3 places (group ctor, draft `Build()`, AXAML path) + red guard test |

Net line count in the config/editor layer drops from ~1180 (record + mappers + validator + VM +
`HexColor`) to an estimated ~750–800. **The LOC reduction is not the point** — the structural win
is that field omission stops being representable, and the per-field tax that remains is
compile-checked or guard-tested.

## The guard net (slice 1 — DONE, PR #175)

Slices 3–5 are mechanical rewrites of exactly the layers where a dropped field is silent. The guard
net exists so that *any* such drop turns a later slice's PR red instead of shipping. Shipped in
PR #175 (merged 2026-08-05); design rationale in `Docs/plans/completed/20260805-grid-style-guards.md`.

The fixture: `SemiStep/SemiStep.Tests/Helpers/GridStyleOptionsTestData.Distinct()` — every field
holds a distinct, valid, exactly-representable value (53 mutually distinct uppercase opaque hex
colors; integers/clean halves so `int`↔`decimal`↔`double` conversions are exact; all five italics
`true` so a dropped bool line surfaces as `false ≠ true`; non-default `Orientation` proving the
unsurfaced field survives `with`).

The guards, split by direction and field kind:

| Guard (test) | Catches |
|---|---|
| `Seed_PopulatesEverySurfacedProperty_FromDistinctFixture` — asserts each of the 77 editable VM properties equals the fixture's mapped value | every `Seed` omission and every non-bool `Seed` cross-wire |
| `BuildRecord_PerturbingEachProperty_ChangesOnlyThatMappedField` — perturb one property, assert exactly one record field changed | every `BuildRecord` drop, mis-target, and same-valued-bool swap |
| `SaveThenLoad_DistinctFixture_PreservesEveryMappedField` — facade `Save` → `Load` == fixture | any omission in either mapper — the flatten/unflatten layer slice 3 rewrites |
| `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly` | integration sanity over the whole editor round-trip |

Every later slice leans on these: a rename or rewrite cannot silently drop or cross-wire a field
without one of them failing. **Documented residual:** a bool↔bool `Seed` cross-wire among the five
italics is structurally invisible to the Seed guard (all five are `true` in the fixture; two-valued
fields cannot be mutually distinct) — accepted, since the perturbation guard covers the
`BuildRecord` direction and slice 5's typed drafts eliminate the hand-written `Seed` entirely.

## Migration slices

Each slice is one independent PR, ordered smallest-safe-first. Ordering dependencies: 3 before 4
(typing colors on the flat 78-arity record would mean re-touching all 53 in place); 4 before 5 is
preferred (drafts seed from typed group records without hex parsing); 2 is independent of 3–5.

### Slice 1 — anti-regression guards. Status: DONE (PR #175, merged)

See "The guard net" above. Also fixed en route: the exact-one-field guards flushed out the
distinct-value blind spots of the old `Default`-based round-trip test (all-`#000000` colors masked
cross-wires).

### Slice 2 — async `Save` off the UI thread. Status: DONE (branch `grid-style-async-save`, PR #176)

`GridStyleEditorFacade.Load` and `Save` now return `Task` and do file I/O off the UI thread.
`IGridStyleEditorFacade.Save` → `Task<Result>`, async `GridStyleWriter`
(`WriteAllTextAsync`; the atomic `File.Move` stays sync), VM command via
`ReactiveCommand.CreateFromTask`. `Validate` stays sync — `RecomputeCanSave` calls it per keystroke
and it is a pure in-memory pass. The window is unchanged. Behavior-preserving; slice 1's guards keep
holding. Touches: the facade interface + implementation, the writer, the VM save path, and every
test caller of `Save`.

Blast radius: small. Risk: low (threading only, no mapping changes).

### Slice 3 — nest the record (+ `DepthPalette`). Status: DONE (PR #177)

The big line-count win and the **only slice that ripples the consumers**.

Scope: replace the flat record with the nested root + group records; rewrite both mappers as
per-group positional construction; rewire every consumer's property path; retarget the fixture and
the VM's `Seed`/`BuildRecord` paths (the VM stays a monolith with a nested `with`-rebuild until
slice 5 — mechanical path change only).

Touches: `GridStyleOptions.cs` + new group-record files (Core/Configuration); both mappers; the VM's
`Seed`/`BuildRecord`; `GridStyleOptionsTestData`; and the consumer files listed in the survey above
(`CellPaletteInstaller`, `ExecutionPaletteInstaller`, `GridFontApplier`, `ColumnBuilder`,
`TextCellFactory`, `ComboBoxCellFactory`, `ColumnWidthCalculator`, `ActiveRecipeGridSurface`,
`TransposedRecipeGridSurface`, `TransposedRecipeGridView.axaml.cs`,
`TransposedCellTemplateFactory`). `App.axaml.cs`, `ConfigFacade`, DI registration: carrier only,
likely untouched. **The YAML format, DTOs, and validator do not change** — the file on disk is
identical before and after.

Blast radius: wide but shallow — every touch is a property-path rename, no logic changes. Risk:
medium by surface area, low by mechanism; the save→load and Seed/perturbation guards catch any
dropped or cross-wired field, and the compiler catches every path rename. Do not mix any behavior
change into this PR.

### Slice 4 — type colors as `StyleColor`, fold validation into the load map. Status: DONE (branch `grid-style-color-typing`, PR #178)

Scope: introduce `StyleColor` in Core; flip the 53 color fields (now spread over the group records)
from `string` to `StyleColor`; move hex parsing + per-key error accumulation into the load mapper;
delete `GridStyleValidator` (orientation and section-missing checks move with it); serialize via
`StyleColor.ToString` in the save mapper (the DTOs keep their double-quoted string properties, so
YAML output shape is unchanged); retarget `PaletteBrushFactory.From(StyleColor)` (channel copy, no
parse); replace `HexColor` in the VM with channel-wise `StyleColor` ↔ `Avalonia.Media.Color`
conversion and delete `HexColor.cs`; decide what remains of `IGridStyleEditorFacade.Validate` (with
typed colors it has almost nothing left to check — see open questions).

Error-identity constraint (the hard part): the mapper must aggregate **all** errors per load, keyed
by the same section path + key name the validator emits today, so `ReasonLocalizer` output and
`GridStyleColorsValidationTests` semantics survive. Parse-accepting rule follows the current
validator: `#RRGGBB` / `#AARRGGBB` (6 or 8 hex digits) — note the shipped file header advertises
`#RGB`/`#ARGB` too, which the validator already rejects for validated keys; slice 4 should settle
that discrepancy explicitly rather than inherit it silently.

Behavior note: hex **case** normalizes on save (typed equality is value equality; the writer emits
canonical uppercase). Acceptable — the editor already rewrites the whole file.

Blast radius: Core config layer + `PaletteBrushFactory` + VM edges; consumers of brushes unchanged.
Risk: medium — concentrated in error-path fidelity, covered by the existing error tests plus the
save→load guard.

### Slice 5 — split the VM into per-group drafts + grouped AXAML. Status: DONE (branch `grid-style-vm-split`). Closes #118

Scope: per-group `ReactiveObject` drafts (initializer = seed, positional `Build()` = compile guard);
parent VM shrinks to ~150 lines; `CanSave` reduces to the numeric range checks (color pickers cannot
produce an invalid `StyleColor`, so the per-keystroke facade `Validate` call goes away);
`IGridStyleEditorFacade` shrinks accordingly; `GridStyleEditorWindow.axaml` paths become grouped —
each rename is build-checked (`x:DataType` + AVLN2000, verified). The screenshot tests
(`SemiStep.Screenshots/GridStyleEditorScreenshotTests`) guard the visual layout across the rewrite.

Documentation lands here: rewrite `Docs/architecture/grid-style-configuration.md` to describe the
nested record, `StyleColor`, the mapper-resident validation, and the draft-based editor as *current*
(slices 2–4 only add interim notes). While in there, fix the doc's already-stale "Known gap"
section — the validator has since gained optional-section coverage (selection, changed, grid line,
status-bar, validation-panel keys are format-checked when present), and after slice 4 the section is
obsolete entirely.

Blast radius: `StyleEditor/` (VM, drafts, AXAML) + facade interface + VM tests. Risk: medium — the
AXAML is 555 lines of renames, but every path is compile-checked and the Seed/perturbation guards
re-verify the field wiring end to end.

## Trade-offs and rejected alternatives

Settled during design — do not relitigate without new facts:

- **Serialize the nested record directly (custom YAML converter), dropping the DTOs.** Rejected.
  Grid style would diverge from every sibling config section (actions/columns/groups/properties all
  use nullable snake_case DTOs), and YamlDotNet-level type converters fail on the first bad scalar —
  losing the aggregate per-key error report that names every invalid key in one pass. The DTO layer
  is also where "absent key → default" is expressible; typed records deliberately cannot represent
  absence.
- **Source-generate the VM mirror (or the mappers).** Rejected. A generator hides exactly the code a
  maintainer must read to answer "where does this field go" — the readability-first goal of #118.
  The positional-constructor discipline gets the same completeness guarantee from the plain
  compiler, with no toolchain cost.
- **Reflection-based generic mapping/seeding.** Rejected for the same reason plus type-erasure: the
  compile-time omission check is the entire point, and reflection trades it away to save lines.
- **Make the record mutable / bind AXAML straight to the record.** Rejected. The immutable record is
  load-bearing (DI singleton, `Avalonia`-free Core, `with`-based tests), and Avalonia two-way
  binding needs settable named properties — hence drafts remain a separate mutable layer; the design
  makes them small and compile-guarded instead of eliminating them.
- **Honest accounting.** The refactor removes roughly 400 lines net (~1180 → ~750–800 in the layer),
  not a dramatic collapse — the DTOs, the AXAML, and the consumers keep their size. What it buys is
  categorical: after slice 5 there is no hand-maintained full-field list left whose omission
  compiles.

## Open questions for the operator

Genuine forks needing a human call at slice 3 / slice 5 planning time; everything above stands
regardless of how these land.

1. **Nesting depth (slice 3).** Mirror the DTO tree exactly (`Colors.Cells.ReadOnly.Depth1` — three
   levels) or flatten to one level of ~8 groups (`ReadOnlyPalette.Depth1`)? One level matches
   consumption and keeps paths short; exact mirroring makes the mappers near-trivial. Recommendation
   pending taste: one level, letting the mapper own the small DTO-tree walk.
2. **`DepthPalette` for Execution (slice 3).** Execution is 8 depths + marker (no
   selected/foreground). Reuse a shared 8-field depth block inside both `DepthPalette` and an
   `ExecutionPalette`, or accept a 9-field standalone record and keep `DepthPalette` for exactly the
   two identical 10-field blocks? Extra sharing saves ~8 lines and costs one more nesting hop.
3. **`StyleColor` struct vs class (slice 4).** `readonly record struct` is the natural fit (value
   semantics, no allocation per color ×53); the alternative is a sealed record class if boxing in
   the reactive layer or a future null-means-inherit semantic matters. Also settle whether
   `Parse` accepts `#RGB`/`#ARGB` (file header says yes, validator says no — see slice 4).
4. **AXAML rename scope (slice 5).** Rename all 77 binding paths to grouped form in the same PR as
   the VM split (one coherent build-checked change, bigger diff) or keep flat names on the drafts
   via bridge properties and rename in a follow-up? The compile check makes the big-bang rename
   safe; the bridge keeps the PR reviewable. Also decide whether the parent exposes drafts as
   properties (`Fonts.HeaderSize`) or the window sets per-card `DataContext`.
5. **What remains of facade `Validate` (slices 4–5).** After colors are typed, `Validate(record)`
   has nothing to check (numeric ranges are VM-side by design). Drop it from
   `IGridStyleEditorFacade` at slice 4 already, or keep a vacuous pass until slice 5 trims the
   interface? Affects only how many PRs touch the interface.
