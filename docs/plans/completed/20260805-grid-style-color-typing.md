# Slice 4 — Type colors as StyleColor, fold validation into the load mapper

## Overview

Slice 4 of the #118 debloat: replace the 53 raw `string` hex fields (spread over the slice-3 group records) with a
typed `StyleColor` value in Core, move hex parsing and per-key error accumulation into `GridStyleMapper`, and delete
`GridStyleValidator` (243 lines) plus the editor's `HexColor` edge (27 lines). After this slice a color is channels
everywhere; hex exists only at the two I/O edges — the load mapper parses, the save mapper formats — and an invalid
color is **unrepresentable** in `GridStyleOptions`.

**The YAML format and the DTOs do not change.** Every `GridStyle*Dto` keeps its `string` properties, so the
serialized key set and value shape on disk are identical. One behavior note: hex **case** normalizes to uppercase on
save (`StyleColor` equality is channel equality; `ToString` emits `X2`). Acceptable — the editor already rewrites the
whole file, and the writer preserves the leading comment block, not the value casing.

**Locked decisions — settled, do not re-open:**
- `StyleColor` is a `readonly record struct` in `SemiStep.Core` (Avalonia-free), channels `A`/`R`/`G`/`B` (`byte`
  each), with `Parse`/`TryParse`/`ToString`. `ToString` absorbs `HexColor.ToHex`'s round-trip rule: opaque
  (`A == 0xFF`) → `#RRGGBB` uppercase; translucent → `#AARRGGBB` uppercase — never the `#FF`-prefixed form
  `Avalonia.Media.Color.ToString()` injects.
- `Parse` accepts ONLY `#RRGGBB` (6 hex digits) and `#AARRGGBB` (8 hex digits) — exactly today's validator regex
  `^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$`. It REJECTS `#RGB`/`#ARGB` shorthand. The shipped file headers that advertise
  `#RGB`/`#ARGB` are wrong today (the validator already rejects them) and get corrected in Task 4.
- `IGridStyleEditorFacade.Validate` STAYS on the interface as a vacuous pass-through (`Result.Ok()`). Do NOT remove
  it — slice 5 trims the interface when it splits the VM. The VM's `RecomputeCanSave` keeps calling it unchanged.

**The hard part is error-identity fidelity** (see the dedicated section below): the load mapper must aggregate ALL
errors per load — not fail-fast — keyed by the exact same section path + key name the validator emits today, so
`ReasonLocalizer` output and the existing grid-style error tests survive byte-for-byte.

**Stacking dependency:** this slice builds on the slice-3 end state (branch `grid-style-nest-record`, PR #177,
unmerged). Exec branches `grid-style-color-typing` off `grid-style-nest-record`, NOT off `master`; rebase onto
`origin/master` once #177 merges.

## The target shape

### StyleColor (new, Core)

`SemiStep/SemiStep.Core/Configuration/StyleColor.cs`:

```csharp
public readonly record struct StyleColor(byte A, byte R, byte G, byte B)
{
	public static StyleColor Parse(string value);            // throws FormatException on reject
	public static bool TryParse(string? value, out StyleColor color);
	public override string ToString() =>
		A == byte.MaxValue
			? $"#{R:X2}{G:X2}{B:X2}"
			: $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
```

`TryParse` semantics mirror the validator regex exactly: `#` + 6 hex digits (→ `A = 0xFF`) or `#` + 8 hex digits
(`AARRGGBB`), any letter case on input; everything else — including null, whitespace, `#RGB`, `#ARGB`, missing `#`,
wrong length — returns `false`. No allocation beyond the `ToString` string.

### The 53 fields that flip `string` → `StyleColor`

| Group record | Color fields flipping | Count |
|---|---|---|
| `SelectionColors` | Background, Foreground | 2 |
| `ChangedCellColors` | Changed, ChangedSelected | 2 |
| `DepthPalette` (used ×2: ReadOnlyCells, DisabledCells) | Depth0–3, Depth0Past–3Past, Selected, Foreground | 10 (×2 uses) |
| `ExecutionPalette` | Depth0–3, Depth0Past–3Past, CurrentStepMarker | 9 |
| `StatusBarStyle` | Background, Foreground (numerics/weights/italics stay) | 2 |
| `ValidationPanelStyle` | Background, Foreground, ErrorColor, WarningColor (MaxHeight stays) | 4 |
| `ChromeColors` | all 14 incl. GridLine | 14 |

