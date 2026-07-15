using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "45";

		ScrollToHorizontalEnd(stepListBox);

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			45f, "the commit-before-rebind hook flushes pending text to the captured cell as its column recycles out");
	}

	// The container-reuse fix: a column scrolled out and a new one scrolled onto the SAME container
	// rebinds the cell subtree instead of rebuilding it. Under the lazy display the reused subtree is the
	// text cell's display presenter (its editor is built only on edit entry); it must survive the recycle
	// and show the recycled-in column's own data.
	[AvaloniaFact]
	public void RecycledContainer_ReusesSamePresenterInstance_ReboundToNewColumn()
	{
		var stepListBox = ShowView();
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var presenterBefore = FindTextPresenter(container, RecipeTestDriver.StepDurationColumn);
		var columnBefore = (StepColumnViewModel)container.DataContext!;

		ScrollToHorizontalEnd(stepListBox);

		stepListBox.GetRealizedContainers().Should().Contain(
			container, "the container is recycled onto a new column, not discarded and rebuilt");

		var columnAfter = (StepColumnViewModel)container.DataContext!;
		columnAfter.Should().NotBeSameAs(columnBefore, "the recycled container must rebind to a different column");

		var presenterAfter = FindTextPresenter(container, RecipeTestDriver.StepDurationColumn);
		presenterAfter.Should().BeSameAs(
			presenterBefore, "the cell subtree is reused across recycle (rebind, not rebuild)");
		((ParameterCellViewModel)presenterAfter.DataContext!).Row.Should().BeSameAs(
			columnAfter.Row, "the reused presenter shows the recycled-in column's own cell");
	}

	// Commit-before-rebind: a focused editor holding pending text whose column is recycled out commits
	// that text to the cell the user was editing (the captured cell) before the pooled slot is rebound,
	// then the SAME reused presenter shows the recycled-in column's value on its display.
	[AvaloniaFact]
	public void FocusedEditor_PendingText_CommitsToCapturedCell_ThenReusedSlotShowsRebindTarget()
	{
		var stepListBox = ShowView();
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var presenter = FindTextPresenter(container, RecipeTestDriver.StepDurationColumn);
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		var capturedColumn = (StepColumnViewModel)container.DataContext!;

		editor.Text = "88";

		ScrollToHorizontalEnd(stepListBox);

		capturedColumn.Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			88f, "recycling the editing column out commits its pending text to the captured cell, never the rebind target");

		var reusedPresenter = FindTextPresenter(container, RecipeTestDriver.StepDurationColumn);
		reusedPresenter.Should().BeSameAs(presenter, "the pooled cell subtree is reused, not rebuilt");
		reusedPresenter.IsEditing.Should().BeFalse("the recycled slot drops back to its display");
		var columnAfter = (StepColumnViewModel)container.DataContext!;
		columnAfter.Should().NotBeSameAs(capturedColumn, "the container rebound to a different column");
		var expected = PropertyTimeEditingConverter.FormatForDisplay(
			columnAfter.Row[RecipeTestDriver.StepDurationColumn], TimeFormatHelper.TimeHmsFormat);
		DisplayText(container, RecipeTestDriver.StepDurationColumn).Should().Be(
			expected, "the reused slot rebinds its display to show the recycled-in column's value");
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
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		var before = new object?[SeededStepCount];
		for (var i = 0; i < SeededStepCount; i++)
		{
			before[i] = _surface.StepColumns[i].Row[RecipeTestDriver.StepDurationColumn];
		}

		editor.Text = "777";

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		for (var i = 1; i < SeededStepCount; i++)
		{
			_surface.StepColumns[i].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
				before[i], $"recycling a focused editor must not write stale text into column {i}");
		}
	}

	// After a recycled container is reused for a far column, its display must show that column's own
	// value (the OneWay display binding rebinds), never a stale value from the column it was built on.
	[AvaloniaFact]
	public void RecycledTextCell_ShowsRebindTargetCellValue_AfterScroll()
	{
		var lastIndex = SeededStepCount - 1;
		_fixture.Coordinator.UpdateStepProperty(lastIndex, RecipeTestDriver.StepDurationColumn, "125")
			.IsSuccess.Should().BeTrue();
		var stepListBox = ShowView();

		ScrollToHorizontalEnd(stepListBox);

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(lastIndex)!;
		var expected = PropertyTimeEditingConverter.FormatForDisplay(
			_surface.StepColumns[lastIndex].Row[RecipeTestDriver.StepDurationColumn],
			TimeFormatHelper.TimeHmsFormat);

		DisplayText(container, RecipeTestDriver.StepDurationColumn).Should().Be(expected);
		DisplayText(container, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:02:05", "the recycled slot shows the rebind-target cell's formatted value");
	}

	// The in-place commit-before-rebind hook (TransposedColumnCellsPresenter.OnDataContextBeginUpdate ->
	// CommitActiveEditor) fires without any detach. Forcing a pooled presenter's whole-column DataContext to
	// a new column while a text cell holds a live pending edit must commit that edit to the captured cell
	// before the slots rebind, never leaking it into the rebind-target column. This drives the panel hook
	// directly, unlike the recycle tests which reach commit through the detach/LostFocus path.
	[AvaloniaFact]
	public void InPlaceColumnRebind_CommitsPendingEdit_ViaPanelHook()
	{
		var stepListBox = ShowView();
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var presenter = container.GetVisualDescendants().OfType<TransposedColumnCellsPresenter>().Single();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		var capturedColumn = (StepColumnViewModel)container.DataContext!;
		var rebindColumn = _surface.StepColumns[1];
		rebindColumn.Should().NotBeSameAs(capturedColumn);
		var rebindTargetBefore = rebindColumn.Row[RecipeTestDriver.StepDurationColumn];

		editor.Text = "63";

		presenter.DataContext = rebindColumn;
		Dispatcher.UIThread.RunJobs();

		capturedColumn.Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			63f, "the panel's OnDataContextBeginUpdate commits the pending edit to the captured cell before rebind");
		rebindColumn.Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			rebindTargetBefore, "the in-place rebind target must never receive the captured cell's pending text");
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
		return FindTextBox((ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!, parameterKey);
	}

	private static TextBox FindTextBox(ListBoxItem container, string parameterKey)
	{
		return container.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(textBox => textBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static TransposedTextCellPresenter FindTextPresenter(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		return FindTextPresenter((ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!, parameterKey);
	}

	private static TransposedTextCellPresenter FindTextPresenter(ListBoxItem container, string parameterKey)
	{
		return container.GetVisualDescendants()
			.OfType<TransposedTextCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static string? DisplayText(ListBoxItem container, string parameterKey)
	{
		return FindTextPresenter(container, parameterKey)
			.GetVisualDescendants()
			.OfType<TextBlock>()
			.First()
			.Text;
	}

	private TextBox EnterTextEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindTextPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindTextBox(stepListBox, columnIndex, parameterKey);
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
