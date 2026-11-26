using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Coordinator;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "CellKeyboard")]
[Trait("Category", "Integration")]
public sealed class CanonicalCellKeyboardTests : IAsyncLifetime
{
	private const int SeededStepCount = 10;
	private const int RowIndex = 2;

	private readonly UIFixture _fixture = new();
	private CanonicalGridWindow _grid = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);
		_grid = new CanonicalGridWindow(_fixture);
	}

	public async ValueTask DisposeAsync()
	{
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void PrintableKey_OnCurrentTextCell_StartsEditWithTypedCharacterOnly()
	{
		MakeCommentCellCurrent();

		_grid.Window.TypeKey(PhysicalKey.Digit5, "5");
		Dispatcher.UIThread.RunJobs();

		EditingTextBox().Should().NotBeNull("a printable key starts the edit");
		EditingTextBox()!.Text.Should().Be("5", "the typed character replaces the value, exactly once");
		EditingTextBox()!.CaretIndex.Should().Be(1, "the caret sits after the typed character");
	}

	[AvaloniaFact]
	public void TwoPrintableKeys_OnCurrentTextCell_TypeBothInOrder()
	{
		MakeCommentCellCurrent();

		_grid.Window.TypeKey(PhysicalKey.A, "a");
		_grid.Window.TypeKey(PhysicalKey.B, "b");
		Dispatcher.UIThread.RunJobs();

		EditingTextBox()!.Text.Should().Be("ab", "both characters land on the editor in order with no drop or double");
	}

	[AvaloniaFact]
	public void TypedValue_Enter_CommitsIt()
	{
		MakeCommentCellCurrent();
		_grid.Window.TypeKey(PhysicalKey.Digit5, "5");
		Dispatcher.UIThread.RunJobs();

		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		CommentOf(RowIndex).Should().Be("5", "Enter commits the typed value");
	}

	[AvaloniaFact]
	public void F2_OnTextCell_StartsEditKeepingValue_EscapeCancels()
	{
		_fixture.Coordinator.UpdateStepProperty(RowIndex, RecipeTestDriver.CommentColumn, "abc").IsSuccess
			.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();
		MakeCommentCellCurrent();

		_grid.PressKey(Key.F2, PhysicalKey.F2);
		EditingTextBox()!.Text.Should().Be("abc", "F2 keeps the value");
		_grid.TypeText("x");
		_grid.PressKey(Key.Escape, PhysicalKey.Escape);

		CommentOf(RowIndex).Should().Be("abc", "Esc cancels the edit");
		EditingTextBox().Should().BeNull("Esc ends the edit");
	}

	[AvaloniaTheory]
	[InlineData(RecipeTestDriver.StepDurationColumn)]
	[InlineData(RecipeTestDriver.TaskColumn)]
	public void Escape_OnTimeAndFloatCells_WritesNothing_LeavesRecipeClean(string columnKey)
	{
		_fixture.Coordinator.ChangeStepAction(RowIndex, columnKey == RecipeTestDriver.TaskColumn
			? RecipeTestDriver.ForLoopActionId
			: RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();
		var stepBefore = _fixture.Coordinator.CurrentRecipe.Steps[RowIndex];
		var dirtyBefore = _fixture.Coordinator.IsDirty;
		var mutations = 0;
		void OnMutated(MutationSignal signal)
		{
			mutations++;
		}

		var column = _grid.DataGrid.Columns.Single(c => Equals(c.Tag, columnKey));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, RowIndex, column);
		_fixture.Coordinator.Mutated += OnMutated;
		try
		{
			_grid.PressKey(Key.F2, PhysicalKey.F2);
			_grid.TypeText("9");
			_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		}
		finally
		{
			_fixture.Coordinator.Mutated -= OnMutated;
		}

		mutations.Should().Be(0, "Esc writes nothing back");
		_fixture.Coordinator.CurrentRecipe.Steps[RowIndex].Should().Be(stepBefore);
		_fixture.Coordinator.IsDirty.Should().Be(dirtyBefore, "Esc leaves the recipe as clean as it was");
	}

	[AvaloniaFact]
	public async Task Escape_OnLoadedFractionalDuration_KeepsValueAndHistory()
	{
		await LoadFractionalDurationAsync();
		var stepBefore = _fixture.Coordinator.CurrentRecipe.Steps[RowIndex];
		var mutations = 0;
		void OnMutated(MutationSignal signal)
		{
			mutations++;
		}

		_fixture.Coordinator.Mutated += OnMutated;
		try
		{
			_grid.PressKey(Key.F2, PhysicalKey.F2);
			_grid.PressKey(Key.Escape, PhysicalKey.Escape);
		}
		finally
		{
			_fixture.Coordinator.Mutated -= OnMutated;
		}

		mutations.Should().Be(0, "Esc writes nothing back");
		_fixture.Coordinator.CurrentRecipe.Steps[RowIndex].Should().Be(stepBefore);
		StoredDuration().Should().Be(90.5f, "Esc keeps the stored value");
		_fixture.Coordinator.CanUndo.Should().BeFalse("Esc adds no undo entry");
	}

	// The display drops the fraction; the editor must show it, in the invariant culture the model parses.
	[AvaloniaFact]
	public async Task F2ThenEnter_OnFractionalDuration_UnderRuRu_KeepsValue()
	{
		using var culture = new CultureScope("ru-RU");
		await LoadFractionalDurationAsync();
		_fixture.SetSyncEnabled(true);
		Dispatcher.UIThread.RunJobs();

		_grid.PressKey(Key.F2, PhysicalKey.F2);
		EditingTextBox()!.Text.Should().Be("00:01:30.5", "the editor shows the stored fraction");
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		StoredDuration().Should().Be(90.5f, "an unchanged edit stores the value it showed");
		_fixture.MessagePanel.Entries.Should().NotContain(entry => entry.IsError, "the shown text parses back");
		_fixture.PlcSyncService.NotifyRecipeChangedCalls
			.Select(call => DurationOf(call.Recipe))
			.Should().AllBeEquivalentTo(90.5f, "the PLC receives the stored duration");
	}

	[AvaloniaFact]
	public async Task TypedDotValue_UnderRuRu_StoresItAndSendsItToThePlc()
	{
		using var culture = new CultureScope("ru-RU");
		await LoadFractionalDurationAsync();
		_fixture.SetSyncEnabled(true);
		Dispatcher.UIThread.RunJobs();

		_grid.PressKey(Key.F2, PhysicalKey.F2);
		EditingTextBox()!.SelectAll();
		_grid.TypeText("75.5");
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		StoredDuration().Should().Be(75.5f);
		DurationOf(_fixture.PlcSyncService.NotifyRecipeChangedCalls[^1].Recipe).Should().Be(75.5f);
	}

	// A comma is a decimal separator, never a thousands separator.
	[AvaloniaFact]
	public async Task TypedCommaValue_UnderRuRu_StoresTheFraction()
	{
		using var culture = new CultureScope("ru-RU");
		await LoadFractionalDurationAsync();

		_grid.PressKey(Key.F2, PhysicalKey.F2);
		EditingTextBox()!.SelectAll();
		_grid.TypeText("75,5");
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		StoredDuration().Should().Be(75.5f, "a decimal comma stores the fraction, it never stores 755");
	}

	[AvaloniaFact]
	public async Task TypedValueWithBothSeparators_UnderRuRu_KeepsTheStoredValue()
	{
		using var culture = new CultureScope("ru-RU");
		await LoadFractionalDurationAsync();

		_grid.PressKey(Key.F2, PhysicalKey.F2);
		EditingTextBox()!.SelectAll();
		_grid.TypeText("1.234,5");
		_grid.PressKey(Key.Enter, PhysicalKey.Enter);

		StoredDuration().Should().Be(90.5f, "a value with both separators is an input error");
	}

	[AvaloniaFact]
	public void PrintableKey_WhileLocked_StartsNoEdit()
	{
		MakeCommentCellCurrent();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		_grid.Window.TypeKey(PhysicalKey.Digit5, "5");
		Dispatcher.UIThread.RunJobs();

		EditingTextBox().Should().BeNull("a locked grid starts no edit");
	}

	private void MakeCommentCellCurrent()
	{
		var column = _grid.DataGrid.Columns.Single(c => Equals(c.Tag, RecipeTestDriver.CommentColumn));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, RowIndex, column);
	}

	private TextBox? EditingTextBox()
	{
		return _grid.DataGrid.GetVisualDescendants().OfType<TextBox>().SingleOrDefault();
	}

	// Loads a 90.5 s duration from a CSV file, with an empty history, and makes its cell current.
	private async Task LoadFractionalDurationAsync()
	{
		_fixture.Coordinator.UpdateStepProperty(RowIndex, RecipeTestDriver.StepDurationColumn, "90.5").IsSuccess
			.Should().BeTrue();
		await _fixture.ReloadFromCsvAsync();
		Dispatcher.UIThread.RunJobs();
		StoredDuration().Should().Be(90.5f, "precondition: the file holds a fractional duration");
		_fixture.Coordinator.CanUndo.Should().BeFalse("precondition: an opened recipe starts with no history");
		var column = _grid.DataGrid.Columns.Single(c => Equals(c.Tag, RecipeTestDriver.StepDurationColumn));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, RowIndex, column);
	}

	private object? StoredDuration()
	{
		return DurationOf(_fixture.Coordinator.CurrentRecipe);
	}

	private static object? DurationOf(Recipe recipe)
	{
		return recipe.Steps[RowIndex].Properties[new PropertyId(RecipeTestDriver.StepDurationColumn)].Value;
	}

	private string? CommentOf(int stepIndex)
	{
		return _fixture.Coordinator.CurrentRecipe.Steps[stepIndex].Properties
			.TryGetValue(new PropertyId(RecipeTestDriver.CommentColumn), out var value)
			? value.Value?.ToString()
			: null;
	}
}