2+2+10+10+9+2+4+14 = 53. `GridStyleFonts`, `GridStyleLayout`, and `Orientation` are untouched.

### The load mapper's aggregated-validation shape (end state, Task 3)

`GridStyleMapper.Map` changes signature to `Result<GridStyleOptions> Map(GridStyleOptionsDto? dto)` and absorbs the
validator wholesale:

```csharp
if (dto is null)        return Result.Fail(new GridStyleConfigMissingError());       // was: return Default (dead in production — validator gated it)
if (dto.Colors is null) return Result.Fail(new GridStyleSectionMissingError("colors")); // fail-fast HERE only, matching today
var errors = new List<IError>();
// parse phase — per-key helpers accumulate into `errors`, in the validator's emission order:
//   execution → readonly → disabled → chrome → selection → cells.changed/changed_selected →
//   colors.grid_line → status_bar → validation_panel → orientation
...
if (errors.Count > 0) return Result.Fail(errors);
return Result.Ok(new GridStyleOptions(...));   // positional per-group construction from the parsed locals
```

Two helper shapes carry the whole per-key policy:

- `RequiredColor(string? value, string section, string key, List<IError> errors)` — for the three cell palettes.
  Null or whitespace → `GridStyleKeyMissingError(section, key)`; `TryParse` failure → `GridStyleHexColorInvalidError
  (section, key, value)`; on error returns `default(StyleColor)` (a placeholder — the whole result is discarded when
  `errors` is non-empty). The three required sections are tested by INDEPENDENT null-propagating reads —
  `dto.Colors.Cells?.Execution`, `?.ReadOnly`, `?.Disabled` — each null read adds its own
  `GridStyleSectionMissingError` (`colors.cells.execution` / `.readonly` / `.disabled`), skips that section's keys,
  and keeps checking the others. There is NO `colors.cells` section error: a null `Cells` nulls all three reads and
  therefore emits exactly THREE section-missing errors, matching the validator (which never checks `Cells` itself —
  the `"colors.cells"` path appears only in the optional changed/changed_selected check, which skips silently on a
  null `Cells`).
