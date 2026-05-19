# Issue #11 — Single Source of Truth for String max_length

## Overview

Remove the hardcoded `ProtocolConstants.WStringMaxChars = 32` and establish `properties.yaml` (`max_length` of the `string` property type) as the single source of truth for the maximum number of characters in a recipe string value (currently used by the `comment` column).

Today the value is duplicated and silently inconsistent: `ConfigFiles/*/properties/properties.yaml` declares `max_length: 255`, while `ProtocolConstants.WStringMaxChars` is fixed at `32`. The S7 codec silently truncates strings longer than 32 chars, so values that pass Core validation are then mutilated on write to the PLC.

After this change:
- `PropertyTypeDefinition.MaxLength` (already populated from `properties.yaml`) is the only place this number lives.
- S7 layer obtains the value via DI factory and stops truncating silently — over-length input becomes a hard `throw` (defence in depth; Core/UI/CSV/clipboard/PLC-read validators are the SoT).
- `ImportedRecipeValidator` (used by both clipboard paste and PLC-read paths through `RecipeSession.LoadAsCurrentValidated`) is extended to invoke `PropertyValidator.Validate` per property — closing the clipboard and PLC-read ingress gaps simultaneously.
- UI propagates the same value to `TextBox.MaxLength` so users physically cannot enter too many characters.
- CSV import is wired through `PropertyValidator` (currently bypassed for property values).
- **Scope intentionally excluded:** changing the actual yaml value `max_length: 255 → 32`. The mechanism fix lands first; the value change requires PLC project inspection and is tracked as a separate follow-up (see Post-Completion).

## Context (from discovery)

**Files/components involved:**
- `SemiStep/SemiStep.Core/Plc/S7/Protocol/ProtocolConstants.cs` — constants to delete (`WStringMaxChars`, `WStringElementSize`).
- `SemiStep/SemiStep.Core/Plc/S7/Serialization/ArrayCodec.cs` — currently uses `ProtocolConstants.WStringMaxChars` in `WriteWString`/`ReadWString` for truncation and slot size.
- `SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs` (~line 88) — uses `WStringElementSize` for bulk read sizing.
- `SemiStep/SemiStep.Core/Plc/S7/S7Di.cs` — DI module for the S7 layer; new factory registration lives here.
- `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs` — domain registry; add `GetStringMaxLength()` accessor that asserts uniqueness internally.
- `SemiStep/SemiStep.Core/Recipes/PropertyValidator.cs` — already enforces `MaxLength`; reused as-is.
- `SemiStep/SemiStep.Core/Recipes/Helpers/ImportedRecipeValidator.cs` — currently validates only group keys; extend to invoke `PropertyValidator.Validate` per property value. Covers clipboard + PLC-read ingress in one place.
- `SemiStep/SemiStep.Core/Recipes/Import/CsvRowConverter.cs` — currently bypasses `PropertyValidator.Validate`.
- `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs` and `ColumnBuilder.cs` — build the editing `TextBox`; no `MaxLength` wired today.
- Tests: existing `S7/` tests touching `ArrayCodec`, new tests for registry method, CSV over-length, clipboard/import validator, UI `TextBox.MaxLength`.

**Related patterns:**
- Constructor DI only; no static mutable state (`CLAUDE.md`).
- `RecipeMetadataRegistry` is the canonical domain registry; using it for `MaxLength` introduces no new edges.
- Validation failures use `FluentResults.Result`. Config validators aggregate via `Result.Merge` and surface to `ErrorWindow`.
- Defence-in-depth invariants (unreachable-by-design failures) use `throw`, not `Result` — keeps internal pipelines simple.

**Dependencies identified:**
- `ArrayCodec` is constructed today inside `PlcTransactionExecutor`. The right move is to register `ArrayCodec` in `S7Di` via a factory that consults `RecipeMetadataRegistry`, then inject the fully-built `ArrayCodec` into `PlcTransactionExecutor`. The executor stays focused on transaction coordination; it does not learn about the registry.
- `RecipeSession.LoadAsCurrentValidated` is the shared seam for clipboard paste **and** PLC-read; routing both through `ImportedRecipeValidator` is sufficient.

## Development Approach

- **Testing approach:** TDD where natural (registry method, validators, codec); Regular for UI wiring (verified via `AvaloniaFact` headless tests).
- Complete each task fully before moving to the next.
- **CRITICAL:** every task MUST include new/updated tests covering both success and failure paths.
- **CRITICAL:** all tests must pass before starting next task.
- **CRITICAL:** update this plan file when scope changes during implementation.

## Testing Strategy

