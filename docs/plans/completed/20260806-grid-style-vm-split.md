# Slice 5 — Split the editor VM into per-group drafts, group the AXAML paths

## Overview

Slice 5 of the #118 debloat — the final slice; its PR closes #118. The monolithic 534-line
`GridStyleEditorViewModel` (77 leaf properties + `Seed` + `BuildRecord`, the last ×3 hand-maintained field
mirror in the stack) becomes a thin parent plus nine per-group `ReactiveObject` draft classes. A draft's
property initializers ARE the seed (each constructed from its group record), and its `Build()` calls the
group record's positional constructor — so an omitted field is a compile error (CS7036), replacing the
`Seed`-line/`with`-rebuild failure modes the slice-1 guards existed to catch. All 77 AXAML binding paths in
`GridStyleEditorWindow.axaml` rename to grouped `Group.Prop` form in the same PR, build-checked by the
window-level `x:DataType` (a bad path fails the build with AVLN2000 — verified empirically during roadmap
work). `IGridStyleEditorFacade` loses the vacuous `Validate` slice 4 deliberately parked; `CanSave` reduces
to the VM-side numeric range checks. The architecture doc is rewritten as current-state.

Behavior-preserving for the operator: the editor looks and behaves identically — same window, same
controls, same save→restart-prompt→close flow, same error surfaces. Only the VM/AXAML internal structure
and the facade seam change.

**Locked decisions — settled by the operator, do not re-open:**
- **Binding shape = grouped paths, one DataContext.** The parent VM keeps the window's single
  `DataContext` and exposes each group draft as a property (`public GridStyleFontsDraft Fonts { get; }`).
  All 77 paths rename to `{Binding Fonts.HeaderFontSize}` form in this PR — big-bang, compile-checked. NOT
  per-card `DataContext`, NOT bridge properties.
- **`Validate` leaves `IGridStyleEditorFacade`** (interface + implementation + the per-keystroke call).
  With typed `StyleColor` a color picker cannot produce an invalid color; nothing is left for a facade
  `Validate` to check. `CanSave` = numeric ranges only.
- **Per-group `ReactiveObject` drafts**: initializer = seed, positional `Build()` = compile guard.
- **`Docs/architecture/grid-style-configuration.md` rewritten as current-state**, including deleting the
  obsolete "Known gap" section (closed by slice 4's `OptionalColor` — every optional-section key IS
  format-checked when present) and all slice-2/3/4 interim notes.

**Decisions made in this plan (rationale inline, executor may not silently deviate):**
- **Draft leaf names mirror the group-record component names exactly** (`StatusBar.Weight`, not
  `StatusBar.FontWeight`; `ValidationPanel.ErrorColor`, not `ValidationPanel.Error`). This makes the
  record path derivable as `{ParentProperty}.{DraftProperty}` and kills the hand-maintained 77-entry
  VM-property→record-path map in the guard tests (see the guard-net section).
- **`Build()` is strict — the numeric null-fallback dies.** Today `BuildRecord` falls back to
  `_source` when a `decimal?` is null because `RecomputeCanSave` called `BuildRecord` per keystroke and a
  NumericUpDown is transiently null while typing. After Task 1 removes the `Validate` call, `BuildRecord`
  runs only on save (gated by `CanSave`, which requires all numerics non-null-in-range) and in tests. So
  draft `Build()` converts strictly: `ToInt`/`ToDouble` throw `InvalidOperationException` on null. This
  removes the drafts' need to retain their source record, which also sidesteps CS9124 (a primary-ctor
  parameter both captured into a member AND used in initializers is a warning — and warnings are errors).
  If an unexpected caller surfaces, the fallback alternative is an explicit
  `private readonly` source field per draft (no CS9124), not silent `default` substitution.
- **`FontFamily` builds as `FontFamily ?? ""`** (empty string = theme default). The old fallback to the
  source family is unreachable: the picker sources always contain the seeded value, so the bound string is
  never nulled (existing contract, kept).
- **CanSave wiring**: the parent subscribes each draft's `Changed` observable into a
  `CompositeDisposable` that is replaced when `LoadAsync` re-seeds (new drafts, new subscriptions; stale
  drafts must not keep driving `CanSave`). The `_seeding` flag dies — seeding is construction, and
  subscriptions attach after the drafts exist.

Branch: `grid-style-vm-split` off `origin/master` (slices 1–4 are merged; no stacking this time).

## The target shape

### One draft (sketch — `DepthPaletteDraft`, used twice)

`SemiStep/SemiStep.UI/StyleEditor/DepthPaletteDraft.cs` (naming rule: `<GroupRecord>Draft`, namespace
`SemiStep.UI.StyleEditor`, one class per file):

