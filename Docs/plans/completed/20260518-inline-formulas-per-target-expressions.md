# Inline Formulas for MBE Actions — Per-Target Explicit Expressions

## Overview

Restore "inline cell formula" functionality (issue #20). When the user edits a numeric cell that participates in a formula, the engine recalculates a coupled cell on the same row so the underlying physical relation stays satisfied. Example: in `t°C плавно` the relation `(task − initial_value) / speed · 60 = step_duration` must hold; if the user edits `task`, then `step_duration` is recomputed and written back.

The original SemiStep formula subsystem was deleted in commit `c14e96b` as dead code (the runtime existed but no compiler produced solver lambdas, so the feature was never reachable). NtoLib re-implemented the concept around AngouriMath (symbolic equation + symbolic `Solve()` per target). This plan introduces a **lighter** equivalent: config carries one closed-form expression per recalc target, evaluator is a thin wrapper over NCalc. No symbolic math.

Trigger frequency is "one cell-commit per user edit", so performance is a non-concern. Correctness, validation, and testability dominate.

## Context (from discovery)

**Files/areas involved:**

- `ConfigFiles/MBE/actions/*.yaml` — destination for new `formula:` blocks (initially `heaters.yaml` actions `110` and `140`).
- `SemiStep.Core/Configuration/Dto/ActionDto.cs`, `ActionColumnDto.cs` — DTOs read from YAML.
- `SemiStep.Core/Configuration/Mapping/ActionMapper.cs` — DTO → `ActionDefinition`.
- `SemiStep.Core/Recipes/ActionDefinition.cs` — domain action record; gains optional `Formula`.
- `SemiStep.Core/Recipes/RecipeSession.cs:380` (`UpdateStepProperty`) — integration point.
- `SemiStep.Core/Recipes/Step.cs`, `PropertyValue.cs` — used by evaluator.
- `SemiStep.Tests/YamlConfigs/Standard/`, `SemiStep.Tests/YamlConfigs/Invalid/` — overlay test fixtures.

**Related patterns found:**

- Config layering: DTO → Mapper → domain record. `ConfigError.WithDetail(...)` for structured failures. Tests use overlay pattern (`Standard/` + diff in `Invalid/<Case>/`).
- Mutation flow: `RecipeRowViewModel.SetPropertyValue` → `PropertyValueChanged` → `RecipeGridViewModel.OnCellValueChanged` → `RecipeCoordinator.UpdateStepProperty` → `RecipeSession.UpdateStepProperty` → `Step.WithProperty` → `Recipe.ReplaceStep` → `Apply` (analyzes + pushes history). The recalculation belongs **inside `RecipeSession.UpdateStepProperty`**, applied to `updatedStep` before `ReplaceStep`, so a formula recalc shares the same undo unit as the originating edit.
- DI registration via extension methods (`AddRecipe()` etc.) in `RecipeDi.cs`.
- Action change resets the entire step via `StepInitializer.Create`, so "user changes action while a formula-coupled cell is mid-edit" is implicitly safe — no special handling required.
- CSV import (`SemiStep.Core/Recipes/Import/CsvService.cs`) writes step properties through a path that does *not* call `UpdateStepProperty` — see "Scope boundary" below.

**Dependencies identified:**

- NuGet `NCalcSync` (sync API, MIT, .NET 8/10 compatible). Single package. Pinned to **`NCalcSync 5.4.2`** (latest stable at plan-creation time).
- AngouriMath is rejected — symbolic `Solve()` is the cost centre we eliminate.

**Reference (NtoLib, read-only):**

- `../NtoLib/NtoLib/Recipes/MbeTable/ModuleConfig/Formulas/FormulaPrecompiler.cs` — what we are *not* doing.
- `../NtoLib/NtoLib/Recipes/MbeTable/ModuleCore/Formulas/CompiledFormula.cs:91-94` (`DetermineTarget`) — confirms the target-selection rule (`recalc_order.FirstOrDefault(v => v != changed)`); we deliberately preserve identical semantics.
- `../NtoLib/DefaultConfig/MBE/ActionsDefs.yaml` — example of how recalc_order is authored.
- **`Semiteq/NtoLib#35`** (closed, milestone v1.9.1) — the original "Bidirectional Formula Engine" spec. Most semantics carry over (target rotation, double storage, init-vs-runtime error split, ~100ms budget which we are far under). Deliberate divergences in this plan: (a) **per-target explicit expressions**, removing the linearity restriction from #35 ("`area = width * height` ❌", "`sin(angle)` ❌" become allowed — the author writes the inverses); (b) **NCalc** replacing AngouriMath; (c) UI choice — *target rotates per edit*, not a static computed cell.

## Development Approach

- **Testing approach: Regular** (project convention — implement, then write tests in the same task).
- Each task ends with new/updated tests, all passing, before the next task starts.
- `dotnet format SemiStep/SemiStep.slnx` before any commit (pre-commit hook).
- Backward compatibility: actions without `formula:` continue unchanged. `Formula` on `ActionDefinition` is nullable.
- **Every new test file carries `[Trait("Component", "Core")]` or `[Trait("Component", "Config")]` per `CLAUDE.md`.** Integration tests additionally carry `[Trait("Category", "Integration")]`.

## Testing Strategy

- **Unit tests** mandatory per task (Component=Core or Config).
- **Integration tests** for the full path `RecipeSession.UpdateStepProperty → recalc → Step → Apply → Result.Reasons` (Component=Core, Category=Integration). One test in Task 4 explicitly asserts that a recalc failure's reason propagates through `Apply` into the outgoing `Result`.
- **Config-loading tests** via the overlay pattern: valid block in `Standard/`; one overlay per failure mode in `Invalid/Formula*/`.
- No new UI behaviour — existing chain shows recalculated cells because `Apply` emits `MutationSignal.PropertyUpdated` and rows call `UpdateStep`. **Manual verification at end** confirms the cell visibly updates in the running app.

## Scope Boundary — CSV Import

CSV import is **out of scope** for this plan and uses **verbatim semantics**: values from CSV are loaded as-is, formulas are *not* re-evaluated, no validation warning is raised if a CSV-loaded step violates a formula. Rationale: CSV authors are responsible for internal consistency; opening a recipe authored elsewhere should not silently mutate the user's data. This is a deliberate decision, not an oversight. The Docs chapter on import will state this explicitly (Task 7).

## Solution Overview

YAML on an action:

```yaml
110:
  ui_name: "t°C плавно"
  deploy_duration: immediate
  columns: [...]
  formula:
    recalc_order: [step_duration, speed, task, initial_value]
    expressions:
      step_duration: "(task - initial_value) / speed * 60"
      speed:         "(task - initial_value) / step_duration * 60"
      task:          "initial_value + speed * step_duration / 60"
      initial_value: "task - speed * step_duration / 60"
```

`recalc_order` semantics (one sentence, stated for the config author):
**target = first entry in `recalc_order` that is not the variable the user just changed.** The list is therefore a priority order: the user's edit is preserved, the highest-priority other variable absorbs the change. Identical to NtoLib's `DetermineTarget`.

Algorithm on cell change inside `RecipeSession.UpdateStepProperty`:

1. Parse + validate the new value, build `updatedStep` (existing logic — `PropertyParser.Parse` + `PropertyValidator.Validate` + group check).
2. If `action.Formula is not null` and `changedColumnKey ∈ formula.RecalcOrder`:
   - **Pre-check keys.** Verify every variable in `formula.RecalcOrder` is present in `updatedStep.Properties` as a numeric `PropertyValue`. Any missing entry → `InvalidOperationException` (programmer error; the mapper guarantees this cannot happen at runtime — see Errors section). This is an explicit upfront fail-fast, not a silent fallback through NCalc's variable-not-found path.
   - **Select target.** `target = formula.RecalcOrder.FirstOrDefault(v => !string.Equals(v, changedColumnKey, OrdinalIgnoreCase))`.
   - **Evaluate.** Build the variable dictionary (only `recalc_order` keys, values via `PropertyValue.AsDouble()`). Construct `new NCalc.Expression(formula.CompiledExpressions[target])` — the `LogicalExpression` is precompiled at config-load (see Task 3); the runtime construction here is parse-free and cheap. Assign `Parameters`, call `Evaluate()`, cast to `double`, guard against NaN/Infinity.
   - **Convert + validate.** Convert the result to a `PropertyValue` using the target column's `PropertyType`. For integer targets (`system_type: int`), use `Math.Round(value, MidpointRounding.ToEven)` (banker's rounding — matches the .NET default and avoids systematic bias when many edits accumulate). For float targets, keep the value as-is. Run `PropertyValidator.Validate` for type + bounds. Out-of-range → `FormulaTargetOutOfRangeError`.