- **Unit tests:** xUnit v3 with `Component`/`Area`/`Category` traits.
- **UI tests:** `[AvaloniaFact]`. Instantiate the editing template, cast to `TextBox`, assert `MaxLength`.
- **Integration tests:** CSV over-length import fails; `ImportedRecipeValidator` rejects over-length string (covers clipboard + PLC-read).
- **Build/test commands:**
  - `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`
  - `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

## Progress Tracking

- mark completed items with `[x]` immediately when done
- add newly discovered tasks with ➕ prefix
- document issues/blockers with ⚠️ prefix
- update plan if implementation deviates from original scope

## Solution Overview

Single SoT: `PropertyTypeDefinition.MaxLength` for `system_type=string`. `RecipeMetadataRegistry.GetStringMaxLength()` returns the unique value (asserts uniqueness internally — no separate validator class). `S7Di` registers an `ArrayCodec` factory that pulls the value from the registry; `PlcTransactionExecutor` is rewired to consume the injected `ArrayCodec`. UI consumes `MaxLength` through the same registry to drive `TextBox.MaxLength`. CSV import and `ImportedRecipeValidator` are wired through the existing `PropertyValidator` so every ingress is guarded.

**Key design decisions:**
- `ArrayCodec.WriteWString` throws on over-length (contract violation, unreachable when validators are wired). No `Result` ripple through the encode pipeline.
- Uniqueness of `MaxLength` across `string`-typed properties is enforced **inside** `GetStringMaxLength()` — single assertion, no extra validator class. The scalar return shape structurally prevents the failure mode.
- `ArrayCodec` is registered in `S7Di` via a factory; executor receives it ready-made. Registry stays out of `PlcTransactionExecutor`'s ctor.
- `ImportedRecipeValidator` becomes the shared property-value validation seam for clipboard + PLC-read; CSV is wired separately (different parsing path).
- yaml value (`max_length: 255 → 32`) intentionally **not** changed in this plan.

## Technical Details

- `RecipeMetadataRegistry.GetStringMaxLength()` → `int`:
  - finds all properties with `SystemType == "string"`,
  - asserts all have the same non-null `MaxLength`; on violation throws (registry construction is once-per-app, failure surfaces at startup via `ErrorWindow`),
  - returns the unique value; throws if no `string` property exists.
- `ArrayCodec`:
  - ctor: `ArrayCodec(DataDbLayout intDb, DataDbLayout floatDb, DataDbLayout stringDb, int wStringMaxChars)`
  - property: `int WStringElementSize => WStringHeaderSize + _wStringMaxChars * 2;`
  - `WriteWString` throws `ArgumentException` if `value.Length > _wStringMaxChars`.
- `S7Di`: registers `ArrayCodec` as a singleton with factory `sp => new ArrayCodec(layout..., sp.GetRequiredService<RecipeMetadataRegistry>().GetStringMaxLength())`.
- `PlcTransactionExecutor`: ctor takes `ArrayCodec` (no registry). Line ~88 uses `_arrayCodec.WStringElementSize`.
- `ImportedRecipeValidator.ValidateStep`: after group validation, for each property in the step invokes `PropertyValidator.Validate(propertyDef, value)` and merges results.
- `CsvRowConverter`: after parsing each property value, invokes `PropertyValidator.Validate(propertyDef, parsedValue)` and aggregates failures with row/column context.
- UI: `TextCellFactory.CreateEditingTemplate(int? maxLength)` sets `TextBox.MaxLength` when provided. `ColumnBuilder` resolves it via `recipeMetadataRegistry.GetProperty(columnDef.PropertyTypeId).MaxLength`.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all code, DI, validator, and test changes.
- **Post-Completion** (no checkboxes): manual PLC verification + the separate yaml value-correction follow-up + issue close.

## Implementation Steps

### Task 1: Add `GetStringMaxLength` to `RecipeMetadataRegistry`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Unit/Properties/RecipeMetadataRegistryStringMaxLengthTests.cs`

- [x] add `GetStringMaxLength()` returning `int`
- [x] internal logic: collect all `PropertyTypeDefinition` with `SystemType == "string"`; if multiple distinct `MaxLength` values, throw with a clear message naming the conflicting property ids; if no string property or `MaxLength` is null, throw
- [x] unit tests: single string property with `MaxLength=32` → returns 32
- [x] unit tests: two string properties with same `MaxLength=32` → returns 32
- [x] unit tests: two string properties with different `MaxLength` → throws with both ids in the message
- [x] unit tests: string property with `MaxLength=null` → throws
- [x] unit tests: no string property at all → throws
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must pass before Task 2

### Task 2: Refactor `ArrayCodec` + register via DI + rewire executor (single atomic task)