```csharp
public sealed class DepthPaletteDraft(DepthPalette source) : ReactiveObject
{
	public Color Depth0 { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
		= source.Depth0.ToMediaColor();
	public Color Depth1 { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
		= source.Depth1.ToMediaColor();
	// ... Depth2, Depth3, Depth0Past..Depth3Past, Selected, Foreground — 10 total.
	// A property WITHOUT an initializer is visibly unseeded in one screenful; a wrong
	// source field is caught by the per-draft round-trip guard (see guard-net section).

	public DepthPalette Build()
	{
		return new DepthPalette(          // positional — an omitted argument is CS7036
			Depth0: Depth0.ToStyleColor(),
			Depth1: Depth1.ToStyleColor(),
			// ... all 10
			Foreground: Foreground.ToStyleColor());
	}
}
```

Numeric-bearing drafts expose `decimal?` (NumericUpDown) and build strictly via a shared helper
(`SemiStep/SemiStep.UI/StyleEditor/DraftNumbers.cs`): `ToInt(decimal?)` rounds away-from-zero,
`ToDouble(decimal?)` casts; both throw `InvalidOperationException` naming the draft state on null (only
reachable if `Build` is called on a draft `CanSave` rejects). `source` appears ONLY in property
initializers — never in `Build` — so no primary-ctor capture and no CS9124.

### The nine draft classes (77 leaves total)

| Draft class (new file) | Parent property | Builds group record | Leaves |
|---|---|---|---|
| `GridStyleFontsDraft` | `Fonts` | `GridStyleFonts` | FontFamily (`string?`), HeaderFontSize/CellFontSize (`decimal?`), HeaderFontWeight/CellFontWeight (`int`), HeaderItalic/CellItalic (`bool`) — 7 |
| `GridStyleLayoutDraft` | `Layout` | `GridStyleLayout` | CellPaddingLeft/Top/Right/Bottom, RowHeight (all `decimal?`) — 5 |
| `SelectionColorsDraft` | `Selection` | `SelectionColors` | Background, Foreground — 2 |
| `ChangedCellColorsDraft` | `ChangedCells` | `ChangedCellColors` | Changed, ChangedSelected — 2 |
| `DepthPaletteDraft` | `ReadOnlyCells` AND `DisabledCells` | `DepthPalette` | Depth0–3, Depth0Past–3Past, Selected, Foreground — 10 (×2 uses) |
| `ExecutionPaletteDraft` | `Execution` | `ExecutionPalette` | Depth0–3, Depth0Past–3Past, CurrentStepMarker — 9 |
| `StatusBarStyleDraft` | `StatusBar` | `StatusBarStyle` | Background, Foreground (`Color`), Padding, ItemSpacing, FontSize, TimerLabelFontSize, TimerValueFontSize (`decimal?`), Weight, TimerLabelWeight, TimerValueWeight (`int`), Italic, TimerLabelItalic, TimerValueItalic (`bool`) — 13 |
| `ValidationPanelStyleDraft` | `ValidationPanel` | `ValidationPanelStyle` | Background, Foreground, ErrorColor, WarningColor (`Color`), MaxHeight (`decimal?`) — 5 |
| `ChromeColorsDraft` | `Chrome` | `ChromeColors` | Info, Connected, Disconnected, LocalMode, Connecting, PanelBackground, PanelHeaderBackground, SubtleBorder, Separator, SecondaryForeground, GridBorder, GridBackground, HeaderForeground, GridLine — 14 |

7+5+2+2+10+10+9+13+5+14 = 77 — the full surfaced set; `Orientation` stays unsurfaced, carried by the
parent from `_source.Orientation`.

### The parent VM (sketch)

`GridStyleEditorViewModel` keeps: both constructors (same signatures — DI, `UIFixture`, and the window
tests construct it today), `SaveCommand` + `ThrownExceptions` routing, `ErrorMessage`, `CanSave`,
`LoadAsync`, `SaveAsync`, `ReportSaveException`, `LogCausedByExceptions`, the range constants
(`MinFontSize`…`MaxPanelMaxHeight` — referenced by tests), `AvailableFontFamilies`/`AvailableFontWeights`
and their builders, `NumericsInRange`/`InRange`, and the ten draft properties:

```csharp
public GridStyleFontsDraft Fonts { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
// ... Layout, Selection, ChangedCells, ReadOnlyCells, DisabledCells, Execution,
//     StatusBar, ValidationPanel, Chrome — names mirror the root record's components.

private void ReplaceDrafts(GridStyleOptions options)   // ctor + successful LoadAsync
{
	Fonts = new GridStyleFontsDraft(options.Fonts);
	// ... one line per group; wrong-group wiring here is caught by the
	//     Seed_ThenBuildRecord whole-record guard (distinct fixture).
	AvailableFontFamilies = BuildFontFamilies(options.Fonts.FontFamily);
	AvailableFontWeights = BuildFontWeights(options);
	_draftSubscriptions.Dispose();
	_draftSubscriptions = new CompositeDisposable(/* each draft.Changed → RecomputeCanSave() */);
	RecomputeCanSave();
}

public GridStyleOptions BuildRecord()
{
	return new GridStyleOptions(          // positional — an omitted group is CS7036
		Fonts: Fonts.Build(),
		Layout: Layout.Build(),
		// ... all ten groups
		Orientation: _source.Orientation);
}

private void RecomputeCanSave() => CanSave = NumericsInRange();   // no facade call, no BuildRecord
```

