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

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridHostTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;

	private readonly UIFixture _fixture = new();
	private ActiveRecipeGridSurface _router = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_router = _fixture.CreateActiveSurface();
		_router.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_router.CanonicalSurface.Dispose();
		_router.TransposedSurface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Host_WithRouterDataContext_RendersCanonicalViewWithRows()
	{
		var host = ShowHost();

		var canonicalView = host.GetVisualDescendants().OfType<CanonicalRecipeGridView>().SingleOrDefault();
		canonicalView.Should().NotBeNull("the host must render the canonical view as its content");

		var dataGrid = FindDataGrid(host);
		var dataGridRows = dataGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
		dataGridRows.Should().HaveCount(SeededStepCount);
	}

	[AvaloniaFact]
	public void Surface_ReturnsHostDataContext()
	{
		var host = ShowHost();

		host.Surface.Should().BeSameAs(_router);
	}

	[AvaloniaFact]
	public void ChildViews_ReceiveMatchingConcreteSurfaces_NotTheRouter()
	{
		var host = ShowHost();

		var canonicalView = (CanonicalRecipeGridView)host.Content!;
		canonicalView.DataContext.Should().BeSameAs(_router.CanonicalSurface);
		canonicalView.ViewModel.Should().BeSameAs(_router.CanonicalSurface);

		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		var transposedView = host.Content.Should().BeOfType<TransposedRecipeGridView>().Subject;
		transposedView.DataContext.Should().BeSameAs(_router.TransposedSurface);
		transposedView.ViewModel.Should().BeSameAs(_router.TransposedSurface);
	}

	[AvaloniaFact]
	public void ToggleOrientation_SwapsHostChild_BothWays()
	{
		var host = ShowHost();

		host.Content.Should().BeOfType<CanonicalRecipeGridView>();

		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();
		host.Content.Should().BeOfType<TransposedRecipeGridView>();

		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();
		host.Content.Should().BeOfType<CanonicalRecipeGridView>();
	}

	[AvaloniaFact]
	public void TransposedOrientation_RendersStepColumns_ProvingInitializeFanOut()
	{
		var host = ShowHost();

		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		_router.StepCount.Should().Be(SeededStepCount);
		var stepListBox = host.GetVisualDescendants().OfType<ListBox>().Single();
		stepListBox.ItemCount.Should().Be(SeededStepCount);
	}

	[AvaloniaFact]
	public void ToggleOrientation_SyncsVisualSelectionInIncomingView_BothWays()
	{
		var host = ShowHost();
		var dataGrid = FindDataGrid(host);
		dataGrid.SelectedIndex = 1;
		Dispatcher.UIThread.RunJobs();
		_router.SelectedStepIndices.Should().Equal(1);

		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = host.GetVisualDescendants().OfType<ListBox>().Single();
		stepListBox.SelectedIndex.Should().Be(1, "the transposed view must show the carried-over selection");
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue();

		stepListBox.SelectedIndex = 2;
		Dispatcher.UIThread.RunJobs();
		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		_router.SelectedStepIndices.Should().Equal(2);
		dataGrid.SelectedItems.Cast<object>().Should().ContainSingle()
			.Which.Should().BeSameAs(
				_router.CanonicalSurface.RecipeRows[2],
				"the canonical view must show the selection made while transposed was active");
	}

	[AvaloniaFact]
	public void IsEditing_TransposedArm_TracksEditorFocus_ThroughHostForwarding()
	{
		var host = ShowHost();
		_router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		host.IsEditing.Should().BeFalse();

		var stepListBox = host.GetVisualDescendants().OfType<ListBox>().Single();
		var editor = stepListBox.GetVisualDescendants().OfType<TextBox>().First(textBox =>
			textBox.DataContext is ParameterCellViewModel cell
			&& cell.Descriptor.ParameterKey == RecipeTestDriver.StepDurationColumn
			&& textBox.IsEnabled);
		editor.Focus().Should().BeTrue();

		host.IsEditing.Should().BeTrue();
	}

	[AvaloniaFact]
	public void NonRouterDataContext_ClearsContentAndChildDataContexts()
	{
		var host = ShowHost();
		host.Content.Should().NotBeNull();

		host.DataContext = new object();
		Dispatcher.UIThread.RunJobs();

		host.Content.Should().BeNull();
		host.IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	public void SelectionRequest_OnSurface_SelectsRowInHostedView()
	{
		var host = ShowHost();

		_router.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		var dataGrid = FindDataGrid(host);
		dataGrid.SelectedIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void IsEditing_TracksCellEditorLifecycle_ThroughHostForwarding()
	{
		var host = ShowHost();
		var dataGrid = FindDataGrid(host);

		host.IsEditing.Should().BeFalse();

		var durationColumn = dataGrid.Columns.Single(column =>
			column.Tag as string == RecipeTestDriver.StepDurationColumn);
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, durationColumn);
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		host.IsEditing.Should().BeTrue();

		dataGrid.CancelEdit();
		Dispatcher.UIThread.RunJobs();

		host.IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ContextMenu_OnWrappingPanel_OpensOnRightClickOverGridRow()
	{
		var contextMenu = new ContextMenu { Items = { new MenuItem { Header = "Add step" } } };
		var host = ShowHost(panel => panel.ContextMenu = contextMenu);
		var dataGrid = FindDataGrid(host);

		var window = _window!;
		var row = dataGrid.GetVisualDescendants().OfType<DataGridRow>().First();
		var center = row.TranslatePoint(
			new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window);
		center.Should().NotBeNull();

		window.MouseDown(center!.Value, MouseButton.Right);
		window.MouseUp(center.Value, MouseButton.Right);
		Dispatcher.UIThread.RunJobs();

		contextMenu.IsOpen.Should().BeTrue(
			"a right-click over grid rows must bubble to the wrapping panel's context menu");
	}

	private RecipeGridHost ShowHost(Action<Panel>? configurePanel = null)
	{
		var host = new RecipeGridHost { DataContext = _router };
		var panel = new Panel { Children = { host } };
		configurePanel?.Invoke(panel);

		_window = new Window
		{
			Width = 1200,
			Height = 600,
			Content = panel,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return host;
	}

	private static DataGrid FindDataGrid(RecipeGridHost host)
	{
		var dataGrid = host.GetVisualDescendants().OfType<DataGrid>().SingleOrDefault();
		dataGrid.Should().NotBeNull();

		return dataGrid!;
	}
}