- `OptionalColor(string? value, StyleColor fallback, string section, string key, List<IError> errors)` — for
  chrome / selection / changed-cells / grid_line / status_bar / validation_panel keys. Null → `fallback` silently
  (today's `?? defaults` regime); present-but-whitespace → `GridStyleKeyMissingError`; `TryParse` failure →
  `GridStyleHexColorInvalidError`; valid → parsed. An optional section that is null → all its fallbacks, no error.

The save side is trivial: `GridStyleDtoMapper` writes `options.<Group>.<Field>.ToString()` into each string DTO
property — 53 `.ToString()` suffixes, no other change, YAML output shape identical.

### Everything else that moves

| Artifact | Change |
|---|---|
| `GridStyleOptions.Default` + group records | color params/args become `StyleColor` (`StyleColor.Parse("#CCE4F7")` literals — valid by construction, exercised by tests) |
| `Validation/GridStyleValidator.cs` | DELETED (Task 3) |
| `Facade/ConfigFacade.cs` | the `GridStyleValidator.Validate(gridStyle)` call at the same pipeline position becomes `GridStyleMapper.Map(gridStyle)`; `MapToDomain` receives the already-mapped `GridStyleOptions` instead of the DTO — grid-style errors keep propagating BEFORE cross-reference validation, exactly as today |
| `GridStyleEditorFacade` | `Load` = loader → `Map` (Result); `Validate` = `Result.Ok()` vacuous with a "slice 5 removes this" comment; `Save` drops its pre-write validation gate (invalid colors are unrepresentable); XML doc updated |
| `Styles/PaletteBrushFactory` | `From(StyleColor)` — `new SolidColorBrush(color.ToMediaColor())` via the shared conversion below, no parse |
| `StyleEditor/HexColor.cs` | DELETED; replaced by `Styles/StyleColorConversions.cs` — channel-wise extensions `ToMediaColor(this StyleColor)` / `ToStyleColor(this Avalonia.Media.Color)`, used by the VM's `Seed`/`BuildRecord` and by `PaletteBrushFactory` |
| `Dto/GridOrientationValues.cs` + `GridStyleMapper` comments | the "validator runs before this" comments are rewritten/removed — the precondition no longer exists |

## Error-identity fidelity — the central risk

The validator is the ONLY producer of five typed errors that `ReasonLocalizer` localizes per type + fields and that
three test suites pin. The mapper must reproduce every one with the identical section path, key name, and constructor
arguments — the error `Message` strings are built in the error-type constructors (unchanged files), so identical
arguments ⇒ byte-identical messages ⇒ byte-identical localized output.

| # | Error emitted today | Validator trigger | Identity carried | Load-mapper reproduction |
|---|---|---|---|---|
| 1 | `GridStyleConfigMissingError` | `dto is null` | — | early `return Fail` — replaces the mapper's current `return GridStyleOptions.Default` null-branch, which was unreachable in production (the validator gated both load paths); `GridStyleMapperTests.Map_NullDto_ReturnsDefaults` retargets to assert this failure |
| 2 | `GridStyleSectionMissingError("colors")` | `dto.Colors is null` | section `colors` | early `return Fail` with ONLY this error — today the validator returns immediately without checking chrome/status_bar/orientation; preserve that fail-fast at this one level |
| 3 | `GridStyleSectionMissingError(path)` | `Cells?.Execution` / `?.ReadOnly` / `?.Disabled` is null — three INDEPENDENT null-propagating reads; a null `Cells` trips all three (three errors, never one `colors.cells`) | `colors.cells.execution` / `colors.cells.readonly` / `colors.cells.disabled` | required-section handling: add the error per null read, skip that section's keys, CONTINUE aggregating the other sections and orientation |
| 4 | `GridStyleKeyMissingError(section, key)` | required cell-palette key null/whitespace; optional-section key present-but-whitespace | e.g. (`colors.cells.readonly`, `depth_0`) — snake_case key names exactly as the validator's tuple lists | `RequiredColor`: null OR whitespace → this error. `OptionalColor`: null → silent fallback (today's `??` regime), whitespace → this error |
| 5 | `GridStyleHexColorInvalidError(section, key, value)` | non-whitespace value fails the 6/8-hex regex | section + key + offending value verbatim | `TryParse` failure on non-whitespace input → same triple |
| 6 | `GridStyleOrientationInvalidError(value, rows, columns)` | orientation non-null and unknown | value + both expected literals | checked in the aggregate BEFORE `GridOrientationValues.Parse` runs (Parse throws on unknown — it must never see an invalid value) |

Full optional-key inventory the mapper must format-check when present (from the validator's tuple lists — carry the
snake_case names verbatim): `chrome.{info, connected, disconnected, local_mode, connecting, panel_background,
panel_header_background, subtle_border, separator, secondary_foreground, grid_border, grid_background,
header_foreground}`, `colors.selection.{background, foreground}`, `colors.cells.{changed, changed_selected}`,
`colors.grid_line` (record path `Chrome.GridLine` — the error key stays `colors.grid_line`, per the slice-3 doc
note), `status_bar.{background, foreground}`, `validation_panel.{background, foreground, error_color,
warning_color}`.

**The `!`-chain trap (spelled out).** Today `GridStyleMapper` reads the three cell palettes through a no-fallback
null-forgiving chain — `dto.Colors!.Cells!.ReadOnly!.Depth0!` — with the comment "presence guaranteed by
GridStyleValidator, which runs before this mapper". The moment the validator is deleted, that presumption is FALSE:
a file with a missing `readonly:` section would throw `NullReferenceException` inside the mapper instead of failing
with `GridStyleSectionMissingError`. The mapper itself must now produce the missing-section and missing-key errors
that used to be its own precondition — every `!` in those three palette blocks becomes a real aggregated check
(`Cells?.ReadOnly` null-test + `RequiredColor` per key). No `!` on a color path survives Task 3. Do NOT "fix" this by
adding `?? default` fallbacks — that would silently swap a load failure for a `#000000` placeholder, exactly the
regression the slice-1 guards exist to prevent.