`NumericsInRange` reads the grouped paths (`Fonts.HeaderFontSize`, `Layout.RowHeight`,
`StatusBar.Padding`, …, `ValidationPanel.MaxHeight`) — same 13 checks, same bounds. Deleted from the
parent: the 77 leaf properties, `Seed`, `SetColor`/`SetNumber`/`SetValue`, `_seeding`, `ToInt`/`ToDouble`
(moved strict into `DraftNumbers`). Honest size accounting: the parent lands around 250 lines, not the
roadmap's ~150 — the picker-source builders (~55 lines) and the range checks stay parent-side; the ×3
mirror (≈240 lines of Seed/BuildRecord/property declarations) is what dies. The structural claim is not
the line count; it is that no hand-maintained full-field list remains whose omission compiles.

### The AXAML rename (77 paths, same file structure)

`GridStyleEditorWindow.axaml` keeps `x:DataType="styleEditor:GridStyleEditorViewModel"` and every
control/layout element; only `{Binding …}` path strings change. Rule: new path =
`{ParentProperty}.{RecordComponentName}`. The renames, by group — divergent names in bold:

| Group | Old flat path → new grouped path |
|---|---|
| Layout (5) | `RowHeight`→`Layout.RowHeight`; `CellPaddingLeft/Top/Right/Bottom`→`Layout.CellPadding*` |
| Fonts (7) | `HeaderFontSize/HeaderFontWeight/HeaderItalic/CellFontSize/CellFontWeight/CellItalic`→`Fonts.*`; `FontFamily`→`Fonts.FontFamily` |
| Selection (2) | **`SelectionBackground`→`Selection.Background`**, **`SelectionForeground`→`Selection.Foreground`** |
| ChangedCells (2) | **`CellChanged`→`ChangedCells.Changed`**, **`CellChangedSelected`→`ChangedCells.ChangedSelected`** |
| ReadOnlyCells (10) | **`ReadOnlyCellDepth0`→`ReadOnlyCells.Depth0`** … `Depth3Past`, **`ReadOnlyCellSelected`→`ReadOnlyCells.Selected`**, **`ReadOnlyCellForeground`→`ReadOnlyCells.Foreground`** |
| DisabledCells (10) | same pattern: `DisabledCell*`→`DisabledCells.*` |
| Execution (9) | `ExecutionDepth0..3(,Past)`→`Execution.Depth*`; `ExecutionCurrentStepMarker`→`Execution.CurrentStepMarker` |
| StatusBar (13) | `StatusBarBackground/Foreground/Padding/ItemSpacing/FontSize`→`StatusBar.*`; **`StatusBarFontWeight`→`StatusBar.Weight`**; **`StatusBarItalic`→`StatusBar.Italic`**; `StatusBarTimerLabelFontSize`→`StatusBar.TimerLabelFontSize`; **`StatusBarTimerLabelFontWeight`→`StatusBar.TimerLabelWeight`**; **`StatusBarTimerLabelItalic`→`StatusBar.TimerLabelItalic`**; same trio for TimerValue |
| ValidationPanel (5) | `ValidationPanelBackground/Foreground`→`ValidationPanel.*`; **`ValidationPanelError`→`ValidationPanel.ErrorColor`**; **`ValidationPanelWarning`→`ValidationPanel.WarningColor`**; `ValidationPanelMaxHeight`→`ValidationPanel.MaxHeight` |
| Chrome (14) | **`GridBackground/GridBorder/GridLine/HeaderForeground/PanelBackground/PanelHeaderBackground/SubtleBorder/Separator/SecondaryForeground/Info/Connected/Disconnected/LocalMode/Connecting` → `Chrome.<same name>`** |

Caution — chrome colors do not all sit in one card: the `Info` picker lives in the notification-panel
card, and `Connected`/`Disconnected`/`LocalMode`/`Connecting` live in the status-bar Colors card (AXAML
~lines 409–420) — yet ALL group under `Chrome.*` because grouping follows the RECORD component, not the
visual card. An executor counting the status-bar card's pickers and expecting six `StatusBar.*` paths
would miswire four of them; the derive-by-record-component rule is authoritative.

Unchanged paths: `SaveCommand`, `ErrorMessage` (×2), `AvailableFontWeights`/`AvailableFontFamilies`
(parent-level), the item-template bindings (`Name`, `Value` against `FontWeightOption`/
`FontFamilyOption`), and the `$parent[ColorPicker]` template bindings. Compiled bindings resolve chained
INPC paths per segment, so replacing a whole draft on re-seed re-evaluates every `Group.Prop` binding when
the parent raises change for the group property.

