using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Boundary shapes for the pooled-presenter + lazy-cell transposed view: an empty recipe (no columns),
/// a single-step recipe (one realized column, all editors lazy), and the plan's core scenario — adding a
/// step while the viewport is scrolled far away, then auto-scrolling to it. These exercise the recycling
/// pool and the lazy display swap at the degenerate ends the per-suite fixtures (which seed 3 or 40 steps)
/// never reach.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedEdgeCaseTests : IAsyncLifetime
{
	private const int WideSeededStepCount = 40;

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface?.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void EmptyRecipe_View_RendersNameColumn_AndNoStepContainers()
	{
		_fixture.SeedRecipe(0);
		var (view, stepListBox) = InitializeAndShow(1200);

		_surface.StepColumns.Should().BeEmpty("an empty recipe projects no step columns");
		stepListBox.GetVisualDescendants().OfType<ListBoxItem>().Should().BeEmpty(
			"with no columns the panel realizes no containers");

		var parameterNameColumn = view.FindControl<ItemsControl>("ParameterNameColumn");
		parameterNameColumn.Should().NotBeNull();
		parameterNameColumn!.ItemCount.Should().Be(
			_surface.ParameterDescriptors.Count, "the frozen name column renders even with no steps");
	}

	[AvaloniaFact]
	public void SingleStepRecipe_View_RealizesOneColumn_WithAllEditorsLazy()
	{
		_fixture.SeedRecipe(1);
		var (_, stepListBox) = InitializeAndShow(1200);

		var containers = stepListBox.GetVisualDescendants().OfType<ListBoxItem>().ToList();
		containers.Should().HaveCount(1, "a single-step recipe realizes exactly one column");

		var container = containers[0];
		container.GetVisualDescendants().OfType<TextBox>().Should().BeEmpty(
			"no cell is in edit, so no TextBox editor is live (lazy display)");
		container.GetVisualDescendants().OfType<ComboBox>().Should().BeEmpty(
			"no cell is in edit, so no ComboBox editor is live (lazy display)");
		container.GetVisualDescendants().OfType<TransposedTextCellPresenter>().Should().NotBeEmpty(
			"property-text cells render their display presenter");
	}

	[AvaloniaFact]
	public void AddStepWhileScrolledFarAway_RealizesNewColumn_ShowsItsValue_AndStaysViewportBound()
	{
		_fixture.SeedRecipe(WideSeededStepCount);
		var (_, stepListBox) = InitializeAndShow(560);

		ScrollToHorizontalEnd(stepListBox);
		var boundedWhileScrolled = stepListBox.GetRealizedContainers().Count();
		boundedWhileScrolled.Should().BeLessThan(
			WideSeededStepCount / 2, "the far scroll must realize only a viewport of columns");

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();
		var newIndex = _surface.StepColumns.Count - 1;
		newIndex.Should().Be(WideSeededStepCount, "the append grew the recipe by one column");
		_fixture.Coordinator.UpdateStepProperty(newIndex, RecipeTestDriver.StepDurationColumn, "125")
			.IsSuccess.Should().BeTrue();

		stepListBox.ScrollIntoView(newIndex);
		Dispatcher.UIThread.RunJobs();

		stepListBox.GetRealizedContainers().Count().Should().BeLessThan(
			WideSeededStepCount / 2,
			"auto-scrolling to the freshly added step must recycle into a viewport, not realize the whole recipe");

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(newIndex)!;
		var expected = PropertyTimeEditingConverter.FormatForDisplay(
			_surface.StepColumns[newIndex].Row[RecipeTestDriver.StepDurationColumn],
			TimeFormatHelper.TimeHmsFormat);
		DisplayText(container, RecipeTestDriver.StepDurationColumn).Should().Be(
			expected, "the recycled slot rebinds its display to the freshly added column's own value");
		DisplayText(container, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:02:05", "the freshly added column shows the value written while it was off-screen");
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static string? DisplayText(ListBoxItem container, string parameterKey)
	{
		return container.GetVisualDescendants()
			.OfType<TransposedTextCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey)
			.GetVisualDescendants()
			.OfType<TextBlock>()
			.First()
			.Text;
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) InitializeAndShow(int width)
	{
		_surface = _fixture.CreateTransposedSurface();
		_surface.Initialize();

		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = width,
			Height = 800,
			Content = view,
		};

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
