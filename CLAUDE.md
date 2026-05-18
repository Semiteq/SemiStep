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

### Comments

- Only for genuinely non-obvious business logic. No process notes (`// TODO`, `// in new version`). No transitional comments.
- English only.

## Avalonia / DataGrid recycling

For any `ItemsControl` / `ComboBox` whose `ItemsSource` depends on the row's data context in a recycled `DataGridTemplateColumn`, bind `ItemsSourceProperty` to a per-row VM property — **never** assign `ItemsSource` imperatively inside the `FuncDataTemplate` lambda. The lambda runs once at first cell materialisation; when Avalonia recycles the visual onto a new row by swapping `DataContext`, an imperatively-assigned property does not refresh and the first row's items leak into every recycled cell.

For **display** (model → UI), use a OneWay `MultiBinding` on `SelectedItem` that resolves `(value, items)` to a `ComboBoxItemViewModel` via a stateless `IMultiValueConverter`. Avalonia 12's MultiBinding waits for both legs to settle before invoking `Convert`, so the resolution is correct regardless of binding-evaluation order. Do **not** use `SelectedValueBinding` + `SelectedValue` for display: Avalonia 12.0.3 does not re-search `ItemsSource` for a matching `SelectedValue` once items arrive after the value (open ordering bug — Avalonia issue [#18147](https://github.com/AvaloniaUI/Avalonia/issues/18147)), which manifests as a blank cell after action change because the cell-recycle swap re-evaluates both bindings in non-deterministic order.

For **writeback** (user selection → row VM), use a `SelectionChanged` event handler subscribed inside the `FuncDataTemplate` lambda, **not** a TwoWay binding. Two Avalonia 12 limitations force this:

- `MultiBindingExpression` in Avalonia 12.0.3 does **not** override `WriteValueToSource`, so `IMultiValueConverter.ConvertBack` is never invoked — any TwoWay `MultiBinding` for writeback is dead code that silently swallows user input.
- `SelectingItemsControl.SelectedValueProperty` is registered TwoWay but has open spurious-null-write bugs (Avalonia issue [#19192](https://github.com/AvaloniaUI/Avalonia/issues/19192)) when `ItemsSource` changes.

Canonical example: `RecipeRowViewModel.GroupItemsByColumn` (per-row dict referencing shared registry-cached lists) + `ComboBoxCellFactory.CreateGroupCellTemplate`. The template:
- Binds `ItemsSourceProperty` to `GroupItemsByColumn[<columnKey>]` (declarative, recycling-safe).
- Binds `SelectedItemProperty` via OneWay `MultiBinding(value, items)` routed through `ComboBoxItemMultiSelectionConverter` (display only, Convert resolves to the matching item).
- Subscribes `SelectionChanged` to write the chosen item's `Id` back via `row.SetPropertyValue(...)`. The row VM's equal-value guard absorbs spurious fires during DataContext swap; `RecipeGridViewModel.OnCellValueChanged` filters null writes.

This pattern recovers from a multi-round regression (`f44bd91` → `642f17b` → `9d8e39b` → the MultiBinding restoration round that silently broke writeback under Avalonia 12 → the SelectedValueBinding attempt that exposed the #18147 ordering bug). Do not "simplify" the group template by collapsing writeback into a TwoWay binding or by replacing the SelectedItem MultiBinding with SelectedValue without first verifying the chosen Avalonia version has functioning `WriteValueToSource` for the relevant binding shape AND retries SelectedValue lookup on ItemsSource change.

## Troubleshooting

**Deleting Windows reserved-name files (`nul`, `con`, `aux`, etc.):** Use Git Bash: `rm -f nul`
