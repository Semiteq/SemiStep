# Nested Actions (Generations): Depth-1 → Depth-N Row Composition

## Overview

Today a recipe row's column composition is depth-1: the primary action fully
determines which columns apply. This plan generalizes to depth-N "nested actions":
a selector column's chosen value pulls in a referenced child action (a "subaction")
that contributes more columns, which may itself carry a further selector, recursively.
Inactive columns reuse the existing greyed "inapplicable" state.

The data model stays flat and cheap: `Step` is unchanged, grid columns stay one global
flat set. The tree lives only in metadata. Two derived facts are computed once at config
load by walking the reference graph:

- **Column union per primary action** — every column reachable through `targets`,
  materialized into `ActionDefinition.Properties` in a deterministic order. This is the
  storage/serialization view. It is the piece the original issue #71 design missed:
  PLC write, CSV import, clipboard paste, and import validation all iterate
  `action.Properties`, so a subaction column absent from it has its value silently
  dropped. Materializing the union fixes this with zero change to those consumers.
- **Per-column activation condition** — which selector value(s) up the chain make a
  column active. This is the runtime UI view, feeding dynamic greying.

Problem it solves: operator-set values on branch columns are silently lost on PLC
write/import today if those columns are not on the primary action; and selectors cannot
change row composition (e.g. RIE `Травление` with `icp_match = Авто` must hide the
capacitor columns).

## Context (from discovery)

- Project: SemiStep, .NET 10 / C# 14, Avalonia + ReactiveUI. Recipe-table editor for S7 PLC.
- Config pipeline: YAML → DTOs → loaders → mappers → validators → `RecipeMetadataRegistry`.
- Files/areas involved:
  - Config DTO/loader/mapper/validation: `ActionColumnDto`, `ActionDto`,
    `ActionsSectionLoader`, `ActionMapper`, `CrossReferenceValidator`.
  - Metadata: `ActionDefinition`, `ActionPropertyDefinition`, `RecipeMetadataRegistry`.
  - Applicability/UI: `CellStateResolver`, `RecipeRowViewModel`, `RecipeGridViewModel`,
    `CellApplicabilityBinding`, `InapplicableCellTheme`.
  - Serialization/import (must NOT change behaviour, only benefit from the union):
    `RecipeConverter`, `CsvRowConverter`, `ClipboardSerializer`, `ImportedRecipeValidator`.
  - Formulas: `FormulaEvaluator`.
- Patterns observed: `FluentResults` `Result<T>` everywhere in config mapping/validation;
  immutable record/`sealed class` definitions; constructor injection; xUnit tests with
  `[Trait("Component"|"Area"|"Category")]`; invalid-config tests use the overlay pattern
  over `SemiStep.Tests/YamlConfigs/`.
- Existing action count to consider for migration: 69 (`RIE` 9, `MOCVD` 38, `MBE` 22).

## Development Approach

- **testing approach**: Regular (code first, then unit tests) — matches the project.
- Complete each task fully before the next. Small, focused changes.
- **Every task includes new/updated tests** (success + error/edge), as separate checklist items.
- **All tests pass before starting the next task.**
- Build entry executable: `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj`.
- Test: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (filter by `Component`/`Area`).
- Backward compatibility: existing actions without `targets` behave exactly as before;
  `role` defaults to `action` so the 69 existing actions need no edit.

## Testing Strategy

- **unit tests**: required every task. Config-side via mapper/validator/registry over
  small inline or overlay YAML; applicability via `CellStateResolver` and row VM.
- **UI/headless**: applicability and selector-edit behaviour use `[AvaloniaFact]` where the
  row view-model and bindings are exercised.
- **no project e2e harness** beyond headless Avalonia tests; treat headless VM tests as the
  integration layer.

## Progress Tracking

- mark completed `[x]` immediately; add discovered tasks with ➕; blockers with ⚠️.
- keep this file in sync; update if scope shifts.

## Solution Overview

Decisions settled in design discussion:

- **Reference-based subactions.** A selector column entry carries `targets: {selectorValue → actionId}`.
  The referenced action is a "subaction" — a named, reusable column bundle. Sharing across
  parents is allowed (reference DAG). Targets are action-scoped (the same group can branch
  differently per selector).
- **Explicit `role: action | subaction`**, defaulting to `action`. `action` = appears in the
  operator dropdown; `subaction` = reachable only via `targets`. Default `action` means zero
  migration on existing configs; `subaction` is stated only on fragments.
- **Two derived structures, one load-time walk**: column union (→ `Properties`, deterministic
  order) and per-column activation condition (→ dynamic applicability).
