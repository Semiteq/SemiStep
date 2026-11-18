# Nested Actions (Subactions) — Reference-Based Depth-N Row Composition

## Overview

A recipe row was composed depth-1: the primary action fully determined which columns
applied to the step, and the **value** chosen in a selector column could not change the
row's composition. Nested actions generalize this to depth-N: a selector column's chosen
value pulls in a referenced **subaction** that contributes more columns, which may itself
carry a further selector, recursively. Inactive columns reuse the existing greyed
"inapplicable" state.

The data model stays flat and cheap. `Step` is unchanged
(`Step(int ActionKey, ImmutableDictionary<PropertyId, PropertyValue>)`), the grid keeps one
global flat column set, and the PLC write path is untouched. The tree lives only in
metadata and is collapsed at config load into two derived facts per primary action:

- **Column union** — every column reachable through `targets`, materialized into
  `ActionDefinition.Properties` in a deterministic order. This is the storage/serialization
  view that PLC write, CSV, clipboard, and import validation iterate.
- **Per-column activation condition** — which selector value(s) up the chain make a column
  active. This is the runtime UI view, feeding dynamic greying.

Problem it solves: operator-set values on subaction columns were silently lost on PLC
write/import if those columns were not on the primary action, and selectors could not change
row composition. Concretely, RIE `Травление` with `icp_match = Авто` must hide the ICP
capacitor columns (`icp_load`, `icp_tune`); with `Ручной` they appear with defaults.

## Data model: the tree is metadata, the row stays flat

- **`Step` is flat.** No action path, no per-generation buckets, no nested sub-steps.
- **Grid columns are global and flat.** An Avalonia `DataGrid` cannot have per-row columns;
  one global set is built from the registry once. Unchanged.
- **The tree lives in configuration/metadata only.** A selector column references a subaction
  by id; the subaction is a named, reusable column bundle.
- **At runtime the tree manifests as dynamic applicability.** Which of the global columns are
  active for a row is a function of the resolved action plus the current selector values,
  recomputed when a selector changes. Inactive columns are the existing greyed inapplicable
  state — non-editable, serialized as default.

## Configuration shape

A selector column carries `targets: {selectorValue → actionId}`. The referenced action is a
subaction. Actions and subactions are distinguished by an explicit `role`:

- `role: action` (the default; may be omitted) — a primary action. Appears in the operator
  dropdown. Existing configs carry no `role`, so they default to `action` with zero migration.
- `role: subaction` — reachable only through some column's `targets`. Never appears in the
  dropdown and never enters the registry's runtime action collections.

Targets are stated per column entry within an action, not on the global group. The same group
(`match_mode`) can therefore branch differently per selector (`icp_match` and `rie_match`
target different subactions). Only the value(s) that change composition carry a target; an
unlisted value (e.g. `Авто`) simply pulls in nothing.

```yaml
# actions/process.yaml — primary action (role omitted = action)
300:
  ui_name: "Травление"
  columns:
    # ... gas flows, gate columns ...
    - { key: icp_power, default_value: "0", property_type_id: power_icp }
    - key: icp_match
      group_name: match_mode            # 1 = Авто, 2 = Ручной
      property_type_id: enum
      targets: { 2: 3002 }              # value 2 (Ручной) -> subaction 3002
    - { key: rie_power, default_value: "0", property_type_id: power_rie }
    - key: rie_match
      group_name: match_mode
      property_type_id: enum
      targets: { 2: 3003 }
    - { key: step_duration, default_value: "10", property_type_id: time }
    - { key: comment, property_type_id: string }

3002:
  ui_name: "ICP ручной"
  role: subaction
  columns:
    - { key: icp_load, default_value: "50", property_type_id: percent }
    - { key: icp_tune, default_value: "50", property_type_id: percent }
3003:
  ui_name: "RIE ручной"
  role: subaction
  columns:
    - { key: rie_load, default_value: "50", property_type_id: percent }
    - { key: rie_tune, default_value: "50", property_type_id: percent }
```