Bundled because deleting the constants breaks the build until both the codec and the executor are updated.

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/S7/Protocol/ProtocolConstants.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/S7/Serialization/ArrayCodec.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/S7/S7Di.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs`
- Modify: existing `SemiStep/SemiStep.Tests/S7/` tests touching `ArrayCodec`/`PlcTransactionExecutor`
- Create: `SemiStep/SemiStep.Tests/S7/ArrayCodecWStringMaxCharsTests.cs`

- [x] delete `WStringMaxChars` and `WStringElementSize` from `ProtocolConstants` (keep `WStringHeaderSize`, `IntElementSize`, `FloatElementSize`)
- [x] `ArrayCodec` ctor accepts `int wStringMaxChars`; field is `private readonly`
- [x] expose `WStringElementSize` as a readonly property computed from the field
- [x] `WriteWString` throws `ArgumentException` (with property id / value length in the message) when `value.Length > _wStringMaxChars`; remove the silent truncation branch
- [x] `ReadWString` uses injected value as the upper bound
- [x] register `ArrayCodec` in `S7Di` via factory consulting `RecipeMetadataRegistry.GetStringMaxLength()`
- [x] `PlcTransactionExecutor` ctor receives `ArrayCodec` (no `RecipeMetadataRegistry`); update the ~line 88 read-size calculation to use `_arrayCodec.WStringElementSize`
- [x] update existing executor/codec tests to construct via the new shape (test helper supplies a known `wStringMaxChars`)
- [x] new tests: round-trip with `wStringMaxChars=16` and `=32` produces correct slot size and bytes
- [x] new tests: `WriteWString` with over-length input throws
- [x] new tests: `WStringElementSize == 4 + maxChars * 2`
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must pass before Task 3

### Task 3: Route `ImportedRecipeValidator` through `PropertyValidator` (covers clipboard + PLC-read)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/ImportedRecipeValidator.cs`
- Modify or Create: `SemiStep/SemiStep.Tests/Core/Unit/Properties/ImportedRecipeValidatorTests.cs`

- [x] for each step's property values, invoke `PropertyValidator.Validate(propertyDef, value)` and merge into the existing `Result` aggregation
- [x] preserve existing group-key validation; do not reorder or short-circuit
- [x] error messages include step index + property id
- [x] unit test: step with over-length string is rejected with a `MaxLength`-related error
- [x] unit test: step with out-of-range int is rejected (locks in that the new seam is general, not string-specific)
- [x] unit test: valid step passes through unchanged
- [x] unit test: multiple violations across steps are all reported (Result.Merge behaviour)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"` — must pass before Task 4

### Task 4: Route CSV import through `PropertyValidator`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Import/CsvRowConverter.cs`
- Create: `SemiStep/SemiStep.Tests/Csv/Integration/CsvPropertyValidationTests.cs`

- [x] after parsing each property value in `CsvRowConverter`, invoke `PropertyValidator.Validate(propertyDef, parsedValue)` and aggregate failures into the existing pipeline
- [x] error messages identify the row/column for the user
- [x] integration test: CSV with over-length string fails import with descriptive error including row/column
- [x] integration test: CSV with out-of-range int fails (general-coverage check)
- [x] integration test: CSV with valid values imports successfully
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Csv"` — must pass before Task 5

### Task 5: UI — bind `TextBox.MaxLength` for string columns

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/TextCellFactory.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/ColumnBuilder.cs`
- Modify or Create: `SemiStep/SemiStep.Tests/UI/RecipeGridStringMaxLengthTests.cs`

- [x] `TextCellFactory.CreateEditingTemplate` accepts `int? maxLength`; when present, sets `TextBox.MaxLength`
- [x] `ColumnBuilder` looks up `PropertyTypeDefinition` via `recipeMetadataRegistry.GetProperty(columnDef.PropertyTypeId)`, reads `MaxLength`, forwards into `TextCellFactory`
- [x] do NOT cache/denormalise `MaxLength` into `GridColumnDefinition`
- [x] `[AvaloniaFact]` test: invoke the editing template factory for the `comment` column against a row VM, cast the produced control to `TextBox`, assert `MaxLength` equals the property's `MaxLength`
- [x] `[AvaloniaFact]` test: column whose property has no `MaxLength` leaves `TextBox.MaxLength` at default (`0` meaning unlimited)
- [x] run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=UI"` — must pass before Task 6

### Task 6: Verify acceptance criteria

- [ ] `Grep` confirms `WStringMaxChars` and the old `WStringElementSize` are gone (only the new property on `ArrayCodec` remains)
- [ ] no static mutable state introduced (grep for `public static.*set`)
- [ ] `GridColumnDefinition` has no `MaxLength` field
- [ ] manual trace: CSV → CsvRowConverter → PropertyValidator; UI typing → existing RecipeSession path → PropertyValidator; clipboard paste → ImportedRecipeValidator → PropertyValidator; PLC read → ImportedRecipeValidator → PropertyValidator; S7 write → ArrayCodec (throws on over-length)
- [ ] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [ ] `dotnet format SemiStep/SemiStep.slnx`

### Task 7: Final — close out

- [ ] `CLAUDE.md` unchanged (no new pattern, reuses existing registry + DI factory patterns)
- [ ] move this plan to `Docs/plans/completed/`
- [ ] reference plan path in commit message

## Post-Completion

**Manual verification:**
- End-to-end smoke test against the actual PLC project: enter a `comment` value at the current `properties.yaml` max length, save recipe, deploy, verify the PLC sees the full string. If truncated, the PLC slot is smaller than yaml — proceed to the value-correction follow-up below.

**Separate follow-up (not in this plan):**
- Open a tracking issue: "Align `properties.yaml` string `max_length` with real PLC WString slot." Inspect the TIA Portal project to confirm the actual slot size (currently presumed 32). Update `ConfigFiles/MOCVD/properties/properties.yaml` and `ConfigFiles/MBE/properties/properties.yaml` accordingly. This is a domain/PLC decision gated on evidence, not a code change.

**External system updates:**
- Close GitHub issue #11 with a comment referencing the merge commit / PR.
