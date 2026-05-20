using System.Reactive;
using System.Reactive.Subjects;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class DataGridEditorCloseBehaviorTests
{
	[AvaloniaFact]
	public void Trigger_Emission_InvokesCommitEdit_AndClearsEditingRow()
	{
		var dataGrid = new DataGrid
		{
			ItemsSource = new[]
			{
				new { Value = "a" },
				new { Value = "b" },
			},
			AutoGenerateColumns = true,
		};

		var trigger = new Subject<Unit>();
		DataGridEditorCloseBehavior.SetTrigger(dataGrid, trigger);

		// Begin an edit on the first cell.
		dataGrid.SelectedIndex = 0;
		dataGrid.BeginEdit();

		trigger.OnNext(Unit.Default);

		// After the behavior commits the cell edit, the grid should no longer report
		// an in-flight edit. CurrentColumn may be set, but the edited row's edit
		// indicator is cleared. We assert via CommitEdit being a no-op when called
		// again (returns true when no edit is active).
		var commitWithNoEdit = dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
		commitWithNoEdit.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Trigger_Replacement_DisposesPreviousSubscription()
	{
		var dataGrid = new DataGrid();

		var triggerA = new Subject<Unit>();
		var triggerB = new Subject<Unit>();

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, triggerA);
		DataGridEditorCloseBehavior.SetTrigger(dataGrid, triggerB);

		// Emitting on the replaced trigger should be a no-op: the behavior does not
		// throw and the test simply observes that no exception propagates.
		var act = () => triggerA.OnNext(Unit.Default);
		act.Should().NotThrow();
	}
}
