# Agent Instructions for SemiStep

SemiStep is a recipe table editor/runtime for PLC integration (S7 protocol).
Platform: .NET 10, Windows, C# 14. UI: Avalonia 12.0.3 + ReactiveUI (MVVM).
Solution: `SemiStep/SemiStep.slnx`. All commands run from repository root.

## Build

```powershell
dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj            # recommended (entry executable)
dotnet build SemiStep/SemiStep.slnx                    # all projects
dotnet run   --project SemiStep/SemiStep.UI/SemiStep.UI.csproj
dotnet format SemiStep/SemiStep.slnx                   # pre-commit hook enforces this
```

## Test

```powershell
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Component=Core"
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=Mutation"
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Category=Unit"
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Test traits: `[Trait("Component", "Core|Config|UI|Domain|Csv|S7")]`, `[Trait("Area", "<AreaName>")]`,
`[Trait("Category", "Unit|Integration")]`.

Invalid config test cases use an overlay pattern: copy `SemiStep.Tests/YamlConfigs/Standard/` to a temp
directory and overlay only the differing files from `SemiStep.Tests/YamlConfigs/Invalid/{CaseName}/`.

**Avalonia headless tests:** UI tests use `[AvaloniaFact]` / `[AvaloniaTheory]` (from `Avalonia.Headless.XUnit`) which wraps the full test lifecycle, including `IAsyncLifetime.InitializeAsync` and `DisposeAsync`, in the headless dispatcher. No manual `Dispatcher.UIThread.RunJobs(...)` or sync-over-async wrappers are needed.

## Code Style

### General

- SOLID, DRY, KISS, YAGNI. Each method does one thing; each class one purpose.
- Prefer better naming over comments.

### File Layout

- One class per file. File-scoped namespaces: `namespace SemiStep.Core.Recipes.Analysis;`
- `using` directives above the namespace. `System` namespaces first, blank line, then others.
- Never inline full namespace paths — use `using` directives.

### Size Limits

- Class: prefer 300 lines. Method: prefer 50 lines.

### Naming

| Element                           | Convention                     | Example                                 |
| --------------------------------- | ------------------------------ | --------------------------------------- |
| Public types, methods, properties | PascalCase                     | `RecipeEditor`, `LoadAsync()`           |
| Interfaces                        | I-prefix                       | `IRecipeRepository`                     |
| Private fields                    | `_camelCase`                   | `_recipeService`                        |
| Class instance fields             | `_className` (no abbreviation) | `_recipeEditor`, `_plcLifecycleManager` |
| Constants                         | PascalCase                     | `MaxStepCount`                          |
| Local variables                   | camelCase                      | `stepIndex`                             |

No abbreviations in names.

### Formatting

- Tabs, size 4. Max line length 120 characters.
- Braces on new line, even for single-line statements.
- Expression-bodied members only for simple properties and indexers.

### Types and `var`

- Always `var` for local declarations.
- Predefined types: `int`, `string` (not `Int32`, `String`).

### Nullability

- Nullable reference types enabled. Avoid nulls in public APIs.
- Use `?.` and `??`. Do not suppress warnings with `!` without a verified reason.

### Dependency Injection

- Constructor injection only (primary constructors preferred). No property injection, no service locator.
- Register services in extension methods: `AddRecipe()`, `AddS7()`, `AddCsv()`, `AddClipboard()`, `AddUi()`.
- Avoid mutable static state.

### Interface Design

- Create an interface when: 2+ implementations exist, the class is mocked in tests, it crosses
  an architectural layer boundary, or it implements Strategy/Factory.
- Do not create an interface for a single concrete class with no extension plans, or for POCOs/DTOs.
- Interfaces belong on the consumer side.

### Comments

- Only for genuinely non-obvious business logic. No process notes (`// TODO`, `// in new version`). No transitional comments.
- English only.

## Troubleshooting

**Deleting Windows reserved-name files (`nul`, `con`, `aux`, etc.):** Use Git Bash: `rm -f nul`

---

This is project overview file, do not add specifics here. See the human readable docs in Docs\*. Keep the language of files as is.
