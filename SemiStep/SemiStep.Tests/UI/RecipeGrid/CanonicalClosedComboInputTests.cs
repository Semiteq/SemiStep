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
[Trait("Area", "ComboOpen")]
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
	public void ClosedCombo_NavigationKey_KeepsValue_FocusStaysInGrid(
		string column,
		Key key,
		PhysicalKey physicalKey)
	{
		var comboBox = FocusClosedCombo(column, StartsAtLastItem(key));
		var idBefore = RecipeId(column);

		_grid.PressKey(key, physicalKey);

		RecipeId(column).Should().Be(idBefore, $"{key} on a closed {column} combo changes no value");
		ShownId(comboBox).Should().Be(idBefore, $"the {column} combo still shows the recipe value");
		comboBox.IsDropDownOpen.Should().BeFalse($"{key} opens no list");
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
	public void ClosedCombo_VerticalArrow_MovesRowSelection(
		string column,
		Key key,
		PhysicalKey physicalKey,
		int rowOffset)
	{
		FocusClosedCombo(column, StartsAtLastItem(key));

		_grid.PressKey(key, physicalKey);

		_grid.Surface.SelectedStepIndices.Should().Equal(
			[RowIndex(column) + rowOffset],
			$"{key} moves the row as in any other cell");
	}

	[AvaloniaTheory]
	[InlineData(ActionColumn)]
	[InlineData(GroupColumn)]
	public void ClosedCombo_Wheel_KeepsValue_ScrollsGrid(string column)
	{
		var comboBox = FocusClosedCombo(column, startsAtLastItem: false);
		var idBefore = RecipeId(column);
		var offsetBefore = _grid.VerticalScrollOffset();

		_grid.Wheel(comboBox, notches: -1);

		RecipeId(column).Should().Be(idBefore, $"the wheel over a closed {column} combo changes no value");
		ShownId(comboBox).Should().Be(idBefore);
		_grid.VerticalScrollOffset().Should().BeGreaterThan(offsetBefore, "the wheel scrolls the grid");
	}

	[AvaloniaTheory]
	[InlineData(ActionColumn)]
	[InlineData(GroupColumn)]
	public void ClosedCombo_TypedLetter_KeepsValue(string column)
	{
		var comboBox = FocusClosedCombo(column, startsAtLastItem: false);
		var idBefore = RecipeId(column);

		_grid.TypeText(OtherItemLetter(comboBox));

		RecipeId(column).Should().Be(idBefore, $"typing on a closed {column} combo changes no value");
		ShownId(comboBox).Should().Be(idBefore);
	}

	[AvaloniaFact]
	public void OpenList_TypedLetterThenEnter_PicksMatchingItem()
	{
		var comboBox = FocusClosedCombo(ActionColumn, startsAtLastItem: false);
		var letter = OtherItemLetter(comboBox);
		_grid.PressKey(Key.F4, PhysicalKey.F4);

		_grid.TypeText(letter);
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		var picked = (ComboBoxItemViewModel)comboBox.SelectedItem!;
		picked.DisplayText.Should().StartWithEquivalentOf(letter, "type-ahead in an open list still picks");
		RecipeId(ActionColumn).Should().Be(picked.Id);
	}

	[AvaloniaFact]
	public void RealTab_ThenDown_KeepsValue_MovesRow()
	{
		var pressedComboBox = Combo(ActionColumn);
		_grid.Click(pressedComboBox);
		_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		var selectionBefore = _grid.Surface.SelectedStepIndices.ToArray();

		_grid.PressKey(Key.Tab, PhysicalKey.Tab);
		var focusedComboBox = _grid.FocusedElement().Should().BeOfType<ComboBox>("Tab moves focus onto a combo cell")
			.Subject;
		var row = (RecipeRowViewModel)focusedComboBox.DataContext!;
		var stepIndex = _grid.Surface.RecipeRows.IndexOf(row);
		var shownBefore = ShownId(focusedComboBox);
		var actionBefore = _fixture.Coordinator.CurrentRecipe.Steps[stepIndex].ActionKey;

		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);

		ShownId(focusedComboBox).Should().Be(shownBefore, "Down on a closed combo reached by Tab changes no value");
		_fixture.Coordinator.CurrentRecipe.Steps[stepIndex].ActionKey.Should().Be(actionBefore);
		_grid.Surface.SelectedStepIndices.Should().NotEqual(selectionBefore, "Down moves the row");
	}

	[AvaloniaTheory]
	[InlineData(Key.F4, PhysicalKey.F4, RawInputModifiers.None)]
	[InlineData(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt)]
	[InlineData(Key.Up, PhysicalKey.ArrowUp, RawInputModifiers.Alt)]
	[InlineData(Key.Enter, PhysicalKey.Enter, RawInputModifiers.None)]
	[InlineData(Key.Space, PhysicalKey.Space, RawInputModifiers.None)]
	public void ClosedCombo_OpenKey_OpensList(Key key, PhysicalKey physicalKey, RawInputModifiers modifiers)
	{
		var comboBox = FocusClosedCombo(ActionColumn, startsAtLastItem: false);

		_grid.PressKey(key, physicalKey, modifiers);

		comboBox.IsDropDownOpen.Should().BeTrue($"{modifiers} {key} opens the list of a focused combo");
	}

	[AvaloniaFact]
	public void GroupComboOpen_DownThenEnter_PicksNextItem()
	{
		SelectItem(GroupColumn, startsAtLastItem: false);
		var comboBox = Combo(GroupColumn);
		var nextId = ((ComboBoxItemViewModel)comboBox.Items[comboBox.SelectedIndex + 1]!).Id;
		_grid.Click(comboBox);

		_grid.PressKey(Key.Down, PhysicalKey.ArrowDown);
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		RecipeId(GroupColumn).Should().Be(nextId, "the keys of an open list still pick");
		ShownId(comboBox).Should().Be(nextId);
	}

	// A closed combo at its first item ignores Up on its own, and at its last item ignores Down, so each combo starts
	// where the key could change its value.
	private static bool StartsAtLastItem(Key key)
	{
		return key is Key.Up or Key.PageUp or Key.Home or Key.Left;
	}

	private void SelectItem(string column, bool startsAtLastItem)
	{
		var items = Combo(column).Items;
		var id = ((ComboBoxItemViewModel)items[startsAtLastItem ? items.Count - 1 : 0]!).Id;
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

	// A press and Esc select the row and leave focus on the grid; Tab is how keyboard focus reaches a combo cell.
	private ComboBox FocusClosedCombo(string column, bool startsAtLastItem)
	{
		SelectItem(column, startsAtLastItem);
		var comboBox = Combo(column);
		_grid.Click(comboBox);
		_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		comboBox.Focus(NavigationMethod.Tab);
		Dispatcher.UIThread.RunJobs();

		comboBox.IsDropDownOpen.Should().BeFalse("precondition: Esc closes the list");
		comboBox.IsFocused.Should().BeTrue("precondition: the combo holds keyboard focus");
		_grid.Surface.SelectedStepIndices.Should().Equal([RowIndex(column)], "precondition: the press selected the row");

		return comboBox;
	}

	private ComboBox Combo(string column)
	{
		return _grid.ComboAt(RowIndex(column), ColumnKey(column));
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

	// The first letter of an item that the selected item does not start with, so a text search would move away from it.
	private static string OtherItemLetter(ComboBox comboBox)
	{
		var selectedLetter = ((ComboBoxItemViewModel)comboBox.SelectedItem!).DisplayText[..1];
		var otherLetter = comboBox.Items
			.Cast<ComboBoxItemViewModel>()
			.Select(item => item.DisplayText[..1])
			.FirstOrDefault(letter => !string.Equals(letter, selectedLetter, StringComparison.OrdinalIgnoreCase));
		otherLetter.Should().NotBeNull("precondition: an item starts with another letter");

		return otherLetter!;
	}

	private static int ShownId(ComboBox comboBox)
	{
		return ((ComboBoxItemViewModel)comboBox.SelectedItem!).Id;
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