**Aggregation, not fail-fast.** `Validate_MultipleErrors_AllCollected` pins that three bad keys in one DTO produce
three errors in one result. The mapper's parse phase must therefore run to completion (below the `colors` early-out)
before deciding success. Preserve the validator's emission order (the table order above); no test pins the order, but
the VM joins errors with `"; "` into one user-visible message, so keep it stable rather than re-sorting by group.

**The end-to-end pin.** `GridStyleEditorViewModelTests.LoadAsync_MalformedHexColor_UnderRussianCulture_
RendersRussianErrorMessage` loads a real file with `changed_selected: "zzz"` through the real facade and asserts the
full Russian message string. It must pass UNTOUCHED after Task 3 — it is the byte-for-byte proof that file → loader →
mapper-resident validation → `ReasonLocalizer` → VM produces the identical error.

## Acceptance Evidence

- `StyleColor` exists in Core with the locked semantics; `SemiStep.Core` still has no Avalonia reference (csproj
  untouched); dedicated `StyleColorTests` cover parse/reject/round-trip incl. `#RGB`/`#ARGB` rejection and the
  opaque-6/translucent-8 `ToString` rule.
- All 53 color fields are `StyleColor`; `grep -r "string.*Color\|Color.*string"` over the group records finds no
  string-typed color; the solution compiles with zero references to `HexColor` or `GridStyleValidator`.
- Slice 1's guard net holds over typed colors: `Seed_PopulatesEverySurfacedProperty…`,
  `BuildRecord_PerturbingEachProperty…`, `SaveThenLoad_DistinctFixture…` green with the fixture retargeted to
  `StyleColor` (same 53 distinct valid values — distinctness now by channel equality, same hex ⇒ same distinctness).
- The error-identity net is green with assertion bodies unchanged: `GridStyleColorsValidationTests` (entry point
  retargeted `GridStyleValidator.Validate` → `GridStyleMapper.Map`, every message assertion byte-identical),
  `ConfigFacadeGridStyleValidationTests`, `GridStyleOrientationTests`, and the untouched RU-culture end-to-end
  message test.
- The two facade tests whose invalid record is now unrepresentable (`Validate_MalformedColor_ReturnsFail`,
  `Save_InvalidRecord_FailsValidationGuardBeforeWriting`) are deleted with a commit-message note — the compiler now
  provides the guarantee they tested.
- On-disk YAML: same keys, same shape; shipped configs load unchanged; saving normalizes only value case to
  uppercase. The MBE/MOCVD file headers no longer advertise `#RGB`/`#ARGB`.
- `dotnet build SemiStep.slnx` 0 warnings (warnings are errors); full `dotnet test` green; `dotnet format` clean.

## Task 1: StyleColor value type in Core, with unit tests

Self-contained — nothing references the type yet, so this commit is trivially green.

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/StyleColor.cs` (BOM, file-scoped namespace
  `SemiStep.Core.Configuration`).
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/StyleColorTests.cs`
  (`[Trait("Component","Config")] [Trait("Category","Unit")]`).

- [x] Author `StyleColor` per the sketch: `readonly record struct` with `byte A, R, G, B`; `TryParse` accepting
      exactly `#` + 6 or 8 hex digits (case-insensitive input, 6-digit → `A = 0xFF`), false for null/whitespace/
      shorthand/anything else; `Parse` delegating to `TryParse` and throwing `FormatException` naming the input on
      reject; `ToString` emitting `#RRGGBB` when opaque, `#AARRGGBB` otherwise, uppercase `X2` — carry over
      `HexColor`'s doc-comment rationale (never the `#FF`-injected `Color.ToString()` form).