### Visual safety — screenshot project is NOT the guard

`SemiStep.Screenshots` is excluded from `SemiStep.slnx` (verified: no `Screenshots` entry) and its
`GridStyleEditorScreenshotTests` is already broken at HEAD independent of this slice — it constructs the
VM via reflection with the removed 3-argument constructor
(`Activator.CreateInstance(viewModelType, [facade, Path.GetTempPath(), gridStyle])`; the logger parameter
added since makes that a runtime `MissingMethodException`). Do not rely on it and do not repair it in this
PR. The visual-safety claim rests on: (a) the AXAML diff touches only `{Binding …}` path strings — zero
layout/control changes; (b) every path is compile-checked (AVLN2000); (c) the headless tests that
construct and show the REAL `GridStyleEditorWindow` (`GridStyleEditorWindowOwnerRoutingTests`, including
`SaveThenExitNow_DrivesRealGlue…` with a real VM as DataContext) load the compiled AXAML and run the full
save→restart-prompt→close flow.

## The guard-net transition

The slice-1 guards were built around the monolith: reflection over 77 flat VM properties, a
hand-maintained property→nested-path map, `Seed`-populates + perturbation guards catching the
`with`-rebuild's silent revert. Slice 5 eliminates `Seed` and the `with`-rebuild, so part of the net is
superseded by the compiler and the rest must be retargeted — nothing is silently dropped. The whole #118
thesis lands here: after this slice, a dropped or miswired field in any draft is a compile error or a red
test, enumerated per failure mode:

| # | Failure mode | Before slice 5: caught by | After slice 5: caught by |
|---|---|---|---|
| 1 | Field omitted from a draft's `Build()` (or group omitted from `BuildRecord`) | perturbation guard at runtime (the `with`-rebuild silently reverted it) | **compile error CS7036** — positional constructors |
| 2 | Draft property has no initializer (unseeded → type default) | `Seed_PopulatesEverySurfacedProperty` | per-draft round-trip test red (`default(Color)`/null ≠ distinct fixture value) |
| 3 | Draft initializer reads the WRONG source field (seed cross-wire) | `Seed_PopulatesEverySurfacedProperty` | per-draft round-trip test red (built group ≠ source group, distinct fixture) |
| 4 | `Build()` passes the wrong draft property into a positional slot (build cross-wire, same type) | `BuildRecord_PerturbingEachProperty` | adapted perturbation guard red (perturbed leaf changes the wrong record leaf); round-trip also red unless the swap is symmetric |
| 5 | Symmetric double swap (initializers AND `Build` swap the same pair — round-trip is clean) | perturbation guard caught the Build side | adapted perturbation guard red on the Build side; a correct Build direction + clean round-trip then implies a correct seed direction |
| 6 | bool↔bool seed cross-wire between two same-valued italics | structurally invisible (documented slice-1 residual: all five fixture italics are `true`) | **still invisible to the round-trip — residual persists, unchanged.** The Build direction stays pinned by the perturbation guard; the seed side now sits one line from its source field in a ≤14-line class instead of a 77-line `Seed`, so review catches it. Accepted again, documented again |
| 7 | Parent wires the wrong group record into a draft (e.g. `source.DisabledCells` into the `ReadOnlyCells` slot — both `DepthPalette`) | n/a (`Seed` read leaf-by-leaf) | `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly` red (whole-record equality; the two fixture palettes are distinct) |
| 8 | `Orientation` dropped or defaulted | `with`-carry + integration guard | CS7036 (root positional ctor) + the same integration guard (fixture uses non-default `ColumnsAsSteps`) |
| 9 | AXAML path stale, mistyped, or pointing at a removed property | AVLN2000 | AVLN2000 (unchanged — `x:DataType` stays) |
| 10 | AXAML binds a valid-but-wrong sibling path (e.g. `ReadOnlyCells.Depth2` on the Depth1 picker) | not caught (no UI-automation guard existed) | not caught — pre-existing residual, unchanged and out of scope |
| 11 | A leaf silently dropped from the editor surface | `SurfacedEditablePropertyCount = 77` pin | pin retargeted: sum of leaf properties across the ten draft instances == 77 (plus CS7036 in `Build` and AVLN2000 for the binding make a silent drop triply loud) |
| 12 | Mapper drop/cross-wire on the save or load side | `SaveThenLoad_DistinctFixture_PreservesEveryMappedField` | same test, untouched — it exercises the facade + mappers, not the VM |
| 13 | Re-seed leaves `CanSave` wired to discarded drafts | n/a (`Seed` mutated properties in place) | new test: after a successful `LoadAsync`, an out-of-range edit on the NEW draft flips `CanSave` false |

**What each old guard becomes:**

- `Seed_PopulatesEverySurfacedProperty_FromDistinctFixture` — **deleted, replaced** by the per-draft
  round-trip guards (rows 2–3): for each draft class, `new <Draft>(fixtureGroup).Build()` must equal
  `fixtureGroup` (record value equality — exhaustive per field because every fixture value is distinct and
  exactly representable through `decimal`). `DepthPaletteDraft` is asserted against BOTH fixture palettes.
  One test per draft class, so a red names the broken draft.
