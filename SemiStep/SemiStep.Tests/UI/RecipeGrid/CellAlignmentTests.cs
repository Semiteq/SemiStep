using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;
using SemiStep.UI.Localization;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// Runs on the shipped MBE preset: its "Задание" column holds values in °C, %, см³/мин and without a unit.
[Trait("Component", "UI")]
[Trait("Area", "CellAlignment")]
[Trait("Category", "Integration")]
public sealed class CellAlignmentTests : IAsyncLifetime
{
	private const string TaskColumn = "task";
	private const string CommentColumn = "comment";
	private const string DurationColumn = "step_duration";
	private const int JumpTemperature = 90;
	private const int JumpPower = 120;
	private const int NitrogenRun = 150;
	private const int LoopStart = 20;
	private const int Pause = 40;
	private const int RampTemperature = 110;
	private const int RampPower = 140;
	private const int TimedOpen = 70;
	private const int TemperatureAndWait = 100;

	private readonly UIFixture _fixture = new();
	private CanonicalGridWindow? _grid;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync(ShippedConfigLocator.GetConfigDirectory("MBE"));
		_fixture.Coordinator.NewRecipe();
		foreach (var actionId in new[]
		{
			JumpTemperature, JumpPower, NitrogenRun, LoopStart, Pause, RampTemperature, TimedOpen, TemperatureAndWait,
		})
		{
			_fixture.Coordinator.AppendStep(actionId).IsSuccess.Should().BeTrue();
		}
	}

	public async ValueTask DisposeAsync()
	{
		_grid?.Dispose();
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void NumberColumn_ValuesShareOneRightEdge_UnitsShareOneLeftEdge()
	{
		ShowCanonicalGrid();
		var cells = Enumerable.Range(0, 4).Select(row => CanonicalCell(row, TaskColumn)).ToList();

		cells.Select(cell => Math.Round(RightEdge(Value(cell)), 1)).Distinct()
			.Should().ContainSingle("every value of the column ends at one right edge");
		cells.Select(cell => Math.Round(LeftEdge(Unit(cell)!), 1)).Distinct()
			.Should().ContainSingle("every unit starts at one left edge");
		cells.Select(cell => Unit(cell)!.Text).Should().Equal(["°C", "%", "см³/мин", ""]);
		Value(cells[0]).TextAlignment.Should().Be(TextAlignment.Right);
		Value(cells[0]).Text.Should().NotContain("°C", "the value half carries no unit");
	}

	[AvaloniaFact]
	public void TextCell_IsLeftAligned_TimeCell_LeavesUnitSlotEmpty()
	{
		ShowCanonicalGrid();

		Value(CanonicalCell(0, CommentColumn)).TextAlignment.Should().Be(TextAlignment.Left);
		var durationCell = CanonicalCell(5, DurationColumn);
		Value(durationCell).Text.Should().NotContain("с", "time_hms values carry no unit");
		(Unit(durationCell)?.Text ?? string.Empty).Should().BeEmpty("a time cell leaves the unit slot empty");
	}

	// A step loaded from a file or the PLC can hold a value for a column its action does not use.
	[AvaloniaFact]
	public void InapplicableCell_WithAStoredValue_ShowsNoText()
	{
		var pauseWithTask = new Step(Pause, ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.Add(new PropertyId(TaskColumn), PropertyValue.FromFloat(5)));
		_fixture.Coordinator.InsertSteps(4, [pauseWithTask]).IsSuccess.Should().BeTrue();
		_fixture.Coordinator.CurrentRecipe.Steps[4].Properties.Should().ContainKey(
			new PropertyId(TaskColumn),
			"precondition: the pause step holds a task value");
		ShowCanonicalGrid();
		var inapplicable = CanonicalCell(4, TaskColumn);

		inapplicable.GetSelfAndVisualDescendants().OfType<TextBlock>()
			.Where(text => text.IsEffectivelyVisible)
			.Select(text => text.Text)
			.Should().AllSatisfy(text => text.Should().BeNullOrEmpty("an inapplicable cell shows no text and no dash"));
	}

	[AvaloniaFact]
	public void BothGrids_WithUnitsAndARunningLoop_RaiseNoBindingError()
	{
		_fixture.Coordinator.ChangeStepAction(3, JumpTemperature).IsSuccess.Should().BeTrue();
		_fixture.Coordinator.AppendStep(LoopStart).IsSuccess.Should().BeTrue();
		_fixture.Coordinator.UpdateStepProperty(8, TaskColumn, "3").IsSuccess.Should().BeTrue();
		_fixture.Coordinator.AppendStep(JumpTemperature).IsSuccess.Should().BeTrue();
		_fixture.Coordinator.AppendStep(30).IsSuccess.Should().BeTrue();
		using var guard = new BindingErrorGuard();

		ShowCanonicalGrid();
		ShowTransposedGrid();
		_fixture.S7Service.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true, ActualLine: 9, StepCurrentTime: 0f, ForLoopCount1: 2, ForLoopCount2: 0, ForLoopCount3: 0));
		Dispatcher.UIThread.RunJobs();
		_grid!.DataGrid.ScrollIntoView(_grid.Surface.RecipeRows[^1], null);
		Dispatcher.UIThread.RunJobs();

		_grid.Surface.RecipeRows[8].LoopPassText.Should().Be("(2/3)", "precondition: the loop runs");
		guard.AssertNoBindingErrors();
	}

	[AvaloniaFact]
	public void UnitSlot_IsSizedByTheWidestUnitOfItsColumn_TimeUnitsDoNotCount()
	{
		var calculator = new ColumnWidthCalculator(_fixture.RecipeMetadataRegistry, _fixture.AppConfiguration.GridStyle);

		calculator.UnitSlotWidth(DurationColumn).Should().Be(0, "a time_hms unit takes no slot");
		calculator.UnitSlotWidth(TaskColumn).Should().BeGreaterThan(
			calculator.UnitSlotWidth("initial_value"),
			"the task column holds см³/мин, wider than the °C and % of the initial value column");
		calculator.WidestUnitSlotWidth().Should().BeGreaterThanOrEqualTo(calculator.UnitSlotWidth(TaskColumn));
	}

	[AvaloniaFact]
	public void TransposedNumberCells_ShowUnitSlot_WithSharedRightEdge()
	{
		var stepListBox = ShowTransposedGrid();
		var cells = Enumerable.Range(0, 3)
			.Select(column => TransposedCell(stepListBox, column, TaskColumn))
			.ToList();

		cells.Select(cell => Unit(cell)!.Text).Should().Equal(["°C", "%", "см³/мин"]);
		cells.Select(cell => Math.Round(RightEdge(Value(cell)) - LeftEdge(cell), 1)).Distinct()
			.Should().ContainSingle("values end at one offset inside every step column");
	}

	[AvaloniaFact]
	public void CanonicalNumberEditor_EndsItsTextWhereTheDisplayEndsIt()
	{
		ShowCanonicalGrid();
		var displayRightEdge = RightEdge(Value(CanonicalCell(0, TaskColumn)));
		var column = _grid!.DataGrid.Columns.Single(c => Equals(c.Tag, TaskColumn));
		DataGridTestHelper.SetCurrentCell(_grid.DataGrid, 0, column);

		_grid.PressKey(Key.F2, PhysicalKey.F2);

		var editor = _grid.DataGrid.GetVisualDescendants().OfType<TextBox>().Single();
		EditorTextRightEdge(editor).Should().BeApproximately(
			displayRightEdge,
			0.5,
			"the digits stay in place when the edit starts");
	}

	[AvaloniaFact]
	public void TransposedNumberEditor_EndsItsTextWhereTheDisplayEndsIt()
	{
		var stepListBox = ShowTransposedGrid();
		var cell = TransposedCell(stepListBox, 0, TaskColumn);
		var displayRightEdge = RightEdge(Value(cell));
		cell.GetVisualDescendants().OfType<TransposedTextCellPresenter>().Single().Focus();
		Dispatcher.UIThread.RunJobs();

		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		var editor = cell.GetVisualDescendants().OfType<TextBox>().Single();
		EditorTextRightEdge(editor).Should().BeApproximately(
			displayRightEdge,
			0.5,
			"the digits stay in place when the edit starts");
	}

	[AvaloniaFact]
	[Trait("Area", "RampTooltip")]
	public void RampStepDuration_HasRampTooltip_InBothGrids()
	{
		ShowCanonicalGrid();

		TipOf(CanonicalCell(5, DurationColumn)).Should().Be(Resources.RampTimeToolTip);

		var stepListBox = ShowTransposedGrid();
		TipOf(TransposedCell(stepListBox, 5, DurationColumn)).Should().Be(Resources.RampTimeToolTip);
	}

	[AvaloniaFact]
	[Trait("Area", "RampTooltip")]
	public void NonRampStepDuration_HasNoTooltip()
	{
		ShowCanonicalGrid();

		TipOf(CanonicalCell(6, DurationColumn)).Should().BeNull("a timed open is not a ramp");
		TipOf(CanonicalCell(7, DurationColumn)).Should().BeNull("a long-lasting step has no ramp time");
		RampActions.IsRamp(RampPower).Should().BeTrue();
	}

	private void ShowCanonicalGrid()
	{
		_grid = new CanonicalGridWindow(_fixture);
		CellPaletteInstaller.Install(_grid.Window.Resources, _fixture.AppConfiguration.GridStyle);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowTransposedGrid()
	{
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 1600, Height = 800, Content = view };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}

	private Control CanonicalCell(int rowIndex, string columnKey)
	{
		var column = _grid!.DataGrid.Columns.Single(c => Equals(c.Tag, columnKey));

		return column.GetCellContent(_grid.Surface.RecipeRows[rowIndex])!;
	}

	private static Control TransposedCell(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<Border>()
			.Single(border => border.Classes.Contains("transposed-cell")
				&& border.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static object? TipOf(Control cell)
	{
		return cell.GetSelfAndVisualDescendants().OfType<Control>().Select(ToolTip.GetTip).FirstOrDefault(tip => tip is not null);
	}

	private static TextBlock Value(Control cell)
	{
		return cell.GetSelfAndVisualDescendants().OfType<TextBlock>()
			.FirstOrDefault(text => text.Classes.Contains(TextCellLayout.ValueClass))
			?? cell.GetSelfAndVisualDescendants().OfType<TextBlock>().First();
	}

	private static TextBlock? Unit(Control cell)
	{
		return cell.GetSelfAndVisualDescendants().OfType<TextBlock>()
			.FirstOrDefault(text => text.Classes.Contains(TextCellLayout.UnitClass));
	}

	private double RightEdge(TextBlock text)
	{
		return text.TranslatePoint(new Point(text.Bounds.Width - text.Padding.Right, 0), Root())!.Value.X;
	}

	// The editor right-aligns its text inside its presenter, so the end of the laid-out line is the text's right edge.
	private double EditorTextRightEdge(TextBox editor)
	{
		var presenter = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
		var line = presenter.TextLayout.TextLines[0];

		var textEnd = new Point(line.Start + line.WidthIncludingTrailingWhitespace, 0);

		return presenter.TranslatePoint(textEnd, Root())!.Value.X;
	}

	private double LeftEdge(Visual visual)
	{
		var padding = visual is TextBlock text ? text.Padding.Left : 0;

		return visual.TranslatePoint(new Point(padding, 0), Root())!.Value.X;
	}

	private Visual Root()
	{
		return (Visual?)_window ?? _grid!.Window;
	}
}