- [x] Tests: parse 6-digit sets `A=0xFF`; parse 8-digit maps `AARRGGBB` channel order; lowercase input parses and
      `ToString` round-trips to uppercase; opaque → 6-digit and translucent → 8-digit `ToString`; rejects `#FFF`,
      `#0FFF`, `FFFFFF`, `#12345`, `#FFFFFFFFF`, `""`, `"  "`, null (TryParse false, Parse throws); value equality
      (`Parse("#aabbcc") == Parse("#AABBCC")`).
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`. One commit.

## Task 2: Flip the 53 color fields to StyleColor — validator still standing (one atomic commit)

The type flip ripples until nothing references a string color — production AND tests reach green in the same commit
(like slice 3's rewrite). **The validator is NOT touched here**: it operates on the DTO (still strings) and still
gates both load paths, so the interim mapper may parse with throwing `StyleColor.Parse` under the existing
validator-guarantees. This boundary is deliberate: Task 2 is mechanical typing proven lossless by the slice-1 guards,
while every error test stays untouched-and-green — which then makes those tests the unchanged fidelity net for
Task 3's validation move. A partial state inside this task does not compile; the two-task split states each compile.

**Files (production):**
- Modify group records: `SelectionColors.cs`, `ChangedCellColors.cs`, `DepthPalette.cs`, `ExecutionPalette.cs`,
  `StatusBarStyle.cs` (2 fields), `ValidationPanelStyle.cs` (4 fields), `ChromeColors.cs` — `string` → `StyleColor`
  per the table.
- Modify: `GridStyleOptions.cs` (`Default` color literals → `StyleColor.Parse("…")`).
- Modify: `Mapping/GridStyleMapper.cs` — required palettes: `StyleColor.Parse(readOnlyCells.Depth0!)` (keep the
  validator-precondition comment for now; Task 3 removes it); optional fields: `dto.X is null ?
  defaults.<Group>.<Field> : StyleColor.Parse(dto.X)` replacing `?? defaults`. Signature still returns plain
  `GridStyleOptions`.
- Modify: `Mapping/GridStyleDtoMapper.cs` — append `.ToString()` on all 53 color reads; DTOs untouched.
- Create: `SemiStep/SemiStep.UI/Styles/StyleColorConversions.cs` — `ToMediaColor(this StyleColor)` /
  `ToStyleColor(this Avalonia.Media.Color)`, channel-wise.
- Modify: `Styles/PaletteBrushFactory.cs` — `From(StyleColor)` returning
  `new SolidColorBrush(color.ToMediaColor())`, no parse.
- Modify: `StyleEditor/GridStyleEditorViewModel.cs` — `Seed`: `HexColor.Parse(options.X)` →
  `options.X.ToMediaColor()`; `BuildRecord`: `HexColor.ToHex(Prop)` → `Prop.ToStyleColor()`. Properties, commands,
  `RecomputeCanSave`, `NumericsInRange` unchanged.
- Delete: `StyleEditor/HexColor.cs`.

**Files (tests — starting list; the build gate is the ground truth, grep the solution for the remaining string-color
constructions):**
- `Helpers/GridStyleOptionsTestData.cs` — the 53 fixture colors → `StyleColor.Parse("#010101")` etc., same values.
- `UI/StyleEditor/GridStyleEditorViewModelTests.cs`:
  - Seed guard: `HexColor.Parse((string)recordValue!)` → `((StyleColor)recordValue!).ToMediaColor()`.
  - **`IsLeaf` must add `type == typeof(StyleColor)`** — without it the perturbation leaf walk recurses into
    `A`/`R`/`G`/`B` and reports paths like `Selection.Background.R`, breaking the exact-one-leaf assertion. This is
    the guard-net adjustment of this slice; the non-vacuity re-proof is in Task 3.
  - `BuildRecord_AfterEditingColorAndFontSize…`: `Background = "#123456"` → `StyleColor.Parse("#123456")`.
  - `ToHex_PreservesSixDigitForm…` / `ToHex_EmitsEightDigitForm…`: assert `…Selection.Background.ToString()` equals
    the 6/8-digit literal — same intent, the round-trip rule now lives on `StyleColor.ToString`.
- `Core/Configuration/GridStyleMapperTests.cs` — color assertions wrap literals in `StyleColor.Parse(…)`.
- `Core/Configuration/GridStyleEditorFacadeTests.cs` — line-30 style string assertions become
  `….ToString().Should().StartWith("#")`; DELETE `Validate_MalformedColor_ReturnsFail` and
  `Save_InvalidRecord_FailsValidationGuardBeforeWriting` — a record with `Depth0 = "not-a-color"` no longer compiles;
  the compiler now provides this guarantee.
- `Core/Configuration/ConfigFacadeGridStyleValidationTests.cs` — `GridStyle.Execution.Depth0.Should().StartWith("#")`
  → `.ToString().Should().StartWith("#")`.
- Other record-constructing tests (compiler-driven): `GridStyleEditorWindowTests`, `CellPaletteInstallerTests`,
  `ExecutionPaletteInstallerTests`, `TransposedCellBackgroundConverterTests`, `GridStyleWriterTests`, and any test
  the build flags.

**NOT touched in this task:** `GridStyleValidator.cs`, `GridStyleColorsValidationTests.cs`,
`GridStyleOrientationTests.cs`, `ConfigFacade.cs` (beyond nothing — the validator call stays), facade `Load`/
`Validate`/`Save` logic, `IGridStyleEditorFacade`.

- [x] Flip the 7 group-record files' color fields to `StyleColor`; update `GridStyleOptions.Default`.
- [x] Rework both mappers per above (load: parse-under-validator-guarantee; save: `.ToString()`); flip
      `PaletteBrushFactory`; add `StyleColorConversions`; rewire the VM's `Seed`/`BuildRecord`; delete `HexColor.cs`.
- [x] Retarget the fixture and all test constructions/assertions; add `StyleColor` to the guard leaf walk; delete
      the two unrepresentable facade tests.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green — including ALL untouched error tests
      (validator still standing) and the slice-1 guards over typed colors; `dotnet format`. One commit.

## Task 3: Fold validation into the load mapper, delete GridStyleValidator (one atomic commit)

The highest-risk unit, isolated: only the validation seat moves; the type system is already settled by Task 2, so
every diff line here is about error production and call-site wiring. The untouched-in-Task-2 error tests are the
fidelity net.

**Files:**
- Modify: `Mapping/GridStyleMapper.cs` — signature → `Result<GridStyleOptions>`; implement the aggregated parse
  phase per "The target shape" and the error-identity table: null-dto and null-colors early-outs; required-section
  handling for the three cell palettes (**every `!` on a color path replaced by real checks** — see the `!`-chain
  trap); `RequiredColor`/`OptionalColor` helpers carrying the exact snake_case section/key strings; orientation
  checked before `GridOrientationValues.Parse`; emission order preserved.
- Delete: `Validation/GridStyleValidator.cs`.
- Modify: `Facade/ConfigFacade.cs` — `GridStyleValidator.Validate(gridStyle)` → `GridStyleMapper.Map(gridStyle)` at
  the SAME pipeline position (grid-style errors must keep propagating before `CrossReferenceValidator`, as today);
  `MapToDomain` takes the mapped `GridStyleOptions` instead of the DTO.
- Modify: `GridStyleEditorFacade.cs` — `Load`: loader → `Map` result directly; `Validate`: `return Result.Ok();`
  with a comment naming it a deliberate vacuous pass-through until slice 5 trims the interface; `Save`: remove the
  pre-write validation gate; update the class XML doc (it currently claims "check color hex format only").
- Modify comments referencing the dead precondition: `Dto/GridOrientationValues.cs` ("GridStyleValidator rejects
  unknown values before this parser runs" — now the mapper does), any residual mapper comment.
- Modify tests:
  - `GridStyleColorsValidationTests.cs` — swap the entry point: `GridStyleValidator.Validate(dto)` →
    `GridStyleMapper.Map(dto)` (both expose `IsFailed`/`IsSuccess`/`Errors`; every assertion body stays
    byte-identical, including all key-name and message-fragment checks). Drop the now-unused
    `SemiStep.Core.Configuration.Validation` using.
  - `GridStyleOrientationTests.cs` — `Validate_UnknownOrientation_Fails` and the `LoadValidated` helper retarget to
    the mapper result (`Map(...).IsFailed` / `.Value`).
  - `GridStyleMapperTests.cs` — `Map` now returns a Result: success paths read `.Value`;
    `Map_NullDto_ReturnsDefaults` becomes `Map_NullDto_FailsWithConfigMissing` (the Default-on-null branch was
    production-dead behind the validator; the pipeline behavior — null → `GridStyleConfigMissingError` — is what
    survives).
  - `ConfigFacadeGridStyleValidationTests.cs`: no further changes beyond Task 2's compile fix (the
    `.ToString().Should().StartWith("#")` rewrite) — its error assertions must pass as-is. `ReasonLocalizerTests`,
    `CoreErrorLocalizationCoverageTests`, and the RU-culture VM test: NO changes — they must pass as-is; that is the
    point.
- [x] Implement the mapper-resident aggregated validation; delete the validator; rewire `ConfigFacade` and
      `GridStyleEditorFacade`; make `Validate` vacuous (interface untouched).
- [x] Retarget the three test files' entry points with assertion bodies unchanged; verify the untouched suites
      (`ConfigFacadeGridStyleValidationTests`, localization tests, RU-culture end-to-end message) are green with
      zero diff.
- [x] **Error-identity scratch checks (do NOT commit any break):** in a temp config dir, (a) malform one required
      hex (`readonly.depth_0: "zzz"`) → `facade.Load` fails with `GridStyleHexColorInvalidError` carrying
      `colors.cells.readonly` / `depth_0` / `zzz`; (b) delete the whole `disabled:` block → single
      `GridStyleSectionMissingError("colors.cells.disabled")` among otherwise-clean errors; (c) malform three keys
      across two sections at once → all three errors in ONE result (aggregation proof); (d) delete the whole
      `cells:` block (keeping `colors:` with e.g. `grid_line`) → EXACTLY three `GridStyleSectionMissingError`s with
      paths `colors.cells.execution`, `colors.cells.readonly`, `colors.cells.disabled` and no `colors.cells` error —
      this identity path is untested by the whole existing suite (the MissingExecution/ReadOnly/Disabled tests all
      construct `Cells` non-null with two of three palettes present), so a wrong `if (Cells is null) → one error`
      implementation would ship green without this check. Record results in the progress log.
- [x] **Guard non-vacuity re-proof:** temporarily cross-wire two same-type colors in the VM's `BuildRecord`
      (e.g. pass `SelectionForeground.ToStyleColor()` into the Background slot) → the perturbation guard goes red
      for those leaves (proves the leaf walk treats `StyleColor` as a leaf and still bites). Revert. Record in the
      progress log.
- [x] `dotnet build SemiStep.slnx` 0 warnings (zero `GridStyleValidator` references left); full `dotnet test` green;
      `dotnet format`. One commit.

## Task 4: Correct the file-header format claims, doc note

The shipped file headers advertise `#RGB`/`#ARGB`, which the validator has always rejected for validated keys and
`StyleColor.Parse` now rejects uniformly. `GridStyleWriter` preserves the leading comment block on every save, so the
wrong claim would survive editor rewrites indefinitely — fix the source files.

