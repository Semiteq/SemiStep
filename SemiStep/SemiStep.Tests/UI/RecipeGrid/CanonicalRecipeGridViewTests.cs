using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class CanonicalRecipeGridViewTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void View_WithThreeStepSurface_RendersOneRowPerStep()
	{
		var dataGrid = ShowView();

		var dataGridRows = dataGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
		dataGridRows.Should().HaveCount(SeededStepCount);
		dataGrid.Columns.Should().NotBeEmpty("the view must build columns from the surface's ColumnBuilder");
	}

	[AvaloniaFact]
	public void SelectionRequest_OnSurface_SelectsCorrespondingRow()
	{
		var dataGrid = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		dataGrid.SelectedIndex.Should().Be(1);
		dataGrid.SelectedItem.Should().BeSameAs(_surface.RecipeRows[1]);
	}

	[AvaloniaFact]
	public void SelectionRequest_WithNull_ClearsGridSelection()
	{
		var dataGrid = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		_surface.RequestSelection(null);
		Dispatcher.UIThread.RunJobs();

		dataGrid.SelectedIndex.Should().Be(-1);
	}

	[AvaloniaFact]
	public void SelectionRequest_OutOfRange_LeavesSelectionUnchanged()
	{
		var dataGrid = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		_surface.RequestSelection(99);
		Dispatcher.UIThread.RunJobs();

		dataGrid.SelectedIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void GridSelection_PropagatesStepIndicesToSurface()
	{
		var dataGrid = ShowView();

		dataGrid.SelectedIndex = 2;
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2);
		_surface.SelectedStepIndex.Should().Be(2);
	}

	[AvaloniaFact]
	public void GridMultiSelection_AddedInReverseOrder_PropagatesAscendingStepIndices()
	{
		var dataGrid = ShowView();

		dataGrid.SelectedItems.Add(_surface.RecipeRows[2]);
		Dispatcher.UIThread.RunJobs();
		dataGrid.SelectedItems.Add(_surface.RecipeRows[0]);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 2);
		_surface.SelectedStepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void LoadingRowWiring_StampsForDepthClasses_OnRenderedRows()
	{
		var dataGrid = ShowView();

		_surface.RecipeRows[1].ForDepth = 1;
		Dispatcher.UIThread.RunJobs();

		var dataGridRows = dataGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
		var stampedRow = dataGridRows.Single(row => ReferenceEquals(row.DataContext, _surface.RecipeRows[1]));
		stampedRow.Classes.Contains(RowExecutionClasses.ForDepth1Class).Should().BeTrue(
			"the view's LoadingRow wiring must bind the ForDepth pseudo-classes on real row containers");
	}

	[AvaloniaFact]
	public void BeginEdit_OnInapplicableColumn_IsCancelled_AndIsEditingStaysFalse()
	{
		var (view, dataGrid) = ShowViewWithControl();

		var row = _surface.RecipeRows[0];
		var inapplicableColumn = dataGrid.Columns.FirstOrDefault(column =>
			!column.IsReadOnly && column.Tag is string key && !row.IsApplicable(key));
		inapplicableColumn.Should().NotBeNull(
			"the seeded Wait step must have at least one editable column it does not use");

		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, inapplicableColumn!);
		var began = dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		began.Should().BeFalse("the view must cancel edits on columns the step's action does not use");
		view.IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	public void BeginEdit_OnApplicableColumn_SetsIsEditing_UntilEditEnds()
	{
		var (view, dataGrid) = ShowViewWithControl();

		var durationColumn = dataGrid.Columns.Single(column =>
			column.Tag as string == RecipeTestDriver.StepDurationColumn);

		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, durationColumn);
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue();

		dataGrid.CancelEdit();
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeFalse();
	}

	private DataGrid ShowView()
	{
		var (_, dataGrid) = ShowViewWithControl();

		return dataGrid;
	}

	private (CanonicalRecipeGridView View, DataGrid DataGrid) ShowViewWithControl()
	{
		var view = new CanonicalRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 600,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var dataGrid = view.FindControl<DataGrid>("RecipeGrid");
		dataGrid.Should().NotBeNull();

		return (view, dataGrid!);
	}

}
