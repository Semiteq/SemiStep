---
applyTo: "**/*.cs"
---

<!-- Reviewer scope: apply these rules only to C# source files.
     Do not review or comment on Markdown documents (plans, docs, README, etc.). -->

# SemiStep — Copilot Code Review Instructions

SemiStep is a recipe table editor/runtime for PLC integration (S7 protocol).
Platform: .NET 10, C# 14, Avalonia 12.0.3, ReactiveUI.Avalonia (MVVM).

---

## How to review this codebase

Apply the rules below. Cite the specific rule for every finding. Distinguish severity:
**CRITICAL** (must fix before merge), **MODERATE** (should fix), **MINOR** (optional polish).
Do not flag choices that are not covered by any rule below — review against documented rules,
not personal preferences.

---

## Error handling

### FluentResults

- `Result` / `Result<T>` is used at all facade and service boundaries.
- Always check `result.IsFailed` before using the value. Discarding a `Result` without
  inspection is a CRITICAL finding.
- Warnings use the `Warning` class (extends `Success`) via `result.WithWarning()`, not
  `result.WithError()`. A warning that triggers `IsFailed` is a correctness bug.
- Each module may use internal error types but must convert to `Result` at its public boundary.

### Exceptions

- Throw exceptions only for truly exceptional conditions (programmer error, corrupted state).
- Using exceptions for expected business logic failures (parsing, validation) is a finding.
- Empty catch blocks (silently swallowing exceptions) are always a CRITICAL finding.

### Null safety

- `<Nullable>enable</Nullable>` is active in all projects.
- Missing null checks on parameters or return values are valid findings.
- `!` suppressor without a verified justification comment is a finding.
- Use `?.` and `??` where the value may be null.

### Unused parameters

- A parameter never read inside the method body is a MODERATE finding. Suggest removing it
  and updating all call sites.

---

## Code style

### Naming

- Public types, methods, properties: PascalCase.
- Private fields: `_camelCase` (underscore prefix). Class-level instance fields named after
  their type, unabbreviated: `_recipeEditor`, `_plcLifecycleManager`.
- Interfaces: `I`-prefix + PascalCase.
- Local variables and parameters: camelCase.
- No abbreviations in any identifier.

### Formatting

- Tabs (size 4). Max line length 120 characters.
- Braces on a new line for all blocks, including single-line `if`/`else`/`for`/`foreach` bodies.
- Expression-bodied members: allowed only for simple properties and indexers, not for methods
  or constructors.

### File layout

- File-scoped namespace: `namespace Foo.Bar;` (not block-scoped).
- `using` directives at the top; `System` namespaces first, blank line, then others.
- One class per file. No full namespace paths inline — use `using` directives.

### Types

- `var` for all local variable declarations.
- Predefined aliases: `int`, `string`, `bool`, not `Int32`, `String`.

### Size limits

- Class preferably under 300 lines. Method preferably under 50 lines.

### Comments

- Only for genuinely non-obvious business logic.
- No `// TODO`, `// HACK`, `// in new version`, or other transient process notes.
- English only.

---

## Code smells

- **Dead code**: unused private methods, unreachable branches, unused private fields, unused
  `using` directives, variables assigned but never read.
- **Duplication**: identical or near-identical logic blocks that should be extracted.
- **Deep nesting**: more than 3 levels of `if`/`else`/`for`; suggest guard clauses or early returns.
- **Inconsistent abstraction**: mixing high-level domain operations with low-level details in
  the same method without helper methods.
- **God class**: a class accumulating responsibilities that belong elsewhere.
- **Disabled/skipped tests**: tests must not be skipped; rewrite or delete them instead.

---

## Design and simplicity

### Interface design

- Create an interface when: 2+ implementations exist, the class is mocked in tests, it crosses
  an architectural layer boundary, or it implements Strategy/Factory.
- Do NOT create an interface for a single concrete class with no extension plans, or for
  POCOs/DTOs/immutable records.
- Interfaces belong on the consumer side (where injected), not the producer side (where implemented).

### YAGNI

- Do not flag patterns that already exist elsewhere in the codebase — consistency is a virtue.
- Flag unused extension points, hooks with no subscribers, and fallback paths that can never
  be reached.