- **Validator rules** (config-load, hard errors): every `targets` id resolves; targets point
  only at `role: subaction`; every `role: subaction` is referenced ≥1; no cycles in the
  reference graph; ids unique. Dropdown built from `role: action` roots only.
- **PLC/serialization/import unchanged**: they keep iterating `Properties`, now the union.
- **Formula guard**: skip recalc when a referenced variable is not in the row's active set
  instead of throwing. The speculative config-time cross-branch ban is dropped (YAGNI).
- **Core untouched**: flat `Step`, one global flat column set, reuse of the greyed
  "inapplicable" palette (no new style).

## Technical Details

### Config shape

```yaml
300:
  ui_name: "Травление"
  role: action                # default; may be omitted
  columns:
    - { key: icp_power, property_type_id: power_icp, default_value: "0" }
    - key: icp_match
      group_name: match_mode
      property_type_id: enum
      targets: { 2: 3002 }    # value 2 (Ручной) -> subaction 3002
    - key: rie_match
      group_name: match_mode
      property_type_id: enum
      targets: { 2: 3003 }
    - { key: step_duration, property_type_id: time, default_value: "10" }
    - { key: comment, property_type_id: string }

3002:
  ui_name: "ICP ручной"
  role: subaction
  columns:
    - { key: icp_load, property_type_id: percent, default_value: "50" }
    - { key: icp_tune, property_type_id: percent, default_value: "50" }
3003:
  ui_name: "RIE ручной"
  role: subaction
  columns:
    - { key: rie_load, property_type_id: percent, default_value: "50" }
    - { key: rie_tune, property_type_id: percent, default_value: "50" }
```

### Derived data

For primary action `300` after the load-time walk:

- `Properties` (union, deterministic order — declaration order, depth-first splice at the
  selector site). For the simplified example above:
  `icp_power, icp_match, icp_load, icp_tune, rie_match, rie_load, rie_tune, step_duration, comment`.
- Activation map: `icp_load`/`icp_tune` active iff `icp_match == 2`; `rie_load`/`rie_tune`
  active iff `rie_match == 2`; the rest always active.

**PLC byte-order invariant.** The PLC writes values into fixed slots by `Properties` order.
For the real RIE action `300` (`process.yaml:88-111`, full order includes `rie_power`
between the icp-manual splice and `rie_match`, plus the gate/flow columns), the union after
splicing MUST reproduce the existing column order byte-for-byte, so the slot layout is
unchanged. This is a hard invariant, regression-tested in Task 10 — not just the abstract
"declaration order, depth-first" rule.

### Processing flow

1. Loader parses `targets` (on columns) and `role` (on actions) into DTOs.
2. Mapper produces raw action definitions carrying own columns, role, and per-column targets.
3. A resolver pass over all raw actions: validates the reference graph, computes per-root
   union + activation map, and builds the final primary `ActionDefinition`s.
4. Registry exposes roots (dropdown) and resolved primary actions (with union `Properties`).
5. At runtime, the active column set for a row is `union minus columns whose activation
   condition is unmet by the step's current selector values`. Inactive → greyed; on selector
   edit, drop now-inactive values and seed defaults for now-active ones in one undo unit.

### Activation representation

`ActionPropertyDefinition` gains an optional activation condition
(`(string SelectorKey, int EnablingValue)?` or a small list to support depth>1 chains —
a column is active iff every condition on its path is met). Always-active columns carry none.

## What Goes Where

- **Implementation Steps** (checkboxes): all code, tests, the RIE config edit, the
  architecture-note rewrite.
- **Post-Completion** (no checkboxes): manual operator-facing QA of greying/undo in the
  running app; review of whether MOCVD/MBE want any subaction usage (not in scope here).

## Implementation Steps

### Task 1: Parse `targets` and `role` from config

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/ActionColumnDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/ActionDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/ActionsSectionLoader.cs`
- Modify: `SemiStep/SemiStep.Tests/...` (loader tests)

- [x] add `Dictionary<int,int>? Targets` to `ActionColumnDto`
- [x] add `string? Role` to `ActionDto` (null → treated as `action` downstream)
- [x] ensure the loader/deserializer maps `targets` and `role` (verify the YAML binding path used by `ActionsSectionLoader`)
- [x] write tests: a column with `targets` and an action with `role: subaction` deserialize correctly
- [x] write tests: absent `targets`/`role` deserialize as null (backward compat)
- [x] run tests — must pass before next task

### Task 2: Carry targets and role through mapping

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/ActionPropertiesDefinition.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/ActionMapper.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/ActionRole.cs` (enum `Action`, `Subaction`)
- Modify: `SemiStep/SemiStep.Tests/...` (mapper tests)

