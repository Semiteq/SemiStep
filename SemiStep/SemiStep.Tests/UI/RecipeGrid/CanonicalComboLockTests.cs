using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ComboOpen")]
[Trait("Category", "Integration")]
public sealed class CanonicalComboLockTests : IAsyncLifetime
{
	private const int SeededStepCount = 10;
	private const int GroupRowIndex = 2;
	private const string ActionColumnKey = "action";

	private readonly UIFixture _fixture = new();
	private CanonicalGridWindow _grid = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);
		_fixture.Coordinator.ChangeStepAction(GroupRowIndex, RecipeTestDriver.WithGroupActionId);
		_grid = new CanonicalGridWindow(_fixture);
	}

	public async ValueTask DisposeAsync()
	{
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Locked_PressOnActionAndGroupCombo_OpensNoList_SelectsRow_ListKeysChangeNothing()
	{
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		foreach (var columnKey in new[] { ActionColumnKey, GroupColumnKey() })
		{
			var comboBox = _grid.ComboAt(GroupRowIndex, columnKey);
			var recipeIdBefore = RecipeId(GroupRowIndex, columnKey);

			_grid.Click(comboBox);

			comboBox.IsDropDownOpen.Should().BeFalse($"a locked {columnKey} combo must not open on a press");
			_grid.Surface.SelectedStepIndices.Should().Equal([GroupRowIndex], "a locked press still selects the row");

			_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
			_grid.PressKey(Key.Enter, PhysicalKey.Enter);

			RecipeId(GroupRowIndex, columnKey).Should().Be(recipeIdBefore, "the list keys pick nothing in a locked combo");
			ShownId(comboBox).Should().Be(recipeIdBefore);
		}
	}

	[AvaloniaFact]
	public void Locked_PressAndRelease_LeavesNoPressedState()
	{
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		var comboBox = _grid.ComboAt(GroupRowIndex, ActionColumnKey);

		_grid.Click(comboBox);

		comboBox.Classes.Should().NotContain(":pressed", "a locked combo never enters the pressed state");
	}

	[AvaloniaFact]
	public void Locked_F4AndAltDown_OpenNoList_ListKeysChangeNothing()
	{
		var comboBox = _grid.ComboAt(GroupRowIndex, ActionColumnKey);
		var actionIdBefore = RecipeId(GroupRowIndex, ActionColumnKey);
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		comboBox.Focus();
		Dispatcher.UIThread.RunJobs();
		comboBox.IsFocused.Should().BeFalse("a locked combo cannot take keyboard focus");

		_grid.PressKey(Key.F4, PhysicalKey.F4);
		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
		comboBox.IsDropDownOpen.Should().BeFalse("no key opens a locked combo");

		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		RecipeId(GroupRowIndex, ActionColumnKey).Should().Be(actionIdBefore, "the list keys pick nothing in a locked combo");
		ShownId(comboBox).Should().Be(actionIdBefore);
	}

	[AvaloniaFact]
	public void Lock_ClosesOpenFocusedCombo_AndMovesFocusToGrid()
	{
		var comboBox = _grid.ComboAt(GroupRowIndex, ActionColumnKey);
		comboBox.Focus();
		Dispatcher.UIThread.RunJobs();
		comboBox.IsFocused.Should().BeTrue("precondition: the unlocked combo takes focus");
		comboBox.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		comboBox.IsDropDownOpen.Should().BeFalse("the lock closes an open combo list");
		_grid.FocusedElement().Should().BeSameAs(_grid.DataGrid, "the lock hands focus from the combo back to the grid");
	}

	[AvaloniaFact]
	public void Lock_FocusedComboWithClosedList_MovesFocusToGrid()
	{
		var comboBox = _grid.ComboAt(GroupRowIndex, ActionColumnKey);
		comboBox.Focus();
		Dispatcher.UIThread.RunJobs();
		comboBox.IsFocused.Should().BeTrue("precondition: the unlocked combo takes focus");

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		comboBox.IsDropDownOpen.Should().BeFalse();
		_grid.FocusedElement().Should().BeSameAs(_grid.DataGrid, "the lock hands focus from the combo back to the grid");
	}

	// Guards the lock leg of the input binding: a leg that never resolves fails closed and would keep the
	// combo dead after the run stops.
	[AvaloniaFact]
	public void Unlock_AfterLock_PressOpensList()
	{
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();
		var comboBox = _grid.ComboAt(GroupRowIndex, ActionColumnKey);

		_grid.Click(comboBox);

		comboBox.IsDropDownOpen.Should().BeTrue("after the run stops a press opens the list again");
	}

	private string GroupColumnKey()
	{
		var row = _grid.Surface.RecipeRows[GroupRowIndex];

		return row.GroupItemsByColumn.First(pair => pair.Value.Count > 0).Key;
	}

	private static int ShownId(ComboBox comboBox)
	{
		return ((ComboBoxItemViewModel)comboBox.SelectedItem!).Id;
	}

	private int RecipeId(int stepIndex, string columnKey)
	{
		var step = _fixture.Coordinator.CurrentRecipe.Steps[stepIndex];
		if (columnKey == ActionColumnKey)
		{
			return step.ActionKey;
		}

		return step.Properties[new PropertyId(columnKey)].AsInt();
	}
}
