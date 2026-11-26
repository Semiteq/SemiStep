using System.Globalization;

using Avalonia;
using Avalonia.Controls;
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

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ComboWheel")]
[Trait("Category", "Integration")]
public sealed class CanonicalClosedComboInputTests : IAsyncLifetime
{
	private const int SeededStepCount = 60;
	private const int GroupRowIndex = 3;
	private const int ActionRowIndex = 5;
	private const string ActionColumn = "action";
	private const string GroupColumn = "group";

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

	[AvaloniaTheory]
	[InlineData(ActionColumn, Key.Down, PhysicalKey.ArrowDown)]
	[InlineData(ActionColumn, Key.Up, PhysicalKey.ArrowUp)]
	[InlineData(ActionColumn, Key.PageDown, PhysicalKey.PageDown)]
	[InlineData(ActionColumn, Key.PageUp, PhysicalKey.PageUp)]
	[InlineData(ActionColumn, Key.Home, PhysicalKey.Home)]
	[InlineData(ActionColumn, Key.End, PhysicalKey.End)]
	[InlineData(ActionColumn, Key.Left, PhysicalKey.ArrowLeft)]
	[InlineData(ActionColumn, Key.Right, PhysicalKey.ArrowRight)]
	[InlineData(GroupColumn, Key.Down, PhysicalKey.ArrowDown)]
	[InlineData(GroupColumn, Key.Up, PhysicalKey.ArrowUp)]
	[InlineData(GroupColumn, Key.PageDown, PhysicalKey.PageDown)]
	[InlineData(GroupColumn, Key.PageUp, PhysicalKey.PageUp)]
	[InlineData(GroupColumn, Key.Home, PhysicalKey.Home)]
	[InlineData(GroupColumn, Key.End, PhysicalKey.End)]
	[InlineData(GroupColumn, Key.Left, PhysicalKey.ArrowLeft)]
	[InlineData(GroupColumn, Key.Right, PhysicalKey.ArrowRight)]
	public void PressEscThenNavigationKey_KeepsValue_OpensNothing_FocusStaysInGrid(
		string column,
		Key key,
		PhysicalKey physicalKey)
	{
		PressThenEscape(column, StartsAtLastItem(key));
		var idBefore = RecipeId(column);

		_grid.PressKey(key, physicalKey);

		RecipeId(column).Should().Be(idBefore, $"{key} after a closed {column} list changes no value");
		_grid.LiveComboBoxCount().Should().Be(0, $"{key} opens no list");
		var focused = _grid.FocusedElement() as Visual;
		focused.Should().NotBeNull("the key leaves keyboard focus on an element");
		(focused == _grid.DataGrid || _grid.DataGrid.IsVisualAncestorOf(focused!))
			.Should().BeTrue($"{key} keeps focus in the grid");
	}

	[AvaloniaTheory]
	[InlineData(ActionColumn, Key.Down, PhysicalKey.ArrowDown, 1)]
	[InlineData(ActionColumn, Key.Up, PhysicalKey.ArrowUp, -1)]
	[InlineData(GroupColumn, Key.Down, PhysicalKey.ArrowDown, 1)]
	[InlineData(GroupColumn, Key.Up, PhysicalKey.ArrowUp, -1)]
	public void PressEscThenVerticalArrow_MovesRowSelection(
		string column,
		Key key,
		PhysicalKey physicalKey,
		int rowOffset)
	{
		PressThenEscape(column, StartsAtLastItem(key));

		_grid.PressKey(key, physicalKey);

		_grid.Surface.SelectedStepIndices.Should().Equal(
			[RowIndex(column) + rowOffset],
			$"{key} moves the row as in any other cell");
	}

	[AvaloniaTheory]
	[InlineData(ActionColumn)]
	[InlineData(GroupColumn)]
	public void PressEscThenWheel_KeepsValue_ScrollsGrid(string column)
	{
		var presenter = PressThenEscape(column, startsAtLastItem: false);
		var idBefore = RecipeId(column);
		var offsetBefore = _grid.VerticalScrollOffset();

		_grid.Wheel(presenter, notches: -1);

		RecipeId(column).Should().Be(idBefore, $"the wheel over a {column} cell changes no value");
		_grid.VerticalScrollOffset().Should().BeGreaterThan(offsetBefore, "the wheel scrolls the grid");
	}