- `BuildRecord_PerturbingEachProperty_ChangesOnlyThatMappedField` — **survives, adapted**: enumerate the
  parent's draft properties by reflection (public instance properties whose type derives from
  `ReactiveObject` — exactly the ten), then each draft's public get/set leaf properties; perturb one leaf
  (same `Perturb` helper), `BuildRecord()`, assert the changed-leaf set == `{ "{Group}.{Leaf}" }` via the
  existing typed leaf walk (`ChangedRecordFields` — kept as-is), restore. The **hand-maintained 77-entry
  path map is deleted**: the record path is now derived as `{ParentProperty}.{DraftProperty}` because
  draft leaf names mirror record component names by design. The derivation is sound because a mismatched
  name cannot hide — it makes the round-trip guard's record equality fail (wrong/no field written) or the
  leaf walk report a different path. The 77 count pin moves into this test (sum over all ten drafts).
- `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly` — **survives verbatim** (row 7 + 8): still
  `CreateViewModel(Distinct()).BuildRecord() == Distinct()`; now the guard for the parent's group wiring
  and the `Orientation` carry.
- `SaveThenLoad_DistinctFixture_PreservesEveryMappedField` — **survives untouched** (lives in
  `GridStyleEditorFacadeTests`, VM-free).

**Non-vacuity is re-proven, not assumed** (Task 4): a deliberately miswired draft initializer must turn
the round-trip guard red, and a deliberately miswired `Build` slot must turn the perturbation guard red,
before the guards are trusted — same protocol as slices 3/4.

## Acceptance Evidence

- `IGridStyleEditorFacade` declares exactly `Load` and `Save`; `git grep -n "Validate" -- '*GridStyle*'`
  finds no facade validate member, call, or fake override.
- The nine draft classes exist (initializer-seed, strict positional `Build()`, leaf names mirroring the
  group records); the parent VM has zero leaf properties, no `Seed`, no `with`-rebuild, and `BuildRecord`
  is ten `Build()` calls plus the `Orientation` carry.
- `GridStyleEditorWindow.axaml` contains no flat leaf path — every editable-field binding is
  `Group.Prop`; the AXAML diff is binding-path-only (no element/layout changes); the build passes, which
  per `x:DataType` + AVLN2000 proves all 77 paths resolve.
- Guard net green and re-proven: per-draft round-trips (one per draft class, `DepthPaletteDraft` ×2),
  adapted perturbation guard with derived paths + 77-leaf count pin, `Seed_ThenBuildRecord…` verbatim,
  `SaveThenLoad_DistinctFixture…` untouched, the new re-seed `CanSave` rewiring test; scratch checks
  recorded showing a miswired initializer and a miswired `Build` slot each go red.
- The headless window tests (`GridStyleEditorWindowTests`, `GridStyleEditorWindowOwnerRoutingTests`,
  `MainWindowStyleEditorInteractionTests`) are green — the real window loads the renamed AXAML and drives
  the real save flow; the RU-culture load-error message test passes untouched.
- `Docs/architecture/grid-style-configuration.md` describes the current state only: nested record,
  `StyleColor`, mapper-resident validation, draft-based editor; no "interim", no "slice N", no
  "Known gap" section.
- `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format` clean.

## Task 1: Drop `Validate` from the facade seam (standalone green commit)

Small, self-contained, and it makes Task 3 safe: with the per-keystroke
`Validate(BuildRecord())` call gone, `BuildRecord` is no longer invoked on every edit, which is the
precondition for the drafts' strict `Build()` (no numeric null-fallback).

**Files:** `SemiStep/SemiStep.Core/Configuration/IGridStyleEditorFacade.cs`,
`GridStyleEditorFacade.cs`, `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs`,
`SemiStep/SemiStep.Tests/Core/Configuration/GridStyleEditorFacadeTests.cs`,
`SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs`.

- [x] Remove `Validate` from `IGridStyleEditorFacade` and `GridStyleEditorFacade`; update both XML docs
      (the facade seam is now `Load` + `Save`; the "stays until slice 5" comment dies with the method).
- [x] `GridStyleEditorViewModel.RecomputeCanSave` → `CanSave = NumericsInRange();` (drop the
      `_gridStyleEditorFacade.Validate(BuildRecord())` conjunct). Update the class XML doc sentence about
      `CanSave` gating. Nothing else in the VM changes in this task.
