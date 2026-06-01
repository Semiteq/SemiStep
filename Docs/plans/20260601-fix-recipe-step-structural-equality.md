# Fix Recipe/Step structural equality so reconnect reconciliation matches identical content (issue #49)

## Overview
- Reconnect reconciliation decides "PLC recipe == PC recipe" via `localRecipe.Equals(plcRecipe)` (`PlcLifecycleManager.cs:320`), but `Recipe` and `Step` record equality degrades to **reference** equality because their members are `ImmutableList<Step>` / `ImmutableDictionary<PropertyId, PropertyValue>`, neither of which implements structural equality.
- Effect: a recipe freshly read from the PLC (new list/dict instances) never compares equal to the local one even when the content is byte-identical, so `!localRecipe.Equals(plcRecipe)` is effectively always true. The intended "committed=true, PLC = PC -> no action" branch is unreachable; the conflict dialog fires on every reconnect where both recipes are non-empty.
- Fix: give `Recipe` and `Step` content-based `Equals`/`GetHashCode`. `Recipe` compares `Steps` order-sensitively (`SequenceEqual`); `Step` compares `ActionKey` plus `Properties` by content (count + per-key value), order-independent. `PropertyValue` already has correct value equality and is **not** changed.

## Context (from discovery)
- Files/components involved:
  - `SemiStep/SemiStep.Core/Recipes/Recipe.cs:5` — `sealed record Recipe(ImmutableList<Step> Steps)`.
  - `SemiStep/SemiStep.Core/Recipes/Step.cs:5-7` — `sealed record Step(int ActionKey, ImmutableDictionary<PropertyId, PropertyValue> Properties)`.
  - `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs:320` — the only consumer of `Recipe.Equals`.
  - `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs` — reconnect integration tests.