3. **Failure policy: reject the originating edit on recalc failure.** Either of {`FormulaComputationFailedError` (NaN/Infinity/divide-by-zero/runtime evaluation exception), `FormulaTargetOutOfRangeError`} causes `UpdateStepProperty` to return `Result.Fail` with the structured reason. The recipe stays untouched. Symmetric with the existing `PropertyValidator` failures on lines 427-437 of `RecipeSession`, which already reject the edit. Programmer-error guards (`InvalidOperationException` from precondition or missing-variable pre-check) bubble as exceptions — they should never fire in a correctly loaded config.
4. On success: `updatedStep = updatedStep.WithProperty(target, newPropertyValue)`. Proceed to `current.ReplaceStep(stepIndex, updatedStep)` → `Apply` as before.

**On precompilation and "unsolvable formulas slipping through":**

- **Syntactic solvability** — that an expression parses, references only declared variables, and is structurally well-formed — is verified at config-load (Task 3). The parser output (`NCalc.LogicalExpression`) is then **stored on `FormulaDefinition`** and reused at runtime. No re-parsing on keystroke. An unparseable or structurally invalid formula cannot reach runtime — the config fails to load.
- **Runtime solvability** — whether a particular *evaluation* succeeds for the current numeric values — is inherently data-dependent (e.g. `a/b` with `b=0`) and cannot be precompiled away. These are the `FormulaComputationFailedError` cases handled by the failure policy.
- **Performance** — even without the LogicalExpression cache, NCalc parsing is sub-millisecond on expressions of this size and the trigger rate is "one per cell commit". With the cache it is essentially free. Cache is correctness/cleanliness, not optimization.

