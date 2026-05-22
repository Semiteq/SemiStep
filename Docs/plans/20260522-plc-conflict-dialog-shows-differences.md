# PLC Conflict Dialog Shows Step Counts

## Overview

When `PlcLifecycleManager.PerformReconnectReconciliationAsync` detects that the local recipe and the PLC recipe differ, it raises `PlcRecipeConflictDetected(local, plc)`. The UI calls `MainWindowViewModel.HandleConflictAsync(Recipe local, Recipe plc)` but discards both arguments and shows a static, contextless dialog: *"The PLC contains a different recipe. Which version do you want to keep?"*. The user picks **Keep local** or **Load from PLC** blind.

This plan is the minimal honest fix: surface the **step counts** of both recipes in the dialog. That uses both parameters (killing IDE0060), gives the user a meaningful signal (different count = significant divergence; equal count = subtle edits), and adds no Core machinery. A richer diff (per-step / per-property differences) is deliberately out of scope — see Post-Completion for the follow-up note.

Housekeeping items (unused test constants, unused `transport` local, ~70 unused `using` directives) are out of scope and will be addressed in a separate PR — see Post-Completion.

## Context (from discovery)

Files involved:
- `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` (`HandleConflictAsync` at line 153)
- `SemiStep/SemiStep.UI/Plc/PlcConflictDialog.axaml` (+ `.axaml.cs`)
- `SemiStep/SemiStep.Core/Recipes/Recipe.cs` — `Recipe.StepCount` already exists (line 9)

Patterns found:
- Avalonia + ReactiveUI MVVM. Dialogs constructed in code-behind; `DataContext` set explicitly when a VM is used.
- No new DI registrations or Core-side abstractions are required for this fix.

## Development Approach

- **Testing approach**: Regular (code first, then tests).
- Make small, focused changes — this is a UI-only fix.
- The change is trivial enough that the existing test suite plus manual verification suffices. No new automated tests are added — see Testing Strategy.
- Update this plan file if scope changes.

## Testing Strategy

- **No new unit tests.** The change is: read `recipe.StepCount` twice and bind two `TextBlock`s. There is no formatting logic, no branching, no service interaction worth testing in isolation. Adding a "test that StepCount maps to a property" test would be ceremony, not signal.
- **Existing tests must continue to pass.** Run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after the change.
- **IDE0060 is the regression guard.** If `dotnet build` (or the IDE) emits IDE0060 on `HandleConflictAsync` after the change, the wire-up is wrong — `local`/`plc` were not actually consumed.
- **Manual verification** of the conflict path is required before merging — see Post-Completion.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with ➕ prefix.
- Document issues/blockers with ⚠️ prefix.

## Solution Overview

Introduce `PlcConflictDialogViewModel` with two read-only `int` properties (`LocalStepCount`, `PlcStepCount`). Construct it inside `HandleConflictAsync` from the `local` and `plc` parameters. The dialog binds its `DataContext` to the VM and displays the counts. The two action buttons (`Keep local` / `Load from PLC`) are unchanged.

No Core changes. No DI registration changes (VM is constructed inline at the call site; it has no service dependencies).

## Technical Details

### View-model

```csharp
namespace SemiStep.UI.Plc;

internal sealed class PlcConflictDialogViewModel
{
    public PlcConflictDialogViewModel(int localStepCount, int plcStepCount)
    {
        LocalStepCount = localStepCount;
        PlcStepCount = plcStepCount;
    }

    public int LocalStepCount { get; }
    public int PlcStepCount { get; }
}
```

The VM takes raw `int`s rather than `Recipe` references because that is the only state it needs; binding to primitives keeps the dialog independent of the `Recipe` type.

### Dialog ctor change

```csharp
internal PlcConflictDialog(PlcConflictDialogViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;
}
```

Visibility stays `internal`; the only call site is `MainWindowViewModel` in the same assembly.

### Dialog XAML change

Replace the single `TextBlock` body with a vertical stack:
- `"The PLC contains a different recipe."` (header)
- `"Local: {Binding LocalStepCount} steps"`
- `"PLC: {Binding PlcStepCount} steps"`
- `"Which version do you want to keep?"`

`Height` is increased from `160` to roughly `200` to fit the four lines comfortably. The two action buttons and their click handlers are unchanged.

### Wire-up

```csharp
private async Task HandleConflictAsync(Recipe local, Recipe plc)
{
    if (MainWindow is null)
    {
        return;
    }

    var viewModel = new PlcConflictDialogViewModel(local.StepCount, plc.StepCount);
    var dialog = new PlcConflictDialog(viewModel);

    // remainder unchanged: ShowDialog, exception handling, dialog.Confirmed/KeepLocal, _coordinator.ResolveConflict
}
```