- Related patterns found:
  - `PropertyValue` (`PropertyValue.cs`) is a `sealed record` over `object Value` + `PropertyType Type`. Synthesized equality routes `Value` through `EqualityComparer<object>.Default` -> `object.Equals`; boxed `int`/`float` and `string` compare by value. **Value equality already correct; leave unchanged.** (Resolves the issue's open "Confirm PropertyValue has value equality" point.)
  - `PropertyId` (`PropertyId.cs`) is a `readonly record struct` with proper value equality; safe as a dictionary key.
  - Core unit tests live under `SemiStep/SemiStep.Tests/Core/Unit/Recipes/` with traits `[Trait("Category","Unit")] [Trait("Component","Core")] [Trait("Area","<name>")]` (see `RecipeMetadataRegistryTests.cs:12-14`).
  - `StubPlcSyncService.NotifyRecipeChangedCallCount` is the deterministic completion signal used by existing reconnect tests for the "push local recipe" path; `NotifyLocalRecipe()` (`PlcLifecycleManager.cs:368-371`) calls `_syncService.NotifyRecipeChanged`, so the equal branch increments that counter.
- Dependencies / blast radius:
  - Grep confirms **no** `HashSet<Recipe>`/`HashSet<Step>`/`Dictionary<Recipe>`/`Dictionary<Step>` anywhere, and the only direct `Recipe.Equals` call is `PlcLifecycleManager.cs:320`. Changing structural equality on the records is therefore safe; no other call site depends on the old reference semantics.

## Development Approach
- **Testing approach**: Regular (code first, then tests).
- Complete each task fully before moving to the next.
- Make small, focused changes.
- **Every task includes new/updated tests.** Tests are a required deliverable, not optional.
- **All tests must pass before starting the next task.**
- Run tests after each change. Keep this plan in sync if scope shifts.

## Testing Strategy
- **Unit tests**: required for every task. New file `RecipeStructuralEqualityTests.cs` for `Recipe`/`Step` equality and hash-code consistency.
- **Integration tests**: extend `PlcLifecycleManagerReconnectTests` with the identical-content -> no-conflict case (the reconnect path is integration-traited).
- **e2e tests**: project has no Playwright/Cypress UI e2e suite; Avalonia headless tests are not relevant to this Core-only change.

## Progress Tracking
- Mark completed items `[x]` immediately when done.
- New tasks get a `➕` prefix; blockers get a `⚠️` prefix.
- Update the plan if implementation deviates from scope.

## Solution Overview
- Override `Equals(Step?)` and `GetHashCode()` on `Step`:
  - Equal when `ActionKey` matches and `Properties` have identical content (same count, and every key maps to an equal `PropertyValue`), independent of insertion order.
  - `GetHashCode` combines `ActionKey` with an order-independent XOR of per-pair hashes so equal steps hash equally.
- Override `Equals(Recipe?)` and `GetHashCode()` on `Recipe`:
  - Equal when `Steps.SequenceEqual(other.Steps)` (order-sensitive; step order is semantically significant), using `Step`'s new content equality for element comparison.
  - `GetHashCode` combines step hashes in order via `HashCode`.
- `with` expressions, `Deconstruct`, `==`/`!=`, and `ToString` remain compiler-synthesized; only `Equals(T?)` and `GetHashCode()` are user-provided. Both must be overridden together to stay consistent (a user `Equals` with a synthesized member-wise `GetHashCode` would hash equal recipes differently).

## Technical Details
- `Step.Equals(Step? other)`: `other is not null && ActionKey == other.ActionKey && Properties.Count == other.Properties.Count && all pairs in Properties have a matching key in other with `PropertyValue`-equal value`.
- `Step.GetHashCode()`: start from `ActionKey`, XOR in `HashCode.Combine(pair.Key, pair.Value)` per pair (order-independent, consistent with content equality). This folds in `PropertyValue.GetHashCode`, which is the compiler-synthesized record hash routing `object Value` through `EqualityComparer<object>.Default` -> value-based for boxed `int`/`float` and `string`; so equal `PropertyValue`s hash equally and the `Step` hash/equality contract holds transitively.
- `Recipe.Equals(Recipe? other)`: `other is not null && Steps.SequenceEqual(other.Steps)`.
- `Recipe.GetHashCode()`: fold step hashes through `HashCode` in sequence order.
- No change to `PropertyValue`, `PropertyId`, or the `PlcLifecycleManager.cs:320` call site.

## What Goes Where
- **Implementation Steps** (`[ ]`): record equality overrides, unit tests, the reconnect no-conflict integration test, acceptance verification, docs.
- **Post-Completion** (no checkboxes): the user-guide match criterion can now be stated honestly; manual reconnect smoke test on real hardware.

## Implementation Steps

### Task 1: Content-based equality for `Step`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Step.cs`
- Create: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/RecipeStructuralEqualityTests.cs`

- [x] Add `public bool Equals(Step? other)` to `Step`: compare `ActionKey`, then `Properties` by content (count + per-key `PropertyValue` equality), order-independent.
- [x] Add `public override int GetHashCode()` consistent with the new equality: `ActionKey` XOR-combined with per-pair hashes (order-independent).
- [x] Keep the record positional definition, `WithProperty`, `with`/`Deconstruct` intact; only `Equals(T?)`/`GetHashCode` are user-provided.
- [x] Write unit tests: identical content in distinct `ImmutableDictionary` instances are equal; different `ActionKey` not equal; different property value not equal; different property key-set / different count not equal; same content built in different insertion order is equal; equal steps produce equal `GetHashCode`; `Equals(null)` is `false` and `Equals(self)` is `true` (null/reflexivity contract).
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=RecipeEquality"` — must pass before Task 2.

### Task 2: Content-based equality for `Recipe`

**Files:**
- Modify: `SemiStep/SemiStep.Core/Recipes/Recipe.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Recipes/RecipeStructuralEqualityTests.cs`

- [x] Add `public bool Equals(Recipe? other)` to `Recipe`: `other is not null && Steps.SequenceEqual(other.Steps)` (order-sensitive), relying on `Step`'s content equality.
- [x] Add `public override int GetHashCode()` folding step hashes in order via `HashCode`.
- [x] Keep `Empty`, `StepCount`, and all mutation helpers (`AppendStep`, `InsertStep`, etc.) intact.
- [x] Write unit tests: two recipes with identical steps in distinct list instances are equal; `Recipe.Empty` equals `new Recipe([])`; recipes differing in step order are not equal; differing step count not equal; a recipe with one content-equal step but a fresh inner `Step`/`Properties` instance is equal (the reconnect scenario at the unit level); equal recipes produce equal `GetHashCode`; `Equals(null)` is `false` and `Equals(self)` is `true`.
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=RecipeEquality"` — must pass before Task 3.

### Task 3: Reconnect identical-content -> no conflict regression test

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs`

- [x] Add `StateChanged_Connected_WhenRecipesIdentical_DoesNotFireConflict_AndPushesLocal`: append a step to `session` (non-empty local), then build the PLC stub recipe as a **fresh-instance deep copy of `session.Current`** — reconstruct each step as `new Step(step.ActionKey, ImmutableDictionary.CreateRange(step.Properties))` wrapped via `ImmutableList.Create(...)`. Set `ManagingAreaToReturn` committed=true.
  - **Do NOT reuse `BuildSingleStepRecipe()` (`PlcLifecycleManagerReconnectTests.cs:62-69`)**: it builds a step with `ImmutableDictionary.Empty`, but `session.AppendStep(WaitActionId)` routes through `StepInitializer.Create` and populates one default `PropertyValue` per action property. Empty vs. populated properties would differ, the conflict branch would fire, `NotifyRecipeChangedCallCount` would never increment, and the test would fail by timeout even with the fix in place. The PLC recipe must deep-copy the session's populated step.
- [x] Subscribe a flag to `PlcRecipeConflictDetected`; capture `syncService.NotifyRecipeChangedCallCount` before raising `Connected`.
- [x] Raise `StateChanged(Connected)`; `WaitUntilAsync(NotifyRecipeChangedCallCount > before)` as the deterministic completion signal (the equal branch falls through to `NotifyLocalRecipe`).
- [x] Assert the conflict flag is `false` (identical content must not raise `PlcRecipeConflictDetected`). Confirms the test is red pre-fix (conflict branch taken, counter never increments -> timeout) and green post-fix.
- [x] Run tests: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Reconnect"` — must pass before Task 4.

### Task 4: Verify acceptance criteria
- [ ] Verify reconnect no longer raises a conflict when PLC and PC recipes have identical content, and still raises one when they genuinely differ (existing `StateChanged_Connected_WhenRecipesDiffer_FiresConflictDetected` still passes).
- [ ] Confirm the existing `.Should().Be(plcRecipe)` assertions (`PlcLifecycleManagerReconnectTests.cs:107` and `:190`) still hold under the new structural equality (same instance is trivially structurally equal) — intentional, not incidental.
- [ ] Verify no other consumer relied on reference equality (re-confirm grep: no `Recipe`/`Step` as hash-set/dictionary keys; single `Recipe.Equals` call site).
- [ ] Run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- [ ] Run `dotnet format SemiStep/SemiStep.slnx` (pre-commit hook enforces formatting).

### Task 5: [Final] Archive the plan
- [ ] Move this plan to `docs/plans/completed/` via `git mv` (verbatim, no content edits).

## Post-Completion
*Items requiring manual intervention or external systems - informational only.*

**Manual verification:**
- Reconnect smoke test against real/simulated S7 PLC: commit an identical recipe to the PLC, drop and restore the connection, confirm no conflict dialog appears; then commit a genuinely different recipe and confirm the dialog does appear.

**Documentation:**
- The user-guide match criterion (issue "Docs" section) becomes accurate once this fix ships; confirm the doc PR/section reflects the shipped behavior.
