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

- One class per file. File-scoped namespaces: `namespace SemiStep.Core.Recipes.Analysis;`
- `using` directives above the namespace. `System` namespaces first, blank line, then others.
- Never inline full namespace paths — use `using` directives.

#### Project structure (post-refactor)

`SemiStep.Core` is organized by feature, not by technical layer:

- `SemiStep/Core/Recipes/` — recipe model, analysis, formulas, state, helpers, CSV import/export, clipboard
  - Namespaces: `SemiStep.Core.Recipes`, `SemiStep.Core.Recipes.Analysis`, `SemiStep.Core.Recipes.Formulas`,
    `SemiStep.Core.Recipes.State`, `SemiStep.Core.Recipes.Helpers`, `SemiStep.Core.Recipes.Import`,
    `SemiStep.Core.Recipes.Clipboard`
- `SemiStep/Core/Plc/` — PLC lifecycle, sync, S7 driver, configuration, runtime state
  - Namespaces: `SemiStep.Core.Plc`, `SemiStep.Core.Plc.Configuration`,
    `SemiStep.Core.Plc.Configuration.Memory`, `SemiStep.Core.Plc.State`, `SemiStep.Core.Plc.Sync`,
    `SemiStep.Core.Plc.S7`, `SemiStep.Core.Plc.S7.Protocol`, `SemiStep.Core.Plc.S7.Serialization`
- `SemiStep/Core/Configuration/` — YAML configuration loading, mapping, validation, DTOs
  - Namespaces: `SemiStep.Core.Configuration`, `SemiStep.Core.Configuration.Loaders`,
    `SemiStep.Core.Configuration.Mapping`, `SemiStep.Core.Configuration.Validation`,
    `SemiStep.Core.Configuration.Facade`, `SemiStep.Core.Configuration.Dto`
- `SemiStep/Core/Shared/` — cross-cutting types (Warning, ResultWarningExtensions)
  - Namespace: `SemiStep.Core.Shared`

The `Recipes` folder uses the plural form to avoid a name collision between the `Recipe` type
and a hypothetical `Recipe` namespace (the C# compiler resolves unqualified `Recipe` to the
sibling namespace, not the imported type).

### Size Limits

- Class: 300 lines max. Method: 50 lines max.

### Naming

| Element                           | Convention                     | Example                            |
| --------------------------------- | ------------------------------ | ---------------------------------- |
| Public types, methods, properties | PascalCase                     | `RecipeEditor`, `LoadAsync()`      |
| Interfaces                        | I-prefix                       | `IRecipeRepository`                |
| Private fields                    | `_camelCase`                   | `_recipeService`                   |
| Class instance fields             | `_className` (no abbreviation) | `_recipeEditor`, `_plcLifecycleManager` |
| Constants                         | PascalCase                     | `MaxStepCount`                     |
| Local variables                   | camelCase                      | `stepIndex`                        |

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

### Threading

Two patterns only. Do not introduce a third.

- **Pattern A (default) - `.ObserveOn(RxApp.MainThreadScheduler)` at the subscription site.**
  The standard ReactiveUI way: producers (`Subject<T>.OnNext`, `IObservable<T>` streams) emit
  on whatever thread their work runs on; consumers (ViewModels) declare their UI-thread
  requirement at subscription. Used in `MainWindowViewModel`, `RecipeFileViewModel`,
  `RecipeCommandsViewModel`, `RecipeGridViewModel`, `ClipboardViewModel`,
  `PlcMonitorViewModel`, `RecipeMutationCoordinator`. New observable subscriptions that bind
  to the UI must follow this pattern.
- **Pattern B (exception) - self-marshalling inside the callee.** Reserved for VMs/services
  that are widely called from mixed thread contexts where pushing the marshalling
  responsibility onto every caller would be worse. `MessagePanelViewModel` is the only
  example: every public mutating method (`AddError`, `AddWarning`, `AddInfo`,
  `RefreshReasons`, `Clear`) dispatches via `PostOnUiThread()`
  (`Dispatcher.UIThread.CheckAccess()` / `Post`). Do NOT wrap calls to
  `MessagePanelViewModel` in `Dispatcher.UIThread.Post` at the call site - it is redundant
  and obscures intent.
- **Do not introduce ad-hoc `Dispatcher.UIThread.Post` calls at producer sites.** A producer
  that wraps its own `Subject.OnNext` in `Dispatcher.UIThread.Post` violates Pattern A by
  forcing every observer onto the UI thread regardless of the observer's actual needs, and
  duplicates marshalling already declared by Pattern A subscribers. If a consumer needs
  UI-thread delivery, use `.ObserveOn(RxApp.MainThreadScheduler)` at its subscription.

### Comments

- Only for genuinely non-obvious business logic. No process notes (`// TODO`, `// in new version`).
- English only.

## Code investigation

Whenever you need to explore code of nuget pkgs, get external methods signatures or explore external libs structure, first try to fetch information via mcp from github or webfetch or skills if any before trying to reverse engineer compiled files. The pkgs info may be retrieved from using directive or .csproj file.

## Troubleshooting

**Deleting Windows reserved-name files (`nul`, `con`, `aux`, etc.):** Use Git Bash: `rm -f nul`