Design decisions:

- **Per-target expressions, not one equation.** No symbolic-math dependency; validation reduces to "expression parses + every variable it references is in `recalc_order`". Nonlinear couplings become possible. Cost: config author writes N algebraic rearrangements.
- **NCalc with precompiled `LogicalExpression` cached on `FormulaDefinition`.** Parsing happens once at config-load (Task 3). At runtime the evaluator wraps the cached `LogicalExpression` in a fresh `NCalc.Expression` instance (parse-free, microseconds, mutable `Parameters` therefore not shared) — thread-safe, evaluator registered as singleton. **AST immutability contract**: NCalcSync 5.x `LogicalExpression` nodes (`BinaryExpression`, `UnaryExpression`, `Identifier`, `ValueExpression`, `Function`, `TernaryExpression`) expose getters only and store children passed via constructor — sharing a single `LogicalExpression` across many wrapper `Expression` instances is safe as long as no code path mutates the AST. The evaluator only assigns `Parameters` and calls `Evaluate()`, neither of which touches the AST.
- **Reject on failure.** Preserves the invariant that "the formula always holds in a saved recipe". Bug-safe round-tripping. Cell editing UX: the user's commit is rejected as if `PropertyValidator` had rejected it (existing well-known UX path; the rejected edit surfaces as a latest-only operation outcome via `MessagePanelViewModel.ReportError` (the operation slot in the unified message panel), not as a validation reason — the panel's validation rows reflect the unchanged, still-valid recipe).
- **Single undo unit.** Because the recalc is applied *before* `Apply` calls `PushHistory`, originating edit + coupled recalc collapse into one undo step.
- **`FormulaEvaluator` is UI-free.** Depends only on `RecipeMetadataRegistry` and `ILogger`. User-facing messaging is the caller's responsibility (the coordinator already routes `Result.Reasons` to `_messagePanel`).
- **No static "computed cell" — target rotates per edit.** Whichever variable the user did not just edit becomes the target for that commit. All cells in a formula-coupled action remain editable; there is no permanent read-only "output" cell. This is the explicit answer to the UI question left open in `NtoLib#35` ("блокировать или переключать target?") — we choose *переключать*.
- **Nonlinear couplings are allowed.** Because each direction of the formula is authored explicitly, the symbolic-linearity restriction from `NtoLib#35` (which forbids `area = width * height`, `power = mass * acceleration`, function-of-variable, etc., because `AngouriMath.Solve()` cannot handle them) **does not apply here**. The config author can express any nonlinear relation by writing the N inverted expressions themselves. The price is purely manual algebra.
- **Built-in math functions available.** NCalc natively exposes `Abs`, `Sqrt`, `Pow`, `Sin`, `Cos`, `Tan`, `Log`, `Exp`, `Min`, `Max`, `Round`, `Truncate` (full list in NCalc docs). All available in expressions without registration. Custom functions are out of scope for this plan — if a future formula needs one, register via NCalc's `EvaluateFunction` event in a follow-up.

