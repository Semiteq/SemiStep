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

	// A focused editor holding uncommitted text that gets recycled across a scroll must never leak
	// that text into any other column's cell. The recycle-out commit may write column 0 (asserted
	// separately); every other column must keep its own value.
	[AvaloniaFact]
	public void FocusedEditorWithPendingText_RecycledAcrossScroll_DoesNotCorruptOtherCells()
	{
		var stepListBox = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		var before = new object?[SeededStepCount];
		for (var i = 0; i < SeededStepCount; i++)
		{
			before[i] = _surface.StepColumns[i].Row[RecipeTestDriver.StepDurationColumn];
		}

		editor.Focus();
		editor.Text = "777";

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		for (var i = 1; i < SeededStepCount; i++)
		{
			_surface.StepColumns[i].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
				before[i], $"recycling a focused editor must not write stale text into column {i}");
		}
	}

	// After a recycled container is reused for a far column, its editor must show that column's own
	// value (the OneWay display binding rebinds), never a stale value from the column it was built on.
	[AvaloniaFact]
	public void RecycledTextEditor_ShowsRebindTargetCellValue_AfterScroll()
	{
		var lastIndex = SeededStepCount - 1;
		_fixture.Coordinator.UpdateStepProperty(lastIndex, RecipeTestDriver.StepDurationColumn, "125")
			.IsSuccess.Should().BeTrue();
		var stepListBox = ShowView();

		ScrollToHorizontalEnd(stepListBox);

		var editor = FindTextBox(stepListBox, lastIndex, RecipeTestDriver.StepDurationColumn);
		var expected = PropertyTimeEditingConverter.FormatForDisplay(
			_surface.StepColumns[lastIndex].Row[RecipeTestDriver.StepDurationColumn],
			TimeFormatHelper.TimeHmsFormat);

		editor.Text.Should().Be(expected);
		editor.Text.Should().Be("00:02:05", "the recycled editor shows the rebind-target cell's formatted value");
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