- [x] add `ActionRole` enum; map `dto.Role` (null/`"action"` → `Action`, `"subaction"` → `Subaction`, else fail)
- [x] extend the mapped column/action shape to carry per-column `Targets` and the action `Role` (raw form, pre-union)
- [x] keep `TryMapColumn` producing own-column data plus its targets
- [x] write tests: role mapping (default, explicit, invalid string → failure)
- [x] write tests: targets survive mapping; non-selector columns have none
- [x] run tests — must pass before next task

### Task 3: Reference-graph resolver — union, activation map, validation

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/ActionTreeResolver.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/ActionDefinition.cs` (carry activation map; `Properties` becomes the union for roots)
- Modify: `SemiStep/SemiStep.Core/Recipes/ActionPropertiesDefinition.cs` (add optional activation condition to the record)
- Create: `SemiStep/SemiStep.Tests/...` (resolver tests)

- [x] add the optional activation condition (`(string SelectorKey, int EnablingValue)` list, empty = always active) to `ActionPropertyDefinition`
- [x] implement a pass over all mapped actions that, per `role: action` root, walks `targets`
      transitively and produces the column union in deterministic order (declaration order,
      depth-first splice at the selector site), deduped by key
- [x] produce the per-column activation condition during the same walk
- [x] detect cycles in the reference graph → `Result.Fail`
- [x] reject only a column key reachable under two branches with *conflicting* property types; the same key+type shared across parents (shared subaction) is allowed
- [x] write tests: depth-1 union (RIE icp/rie), depth-2 union (pump-style chamber→criterion)
- [x] write tests: shared subaction referenced by two parents resolves once per root, stable order
- [x] write tests: cycle → failure; deterministic order is stable across loads
- [x] run tests — must pass before next task

### Task 4: Config-load validation rules for the reference graph

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/Validation/CrossReferenceValidator.cs`
- Modify: `SemiStep/SemiStep.Tests/...` (validation tests, overlay configs)

- [x] every `targets` value resolves to a defined action id (dangling → fail)
- [x] every `targets` target has `role: subaction` (pointing at a root/default-action → fail; this catches a mis-tagged fragment)
- [x] every `role: subaction` is referenced by ≥1 `targets` (orphan → fail)
- [x] ids unique across the registry (confirm existing rule still holds with subactions present)
- [x] write tests (overlay configs): dangling target, target→action, orphan subaction, duplicate id — each fails with a clear message
- [x] write tests: valid nested config passes
- [x] run tests — must pass before next task

### Task 5: Registry exposes roots for the dropdown, resolved primaries for metadata

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeMetadataRegistry.cs`
- Modify: `SemiStep/SemiStep.Tests/...` (registry tests)

- [x] subactions are consumed only by the Task 3 resolver at load and do NOT enter the registry's
      runtime action collections (`_actionsById`/`_allActions`/`_actionsByName`); only resolved
      primary `ActionDefinition`s are stored. This avoids regressing `GetDefaultActionId()`
      (`RecipeCoordinator.cs:169`, returns the first action) and `ColumnWidthCalculator` (widens the
      action column by action names)
- [x] build `GetActionComboBoxItems()` from the resolved primaries (all are `role: action`)
- [x] expose resolved primary `ActionDefinition`s (union `Properties` + activation map) to consumers
- [x] write tests: dropdown excludes subactions; primary action `Properties` equals the union
- [x] write tests: `GetDefaultActionId()` never returns a subaction id
- [x] write tests: backward-compat config (no targets/role) yields the same dropdown as before
- [x] run tests — must pass before next task

### Task 6: Dynamic applicability resolver (step-value aware)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Helpers/CellStateResolver.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/Helpers/ActiveColumnSetResolver.cs` (or fold into resolver)
- Modify: `SemiStep/SemiStep.Tests/...`

- [x] grep and enumerate all callers of `CellStateResolver.IsInapplicable` before changing its signature; migrate each to pass the step/active set
- [x] add a function computing the active column set for a step from the action's activation map + the step's current selector values
- [x] change `IsInapplicable` to consult the active set (column inactive ⇒ inapplicable), keeping the action-column and read-only exclusions
- [x] remove the old single-action `IsPropertyPresentInAction` path once unused
- [x] write tests: icp_match Авто ⇒ icp_load/icp_tune inactive; Ручной ⇒ active; rie independent
- [x] write tests: depth-2 activation (chamber→criterion) and always-active columns
- [x] run tests — must pass before next task