## Technical Details

**Variable extraction from NCalc expressions** (pinned in Task 1):

NCalcSync 5.x exposes the parsed AST via `expression.LogicalExpression` after a call to `Compile()` (or implicitly on first `Evaluate()`). Static identifier extraction walks the AST visiting `Identifier` nodes; this is the chosen mechanism. Alternative (dry-evaluate with `EvaluateParameter` event recording lookups) is rejected because it requires actually executing the expression. The AST walk is implemented as a small visitor in `FormulaEvaluator` (or a helper class if reused).

**New domain type** (`SemiStep.Core/Recipes/Formulas/`):

```csharp
public sealed class FormulaDefinition
{
    public IReadOnlyList<string> RecalcOrder { get; }
    public IReadOnlyDictionary<string, string> ExpressionSources { get; }       // for diagnostics / serialization round-trips
    public IReadOnlyDictionary<string, LogicalExpression> CompiledExpressions { get; }  // precompiled, parse-free at runtime
    // constructor takes all three; only the mapper builds instances
}
```

Class, not record — `LogicalExpression` has no value equality, and structural equality on `FormulaDefinition` has no production use case. `ActionDefinition` gains nullable `FormulaDefinition? Formula`.

**Evaluator** (`SemiStep.Core/Recipes/Formulas/FormulaEvaluator.cs`):

```csharp
public sealed class FormulaEvaluator(ILogger<FormulaEvaluator> logger)
{
    public Result<Step> Recalculate(
        Step step,
        ActionDefinition action,
        string changedColumnKey,
        RecipeMetadataRegistry registry);
}
```

Precondition (API contract): `action.Formula is not null`. Violation → `InvalidOperationException` — genuine programmer error, caller should have branched.

Caller-side filter (NOT an API precondition): `changedColumnKey ∈ formula.RecalcOrder`. The caller (`RecipeSession.UpdateStepProperty`) gates the call with `action.Formula is not null && action.Formula.RecalcOrder.Contains(changedColumnKey, OrdinalIgnoreCase)`. Edits to non-formula columns of a formula-enabled action (e.g. `comment` on `t°C плавно`) bypass the evaluator entirely — they are normal cell edits, not "evaluator called with a column outside recalc_order". Calling `Recalculate(..., "comment", ...)` on a formula-enabled action is undefined behaviour (the evaluator may throw or return a malformed Result); the caller's filter ensures it never happens.

**Errors** (`SemiStep.Core/Recipes/Formulas/Errors/`) — two classes:

- `FormulaComputationFailedError(target, reason)` — NCalc threw at evaluate-time, or result is NaN/Infinity, or divide-by-zero.
- `FormulaTargetOutOfRangeError(target, value, min, max)` — recalculated value failed `PropertyValidator`.