- [x] Delete `Validate_ShippedRecord_ReturnsOk` from `GridStyleEditorFacadeTests` (the guarantee it
      tested is now structural: a loaded record is valid by construction). Remove the `Validate`
      overrides from the `ThrowingFacade` and `CausedByFailingFacade` fakes in
      `GridStyleEditorViewModelTests`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`. One commit.
      (The architecture doc still mentions `Validate` until Task 4 — acceptable inside one PR.)

## Task 2: The nine draft classes + per-draft round-trip guards (standalone green commit)

The drafts compile and are fully guard-tested before anything consumes them — the switchover commit then
contains no new field-mapping code, only wiring and renames. The drafts are production-unused for one
commit inside the PR; that is deliberate (no warning results — unused public classes are clean).

**Files (create):** `SemiStep/SemiStep.UI/StyleEditor/GridStyleFontsDraft.cs`, `GridStyleLayoutDraft.cs`,
`SelectionColorsDraft.cs`, `ChangedCellColorsDraft.cs`, `DepthPaletteDraft.cs`,
`ExecutionPaletteDraft.cs`, `StatusBarStyleDraft.cs`, `ValidationPanelStyleDraft.cs`,
`ChromeColorsDraft.cs`, `DraftNumbers.cs`;
`SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleDraftTests.cs`.

- [x] Author the nine drafts per the sketch and the class table: `public sealed class <Record>Draft(<Record> source) : ReactiveObject`;
      every leaf is a `field`-backed property with `this.RaiseAndSetIfChanged` setter AND an initializer
      reading its same-named source component (`Color` via `.ToMediaColor()`, `decimal?` via `(decimal)`
      cast for `double` sources / implicit for `int`, `int`/`bool`/`string?` direct); `Build()` constructs
      the group record positionally with argument names spelled out (`Depth0: …`), colors via
      `.ToStyleColor()`, numerics via `DraftNumbers.ToInt`/`ToDouble` (strict, away-from-zero rounding as
      today), `FontFamily ?? ""`. `source` must appear only in initializers (CS9124 discipline).
- [x] `DraftNumbers`: internal static, `ToInt(decimal? value)` / `ToDouble(decimal? value)` throwing
      `InvalidOperationException` on null (message naming the guard: build called on an invalid draft).
- [x] `GridStyleDraftTests` (`[Trait("Component","UI")] [Trait("Category","Unit")]`; plain `[Fact]` — the
      drafts touch no Avalonia services, only the `Color` struct. Note: this is the first time drafts run
      outside the headless dispatcher; constructing a `ReactiveObject` and raising changes with no
      subscribers needs no scheduler, and this task's first test run confirms it — if RxApp
      initialization complains anyway, fall back to `[AvaloniaFact]`): one round-trip test per draft class,
      `new <Draft>(fixture.<Group>).Build().Should().Be(fixture.<Group>)` against
      `GridStyleOptionsTestData.Distinct()`; `DepthPaletteDraft` asserted against BOTH `ReadOnlyCells`
      and `DisabledCells` fixtures. Field-exhaustive by construction: record value equality over
      all-distinct fixture values.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green (new guards included);
      `dotnet format`. One commit.

## Task 3: The switchover — parent VM + 77 AXAML paths + guard adaptation (one atomic commit)

This is the ripple unit and it is indivisible: the AXAML paths reference the parent's draft properties,
the parent's draft properties replace the 77 leaves the old AXAML and tests bind, and the guard tests
reflect over whichever surface exists. A partial state does not compile (AVLN2000 or CS1061 either way),
exactly like the earlier slices' atomic type-flips. Production and tests reach green in the same commit.

**Files (production):** rewrite `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` (per the
parent sketch); modify `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorWindow.axaml` (the 77 renames per
the table — nothing else). `GridStyleEditorWindow.axaml.cs`, `UiDi.cs`, `MainWindowViewModel`: untouched
(VM constructor signatures unchanged).

**Files (tests):** `GridStyleEditorViewModelTests.cs` (guard adaptation + every leaf-property access),
`GridStyleEditorWindowTests.cs` (leaf setters). `GridStyleEditorWindowOwnerRoutingTests`,
`MainWindowStyleEditorInteractionTests`, `UIFixture`: construct the VM only — verify untouched. Grep the
solution for any other leaf-property access; the build gate is the ground truth.

- [x] Rewrite the parent VM: ten reactive draft properties (names = root record components); `ReplaceDrafts(options)`
      called from the constructor and from successful `LoadAsync` (builds drafts + picker sources, swaps
      the `CompositeDisposable` of `draft.Changed → RecomputeCanSave()` subscriptions, recomputes);
      `BuildRecord` = positional root construction from ten `Build()` calls + `Orientation: _source.Orientation`;
      `NumericsInRange` reads grouped paths; delete the 77 leaf properties, `Seed`, the three `Set*`
      helpers, `_seeding`, `ToInt`/`ToDouble`. Constructors, `SaveCommand`/`SaveAsync`/`ErrorMessage`/
      `LoadAsync` failure path/logging: behavior-identical.
- [x] Rename all 77 AXAML binding paths to grouped form per the table. Touch only `{Binding …}` strings;
      `x:DataType`, resources, styles, layout stay byte-identical. The build is the path check (AVLN2000).
- [x] Adapt `GridStyleEditorViewModelTests`:
      - Perturbation guard: reflect the ten draft properties (type derives from `ReactiveObject`), walk
        each draft's public get/set leaves, perturb → `BuildRecord` → changed-leaf set equals the derived
        `{Group}.{Leaf}` path → restore. Assert total leaf count == `SurfacedEditablePropertyCount` (77).
        Delete the `_viewModelToRecordPath` map and `RecordPath`; keep `Perturb`, `ChangedRecordFields`,
        `CollectChangedLeaves`, `IsLeaf` as-is.
      - Delete `Seed_PopulatesEverySurfacedProperty_FromDistinctFixture` and its `LeafValue` helper —
        replaced by Task 2's per-draft round-trips (state this in the commit message).
      - Keep `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly` verbatim (it now guards group wiring +
        orientation carry).
      - Retarget ALL remaining `viewModel.<leaf>` accesses to grouped paths (grep the solution; the
        build gate is the ground truth — no named test is implied out of scope):
        `CellFontSize`→`Fonts.CellFontSize`, `SelectionBackground`→`Selection.Background`,
        `StatusBarFontSize`→`StatusBar.FontSize`, `RowHeight`→`Layout.RowHeight`,
        `FontFamily`→`Fonts.FontFamily`, etc. Known affected (non-exhaustive): the three CanSave
        font-size theories, `CanSave_IsFalse_WhenFontSizeOutOfRange`, `CanSave_IsFalse_WhenNumericIsNull`,
        `CanSave_IsFalse_WhenRowHeightOutOfRange`, `BuildRecord_AfterEditingColorAndFontSize…`,
        `BuildRecord_RoundsFractionalStatusBarFontSize_ToNearestInt`,
        `Seed_PopulatesFontFamilyWeightAndItalic_FromRecord`,
        `BuildRecord_CarriesEditedFontFamilyWeightAndItalic`, both `ToHex_…` tests, and
        `LoadAsync_MissingConfigDir_SetsErrorMessageAndDoesNotReseed`.
      - Add the re-seed rewiring test (guard row 13): facade fake whose `Load` succeeds with a distinct
        record → `await LoadAsync()` → drafts replaced (e.g. `Fonts.HeaderFontSize` equals the loaded
        value) → set `Fonts.HeaderFontSize = 999` on the NEW draft → `CanSave` false.
- [x] Adapt `GridStyleEditorWindowTests`: `viewModel.SelectionBackground`→`viewModel.Selection.Background`,
      `viewModel.CellFontSize`→`viewModel.Fonts.CellFontSize` (both tests otherwise unchanged — they are
      the real-file save/round-trip and byte-identity proofs).
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green — including the untouched
      RU-culture error-message test, the owner-routing window tests over the renamed AXAML, and
      `SaveThenLoad_DistinctFixture…`; `dotnet format`. One commit.

## Task 4: Non-vacuity proofs + architecture-doc rewrite (closes the roadmap)

- [x] **Guard non-vacuity scratch checks (do NOT commit any break; record each result in the progress
      log):**
      - Miswire a draft initializer (`Depth0 { … } = source.Depth1.ToMediaColor();` in
        `DepthPaletteDraft`) → the per-draft round-trip test goes RED. Revert.
      - Miswire a `Build` slot (`Depth0: Depth1.ToStyleColor()`) → the adapted perturbation guard goes
        RED for that leaf (and the round-trip too). Revert.
      - Delete one argument from a draft's `Build()` → CS7036 compile error (the omission-is-compile-error
        claim, demonstrated). Revert.
      - Mistype one AXAML path (`Fonts.HeaderSizeX`) → AVLN2000 build error. Revert.
      If the initializer miswire does NOT go red, the fixture or the round-trip is broken — re-open
      Task 2, do not paper over.
- [x] Rewrite `Docs/architecture/grid-style-configuration.md` as current-state — strip ALL interim
      tenses; each item below is present in the doc today and must be individually resolved (verify none
      is missed with `grep -n "slice\|interim\|Known gap\|with.*rebuild\|Validate" Docs/architecture/grid-style-configuration.md`
      returning only intentional survivors):
      - `**Interim (slice 4).**` paragraph (~lines 66–70): fold into the load-pipeline prose — typed
        `StyleColor`, mapper-resident aggregated validation described as the design, no slice numbers.
      - `## Record shape: nested per group (interim, slice 3)` (~lines 72–86): retitle and rewrite as
        the permanent record-shape section; drop the "Colors were `string` at slice 3" history and the
        "corrected in the slice-5 doc rewrite" promise (this IS that rewrite).
      - `**Note for slice 4.**` (~lines 88–94): keep the substance (the `colors.grid_line` error key vs
        `Chrome.GridLine` record path divergence is permanent and load-bearing) but rewrite it as a
        current-state note, no slice framing.
      - The orientation section's `BuildRecord`'s-`with`-rebuild sentence (~line 149): replace with the
        positional construction carrying `_source.Orientation`.
      - The editor write-back ViewModel bullet (~lines 242–256), including its `with`-rebuild closing
        sentence: describe the draft-based editor — thin parent exposing per-group `ReactiveObject`
        drafts, initializers seed from the group records, `Build()` constructs positionally (omission is
        a compile error), AXAML binds grouped compiled-checked paths, `CanSave` is the VM-side numeric
        range checks only.
      - The facade bullet's `Validate` description (~lines 261–267): the seam is `Load`/`Save`; remove
        every `Validate` mention including "stays on the interface until slice 5".
      - Flat-path spellings the slice-3 note deferred (`GridStyleOptions.FontFamily` →
        `GridStyleOptions.Fonts.FontFamily`, etc.).
      - **Delete the `## Known gap` section** (~lines 300–309) — obsolete: since slice 4 the mapper's
        `OptionalColor` format-checks every optional-section key (selection, changed cells, grid line,
        status bar, validation panel, chrome) when present; a hand-edited invalid hex in any key now
        fails the load with a per-key error.
