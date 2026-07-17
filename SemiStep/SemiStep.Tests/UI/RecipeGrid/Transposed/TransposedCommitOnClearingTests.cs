using System.Collections.Generic;
using System.Linq;

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

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Task 5 integration tests: the production template now wires <see cref="TransposedColumnsPanel"/>
/// (no test injector here, so the real <c>.axaml</c> swap is exercised). They pin the two edit-commit
/// paths under keep-attached recycle — both commit through the <c>ContainerClearing</c> unrealize hook,
/// because an open editor holds keyboard focus and becomes the <c>TabOnceActiveElement</c>, so its
/// container is unrealized (not deferred) on scroll-out whether or not its column is selected — plus
/// selection survival across a scroll round-trip and frozen name-column row alignment.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedCommitOnClearingTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;

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

	// Unselected-editor branch: the edited column is not the selection anchor, so scrolling it out
	// unrealizes its container (ContainerClearing), and the view's clearing hook flushes the pending edit.
	[AvaloniaFact]
	public void UnselectedColumnEditor_CommitsThroughContainerClearing_WhenScrolledOut()
	{
		var (view, stepListBox) = ShowView();

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeTrue("the F2 gesture opened the editor");
		stepListBox.SelectedIndex.Should().Be(-1, "focusing a cell must not select the column");

		var cleared = new List<Control>();
		stepListBox.ContainerClearing += (_, e) => cleared.Add(e.Container);

		editor.Text = "45";
		ScrollToHorizontalEnd(stepListBox);

		cleared.Should().Contain(
			container, "an unselected column's container is unrealized (not deferred) when scrolled out");
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			45f, "the ContainerClearing hook commits the pending edit as the unselected column recycles out");
		view.IsEditing.Should().BeFalse("the commit ended the edit; no editor is active after the recycle");
	}

	// Selected-editor branch: even when the column is selected, opening its editor puts keyboard focus on the
	// TextBox, so the editor - not the container - becomes the TabOnceActiveElement. The container is therefore
	// unrealized (not deferred) on scroll-out, and the pending edit still commits through the clearing hook.
	// (The container-level deferral protects a container-focused selected column with NO open editor; that path
	// is pinned by TransposedColumnsPanelContractTests.AnchorContainer_IsDeferredWhileScrolledOut.)
	[AvaloniaFact]
	public void SelectedColumnEditor_CommitsThroughContainerClearing_OnScrollOut()
	{
		var (view, stepListBox) = ShowView();

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var anchor = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		KeyboardNavigation.GetTabOnceActiveElement(stepListBox).Should().BeSameAs(
			anchor, "selecting the column makes its container the anchor");

		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		KeyboardNavigation.GetTabOnceActiveElement(stepListBox).Should().BeSameAs(
			editor, "the open editor holds keyboard focus, so it - not the container - is the tab-active element");
		editor.Text = "88";

		var cleared = new List<Control>();
		stepListBox.ContainerClearing += (_, e) => cleared.Add(e.Container);

		ScrollToHorizontalEnd(stepListBox);

		cleared.Should().Contain(
			anchor, "the open editor owns the anchor, so the selected column's container is unrealized on scroll-out");
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			88f, "the ContainerClearing hook commits the pending edit even for a selected column");
		view.IsEditing.Should().BeFalse("the commit ended the edit; no editor is active after the recycle");
	}

	[AvaloniaFact]
	public void Selection_SurvivesScrollAwayAndBack_InModelAndContainer()
	{
		var (_, stepListBox) = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		stepListBox.Selection.SelectedIndexes.Should().Contain(
			1, "the selection survives the scroll round-trip in the model");
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue(
			"the re-realized container reflects the surviving selection");
	}

	// The frozen parameter-name column on the left must stay row-aligned with the scrolling step columns:
	// each name cell shares its vertical band with the same-row cell of a realized step column.
	[AvaloniaFact]
	public void FrozenNameColumn_StaysRowAligned_WithScrolledStepColumn()
	{
		var (view, stepListBox) = ShowView();

		ScrollToHorizontalEnd(stepListBox);

		var scrolledContainer = stepListBox.GetRealizedContainers()
			.Cast<ListBoxItem>()
			.OrderBy(container => container.Bounds.X)
			.First();

		var nameColumn = view.FindControl<ItemsControl>("ParameterNameColumn")!;
		var nameCellTops = CellTops(nameColumn.GetVisualDescendants(), "transposed-name-cell");
		var stepCellTops = CellTops(scrolledContainer.GetVisualDescendants(), "transposed-cell");

		stepCellTops.Should().HaveCount(
			_surface.ParameterDescriptors.Count, "the step column carries one cell per parameter");
		nameCellTops.Should().HaveCount(
			stepCellTops.Count, "the frozen name column carries the same number of rows");

		for (var row = 0; row < nameCellTops.Count; row++)
		{
			stepCellTops[row].Should().BeApproximately(
				nameCellTops[row], 0.5, $"row {row} of the step column must align with the frozen name row");
		}
	}

	private List<double> CellTops(IEnumerable<Visual> descendants, string cellClass)
	{
		return descendants
			.OfType<Border>()
			.Where(border => border.Classes.Contains(cellClass))
			.Select(border => border.TranslatePoint(default, _window!)!.Value.Y)
			.OrderBy(top => top)
			.ToList();
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

	private static TransposedTextCellPresenter FindTextPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TransposedTextCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static TextBox FindTextBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(textBox => textBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private TextBox EnterTextEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindTextPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindTextBox(stepListBox, columnIndex, parameterKey);
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();

		// No injector: the production .axaml template now wires TransposedColumnsPanel directly.
		_window = new Window
		{
			Width = NarrowWindowWidth,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox!);
	}
}