Other previously considered errors are demoted to `InvalidOperationException` (programmer-error guards):

- `FormulaTargetNotFoundError`, `FormulaExpressionMissingError` — Task 3 mapper validation makes them unreachable.
- **`FormulaVariableMissingError` — demoted as of this revision.** Reachable only if (a) the mapper's `recalc_order ⊆ action.columns` check has a hole, or (b) `Step.Properties` was constructed with a key set that does not match `action.columns`. Both are programmer errors, not user-authoring errors: `StepInitializer.Create` produces step properties from the action's column set, `Step.WithProperty` updates an existing key (never adds), and the mapper guarantees `recalc_order` columns exist on the action. The upfront pre-check in `FormulaEvaluator` (Solution Overview step 2) therefore throws `InvalidOperationException` if a recalc-order variable is missing from the step.

**Config DTO additions** (`Configuration/Dto/`):

- `FormulaDto { List<string>? RecalcOrder; Dictionary<string, string>? Expressions; }` — owned by `ActionDto` as `FormulaDto? Formula`.

**Mapper** (`Configuration/Mapping/ActionMapper.cs`):

New private method `MapFormula(actionId, formulaDto, columnKeys) → Result<FormulaDefinition?>`:

- Returns `Result.Ok<FormulaDefinition?>(null)` if `formulaDto is null`.
- Validates `recalc_order` has ≥ 2 distinct entries.
- Validates each `recalc_order` entry matches a column key of the action.
- Validates `expressions` covers every `recalc_order` entry (no missing key).
- Validates `expressions` has no extra key not in `recalc_order` (kept as self-describing constraint — `recalc_order` is the single source of truth for participating variables; an orphan expression entry is almost always a typo).
- Pre-parses each expression: `var expr = new NCalc.Expression(src); expr.Compile();` then keeps `expr.LogicalExpression` for the cache. Catches `EvaluationException`/`NCalcParserException`/`ArgumentException` as parse errors.
- Walks each parsed `LogicalExpression` (via `LogicalExpressionVisitor`) and verifies every identifier is in `recalc_order`.
- Builds the `CompiledExpressions` dictionary as part of the returned `FormulaDefinition`; sources are kept in `ExpressionSources` for diagnostics.
- Aggregates failures as `ConfigError` with `section: "actions/<file>.yaml"` and details `actionId/actionName/target/expression`.

**DI** (`Recipes/RecipeDi.cs`): register `FormulaEvaluator` as singleton.

**Integration** (`RecipeSession.cs` line 380):

- Inject `FormulaEvaluator` via constructor (added parameter).
- After `var updatedStep = step.WithProperty(columnKey, parsedValue);` (line 439), branch on `action.Formula is not null && action.Formula.RecalcOrder.Contains(columnKey, OrdinalIgnoreCase)`:
  - Call `_formulaEvaluator.Recalculate(updatedStep, action, columnKey, _recipeMetadataRegistry)`.
  - On `Result.IsFailed`: return the failure as-is (reject edit). Log info-level.
  - On success: replace `updatedStep` with `Result.Value`.
- Proceed to `current.ReplaceStep(stepIndex, updatedStep)` → `Apply` unchanged.

## What Goes Where

- **Implementation Steps** (`[ ]`): all code, DTOs, mappers, evaluator, errors, DI, fixture YAML updates, tests.
- **Post-Completion** (no checkbox): manual UI verification, algebraic sanity check on the four expressions in `heaters.yaml`, acceptance criteria walkthrough.

## Implementation Steps

### Task 1: Add NCalc dependency, pin variable-extraction approach, add `FormulaDefinition`

**Files:**
- Modify: `SemiStep/Directory.Packages.props`
- Modify: `SemiStep/SemiStep.Core/SemiStep.Core.csproj`
- Create: `SemiStep/SemiStep.Core/Recipes/Formulas/FormulaDefinition.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/Formulas/FormulaIdentifierExtractor.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/ActionDefinition.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Recipes/Formulas/FormulaIdentifierExtractorTests.cs`