	[AvaloniaTheory]
	[Trait("Area", "CellKeyboard")]
	[InlineData(ActionColumn)]
	[InlineData(GroupColumn)]
	public void TypedLetter_OnCurrentComboCell_KeepsValue_OpensNothing(string column)
	{
		PressThenEscape(column, startsAtLastItem: false);
		var idBefore = RecipeId(column);

		_grid.TypeText(OtherItemLetter(column));

		RecipeId(column).Should().Be(idBefore, $"typing on a {column} cell changes no value");
		_grid.LiveComboBoxCount().Should().Be(0, "combo cells ignore typing");
	}

	[AvaloniaFact]
	public void OpenList_TypedLetterThenEnter_PicksMatchingItem()
	{
		PressThenEscape(ActionColumn, startsAtLastItem: false);
		var letter = OtherItemLetter(ActionColumn);
		_grid.PressKey(Key.F4, PhysicalKey.F4);

		_grid.TypeText(letter);
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		var picked = Items(ActionColumn).Single(item => item.Id == RecipeId(ActionColumn));
		picked.DisplayText.Should().StartWithEquivalentOf(letter, "type-ahead in an open list still picks");
	}

	[AvaloniaTheory]
	[Trait("Area", "CellKeyboard")]
	[InlineData(Key.F2, PhysicalKey.F2, RawInputModifiers.None)]
	[InlineData(Key.F4, PhysicalKey.F4, RawInputModifiers.None)]
	[InlineData(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt)]
	public void OpenKey_OnCurrentComboCell_OpensList_DisplayTakesNoFocus(
		Key key,
		PhysicalKey physicalKey,
		RawInputModifiers modifiers)
	{
		var presenter = PressThenEscape(ActionColumn, startsAtLastItem: false);

		_grid.PressKey(key, physicalKey, modifiers);

		(presenter.Editor as ComboBox)?.IsDropDownOpen.Should().BeTrue($"{modifiers} {key} opens the current cell's list");
		presenter.Focusable.Should().BeFalse("a canonical display cell takes no focus");
	}

	[AvaloniaFact]
	[Trait("Area", "CellKeyboard")]
	public void ArrowFromRow3ToRow5_ThenF4_OpensListOnRow5()
	{
		var actionColumn = _grid.DataGrid.Columns.Single(c => Equals(c.Tag, ActionColumn));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, rowIndex: 3, actionColumn);

		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
		_grid.PressKey(Key.F4, PhysicalKey.F4);

		_grid.PresenterAt(5, ActionColumn).IsEditing.Should().BeTrue("F4 opens the current cell after the arrows");
		_grid.EditorAt(5, ActionColumn).IsDropDownOpen.Should().BeTrue();
		_grid.PresenterAt(3, ActionColumn).IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	[Trait("Area", "CellKeyboard")]
	public void OpenList_ViewIsEditing_UntilTheListCloses()
	{
		var view = _grid.Window.GetVisualDescendants().OfType<CanonicalRecipeGridView>().Single();

		_grid.Click(_grid.PresenterAt(ActionRowIndex, ActionColumn));
		view.IsEditing.Should().BeTrue("an open combo list counts as editing");

		_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		view.IsEditing.Should().BeFalse("the closed list ends the edit");
	}

	[AvaloniaFact]
	public void OpenList_RowScrolledOutAndRecycled_WritesNoValue_LeavesNoComboBox()
	{
		var actionsBefore = _fixture.Coordinator.CurrentRecipe.Steps.Select(step => step.ActionKey).ToArray();
		var presenter = _grid.PresenterAt(0, ActionColumn);
		_grid.Click(presenter);
		_grid.EditorAt(0, ActionColumn).IsDropDownOpen.Should().BeTrue("precondition: the press opens the list");

		_grid.DataGrid.ScrollIntoView(_grid.Surface.RecipeRows[^1], null);
		Dispatcher.UIThread.RunJobs();

		presenter.IsEditing.Should().BeFalse("a recycled row ends the edit");
		_grid.LiveComboBoxCount().Should().Be(0, "no list stays open on the row that reuses the cell");
		_fixture.Coordinator.CurrentRecipe.Steps.Select(step => step.ActionKey)
			.Should().Equal(actionsBefore, "no value lands on any row");
	}

