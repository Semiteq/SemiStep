# Slice 1 — Grid-style editor: anti-regression guards before the debloat

## Overview

Issue #118's debloat of the grid-style pipeline (nest the flat `GridStyleOptions` record, type colors as a value type,
split the editor view model into per-group drafts) is a multi-slice mechanical refactor that will rename ~120 member
accesses and rewrite the editor's `Seed`/`BuildRecord` blocks. Those blocks carry a **silent-field-drop** bug with no
compile or test guard: `GridStyleEditorViewModel.BuildRecord` rebuilds the record with `_source with { …surfaced fields… }`,
so a field surfaced in `Seed` but **missing from `BuildRecord`** compiles fine and silently reverts that field to the
loaded value on save. The three parallel blocks (property declarations, `Seed`, `BuildRecord`) sit 200+ lines apart in a
523-line file, so review cannot see a mismatch either.

**This slice lands the safety net first** — tests only, no production change — so every later structural slice is
protected against a mechanical omission. It must ship before any nest/type/split slice.

**The existing round-trip test is false confidence** on two counts, and a naive replacement inherits a third blind spot:
1. `GridStyleEditorViewModelTests.Seed_RoundTripsShippedHexValues_Losslessly` seeds `GridStyleOptions.Default`, whose 32
   colors are all `#000000` — duplicate values mask cross-wiring (a `Seed` line reading field A into property B passes as
   long as A and B share a value).
2. **A `BuildRecord(Seed(x)) == x` round-trip can NEVER fail on a dropped `BuildRecord` line** — `with` keeps the
   `_source` value, and `_source` *is* the seed source `x`, so the dropped field still equals `x`.
3. **It also can't fail on a dropped `Seed` line for the 14 nullable-fallback fields** (the 13 `decimal?` numerics +
   `FontFamily`): a dropped `Seed` line leaves the property `null`, and `BuildRecord`'s `ToInt(null, _source.X)` /
   `FontFamily ?? _source.FontFamily` restores the source value, so the round-trip stays green. (Color/`int`/`bool`
   fields have no null fallback — a dropped `Seed` line there yields a type default that the distinct round-trip does
   catch.)

So the guards are split by **direction and field kind**, each catching what the others structurally cannot:

| Guard | Catches | Structural blind spot (covered by another guard) |
|---|---|---|
| **Seed-populates** — after `Seed(distinct)`, assert **each** editable property equals the fixture's mapped value | every `Seed` omission (nullable fields → still null; italics → still `false` ≠ fixture `true`) and every non-bool `Seed` cross-wire | the `BuildRecord` direction; a bool↔bool `Seed` cross-wire (inherent to 5 two-valued fields) |
| **Perturbation (exact-one-field)** — seed, then change one property *from its current value*, assert `BuildRecord()` equals the baseline in **every field except that property's mapped field** | every `BuildRecord` drop, mis-target (writes to the wrong field), and same-valued-bool swap | the `Seed` direction |
| **Save→load mapping** — `GridStyleWriter.Save` → `GridStyleLoader.LoadAsync` → `GridStyleMapper.Map` == distinct | `GridStyleDtoMapper` / `GridStyleMapper` omissions (the flatten/unflatten layer the nest slice rewrites) | the editor layer |

A distinct-value `BuildRecord(Seed(distinct)) == distinct` round-trip is kept as an integration sanity check, but it is
**not** load-bearing — the two directional guards above are what actually bite. Together they make a silently-dropped or
mis-wired field impossible across the whole config↔editor pipeline without a red test.

**Counts (verified):** 77 surfaced editable properties — 13 `decimal?` numerics, 1 `FontFamily`, 5 `int` weights, 5
`bool` italics, 53 `Color`s; the record has 78 positional fields (77 surfaced + the unsurfaced `Orientation`). The
property setters are public (via the private `SetColor`/`SetNumber`/`SetValue` helpers); `ErrorMessage`/`CanSave`/
`AvailableFontFamilies`/`AvailableFontWeights` are private-set; `SaveCommand` is get-only.

**Scope:** tests + one shared test-data builder only. No production file changes. Async `Save`, the `HexColor.Parse`
drop, the record nesting, the `StyleColor` typing, and the VM draft split are later slices.

## Acceptance Evidence

- `GridStyleOptionsTestData.Distinct()` returns a `GridStyleOptions` with every surfaced field distinct, valid, and
  exactly-representable: distinct **UPPERCASE** opaque `#RRGGBB` colors (equality is string equality; `ToHex` emits
  `X2`), distinct integer/clean-half numerics in-range, distinct weights, all five italics `true`, a non-empty `FontFamily`,
  and `Orientation` = the non-default value.