- [x] Add `<PackageVersion Include="NCalcSync" Version="5.4.2" />` to `Directory.Packages.props`.
- [x] Reference `NCalcSync` from `SemiStep.Core.csproj`.
- [x] Create `FormulaDefinition` as a **sealed class** under `SemiStep.Core.Recipes.Formulas` (not a record — see Technical Details for rationale).
- [x] Create `FormulaIdentifierExtractor` — a static class with `Extract(string source) → Result<IReadOnlySet<string>>` that constructs `new NCalc.Expression(source)`, calls `Compile()` to force parse, then walks the AST via `NCalc.Domain.LogicalExpressionVisitor` (override-based dispatch through `logicalExpression.Accept(visitor)`, NOT external recursion). Visitor records `Identifier.Name` only for identifier nodes appearing in `BinaryExpression`/`UnaryExpression`/`TernaryExpression` argument positions — function-name identifiers in `Function` nodes are **excluded** from the variable set. Result: case-insensitive set of variable names. On parse exception, returns failure. (Implementation note: NCalcSync 5.4.2 exposes `ILogicalExpressionVisitor<T>` as an interface, not abstract class. Extractor delegates to the library's built-in `Expression.GetParameterNames()` which uses `ParameterExtractionVisitor` — verified by decompilation to apply the exact same Function-name exclusion rule. Equivalent to the spec'd visitor while avoiding reimplementation of identical logic.)
- [x] Add nullable `FormulaDefinition? Formula` to `ActionDefinition`. **Note:** during this work `ActionDefinition` was converted from a positional record to a sealed class (commit 7835e09), so it now uses reference equality by default. `FormulaDefinition` is also a class without a custom `Equals`; two `ActionDefinition`s with separately-constructed `Formula` instances are not considered equal even if their sources match.
- [x] Verify all existing `ActionDefinition` callers compile.
- [x] Tests for `FormulaIdentifierExtractor`: simple expression `"a + b"` returns `{a, b}`; nested `"(x - y) / z * 60"` returns `{x, y, z}`; expression with literals only `"3.14 * 2"` returns empty set; **built-in function disambiguation: `"sqrt(a*a + b*b)"` returns `{a, b}` only (NOT `sqrt`)**; **`"pow(x, 2) + abs(y)"` returns `{x, y}` only**; unparseable `"a +"` returns failure; case-insensitivity (`A` and `a` map to same identifier).
- [x] `dotnet build SemiStep/SemiStep.slnx` clean; `dotnet test` green.

### Task 2: `FormulaEvaluator` with the recalculation algorithm

**Files:**
- Create: `SemiStep/SemiStep.Core/Recipes/Formulas/FormulaEvaluator.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/Formulas/Errors/FormulaComputationFailedError.cs`
- Create: `SemiStep/SemiStep.Core/Recipes/Formulas/Errors/FormulaTargetOutOfRangeError.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Recipes/Formulas/FormulaEvaluatorTests.cs`

- [x] Implement `FormulaEvaluator.Recalculate` per Solution Overview. Construct a fresh `NCalc.Expression` from the cached `LogicalExpression` (`new Expression(formula.CompiledExpressions[target])`) — parse-free wrap. Assign `Parameters`, evaluate, cast to `double`, NaN/Infinity guard.
- [x] Explicit upfront pre-check: every `recalc_order` variable is present in the step's properties as a numeric `PropertyValue`; missing → `InvalidOperationException` (programmer-error guard).
- [x] Precondition guard via `InvalidOperationException` when caller passes `action.Formula = null`.
- [x] Two error classes as listed (`FormulaComputationFailedError`, `FormulaTargetOutOfRangeError`).
- [x] Integer-target conversion uses `Math.Round(value, MidpointRounding.ToEven)`; float-target conversion is a direct cast.
- [x] Tests — success ramp formula, change `task` 500→700 with `initial_value=500, speed=10, step_duration=600`: expect `step_duration` = 1200.
- [x] Tests — symmetric: change `speed` 10→20 with same baseline, expect `step_duration` recomputed; verify target follows recalc_order priority.
- [x] Tests — divide-by-zero (`speed=0`, change `task`): expect `FormulaComputationFailedError`.
- [x] Tests — NaN/Infinity guard.
- [x] Tests — out-of-range result: target with tight `temp` bound, algebra overflows: expect `FormulaTargetOutOfRangeError`.
- [x] Tests — integer-target rounding: configure a synthetic action with an `int`-typed target column, compute a value that lands on `.5` (e.g. expression result `4.5`); expect `Math.Round` to `ToEven` yields `4` (not `5`). One additional case for `3.5 → 4`.
- [x] Tests — programmer-error guards: call with `Formula = null` throws `InvalidOperationException`; call where a recalc-order variable is missing from `Step.Properties` throws `InvalidOperationException`.
- [x] Green.