### Task 7: Core batched selector mutation (one undo unit)

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (expose the batched mutation if mutations are routed through it)
- Modify: `SemiStep/SemiStep.Tests/...`

- [x] the undo boundary is `RecipeSession.Apply` (one call = one undo snapshot); `UpdateStepProperty` calls it once per property, so a selector edit that drops/seeds several columns via N calls would create N undo snapshots
- [x] add a batched mutation, e.g. `UpdateStepForSelectorChange(stepIndex, selectorKey, value, columnsToDrop, columnsToSeed)`, that builds ONE `updatedStep` (set selector, remove dropped keys, add seeded defaults) and calls `Apply` exactly once
- [x] route formula recalc (if any) within the same single `Apply`
- [x] write tests: a batched selector change produces exactly one undo snapshot; `Undo` restores the prior selector value AND the prior dropped values in one step
- [x] write tests: ordinary single-property edits are unchanged
- [x] run tests — must pass before next task

### Task 8: Observable `InapplicableColumns` + selector-edit recompute

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeRowViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/CellApplicabilityBinding.cs` (verify it re-evaluates on `PropertyChanged`)
- Modify: `SemiStep/SemiStep.Tests/...` (`[AvaloniaFact]`)

- [x] change `InapplicableColumns` from ctor-fixed get-only to a `RaiseAndSetIfChanged` property; on recompute assign a NEW `IReadOnlySet<string>` instance (do NOT mutate in place — the `OneWay` binding only re-fires on `PropertyChanged` + reference change)
- [x] on a selector-column edit: compute the new active set, then call the Task 7 batched mutation with the drop/seed sets so the value change + applicability shift is ONE undo unit
- [x] verify `CellApplicabilityBinding`/`InapplicableCellTheme` flip on reassignment; note if neither needs code change beyond the property becoming observable
- [x] write tests: switching icp_match to Авто drops icp_load value and greys it; back to Ручной seeds defaults
- [x] write tests (headless): the cell theme flips when `InapplicableColumns` is reassigned
- [x] run tests — must pass before next task

### Task 9: Wire the recompute trigger in the grid view-model

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/RecipeGridViewModel.cs`
- Modify: `SemiStep/SemiStep.Tests/...`

- [x] in `CreateRowViewModel`, seed the active set from the resolved action + step (done in Task 8: `BuildInapplicableColumns(action, step, ...)` at row creation)
- [x] in the `PropertyUpdated` mutation path, when the edited column has targets, trigger the row recompute (Task 8: selector edits route through `OnSelectorValueChanged` → `UpdateStepForSelectorChange` → `row.RecomputeInapplicableColumns()`)
- [x] confirm non-selector edits do not trigger applicability recompute (no regression in `UpdateSingleRowInPlace`: it only calls `UpdateStep`; ordinary edits go through `OnCellValueChanged` with no recompute)
- [x] write tests: selector edit updates row applicability; ordinary edit does not (selector-positive cases existed from Task 8; added the non-selector negative test `OrdinaryEdit_DoesNotRecomputeApplicability_ReferenceUnchanged`)
- [x] run tests — must pass before next task