- [x] Update `Docs/plans/20260805-grid-style-debloat-roadmap.md` slice statuses: slices 3, 4, 5 → DONE
      (3: PR #177; 4: `grid-style-color-typing`; 5: this PR), and note #118 closes with slice 5.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`. One commit.

## Post-Completion

**This PR closes #118** — the last hand-maintained full-field list in the grid-style stack is gone.
End state across the five slices: nested positional record (compile-checked construction), typed
`StyleColor` (invalid colors unrepresentable), mapper-resident aggregated validation (per-key error
identity preserved), async save, and a draft-based editor where a dropped field is CS7036/AVLN2000 and a
miswired one is a red guard.

- Branch `grid-style-vm-split` bases on `origin/master` (slices 1–4 merged) — no stacking.
- PR body carries `Closes #118`; commit scope `grid-style` per the repo grammar.
- Known accepted residuals, documented in the guard-net section: the bool↔bool seed cross-wire between
  same-valued italics inside one draft (invisible to the round-trip, Build direction still pinned, seed
  line now adjacent to its source in a small class), and a valid-but-wrong sibling AXAML path (never
  guarded, unchanged).
- `SemiStep.Screenshots` remains excluded from the solution and its editor screenshot harness remains
  stale (pre-existing: reflection against a removed VM constructor overload). If it is ever revived, its
  `Activator.CreateInstance` call needs the logger argument — out of scope here.

**Executed by exec:**
- branch: grid-style-vm-split

## Verify it yourself

This slice is a pure structural refactor of the editor: the window looks and behaves identically, the
on-disk YAML is unchanged, and no operator-visible behaviour moves. There is no manual repro that
distinguishes before from after — the evidence is the compiler, the guard tests, and the diff.

1. **The editor still builds and every binding path resolves.** `dotnet build SemiStep.slnx` → 0 warnings,
   0 errors. The build IS the AXAML path check: Avalonia's compiled bindings (`x:DataType` + AVLN2000)
   fail the build on any of the 77 grouped `{Binding Group.Leaf}` paths that does not resolve against the
   parent's draft properties.
2. **Field omission is now a compile error, and miswiring is a red test** — the #118 thesis. Proven by the
   Task-4 non-vacuity scratch checks (recorded in the progress log, all reverted): dropping a `Build()`
   argument → CS7036; mistyping an AXAML path → AVLN2000; miswiring a draft initializer → the per-draft
   round-trip test red; miswiring a `Build()` slot → the perturbation guard red.
3. **The guard net covers all 77 leaves.** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GridStyleDraft|FullyQualifiedName~GridStyleEditorViewModelTests"`
   — the nine per-draft round-trips (`new <Draft>(fixture.Group).Build() == fixture.Group`, DepthPaletteDraft
   against both palettes) plus the perturbation guard that pins the walked leaf count at 77.
4. **Behaviour is preserved.** The untouched suites stay green: the Russian-culture malformed-hex end-to-end
   message, the owner-routing window tests over the renamed AXAML, `SaveThenLoad_DistinctFixture`, and the
   re-seed rewiring test (`LoadAsync_Success_ReplacesDraftsAndRewiresCanSave`). Facade seam is `Load`/`Save`
   only — `git grep -n "Validate" -- SemiStep/SemiStep.Core/Configuration/IGridStyleEditorFacade.cs` is empty.
5. **Full gate:** full `dotnet test` → 1727 passed; `dotnet format` clean. (One unrelated GC-sensitive
   allocation probe, `RecipeGridSurfaceContractTests.AppendStep_PerSurfaceTailAllocation…`, flaked on one
   review run — outside this branch's surface; re-run if CI trips it.)
6. **Manual smoke (optional):** open the style editor, edit a value in each group (font size, a color,
   orientation, a status-bar timer weight), save, reopen — every value persists and the window is visually
   unchanged from before the split.
