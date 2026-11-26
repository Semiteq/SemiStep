using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// The run's tints and glyphs on a real surface: end vs stop from the last line seen while active, and the
// clear on the next edit or load made while no run is active.
[Trait("Component", "UI")]
[Trait("Area", "ExecutionHighlight")]
[Trait("Category", "Integration")]
public sealed class ExecutionRunStateTests : IAsyncLifetime
{
	private const int StepCount = 6;

	// Puts the running row's number at four digits, the widest the № column is sized for.
	private const int FourDigitStepCount = 1000;

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(StepCount);
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void RunEnd_LastLineAtStepCount_EveryRowPast_NoGlyph()
	{
		Run(0, 3, StepCount);
		Stop();

		RowStates().Should().Equal(Repeat("past", StepCount));
	}

	[AvaloniaFact]
	public void RunEnd_InactiveEmissionAtStepCount_AfterTheLastStep_EveryRowPast_NoGlyph()
	{
		Run(0, StepCount - 1);
		Push(PlcExecutionInfo.Empty with { ActualLine = StepCount });

		RowStates().Should().Equal(Repeat("past", StepCount));
		_surface.RecipeRows.Should().OnlyContain(row => !row.StepNumberText.StartsWith('■'));
	}

	[AvaloniaFact]
	public void RunEnd_LastLineOnTheLastStep_StopGlyphOnTheLastRow()
	{
		Run(0, StepCount - 1);
		Stop();

		RowStates().Should().Equal([.. Repeat("past", StepCount - 1), "stop"]);
		Row(StepCount - 1).StepNumberText.Should().StartWith("■");
	}

	[AvaloniaFact]
	public void RunStop_OnRow2_RowsAbovePast_StopGlyphOnRow2()
	{
		Run(0, 1, 2);
		Stop();

		RowStates().Should().Equal("past", "past", "stop", "idle", "idle", "idle");
		Row(2).StepNumberText.Should().Be("■ 3");
	}

	[AvaloniaFact]
	public void RunEnd_LastLineMinusOne_NoGlyph()
	{
		Run(-1);
		Stop();

		RowStates().Should().Equal(Repeat("idle", StepCount));
		_surface.RecipeRows.Should().OnlyContain(row => !row.StepNumberText.StartsWith('■'));
	}

	[AvaloniaFact]
	public void EmptyEmissionAfterLine4_StopsOnRow4_NotRow0()
	{
		Run(4);
		Push(PlcExecutionInfo.Empty);

		RowStates().Should().Equal("past", "past", "past", "past", "stop", "idle");
	}

	[AvaloniaFact]
	public void Running_RowShowsRunningGlyph_AndTheGlyphMovesWithTheLine()
	{
		Run(1);
		Row(1).StepNumberText.Should().Be("► 2");

		Run(2);

		Row(1).StepNumberText.Should().Be("2");
		Row(2).StepNumberText.Should().Be("► 3");
		RowStates().Should().Equal("past", "past", "running", "idle", "idle", "idle");
	}

	[AvaloniaFact]
	public void CellEdit_AfterARun_ClearsEveryTintAndGlyph()
	{
		Run(2);
		Stop();

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "7").IsSuccess.Should().BeTrue();

