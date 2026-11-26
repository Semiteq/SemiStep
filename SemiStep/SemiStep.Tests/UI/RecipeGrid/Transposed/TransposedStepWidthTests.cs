using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// The WithGroups names are short, so the longest one sizes the step column below the cap.
[Trait("Component", "UI")]
[Trait("Area", "TransposedStepWidth")]
[Trait("Category", "Integration")]
public sealed class TransposedStepWidthTests : IAsyncLifetime
{
	private const double Cap = 240;
	private const string ConfigName = "WithGroups";
	private const string WaitNameLine = "ui_name: \"Wait\"";
	private const string ServiceActionsFile = "actions/service.yaml";

	private readonly UIFixture _fixture = new();
	private bool _fixtureStarted;
	private TempDirectory? _configCopy;
	private Window? _window;

	public ValueTask InitializeAsync()
	{
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		if (_fixtureStarted)
		{
			await _fixture.DisposeAsync();
		}

		_configCopy?.Dispose();
	}

	[AvaloniaFact]
	public async Task LongestFixtureName_ShowsUntrimmed_InTheRenderedColumn()
	{
		await StartAsync(ConfigName);
		var longest = _fixture.RecipeMetadataRegistry.GetAllActions().MaxBy(action => MeasureCell(action.UiName))!;
		var stepListBox = ShowTransposedGrid(longest.Id);
		var calculator = new ColumnWidthCalculator(
			_fixture.RecipeMetadataRegistry,
			_fixture.AppConfiguration.GridStyle);

		var columnWidth = stepListBox.ContainerFromIndex(0)!.Bounds.Width;

		columnWidth.Should().Be(calculator.TransposedStepColumnWidth());
		columnWidth.Should().BeLessThan(Cap, "precondition: the longest name fits below the cap");
		var name = ActionNameText(stepListBox);
		name.Text.Should().Be(longest.UiName, "precondition: the step shows the longest name");
		IsTrimmed(name).Should().BeFalse("the column reserves the whole chrome of the name display");
	}

	[AvaloniaFact]
	public async Task LongNameBelowTheCap_ShowsUntrimmed_InTheRenderedColumn()
	{
		var longName = new string('Ш', 15);
		await StartWithLongWaitNameAsync(longName);

		var stepListBox = ShowTransposedGrid(RecipeTestDriver.WaitActionId);

		stepListBox.ContainerFromIndex(0)!.Bounds.Width.Should().BeLessThan(Cap, "precondition: the name fits");
		var name = ActionNameText(stepListBox);
		name.Text.Should().Be(longName, "precondition: the rendered step shows the long name");
		IsTrimmed(name).Should().BeFalse("a name below the cap shows whole");
	}

	[AvaloniaFact]
	public async Task NameLongerThanTheCap_GivesTheCap_AndTheCellTrimsWithAFullTooltip()
	{
		var longName = new string('Ш', 80);
		await StartWithLongWaitNameAsync(longName);

		var stepListBox = ShowTransposedGrid(RecipeTestDriver.WaitActionId);

		stepListBox.ContainerFromIndex(0)!.Bounds.Width.Should().Be(Cap);
		var name = ActionNameText(stepListBox);
		name.Text.Should().Be(longName, "precondition: the rendered step shows the 80-character name");
		IsTrimmed(name).Should().BeTrue("a name past the cap ends in an ellipsis");
		ToolTip.GetTip(name).Should().Be(longName, "the tooltip always holds the full name");
	}

	private async Task StartWithLongWaitNameAsync(string uiName)
	{
		_configCopy = TestDataCopier.PrepareValidCase(ConfigName);
		var actionsFile = Path.Combine(_configCopy.Path, ServiceActionsFile);
		var yaml = await File.ReadAllTextAsync(actionsFile, TestContext.Current.CancellationToken);
		yaml.Should().Contain(WaitNameLine, "precondition: the config names the wait action this way");
		await File.WriteAllTextAsync(
			actionsFile,
			yaml.Replace(WaitNameLine, $"ui_name: \"{uiName}\"", StringComparison.Ordinal),
			TestContext.Current.CancellationToken);

		await StartAsync(_configCopy.Path);
	}

	private async Task StartAsync(string config)
	{
		await _fixture.InitializeAsync(config);
		_fixtureStarted = true;
	}

	private double MeasureCell(string text)
	{
		var probe = new TextBlock { Text = text };
		GridFontApplier.ApplyCellFont(probe, _fixture.AppConfiguration.GridStyle);
		probe.Measure(Avalonia.Size.Infinity);

		return probe.DesiredSize.Width;
	}

	private static bool IsTrimmed(TextBlock text)
	{
		return text.TextLayout.TextLines.Any(line => line.HasCollapsed);
	}

	private static TextBlock ActionNameText(ListBox stepListBox)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;

		return container.GetVisualDescendants()
			.OfType<LazyComboCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel { Descriptor.ParameterKey: "action" })
			.GetVisualDescendants()
			.OfType<TextBlock>()
			.First();
	}

	private ListBox ShowTransposedGrid(int actionId)
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(actionId).IsSuccess.Should().BeTrue();
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 1600, Height = 800, Content = view };
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