### Task 3: Formula DTO + Mapper with config-load validation

**Files:**
- Create: `SemiStep/SemiStep.Core/Configuration/Dto/FormulaDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Dto/ActionDto.cs`
- Modify: `SemiStep/SemiStep.Core/Configuration/Mapping/ActionMapper.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Configuration/Mapping/ActionMapperFormulaTests.cs`

- [x] `FormulaDto { List<string>? RecalcOrder; Dictionary<string, string>? Expressions; }`.
- [x] Wire `FormulaDto? Formula` onto `ActionDto`.
- [x] `ActionMapper.MapFormula` enforces all rules from Technical Details (≥ 2 recalc entries, entries match action columns, expressions covers exactly recalc_order keys both ways, every expression parses, every identifier in expression is in recalc_order). Use `FormulaIdentifierExtractor` from Task 1.
- [x] Failures as FluentResults `Result` failures with `section`, `actionId`, `actionName`, `target`, `expression` content (codebase uses string-message Errors via FluentResults, no `ConfigError` class exists).
- [x] Tests: valid block; missing expression for one recalc entry; extra expression key not in recalc_order; unparseable expression; expression referencing variable absent from recalc_order; recalc_order entry referencing a column not on the action; recalc_order has only one entry; duplicate entries in recalc_order.
- [x] Green.

### Task 4: Register `FormulaEvaluator`, wire into `RecipeSession.UpdateStepProperty`, integration tests

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeDi.cs`
- Modify: `SemiStep/SemiStep.Core/Recipes/RecipeSession.cs`
- Create (or modify): `SemiStep/SemiStep.Tests/Core/Recipes/RecipeSessionFormulaIntegrationTests.cs`

- [x] Register `FormulaEvaluator` as singleton in `AddRecipe()`.
- [x] Inject `FormulaEvaluator` into `RecipeSession`.
- [x] In `UpdateStepProperty` (after line 439): if `action.Formula is not null` and `changedKey in RecalcOrder`, call `Recalculate`; on failure return `Result.Fail` with the reason (reject edit), on success use the returned step.
- [x] Verify undo invariant: one user edit that triggers a recalc grows `_undoStack` by exactly one entry.
- [x] Integration test — success: build a `RecipeSession` with a real `RecipeMetadataRegistry` carrying a formula-enabled action; mutate `task`, assert `step_duration` updates on the resulting Step; assert `_undoStack.Count` grew by 1.
- [x] Integration test — reject: mutate with values that force divide-by-zero; assert `Result.IsFailed`; assert recipe is unchanged (`Current.Steps[stepIndex]` equals pre-mutation); assert `_undoStack` did not grow.
- [x] Integration test — reason propagation: capture the failure `Result`, assert its `Errors` contains a `FormulaComputationFailedError`; assert this propagates through `RecipeCoordinator.UpdateStepProperty` to a caller (mock or capture the `Result`).
- [x] Integration test — action without formula: confirm path unchanged for actions that have `Formula = null` (e.g. action `90` `t°C скачком`).
- [x] Green.

### Task 5: Add real formulas to `ConfigFiles/MBE/actions/heaters.yaml` + invalid-config fixtures

**Files:**
- Modify: `ConfigFiles/MBE/actions/heaters.yaml`
- Verify path: standard test fixture (likely `SemiStep/SemiStep.Tests/YamlConfigs/Standard/...`)
- Create: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/FormulaUnknownVariable/...`
- Create: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/FormulaUnparseable/...`
- Create: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/FormulaMissingExpression/...`
- Create: `SemiStep/SemiStep.Tests/YamlConfigs/Invalid/FormulaRecalcOrderUnknownColumn/...`
- Modify: existing invalid-config integration test

