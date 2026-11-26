using Avalonia;
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

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// The loops.csv shape: an outer loop of 3 passes around an inner loop of 5 passes.
[Trait("Component", "UI")]
[Trait("Area", "LoopBadge")]
[Trait("Category", "Integration")]
public sealed class LoopBadgeTests : IAsyncLifetime
{
	private const int OuterStart = 0;
	private const int InnerStart = 2;
	private const string ActionColumn = "action";
	private static readonly Color _badgeViolet = Color.Parse("#5E35A8");

	private readonly UIFixture _fixture = new();
	private CanonicalGridWindow _grid = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		var coordinator = _fixture.Coordinator;
		coordinator.NewRecipe();
		foreach (var actionId in new[]
		{
			RecipeTestDriver.ForLoopActionId, RecipeTestDriver.WaitActionId, RecipeTestDriver.ForLoopActionId,
			RecipeTestDriver.WaitActionId, RecipeTestDriver.EndForLoopActionId, RecipeTestDriver.WaitActionId,
			RecipeTestDriver.EndForLoopActionId,
		})
		{
			coordinator.AppendStep(actionId).IsSuccess.Should().BeTrue();
		}

		coordinator.UpdateStepProperty(OuterStart, RecipeTestDriver.TaskColumn, "3").IsSuccess.Should().BeTrue();
		coordinator.UpdateStepProperty(InnerStart, RecipeTestDriver.TaskColumn, "5").IsSuccess.Should().BeTrue();
		_grid = new CanonicalGridWindow(_fixture);
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Counters220_ShowOuterAndInnerPills_OnTheirLoopStartRowsOnly()
	{
		Push(Running(line: 3, depth1: 2, depth2: 2));

		PassTexts().Should().Equal(["(2/3)", null, "(2/5)", null, null, null, null]);
		VisiblePillText(OuterStart).Should().Be("(2/3)");
		VisiblePillText(InnerStart).Should().Be("(2/5)");
		VisiblePillText(1).Should().BeNull("a row that starts no loop shows no pill");
	}

	[AvaloniaFact]
	public void ZeroCounters_OrRecipeInactive_ShowNoPill()
	{
		Push(Running(line: 3, depth1: 0, depth2: 0));
		PassTexts().Should().AllSatisfy(text => text.Should().BeNull("a zero counter means no running loop"));

		Push(Running(line: 3, depth1: 2, depth2: 2) with { RecipeActive = false });
		PassTexts().Should().AllSatisfy(text => text.Should().BeNull("no pill outside a run"));
	}

	[AvaloniaFact]
	public void RunEnd_ClearsEveryPill()
	{
		Push(Running(line: 3, depth1: 2, depth2: 2));

		Push(PlcExecutionInfo.Empty);

		PassTexts().Should().AllSatisfy(text => text.Should().BeNull("the end of the run clears every pill"));
		VisiblePillText(OuterStart).Should().BeNull();
	}

	[AvaloniaFact]
	public void RenderedPill_HasTheApprovedStyle()
	{
		Push(Running(line: 3, depth1: 2, depth2: 2));

		var pill = Pill(OuterStart);
		var text = pill.GetVisualDescendants().OfType<TextBlock>().Single();
		((ISolidColorBrush)pill.Background!).Color.Should().Be(Colors.White);
		((ISolidColorBrush)pill.BorderBrush!).Color.Should().Be(_badgeViolet);
		pill.BorderThickness.Left.Should().Be(1);
		pill.CornerRadius.TopLeft.Should().Be(10);
		pill.Bounds.Height.Should().Be(20);
		pill.Padding.Left.Should().Be(8);
		pill.Padding.Right.Should().Be(8);
		((ISolidColorBrush)text.Foreground!).Color.Should().Be(_badgeViolet);
		text.FontSize.Should().Be(12);
		text.FontWeight.Should().Be(FontWeight.SemiBold);
	}

	[AvaloniaFact]
	public void NarrowActionCell_NameTrims_PillStaysWhole()
	{
		var actionColumn = _grid.DataGrid.Columns.Single(column => Equals(column.Tag, ActionColumn));
		actionColumn.MinWidth = 0;
		actionColumn.Width = new DataGridLength(90);
		Push(Running(line: 3, depth1: 2, depth2: 2));
		Dispatcher.UIThread.RunJobs();

		var pill = Pill(OuterStart);
		pill.Bounds.Width.Should().BeApproximately(pill.DesiredSize.Width, 0.5, "the pill keeps its full width");
		var name = _grid.PresenterAt(OuterStart, ActionColumn).GetVisualDescendants().OfType<TextBlock>().First();
		var natural = new TextBlock { Text = name.Text, FontSize = name.FontSize, FontFamily = name.FontFamily };
		natural.Measure(Size.Infinity);
		name.Bounds.Width.Should().BeLessThan(natural.DesiredSize.Width, "the action name gives up its width first");
	}

	[AvaloniaFact]
	public void InnerCounterStep_MovesTheInnerPillOnly()
	{
		Push(Running(line: 3, depth1: 2, depth2: 2));

		Push(Running(line: 3, depth1: 2, depth2: 3));

		PassTexts().Should().Equal(["(2/3)", null, "(3/5)", null, null, null, null]);
		VisiblePillText(InnerStart).Should().Be("(3/5)", "a step of the loop counter alone moves the pill");
	}

	// A recompute writes the resolved text into every row; a sentinel no resolver produces survives only if none ran.
	[AvaloniaFact]
	public void PollChangingOnlyStepTime_RecomputesNoPill()
	{
		const string Sentinel = "sentinel";
		Push(Running(line: 3, depth1: 2, depth2: 2));
		_grid.Surface.RecipeRows[1].LoopPassText = Sentinel;

		Push(Running(line: 3, depth1: 2, depth2: 2) with { StepCurrentTime = 4.5f });

		_grid.Surface.RecipeRows[1].LoopPassText.Should().Be(
			Sentinel,
			"a poll that only advances the step time recomputes nothing");
	}

	[AvaloniaFact]
	public void TransposedActionCell_ShowsThePill()
	{
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 1600, Height = 800, Content = view };
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		Push(Running(line: 3, depth1: 2, depth2: 2));

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(InnerStart)!;
		container.GetVisualDescendants().OfType<Border>()
			.Where(border => border.Classes.Contains(LoopBadge.PillClass) && border.IsEffectivelyVisible)
			.SelectMany(border => border.GetVisualDescendants().OfType<TextBlock>())
			.Select(text => text.Text)
			.Should().Equal(["(2/5)"]);
	}

	private static PlcExecutionInfo Running(int line, int depth1, int depth2)
	{
		return new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: line,
			StepCurrentTime: 0f,
			ForLoopCount1: depth1,
			ForLoopCount2: depth2,
			ForLoopCount3: 0);
	}

	private void Push(PlcExecutionInfo info)
	{
		_fixture.S7Service.PushExecutionState(info);
		Dispatcher.UIThread.RunJobs();
	}

	private List<string?> PassTexts()
	{
		return [.. _grid.Surface.RecipeRows.Select(row => row.LoopPassText)];
	}

	private Border Pill(int rowIndex)
	{
		return _grid.PresenterAt(rowIndex, ActionColumn).GetVisualDescendants().OfType<Border>()
			.Single(border => border.Classes.Contains(LoopBadge.PillClass));
	}

	private string? VisiblePillText(int rowIndex)
	{
		var pill = Pill(rowIndex);

		return pill.IsEffectivelyVisible ? pill.GetVisualDescendants().OfType<TextBlock>().Single().Text : null;
	}
}
