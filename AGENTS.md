# Agent Instructions for SemiStep

SemiStep is a recipe table editor/runtime for PLC integration (S7 protocol).
Platform: .NET 10, Windows, C# 14. UI: Avalonia 11.2 + ReactiveUI (MVVM).
Solution: `SemiStep/SemiStep.slnx`. All commands run from repository root.

## Build

```powershell
dotnet build SemiStep/UI/SemiStep.UI.csproj            # recommended (entry executable)
dotnet build SemiStep/SemiStep.slnx                    # all projects
dotnet run   --project SemiStep/UI/SemiStep.UI.csproj
dotnet format SemiStep/SemiStep.slnx                   # pre-commit hook enforces this
```

## Test

```powershell
dotnet test SemiStep/Tests/Tests.csproj
dotnet test SemiStep/Tests/Tests.csproj --filter "Component=Core"
dotnet test SemiStep/Tests/Tests.csproj --filter "Area=Mutation"
dotnet test SemiStep/Tests/Tests.csproj --filter "Category=Unit"
dotnet test SemiStep/Tests/Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Test traits: `[Trait("Component", "Core|Config|UI|Domain|Csv|S7")]`, `[Trait("Area", "<AreaName>")]`,
`[Trait("Category", "Unit|Integration")]`.

Invalid config test cases use an overlay pattern: copy `Tests/YamlConfigs/Standard/` to a temp
directory and overlay only the differing files from `Tests/YamlConfigs/Invalid/{CaseName}/`.

**Dispatcher flush in tests:** After awaiting `RecipeMutationCoordinator` async methods
(`LoadRecipeAsync`, `LoadRecipeFromPlcAsync`), call `Dispatcher.UIThread.RunJobs(null)` before
asserting on `MessagePanelViewModel` state to flush the pending Avalonia dispatcher queue.

## Code Style

### General

- SOLID, DRY, KISS, YAGNI. Each method does one thing; each class one purpose.
- Prefer better naming over comments.

### File Layout

- One class per file. File-scoped namespaces: `namespace Domain.Services;`
- `using` directives above the namespace. `System` namespaces first, blank line, then others.
- Never inline full namespace paths — use `using` directives.

### Size Limits

- Class: 300 lines max. Method: 50 lines max.

### Naming

| Element                           | Convention                     | Example                      |
| --------------------------------- | ------------------------------ | ---------------------------- |
| Public types, methods, properties | PascalCase                     | `CoreService`, `LoadAsync()` |
| Interfaces                        | I-prefix                       | `IRecipeRepository`          |
| Private fields                    | `_camelCase`                   | `_recipeService`             |
| Class instance fields             | `_className` (no abbreviation) | `_domainFacade`              |
| Constants                         | PascalCase                     | `MaxStepCount`               |
| Local variables                   | camelCase                      | `stepIndex`                  |

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
- Register services in extension methods: `AddDomain()`, `AddConfig()`, `AddRecipe()`.
- Avoid mutable static state.

### Interface Design

- Create an interface when: 2+ implementations exist, the class is mocked in tests, it crosses
  an architectural layer boundary, or it implements Strategy/Factory.
- Do not create an interface for a single concrete class with no extension plans, or for POCOs/DTOs.
- Interfaces belong on the consumer side.

### Threading

- `MessagePanelViewModel` is self-marshalling. Every public mutating method (`AddError`,
  `AddWarning`, `AddInfo`, `RefreshReasons`, `Clear`) dispatches to the UI thread internally
  via `PostOnUiThread()` (`Dispatcher.UIThread.CheckAccess()` / `Post`). Do NOT wrap calls
  to `MessagePanelViewModel` in `Dispatcher.UIThread.Post` at the call site -- doing so is
  redundant and obscures intent.
- Event handlers that fire `Subject.OnNext` into the Avalonia binding system (e.g.,
  `OnPlcRecipeConflictDetected` in `RecipeMutationCoordinator`) must still dispatch via
  `Dispatcher.UIThread.Post` independently of any message panel calls.

### Comments

- Only for genuinely non-obvious business logic. No process notes (`// TODO`, `// in new version`).
- English only.

## Code investigation

Whenever you need to explore code of nuget pkgs, get external methods signatures or explore external libs structure, first try to fetch information via mcp from github or webfetch or skills if any before trying to reverse engineer compiled files. The pkgs info may be retrieved from using directive or .csproj file.

## Troubleshooting

**Deleting Windows reserved-name files (`nul`, `con`, `aux`, etc.):** Use Git Bash: `rm -f nul`