**Files:** `ConfigFiles/MBE/ui/grid_style.yaml`, `ConfigFiles/MOCVD/ui/grid_style.yaml` (RIE ships no header; the
test fixture header makes no format claims), `Docs/architecture/grid-style-configuration.md`.

- [x] Replace the header block in both shipped configs — current lines 2–6:

      ```
      # Color formats supported:
      #   - #RGB        (e.g., #FFF)
      #   - #RRGGBB     (e.g., #FFFFFF)
      #   - #ARGB       (e.g., #0FFF for transparent white)
      #   - #AARRGGBB   (e.g., #00FFFFFF for fully transparent white)
      # Alpha: 00 = fully transparent, FF = fully opaque
      ```

      with:

      ```
      # Color formats supported:
      #   - #RRGGBB     (e.g., #FFFFFF)
      #   - #AARRGGBB   (e.g., #00FFFFFF for fully transparent white)
      # Alpha: 00 = fully transparent, FF = fully opaque
      ```

- [x] `Docs/architecture/grid-style-configuration.md`: fix the sentence claiming the header documents
      "`#RGB`, `#RRGGBB`, `#ARGB`, `#AARRGGBB`" (currently ~line 44) to the two accepted formats. Also correct the
      load-pipeline description this slice invalidated — chosen over a "diagram is stale" disclaimer because leaving
      a wrong diagram directly above a correcting note would contradict itself, and the fix is two lines: in the
      pipeline diagram (~lines 49–57) replace the `GridStyleValidator.Validate` step with the
      `GridStyleMapper.Map` line (now "DTO → record, aggregated per-key validation, applies defaults"), and rewrite
      the ~line-59 prose ("loads the DTO, runs `GridStyleValidator`, calls `GridStyleMapper.Map`" → "loads the DTO,
      calls `GridStyleMapper.Map`, which validates and maps in one pass"). Add the interim slice-4 note: colors are
      typed `StyleColor`, validation lives in the load mapper, the standalone validator and `HexColor` are gone (the
      full doc rewrite lands in slice 5, which also deletes the now-obsolete "Known gap" section).
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green (the shipped-config load tests prove the
      edited files still parse); `dotnet format`. One commit.

## Post-Completion

**Next:** slice 5 — split the VM into per-group `ReactiveObject` drafts (initializer = seed, positional `Build()` =
compile guard), shrink the parent VM to ~150 lines, group the AXAML binding paths (build-checked via `x:DataType`),
trim `IGridStyleEditorFacade` (the vacuous `Validate` and the per-keystroke call go away — `CanSave` reduces to the
VM-side numeric range checks), and rewrite `Docs/architecture/grid-style-configuration.md` as current-state. Slice 5
closes #118. Remaining slice-5 fork, still open: AXAML rename scope (big-bang grouped paths vs bridge properties)
and whether the parent exposes drafts as properties or per-card `DataContext`.

**Stacking:** this slice stacks on `grid-style-nest-record` (PR #177, unmerged) — exec branches
`grid-style-color-typing` off that branch, not `master`. After #177 merges: `git fetch origin && git rebase
origin/master && git push --force-with-lease`.

**Executed by exec:**
- branch: grid-style-color-typing

## Verify it yourself

This slice is a structural refactor with one deliberate behavior change (hex case normalizes to uppercase on
save — the editor already rewrites the whole file). The on-disk YAML shape is unchanged, and the error identity a
user sees for a malformed config is byte-for-byte what the deleted validator produced. The evidence is in the
tests and the diff; there is no manual repro that distinguishes the validation seat move.

**Stacked-branch note:** all commands below use the slice-3 base `grid-style-nest-record` as the diff base, not
`master` — this branch is stacked on the unmerged #177.

1. **The validator is gone and nothing dangles.** `git grep -n GridStyleValidator -- '*.cs'` and
   `git grep -n 'class HexColor'` both return empty; `dotnet build SemiStep.slnx` → 0 warnings, 0 errors.
2. **Error identity survived the validator→mapper move.** The error tests were retargeted from
   `GridStyleValidator.Validate(dto)` to `GridStyleMapper.Map(dto)` with byte-identical assertion bodies, and the
   untouched localization + Russian-culture end-to-end message test still pass:
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GridStyleColorsValidation|FullyQualifiedName~GridStyleOrientation|FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~LoadAsync_MalformedHexColor"`.
3. **The null-`Cells`→exactly-three-errors identity is now a committed regression test** (was untested by the
   whole prior suite): `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~Validate_NullCells_FailsWithExactlyThreeSectionErrors"` — green asserts exactly `colors.cells.execution/readonly/disabled` and no bare `colors.cells` error.
4. **Colors round-trip losslessly through the unchanged YAML format.** `StyleColor.ToString` writes into the
   still-`string` DTOs, so the file shape is identical:
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~SaveThenLoad_DistinctFixture"` (green) plus the full `StyleColor` parse/reject/round-trip suite
   (`--filter "FullyQualifiedName~StyleColor"`).
5. **The header lie is fixed at the source.** `ConfigFiles/MBE/ui/grid_style.yaml` and
   `ConfigFiles/MOCVD/ui/grid_style.yaml` headers now list only `#RRGGBB`/`#AARRGGBB`; `git diff` shows the change
   is comment-only, no key/value touched.
6. **Full gate:** `dotnet build SemiStep.slnx` (0 warnings), full `dotnet test` (1716 passed), `dotnet format`
   clean.
7. **Manual smoke (optional):** open the style editor, change a color and orientation, save, reopen — values
   persist; a malformed hex hand-edited into the config still produces the same localized error at load. Note the
   editor rewrites the whole file on save (hex normalizes to uppercase) — expected.