Sharing is allowed **across distinct roots**: a subaction may be referenced by more than one
primary action, so the reference structure is a DAG, not a strict tree. Each root resolves the
shared subaction independently with its own activation path. Sharing a subaction across two
branches of the **same** root is rejected at load (see Validation rules) — its columns can carry
only one activation path, so the second branch would be greyed wrongly.

## Load-time resolver

`ActionTreeResolver` (`SemiStep.Core/Recipes/ActionTreeResolver.cs`) runs once at registry
construction over the raw mapped actions. For every `role: action` root it walks each selector
column's `targets` transitively and produces a resolved primary `ActionDefinition`. Subactions
are consumed during the walk and are not part of the output, so they never reach the registry's
`_actionsById` / `_allActions` / `_actionsByName`, the dropdown, or `GetDefaultActionId()`.

The walk produces two structures in a single pass:

### Column union (deterministic order)

Columns are spliced in declaration order, depth-first at the selector site: a selector's
subaction columns are inserted immediately after the selector column. Keys are deduped — the
first occurrence wins; a later reachable occurrence of the same key with the **same** property
type **and the same activation path** is accepted, a **conflicting** property type or a
**different activation path within one root** fails the load.

For action `300` above the union is, in order:

```
... gas flows, gate columns ...,
icp_power, icp_match, icp_load, icp_tune,
rie_power, rie_match, rie_load, rie_tune,
step_duration, comment
```

The union becomes `ActionDefinition.Properties`. The resolved `Targets`/`Activation` are written
back onto each column: `Targets` is cleared on the materialized column, and `Activation` is set
to the chain of conditions on its path (`null` for always-active columns).

### Per-column activation condition

Each column carries a list of `ActivationCondition(SelectorKey, EnablingValue)` — one entry per
selector on its path from the root. `icp_load`/`icp_tune` carry `(icp_match, 2)`;
`rie_load`/`rie_tune` carry `(rie_match, 2)`; everything else carries none. A column is active iff
**every** condition on it is met, which gives depth>1 chains for free.

### PLC byte-order invariant

The PLC writes values into fixed slots by `Properties` order. The union after splicing must
reproduce the prior column order byte-for-byte so the slot layout is unchanged: for RIE `300`,
`rie_power` stays between the icp-manual splice and `rie_match`, and the gate/flow columns keep
their positions. This is a hard invariant, regression-tested by serializing a fixed `Травление`
step and asserting an identical `PlcRecipeData` slot layout. Inactive branch columns write
`0`/empty into their reserved slots; the layout never shifts.

## Correction to the original design note

The original note (now superseded) claimed PLC write, CSV, and clipboard were "already
generation-tolerant, no change". That was only true **once the column union is materialized into
`Properties`**. Those consumers iterate `action.Properties`; a subaction column absent from it has
its value silently dropped on PLC write / CSV export / clipboard / import validation. Union
materialization is the load-bearing step that makes them tolerant — name it explicitly. With the
union in place those consumers need no code change; without it they are broken for subaction
columns.