### Task 10: Formula guard for inactive variables

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Formulas/FormulaEvaluator.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs` (thread the active set into `Recalculate`/`BuildVariableMap`)
- Modify: `SemiStep/SemiStep.Tests/...`

- [x] thread the row's active column set (from the Task 6 resolver, which is Core) into `Recalculate`/`BuildVariableMap`
- [x] in `BuildVariableMap`, when a referenced recalc variable is not in the active set, skip the recalc (return the step unchanged) instead of throwing
- [x] write tests: a formula whose variable is currently inactive does not throw and leaves the step unchanged
- [x] write tests: a fully-active formula still recalculates as before
- [x] run tests — must pass before next task

### Task 11: RIE config — Травление manual/auto branches

**Files:**
- Modify: `ConfigFiles/RIE/actions/process.yaml`
- (verify) `ConfigFiles/RIE/groups/match_mode.yaml`, `ConfigFiles/RIE/columns/columns.yaml`

- [x] add `targets` to `icp_match` (value Ручной → ICP-manual subaction) and `rie_match` (→ RIE-manual subaction)
- [x] add the two `role: subaction` entries carrying `icp_load`/`icp_tune` and `rie_load`/`rie_tune`; remove those four from the primary `300` column list (now contributed by subactions)
- [x] confirm config validation passes and the union for `300` reproduces the prior column ORDER byte-for-byte (`rie_power` stays between the icp splice and `rie_match`; gate/flow columns unchanged)
- [x] write/extend tests: load RIE config; `300` union equals the expected column list AND order
- [x] write a PLC regression test: serialize a fixed `Травление` step before/after this change and assert identical slot layout (`PlcRecipeData`) — backs the Post-Completion byte-layout item
- [x] run tests — must pass before next task

### Task 12: Verify acceptance criteria

- [x] RIE `Травление`: `icp_match = Авто` ⇒ `icp_load`/`icp_tune` inapplicable (greyed, no value); `Ручной` ⇒ applicable with defaults; `rie_*` independent — `RieEtchManualBranchConfigTests.EtchAction_AutoMatch_DeactivatesCapacitorColumns` / `_ManualMatch_ActivatesCapacitorColumns` / `_MatchSelectorsAreIndependent`; UI end-to-end `RecipeRowSelectorEditTests.InitialAutoSelection_SubColumnInapplicable` / `SwitchToManual_SeedsSubValueDefault_AndMakesItApplicable` / `SwitchBackToAuto_DropsSubValue_AndGreysIt`
- [x] `Ctrl+Z` after switching to `Авто` restores `Ручной` with prior load/tune values (single undo unit, Task 7) — `RecipeRowSelectorEditTests.SwitchToAutoThenUndo_RestoresManualSelectorAndDroppedValue_InOneStep` (asserts selector restored to 1 AND prior value 73 restored in one Undo)
- [x] a step with an inactive capacitor column writes `0`/empty into its fixed PLC slot (value not lost for active columns) — added `RieEtchManualBranchSerializationTests.SerialiseEtchStep_AutoMatch_InactiveCapacitorColumnsWriteZeroIntoTheirSlots` (4 inactive capacitor floats serialise as 0 with no slot shift; active gas/power/duration keep positions)
- [x] a depth-2 tree (a subaction that itself carries a selector with `targets`) resolves and activates correctly — `ActionTreeResolverTests.Resolve_Depth2_ChamberToCriterion_BuildsChainedActivation` (resolution + chained activation), `ActiveColumnSetResolverTests.Resolve_Depth2Chain_RequiresEveryConditionOnThePath` (activation gating)
- [x] `GetDefaultActionId()` and the dropdown never surface a subaction — `RecipeMetadataRegistryTests.GetAllActions_FirstAction_IsNeverASubaction` (GetDefaultActionId returns GetAllActions().First().Id), `_GetActionComboBoxItems_ExcludesSubactions`, `_Subaction_DoesNotEnterRuntimeActionCollections`; config-level `RieEtchManualBranchConfigTests.RieConfig_DropdownExcludesSubactions`
- [x] backward compatibility: actions without `targets`/`role` behave exactly as before; dropdown unchanged for existing configs — `RecipeMetadataRegistryTests.GetActionComboBoxItems_BackwardCompat_NoTargetsNoRole_MatchesPlainActions`; loader `ActionsSectionLoaderTargetsRoleTests` absent-targets/role cases; resolver `ActionTreeResolverTests` plain-action paths
- [x] run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — run SPLIT BY COMPONENT to avoid the known process-wide Avalonia/ReactiveUI `RxAppBuilder.EnsureInitialized` harness limitation (~195 UI failures only when all run in one process — pre-existing baseline, NOT a regression). Per-component all green: Core 229, Config 200, S7 102, Domain 34, Csv 17, UI 236; `Area=NestedActions` 46. Full UI build: 0 errors.

### Task 13: [Final] Documentation

**Files:**
- Modify: `Docs/architecture/nested-actions.md`
- Modify: `CLAUDE.md` (only if a new durable convention emerged)

- [x] rewrite `nested-actions.md` to the final model: reference subactions, `role` (default `action`), column-union materialization + deterministic order, dynamic applicability, validator rules, formula guard, untouched core; correct the original "already generation-tolerant" claim to name the union step
- [x] update `CLAUDE.md` only if warranted — no CLAUDE.md change warranted (project-overview file only; it states "do not add specifics here", and no cross-project convention emerged)
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
*Manual / external — no checkboxes*

**Manual verification**
- Operator-facing QA in the running app: greying transitions, value-drop on switch to `Авто`,
  undo restoring the manual branch, dropdown shows only top-level actions.
- Confirm PLC byte layout for `Травление` is unchanged (union order matches the prior fixed slots).

**External / follow-up**
- Decide separately whether MOCVD/MBE configs adopt subactions (out of scope here).
- The pump example (`Откачка` → камера/шлюз → по давлению/по времени) is a depth-2
  validation case; wiring it into a real equipment config is a later config task.