- The Seed-populates test fails red if any single `Seed` line is removed (incl. a nullable field).
- The perturbation test fails red if any single `BuildRecord` line is removed — verified by a scratch check, reverted.
- The save→load mapping round-trip fails red if a `GridStyleDtoMapper` line is dropped (a dropped field serializes absent → defaults substitute → differs) or a `GridStyleMapper` line mis-wires. (A dropped `GridStyleMapper` line is a compile error — its 78-arg positional `new GridStyleOptions(...)` won't build — an even stronger guard.)
- `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format` clean.

## Task 1: Distinct-value fixture + Seed-populates + value round-trips

**Files:**
- Create: `SemiStep/SemiStep.Tests/Helpers/GridStyleOptionsTestData.cs` (`public static GridStyleOptions Distinct()`).
- Modify: `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs` (replace `Seed_RoundTripsShippedHexValues_Losslessly`; add the Seed-populates test).
- Modify: `SemiStep/SemiStep.Tests/Core/Configuration/GridStyleEditorFacadeTests.cs` (add the distinct-value save→load mapping round-trip alongside the existing shipped-config `SaveThenLoad_RoundTrips`).

- [x] `GridStyleOptionsTestData.Distinct()`: a hand-written `new GridStyleOptions(...)` with **every** field distinct and valid. Colors: 53 mutually-distinct **uppercase** opaque `#RRGGBB` literals (mutual distinctness is required so the round-trip/Seed-populates expose swaps; e.g. a sequential palette `#010101`, `#020202`, … kept uppercase). Numerics: distinct, in-range per the VM `Min*/Max*` consts, and exactly representable in both `double` and `decimal` (integers or clean halves — the `int`↔`decimal` via `ToInt`'s `Round` and the `double`↔`decimal` via `ToDouble`'s cast must be exact). Weights: distinct ints (e.g. 300/400/500/600/700). Italics: **all 5 `true`** — `bool` is the one kind whose type default (`false`) can equal a fixture value, so a fixture `false` would let a dropped `Seed` line for that italic stay green across every guard; setting all italics `true` makes each differ from the `false` default, so a dropped `Seed` line → `false ≠ true` → red. (Residual limitation to state honestly: a bool↔bool `Seed` cross-wire — a line reading the wrong italic — stays undetectable, inherent to a 2-valued type with 5 instances, and a lower-probability mechanical error than a dropped line.) `FontFamily`: a fixed non-empty string. `Orientation`: `GridOrientation.ColumnsAsSteps` (non-default — proves `BuildRecord` preserves the unsurfaced field via `with`).
- [x] **Seed-populates test** — the `Seed`-direction guard (the one that catches dropped `Seed` lines for the nullable + bool fields). Seed the VM from `Distinct()`, then assert **each** surfaced property equals the fixture's corresponding value: colors as `HexColor.Parse(fixture.<X>Color)`, weights/italics/`FontFamily` directly, numerics via `Convert.ToDecimal(...)` on both sides (the record's `int` font sizes and `double` paddings box under reflection — a direct `(decimal)` cast on a boxed `int` throws `InvalidCastException`; `Convert.ToDecimal` handles both). Drive it by the same public-get+public-set reflection enumeration Task 2 uses, with the VM-property→record-field name map (colors: property name + `"Color"`; everything else: identical name), so a dropped or cross-wired `Seed` line makes exactly that property mismatch → red. (A dropped nullable `Seed` line leaves the property `null` ≠ the fixture value → red; a dropped italic `Seed` line leaves it `false` ≠ the fixture `true` → red.)
- [x] **Distinct-value BuildRecord round-trip** (integration sanity): construct the VM seeded from `Distinct()`, assert `viewModel.BuildRecord().Should().Be(GridStyleOptionsTestData.Distinct())`. Replace the `Default`-based `Seed_RoundTripsShippedHexValues_Losslessly` (all-`#000000` colors mask cross-wires). Name it for what it proves (e.g. `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly`).
- [x] **Save→load mapping round-trip** (Core config tests): drive it through the **facade** — `facade.Save(tempDir, Distinct())` then `facade.Load(tempDir)`, assert the loaded value `== Distinct()` (mirror the existing `SaveThenLoad_RoundTrips` harness; `GridStyleWriter.Save` is an instance method, and the facade path additionally runs `GridStyleValidator` in the real load pipeline — strictly more coverage, less internal reach). This exercises `GridStyleDtoMapper` (options→dto) + YAML serialize/deserialize + `GridStyleMapper` (dto→options) over distinct values — the layer the nest slice rewrites. Complements (does not duplicate) `SaveThenLoad_RoundTrips`, which round-trips the *shipped* config whose duplicated/default-equal values mask cross-wires and default-substitution. Keep both. (`TempDirectory` lives in `SemiStep.Tests.Config.Helpers` — add the `using`; decide the new fixture's namespace deliberately.)
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Perturbation completeness test (the BuildRecord guard)

The `BuildRecord`-direction guard. Seed, then change each editable property **from its current (seeded) value** and
assert the built record changed in **exactly** that property's mapped field.

**Files:** `SemiStep/SemiStep.Tests/UI/StyleEditor/GridStyleEditorViewModelTests.cs` (may share the reflection helper + name map with Task 1's Seed-populates test).

- [x] Enumerate the surfaced editable properties by reflection: `typeof(GridStyleEditorViewModel).GetProperties()` where `GetMethod?.IsPublic == true && SetMethod?.IsPublic == true`. This selects exactly the 77 style fields and excludes `ErrorMessage`/`CanSave`/`AvailableFontFamilies`/`AvailableFontWeights` (private set), `SaveCommand` (get-only), and inherited `ReactiveObject` members (get-only). **Assert the count == 77** (drift guard); if a non-style public-set property is ever added, exclude it by name with a comment. (Reused the `EditableProperties()`/`RecordField()` helpers from Task 1.)
- [x] For each property: seed the VM from `Distinct()`, capture `baseline = BuildRecord()`, set the property to a value **derived from its current value** and distinct from it (`Color` → flip one channel `Color.FromArgb(255, (byte)(c.R ^ 1), c.G, c.B)`, opaque so `ToHex` changes; `decimal?` → `current + 1`; `int` weight → a different valid weight; `bool` → `!current`; `string` `FontFamily` → current + a suffix), then `built = BuildRecord()`. **Assert exactly the mapped record field changed:** the set of `GridStyleOptions` properties where `built` differs from `baseline` (compared by reflection over the record's properties) equals `{ mappedField }`, where `mappedField` is the property name + `"Color"` for colors, else the property name. This catches a dropped line (zero fields differ → red), a mis-target (a different field differs → red), and — because it pins the exact field — a same-valued-bool swap that a plain `NotBe(baseline)` would miss. Report the property name in the assertion message. **Restore the property to its seeded value between iterations** (cheaper than reconstructing the VM 77× — each ctor enumerates system fonts).
- [x] **Non-vacuity check (do NOT commit the break):** temporarily delete one `BuildRecord` assignment line, confirm the perturbation test goes red for exactly that property; delete one nullable (`decimal?`/`FontFamily`) `Seed` line AND one italic (`bool`) `Seed` line, confirm the Seed-populates test goes red for each. Revert all. State the results in the progress log. (All three verified red — dropped `InfoColor` BuildRecord line → perturbation red naming Info; dropped `ValidationPanelMaxHeight` Seed line → Seed-populates red; dropped `StatusBarTimerValueItalic` Seed line → Seed-populates red. All reverted; tree clean of production edits.)
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next slices of #118 (each its own PR):** (2) async `Save` — `IGridStyleEditorFacade.Save` → `Task<Result>`,
`ReactiveCommand.CreateFromTask`; (3) nest `GridStyleOptions` to mirror the DTO groups (+ a shared `DepthPalette` for the
identical ReadOnly/Disabled blocks), rewiring the two mappers and the ~10 runtime consumers under these guards; (4) type
colors as a `StyleColor` value type, fold the color-validation into the load mapper, delete `HexColor`; (5) split the VM
into per-group drafts (property-initializer = seed, positional `Build()` = compile guard), grouped compiled-binding AXAML
paths, reduce `CanSave` to numeric ranges. This slice is the anti-regression net all of those lean on.

**Executed by exec:**
- branch: grid-style-guards

## Verify it yourself

This is a tests-only safety net — there is no behavior to click through. Verify the guards exist and genuinely bite:

1. **The guards run green against the current (correct) code:**
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GridStyleEditorViewModelTests|FullyQualifiedName~GridStyleEditorFacadeTests"` — `Seed_PopulatesEverySurfacedProperty_FromDistinctFixture`, `BuildRecord_PerturbingEachProperty_ChangesOnlyThatMappedField` (count asserts 77), `Seed_ThenBuildRecord_PreservesEveryFieldDistinctly`, and `SaveThenLoad_DistinctFixture_PreservesEveryMappedField` all pass.
2. **The guards bite (each catches its target silent-drop)** — proven during exec by scratch checks (delete a line, confirm red, revert), recorded in the progress log:
   - delete a `BuildRecord` assignment (e.g. `InfoColor`) → the perturbation test goes red, naming the field.
   - delete a nullable `Seed` line (e.g. `ValidationPanelMaxHeight`) → the Seed-populates test goes red (found 0 vs 150.5).
   - delete an italic `Seed` line → the Seed-populates test goes red (false vs true).
   - delete a `GridStyleDtoMapper` line (e.g. `CellWeight`) → the facade save→load test goes red (loaded 400 vs fixture 800). Revert each.
3. **Whole suite:** `dotnet build SemiStep.slnx` (0 warnings) and `dotnet test` (1687 passed, 0 failed). No production file changed — `git diff master...HEAD --name-only` lists only `SemiStep.Tests/**` and the plan.