- [x] Add `formula:` blocks to action `110` (`t°C плавно`) and `140` (`P% плавно`) in `heaters.yaml`. The relation is structurally identical between the two — copy verbatim except for the keys/labels — but **the algebraic rearrangements must still be hand-verified per Post-Completion before merging**. (Adapted path: `ConfigFiles/actions/heaters.yaml` — repo layout in this worktree does not yet contain the planned `ConfigFiles/MBE/` subtree. Required columns `task`, `initial_value`, `speed` added to `ConfigFiles/columns/columns.yaml`.)
- [x] Add four overlay directories under `Invalid/Formula*/`, each with the minimal diff needed to break the standard fixture in exactly one way. (Each overlay carries an extended `columns/columns.yaml` plus a `actions/heaters.yaml` broken in exactly one way; Standard's columns intentionally stay slim so existing CSV/integration tests are unaffected.)
- [x] Wire each invalid case into the existing invalid-config integration test (the overlay-pattern test that loads `Standard/ + overlay` and asserts `ConfigError` content). (`ActionErrorTests.FormulaInvalidCase_HasExpectedError`.)
- [x] Standard fixture still loads green end-to-end. (Standard unchanged; full test suite green.)

### Task 6: Update documentation

- [x] Update `Docs/` chapter(s) describing the recipe-mutation flow to mention recalc inside `RecipeSession.UpdateStepProperty`. (Added §3.4 "Связанные параметры (inline-формулы)" in `Docs/03-data-model.md`; the SemiStep technical docs are user-facing requirements in Russian, with no separate mutation-flow chapter — data-model is the canonical home.)
- [x] Update the import/CSV chapter to state explicitly: "CSV import is verbatim; formula relations are not re-evaluated on load." (Added §3.5 "Импорт CSV" to `Docs/03-data-model.md` — no dedicated CSV chapter existed.)
- [x] Update `CLAUDE.md` if a project-wide convention is worth a one-liner. (Added a "Conventions" section noting the inline-formula route through `RecipeSession.UpdateStepProperty` and the verbatim CSV-import rule.)
- [x] Move this plan to `Docs/plans/completed/`. (deferred to finalize step)

## Post-Completion

**Acceptance walk-through:**

- Verify Overview goal end-to-end: editing each of `task` / `initial_value` / `speed` / `step_duration` in a `t°C плавно` step recalculates the priority-target per `recalc_order`.
- Verify single-undo: edit + recalc collapse into one Ctrl+Z.
- Verify rejection path: divide-by-zero and out-of-range failures reject the user's edit, recipe is untouched, and the structured reason surfaces transiently via `MessagePanelViewModel.ReportError` (the operation slot in the message panel), not the validation rows, which keep reflecting the unchanged valid recipe.
- Verify actions without `formula:` (the bulk of MBE config) behave exactly as before.

**Manual UI verification:**

- Launch UI against an MBE config, switch a step to `t°C плавно`, edit each coupled cell in turn, observe the correct coupled cell updates.
- Edit `speed` to `0`, then edit `task` — confirm the `task` edit is rejected with a clear message (no exception leaks).
- Ctrl+Z after a coupled edit, confirm both originating and recalculated values revert in a single undo step.

**Physical correctness review:**

- The four expressions added to `heaters.yaml` actions `110` and `140` must be reviewed for algebraic correctness against `(task − initial_value)/speed · 60 = step_duration`. This is a math sanity check on hand-written rearrangements — no automatic guarantee that the four expressions agree.

**External system updates:**

- None. PLC side is unaffected; formulas live entirely in the recipe editor.