The original note also modeled role membership by inference ("roots = actions referenced by
nobody") and showed an explicit auto-child (`3001`). The implemented model uses an explicit
`role` enum and lets unlisted selector values pull in nothing, so no empty auto-subaction exists.

## Validation rules (config load, hard errors)

`CrossReferenceValidator` (`SemiStep.Core/Configuration/Validation/CrossReferenceValidator.cs`)
enforces, at load, with a clear message each:

- every `targets` id resolves to a defined action (dangling target fails);
- every `targets` id points at a `role: subaction` (a target on a `role: action` fails — catches
  a mis-tagged fragment);
- every `role: subaction` is referenced by at least one `targets` (orphan subaction fails);
- no cycle in the reference graph (a cycle reachable from any action fails);
- no shared column across branches of one root — a column reachable within a single root via two
  different selector conditions is rejected at config load (OR-activation is unsupported);
- action ids are unique (enforced earlier by the loader across files; uniqueness across files
  and for subaction ids is checked by `ActionsSectionLoader`, not `ValidateReferenceGraph`).

A column may carry only a **single** activation path. OR-activation is not supported, so a
subaction reachable from two branches of the **same** root is rejected — this includes two
selector values mapping to the same subaction (`targets: {2: X, 3: X}`), which is meaningless
(both values would activate the same columns). Cross-**root** sharing (a subaction referenced by
two different primary actions) is allowed; each root resolves it independently with its own path.

The `role: action`/`role: subaction` rule fires regardless of depth — a subaction whose own
selector targets a `role: action` also fails. The resolver additionally guards against cycles
(defense-in-depth alongside the validator) and rejects a column key reachable with conflicting
property types or, within one root, with different activation paths.

### Known gaps

- A `targets` selector **value** is not cross-checked against the column's group at config load.
  A target keyed on a value the group does not define (or a typo) is accepted; that branch is
  simply never reachable, so it silently contributes nothing rather than failing.
- `role` accepts only `null`/`"action"`/`"subaction"`. Any other string fails mapping at config
  load.

## Runtime: dynamic applicability

`ActiveColumnSetResolver` (`SemiStep.Core/Recipes/Helpers/ActiveColumnSetResolver.cs`) computes a
step's active column set from the resolved action's per-column activation conditions and the
step's current selector values. A column with no conditions is always active; a column with
conditions is active iff every selector named in its conditions currently holds the enabling value.

`CellStateResolver.IsInapplicable` (`SemiStep.Core/Recipes/Helpers/CellStateResolver.cs`) consults
this active set: a cell is inapplicable iff it is not the action column, not read-only, and its key
is not in the active set. Inactive columns reuse the greyed inapplicable palette; no new style.

In the UI, `RecipeRowViewModel.InapplicableColumns` is an observable property. On recompute it is
assigned a **new** `IReadOnlySet<string>` instance so the one-way cell binding re-fires; mutating
in place would not notify. Selector edits route through the grid view-model into the batched core
mutation; ordinary (non-selector) edits do not trigger an applicability recompute.

## One-undo-unit selector edit

The undo boundary is `RecipeSession.Apply` — one call, one undo snapshot.
`RecipeSession.UpdateStepForSelectorChange` (`SemiStep.Core/Recipes/RecipeSession.cs`) builds a
single updated step that sets the selector value, drops the keys that became inactive, seeds
defaults for the keys that became active, runs any formula recalc, and calls `Apply` exactly once.
Switching `icp_match` to `Авто` and back is therefore a single undo unit: `Ctrl+Z` restores both
the prior selector value and the prior dropped load/tune values in one step.

## Formula guard

`FormulaEvaluator` (`SemiStep.Core/Recipes/Formulas/FormulaEvaluator.cs`) takes the row's active
column set. In `BuildVariableMap`, when a `recalc_order` variable is not in the active set, the
recalc is skipped and the step is returned unchanged, rather than throwing. An active variable that
is absent or non-numeric is still a genuine error and throws. The speculative config-time
cross-branch formula ban from the original design was dropped (YAGNI).

## What stays untouched

- `Step` is flat; the grid keeps one global flat column set.
- The PLC write path is unchanged; it iterates `Properties` (now the union) and writes defaults
  for absent values, as before.
- CSV, clipboard, and import validation iterate `Properties` and benefit from the union with no
  code change.
- The greyed inapplicable palette is reused; no new visual style.

## Acceptance criteria (met)

- RIE `Травление`: `icp_match = Авто` greys `icp_load`/`icp_tune` and drops their values;
  `Ручной` makes them applicable with defaults; `rie_match` is independent.
- `Ctrl+Z` after switching to `Авто` restores `Ручной` with the prior load/tune values in one step.
- An inactive capacitor column writes `0`/empty into its fixed PLC slot; the slot layout is
  unchanged byte-for-byte.
- A depth-2 tree (a subaction carrying its own selector with `targets`) resolves and activates
  along the full condition chain.
- `GetDefaultActionId()` and the dropdown never surface a subaction.
- Backward compatibility: actions without `targets`/`role` behave exactly as before; the dropdown
  for existing configs is unchanged.