		RowStates().Should().Equal(Repeat("idle", StepCount));
	}

	[AvaloniaTheory]
	[InlineData("append")]
	[InlineData("insert")]
	[InlineData("remove")]
	[InlineData("paste")]
	public void StepGraphEdit_AfterARun_ClearsEveryTintAndGlyph(string edit)
	{
		Run(2);
		Stop();

		var result = edit switch
		{
			"append" => _fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId),
			"insert" => _fixture.Coordinator.InsertStep(1, RecipeTestDriver.WaitActionId),
			"remove" => _fixture.Coordinator.RemoveStep(4),
			_ => _fixture.Coordinator.InsertSteps(1, [_fixture.Coordinator.CurrentRecipe.Steps[0]]),
		};
		result.IsSuccess.Should().BeTrue();

		RowStates().Should().OnlyContain(state => state == "idle");
		_surface.RecipeRows.Should().OnlyContain(row => !row.StepNumberText.StartsWith('■'));
	}

	[AvaloniaFact]
	public void Undo_AfterARun_ClearsEveryTintAndGlyph()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "7").IsSuccess.Should().BeTrue();
		Run(2);
		Stop();

		_fixture.Coordinator.Undo().IsSuccess.Should().BeTrue();

		RowStates().Should().Equal(Repeat("idle", StepCount));
	}

	[AvaloniaFact]
	public void ActionChange_AfterARun_ClearsThePastTint_AndTheResetCellsAreOrange()
	{
		Run(3);
		Stop();

		_fixture.Coordinator.ChangeStepAction(1, RecipeTestDriver.WithGroupActionId).IsSuccess.Should().BeTrue();

		RowStates().Should().Equal(Repeat("idle", StepCount));
		Row(1).ChangedColumns.Should().NotBeEmpty();
	}

	[AvaloniaFact]
	public async Task Save_AfterARun_KeepsTheTintsAndTheGlyph()
	{
		Run(2);
		Stop();
		var path = Path.Combine(Path.GetTempPath(), $"semistep-run-save-{Guid.NewGuid():N}.csv");
		try
		{
			(await _fixture.Coordinator.SaveRecipeAsync(path)).IsSuccess.Should().BeTrue();
			Dispatcher.UIThread.RunJobs();
		}
		finally
		{
			File.Delete(path);
		}

		RowStates().Should().Equal("past", "past", "stop", "idle", "idle", "idle");
	}

	[AvaloniaFact]
	public void RunStart_ClearsThePreviousRunsTintsAndGlyph()
	{
		Run(3);
		Stop();

		Run(0);

		RowStates().Should().Equal("running", "idle", "idle", "idle", "idle", "idle");
	}

	[AvaloniaFact]
	public async Task RecipeReplacedDuringARun_TheNextEmissionRepaintsRunningAndPast()
	{
		Run(3);
		await _fixture.ReloadFromCsvAsync();
		Dispatcher.UIThread.RunJobs();

		Run(3);

		RowStates().Should().Equal("past", "past", "past", "running", "idle", "idle");
	}

	[AvaloniaFact]
	public void CanonicalRunningRow_NumberCellTextStartsWithRunningGlyph()
	{
		var dataGrid = ShowCanonical();
		Run(1);

		var row = dataGrid.GetVisualDescendants().OfType<DataGridRow>()
			.First(candidate => ReferenceEquals(candidate.DataContext, Row(1)));
		var numberCell = row.GetVisualDescendants().OfType<DataGridCell>().First();

		numberCell.GetVisualDescendants().OfType<TextBlock>().First().Text.Should().StartWith("►");
	}

	[AvaloniaFact]
	public void RunStartAndStop_MoveNoColumn()
	{
		var template = _fixture.Coordinator.CurrentRecipe.Steps[0];
		_fixture.Coordinator.InsertSteps(0, [.. Enumerable.Repeat(template, FourDigitStepCount)]).IsSuccess.Should().BeTrue();
		var dataGrid = ShowCanonical();
		var idleWidths = ColumnWidths(dataGrid);

		Run(FourDigitStepCount);
		var runningWidths = ColumnWidths(dataGrid);
		Stop();
		var stopWidths = ColumnWidths(dataGrid);

		runningWidths.Should().Equal(idleWidths, "the running glyph neither widens the № column nor moves a column");
		stopWidths.Should().Equal(idleWidths, "the stop glyph neither widens the № column nor moves a column");
		dataGrid.Columns[0].Width.IsAbsolute.Should().BeTrue("the № column is sized for the glyph up front");
	}

	[AvaloniaFact]
	public void TransposedRunningColumn_HeaderTextStartsWithRunningGlyph()
	{
		var stepListBox = ShowTransposed();

		Run(1);

		StepHeaderText(stepListBox, 1).Text.Should().StartWith("►");
	}

	[AvaloniaFact]
	public void TransposedRunningAndStopHeaders_AreBlack_IdleHeadersKeepTheHeaderColour()
	{
		var stepListBox = ShowTransposed();
		var headerColour = _fixture.AppConfiguration.GridStyle.Chrome.HeaderForeground.ToMediaColor();
		headerColour.Should().NotBe(Colors.Black, "the header colour must differ from black for the check to mean anything");

		Run(1);
		var runningHeader = ForegroundOf(StepHeaderText(stepListBox, 1));
		Stop();

		runningHeader.Should().Be(Colors.Black);
		ForegroundOf(StepHeaderText(stepListBox, 1)).Should().Be(Colors.Black, "the stop header is black too");
		ForegroundOf(StepHeaderText(stepListBox, 2)).Should().Be(headerColour);
	}

	private ListBox ShowTransposed()
	{
		var transposed = _fixture.CreateTransposedSurface();
		transposed.Initialize();
		var view = new TransposedRecipeGridView { DataContext = transposed };
		var stepListBox = view.FindControl<ListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		_window = new Window { Width = 1200, Height = 600 };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		_window.Content = view;
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}

	private static TextBlock StepHeaderText(ListBox stepListBox, int stepIndex)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(stepIndex)!;
		return container.GetVisualDescendants().OfType<Border>()
			.First(border => border.Classes.Contains("transposed-step-header"))
			.GetVisualDescendants().OfType<TextBlock>().First();
	}

	private static Color? ForegroundOf(TextBlock textBlock)
	{
		return (textBlock.Foreground as ISolidColorBrush)?.Color;
	}

	private static List<double> ColumnWidths(DataGrid dataGrid)
	{
		Dispatcher.UIThread.RunJobs();
		return [.. dataGrid.Columns.Select(column => column.ActualWidth)];
	}

	private static IEnumerable<string> Repeat(string state, int count)
	{
		return Enumerable.Repeat(state, count);
	}

	private DataGrid ShowCanonical()
	{
		var view = new CanonicalRecipeGridView { DataContext = _surface };
		_window = new Window { Width = 1600, Height = 600 };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		_window.Content = view;
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return view.FindControl<DataGrid>("RecipeGrid")!;
	}

	private void Run(params int[] lines)
	{
		foreach (var line in lines)
		{
			Push(PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = line });
		}
	}

	private void Stop()
	{
		Push(PlcExecutionInfo.Empty);
	}

	private void Push(PlcExecutionInfo info)
	{
		_fixture.S7Service.PushExecutionState(info);
		Dispatcher.UIThread.RunJobs();
	}

	private RecipeRowViewModel Row(int index)
	{
		return _surface.RecipeRows[index];
	}

	private List<string> RowStates()
	{
		return [.. _surface.RecipeRows.Select(row =>
			row.IsCurrentStep ? "running"
			: row.IsStopStep ? (row.IsPastStep ? "stop" : "stop-not-past")
			: row.IsPastStep ? "past"
			: "idle")];
	}
}
