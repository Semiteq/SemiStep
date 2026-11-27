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

// The WithGroups action and group names are short, so the longest one sizes the step column below the cap.
[Trait("Component", "UI")]
[Trait("Area", "TransposedStepWidth")]
[Trait("Category", "Integration")]
public sealed class TransposedStepWidthTests : IAsyncLifetime
{
	private const double Cap = 240;
	private const string ConfigName = "WithGroups";
	private const string WaitNameLine = "ui_name: \"Wait\"";
	private const string ServiceActionsFile = "actions/service.yaml";
	private const string OpenItemLine = "1: \"Open\"";
	private const string ValveGroupFile = "groups/valve.yaml";
	private const string ActionKey = "action";
	private const string TargetKey = "target";

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

	[AvaloniaFact]
	public async Task GroupNameLongerThanEveryActionName_SizesTheColumn_AndShowsUntrimmed()
	{
		var longName = new string('Ш', 15);
		await StartWithLongValveItemAsync(longName);
		var widestAction = _fixture.RecipeMetadataRegistry.GetAllActions().Max(action => MeasureCell(action.UiName));
		MeasureCell(longName).Should().BeGreaterThan(widestAction, "precondition: the group name is the longest");

		var stepListBox = ShowTransposedGrid(RecipeTestDriver.WithGroupActionId);

		stepListBox.ContainerFromIndex(0)!.Bounds.Width.Should().BeLessThan(Cap, "precondition: the name fits");
		var group = ComboText(stepListBox, TargetKey);
		group.Text.Should().Be(longName, "precondition: the rendered step shows the long group name");
		IsTrimmed(group).Should().BeFalse("the column measures group names as well as action names");
		ToolTip.GetTip(group).Should().Be(longName, "a group cell carries its full text as a tooltip");
	}

	[AvaloniaFact]
	public async Task GroupNameLongerThanTheCap_GivesTheCap_AndTheCellTrimsWithAFullTooltip()
	{
		var longName = new string('Ш', 80);
		await StartWithLongValveItemAsync(longName);

		var stepListBox = ShowTransposedGrid(RecipeTestDriver.WithGroupActionId);

		stepListBox.ContainerFromIndex(0)!.Bounds.Width.Should().Be(Cap);
		var group = ComboText(stepListBox, TargetKey);
		group.Text.Should().Be(longName, "precondition: the rendered step shows the 80-character group name");
		IsTrimmed(group).Should().BeTrue("a group name past the cap ends in an ellipsis");
		ToolTip.GetTip(group).Should().Be(longName, "the tooltip always holds the full group name");
	}

	private async Task StartWithLongWaitNameAsync(string uiName)
	{
		await StartWithReplacedLineAsync(ServiceActionsFile, WaitNameLine, $"ui_name: \"{uiName}\"");
	}

	private async Task StartWithLongValveItemAsync(string itemName)
	{
		await StartWithReplacedLineAsync(ValveGroupFile, OpenItemLine, $"1: \"{itemName}\"");
	}

	private async Task StartWithReplacedLineAsync(string relativeFile, string line, string replacement)
	{
		_configCopy = TestDataCopier.PrepareValidCase(ConfigName);
		var file = Path.Combine(_configCopy.Path, relativeFile);
		var yaml = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
		yaml.Should().Contain(line, "precondition: the config holds the line the test replaces");
		await File.WriteAllTextAsync(
			file,
			yaml.Replace(line, replacement, StringComparison.Ordinal),
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
		return ComboText(stepListBox, ActionKey);
	}

	private static TextBlock ComboText(ListBox stepListBox, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;

		return container.GetVisualDescendants()
			.OfType<LazyComboCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey)
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
