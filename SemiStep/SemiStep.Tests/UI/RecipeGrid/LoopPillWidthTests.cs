using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// The WithGroups loop start renamed to the longest action name: with the widest pill it outgrows every other name,
// and in the headless glyph widths it still fits the transposed cap.
[Trait("Component", "UI")]
[Trait("Area", "LoopBadge")]
[Trait("Category", "Integration")]
public sealed class LoopPillWidthTests : IAsyncLifetime
{
	private const string ActionColumn = "action";
	private const string WidestPassText = "(99/99)";
	private const string LoopStartName = "ForLoop";
	private const string LoopStartNameLine = "ui_name: \"For\"";
	private const string ServiceActionsFile = "actions/service.yaml";

	private readonly UIFixture _fixture = new();
	private TempDirectory? _configCopy;
	private CanonicalGridWindow? _grid;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		_configCopy = TestDataCopier.PrepareValidCase("WithGroups");
		var actionsFile = Path.Combine(_configCopy.Path, ServiceActionsFile);
		var yaml = await File.ReadAllTextAsync(actionsFile, TestContext.Current.CancellationToken);
		yaml.Should().Contain(LoopStartNameLine, "precondition: the config names the loop start this way");
		await File.WriteAllTextAsync(
			actionsFile,
			yaml.Replace(LoopStartNameLine, $"ui_name: \"{LoopStartName}\"", StringComparison.Ordinal),
			TestContext.Current.CancellationToken);
		await _fixture.InitializeAsync(_configCopy.Path);
		var coordinator = _fixture.Coordinator;
		coordinator.NewRecipe();
		foreach (var actionId in new[]
		{
			RecipeTestDriver.ForLoopActionId, RecipeTestDriver.WaitActionId, RecipeTestDriver.EndForLoopActionId,
		})
		{
			coordinator.AppendStep(actionId).IsSuccess.Should().BeTrue();
		}

		coordinator.UpdateStepProperty(0, RecipeTestDriver.TaskColumn, "99").IsSuccess.Should().BeTrue();
	}

	public async ValueTask DisposeAsync()
	{
		_grid?.Dispose();
		_window?.Close();
		await _fixture.DisposeAsync();
		_configCopy?.Dispose();
	}

	[AvaloniaFact]
	public void CanonicalLoopStart_WithTheWidestPill_ShowsItsNameWhole()
	{
		_grid = new CanonicalGridWindow(_fixture);

		PushWidestPass();

		var presenter = _grid.PresenterAt(0, ActionColumn);
		VisiblePillText(presenter).Should().Be(WidestPassText, "precondition: the loop runs its 99th of 99 passes");
		var name = presenter.GetVisualDescendants().OfType<TextBlock>().First();
		name.Text.Should().Be(LoopStartName, "precondition: the cell shows the loop start name");
		name.Bounds.Width.Should().BeGreaterThanOrEqualTo(
			NaturalWidth(name),
			"the action column reserves the widest pill beside the loop start name");
	}

	[AvaloniaFact]
	public void TransposedLoopStart_WithTheWidestPill_ShowsItsNameWhole()
	{
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 1600, Height = 800, Content = view };
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		PushWidestPass();

		var presenter = ((ListBoxItem)stepListBox.ContainerFromIndex(0)!).GetVisualDescendants()
			.OfType<LazyComboCellPresenter>()
			.Single(cell => cell.DataContext is ParameterCellViewModel { Descriptor.ParameterKey: ActionColumn });
		VisiblePillText(presenter).Should().Be(WidestPassText, "precondition: the loop runs its 99th of 99 passes");
		var name = presenter.GetVisualDescendants().OfType<TextBlock>().First();
		name.Text.Should().Be(LoopStartName, "precondition: the step shows the loop start name");
		name.Bounds.Width.Should().BeGreaterThanOrEqualTo(
			NaturalWidth(name),
			"the step column reserves the widest pill beside the loop start name");
	}

	private void PushWidestPass()
	{
		_fixture.S7Service.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 1,
			StepCurrentTime: 0f,
			ForLoopCount1: 99,
			ForLoopCount2: 0,
			ForLoopCount3: 0));
		Dispatcher.UIThread.RunJobs();
	}

	private static string? VisiblePillText(Control presenter)
	{
		return presenter.GetVisualDescendants().OfType<Border>()
			.Where(border => border.Classes.Contains(LoopBadge.PillClass) && border.IsEffectivelyVisible)
			.SelectMany(border => border.GetVisualDescendants().OfType<TextBlock>())
			.Select(text => text.Text)
			.SingleOrDefault();
	}

	private static double NaturalWidth(TextBlock name)
	{
		var natural = new TextBlock
		{
			Text = name.Text,
			FontFamily = name.FontFamily,
			FontSize = name.FontSize,
			FontWeight = name.FontWeight,
			FontStyle = name.FontStyle,
			Padding = name.Padding,
		};
		natural.Measure(Size.Infinity);

		return natural.DesiredSize.Width;
	}
}
