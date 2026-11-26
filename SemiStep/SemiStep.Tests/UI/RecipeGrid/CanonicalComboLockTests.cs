using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.RecipeGrid;

using Xunit;

using Path = Avalonia.Controls.Shapes.Path;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ComboLock")]
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
			var presenter = _grid.PresenterAt(GroupRowIndex, columnKey);
			var recipeIdBefore = RecipeId(GroupRowIndex, columnKey);
			var textBefore = DisplayText(presenter);

			_grid.Click(presenter);

			presenter.IsEditing.Should().BeFalse($"a locked {columnKey} cell must not open on a press");
			_grid.LiveComboBoxCount().Should().Be(0, "a locked press builds no editor");
			_grid.Surface.SelectedStepIndices.Should().Equal([GroupRowIndex], "a locked press still selects the row");

			_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
			_grid.PressKey(Key.Enter, PhysicalKey.Enter);

			RecipeId(GroupRowIndex, columnKey).Should().Be(recipeIdBefore, "the list keys pick nothing in a locked cell");
			DisplayText(presenter).Should().Be(textBefore);
		}
	}

	[AvaloniaTheory]
	[InlineData(Key.F2, RawInputModifiers.None)]
	[InlineData(Key.F4, RawInputModifiers.None)]
	[InlineData(Key.Down, RawInputModifiers.Alt)]
	public void Locked_OpenKeysOnCurrentComboCell_OpenNothing(Key key, RawInputModifiers modifiers)
	{
		var presenter = MakeActionCellCurrent();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		_grid.PressKey(key, PhysicalKeyOf(key), modifiers);

		presenter.IsEditing.Should().BeFalse($"{modifiers} {key} opens no locked cell");
		_grid.LiveComboBoxCount().Should().Be(0);
	}

	[AvaloniaFact]
	public void Lock_ClosesListOpenedByPress_ShowsText_MovesFocusToGrid()
	{
		var presenter = _grid.PresenterAt(GroupRowIndex, ActionColumnKey);
		_grid.Click(presenter);
		var comboBox = _grid.EditorAt(GroupRowIndex, ActionColumnKey);
		comboBox.IsDropDownOpen.Should().BeTrue("precondition: the press opens the list");

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		comboBox.IsDropDownOpen.Should().BeFalse("the lock closes an open combo list");
		presenter.IsEditing.Should().BeFalse("the lock ends the edit and the cell shows text");
		_grid.FocusedElement().Should().BeSameAs(_grid.DataGrid, "the lock hands focus back to the grid");
	}

	[AvaloniaFact]
	public void LockBetweenPressAndDeferredOpen_OpensNoList_ShowsText()
	{
		var presenter = _grid.PresenterAt(GroupRowIndex, ActionColumnKey);
		var point = _grid.CenterOf(presenter);

		_grid.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
		_grid.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		presenter.Editor.Should().BeOfType<ComboBox>("the press built the editor before the lock")
			.Which.IsDropDownOpen.Should().BeFalse("no list opens once the run has started");
		presenter.IsEditing.Should().BeFalse("the display shows again");
		_grid.FocusedElement().Should().BeSameAs(_grid.DataGrid);
	}

	// Guards the lock leg of the input binding: a leg that never resolves fails closed and would keep the
	// cell dead after the run stops.
	[AvaloniaFact]
	public void Unlock_AfterLock_PressOpensList()
	{
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();

		_grid.Click(_grid.PresenterAt(GroupRowIndex, ActionColumnKey));

		_grid.EditorAt(GroupRowIndex, ActionColumnKey).IsDropDownOpen.Should().BeTrue("after the run stops a press opens the list again");
	}

	[AvaloniaFact]
	public void Chevron_OnHoveredCellOnly()
	{
		var hovered = _grid.PresenterAt(4, ActionColumnKey);

		_grid.Window.MouseMove(_grid.CenterOf(hovered));
		Dispatcher.UIThread.RunJobs();

		VisibleChevronOwners().Should().Equal([hovered], "only the hovered combo cell shows a chevron");
	}

	[AvaloniaFact]
	public void Chevron_OnCurrentCellOnly()
	{
		var current = MakeActionCellCurrent();

		VisibleChevronOwners().Should().Equal([current], "only the current combo cell shows a chevron");
	}

	[AvaloniaFact]
	public void Chevron_NoneWhileLocked_OnHoveredOrCurrentCell()
	{
		var current = MakeActionCellCurrent();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		_grid.Window.MouseMove(_grid.CenterOf(current));
		Dispatcher.UIThread.RunJobs();

		VisibleChevronOwners().Should().BeEmpty("a locked cell never shows a chevron");
	}

	[AvaloniaFact]
	public void InapplicableComboCell_NoChevron_NoEditorOnPress()
	{
		var groupColumnKey = GroupColumnKey();
		var inapplicable = _grid.PresenterAt(GroupRowIndex + 1, groupColumnKey);
		_grid.Surface.RecipeRows[GroupRowIndex + 1].IsApplicable(groupColumnKey)
			.Should().BeFalse("precondition: the next row's action has no group selector");

		_grid.Window.MouseMove(_grid.CenterOf(inapplicable));
		Dispatcher.UIThread.RunJobs();
		VisibleChevronOwners().Should().BeEmpty("an inapplicable cell shows no chevron");

		_grid.Click(inapplicable);
		inapplicable.IsEditing.Should().BeFalse("an inapplicable cell opens no editor");
		_grid.LiveComboBoxCount().Should().Be(0);
	}

	private LazyComboCellPresenter MakeActionCellCurrent()
	{
		var actionColumn = _grid.DataGrid.Columns.Single(column => Equals(column.Tag, ActionColumnKey));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, GroupRowIndex, actionColumn);

		return _grid.PresenterAt(GroupRowIndex, ActionColumnKey);
	}

	private LazyComboCellPresenter[] VisibleChevronOwners()
	{
		return [.. _grid.DataGrid.GetVisualDescendants()
			.OfType<Path>()
			.Where(path => path.Classes.Contains(ComboBoxCellFactory.ChevronClass) && path.IsEffectivelyVisible)
			.Select(path => path.FindAncestorOfType<LazyComboCellPresenter>()!)];
	}

	private static PhysicalKey PhysicalKeyOf(Key key)
	{
		return key switch
		{
			Key.F2 => PhysicalKey.F2,
			Key.F4 => PhysicalKey.F4,
			_ => PhysicalKey.ArrowDown,
		};
	}

	private static string? DisplayText(LazyComboCellPresenter presenter)
	{
		return presenter.GetVisualDescendants().OfType<TextBlock>().First().Text;
	}

	private string GroupColumnKey()
	{
		var row = _grid.Surface.RecipeRows[GroupRowIndex];

		return row.GroupItemsByColumn.First(pair => pair.Value.Count > 0).Key;
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