## What Goes Where

- **Implementation Steps**: UI code changes in this repo.
- **Post-Completion**: manual verification, the deferred richer-diff feature, and the second PR for housekeeping.

## Implementation Steps

### Task 1: Add view-model

**Files:**
- Create: `SemiStep/SemiStep.UI/Plc/PlcConflictDialogViewModel.cs`
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj` (add `<Compile Include>` entry)

- [ ] create `PlcConflictDialogViewModel` per Technical Details (two int props, primary-style ctor)
- [ ] register file in csproj
- [ ] build solution

### Task 2: Update dialog markup and code-behind

**Files:**
- Modify: `SemiStep/SemiStep.UI/Plc/PlcConflictDialog.axaml`
- Modify: `SemiStep/SemiStep.UI/Plc/PlcConflictDialog.axaml.cs`

- [ ] replace the single `TextBlock` body with header + two count `TextBlock`s + the question line, inside a `StackPanel` in `Grid.Row="0"`
- [ ] add `xmlns:plc="using:SemiStep.UI.Plc"` and `x:DataType="plc:PlcConflictDialogViewModel"` on the `Window` root for compiled bindings (matches the convention used by `MainWindow.axaml`, `AppStatusBar.axaml`, `MessagePanel.axaml`, `RecipeMenuBar.axaml`)
- [ ] increase `Height` from 160 to 200 (or whatever fits cleanly)
- [ ] change `PlcConflictDialog` ctor signature to `internal PlcConflictDialog(PlcConflictDialogViewModel viewModel)`; set `DataContext = viewModel` after `InitializeComponent()`
- [ ] leave click handlers and `KeepLocal`/`Confirmed` flags untouched
- [ ] build solution

### Task 3: Wire `HandleConflictAsync`

**Files:**
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`

- [ ] in `HandleConflictAsync`, construct `new PlcConflictDialogViewModel(local.StepCount, plc.StepCount)` and pass it to `new PlcConflictDialog(viewModel)`
- [ ] leave the rest of the method unchanged (exception handling, `dialog.Confirmed`, `ResolveConflict` call)
- [ ] build solution

### Task 4: Verify

- [ ] `HandleConflictAsync(Recipe local, Recipe plc)` now reads both parameters; IDE0060 no longer flags it
- [ ] run full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must be green
- [ ] `dotnet format SemiStep/SemiStep.slnx` produces no diff in changed files
- [ ] manually trigger the conflict path (see Post-Completion)

### Task 5: Move plan, prepare PR

- [ ] move this plan to `Docs/plans/completed/`
- [ ] push branch `plc-conflict-dialog-shows-differences`; open PR titled `fix: PLC conflict dialog shows recipe step counts`
- [ ] PR body explains the discovered bug (discarded `local`/`plc` parameters) and the minimal scope

## Post-Completion

**Manual verification** (before merging):
- Edit a recipe locally so its step count differs from the recipe committed to the PLC; reconnect and trigger the reconcile path.
- Confirm the dialog shows both step counts and that the two buttons still call `ResolveConflict` with the correct `keepLocal` bool.
- Confirm the dialog renders correctly when both counts are equal (the conflict is then about per-step content rather than count — still useful to surface equality to the user).

**Deferred — richer diff (future work, not in this PR):**
- If step counts alone prove insufficient in practice, follow up with a per-step / per-property diff. That work is non-trivial: it needs a `RecipeDiffComputer` in Core, a formatter that resolves property display names via `GridColumnDefinition.UiName` (note: `PropertyTypeDefinition` has no `UiName`), `Result<T>`-unwrap discipline against `RecipeMetadataRegistry`, and likely a `Func<Recipe, Recipe, PlcConflictDialogViewModel>` factory injected into `MainWindowViewModel` to avoid adding a 10th forwarder dep. Track separately if/when the need emerges.

**Second PR — housekeeping** (separate branch, e.g. `chore-unused-code-cleanup`, opened after this PR merges):
- Remove unused `private const float DefaultWaitDurationSeconds = 10f;` at `SemiStep.Tests/Core/Integration/Mutation/CoreMutationTests.cs:17`.
- Remove unused `private const string TaskColumn = RecipeTestDriver.TaskColumn;` at `SemiStep.Tests/Core/RecipeSessionBehaviourCharacterizationTests.cs:30`.
- Replace `transport` local with discard at `SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs:219`.
- Drop the ~70 unused `using` directives via IDE "Remove unnecessary usings" or `dotnet format --diagnostics IDE0005`.
- Full test suite must pass.

**Out of scope, noted for future:**
- Defensive `throw` at `SemiStep.Core/Recipes/Formulas/FormulaEvaluator.cs:227` is intentional and documented in its message. No change.
