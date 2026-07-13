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
/// Exercises the real transposed view with enough step-columns in a narrow window that the
/// horizontal VirtualizingStackPanel actually virtualizes and recycles containers — the shipped
/// cell templates and the execution-class binder must survive recycling.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedVirtualizationTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateTransposedSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void NarrowViewport_RealizedContainerCount_StaysViewportBoundAcrossScroll()
	{
		var stepListBox = ShowView();

		var initialCount = RealizedContainerCount(stepListBox);
		initialCount.Should().BeLessThan(
			SeededStepCount / 2, "the panel must realize a viewport of columns, not the recipe");

		ScrollToHorizontalEnd(stepListBox);

		RealizedContainerCount(stepListBox).Should().BeLessThan(
			SeededStepCount / 2, "scrolling must recycle containers, not accumulate them");
	}

	[AvaloniaFact]
	public void PendingEdit_CommitsWhenItsColumnIsRecycledOut()
	{
		var stepListBox = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();
		editor.Text = "45";

		ScrollToHorizontalEnd(stepListBox);

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			45f, "recycling fires LostFocus, which is the commit trigger");
	}

	[AvaloniaFact]
	public void RecycledContainer_CarriesExactlyOneSetOfExecutionClasses()
	{
		var stepListBox = ShowView();
		_surface.StepColumns[0].Row.IsCurrentStep = true;
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		var currentContainer = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		currentContainer.Classes
			.Count(className => className == RowExecutionClasses.CurrentStepClass)
			.Should().Be(1, "recycling must not stack duplicate class bindings");

		var idleContainer = (ListBoxItem)stepListBox.ContainerFromIndex(1)!;
		idleContainer.Classes.Should().NotContain(
			RowExecutionClasses.CurrentStepClass,
			"a container recycled from the current column must not leak its class");
	}

	private static int RealizedContainerCount(ListBox stepListBox)
	{
		return stepListBox.GetRealizedContainers().Count();
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static void ScrollToHorizontalStart(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(0, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static TextBox FindTextBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(textBox => textBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private ListBox ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 560,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();

		return stepListBox!;
	}
}