	[AvaloniaFact]
	public void ScrollThroughDisplayCells_AfterAnEdit_RaisesNoBindingError()
	{
		_grid.Click(_grid.PresenterAt(GroupRowIndex, GroupColumnKey()));
		_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		using var guard = new BindingErrorGuard();

		_grid.DataGrid.ScrollIntoView(_grid.Surface.RecipeRows[^1], null);
		Dispatcher.UIThread.RunJobs();
		_grid.DataGrid.ScrollIntoView(_grid.Surface.RecipeRows[0], null);
		Dispatcher.UIThread.RunJobs();

		guard.AssertNoBindingErrors();
	}

	[AvaloniaFact]
	public void GroupComboOpen_DownThenEnter_PicksNextItem()
	{
		SelectItem(GroupColumn, startsAtLastItem: false);
		var items = Items(GroupColumn);
		var nextId = items[items.FindIndex(item => item.Id == RecipeId(GroupColumn)) + 1].Id;
		_grid.Click(_grid.PresenterAt(GroupRowIndex, GroupColumnKey()));

		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		RecipeId(GroupColumn).Should().Be(nextId, "the keys of an open list still pick");
	}

	// A list at its first item ignores Up on its own, and at its last item ignores Down, so each cell starts where
	// the key could change its value.
	private static bool StartsAtLastItem(Key key)
	{
		return key is Key.Up or Key.PageUp or Key.Home or Key.Left;
	}

	private List<ComboBoxItemViewModel> Items(string column)
	{
		return column == ActionColumn
			? [.. _fixture.ComboBoxItemsProvider.ActionItems]
			: [.. _grid.Surface.RecipeRows[GroupRowIndex].GroupItemsByColumn[GroupColumnKey()]];
	}

	private void SelectItem(string column, bool startsAtLastItem)
	{
		var items = Items(column);
		var id = items[startsAtLastItem ? items.Count - 1 : 0].Id;
		if (column == ActionColumn)
		{
			_fixture.Coordinator.ChangeStepAction(ActionRowIndex, id);
		}
		else
		{
			_grid.Surface.RecipeRows[GroupRowIndex].SetPropertyValue(
				GroupColumnKey(),
				id.ToString(CultureInfo.InvariantCulture));
		}

		Dispatcher.UIThread.RunJobs();
	}

	// The path that once changed values: a press opens the list, Esc closes it, and the next key or wheel
	// notch lands on the cell.
	private LazyComboCellPresenter PressThenEscape(string column, bool startsAtLastItem)
	{
		SelectItem(column, startsAtLastItem);
		var presenter = _grid.PresenterAt(RowIndex(column), ColumnKey(column));
		_grid.Click(presenter);
		_grid.PressKey(Key.Escape, PhysicalKey.Escape);

		presenter.IsEditing.Should().BeFalse("precondition: Esc closes the list and ends the edit");
		_grid.FocusedElement().Should().BeSameAs(_grid.DataGrid, "precondition: focus is back on the grid");
		_grid.Surface.SelectedStepIndices.Should().Equal([RowIndex(column)], "precondition: the press selected the row");

		return presenter;
	}

	private static int RowIndex(string column)
	{
		return column == ActionColumn ? ActionRowIndex : GroupRowIndex;
	}

	private string ColumnKey(string column)
	{
		return column == ActionColumn ? ActionColumn : GroupColumnKey();
	}

	private string GroupColumnKey()
	{
		return _grid.Surface.RecipeRows[GroupRowIndex].GroupItemsByColumn.First(pair => pair.Value.Count > 0).Key;
	}

	// The first letter of an item that the current item does not start with, so a text search would move away.
	private string OtherItemLetter(string column)
	{
		var items = Items(column);
		var selectedLetter = items.Single(item => item.Id == RecipeId(column)).DisplayText[..1];
		var otherLetter = items
			.Select(item => item.DisplayText[..1])
			.FirstOrDefault(letter => !string.Equals(letter, selectedLetter, StringComparison.OrdinalIgnoreCase));
		otherLetter.Should().NotBeNull("precondition: an item starts with another letter");

		return otherLetter;
	}

	private int RecipeId(string column)
	{
		var step = _fixture.Coordinator.CurrentRecipe.Steps[RowIndex(column)];
		if (column == ActionColumn)
		{
			return step.ActionKey;
		}

		return step.Properties[new PropertyId(GroupColumnKey())].AsInt();
	}
}
